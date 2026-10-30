using Avalonia.Headless.XUnit;
using Soundmote.App.Assets;
using Soundmote.App.Settings;
using Soundmote.App.ViewModels;
using Soundmote.Core.Scenes;

namespace Soundmote.App.Tests;

/// <summary>D18 and D19: settings, kept in a file, and the first run with its hints.</summary>
public sealed class SettingsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "soundmote-tests", Guid.NewGuid().ToString("N"));
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 28, 14, 40, 0, TimeSpan.Zero));
    private readonly FakeOutput _output = new();

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private PreferencesStore Preferences => new(Path.Combine(_folder, "settings.json"));

    private ShellViewModel Launch(Preferences? start = null)
    {
        if (start is not null) Preferences.Save(start);
        return new ShellViewModel(BuiltInScenes.All(), new SceneStore(Path.Combine(_folder, "Scenes")), () => _output, _clock,
            preferences: Preferences) { SupportsMenuBar = true };
    }

    private static SceneCardViewModel Card(ShellViewModel shell, string name) => shell.Library.All.Single(c => c.Name == name);

    [AvaloniaFact]
    public void Settings_are_kept_in_a_file_and_a_broken_one_reads_as_a_first_run()
    {
        Preferences.Save(Settings.Preferences.Seasoned with { FadeSeconds = 30, NewGround = Ground.Sky, StartWith = StartWith.Scenes });

        var read = Preferences.Load();

        Assert.Equal((30.0, Ground.Sky, StartWith.Scenes, true), (read.FadeSeconds, read.NewGround, read.StartWith, read.Welcomed));
        Assert.Equal(Hints.All, read.HintsSeen);

        File.WriteAllText(Preferences.Path, "{");
        Assert.False(Preferences.Load().Welcomed);
    }

    [AvaloniaFact]
    public void The_sheet_changes_settings_at_once()
    {
        var shell = Launch(Settings.Preferences.Seasoned);
        shell.OpenSettingsCommand.Execute(null);
        var sheet = shell.Settings!;
        var changed = 0;
        shell.PreferencesChanged += () => changed++;

        sheet.ChooseFadeCommand.Execute(sheet.Fades.Single(f => f.Label == "30 s"));
        sheet.ChooseGroundCommand.Execute(sheet.Grounds.Single(g => g.Label == "Ember"));
        sheet.ShowMenuBar = false;
        sheet.DimAtNight = false;

        Assert.Equal(4, changed);
        Assert.False(sheet.CanKeepPlaying);
        var kept = Preferences.Load();
        Assert.Equal((30.0, Ground.Ember, false, false), (kept.FadeSeconds, kept.NewGround, kept.ShowMenuBar, kept.DimAtNight));
        Assert.True(sheet.Fades[2].IsChecked);

        sheet.CloseCommand.Execute(null);
        shell.StartNewSceneCommand.Execute(null);
        shell.NewScene!.ChooseIntentCommand.Execute(shell.NewScene.Intents.Single(i => i.Label == "Focus"));
        Assert.Equal(Ground.Ember, shell.NewScene.Preview.Ground);
    }

    [AvaloniaFact]
    public void A_scene_that_stops_by_itself_fades_as_long_as_settings_say()
    {
        var shell = Launch(Settings.Preferences.Seasoned with { FadeSeconds = 30 });
        shell.OpenCommand.Execute(Card(shell, "Sit"));
        var player = shell.Player!;
        player.StopAfter = StopAfter.HalfHour;

        _clock.Now += TimeSpan.FromMinutes(31);
        shell.Update();

        Assert.False(player.IsPlaying);
        Assert.Equal(30, player.Renderer.FadeOutSeconds);
        player.IsPlaying = true;
        Assert.Equal(0.25, player.Renderer.FadeOutSeconds);
    }

    [AvaloniaFact]
    public void Reset_all_asks_once_then_undoes_every_changed_scene()
    {
        var shell = Launch(Settings.Preferences.Seasoned);
        foreach (var name in new[] { "Sit", "Deep Work" })
        {
            var card = Card(shell, name);
            shell.SetGroundCommand.Execute(new GroundChoice(card, Ground.Night));
        }
        shell.OpenSettingsCommand.Execute(null);
        var sheet = shell.Settings!;

        sheet.ResetAllCommand.Execute(null);
        Assert.Equal("Reset 2 scenes", sheet.ResetText);
        Assert.Equal(2, shell.ChangedCount);

        sheet.ResetAllCommand.Execute(null);
        Assert.Equal(0, shell.ChangedCount);
        Assert.False(sheet.CanReset);
        Assert.Equal(Ground.Ember, Card(shell, "Sit").Scene.Ground);
    }

    [AvaloniaFact]
    public void Soundmote_opens_on_the_last_scene_or_plays_it_as_settings_say()
    {
        var first = Launch(Settings.Preferences.Seasoned);
        first.OpenCommand.Execute(Card(first, "Sit"));
        first.Dispose();

        var paused = Launch();
        Assert.True(paused.IsDialOpen);
        Assert.Equal("Sit", paused.Player!.Name);
        Assert.False(paused.Player.IsPlaying);
        paused.Dispose();

        var playing = Launch(Preferences.Load() with { StartWith = StartWith.PlayLastScene });
        Assert.True(playing.Player!.IsPlaying);
        playing.Dispose();

        var library = Launch(Preferences.Load() with { StartWith = StartWith.Scenes });
        Assert.True(library.IsLibraryOpen);
        Assert.Null(library.Player);
    }

    [AvaloniaFact]
    public void Dimming_at_night_can_be_turned_off()
    {
        var shell = Launch(Settings.Preferences.Seasoned with { DimAtNight = false });
        shell.OpenCommand.Execute(Card(shell, "Fall Asleep"));

        _clock.Now += TimeSpan.FromSeconds(20);
        shell.Update();

        Assert.False(shell.IsBedside);
    }

    [AvaloniaFact]
    public void The_first_run_plays_a_scene_quietly_and_then_never_comes_back()
    {
        var shell = Launch();
        Assert.True(shell.ShowsFirstRun);
        Assert.False(shell.ShowsLibrary);

        shell.WelcomeCommand.Execute(Card(shell, "Fall Asleep"));

        Assert.False(shell.IsFirstRun);
        Assert.True(shell.IsDialOpen);
        Assert.True(shell.Player!.IsPlaying);
        Assert.Equal(0.4, shell.Player.Volume);
        shell.Dispose();
        Assert.False(Launch().IsFirstRun);
    }

    [AvaloniaFact]
    public void An_empty_dial_from_the_first_run_opens_new_scene()
    {
        var shell = Launch();

        shell.BeginEmptyCommand.Execute(null);

        Assert.True(shell.IsCreating);
        Assert.False(shell.IsFirstRun);
    }

    [AvaloniaFact]
    public void Each_hint_shows_once_and_is_used_up_by_doing_it()
    {
        var shell = Launch();
        shell.WelcomeCommand.Execute(Card(shell, "Sit"));
        shell.Update();
        Assert.Equal("Click a ring to shape it", shell.HintTitle);

        shell.Player!.SelectedIndex = 1;
        shell.Update();
        Assert.Equal("Drag the hand to move in time", shell.HintTitle);

        shell.DismissHintCommand.Execute(null);
        Assert.Equal("+ brings in another sound", shell.HintTitle);

        shell.ShowLibraryCommand.Execute(null);
        shell.Update();
        Assert.False(shell.HasHint);

        shell.ShowDialCommand.Execute(null);
        shell.Player.OpenAddCommand.Execute(null);
        shell.Update();
        Assert.False(shell.HasHint);
        Assert.Equal(Hints.All, Preferences.Load().HintsSeen);
    }
}
