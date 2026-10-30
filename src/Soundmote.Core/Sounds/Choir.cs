using Soundmote.Core.Dsp;

namespace Soundmote.Core.Sounds;

/// <summary>
/// A wordless choir: the recipe language's <c>choir</c> part (S10). Six voices sing slow chords in D minor, each with its own vibrato,
/// swell and slight detune, and move to the nearest note of the next chord at its own moment. The sum goes
/// through vowel formants that drift between "ah", "oh" and "oo". Amount brings voices in one by one.
/// </summary>
public sealed class Choir : Sound
{
    private const int Sub = 64;
    private const float TwoPi = MathF.PI * 2f;

    // Pitch classes; the first is the root the bass sings.
    private static readonly int[][] Chords =
    [
        [2, 5, 9], [10, 2, 5], [5, 9, 0], [0, 4, 7],
        [2, 5, 9], [7, 10, 2], [10, 2, 5], [9, 0, 4]
    ];

    // Low and high MIDI note, the amount at which the part starts singing, and its place in the stereo field.
    private static readonly (int Low, int High, float Entry, float Pan)[] Parts =
    [
        (40, 55, 0.15f, 0.5f),
        (48, 62, 0.05f, 0.3f),
        (55, 69, 0.00f, 0.65f),
        (60, 76, 0.25f, 0.4f),
        (55, 69, 0.45f, 0.1f),
        (60, 76, 0.60f, 0.9f)
    ];

    // Formant frequencies and relative gains for "ah", "oh" and "oo".
    private static readonly float[][] VowelFreq = [[800, 1150, 2900, 3900], [450, 800, 2830, 3800], [325, 700, 2530, 3500]];
    private static readonly float[][] VowelGain = [[1f, 0.5f, 0.2f, 0.12f], [1f, 0.35f, 0.1f, 0.08f], [1f, 0.2f, 0.06f, 0.05f]];
    private static readonly float[] FormantQ = [6f, 8f, 12f, 14f];

    private readonly Voice[] _voices = new Voice[Parts.Length];
    private readonly Svf[] _formantLeft = new Svf[4];
    private readonly Svf[] _formantRight = new Svf[4];
    private readonly float[] _formantGain = new float[4];
    private readonly float _invRate, _glide;
    private Rng _rng;
    private Smoother _amount;
    private RandomWalk _vowel;
    private OnePole _warmLeft, _warmRight;
    private int _chord, _untilChange, _untilUpdate;

    public Choir(int sampleRate, ulong seed) : base(sampleRate)
    {
        _rng = new Rng(seed);
        _invRate = 1f / sampleRate;
        _glide = 1f - MathF.Exp(-1f / (0.35f * sampleRate));
        _amount = new Smoother(0.3f, sampleRate);
        _vowel = new RandomWalk(14f, sampleRate, ref _rng);
        _warmLeft.SetCutoff(900f, sampleRate);
        _warmRight.SetCutoff(900f, sampleRate);
        _untilChange = NextChordLength();

        for (var i = 0; i < Parts.Length; i++)
        {
            var part = Parts[i];
            var midi = Nearest(i, (part.Low + part.High) / 2, Chords[0]);
            var voice = new Voice
            {
                Midi = midi,
                Freq = MidiToHz(midi),
                VibRate = _rng.Range(4.6f, 5.6f),
                VibDepth = _rng.Range(0.003f, 0.006f),
                VibPhase = _rng.NextFloat(),
                Phase = _rng.NextFloat(),
                Left = MathF.Sqrt(1f - part.Pan),
                Right = MathF.Sqrt(part.Pan),
                Gain = new Smoother(0.6f, sampleRate),
                Swell = new RandomWalk(_rng.Range(6f, 11f), sampleRate, ref _rng),
                Detune = new RandomWalk(_rng.Range(3f, 7f), sampleRate, ref _rng)
            };
            voice.Target = voice.Freq;
            voice.Tone.SetCutoff(MathF.Min(4500f, voice.Freq * 14f), sampleRate);
            _voices[i] = voice;
        }
    }

    public override void Render(Span<float> left, Span<float> right, float amount)
    {
        for (var n = 0; n < left.Length; n++)
        {
            _amount.Next(amount);
            // A running count, not n % Sub: callers may render in blocks of any size.
            if (--_untilUpdate <= 0)
            {
                UpdateBlock();
                _untilUpdate = Sub;
            }

            float sumLeft = 0f, sumRight = 0f;
            foreach (var v in _voices)
            {
                var gain = v.Gain.Next(v.GainTarget);
                if (gain < 1e-5f && v.GainTarget == 0f) continue;

                v.Freq += (v.Target - v.Freq) * _glide;
                v.VibPhase += v.VibRate * _invRate;
                if (v.VibPhase >= 1f) v.VibPhase -= 1f;
                var freq = v.Freq * (1f + v.VibDepth * MathF.Sin(TwoPi * v.VibPhase) + v.DetuneNow);
                var dt = freq * _invRate;
                v.Phase += dt;
                if (v.Phase >= 1f) v.Phase -= 1f;

                var saw = 2f * v.Phase - 1f - PolyBlep(v.Phase, dt);
                var s = v.Tone.Low(saw) * gain + _rng.NextBipolar() * gain * 0.12f;
                sumLeft += s * v.Left;
                sumRight += s * v.Right;
            }

            float l = 0.3f * _warmLeft.Low(sumLeft), r = 0.3f * _warmRight.Low(sumRight);
            for (var f = 0; f < 4; f++)
            {
                l += _formantGain[f] * _formantLeft[f].Band(sumLeft);
                r += _formantGain[f] * _formantRight[f].Band(sumRight);
            }
            left[n] = l;
            right[n] = r;
        }
    }

    private void UpdateBlock()
    {
        var a = _amount.Value;

        var vowel = Math.Clamp(0.6f + 0.7f * _vowel.Advance(Sub, ref _rng), 0f, 2f);
        var lo = Math.Min(1, (int)vowel);
        var t = vowel - lo;
        for (var f = 0; f < 4; f++)
        {
            var freq = VowelFreq[lo][f] + (VowelFreq[lo + 1][f] - VowelFreq[lo][f]) * t;
            _formantGain[f] = VowelGain[lo][f] + (VowelGain[lo + 1][f] - VowelGain[lo][f]) * t;
            _formantLeft[f].Set(freq, FormantQ[f], SampleRate);
            _formantRight[f].Set(freq * 1.02f, FormantQ[f], SampleRate);
        }

        _untilChange -= Sub;
        if (_untilChange <= 0)
        {
            _chord = (_chord + 1) % Chords.Length;
            _untilChange = NextChordLength();
            for (var i = 0; i < _voices.Length; i++)
            {
                _voices[i].Midi = Nearest(i, _voices[i].Midi, Chords[_chord]);
                _voices[i].MoveIn = (int)(_rng.Range(0f, 1.8f) * SampleRate);
            }
        }

        var level = MathF.Pow(a, 0.7f);
        for (var i = 0; i < _voices.Length; i++)
        {
            var v = _voices[i];
            if (v.MoveIn > 0 && (v.MoveIn -= Sub) <= 0)
            {
                v.Target = MidiToHz(v.Midi);
                v.Tone.SetCutoff(MathF.Min(4500f, v.Target * 14f), SampleRate);
            }
            var entry = Parts[i].Entry;
            var weight = SmoothStep(entry, entry + 0.2f, a);
            var swell = 0.55f + 0.45f * (0.5f + 0.5f * v.Swell.Advance(Sub, ref _rng));
            v.GainTarget = 0.24f * level * weight * swell;
            v.DetuneNow = 0.003f * v.Detune.Advance(Sub, ref _rng);
        }
    }

    private int NextChordLength() => (int)(_rng.Range(20f, 34f) * SampleRate);

    /// <summary>The chord tone in the part's range closest to <paramref name="from"/>; the bass takes the root.</summary>
    private static int Nearest(int part, int from, int[] chord)
    {
        var (low, high, _, _) = Parts[part];
        var best = from;
        var bestDistance = int.MaxValue;
        for (var note = low; note <= high; note++)
        {
            var pc = note % 12;
            var fits = part == 0 ? pc == chord[0] : Array.IndexOf(chord, pc) >= 0;
            var distance = Math.Abs(note - from);
            if (fits && distance < bestDistance)
            {
                best = note;
                bestDistance = distance;
            }
        }
        return best;
    }

    private static float MidiToHz(int midi) => 440f * MathF.Pow(2f, (midi - 69) / 12f);

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float PolyBlep(float t, float dt)
    {
        if (t < dt)
        {
            t /= dt;
            return t + t - t * t - 1f;
        }
        if (t > 1f - dt)
        {
            t = (t - 1f) / dt;
            return t * t + t + t + 1f;
        }
        return 0f;
    }

    private sealed class Voice
    {
        public int Midi, MoveIn;
        public float Freq, Target, Phase, VibPhase, VibRate, VibDepth, Left, Right, GainTarget, DetuneNow;
        public Smoother Gain;
        public RandomWalk Swell, Detune;
        public OnePole Tone;
    }
}
