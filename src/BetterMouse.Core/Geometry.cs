using System.Drawing;

namespace BetterMouse.Core;

/// <summary>
/// Small geometric helpers, largely ported from MouseUnSnag's <c>GeometryUtil</c> so the
/// unstick/jump/wrap fallbacks behave the same way.
/// </summary>
public static class Geometry
{
    public static Point Sign(Point p) => new(Math.Sign(p.X), Math.Sign(p.Y));

    public static double Magnitude(Point p) => Math.Sqrt((double)p.X * p.X + (double)p.Y * p.Y);

    public static bool OverlapX(Rectangle a, Rectangle b) => a.Left < b.Right && a.Right > b.Left;

    public static bool OverlapY(Rectangle a, Rectangle b) => a.Top < b.Bottom && a.Bottom > b.Top;

    /// <summary>Signed distance from the nearest vertical edge; 0 when inside.</summary>
    public static int OutsideXDistance(Rectangle r, Point p)
        => Math.Max(Math.Min(0, p.X - r.Left), p.X - r.Right + 1);

    /// <summary>Signed distance from the nearest horizontal edge; 0 when inside.</summary>
    public static int OutsideYDistance(Rectangle r, Point p)
        => Math.Max(Math.Min(0, p.Y - r.Top), p.Y - r.Bottom + 1);

    public static Point OutsideDistance(Rectangle r, Point p)
        => new(OutsideXDistance(r, p), OutsideYDistance(r, p));

    /// <summary>Component direction (signs) in which <paramref name="p"/> lies outside <paramref name="r"/>.</summary>
    public static Point OutsideDirection(Rectangle r, Point p) => Sign(OutsideDistance(r, p));

    /// <summary>Component direction in which rectangle <paramref name="r2"/> lies relative to <paramref name="r1"/>.</summary>
    public static Point OutsideDirection(Rectangle r1, Rectangle r2) => (OverlapX(r1, r2), OverlapY(r1, r2)) switch
    {
        (false, false) => new Point(Math.Sign(r2.Left - r1.Left), Math.Sign(r2.Top - r1.Top)),
        (false, true) => new Point(Math.Sign(r2.Left - r1.Left), 0),
        (true, false) => new Point(0, Math.Sign(r2.Top - r1.Top)),
        _ => Point.Empty,
    };

    /// <summary>Clamp <paramref name="p"/> to the nearest pixel inside <paramref name="r"/>.</summary>
    public static Point ClosestBoundaryPoint(Rectangle r, Point p) => new(
        Math.Max(Math.Min(p.X, r.Right - 1), r.Left),
        Math.Max(Math.Min(p.Y, r.Bottom - 1), r.Top));

    public static int Clamp(int value, int min, int max) => Math.Max(Math.Min(value, max), min);
}
