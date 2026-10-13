namespace BetterMouse.Core;

/// <summary>
/// Builds the initial DPI-corrected correspondence for a shared edge. The model is:
/// a point on A at physical offset <c>d</c> from the top of the overlap corresponds to the
/// point on B at the same physical offset, where "physical" is approximated by the
/// Windows scaling factor (DIP = pixel / scale). Where the two displays do not physically
/// overlap, no correspondence is authored, leaving a wall that the unstick/jump fallback
/// can act on. Handles let the user correct the residual error when the arrangement is not
/// physically faithful.
/// </summary>
public static class DefaultMapping
{
    public static BorderProfile? Create(DisplayInfo a, Side aSide, DisplayInfo b)
    {
        // Canonicalize: A is the left (horizontal) or the top (vertical) display.
        if (aSide == Side.Left)
        {
            (a, b) = (b, a);
            aSide = Side.Right;
        }
        else if (aSide == Side.Top)
        {
            (a, b) = (b, a);
            aSide = Side.Bottom;
        }

        return aSide switch
        {
            Side.Right => CreateHorizontal(a, b),
            Side.Bottom => CreateVertical(a, b),
            _ => null,
        };
    }

    private static BorderProfile? CreateHorizontal(DisplayInfo a, DisplayInfo b)
    {
        double sA = a.Scale, sB = b.Scale;
        if (sA <= 0 || sB <= 0) return null;

        var overlapStart = Math.Max(a.Bounds.Top, b.Bounds.Top);
        var overlapEnd = Math.Min(a.Bounds.Bottom, b.Bounds.Bottom);
        if (overlapEnd - overlapStart < 1) return null;

        // A point y on A maps to y' = overlapStart + (y - overlapStart) * sB / sA.
        // Restrict the source range so that y' stays inside B, intersected with A.
        var y0 = Math.Max(a.Bounds.Top, overlapStart + (b.Bounds.Top - overlapStart) * sA / sB);
        var y1 = Math.Min(a.Bounds.Bottom, overlapStart + (b.Bounds.Bottom - overlapStart) * sA / sB);
        if (y1 - y0 < 1) return null;

        var ta = (y0 - a.Bounds.Top) / a.Bounds.Height;
        var tb = (y1 - a.Bounds.Top) / a.Bounds.Height;
        var ya = overlapStart + (y0 - overlapStart) * sB / sA;
        var yb = overlapStart + (y1 - overlapStart) * sB / sA;
        var ua = (ya - b.Bounds.Top) / b.Bounds.Height;
        var ub = (yb - b.Bounds.Top) / b.Bounds.Height;

        return Build(a, b, Side.Right, Clamp01(ta), Clamp01(ua), Clamp01(tb), Clamp01(ub));
    }

    private static BorderProfile? CreateVertical(DisplayInfo a, DisplayInfo b)
    {
        double sA = a.Scale, sB = b.Scale;
        if (sA <= 0 || sB <= 0) return null;

        var overlapStart = Math.Max(a.Bounds.Left, b.Bounds.Left);
        var overlapEnd = Math.Min(a.Bounds.Right, b.Bounds.Right);
        if (overlapEnd - overlapStart < 1) return null;

        var x0 = Math.Max(a.Bounds.Left, overlapStart + (b.Bounds.Left - overlapStart) * sA / sB);
        var x1 = Math.Min(a.Bounds.Right, overlapStart + (b.Bounds.Right - overlapStart) * sA / sB);
        if (x1 - x0 < 1) return null;

        var ta = (x0 - a.Bounds.Left) / a.Bounds.Width;
        var tb = (x1 - a.Bounds.Left) / a.Bounds.Width;
        var xa = overlapStart + (x0 - overlapStart) * sB / sA;
        var xb = overlapStart + (x1 - overlapStart) * sB / sA;
        var ua = (xa - b.Bounds.Left) / b.Bounds.Width;
        var ub = (xb - b.Bounds.Left) / b.Bounds.Width;

        return Build(a, b, Side.Bottom, Clamp01(ta), Clamp01(ua), Clamp01(tb), Clamp01(ub));
    }

    private static BorderProfile Build(
        DisplayInfo a, DisplayInfo b, Side aSide,
        double a0, double b0, double a1, double b1)
    {
        var profile = new BorderProfile
        {
            AId = a.Id,
            BId = b.Id,
            ASide = aSide,
        };
        profile.Points.Add(new ControlPoint(a0, b0));
        profile.Points.Add(new ControlPoint(a1, b1));
        profile.Normalize();
        return profile;
    }

    private static double Clamp01(double v) => Math.Clamp(v, 0, 1);
}
