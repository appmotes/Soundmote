using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Soundmote.App.Assets;

namespace Soundmote.App.Controls;

/// <summary>
/// Where a ring is heard now (D15): a line from left to right, a band as wide as the sound is spread and a dot where it
/// sits, in the sound's colour. Small in a row, larger in the ring's panel.
/// </summary>
public sealed class PlaceMeter : Control
{
    public static readonly StyledProperty<double> PanProperty =
        AvaloniaProperty.Register<PlaceMeter, double>(nameof(Pan));

    public static readonly StyledProperty<double> SpreadProperty =
        AvaloniaProperty.Register<PlaceMeter, double>(nameof(Spread), 1);

    public static readonly StyledProperty<Color> ColourProperty =
        AvaloniaProperty.Register<PlaceMeter, Color>(nameof(Colour), Colors.White);

    public static readonly StyledProperty<IBrush?> GuideBrushProperty =
        AvaloniaProperty.Register<PlaceMeter, IBrush?>(nameof(GuideBrush));

    /// <summary>How much darker ring colours are drawn on this ground (D12); set from the theme.</summary>
    public static readonly StyledProperty<double> RingShadeProperty =
        AvaloniaProperty.Register<PlaceMeter, double>(nameof(RingShade));

    static PlaceMeter() =>
        AffectsRender<PlaceMeter>(PanProperty, SpreadProperty, ColourProperty, GuideBrushProperty, RingShadeProperty);

    /// <summary>-1 left to 1 right.</summary>
    public double Pan
    {
        get => GetValue(PanProperty);
        set => SetValue(PanProperty, value);
    }

    /// <summary>The place's width: 0 a point, 1 as wide as the sound.</summary>
    public double Spread
    {
        get => GetValue(SpreadProperty);
        set => SetValue(SpreadProperty, value);
    }

    public Color Colour
    {
        get => GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    public IBrush? GuideBrush
    {
        get => GetValue(GuideBrushProperty);
        set => SetValue(GuideBrushProperty, value);
    }

    public double RingShade
    {
        get => GetValue(RingShadeProperty);
        set => SetValue(RingShadeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var height = Bounds.Height;
        var dot = Math.Min(height, 10) / 2;
        var left = dot;
        var span = Math.Max(1, Bounds.Width - dot * 2);
        var y = height / 2;
        var colour = Grounds.Shade(Colour, RingShade);
        var line = Math.Max(1.5, height / 6);
        context.DrawRectangle(GuideBrush, null, new Rect(0, y - line / 2, Bounds.Width, line), line / 2, line / 2);

        var x = left + (Math.Clamp(Pan, -1, 1) + 1) / 2 * span;
        // A full spread covers half the line around the dot; a point covers none.
        var half = Math.Clamp(Spread, 0, 1) * span / 4;
        if (half > 0.5)
        {
            var from = Math.Max(0, x - half);
            var to = Math.Min(Bounds.Width, x + half);
            context.DrawRectangle(new SolidColorBrush(colour, 0.35), null, new Rect(from, y - dot * 0.6, to - from, dot * 1.2), dot * 0.6, dot * 0.6);
        }
        context.DrawEllipse(new SolidColorBrush(colour), null, new Point(x, y), dot, dot);
    }
}
