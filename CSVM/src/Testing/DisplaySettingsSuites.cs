using System.IO;
using System.Linq;
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
/// the rest of the run on a frame cap. The window's mode and size are read and never set: a
/// scripted run's window is hidden off screen, putting it back would land it over whatever the
/// machine is doing, and a resize would move the viewport every later shot in the process is
/// captured from. The screen is applied only where it is the one the window already stands on,
/// which logs the line a run owes and moves nothing.</summary>
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

    [Suite("display-resolution",
        "The resolution setting: the list a screen offers holds that screen's own size and the "
        + "project default and no size larger than the screen, a saved size the list offers beats "
        + "the default, a saved size it does not offer falls back to the project default rather "
        + "than to the nearest, a --det launch reads no saved size while a plain one does, and the "
        + "VIDEO page opens on the saved size and carries it on what ACCEPT CHANGES applies")]
    internal static void DisplayResolution(TestContext ctx)
    {
        var offered = ResolutionSetting.SizesUnder(1920, 1080);
        ctx.Check(offered.Contains("1920x1080") && offered.Contains(ResolutionSetting.Default)
            && !offered.Contains("2560x1440"),
            $"a 1920x1080 screen offers its own size and the default but nothing larger ({string.Join(" ", offered)})");
        var narrow = ResolutionSetting.SizesUnder(800, 600);
        ctx.Check(narrow.Contains("800x600") && narrow.Contains(ResolutionSetting.Default),
            $"a screen smaller than the default still offers both, so the fallback is always pickable ({string.Join(" ", narrow)})");
        var saved = ResolutionSetting.Resolve("1920x1080", offered);
        ctx.Check(saved is { Width: 1920, Height: 1080, Source: "options.json" },
            $"a saved size the screen offers is the one applied ({Describe(saved)})");
        var unsupported = ResolutionSetting.Resolve("2560x1440", offered);
        ctx.Check(unsupported is { Width: ResolutionSetting.DefaultWidth, Height: ResolutionSetting.DefaultHeight, Source: "default" },
            $"a saved size it does not offer falls back to the project default, not to 1920x1080 ({Describe(unsupported)})");
        var none = ResolutionSetting.Resolve(null, offered);
        ctx.Check(none.Word == ResolutionSetting.Default && none.Source == unsupported.Source,
            $"and nothing saved reads the same way ({Describe(none)})");

        // This run's own screen is the control on the enumeration: every size the list offers has to
        // fit inside what the engine reports, and the screen's own size has to be on it.
        var screen = DisplayServer.ScreenGetSize(DisplayServer.WindowGetCurrentScreen());
        var here = ResolutionSetting.ScreenSizes();
        bool fits = true;
        foreach (var word in here)
        {
            fits &= OptionsStore.TryParseResolution(word, out int w, out int h) && w <= screen.X && h <= screen.Y;
        }

        ctx.Check(fits && here.Contains(OptionsStore.FormatResolution(screen.X, screen.Y)),
            $"this screen's list fits inside the {screen.X}x{screen.Y} the engine reports and holds it ({string.Join(" ", here)})");

        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        string dir = Path.Combine(ctx.ScratchDir, "display-resolution");
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }

        Directory.CreateDirectory(dir);
        string? previous = OptionsStore.DirectoryOverride;
        OptionsStore.DirectoryOverride = dir;
        try
        {
            OptionsStore.UserOptions().Save(new OptionsDef { Resolution = "1024x768" });
            ctx.Check(ResolutionSetting.SavedWord(det: false) == "1024x768",
                $"a plain launch reads the saved size ({ResolutionSetting.SavedWord(det: false) ?? "unset"})");
            ctx.Check(ResolutionSetting.SavedWord(det: true) == null,
                $"and a --det launch reads no saved display setting at all ({ResolutionSetting.SavedWord(det: true) ?? "unset"})");
            AppliedSizeRow(ctx, layout);
        }
        finally
        {
            OptionsStore.DirectoryOverride = previous;
        }
    }

    [Suite("display-monitor",
        "The monitor setting: a saved index the machine has a screen for beats the fallback, an "
        + "index no screen answers to falls back to the screen the window stands on rather than "
        + "reaching the engine, a screen reads as its index and its size, this machine's own "
        + "enumeration matches what the engine reports, a --det launch reads no saved index while a "
        + "plain one does, and the VIDEO page opens on the saved screen and carries it on the apply")]
    internal static void DisplayMonitor(TestContext ctx)
    {
        // Three screens with the second as the fallback. The development machine has one, so the
        // list a multi-monitor machine would enumerate is built by hand: what it proves is the
        // resolve rule, and the move itself is owed at the controls on a machine with two.
        var many = new ScreenList(
            new[] { MonitorSetting.Label(0, 1920, 1080), MonitorSetting.Label(1, 2560, 1440), MonitorSetting.Label(2, 1280, 1024) }, 1);
        ctx.Check(many.Labels[1] == "Screen 1 (2560x1440)",
            $"a screen reads as the index it is saved as and the size the engine reports ({many.Labels[1]})");
        var saved = MonitorSetting.Resolve("2", many);
        ctx.Check(saved is { Screen: 2, Word: "2", Source: "options.json" },
            $"a saved index the machine has a screen for is the one applied ({Describe(saved)})");
        var absent = MonitorSetting.Resolve("7", many);
        ctx.Check(absent is { Screen: 1, Word: "1", Source: "default" },
            $"an index past the last screen falls back to the screen the window stands on ({Describe(absent)})");
        var none = MonitorSetting.Resolve(null, many);
        ctx.Check(none.Screen == absent.Screen && none.Source == absent.Source,
            $"and nothing saved reads the same way ({Describe(none)})");
        var malformed = MonitorSetting.Resolve("01", many);
        ctx.Check(malformed.Screen == absent.Screen,
            $"as does an index that is not the canonical spelling of one ({Describe(malformed)})");

        // This run's own screens are the control on the enumeration: one label per screen the engine
        // counts, and a fallback that names one of them.
        var here = MonitorSetting.Screens();
        ctx.Check(here.Labels.Count == DisplayServer.GetScreenCount() && here.Fallback >= 0
            && here.Fallback < here.Labels.Count,
            $"this machine enumerates one label per screen with the window on {here.Fallback} ({string.Join(" ", here.Labels)})");
        int standing = DisplayServer.WindowGetCurrentScreen();
        MonitorSetting.Apply(MonitorSetting.Resolve(MonitorSetting.Word(standing), here));
        ctx.Check(DisplayServer.WindowGetCurrentScreen() == standing,
            $"and applying the screen it already stands on logs the line without moving it (screen {standing})");

        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        string dir = Path.Combine(ctx.ScratchDir, "display-monitor");
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }

        Directory.CreateDirectory(dir);
        string? previous = OptionsStore.DirectoryOverride;
        OptionsStore.DirectoryOverride = dir;
        try
        {
            OptionsStore.UserOptions().Save(new OptionsDef { MonitorIndex = MonitorSetting.Word(standing) });
            ctx.Check(MonitorSetting.SavedWord(det: false) == MonitorSetting.Word(standing),
                $"a plain launch reads the saved index ({MonitorSetting.SavedWord(det: false) ?? "unset"})");
            ctx.Check(MonitorSetting.SavedWord(det: true) == null,
                $"and a --det launch reads no saved display setting at all ({MonitorSetting.SavedWord(det: true) ?? "unset"})");
            AppliedMonitorRow(ctx, layout, standing);
        }
        finally
        {
            OptionsStore.DirectoryOverride = previous;
        }
    }

    // The monitor row driven: the page opens on the saved screen, draws that screen's own label and
    // hands the index back on the exit, which is what Launcher.ApplyOptions resolves and applies
    // before the mode and the size. The plan is asserted rather than applied to another screen; a
    // scripted run's window is hidden off screen and moving it would put it over the desktop.
    private static void AppliedMonitorRow(TestContext ctx, MenuLayout layout, int standing)
    {
        var shell = new OriginalShell(layout, new FreeFlightFeature(), new PlayerSetupFeature(),
            _ => null, options: () => OptionsStore.UserOptions().Load(), screens: MonitorSetting.Screens);
        shell.OpenVideo();
        ctx.Check(shell.Screen == OriginalScreen.Video && shell.MonitorChoice == MonitorSetting.Word(standing),
            $"the VIDEO page opens showing the saved screen ({shell.Screen}, {shell.MonitorChoice ?? "unset"})");
        ctx.Check(Label(shell, OriginalShell.MonitorKey) == MonitorSetting.Screens().Labels[standing],
            $"with the row drawing that screen's label ({Label(shell, OriginalShell.MonitorKey)})");
        var applied = Accept(shell);
        ctx.Check(applied?.MonitorIndex == MonitorSetting.Word(standing),
            $"ACCEPT CHANGES carries it on the apply exit ({applied?.MonitorIndex ?? "no exit"})");
        var plan = MonitorSetting.Resolve(applied?.MonitorIndex, MonitorSetting.Screens());
        ctx.Check(plan.Screen == standing && plan.Source == "options.json",
            $"and the apply resolves that index to the screen the window would move to ({Describe(plan)})");
    }

    // The resolution row driven: the page opens on the saved size and ACCEPT CHANGES hands it back on
    // the exit, which is the size Launcher.ApplyOptions resolves and applies. The plan is asserted
    // rather than applied; resizing a scripted run's window would move the viewport every later shot
    // in this process is captured from.
    private static void AppliedSizeRow(TestContext ctx, MenuLayout layout)
    {
        var shell = new OriginalShell(layout, new FreeFlightFeature(), new PlayerSetupFeature(),
            _ => null, options: () => OptionsStore.UserOptions().Load(), screenSizes: ResolutionSetting.ScreenSizes);
        shell.OpenVideo();
        ctx.Check(shell.Screen == OriginalScreen.Video && shell.ResolutionChoice == "1024x768",
            $"the VIDEO page opens showing the saved size ({shell.Screen}, {shell.ResolutionChoice ?? "unset"})");
        ctx.Check(Label(shell, OriginalShell.ResolutionKey) == "1024x768",
            $"with the row drawing it, so the page and the apply name one size ({Label(shell, OriginalShell.ResolutionKey)})");
        var applied = Accept(shell);
        ctx.Check(applied?.Resolution == "1024x768",
            $"ACCEPT CHANGES carries it on the apply exit ({applied?.Resolution ?? "no exit"})");
        var plan = ResolutionSetting.Resolve(applied?.Resolution, ResolutionSetting.ScreenSizes());
        ctx.Check(plan is { Width: 1024, Height: 768, Source: "options.json" },
            $"and the apply resolves that size to the pixels the window would take ({Describe(plan)})");
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

    // What one row of the page draws, found by key rather than by position: the table stands in
    // authored row order, so a row added above shifts every index under it.
    private static string Label(OriginalShell shell, string key)
    {
        foreach (var row in shell.Rows)
        {
            if (row.Key == key)
            {
                return row.Label;
            }
        }

        return "no such row";
    }

    private static string Describe(MonitorPlan plan) =>
        $"screen {plan.Screen} word={plan.Word} source={plan.Source}";

    private static string Describe(ResolutionPlan plan) =>
        $"{plan.Width}x{plan.Height} word={plan.Word} source={plan.Source}";

    private static string Describe(VSyncPlan plan) =>
        $"vsync {(plan.Enabled ? "on" : "off")} max_fps={plan.MaxFps} source={plan.Source}";
}
