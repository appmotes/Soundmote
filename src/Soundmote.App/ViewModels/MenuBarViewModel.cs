using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Soundmote.App.Controls;
using Soundmote.App.Formatting;
using Soundmote.Core.Scenes;

namespace Soundmote.App.ViewModels;

/// <summary>
/// The menu bar (D16): what plays and how far it has come, play and pause, the next phase, every scene, and when to
/// stop. It plays scenes through the shell without changing what the window shows. <see cref="Update"/> is called by
/// the same timer as the shell's.
/// </summary>
public sealed partial class MenuBarViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;

    public MenuBarViewModel(ShellViewModel shell)
    {
        _shell = shell;
        Update();
    }

    /// <summary>Every scene, as the library has them; the menu lists them with ⌘1 to ⌘9.</summary>
    public IReadOnlyList<SceneCardViewModel> Scenes => _shell.Library.All;

    public SceneCardViewModel? Current => _shell.OpenCard;

    [ObservableProperty]
    public partial string Title { get; private set; } = "";

    [ObservableProperty]
    public partial string Status { get; private set; } = "";

    [ObservableProperty]
    public partial string PlayText { get; private set; } = "Play";

    [ObservableProperty]
    public partial bool CanPlay { get; private set; }

    /// <summary>"Skip to Break", or empty when there is no next phase to go to.</summary>
    [ObservableProperty]
    public partial string NextText { get; private set; } = "";

    [ObservableProperty]
    public partial bool IsLoop { get; private set; }

    [ObservableProperty]
    public partial StopAfter StopAfter { get; private set; }

    [ObservableProperty]
    public partial GlyphState Glyph { get; private set; } = GlyphState.Idle;

    /// <summary>Pauses or plays the open scene, or starts the first scene when none is open.</summary>
    [RelayCommand]
    private void TogglePlay()
    {
        if (_shell.Player is { } player) player.TogglePlayCommand.Execute(null);
        else if (Scenes.Count > 0) _shell.PlaySceneCommand.Execute(Scenes[0]);
        Update();
    }

    [RelayCommand]
    private void Play(SceneCardViewModel card)
    {
        _shell.PlaySceneCommand.Execute(card);
        Update();
    }

    [RelayCommand]
    private void SkipToNext()
    {
        _shell.Player?.SkipToNextPhaseCommand.Execute(null);
        Update();
    }

    [RelayCommand]
    private void SetStopAfter(StopAfter when)
    {
        if (_shell.Player is { } player) player.StopAfter = when;
        Update();
    }

    /// <summary>Reads the open scene into the menu's words and the icon. Cheap; meant for a timer.</summary>
    public void Update()
    {
        if (_shell.Player is not { } player)
        {
            Title = "Soundmote";
            Status = "Nothing playing";
            PlayText = "Play";
            CanPlay = Scenes.Count > 0;
            NextText = "";
            IsLoop = false;
            StopAfter = StopAfter.Never;
            Glyph = GlyphState.Idle;
            return;
        }
        var scene = player.Scene;
        Title = player.Name;
        PlayText = player.IsPlaying ? "Pause" : "Play";
        CanPlay = player.CanPlay;
        NextText = player.NextPhase is { } phase ? $"Skip to {phase.Name}" : "";
        IsLoop = scene.Kind == TimeKind.Loop;
        StopAfter = player.StopAfter;
        var status = scene.Kind switch
        {
            TimeKind.Loop when player.NextPhase is { } next => $"{SceneText.Clock(ToNext(player, next))} to {next.Name} · lap {player.Renderer.Lap}",
            TimeKind.Clock => player.Phases.FirstOrDefault(p => p.IsCurrent) is { } now ? $"{player.Clock} · {now.Phase.Name}" : player.Clock,
            _ => $"{player.Clock} {player.ClockCaption}"
        };
        Status = player.IsPlaying || player.Renderer.Finished ? status : $"Paused · {status}";
        var breakAt = scene.Kind == TimeKind.Loop && scene.Phases.Count > 1 ? scene.Phases[^1].From : 0;
        Glyph = GlyphState.Of(scene.Kind, player.Position, breakAt, player.IsPlaying);
    }

    private static TimeSpan ToNext(PlayerViewModel player, Phase next)
    {
        var turns = next.From - player.Position;
        return player.Length * (turns <= 0 ? turns + 1 : turns);
    }
}
