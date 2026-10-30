using Avalonia.Headless.XUnit;
using Soundmote.App.Formatting;
using Soundmote.App.ViewModels;
using Soundmote.Core.Scenes;

namespace Soundmote.App.Tests;

/// <summary>D15: where a ring is heard, set in its panel.</summary>
public class PlaceEditorTests
{
    private static (PlayerViewModel Player, PlaceEditorViewModel Editor, FakeOutput Output) Select(int ring)
    {
        var output = new FakeOutput();
        var player = new PlayerViewModel(BuiltInScenes.Sit(3), output) { SelectedIndex = ring };
        return (player, player.PlaceEditor!, output);
    }

    [AvaloniaFact]
    public void A_ring_starts_in_the_middle_and_the_slider_moves_it()
    {
        var (player, editor, output) = Select(0);
        Assert.Equal("In the middle", editor.Layer.WhereText);
        Assert.Equal([true, false, false, false], editor.Moves.Select(m => m.IsChecked));

        editor.Pan = -0.7;
        output.Pull(0.1);
        player.Update();

        Assert.Equal(-0.7f, player.Renderer.PlaceOf(0).Pan, 3);
        Assert.Equal(-0.7f, editor.Layer.Pan, 3);
        Assert.Equal("Left", editor.Layer.WhereText);

        editor.Pan = 0.03;
        Assert.Equal(0f, player.Renderer.PlaceOf(0).Pan);
    }

    [AvaloniaFact]
    public void Width_and_moves_reach_the_engine()
    {
        var bowl = BuiltInScenes.Sit().Layers.ToList().FindIndex(l => l.Recipe.Id == "bowl");
        var (player, editor, _) = Select(bowl);

        editor.ChooseCommand.Execute(editor.Widths.Single(w => w.Label == "Point"));
        editor.ChooseCommand.Execute(editor.Moves.Single(m => m.Label == "Flits"));
        editor.ChooseCommand.Execute(editor.Glides.Single(g => g.Label == "Flies over"));

        var place = player.Renderer.PlaceOf(bowl);
        Assert.Equal((0f, Move.Flit, 4.0), (place.Width, place.Move, place.Seconds));
        Assert.True(editor.IsFlit && editor.HasRange && !editor.IsWander);
        Assert.Equal("Lands somewhere new at each strike.", editor.FlitText);
        Assert.Equal("Flits around the middle", editor.Layer.WhereText);
    }

    [AvaloniaFact]
    public void A_crossing_ring_keeps_its_way_round_when_it_moves_or_reaches_further()
    {
        var (player, editor, _) = Select(1);

        editor.ChooseCommand.Execute(editor.Moves.Single(m => m.Label == "Crosses"));
        editor.ChooseCommand.Execute(editor.Ways.Single(w => w.Label == "Right to left"));
        editor.ChooseCommand.Execute(editor.Ranges.Single(r => r.Label == "Side to side"));
        editor.Pan = 0.5;

        var path = player.Renderer.PlaceOf(1).Path;
        Assert.Equal(1f, path.At(0), 3);
        Assert.Equal(0.25f, path.At(1), 3);
        Assert.Equal("Crosses right to left", editor.Layer.WhereText);
        Assert.True(editor.Ways[1].IsChecked);
    }

    [AvaloniaFact]
    public void A_place_is_part_of_the_scene_as_it_is_now()
    {
        var (player, editor, _) = Select(2);
        var edited = 0;
        player.Edited += () => edited++;

        editor.Pan = 0.9;

        Assert.Equal(1, edited);
        Assert.Equal(0.9f, player.CurrentLayers()[2].Place.Pan, 3);
    }

    [Theory]
    [InlineData(0f, "In the middle")]
    [InlineData(0.3f, "A little right")]
    [InlineData(-0.6f, "Left")]
    [InlineData(-0.95f, "Far left")]
    public void A_still_place_reads_as_a_side(float pan, string expected) =>
        Assert.Equal(expected, SceneText.Where(new Place(pan)));

    [Fact]
    public void A_moving_place_says_how_it_moves()
    {
        Assert.Equal("Wanders around a little left", SceneText.Where(new Place(-0.3f) { Move = Move.Wander }));
        Assert.Equal("Crosses left to right", SceneText.Where(new Place { Move = Move.Cross, Path = Curve.Even(0.2f, 0.8f) }));
        Assert.Equal("Crosses and comes back", SceneText.Where(new Place { Move = Move.Cross, Path = Curve.Even(0.2f, 0.8f, 0.2f) }));
    }
}
