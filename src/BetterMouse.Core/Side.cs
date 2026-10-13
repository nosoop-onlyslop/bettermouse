namespace BetterMouse.Core;

/// <summary>One of the four edges of a display's pixel rectangle.</summary>
public enum Side
{
    Left = 0,
    Top = 1,
    Right = 2,
    Bottom = 3,
}

public static class SideExtensions
{
    public static Side Opposite(this Side side) => side switch
    {
        Side.Left => Side.Right,
        Side.Right => Side.Left,
        Side.Top => Side.Bottom,
        Side.Bottom => Side.Top,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, null),
    };

    /// <summary>True for the vertical edges (Left/Right), whose coordinate runs along Y.</summary>
    public static bool IsVerticalEdge(this Side side) => side is Side.Left or Side.Right;

    /// <summary>True for the horizontal edges (Top/Bottom), whose coordinate runs along X.</summary>
    public static bool IsHorizontalEdge(this Side side) => side is Side.Top or Side.Bottom;

    /// <summary>Sign vector pointing outwards, across the given edge.</summary>
    public static (int X, int Y) Outward(this Side side) => side switch
    {
        Side.Left => (-1, 0),
        Side.Right => (1, 0),
        Side.Top => (0, -1),
        Side.Bottom => (0, 1),
        _ => (0, 0),
    };
}
