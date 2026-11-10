using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Hangar;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using CSVM.UI.Screens;

namespace CSVM.Testing;

/// <summary>
/// Instant Action through Original's decoded screen and a real <see cref="MenuHost"/> over the
/// install's layout, driving the shared feature to four representative launches.
/// </summary>
internal static class MenuInstantActionSuites
{
    // A course a player titled at length, as the course lab lets them.
    private const string LongCourse = "long_title_slalom";
    private const string LongTitle = "The Grand Hollywoodland Studio Lot Slalom and Harbour Climb Run";

    // The shipped C2 course that refuses one plane, its title, and that plane's node.
    private const string RefusingCourse = "Hollywood_Track_1";
    private const string RefusingTitle = "Studio Lot Dash";
    private const string RefusedPlane = "player_balmoral";

    private static readonly MenuCommands Accept = new() { Accept = true };
    private static readonly MenuCommands Back = new() { Back = true };
    private static readonly MenuCommands Down = new() { MoveY = 1 };
    private static readonly MenuCommands Up = new() { MoveY = -1 };
    private static readonly MenuCommands Left = new() { MoveX = -1 };
    private static readonly MenuCommands Right = new() { MoveX = 1 };

    [Suite("menu-original-instant-action",
        "Original Instant Action through the presentation boundary over the install's decoded "
        + "layout: the top level's Instant Action row is live and a click opens the decoded screen "
        + "with the environment's def loaded, its rows are the layout's contents window, dropdowns "
        + "and buttons, the contents thumb stands as long as the share of the presets its window "
        + "shows, the ace duel's enemy boxes and the wingman plane at zero wingmen standing "
        + "blank and inert beside both paging buttons, keyboard frames cross to a dropdown and step and pick its value, the "
        + "remake-only lives box stands on the clear line over the mission dropdown reading Unlimited at zero, takes the walk, "
        + "a sideways step and a picked row onto the shared feature's own lives, stays live under the ace duel and keeps its "
        + "count over a contents row's preset, four "
        + "representative presets (an ace duel, a squadron, a stunt run and a zeppelin run) each "
        + "leave through Fly Mission as one LaunchExit whose def derives the matching session spec, "
        + "the setup surviving each return to the top level, a build saved to the suite's scratch plane store "
        + "is offered in the Pilot Plane list after the stock rows under its own name, flies its "
        + "airframe's stock node with the def riding the seat, survives a return, and leaves the "
        + "list with its file (the wingman list stays stock), Weapon Loadout with the radio on "
        + "Wingman opens the decoded ammo chrome over the wingmen's shared fit whose CANCEL restores "
        + "and ACCEPT keeps a stepped pick, and Build Custom Plane opens the wallet-free hangar whose "
        + "Back and Purchase Now both return to the screen, the purchase's plane in the Pilot Plane list, "
        + "and a second pilot joined on the screen is named by the seat strip and walked through its own "
        + "aircraft screen by Fly Mission before the launch carries both seats, the remake-only race time box hidden "
        + "on a solo stunt run showing once that pilot joins on the lives box's clear line, taking the walk "
        + "between the pilot plane and the mission type and a sideways step, hiding under another mission type, "
        + "and its pick riding the def into the session spec. The lives box, which a race does not spend, "
        + "hides with its title as the pilot joins and returns with its count under another type, and a "
        + "cursor left on the lives box lifts to the live box above it. The race also blanks the wingman "
        + "and enemy boxes. A bot row on the join board shows the remake-only race toggle, off, titled Bots: beside "
        + "the race time box on its line and ending where the mission dropdown ends, reached by Up from the mission "
        + "type and by Down from the race time box, "
        + "flipped in place by a sideways press and left by Down for the mission type, and its removal hides it.")]
    internal static void MenuOriginalInstantAction(TestContext ctx)
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
        var seat = new ScriptedSeat();
        var registry = new PresentationRegistry();
        var store = MenuSuiteHost.ScratchPlanes(ctx, "menu-original-instant-action");
        registry.Register(PresentationId.Original, () => new OriginalPresentation(
            ctx.Host, ctx.DataRoot, layout, string.Empty, new MenuInput { Keyboard = true }, data: ctx.Data)
        {
            Planes = store,
        });
        var host = new MenuHost(registry, new MenuSuiteHost.SilentMenuAudio(), exits.Add);
        MenuSuiteHost.AddFeatures(host, ctx.DataRoot, ctx.Data);
        host.AddSeat(seat);
        string scratch = ScratchName();
        string built = ScratchName();
        ctx.Check(store.List().Count == 0, $"the suite's scratch store starts empty ({store.List().Count})");
        try
        {
            host.Select("original");
            host.Show(MenuReturnDestination.TopLevel);
            var shell = (host.Active as OriginalPresentation)?.Shell;
            ctx.Check(shell != null, $"Show activates Original ({host.Active?.Id})");
            if (shell == null)
            {
                return;
            }

            var size = ctx.Host.GetViewport().GetVisibleRect().Size;
            var fit = BoardFit.For(size.X, size.Y);
            var ia = host.Features.Get<InstantActionFeature>();
            var section = layout.Screen("InstantAction");
            OriginalScreenOpens(ctx, host, seat, shell, fit, ia, section);
            OriginalKeyboard(ctx, host, seat, shell, ia, section);
            OriginalLives(ctx, host, seat, shell, fit, ia, section);
            OriginalLaunches(ctx, host, seat, shell, fit, ia, exits);
            OriginalCustomPilot(ctx, host, seat, shell, fit, ia, exits, store, scratch);
            OriginalLoadout(ctx, host, seat, shell, fit, ia, layout.Screen("OrdinanceLayout"));
            OriginalBuild(ctx, host, seat, shell, fit, host.Features.Get<HangarFeature>(), store, built);
            OriginalTwoSeats(ctx, host, seat, shell, fit, host.Features.Get<PlayerSetupFeature>(), ia, exits, section);
        }
        finally
        {
            host.Deactivate();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            MenuSuiteHost.DropScratchPlanes(ctx, "menu-original-instant-action");
        }
    }

    [Suite("menu-instant-action-course",
        "Instant Action's Course option through Original's screen over the install's layout, the shipped "
        + "courses and a scratch copy of the sample titled far wider than the Course box: that title stays on one line "
        + "inside its box both as an open list row and picked in the closed box; "
        + "stunt flying on the airfield (C1) opens a Course list of Danger Zones then "
        + "sample_out_and_back by its title, a click picks the sample and blanks the wingman, enemy and lives boxes, Fly "
        + "Mission leaves as a LaunchExit naming the course by file name with no def whose session spec matches "
        + "--stunt --course=C1/sample_out_and_back on the course, chapter, mission, mode, pilots, plane, def and "
        + "bots, the load and pause boards' heading is the title, a second pilot and a racing bot row keep the box on a "
        + "line of its own with the race toggle beside the race time box, and leave as that course raced by both seats and "
        + "the bot whose spec matches --stunt --course= --players=2 --vs-bot in the picked window, and a menu-driven "
        + "session over a course no library holds fails its build with the reason for the menu rather than "
        + "quitting, the return landing back on the Instant Action screen with that reason in its warning box")]
    internal static void MenuInstantActionCourse(TestContext ctx)
    {
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        MenuSuiteHost.RequireLayout(ctx);
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        const string suite = "menu-instant-action-course";
        var exits = new List<MenuExit>();
        var seat = new ScriptedSeat();
        var registry = new PresentationRegistry();
        var store = MenuSuiteHost.ScratchPlanes(ctx, suite);
        registry.Register(PresentationId.Original, () => new OriginalPresentation(
            ctx.Host, ctx.DataRoot, layout, string.Empty, new MenuInput { Keyboard = true }, data: ctx.Data)
        {
            Planes = store,
        });
        var host = new MenuHost(registry, new MenuSuiteHost.SilentMenuAudio(), exits.Add);
        MenuSuiteHost.AddFeatures(host, ctx.DataRoot, ctx.Data, courses: LongTitledLibrary(ctx, suite));
        host.AddSeat(seat);
        try
        {
            host.Select("original");
            host.Show(MenuReturnDestination.TopLevel);
            var original = host.Active as OriginalPresentation;
            var shell = original?.Shell;
            var size = ctx.Host.GetViewport().GetVisibleRect().Size;
            var fit = BoardFit.For(size.X, size.Y);
            if (shell == null || !EnterInstantAction(ctx, host, seat, shell, fit))
            {
                ctx.Check(false, $"Original stands up on its Instant Action screen");
                return;
            }

            var ia = host.Features.Get<InstantActionFeature>();
            CourseLongTitle(ctx, host, seat, shell, fit, ia);
            if (CoursePicked(ctx, host, seat, shell, fit, ia, exits) is not { } exit)
            {
                return;
            }

            CourseRaced(ctx, host, seat, shell, fit, host.Features.Get<PlayerSetupFeature>(), ia, exits);
            CourseRefused(ctx, host, shell, original!, exit);
        }
        finally
        {
            host.Deactivate();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            MenuSuiteHost.DropScratchPlanes(ctx, suite);
        }
    }

    [Suite("menu-instant-action-course-planes",
        "Instant Action over the install's layout, the shipped courses and a saved Balmoral build, with seat 0 on a stock Balmoral "
        + "and two racing bot rows on a Balmoral and an Avenger: off a course every list admits the Balmoral; picking "
        + "C2/Hollywood_Track_1, which refuses it, in the Course box puts the Balmoral bot back on Random and keeps the Avenger "
        + "one; the join board reached through the top level's door, which a local Dogfight shares, offers the Balmoral "
        + "ungreyed, while the board opened under Instant Action's rule greys it in its Edit Bot plane list, ignores a click "
        + "on it and a step from the Avenger lands past it; seat 0 keeps its pick, the Pilot Plane list greys the stock Balmoral and the build, FLY MISSION "
        + "answers with the course's reason and no launch, and a step from the Avenger lands past it; a second seat left "
        + "confirmed on a Balmoral is walked again, its list greys the row, an Accept on it raises the reason, and the walk "
        + "leaves as one launch of the course with no Balmoral anywhere on it, which the course admits")]
    internal static void MenuInstantActionCoursePlanes(TestContext ctx)
    {
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        MenuSuiteHost.RequireLayout(ctx);
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        const string suite = "menu-instant-action-course-planes";
        var exits = new List<MenuExit>();
        var seat = new ScriptedSeat();
        var registry = new PresentationRegistry();
        // Three builds on airframes 0, 1 and 2, the last a Balmoral.
        var store = MenuSuiteHost.ScratchPlanes(ctx, suite, customs: 3);
        registry.Register(PresentationId.Original, () => new OriginalPresentation(
            ctx.Host, ctx.DataRoot, layout, string.Empty, new MenuInput { Keyboard = true }, data: ctx.Data)
        {
            Planes = store,
        });
        var host = new MenuHost(registry, new MenuSuiteHost.SilentMenuAudio(), exits.Add);
        MenuSuiteHost.AddFeatures(host, ctx.DataRoot, ctx.Data);
        host.AddSeat(seat);
        try
        {
            host.Select("original");
            host.Show(MenuReturnDestination.TopLevel);
            var shell = (host.Active as OriginalPresentation)?.Shell;
            var size = ctx.Host.GetViewport().GetVisibleRect().Size;
            var fit = BoardFit.For(size.X, size.Y);
            if (shell == null || !EnterInstantAction(ctx, host, seat, shell, fit))
            {
                ctx.Check(false, $"Original stands up on its Instant Action screen");
                return;
            }

            var ia = host.Features.Get<InstantActionFeature>();
            var setup = host.Features.Get<PlayerSetupFeature>();
            if (CoursePlanesPicked(ctx, host, seat, shell, fit, ia, setup) is { } bots)
            {
                CoursePlanesOffTheDoor(ctx, host, seat, shell, fit, bots.Avenger);
                shell.JoinBoard.Open(ia.AdmitsBot);
                CoursePlanesOnTheBoard(ctx, host, seat, shell, fit, setup, bots.Avenger);
                host.Show(MenuReturnDestination.InstantAction);
                CoursePlanesForSeats(ctx, host, seat, shell, fit, ia, setup, exits);
            }
        }
        finally
        {
            host.Deactivate();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            MenuSuiteHost.DropScratchPlanes(ctx, suite);
        }
    }

    // Seat 0 on a stock Balmoral and two racing bots, then C2/Hollywood_Track_1 picked through the
    // Course box. Answers the two bots' ids, or null where the setup cannot stand.
    private static (int Balmoral, int Avenger)? CoursePlanesPicked(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, InstantActionFeature ia, PlayerSetupFeature setup)
    {
        int balmoral = StockAirframes.IdOf(RefusedPlane) ?? -1;
        int avenger = StockAirframes.IdOf("player_avenger") ?? -1;
        ctx.Check(setup.AddBot() && setup.AddBot(), $"two bot rows join the board ({setup.Bots.Count})");
        if (setup.Bots.Count < 2)
        {
            return null;
        }

        var (onBalmoral, onAvenger) = (setup.Bots.Rows[0].Id, setup.Bots.Rows[1].Id);
        setup.Bots.SetAirframe(onBalmoral, balmoral);
        setup.Bots.SetAirframe(onAvenger, avenger);
        ia.SelectPlayerPlane(InstantActionFeature.Airframes.ToList().FindIndex(a => a.Node == RefusedPlane));
        ia.SelectEnvironment(InstantActionFeature.Environments.ToList().FindIndex(e => e.Code == "C2"));
        ia.ConfirmEnvironment();
        ia.SelectMissionType(ia.MissionTypes.ToList().FindIndex(m => m.Key == InstantActionFeature.StuntKey));
        ia.RaceBots = true;
        var (stock, build) = (PilotRowOfNode(shell, custom: false), PilotRowOfNode(shell, custom: true));
        ctx.Check(shell.InstantAction.OpenDropdownOn(OriginalInstantActionScreen.PlayerPlaneKey)
                && stock >= 0 && build >= 0 && PilotItem(shell, stock) is { Enabled: true } && PilotItem(shell, build) is { Enabled: true }
                && ia.LaunchCourse == null && ia.AdmitsBot(balmoral),
            $"ABLE-TO-FAIL CONTROL: on C2's Danger Zones the Pilot Plane list offers the stock Balmoral and the build, and a bot may fly one (rows {stock}, {build}, course {ia.LaunchCourse ?? "none"})");
        shell.InstantAction.CloseDropdown();

        ctx.Check(shell.InstantAction.OpenDropdownOn(OriginalInstantActionScreen.CourseKey), $"the Course list opens ({ia.Environment.Code}, {ia.MissionType.Key})");
        var pick = shell.Rows.FirstOrDefault(r => r.Label == RefusingTitle);
        if (pick == null)
        {
            ctx.Check(false, $"C2's Course list carries {RefusingTitle} ({string.Join(", ", shell.Rows.Select(r => r.Label))})");
            return null;
        }

        Click(host, seat, Pointer(fit, pick.X + 5f, pick.Y + 5f, pressed: true, clicked: true));
        var (reset, kept) = (setup.Bots.ById(onBalmoral), setup.Bots.ById(onAvenger));
        ctx.Check(ia.LaunchCourse == RefusingCourse && reset is { RandomPlane: true } && kept?.Airframe == avenger,
            $"picking {RefusingCourse} puts the Balmoral bot back on Random and keeps the Avenger one ({ia.LaunchCourse}, {reset?.Airframe}, {kept?.Airframe})");
        ctx.Check(ia.PlayerPlane.Node == RefusedPlane, $"seat 0 keeps its Balmoral pick, as a lobby pilot does ({ia.PlayerPlane.Node})");
        return (onBalmoral, onAvenger);
    }

    // ABLE-TO-FAIL CONTROL for the greying below. The top level's door serves a local Dogfight too,
    // which flies no course, so the Balmoral stands there whatever Instant Action is left on.
    private static void CoursePlanesOffTheDoor(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, int onAvenger)
    {
        host.Show(MenuReturnDestination.TopLevel);
        var door = Row(shell, OriginalShell.JoinBoardKey);
        if (door != null)
        {
            Click(host, seat, Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
        }

        shell.JoinBoard.ShowBot(onAvenger);
        var box = Row(shell, OriginalJoinBoard.BotPlaneKey);
        if (box != null)
        {
            Click(host, seat, Pointer(fit, box.X + 5f, box.Y + 5f, pressed: true, clicked: true));
        }

        var item = Row(shell, BotPlaneItem(StockAirframes.IdOf(RefusedPlane) ?? -1));
        ctx.Check(shell.Screen == OriginalScreen.JoinBoard && item is { Enabled: true },
            $"ABLE-TO-FAIL CONTROL: through the top level's door, with Instant Action left on {RefusingCourse}, the Balmoral stands ungreyed ({shell.Screen}, {item?.Enabled})");
        shell.JoinBoard.CloseDropdown();
    }

    private static string BotPlaneItem(int airframe) =>
        OriginalJoinBoard.BotPlaneKey + ":" + (airframe + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    // The join board opened on Instant Action's behalf: the Balmoral greyed and passed over.
    private static void CoursePlanesOnTheBoard(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, PlayerSetupFeature setup, int onAvenger)
    {
        int balmoral = StockAirframes.IdOf(RefusedPlane) ?? -1;
        int avenger = StockAirframes.IdOf("player_avenger") ?? -1;
        shell.JoinBoard.ShowBot(onAvenger);
        var box = Row(shell, OriginalJoinBoard.BotPlaneKey);
        if (box == null)
        {
            ctx.Check(false, $"Edit Bot stands on the Avenger bot with its plane box");
            return;
        }

        Click(host, seat, Pointer(fit, box.X + 5f, box.Y + 5f, pressed: true, clicked: true));
        var refused = Row(shell, BotPlaneItem(balmoral));
        var admitted = Row(shell, BotPlaneItem(avenger));
        var lines = shell.Compose().Overlays.SelectMany(p => p.Lines).ToList();
        var (grey, black) = (lines.FirstOrDefault(l => l.Text == refused?.Label), lines.FirstOrDefault(l => l.Text == admitted?.Label));
        ctx.Check(shell.JoinBoard.OpenDropdown == OriginalJoinBoard.BotPlaneKey && refused is { Enabled: false } && admitted is { Enabled: true }
                && Row(shell, OriginalJoinBoard.BotPlaneKey + ":0") is { Enabled: true } && grey != null && black != null && grey.Colour != black.Colour,
            $"the open plane list greys the Balmoral and keeps Random and the Avenger ({refused?.Label} {refused?.Enabled}, {admitted?.Label} {admitted?.Enabled})");
        if (refused != null)
        {
            Click(host, seat, Pointer(fit, refused.X + 5f, refused.Y + 5f, pressed: true, clicked: true));
        }

        ctx.Check(setup.Bots.ById(onAvenger)?.Airframe == avenger, $"a click on the greyed Balmoral leaves the bot on its Avenger ({setup.Bots.ById(onAvenger)?.Airframe})");
        shell.JoinBoard.CloseDropdown();
        OriginalWalk.WalkTo(shell, OriginalJoinBoard.BotPlaneKey, frame => Press(host, seat, frame));
        Press(host, seat, Right);
        int stepped = setup.Bots.ById(onAvenger)?.Airframe ?? -1;
        ctx.Check(stepped == balmoral + 1 && stepped != balmoral,
            $"a step right from the Avenger passes the Balmoral to the next airframe ({stepped})");
        Press(host, seat, Left);
        ctx.Check(setup.Bots.ById(onAvenger)?.Airframe == avenger, $"and a step back passes it again to the Avenger ({setup.Bots.ById(onAvenger)?.Airframe})");
        shell.JoinBoard.ShowBot(-1);
    }

    // Seat 0's Pilot Plane list and FLY MISSION, then a second seat's walk, under the course.
    private static void CoursePlanesForSeats(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, InstantActionFeature ia, PlayerSetupFeature setup,
        List<MenuExit> exits)
    {
        var (stock, build) = (PilotRowOfNode(shell, custom: false), PilotRowOfNode(shell, custom: true));
        int fury = shell.InstantAction.PilotRoster.ToList().FindIndex(r => !r.IsCustom && r.Node == "player_fury");
        ctx.Check(ia.LaunchCourse == RefusingCourse && shell.InstantAction.OpenDropdownOn(OriginalInstantActionScreen.PlayerPlaneKey)
                && PilotItem(shell, stock) is { Enabled: false } && PilotItem(shell, build) is { Enabled: false } && PilotItem(shell, fury) is { Enabled: true },
            $"the Pilot Plane list greys the stock Balmoral and the Balmoral build and keeps the Fury ({PilotItem(shell, stock)?.Label}, {PilotItem(shell, build)?.Label})");
        shell.InstantAction.CloseDropdown();

        var fly = Row(shell, OriginalInstantActionScreen.FlyMissionKey)!;
        int before = exits.Count;
        Click(host, seat, Pointer(fit, fly.X + 5f, fly.Y + 5f, pressed: true, clicked: true));
        string reason = shell.Dialog?.Message ?? "";
        ctx.Check(exits.Count == before && shell.Screen == OriginalScreen.InstantAction
                && reason.Contains($"C2/{RefusingCourse}", StringComparison.Ordinal) && reason.Contains($"not {RefusedPlane}", StringComparison.Ordinal),
            $"FLY MISSION on the Balmoral launches nothing and answers with the course's reason ({exits.Count - before}, '{reason}')");
        Press(host, seat, Accept);
        ctx.Check(shell.Dialog == null, $"OK answers the box");

        ia.SelectPlayerPlane(InstantActionFeature.Airframes.ToList().FindIndex(a => a.Node == "player_avenger"));
        OriginalWalk.WalkTo(shell, OriginalInstantActionScreen.PlayerPlaneKey, frame => Press(host, seat, frame));
        Press(host, seat, Right);
        ctx.Check(ia.PlayerPlane.Node == "player_bhawk", $"a step right from the Avenger passes the Balmoral ({ia.PlayerPlane.Node})");

        // A second seat left confirmed on a Balmoral, as a cancelled walk leaves one.
        var guest = new ScriptedSeat();
        var joined = setup.Join(guest);
        setup.SetRoster(shell.InstantAction.PilotRoster);
        ctx.Check(joined != null && setup.Browse(joined, stock) && setup.Select(joined) && setup.Confirm(joined),
            $"a second seat joins confirmed on the stock Balmoral ({joined?.Cursor})");
        if (joined == null)
        {
            return;
        }

        fly = Row(shell, OriginalInstantActionScreen.FlyMissionKey)!;
        Click(host, seat, Pointer(fit, fly.X + 5f, fly.Y + 5f, pressed: true, clicked: true));
        string entry = OriginalWidgets.EntryKeyPrefix;
        ctx.Check(shell.Screen == OriginalScreen.SeatPlane && shell.PickingSeat == 1 && !joined.Confirmed
                && Row(shell, entry + stock.ToString(System.Globalization.CultureInfo.InvariantCulture)) is { Enabled: false }
                && Row(shell, entry + fury.ToString(System.Globalization.CultureInfo.InvariantCulture)) is { Enabled: true },
            $"FLY MISSION walks the second seat again, its list greying the Balmoral ({shell.Screen}, seat {shell.PickingSeat}, confirmed {joined.Confirmed})");
        Press(host, guest, Accept);
        reason = shell.Dialog?.Message ?? "";
        ctx.Check(shell.Screen == OriginalScreen.SeatPlane && !joined.Locked && reason.Contains($"not {RefusedPlane}", StringComparison.Ordinal),
            $"an Accept on the greyed Balmoral selects nothing and raises the reason ('{reason}')");
        Press(host, guest, Accept);
        Press(host, guest, Down);
        Press(host, guest, Accept);
        Press(host, guest, Accept);
        if (exits.Count != before + 1 || exits[^1] is not LaunchExit exit)
        {
            ctx.Check(false, $"the walk ends as one LaunchExit ({exits.Count - before}, {shell.Screen}, '{shell.Dialog?.Message}')");
            return;
        }

        var planes = exit.Seats.Select(s => s.PlaneNode).ToList();
        var spec = SessionSpec.FromMenu(SessionSpec.Parse(Array.Empty<string>()), exit.Chapter, planes, exit.Mode,
            exit.InstantAction, missionType: exit.Match?.MissionType ?? DogfightMissionType.Deathmatch, bots: exit.Bots, course: exit.Course);
        var course = new Flight.Modes.CourseLibrary(Flight.Modes.CourseLibrary.ShippedRoot, null).Load("C2", RefusingCourse, out string error);
        string? refusal = course == null ? error : Launch.SeatFields.CoursePlaneRefusal(spec, course);
        ctx.Check(exit is { Chapter: "C2", Course: RefusingCourse, Seats.Count: 2, Bots.Count: 2 } && !planes.Contains(RefusedPlane)
                && exit.Bots.All(b => b.Plane != RefusedPlane) && refusal == null,
            $"the walk leaves as one launch of the course, no seat or bot on a Balmoral, which the course admits ({string.Join(", ", planes)}; {string.Join(", ", exit.Bots?.Select(b => b.Plane ?? "random") ?? Array.Empty<string>())}; {refusal ?? "admitted"})");
        setup.Unjoin(joined);
    }

    // The Pilot Plane list's row index for the Balmoral, the stock row or the saved build.
    private static int PilotRowOfNode(OriginalShell shell, bool custom) =>
        shell.InstantAction.PilotRoster.ToList().FindIndex(r => r.IsCustom == custom && r.Node == RefusedPlane);

    // The open Pilot Plane list's item row at roster index <paramref name="row"/>.
    private static OriginalRow? PilotItem(OriginalShell shell, int row) =>
        Row(shell, OriginalInstantActionScreen.PlayerPlaneKey + ":" + row.ToString(System.Globalization.CultureInfo.InvariantCulture));

    // The shipped courses plus, in the suite's scratch game folder, the sample under a title far wider than the Course box.
    private static Flight.Modes.CourseLibrary LongTitledLibrary(TestContext ctx, string suite)
    {
        string folder = System.IO.Path.Combine(ctx.ScratchDir, suite, Flight.Modes.CourseLibrary.GameFolderName);
        if (System.IO.Directory.Exists(folder))
        {
            System.IO.Directory.Delete(folder, recursive: true);
        }

        var library = new Flight.Modes.CourseLibrary(Flight.Modes.CourseLibrary.ShippedRoot, folder);
        if (library.Load("C1", "sample_out_and_back", out string error) is { } sample)
        {
            library.Save(sample with { Name = LongCourse, Title = LongTitle }, Flight.Modes.CourseSource.GameFolder);
        }
        else
        {
            ctx.Check(false, $"the shipped sample loads to copy under a long title ({error})");
        }

        return library;
    }

    // A title far wider than the Course box, in the open list and then closed in the box. Each stays
    // on one line inside its box, clipped and scrolling as the lobby's long names do.
    private static void CourseLongTitle(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, InstantActionFeature ia)
    {
        ctx.Check(shell.InstantAction.PoseCourse(), $"stunt flying opens the Course list ({ia.MissionType.Key})");
        var item = shell.Rows.FirstOrDefault(r => r.Label == LongTitle);
        StaysInBox(ctx, "the open list's row", shell.Compose().Overlays.SelectMany(p => p.Lines).FirstOrDefault(l => l.Text == LongTitle), item);
        if (item == null)
        {
            return;
        }

        Click(host, seat, Pointer(fit, item.X + 5f, item.Y + 5f, pressed: true, clicked: true));
        var box = Row(shell, OriginalInstantActionScreen.CourseKey);
        ctx.Check(ia.Course == LongCourse && box?.Label == LongTitle, $"a click picks the long-titled course ({ia.Course})");
        StaysInBox(ctx, "the closed Course box", shell.Compose().Lines.FirstOrDefault(l => l.Text == LongTitle), box);
        // Back to the Danger Zones, so the steps after start where a fresh screen does.
        ia.SelectCourse(0);
    }

    // The title drawn whole is wider than its box, so the check can fail. A plain line wraps onto the
    // rows below; a marquee line stays on one, clipped to its box.
    private static void StaysInBox(TestContext ctx, string what, BoardLine? line, OriginalRow? box)
    {
        ctx.Check(line != null && box != null, $"{what} draws the long title");
        if (line == null || box == null)
        {
            return;
        }

        // At the authored page size: the suite's own viewport is no window a pilot sizes.
        var fit = BoardFit.For(BoardFit.AuthoredWidth, BoardFit.AuthoredHeight);
        var view = new ComposedBoardView();
        try
        {
            var measure = ComposedBoardView.MeasureBox(fit, view.GetThemeDefaultFont(), line.Size);
            var one = measure(line.Text, 100000f);
            var wrapped = measure(line.Text, line.Width);
            ctx.Check(one.X > line.Width,
                $"ABLE-TO-FAIL CONTROL: drawn whole, the title is wider than {what}'s text ({one.X:0.0} against {line.Width:0})");
            float right = line.X + (line.Marquee ? line.Width : wrapped.X);
            ctx.Check((line.Marquee || wrapped.Y <= one.Y) && line.X >= box.X && right <= box.X + box.Width,
                $"{what} keeps the title on one line inside its box (marquee {line.Marquee}, {wrapped.Y:0.0} px tall against one line's {one.Y:0.0}, {line.X:0.0} to {right:0.0} inside {box.X:0} to {box.X + box.Width:0})");
        }
        finally
        {
            view.Free();
        }
    }

    // Stunt flying on the airfield with the Course list open, the sample picked by a click and flown.
    private static LaunchExit? CoursePicked(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, InstantActionFeature ia, List<MenuExit> exits)
    {
        const string sample = "sample_out_and_back";
        const string title = "Airfield Out and Back";
        ctx.Check(shell.InstantAction.PoseCourse() && shell.InstantAction.OpenDropdown == OriginalInstantActionScreen.CourseKey,
            $"stunt flying opens the Course list ({ia.MissionType.Key}, {shell.InstantAction.OpenDropdown})");
        var labels = shell.Rows.Select(r => r.Label).ToList();
        ctx.Check(ia.Environment.Code == "C1" && labels.Count >= 2 && labels[0] == InstantActionFeature.DangerZonesLabel && labels.Contains(title),
            $"on the airfield the list is the Danger Zones then the chapter's courses by their titles ({ia.Environment.Code}: {string.Join(", ", labels)})");
        var pick = shell.Rows.FirstOrDefault(r => r.Label == title);
        if (pick == null)
        {
            return null;
        }

        Click(host, seat, Pointer(fit, pick.X + 5f, pick.Y + 5f, pressed: true, clicked: true));
        ctx.Check(ia.Course == sample && shell.InstantAction.OpenDropdown == null && Row(shell, OriginalInstantActionScreen.CourseKey)?.Label == title,
            $"a click on the sample picks it and closes the list ({ia.Course}, {Row(shell, OriginalInstantActionScreen.CourseKey)?.Label})");
        ctx.Check(Row(shell, OriginalInstantActionScreen.WingmenKey) is { Enabled: false } && Row(shell, "IA_D_NENEMY0") is { Enabled: false }
                && Row(shell, OriginalInstantActionScreen.LivesKey) is { Enabled: false, Label: "" },
            $"a course flies alone, so the wingman, enemy and lives boxes stand blank and inert");

        var fly = Row(shell, OriginalInstantActionScreen.FlyMissionKey)!;
        int before = exits.Count;
        Click(host, seat, Pointer(fit, fly.X + 5f, fly.Y + 5f, pressed: true, clicked: true));
        if (exits.Count != before + 1 || exits[^1] is not LaunchExit exit)
        {
            ctx.Check(false, $"Fly Mission leaves as one LaunchExit ({exits.Count - before})");
            return null;
        }

        ctx.Check(exit is { Chapter: "C1", Mode: MenuMode.Stunt, Course: sample, InstantAction: null, Bots: null, Seats.Count: 1 },
            $"the exit names the course on C1 with no def ({exit.Chapter}, {exit.Course}, def {exit.InstantAction?.MissionType ?? "none"})");
        string plane = exit.Seats[0].PlaneNode;
        var menu = SessionSpec.FromMenu(SessionSpec.Parse(Array.Empty<string>()), exit.Chapter, new[] { plane }, exit.Mode,
            exit.InstantAction, bots: exit.Bots, course: exit.Course);
        var cli = SessionSpec.Parse(new[] { "--stunt", $"--course=C1/{sample}", $"--plane={plane}" });
        ctx.Check(menu.Course == cli.Course && menu.CourseRefusal == null && cli.CourseRefusal == null
                && menu.Chapter == cli.Chapter && menu.Mission == cli.Mission && menu.Stunt && cli.Stunt && !menu.Versus
                && menu.Players == cli.Players && menu.PlaneName == cli.PlaneName && menu.IaDef == cli.IaDef && menu.VsBots.Count == cli.VsBots.Count,
            $"its session spec is the one --stunt --course= builds ({menu.Course}/{cli.Course}, {menu.Chapter}/{menu.Mission}, players {menu.Players}, plane {menu.PlaneName})");
        ctx.Check(MenuReturnDestination.ForLaunch(exit) == MenuReturnDestination.InstantAction,
            $"and it names the Instant Action screen as its return ({MenuReturnDestination.ForLaunch(exit).GetType().Name})");
        // --det keeps a game-folder course of the same name out, as the shipped list above is.
        string heading = Launch.LaunchBoards.Subject(SessionSpec.Parse(new[] { "--det", $"--course=C1/{sample}" }), ctx.DataRoot);
        ctx.Check(heading == title, $"the load and pause boards head the flight with the course's title ({heading})");
        return exit;
    }

    // A second pilot and a racing bot row keep the Course box. The race time box and the race toggle
    // share the first clear line, and the Course box has the second alone. The launch races the
    // picked course with both seats and the bot in the picked window.
    private static void CourseRaced(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, PlayerSetupFeature setup,
        InstantActionFeature ia, List<MenuExit> exits)
    {
        const string sample = "sample_out_and_back";
        host.Show(MenuReturnDestination.InstantAction);
        var guest = new ScriptedSeat();
        var joined = setup.Join(guest);
        ctx.Check(joined != null && setup.AddBot(), $"a second pilot joins and a bot row joins the board ({host.Seats.Count}, {setup.Bots.Count})");
        ia.RaceBots = true;
        var time = Row(shell, OriginalInstantActionScreen.RaceTimeKey);
        var toggle = Row(shell, OriginalInstantActionScreen.RaceBotsKey);
        var course = Row(shell, OriginalInstantActionScreen.CourseKey);
        ctx.Check(ia.Course == sample && time is { Visible: true } && toggle is { Visible: true, Label: "On" } && course is { Visible: true, Enabled: true },
            $"the Course box stays with the race time box and the race toggle ({ia.Course}, {time?.Visible}, {toggle?.Visible}, {course?.Visible})");
        ctx.CheckShipped(time is { X: 525f, Y: 257.5f, Width: 110f } && toggle is { X: 690f, Y: 257.5f, Width: 45f } && course is { X: 525f, Y: 327.5f, Width: 210f },
            $"on the install the race toggle stands beside the race time box at 690, 257.5 and the Course box alone at 525, 327.5 ({toggle?.X}, {toggle?.Y}; {course?.X}, {course?.Y})");
        ctx.Check(time != null && toggle != null && course != null && toggle.Y == time.Y && toggle.X >= time.X + time.Width && course.Y > time.Y
                && HasLine(shell, "Bots:", null, toggle.Y) && HasLine(shell, "Course:", null, course.Y)
                && !new[] { time, toggle, course }.Any(b => shell.Rows.Any(r => r.Key != b.Key && r.Visible && r.Width > 0f && r.Height > 0f
                    && r.X < b.X + b.Width && b.X < r.X + r.Width && r.Y < b.Y + b.Height && b.Y < r.Y + r.Height)),
            $"the race toggle shares the race time box's line to its right, the Course box stands on a line of its own below, each titled and sharing no space");
        ctx.Check(Row(shell, OriginalInstantActionScreen.WingmenKey) is { Enabled: false } && Row(shell, "IA_D_NENEMY0") is { Enabled: false }
                && Row(shell, OriginalInstantActionScreen.LivesKey) is { Visible: false },
            $"the course race still takes no wingmen, waves or lives");

        var fly = Row(shell, OriginalInstantActionScreen.FlyMissionKey)!;
        int before = exits.Count;
        Click(host, seat, Pointer(fit, fly.X + 5f, fly.Y + 5f, pressed: true, clicked: true));
        Press(host, guest, Down);
        Press(host, guest, Accept);
        Press(host, guest, Accept);
        if (exits.Count != before + 1 || exits[^1] is not LaunchExit exit)
        {
            ctx.Check(false, $"the guest's walk ends as one LaunchExit ({exits.Count - before})");
            return;
        }

        ctx.Check(exit is { Chapter: "C1", Mode: MenuMode.Stunt, Course: sample, InstantAction: null, Seats.Count: 2, Bots.Count: 1 }
                && MenuReturnDestination.ForLaunch(exit) == MenuReturnDestination.InstantAction,
            $"the exit races the course with both seats and the bot, no def, back to Instant Action ({exit.Course}, {exit.Seats.Count} seats, {exit.Bots?.Count} bots)");
        // The launcher's own call over the exit, against the command line's course race.
        var planes = exit.Seats.Select(s => s.PlaneNode).ToList();
        var menu = SessionSpec.FromMenu(SessionSpec.Parse(Array.Empty<string>()), exit.Chapter, planes, exit.Mode, exit.InstantAction,
            vsKills: exit.Match?.KillTarget, vsTimeMinutes: exit.Match?.TimeLimitMinutes, vsLives: exit.Match?.Lives,
            missionType: exit.Match?.MissionType ?? DogfightMissionType.Deathmatch, bots: exit.Bots, course: exit.Course);
        var bot = exit.Bots![0];
        var cli = SessionSpec.Parse(new[] { "--stunt", $"--course=C1/{sample}", "--players=2", $"--plane={planes[0]},{planes[1]}",
            $"--vs-bot={bot.Plane}:skill={bot.Skill.ToString().ToLowerInvariant()}" });
        ctx.Check(menu.Course == cli.Course && menu.CourseRefusal == null && cli.CourseRefusal == null && menu.Chapter == cli.Chapter
                && menu.Mission == cli.Mission && menu.Stunt && cli.Stunt && !menu.Versus && menu.Players == 2 && cli.Players == 2
                && menu.PlaneNames.SequenceEqual(cli.PlaneNames) && menu.IaDef == null
                && menu.VsBots.Select(b => (b.Plane, b.Skill)).SequenceEqual(cli.VsBots.Select(b => (b.Plane, b.Skill))),
            $"its session spec is the one --stunt --course= --players=2 --vs-bot builds ({menu.Course}/{cli.Course}, players {menu.Players}/{cli.Players}, bots {menu.VsBots.Count}/{cli.VsBots.Count}, refusal {menu.CourseRefusal ?? "none"}/{cli.CourseRefusal ?? "none"})");
        ctx.Check(menu.StuntRaceMinutes == ia.RaceWindowMinutes,
            $"and races in the race time box's window ({menu.StuntRaceMinutes} against {ia.RaceWindowMinutes})");

        host.Show(MenuReturnDestination.InstantAction);
        ia.RaceBots = false;
        setup.Bots.Remove(setup.Bots.Rows[0].Id);
        if (joined != null)
        {
            setup.Unjoin(joined);
        }

        host.Show(MenuReturnDestination.InstantAction);
    }

    // A menu-driven session over a course no library holds. Its build fails with the reason kept
    // for the menu, and the run goes on (a Quit would end it). The launcher's failure path is then
    // replayed: the exit's own return, and the reason in Original's warning box.
    private static void CourseRefused(TestContext ctx, MenuHost host, OriginalShell shell, OriginalPresentation original, LaunchExit exit)
    {
        const string missing = "no_such_course";
        var spec = SessionSpec.FromMenu(SessionSpec.Parse(new[] { "--det", "--mute", "--no-pads" }), exit.Chapter,
            new[] { exit.Seats[0].PlaneNode }, exit.Mode, course: missing);
        var pane = new Godot.SubViewport { Size = new Godot.Vector2I(64, 64), OwnWorld3D = true, RenderTargetUpdateMode = Godot.SubViewport.UpdateMode.Disabled };
        var camera = new Godot.Camera3D();
        var sun = new Godot.DirectionalLight3D();
        pane.AddChild(camera);
        pane.AddChild(sun);
        ctx.Host.AddChild(pane);
        var paths = new BasePaths(RepoRoot: ctx.RepoRoot, DataRoot: ctx.DataRoot) { Game = ctx.Game, Data = ctx.Data };
        var savedClock = Utils.GameClock.Current;
        var session = new Launch.GameSession(spec, new Launch.LauncherContext
        {
            Decode = ctx.Decode,
            Paths = paths,
            ProbeRunner = new Tooling.ProbeRunner(paths),
            CaptureDirector = new Tooling.CaptureDirector(spec),
            MasterSeed = 1,
            Camera = camera,
            Orbit = new Flight.Camera.OrbitCamera(camera),
            Sun = sun,
            Env = new Godot.Environment(),
            MenuDriven = true,
            MenuPads = null,
            ExitSession = () => { },
            RestartSession = () => { },
        });
        pane.AddChild(session);
        string? refusal;
        try
        {
            bool built = session.StartSession();
            refusal = session.MenuRefusal;
            ctx.Check(!built && refusal != null && refusal.Contains($"C1/{missing}", StringComparison.Ordinal),
                $"a menu launch of a course no library holds fails its build with the reason kept ({built}: {refusal})");
        }
        finally
        {
            session.Free();
            pane.Free();
            Utils.GameClock.Current = savedClock;
        }

        if (refusal == null)
        {
            return;
        }

        host.Show(MenuReturnDestination.ForLaunch(exit));
        ctx.Check(shell.Screen == OriginalScreen.InstantAction && original.RaiseWarning(refusal) && shell.Dialog?.Message == refusal,
            $"the return lands on the Instant Action screen with the reason in the warning box ({shell.Screen}, {shell.Dialog?.Message})");
    }

    // A name no player would type, distinct per run, inside the name screen's own character set.
    private static string ScratchName() => "Scratch " + Guid.NewGuid().ToString("N")[..8];

    private static void OriginalScreenOpens(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, InstantActionFeature ia, MenuLayoutScreen? section)
    {
        var door = Row(shell, "MM_B_INSTANTACTION");
        ctx.Check(door is { Enabled: true }, $"the top level's Instant Action row is live");
        if (door == null)
        {
            return;
        }

        Click(host, seat, Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.InstantAction, $"a click on it opens the Instant Action screen ({shell.Screen})");
        // The invented tree carries no C1/IA1, so its first environment opens on the built-in def;
        // the launches below read the other chapters' own.
        ctx.Check(ia.BaseDef != null, $"with the first environment's def loaded as the base ({ia.BaseDef?.AceName ?? "none"})");
        ctx.CheckShipped(ia.BaseDef?.AceName != "Marshall Bill Redmann",
            $"on the install that def is the chapter's own rather than the built-in one ({ia.BaseDef?.AceName ?? "none"})");
        int contents = 0;
        foreach (var row in shell.Rows)
        {
            if (row.Kind == OriginalRowKind.ListRow && row.Key.StartsWith(OriginalInstantActionScreen.ContentsKey + ":", StringComparison.Ordinal))
            {
                contents++;
            }
        }

        var list = Box(section, OriginalInstantActionScreen.ContentsKey);
        int window = section?.Widget(OriginalInstantActionScreen.ContentsKey)?.Int("TotalDisplayed", 0) ?? 0;
        ctx.CheckShipped(list == (76f, 175f, 277f, 18f) && window == 14,
            $"the install's contents list is X 76 Y 175 Width 277 in 18-pixel rows, fourteen to the window ({list}, {window})");
        ctx.Check(window > 0 && contents == window, $"the contents window shows the layout's own count of rows ({contents} of {window})");

        // The list's arrows stand inside its right edge, in the gutter the page's own background
        // paints, and the rows stop short of them. On the install that column is 337 to 353.
        var upArrow = Row(shell, OriginalInstantActionScreen.ContentsUpKey);
        var downArrow = Row(shell, OriginalInstantActionScreen.ContentsDownKey);
        float column = upArrow?.Width ?? 0f;
        float edge = list.X + list.Width;
        ctx.CheckShipped(column == 16f, $"the install's scroll art is 16 wide ({column})");
        ctx.Check(column > 0f && upArrow!.X == edge - column && upArrow.Y == list.Y
                && downArrow is { } down && down.X == edge - down.Width && down.Width == column,
            $"the contents arrows stand inside the list's right edge ({upArrow?.X}, {downArrow?.X}, edge {edge})");
        ctx.Check(Row(shell, OriginalInstantActionScreen.ContentsKey + ":0") is { } first && first.X == list.X && first.Width == list.Width - column,
            $"and its rows give that column up rather than the authored box growing ({Row(shell, OriginalInstantActionScreen.ContentsKey + ":0")?.Width})");
        ctx.Check(List(shell, OriginalInstantActionScreen.ContentsKey) is { } contentsList && contentsList.Window.X == list.X
                && contentsList.Window.Width == list.Width && contentsList.Window.ThumbX == edge - column,
            $"the thumb rides the same column over the authored window ({List(shell, OriginalInstantActionScreen.ContentsKey)?.Window.ThumbX})");

        // The track runs between the two arrows, and the unscrolled thumb's length is the window's
        // share of the presets rather than the scroll tile's own. The reference frame draws its
        // foot at board y 356 on a track running 186 to 416.
        var contentsWindow = List(shell, OriginalInstantActionScreen.ContentsKey)?.Window;
        float trackTop = list.Y + (upArrow?.Height ?? 0f);
        float trackHeight = (window * list.ItemHeight) - (upArrow?.Height ?? 0f) - (downArrow?.Height ?? 0f);
        float share = trackHeight * window / InstantActionFeature.Presets.Count;
        ctx.CheckShipped(trackTop == 186f && trackHeight == 230f && Math.Abs(trackTop + share - 356f) < 1f,
            $"on the install the track runs 186 to 416 and the thumb's foot is at 356 ({trackTop}, {trackHeight}, {trackTop + share})");
        ctx.Check(contentsWindow is { Top: 0 } && contentsWindow.Value.TrackTop == trackTop && contentsWindow.Value.TrackHeight == trackHeight
            && Math.Abs(contentsWindow.Value.ThumbHeight - share) < 0.5f
            && Math.Abs(contentsWindow.Value.ThumbY - trackTop) < 1f,
            $"and its length is the share of the list the window shows ({contentsWindow?.ThumbHeight} of {contentsWindow?.TrackHeight}, wanting {share})");
        var plane = Box(section, OriginalInstantActionScreen.PlayerPlaneKey);
        ctx.CheckShipped(plane == (511f, 210f, 224f, 18f), $"the install's player plane dropdown is at 511, 210, 224 by 18 ({plane})");
        ctx.Check(Row(shell, OriginalInstantActionScreen.PlayerPlaneKey) is { Label: "Stock Autogyro" } planeRow
                && (planeRow.X, planeRow.Y, planeRow.Width, planeRow.Height) == plane,
            $"the player plane dropdown stands at its authored line with the first airframe as its stock row ({Row(shell, OriginalInstantActionScreen.PlayerPlaneKey)?.Label})");
        ctx.Check(Row(shell, OriginalInstantActionScreen.MissionKey)?.Label == "Dogfighting an Ace"
            && Row(shell, "IA_D_NENEMY0") is { Enabled: false, Label: "" } && Row(shell, "IA_D_PLANEE0") is { Enabled: false, Label: "" },
            $"the ace duel opens with its enemy boxes drawn blank and inert ({Row(shell, OriginalInstantActionScreen.MissionKey)?.Label}, {Row(shell, "IA_D_NENEMY0")?.Enabled}, {Row(shell, "IA_D_PLANEE0")?.Enabled})");
        ctx.Check(Row(shell, OriginalInstantActionScreen.PageUpKey) is { Enabled: false } && Row(shell, OriginalInstantActionScreen.PageDownKey) is { Enabled: true },
            $"with both paging buttons drawn and only the down one live ({Row(shell, OriginalInstantActionScreen.PageUpKey)?.Enabled}, {Row(shell, OriginalInstantActionScreen.PageDownKey)?.Enabled})");
        ctx.Check(Row(shell, OriginalInstantActionScreen.ExitKey) is { Enabled: true, Width: 200f, Height: 32f },
            $"Exit is the measured four-frame strip ({Row(shell, OriginalInstantActionScreen.ExitKey)?.Width}x{Row(shell, OriginalInstantActionScreen.ExitKey)?.Height})");
        ctx.Check(Row(shell, OriginalInstantActionScreen.BuildKey) is { Enabled: true } && Row(shell, OriginalInstantActionScreen.WeaponLoadoutKey) is { Enabled: true },
            $"Build Custom Plane and Weapon Loadout are live");
        var board = shell.Compose();
        string backdrop = section?.Widget("IA_BackGround")?.Art.FirstOrDefault() ?? string.Empty;
        ctx.CheckShipped(backdrop == "IA_BackGround.jpg", $"the install's page background is IA_BackGround.jpg ({backdrop})");
        ctx.Check(backdrop.Length > 0 && board.Backdrop.Count == 1 && board.Backdrop[0].Art.Name == backdrop,
            $"the page's background is the layout's own ({board.Backdrop.Count})");
    }

    private static void OriginalKeyboard(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, InstantActionFeature ia, MenuLayoutScreen? section)
    {
        ctx.Check(shell.FocusedKey == OriginalInstantActionScreen.ContentsKey + ":0", $"the focus opens on the first contents row ({shell.FocusedKey})");
        Press(host, seat, Right);
        ctx.Check(shell.FocusedKey == OriginalInstantActionScreen.PlayerPlaneKey, $"Right crosses to the right page's first dropdown ({shell.FocusedKey})");
        Press(host, seat, Right);
        ctx.Check(ia.PlayerPlane.Name == "Hellhound", $"Right on a dropdown steps its value ({ia.PlayerPlane.Name})");
        Press(host, seat, Accept);
        // The list is the eleven stock rows and then whatever the scratch store holds. The count
        // is therefore a floor, and the first rows are checked for the stock prefix.
        ctx.Check(shell.InstantAction.OpenDropdown == OriginalInstantActionScreen.PlayerPlaneKey && shell.Rows.Count >= 11 && StockRows(shell) == 11,
            $"Accept opens its list with the eleven stock rows first ({shell.InstantAction.OpenDropdown}, {shell.Rows.Count}, {StockRows(shell)} stock)");
        ctx.Check(shell.Rows[0].Label == "Stock Autogyro" && shell.Rows[1].Label == "Stock Hellhound" && shell.FocusedKey == OriginalInstantActionScreen.PlayerPlaneKey + ":1",
            $"named Stock <airframe> with the focus on the current row ({shell.Rows[0].Label}, {shell.Rows[1].Label}, {shell.FocusedKey})");

        // The store's length decides whether the list outruns its authored window of twenty, so
        // the row width is read against both readings of the same rule rather than one count.
        var open = List(shell, OriginalInstantActionScreen.PlayerPlaneKey);
        bool scrolls = open is { Window.Scrolls: true };
        var plane = Box(section, OriginalInstantActionScreen.PlayerPlaneKey);
        float rowWidth = scrolls ? open!.Window.ThumbX - plane.X : plane.Width;
        ctx.Check(shell.Rows[0].X == plane.X && Math.Abs(shell.Rows[0].Width - rowWidth) < 0.5f,
            $"its rows keep off the scroll column only where there is one ({shell.Rows[0].Width} of {rowWidth}, scrolling={scrolls})");
        Press(host, seat, Down);
        Press(host, seat, Accept);
        ctx.Check(shell.InstantAction.OpenDropdown == null && ia.PlayerPlane.Name == "Balmoral" && Row(shell, OriginalInstantActionScreen.PlayerPlaneKey)?.Label == "Stock Balmoral",
            $"Down and Accept pick the next row and close the list ({ia.PlayerPlane.Name}, {Row(shell, OriginalInstantActionScreen.PlayerPlaneKey)?.Label})");
        Press(host, seat, Down);
        ctx.Check(shell.FocusedKey == OriginalInstantActionScreen.WingmenKey, $"Down walks the right page ({shell.FocusedKey})");
        Press(host, seat, Right);
        ctx.Check(ia.NumWingmen == 1 && Row(shell, OriginalInstantActionScreen.WingmanPlaneKey) is { Enabled: true },
            $"one wingman fills the wingman plane dropdown ({ia.NumWingmen})");
        Press(host, seat, Left);
        ctx.Check(ia.NumWingmen == 0 && Row(shell, OriginalInstantActionScreen.WingmanPlaneKey) is { Enabled: false, Label: "" },
            $"and zero leaves its box standing blank and inert ({ia.NumWingmen}, {Row(shell, OriginalInstantActionScreen.WingmanPlaneKey)?.Enabled})");
    }

    // The remake-only lives box, the one control on the screen the layout authors no row for: the
    // walk reaches it, a sideways step and a picked row both write the shared feature's own value,
    // the ace duel leaves it live where it blanks the enemy boxes, and a contents row's preset,
    // which carries no lives, leaves it where the player put it.
    private static void OriginalLives(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, InstantActionFeature ia, MenuLayoutScreen? section)
    {
        var box = Row(shell, OriginalInstantActionScreen.LivesKey);
        var mission = Box(section, OriginalInstantActionScreen.MissionKey);
        ctx.CheckShipped(box is { X: 525f, Y: 257.5f, Height: 18f },
            $"on the install that is the line between the wingman and mission rows ({box?.X}, {box?.Y})");
        ctx.Check(box is { Enabled: true, Label: "1", Width: 90f } && box.X == mission.X && box.Height == mission.ItemHeight,
            $"the lives box stands in the mission dropdown's column at its height reading the opening life ({box?.Label}, {box?.X}, {box?.Y})");
        ctx.Check(!shell.Rows.Any(r => r.Key != OriginalInstantActionScreen.LivesKey && r.Visible && r.Width > 0f && r.Height > 0f
            && r.X < box!.X + box.Width && box.X < r.X + r.Width && r.Y < box.Y + box.Height && box.Y < r.Y + r.Height),
            $"and no authored row shares its space, the line being one the section skips ({box?.Y})");
        // The walk reaches it from the live box straight above it, the wingman count on the install.
        string above = box != null ? AboveKey(shell, box) : "none";
        ctx.CheckShipped(above == OriginalInstantActionScreen.WingmenKey, $"the box above it is the wingman count ({above})");
        OriginalWalk.WalkTo(shell, above, frame => Press(host, seat, frame));
        Press(host, seat, Down);
        ctx.Check(shell.FocusedKey == OriginalInstantActionScreen.LivesKey, $"the walk reaches it from the box above it ({above}, {shell.FocusedKey})");
        Press(host, seat, Right);
        ctx.Check(ia.Lives == 2 && Row(shell, OriginalInstantActionScreen.LivesKey)?.Label == "2",
            $"a sideways step writes the shared feature's lives ({ia.Lives})");
        Press(host, seat, Left);
        Press(host, seat, Left);
        ctx.Check(ia.Lives == 0 && Row(shell, OriginalInstantActionScreen.LivesKey)?.Label == "Unlimited",
            $"and zero reads Unlimited, the word for no lives limit ({Row(shell, OriginalInstantActionScreen.LivesKey)?.Label})");
        Press(host, seat, Accept);
        ctx.Check(shell.InstantAction.OpenDropdown == OriginalInstantActionScreen.LivesKey
            && shell.Rows.Count == InstantActionFeature.MaxLives + 1
            && shell.Rows[0] is { Label: "Unlimited", Width: 90f } && shell.Rows[0].X == mission.X && shell.Rows[^1].Label == "9",
            $"Accept opens a list of every count with no scroll chrome ({shell.Rows.Count} rows, {shell.Rows[0].Label} to {shell.Rows[^1].Label})");
        var pick = shell.Rows[3];
        Click(host, seat, Pointer(fit, pick.X + 5f, pick.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.InstantAction.OpenDropdown == null && ia.Lives == 3 && Row(shell, OriginalInstantActionScreen.LivesKey)?.Label == "3",
            $"a click on a row picks that count and closes the list ({ia.Lives})");
        ctx.Check(ia.IsAceDuel && Row(shell, OriginalInstantActionScreen.LivesKey) is { Enabled: true }
            && Row(shell, "IA_D_NENEMY0") is { Enabled: false },
            $"the ace duel leaves it live where it blanks the enemy boxes ({Row(shell, OriginalInstantActionScreen.LivesKey)?.Enabled})");

        var contents = Row(shell, OriginalInstantActionScreen.ContentsKey + ":1");
        if (contents != null)
        {
            Click(host, seat, Pointer(fit, contents.X + 5f, contents.Y + 5f, pressed: true, clicked: true));
            ctx.Check(ia.PresetIndex == 1 && ia.Lives == 3 && Row(shell, OriginalInstantActionScreen.LivesKey)?.Label == "3",
                $"and a contents row applies its preset over the page with the lives left alone ({ia.PresetIndex}, {ia.Lives})");
        }

        // Back onto the opening life, so the launches below fly the def the rest of the suite pins.
        ctx.Check(shell.InstantAction.PoseLives(1) && ia.Lives == 1 && Row(shell, OriginalInstantActionScreen.LivesKey)?.Label == "1",
            $"the pose the screenshot aid uses puts it back on one life ({ia.Lives})");
    }

    // Four presets, one per mission type, each flown through Fly Mission: the exit is the
    // feature's, its def derives the session spec the launcher would build, and the screen is
    // re-entered from the top level the way a return does.
    private static void OriginalLaunches(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, InstantActionFeature ia, List<MenuExit> exits)
    {
        var cli = SessionSpec.Parse(Array.Empty<string>());
        var cases = new (int Preset, string Chapter, string Mission, string Plane, string Node, int Wingmen, int Enemies)[]
        {
            (2, "C1B", "dogfight_ace", "Autogyro", "player_autogyro", 0, 0),
            (0, "C4", "dogfight_squadron", "Firebrand", "player_fbrand", 2, 4),
            (1, "C5", "stunt_flying", "Bloodhawk", "player_bhawk", 0, 4),
            (3, "C3", "zeppelin_run", "Fury", "player_fury", 4, 6),
        };
        foreach (var c in cases)
        {
            if (shell.Screen != OriginalScreen.InstantAction)
            {
                var door = Row(shell, "MM_B_INSTANTACTION");
                if (door == null)
                {
                    return;
                }

                Click(host, seat, Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
            }

            var row = Row(shell, $"{OriginalInstantActionScreen.ContentsKey}:{c.Preset}");
            var fly = Row(shell, OriginalInstantActionScreen.FlyMissionKey);
            ctx.Check(row != null && fly != null, $"preset {c.Preset} and Fly Mission are on screen");
            if (row == null || fly == null)
            {
                return;
            }

            int before = exits.Count;
            Click(host, seat, Pointer(fit, row.X + 5f, row.Y + 5f, pressed: true, clicked: true));
            ctx.Check(ia.PresetIndex == c.Preset && ia.Environment.Code == c.Chapter && ia.MissionType.Key == c.Mission,
                $"clicking contents row {c.Preset} applies it ({ia.PresetIndex}, {ia.Environment.Code}, {ia.MissionType.Key})");
            fly = Row(shell, OriginalInstantActionScreen.FlyMissionKey)!;
            Click(host, seat, Pointer(fit, fly.X + 5f, fly.Y + 5f, pressed: true, clicked: true));
            ctx.Check(exits.Count == before + 1 && exits[^1] is LaunchExit, $"Fly Mission leaves as one LaunchExit ({exits.Count - before})");
            if (exits[^1] is not LaunchExit launch)
            {
                return;
            }

            var def = launch.InstantAction;
            ctx.Check(launch.Chapter == c.Chapter && launch.Mode == MenuMode.Stunt && def != null,
                $"the {c.Mission} exit names {c.Chapter} in the Instant Action mode ({launch.Chapter}, {launch.Mode})");
            ctx.Check(launch.Seats.Count == 1 && launch.Seats[0].PlaneNode == c.Node && launch.Seats[0].Pads.Count == 0,
                $"for seat 0 alone in the preset's airframe with no pads ({launch.Seats[0].PlaneNode})");
            if (def == null)
            {
                return;
            }

            ctx.Check(def.MissionType == c.Mission && def.PlayerPlane == c.Plane && def.NumWingmen == c.Wingmen && def.Waves[0].NumEnemies == c.Enemies,
                $"the def is the preset's ({def.MissionType}, {def.PlayerPlane}, {def.NumWingmen} wingmen, {def.Waves[0].NumEnemies} in wave 1)");
            ctx.Check(def.AceName != "Marshall Bill Redmann",
                $"over the environment's own def ({def.AceName})");
            var spec = SessionSpec.FromMenu(cli, launch.Chapter, new[] { launch.Seats[0].PlaneNode }, launch.Mode, def);
            ctx.Check(spec.Chapter == c.Chapter && spec.Scenario == c.Mission && spec.Stunt == (c.Mission == "stunt_flying") && spec.IaDef == def,
                $"and derives the session spec the launcher builds ({spec.Chapter}, {spec.Scenario}, stunt={spec.Stunt})");
            ctx.Check(!host.Shown, $"the host hid the presentation on the exit");
            host.Show(MenuReturnDestination.TopLevel);
            ctx.Check(shell.Screen == OriginalScreen.TopLevel && ia.PresetIndex == c.Preset,
                $"the return re-enters the top level with the setup kept ({shell.Screen}, preset {ia.PresetIndex})");
        }
    }

    // A build saved to the scratch store, entered from the top level. The Pilot Plane list offers
    // it after the stock rows under "<build name> <airframe>". A click on it flies the airframe's
    // stock node with the def on the seat, and the pick survives a return. The wingman list never
    // lists it. The roster re-read offers a build saved while the screen shows. Deleting the file
    // drops the row and the pick back onto the airframe's stock row.
    private static void OriginalCustomPilot(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, InstantActionFeature ia, List<MenuExit> exits, CustomPlaneStore store, string scratch)
    {
        const int Fury = 7;
        string rowText = scratch + " Fury";
        store.Save(new CustomPlaneDef { Name = scratch, Airframe = Fury, Engine = 1 });
        if (!EnterInstantAction(ctx, host, seat, shell, fit))
        {
            return;
        }

        int row = PilotRowOf(shell, scratch);
        ctx.Check(row >= 11 && shell.InstantAction.PilotRoster[row].Node == "player_fury",
            $"entering the screen reads the store: the build sits after the eleven stock rows flying the Fury's node (row {row})");

        // The wingman list, opened beside it, is the stock table alone.
        if (ia.NumWingmen == 0)
        {
            ia.SetWingmen(1);
        }

        var wingmanDrop = Row(shell, OriginalInstantActionScreen.WingmanPlaneKey);
        ctx.Check(wingmanDrop != null, $"the Wingman Plane dropdown shows with a wingman ({ia.NumWingmen})");
        if (wingmanDrop != null)
        {
            Click(host, seat, Pointer(fit, wingmanDrop.X + 5f, wingmanDrop.Y + 5f, pressed: true, clicked: true));
            ctx.Check(shell.InstantAction.OpenDropdown == OriginalInstantActionScreen.WingmanPlaneKey && shell.Rows.Count == 11 && LabelRow(shell, rowText) < 0 && shell.Rows[7].Label == "Fury",
                $"the wingman list is the eleven stock names alone ({shell.Rows.Count}, {shell.Rows[7].Label})");
            Press(host, seat, Accept);
            ctx.Check(shell.InstantAction.OpenDropdown == null, $"Accept on its current row closes it");
        }

        var drop = Row(shell, OriginalInstantActionScreen.PlayerPlaneKey);
        ctx.Check(drop != null, $"the Pilot Plane dropdown is on screen");
        if (drop == null || row < 0)
        {
            return;
        }

        Click(host, seat, Pointer(fit, drop.X + 5f, drop.Y + 5f, pressed: true, clicked: true));
        var item = Row(shell, $"{OriginalInstantActionScreen.PlayerPlaneKey}:{row}");
        ctx.Check(shell.InstantAction.OpenDropdown == OriginalInstantActionScreen.PlayerPlaneKey && item != null && item.Label == rowText && StockRows(shell) == 11,
            $"a click opens the list with the build's row named for it after the stock rows ({item?.Label})");
        if (item == null)
        {
            return;
        }

        Click(host, seat, Pointer(fit, item.X + 5f, item.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.InstantAction.OpenDropdown == null && shell.InstantAction.PilotRow == row && Row(shell, OriginalInstantActionScreen.PlayerPlaneKey)?.Label == rowText,
            $"a click on it picks the build and closes the list ({shell.InstantAction.PilotRow}, {Row(shell, OriginalInstantActionScreen.PlayerPlaneKey)?.Label})");
        ctx.Check(ia.PlayerPlane.Name == "Fury", $"the feature's own pick moves onto the build's airframe ({ia.PlayerPlane.Name})");

        var fly = Row(shell, OriginalInstantActionScreen.FlyMissionKey)!;
        int before = exits.Count;
        Click(host, seat, Pointer(fit, fly.X + 5f, fly.Y + 5f, pressed: true, clicked: true));
        ctx.Check(exits.Count == before + 1 && exits[^1] is LaunchExit, $"Fly Mission leaves as one LaunchExit ({exits.Count - before})");
        if (exits[^1] is not LaunchExit launch)
        {
            return;
        }

        var choice = launch.Seats[0];
        ctx.Check(launch.Seats.Count == 1 && choice.PlaneNode == "player_fury" && choice.Custom?.Name == scratch,
            $"seat 0 flies the airframe's stock node with the build's def on the seat ({choice.PlaneNode}, {choice.Custom?.Name ?? "no def"})");
        ctx.Check(launch.InstantAction?.PlayerPlane == "Fury", $"the def's player plane is the airframe's stock name ({launch.InstantAction?.PlayerPlane})");
        var spec = SessionSpec.FromMenu(SessionSpec.Parse(Array.Empty<string>()), launch.Chapter, new[] { choice.PlaneNode },
            launch.Mode, launch.InstantAction, customPlanes: new[] { choice.Custom });
        ctx.Check(spec.PlaneNames.Count == 1 && spec.PlaneNames[0] == "player_fury" && spec.MenuCustomPlanes.Count == 1 && spec.MenuCustomPlanes[0]?.Name == scratch,
            $"and the session spec resolves to the stock node with the build's name riding it ({spec.PlaneNames[0]}, {spec.MenuCustomPlanes[0]?.Name ?? "no def"})");

        host.Show(MenuReturnDestination.TopLevel);
        if (!EnterInstantAction(ctx, host, seat, shell, fit))
        {
            return;
        }

        ctx.Check(shell.InstantAction.PilotRow == PilotRowOf(shell, scratch) && Row(shell, OriginalInstantActionScreen.PlayerPlaneKey)?.Label == rowText,
            $"the pick survives the return ({Row(shell, OriginalInstantActionScreen.PlayerPlaneKey)?.Label})");

        // The re-read the hangar's return calls: a build saved while the screen shows is offered
        // without leaving it, and a deleted file drops both its row and the pick.
        string second = ScratchName();
        store.Save(new CustomPlaneDef { Name = second, Airframe = 3, Engine = 1 });
        try
        {
            ctx.Check(PilotRowOf(shell, second) < 0, $"a build saved while the screen shows is not offered until the roster is re-read");
            shell.InstantAction.RefreshRoster();
            int secondRow = PilotRowOf(shell, second);
            ctx.Check(secondRow >= 11 && shell.InstantAction.PilotRoster[secondRow].Node == "player_bhawk",
                $"RefreshInstantActionRoster offers it on the Bloodhawk's node (row {secondRow})");
            ctx.Check(Row(shell, OriginalInstantActionScreen.PlayerPlaneKey)?.Label == rowText, $"and the standing pick is kept ({Row(shell, OriginalInstantActionScreen.PlayerPlaneKey)?.Label})");
        }
        finally
        {
            store.Delete(second);
        }

        store.Delete(scratch);
        shell.InstantAction.RefreshRoster();
        ctx.Check(PilotRowOf(shell, scratch) < 0 && PilotRowOf(shell, second) < 0 && shell.InstantAction.PilotRoster.Count >= 11,
            $"deleting the files drops their rows ({shell.InstantAction.PilotRoster.Count} rows)");
        ctx.Check(Row(shell, OriginalInstantActionScreen.PlayerPlaneKey)?.Label == "Stock Fury" && ia.PlayerPlane.Name == "Fury",
            $"and the pick falls back onto the airframe's stock row ({Row(shell, OriginalInstantActionScreen.PlayerPlaneKey)?.Label})");
        var exit = Row(shell, OriginalInstantActionScreen.ExitKey)!;
        Click(host, seat, Pointer(fit, exit.X + 5f, exit.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.TopLevel, $"Exit returns to the top level ({shell.Screen})");
    }

    // Weapon Loadout with the radio on Wingman: the decoded ammo chrome opens over the wingmen's
    // shared fit and the wingman airframe's own slots and pylons, a sideways step on a field
    // writes the fit, CANCEL restores what stood on entry, ACCEPT keeps it, and Fly Mission's def
    // carries the kept fit.
    private static void OriginalLoadout(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, InstantActionFeature ia, MenuLayoutScreen? section)
    {
        if (!EnterInstantAction(ctx, host, seat, shell, fit))
        {
            return;
        }

        var radio = Row(shell, OriginalInstantActionScreen.WingmanRadioKey)!;
        Click(host, seat, Pointer(fit, radio.X + 5f, radio.Y + 5f, pressed: true, clicked: true));
        var loadout = Row(shell, OriginalInstantActionScreen.WeaponLoadoutKey);
        ctx.Check(shell.InstantAction.LoadoutTarget == 1 && loadout is { Enabled: true }, $"the Wingman radio takes the target and Weapon Loadout is live ({shell.InstantAction.LoadoutTarget})");
        if (loadout == null)
        {
            return;
        }

        Click(host, seat, Pointer(fit, loadout.X + 5f, loadout.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.InstantActionLoadout && ReferenceEquals(shell.InstantAction.LoadoutFit, ia.WingmanFit) && shell.InstantAction.LoadoutNode == ia.WingmanPlane.Node,
            $"a click opens the loadout screen on the wingmen's shared fit over their airframe ({shell.Screen}, {shell.InstantAction.LoadoutNode})");
        var def = StockLoadouts.Load().ForModel(ia.WingmanPlane.Node);
        int guns = 0;
        foreach (var gun in def?.Guns ?? new List<GunSpec>())
        {
            guns += gun.Turret ? 0 : 1;
        }

        int pylons = def?.Hardpoints?.Count ?? 0;
        ctx.Check(shell.Rows.Count == guns + pylons + 2, $"one field per firable gun slot and per pylon of the {ia.WingmanPlane.Name}, then ACCEPT and CANCEL ({shell.Rows.Count} rows, {guns} guns, {pylons} pylons)");
        var ammo = Box(section, OriginalInstantActionScreen.LoadoutAmmoPrefix + "0");
        var acceptAt = Corner(section, OriginalInstantActionScreen.LoadoutAcceptKey);
        var cancelAt = Corner(section, OriginalInstantActionScreen.LoadoutCancelKey);
        string backdrop = section?.Widget("OL_BACKGROUND")?.Art.FirstOrDefault() ?? string.Empty;
        ctx.CheckShipped(ammo == (136f, 120f, 148f, 15f) && acceptAt == (341f, 553f) && cancelAt == (551f, 553f) && backdrop == "OL_BackGround.jpg",
            $"the install's first ammunition field, plaques and backdrop ({ammo}, {acceptAt}, {cancelAt}, {backdrop})");
        ctx.Check(Row(shell, OriginalInstantActionScreen.LoadoutAmmoPrefix + "0") is { } field && (field.X, field.Y, field.Width, field.Height) == ammo,
            $"the first ammunition field stands at its authored box ({Row(shell, OriginalInstantActionScreen.LoadoutAmmoPrefix + "0")?.X})");
        ctx.Check(Row(shell, OriginalInstantActionScreen.LoadoutAcceptKey) is { Enabled: true } acceptRow && (acceptRow.X, acceptRow.Y) == acceptAt
                && Row(shell, OriginalInstantActionScreen.LoadoutCancelKey) is { Enabled: true } cancelRow && (cancelRow.X, cancelRow.Y) == cancelAt,
            $"ACCEPT and CANCEL LOADOUT stand at their authored places");
        var board = shell.Compose();
        ctx.Check(backdrop.Length > 0 && board.Backdrop.Count == 1 && board.Backdrop[0].Art.Name == backdrop,
            $"the screen's background is the section's own ({board.Backdrop.Count})");
        ctx.Check(board.Lines.Any(l => l.Text == ia.WingmanPlane.Name), $"and the fitted aircraft is named on it");

        OriginalRow? rocket = null;
        foreach (var row in shell.Rows)
        {
            if (row.Key.StartsWith(OriginalInstantActionScreen.LoadoutRocketPrefix, StringComparison.Ordinal))
            {
                rocket = row;
                break;
            }
        }

        ctx.Check(rocket != null && ia.WingmanFit.IsStock, $"the {ia.WingmanPlane.Name} carries a pylon and the fit opens stock");
        if (rocket == null)
        {
            return;
        }

        int pylon = int.Parse(rocket.Key[OriginalInstantActionScreen.LoadoutRocketPrefix.Length..], System.Globalization.CultureInfo.InvariantCulture) + 1;
        Press(host, seat, Pointer(fit, rocket.X + 5f, rocket.Y + 5f));
        Press(host, seat, Right);
        ctx.Check(!ia.WingmanFit.IsStock && ia.WingmanFit.PylonFor(pylon) != null && Row(shell, rocket.Key)?.Label != rocket.Label,
            $"Right on the pylon field steps its ordnance into the shared fit ({ia.WingmanFit.PylonFor(pylon)}, {Row(shell, rocket.Key)?.Label})");
        var cancel = Row(shell, OriginalInstantActionScreen.LoadoutCancelKey)!;
        Click(host, seat, Pointer(fit, cancel.X + 5f, cancel.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.InstantAction && ia.WingmanFit.IsStock && shell.FocusedKey == OriginalInstantActionScreen.WeaponLoadoutKey,
            $"CANCEL restores the stock fit and lands back on Weapon Loadout ({shell.Screen}, {shell.FocusedKey})");

        Click(host, seat, Pointer(fit, loadout.X + 5f, loadout.Y + 5f, pressed: true, clicked: true));
        Click(host, seat, Pointer(fit, rocket.X + 5f, rocket.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.InstantAction.OpenDropdown == rocket.Key && shell.Rows.Count == StockLoadouts.Load().Options.PylonOrdnance.Count,
            $"a click on the field opens its list over the ordnance roster ({shell.InstantAction.OpenDropdown}, {shell.Rows.Count})");
        Press(host, seat, Down);
        Press(host, seat, Accept);
        string? picked = ia.WingmanFit.PylonFor(pylon);
        ctx.Check(shell.InstantAction.OpenDropdown == null && picked != null, $"Down and Accept pick the next row and close the list ({picked})");
        var accept = Row(shell, OriginalInstantActionScreen.LoadoutAcceptKey)!;
        Click(host, seat, Pointer(fit, accept.X + 5f, accept.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.InstantAction && ia.WingmanFit.PylonFor(pylon) == picked,
            $"ACCEPT keeps the pick and returns to the screen ({shell.Screen}, {ia.WingmanFit.PylonFor(pylon)})");
        // The launch carries the wingman fit only where wingmen fly.
        if (ia.IsAceDuel)
        {
            ia.SelectMissionType(1);
        }

        if (ia.NumWingmen == 0)
        {
            ia.SetWingmen(1);
        }

        ctx.Check(ReferenceEquals(ia.LaunchWingmanFit(host.Seats.Count), ia.WingmanFit), $"and the launch carries the wingman fit ({ia.MissionType.Key}, {ia.NumWingmen} wingmen)");
        ia.ResetWingmanFit();
    }

    // Build Custom Plane: the wallet-free hangar opens on the name screen, Back returns to the
    // screen, and a purchase returns to it with the new build in the Pilot Plane list.
    private static void OriginalBuild(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, HangarFeature hangar, CustomPlaneStore store, string built)
    {
        var build = Row(shell, OriginalInstantActionScreen.BuildKey);
        ctx.Check(shell.Screen == OriginalScreen.InstantAction && build is { Enabled: true }, $"Build Custom Plane is live on the screen ({shell.Screen})");
        if (build == null)
        {
            return;
        }

        // A default-configuration build inherits the airframe the door was opened over, which on
        // this door is the screen's own Pilot Plane pick (docs/org/hangar.md).
        int door = host.Features.Get<InstantActionFeature>().PlayerPlaneIndex;
        Click(host, seat, Pointer(fit, build.X + 5f, build.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.PlaneName && hangar.IsOpen && hangar.Wallet == null,
            $"a click opens the decoded name screen over a wallet-free build ({shell.Screen})");
        Press(host, seat, Back);
        ctx.Check(shell.Screen == OriginalScreen.InstantAction && !hangar.IsOpen && shell.FocusedKey == OriginalInstantActionScreen.BuildKey,
            $"Back drops the build and returns to the screen on its Build button ({shell.Screen}, {shell.FocusedKey})");

        Click(host, seat, Pointer(fit, build.X + 5f, build.Y + 5f, pressed: true, clicked: true));
        Press(host, seat, new MenuCommands { Typed = built });
        var ok = Row(shell, OriginalHangarScreen.NameOkKey);
        ctx.Check(ok is { Enabled: true } && shell.Hangar!.HangarName == built, $"typed frames name the plane ({shell.Hangar!.HangarName})");
        if (ok == null)
        {
            return;
        }

        Click(host, seat, Pointer(fit, ok.X + 5f, ok.Y + 5f, pressed: true, clicked: true));
        var ready = Row(shell, OriginalHangarScreen.ReadyKey);
        ctx.Check(shell.Screen == OriginalScreen.HangarAirframe && ready != null, $"OK opens the hub on the default configuration ({shell.Screen})");
        if (ready == null)
        {
            return;
        }

        Click(host, seat, Pointer(fit, ready.X + 5f, ready.Y + 5f, pressed: true, clicked: true));
        var purchase = Row(shell, OriginalHangarScreen.PurchaseNowKey);
        ctx.Check(shell.Screen == OriginalScreen.HangarPurchase && purchase is { Enabled: true }, $"READY opens the totals page with Purchase Now live ({shell.Screen})");
        if (purchase == null)
        {
            return;
        }

        Click(host, seat, Pointer(fit, purchase.X + 5f, purchase.Y + 5f, pressed: true, clicked: true));
        int row = PilotRowOf(shell, built);
        ctx.Check(shell.Screen == OriginalScreen.InstantAction && !hangar.IsOpen && store.Load(built) != null,
            $"Purchase Now saves the plane and returns to the screen ({shell.Screen})");
        ctx.Check(row >= 11 && store.Load(built) is { } saved && saved.Airframe == door
            && shell.InstantAction.PilotRoster[row].Node == StockAirframes.Node(door),
            $"whose Pilot Plane list offers the build after the stock rows on the door's airframe node (row {row}, airframe {door})");
        var drop = Row(shell, OriginalInstantActionScreen.PlayerPlaneKey);
        if (drop != null && row >= 0)
        {
            // The row's airframe word is the Instant Action table's, the stock rows' own vocabulary.
            string airframe = string.Empty;
            foreach (var stock in InstantActionFeature.Airframes)
            {
                if (stock.Node == shell.InstantAction.PilotRoster[row].Node)
                {
                    airframe = stock.Name;
                }
            }

            Click(host, seat, Pointer(fit, drop.X + 5f, drop.Y + 5f, pressed: true, clicked: true));
            ctx.Check(Row(shell, $"{OriginalInstantActionScreen.PlayerPlaneKey}:{row}")?.Label == built + " " + airframe,
                $"named for the build in the open list ({Row(shell, $"{OriginalInstantActionScreen.PlayerPlaneKey}:{row}")?.Label})");
            Press(host, seat, Back);
        }

        var exit = Row(shell, OriginalInstantActionScreen.ExitKey)!;
        Click(host, seat, Pointer(fit, exit.X + 5f, exit.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.TopLevel, $"Exit returns to the top level ({shell.Screen})");
    }

    // A second pilot joined on the Instant Action screen. The strip names it and the race time box
    // shows for a stunt run. FLY MISSION opens its per-seat aircraft screen instead of launching,
    // and the launch that follows carries both seats and the picked race window.
    private static void OriginalTwoSeats(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit,
        PlayerSetupFeature setup, InstantActionFeature ia, List<MenuExit> exits, MenuLayoutScreen? section)
    {
        if (!EnterInstantAction(ctx, host, seat, shell, fit))
        {
            return;
        }

        var stunt = Row(shell, $"{OriginalInstantActionScreen.ContentsKey}:1");
        if (stunt != null)
        {
            Click(host, seat, Pointer(fit, stunt.X + 5f, stunt.Y + 5f, pressed: true, clicked: true));
        }

        ctx.Check(ia.MissionType.Key == InstantActionFeature.StuntKey
            && Row(shell, OriginalInstantActionScreen.RaceTimeKey) is { Visible: false, Enabled: false } && !HasLine(shell, "Race Time:"),
            $"a solo stunt run shows no race time box ({ia.MissionType.Key}, {Row(shell, OriginalInstantActionScreen.RaceTimeKey)?.Visible})");
        ctx.Check(StripLines(shell) == 0, $"one seat draws no seat strip over the screen ({StripLines(shell)})");
        string livesLabel = InstantActionFeature.LivesLabel(ia.Lives);
        ctx.Check(Row(shell, OriginalInstantActionScreen.LivesKey) is { Visible: true, Enabled: true, Column: 1 } solo && solo.Label == livesLabel
            && HasLine(shell, "Lives:"),
            $"a solo stunt run keeps its lives box ({Row(shell, OriginalInstantActionScreen.LivesKey)?.Label})");
        ctx.Check(shell.InstantAction.PoseLives(ia.Lives) && shell.FocusedKey == OriginalInstantActionScreen.LivesKey,
            $"the cursor stands on the lives box before the second pilot joins ({shell.FocusedKey})");
        var guest = new ScriptedSeat();
        ctx.Check(setup.Join(guest) != null && host.Seats.Count == 2, $"a second pilot joins on the screen ({host.Seats.Count})");
        ctx.Check(StripLines(shell) == 2, $"and the strip names both seats ({StripLines(shell)})");
        ctx.Check(Row(shell, OriginalInstantActionScreen.LivesKey) is { Visible: false, Enabled: false, Column: -1, Label: "" } && !HasLine(shell, "Lives:"),
            $"a race spends no lives, so the lives box and its title go ({Row(shell, OriginalInstantActionScreen.LivesKey)?.Visible})");
        ctx.Check(Row(shell, OriginalInstantActionScreen.WingmenKey) is { Enabled: false, Label: "" } && Row(shell, "IA_D_NENEMY0") is { Enabled: false, Label: "" },
            $"the race blanks the wingman count and the enemy boxes ({Row(shell, OriginalInstantActionScreen.WingmenKey)?.Enabled})");
        ctx.Check(shell.FocusedKey == OriginalInstantActionScreen.PlayerPlaneKey,
            $"and the cursor left on it lifts to the live box above it, not the far page ({shell.FocusedKey})");
        OriginalRaceTime(ctx, host, seat, shell, ia, livesLabel, section);
        OriginalRaceBots(ctx, host, seat, shell, setup, ia, section);

        var fly = Row(shell, OriginalInstantActionScreen.FlyMissionKey);
        ctx.Check(fly != null, $"Fly Mission is on screen");
        if (fly == null)
        {
            return;
        }

        int before = exits.Count;
        Click(host, seat, Pointer(fit, fly.X + 5f, fly.Y + 5f, pressed: true, clicked: true));
        ctx.Check(exits.Count == before && shell.Screen == OriginalScreen.SeatPlane && shell.PickingSeat == 1,
            $"Fly Mission opens the guest's own aircraft screen instead of launching ({shell.Screen}, picking {shell.PickingSeat})");
        ctx.Check(shell.SeatReturn == OriginalScreen.InstantAction && ReferenceEquals(setup.Roster, shell.InstantAction.PilotRoster),
            $"over the Pilot Plane roster, the walk returning to Instant Action ({shell.SeatReturn})");
        Press(host, guest, Down);
        Press(host, guest, Accept);
        Press(host, guest, Accept);
        ctx.Check(exits.Count == before + 1 && exits[^1] is LaunchExit { Seats.Count: 2 },
            $"the guest's second Accept ends the walk as one LaunchExit for both seats ({exits.Count - before})");
        if (exits[^1] is LaunchExit both && both.Seats.Count == 2 && shell.InstantAction.PilotRoster.Count > 1)
        {
            ctx.Check(both.Seats[1].PlaneNode == shell.InstantAction.PilotRoster[1].Node && both.InstantAction != null,
                $"the guest flying the row it picked, the def still the screen's ({both.Seats[1].PlaneNode})");
            var spec = SessionSpec.FromMenu(SessionSpec.Parse(Array.Empty<string>()), both.Chapter,
                both.Seats.Select(s => s.PlaneNode).ToList(), both.Mode, both.InstantAction);
            ctx.Check(both.InstantAction?.RaceWindowMinutes == 10 && spec.StuntRaceMinutes == 10 && spec.Players == 2,
                $"the def carries the picked race window into the session spec ({both.InstantAction?.RaceWindowMinutes}, {spec.StuntRaceMinutes})");
        }
    }

    // The race time box over the layout with a second seat joined on a stunt run. It stands on the
    // lives box's line, which a race leaves free; on the install that is between the wingman row and
    // the mission type. The keyboard walk reaches it there. A sideways step picks the next window.
    // Another mission type hides it with the pick kept and brings the lives box back with its count.
    private static void OriginalRaceTime(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, InstantActionFeature ia, string livesLabel,
        MenuLayoutScreen? section)
    {
        var rows = shell.Rows;
        int at = -1;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Key == OriginalInstantActionScreen.RaceTimeKey)
            {
                at = i;
            }
        }

        var box = at >= 0 ? rows[at] : null;
        var mission = Box(section, OriginalInstantActionScreen.MissionKey);
        float titleX = section?.Widget("IA_T_MISSIONTITLE")?.Int("X") ?? -1;
        ctx.CheckShipped(box is { X: 525f, Y: 257.5f, Height: 18f } && titleX == 420f,
            $"on the install the race time box is at 525, 257.5 under a title at x 420 ({box?.X}, {box?.Y}, {titleX})");
        ctx.Check(box is { Visible: true, Enabled: true, Column: 1, Label: "5 minutes", Width: 110f }
                && box.X == mission.X && box.Height == mission.ItemHeight && box.Y == Row(shell, OriginalInstantActionScreen.LivesKey)?.Y,
            $"two seats show the race time box on the lives box's clear line at five minutes ({box?.Label}, {box?.X}, {box?.Y})");
        if (box == null)
        {
            return;
        }

        ctx.Check(!rows.Any(r => r.Key != box.Key && r.Visible && r.Width > 0f && r.Height > 0f
            && r.X < box.X + box.Width && box.X < r.X + r.Width && r.Y < box.Y + box.Height && box.Y < r.Y + r.Height),
            $"no other row shares its space ({box.Y})");
        // The race toggle's row shares the line, so the first box below is the first row off it.
        int below = at + 1;
        while (below < rows.Count && rows[below].Y == box.Y)
        {
            below++;
        }

        string before = at > 0 ? rows[at - 1].Key : "?";
        string after = below < rows.Count ? rows[below].Key : "?";
        ctx.CheckShipped(before == OriginalInstantActionScreen.LivesKey && after == OriginalInstantActionScreen.MissionKey,
            $"on the install its row stands with the hidden lives box's, before the mission type ({before}, {after})");
        ctx.Check(at > 0 && below < rows.Count && rows[at - 1].Y <= box.Y && rows[below].Y > box.Y,
            $"its row stands where its line falls, before the first box below it ({before}, {after})");
        ctx.Check(HasLine(shell, "Race Time:", titleX, box.Y), $"titled in the column's title ink and place");

        ctx.Check(shell.InstantAction.PoseRaceTime() && shell.FocusedKey == OriginalInstantActionScreen.RaceTimeKey,
            $"the screenshot aid's pose focuses it ({shell.FocusedKey})");
        Press(host, seat, Up);
        string up = shell.FocusedKey;
        Press(host, seat, Down);
        bool downLands = shell.FocusedKey == OriginalInstantActionScreen.RaceTimeKey;
        ctx.CheckShipped(up == OriginalInstantActionScreen.PlayerPlaneKey,
            $"on the install Up walks past the blank wingman boxes to the pilot plane ({up})");
        ctx.Check(up != OriginalInstantActionScreen.RaceTimeKey && downLands,
            $"Up walks to the live box above it and Down back onto it ({up}, {shell.FocusedKey})");
        Press(host, seat, Right);
        ctx.Check(ia.RaceWindowMinutes == 10 && Row(shell, OriginalInstantActionScreen.RaceTimeKey)?.Label == "10 minutes",
            $"a sideways step picks the next window ({ia.RaceWindowMinutes})");
        Press(host, seat, Down);
        ctx.CheckShipped(shell.FocusedKey == OriginalInstantActionScreen.MissionKey,
            $"on the install Down leaves it for the mission type, drawn under it ({shell.FocusedKey})");
        ctx.Check(shell.FocusedKey != OriginalInstantActionScreen.RaceTimeKey,
            $"and Down leaves it for the box under it ({shell.FocusedKey})");
        OriginalWalk.WalkTo(shell, OriginalInstantActionScreen.MissionKey, frame => Press(host, seat, frame));
        Press(host, seat, Right);
        ctx.Check(ia.MissionType.Key == "zeppelin_run" && Row(shell, OriginalInstantActionScreen.RaceTimeKey) is { Visible: false, Enabled: false }
            && !HasLine(shell, "Race Time:"),
            $"another mission type hides it ({ia.MissionType.Key}, focus {shell.FocusedKey})");
        ctx.Check(Row(shell, OriginalInstantActionScreen.LivesKey) is { Visible: true, Enabled: true, Column: 1 } lives && lives.Label == livesLabel
            && HasLine(shell, "Lives:"),
            $"and brings the lives box back with its count, two seats on another type spending lives ({Row(shell, OriginalInstantActionScreen.LivesKey)?.Label})");
        Press(host, seat, Left);
        ctx.Check(ia.MissionType.Key == InstantActionFeature.StuntKey && Row(shell, OriginalInstantActionScreen.RaceTimeKey) is { Visible: true, Label: "10 minutes" }
            && Row(shell, OriginalInstantActionScreen.LivesKey) is { Visible: false },
            $"and stunt flying brings it back with the pick kept and the lives box gone again ({Row(shell, OriginalInstantActionScreen.RaceTimeKey)?.Label})");
    }

    // The race toggle over the layout. A bot row left on a stunt run's join board shows it off on
    // the race time box's line, to its right. The row's removal hides it again.
    private static void OriginalRaceBots(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, PlayerSetupFeature setup, InstantActionFeature ia,
        MenuLayoutScreen? section)
    {
        ctx.Check(Row(shell, OriginalInstantActionScreen.RaceBotsKey) is { Visible: false } && !HasLine(shell, "Bots:"),
            $"with no bot row on the join board the race toggle is hidden ({ia.MissionType.Key})");
        ctx.Check(setup.AddBot(), $"a bot row joins the board");
        var toggle = Row(shell, OriginalInstantActionScreen.RaceBotsKey);
        var time = Row(shell, OriginalInstantActionScreen.RaceTimeKey);
        var mission = Box(section, OriginalInstantActionScreen.MissionKey);
        ctx.CheckShipped(toggle is { X: 690f, Y: 257.5f, Height: 18f },
            $"on the install the race toggle is at 690, 257.5 ({toggle?.X}, {toggle?.Y})");
        ctx.Check(!ia.RaceBots && toggle is { Visible: true, Enabled: true, Column: 1, Label: "Off", Width: 45f } && time != null
            && toggle.Y == time.Y && toggle.X + toggle.Width == mission.X + mission.Width && toggle.Height == mission.ItemHeight
            && shell.Compose().Lines.Any(l => l.Text == "Bots:" && l.Y == toggle.Y && l.X > time.X + time.Width && l.X < toggle.X),
            $"the race toggle shows off beside the race time box, ending where the mission dropdown ends, titled between them ({toggle?.Label}, {toggle?.X}, {toggle?.Y})");
        ctx.Check(toggle != null && !shell.Rows.Any(r => r.Key != toggle.Key && r.Visible && r.Width > 0f && r.Height > 0f
            && r.X < toggle.X + toggle.Width && toggle.X < r.X + r.Width && r.Y < toggle.Y + toggle.Height && toggle.Y < r.Y + r.Height),
            $"no other row shares its space");

        // The line walks in reading order. Up from the mission type reaches the toggle, Up again the
        // race time box, and Down from it the toggle.
        OriginalWalk.WalkTo(shell, OriginalInstantActionScreen.MissionKey, frame => Press(host, seat, frame));
        Press(host, seat, Up);
        string fromMission = shell.FocusedKey;
        Press(host, seat, Up);
        string fromToggle = shell.FocusedKey;
        Press(host, seat, Down);
        ctx.Check(fromMission == OriginalInstantActionScreen.RaceBotsKey && fromToggle == OriginalInstantActionScreen.RaceTimeKey
                && shell.FocusedKey == OriginalInstantActionScreen.RaceBotsKey,
            $"Up from the mission type lands on the toggle, Up from it on the race time box, and Down from that back on the toggle ({fromMission}, {fromToggle}, {shell.FocusedKey})");
        Press(host, seat, Right);
        ctx.Check(ia.RaceBots && shell.FocusedKey == OriginalInstantActionScreen.RaceBotsKey && Row(shell, OriginalInstantActionScreen.RaceBotsKey)?.Label == "On",
            $"Right turns it on and keeps the cursor on it ({ia.RaceBots}, {shell.FocusedKey})");
        Press(host, seat, Left);
        ctx.Check(!ia.RaceBots && shell.FocusedKey == OriginalInstantActionScreen.RaceBotsKey && Row(shell, OriginalInstantActionScreen.RaceBotsKey)?.Label == "Off",
            $"Left turns it off and keeps the cursor on it ({ia.RaceBots}, {shell.FocusedKey})");
        Press(host, seat, Down);
        ctx.CheckShipped(shell.FocusedKey == OriginalInstantActionScreen.MissionKey,
            $"on the install Down leaves it for the mission type under its line ({shell.FocusedKey})");
        ctx.Check(shell.FocusedKey != OriginalInstantActionScreen.RaceBotsKey, $"and Down leaves it ({shell.FocusedKey})");

        setup.Bots.Remove(setup.Bots.Rows[0].Id);
        ctx.Check(Row(shell, OriginalInstantActionScreen.RaceBotsKey) is { Visible: false }, $"and the row's removal hides it");
    }

    // Whether the composed screen writes a line, at a place when one is given.
    private static bool HasLine(OriginalShell shell, string text, float? x = null, float? y = null) =>
        shell.Compose().Lines.Any(l => l.Text == text && (x == null || l.X == x) && (y == null || l.Y == y));

    // How many seat-strip chips the composed screen carries, the strip being the overlay whose
    // lines are the player tags alone.
    private static int StripLines(OriginalShell shell)
    {
        int lines = 0;
        foreach (var overlay in shell.Compose().Overlays)
        {
            foreach (var line in overlay.Lines)
            {
                if (line.Text == SplitScreen.PlayerTag(0) || line.Text == SplitScreen.PlayerTag(1))
                {
                    lines++;
                }
            }
        }

        return lines;
    }

    private static bool EnterInstantAction(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit)
    {
        var door = Row(shell, "MM_B_INSTANTACTION");
        ctx.Check(door != null && shell.Screen == OriginalScreen.TopLevel, $"the top level's Instant Action row is on screen ({shell.Screen})");
        if (door == null)
        {
            return false;
        }

        Click(host, seat, Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
        return shell.Screen == OriginalScreen.InstantAction;
    }

    // A list or dropdown widget's box as the layout authors it: its corner, width and item height.
    private static (float X, float Y, float Width, float ItemHeight) Box(MenuLayoutScreen? section, string key) =>
        section?.Widget(key) is { } widget
            ? (widget.Int("X"), widget.Int("Y"), widget.Int("Width"), widget.Int("ItemHeight"))
            : (-1f, -1f, -1f, -1f);

    // A widget's authored corner.
    private static (float X, float Y) Corner(MenuLayoutScreen? section, string key) =>
        section?.Widget(key) is { } widget ? (widget.Int("X"), widget.Int("Y")) : (-1f, -1f);

    // The live row of the same column standing nearest above a box and over part of its width. A
    // Down onto the box starts there. Ties on a line go to the row whose edge is nearest.
    private static string AboveKey(OriginalShell shell, OriginalRow box) =>
        shell.Rows.Where(r => r.Key != box.Key && r.Column == box.Column && r.Visible && r.Enabled
                && r.Y + r.Height <= box.Y && r.X < box.X + box.Width && box.X < r.X + r.Width)
            .OrderByDescending(r => r.Y).ThenBy(r => Math.Abs(r.X - box.X))
            .Select(r => r.Key).FirstOrDefault() ?? "none";

    // The index of the open list's row carrying a label, or -1.
    private static int LabelRow(OriginalShell shell, string label)
    {
        var rows = shell.Rows;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Label == label)
            {
                return i;
            }
        }

        return -1;
    }

    // The row a build sits at in the pilot list, or -1: found by its own name among the custom
    // rows, never by the airframe it flies.
    private static int PilotRowOf(OriginalShell shell, string build)
    {
        for (int i = 0; i < shell.InstantAction.PilotRoster.Count; i++)
        {
            if (shell.InstantAction.PilotRoster[i].IsCustom && shell.InstantAction.PilotRoster[i].Name == build)
            {
                return i;
            }
        }

        return -1;
    }

    // How many rows from the top of an open list carry the stock prefix before the first that does not.
    private static int StockRows(OriginalShell shell)
    {
        int n = 0;
        foreach (var row in shell.Rows)
        {
            if (!row.Label.StartsWith("Stock ", StringComparison.Ordinal))
            {
                break;
            }

            n++;
        }

        return n;
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

    private static MenuCommands Pointer(BoardFit fit, float authoredX, float authoredY, bool pressed = false, bool clicked = false) =>
        new() { Pointer = new MenuPointer(fit.X(authoredX), fit.Y(authoredY), pressed, clicked) };

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
        host.Tick(1f / 60f);
    }

    private sealed class ScriptedSeat : IMenuInputSource
    {
        private readonly Queue<MenuCommands> _frames = new();

        public string DeviceLabel => "scripted";

        public bool CapturingText { get; set; }

        public void Enqueue(MenuCommands frame) => _frames.Enqueue(frame);

        public MenuCommands Poll(float dt) => _frames.Count > 0 ? _frames.Dequeue() : MenuCommands.None;

        public void Prime()
        {
        }
    }
}
