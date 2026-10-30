namespace Soundmote.Core.Dsp;

/// <summary>
/// SplitMix64. Small, fast and fully determined by its seed, so a scene with the same seed renders the same
/// samples every time (A6). A mutable struct: keep it in a field and pass it by <c>ref</c>.
/// </summary>
public struct Rng(ulong seed)
{
    private ulong _state = seed;

    public ulong NextULong()
    {
        var z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Uniform in [0, 1).</summary>
    public float NextFloat() => (NextULong() >> 40) * (1f / (1 << 24));

    /// <summary>Uniform in [-1, 1).</summary>
    public float NextBipolar() => NextFloat() * 2f - 1f;

    public float Range(float min, float max) => min + (max - min) * NextFloat();

    /// <summary>A new generator seeded from this one, for a part that needs its own stream.</summary>
    public Rng Fork() => new(NextULong());
}
