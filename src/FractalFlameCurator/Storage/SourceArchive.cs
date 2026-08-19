using FractalFlameCurator.Models;
using FractalFlameCurator.Rendering;
using FractalFlameCurator.Serialization;

namespace FractalFlameCurator.Storage;

public sealed record RenderedArtifact(string BaseName, string ImagePath, string FlamePath, long Seed, int Sequence)
{
    public string SourceId => CandidateFileNaming.GetSourceId(BaseName);
}

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

    public IReadOnlyList<RenderedArtifact> EnumerateArtifacts()
    {
        if (!Directory.Exists(RenderedDirectory)) return [];
        return Directory.EnumerateFiles(RenderedDirectory, "*.png", SearchOption.TopDirectoryOnly)
            .Where(IsCompleteCandidate)
            .Select(path =>
            {
                var baseName = Path.GetFileNameWithoutExtension(path);
                var sourceId = CandidateFileNaming.GetSourceId(baseName);
                var flamePath = Path.Combine(RenderedDirectory, sourceId + ".flame");
                return new RenderedArtifact(baseName, path, flamePath, 0, ParseSequence(sourceId));
            })
            .OrderBy(artifact => artifact.SourceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsCompleteCandidate(string imagePath)
    {
        if (!File.Exists(imagePath)) return false;
        var sourceId = CandidateFileNaming.GetSourceId(Path.GetFileNameWithoutExtension(imagePath));
        var flamePath = Path.Combine(Path.GetDirectoryName(imagePath) ?? ".", sourceId + ".flame");
        if (!File.Exists(flamePath)) return false;
        try
        {
            using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return stream.Length > 0;
        }
        catch (IOException) { return false; }
    }

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
            File.Move(flameTemp, flamePath, true);
            // Publish the image last. A watcher can therefore only observe a candidate
            // after both the PNG and its stable source genome are complete.
            File.Move(imageTemp, imagePath, true);
            return new RenderedArtifact(baseName, imagePath, flamePath, genome.Seed, sequence);
        }
        finally
        {
            if (File.Exists(imageTemp)) File.Delete(imageTemp);
            if (File.Exists(flameTemp)) File.Delete(flameTemp);
        }
    }

    private static int ParseSequence(string sourceId)
    {
        var marker = "flame_";
        var start = sourceId.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return 0;
        var valueStart = start + marker.Length;
        var value = sourceId[valueStart..].TakeWhile(char.IsDigit).ToArray();
        return int.TryParse(new string(value), out var sequence) ? sequence : 0;
    }
}
