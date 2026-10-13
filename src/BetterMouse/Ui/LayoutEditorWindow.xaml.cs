using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using BetterMouse.Core;
using DPoint = System.Drawing.Point;
using DRect = System.Drawing.Rectangle;

namespace BetterMouse.Ui;

/// <summary>
/// Direct-manipulation editor for the border correspondence. Displays are drawn in their
/// true relative pixel positions; each authored control point appears as a paired handle
/// on the two facing edges, joined by a dashed connector.
/// </summary>
public partial class LayoutEditorWindow : Window
{
    private readonly DisplayLayout _layout;
    private readonly MouseMappingConfig _config;
    private readonly Action _changed;

    private readonly List<HandleHit> _handles = new();
    private HandleHit? _active;

    private double _scale = 1;
    private double _offsetX;
    private double _offsetY;

    private sealed record HandleHit(BorderProfile Profile, int Index, bool IsA, Point CanvasPos);

    public LayoutEditorWindow(DisplayLayout layout, MouseMappingConfig config, Action changed)
    {
        InitializeComponent();
        _layout = layout;
        _config = config;
        _changed = changed;

        Loaded += (_, _) => Render();
        SizeChanged += (_, _) => Render();
    }

    // --- rendering -----------------------------------------------------------

    private void Render()
    {
        Surface.Children.Clear();
        _handles.Clear();

        var vb = _layout.VirtualBounds;
        if (vb.Width <= 0 || vb.Height <= 0)
            return;

        var availableWidth = Surface.ActualWidth > 10 ? Surface.ActualWidth : Math.Max(10, Width - 16);
        var availableHeight = Surface.ActualHeight > 10 ? Surface.ActualHeight : Math.Max(10, Height - 64);

        const double pad = 24;
        _scale = Math.Min(
            (availableWidth - 2 * pad) / vb.Width,
            (availableHeight - 2 * pad) / vb.Height);
        if (_scale <= 0 || double.IsInfinity(_scale))
            _scale = 0.05;

        _offsetX = (availableWidth - vb.Width * _scale) / 2 - vb.Left * _scale;
        _offsetY = (availableHeight - vb.Height * _scale) / 2 - vb.Top * _scale;

        foreach (var display in _layout.Displays)
            DrawDisplay(display);

        foreach (var profile in _config.Borders)
            DrawProfile(profile);
    }

    private void DrawDisplay(DisplayInfo d)
    {
        var origin = ToCanvas(new DPoint(d.Bounds.Left, d.Bounds.Top));

        var rect = new Rectangle
        {
            Width = d.Bounds.Width * _scale,
            Height = d.Bounds.Height * _scale,
            Fill = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x2A)),
            Stroke = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x63)),
            StrokeThickness = 1,
        };
        Canvas.SetLeft(rect, origin.X);
        Canvas.SetTop(rect, origin.Y);
        Surface.Children.Add(rect);

        var label = new TextBlock
        {
            Text = $"{d.Name}\n{d.Bounds.Width}×{d.Bounds.Height} @ {d.ScalePercent}" +
                   (d.IsPrimary ? "  (primary)" : string.Empty),
            Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xD0)),
            FontSize = 11,
            TextAlignment = TextAlignment.Left,
        };
        Canvas.SetLeft(label, origin.X + 7);
        Canvas.SetTop(label, origin.Y + 6);
        Surface.Children.Add(label);
    }

    private void DrawProfile(BorderProfile profile)
    {
        var a = _layout.ById(profile.AId);
        var b = _layout.ById(profile.BId);
        if (a is null || b is null)
            return;

        for (var i = 0; i < profile.Points.Count; i++)
        {
            var cp = profile.Points[i];
            var aCanvas = ToCanvas(EdgeWorld(a.Bounds, profile.ASide, cp.A));
            var bCanvas = ToCanvas(EdgeWorld(b.Bounds, profile.BSide, cp.B));

            Surface.Children.Add(new Line
            {
                X1 = aCanvas.X,
                Y1 = aCanvas.Y,
                X2 = bCanvas.X,
                Y2 = bCanvas.Y,
                Stroke = new SolidColorBrush(Color.FromArgb(0x80, 0x4E, 0xC9, 0xB0)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 3 },
            });

            AddHandle(profile, i, true, aCanvas, Color.FromRgb(0x4E, 0xC9, 0xB0));
            AddHandle(profile, i, false, bCanvas, Color.FromRgb(0xE0, 0x9F, 0x3E));
        }
    }

    private void AddHandle(BorderProfile profile, int index, bool isA, Point canvas, Color color)
    {
        var dot = new Ellipse
        {
            Width = 11,
            Height = 11,
            Fill = new SolidColorBrush(color),
            Stroke = Brushes.White,
            StrokeThickness = 1,
        };
        Canvas.SetLeft(dot, canvas.X - 5.5);
        Canvas.SetTop(dot, canvas.Y - 5.5);
        Surface.Children.Add(dot);

        _handles.Add(new HandleHit(profile, index, isA, canvas));
    }

    // --- interaction ---------------------------------------------------------

    private void OnSurfaceMouseDown(object sender, MouseButtonEventArgs e)
    {
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

    private void OnSurfaceMouseMove(object sender, MouseEventArgs e)
    {
        if (_active is null || e.LeftButton != MouseButtonState.Pressed)
            return;

        var profile = _active.Profile;
        var a = _layout.ById(profile.AId);
        var b = _layout.ById(profile.BId);
        if (a is null || b is null)
            return;

        var world = ToWorld(e.GetPosition(Surface));
        var index = _active.Index;
        var points = profile.Points;
        if (index < 0 || index >= points.Count)
            return;

        var norm = Math.Clamp(
            _active.IsA
                ? NormalizeOnEdge(a.Bounds, profile.ASide, world)
                : NormalizeOnEdge(b.Bounds, profile.BSide, world),
            0, 1);

        // Keep handles ordered so the piecewise map stays well-defined.
        var low = index > 0 ? (profile.Points[index - 1].A) : 0;
        var high = index < points.Count - 1 ? (profile.Points[index + 1].A) : 1;
        if (_active.IsA)
        {
            norm = Math.Clamp(norm, low, high);
        }
        else
        {
            var bLow = index > 0 ? profile.Points[index - 1].B : 0;
            var bHigh = index < points.Count - 1 ? profile.Points[index + 1].B : 1;
            if (bLow > bHigh)
                (bLow, bHigh) = (bHigh, bLow);
            norm = Math.Clamp(norm, bLow, bHigh);
        }

        var cp = points[index];
        if (_active.IsA)
            cp.A = norm;
        else
            cp.B = norm;
        points[index] = cp;

        _changed();
        Render();
    }

    private void OnSurfaceMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_active is null)
            return;

        _active = null;
        Surface.ReleaseMouseCapture();
    }

    private void OnSurfaceRightMouseDown(object sender, MouseButtonEventArgs e)
    {
        var hit = HitHandle(e.GetPosition(Surface));
        if (hit is null)
            return;

        if (hit.Profile.Points.Count <= 2)
            return;

        hit.Profile.Points.RemoveAt(hit.Index);
        _changed();
        Render();
        e.Handled = true;
    }

    private void TryAddPoint(Point canvasPos)
    {
        var world = ToWorld(canvasPos);

        BorderProfile? bestProfile = null;
        var bestIsA = true;
        var bestDist = 18.0;
        var bestNorm = 0.5;

        foreach (var profile in _config.Borders)
        {
            var a = _layout.ById(profile.AId);
            var b = _layout.ById(profile.BId);
            if (a is null || b is null)
                continue;

            var (da, na) = DistanceToEdge(a.Bounds, profile.ASide, world);
            if (da < bestDist)
            {
                bestDist = da;
                bestProfile = profile;
                bestIsA = true;
                bestNorm = na;
            }

            var (db, nb) = DistanceToEdge(b.Bounds, profile.BSide, world);
            if (db < bestDist)
            {
                bestDist = db;
                bestProfile = profile;
                bestIsA = false;
                bestNorm = nb;
            }
        }

        if (bestProfile is null)
            return;

        double counterpart;
        if (bestIsA)
        {
            counterpart = bestProfile.MapAToB(bestNorm);
            if (double.IsNaN(counterpart))
                counterpart = NearestB(bestProfile, bestNorm);
        }
        else
        {
            counterpart = bestProfile.MapBToA(bestNorm);
            if (double.IsNaN(counterpart))
                counterpart = NearestA(bestProfile, bestNorm);
        }

        bestProfile.Points.Add(bestIsA
            ? new ControlPoint(bestNorm, counterpart)
            : new ControlPoint(counterpart, bestNorm));
        bestProfile.Normalize();

        _changed();
        Render();
    }

    private HandleHit? HitHandle(Point pos) => _handles
        .Select(h => (Hit: h, Dist: (h.CanvasPos - pos).Length))
        .Where(x => x.Dist <= 9)
        .OrderBy(x => x.Dist)
        .Select(x => x.Hit)
        .FirstOrDefault();

    private void OnSave(object sender, RoutedEventArgs e) => _changed();

    private void OnReset(object sender, RoutedEventArgs e)
    {
        _config.ResetToDefaults(_layout);
        _changed();
        Render();
    }

    // --- coordinate helpers --------------------------------------------------

    private Point ToCanvas(DPoint p) => new(p.X * _scale + _offsetX, p.Y * _scale + _offsetY);

    private DPoint ToWorld(Point canvas) => new(
        (int)Math.Round((canvas.X - _offsetX) / _scale),
        (int)Math.Round((canvas.Y - _offsetY) / _scale));

    private static DPoint EdgeWorld(DRect r, Side side, double n)
    {
        n = Math.Clamp(n, 0, 1);
        return side switch
        {
            Side.Left => new DPoint(r.Left, r.Top + (int)Math.Round(n * (r.Height - 1))),
            Side.Right => new DPoint(r.Right - 1, r.Top + (int)Math.Round(n * (r.Height - 1))),
            Side.Top => new DPoint(r.Left + (int)Math.Round(n * (r.Width - 1)), r.Top),
            Side.Bottom => new DPoint(r.Left + (int)Math.Round(n * (r.Width - 1)), r.Bottom - 1),
            _ => new DPoint(r.Left, r.Top),
        };
    }

    private static double NormalizeOnEdge(DRect r, Side side, DPoint p)
    {
        var v = side.IsVerticalEdge()
            ? (p.Y - r.Top) / (double)r.Height
            : (p.X - r.Left) / (double)r.Width;
        return Math.Clamp(v, 0, 1);
    }

    private static (double Distance, double Norm) DistanceToEdge(DRect r, Side side, DPoint p)
    {
        var n = NormalizeOnEdge(r, side, p);
        var edge = EdgeWorld(r, side, n);
        var dx = p.X - edge.X;
        var dy = p.Y - edge.Y;
        return (Math.Sqrt(dx * dx + dy * dy), n);
    }

    private static double NearestB(BorderProfile profile, double a)
        => profile.Points.OrderBy(p => Math.Abs(p.A - a)).First().B;

    private static double NearestA(BorderProfile profile, double b)
        => profile.Points.OrderBy(p => Math.Abs(p.B - b)).First().A;
}
