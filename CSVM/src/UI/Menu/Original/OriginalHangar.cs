using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using CSVM.Flight;

namespace CSVM.UI.Menu.Original;

/// <summary>The colours the plane-construction screens write in, read off their own rows: the
/// right page's authored text and title colours, the left page's white, and the tab bar's label
/// tail (the standing tab draws in its disabled colour).</summary>
public sealed record OriginalHangarInks(
    MenuLayoutColor Text, MenuLayoutColor Title, MenuLayoutColor Page,
    MenuLayoutColor TabLabel, MenuLayoutColor TabCurrent, MenuLayoutColor TabDepressed);

/// <summary>
/// The Original hangar over the shared <see cref="HangarFeature"/>: the PLANE NAME screen from
/// <c>[@PlaneName@]</c>, the Plane Construction hub from <c>[@PlaneConstruction@]</c> with one
/// of the six tab sections composed on its right page, the CONSTRUCTION TOTALS page from
/// <c>[@Purchase@]</c> and the INVENTORY from <c>[@Hangar@]</c>. The tab bar is the layout's own
/// seven <c>0x1100</c> edges: every tab is a sibling reachable from every other, READY TO PURCHASE
/// opens the totals, CANCEL drops the scratch plane and SELL PLANES opens the inventory. Each tab's
/// dropdowns bind to the feature's operations; a pick that changes the airframe raises the
/// defaults ask as a dialog. Entered wallet-free from Instant Action's Build Custom Plane and over
/// the wallet from the cabin's PLANE CONSTRUCTION. Remake-only until the tab bar is filmed: the
/// export wording, the standing tab drawn disabled, the open list, keyboard and pad focus.
/// </summary>
public sealed partial class OriginalShell
{
    /// <summary>The layout section the name screen is composed from.</summary>
    public const string PlaneNameSection = "PlaneName";

    /// <summary>The layout section the hub's chrome is composed from.</summary>
    public const string PlaneConstructionSection = "PlaneConstruction";

    /// <summary>The layout section the totals page is composed from.</summary>
    public const string PurchaseSection = "Purchase";

    /// <summary>The layout section the inventory is composed from.</summary>
    public const string InventorySection = "Hangar";

    /// <summary>The name screen's edit box.</summary>
    public const string NameFieldKey = "PN_E_NAME";

    /// <summary>The name screen's Load Default Configuration checkbox.</summary>
    public const string NameDefaultsKey = "PN_B_DEFAULT";

    /// <summary>The name screen's OK button, into the hub.</summary>
    public const string NameOkKey = "PN_B_OK";

    /// <summary>The name screen's Cancel button, dropping the build.</summary>
    public const string NameCancelKey = "PN_B_CANCEL";

    /// <summary>The hub's SELL PLANES button, into the inventory.</summary>
    public const string SellPlanesKey = "PX_B_Sell";

    /// <summary>The hub's READY TO PURCHASE button, into the totals page.</summary>
    public const string ReadyKey = "PX_B_Ready";

    /// <summary>The hub's CANCEL PURCHASE button, dropping the build.</summary>
    public const string CancelBuildKey = "PX_B_Cancel";

    /// <summary>The totals page's Purchase Now button, the commit.</summary>
    public const string PurchaseNowKey = "PUR_B_PURCHASE";

    /// <summary>The defaults ask's OK, loading the airframe's defaults.</summary>
    public const string AskOkKey = "ASK:OK";

    /// <summary>The defaults ask's Cancel, keeping every current pick.</summary>
    public const string AskCancelKey = "ASK:CANCEL";

    /// <summary>The inventory's plane dropdown; its items are keyed <c>HA_D_PILOTPLANE:&lt;index&gt;</c>.</summary>
    public const string InventoryPlanesKey = "HA_D_PILOTPLANE";

    /// <summary>The inventory's Sell button.</summary>
    public const string InventorySellKey = "HA_B_SELLP";

    /// <summary>The inventory's Export button, the campaign's verb, drawn disabled here.</summary>
    public const string InventoryExportKey = "HA_B_EXPORTP";

    /// <summary>The inventory's Done button, back to the hub.</summary>
    public const string InventoryDoneKey = "HA_B_DONE";

    /// <summary>The airframe tab's dropdown.</summary>
    public const string AirframeDropKey = "AF_D_AIRFRAME";

    /// <summary>The engine tab's dropdown.</summary>
    public const string EngineDropKey = "EN_D_ENGINE";

    /// <summary>The paint tab's pattern dropdown.</summary>
    public const string PatternDropKey = "PT_D_PATTERN";

    // The plane picture's authored corner, the four PX_P_PLANE panes' own.
    private const float PlaneX = 16f;
    private const float PlaneY = 44f;

    // Where the typed name sits in the hub's name box, whose authored row carries no width.
    private const float HubNameX = 120f;
    private const float HubNameY = 12f;

    // Text sizes against the authored 15-pixel item height and the page's own text rows.
    private const float HubTitleFont = 16f;
    private const float HubTextFont = 12f;
    private const float HubItemFont = 12f;
    private const float HubLabelFont = 14f;
    private const float DescFont = 11f;
    private const float DescLine = 14f;

    // A dropdown's fallback item height where the layout row is missing.
    private const float FallbackHubItemHeight = 15f;

    // The defaults ask as a dialog over the page: a panel in the message box's own proportions.
    private const float AskX = 160f;
    private const float AskY = 200f;
    private const float AskWidth = 480f;
    private const float AskHeight = 180f;

    private static readonly Regex FontTag = new(@"\[[A-Za-z0-9]+\]", RegexOptions.Compiled);

    private static readonly string[] RatingWords = { "Poor", "Fair", "Average", "Good", "Excellent" };

    private static readonly (OriginalScreen Screen, string Key, string Section)[] Tabs =
    {
        (OriginalScreen.HangarAirframe, "PX_B_AIRFRAME", "AirFrame"),
        (OriginalScreen.HangarEngine, "PX_B_ENGINE", "Engine"),
        (OriginalScreen.HangarArmor, "PX_B_ARMOR", "Armor"),
        (OriginalScreen.HangarGuns, "PX_B_GUNS", "Guns"),
        (OriginalScreen.HangarHardpoints, "PX_B_HARDPOINTS", "HardPoints"),
        (OriginalScreen.HangarPaint, "PX_B_PAINT", "Paint"),
    };

    private readonly HangarFeature? _hangar;
    private readonly CustomPlaneStore? _planes;
    private OriginalScreen _hangarReturn = OriginalScreen.TopLevel;
    private OriginalScreen _hangarTab = OriginalScreen.HangarAirframe;
    private string _hangarName = string.Empty;
    private bool _hangarDefaults = true;
    private string? _hangarOpen;
    private int _hangarListTop;
    private int _inventoryIndex;
    private string? _builtPlane;

    /// <summary>The colours the plane-construction screens write in.</summary>
    public OriginalHangarInks HangarInks { get; private set; } = new(
        new MenuLayoutColor(255, 0, 0, 0), new MenuLayoutColor(255, 0x26, 0x1E, 0x47), new MenuLayoutColor(255, 255, 255, 255),
        new MenuLayoutColor(255, 255, 255, 255), new MenuLayoutColor(255, 0x37, 0x27, 0x7A), new MenuLayoutColor(255, 0x37, 0x27, 0x7A));

    /// <summary>Whether the screen showing is one of the hangar's: the name screen, a tab, the
    /// totals page or the inventory.</summary>
    public bool IsHangarScreen => _screen >= OriginalScreen.PlaneName;

    /// <summary>Whether seat 0's typed characters feed a text field right now: the name screen's
    /// edit box, and the campaign roster's name box while no dialog stands over it.</summary>
    public bool CapturingText =>
        _screen == OriginalScreen.PlaneName || (_screen == OriginalScreen.CampaignRoster && _dialog == null);

    /// <summary>The name typed on the name screen so far.</summary>
    public string HangarName => _hangarName;

    /// <summary>Whether the name screen's Load Default Configuration box is checked.</summary>
    public bool LoadDefaultsChecked => _hangarDefaults;

    /// <summary>The open hangar dropdown's key, or null when none is open.</summary>
    public string? OpenHangarDropdown => _hangarOpen;

    /// <summary>The inventory's picked saved plane, an index into the feature's roster.</summary>
    public int InventoryIndex => _inventoryIndex;

    /// <summary>The name the last completed build saved under, or null when none has this session.</summary>
    public string? LastBuiltPlane => _builtPlane;

    /// <summary>The tab the hub last showed, which SELL PLANES and Back on the totals page return to.</summary>
    public OriginalScreen HangarTab => _hangarTab;

    private bool IsHangarTab => _screen >= OriginalScreen.HangarAirframe && _screen <= OriginalScreen.HangarPaint;

    private bool IsHub => IsHangarTab || _screen == OriginalScreen.HangarPurchase;

    /// <summary>Opens the hangar from the screen showing: a build over the saved-plane store,
    /// wallet-free from Instant Action's Build Custom Plane and over <paramref name="wallet"/> from
    /// the cabin's PLANE CONSTRUCTION, entered through the name screen as the original's own chain
    /// does. The screen the door was pressed on is where CANCEL and a commit return to. Nothing
    /// happens when the shell has no feature or no store.</summary>
    public void OpenHangar(IHangarWallet? wallet = null)
    {
        if (_hangar == null || _planes == null)
        {
            return;
        }

        // Over a wallet the build store is the campaign's own, so ownership and the file it names
        // cannot end up in two different directories when a suite or an aid seats a scratch store.
        _hangar.Open(wallet != null ? _campaign?.Planes ?? _planes : _planes, wallet);
        _hangarReturn = IsHangarScreen ? OriginalScreen.TopLevel : _screen;
        _hangarName = string.Empty;
        _hangarDefaults = true;
        _hangarOpen = null;
        _hangarTab = OriginalScreen.HangarAirframe;
        Open(OriginalScreen.PlaneName);
    }

    /// <summary>Opens a hangar tab directly on a default-configuration build named
    /// <paramref name="name"/>, the screenshot aids' door: the name screen's OK with the box
    /// checked, then the tab, over <paramref name="wallet"/> when the shot wants the campaign's
    /// cash note. Nothing happens without a feature or a store.</summary>
    public void OpenHangarTab(OriginalScreen tab, string name, IHangarWallet? wallet = null)
    {
        if (_hangar == null || _planes == null)
        {
            return;
        }

        OpenHangar(wallet);
        _hangarName = name;
        AcceptName();
        if (tab != OriginalScreen.HangarAirframe)
        {
            ShowHangarScreen(tab);
        }
    }

    private static string Fill(string text, params object[] args)
    {
        string words = FontTag.Replace(text, string.Empty).Replace("\r", string.Empty).Replace("\n", " ").Trim();
        for (int i = 0; i < args.Length; i++)
        {
            string value = Convert.ToString(args[i], CultureInfo.InvariantCulture) ?? string.Empty;
            words = words.Replace($"%{i + 1}!d!", value).Replace($"%{i + 1}!s!", value);
        }

        return words;
    }

    private static string Rating(int stars) => RatingWords[Math.Clamp(stars, 0, RatingWords.Length - 1)];

    private static OriginalHangarInks ReadHangarInks(MenuLayout layout)
    {
        var hub = layout.Screen(PlaneConstructionSection);
        var airframe = layout.Screen("AirFrame");
        var black = new MenuLayoutColor(255, 0, 0, 0);
        var white = new MenuLayoutColor(255, 255, 255, 255);
        var purple = new MenuLayoutColor(255, 0x37, 0x27, 0x7A);
        var text = airframe?.Widget("AF_T_AIRFRAME");
        var title = airframe?.Widget("AF_T_TITLE");
        var page = hub?.Widget("PX_T_PLANENAME");
        var tab = hub?.Widget(Tabs[0].Key);
        return new OriginalHangarInks(
            text != null && text.TryColor("Color", out var t) ? t : black,
            title != null && title.TryColor("Color", out var h) ? h : new MenuLayoutColor(255, 0x26, 0x1E, 0x47),
            page != null && page.TryColor("Color", out var p) ? p : white,
            tab != null && tab.TryColor("ColorActive", out var a) ? a : white,
            tab != null && tab.TryColor("ColorDisabled", out var d) ? d : purple,
            tab != null && tab.TryColor("ColorDepressed", out var e) ? e : purple);
    }

    private static string ListKey(string key, int index) => key + ":" + index.ToString(CultureInfo.InvariantCulture);

    private static (float X, float Y, float Width, float Height) HubBox(MenuLayoutWidget widget) =>
        (widget.Int("X"), widget.Int("Y"), widget.Int("Width", (int)FallbackDropWidth), widget.Int("ItemHeight", (int)FallbackHubItemHeight));

    private static BoardTint Tint(PaintColour colour) => new(colour.R, colour.G, colour.B);

    // The screen a tab key names, or null for a key that is not a tab.
    private static OriginalScreen? TabOf(string key)
    {
        foreach (var tab in Tabs)
        {
            if (tab.Key == key)
            {
                return tab.Screen;
            }
        }

        return null;
    }

    private static string SectionOf(OriginalScreen screen)
    {
        foreach (var tab in Tabs)
        {
            if (tab.Screen == screen)
            {
                return tab.Section;
            }
        }

        return screen == OriginalScreen.HangarPurchase ? PurchaseSection : PlaneConstructionSection;
    }

    private static int? Indexed(string key, string prefix)
    {
        if (!key.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        return int.TryParse(key.AsSpan(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : null;
    }

    private static int IndexOf(IReadOnlyList<int> values, int value)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    // Which numbered dropdown of a family the focus is on or in, or null.
    private static int? FocusedSlot(IReadOnlyList<OriginalRow> rows, int focus, string prefix)
    {
        if (focus < 0 || focus >= rows.Count)
        {
            return null;
        }

        string key = rows[focus].Key;
        int colon = key.IndexOf(':');
        return Indexed(colon > 0 ? key[..colon] : key, prefix);
    }

    // The paper buttons whose authored label colours are the page's black, not the tabs' white.
    private static bool IsPaperButton(string key) =>
        key is PurchaseNowKey or InventorySellKey or InventoryExportKey or AskOkKey or AskCancelKey;

    // The wallet mark on every priced row the funds could not cover after that pick, baked into
    // the item texts so the closed box, the open list and the focus read the same row.
    private static void MarkUnaffordable(HangarFeature hangar, string[] items, Func<int, int> costWith)
    {
        if (hangar.Wallet == null)
        {
            return;
        }

        for (int i = 0; i < items.Length; i++)
        {
            if (hangar.Unaffordable(costWith(i)))
            {
                items[i] = HangarFeature.UnaffordableMark + items[i];
            }
        }
    }

    // A rating's bar: the five authored segment panes, the first n drawn for n stars.
    private static void ComposeBar(MenuLayoutScreen hub, List<BoardPicture> pictures, string prefix, int filled)
    {
        for (int i = 0; i < filled && i < 5; i++)
        {
            if (hub.Widget(prefix + i.ToString(CultureInfo.InvariantCulture)) is { Art.Count: > 0 } segment)
            {
                pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, segment.Art[0]), segment.Int("X"), segment.Int("Y")));
            }
        }
    }

    // The scroll-text box as a panel: its authored back and border colours, the lines inside.
    private static void ComposeDescription(MenuLayoutWidget widget, IReadOnlyList<string> description, List<BoardFill> fills, List<BoardLine> lines)
    {
        float x = widget.Int("X");
        float y = widget.Int("Y");
        float width = widget.Int("Width", 300);
        float height = widget.Int("Height", 150);
        if (widget.TryColor("BackColor", out var back))
        {
            fills.Add(new BoardFill(x, y, width, height, back.R, back.G, back.B));
        }

        if (widget.TryColor("BorderColor", out var border))
        {
            fills.Add(new BoardFill(x, y, width, height, border.R, border.G, border.B, Border: true));
        }

        for (int i = 0; i < description.Count; i++)
        {
            lines.Add(new BoardLine(description[i], x + 6f, y + 4f + (i * DescLine), width - 12f, DescFont, BoardInk.Row, -1, true));
        }
    }

    private static void PurchaseLine(MenuLayoutScreen page, List<BoardLine> lines, string key, string name, CostWeight line)
    {
        if (page.Widget(key) is { } item)
        {
            lines.Add(new BoardLine(name, item.Int("X"), item.Int("Y"), item.Int("Width"), HubTextFont, BoardInk.Row));
        }

        PurchaseFigure(page, lines, key + "WEIGHT", line.Weight.ToString(CultureInfo.InvariantCulture));
        PurchaseFigure(page, lines, key + "COST", "$" + line.Cost.ToString(CultureInfo.InvariantCulture));
    }

    private static void PurchaseFigure(MenuLayoutScreen page, List<BoardLine> lines, string key, string text)
    {
        if (page.Widget(key) is { } figure)
        {
            lines.Add(new BoardLine(text, figure.Int("X"), figure.Int("Y"), figure.Int("Width"), HubTextFont, BoardInk.Row, -1, false, Justify(figure)));
        }
    }

    // One text list of the totals page and its two figure columns, spaced by the list's own item
    // height and spacing.
    private static void PurchaseList(MenuLayoutScreen page, List<BoardLine> lines, string key, IReadOnlyList<(string Name, CostWeight Line)> items)
    {
        var list = page.Widget(key + "LIST");
        var weights = page.Widget(key + "WEIGHT");
        var costs = page.Widget(key + "COST");
        if (list == null)
        {
            return;
        }

        float pitch = list.Int("Height", 16) + list.Int("ItemSpacing", 1);
        for (int i = 0; i < items.Count; i++)
        {
            // One line per item, unwrapped: the list's pitch is one row, so a wrapped name would
            // overwrite the row under it.
            float dy = i * pitch;
            lines.Add(new BoardLine(items[i].Name, list.Int("X"), list.Int("Y") + dy, 0f, DescFont, BoardInk.Row));
            if (weights != null)
            {
                lines.Add(new BoardLine(items[i].Line.Weight.ToString(CultureInfo.InvariantCulture), weights.Int("X"), weights.Int("Y") + dy,
                    weights.Int("Width"), HubTextFont, BoardInk.Row, -1, false, Justify(weights)));
            }

            if (costs != null)
            {
                lines.Add(new BoardLine("$" + items[i].Line.Cost.ToString(CultureInfo.InvariantCulture), costs.Int("X"), costs.Int("Y") + dy,
                    costs.Int("Width"), HubTextFont, BoardInk.Row, -1, false, Justify(costs)));
            }
        }
    }

    private static void InventoryLine(MenuLayoutScreen screen, List<BoardLine> lines, string key, string text, float size)
    {
        if (screen.Widget(key) is { } widget)
        {
            lines.Add(new BoardLine(text, widget.Int("X"), widget.Int("Y"), widget.Int("Width"), size, BoardInk.Row));
        }
    }

    private static void AddPane(MenuLayoutScreen screen, List<BoardPicture> pictures, string key)
    {
        if (screen.Widget(key) is { Art.Count: > 0 } pane)
        {
            pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, pane.Art[0], Math.Max(1, pane.Frames)), pane.Int("X"), pane.Int("Y")));
        }
    }

    // A text row at its authored place in a chosen ink; the hangar's pages author black text the
    // shared reader would otherwise draw in the heading colour.
    private static void AddHangarText(MenuLayoutScreen screen, List<BoardLine> lines, string key, float size, BoardInk ink)
    {
        if (screen.Widget(key) is not { } widget || string.IsNullOrEmpty(widget.Text))
        {
            return;
        }

        lines.Add(new BoardLine(Fill(widget.Text), widget.Int("X"), widget.Int("Y"), widget.Int("Width"), size,
            IsWhite(widget) ? BoardInk.Dialog : ink, -1, false, Justify(widget)));
    }

    // Enters one of the hub's screens: the tabs remember themselves for the inventory's Done and
    // the totals page's Back, and no list stays open across the change.
    private void ShowHangarScreen(OriginalScreen screen)
    {
        _hangarOpen = null;
        if (screen >= OriginalScreen.HangarAirframe && screen <= OriginalScreen.HangarPaint)
        {
            _hangarTab = screen;
        }

        Open(screen);
    }

    // The name screen's OK: the typed name onto a bare or default-configuration build, then the
    // first tab. An empty name is refused in the screen's own words (langui 203).
    private void AcceptName()
    {
        if (_hangar == null || string.IsNullOrWhiteSpace(_hangarName))
        {
            return;
        }

        if (_hangarDefaults)
        {
            _hangar.StartDefaultPlane();
        }
        else
        {
            _hangar.StartNewPlane();
        }

        _hangar.Scratch.Name = _hangarName.Trim();
        ShowHangarScreen(OriginalScreen.HangarAirframe);
    }

    // Leaves the hangar without saving: the scratch plane is dropped and the screen the door was
    // pressed on comes back.
    private void CancelHangar()
    {
        _hangar?.Discard();
        _hangarOpen = null;
        ReturnFromHangar();
    }

    // The commit: on success the roster both presentations pick from gains the plane at once, the
    // scratch build is dropped and the entry screen comes back; a refusal stays with its reason.
    private void PurchaseNow()
    {
        if (_hangar == null || !_hangar.Commit())
        {
            return;
        }

        _builtPlane = _hangar.BuiltPlaneName;
        RefreshRosterFromStore();
        _hangar.Discard();
        ReturnFromHangar();
    }

    // Back onto the entry screen. The cabin re-reads its profile on the way, so a purchase or a
    // sale through the wallet shows on it; the Instant Action screen re-reads its Pilot Plane
    // list, so a build saved here is offered without leaving it.
    private void ReturnFromHangar()
    {
        if (_hangarReturn == OriginalScreen.CampaignCabin)
        {
            ResumeCampaign();
            return;
        }

        if (_hangarReturn == OriginalScreen.InstantAction)
        {
            RefreshInstantActionRoster();
        }

        Open(_hangarReturn);
    }

    private void RefreshRosterFromStore()
    {
        if (_planes == null)
        {
            return;
        }

        _setup.SetRoster(OriginalRosters.Roster(_planes.List()));
        foreach (var seat in _setup.Seats)
        {
            seat.Cursor = Math.Clamp(seat.Cursor, 0, Math.Max(0, _setup.Roster.Count - 1));
        }
    }

    // Typed characters and Backspace into whichever edit box is showing, each box's own rule: the
    // hangar name's character set and cap on the name screen, the campaign name's on the roster.
    // A taken character cues the edit box's keystroke sound and a refused one its reject sound,
    // the two wavs the globals script binds to the box.
    private bool TypeName(MenuCommands commands, List<string> cues)
    {
        if (_screen == OriginalScreen.CampaignRoster)
        {
            return TypeRosterName(commands, cues);
        }

        if (_screen != OriginalScreen.PlaneName)
        {
            return false;
        }

        bool changed = false;
        foreach (char c in commands.Typed)
        {
            if (HangarFeature.AcceptsNameChar(c) && _hangarName.Length < HangarFeature.MaxNameLength)
            {
                _hangarName += c;
                changed = true;
                cues.Add(OriginalCues.Text);
            }
            else
            {
                cues.Add(OriginalCues.TextError);
            }
        }

        if (commands.Erase && _hangarName.Length > 0)
        {
            _hangarName = _hangarName[..^1];
            changed = true;
        }

        return changed;
    }

    private bool CloseHangarDropdown()
    {
        if (_hangarOpen == null)
        {
            return false;
        }

        string key = _hangarOpen;
        _hangarOpen = null;
        FocusKey(key);
        return true;
    }

    // Back inside the hangar: an open list closes, the totals page and the inventory return to
    // the tab, a tab or the name screen cancels the build.
    private MenuExit? BackHangar()
    {
        if (CloseHangarDropdown())
        {
            return null;
        }

        if (_hangar?.DefaultsAsk != null)
        {
            _hangar.AnswerDefaultsAsk(false);
            return null;
        }

        if (_screen is OriginalScreen.HangarPurchase or OriginalScreen.HangarInventory)
        {
            ShowHangarScreen(_hangarTab);
            return null;
        }

        CancelHangar();
        return null;
    }

    // A sideways step inside the hangar: on a dropdown it picks the next value with wrap, on the
    // tab bar and the buttons it walks along them; a text field or a checkbox takes none.
    private bool StepHangarSideways(IReadOnlyList<OriginalRow> rows, int focus, int direction)
    {
        if (focus < 0 || focus >= rows.Count)
        {
            return false;
        }

        var row = rows[focus];
        if (row.Kind == OriginalRowKind.Dropdown && HangarListFor(row.Key) is { } list && list.Items.Count > 0)
        {
            int next = ((list.Current + direction) % list.Items.Count + list.Items.Count) % list.Items.Count;
            if (next != list.Current)
            {
                list.Select(next);
            }

            FocusKey(row.Key);
            return true;
        }

        if (row.Kind is not (OriginalRowKind.Button or OriginalRowKind.TextButton))
        {
            return false;
        }

        int i = focus;
        for (int n = 0; n < rows.Count; n++)
        {
            i = (i + direction + rows.Count) % rows.Count;
            if (rows[i].Enabled && rows[i].Kind is OriginalRowKind.Button or OriginalRowKind.TextButton)
            {
                _focus[(int)_screen] = i;
                return true;
            }
        }

        return true;
    }

    private void BuildHangarRows(List<OriginalRow> rows)
    {
        if (_hangar == null)
        {
            return;
        }

        switch (_screen)
        {
            case OriginalScreen.PlaneName:
                BuildPlaneNameRows(rows);
                break;
            case OriginalScreen.HangarInventory:
                if (!BuildOpenHangarList(rows))
                {
                    BuildInventoryRows(rows);
                }

                break;
            default:
                if (_hangar.DefaultsAsk != null)
                {
                    var size = PlaqueSize();
                    rows.Add(TextButton(AskOkKey, "OK", AskX + 40f, AskY + AskHeight - size.Height - 16f, true, 0));
                    rows.Add(TextButton(AskCancelKey, "Cancel", AskX + AskWidth - size.Width - 40f, AskY + AskHeight - size.Height - 16f, true, 0));
                    break;
                }

                if (!BuildOpenHangarList(rows))
                {
                    BuildHubRows(rows);
                }

                break;
        }
    }

    private void BuildPlaneNameRows(List<OriginalRow> rows)
    {
        var screen = _layout.Screen(PlaneNameSection);
        if (screen == null)
        {
            return;
        }

        if (screen.Widget(NameFieldKey) is { } field)
        {
            rows.Add(new OriginalRow(NameFieldKey, _hangarName, OriginalRowKind.TextField, field.Int("X"), field.Int("Y"),
                field.Int("Width", 200), field.Int("Height", 16), true, 0, null));
        }

        if (screen.Widget(NameDefaultsKey) is { } box)
        {
            var art = StripArt(box.Art, 0, box.Frames);
            var size = StripSize(art, 16f, 16f);
            rows.Add(new OriginalRow(NameDefaultsKey, string.Empty, OriginalRowKind.Radio, box.Int("X"), box.Int("Y"),
                size.Width, size.Height, true, 0, art));
        }

        AddStrip(screen, rows, NameOkKey, OriginalRowKind.TextButton, !string.IsNullOrWhiteSpace(_hangarName), 0);
        AddStrip(screen, rows, NameCancelKey, OriginalRowKind.TextButton, true, 0);
    }

    // The hub's rows in focus order: the page's dropdowns (or the totals page's Purchase Now),
    // then the six tabs with the standing one disabled, then SELL PLANES, READY and CANCEL.
    private void BuildHubRows(List<OriginalRow> rows)
    {
        var hub = _layout.Screen(PlaneConstructionSection);
        if (_screen == OriginalScreen.HangarPurchase)
        {
            if (_layout.Screen(PurchaseSection) is { } purchase)
            {
                AddStrip(purchase, rows, PurchaseNowKey, OriginalRowKind.TextButton, _hangar!.CanCommit, 0);
            }
        }
        else if (_layout.Screen(SectionOf(_screen)) is { } page)
        {
            foreach (var widget in page.Widgets)
            {
                if (widget.TypeCode == "D" && HangarListFor(widget.Key) is { } list)
                {
                    var box = HubBox(widget);
                    string value = list.Current >= 0 && list.Current < list.Items.Count ? list.Items[list.Current] : string.Empty;
                    rows.Add(new OriginalRow(widget.Key, value, OriginalRowKind.Dropdown, box.X, box.Y, box.Width, box.Height,
                        true, 0, StripArt(widget.Art, 4)));
                }
            }
        }

        if (hub == null)
        {
            return;
        }

        foreach (var tab in Tabs)
        {
            AddStrip(hub, rows, tab.Key, OriginalRowKind.TextButton, tab.Screen != _screen, 0);
        }

        AddStrip(hub, rows, SellPlanesKey, OriginalRowKind.Button, true, 0);
        AddStrip(hub, rows, ReadyKey, OriginalRowKind.Button, _screen != OriginalScreen.HangarPurchase, 0);
        AddStrip(hub, rows, CancelBuildKey, OriginalRowKind.Button, true, 0);
        if (_hangar!.Wallet == null)
        {
            // The wallet-free door wears the export strips the stills show on the Instant Action
            // path (READY TO EXPORT, CANCEL EXPORT), when the extraction carries them.
            ExportVariant(rows, ReadyKey, "PX_B_ReadyToExport.png");
            ExportVariant(rows, CancelBuildKey, "PX_B_CancelExport.png");
        }
    }

    private void ExportVariant(List<OriginalRow> rows, string key, string art)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Key == key && rows[i].Art is { } strip && Measure(art) != null)
            {
                rows[i] = rows[i] with { Art = strip with { Name = art } };
            }
        }
    }

    private void BuildInventoryRows(List<OriginalRow> rows)
    {
        var screen = _layout.Screen(InventorySection);
        if (screen == null || _hangar == null)
        {
            return;
        }

        if (screen.Widget(InventoryPlanesKey) is { } planes && HangarListFor(InventoryPlanesKey) is { } list)
        {
            var box = HubBox(planes);
            string value = list.Current >= 0 && list.Current < list.Items.Count ? list.Items[list.Current] : string.Empty;
            rows.Add(new OriginalRow(InventoryPlanesKey, value, OriginalRowKind.Dropdown, box.X, box.Y, box.Width, box.Height,
                list.Items.Count > 0, 0, StripArt(planes.Art, 4)));
        }

        AddStrip(screen, rows, InventorySellKey, OriginalRowKind.TextButton, _hangar.Saved.Count > 0, 0);
        AddStrip(screen, rows, InventoryExportKey, OriginalRowKind.TextButton, false, 0);
        AddStrip(screen, rows, InventoryDoneKey, OriginalRowKind.Button, true, 0);
    }

    // An open list's rows: every item keyed <key>:<index> under the box, the ones outside the
    // authored window unseen and unhit, the window following the focused item, and the list's own
    // arrows on its right edge while there is more list that way.
    private bool BuildOpenHangarList(List<OriginalRow> rows)
    {
        if (_hangarOpen == null)
        {
            return false;
        }

        string section = _screen == OriginalScreen.HangarInventory ? InventorySection : SectionOf(_screen);
        if (_layout.Screen(section)?.Widget(_hangarOpen) is not { } widget || HangarListFor(_hangarOpen) is not { } list)
        {
            _hangarOpen = null;
            return false;
        }

        var box = HubBox(widget);
        int count = list.Items.Count;
        int window = Math.Clamp(widget.Int("TotalDisplayed", count), 1, Math.Max(1, count));
        int focused = _focus[(int)_screen];
        if (focused >= 0 && focused < count)
        {
            if (focused < _hangarListTop)
            {
                _hangarListTop = focused;
            }
            else if (focused >= _hangarListTop + window)
            {
                _hangarListTop = focused - window + 1;
            }
        }

        _hangarListTop = Math.Clamp(_hangarListTop, 0, Math.Max(0, count - window));
        for (int i = 0; i < count; i++)
        {
            bool visible = i >= _hangarListTop && i < _hangarListTop + window;
            rows.Add(new OriginalRow(ListKey(_hangarOpen, i), list.Items[i], OriginalRowKind.ListRow,
                box.X, box.Y + (box.Height * (i - _hangarListTop + 1)), box.Width, box.Height, true, 0, null, visible));
        }

        if (window < count)
        {
            var up = StripArt(widget.Art, 3);
            var down = StripArt(widget.Art, 4);
            var upSize = StripSize(up, FallbackArrowWidth, FallbackArrowHeight);
            var downSize = StripSize(down, FallbackArrowWidth, FallbackArrowHeight);
            rows.Add(new OriginalRow(_hangarOpen + ":up", string.Empty, OriginalRowKind.Button,
                box.X + box.Width, box.Y + box.Height, upSize.Width, upSize.Height, _hangarListTop > 0, 0, up));
            rows.Add(new OriginalRow(_hangarOpen + ":down", string.Empty, OriginalRowKind.Button,
                box.X + box.Width, box.Y + (box.Height * (window + 1)) - downSize.Height, downSize.Width, downSize.Height,
                _hangarListTop + window < count, 0, down));
        }

        return true;
    }

    // The hangar's lists for the pointer: an open dropdown's list alone while one stands.
    private void HangarLists(List<OriginalList> lists)
    {
        if (_hangarOpen != null && OpenHangarListWindow() is { } window)
        {
            lists.Add(new OriginalList(_hangarOpen, window, ScrollHangarList));
        }
    }

    // The open list's window under its box, the thumb between its two arrows on the right edge;
    // null while the items fit the authored window.
    private ListWindow? OpenHangarListWindow()
    {
        if (_hangarOpen == null)
        {
            return null;
        }

        string section = _screen == OriginalScreen.HangarInventory ? InventorySection : SectionOf(_screen);
        if (_layout.Screen(section)?.Widget(_hangarOpen) is not { } widget || HangarListFor(_hangarOpen) is not { } list)
        {
            return null;
        }

        var box = HubBox(widget);
        int count = list.Items.Count;
        int window = Math.Clamp(widget.Int("TotalDisplayed", count), 1, Math.Max(1, count));
        if (count <= window)
        {
            return null;
        }

        var upSize = StripSize(StripArt(widget.Art, 3), FallbackArrowWidth, FallbackArrowHeight);
        var downSize = StripSize(StripArt(widget.Art, 4), FallbackArrowWidth, FallbackArrowHeight);
        var thumb = StripSize(StripArt(widget.Art, 0, 1), upSize.Width, 11f);
        float top = box.Y + box.Height;
        float height = window * box.Height;
        float trackHeight = height - upSize.Height - downSize.Height;
        int first = Math.Clamp(_hangarListTop, 0, count - window);
        return new ListWindow(
            box.X, top, box.Width + upSize.Width, height,
            box.X + box.Width, ListWindow.ThumbYFor(top + upSize.Height, trackHeight, thumb.Height, first, count - window), thumb.Width, thumb.Height,
            top + upSize.Height, trackHeight, count, window, first);
    }

    // Puts the open list's window at top; a focused item the move would hide is pulled to the
    // window's nearer edge, since the window otherwise follows the focus back.
    private void ScrollHangarList(int top)
    {
        if (_hangarOpen == null || HangarListFor(_hangarOpen) is not { } list || OpenHangarListWindow() is not { } window)
        {
            return;
        }

        _hangarListTop = Math.Clamp(top, 0, window.LastTop);
        int focused = _focus[(int)_screen];
        if (focused >= 0 && focused < list.Items.Count)
        {
            _focus[(int)_screen] = Math.Clamp(focused, _hangarListTop, _hangarListTop + window.Rows - 1);
        }
    }

    // One dropdown's list over the feature: its items, the standing pick, what a pick does, and
    // for the paint colours and decals the swatch or tile a row draws instead of words.
    private HangarList? HangarListFor(string key)
    {
        if (_hangar is not { } hangar)
        {
            return null;
        }

        var scratch = hangar.Scratch;
        switch (key)
        {
            case AirframeDropKey:
                var names = new string[HangarEconomy.Airframes.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    names[i] = hangar.AirframeName(i);
                }

                MarkUnaffordable(hangar, names, hangar.CostWithAirframe);
                return new HangarList(names, hangar.AirframeChosen ? scratch.Airframe : -1, i => hangar.PickAirframe(i));
            case EngineDropKey:
                var engines = new string[CustomPlaneDef.EngineNone + 1];
                for (int i = 0; i < engines.Length; i++)
                {
                    engines[i] = i == CustomPlaneDef.EngineNone ? hangar.Strings.Text(1165, "None") : hangar.EngineName(scratch.Airframe, i);
                }

                MarkUnaffordable(hangar, engines, hangar.CostWithEngine);
                return new HangarList(engines, scratch.Engine, i => hangar.SetEngine(i));
            case PatternDropKey:
                var patterns = hangar.WearablePatterns();
                var labels = new string[patterns.Count];
                for (int i = 0; i < labels.Length; i++)
                {
                    labels[i] = hangar.PatternLabel(patterns[i]);
                }

                return new HangarList(labels, IndexOf(patterns, scratch.PaintPattern), i => hangar.SetPattern(patterns[i]));
            case InventoryPlanesKey:
                var saved = new string[hangar.Saved.Count];
                for (int i = 0; i < saved.Length; i++)
                {
                    saved[i] = hangar.Saved[i].Name;
                }

                _inventoryIndex = Math.Clamp(_inventoryIndex, 0, Math.Max(0, saved.Length - 1));
                return new HangarList(saved, saved.Length == 0 ? -1 : _inventoryIndex, i => _inventoryIndex = i);
        }

        if (Indexed(key, "AR_D_POINT") is { } zone && zone < 4)
        {
            var units = new string[CustomPlaneDef.MaxArmourUnits + 1];
            for (int i = 0; i < units.Length; i++)
            {
                units[i] = hangar.ArmourLabel(i);
            }

            MarkUnaffordable(hangar, units, i => hangar.CostWithArmour(zone, i));
            return new HangarList(units, hangar.ArmourUnits(zone), i => hangar.SetArmour(zone, i));
        }

        if (Indexed(key, "GN_D_GUN") is { } slot && slot < CustomPlaneDef.GunSlots)
        {
            var guns = new string[HangarFeature.GunCycleRows];
            for (int i = 0; i < guns.Length; i++)
            {
                guns[i] = hangar.GunCycleName(i);
            }

            MarkUnaffordable(hangar, guns, i => hangar.CostWithGun(slot, i));
            return new HangarList(guns, HangarFeature.GunCycleIndex(scratch.Guns[slot]), i => hangar.SetGun(slot, i));
        }

        if (Indexed(key, "HP_D_POINT") is { } wing && wing < 2)
        {
            var counts = new string[CustomPlaneDef.MaxHardpointsPerWing + 1];
            for (int i = 0; i < counts.Length; i++)
            {
                counts[i] = hangar.HardpointsLabel(i);
            }

            MarkUnaffordable(hangar, counts, i => hangar.CostWithHardpoints(wing, i));
            return new HangarList(counts, wing == 0 ? scratch.LeftHardpoints : scratch.RightHardpoints, i => hangar.SetHardpoints(wing, i));
        }

        var tables = HangarPaintTables.Default;
        if (Indexed(key, "PT_D_COLORS") is { } colourSlot && colourSlot < HangarPaintTables.Slots)
        {
            int rows = Math.Max(1, tables.Swatches.Count);
            var blanks = new string[rows];
            Array.Fill(blanks, string.Empty);
            return new HangarList(blanks, scratch.PaintColours[colourSlot], i => hangar.SetColour(colourSlot, i),
                Swatch: i => Tint(tables.Resolve(i, tables.DefaultShadeFor(i))));
        }

        if (Indexed(key, "PT_D_SHADES") is { } shadeSlot && shadeSlot < HangarPaintTables.Slots)
        {
            int colour = scratch.PaintColours[shadeSlot];
            int rows = Math.Max(1, tables.ShadeCount(colour));
            var blanks = new string[rows];
            Array.Fill(blanks, string.Empty);
            return new HangarList(blanks, scratch.PaintShades[shadeSlot], i => hangar.SetShade(shadeSlot, i),
                Swatch: i => Tint(tables.Resolve(colour, i)));
        }

        if (Indexed(key, "PT_D_DECALS") is { } decalSlot && decalSlot < HangarPaintTables.Slots)
        {
            var decals = new string[HangarPaintTables.DecalCount];
            for (int i = 0; i < decals.Length; i++)
            {
                string name = tables.DecalName(i);
                decals[i] = name.Length == 0 ? i.ToString("00", CultureInfo.InvariantCulture) : name;
            }

            return new HangarList(decals, hangar.Decal(decalSlot), i => hangar.SetDecal(decalSlot, i), Tile: i => i);
        }

        return null;
    }

    // Sell asks first, the sell path's own two-button messagebox (langui 700 over the plane's
    // short airframe name and its value, Yes and No, HANGAR.SCRIPT's 0x4 mask and so the query
    // icon), and a refused sale (a reward aircraft, the two-plane floor) comes back as the
    // one-button 0x1 box in the feature's words, under the warning.
    private void AskToSell()
    {
        if (_hangar == null || _inventoryIndex < 0 || _inventoryIndex >= _hangar.Saved.Count)
        {
            return;
        }

        var plane = _hangar.Saved[_inventoryIndex];
        string question = _hangar.Strings.Format(700, _hangar.AirframeShortName(plane.Airframe), HangarEconomy.Price(plane).Total.Cost);
        if (question.Length == 0)
        {
            question = $"Your {_hangar.AirframeShortName(plane.Airframe)} is worth ${HangarEconomy.Price(plane).Total.Cost}. Are you sure you want to sell it?";
        }

        RaiseDialog(
            Fill(question.Replace("<B>", string.Empty).Replace("<b>", string.Empty)),
            DialogIcon.Query,
            Yes(() =>
            {
                if (_hangar.DeleteSaved(plane.Name))
                {
                    RefreshRosterFromStore();
                    return;
                }

                string refusal = _hangar.Message;
                _hangar.ClearMessage();
                RaiseDialog(refusal, DialogIcon.Warning, Ok());
            }),
            No());
    }

    private MenuExit? ActivateHangar(OriginalRow row)
    {
        if (_hangar == null)
        {
            return null;
        }

        int colon = row.Key.IndexOf(':');
        if (colon > 0)
        {
            string prefix = row.Key[..colon];
            string suffix = row.Key[(colon + 1)..];
            switch (row.Key)
            {
                case AskOkKey:
                    _hangar.AnswerDefaultsAsk(true);
                    FocusKey(AirframeDropKey);
                    return null;
                case AskCancelKey:
                    _hangar.AnswerDefaultsAsk(false);
                    FocusKey(AirframeDropKey);
                    return null;
            }

            if (suffix == "up")
            {
                _hangarListTop--;
                return null;
            }

            if (suffix == "down")
            {
                _hangarListTop++;
                return null;
            }

            if (HangarListFor(prefix) is { } list)
            {
                list.Select(int.Parse(suffix, CultureInfo.InvariantCulture));
                _hangarOpen = null;
                // A pick that raised the defaults ask lands the focus on its OK; any other pick
                // lands back on the box it came from.
                FocusKey(_hangar.DefaultsAsk != null ? AskOkKey : prefix);
            }

            return null;
        }

        switch (row.Key)
        {
            case NameFieldKey:
                return null;
            case NameDefaultsKey:
                _hangarDefaults = !_hangarDefaults;
                return null;
            case NameOkKey:
                AcceptName();
                return null;
            case NameCancelKey:
            case CancelBuildKey:
                CancelHangar();
                return null;
            case SellPlanesKey:
                _hangarOpen = null;
                Open(OriginalScreen.HangarInventory);
                return null;
            case ReadyKey:
                ShowHangarScreen(OriginalScreen.HangarPurchase);
                return null;
            case PurchaseNowKey:
                PurchaseNow();
                return null;
            case InventorySellKey:
                AskToSell();
                return null;
            case InventoryDoneKey:
                ShowHangarScreen(_hangarTab);
                return null;
        }

        if (TabOf(row.Key) is { } tab)
        {
            ShowHangarScreen(tab);
            return null;
        }

        if (row.Kind == OriginalRowKind.Dropdown && HangarListFor(row.Key) is { } open && open.Items.Count > 0)
        {
            _hangarOpen = row.Key;
            _hangarListTop = 0;
            _focus[(int)_screen] = Math.Max(0, open.Current);
        }

        return null;
    }

    // The hangar screens as drawn.
    private void ComposeHangar(
        IReadOnlyList<OriginalRow> rows, int focus, List<BoardPicture> backdrop, List<BoardPicture> pictures,
        List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques, List<BoardPanel> overlays)
    {
        if (_hangar == null)
        {
            return;
        }

        switch (_screen)
        {
            case OriginalScreen.PlaneName:
                ComposePlaneName(rows, focus, backdrop, pictures, fills, lines, plaques);
                return;
            case OriginalScreen.HangarInventory:
                ComposeInventory(rows, focus, backdrop, pictures, fills, lines, plaques, overlays);
                return;
        }

        ComposeHubChrome(backdrop, pictures, lines);
        if (_screen == OriginalScreen.HangarPurchase)
        {
            ComposePurchasePage(lines);
        }
        else
        {
            ComposeTabPage(rows, focus, pictures, fills, lines);
        }

        // With a list or the ask up the rows are its own; the page under it is drawn from the
        // closed widgets, and the list becomes an overlay over the finished page.
        IReadOnlyList<OriginalRow> widgets = rows;
        int widgetFocus = focus;
        int widgetPressed = _pressed;
        bool covered = _hangarOpen != null || _hangar.DefaultsAsk != null;
        if (covered)
        {
            var closed = new List<OriginalRow>();
            BuildHubRows(closed);
            widgets = closed;
            widgetFocus = -1;
            widgetPressed = -1;
            for (int i = 0; _hangarOpen != null && i < closed.Count; i++)
            {
                if (closed[i].Key == _hangarOpen)
                {
                    widgetFocus = i;
                }
            }
        }

        for (int i = 0; i < widgets.Count; i++)
        {
            ComposeHangarRow(widgets[i], i == widgetFocus, i == widgetPressed, i, fills, lines, plaques, pictures);
        }

        if (_hangarOpen != null)
        {
            ComposeOpenList(rows, focus, overlays);
        }
        else if (_hangar.DefaultsAsk != null)
        {
            ComposeAsk(rows, focus, overlays);
        }
    }

    private void ComposePlaneName(
        IReadOnlyList<OriginalRow> rows, int focus, List<BoardPicture> backdrop, List<BoardPicture> pictures,
        List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques)
    {
        var screen = _layout.Screen(PlaneNameSection);
        if (screen == null)
        {
            return;
        }

        AddPane(screen, backdrop, "PX_P_BACKGROUND");
        AddPane(screen, backdrop, "PN_P_BACKGROUND");
        AddHangarText(screen, lines, "PN_T_TITLE", HubLabelFont, BoardInk.Row);
        AddHangarText(screen, lines, "PN_T_DEFAULT", HubTextFont, BoardInk.Row);
        for (int i = 0; i < rows.Count; i++)
        {
            ComposeHangarRow(rows[i], i == focus, i == _pressed, i, fills, lines, plaques, pictures);
        }

        if (string.IsNullOrWhiteSpace(_hangarName) && screen.Widget(NameFieldKey) is { } field
            && rows.Count > 0 && rows[rows.Count - 1] is { } last)
        {
            // The screen's own refusal (langui 203), shown under the panel's buttons before OK is
            // pressed rather than after.
            lines.Add(new BoardLine(_hangar!.Strings.Text(203, "You must enter a name for your new plane."),
                field.Int("X"), last.Y + last.Height + 8f, 0f, DescFont, BoardInk.Dialog));
        }
    }

    // The hub's own frame: the page background, the plane on the blueprint, the name and cost
    // over it, the cash note over a wallet, and the airframe figures under the picture.
    private void ComposeHubChrome(List<BoardPicture> backdrop, List<BoardPicture> pictures, List<BoardLine> lines)
    {
        var hub = _layout.Screen(PlaneConstructionSection);
        if (hub == null || _hangar is not { } hangar)
        {
            return;
        }

        AddPane(hub, backdrop, "PX_P_BACKGROUND");
        ComposePlanePicture(pictures);

        var scratch = hangar.Scratch;
        var bill = hangar.Bill;
        AddHangarText(hub, lines, "PX_T_PLANENAME", HubLabelFont, BoardInk.Dialog);
        lines.Add(new BoardLine(scratch.Name, HubNameX, HubNameY, 0f, HubLabelFont, BoardInk.Dialog));
        if (hub.Widget("PX_T_PLANECOST") is { } cost)
        {
            lines.Add(new BoardLine(Fill(cost.Text ?? "PLANE COST:  $%1!d!", bill.Total.Cost), cost.Int("X"), cost.Int("Y"), 0f, HubLabelFont, BoardInk.Dialog));
        }

        if (hangar.Wallet is { } wallet)
        {
            // The cash note's two authored rows, on every tab and the totals page alike; the figure
            // takes the problems ink once the build outruns it, the mark's own colour.
            AddHangarText(hub, lines, "PX_T_CASHTITLE", HubTextFont, BoardInk.Row);
            if (hub.Widget("PX_T_CASH") is { } cash)
            {
                lines.Add(new BoardLine("$" + wallet.Funds.ToString(CultureInfo.InvariantCulture), cash.Int("X"), cash.Int("Y"),
                    cash.Int("Width"), HubLabelFont, hangar.TotalUnaffordable() ? BoardInk.Heading : BoardInk.Row, -1, false, Justify(cash)));
            }
        }

        if (hub.Widget("PX_T_AIRFRAME") is { } airframe)
        {
            string name = hangar.AirframeChosen ? hangar.AirframeName(scratch.Airframe) : string.Empty;
            lines.Add(new BoardLine(Fill(airframe.Text ?? "AIRFRAME: %1!s!", name), airframe.Int("X"), airframe.Int("Y"),
                airframe.Int("Width"), HubTextFont, BoardInk.Dialog));
        }

        if (hub.Widget("PX_T_WEIGHTCAPACITY") is { } capacity)
        {
            lines.Add(new BoardLine(Fill(capacity.Text ?? "WEIGHT CAPACITY: %1!d! lbs.", bill.Capacity), capacity.Int("X"), capacity.Int("Y"),
                capacity.Int("Width"), HubTextFont, BoardInk.Dialog));
        }

        if (hub.Widget("PX_T_CURRENTWEIGHT") is { } weight)
        {
            string text = hangar.AirframeChosen
                ? Fill(weight.Text ?? "CURRENT WEIGHT: %1!d! lbs.", bill.Total.Weight)
                : Fill(weight.Text ?? "CURRENT WEIGHT: %1!d! lbs.", "Pending").Replace("Pending lbs.", "Pending");
            lines.Add(new BoardLine(text, weight.Int("X"), weight.Int("Y"), weight.Int("Width"), HubTextFont, BoardInk.Dialog));
        }

        if (hub.Widget("PX_T_AGILITY") is { } agility)
        {
            lines.Add(new BoardLine("AGILITY:  " + Rating(bill.AgilityStars), agility.Int("X"), agility.Int("Y"), 0f, HubTextFont, BoardInk.Dialog));
            ComposeBar(hub, pictures, "PX_P_AD", bill.AgilityStars);
        }

        if (hub.Widget("PX_T_ARMOR") is { } armour)
        {
            lines.Add(new BoardLine("ARMOR:  " + Rating(bill.ArmourStars), armour.Int("X"), armour.Int("Y"), 0f, HubTextFont, BoardInk.Dialog));
            ComposeBar(hub, pictures, "PX_P_SI", bill.ArmourStars);
        }
    }

    // The plane on the blueprint: the airframe tab shows the focused airframe's blueprint, every
    // other tab the paint composite, the three region masks tinted with the picked colours under
    // the detail plate, the pattern's own icon set. A pair with no set falls back to the blueprint.
    private void ComposePlanePicture(List<BoardPicture> pictures)
    {
        var hangar = _hangar!;
        int airframe = hangar.AirframeChosen ? hangar.Scratch.Airframe : FocusedAirframe();
        if (_screen == OriginalScreen.HangarAirframe || !hangar.AirframeChosen)
        {
            pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, $"PX_{FocusedAirframe()}_BLUEPRINT.TGA"), PlaneX, PlaneY));
            return;
        }

        var scratch = hangar.Scratch;
        string prefix = $"PX_ICON_{airframe}_{scratch.PaintPattern}_";
        if (Measure(prefix + "0.TGA") == null)
        {
            pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, $"PX_{airframe}_BLUEPRINT.TGA"), PlaneX, PlaneY));
            return;
        }

        PaintColour[] colours = { scratch.Colour1, scratch.Colour2, scratch.Colour3 };
        for (int slot = 0; slot < colours.Length; slot++)
        {
            pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, $"{prefix}{slot + 1}.TGA"), PlaneX, PlaneY, Tint: Tint(colours[slot])));
        }

        pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, prefix + "0.TGA"), PlaneX, PlaneY));
    }

    // The airframe the airframe tab previews: the focused list item while the list is open, else
    // the standing pick, else the first row.
    private int FocusedAirframe()
    {
        var hangar = _hangar!;
        if (_screen == OriginalScreen.HangarAirframe && _hangarOpen == AirframeDropKey)
        {
            int focus = _focus[(int)_screen];
            if (focus >= 0 && focus < HangarEconomy.Airframes.Length)
            {
                return focus;
            }
        }

        return hangar.AirframeChosen ? hangar.Scratch.Airframe : 0;
    }

    // One tab's right page: its title, rules and labels at their authored places, the name line
    // for the focused item and the description box with the decoded figures.
    private void ComposeTabPage(IReadOnlyList<OriginalRow> rows, int focus, List<BoardPicture> pictures, List<BoardFill> fills, List<BoardLine> lines)
    {
        var page = _layout.Screen(SectionOf(_screen));
        if (page == null || _hangar is not { } hangar)
        {
            return;
        }

        foreach (var widget in page.Widgets)
        {
            if (widget.TypeCode == "P" && widget.Art.Count > 0 && widget.Frames == 1 && widget.Key != "PT_P_DECALS")
            {
                pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, widget.Art[0]), widget.Int("X"), widget.Int("Y")));
            }
            else if (widget.TypeCode == "T" && widget.Key.EndsWith("_T_TITLE", StringComparison.Ordinal))
            {
                AddHangarText(page, lines, widget.Key, HubTitleFont, BoardInk.Heading);
            }
            else if (widget.TypeCode == "T" && widget.Text is { Length: > 0 })
            {
                AddHangarText(page, lines, widget.Key, HubLabelFont, BoardInk.Row);
            }
        }

        var scratch = hangar.Scratch;
        var bill = hangar.Bill;
        var stats = HangarEconomy.Airframes[scratch.Airframe];
        string name;
        var description = new List<string>();
        switch (_screen)
        {
            case OriginalScreen.HangarAirframe:
                int airframe = FocusedAirframe();
                var frame = HangarEconomy.Airframes[airframe];
                var bare = HangarEconomy.Price(new CustomPlaneDef { Airframe = airframe });
                name = hangar.AirframeName(airframe);
                description.Add($"COST: ${frame.Cost}");
                description.Add($"WEIGHT: {frame.Weight} lbs.");
                description.Add($"WEIGHT CAPACITY: {frame.Capacity} lbs.");
                description.Add($"AGILITY: {Rating(bare.AgilityStars)}");
                description.Add($"BASE ARMOR: {Rating(bare.ArmourStars)}");
                break;
            case OriginalScreen.HangarEngine:
                int engine = FocusedItem(rows, focus, EngineDropKey) ?? scratch.Engine;
                name = engine == CustomPlaneDef.EngineNone ? hangar.Strings.Text(1165, "None") : hangar.EngineName(scratch.Airframe, engine);
                var line = HangarEconomy.EngineLine(scratch.Airframe, engine);
                description.Add($"COST: ${line.Cost}");
                description.Add($"WEIGHT: {line.Weight} lbs.");
                if (engine != CustomPlaneDef.EngineNone)
                {
                    description.Add($"POWER: {HangarEconomy.PowerStat(scratch.Airframe, engine)}");
                }

                break;
            case OriginalScreen.HangarArmor:
                name = "ABOUT ARMOR";
                description.Add($"COST: ${HangarEconomy.ArmourUnitCost * 5}/5 units");
                description.Add($"WEIGHT: {HangarEconomy.ArmourUnitWeight * 5} lbs./5 units");
                description.Add($"TOTAL: ${bill.Armour.Cost}   {bill.Armour.Weight} lbs.");
                break;
            case OriginalScreen.HangarGuns:
                for (int slot = 0; slot < CustomPlaneDef.GunSlots; slot++)
                {
                    if (page.Widget("GN_T_GUNTITLE" + slot.ToString(CultureInfo.InvariantCulture)) is { } title)
                    {
                        lines.Add(new BoardLine(hangar.Strings.Text(stats.SlotTitle(slot), $"Slot {slot + 1}"), title.Int("X"), title.Int("Y"), 0f, HubLabelFont, BoardInk.Row));
                    }
                }

                int gunSlot = FocusedSlot(rows, focus, "GN_D_GUN") ?? 0;
                name = hangar.GunCycleName(HangarFeature.GunCycleIndex(scratch.Guns[gunSlot]));
                description.Add($"COST: ${bill.Guns[gunSlot].Cost}");
                description.Add($"WEIGHT: {bill.Guns[gunSlot].Weight} lbs.");
                break;
            case OriginalScreen.HangarHardpoints:
                int wing = FocusedSlot(rows, focus, "HP_D_POINT") ?? 0;
                name = hangar.HardpointsLabel(wing == 0 ? scratch.LeftHardpoints : scratch.RightHardpoints);
                description.Add($"COST: ${HangarEconomy.HardpointCost} each");
                description.Add($"WEIGHT: {HangarEconomy.HardpointWeight} lbs. each");
                description.Add($"TOTAL: ${bill.Hardpoints.Cost}   {bill.Hardpoints.Weight} lbs.");
                break;
            default:
                name = hangar.PatternLabel(scratch.PaintPattern);
                description.Add("COST: Free");
                break;
        }

        foreach (var widget in page.Widgets)
        {
            bool nameRow = widget.Key == "AF_T_AIRFRAME" || widget.Key.EndsWith("NAME", StringComparison.Ordinal);
            if (widget.TypeCode == "T" && string.IsNullOrEmpty(widget.Text) && nameRow)
            {
                lines.Add(new BoardLine(name, widget.Int("X"), widget.Int("Y"), widget.Int("Width"), HubLabelFont, BoardInk.Row));
            }
            else if (widget.TypeCode == "S")
            {
                ComposeDescription(widget, description, fills, lines);
            }
        }
    }

    // The item under the focus in an open list of the given dropdown, or null.
    private int? FocusedItem(IReadOnlyList<OriginalRow> rows, int focus, string key)
    {
        if (_hangarOpen != key || focus < 0 || focus >= rows.Count)
        {
            return null;
        }

        return Indexed(rows[focus].Key, key + ":");
    }

    // The totals page: the column heads, one line per priced component at the authored lines,
    // the totals row, and the problems text in the commit's own words.
    private void ComposePurchasePage(List<BoardLine> lines)
    {
        var page = _layout.Screen(PurchaseSection);
        if (page == null || _hangar is not { } hangar)
        {
            return;
        }

        foreach (string key in new[] { "PUR_T_TITLE", "PUR_T_ITEMTITLE", "PUR_T_WEIGHTTITLE", "PUR_T_COSTTITLE", "PUR_T_AIRFRAMETITLE", "PUR_T_ENGINETITLE", "PUR_T_ARMORTITLE", "PUR_T_GUNSTITLE", "PUR_T_HARDPOINTTITLE", "PUR_T_TOTALSTITLE" })
        {
            AddHangarText(page, lines, key, key == "PUR_T_TITLE" ? HubTitleFont : HubTextFont, key == "PUR_T_TITLE" ? BoardInk.Heading : BoardInk.Row);
        }

        var scratch = hangar.Scratch;
        var bill = hangar.Bill;
        var stats = HangarEconomy.Airframes[scratch.Airframe];
        PurchaseLine(page, lines, "PUR_T_AIRFRAME", hangar.AirframeName(scratch.Airframe), bill.Airframe);
        if (scratch.Engine != CustomPlaneDef.EngineNone)
        {
            PurchaseLine(page, lines, "PUR_T_ENGINE", hangar.EngineName(scratch.Airframe, scratch.Engine), bill.Engine);
        }

        var armour = new List<(string Name, CostWeight Line)>();
        for (int zone = 0; zone < 4; zone++)
        {
            int units = hangar.ArmourUnits(zone);
            if (units > 0)
            {
                string label = hangar.Strings.Format(1191 + zone, units * 5);
                armour.Add((label.Length > 0 ? label : $"Zone {zone + 1}: {units * 5} units",
                    new CostWeight(units * HangarEconomy.ArmourUnitCost, units * HangarEconomy.ArmourUnitWeight)));
            }
        }

        PurchaseList(page, lines, "PUR_T_ARMOR", armour);
        var guns = new List<(string Name, CostWeight Line)>();
        for (int slot = 0; slot < CustomPlaneDef.GunSlots; slot++)
        {
            if (!scratch.Guns[slot].IsEmpty)
            {
                // The gun's own name alone: the slot title would wrap the authored 220-pixel list
                // column onto the next row.
                guns.Add((hangar.GunName(scratch.Guns[slot]), bill.Guns[slot]));
            }
        }

        PurchaseList(page, lines, "PUR_T_GUN", guns);
        var hardpoints = new List<(string Name, CostWeight Line)>();
        for (int wing = 0; wing < 2; wing++)
        {
            int count = wing == 0 ? scratch.LeftHardpoints : scratch.RightHardpoints;
            if (count > 0)
            {
                string label = hangar.Strings.Format(1176 + wing, count);
                hardpoints.Add((label.Length > 0 ? label : $"{(wing == 0 ? "Left" : "Right")} Wing: {count}",
                    new CostWeight(count * HangarEconomy.HardpointCost, count * HangarEconomy.HardpointWeight)));
            }
        }

        PurchaseList(page, lines, "PUR_T_HARDPOINT", hardpoints);
        PurchaseFigure(page, lines, "PUR_T_TOTALSWEIGHT", bill.Total.Weight.ToString(CultureInfo.InvariantCulture));
        PurchaseFigure(page, lines, "PUR_T_TOTALSCOST", "$" + bill.Total.Cost.ToString(CultureInfo.InvariantCulture));
        string problems = hangar.Message.Length > 0 ? hangar.Message : hangar.Refusal() ?? string.Empty;
        if (problems.Length > 0 && page.Widget("PUR_T_PROBLEMS") is { } trouble)
        {
            lines.Add(new BoardLine(problems, trouble.Int("X"), trouble.Int("Y"), trouble.Int("Width"), HubTextFont, BoardInk.Heading));
        }
    }

    // The inventory: the page, its title and prompt, the plane dropdown, the picked plane's icon,
    // name, figures and guns, and the three buttons.
    private void ComposeInventory(
        IReadOnlyList<OriginalRow> rows, int focus, List<BoardPicture> backdrop, List<BoardPicture> pictures,
        List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques, List<BoardPanel> overlays)
    {
        var screen = _layout.Screen(InventorySection);
        if (screen == null || _hangar is not { } hangar)
        {
            return;
        }

        AddPane(screen, backdrop, "HA_BACKGROUND");
        AddHangarText(screen, lines, "HA_T_TITLE", HubTitleFont, BoardInk.Heading);
        AddHangarText(screen, lines, "HA_T_PROMPT", HubLabelFont, BoardInk.Row);
        if (_inventoryIndex >= 0 && _inventoryIndex < hangar.Saved.Count)
        {
            var plane = hangar.Saved[_inventoryIndex];
            var bill = HangarEconomy.Price(plane);
            if (screen.Widget("HA_P_PILOTPLANE") is { Art.Count: > 0 } icon)
            {
                pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, icon.Art[0], Math.Max(1, icon.Frames)),
                    icon.Int("X"), icon.Int("Y"), Math.Clamp(plane.Airframe, 0, Math.Max(0, icon.Frames - 1))));
            }

            InventoryLine(screen, lines, "HA_T_PILOTPLANE", plane.Name + "   " + hangar.AirframeName(plane.Airframe), HubLabelFont);
            InventoryLine(screen, lines, "HA_T_AGILITYP", "AGILITY: " + Rating(bill.AgilityStars), HubTextFont);
            InventoryLine(screen, lines, "HA_T_ARMORP", "ARMOR: " + Rating(bill.ArmourStars), HubTextFont);
            string value = hangar.Strings.Format(1258, bill.Total.Cost);
            InventoryLine(screen, lines, "HA_T_VALUEP", value.Length > 0 ? value : $"Value: ${bill.Total.Cost}", HubTextFont);
            if (screen.Widget("HA_A_PLANEWEAPONSP") is { } weapons)
            {
                float pitch = weapons.Int("Height", 16) + weapons.Int("ItemSpacing", 1);
                int shown = 0;
                for (int slot = 0; slot < CustomPlaneDef.GunSlots; slot++)
                {
                    if (!plane.Guns[slot].IsEmpty)
                    {
                        lines.Add(new BoardLine(hangar.GunName(plane.Guns[slot]), weapons.Int("X"), weapons.Int("Y") + (shown++ * pitch),
                            0f, DescFont, BoardInk.Row));
                    }
                }
            }
        }

        IReadOnlyList<OriginalRow> widgets = rows;
        int widgetFocus = focus;
        if (_hangarOpen != null)
        {
            var closed = new List<OriginalRow>();
            BuildInventoryRows(closed);
            widgets = closed;
            widgetFocus = 0;
        }

        for (int i = 0; i < widgets.Count; i++)
        {
            ComposeHangarRow(widgets[i], i == widgetFocus, _hangarOpen == null && i == _pressed, i, fills, lines, plaques, pictures);
        }

        if (_hangarOpen != null)
        {
            ComposeOpenList(rows, focus, overlays);
        }
    }

    // One hangar row as drawn: the edit box with its caret, the checkbox from its eight-state
    // strip, a dropdown's box with its value, swatch or tile, and the strips through the shared
    // plaque drawing. The paper buttons write their labels in the page's text ink, the tabs in
    // their own label tail.
    private void ComposeHangarRow(
        OriginalRow row, bool focused, bool pressed, int index,
        List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques, List<BoardPicture> pictures)
    {
        switch (row.Kind)
        {
            case OriginalRowKind.TextField:
                fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 255, 255, 255, 0.85f));
                fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 0x73, 0x69, 0x9C, 1f, Border: true));
                lines.Add(new BoardLine(row.Label + (focused ? "_" : string.Empty), row.X + 4f, row.Y + 1f, row.Width - 8f, HubItemFont, BoardInk.Row, index));
                return;
            case OriginalRowKind.Radio when row.Art != null:
                // An eight-state checkbox strip: the four button states unmarked, then the same four marked.
                int state = row.Enabled ? (pressed ? 3 : focused ? 2 : 1) : 0;
                plaques.Add(new BoardPlaque(row.Art, row.X, row.Y, index, (_hangarDefaults ? 4 : 0) + state, string.Empty, BoardInk.LabelNormal));
                return;
            case OriginalRowKind.Dropdown:
                ComposeHangarDropdown(row, focused, pressed, index, fills, lines, pictures);
                return;
            case OriginalRowKind.TextButton when row.Art != null && IsPaperButton(row.Key):
                int paperFrame = row.Enabled ? ComposedBoard.PlaqueFrame(row.Art.Frames, focused, pressed) : 0;
                plaques.Add(new BoardPlaque(row.Art, row.X, row.Y, index, paperFrame, row.Label, row.Enabled ? BoardInk.Row : BoardInk.Detail));
                return;
        }

        ComposeInstantActionRow(row, focused, pressed, index, fills, lines, plaques, pictures);
    }

    private void ComposeHangarDropdown(OriginalRow row, bool focused, bool pressed, int index, List<BoardFill> fills, List<BoardLine> lines, List<BoardPicture> pictures)
    {
        var list = HangarListFor(row.Key);
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
            int frame = row.Enabled ? ComposedBoard.PlaqueFrame(row.Art.Frames, focused, pressed) : 0;
            pictures.Add(new BoardPicture(row.Art, row.X + row.Width - size.Width, row.Y + ((row.Height - size.Height) / 2f), frame));
        }

        if (list != null && list.Current >= 0)
        {
            if (list.Swatch?.Invoke(list.Current) is { } swatch)
            {
                fills.Add(new BoardFill(row.X + 2f, row.Y + 2f, Math.Max(1f, row.Width - arrowWidth - 4f), row.Height - 4f, swatch.R, swatch.G, swatch.B));
                return;
            }

            if (list.Tile?.Invoke(list.Current) is { } tile && DecalArt() is { } sheet)
            {
                var tileSize = StripSize(sheet, 66f, 66f);
                pictures.Add(new BoardPicture(sheet, row.X + 2f, row.Y + ((row.Height - tileSize.Height) / 2f), tile));
                return;
            }
        }

        lines.Add(new BoardLine(row.Label, row.X + 4f, row.Y + 1f, Math.Max(1f, row.Width - arrowWidth - 6f), HubItemFont,
            focused ? BoardInk.RowFocused : BoardInk.Row, index));
    }

    // The decal sheet, the paint section's own fifty-frame pane.
    private BoardArt? DecalArt()
    {
        var pane = _layout.Screen("Paint")?.Widget("PT_P_DECALS");
        return pane is { Art.Count: > 0 } ? new BoardArt(BoardArtLibrary.Ui, pane.Art[0], Math.Max(1, pane.Frames)) : null;
    }

    // An open list as the overlay over the finished page: the visible items on a paper panel, the
    // focused one marked, swatches and tiles where the list has them, and the arrows.
    private void ComposeOpenList(IReadOnlyList<OriginalRow> rows, int focus, List<BoardPanel> overlays)
    {
        if (_hangarOpen == null || HangarListFor(_hangarOpen) is not { } list)
        {
            return;
        }

        var panelFills = new List<BoardFill>();
        var panelLines = new List<BoardLine>();
        var panelPictures = new List<BoardPicture>();
        float top = float.MaxValue, bottom = float.MinValue, left = 0f, width = 0f;
        foreach (var row in rows)
        {
            if (row.Visible && row.Kind == OriginalRowKind.ListRow)
            {
                top = Math.Min(top, row.Y);
                bottom = Math.Max(bottom, row.Y + row.Height);
                left = row.X;
                width = row.Width;
            }
        }

        if (top < bottom)
        {
            panelFills.Add(new BoardFill(left, top, width, bottom - top, 0xE6, 0xDA, 0xBE, 0.97f));
            panelFills.Add(new BoardFill(left, top, width, bottom - top, 0, 0, 0, 1f, Border: true));
        }

        var sheet = DecalArt();
        for (int i = 0; i < rows.Count; i++)
        {
            var item = rows[i];
            if (!item.Visible)
            {
                continue;
            }

            if (item.Kind == OriginalRowKind.Button && item.Art != null)
            {
                int frame = item.Enabled ? ComposedBoard.PlaqueFrame(item.Art.Frames, i == focus, i == _pressed) : 0;
                panelPictures.Add(new BoardPicture(item.Art, item.X, item.Y, frame));
                continue;
            }

            int index = Indexed(item.Key, _hangarOpen + ":") ?? -1;
            if (i == focus)
            {
                panelFills.Add(new BoardFill(item.X, item.Y, item.Width, item.Height, 0, 0, 0, 0.12f));
            }

            if (index >= 0 && list.Swatch?.Invoke(index) is { } swatch)
            {
                panelFills.Add(new BoardFill(item.X + 2f, item.Y + 2f, item.Width - 4f, item.Height - 4f, swatch.R, swatch.G, swatch.B));
                continue;
            }

            if (index >= 0 && list.Tile?.Invoke(index) is { } tile && sheet != null)
            {
                var tileSize = StripSize(sheet, 66f, 66f);
                panelPictures.Add(new BoardPicture(sheet, item.X + 2f, item.Y + ((item.Height - tileSize.Height) / 2f), tile));
                panelLines.Add(new BoardLine(item.Label, item.X + tileSize.Width + 6f, item.Y + 2f, Math.Max(1f, item.Width - tileSize.Width - 8f), DescFont, BoardInk.Row, i));
                continue;
            }

            panelLines.Add(new BoardLine(item.Label, item.X + 4f, item.Y + 1f, item.Width - 8f, HubItemFont,
                i == focus ? BoardInk.RowFocused : BoardInk.Row, i));
        }

        string section = _screen == OriginalScreen.HangarInventory ? InventorySection : SectionOf(_screen);
        if (OpenHangarListWindow() is { } window && _layout.Screen(section)?.Widget(_hangarOpen) is { } widget
            && StripArt(widget.Art, 0, 1) is { } thumb)
        {
            panelPictures.Add(new BoardPicture(thumb, window.ThumbX, window.ThumbY));
        }

        overlays.Add(new BoardPanel(panelFills, panelPictures, panelLines));
    }

    // The defaults ask as a dialog over the page: a panel carrying string 206 and the two rows.
    private void ComposeAsk(IReadOnlyList<OriginalRow> rows, int focus, List<BoardPanel> overlays)
    {
        var panelFills = new List<BoardFill>
        {
            new(AskX, AskY, AskWidth, AskHeight, 0xE6, 0xDA, 0xBE, 0.97f),
            new(AskX, AskY, AskWidth, AskHeight, 0, 0, 0, 1f, Border: true),
        };
        var panelLines = new List<BoardLine>
        {
            new(_hangar!.DefaultsAskText, AskX + 16f, AskY + 16f, AskWidth - 32f, HubTextFont, BoardInk.Row),
        };
        var panelPictures = new List<BoardPicture>();
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            bool focused = i == focus;
            if (row.Art != null)
            {
                // The plaque art rides the panel as a picture, its label written over it.
                panelPictures.Add(new BoardPicture(row.Art, row.X, row.Y, ComposedBoard.PlaqueFrame(row.Art.Frames, focused, i == _pressed)));
            }
            else
            {
                panelFills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 255, 255, 255, 0.6f, Border: true));
            }

            panelLines.Add(new BoardLine(row.Label, row.X, row.Y + 6f, row.Width, HubItemFont,
                focused ? BoardInk.RowFocused : BoardInk.Row, i, false, BoardJustify.Center));
        }

        overlays.Add(new BoardPanel(panelFills, panelPictures, panelLines));
    }

    // One dropdown's list, its standing pick, what picking one does, and the swatch or decal tile
    // a row draws in place of words where the list has one.
    private sealed record HangarList(
        IReadOnlyList<string> Items, int Current, Action<int> Select,
        Func<int, BoardTint?>? Swatch = null, Func<int, int?>? Tile = null);
}
