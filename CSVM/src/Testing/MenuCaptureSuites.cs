using System.Collections.Generic;
using System.IO;
using CSVM.UI;
using Godot;

namespace CSVM.Testing;

/// <summary>
/// BL-489: the screenshot key on the menu screens. The key is an input, so the check pushes a real
/// <see cref="InputEventKey"/> through the real viewport with a real menu standing, rather than
/// calling the capture directly: what has to hold is that a press reaches the capture, and that the
/// menu is the thing that takes it. Both layouts are covered, since the launchscreen is built as
/// controls and the campaign screens are drawn through <c>ComposedBoardView</c>.
/// </summary>
internal static class MenuCaptureSuites
{
    // BL-489: the screenshot key reached the launcher only when no menu was up, so a menu
    // defect could be described but not shown.
    [Suite("menu-screenshot-key",
        "the screenshot key on the menu screens: a real key event is pushed through the real "
        + "viewport with a LaunchMenu standing, once on the launchscreen and once on a "
        + "campaign board, and each press must leave one more file in the folder the flight "
        + "capture writes to")]
    internal static void MenuScreenshotKey(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var written = new List<string>();
        var menu = MenuSuiteHost.Menu(ctx);
        ctx.Host.AddChild(menu);
        try
        {
            menu.ShowMenu();
            Press(ctx, written, "the launchscreen");
            menu.ShowMenu("campaign-briefing:0");
            ctx.Check(menu.ShownBoard != null,
                $"the campaign board really was up for its press (ComposedBoardView, not the control layout)");
            Press(ctx, written, "a campaign board");
        }
        finally
        {
            ctx.Host.RemoveChild(menu);
            menu.QueueFree();

            // The frames themselves are not the evidence, so the folder a pilot collects their own
            // shots in is left as it was found.
            foreach (var path in written)
            {
                File.Delete(path);
            }
        }
    }

    private static void Press(TestContext ctx, List<string> written, string where)
    {
        var before = new HashSet<string>(Shots());
        var viewport = ctx.Host.GetViewport();
        viewport.PushInput(new InputEventKey { Keycode = Key.F12, PhysicalKeycode = Key.F12, Pressed = true });
        bool taken = viewport.IsInputHandled();
        var fresh = new List<string>();
        foreach (var path in Shots())
        {
            if (!before.Contains(path))
            {
                fresh.Add(path);
                written.Add(path);
            }
        }

        long size = fresh.Count == 1 ? new FileInfo(fresh[0]).Length : 0;
        ctx.Note($"{where}: handled={taken}, new files={fresh.Count}, bytes={size}");
        ctx.Check(fresh.Count == 1,
            $"one press on {where} wrote exactly one file ({fresh.Count}), so the shot lands and nothing captures it twice");
        ctx.Check(taken,
            $"and the menu took the key itself on {where} rather than leaving it to whatever is above it");
        ctx.Check(size > 0, $"and the file written from {where} has content ({size} bytes)");
        if (fresh.Count == 1)
        {
            ctx.Check(Path.GetDirectoryName(fresh[0]) == CaptureDirector.ShotDir(),
                $"the folder a menu shot lands in is the one the flight capture writes to ({CaptureDirector.ShotDir()})");
        }
    }

    private static string[] Shots()
    {
        var dir = CaptureDirector.ShotDir();
        return Directory.Exists(dir) ? Directory.GetFiles(dir, "*.png") : System.Array.Empty<string>();
    }
}
