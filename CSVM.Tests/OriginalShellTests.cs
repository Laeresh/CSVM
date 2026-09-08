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
    private static readonly MenuCommands Left = new() { MoveX = -1 };

    [Fact]
    public void TheTopLevelIsTheSixDecodedRowsPlusTheFreeFlightDoorAndOpensFocusedOnTheDoor()
    {
        var shell = Shell(out _);

        Assert.Equal(OriginalScreen.TopLevel, shell.Screen);
        Assert.Equal(
            new[] { OriginalShell.FreeFlightKey, OriginalShell.DogfightKey, "MM_B_CAMPAIGN", "MM_B_INSTANTACTION", "MM_B_MULTIPLAYER", "MM_B_PREFERENCES", "MM_B_CREDITS", "MM_B_QUIT" },
            shell.Rows.Select(r => r.Key));
        Assert.Equal(OriginalShell.FreeFlightKey, shell.FocusedKey);
        // The decoded rows with no remake destination yet are disabled; Instant Action,
        // Preferences (the Options door), Credits and Quit react. The hangar is reached through
        // Instant Action's Build Custom Plane, so no door of its own stands here.
        Assert.Equal(new[] { true, true, false, true, false, true, true, true }, shell.Rows.Select(r => r.Enabled));
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
        Assert.Equal("MM_B_INSTANTACTION", shell.FocusedKey);
        shell.Step(Down);
        Assert.Equal("MM_B_PREFERENCES", shell.FocusedKey);
        shell.Step(Down);
        Assert.Equal(OriginalShell.CreditsDoorKey, shell.FocusedKey);
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
    public void TheGameOptionsPageTakesEveryChoiceAndAppliesThemAsOneExit()
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
        Assert.Equal(CSVM.Flight.Difficulty.Normal, shell.DifficultyChoice);
        Assert.Equal(PresentationId.Original.Value, shell.PresentationChoice);
        Assert.Equal(GraphicsMode.Default, shell.GraphicsChoice);
        Assert.Equal(OriginalShell.DifficultyKey, shell.FocusedKey);

        // The first row is the original's own Difficulty dropdown over the three campaign tiers:
        // its list opens on Accept, and the third item picks Hardest.
        shell.Step(Accept);
        Assert.Equal(OriginalShell.DifficultyKey, shell.OpenGameOption);
        Assert.Equal(new[] { "Normal", "Hard", "Hardest" }, shell.Rows.Select(r => r.Label));
        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Accept);
        Assert.Null(shell.OpenGameOption);
        Assert.Equal(CSVM.Flight.Difficulty.Hardest, shell.DifficultyChoice);
        Assert.Equal("Hardest", shell.Rows.Single(r => r.Key == OriginalShell.DifficultyKey).Label);
        // A sideways step wraps back onto Normal, then on to Hard.
        shell.Step(Right);
        Assert.Equal(CSVM.Flight.Difficulty.Normal, shell.DifficultyChoice);
        shell.Step(Right);
        Assert.Equal(CSVM.Flight.Difficulty.Hard, shell.DifficultyChoice);

        shell.Step(Down);
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
        Assert.Equal(OriginalShell.GameOptionsAcceptKey, shell.FocusedKey);
        var step = shell.Step(Accept);
        var exit = Assert.IsType<OptionsApplyExit>(step.Exit);
        Assert.Equal(PresentationId.BuiltIn, exit.Presentation);
        // The graphics word rides this page's apply unchanged: it is the VIDEO page's row now, and
        // the apply carries every saved choice whichever page sends it.
        Assert.Equal(GraphicsMode.Default, exit.Graphics);
        Assert.Equal("hard", exit.Difficulty);
    }

    /// <summary>The AUDIO page behind the Preferences page's second door: it opens on the saved
    /// mix with Master focused, a sideways step moves the focused level and clamps rather than
    /// wrapping, and ACCEPT CHANGES leaves as the one apply exit carrying the four levels beside
    /// the settings the page never showed.</summary>
    [Fact]
    public void TheAudioPageMovesALevelAndAppliesTheMixAsOneExit()
    {
        var saved = new OptionsDef
        {
            AudioMaster = 80, AudioMusic = 20, AudioEffects = 55, AudioVoice = 5,
            Difficulty = "hard", VSync = "120",
        };
        var shell = Shell(out _, () => saved);
        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.Options, shell.Screen);
        shell.Step(Down);
        Assert.Equal(OriginalShell.AudioDoorKey, shell.FocusedKey);
        shell.Step(Accept);

        // The page opens on the saved mix, not on the shipped defaults.
        Assert.Equal(OriginalScreen.Audio, shell.Screen);
        Assert.Equal(OriginalShell.AudioMasterKey, shell.FocusedKey);
        Assert.Equal(80, shell.AudioMasterChoice);
        Assert.Equal(20, shell.AudioMusicChoice);
        Assert.Equal(55, shell.AudioEffectsChoice);
        Assert.Equal(5, shell.AudioVoiceChoice);

        // A sideways step moves the focused level by the control's own step and clamps at silence
        // instead of wrapping to full, which is what every other stepped row on this shell does.
        shell.Step(Right);
        Assert.Equal(85, shell.AudioMasterChoice);
        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Down);
        Assert.Equal(OriginalShell.AudioVoiceKey, shell.FocusedKey);
        shell.Step(Left);
        Assert.Equal(0, shell.AudioVoiceChoice);
        shell.Step(Left);
        Assert.Equal(0, shell.AudioVoiceChoice);

        // Accept on a slider is a no-op: the level moves under the pointer or by a step alone.
        shell.Step(Accept);
        Assert.Equal(0, shell.AudioVoiceChoice);

        shell.Step(Down);
        Assert.Equal(OriginalShell.AudioAcceptKey, shell.FocusedKey);
        var exit = Assert.IsType<OptionsApplyExit>(shell.Step(Accept).Exit);
        Assert.Equal(85, exit.AudioMaster);
        Assert.Equal(20, exit.AudioMusic);
        Assert.Equal(55, exit.AudioEffects);
        Assert.Equal(0, exit.AudioVoice);
        // The settings this page never showed ride the apply unchanged, read back when it opened.
        Assert.Equal("hard", exit.Difficulty);
        Assert.Equal("120", exit.VSync);
        Assert.Equal(PresentationId.Original, exit.Presentation);
    }

    /// <summary>A level the options file has never carried opens the row on the shipped default,
    /// and CANCEL CHANGES and Back both leave with the edits dropped and no exit.</summary>
    [Fact]
    public void TheAudioPageOpensOnTheShippedDefaultsAndDropsAnEditOnCancelAndOnBack()
    {
        var shell = Shell(out _);
        shell.OpenAudio();
        Assert.Null(shell.AudioMasterChoice);
        Assert.Equal(AudioMix.DefaultMaster, shell.Rows.Single(r => r.Key == OriginalShell.AudioMasterKey).Slider!.Value);
        Assert.Equal(AudioMix.DefaultMusic, shell.Rows.Single(r => r.Key == OriginalShell.AudioMusicKey).Slider!.Value);
        Assert.Equal(AudioMix.DefaultEffects, shell.Rows.Single(r => r.Key == OriginalShell.AudioEffectsKey).Slider!.Value);
        Assert.Equal(AudioMix.DefaultVoice, shell.Rows.Single(r => r.Key == OriginalShell.AudioVoiceKey).Slider!.Value);

        shell.Step(Down);
        shell.Step(Left);
        Assert.Equal(45, shell.AudioMusicChoice);
        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Down);
        Assert.Equal(OriginalShell.AudioCancelKey, shell.FocusedKey);
        Assert.Null(shell.Step(Accept).Exit);
        Assert.Equal(OriginalScreen.Options, shell.Screen);
        Assert.Null(shell.AudioMusicChoice);

        shell.OpenAudio();
        // A step at an end moves nothing, so it writes nothing and the level is still never set.
        shell.Step(Right);
        Assert.Null(shell.AudioMasterChoice);
        shell.Step(Left);
        Assert.Equal(95, shell.AudioMasterChoice);
        Assert.Null(shell.Step(Back).Exit);
        Assert.Equal(OriginalScreen.Options, shell.Screen);
        Assert.Null(shell.AudioMasterChoice);
    }

    /// <summary>The AUDIO page's live preview as the shell states it: while the page is open it
    /// names the four levels it stands at, off the page it names no mix at all, and it names the
    /// level a frame moved only on the frames that actually moved one, so a host cannot sound a
    /// category once per pointer frame of a drag.</summary>
    [Fact]
    public void TheAudioPageStatesItsMixWhileOpenAndNamesAMovedLevelOnlyWhenOneMoved()
    {
        var shell = Shell(out _);
        Assert.Null(shell.AudioPreviewMix);
        shell.OpenAudio();
        Assert.Equal(
            new AudioLevels(AudioMix.DefaultMaster, AudioMix.DefaultMusic, AudioMix.DefaultEffects, AudioMix.DefaultVoice),
            shell.AudioPreviewMix);
        Assert.Equal(MenuMixLevel.None, shell.TakeAudioMoved());

        // A sideways step on the Effects row names Effects, and names it once: the moved level is
        // taken rather than read, so a second ask cannot sound the same move again.
        shell.Step(Down);
        shell.Step(Down);
        Assert.Equal(OriginalShell.AudioEffectsKey, shell.FocusedKey);
        shell.Step(Left);
        Assert.Equal(MenuMixLevel.Effects, shell.TakeAudioMoved());
        Assert.Equal(MenuMixLevel.None, shell.TakeAudioMoved());
        Assert.Equal(AudioMix.DefaultEffects - SliderControl.KeyStep, shell.AudioPreviewMix!.Value.Effects);

        // A drag: the frame that takes hold moves the level and names it, a held frame at the same
        // point moves nothing and names nothing, and the same holds at the far end of the track.
        var row = shell.Rows.Single(r => r.Key == OriginalShell.AudioEffectsKey);
        float left = row.X + 1f;
        float right = row.X + row.Width - 1f;
        shell.Step(Pointer(left, row.Y + 5f, pressed: true, clicked: true));
        Assert.Equal(AudioMix.MinLevel, shell.AudioEffectsChoice);
        Assert.Equal(MenuMixLevel.Effects, shell.TakeAudioMoved());
        shell.Step(Pointer(left, row.Y + 5f, pressed: true));
        Assert.Equal(MenuMixLevel.None, shell.TakeAudioMoved());
        shell.Step(Pointer(right, row.Y + 5f, pressed: true));
        Assert.Equal(AudioMix.MaxLevel, shell.AudioEffectsChoice);
        Assert.Equal(MenuMixLevel.Effects, shell.TakeAudioMoved());
        shell.Step(Pointer(right, row.Y + 5f, pressed: true));
        Assert.Equal(MenuMixLevel.None, shell.TakeAudioMoved());
        Assert.Equal(AudioMix.MaxLevel, shell.AudioPreviewMix!.Value.Effects);

        // And off the page there is no mix to apply, which is what drops the preview whichever door
        // the page was left by.
        Assert.Null(shell.Step(Back).Exit);
        Assert.Equal(OriginalScreen.Options, shell.Screen);
        Assert.Null(shell.AudioPreviewMix);
    }

    /// <summary>The AUDIO page over its own section: four sliders in the section's slider column,
    /// each on its own authored line (the pitch is uneven, so a first row and one pitch would
    /// misplace the rows below the second), the Master row on the In-Game Music line at the slider
    /// offset rather than at that row's checkbox corner, and the two plaques under them.</summary>
    [Fact]
    public void TheAudioPageIsComposedOverItsSectionsOwnRowShape()
    {
        var shell = Shell(out _);
        shell.OpenAudio();

        // Each row is the authored slot inset by 0, -10, 1 and -10: 137 wide of press region over a
        // three-pixel line, standing at the slider column and 26 pixels under its own title.
        var master = shell.Rows.Single(r => r.Key == OriginalShell.AudioMasterKey);
        Assert.Equal((130f, 266f, 170f, 23f), (master.X, master.Y, master.Width, master.Height));
        var music = shell.Rows.Single(r => r.Key == OriginalShell.AudioMusicKey);
        Assert.Equal((130f, 326f, 170f, 23f), (music.X, music.Y, music.Width, music.Height));
        var effects = shell.Rows.Single(r => r.Key == OriginalShell.AudioEffectsKey);
        Assert.Equal((130f, 381f, 170f, 23f), (effects.X, effects.Y, effects.Width, effects.Height));
        var voice = shell.Rows.Single(r => r.Key == OriginalShell.AudioVoiceKey);
        Assert.Equal((130f, 431f, 170f, 23f), (voice.X, voice.Y, voice.Width, voice.Height));
        var accept = shell.Rows.Single(r => r.Key == OriginalShell.AudioAcceptKey);
        Assert.Equal((200f, 500f, 240f, 50f), (accept.X, accept.Y, accept.Width, accept.Height));

        var board = shell.Compose();
        // The logo and the plate are the backdrop, not pictures. A board draws its fills between the
        // two layers, so a plate among the pictures would paint over the focused row's own mark and
        // the page would show no focus at all.
        Assert.Equal(new[] { "PM_Logo.png", "PP_ApBack.png" }, board.Backdrop.Select(p => p.Art.Name));
        Assert.DoesNotContain(board.Pictures, p => p.Art.Name is "PM_Logo.png" or "PP_ApBack.png");
        Assert.Contains(board.Lines, l => l.Text == "AUDIO" && l.X == 120f && l.Justify == BoardJustify.Center);
        Assert.Contains(board.Lines, l => l.Text == "Master" && l.X == 130f && l.Y == 250f && l.Width == 112f);
        Assert.Contains(board.Lines, l => l.Text == "Set the overall volume of all sounds."
            && l.X == 340f && l.Y == 260f && l.Width == 310f);
        Assert.Contains(board.Lines, l => l.Text == "Music Volume" && l.X == 130f && l.Y == 310f && l.Width == 170f);
        Assert.Contains(board.Lines, l => l.Text == "Effects Volume" && l.X == 130f && l.Y == 365f && l.Width == 170f);
        Assert.Contains(board.Lines, l => l.Text == "Voice Volume" && l.X == 130f && l.Y == 415f && l.Width == 170f);
        Assert.Equal(9, board.Lines.Count(l => l.Row < 0));

        // The focused row is marked twice and the others not at all: an outline around the row the
        // page opens on, and that row's title alone in the focused ink. Both halves are asserted
        // because either alone is a mark a player at a pad reported not seeing.
        Assert.Equal(
            new[] { (130f, 266f, 170f, 23f) },
            board.Fills.Where(f => f.Border).Select(f => (f.X, f.Y, f.Width, f.Height)));
        Assert.Contains(board.Lines, l => l.Text == "Master" && l.Ink == BoardInk.RowFocused);
        Assert.All(
            new[] { "Music Volume", "Effects Volume", "Voice Volume" },
            title => Assert.Contains(board.Lines, l => l.Text == title && l.Ink == BoardInk.Row));

        // And the mark follows the cursor rather than sticking to the first row.
        shell.Step(Down);
        var moved = shell.Compose();
        Assert.Equal(
            new[] { (130f, 326f, 170f, 23f) },
            moved.Fills.Where(f => f.Border).Select(f => (f.X, f.Y, f.Width, f.Height)));
        Assert.Contains(moved.Lines, l => l.Text == "Music Volume" && l.Ink == BoardInk.RowFocused);
        Assert.Contains(moved.Lines, l => l.Text == "Master" && l.Ink == BoardInk.Row);

        // The slot is drawn at the track's corner, the thumb at the level's own place on it. The
        // Master row authors no slider, so it takes the shipped art names as well as the sizes.
        var masterSlot = board.Pictures.Single(p => p.Art.Name == "PF_B_SliderSlot.png");
        Assert.Equal((130f, 276f), (masterSlot.X, masterSlot.Y));
        var masterThumb = board.Pictures.Single(p => p.Art.Name == "PF_B_Slider.png");
        Assert.Equal((258f, 267f), (masterThumb.X, masterThumb.Y));
        // The music row stands at the shipped default of 50, which is half a run along its slot.
        Assert.Equal(130f, board.Pictures.Single(p => p.Art.Name == "PP_B_SliderSlot.png" && p.Y == 336f).X);
        Assert.Equal(194f, board.Pictures.Single(p => p.Art.Name == "PP_B_Slider.png" && p.Y == 327f).X);
    }

    /// <summary>The VIDEO page behind the Preferences page's third door: it opens on the saved
    /// words with its first row focused, the checkbox under that flips the graphics word, and
    /// ACCEPT CHANGES leaves as the one apply exit carrying both display choices beside the two the
    /// Game Options page owns.</summary>
    [Fact]
    public void TheVideoPageFlipsEnhancedGraphicsAndAppliesItAsOneExit()
    {
        var shell = Shell(out _);
        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.Options, shell.Screen);
        // The AUDIO door stands between them, so the VIDEO door is two steps down.
        shell.Step(Down);
        shell.Step(Down);
        Assert.Equal(OriginalShell.VideoDoorKey, shell.FocusedKey);
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.Video, shell.Screen);
        Assert.Equal(OriginalShell.MonitorKey, shell.FocusedKey);
        Assert.Null(shell.MonitorChoice);
        Assert.Null(shell.ResolutionChoice);
        Assert.Null(shell.DisplayModeChoice);
        Assert.Null(shell.VSyncChoice);
        Assert.Equal(GraphicsMode.Default, shell.GraphicsChoice);

        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Down);
        Assert.Equal(OriginalShell.GraphicsKey, shell.FocusedKey);
        shell.Step(Accept);
        Assert.Equal(GraphicsMode.EnhancedWord, shell.GraphicsChoice);
        Assert.Equal(4 + 2, shell.Compose().Plaques.Single(p => p.Art.Name == "PP_B_Check8.png").Frame);

        // A sideways step takes the next word with wrap, as a Game Options row does.
        shell.Step(Right);
        Assert.Equal(GraphicsMode.Default, shell.GraphicsChoice);
        shell.Step(Right);
        Assert.Equal(GraphicsMode.EnhancedWord, shell.GraphicsChoice);

        shell.Step(Down);
        Assert.Equal(OriginalShell.VideoAcceptKey, shell.FocusedKey);
        var step = shell.Step(Accept);
        var exit = Assert.IsType<OptionsApplyExit>(step.Exit);
        Assert.Equal(GraphicsMode.EnhancedWord, exit.Graphics);
        Assert.Null(exit.MonitorIndex);
        Assert.Null(exit.Resolution);
        Assert.Null(exit.DisplayMode);
        Assert.Null(exit.VSync);
        Assert.Equal(PresentationId.Original, exit.Presentation);
        Assert.Equal("normal", exit.Difficulty);
    }

    /// <summary>The display-mode row: its list opens over the three words at the authored Viewing
    /// Range dropdown's own box, picking one closes the list on it, and ACCEPT CHANGES carries the
    /// store word rather than the label the row draws.</summary>
    [Fact]
    public void TheVideoPageDisplayModeRowPicksAWordAndCarriesItOnTheApply()
    {
        var shell = Shell(out _);
        shell.OpenVideoOn(OriginalShell.DisplayModeKey);
        Assert.Equal("Windowed", shell.Rows.Single(r => r.Key == OriginalShell.DisplayModeKey).Label);

        shell.Step(Accept);
        Assert.Equal(OriginalShell.DisplayModeKey, shell.OpenVideoOption);
        Assert.Equal(DisplayWords.DisplayModes.Count, shell.Rows.Count);
        Assert.Equal((260f, 352f, 70f, 17f), (shell.Rows[0].X, shell.Rows[0].Y, shell.Rows[0].Width, shell.Rows[0].Height));
        shell.Step(Down);
        shell.Step(Accept);
        Assert.Null(shell.OpenVideoOption);
        Assert.Equal(DisplayWords.Borderless, shell.DisplayModeChoice);
        Assert.Equal("Borderless", shell.Rows.Single(r => r.Key == OriginalShell.DisplayModeKey).Label);

        // A sideways step wraps past the last word back to the first, as every word row does.
        shell.Step(Right);
        Assert.Equal(DisplayWords.Fullscreen, shell.DisplayModeChoice);
        shell.Step(Right);
        Assert.Equal(DisplayWords.Windowed, shell.DisplayModeChoice);

        shell.Step(Left);
        Assert.Equal(DisplayWords.Fullscreen, shell.DisplayModeChoice);

        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Down);
        Assert.Equal(OriginalShell.VideoAcceptKey, shell.FocusedKey);
        var exit = Assert.IsType<OptionsApplyExit>(shell.Step(Accept).Exit);
        Assert.Equal(DisplayWords.Fullscreen, exit.DisplayMode);
    }

    /// <summary>The V-Sync row's dropdown: Accept opens the list over every choice at the row's own
    /// box, picking closes it on that word, and Back closes an open list before it leaves the
    /// page.</summary>
    [Fact]
    public void TheVideoPageVSyncRowOpensItsListPicksAndCloses()
    {
        var shell = Shell(out _);
        shell.OpenVideoOn(OriginalShell.VSyncKey);
        Assert.Equal("On", shell.Rows.Single(r => r.Key == OriginalShell.VSyncKey).Label);

        shell.Step(Accept);
        Assert.Equal(OriginalShell.VSyncKey, shell.OpenVideoOption);
        Assert.Equal(DisplayWords.VSyncChoices.Count, shell.Rows.Count);
        Assert.Equal((260f, 397f, 70f, 17f), (shell.Rows[0].X, shell.Rows[0].Y, shell.Rows[0].Width, shell.Rows[0].Height));
        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Accept);
        Assert.Null(shell.OpenVideoOption);
        Assert.Equal("60", shell.VSyncChoice);
        Assert.Equal("60 FPS", shell.Rows.Single(r => r.Key == OriginalShell.VSyncKey).Label);

        // Back on the open list closes it; the next Back is CANCEL CHANGES.
        shell.Step(Accept);
        Assert.Equal(OriginalShell.VSyncKey, shell.OpenVideoOption);
        Assert.Null(shell.Step(Back).Exit);
        Assert.Null(shell.OpenVideoOption);
        Assert.Equal(OriginalScreen.Video, shell.Screen);
        shell.Step(Back);
        Assert.Equal(OriginalScreen.Options, shell.Screen);
    }

    /// <summary>CANCEL CHANGES and Back both leave the VIDEO page with every choice dropped, and
    /// neither is an exit: the page's own plaques are its two doors back to Preferences.</summary>
    [Fact]
    public void TheVideoPageDropsTheChoiceOnCancelAndOnBack()
    {
        var shell = Shell(out _);
        shell.OpenVideo();
        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Right);
        Assert.Equal(DisplayWords.Borderless, shell.DisplayModeChoice);
        shell.Step(Down);
        shell.Step(Right);
        Assert.Equal(DisplayWords.VSyncOff, shell.VSyncChoice);
        shell.Step(Down);
        shell.Step(Accept);
        Assert.Equal(GraphicsMode.EnhancedWord, shell.GraphicsChoice);
        shell.Step(Down);
        shell.Step(Down);
        Assert.Equal(OriginalShell.VideoCancelKey, shell.FocusedKey);
        Assert.Null(shell.Step(Accept).Exit);
        Assert.Equal(OriginalScreen.Options, shell.Screen);
        Assert.Equal(GraphicsMode.Default, shell.GraphicsChoice);
        Assert.Null(shell.DisplayModeChoice);
        Assert.Null(shell.VSyncChoice);

        shell.OpenVideo();
        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Accept);
        Assert.Equal(GraphicsMode.EnhancedWord, shell.GraphicsChoice);
        Assert.Null(shell.Step(Back).Exit);
        Assert.Equal(OriginalScreen.Options, shell.Screen);
        Assert.Equal(GraphicsMode.Default, shell.GraphicsChoice);
    }

    [Fact]
    public void CancelChangesAndBackBothDropTheEditsAndReturnToPreferences()
    {
        var shell = Shell(out _);
        shell.OpenGameOptions();
        shell.Step(Right);
        Assert.Equal(CSVM.Flight.Difficulty.Hard, shell.DifficultyChoice);
        shell.Step(Down);
        shell.Step(Right);
        Assert.Equal(PresentationId.BuiltIn.Value, shell.PresentationChoice);

        shell.Step(Down);
        shell.Step(Down);
        Assert.Equal(OriginalShell.GameOptionsCancelKey, shell.FocusedKey);
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.Options, shell.Screen);
        Assert.Equal(PresentationId.Original.Value, shell.PresentationChoice);
        Assert.Equal(CSVM.Flight.Difficulty.Normal, shell.DifficultyChoice);

        // Back on the open list closes it; the next Back is CANCEL CHANGES.
        shell.OpenGameOptions();
        shell.Step(Accept);
        Assert.Equal(OriginalShell.DifficultyKey, shell.OpenGameOption);
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
        var saved = new OptionsDef
        {
            GraphicsMode = GraphicsMode.EnhancedWord, MenuPresentation = PresentationId.BuiltIn.Value,
            Difficulty = "hardest", VSync = "120", DisplayMode = DisplayWords.Fullscreen,
            Resolution = "1920x1080", MonitorIndex = "0",
        };
        var shell = Shell(out _, () => saved);
        shell.OpenGameOptions();

        Assert.Equal(GraphicsMode.EnhancedWord, shell.GraphicsChoice);
        Assert.Equal(PresentationId.BuiltIn.Value, shell.PresentationChoice);
        Assert.Equal(CSVM.Flight.Difficulty.Hardest, shell.DifficultyChoice);
        Assert.Equal("BUILT-IN", shell.Rows.Single(r => r.Key == OriginalShell.PresentationKey).Label);
        Assert.Equal("Hardest", shell.Rows.Single(r => r.Key == OriginalShell.DifficultyKey).Label);
        shell.OpenVideo();
        Assert.Equal("120", shell.VSyncChoice);
        Assert.Equal("120 FPS", shell.Rows.Single(r => r.Key == OriginalShell.VSyncKey).Label);
        Assert.Equal(DisplayWords.Fullscreen, shell.DisplayModeChoice);
        Assert.Equal("Fullscreen", shell.Rows.Single(r => r.Key == OriginalShell.DisplayModeKey).Label);
        Assert.Equal("1920x1080", shell.ResolutionChoice);
        Assert.Equal("1920x1080", shell.Rows.Single(r => r.Key == OriginalShell.ResolutionKey).Label);
        Assert.Equal("0", shell.MonitorChoice);
        Assert.Equal("Screen 0", shell.Rows.Single(r => r.Key == OriginalShell.MonitorKey).Label);

        // A file that never set the fields opens the rows on the shipped defaults: the project's own
        // size rather than the smallest a screen holds, and the standing screen rather than an index
        // no screen answers to, each row agreeing with the fallback rule its own setting applies.
        saved.GraphicsMode = null;
        saved.MenuPresentation = null;
        saved.Difficulty = null;
        saved.VSync = null;
        saved.DisplayMode = null;
        saved.Resolution = null;
        saved.MonitorIndex = "9";
        shell.OpenGameOptions();
        Assert.Equal(GraphicsMode.Default, shell.GraphicsChoice);
        Assert.Equal(PresentationId.Original.Value, shell.PresentationChoice);
        Assert.Equal(CSVM.Flight.Difficulty.Normal, shell.DifficultyChoice);
        shell.OpenVideo();
        Assert.Null(shell.VSyncChoice);
        Assert.Equal("On", shell.Rows.Single(r => r.Key == OriginalShell.VSyncKey).Label);
        Assert.Null(shell.DisplayModeChoice);
        Assert.Equal("Windowed", shell.Rows.Single(r => r.Key == OriginalShell.DisplayModeKey).Label);
        Assert.Null(shell.ResolutionChoice);
        Assert.Equal(ResolutionSetting.Default, shell.Rows.Single(r => r.Key == OriginalShell.ResolutionKey).Label);
        Assert.Equal("9", shell.MonitorChoice);
        Assert.Equal("Screen 0", shell.Rows.Single(r => r.Key == OriginalShell.MonitorKey).Label);
    }

    /// <summary>The graphics row's description reads the choice against the running mode, so a
    /// saved word the world has not picked up yet says a restart is owed rather than repeating
    /// the next-start note. Which of the two words is the running one depends on the process, so
    /// the fact checks that exactly one of them owes the restart.</summary>
    [Fact]
    public void TheGraphicsRowSaysWhenARestartIsStillOwed()
    {
        var saved = new OptionsDef { GraphicsMode = GraphicsMode.EnhancedWord };
        var shell = Shell(out _, () => saved);
        shell.OpenVideo();
        string enhanced = shell.Compose().Lines.Single(l => l.Text.StartsWith("Select the lit world.", System.StringComparison.Ordinal)).Text;

        saved.GraphicsMode = GraphicsMode.Default;
        shell.OpenVideo();
        string original = shell.Compose().Lines.Single(l => l.Text.StartsWith("Select the lit world.", System.StringComparison.Ordinal)).Text;

        var owed = new[] { enhanced, original }.Where(t => t.EndsWith("restart to apply.", System.StringComparison.Ordinal)).ToList();
        var settled = new[] { enhanced, original }.Where(t => t.EndsWith("Takes effect on the next start.", System.StringComparison.Ordinal)).ToList();
        Assert.Single(owed);
        Assert.Single(settled);
        Assert.Contains(GraphicsMode.Enhanced ? "This run is enhanced" : "This run is original", owed[0]);
    }

    /// <summary>A drag holds the pointer, so the click that took hold of a thumb is spent on it
    /// and the row the thumb is drawn over is not activated. The shell makes that distinction once
    /// for anything that holds the pointer; the list thumb is the hold an engine-free screen has,
    /// and a slider row joins the same gate (<see cref="SliderControlTests"/> drives that
    /// side).</summary>
    [Fact]
    public void ADragHoldsThePointerSoItsClickActivatesNoRowUnderIt()
    {
        // A roster longer than the eleven-row window, so the aircraft column has a thumb at all.
        var setup = new PlayerSetupFeature();
        var roster = OriginalPresentation.Roster(System.Array.Empty<CSVM.Flight.CustomPlaneDef>()).ToList();
        roster.AddRange(roster.Take(3).ToList());
        setup.SetRoster(roster);
        setup.Join(new ScriptedMenuSeat());
        var shell = new OriginalShell(MenuLayoutReaderTests.OriginalLayout(), new FreeFlightFeature(), setup, Measure);
        shell.Open(OriginalScreen.FreeFlight);

        var window = shell.Lists.Single(l => l.Key == "AIRFRAMES").Window;
        Assert.True(window.Scrolls);
        // The thumb runs down the column's own right edge, so the point it stands on is an
        // aircraft row that a click would otherwise pick.
        float x = window.ThumbX + 1f;
        float y = window.ThumbY + 1f;
        Assert.StartsWith("AIRFRAME", HitTestKey(shell, x, y), System.StringComparison.Ordinal);

        shell.Step(Pointer(x, y, pressed: true, clicked: true));
        Assert.Equal("AIRFRAMES", shell.Dragging);
        Assert.Null(shell.PickedAirframe);

        shell.Step(Pointer(x, y + window.Height, pressed: true));
        shell.Step(Pointer(x, y + window.Height));
        Assert.Null(shell.Dragging);
        Assert.Null(shell.PickedAirframe);
    }

    [Fact]
    public void TheOptionsScreenIsComposedOverThePreferencesChromeWithItsThreeBuiltDoorsLive()
    {
        var shell = Shell(out _);
        shell.Open(OriginalScreen.Options);

        // The four decoded page doors at their authored corners, the first three live and CONTROLS
        // disabled, then the section's own RETURN TO MAIN MENU.
        Assert.Equal(
            new[] { "PF_B_GAMEOPTIONS", "PF_B_AUDIO", "PF_B_VIDEO", "PF_B_CONTROLS", OriginalShell.OptionsBackKey },
            shell.Rows.Select(r => r.Key));
        Assert.Equal(new[] { true, true, true, false, true }, shell.Rows.Select(r => r.Enabled));
        var back = shell.Rows.Single(r => r.Key == OriginalShell.OptionsBackKey);
        Assert.Equal((460f, 500f, 240f, 50f), (back.X, back.Y, back.Width, back.Height));

        var board = shell.Compose();
        Assert.Equal(new[] { "PM_Logo.png", "PP_Back.png" }, board.Pictures.Select(p => p.Art.Name));
        Assert.Equal((100f, 200f), (board.Pictures[1].X, board.Pictures[1].Y));
        Assert.Contains(board.Lines, l => l.Text == "PREFERENCES" && l.X == 120f && l.Justify == BoardJustify.Center);
        Assert.Contains(board.Lines, l => l.Text == "Change the audio settings." && l.X == 340f && l.Y == 320f);
        Assert.Contains(board.Lines, l => l.Text == "Change the difficulty level and default views." && l.X == 340f);
        Assert.Equal(1, board.Plaques.Single(p => p.Art.Name == "PP_B_Audio.png").Frame);
        Assert.Equal(0, board.Plaques.Single(p => p.Art.Name == "PP_B_Controls.png").Frame);
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

        // Row one's dropdown box at the authored Difficulty dropdown's corner and width, row two's
        // one row down at the pitch, then the two decoded plaques.
        var difficulty = shell.Rows.Single(r => r.Key == OriginalShell.DifficultyKey);
        Assert.Equal((135f, 295f, 144f, 17f), (difficulty.X, difficulty.Y, difficulty.Width, difficulty.Height));
        var menu = shell.Rows.Single(r => r.Key == OriginalShell.PresentationKey);
        Assert.Equal((135f, 355f, 144f, 17f), (menu.X, menu.Y, menu.Width, menu.Height));
        var accept = shell.Rows.Single(r => r.Key == OriginalShell.GameOptionsAcceptKey);
        Assert.Equal((200f, 470f, 240f, 50f), (accept.X, accept.Y, accept.Width, accept.Height));

        var board = shell.Compose();
        // Backdrop, not pictures, for the reason the AUDIO page's own case states: a board draws its
        // fills between the two layers, so a plate among the pictures buries every focus mark.
        Assert.Equal(new[] { "PM_Logo.png", "PP_GoBack.png" }, board.Backdrop.Select(p => p.Art.Name));
        Assert.DoesNotContain(board.Pictures, p => p.Art.Name is "PM_Logo.png" or "PP_GoBack.png");
        Assert.Contains(board.Lines, l => l.Text == "GAME OPTIONS" && l.X == 120f && l.Justify == BoardJustify.Center);
        Assert.Contains(board.Lines, l => l.Text == "Difficulty" && l.X == 130f && l.Y == 280f && l.Width == 170f);
        Assert.Contains(board.Lines, l => l.Text == "Select the difficulty level for a solo campaign." && l.X == 340f && l.Y == 290f && l.Width == 310f);
        Assert.Contains(board.Lines, l => l.Text == "Menu" && l.X == 130f && l.Y == 340f && l.Width == 170f);
        Assert.Contains(board.Lines, l => l.Text == "Select the menu presentation." && l.X == 340f && l.Y == 350f && l.Width == 310f);
        // The first two authored rows are taken: two titles, two descriptions and the page's own
        // tab title.
        Assert.Equal(5, board.Lines.Count(l => l.Row < 0));

        // The box is the focus mark on this page's plate rather than standing chrome, so exactly one
        // row carries it and it is the focused one.
        Assert.Equal(
            new[] { (135f, 295f, 144f, 17f) },
            board.Fills.Where(f => f.Border).Select(f => (f.X, f.Y, f.Width, f.Height)));
        shell.Step(Down);
        var moved = shell.Compose();
        Assert.Equal(
            new[] { (135f, 355f, 144f, 17f) },
            moved.Fills.Where(f => f.Border).Select(f => (f.X, f.Y, f.Width, f.Height)));
    }

    /// <summary>The VIDEO page over its own section: the dropdowns at the authored Graphics,
    /// Resolution, Viewing Range and Effects Level boxes and the checkbox at the authored Shadows
    /// box, each title box stopped at the control beside it and each description at the description
    /// column, the Shadows one wrapped at the plaque column since that authored row carries no width
    /// of its own. The Graphics title keeps its authored width, that row's title already stopping
    /// before the control beside it.</summary>
    [Fact]
    public void TheVideoPageIsComposedOverItsSectionsOwnRowShape()
    {
        var shell = Shell(out _);
        shell.OpenVideo();

        var monitor = shell.Rows.Single(r => r.Key == OriginalShell.MonitorKey);
        Assert.Equal((260f, 245f, 70f, 15f), (monitor.X, monitor.Y, monitor.Width, monitor.Height));
        var resolution = shell.Rows.Single(r => r.Key == OriginalShell.ResolutionKey);
        Assert.Equal((260f, 290f, 70f, 17f), (resolution.X, resolution.Y, resolution.Width, resolution.Height));
        var mode = shell.Rows.Single(r => r.Key == OriginalShell.DisplayModeKey);
        Assert.Equal((260f, 335f, 70f, 17f), (mode.X, mode.Y, mode.Width, mode.Height));
        var vsync = shell.Rows.Single(r => r.Key == OriginalShell.VSyncKey);
        Assert.Equal((260f, 380f, 70f, 17f), (vsync.X, vsync.Y, vsync.Width, vsync.Height));
        var graphics = shell.Rows.Single(r => r.Key == OriginalShell.GraphicsKey);
        Assert.Equal((260f, 420f, 16f, 16f), (graphics.X, graphics.Y, graphics.Width, graphics.Height));
        var accept = shell.Rows.Single(r => r.Key == OriginalShell.VideoAcceptKey);
        Assert.Equal((500f, 470f, 240f, 50f), (accept.X, accept.Y, accept.Width, accept.Height));
        var cancel = shell.Rows.Single(r => r.Key == OriginalShell.VideoCancelKey);
        Assert.Equal((500f, 520f, 240f, 50f), (cancel.X, cancel.Y, cancel.Width, cancel.Height));

        var board = shell.Compose();
        // Backdrop, not pictures, for the reason the AUDIO page's own case states: a board draws its
        // fills between the two layers, so a plate among the pictures buries every focus mark.
        Assert.Equal(new[] { "PM_Logo.png", "PP_VpBack.png" }, board.Backdrop.Select(p => p.Art.Name));
        Assert.DoesNotContain(board.Pictures, p => p.Art.Name is "PM_Logo.png" or "PP_VpBack.png");
        Assert.Contains(board.Lines, l => l.Text == "VIDEO" && l.X == 120f && l.Justify == BoardJustify.Center);
        Assert.Contains(board.Lines, l => l.Text == "Monitor" && l.X == 130f && l.Y == 245f && l.Width == 90f);
        Assert.Contains(board.Lines, l => l.Text.StartsWith("Select the monitor", System.StringComparison.Ordinal)
            && l.X == 340f && l.Y == 245f && l.Width == 310f);
        Assert.Contains(board.Lines, l => l.Text == "Resolution" && l.X == 130f && l.Y == 290f && l.Width == 130f);
        Assert.Contains(board.Lines, l => l.Text == "Select the screen resolution."
            && l.X == 340f && l.Y == 290f && l.Width == 310f);
        Assert.Contains(board.Lines, l => l.Text == "Display Mode" && l.X == 130f && l.Y == 335f && l.Width == 130f);
        Assert.Contains(board.Lines, l => l.Text.StartsWith("Select how the window sits", System.StringComparison.Ordinal)
            && l.X == 340f && l.Y == 335f && l.Width == 310f);
        Assert.Contains(board.Lines, l => l.Text == "V-Sync" && l.X == 130f && l.Y == 380f && l.Width == 130f);
        Assert.Contains(board.Lines, l => l.Text.StartsWith("Select the frame pacing.", System.StringComparison.Ordinal)
            && l.X == 340f && l.Y == 380f && l.Width == 310f);
        Assert.Contains(board.Lines, l => l.Text == "Enhanced Graphics" && l.X == 130f && l.Y == 425f && l.Width == 130f);
        Assert.Contains(board.Lines, l => l.Text.StartsWith("Select the lit world.", System.StringComparison.Ordinal)
            && l.X == 340f && l.Y == 425f && l.Width == 160f);
        Assert.Equal(11, board.Lines.Count(l => l.Row < 0));
        // The checkbox draws unchecked and unfocused, the page opening on the monitor row four
        // above it: the second of its eight frames.
        Assert.Equal(1, board.Plaques.Single(p => p.Art.Name == "PP_B_Check8.png").Frame);

        // The box marks the focused dropdown and no other, and it follows the cursor. Four rows down
        // the cursor stands on the checkbox, which is a plaque strip and takes no box at all, so the
        // page draws none.
        Assert.Equal(
            new[] { (260f, 245f, 70f, 15f) },
            board.Fills.Where(f => f.Border).Select(f => (f.X, f.Y, f.Width, f.Height)));
        shell.Step(Down);
        Assert.Equal(
            new[] { (260f, 290f, 70f, 17f) },
            shell.Compose().Fills.Where(f => f.Border).Select(f => (f.X, f.Y, f.Width, f.Height)));
        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Down);
        Assert.Equal(OriginalShell.GraphicsKey, shell.FocusedKey);
        Assert.Empty(shell.Compose().Fills.Where(f => f.Border));
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
    // arrows 15x56, the slider's slot and thumb at their shipped sizes, the panes unmeasured.
    private static (int Width, int Height)? Measure(string art) => art switch
    {
        "PM_B_Paper.png" => (160, 112),
        "PP_B_Check8.png" => (16, 128),
        "PP_B_DropUp.png" or "PP_B_DropDown.png" => (15, 56),
        // The section's own slider art and the shipped names a row with no slider widget falls
        // back to, both at the shipped sizes: a three-pixel slot and a thumb that clears it.
        "PP_B_SliderSlot.png" or "PF_B_SliderSlot.png" => (171, 3),
        "PP_B_Slider.png" or "PF_B_Slider.png" => (43, 21),
        _ when art.StartsWith("PM_B_", System.StringComparison.Ordinal) => (240, 200),
        _ when art.StartsWith("PP_B_", System.StringComparison.Ordinal) => (240, 200),
        _ => null,
    };

    private static MenuCommands Pointer(float x, float y, bool pressed = false, bool clicked = false) =>
        new() { Pointer = new MenuPointer(x, y, pressed, clicked) };

    // The key of the topmost row a point lands on, the shell's own last-hit-wins reading.
    private static string HitTestKey(OriginalShell shell, float x, float y)
    {
        var rows = shell.Rows;
        for (int i = rows.Count - 1; i >= 0; i--)
        {
            if (rows[i].Visible && rows[i].Contains(x, y))
            {
                return rows[i].Key;
            }
        }

        return string.Empty;
    }
}
