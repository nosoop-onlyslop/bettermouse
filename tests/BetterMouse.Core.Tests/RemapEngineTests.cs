using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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

    [Fact]
    public void EnsureDefaults_KeepsBothProfilesSharingOneDisplayEdge()
    {
        // A tall monitor on the left; two stacked monitors on the right both share A's edge.
        var layout = new DisplayLayout(new[]
        {
            Display("A", 0, 0, 1920, 2160, 96),
            Display("B", 1920, 0, 1920, 1080, 96),
            Display("C", 1920, 1080, 1920, 1080, 96),
        });
        var config = new MouseMappingConfig();
        config.EnsureDefaults(layout);

        // A-B and A-C share A's right edge; B-C also share a horizontal edge, so three
        // profiles exist in total and must all survive regeneration.
        Assert.Contains(config.Borders, p => p.AId == "A" && p.BId == "B");
        Assert.Contains(config.Borders, p => p.AId == "A" && p.BId == "C");
        Assert.Contains(config.Borders, p => p.AId == "B" && p.BId == "C");

        // Both are outgoing from A's right edge, so the engine must offer two candidates.
        var from = layout.ById("A")!;
        Assert.Equal(2, config.FindAll(from, Side.Right).Count());
    }

    [Fact]
    public void SharedEdge_TwoNeighbours_MapsEachSegment()
    {
        var layout = new DisplayLayout(new[]
        {
            Display("A", 0, 0, 1920, 2160, 96),
            Display("B", 1920, 0, 1920, 1080, 96),
            Display("C", 1920, 1080, 1920, 1080, 96),
        });
        // Isolate the border map from the unstick/jump fallbacks.
        var config = new MouseMappingConfig { Unstick = false, Jump = false, Wrap = false };
        config.EnsureDefaults(layout);
        var engine = new RemapEngine(layout, config);
        var cursor = new FakeCursor();

        // A -> B (upper segment).
        cursor.Position = new Point(1919, 400);
        Assert.True(engine.HandleMove(new Point(1921, 400), cursor));
        Assert.Equal(new Point(1920, 400), cursor.Position);

        // A -> C (lower segment) must use C's profile, not just fall back to unstick.
        cursor.Position = new Point(1919, 1600);
        Assert.True(engine.HandleMove(new Point(1921, 1600), cursor));
        Assert.Equal(new Point(1920, 1600), cursor.Position);

        // ...and back, each neighbour resolving through its own profile.
        cursor.Position = new Point(1921, 400);
        Assert.True(engine.HandleMove(new Point(1919, 400), cursor));
        Assert.Equal(new Point(1919, 400), cursor.Position);

        cursor.Position = new Point(1921, 1600);
        Assert.True(engine.HandleMove(new Point(1919, 1600), cursor));
        Assert.Equal(new Point(1919, 1599), cursor.Position);
    }

    [Fact]
    public void SharedEdge_SecondNeighbour_MixedDpi_AppliesPhysicalCorrespondence()
    {
        var layout = new DisplayLayout(new[]
        {
            Display("A", 0, 0, 1920, 2160, 96),
            Display("B", 1920, 0, 1920, 1080, 96),
            Display("C", 1920, 1080, 1920, 1080, 192), // C is at 200% scaling.
        });
        var config = new MouseMappingConfig { Unstick = false, Jump = false, Wrap = false };
        config.EnsureDefaults(layout);
        var engine = new RemapEngine(layout, config);
        var cursor = new FakeCursor { Position = new Point(1919, 1350) };

        Assert.True(engine.HandleMove(new Point(1921, 1350), cursor));

        // A at 100%: y=1350 is physical y=1350. C at 200% maps the shared physical offset
        // to mid-height on C, so the corrected landing is y=1620, not the raw 1350 the
        // unstick fallback would have produced.
        Assert.Equal(new Point(1920, 1620), cursor.Position);
    }

    [Fact]
    public void Wall_OutsideAuthoredRange_HoldsCursorOnSourceDisplay()
    {
        // A vertically offset left monitor beside a taller centre one (both 100%). The
        // authored border only covers the physically-overlapping part of the shared edge;
        // below B=0.941 there is no counterpart on the left, so that part is a wall.
        var layout = new DisplayLayout(new[]
        {
            Display("L", -1280, 25, 1280, 1024, 96),
            Display("C", 0, 0, 1920, 1080, 96),
        });
        var config = new MouseMappingConfig { Unstick = false, Jump = false, Wrap = false };
        config.Borders.Add(new BorderProfile
        {
            AId = "L", BId = "C", ASide = Side.Right,
            Points = { new ControlPoint(0, 0.037), new ControlPoint(1, 0.941) },
        });
        var engine = new RemapEngine(layout, config);

        // In range: left bottom corner maps up onto the centre.
        var cursor = new FakeCursor { Position = new Point(-1, 1048) };
        Assert.True(engine.HandleMove(new Point(0, 1048), cursor));
        Assert.Equal(new Point(0, 1014), cursor.Position);

        // Nudge down into the wall on the centre, then try to come back.
        Assert.False(engine.HandleMove(new Point(0, 1019), cursor));
        cursor.Position = new Point(0, 1019); // the OS applies the ordinary move

        // The reverse crossing is outside the authored range. The engine must swallow it
        // and hold the cursor on the centre, not pass it to Windows' pixel-continuous
        // cross-display positioning (which would land at (-1, 1019), above the corner).
        Assert.True(engine.HandleMove(new Point(-1, 1019), cursor));
        Assert.Equal(new Point(0, 1019), cursor.Position);
    }

    [Fact]
    public void Wall_UnstickEnabled_StillCrossesAtRawPosition()
    {
        // The same wall, but with unstick on: the fallback deliberately moves the cursor
        // onto the other display (MouseUnSnag parity), so the wall is bypassed.
        var layout = new DisplayLayout(new[]
        {
            Display("L", -1280, 25, 1280, 1024, 96),
            Display("C", 0, 0, 1920, 1080, 96),
        });
        var config = new MouseMappingConfig { Unstick = true, Jump = false, Wrap = false };
        config.Borders.Add(new BorderProfile
        {
            AId = "L", BId = "C", ASide = Side.Right,
            Points = { new ControlPoint(0, 0.037), new ControlPoint(1, 0.941) },
        });
        var engine = new RemapEngine(layout, config);
        var cursor = new FakeCursor { Position = new Point(0, 1019) };

        Assert.True(engine.HandleMove(new Point(-1, 1019), cursor));
        Assert.Equal(new Point(-1, 1019), cursor.Position);
    }

    [Fact]
    public void OuterEdge_NoNeighbour_PassesThroughToOs()
    {
        // Nothing is beyond A's left edge, so Windows only clamps; the engine leaves the
        // event alone rather than swallowing it.
        var engine = Build(out _, out var cursor,
            Display("A", 0, 0, 1920, 1080, 96));
        cursor.Position = new Point(0, 540);

        Assert.False(engine.HandleMove(new Point(-1, 540), cursor));
        Assert.Equal(new Point(0, 540), cursor.Position);
    }
}
