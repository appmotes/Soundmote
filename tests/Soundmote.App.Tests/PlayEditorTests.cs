using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Soundmote.App.Controls;
using Soundmote.App.ViewModels;
using Soundmote.Core.Scenes;

namespace Soundmote.App.Tests;

public class PlayEditorTests
{
    private static (PlayerViewModel Player, FakeOutput Output, PlayEditorViewModel Editor) Select(int ring)
    {
        var output = new FakeOutput();
        var player = new PlayerViewModel(BuiltInScenes.FallAsleep(3), output) { SelectedIndex = ring };
        return (player, output, player.PlayEditor!);
    }

    private static ChoiceItem Mode(PlayEditorViewModel editor, string label) => editor.Modes.Single(m => m.Label == label);

    [AvaloniaFact]
    public void Switching_to_on_off_reaches_the_engine_the_dial_and_the_words()
    {
        var (player, _, editor) = Select(0);

        editor.ChooseCommand.Execute(Mode(editor, "On/off"));

        var play = Assert.IsType<OnOffPlay>(player.Layers[0].Play);
        Assert.Same(play, player.Renderer.PlayOf(0));
        Assert.Same(play, player.Rings[0].Play);
        Assert.True(editor.IsOnOff);
        Assert.Equal("On once · 15 min in all", player.Layers[0].Description);
        Assert.Equal("15:00 – 30:00", Assert.Single(editor.Stretches).Text);
        Assert.True(Mode(editor, "On/off").IsChecked);
    }

    [AvaloniaFact]
    public void Every_shows_its_strikes_and_the_next_one()
    {
        var (player, output, editor) = Select(1);

        editor.ChooseCommand.Execute(Mode(editor, "Every"));
        editor.ChooseCommand.Execute(editor.Intervals.Single(i => i.Label == "10 min"));
        player.SeekCommand.Execute(12 / 45.0);
        output.Pull(0.05);
        player.Update();

        Assert.Equal(5, player.Layers[1].Strikes.Count);
        Assert.Equal(5, player.Rings[1].Strikes.Count);
        Assert.Equal("Every 10 min", player.Layers[1].Description);
        Assert.Equal("next 20:00", player.Layers[1].AmountText);
    }

    [AvaloniaFact]
    public void Each_way_remembers_its_settings()
    {
        var (player, _, editor) = Select(1);
        editor.ChooseCommand.Execute(Mode(editor, "Every"));
        editor.ChooseCommand.Execute(editor.Intervals.Single(i => i.Label == "2 min"));

        editor.ChooseCommand.Execute(Mode(editor, "Shape"));
        editor.ChooseCommand.Execute(Mode(editor, "Every"));

        Assert.Equal(TimeSpan.FromMinutes(2), Assert.IsType<EveryPlay>(player.Layers[1].Play).Interval);
        Assert.True(editor.Intervals.Single(i => i.Label == "2 min").IsChecked);
    }

    [AvaloniaFact]
    public void Random_changes_its_rate_and_shuffles_its_places()
    {
        var (player, _, editor) = Select(2);
        editor.ChooseCommand.Execute(Mode(editor, "Random"));
        var before = player.Layers[2].Strikes.ToArray();

        editor.MoreStrikesCommand.Execute(null);
        Assert.Equal(7, Assert.IsType<RandomPlay>(player.Layers[2].Play).PerTenMinutes);
        Assert.Equal("7 per 10 min", editor.RateText);

        var unshuffled = player.Layers[2].Strikes.ToArray();
        editor.ShuffleCommand.Execute(null);
        Assert.NotEqual(unshuffled, player.Layers[2].Strikes);
        Assert.NotEmpty(before);
    }

    [AvaloniaFact]
    public void Stretches_are_added_removed_and_levelled_from_the_panel()
    {
        var (player, _, editor) = Select(0);
        editor.ChooseCommand.Execute(Mode(editor, "On/off"));

        editor.AddStretchCommand.Execute(null);
        Assert.Equal(2, editor.Stretches.Count);
        editor.RemoveStretchCommand.Execute(editor.Stretches[0]);
        Assert.Single(editor.Stretches);
        editor.Level = 0.4;

        Assert.Equal(0.4f, Assert.IsType<OnOffPlay>(player.Layers[0].Play).Level, 3);
    }

    [AvaloniaFact]
    public void A_stretch_end_drags_on_the_ring_and_the_ring_takes_a_new_stretch_on_double_click()
    {
        var (player, _, editor) = Select(0);
        editor.ChooseCommand.Execute(Mode(editor, "On/off"));
        var dial = new Dial { Length = player.Length, SelectedIndex = 0, PlayCommand = player.ChangePlayCommand };
        dial.Bind(Dial.RingsProperty, new Avalonia.Data.Binding(nameof(PlayerViewModel.Rings)) { Source = player });
        var window = new Window { Width = 600, Height = 600, Content = dial };
        window.Show();
        var layout = dial.CurrentLayout();
        var r = layout.RingRadius(0);

        var end = layout.At(r, 2 / 3.0);
        Assert.Equal(1, dial.HandleNear(layout, end));
        window.MouseDown(end, MouseButton.Left);
        window.MouseMove(layout.At(r, 0.75));
        window.MouseUp(layout.At(r, 0.75), MouseButton.Left);
        Assert.Equal(0.75, Assert.IsType<OnOffPlay>(player.Layers[0].Play).Stretches[0].To, 2);

        var spot = layout.At(r, 0.1);
        window.MouseDown(spot, MouseButton.Left);
        window.MouseUp(spot, MouseButton.Left);
        window.MouseDown(spot, MouseButton.Left);
        window.MouseUp(spot, MouseButton.Left);
        Assert.Equal(2, Assert.IsType<OnOffPlay>(player.Layers[0].Play).Stretches.Count);
        window.Close();
    }
}
