using System.Collections.Concurrent;
using System.Threading.Channels;
using FractalFlameCurator.Ai;
using FractalFlameCurator.Models;
using FractalFlameCurator.Storage;

namespace FractalFlameCurator.Pipeline;

public sealed record AiScoringStatus(
    bool IsRunning,
    bool IsPaused,
    bool IsTraining,
    int PendingImages,
    int ScoredImages,
    int Failed,
    int Completed,
    int Total,
    string? ModelVersion,
    DeviceDiagnostics Diagnostics);

public sealed class ContinuousAiScoringService : IAsyncDisposable
{
    private readonly IPreferenceScoringBackend _backend;
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, PreferenceScore> _scores = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _knownSourceIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _inferenceGate = new(1, 1);
    private Channel<string>? _pending;
    private CancellationTokenSource? _cancellation;
    private Task? _worker;
    private Task? _scanner;
    private FileSystemWatcher? _watcher;
    private TaskCompletionSource<bool> _resume = CompletedSignal();
    private string _renderedDirectory = string.Empty;
    private RatingStore? _ratings;
    private string? _modelVersion;
    private bool _training;
    private int _failed;
    private int _completed;
    private int _total;
    private int _pendingCount;

    public ContinuousAiScoringService(IPreferenceScoringBackend backend) => _backend = backend;

    public event Action<PreferenceScore>? ImageScored;
    public event Action<Exception>? ScoringFailed;
    public event Action<TrainingResult>? TrainingCompleted;

    public DeviceDiagnostics Diagnostics => _backend.Diagnostics;
    public IReadOnlyDictionary<string, PreferenceScore> Scores => _scores;

    public AiScoringStatus Status
    {
        get
        {
            var cancellation = _cancellation;
            var running = cancellation is not null && !cancellation.IsCancellationRequested && _worker is { IsCompleted: false };
            return new AiScoringStatus(running, !_resume.Task.IsCompleted, _training, Volatile.Read(ref _pendingCount), _scores.Count, Volatile.Read(ref _failed), Volatile.Read(ref _completed), Volatile.Read(ref _total), _modelVersion ?? _backend.ActiveModelVersion, _backend.Diagnostics);
        }
    }

    public async Task<DeviceDiagnostics> InitializeAsync(CancellationToken cancellationToken = default) => await _backend.GetDiagnosticsAsync(cancellationToken);

    public void Start(string outputDirectory, RatingStore ratings)
    {
        lock (_gate)
        {
            if (_cancellation is not null) throw new InvalidOperationException("AI scoring is already running.");
            if (!_backend.Diagnostics.AiReady) throw new InvalidOperationException("AI scoring is disabled. CUDA and a usable PyTorch DINOv2 runtime are required.");
            _renderedDirectory = Path.Combine(Path.GetFullPath(outputDirectory), "rendered");
            Directory.CreateDirectory(_renderedDirectory);
            _ratings = ratings;
            _pending = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            _cancellation = new CancellationTokenSource();
            _resume = CompletedSignal();
            _failed = 0;
            _completed = 0;
            _total = 0;
            _pendingCount = 0;
            lock (_knownSourceIds) _knownSourceIds.Clear();
            _watcher = new FileSystemWatcher(_renderedDirectory, "*.png") { NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite, EnableRaisingEvents = true };
            _watcher.Created += RenderedFileChanged;
            _watcher.Changed += RenderedFileChanged;
            _worker = Task.Run(() => ConsumeAsync(_cancellation.Token));
            _scanner = Task.Run(() => ScanAsync(_cancellation.Token));
            foreach (var path in Directory.EnumerateFiles(_renderedDirectory, "*.png", SearchOption.TopDirectoryOnly)) EnqueueIfCandidate(path);
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (_cancellation is null) return;
            if (_resume.Task.IsCompleted) _resume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void Resume() => _resume.TrySetResult(true);

    public async Task StopAsync()
    {
        Task? worker;
        Task? scanner;
        lock (_gate)
        {
            if (_cancellation is null) return;
            _cancellation.Cancel();
            _pending?.Writer.TryComplete();
            _resume.TrySetResult(true);
            _watcher?.Dispose();
            _watcher = null;
            worker = _worker;
            scanner = _scanner;
        }
        if (worker is not null)
        {
            try { await worker; } catch (OperationCanceledException) { }
        }
        if (scanner is not null)
        {
            try { await scanner; } catch (OperationCanceledException) { }
        }
        lock (_gate)
        {
            _cancellation?.Dispose();
            _cancellation = null;
            _worker = null;
            _scanner = null;
            _pending = null;
        }
    }

    public async Task<TrainingResult> TrainAsync(DatasetSnapshot snapshot, string modelDirectory, CancellationToken cancellationToken = default)
    {
        if (!_backend.Diagnostics.AiReady) throw new InvalidOperationException("AI training is disabled. CUDA and a usable PyTorch DINOv2 runtime are required.");
        _training = true;
        await _inferenceGate.WaitAsync(cancellationToken);
        TrainingResult result;
        try
        {
            result = await _backend.TrainAsync(snapshot, modelDirectory, cancellationToken);
        }
        catch
        {
            _training = false;
            throw;
        }
        finally
        {
            _inferenceGate.Release();
        }
        try
        {
            _modelVersion = result.ModelVersion;
            _scores.Clear();
            await RescoreExistingAsync(cancellationToken);
            TrainingCompleted?.Invoke(result);
            return result;
        }
        finally { _training = false; }
    }

    public async Task RescoreExistingAsync(CancellationToken cancellationToken = default)
    {
        if (_ratings is null || string.IsNullOrWhiteSpace(_renderedDirectory)) return;
        var paths = Directory.EnumerateFiles(_renderedDirectory, "*.png", SearchOption.TopDirectoryOnly)
            .Where(SourceArchive.IsCompleteCandidate)
            .Where(path => _ratings.FindRating(path) is null)
            .ToArray();
        if (paths.Length == 0) return;
        Interlocked.Add(ref _total, paths.Length);
        await ScorePathsAsync(paths, cancellationToken);
    }

    public async Task<int> RescoreRatedAsync(RatingStore ratings, CancellationToken cancellationToken = default)
    {
        if (!_backend.Diagnostics.AiReady) throw new InvalidOperationException("AI scoring is disabled. CUDA and a usable PyTorch DINOv2 runtime are required.");
        var paths = ratings.EnumerateRatedImagePaths();
        if (paths.Count == 0) return 0;
        Interlocked.Add(ref _total, paths.Count);
        await ScorePathsAsync(paths, cancellationToken, allowUnpairedImages: true);
        return paths.Count;
    }

    public bool TryGetScore(string sourceId, out PreferenceScore score) => _scores.TryGetValue(sourceId, out score!);

    public async Task InvalidateAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        await _inferenceGate.WaitAsync(cancellationToken);
        try
        {
            _scores.TryRemove(sourceId, out _);
            lock (_knownSourceIds) _knownSourceIds.Remove(sourceId);
        }
        finally { _inferenceGate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await _backend.DisposeAsync();
        _inferenceGate.Dispose();
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string path;
            try { path = await _pending!.Reader.ReadAsync(cancellationToken); }
            catch (ChannelClosedException) { return; }
            catch (OperationCanceledException) { return; }
            Interlocked.Decrement(ref _pendingCount);
            try
            {
                await WaitIfPaused(cancellationToken);
                await WaitForCompleteCandidateAsync(path, cancellationToken);
                if (_ratings?.FindRating(path) is not null) continue;
                await ScorePathsAsync([path], cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (FileNotFoundException) { }
            catch (Exception exception)
            {
                Interlocked.Increment(ref _failed);
                ScoringFailed?.Invoke(exception);
            }
        }
    }

    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(_renderedDirectory, "*.png", SearchOption.TopDirectoryOnly)) EnqueueIfCandidate(path);
                await Task.Delay(250, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (DirectoryNotFoundException) { await Task.Delay(250, cancellationToken); }
        }
    }

    private async Task ScorePathsAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken, bool allowUnpairedImages = false)
    {
        await _inferenceGate.WaitAsync(cancellationToken);
        try
        {
            var existing = paths.Where(path => allowUnpairedImages ? File.Exists(path) : SourceArchive.IsCompleteCandidate(path)).ToArray();
            if (existing.Length == 0) return;
            var scores = await _backend.ScoreAsync(existing, cancellationToken);
            foreach (var score in scores)
            {
                var renamedPath = RenameScoredPair(score.ImagePath, score.Score, allowUnpairedImages);
                var finalScore = score with { ImagePath = renamedPath };
                _scores[finalScore.SourceId] = finalScore;
                _knownSourceIds.Add(finalScore.SourceId);
                Interlocked.Increment(ref _completed);
                ImageScored?.Invoke(finalScore);
            }
        }
        finally { _inferenceGate.Release(); }
    }

    private void EnqueueIfCandidate(string path)
    {
        if (_ratings?.FindRating(path) is not null) return;
        var sourceId = CandidateFileNaming.GetSourceId(Path.GetFileName(path));
        lock (_knownSourceIds)
        {
            if (!_knownSourceIds.Add(sourceId)) return;
        }
        if (_pending?.Writer.TryWrite(path) == true)
        {
            Interlocked.Increment(ref _pendingCount);
            Interlocked.Increment(ref _total);
        }
    }

    private void RenderedFileChanged(object sender, FileSystemEventArgs args) => EnqueueIfCandidate(args.FullPath);

    private async Task WaitForCompleteCandidateAsync(string path, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (SourceArchive.IsCompleteCandidate(path)) return;
            await Task.Delay(100, cancellationToken);
        }
        throw new FileNotFoundException("The rendered image did not become a complete candidate.", path);
    }

    private string RenameScoredPair(string imagePath, double score, bool allowUnpairedImage)
    {
        var directory = Path.GetDirectoryName(imagePath) ?? _renderedDirectory;
        var flamePath = SourceArchive.FindMatchingFlamePath(imagePath);
        if (flamePath is null && !allowUnpairedImage) throw new FileNotFoundException("The matching .flame source is not available.", imagePath);
        var destinationImagePath = Path.Combine(directory, CandidateFileNaming.WithScorePrefix(Path.GetFileName(imagePath), score));
        var destinationFlamePath = flamePath is null
            ? null
            : Path.Combine(directory, CandidateFileNaming.WithScorePrefix(Path.GetFileName(flamePath), score));
        if (string.Equals(imagePath, destinationImagePath, StringComparison.OrdinalIgnoreCase)
            && (flamePath is null || string.Equals(flamePath, destinationFlamePath, StringComparison.OrdinalIgnoreCase))) return imagePath;
        var token = Guid.NewGuid().ToString("N");
        var temporaryImagePath = imagePath + ".scoring-" + token + ".tmp";
        var temporaryFlamePath = flamePath is null ? null : flamePath + ".scoring-" + token + ".tmp";
        File.Move(imagePath, temporaryImagePath);
        try
        {
            if (flamePath is not null) File.Move(flamePath, temporaryFlamePath!);
            try
            {
                File.Move(temporaryImagePath, destinationImagePath, true);
                if (temporaryFlamePath is not null) File.Move(temporaryFlamePath, destinationFlamePath!, true);
                return destinationImagePath;
            }
            catch
            {
                if (File.Exists(temporaryImagePath) && !File.Exists(imagePath)) File.Move(temporaryImagePath, imagePath);
                if (temporaryFlamePath is not null && File.Exists(temporaryFlamePath) && !File.Exists(flamePath)) File.Move(temporaryFlamePath, flamePath!);
                throw;
            }
        }
        catch
        {
            if (File.Exists(temporaryImagePath) && !File.Exists(imagePath)) File.Move(temporaryImagePath, imagePath);
            throw;
        }
    }

    private Task WaitIfPaused(CancellationToken cancellationToken) => _resume.Task.WaitAsync(cancellationToken);

    private static TaskCompletionSource<bool> CompletedSignal()
    {
        var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.TrySetResult(true);
        return signal;
    }
}
