using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using BetterMouse.Core;
using BetterMouse.Input;

namespace BetterMouse.Overlay;

/// <summary>
/// A single transparent window covering one display, with the border handles and
/// cross-display guide lines drawn at their true screen positions. In view mode it is
/// click-through; in edit mode it captures the mouse so handles can be dragged on the
/// physical monitors.
/// </summary>
public partial class DisplayOverlayWindow : Window
{
    private readonly DisplayInfo _display;
    private readonly MouseMappingConfig _config;
    private readonly Func<OverlayMode> _mode;
    private readonly Action _changed;
    private readonly Action _done;

    private readonly List<HandleHit> _handles = new();
    private HandleHit? _active;

    private sealed record HandleHit(BorderProfile Profile, int Index, bool IsA, Point CanvasPos);

    /// <summary>Physical pixels -> WPF device-independent units for this display.</summary>
    private double DipScale => 1.0 / _display.Scale;

    public DisplayOverlayWindow(
        DisplayInfo display,
        MouseMappingConfig config,
        Func<OverlayMode> mode,
        Action changed,
        Action done)
    {
        InitializeComponent();

        _display = display;
        _config = config;
        _mode = mode;
        _changed = changed;
        _done = done;

        Width = display.Width * DipScale;
        Height = display.Height * DipScale;

        SourceInitialized += (_, _) =>
        {
            ApplyExtendedStyles();
            ApplyBounds();
        };
        Loaded += (_, _) =>
        {
            ApplyBounds();
            Refresh();
        };
    }

    // --- window setup --------------------------------------------------------

    private void ApplyExtendedStyles()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        ex |= NativeMethods.WS_EX_LAYERED
            | NativeMethods.WS_EX_TOOLWINDOW
            | NativeMethods.WS_EX_NOACTIVATE
            | NativeMethods.WS_EX_TRANSPARENT;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));
    }

    private void ApplyBounds()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            _display.Bounds.Left,
            _display.Bounds.Top,
            _display.Bounds.Width,
            _display.Bounds.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    private void SetClickThrough(bool clickThrough)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        if (clickThrough)
            ex |= NativeMethods.WS_EX_TRANSPARENT;
        else
            ex &= ~NativeMethods.WS_EX_TRANSPARENT;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));
    }

    public void ApplyMode(OverlayMode mode)
    {
        SetClickThrough(mode != OverlayMode.Edit);
        DoneButton.Visibility = mode == OverlayMode.Edit ? Visibility.Visible : Visibility.Collapsed;
        ApplyBounds();
        Refresh();
    }

    // --- rendering -----------------------------------------------------------

    public void Refresh()
    {
        Surface.Children.Clear();
        _handles.Clear();

        if (_mode() == OverlayMode.Off)
            return;

        var w = _display.Width;
        var h = _display.Height;
        var cw = w * DipScale;
        var ch = h * DipScale;

        // Dim the desktop so the guides stand out. Stronger while editing.
        var dimAlpha = _mode() == OverlayMode.Edit ? (byte)0x66 : (byte)0x40;
        var dim = new Rectangle
        {
            Width = cw,
            Height = ch,
            Fill = new SolidColorBrush(Color.FromArgb(dimAlpha, 0x00, 0x00, 0x00)),
        };
        Canvas.SetLeft(dim, 0);
        Canvas.SetTop(dim, 0);
        Surface.Children.Add(dim);

        var outlineBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x4E, 0xC9, 0xB0));
        var outline = new Rectangle { Width = cw, Height = ch, Stroke = outlineBrush, StrokeThickness = 2 };
        Canvas.SetLeft(outline, 0);
        Canvas.SetTop(outline, 0);
        Surface.Children.Add(outline);

        var label = new TextBlock
        {
            Text = $"{_display.Name}   {w}×{h} @ {_display.ScalePercent}" + (_display.IsPrimary ? "   (primary)" : string.Empty),
            Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
            FontSize = 13,
        };
        Canvas.SetLeft(label, 14);
        Canvas.SetTop(label, 12);
        Surface.Children.Add(label);

        foreach (var profile in _config.Borders)
        {
            if (profile.AId == _display.Id)
                DrawProfile(profile, true, w, h, cw, ch);
            else if (profile.BId == _display.Id)
                DrawProfile(profile, false, w, h, cw, ch);
        }
    }

    private void DrawProfile(BorderProfile profile, bool isA, int w, int h, double cw, double ch)
    {
        var side = isA ? profile.ASide : profile.BSide;
        var color = isA ? Color.FromRgb(0x4E, 0xC9, 0xB0) : Color.FromRgb(0xE0, 0x9F, 0x3E);
        var handleBrush = new SolidColorBrush(color);
        var guideBrush = new SolidColorBrush(Color.FromArgb(0x38, color.R, color.G, color.B));

        for (var i = 0; i < profile.Points.Count; i++)
        {
            var n = isA ? profile.Points[i].A : profile.Points[i].B;
            var (ex, ey) = EdgeLocalPixels(side, n, w, h);

            const double tick = 54;
            const double knobInset = 16;

            // A guide line spanning the whole display, so the same control point can be
            // compared against its counterpart on the adjacent monitor.
            if (side.IsVerticalEdge())
            {
                Surface.Children.Add(new Line
                {
                    X1 = 0,
                    Y1 = ey * DipScale,
                    X2 = cw,
                    Y2 = ey * DipScale,
                    Stroke = guideBrush,
                    StrokeThickness = 1,
                });
            }
            else
            {
                Surface.Children.Add(new Line
                {
                    X1 = ex * DipScale,
                    Y1 = 0,
                    X2 = ex * DipScale,
                    Y2 = ch,
                    Stroke = guideBrush,
                    StrokeThickness = 1,
                });
            }

            // Perpendicular tick from the edge inward.
            var (tickX, tickY) = side switch
            {
                Side.Left => (ex + tick, ey),
                Side.Right => (ex - tick, ey),
                Side.Top => (ex, ey + tick),
                Side.Bottom => (ex, ey - tick),
                _ => (ex, ey),
            };
            Surface.Children.Add(new Line
            {
                X1 = ex * DipScale,
                Y1 = ey * DipScale,
                X2 = tickX * DipScale,
                Y2 = tickY * DipScale,
                Stroke = handleBrush,
                StrokeThickness = 2,
            });

            // Knob, offset a little inward so it is easy to grab.
            var (knobX, knobY) = side switch
            {
                Side.Left => (ex + knobInset, ey),
                Side.Right => (ex - knobInset, ey),
                Side.Top => (ex, ey + knobInset),
                Side.Bottom => (ex, ey - knobInset),
                _ => (ex, ey),
            };
            var knob = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = handleBrush,
                Stroke = Brushes.White,
                StrokeThickness = 1,
            };
            Canvas.SetLeft(knob, knobX * DipScale - 7);
            Canvas.SetTop(knob, knobY * DipScale - 7);
            Surface.Children.Add(knob);

            var pct = new TextBlock
            {
                Text = $"{n * 100:0.#}%",
                Foreground = handleBrush,
                FontSize = 11,
            };
            Canvas.SetLeft(pct, knobX * DipScale + 10);
            Canvas.SetTop(pct, knobY * DipScale - 18);
            Surface.Children.Add(pct);

            _handles.Add(new HandleHit(profile, i, isA, new Point(knobX * DipScale, knobY * DipScale)));
        }
    }

    // --- interaction ---------------------------------------------------------

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_mode() != OverlayMode.Edit)
            return;

        var pos = e.GetPosition(Surface);

        if (e.ClickCount == 2)
        {
            TryAddPoint(pos);
            return;
        }

        var hit = HitHandle(pos);
        if (hit is null)
            return;

        _active = hit;
        Surface.CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_active is null || e.LeftButton != MouseButtonState.Pressed)
            return;

        var profile = _active.Profile;
        var index = _active.Index;
        var points = profile.Points;
        if (index < 0 || index >= points.Count)
            return;

        var pos = e.GetPosition(Surface);
        var localX = pos.X * _display.Scale;
        var localY = pos.Y * _display.Scale;
        var side = _active.IsA ? profile.ASide : profile.BSide;

        var n = side.IsVerticalEdge()
            ? localY / (_display.Height - 1)
            : localX / (_display.Width - 1);
        n = Math.Clamp(n, 0, 1);

        // Keep handles ordered so the piecewise map stays well-defined.
        if (_active.IsA)
        {
            var low = index > 0 ? points[index - 1].A : 0;
            var high = index < points.Count - 1 ? points[index + 1].A : 1;
            n = Math.Clamp(n, low, high);
        }
        else
        {
            var low = index > 0 ? points[index - 1].B : 0;
            var high = index < points.Count - 1 ? points[index + 1].B : 1;
            if (low > high)
                (low, high) = (high, low);
            n = Math.Clamp(n, low, high);
        }

        var cp = points[index];
        if (_active.IsA)
            cp.A = n;
        else
            cp.B = n;
        points[index] = cp;

        _changed();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_active is null)
            return;

        _active = null;
        Surface.ReleaseMouseCapture();
    }

    private void OnRightMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_mode() != OverlayMode.Edit)
            return;

        var hit = HitHandle(e.GetPosition(Surface));
        if (hit is null)
            return;

        if (hit.Profile.Points.Count <= 2)
            return;

        hit.Profile.Points.RemoveAt(hit.Index);
        _changed();
        e.Handled = true;
    }

    private void OnDone(object sender, RoutedEventArgs e) => _done();

    private void TryAddPoint(Point canvasPos)
    {
        var localX = canvasPos.X * _display.Scale;
        var localY = canvasPos.Y * _display.Scale;

        BorderProfile? best = null;
        var bestIsA = true;
        var bestDistance = double.MaxValue;
        var bestNorm = 0.5;

        foreach (var profile in _config.Borders)
        {
            var isA = profile.AId == _display.Id;
            var isB = profile.BId == _display.Id;
            if (!isA && !isB)
                continue;

            var side = isA ? profile.ASide : profile.BSide;
            var (distance, n) = DistanceToEdge(side, localX, localY, _display.Width, _display.Height);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = profile;
                bestIsA = isA;
                bestNorm = n;
            }
        }

        if (best is null || bestDistance > 48)
            return;

        var counterpart = bestIsA ? best.MapAToB(bestNorm) : best.MapBToA(bestNorm);
        if (double.IsNaN(counterpart))
            counterpart = bestIsA ? NearestB(best, bestNorm) : NearestA(best, bestNorm);

        best.Points.Add(bestIsA
            ? new ControlPoint(bestNorm, counterpart)
            : new ControlPoint(counterpart, bestNorm));
        best.Normalize();

        _changed();
    }

    // --- helpers -------------------------------------------------------------

    private HandleHit? HitHandle(Point pos) => _handles
        .Select(h => (Hit: h, Distance: (h.CanvasPos - pos).Length))
        .Where(x => x.Distance <= 12)
        .OrderBy(x => x.Distance)
        .Select(x => x.Hit)
        .FirstOrDefault();

    private static (double X, double Y) EdgeLocalPixels(Side side, double n, int w, int h)
    {
        n = Math.Clamp(n, 0, 1);
        return side switch
        {
            Side.Left => (0, n * (h - 1)),
            Side.Right => (w - 1, n * (h - 1)),
            Side.Top => (n * (w - 1), 0),
            Side.Bottom => (n * (w - 1), h - 1),
            _ => (0, 0),
        };
    }

    private static (double Distance, double Norm) DistanceToEdge(
        Side side, double x, double y, int w, int h)
    {
        var n = side.IsVerticalEdge() ? y / (h - 1) : x / (w - 1);
        n = Math.Clamp(n, 0, 1);
        var (ex, ey) = EdgeLocalPixels(side, n, w, h);
        var dx = x - ex;
        var dy = y - ey;
        return (Math.Sqrt(dx * dx + dy * dy), n);
    }

    private static double NearestB(BorderProfile profile, double a)
        => profile.Points.OrderBy(p => Math.Abs(p.A - a)).First().B;

    private static double NearestA(BorderProfile profile, double b)
        => profile.Points.OrderBy(p => Math.Abs(p.B - b)).First().A;
}
