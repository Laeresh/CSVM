using System.Collections.Generic;
using System.Linq;
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
    private static readonly MenuCommands Left = new() { MoveX = -1 };
    private static readonly MenuCommands Back = new() { Back = true };

    [Suite("menu-original-tracer",
        "Original Free Flight through the presentation boundary over the install's decoded layout: "
        + "selected by the CLI override, shown at the top level with the pointer hidden and the "
        + "Free Flight door focused, a pointer frame over Quit takes focus and cues the rollover, a "
        + "press on a plaque arms it and opens nothing while the pointer over it draws the active "
        + "bitmap, a release on another plaque activates neither, a "
        + "click on the door opens Free Flight, keyboard frames pick a chapter and an airframe and "
        + "FLY leaves as one LaunchExit, the return re-enters the top level, PREFERENCES and its "
        + "GAME OPTIONS door open the decoded page whose Difficulty dropdown stands first and whose "
        + "three rows take every choice and whose CANCEL CHANGES drops them, a wheel step over the "
        + "aircraft column and over Instant Action's contents window moves each one row and clamps "
        + "at the head, a drag down each thumb's track lands the window on its last row without "
        + "activating what the click stood over, and the contents arrows still step it, seat 0 steering with a "
        + "pad claims it so it can never join as another seat, joining is open on the Instant Action "
        + "screen and a second seat joined there stays seated, the campaign flight check carries the "
        + "seat strip with two seats and none with one, a switch to Built-in "
        + "from mid-setup discards the pick and shows Built-in's Mode screen, a switch back starts "
        + "Original fresh, Built-in's Options route steps the difficulty, the targeting setting on "
        + "and back off, the two other choices and "
        + "its four display rows over the machine's own screens and sizes, the two vocabularies and "
        + "a wrap onto the last frame cap, "
        + "opens and leaves the rebinding screen behind its Controls door and emits the apply exit "
        + "carrying all eight, Original's VIDEO door opens the decoded page on its Display Mode dropdown "
        + "which fits its authored window and draws no bar, over the V-Sync one whose five words "
        + "window into four with the arrows and the thumb inside the box's right edge and the fifth "
        + "kept for the walk but unseen and unhit, that list wheeling and dragging like any other "
        + "and picking a frame cap, and the Enhanced Graphics checkbox "
        + "that flips, whose CANCEL CHANGES drops them all with no exit and whose ACCEPT CHANGES "
        + "leaves as one more apply exit carrying them, Original's AUDIO door opens the decoded page "
        + "on its Master slider over four thumbs, a sideways step moves a level and clamps at "
        + "silence, the open page states its mix to the host every frame and names the level a "
        + "frame moved only where one moved, its CANCEL CHANGES drops the edit with no exit and "
        + "ends the preview, and its ACCEPT CHANGES leaves as "
        + "one more apply exit carrying the levels, the force flag recovers and a missing layout "
        + "falls back with the request kept")]
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
            Lists(ctx, host, seat, shell);
            Seats(ctx, host, seat, shell, player1);
            OriginalOptionsRoute(ctx, host, seat, shell, exits);
            SwitchToBuiltIn(ctx, host, seat, shell);
            BuiltInOptionsRoute(ctx, host, seat, exits);
            // The VIDEO route runs on the shell the switch back creates, and last of the Original
            // walks: its ACCEPT CHANGES hides the presentation for the launcher to act, so nothing
            // after it can drive the same shell.
            OriginalVideoRoute(ctx, host, seat, SwitchBackToOriginal(ctx, host), exits);
            // The AUDIO route needs a shell of its own for the same reason, the VIDEO route's
            // ACCEPT CHANGES having hidden the presentation the walk before it drove.
            OriginalAudioRoute(ctx, host, seat, SwitchBackToOriginal(ctx, host), exits, audio);
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
        // The press arms the plaque and opens nothing: what fires is the release still on it.
        Press(host, seat, Pointer(fit, campaign.X + 5f, campaign.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.TopLevel && shell.ArmedKey == OriginalShell.CampaignKey,
            $"a press on Campaign arms it and opens nothing ({shell.Screen}, {shell.ArmedKey})");
        string bitmap = shell.Compose().Overlays[^1].Pictures[0].Art.Name;
        ctx.Check(bitmap == "activepointerz.png", $"the pointer over a live plaque is the active bitmap ({bitmap})");
        Press(host, seat, Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true));
        Press(host, seat, Pointer(fit, door.X + 5f, door.Y + 5f));
        ctx.Check(shell.Screen == OriginalScreen.TopLevel && shell.ArmedKey == string.Empty,
            $"and a release on another plaque activates neither of them ({shell.Screen}, {shell.ArmedKey})");

        Click(host, seat, Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
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
        host.Show(new DebriefReturn("Nathan", 2, MissionWon: true));
        ctx.Check(shell.Screen == OriginalScreen.CampaignRoster && host.Features.Get<CampaignFeature>().Profile == null,
            $"a debrief return for a profile the store lacks lands on the profile screen with nobody seated ({shell.Screen})");
        host.Show(MenuReturnDestination.TopLevel);
        ctx.Check(shell.Screen == OriginalScreen.TopLevel && !host.Features.Get<CampaignFeature>().IsOpen,
            $"and a top-level show closes that campaign again ({shell.Screen})");
    }

    // Seats and joining. A scripted run has no pad, so the claim is driven through the poller's
    // own record of the pad seat 0 last steered with, and the join through the feature, which is
    // where a pad's Start lands; the per-screen rule itself is read off the shell.
    // The pointer's wheel and thumb over the two lists this screen graph reaches: the sortie
    // screens' aircraft column and Instant Action's contents window, each wheeled a row, dragged
    // down its track and left back at its head, with the list's own arrows still stepping it.
    private static void Lists(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell)
    {
        var size = ctx.Host.GetViewport().GetVisibleRect().Size;
        var fit = BoardFit.For(size.X, size.Y);
        WalkTo(host, seat, shell, OriginalShell.FreeFlightKey);
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.FreeFlight, $"the Free Flight door opens the aircraft column again ({shell.Screen})");
        WheelAndDrag(ctx, host, seat, shell, fit, "AIRFRAMES", "the sortie screens' aircraft column");
        ctx.Check(shell.PickedAirframe == null && shell.Screen == OriginalScreen.FreeFlight,
            $"and the wheel and the drag picked nothing and left no screen ({shell.PickedAirframe ?? "none"}, {shell.Screen})");
        Press(host, seat, Back);

        WalkTo(host, seat, shell, "MM_B_INSTANTACTION");
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.InstantAction, $"Instant Action opens its contents window ({shell.Screen})");
        WheelAndDrag(ctx, host, seat, shell, fit, OriginalInstantActionScreen.ContentsKey, "Instant Action's contents window");
        var down = Row(shell, OriginalInstantActionScreen.ContentsDownKey);
        ctx.Check(down != null, $"the contents window carries its authored down arrow");
        if (down != null)
        {
            int top = shell.InstantAction.ContentsTop;
            Click(host, seat, Pointer(fit, down.X + 2f, down.Y + 2f, pressed: true, clicked: true));
            ctx.Check(shell.InstantAction.ContentsTop == top + 1,
                $"and the arrow still steps the window one row, the wheel having changed nothing about it ({top} -> {shell.InstantAction.ContentsTop})");
            var up = Row(shell, OriginalInstantActionScreen.ContentsUpKey)!;
            Click(host, seat, Pointer(fit, up.X + 2f, up.Y + 2f, pressed: true, clicked: true));
            ctx.Check(shell.InstantAction.ContentsTop == top, $"and the up arrow steps it back ({shell.InstantAction.ContentsTop})");
        }

        Press(host, seat, Back);
        ctx.Check(shell.Screen == OriginalScreen.TopLevel, $"Back leaves Instant Action for the top level ({shell.Screen})");
    }

    // One list under the pointer: a wheel step down moves its window by exactly one row, a step
    // past the head clamps there, and a thumb taken at the top of its track and dragged to the
    // foot lands the window on its last row without activating whatever the click stood over.
    private static void WheelAndDrag(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell,
        BoardFit fit, string key, string what)
    {
        var list = List(shell, key);
        ctx.Check(list is { Window.Scrolls: true },
            $"{what} is a scrolling list the pointer can see ({list?.Window.Count ?? -1} rows in a window of {list?.Window.Rows ?? -1})");
        if (list is not { Window.Scrolls: true })
        {
            return;
        }

        var window = list.Window;
        float x = window.X + (window.Width / 2f);
        float y = window.Y + (window.Height / 2f);
        Press(host, seat, Pointer(fit, x, y, wheel: 1));
        ctx.Check(List(shell, key)?.Window.Top == window.Top + 1,
            $"a wheel step over it moves the window one row ({window.Top} -> {List(shell, key)?.Window.Top})");
        Press(host, seat, Pointer(fit, x, y, wheel: -3));
        ctx.Check(List(shell, key)?.Window.Top == 0, $"and three steps back up clamp at the head ({List(shell, key)?.Window.Top})");

        var head = List(shell, key)!.Window;
        Press(host, seat, Pointer(fit, head.ThumbX + 1f, head.ThumbY + 1f, pressed: true, clicked: true));
        ctx.Check(shell.Dragging == key, $"a click on its thumb takes hold of it ({shell.Dragging ?? "nothing"})");
        Press(host, seat, Pointer(fit, head.ThumbX + 1f, head.ThumbY + 1f + head.TrackHeight - head.ThumbHeight, pressed: true));
        ctx.Check(List(shell, key)?.Window.Top == head.LastTop,
            $"and dragging it the length of its track lands the window on its last row ({List(shell, key)?.Window.Top} of {head.LastTop})");
        Press(host, seat, Pointer(fit, head.ThumbX + 1f, head.ThumbY + 1f));
        ctx.Check(shell.Dragging == null, $"letting the button go ends the drag ({shell.Dragging ?? "nothing"})");
        Press(host, seat, Pointer(fit, x, y, wheel: -head.Count));
        ctx.Check(List(shell, key)?.Window.Top == 0, $"and the wheel brings it home ({List(shell, key)?.Window.Top})");
    }

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
    // the graphics mode, the four display rows under those step over the machine's own screens and
    // sizes, and the apply row's Accept leaves as the one exit the launcher persists every choice
    // from.
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
        ctx.Check(menu.ShownScreen == "Options" && menu.ShownRowCount == 10 && menu.ShownRowText == "Difficulty: Normal",
            $"Accept opens the Options screen with its ten rows, the difficulty stepper first ({menu.ShownScreen}, {menu.ShownRowCount}, {menu.ShownRowText})");
        Press(host, seat, Right);
        ctx.Check(menu.ShownRowText == "Difficulty: Hard", $"Right steps the difficulty to Hard ({menu.ShownRowText})");
        Press(host, seat, Down);
        ctx.Check(menu.ShownRowText == "Nearest target after a kill: Off",
            $"the second row is the targeting setting, unsaved showing the decoded Off ({menu.ShownRowText})");
        Press(host, seat, Right);
        ctx.Check(menu.ShownRowText == "Nearest target after a kill: On",
            $"Right turns it on ({menu.ShownRowText})");
        Press(host, seat, Right);
        ctx.Check(menu.ShownRowText == "Nearest target after a kill: Off",
            $"and Right again turns it back off ({menu.ShownRowText})");
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
        var display = BuiltInDisplayRows(ctx, host, seat, menu);
        Press(host, seat, Down);
        ctx.Check(menu.ShownRowText == LaunchMenu.ControlsRow, $"the ninth row is the Controls door ({menu.ShownRowText})");
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
            ctx.Check(applied.Presentation.Value == chosen && applied.Graphics == graphics && applied.Difficulty == "hard"
                && applied.NearestAfterKill == false,
                $"carrying every stepped choice, the targeting setting stepped back off among them ({applied.Presentation}, {applied.Graphics}, {applied.Difficulty}, {applied.NearestAfterKill})");
            ctx.Check(applied.MonitorIndex == display.Monitor && applied.Resolution == display.Resolution
                && applied.DisplayMode == display.DisplayMode && applied.VSync == display.VSync,
                $"and all four display settings the rows stepped ({applied.MonitorIndex}, {applied.Resolution}, {applied.DisplayMode}, {applied.VSync})");
            var plan = MonitorSetting.Resolve(applied.MonitorIndex, MonitorSetting.Screens());
            ctx.Check(plan.Source == "options.json" && ResolutionSetting.Resolve(applied.Resolution,
                    ResolutionSetting.ScreenSizes(), applied.DisplayMode).Source == "options.json",
                $"which the launcher's own resolvers take as saved rather than dropping ({plan.Word})");
        }

        ctx.Check(!host.Shown, $"and the presentation is hidden for the launcher to act (shown={host.Shown})");
    }

    // The four display rows, walked from the graphics row: each steps in the store's own words, the
    // monitor row over the machine's screens (one step wraps within a single-screen list, which is
    // why its label is checked against the enumeration rather than for a change), the resolution row
    // over the sizes the standing screen offers once the mode leaves borderless, which pins it, and
    // the mode and pacing rows over their vocabularies. Returns the four words the apply carries.
    private static (string? Monitor, string? Resolution, string? DisplayMode, string? VSync) BuiltInDisplayRows(
        TestContext ctx, MenuHost host, ScriptedSeat seat, LaunchMenu menu)
    {
        var screens = MonitorSetting.Screens();
        int standing = MonitorSetting.Resolve(null, screens).Screen;
        Press(host, seat, Down);
        ctx.Check(menu.ShownRowText == $"Monitor: {screens.Labels[standing]}",
            $"the fourth row is the monitor, an unsaved index showing the screen the window stands on ({menu.ShownRowText})");
        Press(host, seat, Right);
        int stepped = DisplaySettingRows.Step(standing, 1, screens.Labels.Count);
        ctx.Check(menu.ShownRowText == $"Monitor: {screens.Labels[stepped]}",
            $"Right steps it to the next screen the engine reports ({menu.ShownRowText})");

        var sizes = ResolutionSetting.ScreenSizes();
        Press(host, seat, Down);
        string opened = menu.ShownRowText;
        ctx.Check(opened == $"Resolution: {sizes.Fallback}",
            $"the fifth row is the resolution, showing the screen's own size ({opened})");
        Press(host, seat, Right);
        ctx.Check(menu.ShownRowText == opened,
            $"which the shipped borderless mode pins, so Right steps it nowhere ({menu.ShownRowText})");
        ctx.Check(menu.ShownDetail.StartsWith("Borderless runs at", System.StringComparison.Ordinal),
            $"and the row says who owns the size rather than refusing in silence ({menu.ShownDetail})");

        Press(host, seat, Down);
        ctx.Check(menu.ShownRowText == "Display mode: Borderless",
            $"the sixth row is the display mode, unsaved showing the shipped borderless default ({menu.ShownRowText})");
        Press(host, seat, Right);
        ctx.Check(menu.ShownRowText == "Display mode: Fullscreen",
            $"Right steps it one word along the vocabulary ({menu.ShownRowText})");

        // Back up to the size row, which exclusive fullscreen leaves to the player: the pin above
        // is the mode's and not the row's, and this mode flies at the size the row names.
        Press(host, seat, Up);
        Press(host, seat, Right);
        string size = menu.ShownRowText["Resolution: ".Length..];
        ctx.Check(menu.ShownRowText != opened && Offers(sizes.Words, size),
            $"under Fullscreen the same Right steps it to another size the screen can hold ({opened} -> {menu.ShownRowText})");
        Press(host, seat, Down);

        Press(host, seat, Down);
        ctx.Check(menu.ShownRowText == "V-Sync: Off", $"the seventh row is V-Sync, unsaved showing the shipped Off ({menu.ShownRowText})");
        Press(host, seat, Right);
        ctx.Check(menu.ShownRowText == "V-Sync: 60 FPS",
            $"Right steps it onto the first cap ({menu.ShownRowText})");
        Press(host, seat, Left);
        Press(host, seat, Left);
        ctx.Check(menu.ShownRowText == "V-Sync: On",
            $"and two Lefts step back past Off onto On without wrapping ({menu.ShownRowText})");
        Press(host, seat, Left);
        ctx.Check(menu.ShownRowText == "V-Sync: 144 FPS",
            $"and one more Left wraps onto the last cap rather than stopping ({menu.ShownRowText})");

        return (MonitorSetting.Word(stepped), size, DisplayWords.Fullscreen, "144");
    }

    private static bool Offers(IReadOnlyList<string> sizes, string word)
    {
        foreach (string size in sizes)
        {
            if (size == word)
            {
                return true;
            }
        }

        return false;
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
            titles += line.Text is "GAME OPTIONS" or "Difficulty" or "Menu" or "Next Target" ? 1 : 0;
        }

        ctx.Check(titles == 4, $"drawing the section's own tab title over the three row titles ({titles} of 4)");
        Press(host, seat, Accept);
        ctx.Check(shell.OpenGameOption == OriginalShell.DifficultyKey && shell.Rows.Count == 3
            && List(shell, OriginalShell.DifficultyKey) == null,
            $"Accept opens the dropdown over the three campaign tiers, inside its window and with no bar ({shell.OpenGameOption}, {shell.Rows.Count})");
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
        WalkTo(host, seat, shell, OriginalShell.NearestAfterKillKey);
        Press(host, seat, Accept);
        ctx.Check(shell.NearestAfterKillChoice == true,
            $"Accept on the Next Target checkbox under them turns the targeting setting on ({shell.NearestAfterKillChoice})");
        WalkTo(host, seat, shell, OriginalShell.GameOptionsCancelKey);
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.Options && shell.PresentationChoice == PresentationId.Original.Value
            && shell.DifficultyChoice == CSVM.Flight.Difficulty.Normal && shell.NearestAfterKillChoice == null,
            $"CANCEL CHANGES lands back on Preferences with every edit dropped ({shell.Screen}, {shell.PresentationChoice}, {shell.DifficultyChoice}, {shell.NearestAfterKillChoice})");
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

    private static OriginalShell? SwitchBackToOriginal(TestContext ctx, MenuHost host)
    {
        host.Deactivate();
        string? reason = host.Select(forceBuiltIn: false, cliOverride: null, savedRequest: "original");
        host.Show(MenuReturnDestination.TopLevel);
        var shell = (host.Active as OriginalPresentation)?.Shell;
        ctx.Check(host.Selected == PresentationId.Original && reason == null && shell != null,
            $"the saved request re-selects Original and Show creates a fresh instance ({host.Selected})");
        ctx.Check(shell is { Screen: OriginalScreen.TopLevel, PickedChapter: null, FocusedKey: OriginalShell.FreeFlightKey },
            $"standing on its top level with nothing picked and the door focused ({shell?.Screen}, {shell?.PickedChapter ?? "none"}, {shell?.FocusedKey})");
        return shell;
    }

    // Original's VIDEO route over the install's decoded sections: the Preferences page's third door
    // opens the decoded page on its monitor dropdown, the rows under it take a screen, a size, a
    // display mode and a frame cap, the checkbox flips the graphics word, CANCEL CHANGES drops them
    // all without an exit, and ACCEPT CHANGES on a second visit leaves as one apply exit carrying them.
    private static void OriginalVideoRoute(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell? shell, List<MenuExit> exits)
    {
        if (shell == null)
        {
            return;
        }

        int before = exits.Count;
        WalkTo(host, seat, shell, "MM_B_PREFERENCES");
        Press(host, seat, Accept);
        WalkTo(host, seat, shell, OriginalShell.VideoDoorKey);
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.Video && shell.FocusedKey == OriginalShell.MonitorKey,
            $"VIDEO opens the decoded page on its Monitor row, the first of the authored rows it carries ({shell.Screen}, {shell.FocusedKey})");
        var board = shell.Compose();
        int titles = 0;
        foreach (var line in board.Lines)
        {
            titles += line.Text is "VIDEO" or "Monitor" or "Resolution" or "Display Mode" or "V-Sync" or "Enhanced Graphics" ? 1 : 0;
        }

        ctx.Check(titles == 6, $"drawing the section's own tab title over the five row titles ({titles} of 6)");
        bool box = false;
        foreach (var plaque in board.Plaques)
        {
            box |= plaque.Art.Frames == 8;
        }

        ctx.Check(box, $"with the checkbox drawn from its eight-state strip ({board.Plaques.Count} plaques)");
        // The monitor row's words are this machine's screens, so the claim is the count rather than
        // the labels: one per screen the engine reports, which is one on a single-screen machine.
        ctx.Check(shell.MonitorWords.Count == Godot.DisplayServer.GetScreenCount(),
            $"the monitor row offers one label per screen ({string.Join(" ", shell.MonitorWords)})");
        Press(host, seat, Right);
        ctx.Check(shell.MonitorChoice != null
            && OptionsStore.TryParseMonitorIndex(shell.MonitorChoice, out int picked) && picked < shell.MonitorWords.Count,
            $"and a sideways step on it takes a screen this machine has ({shell.MonitorChoice ?? "unset"} of {shell.MonitorWords.Count})");
        // The size row under the borderless default, which owns the size: it draws dead and the
        // walk cannot land on it, so the row the walk ends on is what the check names.
        WalkTo(host, seat, shell, OriginalShell.ResolutionKey);
        ctx.Check(shell.ResolutionPinned && Row(shell, OriginalShell.ResolutionKey) is { Enabled: false }
            && shell.FocusedKey != OriginalShell.ResolutionKey && shell.ResolutionChoice == null,
            $"borderless leaves the size row dead, out of the walk's reach ({shell.FocusedKey}, {shell.ResolutionChoice ?? "unset"})");
        WalkTo(host, seat, shell, OriginalShell.DisplayModeKey);
        // A list inside the window its own row authors is exactly as tall as its items: no arrows,
        // no thumb and nothing for the pointer to scroll, which is the shape the film shows.
        Press(host, seat, Accept);
        ctx.Check(shell.Rows.Count == DisplayWords.DisplayModes.Count
            && List(shell, OriginalShell.DisplayModeKey) == null
            && Row(shell, OriginalShell.DisplayModeKey + ":down") == null,
            $"the Display Mode list fits its authored window and draws no bar ({shell.Rows.Count} rows)");
        Press(host, seat, Back);
        Press(host, seat, Right);
        ctx.Check(shell.DisplayModeChoice == DisplayWords.Fullscreen,
            $"a sideways step on the focused row takes the display mode after the borderless default ({shell.DisplayModeChoice ?? "unset"})");
        // The row's own words are this machine's screen sizes, so what it steps to is read back off
        // the shell rather than named: the claim is that the step lands on a size the screen offers
        // and not on the screen's own size the row opened at.
        WalkTo(host, seat, shell, OriginalShell.ResolutionKey);
        Press(host, seat, Right);
        ctx.Check(shell.FocusedKey == OriginalShell.ResolutionKey && shell.ResolutionChoice != null
            && shell.ResolutionChoice != ResolutionSetting.ScreenSizes().Fallback
            && shell.ResolutionWords.Contains(shell.ResolutionChoice),
            $"the size row is live again under fullscreen and steps to the next size this screen offers ({shell.ResolutionChoice ?? "unset"} of {shell.ResolutionWords.Count})");
        WalkTo(host, seat, shell, OriginalShell.VSyncKey);
        Press(host, seat, Accept);
        OpenVideoListWindow(ctx, host, seat, shell);
        WalkTo(host, seat, shell, OriginalShell.VSyncKey);
        Press(host, seat, Accept);
        Press(host, seat, Down);
        Press(host, seat, Down);
        Press(host, seat, Accept);
        ctx.Check(shell.VSyncChoice == "120" && shell.FocusedKey == OriginalShell.VSyncKey,
            $"and picking two below the off default closes it on the 120 fps cap ({shell.VSyncChoice ?? "unset"}, {shell.FocusedKey})");
        WalkTo(host, seat, shell, OriginalShell.GraphicsKey);
        Press(host, seat, Accept);
        ctx.Check(shell.GraphicsChoice == GraphicsMode.EnhancedWord,
            $"Accept on the checkbox under it flips the graphics word ({shell.GraphicsChoice})");
        WalkTo(host, seat, shell, OriginalShell.VideoCancelKey);
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.Options && shell.GraphicsChoice == GraphicsMode.Default
            && shell.VSyncChoice == null && shell.DisplayModeChoice == null && shell.ResolutionChoice == null
            && shell.MonitorChoice == null && exits.Count == before,
            $"CANCEL CHANGES lands back on Preferences with all five edits dropped and no exit ({shell.Screen}, {shell.GraphicsChoice}, {shell.VSyncChoice ?? "unset"}, {shell.DisplayModeChoice ?? "unset"}, {shell.ResolutionChoice ?? "unset"}, {shell.MonitorChoice ?? "unset"})");

        WalkTo(host, seat, shell, OriginalShell.VideoDoorKey);
        Press(host, seat, Accept);
        WalkTo(host, seat, shell, OriginalShell.DisplayModeKey);
        Press(host, seat, Right);
        WalkTo(host, seat, shell, OriginalShell.VSyncKey);
        Press(host, seat, Right);
        WalkTo(host, seat, shell, OriginalShell.GraphicsKey);
        Press(host, seat, Accept);
        WalkTo(host, seat, shell, OriginalShell.VideoAcceptKey);
        Press(host, seat, Accept);
        ctx.Check(exits.Count == before + 1 && exits[^1] is OptionsApplyExit
        {
            Graphics: GraphicsMode.EnhancedWord, VSync: "60",
            DisplayMode: DisplayWords.Fullscreen,
        },
            $"and ACCEPT CHANGES leaves through the host as one OptionsApplyExit carrying all three words ({exits.Count - before}, {exits[^1].GetType().Name})");
        ctx.Check(!host.Shown, $"with the presentation hidden for the launcher to act (shown={host.Shown})");
    }

    // The V-Sync list, whose five words outrun the four-row window its own layout row authors: every
    // word is a row so the walk still reaches it, only the window's own are drawn, the arrows and the
    // thumb stand inside the box's right edge, the pointer wheels and drags the window, and a press
    // on the word outside it reaches no row at all. The list is left closed on nothing picked.
    private static void OpenVideoListWindow(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell)
    {
        int drawn = 0;
        foreach (var row in shell.Rows)
        {
            drawn += row.Kind == OriginalRowKind.ListRow && row.Visible ? 1 : 0;
        }

        ctx.Check(shell.OpenVideoOption == OriginalShell.VSyncKey
            && shell.Rows.Count == DisplayWords.VSyncChoices.Count + 2 && drawn == 4,
            $"Accept on the V-Sync row opens its five choices in the authored four-row window ({shell.Rows.Count} rows, {drawn} drawn)");
        var bar = List(shell, OriginalShell.VSyncKey);
        var arrow = Row(shell, OriginalShell.VSyncKey + ":down");
        float edge = (bar?.Window.X ?? 0f) + (bar?.Window.Width ?? 0f);
        ctx.Check(bar is { Window.Scrolls: true } && arrow != null
            && bar.Window.ThumbX + bar.Window.ThumbWidth <= edge && arrow.X + arrow.Width <= edge,
            $"with its thumb and its arrows inside the box's own right edge (thumb {bar?.Window.ThumbX ?? -1}, arrow {arrow?.X ?? -1}, edge {edge})");
        var size = ctx.Host.GetViewport().GetVisibleRect().Size;
        var fit = BoardFit.For(size.X, size.Y);
        WheelAndDrag(ctx, host, seat, shell, fit, OriginalShell.VSyncKey, "the V-Sync page's open list");
        var offscreen = Row(shell, OriginalShell.VSyncKey + ":4");
        ctx.Check(offscreen is { Visible: false },
            $"the word outside the window keeps its place for the walk, unseen ({offscreen?.Visible.ToString() ?? "missing"})");
        if (offscreen == null)
        {
            return;
        }

        Click(host, seat, Pointer(fit, offscreen.X + 2f, offscreen.Y + 2f, pressed: true, clicked: true));
        ctx.Check(shell.VSyncChoice == null && shell.OpenVideoOption == null,
            $"and a press on it picks nothing, the pointer reaching no row it cannot see ({shell.VSyncChoice ?? "unset"})");
    }

    // Original's AUDIO route over the install's decoded sections: the Preferences page's second door
    // opens the decoded page on its Master slider, the page draws a thumb per row, a sideways step
    // moves a level and clamps at silence rather than wrapping to full, the open page states its mix
    // to the host every frame and names the level a frame moved only when one did, CANCEL CHANGES
    // drops the edits without an exit and ends the preview, and ACCEPT CHANGES on a second visit
    // leaves as one apply exit.
    private static void OriginalAudioRoute(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell? shell, List<MenuExit> exits, RecordingAudio audio)
    {
        if (shell == null)
        {
            return;
        }

        int before = exits.Count;
        WalkTo(host, seat, shell, "MM_B_PREFERENCES");
        Press(host, seat, Accept);
        WalkTo(host, seat, shell, OriginalShell.AudioDoorKey);
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.Audio && shell.FocusedKey == OriginalShell.AudioMasterKey,
            $"AUDIO opens the decoded page on its Master row, the first of the authored rows it carries ({shell.Screen}, {shell.FocusedKey})");
        var board = shell.Compose();
        int titles = 0;
        int thumbs = 0;
        foreach (var line in board.Lines)
        {
            titles += line.Text is "AUDIO" or "Master" or "Music Volume" or "Effects Volume" or "Voice Volume" ? 1 : 0;
        }

        foreach (var picture in board.Pictures)
        {
            thumbs += picture.Art.Name == "PF_B_Slider.png" ? 1 : 0;
        }

        ctx.Check(titles == 5, $"drawing the section's own tab title over the four row titles ({titles} of 5)");
        ctx.Check(thumbs == 4, $"with the authored thumb drawn once per row ({thumbs} of 4)");
        // The store is a scratch one under --run-tests, so the page opens on the shipped mix: every
        // level never set, each row standing at its own default.
        ctx.Check(shell.AudioMasterChoice == null && shell.AudioMusicChoice == null
            && shell.AudioEffectsChoice == null && shell.AudioVoiceChoice == null,
            $"on a mix nothing has saved, every level reading as never set ({shell.AudioMasterChoice?.ToString() ?? "unset"})");
        audio.Mixes.Clear();
        Press(host, seat, Right);
        ctx.Check(shell.AudioMasterChoice == null,
            $"a step off the top of the Master row moves nothing, so it writes nothing ({shell.AudioMasterChoice?.ToString() ?? "unset"})");
        // A step at an end moves no level, so the frame states the mix and names no moved level:
        // this is what keeps a host from sounding a category once per pointer frame of a drag.
        ctx.Check(audio.Mixes.Count == 1 && audio.Mixes[0].Moved == MenuMixLevel.None
            && audio.Mixes[0].Levels == new AudioLevels(
                AudioMix.DefaultMaster, AudioMix.DefaultMusic, AudioMix.DefaultEffects, AudioMix.DefaultVoice),
            $"the open page states the shipped mix to the host and names no moved level on a frame that moved none ({audio.Mixes.Count}, {(audio.Mixes.Count > 0 ? audio.Mixes[0].Moved : MenuMixLevel.None)})");
        WalkTo(host, seat, shell, OriginalShell.AudioMusicKey);
        audio.Mixes.Clear();
        Press(host, seat, Left);
        ctx.Check(shell.AudioMusicChoice == AudioMix.DefaultMusic - SliderControl.KeyStep,
            $"a sideways step on the focused row moves that level by the control's own step ({shell.AudioMusicChoice?.ToString() ?? "unset"})");
        ctx.Check(audio.Mixes.Count == 1 && audio.Mixes[0].Moved == MenuMixLevel.Music
            && audio.Mixes[0].Levels.Music == AudioMix.DefaultMusic - SliderControl.KeyStep,
            $"and names Music as the level that moved, carrying the level it moved to ({(audio.Mixes.Count > 0 ? audio.Mixes[0].Moved : MenuMixLevel.None)})");
        int endsBefore = audio.MixEnds;
        WalkTo(host, seat, shell, OriginalShell.AudioCancelKey);
        audio.Mixes.Clear();
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.Options && shell.AudioMusicChoice == null && exits.Count == before,
            $"CANCEL CHANGES lands back on Preferences with the edit dropped and no exit ({shell.Screen}, {shell.AudioMusicChoice?.ToString() ?? "unset"})");
        // Off the page there is no mix to state, and the host is told to put back the one the page
        // opened over, which is the half a player notices when it is wrong.
        ctx.Check(audio.Mixes.Count == 0 && audio.MixEnds > endsBefore,
            $"ending the preview and stating no mix off the page ({audio.Mixes.Count}, ends={audio.MixEnds - endsBefore})");

        WalkTo(host, seat, shell, OriginalShell.AudioDoorKey);
        Press(host, seat, Accept);
        WalkTo(host, seat, shell, OriginalShell.AudioVoiceKey);
        for (int step = 0; step < 12; step++)
        {
            Press(host, seat, Left);
        }

        ctx.Check(shell.AudioVoiceChoice == AudioMix.MinLevel,
            $"and stepping the Voice row past its floor clamps at silence rather than wrapping to full ({shell.AudioVoiceChoice?.ToString() ?? "unset"})");
        WalkTo(host, seat, shell, OriginalShell.AudioAcceptKey);
        Press(host, seat, Accept);
        ctx.Check(exits.Count == before + 1 && exits[^1] is OptionsApplyExit { AudioVoice: AudioMix.MinLevel, AudioMaster: null },
            $"and ACCEPT CHANGES leaves through the host as one OptionsApplyExit carrying the levels ({exits.Count - before}, {exits[^1].GetType().Name})");
        ctx.Check(!host.Shown, $"with the presentation hidden for the launcher to act (shown={host.Shown})");
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

    private static MenuCommands Pointer(BoardFit fit, float authoredX, float authoredY, bool pressed = false, bool clicked = false, int wheel = 0) =>
        new() { Pointer = new MenuPointer(fit.X(authoredX), fit.Y(authoredY), pressed, clicked, wheel) };

    // The screen's list under that key, or null. Read fresh after every frame, since a window that
    // moved is a new record.
    private static OriginalList? List(OriginalShell shell, string key)
    {
        foreach (var list in shell.Lists)
        {
            if (list.Key == key)
            {
                return list;
            }
        }

        return null;
    }

    // One click as the Original shell reads it: the press arms the row it lands on and the
    // release still on that row is what fires, so a click is two frames rather than one.
    private static void Click(MenuHost host, ScriptedSeat seat, MenuCommands frame)
    {
        Press(host, seat, frame);
        Press(host, seat, frame with { Pointer = frame.Pointer!.Value with { Pressed = false, Clicked = false } });
    }

    private static void Press(MenuHost host, ScriptedSeat seat, MenuCommands frame)
    {
        seat.Enqueue(frame);
        host.Tick(Dt);
    }

    private sealed class RecordingAudio : IMenuAudio
    {
        public List<string> Cues { get; } = new();

        // What a mix page stated this frame, and how often the preview was ended: the page names
        // four levels and the one that moved, and a real service is what applies and sounds them.
        public List<(AudioLevels Levels, MenuMixLevel Moved)> Mixes { get; } = new();

        public int MixEnds { get; private set; }

        public void Cue(MenuCue cue) => Cues.Add(cue.Name);

        public void BeginNarration(string wavName)
        {
        }

        public void EndNarration()
        {
        }

        public void PreviewMix(AudioLevels levels, MenuMixLevel moved) => Mixes.Add((levels, moved));

        public void EndMixPreview() => MixEnds++;
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
