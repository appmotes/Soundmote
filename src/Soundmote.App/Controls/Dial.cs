using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Soundmote.App.Assets;
using Soundmote.Core.Scenes;
using Stretch = Soundmote.Core.Scenes.Stretch;

namespace Soundmote.App.Controls;

/// <summary>One ring on the dial: the curve it draws, its sound's colour, and whether it is muted.</summary>
public sealed record DialRing(Curve Curve, Color Colour, bool Muted, bool Trial = false)
{
    /// <summary>The sound's glyph, drawn where the ring begins (D10).</summary>
    public Geometry? Icon { get; init; }

    public double IconStroke { get; init; } = 1.8;

    /// <summary>How the ring plays (D11); a shape reads <see cref="Curve"/>.</summary>
    public Play Play { get; init; } = Play.Shape;

    public IReadOnlyList<Strike> Strikes { get; init; } = [];

    /// <summary>A scene's ring as written, with the strikes the renderer will play for it (A11).</summary>
    public static DialRing Of(Scene scene, int index)
    {
        var layer = scene.Layers[index];
        var (icon, stroke) = SoundIcons.For(layer.Recipe);
        return new DialRing(layer.Curve, Color.Parse(layer.Colour), false)
        {
            Icon = icon,
            IconStroke = stroke,
            Play = layer.Play,
            Strikes = layer.Play.Strikes(scene.Length, SceneRenderer.StrikeSeed(scene.Seed, index))
        };
    }
}

/// <summary>A ring's new way to play, sent while the user drags or adds an on/off stretch.</summary>
public sealed record RingPlay(int Index, Play Play);

/// <summary>A ring's new shape, sent while the user drags, adds or removes a point.</summary>
public sealed record RingShape(int Index, Curve Curve);

/// <summary>
/// The scene as a dial (D4). Each ring's width is its curve over the turn; the part behind the hand is solid,
/// the part ahead is faint. Clicking a ring selects it; pressing on the tick track and dragging moves the hand.
/// The selected ring shows its points: drag one to reshape the ring, double-click the ring to add a point and a
/// point to remove it (D8). A ring that plays on and off is drawn as stretches whose ends drag the same way; one that
/// strikes is drawn as dots (D11). All sizes follow the radius, so the dial scales with the window.
/// </summary>
public sealed class Dial : Control
{
    public static readonly StyledProperty<IReadOnlyList<DialRing>?> RingsProperty =
        AvaloniaProperty.Register<Dial, IReadOnlyList<DialRing>?>(nameof(Rings));

    public static readonly StyledProperty<double> PositionProperty =
        AvaloniaProperty.Register<Dial, double>(nameof(Position));

    public static readonly StyledProperty<TimeSpan> LengthProperty =
        AvaloniaProperty.Register<Dial, TimeSpan>(nameof(Length), TimeSpan.FromMinutes(45));

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<Dial, int>(nameof(SelectedIndex), -1, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<ICommand?> SeekCommandProperty =
        AvaloniaProperty.Register<Dial, ICommand?>(nameof(SeekCommand));

    public static readonly StyledProperty<ICommand?> ShapeCommandProperty =
        AvaloniaProperty.Register<Dial, ICommand?>(nameof(ShapeCommand));

    public static readonly StyledProperty<ICommand?> PlayCommandProperty =
        AvaloniaProperty.Register<Dial, ICommand?>(nameof(PlayCommand));

    public static readonly StyledProperty<IBrush?> FaceBrushProperty =
        AvaloniaProperty.Register<Dial, IBrush?>(nameof(FaceBrush));

    public static readonly StyledProperty<IBrush?> GuideBrushProperty =
        AvaloniaProperty.Register<Dial, IBrush?>(nameof(GuideBrush));

    public static readonly StyledProperty<IBrush?> TickBrushProperty =
        AvaloniaProperty.Register<Dial, IBrush?>(nameof(TickBrush));

    public static readonly StyledProperty<IBrush?> HandBrushProperty =
        AvaloniaProperty.Register<Dial, IBrush?>(nameof(HandBrush));

    public static readonly StyledProperty<IBrush?> TipBrushProperty =
        AvaloniaProperty.Register<Dial, IBrush?>(nameof(TipBrush));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<Dial, IBrush?>(nameof(LabelBrush));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<Dial>();

    /// <summary>How much darker ring colours are drawn on this ground (D12); set from the theme.</summary>
    public static readonly StyledProperty<double> RingShadeProperty =
        AvaloniaProperty.Register<Dial, double>(nameof(RingShade));

    public double RingShade
    {
        get => GetValue(RingShadeProperty);
        set => SetValue(RingShadeProperty, value);
    }

    /// <summary>Opacity of the part of a ring still ahead of the hand.</summary>
    public const double FutureOpacity = 0.3;

    private const double MutedOpacity = 0.25;
    private const double OthersOpacity = 0.45;
    private const double LabelSpace = 30;
    private const double HandleReach = 10;
    private const double BadgeAt = 0.975;
    private bool _dragging;
    private int _dragPoint = -1;
    private Curve? _ghost;

    static Dial()
    {
        AffectsRender<Dial>(RingsProperty, PositionProperty, LengthProperty, SelectedIndexProperty, FaceBrushProperty,
            GuideBrushProperty, TickBrushProperty, HandBrushProperty, TipBrushProperty, LabelBrushProperty, FontFamilyProperty, RingShadeProperty);
    }

    public IReadOnlyList<DialRing>? Rings
    {
        get => GetValue(RingsProperty);
        set => SetValue(RingsProperty, value);
    }

    /// <summary>Where the hand is, 0 to 1 clockwise from the top.</summary>
    public double Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    /// <summary>The length of one turn, for the minute labels.</summary>
    public TimeSpan Length
    {
        get => GetValue(LengthProperty);
        set => SetValue(LengthProperty, value);
    }

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>Executed with the position (a <see cref="double"/> from 0 to 1) the user drags the hand to.</summary>
    public ICommand? SeekCommand
    {
        get => GetValue(SeekCommandProperty);
        set => SetValue(SeekCommandProperty, value);
    }

    /// <summary>Executed with a <see cref="RingShape"/> each time the selected ring's curve changes by hand.</summary>
    public ICommand? ShapeCommand
    {
        get => GetValue(ShapeCommandProperty);
        set => SetValue(ShapeCommandProperty, value);
    }

    /// <summary>Executed with a <see cref="RingPlay"/> when on/off stretches are dragged, added or removed (D11).</summary>
    public ICommand? PlayCommand
    {
        get => GetValue(PlayCommandProperty);
        set => SetValue(PlayCommandProperty, value);
    }

    public IBrush? FaceBrush
    {
        get => GetValue(FaceBrushProperty);
        set => SetValue(FaceBrushProperty, value);
    }

    public IBrush? GuideBrush
    {
        get => GetValue(GuideBrushProperty);
        set => SetValue(GuideBrushProperty, value);
    }

    public IBrush? TickBrush
    {
        get => GetValue(TickBrushProperty);
        set => SetValue(TickBrushProperty, value);
    }

    public IBrush? HandBrush
    {
        get => GetValue(HandBrushProperty);
        set => SetValue(HandBrushProperty, value);
    }

    public IBrush? TipBrush
    {
        get => GetValue(TipBrushProperty);
        set => SetValue(TipBrushProperty, value);
    }

    public IBrush? LabelBrush
    {
        get => GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>The dial's proportions for a given size; every radius is a share of the tick radius (D4).</summary>
    internal readonly record struct Layout(Point Centre, double Radius, int RingCount)
    {
        public double FaceRadius => Radius * 0.36;
        public double FirstRing => Radius * 0.842;
        public double MaxWidth => Spacing * 0.78;
        public double TickInner => Radius * 0.95;
        public double TickMinor => Radius * 0.975;

        public double Spacing => RingCount <= 1
            ? Radius * 0.1
            : Math.Min(Radius * 0.1, (FirstRing - FaceRadius - Radius * 0.1) / (RingCount - 1));

        public double RingRadius(int index) => FirstRing - index * Spacing;

        public Point At(double radius, double position)
        {
            var angle = (position * 360 - 90) * Math.PI / 180;
            return new Point(Centre.X + radius * Math.Cos(angle), Centre.Y + radius * Math.Sin(angle));
        }

        /// <summary>The position 0–1 of a point, clockwise from the top.</summary>
        public double PositionOf(Point p)
        {
            var angle = Math.Atan2(p.Y - Centre.Y, p.X - Centre.X) * 180 / Math.PI + 90;
            return (angle + 360) % 360 / 360;
        }
    }

    internal Layout CurrentLayout()
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        return new Layout(new Point(Bounds.Width / 2, Bounds.Height / 2), Math.Max(10, size / 2 - LabelSpace), Rings?.Count ?? 0);
    }

    public override void Render(DrawingContext context)
    {
        var layout = CurrentLayout();
        var rings = Rings ?? [];
        var position = Math.Clamp(Position, 0, 1);

        // Avalonia hit-tests only what is drawn: a transparent fill lets the whole square take the pointer.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

        DrawTicks(context, layout);

        if (GuideBrush is { } guide)
        {
            var pen = new Pen(guide, 1);
            for (var i = 0; i < rings.Count; i++)
                context.DrawEllipse(null, pen, layout.Centre, layout.RingRadius(i), layout.RingRadius(i));
        }

        var selected = SelectedIndex >= 0 && SelectedIndex < rings.Count ? SelectedIndex : -1;
        for (var i = 0; i < rings.Count; i++)
        {
            var ring = rings[i];
            var r = layout.RingRadius(i);
            var opacity = (ring.Muted ? MutedOpacity : 1) * (selected >= 0 && i != selected ? OthersOpacity : 1);
            if (ring.Trial)
            {
                DrawTrial(context, layout, r, ring);
                continue;
            }
            DrawRing(context, layout, r, ring, Shaded(ring), position, opacity);
        }

        for (var i = 0; i < rings.Count; i++) DrawBadge(context, layout, i, rings[i]);

        if (FaceBrush is { } face)
            context.DrawEllipse(face, GuideBrush is { } edge ? new Pen(edge, 1) : null, layout.Centre, layout.FaceRadius, layout.FaceRadius);

        if (HandBrush is { } hand)
        {
            context.DrawLine(new Pen(hand, 2, lineCap: PenLineCap.Round),
                layout.At(layout.FaceRadius + 4, position), layout.At(layout.Radius + 4, position));
            var tip = layout.At(layout.Radius, position);
            context.DrawEllipse(TipBrush ?? hand, null, tip, 6, 6);
        }

        if (selected >= 0) DrawEditing(context, layout, selected, rings[selected]);
    }

    /// <summary>One ring as it plays (D11): a band for a shape, arcs for on/off, dots for strikes. Shared with <see cref="SceneThumb"/>.</summary>
    internal static void DrawRing(DrawingContext context, Layout layout, double radius, DialRing ring, Color colour, double position, double opacity)
    {
        var past = new SolidColorBrush(colour, opacity);
        var future = new SolidColorBrush(colour, FutureOpacity * opacity);
        switch (ring.Play)
        {
            case OnOffPlay onOff:
                DrawStretches(context, layout, radius, onOff, past, future, position);
                break;
            case EveryPlay or RandomPlay:
                if (ring.Play is RandomPlay random)
                    context.DrawGeometry(new SolidColorBrush(colour, 0.12 * opacity), null, Band(layout, radius, random.Busy, 0, 1));
                DrawStrikes(context, layout, radius, ring.Strikes, past, future, position);
                break;
            default:
                if (position > 0) context.DrawGeometry(past, null, Band(layout, radius, ring.Curve, 0, position));
                if (position < 1) context.DrawGeometry(future, null, Band(layout, radius, ring.Curve, position, 1));
                break;
        }
    }

    /// <summary>Each stretch as a thick arc at its level, solid behind the hand and faint ahead (D11).</summary>
    private static void DrawStretches(DrawingContext context, Layout layout, double radius, OnOffPlay play, IBrush past, IBrush future, double position)
    {
        var width = Math.Max(2, play.Level * layout.MaxWidth);
        var pastPen = new Pen(past, width, lineCap: PenLineCap.Round);
        var futurePen = new Pen(future, width, lineCap: PenLineCap.Round);
        foreach (var s in play.Stretches)
        {
            if (s.From < position) context.DrawGeometry(null, pastPen, Arc(layout, radius, s.From, Math.Min(s.To, position)));
            if (s.To > position) context.DrawGeometry(null, futurePen, Arc(layout, radius, Math.Max(s.From, position), s.To));
        }
    }

    /// <summary>Each strike as a dot sized by its strength: filled once the hand has passed it, a ring before (D11).</summary>
    private static void DrawStrikes(DrawingContext context, Layout layout, double radius, IReadOnlyList<Strike> strikes, IBrush past, IBrush future, double position)
    {
        var scale = layout.Spacing / 28;
        var outline = new Pen(future, 1.6);
        foreach (var strike in strikes)
        {
            var r = (2.5 + 4.5 * strike.Strength) * scale;
            var at = layout.At(radius, strike.At);
            if (strike.At < position) context.DrawEllipse(past, null, at, r, r);
            else context.DrawEllipse(null, outline, at, r, r);
        }
    }

    private static StreamGeometry Arc(Layout layout, double radius, double from, double to)
    {
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        var steps = Math.Max(2, (int)Math.Ceiling((to - from) * 360));
        for (var i = 0; i <= steps; i++)
        {
            var point = layout.At(radius, from + (to - from) * i / steps);
            if (i == 0) g.BeginFigure(point, false);
            else g.LineTo(point);
        }
        g.EndFigure(false);
        return geometry;
    }

    /// <summary>The ring's glyph in a small disc just before the top, where the ring begins (D10).</summary>
    private void DrawBadge(DrawingContext context, Layout layout, int index, DialRing ring)
    {
        if (ring.Icon is not { } icon) return;
        var radius = Math.Min(13, layout.Spacing * 0.48);
        var centre = layout.At(layout.RingRadius(index), BadgeAt);
        var colour = new SolidColorBrush(Shaded(ring), ring.Muted ? MutedOpacity : 1);
        context.DrawEllipse(FaceBrush, new Pen(colour, 1.5), centre, radius, radius);
        SoundBadge.Draw(context, icon, ring.IconStroke, colour, centre, radius * 1.25);
    }

    /// <summary>A sound on trial (D9): its whole shape faint, edged with a dashed line in its colour.</summary>
    private void DrawTrial(DrawingContext context, Layout layout, double radius, DialRing ring)
    {
        context.DrawGeometry(new SolidColorBrush(Shaded(ring), 0.22), null, Band(layout, radius, ring.Curve, 0, 1));
        var pen = new Pen(new SolidColorBrush(Shaded(ring)), 1.5, new DashStyle([2, 2.7], 0));
        context.DrawGeometry(null, pen, Edge(layout, radius, ring.Curve, 1));
        context.DrawGeometry(null, pen, Edge(layout, radius, ring.Curve, -1));
    }

    /// <summary>The selected ring's points, the shape before the drag, and the value being dragged (D8).</summary>
    private void DrawEditing(DrawingContext context, Layout layout, int index, DialRing ring)
    {
        if (HandBrush is not { } ink) return;
        var r = layout.RingRadius(index);

        if (ring.Play is OnOffPlay onOff)
        {
            var handles = StretchEnds(onOff);
            for (var i = 0; i < handles.Length; i++)
            {
                var at = layout.At(r, handles[i]);
                if (i == _dragPoint) context.DrawEllipse(new SolidColorBrush(Shaded(ring)), new Pen(ink, 2.5), at, 8, 8);
                else context.DrawEllipse(FaceBrush, new Pen(ink, 2), at, 5, 5);
            }
            if (_dragPoint >= 0 && _dragPoint < handles.Length)
                DrawTip(context, layout.At(r, handles[_dragPoint]), Formatting.SceneText.Clock(Length * handles[_dragPoint]), ink);
            return;
        }
        if (ring.Play is not ShapePlay) return;

        if (_ghost is { } ghost)
        {
            using (context.PushOpacity(0.55))
                context.DrawGeometry(null, new Pen(ink, 1.4, new DashStyle([1.5, 2.8], 0)), Edge(layout, r, ghost));
        }
        else
            context.DrawGeometry(null, new Pen(new SolidColorBrush(Shaded(ring), 0.9), 1.2), Edge(layout, r, ring.Curve));

        var points = ring.Curve.Points;
        for (var i = 0; i < points.Count; i++)
        {
            var at = HandleAt(layout, r, points[i]);
            if (i == _dragPoint)
            {
                context.DrawEllipse(new SolidColorBrush(Shaded(ring), 0.22), null, at, 16, 16);
                context.DrawEllipse(new SolidColorBrush(Shaded(ring)), new Pen(ink, 2.5), at, 8, 8);
            }
            else
            {
                context.DrawEllipse(FaceBrush, new Pen(ink, 2), at, 5, 5);
            }
        }

        if (_dragPoint >= 0 && _dragPoint < points.Count)
            DrawTip(context, HandleAt(layout, r, points[_dragPoint]),
                $"{Formatting.SceneText.Clock(Length * points[_dragPoint].At)} · {Math.Round(points[_dragPoint].Value * 100):0} %", ink);
    }

    /// <summary>Every stretch's start and end in turn: handle 2k is stretch k's start, 2k+1 its end.</summary>
    private static double[] StretchEnds(OnOffPlay play) => play.Stretches.SelectMany(s => new[] { s.From, s.To }).ToArray();

    private void DrawTip(DrawingContext context, Point handle, string text, IBrush ink)
    {
        var label = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle.Normal, FontWeight.Bold), 12.5, FaceBrush ?? Brushes.Black);
        var box = new Rect(handle.X - label.Width / 2 - 10, handle.Y - label.Height - 34, label.Width + 20, label.Height + 12);
        context.DrawRectangle(ink, null, box, 8, 8);
        context.DrawText(label, new Point(box.X + 10, box.Y + 6));
    }

    private Color Shaded(DialRing ring) => Grounds.Shade(ring.Colour, RingShade);

    private static Point HandleAt(Layout layout, double radius, (double At, float Value) point) =>
        layout.At(radius + Math.Max(0.8, point.Value * layout.MaxWidth) / 2, point.At);

    private void DrawTicks(DrawingContext context, Layout layout)
    {
        var minutes = (int)Math.Round(Length.TotalMinutes);
        if (minutes <= 0) return;
        var step = minutes <= 60 ? 1 : minutes <= 240 ? 5 : 60;
        var labelEvery = minutes <= 60 ? 5 : minutes <= 240 ? 30 : 180;

        if (TickBrush is { } tickBrush)
        {
            var pen = new Pen(tickBrush, 1.2, lineCap: PenLineCap.Round);
            for (var m = 0; m < minutes; m += step)
            {
                var p = (double)m / minutes;
                var inner = m % labelEvery == 0 ? layout.TickInner : layout.TickMinor;
                context.DrawLine(pen, layout.At(inner, p), layout.At(layout.Radius, p));
            }
        }

        if (LabelBrush is not { } labelBrush) return;
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight.SemiBold);
        for (var m = 0; m < minutes; m += labelEvery)
        {
            var text = minutes > 240 ? $"{m / 60:00}" : m.ToString(CultureInfo.InvariantCulture);
            var label = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 10.5, labelBrush);
            var at = layout.At(layout.Radius + 16, (double)m / minutes);
            context.DrawText(label, new Point(at.X - label.Width / 2, at.Y - label.Height / 2));
        }
    }

    private static StreamGeometry Band(Layout layout, double radius, Curve curve, double from, double to)
    {
        var steps = Math.Max(2, (int)Math.Ceiling((to - from) * 360));
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        for (var i = 0; i <= steps; i++)
        {
            var p = from + (to - from) * i / steps;
            var point = layout.At(radius + HalfWidth(layout, curve, p), p);
            if (i == 0) g.BeginFigure(point, true);
            else g.LineTo(point);
        }
        for (var i = steps; i >= 0; i--)
        {
            var p = from + (to - from) * i / steps;
            g.LineTo(layout.At(radius - HalfWidth(layout, curve, p), p));
        }
        g.EndFigure(true);
        return geometry;
    }

    /// <summary>The ring's outer edge, or its inner edge with <paramref name="side"/> -1.</summary>
    private static StreamGeometry Edge(Layout layout, double radius, Curve curve, int side = 1)
    {
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        for (var i = 0; i <= 360; i++)
        {
            var p = i / 360.0;
            var point = layout.At(radius + side * HalfWidth(layout, curve, p), p);
            if (i == 0) g.BeginFigure(point, false);
            else g.LineTo(point);
        }
        g.EndFigure(false);
        return geometry;
    }

    private static double HalfWidth(Layout layout, Curve curve, double p) => Math.Max(0.8, curve.At(p) * layout.MaxWidth) / 2;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var layout = CurrentLayout();
        var point = e.GetPosition(this);
        var distance = Distance(point, layout.Centre);

        if (SelectedRing() is { } selected && HandleNear(layout, point) is var handle and >= 0)
        {
            if (e.ClickCount == 2)
            {
                if (selected.Play is OnOffPlay onOff)
                    Replay(onOff with { Stretches = onOff.Stretches.Where((_, i) => i != handle / 2).ToArray() });
                else
                    Reshape(selected.Curve.WithoutPoint(handle));
            }
            else
            {
                _dragPoint = handle;
                _ghost = selected.Play is ShapePlay ? selected.Curve : null;
                e.Pointer.Capture(this);
                InvalidateVisual();
            }
            e.Handled = true;
            return;
        }

        if (OnTrack(layout, distance))
        {
            _dragging = true;
            e.Pointer.Capture(this);
            Seek(layout.PositionOf(point));
            e.Handled = true;
            return;
        }

        var ring = RingAt(layout, distance);
        if (ring >= 0 && ring == SelectedIndex && e.ClickCount == 2 && SelectedRing() is { } current)
        {
            var at = layout.PositionOf(point);
            if (current.Play is OnOffPlay onOff) AddStretch(onOff, at);
            else if (current.Play is ShapePlay) Reshape(current.Curve.WithPointAdded(at, current.Curve.At(at)));
        }
        SelectedIndex = ring;
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var layout = CurrentLayout();
        var point = e.GetPosition(this);
        if (_dragging)
        {
            Seek(layout.PositionOf(point));
            return;
        }
        if (_dragPoint >= 0 && SelectedRing() is { Play: OnOffPlay stretches })
        {
            Replay(MoveEnd(stretches, _dragPoint, layout.PositionOf(point)));
            return;
        }
        if (_dragPoint >= 0 && SelectedRing() is { } ring)
        {
            // The pointer's distance from the ring is the value: its outer edge sits at radius + amount × width / 2.
            var value = (float)(2 * (Distance(point, layout.Centre) - layout.RingRadius(SelectedIndex)) / layout.MaxWidth);
            Reshape(ring.Curve.WithPointMoved(_dragPoint, layout.PositionOf(point), value));
            return;
        }
        var distance = Distance(point, layout.Centre);
        Cursor = HandleNear(layout, point) >= 0 || OnTrack(layout, distance) || RingAt(layout, distance) >= 0
            ? new Cursor(StandardCursorType.Hand)
            : Cursor.Default;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging && _dragPoint < 0) return;
        _dragging = false;
        _dragPoint = -1;
        _ghost = null;
        e.Pointer.Capture(null);
        InvalidateVisual();
    }

    private DialRing? SelectedRing() =>
        Rings is { } rings && SelectedIndex >= 0 && SelectedIndex < rings.Count ? rings[SelectedIndex] : null;

    /// <summary>The selected ring's point or stretch end under the pointer, or -1.</summary>
    internal int HandleNear(Layout layout, Point point)
    {
        if (SelectedRing() is not { } ring) return -1;
        var r = layout.RingRadius(SelectedIndex);
        if (ring.Play is OnOffPlay onOff)
        {
            var ends = StretchEnds(onOff);
            for (var i = 0; i < ends.Length; i++)
                if (Distance(layout.At(r, ends[i]), point) <= HandleReach)
                    return i;
            return -1;
        }
        if (ring.Play is not ShapePlay) return -1;
        var points = ring.Curve.Points;
        for (var i = 0; i < points.Count; i++)
            if (Distance(HandleAt(layout, r, points[i]), point) <= HandleReach)
                return i;
        return -1;
    }

    private bool OnTrack(Layout layout, double distance) =>
        distance > layout.TickInner - layout.Spacing * 0.3 && distance < layout.Radius + LabelSpace;

    /// <summary>Moves one stretch end, keeping the stretch at least a minute's worth long and clear of its neighbours.</summary>
    private OnOffPlay MoveEnd(OnOffPlay play, int handle, double at)
    {
        var minimum = Math.Max(Curve.MinGap, 60 / Math.Max(60, Length.TotalSeconds));
        var stretches = play.Stretches.ToArray();
        var k = handle / 2;
        var s = stretches[k];
        if (handle % 2 == 0)
        {
            var low = k == 0 ? 0 : stretches[k - 1].To + Curve.MinGap;
            stretches[k] = s with { From = Math.Clamp(at, low, s.To - minimum) };
        }
        else
        {
            var high = k == stretches.Length - 1 ? 1 : stretches[k + 1].From - Curve.MinGap;
            stretches[k] = s with { To = Math.Clamp(at, s.From + minimum, high) };
        }
        return play with { Stretches = stretches };
    }

    /// <summary>A five-minute stretch centred where the ring was double-clicked, if it fits there.</summary>
    private void AddStretch(OnOffPlay play, double at)
    {
        var half = Math.Min(0.25, 150 / Math.Max(300, Length.TotalSeconds));
        var from = Math.Clamp(at - half, 0, 1 - 2 * half);
        var added = new Stretch(from, from + 2 * half);
        if (play.Stretches.Any(s => added.From < s.To + Curve.MinGap && added.To > s.From - Curve.MinGap)) return;
        Replay(play with { Stretches = play.Stretches.Append(added).OrderBy(s => s.From).ToArray() });
    }

    private void Replay(Play play)
    {
        if (SelectedIndex < 0 || PlayCommand is not { } command) return;
        var change = new RingPlay(SelectedIndex, play);
        if (command.CanExecute(change)) command.Execute(change);
    }

    private void Reshape(Curve curve)
    {
        if (SelectedIndex < 0 || ShapeCommand is not { } command) return;
        var shape = new RingShape(SelectedIndex, curve);
        if (command.CanExecute(shape)) command.Execute(shape);
    }

    /// <summary>The ring whose lane holds the distance from the centre, or -1.</summary>
    internal int RingAt(Layout layout, double distance)
    {
        var count = Rings?.Count ?? 0;
        for (var i = 0; i < count; i++)
            if (Math.Abs(distance - layout.RingRadius(i)) <= layout.Spacing / 2)
                return i;
        return -1;
    }

    private void Seek(double position)
    {
        if (SeekCommand is { } command && command.CanExecute(position))
            command.Execute(position);
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
