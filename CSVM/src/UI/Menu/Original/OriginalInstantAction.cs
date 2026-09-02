using System;
using System.Collections.Generic;
using System.Globalization;

namespace CSVM.UI.Menu.Original;

/// <summary>The colours the Instant Action screen writes in, read off its own rows: the text
/// rows' authored colour and the paper buttons' label tail.</summary>
public sealed record OriginalInstantActionInks(
    MenuLayoutColor Text, MenuLayoutColor LabelNormal, MenuLayoutColor LabelRollover, MenuLayoutColor LabelDepressed);

/// <summary>
/// The Original Instant Action screen, composed from the decoded <c>[@InstantAction@]</c> rows over
/// the shared <see cref="InstantActionFeature"/>: the Table of Contents window with its scroll
/// arrows, the dropdowns at their authored lines, the enemy rows paged by the up and down buttons
/// (the first wave beside the pilot fields, waves two to four on the second page), the radio pair
/// and the five buttons. Decoded rules bound here: a contents row applies its preset, View Story
/// writes the preset's name as the story title, the ace duel hides every enemy control, the
/// wingman plane hides at zero wingmen, a changed militia resets its aircraft, stunt flying bars
/// the clouds. Remake-only until the screen is filmed: the open list drawn under its box, a
/// sideways step changing a value, both halves shown together, Build and Weapon Loadout disabled.
/// </summary>
public sealed partial class OriginalShell
{
    /// <summary>The layout section the screen is composed from.</summary>
    public const string InstantActionSection = "InstantAction";

    /// <summary>The Table of Contents list; its rows are keyed <c>IA_TL_Contents:&lt;index&gt;</c>.</summary>
    public const string ContentsKey = "IA_TL_Contents";

    /// <summary>The contents list's scroll-up arrow.</summary>
    public const string ContentsUpKey = "IA_TL_Contents:up";

    /// <summary>The contents list's scroll-down arrow.</summary>
    public const string ContentsDownKey = "IA_TL_Contents:down";

    /// <summary>The View Story button.</summary>
    public const string ViewStoryKey = "IA_B_VIEW";

    /// <summary>The Fly Mission button.</summary>
    public const string FlyMissionKey = "IA_B_FLY";

    /// <summary>The Weapon Loadout button.</summary>
    public const string WeaponLoadoutKey = "IA_B_CHANGEWEAPONS";

    /// <summary>The Exit button, back to the main menu.</summary>
    public const string ExitKey = "IA_B_Exit";

    /// <summary>The Build Custom Plane button.</summary>
    public const string BuildKey = "IA_B_BUILD";

    /// <summary>The radio pair: whose loadout the Weapon Loadout button edits.</summary>
    public const string PlayerRadioKey = "IA_B_PLAYER";

    /// <summary>The wingman half of the radio pair.</summary>
    public const string WingmanRadioKey = "IA_B_WINGMAN";

    /// <summary>The enemy rows' page-up button.</summary>
    public const string PageUpKey = "IA_B_UP";

    /// <summary>The enemy rows' page-down button.</summary>
    public const string PageDownKey = "IA_B_DOWN";

    /// <summary>The player's plane dropdown.</summary>
    public const string PlayerPlaneKey = "IA_D_PLANEP";

    /// <summary>The wingman count dropdown.</summary>
    public const string WingmenKey = "IA_D_NWING";

    /// <summary>The wingman plane dropdown, hidden at zero wingmen.</summary>
    public const string WingmanPlaneKey = "IA_D_PLANEW";

    /// <summary>The mission type dropdown.</summary>
    public const string MissionKey = "IA_D_MISSTYPE";

    /// <summary>The environment dropdown.</summary>
    public const string EnvironmentKey = "IA_D_ENVIRONMENT";

    // Text sizes against the authored 18-pixel item height and the text rows' own boxes.
    private const float ItemFont = 13f;
    private const float TitleFont = 20f;
    private const float LabelFont = 14f;
    private const float InstructionFont = 12f;
    private const float StoryFont = 18f;

    // A widget's size when the layout row is missing or its art cannot be measured.
    private const float FallbackItemHeight = 18f;
    private const float FallbackDropWidth = 160f;
    private const float FallbackArrowWidth = 15f;
    private const float FallbackArrowHeight = 14f;

    private static readonly string[] WaveKeyPrefixes = { "IA_D_NENEMY", "IA_D_EGROUP", "IA_D_DIFFICULTY", "IA_D_PLANEE" };

    private readonly InstantActionFeature _instantAction;
    private int _iaPage;
    private string? _iaOpen;
    private int _iaContentsTop;
    private int _iaRadio;
    private string _iaStoryTitle = string.Empty;

    /// <summary>The colours the Instant Action screen writes in.</summary>
    public OriginalInstantActionInks InstantActionInks { get; }

    /// <summary>The open dropdown's key, or null when none is open.</summary>
    public string? OpenDropdown => _iaOpen;

    /// <summary>Which enemy page shows: 0 the pilot fields with the first wave, 1 waves two to four.</summary>
    public int EnemyPage => _iaPage;

    /// <summary>The contents window's first visible preset.</summary>
    public int ContentsTop => _iaContentsTop;

    /// <summary>Whose loadout the Weapon Loadout button targets: 0 the pilot, 1 the wingmen.</summary>
    public int LoadoutTarget => _iaRadio;

    /// <summary>The story title View Story last wrote, or "".</summary>
    public string StoryTitle => _iaStoryTitle;

    /// <summary>Opens the Instant Action screen: the environment is confirmed so the launch's base
    /// def is the environment's own, and no list is open.</summary>
    public void OpenInstantAction()
    {
        _instantAction.ConfirmEnvironment();
        if (_instantAction.IsAceDuel)
        {
            _iaPage = 0;
        }

        _iaOpen = null;
        Open(OriginalScreen.InstantAction);
    }

    private static OriginalInstantActionInks ReadInstantActionInks(MenuLayout layout)
    {
        var screen = layout.Screen(InstantActionSection);
        var black = new MenuLayoutColor(255, 0, 0, 0);
        var white = new MenuLayoutColor(255, 255, 255, 255);
        var title = screen?.Widget("IA_T_TABLETITLE");
        var fly = screen?.Widget(FlyMissionKey);
        return new OriginalInstantActionInks(
            title != null && title.TryColor("Color", out var text) ? text : black,
            fly != null && fly.TryColor("ColorActive", out var n) ? n : black,
            fly != null && fly.TryColor("ColorRollover", out var r) ? r : black,
            fly != null && fly.TryColor("ColorDepressed", out var d) ? d : white);
    }

    private static bool IsWhite(MenuLayoutWidget widget) =>
        widget.TryColor("Color", out var c) && c.R == 255 && c.G == 255 && c.B == 255;

    private static BoardJustify Justify(MenuLayoutWidget widget) => widget.Int("Justify") switch
    {
        1 => BoardJustify.Center,
        2 => BoardJustify.Right,
        _ => BoardJustify.Left,
    };

    private static string Capitalise(string s) =>
        s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    // A dropdown's box: its authored corner and width, one item high.
    private static (float X, float Y, float Width, float Height) DropBox(MenuLayoutWidget widget) =>
        (widget.Int("X"), widget.Int("Y"), widget.Int("Width", (int)FallbackDropWidth), widget.Int("ItemHeight", (int)FallbackItemHeight));

    // The n-th art a row names as a strip; arrows and radios carry their frame count in the row,
    // the dropdown arrows are the same four-frame strips the page buttons draw.
    private static BoardArt? StripArt(IReadOnlyList<string> art, int index, int frames = 4) =>
        index < art.Count && art[index].Length > 0 ? new BoardArt(BoardArtLibrary.Ui, art[index], Math.Max(1, frames)) : null;

    private static IReadOnlyList<string> Names<T>(IReadOnlyList<T> rows, Func<T, string> name)
    {
        var names = new string[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            names[i] = name(rows[i]);
        }

        return names;
    }

    private static IReadOnlyList<string> Counts(int max)
    {
        var names = new string[max + 1];
        for (int i = 0; i <= max; i++)
        {
            names[i] = i.ToString(CultureInfo.InvariantCulture);
        }

        return names;
    }

    private static int ContentsIndex(string key)
    {
        if (!key.StartsWith(ContentsKey + ":", StringComparison.Ordinal))
        {
            return -1;
        }

        return int.TryParse(key[(ContentsKey.Length + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : -1;
    }

    // The rows: an open list's items alone while one is open, else the screen's widgets.
    private void BuildInstantActionRows(List<OriginalRow> rows)
    {
        var screen = _layout.Screen(InstantActionSection);
        if (screen == null)
        {
            return;
        }

        if (_iaOpen != null && screen.Widget(_iaOpen) is { } open && DropdownFor(_iaOpen) is { } list)
        {
            var box = DropBox(open);
            for (int i = 0; i < list.Items.Count; i++)
            {
                rows.Add(new OriginalRow($"{_iaOpen}:{i}", list.Items[i], OriginalRowKind.ListRow,
                    box.X, box.Y + (box.Height * (i + 1)), box.Width, box.Height, list.Allowed(i), 0, null));
            }

            return;
        }

        BuildInstantActionWidgets(screen, rows);
    }

    // The screen's widgets in focus order: the left page (the contents window, its arrows, View
    // Story, Build) as column 0, the right page (the dropdowns of the shown enemy page, the paging
    // button, the radio pair, Weapon Loadout, Fly Mission, Exit) as column 1.
    private void BuildInstantActionWidgets(MenuLayoutScreen screen, List<OriginalRow> rows)
    {
        var presets = InstantActionFeature.Presets;
        if (screen.Widget(ContentsKey) is { } list)
        {
            int window = Math.Max(1, list.Int("TotalDisplayed", 14));
            float itemHeight = list.Int("ItemHeight", (int)FallbackItemHeight);
            float x = list.Int("X");
            float y = list.Int("Y");
            float width = list.Int("Width", 277);
            int top = ContentsTopClamped(window);
            for (int i = top; i < presets.Count && i < top + window; i++)
            {
                rows.Add(new OriginalRow($"{ContentsKey}:{i}", presets[i].Name, OriginalRowKind.ListRow,
                    x, y + ((i - top) * itemHeight), width, itemHeight, true, 0, null));
            }

            // The list's own arrows stand on its right edge, the up arrow at the top of the
            // window and the down arrow at its foot; each is live only while there is more list
            // that way.
            var up = StripArt(list.Art, 1);
            var down = StripArt(list.Art, 2);
            var upSize = StripSize(up, FallbackArrowWidth, FallbackArrowHeight);
            var downSize = StripSize(down, FallbackArrowWidth, FallbackArrowHeight);
            rows.Add(new OriginalRow(ContentsUpKey, string.Empty, OriginalRowKind.Button,
                x + width, y, upSize.Width, upSize.Height, top > 0, 0, up));
            rows.Add(new OriginalRow(ContentsDownKey, string.Empty, OriginalRowKind.Button,
                x + width, y + (window * itemHeight) - downSize.Height, downSize.Width, downSize.Height,
                top + window < presets.Count, 0, down));
        }

        AddStrip(screen, rows, ViewStoryKey, OriginalRowKind.TextButton, true, 0);
        AddStrip(screen, rows, BuildKey, OriginalRowKind.Button, false, 0);

        bool ace = _instantAction.IsAceDuel;
        if (_iaPage == 0)
        {
            AddDropdown(screen, rows, PlayerPlaneKey);
            AddDropdown(screen, rows, WingmenKey);
            if (_instantAction.NumWingmen > 0)
            {
                AddDropdown(screen, rows, WingmanPlaneKey);
            }

            AddDropdown(screen, rows, MissionKey);
            AddDropdown(screen, rows, EnvironmentKey);
            if (!ace)
            {
                AddWave(screen, rows, 0);
                AddStrip(screen, rows, PageDownKey, OriginalRowKind.Button, true, 1);
            }
        }
        else
        {
            if (!ace)
            {
                AddWave(screen, rows, 1);
                AddWave(screen, rows, 2);
                AddWave(screen, rows, 3);
            }

            AddStrip(screen, rows, PageUpKey, OriginalRowKind.Button, true, 1);
        }

        AddStrip(screen, rows, PlayerRadioKey, OriginalRowKind.Radio, true, 1);
        AddStrip(screen, rows, WingmanRadioKey, OriginalRowKind.Radio, true, 1);
        AddStrip(screen, rows, WeaponLoadoutKey, OriginalRowKind.TextButton, false, 1);
        AddStrip(screen, rows, FlyMissionKey, OriginalRowKind.TextButton, true, 1);
        AddStrip(screen, rows, ExitKey, OriginalRowKind.Button, true, 1);
    }

    private void AddWave(MenuLayoutScreen screen, List<OriginalRow> rows, int wave)
    {
        foreach (string prefix in WaveKeyPrefixes)
        {
            AddDropdown(screen, rows, prefix + wave.ToString(CultureInfo.InvariantCulture));
        }
    }

    private void AddDropdown(MenuLayoutScreen screen, List<OriginalRow> rows, string key)
    {
        if (screen.Widget(key) is not { } widget || DropdownFor(key) is not { } list)
        {
            return;
        }

        var box = DropBox(widget);
        string value = list.Current >= 0 && list.Current < list.Items.Count ? list.Items[list.Current] : string.Empty;
        rows.Add(new OriginalRow(key, value, OriginalRowKind.Dropdown, box.X, box.Y, box.Width, box.Height,
            true, 1, StripArt(widget.Art, 4)));
    }

    private void AddStrip(MenuLayoutScreen screen, List<OriginalRow> rows, string key, OriginalRowKind kind, bool enabled, int column)
    {
        if (screen.Widget(key) is not { } widget)
        {
            return;
        }

        var art = StripArt(widget.Art, 0, widget.Frames);
        var size = StripSize(art, FallbackButtonWidth, FallbackButtonHeight);
        rows.Add(new OriginalRow(key, widget.Text ?? string.Empty, kind, widget.Int("X"), widget.Int("Y"),
            size.Width, size.Height, enabled, column, art));
    }

    // A strip's one-frame size from the measurer, or the fallback when the file is not there.
    private (float Width, float Height) StripSize(BoardArt? art, float fallbackWidth, float fallbackHeight)
    {
        if (art == null || Measure(art.Name) is not { } size)
        {
            return (fallbackWidth, fallbackHeight);
        }

        return (size.Width, (float)Math.Floor(size.Height / (float)Math.Max(1, art.Frames)));
    }

    private int ContentsTopClamped(int window)
    {
        _iaContentsTop = Math.Clamp(_iaContentsTop, 0, Math.Max(0, InstantActionFeature.Presets.Count - window));
        return _iaContentsTop;
    }

    private DropdownList? DropdownFor(string key)
    {
        var ia = _instantAction;
        switch (key)
        {
            case PlayerPlaneKey:
                return new DropdownList(Names(InstantActionFeature.Airframes, a => a.Name), ia.PlayerPlaneIndex, _ => true, ia.SelectPlayerPlane);
            case WingmenKey:
                return new DropdownList(Counts(InstantActionFeature.MaxWingmen), ia.NumWingmen, _ => true, ia.SetWingmen);
            case WingmanPlaneKey:
                return new DropdownList(Names(InstantActionFeature.Airframes, a => a.Name), ia.WingmanPlaneIndex, _ => true, ia.SelectWingmanPlane);
            case MissionKey:
                return new DropdownList(Names(ia.MissionTypes, m => m.Label), ia.MissionTypeIndex, _ => true, i =>
                {
                    ia.SelectMissionType(i);
                    if (ia.IsAceDuel)
                    {
                        // The ace duel hides every enemy control, so the enemy page has nothing to show.
                        _iaPage = 0;
                    }
                });
            case EnvironmentKey:
                return new DropdownList(Names(InstantActionFeature.Environments, e => e.Name), ia.EnvironmentIndex,
                    i => InstantActionFeature.EnvironmentAllowed(i, ia.MissionType.Key), i =>
                    {
                        ia.SelectEnvironment(i);
                        ia.ConfirmEnvironment();
                    });
        }

        for (int wave = 0; wave < InstantActionFeature.WaveSlots; wave++)
        {
            string n = wave.ToString(CultureInfo.InvariantCulture);
            var setup = ia.Waves[wave];
            int w = wave;
            if (key == WaveKeyPrefixes[0] + n)
            {
                return new DropdownList(Counts(InstantActionFeature.MaxEnemies), setup.Count, _ => true,
                    i => ia.SetWave(w, ia.Waves[w] with { Count = i }));
            }

            if (key == WaveKeyPrefixes[1] + n)
            {
                return new DropdownList(Names(InstantActionFeature.Militias, m => m.Name), setup.MilitiaIndex, _ => true,
                    i => ia.SelectWaveMilitia(w, i));
            }

            if (key == WaveKeyPrefixes[2] + n)
            {
                return new DropdownList(Names(InstantActionFeature.Skills, s => Capitalise(s)), setup.SkillIndex, _ => true,
                    i => ia.SelectWaveSkill(w, i));
            }

            if (key == WaveKeyPrefixes[3] + n)
            {
                return new DropdownList(Names(ia.WaveAircraft(w), a => a), setup.AircraftIndex, _ => true,
                    i => ia.SelectWaveAircraft(w, i));
            }
        }

        return null;
    }

    // A sideways step on the focused dropdown picks the next allowed value with wrap; on a radio
    // it moves the mark. False when the focused row is neither, so the step crosses columns.
    private bool StepInstantActionValue(IReadOnlyList<OriginalRow> rows, int focus, int direction)
    {
        if (focus < 0 || focus >= rows.Count)
        {
            return false;
        }

        var row = rows[focus];
        if (row.Kind == OriginalRowKind.Radio)
        {
            _iaRadio = row.Key == PlayerRadioKey ? 1 : 0;
            FocusKey(_iaRadio == 0 ? PlayerRadioKey : WingmanRadioKey);
            return true;
        }

        if (row.Kind != OriginalRowKind.Dropdown || DropdownFor(row.Key) is not { } list || list.Items.Count == 0)
        {
            return false;
        }

        int next = list.Current;
        for (int n = 0; n < list.Items.Count; n++)
        {
            next = ((next + direction) % list.Items.Count + list.Items.Count) % list.Items.Count;
            if (list.Allowed(next))
            {
                break;
            }
        }

        if (next != list.Current && list.Allowed(next))
        {
            list.Select(next);
        }

        FocusKey(row.Key);
        return true;
    }

    private bool CloseInstantActionDropdown()
    {
        if (_iaOpen == null)
        {
            return false;
        }

        string key = _iaOpen;
        _iaOpen = null;
        FocusKey(key);
        return true;
    }

    // Puts the focus on the row carrying a key, when the current rows have it.
    private void FocusKey(string key)
    {
        var rows = Rows;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Key == key)
            {
                _focus[(int)_screen] = i;
                return;
            }
        }
    }

    private MenuExit? ActivateInstantAction(OriginalRow row)
    {
        int colon = row.Key.IndexOf(':');
        if (colon > 0)
        {
            string prefix = row.Key[..colon];
            string suffix = row.Key[(colon + 1)..];
            if (prefix == ContentsKey)
            {
                switch (suffix)
                {
                    case "up":
                        _iaContentsTop--;
                        break;
                    case "down":
                        _iaContentsTop++;
                        break;
                    default:
                        // Selecting a contents row applies its preset over every other control,
                        // the list's own select callback; the environment is re-confirmed so the
                        // launch's base def follows the preset's chapter.
                        _instantAction.ApplyPreset(int.Parse(suffix, CultureInfo.InvariantCulture));
                        _instantAction.ConfirmEnvironment();
                        _iaPage = 0;
                        break;
                }

                return null;
            }

            if (DropdownFor(prefix) is { } list)
            {
                int index = int.Parse(suffix, CultureInfo.InvariantCulture);
                if (list.Allowed(index))
                {
                    list.Select(index);
                }

                _iaOpen = null;
                FocusKey(prefix);
            }

            return null;
        }

        switch (row.Key)
        {
            case ViewStoryKey:
                // View Story formats the stored preset's name through IDS_IA_STORYTITLE, whose
                // whole text is the name itself; nothing else changes.
                _iaStoryTitle = _instantAction.PresetIndex >= 0 ? InstantActionFeature.Presets[_instantAction.PresetIndex].Name : string.Empty;
                return null;
            case PageDownKey:
                _iaPage = 1;
                FocusKey(PageUpKey);
                return null;
            case PageUpKey:
                _iaPage = 0;
                FocusKey(PageDownKey);
                return null;
            case PlayerRadioKey:
                _iaRadio = 0;
                return null;
            case WingmanRadioKey:
                _iaRadio = 1;
                return null;
            case FlyMissionKey:
                return _instantAction.BuildExit(new[]
                {
                    new MenuSeatChoice(_instantAction.PlayerPlane.Node, Array.Empty<int>()),
                });
            case ExitKey:
                Open(OriginalScreen.TopLevel);
                return null;
        }

        if (row.Kind == OriginalRowKind.Dropdown && DropdownFor(row.Key) is { } open)
        {
            _iaOpen = row.Key;
            _focus[(int)_screen] = Math.Max(0, open.Current);
        }

        return null;
    }

    // The screen as drawn: the background under everything, the text rows, the widgets in their
    // states, and an open list as the overlay over the finished page.
    private void ComposeInstantAction(
        IReadOnlyList<OriginalRow> rows, int focus, List<BoardPicture> backdrop, List<BoardPicture> pictures,
        List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques, List<BoardPanel> overlays)
    {
        var screen = _layout.Screen(InstantActionSection);
        if (screen == null)
        {
            return;
        }

        if (screen.Widget("IA_BackGround") is { Art.Count: > 0 } background)
        {
            backdrop.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, background.Art[0], Math.Max(1, background.Frames)),
                background.Int("X"), background.Int("Y")));
        }

        bool ace = _instantAction.IsAceDuel;
        AddText(screen, lines, "IA_T_TABLETITLE", TitleFont);
        AddText(screen, lines, "IA_T_TABLEINSTR", InstructionFont);
        AddText(screen, lines, "IA_T_STORYINSTR", InstructionFont);
        AddText(screen, lines, "IA_T_STORYTITLE", StoryFont, _iaStoryTitle);
        AddText(screen, lines, "IA_T_PLAYERLABEL", InstructionFont);
        AddText(screen, lines, "IA_T_WINGMANLABEL", InstructionFont);
        if (_iaPage == 0)
        {
            AddText(screen, lines, "IA_T_PILOTPLANETITLE", LabelFont);
            AddText(screen, lines, "IA_T_WINGMANTITLE", LabelFont);
            AddText(screen, lines, "IA_T_MISSIONTITLE", LabelFont);
            AddText(screen, lines, "IA_T_ENVIRONMENTTITLE", LabelFont);
            if (!ace)
            {
                AddText(screen, lines, "IA_T_ENEMY0", LabelFont);
                AddText(screen, lines, "IA_T_CONTINUED", InstructionFont);
            }
        }
        else
        {
            if (!ace)
            {
                AddText(screen, lines, "IA_T_ENEMY1", LabelFont);
                AddText(screen, lines, "IA_T_ENEMY2", LabelFont);
                AddText(screen, lines, "IA_T_ENEMY3", LabelFont);
            }

            AddText(screen, lines, "IA_T_GOBACK", InstructionFont);
        }

        // With a list open the rows are its items; the page under it is drawn from the closed
        // widgets with the open dropdown focused, and the items become the overlay.
        IReadOnlyList<OriginalRow> widgets = rows;
        int widgetFocus = focus;
        int widgetPressed = _pressed;
        if (_iaOpen != null)
        {
            var closed = new List<OriginalRow>();
            BuildInstantActionWidgets(screen, closed);
            widgets = closed;
            widgetFocus = -1;
            widgetPressed = -1;
            for (int i = 0; i < closed.Count; i++)
            {
                if (closed[i].Key == _iaOpen)
                {
                    widgetFocus = i;
                }
            }
        }

        ComposeContentsThumb(screen, pictures);
        for (int i = 0; i < widgets.Count; i++)
        {
            ComposeInstantActionRow(widgets[i], i == widgetFocus, i == widgetPressed, i, fills, lines, plaques, pictures);
        }

        if (_iaOpen != null && rows.Count > 0)
        {
            var panelFills = new List<BoardFill>();
            var panelLines = new List<BoardLine>();
            var first = rows[0];
            var last = rows[rows.Count - 1];
            float height = last.Y + last.Height - first.Y;
            panelFills.Add(new BoardFill(first.X, first.Y, first.Width, height, 255, 255, 255, 0.94f));
            panelFills.Add(new BoardFill(first.X, first.Y, first.Width, height, 0, 0, 0, 1f, Border: true));
            for (int i = 0; i < rows.Count; i++)
            {
                var item = rows[i];
                if (i == focus)
                {
                    panelFills.Add(new BoardFill(item.X, item.Y, item.Width, item.Height, 0, 0, 0, 0.12f));
                }

                panelLines.Add(new BoardLine(item.Label, item.X + 4f, item.Y + 2f, item.Width - 8f, ItemFont,
                    item.Enabled ? (i == focus ? BoardInk.RowFocused : BoardInk.Row) : BoardInk.Detail, i));
            }

            overlays.Add(new BoardPanel(panelFills, Array.Empty<BoardPicture>(), panelLines));
        }
    }

    private void ComposeInstantActionRow(
        OriginalRow row, bool focused, bool pressed, int index,
        List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques, List<BoardPicture> pictures)
    {
        switch (row.Kind)
        {
            case OriginalRowKind.ListRow:
                // The contents list draws its picked and focused rows as rectangles and nothing
                // else, the list widget's own frame and fill.
                if (ContentsIndex(row.Key) == _instantAction.PresetIndex)
                {
                    fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 0, 0, 0, 0.15f));
                }

                if (focused)
                {
                    fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 0, 0, 0, 1f, Border: true));
                }

                lines.Add(new BoardLine(row.Label, row.X + 4f, row.Y + 2f, row.Width - 8f, ItemFont,
                    focused ? BoardInk.RowFocused : BoardInk.Row, index));
                break;
            case OriginalRowKind.Dropdown:
                if (focused)
                {
                    fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 0, 0, 0, 0.10f));
                }

                fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 0, 0, 0, 1f, Border: true));
                float arrowWidth = 0f;
                if (row.Art != null)
                {
                    var size = StripSize(row.Art, FallbackArrowWidth, FallbackArrowHeight);
                    arrowWidth = size.Width;
                    int frame = ComposedBoard.PlaqueFrame(row.Art.Frames, focused, pressed);
                    pictures.Add(new BoardPicture(row.Art, row.X + row.Width - size.Width, row.Y + ((row.Height - size.Height) / 2f), frame));
                }

                lines.Add(new BoardLine(row.Label, row.X + 4f, row.Y + 2f, Math.Max(1f, row.Width - arrowWidth - 6f), ItemFont,
                    focused ? BoardInk.RowFocused : BoardInk.Row, index));
                break;
            case OriginalRowKind.Radio when row.Art != null:
                // An eight-state strip: the four button states unmarked, then the same four marked.
                bool checkedRadio = (row.Key == PlayerRadioKey) == (_iaRadio == 0);
                int state = row.Enabled ? (pressed ? 3 : focused ? 2 : 1) : 0;
                plaques.Add(new BoardPlaque(row.Art, row.X, row.Y, index, (checkedRadio ? 4 : 0) + state, string.Empty, BoardInk.LabelNormal));
                break;
            case OriginalRowKind.TextButton when row.Art != null:
                int labelFrame = row.Enabled ? ComposedBoard.PlaqueFrame(row.Art.Frames, focused, pressed) : 0;
                var ink = row.Enabled ? ComposedBoard.PlaqueInk(focused, pressed) : BoardInk.Detail;
                plaques.Add(new BoardPlaque(row.Art, row.X, row.Y, index, labelFrame, row.Label, ink));
                break;
            case OriginalRowKind.Button when row.Art != null:
                int stripFrame = row.Enabled ? ComposedBoard.PlaqueFrame(row.Art.Frames, focused, pressed) : 0;
                plaques.Add(new BoardPlaque(row.Art, row.X, row.Y, index, stripFrame, string.Empty, BoardInk.LabelNormal));
                break;
        }
    }

    // The contents list's slider thumb, on the track between its two arrows, placed by how far
    // the window has scrolled.
    private void ComposeContentsThumb(MenuLayoutScreen screen, List<BoardPicture> pictures)
    {
        if (screen.Widget(ContentsKey) is not { } list || StripArt(list.Art, 0, 1) is not { } thumb)
        {
            return;
        }

        int window = Math.Max(1, list.Int("TotalDisplayed", 14));
        int count = InstantActionFeature.Presets.Count;
        if (count <= window)
        {
            return;
        }

        float itemHeight = list.Int("ItemHeight", (int)FallbackItemHeight);
        var arrow = StripSize(StripArt(list.Art, 1), FallbackArrowWidth, FallbackArrowHeight);
        var size = StripSize(thumb, arrow.Width, 11f);
        float trackTop = list.Int("Y") + arrow.Height;
        float track = (window * itemHeight) - (2f * arrow.Height) - size.Height;
        float y = trackTop + (track * ContentsTopClamped(window) / (count - window));
        pictures.Add(new BoardPicture(thumb, list.Int("X") + list.Int("Width", 277), y));
    }

    private void AddText(MenuLayoutScreen screen, List<BoardLine> lines, string key, float size, string? text = null)
    {
        if (screen.Widget(key) is not { } widget)
        {
            return;
        }

        string words = text ?? widget.Text ?? string.Empty;
        if (words.Length == 0)
        {
            return;
        }

        lines.Add(new BoardLine(words, widget.Int("X"), widget.Int("Y"), widget.Int("Width"), size,
            IsWhite(widget) ? BoardInk.Dialog : BoardInk.Heading, -1, false, Justify(widget)));
    }

    // One dropdown's list, its picked row, which rows may be picked and what picking one does.
    private sealed record DropdownList(IReadOnlyList<string> Items, int Current, Func<int, bool> Allowed, Action<int> Select);
}
