namespace FractalFlameCurator.Generation;

public sealed class DeterministicRandom
{
    private ulong _state;

    public DeterministicRandom(long seed)
    {
        _state = unchecked((ulong)seed) + 0x9E3779B97F4A7C15UL;
        _ = NextUInt64();
    }

    public ulong NextUInt64()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        return _state * 2685821657736338717UL;
    }

    public double NextDouble() => (NextUInt64() >> 11) * (1d / (1UL << 53));
    public double NextSigned(double magnitude) => (NextDouble() * 2 - 1) * magnitude;
    public int NextInt(int minInclusive, int maxExclusive) => minInclusive + (int)(NextDouble() * (maxExclusive - minInclusive));
    public bool NextBool(double probability = 0.5) => NextDouble() < probability;
}

