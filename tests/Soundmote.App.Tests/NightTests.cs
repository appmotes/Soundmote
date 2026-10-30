using Avalonia.Headless.XUnit;
using Soundmote.App.Assets;
using Soundmote.App.ViewModels;
using Soundmote.Core.Scenes;

namespace Soundmote.App.Tests;

/// <summary>D17: the bedside clock and the morning.</summary>
public sealed class NightTests : IDisposable
{
    private readonly SceneStore _store = new(Path.Combine(Path.GetTempPath(), "soundmote-tests", Guid.NewGuid().ToString("N")));
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 28, 14, 40, 0, TimeSpan.Zero));
    private readonly FakeOutput _output = new();
    private readonly ShellViewModel _shell;

    public NightTests() => _shell = new ShellViewModel(BuiltInScenes.All(), _store, () => _output, _clock);

    public void Dispose()
    {
        _shell.Dispose();
        if (Directory.Exists(_store.Folder)) Directory.Delete(_store.Folder, recursive: true);
    }

    private PlayerViewModel Open(string name)
    {
        _shell.OpenCommand.Execute(_shell.Library.All.Single(c => c.Name == name));
        return _shell.Player!;
    }

    private void Wait(double seconds)
    {
        _clock.Now += TimeSpan.FromSeconds(seconds);
        _output.Pull(0.02);
        _shell.Update();
    }

    [AvaloniaFact]
    public void A_sleep_scene_left_alone_dims_to_the_hour_and_a_touch_wakes_it()
    {
        Open("Fall Asleep");
        Wait(10);
        Assert.False(_shell.IsBedside);

        Wait(6);

        Assert.True(_shell.IsBedside);
        Assert.Equal("14:40", _shell.BedsideTime);
        Assert.Equal("45 min left", _shell.BedsideCaption);
        Assert.True(_shell.Touch());
        Assert.False(_shell.IsBedside);
        Assert.False(_shell.Touch());
    }

    [AvaloniaFact]
    public void Only_at_night_or_for_sleep_and_only_while_a_dial_plays()
    {
        var work = Open("Deep Work");
        Wait(20);
        Assert.False(_shell.IsBedside);

        _clock.Now = new DateTimeOffset(2026, 10, 28, 22, 10, 0, TimeSpan.Zero);
        Wait(20);
        Assert.True(_shell.IsBedside);
        Assert.Equal("Deep Work", _shell.BedsideCaption);

        work.IsPlaying = false;
        Wait(1);
        Assert.False(_shell.IsBedside);

        work.IsPlaying = true;
        _shell.ShowLibraryCommand.Execute(null);
        Wait(20);
        Assert.False(_shell.IsBedside);
    }

    [AvaloniaFact]
    public void A_journey_can_end_by_waking_at_a_time()
    {
        var player = Open("Fall Asleep");
        Assert.True(player.CanWake);
        Assert.Equal(("Then silence", "Wake me instead"), (player.EndText, player.WakeToggleText));

        player.ToggleWakeCommand.Execute(null);
        player.LaterWakeCommand.Execute(null);

        Assert.Equal("07:15", player.WakeText);
        Assert.Equal(new TimeSpan(7, 15, 0), player.Renderer.Wake!.At);
        Assert.Equal("Then wake at", player.EndText);
        Assert.Equal("Distant sea and stream rise over 10 min before, and play until you stop them.", player.WakeNote);

        player.EarlierWakeCommand.Execute(null);
        player.EarlierWakeCommand.Execute(null);
        player.ToggleWakeCommand.Execute(null);
        Assert.Null(player.Renderer.Wake);
        player.ToggleWakeCommand.Execute(null);
        Assert.Equal("06:45", player.WakeText);

        Wait(2);
        Assert.Equal(new TimeSpan(6, 45, 0), _store.Load().Single().Wake!.At);
    }

    [AvaloniaFact]
    public void After_the_journey_the_clock_says_when_it_wakes_and_the_morning_turns_the_dial_to_day()
    {
        _clock.Now = new DateTimeOffset(2026, 10, 28, 23, 0, 0, TimeSpan.Zero);
        var player = Open("Fall Asleep");
        player.ToggleWakeCommand.Execute(null);
        player.SeekCommand.Execute(1.0);
        Wait(1);

        Assert.True(player.IsPlaying);
        Assert.Equal("23:00", player.Clock);
        Assert.Equal("waking at 07:00", player.ClockCaption);
        Assert.Equal(Grounds.Night, _shell.Ground);
        Wait(20);
        Assert.Equal("waking at 07:00", _shell.BedsideCaption);

        _clock.Now = new DateTimeOffset(2026, 10, 29, 6, 55, 0, TimeSpan.Zero);
        Wait(1);

        Assert.True(player.IsWaking);
        Assert.Equal("good morning", player.ClockCaption);
        Assert.Equal(Grounds.Sky, _shell.Ground);
        Assert.False(_shell.IsBedside);
    }

    [AvaloniaFact]
    public void A_loop_cannot_wake()
    {
        var player = Open("Deep Work");

        player.ToggleWakeCommand.Execute(null);

        Assert.False(player.CanWake);
        Assert.Null(player.Wake);
    }
}
