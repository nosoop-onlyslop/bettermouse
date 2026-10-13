using System;
using System.Collections.Generic;

namespace BetterMouse.Overlay;

/// <summary>Owns one <see cref="DisplayOverlayWindow"/> per display and keeps them in sync.</summary>
internal sealed class OverlayManager : IDisposable
{
    private readonly App _app;
    private readonly List<DisplayOverlayWindow> _windows = new();

    public OverlayManager(App app)
    {
        _app = app;
    }

    public OverlayMode Mode { get; private set; } = OverlayMode.Off;

    public void SetMode(OverlayMode mode)
    {
        Mode = mode;

        if (mode == OverlayMode.Off)
        {
            CloseAll();
            return;
        }

        EnsureWindows();
        foreach (var window in _windows)
            window.ApplyMode(mode);
    }

    public void RefreshAll()
    {
        foreach (var window in _windows)
            window.Refresh();
    }

    /// <summary>Recreate the overlays against the current display layout (e.g. after a hotplug).</summary>
    public void Rebuild()
    {
        if (Mode == OverlayMode.Off)
            return;

        CloseAll();
        SetMode(Mode);
    }

    private void EnsureWindows()
    {
        if (_windows.Count > 0)
            return;

        foreach (var display in _app.Layout.Displays)
        {
            var window = new DisplayOverlayWindow(
                display,
                _app.Config,
                () => Mode,
                _app.OnOverlayChanged,
                () => _app.SetOverlayMode(OverlayMode.View));
            window.Show();
            _windows.Add(window);
        }
    }

    private void CloseAll()
    {
        foreach (var window in _windows)
            window.Close();
        _windows.Clear();
    }

    public void Dispose() => CloseAll();
}
