using Avalonia.Headless.XUnit;
using Soundmote.App.Assets;
using Soundmote.App.Controls;
using Soundmote.App.ViewModels;
using Soundmote.Core.Scenes;

namespace Soundmote.App.Tests;

public sealed class ShellViewModelTests : IDisposable
{
    private readonly SceneStore _store = new(Path.Combine(Path.GetTempPath(), "soundmote-tests", Guid.NewGuid().ToString("N")));
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 28, 14, 40, 0, TimeSpan.Zero));
    private readonly List<FakeOutput> _outputs = [];

    public void Dispose()
    {
        if (Directory.Exists(_store.Folder)) Directory.Delete(_store.Folder, recursive: true);
    }

    private ShellViewModel Create() => new(BuiltInScenes.All(), _store, () =>
    {
        var output = new FakeOutput();
        _outputs.Add(output);
        return output;
    }, _clock);

    private static SceneCardViewModel Card(ShellViewModel shell, string name) => shell.Library.All.Single(c => c.Name == name);

    private static IEnumerable<string> Yours(ShellViewModel shell) => shell.Library.Yours.OfType<SceneCardViewModel>().Select(c => c.Name);

    /// <summary>Lets the clock pass the rest a scene waits before it is written.</summary>
    private void Rest(ShellViewModel shell)
    {
        _clock.Now += TimeSpan.FromSeconds(2);
        shell.Update();
    }

    [AvaloniaFact]
    public void It_opens_on_the_library_on_paper_with_nothing_playing()
    {
        var shell = Create();

        Assert.True(shell.IsLibraryOpen);
        Assert.Null(shell.Player);
        Assert.Empty(_outputs);
        Assert.Equal(Grounds.Paper, shell.Ground);
        Assert.Equal(["Deep Work", "Fall Asleep", "Sit", "Unwind", "All Day"], shell.Library.FromSoundmote.Select(c => c.Name));
        Assert.Empty(Yours(shell));
        Assert.IsType<NewSceneTile>(Assert.Single(shell.Library.Yours));
        Assert.Equal("YOURS · 0", shell.Library.YoursTitle);
    }

    [AvaloniaFact]
    public void Opening_a_scene_plays_it_on_its_ground()
    {
        var shell = Create();

        shell.OpenCommand.Execute(Card(shell, "Sit"));
        shell.Update();

        Assert.True(shell.IsDialOpen);
        Assert.True(shell.Player!.IsPlaying);
        Assert.Equal(Grounds.Ember, shell.Ground);
        Assert.True(Card(shell, "Sit").IsPlaying);
    }

    [AvaloniaFact]
    public void Opening_another_scene_stops_the_first_and_keeps_the_volume()
    {
        var shell = Create();
        shell.OpenCommand.Execute(Card(shell, "Sit"));
        shell.Player!.Volume = 0.3;

        shell.ShowLibraryCommand.Execute(null);
        shell.OpenCommand.Execute(Card(shell, "Fall Asleep"));
        shell.Update();

        Assert.Equal("Fall Asleep", shell.Player.Name);
        Assert.Equal(0.3, shell.Player.Volume);
        Assert.False(_outputs[0].Started);
        Assert.True(_outputs[1].Started);
        Assert.False(Card(shell, "Sit").IsPlaying);
        Assert.True(Card(shell, "Fall Asleep").IsPlaying);
    }

    [AvaloniaFact]
    public void Going_back_to_the_library_keeps_the_scene_playing_and_reopening_it_does_not_restart_it()
    {
        var shell = Create();
        shell.OpenCommand.Execute(Card(shell, "Deep Work"));
        var player = shell.Player;
        player!.SelectedIndex = 1;

        shell.ShowLibraryCommand.Execute(null);

        Assert.True(shell.IsLibraryOpen);
        Assert.Equal(Grounds.Paper, shell.Ground);
        Assert.True(player.IsPlaying);
        Assert.Equal(-1, player.SelectedIndex);

        shell.OpenCommand.Execute(Card(shell, "Deep Work"));
        Assert.Same(player, shell.Player);
        Assert.Single(_outputs);
    }

    [AvaloniaFact]
    public void A_filter_shows_only_scenes_for_that_intent()
    {
        var shell = Create();
        var library = shell.Library;

        Assert.Equal(["All", "Sleep", "Focus", "Meditate", "Relax", "Background"], library.Chips.Select(c => c.Label));
        library.FilterCommand.Execute(library.Chips.Single(c => c.Label == "Meditate"));

        Assert.Equal(["Sit"], library.FromSoundmote.Select(c => c.Name));
        Assert.Equal([false, false, false, true, false, false], library.Chips.Select(c => c.IsChecked));

        library.FilterCommand.Execute(library.Chips[0]);
        Assert.Equal(5, library.FromSoundmote.Count);
    }

    [AvaloniaFact]
    public void A_card_draws_the_strikes_its_dial_will_play()
    {
        var shell = Create();
        var card = Card(shell, "Sit");

        shell.OpenCommand.Execute(card);

        var bowl = BuiltInScenes.Sit().Layers.ToList().FindIndex(l => l.Recipe.Id == "bowl");
        Assert.Equal(shell.Player!.Rings[bowl].Strikes, card.Rings[bowl].Strikes);
        Assert.Equal(4, card.Rings[bowl].Strikes.Count);
    }

    /// <summary>A12: no Save button. A change is written once the scene has rested, and a changed built-in says so.</summary>
    [AvaloniaFact]
    public void A_shaped_ring_is_kept_and_reset_brings_the_original_back()
    {
        var shell = Create();
        shell.OpenCommand.Execute(Card(shell, "Fall Asleep"));

        shell.Player!.ReshapeCommand.Execute(new RingShape(0, Curve.Steady(0.3f)));
        shell.Update();
        Assert.False(File.Exists(_store.PathOf("fall-asleep")));
        Rest(shell);
        Assert.True(File.Exists(_store.PathOf("fall-asleep")));
        Assert.Equal("Changed", Card(shell, "Fall Asleep").Pill);

        shell.Dispose();
        var again = Create();
        var card = Card(again, "Fall Asleep");
        Assert.True(card.IsChanged);
        Assert.Equal(0.3f, card.Scene.Layers[0].Curve.At(0.5));

        again.ResetCommand.Execute(card);
        Assert.False(File.Exists(_store.PathOf("fall-asleep")));
        Assert.False(card.IsChanged);
        Assert.Equal(BuiltInScenes.FallAsleep().Layers[0].Curve.At(0.5), card.Scene.Layers[0].Curve.At(0.5));
    }

    [AvaloniaFact]
    public void Leaving_the_app_writes_a_change_that_has_not_rested()
    {
        var shell = Create();
        shell.OpenCommand.Execute(Card(shell, "Sit"));
        shell.Player!.SelectedIndex = 0;
        shell.Player.RemoveSelectedCommand.Execute(null);

        shell.Dispose();

        Assert.Equal(BuiltInScenes.Sit().Layers.Count - 1, _store.Load().Single().Layers.Count);
    }

    [AvaloniaFact]
    public void Duplicate_lands_among_yours_and_opens()
    {
        var shell = Create();

        shell.DuplicateCommand.Execute(Card(shell, "Sit"));
        shell.DuplicateCommand.Execute(Card(shell, "Sit 2"));

        Assert.Equal(["Sit 2", "Sit 3"], Yours(shell));
        var copy = Card(shell, "Sit 3");
        Assert.Equal("Just made", copy.Pill);
        Assert.Same(copy, shell.OpenCard);
        Assert.True(shell.Player!.IsPlaying);
        Assert.Equal(["sit-2", "sit-3"], _store.Load().Select(s => s.Id));
        Assert.False(Card(shell, "Sit").IsChanged);
    }

    [AvaloniaFact]
    public void Rename_in_place_keeps_a_name_and_ignores_an_empty_one()
    {
        var shell = Create();
        shell.DuplicateCommand.Execute(Card(shell, "Sit"));
        var card = Card(shell, "Sit 2");

        shell.StartRenameCommand.Execute(card);
        Assert.True(card.IsRenaming);
        Assert.Equal("Sit 2", card.EditName);
        card.EditName = "  Evening Sit ";
        shell.CommitRenameCommand.Execute(card);

        Assert.False(card.IsRenaming);
        Assert.Equal("Evening Sit", shell.Player!.Name);
        Assert.Equal("Evening Sit", _store.Load().Single().Name);
        Assert.Equal("sit-2", _store.Load().Single().Id);

        shell.StartRenameCommand.Execute(card);
        card.EditName = " ";
        shell.CommitRenameCommand.Execute(card);
        Assert.Equal("Evening Sit", card.Name);
    }

    [AvaloniaFact]
    public void A_deleted_scene_comes_back_with_undo_until_the_bar_goes()
    {
        var shell = Create();
        shell.DuplicateCommand.Execute(Card(shell, "Sit"));
        var card = Card(shell, "Sit 2");

        shell.DeleteCommand.Execute(card);

        Assert.Null(shell.Player);
        Assert.True(shell.IsLibraryOpen);
        Assert.Empty(Yours(shell));
        Assert.Empty(_store.Load());
        Assert.Equal("Sit 2 deleted", shell.Toast);
        Assert.True(shell.CanUndo);

        shell.UndoCommand.Execute(null);
        Assert.Equal(["Sit 2"], Yours(shell));
        Assert.Single(_store.Load());
        Assert.False(shell.HasToast);

        shell.DeleteCommand.Execute(card);
        _clock.Now += TimeSpan.FromSeconds(9);
        shell.Update();
        Assert.False(shell.HasToast);
        shell.UndoCommand.Execute(null);
        Assert.Empty(Yours(shell));
    }

    [AvaloniaFact]
    public void Soundmotes_scenes_cannot_be_deleted_and_take_a_new_ground()
    {
        var shell = Create();
        var sit = Card(shell, "Sit");
        shell.OpenCommand.Execute(sit);

        shell.DeleteCommand.Execute(sit);
        shell.SetGroundCommand.Execute(new GroundChoice(sit, Ground.Sky));
        Rest(shell);

        Assert.Contains(sit, shell.Library.FromSoundmote);
        Assert.Equal(Grounds.Sky, shell.Ground);
        Assert.Equal(Ground.Sky, _store.Load().Single().Ground);
    }

    [AvaloniaFact]
    public void A_new_scene_from_another_scenes_sounds_opens_playing()
    {
        var shell = Create();
        shell.StartNewSceneCommand.Execute(null);
        var sheet = shell.NewScene!;

        sheet.ChooseIntentCommand.Execute(sheet.Intents.Single(i => i.Label == "Meditate"));
        Assert.Equal(Ground.Ember, sheet.Preview.Ground);
        Assert.Equal(TimeSpan.FromMinutes(20), sheet.Preview.Length);
        Assert.Equal("Arrive · Sit · Return", sheet.PhasesText);
        Assert.Equal(["Empty", "Sit's sounds"], sheet.Starts.Select(s => s.Label));

        sheet.ChooseStartCommand.Execute(sheet.Starts[1]);
        sheet.ChooseLengthCommand.Execute(sheet.Lengths.Single(l => l.Label == "30"));
        sheet.Name = "Evening Sit";
        sheet.CreateCommand.Execute(null);

        Assert.False(shell.IsCreating);
        Assert.Equal(["Evening Sit"], Yours(shell));
        Assert.Equal("Evening Sit", shell.Player!.Name);
        Assert.Equal(BuiltInScenes.Sit().Layers.Count, shell.Player.Layers.Count);
        Assert.Equal(TimeSpan.FromMinutes(30), shell.Player.Length);
        Assert.True(shell.Player.IsPlaying);
        Assert.Equal("evening-sit", _store.Load().Single().Id);
    }

    [AvaloniaFact]
    public void The_sheet_follows_the_purpose_until_a_choice_is_made_and_an_empty_scene_opens_the_add_panel()
    {
        var shell = Create();
        shell.StartNewSceneCommand.Execute(null);
        var sheet = shell.NewScene!;

        sheet.ChooseIntentCommand.Execute(sheet.Intents.Single(i => i.Label == "Focus"));
        Assert.Equal(TimeKind.Loop, sheet.Preview.Kind);
        sheet.ChooseGroundCommand.Execute(sheet.Grounds.Single(g => g.Label == "Night"));
        sheet.ChooseKindCommand.Execute(sheet.Kinds.Single(k => k.Label == "Clock"));
        sheet.ChooseIntentCommand.Execute(sheet.Intents.Single(i => i.Label == "Background"));

        Assert.Equal(Ground.Night, sheet.Preview.Ground);
        Assert.Equal(TimeKind.Clock, sheet.Preview.Kind);
        Assert.False(sheet.HasLengths);

        sheet.Name = "";
        Assert.False(sheet.CreateCommand.CanExecute(null));
        sheet.Name = "Room";
        sheet.CreateCommand.Execute(null);

        Assert.True(shell.Player!.IsAdding);
        Assert.DoesNotContain(shell.Player.Layers, l => !l.IsTrial);
    }

    [AvaloniaFact]
    public void Cancel_closes_the_sheet_and_makes_nothing()
    {
        var shell = Create();
        shell.StartNewSceneCommand.Execute(null);

        shell.NewScene!.CancelCommand.Execute(null);

        Assert.False(shell.IsCreating);
        Assert.Empty(Yours(shell));
        Assert.False(Directory.Exists(_store.Folder));
    }

    [AvaloniaFact]
    public void A_file_that_does_not_read_is_said_in_the_bar()
    {
        Directory.CreateDirectory(_store.Folder);
        File.WriteAllText(_store.PathOf("broken"), "{");

        var shell = Create();

        Assert.Equal("One scene file could not be read", shell.Toast);
        Assert.StartsWith("broken.json", Assert.Single(shell.Skipped));
    }
}
