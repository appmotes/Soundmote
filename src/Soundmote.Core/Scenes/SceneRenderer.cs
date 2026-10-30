using Soundmote.Core.Dsp;
using Soundmote.Core.Output;
using Soundmote.Core.Recipes;
using Soundmote.Core.Sounds;

namespace Soundmote.Core.Scenes;

/// <summary>
/// Plays a scene: reads each ring's curve at the current place on the dial, lets the amount drift, renders the
/// sounds, mixes them through the shared room and a soft limiter (A4). <see cref="Render"/> runs on the audio
/// thread; every other member may be called from any thread (A5).
/// </summary>
public sealed class SceneRenderer : IAudioSource
{
    private const int MaxBlock = 512;
    private const float LimitFrom = 0.7f;

    /// <summary>Gives the morning's rings their own seeds, so a wake never changes how the journey sounds (A6).</summary>
    private const ulong WakeSalt = 0x3A4E5EEDUL;

    /// <summary>Gives a ring's place its own seed beside its strikes' (A6).</summary>
    private const ulong PlaceSalt = 0x51ACE5EEDUL;

    private readonly Scene _scene;
    private readonly TimeProvider _time;
    private readonly object _edit = new();
    private Track[] _playing;
    private Track[] _visible;
    private Rng _addRng;
    private readonly Reverb _reverb;
    private readonly float[] _left = new float[MaxBlock], _right = new float[MaxBlock];
    private readonly float[] _dryLeft = new float[MaxBlock], _dryRight = new float[MaxBlock];
    private readonly float[] _sendLeft = new float[MaxBlock], _sendRight = new float[MaxBlock];
    private readonly float[] _wetLeft = new float[MaxBlock], _wetRight = new float[MaxBlock];
    private Rng _rng;
    private float _master = 1f;
    private double _fadeOut = DefaultFade;
    private WakeState? _wake;
    private float _wakeAmount;
    private double _seconds, _speed = 1, _seekTo = double.NaN, _lastPosition = double.NaN;
    private volatile bool _paused;

    /// <summary>Beats since the scene began, paused or not, at the scene's tempo: the beat every ring keeps (A16).</summary>
    private double _beat, _tempo;

    public SceneRenderer(Scene scene, int sampleRate = 48000, TimeProvider? time = null)
    {
        _scene = scene;
        _time = time ?? TimeProvider.System;
        SampleRate = sampleRate;
        _rng = new Rng(scene.Seed);
        _reverb = new Reverb(sampleRate, scene.RoomSeconds);
        Tempo = scene.Tempo;
        _visible = _playing = scene.Layers.Select((layer, i) => new Track(layer, RecipeSound.Create(layer.Recipe, sampleRate, _rng.NextULong()),
            new RandomWalk(25f, sampleRate, ref _rng), new Smoother(0.05f, sampleRate, layer.Level),
            PlayState.For(layer.Play, scene.Length, StrikeSeed(scene.Seed, i)),
            new PlaceMover(sampleRate, StrikeSeed(scene.Seed, i) ^ PlaceSalt))).ToArray();
        // Rings added later draw their seeds from here, never from the audio thread's generator (A6).
        _addRng = new Rng(scene.Seed ^ 0xADD5EED5UL);
        if (scene.Kind == TimeKind.Journey && scene.Wake is { } wake) _wake = BuildWake(wake, scene.Seed ^ WakeSalt);
    }

    public int SampleRate { get; }

    public Scene Scene => _scene;

    /// <summary>Scene seconds per real second. 1 is real time; the sandbox uses more to hear a whole journey.</summary>
    public double Speed
    {
        get => Volatile.Read(ref _speed);
        set => Volatile.Write(ref _speed, Math.Max(0, value));
    }

    /// <summary>The fade in and out of a pause, unless <see cref="FadeOutSeconds"/> asks for a longer one.</summary>
    public const double DefaultFade = 0.25;

    /// <summary>How long the next pause takes to fall silent, in a straight line. Fading in always takes <see cref="DefaultFade"/>.</summary>
    public double FadeOutSeconds
    {
        get => Volatile.Read(ref _fadeOut);
        set => Volatile.Write(ref _fadeOut, Math.Max(0.01, value));
    }

    /// <summary>Fades out and holds the dial still; fades back in when cleared.</summary>
    public bool Paused
    {
        get => _paused;
        set => _paused = value;
    }

    /// <summary>Time since the scene started, all laps included. For a clock scene, the time of day.</summary>
    public TimeSpan Elapsed => _scene.Kind == TimeKind.Clock
        ? _time.GetLocalNow().TimeOfDay
        : TimeSpan.FromSeconds(Volatile.Read(ref _seconds));

    /// <summary>Where the hand is, from 0 to 1 round the dial.</summary>
    public double Position => PositionAt(Elapsed.TotalSeconds);

    public int Lap => _scene.Kind == TimeKind.Loop ? (int)(Elapsed.TotalSeconds / _scene.Length.TotalSeconds) + 1 : 1;

    /// <summary>A journey past its end, unless a morning waits after it (A14).</summary>
    public bool Finished => _scene.Kind == TimeKind.Journey && Elapsed >= _scene.Length && Volatile.Read(ref _wake) is null;

    public Wake? Wake => Volatile.Read(ref _wake)?.Wake;

    /// <summary>How far the morning has risen, 0 to 1. Written by the audio thread.</summary>
    public float WakeAmount => Volatile.Read(ref _wakeAmount);

    /// <summary>
    /// Sets or clears a journey's morning (A14). Its sounds are built here, on the caller's thread; the audio thread
    /// picks the new morning up at its next block.
    /// </summary>
    public void SetWake(Wake? wake)
    {
        if (_scene.Kind != TimeKind.Journey) return;
        lock (_edit)
        {
            Volatile.Write(ref _wake, wake is null ? null : BuildWake(wake, _addRng.NextULong()));
            if (wake is null) Volatile.Write(ref _wakeAmount, 0f);
        }
    }

    private WakeState BuildWake(Wake wake, ulong seed)
    {
        var rng = new Rng(seed);
        var tracks = new Track[wake.Layers.Count];
        for (var i = 0; i < tracks.Length; i++)
        {
            var layer = wake.Layers[i];
            tracks[i] = new Track(layer, RecipeSound.Create(layer.Recipe, SampleRate, rng.NextULong()),
                new RandomWalk(25f, SampleRate, ref rng), new Smoother(0.05f, SampleRate, layer.Level),
                PlayState.For(Play.Shape, _scene.Length, 0), new PlaceMover(SampleRate, rng.NextULong()));
        }
        return new WakeState(wake, tracks, rng);
    }

    /// <summary>The rings, in order. Indexes below refer to this order, which changes when rings are added or removed.</summary>
    public int LayerCount => Visible.Length;

    public Layer LayerAt(int layer) => Visible[layer].Layer;

    /// <summary>The amount the ring is playing at now, after drift. Written by the audio thread.</summary>
    public float AmountOf(int layer) => Volatile.Read(ref Visible[layer].Amount);

    public bool IsMuted(int layer) => Visible[layer].Muted;

    public void SetMuted(int layer, bool muted) => Visible[layer].Muted = muted;

    /// <summary>The ring's shape now. Starts as the layer's own curve.</summary>
    public Curve CurveOf(int layer) => Volatile.Read(ref Visible[layer].Curve);

    /// <summary>Gives a ring a new shape, heard from the next block on. Curves are immutable, so this is one swap.</summary>
    public void SetCurve(int layer, Curve curve) => Volatile.Write(ref Visible[layer].Curve, curve);

    public Play PlayOf(int layer) => Volatile.Read(ref Visible[layer].Play).Play;

    /// <summary>The ring's strikes over one turn, for drawing; empty unless it plays every or random.</summary>
    public IReadOnlyList<Strike> StrikesOf(int layer) => Volatile.Read(ref Visible[layer].Play).Strikes;

    /// <summary>
    /// Changes how a ring plays (A11). Strikes are worked out here, on the caller's thread, with
    /// <paramref name="seed"/>; the audio thread picks up the new play at its next block.
    /// </summary>
    public void SetPlay(int layer, Play play, ulong seed) =>
        Volatile.Write(ref Visible[layer].Play, PlayState.For(play, _scene.Length, seed));

    public Place PlaceOf(int layer) => Volatile.Read(ref Visible[layer].Place);

    /// <summary>Moves a ring between left and right (A13), heard from the next block; one reference swap.</summary>
    public void SetPlace(int layer, Place place) => Volatile.Write(ref Visible[layer].Place, place);

    /// <summary>Where the ring is heard now, -1 left to 1 right. Written by the audio thread.</summary>
    public float PanOf(int layer) => Volatile.Read(ref Visible[layer].Pan);

    /// <summary>The seed a ring's random strikes start from, so a scene plays the same strikes every time.</summary>
    public static ulong StrikeSeed(ulong sceneSeed, int index) => sceneSeed * 0x9E3779B97F4A7C15UL ^ (ulong)(index + 1);

    /// <summary>
    /// Adds a ring at the end and returns its index. The sound is built here, on the caller's thread; the audio
    /// thread only sees a new array of tracks at its next block, and the ring fades in (A10).
    /// </summary>
    public int AddLayer(Layer layer)
    {
        lock (_edit)
        {
            var track = new Track(layer, RecipeSound.Create(layer.Recipe, SampleRate, _addRng.NextULong()),
                new RandomWalk(25f, SampleRate, ref _addRng), new Smoother(0.05f, SampleRate),
                PlayState.For(layer.Play, _scene.Length, _addRng.NextULong()),
                new PlaceMover(SampleRate, _addRng.NextULong()));
            Volatile.Write(ref _visible, [.. _visible, track]);
            Volatile.Write(ref _playing, [.. Alive(_playing), track]);
            return _visible.Length - 1;
        }
    }

    /// <summary>Takes a ring out of the scene at once; its sound fades out over the next blocks and is then dropped.</summary>
    public void RemoveLayer(int layer)
    {
        lock (_edit)
        {
            var track = _visible[layer];
            track.Removed = true;
            Volatile.Write(ref _visible, _visible.Where(t => t != track).ToArray());
            Volatile.Write(ref _playing, Alive(_playing).ToArray());
        }
    }

    private Track[] Visible => Volatile.Read(ref _visible);

    /// <summary>Tracks still to be heard: all but removed ones whose fade has finished.</summary>
    private static IEnumerable<Track> Alive(Track[] tracks) => tracks.Where(t => !t.Silent);

    public float WaverOf(int layer) => Volatile.Read(ref Visible[layer].Waver);

    /// <summary>Beats a minute for every ring that keeps the beat (A16); a change is heard at the next block.</summary>
    public double Tempo
    {
        get => Volatile.Read(ref _tempo);
        set => Volatile.Write(ref _tempo, Math.Clamp(value, Scene.MinTempo, Scene.MaxTempo));
    }

    /// <summary>Sets how much a ring wavers in pitch (S17); heard from its next block.</summary>
    public void SetWaver(int layer, float waver) => Volatile.Write(ref Visible[layer].Waver, Math.Clamp(waver, 0f, 1f));

    /// <summary>Strikes a ring of strikes once at its next block, as when it is tried, instead of waiting for its first strike.</summary>
    public void StrikeNow(int layer) => Visible[layer].StrikeAsked = true;

    /// <summary>Moves the hand. Ignored by clock scenes.</summary>
    public void Seek(TimeSpan elapsed) => Volatile.Write(ref _seekTo, Math.Max(0, elapsed.TotalSeconds));

    public void Render(Span<float> interleaved)
    {
        for (var offset = 0; offset < interleaved.Length;)
        {
            var frames = Math.Min(MaxBlock, (interleaved.Length - offset) / 2);
            if (frames == 0) break;
            RenderBlock(interleaved.Slice(offset, frames * 2), frames);
            offset += frames * 2;
        }
    }

    private void RenderBlock(Span<float> output, int frames)
    {
        var seek = Volatile.Read(ref _seekTo);
        var jumped = !double.IsNaN(seek);
        if (jumped)
        {
            Volatile.Write(ref _seconds, seek);
            Volatile.Write(ref _seekTo, double.NaN);
        }

        var elapsed = Elapsed.TotalSeconds;
        var position = PositionAt(elapsed);
        var ended = _scene.Kind == TimeKind.Journey && elapsed >= _scene.Length.TotalSeconds;
        // Strikes fire when the hand passes them while playing; a seek or a long gap only moves past them.
        var from = _lastPosition;
        var wrapped = _scene.Kind == TimeKind.Loop && position < from;
        jumped |= double.IsNaN(from) || (!wrapped && Math.Abs(position - from) > 0.05) || (wrapped && from - position < 0.95);
        _lastPosition = position;

        var dryLeft = _dryLeft.AsSpan(0, frames);
        var dryRight = _dryRight.AsSpan(0, frames);
        var sendLeft = _sendLeft.AsSpan(0, frames);
        var sendRight = _sendRight.AsSpan(0, frames);
        dryLeft.Clear();
        dryRight.Clear();
        sendLeft.Clear();
        sendRight.Clear();

        foreach (var track in Volatile.Read(ref _playing))
        {
            if (track.Silent) continue;
            var drift = 1f + track.Layer.Drift * track.Drift.Advance(frames, ref _rng);
            var play = Volatile.Read(ref track.Play);
            float amount;
            var strikes = play.Play is EveryPlay or RandomPlay;
            var struck = false;
            if (strikes)
            {
                struck = track.Pass(play, from, position, jumped, wrapped);
                struck |= track.StrikeIfAsked();
                // A sound that plays something whole at each strike, as a bowl, rings out by itself at full amount.
                amount = track.Sound.TakesStrikes ? 1f : track.Strike(frames, SampleRate);
            }
            else if (play.Play is OnOffPlay onOff)
                amount = onOff.At(position, _scene.Length);
            else
                amount = Volatile.Read(ref track.Curve).At(position) * drift;
            amount = ended ? 0f : Math.Clamp(amount, 0f, 1f);
            Mix(track, amount, frames, position, strikes, struck);
        }

        // The morning plays after the journey's own rings have ended, its amount from the time of day (A14).
        if (Volatile.Read(ref _wake) is { } wake)
        {
            var rise = wake.Level(_time.GetLocalNow().TimeOfDay);
            Volatile.Write(ref _wakeAmount, rise);
            foreach (var track in wake.Tracks)
            {
                var drift = 1f + track.Layer.Drift * track.Drift.Advance(frames, ref wake.Rng);
                Mix(track, Math.Clamp(rise * track.Curve.At(0) * drift, 0f, 1f), frames, position, strikes: false, struck: false);
            }
        }

        var wetLeft = _wetLeft.AsSpan(0, frames);
        var wetRight = _wetRight.AsSpan(0, frames);
        _reverb.Process(sendLeft, sendRight, wetLeft, wetRight);

        var paused = _paused;
        var step = (float)(1 / ((paused ? FadeOutSeconds : DefaultFade) * SampleRate));
        for (var i = 0; i < frames; i++)
        {
            var m = _master = paused ? MathF.Max(0f, _master - step) : MathF.Min(1f, _master + step);
            output[i * 2] = Limit((dryLeft[i] + wetLeft[i]) * m);
            output[i * 2 + 1] = Limit((dryRight[i] + wetRight[i]) * m);
        }

        if (!paused && _scene.Kind != TimeKind.Clock)
            Volatile.Write(ref _seconds, Volatile.Read(ref _seconds) + frames * Speed / SampleRate);
        _beat += frames * Tempo / 60 / SampleRate;
    }


    /// <summary>Renders one ring at an amount, places it (A13) and adds it to the dry bus and the room's send.</summary>
    private void Mix(Track track, float amount, int frames, double position, bool strikes, bool struck)
    {
        var layer = track.Layer;
        var left = _left.AsSpan(0, frames);
        var right = _right.AsSpan(0, frames);
        var dryLeft = _dryLeft.AsSpan(0, frames);
        var dryRight = _dryRight.AsSpan(0, frames);
        var sendLeft = _sendLeft.AsSpan(0, frames);
        var sendRight = _sendRight.AsSpan(0, frames);
        Volatile.Write(ref track.Amount, amount);

        track.Sound.Waver = Volatile.Read(ref track.Waver);
        track.Sound.Beat = _beat;
        track.Sound.Tempo = Tempo;
        track.Sound.Render(left, right, amount);
        var target = track.Muted || track.Removed ? 0f : layer.Level;
        var place = Volatile.Read(ref track.Place);
        var pan = track.Mover.Advance(place, frames, position, strikes, struck);
        Volatile.Write(ref track.Pan, pan);
        // Equal power, scaled so the middle is unity: moving never changes the loudness (A13).
        var angle = (pan + 1f) * MathF.PI / 4f;
        // The middle and both ends are exact, so a centred ring sounds as it always did and a hard pan is silent opposite.
        var toLeft = pan == 0f ? 1f : pan >= 1f ? 0f : MathF.Sqrt(2f) * MathF.Cos(angle);
        var toRight = pan == 0f ? 1f : pan <= -1f ? 0f : MathF.Sqrt(2f) * MathF.Sin(angle);
        // The first block starts where the ring is placed; later ones glide on from the block before.
        var fromLeft = track.Placed ? track.ToLeft : toLeft;
        var fromRight = track.Placed ? track.ToRight : toRight;
        track.Placed = true;
        var width = place.Width;
        var step = 1f / frames;
        for (var i = 0; i < frames; i++)
        {
            var g = track.Gain.Next(target);
            var l = left[i] * g;
            var r = right[i] * g;
            // The room hears the sound as it is; only the dry sound is placed, so the room stays wide.
            sendLeft[i] += l * layer.ReverbSend;
            sendRight[i] += r * layer.ReverbSend;
            if (width < 1f)
            {
                var mid = (l + r) * 0.5f;
                var side = (l - r) * 0.5f * width;
                l = mid + side;
                r = mid - side;
            }
            var k = (i + 1) * step;
            dryLeft[i] += l * (fromLeft + (toLeft - fromLeft) * k);
            dryRight[i] += r * (fromRight + (toRight - fromRight) * k);
        }
        track.ToLeft = toLeft;
        track.ToRight = toRight;
    }

    private double PositionAt(double elapsed)
    {
        var length = _scene.Kind == TimeKind.Clock ? TimeSpan.FromDays(1).TotalSeconds : _scene.Length.TotalSeconds;
        return _scene.Kind switch
        {
            TimeKind.Loop => elapsed % length / length,
            _ => Math.Min(1, elapsed / length)
        };
    }

    /// <summary>Linear up to <see cref="LimitFrom"/>, then a smooth bend that never passes 1.</summary>
    private static float Limit(float x)
    {
        var a = MathF.Abs(x);
        if (a <= LimitFrom) return x;
        var bent = LimitFrom + (1f - LimitFrom) * MathF.Tanh((a - LimitFrom) / (1f - LimitFrom));
        return MathF.CopySign(bent, x);
    }

    /// <summary>A morning with its sounds built; swapped as one reference. Its fields after the first are the audio thread's.</summary>
    private sealed class WakeState(Wake wake, Track[] tracks, Rng rng)
    {
        public readonly Wake Wake = wake;
        public readonly Track[] Tracks = tracks;
        public Rng Rng = rng;
        private bool _woken;

        /// <summary>
        /// 0 until the rise starts, then up to 1 at the wake time, and 1 from then on. A rise that was missed, because
        /// the scene started after it, stays at 0 until the same time comes round again.
        /// </summary>
        public float Level(TimeSpan timeOfDay)
        {
            const double day = 86400;
            var rise = Math.Max(1, Wake.Rise.TotalSeconds);
            var since = ((timeOfDay.TotalSeconds - (Wake.At.TotalSeconds - rise)) % day + day) % day;
            if (since > rise) return _woken ? 1f : 0f;
            if (since >= rise * 0.999) _woken = true;
            return (float)(since / rise);
        }
    }

    /// <summary>A play with its strikes worked out once; swapped as one reference.</summary>
    private sealed class PlayState(Play play, Strike[] strikes)
    {
        public readonly Play Play = play;
        public readonly Strike[] Strikes = strikes;

        public static PlayState For(Play play, TimeSpan length, ulong seed) => new(play, play.Strikes(length, seed));
    }

    private sealed class Track(Layer layer, Sound sound, RandomWalk drift, Smoother gain, PlayState play, PlaceMover mover)
    {
        /// <summary>A strike swells in over this long, then dies away with <see cref="RingSeconds"/>, in real time.</summary>
        private const float AttackSeconds = 0.15f;
        private const float RingSeconds = 1.6f;

        public readonly Layer Layer = layer;
        public readonly Sound Sound = sound;
        public Curve Curve = layer.Curve;
        public PlayState Play = play;
        public Place Place = layer.Place;
        public float Waver = layer.Waver;
        public PlaceMover Mover = mover;
        public float Pan = layer.Place.Pan;

        /// <summary>The gains reached at the end of the last block; the next block glides on from them.</summary>
        public float ToLeft = 1f, ToRight = 1f;

        public bool Placed;
        public volatile bool StrikeAsked;
        private PlayState? _seen;
        private int _next;
        private float _envelope, _strength;
        private bool _rising;

        /// <summary>Fires every strike the hand passed between two blocks; true if any fired. Audio thread only.</summary>
        public bool Pass(PlayState play, double from, double to, bool jumped, bool wrapped)
        {
            var strikes = play.Strikes;
            if (!ReferenceEquals(play, _seen) || jumped)
            {
                _seen = play;
                _next = FirstAtOrAfter(strikes, to);
                return false;
            }
            var fired = false;
            if (wrapped)
            {
                while (_next < strikes.Length) fired |= Fire(strikes[_next++].Strength);
                _next = 0;
            }
            while (_next < strikes.Length && strikes[_next].At < to) fired |= Fire(strikes[_next++].Strength);
            return fired;
        }

        /// <summary>Fires one full strike if <see cref="StrikeNow"/> asked for one. Audio thread only.</summary>
        public bool StrikeIfAsked()
        {
            if (!StrikeAsked) return false;
            StrikeAsked = false;
            return Fire(1f);
        }

        /// <summary>The strike envelope for this block: up over the attack, then a ringing decay.</summary>
        public float Strike(int frames, int sampleRate)
        {
            if (_rising)
            {
                _envelope += frames / (AttackSeconds * sampleRate);
                if (_envelope >= 1f)
                {
                    _envelope = 1f;
                    _rising = false;
                }
            }
            else
            {
                _envelope *= MathF.Exp(-frames / (RingSeconds * sampleRate));
            }
            return _envelope * _strength;
        }

        private bool Fire(float strength)
        {
            if (Sound.TakesStrikes) Sound.Strike(strength);
            _strength = MathF.Max(strength, _envelope * _strength);
            _rising = true;
            return true;
        }

        private static int FirstAtOrAfter(Strike[] strikes, double position)
        {
            var i = 0;
            while (i < strikes.Length && strikes[i].At < position) i++;
            return i;
        }
        public RandomWalk Drift = drift;
        public Smoother Gain = gain;
        public float Amount;
        public volatile bool Muted;
        public volatile bool Removed;

        /// <summary>Removed and faded out: nothing left to render.</summary>
        public bool Silent => Removed && Gain.Value < 1e-5f;
    }
}
