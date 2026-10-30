using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Soundmote.Core.Scenes;

namespace Soundmote.App.ViewModels;

/// <summary>
/// The bottom of a ring's panel (D15): where it is heard between left and right, how wide, and how that moves. Every
/// change becomes a new immutable <see cref="Place"/> handed to the player, which the engine hears at its next block.
/// </summary>
public sealed partial class PlaceEditorViewModel : ObservableObject
{
    private readonly PlayerViewModel _player;
    private bool _refreshing;

    internal PlaceEditorViewModel(PlayerViewModel player, LayerViewModel layer)
    {
        _player = player;
        Layer = layer;
        Moves = [Choice("move", "Stays", 0), Choice("move", "Wanders", 1), Choice("move", "Crosses", 2), Choice("move", "Flits", 3)];
        Widths = [Choice("width", "Point", 0), Choice("width", "Narrow", 0.4), Choice("width", "Wide", 1)];
        Ranges = [Choice("range", "A little", 0.25), Choice("range", "Half", 0.5), Choice("range", "Side to side", 1)];
        Swings = [Choice("swing", "60 s", 60), Choice("swing", "20 s", 20), Choice("swing", "5 s", 5)];
        Glides = [Choice("glide", "Hops", 0.3), Choice("glide", "Glides", 1.5), Choice("glide", "Flies over", 4)];
        Ways = [Choice("way", "Left to right", 0), Choice("way", "Right to left", 1), Choice("way", "There and back", 2)];
        Refresh();
    }

    public LayerViewModel Layer { get; }

    public IReadOnlyList<ChoiceItem> Moves { get; }
    public IReadOnlyList<ChoiceItem> Widths { get; }
    public IReadOnlyList<ChoiceItem> Ranges { get; }
    public IReadOnlyList<ChoiceItem> Swings { get; }
    public IReadOnlyList<ChoiceItem> Glides { get; }
    public IReadOnlyList<ChoiceItem> Ways { get; }

    public bool IsWander => Layer.Place.Move == Move.Wander;
    public bool IsCross => Layer.Place.Move == Move.Cross;
    public bool IsFlit => Layer.Place.Move == Move.Flit;
    public bool HasRange => Layer.Place.Move != Move.Still;

    /// <summary>A flitting ring of strikes moves at each strike; one that plays a shape every so often.</summary>
    public string FlitText => Layer.IsStrikes ? "Lands somewhere new at each strike." : "Lands somewhere new every 6 to 24 seconds.";

    /// <summary>-1 left to 1 right; the middle of the range for a ring that moves.</summary>
    [ObservableProperty]
    public partial double Pan { get; set; }

    partial void OnPanChanged(double value)
    {
        if (_refreshing) return;
        // Close to the middle snaps to it, so the middle is easy to find again with a slider.
        var pan = Math.Abs(value) < 0.04 ? 0f : (float)Math.Clamp(value, -1, 1);
        Apply(Retrace(Layer.Place with { Pan = pan }));
    }

    [RelayCommand]
    private void Choose(ChoiceItem item)
    {
        var place = Layer.Place;
        Apply(item.Group switch
        {
            "move" => Retrace(place with { Move = (Move)(int)item.Value, Seconds = (Move)(int)item.Value == Move.Flit ? 1.5 : 20 }),
            "width" => place with { Width = (float)item.Value },
            "range" => Retrace(place with { Range = (float)item.Value }),
            "swing" or "glide" => place with { Seconds = item.Value },
            "way" => Trace(place, (int)item.Value),
            _ => place
        });
    }

    private void Apply(Place place)
    {
        _player.ApplyPlace(Layer, place);
        Refresh();
    }

    /// <summary>A crossing ring's path, kept the same way round when its pan or range changes.</summary>
    private static Place Retrace(Place place) => place.Move == Move.Cross ? Trace(place, Way(place)) : place;

    /// <summary>The path from one end of the range to the other: 0 left to right, 1 right to left, 2 there and back.</summary>
    private static Place Trace(Place place, int way)
    {
        var from = (Math.Clamp(place.Pan - place.Range, -1f, 1f) + 1f) / 2f;
        var to = (Math.Clamp(place.Pan + place.Range, -1f, 1f) + 1f) / 2f;
        return place with
        {
            Path = way switch { 1 => Curve.Even(to, from), 2 => Curve.Even(from, to, from), _ => Curve.Even(from, to) }
        };
    }

    private static int Way(Place place) =>
        place.Path.Points.Count > 2 ? 2 : place.Path.At(0) > place.Path.At(1) ? 1 : 0;

    private void Refresh()
    {
        var place = Layer.Place;
        _refreshing = true;
        Pan = place.Pan;
        _refreshing = false;
        Mark(Moves, (int)place.Move);
        Mark(Widths, place.Width);
        Mark(Ranges, place.Range);
        Mark(Swings, place.Seconds);
        Mark(Glides, place.Seconds);
        Mark(Ways, Way(place));
        OnPropertyChanged(nameof(IsWander));
        OnPropertyChanged(nameof(IsCross));
        OnPropertyChanged(nameof(IsFlit));
        OnPropertyChanged(nameof(HasRange));
    }

    private static void Mark(IEnumerable<ChoiceItem> items, double value)
    {
        foreach (var item in items) item.IsChecked = Math.Abs(item.Value - value) < 1e-4;
    }

    private static ChoiceItem Choice(string group, string label, double value) => new(group, label, value);
}
