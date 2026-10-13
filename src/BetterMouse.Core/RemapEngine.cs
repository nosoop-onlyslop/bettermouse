using System.Drawing;

namespace BetterMouse.Core;

/// <summary>The two OS operations the engine needs, abstracted so the algorithm is unit-testable.</summary>
public interface ICursorEnv
{
    Point GetCursorPosition();

    void SetCursorPosition(Point position);
}

/// <summary>
/// Pure position-only remapping. For every mouse-move event it decides whether the cursor
/// should be moved, and where. All OS access goes through <see cref="ICursorEnv"/>.
/// </summary>
public sealed class RemapEngine
{
    private Point _lastRaw;
    private bool _hasLastRaw;

    public RemapEngine(DisplayLayout layout, MouseMappingConfig config)
    {
        Layout = layout;
        Config = config;
    }

    public DisplayLayout Layout { get; set; }

    public MouseMappingConfig Config { get; set; }

    /// <summary>Temporarily stop remapping (e.g. while the layout editor is open).</summary>
    public bool Suspended { get; set; }

    /// <summary>
    /// Handle one intended mouse position (the raw, unclamped hook position). Returns true
    /// when the cursor was moved and the raw event should be swallowed.
    /// </summary>
    public bool HandleMove(Point raw, ICursorEnv env)
    {
        if (Suspended)
            return false;

        if (_hasLastRaw && raw == _lastRaw)
            return false;
        _lastRaw = raw;
        _hasLastRaw = true;

        if (!Config.Enabled)
            return false;

        var cursor = env.GetCursorPosition();
        var from = Layout.DisplayAt(cursor) ?? Layout.DisplayAt(raw);
        if (from is null)
            return false;

        // Still on the same display: an ordinary move.
        if (from.Bounds.Contains(raw))
            return false;

        // 1) Straight border crossing: apply the authored DPI correspondence. A single
        // edge may face several neighbours (e.g. a tall monitor beside two stacked ones),
        // so try every candidate profile; only the one whose authored range covers this
        // coordinate maps, and the rest fall through to the fallbacks below.
        var side = DominantSide(from.Bounds, raw);
        foreach (var profile in Config.FindAll(from, side))
        {
            if (TryMap(profile, from, side, raw, out var landing))
            {
                env.SetCursorPosition(landing);
                return true;
            }
        }

        // 2) MouseUnSnag "unstick": the intended point is already over another display.
        var rawDisplay = Layout.DisplayAt(raw);
        if (rawDisplay is not null && !ReferenceEquals(rawDisplay, from))
        {
            if (!Config.Unstick)
                return false;
            env.SetCursorPosition(Geometry.ClosestBoundaryPoint(rawDisplay.Bounds, raw));
            return true;
        }

        // 3) MouseUnSnag "jump": head for the nearest display in the direction of travel.
        if (Config.Jump)
        {
            var target = FindJumpTarget(from, raw);
            if (target is not null)
            {
                env.SetCursorPosition(Geometry.ClosestBoundaryPoint(target.Bounds, raw));
                return true;
            }
        }

        // 4) MouseUnSnag "wrap".
        if (Config.Wrap && TryWrap(from, raw, out var wrapped))
        {
            env.SetCursorPosition(wrapped);
            return true;
        }

        return false;
    }

    private bool TryMap(BorderProfile profile, DisplayInfo from, Side side, Point raw, out Point landing)
    {
        landing = default;

        var forward = profile.AId == from.Id && profile.ASide == side;
        var reverse = profile.BId == from.Id && profile.BSide == side;
        if (!forward && !reverse)
            return false;

        var t = NormalizeOnEdge(from.Bounds, side, raw);
        var u = forward ? profile.MapAToB(t) : profile.MapBToA(t);
        if (double.IsNaN(u))
            return false;

        var target = Layout.ById(forward ? profile.BId : profile.AId);
        if (target is null)
            return false;

        landing = DenormalizeOnEdge(target.Bounds, side.Opposite(), u);
        return true;
    }

    /// <summary>Which edge of the rectangle the point is predominantly outside of.</summary>
    internal static Side DominantSide(Rectangle b, Point p)
    {
        var dxLeft = p.X < b.Left ? b.Left - p.X : 0;
        var dxRight = p.X >= b.Right ? p.X - (b.Right - 1) : 0;
        var dyTop = p.Y < b.Top ? b.Top - p.Y : 0;
        var dyBottom = p.Y >= b.Bottom ? p.Y - (b.Bottom - 1) : 0;

        var dx = Math.Max(dxLeft, dxRight);
        var dy = Math.Max(dyTop, dyBottom);

        if (dx >= dy)
            return dxLeft > 0 ? Side.Left : Side.Right;
        return dyTop > 0 ? Side.Top : Side.Bottom;
    }

    internal static double NormalizeOnEdge(Rectangle b, Side side, Point p)
    {
        var v = side.IsVerticalEdge()
            ? (p.Y - b.Top) / (double)b.Height
            : (p.X - b.Left) / (double)b.Width;
        return Math.Clamp(v, 0, 1);
    }

    internal static Point DenormalizeOnEdge(Rectangle b, Side side, double n)
    {
        n = Math.Clamp(n, 0, 1);

        switch (side)
        {
            case Side.Left:
                return new Point(b.Left, Geometry.Clamp(b.Top + (int)Math.Round(n * (b.Height - 1)), b.Top, b.Bottom - 1));
            case Side.Right:
                return new Point(b.Right - 1, Geometry.Clamp(b.Top + (int)Math.Round(n * (b.Height - 1)), b.Top, b.Bottom - 1));
            case Side.Top:
                return new Point(Geometry.Clamp(b.Left + (int)Math.Round(n * (b.Width - 1)), b.Left, b.Right - 1), b.Top);
            case Side.Bottom:
                return new Point(Geometry.Clamp(b.Left + (int)Math.Round(n * (b.Width - 1)), b.Left, b.Right - 1), b.Bottom - 1);
            default:
                return new Point(b.Left, b.Top);
        }
    }

    private DisplayInfo? FindJumpTarget(DisplayInfo from, Point raw)
    {
        var dir = Geometry.OutsideDirection(from.Bounds, raw);

        DisplayInfo? best = null;
        var bestDistance = double.PositiveInfinity;

        foreach (var d in Layout.Displays)
        {
            if (ReferenceEquals(d, from)) continue;

            var screenDir = Geometry.OutsideDirection(from.Bounds, d.Bounds);
            var matches =
                (screenDir.X * dir.X == 1 && Math.Abs(screenDir.Y - dir.Y) <= 1) ||
                (screenDir.Y * dir.Y == 1 && Math.Abs(screenDir.X - dir.X) <= 1);
            if (!matches) continue;

            var distance = Geometry.Magnitude(Geometry.OutsideDistance(d.Bounds, raw));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = d;
            }
        }

        return best;
    }

    private bool TryWrap(DisplayInfo from, Point raw, out Point landing)
    {
        landing = default;

        var dir = Geometry.OutsideDirection(from.Bounds, raw);
        var bounds = Layout.VirtualBounds;

        if (dir.X > 0 && from.Bounds.Right >= bounds.Right)
        {
            var target = Layout.Displays.OrderBy(d => d.Bounds.Left).FirstOrDefault();
            if (target is null || ReferenceEquals(target, from)) return false;
            landing = DenormalizeOnEdge(target.Bounds, Side.Left, NormalizeOnEdge(from.Bounds, Side.Right, raw));
            return true;
        }

        if (dir.X < 0 && from.Bounds.Left <= bounds.Left)
        {
            var target = Layout.Displays.OrderByDescending(d => d.Bounds.Right).FirstOrDefault();
            if (target is null || ReferenceEquals(target, from)) return false;
            landing = DenormalizeOnEdge(target.Bounds, Side.Right, NormalizeOnEdge(from.Bounds, Side.Left, raw));
            return true;
        }

        if (dir.Y > 0 && from.Bounds.Bottom >= bounds.Bottom)
        {
            var target = Layout.Displays.OrderBy(d => d.Bounds.Top).FirstOrDefault();
            if (target is null || ReferenceEquals(target, from)) return false;
            landing = DenormalizeOnEdge(target.Bounds, Side.Top, NormalizeOnEdge(from.Bounds, Side.Bottom, raw));
            return true;
        }

        if (dir.Y < 0 && from.Bounds.Top <= bounds.Top)
        {
            var target = Layout.Displays.OrderByDescending(d => d.Bounds.Bottom).FirstOrDefault();
            if (target is null || ReferenceEquals(target, from)) return false;
            landing = DenormalizeOnEdge(target.Bounds, Side.Bottom, NormalizeOnEdge(from.Bounds, Side.Top, raw));
            return true;
        }

        return false;
    }
}
