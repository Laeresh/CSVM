using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;

namespace CSVM.UI;

/// <summary>
/// The ammo selection screen (<c>Campaign Ammo Selection.png</c>, <c>ORDINANCELAYOUT.SCRIPT</c>,
/// <c>docs/formats/campaign-screens.md</c> "Ammo selection"): four gun-group ammunition picks, eight
/// pylon ordnance picks, a description panel, and ACCEPT/CANCEL LOADOUT, for whichever record
/// <see cref="CampaignFlow.AmmoTarget"/> names: <see cref="CampaignFlow.AmmoSlot"/> 0 the pilot or
/// 1 the wingman on the seated player's check, a guest's own aircraft on theirs. Edits a working copy, the
/// original's own model (<c>uiData</c> 2035/2034): nothing reaches <see cref="Flow"/>'s profile until
/// ACCEPT, and CANCEL — or backing out — simply drops the copy. A gun group's build comes from the
/// plane's <see cref="CustomPlaneDef"/> when hangar-built, else the airframe's stock fit for the two
/// profile-seeded starters (B13). <see cref="OwnedPlane.Ordnance"/>'s encoding, since the original's
/// own ordnance id is undecoded, is <c>docs/PLAN-M5-campaign.md</c> C25's, not the save's.
/// </summary>
public sealed class CampaignAmmoPage : CampaignPage
{
    // Row layout: four ammo groups, eight pylon cells (0-3 left wing, 4-7 right wing, per
    // campaign-screens.md's own model), then the two commit buttons.
    private const int GroupRows = 4;
    private const int PylonRows = 8;
    private const int AcceptRow = GroupRows + PylonRows;
    private const int CancelRow = AcceptRow + 1;

    // [@OrdinanceLayout@]'s own geometry, with V3=136, V4=410, DROPWIDTH=148 and [GLOBALVARS]'
    // STDITEMH=15 resolved: the gun captions (OL_T_GunName0..3), the ammunition fields
    // (OL_D_AMMO0..3) fifteen pixels under them, and the rockets in two columns
    // (OL_D_ROCKETS0..3 at V3, OL_D_ROCKETS4..7 at V4).
    private const float CaptionX = 142f;
    private const float CaptionY = 105f;
    private const float GroupPitch = 42f;
    private const float LeftColumnX = 136f;
    private const float RightColumnX = 410f;
    private const float AmmoFieldY = 120f;
    private const float RocketFieldY = 320f;
    private const float RocketPitch = 28f;
    private const float DropWidth = 148f;
    private const float ItemHeight = 15f;

    // The two D rows' own trailing counts, GUNS and ROCKETS. Neither list ever outruns its window,
    // which is why the reference screenshot's open rocket list carries no scrollbar.
    private const int AmmoListRows = 5;
    private const int RocketListRows = 12;

    // The caption takes the same face its field's words do, which is what the reference draws.
    private const float CaptionFont = 11f;
    private const float HeadingFont = 15f;

    // IDS_OL_AMMO_CAPTION and IDS_OL_ROCKET_CAPTION, the parenthesised notes beside the two panel
    // headings. Both strings own a leading space, and the authored x is where that space starts.
    private const int AmmoCaptionLabel = 1026;
    private const int RocketCaptionLabel = 1027;

    // IDS_GUNSHORTNAME, the calibre words the captions carry.
    private const int CalibreLabel = 3320;

    // The greyed marker an empty gun group shows where its calibre would be.
    private const int NoGunLabel = 3315;

    // Both diagram sheets stack one frame per airframe, in the airframe id's own order.
    private const int DiagramFrames = 11;

    // IDS_AMMOSHORTNAME (3360+) fallback text, index 4 the no-gun marker's own row.
    private static readonly string[] AmmoFallback = { "Slug", "Dum-dum", "Armor-piercing", "Explosive", "None" };

    // The ordnance table (campaign-screens.md "Ammo selection"), row-for-row the same order as
    // stock_loadouts.json's pylon_ordnance list: the mission ordinal a row unlocks at, and its
    // fallback label for when langui text is unavailable.
    private static readonly int[] OrdnanceThreshold = { 1, 1, 2, 8, 7, 12, 7, 7, 17, 17, 20, 1 };

    private readonly CustomPlaneStore? _planes;
    private readonly CampaignCombo[] _groupField = new CampaignCombo[GroupRows];
    private readonly CampaignCombo[] _pylonField = new CampaignCombo[PylonRows];

    // Which ordnance table row each pylon field's entries stand for. The list holds only what the
    // mission ordinal has unlocked, so an entry index is not a table index.
    private readonly int[][] _pylonRows = new int[PylonRows][];

    private StockLoadouts? _stock;

    private OwnedPlane? _plane;
    private SlotBuild _build;
    private int[] _ammo = new int[4];
    private int[] _ordnance = new int[8];

    // The mission ordinal the pylon lists were filled at. A later mission unlocks more rows, so a
    // changed ordinal refills them.
    private int _filledOrdinal = -1;

    /// <summary>Binds the page to its flow, resolving builds against the flow's hangar store (null
    /// off-engine, where every plane then reads as its stock fit) and the stock-loadout table.</summary>
    public CampaignAmmoPage(CampaignFlow flow)
        : this(flow, flow.Planes, flow.Stock)
    {
    }

    /// <summary>Binds the page to its flow over explicit stores, for engine-free testing.</summary>
    public CampaignAmmoPage(CampaignFlow flow, CustomPlaneStore? planes, StockLoadouts? stock)
        : base(flow)
    {
        _planes = planes;
        _stock = stock;
        for (int group = 0; group < GroupRows; group++)
        {
            _groupField[group] = new CampaignCombo(
                LeftColumnX, AmmoFieldY + (group * GroupPitch), DropWidth, ItemHeight, AmmoListRows);
        }

        for (int cell = 0; cell < PylonRows; cell++)
        {
            _pylonField[cell] = new CampaignCombo(
                cell < 4 ? LeftColumnX : RightColumnX,
                RocketFieldY + (cell % 4 * RocketPitch),
                DropWidth, ItemHeight, RocketListRows);
            _pylonRows[cell] = Array.Empty<int>();
        }
    }

    /// <inheritdoc/>
    public override CampaignScreen Screen => CampaignScreen.Ammo;

    /// <summary>The original screen carries no title of its own; this names it for the board
    /// chrome, which heads every screen (<see cref="CampaignRosterPage"/>'s own reasoning).</summary>
    public override string Title => "AMMO SELECTION";

    /// <inheritdoc/>
    public override int RowCount => CancelRow + 1;

    /// <inheritdoc/>
    public override string Footer => Flow.OpenCombo != null
        ? "↑↓  Choose       Enter / A  Take       Esc / B  Close"
        : "↑↓  Choose       ←→  Change       Enter / A  Select       Esc / B  Back";

    /// <summary>The top view of the aircraft being fitted, the frame this airframe owns in
    /// the plan-view sheet.</summary>
    public override HangarArt? Art => Diagram(PlaneDiagrams.Top);

    /// <summary>The two aircraft diagrams, each the airframe's own frame of its sheet, at the
    /// authored positions of <c>ol_p_planetopicon</c> and <c>ol_p_planefrticon</c>.</summary>
    public override IReadOnlyList<BoardPicture> Pictures
    {
        get
        {
            EnsureLoaded();
            if (_plane is not { } plane)
            {
                return Array.Empty<BoardPicture>();
            }

            int frame = ClampAirframe(plane.Airframe);
            return new[]
            {
                new BoardPicture(new BoardArt(BoardArtLibrary.Ui, "OL_PlaneDiagramsTop.png", DiagramFrames), 305, 96, frame),
                new BoardPicture(new BoardArt(BoardArtLibrary.Ui, "OL_PlaneDiagramsFront.png", DiagramFrames), 225, 437, frame),
            };
        }
    }

    /// <summary>The screen's own title, the two panel headings with their parenthesised notes, and
    /// one caption per gun group: the calibre the group mounts, or the greyed marker an empty group
    /// carries in its place. All at their authored widgets, the captions at
    /// <c>OL_T_GunName0..3</c>.</summary>
    public override IReadOnlyList<BoardLine> Captions
    {
        get
        {
            EnsureLoaded();
            var lines = new List<BoardLine>
            {
                new("AMMO SELECTION", 138, 36, 190, 20, BoardInk.Heading),
                new("AMMUNITION", LeftColumnX, 74, 150, HeadingFont, BoardInk.Heading),
                new(Flow.Strings.Text(AmmoCaptionLabel, " (by gun group):"),
                    246, 75, 200, CaptionFont, BoardInk.Detail),
                new("ROCKETS", LeftColumnX, 290, 200, HeadingFont, BoardInk.Heading),
                new(Flow.Strings.Text(RocketCaptionLabel, " (underwing hardpoints):"),
                    214, 291, 200, CaptionFont, BoardInk.Detail),
            };
            if (_plane == null)
            {
                return lines;
            }

            for (int group = 0; group < GroupRows; group++)
            {
                bool armed = _build.GunPresent[group];
                lines.Add(new BoardLine(
                    armed ? CalibreName(_build.GunCalibre[group]) : Flow.Strings.Text(NoGunLabel, "No Gun"),
                    CaptionX, CaptionY + (group * GroupPitch), 200, CaptionFont,
                    armed ? BoardInk.Row : BoardInk.Detail));
            }

            return lines;
        }
    }

    // The stock table: the flow's, else (on-engine only, where res:// resolves) the default file.
    // Off-engine a flow without one reads every plane as fit-less rather than touching Godot.
    private StockLoadouts? Stock => _stock ??= Flow.Stock;

    /// <inheritdoc/>
    public override BoardButtonRef Button(int row) => row switch
    {
        AcceptRow => new BoardButtonRef(BoardButton.AcceptLoadout),
        CancelRow => new BoardButtonRef(BoardButton.CancelLoadout),
        _ => BoardButtonRef.None,
    };

    /// <summary>The drop-down a row owns, at its authored rectangle. An empty gun group and a
    /// hardpoint the wing does not carry own none: the original draws no field for either, and the
    /// group's own caption says so instead.</summary>
    public override CampaignCombo? Combo(int row)
    {
        EnsureLoaded();
        if (_plane == null)
        {
            return null;
        }

        if (row >= 0 && row < GroupRows)
        {
            return _build.GunPresent[row] ? _groupField[row] : null;
        }

        if (row >= GroupRows && row < AcceptRow)
        {
            int cell = row - GroupRows;
            return PylonActive(cell) ? _pylonField[cell] : null;
        }

        return null;
    }

    /// <summary>A pick row's words are its field's, and the board draws them inside the field. A
    /// row with no field contributes no line at all, which is why these are empty rather than the
    /// caption's words repeated.</summary>
    public override string RowText(int row)
    {
        EnsureLoaded();
        if (row < AcceptRow)
        {
            return Combo(row) is { } combo ? combo.Text : string.Empty;
        }

        return row == AcceptRow ? "ACCEPT LOADOUT" : "CANCEL LOADOUT";
    }

    /// <inheritdoc/>
    public override string Detail(int row)
    {
        EnsureLoaded();
        if (row < GroupRows)
        {
            return GroupDescription(row);
        }

        if (row < AcceptRow)
        {
            return PylonDescription(row - GroupRows);
        }

        return row == AcceptRow
            ? "Writes these picks into the plane's saved fit"
            : "Leaves the plane's saved fit unchanged";
    }

    /// <summary>The closed field's own stepper, which takes the same door a picked list row does:
    /// the entry beside the current one, wrapping. A pylon's list holds only the rows the mission
    /// ordinal has unlocked, so the step skips the locked ones without knowing they exist.</summary>
    public override bool Step(int row, int dir)
    {
        EnsureLoaded();
        if (dir == 0 || Combo(row) is not { } combo)
        {
            return false;
        }

        return Take(row, combo.Next(dir));
    }

    /// <inheritdoc/>
    public override bool Accept(int row)
    {
        EnsureLoaded();
        if (Combo(row) is { } combo)
        {
            // An open list's confirm takes the row under the cursor; a closed one opens.
            return combo.Confirm() is { } picked ? Take(row, picked) : combo.Expand();
        }

        if (row == AcceptRow)
        {
            Commit();
            Discard();
            Flow.GoTo(CampaignScreen.FlightCheck);
            return true;
        }

        if (row == CancelRow)
        {
            Discard();
            Flow.GoTo(CampaignScreen.FlightCheck);
            return true;
        }

        return false;
    }

    /// <summary>The front view, the same frame index in the head-on sheet. It is a per-plane
    /// picture, not a per-row one, so every row shows it.</summary>
    public override HangarArt? RowArt(int row) => Diagram(PlaneDiagrams.Front);

    /// <summary>Back closes an open list first, changing nothing; otherwise it drops the working
    /// copy the way CANCEL does.</summary>
    public override bool Back()
    {
        foreach (var field in _groupField)
        {
            if (field.Collapse())
            {
                return true;
            }
        }

        foreach (var field in _pylonField)
        {
            if (field.Collapse())
            {
                return true;
            }
        }

        Discard();
        return false;
    }

    private static int ClampAmmo(int ammo) => ammo >= 0 && ammo <= 3 ? ammo : 0;

    private static int ClampAirframe(int airframe) =>
        Math.Clamp(airframe, 0, HangarEconomy.Airframes.Length - 1);

    // A starter's shape straight from stock_loadouts.json: per-slot presence/calibre by matching
    // GunSpec.Slot, and a per-wing hardpoint count from how many of Loadout.PylonFillOrder's first
    // Count entries land on each wing (1-4 left, 5-8 right, CustomPlaneBuild's own split).
    private static SlotBuild StockBuild(LoadoutDef? stock)
    {
        var present = new bool[4];
        var calibre = new int[4];
        if (stock != null)
        {
            foreach (var gun in stock.Guns)
            {
                if (gun.Slot is >= 1 and <= 4)
                {
                    present[gun.Slot - 1] = true;
                    calibre[gun.Slot - 1] = (gun.Caliber - 30) / 10;
                }
            }
        }

        int count = stock?.Hardpoints?.Count ?? 0;
        int left = 0, right = 0;
        for (int i = 0; i < count && i < Loadout.PylonFillOrder.Length; i++)
        {
            if (Loadout.PylonFillOrder[i] <= 4)
            {
                left++;
            }
            else
            {
                right++;
            }
        }

        return new SlotBuild(present, calibre, left, right);
    }

    // Reloads the working copy whenever the flow names a different record than the one loaded, or
    // when a prior Accept/Cancel/Back dropped it. A re-entry onto the same record after a cancel
    // therefore reads the still-unedited stored fit, never the discarded edits. The record itself
    // is the key: a guest's aircraft is not in the profile, so no index names it.
    private void EnsureLoaded()
    {
        var target = Flow.AmmoTarget();
        if (_plane != null && ReferenceEquals(target, _plane))
        {
            // The unlocked ordnance is a statement about the mission, not about the plane, so a
            // flow moved to another mission refills the pylon lists over the same working copy.
            if (_filledOrdinal != MissionOrdinal())
            {
                FillPylons();
            }

            return;
        }

        _plane = target;
        _build = _plane != null ? ResolveBuild(_plane) : default;
        _ammo = _plane != null ? (int[])_plane.Ammo.Clone() : new int[4];
        _ordnance = _plane != null ? (int[])_plane.Ordnance.Clone() : new int[8];
        FillGroups();
        FillPylons();
    }

    // The four ammunition fields, each over the whole IDS_AMMOSHORTNAME set: which of them a group
    // may carry does not depend on the gun, only on whether there is one at all.
    private void FillGroups()
    {
        var entries = new string[4];
        for (int ammo = 0; ammo < entries.Length; ammo++)
        {
            entries[ammo] = AmmoLabel(ammo);
        }

        for (int group = 0; group < GroupRows; group++)
        {
            _groupField[group].Load(entries, ClampAmmo(_ammo[group]));
        }
    }

    // The eight pylon fields, each over the ordnance rows this mission has unlocked. ⚠ The row the
    // plane already carries stays in its own list whatever the threshold says: the field draws what
    // is fitted, and a list without it would show the wrong ordnance on a replayed mission.
    private void FillPylons()
    {
        _filledOrdinal = MissionOrdinal();
        for (int cell = 0; cell < PylonRows; cell++)
        {
            int current = OrdnanceTableIndex(cell);
            var rows = new List<int>(CampaignLoadout.PylonRows);
            var entries = new List<string>(CampaignLoadout.PylonRows);
            int selected = 0;
            for (int table = 0; table < CampaignLoadout.PylonRows; table++)
            {
                if (_filledOrdinal < OrdnanceThreshold[table] && table != current)
                {
                    continue;
                }

                if (table == current)
                {
                    selected = rows.Count;
                }

                rows.Add(table);
                entries.Add(OrdnanceLabel(table));
            }

            _pylonRows[cell] = rows.ToArray();
            _pylonField[cell].Load(entries, selected);
        }
    }

    // Applies a pick to the working copy and to the field that made it. Nothing here can be
    // refused, unlike the plane screen's own Take: the ordnance a mission forbids is simply not in
    // the list, and every ammunition is legal for a mounted gun.
    private bool Take(int row, int pick)
    {
        if (Combo(row) is not { } combo || pick == combo.Selected)
        {
            return false;
        }

        combo.Select(pick);
        if (row < GroupRows)
        {
            _ammo[row] = ClampAmmo(pick);
            return true;
        }

        int cell = row - GroupRows;
        var rows = _pylonRows[cell];
        if (pick < 0 || pick >= rows.Length)
        {
            return false;
        }

        _ordnance[cell] = rows[pick] + 1;
        return true;
    }

    // Drops the working copy without writing it anywhere, forcing a fresh EnsureLoaded on the next
    // access: Cancel, Back and a just-completed Accept all call this.
    private void Discard() => _plane = null;

    // Writes the working copy into the plane's own record and saves the profile, the original's
    // uiData 2034/gosCallback 12 commit path. ⚠ A guest's record is session-scoped and belongs to
    // no profile, so their ACCEPT writes the record and saves nothing.
    private void Commit()
    {
        if (_plane == null)
        {
            return;
        }

        _plane.Ammo = _ammo;
        _plane.Ordnance = _ordnance;
        if (Flow.Field.Current == 0 && Flow.Profile is { } profile)
        {
            Flow.Store.Save(profile);
        }
    }

    // The aircraft's frame of one diagram sheet, captioned with the plane it is fitting.
    private HangarArt? Diagram(string file)
    {
        EnsureLoaded();
        if (_plane == null || Flow.DataRoot is not { } root)
        {
            return null;
        }

        return PlaneDiagrams.Frame(root, file, _plane.Airframe) is { } frame
            ? new HangarArt(frame, _plane.Name)
            : null;
    }

    // IDS_GUNSHORTNAME, whose string owns a leading space; the reference prints the caption without
    // one at the authored x, so it is trimmed. ⚠ The caption is the calibre, not the slot title:
    // matching the original costs us the "Inner Wing Guns" wording the row text used to carry.
    private string CalibreName(int calibre)
    {
        int idx = Math.Clamp(calibre, 0, 4);
        return Flow.Strings.Text(CalibreLabel + idx, $" .{30 + (idx * 10)}-cal.").Trim();
    }

    private string GroupDescription(int row)
    {
        if (_plane == null || !_build.GunPresent[row])
        {
            return "No gun mounted in this slot";
        }

        int ammo = ClampAmmo(_ammo[row]);
        string title = Flow.Strings.Text(3350 + ammo, AmmoFallback[ammo]);
        string body = Flow.Strings.Text(3370 + ammo, string.Empty);
        return body.Length > 0 ? $"{title} - {body}" : title;
    }

    private string PylonDescription(int cell)
    {
        if (_plane == null || !PylonActive(cell))
        {
            return "No hardpoint at this position";
        }

        int table = OrdnanceTableIndex(cell);
        string title = Flow.Strings.Text(3380 + table, OrdnanceFallback(table));
        string body = Flow.Strings.Text(3410 + table, string.Empty);
        return body.Length > 0 ? $"{title} - {body}" : title;
    }

    // A cell's ordnance pick as a table index (0..11), through the one decoder every reader of the
    // stored field shares.
    private int OrdnanceTableIndex(int cell) =>
        CampaignLoadout.PylonRow(cell < _ordnance.Length ? _ordnance[cell] : 0);

    private bool PylonActive(int cell) =>
        cell < 4 ? cell < _build.LeftHardpoints : (cell - 4) < _build.RightHardpoints;

    private string AmmoLabel(int ammo) => Flow.Strings.Text(3360 + ammo, AmmoFallback[ammo]);

    private string OrdnanceLabel(int table)
    {
        string fallback = OrdnanceFallback(table);
        return Flow.Strings.Text(3395 + table, fallback);
    }

    private string OrdnanceFallback(int table) =>
        Stock is { } stock && table >= 0 && table < stock.Options.PylonOrdnance.Count
            ? stock.Options.PylonOrdnance[table].Label
            : "None";

    private int MissionOrdinal() => Math.Max(1, Flow.MissionSeq + 1);

    // The plane's gun/hardpoint shape: from its own CustomPlaneStore build when it has one (every
    // hangar-built plane), else the airframe's plain stock fit (the two profile-seeded starters,
    // which use the stock-fit fallback and never touch CustomPlaneStore, and a guest's
    // stock airframe, which is named for its airframe and so must never look a build up by name).
    private SlotBuild ResolveBuild(OwnedPlane plane)
    {
        var built = Flow.Field.IsStock(plane) ? null : _planes?.Load(plane.Name);
        if (built != null)
        {
            var present = new bool[4];
            var calibre = new int[4];
            for (int i = 0; i < 4; i++)
            {
                present[i] = !built.Guns[i].IsEmpty;
                calibre[i] = built.Guns[i].Calibre ?? 0;
            }

            return new SlotBuild(present, calibre, built.LeftHardpoints, built.RightHardpoints);
        }

        var stock = Stock?.ForModel(PlanePickerRoster.AirframeNode(plane.Airframe));
        return StockBuild(stock);
    }

    // Which of the four gun slots mount something and at what calibre index (0-4), plus how many
    // hardpoints each wing carries. Engine-free: built from either CustomPlaneDef or a stock
    // LoadoutDef, never from a live model.
    private readonly struct SlotBuild
    {
        public SlotBuild(bool[] gunPresent, int[] gunCalibre, int left, int right)
        {
            GunPresent = gunPresent;
            GunCalibre = gunCalibre;
            LeftHardpoints = left;
            RightHardpoints = right;
        }

        public bool[] GunPresent { get; }

        public int[] GunCalibre { get; }

        public int LeftHardpoints { get; }

        public int RightHardpoints { get; }
    }
}
