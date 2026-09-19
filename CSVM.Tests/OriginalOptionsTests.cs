using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Bindings;
using CSVM.Mech3;
using CSVM.UI;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The options module alone over the hand-authored layout fixture: the five leaves behind the
/// Preferences hub (Game Options, AUDIO, VIDEO, CONTROLS and KEYS AND BUTTONS), the words each page
/// opens on and the settings its one apply carries, the dropdown lists and their windows, the four
/// sliders and the mix they preview, the seven rebinding tabs and the captures their cells arm, and
/// what every page draws. The hub itself is the shell's screen, so the seam to it is one wiring fact
/// in <see cref="OriginalShellTests"/>; everything here drives the module over a hand-written host.
/// </summary>
public class OriginalOptionsTests
{
    [Fact]
    public void TheGameOptionsPageTakesEveryChoiceAndAppliesThemAsOneExit()
    {
        var host = Host();
        host.Module.OpenGameOptions();

        Assert.Equal(OriginalScreen.GameOptions, host.Screen);
        Assert.Equal(CSVM.Flight.Difficulty.Normal, host.Module.DifficultyChoice);
        Assert.Equal(GraphicsMode.Default, host.Module.GraphicsChoice);
        Assert.Equal(OriginalOptionsScreen.DifficultyKey, host.FocusedKey);

        // The first row is the original's own Difficulty dropdown over the three campaign tiers:
        // its list opens on Accept, and the third item picks Hardest.
        Accept(host);
        Assert.Equal(OriginalOptionsScreen.DifficultyKey, host.Module.OpenGameOption);
        Assert.Equal(new[] { "Normal", "Hard", "Hardest" }, host.Rows.Select(r => r.Label));
        Down(host);
        Down(host);
        Accept(host);
        Assert.Null(host.Module.OpenGameOption);
        Assert.Equal(CSVM.Flight.Difficulty.Hardest, host.Module.DifficultyChoice);
        Assert.Equal("Hardest", Row(host, OriginalOptionsScreen.DifficultyKey).Label);
        // A sideways step wraps back onto Normal, then on to Hard.
        StepX(host, 1);
        Assert.Equal(CSVM.Flight.Difficulty.Normal, host.Module.DifficultyChoice);
        StepX(host, 1);
        Assert.Equal(CSVM.Flight.Difficulty.Hard, host.Module.DifficultyChoice);

        // The second row is the original's own Default View dropdown, over the three views its own
        // list carries in its own order: Cockpit, First Person, Exterior.
        Down(host);
        Assert.Equal(OriginalOptionsScreen.DefaultViewKey, host.FocusedKey);
        Assert.Null(host.Module.DefaultViewChoice);
        Assert.Equal("Exterior", Row(host, OriginalOptionsScreen.DefaultViewKey).Label);
        Accept(host);
        Assert.Equal(OriginalOptionsScreen.DefaultViewKey, host.Module.OpenGameOption);
        Assert.Equal(new[] { "Cockpit", "First Person", "Exterior" }, host.Rows.Select(r => r.Label));
        // The list opens on the item the row stands at, which with nothing saved is Exterior, the
        // last of the three; two steps up reach the first.
        MoveY(host, -1);
        MoveY(host, -1);
        Accept(host);
        Assert.Null(host.Module.OpenGameOption);
        Assert.Equal("cockpit", host.Module.DefaultViewChoice);
        StepX(host, 1);
        Assert.Equal("nose", host.Module.DefaultViewChoice);
        StepX(host, 1);
        Assert.Equal("chase", host.Module.DefaultViewChoice);
        StepX(host, 1);
        Assert.Equal("cockpit", host.Module.DefaultViewChoice);

        // The third row is the original's own Auto Head Turn checkbox, which reads OFF while nothing
        // is saved, since an unset field leaves the headLook.autohead config key deciding and that
        // ships off.
        Down(host);
        Assert.Equal(OriginalOptionsScreen.AutoHeadTurnKey, host.FocusedKey);
        Assert.Null(host.Module.AutoHeadTurnChoice);
        Accept(host);
        Assert.True(host.Module.AutoHeadTurnChoice);
        StepX(host, 1);
        Assert.False(host.Module.AutoHeadTurnChoice);
        StepX(host, 1);
        Assert.True(host.Module.AutoHeadTurnChoice);

        // The fourth row is the remake-only Next Target checkbox, straight under the head turn: the
        // page offers no menu presentation row, the command line alone choosing one. Accept flips
        // it, and a sideways step is the same flip, so the row is walkable with either gesture.
        Down(host);
        Assert.Equal(OriginalOptionsScreen.NearestAfterKillKey, host.FocusedKey);
        Assert.Null(host.Module.NearestAfterKillChoice);
        Accept(host);
        Assert.True(host.Module.NearestAfterKillChoice);
        StepX(host, 1);
        Assert.False(host.Module.NearestAfterKillChoice);
        StepX(host, 1);
        Assert.True(host.Module.NearestAfterKillChoice);

        // The fifth row is the rumble toggle, which reads ON while nothing is saved, so its first
        // press is the one that turns it off.
        Down(host);
        Assert.Equal(OriginalOptionsScreen.RumbleKey, host.FocusedKey);
        Assert.Null(host.Module.RumbleChoice);
        Accept(host);
        Assert.False(host.Module.RumbleChoice);
        StepX(host, 1);
        Assert.True(host.Module.RumbleChoice);

        Down(host);
        Assert.Equal(OriginalOptionsScreen.GameOptionsAcceptKey, host.FocusedKey);
        var exit = Assert.IsType<OptionsApplyExit>(Accept(host));
        Assert.True(exit.NearestAfterKill);
        Assert.True(exit.Rumble);
        Assert.Equal("cockpit", exit.DefaultView);
        Assert.True(exit.AutoHeadTurn);
        // The graphics word rides this page's apply unchanged: it is the VIDEO page's row now, and
        // the apply carries every saved choice whichever page sends it.
        Assert.Equal(GraphicsMode.Default, exit.Graphics);
        Assert.Equal("hard", exit.Difficulty);
    }

    [Fact]
    public void CancelChangesAndBackBothDropTheEditsAndReturnToPreferences()
    {
        var host = Host();
        host.Module.OpenGameOptions();
        StepX(host, 1);
        Assert.Equal(CSVM.Flight.Difficulty.Hard, host.Module.DifficultyChoice);
        Down(host);
        StepX(host, 1);
        Assert.Equal("cockpit", host.Module.DefaultViewChoice);
        Down(host);
        StepX(host, 1);
        Assert.True(host.Module.AutoHeadTurnChoice);
        Down(host);
        StepX(host, 1);
        Assert.True(host.Module.NearestAfterKillChoice);
        Down(host);
        StepX(host, 1);
        Assert.False(host.Module.RumbleChoice);

        Down(host);
        Down(host);
        Assert.Equal(OriginalOptionsScreen.GameOptionsCancelKey, host.FocusedKey);
        Assert.Null(Accept(host));
        Assert.Equal(OriginalScreen.Options, host.Screen);
        Assert.Equal(CSVM.Flight.Difficulty.Normal, host.Module.DifficultyChoice);
        Assert.Null(host.Module.NearestAfterKillChoice);
        Assert.Null(host.Module.RumbleChoice);
        Assert.Null(host.Module.DefaultViewChoice);
        Assert.Null(host.Module.AutoHeadTurnChoice);

        // Back on the open list closes it; the next Back is CANCEL CHANGES.
        host.Module.OpenGameOptions();
        Accept(host);
        Assert.Equal(OriginalOptionsScreen.DifficultyKey, host.Module.OpenGameOption);
        // The open list is only as tall as its own items: three tiers, three rows, and the panel
        // under them no taller, which is how the original draws a list shorter than its box allows.
        var open = Assert.Single(Compose(host).Overlays);
        Assert.Equal(3, open.Lines.Count);
        Assert.Equal(3f * 17f, open.Fills[0].Height);
        Assert.True(Back(host));
        Assert.Null(host.Module.OpenGameOption);
        Assert.Equal(OriginalScreen.GameOptions, host.Screen);
        Assert.True(Back(host));
        Assert.Equal(OriginalScreen.Options, host.Screen);
    }

    /// <summary>The AUDIO page: it opens on the saved mix with Master focused, a sideways step moves
    /// the focused level and clamps rather than wrapping, and ACCEPT CHANGES leaves as the one apply
    /// exit carrying the four levels beside the settings the page never showed.</summary>
    [Fact]
    public void TheAudioPageMovesALevelAndAppliesTheMixAsOneExit()
    {
        var saved = new OptionsDef
        {
            AudioMaster = 80, AudioMusic = 20, AudioEffects = 55, AudioVoice = 5,
            Difficulty = "hard", VSync = "120",
        };
        var host = Host(() => saved);
        host.Module.OpenAudio();

        // The page opens on the saved mix, not on the shipped defaults.
        Assert.Equal(OriginalScreen.Audio, host.Screen);
        Assert.Equal(OriginalOptionsScreen.AudioMasterKey, host.FocusedKey);
        Assert.Equal(80, host.Module.AudioMasterChoice);
        Assert.Equal(20, host.Module.AudioMusicChoice);
        Assert.Equal(55, host.Module.AudioEffectsChoice);
        Assert.Equal(5, host.Module.AudioVoiceChoice);

        // A sideways step moves the focused level by the control's own step and clamps at silence
        // instead of wrapping to full, which is what every other stepped row on this shell does.
        StepX(host, 1);
        Assert.Equal(85, host.Module.AudioMasterChoice);
        Down(host);
        Down(host);
        Down(host);
        Assert.Equal(OriginalOptionsScreen.AudioVoiceKey, host.FocusedKey);
        StepX(host, -1);
        Assert.Equal(0, host.Module.AudioVoiceChoice);
        StepX(host, -1);
        Assert.Equal(0, host.Module.AudioVoiceChoice);

        // Accept on a slider is a no-op: the level moves under the pointer or by a step alone.
        Accept(host);
        Assert.Equal(0, host.Module.AudioVoiceChoice);

        Down(host);
        Assert.Equal(OriginalOptionsScreen.AudioAcceptKey, host.FocusedKey);
        var exit = Assert.IsType<OptionsApplyExit>(Accept(host));
        Assert.Equal(85, exit.AudioMaster);
        Assert.Equal(20, exit.AudioMusic);
        Assert.Equal(55, exit.AudioEffects);
        Assert.Equal(0, exit.AudioVoice);
        // The settings this page never showed ride the apply unchanged, read back when it opened.
        Assert.Equal("hard", exit.Difficulty);
        Assert.Equal("120", exit.VSync);
    }

    /// <summary>A level the options file has never carried opens the row on the shipped default,
    /// and CANCEL CHANGES and Back both leave with the edits dropped.</summary>
    [Fact]
    public void TheAudioPageOpensOnTheShippedDefaultsAndDropsAnEditOnCancelAndOnBack()
    {
        var host = Host();
        host.Module.OpenAudio();
        Assert.Null(host.Module.AudioMasterChoice);
        Assert.Equal(AudioMix.DefaultMaster, Row(host, OriginalOptionsScreen.AudioMasterKey).Slider!.Value);
        Assert.Equal(AudioMix.DefaultMusic, Row(host, OriginalOptionsScreen.AudioMusicKey).Slider!.Value);
        Assert.Equal(AudioMix.DefaultEffects, Row(host, OriginalOptionsScreen.AudioEffectsKey).Slider!.Value);
        Assert.Equal(AudioMix.DefaultVoice, Row(host, OriginalOptionsScreen.AudioVoiceKey).Slider!.Value);

        Down(host);
        StepX(host, -1);
        Assert.Equal(45, host.Module.AudioMusicChoice);
        Down(host);
        Down(host);
        Down(host);
        Down(host);
        Assert.Equal(OriginalOptionsScreen.AudioCancelKey, host.FocusedKey);
        Assert.Null(Accept(host));
        Assert.Equal(OriginalScreen.Options, host.Screen);
        Assert.Null(host.Module.AudioMusicChoice);

        host.Module.OpenAudio();
        // A step at an end moves nothing, so it writes nothing and the level is still never set.
        StepX(host, 1);
        Assert.Null(host.Module.AudioMasterChoice);
        StepX(host, -1);
        Assert.Equal(95, host.Module.AudioMasterChoice);
        Assert.True(Back(host));
        Assert.Equal(OriginalScreen.Options, host.Screen);
        Assert.Null(host.Module.AudioMasterChoice);
    }

    /// <summary>The AUDIO page's live preview as the module states it: while the page is open it
    /// names the four levels it stands at, off the page it names no mix at all, and it names the
    /// level a frame moved only on the frames that actually moved one, so a host cannot sound a
    /// category once per pointer frame of a drag.</summary>
    [Fact]
    public void TheAudioPageStatesItsMixWhileOpenAndNamesAMovedLevelOnlyWhenOneMoved()
    {
        var host = Host();
        Assert.Null(host.Module.AudioPreviewMix);
        host.Module.OpenAudio();
        Assert.Equal(
            new AudioLevels(AudioMix.DefaultMaster, AudioMix.DefaultMusic, AudioMix.DefaultEffects, AudioMix.DefaultVoice),
            host.Module.AudioPreviewMix);
        Assert.Equal(MenuMixLevel.None, host.Module.TakeAudioMoved());

        // A sideways step on the Effects row names Effects, and names it once: the moved level is
        // taken rather than read, so a second ask cannot sound the same move again.
        Down(host);
        Down(host);
        Assert.Equal(OriginalOptionsScreen.AudioEffectsKey, host.FocusedKey);
        StepX(host, -1);
        Assert.Equal(MenuMixLevel.Effects, host.Module.TakeAudioMoved());
        Assert.Equal(MenuMixLevel.None, host.Module.TakeAudioMoved());
        Assert.Equal(AudioMix.DefaultEffects - SliderControl.KeyStep, host.Module.AudioPreviewMix!.Value.Effects);

        // A drag: the frame that takes hold moves the level and names it, a held frame at the same
        // point moves nothing and names nothing, and the same holds at the far end of the track.
        var row = Row(host, OriginalOptionsScreen.AudioEffectsKey);
        float left = row.X + 1f;
        float right = row.X + row.Width - 1f;
        Drag(host, left, row.Y + 5f, pressed: true, clicked: true);
        Assert.Equal(AudioMix.MinLevel, host.Module.AudioEffectsChoice);
        Assert.Equal(MenuMixLevel.Effects, host.Module.TakeAudioMoved());
        Drag(host, left, row.Y + 5f, pressed: true);
        Assert.Equal(MenuMixLevel.None, host.Module.TakeAudioMoved());
        Drag(host, right, row.Y + 5f, pressed: true);
        Assert.Equal(AudioMix.MaxLevel, host.Module.AudioEffectsChoice);
        Assert.Equal(MenuMixLevel.Effects, host.Module.TakeAudioMoved());
        Drag(host, right, row.Y + 5f, pressed: true);
        Assert.Equal(MenuMixLevel.None, host.Module.TakeAudioMoved());
        Assert.Equal(AudioMix.MaxLevel, host.Module.AudioPreviewMix!.Value.Effects);

        // And off the page there is no mix to apply, which is what drops the preview whichever door
        // the page was left by.
        Assert.True(Back(host));
        Assert.Equal(OriginalScreen.Options, host.Screen);
        Assert.Null(host.Module.AudioPreviewMix);
    }

    /// <summary>The AUDIO page over its own section: four sliders in the section's slider column,
    /// each on its own authored line (the pitch is uneven, so a first row and one pitch would
    /// misplace the rows below the second), the Master row on the In-Game Music line at the slider
    /// offset rather than at that row's checkbox corner, and the two plaques under them.</summary>
    [Fact]
    public void TheAudioPageIsComposedOverItsSectionsOwnRowShape()
    {
        var host = Host();
        host.Module.OpenAudio();

        // Each row is the authored slot inset by 0, -10, 1 and -10: 137 wide of press region over a
        // three-pixel line, standing at the slider column and 26 pixels under its own title.
        var master = Row(host, OriginalOptionsScreen.AudioMasterKey);
        Assert.Equal((130f, 266f, 170f, 23f), Rect(master));
        Assert.Equal((130f, 326f, 170f, 23f), Rect(Row(host, OriginalOptionsScreen.AudioMusicKey)));
        Assert.Equal((130f, 381f, 170f, 23f), Rect(Row(host, OriginalOptionsScreen.AudioEffectsKey)));
        Assert.Equal((130f, 431f, 170f, 23f), Rect(Row(host, OriginalOptionsScreen.AudioVoiceKey)));
        Assert.Equal((200f, 500f, 240f, 50f), Rect(Row(host, OriginalOptionsScreen.AudioAcceptKey)));

        var board = Compose(host);
        // The logo and the plate are the backdrop, not pictures. A board draws its fills between the
        // two layers, so a plate among the pictures would paint over the focused row's own mark and
        // the page would show no focus at all. The flag under them is the shell's own movie layer.
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
        Down(host);
        var moved = Compose(host);
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

    /// <summary>The VIDEO page: it opens on the saved words with its first row focused, the checkbox
    /// under that flips the graphics word, and ACCEPT CHANGES leaves as the one apply exit carrying
    /// both display choices beside the two the Game Options page owns.</summary>
    [Fact]
    public void TheVideoPageFlipsEnhancedGraphicsAndAppliesItAsOneExit()
    {
        var host = Host();
        host.Module.OpenVideo();

        Assert.Equal(OriginalScreen.Video, host.Screen);
        Assert.Equal(OriginalOptionsScreen.MonitorKey, host.FocusedKey);
        Assert.Null(host.Module.MonitorChoice);
        Assert.Null(host.Module.ResolutionChoice);
        Assert.Null(host.Module.DisplayModeChoice);
        Assert.Null(host.Module.VSyncChoice);
        Assert.Equal(GraphicsMode.Default, host.Module.GraphicsChoice);

        // Four steps, not five: the size row is dead under the borderless default, which owns the
        // size, and a dead row is out of the walk.
        Down(host);
        Down(host);
        Down(host);
        // The carve row above the graphics one opens off, the shipped default, and flips.
        Assert.Equal(OriginalOptionsScreen.RocketCratersKey, host.FocusedKey);
        Assert.Null(host.Module.RocketCratersChoice);
        Accept(host);
        Assert.True(host.Module.RocketCratersChoice);
        Down(host);
        Assert.Equal(OriginalOptionsScreen.GraphicsKey, host.FocusedKey);
        Accept(host);
        Assert.Equal(GraphicsMode.EnhancedWord, host.Module.GraphicsChoice);
        Assert.Equal(4 + 2, Compose(host).Plaques.Single(p => p.Art.Name == "PP_B_Check8.png" && p.Y == 465f).Frame);

        // A sideways step takes the next word with wrap, as a Game Options row does.
        StepX(host, 1);
        Assert.Equal(GraphicsMode.Default, host.Module.GraphicsChoice);
        StepX(host, 1);
        Assert.Equal(GraphicsMode.EnhancedWord, host.Module.GraphicsChoice);

        Down(host);
        Assert.Equal(OriginalOptionsScreen.VideoAcceptKey, host.FocusedKey);
        var exit = Assert.IsType<OptionsApplyExit>(Accept(host));
        Assert.Equal(GraphicsMode.EnhancedWord, exit.Graphics);
        Assert.True(exit.RocketCraters);
        Assert.Null(exit.MonitorIndex);
        Assert.Null(exit.Resolution);
        Assert.Null(exit.DisplayMode);
        Assert.Null(exit.VSync);
        Assert.Equal("normal", exit.Difficulty);
    }

    /// <summary>The display-mode row: its list opens over the three words at the authored Viewing
    /// Range dropdown's own box, picking one closes the list on it, and ACCEPT CHANGES carries the
    /// store word rather than the label the row draws.</summary>
    [Fact]
    public void TheVideoPageDisplayModeRowPicksAWordAndCarriesItOnTheApply()
    {
        var host = Host();
        host.Module.OpenVideoOn(OriginalOptionsScreen.DisplayModeKey);
        Assert.Equal("Borderless", Row(host, OriginalOptionsScreen.DisplayModeKey).Label);

        // The list opens focused on the row's own word, the borderless default here, not on its first.
        Accept(host);
        Assert.Equal(OriginalOptionsScreen.DisplayModeKey, host.Module.OpenVideoOption);
        Assert.Equal(DisplayWords.DisplayModes.Count, host.Rows.Count);
        Assert.Equal((260f, 352f, 70f, 17f), Rect(host.Rows[0]));
        Down(host);
        Accept(host);
        Assert.Null(host.Module.OpenVideoOption);
        Assert.Equal(DisplayWords.Fullscreen, host.Module.DisplayModeChoice);
        Assert.Equal("Fullscreen", Row(host, OriginalOptionsScreen.DisplayModeKey).Label);

        // A sideways step wraps past the last word back to the first, as every word row does.
        StepX(host, 1);
        Assert.Equal(DisplayWords.Windowed, host.Module.DisplayModeChoice);
        StepX(host, 1);
        Assert.Equal(DisplayWords.Borderless, host.Module.DisplayModeChoice);

        StepX(host, -1);
        Assert.Equal(DisplayWords.Windowed, host.Module.DisplayModeChoice);

        Down(host);
        Down(host);
        Down(host);
        Down(host);
        Assert.Equal(OriginalOptionsScreen.VideoAcceptKey, host.FocusedKey);
        var exit = Assert.IsType<OptionsApplyExit>(Accept(host));
        Assert.Equal(DisplayWords.Windowed, exit.DisplayMode);
    }

    /// <summary>The V-Sync row's dropdown: Accept opens the list over every choice at the row's own
    /// box, picking closes it on that word, and Back closes an open list before it leaves the
    /// page.</summary>
    [Fact]
    public void TheVideoPageVSyncRowOpensItsListPicksAndCloses()
    {
        var host = Host();
        host.Module.OpenVideoOn(OriginalOptionsScreen.VSyncKey);
        Assert.Equal("Off", Row(host, OriginalOptionsScreen.VSyncKey).Label);

        // The list opens focused on the row's own word, the off default here, so two steps down
        // land on the third choice after it.
        Accept(host);
        Assert.Equal(OriginalOptionsScreen.VSyncKey, host.Module.OpenVideoOption);
        // Five words in the row's authored four-row window, so the list carries its two arrows
        // beside the five item rows and the rows narrow by the column those stand in.
        Assert.Equal(DisplayWords.VSyncChoices.Count + 2, host.Rows.Count);
        Assert.Equal((260f, 397f, 54f, 17f), Rect(host.Rows[0]));
        Down(host);
        Down(host);
        Accept(host);
        Assert.Null(host.Module.OpenVideoOption);
        Assert.Equal("120", host.Module.VSyncChoice);
        Assert.Equal("120 FPS", Row(host, OriginalOptionsScreen.VSyncKey).Label);

        // Back on the open list closes it; the next Back is CANCEL CHANGES.
        Accept(host);
        Assert.Equal(OriginalOptionsScreen.VSyncKey, host.Module.OpenVideoOption);
        Assert.True(Back(host));
        Assert.Null(host.Module.OpenVideoOption);
        Assert.Equal(OriginalScreen.Video, host.Screen);
        Assert.True(Back(host));
        Assert.Equal(OriginalScreen.Options, host.Screen);
    }

    /// <summary>A leaf's list that fits the window its own row authors stands exactly as tall as its
    /// items and carries no chrome: no arrows, no thumb, no column given up and nothing for the
    /// pointer to scroll.</summary>
    [Fact]
    public void AnOptionListInsideItsWindowIsAsTallAsItsItemsAndCarriesNoBar()
    {
        var host = Host();
        host.Module.OpenGameOptions();
        Accept(host);

        Assert.Equal(OriginalOptionsScreen.DifficultyKey, host.Module.OpenGameOption);
        Assert.Equal(3, host.Rows.Count);
        Assert.All(host.Rows, r => Assert.Equal(OriginalRowKind.ListRow, r.Kind));
        Assert.All(host.Rows, r => Assert.True(r.Visible));
        // The fixture's box is 144 wide at x 135, and a list with nowhere to scroll keeps all of it.
        Assert.Equal((135f, 312f, 144f, 17f), Rect(host.Rows[0]));
        Assert.Empty(Lists(host));
        Assert.Empty(Box(host).Pictures);
    }

    /// <summary>A leaf's list longer than the window its own row authors: every word is still a row
    /// so the walk reaches it, only the window's are drawn and hit, the arrows and the thumb stand
    /// inside the box's own right edge, and a wheel step, an arrow press and a press on a hidden
    /// row each do what they should.</summary>
    [Fact]
    public void AnOptionListPastItsWindowScrollsOnItsOwnBarInsideTheBox()
    {
        var host = Host();
        host.Module.OpenVideoOn(OriginalOptionsScreen.VSyncKey);
        Accept(host);

        // Five words in the authored four-row window: five item rows, four of them visible.
        Assert.Equal(5, host.Rows.Count(r => r.Kind == OriginalRowKind.ListRow));
        Assert.Equal(4, host.Rows.Count(r => r.Kind == OriginalRowKind.ListRow && r.Visible));
        var up = Row(host, OriginalOptionsScreen.VSyncKey + ":up");
        var down = Row(host, OriginalOptionsScreen.VSyncKey + ":down");
        Assert.False(up.Enabled);
        Assert.True(down.Enabled);

        // The chrome stands inside the box, whose right edge is at 260 + 70: a 16-wide arrow at its
        // head and its foot, the thumb between them, and the two arrow pictures and the thumb on
        // the list's own panel.
        Assert.Equal((314f, 397f, 16f, 11f), Rect(up));
        Assert.Equal((314f, 454f, 16f, 11f), Rect(down));
        var window = Assert.Single(Lists(host)).Window;
        Assert.Equal(314f, window.ThumbX);
        Assert.Equal((260f, 397f, 70f, 68f), (window.X, window.Y, window.Width, window.Height));
        Assert.Equal(3, Box(host).Pictures.Count);

        // A wheel over the window moves it by a row, and the word that was outside it is the one
        // drawn; both arrows then have somewhere to go but the down one does not.
        Wheel(host, window.X + 2f, window.Y + 2f, 1);
        Assert.True(Row(host, OriginalOptionsScreen.VSyncKey + ":4").Visible);
        Assert.False(Row(host, OriginalOptionsScreen.VSyncKey + ":0").Visible);
        Assert.True(Row(host, OriginalOptionsScreen.VSyncKey + ":up").Enabled);
        Assert.False(Row(host, OriginalOptionsScreen.VSyncKey + ":down").Enabled);

        // The up arrow puts the window back where it stood, and picks nothing on the way.
        var arrow = Row(host, OriginalOptionsScreen.VSyncKey + ":up");
        ClickAt(host, arrow.X + 2f, arrow.Y + 2f);
        Assert.False(Row(host, OriginalOptionsScreen.VSyncKey + ":4").Visible);
        Assert.Equal(OriginalOptionsScreen.VSyncKey, host.Module.OpenVideoOption);
        Assert.Null(host.Module.VSyncChoice);

        // A press on the row outside the window lands on no row at all: it is built for the walk
        // and hidden, so the list closes on nothing rather than picking the word under the pointer.
        var hidden = Row(host, OriginalOptionsScreen.VSyncKey + ":4");
        Assert.False(hidden.Visible);
        Assert.Equal(string.Empty, HitTestKey(host, hidden.X + 2f, hidden.Y + 2f));
        ClickAt(host, hidden.X + 2f, hidden.Y + 2f);
        Assert.Null(host.Module.OpenVideoOption);
        Assert.Null(host.Module.VSyncChoice);
    }

    /// <summary>CANCEL CHANGES and Back both leave the VIDEO page with every choice dropped: the
    /// page's own plaques are its two doors back to Preferences.</summary>
    [Fact]
    public void TheVideoPageDropsTheChoiceOnCancelAndOnBack()
    {
        var host = Host();
        host.Module.OpenVideo();
        // The size row is dead under the borderless default, which owns the size, so the walk goes
        // from the monitor row straight onto Display Mode.
        Down(host);
        StepX(host, 1);
        Assert.Equal(DisplayWords.Fullscreen, host.Module.DisplayModeChoice);
        Down(host);
        StepX(host, 1);
        Assert.Equal("60", host.Module.VSyncChoice);
        Down(host);
        Accept(host);
        Assert.True(host.Module.RocketCratersChoice);
        Down(host);
        Accept(host);
        Assert.Equal(GraphicsMode.EnhancedWord, host.Module.GraphicsChoice);
        Down(host);
        Down(host);
        Assert.Equal(OriginalOptionsScreen.VideoCancelKey, host.FocusedKey);
        Assert.Null(Accept(host));
        Assert.Equal(OriginalScreen.Options, host.Screen);
        Assert.Equal(GraphicsMode.Default, host.Module.GraphicsChoice);
        Assert.Null(host.Module.DisplayModeChoice);
        Assert.Null(host.Module.VSyncChoice);
        Assert.Null(host.Module.RocketCratersChoice);

        host.Module.OpenVideo();
        Down(host);
        Down(host);
        Down(host);
        Down(host);
        Assert.Equal(OriginalOptionsScreen.GraphicsKey, host.FocusedKey);
        Accept(host);
        Assert.Equal(GraphicsMode.EnhancedWord, host.Module.GraphicsChoice);
        Assert.True(Back(host));
        Assert.Equal(OriginalScreen.Options, host.Screen);
        Assert.Equal(GraphicsMode.Default, host.Module.GraphicsChoice);
    }

    /// <summary>Opening a page shows back the saved words, not what this process resolved: a flag or
    /// the config key can have decided the running mode, and the rows owe the player the choices
    /// their own ACCEPT CHANGES saved. The module never writes the options file.</summary>
    [Fact]
    public void TheRowsOpenOnTheSavedWords()
    {
        var saved = new OptionsDef
        {
            GraphicsMode = GraphicsMode.EnhancedWord, MenuPresentation = PresentationId.BuiltIn.Value,
            Difficulty = "hardest", VSync = "120", DisplayMode = DisplayWords.Fullscreen,
            Resolution = "1920x1080", MonitorIndex = "0", NearestAfterKill = true, Rumble = false,
        };
        var host = Host(() => saved);
        host.Module.OpenGameOptions();

        Assert.Equal(GraphicsMode.EnhancedWord, host.Module.GraphicsChoice);
        Assert.Equal(CSVM.Flight.Difficulty.Hardest, host.Module.DifficultyChoice);
        Assert.True(host.Module.NearestAfterKillChoice);
        Assert.False(host.Module.RumbleChoice);
        // An older file's saved presentation word opens no row: the page reads it nowhere.
        Assert.Equal(
            new[]
            {
                OriginalOptionsScreen.DifficultyKey, OriginalOptionsScreen.DefaultViewKey, OriginalOptionsScreen.AutoHeadTurnKey,
                OriginalOptionsScreen.NearestAfterKillKey, OriginalOptionsScreen.RumbleKey,
                OriginalOptionsScreen.GameOptionsAcceptKey, OriginalOptionsScreen.GameOptionsCancelKey,
            },
            host.Rows.Select(r => r.Key));
        Assert.Equal("Hardest", Row(host, OriginalOptionsScreen.DifficultyKey).Label);
        host.Module.OpenVideo();
        Assert.Equal("120", host.Module.VSyncChoice);
        Assert.Equal("120 FPS", Row(host, OriginalOptionsScreen.VSyncKey).Label);
        Assert.Equal(DisplayWords.Fullscreen, host.Module.DisplayModeChoice);
        Assert.Equal("Fullscreen", Row(host, OriginalOptionsScreen.DisplayModeKey).Label);
        Assert.Equal("1920x1080", host.Module.ResolutionChoice);
        Assert.Equal("1920x1080", Row(host, OriginalOptionsScreen.ResolutionKey).Label);
        Assert.Equal("0", host.Module.MonitorChoice);
        Assert.Equal("Screen 0", Row(host, OriginalOptionsScreen.MonitorKey).Label);

        // A file that never set the fields opens the rows on the shipped defaults, each row agreeing
        // with its own setting's fallback rule: off and borderless rather than either vocabulary's
        // first word, the size list's fallback (the project size, with no screen to ask), the standing screen.
        saved.GraphicsMode = null;
        saved.Difficulty = null;
        saved.NearestAfterKill = null;
        saved.Rumble = null;
        saved.VSync = null;
        saved.DisplayMode = null;
        saved.Resolution = null;
        saved.MonitorIndex = "9";
        host.Module.OpenGameOptions();
        Assert.Equal(GraphicsMode.Default, host.Module.GraphicsChoice);
        Assert.Equal(CSVM.Flight.Difficulty.Normal, host.Module.DifficultyChoice);
        Assert.Null(host.Module.NearestAfterKillChoice);
        Assert.Null(host.Module.RumbleChoice);
        host.Module.OpenVideo();
        Assert.Null(host.Module.VSyncChoice);
        Assert.Equal("Off", Row(host, OriginalOptionsScreen.VSyncKey).Label);
        Assert.Null(host.Module.DisplayModeChoice);
        Assert.Equal("Borderless", Row(host, OriginalOptionsScreen.DisplayModeKey).Label);
        Assert.Null(host.Module.ResolutionChoice);
        Assert.Equal(ResolutionSetting.ProjectSize, Row(host, OriginalOptionsScreen.ResolutionKey).Label);
        Assert.Equal("9", host.Module.MonitorChoice);
        Assert.Equal("Screen 0", Row(host, OriginalOptionsScreen.MonitorKey).Label);
    }

    /// <summary>The graphics row's description reads the choice against the running mode, so a
    /// saved word the world has not picked up yet says a restart is owed rather than repeating
    /// the next-start note. Which of the two words is the running one depends on the process, so
    /// the fact checks that exactly one of them owes the restart.</summary>
    [Fact]
    public void TheGraphicsRowSaysWhenARestartIsStillOwed()
    {
        var saved = new OptionsDef { GraphicsMode = GraphicsMode.EnhancedWord };
        var host = Host(() => saved);
        host.Module.OpenVideo();
        string enhanced = Compose(host).Lines.Single(l => l.Text.StartsWith("Select the lit world.", StringComparison.Ordinal)).Text;

        saved.GraphicsMode = GraphicsMode.Default;
        host.Module.OpenVideo();
        string original = Compose(host).Lines.Single(l => l.Text.StartsWith("Select the lit world.", StringComparison.Ordinal)).Text;

        var owed = new[] { enhanced, original }.Where(t => t.EndsWith("restart to apply.", StringComparison.Ordinal)).ToList();
        var settled = new[] { enhanced, original }.Where(t => t.EndsWith("Takes effect on the next start.", StringComparison.Ordinal)).ToList();
        Assert.Single(owed);
        Assert.Single(settled);
        Assert.Contains(GraphicsMode.Enhanced ? "This run is enhanced" : "This run is original", owed[0]);
    }

    [Fact]
    public void TheGameOptionsPageIsComposedOverItsSectionsOwnRowShape()
    {
        var host = Host();
        host.Module.OpenGameOptions();

        // Row one's dropdown box at the authored Difficulty dropdown's corner and width, the Default
        // View row's one whole band down, then the two decoded plaques.
        Assert.Equal((135f, 295f, 144f, 17f), Rect(Row(host, OriginalOptionsScreen.DifficultyKey)));
        Assert.Equal((135f, 355f, 144f, 17f), Rect(Row(host, OriginalOptionsScreen.DefaultViewKey)));
        // The exit pair side by side on one line, ACCEPT left of CANCEL, which is the arrangement
        // this section authors and the film shows; the VIDEO page's own section stacks them instead.
        // Both stand one whole band below their authored 470, the band the plate grew by.
        Assert.Equal((200f, 532f, 240f, 50f), Rect(Row(host, OriginalOptionsScreen.GameOptionsAcceptKey)));
        Assert.Equal((450f, 532f, 240f, 50f), Rect(Row(host, OriginalOptionsScreen.GameOptionsCancelKey)));

        var board = Compose(host);
        // Backdrop, not pictures, for the reason the AUDIO page's own case states: a fill is drawn
        // between the layers, so a plate among the pictures buries every focus mark. It arrives as
        // four crops (head, row band twice, tail), this page holding more rows than the section has.
        Assert.Equal(new[] { "PM_Logo.png", "PP_GoBack.png", "PP_GoBack.png", "PP_GoBack.png", "PP_GoBack.png" },
            board.Backdrop.Select(p => p.Art.Name));
        Assert.Equal(
            new[] { (200f, 0f, 116f), (316f, 116f, 62f), (378f, 116f, 62f), (440f, 178f, 111f) },
            board.Backdrop.Skip(1).Select(p => (p.Y, p.Crop!.Value.Y, p.Crop!.Value.Height)));
        Assert.DoesNotContain(board.Pictures, p => p.Art.Name is "PM_Logo.png" or "PP_GoBack.png");
        Assert.Contains(board.Lines, l => l.Text == "GAME OPTIONS" && l.X == 120f && l.Justify == BoardJustify.Center);
        Assert.Contains(board.Lines, l => l.Text == "Difficulty" && l.X == 130f && l.Y == 280f && l.Width == 170f);
        Assert.Contains(board.Lines, l => l.Text == "Select the difficulty level for a solo campaign." && l.X == 340f && l.Y == 290f && l.Width == 310f);
        // Five rows over a three-row section, so the plate grows one band and each row stands on a
        // band of its own bar the two checkboxes sharing the third. The descriptions are spread
        // evenly down their own window, so this one stands above its row rather than under it.
        Assert.Contains(board.Lines, l => l.Text == "Default View" && l.X == 130f && l.Y == 340f && l.Width == 170f);
        Assert.Contains(board.Lines, l => l.Text == "Select your default view." && l.X == 340f && l.Y == 335f && l.Width == 310f);
        // A checkbox row takes the narrower title box the authored head-turn row carries. The pair
        // opens at its band's own top, the first row's 26-pixel inset above the authored line, and
        // steps by one 16-pixel checkbox, which is what fits two into a band painted for one.
        Assert.Contains(board.Lines, l => l.Text == "Auto Head Turn" && l.X == 130f && l.Y == 374f && l.Width == 112f);
        // No row offers the menu presentation: the command line alone chooses it.
        Assert.DoesNotContain(board.Lines, l => l.Text is "Menu" or "Select the menu presentation.");
        Assert.Contains(board.Lines, l => l.Text == "Next Target" && l.X == 130f && l.Y == 390f && l.Width == 112f);
        Assert.Contains(board.Lines,
            l => l.Text == "Take the nearest target after a kill instead of the first of the list."
                && l.X == 340f && l.Y == 425f && l.Width == 310f);
        // The bottom band keeps one row, so Rumble's own description clears the moved plaques.
        Assert.Contains(board.Lines, l => l.Text == "Rumble" && l.X == 130f && l.Y == 460f && l.Width == 112f);
        Assert.Contains(board.Lines,
            l => l.Text.StartsWith("Rumble the gamepad", StringComparison.Ordinal) && l.Y == 470f);
        // Five titles, five descriptions and the page's own tab title.
        Assert.Equal(11, board.Lines.Count(l => l.Row < 0));

        // The box is this page's focus mark, not standing chrome, so exactly one row carries it and
        // it is the focused one.
        Assert.Equal(
            new[] { (135f, 295f, 144f, 17f) },
            board.Fills.Where(f => f.Border).Select(f => (f.X, f.Y, f.Width, f.Height)));
        Down(host);
        Assert.Equal(
            new[] { (135f, 355f, 144f, 17f) },
            Compose(host).Fills.Where(f => f.Border).Select(f => (f.X, f.Y, f.Width, f.Height)));
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
        var host = Host();
        host.Module.OpenVideo();

        Assert.Equal((260f, 245f, 70f, 15f), Rect(Row(host, OriginalOptionsScreen.MonitorKey)));
        var resolution = Row(host, OriginalOptionsScreen.ResolutionKey);
        Assert.Equal((260f, 290f, 70f, 17f), Rect(resolution));
        // The size row keeps its authored geometry under the borderless default and draws dead.
        Assert.False(resolution.Enabled);
        Assert.Equal((260f, 335f, 70f, 17f), Rect(Row(host, OriginalOptionsScreen.DisplayModeKey)));
        Assert.Equal((260f, 380f, 70f, 17f), Rect(Row(host, OriginalOptionsScreen.VSyncKey)));
        Assert.Equal((260f, 420f, 16f, 16f), Rect(Row(host, OriginalOptionsScreen.RocketCratersKey)));
        Assert.Equal((260f, 465f, 16f, 16f), Rect(Row(host, OriginalOptionsScreen.GraphicsKey)));
        Assert.Equal((500f, 470f, 240f, 50f), Rect(Row(host, OriginalOptionsScreen.VideoAcceptKey)));
        Assert.Equal((500f, 520f, 240f, 50f), Rect(Row(host, OriginalOptionsScreen.VideoCancelKey)));

        var board = Compose(host);
        // Backdrop, not pictures, for the reason the AUDIO page's own case states: a board draws its
        // fills between the two layers, so a plate among the pictures buries every focus mark.
        Assert.Equal(new[] { "PM_Logo.png", "PP_VpBack.png" }, board.Backdrop.Select(p => p.Art.Name));
        Assert.DoesNotContain(board.Pictures, p => p.Art.Name is "PM_Logo.png" or "PP_VpBack.png");
        Assert.Contains(board.Lines, l => l.Text == "VIDEO" && l.X == 120f && l.Justify == BoardJustify.Center);
        Assert.Contains(board.Lines, l => l.Text == "Monitor" && l.X == 130f && l.Y == 245f && l.Width == 90f);
        Assert.Contains(board.Lines, l => l.Text.StartsWith("Select the monitor", StringComparison.Ordinal)
            && l.X == 340f && l.Y == 245f && l.Width == 310f);
        Assert.Contains(board.Lines, l => l.Text == "Resolution" && l.X == 130f && l.Y == 290f && l.Width == 130f);
        // The size row's description is the mode's, and nothing saved stands on the borderless
        // default, which owns the size.
        Assert.Contains(board.Lines, l => l.Text.StartsWith("Borderless runs at", StringComparison.Ordinal)
            && l.X == 340f && l.Y == 290f && l.Width == 310f);
        Assert.Contains(board.Lines, l => l.Text == "Display Mode" && l.X == 130f && l.Y == 335f && l.Width == 130f);
        Assert.Contains(board.Lines, l => l.Text.StartsWith("Select how the window sits", StringComparison.Ordinal)
            && l.X == 340f && l.Y == 335f && l.Width == 310f);
        Assert.Contains(board.Lines, l => l.Text == "V-Sync" && l.X == 130f && l.Y == 380f && l.Width == 130f);
        Assert.Contains(board.Lines, l => l.Text.StartsWith("Select the frame pacing.", StringComparison.Ordinal)
            && l.X == 340f && l.Y == 380f && l.Width == 310f);
        // The carve row stands on the authored Clutter Detail line, the checkbox line above Shadows,
        // and its description wraps at the plaque column for the same reason the Shadows one does.
        Assert.Contains(board.Lines, l => l.Text == "Rocket Craters" && l.X == 130f && l.Y == 425f && l.Width == 130f);
        Assert.Contains(board.Lines, l => l.Text.StartsWith("Let a rocket's ground burst", StringComparison.Ordinal)
            && l.X == 340f && l.Y == 425f && l.Width == 160f);
        Assert.Contains(board.Lines, l => l.Text == "Enhanced Graphics" && l.X == 130f && l.Y == 470f && l.Width == 130f);
        Assert.Contains(board.Lines, l => l.Text.StartsWith("Select the lit world.", StringComparison.Ordinal)
            && l.X == 340f && l.Y == 470f && l.Width == 160f);
        Assert.Equal(13, board.Lines.Count(l => l.Row < 0));
        // Both checkboxes draw unchecked and unfocused, the page opening on the monitor row above
        // them: the second of their eight frames.
        Assert.Equal(new[] { 1, 1 }, board.Plaques.Where(p => p.Art.Name == "PP_B_Check8.png").Select(p => p.Frame));

        // The box marks the focused dropdown and no other, and it follows the cursor past the dead
        // size row onto the display mode's. Four rows down it stands on the second checkbox, a
        // plaque strip that takes no box at all, so the page draws none.
        Assert.Equal(
            new[] { (260f, 245f, 70f, 15f) },
            board.Fills.Where(f => f.Border).Select(f => (f.X, f.Y, f.Width, f.Height)));
        Down(host);
        Assert.Equal(
            new[] { (260f, 335f, 70f, 17f) },
            Compose(host).Fills.Where(f => f.Border).Select(f => (f.X, f.Y, f.Width, f.Height)));
        Down(host);
        Down(host);
        Assert.Equal(OriginalOptionsScreen.RocketCratersKey, host.FocusedKey);
        Assert.Empty(Compose(host).Fills.Where(f => f.Border));
        Down(host);
        Assert.Equal(OriginalOptionsScreen.GraphicsKey, host.FocusedKey);
        Assert.Empty(Compose(host).Fills.Where(f => f.Border));
    }

    [Fact]
    public void EveryActionBelongsToExactlyOneCategoryTab()
    {
        var seen = new Dictionary<InputAction, int>();
        foreach (var tab in OriginalOptionsScreen.ControlTabs)
        {
            foreach (var row in tab.Rows)
            {
                seen[row.Action] = seen.TryGetValue(row.Action, out int count) ? count + 1 : 1;
            }
        }

        foreach (var action in Enum.GetValues<InputAction>())
        {
            Assert.True(seen.TryGetValue(action, out int count) && count == 1,
                $"{action} is listed {(seen.TryGetValue(action, out int n) ? n : 0)} times across the seven tabs");
        }
    }

    [Fact]
    public void TheSixNamedTabsAreFlightActionsAndOtherHoldsWhatTheOriginalNeverBound()
    {
        var tabs = OriginalOptionsScreen.ControlTabs;
        Assert.Equal(7, tabs.Count);
        Assert.Equal(
            new[] { "Movement", "Throttle", "Weapons", "Targeting", "Views 1", "Views 2", "Other" },
            tabs.Select(t => t.Name).ToArray());
        for (int i = 0; i < 6; i++)
        {
            Assert.All(tabs[i].Rows, row => Assert.Equal(InputContext.Flight, row.Context));
        }

        var other = tabs[6].Rows;
        Assert.Contains(other, row => row.Context == InputContext.Menu);
        Assert.Contains(other, row => row.Context == InputContext.Camera);
        // Every menu and free-camera action, which the original binds on no page of its own.
        foreach (var context in new[] { InputContext.Menu, InputContext.Camera })
        {
            foreach (var action in DefaultBindings.ActionsIn(context))
            {
                Assert.Contains(other, row => row.Context == context && row.Action == action);
            }
        }
    }

    /// <summary>The Movement tab is the six attitude half-axes in the original page's own order,
    /// which begins with Point Nose Down rather than with the enum's first member
    /// (<c>OriginalScreenshots/Keybinds Movement.png</c>).</summary>
    [Fact]
    public void TheMovementTabIsTheSixAttitudeHalfAxesInTheOriginalsOrder()
    {
        Assert.Equal(
            new[]
            {
                InputAction.PitchDown, InputAction.PitchUp, InputAction.RollLeft, InputAction.RollRight,
                InputAction.YawLeft, InputAction.YawRight,
            },
            OriginalOptionsScreen.ControlTabs[0].Rows.Select(r => r.Action).ToArray());
    }

    /// <summary>The Throttle tab is the two lever keys and then the nine absolute eighths, which are
    /// the digit row the original reserves for them.</summary>
    [Fact]
    public void TheThrottleTabCarriesTheNineEighthsBelowTheLeverPair()
    {
        var rows = OriginalOptionsScreen.ControlTabs[1].Rows.Select(r => r.Action).ToArray();

        Assert.Equal(InputAction.ThrottleUp, rows[0]);
        Assert.Equal(InputAction.ThrottleDown, rows[1]);
        for (int eighths = 0; eighths <= 8; eighths++)
        {
            Assert.Equal(InputAction.ThrottleSet0 + eighths, rows[2 + eighths]);
        }

        Assert.Equal(11, rows.Length);
    }

    /// <summary>The Targeting tab lists all eleven of the original's targeting actions in the
    /// original page's own order, Next then Previous then Nearest per class and the two class-less
    /// ones last (<c>OriginalScreenshots/Keybinds Targeting.png</c>). Pinned because the order is
    /// read off that page rather than off the enum, which appends new members at its end.</summary>
    [Fact]
    public void TheTargetingTabIsTheOriginalsElevenInItsOwnOrder()
    {
        Assert.Equal(
            new[]
            {
                InputAction.TargetNextEnemy, InputAction.TargetPreviousEnemy, InputAction.TargetNearestEnemy,
                InputAction.TargetNextAlly, InputAction.TargetPreviousAlly, InputAction.TargetNearestAlly,
                InputAction.TargetNextNonAircraft, InputAction.TargetPreviousNonAircraft,
                InputAction.TargetNearestNonAircraft, InputAction.TargetNearest, InputAction.TargetClear,
            },
            OriginalOptionsScreen.ControlTabs[3].Rows.Select(r => r.Action).ToArray());
    }

    [Fact]
    public void TheKeysPageAuthorsCancelLeftOfAccept()
    {
        var host = Host(controls: Controls(out _, out _));
        host.Module.OpenKeys();
        var cancel = Row(host, OriginalOptionsScreen.KeysCancelKey);
        var accept = Row(host, OriginalOptionsScreen.KeysAcceptKey);
        Assert.True(cancel.X < accept.X, $"CANCEL CHANGES stands left of ACCEPT CHANGES ({cancel.X} vs {accept.X})");
        Assert.Equal(cancel.Y, accept.Y);
    }

    [Fact]
    public void ACellNamesTheActionItsRowStandsOnAndArmsACaptureOnItsOwnSlot()
    {
        var controls = Controls(out _, out _);
        var host = Host(controls: controls);
        host.Module.OpenKeys();
        var tab = OriginalOptionsScreen.ControlTabs[0];
        int row = tab.Rows.Count - 1;
        Click(host, OriginalOptionsScreen.KeysCellKey(row, second: false));
        Assert.Equal(tab.Rows[row].Context, controls.Context);
        Assert.Equal(tab.Rows[row].Action, controls.Focused);
        Assert.Equal(0, controls.Slot);
        Assert.True(controls.Capturing);

        controls.CancelCapture();
        Click(host, OriginalOptionsScreen.KeysCellKey(row, second: true));
        Assert.Equal(tab.Rows[row].Action, controls.Focused);
        Assert.Equal(Math.Min(1, controls.FocusedBindings.Count), controls.Slot);
    }

    [Fact]
    public void ARowWithMoreControlsThanColumnsSaysHowManyItIsNotShowing()
    {
        var controls = Controls(out _, out _);
        var host = Host(controls: controls);
        host.Module.OpenKeys();
        var action = OriginalOptionsScreen.ControlTabs[0].Rows[0].Action;
        controls.Context = InputContext.Flight;
        controls.Focus(IndexOf(controls, action));
        while (controls.FocusedBindings.Count > 0)
        {
            controls.MoveSlot(0);
            controls.UnbindSlot();
        }

        foreach (var key in new[] { Godot.Key.M, Godot.Key.N, Godot.Key.B })
        {
            controls.MoveSlot(9);
            controls.Offer(new Binding(DeviceId.Keyboard, BindingControl.Key((int)key)));
            // A key the shipped set already holds asks first; this row wants all three either way.
            controls.ConfirmSteal();
        }

        var text = host.Module.KeysCellText(0);
        Assert.Equal("M", text.A);
        Assert.Equal("N, +1 more", text.B);
    }

    [Fact]
    public void TheKeysPageDrawsItsTabsWithTheStandingOneDepressed()
    {
        var host = Host(controls: Controls(out _, out _));
        host.Module.OpenKeys();
        Click(host, OriginalOptionsScreen.KeysTabKey(2));
        var board = Compose(host);
        var tabs = board.Plaques.Where(p => p.Label.Length > 0).ToList();
        Assert.Equal(OriginalOptionsScreen.ControlTabs.Select(t => t.Name).ToArray(), tabs.Select(p => p.Label).ToArray());
        Assert.Equal(3, tabs[2].Frame);
        Assert.All(tabs.Where((_, i) => i != 2), p => Assert.NotEqual(3, p.Frame));
        Assert.Contains(board.Lines, l => l.Text == "Weapons");
        Assert.Contains(board.Lines, l => l.Text == "Action");
        Assert.Contains(board.Lines, l => l.Text == "Control A");
        Assert.Contains(board.Lines, l => l.Text == "Control B");
    }

    [Fact]
    public void TheControlsPageDrawsItsSeatRowAndItsFlyingSchemeRow()
    {
        var controls = Controls(out _, out _);
        var host = Host(controls: controls);
        host.Module.OpenControlsPrefs();
        Assert.Equal("Player 1", Row(host, OriginalOptionsScreen.ControlsPlayerKey).Label);
        Assert.Equal("Look", Row(host, OriginalOptionsScreen.ControlsMouseKey).Label);
        var board = Compose(host);
        Assert.Contains(board.Lines, l => l.Text == "CONTROLS");
        Assert.Contains(board.Lines, l => l.Text == "Player");
        Assert.Contains(board.Lines, l => l.Text == "Mouse");
        Assert.DoesNotContain(board.Lines, l => l.Text == "Mouse Sensitivity");
        Assert.Contains(board.Lines, l => l.Text.StartsWith("Configure the keyboard", StringComparison.Ordinal));
        Assert.Equal(1, controls.Players.Count);
    }

    /// <summary>Each CONTROLS row takes its rectangle from its own layout entry: the seat chooser
    /// from the Controller Type dropdown's box, the sensitivity slider from the Mouse Sensitivity
    /// slider's own press region, and the scheme chooser from the right half of the seat box's
    /// column on the Mouse Sensitivity title's line.</summary>
    [Fact]
    public void TheControlsRowsTakeTheirBoxesFromTheirOwnLayoutEntries()
    {
        var host = Host(controls: Controls(out _, out _));
        host.Module.OpenControlsPrefs();
        Assert.Equal((128f, 305f, 175f, 17f), Rect(Row(host, OriginalOptionsScreen.ControlsPlayerKey)));
        // 16 rather than the item height's 17: the fixture's slider region starts at 371.
        Assert.Equal((215.5f, 355f, 87.5f, 16f), Rect(Row(host, OriginalOptionsScreen.ControlsMouseKey)));
        Assert.Equal((136f, 371f, 170f, 23f), Rect(Row(host, OriginalOptionsScreen.ControlsSensitivityKey)));
    }

    /// <summary>The scheme chooser's arrow ends where the seat row's does, and the panel's title
    /// stops where the chooser on its line starts, so the two share the line without touching.
    /// </summary>
    [Fact]
    public void TheFlyingSchemeRowSharesTheTitleLineAndTheSeatRowsArrowColumn()
    {
        var host = Host(controls: Controls(out _, out _));
        host.Module.OpenControlsPrefs();
        var seat = Row(host, OriginalOptionsScreen.ControlsPlayerKey);
        var scheme = Row(host, OriginalOptionsScreen.ControlsMouseKey);
        Assert.Equal(seat.X + seat.Width, scheme.X + scheme.Width);

        var board = Compose(host);
        var arrows = board.Pictures.Where(p => p.Art.Name == seat.Art!.Name).ToArray();
        Assert.Equal(2, arrows.Length);
        Assert.Equal(arrows[0].X, arrows[1].X);

        var title = board.Lines.Single(l => l.Text == "Mouse");
        Assert.Equal(scheme.Y, title.Y);
        Assert.Equal(scheme.X, title.X + title.Width);
    }

    /// <summary>The authored Mouse Sensitivity slider is drawn as authored, slot and thumb, standing
    /// at the default's level in the middle of its scale, and moving it stages the seat's
    /// sensitivity until ACCEPT CHANGES writes it.</summary>
    [Fact]
    public void TheSensitivitySliderStagesTheMultiplierAndAcceptWritesIt()
    {
        var controls = Controls(out _, out var written);
        var host = Host(controls: controls);
        host.Module.OpenControlsPrefs();
        var slider = Row(host, OriginalOptionsScreen.ControlsSensitivityKey);
        Assert.Equal(OriginalRowKind.Slider, slider.Kind);
        Assert.Equal(SensitivityScale.Level(SensitivityScale.Default), slider.Slider!.Value);
        Assert.Contains(Compose(host).Pictures, p => p.Art.Name == slider.Slider.Slot!.Name);

        slider.Slider.SetValue(75);

        Assert.Equal(2f, controls.MouseSensitivity);
        Assert.Empty(written);
        Assert.Equal(75, Row(host, OriginalOptionsScreen.ControlsSensitivityKey).Slider!.Value);

        Click(host, OriginalOptionsScreen.ControlsAcceptKey);

        Assert.Equal(new[] { 1 }, written);
        Assert.Equal(2f, controls.MouseSensitivity);
    }

    /// <summary>The scheme row is the Controls door's own: a press flips it, a sideways step flips
    /// it back, and neither reaches the seat until ACCEPT CHANGES.</summary>
    [Fact]
    public void TheFlyingSchemeRowStagesTheChoiceAndAcceptWritesIt()
    {
        var controls = Controls(out _, out var written);
        var host = Host(controls: controls);
        host.Module.OpenControlsPrefs();

        Click(host, OriginalOptionsScreen.ControlsMouseKey);

        Assert.True(controls.MouseFlying);
        Assert.Equal("Fly", Row(host, OriginalOptionsScreen.ControlsMouseKey).Label);
        Assert.Empty(written);

        StepX(host, 1);

        Assert.False(controls.MouseFlying);

        StepX(host, -1);
        Click(host, OriginalOptionsScreen.ControlsAcceptKey);

        Assert.True(controls.MouseFlying);
        Assert.Equal(new[] { 1 }, written);
    }

    [Fact]
    public void TheGameOptionsRowsClearEachOtherAndTheirWordsOverTheFixture()
    {
        GameOptionRowsAreClearOfEachOther(MenuLayoutReaderTests.OriginalLayout(), Measure);
    }

    [ExtractedDataFact]
    public void TheGameOptionsRowsClearEachOtherAndTheirWordsOverTheInstall()
    {
        GameOptionRowsAreClearOfEachOther(InstallLayout(out var measure), measure);
    }

    [Fact]
    public void TheAudioRowsClearEachOtherAndTheirWordsOverTheFixture()
    {
        AudioRowsAreClearOfEachOther(MenuLayoutReaderTests.OriginalLayout(), Measure);
    }

    [ExtractedDataFact]
    public void TheAudioRowsClearEachOtherAndTheirWordsOverTheInstall()
    {
        AudioRowsAreClearOfEachOther(InstallLayout(out var measure), measure);
    }

    [Fact]
    public void TheVideoRowsClearEachOtherAndTheirWordsOverTheFixture()
    {
        VideoRowsAreClearOfEachOther(MenuLayoutReaderTests.OriginalLayout(), Measure);
    }

    [ExtractedDataFact]
    public void TheVideoRowsClearEachOtherAndTheirWordsOverTheInstall()
    {
        VideoRowsAreClearOfEachOther(InstallLayout(out var measure), measure);
    }

    [Fact]
    public void TheControlsRowsClearEachOtherAndTheirWordsOverTheFixture()
    {
        ControlsRowsAreClearOfEachOther(MenuLayoutReaderTests.OriginalLayout(), Measure);
    }

    [ExtractedDataFact]
    public void TheControlsRowsClearEachOtherAndTheirWordsOverTheInstall()
    {
        ControlsRowsAreClearOfEachOther(InstallLayout(out var measure), measure);
    }

    // The Game Options page's seven rows share one plate, so no two of them may overlap, and no
    // control may sit over a title or a description. The first three are the original's own, in its
    // order; the two under them are this port's.
    private static void GameOptionRowsAreClearOfEachOther(MenuLayout layout, Func<string, (int Width, int Height)?> measure)
    {
        var host = Host(layout, measure);
        host.Module.OpenGameOptions();
        var rows = host.Rows.ToArray();
        Assert.Equal(
            new[]
            {
                OriginalOptionsScreen.DifficultyKey, OriginalOptionsScreen.DefaultViewKey,
                OriginalOptionsScreen.AutoHeadTurnKey,
                OriginalOptionsScreen.NearestAfterKillKey, OriginalOptionsScreen.RumbleKey,
                OriginalOptionsScreen.GameOptionsAcceptKey, OriginalOptionsScreen.GameOptionsCancelKey,
            },
            rows.Select(r => r.Key));
        RowsAreClearOfEachOther(host, rows, "GAME OPTIONS", 10);
    }

    // The AUDIO page's rows, the same rule over its own plate. Its press regions are the widest of
    // any option page (the authored slot grown ten pixels above and below), and the Master row's
    // slider stands on a line the layout authors for a checkbox, so a row that took the checkbox's
    // own corner would land in the title column beside the words rather than under them.
    private static void AudioRowsAreClearOfEachOther(MenuLayout layout, Func<string, (int Width, int Height)?> measure)
    {
        var host = Host(layout, measure);
        host.Module.OpenAudio();
        var rows = host.Rows.ToArray();
        Assert.Equal(
            new[]
            {
                OriginalOptionsScreen.AudioMasterKey, OriginalOptionsScreen.AudioMusicKey,
                OriginalOptionsScreen.AudioEffectsKey, OriginalOptionsScreen.AudioVoiceKey,
                OriginalOptionsScreen.AudioAcceptKey, OriginalOptionsScreen.AudioCancelKey,
            },
            rows.Select(r => r.Key));
        RowsAreClearOfEachOther(host, rows, "AUDIO", 8);
    }

    // The VIDEO page's rows, the same rule over its own plate: each control must stand clear of the
    // title beside it and the description must stop before the plaque column, which is what makes
    // the authored Shadows row carry a longer title and a longer description than it was written
    // for. The rows come back in authored order, since that is the order the cursor walks.
    private static void VideoRowsAreClearOfEachOther(MenuLayout layout, Func<string, (int Width, int Height)?> measure)
    {
        var host = Host(layout, measure);
        host.Module.OpenVideo();
        var rows = host.Rows.ToArray();
        Assert.Equal(
            new[]
            {
                OriginalOptionsScreen.MonitorKey, OriginalOptionsScreen.ResolutionKey, OriginalOptionsScreen.DisplayModeKey,
                OriginalOptionsScreen.VSyncKey, OriginalOptionsScreen.RocketCratersKey, OriginalOptionsScreen.GraphicsKey,
                OriginalOptionsScreen.VideoAcceptKey, OriginalOptionsScreen.VideoCancelKey,
            },
            rows.Select(r => r.Key));
        RowsAreClearOfEachOther(host, rows, "VIDEO", 12);
    }

    // The CONTROLS page's rows, the same rule over its own plate, with the sensitivity slider pinned
    // to the Mouse Sensitivity row's own authored press region (the slot art at that row's corner,
    // the four insets applied), so a layout whose entries differ in size or column puts the slider
    // where its own entry says and the scheme chooser on the title line above it.
    private static void ControlsRowsAreClearOfEachOther(MenuLayout layout, Func<string, (int Width, int Height)?> measure)
    {
        var host = Host(layout, measure, controls: Controls(out _, out _));
        host.Module.OpenControlsPrefs();
        var rows = host.Rows.ToArray();
        Assert.Equal(
            new[]
            {
                OriginalOptionsScreen.ControlsPlayerKey, OriginalOptionsScreen.ControlsMouseKey,
                OriginalOptionsScreen.ControlsSensitivityKey,
                OriginalOptionsScreen.KeysDoorKey, OriginalOptionsScreen.ControlsAcceptKey,
                OriginalOptionsScreen.ControlsCancelKey,
            },
            rows.Select(r => r.Key));

        var mouse = layout.Screen(OriginalOptionsScreen.ControlsPrefsSection)!.Widget(OriginalOptionsScreen.ControlsSensitivityKey)!;
        var slot = measure(mouse.Art[0])!.Value;
        Assert.Equal(
            ((float)(mouse.Int("X") + mouse.Int("Left")),
             (float)(mouse.Int("Y") + mouse.Int("Top")),
             (float)(slot.Width - mouse.Int("Right") - mouse.Int("Left")),
             (float)(slot.Height - mouse.Int("Bottom") - mouse.Int("Top"))),
            Rect(Row(host, OriginalOptionsScreen.ControlsSensitivityKey)));

        RowsAreClearOfEachOther(host, rows, "CONTROLS", 5);
    }

    // No row of an option page may overlap another, and no control may sit over a title or a
    // description: a control that covered the words beside it is what sent the options off the
    // Preferences page in the first place. The authored space scales uniformly, so disjoint here is
    // disjoint at every window size.
    private static void RowsAreClearOfEachOther(OptionsHost host, OriginalRow[] rows, string pageTitle, int wordCount)
    {
        var words = Compose(host).Lines.Where(l => l.Row < 0 && l.Text != pageTitle).ToArray();
        Assert.Equal(wordCount, words.Length);
        foreach (var row in rows)
        {
            foreach (var line in words)
            {
                bool clear = line.Y + line.Size <= row.Y || row.Y + row.Height <= line.Y
                    || line.X + line.Width <= row.X || row.X + row.Width <= line.X;
                Assert.True(clear, $"{row.Key} at ({row.X}, {row.Y}, {row.Width}, {row.Height}) covers " +
                    $"'{line.Text}' at ({line.X}, {line.Y}, {line.Width}, {line.Size})");
            }
        }

        for (int i = 0; i < rows.Length; i++)
        {
            for (int j = i + 1; j < rows.Length; j++)
            {
                var a = rows[i];
                var b = rows[j];
                bool clear = a.X + a.Width <= b.X || b.X + b.Width <= a.X
                    || a.Y + a.Height <= b.Y || b.Y + b.Height <= a.Y;
                Assert.True(clear, $"{a.Key} at ({a.X}, {a.Y}, {a.Width}, {a.Height}) overlaps " +
                    $"{b.Key} at ({b.X}, {b.Y}, {b.Width}, {b.Height})");
            }
        }
    }

    // The install's own decoded layout and the art beside it, which the three clearance facts walk
    // as well as the fixture: the authored rectangles there are the game's, not this repo's.
    private static MenuLayout InstallLayout(out Func<string, (int Width, int Height)?> measure)
    {
        string dataRoot = TestData.DataRoot!;
        var layout = MenuLayout.TryLoad(MenuLayout.PathUnder(dataRoot), out var reason);
        Assert.True(layout != null, reason ?? "the install's decoded layout reads");
        measure = art => OriginalCoverageTests.PngSize(OriginalAvailability.ArtPath(dataRoot, art));
        return layout!;
    }

    // The module over the layout fixture and the fixture's own art sizes. With no options reader the
    // pages open on the shipped defaults, which is what an engine-free test wants, and no test can
    // reach the player's own user://options.json.
    private static OptionsHost Host(Func<OptionsDef>? options = null, ControlsFeature? controls = null) =>
        Host(MenuLayoutReaderTests.OriginalLayout(), Measure, options, controls);

    private static OptionsHost Host(
        MenuLayout layout, Func<string, (int Width, int Height)?> measure,
        Func<OptionsDef>? options = null, ControlsFeature? controls = null)
    {
        var host = new OptionsHost(measure);
        host.Module = new OriginalOptionsScreen(layout, host, options, controls: controls);
        return host;
    }

    // The shared rebinding feature with one seat on the shipped keymaps and a save that only
    // counts: the two CONTROLS pages stage their edits in it and ACCEPT CHANGES is the one writer.
    private static ControlsFeature Controls(out OriginalControlsTests.FakeCaptureDevices devices, out List<int> written)
    {
        var saved = new List<int>();
        written = saved;
        devices = new OriginalControlsTests.FakeCaptureDevices();
        var controls = new ControlsFeature((player, _) => saved.Add(player));
        controls.AddSeat(1, OriginalControlsTests.Profile(), devices, readsKeyboard: true);
        return controls;
    }

    private static int IndexOf(ControlsFeature controls, InputAction action)
    {
        var actions = controls.Actions;
        for (int i = 0; i < actions.Count; i++)
        {
            if (actions[i] == action)
            {
                return i;
            }
        }

        return 0;
    }

    private static OriginalRow Row(OptionsHost host, string key) => host.Rows.Single(r => r.Key == key);

    private static (float X, float Y, float Width, float Height) Rect(OriginalRow row) =>
        (row.X, row.Y, row.Width, row.Height);

    // The cursor's walk down and up a column, the shell's own rule restated: within the focused
    // row's column, enabled rows only, wrapping at either end.
    private static void Down(OptionsHost host) => MoveY(host, 1);

    private static void MoveY(OptionsHost host, int direction)
    {
        var rows = host.Rows;
        int focus = host.Focus;
        if (focus >= 0)
        {
            int column = rows[focus].Column;
            int i = focus;
            for (int n = 0; n < rows.Count; n++)
            {
                i = (i + direction + rows.Count) % rows.Count;
                if (rows[i].Column == column && rows[i].Enabled)
                {
                    focus = i;
                    break;
                }
            }

            host.FocusedRow = focus;
        }

        EndFrame(host);
    }

    // A sideways step, the shell's own cascade restated as far as this module reaches: a slider
    // first, since it belongs to no one screen, then the page's own row. Where neither takes it the
    // shell crosses columns, which is its business and not the module's.
    private static bool StepX(OptionsHost host, int direction)
    {
        var rows = host.Rows;
        int focus = host.Focus;
        bool took = SliderControl.StepValue(rows, focus, direction)
            || host.Module.StepSideways(rows, focus, direction);
        EndFrame(host);
        return took;
    }

    private static MenuExit? Accept(OptionsHost host)
    {
        var exit = host.Module.Activate(host.Rows[host.Focus]);
        EndFrame(host);
        return exit;
    }

    private static bool Back(OptionsHost host)
    {
        bool answered = host.Module.Back();
        EndFrame(host);
        return answered;
    }

    // One click at a point, as the shell reads it: the topmost visible row the point lands on takes
    // the press and the release on it activates, and a click on no row at all closes an open list.
    private static MenuExit? ClickAt(OptionsHost host, float x, float y)
    {
        var rows = host.Rows;
        int over = HitTest(rows, x, y);
        MenuExit? exit = null;
        if (over >= 0 && rows[over].Enabled)
        {
            host.PointAt(over, rows[over]);
            exit = host.Module.Activate(rows[over]);
        }
        else
        {
            host.Module.CloseDropdown();
        }

        EndFrame(host);
        return exit;
    }

    // The same click on the centre of the row carrying a key, since two authored rows may share an
    // edge pixel.
    private static MenuExit? Click(OptionsHost host, string key)
    {
        var row = Row(host, key);
        return ClickAt(host, row.X + (row.Width / 2f), row.Y + (row.Height / 2f));
    }

    // One frame of the pointer against a slider, which is the shell's own hold-and-move: it has the
    // press before any row does, so the click that took hold of a track is spent on it.
    private static void Drag(OptionsHost host, float x, float y, bool pressed, bool clicked = false)
    {
        host.Slider.Drive(host.Rows, new MenuPointer(x, y, pressed, clicked), out _);
        EndFrame(host);
    }

    // A wheel step over whatever list the point stands in, the shell's own reading.
    private static void Wheel(OptionsHost host, float x, float y, int steps)
    {
        foreach (var list in Lists(host))
        {
            if (!list.Window.Contains(x, y))
            {
                continue;
            }

            int top = list.Window.TopAfterWheel(steps);
            if (top != list.Window.Top)
            {
                list.ScrollTo(top);
            }

            return;
        }
    }

    // The end of a frame, where the shell puts the list windows back over the cursor.
    private static void EndFrame(OptionsHost host) => host.Module.SyncWindows();

    private static int HitTest(IReadOnlyList<OriginalRow> rows, float x, float y)
    {
        // Later rows draw over earlier ones, so the last hit wins.
        for (int i = rows.Count - 1; i >= 0; i--)
        {
            if (rows[i].Visible && rows[i].Contains(x, y))
            {
                return i;
            }
        }

        return -1;
    }

    private static string HitTestKey(OptionsHost host, float x, float y)
    {
        var rows = host.Rows;
        int over = HitTest(rows, x, y);
        return over >= 0 ? rows[over].Key : string.Empty;
    }

    private static IReadOnlyList<OriginalList> Lists(OptionsHost host)
    {
        var lists = new List<OriginalList>();
        if (host.Module.Owns(host.Screen))
        {
            host.Module.Lists(lists);
        }

        return lists;
    }

    // The standing list's panel: the one overlay carrying words.
    private static BoardPanel Box(OptionsHost host) => Compose(host).Overlays.First(o => o.Lines.Count > 0);

    // The page as the module draws it, assembled the way the shell assembles its own board. The
    // shell's own layers (the flag movie, the strokes and the pointer overlay) are not the module's,
    // and none of the five pages writes a note, so the note layer comes back empty.
    private static ComposedBoard Compose(OptionsHost host)
    {
        var rows = host.Rows;
        var backdrop = new List<BoardPicture>();
        var pictures = new List<BoardPicture>();
        var fills = new List<BoardFill>();
        var lines = new List<BoardLine>();
        var plaques = new List<BoardPlaque>();
        var notes = new List<BoardNote>();
        var overlays = new List<BoardPanel>();
        // The pen the seam offers is the campaign scrapbook's alone, so no option page writes a stroke.
        var strokes = new List<BoardStroke>();
        host.Module.Compose(rows, host.Focus, backdrop, pictures, fills, strokes, lines, plaques, notes, overlays);
        return new ComposedBoard(pictures, strokes, lines, plaques, notes,
            backdrop: backdrop, fills: fills, overlays: overlays);
    }

    // The fixture's strips: every button strip 240x200 (four 50-pixel frames), the checkbox 16x128
    // (eight 16-pixel frames), the dropdown arrows 15x56, the tab strip 120x148, the slider's slot
    // and thumb and the scroll art at their shipped sizes, the other two plates unmeasured.
    private static (int Width, int Height)? Measure(string art) => art switch
    {
        // The Game Options plate at the shipped size, the one plate this fixture measures: an
        // unmeasured plate has no band to repeat and no height to cap the growth against, so leaving
        // it out would take the whole plate-growth rule out of these facts.
        "PP_GoBack.png" => (566, 289),
        "PM_B_Paper.png" => (160, 112),
        "PP_B_Large.png" => (120, 148),
        "PP_B_Check8.png" => (16, 128),
        "PP_B_DropUp.png" or "PP_B_DropDown.png" => (15, 56),
        // The option pages' scroll art at the shipped sizes: four-frame arrows and a one-frame bar.
        "PP_B_ScrollUp.png" or "PP_B_ScrollDown.png" or "PP_B_KbUp.png" or "PP_B_KbDown.png" => (16, 44),
        "PP_B_ScrollBar.png" or "PP_B_KbBar.png" => (16, 11),
        // The section's own slider art and the shipped names a row with no slider widget falls
        // back to, both at the shipped sizes: a three-pixel slot and a thumb that clears it.
        "PP_B_SliderSlot.png" or "PF_B_SliderSlot.png" => (171, 3),
        "PP_B_Slider.png" or "PF_B_Slider.png" => (43, 21),
        _ when art.StartsWith("PM_B_", StringComparison.Ordinal) => (240, 200),
        _ when art.StartsWith("PP_B_", StringComparison.Ordinal) => (240, 200),
        _ => null,
    };

    /// <summary>The shell's side of the seam, hand-written: the screen showing, one focus per screen
    /// (the first live row where none was set, as the shell's own EnsureFocus rules), the pointer's
    /// row as a frame leaves it, and the slider the shell drives ahead of its rows. The rows are the
    /// module's own only while the screen showing is one of its five, which is where the shell's own
    /// dispatch sends them.</summary>
    private sealed class OptionsHost : IOriginalScreenHost
    {
        // The shell's own plate-page numbers, so a row this fake places or marks stands where the
        // shell would place or mark it.
        private const float ArrowWidth = 15f;
        private const float ArrowHeight = 14f;
        private const float ItemFont = 13f;

        private readonly Func<string, (int Width, int Height)?> _measure;
        private readonly int[] _focus = new int[Enum.GetValues<OriginalScreen>().Length];
        private int _hover = -1;
        private int _pressed = -1;
        private (float X, float Y)? _pointer;

        internal OptionsHost(Func<string, (int Width, int Height)?> measure)
        {
            _measure = measure;
            Array.Fill(_focus, -1);
        }

        public OriginalOptionsScreen Module { get; set; } = null!;

        public OriginalScreen Screen { get; private set; } = OriginalScreen.Options;

        public bool DialogOpen => false;

        public int PressedRow => _pressed;

        public int HoveredRow => _hover;

        public int FocusBeforeDialog => -1;

        public (float X, float Y)? Pointer => _pointer;

        public CSVM.Flight.CustomPlaneStore? CampaignPlanes => null;

        public UiStrings MenuStrings => UiStrings.Empty;

        public bool CanBuildPlane => false;

        public IReadOnlyList<OriginalRow> Rows
        {
            get
            {
                var rows = new List<OriginalRow>();
                if (Module.Owns(Screen))
                {
                    Module.BuildRows(rows);
                }

                return rows;
            }
        }

        public int Focus
        {
            get
            {
                var rows = Rows;
                int focus = _focus[(int)Screen];
                if (focus >= 0 && focus < rows.Count && rows[focus].Enabled)
                {
                    return focus;
                }

                focus = rows.ToList().FindIndex(r => r.Enabled);
                _focus[(int)Screen] = focus;
                return focus;
            }
        }

        public string FocusedKey
        {
            get
            {
                int focus = Focus;
                return focus >= 0 ? Rows[focus].Key : string.Empty;
            }
        }

        public int FocusedRow
        {
            get => _focus[(int)Screen];
            set => _focus[(int)Screen] = value;
        }

        /// <summary>The shell's one slider, which is no module's: a page's four tracks are driven
        /// through it because the pointer's hold crosses screens.</summary>
        internal SliderControl Slider { get; } = new();

        public void Open(OriginalScreen screen)
        {
            Screen = screen;
            Slider.LetGo();
            _pressed = -1;
            // The shell tells the module every screen it opens, which is what re-reads the saved
            // settings the three showing pages owe the player.
            Module.ScreenOpened(screen);
        }

        public void FocusKey(string key)
        {
            var rows = Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Key == key)
                {
                    _focus[(int)Screen] = i;
                    return;
                }
            }
        }

        public void RaiseDialog(string message, DialogIcon icon, params OriginalDialogAnswer[] answers)
        {
        }

        public void CloseDialog()
        {
        }

        // No frame loop behind this fake, so a module's own re-entrant press has nothing to run.
        public void Frame(MenuCommands commands)
        {
        }

        public void PlayFilm(Action<Action> play, Action then) => OriginalTestHost.PlayFilm(play, then);

        public (int Width, int Height)? Measure(string art) => art.Length > 0 ? _measure(art) : null;

        public void ResumeCampaign()
        {
        }

        public void RefreshInstantActionRoster()
        {
        }

        public void RefreshRosterFromStore()
        {
        }

        public void OpenHangar(IHangarWallet? wallet)
        {
        }

        public MenuExit? BeginSeatWalk() => null;

        public int CheatedMission(int ordinary) => OriginalTestHost.CheatedMission(ordinary);

        public BoardPanel? SeatPanel(bool onPaper) => null;

        /// <summary>The shell's own plate-row rule restated, so a page's drawing can be read with no
        /// shell behind it: a dropdown's value in its box under the focus outline, a slider's slot
        /// and thumb over the wash, and the two plaque kinds in their state frames. ⚠ Keep it in
        /// step with <c>OriginalShell.ComposePlateRow</c>: the facts that pin a row's art, its words
        /// and its focus mark read what this writes.</summary>
        public void ComposeGenericRow(
            OriginalRow row, bool focused, bool pressed, int index,
            List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques, List<BoardPicture> pictures)
        {
            switch (row.Kind)
            {
                case OriginalRowKind.Dropdown:
                    if (focused)
                    {
                        fills.Add(FocusMark(row));
                    }

                    float arrowWidth = 0f;
                    if (row.Art != null)
                    {
                        var size = StripSize(row.Art, ArrowWidth, ArrowHeight);
                        arrowWidth = size.Width;
                        int frame = row.Enabled ? ComposedBoard.PlaqueFrame(row.Art.Frames, focused, pressed) : 0;
                        pictures.Add(new BoardPicture(
                            row.Art, row.X + row.Width - size.Width, row.Y + ((row.Height - size.Height) / 2f), frame));
                    }

                    lines.Add(new BoardLine(row.Label, row.X + 4f, row.Y + 2f, Math.Max(1f, row.Width - arrowWidth - 6f),
                        ItemFont, focused ? BoardInk.RowFocused : BoardInk.Row, index));
                    break;
                case OriginalRowKind.Slider:
                    ComposeSlider(row, focused, fills, pictures);
                    break;
                case OriginalRowKind.TextButton when row.Art != null:
                    plaques.Add(new BoardPlaque(
                        row.Art, row.X, row.Y, index,
                        row.Enabled ? ComposedBoard.PlaqueFrame(row.Art.Frames, focused, pressed) : 0,
                        row.Label, row.Enabled ? ComposedBoard.PlaqueInk(focused, pressed) : BoardInk.Detail));
                    break;
                case OriginalRowKind.Button when row.Art != null:
                    plaques.Add(new BoardPlaque(
                        row.Art, row.X, row.Y, index,
                        row.Enabled ? ComposedBoard.PlaqueFrame(row.Art.Frames, focused, pressed) : 0,
                        string.Empty, BoardInk.LabelNormal));
                    break;
            }
        }

        public OriginalRow PlaqueRow(string key, string label, int row, bool enabled, int column) =>
            OriginalTestHost.PlaqueRow(key, label, row, enabled, column);

        public void ComposePlainPage(
            string heading, IReadOnlyList<OriginalRow> rows, int focus,
            List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques) =>
            OriginalTestHost.ComposePlainPage(heading, rows, focus, lines);

        public BoardFill FocusMark(OriginalRow row) => OriginalTestHost.FocusMark(row);

        // The pointer put on one row, as a frame under the cursor leaves the shell: the row is
        // hovered, pressed and, where it is live, focused, since the frame a click fires on is the
        // one that still holds the row down.
        internal void PointAt(int index, OriginalRow row)
        {
            _hover = index;
            _pressed = index;
            _pointer = (row.X + 3f, row.Y + 3f);
            if (row.Enabled)
            {
                _focus[(int)Screen] = index;
            }
        }

        // A slider as the shell draws one: the slot, then the thumb at the value's own place on it,
        // with the wash and the outline under a focused row and rectangles where the art is missing.
        private void ComposeSlider(OriginalRow row, bool focused, List<BoardFill> fills, List<BoardPicture> pictures)
        {
            if (row.Slider is not { } slider)
            {
                return;
            }

            var track = slider.Track;
            if (focused)
            {
                fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 0, 0, 0, 0.10f));
                fills.Add(FocusMark(row));
            }

            float thumbX = track.ThumbX(slider.Value);
            if (slider.Slot != null && row.Art != null && Measure(slider.Slot.Name) != null && Measure(row.Art.Name) != null)
            {
                pictures.Add(new BoardPicture(slider.Slot, track.X, track.Y));
                pictures.Add(new BoardPicture(row.Art, thumbX, track.ThumbY));
                return;
            }

            fills.Add(new BoardFill(track.X, track.Y, track.Width, track.Height, 255, 255, 255, 0.6f, Border: true));
            fills.Add(new BoardFill(thumbX, track.ThumbY, track.ThumbWidth, track.ThumbHeight, 255, 255, 255, 0.6f));
        }

        private (float Width, float Height) StripSize(BoardArt art, float fallbackWidth, float fallbackHeight)
        {
            if (Measure(art.Name) is not { } size)
            {
                return (fallbackWidth, fallbackHeight);
            }

            return (size.Width, (float)Math.Floor(size.Height / (float)Math.Max(1, art.Frames)));
        }
    }
}
