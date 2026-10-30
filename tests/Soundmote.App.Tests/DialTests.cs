using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Soundmote.App.Controls;
using Soundmote.App.ViewModels;
using Soundmote.Core.Scenes;

namespace Soundmote.App.Tests;

public class DialTests
{
    [AvaloniaFact]
    public void Rings_never_reach_the_face_even_when_there_are_many()
    {
        var layout = new Dial.Layout(new Point(300, 300), 270, RingCount: 9);

        var innermost = layout.RingRadius(8) - layout.MaxWidth / 2;
        Assert.True(innermost > layout.FaceRadius, $"{innermost:0.0} ≤ {layout.FaceRadius:0.0}");
        Assert.True(layout.Spacing > layout.MaxWidth, "lanes do not overlap");
    }

    [AvaloniaFact]
    public void Positions_run_clockwise_from_the_top()
    {
        var layout = new Dial.Layout(new Point(100, 100), 80, 3);

        Assert.Equal(0, layout.PositionOf(new Point(100, 10)), 6);
        Assert.Equal(0.25, layout.PositionOf(new Point(190, 100)), 6);
        Assert.Equal(0.5, layout.PositionOf(new Point(100, 190)), 6);
        Assert.Equal(0.75, layout.PositionOf(new Point(10, 100)), 6);
    }

    [AvaloniaFact]
    public void Clicking_a_ring_selects_it_and_pressing_the_track_seeks()
    {
        var player = new PlayerViewModel(BuiltInScenes.FallAsleep(), new FakeOutput());
        var dial = new Dial
        {
            Rings = player.Rings, Length = player.Length,
            [!Dial.SelectedIndexProperty] = new Avalonia.Data.Binding(nameof(PlayerViewModel.SelectedIndex)) { Source = player, Mode = Avalonia.Data.BindingMode.TwoWay },
            SeekCommand = player.SeekCommand
        };
        var window = new Window { Width = 600, Height = 600, Content = dial };
        window.Show();
        var layout = dial.CurrentLayout();

        var choir = layout.At(layout.RingRadius(2), 0.1);
        window.MouseDown(choir, MouseButton.Left);
        window.MouseUp(choir, MouseButton.Left);
        Assert.Equal(2, player.SelectedIndex);

        var track = layout.At(layout.Radius - 4, 0.25);
        window.MouseDown(track, MouseButton.Left);
        window.MouseUp(track, MouseButton.Left);
        Assert.Equal(0.25, player.Position, 2);
        window.Close();
    }

    [AvaloniaFact]
    public void Dragging_a_point_outwards_makes_the_ring_louder_there()
    {
        var player = new PlayerViewModel(BuiltInScenes.FallAsleep(), new FakeOutput()) { SelectedIndex = 1 };
        var dial = new Dial { Length = player.Length, SelectedIndex = 1, ShapeCommand = player.ReshapeCommand };
        dial.Bind(Dial.RingsProperty, new Avalonia.Data.Binding(nameof(PlayerViewModel.Rings)) { Source = player });
        var window = new Window { Width = 600, Height = 600, Content = dial };
        window.Show();
        var layout = dial.CurrentLayout();
        var wind = player.Layers[1];
        var (at, value) = wind.Curve.Points[4];
        var handle = layout.At(layout.RingRadius(1) + value * layout.MaxWidth / 2, at);
        Assert.Equal(4, dial.HandleNear(layout, handle));

        window.MouseDown(handle, MouseButton.Left);
        window.MouseMove(layout.At(layout.RingRadius(1) + 0.9 * layout.MaxWidth / 2, at));
        window.MouseUp(layout.At(layout.RingRadius(1) + 0.9 * layout.MaxWidth / 2, at), MouseButton.Left);

        Assert.Equal(0.9f, wind.Curve.Points[4].Value, 2);
        Assert.Equal(at, wind.Curve.Points[4].At, 2);
        window.Close();
    }

    [AvaloniaFact]
    public void Double_clicking_the_selected_ring_adds_a_point()
    {
        var player = new PlayerViewModel(BuiltInScenes.FallAsleep(), new FakeOutput()) { SelectedIndex = 0 };
        var dial = new Dial { Length = player.Length, ShapeCommand = player.ReshapeCommand };
        dial.Bind(Dial.RingsProperty, new Avalonia.Data.Binding(nameof(PlayerViewModel.Rings)) { Source = player });
        dial.Bind(Dial.SelectedIndexProperty, new Avalonia.Data.Binding(nameof(PlayerViewModel.SelectedIndex)) { Source = player, Mode = Avalonia.Data.BindingMode.TwoWay });
        var window = new Window { Width = 600, Height = 600, Content = dial };
        window.Show();
        var layout = dial.CurrentLayout();
        var before = player.Layers[0].Curve.Points.Count;
        var spot = layout.At(layout.RingRadius(0) - 2, 0.36);

        window.MouseDown(spot, MouseButton.Left);
        window.MouseUp(spot, MouseButton.Left);
        window.MouseDown(spot, MouseButton.Left);
        window.MouseUp(spot, MouseButton.Left);

        Assert.Equal(before + 1, player.Layers[0].Curve.Points.Count);
        window.Close();
    }

    /// <summary>Renders the dial alone into bin/…/snapshots for visual review.</summary>
    [AvaloniaFact]
    public void Dial_renders()
    {
        var output = new FakeOutput();
        var player = new PlayerViewModel(BuiltInScenes.FallAsleep(), output);
        player.SeekCommand.Execute(12.67 / 45);
        output.Pull(0.05);
        player.Update();
        Application.Current!.TryGetResource("Brush.Bg.Ground", ThemeVariant.Dark, out var ground);
        var window = new Window
        {
            Width = 640, Height = 640, Background = ground as Avalonia.Media.IBrush,
            Content = new Dial { Rings = player.Rings, Position = player.Position, Length = player.Length, SelectedIndex = 0 }
        };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var folder = Path.Combine(AppContext.BaseDirectory, "snapshots");
        Directory.CreateDirectory(folder);
        frame!.Save(Path.Combine(folder, "dial.png"), PngBitmapEncoderOptions.Default);
        window.Close();
    }
}
