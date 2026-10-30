using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Soundmote.App.Assets;
using Soundmote.App.Controls;
using Soundmote.App.Formatting;
using Soundmote.Core.Scenes;

namespace Soundmote.App.ViewModels;

/// <summary>One scene in the library (D13): a small dial on the scene's own ground, its name and kind of time.</summary>
public sealed partial class SceneCardViewModel : ObservableObject
{
    /// <param name="original">The built-in scene this card shows, changed or not; null for the user's own.</param>
    public SceneCardViewModel(Scene scene, Scene? original = null)
    {
        Scene = scene;
        Original = original;
    }

    /// <summary>The scene as it is now: renamed, regrounded and shaped. The library saves it as it changes (A12).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name), nameof(IntentText), nameof(KindText), nameof(SoundsText), nameof(KindIcon), nameof(Ground), nameof(Rings))]
    public partial Scene Scene { get; set; }

    /// <summary>Soundmote's version, which Reset brings back; null for the user's own scenes.</summary>
    public Scene? Original { get; }

    public bool IsBuiltIn => Original is not null;

    public string Name => Scene.Name;

    /// <summary>"FOCUS" above the dial; capitals because the text box has no case transform.</summary>
    public string IntentText => Scene.Intent.ToString().ToUpperInvariant();

    public string KindText => SceneText.Kind(Scene);

    /// <summary>"Rain, wind, choir": the scene's sounds, for the first-run cards (D19).</summary>
    public string SoundsText => string.Join(", ", Scene.Layers.Select((l, i) => i == 0 ? l.Name : l.Name.ToLowerInvariant()));

    /// <summary>A stroked glyph on the 24 grid for the kind of time: an arrow to a line, a loop, a clock face.</summary>
    public Geometry KindIcon => Geometry.Parse(KindPath(Scene.Kind));

    public static string KindPath(TimeKind kind) => kind switch
    {
        TimeKind.Journey => "M4 12 H17 M12 6 L18 12 L12 18 M21 5 V19",
        TimeKind.Loop => "M21 12 A9 9 0 1 1 18 5.3 L21 8 M21 3 V8 H16",
        _ => "M3 12 A9 9 0 1 0 21 12 A9 9 0 1 0 3 12 M12 7 V12 L15 14"
    };

    public ThemeVariant Ground => Grounds.For(Scene.Ground);

    public IReadOnlyList<DialRing> Rings => Scene.Layers.Select((_, i) => DialRing.Of(Scene, i)).ToArray();

    /// <summary>This scene is open in the player and playing.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPill))]
    public partial bool IsPlaying { get; set; }

    [ObservableProperty]
    public partial double Position { get; set; }

    /// <summary>A built-in scene the user has changed; its menu offers Reset to original.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Pill), nameof(ShowPill), nameof(CanDelete))]
    public partial bool IsChanged { get; set; }

    /// <summary>Made by Duplicate or New scene in this session.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Pill), nameof(ShowPill))]
    public partial bool IsNew { get; set; }

    public string Pill => IsNew ? "Just made" : IsChanged ? "Changed" : "";

    /// <summary>The pill gives way to PLAYING in the same corner.</summary>
    public bool ShowPill => Pill.Length > 0 && !IsPlaying;

    public bool CanDelete => !IsBuiltIn;

    public bool CanReset => IsBuiltIn && IsChanged;

    partial void OnIsChangedChanged(bool value) => OnPropertyChanged(nameof(CanReset));

    /// <summary>The name is being edited in place on the card.</summary>
    [ObservableProperty]
    public partial bool IsRenaming { get; set; }

    [ObservableProperty]
    public partial string EditName { get; set; } = "";
}

/// <summary>A ground picked for a scene from its card's menu.</summary>
public sealed record GroundChoice(SceneCardViewModel Card, Ground Ground);
