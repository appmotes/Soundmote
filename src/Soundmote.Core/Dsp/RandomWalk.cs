namespace Soundmote.Core.Dsp;

/// <summary>
/// Slow, smooth wandering in [-1, 1]. Glides along a cosine between random points spaced 0.5 to 1.5 times the
/// mean period apart. This is what keeps a layer from ever sounding the same twice (A6).
/// </summary>
public struct RandomWalk
{
    private readonly float _meanSamples;
    private float _from, _to, _phase, _step;

    public RandomWalk(float meanPeriodSeconds, int sampleRate, ref Rng rng)
    {
        _meanSamples = meanPeriodSeconds * sampleRate;
        _from = rng.NextBipolar();
        _to = rng.NextBipolar();
        _phase = rng.NextFloat();
        _step = NewStep(ref rng);
    }

    public readonly float Value => _from + (_to - _from) * (0.5f - 0.5f * MathF.Cos(MathF.PI * _phase));

    /// <summary>The value in [0, 1] instead of [-1, 1].</summary>
    public readonly float Unit => 0.5f + 0.5f * Value;

    public float Advance(int samples, ref Rng rng)
    {
        _phase += _step * samples;
        while (_phase >= 1f)
        {
            _phase -= 1f;
            _from = _to;
            _to = rng.NextBipolar();
            _step = NewStep(ref rng);
        }
        return Value;
    }

    private readonly float NewStep(ref Rng rng) => 1f / (_meanSamples * rng.Range(0.5f, 1.5f));
}
