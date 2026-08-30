using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Session;

namespace CSVM.UI;

/// <summary>What a plane selection row does. Every row on this screen is one of the four.</summary>
internal enum PlaneRowKind
{
    Pick,
    Export,
    Accept,
    Cancel,
}

/// <summary>One drawn row: what it does, and which crew slot it belongs to.</summary>
internal readonly record struct PlaneRow(PlaneRowKind Kind, int Slot = 0);

/// <summary>
/// The plane selection screen (<c>Campaign Flight Check Change Plane.png</c>,
/// <c>PLANESELECTION.SCRIPT</c>, <c>docs/formats/campaign-screens.md</c>): a drop-down per crew slot
/// over the profile's aircraft, each with the airframe's silhouette, its four ratings and its gun
/// and hardpoint list, EXPORT per slot, and ACCEPT/CANCEL SELECTIONS. Opened by the flight check's
/// CHANGE PLANE, which names the slot the cursor opens on (<see cref="CampaignFlow.PlaneSlot"/>).
///
/// <para>The screen edits live and restores on CANCEL, which is what <c>PS_B_CANCEL</c> does: it
/// calls <c>uiData</c> 2013 for both slots with the picks the screen opened with. ACCEPT writes
/// them into the profile and saves it, the script's own <c>gosCallback</c> 12.</para>
/// </summary>
public sealed class CampaignPlaneSelectionPage : CampaignPage
{
    // The two crew slots, and the layout's own y for each block: PS_D_PILOTPLANE at 132 against
    // PS_D_WINGPLANE at 350, every other widget of the pair the same 218 pixels apart.
    private const int Slots = 2;
    private const float SlotDrop = 218f;

    // The authored geometry of one crew slot's block, the pilot's row of [@PlaneSelection@].
    private const float ComboX = 138f;
    private const float ComboY = 132f;
    private const float ComboWidth = 271f;
    private const float ItemHeight = 15f;
    private const int ItemsDisplayed = 13;
    private const float HeadingY = 102f;
    private const float PlaneNameX = 236f;
    private const float PlaneNameY = 106f;
    private const float SilhouetteX = 444f;
    private const float SilhouetteY = 138f;
    private const float RatingX = 430f;
    private const float RatingY = 228f;
    private const float RatingPitch = 17f;
    private const float RatingWidth = 140f;
    private const float WeaponsX = 578f;
    private const float WeaponsWidth = 160f;

    // The screen's own chrome, at PS_T_TITLE and PS_T_MISSIONINFO.
    private const float TitleFont = 20f;
    private const float BodyFont = 12f;

    // The plane-icon sheet stacks one silhouette per airframe, in the airframe id's own order.
    private const int SilhouetteFrames = 12;

    // IDS_PS_B_EXPORT, the plaque's own words, and the weapon column's hardpoint row.
    private const int ExportLabel = 1139;
    private const int HardpointsLabel = 1008;

    // IDS_PS_CANTFLYSAMEPLANE, the words the refused pick is answered with.
    private const int SamePlaneRefusal = 710;

    // The words a finished export is answered with. ⚠ Its %1!s! is the airframe's title, not the
    // plane's name: the original's dialog reads "Your Devastator has been exported" for a wingman
    // aircraft named "The Knave" (OriginalScreenshots/Campaign Flight Check Change Plane Export
    // dialog.png).
    private const int ExportedMessage = 702;

    // The four rating words, langui 501-505 in the order Poor to Excellent.
    private static readonly string[] RatingWords = { "Poor", "Fair", "Average", "Good", "Excellent" };

    private static readonly CampaignProfileDef EmptyProfile = new() { Name = string.Empty };

    private readonly CampaignCombo[] _combos = new CampaignCombo[Slots];
    private readonly int[] _opened = new int[Slots];
    private readonly bool? _wingmanOverride;

    // Which profile and how many planes the combos were filled from. A different one refills them,
    // since a combo's entries are a statement about that profile's aircraft.
    private CampaignProfileDef? _filledFrom;
    private int _filledCount = -1;

    /// <summary>Binds the page to its flow. <paramref name="wingman"/> lets a test fix the mission's
    /// wingman flag without an extraction to read <c>cm_sequence.zrd</c> from; production callers
    /// (the <see cref="CampaignFlow.Registry"/> line) pass none.</summary>
    public CampaignPlaneSelectionPage(CampaignFlow flow, bool? wingman = null)
        : base(flow)
    {
        _wingmanOverride = wingman;
        for (int slot = 0; slot < Slots; slot++)
        {
            _combos[slot] = new CampaignCombo(
                ComboX, ComboY + (slot * SlotDrop), ComboWidth, ItemHeight, ItemsDisplayed);
        }
    }

    /// <inheritdoc/>
    public override CampaignScreen Screen => CampaignScreen.PlaneSelection;

    /// <inheritdoc/>
    public override string Title => Flow.Strings.Text(3450 + Flow.MissionSeq, $"Mission {Flow.MissionSeq + 1}");

    /// <inheritdoc/>
    public override int RowCount => Rows().Count;

    /// <summary>The cursor opens on the combo of whichever slot's CHANGE PLANE was pressed.</summary>
    public override int OpeningRow
    {
        get
        {
            var rows = Rows();
            for (int row = 0; row < rows.Count; row++)
            {
                if (rows[row].Kind == PlaneRowKind.Pick && rows[row].Slot == Flow.PlaneSlot)
                {
                    return row;
                }
            }

            return 0;
        }
    }

    /// <inheritdoc/>
    public override string Footer => Flow.OpenCombo != null
        ? "↑↓  Choose       Enter / A  Take       Esc / B  Close"
        : "↑↓  Choose       ←→  Change       Enter / A  Select       Esc / B  Back";

    /// <summary>Each crew slot's aircraft silhouette, the airframe's own frame of the icon
    /// sheet, at the authored positions of <c>PS_P_PILOTPLANE</c> and <c>PS_P_WINGPLANE</c>.</summary>
    public override IReadOnlyList<BoardPicture> Pictures
    {
        get
        {
            Fill();
            var pictures = new List<BoardPicture>(Slots);
            for (int slot = 0; slot < ActiveSlots; slot++)
            {
                if (PlaneFor(slot) is { } plane)
                {
                    pictures.Add(new BoardPicture(
                        new BoardArt(BoardArtLibrary.Ui, "FC_PlaneIcons.Png", SilhouetteFrames),
                        SilhouetteX, SilhouetteY + (slot * SlotDrop), plane.Airframe));
                }
            }

            return pictures;
        }
    }

    /// <summary>The screen's title and mission line, each slot's heading and named aircraft, its
    /// four ratings and its gun and hardpoint list, all at their authored widgets.</summary>
    public override IReadOnlyList<BoardLine> Captions
    {
        get
        {
            Fill();
            var lines = new List<BoardLine>
            {
                new("PLANE SELECTION", 132, 36, 190, TitleFont, BoardInk.Heading),
                new(Title, 136, 70, 500, 15, BoardInk.Detail, Italic: true),
            };
            for (int slot = 0; slot < ActiveSlots; slot++)
            {
                float drop = slot * SlotDrop;
                lines.Add(new BoardLine(
                    slot == 0 ? "PILOT" : "WINGMAN", ComboX, HeadingY + drop, 94, 15, BoardInk.Heading));
                if (PlaneFor(slot) is not { } plane)
                {
                    continue;
                }

                lines.Add(new BoardLine(
                    SlotLabel(plane), PlaneNameX, PlaneNameY + drop, 400, BodyFont, BoardInk.Row));
                AddRatings(lines, plane, drop);
                lines.Add(new BoardLine(
                    WeaponList(plane), WeaponsX, RatingY + drop, WeaponsWidth, BodyFont, BoardInk.Row));
            }

            return lines;
        }
    }

    /// <summary>How many crew slots this screen draws: both when the mission carries a wingman, the
    /// pilot's alone otherwise.</summary>
    public int ActiveSlots => HasWingman ? 2 : 1;

    private bool HasWingman => _wingmanOverride ?? Flow.MissionHasWingman;

    /// <inheritdoc/>
    public override BoardButtonRef Button(int row)
    {
        var rows = Rows();
        if (row < 0 || row >= rows.Count)
        {
            return BoardButtonRef.None;
        }

        return rows[row].Kind switch
        {
            PlaneRowKind.Export => new BoardButtonRef(BoardButton.ExportPlane, rows[row].Slot),
            PlaneRowKind.Accept => new BoardButtonRef(BoardButton.AcceptSelections),
            PlaneRowKind.Cancel => new BoardButtonRef(BoardButton.CancelSelections),
            _ => BoardButtonRef.None,
        };
    }

    /// <inheritdoc/>
    public override CampaignCombo? Combo(int row)
    {
        Fill();
        var rows = Rows();
        return row >= 0 && row < rows.Count && rows[row].Kind == PlaneRowKind.Pick
            ? _combos[rows[row].Slot]
            : null;
    }

    /// <inheritdoc/>
    public override string RowText(int row)
    {
        Fill();
        var rows = Rows();
        if (row < 0 || row >= rows.Count)
        {
            return string.Empty;
        }

        return rows[row].Kind switch
        {
            PlaneRowKind.Pick => _combos[rows[row].Slot].Text,
            PlaneRowKind.Export => Flow.Strings.Text(ExportLabel, "Export"),
            PlaneRowKind.Accept => "ACCEPT SELECTIONS",
            _ => "CANCEL SELECTIONS",
        };
    }

    /// <inheritdoc/>
    public override string Detail(int row)
    {
        var rows = Rows();
        if (row < 0 || row >= rows.Count)
        {
            return string.Empty;
        }

        return rows[row].Kind switch
        {
            PlaneRowKind.Pick => "Choose the aircraft this crew slot flies",
            PlaneRowKind.Export => "Copies this plane and its loadout to Instant Action and multiplayer",
            PlaneRowKind.Accept => "Keeps these aircraft and returns to the flight check",
            _ => "Leaves both crew slots flying what they flew",
        };
    }

    /// <summary>The closed field's own stepper. It takes the same door a committed pick does, so a
    /// step and a picked list row are refused on the same terms.</summary>
    public override bool Step(int row, int dir)
    {
        Fill();
        var rows = Rows();
        if (row < 0 || row >= rows.Count || rows[row].Kind != PlaneRowKind.Pick || dir == 0)
        {
            return false;
        }

        int slot = rows[row].Slot;
        return Take(slot, _combos[slot].Next(dir));
    }

    /// <inheritdoc/>
    public override bool Accept(int row)
    {
        Fill();
        var rows = Rows();
        if (row < 0 || row >= rows.Count)
        {
            return false;
        }

        int slot = rows[row].Slot;
        switch (rows[row].Kind)
        {
            case PlaneRowKind.Pick:
                // An open list's confirm takes the row under the cursor; a closed one opens.
                return _combos[slot].Confirm() is { } picked
                    ? Take(slot, picked)
                    : _combos[slot].Expand();
            case PlaneRowKind.Export:
                return Export(slot);
            case PlaneRowKind.Accept:
                Commit();
                Flow.GoTo(CampaignScreen.FlightCheck);
                return true;
            case PlaneRowKind.Cancel:
                Restore();
                Flow.GoTo(CampaignScreen.FlightCheck);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Back closes an open list first, and otherwise leaves the screen the way CANCEL
    /// does: the picks the screen opened with are what the flight check goes back to.</summary>
    public override bool Back()
    {
        Fill();
        foreach (var combo in _combos)
        {
            if (combo.Collapse())
            {
                return true;
            }
        }

        Restore();
        return false;
    }

    // A named aircraft and its airframe, except where the two are the same word, the flight check's
    // own rule: "Devastator   Devastator" is not a second fact.
    private string SlotLabel(OwnedPlane plane)
    {
        string title = AirframeTitle(plane.Airframe);
        return plane.Name == title ? title : $"{plane.Name}   {title}";
    }

    // One combo entry, the reference screenshot's own "Blue Streak - Bloodhawk".
    private string EntryLabel(OwnedPlane plane) => $"{plane.Name} - {AirframeTitle(plane.Airframe)}";

    private string AirframeTitle(int airframe) => Flow.Strings.Text(3000 + airframe, $"Airframe {airframe}");

    // Every row this screen draws: a combo and an EXPORT per active crew slot, then the two commit
    // buttons. A slot with no aircraft to pick from still draws its combo, which is then empty.
    private List<PlaneRow> Rows()
    {
        var rows = new List<PlaneRow>(6);
        for (int slot = 0; slot < ActiveSlots; slot++)
        {
            rows.Add(new PlaneRow(PlaneRowKind.Pick, slot));
            rows.Add(new PlaneRow(PlaneRowKind.Export, slot));
        }

        rows.Add(new PlaneRow(PlaneRowKind.Accept));
        rows.Add(new PlaneRow(PlaneRowKind.Cancel));
        return rows;
    }

    // Fills both combos from the seated profile and remembers the picks the screen opened with,
    // which is what CANCEL restores. Refilled when the profile or its plane count changed under it.
    private void Fill()
    {
        var profile = Flow.Profile ?? EmptyProfile;
        if (ReferenceEquals(_filledFrom, profile) && _filledCount == profile.Planes.Count)
        {
            return;
        }

        _filledFrom = profile;
        _filledCount = profile.Planes.Count;
        var entries = new List<string>(profile.Planes.Count);
        foreach (var plane in profile.Planes)
        {
            entries.Add(EntryLabel(plane));
        }

        _opened[0] = Clamped(profile, profile.SelectedPlane);
        _opened[1] = Clamped(profile, profile.WingmanPlane);
        for (int slot = 0; slot < Slots; slot++)
        {
            _combos[slot].Load(entries, _opened[slot]);
        }
    }

    private int Clamped(CampaignProfileDef profile, int index) =>
        profile.Planes.Count == 0 ? 0 : Math.Clamp(index, 0, profile.Planes.Count - 1);

    // Applies a pick, or refuses it in the original's words. The refusal is the answer to a commit
    // (a picked list row or a closed field's step), never to movement inside an open list, which is
    // where message 10015 sits: refusing per movement would pop the dialog at every scrolled row.
    private bool Take(int slot, int pick)
    {
        if (pick == _combos[slot].Selected)
        {
            return false;
        }

        if (Clashes(slot, pick))
        {
            Flow.RaiseModal(Flow.Strings.Text(
                SamePlaneRefusal, "Pilot and Wingman must fly different planes."));
            return true;
        }

        _combos[slot].Select(pick);
        return true;
    }

    // The script's permit test on 10015: the change is allowed when the two combos differ or when
    // only one crew slot is active. ⚠ Compared by name, CampaignFlightField.KeyOf's rule for a
    // profile aircraft, since two of them may share an airframe and flying that pair is legal.
    private bool Clashes(int slot, int pick)
    {
        if (ActiveSlots < 2)
        {
            return false;
        }

        var planes = (Flow.Profile ?? EmptyProfile).Planes;
        int other = _combos[slot == 0 ? 1 : 0].Selected;
        return pick >= 0 && pick < planes.Count && other >= 0 && other < planes.Count
            && planes[pick].Name == planes[other].Name;
    }

    // EXPORT: the plane and the loadout the campaign fitted it with, into the build store the
    // Instant Action and multiplayer pickers list, then the original's own words for it.
    // ⚠ Refused for a stock record. It is named for its airframe, so a write under that name would
    // land on any hangar plane sharing it (CampaignFlightField.IsStock, and why a guest's check
    // draws no EXPORT at all).
    private bool Export(int slot)
    {
        if (PlaneFor(slot) is not { } plane || Flow.Planes is not { } store
            || Flow.Field.IsStock(plane) || string.IsNullOrWhiteSpace(plane.Name))
        {
            return false;
        }

        // ⚠ An existing record IS the build: export sets its loadout and leaves paint, armour and
        // engine alone. A starter or a granted aircraft has none, so one is created over the
        // flight check's own build-or-stock resolution rather than exporting an unarmed airframe.
        var def = store.Load(plane.Name) ?? CampaignProgression.BuildForOwned(plane) ?? StockBuild(plane);
        def.SetLoadout(plane.Ammo, plane.Ordnance);
        store.Save(def);
        Flow.RaiseModal(ExportedText(AirframeTitle(plane.Airframe)));
        return true;
    }

    private CustomPlaneDef StockBuild(OwnedPlane plane)
    {
        var def = new CustomPlaneDef { Name = plane.Name, Airframe = plane.Airframe };
        HangarFlow.LoadStockWeapons(
            def, Flow.Stock?.ForModel(PlanePickerRoster.AirframeNode(plane.Airframe)));
        return def;
    }

    // langui 702 with the airframe's title in its one placeholder. The fallback carries the same
    // words, since a build with no extraction still exports and still owes the player an answer.
    private string ExportedText(string title)
    {
        string text = Flow.Strings.Format(ExportedMessage, title);
        return text.Length > 0
            ? text
            : $"Your {title} has been exported and is now available for Multiplayer and " +
              "Instant Action missions.";
    }

    // ACCEPT: the two picks into the profile, and the profile to disk.
    private void Commit()
    {
        if (Flow.Profile is not { } profile || profile.Planes.Count == 0)
        {
            return;
        }

        profile.SelectedPlane = _combos[0].Selected;
        if (HasWingman)
        {
            profile.WingmanPlane = _combos[1].Selected;
        }

        Flow.Store.Save(profile);
        _opened[0] = profile.SelectedPlane;
        _opened[1] = profile.WingmanPlane;
    }

    // CANCEL and Back: both combos back to what the screen opened with, nothing written.
    private void Restore()
    {
        for (int slot = 0; slot < Slots; slot++)
        {
            _combos[slot].Collapse();
            _combos[slot].Select(_opened[slot]);
        }
    }

    private OwnedPlane? PlaneFor(int slot)
    {
        var profile = Flow.Profile ?? EmptyProfile;
        int at = _combos[slot].Selected;
        return at >= 0 && at < profile.Planes.Count ? profile.Planes[at] : null;
    }

    // The four rating lines, at PS_T_TOPSPEEDP and its three neighbours.
    private void AddRatings(List<BoardLine> lines, OwnedPlane plane, float drop)
    {
        var ratings = PlaneRatings.For(plane.Airframe, FitOf(plane));
        string[] labels = { "TOP SPEED:", "ARMOR:", "AGILITY:", "OFFENSE:" };
        for (int i = 0; i < labels.Length; i++)
        {
            lines.Add(new BoardLine(
                $"{labels[i]}   {RatingWord(ratings[i])}",
                RatingX, RatingY + drop + (i * RatingPitch), RatingWidth, BodyFont, BoardInk.Row));
        }
    }

    private string RatingWord(int stars)
    {
        int at = Math.Clamp(stars, 0, RatingWords.Length - 1);
        return Flow.Strings.Text(501 + at, RatingWords[at]);
    }

    // The gun and hardpoint column, the reference's own "(2) .50-cal." rows and its "(4)
    // Hardpoints" line, from the same build-or-stock resolution the flight check reads.
    private string WeaponList(OwnedPlane plane)
    {
        var fit = FitOf(plane);
        var lines = new List<string>();
        foreach (int calibre in new[] { 70, 60, 50, 40, 30 })
        {
            if (fit.Barrels.TryGetValue(calibre, out int barrels))
            {
                lines.Add($"({barrels}) {CalibreName(calibre)}");
            }
        }

        if (fit.Hardpoints > 0)
        {
            lines.Add($"({fit.Hardpoints}) {Flow.Strings.Text(HardpointsLabel, "Hardpoints")}");
        }

        return string.Join("\n", lines);
    }

    // IDS_GUNSHORTNAME, the same " .50-cal." the flight check's own weapon rows read, whose string
    // owns its leading space; the reference prints it here without one, so it is trimmed.
    private string CalibreName(int calibre)
    {
        int idx = Math.Clamp((calibre - 30) / 10, 0, 4);
        return Flow.Strings.Text(3320 + idx, $" .{30 + (idx * 10)}-cal.").Trim();
    }

    // ⚠ Asks the field whether the record is a stock airframe before touching CustomPlaneStore: a
    // guest's stock record is named for its airframe, and a hangar plane sharing that name would
    // otherwise fit it with somebody else's build.
    private PlaneFit FitOf(OwnedPlane plane) => PlaneFit.For(
        Flow.Field.IsStock(plane) ? null : Flow.Planes?.Load(plane.Name),
        Flow.Stock?.ForModel(PlanePickerRoster.AirframeNode(plane.Airframe)));
}
