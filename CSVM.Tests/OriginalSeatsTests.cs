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
/// fixture: the Dogfight door and its gate, a second seat walking, selecting and confirming on
/// the aircraft column with its tag drawn, FLY as seat 0's confirmation and the launch, Back
/// undoing seat 0's pick before leaving, a guest's Back unjoining, the hint at each stage, and
/// the aircraft window over a roster with saved customs.
/// </summary>
public class OriginalSeatsTests
{
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
    public void ASecondSeatWalksSelectsAndConfirmsOnTheAircraftColumnAndFlyLaunchesTheDogfightForBoth()
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

        var walk = shell.StepSeat(1, Down);
        Assert.True(walk.Changed);
        Assert.Equal(1, second.Cursor);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "P2" && l.Justify == BoardJustify.Right);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "P2  scripted   choosing");

        shell.StepSeat(1, Accept);
        Assert.True(second.Locked);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "P2 ✓");
        Assert.Contains(shell.Compose().Lines, l => l.Text == "Waiting for P2 to confirm (A again)");
        Assert.False(Fly(shell).Enabled);
        Assert.Null(shell.Step(Accept).Exit);

        shell.StepSeat(1, Accept);
        Assert.True(second.Confirmed);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "P2 ✓✓");
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
        shell.StepSeat(1, Down);
        shell.StepSeat(1, Down);
        shell.StepSeat(1, Accept);
        shell.StepSeat(1, Accept);
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
    public void ALaterSeatsBackUnselectsThenUnjoinsAndUnjoinsFromAnyOtherScreenAtOnce()
    {
        var shell = Shell(out var setup, out _);
        var pad = new ScriptedMenuSeat();
        setup.Join(pad);

        Assert.True(shell.StepSeat(1, Back).Changed);
        Assert.Single(setup.Seats);
        Assert.False(shell.StepSeat(1, Back).Changed);

        shell.Step(Accept);
        var second = setup.Join(pad)!;
        shell.StepSeat(1, Accept);
        Assert.True(second.Locked);
        shell.StepSeat(1, Back);
        Assert.False(second.Locked);
        Assert.True(second.Joined);
        shell.StepSeat(1, Back);
        Assert.False(second.Joined);
        Assert.Single(setup.Seats);
        Assert.Equal(OriginalScreen.FreeFlight, shell.Screen);
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
        shell.StepSeat(1, Accept);
        shell.StepSeat(1, Accept);

        shell.ReturnToTopLevel();

        Assert.Equal(2, setup.Seats.Count);
        Assert.Null(shell.PickedAirframe);
        Assert.False(second.Locked || second.Confirmed);
        Assert.Equal("C1", shell.PickedChapter);
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
