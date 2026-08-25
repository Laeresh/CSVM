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
/// pylon ordnance picks, a description panel, and ACCEPT/CANCEL LOADOUT, for
/// <see cref="CampaignFlow.AmmoSlot"/> 0 the pilot or 1 the wingman. Edits a working copy, the
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

    // Both diagram sheets stack one frame per airframe, in the airframe id's own order.
    private const int DiagramFrames = 11;

    // IDS_AMMOSHORTNAME (3360+) fallback text, index 4 the no-gun marker's own row.
    private static readonly string[] AmmoFallback = { "Slug", "Dum-dum", "Armor-piercing", "Explosive", "None" };

    // The ordnance table (campaign-screens.md "Ammo selection"), row-for-row the same order as
    // stock_loadouts.json's pylon_ordnance list: the mission ordinal a row unlocks at, and its
    // fallback label for when langui text is unavailable.
    private static readonly int[] OrdnanceThreshold = { 1, 1, 2, 8, 7, 12, 7, 7, 17, 17, 20, 1 };

    private readonly CustomPlaneStore? _planes;
    private StockLoadouts? _stock;

    private CampaignProfileDef? _loadedProfile;
    private int _loadedSlot = -1;
    private int _loadedPlaneIndex = -1;
    private OwnedPlane? _plane;
    private SlotBuild _build;
    private int[] _ammo = new int[4];
    private int[] _ordnance = new int[8];

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
    }

    /// <inheritdoc/>
    public override CampaignScreen Screen => CampaignScreen.Ammo;

    /// <summary>The original screen carries no title of its own; this names it for the board
    /// chrome, which heads every screen (<see cref="CampaignRosterPage"/>'s own reasoning).</summary>
    public override string Title => "AMMO SELECTION";

    /// <inheritdoc/>
    public override int RowCount => CancelRow + 1;

    /// <inheritdoc/>
    public override string Footer =>
        "↑↓  Choose       ←→  Change       Enter / A  Select       Esc / B  Back";

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

    /// <summary>The screen's own title and the two panel headings, at their authored positions.</summary>
    public override IReadOnlyList<BoardLine> Captions => new[]
    {
        new BoardLine("AMMO SELECTION", 138, 36, 190, 20, BoardInk.Heading),
        new BoardLine("AMMUNITION", 138, 76, 200, 15, BoardInk.Heading),
        new BoardLine("ROCKETS", 138, 291, 200, 15, BoardInk.Heading),
    };

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

    /// <inheritdoc/>
    public override string RowText(int row)
    {
        EnsureLoaded();
        if (row < GroupRows)
        {
            return GroupRowText(row);
        }

        if (row < AcceptRow)
        {
            return PylonRowText(row - GroupRows);
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

    /// <inheritdoc/>
    public override bool Step(int row, int dir)
    {
        EnsureLoaded();
        if (_plane == null || dir == 0)
        {
            return false;
        }

        if (row < GroupRows)
        {
            return StepGroup(row, dir);
        }

        if (row < AcceptRow)
        {
            return StepPylon(row - GroupRows, dir);
        }

        return false;
    }

    /// <inheritdoc/>
    public override bool Accept(int row)
    {
        EnsureLoaded();
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

    /// <inheritdoc/>
    public override bool Back()
    {
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

    // Reloads the working copy from the flow's current target (profile, slot, plane index) when it
    // changed since the last load, or when a prior Accept/Cancel/Back dropped it. A re-entry onto
    // the same target after a cancel therefore reads the still-unedited stored fit, never the
    // discarded edits.
    private void EnsureLoaded()
    {
        var profile = Flow.Profile;
        if (profile == null)
        {
            _plane = null;
            return;
        }

        int slot = Flow.AmmoSlot;
        int planeIndex = slot == 0 ? profile.SelectedPlane : profile.WingmanPlane;
        if (_plane != null && ReferenceEquals(profile, _loadedProfile)
            && slot == _loadedSlot && planeIndex == _loadedPlaneIndex)
        {
            return;
        }

        _loadedProfile = profile;
        _loadedSlot = slot;
        _loadedPlaneIndex = planeIndex;
        _plane = planeIndex >= 0 && planeIndex < profile.Planes.Count ? profile.Planes[planeIndex] : null;
        _build = _plane != null ? ResolveBuild(_plane) : default;
        _ammo = _plane != null ? (int[])_plane.Ammo.Clone() : new int[4];
        _ordnance = _plane != null ? (int[])_plane.Ordnance.Clone() : new int[8];
    }

    // Drops the working copy without writing it anywhere, forcing a fresh EnsureLoaded on the next
    // access: Cancel, Back and a just-completed Accept all call this.
    private void Discard() => _plane = null;

    // Writes the working copy into the plane's own record and saves the profile, the original's
    // uiData 2034/gosCallback 12 commit path.
    private void Commit()
    {
        if (_plane == null)
        {
            return;
        }

        _plane.Ammo = _ammo;
        _plane.Ordnance = _ordnance;
        Flow.Store.Save(Flow.Profile!);
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

    private string GroupRowText(int row)
    {
        string title = _plane == null
            ? $"Slot {row + 1}"
            : Flow.Strings.Text(HangarEconomy.Airframes[ClampAirframe(_plane.Airframe)].SlotTitle(row), $"Slot {row + 1}");
        if (_plane == null || !_build.GunPresent[row])
        {
            return $"{title}: {Flow.Strings.Text(3315, "No Gun")}";
        }

        int ammo = ClampAmmo(_ammo[row]);
        return $"{title}: {AmmoLabel(ammo)}";
    }

    private bool StepGroup(int row, int dir)
    {
        if (!_build.GunPresent[row])
        {
            return false;
        }

        int at = ((ClampAmmo(_ammo[row]) + dir) % 4 + 4) % 4;
        _ammo[row] = at;
        return true;
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

    private string PylonRowText(int cell)
    {
        if (_plane == null || !PylonActive(cell))
        {
            return "-";
        }

        int table = OrdnanceTableIndex(cell);
        return OrdnanceLabel(table);
    }

    private bool StepPylon(int cell, int dir)
    {
        if (!PylonActive(cell))
        {
            return false;
        }

        int ordinal = MissionOrdinal();
        int at = OrdnanceTableIndex(cell);
        for (int tries = 0; tries < CampaignLoadout.PylonRows; tries++)
        {
            at = ((at + dir) % CampaignLoadout.PylonRows + CampaignLoadout.PylonRows)
                % CampaignLoadout.PylonRows;
            if (ordinal >= OrdnanceThreshold[at])
            {
                _ordnance[cell] = at + 1;
                return true;
            }
        }

        return false;
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
    // which never touch CustomPlaneStore per B13's own SellPrice fallback reasoning).
    private SlotBuild ResolveBuild(OwnedPlane plane)
    {
        var built = _planes?.Load(plane.Name);
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
