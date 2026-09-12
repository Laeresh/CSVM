using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Bindings;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// Original's two rebinding pages over the shared <see cref="ControlsFeature"/>: the decoded
/// <c>[@ControlsPrefs@]</c> page behind the Preferences page's CONTROLS door, and the decoded
/// <c>[@Keys@]</c> page behind its KEYS AND BUTTONS button, with the seven category tabs, the
/// action list in its two control columns, RESET TO DEFAULT and the exit pair. Every edit is
/// staged by the feature, so both pages' CANCEL CHANGES drops the whole visit and ACCEPT CHANGES
/// is what reaches the seat's keymap file. The readings the layout does not settle are in
/// <c>docs/org/menu-inventory.md</c>.
/// </summary>
public sealed partial class OriginalShell
{
    /// <summary>The Preferences page's fourth door, onto the CONTROLS page.</summary>
    public const string ControlsDoorKey = "PF_B_CONTROLS";

    /// <summary>The CONTROLS page's layout section.</summary>
    public const string ControlsPrefsSection = "ControlsPrefs";

    /// <summary>The KEYS AND BUTTONS page's layout section.</summary>
    public const string KeysSection = "Keys";

    /// <summary>The CONTROLS page's seat chooser, which takes the Controller Type row.</summary>
    public const string ControlsPlayerKey = "CP_D_Fly";

    /// <summary>The CONTROLS page's door onto the KEYS AND BUTTONS page.</summary>
    public const string KeysDoorKey = "CP_B_KEYS";

    /// <summary>The CONTROLS page's ACCEPT CHANGES, which writes the staged keymaps.</summary>
    public const string ControlsAcceptKey = "CP_B_ACCEPTCHANGES";

    /// <summary>The CONTROLS page's CANCEL CHANGES, which drops them.</summary>
    public const string ControlsCancelKey = "CP_B_CANCELCHANGES";

    /// <summary>The KEYS AND BUTTONS page's RESET TO DEFAULT.</summary>
    public const string KeysResetKey = "KB_B_RESET";

    /// <summary>The KEYS AND BUTTONS page's ACCEPT CHANGES.</summary>
    public const string KeysAcceptKey = "KB_B_ACCEPTCHANGES";

    /// <summary>The KEYS AND BUTTONS page's CANCEL CHANGES, authored left of ACCEPT CHANGES.
    /// </summary>
    public const string KeysCancelKey = "KB_B_CANCELCHANGES";

    /// <summary>The KEYS AND BUTTONS page's action list, whose window and pitch the rows take.
    /// </summary>
    public const string KeysListKey = "KB_L_Controls";

    /// <summary>How many category tabs the page authors.</summary>
    public const int KeysTabCount = 7;

    // The two control cells' key prefixes, each followed by the action's index in the standing tab.
    private const string KeysCellA = "KB:A:";
    private const string KeysCellB = "KB:B:";

    // The page's authored geometry, used where a layout does not carry the section: the list's
    // corner, pitch and window, the three column heads and the tab strip's corner and pitch.
    private const float KeysListX = 200f;
    private const float KeysListY = 310f;
    private const float KeysItemHeight = 16f;
    private const int KeysWindowRows = 12;
    private const float KeysActionX = 196f;
    private const float KeysActionWidth = 220f;
    private const float KeysControlAX = 455f;
    private const float KeysControlAWidth = 182f;
    private const float KeysControlBX = 641f;
    private const float KeysControlBWidth = 174f;
    private const float KeysHeadY = 287f;
    private const float KeysPlateX = 9f;
    private const float KeysPlateWidth = 788f;
    private const float KeysArrowWidth = 16f;

    // The rows under the category heading are indented, which is what the page's own still shows.
    private const float KeysRowIndent = 14f;

    private const float KeysHeadFont = 14f;
    private const float KeysRowFont = 12f;
    private const float KeysDescFont = 12f;

    // The CONTROLS page's authored row shape, the same fallbacks in the same spirit.
    private const float ControlsTitleX = 136f;
    private const float ControlsTitleWidth = 173f;
    private const float ControlsDropX = 134f;
    private const float ControlsDropY = 315f;
    private const float ControlsDropWidth = 175f;
    private const float ControlsItemHeight = 17f;
    private const float ControlsDescX = 349f;
    private const float ControlsDescWidth = 310f;

    // The six named tabs are the original's own action categories, over this port's flight actions.
    // The seventh, Other, is every action the six leave over, in context then enum order, so a new
    // action lands on a page rather than nowhere. docs/org/menu-inventory.md holds the reading.
    private static readonly InputAction[][] KeysFlightGroups =
    {
        new[]
        {
            InputAction.PitchUp, InputAction.PitchDown, InputAction.RollLeft, InputAction.RollRight,
            InputAction.YawLeft, InputAction.YawRight,
        },
        new[] { InputAction.ThrottleUp, InputAction.ThrottleDown, InputAction.Nitro, InputAction.AutoLand },
        new[]
        {
            InputAction.FireGuns, InputAction.FireRockets, InputAction.SelectGunGroup,
            InputAction.SelectGunGroupPrev, InputAction.SelectOrdnance, InputAction.SelectOrdnancePrev,
        },
        new[]
        {
            InputAction.TargetNextEnemy, InputAction.TargetNextAlly, InputAction.TargetNextNonAircraft,
            InputAction.TargetNearest, InputAction.TargetClear,
        },
        new[]
        {
            InputAction.CycleCockpitViews, InputAction.SelectChaseView, InputAction.FlybyView,
            InputAction.LookBack, InputAction.LookCenter, InputAction.FreeLook,
        },
        new[]
        {
            InputAction.LookUp, InputAction.LookDown, InputAction.LookLeft, InputAction.LookRight,
            InputAction.LookAimUp, InputAction.LookAimDown, InputAction.LookAimLeft, InputAction.LookAimRight,
        },
    };

    private static readonly string[] KeysTabNames =
    {
        "Movement", "Throttle", "Weapons", "Targeting", "Views 1", "Views 2", "Other",
    };

    private static readonly ControlsTab[] KeysTabs = BuildKeysTabs();

    private int _keysTab;
    private int _keysTop;

    /// <summary>The seven category tabs and the rows each one lists, in the order the page draws
    /// them.</summary>
    public static IReadOnlyList<ControlsTab> ControlTabs => KeysTabs;

    /// <summary>Which category tab the KEYS AND BUTTONS page is standing on.</summary>
    public int KeysTab => _keysTab;

    /// <summary>The first row of the standing tab that the list's window shows.</summary>
    public int KeysTop => _keysTop;

    // Whether a seat is registered at all. Without one the feature holds no keymap to read, so the
    // cells and the two whole-keymap buttons stand disabled rather than drawing somebody's blanks.
    private bool ControlsSeated => _controls is { } controls && controls.Players.Count > 0;

    /// <summary>The n-th tab's own button key, which is how the layout spells the strip.</summary>
    public static string KeysTabKey(int tab) =>
        "KB_B_CAT" + (tab + 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>The key of the control cell at that row and column, the two positions of the
    /// authored Control A and Control B columns.</summary>
    public static string KeysCellKey(int row, bool second) =>
        (second ? KeysCellB : KeysCellA) + row.ToString(CultureInfo.InvariantCulture);

    /// <summary>Opens the CONTROLS page on its first row, the Preferences page's CONTROLS door and
    /// the screenshot aid's door alike.</summary>
    public void OpenControlsPrefs()
    {
        _focus[(int)OriginalScreen.ControlsPrefs] = -1;
        Open(OriginalScreen.ControlsPrefs);
    }

    /// <summary>Opens the KEYS AND BUTTONS page on its first category with the list at its head.
    /// </summary>
    public void OpenKeys()
    {
        _keysTab = 0;
        _keysTop = 0;
        _focus[(int)OriginalScreen.Keys] = -1;
        _controls?.CancelCapture();
        Open(OriginalScreen.Keys);
    }

    /// <summary>Stands the KEYS AND BUTTONS page on one category tab, the screenshot aids' door.
    /// </summary>
    public void ShowKeysTab(int tab)
    {
        _keysTab = Math.Clamp(tab, 0, KeysTabs.Length - 1);
        _keysTop = 0;
        _focus[(int)OriginalScreen.Keys] = -1;
    }

    /// <summary>What the KEYS AND BUTTONS page prints in that row's Control A and Control B cells.
    /// The first control stands in A and the rest in B, which is what keeps a third binding from
    /// being hidden by two authored columns.</summary>
    public (string A, string B) KeysCellText(int row)
    {
        if (_controls is not { } controls || controls.Players.Count == 0
            || row < 0 || row >= KeysTabs[_keysTab].Rows.Count)
        {
            return (string.Empty, string.Empty);
        }

        var cell = KeysTabs[_keysTab].Rows[row];
        var bindings = controls.Bindings(cell.Context, cell.Action);
        if (bindings.Count == 0)
        {
            return (string.Empty, string.Empty);
        }

        string a = BindingLabels.Describe(bindings[0]);
        if (bindings.Count == 1)
        {
            return (a, string.Empty);
        }

        var rest = new List<Binding>(bindings.Count - 1);
        for (int i = 1; i < bindings.Count; i++)
        {
            rest.Add(bindings[i]);
        }

        return (a, BindingLabels.Row(rest, 1));
    }

    // The seven tabs: the six named ones over their own flight actions, then every action they
    // leave over. A tab claims each action once, which the shell's own unit test pins.
    private static ControlsTab[] BuildKeysTabs()
    {
        var tabs = new ControlsTab[KeysTabNames.Length];
        var claimed = new HashSet<InputAction>();
        for (int i = 0; i < KeysFlightGroups.Length; i++)
        {
            var rows = new List<ControlsTabRow>(KeysFlightGroups[i].Length);
            foreach (var action in KeysFlightGroups[i])
            {
                claimed.Add(action);
                rows.Add(new ControlsTabRow(InputContext.Flight, action));
            }

            tabs[i] = new ControlsTab(KeysTabNames[i], rows);
        }

        var other = new List<ControlsTabRow>();
        foreach (var context in Enum.GetValues<InputContext>())
        {
            foreach (var action in DefaultBindings.ActionsIn(context))
            {
                if (context != InputContext.Flight || !claimed.Contains(action))
                {
                    other.Add(new ControlsTabRow(context, action));
                }
            }
        }

        tabs[^1] = new ControlsTab(KeysTabNames[^1], other);
        return tabs;
    }

    // A leading [FONTID] tag is a renderer directive the extractor left on this one multi-line row,
    // and the newline inside it is the original's own break, which a wrapped line does not need.
    private static string KeysDescription(string text)
    {
        string line = text.Replace('\n', ' ');
        if (line.Length > 2 && line[0] == '[' && line.IndexOf(']') is var close && close > 1)
        {
            line = line[(close + 1)..];
        }

        return line;
    }

    private static int IndexOfAction(ControlsFeature controls, InputAction action)
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

    private static (int Row, bool Second)? KeysCellOf(string key)
    {
        bool second = key.StartsWith(KeysCellB, StringComparison.Ordinal);
        if (!second && !key.StartsWith(KeysCellA, StringComparison.Ordinal))
        {
            return null;
        }

        return int.TryParse(key[KeysCellA.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int row)
            ? (row, second)
            : null;
    }

    // The CONTROLS page: the seat chooser on the Controller Type row, the KEYS AND BUTTONS door and
    // the exit pair, all one column. Without the section the three stand as text buttons so the
    // page is still walkable.
    private void BuildControlsPrefsRows(List<OriginalRow> rows)
    {
        var screen = _layout.Screen(ControlsPrefsSection);
        if (screen == null)
        {
            rows.Add(TextButton(ControlsPlayerKey, PlayerWord(), OptionsX, OptionsTop, ControlsSeated, 0));
            rows.Add(TextButton(KeysDoorKey, "KEYS AND BUTTONS", OptionsX, OptionsTop + OptionsPitch, true, 0));
            rows.Add(TextButton(ControlsAcceptKey, "ACCEPT CHANGES", OptionsX, OptionsTop + (2f * OptionsPitch), true, 0));
            rows.Add(TextButton(ControlsCancelKey, "CANCEL CHANGES", OptionsX, OptionsTop + (3f * OptionsPitch), true, 0));
            return;
        }

        var box = ControlsPlayerBox(screen);
        rows.Add(new OriginalRow(ControlsPlayerKey, PlayerWord(), OriginalRowKind.Dropdown,
            box.X, box.Y, box.Width, box.Height, ControlsSeated, 0,
            StripArt(screen.Widget(ControlsPlayerKey)?.Art ?? Array.Empty<string>(), 4)));
        AddStrip(screen, rows, KeysDoorKey, OriginalRowKind.Button, true, 0);
        AddStrip(screen, rows, ControlsAcceptKey, OriginalRowKind.Button, true, 0);
        AddStrip(screen, rows, ControlsCancelKey, OriginalRowKind.Button, true, 0);
    }

    // The KEYS AND BUTTONS page's rows in cursor order: the seven tabs in their own column, then
    // each action's two control cells interleaved so a sideways step crosses from Control A to
    // Control B of the same action, then RESET and CANCEL under the left column with ACCEPT under
    // the right. A cell outside the list's window keeps its place for the cursor, unseen and unhit.
    private void BuildKeysRows(List<OriginalRow> rows)
    {
        var screen = _layout.Screen(KeysSection);
        var page = ReadKeysPage(screen);
        var tab = KeysTabs[_keysTab];
        for (int i = 0; i < KeysTabs.Length; i++)
        {
            var widget = screen?.Widget(KeysTabKey(i));
            var art = StripArt(widget?.Art ?? Array.Empty<string>(), 0, widget?.Frames ?? 4);
            var size = StripSize(art, FallbackPlaqueWidth, FallbackPlaqueHeight);
            float x = widget?.Int("X", (int)KeysPlateX) ?? KeysPlateX;
            float y = widget?.Int("Y", (int)(KeysHeadY + (i * size.Height))) ?? (KeysHeadY + (i * size.Height));
            rows.Add(new OriginalRow(KeysTabKey(i), KeysTabs[i].Name, OriginalRowKind.TextButton,
                x, y, size.Width, size.Height, true, 0, art));
        }

        int window = page.Rows;
        bool live = ControlsSeated;
        for (int i = 0; i < tab.Rows.Count; i++)
        {
            float y = page.LineY(i, _keysTop);
            bool visible = i >= _keysTop && i < _keysTop + window;
            rows.Add(new OriginalRow(KeysCellKey(i, false), string.Empty, OriginalRowKind.TextButton,
                page.ControlAX, y, page.ControlAWidth, page.ItemHeight, live, 1, null, visible));
            rows.Add(new OriginalRow(KeysCellKey(i, true), string.Empty, OriginalRowKind.TextButton,
                page.ControlBX, y, page.ControlBWidth, page.ItemHeight, live, 2, null, visible));
        }

        if (screen == null)
        {
            rows.Add(TextButton(KeysResetKey, "RESET TO DEFAULT", OptionsX, OptionsTop, live, 1));
            rows.Add(TextButton(KeysCancelKey, "CANCEL CHANGES", OptionsX, OptionsTop + OptionsPitch, true, 1));
            rows.Add(TextButton(KeysAcceptKey, "ACCEPT CHANGES", OptionsX, OptionsTop + (2f * OptionsPitch), true, 2));
            return;
        }

        AddStrip(screen, rows, KeysResetKey, OriginalRowKind.Button, live, 1);
        AddStrip(screen, rows, KeysCancelKey, OriginalRowKind.Button, true, 1);
        AddStrip(screen, rows, KeysAcceptKey, OriginalRowKind.Button, true, 2);
    }

    // The page's row shape off the section's own widgets, each falling back to the authored number
    // where the row is absent. ⚠ Two authored widths run past the plate: the list's 644 puts its
    // scrollbar at 844 and the Control B column's 174 ends at 815, against a plate that ends at 797.
    // Both are clamped to the plate, which is what the page's own still shows.
    private KeysPage ReadKeysPage(MenuLayoutScreen? screen)
    {
        var list = screen?.Widget(KeysListKey);
        var action = screen?.Widget("KB_T_COMMANDTITLE");
        var first = screen?.Widget("KB_T_CONTTITLEA");
        var second = screen?.Widget("KB_T_CONTTITLEB");
        var plate = screen?.Widget("KB_BACKGROUND");
        float plateX = plate?.Int("X", (int)KeysPlateX) ?? KeysPlateX;
        float plateWidth = plate != null && Measure(plate.Art.Count > 0 ? plate.Art[0] : string.Empty) is { } size
            ? size.Width
            : KeysPlateWidth;
        float right = plateX + plateWidth;
        float itemHeight = list?.Int("ItemHeight", (int)KeysItemHeight) ?? KeysItemHeight;
        var bar = StripArt(list?.Art ?? Array.Empty<string>(), 0, 1);
        var arrow = StripArt(list?.Art ?? Array.Empty<string>(), 1);
        var arrowSize = StripSize(arrow, KeysArrowWidth, KeysArrowWidth);
        float controlBX = second?.Int("X", (int)KeysControlBX) ?? KeysControlBX;
        return new KeysPage(
            list?.Int("X", (int)KeysListX) ?? KeysListX,
            list?.Int("Y", (int)KeysListY) ?? KeysListY,
            itemHeight,
            // The heading line naming the category takes the window's first row.
            Math.Max(1, (list?.Int("TotalDisplayed", KeysWindowRows) ?? KeysWindowRows) - 1),
            action?.Int("X", (int)KeysActionX) ?? KeysActionX,
            action?.Int("Width", (int)KeysActionWidth) ?? KeysActionWidth,
            first?.Int("X", (int)KeysControlAX) ?? KeysControlAX,
            first?.Int("Width", (int)KeysControlAWidth) ?? KeysControlAWidth,
            controlBX,
            Math.Min(second?.Int("Width", (int)KeysControlBWidth) ?? KeysControlBWidth, Math.Max(1f, right - controlBX)),
            action?.Int("Y", (int)KeysHeadY) ?? KeysHeadY,
            right - arrowSize.Width,
            arrowSize.Width,
            arrowSize.Height,
            StripSize(bar, arrowSize.Width, 11f).Height);
    }

    // The CONTROLS page's seat chooser at the Controller Type dropdown's own box.
    private (float X, float Y, float Width, float Height) ControlsPlayerBox(MenuLayoutScreen screen)
    {
        var drop = screen.Widget(ControlsPlayerKey);
        return (
            drop?.Int("X", (int)ControlsDropX) ?? ControlsDropX,
            drop?.Int("Y", (int)ControlsDropY) ?? ControlsDropY,
            drop?.Int("Width", (int)ControlsDropWidth) ?? ControlsDropWidth,
            drop?.Int("ItemHeight", (int)ControlsItemHeight) ?? ControlsItemHeight);
    }

    private string PlayerWord() => _controls is { } controls && controls.Players.Count > 0
        ? "Player " + controls.Player.ToString(CultureInfo.InvariantCulture)
        : string.Empty;

    private MenuExit? ActivateControlsPrefs(OriginalRow row)
    {
        switch (row.Key)
        {
            case ControlsPlayerKey:
                StepControlsPlayer(1);
                break;
            case KeysDoorKey:
                OpenKeys();
                break;
            case ControlsAcceptKey:
                _controls?.Accept();
                BackToPreferences();
                break;
            case ControlsCancelKey:
                _controls?.Cancel();
                BackToPreferences();
                break;
        }

        return null;
    }

    // A press on the KEYS AND BUTTONS page: a pending steal takes the answer first, a tab stands
    // its category, a cell arms a capture on that control's position, RESET restages this seat's
    // whole keymap from the shipped defaults, and the exit pair writes or drops the visit.
    private MenuExit? ActivateKeys(OriginalRow row)
    {
        if (_controls is not { } controls)
        {
            return null;
        }

        if (controls.Pending != null)
        {
            controls.ConfirmSteal();
            return null;
        }

        for (int i = 0; i < KeysTabs.Length; i++)
        {
            if (row.Key == KeysTabKey(i))
            {
                controls.CancelCapture();
                _keysTab = i;
                _keysTop = 0;
                return null;
            }
        }

        switch (row.Key)
        {
            case KeysResetKey:
                controls.ResetSeat();
                return null;
            case KeysAcceptKey:
                controls.Accept();
                Open(OriginalScreen.ControlsPrefs);
                return null;
            case KeysCancelKey:
                controls.Cancel();
                Open(OriginalScreen.ControlsPrefs);
                return null;
        }

        if (KeysCellOf(row.Key) is not { } cell)
        {
            return null;
        }

        FocusCell(cell.Row, cell.Second);
        controls.BeginCapture();
        return null;
    }

    // Points the feature at the control the cell stands for: the tab's context and action, then the
    // slot the column is. Control B is the second position, which on an action holding one control
    // is the empty slot past it, so a press there adds rather than replaces.
    private void FocusCell(int row, bool second)
    {
        if (_controls is not { } controls || row < 0 || row >= KeysTabs[_keysTab].Rows.Count)
        {
            return;
        }

        var cell = KeysTabs[_keysTab].Rows[row];
        controls.Context = cell.Context;
        int index = IndexOfAction(controls, cell.Action);
        controls.Focus(index);
        if (second)
        {
            controls.MoveSlot(1);
        }
    }

    // The seat chooser steps to the next registered player with wrap; a lone seat has nobody to
    // step to and the row stays where it is.
    private bool StepControlsPlayer(int direction)
    {
        if (_controls is not { } controls || controls.Players.Count < 2)
        {
            return false;
        }

        var players = controls.Players;
        int at = 0;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] == controls.Player)
            {
                at = i;
            }
        }

        int next = ((at + direction) % players.Count + players.Count) % players.Count;
        controls.Player = players[next];
        FocusKey(ControlsPlayerKey);
        return true;
    }

    // A sideways step on the seat chooser picks the next player; anything else crosses columns.
    private bool StepControlsValue(IReadOnlyList<OriginalRow> rows, int focus, int direction) =>
        focus >= 0 && focus < rows.Count && rows[focus].Key == ControlsPlayerKey
        && StepControlsPlayer(direction);

    // Back from either page: a pending steal is dropped first, then the page leaves the way its own
    // CANCEL CHANGES does, since that is the declining answer the layout gives each of them.
    private void BackControls()
    {
        if (_controls is { Pending: not null } pending)
        {
            pending.DiscardSteal();
            return;
        }

        _controls?.Cancel();
        if (_screen == OriginalScreen.Keys)
        {
            Open(OriginalScreen.ControlsPrefs);
            return;
        }

        BackToPreferences();
    }

    // Keeps the list's window over the cursor, so the cell the player is looking at is the cell a
    // capture binds. Read off the shell's own focus rather than the feature's row, which only
    // moves when a cell is pressed.
    private void SyncKeysWindow()
    {
        var page = ReadKeysPage(_layout.Screen(KeysSection));
        int count = KeysTabs[_keysTab].Rows.Count;
        int last = Math.Max(0, count - page.Rows);
        int top = Math.Clamp(_keysTop, 0, last);
        int focus = _focus[(int)OriginalScreen.Keys] - KeysTabCount;
        if (focus >= 0 && focus < count * 2)
        {
            int at = focus / 2;
            if (at < top)
            {
                top = at;
            }
            else if (at >= top + page.Rows)
            {
                top = at - page.Rows + 1;
            }
        }

        _keysTop = Math.Clamp(top, 0, last);
    }

    // The action list as the pointer's wheel and thumb see it, when the standing tab is longer than
    // the window.
    private void KeysLists(List<OriginalList> lists)
    {
        var page = ReadKeysPage(_layout.Screen(KeysSection));
        int count = KeysTabs[_keysTab].Rows.Count;
        if (count <= page.Rows)
        {
            return;
        }

        float top = page.LineY(0, _keysTop);
        float height = page.Rows * page.ItemHeight;
        float trackHeight = height - (2f * page.ArrowHeight);
        int at = Math.Clamp(_keysTop, 0, count - page.Rows);
        var window = new ListWindow(
            page.ListX, top, page.BarX + page.ArrowWidth - page.ListX, height,
            page.BarX,
            ListWindow.ThumbYFor(top + page.ArrowHeight, trackHeight, page.ThumbHeight, at, count - page.Rows),
            page.ArrowWidth, page.ThumbHeight,
            top + page.ArrowHeight, trackHeight, count, page.Rows, at);
        lists.Add(new OriginalList(KeysListKey, window, line => _keysTop = line));
    }

    // The CONTROLS page as drawn: the Preferences page's logo and this section's plate as backdrop,
    // the page title, the seat row's own title and description, the KEYS AND BUTTONS description as
    // authored, then the rows. The Mouse Sensitivity row is not drawn, this port having no cursor
    // speed of its own; its title and description go with it.
    private void ComposeControlsPrefs(
        IReadOnlyList<OriginalRow> rows, int focus, List<BoardPicture> backdrop, List<BoardPicture> pictures,
        List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques)
    {
        ComposePreferencesLogo(backdrop);
        var screen = _layout.Screen(ControlsPrefsSection);
        if (screen == null)
        {
            lines.Add(new BoardLine("CONTROLS", OptionsX, OptionsTop - 44f, 0f, HeadingFont, BoardInk.Heading));
            ComposeRows(rows, focus, fills, lines, plaques);
            return;
        }

        ComposePlate(screen, "CP_BACKGROUND", backdrop);
        ComposePageTitle(screen, "CP_T_TITLE", "CONTROLS", lines);
        if (screen.Widget("CP_T_JoystickTitle") is { } title)
        {
            lines.Add(new BoardLine("Player", title.Int("X", (int)ControlsTitleX), title.Int("Y"),
                title.Int("Width", (int)ControlsTitleWidth), GameOptionTitleFont, BoardInk.Row));
        }

        if (screen.Widget("CP_T_JoystickDesc") is { } description)
        {
            lines.Add(new BoardLine(
                "Whose keymap KEYS AND BUTTONS edits. Each seat holds its own.",
                description.Int("X", (int)ControlsDescX), description.Int("Y"),
                description.Int("Width", (int)ControlsDescWidth), KeysDescFont, BoardInk.Row));
        }

        if (screen.Widget("CP_T_KeysDesc") is { } keys)
        {
            lines.Add(new BoardLine(KeysDescription(keys.Text ?? string.Empty),
                keys.Int("X"), keys.Int("Y"), keys.Int("Width"), KeysDescFont, BoardInk.Row));
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Kind == OriginalRowKind.Button && row.Art != null)
            {
                plaques.Add(new BoardPlaque(row.Art, row.X, row.Y, i,
                    row.Enabled ? ComposedBoard.PlaqueFrame(row.Art.Frames, i == focus, i == _pressed) : 0,
                    string.Empty, BoardInk.LabelNormal));
                continue;
            }

            ComposeInstantActionRow(row, i == focus, i == _pressed, i, fills, lines, plaques, pictures,
                boxOnFocus: true);
        }
    }

    // The KEYS AND BUTTONS page as drawn: the plate, the title, the three column heads, the tab
    // strip with the standing tab in its depressed frame, the category heading over its rows in the
    // list's window, the two control columns, and the instruction line the status replaces while
    // the feature has something to say.
    private void ComposeKeys(
        IReadOnlyList<OriginalRow> rows, int focus, List<BoardPicture> backdrop, List<BoardPicture> pictures,
        List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques)
    {
        ComposePreferencesLogo(backdrop);
        var screen = _layout.Screen(KeysSection);
        if (screen == null)
        {
            lines.Add(new BoardLine("KEYS AND BUTTONS", OptionsX, OptionsTop - 44f, 0f, HeadingFont, BoardInk.Heading));
            ComposeRows(rows, focus, fills, lines, plaques);
            return;
        }

        ComposePlate(screen, "KB_BACKGROUND", backdrop);
        ComposePageTitle(screen, "KB_T_TITLE", "KEYS AND BUTTONS", lines);
        var page = ReadKeysPage(screen);
        foreach (var (key, fallbackX, fallbackWidth) in new[]
        {
            ("KB_T_COMMANDTITLE", page.ActionX, page.ActionWidth),
            ("KB_T_CONTTITLEA", page.ControlAX, page.ControlAWidth),
            ("KB_T_CONTTITLEB", page.ControlBX, page.ControlBWidth),
        })
        {
            if (screen.Widget(key) is { } head)
            {
                lines.Add(new BoardLine(head.Text ?? string.Empty, fallbackX, head.Int("Y", (int)page.HeadY),
                    fallbackWidth, KeysHeadFont, BoardInk.Row, -1, false, BoardJustify.Left, Bold: true));
            }
        }

        var tab = KeysTabs[_keysTab];
        lines.Add(new BoardLine(tab.Name, page.ActionX, page.ListY, page.ActionWidth, KeysRowFont,
            BoardInk.RowFocused, -1, false, BoardJustify.Left, Bold: true));
        for (int i = _keysTop; i < tab.Rows.Count && i < _keysTop + page.Rows; i++)
        {
            float y = page.LineY(i, _keysTop);
            lines.Add(new BoardLine(BindingLabels.Name(tab.Rows[i].Action), page.ActionX + KeysRowIndent, y,
                page.ActionWidth - KeysRowIndent, KeysRowFont, BoardInk.Row));
            var text = KeysCellText(i);
            lines.Add(new BoardLine(text.A, page.ControlAX, y, page.ControlAWidth, KeysRowFont, BoardInk.Row));
            lines.Add(new BoardLine(text.B, page.ControlBX, y, page.ControlBWidth, KeysRowFont, BoardInk.Row));
        }

        ComposeKeysRows(rows, focus, fills, plaques);
        ComposeKeysBar(page, tab.Rows.Count, fills, pictures, screen);
        if (screen.Widget("KB_T_DEFAULTDESC") is { } instruction)
        {
            string status = _controls?.Status ?? string.Empty;
            lines.Add(new BoardLine(
                status.Length > 0 ? status : KeysDescription(instruction.Text ?? string.Empty),
                instruction.Int("X"), instruction.Int("Y"), instruction.Int("Width"), KeysDescFont, BoardInk.Detail));
        }
    }

    // The page's own rows: the tabs and the three buttons as plaques, the standing tab in the
    // depressed frame it never leaves, and the focused control cell marked with the focus box.
    private void ComposeKeysRows(IReadOnlyList<OriginalRow> rows, int focus, List<BoardFill> fills, List<BoardPlaque> plaques)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Art != null && row.Kind == OriginalRowKind.TextButton)
            {
                int frame = row.Key == KeysTabKey(_keysTab)
                    ? 3
                    : ComposedBoard.PlaqueFrame(row.Art.Frames, i == focus, i == _pressed);
                // A tab's label takes the page's own text pair rather than the paper plaque's:
                // the strip authors ColorActive as the description cream and ColorRollover as the
                // file-wide ACTIVE, which is what those two palette roles already carry.
                plaques.Add(new BoardPlaque(row.Art, row.X, row.Y, i, frame, row.Label,
                    i == focus || i == _pressed ? BoardInk.RowFocused : BoardInk.Row));
                continue;
            }

            if (row.Art != null && row.Kind == OriginalRowKind.Button)
            {
                plaques.Add(new BoardPlaque(row.Art, row.X, row.Y, i,
                    row.Enabled ? ComposedBoard.PlaqueFrame(row.Art.Frames, i == focus, i == _pressed) : 0,
                    string.Empty, BoardInk.LabelNormal));
                continue;
            }

            if (i == focus && row.Visible && row.Enabled)
            {
                fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 0, 0, 0, 0.10f));
                fills.Add(FocusBox(row));
            }
        }
    }

    // The list's scrollbar, at the plate's own right edge rather than at the authored list width,
    // which stands past it. Nothing is drawn while the standing tab fits its window.
    private void ComposeKeysBar(
        KeysPage page, int count, List<BoardFill> fills, List<BoardPicture> pictures, MenuLayoutScreen screen)
    {
        if (count <= page.Rows)
        {
            return;
        }

        var list = screen.Widget(KeysListKey);
        var up = StripArt(list?.Art ?? Array.Empty<string>(), 1);
        var down = StripArt(list?.Art ?? Array.Empty<string>(), 2);
        var bar = StripArt(list?.Art ?? Array.Empty<string>(), 0, 1);
        float top = page.LineY(0, _keysTop);
        float trackHeight = (page.Rows * page.ItemHeight) - (2f * page.ArrowHeight);
        float thumbY = ListWindow.ThumbYFor(top + page.ArrowHeight, trackHeight, page.ThumbHeight,
            Math.Clamp(_keysTop, 0, count - page.Rows), count - page.Rows);
        if (up != null && down != null && bar != null
            && Measure(up.Name) != null && Measure(down.Name) != null && Measure(bar.Name) != null)
        {
            pictures.Add(new BoardPicture(up, page.BarX, top));
            pictures.Add(new BoardPicture(down, page.BarX, top + (page.Rows * page.ItemHeight) - page.ArrowHeight));
            pictures.Add(new BoardPicture(bar, page.BarX, thumbY));
            return;
        }

        fills.Add(new BoardFill(page.BarX, top, page.ArrowWidth, page.Rows * page.ItemHeight, 255, 255, 255, 0.3f, Border: true));
        fills.Add(new BoardFill(page.BarX, thumbY, page.ArrowWidth, page.ThumbHeight, 255, 255, 255, 0.6f));
    }

    // Both pages draw the Preferences page's logo, their own sections authoring none, and both put
    // their plate in the backdrop for the reason the other option pages do: a board draws its fills
    // between the two layers, so a plate among the pictures buries every focus mark.
    private void ComposePreferencesLogo(List<BoardPicture> backdrop)
    {
        if (_layout.Screen(PreferencesSection)?.Widget("PF_LOGO") is { Art.Count: > 0 } logo)
        {
            backdrop.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, logo.Art[0], Math.Max(1, logo.Frames)),
                logo.Int("X"), logo.Int("Y")));
        }
    }

    private void ComposePlate(MenuLayoutScreen screen, string key, List<BoardPicture> backdrop)
    {
        if (screen.Widget(key) is { Art.Count: > 0 } plate)
        {
            backdrop.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, plate.Art[0], Math.Max(1, plate.Frames)),
                plate.Int("X"), plate.Int("Y")));
        }
    }

    private void ComposePageTitle(MenuLayoutScreen screen, string key, string fallback, List<BoardLine> lines)
    {
        if (screen.Widget(key) is { } title)
        {
            lines.Add(new BoardLine(title.Text ?? fallback, title.Int("X"), title.Int("Y"), title.Int("Width"),
                PreferencesTitleFont, BoardInk.Heading, -1, false,
                title.Int("Justify") == 1 ? BoardJustify.Center : BoardJustify.Left));
        }
    }

    /// <summary>One row of a category tab: which keymap it belongs to and which action it names.
    /// The two travel together because the seven tabs are the original's action groups and this
    /// port holds three keymaps, so one tab can list rows from more than one of them.</summary>
    public readonly record struct ControlsTabRow(InputContext Context, InputAction Action);

    /// <summary>One category tab of the KEYS AND BUTTONS page: the word on its button and the rows
    /// it lists.</summary>
    public sealed record ControlsTab(string Name, IReadOnlyList<ControlsTabRow> Rows);

    // The KEYS AND BUTTONS page's row shape in authored pixels: the list's corner, pitch and the
    // rows it shows under the category heading, the three columns, the head line, and the
    // scrollbar's own column at the plate's right edge.
    private sealed record KeysPage(
        float ListX, float ListY, float ItemHeight, int Rows,
        float ActionX, float ActionWidth, float ControlAX, float ControlAWidth,
        float ControlBX, float ControlBWidth, float HeadY,
        float BarX, float ArrowWidth, float ArrowHeight, float ThumbHeight)
    {
        // The category heading stands on the window's first line, so a row sits one line below it.
        public float LineY(int row, int top) => ListY + ((row - top + 1) * ItemHeight);
    }
}
