using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Soundmote.App.Controls;
using Soundmote.App.Tests;
using Soundmote.App.ViewModels;
using Soundmote.Core.Scenes;
using Soundmote.Desktop.Shell;

namespace Soundmote.Desktop.Tests;

public class PlayerWindowTests
{
    /// <summary>Where these tests keep scenes; emptied as each window closes.</summary>
    private static readonly string Scenes = Path.Combine(Path.GetTempPath(), "soundmote-window-tests");

    private static readonly FixedClock Afternoon = new(new DateTimeOffset(2026, 10, 28, 14, 40, 0, TimeSpan.Zero));

    /// <summary>The whole window at 1280 × 800, as on the design canvas, into bin/…/snapshots for review.</summary>
    [AvaloniaFact]
    public void Window_renders_the_scene_on_the_dial()
    {
        var (shell, player, output) = Open("Fall Asleep");
        player.SeekCommand.Execute(12 / 45.0);
        output.Pull(0.4);
        shell.Update();
        player.SelectedIndex = 0;

        Snapshot(shell, "player.png");
    }

    /// <summary>The wind ring being shaped: points, the old shape, the value tip and the flat view.</summary>
    [AvaloniaFact]
    public void Window_renders_a_ring_being_shaped()
    {
        var (shell, player, output) = Open("Fall Asleep");
        player.IsPlaying = false;
        player.SeekCommand.Execute(12 / 45.0);
        output.Pull(0.1);
        shell.Update();
        player.SelectedIndex = 1;

        var window = Show(shell);
        var dial = window.GetVisualDescendants().OfType<Dial>().Single();
        var layout = dial.CurrentLayout();
        var (at, value) = player.Layers[1].Curve.Points[6];
        var offset = dial.TranslatePoint(new Point(), window)!.Value;
        var handle = layout.At(layout.RingRadius(1) + value * layout.MaxWidth / 2, at) + offset;
        window.MouseDown(handle, MouseButton.Left);
        window.MouseMove(layout.At(layout.RingRadius(1) + 0.85 * layout.MaxWidth / 2, at) + offset);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        Assert.True(player.Layers[1].Curve.Points[6].Value > value + 0.3f);
        Save(window, "player-shaping.png");
        window.MouseUp(handle, MouseButton.Left);
        Close(window, shell);
    }

    /// <summary>The add panel open on the families (D20).</summary>
    [AvaloniaFact]
    public void Window_renders_the_add_panel_with_the_families()
    {
        var (shell, player, output) = Open("Fall Asleep");
        player.IsPlaying = false;
        player.SeekCommand.Execute(12 / 45.0);
        player.OpenAddCommand.Execute(null);
        output.Pull(0.2);
        shell.Update();

        Snapshot(shell, "player-adding-families.png");
    }

    /// <summary>The add panel with Rain opened and the window rain on trial.</summary>
    [AvaloniaFact]
    public void Window_renders_the_add_panel_with_a_sound_on_trial()
    {
        var (shell, player, output) = Open("Fall Asleep");
        player.IsPlaying = false;
        player.SeekCommand.Execute(12 / 45.0);
        player.OpenAddCommand.Execute(null);
        var add = player.AddSound!;
        add.OpenFamilyCommand.Execute(add.Families.Single(f => f.Name == "Rain"));
        add.TryCommand.Execute(add.Tiles.Single(t => t.Name == "Window rain"));
        output.Pull(0.2);
        shell.Update();

        Snapshot(shell, "player-adding.png");
    }

    /// <summary>Rain on and off (selected), wind striking every 5 min, choir at random: the three new ways at once.</summary>
    [AvaloniaFact]
    public void Window_renders_rings_that_play_on_off_every_and_random()
    {
        var (shell, player, output) = Open("Fall Asleep");
        player.IsPlaying = false;
        player.SeekCommand.Execute(12 / 45.0);
        void Mode(int ring, string label)
        {
            player.SelectedIndex = ring;
            var editor = player.PlayEditor!;
            editor.ChooseCommand.Execute(editor.Modes.Single(m => m.Label == label));
        }
        Mode(1, "Every");
        Mode(2, "Random");
        Mode(0, "On/off");
        player.PlayEditor!.AddStretchCommand.Execute(null);
        output.Pull(0.1);
        shell.Update();

        Snapshot(shell, "player-modes.png");
    }

    /// <summary>Each built-in scene on its own ground (D12): Deep Work on Paper, Sit and Unwind on Ember, All Day on Sky.</summary>
    [AvaloniaTheory]
    [InlineData("Deep Work", 31 / 60.0, "player-paper.png")]
    [InlineData("Sit", 8 / 20.0, "player-ember.png")]
    [InlineData("Unwind", 13 / 32.0, "player-unwind.png")]
    [InlineData("All Day", 0, "player-sky.png")]
    public void Window_renders_each_scene_on_its_ground(string name, double at, string file)
    {
        var (shell, player, output) = Open(name);
        player.SeekCommand.Execute(at);
        output.Pull(0.2);
        shell.Update();

        Snapshot(shell, file);
    }

    /// <summary>The library with Deep Work playing: its card outlined with the hand on it, the player bar below.</summary>
    [AvaloniaFact]
    public void Window_renders_the_library_with_a_scene_playing()
    {
        var (shell, player, output) = Open("Deep Work");
        player.SeekCommand.Execute(31 / 60.0);
        output.Pull(0.2);
        shell.ShowLibraryCommand.Execute(null);
        shell.Update();

        Assert.True(shell.IsLibraryOpen);
        Assert.True(shell.Library.All.Single(c => c.Name == "Deep Work").IsPlaying);
        Snapshot(shell, "library.png");
    }

    /// <summary>
    /// The library with scenes of the user's own: a copy just made and playing, one being renamed, a changed built-in
    /// and the bar after a delete.
    /// </summary>
    [AvaloniaFact]
    public void Window_renders_the_library_with_your_scenes()
    {
        var (shell, _) = Create();
        shell.OpenCommand.Execute(Card(shell, "Fall Asleep"));
        shell.Player!.ReshapeCommand.Execute(new RingShape(0, Curve.Steady(0.4f)));
        shell.DuplicateCommand.Execute(Card(shell, "Deep Work"));
        shell.DeleteCommand.Execute(Card(shell, "Deep Work 2"));
        shell.DuplicateCommand.Execute(Card(shell, "All Day"));
        shell.StartRenameCommand.Execute(Card(shell, "All Day 2"));
        Card(shell, "All Day 2").EditName = "Desk Day";
        shell.DuplicateCommand.Execute(Card(shell, "Sit"));
        shell.ShowLibraryCommand.Execute(null);
        shell.Update();

        Assert.Equal("Changed", Card(shell, "Fall Asleep").Pill);
        Assert.True(Card(shell, "Sit 2").IsPlaying);
        Snapshot(shell, "library-yours.png");
    }

    /// <summary>The New scene sheet for a sit on Ember, starting from Sit's sounds.</summary>
    [AvaloniaFact]
    public void Window_renders_the_new_scene_sheet()
    {
        var (shell, _) = Create();
        shell.StartNewSceneCommand.Execute(null);
        var sheet = shell.NewScene!;
        sheet.ChooseIntentCommand.Execute(sheet.Intents.Single(i => i.Label == "Meditate"));
        sheet.ChooseStartCommand.Execute(sheet.Starts.Single(s => s.Label == "Sit's sounds"));
        sheet.ChooseLengthCommand.Execute(sheet.Lengths.Single(l => l.Label == "30"));
        sheet.Name = "Evening Sit";

        Snapshot(shell, "new-scene.png");
    }

    /// <summary>D7: ⌘N (Ctrl+N elsewhere) opens the sheet, Escape closes it, and Return on it creates the scene.</summary>
    [AvaloniaFact]
    public void Keys_open_cancel_and_create_a_new_scene()
    {
        var (shell, _) = Create();
        var window = Show(shell);
        var command = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

        window.KeyPress(Key.N, command, PhysicalKey.N, "n");
        Assert.True(shell.IsCreating);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.False(shell.IsCreating);

        window.KeyPress(Key.N, command, PhysicalKey.N, "n");
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Assert.False(shell.IsCreating);
        Assert.Equal("New scene", shell.Player!.Name);
        Close(window, shell);
    }

    /// <summary>D15: Sit's strikes flitting around the left, the right column scrolled to the place panel.</summary>
    [AvaloniaFact]
    public void Window_renders_where_a_ring_is_heard()
    {
        var (shell, player, output) = Open("Sit");
        player.SeekCommand.Execute(8 / 20.0);
        player.SelectedIndex = 0;
        var fire = player.PlaceEditor!;
        fire.Pan = -0.6;
        player.SelectedIndex = 3;
        var editor = player.PlaceEditor!;
        editor.ChooseCommand.Execute(editor.Moves.Single(m => m.Label == "Flits"));
        editor.ChooseCommand.Execute(editor.Widths.Single(w => w.Label == "Narrow"));
        editor.Pan = 0.4;
        output.Pull(0.3);
        shell.Update();

        var window = Show(shell);
        var meter = window.GetVisualDescendants().OfType<PlaceMeter>().Single(m => m.Height == 14);
        meter.FindAncestorOfType<Avalonia.Controls.ScrollViewer>()!.ScrollToEnd();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Save(window, "player-place.png");
        Close(window, shell);
    }

    /// <summary>D17: Fall Asleep waking at 07:00, its morning row in the left panel.</summary>
    [AvaloniaFact]
    public void Window_renders_a_journey_that_wakes()
    {
        var (shell, player, output) = Open("Fall Asleep");
        player.ToggleWakeCommand.Execute(null);
        player.SeekCommand.Execute(24 / 45.0);
        output.Pull(0.2);
        shell.Update();

        Snapshot(shell, "player-wake.png");
    }

    /// <summary>D17: the bedside clock, and the first key only waking it.</summary>
    [AvaloniaFact]
    public void Window_dims_to_the_bedside_clock()
    {
        var (shell, player, output) = Open("Fall Asleep");
        player.SeekCommand.Execute(24 / 45.0);
        output.Pull(0.2);
        Afternoon.Now += TimeSpan.FromSeconds(20);
        shell.Update();
        var window = Show(shell);

        Assert.True(shell.IsBedside);
        Save(window, "bedside.png");

        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        Assert.False(shell.IsBedside);
        Assert.True(player.IsPlaying);
        Afternoon.Now -= TimeSpan.FromSeconds(20);
        Close(window, shell);
    }

    /// <summary>D18: Settings over Sit's dial, on Ember.</summary>
    [AvaloniaFact]
    public void Window_renders_settings()
    {
        var (shell, _, _) = Open("Sit");
        shell.OpenSettingsCommand.Execute(null);

        Snapshot(shell, "settings.png");
    }

    /// <summary>D19: the first launch, and its first hint on the dial.</summary>
    [AvaloniaFact]
    public void Window_renders_the_first_run_and_a_hint()
    {
        var output = new FakeOutput();
        var folder = Path.Combine(Scenes, Guid.NewGuid().ToString("N"));
        var shell = new ShellViewModel(BuiltInScenes.All(), new SceneStore(folder), () => output, Afternoon,
            preferences: new App.Settings.PreferencesStore(Path.Combine(folder, "settings.json")));
        var window = Show(shell);
        Save(window, "first-run.png");

        shell.WelcomeCommand.Execute(shell.Library.All.Single(c => c.Name == "Deep Work"));
        output.Pull(0.1);
        shell.Update();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Save(window, "player-hint.png");
        Close(window, shell);
    }

    /// <summary>D2: the strip on Windows and on Linux, in the library.</summary>
    [AvaloniaTheory]
    [InlineData(TitleStrip.Windows, "strip-windows.png")]
    [InlineData(TitleStrip.Linux, "strip-linux.png")]
    public void Window_renders_the_strip_of_each_system(TitleStrip strip, string file)
    {
        var (shell, _) = Create();
        var window = new MainWindow(shell, strip) { Width = 1280, Height = 800 };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        Assert.Equal(strip != TitleStrip.Linux, window.GetVisualDescendants().OfType<Avalonia.Controls.TextBlock>().Single(t => t.Name == "Brand").IsVisible);
        Save(window, file);
        Close(window, shell);
    }

    /// <summary>D7: ⌘, opens Settings and Escape closes it, before it reaches the dial.</summary>
    [AvaloniaFact]
    public void Keys_open_and_close_settings()
    {
        var (shell, player, _) = Open("Sit");
        var window = Show(shell);
        var command = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

        window.KeyPress(Key.OemComma, command, PhysicalKey.Comma, ",");
        Assert.True(shell.IsSettingsOpen);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);

        Assert.False(shell.IsSettingsOpen);
        Assert.True(shell.IsDialOpen);
        Assert.True(player.IsPlaying);
        Close(window, shell);
    }

    private static SceneCardViewModel Card(ShellViewModel shell, string name) => shell.Library.All.Single(c => c.Name == name);

    private static (ShellViewModel Shell, FakeOutput Output) Create()
    {
        var output = new FakeOutput();
        var store = new SceneStore(Path.Combine(Scenes, Guid.NewGuid().ToString("N")));
        return (new ShellViewModel(BuiltInScenes.All(), store, () => output, Afternoon), output);
    }

    private static (ShellViewModel Shell, PlayerViewModel Player, FakeOutput Output) Open(string name)
    {
        var (shell, output) = Create();
        shell.OpenCommand.Execute(Card(shell, name));
        return (shell, shell.Player!, output);
    }

    private static MainWindow Show(ShellViewModel shell)
    {
        var window = new MainWindow(shell) { Width = 1280, Height = 800 };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return window;
    }

    private static void Snapshot(ShellViewModel shell, string file)
    {
        var window = Show(shell);
        Save(window, file);
        Close(window, shell);
    }

    private static void Save(MainWindow window, string file)
    {
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var folder = Path.Combine(AppContext.BaseDirectory, "snapshots");
        Directory.CreateDirectory(folder);
        frame!.Save(Path.Combine(folder, file), PngBitmapEncoderOptions.Default);
    }

    private static void Close(MainWindow window, ShellViewModel shell)
    {
        window.Close();
        shell.Dispose();
        if (Directory.Exists(Scenes)) Directory.Delete(Scenes, recursive: true);
    }
}
