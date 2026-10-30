using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Soundmote.App.Assets;
using Soundmote.Core.Scenes;

namespace Soundmote.App.Controls;

/// <summary>
/// A ring's curve laid flat, for precise shaping (D8): time left to right, amount bottom to top. Points are edited
/// as on the dial: drag one, double-click empty space to add, double-click a point to remove it.
/// </summary>
public sealed class CurveEditor : Control
{
    public static readonly StyledProperty<Curve?> CurveProperty =
        AvaloniaProperty.Register<CurveEditor, Curve?>(nameof(Curve));

    public static readonly StyledProperty<Color> ColourProperty =
        AvaloniaProperty.Register<CurveEditor, Color>(nameof(Colour), Colors.White);

    public static readonly StyledProperty<double> PositionProperty =
        AvaloniaProperty.Register<CurveEditor, double>(nameof(Position));

    public static readonly StyledProperty<TimeSpan> LengthProperty =
        AvaloniaProperty.Register<CurveEditor, TimeSpan>(nameof(Length), TimeSpan.FromMinutes(45));

    public static readonly StyledProperty<IReadOnlyList<double>?> MarksProperty =
        AvaloniaProperty.Register<CurveEditor, IReadOnlyList<double>?>(nameof(Marks));

    public static readonly StyledProperty<ICommand?> ChangeCommandProperty =
        AvaloniaProperty.Register<CurveEditor, ICommand?>(nameof(ChangeCommand));

    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<CurveEditor, IBrush?>(nameof(GridBrush));

    public static readonly StyledProperty<IBrush?> InkBrushProperty =
        AvaloniaProperty.Register<CurveEditor, IBrush?>(nameof(InkBrush));

    public static readonly StyledProperty<IBrush?> FaceBrushProperty =
        AvaloniaProperty.Register<CurveEditor, IBrush?>(nameof(FaceBrush));

    public static readonly StyledProperty<IBrush?> PlayheadBrushProperty =
        AvaloniaProperty.Register<CurveEditor, IBrush?>(nameof(PlayheadBrush));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<CurveEditor, IBrush?>(nameof(LabelBrush));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<CurveEditor>();

    private const double Side = 6;
    private const double Top = 8;
    private const double Bottom = 18;
    private const double HandleReach = 9;
    private int _dragPoint = -1;
    private Curve? _ghost;

    /// <summary>How much darker ring colours are drawn on this ground (D12); set from the theme.</summary>
    public static readonly StyledProperty<double> RingShadeProperty =
        AvaloniaProperty.Register<CurveEditor, double>(nameof(RingShade));

    public double RingShade
    {
        get => GetValue(RingShadeProperty);
        set => SetValue(RingShadeProperty, value);
    }

    private Color Shaded => Grounds.Shade(Colour, RingShade);

    static CurveEditor()
    {
        AffectsRender<CurveEditor>(CurveProperty, ColourProperty, RingShadeProperty, PositionProperty, LengthProperty, MarksProperty,
            GridBrushProperty, InkBrushProperty, FaceBrushProperty, PlayheadBrushProperty, LabelBrushProperty);
    }

    public Curve? Curve
    {
        get => GetValue(CurveProperty);
        set => SetValue(CurveProperty, value);
    }

    public Color Colour
    {
        get => GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    /// <summary>The hand's place, drawn as a playhead.</summary>
    public double Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public TimeSpan Length
    {
        get => GetValue(LengthProperty);
        set => SetValue(LengthProperty, value);
    }

    /// <summary>Positions of phase boundaries, drawn as faint lines.</summary>
    public IReadOnlyList<double>? Marks
    {
        get => GetValue(MarksProperty);
        set => SetValue(MarksProperty, value);
    }

    /// <summary>Executed with the new <see cref="Soundmote.Core.Scenes.Curve"/> on every edit.</summary>
    public ICommand? ChangeCommand
    {
        get => GetValue(ChangeCommandProperty);
        set => SetValue(ChangeCommandProperty, value);
    }

    public IBrush? GridBrush
    {
        get => GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public IBrush? InkBrush
    {
        get => GetValue(InkBrushProperty);
        set => SetValue(InkBrushProperty, value);
    }

    public IBrush? FaceBrush
    {
        get => GetValue(FaceBrushProperty);
        set => SetValue(FaceBrushProperty, value);
    }

    public IBrush? PlayheadBrush
    {
        get => GetValue(PlayheadBrushProperty);
        set => SetValue(PlayheadBrushProperty, value);
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

    private Rect Plot => new(Side, Top, Math.Max(1, Bounds.Width - 2 * Side), Math.Max(1, Bounds.Height - Top - Bottom));

    internal Point ToPoint(double at, float value)
    {
        var plot = Plot;
        return new Point(plot.X + at * plot.Width, plot.Y + (1 - value) * plot.Height);
    }

    private (double At, float Value) FromPoint(Point p)
    {
        var plot = Plot;
        return (Math.Clamp((p.X - plot.X) / plot.Width, 0, 1), (float)Math.Clamp(1 - (p.Y - plot.Y) / plot.Height, 0, 1));
    }

    public override void Render(DrawingContext context)
    {
        var plot = Plot;
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (GridBrush is { } grid)
        {
            context.DrawRectangle(grid, null, plot, 6, 6);
            var pen = new Pen(grid, 1, new DashStyle([2, 4], 0));
            foreach (var mark in Marks ?? [])
                context.DrawLine(pen, ToPoint(mark, 1), ToPoint(mark, 0));
        }
        if (Curve is not { } curve) return;

        if (_ghost is { } ghost && InkBrush is { } ghostInk)
        {
            using (context.PushOpacity(0.55))
                context.DrawGeometry(null, new Pen(ghostInk, 1.3, new DashStyle([1.5, 3], 0)), Line(ghost, closed: false));
        }
        context.DrawGeometry(new SolidColorBrush(Shaded, 0.45), null, Line(curve, closed: true));
        context.DrawGeometry(null, new Pen(new SolidColorBrush(Shaded), 2), Line(curve, closed: false));

        if (PlayheadBrush is { } playhead)
            context.DrawLine(new Pen(playhead, 1.5), ToPoint(Position, 1), ToPoint(Position, 0));

        if (InkBrush is { } ink)
        {
            var points = curve.Points;
            for (var i = 0; i < points.Count; i++)
            {
                var p = ToPoint(points[i].At, points[i].Value);
                if (i == _dragPoint) context.DrawEllipse(new SolidColorBrush(Shaded), new Pen(ink, 2), p, 5.5, 5.5);
                else context.DrawEllipse(FaceBrush, new Pen(ink, 1.6), p, 3.5, 3.5);
            }
        }

        if (LabelBrush is { } label)
        {
            var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight.SemiBold);
            var start = new FormattedText("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 9.5, label);
            var end = new FormattedText($"{Length.TotalMinutes:0} min", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 9.5, label);
            context.DrawText(start, new Point(plot.X, plot.Bottom + 3));
            context.DrawText(end, new Point(plot.Right - end.Width, plot.Bottom + 3));
        }
    }

    private StreamGeometry Line(Curve curve, bool closed)
    {
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        const int steps = 160;
        for (var i = 0; i <= steps; i++)
        {
            var at = (double)i / steps;
            var p = ToPoint(at, curve.At(at));
            if (i == 0) g.BeginFigure(closed ? ToPoint(0, 0) : p, closed);
            if (i > 0 || closed) g.LineTo(p);
        }
        if (closed) g.LineTo(ToPoint(1, 0));
        g.EndFigure(closed);
        return geometry;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Curve is not { } curve) return;
        var point = e.GetPosition(this);
        var handle = HandleNear(curve, point);
        if (handle >= 0)
        {
            if (e.ClickCount == 2) Change(curve.WithoutPoint(handle));
            else
            {
                _dragPoint = handle;
                _ghost = curve;
                e.Pointer.Capture(this);
                InvalidateVisual();
            }
        }
        else if (e.ClickCount == 2)
        {
            var (at, value) = FromPoint(point);
            Change(curve.WithPointAdded(at, value));
        }
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Curve is not { } curve) return;
        var point = e.GetPosition(this);
        if (_dragPoint >= 0)
        {
            var (at, value) = FromPoint(point);
            Change(curve.WithPointMoved(_dragPoint, at, value));
            return;
        }
        Cursor = HandleNear(curve, point) >= 0 ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragPoint < 0) return;
        _dragPoint = -1;
        _ghost = null;
        e.Pointer.Capture(null);
        InvalidateVisual();
    }

    private int HandleNear(Curve curve, Point point)
    {
        var points = curve.Points;
        for (var i = 0; i < points.Count; i++)
        {
            var p = ToPoint(points[i].At, points[i].Value);
            if (Math.Abs(p.X - point.X) <= HandleReach && Math.Abs(p.Y - point.Y) <= HandleReach) return i;
        }
        return -1;
    }

    private void Change(Curve curve)
    {
        if (ChangeCommand is { } command && command.CanExecute(curve)) command.Execute(curve);
    }
}
