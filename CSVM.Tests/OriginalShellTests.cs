using System.Linq;
using CSVM.UI;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using CSVM.Utils;
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
            new[] { OriginalShell.FreeFlightKey, OriginalShell.DogfightKey, OriginalShell.HangarKey, "MM_B_CAMPAIGN", "MM_B_INSTANTACTION", "MM_B_MULTIPLAYER", "MM_B_PREFERENCES", "MM_B_CREDITS", "MM_B_QUIT" },
            shell.Rows.Select(r => r.Key));
        Assert.Equal(OriginalShell.FreeFlightKey, shell.FocusedKey);
        // The decoded rows with no remake destination yet are disabled; Instant Action,
        // Preferences (the Options door) and Quit react. The hangar door stands only over a
        // saved-plane store, which this shell has none of.
        Assert.Equal(new[] { true, true, false, false, true, false, true, false, true }, shell.Rows.Select(r => r.Enabled));
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
        Assert.Equal(3, shell.Hover);
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
        Assert.Equal("MM_B_INSTANTACTION", shell.FocusedKey);
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
    public void TheGameOptionsPageTakesBothChoicesAndAppliesThemAsOneExit()
    {
        var shell = Shell(out _);
        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.Options, shell.Screen);
        Assert.Equal(OriginalShell.GameOptionsDoorKey, shell.FocusedKey);
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.GameOptions, shell.Screen);
        Assert.Equal(PresentationId.Original.Value, shell.PresentationChoice);
        Assert.Equal(GraphicsMode.Default, shell.GraphicsChoice);
        Assert.Equal(OriginalShell.PresentationKey, shell.FocusedKey);

        // Accept on the closed dropdown opens its list; picking the second item closes it and
        // leaves the focus on the box, now reading the other token.
        shell.Step(Accept);
        Assert.Equal(OriginalShell.PresentationKey, shell.OpenGameOption);
        Assert.Equal(new[] { "ORIGINAL", "BUILT-IN" }, shell.Rows.Select(r => r.Label));
        shell.Step(Down);
        shell.Step(Accept);
        Assert.Null(shell.OpenGameOption);
        Assert.Equal(PresentationId.BuiltIn.Value, shell.PresentationChoice);
        Assert.Equal("BUILT-IN", shell.Rows.Single(r => r.Key == OriginalShell.PresentationKey).Label);

        // A sideways step on the closed box takes the next token with wrap.
        shell.Step(Right);
        Assert.Equal(PresentationId.Original.Value, shell.PresentationChoice);
        shell.Step(Right);
        Assert.Equal(PresentationId.BuiltIn.Value, shell.PresentationChoice);

        shell.Step(Down);
        Assert.Equal(OriginalShell.GraphicsKey, shell.FocusedKey);
        shell.Step(Accept);
        Assert.Equal(GraphicsMode.EnhancedWord, shell.GraphicsChoice);
        Assert.Equal(4 + 2, shell.Compose().Plaques.Single(p => p.Art.Name == "PP_B_Check8.png").Frame);

        shell.Step(Down);
        Assert.Equal(OriginalShell.GameOptionsAcceptKey, shell.FocusedKey);
        var step = shell.Step(Accept);
        var exit = Assert.IsType<OptionsApplyExit>(step.Exit);
        Assert.Equal(PresentationId.BuiltIn, exit.Presentation);
        Assert.Equal(GraphicsMode.EnhancedWord, exit.Graphics);
    }

    [Fact]
    public void CancelChangesAndBackBothDropTheEditsAndReturnToPreferences()
    {
        var shell = Shell(out _);
        shell.OpenGameOptions();
        shell.Step(Right);
        Assert.Equal(PresentationId.BuiltIn.Value, shell.PresentationChoice);

        shell.Step(Down);
        Assert.Equal(OriginalShell.GraphicsKey, shell.FocusedKey);
        shell.Step(Down);
        shell.Step(Down);
        Assert.Equal(OriginalShell.GameOptionsCancelKey, shell.FocusedKey);
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.Options, shell.Screen);
        Assert.Equal(PresentationId.Original.Value, shell.PresentationChoice);

        // Back on the open list closes it; the next Back is CANCEL CHANGES.
        shell.OpenGameOptions();
        shell.Step(Accept);
        Assert.Equal(OriginalShell.PresentationKey, shell.OpenGameOption);
        shell.Step(Back);
        Assert.Null(shell.OpenGameOption);
        Assert.Equal(OriginalScreen.GameOptions, shell.Screen);
        shell.Step(Back);
        Assert.Equal(OriginalScreen.Options, shell.Screen);
    }

    /// <summary>Opening the page shows back the saved words, not what this process resolved: a
    /// flag or the config key can have decided the running mode, and the rows owe the player the
    /// choices their own ACCEPT CHANGES saved. The store is a scratch one; the shell never writes it.</summary>
    [Fact]
    public void TheRowsOpenOnTheSavedWords()
    {
        var saved = new OptionsDef { GraphicsMode = GraphicsMode.EnhancedWord, MenuPresentation = PresentationId.BuiltIn.Value };
        var shell = Shell(out _, () => saved);
        shell.OpenGameOptions();

        Assert.Equal(GraphicsMode.EnhancedWord, shell.GraphicsChoice);
        Assert.Equal(PresentationId.BuiltIn.Value, shell.PresentationChoice);
        Assert.Equal("BUILT-IN", shell.Rows.Single(r => r.Key == OriginalShell.PresentationKey).Label);

        // A file that never set the fields opens the rows on the shipped defaults.
        saved.GraphicsMode = null;
        saved.MenuPresentation = null;
        shell.OpenGameOptions();
        Assert.Equal(GraphicsMode.Default, shell.GraphicsChoice);
        Assert.Equal(PresentationId.Original.Value, shell.PresentationChoice);
    }

    [Fact]
    public void TheOptionsScreenIsComposedOverThePreferencesChromeWithOnlyItsGameOptionsDoorLive()
    {
        var shell = Shell(out _);
        shell.Open(OriginalScreen.Options);

        // The four decoded page doors at their authored corners, the first live and the other
        // three disabled, then the section's own RETURN TO MAIN MENU.
        Assert.Equal(
            new[] { "PF_B_GAMEOPTIONS", "PF_B_AUDIO", "PF_B_VIDEO", "PF_B_CONTROLS", OriginalShell.OptionsBackKey },
            shell.Rows.Select(r => r.Key));
        Assert.Equal(new[] { true, false, false, false, true }, shell.Rows.Select(r => r.Enabled));
        var back = shell.Rows.Single(r => r.Key == OriginalShell.OptionsBackKey);
        Assert.Equal((460f, 500f, 240f, 50f), (back.X, back.Y, back.Width, back.Height));

        var board = shell.Compose();
        Assert.Equal(new[] { "PM_Logo.png", "PP_Back.png" }, board.Pictures.Select(p => p.Art.Name));
        Assert.Equal((100f, 200f), (board.Pictures[1].X, board.Pictures[1].Y));
        Assert.Contains(board.Lines, l => l.Text == "PREFERENCES" && l.X == 120f && l.Justify == BoardJustify.Center);
        Assert.Contains(board.Lines, l => l.Text == "Change the audio settings." && l.X == 340f && l.Y == 320f);
        Assert.Contains(board.Lines, l => l.Text == "Change the difficulty level and default views." && l.X == 340f);
        Assert.Equal(0, board.Plaques.Single(p => p.Art.Name == "PP_B_Audio.png").Frame);
        Assert.Equal(2, board.Plaques.Single(p => p.Art.Name == "PP_B_GameOptions.png").Frame);
        Assert.Equal(new MenuLayoutColor(0xFF, 0xFF, 0xDD, 0xC4), shell.PreferencesInks.Text);
        Assert.Equal(new MenuLayoutColor(0xFF, 0xC0, 0xBA, 0xAD), shell.PreferencesInks.Title);

        // A click on RETURN TO MAIN MENU leaves; Back leaves too.
        shell.Step(Pointer(back.X + 2f, back.Y + 2f, pressed: true, clicked: true));
        Assert.Equal(OriginalScreen.TopLevel, shell.Screen);
        shell.Open(OriginalScreen.Options);
        shell.Step(Back);
        Assert.Equal(OriginalScreen.TopLevel, shell.Screen);
    }

    [Fact]
    public void TheGameOptionsPageIsComposedOverItsSectionsOwnRowShape()
    {
        var shell = Shell(out _);
        shell.OpenGameOptions();

        // Row one's dropdown box at the authored corner and width, row two's checkbox at the
        // head-turn box's offset from its own row, then the two decoded plaques.
        var menu = shell.Rows.Single(r => r.Key == OriginalShell.PresentationKey);
        Assert.Equal((135f, 295f, 144f, 17f), (menu.X, menu.Y, menu.Width, menu.Height));
        var graphics = shell.Rows.Single(r => r.Key == OriginalShell.GraphicsKey);
        Assert.Equal((250f, 340f, 16f, 16f), (graphics.X, graphics.Y, graphics.Width, graphics.Height));
        var accept = shell.Rows.Single(r => r.Key == OriginalShell.GameOptionsAcceptKey);
        Assert.Equal((200f, 470f, 240f, 50f), (accept.X, accept.Y, accept.Width, accept.Height));

        var board = shell.Compose();
        Assert.Equal(new[] { "PM_Logo.png", "PP_GoBack.png" }, board.Pictures.Select(p => p.Art.Name).Take(2));
        Assert.Contains(board.Lines, l => l.Text == "GAME OPTIONS" && l.X == 120f && l.Justify == BoardJustify.Center);
        Assert.Contains(board.Lines, l => l.Text == "Menu" && l.X == 130f && l.Y == 280f && l.Width == 170f);
        Assert.Contains(board.Lines, l => l.Text == "Select the menu presentation." && l.X == 340f && l.Y == 290f && l.Width == 310f);
        Assert.Contains(board.Lines, l => l.Text == "Enhanced Graphics" && l.X == 130f && l.Y == 340f && l.Width == 112f);
        Assert.Contains(board.Lines, l => l.Text.StartsWith("Select the lit world.", System.StringComparison.Ordinal) && l.Y == 350f);
        // The third authored row is left empty: the page draws two titles, two descriptions and
        // its own tab title, and nothing at the Auto Head Turn line.
        Assert.Equal(5, board.Lines.Count(l => l.Row < 0));
        // The checkbox draws unchecked and unfocused: the second of its eight frames.
        Assert.Equal(1, board.Plaques.Single(p => p.Art.Name == "PP_B_Check8.png").Frame);
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
        Assert.Equal(new[] { OriginalShell.FreeFlightKey, OriginalShell.DogfightKey, OriginalShell.HangarKey }, rows.Select(r => r.Key));
        var board = shell.Compose();
        Assert.Empty(board.Plaques);
        Assert.Contains(board.Lines, l => l.Text == "FREE FLIGHT");
        Assert.Contains(board.Lines, l => l.Text == "DOGFIGHT");
        Assert.Contains(board.Fills, f => f.Border);
        Assert.Equal(new MenuLayoutColor(0xFF, 0xFF, 0xFF, 0xFF), shell.Inks.LabelNormal);
    }

    // Seat 0 is a scripted source; the roster is the eleven stock airframes with no customs. No
    // options reader by default, so the Options screen opens on the defaults and no test can reach
    // the player's own user://options.json.
    private static OriginalShell Shell(out FreeFlightFeature free, System.Func<OptionsDef>? options = null)
    {
        free = new FreeFlightFeature();
        var setup = new PlayerSetupFeature();
        setup.SetRoster(OriginalPresentation.Roster(System.Array.Empty<CSVM.Flight.CustomPlaneDef>()));
        setup.Join(new ScriptedMenuSeat());
        return new OriginalShell(MenuLayoutReaderTests.OriginalLayout(), free, setup, Measure, options: options);
    }

    // The fixture's strips: every button strip 240x200 (four 50-pixel frames), the paper plaque
    // 160x112 (four 28-pixel frames), the checkbox 16x128 (eight 16-pixel frames), the dropdown
    // arrows 15x56, the panes unmeasured.
    private static (int Width, int Height)? Measure(string art) => art switch
    {
        "PM_B_Paper.png" => (160, 112),
        "PP_B_Check8.png" => (16, 128),
        "PP_B_DropUp.png" or "PP_B_DropDown.png" => (15, 56),
        _ when art.StartsWith("PM_B_", System.StringComparison.Ordinal) => (240, 200),
        _ when art.StartsWith("PP_B_", System.StringComparison.Ordinal) => (240, 200),
        _ => null,
    };

    private static MenuCommands Pointer(float x, float y, bool pressed = false, bool clicked = false) =>
        new() { Pointer = new MenuPointer(x, y, pressed, clicked) };
}
