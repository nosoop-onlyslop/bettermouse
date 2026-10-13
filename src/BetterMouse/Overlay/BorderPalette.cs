using System.Windows.Media;

namespace BetterMouse.Overlay;

/// <summary>
/// Assigns a stable, visually distinct colour to each border profile. This matters when a
/// display edge faces more than one neighbour (e.g. a tall monitor beside two stacked
/// ones): the profiles' handles land on the same physical edge, and a single shared colour
/// made them indistinguishable.
/// </summary>
/// <remarks>
/// A profile keeps the same hue on both of its displays, and the canonical A/B side is
/// distinguished by brightness so the orientation stays readable.
/// </remarks>
internal static class BorderPalette
{
    private static readonly Color[] Palette =
    {
        Color.FromRgb(0x4E, 0xC9, 0xB0), // teal
        Color.FromRgb(0xE0, 0x9F, 0x3E), // amber
        Color.FromRgb(0x6C, 0x9B, 0xFF), // blue
        Color.FromRgb(0xE0, 0x5C, 0x8A), // pink
        Color.FromRgb(0x8B, 0xD4, 0x50), // green
        Color.FromRgb(0xB0, 0x7C, 0xE8), // purple
        Color.FromRgb(0xE8, 0x5D, 0x5D), // red
        Color.FromRgb(0x4F, 0xC3, 0xE8), // cyan
    };

    public static Color For(int profileIndex, bool isA)
    {
        var count = Palette.Length;
        var color = Palette[((profileIndex % count) + count) % count];
        return isA ? color : BlendTowardWhite(color, 0.45);
    }

    private static Color BlendTowardWhite(Color c, double t) => Color.FromRgb(
        (byte)(c.R + (255 - c.R) * t),
        (byte)(c.G + (255 - c.G) * t),
        (byte)(c.B + (255 - c.B) * t));
}
