using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Soundmote.App.Assets;

namespace Soundmote.App.Controls;

/// <summary>
/// A sound's glyph on a rounded square tinted with its colour (D10). <see cref="Size"/> is the square; the glyph
/// takes 58 % of it and keeps a steady stroke at any size.
/// </summary>
public sealed class SoundBadge : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<SoundBadge, Geometry?>(nameof(Data));

    public static readonly StyledProperty<double> StrokeProperty =
        AvaloniaProperty.Register<SoundBadge, double>(nameof(Stroke), 1.8);

    public static readonly StyledProperty<Color> ColourProperty =
        AvaloniaProperty.Register<SoundBadge, Color>(nameof(Colour), Colors.White);

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<SoundBadge, double>(nameof(Size), 28);

    /// <summary>How much darker ring colours are drawn on this ground (D12); set from the theme.</summary>
    public static readonly StyledProperty<double> RingShadeProperty =
        AvaloniaProperty.Register<SoundBadge, double>(nameof(RingShade));

    public double RingShade
    {
        get => GetValue(RingShadeProperty);
        set => SetValue(RingShadeProperty, value);
    }

    static SoundBadge()
    {
        AffectsRender<SoundBadge>(DataProperty, StrokeProperty, ColourProperty, RingShadeProperty);
        AffectsMeasure<SoundBadge>(SizeProperty);
    }

    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>Stroke width on the 24 grid.</summary>
    public double Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Color Colour
    {
        get => GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        var size = Size;
        var colour = Grounds.Shade(Colour, RingShade);
        context.DrawRectangle(new SolidColorBrush(colour, 0.16), null, new Rect(0, 0, size, size), size * 0.32, size * 0.32);
        if (Data is not { } data) return;
        Draw(context, data, Stroke, new SolidColorBrush(colour), new Point(size / 2, size / 2), size * 0.58);
    }

    /// <summary>Strokes a 24-grid glyph centred on <paramref name="centre"/> at <paramref name="size"/>; shared with the dial.</summary>
    public static void Draw(DrawingContext context, Geometry glyph, double stroke, IBrush brush, Point centre, double size)
    {
        var scale = size / 24;
        var pen = new Pen(brush, stroke * Math.Max(scale, 0.75) / scale, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        using (context.PushTransform(Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(centre.X, centre.Y)))
            context.DrawGeometry(null, pen, glyph);
    }
}
