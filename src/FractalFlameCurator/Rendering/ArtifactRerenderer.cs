using FractalFlameCurator.Models;
using FractalFlameCurator.Serialization;
using FractalFlameCurator.Storage;

namespace FractalFlameCurator.Rendering;

public sealed class ArtifactRerenderer
{
    private readonly IFlameRenderer _renderer;

    public ArtifactRerenderer(IFlameRenderer renderer)
    {
        _renderer = renderer;
    }

    public async Task RerenderAsync(
        RenderedArtifact artifact,
        PaletteDefinition palette,
        RenderSettings settings,
        IProgress<RenderProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!SourceArchive.IsCompleteCandidate(artifact.ImagePath))
        {
            throw new InvalidDataException("The current image and matching source .flame file are not both available.");
        }

        var genome = FlameXmlSerializer.Load(artifact.FlamePath);
        genome.Palette = palette;
        genome.Width = settings.Width;
        genome.Height = settings.Height;
        genome.Quality = settings.SampleBudget;
        genome.Oversample = settings.Oversample;
        genome.FilterRadius = settings.FilterRadius;
        genome.Gamma = settings.Gamma;
        genome.Brightness = settings.Brightness;
        genome.Vibrancy = settings.Vibrancy;

        var temporaryImagePath = artifact.ImagePath + ".rerender-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var frame = await _renderer.RenderAsync(genome, settings, progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            frame.SavePng(temporaryImagePath);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryImagePath, artifact.ImagePath, true);
        }
        finally
        {
            if (File.Exists(temporaryImagePath)) File.Delete(temporaryImagePath);
        }
    }
}
