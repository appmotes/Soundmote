namespace Soundmote.Core.Scenes;

/// <summary>
/// A ring's shape: amount from 0 to 1 over one turn of the dial, position 0 to 1. Points are joined by a
/// cosine, so the curve is flat at every point and never overshoots.
/// </summary>
public sealed class Curve
{
    private readonly (double At, float Value)[] _points;

    public Curve(params (double At, float Value)[] points)
    {
        if (points.Length == 0) throw new ArgumentException("A curve needs at least one point.", nameof(points));
        _points = points.OrderBy(p => p.At).Select(p => (Math.Clamp(p.At, 0, 1), Math.Clamp(p.Value, 0f, 1f))).ToArray();
    }

    /// <summary>Values spaced evenly over the turn, first at 0 and last at 1, as drawn on the dial boards.</summary>
    public static Curve Even(params float[] values) =>
        values.Length == 1
            ? new Curve((0, values[0]))
            : new Curve(values.Select((v, i) => ((double)i / (values.Length - 1), v)).ToArray());

    public static Curve Steady(float value) => new((0, value));

    public IReadOnlyList<(double At, float Value)> Points => _points;

    /// <summary>The closest two points may come in time when one is moved or added.</summary>
    public const double MinGap = 0.01;

    /// <summary>
    /// A copy with one point moved. It stays between its neighbours, and the first and last point keep their place
    /// in time, so the curve always covers the same stretch of the turn.
    /// </summary>
    public Curve WithPointMoved(int index, double at, float value)
    {
        var points = _points.ToArray();
        var last = points.Length - 1;
        var time = index == 0 || index == last
            ? points[index].At
            : Math.Clamp(at, points[index - 1].At + MinGap, points[index + 1].At - MinGap);
        points[index] = (time, Math.Clamp(value, 0f, 1f));
        return new Curve(points);
    }

    /// <summary>A copy with a new point, or this curve if one is already within <see cref="MinGap"/>.</summary>
    public Curve WithPointAdded(double at, float value)
    {
        if (_points.Any(p => Math.Abs(p.At - at) < MinGap)) return this;
        return new Curve(_points.Append((at, value)).ToArray());
    }

    /// <summary>A copy without one point. The first and last point stay, and so do curves of two points.</summary>
    public Curve WithoutPoint(int index)
    {
        if (_points.Length <= 2 || index <= 0 || index >= _points.Length - 1) return this;
        return new Curve(_points.Where((_, i) => i != index).ToArray());
    }

    public float At(double position)
    {
        var p = _points;
        if (position <= p[0].At) return p[0].Value;
        if (position >= p[^1].At) return p[^1].Value;
        var i = 1;
        while (p[i].At < position) i++;
        var (at0, v0) = p[i - 1];
        var (at1, v1) = p[i];
        var t = (float)((position - at0) / (at1 - at0));
        return v0 + (v1 - v0) * (0.5f - 0.5f * MathF.Cos(MathF.PI * t));
    }
}
