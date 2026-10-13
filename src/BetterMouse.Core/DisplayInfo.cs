using System.Drawing;

namespace BetterMouse.Core;

/// <summary>
/// A single display as Windows reports it while the process is PerMonitorV2 aware:
/// <see cref="Bounds"/> is in true virtual-desktop pixels and <see cref="Dpi"/> is the
/// effective DPI (the value implied by the user's scaling percentage).
/// </summary>
public sealed class DisplayInfo
{
    /// <summary>
    /// Stable identity used to persist per-border settings. On Windows this is the
    /// monitor device interface path, which survives reboots and re-arrangement.
    /// </summary>
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Pixel rectangle in the virtual desktop (physical pixels).</summary>
    public required Rectangle Bounds { get; init; }

    /// <summary>Effective DPI. 96 == 100% scaling.</summary>
    public int Dpi { get; init; } = 96;

    public bool IsPrimary { get; init; }

    /// <summary>Scaling factor relative to 96 DPI (1.0 == 100%).</summary>
    public double Scale => Dpi / 96.0;

    public int Width => Bounds.Width;
    public int Height => Bounds.Height;

    public string ScalePercent => $"{Math.Round(Scale * 100)}%";

    public override string ToString()
        => $"{Name} [{Bounds.Left},{Bounds.Top} {Width}x{Height} @ {ScalePercent}]";
}
