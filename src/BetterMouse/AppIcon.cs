using System;
using System.Drawing;

namespace BetterMouse;

/// <summary>
/// Loads the embedded application icon (<c>Assets/bettermouse.ico</c>) for the tray and
/// window chrome. Falls back to the stock application icon if the resource is unavailable
/// (e.g. a stripped-down build).
/// </summary>
internal static class AppIcon
{
    private const string PackUri = "pack://application:,,,/Assets/bettermouse.ico";

    public static Icon Load()
    {
        try
        {
            if (System.Windows.Application.GetResourceStream(new Uri(PackUri))?.Stream is { } stream)
            {
                using (stream)
                    return new Icon(stream);
            }
        }
        catch
        {
            // Ignore and fall through to the stock icon.
        }

        return SystemIcons.Application;
    }
}
