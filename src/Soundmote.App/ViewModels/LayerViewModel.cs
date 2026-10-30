using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Soundmote.App.Controls;
using Soundmote.App.Formatting;
using Soundmote.Core.Scenes;

namespace Soundmote.App.ViewModels;

/// <summary>One row beside the dial: a ring's name, colour, shape and what it plays at now.</summary>
public sealed partial class LayerViewModel : ObservableObject
{
    private readonly TimeSpan _length;

    public LayerViewModel(Layer layer, int index, TimeSpan length)
    {
        Layer = layer;
        Index = index;
        _length = length;
        Colour = Color.Parse(layer.Colour);
        Swatch = new SolidColorBrush(Colour);
        Tint = new SolidColorBrush(Colour, 0.16);
        (Icon, IconStroke) = SoundIcons.For(layer.Recipe);
        Curve = layer.Curve;
        Play = layer.Play;
        Place = layer.Place;
        Pan = layer.Place.Pan;
        Waver = layer.Waver;
    }

    /// <summary>How much the ring wavers in pitch (S17); only a sound that reads it changes.</summary>
    public float Waver { get; set; }

    public bool Wavers => Layer.Recipe.Wavers;

    public Layer Layer { get; }

    /// <summary>The ring's place on the dial, counted from the outside. Changes when a ring before it is removed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Key))]
    public partial int Index { get; set; }

    /// <summary>A sound being tried from the add panel: drawn dashed, not yet part of the scene (D9).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Description))]
    public partial bool IsTrial { get; set; }

    public string Name => Layer.Name;

    /// <summary>The sound's own colour (D3): the same on the ring, the row and every other view.</summary>
    public Color Colour { get; }

    public IBrush Swatch { get; }

    /// <summary>The sound's glyph (D10).</summary>
    public Geometry Icon { get; }

    public double IconStroke { get; }

    /// <summary>The row's background while its ring is selected.</summary>
    public IBrush Tint { get; }

    /// <summary>The ring's shape now; starts as the scene's and changes when the user reshapes it (D8).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Description), nameof(IsReshaped))]
    public partial Curve Curve { get; set; }

    /// <summary>Shape, on/off, every or random (A11). Starts as the scene's.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Description), nameof(AmountText), nameof(IsStrikes))]
    public partial Play Play { get; set; }

    /// <summary>Where it is heard and how that moves (A13). Starts as the scene's.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WhereText))]
    public partial Place Place { get; set; }

    /// <summary>Where it is heard now, -1 left to 1 right. Set by the player each frame.</summary>
    [ObservableProperty]
    public partial float Pan { get; set; }

    public string WhereText => SceneText.Where(Place);

    /// <summary>Where this ring strikes over one turn; empty unless it plays every or random.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<Strike> Strikes { get; set; } = [];

    /// <summary>"next 15:00" for a ring of strikes. Set by the player each frame.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AmountText))]
    public partial string Next { get; set; } = "";

    public bool IsStrikes => Play is EveryPlay or RandomPlay;

    /// <summary>The last play used in each way, so switching back finds the settings as they were.</summary>
    internal Dictionary<Type, Play> Remembered { get; } = [];

    /// <summary>Seed offset for random strikes; Shuffle moves it on.</summary>
    internal ulong Shuffle { get; set; }

    /// <summary>What the ring does, in words (D6, D11).</summary>
    public string Description => IsTrial ? "Trying it" : SceneText.Describe(Play, Curve, _length);

    public bool IsReshaped => !ReferenceEquals(Curve, Layer.Curve);

    /// <summary>The number key that mutes this ring, as shown on the row.</summary>
    public string Key => Index < 9 ? (Index + 1).ToString() : "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AmountText))]
    public partial float Amount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AmountText), nameof(Background))]
    public partial bool IsMuted { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Background))]
    public partial bool IsSelected { get; set; }

    /// <summary>The amount now; for strikes the next one, and for an on/off ring that is off, when it comes on.</summary>
    public string AmountText => IsMuted ? "muted" : Next.Length > 0 ? Next : $"{Math.Round(Amount * 100):0} %";

    public IBrush Background => IsSelected ? Tint : Brushes.Transparent;
}
