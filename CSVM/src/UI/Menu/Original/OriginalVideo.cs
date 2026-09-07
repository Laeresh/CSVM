using System;
using System.Collections.Generic;
using System.Globalization;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// Original's VIDEO page, composed from the decoded <c>[@Video@]</c> rows: the page's background
/// and title, the display settings in the section's own row shape (a title in the title column, a
/// control beside it, a description in the description column) and ACCEPT CHANGES and CANCEL
/// CHANGES beside them. The settings are a table in authored row order, so a further one is an
/// entry plus the store field it reads and the authored row it stands on. Display Mode and V-Sync
/// are dropdowns on the authored Viewing Range and Effects Level rows; Enhanced Graphics takes the
/// checkbox row, Shadows, whose gate it owns. The decode and the readings are in
/// <c>docs/org/menu-inventory.md</c>.
/// </summary>
public sealed partial class OriginalShell
{
    /// <summary>The layout section the page is composed from.</summary>
    public const string VideoSection = "Video";

    /// <summary>The Preferences page's door onto it.</summary>
    public const string VideoDoorKey = "PF_B_VIDEO";

    /// <summary>ACCEPT CHANGES, which leaves as the options apply carrying every choice.</summary>
    public const string VideoAcceptKey = "VP_B_ACCEPTCHANGES";

    /// <summary>CANCEL CHANGES, back to Preferences with the choices dropped.</summary>
    public const string VideoCancelKey = "VP_B_CANCELCHANGES";

    // The page's authored row shape, used where a layout does not carry the section: the title
    // column and the Shadows row's line, the checkbox's offset from its title, the dropdown column
    // and box, and the description column with the width the plaque column leaves it.
    // docs/org/menu-inventory.md holds the decode these come from.
    private const float VideoTitleX = 18f;
    private const float VideoTitleWidth = 162f;
    private const float VideoShadowsY = 565f;
    private const float VideoCheckDx = 158f;
    private const float VideoCheckDy = -4f;
    private const float VideoDropX = 186f;
    private const float VideoDropWidth = 120f;
    private const float VideoItemHeight = 17f;
    private const float VideoDescX = 325f;
    private const float VideoDescWidth = 244f;
    private const float VideoPlaqueX = 569f;

    private const float VideoTitleFont = 14f;
    private const float VideoDescFont = 12f;

    private static readonly string[] GraphicsWords = { "FAITHFUL", "ENHANCED" };

    // The display-mode row's labels, one per DisplayWords.DisplayModes entry and in that order,
    // since the row reads and writes the store word by index. The two fullscreen modes read as one
    // word each because the authored box is 120 wide; which of them keeps the desktop alive beside
    // the game is what the row's description says.
    private static readonly string[] DisplayModeWords = { "Windowed", "Borderless", "Fullscreen" };

    // The V-Sync row's labels, one per DisplayWords.VSyncChoices entry and in that order, since the
    // row reads and writes the store word by index. A word that parses as a number is a cap in
    // frames per second with V-Sync off, which is why the caps read as rates rather than as bare
    // numbers on the page.
    private static readonly string[] VSyncWords = { "On", "Off", "60 FPS", "120 FPS", "144 FPS" };

    // The page's settings, in the authored order of the rows they stand on, since the cursor walks
    // the table and a form is read top to bottom. Each is a title, the authored row it stands on, a
    // description, a control and the words of the store field it reads and writes. A screen never
    // saves: the apply exit carries every choice and Launcher.ApplyOptions is the options file's
    // one writer.
    private static readonly VideoOption[] VideoOptions =
    {
        new(DisplayModeKey, "Display Mode", "VP_T_ViewTitle", "VP_D_View", "VP_T_ViewDESC",
            _ => "Select how the window sits on the screen. Borderless leaves the desktop beneath it.",
            OriginalRowKind.Dropdown, DisplayModeWords,
            s => WordIndex(CSVM.Utils.DisplayWords.DisplayModes, s._displayMode),
            (s, i) => s._displayMode = CSVM.Utils.DisplayWords.DisplayModes[i]),
        new(VSyncKey, "V-Sync", "VP_T_EffectsTitle", "VP_D_Effects", "VP_T_EffectsDESC",
            _ => "Select the frame pacing. On follows the screen; off runs free, or to a frame cap.",
            OriginalRowKind.Dropdown, VSyncWords,
            s => WordIndex(CSVM.Utils.DisplayWords.VSyncChoices, s._vsync),
            (s, i) => s._vsync = CSVM.Utils.DisplayWords.VSyncChoices[i]),
        new(GraphicsKey, "Enhanced Graphics", "VP_T_ShadowsTitle", "VP_B_SHADOWS", "VP_T_ShadowsDESC",
            s => s.GraphicsDescription(), OriginalRowKind.Radio, GraphicsWords,
            s => s._graphics == CSVM.Utils.GraphicsMode.EnhancedWord ? 1 : 0,
            (s, i) => s._graphics = i == 1 ? CSVM.Utils.GraphicsMode.EnhancedWord : CSVM.Utils.GraphicsMode.Default),
    };

    private string? _vpOpen;

    /// <summary>The VIDEO page's open option list's key, or null when none is open.</summary>
    public string? OpenVideoOption => _vpOpen;

    /// <summary>Opens the VIDEO page on the saved options with its first row focused, which is what
    /// the Preferences page's VIDEO door and the screenshot aid both go through. The page is a form,
    /// not a list, so it opens on its first setting rather than on wherever the cursor stood when it
    /// was last left.</summary>
    public void OpenVideo()
    {
        _vpOpen = null;
        _focus[(int)OriginalScreen.Video] = -1;
        Open(OriginalScreen.Video);
    }

    /// <summary>Opens the VIDEO page with one named row focused rather than its first, the
    /// screenshot aid's way onto a setting further down the page.</summary>
    public void OpenVideoOn(string key)
    {
        OpenVideo();
        FocusKey(key);
    }

    // Where a saved word sits among a row's own values, and every word row's default: a word the
    // vocabulary does not know, or none saved at all, shows as the first value, which each of these
    // vocabularies orders as the behaviour with no options file.
    private static int WordIndex(IReadOnlyList<string> words, string? word)
    {
        for (int i = 0; i < words.Count; i++)
        {
            if (words[i] == word)
            {
                return i;
            }
        }

        return 0;
    }

    // The graphics row's description says whether a restart is still owed. The mode is resolved
    // once at launch, so a choice that differs from the running one reaches the world on the next
    // start and nothing on the page can show it sooner; a player who saved it and came back
    // otherwise sees the box checked and a world unchanged, and reads that as a failed switch.
    private string GraphicsDescription()
    {
        bool running = CSVM.Utils.GraphicsMode.Enhanced;
        bool chosen = _graphics == CSVM.Utils.GraphicsMode.EnhancedWord;
        return chosen == running
            ? "Select the lit world. Takes effect on the next start."
            : $"Select the lit world. This run is {(running ? "enhanced" : "original")}; restart to apply.";
    }

    // The rows: an open list's items alone while one is open, else each setting's control on its
    // authored row and the two plaques beside them, all one column. Without the section the
    // controls stand as text buttons so the page is still walkable.
    private void BuildVideoRows(List<OriginalRow> rows)
    {
        var screen = _layout.Screen(VideoSection);
        if (screen == null)
        {
            for (int i = 0; i < VideoOptions.Length; i++)
            {
                var fallback = VideoOptions[i];
                rows.Add(TextButton(fallback.Key, fallback.Words[fallback.Read(this)], OptionsX, OptionsTop + (i * OptionsPitch), true, 0));
            }

            rows.Add(TextButton(VideoAcceptKey, "ACCEPT CHANGES", OptionsX, OptionsTop + (VideoOptions.Length * OptionsPitch), true, 0));
            rows.Add(TextButton(VideoCancelKey, "CANCEL CHANGES", OptionsX, OptionsTop + ((VideoOptions.Length + 1) * OptionsPitch), true, 0));
            return;
        }

        if (_vpOpen != null && VideoOptionFor(_vpOpen) is { } open)
        {
            var box = PlaceVideoRow(screen, open);
            for (int i = 0; i < open.Words.Count; i++)
            {
                rows.Add(new OriginalRow($"{_vpOpen}:{i}", open.Words[i], OriginalRowKind.ListRow,
                    box.BoxX, box.BoxY + (box.BoxHeight * (i + 1)), box.BoxWidth, box.BoxHeight, true, 0, null));
            }

            return;
        }

        BuildVideoControls(screen, rows);
    }

    private void BuildVideoControls(MenuLayoutScreen screen, List<OriginalRow> rows)
    {
        foreach (var option in VideoOptions)
        {
            var place = PlaceVideoRow(screen, option);
            rows.Add(new OriginalRow(option.Key,
                option.Kind == OriginalRowKind.Dropdown ? option.Words[option.Read(this)] : string.Empty,
                option.Kind, place.BoxX, place.BoxY, place.BoxWidth, place.BoxHeight, true, 0, place.Box));
        }

        AddStrip(screen, rows, VideoAcceptKey, OriginalRowKind.Button, true, 0);
        AddStrip(screen, rows, VideoCancelKey, OriginalRowKind.Button, true, 0);
    }

    // One row's shape off the section's own widgets, each number falling back to the authored one
    // when the row is not there. Two of them are derived rather than read: the title box stops at
    // the control beside it, since every Video title is authored the same 162 wide whatever stands
    // to its right, and a description the section gives no width wraps at the plaque column, the
    // two widthless rows being the two the plaques stand beside.
    private VideoPlacement PlaceVideoRow(MenuLayoutScreen screen, VideoOption option)
    {
        var title = screen.Widget(option.TitleKey);
        var control = screen.Widget(option.ControlKey);
        var description = screen.Widget(option.DescriptionKey);
        bool drop = option.Kind == OriginalRowKind.Dropdown;
        float titleX = title?.Int("X", (int)VideoTitleX) ?? VideoTitleX;
        float titleY = title?.Int("Y", (int)VideoShadowsY) ?? VideoShadowsY;
        var box = drop
            ? StripArt(control?.Art ?? Array.Empty<string>(), 4)
            : StripArt(control?.Art ?? Array.Empty<string>(), 0, control?.Frames ?? 8);
        float fallbackX = drop ? VideoDropX : titleX + VideoCheckDx;
        float fallbackY = drop ? titleY : titleY + VideoCheckDy;
        float boxX = control?.Int("X", (int)fallbackX) ?? fallbackX;
        float boxY = control?.Int("Y", (int)fallbackY) ?? fallbackY;
        float boxWidth;
        float boxHeight;
        if (drop)
        {
            boxWidth = control?.Int("Width", (int)VideoDropWidth) ?? VideoDropWidth;
            boxHeight = control?.Int("ItemHeight", (int)VideoItemHeight) ?? VideoItemHeight;
        }
        else
        {
            var size = StripSize(box, FallbackCheckSize, FallbackCheckSize);
            boxWidth = size.Width;
            boxHeight = size.Height;
        }

        float descX = description?.Int("X", (int)VideoDescX) ?? VideoDescX;
        float descY = description?.Int("Y", (int)titleY) ?? titleY;
        float plaqueX = screen.Widget(VideoAcceptKey)?.Int("X", (int)VideoPlaqueX) ?? VideoPlaqueX;
        int authoredDesc = description?.Int("Width") ?? 0;
        return new VideoPlacement(
            titleX,
            titleY,
            Math.Max(1f, Math.Min(title?.Int("Width", (int)VideoTitleWidth) ?? VideoTitleWidth, boxX - titleX)),
            boxX,
            boxY,
            boxWidth,
            boxHeight,
            descX,
            descY,
            authoredDesc > 0 ? authoredDesc : Math.Max(1f, plaqueX - descX),
            box);
    }

    private int IndexOfVideoOption(string key)
    {
        for (int i = 0; i < VideoOptions.Length; i++)
        {
            if (VideoOptions[i].Key == key)
            {
                return i;
            }
        }

        return -1;
    }

    private VideoOption? VideoOptionFor(string key)
    {
        int at = IndexOfVideoOption(key);
        return at >= 0 ? VideoOptions[at] : null;
    }

    // A press: a list item picks and closes, a dropdown opens its list, a checkbox flips, ACCEPT
    // CHANGES leaves as the apply exit carrying every saved choice (the settings this page does not
    // show ride it unchanged, read back when the page opened) and CANCEL CHANGES drops the edits
    // and goes back.
    private MenuExit? ActivateVideo(OriginalRow row)
    {
        int colon = row.Key.IndexOf(':');
        if (colon > 0 && VideoOptionFor(row.Key[..colon]) is { } picked)
        {
            picked.Write(this, int.Parse(row.Key[(colon + 1)..], CultureInfo.InvariantCulture));
            _vpOpen = null;
            FocusKey(picked.Key);
            return null;
        }

        switch (row.Key)
        {
            case VideoAcceptKey:
                return AppliedOptions();
            case VideoCancelKey:
                BackToPreferences();
                return null;
        }

        if (VideoOptionFor(row.Key) is not { } option)
        {
            return null;
        }

        if (option.Kind == OriginalRowKind.Dropdown && _layout.Screen(VideoSection) != null)
        {
            _vpOpen = option.Key;
            _focus[(int)_screen] = Math.Max(0, option.Read(this));
            return null;
        }

        option.Write(this, (option.Read(this) + 1) % option.Words.Count);
        return null;
    }

    // A sideways step on a focused setting picks the next value with wrap, as the Game Options
    // page's rows do. False on anything else, so the step crosses columns.
    private bool StepVideoValue(IReadOnlyList<OriginalRow> rows, int focus, int direction)
    {
        if (focus < 0 || focus >= rows.Count || VideoOptionFor(rows[focus].Key) is not { } option)
        {
            return false;
        }

        int count = option.Words.Count;
        option.Write(this, ((option.Read(this) + direction) % count + count) % count);
        FocusKey(option.Key);
        return true;
    }

    // Back from the page: an open list closes first, then the page leaves the way CANCEL CHANGES
    // does, since VP_B_CANCELCHANGES is the declining answer the layout gives the page.
    private void BackVideo()
    {
        if (_vpOpen == null)
        {
            BackToPreferences();
            return;
        }

        string key = _vpOpen;
        _vpOpen = null;
        FocusKey(key);
    }

    // The page as drawn: the Preferences page's logo (this section authors none and the original
    // keeps it standing), the page's background, its title, then each setting's title and
    // description at their authored columns, the controls over them, and an open list as the
    // overlay.
    private void ComposeVideo(
        IReadOnlyList<OriginalRow> rows, int focus, List<BoardPicture> pictures,
        List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques, List<BoardPanel> overlays)
    {
        if (_layout.Screen(PreferencesSection)?.Widget("PF_LOGO") is { Art.Count: > 0 } logo)
        {
            pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, logo.Art[0], Math.Max(1, logo.Frames)),
                logo.Int("X"), logo.Int("Y")));
        }

        var screen = _layout.Screen(VideoSection);
        if (screen == null)
        {
            lines.Add(new BoardLine("VIDEO", OptionsX, OptionsTop - 44f, 0f, HeadingFont, BoardInk.Heading));
            ComposeRows(rows, focus, fills, lines, plaques);
            return;
        }

        if (screen.Widget("VP_BACKGROUND") is { Art.Count: > 0 } background)
        {
            pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, background.Art[0], Math.Max(1, background.Frames)),
                background.Int("X"), background.Int("Y")));
        }

        if (screen.Widget("VP_T_TITLE") is { } title)
        {
            lines.Add(new BoardLine(title.Text ?? "VIDEO", title.Int("X"), title.Int("Y"), title.Int("Width"),
                PreferencesTitleFont, BoardInk.Heading, -1, false,
                title.Int("Justify") == 1 ? BoardJustify.Center : BoardJustify.Left));
        }

        foreach (var option in VideoOptions)
        {
            var place = PlaceVideoRow(screen, option);
            lines.Add(new BoardLine(option.Title, place.TitleX, place.TitleY, place.TitleWidth,
                VideoTitleFont, BoardInk.Row));
            lines.Add(new BoardLine(option.Description(this), place.DescX, place.DescY, place.DescWidth,
                VideoDescFont, BoardInk.Row));
        }

        ComposeVideoControls(screen, rows, focus, fills, lines, plaques, pictures, overlays);
    }

    // The controls in their states. With a list open the page under it is drawn from the closed
    // controls with the open one focused, and the items become the overlay, as the Game Options
    // page's own list does.
    private void ComposeVideoControls(
        MenuLayoutScreen screen, IReadOnlyList<OriginalRow> rows, int focus, List<BoardFill> fills,
        List<BoardLine> lines, List<BoardPlaque> plaques, List<BoardPicture> pictures, List<BoardPanel> overlays)
    {
        var controls = rows;
        int controlFocus = focus;
        int controlPressed = _pressed;
        if (_vpOpen != null)
        {
            var closed = new List<OriginalRow>();
            BuildVideoControls(screen, closed);
            controls = closed;
            controlFocus = IndexOfVideoOption(_vpOpen);
            controlPressed = -1;
        }

        for (int i = 0; i < controls.Count; i++)
        {
            var row = controls[i];
            if (row.Kind == OriginalRowKind.Radio && row.Art != null)
            {
                // An eight-state strip: the four button states unchecked, then the same four checked.
                int state = row.Enabled ? (i == controlPressed ? 3 : i == controlFocus ? 2 : 1) : 0;
                int frame = (VideoOptionFor(row.Key)?.Read(this) == 1 ? 4 : 0) + state;
                plaques.Add(new BoardPlaque(row.Art, row.X, row.Y, i, frame, string.Empty, BoardInk.LabelNormal));
                continue;
            }

            ComposeInstantActionRow(row, i == controlFocus, i == controlPressed, i, fills, lines, plaques, pictures);
        }

        if (_vpOpen != null && rows.Count > 0)
        {
            overlays.Add(ComposeOptionList(rows, focus));
        }
    }

    // One setting of the page: its title, the authored widgets it composes over (the title, the
    // control and the description), its description (read off the shell, since a row can say
    // something about its saved state), the control it takes, the words of the store field it
    // shows, and how that field is read and written.
    private sealed record VideoOption(
        string Key, string Title, string TitleKey, string ControlKey, string DescriptionKey,
        Func<OriginalShell, string> Description, OriginalRowKind Kind, IReadOnlyList<string> Words,
        Func<OriginalShell, int> Read, Action<OriginalShell, int> Write);

    // One row's place in authored pixels: the title box, the control's own rectangle and strip, and
    // the description box.
    private sealed record VideoPlacement(
        float TitleX, float TitleY, float TitleWidth,
        float BoxX, float BoxY, float BoxWidth, float BoxHeight,
        float DescX, float DescY, float DescWidth, BoardArt? Box);
}
