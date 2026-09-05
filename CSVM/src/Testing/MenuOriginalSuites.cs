using System.Collections.Generic;
using CSVM.UI;
using CSVM.UI.Menu;
using CSVM.UI.Menu.BuiltIn;
using CSVM.UI.Menu.Original;
using CSVM.Utils;

namespace CSVM.Testing;

/// <summary>
/// The Original presentation through the boundary, over the install's own decoded layout: a real
/// <see cref="MenuHost"/> with both presentations registered, Original selected by the CLI
/// override, shown at the top level, driven by pointer frames (window pixels, mapped through the
/// view's own <see cref="BoardFit"/>) and by keyboard frames through the host's first seat, the
/// launch leaving as one <see cref="LaunchExit"/>, the return re-entering the top level, then the
/// switches: to Built-in from mid-setup with the picked chapter discarded, back to Original fresh,
/// the Built-in Options route producing its switch exit, force-Built-in recovery, and the
/// availability fallback keeping the request.
/// </summary>
internal static class MenuOriginalSuites
{
    private const float Dt = 1f / 60f;

    private static readonly MenuCommands Accept = new() { Accept = true };
    private static readonly MenuCommands Down = new() { MoveY = 1 };
    private static readonly MenuCommands Up = new() { MoveY = -1 };
    private static readonly MenuCommands Right = new() { MoveX = 1 };
    private static readonly MenuCommands Back = new() { Back = true };

    [Suite("menu-original-tracer",
        "Original Free Flight through the presentation boundary over the install's decoded layout: "
        + "selected by the CLI override, shown at the top level with the pointer hidden and the "
        + "Free Flight door focused, a pointer frame over Quit takes focus and cues the rollover, a "
        + "click on the door opens Free Flight, keyboard frames pick a chapter and an airframe and "
        + "FLY leaves as one LaunchExit, the return re-enters the top level, PREFERENCES and its "
        + "GAME OPTIONS door open the decoded page whose Difficulty dropdown stands first and whose "
        + "three rows take every choice and whose CANCEL CHANGES drops them, seat 0 steering with a "
        + "pad claims it so it can never join as another seat, joining is open on the Instant Action "
        + "screen and a second seat joined there stays seated, the campaign flight check carries the "
        + "seat strip with two seats and none with one, a switch to Built-in "
        + "from mid-setup discards the pick and shows Built-in's Mode screen, a switch back starts "
        + "Original fresh, Built-in's Options route steps the difficulty and both other choices, "
        + "opens and leaves the rebinding screen behind its Controls door and emits the apply exit "
        + "carrying them, the force flag recovers "
        + "and a missing layout falls back with the request kept")]
    internal static void MenuOriginalTracer(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        var exits = new List<MenuExit>();
        var audio = new RecordingAudio();
        var seat = new ScriptedSeat();
        var registry = new PresentationRegistry();
        registry.Register(PresentationId.BuiltIn, () => new BuiltInPresentation(
            ctx.Host, ctx.ZrdrPath, ctx.DataRoot, string.Empty, new MenuInput { Keyboard = true }));
        // ⚠ The debrief return below opens the campaign, so the presentation is pointed at a
        // scratch store: nothing here may read or write user://Profiles.
        string profiles = System.IO.Path.Combine(ctx.ScratchDir, "menu-original-tracer", "Profiles");
        var player1 = new MenuInput { Keyboard = true };
        registry.Register(PresentationId.Original, () => new OriginalPresentation(
            ctx.Host, ctx.DataRoot, layout, string.Empty, player1)
        {
            CampaignProfiles = new CSVM.Session.CampaignProfileStore(profiles),
        });
        var host = new MenuHost(registry, audio, exits.Add);
        MenuSuiteHost.AddFeatures(host, ctx.DataRoot);
        host.AddSeat(seat);
        try
        {
            var shell = ColdStart(ctx, host);
            if (shell == null)
            {
                return;
            }

            Pointer(ctx, host, seat, shell, audio);
            Fly(ctx, host, seat, shell, exits);
            Return(ctx, host, shell, exits);
            Seats(ctx, host, seat, shell, player1);
            OriginalOptionsRoute(ctx, host, seat, shell, exits);
            SwitchToBuiltIn(ctx, host, seat, shell);
            BuiltInOptionsRoute(ctx, host, seat, exits);
            SwitchBackToOriginal(ctx, host);
            Recovery(ctx, host, registry, audio);
        }
        finally
        {
            host.Deactivate();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
        }

        ctx.Check(host.Active == null && !host.Shown, $"Deactivate leaves the host holding no presentation");
    }

    private static OriginalShell? ColdStart(TestContext ctx, MenuHost host)
    {
        string? reason = host.Select(forceBuiltIn: false, cliOverride: "original", savedRequest: null);
        ctx.Check(host.Selected == PresentationId.Original && reason == null,
            $"--presentation=original selects Original over the install's layout ({host.Selected}, {reason ?? "no reason"})");
        host.Show(MenuReturnDestination.TopLevel);
        var original = host.Active as OriginalPresentation;
        ctx.Check(original != null && host.Shown, $"Show activates a fresh Original presentation ({host.Active?.Id})");
        var shell = original?.Shell;
        ctx.Check(shell is { Screen: OriginalScreen.TopLevel },
            $"a cold start opens on the top level ({shell?.Screen})");
        ctx.Check(shell?.FocusedKey == OriginalShell.FreeFlightKey,
            $"with the Free Flight door focused ({shell?.FocusedKey})");
        ctx.Check(shell?.Rows.Count == 8, $"the top level is the six decoded rows plus the two doors ({shell?.Rows.Count})");
        ctx.Check(Godot.Input.MouseMode == Godot.Input.MouseModeEnum.Hidden,
            $"the OS pointer is hidden while Original draws its own ({Godot.Input.MouseMode})");
        return shell;
    }

    // Pointer frames in window pixels: the presentation maps them through the same BoardFit the
    // view draws with, so a point over the authored Quit plaque lands on Quit whatever the window.
    private static void Pointer(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, RecordingAudio audio)
    {
        var size = ctx.Host.GetViewport().GetVisibleRect().Size;
        var fit = BoardFit.For(size.X, size.Y);
        var quit = Row(shell, "MM_B_QUIT");
        var campaign = Row(shell, "MM_B_CAMPAIGN");
        var door = Row(shell, OriginalShell.FreeFlightKey);
        ctx.Check(quit != null && campaign != null && door != null, $"the top level carries Quit, Campaign and the door");
        if (quit == null || campaign == null || door == null)
        {
            return;
        }

        audio.Cues.Clear();
        Press(host, seat, Pointer(fit, quit.X + 5f, quit.Y + 5f));
        ctx.Check(shell.FocusedKey == "MM_B_QUIT", $"a pointer over Quit takes the focus ({shell.FocusedKey})");
        ctx.Check(audio.Cues.Count == 1 && audio.Cues[0] == OriginalCues.Rollover,
            $"and cues one rollover through the host's audio ({string.Join(",", audio.Cues)})");
        var multiplayer = Row(shell, "MM_B_MULTIPLAYER");
        ctx.Check(multiplayer is { Enabled: false }, $"the Multiplayer plaque has no destination yet and is disabled");
        if (multiplayer != null)
        {
            Press(host, seat, Pointer(fit, multiplayer.X + 5f, multiplayer.Y + 5f));
            ctx.Check(shell.FocusedKey == "MM_B_QUIT" && audio.Cues.Count == 1,
                $"a pointer over the disabled Multiplayer plaque moves nothing and cues nothing ({shell.FocusedKey}, {audio.Cues.Count})");
        }

        Press(host, seat, Pointer(fit, campaign.X + 5f, campaign.Y + 5f));
        ctx.Check(shell.FocusedKey == OriginalShell.CampaignKey && audio.Cues.Count == 2,
            $"the Campaign plaque is live over the campaign feature: a pointer over it takes the focus and cues a rollover ({shell.FocusedKey}, {audio.Cues.Count})");
        var board = shell.Compose();
        ctx.Check(board.Overlays.Count == 1 && board.Overlays[0].Pictures.Count == 1,
            $"the pointer is composed as the last overlay ({board.Overlays.Count})");
        Press(host, seat, Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.FreeFlight,
            $"a click on the door opens the Free Flight screen ({shell.Screen})");
        ctx.Check(audio.Cues.Contains(OriginalCues.Click), $"with a click cue ({string.Join(",", audio.Cues)})");
    }

    // Keyboard frames: down the chapter column, across to the airframe column, up onto FLY.
    private static void Fly(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, List<MenuExit> exits)
    {
        Press(host, seat, Down);
        Press(host, seat, Down);
        Press(host, seat, Down);
        Press(host, seat, Accept);
        ctx.Check(shell.PickedChapter == "C2", $"three rows down the chapter column and Accept picks Hollywood ({shell.PickedChapter})");
        ctx.Check(host.Features.Get<FreeFlightFeature>().Chapter?.Code == "C2",
            $"and the shared feature holds the pick ({host.Features.Get<FreeFlightFeature>().Chapter?.Code})");
        Press(host, seat, Right);
        Press(host, seat, Accept);
        ctx.Check(shell.PickedAirframe == "player_bhawk",
            $"Right lands on the same row of the airframe column, Accept picks it ({shell.PickedAirframe})");
        Press(host, seat, Up);
        Press(host, seat, Up);
        Press(host, seat, Up);
        Press(host, seat, Up);
        ctx.Check(shell.FocusedKey == OriginalShell.FlyKey, $"Up past the column's top wraps onto FLY ({shell.FocusedKey})");
        Press(host, seat, Accept);
        ctx.Check(exits.Count == 1 && exits[0] is LaunchExit,
            $"FLY leaves through the host as one LaunchExit ({exits.Count})");
        if (exits.Count == 1 && exits[0] is LaunchExit launch)
        {
            ctx.Check(launch.Chapter == "C2" && launch.Mode == MenuMode.Free && launch.InstantAction == null,
                $"carrying the picked chapter, mode Free and no Instant Action def ({launch.Chapter}, {launch.Mode})");
            ctx.Check(launch.Seats.Count == 1 && launch.Seats[0].PlaneNode == "player_bhawk",
                $"and the one seat's airframe ({launch.Seats.Count}, {launch.Seats[0].PlaneNode})");
        }

        ctx.Check(!host.Shown, $"the host hid the presentation on the exit (shown={host.Shown})");
        ctx.Check(Godot.Input.MouseMode == Godot.Input.MouseModeEnum.Visible,
            $"and the OS pointer is back ({Godot.Input.MouseMode})");
        Press(host, seat, Accept);
        ctx.Check(exits.Count == 1, $"a frame while hidden reaches nothing ({exits.Count})");
        seat.Clear();
    }

    private static void Return(TestContext ctx, MenuHost host, OriginalShell shell, List<MenuExit> exits)
    {
        var before = host.Active;
        host.Show(MenuReturnDestination.TopLevel);
        ctx.Check(ReferenceEquals(before, host.Active) && host.Shown,
            $"a return shows the same Original instance again (shown={host.Shown})");
        ctx.Check(shell.Screen == OriginalScreen.TopLevel && shell.PickedAirframe == null,
            $"on the top level with the airframe pick dropped ({shell.Screen}, {shell.PickedAirframe ?? "none"})");
        ctx.Check(exits.Count == 1, $"and nothing relaunched on the way back in ({exits.Count})");
        // The scratch store holds no such profile, so the return opens the campaign on its
        // profile screen rather than the book; menu-original-campaign drives the seated case.
        host.Show(new DebriefReturn("Nathan", 2));
        ctx.Check(shell.Screen == OriginalScreen.CampaignRoster && host.Features.Get<CampaignFeature>().Profile == null,
            $"a debrief return for a profile the store lacks lands on the profile screen with nobody seated ({shell.Screen})");
        host.Show(MenuReturnDestination.TopLevel);
        ctx.Check(shell.Screen == OriginalScreen.TopLevel && !host.Features.Get<CampaignFeature>().IsOpen,
            $"and a top-level show closes that campaign again ({shell.Screen})");
    }

    // Seats and joining. A scripted run has no pad, so the claim is driven through the poller's
    // own record of the pad seat 0 last steered with, and the join through the feature, which is
    // where a pad's Start lands; the per-screen rule itself is read off the shell.
    private static void Seats(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, MenuInput player1)
    {
        var original = host.Active as OriginalPresentation;
        var setup = host.Features.Get<PlayerSetupFeature>();
        ctx.Check(original?.Devices != null && !shell.JoiningOpen,
            $"the top level keeps joining closed ({shell.Screen}, open={shell.JoiningOpen})");
        if (original?.Devices is not { } devices)
        {
            return;
        }

        player1.LastActivePad = 2;
        host.Tick(Dt);
        ctx.Check(devices.P1Pad == 2 && devices.IsClaimed(2),
            $"seat 0 steering with pad 2 claims it, so the join scan skips that pad ({devices.P1Pad}, claimed={devices.IsClaimed(2)})");
        player1.LastActivePad = -1;

        WalkTo(host, seat, shell, "MM_B_INSTANTACTION");
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.InstantAction && shell.JoiningOpen,
            $"the Instant Action screen opens joining ({shell.Screen}, open={shell.JoiningOpen})");
        var s2 = new ScriptedSeat();
        ctx.Check(setup.Join(s2) != null && host.Seats.Count == 2, $"a second seat joins there ({host.Seats.Count})");
        Press(host, seat, Down);
        ctx.Check(host.Seats.Count == 2 && shell.Screen == OriginalScreen.InstantAction,
            $"and stays seated while seat 0 keeps steering the screen ({host.Seats.Count}, {shell.Screen})");

        host.Show(MenuReturnDestination.TopLevel);
        shell.OpenCampaignOver(CampaignAidProfiles.Store(seeded: true), CampaignAidProfiles.Planes());
        ctx.Check(shell.ShowCabin(CampaignAidProfiles.Pilot), $"the scratch campaign seats its pilot");
        shell.ShowMissionScreen(OriginalScreen.CampaignFlightCheck);
        ctx.Check(shell.Screen == OriginalScreen.CampaignFlightCheck && shell.JoiningOpen,
            $"the flight check opens joining too ({shell.Screen}, open={shell.JoiningOpen})");
        ctx.Check(StripSeats(shell) == 2, $"its board carries the seat strip naming both seats ({StripSeats(shell)} lines)");
        Press(host, s2, Back);
        ctx.Check(host.Seats.Count == 1, $"the second seat's Back unjoins it ({host.Seats.Count})");
        ctx.Check(StripSeats(shell) == 0, $"and the strip goes with it, leaving the authored board alone ({StripSeats(shell)} lines)");
        host.Show(MenuReturnDestination.TopLevel);
        ctx.Check(shell.Screen == OriginalScreen.TopLevel && !host.Features.Get<CampaignFeature>().IsOpen,
            $"a top-level show closes the scratch campaign again ({shell.Screen})");
    }

    // How many seat lines the composed board's overlays carry: the strip's lines are the only
    // overlay text that begins with a player tag.
    private static int StripSeats(OriginalShell shell)
    {
        int count = 0;
        foreach (var panel in shell.Compose().Overlays)
        {
            foreach (var line in panel.Lines)
            {
                count += line.Text.StartsWith("P", System.StringComparison.Ordinal) && line.Text.Length > 1 && char.IsDigit(line.Text[1]) ? 1 : 0;
            }
        }

        return count;
    }

    // The switch, as the launcher performs it after an Options exit: from mid-setup on the Free
    // Flight screen, Deactivate discards the feature's pick and Built-in opens on its Mode screen.
    private static void SwitchToBuiltIn(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell)
    {
        Press(host, seat, Accept);
        Press(host, seat, Down);
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.FreeFlight && host.Features.Get<FreeFlightFeature>().Chapter != null,
            $"mid-setup: on the Free Flight screen with a chapter picked ({shell.Screen})");
        host.Deactivate();
        ctx.Check(host.Active == null && host.Features.Get<FreeFlightFeature>().Chapter == null,
            $"Deactivate frees Original and discards the unfinished pick");
        string? reason = host.Select(forceBuiltIn: false, cliOverride: null, savedRequest: "built-in");
        host.Show(MenuReturnDestination.TopLevel);
        var menu = (host.Active as BuiltInPresentation)?.Menu;
        ctx.Check(host.Selected == PresentationId.BuiltIn && reason == null && menu is { Visible: true },
            $"the saved request re-selects Built-in and Show stands the launchscreen up ({host.Selected})");
        ctx.Check(menu?.ShownScreen == "Mode" && menu.ShownRowText == "Free Flight",
            $"at its own top level, the Mode screen ({menu?.ShownScreen}, {menu?.ShownRowText})");
        ctx.Check(menu?.ShownRowCount == 6, $"whose sixth row is the Options door ({menu?.ShownRowCount})");
    }

    // Built-in's Options route: the last Mode row opens Options, Right steps the difficulty to
    // Hard, Right on the row under it steps the presentation to Original, Right on the next steps
    // the graphics mode, and the apply row's Accept leaves as the one exit the launcher persists
    // every choice from.
    private static void BuiltInOptionsRoute(TestContext ctx, MenuHost host, ScriptedSeat seat, List<MenuExit> exits)
    {
        var menu = (host.Active as BuiltInPresentation)?.Menu;
        if (menu == null)
        {
            return;
        }

        Press(host, seat, Up);
        ctx.Check(menu.ShownRowText == LaunchMenu.OptionsRow, $"Up from Free Flight wraps onto Options ({menu.ShownRowText})");
        Press(host, seat, Accept);
        ctx.Check(menu.ShownScreen == "Options" && menu.ShownRowCount == 5 && menu.ShownRowText == "Difficulty: Normal",
            $"Accept opens the Options screen with its five rows, the difficulty stepper first ({menu.ShownScreen}, {menu.ShownRowCount}, {menu.ShownRowText})");
        Press(host, seat, Right);
        ctx.Check(menu.ShownRowText == "Difficulty: Hard", $"Right steps the difficulty to Hard ({menu.ShownRowText})");
        Press(host, seat, Down);
        string before = menu.ShownRowText;
        Press(host, seat, Right);
        ctx.Check(menu.ShownRowText != before && menu.ShownRowText.StartsWith("Menu presentation: ", System.StringComparison.Ordinal),
            $"Right steps the presentation row ({before} -> {menu.ShownRowText})");
        string chosen = menu.ShownRowText.EndsWith("Original", System.StringComparison.Ordinal) ? "original" : "built-in";
        Press(host, seat, Down);
        string beforeGraphics = menu.ShownRowText;
        Press(host, seat, Right);
        ctx.Check(menu.ShownRowText != beforeGraphics && menu.ShownRowText.StartsWith("Graphics: ", System.StringComparison.Ordinal),
            $"Right steps the graphics row under it ({beforeGraphics} -> {menu.ShownRowText})");
        string graphics = menu.ShownRowText.EndsWith("Enhanced", System.StringComparison.Ordinal) ? "enhanced" : "original";
        Press(host, seat, Down);
        ctx.Check(menu.ShownRowText == LaunchMenu.ControlsRow, $"the fourth row is the Controls door ({menu.ShownRowText})");
        Press(host, seat, Accept);
        ctx.Check(menu.ShownScreen == "Controls" && menu.ShownRowCount > 2,
            $"which opens the rebinding screen over a seat's own keymap ({menu.ShownScreen}, {menu.ShownRowCount} rows)");
        Press(host, seat, Back);
        ctx.Check(menu.ShownScreen == "Options" && menu.ShownRowText == LaunchMenu.ControlsRow,
            $"and Back returns to Options on the door it came from ({menu.ShownScreen}, {menu.ShownRowText})");
        Press(host, seat, Down);
        Press(host, seat, Accept);
        ctx.Check(exits.Count == 2 && exits[1] is OptionsApplyExit,
            $"Apply leaves through the host as an OptionsApplyExit ({exits.Count}, {exits[^1].GetType().Name})");
        if (exits.Count == 2 && exits[1] is OptionsApplyExit applied)
        {
            ctx.Check(applied.Presentation.Value == chosen && applied.Graphics == graphics && applied.Difficulty == "hard",
                $"carrying every stepped choice ({applied.Presentation}, {applied.Graphics}, {applied.Difficulty})");
        }

        ctx.Check(!host.Shown, $"and the presentation is hidden for the launcher to act (shown={host.Shown})");
    }

    // Original's own Options route over the install's decoded sections: PREFERENCES opens the
    // Preferences page, its GAME OPTIONS door the decoded page, whose three rows take every choice
    // and whose CANCEL CHANGES drops them; the walk leaves the top level as it found it.
    private static void OriginalOptionsRoute(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, List<MenuExit> exits)
    {
        WalkTo(host, seat, shell, "MM_B_PREFERENCES");
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.Options && shell.FocusedKey == OriginalShell.GameOptionsDoorKey,
            $"PREFERENCES opens the Preferences page with its GAME OPTIONS door focused ({shell.Screen}, {shell.FocusedKey})");
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.GameOptions && shell.FocusedKey == OriginalShell.DifficultyKey,
            $"GAME OPTIONS opens the decoded page on its Difficulty dropdown, the first row ({shell.Screen}, {shell.FocusedKey})");
        var board = shell.Compose();
        int titles = 0;
        foreach (var line in board.Lines)
        {
            titles += line.Text is "GAME OPTIONS" or "Difficulty" or "Menu" or "Enhanced Graphics" ? 1 : 0;
        }

        ctx.Check(titles == 4, $"drawing the section's own tab title over the three row titles ({titles} of 4)");
        bool box = false;
        foreach (var plaque in board.Plaques)
        {
            box |= plaque.Art.Frames == 8;
        }

        ctx.Check(box, $"with the checkbox drawn from its eight-state strip ({board.Plaques.Count} plaques)");
        Press(host, seat, Accept);
        ctx.Check(shell.OpenGameOption == OriginalShell.DifficultyKey && shell.Rows.Count == 3,
            $"Accept opens the dropdown over the three campaign tiers ({shell.OpenGameOption}, {shell.Rows.Count})");
        Press(host, seat, Down);
        Press(host, seat, Accept);
        ctx.Check(shell.DifficultyChoice == CSVM.Flight.Difficulty.Hard && shell.FocusedKey == OriginalShell.DifficultyKey,
            $"and picking the second closes it on Hard ({shell.DifficultyChoice}, {shell.FocusedKey})");
        Press(host, seat, Down);
        Press(host, seat, Accept);
        ctx.Check(shell.OpenGameOption == OriginalShell.PresentationKey && shell.Rows.Count == 2,
            $"Accept on the Menu row under it opens the dropdown over the two shipped presentations ({shell.OpenGameOption}, {shell.Rows.Count})");
        Press(host, seat, Down);
        Press(host, seat, Accept);
        ctx.Check(shell.PresentationChoice == PresentationId.BuiltIn.Value,
            $"and picking the second closes it on the other token ({shell.PresentationChoice})");
        Press(host, seat, Down);
        Press(host, seat, Accept);
        ctx.Check(shell.GraphicsChoice == GraphicsMode.EnhancedWord,
            $"Accept on the checkbox under it flips the graphics word ({shell.GraphicsChoice})");
        WalkTo(host, seat, shell, OriginalShell.GameOptionsCancelKey);
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.Options && shell.PresentationChoice == PresentationId.Original.Value
            && shell.GraphicsChoice == GraphicsMode.Default && shell.DifficultyChoice == CSVM.Flight.Difficulty.Normal,
            $"CANCEL CHANGES lands back on Preferences with every edit dropped ({shell.Screen}, {shell.PresentationChoice}, {shell.GraphicsChoice}, {shell.DifficultyChoice})");
        WalkTo(host, seat, shell, OriginalShell.OptionsBackKey);
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.TopLevel && exits.Count == 1,
            $"and RETURN TO MAIN MENU leaves the page without an exit ({shell.Screen}, {exits.Count})");
        WalkTo(host, seat, shell, OriginalShell.FreeFlightKey);
    }

    // Walks the focus down onto a row by key, the keyboard's own way there.
    private static void WalkTo(MenuHost host, ScriptedSeat seat, OriginalShell shell, string key)
    {
        for (int guard = 0; guard < 32 && shell.FocusedKey != key; guard++)
        {
            Press(host, seat, Down);
        }
    }

    private static void SwitchBackToOriginal(TestContext ctx, MenuHost host)
    {
        host.Deactivate();
        string? reason = host.Select(forceBuiltIn: false, cliOverride: null, savedRequest: "original");
        host.Show(MenuReturnDestination.TopLevel);
        var shell = (host.Active as OriginalPresentation)?.Shell;
        ctx.Check(host.Selected == PresentationId.Original && reason == null && shell != null,
            $"the saved request re-selects Original and Show creates a fresh instance ({host.Selected})");
        ctx.Check(shell is { Screen: OriginalScreen.TopLevel, PickedChapter: null, FocusedKey: OriginalShell.FreeFlightKey },
            $"standing on its top level with nothing picked and the door focused ({shell?.Screen}, {shell?.PickedChapter ?? "none"}, {shell?.FocusedKey})");
    }

    // Recovery: the force flag beats an Original override, and a registered Original whose layout
    // is not there falls back to Built-in with the availability reason and the request kept.
    private static void Recovery(TestContext ctx, MenuHost host, PresentationRegistry registry, RecordingAudio audio)
    {
        string? reason = host.Select(forceBuiltIn: true, cliOverride: "original", savedRequest: "original");
        ctx.Check(host.Selected == PresentationId.BuiltIn && reason != null && host.Requested == PresentationId.Original,
            $"--force-builtin resolves Built-in over the Original override and keeps the request ({reason})");

        var missing = new MenuHost(registry, audio, _ => { });
        missing.Availability = id => id == PresentationId.Original ? OriginalAvailability.Load(EmptyDataRoot(ctx), out var why) == null ? why : null : null;
        reason = missing.Select(forceBuiltIn: false, cliOverride: null, savedRequest: "original");
        ctx.Check(missing.Selected == PresentationId.BuiltIn && missing.Requested == PresentationId.Original,
            $"a data root with no decoded layout selects Built-in and keeps the saved request ({missing.Selected}, {missing.Requested})");
        ctx.Check(reason != null && reason.Contains("menu_layout.json", System.StringComparison.Ordinal),
            $"with the reason naming the missing file ({reason})");
    }

    private static string EmptyDataRoot(TestContext ctx)
    {
        string dir = System.IO.Path.Combine(ctx.ScratchDir, "menu-original-empty-root");
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    private static OriginalRow? Row(OriginalShell shell, string key)
    {
        foreach (var row in shell.Rows)
        {
            if (row.Key == key)
            {
                return row;
            }
        }

        return null;
    }

    private static MenuCommands Pointer(BoardFit fit, float authoredX, float authoredY, bool pressed = false, bool clicked = false) =>
        new() { Pointer = new MenuPointer(fit.X(authoredX), fit.Y(authoredY), pressed, clicked) };

    private static void Press(MenuHost host, ScriptedSeat seat, MenuCommands frame)
    {
        seat.Enqueue(frame);
        host.Tick(Dt);
    }

    private sealed class RecordingAudio : IMenuAudio
    {
        public List<string> Cues { get; } = new();

        public void Cue(MenuCue cue) => Cues.Add(cue.Name);

        public void BeginNarration(string wavName)
        {
        }

        public void EndNarration()
        {
        }
    }

    private sealed class ScriptedSeat : IMenuInputSource
    {
        private readonly Queue<MenuCommands> _frames = new();

        public string DeviceLabel => "scripted";

        public bool CapturingText { get; set; }

        public void Enqueue(MenuCommands frame) => _frames.Enqueue(frame);

        public void Clear() => _frames.Clear();

        public MenuCommands Poll(float dt) => _frames.Count > 0 ? _frames.Dequeue() : MenuCommands.None;

        public void Prime()
        {
        }
    }
}
