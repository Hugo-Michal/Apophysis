using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FractalFlameCurator.Ai;
using FractalFlameCurator.Generation;
using FractalFlameCurator.Models;
using FractalFlameCurator.Pipeline;
using FractalFlameCurator.Rendering;
using FractalFlameCurator.Storage;
using Forms = System.Windows.Forms;
using WpfButton = System.Windows.Controls.Button;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMessageBox = System.Windows.MessageBox;
using WpfPoint = System.Windows.Point;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfBrushes = System.Windows.Media.Brushes;

namespace FractalFlameCurator;

public partial class MainWindow : Window
{
    private readonly CpuFlameRenderer _renderer = new();
    private readonly ContinuousRenderService _renderService;
    private readonly DinoV2PreferenceBackend _aiBackend;
    private readonly ContinuousAiScoringService _aiService;
    private readonly CandidateCatalog _catalog = new();
    private ContinuousRenderOptions? _sessionOptions;
    private SourceArchive? _archive;
    private RatingStore? _ratingStore;
    private RenderedArtifact? _current;
    private RenderedArtifact? _lastRated;
    private RenderedArtifact? _deferredAfterUndo;
    private bool _aiEnabled;
    private double _zoomScale = 1;
    private int _imagePixelWidth;
    private int _imagePixelHeight;
    private bool _fitToViewport = true;
    private bool _applyingZoom;
    private bool _closing;
    private DateTime _nextUiRefresh;

    public MainWindow()
    {
        InitializeComponent();
        _renderService = new ContinuousRenderService(new FlameGenerator(), _renderer);
        _renderService.ImageReady += RenderService_ImageReady;
        _renderService.RenderFailed += RenderService_RenderFailed;
        _aiBackend = new DinoV2PreferenceBackend(new DinoV2BackendOptions { ScriptPath = Path.Combine(AppContext.BaseDirectory, "Ai", "dinov2_service.py") });
        _aiService = new ContinuousAiScoringService(_aiBackend);
        _aiService.ImageScored += AiService_ImageScored;
        _aiService.ScoringFailed += AiService_ScoringFailed;
        _aiService.TrainingCompleted += AiService_TrainingCompleted;
        OutputDirectoryTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ApophysisCurator");
        SeedTextBox.Text = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
        BatchLimitTextBox.Text = "100";
        WorkersTextBox.Text = Math.Max(1, Math.Min(4, Environment.ProcessorCount)).ToString(CultureInfo.InvariantCulture);
        QueueCapacityTextBox.Text = "4";
        SampleBudgetTextBox.Text = "20000000";
        OversampleComboBox.SelectedIndex = 0;
        FilterRadiusTextBox.Text = "0.5";
        GammaTextBox.Text = "2.2";
        BrightnessTextBox.Text = "1.0";
        VibrancyTextBox.Text = "1.0";
        PaletteComboBox.ItemsSource = PaletteDefinition.BuiltIns;
        PaletteComboBox.SelectedIndex = 0;
        BackendTextBlock.Text = $"Renderer: {_renderer.Status.Backend} · {_renderer.Status.Device}\n{_renderer.Status.Detail}";
        AiStatusTextBlock.Text = "AI diagnostics are loading…";
        CompositionTarget.Rendering += UpdateStatus;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var diagnostics = await _aiService.InitializeAsync();
            UpdateAiDiagnostics(diagnostics);
        }
        catch (Exception exception)
        {
            UpdateAiDiagnostics(DeviceDiagnostics.Unavailable(exception.Message));
        }
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            EnsureWorkspace();
            var palette = (PaletteComboBox.SelectedItem as PaletteDefinition) ?? PaletteDefinition.Monochrome;
            _sessionOptions = new ContinuousRenderOptions
            {
                OutputDirectory = OutputDirectoryTextBox.Text.Trim(),
                BatchLimit = ParseInt(BatchLimitTextBox, 100, 1, 1_000_000),
                WorkerCount = ParseInt(WorkersTextBox, 1, 1, Math.Max(1, Environment.ProcessorCount)),
                QueueCapacity = ParseInt(QueueCapacityTextBox, 4, 1, 64),
                Seed = ParseLong(SeedTextBox, DateTime.UtcNow.Ticks),
                Palette = palette,
                RenderSettings = new RenderSettings
                {
                    Width = 2048,
                    Height = 2048,
                    SampleBudget = ParseInt(SampleBudgetTextBox, 20_000_000, 100, 500_000_000),
                    Oversample = ParseInt((OversampleComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString(), 1, 1, 3),
                    FilterRadius = ParseDouble(FilterRadiusTextBox, 0.5, 0, 3),
                    Gamma = ParseDouble(GammaTextBox, 2.2, 0.1, 8),
                    Brightness = ParseDouble(BrightnessTextBox, 1, 0.05, 5),
                    Vibrancy = ParseDouble(VibrancyTextBox, 1, 0, 1),
                    PaletteName = palette.Name
                }
            };
            _renderService.Start(_sessionOptions);
            RefreshCandidates();
            EmptyPreviewTextBlock.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            WpfMessageBox.Show(this, exception.Message, "Could not start rendering", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Pause_Click(object sender, RoutedEventArgs e) => _renderService.Pause();
    private void Resume_Click(object sender, RoutedEventArgs e) => _renderService.Resume();
    private async void Stop_Click(object sender, RoutedEventArgs e) => await _renderService.StopAsync();

    private void StartAi_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            EnsureWorkspace();
            _aiService.Start(OutputDirectoryTextBox.Text.Trim(), _ratingStore!);
            _aiEnabled = true;
            RefreshCandidates();
            ShowNextReady();
        }
        catch (Exception exception) { WpfMessageBox.Show(this, exception.Message, "Could not start AI scoring", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void PauseAi_Click(object sender, RoutedEventArgs e) => _aiService.Pause();
    private void ResumeAi_Click(object sender, RoutedEventArgs e) => _aiService.Resume();
    private async void StopAi_Click(object sender, RoutedEventArgs e) { await _aiService.StopAsync(); _aiEnabled = false; }

    private async void TrainModel_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            EnsureWorkspace();
            var snapshot = PreferenceDatasetBuilder.Snapshot(OutputDirectoryTextBox.Text.Trim());
            UpdateDatasetStatistics(snapshot.Statistics);
            if (TrainingWarningPolicy.ShouldShow(snapshot.Statistics, DoNotShowTrainingWarningCheckBox.IsChecked == true))
            {
                var result = WpfMessageBox.Show(this, $"This corpus has {snapshot.Statistics.Total} rated image(s). Training is allowed, but validation/test metrics may be unreliable. Continue?", "Small dataset warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
            }
            if (!_aiService.Status.IsRunning) _aiService.Start(OutputDirectoryTextBox.Text.Trim(), _ratingStore!);
            _aiEnabled = true;
            TrainingMetricsTextBlock.Text = "Training on the frozen DINOv2 backbone… rendering remains operational.";
            var training = await _aiService.TrainAsync(snapshot, _aiBackend.ModelDirectory);
            UpdateTrainingMetrics(training);
        }
        catch (Exception exception) { WpfMessageBox.Show(this, exception.Message, "Could not train preference model", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void RenderService_ImageReady(RenderedArtifact artifact)
    {
        Dispatcher.Invoke(() =>
        {
            while (_renderService.TryDequeueReady(out _)) { }
            RefreshCandidates();
            if (_current is null) ShowNextReady();
        });
    }

    private void RenderService_RenderFailed(Exception exception) => Dispatcher.Invoke(() => CurrentTextBlock.Text = $"Render failure: {exception.Message}");

    private void AiService_ImageScored(PreferenceScore score)
    {
        Dispatcher.Invoke(() =>
        {
            _catalog.RecordScore(score);
            if (_current is not null && string.Equals(_current.SourceId, score.SourceId, StringComparison.OrdinalIgnoreCase))
            {
                _current = _current with { BaseName = Path.GetFileNameWithoutExtension(score.ImagePath), ImagePath = score.ImagePath };
                ShowArtifact(_current);
            }
            else if (_current is null) ShowNextReady();
        });
    }

    private void AiService_ScoringFailed(Exception exception) => Dispatcher.Invoke(() => AiStatusTextBlock.Text = $"AI scoring error: {exception.Message}");

    private void AiService_TrainingCompleted(TrainingResult result) => Dispatcher.Invoke(() => UpdateTrainingMetrics(result));

    private void ShowArtifact(RenderedArtifact artifact)
    {
        if (!File.Exists(artifact.ImagePath)) return;
        try
        {
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(artifact.ImagePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            _current = artifact;
            PreviewImage.Source = bitmap;
            _imagePixelWidth = bitmap.PixelWidth;
            _imagePixelHeight = bitmap.PixelHeight;
            EmptyPreviewTextBlock.Visibility = Visibility.Collapsed;
            UpdateCurrentText();
            if (_fitToViewport) ApplyFitZoom(); else ApplyZoom(_zoomScale);
        }
        catch (Exception exception) { CurrentTextBlock.Text = $"Could not open candidate: {exception.Message}"; }
    }

    private void ShowNextReady()
    {
        RefreshCandidates();
        var next = _current is null ? _catalog.Best(_aiEnabled) : _catalog.Adjacent(_current.SourceId, 1, _aiEnabled);
        if (_deferredAfterUndo is { } deferred)
        {
            _deferredAfterUndo = null;
            ShowArtifact(deferred);
        }
        else if (next is not null) ShowArtifact(next);
        else
        {
            _current = null;
            PreviewImage.Source = null;
            EmptyPreviewTextBlock.Visibility = Visibility.Visible;
            CurrentTextBlock.Text = string.Empty;
        }
    }

    private void Previous_Click(object sender, RoutedEventArgs e)
    {
        RefreshCandidates();
        if (_current is not null && _catalog.Adjacent(_current.SourceId, -1, _aiEnabled) is { } previous) ShowArtifact(previous);
    }

    private void Next_Click(object sender, RoutedEventArgs e) => ShowNextReady();

    private void Rating_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null || _ratingStore is null) return;
        var rating = int.Parse(((WpfButton)sender).Tag.ToString()!, CultureInfo.InvariantCulture);
        try
        {
            var current = ResolveCurrentArtifact();
            _ratingStore.Rate(current.ImagePath, rating);
            _lastRated = current;
            ShowNextReady();
        }
        catch (Exception exception) { WpfMessageBox.Show(this, exception.Message, "Could not save rating", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void Skip_Click(object sender, RoutedEventArgs e) => ShowNextReady();

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_lastRated is null || _ratingStore?.Undo() != true) return;
        _deferredAfterUndo = _current;
        ShowArtifact(_lastRated);
        _lastRated = null;
    }

    private void ZoomFit_Click(object sender, RoutedEventArgs e) { _fitToViewport = true; ApplyFitZoom(); }
    private void ActualSize_Click(object sender, RoutedEventArgs e) { _fitToViewport = false; ApplyZoom(1); }

    private void PreviewScroll_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_current is null || _imagePixelWidth <= 0 || _imagePixelHeight <= 0) return;
        _fitToViewport = false;
        var pointer = e.GetPosition(PreviewScroll);
        ApplyZoom(_zoomScale * (e.Delta > 0 ? 1.2 : 1 / 1.2), pointer);
        e.Handled = true;
    }

    private void PreviewScroll_SizeChanged(object sender, SizeChangedEventArgs e) { if (_fitToViewport && _current is not null) ApplyFitZoom(); }

    private void ApplyFitZoom()
    {
        if (_imagePixelWidth <= 0 || _imagePixelHeight <= 0) return;
        var scale = Math.Min(Math.Max(1, PreviewScroll.ViewportWidth) / _imagePixelWidth, Math.Max(1, PreviewScroll.ViewportHeight) / _imagePixelHeight);
        ApplyZoom(Math.Clamp(scale, 0.05, 8));
    }

    private void ApplyZoom(double requestedScale, WpfPoint? anchor = null)
    {
        if (_applyingZoom || _imagePixelWidth <= 0 || _imagePixelHeight <= 0) return;
        _applyingZoom = true;
        try
        {
            var oldScale = Math.Max(0.05, _zoomScale);
            var anchorPoint = anchor ?? new WpfPoint(PreviewScroll.ViewportWidth / 2, PreviewScroll.ViewportHeight / 2);
            var imageOrigin = PreviewImage.TranslatePoint(new WpfPoint(0, 0), PreviewScroll);
            var imageX = Math.Clamp((anchorPoint.X - imageOrigin.X) / oldScale, 0, _imagePixelWidth);
            var imageY = Math.Clamp((anchorPoint.Y - imageOrigin.Y) / oldScale, 0, _imagePixelHeight);
            _zoomScale = Math.Clamp(requestedScale, 0.05, 8);
            PreviewImage.Width = _imagePixelWidth * _zoomScale;
            PreviewImage.Height = _imagePixelHeight * _zoomScale;
            PreviewImage.Stretch = Stretch.Fill;
            PreviewScroll.UpdateLayout();
            if (anchor is not null)
            {
                var newOrigin = PreviewImage.TranslatePoint(new WpfPoint(0, 0), PreviewScroll);
                PreviewScroll.ScrollToHorizontalOffset(PreviewScroll.HorizontalOffset + newOrigin.X - (anchorPoint.X - imageX * _zoomScale));
                PreviewScroll.ScrollToVerticalOffset(PreviewScroll.VerticalOffset + newOrigin.Y - (anchorPoint.Y - imageY * _zoomScale));
            }
        }
        finally { _applyingZoom = false; }
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog { SelectedPath = OutputDirectoryTextBox.Text };
        if (dialog.ShowDialog() == Forms.DialogResult.OK) OutputDirectoryTextBox.Text = dialog.SelectedPath;
    }

    private void Window_KeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key >= Key.D1 && e.Key <= Key.D5) { Rating_Click(new WpfButton { Tag = (int)e.Key - (int)Key.D0 }, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.U) { Undo_Click(sender, e); e.Handled = true; }
        else if (e.Key == Key.P) { if (_renderService.Status.IsPaused) Resume_Click(sender, e); else Pause_Click(sender, e); e.Handled = true; }
        else if (e.Key == Key.Escape) { Stop_Click(sender, e); e.Handled = true; }
    }

    private void UpdateStatus(object? sender, EventArgs e)
    {
        var status = _renderService.Status;
        QueueTextBlock.Text = $"Queue: {status.QueueDepth}/{_sessionOptions?.QueueCapacity ?? 0} · ready {status.ReadyCount}\nCompleted: {status.Completed} · failures: {status.Failed}";
        var sampleProgress = status.ActiveSampleBudget > 0 ? $" · samples {status.ActiveSamples:N0}/{status.ActiveSampleBudget:N0}" : string.Empty;
        SessionTextBlock.Text = status.IsRunning ? $"Session: {(status.IsPaused ? "PAUSED" : "RUNNING")} · {status.Elapsed:hh\\:mm\\:ss} · limit {status.BatchLimit}{sampleProgress}" : "Session: idle";
        var ai = _aiService.Status;
        AiStatusTextBlock.Text = $"AI: {(ai.IsRunning ? (ai.IsPaused ? "PAUSED" : "RUNNING") : "idle")} · pending {ai.PendingImages} · scored {ai.ScoredImages} · progress {ai.Completed}/{ai.Total}\nModel: {ai.ModelVersion ?? "not trained"} · device {ai.Diagnostics.ActiveDevice}";
        RatingCountTextBlock.Text = _ratingStore is null ? "Rated: 0" : $"Rated: {_ratingStore.RatedImageCount()} · PNG/.flame pairs: {_ratingStore.RatingFoldersContainPairedFiles()}";
        if (DateTime.UtcNow >= _nextUiRefresh)
        {
            _nextUiRefresh = DateTime.UtcNow.AddMilliseconds(500);
            RefreshCandidates();
            if (_ratingStore is not null) UpdateDatasetStatistics(PreferenceDatasetBuilder.Snapshot(_ratingStore.RootDirectory).Statistics);
            UpdateCurrentText();
        }
    }

    private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_closing) return;
        e.Cancel = true;
        _closing = true;
        CompositionTarget.Rendering -= UpdateStatus;
        await _renderService.StopAsync();
        await _aiService.DisposeAsync();
        Close();
    }

    private void EnsureWorkspace()
    {
        var output = Path.GetFullPath(OutputDirectoryTextBox.Text.Trim());
        _archive = new SourceArchive(output);
        _ratingStore = new RatingStore(output);
        RefreshCandidates();
    }

    private void RefreshCandidates()
    {
        if (_archive is null || _ratingStore is null) return;
        _catalog.Refresh(_archive, _ratingStore);
    }

    private RenderedArtifact ResolveCurrentArtifact()
    {
        if (_current is not null && File.Exists(_current.ImagePath)) return _current;
        RefreshCandidates();
        return _catalog.Ordered(_aiEnabled).FirstOrDefault(artifact => string.Equals(artifact.SourceId, _current?.SourceId, StringComparison.OrdinalIgnoreCase)) ?? throw new FileNotFoundException("The current candidate is no longer available.");
    }

    private void UpdateCurrentText()
    {
        if (_current is null) return;
        var score = _aiService.TryGetScore(_current.SourceId, out var scored) ? scored : _catalog.GetScore(_current);
        var rating = _ratingStore?.FindRating(_current.ImagePath);
        var scoreText = score is null ? "AI score: pending" : $"AI score: {score.Score:0.00000} · expected rating {score.ExpectedRating:0.00}";
        var ratingText = rating is null ? string.Empty : $" · human rating: {rating}★";
        CurrentTextBlock.Text = $"{_current.BaseName}\nSource ID: {_current.SourceId}\n{scoreText} · model: {score?.ModelVersion ?? _aiService.Status.ModelVersion ?? "none"}{ratingText}";
    }

    private void UpdateAiDiagnostics(DeviceDiagnostics diagnostics)
    {
        AiDiagnosticsTextBlock.Text = $"PyTorch: {diagnostics.PyTorchVersion} · CUDA: {(diagnostics.CudaAvailable ? "available" : "unavailable")}\nGPU: {diagnostics.GpuName} · active device: {diagnostics.ActiveDevice}\n{diagnostics.Detail}";
        AiStatusTextBlock.Text = diagnostics.AiReady ? "AI is ready; train a model to begin preference scoring." : "AI scoring/training disabled; manual rendering and rating remain available.";
    }

    private void UpdateDatasetStatistics(DatasetStatistics statistics)
    {
        var maximum = Math.Max(1, statistics.Counts.Values.Max());
        RatingCount1.Text = statistics.CountFor(1).ToString(CultureInfo.InvariantCulture);
        RatingCount2.Text = statistics.CountFor(2).ToString(CultureInfo.InvariantCulture);
        RatingCount3.Text = statistics.CountFor(3).ToString(CultureInfo.InvariantCulture);
        RatingCount4.Text = statistics.CountFor(4).ToString(CultureInfo.InvariantCulture);
        RatingCount5.Text = statistics.CountFor(5).ToString(CultureInfo.InvariantCulture);
        RatingBar1.Width = 220d * statistics.CountFor(1) / maximum;
        RatingBar2.Width = 220d * statistics.CountFor(2) / maximum;
        RatingBar3.Width = 220d * statistics.CountFor(3) / maximum;
        RatingBar4.Width = 220d * statistics.CountFor(4) / maximum;
        RatingBar5.Width = 220d * statistics.CountFor(5) / maximum;
        var readinessBrush = statistics.Readiness switch
        {
            DatasetReadinessColor.Red => WpfBrushes.IndianRed,
            DatasetReadinessColor.Amber => WpfBrushes.Goldenrod,
            _ => WpfBrushes.MediumSeaGreen
        };
        RatingBar1.Background = readinessBrush;
        RatingBar2.Background = readinessBrush;
        RatingBar3.Background = readinessBrush;
        RatingBar4.Background = readinessBrush;
        RatingBar5.Background = readinessBrush;
        DatasetReadinessTextBlock.Foreground = readinessBrush;
        DatasetReadinessTextBlock.Text = $"Total: {statistics.Total} · readiness: {statistics.Readiness}\n{statistics.ReadinessMessage}\nReadiness colors are heuristics, not scientific guarantees.";
        TrainingWarningTextBlock.Text = statistics.Readiness == DatasetReadinessColor.Green ? string.Empty : "Small/imbalanced corpus warning: training is available, but held-out metrics may be unreliable.";
    }

    private void UpdateTrainingMetrics(TrainingResult result)
    {
        var metrics = result.Metrics;
        var controls = metrics.Controls.Count == 0 ? "none" : string.Join(", ", metrics.Controls.Select(control => $"{control.Name} {control.Score:0.000}"));
        TrainingMetricsTextBlock.Text = $"Model {result.ModelVersion}\nOrdinal accuracy {metrics.OrdinalAccuracy:0.000} · MAE {metrics.MeanAbsoluteRatingError:0.000}\nSpearman/rank {metrics.SpearmanCorrelation:0.000} · calibration error {metrics.CalibrationError:0.000}\nEvaluation: {(metrics.IsReliable ? "usable split" : "unreliable: corpus too small for meaningful validation/test")}\nControls (evaluation only): {controls}";
    }

    private static int ParseInt(WpfTextBox box, int fallback, int minimum, int maximum) => ParseInt(box.Text, fallback, minimum, maximum);
    private static int ParseInt(string? value, int fallback, int minimum, int maximum) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? Math.Clamp(parsed, minimum, maximum) : fallback;
    private static long ParseLong(WpfTextBox box, long fallback) => long.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    private static double ParseDouble(WpfTextBox box, double fallback, double minimum, double maximum) => double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? Math.Clamp(parsed, minimum, maximum) : fallback;
}
