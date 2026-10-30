using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Bindings;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using CSVM.UI.Screens;
using CSVM.Utils;

namespace CSVM.Testing;

/// <summary>
/// The Original presentation through the boundary, over the install's own decoded layout, on a
/// real <see cref="MenuHost"/> with Original selected by the CLI override. It is driven by pointer
/// frames mapped through the view's own <see cref="BoardFit"/> and by keyboard frames through the
/// host's first seat. The launch leaves as one <see cref="LaunchExit"/> and the return re-enters the
/// top level. Then a switch from mid-setup discards the picked chapter, the Options routes produce
/// their apply exits, and the resolution keeps Original with the request kept.
/// </summary>
internal static class MenuOriginalSuites
{
    private const float Dt = 1f / 60f;
    // A connection index past anything the platform hands out. A seat placed on it is off the
    // roster whatever is plugged into the machine running the suite.
    private const int AbsentPad = 99;

    // The two pads the join board walk signs on, absent for the same reason.
    private const int FirstPad = 96;
    private const int SecondPad = 97;

    // The scrapbook's Current Mission tab, the one plaque a campaign board puts in the top band.
    // Its art starts at authored x 558 and is 114 wide, so a chip row in the corner clears this.
    private const float BookTabRight = 672f;

    // The install's [@GameOptions@] plate: its corner, the art's own height, the pitch between row
    // panels, and the line its two plaques stand on. The pitch is the art's band, which the page
    // holds too. The other three are pins on the install's values; the walk reads them off the layout.
    private const float AuthoredPlateY = 215f;
    private const float AuthoredPlateHeight = 289f;
    private const float AuthoredGameOptionPitch = 62f;
    private const float AuthoredPlaqueY = 457f;


    private static readonly MenuCommands Accept = new() { Accept = true };
    private static readonly MenuCommands Down = new() { MoveY = 1 };
    private static readonly MenuCommands Up = new() { MoveY = -1 };
    private static readonly MenuCommands Right = new() { MoveX = 1 };
    private static readonly MenuCommands Left = new() { MoveX = -1 };
    private static readonly MenuCommands Back = new() { Back = true };

    [Suite("menu-original-tracer",
        "Original's hold on the display server over the install's decoded layout: shown at the top "
        + "level with the OS pointer hidden while it draws its own, the VIDEO page's Monitor row "
        + "offering one label per screen the engine reports and a sideways step taking one of them, "
        + "and the pointer back once the presentation hides")]
    internal static void MenuOriginalTracer(TestContext ctx)
    {
        MenuSuiteHost.RequireLayout(ctx);
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        var seat = new ScriptedSeat();
        var registry = new PresentationRegistry();
        var planes = MenuSuiteHost.ScratchPlanes(ctx, "menu-original-tracer");
        registry.Register(PresentationId.Original, () => new OriginalPresentation(
            ctx.Host, ctx.DataRoot, layout, string.Empty, new MenuInput { Keyboard = true }, data: ctx.Data)
        {
            Planes = planes,
        });
        var host = new MenuHost(registry, new RecordingAudio(), _ => { });
        MenuSuiteHost.AddFeatures(host, ctx.DataRoot, ctx.Data);
        host.AddSeat(seat);
        try
        {
            host.Select("original");
            host.Show(MenuReturnDestination.TopLevel);
            var shell = (host.Active as OriginalPresentation)?.Shell;
            ctx.Check(shell is { Screen: OriginalScreen.TopLevel } && Godot.Input.MouseMode == Godot.Input.MouseModeEnum.Hidden,
                $"the OS pointer is hidden while Original draws its own ({shell?.Screen}, {Godot.Input.MouseMode})");
            if (shell == null)
            {
                return;
            }

            WalkTo(host, seat, shell, "MM_B_PREFERENCES");
            Press(host, seat, Accept);
            WalkTo(host, seat, shell, OriginalOptionsScreen.VideoDoorKey);
            Press(host, seat, Accept);
            // The monitor row's words are this machine's screens, so the claim is the count rather than
            // the labels. That is one per screen the engine reports.
            ctx.Check(shell.Screen == OriginalScreen.Video && shell.Options.Video.MonitorWords.Count == Godot.DisplayServer.GetScreenCount(),
                $"the monitor row offers one label per screen ({shell.Screen}, {string.Join(" ", shell.Options.Video.MonitorWords)})");
            Press(host, seat, Right);
            ctx.Check(shell.Options.Video.MonitorChoice != null
                && OptionsStore.TryParseMonitorIndex(shell.Options.Video.MonitorChoice, out int picked) && picked < shell.Options.Video.MonitorWords.Count,
                $"and a sideways step on it takes a screen this machine has ({shell.Options.Video.MonitorChoice ?? "unset"} of {shell.Options.Video.MonitorWords.Count})");
            host.Deactivate();
            ctx.Check(Godot.Input.MouseMode == Godot.Input.MouseModeEnum.Visible,
                $"and the OS pointer is back once the presentation hides ({Godot.Input.MouseMode})");
        }
        finally
        {
            host.Deactivate();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            MenuSuiteHost.DropScratchPlanes(ctx, "menu-original-tracer");
        }
    }

    [Suite("menu-original-tracer-core",
        "Original Free Flight through the presentation boundary over the install's decoded layout: "
        + "selected by the CLI override, shown at the top level with the "
        + "Free Flight door focused and the roster's builds read from a scratch store of the suite's "
        + "own, a pointer frame over Quit takes focus and cues the rollover, a "
        + "press on a plaque arms it and opens nothing while the pointer over it draws the active "
        + "bitmap, a release on another plaque activates neither, a "
        + "click on the door opens Free Flight on the lobby's layout with its rules greyed, keyboard "
        + "frames pick a map on Mission Options and an aircraft on Select Plane and FLY leaves as one "
        + "LaunchExit, the return re-enters the top level, PREFERENCES and its "
        + "GAME OPTIONS door open the decoded page whose Difficulty dropdown stands first and whose "
        + "seven rows scroll four at a time through a window and take every choice, none of them the menu presentation, on a plate grown a band "
        + "to hold them where each row stands wholly inside a band of its own "
        + "and each checkbox row's title stands on its own box's "
        + "centre line at the dropdown rows' column, and whose CANCEL CHANGES "
        + "drops them, a wheel step over "
        + "Instant Action's contents window moves it one row and clamps "
        + "at the head, a drag down its thumb's track lands the window on its last row without "
        + "activating what the click stood over, and the contents arrows still step it, "
        + "a seat whose pad drops off the roster holds it through the grace and leaves once the "
        + "device stays gone past it, the Instant Action screen reads the roster without opening "
        + "joining and a seat signed on at the board stays seated, the campaign flight check carries the "
        + "seat strip with two seats and none with one, drawn as a chip row in the "
        + "top-right corner clear of the book tab, a switch from mid-setup discards the pick and the "
        + "fresh instance starts on the top level, Original's VIDEO door opens the decoded page on its Display Mode dropdown "
        + "which fits its authored window and draws no bar, over the V-Sync one whose five words "
        + "window into four with the arrows and the thumb inside the box's right edge and the fifth "
        + "kept for the walk but unseen and unhit, that list wheeling and dragging like any other "
        + "and picking a frame cap, over the Render Scale and Anti-aliasing rows, the Shadow Quality row "
        + "dead under Original and live once the Enhanced Graphics checkbox flips, View Distance, Vegetation, 3D Vegetation, Ground Cover and then VR Mode "
        + "scrolling into the window's foot, whose CANCEL CHANGES drops them all with no exit and whose ACCEPT CHANGES "
        + "leaves as one more apply exit carrying them, Original's AUDIO door opens the decoded page "
        + "on its Master slider over four thumbs, a sideways step moves a level and clamps at "
        + "silence, the open page states its mix to the host every frame and names the level a "
        + "frame moved only where one moved, its CANCEL CHANGES drops the edit with no exit and "
        + "ends the preview, and its ACCEPT CHANGES leaves as "
        + "one more apply exit carrying the levels, an unknown override resolves to Original and a "
        + "missing layout keeps Original selected with the request kept")]
    internal static void MenuOriginalTracerCore(TestContext ctx)
    {
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        MenuSuiteHost.RequireLayout(ctx);
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
        // One build past the stock airframes, so the roster check below has a custom to compare.
        var planes = MenuSuiteHost.ScratchPlanes(ctx, "menu-original-tracer-core", 1);
        // ⚠ The debrief return below opens the campaign, so the presentation is pointed at a
        // scratch store: nothing here may read or write user://Profiles.
        string profiles = System.IO.Path.Combine(ctx.ScratchDir, "menu-original-tracer-core", "Profiles");
        var player1 = new MenuInput { Keyboard = true };
        registry.Register(PresentationId.Original, () => new OriginalPresentation(
            ctx.Host, ctx.DataRoot, layout, string.Empty, player1, data: ctx.Data)
        {
            CampaignProfiles = new CSVM.Session.Campaign.CampaignProfileStore(profiles),
            Planes = planes,
        });
        var host = new MenuHost(registry, audio, exits.Add);
        MenuSuiteHost.AddFeatures(host, ctx.DataRoot, ctx.Data);
        host.AddSeat(seat);
        try
        {
            var shell = ColdStart(ctx, host);
            if (shell == null)
            {
                return;
            }

            var customs = host.Features.Get<PlayerSetupFeature>().Roster.Where(a => a.IsCustom).Select(a => a.Name);
            var scratch = planes.List().Select(p => p.Name);
            ctx.Check(customs.SequenceEqual(scratch),
                $"the roster's builds are the suite's scratch store's and no player's ({string.Join(", ", customs)})");
            Pointer(ctx, host, seat, shell, audio);
            Fly(ctx, host, seat, shell, exits);
            Return(ctx, host, shell, exits);
            Lists(ctx, host, seat, shell);
            Seats(ctx, host, seat, shell, player1);
            OriginalOptionsRoute(ctx, host, seat, shell, exits, layout);
            SwitchMidSetup(ctx, host, seat, shell);
            // The VIDEO route runs on the shell the switch creates, and last of the Original walks.
            // Its ACCEPT CHANGES hides the presentation for the launcher, so nothing after it can
            // drive the same shell.
            OriginalVideoRoute(ctx, host, seat, SwitchBackToOriginal(ctx, host), exits, layout);
            // The AUDIO route needs a shell of its own for the same reason, the VIDEO route's
            // ACCEPT CHANGES having hidden the presentation the walk before it drove.
            OriginalAudioRoute(ctx, host, seat, SwitchBackToOriginal(ctx, host), exits, audio, layout);
            Recovery(ctx, host, registry, audio);
        }
        finally
        {
            host.Deactivate();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            MenuSuiteHost.DropScratchPlanes(ctx, "menu-original-tracer-core");
        }

        ctx.Check(host.Active == null && !host.Shown, $"Deactivate leaves the host holding no presentation");
    }

    [Suite("menu-join-board",
        "The join board over the install's decoded layout: the top level's third door opens it and "
        + "it is the one screen joining is open on, its manifest starting as four open seats, the "
        + "first pad to sign on taking the captain's chair beside the keyboard and the second a seat "
        + "of its own with the manifest drawing both, B on the second pad giving that seat up and "
        + "leaving the captain's entry where it was, the captain's Start casting off to the top "
        + "level with the manifest standing, BACK dropping every sign-on, and Free Flight, Dogfight "
        + "and Instant Action reading the roster without opening joining, their footer and hint no "
        + "longer inviting a pad's START")]
    internal static void MenuJoinBoard(TestContext ctx)
    {
        MenuSuiteHost.RequireLayout(ctx);
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        var seat = new ScriptedSeat();
        var registry = new PresentationRegistry();
        var player1 = new MenuInput { Keyboard = true };
        var planes = MenuSuiteHost.ScratchPlanes(ctx, "menu-join-board");
        registry.Register(PresentationId.Original, () => new OriginalPresentation(
            ctx.Host, ctx.DataRoot, layout, string.Empty, player1, data: ctx.Data)
        {
            Planes = planes,
        });
        var host = new MenuHost(registry, new RecordingAudio(), _ => { });
        MenuSuiteHost.AddFeatures(host, ctx.DataRoot, ctx.Data);
        host.AddSeat(seat);
        try
        {
            host.Select("original");
            host.Show(MenuReturnDestination.TopLevel);
            var original = host.Active as OriginalPresentation;
            ctx.Check(original?.Shell != null && original.Devices != null,
                $"Original stands at the top level with its shell and its pad roster ({host.Active?.Id})");
            if (original?.Shell is not { } shell || original.Devices is not { } devices)
            {
                return;
            }

            SignOnAtTheBoard(ctx, host, seat, shell, devices);
            JoiningIsTheBoardsAlone(ctx, host, seat, shell);
        }
        finally
        {
            host.Deactivate();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            MenuSuiteHost.DropScratchPlanes(ctx, "menu-join-board");
        }
    }

    // The board itself. The gestures are raw device reads and a scripted run has no pad to press,
    // so the walk calls what the board's own scan calls. The two pad indices are ones no real
    // device holds, so a machine with pads plugged in walks this path too.
    private static void SignOnAtTheBoard(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, MenuSeatDevices devices)
    {
        var setup = host.Features.Get<PlayerSetupFeature>();
        WalkTo(host, seat, shell, OriginalShell.JoinBoardKey);
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.JoinBoard && shell.JoiningOpen,
            $"the top level's JOIN BOARD door opens the one screen joining is open on ({shell.Screen}, open={shell.JoiningOpen})");
        ctx.Check(BoardLines(shell, "open seat") == 4 && BoardLines(shell, "signed on") == 0,
            $"whose manifest starts as four open seats ({BoardLines(shell, "open seat")} open)");

        ctx.Check(devices.SignOn(FirstPad) && devices.P1Pad == FirstPad,
            $"A on the first pad takes the captain's chair, seat 1 beside the keyboard ({devices.P1Pad})");
        ctx.Check(devices.SignOn(SecondPad) && setup.Seats.Count == 2,
            $"and A on the second signs it onto a seat of its own ({setup.Seats.Count} seats)");
        ctx.Check(devices.SignedOn(0) && devices.SignedOn(1) && !devices.SignedOn(2),
            $"so two entries hold a pad and the rest stay open");
        ctx.Check(BoardLines(shell, "signed on") == 2 && BoardLines(shell, "open seat") == 2,
            $"which is what the manifest draws ({BoardLines(shell, "signed on")} signed on)");

        ctx.Check(devices.SignOff(SecondPad) && setup.Seats.Count == 1 && devices.P1Pad == FirstPad,
            $"B on the second pad gives its seat up, the entry above it staying put ({devices.P1Pad}, {setup.Seats.Count} seats)");
        ctx.Check(shell.JoinBoard.CastOff() && shell.Screen == OriginalScreen.TopLevel && devices.P1Pad == FirstPad,
            $"and the captain's Start casts off with the manifest standing ({shell.Screen}, {devices.P1Pad})");

        // The other way off, which keeps nobody.
        WalkTo(host, seat, shell, OriginalShell.JoinBoardKey);
        Press(host, seat, Accept);
        Press(host, seat, Back);
        ctx.Check(shell.Screen == OriginalScreen.TopLevel && devices.P1Pad < 0 && setup.Seats.Count == 1,
            $"BACK leaves the board and drops the sign-ons ({shell.Screen}, {devices.P1Pad}, {setup.Seats.Count} seats)");
    }

    // The screens seats used to be decided on: each reads the roster the board wrote, and none of
    // them opens the gesture that writes it.
    private static void JoiningIsTheBoardsAlone(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell)
    {
        var doors = new (string Key, OriginalScreen Screen)[]
        {
            (OriginalShell.FreeFlightKey, OriginalScreen.FreeFlight),
            (OriginalShell.DogfightKey, OriginalScreen.Dogfight),
            ("MM_B_INSTANTACTION", OriginalScreen.InstantAction),
        };
        foreach (var door in doors)
        {
            WalkTo(host, seat, shell, door.Key);
            Press(host, seat, Accept);
            ctx.Check(shell.Screen == door.Screen && !shell.JoiningOpen,
                $"{door.Screen} reads the roster without opening joining ({shell.Screen}, open={shell.JoiningOpen})");
            if (door.Screen == OriginalScreen.FreeFlight)
            {
                ctx.Check(BoardLines(shell, "START") == 0 && BoardLines(shell, "to join") == 0,
                    $"and neither its footer nor its hint invites a pad's START ({BoardLines(shell, "START")} lines)");
            }

            host.Show(MenuReturnDestination.TopLevel);
        }

        ctx.Check(shell.Screen == OriginalScreen.TopLevel, $"the walk lands back on the top level ({shell.Screen})");
    }

    // How many composed lines carry a piece of text. The manifest states a seat in words rather
    // than in a row, so its entries are counted here.
    private static int BoardLines(OriginalShell shell, string text)
    {
        int count = 0;
        foreach (var line in shell.Compose().Lines)
        {
            if (line.Text.Contains(text, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static OriginalShell? ColdStart(TestContext ctx, MenuHost host)
    {
        string? reason = host.Select("original");
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
        ctx.Check(shell?.Rows.Count == 9, $"the top level is the six decoded rows plus the three doors ({shell?.Rows.Count})");
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
        ctx.Check(multiplayer is { Enabled: true }, $"the Multiplayer plaque is live over the network door");
        if (multiplayer != null)
        {
            Press(host, seat, Pointer(fit, multiplayer.X + 5f, multiplayer.Y + 5f));
            ctx.Check(shell.FocusedKey == OriginalShell.MultiplayerKey && audio.Cues.Count == 2,
                $"a pointer over the Multiplayer plaque takes the focus and cues a rollover ({shell.FocusedKey}, {audio.Cues.Count})");
        }

        Press(host, seat, Pointer(fit, campaign.X + 5f, campaign.Y + 5f));
        ctx.Check(shell.FocusedKey == OriginalShell.CampaignKey && audio.Cues.Count == 3,
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

    // Keyboard frames on the local lobby: the map box, three rows down its list, then Select Plane's
    // box the same way, then FLY.
    private static void Fly(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, List<MenuExit> exits)
    {
        var setup = host.Features.Get<PlayerSetupFeature>();
        ctx.Check(shell.FocusedKey == OriginalLobbyScreen.MissionTabKey && Row(shell, OriginalLobbyScreen.TimeKey) is { Enabled: false }
            && Row(shell, OriginalLobbyScreen.TimeRadioKey) is { Enabled: false } && Row(shell, OriginalLobbyScreen.LimitedLivesKey) is { Enabled: false },
            $"Free Flight opens on the lobby's Mission Options tab with the mission rules greyed ({shell.FocusedKey})");
        WalkTo(host, seat, shell, OriginalLobbyScreen.EnvironmentKey);
        Press(host, seat, Accept);
        Press(host, seat, Down);
        Press(host, seat, Down);
        Press(host, seat, Down);
        Press(host, seat, Accept);
        ctx.Check(shell.LocalLobby.PickedChapter(OriginalScreen.FreeFlight) == "C2", $"three rows down the map list and Accept picks Hollywood ({shell.LocalLobby.PickedChapter(OriginalScreen.FreeFlight)})");
        ctx.Check(host.Features.Get<FreeFlightFeature>().Chapter?.Code == "C2",
            $"and the shared feature holds the pick ({host.Features.Get<FreeFlightFeature>().Chapter?.Code})");
        WalkTo(host, seat, shell, OriginalLobbyScreen.PlaneTabKey);
        Press(host, seat, Accept);
        WalkTo(host, seat, shell, OriginalLobbyScreen.PlaneKey);
        Press(host, seat, Accept);
        Press(host, seat, Down);
        Press(host, seat, Down);
        Press(host, seat, Down);
        Press(host, seat, Accept);
        ctx.Check(shell.LocalLobby.Tab == LobbyTab.Plane && setup.Roster[setup.Seats[0].Cursor].Node == "player_bhawk",
            $"Select Plane's box three rows down picks seat 0's aircraft ({setup.Roster[setup.Seats[0].Cursor].Node})");
        WalkTo(host, seat, shell, OriginalShell.FlyKey);
        ctx.Check(shell.FocusedKey == OriginalShell.FlyKey, $"the cursor reaches FLY ({shell.FocusedKey})");
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
        ctx.Check(shell.Screen == OriginalScreen.TopLevel && !host.Features.Get<PlayerSetupFeature>().Seats[0].Confirmed,
            $"on the top level with seat 0's confirmation dropped ({shell.Screen})");
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

    // The pointer's wheel and thumb over Instant Action's contents window. It is wheeled a row,
    // dragged down its track and left back at its head, the list's own arrows still stepping it.
    private static void Lists(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell)
    {
        var size = ctx.Host.GetViewport().GetVisibleRect().Size;
        var fit = BoardFit.For(size.X, size.Y);
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

    // Seats and joining. A scripted run has no pad, so the claim is driven through the poller's
    // own record of the pad seat 0 last steered with. The join goes through the feature, where a
    // pad's Start lands; the per-screen rule itself is read off the shell.
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

        PadBlipGrace(ctx, host, setup, devices, player1);

        WalkTo(host, seat, shell, "MM_B_INSTANTACTION");
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.InstantAction && !shell.JoiningOpen,
            $"the Instant Action screen reads the roster without opening joining ({shell.Screen}, open={shell.JoiningOpen})");
        var s2 = new ScriptedSeat();
        ctx.Check(setup.Join(s2) != null && host.Seats.Count == 2,
            $"a seat signed on at the board is one the screen shows ({host.Seats.Count})");
        Press(host, seat, Down);
        ctx.Check(host.Seats.Count == 2 && shell.Screen == OriginalScreen.InstantAction,
            $"and stays seated while seat 0 keeps steering the screen ({host.Seats.Count}, {shell.Screen})");

        host.Show(MenuReturnDestination.TopLevel);
        shell.Campaign.OpenCampaignOver(CampaignAidProfiles.Store(seeded: true), CampaignAidProfiles.Planes());
        ctx.Check(shell.Campaign.ShowCabin(CampaignAidProfiles.Pilot), $"the scratch campaign seats its pilot");
        shell.Campaign.ShowMissionScreen(OriginalScreen.CampaignFlightCheck);
        ctx.Check(shell.Screen == OriginalScreen.CampaignFlightCheck && !shell.JoiningOpen,
            $"and the flight check the same ({shell.Screen}, open={shell.JoiningOpen})");
        ctx.Check(StripSeats(shell) == 2, $"its board carries the seat strip naming both seats ({StripSeats(shell)} lines)");
        ctx.Check(StripIsChipRow(shell, out string chips),
            $"drawn as a chip row, tags in their own seat inks in the top-right corner clear of the book tab ({chips})");
        Press(host, s2, Back);
        ctx.Check(host.Seats.Count == 1, $"the second seat's Back unjoins it ({host.Seats.Count})");
        ctx.Check(StripSeats(shell) == 0, $"and the strip goes with it, leaving the authored board alone ({StripSeats(shell)} lines)");
        host.Show(MenuReturnDestination.TopLevel);
        ctx.Check(shell.Screen == OriginalScreen.TopLevel && !host.Features.Get<CampaignFeature>().IsOpen,
            $"a top-level show closes the scratch campaign again ({shell.Screen})");
    }

    // Steam Input re-enumerates its virtual pads mid-menu, and a seat that leaves on every blip is
    // the join flow coming apart. The roster is live here because --no-pads has none to come back
    // to. The pad index is one no real device can hold, so the grace is what decides.
    private static void PadBlipGrace(
        TestContext ctx, MenuHost host, PlayerSetupFeature setup, MenuSeatDevices devices, MenuInput player1)
    {
        bool disabled = Pads.Disabled;
        var bound = player1.Pads;
        Pads.Disabled = false;
        int before = setup.Seats.Count;
        var seat = setup.Join(new MenuInputSeat(new MenuInput { Pads = new[] { AbsentPad } }));
        devices.Sync(MenuSeatDevices.DeviceGrace / 2f);
        ctx.Check(seat != null && setup.Seats.Count == before + 1,
            $"a seat whose pad drops off the roster holds it through the grace ({setup.Seats.Count} seats)");
        devices.Sync(MenuSeatDevices.DeviceGrace);
        ctx.Check(setup.Seats.Count == before,
            $"and leaves once the device stays gone past it ({setup.Seats.Count} seats)");

        Pads.Disabled = disabled;
        player1.Pads = bound;
        if (seat != null && setup.Seats.Count > before)
        {
            setup.Unjoin(seat);
        }

        host.Tick(Dt);
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

    // Whether the strip is a chip row: the player tags alone, each in its own
    // seat's ink. The row sits on one line of the corner and begins right of the book tab.
    private static bool StripIsChipRow(OriginalShell shell, out string report)
    {
        var chips = new List<BoardLine>();
        foreach (var panel in shell.Compose().Overlays)
        {
            foreach (var line in panel.Lines)
            {
                if (line.Text.Length == 2 && line.Text[0] == 'P' && char.IsDigit(line.Text[1]))
                {
                    chips.Add(line);
                }
            }
        }

        bool ok = chips.Count > 0;
        float left = BoardFit.AuthoredWidth;
        for (int i = 0; i < chips.Count; i++)
        {
            ok &= chips[i].Text == SplitScreen.PlayerTag(i) && chips[i].Ink == SeatStrip.Ink(i)
                && chips[i].Y == SeatStrip.Inset;
            left = System.Math.Min(left, chips[i].X);
        }

        ok &= left > BookTabRight;
        report = $"{chips.Count} chips from x {(int)left}, first {(chips.Count > 0 ? chips[0].Ink.ToString() : "none")}";
        return ok;
    }

    // The host's switch: Deactivate, Select and Show. From mid-setup on the Free Flight screen,
    // Deactivate discards the feature's pick, so the fresh instance the switch shows has none.
    private static void SwitchMidSetup(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell)
    {
        Press(host, seat, Accept);
        WalkTo(host, seat, shell, OriginalLobbyScreen.EnvironmentKey);
        Press(host, seat, Accept);
        Press(host, seat, Down);
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.FreeFlight && host.Features.Get<FreeFlightFeature>().Chapter != null,
            $"mid-setup: on the Free Flight screen with a chapter picked ({shell.Screen})");
        host.Deactivate();
        ctx.Check(host.Active == null && host.Features.Get<FreeFlightFeature>().Chapter == null,
            $"Deactivate frees Original and discards the unfinished pick");
    }
    // Original's own Options route over the install's decoded sections. PREFERENCES opens the
    // Preferences page, and its GAME OPTIONS door the decoded page. That page's rows take every
    // choice, and its CANCEL CHANGES drops them. The walk leaves the top level as it found it.
    // The first three rows are the original's own, in its order, the two after them this port's.
    private static void OriginalOptionsRoute(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, List<MenuExit> exits, MenuLayout layout)
    {
        WalkTo(host, seat, shell, "MM_B_PREFERENCES");
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.Options && shell.FocusedKey == OriginalOptionsScreen.GameOptionsDoorKey,
            $"PREFERENCES opens the Preferences page with its GAME OPTIONS door focused ({shell.Screen}, {shell.FocusedKey})");
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.GameOptions && shell.FocusedKey == OriginalGameOptionsPage.DifficultyKey,
            $"GAME OPTIONS opens the decoded page on its Difficulty dropdown, the first row ({shell.Screen}, {shell.FocusedKey})");
        var board = shell.Compose();
        string tab = Words(layout, "GameOptions", "GO_T_TITLE");
        ctx.CheckShipped(tab == "GAME OPTIONS", $"the GAME OPTIONS tab title is the install's ({tab})");
        int titles = 0;
        int menuTitles = 0;
        foreach (var line in board.Lines)
        {
            titles += line.Text == tab || line.Text is "Difficulty" or "Default View" or "Auto Head Turn"
                or "Next Target" ? 1 : 0;
            menuTitles += line.Text is "Menu" ? 1 : 0;
        }

        ctx.Check(tab.Length > 0 && titles == 5 && menuTitles == 0,
            $"drawing the section's own tab title over the window's four row titles and no Menu row ({titles} of 5, {menuTitles} Menu)");
        OriginalGameOptionsPlate(ctx, shell, board, layout.Screen("GameOptions"));
        Press(host, seat, Accept);
        ctx.Check(shell.Options.GameOptions.OpenOption == OriginalGameOptionsPage.DifficultyKey && shell.Rows.Count == 3
            && List(shell, OriginalGameOptionsPage.DifficultyKey) == null,
            $"Accept opens the dropdown over the three campaign tiers, inside its window and with no bar ({shell.Options.GameOptions.OpenOption}, {shell.Rows.Count})");
        Press(host, seat, Down);
        Press(host, seat, Accept);
        ctx.Check(shell.Options.GameOptions.DifficultyChoice == CSVM.Flight.Hangar.Difficulty.Hard && shell.FocusedKey == OriginalGameOptionsPage.DifficultyKey,
            $"and picking the second closes it on Hard ({shell.Options.GameOptions.DifficultyChoice}, {shell.FocusedKey})");
        Press(host, seat, Down);
        Press(host, seat, Accept);
        ctx.Check(shell.Options.GameOptions.OpenOption == OriginalGameOptionsPage.DefaultViewKey && shell.Rows.Count == 3,
            $"Accept on the Default View row under it opens the dropdown over the original's three views ({shell.Options.GameOptions.OpenOption}, {shell.Rows.Count})");
        // The list opens on the item the row stands at, which with nothing saved is the last of the
        // three. One step down wraps onto the first.
        Press(host, seat, Down);
        Press(host, seat, Accept);
        ctx.Check(shell.Options.GameOptions.DefaultViewChoice == CSVM.Flight.Camera.PilotView.Name(CSVM.Flight.Camera.PilotViewMode.Cockpit),
            $"and a step down wraps onto the list's own first view and picks it ({shell.Options.GameOptions.DefaultViewChoice ?? "none"})");
        Press(host, seat, Down);
        Press(host, seat, Accept);
        ctx.Check(shell.Options.GameOptions.AutoHeadTurnChoice == true && shell.FocusedKey == OriginalGameOptionsPage.AutoHeadTurnKey,
            $"Accept on the Auto Head Turn checkbox under it turns the head turn on ({shell.Options.GameOptions.AutoHeadTurnChoice})");
        Press(host, seat, Down);
        ctx.Check(shell.FocusedKey == OriginalGameOptionsPage.NearestAfterKillKey,
            $"the row straight under the head turn is Next Target, no presentation row standing between ({shell.FocusedKey})");
        Press(host, seat, Accept);
        ctx.Check(shell.Options.GameOptions.NearestAfterKillChoice == true,
            $"Accept on the Next Target checkbox turns the targeting setting on ({shell.Options.GameOptions.NearestAfterKillChoice})");
        // A walk off the window's last row scrolls Rumble, then the gun aids under it, into view.
        Press(host, seat, Down);
        Press(host, seat, Down);
        bool leadingShown = shell.Rows.FirstOrDefault(r => r.Key == OriginalGameOptionsPage.LeadingAimAssistKey) is { Visible: true };
        ctx.Check(shell.FocusedKey == OriginalGameOptionsPage.LeadingAimAssistKey && leadingShown && shell.Options.GameOptions.Top == 2,
            $"the walk off Next Target scrolls the window a row at a time onto Leading Aim ({shell.FocusedKey}, shown {leadingShown}, top {shell.Options.GameOptions.Top})");
        WalkTo(host, seat, shell, OriginalGameOptionsPage.CancelKey);
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.Options
            && shell.Options.GameOptions.DifficultyChoice == CSVM.Flight.Hangar.Difficulty.Normal && shell.Options.GameOptions.NearestAfterKillChoice == null
            && shell.Options.GameOptions.DefaultViewChoice == null && shell.Options.GameOptions.AutoHeadTurnChoice == null,
            $"CANCEL CHANGES lands back on Preferences with every edit dropped ({shell.Screen}, {shell.Options.GameOptions.DifficultyChoice}, {shell.Options.GameOptions.NearestAfterKillChoice}, {shell.Options.GameOptions.DefaultViewChoice ?? "none"}, {shell.Options.GameOptions.AutoHeadTurnChoice})");
        WalkTo(host, seat, shell, OriginalShell.OptionsBackKey);
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.TopLevel && exits.Count == 1,
            $"and RETURN TO MAIN MENU leaves the page without an exit ({shell.Screen}, {exits.Count})");
        WalkTo(host, seat, shell, OriginalShell.FreeFlightKey);
    }

    // The grown plate and what stands on it. The page carries more rows than the section's three.
    // The plate is drawn as a head, the repeated row band and a tail rather than one picture. It
    // reaches a whole band further down than the art it is cut from. The two plaques stand that
    // same band below their authored line. Every row in the window stands wholly inside a band of
    // its own, and no row's own reach crosses the moved plaque line.
    private static void OriginalGameOptionsPlate(TestContext ctx, OriginalShell shell, ComposedBoard board, MenuLayoutScreen? section)
    {
        string art = section?.Widget("GO_BACKGROUND")?.Art.FirstOrDefault() ?? string.Empty;
        float plateY = section?.Widget("GO_BACKGROUND")?.Int("Y") ?? -1f;
        float plaqueY = section?.Widget(OriginalGameOptionsPage.AcceptKey)?.Int("Y") ?? -1f;
        float plateHeight = ((IOriginalScreenHost)shell).Measure(art)?.Height ?? -1f;
        // The rows step at the section's own title pitch, which on the install is the art's band.
        float rowPitch = (section?.Widget("GO_T_VIEWTITLE")?.Int("Y") ?? 0) - (section?.Widget("GO_T_DIFFTITLE")?.Int("Y") ?? 0);
        ctx.CheckShipped(art.Equals("GO_BackGround.png", StringComparison.OrdinalIgnoreCase)
            && plateY == AuthoredPlateY && plateHeight == AuthoredPlateHeight && plaqueY == AuthoredPlaqueY
            && rowPitch == AuthoredGameOptionPitch,
            $"the plate is the install's own art at its authored corner and height, the plaques on their authored line and the rows a band apart ({art}, {plateY}, {plateHeight}, {plaqueY}, {rowPitch})");
        var plate = new List<BoardPicture>();
        foreach (var picture in board.Backdrop)
        {
            if (picture.Art.Name.Equals(art, StringComparison.OrdinalIgnoreCase))
            {
                plate.Add(picture);
            }
        }

        float top = float.MaxValue;
        float bottom = 0f;
        bool cropped = plate.Count > 0;
        foreach (var picture in plate)
        {
            cropped &= picture.Crop is { } crop && crop.Height > 0f && picture.Width == 0f && picture.Height == 0f;
            top = Math.Min(top, picture.Y);
            bottom = Math.Max(bottom, picture.Y + (picture.Crop?.Height ?? 0f));
        }

        ctx.Check(plate.Count >= 4 && cropped,
            $"the plate is tiled from crops of its own art rather than drawn once or stretched ({plate.Count} pieces, crops {cropped})");
        ctx.Check(Math.Abs(top - plateY) < 0.5f && Math.Abs(bottom - (plateY + plateHeight + AuthoredGameOptionPitch)) < 0.5f,
            $"standing at its authored corner and reaching one whole band further down ({top}, {bottom})");

        float accept = RowY(shell, OriginalGameOptionsPage.AcceptKey);
        float cancel = RowY(shell, OriginalGameOptionsPage.CancelKey);
        ctx.Check(Math.Abs(accept - (plaqueY + AuthoredGameOptionPitch)) < 0.5f && Math.Abs(cancel - accept) < 0.5f,
            $"with ACCEPT CHANGES and CANCEL CHANGES that same band below their authored line ({accept}, {cancel})");

        // The first band opens one art band above the head crop's bottom edge. Every band after it
        // is a whole row pitch down from there. A row wholly inside one of them stands on drawn panel.
        float first = plate.Count > 0 ? plate[0].Y + (plate[0].Crop?.Height ?? 0f) - AuthoredGameOptionPitch : 0f;
        var held = new int[Math.Max(1, plate.Count)];
        bool banded = plate.Count > 0;
        float lowest = 0f;
        foreach (var row in shell.Rows)
        {
            // The plaques, the window's scroll arrows and the rows scrolled out of it stand on no band.
            if (row.Key is OriginalGameOptionsPage.AcceptKey or OriginalGameOptionsPage.CancelKey || !row.Visible
                || row.Key.StartsWith(OriginalGameOptionsPage.WindowKey + ":", StringComparison.Ordinal))
            {
                continue;
            }

            lowest = Math.Max(lowest, row.Y + row.Height);
            int band = rowPitch > 0f ? (int)Math.Floor((row.Y - first) / rowPitch) : -1;
            banded &= band >= 0 && band < held.Length
                && row.Y + row.Height <= first + ((band + 1) * rowPitch);
            held[Math.Clamp(band, 0, held.Length - 1)]++;
        }

        int shared = 0;
        foreach (int count in held)
        {
            shared += count > 1 ? 1 : 0;
        }

        ctx.Check(banded && shared == 0,
            $"every option row standing wholly inside a band of its own, none carrying two ({banded}, {shared} shared, bands from {first}: {string.Join(" ", shell.Rows.Select(r => $"{r.Key}@{r.Y}+{r.Height}"))})");
        ctx.Check(lowest > 0f && lowest <= accept,
            $"and no option row reaching past that line, which would take one press for two rows ({lowest} of {accept})");

        // A checkbox row's words beside its own box rather than over the band's top edge. They
        // stand on the box's centre line, in the dropdown rows' own column and left-aligned there.
        float column = -1f;
        foreach (var line in board.Lines)
        {
            column = line.Text == "Difficulty" ? line.X : column;
        }

        int aligned = 0;
        foreach (var row in shell.Rows)
        {
            if (row.Kind != OriginalRowKind.Radio || !row.Visible)
            {
                continue;
            }

            foreach (var line in board.Lines)
            {
                aligned += line.Text is "Auto Head Turn" or "Next Target"
                    && line.X == column && line.Justify == BoardJustify.Left
                    && Math.Abs(line.Y + (line.Size / 2f) - row.Y - (row.Height / 2f)) <= 1f ? 1 : 0;
            }
        }

        ctx.Check(column > 0f && aligned == 2,
            $"each checkbox row's title on its own box's centre line at the dropdown rows' column ({aligned} of 2 at {column})");
    }

    // A text widget's words, read off the layout so a check holds over the invented tree too.
    private static string Words(MenuLayout layout, string section, string key) =>
        layout.Screen(section)?.Widget(key)?.Text ?? string.Empty;

    private static float RowY(OriginalShell shell, string key)
    {
        foreach (var row in shell.Rows)
        {
            if (row.Key == key)
            {
                return row.Y;
            }
        }

        return -1f;
    }

    // Walks the focus onto a row by key, the shortest keyboard walk there over the shell's own step.
    private static void WalkTo(MenuHost host, ScriptedSeat seat, OriginalShell shell, string key) =>
        OriginalWalk.WalkTo(shell, key, frame => Press(host, seat, frame));

    private static OriginalShell? SwitchBackToOriginal(TestContext ctx, MenuHost host)
    {
        host.Deactivate();
        string? reason = host.Select("original");
        host.Show(MenuReturnDestination.TopLevel);
        var shell = (host.Active as OriginalPresentation)?.Shell;
        ctx.Check(host.Selected == PresentationId.Original && reason == null && shell != null,
            $"an Original request re-selects it and Show creates a fresh instance ({host.Selected})");
        ctx.Check(shell is { Screen: OriginalScreen.TopLevel, FocusedKey: OriginalShell.FreeFlightKey } && shell.LocalLobby.PickedChapter(OriginalScreen.FreeFlight) == null,
            $"standing on its top level with nothing picked and the door focused ({shell?.Screen}, {shell?.LocalLobby.PickedChapter(OriginalScreen.FreeFlight) ?? "none"}, {shell?.FocusedKey})");
        return shell;
    }

    // Original's VIDEO route over the install's decoded sections. The Preferences page's third door
    // opens the decoded page on its monitor dropdown. The rows under it take a size, a display mode
    // and a frame cap, and the checkbox flips the graphics word. CANCEL CHANGES drops them all
    // without an exit, and ACCEPT CHANGES on a second visit leaves as one apply exit carrying them.
    private static void OriginalVideoRoute(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell? shell, List<MenuExit> exits, MenuLayout layout)
    {
        if (shell == null)
        {
            return;
        }

        int before = exits.Count;
        WalkTo(host, seat, shell, "MM_B_PREFERENCES");
        Press(host, seat, Accept);
        WalkTo(host, seat, shell, OriginalOptionsScreen.VideoDoorKey);
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.Video && shell.FocusedKey == OriginalVideoPage.MonitorKey,
            $"VIDEO opens the decoded page on its Monitor row, the first of the authored rows it carries ({shell.Screen}, {shell.FocusedKey})");
        var board = shell.Compose();
        string tab = Words(layout, "Video", "VP_T_TITLE");
        ctx.CheckShipped(tab == "VIDEO", $"the VIDEO tab title is the install's ({tab})");
        int titles = 0;
        foreach (var line in board.Lines)
        {
            titles += line.Text == tab || line.Text is "Monitor" or "Resolution" or "Display Mode" or "V-Sync"
                or "Render Scale" or "Anti-aliasing" or "Shadow Quality" or "Enhanced Graphics" ? 1 : 0;
        }

        ctx.Check(tab.Length > 0 && titles == 9, $"drawing the section's own tab title over the eight row titles ({titles} of 9)");
        bool box = false;
        foreach (var plaque in board.Plaques)
        {
            box |= plaque.Art.Frames == 8;
        }

        ctx.Check(box, $"with the checkbox drawn from its eight-state strip ({board.Plaques.Count} plaques)");
        // The size row under the borderless default, which owns the size. It draws dead and the
        // walk cannot land on it, so the row the walk ends on is what the check names.
        WalkTo(host, seat, shell, OriginalVideoPage.ResolutionKey);
        ctx.Check(shell.Options.Video.ResolutionPinned && Row(shell, OriginalVideoPage.ResolutionKey) is { Enabled: false }
            && shell.FocusedKey != OriginalVideoPage.ResolutionKey && shell.Options.Video.ResolutionChoice == null,
            $"borderless leaves the size row dead, out of the walk's reach ({shell.FocusedKey}, {shell.Options.Video.ResolutionChoice ?? "unset"})");
        WalkTo(host, seat, shell, OriginalVideoPage.DisplayModeKey);
        // A list inside the window its own row authors is exactly as tall as its items: no arrows,
        // no thumb and nothing for the pointer to scroll, which is the shape the film shows.
        Press(host, seat, Accept);
        ctx.Check(shell.Rows.Count == DisplayWords.DisplayModes.Count
            && List(shell, OriginalVideoPage.DisplayModeKey) == null
            && Row(shell, OriginalVideoPage.DisplayModeKey + ":down") == null,
            $"the Display Mode list fits its authored window and draws no bar ({shell.Rows.Count} rows)");
        Press(host, seat, Back);
        Press(host, seat, Right);
        ctx.Check(shell.Options.Video.DisplayModeChoice == DisplayWords.Fullscreen,
            $"a sideways step on the focused row takes the display mode after the borderless default ({shell.Options.Video.DisplayModeChoice ?? "unset"})");
        // The row's own words are this machine's screen sizes, so what it steps to is read back off
        // the shell rather than named. The claim is that the step lands on a size the screen offers,
        // and not on the screen's own size the row opened at.
        WalkTo(host, seat, shell, OriginalVideoPage.ResolutionKey);
        Press(host, seat, Right);
        ctx.Check(shell.FocusedKey == OriginalVideoPage.ResolutionKey && shell.Options.Video.ResolutionChoice != null
            && shell.Options.Video.ResolutionChoice != ResolutionSetting.ScreenSizes().Fallback
            && shell.Options.Video.ResolutionWords.Contains(shell.Options.Video.ResolutionChoice),
            $"the size row is live again under fullscreen and steps to the next size this screen offers ({shell.Options.Video.ResolutionChoice ?? "unset"} of {shell.Options.Video.ResolutionWords.Count})");
        WalkTo(host, seat, shell, OriginalVideoPage.VSyncKey);
        Press(host, seat, Accept);
        OpenVideoListWindow(ctx, host, seat, shell);
        WalkTo(host, seat, shell, OriginalVideoPage.VSyncKey);
        Press(host, seat, Accept);
        Press(host, seat, Down);
        Press(host, seat, Down);
        Press(host, seat, Accept);
        ctx.Check(shell.Options.Video.VSyncChoice == "120" && shell.FocusedKey == OriginalVideoPage.VSyncKey,
            $"and picking two below the off default closes it on the 120 fps cap ({shell.Options.Video.VSyncChoice ?? "unset"}, {shell.FocusedKey})");
        // The shadow row under Original, whose world casts no sun shadow: dead and out of the walk.
        WalkTo(host, seat, shell, OriginalVideoPage.ShadowQualityKey);
        ctx.Check(Row(shell, OriginalVideoPage.ShadowQualityKey) is { Enabled: false }
            && shell.FocusedKey != OriginalVideoPage.ShadowQualityKey && shell.Options.Video.Choice(ShadowQualitySetting.Lookup) == null,
            $"the Shadow Quality row stands dead under Original, out of the walk's reach ({shell.FocusedKey}, {shell.Options.Video.Choice(ShadowQualitySetting.Lookup) ?? "unset"})");
        WalkTo(host, seat, shell, OriginalVideoPage.GraphicsKey);
        Press(host, seat, Accept);
        ctx.Check(shell.Options.Video.GraphicsChoice == GraphicsMode.EnhancedWord,
            $"Accept on the checkbox under it flips the graphics word ({shell.Options.Video.GraphicsChoice})");
        WalkTo(host, seat, shell, OriginalVideoPage.ShadowQualityKey);
        Press(host, seat, Left);
        ctx.Check(shell.FocusedKey == OriginalVideoPage.ShadowQualityKey && shell.Options.Video.Choice(ShadowQualitySetting.Lookup) == ShadowQualitySetting.High,
            $"which brings the Shadow Quality row to life, a step left of the Ultra default taking High ({shell.FocusedKey}, {shell.Options.Video.Choice(ShadowQualitySetting.Lookup) ?? "unset"})");
        WalkTo(host, seat, shell, OriginalVideoPage.CancelKey);
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.Options && shell.Options.Video.GraphicsChoice == GraphicsMode.Default
            && shell.Options.Video.VSyncChoice == null && shell.Options.Video.DisplayModeChoice == null && shell.Options.Video.ResolutionChoice == null
            && shell.Options.Video.MonitorChoice == null && shell.Options.Video.Choice(ShadowQualitySetting.Lookup) == null && exits.Count == before,
            $"CANCEL CHANGES lands back on Preferences with every edit dropped and no exit ({shell.Screen}, {shell.Options.Video.GraphicsChoice}, {shell.Options.Video.VSyncChoice ?? "unset"}, {shell.Options.Video.DisplayModeChoice ?? "unset"}, {shell.Options.Video.ResolutionChoice ?? "unset"}, {shell.Options.Video.MonitorChoice ?? "unset"})");

        WalkTo(host, seat, shell, OriginalOptionsScreen.VideoDoorKey);
        Press(host, seat, Accept);
        WalkTo(host, seat, shell, OriginalVideoPage.DisplayModeKey);
        Press(host, seat, Right);
        WalkTo(host, seat, shell, OriginalVideoPage.VSyncKey);
        Press(host, seat, Right);
        WalkTo(host, seat, shell, OriginalVideoPage.GraphicsKey);
        Press(host, seat, Accept);
        WalkTo(host, seat, shell, OriginalVideoPage.ShadowQualityKey);
        Press(host, seat, Right);
        // The shadow row is the dropdown window's foot. Down scrolls View Distance onto it, then both
        // Vegetation rows, Ground Cover and VR Mode, a stop where it is live. The install draws the two
        // checkboxes under the window, Enhanced Graphics at the column's foot, and Down from it wraps out.
        var expected = new List<string>
        {
            OriginalVideoPage.ShadowQualityKey, OriginalVideoPage.ViewDistanceKey, OriginalVideoPage.VegetationQualityKey,
            OriginalVideoPage.VegetationModelsKey, OriginalVideoPage.GroundCoverKey,
        };
        if (System.OperatingSystem.IsWindows())
        {
            expected.Add(OriginalVideoPage.VrModeKey);
        }

        expected.AddRange(new[] { OriginalVideoPage.WaterQualityKey, OriginalVideoPage.GraphicsKey, OriginalVideoPage.AcceptKey, OriginalVideoPage.CancelKey });
        var walk = new List<string> { shell.FocusedKey };
        while (walk.Count < expected.Count)
        {
            Press(host, seat, Down);
            walk.Add(shell.FocusedKey);
        }

        ctx.Check(walk.SequenceEqual(expected) && shell.Options.Video.Top == 5,
            $"Down walks the shadow row, scrolls View Distance, both Vegetation rows, Ground Cover and then VR Mode into the window, crosses both checkboxes, then out to ACCEPT and CANCEL ({string.Join(" -> ", walk)}, top {shell.Options.Video.Top})");
        WalkTo(host, seat, shell, OriginalVideoPage.AcceptKey);
        Press(host, seat, Accept);
        ctx.Check(exits.Count == before + 1 && exits[^1] is OptionsApplyExit
        {
            Graphics: GraphicsMode.EnhancedWord, VSync: "60", DisplayMode: DisplayWords.Fullscreen,
        } applied && applied.Words[ShadowQualitySetting.Lookup] == ShadowQualitySetting.Off,
            $"and ACCEPT CHANGES leaves through the host as one OptionsApplyExit carrying all four words ({exits.Count - before}, {exits[^1].GetType().Name})");
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

        ctx.Check(shell.Options.Video.OpenOption == OriginalVideoPage.VSyncKey
            && shell.Rows.Count == DisplayWords.VSyncChoices.Count + 2 && drawn == 4,
            $"Accept on the V-Sync row opens its five choices in the authored four-row window ({shell.Rows.Count} rows, {drawn} drawn)");
        var bar = List(shell, OriginalVideoPage.VSyncKey);
        var arrow = Row(shell, OriginalVideoPage.VSyncKey + ":down");
        float edge = (bar?.Window.X ?? 0f) + (bar?.Window.Width ?? 0f);
        ctx.Check(bar is { Window.Scrolls: true } && arrow != null
            && bar.Window.ThumbX + bar.Window.ThumbWidth <= edge && arrow.X + arrow.Width <= edge,
            $"with its thumb and its arrows inside the box's own right edge (thumb {bar?.Window.ThumbX ?? -1}, arrow {arrow?.X ?? -1}, edge {edge})");
        var size = ctx.Host.GetViewport().GetVisibleRect().Size;
        var fit = BoardFit.For(size.X, size.Y);
        WheelAndDrag(ctx, host, seat, shell, fit, OriginalVideoPage.VSyncKey, "the V-Sync page's open list");
        var offscreen = Row(shell, OriginalVideoPage.VSyncKey + ":4");
        ctx.Check(offscreen is { Visible: false },
            $"the word outside the window keeps its place for the walk, unseen ({offscreen?.Visible.ToString() ?? "missing"})");
        if (offscreen == null)
        {
            return;
        }

        Click(host, seat, Pointer(fit, offscreen.X + 2f, offscreen.Y + 2f, pressed: true, clicked: true));
        ctx.Check(shell.Options.Video.VSyncChoice == null && shell.Options.Video.OpenOption == null,
            $"and a press on it picks nothing, the pointer reaching no row it cannot see ({shell.Options.Video.VSyncChoice ?? "unset"})");
    }

    // Original's AUDIO route over the install's decoded sections: the Preferences page's second door
    // opens the decoded page on its Master slider, the page draws a thumb per row, a sideways step
    // moves a level and clamps at silence rather than wrapping to full, the open page states its mix
    // to the host every frame and names the level a frame moved only when one did, CANCEL CHANGES
    // drops the edits without an exit and ends the preview, and ACCEPT CHANGES on a second visit
    // leaves as one apply exit.
    private static void OriginalAudioRoute(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell? shell, List<MenuExit> exits, RecordingAudio audio,
        MenuLayout layout)
    {
        if (shell == null)
        {
            return;
        }

        int before = exits.Count;
        WalkTo(host, seat, shell, "MM_B_PREFERENCES");
        Press(host, seat, Accept);
        WalkTo(host, seat, shell, OriginalOptionsScreen.AudioDoorKey);
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.Audio && shell.FocusedKey == OriginalAudioPage.MasterKey,
            $"AUDIO opens the decoded page on its Master row, the first of the authored rows it carries ({shell.Screen}, {shell.FocusedKey})");
        var board = shell.Compose();
        string tab = Words(layout, "Audio", "AP_T_TITLE");
        // Every slider row draws the thumb the section's music slider authors, Master included.
        var slider = layout.Screen("Audio")?.Widget("AP_S_MVOLUME")?.Art;
        string thumb = slider is { Count: > 1 } ? slider[1] : string.Empty;
        ctx.CheckShipped(tab == "AUDIO" && thumb == "PF_B_Slider.png",
            $"the AUDIO tab title and the slider thumb are the install's ({tab}, {thumb})");
        int titles = 0;
        int thumbs = 0;
        foreach (var line in board.Lines)
        {
            titles += line.Text == tab || line.Text is "Master" or "Music Volume" or "Effects Volume" or "Voice Volume" ? 1 : 0;
        }

        foreach (var picture in board.Pictures)
        {
            thumbs += thumb.Length > 0 && picture.Art.Name == thumb ? 1 : 0;
        }

        ctx.Check(tab.Length > 0 && titles == 5, $"drawing the section's own tab title over the four row titles ({titles} of 5)");
        ctx.Check(thumbs == 4, $"with the authored thumb drawn once per row ({thumbs} of 4)");
        // The store is a scratch one under --run-tests, so the page opens on the shipped mix: every
        // level never set, each row standing at its own default.
        ctx.Check(shell.Options.Audio.MasterChoice == null && shell.Options.Audio.MusicChoice == null
            && shell.Options.Audio.EffectsChoice == null && shell.Options.Audio.VoiceChoice == null,
            $"on a mix nothing has saved, every level reading as never set ({shell.Options.Audio.MasterChoice?.ToString() ?? "unset"})");
        audio.Mixes.Clear();
        Press(host, seat, Right);
        ctx.Check(shell.Options.Audio.MasterChoice == null,
            $"a step off the top of the Master row moves nothing, so it writes nothing ({shell.Options.Audio.MasterChoice?.ToString() ?? "unset"})");
        // A step at an end moves no level, so the frame states the mix and names no moved level:
        // this is what keeps a host from sounding a category once per pointer frame of a drag.
        ctx.Check(audio.Mixes.Count == 1 && audio.Mixes[0].Moved == MenuMixLevel.None
            && audio.Mixes[0].Levels == new AudioLevels(
                AudioMix.DefaultMaster, AudioMix.DefaultMusic, AudioMix.DefaultEffects, AudioMix.DefaultVoice),
            $"the open page states the shipped mix to the host and names no moved level on a frame that moved none ({audio.Mixes.Count}, {(audio.Mixes.Count > 0 ? audio.Mixes[0].Moved : MenuMixLevel.None)})");
        WalkTo(host, seat, shell, OriginalAudioPage.MusicKey);
        audio.Mixes.Clear();
        Press(host, seat, Left);
        ctx.Check(shell.Options.Audio.MusicChoice == AudioMix.DefaultMusic - SliderControl.KeyStep,
            $"a sideways step on the focused row moves that level by the control's own step ({shell.Options.Audio.MusicChoice?.ToString() ?? "unset"})");
        ctx.Check(audio.Mixes.Count == 1 && audio.Mixes[0].Moved == MenuMixLevel.Music
            && audio.Mixes[0].Levels.Music == AudioMix.DefaultMusic - SliderControl.KeyStep,
            $"and names Music as the level that moved, carrying the level it moved to ({(audio.Mixes.Count > 0 ? audio.Mixes[0].Moved : MenuMixLevel.None)})");
        int endsBefore = audio.MixEnds;
        WalkTo(host, seat, shell, OriginalAudioPage.CancelKey);
        audio.Mixes.Clear();
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.Options && shell.Options.Audio.MusicChoice == null && exits.Count == before,
            $"CANCEL CHANGES lands back on Preferences with the edit dropped and no exit ({shell.Screen}, {shell.Options.Audio.MusicChoice?.ToString() ?? "unset"})");
        // Off the page there is no mix to state, and the host is told to put back the one the page
        // opened over, which is the half a player notices when it is wrong.
        ctx.Check(audio.Mixes.Count == 0 && audio.MixEnds > endsBefore,
            $"ending the preview and stating no mix off the page ({audio.Mixes.Count}, ends={audio.MixEnds - endsBefore})");

        WalkTo(host, seat, shell, OriginalOptionsScreen.AudioDoorKey);
        Press(host, seat, Accept);
        WalkTo(host, seat, shell, OriginalAudioPage.VoiceKey);
        for (int step = 0; step < 12; step++)
        {
            Press(host, seat, Left);
        }

        ctx.Check(shell.Options.Audio.VoiceChoice == AudioMix.MinLevel,
            $"and stepping the Voice row past its floor clamps at silence rather than wrapping to full ({shell.Options.Audio.VoiceChoice?.ToString() ?? "unset"})");
        WalkTo(host, seat, shell, OriginalAudioPage.AcceptKey);
        Press(host, seat, Accept);
        ctx.Check(exits.Count == before + 1 && exits[^1] is OptionsApplyExit { AudioVoice: AudioMix.MinLevel, AudioMaster: null },
            $"and ACCEPT CHANGES leaves through the host as one OptionsApplyExit carrying the levels ({exits.Count - before}, {exits[^1].GetType().Name})");
        ctx.Check(!host.Shown, $"with the presentation hidden for the launcher to act (shown={host.Shown})");
    }

    // Recovery: an unknown override resolves to Original with a reason. An Original whose layout
    // is not there stays selected, with the availability reason and the request kept.
    private static void Recovery(TestContext ctx, MenuHost host, PresentationRegistry registry, RecordingAudio audio)
    {
        string? reason = host.Select("no-such-presentation");
        ctx.Check(host.Selected == PresentationId.Original && reason != null && host.Requested == new PresentationId("no-such-presentation"),
            $"an unknown override resolves to Original with a reason and keeps the request ({reason})");

        var missing = new MenuHost(registry, audio, _ => { });
        missing.Availability = id => id == PresentationId.Original ? OriginalAvailability.Load(EmptyDataRoot(ctx), out var why) == null ? why : null : null;
        reason = missing.Select(null);
        ctx.Check(missing.Selected == PresentationId.Original && missing.Requested == PresentationId.Original,
            $"a flagless run over a data root with no decoded layout keeps Original selected and requested ({missing.Selected}, {missing.Requested})");
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
