using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;

namespace CSVM.UI;

/// <summary>What a flight check row does on Accept/Step. Info rows are display only.</summary>
internal enum FlightRowKind
{
    Info,
    ChangeAmmo,
    ChangePlane,
    ReturnToBriefing,
    FlyMission,
}

/// <summary>One gun group's picked calibre and whether the mount carries a second barrel — the
/// flight check's own reading of a plane's guns, resolved either from a hangar build
/// (<see cref="CustomPlaneDef.Guns"/>) or, for a plane with no build on file (the two starter
/// Devastators, a granted reward aircraft), from the airframe's stock fit
/// (<c>docs/formats/loadouts.md</c>).</summary>
internal readonly record struct FlightCheckGun(int Caliber, bool Twin);

/// <summary>One drawn row: its label, its <see cref="CampaignPage.Detail"/> text, what it does, the
/// ammo/plane slot (0 pilot, 1 wingman) a <see cref="FlightRowKind.ChangeAmmo"/>/
/// <see cref="FlightRowKind.ChangePlane"/> row acts on, and the airframe an info row's silhouette
/// (<see cref="ICampaignPage.RowArt"/>) is drawn from, if any.</summary>
internal readonly record struct FlightRow(
    string Text, string Detail, FlightRowKind Kind, int Slot = 0, int? Silhouette = null);

/// <summary>
/// The flight check screen (<c>Campaign Flight Check.png</c>, <c>FLIGHTCHECK.SCRIPT</c>,
/// <c>docs/formats/campaign-screens.md</c>): the mission title, a PILOT row and, when the mission's
/// <c>cm_sequence</c> wingman flag is set, a WINGMAN row, CHANGE AMMO per row (into
/// <see cref="CampaignScreen.Ammo"/>), CHANGE PLANE where the mission allows it, RETURN TO BRIEFING
/// and FLY MISSION. The row list carries only these actionable items; each plane's dense text
/// (title, both eight-row lists) and the objectives note live in <see cref="Detail"/>, the split
/// <see cref="CampaignRosterPage"/> uses for its own descriptive text. CHANGE PLANE's own design
/// (no picker screen exists in this item's boundary, so it cycles in place) is recorded in the
/// plan's C24 section, not repeated here.
/// </summary>
public sealed class CampaignFlightCheckPage : CampaignPage
{
    // The ordnance table's own "None" row (docs/formats/campaign-screens.md, the Ammo selection
    // decode): a pylon holding this id carries nothing, whether or not the pylon itself exists.
    private const int NoOrdnance = 11;

    // Airframe id 0-10 to stock_loadouts.json's def key, the same row order
    // PlanePickerRoster.AirframeNodes and loadouts.md's stock table both use.
    private static readonly string[] AirframeDefKeys =
    {
        "pautogyro", "pavenger", "pbalmoral", "pbloodhawk", "pbrigand",
        "pdevastator", "pfirebrand", "pfury", "pkestrel", "ppeacemaker", "pwarhawk",
    };

    // The ammunition short names (langui 3360+index), in the same order loadouts.md's
    // "selectable.gun_ammo" carries them; index 4 is the profile record's own no-gun marker.
    private static readonly string[] AmmoShortNames =
        { "Slug", "Dum-dum", "Armor-piercing", "Explosive", "None" };

    // The ordnance table's short names (langui 3395+index), loadouts.md's
    // "selectable.pylon_ordnance" verbatim, index 11 being NoOrdnance.
    private static readonly string[] OrdnanceShortNames =
    {
        "Armor-piercing", "High explosive", "Flak", "Sonic", "Flash", "Rear flash",
        "Smoke", "Choker", "Beeper", "Seeker", "Torpedo", "None",
    };

    private static readonly CampaignProfileDef EmptyProfile = new() { Name = string.Empty };

    private readonly Dictionary<int, HangarArt?> _silhouettes = new();
    private readonly Dictionary<int, HangarArt?> _diagrams = new();
    private readonly bool? _wingmanOverride;

    private CustomPlaneStore? _planes;
    private StockLoadouts? _stock;
    private Messages? _messages;
    private bool _missionResolved;
    private CampaignMission? _mission;

    /// <summary>Binds the page to its flow. <paramref name="planes"/>/<paramref name="stock"/> let a
    /// test supply an explicit store and stock-loadout table instead of the Godot-resolved
    /// defaults; <paramref name="wingman"/> lets a test fix the mission's wingman flag without a
    /// real extraction to read <c>cm_sequence.zrd</c> from. Production callers (the
    /// <see cref="CampaignFlow.Registry"/> line) pass none of the three.</summary>
    public CampaignFlightCheckPage(
        CampaignFlow flow, CustomPlaneStore? planes = null, StockLoadouts? stock = null, bool? wingman = null)
        : base(flow)
    {
        _planes = planes;
        _stock = stock;
        _wingmanOverride = wingman;
    }

    /// <inheritdoc/>
    public override CampaignScreen Screen => CampaignScreen.FlightCheck;

    /// <inheritdoc/>
    public override string Title => Flow.Strings.Text(3450 + Flow.MissionSeq, $"Mission {Flow.MissionSeq + 1}");

    /// <inheritdoc/>
    public override int RowCount => Rows().Count;

    /// <inheritdoc/>
    public override string Footer => Flow.Row < Rows().Count && Rows()[Flow.Row].Kind == FlightRowKind.ChangePlane
        ? "↑↓  Choose       ←→  Change Plane       Enter / A  Select       Esc / B  Back"
        : "↑↓  Choose       Enter / A  Select       Esc / B  Back";

    /// <summary>The focused pilot's aircraft head-on, its frame of the diagram sheet the ammo
    /// screen draws from. The shell hangs the row picture off this one, so without it the
    /// silhouette below never reached the screen at all.</summary>
    public override HangarArt? Art =>
        Airframe(Flow.Row) is { } airframe ? DiagramFor(airframe) : null;

    private bool HasWingman => _wingmanOverride ?? Mission()?.Wingman ?? false;

    // The flow's hangar store, or null off-engine: every plane then reads as its stock fit.
    private CustomPlaneStore? Planes => _planes ??= Flow.Planes;

    // The flow's stock table, or null off-engine: a plane then reads as fit-less rather than the
    // page touching Godot for the res:// default.
    private StockLoadouts? Stock => _stock ??= Flow.Stock;

    /// <inheritdoc/>
    public override HangarArt? RowArt(int row) =>
        Airframe(row) is { } airframe ? SilhouetteFor(airframe) : null;

    /// <inheritdoc/>
    public override string RowText(int row)
    {
        var rows = Rows();
        return row >= 0 && row < rows.Count ? rows[row].Text : string.Empty;
    }

    /// <inheritdoc/>
    public override string Detail(int row)
    {
        var rows = Rows();
        if (row < 0 || row >= rows.Count)
        {
            return string.Empty;
        }

        string own = rows[row].Detail;
        string objectives = ObjectivesNote();
        if (objectives.Length == 0)
        {
            return own;
        }

        return own.Length == 0 ? objectives : own + "\n\n" + objectives;
    }

    /// <inheritdoc/>
    public override bool Step(int row, int dir)
    {
        var rows = Rows();
        if (row < 0 || row >= rows.Count || rows[row].Kind != FlightRowKind.ChangePlane || dir == 0)
        {
            return false;
        }

        var profile = Flow.Profile ?? EmptyProfile;
        if (profile.Planes.Count == 0)
        {
            return false;
        }

        int count = profile.Planes.Count;
        if (rows[row].Slot == 0)
        {
            profile.SelectedPlane = (((profile.SelectedPlane + dir) % count) + count) % count;
        }
        else
        {
            profile.WingmanPlane = (((profile.WingmanPlane + dir) % count) + count) % count;
        }

        Flow.Store.Save(profile);
        return true;
    }

    /// <inheritdoc/>
    public override bool Accept(int row)
    {
        var rows = Rows();
        if (row < 0 || row >= rows.Count)
        {
            return false;
        }

        switch (rows[row].Kind)
        {
            case FlightRowKind.ChangeAmmo:
                Flow.SetAmmoSlot(rows[row].Slot);
                Flow.GoTo(CampaignScreen.Ammo);
                return true;
            case FlightRowKind.ReturnToBriefing:
                Flow.GoTo(CampaignScreen.Briefing);
                return true;
            case FlightRowKind.FlyMission:
                Flow.Request(CampaignExit.FlyMission);
                return true;
            default:
                return false;
        }
    }

    // Every row this screen draws, computed fresh each call from the profile and the mission's
    // wingman flag: the PILOT block, its two action rows, the WINGMAN block and its own two action
    // rows when the mission carries a wingman, then RETURN TO BRIEFING and FLY MISSION.
    private List<FlightRow> Rows()
    {
        var profile = Flow.Profile ?? EmptyProfile;
        var rows = new List<FlightRow>();
        AddSlot(rows, profile, slot: 0, heading: "PILOT",
            planeIndex: Math.Clamp(profile.SelectedPlane, 0, Math.Max(0, profile.Planes.Count - 1)));

        if (HasWingman)
        {
            AddSlot(rows, profile, slot: 1, heading: "WINGMAN",
                planeIndex: Math.Clamp(profile.WingmanPlane, 0, Math.Max(0, profile.Planes.Count - 1)));
        }

        rows.Add(new FlightRow("RETURN TO BRIEFING", string.Empty, FlightRowKind.ReturnToBriefing));
        rows.Add(new FlightRow("FLY MISSION", string.Empty, FlightRowKind.FlyMission));
        return rows;
    }

    private void AddSlot(List<FlightRow> rows, CampaignProfileDef profile, int slot, string heading, int planeIndex)
    {
        if (planeIndex < 0 || planeIndex >= profile.Planes.Count)
        {
            rows.Add(new FlightRow($"{heading}   (no plane)", string.Empty, FlightRowKind.Info));
            return;
        }

        var plane = profile.Planes[planeIndex];
        rows.Add(new FlightRow($"{heading}   {plane.Name}   {AirframeTitle(plane.Airframe)}",
            LoadoutBlock(plane), FlightRowKind.Info, Silhouette: plane.Airframe));
        rows.Add(new FlightRow("CHANGE AMMO", string.Empty, FlightRowKind.ChangeAmmo, slot));
        if (ChangePlaneAllowed(profile))
        {
            rows.Add(new FlightRow($"CHANGE PLANE: {plane.Name}", string.Empty, FlightRowKind.ChangePlane, slot));
        }
    }

    // The two rules FLIGHTCHECK.SCRIPT applies, both traced (docs/formats/campaign-screens.md,
    // "Plane change"): barred outright on the two story-aircraft-grant missions (ordinal 13 and
    // 17, MissionSeq+1), and barred while the profile owns fewer than three planes.
    private bool ChangePlaneAllowed(CampaignProfileDef profile)
    {
        int ordinal = Flow.MissionSeq + 1;
        return ordinal != 13 && ordinal != 17 && profile.Planes.Count >= 3;
    }

    private string AirframeTitle(int airframe) => Flow.Strings.Text(3000 + airframe, $"Airframe {airframe}");

    private string CalibreName(int caliber)
    {
        int idx = Math.Clamp((caliber - 30) / 10, 0, 4);
        return Flow.Strings.Text(3310 + idx, $".{30 + (idx * 10)}-cal.");
    }

    private string AmmoName(int index)
    {
        int i = Math.Clamp(index, 0, AmmoShortNames.Length - 1);
        return Flow.Strings.Text(3360 + i, AmmoShortNames[i]);
    }

    private string OrdnanceName(int index)
    {
        int i = Math.Clamp(index, 0, OrdnanceShortNames.Length - 1);
        return Flow.Strings.Text(3395 + i, OrdnanceShortNames[i]);
    }

    // The eight gun rows beside the eight rocket rows, one plane's full loadout block.
    private string LoadoutBlock(OwnedPlane plane)
    {
        var guns = ResolveGuns(plane);
        var (left, right) = ResolveHardpoints(plane);
        var lines = new List<string>(8);
        for (int row = 0; row < 8; row++)
        {
            string gun = GunRowText(guns, plane.Ammo, row);
            string rocket = RocketRowText(plane.Ordnance, left, right, row);
            int n = row + 1;
            lines.Add($"{n}){(gun.Length > 0 ? " " + gun : string.Empty)}" +
                      $"    {n}){(rocket.Length > 0 ? " " + rocket : string.Empty)}");
        }

        return string.Join("\n", lines);
    }

    private string GunRowText(FlightCheckGun?[] guns, IReadOnlyList<int> ammo, int row)
    {
        int group = row >> 1;
        if (guns[group] is not { } gun || (row % 2 == 1 && !gun.Twin))
        {
            return string.Empty;
        }

        int ammoIndex = group < ammo.Count ? ammo[group] : 0;
        return $"{CalibreName(gun.Caliber)} {AmmoName(ammoIndex)}";
    }

    private string RocketRowText(IReadOnlyList<int> ordnance, int left, int right, int row)
    {
        bool exists = row < 4 ? row < left : row - 4 < right;
        if (!exists)
        {
            return string.Empty;
        }

        int value = row < ordnance.Count ? ordnance[row] : NoOrdnance;
        return value == NoOrdnance ? string.Empty : OrdnanceName(value);
    }

    // A plane's four gun groups: a hangar build's own picks when one is on file under the plane's
    // name, else the airframe's stock fit (the two starter Devastators and every granted reward
    // aircraft carry no CustomPlaneStore entry — docs/org/hangar.md, "the campaign wallet").
    private FlightCheckGun?[] ResolveGuns(OwnedPlane plane)
    {
        var groups = new FlightCheckGun?[CustomPlaneDef.GunSlots];
        if (Planes?.Load(plane.Name) is { } custom)
        {
            for (int i = 0; i < custom.Guns.Length && i < groups.Length; i++)
            {
                if (custom.Guns[i].Calibre is { } calibre)
                {
                    groups[i] = new FlightCheckGun(30 + (calibre * 10), custom.Guns[i].Twin);
                }
            }

            return groups;
        }

        if (StockFor(plane.Airframe) is { } stock)
        {
            foreach (var gun in stock.Guns)
            {
                int slot = gun.Slot - 1;
                if (slot >= 0 && slot < groups.Length)
                {
                    groups[slot] = new FlightCheckGun(gun.Caliber, gun.Markers.Count >= 2);
                }
            }
        }

        return groups;
    }

    // A plane's per-wing hardpoint counts, same build-or-stock precedence as the guns; the stock
    // half reuses HangarFlow's own fill-order-to-wing split rather than re-deriving it.
    private (int Left, int Right) ResolveHardpoints(OwnedPlane plane)
    {
        if (Planes?.Load(plane.Name) is { } custom)
        {
            return (custom.LeftHardpoints, custom.RightHardpoints);
        }

        return HangarFlow.StockWingCounts(StockFor(plane.Airframe)?.Hardpoints);
    }

    private LoadoutDef? StockFor(int airframe) =>
        Stock?.For(AirframeDefKeys[Math.Clamp(airframe, 0, AirframeDefKeys.Length - 1)]);

    // The mission's objectives note (uiData 2022 / gosCallback 28,
    // docs/formats/campaign-screens.md): one line per IDENTITY'd objective, sorted and numbered by
    // its unique priority (docs/formats/objectives.md, "IDENTITY and the objectives display"), text
    // resolved through messages.json with the raw MSG_ key as its own fallback. Empty (never a
    // crash) when the data root, the mission or the file is unavailable.
    private string ObjectivesNote()
    {
        if (Flow.DataRoot is not { } root || Mission() is not { } mission)
        {
            return string.Empty;
        }

        try
        {
            string zrdrPath = SessionPaths.MissionZrdr(root, mission.ChapterFolder, mission.MissionFolder);
            var file = ZrdrDict.FromAlternating(Zrdr.LoadFileOrEmpty(zrdrPath, "objectives.json"));
            var messages = MessagesFor(root);
            var entries = new List<(int Priority, string Text)>();
            for (int n = 1; file.List($"OBJECTIVE{n}") is { } block; n++)
            {
                var identity = ZrdrDict.FromAlternating(block).List("IDENTITY");
                if (identity == null || identity.Count < 2 || identity[1] is not float priorityValue)
                {
                    continue;
                }

                int priority = (int)priorityValue;
                if (entries.Exists(e => e.Priority == priority))
                {
                    continue;
                }

                string? key = identity.Count >= 3 ? identity[2] as string : null;
                entries.Add((priority, messages.Get(key)));
            }

            entries.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            var lines = new List<string>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                lines.Add($"{i + 1}) {entries[i].Text}");
            }

            return string.Join("\n", lines);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FileNotFoundException)
        {
            return string.Empty;
        }
    }

    private CampaignMission? Mission()
    {
        if (_missionResolved)
        {
            return _mission;
        }

        _missionResolved = true;
        if (Flow.DataRoot is { } root)
        {
            try
            {
                string zrdrPath = SessionPaths.PreferUnzipped(Path.Combine(root, "extracted", "zrdr.zip"));
                foreach (var mission in CampaignSequence.Load(zrdrPath))
                {
                    if (mission.Seq == Flow.MissionSeq)
                    {
                        _mission = mission;
                        break;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or FileNotFoundException)
            {
                _mission = null;
            }
        }

        return _mission;
    }

    private Messages MessagesFor(string root) =>
        _messages ??= Messages.Load(Path.Combine(root, "extracted", "messages.json"));

    // One airframe's blueprint TGA, the hangar's own plane preview path
    // (extracted/rof/ASSETS/GRAPHICS/PX_<n>_BLUEPRINT.TGA — HangarAirframePage), reused rather
    // than authoring new silhouette art. Decoded once and kept, including a miss.
    private HangarArt? SilhouetteFor(int airframe)
    {
        if (_silhouettes.TryGetValue(airframe, out var art))
        {
            return art;
        }

        art = null;
        if (Flow.DataRoot is { } root)
        {
            string path = Path.Combine(root, "extracted", "rof", "ASSETS", "GRAPHICS",
                $"PX_{airframe}_BLUEPRINT.TGA");
            if (ArtImage.TryLoad(path) is { } image)
            {
                art = new HangarArt(image, AirframeTitle(airframe));
            }
        }

        _silhouettes[airframe] = art;
        return art;
    }

    // Which aircraft a row's art shows: its own where the row names a plane (the two pilot lines),
    // that slot's where the row acts on one (CHANGE AMMO, CHANGE PLANE), and the pilot's for the
    // two rows that belong to nobody, so the column does not blink out on the way to FLY MISSION.
    private int? Airframe(int row)
    {
        var rows = Rows();
        if (row < 0 || row >= rows.Count)
        {
            return null;
        }

        if (rows[row].Silhouette is { } own)
        {
            return own;
        }

        var flown = new List<int>();
        foreach (var candidate in rows)
        {
            if (candidate.Silhouette is { } airframe)
            {
                flown.Add(airframe);
            }
        }

        int slot = rows[row].Kind is FlightRowKind.ChangeAmmo or FlightRowKind.ChangePlane
            ? rows[row].Slot
            : 0;
        return flown.Count == 0 ? null : flown[Math.Min(slot, flown.Count - 1)];
    }

    // The head-on frame, cached the same way and for the same reason as the silhouette above.
    private HangarArt? DiagramFor(int airframe)
    {
        if (_diagrams.TryGetValue(airframe, out var art))
        {
            return art;
        }

        art = Flow.DataRoot is { } root
              && PlaneDiagrams.Frame(root, PlaneDiagrams.Front, airframe) is { } frame
            ? new HangarArt(frame, AirframeTitle(airframe))
            : null;
        _diagrams[airframe] = art;
        return art;
    }
}
