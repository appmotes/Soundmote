using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Soundmote.App.Formatting;
using Soundmote.Core.Scenes;

namespace Soundmote.App.ViewModels;

/// <summary>
/// The top of a ring's panel (D11): which way it plays, and the few settings that way has. Every change becomes a
/// new immutable <see cref="Play"/> handed to the player, which the engine hears at its next block.
/// </summary>
public sealed partial class PlayEditorViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan DefaultStretch = TimeSpan.FromMinutes(5);
    private readonly PlayerViewModel _player;

    internal PlayEditorViewModel(PlayerViewModel player, LayerViewModel layer)
    {
        _player = player;
        Layer = layer;
        Modes = [Choice("mode", "Shape", 0), Choice("mode", "On/off", 1), Choice("mode", "Every", 2), Choice("mode", "Random", 3)];
        Fades = [Choice("fade", "2 s", 2), Choice("fade", "10 s", 10), Choice("fade", "30 s", 30)];
        Intervals = new[] { 1, 2, 5, 10, 15, 30 }.Select(m => Choice("interval", $"{m} min", m)).ToArray();
        Gaps = [Choice("gap", "5 s", 5), Choice("gap", "20 s", 20), Choice("gap", "1 min", 60)];
        Varies = [Choice("vary", "Even", 0), Choice("vary", "± 30 %", 0.3), Choice("vary", "± 60 %", 0.6)];
        Busies = ShapePreset.All.Select((p, i) => Choice("busy", p.Name, i)).ToArray();
        layer.PropertyChanged += OnLayerChanged;
        Refresh();
    }

    public LayerViewModel Layer { get; }

    public IReadOnlyList<ChoiceItem> Modes { get; }
    public IReadOnlyList<ChoiceItem> Fades { get; }
    public IReadOnlyList<ChoiceItem> Intervals { get; }
    public IReadOnlyList<ChoiceItem> Gaps { get; }
    public IReadOnlyList<ChoiceItem> Varies { get; }
    public IReadOnlyList<ChoiceItem> Busies { get; }

    public bool IsShape => Layer.Play is ShapePlay;
    public bool IsOnOff => Layer.Play is OnOffPlay;
    public bool IsEvery => Layer.Play is EveryPlay;
    public bool IsRandom => Layer.Play is RandomPlay;

    /// <summary>The on/off stretches as "10:00 – 18:00".</summary>
    [ObservableProperty]
    public partial IReadOnlyList<StretchItem> Stretches { get; private set; } = [];

    [ObservableProperty]
    public partial string FirstText { get; private set; } = "";

    [ObservableProperty]
    public partial string RateText { get; private set; } = "";

    /// <summary>The on/off level, or the strength of strikes, 0 to 1.</summary>
    [ObservableProperty]
    public partial double Level { get; set; }

    private bool _refreshing;

    partial void OnLevelChanged(double value)
    {
        if (_refreshing) return;
        var level = (float)Math.Clamp(value, 0.05, 1);
        Apply(Layer.Play switch
        {
            OnOffPlay o => o with { Level = level },
            EveryPlay e => e with { Strength = level },
            RandomPlay r => r with { Strength = level },
            var p => p
        });
    }

    [RelayCommand]
    private void Choose(ChoiceItem item)
    {
        var play = Layer.Play;
        Apply(item.Group switch
        {
            "mode" => ModePlay((int)item.Value),
            "fade" when play is OnOffPlay o => o with { FadeSeconds = item.Value },
            "interval" when play is EveryPlay e => e with { Interval = TimeSpan.FromMinutes(item.Value) },
            "gap" when play is RandomPlay r => r with { MinGap = TimeSpan.FromSeconds(item.Value) },
            "vary" when play is RandomPlay r => r with { Vary = (float)item.Value },
            "busy" when play is RandomPlay r => r with { Busy = ShapePreset.All[(int)item.Value].Shape(1f) },
            _ => play
        });
        Refresh();
    }

    /// <summary>A new stretch after the last one, or at the hand when the turn is empty.</summary>
    [RelayCommand]
    private void AddStretch()
    {
        if (Layer.Play is not OnOffPlay o) return;
        var length = _player.Length;
        var size = Math.Min(DefaultStretch / length, 0.5);
        var from = o.Stretches.Count == 0 ? _player.Position : o.Stretches.Max(s => s.To) + TimeSpan.FromMinutes(2) / length;
        if (from + size > 1) from = Math.Max(0, 1 - size);
        if (o.Stretches.Any(s => from < s.To && from + size > s.From)) return;
        Apply(o with { Stretches = o.Stretches.Append(new Stretch(from, from + size)).OrderBy(s => s.From).ToArray() });
    }

    [RelayCommand]
    private void RemoveStretch(StretchItem item)
    {
        if (Layer.Play is OnOffPlay o) Apply(o with { Stretches = o.Stretches.Where((_, i) => i != item.Index).ToArray() });
    }

    [RelayCommand]
    private void EarlierFirst() => MoveFirst(-1);

    [RelayCommand]
    private void LaterFirst() => MoveFirst(1);

    [RelayCommand]
    private void FewerStrikes() => ChangeRate(-1);

    [RelayCommand]
    private void MoreStrikes() => ChangeRate(1);

    private void MoveFirst(int minutes)
    {
        if (Layer.Play is not EveryPlay e) return;
        var first = e.First + TimeSpan.FromMinutes(minutes);
        if (first < TimeSpan.Zero || first >= _player.Length) return;
        Apply(e with { First = first });
    }

    private void ChangeRate(int step)
    {
        if (Layer.Play is RandomPlay r) Apply(r with { PerTenMinutes = Math.Clamp(r.PerTenMinutes + step, 1, 30) });
    }

    /// <summary>The same random settings, new places for the strikes.</summary>
    [RelayCommand]
    private void Shuffle()
    {
        Layer.Shuffle++;
        _player.ApplyPlay(Layer, Layer.Play);
    }

    private Play ModePlay(int mode)
    {
        var length = _player.Length;
        Type type = mode switch { 1 => typeof(OnOffPlay), 2 => typeof(EveryPlay), 3 => typeof(RandomPlay), _ => typeof(ShapePlay) };
        if (Layer.Play.GetType() == type) return Layer.Play;
        Layer.Remembered[Layer.Play.GetType()] = Layer.Play;
        if (Layer.Remembered.TryGetValue(type, out var remembered)) return remembered;
        return mode switch
        {
            1 => new OnOffPlay([new Stretch(1 / 3.0, 2 / 3.0)]),
            2 => new EveryPlay(length >= TimeSpan.FromMinutes(20) ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(1), TimeSpan.Zero),
            3 => new RandomPlay(6, TimeSpan.FromSeconds(20), 0.3f, Curve.Steady(1f)),
            _ => Play.Shape
        };
    }

    private void Apply(Play play)
    {
        if (!ReferenceEquals(play, Layer.Play)) _player.ApplyPlay(Layer, play);
    }

    private void OnLayerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LayerViewModel.Play)) Refresh();
    }

    private void Refresh()
    {
        var play = Layer.Play;
        var length = _player.Length;
        _refreshing = true;
        Mark(Modes, play switch { OnOffPlay => 1, EveryPlay => 2, RandomPlay => 3, _ => 0 });
        switch (play)
        {
            case OnOffPlay o:
                Stretches = o.Stretches.Select((s, i) => new StretchItem(i, $"{SceneText.Clock(length * s.From)} – {SceneText.Clock(length * s.To)}")).ToArray();
                Mark(Fades, o.FadeSeconds);
                Level = o.Level;
                break;
            case EveryPlay e:
                Mark(Intervals, e.Interval.TotalMinutes);
                FirstText = SceneText.Clock(e.First);
                Level = e.Strength;
                break;
            case RandomPlay r:
                RateText = $"{r.PerTenMinutes:0} per 10 min";
                Mark(Gaps, r.MinGap.TotalSeconds);
                Mark(Varies, r.Vary);
                Mark(Busies, ShapePreset.All.Select((p, i) => (p, i)).FirstOrDefault(x => Same(x.p.Shape(1f), r.Busy)).i);
                Level = r.Strength;
                break;
        }
        _refreshing = false;
        OnPropertyChanged(nameof(IsShape));
        OnPropertyChanged(nameof(IsOnOff));
        OnPropertyChanged(nameof(IsEvery));
        OnPropertyChanged(nameof(IsRandom));
    }

    private static void Mark(IEnumerable<ChoiceItem> items, double value)
    {
        foreach (var item in items) item.IsChecked = Math.Abs(item.Value - value) < 1e-6;
    }

    private static bool Same(Curve a, Curve b) =>
        a.Points.Count == b.Points.Count && a.Points.Zip(b.Points).All(p => Math.Abs(p.First.Value - p.Second.Value) < 1e-4);

    private static ChoiceItem Choice(string group, string label, double value) => new(group, label, value);

    public void Dispose() => Layer.PropertyChanged -= OnLayerChanged;
}

/// <summary>One chip in a set: a way to play, a fade, an interval. One per group is checked.</summary>
public sealed partial class ChoiceItem(string group, string label, double value) : ObservableObject
{
    public string Group { get; } = group;

    public string Label { get; } = label;

    public double Value { get; } = value;

    [ObservableProperty]
    public partial bool IsChecked { get; set; }
}

public sealed record StretchItem(int Index, string Text);
