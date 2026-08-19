using System.Xml.Linq;
using FractalFlameCurator.Generation;
using FractalFlameCurator.Models;
using FractalFlameCurator.Pipeline;
using FractalFlameCurator.Rendering;
using FractalFlameCurator.Serialization;
using FractalFlameCurator.Storage;
using Xunit;

namespace FractalFlameCurator.Tests;

public sealed class PhaseOneTests
{
    [Fact]
    public void GeneratedFlameIsValidApophysisXml()
    {
        var genome = new FlameGenerator().Generate(12345);
        var xml = FlameXmlSerializer.Serialize(genome);
        var document = XDocument.Parse(xml);
        Assert.Equal("flames", document.Root?.Name.LocalName);
        Assert.NotNull(document.Root?.Element("flame"));
        Assert.Equal(256, document.Root!.Element("flame")!.Element("palette")!.Attribute("count")!.Value is "256" ? 256 : 0);
        var roundTrip = FlameXmlSerializer.Deserialize(xml);
        Assert.Equal(genome.Seed, roundTrip.Seed);
        Assert.Equal(genome.Transforms.Count, roundTrip.Transforms.Count);
        Assert.Equal(genome.Transforms.SelectMany(t => t.Variations.Keys).OrderBy(x => x), roundTrip.Transforms.SelectMany(t => t.Variations.Keys).OrderBy(x => x));
    }

    [Fact]
    public async Task FixedSeedProducesTheSameGenomeAndPixels()
    {
        var first = new FlameGenerator().Generate(777);
        var second = new FlameGenerator().Generate(777);
        Assert.Equal(FlameXmlSerializer.Serialize(first), FlameXmlSerializer.Serialize(second));
        var renderer = new CpuFlameRenderer();
        var settings = new RenderSettings { Width = 64, Height = 64, SampleBudget = 7_000, Oversample = 1, FilterRadius = 0.5, PaletteName = "Monochrome" };
        var firstFrame = await renderer.RenderAsync(first, settings, null, CancellationToken.None);
        var secondFrame = await renderer.RenderAsync(second, settings, null, CancellationToken.None);
        Assert.Equal(firstFrame.BgraPixels, secondFrame.BgraPixels);
    }

    [Fact]
    public async Task DefaultRenderIsSquare2048AndMonochrome()
    {
        var genome = new FlameGenerator().Generate(314159);
        var settings = new RenderSettings { SampleBudget = 1_000 };
        var frame = await new CpuFlameRenderer().RenderAsync(genome, settings, null, CancellationToken.None);
        Assert.Equal(2048, frame.Width);
        Assert.Equal(2048, frame.Height);
        Assert.Equal(2048 * 2048 * 4, frame.BgraPixels.Length);
        Assert.Equal(PaletteDefinition.Monochrome.Name, genome.Palette.Name);
        Assert.Equal(255, frame.BgraPixels[0]);
        Assert.Equal(255, frame.BgraPixels[1]);
        Assert.Equal(255, frame.BgraPixels[2]);
    }

    [Fact]
    public void GeneratorAuditsBroadVariationCoverage()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var generator = new FlameGenerator();
        for (var seed = 1; seed <= 300; seed++)
        {
            seen.UnionWith(generator.Generate(seed).Transforms.SelectMany(transform => transform.Variations.Keys));
        }
        Assert.True(seen.Count >= 30, $"Only {seen.Count} variations were sampled.");
        Assert.All(seen, name => Assert.Contains(name, VariationRegistry.Names));
    }

    [Fact]
    public async Task RendererCancelsWithoutReturningAFrame()
    {
        var genome = new FlameGenerator().Generate(22);
        var renderer = new CpuFlameRenderer();
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(5);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => renderer.RenderAsync(genome, new RenderSettings { Width = 128, Height = 128, SampleBudget = 20_000_000 }, null, cancellation.Token));
    }

    [Fact]
    public void BackendReportingDoesNotClaimGpu()
    {
        var status = new CpuFlameRenderer().Status;
        Assert.Equal("CPU", status.Backend);
        Assert.False(status.IsGpu);
        Assert.Contains("CPU", status.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BoundedQueueNeverExceedsCapacity()
    {
        var queue = new BoundedRenderQueue<int>(1);
        await queue.EnqueueAsync(1, CancellationToken.None);
        var pending = queue.EnqueueAsync(2, CancellationToken.None).AsTask();
        await Task.Delay(30);
        Assert.Equal(1, queue.Count);
        Assert.False(pending.IsCompleted);
        Assert.Equal(1, await queue.DequeueAsync(CancellationToken.None));
        await pending;
        Assert.Equal(1, queue.Count);
        Assert.Equal(2, await queue.DequeueAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RendererFailureIsCountedAndSessionKeepsItsBoundary()
    {
        var failed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var service = new ContinuousRenderService(new FlameGenerator(), new ThrowingRenderer());
        service.RenderFailed += _ => failed.TrySetResult(true);
        service.Start(new ContinuousRenderOptions { OutputDirectory = NewTempDirectory(), BatchLimit = 1, QueueCapacity = 1, WorkerCount = 1 });
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, service.Status.Failed);
        await service.StopAsync();
    }

    [Fact]
    public async Task FiniteSessionStopsReportingRunningAfterWorkersDrain()
    {
        var root = NewTempDirectory();
        try
        {
            await using var service = new ContinuousRenderService(new FlameGenerator(), new FastRenderer());
            service.Start(new ContinuousRenderOptions { OutputDirectory = root, BatchLimit = 2, QueueCapacity = 1, WorkerCount = 1 });
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (service.Status.Completed < 2 && DateTime.UtcNow < deadline) await Task.Delay(20);
            Assert.Equal(2, service.Status.Completed);
            Assert.False(service.Status.IsRunning);
            Assert.Equal(2, service.Status.ReadyCount);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void RatingCopiesOnlyImagesAndUndoPreservesSourceArchive()
    {
        var root = NewTempDirectory();
        try
        {
            var genome = new FlameGenerator().Generate(98);
            var archive = new SourceArchive(root);
            var frame = new RenderedFrame(32, 32, new byte[32 * 32 * 4]);
            var artifact = archive.Save(genome, frame, 1);
            var ratings = new RatingStore(root);
            ratings.Rate(artifact.ImagePath, 2);
            Assert.True(File.Exists(artifact.ImagePath));
            Assert.True(File.Exists(artifact.FlamePath));
            Assert.Equal(2, ratings.FindRating(artifact.ImagePath));
            Assert.True(ratings.RatingFoldersContainImagesOnly());
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, "ratings", "2"), "*.flame"));
            ratings.Rate(artifact.ImagePath, 5);
            Assert.Equal(5, ratings.FindRating(artifact.ImagePath));
            Assert.True(ratings.Undo());
            Assert.Equal(2, ratings.FindRating(artifact.ImagePath));
            Assert.True(File.Exists(artifact.FlamePath));
        }
        finally { Directory.Delete(root, true); }
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ffc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class ThrowingRenderer : IFlameRenderer
    {
        public RendererStatus Status { get; } = new("CPU", "Test CPU", false, "Test renderer");
        public Task<RenderedFrame> RenderAsync(FlameGenome genome, RenderSettings settings, IProgress<RenderProgress>? progress, CancellationToken cancellationToken) => throw new InvalidOperationException("synthetic renderer failure");
    }

    private sealed class FastRenderer : IFlameRenderer
    {
        public RendererStatus Status { get; } = new("CPU", "Test CPU", false, "Test renderer");
        public Task<RenderedFrame> RenderAsync(FlameGenome genome, RenderSettings settings, IProgress<RenderProgress>? progress, CancellationToken cancellationToken) => Task.FromResult(new RenderedFrame(32, 32, new byte[32 * 32 * 4]));
    }
}
