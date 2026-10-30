namespace Soundmote.Core.Dsp;

/// <summary>Pink noise (-3 dB per octave) from Paul Kellet's refined filter. RMS is about 0.2.</summary>
public struct PinkNoise
{
    private float _b0, _b1, _b2, _b3, _b4, _b5, _b6;

    public float Next(ref Rng rng)
    {
        var white = rng.NextBipolar();
        _b0 = 0.99886f * _b0 + white * 0.0555179f;
        _b1 = 0.99332f * _b1 + white * 0.0750759f;
        _b2 = 0.96900f * _b2 + white * 0.1538520f;
        _b3 = 0.86650f * _b3 + white * 0.3104856f;
        _b4 = 0.55000f * _b4 + white * 0.5329522f;
        _b5 = -0.7616f * _b5 - white * 0.0168980f;
        var pink = _b0 + _b1 + _b2 + _b3 + _b4 + _b5 + _b6 + white * 0.5362f;
        _b6 = white * 0.115926f;
        return pink * 0.11f;
    }
}

/// <summary>Brown noise (-6 dB per octave): leaky integrated white noise. RMS is about 0.35.</summary>
public struct BrownNoise
{
    private float _y;

    public float Next(ref Rng rng) => _y = _y * 0.998f + rng.NextBipolar() * 0.04f;
}
