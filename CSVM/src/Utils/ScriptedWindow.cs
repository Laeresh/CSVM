using System;
using Godot;

namespace CSVM.Utils;

/// <summary>Win32-only window hiding for scripted runs.</summary>
public static class ScriptedWindow
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    private const int SwHide = 0;

    /// <summary>Takes a scripted run's window off the screen entirely. `no_focus` only stops the
    /// window taking the KEYBOARD — it still opens in front of whatever the user is working in, and
    /// a full RunTests.ps1 does that about twenty times. Godot has no lever for this: there is no
    /// always-on-bottom window flag, and --position is clamped so roughly a third of the window
    /// stays on the desktop whatever you ask for (measured: 5184 and 10000 both land at 4686 on a
    /// 5120-wide desktop). Hiding is not minimizing — a minimized window stops rendering, which
    /// turns the captures blank (SHOT-16).</summary>
    public static void Hide()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        nint hwnd = (nint)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle);
        if (hwnd == 0)
        {
            Log.Debug("core", $"window: no native handle, cannot hide (scripted session)");
            return;
        }
        ShowWindow(hwnd, SwHide);
        Log.Debug("core", $"window: hidden (scripted session)");
    }
}
