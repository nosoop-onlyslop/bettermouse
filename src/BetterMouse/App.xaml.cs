using System;
using System.Threading;
using System.Windows;
using BetterMouse.Configuration;
using BetterMouse.Core;
using BetterMouse.Input;
using BetterMouse.Ui;
using Microsoft.Win32;

namespace BetterMouse;

public partial class App : Application
{
    private Mutex? _instanceMutex;
    private RemapEngine _engine = null!;
    private MouseHook _hook = null!;
    private TrayController _tray = null!;
    private LayoutEditorWindow? _editor;
    private bool _disposed;

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

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public void SaveConfig() => ConfigStore.Save(Config);

    public void ReloadDisplays()
    {
        Layout = DisplayEnumerator.Enumerate();
        Config.EnsureDefaults(Layout);
        _engine.Layout = Layout;
        SaveConfig();
    }

    public void ResetLayout()
    {
        Config.ResetToDefaults(Layout);
        SaveConfig();
    }

    public void ShowEditor()
    {
        if (_editor is not null)
        {
            _editor.Activate();
            return;
        }

        _engine.Suspended = true;
        _editor = new LayoutEditorWindow(Layout, Config, () =>
        {
            _engine.Layout = Layout;
            SaveConfig();
        });
        _editor.Closed += (_, _) =>
        {
            _editor = null;
            _engine.Suspended = false;
        };
        _editor.Show();
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
        _tray?.Dispose();
        _instanceMutex?.Dispose();
        _instanceMutex = null;
    }
}
