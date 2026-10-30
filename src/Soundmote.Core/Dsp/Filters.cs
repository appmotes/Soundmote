namespace Soundmote.Core.Dsp;

/// <summary>
/// State-variable filter (trapezoidal, after Andrew Simper). Stable under fast cutoff changes, so it can be
/// swept. <see cref="Set"/> costs a <c>tan</c>: call it per sub-block, not per sample.
/// </summary>
public struct Svf
{
    private float _k, _a1, _a2, _a3, _ic1, _ic2;

    public void Set(float cutoff, float q, int sampleRate)
    {
        cutoff = Math.Clamp(cutoff, 10f, sampleRate * 0.45f);
        var g = MathF.Tan(MathF.PI * cutoff / sampleRate);
        _k = 1f / q;
        _a1 = 1f / (1f + g * (g + _k));
        _a2 = g * _a1;
        _a3 = g * _a2;
    }

    public float Low(float x)
    {
        Tick(x, out _, out var low);
        return low;
    }

    public float High(float x)
    {
        Tick(x, out var band, out var low);
        return x - _k * band - low;
    }

    /// <summary>Band-pass with a peak gain of 1 at the cutoff, whatever the Q.</summary>
    public float Band(float x)
    {
        Tick(x, out var band, out _);
        return _k * band;
    }

    private void Tick(float x, out float band, out float low)
    {
        var v3 = x - _ic2;
        band = _a1 * _ic1 + _a2 * v3;
        low = _ic2 + _a2 * _ic1 + _a3 * v3;
        _ic1 = 2f * band - _ic1;
        _ic2 = 2f * low - _ic2;
    }
}

/// <summary>One-pole low-pass, and the high-pass that is its complement.</summary>
public struct OnePole
{
    private float _a, _y;

    public void SetCutoff(float hz, int sampleRate) => _a = 1f - MathF.Exp(-2f * MathF.PI * hz / sampleRate);

    public float Low(float x) => _y += _a * (x - _y);

    public float High(float x) => x - Low(x);
}

/// <summary>Glides towards a target with a fixed time constant. Construct it; the default value never moves.</summary>
public struct Smoother(float seconds, int sampleRate, float initial = 0f)
{
    private readonly float _coeff = 1f - MathF.Exp(-1f / (seconds * sampleRate));
    private float _value = initial;

    public readonly float Value => _value;

    public float Next(float target) => _value += (target - _value) * _coeff;
}
