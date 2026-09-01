using System.Linq;
using CSVM.UI;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The Original presentation's engine-free shell over the hand-authored layout fixture: the top
/// level's rows and states, pointer hit-testing and rollover, keyboard and pad focus by column,
/// the Free Flight picks and launch, the Options chooser, the exits, and the composition each
/// state draws. Every rectangle here is the fixture's invented geometry; the game's is read the
/// same way.
/// </summary>
public class OriginalShellTests
{
    private const float Dt = 1f / 60f;

    private static readonly MenuCommands Accept = new() { Accept = true };
    private static readonly MenuCommands Back = new() { Back = true };
    private static readonly MenuCommands Down = new() { MoveY = 1 };
    private static readonly MenuCommands Up = new() { MoveY = -1 };
    private static readonly MenuCommands Right = new() { MoveX = 1 };

    [Fact]
    public void TheTopLevelIsTheSixDecodedRowsPlusTheFreeFlightDoorAndOpensFocusedOnTheDoor()
    {
        var shell = Shell(out _);

        Assert.Equal(OriginalScreen.TopLevel, shell.Screen);
        Assert.Equal(
            new[] { OriginalShell.FreeFlightKey, OriginalShell.DogfightKey, "MM_B_CAMPAIGN", "MM_B_INSTANTACTION", "MM_B_MULTIPLAYER", "MM_B_PREFERENCES", "MM_B_CREDITS", "MM_B_QUIT" },
            shell.Rows.Select(r => r.Key));
        Assert.Equal(OriginalShell.FreeFlightKey, shell.FocusedKey);
        // The decoded rows with no remake destination yet are disabled; Preferences (the Options
        // door) and Quit react.
        Assert.Equal(new[] { true, true, false, false, false, true, false, true }, shell.Rows.Select(r => r.Enabled));
        // A decoded button's rectangle is its authored corner and its measured strip's frame.
        var quit = shell.Rows.Single(r => r.Key == "MM_B_QUIT");
        Assert.Equal((280f, 530f, 240f, 50f), (quit.X, quit.Y, quit.Width, quit.Height));
        Assert.Equal(4, quit.Art!.Frames);
    }

    [Fact]
    public void ThePointerHoveringALiveButtonTakesFocusAndCuesARolloverOnceAndADisabledOneDoesNeither()
    {
        var shell = Shell(out _);

        var step = shell.Step(Pointer(290f, 540f));
        Assert.Equal("MM_B_QUIT", shell.FocusedKey);
        Assert.Equal(new[] { OriginalCues.Rollover }, step.Cues);
        Assert.True(step.Changed);

        step = shell.Step(Pointer(295f, 545f));
        Assert.Empty(step.Cues);

        step = shell.Step(Pointer(290f, 290f));
        Assert.Equal("MM_B_QUIT", shell.FocusedKey);
        Assert.Empty(step.Cues);
        Assert.Equal(2, shell.Hover);
    }

    [Fact]
    public void AClickOnTheDoorOpensFreeFlightWithAClickCueAndThePressedFrameDrawsWhileHeld()
    {
        var shell = Shell(out _);
        var door = shell.Rows[0];

        shell.Step(Pointer(door.X + 2f, door.Y + 2f, pressed: true));
        var plaque = shell.Compose().Plaques.Single(p => p.Label == "FREE FLIGHT");
        Assert.Equal(3, plaque.Frame);
        Assert.Equal(BoardInk.LabelActivate, plaque.Ink);

        var step = shell.Step(Pointer(door.X + 2f, door.Y + 2f, pressed: true, clicked: true));
        Assert.Equal(OriginalScreen.FreeFlight, shell.Screen);
        Assert.Contains(OriginalCues.Click, step.Cues);
        Assert.Null(step.Exit);
    }

    [Fact]
    public void KeyboardFocusWalksEnabledRowsWithinAColumnAndWrapsAndSkipsDisabledOnes()
    {
        var shell = Shell(out _);

        shell.Step(Down);
        Assert.Equal(OriginalShell.DogfightKey, shell.FocusedKey);
        shell.Step(Down);
        Assert.Equal("MM_B_PREFERENCES", shell.FocusedKey);
        shell.Step(Down);
        Assert.Equal("MM_B_QUIT", shell.FocusedKey);
        shell.Step(Down);
        Assert.Equal(OriginalShell.FreeFlightKey, shell.FocusedKey);
        shell.Step(Up);
        Assert.Equal("MM_B_QUIT", shell.FocusedKey);
    }

    [Fact]
    public void FreeFlightPicksAChapterAndAnAirframeAcrossTwoColumnsThenFliesThroughTheFeature()
    {
        var shell = Shell(out var free);
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.FreeFlight, shell.Screen);
        Assert.Equal("C1", shell.FocusedKey);
        Assert.False(shell.Rows.Single(r => r.Key == OriginalShell.FlyKey).Enabled);

        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Accept);
        Assert.Equal("C1C", shell.PickedChapter);
        Assert.Equal("C1C", free.Chapter?.Code);

        shell.Step(Right);
        Assert.Equal(1, shell.Rows[shell.Focus].Column);
        Assert.Equal(OriginalShell.AirframeKey(2), shell.FocusedKey);
        shell.Step(Accept);
        Assert.Equal("player_balmoral", shell.PickedAirframe);
        Assert.True(shell.Rows.Single(r => r.Key == OriginalShell.FlyKey).Enabled);

        // Up from the top of the airframe column wraps onto FLY, the column's last row.
        shell.Step(Up);
        shell.Step(Up);
        shell.Step(Up);
        Assert.Equal(OriginalShell.FlyKey, shell.FocusedKey);
        var step = shell.Step(Accept);
        var launch = Assert.IsType<LaunchExit>(step.Exit);
        Assert.Equal("C1C", launch.Chapter);
        Assert.Equal(MenuMode.Free, launch.Mode);
        Assert.Equal("player_balmoral", Assert.Single(launch.Seats).PlaneNode);
        Assert.Contains(OriginalCues.Click, step.Cues);
    }

    [Fact]
    public void ListRowsUnderThePointerTakeFocusWithoutARolloverCueAndAClickPicks()
    {
        var shell = Shell(out _);
        shell.Open(OriginalScreen.FreeFlight);
        var hawaii = shell.Rows.Single(r => r.Key == "C3");

        var step = shell.Step(Pointer(hawaii.X + 10f, hawaii.Y + 5f));
        Assert.Equal("C3", shell.FocusedKey);
        Assert.Empty(step.Cues);

        step = shell.Step(Pointer(hawaii.X + 10f, hawaii.Y + 5f, pressed: true, clicked: true));
        Assert.Equal("C3", shell.PickedChapter);
        Assert.Empty(step.Cues);
        Assert.Contains(shell.Compose().Fills, f => f.X == hawaii.X && f.Y == hawaii.Y && !f.Border);
    }

    [Fact]
    public void BackLeavesFreeFlightForTheTopLevelAndQuitsFromTheTopLevel()
    {
        var shell = Shell(out _);
        shell.Step(Accept);
        shell.Step(Down);
        shell.Step(Accept);

        var step = shell.Step(Back);
        Assert.Equal(OriginalScreen.TopLevel, shell.Screen);
        Assert.Null(step.Exit);
        Assert.Equal("C1B", shell.PickedChapter);

        step = shell.Step(Back);
        Assert.IsType<QuitExit>(step.Exit);

        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Down);
        Assert.Equal("MM_B_QUIT", shell.FocusedKey);
        Assert.IsType<QuitExit>(shell.Step(Accept).Exit);
    }

    [Fact]
    public void AReturnToTheTopLevelKeepsTheListCursorsAndDropsTheAirframePick()
    {
        var shell = Shell(out _);
        shell.Step(Accept);
        shell.Step(Down);
        shell.Step(Accept);
        shell.Step(Right);
        shell.Step(Down);
        shell.Step(Accept);
        Assert.NotNull(shell.PickedAirframe);

        shell.ReturnToTopLevel();

        Assert.Equal(OriginalScreen.TopLevel, shell.Screen);
        Assert.Null(shell.PickedAirframe);
        Assert.Equal("C1B", shell.PickedChapter);
        shell.Step(Accept);
        Assert.Equal(OriginalShell.AirframeKey(2), shell.FocusedKey);
    }

    [Fact]
    public void TheOptionsScreenTogglesThePresentationAndAppliesItAsASwitchExit()
    {
        var shell = Shell(out _);
        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.Options, shell.Screen);
        Assert.Equal(PresentationId.Original.Value, shell.PresentationChoice);
        Assert.Contains(shell.Compose().Plaques, p => p.Label == "MENU: ORIGINAL");

        shell.Step(Accept);
        Assert.Equal(PresentationId.BuiltIn.Value, shell.PresentationChoice);
        Assert.Contains(shell.Compose().Plaques, p => p.Label == "MENU: BUILT-IN");

        shell.Step(Down);
        var step = shell.Step(Accept);
        var exit = Assert.IsType<PresentationSwitchExit>(step.Exit);
        Assert.Equal(PresentationId.BuiltIn, exit.Requested);

        shell.Step(Down);
        Assert.Equal(OriginalShell.BackKey, shell.FocusedKey);
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.TopLevel, shell.Screen);
    }

    [Fact]
    public void TheTopLevelComposesTheDecodedPanesAndPlaquesInTheirStateFramesAndThePointerLast()
    {
        var shell = Shell(out _);
        shell.Step(Pointer(290f, 440f));

        var board = shell.Compose();
        Assert.Equal(new[] { "PM_Logo.png", "PM_Frame.png" }, board.Pictures.Select(p => p.Art.Name));
        Assert.Equal((100f, 10f), (board.Pictures[0].X, board.Pictures[0].Y));
        var campaign = board.Plaques.Single(p => p.Art.Name == "PM_B_Campaign.png");
        Assert.Equal(0, campaign.Frame);
        var preferences = board.Plaques.Single(p => p.Art.Name == "PM_B_Preferences.png");
        Assert.Equal(2, preferences.Frame);
        var quit = board.Plaques.Single(p => p.Art.Name == "PM_B_Quit.png");
        Assert.Equal(1, quit.Frame);
        var door = board.Plaques.Single(p => p.Label == "FREE FLIGHT");
        Assert.Equal("PM_B_Paper.png", door.Art.Name);
        Assert.Equal(BoardInk.LabelNormal, door.Ink);
        var pointer = Assert.Single(board.Overlays);
        var picture = Assert.Single(pointer.Pictures);
        Assert.Equal("activepointerz.png", picture.Art.Name);
        Assert.Equal((290f, 440f), (picture.X, picture.Y));

        shell.Step(Pointer(10f, 10f));
        Assert.Equal("passivepointerz.png", Assert.Single(shell.Compose().Overlays).Pictures[0].Art.Name);
    }

    [Fact]
    public void TheInksComeOffTheLayoutsGlobalsAndThePaperPlaquesOwnTail()
    {
        var shell = Shell(out _);

        Assert.Equal(new MenuLayoutColor(0xFF, 0xBC, 0xBC, 0xBC), shell.Inks.Disabled);
        Assert.Equal(new MenuLayoutColor(0xFF, 0xFF, 0xFF, 0xFF), shell.Inks.Active);
        Assert.Equal(new MenuLayoutColor(0xFF, 0x13, 0x3D, 0x77), shell.Inks.LabelNormal);
        Assert.Equal(new MenuLayoutColor(0xFF, 0x37, 0x4D, 0x6B), shell.Inks.LabelRollover);
        Assert.Equal(new MenuLayoutColor(0xFF, 0x00, 0x00, 0x00), shell.Inks.LabelDepressed);
    }

    [Fact]
    public void ALayoutWithNoPlaqueRowStillComposesTextButtonsAsOutlinedLabels()
    {
        var layout = MenuLayout.Parse(
            "{\"schema\":1,\"widgetTypes\":[],\"globals\":[],\"screens\":[{\"section\":\"MainMenu\",\"script\":\"\",\"widgets\":[]}],\"navigation\":[],\"externalAssets\":[]}");
        var shell = new OriginalShell(layout, new FreeFlightFeature(), new PlayerSetupFeature(), _ => null);

        var rows = shell.Rows;
        Assert.Equal(new[] { OriginalShell.FreeFlightKey, OriginalShell.DogfightKey }, rows.Select(r => r.Key));
        var board = shell.Compose();
        Assert.Empty(board.Plaques);
        Assert.Contains(board.Lines, l => l.Text == "FREE FLIGHT");
        Assert.Contains(board.Lines, l => l.Text == "DOGFIGHT");
        Assert.Contains(board.Fills, f => f.Border);
        Assert.Equal(new MenuLayoutColor(0xFF, 0xFF, 0xFF, 0xFF), shell.Inks.LabelNormal);
    }

    // Seat 0 is a scripted source; the roster is the eleven stock airframes with no customs.
    private static OriginalShell Shell(out FreeFlightFeature free)
    {
        free = new FreeFlightFeature();
        var setup = new PlayerSetupFeature();
        setup.SetRoster(OriginalPresentation.Roster(System.Array.Empty<CSVM.Flight.CustomPlaneDef>()));
        setup.Join(new ScriptedMenuSeat());
        return new OriginalShell(MenuLayoutReaderTests.OriginalLayout(), free, setup, Measure);
    }

    // The fixture's strips: every button strip 240x200 (four 50-pixel frames), the paper plaque
    // 160x112 (four 28-pixel frames), the panes unmeasured.
    private static (int Width, int Height)? Measure(string art) => art switch
    {
        "PM_B_Paper.png" => (160, 112),
        _ when art.StartsWith("PM_B_", System.StringComparison.Ordinal) => (240, 200),
        _ => null,
    };

    private static MenuCommands Pointer(float x, float y, bool pressed = false, bool clicked = false) =>
        new() { Pointer = new MenuPointer(x, y, pressed, clicked) };
}
