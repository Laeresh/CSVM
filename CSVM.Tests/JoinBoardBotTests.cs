using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Net;
using CSVM.Session.Roster;
using CSVM.Spec;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// A local Dogfight's bot rows. In the player setup, the two-pilot minimum counts bots and
/// Fill-to-N stops at 16 pilots. A seat signing onto a full field takes the newest bot's place, and
/// the rows ride the Dogfight exit and no other. The menu spec carries them and Random resolves at
/// launch. The join board's Bots block and Edit Bot panel are driven on the layout fixture.
/// </summary>
public class JoinBoardBotTests
{
    private static readonly MenuCommands Accept = new() { Accept = true };
    private static readonly MenuCommands Down = new() { MoveY = 1 };
    private static readonly MenuCommands Right = new() { MoveX = 1 };

    [Fact]
    public void OneSeatAndOneBotLaunchALocalDogfight()
    {
        var setup = Confirmed(seats: 1);
        Assert.Equal("Dogfight needs a second seat", setup.Refusal(MenuMode.Versus));
        Assert.Throws<InvalidOperationException>(() => setup.BuildExit("C1", MenuMode.Versus, _ => Array.Empty<int>()));

        Assert.True(setup.AddBot());
        Assert.Equal(2, setup.Pilots(MenuMode.Versus));
        Assert.Null(setup.Refusal(MenuMode.Versus));
        var exit = setup.BuildExit("C1", MenuMode.Versus, _ => Array.Empty<int>());
        var bot = Assert.Single(exit.Bots!);
        Assert.Single(exit.Seats);
        Assert.Null(bot.Plane);
        Assert.Equal(NetBotSkill.Veteran, bot.Skill);
        Assert.Equal(0, bot.Team);
        Assert.Equal(setup.Bots.Rows[0].Callsign, bot.Callsign);
        Assert.NotEmpty(bot.Callsign);

        // The menu spec seats the board's bots after its one pane.
        var spec = SessionSpec.FromMenu(SessionSpec.Parse(Array.Empty<string>()), exit.Chapter,
            exit.Seats.Select(s => s.PlaneNode).ToArray(), exit.Mode, bots: exit.Bots);
        Assert.True(spec.Versus);
        Assert.Equal(1, spec.Players);
        Assert.Equal(exit.Bots, spec.VsBots);
    }

    [Fact]
    public void BotsCountTowardADogfightAloneAndRideOnlyItsExit()
    {
        var setup = Confirmed(seats: 1);
        setup.AddBot();
        Assert.Equal(1, setup.Pilots(MenuMode.Free));
        Assert.Null(setup.BuildExit("C1", MenuMode.Free, _ => Array.Empty<int>()).Bots);

        // A Capture the Flag, Zeppelin or Stunt Race spec, and a Free Flight one, carry none.
        var cli = SessionSpec.Parse(Array.Empty<string>());
        var bots = setup.Bots.LaunchEntries();
        Assert.Empty(SessionSpec.FromMenu(cli, "C1", new[] { "player_bhawk" }, MenuMode.Free, bots: bots).VsBots);
        Assert.Empty(SessionSpec.FromMenu(cli, "C1", new[] { "player_bhawk" }, MenuMode.Versus,
            missionType: DogfightMissionType.CaptureTheFlag, bots: bots).VsBots);
        Assert.Empty(SessionSpec.FromMenu(cli, "C1", new[] { "player_bhawk" }, MenuMode.Versus,
            missionType: DogfightMissionType.ZeppelinVsZeppelin, bots: bots).VsBots);
        Assert.Empty(SessionSpec.FromMenu(cli, "C1", new[] { "player_bhawk" }, MenuMode.Stunt,
            missionType: DogfightMissionType.StuntRace, bots: bots).VsBots);
        Assert.NotEmpty(SessionSpec.FromMenu(cli, "C1", new[] { "player_bhawk" }, MenuMode.Versus,
            missionType: DogfightMissionType.Deathmatch, bots: bots).VsBots);

        // The menu's bots replace the command line's, so a menu launch never seats a flag's bots.
        var flagged = SessionSpec.Parse(new[] { "--vs", "--vs-bots=3" });
        Assert.Equal(3, flagged.VsBots.Count);
        Assert.Empty(SessionSpec.FromMenu(flagged, "C1", new[] { "player_bhawk" }, MenuMode.Versus).VsBots);

        // A presentation switch drops the rows with the rest of the unfinished setup.
        setup.Discard();
        Assert.Equal(0, setup.Bots.Count);
    }

    [Fact]
    public void FillToStopsAtSixteenPilotsAndAddStopsThere()
    {
        var setup = Confirmed(seats: 1);
        Assert.Equal(15, setup.FillBots(20));
        Assert.Equal(NetSeats.MaxPlayers, setup.FieldPilots);
        Assert.Equal(0, setup.BotRoom);
        Assert.False(setup.AddBot());
        Assert.Equal(0, setup.FillBots(16));

        // Fill-to-N counts the seats, and removes nothing from a field already that large.
        var pair = Confirmed(seats: 2);
        Assert.Equal(6, pair.FillBots(8));
        Assert.Equal(8, pair.FieldPilots);
        Assert.Equal(0, pair.FillBots(4));
        Assert.Equal(6, pair.Bots.Count);

        // Every callsign is distinct, and none is a pane's player tag.
        var names = setup.Bots.Rows.Select(b => b.Callsign).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.DoesNotContain(names, n => n is "P1" or "P2" or "P3" or "P4");
    }

    [Fact]
    public void ASeatSigningOntoAFullFieldTakesTheNewestBotsPlace()
    {
        var setup = Confirmed(seats: 1);
        setup.FillBots(NetSeats.MaxPlayers);
        int oldest = setup.Bots.Rows[0].Id;
        int newest = setup.Bots.Rows[^1].Id;
        Assert.NotNull(setup.Join(new ScriptedMenuSeat()));
        Assert.Equal(NetSeats.MaxPlayers, setup.FieldPilots);
        Assert.Null(setup.Bots.ById(newest));
        Assert.NotNull(setup.Bots.ById(oldest));

        // ABLE-TO-FAIL CONTROL: a seat joining with room takes no bot's place.
        var roomy = Confirmed(seats: 1);
        roomy.FillBots(4);
        roomy.Join(new ScriptedMenuSeat());
        Assert.Equal(3, roomy.Bots.Count);
    }

    [Fact]
    public void ARenameIsCutAndRefusesAPlayerTagOrAnotherBotsName()
    {
        var setup = Confirmed(seats: 1);
        setup.AddBot();
        setup.AddBot();
        int first = setup.Bots.Rows[0].Id;
        Assert.False(setup.RenameBot(first, "P3"));
        Assert.False(setup.RenameBot(first, setup.Bots.Rows[1].Callsign));
        Assert.False(setup.RenameBot(first, "   "));
        Assert.True(setup.RenameBot(first, "Baron von Richthofen"));
        Assert.Equal("Baron von Ri", setup.Bots.ById(first)!.Value.Callsign);
    }

    [Fact]
    public void RandomResolvesToAStockPlaneAtLaunchAndANamedOneIsKept()
    {
        var setup = Confirmed(seats: 1);
        setup.AddBot();
        setup.AddBot();
        int fury = StockAirframes.IdOf("player_fury")!.Value;
        Assert.True(setup.Bots.SetAirframe(setup.Bots.Rows[1].Id, fury));
        var entries = setup.Bots.LaunchEntries();
        Assert.Null(entries[0].Plane);
        Assert.Equal("player_fury", entries[1].Plane);

        var panes = NetSeats.LocalPanes(1);
        var resolved = BotSeats.Resolve(entries, panes.Select(s => s.Callsign), Array.Empty<string>(), new Random(7));
        Assert.Contains(resolved[0].Plane, StockAirframes.Nodes);
        Assert.Equal("player_fury", resolved[1].Plane);
        Assert.Equal(entries.Select(e => e.Callsign), resolved.Select(r => r.Callsign));

        // The local roster those make: the pane, then both bots on its peer.
        Assert.Equal(0, NetSeats.AddBots(panes, NetSeats.OfflinePeer, resolved));
        NetSeats.Validate(panes, NetSeats.OfflinePeer);
        Assert.Equal(new[] { false, true, true }, panes.Select(s => s.IsBot));
    }

    [Fact]
    public void TheJoinBoardAddsFillsAndEditsBotRowsAndTheDogfightCountsThem()
    {
        var shell = Shell(out var setup);
        shell.JoinBoard.Open();
        Assert.Equal(OriginalJoinBoard.ContinueKey, shell.FocusedKey);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "Bots");
        Assert.DoesNotContain(shell.Rows, r => r.Key.StartsWith(OriginalJoinBoard.BotRowPrefix, StringComparison.Ordinal));

        Press(shell, OriginalJoinBoard.AddBotKey);
        var row = Assert.Single(shell.Rows, r => r.Key.StartsWith(OriginalJoinBoard.BotRowPrefix, StringComparison.Ordinal));
        Assert.Equal(setup.Bots.Rows[0].Callsign, row.Label);

        // The count box's arrows step Fill to's target, and Fill to fills the field to it.
        Assert.Equal(DogfightLobby.DefaultFillTo, shell.JoinBoard.FillCount);
        Press(shell, OriginalJoinBoard.FillArrowPrefix + "-");
        Press(shell, OriginalJoinBoard.FillKey);
        Assert.Equal(DogfightLobby.DefaultFillTo - 1, setup.FieldPilots);
        Assert.False(shell.Rows.Single(r => r.Key == OriginalJoinBoard.FillKey).Enabled);

        // A row's press opens Edit Bot on it, the callsign box taking the typing.
        int id = setup.Bots.Rows[1].Id;
        Press(shell, OriginalBotPanel.RowKey(1));
        Assert.Equal(id, shell.JoinBoard.PickedBot);
        Assert.Equal(OriginalJoinBoard.BotNameKey, shell.FocusedKey);
        Assert.True(shell.CapturingText);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "Edit Bot");
        Assert.DoesNotContain(shell.Compose().Lines, l => l.Text == "ARTICLES OF THE CREW");
        for (int i = 0; i < 12; i++)
        {
            shell.Step(new MenuCommands { Erase = true });
        }

        shell.Step(new MenuCommands { Typed = "Red Baron" });
        Assert.Equal("Red Baron", setup.Bots.ById(id)!.Value.Callsign);

        // The plane box's list: Random, then the eleven stock planes; the skill box steps sideways.
        shell.Step(Down);
        Assert.Equal(OriginalJoinBoard.BotPlaneKey, shell.FocusedKey);
        shell.Step(Accept);
        Assert.Equal(OriginalJoinBoard.BotPlaneKey, shell.JoinBoard.OpenDropdown);
        Assert.Equal(DogfightLobby.AirframeCount + 1, shell.Rows.Count);
        Assert.Equal("Random", shell.Rows[0].Label);
        Press(shell, OriginalJoinBoard.BotPlaneKey + ":8");
        Assert.Null(shell.JoinBoard.OpenDropdown);
        Assert.Equal(7, setup.Bots.ById(id)!.Value.Airframe);
        shell.Step(Down);
        shell.Step(Right);
        Assert.Equal(NetBotSkill.Ace, setup.Bots.ById(id)!.Value.Skill);

        // Remove lets the row go; Back with no row picked leaves the board.
        int before = setup.Bots.Count;
        Press(shell, OriginalJoinBoard.RemoveBotKey);
        Assert.Equal(before - 1, setup.Bots.Count);
        Assert.Null(setup.Bots.ById(id));
        Assert.Equal(-1, shell.JoinBoard.PickedBot);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "ARTICLES OF THE CREW");

        // The Dogfight screen counts the bots: one seat flies with them, and the strip names them.
        Press(shell, OriginalJoinBoard.ContinueKey);
        shell.Step(Down);
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.Dogfight, shell.Screen);
        shell.Step(Accept);
        shell.Step(Right);
        shell.Step(Accept);
        Assert.Contains(shell.Compose().Lines, l => l.Text == OriginalShell.BotStripLine(setup.Bots.Count));
        Assert.Contains(shell.Compose().Lines, l => l.Text == "FLY when ready");
        var step = Click(shell, shell.Rows.Single(r => r.Key == OriginalShell.FlyKey));
        var launch = Assert.IsType<LaunchExit>(step.Exit);
        Assert.Single(launch.Seats);
        Assert.Equal(setup.Bots.Rows.Select(b => b.Callsign), launch.Bots!.Select(b => b.Callsign));
    }

    [Fact]
    public void ALoneSeatWithNoBotIsToldItCanAddOne()
    {
        var shell = Shell(out _);
        shell.Step(Down);
        shell.Step(Accept);
        shell.Step(Accept);
        shell.Step(Right);
        shell.Step(Accept);
        Assert.Contains(shell.Compose().Lines,
            l => l.Text == "Dogfight needs a second seat or a bot, from the JOIN BOARD");
        Assert.False(shell.Rows.Single(r => r.Key == OriginalShell.FlyKey).Enabled);
    }

    private static PlayerSetupFeature Confirmed(int seats)
    {
        var setup = new PlayerSetupFeature();
        setup.SetRoster(OriginalPresentation.Roster(Array.Empty<CustomPlaneDef>()));
        for (int i = 0; i < seats; i++)
        {
            var seat = setup.Join(new ScriptedMenuSeat())!;
            setup.Select(seat);
            setup.Confirm(seat);
        }

        return setup;
    }

    private static OriginalShell Shell(out PlayerSetupFeature setup)
    {
        setup = new PlayerSetupFeature();
        setup.SetRoster(OriginalPresentation.Roster(Array.Empty<CustomPlaneDef>()));
        setup.Join(new ScriptedMenuSeat());
        return new OriginalShell(MenuLayoutReaderTests.OriginalLayout(), new FreeFlightFeature(), setup, _ => null);
    }

    private static void Press(OriginalShell shell, string key) => Click(shell, shell.Rows.Single(r => r.Key == key));

    private static OriginalStep Click(OriginalShell shell, OriginalRow row)
    {
        float x = row.X + 2f;
        float y = row.Y + (row.Height / 2f);
        shell.Step(new MenuCommands { Pointer = new MenuPointer(x, y, true, true) });
        return shell.Step(new MenuCommands { Pointer = new MenuPointer(x, y, false, false) });
    }
}
