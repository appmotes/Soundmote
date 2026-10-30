using System.Runtime.CompilerServices;
using Soundmote.Core.Dsp;
using Soundmote.Core.Sounds;

namespace Soundmote.Core.Recipes;

/// <summary>
/// Plays a <see cref="Recipe"/> (S10). Every 32 samples it updates the signals and each part's filters and gains
/// from their expressions, once per ear; between updates the parts run sample by sample. Everything is allocated in
/// the constructor, so rendering is as safe on the audio thread as a hand-written sound (A5).
/// </summary>
public sealed class RecipeSound : Sound
{
    /// <summary>The loudness every sound is trimmed to at full amount, in dBFS RMS (S4).</summary>
    public const float TargetDb = -23f;

    private const int Sub = 32;
    private static readonly ConditionalWeakTable<Recipe, StrongBox<float>> Trims = new();

    private readonly float[][] _slots;
    private readonly Signal[] _signals;
    private readonly Part[] _parts;
    private readonly float _trim;
    private readonly bool _takesStrikes;
    private readonly BeatClock _clock = new();
    private Smoother _amount;

    private RecipeSound(Recipe recipe, int sampleRate, ulong seed, float trim) : base(sampleRate)
    {
        Recipe = recipe;
        _trim = trim;
        var rng = new Rng(seed);
        var names = recipe.Names;
        _slots = [new float[names.Count], new float[names.Count]];
        _slots[0][1] = -1f;
        _slots[1][1] = 1f;
        _amount = new Smoother(recipe.Glide, sampleRate);
        _signals = recipe.Signals.Select((s, i) => Signal.Create(s, i + 3, names, sampleRate, rng.Fork())).ToArray();
        _parts = recipe.Parts.Select(p => Part.Create(p, names, sampleRate, rng.Fork(), _clock)).ToArray();
        _takesStrikes = _parts.Any(p => p is Shots { TakesStrikes: true });
    }

    public Recipe Recipe { get; }

    /// <summary>Read on the audio thread every block, so worked out once (A5).</summary>
    public override bool TakesStrikes => _takesStrikes;

    public override void Strike(float strength)
    {
        foreach (var part in _parts)
            if (part is Shots { TakesStrikes: true } shots) shots.Strike(strength);
    }

    /// <summary>
    /// A sound for the recipe at the shared loudness: the recipe's own trim if it has one, else one measured once per
    /// recipe and kept (S10).
    /// </summary>
    public static RecipeSound Create(Recipe recipe, int sampleRate, ulong seed) =>
        new(recipe, sampleRate, seed, recipe.Trim ?? Trims.GetValue(recipe, r => new StrongBox<float>(Measure(r))).Value);

    /// <summary>A sound that takes strikes is measured over its loudest this-many seconds…</summary>
    public const int StrikeSeconds = 4;

    /// <summary>…within this long after it is struck (S12): a bowl's first seconds, a rumble's peak.</summary>
    public const int StrikeSearchSeconds = 20;

    /// <summary>
    /// The gain that brings the recipe to <see cref="TargetDb"/> at full amount, from 8 s at 24 kHz after a second to
    /// settle. A sound that takes strikes is struck once at full strength and measured over its loudest
    /// <see cref="StrikeSeconds"/> (S12).
    /// </summary>
    public static float Measure(Recipe recipe)
    {
        const int rate = 24000;
        var sound = new RecipeSound(recipe, rate, 0x7E57, 1f);
        var struck = sound.TakesStrikes;
        if (struck) sound.Strike(1f);
        var left = new float[512];
        var right = new float[512];
        var blocks = new List<double>();
        for (var b = 0; b < (struck ? StrikeSearchSeconds : 9) * rate / 512; b++)
        {
            sound.Render(left, right, 1f);
            if (!struck && b < rate / 512) continue;
            double sum = 0;
            for (var i = 0; i < left.Length; i++) sum += left[i] * left[i] + right[i] * right[i];
            blocks.Add(sum / (2 * left.Length));
        }
        var rms = Math.Sqrt(struck ? LoudestPower(blocks, StrikeSeconds * rate / 512) : blocks.Average());
        if (rms < 1e-7) return 1f;
        return (float)Math.Clamp(Math.Pow(10, (TargetDb - 20 * Math.Log10(rms)) / 20), 0.01, 50);
    }

    /// <summary>The highest mean of <paramref name="span"/> neighbouring blocks' power, or of all of them if fewer.</summary>
    public static double LoudestPower(IReadOnlyList<double> blocks, int span)
    {
        span = Math.Min(span, blocks.Count);
        double sum = 0, best = 0;
        for (var i = 0; i < blocks.Count; i++)
        {
            sum += blocks[i] - (i >= span ? blocks[i - span] : 0);
            if (i >= span - 1) best = Math.Max(best, sum / span);
        }
        return best;
    }

    public override void Render(Span<float> left, Span<float> right, float amount)
    {
        // The renderer sets the beat before each block; a sound alone carries it on from where the last block ended.
        var beat = Beat;
        _clock.Step = Tempo / 60 / SampleRate;
        for (var offset = 0; offset < left.Length; offset += Sub)
        {
            var n = Math.Min(Sub, left.Length - offset);
            var a = 0f;
            for (var i = 0; i < n; i++) a = _amount.Next(amount);
            _slots[0][0] = _slots[1][0] = a;
            _slots[0][2] = _slots[1][2] = Waver;
            foreach (var signal in _signals) signal.Update(_slots, n);

            var l = left.Slice(offset, n);
            var r = right.Slice(offset, n);
            l.Clear();
            r.Clear();
            _clock.At = beat;
            foreach (var part in _parts) part.Render(l, r, _slots, a);
            beat += n * _clock.Step;
            for (var i = 0; i < n; i++)
            {
                l[i] = Math.Clamp(l[i] * _trim, -4f, 4f);
                r[i] = Math.Clamp(r[i] * _trim, -4f, 4f);
            }
        }
        Beat = beat;
    }

    private static float Finite(float x) => float.IsFinite(x) ? x : 0f;

    private abstract class Signal(int slot)
    {
        protected readonly int Slot = slot;

        public abstract void Update(float[][] slots, int samples);

        public static Signal Create(SignalSpec spec, int slot, IReadOnlyList<string> names, int rate, Rng rng) => spec switch
        {
            WalkSignal w => new Walk(slot, w, rate, rng),
            WaveSignal w => new Wave(slot, w, rate, rng),
            LagSignal l => new Lag(slot, Expr.Parse(l.Of, names), l.Seconds, rate),
            ExprSignal e => new Formula(slot, Expr.Parse(e.Expr, names)),
            _ => throw new RecipeException($"Unknown signal {spec.GetType().Name}.")
        };
    }

    private sealed class Walk : Signal
    {
        private readonly RandomWalk[] _walks;
        private Rng _rng;

        public Walk(int slot, WalkSignal spec, int rate, Rng rng) : base(slot)
        {
            _rng = rng;
            _walks = new RandomWalk[spec.PerEar ? 2 : 1];
            for (var i = 0; i < _walks.Length; i++) _walks[i] = new RandomWalk(spec.Period, rate, ref _rng);
        }

        public override void Update(float[][] slots, int samples)
        {
            var left = _walks[0].Advance(samples, ref _rng);
            slots[0][Slot] = left;
            slots[1][Slot] = _walks.Length > 1 ? _walks[1].Advance(samples, ref _rng) : left;
        }
    }

    /// <summary>Asymmetric swells: rise over a share of each cycle, drain over the rest; each cycle new length and size.</summary>
    private sealed class Wave : Signal
    {
        private readonly WaveSignal _spec;
        private readonly int _rate;
        private readonly float[] _phase, _step, _size;
        private Rng _rng;

        public Wave(int slot, WaveSignal spec, int rate, Rng rng) : base(slot)
        {
            _spec = spec;
            _rate = rate;
            _rng = rng;
            var count = spec.PerEar ? 2 : 1;
            (_phase, _step, _size) = (new float[count], new float[count], new float[count]);
            for (var i = 0; i < count; i++)
            {
                NewCycle(i);
                _phase[i] = _rng.NextFloat();
            }
        }

        public override void Update(float[][] slots, int samples)
        {
            for (var i = 0; i < _phase.Length; i++)
            {
                _phase[i] += _step[i] * samples;
                if (_phase[i] >= 1f)
                {
                    var carry = _phase[i] - 1f;
                    NewCycle(i);
                    _phase[i] = carry;
                }
            }
            slots[0][Slot] = Value(0);
            slots[1][Slot] = Value(_phase.Length - 1);
        }

        private float Value(int i)
        {
            var p = Math.Clamp(_phase[i], 0f, 1f);
            var rise = _spec.Rise;
            var shape = p < rise
                ? 0.5f - 0.5f * MathF.Cos(MathF.PI * p / rise)
                : MathF.Pow(1f - (p - rise) / (1f - rise), 1.8f);
            return _size[i] * (_spec.Floor + (1f - _spec.Floor) * shape);
        }

        private void NewCycle(int i)
        {
            _step[i] = 1f / (_rng.Range(_spec.MinPeriod, _spec.MaxPeriod) * _rate);
            _size[i] = _rng.Range(_spec.MinSize, _spec.MaxSize);
        }
    }

    private sealed class Lag(int slot, Expr of, float seconds, int rate) : Signal(slot)
    {
        private readonly float[] _value = new float[2];

        public override void Update(float[][] slots, int samples)
        {
            var coeff = 1f - MathF.Exp(-samples / (seconds * rate));
            for (var ear = 0; ear < 2; ear++)
            {
                _value[ear] += (Finite(of.Eval(slots[ear])) - _value[ear]) * coeff;
                slots[ear][Slot] = _value[ear];
            }
        }
    }

    private sealed class Formula(int slot, Expr expr) : Signal(slot)
    {
        public override void Update(float[][] slots, int samples)
        {
            slots[0][Slot] = Finite(expr.Eval(slots[0]));
            slots[1][Slot] = Finite(expr.Eval(slots[1]));
        }
    }

    private abstract class Part(Expr gain)
    {
        /// <summary>Adds this part's sound for one update block to both ears.</summary>
        public abstract void Render(Span<float> left, Span<float> right, float[][] slots, float amount);

        protected float Gain(float[] slots) => Math.Clamp(Finite(gain.Eval(slots)), 0f, 4f);

        public static Part Create(PartSpec spec, IReadOnlyList<string> names, int rate, Rng rng, BeatClock clock)
        {
            var gain = Expr.Parse(spec.Gain, names);
            return spec switch
            {
                NoisePart n => new Noise(n, names, rate, rng, gain),
                EventsPart e => new Events(e, names, rate, rng, gain),
                PingsPart p => new Pings(p, names, rate, rng, gain),
                ChoirPart => new Voices(rate, rng, gain),
                SamplePart { Loop: true } s => new Looper(s, names, rate, rng, gain),
                SamplePart s => new Shots(s, names, rate, rng, gain, clock),
                _ => throw new RecipeException($"Unknown part {spec.GetType().Name}.")
            };
        }
    }

    /// <summary>White, pink and brown noise mixed per ear, with the part's filters per ear.</summary>
    private struct Source(NoiseMix mix)
    {
        private PinkNoise _pink;
        private BrownNoise _brown;

        public float Next(ref Rng rng)
        {
            var x = 0f;
            if (mix.White > 0) x += mix.White * rng.NextBipolar();
            if (mix.Pink > 0) x += mix.Pink * _pink.Next(ref rng);
            if (mix.Brown > 0) x += mix.Brown * _brown.Next(ref rng);
            return x;
        }
    }

    private sealed class Filters
    {
        private readonly FilterKind[] _kinds;
        private readonly Expr[] _freq, _q;
        private readonly Svf[][] _svf;
        private readonly int _rate;

        public Filters(IReadOnlyList<FilterSpec> specs, IReadOnlyList<string> names, int rate)
        {
            _rate = rate;
            _kinds = specs.Select(f => f.Kind).ToArray();
            _freq = specs.Select(f => Expr.Parse(f.Freq, names)).ToArray();
            _q = specs.Select(f => Expr.Parse(f.Q, names)).ToArray();
            _svf = [new Svf[specs.Count], new Svf[specs.Count]];
        }

        public void Update(int ear, float[] slots)
        {
            for (var i = 0; i < _kinds.Length; i++)
                _svf[ear][i].Set(Math.Clamp(Finite(_freq[i].Eval(slots)), 20f, 20000f), Math.Clamp(Finite(_q[i].Eval(slots)), 0.3f, 30f), _rate);
        }

        public float Process(int ear, float x)
        {
            var svf = _svf[ear];
            for (var i = 0; i < _kinds.Length; i++)
            {
                x = _kinds[i] switch
                {
                    FilterKind.LowPass => svf[i].Low(x),
                    FilterKind.HighPass => svf[i].High(x),
                    _ => svf[i].Band(x)
                };
            }
            return x;
        }
    }

    private sealed class Noise : Part
    {
        private readonly Source[] _source;
        private readonly Filters _filters;
        private Rng _rng;

        public Noise(NoisePart spec, IReadOnlyList<string> names, int rate, Rng rng, Expr gain) : base(gain)
        {
            _rng = rng;
            _source = [new Source(spec.Source), new Source(spec.Source)];
            _filters = new Filters(spec.Filters, names, rate);
        }

        public override void Render(Span<float> left, Span<float> right, float[][] slots, float amount)
        {
            for (var ear = 0; ear < 2; ear++)
            {
                var gain = Gain(slots[ear]);
                _filters.Update(ear, slots[ear]);
                var output = ear == 0 ? left : right;
                for (var i = 0; i < output.Length; i++)
                    output[i] += gain * _filters.Process(ear, _source[ear].Next(ref _rng));
            }
        }
    }

    /// <summary>A beat grid: events fire on it with their own jitter, so jitter never accumulates into drift.</summary>
    private struct Metronome(float bpm, float offset, float jitter, int rate)
    {
        private readonly double _length = 60.0 / bpm * rate;
        private double _next = (0.25 + offset) * (60.0 / bpm * rate);
        private double _fireAt = (0.25 + offset) * (60.0 / bpm * rate);
        private long _t;

        public bool Tick(ref Rng rng)
        {
            if (++_t < _fireAt) return false;
            _next += _length;
            _fireAt = _next + rng.Range(-jitter, jitter) * rate;
            return true;
        }
    }

    private sealed class Events : Part
    {
        private readonly EventsPart _spec;
        private readonly Expr? _rate;
        private readonly Source[] _source;
        private readonly Filters _filters;
        private readonly float[] _env = new float[2], _gain = new float[2], _chance = new float[2];
        private readonly int[] _burst = new int[2];
        private readonly float _decay, _clickChance;
        private readonly int _rateHz;
        private Metronome _metronome;
        private Rng _rng;

        public Events(EventsPart spec, IReadOnlyList<string> names, int rate, Rng rng, Expr gain) : base(gain)
        {
            _spec = spec;
            _rng = rng;
            _rateHz = rate;
            _rate = spec.Trigger.Rate is { } r ? Expr.Parse(r, names) : null;
            _metronome = new Metronome(spec.Trigger.Bpm, spec.Trigger.Offset, spec.Trigger.Jitter, rate);
            _source = [new Source(spec.Source), new Source(spec.Source)];
            _filters = new Filters(spec.Filters, names, rate);
            _decay = MathF.Exp(-1f / (spec.Decay * rate));
            _clickChance = 1f / (spec.Spacing * rate);
        }

        public override void Render(Span<float> left, Span<float> right, float[][] slots, float amount)
        {
            for (var ear = 0; ear < 2; ear++)
            {
                _gain[ear] = Gain(slots[ear]);
                _filters.Update(ear, slots[ear]);
                if (_rate is not null) _chance[ear] = Math.Clamp(Finite(_rate.Eval(slots[ear])), 0f, 4000f) / _rateHz;
            }
            for (var i = 0; i < left.Length; i++)
            {
                var beat = _rate is null && _metronome.Tick(ref _rng);
                for (var ear = 0; ear < 2; ear++)
                {
                    var fire = _rate is null ? beat : _rng.NextFloat() < _chance[ear];
                    if (fire)
                    {
                        if (_spec.Burst.Max > 1) _burst[ear] = _spec.Burst.Min + (int)(_rng.NextFloat() * (_spec.Burst.Max - _spec.Burst.Min + 1));
                        else _env[ear] += _rng.Range(_spec.Level.Min, _spec.Level.Max);
                    }
                    if (_burst[ear] > 0 && _rng.NextFloat() < _clickChance)
                    {
                        _env[ear] += _rng.Range(_spec.Level.Min, _spec.Level.Max);
                        _burst[ear]--;
                    }
                    _env[ear] *= _decay;
                    var x = _gain[ear] * _env[ear] * _filters.Process(ear, _source[ear].Next(ref _rng));
                    if (ear == 0) left[i] += x;
                    else right[i] += x;
                }
            }
        }
    }

    private sealed class Pings : Part
    {
        private readonly PingsPart _spec;
        private readonly Expr? _rate;
        private readonly Voice[] _voices;
        private readonly float _rise;
        private readonly int _rateHz, _spacing;
        private OnePole _lowLeft, _lowRight;
        private int _burstLeft, _untilPulse;
        private float _pitch, _pan;
        private Metronome _metronome;
        private Rng _rng;

        public Pings(PingsPart spec, IReadOnlyList<string> names, int rate, Rng rng, Expr gain) : base(gain)
        {
            _spec = spec;
            _rng = rng;
            _rateHz = rate;
            _rate = spec.Trigger.Rate is { } r ? Expr.Parse(r, names) : null;
            _metronome = new Metronome(spec.Trigger.Bpm, spec.Trigger.Offset, spec.Trigger.Jitter, rate);
            _voices = new Voice[spec.Voices];
            _rise = MathF.Exp(MathF.Log(spec.Glide) / (spec.GlideTime * rate));
            _spacing = Math.Max(1, (int)(spec.Spacing * rate));
            if (spec.Lowpass > 0)
            {
                _lowLeft.SetCutoff(spec.Lowpass, rate);
                _lowRight.SetCutoff(spec.Lowpass, rate);
            }
        }

        public override void Render(Span<float> left, Span<float> right, float[][] slots, float amount)
        {
            var gain = Gain(slots[0]);
            var chance = _rate is null ? 0f : Math.Clamp(Finite(_rate.Eval(slots[0])), 0f, 2000f) / _rateHz;
            for (var i = 0; i < left.Length; i++)
            {
                var fire = _rate is null ? _metronome.Tick(ref _rng) : _rng.NextFloat() < chance;
                if (fire) Trigger();
                else if (_burstLeft > 0 && --_untilPulse <= 0)
                {
                    Start();
                    _burstLeft--;
                    _untilPulse = _spacing;
                }
                float l = 0f, r = 0f;
                for (var v = 0; v < _voices.Length; v++)
                {
                    ref var d = ref _voices[v];
                    if (!d.Active) continue;
                    var s = MathF.Sin(d.Phase) * d.Amp;
                    d.Phase += d.Step;
                    if (d.Phase > MathF.Tau) d.Phase -= MathF.Tau;
                    d.Step = MathF.Min(d.Step * _rise, MathF.PI * 0.9f);
                    d.Amp *= d.Decay;
                    if (d.Amp < 1e-5f) d.Active = false;
                    l += s * d.Left;
                    r += s * d.Right;
                }
                if (_spec.Lowpass > 0)
                {
                    l = _lowLeft.Low(l);
                    r = _lowRight.Low(r);
                }
                left[i] += gain * l;
                right[i] += gain * r;
            }
        }

        /// <summary>A new tone, or the first of a burst; every pulse of a burst keeps its pitch and place.</summary>
        private void Trigger()
        {
            _pitch = _rng.Range(_spec.Pitch.Min, _spec.Pitch.Max);
            _pan = Math.Clamp(0.5f + 0.5f * _spec.Place + (_rng.NextFloat() - 0.5f) * _spec.Pan, 0f, 1f);
            Start();
            _burstLeft = _spec.Burst.Max > 1 ? _spec.Burst.Min - 1 + (int)(_rng.NextFloat() * (_spec.Burst.Max - _spec.Burst.Min + 1)) : 0;
            _untilPulse = _spacing;
        }

        /// <summary>Takes a free voice, or the quietest one when all ring.</summary>
        private void Start()
        {
            var pick = 0;
            for (var v = 0; v < _voices.Length; v++)
            {
                if (!_voices[v].Active)
                {
                    pick = v;
                    break;
                }
                if (_voices[v].Amp < _voices[pick].Amp) pick = v;
            }
            var pan = _pan;
            _voices[pick] = new Voice
            {
                Active = true,
                Step = _pitch * MathF.Tau / _rateHz,
                Amp = _rng.Range(_spec.Level.Min, _spec.Level.Max),
                Decay = MathF.Exp(-1f / (_rng.Range(_spec.Decay.Min, _spec.Decay.Max) * _rateHz)),
                Left = MathF.Sqrt(1f - pan),
                Right = MathF.Sqrt(pan)
            };
        }

        private struct Voice
        {
            public bool Active;
            public float Phase, Step, Amp, Decay, Left, Right;
        }
    }

    /// <summary>Reads a sample between two frames, wrapping at its end when it loops.</summary>
    private static (float Left, float Right) Read(Sample sample, double position, bool wrap)
    {
        var i = (int)position;
        var k = (float)(position - i);
        var j = i + 1;
        if (j >= sample.Length) j = wrap ? 0 : i;
        return (sample.Left[i] + (sample.Left[j] - sample.Left[i]) * k, sample.Right[i] + (sample.Right[j] - sample.Right[i]) * k);
    }

    /// <summary>
    /// A loop of files (S12) with two read heads: one plays; on a change the other starts somewhere in another file and
    /// the two cross with equal power over the part's fade.
    /// </summary>
    private sealed class Looper : Part
    {
        private readonly Sample[] _takes;
        private readonly Filters _filters;
        private readonly float[] _gain = new float[2];
        private readonly double[] _position = new double[2];
        private readonly int[] _take = new int[2];
        private readonly int _rate, _fade;
        private readonly float _change;
        private readonly Expr _tune;
        private double _speed = 1;
        private int _active, _fading;
        private double _untilChange;
        private Rng _rng;

        public Looper(SamplePart spec, IReadOnlyList<string> names, int rate, Rng rng, Expr gain) : base(gain)
        {
            _rng = rng;
            _rate = rate;
            _tune = Expr.Parse(spec.Tune, names);
            _takes = spec.Files.Select(f => Sample.Get(f, spec.Fade)).ToArray();
            _filters = new Filters(spec.Filters, names, rate);
            _fade = Math.Max(1, (int)(spec.Fade * rate));
            _change = spec.Change;
            Begin(0);
            _untilChange = NextChange();
        }

        private double NextChange() => _change * _rate * _rng.Range(0.5f, 1.5f);

        /// <summary>Puts a head somewhere at random in a file other than the one playing, where there is more than one.</summary>
        private void Begin(int head)
        {
            var take = (int)(_rng.NextFloat() * _takes.Length) % _takes.Length;
            if (_takes.Length > 1 && head != _active && take == _take[_active]) take = (take + 1) % _takes.Length;
            _take[head] = take;
            _position[head] = _rng.NextFloat() * (_takes[take].Length - 1);
        }

        public override void Render(Span<float> left, Span<float> right, float[][] slots, float amount)
        {
            for (var ear = 0; ear < 2; ear++)
            {
                _gain[ear] = Gain(slots[ear]);
                _filters.Update(ear, slots[ear]);
            }
            // One speed for both ears: they read the same head. Evaluated per update block, like the gain.
            _speed = Math.Clamp(Finite(_tune.Eval(slots[0])), 0.5f, 2f);
            for (var i = 0; i < left.Length; i++)
            {
                if (_fading == 0 && --_untilChange <= 0)
                {
                    Begin(1 - _active);
                    _fading = _fade;
                    _untilChange = NextChange();
                }
                var (l, r) = Head(_active);
                if (_fading > 0)
                {
                    var t = 1f - (float)_fading / _fade;
                    var (l2, r2) = Head(1 - _active);
                    var outGain = MathF.Cos(t * MathF.PI / 2);
                    var inGain = MathF.Sin(t * MathF.PI / 2);
                    l = l * outGain + l2 * inGain;
                    r = r * outGain + r2 * inGain;
                    if (--_fading == 0) _active = 1 - _active;
                }
                left[i] += _gain[0] * _filters.Process(0, l);
                right[i] += _gain[1] * _filters.Process(1, r);
            }
        }

        /// <summary>The head's frame now, moving it on one output frame.</summary>
        private (float, float) Head(int head)
        {
            var sample = _takes[_take[head]];
            var frame = Read(sample, _position[head], wrap: true);
            _position[head] += _speed * sample.Rate / _rate;
            if (_position[head] >= sample.Length) _position[head] -= sample.Length;
            return frame;
        }
    }

    /// <summary>
    /// Whole files played once each time the part fires (S12): on its trigger, or at the ring's strikes. Up to eight
    /// sound at once; a ninth takes the place of the one furthest through.
    /// </summary>
    private sealed class Shots : Part
    {
        private const int MaxShots = 8;

        private readonly SamplePart _spec;
        private readonly Sample[] _takes;
        private readonly Filters _filters;
        private readonly Expr? _rate;
        private readonly Shot[] _shots = new Shot[MaxShots];
        private readonly float[] _gain = new float[2];
        private readonly int _hz;
        private readonly BeatClock _clock;
        private Metronome _metronome;
        private float _chance, _pending;
        private Rng _rng;

        public Shots(SamplePart spec, IReadOnlyList<string> names, int rate, Rng rng, Expr gain, BeatClock clock) : base(gain)
        {
            _spec = spec;
            _clock = clock;
            _rng = rng;
            _hz = rate;
            _takes = spec.Files.Select(f => Sample.Get(f)).ToArray();
            _filters = new Filters(spec.Filters, names, rate);
            _rate = spec.Trigger?.Rate is { } r ? Expr.Parse(r, names) : null;
            _metronome = spec.Trigger is { Rate: null } t ? new Metronome(t.Bpm, t.Offset, t.Jitter, rate) : default;
        }

        public bool TakesStrikes => _spec.TakesStrikes;

        public void Strike(float strength) => _pending = MathF.Max(_pending, strength);

        public override void Render(Span<float> left, Span<float> right, float[][] slots, float amount)
        {
            for (var ear = 0; ear < 2; ear++)
            {
                _gain[ear] = Gain(slots[ear]);
                _filters.Update(ear, slots[ear]);
            }
            if (_rate is not null) _chance = Math.Clamp(Finite(_rate.Eval(slots[0])), 0f, 50f) / _hz;
            if (_pending > 0)
            {
                Fire(_pending);
                _pending = 0;
            }
            if (_spec.Pattern is { } pattern) Hits(pattern, _clock.At, _clock.At + left.Length * _clock.Step);
            for (var i = 0; i < left.Length; i++)
            {
                if (_spec.Trigger is not null && (_rate is not null ? _rng.NextFloat() < _chance : _metronome.Tick(ref _rng))) Fire(1f);
                float l = 0, r = 0;
                for (var s = 0; s < MaxShots; s++)
                {
                    ref var shot = ref _shots[s];
                    if (!shot.Active) continue;
                    var sample = _takes[shot.Take];
                    var (sl, sr) = Read(sample, shot.Position, wrap: false);
                    l += sl * shot.Left;
                    r += sr * shot.Right;
                    shot.Position += shot.Step;
                    if (shot.Position >= sample.Length - 1) shot.Active = false;
                }
                left[i] += _gain[0] * _filters.Process(0, l);
                right[i] += _gain[1] * _filters.Process(1, r);
            }
        }

        /// <summary>Fires every hit of the pattern whose beat falls in [from, to): at the start of this update block, at most 32 samples early.</summary>
        private void Hits(Pattern pattern, double from, double to)
        {
            for (var cycle = Math.Floor(from / pattern.Every) * pattern.Every; cycle < to; cycle += pattern.Every)
                for (var i = 0; i < pattern.At.Count; i++)
                {
                    var at = cycle + pattern.At[i];
                    if (at >= from && at < to) Fire(pattern.Levels[i]);
                }
        }

        private void Fire(float strength)
        {
            var slot = 0;
            var furthest = -1.0;
            for (var s = 0; s < MaxShots; s++)
            {
                if (!_shots[s].Active)
                {
                    slot = s;
                    break;
                }
                var through = _shots[s].Position / _takes[_shots[s].Take].Length;
                if (through > furthest)
                {
                    furthest = through;
                    slot = s;
                }
            }
            var take = (int)(_rng.NextFloat() * _takes.Length) % _takes.Length;
            var level = strength * _rng.Range(_spec.Level.Min, _spec.Level.Max);
            // Equal power around the middle, scaled so the middle is unity (A13).
            var pan = _spec.Spread * _rng.NextBipolar();
            var angle = (pan + 1f) * MathF.PI / 4f;
            _shots[slot] = new Shot
            {
                Active = true,
                Take = take,
                Position = 0,
                Step = (double)_takes[take].Rate / _hz * _rng.Range(_spec.Pitch.Min, _spec.Pitch.Max),
                Left = level * MathF.Sqrt(2f) * MathF.Cos(angle),
                Right = level * MathF.Sqrt(2f) * MathF.Sin(angle)
            };
        }

        private struct Shot
        {
            public bool Active;
            public int Take;
            public double Position, Step;
            public float Left, Right;
        }
    }

    /// <summary>The formant choir as a part; it reads the amount itself to bring voices in.</summary>
    private sealed class Voices(int rate, Rng rng, Expr gain) : Part(gain)
    {
        private readonly Choir _choir = new(rate, rng.NextULong());
        private readonly float[] _left = new float[Sub], _right = new float[Sub];

        public override void Render(Span<float> left, Span<float> right, float[][] slots, float amount)
        {
            var gain = Gain(slots[0]);
            var l = _left.AsSpan(0, left.Length);
            var r = _right.AsSpan(0, left.Length);
            _choir.Render(l, r, amount);
            for (var i = 0; i < left.Length; i++)
            {
                left[i] += gain * l[i];
                right[i] += gain * r[i];
            }
        }
    }

    /// <summary>Where the scene's beat stands for the update block being played, and how far it moves per sample (A16).</summary>
    private sealed class BeatClock
    {
        public double At, Step;
    }
}
