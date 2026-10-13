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

    private OverlayMode _returnMode = OverlayMode.Off;

    public OverlayMode Mode { get; private set; } = OverlayMode.Off;

    /// <summary>
    /// Where "Done editing" returns to: <see cref="OverlayMode.View"/> if the overlay was
    /// already on when edit mode started, otherwise <see cref="OverlayMode.Off"/>.
    /// </summary>
    public OverlayMode ReturnMode => _returnMode;

    public void SetMode(OverlayMode mode)
    {
        if (mode == OverlayMode.Edit && Mode != OverlayMode.Edit)
            _returnMode = Mode == OverlayMode.View ? OverlayMode.View : OverlayMode.Off;

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
                _app.Layout,
                _app.Config,
                () => Mode,
                _app.OnOverlayChanged,
                _app.FinishEditing);
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
