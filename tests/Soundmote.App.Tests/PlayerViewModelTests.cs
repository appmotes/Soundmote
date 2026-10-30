using Avalonia.Headless.XUnit;
using Soundmote.App.ViewModels;
using Soundmote.Core.Recipes;
using Soundmote.Core.Scenes;

namespace Soundmote.App.Tests;

public class PlayerViewModelTests
{
    private static (PlayerViewModel Player, FakeOutput Output) Create()
    {
        var output = new FakeOutput();
        return (new PlayerViewModel(BuiltInScenes.FallAsleep(3), output), output);
    }

    /// <summary>D23: a rhythm's ring shows the scene's tempo; changing it reaches the engine and is kept with the scene.</summary>
    [AvaloniaFact]
    public void A_rhythm_ring_shows_the_scenes_tempo_and_changing_it_reaches_the_engine()
    {
        var player = new PlayerViewModel(BuiltInScenes.Unwind(), new FakeOutput());
        player.SelectedIndex = 0;
        Assert.False(player.ShowsTempo);

        player.SelectedIndex = player.Layers.Single(l => l.Layer.Recipe.Id == "rhythm-downtempo").Index;
        Assert.True(player.ShowsTempo);
        Assert.Equal(90, player.Tempo);

        player.Tempo = 72;
        Assert.Equal(72, player.Renderer.Tempo);
        Assert.Equal("72 beats a minute", player.TempoText);
    }

    /// <summary>D21: a pad's ring has a Waver knob that reaches the engine; a rain's has none.</summary>
    [AvaloniaFact]
    public void A_pad_ring_shows_its_waver_knob_and_turning_it_reaches_the_engine()
    {
        var (player, _) = Create();
        player.SelectedIndex = 0;
        Assert.False(player.ShowsWaver);

        player.Try(RecipeLibrary.Get("pad-warm"), Curve.Steady(0.6f));
        player.KeepTrial();
        var pad = player.Layers.Count - 1;
        player.SelectedIndex = pad;

        Assert.True(player.ShowsWaver);
        Assert.Equal(Layer.DefaultWaver, player.Waver, 3);
        player.Waver = 0.8;
        Assert.Equal(0.8f, player.Renderer.WaverOf(pad), 3);
        Assert.Equal("±40 cents", player.WaverText);
        Assert.Equal(0.8f, player.CurrentLayers()[pad].Waver, 3);
    }

    [AvaloniaFact]
    public void It_starts_paused_with_the_output_running()
    {
        var (player, output) = Create();

        Assert.True(output.Started);
        Assert.False(player.IsPlaying);
        Assert.True(player.Renderer.Paused);
        Assert.Equal("45:00", player.Clock);
        Assert.Equal("left", player.ClockCaption);
    }

    [AvaloniaFact]
    public void Play_runs_the_engine_and_the_hand_moves()
    {
        var (player, output) = Create();
        player.SetSpeedCommand.Execute(player.Speeds[2]);

        player.TogglePlayCommand.Execute(null);
        output.Pull(2);
        player.Update();

        Assert.True(player.IsPlaying);
        Assert.InRange(player.Position, 115 / 2700.0, 125 / 2700.0); // ×60 for 2 s: two minutes of 45
        Assert.StartsWith("left · ends ", player.ClockCaption);
        Assert.Equal(new[] { false, false, true }, player.Speeds.Select(s => s.IsChecked));
    }

    [AvaloniaFact]
    public void Seeking_moves_the_hand_and_marks_the_phase()
    {
        var (player, output) = Create();

        player.SeekCommand.Execute(0.5);
        output.Pull(0.05);
        player.Update();

        Assert.Equal(0.5, player.Position, 3);
        Assert.Equal("22:30", player.Clock);
        Assert.Equal(new[] { false, true, false }, player.Phases.Select(p => p.IsCurrent));
        Assert.Equal("Drift · now", player.Phases[1].Title);
        Assert.True(player.Layers.Single(l => l.Layer.Recipe.Id == "pad-drone").Amount > 0.3f, "the drone carries the middle");
    }

    [AvaloniaFact]
    public void Muting_reaches_the_engine_the_ring_and_the_row()
    {
        var (player, _) = Create();
        var rain = player.Layers[0];

        player.ToggleMuteCommand.Execute(rain);

        Assert.True(player.Renderer.IsMuted(0));
        Assert.True(player.Rings[0].Muted);
        Assert.Equal("muted", rain.AmountText);
    }

    [AvaloniaFact]
    public void Selecting_a_ring_or_a_row_is_the_same_selection()
    {
        var (player, _) = Create();

        player.SelectedIndex = 1;
        Assert.True(player.Layers[1].IsSelected);
        Assert.Same(player.Layers[1].Tint, player.Layers[1].Background);

        player.SelectCommand.Execute(player.Layers[1]);
        Assert.Equal(-1, player.SelectedIndex);
        Assert.False(player.Layers[1].IsSelected);
    }

    [AvaloniaFact]
    public void A_finished_journey_stops_and_plays_again_from_the_start()
    {
        var (player, output) = Create();
        player.SeekCommand.Execute(1.0);
        player.TogglePlayCommand.Execute(null);
        output.Pull(0.05);
        player.Update();

        Assert.False(player.IsPlaying);
        Assert.Equal("ended", player.ClockCaption);

        player.TogglePlayCommand.Execute(null);
        output.Pull(0.05);
        player.Update();
        Assert.True(player.IsPlaying);
        Assert.True(player.Position < 0.01);
    }

    [AvaloniaFact]
    public void Volume_goes_to_the_device()
    {
        var (player, output) = Create();

        player.Volume = 0.3;

        Assert.Equal(0.3f, output.Volume, 3);
    }

    [AvaloniaFact]
    public void Without_an_output_the_scene_is_shown_but_cannot_play()
    {
        var player = new PlayerViewModel(BuiltInScenes.FallAsleep(), output: null);

        Assert.False(player.CanPlay);
        Assert.Equal(BuiltInScenes.FallAsleep().Layers.Count, player.Rings.Count);
    }

    [AvaloniaFact]
    public void Reshaping_a_ring_reaches_the_engine_the_dial_and_the_words()
    {
        var (player, output) = Create();
        player.SeekCommand.Execute(0.5);
        var rain = player.Layers[0];

        player.ReshapeCommand.Execute(new Controls.RingShape(0, Curve.Steady(0.05f)));
        output.Pull(0.05);
        player.Update();

        Assert.Same(rain.Curve, player.Renderer.CurveOf(0));
        Assert.Same(rain.Curve, player.Rings[0].Curve);
        Assert.Equal("Steady", rain.Description);
        Assert.True(rain.IsReshaped);
        Assert.True(rain.Amount < 0.1f);
    }

    [AvaloniaFact]
    public void A_preset_keeps_the_rings_loudness_and_reset_brings_the_scene_back()
    {
        var (player, _) = Create();
        var drone = BuiltInScenes.FallAsleep().Layers.ToList().FindIndex(l => l.Recipe.Id == "pad-drone");
        player.SelectedIndex = drone;
        var choir = player.Layers[drone];
        var original = choir.Curve;

        player.ApplyPresetCommand.Execute(player.Presets.Single(p => p.Name == "Fades"));

        Assert.Equal(BuiltInScenes.FallAsleep().Layers[drone].Curve.Points.Max(p => p.Value), choir.Curve.At(0), 3);
        Assert.Equal("Dies down slowly", choir.Description);

        player.ResetShapeCommand.Execute(null);
        Assert.Same(original, choir.Curve);
        Assert.False(choir.IsReshaped);
    }

    [AvaloniaFact]
    public void The_flat_view_shapes_the_selected_ring()
    {
        var (player, _) = Create();
        player.SelectedIndex = 1;

        player.ReshapeSelectedCommand.Execute(Curve.Even(0.1f, 0.9f));

        Assert.Equal(0.9f, player.Layers[1].Curve.At(1), 3);
        Assert.Equal("Builds to the end", player.SelectedLayer!.Description);
    }

    [AvaloniaFact]
    public void Trying_a_sound_plays_it_as_a_dashed_ring_and_adding_keeps_it()
    {
        var (player, output) = Create();
        player.OpenAddCommand.Execute(null);
        var add = player.AddSound!;
        var sea = add.Tiles.Single(t => t.Recipe.Id == "sea");

        add.TryCommand.Execute(sea);
        output.Pull(0.5);
        player.Update();

        var rings = BuiltInScenes.FallAsleep().Layers.Count;
        Assert.Equal(rings + 1, player.Layers.Count);
        Assert.True(player.Rings[rings].Trial);
        Assert.True(player.IsPlaying);
        Assert.Equal("Trying Calm sea", player.TrialText);
        Assert.Equal($"{rings} sounds + 1 on trial", player.Status.Split(" · ")[0]);

        add.ChooseShapeCommand.Execute(add.Shapes.Single(s => s.Name == "Rises"));
        Assert.Same(player.Layers[rings].Curve, player.Renderer.CurveOf(rings));
        Assert.Equal("Rises", add.Shape.Name);

        add.AddCommand.Execute(null);

        Assert.False(player.IsAdding);
        Assert.False(player.Layers[rings].IsTrial);
        Assert.False(player.Rings[rings].Trial);
        Assert.Equal(rings, player.SelectedIndex);
        Assert.Equal("", player.TrialText);
    }

    [AvaloniaFact]
    public void Trying_another_sound_replaces_the_trial_and_cancel_drops_it()
    {
        var (player, _) = Create();
        player.OpenAddCommand.Execute(null);
        var add = player.AddSound!;

        var rings = BuiltInScenes.FallAsleep().Layers.Count;
        add.TryCommand.Execute(add.Tiles[1]);
        add.TryCommand.Execute(add.Tiles[2]);
        Assert.Equal(rings + 1, player.Layers.Count);
        Assert.Equal(add.Tiles[2].Name, player.Layers[rings].Name);
        Assert.Equal(rings + 1, player.Renderer.LayerCount);

        add.CancelCommand.Execute(null);

        Assert.Equal(rings, player.Layers.Count);
        Assert.Equal(rings, player.Renderer.LayerCount);
        Assert.False(player.IsAdding);
    }

    [AvaloniaFact]
    public void Removing_a_ring_renumbers_the_rest()
    {
        var (player, _) = Create();
        player.SelectedIndex = 0;

        player.RemoveSelectedCommand.Execute(null);

        var rest = BuiltInScenes.FallAsleep().Layers.Skip(1).Select(l => l.Name).ToArray();
        Assert.Equal(rest, player.Layers.Select(l => l.Name));
        Assert.Equal(Enumerable.Range(0, rest.Length), player.Layers.Select(l => l.Index));
        Assert.Equal("1", player.Layers[0].Key);
        Assert.Equal(rest[0], player.Renderer.LayerAt(0).Name);
        Assert.Equal(rest.Length, player.Rings.Count);
        Assert.Equal(-1, player.SelectedIndex);
    }

    [AvaloniaFact]
    public void The_panel_shows_the_families_and_opens_one()
    {
        var (player, _) = Create();
        player.OpenAddCommand.Execute(null);
        var add = player.AddSound!;

        Assert.True(add.ShowsFamilies);
        Assert.Equal(Family.All.Select(f => f.Name), add.VisibleFamilies.Select(f => f.Name));
        var rain = add.VisibleFamilies.Single(f => f.Name == "Rain");
        Assert.Equal("6 sounds", rain.CountText);
        Assert.Equal("5 sounds", add.VisibleFamilies.Single(f => f.Name == "Thunder").CountText);
        Assert.All(add.Families, f => Assert.False(f.IsComing));
        Assert.Equal(BuiltInScenes.FallAsleep().Layers.Select(l => l.Name), add.InScene.Select(l => l.Name));

        add.OpenFamilyCommand.Execute(rain);
        Assert.True(add.ShowsSounds);
        Assert.Equal("Rain", add.Title);
        Assert.Equal(new[] { "Soft rain", "Window rain", "Skylight rain", "Tent rain", "Tropical rain", "Downpour" },
            add.Visible.Select(t => t.Name));
        Assert.Equal("Already in this scene", add.Visible[0].Sub);
        Assert.Equal("Indoors · cosy · calm", add.Visible.Single(t => t.Name == "Window rain").Sub);

        add.BackCommand.Execute(null);
        Assert.True(add.ShowsFamilies);
        Assert.Equal("Add a sound", add.Title);
    }

    [AvaloniaFact]
    public void A_place_or_a_mood_narrows_the_families_and_words_find_sounds()
    {
        var (player, _) = Create();
        player.OpenAddCommand.Execute(null);
        var add = player.AddSound!;

        add.TogglePlaceMenuCommand.Execute(null);
        Assert.True(add.IsPlaceMenuOpen);
        add.SetPlaceCommand.Execute(add.Places.Single(c => c.Label == "Indoors"));
        Assert.False(add.IsPlaceMenuOpen);
        Assert.Equal("Indoors", add.PlaceText);
        Assert.Equal(new[] { "Rain", "Wind", "Fire", "Voices & chant", "Bowls & bells", "Town & traffic", "Noise & pulse" },
            add.VisibleFamilies.Select(f => f.Name));
        Assert.Equal("2 sounds", add.VisibleFamilies[0].CountText);

        add.SetPlaceCommand.Execute(add.Places[0]);
        add.SetMoodCommand.Execute(add.Moods.Single(c => c.Label == "Wild"));
        Assert.Equal(new[] { "Rain", "Thunder", "Sea & shore", "Streams", "Wind", "Birds", "Rhythms" }, add.VisibleFamilies.Select(f => f.Name));

        add.SetMoodCommand.Execute(add.Moods[0]);
        add.Query = "crackle";
        Assert.True(add.ShowsSounds);
        Assert.Equal("Crackling hearth", Assert.Single(add.Visible).Name);
    }

    [AvaloniaFact]
    public void A_family_shows_only_its_own_places_and_drops_a_choice_it_does_not_have()
    {
        var (player, _) = Create();
        player.OpenAddCommand.Execute(null);
        var add = player.AddSound!;
        add.SetPlaceCommand.Execute(add.Places.Single(c => c.Label == "Temple"));

        add.OpenFamilyCommand.Execute(add.Families.Single(f => f.Name == "Rain"));

        Assert.Equal(new[] { "Any", "Forest", "Jungle", "Indoors", "Outdoors" }, add.Places.Select(c => c.Label));
        Assert.True(add.Places[0].IsChecked);
        Assert.Equal(6, add.Visible.Count);
    }

    [AvaloniaFact]
    public void Thunder_joins_a_scene_striking_at_random()
    {
        var (player, _) = Create();
        player.OpenAddCommand.Execute(null);
        var add = player.AddSound!;

        add.TryCommand.Execute(add.Tiles.Single(t => t.Name == "Distant thunder"));

        Assert.IsType<RandomPlay>(player.Layers[^1].Layer.Play);
    }

    [AvaloniaFact]
    public void Trying_a_sound_suggests_others_that_go_with_it()
    {
        var (player, _) = Create();
        player.OpenAddCommand.Execute(null);
        var add = player.AddSound!;

        add.TryCommand.Execute(add.Tiles.Single(t => t.Name == "Window rain"));

        Assert.NotEmpty(add.GoesWith);
        Assert.DoesNotContain(add.GoesWith, t => t.Recipe.Family == "rain" || t.InScene);
        Assert.Equal(add.GoesWith.Count, add.GoesWith.Select(t => t.Recipe.Family).Distinct().Count());
    }
}
