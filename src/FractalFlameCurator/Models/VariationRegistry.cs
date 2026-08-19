namespace FractalFlameCurator.Models;

public sealed record VariationDefinition(string Name, string Category);

public static class VariationRegistry
{
    private static readonly VariationDefinition[] Definitions =
    [
        new("linear", "core"), new("sinusoidal", "core"), new("spherical", "core"),
        new("swirl", "core"), new("horseshoe", "core"), new("polar", "core"),
        new("handkerchief", "core"), new("heart", "core"), new("disc", "core"),
        new("spiral", "core"), new("hyperbolic", "core"), new("diamond", "core"),
        new("ex", "core"), new("julia", "core"), new("bent", "core"),
        new("waves", "core"), new("fisheye", "core"), new("popcorn", "core"),
        new("exponential", "core"), new("power", "core"), new("cosine", "core"),
        new("rings", "core"), new("fan", "core"), new("blob", "core"),
        new("pdj", "advanced"), new("perspective", "advanced"), new("noise", "advanced"),
        new("julian", "advanced"), new("juliascope", "advanced"), new("curl", "advanced"),
        new("rectangles", "advanced"), new("tangent", "advanced"), new("cross", "advanced"),
        new("rays", "advanced"), new("secant", "advanced"), new("twintrian", "advanced"),
        new("blur", "advanced"), new("radial_blur", "advanced")
    ];

    public static IReadOnlyList<VariationDefinition> All { get; } = Definitions;
    public static IReadOnlySet<string> Names { get; } = Definitions.Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
}

