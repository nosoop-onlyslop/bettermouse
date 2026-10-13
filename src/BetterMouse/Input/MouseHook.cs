using System;
using System.Drawing;
using System.Runtime.InteropServices;
using BetterMouse.Core;

namespace BetterMouse.Input;

/// <summary>
/// Installs the WH_MOUSE_LL hook and routes every move through the <see cref="RemapEngine"/>.
/// Must be installed on the thread running the UI message loop so the callback stays on that
/// thread (and so layout edits are not concurrent with a callback).
/// </summary>
internal sealed class MouseHook : IDisposable
{
    private readonly RemapEngine _engine;
    private readonly ICursorEnv _env;

    private NativeMethods.HookProc? _proc;
    private IntPtr _hook = IntPtr.Zero;
    private bool _inCallback;

    public MouseHook(RemapEngine engine, ICursorEnv env)
    {
        _engine = engine;
        _env = env;
    }

    public bool IsInstalled => _hook != IntPtr.Zero;

    public void Install()
    {
        if (_hook != IntPtr.Zero)
            return;

        _proc = Callback;
        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            _proc,
            NativeMethods.GetModuleHandle(null),
            0);
    }

    public void Uninstall()
    {
        if (_hook == IntPtr.Zero)
            return;

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // SetCursorPos from inside the hook can re-enter; let the nested event through.
        if (_inCallback)
            return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);

        _inCallback = true;
        try
        {
            if (nCode >= 0 && wParam == (IntPtr)NativeMethods.WM_MOUSEMOVE)
            {
                var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                var raw = new Point(data.pt.X, data.pt.Y);

                if (_engine.HandleMove(raw, _env))
                    return (IntPtr)1;
            }
        }
        catch
        {
            // Never let an exception unwind across the hook boundary.
        }
        finally
        {
            _inCallback = false;
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
