using Avalonia.Headless.XUnit;
using Soundmote.App.Controls;
using Soundmote.App.ViewModels;
using Soundmote.Core.Scenes;

namespace Soundmote.App.Tests;

/// <summary>D16: the menu bar.</summary>
public sealed class MenuBarViewModelTests : IDisposable
{
    private readonly SceneStore _store = new(Path.Combine(Path.GetTempPath(), "soundmote-tests", Guid.NewGuid().ToString("N")));
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 28, 14, 40, 0, TimeSpan.Zero));
    private readonly FakeOutput _output = new();
    private readonly ShellViewModel _shell;
    private readonly MenuBarViewModel _menu;

    public MenuBarViewModelTests()
    {
        _shell = new ShellViewModel(BuiltInScenes.All(), _store, () => _output, _clock);
        _menu = new MenuBarViewModel(_shell);
    }

    public void Dispose()
    {
        _shell.Dispose();
        if (Directory.Exists(_store.Folder)) Directory.Delete(_store.Folder, recursive: true);
    }

    private SceneCardViewModel Card(string name) => _shell.Library.All.Single(c => c.Name == name);

    private void Tick(double seconds = 0.05)
    {
        _output.Pull(seconds);
        _shell.Update();
        _menu.Update();
    }

    [AvaloniaFact]
    public void With_nothing_open_play_starts_the_first_scene_and_leaves_the_window_alone()
    {
        Assert.Equal(("Soundmote", "Nothing playing"), (_menu.Title, _menu.Status));
        Assert.Equal(GlyphState.Idle, _menu.Glyph);

        _menu.TogglePlayCommand.Execute(null);
        Tick();

        Assert.Equal("Deep Work", _menu.Title);
        Assert.Equal("Pause", _menu.PlayText);
        Assert.True(_shell.IsLibraryOpen);
        Assert.Same(Card("Deep Work"), _menu.Current);
    }

    [AvaloniaFact]
    public void A_loop_counts_down_to_its_next_phase_and_can_skip_to_it()
    {
        _menu.PlayCommand.Execute(Card("Deep Work"));
        _shell.Player!.SeekCommand.Execute(31 / 60.0);
        Tick(0.01);

        // One block of 512 frames has played since the seek, so the countdown is a moment under 19 minutes.
        Assert.Equal("18:59 to Break · lap 1", _menu.Status);
        Assert.Equal("Skip to Break", _menu.NextText);
        Assert.Equal(GlyphState.Of(TimeKind.Loop, _shell.Player.Position, 50 / 60.0, true), _menu.Glyph);
        Assert.Equal(50 / 96.0, _menu.Glyph.Position);

        _menu.SkipToNextCommand.Execute(null);
        Tick(0.01);

        Assert.Equal(50 / 60.0, _shell.Player.Position, 3);
        Assert.Equal("9:59 to Work · lap 1", _menu.Status);
        Assert.Equal("Skip to Work", _menu.NextText);
    }

    [AvaloniaFact]
    public void A_journey_says_what_is_left_and_a_paused_one_says_so()
    {
        _menu.PlayCommand.Execute(Card("Fall Asleep"));
        Tick(0);
        Assert.Equal("45:00 left · ends 15:25", _menu.Status);

        _menu.TogglePlayCommand.Execute(null);
        Tick(0);

        Assert.Equal("Paused · 45:00 left", _menu.Status);
        Assert.Equal("Play", _menu.PlayText);
        Assert.False(_menu.Glyph.Playing);
    }

    [AvaloniaFact]
    public void The_clock_says_the_time_and_the_part_of_the_day()
    {
        _menu.PlayCommand.Execute(Card("All Day"));
        Tick();

        Assert.Equal("14:40 · Afternoon", _menu.Status);
        Assert.Equal("", _menu.NextText);
        Assert.Equal(TimeKind.Clock, _menu.Glyph.Kind);
    }

    [AvaloniaFact]
    public void Playing_from_the_menu_keeps_the_dial_open_on_the_new_scene()
    {
        _shell.OpenCommand.Execute(Card("Sit"));

        _menu.PlayCommand.Execute(Card("Fall Asleep"));

        Assert.True(_shell.IsDialOpen);
        Assert.Equal("Fall Asleep", _shell.Player!.Name);
        Assert.True(_shell.Player.IsPlaying);
    }

    [AvaloniaFact]
    public void Stop_after_half_an_hour_pauses_by_itself()
    {
        _menu.PlayCommand.Execute(Card("Sit"));
        _menu.SetStopAfterCommand.Execute(StopAfter.HalfHour);
        Tick();
        Assert.Equal(StopAfter.HalfHour, _menu.StopAfter);

        _clock.Now += TimeSpan.FromMinutes(31);
        Tick();

        Assert.False(_shell.Player!.IsPlaying);
        Assert.Equal(StopAfter.Never, _menu.StopAfter);
    }

    [AvaloniaFact]
    public void Stop_after_this_lap_pauses_at_the_start_of_the_next()
    {
        _menu.PlayCommand.Execute(Card("Deep Work"));
        var player = _shell.Player!;
        player.SetSpeedCommand.Execute(player.Speeds[2]);
        player.SeekCommand.Execute(0.99);
        Tick();
        _menu.SetStopAfterCommand.Execute(StopAfter.ThisLap);
        Assert.True(_menu.IsLoop);

        Tick(1);

        Assert.False(player.IsPlaying);
        Assert.Equal(2, player.Renderer.Lap);
        Assert.True(player.Position < 0.01);
    }
}
