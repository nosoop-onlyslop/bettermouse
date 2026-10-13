namespace BetterMouse.Core;

/// <summary>
/// One correspondence anchor on a shared border. <see cref="A"/> and <see cref="B"/> are
/// normalized positions (0..1) along the two facing edges. A set of anchors, sorted by A,
/// defines a piecewise-linear map between the edges: this is the data your handles author.
/// </summary>
public struct ControlPoint
{
    public double A { get; set; }
    public double B { get; set; }

    public ControlPoint(double a, double b)
    {
        A = a;
        B = b;
    }

    public override string ToString() => $"({A:0.###}, {B:0.###})";
}

/// <summary>
/// The DPI-corrected correspondence between two adjacent displays across one shared edge.
/// Canonicalized so that <see cref="AId"/> is always the left (horizontal) or top (vertical)
/// display and <see cref="ASide"/> is its <see cref="Side.Right"/> or <see cref="Side.Bottom"/>.
/// </summary>
public sealed class BorderProfile
{
    private const double Eps = 1e-9;

    public string AId { get; set; } = string.Empty;
    public string BId { get; set; } = string.Empty;

    /// <summary>The edge of A that faces B. Canonically Right or Bottom.</summary>
    public Side ASide { get; set; }

    /// <summary>The facing edge of B. Always the opposite of <see cref="ASide"/>.</summary>
    public Side BSide => ASide.Opposite();

    /// <summary>Control points, expected sorted by <see cref="ControlPoint.A"/>.</summary>
    public List<ControlPoint> Points { get; set; } = new();

    public bool MatchesPair(string id1, string id2)
        => (AId == id1 && BId == id2) || (AId == id2 && BId == id1);

    /// <summary>
    /// The display on the far side of this border when crossing out of
    /// <paramref name="fromId"/> across <paramref name="side"/>, or null when this profile
    /// does not describe that crossing.
    /// </summary>
    public string? OtherId(string fromId, Side side)
    {
        if (AId == fromId && ASide == side) return BId;
        if (BId == fromId && BSide == side) return AId;
        return null;
    }

    /// <summary>Sort by A and clamp everything into [0,1].</summary>
    public void Normalize()
    {
        Points = Points.OrderBy(p => p.A).ToList();
        for (var i = 0; i < Points.Count; i++)
        {
            var p = Points[i];
            p.A = Math.Clamp(p.A, 0, 1);
            p.B = Math.Clamp(p.B, 0, 1);
            Points[i] = p;
        }
    }

    /// <summary>
    /// Map a normalized position on A's edge to B's edge. Returns <see cref="double.NaN"/>
    /// when <paramref name="a"/> falls outside the authored range, which the engine treats
    /// as a wall on that border.
    /// </summary>
    public double MapAToB(double a)
    {
        if (Points.Count == 0) return double.NaN;
        var pts = Points;
        if (a < pts[0].A - Eps || a > pts[^1].A + Eps) return double.NaN;
        if (a <= pts[0].A) return pts[0].B;
        if (a >= pts[^1].A) return pts[^1].B;

        for (var i = 0; i < pts.Count - 1; i++)
        {
            var p0 = pts[i];
            var p1 = pts[i + 1];
            if (a >= p0.A && a <= p1.A)
            {
                var span = p1.A - p0.A;
                if (span <= Eps) return p1.B;
                var f = (a - p0.A) / span;
                return p0.B + f * (p1.B - p0.B);
            }
        }

        return pts[^1].B;
    }

    /// <summary>Inverse of <see cref="MapAToB"/>.</summary>
    public double MapBToA(double b)
    {
        if (Points.Count == 0) return double.NaN;
        var pts = Points.OrderBy(p => p.B).ToList();
        if (b < pts[0].B - Eps || b > pts[^1].B + Eps) return double.NaN;
        if (b <= pts[0].B) return pts[0].A;
        if (b >= pts[^1].B) return pts[^1].A;

        for (var i = 0; i < pts.Count - 1; i++)
        {
            var p0 = pts[i];
            var p1 = pts[i + 1];
            if (b >= p0.B && b <= p1.B)
            {
                var span = p1.B - p0.B;
                if (span <= Eps) return p1.A;
                var f = (b - p0.B) / span;
                return p0.A + f * (p1.A - p0.A);
            }
        }

        return pts[^1].A;
    }

    /// <summary>
    /// Like <see cref="MapAToB"/> but clamps to the nearest authored point instead of
    /// returning <see cref="double.NaN"/> outside the authored range. A fallback that
    /// deliberately crosses a wall (unstick) uses this so it still lands at the
    /// physically-corresponding height rather than the raw pixel height.
    /// </summary>
    public double MapAToBClamped(double a)
    {
        if (Points.Count == 0) return double.NaN;
        if (a < Points[0].A) return Points[0].B;
        if (a > Points[^1].A) return Points[^1].B;
        return MapAToB(a);
    }

    /// <summary>Clamped inverse of <see cref="MapAToB"/> (see <see cref="MapAToBClamped"/>).</summary>
    public double MapBToAClamped(double b)
    {
        if (Points.Count == 0) return double.NaN;
        var pts = Points.OrderBy(p => p.B).ToList();
        if (b < pts[0].B) return pts[0].A;
        if (b > pts[^1].B) return pts[^1].A;
        return MapBToA(b);
    }
}
