using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Bindings;
using CSVM.Sticks;
using CSVM.UI.Boards;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The KEYS AND BUTTONS page behind the CONTROLS page, one page module over the decoded
/// <c>[@Keys@]</c> section and the shared <see cref="ControlsFeature"/>. It carries seven category
/// tabs and one action list under its heading in the listbox's own window. Each row's key and pad
/// controls stand in the two authored columns, its stick controls in the Stick column between them
/// (<see cref="KeysStickColumn"/>). A cell press arms a capture on that row's action and slot,
/// and the shell swallows the frame while one runs. Its edits are staged in the feature.
/// </summary>
public sealed class OriginalKeysPage : IOriginalOptionsPage
{
    /// <summary>The KEYS AND BUTTONS page's layout section.</summary>
    public const string Section = "Keys";

    /// <summary>The page's RESET TO DEFAULT.</summary>
    public const string ResetKey = "KB_B_RESET";

    /// <summary>The page's ACCEPT CHANGES.</summary>
    public const string AcceptKey = "KB_B_ACCEPTCHANGES";

    /// <summary>The page's CANCEL CHANGES, authored left of ACCEPT CHANGES.</summary>
    public const string CancelKey = "KB_B_CANCELCHANGES";

    /// <summary>The page's action list, whose window and pitch the rows take.</summary>
    public const string ListKey = "KB_L_Controls";

    /// <summary>How many category tabs the page authors.</summary>
    public const int TabCount = 7;

    /// <summary>The sentence after the page's authored description, naming the clear gesture the
    /// original page has no word for (<see cref="ClearCell"/>). ⚠ Keep it short: the authored
    /// description fills most of the plate's two lines, and a third runs onto CANCEL CHANGES.</summary>
    public const string ClearHint = "Delete clears the control.";

    // The three control cells' key prefixes, each followed by the action's index in the standing tab.
    private const string CellA = "KB:A:";
    private const string CellB = "KB:B:";
    private const string CellStick = "KB:S:";

    // The cells each action row carries, in cursor order: Control A, Stick, Control B.
    private const int CellsPerRow = 3;

    // A category tab's size where its strip cannot be measured: the shell's paper-plaque fallback,
    // that strip being what the tab column falls back to.
    private const float FallbackTabWidth = 162f;
    private const float FallbackTabHeight = 28f;

    // The page's authored geometry, used where a layout does not carry the section. It is the
    // list's corner, pitch and window, the three column heads, and the tab strip's corner and pitch.
    private const float ListX = 200f;
    private const float ListY = 310f;
    private const float ItemHeight = 16f;
    private const int WindowRows = 12;
    private const float ActionX = 196f;
    private const float ActionWidth = 220f;
    private const float ControlAX = 455f;
    private const float ControlAWidth = 182f;
    private const float ControlBX = 641f;
    private const float ControlBWidth = 174f;
    private const float HeadY = 287f;
    private const float PlateX = 9f;
    private const float PlateWidth = 788f;
    private const float ArrowWidth = 16f;

    // The Stick column is this port's own, so no widget authors it. The plate paints two dividers,
    // leaving three panels, and the Control A panel's right half is the one space its keys leave
    // empty. The column stands there, ending short of the divider before Control B.
    private const string StickHead = "Stick";
    private const float StickX = 535f;
    private const float StickWidth = 77f;
    private const float ColumnGap = 4f;

    // The rows under the category heading are indented, which is what the page's own still shows.
    private const float RowIndent = 14f;

    private const float HeadFont = 14f;
    private const float RowFont = 12f;

    // The six named tabs are the original's own action categories, over this port's flight actions,
    // each in that page's own row order (`OriginalScreenshots/Keybinds *.png`). The Throttle tab
    // appends the port's Throttle (lever) row after the original's eleven. The seventh, Other,
    // takes its own flight rows first, then every action the six leave over, in context then enum
    // order. A new action therefore lands on a page rather than nowhere. The reading is in
    // docs/org/menu-inventory.md.
    private static readonly InputAction[][] FlightGroups =
    {
        new[]
        {
            InputAction.PitchDown, InputAction.PitchUp, InputAction.RollLeft, InputAction.RollRight,
            InputAction.YawLeft, InputAction.YawRight,
        },
        new[]
        {
            InputAction.ThrottleUp, InputAction.ThrottleDown, InputAction.ThrottleSet0,
            InputAction.ThrottleSet1, InputAction.ThrottleSet2, InputAction.ThrottleSet3,
            InputAction.ThrottleSet4, InputAction.ThrottleSet5, InputAction.ThrottleSet6,
            InputAction.ThrottleSet7, InputAction.ThrottleSet8, InputAction.ThrottleLever,
        },
        new[]
        {
            InputAction.FireGuns, InputAction.FireRockets, InputAction.SelectGunGroup,
            InputAction.SelectGunGroupPrev, InputAction.SelectOrdnance, InputAction.SelectOrdnancePrev,
        },
        new[]
        {
            InputAction.TargetNextEnemy, InputAction.TargetPreviousEnemy, InputAction.TargetNearestEnemy,
            InputAction.TargetNextAlly, InputAction.TargetPreviousAlly, InputAction.TargetNearestAlly,
            InputAction.TargetNextNonAircraft, InputAction.TargetPreviousNonAircraft,
            InputAction.TargetNearestNonAircraft, InputAction.TargetNearest, InputAction.TargetClear,
        },
        new[]
        {
            InputAction.ToggleSpyglass, InputAction.CycleCockpitViews, InputAction.FlybyView,
            InputAction.SnapLookMode, InputAction.TrackTarget, InputAction.SmoothLookMode,
            InputAction.SelectChaseView,
            InputAction.LookBack, InputAction.LookCenter, InputAction.FreeLook,
            InputAction.ZoomIn, InputAction.ZoomOut,
        },
        new[]
        {
            InputAction.LookUp, InputAction.LookDown, InputAction.LookLeft, InputAction.LookRight,
            InputAction.LookAimUp, InputAction.LookAimDown, InputAction.LookAimLeft, InputAction.LookAimRight,
        },
    };

    // The Other tab's own flight rows, in the original's Other page order, then this port's
    // graphics-mode switch. The rest of that tab is whatever the seven groups leave over, which is
    // every menu and free-camera action.
    private static readonly InputAction[] OtherFlightGroup =
    {
        InputAction.AutoLand, InputAction.Nitro, InputAction.Respawn, InputAction.Pause,
        InputAction.ChatEveryone, InputAction.ChatTeam,
        InputAction.ToggleGraphicsMode,
    };

    private static readonly string[] TabNames =
    {
        "Movement", "Throttle", "Weapons", "Targeting", "Views 1", "Views 2", "Other",
    };

    private static readonly ControlsTab[] Tabs = BuildTabs();

    private readonly OriginalOptionsChrome _chrome;
    private readonly IOriginalScreenHost _host;
    private readonly ControlsFeature? _controls;

    private int _tab;
    private int _top;

    internal OriginalKeysPage(OriginalOptionsChrome chrome, ControlsFeature? controls)
    {
        _chrome = chrome ?? throw new ArgumentNullException(nameof(chrome));
        _host = chrome.Host;
        _controls = controls;
    }

    // Which of a row's three control cells a key names.
    private enum Column
    {
        A,
        B,
        Stick,
    }

    /// <summary>The seven category tabs and the rows each one lists, in the order the page draws
    /// them.</summary>
    public static IReadOnlyList<ControlsTab> ControlTabs => Tabs;

    /// <summary>Which category tab the page is standing on.</summary>
    public int Tab => _tab;

    /// <summary>The first row of the standing tab that the list's window shows.</summary>
    public int Top => _top;

    OriginalScreen IOriginalOptionsPage.Screen => OriginalScreen.Keys;

    string IOriginalOptionsPage.AcceptKey => AcceptKey;

    string IOriginalOptionsPage.CancelKey => CancelKey;

    // Whether a seat is registered at all. Without one the feature holds no keymap to read, so the
    // cells and the two whole-keymap buttons stand disabled rather than drawing somebody's blanks.
    private bool Seated => _controls is { } controls && controls.Players.Count > 0;

    /// <summary>The n-th tab's own button key, which is how the layout spells the strip.</summary>
    public static string TabKey(int tab) =>
        "KB_B_CAT" + (tab + 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>The key of the control cell at that row and column, the two positions of the
    /// authored Control A and Control B columns.</summary>
    public static string CellKey(int row, bool second) =>
        (second ? CellB : CellA) + row.ToString(CultureInfo.InvariantCulture);

    /// <summary>The key of that row's Stick cell, which lists and captures stick controls alone.
    /// </summary>
    public static string StickCellKey(int row) =>
        CellStick + row.ToString(CultureInfo.InvariantCulture);

    /// <summary>Opens the page on its first category with the list at its head.</summary>
    public void Open()
    {
        _tab = 0;
        _top = 0;
        _controls?.CancelCapture();
        _host.Open(OriginalScreen.Keys);
        _host.FocusedRow = -1;
    }

    /// <summary>Stands the page on one category tab, the screenshot aids' door.</summary>
    public void ShowTab(int tab)
    {
        _tab = Math.Clamp(tab, 0, Tabs.Length - 1);
        _top = 0;
        _host.FocusedRow = -1;
    }

    /// <summary>Adds posed controls to the standing tab's first three rows, the screenshot aids'
    /// door to the cells' marquee. They are a long stick caption, several sticks in one cell and
    /// several keys in Control B. The sticks are two unnamed models no roster holds. Nothing is
    /// staged or saved: the live keymap changes in memory for the run.</summary>
    public void PoseStickCaptions()
    {
        if (_controls is not { Players.Count: > 0 } controls)
        {
            return;
        }

        var rows = Tabs[_tab].Rows;
        var right = new StickModel(0x231D, 0x0200).Device;
        var left = new StickModel(0x231D, 0x0201).Device;
        controls.Follow(controls.Player, profile =>
        {
            for (int i = 0; i < rows.Count && i < 3; i++)
            {
                var map = profile.Map(rows[i].Context);
                var action = rows[i].Action;
                var posed = i switch
                {
                    0 => new[] { new Binding(right, BindingControl.FullAxis(5, true, StickCapture.FlightDeadzone)) },
                    1 => new[]
                    {
                        new Binding(right, BindingControl.Button(3)),
                        new Binding(left, BindingControl.Button(11)),
                        new Binding(left, BindingControl.Hat(0, HatDirection.Up)),
                    },
                    _ => new[]
                    {
                        new Binding(DeviceId.Keyboard, BindingControl.Key((int)Godot.Key.Pagedown)),
                        new Binding(DeviceId.Keyboard, BindingControl.Key((int)Godot.Key.Insert)),
                    },
                };
                foreach (var binding in posed)
                {
                    // A row taking no full axis refuses one; a hat stands in for the long caption.
                    if (!map.Add(action, binding) && binding.Control.Kind == ControlKind.FullAxis)
                    {
                        map.Add(action, new Binding(right, BindingControl.Hat(0, HatDirection.Down)));
                    }
                }
            }
        });
    }

    /// <summary>What the page prints in that row's Control A, Control B and Stick cells. Stick
    /// controls stand in Stick alone (<see cref="KeysStickColumn"/>). Of the rest the first stands
    /// in A and every other in B, joined by <see cref="KeysStickColumn.Separator"/>, so a third
    /// binding is never hidden.</summary>
    public (string A, string B, string Stick) CellText(int row)
    {
        if (_controls is not { } controls || controls.Players.Count == 0
            || row < 0 || row >= Tabs[_tab].Rows.Count)
        {
            return (string.Empty, string.Empty, string.Empty);
        }

        var cell = Tabs[_tab].Rows[row];
        var (others, sticks) = KeysStickColumn.Split(controls.Bindings(cell.Context, cell.Action));
        string stick = KeysStickColumn.Text(sticks);
        if (others.Count == 0)
        {
            return (string.Empty, string.Empty, stick);
        }

        return (BindingLabels.Describe(others[0]),
            KeysStickColumn.Joined(others.GetRange(1, others.Count - 1), BindingLabels.Describe), stick);
    }

    /// <summary>Clears the control a cell shows, answering whether the page changed. Control A
    /// drops the binding it prints. Control B and the Stick cell drop the first binding they list,
    /// so each press takes the leading caption and the next one moves up. A full axis leaves its
    /// pair's other row as well (<see cref="ControlsFeature.UnbindSlot"/>). Nothing happens off a
    /// cell or while a steal awaits its answer.</summary>
    public bool ClearCell(OriginalRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (_controls is not { Capturing: false, Pending: null } controls || CellOf(row.Key) is not { } cell)
        {
            return false;
        }

        FocusCell(cell.Row, cell.Column);
        if (cell.Column == Column.Stick)
        {
            controls.MoveSlot(KeysStickColumn.SlotOfStick(controls.FocusedBindings));
        }

        if (controls.Slot >= controls.FocusedBindings.Count)
        {
            return false;
        }

        controls.UnbindSlot();
        return true;
    }

    // The tabs in their own column, then each action's three cells in the order they stand, so a
    // sideways step crosses one action's cells. RESET and CANCEL stand under the left column with
    // ACCEPT under Control B. A cell outside the list's window keeps its place, unseen.
    void IOriginalOptionsPage.BuildRows(List<OriginalRow> rows)
    {
        var screen = _chrome.Layout.Screen(Section);
        var page = ReadShape(screen);
        var tab = Tabs[_tab];
        for (int i = 0; i < Tabs.Length; i++)
        {
            var widget = screen?.Widget(TabKey(i));
            var art = OriginalOptionsChrome.StripArt(widget?.Art ?? Array.Empty<string>(), 0, widget?.Frames ?? 4);
            var size = _chrome.StripSize(art, FallbackTabWidth, FallbackTabHeight);
            float x = widget?.Int("X", (int)PlateX) ?? PlateX;
            float y = widget?.Int("Y", (int)(HeadY + (i * size.Height))) ?? (HeadY + (i * size.Height));
            rows.Add(new OriginalRow(TabKey(i), Tabs[i].Name, OriginalRowKind.TextButton,
                x, y, size.Width, size.Height, true, 0, art));
        }

        int window = page.Rows;
        bool live = Seated;
        for (int i = 0; i < tab.Rows.Count; i++)
        {
            float y = page.LineY(i, _top);
            bool visible = i >= _top && i < _top + window;
            rows.Add(new OriginalRow(CellKey(i, false), string.Empty, OriginalRowKind.TextButton,
                page.ControlAX, y, page.ControlAWidth, page.ItemHeight, live, 1, null, visible));
            rows.Add(new OriginalRow(StickCellKey(i), string.Empty, OriginalRowKind.TextButton,
                page.StickX, y, page.StickWidth, page.ItemHeight, live, 2, null, visible));
            rows.Add(new OriginalRow(CellKey(i, true), string.Empty, OriginalRowKind.TextButton,
                page.ControlBX, y, page.ControlBWidth, page.ItemHeight, live, 3, null, visible));
        }

        if (screen == null)
        {
            rows.Add(_host.PlaqueRow(ResetKey, "RESET TO DEFAULT", 0, live, 1));
            rows.Add(_host.PlaqueRow(CancelKey, "CANCEL CHANGES", 1, true, 1));
            rows.Add(_host.PlaqueRow(AcceptKey, "ACCEPT CHANGES", 2, true, 3));
            return;
        }

        _chrome.AddStrip(screen, rows, ResetKey, OriginalRowKind.Button, live, 1);
        _chrome.AddStrip(screen, rows, CancelKey, OriginalRowKind.Button, true, 1);
        _chrome.AddStrip(screen, rows, AcceptKey, OriginalRowKind.Button, true, 3);
    }

    // The action list as the pointer's wheel and thumb see it, when the standing tab is longer than
    // the window.
    void IOriginalOptionsPage.Lists(List<OriginalList> lists)
    {
        var page = ReadShape(_chrome.Layout.Screen(Section));
        int count = Tabs[_tab].Rows.Count;
        if (count <= page.Rows)
        {
            return;
        }

        float top = page.WindowY;
        float height = page.Rows * page.ItemHeight;
        float trackHeight = height - (2f * page.ArrowHeight);
        int at = Math.Clamp(_top, 0, count - page.Rows);
        var window = new ListWindow(
            page.ListX, top, page.BarX + page.ArrowWidth - page.ListX, height,
            page.BarX,
            ListWindow.ThumbYFor(top + page.ArrowHeight, trackHeight, page.ThumbHeight, at, count - page.Rows),
            page.ArrowWidth, page.ThumbHeight,
            top + page.ArrowHeight, trackHeight, count, page.Rows, at);
        lists.Add(new OriginalList(ListKey, window, line => _top = line));
    }

    bool IOriginalOptionsPage.StepSideways(IReadOnlyList<OriginalRow> rows, int focus, int direction) => false;

    bool IOriginalOptionsPage.CloseDropdown() => false;

    // A press. A pending steal takes the answer first, and a tab stands its category. A cell arms a
    // capture on that control's position, and RESET restages this seat's whole keymap from the
    // shipped defaults.
    MenuExit? IOriginalOptionsPage.Activate(OriginalRow row)
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

        for (int i = 0; i < Tabs.Length; i++)
        {
            if (row.Key == TabKey(i))
            {
                controls.CancelCapture();
                _tab = i;
                _top = 0;
                return null;
            }
        }

        if (row.Key == ResetKey)
        {
            controls.ResetSeat();
            return null;
        }

        if (CellOf(row.Key) is not { } cell)
        {
            return null;
        }

        FocusCell(cell.Row, cell.Column);
        if (cell.Column == Column.Stick)
        {
            controls.BeginStickCapture();
        }
        else
        {
            controls.BeginCapture();
        }

        return null;
    }

    // The exit pair writes or drops the visit, a pending steal taking the press first.
    MenuExit? IOriginalOptionsPage.Accept()
    {
        if (Answers() is { } controls)
        {
            controls.Accept();
            _host.Open(OriginalScreen.ControlsPrefs);
        }

        return null;
    }

    void IOriginalOptionsPage.Cancel()
    {
        if (Answers() is { } controls)
        {
            controls.Cancel();
            _host.Open(OriginalScreen.ControlsPrefs);
        }
    }

    // A pending steal is dropped first. Then the page leaves for CONTROLS the way its own CANCEL
    // CHANGES does, the declining answer the layout gives it.
    void IOriginalOptionsPage.Back()
    {
        if (_controls is { Pending: not null } pending)
        {
            pending.DiscardSteal();
            return;
        }

        _controls?.Cancel();
        _host.Open(OriginalScreen.ControlsPrefs);
    }

    // The plate, the title, the column heads and the tab strip with the standing tab depressed. The
    // category heading stands over its rows in the list's window. The instruction line closes it,
    // replaced by the status while the feature has something to say.
    void IOriginalOptionsPage.Compose(IReadOnlyList<OriginalRow> rows, int focus, BoardLayers layers)
    {
        var screen = _chrome.Layout.Screen(Section);
        if (screen == null)
        {
            _host.ComposePlainPage("KEYS AND BUTTONS", rows, focus, layers);
            return;
        }

        _chrome.ComposePlate(screen, "KB_BACKGROUND", layers.Backdrop);
        _chrome.ComposePageTitle(screen, "KB_T_TITLE", "KEYS AND BUTTONS", layers.Lines);
        var lines = layers.Lines;
        var page = ReadShape(screen);
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
                    fallbackWidth, HeadFont, BoardInk.Row, -1, false, BoardJustify.Left, Bold: true));
            }
        }

        lines.Add(new BoardLine(StickHead, page.StickX, page.HeadY, page.StickWidth, HeadFont,
            BoardInk.Row, -1, false, BoardJustify.Left, Bold: true));
        var tab = Tabs[_tab];
        lines.Add(new BoardLine(tab.Name, page.ActionX, page.ListY, page.ActionWidth, RowFont,
            BoardInk.RowFocused, -1, false, BoardJustify.Left, Bold: true));
        for (int i = _top; i < tab.Rows.Count && i < _top + page.Rows; i++)
        {
            float y = page.LineY(i, _top);
            lines.Add(new BoardLine(BindingLabels.Name(tab.Rows[i].Action), page.ActionX + RowIndent, y,
                page.ActionWidth - RowIndent, RowFont, BoardInk.Row));
            var text = CellText(i);
            // A caption wider than its cell scrolls inside it rather than wrapping onto the row
            // below, which the 16-pixel pitch has no room for. Control B's clip stops short of the
            // scrollbar's column, where its authored width would run it under the plate's frame.
            lines.Add(new BoardLine(text.A, page.ControlAX, y, page.ControlAWidth, RowFont, BoardInk.Row)
            {
                Marquee = true,
            });
            lines.Add(new BoardLine(text.Stick, page.StickX, y, page.StickWidth, RowFont, BoardInk.Row)
            {
                Marquee = true,
            });
            float bWidth = Math.Max(1f, Math.Min(page.ControlBWidth, page.BarX - ColumnGap - page.ControlBX));
            lines.Add(new BoardLine(text.B, page.ControlBX, y, bWidth, RowFont, BoardInk.Row)
            {
                Marquee = true,
            });
        }

        ComposeRows(rows, focus, layers);
        ComposeBar(page, tab.Rows.Count, layers, screen);
        if (screen.Widget("KB_T_DEFAULTDESC") is { } instruction)
        {
            string status = _controls?.Status ?? string.Empty;
            lines.Add(new BoardLine(
                status.Length > 0 ? status : OriginalOptionsChrome.Untagged(instruction.Text ?? string.Empty) + " " + ClearHint,
                instruction.Int("X"), instruction.Int("Y"), instruction.Int("Width"),
                OriginalOptionsChrome.DescriptionFont, BoardInk.Detail));
        }
    }

    /// <summary>Keeps the list's window over the cursor at the end of a frame. The cell the player
    /// is looking at is then the cell a capture binds. Read off the showing screen's own focus rather
    /// than the feature's row, which only moves when a cell is pressed.</summary>
    internal void SyncWindow()
    {
        var page = ReadShape(_chrome.Layout.Screen(Section));
        int count = Tabs[_tab].Rows.Count;
        int last = Math.Max(0, count - page.Rows);
        int top = Math.Clamp(_top, 0, last);
        int focus = _host.FocusedRow - TabCount;
        if (focus >= 0 && focus < count * CellsPerRow)
        {
            int at = focus / CellsPerRow;
            if (at < top)
            {
                top = at;
            }
            else if (at >= top + page.Rows)
            {
                top = at - page.Rows + 1;
            }
        }

        _top = Math.Clamp(top, 0, last);
    }

    // The seven tabs: the six named ones over their own flight actions, then every action they
    // leave over. A tab claims each action once, which the page's own unit test pins.
    private static ControlsTab[] BuildTabs()
    {
        var tabs = new ControlsTab[TabNames.Length];
        var claimed = new HashSet<InputAction>();
        for (int i = 0; i < FlightGroups.Length; i++)
        {
            var rows = new List<ControlsTabRow>(FlightGroups[i].Length);
            foreach (var action in FlightGroups[i])
            {
                claimed.Add(action);
                rows.Add(new ControlsTabRow(InputContext.Flight, action));
            }

            tabs[i] = new ControlsTab(TabNames[i], rows);
        }

        var other = new List<ControlsTabRow>();
        foreach (var action in OtherFlightGroup)
        {
            claimed.Add(action);
            other.Add(new ControlsTabRow(InputContext.Flight, action));
        }

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

        tabs[^1] = new ControlsTab(TabNames[^1], other);
        return tabs;
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

    private static (int Row, Column Column)? CellOf(string key)
    {
        Column column;
        if (key.StartsWith(CellA, StringComparison.Ordinal))
        {
            column = Column.A;
        }
        else if (key.StartsWith(CellB, StringComparison.Ordinal))
        {
            column = Column.B;
        }
        else if (key.StartsWith(CellStick, StringComparison.Ordinal))
        {
            column = Column.Stick;
        }
        else
        {
            return null;
        }

        // The three prefixes are one length, so the row number starts at the same place in each.
        return int.TryParse(key[CellA.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int row)
            ? (row, column)
            : null;
    }

    // The feature where the exit pair may write or drop the visit, or null. A pending steal takes
    // the press as its answer instead, and with no feature the pair does nothing.
    private ControlsFeature? Answers()
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

        return controls;
    }

    // Points the feature at the control the cell stands for: the tab's context and action, then the
    // slot the column is. Control A is the first control no stick holds and Control B the second.
    // On an action holding one, B is the empty slot past the list, so a press there adds. A Stick
    // cell's slot is chosen once the capture knows which stick answered (ControlsFeature.OfferStick).
    private void FocusCell(int row, Column column)
    {
        if (_controls is not { } controls || row < 0 || row >= Tabs[_tab].Rows.Count)
        {
            return;
        }

        var cell = Tabs[_tab].Rows[row];
        controls.Context = cell.Context;
        int index = IndexOfAction(controls, cell.Action);
        controls.Focus(index);
        if (column != Column.Stick)
        {
            controls.MoveSlot(KeysStickColumn.SlotOfOther(controls.FocusedBindings, column == Column.B ? 1 : 0));
        }
    }

    // The page's row shape off the section's own widgets, each falling back to the authored number
    // where the row is absent. ⚠ Two authored widths run past the plate. The list's 644 puts its
    // scrollbar at 844, and the Control B column's 174 ends at 815, against a plate ending at 797.
    // Both are clamped to the plate, which is what the page's own still shows.
    private KeysShape ReadShape(MenuLayoutScreen? screen)
    {
        var list = screen?.Widget(ListKey);
        var action = screen?.Widget("KB_T_COMMANDTITLE");
        var first = screen?.Widget("KB_T_CONTTITLEA");
        var second = screen?.Widget("KB_T_CONTTITLEB");
        var plate = screen?.Widget("KB_BACKGROUND");
        float plateX = plate?.Int("X", (int)PlateX) ?? PlateX;
        float plateWidth = plate != null && _host.Measure(plate.Art.Count > 0 ? plate.Art[0] : string.Empty) is { } size
            ? size.Width
            : PlateWidth;
        float right = plateX + plateWidth;
        float itemHeight = list?.Int("ItemHeight", (int)ItemHeight) ?? ItemHeight;
        var bar = OriginalOptionsChrome.StripArt(list?.Art ?? Array.Empty<string>(), 0, 1);
        var arrow = OriginalOptionsChrome.StripArt(list?.Art ?? Array.Empty<string>(), 1);
        var arrowSize = _chrome.StripSize(arrow, ArrowWidth, ArrowWidth);
        float controlBX = second?.Int("X", (int)ControlBX) ?? ControlBX;
        float controlAX = first?.Int("X", (int)ControlAX) ?? ControlAX;
        // Control A gives up the panel half the Stick column takes.
        float controlAWidth = Math.Min(
            first?.Int("Width", (int)ControlAWidth) ?? ControlAWidth,
            Math.Max(1f, StickX - ColumnGap - controlAX));
        return new KeysShape(
            list?.Int("X", (int)ListX) ?? ListX,
            list?.Int("Y", (int)ListY) ?? ListY,
            itemHeight,
            // The heading line naming the category takes the window's first row.
            Math.Max(1, (list?.Int("TotalDisplayed", WindowRows) ?? WindowRows) - 1),
            action?.Int("X", (int)ActionX) ?? ActionX,
            action?.Int("Width", (int)ActionWidth) ?? ActionWidth,
            controlAX,
            controlAWidth,
            StickX,
            StickWidth,
            controlBX,
            Math.Min(second?.Int("Width", (int)ControlBWidth) ?? ControlBWidth, Math.Max(1f, right - controlBX)),
            action?.Int("Y", (int)HeadY) ?? HeadY,
            right - arrowSize.Width,
            arrowSize.Width,
            arrowSize.Height,
            _chrome.StripSize(bar, arrowSize.Width, 11f).Height);
    }

    // The page's own rows. The tabs and the three buttons draw as plaques, the standing tab in the
    // depressed frame it never leaves. The focused control cell is marked with the focus box.
    private void ComposeRows(IReadOnlyList<OriginalRow> rows, int focus, BoardLayers layers)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Art != null && row.Kind == OriginalRowKind.TextButton)
            {
                int frame = row.Key == TabKey(_tab)
                    ? 3
                    : ComposedBoard.PlaqueFrame(row.Art.Frames, i == focus, i == _host.PressedRow);
                // A tab's label takes the page's own text pair rather than the paper plaque's.
                // The strip authors ColorActive as the description cream and ColorRollover as
                // the file-wide ACTIVE. Those two palette roles already carry exactly that.
                layers.Plaques.Add(new BoardPlaque(row.Art, row.X, row.Y, i, frame, row.Label,
                    i == focus || i == _host.PressedRow ? BoardInk.RowFocused : BoardInk.Row));
                continue;
            }

            if (row.Art != null && row.Kind == OriginalRowKind.Button)
            {
                layers.Plaques.Add(new BoardPlaque(row.Art, row.X, row.Y, i,
                    row.Enabled ? ComposedBoard.PlaqueFrame(row.Art.Frames, i == focus, i == _host.PressedRow) : 0,
                    string.Empty, BoardInk.LabelNormal));
                continue;
            }

            if (i == focus && row.Visible && row.Enabled)
            {
                layers.Fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 0, 0, 0, 0.10f));
                layers.Fills.Add(_host.FocusMark(row));
            }
        }
    }

    // The list's scrollbar, at the plate's own right edge rather than at the authored list width,
    // which stands past it. Nothing is drawn while the standing tab fits its window.
    private void ComposeBar(KeysShape page, int count, BoardLayers layers, MenuLayoutScreen screen)
    {
        if (count <= page.Rows)
        {
            return;
        }

        var list = screen.Widget(ListKey);
        var up = OriginalOptionsChrome.StripArt(list?.Art ?? Array.Empty<string>(), 1);
        var down = OriginalOptionsChrome.StripArt(list?.Art ?? Array.Empty<string>(), 2);
        var bar = OriginalOptionsChrome.StripArt(list?.Art ?? Array.Empty<string>(), 0, 1);
        float top = page.WindowY;
        float trackHeight = (page.Rows * page.ItemHeight) - (2f * page.ArrowHeight);
        float thumbY = ListWindow.ThumbYFor(top + page.ArrowHeight, trackHeight, page.ThumbHeight,
            Math.Clamp(_top, 0, count - page.Rows), count - page.Rows);
        if (up != null && down != null && bar != null
            && _host.Measure(up.Name) != null && _host.Measure(down.Name) != null && _host.Measure(bar.Name) != null)
        {
            layers.Pictures.Add(new BoardPicture(up, page.BarX, top));
            layers.Pictures.Add(new BoardPicture(down, page.BarX, top + (page.Rows * page.ItemHeight) - page.ArrowHeight));
            layers.Pictures.Add(new BoardPicture(bar, page.BarX, thumbY));
            return;
        }

        layers.Fills.Add(new BoardFill(page.BarX, top, page.ArrowWidth, page.Rows * page.ItemHeight, 255, 255, 255, 0.3f, Border: true));
        layers.Fills.Add(new BoardFill(page.BarX, thumbY, page.ArrowWidth, page.ThumbHeight, 255, 255, 255, 0.6f));
    }

    /// <summary>One row of a category tab: which keymap it belongs to and which action it names.
    /// The two travel together because the seven tabs are the original's action groups and this
    /// port holds three keymaps. One tab can therefore list rows from more than one of them.</summary>
    public readonly record struct ControlsTabRow(InputContext Context, InputAction Action);

    /// <summary>One category tab of the page: the word on its button and the rows it lists.</summary>
    public sealed record ControlsTab(string Name, IReadOnlyList<ControlsTabRow> Rows);

    // The page's row shape in authored pixels. It is the list's corner, its pitch, and the rows it
    // shows under the category heading. The four columns, the head line, and the scrollbar's own
    // column at the plate's right edge follow.
    private sealed record KeysShape(
        float ListX, float ListY, float ItemHeight, int Rows,
        float ActionX, float ActionWidth, float ControlAX, float ControlAWidth,
        float StickX, float StickWidth, float ControlBX, float ControlBWidth, float HeadY,
        float BarX, float ArrowWidth, float ArrowHeight, float ThumbHeight)
    {
        // The window's own first line, where the scrollbar stands. ⚠ Do not anchor the bar at
        // LineY(0, top). A scrolled-away first row's line sits above the window. The bar would then
        // slide up with the list instead of holding still under its thumb.
        public float WindowY => ListY + ItemHeight;

        // The category heading stands on the window's first line, so a row sits one line below it.
        public float LineY(int row, int top) => ListY + ((row - top + 1) * ItemHeight);
    }
}
