using BetterMouse.Core;
using Xunit;

namespace BetterMouse.Core.Tests;

public class BorderProfileTests
{
    [Fact]
    public void PiecewiseLinear_InterpolatesEachSegment()
    {
        var profile = new BorderProfile { AId = "A", BId = "B", ASide = Side.Right };
        profile.Points.Add(new ControlPoint(0.0, 0.0));
        profile.Points.Add(new ControlPoint(0.5, 0.75));
        profile.Points.Add(new ControlPoint(1.0, 1.0));

        Assert.Equal(0.75, profile.MapAToB(0.5), 3);
        Assert.Equal(0.875, profile.MapAToB(0.75), 3);
        Assert.Equal(0.5, profile.MapBToA(0.75), 3);
    }

    [Fact]
    public void OutsideAuthoredRange_IsWall()
    {
        var profile = new BorderProfile { AId = "A", BId = "B", ASide = Side.Right };
        profile.Points.Add(new ControlPoint(0.25, 0.0));
        profile.Points.Add(new ControlPoint(0.75, 1.0));

        Assert.True(double.IsNaN(profile.MapAToB(0.1)));
        Assert.True(double.IsNaN(profile.MapAToB(0.9)));
    }

    [Fact]
    public void Normalize_SortsByA()
    {
        var profile = new BorderProfile { AId = "A", BId = "B", ASide = Side.Right };
        profile.Points.Add(new ControlPoint(0.9, 1.0));
        profile.Points.Add(new ControlPoint(0.1, 0.0));

        profile.Normalize();

        Assert.Equal(0.1, profile.Points[0].A, 3);
        Assert.Equal(0.9, profile.Points[1].A, 3);
    }
}
