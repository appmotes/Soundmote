using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Soundmote.Core.Scenes;

namespace Soundmote.App.Controls;

/// <summary>
/// What the menu bar icon shows (D16). <see cref="Position"/> is rounded to a 96th of a turn, so two states are equal
/// whenever their drawings are and the icon is redrawn only when it changes.
/// </summary>
public readonly record struct GlyphState(TimeKind? Kind, double Position, double Break, bool Playing)
{
    public static GlyphState Idle { get; } = new(null, 0, 0, false);

    public static GlyphState Of(TimeKind kind, double position, double breakAt, bool playing) =>
        new(kind, Math.Round(position * 96) / 96, breakAt, playing);
}

/// <summary>
/// The menu bar icon as a small dial on the 24 grid (D16): a loop fills through the lap with a dot at its break, a
/// journey shrinks as its time runs out, the clock's sun travels round the day, and a paused scene shows two bars.
/// </summary>
public static class MenuBarGlyph
{
    private const double Radius = 9, Stroke = 2.4;

    /// <summary>The icon as a square bitmap of <paramref name="pixels"/>, in one ink on a clear ground.</summary>
    public static Bitmap Render(GlyphState state, Color ink, int pixels)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(pixels, pixels));
        using (var context = bitmap.CreateDrawingContext())
            Draw(context, state, ink, pixels);
        return bitmap;
    }

    public static void Draw(DrawingContext context, GlyphState state, Color ink, double size)
    {
        var solid = new SolidColorBrush(ink);
        using var _ = context.PushTransform(Matrix.CreateScale(size / 24, size / 24));
        var faint = state.Playing || state.Kind is null ? 0.3 : 0.45;
        context.DrawEllipse(null, new Pen(new SolidColorBrush(ink, faint), Stroke), new Point(12, 12), Radius, Radius);

        if (state.Kind is null)
        {
            context.DrawEllipse(solid, null, new Point(12, 12), 1.6, 1.6);
            return;
        }
        if (!state.Playing)
        {
            context.DrawRectangle(solid, null, new Rect(9, 8.5, 2, 7), 0.6, 0.6);
            context.DrawRectangle(solid, null, new Rect(13, 8.5, 2, 7), 0.6, 0.6);
            return;
        }
        var pen = new Pen(solid, Stroke, lineCap: PenLineCap.Round);
        switch (state.Kind)
        {
            case TimeKind.Loop:
                Arc(context, pen, 0, state.Position);
                context.DrawEllipse(solid, null, new Point(12, 12), 1.6, 1.6);
                if (state.Break > 0) context.DrawEllipse(solid, null, At(state.Break), 1.4, 1.4);
                break;
            case TimeKind.Journey:
                Arc(context, pen, state.Position, 1);
                break;
            default:
                context.DrawEllipse(solid, null, At(state.Position), 2.6, 2.6);
                break;
        }
    }

    private static void Arc(DrawingContext context, Pen pen, double from, double to)
    {
        if (to - from <= 0.004) return;
        if (to - from >= 0.996)
        {
            context.DrawEllipse(null, pen, new Point(12, 12), Radius, Radius);
            return;
        }
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(At(from), false);
            g.ArcTo(At(to), new Size(Radius, Radius), 0, to - from > 0.5, SweepDirection.Clockwise);
            g.EndFigure(false);
        }
        context.DrawGeometry(null, pen, geometry);
    }

    /// <summary>A place on the circle, 0 at the top and clockwise, as on the dial.</summary>
    private static Point At(double turn)
    {
        var angle = (turn * 360 - 90) * Math.PI / 180;
        return new Point(12 + Radius * Math.Cos(angle), 12 + Radius * Math.Sin(angle));
    }
}
