using FractalFlameCurator.Models;

namespace FractalFlameCurator.Generation;

public sealed record FlameGeneratorOptions
{
    public int Width { get; init; } = 2048;
    public int Height { get; init; } = 2048;
    public PaletteDefinition Palette { get; init; } = PaletteDefinition.Monochrome;
}

public sealed class FlameGenerator
{
    public FlameGenome Generate(long seed, FlameGeneratorOptions? options = null)
    {
        options ??= new FlameGeneratorOptions();
        var random = new DeterministicRandom(seed);
        var genome = new FlameGenome
        {
            Name = $"flame_seed_{seed}",
            Seed = seed,
            Width = options.Width,
            Height = options.Height,
            CenterX = random.NextSigned(0.35),
            CenterY = random.NextSigned(0.35),
            Scale = 80 + random.NextDouble() * 90,
            Rotate = random.NextSigned(30),
            Oversample = 1,
            FilterRadius = 0.35 + random.NextDouble() * 0.85,
            Quality = 5_000_000,
            Brightness = 0.85 + random.NextDouble() * 0.5,
            Gamma = 1.7 + random.NextDouble() * 1.0,
            Vibrancy = 0.8 + random.NextDouble() * 0.2,
            Symmetry = random.NextInt(-1, 4),
            Palette = options.Palette
        };

        var transformCount = random.NextInt(2, 6);
        var definitions = VariationRegistry.All.ToArray();
        for (var i = 0; i < transformCount; i++)
        {
            var angle = random.NextSigned(Math.PI);
            var scale = 0.35 + random.NextDouble() * 0.5;
            var shear = random.NextSigned(0.25);
            var transform = new FlameTransform
            {
                Weight = 0.35 + random.NextDouble() * 0.85,
                Color = i / (double)Math.Max(1, transformCount - 1),
                Symmetry = 0.7 + random.NextDouble() * 0.6,
                A = Math.Cos(angle) * scale,
                B = -Math.Sin(angle) * scale + shear,
                C = Math.Sin(angle) * scale,
                D = Math.Cos(angle) * scale,
                E = random.NextSigned(0.75),
                F = random.NextSigned(0.75)
            };

            var variationCount = random.NextInt(1, 4);
            for (var v = 0; v < variationCount; v++)
            {
                var definition = definitions[random.NextInt(0, definitions.Length)];
                if (transform.Variations.ContainsKey(definition.Name))
                {
                    v--;
                    continue;
                }
                transform.Variations[definition.Name] = 0.2 + random.NextDouble() * 0.95;
            }

            if (random.NextBool(0.42))
            {
                var postAngle = random.NextSigned(Math.PI);
                var postScale = 0.75 + random.NextDouble() * 0.5;
                transform.PostTransform = new AffineTransform(
                    Math.Cos(postAngle) * postScale,
                    -Math.Sin(postAngle) * postScale,
                    Math.Sin(postAngle) * postScale,
                    Math.Cos(postAngle) * postScale,
                    random.NextSigned(0.18),
                    random.NextSigned(0.18));
            }
            genome.Transforms.Add(transform);
        }

        FlameValidator.ThrowIfInvalid(genome);
        return genome;
    }
}
