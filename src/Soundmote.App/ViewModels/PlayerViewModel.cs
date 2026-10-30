using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Soundmote.App.Controls;
using Soundmote.App.Formatting;
using Soundmote.Core.Output;
using Soundmote.Core.Recipes;
using Soundmote.Core.Scenes;

namespace Soundmote.App.ViewModels;

/// <summary>
/// One scene on the dial (A9): owns its renderer and the output that plays it. The engine runs on the audio
/// thread; <see cref="Update"/> copies what it is doing into these properties, called by a UI timer.
/// </summary>
public sealed partial class PlayerViewModel : ObservableObject, IDisposable
{
    private readonly SceneRenderer _renderer;
    private readonly IAudioOutput? _output;
    private readonly TimeProvider _clock;
    private LayerViewModel? _trial;

    public PlayerViewModel(Scene scene, IAudioOutput? output, TimeProvider? clock = null)
    {
        Scene = scene;
        Name = scene.Name;
        Wake = scene.Kind == TimeKind.Journey ? scene.Wake : null;
        _output = output;
        _clock = clock ?? TimeProvider.System;
        _renderer = new SceneRenderer(scene, time: _clock) { Paused = true };
        _showingWaver = true;
        Tempo = scene.Tempo;
        _showingWaver = false;

        Summary = SceneText.Summary(scene);
        Layers = new ObservableCollection<LayerViewModel>(scene.Layers.Select((layer, i) => new LayerViewModel(layer, i, scene.Length)));
        Phases = scene.Phases.Select(p => new PhaseViewModel(p, scene.Length)).ToArray();
        Speeds = [new SpeedViewModel(1) { IsChecked = true }, new SpeedViewModel(10), new SpeedViewModel(60)];
        Presets = ShapePreset.All;
        PhaseMarks = scene.Phases.Select(p => p.To).Where(to => to < 1).ToArray();
        foreach (var layer in Layers) layer.Strikes = _renderer.StrikesOf(layer.Index);
        Rings = BuildRings();

        if (_output is not null)
        {
            _output.Volume = (float)Volume;
            _output.Start(_renderer);
        }
        Update();
    }

    /// <summary>The scene as it was opened. <see cref="CurrentLayers"/> has the rings as they are now.</summary>
    public Scene Scene { get; }

    /// <summary>Shaping, adding, removing or changing how a ring plays; the shell keeps the scene (A12).</summary>
    public event Action? Edited;

    [ObservableProperty]
    public partial string Name { get; set; }

    public string Summary { get; }

    public TimeSpan Length => Scene.Length;

    /// <summary>The rings, outermost first; a sound on trial is the last one.</summary>
    public ObservableCollection<LayerViewModel> Layers { get; }

    public IReadOnlyList<PhaseViewModel> Phases { get; }

    public bool HasPhases => Phases.Count > 0;

    public IReadOnlyList<SpeedViewModel> Speeds { get; }

    public IReadOnlyList<ShapePreset> Presets { get; }

    /// <summary>Where one phase ends and the next begins, for the flat curve.</summary>
    public IReadOnlyList<double> PhaseMarks { get; }

    /// <summary>The ring being looked at or shaped, or null.</summary>
    public LayerViewModel? SelectedLayer => SelectedIndex >= 0 && SelectedIndex < Layers.Count ? Layers[SelectedIndex] : null;

    public bool HasSelection => SelectedLayer is not null;

    /// <summary>False where the OS has no live output yet (N1); the dial still shows the scene.</summary>
    public bool CanPlay => _output is not null;

    /// <summary>The renderer this view plays, for tests and tools that pull blocks themselves.</summary>
    internal SceneRenderer Renderer => _renderer;

    [ObservableProperty]
    public partial IReadOnlyList<DialRing> Rings { get; private set; }

    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    /// <summary>The add panel while it is open (D9), else null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAdding))]
    public partial AddSoundViewModel? AddSound { get; private set; }

    public bool IsAdding => AddSound is not null;

    /// <summary>"Trying Distant sea" under the clock while a sound is on trial.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTrying))]
    public partial string TrialText { get; private set; } = "";

    public bool IsTrying => TrialText.Length > 0;

    [ObservableProperty]
    public partial double Position { get; private set; }

    [ObservableProperty]
    public partial string Clock { get; private set; } = "";

    [ObservableProperty]
    public partial string ClockCaption { get; private set; } = "";

    [ObservableProperty]
    public partial string Status { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedLayer), nameof(HasSelection))]
    public partial int SelectedIndex { get; set; } = -1;

    /// <summary>The way the selected ring plays and its settings (D11), or null with nothing selected.</summary>
    [ObservableProperty]
    public partial PlayEditorViewModel? PlayEditor { get; private set; }

    /// <summary>Where the selected ring is heard (D15), or null with nothing selected.</summary>
    [ObservableProperty]
    public partial PlaceEditorViewModel? PlaceEditor { get; private set; }

    [ObservableProperty]
    public partial double Volume { get; set; } = 0.8;

    /// <summary>The journey's morning (A14, D17), or null when it ends in silence.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWake), nameof(WakeText), nameof(EndText), nameof(WakeNote), nameof(WakeToggleText))]
    public partial Wake? Wake { get; private set; }

    /// <summary>Only a journey ends, so only a journey can end by waking.</summary>
    public bool CanWake => Scene.Kind == TimeKind.Journey;

    public bool HasWake => Wake is not null;

    /// <summary>"07:00".</summary>
    public string WakeText => Wake is { } w ? SceneText.Clock24(w.At) : "";

    public string EndText => HasWake ? "Then wake at" : "Then silence";

    public string WakeNote => Wake is { } w ? SceneText.Morning(w) : "The sound fades out with the journey.";

    public string WakeToggleText => HasWake ? "End in silence" : "Wake me instead";

    /// <summary>The morning has begun to rise. Set by <see cref="Update"/>.</summary>
    [ObservableProperty]
    public partial bool IsWaking { get; private set; }

    private static readonly TimeSpan DefaultWake = TimeSpan.FromHours(7);
    private TimeSpan _lastWake = DefaultWake;

    [RelayCommand]
    private void ToggleWake()
    {
        if (!CanWake) return;
        if (Wake is { } w) _lastWake = w.At;
        SetWake(Wake is null ? Core.Scenes.Wake.Morning(_lastWake) : null);
    }

    [RelayCommand]
    private void EarlierWake() => MoveWake(-15);

    [RelayCommand]
    private void LaterWake() => MoveWake(15);

    private void MoveWake(int minutes)
    {
        if (Wake is not { } w) return;
        var day = TimeSpan.FromDays(1);
        var at = TimeSpan.FromTicks(((w.At + TimeSpan.FromMinutes(minutes)).Ticks % day.Ticks + day.Ticks) % day.Ticks);
        SetWake(w with { At = at });
    }

    private void SetWake(Wake? wake)
    {
        _renderer.SetWake(wake);
        Wake = wake;
        Edited?.Invoke();
    }

    /// <summary>When playing stops by itself (D16). Set again whenever chosen, from that moment.</summary>
    [ObservableProperty]
    public partial StopAfter StopAfter { get; set; }

    private DateTimeOffset _stopAt;
    private int _stopLap;

    partial void OnStopAfterChanged(StopAfter value)
    {
        _stopAt = _clock.GetUtcNow() + value switch
        {
            StopAfter.HalfHour => TimeSpan.FromMinutes(30),
            StopAfter.Hour => TimeSpan.FromHours(1),
            _ => TimeSpan.Zero
        };
        _stopLap = _renderer.Lap;
    }

    /// <summary>The phase after the hand's: in a loop the first again after the last; none on the clock or at a journey's last.</summary>
    public Phase? NextPhase
    {
        get
        {
            if (Scene.Kind == TimeKind.Clock || Scene.Phases.Count < 2) return null;
            var position = Position;
            var next = Scene.Phases.FirstOrDefault(p => p.From > position + 1e-6);
            return next ?? (Scene.Kind == TimeKind.Loop ? Scene.Phases[0] : null);
        }
    }

    /// <summary>Moves the hand to the start of the next phase: "Skip to Break" (D16).</summary>
    [RelayCommand]
    private void SkipToNextPhase()
    {
        if (NextPhase is not { } next) return;
        var lap = _renderer.Lap - 1 + (next.From <= Position ? 1 : 0);
        _renderer.Seek(Scene.Length * (lap + next.From));
        Position = next.From;
    }

    partial void OnIsPlayingChanged(bool value)
    {
        // A long fade is only for stopping by itself; play and the space bar keep the short one.
        if (value) _renderer.FadeOutSeconds = SceneRenderer.DefaultFade;
        _renderer.Paused = !value;
    }

    /// <summary>How long a scene fades out when Stop after ends it (D18).</summary>
    public double StopFadeSeconds { get; set; } = 10;

    /// <summary>The hand has been moved by hand, by dragging, a key or a skip; the first-run hint listens for it (D19).</summary>
    public bool HasSought { get; private set; }

    partial void OnVolumeChanged(double value)
    {
        if (_output is not null) _output.Volume = (float)Math.Clamp(value, 0, 1);
    }

    partial void OnSelectedIndexChanged(int value)
    {
        foreach (var layer in Layers) layer.IsSelected = layer.Index == value;
        PlayEditor?.Dispose();
        PlayEditor = SelectedLayer is { } selected ? new PlayEditorViewModel(this, selected) : null;
        PlaceEditor = SelectedLayer is { } placed ? new PlaceEditorViewModel(this, placed) : null;
        _showingWaver = true;
        Waver = SelectedLayer?.Waver ?? 0;
        _showingWaver = false;
        OnPropertyChanged(nameof(ShowsWaver));
        OnPropertyChanged(nameof(ShowsTempo));
    }

    /// <summary>The selected ring keeps the scene's beat, as a rhythm does, so it shows the scene's tempo (D23).</summary>
    public bool ShowsTempo => SelectedLayer?.Layer.Recipe.KeepsBeat == true;

    /// <summary>Beats a minute for every rhythm in the scene (A16): one tempo, so they keep time together.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TempoText))]
    public partial double Tempo { get; set; }

    public string TempoText => $"{Math.Round(Tempo)} beats a minute";

    partial void OnTempoChanged(double value)
    {
        if (_showingWaver) return;
        _renderer.Tempo = Math.Round(value);
        Edited?.Invoke();
    }

    private bool _showingWaver;

    /// <summary>The selected ring's sound reads the Waver knob, as a pad does (D21).</summary>
    public bool ShowsWaver => SelectedLayer?.Wavers == true;

    /// <summary>The selected ring's Waver knob, 0–1 (S17).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WaverText))]
    public partial double Waver { get; set; }

    /// <summary>The swing at most, in cents: the pads' tune reaches ±50 at full.</summary>
    public string WaverText => Waver < 0.01 ? "Still" : $"±{Math.Round(Waver * 50)} cents";

    partial void OnWaverChanged(double value)
    {
        if (_showingWaver || SelectedLayer is not { } layer) return;
        layer.Waver = (float)Math.Clamp(value, 0, 1);
        _renderer.SetWaver(layer.Index, layer.Waver);
        if (!layer.IsTrial) Edited?.Invoke();
    }

    /// <summary>A ring's on/off stretches changed on the dial (D11).</summary>
    [RelayCommand]
    private void ChangePlay(RingPlay change) => ApplyPlay(Layers[change.Index], change.Play);

    /// <summary>Gives a ring a new way to play; the engine works out its strikes and hears it at the next block (A11).</summary>
    internal void ApplyPlay(LayerViewModel layer, Play play)
    {
        _renderer.SetPlay(layer.Index, play, SceneRenderer.StrikeSeed(Scene.Seed, layer.Index) + layer.Shuffle);
        layer.Strikes = _renderer.StrikesOf(layer.Index);
        layer.Play = play;
        Rings = BuildRings();
        Edited?.Invoke();
    }

    /// <summary>Moves a ring between left and right; heard from the next block (A13).</summary>
    internal void ApplyPlace(LayerViewModel layer, Place place)
    {
        if (ReferenceEquals(place, layer.Place)) return;
        _renderer.SetPlace(layer.Index, place);
        layer.Place = place;
        if (!layer.IsTrial) Edited?.Invoke();
    }

    [RelayCommand]
    private void TogglePlay()
    {
        if (!IsPlaying && _renderer.Finished) _renderer.Seek(TimeSpan.Zero);
        IsPlaying = !IsPlaying;
    }

    /// <summary>Moves the hand to a place on the dial, 0 to 1. A loop stays in its lap.</summary>
    [RelayCommand]
    private void Seek(double position)
    {
        HasSought = true;
        position = Math.Clamp(position, 0, 1);
        var lapStart = Scene.Kind == TimeKind.Loop ? Scene.Length * (_renderer.Lap - 1) : TimeSpan.Zero;
        _renderer.Seek(lapStart + Scene.Length * position);
        Position = position;
    }

    [RelayCommand]
    private void Skip(int minutes)
    {
        HasSought = true;
        var to = _renderer.Elapsed + TimeSpan.FromMinutes(minutes);
        _renderer.Seek(to < TimeSpan.Zero ? TimeSpan.Zero : to);
    }

    [RelayCommand]
    private void ToggleMute(LayerViewModel layer)
    {
        layer.IsMuted = !layer.IsMuted;
        _renderer.SetMuted(layer.Index, layer.IsMuted);
        Rings = BuildRings();
    }

    [RelayCommand]
    private void Select(LayerViewModel layer) => SelectedIndex = SelectedIndex == layer.Index ? -1 : layer.Index;

    /// <summary>A ring reshaped on the dial: heard from the next block, drawn at once (D8).</summary>
    [RelayCommand]
    private void Reshape(RingShape shape) => SetCurve(Layers[shape.Index], shape.Curve);

    /// <summary>The selected ring reshaped in the flat view.</summary>
    [RelayCommand]
    private void ReshapeSelected(Curve curve)
    {
        if (SelectedLayer is { } layer) SetCurve(layer, curve);
    }

    [RelayCommand]
    private void ApplyPreset(ShapePreset preset)
    {
        if (SelectedLayer is { } layer) SetCurve(layer, preset.Shape(layer.Curve));
    }

    /// <summary>Back to the scene's own shape for the selected ring.</summary>
    [RelayCommand]
    private void ResetShape()
    {
        if (SelectedLayer is { } layer) SetCurve(layer, layer.Layer.Curve);
    }

    [RelayCommand]
    private void OpenAdd()
    {
        SelectedIndex = -1;
        AddSound ??= new AddSoundViewModel(this);
    }

    /// <summary>Takes the selected ring out of the scene; its sound fades out (A10).</summary>
    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedLayer is not { } layer) return;
        SelectedIndex = -1;
        Remove(layer);
        Edited?.Invoke();
    }

    /// <summary>Plays a sound as a trial ring at the end of the dial, replacing the one tried before.</summary>
    internal void Try(Recipe recipe, Curve curve)
    {
        if (_trial is not null) Remove(_trial);
        // A sound that plays something whole at each strike joins as strikes (S12): a bowl every few minutes, thunder at random.
        var layer = new Layer(recipe.Name, recipe, curve)
        {
            Play = !recipe.TakesStrikes ? Play.Shape
                : recipe.StrikesAtRandom ? new RandomPlay(4, TimeSpan.FromSeconds(45), 0.4f, Curve.Steady(1f))
                : new EveryPlay(Scene.Length >= TimeSpan.FromMinutes(20) ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(1), TimeSpan.Zero)
        };
        var index = _renderer.AddLayer(layer);
        // A struck sound is heard at once on trial, not only at its first strike minutes away.
        if (recipe.TakesStrikes) _renderer.StrikeNow(index);
        _trial = new LayerViewModel(layer, index, Scene.Length) { IsTrial = true };
        Layers.Add(_trial);
        TrialText = $"Trying {recipe.Name}";
        Rings = BuildRings();
        if (!IsPlaying && CanPlay && !_renderer.Finished) IsPlaying = true;
    }

    internal void ReshapeTrial(Curve curve)
    {
        if (_trial is not null) SetCurve(_trial, curve);
    }

    /// <summary>The trial ring joins the scene and is selected, ready to shape.</summary>
    internal void KeepTrial()
    {
        if (_trial is not { } kept) return;
        kept.IsTrial = false;
        _trial = null;
        TrialText = "";
        AddSound = null;
        Rings = BuildRings();
        SelectedIndex = kept.Index;
        Edited?.Invoke();
    }

    /// <summary>Closes the add panel; a sound still on trial is dropped.</summary>
    internal void CloseAdd()
    {
        if (_trial is not null) Remove(_trial);
        _trial = null;
        TrialText = "";
        AddSound = null;
    }

    private void Remove(LayerViewModel layer)
    {
        _renderer.RemoveLayer(layer.Index);
        Layers.Remove(layer);
        for (var i = 0; i < Layers.Count; i++) Layers[i].Index = i;
        Rings = BuildRings();
    }

    private void SetCurve(LayerViewModel layer, Curve curve)
    {
        if (ReferenceEquals(layer.Curve, curve)) return;
        layer.Curve = curve;
        _renderer.SetCurve(layer.Index, curve);
        Rings = BuildRings();
        if (!layer.IsTrial) Edited?.Invoke();
    }

    /// <summary>The scene's rings as shaped now, without a sound on trial.</summary>
    public IReadOnlyList<Layer> CurrentLayers() =>
        Layers.Where(l => !l.IsTrial).Select(l => l.Layer with { Curve = l.Curve, Play = l.Play, Place = l.Place, Waver = l.Waver }).ToArray();

    [RelayCommand]
    private void SetSpeed(SpeedViewModel speed)
    {
        foreach (var s in Speeds) s.IsChecked = s == speed;
        _renderer.Speed = speed.Speed;
    }

    /// <summary>Reads the engine's state into the view. Cheap; meant for a timer at frame rate.</summary>
    public void Update()
    {
        if (IsPlaying && _renderer.Finished) IsPlaying = false;
        if (IsPlaying && StopAfter switch
            {
                StopAfter.ThisLap => _renderer.Lap > _stopLap,
                StopAfter.HalfHour or StopAfter.Hour => _clock.GetUtcNow() >= _stopAt,
                _ => false
            })
        {
            _renderer.FadeOutSeconds = StopFadeSeconds;
            IsPlaying = false;
            if (StopAfter == StopAfter.ThisLap) Seek(0);
            StopAfter = StopAfter.Never;
        }

        Position = _renderer.Position;
        IsWaking = _renderer.WakeAmount > 0;
        foreach (var layer in Layers)
        {
            layer.Amount = _renderer.AmountOf(layer.Index);
            layer.Pan = _renderer.PanOf(layer.Index);
            layer.Next = layer.Play switch
            {
                EveryPlay or RandomPlay => NextStrike(layer),
                OnOffPlay onOff when layer.Amount < 0.01f => NextOn(onOff),
                _ => ""
            };
        }
        foreach (var phase in Phases) phase.IsCurrent = phase.Contains(Position);

        var elapsed = _renderer.Elapsed;
        switch (Scene.Kind)
        {
            case TimeKind.Journey when Wake is { } wake && elapsed >= Scene.Length:
                Clock = SceneText.TimeOfDay(_clock.GetLocalNow());
                ClockCaption = IsWaking ? "good morning" : $"waking at {SceneText.Clock24(wake.At)}";
                Status = $"{SoundsText} · {ClockCaption}";
                break;
            case TimeKind.Journey:
                var left = Scene.Length - elapsed;
                Clock = SceneText.Clock(left);
                ClockCaption = _renderer.Finished ? "ended"
                    : IsPlaying ? $"left · ends {SceneText.TimeOfDay(_clock.GetLocalNow() + left / Math.Max(_renderer.Speed, 1e-6))}"
                    : "left";
                Status = _renderer.Finished ? $"{SoundsText} · ended" : $"{SoundsText} · {Clock} left";
                break;
            case TimeKind.Loop:
                var lapLeft = Scene.Length * (1 - Position);
                Clock = SceneText.Clock(lapLeft);
                ClockCaption = $"left of lap {_renderer.Lap}";
                Status = $"{SoundsText} · lap {_renderer.Lap}";
                break;
            default:
                Clock = SceneText.TimeOfDay(_clock.GetLocalNow());
                ClockCaption = "";
                Status = $"{SoundsText} · follows the clock";
                break;
        }
    }

    /// <summary>"next 15:00", or "none left" when the hand has passed the last strike.</summary>
    private string NextStrike(LayerViewModel layer)
    {
        foreach (var strike in layer.Strikes)
            if (strike.At > Position) return $"next {SceneText.Clock(Scene.Length * strike.At)}";
        return "none left";
    }

    /// <summary>"on at 15:00" while an on/off ring waits for its next stretch, "done" after its last.</summary>
    private string NextOn(OnOffPlay play)
    {
        foreach (var s in play.Stretches.OrderBy(s => s.From))
            if (s.From > Position) return $"on at {SceneText.Clock(Scene.Length * s.From)}";
        return play.Stretches.Any(s => s.To > Position) ? "" : "done";
    }

    /// <summary>"3 sounds", or "3 sounds + 1 on trial" while one is being tried.</summary>
    private string SoundsText
    {
        get
        {
            var count = Layers.Count(l => !l.IsTrial);
            var text = count == 1 ? "1 sound" : $"{count} sounds";
            return _trial is null ? text : text + " + 1 on trial";
        }
    }

    private DialRing[] BuildRings() => Layers.Select(l => new DialRing(l.Curve, l.Colour, l.IsMuted, l.IsTrial)
    {
        Icon = l.Icon,
        IconStroke = l.IconStroke,
        Play = l.Play,
        Strikes = l.Strikes
    }).ToArray();

    public void Dispose()
    {
        _output?.Stop();
        _output?.Dispose();
    }
}

/// <summary>When a scene stops playing by itself (D16).</summary>
public enum StopAfter
{
    Never,
    ThisLap,
    HalfHour,
    Hour
}
