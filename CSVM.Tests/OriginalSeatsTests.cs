using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Net;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The Original shell's local lobby over the shared player setup, on the hand-authored layout
/// fixture. It covers the Dogfight door and its gate, and the network lobby's opening rules with a
/// rule change reaching the launch. It covers Free Flight's greyed rules and seat 0's aircraft on
/// Select Plane. FLY opens the per-seat aircraft screen for each joined seat in turn. The picking
/// seat's device and the mouse drive it, and seat 0's stick does not. Its WEAPON LOADOUT row
/// edits that seat's own fit, and the last seat's confirm is the launch, here and on Instant Action.
/// </summary>
public class OriginalSeatsTests
{
    // The per-seat screen's WEAPON LOADOUT row, keyed by the board button it draws as.
    private const string LoadoutRow = nameof(BoardButton.ChangeAmmo);

    private static readonly MenuCommands None = new();
    private static readonly MenuCommands Accept = new() { Accept = true };
    private static readonly MenuCommands Back = new() { Back = true };
    private static readonly MenuCommands Down = new() { MoveY = 1 };
    private static readonly MenuCommands Up = new() { MoveY = -1 };
    private static readonly MenuCommands Right = new() { MoveX = 1 };
    private static readonly MenuCommands Left = new() { MoveX = -1 };

    [Fact]
    public void TheDogfightDoorOpensTheLocalLobbyWhoseFlyWaitsForASecondPilot()
    {
        var shell = Shell(out var setup, out _);

        shell.Step(Down);
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.Dogfight, shell.Screen);
        var board = shell.Compose();
        Assert.Contains(board.Backdrop, p => p.Art.Name == "MP_LOBBY_BACKGROUND.JPG");
        Assert.Contains(board.Lines, l => l.Text == "DOGFIGHT");
        Assert.Contains(board.Lines, l => l.Text == OriginalLocalLobby.SecondPilotHint);
        Assert.Contains(board.Lines, l => l.Text == "P1  scripted");
        Assert.False(Fly(shell).Enabled);

        // A disabled FLY takes no click.
        var fly = Fly(shell);
        Assert.Null(Click(shell, fly.X + 4f, fly.Y + 4f).Exit);
        Assert.False(setup.Seats[0].Confirmed);

        // A join board bot is the second pilot, listed with its tag.
        setup.AddBot();
        Assert.True(Fly(shell).Enabled);
        board = shell.Compose();
        Assert.Contains(board.Lines, l => l.Text == setup.Bots.Rows[0].Callsign);
        Assert.Contains(board.Lines, l => l.Text == OriginalLobbyScreen.BotTag);
        Assert.Contains(board.Lines, l => l.Text == OriginalLocalLobby.ReadyHint);
    }

    /// <summary>A local Dogfight opens on the network lobby's own rules, the Time box at 10 armed
    /// and the Score box at 40 unarmed. The lobby's radios, boxes and checkboxes change them, and the
    /// launch carries them as the lobby's own launch reads them.</summary>
    [Fact]
    public void TheDogfightOpensOnTheLobbysRulesAndARuleChangeReachesTheLaunch()
    {
        var shell = Shell(out var setup, out _);
        shell.Step(Down);
        shell.Step(Accept);
        Assert.Equal(DogfightLobby.OpeningOptions, setup.MatchOptions);
        Assert.Equal("10", Row(shell, OriginalLobbyScreen.TimeKey).Label);
        Assert.True(Row(shell, OriginalLobbyScreen.TimeKey).Enabled);
        Assert.Equal("40", Row(shell, OriginalLobbyScreen.ScoreKey).Label);
        Assert.False(Row(shell, OriginalLobbyScreen.ScoreKey).Enabled);
        Assert.False(Row(shell, OriginalLobbyScreen.LivesKey).Enabled);

        // Right from the map box crosses to the rules, the box taking no value sideways.
        var map = Row(shell, OriginalLobbyScreen.EnvironmentKey);
        shell.Step(Pointer(map.X + 4f, map.Y + 4f));
        shell.Step(Right);
        Assert.Equal(OriginalLobbyScreen.TimeRadioKey, shell.FocusedKey);

        // The Score radio arms the score beside the clock, and its box takes typed digits.
        Press(shell, OriginalLobbyScreen.ScoreRadioKey);
        Assert.Equal(DogfightVictory.Both, setup.MatchOptions.Victory);
        var score = Row(shell, OriginalLobbyScreen.ScoreKey);
        shell.Step(Pointer(score.X + 4f, score.Y + 4f));
        Assert.Equal(OriginalLobbyScreen.ScoreKey, shell.FocusedKey);
        Assert.True(shell.CapturingText);
        shell.Step(new MenuCommands { Erase = true });
        shell.Step(new MenuCommands { Erase = true });
        shell.Step(new MenuCommands { Typed = "25" });
        Assert.Equal(25, setup.MatchOptions.Score);

        // Then the Time radio disarms the clock, which leaves the score alone to end the match.
        Press(shell, OriginalLobbyScreen.TimeRadioKey);
        Assert.Equal(DogfightVictory.Score, setup.MatchOptions.Victory);
        Press(shell, OriginalLobbyScreen.LimitedLivesKey);
        Assert.True(Row(shell, OriginalLobbyScreen.LivesKey).Enabled);
        Press(shell, OriginalLobbyScreen.AutoRespawnKey);

        setup.AddBot();
        var launch = Assert.IsType<LaunchExit>(Press(shell, OriginalShell.FlyKey).Exit);
        Assert.Equal(MenuMode.Versus, launch.Mode);
        Assert.Equal(new VersusRules(25, 0, DogfightLobby.DefaultLives, AutoRespawn: false), launch.Match);
        Assert.Single(launch.Bots!);
    }

    [Fact]
    public void FreeFlightGreysTheRuleRowsAndFliesOneSeat()
    {
        var shell = Shell(out _, out var free);
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.FreeFlight, shell.Screen);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "FREE FLIGHT");
        foreach (string key in new[]
        {
            OriginalLobbyScreen.TypeKey, OriginalLobbyScreen.TimeRadioKey, OriginalLobbyScreen.TimeKey,
            OriginalLobbyScreen.ScoreRadioKey, OriginalLobbyScreen.ScoreKey, OriginalLobbyScreen.LimitedLivesKey,
            OriginalLobbyScreen.LivesKey, OriginalLobbyScreen.AutoRespawnKey,
        })
        {
            Assert.False(Row(shell, key).Enabled, key);
        }

        Assert.True(Row(shell, OriginalLobbyScreen.EnvironmentKey).Enabled);
        Assert.False(Row(shell, OriginalLobbyScreen.AmmoTabKey).Enabled);
        Assert.False(Row(shell, OriginalLobbyScreen.ScoresTabKey).Enabled);

        Press(shell, OriginalLobbyScreen.EnvironmentKey);
        Press(shell, OriginalLobbyScreen.EnvironmentKey + ":1");
        Assert.Equal("C1B", shell.LocalLobby.PickedChapter(OriginalScreen.FreeFlight));
        Assert.Equal("C1B", free.Chapter?.Code);

        var launch = Assert.IsType<LaunchExit>(Press(shell, OriginalShell.FlyKey).Exit);
        Assert.Equal(MenuMode.Free, launch.Mode);
        Assert.Equal("C1B", launch.Chapter);
        Assert.Null(launch.Match);
    }

    /// <summary>Each mode keeps its own map, as the two screens did. A Free Flight pick does not
    /// carry into a Dogfight, and a Dogfight launches on its own.</summary>
    [Fact]
    public void EachModeKeepsItsOwnMap()
    {
        var shell = Shell(out var setup, out var free);
        shell.LocalLobby.Enter(OriginalScreen.FreeFlight);
        Press(shell, OriginalLobbyScreen.EnvironmentKey);
        Press(shell, OriginalLobbyScreen.EnvironmentKey + ":1");

        shell.LocalLobby.Enter(OriginalScreen.Dogfight);
        Assert.Null(shell.LocalLobby.PickedChapter(OriginalScreen.Dogfight));
        Assert.Equal("Sea Haven (night)", Row(shell, OriginalLobbyScreen.EnvironmentKey).Label);
        Press(shell, OriginalLobbyScreen.EnvironmentKey);
        Press(shell, OriginalLobbyScreen.EnvironmentKey + ":5");
        setup.AddBot();
        var launch = Assert.IsType<LaunchExit>(Press(shell, OriginalShell.FlyKey).Exit);
        Assert.Equal("C3", launch.Chapter);
        Assert.Equal("C3", setup.MatchChapter);
        Assert.Equal("C1B", shell.LocalLobby.PickedChapter(OriginalScreen.FreeFlight));
        Assert.Equal("C1B", free.Chapter?.Code);
    }

    [Fact]
    public void SeatZerosAircraftIsSelectPlanesBoxWhoseListOpensOnAccept()
    {
        var shell = Shell(out var setup, out _);
        shell.Step(Accept);
        Press(shell, OriginalLobbyScreen.PlaneTabKey);
        var plane = Row(shell, OriginalLobbyScreen.PlaneKey);
        Assert.Equal("Autogyro", plane.Label);
        Assert.Contains(shell.Compose().Pictures, p => p.Art.Name == "MP_PLANEICONSTOPFRONT.PNG");

        // A sideways press moves off the box rather than stepping its value. A press opens its list
        // on the seat's own row, and Accept a row down picks the next.
        shell.Step(Pointer(plane.X + 4f, plane.Y + 4f));
        shell.Step(Right);
        Assert.Equal(0, setup.Seats[0].Cursor);
        Press(shell, OriginalLobbyScreen.PlaneKey);
        Assert.Equal(OriginalLobbyScreen.PlaneKey, shell.LocalLobby.OpenDropdown);
        Assert.Equal(setup.Roster.Count, shell.Rows.Count);
        shell.Step(Down);
        shell.Step(Accept);
        Assert.Equal(1, setup.Seats[0].Cursor);
        Assert.Equal("Hellhound", Row(shell, OriginalLobbyScreen.PlaneKey).Label);
        Assert.False(setup.Seats[0].Locked);
    }

    [Fact]
    public void ASecondSeatPicksOnItsOwnScreenAndItsConfirmLaunchesTheDogfightForBoth()
    {
        PlayerSetupFeature setup = null!;
        var shell = Shell(out setup, out _, seat => ReferenceEquals(seat, setup.Seats[1]) ? new[] { 3 } : Array.Empty<int>());
        shell.Step(Down);
        shell.Step(Accept);
        var pad = new ScriptedMenuSeat();
        var second = setup.Join(pad)!;

        // The join opens nothing by itself; FLY opens the second seat's screen.
        shell.Step(None);
        Assert.Equal(OriginalScreen.Dogfight, shell.Screen);
        Assert.Null(Press(shell, OriginalShell.FlyKey).Exit);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        Assert.Equal(1, shell.PickingSeat);
        Assert.Equal(OriginalScreen.Dogfight, shell.SeatReturn);
        var board = shell.Compose();
        Assert.Contains(board.Backdrop, p => p.Art.Name == "PM_Plane.jpg");
        Assert.Contains(board.Lines, l => l.Text == "PLANE SELECTION");
        Assert.Contains(board.Lines, l => l.Text == "P2  scripted   choose your aircraft");
        Assert.Contains(board.Overlays.SelectMany(o => o.Lines), l => l.Text == "Hellhound");
        Assert.Contains(board.Overlays.SelectMany(o => o.Lines), l => l.Text == "P2" && l.Ink == SeatStrip.Ink(1));
        Assert.Equal(new[] { OriginalShell.SeatPlaneFieldKey, "AcceptSelections", "CancelSelections" },
            shell.Rows.Where(r => !r.Key.StartsWith("ENTRY:", StringComparison.Ordinal)).Select(r => r.Key));

        var walk = shell.StepSeat(1, Down);
        Assert.True(walk.Changed);
        Assert.Equal(0, second.Cursor);
        Assert.Contains(shell.Compose().Pictures, p => p.Art.Name == "FC_PlaneIcons.Png" && p.Frame == 1);

        shell.StepSeat(1, Accept);
        Assert.True(second.Locked);
        Assert.Equal(1, second.Cursor);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        Assert.Equal(4, shell.Rows.Count);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "P2  scripted   A again to confirm, B to change");
        Assert.Contains(shell.Compose().Lines, l => l.Text.StartsWith("AGILITY:", StringComparison.Ordinal));

        // The last seat's confirm is the launch: the lobby returns behind it with both seats
        // confirmed, so nobody has to reach FLY again.
        var step = shell.StepSeat(1, Accept);
        Assert.True(second.Confirmed);
        Assert.Equal(OriginalScreen.Dogfight, shell.Screen);
        Assert.Equal(-1, shell.PickingSeat);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "P2  scripted");
        Assert.Contains(shell.Compose().Lines, l => l.Text == "Hellhound");
        var launch = Assert.IsType<LaunchExit>(step.Exit);
        Assert.Equal(MenuMode.Versus, launch.Mode);
        Assert.Equal("C1", launch.Chapter);
        Assert.Equal(new[] { "player_autogyro", "player_avenger" }, launch.Seats.Select(s => s.PlaneNode));
        Assert.Empty(launch.Seats[0].Pads);
        Assert.Equal(new[] { 3 }, launch.Seats[1].Pads);
        Assert.Equal(DogfightLobby.RulesOf(DogfightLobby.OpeningOptions), launch.Match);
        Assert.True(setup.Seats[0].Confirmed);
    }

    [Fact]
    public void TheLastSeatsConfirmLaunchesTwoPilotFreeFlightThroughTheFeature()
    {
        var shell = Shell(out var setup, out var free);
        shell.Step(Accept);
        Press(shell, OriginalLobbyScreen.EnvironmentKey);
        Press(shell, OriginalLobbyScreen.EnvironmentKey + ":1");
        Press(shell, OriginalLobbyScreen.PlaneTabKey);
        Press(shell, OriginalLobbyScreen.PlaneKey);
        Press(shell, OriginalLobbyScreen.PlaneKey + ":1");
        var second = setup.Join(new ScriptedMenuSeat())!;
        Press(shell, OriginalShell.FlyKey);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        shell.StepSeat(1, Down);
        shell.StepSeat(1, Down);
        shell.StepSeat(1, Accept);

        var launch = Assert.IsType<LaunchExit>(shell.StepSeat(1, Accept).Exit);

        Assert.Equal(OriginalScreen.FreeFlight, shell.Screen);
        Assert.Equal(MenuMode.Free, launch.Mode);
        Assert.Equal("C1B", launch.Chapter);
        Assert.Equal("C1B", free.Chapter?.Code);
        Assert.Equal(new[] { "player_avenger", "player_balmoral" }, launch.Seats.Select(s => s.PlaneNode));
        Assert.True(setup.Seats[0].Confirmed);
        Assert.Equal(2, second.Cursor);
    }

    [Fact]
    public void AWalkLosingTheSecondPilotReturnsTheDogfightWithFlyToPress()
    {
        var shell = Shell(out var setup, out _);
        shell.Step(Down);
        shell.Step(Accept);
        var second = setup.Join(new ScriptedMenuSeat())!;
        Press(shell, OriginalShell.FlyKey);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);

        // The walk ends with every seat left confirmed, but the Dogfight's own gate is unmet.
        setup.Unjoin(second);
        Assert.Null(shell.Step(None).Exit);
        Assert.Equal(OriginalScreen.Dogfight, shell.Screen);
        Assert.False(Fly(shell).Enabled);
        Assert.False(setup.Seats[0].Confirmed);
        Assert.Contains(shell.Compose().Lines, l => l.Text == OriginalLocalLobby.SecondPilotHint);
    }

    [Fact]
    public void TheHintNamesFourSeatsAsTheMaximum()
    {
        var shell = Shell(out var setup, out _);
        shell.Step(Accept);
        Assert.Contains(shell.Compose().Lines, l => l.Text == OriginalLocalLobby.ReadyHint);
        for (int i = 0; i < 3; i++)
        {
            setup.Join(new ScriptedMenuSeat());
        }

        Assert.Contains(shell.Compose().Lines, l => l.Text == "Four seats signed on, the maximum. " + OriginalLocalLobby.ReadyHint);
    }

    [Fact]
    public void EachJoinedSeatPicksInPlayerOrderOnItsOwnDeviceWhileSeatZerosMovesNothing()
    {
        var shell = Shell(out var setup, out _);
        shell.Step(Accept);
        var second = setup.Join(new ScriptedMenuSeat())!;
        var third = setup.Join(new ScriptedMenuSeat())!;
        Press(shell, OriginalShell.FlyKey);
        Assert.Equal(1, shell.PickingSeat);

        // Seat 0's stick and buttons move nothing on a screen that is not its own.
        Assert.False(shell.Step(Down).Changed);
        Assert.False(shell.Step(Accept).Changed);
        Assert.False(shell.Step(Back).Changed);
        Assert.False(second.Locked);
        Assert.Equal(0, second.Cursor);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        Assert.Equal(1, shell.PickingSeat);

        // The picking seat's own device drives it: select, then confirm.
        shell.StepSeat(1, Down);
        shell.StepSeat(1, Accept);
        Assert.True(second.Locked);
        shell.StepSeat(1, Accept);
        Assert.True(second.Confirmed);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        Assert.Equal(2, shell.PickingSeat);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "P3  scripted   choose your aircraft");

        // Seat 0's pointer still drives the screen, one mouse at the desk serving whoever picks: a
        // click on a list entry selects it for the picking seat, ACCEPT SELECTIONS confirms.
        var entry = shell.Rows.Single(r => r.Key == "ENTRY:2");
        Click(shell, entry.X + 4f, entry.Y + 4f);
        Assert.True(third.Locked);
        Assert.Equal(2, third.Cursor);
        var accept = shell.Rows.Single(r => r.Key == "AcceptSelections");
        var last = Click(shell, accept.X + 4f, accept.Y + 4f);
        Assert.True(third.Confirmed);

        // The last seat confirmed, so that press is the launch for all three, whichever device it
        // came from, and the Free Flight screen returns behind it.
        Assert.Equal(3, Assert.IsType<LaunchExit>(last.Exit).Seats.Count);
        Assert.Equal(OriginalScreen.FreeFlight, shell.Screen);
        Assert.True(Fly(shell).Enabled);
    }

    [Fact]
    public void SeatZerosBackIsNotThePerSeatScreensExitAndTheMouseLeavesThroughCancelSelections()
    {
        var shell = Shell(out var setup, out _);
        shell.Step(Accept);
        var second = setup.Join(new ScriptedMenuSeat())!;
        Press(shell, OriginalShell.FlyKey);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);

        // Back here belongs to the picking seat, so seat 0's leaves nothing.
        Assert.False(shell.Step(Back).Changed);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);

        // CANCEL SELECTIONS over the open list is the pointer's own way out of the walk.
        var cancel = shell.Rows.Single(r => r.Key == "CancelSelections");
        Assert.Null(Click(shell, cancel.X + 4f, cancel.Y + 4f).Exit);
        Assert.Equal(OriginalScreen.FreeFlight, shell.Screen);
        Assert.True(second.Joined);
        Assert.Equal(2, setup.Seats.Count);
        Assert.False(setup.Seats[0].Locked);

        // FLY again walks the seat again; a frame with nothing pressed changes nothing.
        Press(shell, OriginalShell.FlyKey);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        Assert.False(shell.Step(None).Changed);
    }

    [Fact]
    public void ALaterSeatsBackUnselectsThenLeavesTheWalkAndUnjoinsFromAnyOtherScreenAtOnce()
    {
        var shell = Shell(out var setup, out _);
        var pad = new ScriptedMenuSeat();
        setup.Join(pad);

        Assert.True(shell.StepSeat(1, Back).Changed);
        Assert.Single(setup.Seats);
        Assert.False(shell.StepSeat(1, Back).Changed);

        shell.Step(Accept);
        var second = setup.Join(pad)!;
        Press(shell, OriginalShell.FlyKey);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        shell.StepSeat(1, Accept);
        Assert.True(second.Locked);
        Assert.Equal(4, shell.Rows.Count);
        shell.StepSeat(1, Back);
        Assert.False(second.Locked);
        Assert.True(second.Joined);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        Assert.True(shell.Rows.Count > 4);

        // Over the open list Back leaves the walk with both seats kept.
        shell.StepSeat(1, Back);
        Assert.True(second.Joined);
        Assert.Equal(2, setup.Seats.Count);
        Assert.Equal(OriginalScreen.FreeFlight, shell.Screen);
        Assert.True(Fly(shell).Enabled);

        // FLY again reopens the walk at the same seat, so the return is not a dead end.
        Press(shell, OriginalShell.FlyKey);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        Assert.Equal(1, shell.PickingSeat);

        // CANCEL SELECTIONS drops the selection and reopens the list, leaving neither the walk nor
        // the lobby.
        shell.StepSeat(1, Accept);
        Assert.True(second.Locked);
        var cancel = shell.Rows.Single(r => r.Key == "CancelSelections");
        Click(shell, cancel.X + 4f, cancel.Y + 4f);
        Assert.False(second.Locked);
        Assert.True(second.Joined);
        Assert.Equal(2, setup.Seats.Count);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        Assert.True(shell.Rows.Count > 4);

        // With no selection left to drop, the same press leaves the walk, both seats kept: the one
        // exit a mouse has, Back belonging to the picking seat's device.
        var again = shell.Rows.Single(r => r.Key == "CancelSelections");
        Assert.Null(Click(shell, again.X + 4f, again.Y + 4f).Exit);
        Assert.Equal(OriginalScreen.FreeFlight, shell.Screen);
        Assert.True(second.Joined);
        Assert.Equal(2, setup.Seats.Count);
    }

    [Fact]
    public void FlyMissionWithASecondSeatWalksItThroughThePerSeatScreenThenLaunchesBoth()
    {
        PlayerSetupFeature setup = null!;
        var shell = Shell(out setup, out _, seat => ReferenceEquals(seat, setup.Seats[1]) ? new[] { 2 } : Array.Empty<int>());
        shell.InstantAction.OpenInstantAction();
        Assert.DoesNotContain(shell.Compose().Overlays.SelectMany(o => o.Lines), l => l.Text.StartsWith("P1", StringComparison.Ordinal));
        var second = setup.Join(new ScriptedMenuSeat())!;
        Assert.Contains(shell.Compose().Overlays.SelectMany(o => o.Lines), l => l.Text == "P2" && l.Ink == SeatStrip.Ink(1));

        var fly = shell.Rows.Single(r => r.Key == OriginalInstantActionScreen.FlyMissionKey);
        Assert.Null(Click(shell, fly.X + 4f, fly.Y + 4f).Exit);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        Assert.Equal(1, shell.PickingSeat);
        Assert.Equal(OriginalScreen.InstantAction, shell.SeatReturn);
        Assert.True(setup.Seats[0].Locked);
        Assert.Same(shell.InstantAction.PilotRoster, setup.Roster);

        shell.StepSeat(1, Down);
        shell.StepSeat(1, Down);
        shell.StepSeat(1, Accept);
        Assert.True(second.Locked);
        var launch = Assert.IsType<LaunchExit>(shell.StepSeat(1, Accept).Exit);
        Assert.Equal(OriginalScreen.InstantAction, shell.Screen);
        Assert.NotNull(launch.InstantAction);
        Assert.Equal(2, launch.Seats.Count);
        Assert.Equal(setup.Roster[shell.InstantAction.PilotRow].Node, launch.Seats[0].PlaneNode);
        Assert.Equal("player_balmoral", launch.Seats[1].PlaneNode);
        Assert.Equal(new[] { 2 }, launch.Seats[1].Pads);
        Assert.True(setup.Seats[0].Confirmed && second.Confirmed);
    }

    [Fact]
    public void EachPickingSeatsWeaponLoadoutRowEditsItsOwnFitAndTheLaunchCarriesEveryOne()
    {
        var shell = Shell(out var setup, out _);
        shell.Step(Accept);
        var second = setup.Join(new ScriptedMenuSeat())!;
        var third = setup.Join(new ScriptedMenuSeat())!;
        Press(shell, OriginalShell.FlyKey);
        Assert.Equal(1, shell.PickingSeat);

        // While the seat browses there is no loadout row: a fit needs an aeroplane to hang on.
        Assert.DoesNotContain(shell.Rows, r => r.Key == LoadoutRow);
        shell.StepSeat(1, Accept);
        Assert.True(second.Locked);
        Assert.Equal(4, shell.Rows.Count);
        Assert.Equal("WEAPON LOADOUT", shell.Rows.Single(r => r.Key == LoadoutRow).Label);

        // The row opens the loadout screen on the picking seat's own fit and its own aeroplane.
        // The row stands right of the pick over ACCEPT SELECTIONS, so the cursor reaches it from below.
        shell.StepSeat(1, Down);
        Assert.Equal("AcceptSelections", shell.FocusedKey);
        shell.StepSeat(1, Up);
        Assert.Equal(LoadoutRow, shell.FocusedKey);
        shell.StepSeat(1, Accept);
        Assert.Equal(OriginalScreen.InstantActionLoadout, shell.Screen);
        Assert.Equal(1, shell.InstantAction.LoadoutSeat);
        Assert.Same(second.Fit, shell.InstantAction.LoadoutFit);
        Assert.Equal(setup.Roster[second.Cursor].Node, shell.InstantAction.LoadoutNode);

        // The walk stands on this screen too: seat 0's frame still moves nothing, and the picking
        // seat's Back is CANCEL LOADOUT rather than an unjoin, so it lands back on its own row.
        Assert.False(shell.Step(Back).Changed);
        second.Fit.SetGunAmmo(1, "wep_ap");
        shell.StepSeat(1, Back);
        Assert.Equal(3, setup.Seats.Count);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        Assert.Equal(1, shell.PickingSeat);
        Assert.Equal(LoadoutRow, shell.FocusedKey);
        Assert.True(second.Fit.IsStock);

        // ACCEPT LOADOUT keeps the picks, and on that seat's fit alone.
        shell.StepSeat(1, Accept);
        second.Fit.SetGunAmmo(1, "wep_ap");
        var keep = shell.Rows.Single(r => r.Key == OriginalInstantActionScreen.LoadoutAcceptKey);
        Click(shell, keep.X + 4f, keep.Y + 4f);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        Assert.Equal(LoadoutRow, shell.FocusedKey);
        Assert.Equal("wep_ap", second.Fit.GunAmmoFor(1));
        Assert.True(setup.Seats[0].Fit.IsStock);
        Assert.True(third.Fit.IsStock);

        // Every seat's own fit reaches the launch, seat 0's carrying nothing because it picked none.
        var confirm = shell.Rows.Single(r => r.Key == "AcceptSelections");
        Click(shell, confirm.X + 4f, confirm.Y + 4f);
        Assert.True(second.Confirmed);
        Assert.Equal(2, shell.PickingSeat);
        shell.StepSeat(2, Accept);
        third.Fit.SetPylon(1, "wep_rocket");
        var launch = Assert.IsType<LaunchExit>(shell.StepSeat(2, Accept).Exit);
        Assert.Null(launch.Seats[0].Fit);
        Assert.Same(second.Fit, launch.Seats[1].Fit);
        Assert.Same(third.Fit, launch.Seats[2].Fit);
    }

    [Fact]
    public void AWalkLosingItsPickingSeatOverTheLoadoutScreenMovesOnWithoutIt()
    {
        var shell = Shell(out var setup, out _);
        shell.Step(Accept);
        var second = setup.Join(new ScriptedMenuSeat())!;
        setup.Join(new ScriptedMenuSeat());
        Press(shell, OriginalShell.FlyKey);
        shell.StepSeat(1, Accept);
        shell.StepSeat(1, Down);
        shell.StepSeat(1, Up);
        shell.StepSeat(1, Accept);
        Assert.Equal(OriginalScreen.InstantActionLoadout, shell.Screen);

        setup.Unjoin(second);
        Assert.True(shell.Step(None).Changed);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        Assert.Equal(1, shell.PickingSeat);
        Assert.Equal(-1, shell.InstantAction.LoadoutSeat);
        Assert.Null(shell.InstantAction.LoadoutFit);
    }

    [Fact]
    public void AReturnToTheTopLevelResetsEverySeatsPickAndKeepsTheSeats()
    {
        var shell = Shell(out var setup, out _);
        shell.Step(Accept);
        var second = setup.Join(new ScriptedMenuSeat())!;
        Press(shell, OriginalShell.FlyKey);
        shell.StepSeat(1, Accept);
        shell.StepSeat(1, Accept);

        shell.ReturnToTopLevel();

        Assert.Equal(2, setup.Seats.Count);
        Assert.False(setup.Seats[0].Locked || setup.Seats[0].Confirmed);
        Assert.False(second.Locked || second.Confirmed);
        Assert.Equal(-1, shell.PickingSeat);
    }

    private static OriginalRow Fly(OriginalShell shell) => Row(shell, OriginalShell.FlyKey);

    private static OriginalRow Row(OriginalShell shell, string key) => shell.Rows.Single(r => r.Key == key);

    // Seat 0 is a scripted source over the three-stock-plus-customs roster the fixture's shell picks from.
    private static OriginalShell Shell(
        out PlayerSetupFeature setup,
        out FreeFlightFeature free,
        Func<PlayerSeat, IReadOnlyList<int>>? flightDevices = null,
        IReadOnlyList<CustomPlaneDef>? customs = null)
    {
        free = new FreeFlightFeature();
        setup = new PlayerSetupFeature();
        setup.SetRoster(OriginalPresentation.Roster(customs ?? Array.Empty<CustomPlaneDef>()));
        setup.Join(new ScriptedMenuSeat());
        return new OriginalShell(MenuLayoutReaderTests.OriginalLayout(), free, setup, Measure, flightDevices);
    }

    private static (int Width, int Height)? Measure(string art) => art switch
    {
        "PM_B_Paper.png" => (160, 112),
        _ when art.StartsWith("PM_B_", StringComparison.Ordinal) => (240, 200),
        _ => null,
    };

    private static MenuCommands Pointer(float x, float y, bool pressed = false, bool clicked = false) =>
        new() { Pointer = new MenuPointer(x, y, pressed, clicked) };

    // One click as the shell reads it: the press arms the row and the release on it fires, so the
    // step that carries the activation is the second one.
    private static OriginalStep Click(OriginalShell shell, float x, float y)
    {
        shell.Step(Pointer(x, y, pressed: true, clicked: true));
        return shell.Step(Pointer(x, y));
    }

    // The same click, just inside the corner of the row carrying a key.
    private static OriginalStep Press(OriginalShell shell, string key)
    {
        var row = Row(shell, key);
        return Click(shell, row.X + 2f, row.Y + 2f);
    }
}
