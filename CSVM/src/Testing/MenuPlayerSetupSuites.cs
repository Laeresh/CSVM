using System.Collections.Generic;
using CSVM.UI;
using CSVM.UI.Menu;

namespace CSVM.Testing;

/// <summary>
/// The shared player setup in both presentations. The first suite characterizes Built-in: a real
/// <see cref="LaunchMenu"/> is driven through the aircraft screen with one to four seats (the
/// extra ones through <see cref="LaunchMenu.DebugJoin"/>, which adds device-less seats), and the
/// read-outs pin the two-stage pick, Back at every stage, the launch gate under Free Flight and
/// Dogfight, the join hint at each seat count, and what a return from flight keeps. The second
/// joins scripted seats through the feature and drives them in Built-in and in Original.
/// </summary>
internal static class MenuPlayerSetupSuites
{
    private const float Dt = 1f / 60f;

    private static readonly MenuCommands Accept = new() { Accept = true };
    private static readonly MenuCommands Back = new() { Back = true };
    private static readonly MenuCommands Loadout = new() { Loadout = true };
    private static readonly MenuCommands Down = new() { MoveY = 1 };
    private static readonly MenuCommands Up = new() { MoveY = -1 };
    private static readonly MenuCommands Right = new() { MoveX = 1 };

    [Suite("menu-player-setup-journey",
        "Built-in's player setup pinned on the real launchscreen: a lone seat's two-stage pick with "
        + "Back at every stage and the launch it ends in, a second device-less seat splitting the "
        + "screen and holding the gate until it confirms, player 1's Back unselecting everyone, the "
        + "Dogfight gate waiting with its own hint for a second seat, the four-seat maximum, the "
        + "selected and loadout aids under two seats, and a return from flight keeping the seats "
        + "and dropping the picks")]
    internal static void MenuPlayerSetupJourney(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var exits = new List<MenuExit>();
        var host = MenuSuiteHost.Bare(exits, out var seat);
        var menu = LaunchMenu.Build(ctx.ZrdrPath, ctx.DataRoot, host, seat.Input);
        ctx.Host.AddChild(menu);
        var launches = new Exits<LaunchExit>(exits);
        try
        {
            LoneSeat(ctx, menu, launches);
            SecondSeat(ctx, menu, launches);
            DogfightGate(ctx, menu, launches);
            FourSeats(ctx, menu);
            AidsUnderTwoSeats(ctx, menu);
        }
        finally
        {
            ctx.Host.RemoveChild(menu);
            menu.QueueFree();
        }
    }

    // One seat: select, weapons, Back at each stage, then the launch, and the return that keeps
    // the seat and drops every stage of its pick.
    private static void LoneSeat(TestContext ctx, LaunchMenu menu, Exits<LaunchExit> launches)
    {
        menu.ShowMenu();
        menu.Drive(Accept);
        menu.Drive(Accept);
        Is(ctx, "Free Flight, first chapter, lands on the aircraft screen", "Plane", menu.ShownScreen);
        Is(ctx, "a lone seat's heading", "SELECT AIRCRAFT", menu.ShownHeading);
        Has(ctx, "joining is open on this screen", "START", menu.ShownJoinHint);
        Has(ctx, "the footer offers the first Select", "Enter / A  Select", menu.ShownFooter);

        menu.Drive(Down);
        Is(ctx, "one row down", "Hellhound", menu.ShownRowText);
        menu.Drive(Loadout);
        Is(ctx, "Weapons while browsing does nothing", "SELECT AIRCRAFT", menu.ShownHeading);
        menu.Drive(Accept);
        Is(ctx, "the first Accept selects", "AIRCRAFT SELECTED", menu.ShownHeading);
        Has(ctx, "and the footer offers FLY", "Enter / A  FLY", menu.ShownFooter);
        menu.Drive(Loadout);
        Is(ctx, "Weapons on a selected airframe opens its list", "AMMO SELECTION  (Hellhound)", menu.ShownHeading);
        menu.Drive(Loadout);
        Is(ctx, "Weapons again closes it with the selection standing", "AIRCRAFT SELECTED", menu.ShownHeading);
        menu.Drive(Back);
        Is(ctx, "Back unselects", "SELECT AIRCRAFT", menu.ShownHeading);
        Is(ctx, "keeping the cursor", "Hellhound", menu.ShownRowText);
        ctx.Check(launches.Count == 0, $"nothing has launched ({launches.Count})");

        menu.Drive(Accept);
        menu.Drive(Accept);
        ctx.Check(launches.Count == 1, $"select then confirm launches a lone seat once ({launches.Count})");
        if (launches.Count == 1)
        {
            var launch = launches[0];
            ctx.Check(launch.Mode == MenuMode.Free && launch.Seats.Count == 1 && launch.Seats[0].PlaneNode == "player_avenger",
                $"as one Free Flight seat flying the Hellhound ({launch.Mode}, {launch.Seats.Count}, {(launch.Seats.Count > 0 ? launch.Seats[0].PlaneNode : "")})");
            ctx.Check(launch.Seats[0].Pads.Count == 0 && launch.Seats[0].Fit == null,
                $"with no pad and the stock fit ({launch.Seats[0].Pads.Count}, {launch.Seats[0].Fit})");
        }

        menu.HideMenu();
        menu.ShowMenu("plane");
        Is(ctx, "a return re-enters the aircraft screen browsing", "SELECT AIRCRAFT", menu.ShownHeading);
        Is(ctx, "with the cursor kept", "Hellhound", menu.ShownRowText);
        ctx.Check(launches.Count == 1, $"and nothing relaunched ({launches.Count})");
    }

    // A second, device-less seat: the screen splits, the gate waits for it, player 1's Back
    // walks back through the stages and finally takes everyone to the Chapter screen.
    private static void SecondSeat(TestContext ctx, LaunchMenu menu, Exits<LaunchExit> launches)
    {
        menu.DebugJoin(1);
        Is(ctx, "a second seat splits the aircraft screen", "SELECT AIRCRAFT — ALL PLAYERS", menu.ShownHeading);
        Has(ctx, "the footer says who steers the shared screens", "(P1 chooses)", menu.ShownFooter);
        Has(ctx, "joining stays open below four seats", "START", menu.ShownJoinHint);
        menu.Drive(Accept);
        menu.Drive(Accept);
        ctx.Check(launches.Count == 1,
            $"player 1 selecting and confirming does not launch while the other seat has not confirmed ({launches.Count})");
        menu.Drive(Back);
        menu.Drive(Back);
        menu.Drive(Accept);
        menu.Drive(Accept);
        ctx.Check(launches.Count == 1, $"Back twice then select and confirm again still waits ({launches.Count})");
        menu.Drive(Back);
        menu.Drive(Back);
        menu.Drive(Back);
        Is(ctx, "player 1's third Back, browsing, returns everyone to the Chapter screen", "Chapter", menu.ShownScreen);
        menu.Drive(Accept);
        Is(ctx, "and the aircraft screen is still split", "SELECT AIRCRAFT — ALL PLAYERS", menu.ShownHeading);
        menu.Drive(Accept);
        menu.Drive(Accept);
        ctx.Check(launches.Count == 1, $"the other seat's pick was unselected too, so the gate still waits ({launches.Count})");
        menu.Drive(Back);
        menu.Drive(Back);
    }

    // Dogfight: a lone seat waits with the hint naming what is missing; a second seat lifts the
    // hint but must confirm before anything flies.
    private static void DogfightGate(TestContext ctx, LaunchMenu menu, Exits<LaunchExit> launches)
    {
        menu.ShowMenu();
        menu.Drive(Down);
        menu.Drive(Down);
        Is(ctx, "two rows down the Mode screen", "Dogfight", menu.ShownRowText);
        menu.Drive(Accept);
        menu.Drive(Accept);
        Is(ctx, "Dogfight reaches the aircraft screen", "Plane", menu.ShownScreen);
        Is(ctx, "the second seat survived the trip to Mode and back", "SELECT AIRCRAFT — ALL PLAYERS", menu.ShownHeading);
        ctx.Check(menu.ShownJoinHint != "(Dogfight needs a fight — P2: press START to join)",
            $"two seats satisfy Dogfight's count, so the hint is the ordinary one ({menu.ShownJoinHint})");
        menu.Drive(Accept);
        menu.Drive(Accept);
        ctx.Check(launches.Count == 1, $"player 1 alone cannot fly a Dogfight while the second seat has not confirmed ({launches.Count})");
        menu.Drive(Back);
        menu.Drive(Back);

        // Rebuilt on one seat: Deactivate is the only thing that drops a seat, so a fresh
        // launchscreen stands in for it here.
        var lone = MenuSuiteHost.Menu(ctx);
        ctx.Host.AddChild(lone);
        try
        {
            lone.ShowMenu();
            lone.Drive(Down);
            lone.Drive(Down);
            lone.Drive(Accept);
            lone.Drive(Accept);
            Is(ctx, "a lone Dogfight seat waits with the hint naming the missing seat",
                "(Dogfight needs a fight — P2: press START to join)", lone.ShownJoinHint);
            Is(ctx, "on the lone-seat layout", "SELECT AIRCRAFT", lone.ShownHeading);
            lone.Drive(Accept);
            Is(ctx, "it can still select", "AIRCRAFT SELECTED", lone.ShownHeading);
            lone.Drive(Accept);
            Is(ctx, "and confirming leaves it standing on the screen, no launch", "Plane", lone.ShownScreen);
            Is(ctx, "with the heading unchanged", "AIRCRAFT SELECTED", lone.ShownHeading);
        }
        finally
        {
            ctx.Host.RemoveChild(lone);
            lone.QueueFree();
        }
    }

    private static void FourSeats(TestContext ctx, LaunchMenu menu)
    {
        menu.DebugJoin(2);
        Is(ctx, "four seats close the join", "(4-player maximum)", menu.ShownJoinHint);
        menu.DebugJoin(1);
        Is(ctx, "a fifth is refused", "(4-player maximum)", menu.ShownJoinHint);
        menu.ShowMenu();
        Is(ctx, "off the aircraft screen the maximum still reads", "(4-player maximum)", menu.ShownJoinHint);
    }

    // The aids that press for the reader do so for player 1 only; the seats stay.
    private static void AidsUnderTwoSeats(TestContext ctx, LaunchMenu menu)
    {
        menu.ShowMenu("selected");
        Is(ctx, "--menu=selected with seats joined keeps the split heading", "SELECT AIRCRAFT — ALL PLAYERS", menu.ShownHeading);
        Is(ctx, "the seats survive an aid's re-entry", "(4-player maximum)", menu.ShownJoinHint);
        menu.ShowMenu("loadout");
        Is(ctx, "--menu=loadout likewise", "SELECT AIRCRAFT — ALL PLAYERS", menu.ShownHeading);
        menu.ShowMenu();
        Is(ctx, "and a plain re-entry lands on Mode", "Mode", menu.ShownScreen);
    }

    [Suite("menu-player-setup-seats",
        "Scripted seats through the shared player setup in both presentations: in Built-in a "
        + "second scripted seat joins on the real launchscreen, picks and confirms, and Free "
        + "Flight and Dogfight launch for both seats, a return keeps the seats and drops the picks, "
        + "a guest's Back unjoins, four seats close the join and a fifth is refused, a lock and an "
        + "unjoin land on one frame, and Deactivate discards every seat but the first; in Original "
        + "the Dogfight door opens the Dogfight screen, FLY waits for a second seat with the hint "
        + "naming it, a joined seat walks, selects and confirms on the aircraft column, FLY leaves "
        + "as a Dogfight launch for both seats, Free Flight launches both too, and a guest's Back "
        + "unjoins from the top level")]
    internal static void MenuPlayerSetupSeats(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        BuiltInSeats(ctx);
        OriginalSeats(ctx);
    }

    // Built-in: later seats are scripted sources joined through the feature, their frames read by
    // the launchscreen's own frame (_Process, as the presentation's Tick runs it); player 1 drives.
    private static void BuiltInSeats(TestContext ctx)
    {
        var exits = new List<MenuExit>();
        var host = MenuSuiteHost.Bare(exits, out var seat);
        var setup = host.Features.Get<PlayerSetupFeature>();
        var menu = LaunchMenu.Build(ctx.ZrdrPath, ctx.DataRoot, host, seat.Input);
        ctx.Host.AddChild(menu);
        var launches = new Exits<LaunchExit>(exits);
        try
        {
            menu.ShowMenu();
            menu.Drive(Accept);
            menu.Drive(Accept);
            var s2 = new ScriptedSeat();
            ctx.Check(setup.Join(s2) != null && host.Seats.Count == 2,
                $"a second scripted seat joins through the feature and the host's live seat list grows ({host.Seats.Count})");
            Frame(menu, s2, MenuCommands.None);
            Is(ctx, "the launchscreen picks the seat up on its next frame", "SELECT AIRCRAFT — ALL PLAYERS", menu.ShownHeading);
            Frame(menu, s2, Down);
            Frame(menu, s2, Accept);
            Frame(menu, s2, Accept);
            ctx.Check(setup.Seats[1].Confirmed && setup.Seats[1].Cursor == 1,
                $"the seat's frames walk one row down, select and confirm (confirmed={setup.Seats[1].Confirmed}, row {setup.Seats[1].Cursor})");
            ctx.Check(launches.Count == 0, $"nothing launches on the guest's confirmation alone ({launches.Count})");
            menu.Drive(Accept);
            menu.Drive(Accept);
            ctx.Check(launches.Count == 1, $"player 1 selecting and confirming then launches ({launches.Count})");
            if (launches.Count == 1)
            {
                var launch = launches[0];
                ctx.Check(launch.Mode == MenuMode.Free && launch.Seats.Count == 2,
                    $"a Free Flight launch for both seats ({launch.Mode}, {launch.Seats.Count})");
                ctx.Check(launch.Seats.Count == 2 && launch.Seats[1].PlaneNode == "player_avenger" && launch.Seats[1].Pads.Count == 0,
                    $"the guest flies the Hellhound with no pad behind a scripted source ({(launch.Seats.Count > 1 ? launch.Seats[1].PlaneNode : "")})");
            }

            menu.HideMenu();
            menu.ShowMenu();
            ctx.Check(host.Seats.Count == 2 && !setup.Seats[1].Locked && !setup.Seats[1].Confirmed,
                $"a return keeps the seats and drops every stage of their picks ({host.Seats.Count}, locked={setup.Seats[1].Locked})");

            menu.Drive(Down);
            menu.Drive(Down);
            menu.Drive(Accept);
            menu.Drive(Accept);
            Is(ctx, "Dogfight with two seats reaches the split aircraft screen", "SELECT AIRCRAFT — ALL PLAYERS", menu.ShownHeading);
            Frame(menu, s2, Accept);
            Frame(menu, s2, Accept);
            menu.Drive(Accept);
            menu.Drive(Accept);
            ctx.Check(launches.Count == 2 && launches[1].Mode == MenuMode.Versus && launches[1].Seats.Count == 2,
                $"both confirmed, a Dogfight launches for the two seats ({launches.Count})");

            menu.HideMenu();
            menu.ShowMenu("plane");
            var s3 = new ScriptedSeat();
            setup.Join(s3);
            Frame(menu, s3, Back);
            ctx.Check(host.Seats.Count == 2, $"a guest's Back while browsing unjoins it ({host.Seats.Count})");
            var s4 = new ScriptedSeat();
            setup.Join(s3);
            setup.Join(s4);
            Frame(menu, s4, MenuCommands.None);
            Is(ctx, "four seats close the join", "(4-player maximum)", menu.ShownJoinHint);
            ctx.Check(setup.Join(new ScriptedSeat()) == null, $"and a fifth is refused");
            ctx.Check(setup.Join(s2) == null, $"as is a source already seated");

            // One frame: seat 2 selects while seat 4 leaves. Both land, in seat order.
            s2.Enqueue(Accept);
            s4.Enqueue(Back);
            menu._Process(Dt);
            ctx.Check(host.Seats.Count == 3 && setup.Seats[1].Locked,
                $"a lock and an unjoin on the same frame both land ({host.Seats.Count}, locked={setup.Seats[1].Locked})");

            host.Deactivate();
            ctx.Check(host.Seats.Count == 1 && !setup.Seats[0].Locked && setup.Seats[0].Cursor == 0,
                $"Deactivate discards every seat but the first and its pick ({host.Seats.Count})");
        }
        finally
        {
            ctx.Host.RemoveChild(menu);
            menu.QueueFree();
        }
    }

    // Original: seat 0 is a scripted keyboard, a second scripted seat joins through the feature
    // and is polled by the presentation's Tick like a pad would be.
    private static void OriginalSeats(TestContext ctx)
    {
        if (!System.IO.File.Exists(MenuLayout.PathUnder(ctx.DataRoot)))
        {
            ctx.Note($"Original seats not checked: no decoded menu layout under {ctx.DataRoot}");
            return;
        }

        var layout = CSVM.UI.Menu.Original.OriginalAvailability.Load(ctx.DataRoot, out var why);
        if (layout == null)
        {
            ctx.Note($"Original seats not checked: {why}");
            return;
        }

        var exits = new List<MenuExit>();
        var seat0 = new ScriptedSeat();
        var registry = new PresentationRegistry();
        registry.Register(PresentationId.BuiltIn, () => new CSVM.UI.Menu.BuiltIn.BuiltInPresentation(
            ctx.Host, ctx.ZrdrPath, ctx.DataRoot, string.Empty, new MenuInput { Keyboard = true }));
        registry.Register(PresentationId.Original, () => new CSVM.UI.Menu.Original.OriginalPresentation(
            ctx.Host, ctx.DataRoot, layout, string.Empty, new MenuInput { Keyboard = true }));
        var host = new MenuHost(registry, new MenuSuiteHost.SilentMenuAudio(), exits.Add);
        host.Features.Add(new FreeFlightFeature());
        host.Features.Add(new PlayerSetupFeature());
        host.AddSeat(seat0);
        var setup = host.Features.Get<PlayerSetupFeature>();
        try
        {
            host.Select(forceBuiltIn: false, cliOverride: "original", savedRequest: null);
            host.Show(MenuReturnDestination.TopLevel);
            var shell = (host.Active as CSVM.UI.Menu.Original.OriginalPresentation)?.Shell;
            ctx.Check(shell != null, $"Original shows with its shell built");
            if (shell == null)
            {
                return;
            }

            Press(host, seat0, Down);
            Press(host, seat0, Accept);
            ctx.Check(shell.Screen == CSVM.UI.Menu.Original.OriginalScreen.Dogfight,
                $"Down from the Free Flight door and Accept opens the Dogfight screen ({shell.Screen})");
            Press(host, seat0, Accept);
            Press(host, seat0, Right);
            Press(host, seat0, Accept);
            ctx.Check(shell.PickedDogfightChapter == "C1" && shell.PickedAirframe == "player_autogyro",
                $"seat 0 picks the first chapter and the first airframe ({shell.PickedDogfightChapter}, {shell.PickedAirframe})");
            var fly = Row(shell, CSVM.UI.Menu.Original.OriginalShell.FlyKey);
            ctx.Check(fly is { Enabled: false }, $"FLY stands disabled with one seat in Dogfight");
            Press(host, seat0, Up);
            ctx.Check(shell.FocusedKey != CSVM.UI.Menu.Original.OriginalShell.FlyKey, $"and Up wraps past it ({shell.FocusedKey})");
            Press(host, seat0, Down);
            ctx.Check(HasLine(shell, "Dogfight needs a second seat"), $"the hint names the missing seat");

            var s2 = new ScriptedSeat();
            ctx.Check(setup.Join(s2) != null && host.Seats.Count == 2, $"a second seat joins ({host.Seats.Count})");
            Press(host, s2, Down);
            Press(host, s2, Accept);
            ctx.Check(setup.Seats[1].Locked && setup.Seats[1].Cursor == 1 && HasLine(shell, "P2 ✓"),
                $"the seat's frames reach the shell through the presentation: one row down and selected, tagged on the row");
            ctx.Check(Row(shell, CSVM.UI.Menu.Original.OriginalShell.FlyKey) is { Enabled: false },
                $"FLY still waits while the second seat has not confirmed");
            Press(host, s2, Accept);
            ctx.Check(setup.Seats[1].Confirmed, $"Accept again confirms it");
            Press(host, seat0, Up);
            ctx.Check(shell.FocusedKey == CSVM.UI.Menu.Original.OriginalShell.FlyKey, $"Up from the first airframe now wraps onto the live FLY ({shell.FocusedKey})");
            Press(host, seat0, Accept);
            ctx.Check(exits.Count == 1 && exits[0] is LaunchExit { Mode: MenuMode.Versus, Seats.Count: 2 },
                $"FLY leaves as one Dogfight LaunchExit for both seats ({exits.Count}, {(exits.Count > 0 ? exits[0].GetType().Name : "")})");
            if (exits.Count == 1 && exits[0] is LaunchExit dogfight)
            {
                ctx.Check(dogfight.Chapter == "C1" && dogfight.Seats[0].PlaneNode == "player_autogyro" && dogfight.Seats[1].PlaneNode == "player_avenger",
                    $"carrying the chapter and each seat's airframe ({dogfight.Chapter}, {dogfight.Seats[0].PlaneNode}, {dogfight.Seats[1].PlaneNode})");
            }

            host.Show(MenuReturnDestination.TopLevel);
            ctx.Check(host.Seats.Count == 2 && shell.PickedAirframe == null && !setup.Seats[1].Locked,
                $"a return keeps both seats and drops every pick ({host.Seats.Count})");
            Press(host, seat0, Up);
            Press(host, seat0, Accept);
            ctx.Check(shell.Screen == CSVM.UI.Menu.Original.OriginalScreen.FreeFlight, $"Up onto the Free Flight door and Accept opens Free Flight ({shell.Screen})");
            Press(host, seat0, Accept);
            Press(host, seat0, Right);
            Press(host, seat0, Accept);
            Press(host, s2, Accept);
            Press(host, s2, Accept);
            Press(host, seat0, Up);
            Press(host, seat0, Accept);
            ctx.Check(exits.Count == 2 && exits[1] is LaunchExit { Mode: MenuMode.Free, Seats.Count: 2 },
                $"Free Flight launches both seats through its feature ({exits.Count})");

            host.Show(MenuReturnDestination.TopLevel);
            var s3 = new ScriptedSeat();
            setup.Join(s3);
            Press(host, s3, Back);
            ctx.Check(host.Seats.Count == 2, $"a guest's Back on the top level unjoins it ({host.Seats.Count})");

            host.Deactivate();
            ctx.Check(host.Seats.Count == 1 && host.Features.Get<FreeFlightFeature>().Chapter == null,
                $"Deactivate discards every seat but the first and the unfinished picks ({host.Seats.Count})");
        }
        finally
        {
            host.Deactivate();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
        }
    }

    private static CSVM.UI.Menu.Original.OriginalRow? Row(CSVM.UI.Menu.Original.OriginalShell shell, string key)
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

    private static bool HasLine(CSVM.UI.Menu.Original.OriginalShell shell, string text)
    {
        foreach (var line in shell.Compose().Lines)
        {
            if (line.Text.Contains(text, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // One launchscreen frame with one guest's commands queued; player 1 reads idle.
    private static void Frame(LaunchMenu menu, ScriptedSeat guest, MenuCommands frame)
    {
        guest.Enqueue(frame);
        menu._Process(Dt);
    }

    private static void Press(MenuHost host, ScriptedSeat seat, MenuCommands frame)
    {
        seat.Enqueue(frame);
        host.Tick(Dt);
    }

    private static void Is(TestContext ctx, string what, string expected, string actual) =>
        ctx.Check(expected == actual, $"{what}: expected '{expected}', got '{actual}'");

    private static void Has(TestContext ctx, string what, string expected, string actual) =>
        ctx.Check(actual.Contains(expected, System.StringComparison.Ordinal),
            $"{what}: expected '{expected}' in '{actual}'");

    // A seat fed from a queue of frames; an empty queue reads idle.
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

    // The exits of one kind among everything the host's sink recorded, read live.
    private sealed class Exits<T>
        where T : MenuExit
    {
        private readonly List<MenuExit> _all;

        public Exits(List<MenuExit> all)
        {
            _all = all;
        }

        public int Count
        {
            get
            {
                int n = 0;
                foreach (var exit in _all)
                {
                    if (exit is T)
                    {
                        n++;
                    }
                }

                return n;
            }
        }

        public T this[int index]
        {
            get
            {
                int seen = 0;
                foreach (var exit in _all)
                {
                    if (exit is T match && seen++ == index)
                    {
                        return match;
                    }
                }

                throw new System.ArgumentOutOfRangeException(nameof(index));
            }
        }
    }
}
