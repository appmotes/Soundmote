using Soundmote.Core.Scenes;

namespace Soundmote.Core.Tests;

public class CurveTests
{
    [Fact]
    public void Even_values_sit_at_their_points()
    {
        var curve = Curve.Even(0.2f, 0.8f, 0.4f);

        Assert.Equal(0.2f, curve.At(0), 5);
        Assert.Equal(0.8f, curve.At(0.5), 5);
        Assert.Equal(0.4f, curve.At(1), 5);
    }

    [Fact]
    public void Between_points_it_eases_without_overshoot()
    {
        var curve = Curve.Even(0f, 1f);

        Assert.Equal(0.5f, curve.At(0.5), 5);
        Assert.True(curve.At(0.1) < 0.1f, "starts flat");
        for (var x = 0.0; x <= 1.0; x += 0.01) Assert.InRange(curve.At(x), 0f, 1f);
    }

    [Fact]
    public void Outside_the_turn_it_holds_the_end_values()
    {
        var curve = Curve.Even(0.3f, 0.6f);

        Assert.Equal(0.3f, curve.At(-1));
        Assert.Equal(0.6f, curve.At(2));
    }

    [Fact]
    public void Values_are_clamped_to_the_unit_range() =>
        Assert.Equal(1f, new Curve((0, 3f)).At(0.5));

    [Fact]
    public void A_moved_point_stays_between_its_neighbours()
    {
        var curve = Curve.Even(0.2f, 0.5f, 0.8f);

        var moved = curve.WithPointMoved(1, at: 1.2, value: 1.4f);

        Assert.Equal(1 - Curve.MinGap, moved.Points[1].At, 6);
        Assert.Equal(1f, moved.Points[1].Value);
        Assert.Equal(0.5f, curve.Points[1].Value);
    }

    [Fact]
    public void The_ends_keep_their_place_in_time()
    {
        var moved = Curve.Even(0.2f, 0.8f).WithPointMoved(0, at: 0.3, value: 0.6f);

        Assert.Equal(0, moved.Points[0].At);
        Assert.Equal(0.6f, moved.Points[0].Value);
    }

    [Fact]
    public void Points_are_added_in_order_and_never_on_top_of_another()
    {
        var curve = Curve.Even(0.2f, 0.8f);

        var added = curve.WithPointAdded(0.25, 0.9f);

        Assert.Equal(new[] { 0, 0.25, 1 }, added.Points.Select(p => p.At));
        Assert.Same(added, added.WithPointAdded(0.255, 0.1f));
    }

    [Fact]
    public void Only_middle_points_can_be_removed()
    {
        var curve = Curve.Even(0.2f, 0.5f, 0.8f);

        Assert.Equal(2, curve.WithoutPoint(1).Points.Count);
        Assert.Same(curve, curve.WithoutPoint(0));
        Assert.Same(curve, curve.WithoutPoint(2));
        var two = Curve.Even(0.2f, 0.8f);
        Assert.Same(two, two.WithoutPoint(1));
    }
}
