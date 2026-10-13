using System.Collections.Generic;
using System.Drawing;
using BetterMouse.Core;
using Xunit;

namespace BetterMouse.Core.Tests;

public class RemapEngineTests
{
    private sealed class FakeCursor : ICursorEnv
    {
        public Point Position;
        public List<Point> Sets { get; } = new();

        public Point GetCursorPosition() => Position;

        public void SetCursorPosition(Point position)
        {
            Position = position;
            Sets.Add(position);
        }
    }

    private static DisplayInfo Display(string id, int x, int y, int w, int h, int dpi)
        => new() { Id = id, Name = id, Bounds = new Rectangle(x, y, w, h), Dpi = dpi };

    private static RemapEngine Build(out DisplayLayout layout, out FakeCursor cursor, params DisplayInfo[] displays)
    {
        layout = new DisplayLayout(displays);
        var config = new MouseMappingConfig();
        config.EnsureDefaults(layout);
        cursor = new FakeCursor();
        return new RemapEngine(layout, config);
    }

    [Fact]
    public void CrossingRight_RemapsVertically()
    {
        var engine = Build(out _, out var cursor,
            Display("A", 0, 0, 1920, 1080, 96),
            Display("B", 1920, 0, 1920, 1080, 96));
        cursor.Position = new Point(1919, 540);

        var handled = engine.HandleMove(new Point(1921, 540), cursor);

        Assert.True(handled);
        Assert.Equal(new Point(1920, 540), cursor.Position);
    }

    [Fact]
    public void CrossingRight_MixedDpi_LandsAtPhysicalEquivalent()
    {
        var engine = Build(out _, out var cursor,
            Display("A", 0, 0, 1920, 1080, 96),
            Display("B", 1920, 0, 3840, 2160, 192));
        cursor.Position = new Point(1919, 540);

        var handled = engine.HandleMove(new Point(1921, 540), cursor);

        Assert.True(handled);
        Assert.Equal(1920, cursor.Position.X);
        Assert.Equal(1080, cursor.Position.Y);
    }

    [Fact]
    public void DiagonalCorner_NoBorder_Unsticks()
    {
        var engine = Build(out _, out var cursor,
            Display("A", 0, 0, 1920, 1080, 96),
            Display("B", 1920, 1080, 1920, 1080, 96));
        cursor.Position = new Point(1919, 1079);

        var handled = engine.HandleMove(new Point(1921, 1081), cursor);

        Assert.True(handled);
        Assert.True(new Rectangle(1920, 1080, 1920, 1080).Contains(cursor.Position));
    }

    [Fact]
    public void WrapDisabled_DoesNotWrap()
    {
        var engine = Build(out var layout, out var cursor,
            Display("A", 0, 0, 1920, 1080, 96),
            Display("B", 1920, 0, 1920, 1080, 96));
        cursor.Position = new Point(0, 540);

        // No profile on A's left edge, no display there, wrap off by default.
        var handled = engine.HandleMove(new Point(-1, 540), cursor);

        Assert.False(handled);
        Assert.Equal(new Point(0, 540), cursor.Position);
    }

    [Fact]
    public void Disabled_DoesNothing()
    {
        var layout = new DisplayLayout(new[]
        {
            Display("A", 0, 0, 1920, 1080, 96),
            Display("B", 1920, 0, 1920, 1080, 96),
        });
        var config = new MouseMappingConfig { Enabled = false };
        config.EnsureDefaults(layout);
        var engine = new RemapEngine(layout, config);
        var cursor = new FakeCursor { Position = new Point(1919, 540) };

        Assert.False(engine.HandleMove(new Point(1921, 540), cursor));
    }

    [Fact]
    public void DuplicateRawMove_IsIgnored()
    {
        var engine = Build(out _, out var cursor,
            Display("A", 0, 0, 1920, 1080, 96),
            Display("B", 1920, 0, 1920, 1080, 96));
        cursor.Position = new Point(1919, 540);

        Assert.True(engine.HandleMove(new Point(1921, 540), cursor));
        cursor.Position = new Point(1920, 540);
        Assert.False(engine.HandleMove(new Point(1921, 540), cursor));
    }
}
