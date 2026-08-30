using System;

namespace CSVM.Flight;

/// <summary>The purchase gate's answer. Capacity and engine presence only: pricing a build never
/// checks funds, so there is no insufficient-funds verdict.</summary>
public enum PurchaseVerdict
{
    /// <summary>Buildable: within the airframe's weight capacity, engine chosen.</summary>
    Ok,

    /// <summary>Total weight exceeds the airframe's capacity.</summary>
    Overweight,

    /// <summary>Engine id 6, the explicit no-engine pick.</summary>
    NoEngine,
}

/// <summary>One priced line of a build: dollars and pounds together, because every component in
/// the decoded economy carries both.</summary>
public readonly record struct CostWeight(int Cost, int Weight)
{
    public static CostWeight operator +(CostWeight a, CostWeight b) =>
        new(a.Cost + b.Cost, a.Weight + b.Weight);

    public static CostWeight operator *(CostWeight a, int k) => new(a.Cost * k, a.Weight * k);
}

/// <summary>One airframe's decoded stat row. <paramref name="TurretMask"/> bit i set means gun
/// slot i (0-based, matching <see cref="CustomPlaneDef.Guns"/>) mounts in a turret and takes the
/// gun table's turret price and weight columns. The four slot-title langui string ids ride along
/// for the GUNS screen.</summary>
public readonly record struct AirframeStats(
    int Cost,
    int Weight,
    int Capacity,
    int Agility,
    int Armour,
    int Availability,
    int TurretMask,
    int SlotTitle0,
    int SlotTitle1,
    int SlotTitle2,
    int SlotTitle3)
{
    /// <summary>Whether gun slot 0-3 is a turret mount.</summary>
    public bool IsTurretSlot(int slot) => (TurretMask & (1 << slot)) != 0;

    /// <summary>The langui string id titling gun slot 0-3.</summary>
    public int SlotTitle(int slot) => slot switch
    {
        0 => SlotTitle0,
        1 => SlotTitle1,
        2 => SlotTitle2,
        3 => SlotTitle3,
        _ => throw new ArgumentOutOfRangeException(nameof(slot)),
    };
}

/// <summary>One gun calibre's decoded price row; the airframe's turret bit for the slot picks
/// the column pair.</summary>
public readonly record struct GunStats(int WingCost, int TurretCost, int WingWeight, int TurretWeight);

/// <summary>One airframe's engine base line; the chosen engine id 0-5 then shifts cost and
/// weight by the shared offset tables. Power is carried for display, not priced.</summary>
public readonly record struct EngineBase(int Cost, int Weight, int Power);

/// <summary>
/// The hangar's decoded economy: prices a <see cref="CustomPlaneDef"/> exactly as the original
/// does. The tables and formulas are verbatim transcriptions; their provenance, with addresses,
/// is docs/org/hangar.md "The airframe stat table" and "The economy". Pure by design: no Godot,
/// no UI, so a campaign layer can reuse it unchanged.
/// </summary>
public static class HangarEconomy
{
    /// <summary>Cost and weight per hardpoint.</summary>
    public const int HardpointCost = 410;
    public const int HardpointWeight = 480;

    /// <summary>Cost and weight per armour unit. The ARMOR screen's lb readout multiplies units
    /// by five instead; that factor is display-only and must not leak in here.</summary>
    public const int ArmourUnitCost = 4;
    public const int ArmourUnitWeight = 4;

    /// <summary>The 11-airframe stat table, ids 0-10.</summary>
    public static readonly AirframeStats[] Airframes =
    {
        new(6800, 1400, 4160, 20, 60, 12, 0x00, 3061, 3070, 3062, 3069),
        new(4548, 3255, 9525, 12, 95, 16, 0x08, 3071, 3072, 3061, 3073),
        new(1870, 5460, 15760, -2, 125, 3, 0x0c, 3061, 3062, 3060, 3073),
        new(5568, 2415, 6545, 19, 80, 8, 0x00, 3061, 3062, 3063, 3064),
        new(3783, 3885, 11015, 8, 105, 6, 0x08, 3062, 3069, 3061, 3073),
        new(4250, 3500, 10100, 10, 100, 1, 0x00, 3074, 3075, 3076, 3077),
        new(2763, 4725, 14035, 3, 110, 12, 0x08, 3061, 3079, 3062, 3073),
        new(5015, 2870, 7610, 16, 90, 7, 0x00, 3062, 3069, 3061, 3070),
        new(2890, 4620, 13780, 4, 110, 2, 0x08, 3065, 3078, 3062, 3073),
        new(5228, 2695, 7205, 17, 85, 3, 0x00, 3065, 3066, 3067, 3068),
        new(2423, 5005, 14675, 2, 120, 19, 0x00, 3061, 3070, 3062, 3069),
    };

    /// <summary>The 5-calibre gun price table, gun ids 0-4 (.30 through .70).</summary>
    public static readonly GunStats[] GunTable =
    {
        new(240, 440, 280, 520),
        new(320, 530, 380, 620),
        new(410, 610, 480, 720),
        new(490, 700, 580, 820),
        new(580, 780, 680, 920),
    };

    /// <summary>Per-airframe engine base lines, airframe ids 0-10.</summary>
    public static readonly EngineBase[] EngineBases =
    {
        new(850, 1000, 200),
        new(1700, 2000, 261),
        new(2550, 3000, 126),
        new(850, 1000, 300),
        new(1700, 2000, 240),
        new(1700, 2000, 251),
        new(2550, 3000, 207),
        new(850, 1000, 281),
        new(2550, 3000, 215),
        new(850, 1000, 290),
        new(2550, 3000, 201),
    };

    /// <summary>Cost offsets added to the airframe's engine base, engine ids 0-5.</summary>
    public static readonly int[] EngineCostOffsets = { -425, 0, 425, 5, 430, 855 };

    /// <summary>Weight offsets added to the airframe's engine base, engine ids 0-5.</summary>
    public static readonly int[] EngineWeightOffsets = { -500, 0, 500, 0, 500, 1000 };

    /// <summary>The per-engine-id factors the original's power stat line multiplies the base
    /// rating by (the double table at 0x00619e38): the three tiers, then the same three with
    /// nitrous, exactly x1.33. Display-only, like the rating itself.</summary>
    public static readonly double[] EnginePowerFactors = { 0.9, 1.0, 1.1, 1.197, 1.33, 1.463 };

    /// <summary>Prices one build. The def is read as-is; call <see cref="CustomPlaneDef.Clamp"/>
    /// first if it came from outside the screens.</summary>
    public static HangarBill Price(CustomPlaneDef def)
    {
        var stats = Airframes[def.Airframe];
        var engine = EngineLine(def.Airframe, def.Engine);
        var guns = new CostWeight[CustomPlaneDef.GunSlots];
        for (int slot = 0; slot < guns.Length; slot++)
        {
            guns[slot] = GunLine(stats, def.Guns[slot], slot);
        }

        int armourUnits = def.ArmourNose + def.ArmourTail + def.ArmourLeftWing + def.ArmourRightWing;
        var armour = new CostWeight(armourUnits * ArmourUnitCost, armourUnits * ArmourUnitWeight);
        int hardpoints = def.LeftHardpoints + def.RightHardpoints;
        var hp = new CostWeight(hardpoints * HardpointCost, hardpoints * HardpointWeight);

        var total = new CostWeight(stats.Cost, stats.Weight) + engine + armour + hp;
        foreach (var g in guns)
        {
            total += g;
        }

        return new HangarBill
        {
            Airframe = new CostWeight(stats.Cost, stats.Weight),
            Engine = engine,
            Guns = guns,
            Armour = armour,
            Hardpoints = hp,
            Total = total,
            Capacity = stats.Capacity,
            Verdict = total.Weight > stats.Capacity ? PurchaseVerdict.Overweight
                : def.Engine == CustomPlaneDef.EngineNone ? PurchaseVerdict.NoEngine
                : PurchaseVerdict.Ok,
            AgilityStars = Math.Min((stats.Agility - 1) / 4, 4),
            // The star formula reads the record's stored armour, which is units x5; the only
            // place that factor enters arithmetic rather than display.
            ArmourStars = Math.Min((stats.Armour + (armourUnits * 5) - 1) / 0x49, 4),
        };
    }

    /// <summary>The engine line for one airframe and engine id; id 6 (none) is zero.</summary>
    public static CostWeight EngineLine(int airframe, int engineId)
    {
        if (engineId == CustomPlaneDef.EngineNone)
        {
            return default;
        }

        var b = EngineBases[airframe];
        return new CostWeight(b.Cost + EngineCostOffsets[engineId], b.Weight + EngineWeightOffsets[engineId]);
    }

    /// <summary>The displayed power stat for one airframe and engine id: the base rating times
    /// the id's factor, truncated as the original truncates. Id 6 (none) is zero.</summary>
    public static int PowerStat(int airframe, int engineId)
    {
        if (engineId < 0 || engineId >= EnginePowerFactors.Length)
        {
            return 0;
        }

        return (int)(EngineBases[airframe].Power * EnginePowerFactors[engineId]);
    }

    private static CostWeight GunLine(AirframeStats stats, GunChoice gun, int slot)
    {
        if (gun.Calibre is not { } calibre)
        {
            return default;
        }

        var row = GunTable[calibre];
        var one = stats.IsTurretSlot(slot)
            ? new CostWeight(row.TurretCost, row.TurretWeight)
            : new CostWeight(row.WingCost, row.WingWeight);
        return gun.Twin ? one * 2 : one;
    }
}

/// <summary>
/// Everything the economy says about one build: the per-line costs and weights, the two totals,
/// the capacity they are judged against, the purchase verdict, and the two star ratings.
/// <see cref="AgilityStars"/> and <see cref="ArmourStars"/> are display-only: the original
/// renders them as stars and nothing else consumes them.
/// </summary>
public sealed class HangarBill
{
    /// <summary>The airframe's own price line (stat table cost and weight).</summary>
    public CostWeight Airframe { get; init; }

    /// <summary>The engine line: per-airframe base plus per-id offsets; zero when engineless.</summary>
    public CostWeight Engine { get; init; }

    /// <summary>Per gun slot: wing or turret column per the airframe's turret bit, doubled when
    /// twinned, zero when empty. Fixed length <see cref="CustomPlaneDef.GunSlots"/>.</summary>
    public CostWeight[] Guns { get; init; } = new CostWeight[CustomPlaneDef.GunSlots];

    /// <summary>The armour line: total units across the four zones, x4 for cost AND weight.
    /// The x5 lb figure the ARMOR screen shows is display-only and not priced here.</summary>
    public CostWeight Armour { get; init; }

    /// <summary>The hardpoint line: both wings' counts at $410 / 480 lb each.</summary>
    public CostWeight Hardpoints { get; init; }

    /// <summary>The grand total, the sum of every line.</summary>
    public CostWeight Total { get; init; }

    /// <summary>The airframe's weight capacity the total is judged against.</summary>
    public int Capacity { get; init; }

    /// <summary>The purchase gate's answer for this build.</summary>
    public PurchaseVerdict Verdict { get; init; }

    /// <summary>Display-only agility stars 0-4.</summary>
    public int AgilityStars { get; init; }

    /// <summary>Display-only armour stars 0-4.</summary>
    public int ArmourStars { get; init; }
}
