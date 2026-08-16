using System;
using Godot;

namespace CSVM.Utils;

/// <summary>Win32-only window hiding for scripted runs.</summary>
public static class ScriptedWindow
{
    private const int SwHide = 0;

    /// <summary>Takes a scripted run's window off the screen entirely. Godot has no lever for
    /// this: <c>no_focus</c> only stops the window taking keyboard focus, and <c>--position</c>
    /// is clamped to keep part of the window on the desktop. Decode: docs/verification.md SHOT-16.
    /// ⚠ Hide, never minimize. A minimized window stops rendering and turns captures blank.</summary>
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

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);
}
