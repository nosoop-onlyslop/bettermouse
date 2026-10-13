using System;
using System.Linq;
using System.Threading;
using System.Windows;
using BetterMouse.Configuration;
using BetterMouse.Core;
using BetterMouse.Input;
using BetterMouse.Overlay;
using BetterMouse.Ui;
using Microsoft.Win32;

namespace BetterMouse;

public partial class App : Application
{
    private Mutex? _instanceMutex;
    private RemapEngine _engine = null!;
    private MouseHook _hook = null!;
    private TrayController _tray = null!;
    private OverlayManager _overlay = null!;
    private LayoutEditorWindow? _editor;
    private bool _disposed;

    public event Action? OverlayModeChanged;

    public DisplayLayout Layout { get; private set; } = null!;

    public MouseMappingConfig Config { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceMutex = new Mutex(true, "__BetterMouse_EXE__", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("BetterMouse is already running.", "BetterMouse",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        Config = ConfigStore.Load();
        Layout = DisplayEnumerator.Enumerate();
        Config.EnsureDefaults(Layout);

        _engine = new RemapEngine(Layout, Config);
        _hook = new MouseHook(_engine, new Win32CursorEnv());
        _hook.Install();

        _tray = new TrayController(this);
        _overlay = new OverlayManager(this);

        // Optional start-up modes, handy for scripted/visual checks:
        //   --overlay        show the physical overlay (click-through)
        //   --edit-overlay   show it in interactive edit mode
        var startEdit = e.Args.Any(a => string.Equals(a, "--edit-overlay", StringComparison.OrdinalIgnoreCase));
        var startView = startEdit || e.Args.Any(a => string.Equals(a, "--overlay", StringComparison.OrdinalIgnoreCase));
        if (startEdit)
            SetOverlayMode(OverlayMode.Edit);
        else if (startView || Config.ShowOverlay)
            SetOverlayMode(OverlayMode.View);

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public OverlayMode OverlayMode => _overlay?.Mode ?? OverlayMode.Off;

    public void SetOverlayMode(OverlayMode mode)
    {
        Config.ShowOverlay = mode != OverlayMode.Off;
        _overlay.SetMode(mode);
        SaveConfig();
        UpdateSuspension();
        OverlayModeChanged?.Invoke();
    }

    /// <summary>Called by an overlay after the user edits a handle.</summary>
    public void OnOverlayChanged()
    {
        SaveConfig();
        _overlay.RefreshAll();
    }

    private void UpdateSuspension()
    {
        _engine.Suspended = _editor is not null || _overlay?.Mode == OverlayMode.Edit;
    }

    public void SaveConfig() => ConfigStore.Save(Config);

    public void ReloadDisplays()
    {
        Layout = DisplayEnumerator.Enumerate();
        Config.EnsureDefaults(Layout);
        _engine.Layout = Layout;
        _overlay?.Rebuild();
        SaveConfig();
    }

    public void ResetLayout()
    {
        Config.ResetToDefaults(Layout);
        SaveConfig();
        _overlay?.RefreshAll();
    }

    public void ShowEditor()
    {
        if (_editor is not null)
        {
            _editor.Activate();
            return;
        }

        _editor = new LayoutEditorWindow(Layout, Config, () =>
        {
            _engine.Layout = Layout;
            SaveConfig();
            _overlay?.RefreshAll();
        });
        _editor.Closed += (_, _) =>
        {
            _editor = null;
            UpdateSuspension();
        };
        _editor.Show();
        UpdateSuspension();
    }

    public void OpenConfigFile()
    {
        try
        {
            if (!System.IO.File.Exists(ConfigStore.FilePath))
                SaveConfig();

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = ConfigStore.FilePath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "BetterMouse", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void ExitApplication()
    {
        DisposeServices();
        Shutdown();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(ReloadDisplays));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DisposeServices();
        base.OnExit(e);
    }

    private void DisposeServices()
    {
        if (_disposed)
            return;
        _disposed = true;

        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _hook?.Uninstall();
        _overlay?.Dispose();
        _tray?.Dispose();
        _instanceMutex?.Dispose();
        _instanceMutex = null;
    }
}
