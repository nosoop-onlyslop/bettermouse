using System;
using System.Windows.Forms;
using BetterMouse.Overlay;

namespace BetterMouse;

/// <summary>Right-click tray menu, modelled on MouseUnSnag's minimal tray UX.</summary>
internal sealed class TrayController : IDisposable
{
    private readonly NotifyIcon _icon;

    public TrayController(App app)
    {
        var menu = new ContextMenuStrip();

        var enabled = new ToolStripMenuItem("Enabled")
        {
            CheckOnClick = true,
            Checked = app.Config.Enabled,
        };
        enabled.CheckedChanged += (_, _) =>
        {
            app.Config.Enabled = enabled.Checked;
            app.SaveConfig();
        };

        var unstick = new ToolStripMenuItem("Unstick from corners")
        {
            CheckOnClick = true,
            Checked = app.Config.Unstick,
        };
        unstick.CheckedChanged += (_, _) =>
        {
            app.Config.Unstick = unstick.Checked;
            app.SaveConfig();
        };

        var jump = new ToolStripMenuItem("Jump between displays")
        {
            CheckOnClick = true,
            Checked = app.Config.Jump,
        };
        jump.CheckedChanged += (_, _) =>
        {
            app.Config.Jump = jump.Checked;
            app.SaveConfig();
        };

        var wrap = new ToolStripMenuItem("Wrap around desktop")
        {
            CheckOnClick = true,
            Checked = app.Config.Wrap,
        };
        wrap.CheckedChanged += (_, _) =>
        {
            app.Config.Wrap = wrap.Checked;
            app.SaveConfig();
        };

        var showOverlay = new ToolStripMenuItem("Show overlay");
        var editOverlay = new ToolStripMenuItem("Edit overlay");

        void SyncOverlay()
        {
            showOverlay.Checked = app.OverlayMode != OverlayMode.Off;
            editOverlay.Checked = app.OverlayMode == OverlayMode.Edit;
        }

        showOverlay.Click += (_, _) => app.SetOverlayMode(
            app.OverlayMode == OverlayMode.Off ? OverlayMode.View : OverlayMode.Off);
        editOverlay.Click += (_, _) => app.SetOverlayMode(
            app.OverlayMode == OverlayMode.Edit ? OverlayMode.View : OverlayMode.Edit);
        app.OverlayModeChanged += SyncOverlay;
        SyncOverlay();

        var edit = new ToolStripMenuItem("Editor window...");
        edit.Click += (_, _) => app.ShowEditor();

        var reset = new ToolStripMenuItem("Reset layout to defaults");
        reset.Click += (_, _) => app.ResetLayout();

        var reload = new ToolStripMenuItem("Reload displays");
        reload.Click += (_, _) => app.ReloadDisplays();

        var open = new ToolStripMenuItem("Open config file");
        open.Click += (_, _) => app.OpenConfigFile();

        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => app.ExitApplication();

        menu.Items.Add(enabled);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(unstick);
        menu.Items.Add(jump);
        menu.Items.Add(wrap);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(showOverlay);
        menu.Items.Add(editOverlay);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(edit);
        menu.Items.Add(reset);
        menu.Items.Add(reload);
        menu.Items.Add(open);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);

        _icon = new NotifyIcon
        {
            Icon = AppIcon.Load(),
            Text = "BetterMouse",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => app.ShowEditor();
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
