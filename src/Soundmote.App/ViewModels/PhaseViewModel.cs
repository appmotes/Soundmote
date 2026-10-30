using CommunityToolkit.Mvvm.ComponentModel;
using Soundmote.App.Formatting;
using Soundmote.Core.Scenes;

namespace Soundmote.App.ViewModels;

/// <summary>A named stretch of the turn in the side panel; the one the hand is in is marked.</summary>
public sealed partial class PhaseViewModel(Phase phase, TimeSpan length) : ObservableObject
{
    public Phase Phase { get; } = phase;

    public string Range { get; } = SceneText.Range(phase, length);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial bool IsCurrent { get; set; }

    public string Title => IsCurrent ? $"{Phase.Name} · now" : Phase.Name;

    public bool Contains(double position) =>
        position >= Phase.From && (position < Phase.To || (Phase.To >= 1 && position <= 1));
}
