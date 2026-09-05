using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight;
using CSVM.UI;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The Original shell's sortie screens over the shared player setup, on the hand-authored layout
/// fixture: the Dogfight door and its gate, seat 0's pick opening the per-seat aircraft screen
/// for each joined seat in turn (its plane-selection shape, the list open while browsing, Accept
/// selecting then confirming, Back undoing or unjoining, seat 0's Back cancelling), FLY as seat
/// 0's confirmation and the launch, Instant Action's FLY MISSION walking a joined seat before its
/// launch, the hint at each stage, and the aircraft window over a roster with saved customs.
/// </summary>
public class OriginalSeatsTests
{
    private static readonly MenuCommands None = new();
    private static readonly MenuCommands Accept = new() { Accept = true };
    private static readonly MenuCommands Back = new() { Back = true };
    private static readonly MenuCommands Down = new() { MoveY = 1 };
    private static readonly MenuCommands Up = new() { MoveY = -1 };
    private static readonly MenuCommands Right = new() { MoveX = 1 };

    [Fact]
    public void TheDogfightDoorOpensTheDogfightScreenWhoseFlyWaitsForASecondSeat()
    {
        var shell = Shell(out var setup, out _);

        shell.Step(Down);
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.Dogfight, shell.Screen);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "DOGFIGHT");
        Assert.Contains(shell.Compose().Lines, l => l.Text == "Pick a map, then an aircraft");
        Assert.False(Fly(shell).Enabled);

        shell.Step(Accept);
        Assert.Equal("C1", shell.PickedDogfightChapter);
        Assert.Null(shell.PickedChapter);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "Pick an aircraft");
        shell.Step(Right);
        shell.Step(Accept);
        Assert.Equal("player_autogyro", shell.PickedAirframe);
        Assert.True(setup.Seats[0].Locked);
        Assert.False(Fly(shell).Enabled);
        Assert.Contains(shell.Compose().Lines, l => l.Text.StartsWith("Dogfight needs a second seat", StringComparison.Ordinal));

        // A disabled FLY takes no keyboard focus (Up wraps past it) and no click.
        shell.Step(Up);
        Assert.Equal(OriginalShell.AirframeKey(10), shell.FocusedKey);
        var fly = Fly(shell);
        var step = shell.Step(Pointer(fly.X + 4f, fly.Y + 4f, pressed: true, clicked: true));
        Assert.Null(step.Exit);
        Assert.False(setup.Seats[0].Confirmed);
    }

    [Fact]
    public void ASecondSeatPicksOnItsOwnScreenAndFlyLaunchesTheDogfightForBoth()
    {
        PlayerSetupFeature setup = null!;
        var shell = Shell(out setup, out _, seat => ReferenceEquals(seat, setup.Seats[1]) ? new[] { 3 } : Array.Empty<int>());
        shell.Step(Down);
        shell.Step(Accept);
        shell.Step(Accept);
        shell.Step(Right);
        shell.Step(Accept);
        var pad = new ScriptedMenuSeat();
        var second = setup.Join(pad)!;

        // The join lands between frames; seat 0's next frame opens the second seat's screen.
        Assert.True(shell.Step(None).Changed);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        Assert.Equal(1, shell.PickingSeat);
        Assert.Equal(OriginalScreen.Dogfight, shell.SeatReturn);
        var board = shell.Compose();
        Assert.Contains(board.Backdrop, p => p.Art.Name == "PM_Plane.jpg");
        Assert.Contains(board.Lines, l => l.Text == "PLANE SELECTION");
        Assert.Contains(board.Lines, l => l.Text == "P2  scripted   choose your aircraft");
        Assert.Contains(board.Overlays.SelectMany(o => o.Lines), l => l.Text == "Hellhound");
        Assert.Contains(board.Overlays.SelectMany(o => o.Lines), l => l.Text == "P2  scripted" && l.Ink == BoardInk.RowFocused);
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
        Assert.Equal(3, shell.Rows.Count);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "P2  scripted   A again to confirm, B to change");
        Assert.Contains(shell.Compose().Lines, l => l.Text.StartsWith("AGILITY:", StringComparison.Ordinal));

        shell.StepSeat(1, Accept);
        Assert.True(second.Confirmed);
        Assert.Equal(OriginalScreen.Dogfight, shell.Screen);
        Assert.Equal(-1, shell.PickingSeat);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "P2  scripted   Hellhound  READY");
        Assert.True(Fly(shell).Enabled);

        shell.Step(Up);
        Assert.Equal(OriginalShell.FlyKey, shell.FocusedKey);
        var step = shell.Step(Accept);
        var launch = Assert.IsType<LaunchExit>(step.Exit);
        Assert.Equal(MenuMode.Versus, launch.Mode);
        Assert.Equal("C1", launch.Chapter);
        Assert.Equal(new[] { "player_autogyro", "player_avenger" }, launch.Seats.Select(s => s.PlaneNode));
        Assert.Empty(launch.Seats[0].Pads);
        Assert.Equal(new[] { 3 }, launch.Seats[1].Pads);
        Assert.Contains(OriginalCues.Click, step.Cues);
    }

    [Fact]
    public void FlyOnFreeFlightConfirmsSeatZeroAndLeavesThroughTheFeatureWithEverySeat()
    {
        var shell = Shell(out var setup, out var free);
        shell.Step(Accept);
        shell.Step(Down);
        shell.Step(Accept);
        shell.Step(Right);
        shell.Step(Accept);
        var second = setup.Join(new ScriptedMenuSeat())!;
        shell.Step(None);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        shell.StepSeat(1, Down);
        shell.StepSeat(1, Down);
        shell.StepSeat(1, Accept);
        shell.StepSeat(1, Accept);
        Assert.Equal(OriginalScreen.FreeFlight, shell.Screen);
        shell.Step(Up);
        shell.Step(Up);
        Assert.Equal(OriginalShell.FlyKey, shell.FocusedKey);

        var launch = Assert.IsType<LaunchExit>(shell.Step(Accept).Exit);

        Assert.Equal(MenuMode.Free, launch.Mode);
        Assert.Equal("C1B", launch.Chapter);
        Assert.Equal("C1B", free.Chapter?.Code);
        Assert.Equal(new[] { "player_avenger", "player_balmoral" }, launch.Seats.Select(s => s.PlaneNode));
        Assert.True(setup.Seats[0].Confirmed);
        Assert.Equal(2, second.Cursor);
    }

    [Fact]
    public void EachJoinedSeatPicksInPlayerOrderAndSeatZerosOwnControllerCanWalkTheScreen()
    {
        var shell = Shell(out var setup, out _);
        shell.Step(Accept);
        shell.Step(Accept);
        shell.Step(Right);
        shell.Step(Accept);
        var second = setup.Join(new ScriptedMenuSeat())!;
        var third = setup.Join(new ScriptedMenuSeat())!;
        shell.Step(None);
        Assert.Equal(1, shell.PickingSeat);

        // Seat 0's own Accept drives the picking seat: select, then confirm.
        shell.Step(Down);
        shell.Step(Accept);
        Assert.True(second.Locked);
        shell.Step(Accept);
        Assert.True(second.Confirmed);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        Assert.Equal(2, shell.PickingSeat);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "P3  scripted   choose your aircraft");

        // A pointer click on a list entry selects it for the seat, ACCEPT SELECTIONS confirms.
        var entry = shell.Rows.Single(r => r.Key == "ENTRY:2");
        shell.Step(Pointer(entry.X + 4f, entry.Y + 4f, pressed: true, clicked: true));
        Assert.True(third.Locked);
        Assert.Equal(2, third.Cursor);
        var accept = shell.Rows.Single(r => r.Key == "AcceptSelections");
        shell.Step(Pointer(accept.X + 4f, accept.Y + 4f, pressed: true, clicked: true));
        Assert.True(third.Confirmed);
        Assert.Equal(OriginalScreen.FreeFlight, shell.Screen);
        Assert.True(Fly(shell).Enabled);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "FLY when ready, or press START on a free pad to join");
    }

    [Fact]
    public void SeatZerosBackOnThePerSeatScreenUndoesItsOwnPickAndKeepsTheSeat()
    {
        var shell = Shell(out var setup, out _);
        shell.Step(Accept);
        shell.Step(Accept);
        shell.Step(Right);
        shell.Step(Accept);
        var second = setup.Join(new ScriptedMenuSeat())!;
        shell.Step(None);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);

        Assert.Null(shell.Step(Back).Exit);
        Assert.Equal(OriginalScreen.FreeFlight, shell.Screen);
        Assert.Null(shell.PickedAirframe);
        Assert.True(second.Joined);
        Assert.Equal(2, setup.Seats.Count);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "Pick an aircraft");

        // Picking again walks the seat again; a frame with nothing pressed changes nothing.
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        Assert.False(shell.Step(None).Changed);
    }

    [Fact]
    public void BackOnSeatZerosPickUndoesItBeforeLeavingTheScreen()
    {
        var shell = Shell(out var setup, out _);
        shell.Step(Accept);
        shell.Step(Accept);
        shell.Step(Right);
        shell.Step(Accept);
        Assert.NotNull(shell.PickedAirframe);

        Assert.Null(shell.Step(Back).Exit);
        Assert.Equal(OriginalScreen.FreeFlight, shell.Screen);
        Assert.Null(shell.PickedAirframe);
        Assert.Equal("C1", shell.PickedChapter);

        Assert.Null(shell.Step(Back).Exit);
        Assert.Equal(OriginalScreen.TopLevel, shell.Screen);
        Assert.Single(setup.Seats);
    }

    [Fact]
    public void AClickOnAnotherAircraftRePicksAndTheSameRowAgainChangesNothing()
    {
        var shell = Shell(out var setup, out _);
        shell.Open(OriginalScreen.FreeFlight);
        var first = shell.Rows.Single(r => r.Key == OriginalShell.AirframeKey(0));
        var third = shell.Rows.Single(r => r.Key == OriginalShell.AirframeKey(2));

        shell.Step(Pointer(first.X + 4f, first.Y + 4f, pressed: true, clicked: true));
        Assert.Equal("player_autogyro", shell.PickedAirframe);
        shell.Step(Pointer(first.X + 4f, first.Y + 4f, pressed: true, clicked: true));
        Assert.Equal("player_autogyro", shell.PickedAirframe);
        Assert.True(setup.Seats[0].Locked);

        shell.Step(Pointer(third.X + 4f, third.Y + 4f, pressed: true, clicked: true));
        Assert.Equal("player_balmoral", shell.PickedAirframe);
        Assert.True(setup.Seats[0].Locked);
        Assert.Contains(shell.Compose().Fills, f => f.X == third.X && f.Y == third.Y);
    }

    [Fact]
    public void ALaterSeatsBackUnselectsThenUnjoinsOnItsScreenAndUnjoinsFromAnyOtherScreenAtOnce()
    {
        var shell = Shell(out var setup, out _);
        var pad = new ScriptedMenuSeat();
        setup.Join(pad);

        Assert.True(shell.StepSeat(1, Back).Changed);
        Assert.Single(setup.Seats);
        Assert.False(shell.StepSeat(1, Back).Changed);

        shell.Step(Accept);
        shell.Step(Accept);
        shell.Step(Right);
        shell.Step(Accept);
        var second = setup.Join(pad)!;
        shell.Step(None);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        shell.StepSeat(1, Accept);
        Assert.True(second.Locked);
        Assert.Equal(3, shell.Rows.Count);
        shell.StepSeat(1, Back);
        Assert.False(second.Locked);
        Assert.True(second.Joined);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        Assert.True(shell.Rows.Count > 3);
        shell.StepSeat(1, Back);
        Assert.False(second.Joined);
        Assert.Single(setup.Seats);
        Assert.Equal(OriginalScreen.FreeFlight, shell.Screen);
        Assert.True(Fly(shell).Enabled);

        // Dogfight's FLY stays dark once the only other seat has left.
        shell.Step(Back);
        shell.Step(Back);
        shell.Step(Down);
        shell.Step(Accept);
        shell.Step(Accept);
        shell.Step(Right);
        shell.Step(Accept);
        setup.Join(pad);
        shell.Step(None);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        var cancel = shell.Rows.Single(r => r.Key == "CancelSelections");
        shell.Step(Pointer(cancel.X + 4f, cancel.Y + 4f, pressed: true, clicked: true));
        Assert.Single(setup.Seats);
        Assert.Equal(OriginalScreen.Dogfight, shell.Screen);
        Assert.False(Fly(shell).Enabled);
    }

    [Fact]
    public void FlyMissionWithASecondSeatWalksItThroughThePerSeatScreenThenLaunchesBoth()
    {
        PlayerSetupFeature setup = null!;
        var shell = Shell(out setup, out _, seat => ReferenceEquals(seat, setup.Seats[1]) ? new[] { 2 } : Array.Empty<int>());
        shell.OpenInstantAction();
        Assert.DoesNotContain(shell.Compose().Overlays.SelectMany(o => o.Lines), l => l.Text.StartsWith("P1", StringComparison.Ordinal));
        var second = setup.Join(new ScriptedMenuSeat())!;
        Assert.Contains(shell.Compose().Overlays.SelectMany(o => o.Lines), l => l.Text == "P2  scripted");

        var fly = shell.Rows.Single(r => r.Key == OriginalShell.FlyMissionKey);
        Assert.Null(shell.Step(Pointer(fly.X + 4f, fly.Y + 4f, pressed: true, clicked: true)).Exit);
        Assert.Equal(OriginalScreen.SeatPlane, shell.Screen);
        Assert.Equal(1, shell.PickingSeat);
        Assert.Equal(OriginalScreen.InstantAction, shell.SeatReturn);
        Assert.True(setup.Seats[0].Locked);
        Assert.Same(shell.PilotRoster, setup.Roster);

        shell.StepSeat(1, Down);
        shell.StepSeat(1, Down);
        shell.StepSeat(1, Accept);
        Assert.True(second.Locked);
        var launch = Assert.IsType<LaunchExit>(shell.StepSeat(1, Accept).Exit);
        Assert.Equal(OriginalScreen.InstantAction, shell.Screen);
        Assert.NotNull(launch.InstantAction);
        Assert.Equal(2, launch.Seats.Count);
        Assert.Equal(setup.Roster[shell.PilotRow].Node, launch.Seats[0].PlaneNode);
        Assert.Equal("player_balmoral", launch.Seats[1].PlaneNode);
        Assert.Equal(new[] { 2 }, launch.Seats[1].Pads);
        Assert.True(setup.Seats[0].Confirmed && second.Confirmed);
    }

    [Fact]
    public void TheAircraftColumnIsAWindowThatFollowsTheFocusAndHidesTheRest()
    {
        var customs = Enumerable.Range(1, 7).Select(i => new CustomPlaneDef { Name = $"Custom {i}", Airframe = i }).ToArray();
        var shell = Shell(out _, out _, customs: customs);
        shell.Step(Accept);
        var column = shell.Rows.Where(r => r.Column == 1 && r.Kind == OriginalRowKind.ListRow).ToList();
        Assert.Equal(18, column.Count);
        Assert.Equal(OriginalShell.AirframeWindow, column.Count(r => r.Visible));
        Assert.Equal(0, shell.AirframeTop);
        Assert.DoesNotContain(shell.Compose().Lines, l => l.Text == "▲");
        Assert.Contains(shell.Compose().Lines, l => l.Text == "▼");
        Assert.DoesNotContain(shell.Compose().Lines, l => l.Text == "Custom 7");

        shell.Step(Right);
        for (int i = 0; i < OriginalShell.AirframeWindow; i++)
        {
            shell.Step(Down);
        }

        Assert.Equal(OriginalShell.AirframeKey(11), shell.FocusedKey);
        Assert.Equal(1, shell.AirframeTop);
        var hidden = shell.Rows.Single(r => r.Key == OriginalShell.AirframeKey(0));
        Assert.False(hidden.Visible);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "▲");
        Assert.DoesNotContain(shell.Compose().Lines, l => l.Text == "Autogyro");

        // The hidden row's rectangle is above the window; a pointer there hits nothing.
        shell.Step(Pointer(hidden.X + 4f, hidden.Y + 4f));
        Assert.Equal(-1, shell.Hover);

        for (int i = 0; i < 6; i++)
        {
            shell.Step(Down);
        }

        Assert.Equal(OriginalShell.AirframeKey(17), shell.FocusedKey);
        Assert.Equal(7, shell.AirframeTop);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "Custom 7");
        Assert.DoesNotContain(shell.Compose().Lines, l => l.Text == "▼");

        // FLY is disabled with nothing picked, so Down wraps past it onto the first row and the
        // window follows.
        shell.Step(Down);
        Assert.Equal(OriginalShell.AirframeKey(0), shell.FocusedKey);
        Assert.Equal(0, shell.AirframeTop);
    }

    [Fact]
    public void AReturnToTheTopLevelResetsEverySeatsPickAndKeepsTheSeats()
    {
        var shell = Shell(out var setup, out _);
        shell.Step(Accept);
        shell.Step(Accept);
        shell.Step(Right);
        shell.Step(Accept);
        var second = setup.Join(new ScriptedMenuSeat())!;
        shell.Step(None);
        shell.StepSeat(1, Accept);
        shell.StepSeat(1, Accept);

        shell.ReturnToTopLevel();

        Assert.Equal(2, setup.Seats.Count);
        Assert.Null(shell.PickedAirframe);
        Assert.False(second.Locked || second.Confirmed);
        Assert.Equal("C1", shell.PickedChapter);
        Assert.Equal(-1, shell.PickingSeat);
    }

    private static OriginalRow Fly(OriginalShell shell) => shell.Rows.Single(r => r.Key == OriginalShell.FlyKey);

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
}
