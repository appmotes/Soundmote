using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Soundmote.App.Controls;

/// <summary>A stroked glyph on the 24 grid, drawn in the inherited foreground at the control's size (D10).</summary>
public sealed class Glyph : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<Glyph, Geometry?>(nameof(Data));

    public static readonly StyledProperty<double> StrokeProperty =
        AvaloniaProperty.Register<Glyph, double>(nameof(Stroke), 2.2);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<Glyph>();

    static Glyph()
    {
        AffectsRender<Glyph>(DataProperty, StrokeProperty, ForegroundProperty);
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

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Data is not { } data || Foreground is not { } brush) return;
        var size = Math.Min(Bounds.Width, Bounds.Height);
        SoundBadge.Draw(context, data, Stroke, brush, new Point(Bounds.Width / 2, Bounds.Height / 2), size);
    }
}
