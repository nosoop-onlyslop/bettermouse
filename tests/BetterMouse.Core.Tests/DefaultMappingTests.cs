using System.Drawing;
using BetterMouse.Core;
using Xunit;

namespace BetterMouse.Core.Tests;

public class DefaultMappingTests
{
    private static DisplayInfo Display(string id, int x, int y, int w, int h, int dpi)
        => new() { Id = id, Name = id, Bounds = new Rectangle(x, y, w, h), Dpi = dpi };

    [Fact]
    public void SameDpi_TopAligned_MapsFullEdge()
    {
        var a = Display("A", 0, 0, 1920, 1080, 96);
        var b = Display("B", 1920, 0, 1920, 1080, 96);

        var profile = DefaultMapping.Create(a, Side.Right, b);

        Assert.NotNull(profile);
        Assert.Equal(0.0, profile!.MapAToB(0.0), 3);
        Assert.Equal(1.0, profile.MapAToB(1.0), 3);
        Assert.Equal(0.5, profile.MapAToB(0.5), 3);
    }

    [Fact]
    public void MixedDpi_SamePhysicalHeight_MapsFullEdge()
    {
        var a = Display("A", 0, 0, 1920, 1080, 96);
        var b = Display("B", 1920, 0, 3840, 2160, 192);

        var profile = DefaultMapping.Create(a, Side.Right, b);

        Assert.NotNull(profile);
        Assert.Equal(0.0, profile!.MapAToB(0.0), 3);
        Assert.Equal(1.0, profile.MapAToB(1.0), 3);
        Assert.Equal(0.5, profile.MapAToB(0.5), 3);
    }

    [Fact]
    public void ShorterPhysical_LeavesWall()
    {
        // A is 1080 DIP tall, B is only 960 DIP tall (1440px @ 150%), top aligned.
        var a = Display("A", 0, 0, 1920, 1080, 96);
        var b = Display("B", 1920, 0, 1920, 1440, 144);

        var profile = DefaultMapping.Create(a, Side.Right, b);

        Assert.NotNull(profile);
        Assert.Equal(0.0, profile!.MapAToB(0.0), 3);
        Assert.Equal(1.0, profile.MapAToB(960.0 / 1080.0), 3);
        Assert.True(double.IsNaN(profile.MapAToB(0.99)));
    }

    [Fact]
    public void Reverse_MapsBackToSource()
    {
        // A is 1080 DIP tall, B only 960 DIP: the profile is (0,0)-(0.888,1).
        var a = Display("A", 0, 0, 1920, 1080, 96);
        var b = Display("B", 1920, 0, 1920, 1440, 144);

        var profile = DefaultMapping.Create(a, Side.Right, b);

        Assert.NotNull(profile);
        Assert.Equal(960.0 / 1080.0, profile!.MapBToA(1.0), 3);
        Assert.Equal(480.0 / 1080.0, profile.MapBToA(0.5), 3);
    }

    [Fact]
    public void VerticalNeighbours_MapAlongX()
    {
        var a = Display("A", 0, 0, 1920, 1080, 96);
        var b = Display("B", 0, 1080, 1920, 1080, 96);

        var profile = DefaultMapping.Create(a, Side.Bottom, b);

        Assert.NotNull(profile);
        Assert.Equal(Side.Bottom, profile!.ASide);
        Assert.Equal(0.0, profile.MapAToB(0.0), 3);
        Assert.Equal(1.0, profile.MapAToB(1.0), 3);
    }
}
