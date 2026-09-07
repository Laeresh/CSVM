using System.IO;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The display settings between the VIDEO page and the engine: the precedence the sources
/// resolve in, and what the engine reads back once a choice is applied. The page is driven as a
/// bare <see cref="OriginalShell"/> over the install's decoded layout, since the question here is
/// the setting rather than the presentation boundary <see cref="MenuOriginalSuites"/> drives.
/// ⚠ The store is pointed at this suite's own scratch directory and the window's pacing is put
/// back in a finally, so nothing here reads the options saved at this machine's controls or leaves
/// the rest of the run on a frame cap.</summary>
internal static class DisplaySettingsSuites
{
    [Suite("display-vsync",
        "The V-Sync setting: --no-vsync beats a saved cap, the saved word beats the display.vsync "
        + "config key, the key beats the default and the default is V-Sync on, a word the vocabulary "
        + "does not know reads as never set, a --det launch reads no saved word while a plain one "
        + "does, the VIDEO page opens on the saved word, and applying what its ACCEPT CHANGES "
        + "carried leaves Engine.MaxFps and DisplayServer.WindowGetVsyncMode reading back the cap")]
    internal static void DisplayVSync(TestContext ctx)
    {
        var flag = VSyncSetting.Resolve(flagOff: true, savedWord: "144", configOn: true);
        ctx.Check(!flag.Enabled && flag.MaxFps == VSyncSetting.Uncapped && flag.Source == "--no-vsync",
            $"--no-vsync beats a saved 144 cap and leaves the loop uncapped ({Describe(flag)})");
        var savedCap = VSyncSetting.Resolve(false, "144", true);
        ctx.Check(!savedCap.Enabled && savedCap.MaxFps == 144 && savedCap.Source == "options.json",
            $"the saved cap is V-Sync off at that rate ({Describe(savedCap)})");
        var savedOn = VSyncSetting.Resolve(false, DisplayWords.VSyncOn, false);
        ctx.Check(savedOn.Enabled && savedOn.Source == "options.json",
            $"and a saved on beats a config key that says off ({Describe(savedOn)})");
        var key = VSyncSetting.Resolve(false, null, false);
        ctx.Check(!key.Enabled && key.MaxFps == VSyncSetting.Uncapped && key.Source == VSyncSetting.Key,
            $"with nothing saved the config key decides ({Describe(key)})");
        var fallback = VSyncSetting.Resolve(false, null, true);
        ctx.Check(fallback.Enabled && fallback.MaxFps == VSyncSetting.Uncapped,
            $"and with neither, V-Sync is on, the behaviour with no options file ({Describe(fallback)})");
        var unknown = VSyncSetting.Resolve(false, "sometimes", true);
        ctx.Check(unknown.Enabled && unknown.Source == fallback.Source,
            $"a word the vocabulary does not know reads as never set rather than as off ({Describe(unknown)})");

        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        string dir = Path.Combine(ctx.ScratchDir, "display-vsync");
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }

        Directory.CreateDirectory(dir);
        string? previous = OptionsStore.DirectoryOverride;
        OptionsStore.DirectoryOverride = dir;
        try
        {
            OptionsStore.UserOptions().Save(new OptionsDef { VSync = "144" });
            ctx.Check(VSyncSetting.SavedWord(det: false) == "144",
                $"a plain launch reads the saved word ({VSyncSetting.SavedWord(det: false) ?? "unset"})");
            ctx.Check(VSyncSetting.SavedWord(det: true) == null,
                $"and a --det launch reads no saved display setting at all ({VSyncSetting.SavedWord(det: true) ?? "unset"})");
            AppliedRow(ctx, layout);
        }
        finally
        {
            OptionsStore.DirectoryOverride = previous;
        }
    }

    // The row driven and its choice applied: the page opens on the saved word, ACCEPT CHANGES hands
    // it back on the exit, and the two engine calls the apply makes are read back off the engine
    // rather than trusted. The window's pacing is restored before anything else in the run sees it.
    private static void AppliedRow(TestContext ctx, MenuLayout layout)
    {
        var shell = new OriginalShell(layout, new FreeFlightFeature(), new PlayerSetupFeature(),
            _ => null, options: () => OptionsStore.UserOptions().Load());
        shell.OpenVideo();
        ctx.Check(shell.Screen == OriginalScreen.Video && shell.VSyncChoice == "144",
            $"the VIDEO page opens showing the saved cap ({shell.Screen}, {shell.VSyncChoice ?? "unset"})");
        var applied = Accept(shell);
        ctx.Check(applied?.VSync == "144", $"ACCEPT CHANGES carries it on the apply exit ({applied?.VSync ?? "no exit"})");

        var entry = new VSyncPlan(DisplayServer.WindowGetVsyncMode() != DisplayServer.VSyncMode.Disabled, Engine.MaxFps, "restored");
        try
        {
            VSyncSetting.Apply(VSyncSetting.Resolve(false, applied?.VSync, true));
            ctx.Check(Engine.MaxFps == 144 && DisplayServer.WindowGetVsyncMode() == DisplayServer.VSyncMode.Disabled,
                $"applying it caps the frame loop at 144 with V-Sync off (max_fps={Engine.MaxFps}, {DisplayServer.WindowGetVsyncMode()})");
            VSyncSetting.Apply(VSyncSetting.Resolve(false, DisplayWords.VSyncOn, true));
            ctx.Check(Engine.MaxFps == VSyncSetting.Uncapped && DisplayServer.WindowGetVsyncMode() == DisplayServer.VSyncMode.Enabled,
                $"the On word puts it back on the screen with no cap (max_fps={Engine.MaxFps}, {DisplayServer.WindowGetVsyncMode()})");
            VSyncSetting.Apply(VSyncSetting.Resolve(true, "144", true));
            ctx.Check(Engine.MaxFps == VSyncSetting.Uncapped && DisplayServer.WindowGetVsyncMode() == DisplayServer.VSyncMode.Disabled,
                $"and --no-vsync applies as an uncapped loop over the same saved cap (max_fps={Engine.MaxFps})");
        }
        finally
        {
            VSyncSetting.Apply(entry);
        }
    }

    // Walks the page's focus onto ACCEPT CHANGES and presses it, the keyboard's own way out.
    private static OptionsApplyExit? Accept(OriginalShell shell)
    {
        for (int guard = 0; guard < 32 && shell.FocusedKey != OriginalShell.VideoAcceptKey; guard++)
        {
            shell.Step(new MenuCommands { MoveY = 1 });
        }

        return shell.Step(new MenuCommands { Accept = true }).Exit as OptionsApplyExit;
    }

    private static string Describe(VSyncPlan plan) =>
        $"vsync {(plan.Enabled ? "on" : "off")} max_fps={plan.MaxFps} source={plan.Source}";
}
