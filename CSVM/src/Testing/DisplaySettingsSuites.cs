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
/// ⚠ The store is pointed at each suite's own scratch directory and the window's pacing is put
/// back in a finally, so nothing here reads the options saved at this machine's controls or leaves
/// the rest of the run on a frame cap. The window's mode is read and never set: a scripted run's
/// window is hidden off screen, and putting it back on the screen would land it over whatever the
/// machine is doing.</summary>
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

    [Suite("display-mode",
        "The display-mode setting: the saved word beats the default and the default is windowed, "
        + "which is what project.godot ships, a word the vocabulary does not know reads as never "
        + "set, borderless and exclusive fullscreen resolve to the two engine modes Godot spells "
        + "the other way round, a --det launch reads no saved word while a plain one does, and the "
        + "VIDEO page opens on the saved word and carries it on what ACCEPT CHANGES applies")]
    internal static void DisplayMode(TestContext ctx)
    {
        var saved = DisplayModeSetting.Resolve(DisplayWords.Fullscreen);
        ctx.Check(saved.Mode == DisplayServer.WindowMode.ExclusiveFullscreen && saved.Source == "options.json",
            $"the saved fullscreen word is the exclusive mode ({saved.Mode}, {saved.Source})");
        var borderless = DisplayModeSetting.Resolve(DisplayWords.Borderless);
        ctx.Check(borderless.Mode == DisplayServer.WindowMode.Fullscreen,
            $"and borderless is Godot's Fullscreen, the window that fills the screen ({borderless.Mode})");
        var none = DisplayModeSetting.Resolve(null);
        ctx.Check(none.Mode == DisplayServer.WindowMode.Windowed && none.Word == DisplayWords.Windowed
            && none.Source == "default",
            $"with nothing saved the mode is windowed, project.godot's own ({none.Mode}, {none.Source})");
        var unknown = DisplayModeSetting.Resolve("maximized");
        ctx.Check(unknown.Mode == none.Mode && unknown.Source == none.Source,
            $"a word the vocabulary does not know reads as never set ({unknown.Mode}, {unknown.Source})");
        // The run's own window is the control on the mapping: a --run-tests process is windowed, so
        // the engine's answer and the word's must agree. The mode is never changed here, a scripted
        // run's window being hidden off screen for the rest of the suites to render into.
        ctx.Check(DisplayServer.WindowGetMode() == none.Mode,
            $"and the engine agrees this run's own window is that mode ({DisplayServer.WindowGetMode()})");

        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        string dir = Path.Combine(ctx.ScratchDir, "display-mode");
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }

        Directory.CreateDirectory(dir);
        string? previous = OptionsStore.DirectoryOverride;
        OptionsStore.DirectoryOverride = dir;
        try
        {
            OptionsStore.UserOptions().Save(new OptionsDef { DisplayMode = DisplayWords.Borderless });
            ctx.Check(DisplayModeSetting.SavedWord(det: false) == DisplayWords.Borderless,
                $"a plain launch reads the saved word ({DisplayModeSetting.SavedWord(det: false) ?? "unset"})");
            ctx.Check(DisplayModeSetting.SavedWord(det: true) == null,
                $"and a --det launch reads no saved display setting at all ({DisplayModeSetting.SavedWord(det: true) ?? "unset"})");
            AppliedModeRow(ctx, layout);
        }
        finally
        {
            OptionsStore.DirectoryOverride = previous;
        }
    }

    // The display-mode row driven: the page opens on the saved word and ACCEPT CHANGES hands it back
    // on the exit, which is the word Launcher.ApplyOptions resolves and applies. The mode the plan
    // resolves to is asserted rather than applied; changing a scripted run's window mode would put
    // the hidden window back on the screen every other suite renders behind.
    private static void AppliedModeRow(TestContext ctx, MenuLayout layout)
    {
        var shell = new OriginalShell(layout, new FreeFlightFeature(), new PlayerSetupFeature(),
            _ => null, options: () => OptionsStore.UserOptions().Load());
        shell.OpenVideo();
        ctx.Check(shell.Screen == OriginalScreen.Video && shell.DisplayModeChoice == DisplayWords.Borderless,
            $"the VIDEO page opens showing the saved mode ({shell.Screen}, {shell.DisplayModeChoice ?? "unset"})");
        var applied = Accept(shell);
        ctx.Check(applied?.DisplayMode == DisplayWords.Borderless,
            $"ACCEPT CHANGES carries it on the apply exit ({applied?.DisplayMode ?? "no exit"})");
        var plan = DisplayModeSetting.Resolve(applied?.DisplayMode);
        ctx.Check(plan.Mode == DisplayServer.WindowMode.Fullscreen && plan.Source == "options.json",
            $"and the apply resolves that word to the borderless window mode ({plan.Mode}, {plan.Source})");
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
