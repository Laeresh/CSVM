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
/// the flow, which advances to the next screen.
/// </summary>
public interface IHangarPage
{
    /// <summary>Which screen this page is.</summary>
    HangarScreen Screen { get; }

    /// <summary>The heading, from the screen's own langui string.</summary>
    string Title { get; }

    /// <summary>How many rows the page draws right now.</summary>
    int RowCount { get; }

    /// <summary>The picture the shell should draw under the list right now, or null for none —
    /// which every screen without art, and any screen missing its extraction, simply is.</summary>
    HangarArt? Art { get; }

    /// <summary>Row <paramref name="row"/>'s text.</summary>
    string RowText(int row);

    /// <summary>The detail line under the list for the focused row, or "" for none.</summary>
    string Detail(int row);

    /// <summary>The horizontal stepper on the focused row. Returns whether anything changed.</summary>
    bool Step(int row, int dir);

    /// <summary>The confirm press on the focused row. Returns true when the page handled it;
    /// false lets the flow advance to the next screen.</summary>
    bool Accept(int row);
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

    private readonly CustomPlaneStore _store;
    private readonly Dictionary<HangarScreen, IHangarPage> _pages = new();

    /// <summary>Opens a flow over <paramref name="store"/>, reading its saved planes once for the
    /// plane-selection screen. <paramref name="strings"/> may be <see cref="UiStrings.Empty"/>;
    /// every label then falls back to its own plain text. <paramref name="dataRoot"/> is where
    /// <c>extracted/</c> lives, for the art-bearing pages; null means every page's art is null.</summary>
    public HangarFlow(CustomPlaneStore store, UiStrings strings, string? dataRoot = null)
    {
        _store = store;
        Strings = strings;
        DataRoot = dataRoot;
        Saved = store.List();
    }

    /// <summary>The langui table the screens title and label themselves from.</summary>
    public UiStrings Strings { get; }

    /// <summary>The folder <c>extracted/</c> sits in, or null when the caller has none. Pages
    /// resolve their TGAs under it and treat absence as no art.</summary>
    public string? DataRoot { get; }

    /// <summary>The saved planes as they were when the flow opened, the plane-selection roster.
    /// Not refreshed mid-flow: the only writer is this flow's own commit, which ends it.</summary>
    public IReadOnlyList<CustomPlaneDef> Saved { get; }

    /// <summary>The plane being built. Replaced outright when the plane-selection screen starts a
    /// new build or loads a saved one; edited in place by every screen after that.</summary>
    public CustomPlaneDef Scratch { get; private set; } = new();

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
        Row = 0;
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
        Row = 0;
        return true;
    }

    /// <summary>Saves the scratch plane and ends the flow, or refuses and leaves
    /// <see cref="Message"/> saying why. The gate is <see cref="HangarEconomy"/>'s verdict plus a
    /// name, in the original's own words; funds are never checked (Decision 2). Returns whether
    /// the plane was saved.</summary>
    public bool Commit()
    {
        if (string.IsNullOrWhiteSpace(Scratch.Name))
        {
            Message = Strings.Text(203, "You must enter a name for your new plane.");
            return false;
        }

        var verdict = HangarEconomy.Price(Scratch).Verdict;
        if (verdict != PurchaseVerdict.Ok)
        {
            Message = Strings.Text(1182, "CAN'T PURCHASE:") + " " + (verdict == PurchaseVerdict.Overweight
                ? Strings.Text(1227, "OVERWEIGHT")
                : Strings.Text(1171, "No Engine Selected"));
            return false;
        }

        _store.Save(Scratch);
        BuiltPlaneName = Scratch.Name;
        Exit = HangarExit.Built;
        return true;
    }

    /// <summary>Starts a fresh build (the plane-selection screen's New Plane row).</summary>
    public void StartNewPlane() => Scratch = new CustomPlaneDef();

    /// <summary>Starts from a saved plane, as a copy: editing and abandoning it must not touch
    /// what is on disk, so the store's own canonical serialisation is the deep copy.</summary>
    public void StartFromSaved(CustomPlaneDef saved) =>
        Scratch = CustomPlaneStore.Deserialize(CustomPlaneStore.Serialize(saved)) ?? new CustomPlaneDef();

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
    // ⚠ This switch is the mount point: C22-C26 each replace one placeholder line with their own
    // page type and change nothing else in the shell.
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
                HangarScreen.Name => new HangarNamePage(this),
                HangarScreen.Purchase => new HangarPurchasePage(this),
                _ => new HangarPlaceholderPage(this, screen),
            };
            _pages[screen] = page;
        }

        return page;
    }

    // The focused row, kept inside the page's current list: a page whose row count shrank under
    // the cursor (a shorter saved-plane roster, a screen swapped by a later item) must not index
    // past its own end.
    private int ClampedRow()
    {
        Row = Math.Clamp(Row, 0, Math.Max(0, Page.RowCount - 1));
        return Row;
    }
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
    public virtual bool Step(int row, int dir) => false;

    /// <inheritdoc/>
    public virtual bool Accept(int row) => false;
}

/// <summary>
/// The flow's first screen: start a new plane, or load one of the store's saved planes to edit.
/// Confirming either seats the scratch plane and lets the flow advance, so the screens after this
/// one always have a plane to edit.
/// </summary>
public sealed class HangarPlaneSelectionPage : HangarPage
{
    /// <summary>Binds the page to its flow.</summary>
    public HangarPlaneSelectionPage(HangarFlow flow)
        : base(flow)
    {
    }

    /// <inheritdoc/>
    public override HangarScreen Screen => HangarScreen.PlaneSelection;

    /// <inheritdoc/>
    public override int RowCount => Flow.Saved.Count + 1;

    /// <inheritdoc/>
    public override string RowText(int row) => row == 0 ? "New Plane" : Flow.Saved[row - 1].Name;

    /// <inheritdoc/>
    public override string Detail(int row) =>
        row == 0
            ? "Build a plane from a bare airframe"
            : Flow.AirframeName(Flow.Saved[row - 1].Airframe);

    /// <inheritdoc/>
    public override bool Accept(int row)
    {
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
}

/// <summary>
/// The plane's name, which is its identity in the store. ⚠ C25 replaces this with real text
/// entry; the stepper here walks names built off the chosen airframe so a flow can be completed
/// and the store exercised before that lands.
/// </summary>
public sealed class HangarNamePage : HangarPage
{
    /// <summary>Binds the page to its flow.</summary>
    public HangarNamePage(HangarFlow flow)
        : base(flow)
    {
    }

    /// <inheritdoc/>
    public override HangarScreen Screen => HangarScreen.Name;

    /// <inheritdoc/>
    public override int RowCount => 1;

    /// <inheritdoc/>
    public override string RowText(int row) =>
        Scratch.Name.Length > 0 ? Scratch.Name : "(unnamed)";

    /// <inheritdoc/>
    public override string Detail(int row) => Flow.Strings.Text(1028, "PLANE NAME:");

    /// <inheritdoc/>
    public override bool Step(int row, int dir)
    {
        var names = Candidates();
        int at = names.IndexOf(Scratch.Name);
        Scratch.Name = names[((at + dir) % names.Count + names.Count) % names.Count];
        return true;
    }

    // The airframe's own name, then numbered variants of it. Always at least two entries, so the
    // stepper always moves.
    private List<string> Candidates()
    {
        string based = Flow.AirframeName(Scratch.Airframe);
        var names = new List<string>();
        if (Scratch.Name.Length > 0)
        {
            names.Add(Scratch.Name);
        }

        for (int i = 1; i <= 4; i++)
        {
            string candidate = i == 1 ? based : $"{based} {i}";
            if (!names.Contains(candidate))
            {
                names.Add(candidate);
            }
        }

        return names;
    }
}

/// <summary>
/// The purchase review and the Build action. ⚠ C26 replaces this with the itemised list; what
/// stands here is the totals line and the commit, so the gate and the save are already the real
/// ones and C26 changes only what is drawn above them.
/// </summary>
public sealed class HangarPurchasePage : HangarPage
{
    /// <summary>Binds the page to its flow.</summary>
    public HangarPurchasePage(HangarFlow flow)
        : base(flow)
    {
    }

    /// <inheritdoc/>
    public override HangarScreen Screen => HangarScreen.Purchase;

    /// <inheritdoc/>
    public override int RowCount => 1;

    /// <inheritdoc/>
    public override string RowText(int row) => Flow.Strings.Text(1199, "Purchase Now");

    /// <inheritdoc/>
    public override string Detail(int row)
    {
        var bill = HangarEconomy.Price(Scratch);
        return $"{Flow.Strings.Text(1198, "Totals")}   ${bill.Total.Cost}   " +
               $"{bill.Total.Weight} / {bill.Capacity} lbs.";
    }

    /// <inheritdoc/>
    public override bool Accept(int row)
    {
        Flow.Commit();
        return true; // the last screen: a refused commit stays here with the reason showing
    }
}

/// <summary>
/// A screen C22-C26 has not landed yet: the right heading, a Continue row, and a summary of what
/// the scratch plane currently carries for that screen. It edits nothing, so a flow walked through
/// it produces exactly the plane the screens before it chose.
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
