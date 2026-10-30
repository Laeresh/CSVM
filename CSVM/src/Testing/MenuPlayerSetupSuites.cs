using System.Collections.Generic;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.UI.Menu;

namespace CSVM.Testing;

/// <summary>
/// The shared player setup driven through Original. Scripted seats join through the feature and
/// are polled by the presentation. Each joined seat picks on its own aircraft screen, and the last
/// confirm is the launch.
/// </summary>
internal static class MenuPlayerSetupSuites
{
    private const float Dt = 1f / 60f;

    private static readonly MenuCommands Accept = new() { Accept = true };
    private static readonly MenuCommands Back = new() { Back = true };
    private static readonly MenuCommands Down = new() { MoveY = 1 };
    private static readonly MenuCommands Up = new() { MoveY = -1 };
    private static readonly MenuCommands Right = new() { MoveX = 1 };
    private static readonly MenuCommands Left = new() { MoveX = -1 };

    [Suite("menu-player-setup-seats",
        "Scripted seats through the shared player setup in Original: the Dogfight door opens the "
        + "local lobby on the network lobby's opening rules, FLY waits for a second seat with the hint "
        + "naming it, with a seat joined FLY walks it through its own aircraft screen where seat 0's "
        + "cursor and buttons move nothing, Back leaves the walk with the seat kept, FLY again reopens "
        + "it at the same seat, CANCEL SELECTIONS and Back over a closed list each drop the selection "
        + "without leaving, a selected seat's WEAPON LOADOUT opens the decoded ammo chrome on its own "
        + "fit and airframe with the walk and its own reach kept there, CANCEL LOADOUT restores the "
        + "picks and ACCEPT keeps them, two Accepts select and confirm, and that last confirm is the "
        + "Dogfight launch for both seats carrying seat 1's own loadout, seat 0's stock fit and the "
        + "opening rules; Free Flight greys the rules, takes a map off its list and launches both "
        + "seats the same way, and a guest's Back unjoins from the top level")]
    internal static void MenuPlayerSetupSeats(TestContext ctx) => OriginalSeats(ctx);
    // Original: seat 0 is a scripted keyboard, a second scripted seat joins through the feature
    // and is polled by the presentation's Tick like a pad would be.
    private static void OriginalSeats(TestContext ctx)
    {
        MenuSuiteHost.RequireLayout(ctx);
        var layout = CSVM.UI.Menu.Original.OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        var exits = new List<MenuExit>();
        var seat0 = new ScriptedSeat();
        var registry = new PresentationRegistry();
        var planes = MenuSuiteHost.ScratchPlanes(ctx, "menu-player-setup-seats");
        registry.Register(PresentationId.Original, () => new CSVM.UI.Menu.Original.OriginalPresentation(
            ctx.Host, ctx.DataRoot, layout, string.Empty, new MenuInput { Keyboard = true })
        {
            Planes = planes,
        });
        var host = new MenuHost(registry, new MenuSuiteHost.SilentMenuAudio(), exits.Add);
        MenuSuiteHost.AddFeatures(host, ctx.DataRoot, ctx.Data);
        host.AddSeat(seat0);
        var setup = host.Features.Get<PlayerSetupFeature>();
        const string Fly = CSVM.UI.Menu.Original.OriginalShell.FlyKey;
        try
        {
            host.Select("original");
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
            ctx.Check(Row(shell, CSVM.UI.Menu.Original.OriginalLobbyScreen.TimeKey) is { Label: "10", Enabled: true }
                && Row(shell, CSVM.UI.Menu.Original.OriginalLobbyScreen.ScoreKey) is { Label: "40", Enabled: false },
                $"its rules open on the network lobby's: 10 minutes armed, the Score box at 40");
            ctx.Check(Row(shell, Fly) is { Enabled: false }, $"FLY stands disabled with one seat in Dogfight");
            ctx.Check(HasLine(shell, "Dogfight needs a second seat"), $"the hint names the missing seat");

            var s2 = new ScriptedSeat();
            ctx.Check(setup.Join(s2) != null && host.Seats.Count == 2, $"a second seat joins ({host.Seats.Count})");
            host.Tick(Dt);
            ctx.Check(shell.Screen == CSVM.UI.Menu.Original.OriginalScreen.Dogfight && Row(shell, Fly) is { Enabled: true },
                $"the join opens no screen of its own and FLY stands ({shell.Screen})");
            ctx.Check(WalkTo(host, seat0, shell, Fly), $"seat 0's cursor reaches FLY ({shell.FocusedKey})");
            Press(host, seat0, Accept);
            ctx.Check(shell.Screen == CSVM.UI.Menu.Original.OriginalScreen.SeatPlane && shell.PickingSeat == 1
                && shell.SeatReturn == CSVM.UI.Menu.Original.OriginalScreen.Dogfight && exits.Count == 0,
                $"FLY opens the second seat's own aircraft screen and launches nothing ({shell.Screen}, picking {shell.PickingSeat})");
            Press(host, seat0, Down);
            Press(host, seat0, Accept);
            Press(host, seat0, Back);
            ctx.Check(!setup.Seats[1].Locked && setup.Seats[1].Cursor == 0
                && shell.Screen == CSVM.UI.Menu.Original.OriginalScreen.SeatPlane && shell.PickingSeat == 1,
                $"seat 0's cursor, Accept and Back move nothing on seat 1's screen ({shell.Screen}, picking {shell.PickingSeat}, row {setup.Seats[1].Cursor}, locked={setup.Seats[1].Locked})");
            Press(host, s2, Back);
            ctx.Check(host.Seats.Count == 2 && shell.Screen == CSVM.UI.Menu.Original.OriginalScreen.Dogfight && exits.Count == 0,
                $"its Back there keeps the seat and returns to the Dogfight screen ({host.Seats.Count}, {shell.Screen})");
            WalkTo(host, seat0, shell, Fly);
            Press(host, seat0, Accept);
            ctx.Check(shell.Screen == CSVM.UI.Menu.Original.OriginalScreen.SeatPlane && shell.PickingSeat == 1,
                $"and FLY again reopens the walk at the same seat ({shell.Screen}, picking {shell.PickingSeat})");
            Press(host, s2, Down);
            Press(host, s2, Accept);
            ctx.Check(setup.Seats[1].Locked && setup.Seats[1].Cursor == 1,
                $"the seat's frames reach the shell through the presentation: one row down and selected ({setup.Seats[1].Cursor})");
            // By key, not by a count of Downs: a selection also raises the WEAPON LOADOUT row, so
            // the plaques a step lands on depend on the seat's stage.
            ctx.Check(WalkTo(host, s2, shell, nameof(CSVM.UI.Boards.BoardButton.CancelSelections)),
                $"the seat's cursor reaches CANCEL SELECTIONS ({shell.FocusedKey})");
            Press(host, s2, Accept);
            ctx.Check(host.Seats.Count == 2 && !setup.Seats[1].Locked
                && shell.Screen == CSVM.UI.Menu.Original.OriginalScreen.SeatPlane,
                $"CANCEL SELECTIONS drops the selection and leaves the seat on its screen ({host.Seats.Count}, locked={setup.Seats[1].Locked})");
            Press(host, s2, Accept);
            Press(host, s2, Back);
            ctx.Check(host.Seats.Count == 2 && !setup.Seats[1].Locked
                && shell.Screen == CSVM.UI.Menu.Original.OriginalScreen.SeatPlane,
                $"as does Back over the closed list, the reopened list standing on the same row ({shell.Screen}, locked={setup.Seats[1].Locked})");
            Press(host, s2, Accept);
            ctx.Check(setup.Seats[1].Cursor == 1, $"so Accept selects that row again ({setup.Seats[1].Cursor})");
            SeatLoadout(ctx, host, seat0, s2, shell, setup);
            Press(host, s2, Accept);
            ctx.Check(setup.Seats[1].Confirmed && shell.Screen == CSVM.UI.Menu.Original.OriginalScreen.Dogfight,
                $"Accept again confirms it and the Dogfight screen returns ({shell.Screen})");
            ctx.Check(exits.Count == 1 && exits[0] is LaunchExit { Mode: MenuMode.Versus, Seats.Count: 2 },
                $"and that last confirm is the launch, one Dogfight LaunchExit for both seats ({exits.Count}, {(exits.Count > 0 ? exits[0].GetType().Name : "")})");
            if (exits.Count == 1 && exits[0] is LaunchExit dogfight)
            {
                ctx.Check(dogfight.Chapter == "C1" && dogfight.Seats[0].PlaneNode == "player_autogyro" && dogfight.Seats[1].PlaneNode == "player_avenger",
                    $"carrying the map box's first chapter and each seat's airframe ({dogfight.Chapter}, {dogfight.Seats[0].PlaneNode}, {dogfight.Seats[1].PlaneNode})");
                ctx.Check(dogfight.Seats[0].Fit == null && ReferenceEquals(dogfight.Seats[1].Fit, setup.Seats[1].Fit),
                    $"and seat 1's own loadout while seat 0, having picked none, flies stock ({dogfight.Seats[1].Fit != null})");
                ctx.Check(dogfight.Match is { KillTarget: 0, TimeLimitMinutes: 10, Lives: 0, AutoRespawn: true },
                    $"on the lobby's opening rules, ten minutes and no score limit ({dogfight.Match})");
            }

            host.Show(MenuReturnDestination.TopLevel);
            ctx.Check(host.Seats.Count == 2 && !setup.Seats[0].Confirmed && !setup.Seats[1].Locked,
                $"a return keeps both seats and drops every pick ({host.Seats.Count})");
            Press(host, seat0, Up);
            Press(host, seat0, Accept);
            ctx.Check(shell.Screen == CSVM.UI.Menu.Original.OriginalScreen.FreeFlight, $"Up onto the Free Flight door and Accept opens Free Flight ({shell.Screen}, {shell.FocusedKey})");
            ctx.Check(Row(shell, CSVM.UI.Menu.Original.OriginalLobbyScreen.TimeRadioKey) is { Enabled: false }
                && Row(shell, CSVM.UI.Menu.Original.OriginalLobbyScreen.AutoRespawnKey) is { Enabled: false },
                $"whose rule rows stand greyed");
            WalkTo(host, seat0, shell, CSVM.UI.Menu.Original.OriginalLobbyScreen.EnvironmentKey);
            Press(host, seat0, Accept);
            Press(host, seat0, Down);
            Press(host, seat0, Accept);
            ctx.Check(shell.LocalLobby.PickedChapter(CSVM.UI.Menu.Original.OriginalScreen.FreeFlight) == "C1B" && host.Features.Get<FreeFlightFeature>().Chapter?.Code == "C1B",
                $"Accept opens the map list and a row down picks the second map ({shell.LocalLobby.PickedChapter(CSVM.UI.Menu.Original.OriginalScreen.FreeFlight)})");
            WalkTo(host, seat0, shell, Fly);
            Press(host, seat0, Accept);
            Press(host, s2, Accept);
            Press(host, s2, Accept);
            ctx.Check(exits.Count == 2 && exits[1] is LaunchExit { Mode: MenuMode.Free, Seats.Count: 2, Chapter: "C1B", Match: null },
                $"and FLY's walk launches both seats through the Free Flight feature, carrying no rules ({exits.Count})");

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

    // The picking seat's WEAPON LOADOUT row: it stands only over a selection, opens the decoded ammo
    // chrome on that seat's own fit and airframe, keeps the walk and the seat's own reach while it
    // shows, and lands back on its own row with the picks CANCEL dropped and ACCEPT kept. Leaves the
    // cursor on the list field, so the caller's next Accept confirms the pick as it would have.
    private static void SeatLoadout(
        TestContext ctx, MenuHost host, ScriptedSeat seat0, ScriptedSeat picking,
        CSVM.UI.Menu.Original.OriginalShell shell, PlayerSetupFeature setup)
    {
        var seat = setup.Seats[1];
        string row = nameof(CSVM.UI.Boards.BoardButton.ChangeAmmo);
        ctx.Check(Row(shell, row) is { Enabled: true }, $"a selected seat's screen offers WEAPON LOADOUT ({Row(shell, row)?.Label})");
        ctx.Check(WalkTo(host, picking, shell, row), $"the picking seat's own cursor reaches it ({shell.FocusedKey})");
        Press(host, picking, Accept);
        ctx.Check(shell.Screen == CSVM.UI.Menu.Original.OriginalScreen.InstantActionLoadout && shell.InstantAction.LoadoutSeat == 1
            && ReferenceEquals(shell.InstantAction.LoadoutFit, seat.Fit) && shell.InstantAction.LoadoutNode == setup.Roster[seat.Cursor].Node,
            $"and opens the loadout screen on that seat's own fit and airframe ({shell.Screen}, seat {shell.InstantAction.LoadoutSeat}, {shell.InstantAction.LoadoutNode})");
        ctx.Check(HasLine(shell, "P2  " + setup.Roster[seat.Cursor].Name), $"named for the pilot whose loadout it is");
        Press(host, seat0, Back);
        ctx.Check(shell.Screen == CSVM.UI.Menu.Original.OriginalScreen.InstantActionLoadout && host.Seats.Count == 2,
            $"seat 0's Back moves nothing there and unjoins nobody ({shell.Screen}, {host.Seats.Count} seats)");

        Press(host, picking, Right);
        ctx.Check(!seat.Fit.IsStock && setup.Seats[0].Fit.IsStock,
            $"a sideways step on the focused field writes that seat's own fit and no other's (stock={seat.Fit.IsStock})");
        Press(host, picking, Back);
        ctx.Check(shell.Screen == CSVM.UI.Menu.Original.OriginalScreen.SeatPlane && shell.PickingSeat == 1
            && seat.Fit.IsStock && shell.FocusedKey == row,
            $"its Back is CANCEL LOADOUT, restoring the picks and landing on its own row ({shell.Screen}, {shell.FocusedKey})");

        Press(host, picking, Accept);
        Press(host, picking, Right);
        ctx.Check(WalkTo(host, picking, shell, CSVM.UI.Menu.Original.OriginalInstantActionScreen.LoadoutAcceptKey),
            $"the cursor reaches ACCEPT LOADOUT ({shell.FocusedKey})");
        Press(host, picking, Accept);
        ctx.Check(shell.Screen == CSVM.UI.Menu.Original.OriginalScreen.SeatPlane && !seat.Fit.IsStock,
            $"and ACCEPT keeps the picks on the way back to the picker ({shell.Screen}, stock={seat.Fit.IsStock})");
        ctx.Check(WalkTo(host, picking, shell, CSVM.UI.Menu.Original.OriginalShell.SeatPlaneFieldKey),
            $"the cursor walks back onto the list field ({shell.FocusedKey})");
    }

    // Steps a seat's cursor onto the row keyed <paramref name="key"/> by the shortest walk of
    // arrow presses over the shell's own spatial step.
    private static bool WalkTo(
        MenuHost host, ScriptedSeat seat, CSVM.UI.Menu.Original.OriginalShell shell, string key)
    {
        OriginalWalk.WalkTo(shell, key, frame => Press(host, seat, frame));
        return shell.FocusedKey == key;
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

}
