namespace FractalFlameCurator.Storage;

public sealed record RatingAction(string ImagePath, int NewRating, int? PreviousRating);

public sealed class RatingStore
{
    private readonly Stack<RatingAction> _history = new();

    public RatingStore(string rootDirectory)
    {
        RootDirectory = Path.GetFullPath(rootDirectory);
        RatingsDirectory = Path.Combine(RootDirectory, "ratings");
        for (var rating = 1; rating <= 5; rating++) Directory.CreateDirectory(GetRatingDirectory(rating));
    }

    public string RootDirectory { get; }
    public string RatingsDirectory { get; }

    public RatingAction Rate(string sourceImagePath, int rating)
    {
        if (rating is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(rating));
        if (!File.Exists(sourceImagePath)) throw new FileNotFoundException("The source image is not available.", sourceImagePath);
        if (!string.Equals(Path.GetExtension(sourceImagePath), ".png", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Only rendered PNG images can be rated.");
        var previous = FindRating(sourceImagePath);
        var action = new RatingAction(sourceImagePath, rating, previous);
        RemoveAllRatingCopies(sourceImagePath);
        File.Copy(sourceImagePath, Path.Combine(GetRatingDirectory(rating), Path.GetFileName(sourceImagePath)), true);
        _history.Push(action);
        return action;
    }

    public bool Undo()
    {
        if (_history.Count == 0) return false;
        var action = _history.Pop();
        RemoveAllRatingCopies(action.ImagePath);
        if (action.PreviousRating is { } previous)
        {
            File.Copy(action.ImagePath, Path.Combine(GetRatingDirectory(previous), Path.GetFileName(action.ImagePath)), true);
        }
        return true;
    }

    public int? FindRating(string sourceImagePath)
    {
        var fileName = Path.GetFileName(sourceImagePath);
        for (var rating = 1; rating <= 5; rating++)
        {
            if (File.Exists(Path.Combine(GetRatingDirectory(rating), fileName))) return rating;
        }
        return null;
    }

    public int RatedImageCount() => Enumerable.Range(1, 5).SelectMany(rating => Directory.EnumerateFiles(GetRatingDirectory(rating), "*.png", SearchOption.TopDirectoryOnly)).Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    public bool RatingFoldersContainImagesOnly() => Enumerable.Range(1, 5).SelectMany(rating => Directory.EnumerateFiles(GetRatingDirectory(rating), "*", SearchOption.TopDirectoryOnly)).All(path => string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase));

    private string GetRatingDirectory(int rating) => Path.Combine(RatingsDirectory, rating.ToString());

    private void RemoveAllRatingCopies(string sourceImagePath)
    {
        var fileName = Path.GetFileName(sourceImagePath);
        for (var rating = 1; rating <= 5; rating++)
        {
            var path = Path.Combine(GetRatingDirectory(rating), fileName);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}

