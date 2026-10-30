using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Soundmote.App.Controls;
using Soundmote.Core.Scenes;

namespace Soundmote.Desktop.Tests;

/// <summary>D16: the menu bar icon in each state, black on a light bar and white on a dark one, into snapshots for review.</summary>
public class MenuBarGlyphTests
{
    private const int Cell = 96;

    private static readonly GlyphState[] States =
    [
        GlyphState.Idle,
        GlyphState.Of(TimeKind.Loop, 31.3 / 60, 50 / 60.0, true),
        GlyphState.Of(TimeKind.Journey, 12.7 / 45, 0, true),
        GlyphState.Of(TimeKind.Clock, 14.53 / 24, 0, true),
        GlyphState.Of(TimeKind.Loop, 31.3 / 60, 50 / 60.0, false)
    ];

    [AvaloniaFact]
    public void Every_state_draws_on_a_light_and_a_dark_bar()
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(Cell * States.Length, Cell * 2));
        using (var context = bitmap.CreateDrawingContext())
        {
            foreach (var (ground, ink, row) in new[] { (Color.Parse("#ECECEC"), Colors.Black, 0), (Color.Parse("#2A2A2E"), Colors.White, 1) })
            {
                context.DrawRectangle(new SolidColorBrush(ground), null, new Rect(0, row * Cell, Cell * States.Length, Cell));
                for (var i = 0; i < States.Length; i++)
                    using (context.PushTransform(Matrix.CreateTranslation(i * Cell + 12, row * Cell + 12)))
                        MenuBarGlyph.Draw(context, States[i], ink, Cell - 24);
            }
        }
        var folder = Path.Combine(AppContext.BaseDirectory, "snapshots");
        Directory.CreateDirectory(folder);
        bitmap.Save(Path.Combine(folder, "menubar-glyphs.png"), PngBitmapEncoderOptions.Default);

        Assert.Equal(5, States.Distinct().Count());
        Assert.Equal(GlyphState.Of(TimeKind.Loop, 0.5001, 0, true), GlyphState.Of(TimeKind.Loop, 0.4999, 0, true));
    }

    [AvaloniaFact]
    public void The_icon_is_a_bitmap_of_the_asked_size()
    {
        using var bitmap = MenuBarGlyph.Render(States[1], Colors.Black, 44);

        Assert.Equal(new PixelSize(44, 44), bitmap.PixelSize);
    }
}
