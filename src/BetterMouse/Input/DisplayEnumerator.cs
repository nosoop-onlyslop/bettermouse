using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BetterMouse.Core;

namespace BetterMouse.Input;

/// <summary>Builds a <see cref="DisplayLayout"/> from the live Win32 display state.</summary>
internal static class DisplayEnumerator
{
    public static DisplayLayout Enumerate()
    {
        var displays = new List<DisplayInfo>();

        foreach (var screen in Screen.AllScreens)
        {
            var dpi = GetEffectiveDpi(screen.Bounds);
            var id = GetStableId(screen.DeviceName, out var friendlyName);

            displays.Add(new DisplayInfo
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(friendlyName) ? screen.DeviceName : friendlyName,
                Bounds = screen.Bounds,
                Dpi = (int)(dpi == 0 ? 96u : dpi),
                IsPrimary = screen.Primary,
            });
        }

        return new DisplayLayout(displays);
    }

    private static uint GetEffectiveDpi(Rectangle bounds)
    {
        var pt = new NativeMethods.POINT
        {
            X = bounds.Left + bounds.Width / 2,
            Y = bounds.Top + bounds.Height / 2,
        };

        var monitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor != System.IntPtr.Zero &&
            NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0)
        {
            return dpiX;
        }

        return 96;
    }

    private static string GetStableId(string deviceName, out string friendlyName)
    {
        friendlyName = string.Empty;

        var dd = new NativeMethods.DISPLAY_DEVICE
        {
            cb = Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>(),
        };

        if (NativeMethods.EnumDisplayDevices(deviceName, 0, ref dd, NativeMethods.EDD_GET_DEVICE_INTERFACE_NAME))
        {
            friendlyName = dd.DeviceString;
            if (!string.IsNullOrWhiteSpace(dd.DeviceID))
                return dd.DeviceID;
        }

        return deviceName;
    }
}
