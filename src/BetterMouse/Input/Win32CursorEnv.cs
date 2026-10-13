using System.Drawing;
using BetterMouse.Core;

namespace BetterMouse.Input;

internal sealed class Win32CursorEnv : ICursorEnv
{
    public Point GetCursorPosition()
        => NativeMethods.GetCursorPos(out var p) ? new Point(p.X, p.Y) : Point.Empty;

    public void SetCursorPosition(Point position)
        => NativeMethods.SetCursorPos(position.X, position.Y);
}
