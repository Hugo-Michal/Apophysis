using FractalFlameCurator.Models;
using FractalFlameCurator.Rendering;
using FractalFlameCurator.Serialization;

namespace FractalFlameCurator.Storage;

public sealed record RenderedArtifact(string BaseName, string ImagePath, string FlamePath, long Seed, int Sequence);

public sealed class SourceArchive
{
    public SourceArchive(string rootDirectory)
    {
        RootDirectory = Path.GetFullPath(rootDirectory);
        RenderedDirectory = Path.Combine(RootDirectory, "rendered");
        Directory.CreateDirectory(RenderedDirectory);
    }

    public string RootDirectory { get; }
    public string RenderedDirectory { get; }

    public RenderedArtifact Save(FlameGenome genome, RenderedFrame frame, int sequence)
    {
        var baseName = $"flame_{sequence:000000}_seed_{genome.Seed}";
        var imagePath = Path.Combine(RenderedDirectory, baseName + ".png");
        var flamePath = Path.Combine(RenderedDirectory, baseName + ".flame");
        var imageTemp = imagePath + ".tmp";
        var flameTemp = flamePath + ".tmp";
        try
        {
            frame.SavePng(imageTemp);
            FlameXmlSerializer.Save(genome, flameTemp);
            File.Move(imageTemp, imagePath, true);
            File.Move(flameTemp, flamePath, true);
            return new RenderedArtifact(baseName, imagePath, flamePath, genome.Seed, sequence);
        }
        finally
        {
            if (File.Exists(imageTemp)) File.Delete(imageTemp);
            if (File.Exists(flameTemp)) File.Delete(flameTemp);
        }
    }
}

