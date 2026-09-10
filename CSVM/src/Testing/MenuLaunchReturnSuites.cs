using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Session;
using CSVM.UI;
using CSVM.UI.Menu;
using CSVM.UI.Menu.BuiltIn;
using CSVM.UI.Menu.Original;

namespace CSVM.Testing;

/// <summary>
/// Every launch and every return through the presentation boundary, in both presentations, over a
/// real <see cref="MenuHost"/> and a scratch profile store: Free Flight, Instant Action (an ace and
/// a squadron), Dogfight (two seats) and a campaign mission each leave as their typed exit with the
/// host hiding the presentation; a top-level return lands on the top level itself even when the
/// process started on a <c>--menu=</c> aid; the debrief and cabin returns land on the book and the
/// cabin; and a presentation fallback before a return lands on Built-in's top level with the
/// request kept. The two hosts stand for two processes, one started under each presentation.
/// </summary>
internal static class MenuLaunchReturnSuites
{
    private const float Dt = 1f / 60f;
    private const string Pilot = "Zachary";

    private static readonly MenuCommands Accept = new() { Accept = true };
    private static readonly MenuCommands Back = new() { Back = true };
    private static readonly MenuCommands Down = new() { MoveY = 1 };
    private static readonly MenuCommands Up = new() { MoveY = -1 };
    private static readonly MenuCommands Right = new() { MoveX = 1 };
    private static readonly MenuCommands Contents = new() { Contents = true };

    [Suite("menu-launch-return",
        "every launch and return through the boundary in both presentations over a scratch store: "
        + "a Built-in process started on --menu=chapter opens on Chapter once, Free Flight, an ace "
        + "duel, a squadron, a two-seat Dogfight and a campaign mission each leave as their typed "
        + "exit with the host hiding the screen, every top-level return lands on Mode with the "
        + "cursors kept, the debrief and cabin returns land on the book and the cabin, Back on Mode "
        + "quits and Options' apply switches; an Original process started on --menu=free-flight "
        + "opens on Free Flight once and does the same over the decoded layout; and Original "
        + "refused at selection falls back to Built-in's top level with the request kept, a return "
        + "re-showing the same presentation without re-selecting")]
    internal static void MenuLaunchReturn(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        string root = Path.Combine(ctx.ScratchDir, "menu-launch-return");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        // ⚠ Both presentations open the campaign over this store: nothing here may read or write
        // user://Profiles. Seeded with the pilot, so the cabin is one Accept away from the roster.
        var store = new CampaignProfileStore(Path.Combine(root, "Profiles"));
        store.Save(CampaignProfileDef.NewProfile(Pilot));
        try
        {
            BuiltInProcess(ctx, store);
            OriginalProcess(ctx, layout, store);
            OriginalPlayerDoor(ctx, layout, store);
        }
        finally
        {
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    // A process started under Built-in with --menu=chapter: the factory reads the aid while it is
    // set and the first show consumes it, as the launcher does.
    private static void BuiltInProcess(TestContext ctx, CampaignProfileStore store)
    {
        var run = new Run(ctx, "chapter", null, store);
        var host = run.Host;
        try
        {
            host.Select(forceBuiltIn: false, cliOverride: null, savedRequest: null);
            run.Show(MenuReturnDestination.TopLevel);
            var built = host.Active as BuiltInPresentation;
            var menu = built?.Menu;
            ctx.Check(menu is { Visible: true }, $"the cold start stands Built-in up ({host.Active?.Id})");
            if (menu == null)
            {
                return;
            }

            menu.CampaignProfiles = store;
            ctx.Check(menu.ShownScreen == "Chapter", $"the cold start opens on the aid's screen ({menu.ShownScreen})");

            // Free Flight from the aid's screen: the first chapter, the first airframe, select, confirm.
            run.Press(Accept);
            run.Press(Accept);
            run.Press(Accept);
            var launch = run.Expect<LaunchExit>();
            ctx.Check(launch is { Mode: MenuMode.Free, Chapter: "C1", InstantAction: null, Seats.Count: 1 },
                $"Free Flight leaves as one Free LaunchExit for C1 with one seat ({launch?.Chapter}, {launch?.Seats.Count})");
            ctx.Check(!menu.Visible, $"the launchscreen is off screen after the exit");

            run.Show(MenuReturnDestination.TopLevel);
            ctx.Check(menu.Visible && menu.ShownScreen == "Mode" && menu.ShownRow == 0,
                $"the return lands on Mode, not on the aid's screen ({menu.ShownScreen}, row {menu.ShownRow})");
            run.Press(Accept);
            ctx.Check(menu.ShownScreen == "Chapter" && menu.ShownRow == 0, $"the chapter cursor survived the flight ({menu.ShownRowText})");
            run.Press(Back);

            InstantAction(ctx, run, menu);
            Dogfight(ctx, run, menu);
            Campaign(ctx, run, menu, store);

            // Quit and the Options route: the two exits that are not launches.
            run.Press(Back);
            ctx.Check(run.Expect<QuitExit>() != null, $"Back on the Mode screen leaves as one QuitExit");
            run.Show(MenuReturnDestination.TopLevel);
            WalkTo(run, menu, LaunchMenu.OptionsRow);
            run.Press(Accept);
            // Past the three steppers and the Controls door onto the fifth row, the apply row.
            run.Press(Down);
            run.Press(Down);
            run.Press(Down);
            run.Press(Down);
            run.Press(Accept);
            ctx.Check(run.Expect<OptionsApplyExit>() != null, $"Options' apply row leaves as one OptionsApplyExit");
            ctx.Check(run.HiddenAtEveryExit, $"the host had hidden the presentation before every one of the {run.Exits.Count} exits reached the sink");
        }
        finally
        {
            host.Deactivate();
        }
    }

    // The wizard twice: the ace duel skips Waves and Wingmen, the squadron walks them.
    private static void InstantAction(TestContext ctx, Run run, LaunchMenu menu)
    {
        WalkTo(run, menu, "Instant Action");
        run.Press(Accept);
        run.Press(Contents);
        run.Press(Accept);
        ctx.Check(menu.ShownScreen == "Environment" && menu.ShownRowText == "Sky Haven",
            $"the Table of Contents applies Girl Trouble onto Sky Haven ({menu.ShownScreen}, {menu.ShownRowText})");
        run.Press(Accept);
        run.Press(Up);
        ctx.Check(menu.ShownScreen == "MissionType" && menu.ShownRowText == "Dogfighting an Ace",
            $"Up from the preset's mission is the ace duel ({menu.ShownRowText})");
        run.Press(Accept);
        ctx.Check(menu.ShownScreen == "Plane", $"the ace duel goes straight to the aircraft ({menu.ShownScreen})");
        run.Press(Accept);
        run.Press(Accept);
        var ace = run.Expect<LaunchExit>();
        ctx.Check(ace is { Mode: MenuMode.Stunt, Chapter: "C4", InstantAction: { MissionType: "dogfight_ace" } },
            $"an ace duel leaves as one Instant Action LaunchExit with the ace def ({ace?.Chapter}, {ace?.InstantAction?.MissionType})");
        run.Show(MenuReturnDestination.TopLevel);
        ctx.Check(menu.ShownScreen == "Mode", $"the return lands on Mode ({menu.ShownScreen})");

        WalkTo(run, menu, "Instant Action");
        run.Press(Accept);
        run.Press(Accept);
        run.Press(Down);
        ctx.Check(menu.ShownScreen == "MissionType" && menu.ShownRowText == "Dogfighting a Squadron",
            $"the mission cursor survived the flight and Down is the squadron ({menu.ShownRowText})");
        run.Press(Accept);
        ctx.Check(menu.ShownScreen == "Waves", $"a squadron opens Waves ({menu.ShownScreen})");
        run.Press(Accept);
        ctx.Check(menu.ShownScreen == "Wingmen", $"Continue opens Wingmen ({menu.ShownScreen})");
        run.Press(Accept);
        ctx.Check(menu.ShownScreen == "Plane", $"and Wingmen's Accept opens the aircraft ({menu.ShownScreen})");
        run.Press(Accept);
        run.Press(Accept);
        var squadron = run.Expect<LaunchExit>();
        ctx.Check(squadron is { Mode: MenuMode.Stunt, InstantAction: { MissionType: "dogfight_squadron", NumWingmen: 2 } },
            $"a squadron leaves as one Instant Action LaunchExit with the squadron def ({squadron?.InstantAction?.MissionType}, {squadron?.InstantAction?.NumWingmen} wingmen)");
        run.Show(MenuReturnDestination.TopLevel);
        ctx.Check(menu.ShownScreen == "Mode", $"the return lands on Mode ({menu.ShownScreen})");
    }

    // Two seats: a second scripted source joins through the feature and is polled by the
    // launchscreen's own frame; both confirm and the launch carries both.
    private static void Dogfight(TestContext ctx, Run run, LaunchMenu menu)
    {
        var setup = run.Host.Features.Get<PlayerSetupFeature>();
        WalkTo(run, menu, "Dogfight");
        run.Press(Accept);
        run.Press(Accept);
        ctx.Check(menu.ShownScreen == "Plane", $"Dogfight reaches the aircraft screen ({menu.ShownScreen})");
        var guest = new ScriptedSeat();
        int before = run.Exits.Count;
        ctx.Check(setup.Join(guest) != null && run.Host.Seats.Count == 2, $"a second seat joins ({run.Host.Seats.Count})");
        run.Press(guest, Down);
        run.Press(guest, Accept);
        run.Press(guest, Accept);
        ctx.Check(setup.Seats[1].Confirmed && run.Exits.Count == before, $"the guest confirms and nothing launches on that alone ({run.Exits.Count})");
        run.Press(Accept);
        run.Press(Accept);
        var dogfight = run.Expect<LaunchExit>();
        ctx.Check(dogfight is { Mode: MenuMode.Versus, Seats.Count: 2 },
            $"Dogfight leaves as one Versus LaunchExit for both seats ({dogfight?.Mode}, {dogfight?.Seats.Count})");
        run.Show(MenuReturnDestination.TopLevel);
        ctx.Check(menu.ShownScreen == "Mode" && run.Host.Seats.Count == 2 && !setup.Seats[1].Locked,
            $"the return lands on Mode with both seats kept and the picks dropped ({menu.ShownScreen}, {run.Host.Seats.Count})");
        run.Host.RemoveSeat(guest);
        ctx.Check(run.Host.Seats.Count == 1, $"the guest leaves for the campaign leg ({run.Host.Seats.Count})");
    }

    // The cabin's FLY MISSION over the scratch store, then the two campaign returns and a plain
    // return from an abandoned mission, all on the same instance.
    private static void Campaign(TestContext ctx, Run run, LaunchMenu menu, CampaignProfileStore store)
    {
        WalkTo(run, menu, LaunchMenu.CampaignRow);
        run.Press(Accept);
        ctx.Check(menu.Campaign is { Screen: CampaignScreen.Roster } flow && ReferenceEquals(flow.Store, store),
            $"the Campaign door opens the roster over the scratch store ({menu.Campaign?.Screen})");
        if (menu.Campaign is not { } roster)
        {
            return;
        }

        roster.SelectProfile(store.Load(Pilot)!);
        int seq = roster.Feature.NextMissionSeq;
        WalkTo(run, menu, "Next Mission");
        run.Press(Accept);
        WalkTo(run, menu, "GO TO FLIGHT CHECK");
        run.Press(Accept);
        WalkTo(run, menu, "FLY MISSION");
        run.Press(Accept);
        var mission = run.Expect<CampaignMissionExit>();
        ctx.Check(mission is { Profile: Pilot, Seats.Count: 1 } && mission.MissionSeq == seq,
            $"FLY MISSION leaves as one CampaignMissionExit for the pilot's next mission ({mission?.Profile}, seq {mission?.MissionSeq})");
        ctx.Check(menu.Campaign == null, $"the flow is closed behind the exit");

        // The director records the flown mission and saves; the returns re-read the profile.
        var profile = store.Load(Pilot)!;
        CampaignProgression.Record(profile, new MissionAttempt(
            seq, CampaignProgression.PrimaryObjectiveMask, 420_000, 200, 90, profile.Planes[0].Airframe, profile.Planes[0].Name));
        store.Save(profile);
        run.Show(new DebriefReturn(Pilot, seq));
        ctx.Check(menu.Campaign is { Screen: CampaignScreen.Scrapbook } book && book.MissionSeq == seq && book.Profile?.MissionsCompleted == seq + 1,
            $"the debrief return opens the book on the flown mission over the saved profile ({menu.Campaign?.Screen}, seq {menu.Campaign?.MissionSeq})");
        ctx.Check(menu.ShownRowText == "RETURN TO CABIN", $"with the cursor on the way out ({menu.ShownRowText})");
        run.Show(new CabinReturn(Pilot));
        ctx.Check(menu.Campaign is { Screen: CampaignScreen.Cabin } cabin && cabin.Profile?.Name == Pilot,
            $"the cabin return lands on the cabin with the pilot seated ({menu.Campaign?.Screen})");
        run.Show(MenuReturnDestination.TopLevel);
        ctx.Check(menu.ShownScreen == "Mode" && menu.Campaign == null,
            $"a top-level return from a campaign mission lands on Mode with no open campaign ({menu.ShownScreen})");
    }

    // A process started under Original with --menu=free-flight, Built-in registered beside it.
    private static void OriginalProcess(TestContext ctx, MenuLayout layout, CampaignProfileStore store)
    {
        var run = new Run(ctx, "free-flight", layout, store);
        var host = run.Host;
        try
        {
            string? reason = host.Select(forceBuiltIn: false, cliOverride: "original", savedRequest: null);
            ctx.Check(host.Selected == PresentationId.Original && reason == null, $"Original is selected ({reason ?? "no reason"})");
            run.Show(MenuReturnDestination.TopLevel);
            var shell = (host.Active as OriginalPresentation)?.Shell;
            ctx.Check(shell != null, $"the cold start stands Original up ({host.Active?.Id})");
            if (shell == null)
            {
                return;
            }

            var size = ctx.Host.GetViewport().GetVisibleRect().Size;
            var fit = BoardFit.For(size.X, size.Y);
            ctx.Check(shell.Screen == OriginalScreen.FreeFlight, $"the cold start opens on the aid's screen ({shell.Screen})");

            // Free Flight from the aid's screen: the first chapter, the first airframe, FLY.
            run.Press(Accept);
            run.Press(Right);
            run.Press(Accept);
            run.Press(Up);
            run.Press(Accept);
            var launch = run.Expect<LaunchExit>();
            ctx.Check(launch is { Mode: MenuMode.Free, Chapter: "C1", Seats.Count: 1 },
                $"FLY leaves as one Free LaunchExit for C1 with one seat ({launch?.Chapter}, {launch?.Seats.Count})");
            run.Show(MenuReturnDestination.TopLevel);
            ctx.Check(shell.Screen == OriginalScreen.TopLevel, $"the return lands on the top level, not on the aid's screen ({shell.Screen})");

            OriginalInstantAction(ctx, run, shell, fit);
            OriginalDogfight(ctx, run, shell, fit);
            OriginalCampaign(ctx, run, shell, fit, store);

            var quit = Row(shell, "MM_B_QUIT");
            ctx.Check(quit != null, $"the top level carries Quit");
            if (quit != null)
            {
                run.Press(Pointer(fit, quit.X + 5f, quit.Y + 5f, pressed: true, clicked: true));
                ctx.Check(run.Expect<QuitExit>() != null, $"a click on Quit leaves as one QuitExit");
            }

            ctx.Check(run.HiddenAtEveryExit, $"the host had hidden the presentation before every one of the {run.Exits.Count} exits reached the sink");
            FallbackBeforeReturn(ctx, run);
        }
        finally
        {
            host.Deactivate();
        }
    }

    private static void OriginalInstantAction(TestContext ctx, Run run, OriginalShell shell, BoardFit fit)
    {
        var cases = new (int Preset, string Mission)[] { (2, "dogfight_ace"), (0, "dogfight_squadron") };
        foreach (var (preset, mission) in cases)
        {
            var door = Row(shell, "MM_B_INSTANTACTION");
            if (door == null)
            {
                ctx.Check(false, $"the top level carries Instant Action");
                return;
            }

            run.Press(Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
            var row = Row(shell, $"{OriginalShell.ContentsKey}:{preset}");
            if (row == null)
            {
                ctx.Check(false, $"contents row {preset} is on screen");
                return;
            }

            run.Press(Pointer(fit, row.X + 5f, row.Y + 5f, pressed: true, clicked: true));
            var fly = Row(shell, OriginalShell.FlyMissionKey)!;
            run.Press(Pointer(fit, fly.X + 5f, fly.Y + 5f, pressed: true, clicked: true));
            var launch = run.Expect<LaunchExit>();
            ctx.Check(launch is { Mode: MenuMode.Stunt } && launch.InstantAction?.MissionType == mission,
                $"Fly Mission leaves as one Instant Action LaunchExit with the {mission} def ({launch?.InstantAction?.MissionType})");
            run.Show(MenuReturnDestination.TopLevel);
            ctx.Check(shell.Screen == OriginalScreen.TopLevel, $"the return lands on the top level ({shell.Screen})");
        }
    }

    private static void OriginalDogfight(TestContext ctx, Run run, OriginalShell shell, BoardFit fit)
    {
        var setup = run.Host.Features.Get<PlayerSetupFeature>();
        var door = Row(shell, OriginalShell.DogfightKey);
        if (door == null)
        {
            ctx.Check(false, $"the top level carries the Dogfight door");
            return;
        }

        run.Press(Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.Dogfight, $"the door opens Dogfight ({shell.Screen})");
        run.Press(Accept);
        run.Press(Right);
        run.Press(Accept);
        var guest = new ScriptedSeat();
        ctx.Check(setup.Join(guest) != null && run.Host.Seats.Count == 2, $"a second seat joins ({run.Host.Seats.Count})");
        run.Press(guest, Down);
        run.Press(guest, Accept);
        run.Press(guest, Accept);
        ctx.Check(setup.Seats[1].Confirmed, $"the guest confirms through the presentation's poll");
        var dogfight = run.Expect<LaunchExit>();
        ctx.Check(dogfight is { Mode: MenuMode.Versus, Seats.Count: 2 },
            $"and that last confirm leaves as one Versus LaunchExit for both seats ({dogfight?.Mode}, {dogfight?.Seats.Count})");
        run.Show(MenuReturnDestination.TopLevel);
        ctx.Check(shell.Screen == OriginalScreen.TopLevel && run.Host.Seats.Count == 2 && !setup.Seats[1].Locked,
            $"the return lands on the top level with both seats kept and the picks dropped ({shell.Screen}, {run.Host.Seats.Count})");
        run.Host.RemoveSeat(guest);
    }

    private static void OriginalCampaign(TestContext ctx, Run run, OriginalShell shell, BoardFit fit, CampaignProfileStore store)
    {
        var campaign = run.Host.Features.Get<CampaignFeature>();
        var door = Row(shell, OriginalShell.CampaignKey);
        if (door == null)
        {
            ctx.Check(false, $"the top level carries Campaign");
            return;
        }

        run.Press(Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.CampaignRoster && ReferenceEquals(campaign.Store, store),
            $"the Campaign door opens the profile screen over the scratch store ({shell.Screen})");
        if (shell.RosterName != Pilot)
        {
            run.Press(new MenuCommands { Typed = Pilot });
        }

        run.Press(Accept);
        ctx.Check(shell.Screen == OriginalScreen.CampaignCabin && campaign.Profile?.Name == Pilot,
            $"Enter in the box seats the pilot on the cabin ({shell.Screen}, {campaign.Profile?.Name})");
        int seq = campaign.NextMissionSeq;
        Click(run, shell, fit, "NextMission");
        Click(run, shell, fit, "GoToFlightCheck");
        ctx.Check(shell.Screen == OriginalScreen.CampaignFlightCheck, $"NEXT MISSION then GO TO FLIGHT CHECK reach the check ({shell.Screen})");
        Click(run, shell, fit, "FlyMission");
        var mission = run.Expect<CampaignMissionExit>();
        ctx.Check(mission is { Profile: Pilot, Seats.Count: 1 } && mission.MissionSeq == seq,
            $"FLY MISSION leaves as one CampaignMissionExit for the pilot's next mission ({mission?.Profile}, seq {mission?.MissionSeq})");

        var profile = store.Load(Pilot)!;
        CampaignProgression.Record(profile, new MissionAttempt(
            seq, CampaignProgression.PrimaryObjectiveMask, 420_000, 200, 90, profile.Planes[0].Airframe, profile.Planes[0].Name));
        store.Save(profile);
        run.Show(new DebriefReturn(Pilot, seq));
        ctx.Check(shell.Screen == OriginalScreen.CampaignScrapbook && campaign.MissionSeq == seq && campaign.Profile?.MissionsCompleted == seq + 1,
            $"the debrief return opens the book on the flown mission over the saved profile ({shell.Screen}, seq {campaign.MissionSeq})");
        ctx.Check(shell.FocusedKey == "ReturnToCabin", $"with the focus on the way out ({shell.FocusedKey})");
        run.Show(new CabinReturn(Pilot));
        ctx.Check(shell.Screen == OriginalScreen.CampaignCabin && campaign.Profile?.Name == Pilot,
            $"the cabin return lands on the cabin with the pilot seated ({shell.Screen})");
        run.Show(MenuReturnDestination.TopLevel);
        ctx.Check(shell.Screen == OriginalScreen.TopLevel && !campaign.IsOpen,
            $"a top-level return from a campaign mission lands on the top level with no open campaign ({shell.Screen}, open={campaign.IsOpen})");
    }

    // A process started under Original with --menu=campaign, the player's door: the profile screen
    // over the presentation's own store, once, and the top level on the return.
    private static void OriginalPlayerDoor(TestContext ctx, MenuLayout layout, CampaignProfileStore store)
    {
        var run = new Run(ctx, CampaignAidProfiles.PlayerDoor, layout, store);
        var host = run.Host;
        try
        {
            host.Select(forceBuiltIn: false, cliOverride: "original", savedRequest: null);
            run.Show(MenuReturnDestination.TopLevel);
            var shell = (host.Active as OriginalPresentation)?.Shell;
            var campaign = host.Features.Get<CampaignFeature>();
            ctx.Check(shell is { Screen: OriginalScreen.CampaignRoster } && ReferenceEquals(campaign.Store, store),
                $"--menu=campaign opens the profile screen over the presentation's store, not the scratch aid store ({shell?.Screen})");
            run.Press(Back);
            ctx.Check(shell is { Screen: OriginalScreen.TopLevel } && !campaign.IsOpen, $"Back closes the campaign ({shell?.Screen})");
            var quit = shell == null ? null : Row(shell, "MM_B_QUIT");
            if (quit != null)
            {
                var size = ctx.Host.GetViewport().GetVisibleRect().Size;
                var fit = BoardFit.For(size.X, size.Y);
                run.Press(Pointer(fit, quit.X + 5f, quit.Y + 5f, pressed: true, clicked: true));
                ctx.Check(run.Expect<QuitExit>() != null, $"Quit leaves as one QuitExit");
                run.Show(MenuReturnDestination.TopLevel);
                ctx.Check(shell is { Screen: OriginalScreen.TopLevel } && !campaign.IsOpen,
                    $"the show after the exit is the top level, not the door again ({shell?.Screen})");
            }
        }
        finally
        {
            host.Deactivate();
        }
    }

    // Original refused at selection: Built-in runs with the request kept, its return is its own
    // top level, and a return never re-selects, so a repaired tree is seen only by a switch.
    private static void FallbackBeforeReturn(TestContext ctx, Run run)
    {
        var host = run.Host;
        host.Deactivate();
        host.Availability = id => id == PresentationId.Original ? "the layout went away" : null;
        string? reason = host.Select(forceBuiltIn: false, cliOverride: "original", savedRequest: null);
        ctx.Check(host.Selected == PresentationId.BuiltIn && host.Requested == PresentationId.Original,
            $"Original refused at selection resolves Built-in with the request kept ({host.Selected}, {host.Requested})");
        ctx.Check(reason != null && reason.Contains("went away", StringComparison.Ordinal), $"with the availability reason appended ({reason})");
        run.Show(MenuReturnDestination.TopLevel);
        var menu = (host.Active as BuiltInPresentation)?.Menu;
        ctx.Check(menu is { Visible: true, ShownScreen: "Mode" }, $"the fallback shows Built-in's own top level, no aid ({menu?.ShownScreen})");
        if (menu == null)
        {
            return;
        }

        run.Press(Accept);
        run.Press(Accept);
        run.Press(Accept);
        run.Press(Accept);
        ctx.Check(run.Expect<LaunchExit>() is { Mode: MenuMode.Free }, $"Free Flight launches under the fallback");
        host.Availability = _ => null;
        var before = host.Active;
        run.Show(MenuReturnDestination.TopLevel);
        ctx.Check(ReferenceEquals(before, host.Active) && menu.ShownScreen == "Mode" && host.Requested == PresentationId.Original,
            $"the return re-shows the same Built-in instance on Mode with the request still Original ({menu.ShownScreen}, {host.Requested})");
    }

    // Walks the Built-in cursor down to the named row, through the host's seat.
    private static void WalkTo(Run run, LaunchMenu menu, string text)
    {
        int count = menu.ShownRowCount;
        for (int i = 0; i < count && menu.ShownRowText != text; i++)
        {
            run.Press(Down);
        }
    }

    private static void Click(Run run, OriginalShell shell, BoardFit fit, string key)
    {
        if (Row(shell, key) is { } row)
        {
            run.Press(Pointer(fit, row.X + 5f, row.Y + 5f, pressed: true, clicked: true));
        }
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

    // One process's menu: a host with both presentations registered, the aid handed to the first
    // instance created and consumed by the first show, a sink recording each exit and whether the
    // host had hidden the presentation when it arrived.
    private sealed class Run
    {
        private readonly List<bool> _hidden = new();
        private string? _aid;
        private int _taken;

        public Run(TestContext ctx, string aid, MenuLayout? layout, CampaignProfileStore store)
        {
            _aid = aid;
            var registry = new PresentationRegistry();
            registry.Register(PresentationId.BuiltIn, () => new BuiltInPresentation(
                ctx.Host, ctx.ZrdrPath, ctx.DataRoot, _aid ?? string.Empty, new MenuInput { Keyboard = true }));
            if (layout != null)
            {
                registry.Register(PresentationId.Original, () => new OriginalPresentation(
                    ctx.Host, ctx.DataRoot, layout, _aid ?? string.Empty, new MenuInput { Keyboard = true })
                {
                    CampaignProfiles = store,
                });
            }

            Host = new MenuHost(registry, new MenuSuiteHost.SilentMenuAudio(), exit =>
            {
                Exits.Add(exit);
                _hidden.Add(Host is { Shown: false });
            });
            MenuSuiteHost.AddFeatures(Host, ctx.DataRoot);
            Host.AddSeat(Seat);
        }

        public MenuHost Host { get; }

        public ScriptedSeat Seat { get; } = new();

        public List<MenuExit> Exits { get; } = new();

        public bool HiddenAtEveryExit => _hidden.Count == Exits.Count && _hidden.TrueForAll(h => h);

        public void Show(MenuReturnDestination destination)
        {
            Host.Show(destination);
            _aid = null;
        }

        public void Press(MenuCommands frame) => Press(Seat, frame);

        // Seat 0's frame, or a guest's: a Built-in guest is polled by the launchscreen's own frame,
        // which the host's tick runs, and an Original guest by the presentation's poll.
        public void Press(ScriptedSeat seat, MenuCommands frame)
        {
            seat.Enqueue(frame);
            Host.Tick(Dt);
        }

        // The one exit that arrived since the last call when it is a T, else null; the caller
        // reports the absence. Frames still queued on seat 0 are dropped with the screen hidden.
        public T? Expect<T>()
            where T : MenuExit
        {
            Seat.Clear();
            if (Exits.Count != _taken + 1)
            {
                _taken = Exits.Count;
                return null;
            }

            _taken = Exits.Count;
            return Exits[^1] as T;
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
