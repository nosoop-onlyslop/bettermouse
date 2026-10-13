using System.Drawing;

namespace BetterMouse.Core;

/// <summary>A canonical adjacency: <see cref="A"/>'s <see cref="ASide"/> faces <see cref="B"/>'s <see cref="BSide"/>.</summary>
public sealed record BorderPair(DisplayInfo A, Side ASide, DisplayInfo B, Side BSide);

/// <summary>The set of displays currently attached, plus the straight-edge adjacencies between them.</summary>
public sealed class DisplayLayout
{
    public DisplayLayout(IEnumerable<DisplayInfo> displays)
    {
        Displays = displays.ToList();

        Rectangle? union = null;
        foreach (var d in Displays)
            union = union is null ? d.Bounds : Rectangle.Union(union.Value, d.Bounds);
        VirtualBounds = union ?? Rectangle.Empty;

        Borders = BuildBorders();
    }

    public IReadOnlyList<DisplayInfo> Displays { get; }

    public Rectangle VirtualBounds { get; }

    public IReadOnlyList<BorderPair> Borders { get; }

    public DisplayInfo? DisplayAt(Point p)
    {
        foreach (var d in Displays)
            if (d.Bounds.Contains(p))
                return d;
        return null;
    }

    public DisplayInfo? ById(string id) => Displays.FirstOrDefault(d => d.Id == id);

    public DisplayInfo? Primary => Displays.FirstOrDefault(d => d.IsPrimary) ?? Displays.FirstOrDefault();

    /// <summary>
    /// True when <paramref name="display"/> shares a straight edge with another display
    /// across <paramref name="side"/>. This is what distinguishes a wall that Windows could
    /// otherwise cross natively from an outer desktop edge that only clamps.
    /// </summary>
    public bool HasNeighbour(DisplayInfo display, Side side)
        => Borders.Any(b =>
            (b.A.Id == display.Id && b.ASide == side) ||
            (b.B.Id == display.Id && b.BSide == side));

    private List<BorderPair> BuildBorders()
    {
        var result = new List<BorderPair>();
        for (var i = 0; i < Displays.Count; i++)
        {
            for (var j = i + 1; j < Displays.Count; j++)
            {
                var a = Displays[i];
                var b = Displays[j];

                // Horizontal neighbours (share a vertical edge).
                if (a.Bounds.Right == b.Bounds.Left && Geometry.OverlapY(a.Bounds, b.Bounds))
                    result.Add(new BorderPair(a, Side.Right, b, Side.Left));
                else if (b.Bounds.Right == a.Bounds.Left && Geometry.OverlapY(b.Bounds, a.Bounds))
                    result.Add(new BorderPair(b, Side.Right, a, Side.Left));

                // Vertical neighbours (share a horizontal edge).
                if (a.Bounds.Bottom == b.Bounds.Top && Geometry.OverlapX(a.Bounds, b.Bounds))
                    result.Add(new BorderPair(a, Side.Bottom, b, Side.Top));
                else if (b.Bounds.Bottom == a.Bounds.Top && Geometry.OverlapX(b.Bounds, a.Bounds))
                    result.Add(new BorderPair(b, Side.Bottom, a, Side.Top));
            }
        }

        return result;
    }
}
