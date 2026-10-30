using Soundmote.Core.Dsp;

namespace Soundmote.Core.Scenes;

/// <summary>How a ring's place changes while it plays (A13).</summary>
public enum Move
{
    /// <summary>Stays at <see cref="Place.Pan"/>.</summary>
    Still,

    /// <summary>Drifts slowly within <see cref="Place.Range"/> of the pan, one swing about every <see cref="Place.Seconds"/>.</summary>
    Wander,

    /// <summary>Follows <see cref="Place.Path"/> over the turn, so it crosses once, or there and back.</summary>
    Cross,

    /// <summary>
    /// Lands somewhere new within <see cref="Place.Range"/> of the pan and glides there in <see cref="Place.Seconds"/>:
    /// at each strike for a ring of strikes, otherwise every 6 to 24 seconds.
    /// </summary>
    Flit
}

/// <summary>
/// Where a ring is heard (A13): <see cref="Pan"/> from -1 (left) to 1 (right), <see cref="Width"/> from 0 (a point)
/// to 1 (as wide as the sound is). Immutable; the renderer swaps a ring's place in one step.
/// </summary>
public sealed record Place(float Pan = 0f, float Width = 1f)
{
    /// <summary>A path that stays in the middle; only <see cref="Move.Cross"/> reads it.</summary>
    public static Curve Middle { get; } = Curve.Steady(0.5f);

    /// <summary>Where every ring starts: in the middle, as wide as its sound.</summary>
    public static Place Centre { get; } = new();

    public Move Move { get; init; } = Move.Still;

    /// <summary>How far a wandering or flitting ring may go from its pan, 0 to 2.</summary>
    public float Range { get; init; } = 0.5f;

    /// <summary>A wander's swing, or how long a flit takes to land.</summary>
    public double Seconds { get; init; } = 20;

    /// <summary>For <see cref="Move.Cross"/>: where over the turn, 0 left to 1 right.</summary>
    public Curve Path { get; init; } = Middle;

    /// <summary>The pan at a place on the dial when it does not move at random: still or crossing.</summary>
    public float At(double position) => Move == Move.Cross ? Path.At(position) * 2f - 1f : Pan;
}

/// <summary>
/// Moves one ring's place on the audio thread (A13). Has its own generator, so a still ring takes nothing from the
/// scene's and sounds as it did before places existed (A6).
/// </summary>
internal struct PlaceMover
{
    private const float WanderPeriod = 20f;

    private readonly int _rate;
    private Rng _rng;
    private RandomWalk _wander;
    private float _from, _to, _glide, _pan;
    private double _wait;
    private bool _started;

    public PlaceMover(int sampleRate, ulong seed)
    {
        _rate = sampleRate;
        _rng = new Rng(seed);
        _wander = new RandomWalk(WanderPeriod, sampleRate, ref _rng);
        _glide = 1f;
    }

    /// <summary>The pan for this block. <paramref name="struck"/> is true when a strike fired in it.</summary>
    public float Advance(Place place, int frames, double position, bool strikes, bool struck)
    {
        if (!_started)
        {
            _started = true;
            _pan = _from = _to = place.Pan;
            _wait = _rate * _rng.Range(6f, 24f);
        }
        switch (place.Move)
        {
            case Move.Wander:
                var scaled = (int)(frames * WanderPeriod / Math.Max(1, place.Seconds));
                _pan = place.Pan + place.Range * _wander.Advance(scaled, ref _rng);
                break;
            case Move.Cross:
                _pan = place.At(position);
                break;
            case Move.Flit:
                _wait -= frames;
                if (struck || (!strikes && _wait <= 0))
                {
                    _from = _pan;
                    _to = Math.Clamp(place.Pan + place.Range * _rng.NextBipolar(), -1f, 1f);
                    _glide = 0f;
                    _wait = _rate * _rng.Range(6f, 24f);
                }
                if (_glide < 1f) _glide = MathF.Min(1f, _glide + (float)(frames / (Math.Max(0.05, place.Seconds) * _rate)));
                _pan = _from + (_to - _from) * (0.5f - 0.5f * MathF.Cos(MathF.PI * _glide));
                break;
            default:
                _pan = place.Pan;
                break;
        }
        _pan = Math.Clamp(_pan, -1f, 1f);
        // A ring that starts to flit later sets off from where it is.
        if (place.Move != Move.Flit)
        {
            _from = _to = _pan;
            _glide = 1f;
        }
        return _pan;
    }
}
