using System;
using System.Collections.Generic;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// Original's AUDIO page, composed from the decoded <c>[@Audio@]</c> rows: the page's background
/// and title, four volume levels in the section's own row shape (a title in the title column, a
/// slider beside it, a description in the description column) and ACCEPT CHANGES and CANCEL
/// CHANGES under them. The levels are a table in authored row order, so a further one is an entry
/// plus the store field it reads. Master takes the In-Game Music checkbox's row and that row's
/// title and description are the page's own, since a slider that reaches zero is that checkbox in
/// one fewer widget; the three authored volume rows keep their words. Sound Quality is left out,
/// its authored row tiering a mixer this port has no answer to. While the page is open it states
/// the mix it stands at and which level a frame moved, and the host is what applies and sounds
/// them. The decode and the readings are in <c>docs/org/menu-inventory.md</c>.
/// </summary>
public sealed partial class OriginalShell
{
    /// <summary>The layout section the page is composed from.</summary>
    public const string AudioSection = "Audio";

    /// <summary>The Preferences page's door onto it.</summary>
    public const string AudioDoorKey = "PF_B_AUDIO";

    /// <summary>ACCEPT CHANGES, which leaves as the options apply carrying every choice.</summary>
    public const string AudioAcceptKey = "AP_B_ACCEPTCHANGES";

    /// <summary>CANCEL CHANGES, back to Preferences with the levels dropped.</summary>
    public const string AudioCancelKey = "AP_B_CANCELCHANGES";

    // The page's authored row shape, used where a layout does not carry the section or one of its
    // rows: the title column and its box, the slider column and the distance from a title's line
    // down to its own slider's, and the description column and its width. Each row's own line is
    // on its table entry instead, the authored pitch being 58, 57, 53 and 53 rather than one
    // number. docs/org/menu-inventory.md holds the decode these come from.
    private const float AudioTitleX = 137f;
    private const float AudioTitleWidth = 170f;
    private const float AudioSliderX = 137f;
    private const float AudioSliderOffset = 26f;
    private const float AudioDescX = 348f;
    private const float AudioDescWidth = 310f;

    private const float AudioTitleFont = 14f;
    private const float AudioDescFont = 12f;

    // The aid's four levels, one per row and all distinct, so a single shot shows the thumb at
    // four places on the track rather than at the shipped default three times over.
    private const int AudioPoseMaster = 100;
    private const int AudioPoseMusic = 25;
    private const int AudioPoseEffects = 60;
    private const int AudioPoseVoice = 85;

    // The page's levels, in the authored order of the rows they stand on, since the cursor walks
    // the table and a form is read top to bottom. Each is a title, the authored title, control and
    // description widgets it stands on, a description, the authored lines those two texts fall back
    // to, and how the store field is read and written. A level is never a vocabulary word, so a row
    // reads a never-set field as the shipped default rather than as a first word. A screen never
    // saves: the apply exit carries every choice and Launcher.ApplyOptions is the one writer.
    private static readonly AudioOption[] AudioOptions =
    {
        new(AudioMasterKey, MenuMixLevel.Master, "Master", "AP_T_MusicTitle", null, "AP_T_MusicDesc",
            "Set the overall volume of all sounds.", 266f, 278f,
            s => s._audioMaster ?? CSVM.Utils.AudioMix.DefaultMaster,
            (s, v) => s._audioMaster = v),
        new(AudioMusicKey, MenuMixLevel.Music, "Music Volume", "AP_T_MVolTitle", "AP_S_MVOLUME", "AP_T_MVolDesc",
            "Set the volume of the in-game music.", 324f, 332f,
            s => s._audioMusic ?? CSVM.Utils.AudioMix.DefaultMusic,
            (s, v) => s._audioMusic = v),
        new(AudioEffectsKey, MenuMixLevel.Effects, "Effects Volume", "AP_T_EVolTitle", "AP_S_EVOLUME", "AP_T_EVolDesc",
            "Set the volume of the sound effects.", 381f, 387f,
            s => s._audioEffects ?? CSVM.Utils.AudioMix.DefaultEffects,
            (s, v) => s._audioEffects = v),
        new(AudioVoiceKey, MenuMixLevel.Voice, "Voice Volume", "AP_T_VVolTitle", "AP_S_VVOLUME", "AP_T_VVolDesc",
            "Set the volume of the voices.", 434f, 442f,
            s => s._audioVoice ?? CSVM.Utils.AudioMix.DefaultVoice,
            (s, v) => s._audioVoice = v),
    };

    // The first authored slider row, which is where the page's slider geometry is read from. The
    // Master row has no authored slider of its own: its authored row carries the In-Game Music
    // checkbox, which stands at another column and on the title's own line, so a Master slider
    // placed at that widget's corner would sit 122 pixels right of the three below it.
    private static readonly AudioOption AudioSliderRow = AudioOptions[1];

    // Which level a row's slider moved, cleared by the host's read of it. The control writes only
    // where the value actually changed, so this stands at None through every frame of a drag that
    // held the thumb still, which is what keeps a preview off a pointer's frame rate.
    private MenuMixLevel _audioMoved;

    /// <summary>The mix the AUDIO page stands at while it is open, for the host to apply so a level
    /// can be judged by ear as it moves, and null on every other screen, which is what makes leaving
    /// this page by any door drop the preview. The shell states four levels and nothing more: which
    /// bus each reaches, and whether a preview sounds at all, are the audio service's.</summary>
    public CSVM.Utils.AudioLevels? AudioPreviewMix =>
        _screen == OriginalScreen.Audio
            ? new CSVM.Utils.AudioLevels(
                AudioLevel(MenuMixLevel.Master), AudioLevel(MenuMixLevel.Music),
                AudioLevel(MenuMixLevel.Effects), AudioLevel(MenuMixLevel.Voice))
            : null;

    /// <summary>Which level has moved since this was last asked, and <see cref="MenuMixLevel.None"/>
    /// when none has. ⚠ Taken rather than read: the host sounds the moved category, so a caller that
    /// saw one move twice would sound it twice.</summary>
    public MenuMixLevel TakeAudioMoved()
    {
        var moved = _audioMoved;
        _audioMoved = MenuMixLevel.None;
        return moved;
    }

    /// <summary>Opens the AUDIO page on the saved mix with its first row focused, which is what the
    /// Preferences page's AUDIO door and the screenshot aid both go through. The page is a form,
    /// not a list, so it opens on Master rather than on wherever the cursor stood when it was last
    /// left.</summary>
    public void OpenAudio()
    {
        _focus[(int)OriginalScreen.Audio] = -1;
        Open(OriginalScreen.Audio);
    }

    /// <summary>Stands the four rows at four distinct levels, the screenshot aid's pose for a page
    /// whose four thumbs otherwise sit at two places. Nothing happens off the AUDIO page, and
    /// nothing is saved: the pose is an edit the aid's shot is taken of.</summary>
    public void PoseAudioMix()
    {
        if (_screen != OriginalScreen.Audio)
        {
            return;
        }

        _audioMaster = AudioPoseMaster;
        _audioMusic = AudioPoseMusic;
        _audioEffects = AudioPoseEffects;
        _audioVoice = AudioPoseVoice;
    }

    // The rows: each level's slider on its authored line and the two plaques beside them, all one
    // column. Without the section the levels stand as text buttons carrying their own value, so the
    // page is still walkable and still says what the mix is; the pageless composer draws no slider,
    // which is why the fallback is not one.
    private void BuildAudioRows(List<OriginalRow> rows)
    {
        var screen = _layout.Screen(AudioSection);
        if (screen == null)
        {
            for (int i = 0; i < AudioOptions.Length; i++)
            {
                var fallback = AudioOptions[i];
                rows.Add(TextButton(fallback.Key, $"{fallback.Title} {fallback.Read(this)}",
                    OptionsX, OptionsTop + (i * OptionsPitch), true, 0));
            }

            rows.Add(TextButton(AudioAcceptKey, "ACCEPT CHANGES", OptionsX, OptionsTop + (AudioOptions.Length * OptionsPitch), true, 0));
            rows.Add(TextButton(AudioCancelKey, "CANCEL CHANGES", OptionsX, OptionsTop + ((AudioOptions.Length + 1) * OptionsPitch), true, 0));
            return;
        }

        BuildAudioControls(screen, rows);
    }

    private void BuildAudioControls(MenuLayoutScreen screen, List<OriginalRow> rows)
    {
        foreach (var option in AudioOptions)
        {
            var place = PlaceAudioRow(screen, option);
            var control = option.ControlKey is { } key ? screen.Widget(key) : null;
            // The moved level is recorded here rather than in the table's own write, because the
            // control calls this only where the value actually changed: a drag that held the thumb
            // on the same whole number records nothing, and the host sounds nothing for it.
            rows.Add(SliderRow(control, option.Key, place.SliderX, place.SliderY,
                CSVM.Utils.AudioMix.MinLevel, CSVM.Utils.AudioMix.MaxLevel,
                option.Read(this),
                v =>
                {
                    option.Write(this, v);
                    _audioMoved = option.Level;
                }));
        }

        AddStrip(screen, rows, AudioAcceptKey, OriginalRowKind.Button, true, 0);
        AddStrip(screen, rows, AudioCancelKey, OriginalRowKind.Button, true, 0);
    }

    // The level a row stands at, found through its own table entry so a level and its shipped
    // fallback keep one definition. Every member but None is in the table, so the last answer is
    // unreachable; it is the resting level rather than silence, a preview being no place to invent
    // a mute.
    private int AudioLevel(MenuMixLevel level)
    {
        foreach (var option in AudioOptions)
        {
            if (option.Level == level)
            {
                return option.Read(this);
            }
        }

        return CSVM.Utils.AudioMix.MaxLevel;
    }

    // One row's shape off the section's own widgets, each number falling back to the authored one
    // when the row is not there. The lines are read per row rather than off a first row and a
    // pitch: this section's pitch is 58, 57, 53 and 53, so a single pitch would misplace the rows
    // under the second by up to five pixels each.
    private AudioPlacement PlaceAudioRow(MenuLayoutScreen screen, AudioOption option)
    {
        var title = screen.Widget(option.TitleKey);
        var description = screen.Widget(option.DescriptionKey);
        float titleY = title?.Int("Y", (int)option.TitleY) ?? option.TitleY;
        return new AudioPlacement(
            title?.Int("X", (int)AudioTitleX) ?? AudioTitleX,
            titleY,
            title?.Int("Width", (int)AudioTitleWidth) ?? AudioTitleWidth,
            SliderColumnX(screen),
            titleY + SliderOffset(screen),
            description?.Int("X", (int)AudioDescX) ?? AudioDescX,
            description?.Int("Y", (int)option.DescY) ?? option.DescY,
            description?.Int("Width", (int)AudioDescWidth) ?? AudioDescWidth);
    }

    // The column every slider stands in, and the drop from a title's line to its own slider's, both
    // read off the first authored slider row. They place the Master row, whose own authored widget
    // is the checkbox; a row with a slider of its own is placed by that widget and reaches these
    // only when the layout has dropped it.
    private float SliderColumnX(MenuLayoutScreen screen) =>
        screen.Widget(AudioSliderRow.ControlKey!)?.Int("X", (int)AudioSliderX) ?? AudioSliderX;

    private float SliderOffset(MenuLayoutScreen screen)
    {
        var title = screen.Widget(AudioSliderRow.TitleKey);
        var slider = screen.Widget(AudioSliderRow.ControlKey!);
        return title != null && slider != null ? slider.Int("Y") - title.Int("Y") : AudioSliderOffset;
    }

    // A press: ACCEPT CHANGES leaves as the apply exit carrying every saved choice (the settings
    // this page does not show ride it unchanged, read back when the page opened) and CANCEL CHANGES
    // drops the edits and goes back. A slider row answers nothing: its value moves under the
    // pointer or by a sideways step, and an Accept that also moved it would have no opposite.
    private MenuExit? ActivateAudio(OriginalRow row)
    {
        switch (row.Key)
        {
            case AudioAcceptKey:
                return AppliedOptions();
            case AudioCancelKey:
                BackToPreferences();
                return null;
        }

        return null;
    }

    // The page as drawn: the Preferences page's logo (this section authors none and the original
    // keeps it standing), the page's background, its title, then each level's title and description
    // at their authored columns and the sliders and plaques over them.
    // ⚠ The logo and the plate are backdrop, not pictures. A board draws every fill between the two
    // layers, so a plate in the picture layer paints over each row's own focus mark: the page then
    // shows no focused row at all, whatever the mark is, because the mark is under opaque art.
    private void ComposeAudio(
        IReadOnlyList<OriginalRow> rows, int focus, List<BoardPicture> backdrop, List<BoardPicture> pictures,
        List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques)
    {
        if (_layout.Screen(PreferencesSection)?.Widget("PF_LOGO") is { Art.Count: > 0 } logo)
        {
            backdrop.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, logo.Art[0], Math.Max(1, logo.Frames)),
                logo.Int("X"), logo.Int("Y")));
        }

        var screen = _layout.Screen(AudioSection);
        if (screen == null)
        {
            lines.Add(new BoardLine("AUDIO", OptionsX, OptionsTop - 44f, 0f, HeadingFont, BoardInk.Heading));
            ComposeRows(rows, focus, fills, lines, plaques);
            return;
        }

        if (screen.Widget("AP_BACKGROUND") is { Art.Count: > 0 } background)
        {
            backdrop.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, background.Art[0], Math.Max(1, background.Frames)),
                background.Int("X"), background.Int("Y")));
        }

        if (screen.Widget("AP_T_TITLE") is { } title)
        {
            lines.Add(new BoardLine(title.Text ?? "AUDIO", title.Int("X"), title.Int("Y"), title.Int("Width"),
                PreferencesTitleFont, BoardInk.Heading, -1, false,
                title.Int("Justify") == 1 ? BoardJustify.Center : BoardJustify.Left));
        }

        // The focused row's title in the focused ink, the mark a list row takes, so the cursor shows
        // where the eye already reads the row's name. The rows are built from this table in this
        // order, so a level's index is its row's.
        for (int i = 0; i < AudioOptions.Length; i++)
        {
            var option = AudioOptions[i];
            var place = PlaceAudioRow(screen, option);
            lines.Add(new BoardLine(option.Title, place.TitleX, place.TitleY, place.TitleWidth,
                AudioTitleFont, i == focus ? BoardInk.RowFocused : BoardInk.Row));
            lines.Add(new BoardLine(option.Description, place.DescX, place.DescY, place.DescWidth,
                AudioDescFont, BoardInk.Row));
        }

        for (int i = 0; i < rows.Count; i++)
        {
            ComposePlateRow(rows[i], i == focus, i == _pressed, i, fills, lines, plaques, pictures);
        }
    }

    // One level of the page: its key, which of the mix's levels it is (for the host that hears one
    // move), its title, the authored widgets it composes over (the title, the slider and the
    // description, the slider null on the row that has none), its description, the authored lines
    // the title and the description fall back to, and how the store field is read and written. The
    // description is a fixed string: unlike the VIDEO page's graphics row, no level says anything
    // about its saved state that the thumb does not already show.
    private sealed record AudioOption(
        string Key, MenuMixLevel Level, string Title, string TitleKey, string? ControlKey,
        string DescriptionKey, string Description, float TitleY, float DescY,
        Func<OriginalShell, int> Read, Action<OriginalShell, int> Write);

    // One row's place in authored pixels: the title box, the corner its slider stands at where the
    // row authors no slider of its own, and the description box.
    private sealed record AudioPlacement(
        float TitleX, float TitleY, float TitleWidth,
        float SliderX, float SliderY,
        float DescX, float DescY, float DescWidth);
}
