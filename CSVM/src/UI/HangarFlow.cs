using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;

namespace CSVM.UI;

/// <summary>The hangar's screens, in the order the original walks them.</summary>
public enum HangarScreen
{
    /// <summary>Start a new plane, or load a saved one to edit.</summary>
    PlaneSelection,
    Airframe,
    Engine,
    Armour,
    Guns,
    Hardpoints,
    Paint,

    /// <summary>The plane's name, which is also its identity in the store.</summary>
    Name,

    /// <summary>The itemised review and the Build action.</summary>
    Purchase,
}

/// <summary>How a hangar flow ended, or that it is still running.</summary>
public enum HangarExit
{
    /// <summary>Still on a screen.</summary>
    None,

    /// <summary>Backed out; the scratch plane was discarded unsaved.</summary>
    Cancelled,

    /// <summary>Committed; the scratch plane is in the store under
    /// <see cref="HangarFlow.BuiltPlaneName"/>.</summary>
    Built,
}

/// <summary>
/// One hangar screen, as the shell draws and drives it. Everything is plain text and plain
/// indices, so a page is engine-free and testable: the shell owns every Godot control. A page
/// edits <see cref="HangarFlow.Scratch"/> in place through <see cref="Step"/>, which is the
/// launchscreen's live-stepper idiom; <see cref="Accept"/> returning false hands the press back to
/// the flow, which advances to the next screen. The pick screens (airframe, engine) select on
/// <see cref="Accept"/> instead and leave <see cref="Step"/> inert (E49).
/// </summary>
public interface IHangarPage
{
    /// <summary>Which screen this page is.</summary>
    HangarScreen Screen { get; }

    /// <summary>The heading, from the screen's own langui string.</summary>
    string Title { get; }

    /// <summary>How many rows the page draws right now.</summary>
    int RowCount { get; }

    /// <summary>The row the cursor lands on when the flow arrives, 0 for most screens. A screen
    /// whose rows ARE the picks opens on its current pick, so confirming twice walks the flow
    /// through without rewriting anything (E49's double-enter idiom).</summary>
    int OpeningRow { get; }

    /// <summary>The picture the shell should draw beside the list right now, or null for none —
    /// which every screen without art, and any screen missing its extraction, simply is.</summary>
    HangarArt? Art { get; }

    /// <summary>A second, smaller picture for the focused row, or null for none. Only the paint
    /// screen's three decal rows have one: the decal's own tile out of the shipped sheet.</summary>
    HangarArt? RowArt(int row);

    /// <summary>Row <paramref name="row"/>'s text.</summary>
    string RowText(int row);

    /// <summary>The detail line under the list for the focused row, or "" for none.</summary>
    string Detail(int row);

    /// <summary>The plane the shell's totals row prices while <paramref name="row"/> is focused:
    /// the scratch plane on every build screen, and null where the row is an action rather than a
    /// plane, which hides the row (E50).</summary>
    CustomPlaneDef? TotalsPlane(int row);

    /// <summary>The horizontal stepper on the focused row. Returns whether anything changed.</summary>
    bool Step(int row, int dir);

    /// <summary>The confirm press on the focused row. Returns true when the page handled it;
    /// false lets the flow advance to the next screen.</summary>
    bool Accept(int row);

    /// <summary>The flow has just arrived on this screen, before its opening row is read. A page
    /// is built once and kept, so this is the only place it can act on the scratch plane it is
    /// being shown this time rather than the one it was built over.</summary>
    void Entered();
}

/// <summary>One picture a page asks the shell to draw: a decoded TGA (blueprint, icon, paint
/// preview) plus the caption under it. The page decodes; the shell owns the one Godot texture.</summary>
public sealed record HangarArt(TgaImage Image, string Caption);

/// <summary>
/// The Build Custom Plane flow: one scratch <see cref="CustomPlaneDef"/> walked through
/// <see cref="Order"/>, with back/next navigation and a commit at the end. Engine-free, so the
/// screen order, the cancel semantics and the scratch lifecycle test off engine; the launchscreen
/// is only its renderer and input source. Nothing is written until <see cref="Commit"/> succeeds,
/// which is what makes cancelling from any screen residue-free by construction.
/// </summary>
public sealed class HangarFlow
{
    /// <summary>The original's screen order (docs/org/hangar.md).</summary>
    public static readonly HangarScreen[] Order =
    {
        HangarScreen.PlaneSelection,
        HangarScreen.Airframe,
        HangarScreen.Engine,
        HangarScreen.Armour,
        HangarScreen.Guns,
        HangarScreen.Hardpoints,
        HangarScreen.Paint,
        HangarScreen.Name,
        HangarScreen.Purchase,
    };

    // The four hangar zones in the record's own order, as the vehicle defs spell them.
    private static readonly string[] ArmourZones = { "nose", "tail", "leftwing", "rightwing" };

    private readonly CustomPlaneStore _store;
    private readonly Dictionary<HangarScreen, IHangarPage> _pages = new();

    // Each airframe's stock armour allocations in units, read once from the vehicle defs.
    private readonly Dictionary<int, int[]> _stockArmour = new();

    /// <summary>Opens a flow over <paramref name="store"/>, reading its saved planes once for the
    /// plane-selection screen. <paramref name="strings"/>, <paramref name="dataRoot"/>,
    /// <paramref name="stockFits"/> and <paramref name="zrdrPath"/> may be null or empty; the
    /// affected labels, art and defaults then simply fall back or load empty.
    /// <paramref name="campaign"/> is null for both existing doors, keeping them wallet-free; only
    /// the cabin's Plane Construction (B13) passes one.</summary>
    public HangarFlow(CustomPlaneStore store, UiStrings strings, string? dataRoot = null,
        StockLoadouts? stockFits = null, string? zrdrPath = null, HangarCampaignContext? campaign = null,
        Random? nameRng = null)
    {
        _store = store;
        Strings = strings;
        NameRng = nameRng ?? new Random();
        DataRoot = dataRoot;
        StockFits = stockFits;
        ZrdrPath = zrdrPath;
        Campaign = campaign;
        Saved = store.List();
    }

    /// <summary>The langui table the screens title and label themselves from.</summary>
    public UiStrings Strings { get; }

    /// <summary>The generator the PLANENAME screen rolls names from. Handed in rather than drawn
    /// here so the flow stays engine-free: the session's own stream (<c>Rng.PlaneName</c>) lives
    /// behind Godot's generator, and a test wants a pinned seed and an exact name.</summary>
    public Random NameRng { get; }

    /// <summary>The folder <c>extracted/</c> sits in, or null when the caller has none. Pages
    /// resolve their TGAs under it and treat absence as no art.</summary>
    public string? DataRoot { get; }

    /// <summary>The stock-fit table the airframe-defaults ask reads its gun and hardpoint
    /// defaults back off, or null when the caller has none (those defaults then load empty).</summary>
    public StockLoadouts? StockFits { get; }

    /// <summary>The zrdr scope <see cref="PlaneStats"/> reads vehicle defs from, for the stock
    /// armour allocations; null or unreadable reads as armour defaults of 0.</summary>
    public string? ZrdrPath { get; }

    /// <summary>The campaign wallet this flow prices against, or null over the two existing doors
    /// (Instant Action's Build button, the top-level entry), which stay wallet-free by construction
    /// (PLAN-hangar Decision 2). Non-null only when the cabin's Plane Construction opened this
    /// flow (B13).</summary>
    public HangarCampaignContext? Campaign { get; }

    /// <summary>The airframe whose defaults the pending ask (langui 206) offers, or null when
    /// none is showing. Raised only by an explicit confirm on an airframe row that is not already
    /// the pick (E49); the airframe page renders it as a two-row confirm.</summary>
    public int? DefaultsAsk { get; private set; }

    /// <summary>String 206 with both names formatted in, captured when the ask was raised (so
    /// the plane-being-built name is the one it had then).</summary>
    public string DefaultsAskText { get; private set; } = string.Empty;

    /// <summary>The store's saved planes, the plane-selection roster. Re-read only by
    /// <see cref="DeleteSaved"/>: the other writer is this flow's own commit, which ends it.</summary>
    public IReadOnlyList<CustomPlaneDef> Saved { get; private set; }

    /// <summary>The plane being built. Replaced outright when the plane-selection screen starts a
    /// new build or loads a saved one; edited in place by every screen after that.</summary>
    public CustomPlaneDef Scratch { get; private set; } = new();

    /// <summary>The saved plane this flow opened to edit, or null for a new build. The commit
    /// writes over that file by design, so it is the one name the PLANENAME screen must not warn
    /// about overwriting.</summary>
    public string? EditingName { get; private set; }

    /// <summary>The screen showing.</summary>
    public HangarScreen Screen { get; private set; } = HangarScreen.PlaneSelection;

    /// <summary>The focused row on that screen.</summary>
    public int Row { get; private set; }

    /// <summary>The page drawing the current screen.</summary>
    public IHangarPage Page => PageFor(Screen);

    /// <summary>Whether the flow is still running, and how it ended if not.</summary>
    public HangarExit Exit { get; private set; }

    /// <summary>The name the commit saved under, once <see cref="Exit"/> is
    /// <see cref="HangarExit.Built"/>.</summary>
    public string? BuiltPlaneName { get; private set; }

    /// <summary>The refusal line a failed commit left, or "". Cleared by any navigation.</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>The persistent second stats line every hangar screen shows under its heading
    /// (PLAN-hangar Decision 10): the build's total price and its weight against the airframe's
    /// capacity, recomputed from <see cref="HangarEconomy.Price"/> on demand and flagged with the
    /// original's own word (langui 1227) when over. Which plane it prices is the page's to say
    /// (<see cref="IHangarPage.TotalsPlane"/>): the plane-selection screen prices the saved plane
    /// under the cursor, and "" hides the line where the focused row is not a plane at all.</summary>
    public string TotalsLine
    {
        get
        {
            if (Page.TotalsPlane(RowInPage()) is not { } plane)
            {
                return string.Empty;
            }

            var bill = HangarEconomy.Price(plane);
            string line = $"${bill.Total.Cost}   {bill.Total.Weight} / {bill.Capacity} lbs.";
            return bill.Total.Weight > bill.Capacity
                ? line + "   ⚠ " + Strings.Text(1227, "OVERWEIGHT")
                : line;
        }
    }

    /// <summary>Whether <see cref="TotalsLine"/> is over capacity, so the shell can colour the
    /// flag as well as print it.</summary>
    public bool TotalsOverweight =>
        Page.TotalsPlane(RowInPage()) is { } plane
        && HangarEconomy.Price(plane).Verdict == PurchaseVerdict.Overweight;

    /// <summary>Whether an airframe has been picked outright on the AIRFRAME screen. A new plane
    /// starts false, so nothing reads as chosen before the pilot chooses (E49): the model still
    /// carries airframe 0 underneath, but no row is ticked and the first confirm is a pick rather
    /// than an advance. Loading a saved plane starts true.</summary>
    public bool AirframeChosen { get; private set; }

    /// <summary>The heading string id for each screen, from the original's own tab labels.</summary>
    public static int TitleStringId(HangarScreen screen) => screen switch
    {
        HangarScreen.PlaneSelection => 1017,
        HangarScreen.Airframe => 1004,
        HangarScreen.Engine => 1005,
        HangarScreen.Armour => 1006,
        HangarScreen.Guns => 1007,
        HangarScreen.Hardpoints => 1008,
        HangarScreen.Paint => 1009,
        HangarScreen.Name => 1401,
        _ => 1010,
    };

    /// <summary>The fallback heading for a screen when the langui table is missing.</summary>
    public static string TitleFallback(HangarScreen screen) => screen switch
    {
        HangarScreen.PlaneSelection => "PLANE SELECTION",
        HangarScreen.Armour => "Armor",
        HangarScreen.Name => "PLANE NAME",
        _ => screen.ToString(),
    };

    /// <summary>D32's wing rule read backwards: the stock fit's authored pylons counted per
    /// wing through <see cref="Loadout.PylonFillOrder"/>'s interleaved halves (pylons 1-4 one
    /// wing, 5-8 the other).</summary>
    public static (int Left, int Right) StockWingCounts(HardpointSpec? stock)
    {
        int left = 0, right = 0;
        for (int i = 0; stock != null && i < Loadout.PylonFillOrder.Length; i++)
        {
            if (i >= stock.Count || i >= stock.Stock.Length
                || string.Equals(stock.Stock[i], LoadoutChoice.None, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Loadout.PylonFillOrder[i] <= 4)
            {
                left++;
            }
            else
            {
                right++;
            }
        }

        return (left, right);
    }

    /// <summary>Moves the row cursor, wrapping like every other launchscreen list.</summary>
    public bool Move(int dir)
    {
        int count = Page.RowCount;
        if (dir == 0 || count <= 1)
        {
            return false;
        }

        Message = string.Empty;
        Row = ((Row + dir) % count + count) % count;
        return true;
    }

    /// <summary>Applies the horizontal stepper to the focused row.</summary>
    public bool Step(int dir)
    {
        if (dir == 0)
        {
            return false;
        }

        Message = string.Empty;
        return Page.Step(ClampedRow(), dir);
    }

    /// <summary>The confirm press: the page first, then the flow's own advance to the next
    /// screen. On the last screen the page commits, so nothing advances past it.</summary>
    public bool Accept()
    {
        Message = string.Empty;
        int row = ClampedRow();
        if (Page.Accept(row))
        {
            return true;
        }

        return Advance();
    }

    /// <summary>The back press: the previous screen, or cancelling the flow from the first one.
    /// The scratch plane is dropped with it, and since nothing is written before
    /// <see cref="Commit"/>, that leaves no residue at all.</summary>
    public bool Back()
    {
        Message = string.Empty;
        int at = Array.IndexOf(Order, Screen);
        if (at <= 0)
        {
            Exit = HangarExit.Cancelled;
            return true;
        }

        Screen = Order[at - 1];
        Page.Entered();
        Row = Page.OpeningRow;
        ClampedRow();
        return true;
    }

    /// <summary>Moves to the next screen in <see cref="Order"/>, or does nothing on the last.</summary>
    public bool Advance()
    {
        int at = Array.IndexOf(Order, Screen);
        if (at < 0 || at >= Order.Length - 1)
        {
            return false;
        }

        Screen = Order[at + 1];
        Page.Entered();
        Row = Page.OpeningRow;
        ClampedRow();
        return true;
    }

    /// <summary>Saves the scratch plane and ends the flow, or refuses and leaves
    /// <see cref="Message"/> saying why. The gate is <see cref="HangarEconomy"/>'s verdict plus a
    /// name, in the original's own words; funds are never checked over the two IA/top-level doors
    /// (Decision 2). Over a campaign flow (<see cref="Campaign"/> non-null, B13) the same commit
    /// also refuses an unavailable airframe or an unaffordable total, and on success moves the
    /// money and records ownership. Returns whether the plane was saved.</summary>
    public bool Commit()
    {
        if (string.IsNullOrWhiteSpace(Scratch.Name))
        {
            Message = Strings.Text(203, "You must enter a name for your new plane.");
            return false;
        }

        var bill = HangarEconomy.Price(Scratch);
        if (bill.Verdict != PurchaseVerdict.Ok)
        {
            Message = Strings.Text(1182, "CAN'T PURCHASE:") + " " + (bill.Verdict == PurchaseVerdict.Overweight
                ? Strings.Text(1227, "OVERWEIGHT")
                : Strings.Text(1171, "No Engine Selected"));
            return false;
        }

        if (Campaign is { } campaign)
        {
            if (!campaign.IsAirframeAvailable(Scratch.Airframe))
            {
                Message = Strings.Text(1182, "CAN'T PURCHASE:").TrimEnd() +
                    " That airframe is not available yet.";
                return false;
            }

            if (!campaign.CanAfford(bill.Total.Cost))
            {
                Message = Strings.Text(1182, "CAN'T PURCHASE:").TrimEnd() + " " +
                    Strings.Text(1226, "INSUFFICIENT FUNDS");
                return false;
            }
        }

        _store.Save(Scratch);
        BuiltPlaneName = Scratch.Name;
        Exit = HangarExit.Built;
        Campaign?.Purchase(Scratch.Name, Scratch.Airframe, bill.Total.Cost);
        return true;
    }

    /// <summary>Removes a saved plane from the store and re-reads <see cref="Saved"/>: the
    /// original's Sell Plane (<c>ps_b_sellp</c>), free over the two wallet-free doors; over a
    /// campaign flow (B13) it is an actual sale, <see cref="Campaign"/>'s decoded price crediting
    /// the wallet, refused (nothing changed, <see cref="Message"/> says why) for a reward aircraft
    /// or below the two-plane floor. Cursor and scratch plane are untouched either way.</summary>
    public bool DeleteSaved(string name)
    {
        bool gone;
        if (Campaign is { } campaign)
        {
            if (campaign.IsSpecial(name))
            {
                Message = Strings.Text(704, "This plane cannot be sold.");
                return false;
            }

            if (!campaign.CanSell(name))
            {
                Message = Strings.Text(701, "You must keep at least two planes in your hangar.");
                return false;
            }

            gone = campaign.Sell(name);
        }
        else
        {
            gone = _store.Delete(name);
        }

        Saved = _store.List();
        ClampedRow();
        return gone;
    }

    /// <summary>Puts the cursor on a row, clamped into the page's current list. A page that
    /// changes how many rows it draws calls this so the cursor lands on something real.</summary>
    public void FocusRow(int row)
    {
        Row = Math.Max(0, row);
        ClampedRow();
    }

    /// <summary>Starts a fresh build (the plane-selection screen's New Plane row) with nothing
    /// committed: no airframe is chosen, so the airframe screen opens on a plain list with no row
    /// ticked and no question asked (E49). The defaults ask waits for the pilot's own pick.</summary>
    public void StartNewPlane()
    {
        Scratch = new CustomPlaneDef();
        EditingName = null;
        AirframeChosen = false;
        DefaultsAsk = null;
        DefaultsAskText = string.Empty;
    }

    /// <summary>Starts from a saved plane, as a copy: editing and abandoning it must not touch
    /// what is on disk, so the store's own canonical serialisation is the deep copy. No
    /// defaults ask: the plane already is what its builder chose, and its airframe is already
    /// chosen, so the screen opens on that row ticked.</summary>
    public void StartFromSaved(CustomPlaneDef saved)
    {
        Scratch = CustomPlaneStore.Deserialize(CustomPlaneStore.Serialize(saved)) ?? new CustomPlaneDef();
        EditingName = saved.Name;
        AirframeChosen = true;
        DefaultsAsk = null;
        DefaultsAskText = string.Empty;
    }

    /// <summary>The AIRFRAME screen's confirm on a row: picking an airframe that is not already
    /// the pick switches to it and raises the defaults ask, and returns true so the press stays on
    /// the screen. Confirming the row that already IS the pick returns false, which is what lets
    /// the flow advance (E49's double-enter idiom). A new plane's first confirm always picks, even
    /// on the airframe the model was carrying underneath.</summary>
    public bool PickAirframe(int airframe)
    {
        if (AirframeChosen && Scratch.Airframe == airframe)
        {
            return false;
        }

        int was = Scratch.Airframe;
        Scratch.Airframe = airframe;
        AirframeChosen = true;
        NormalisePattern(airframe);
        RaiseDefaultsAsk(airframe, was);
        return true;
    }

    /// <summary>Raises the airframe-defaults ask, string 206's own question: %1 is the new
    /// airframe, %2 the plane being built (its name, or its previous airframe's name while it
    /// has none). The cursor moves onto the confirm rows.</summary>
    public void RaiseDefaultsAsk(int airframe, int previousAirframe)
    {
        string plane = string.IsNullOrWhiteSpace(Scratch.Name) ? AirframeName(previousAirframe) : Scratch.Name;
        string text = Strings.Format(206, AirframeName(airframe), plane);
        DefaultsAskText = text.Length > 0 ? text
            : $"Do you want the default armor, engine, and guns for this new airframe ({AirframeName(airframe)})?  " +
              $"If you do not want to lose the changes you've made to the {plane} you were building, click Cancel.";
        DefaultsAsk = airframe;
        Row = 0;
    }

    /// <summary>Answers the pending ask: accepting loads the airframe's defaults into the
    /// scratch plane, declining keeps every current pick (the airframe switch itself already
    /// happened when the ask was raised). The cursor lands back on the chosen airframe.</summary>
    public void AnswerDefaultsAsk(bool loadDefaults)
    {
        if (DefaultsAsk is not { } airframe)
        {
            return;
        }

        DefaultsAsk = null;
        DefaultsAskText = string.Empty;
        if (loadDefaults)
        {
            LoadAirframeDefaults(airframe);
        }

        Row = Scratch.Airframe;
    }

    /// <summary>Loads an airframe's defaults into the scratch plane, the ask's accept arm: gun
    /// picks and per-wing hardpoint counts read back off the airframe's stock fit, engine id 1
    /// (the stock Lvl-2 tier), armour the stock zone allocations in units. Paint and name are
    /// not the airframe's to default and stay as they are.</summary>
    public void LoadAirframeDefaults(int airframe)
    {
        Scratch.Airframe = airframe;
        Scratch.Engine = 1;
        var fit = StockFits?.ForModel(PlanePickerRoster.AirframeNode(airframe));
        for (int slot = 0; slot < CustomPlaneDef.GunSlots; slot++)
        {
            Scratch.Guns[slot] = default;
        }

        if (fit != null)
        {
            // The A3 mapping read backwards: stock caliber 30..70 is calibre row 0..4, and a
            // two-marker slot is the twin mount (one gun over both firepoints).
            foreach (var gun in fit.Guns)
            {
                if (gun.Slot is >= 1 and <= CustomPlaneDef.GunSlots)
                {
                    Scratch.Guns[gun.Slot - 1] = new GunChoice((gun.Caliber - 30) / 10, gun.Markers.Count >= 2);
                }
            }
        }

        (Scratch.LeftHardpoints, Scratch.RightHardpoints) = StockWingCounts(fit?.Hardpoints);
        int[] armour = StockArmourUnits(airframe);
        Scratch.ArmourNose = armour[0];
        Scratch.ArmourTail = armour[1];
        Scratch.ArmourLeftWing = armour[2];
        Scratch.ArmourRightWing = armour[3];
        Scratch.Clamp();
    }

    /// <summary>The airframe's own name (langui 3000 + id).</summary>
    public string AirframeName(int airframe) =>
        Strings.Text(3000 + airframe, $"Airframe {airframe}");

    /// <summary>The engine's name for an airframe (langui 3100 + airframe*6 + id), or the
    /// no-engine line for id 6.</summary>
    public string EngineName(int airframe, int engine) =>
        engine == CustomPlaneDef.EngineNone
            ? Strings.Text(1171, "No Engine Selected")
            : Strings.Text(3100 + (airframe * 6) + engine, $"Engine {engine}");

    /// <summary>One gun slot's pick as the original names it: the calibre's own name (langui
    /// 3310 + calibre), prefixed "(2) " when twinned (format 506), or None for an empty slot.</summary>
    public string GunName(GunChoice gun)
    {
        if (gun.Calibre is not { } calibre)
        {
            return Strings.Text(1165, "None");
        }

        string name = Strings.Text(3310 + calibre, $".{30 + (calibre * 10)}-cal.");
        if (!gun.Twin)
        {
            return name;
        }

        string prefix = Strings.Format(506, 2);
        return (prefix.Length == 0 ? "(2)" : prefix.TrimEnd()) + " " + name;
    }

    // The page for a screen, built on first sight and kept, so a page may hold state of its own.
    // Every screen's page is real and lives in its own file; the placeholder survives only as
    // the default arm's guard.
    private IHangarPage PageFor(HangarScreen screen)
    {
        if (!_pages.TryGetValue(screen, out var page))
        {
            page = screen switch
            {
                HangarScreen.PlaneSelection => new HangarPlaneSelectionPage(this),
                HangarScreen.Airframe => new HangarAirframePage(this),
                HangarScreen.Engine => new HangarEnginePage(this),
                HangarScreen.Armour => new HangarArmourPage(this),
                HangarScreen.Guns => new HangarGunsPage(this),
                HangarScreen.Hardpoints => new HangarHardpointsPage(this),
                HangarScreen.Paint => new HangarPaintPage(this),
                HangarScreen.Name => new HangarNamePage(this),
                HangarScreen.Purchase => new HangarPurchasePage(this),
                _ => new HangarPlaceholderPage(this, screen),
            };
            _pages[screen] = page;
        }

        return page;
    }

    // A pattern the picked airframe may not wear snaps to its first available one, carrying that
    // pattern's own colour/shade defaults: a fresh scratch holds pattern 0 (blackhat), which most
    // airframes' availability masks exclude, and the paint screen must never open on a paint job
    // the plane cannot wear.
    private void NormalisePattern(int airframe)
    {
        var tables = HangarPaintTables.Default;
        if (tables.Available(Scratch.PaintPattern, airframe))
        {
            return;
        }

        for (int pattern = 0; pattern < HangarPaintTables.PatternCount; pattern++)
        {
            if (tables.Available(pattern, airframe))
            {
                Scratch.LoadPatternDefaults(pattern);
                return;
            }
        }
    }

    // The airframe's stock zone allocations in units (the destroyable_parts armour pools / 5,
    // docs/formats/vehicle.md), read through PlaneStats off the zrdr scope and cached. No
    // reachable extraction reads as all zeros, the missing-data idiom every hangar source uses.
    private int[] StockArmourUnits(int airframe)
    {
        if (_stockArmour.TryGetValue(airframe, out var units))
        {
            return units;
        }

        units = new int[ArmourZones.Length];
        if (ZrdrPath is { } zrdr)
        {
            try
            {
                foreach (var part in PlaneStats.Load(zrdr, PlanePickerRoster.AirframeNode(airframe)).DestroyableParts)
                {
                    int zone = Array.IndexOf(ArmourZones, part.Name.ToLowerInvariant());
                    if (zone >= 0)
                    {
                        units[zone] = (int)Math.Round(part.MaxArmor / CustomPlaneBuild.ArmourUnitScale);
                    }
                }
            }
            catch (Exception)
            {
                // An unreadable extraction is the same as none; the zeros stand.
            }
        }

        _stockArmour[airframe] = units;
        return units;
    }

    // The focused row, kept inside the page's current list: a page whose row count shrank under
    // the cursor (a shorter saved-plane roster, a screen swapped by a later item) must not index
    // past its own end.
    private int ClampedRow()
    {
        Row = Math.Clamp(Row, 0, Math.Max(0, Page.RowCount - 1));
        return Row;
    }

    // The same clamp for a reader that must not move the cursor while answering: the totals line
    // is drawn every frame, and a getter that writes Row would fight the pilot's own navigation.
    private int RowInPage() => Math.Clamp(Row, 0, Math.Max(0, Page.RowCount - 1));
}

/// <summary>
/// The base every hangar page shares: its flow, its scratch plane, its strings, and the heading
/// resolved from the screen's own langui id. A page overriding nothing but
/// <see cref="RowCount"/> and <see cref="RowText"/> is a legal screen that advances on confirm and
/// backs out on cancel, which is what the C21 placeholders are.
/// </summary>
public abstract class HangarPage : IHangarPage
{
    /// <summary>Binds the page to the flow whose scratch plane it edits.</summary>
    protected HangarPage(HangarFlow flow) => Flow = flow;

    /// <inheritdoc/>
    public abstract HangarScreen Screen { get; }

    /// <inheritdoc/>
    public virtual string Title =>
        Flow.Strings.Text(HangarFlow.TitleStringId(Screen), HangarFlow.TitleFallback(Screen));

    /// <inheritdoc/>
    public abstract int RowCount { get; }

    /// <inheritdoc/>
    public virtual int OpeningRow => 0;

    /// <inheritdoc/>
    public virtual HangarArt? Art => null;

    /// <summary>The flow this page belongs to.</summary>
    protected HangarFlow Flow { get; }

    /// <summary>The plane being built.</summary>
    protected CustomPlaneDef Scratch => Flow.Scratch;

    /// <inheritdoc/>
    public abstract string RowText(int row);

    /// <inheritdoc/>
    public virtual string Detail(int row) => string.Empty;

    /// <inheritdoc/>
    public virtual CustomPlaneDef? TotalsPlane(int row) => Scratch;

    /// <inheritdoc/>
    public virtual bool Step(int row, int dir) => false;

    /// <inheritdoc/>
    public virtual bool Accept(int row) => false;

    /// <inheritdoc/>
    public virtual void Entered()
    {
    }

    /// <inheritdoc/>
    public virtual HangarArt? RowArt(int row) => null;
}

/// <summary>
/// The flow's first screen: start a new plane, or load one of the store's saved planes to edit.
/// Confirming either seats the scratch plane and lets the flow advance, so the screens after this
/// one always have a plane to edit.
///
/// <para>It also removes planes, the role the original's Sell Plane button (<c>ps_b_sellp</c>)
/// fills on its own plane-selection screen; with no economy to sell into, ours deletes. The
/// gesture is two-stage rather than a stepper, because a stepper on a destructive action is one
/// stray nudge away from losing a build: a trailing row opens a second list whose every row reads
/// "Delete &lt;name&gt;", so the press that removes a plane names the plane it removes.</para>
/// </summary>
public sealed class HangarPlaneSelectionPage : HangarPage
{
    private bool _deleting;

    /// <summary>Binds the page to its flow.</summary>
    public HangarPlaneSelectionPage(HangarFlow flow)
        : base(flow)
    {
    }

    /// <inheritdoc/>
    public override HangarScreen Screen => HangarScreen.PlaneSelection;

    /// <inheritdoc/>
    public override int RowCount =>
        _deleting ? Flow.Saved.Count + 1 : Flow.Saved.Count + (Flow.Saved.Count > 0 ? 2 : 1);

    /// <inheritdoc/>
    public override string RowText(int row)
    {
        if (_deleting)
        {
            return row < Flow.Saved.Count ? "Delete " + Flow.Saved[row].Name : "Cancel";
        }

        if (row == 0)
        {
            return "New Plane";
        }

        return row <= Flow.Saved.Count ? Flow.Saved[row - 1].Name : "Delete a saved plane";
    }

    /// <inheritdoc/>
    public override string Detail(int row)
    {
        if (_deleting)
        {
            return row < Flow.Saved.Count
                ? $"Removes {Flow.Saved[row].Name} from the hangar for good"
                : "Keep every saved plane";
        }

        if (row == 0)
        {
            return "Build a plane from a bare airframe";
        }

        return row <= Flow.Saved.Count
            ? Flow.AirframeName(Flow.Saved[row - 1].Airframe)
            : "Remove a saved plane from the hangar";
    }

    /// <inheritdoc/>
    /// <remarks>E50: the totals row prices the saved plane under the cursor, so the roster reads
    /// as a hangar rather than as a list of names. Every other row here is an action (New Plane,
    /// the delete stage, Cancel) and has no plane to price, so the row hides: the scratch plane's
    /// own totals would be a stale figure from a build this screen has not started yet.</remarks>
    public override CustomPlaneDef? TotalsPlane(int row) =>
        !_deleting && row >= 1 && row <= Flow.Saved.Count ? Flow.Saved[row - 1] : null;

    /// <inheritdoc/>
    public override bool Accept(int row)
    {
        if (_deleting)
        {
            return AcceptDelete(row);
        }

        if (row > Flow.Saved.Count)
        {
            _deleting = true;
            Flow.FocusRow(0);
            return true; // the delete list is this screen's own stage, not the next screen
        }

        if (row == 0)
        {
            Flow.StartNewPlane();
        }
        else
        {
            Flow.StartFromSaved(Flow.Saved[row - 1]);
        }

        return false; // let the flow advance to the first build screen
    }

    // The delete list's own press: a Delete row removes that plane and the list stays up while
    // any remain, Cancel (and the last plane going) returns to the pick list. Which row Cancel is
    // has to be read BEFORE the delete, since the roster shrinks under it.
    private bool AcceptDelete(int row)
    {
        bool cancel = row >= Flow.Saved.Count;
        if (!cancel)
        {
            Flow.DeleteSaved(Flow.Saved[row].Name);
        }

        _deleting = !cancel && Flow.Saved.Count > 0;
        // Stay on a plane row rather than sliding onto Cancel when the list's last one went.
        Flow.FocusRow(_deleting ? Math.Min(row, Flow.Saved.Count - 1) : 0);
        return true;
    }
}

/// <summary>
/// A screen with no page of its own, which is only the switch's default arm now that every screen
/// has one: the right heading, a Continue row, and a summary of what the scratch plane currently
/// carries for that screen. It edits nothing, so a flow walked through it produces exactly the
/// plane the screens before it chose.
/// </summary>
public sealed class HangarPlaceholderPage : HangarPage
{
    private readonly HangarScreen _screen;

    /// <summary>Binds the page to its flow and the screen it stands in for.</summary>
    public HangarPlaceholderPage(HangarFlow flow, HangarScreen screen)
        : base(flow) => _screen = screen;

    /// <inheritdoc/>
    public override HangarScreen Screen => _screen;

    /// <inheritdoc/>
    public override int RowCount => 1;

    /// <inheritdoc/>
    public override string RowText(int row) => "Continue";

    /// <inheritdoc/>
    public override string Detail(int row) => Summary();

    // What the scratch plane carries for this screen right now, from the decoded strings and the
    // economy, so the placeholder shows real data rather than a stub line.
    private string Summary()
    {
        var bill = HangarEconomy.Price(Scratch);
        return _screen switch
        {
            HangarScreen.Airframe =>
                $"{Flow.AirframeName(Scratch.Airframe)}   ${bill.Airframe.Cost}   {bill.Airframe.Weight} lbs.",
            HangarScreen.Engine =>
                $"{Flow.EngineName(Scratch.Airframe, Scratch.Engine)}   ${bill.Engine.Cost}   {bill.Engine.Weight} lbs.",
            HangarScreen.Armour => ArmourSummary(),
            HangarScreen.Guns => GunSummary(),
            HangarScreen.Hardpoints =>
                Line(1176, "Left Wing", Scratch.LeftHardpoints) + "   " +
                Line(1177, "Right Wing", Scratch.RightHardpoints),
            _ => $"Pattern {Scratch.PaintPattern}",
        };
    }

    // One "<label>: <n>" line through its own langui format, falling back to the plain label when
    // the string table is missing.
    private string Line(int id, string label, int value)
    {
        string text = Flow.Strings.Format(id, value);
        return text.Length > 0 ? text : $"{label}: {value}";
    }

    // The four zones in the record's own order, each through its own langui line (1191-1194).
    private string ArmourSummary()
    {
        int[] units = { Scratch.ArmourNose, Scratch.ArmourTail, Scratch.ArmourLeftWing, Scratch.ArmourRightWing };
        string[] labels = { "Nose", "Tail", "Left Wing", "Right Wing" };
        var parts = new string[units.Length];
        for (int i = 0; i < units.Length; i++)
        {
            parts[i] = Line(1191 + i, labels[i], units[i]);
        }

        return string.Join("   ", parts);
    }

    // The four slots, each under the airframe's own slot title from the stat table.
    private string GunSummary()
    {
        var stats = HangarEconomy.Airframes[Scratch.Airframe];
        var parts = new string[CustomPlaneDef.GunSlots];
        for (int slot = 0; slot < parts.Length; slot++)
        {
            string title = Flow.Strings.Text(stats.SlotTitle(slot), $"Slot {slot + 1}");
            parts[slot] = $"{title}: {Flow.GunName(Scratch.Guns[slot])}";
        }

        return string.Join("   ", parts);
    }
}
