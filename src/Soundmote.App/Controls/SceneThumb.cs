using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Soundmote.App.Assets;

namespace Soundmote.App.Controls;

/// <summary>
/// A scene as a small dial for the library (D13): its rings as they play, without ticks, badges or face. The hand
/// shows only while the scene is playing.
/// </summary>
public sealed class SceneThumb : Control
{
    public static readonly StyledProperty<IReadOnlyList<DialRing>?> RingsProperty =
        AvaloniaProperty.Register<SceneThumb, IReadOnlyList<DialRing>?>(nameof(Rings));

    public static readonly StyledProperty<double> PositionProperty =
        AvaloniaProperty.Register<SceneThumb, double>(nameof(Position));

    public static readonly StyledProperty<bool> ShowHandProperty =
        AvaloniaProperty.Register<SceneThumb, bool>(nameof(ShowHand));

    public static readonly StyledProperty<IBrush?> GuideBrushProperty =
        AvaloniaProperty.Register<SceneThumb, IBrush?>(nameof(GuideBrush));

    public static readonly StyledProperty<IBrush?> HandBrushProperty =
        AvaloniaProperty.Register<SceneThumb, IBrush?>(nameof(HandBrush));

    public static readonly StyledProperty<double> RingShadeProperty =
        AvaloniaProperty.Register<SceneThumb, double>(nameof(RingShade));

    static SceneThumb()
    {
        AffectsRender<SceneThumb>(RingsProperty, PositionProperty, ShowHandProperty, GuideBrushProperty, HandBrushProperty, RingShadeProperty);
    }

    public IReadOnlyList<DialRing>? Rings
    {
        get => GetValue(RingsProperty);
        set => SetValue(RingsProperty, value);
    }

    public double Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public bool ShowHand
    {
        get => GetValue(ShowHandProperty);
        set => SetValue(ShowHandProperty, value);
    }

    public IBrush? GuideBrush
    {
        get => GetValue(GuideBrushProperty);
        set => SetValue(GuideBrushProperty, value);
    }

    public IBrush? HandBrush
    {
        get => GetValue(HandBrushProperty);
        set => SetValue(HandBrushProperty, value);
    }

    /// <summary>How much darker ring colours are drawn on this ground (D12); set from the theme.</summary>
    public double RingShade
    {
        get => GetValue(RingShadeProperty);
        set => SetValue(RingShadeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var rings = Rings ?? [];
        var size = Math.Min(Bounds.Width, Bounds.Height);
        var layout = new Dial.Layout(new Point(Bounds.Width / 2, Bounds.Height / 2), size / 2 / 0.97, rings.Count);
        var outer = size / 2 - 1;
        if (GuideBrush is { } guide) context.DrawEllipse(null, new Pen(guide, 1), layout.Centre, outer, outer);

        // Not playing, the whole ring is drawn at full strength; playing, the part ahead of the hand is faint.
        var position = ShowHand ? Math.Clamp(Position, 0, 1) : 1;
        for (var i = 0; i < rings.Count; i++)
            Dial.DrawRing(context, layout, layout.RingRadius(i), rings[i], Grounds.Shade(rings[i].Colour, RingShade), position, 1);

        if (ShowHand && HandBrush is { } hand)
            context.DrawLine(new Pen(hand, 1.6, lineCap: PenLineCap.Round), layout.Centre, layout.At(outer - 2, position));
    }
}
