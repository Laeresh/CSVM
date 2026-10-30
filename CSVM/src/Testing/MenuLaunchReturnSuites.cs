using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Flight.Hangar;
using CSVM.Launch;
using CSVM.Session.Campaign;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.UI.Campaign;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using CSVM.UI.Screens;
using CSVM.Utils;

namespace CSVM.Testing;

/// <summary>
/// Every launch and every return through the presentation boundary, over a real
/// <see cref="MenuHost"/> and a scratch profile store. Free Flight, Instant Action, Dogfight and a
/// campaign mission each leave as their typed exit, the host hiding the presentation. Every launch
/// names the screen it came from as the way back. A top-level return lands on the top level even
/// after a <c>--menu=</c> aid, and the debrief and cabin returns land where they name.
/// An unavailable Original stays selected with the request kept, each host standing for one process.
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

    [Suite("menu-launch-return",
        "every launch and return through the boundary over a scratch store: an Original process "
        + "started on --menu=free-flight opens on Free Flight once, Free Flight, an ace duel, a "
        + "squadron, a two-seat Dogfight and a campaign mission each leave as their typed exit with "
        + "the host hiding the screen, each launch names the screen it came from as its return, the "
        + "Instant Action one landing back on its screen over the setup that flew, every top-level "
        + "return lands on the top level, the live launcher holds each launch's own destination for "
        + "the exit press to read, the debrief and cabin returns land on the book and the cabin, and "
        + "Quit leaves by its exit; a process started on --menu=campaign opens the player's door once; "
        + "and Original refused at selection stays selected with the request kept, a return "
        + "re-showing the same presentation without re-selecting")]
    internal static void MenuLaunchReturn(TestContext ctx)
    {
        MenuSuiteHost.RequireLayout(ctx);
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

        // ⚠ The presentation opens the campaign over this store: nothing here may read or write
        // user://Profiles. Seeded with the pilot, so the cabin is one Accept away from the roster.
        var store = new CampaignProfileStore(Path.Combine(root, "Profiles"));
        store.Save(CampaignProfileDef.NewProfile(Pilot));
        var planes = MenuSuiteHost.ScratchPlanes(ctx, "menu-launch-return");
        try
        {
            OriginalProcess(ctx, layout, store, planes);
            OriginalPlayerDoor(ctx, layout, store, planes);
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

    [Suite("menu-backdrop",
        "the persistent environment's background over an Options restart and a launch, on the "
        + "launcher's own WorldEnvironment: a run that shows no menu leaves it on the sky, the "
        + "menu's first show blacks it, Original's Game Options ACCEPT leaves by the apply exit, the "
        + "frame between the apply's exit and the restart carries no presentation and stays black, "
        + "the restart stands a fresh Original up over the same "
        + "black, and a launch puts the sky back with the material the rig built still on it")]
    internal static void MenuBackdrop(TestContext ctx)
    {
        MenuSuiteHost.RequireLayout(ctx);
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        var env = LiveEnvironment(ctx);
        ctx.Check(env != null, $"the launcher's own WorldEnvironment is in the tree beside the test host");
        if (layout == null || env == null)
        {
            return;
        }

        var sky = env.Sky;
        ctx.Check(env.BackgroundMode == Godot.Environment.BGMode.Sky,
            $"a run that shows no menu leaves the sky standing, so no scripted run pays for the black ({env.BackgroundMode})");

        string root = Path.Combine(ctx.ScratchDir, "menu-backdrop");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        var store = new CampaignProfileStore(Path.Combine(root, "Profiles"));
        var run = new Run(ctx, string.Empty, layout, store, MenuSuiteHost.ScratchPlanes(ctx, "menu-backdrop"));
        try
        {
            Switch(ctx, run, env, sky);
        }
        finally
        {
            run.Host.Deactivate();
            // The environment is the process's, not this suite's: every later suite and every
            // world built after this one reads it.
            WorldBackdrop.Sky(env);
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    // The Options restart as the launcher performs it, with the background read at every step:
    // ShowMenu blacks and shows, the apply exit arrives at the end of one frame, and ApplyOptions
    // runs at the top of the next, which is the one frame that could show the procedural sky.
    private static void Switch(TestContext ctx, Run run, Godot.Environment env, Godot.Sky? sky)
    {
        var host = run.Host;
        host.Select(null);
        WorldBackdrop.Black(env);
        run.Show(MenuReturnDestination.TopLevel);
        var first = host.Active as OriginalPresentation;
        ctx.Check(first?.Shell != null && WorldBackdrop.IsBlack(env),
            $"the menu's first show leaves the background flat black ({env.BackgroundMode}, {env.BackgroundColor})");
        if (first?.Shell is not { } shell)
        {
            return;
        }

        var size = ctx.Host.GetViewport().GetVisibleRect().Size;
        var fit = BoardFit.For(size.X, size.Y);
        Click(run, shell, fit, "MM_B_PREFERENCES");
        Click(run, shell, fit, OriginalOptionsScreen.GameOptionsDoorKey);
        ctx.Check(shell.Screen == OriginalScreen.GameOptions, $"PREFERENCES and its GAME OPTIONS door reach the page ({shell.Screen})");
        Click(run, shell, fit, OriginalGameOptionsPage.AcceptKey);
        var applied = run.Expect<OptionsApplyExit>();
        ctx.Check(applied != null, $"the page's ACCEPT leaves by the options exit");
        ctx.Check(!host.Shown && WorldBackdrop.IsBlack(env),
            $"the frame the exit lands on has no presentation on screen and is still black ({env.BackgroundMode})");

        host.Deactivate();
        ctx.Check(host.Active == null && WorldBackdrop.IsBlack(env),
            $"the restart's first call frees the old instance over the same black ({env.BackgroundMode})");
        WorldBackdrop.Black(env);
        run.Show(MenuReturnDestination.TopLevel);
        ctx.Check(host.Selected == PresentationId.Original && host.Active is OriginalPresentation fresh && !ReferenceEquals(fresh, first),
            $"and the show stands a fresh Original up, the apply having changed no presentation ({host.Selected})");
        ctx.Check(WorldBackdrop.IsBlack(env), $"with the background black across every frame of the restart ({env.BackgroundMode})");

        WorldBackdrop.Sky(env);
        ctx.Check(env.BackgroundMode == Godot.Environment.BGMode.Sky && env.Sky?.GetInstanceId() == sky?.GetInstanceId(),
            $"a launch takes the sky back with the material the rig built ({env.BackgroundMode})");
    }
    // The launcher's process-lifetime environment: the WorldEnvironment its lighting rig adds
    // beside the node the suites are hosted under, which is the object the menu path blacks.
    private static Godot.Environment? LiveEnvironment(TestContext ctx)
    {
        if (ctx.Host.GetParent() is not { } launcher)
        {
            return null;
        }

        foreach (var child in launcher.GetChildren())
        {
            if (child is Godot.WorldEnvironment world)
            {
                return world.Environment;
            }
        }

        return null;
    }

    // A process started under Original with --menu=free-flight.
    private static void OriginalProcess(TestContext ctx, MenuLayout layout, CampaignProfileStore store, CustomPlaneStore planes)
    {
        var run = new Run(ctx, "free-flight", layout, store, planes);
        var host = run.Host;
        try
        {
            string? reason = host.Select("original");
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

            // Free Flight from the aid's screen: FLY on the map box's first chapter and seat 0's
            // first aircraft, as the local lobby opens.
            var fly = Row(shell, OriginalShell.FlyKey)!;
            run.Click(Pointer(fit, fly.X + 5f, fly.Y + 5f, pressed: true, clicked: true));
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
                run.Click(Pointer(fit, quit.X + 5f, quit.Y + 5f, pressed: true, clicked: true));
                ctx.Check(run.Expect<QuitExit>() != null, $"a click on Quit leaves as one QuitExit");
            }

            ctx.Check(run.HiddenAtEveryExit, $"the host had hidden the presentation before every one of the {run.Exits.Count} exits reached the sink");
            CheckLauncherHolds(ctx, run.Exits);
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

            run.Click(Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
            var row = Row(shell, $"{OriginalInstantActionScreen.ContentsKey}:{preset}");
            if (row == null)
            {
                ctx.Check(false, $"contents row {preset} is on screen");
                return;
            }

            run.Click(Pointer(fit, row.X + 5f, row.Y + 5f, pressed: true, clicked: true));
            var fly = Row(shell, OriginalInstantActionScreen.FlyMissionKey)!;
            run.Click(Pointer(fit, fly.X + 5f, fly.Y + 5f, pressed: true, clicked: true));
            var launch = run.Expect<LaunchExit>();
            ctx.Check(launch is { Mode: MenuMode.Stunt } && launch.InstantAction?.MissionType == mission,
                $"Fly Mission leaves as one Instant Action LaunchExit with the {mission} def ({launch?.InstantAction?.MissionType})");
            ctx.Check(launch != null && MenuReturnDestination.ForLaunch(launch) is InstantActionReturn,
                $"and its launch origin names the Instant Action screen as the way back ({Origin(launch)})");
            run.Show(MenuReturnDestination.InstantAction);
            var ia = run.Host.Features.Get<InstantActionFeature>();
            ctx.Check(shell.Screen == OriginalScreen.InstantAction && ia.PresetIndex == preset && ia.MissionType.Key == mission,
                $"the Instant Action return lands on the screen with the sortie's preset and mission standing ({shell.Screen}, preset {ia.PresetIndex}, {ia.MissionType.Key})");
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

        run.Click(Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.Dogfight, $"the door opens Dogfight ({shell.Screen})");
        var guest = new ScriptedSeat();
        ctx.Check(setup.Join(guest) != null && run.Host.Seats.Count == 2, $"a second seat joins ({run.Host.Seats.Count})");
        var fly = Row(shell, OriginalShell.FlyKey)!;
        run.Click(Pointer(fit, fly.X + 5f, fly.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.SeatPlane, $"FLY walks the second seat through its own aircraft screen ({shell.Screen})");
        run.Press(guest, Down);
        run.Press(guest, Accept);
        run.Press(guest, Accept);
        ctx.Check(setup.Seats[1].Confirmed, $"the guest confirms through the presentation's poll");
        var dogfight = run.Expect<LaunchExit>();
        ctx.Check(dogfight is { Mode: MenuMode.Versus, Seats.Count: 2, Match: { KillTarget: 0, TimeLimitMinutes: DogfightLobby.DefaultTimeMinutes } },
            $"and that last confirm leaves as one Versus LaunchExit for both seats on the lobby's opening rules ({dogfight?.Mode}, {dogfight?.Seats.Count}, {dogfight?.Match})");
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

        run.Click(Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.CampaignRoster && ReferenceEquals(campaign.Store, store),
            $"the Campaign door opens the profile screen over the scratch store ({shell.Screen})");
        if (shell.Campaign.RosterName != Pilot)
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
        run.Show(new DebriefReturn(Pilot, seq, MissionWon: true));
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
    private static void OriginalPlayerDoor(TestContext ctx, MenuLayout layout, CampaignProfileStore store, CustomPlaneStore planes)
    {
        var run = new Run(ctx, CampaignAidProfiles.PlayerDoor, layout, store, planes);
        var host = run.Host;
        try
        {
            host.Select("original");
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
                run.Click(Pointer(fit, quit.X + 5f, quit.Y + 5f, pressed: true, clicked: true));
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

    // Original refused at selection stays selected with the request kept and the reason said. A
    // return never re-selects, so a repaired tree is seen only by a switch.
    private static void FallbackBeforeReturn(TestContext ctx, Run run)
    {
        var host = run.Host;
        host.Deactivate();
        host.Availability = id => id == PresentationId.Original ? "the layout went away" : null;
        string? reason = host.Select("original");
        ctx.Check(host.Selected == PresentationId.Original && host.Requested == PresentationId.Original,
            $"Original refused at selection stays selected with the request kept ({host.Selected}, {host.Requested})");
        ctx.Check(reason != null && reason.Contains("went away", StringComparison.Ordinal), $"with the availability reason appended ({reason})");
        run.Show(MenuReturnDestination.TopLevel);
        var before = host.Active;
        ctx.Check(before is OriginalPresentation { Shell.Screen: OriginalScreen.TopLevel },
            $"the show stands Original up on its own top level, no aid ({host.Active?.Id})");
        host.Availability = _ => null;
        run.Show(MenuReturnDestination.TopLevel);
        ctx.Check(ReferenceEquals(before, host.Active) && host.Requested == PresentationId.Original,
            $"the return re-shows the same instance with the request still Original ({host.Requested})");
    }
    // The launcher's own half of the return: a launch writes the destination it came from and the
    // exit press reads that field back, which no exit and no presentation can show. Driven on the
    // live node the suites are hosted under, since nothing instantiates a Launcher headlessly, over
    // the exits this process really produced, with its own destination put back afterwards.
    private static void CheckLauncherHolds(TestContext ctx, IReadOnlyList<MenuExit> exits)
    {
        if (ctx.Host.GetParent() is not Launcher launcher)
        {
            ctx.Check(false, $"the live Launcher is the node the suites are hosted under");
            return;
        }

        var restore = launcher.ExitDestination;
        try
        {
            int held = 0;
            foreach (var exit in exits)
            {
                if (exit is not LaunchExit and not CampaignMissionExit)
                {
                    continue;
                }

                launcher.LaunchedFrom(exit);
                held++;
                ctx.Check(launcher.ExitDestination == MenuReturnDestination.ForLaunch(exit),
                    $"the launcher holds {Origin(exit)} from the launch that wrote it ({launcher.ExitDestination.GetType().Name})");
            }

            ctx.Check(held >= 4, $"over every launch this process made ({held})");
        }
        finally
        {
            launcher.ExitDestination = restore;
        }
    }

    // The destination a launch's own exit names, for a check's message; "no exit" where the leg
    // above recorded none, which that leg's own check has already reported.
    private static string Origin(MenuExit? exit) =>
        exit == null ? "no exit" : MenuReturnDestination.ForLaunch(exit).GetType().Name;

    private static void Click(Run run, OriginalShell shell, BoardFit fit, string key)
    {
        if (Row(shell, key) is { } row)
        {
            run.Click(Pointer(fit, row.X + 5f, row.Y + 5f, pressed: true, clicked: true));
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

    // One process's menu: a host with Original registered and the aid handed to the first instance,
    // consumed by the first show. Its sink records each exit and whether the host had hidden the
    // presentation when it arrived.
    private sealed class Run
    {
        private readonly List<bool> _hidden = new();
        private string? _aid;
        private int _taken;

        public Run(TestContext ctx, string aid, MenuLayout layout, CampaignProfileStore store, CustomPlaneStore planes)
        {
            _aid = aid;
            var registry = new PresentationRegistry();
            registry.Register(PresentationId.Original, () => new OriginalPresentation(
                ctx.Host, ctx.DataRoot, layout, _aid ?? string.Empty, new MenuInput { Keyboard = true }, data: ctx.Data)
            {
                CampaignProfiles = store,
                Planes = planes,
            });

            Host = new MenuHost(registry, new MenuSuiteHost.SilentMenuAudio(), exit =>
            {
                Exits.Add(exit);
                _hidden.Add(Host is { Shown: false });
            });
            MenuSuiteHost.AddFeatures(Host, ctx.DataRoot, ctx.Data);
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

        // One click as the Original shell reads it: the press arms the row it lands on and the
        // release still on that row is what fires, so a click is two frames rather than one.
        public void Click(MenuCommands frame)
        {
            Press(frame);
            Press(frame with { Pointer = frame.Pointer!.Value with { Pressed = false, Clicked = false } });
        }

        // Seat 0's frame, or a guest's, polled by the presentation inside the host's tick.
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
