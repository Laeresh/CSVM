using System;
using System.Linq;
using CSVM.Flight.Hangar;

namespace CSVM.Session.Career;

/// <summary>The parts of a plane a hit can mark damaged: the four gun slots and the engine.</summary>
[Flags]
public enum PlaneParts
{
    None = 0,
    Gun1 = 1,
    Gun2 = 2,
    Gun3 = 4,
    Gun4 = 8,
    Engine = 16,
}

/// <summary>The four armour zones, in the saved-plane record's order.</summary>
public enum ArmourZone
{
    Nose,
    Tail,
    LeftWing,
    RightWing,
}

/// <summary>What a repair costs: dollars and machine-shop days.</summary>
public readonly record struct RepairQuote(int Cost, int Days)
{
    public bool IsNothing => Cost == 0 && Days == 0;
}

/// <summary>
/// One plane the gang owns: its hangar build, the armour each zone has lost and the parts marked
/// damaged. Damage persists between sorties until repaired; there is no airframe wear. The build
/// is a <see cref="CustomPlaneDef"/> unchanged, so the hangar and the economy read it as they read
/// any custom plane. Zone damage counts the record's armour units, five per hangar press. The two
/// wings stay independent, although the hangar's ARMOR tab sets them together.
/// </summary>
public sealed class PlaneInstance
{
    private readonly int[] _zoneDamage = new int[4];

    public PlaneInstance(CustomPlaneDef build)
    {
        Build = build ?? throw new ArgumentNullException(nameof(build));
    }

    public CustomPlaneDef Build { get; }

    public PlaneParts DamagedParts { get; set; }

    /// <summary>Sorties this plane has flown, kept as flavour.</summary>
    public int Sorties { get; set; }

    /// <summary>Kills scored in this plane, kept as flavour.</summary>
    public int Kills { get; set; }

    /// <summary>Whether any zone or part needs the machine shop.</summary>
    public bool NeedsRepair => DamagedParts != PlaneParts.None || TotalZoneDamage > 0;

    /// <summary>Whether the plane can fly a sortie: its engine works and no armoured zone is
    /// emptied. A damaged gun does not ground it.</summary>
    public bool CanFly =>
        !DamagedParts.HasFlag(PlaneParts.Engine)
        && Enum.GetValues<ArmourZone>().All(z => ZoneCapacity(z) == 0 || ZoneArmour(z) > 0);

    /// <summary>Armour units lost across all four zones.</summary>
    public int TotalZoneDamage => _zoneDamage[0] + _zoneDamage[1] + _zoneDamage[2] + _zoneDamage[3];

    /// <summary>The part flag of gun slot 0 to 3.</summary>
    public static PlaneParts GunPart(int slot) => (PlaneParts)(1 << slot);

    /// <summary>The armour units a zone holds when whole: the build's presses times five.</summary>
    public int ZoneCapacity(ArmourZone zone) => HangarEconomy.ArmourUnitsPerStep * zone switch
    {
        ArmourZone.Nose => Build.ArmourNose,
        ArmourZone.Tail => Build.ArmourTail,
        ArmourZone.LeftWing => Build.ArmourLeftWing,
        _ => Build.ArmourRightWing,
    };

    /// <summary>Armour units the zone has lost.</summary>
    public int ZoneDamage(ArmourZone zone) => _zoneDamage[(int)zone];

    /// <summary>Armour units the zone still holds.</summary>
    public int ZoneArmour(ArmourZone zone) => ZoneCapacity(zone) - ZoneDamage(zone);

    /// <summary>Sets the units a zone has lost, clamped to what the zone holds.</summary>
    public void SetZoneDamage(ArmourZone zone, int units) =>
        _zoneDamage[(int)zone] = Math.Clamp(units, 0, ZoneCapacity(zone));

    /// <summary>Adds to the units a zone has lost, as a sortie's result does.</summary>
    public void Damage(ArmourZone zone, int units) => SetZoneDamage(zone, ZoneDamage(zone) + Math.Max(0, units));

    /// <summary>The price and days of a full repair: every zone restored and every part mended.</summary>
    public RepairQuote QuoteRepair(CareerTunes tunes)
    {
        int units = TotalZoneDamage;
        int cost = units * tunes.RepairCostPerUnit;
        int days = units == 0 ? 0 : (units + tunes.RepairUnitsPerDay - 1) / tunes.RepairUnitsPerDay;
        var stats = HangarEconomy.Airframes[Build.Airframe];
        for (int slot = 0; slot < CustomPlaneDef.GunSlots; slot++)
        {
            if (DamagedParts.HasFlag(GunPart(slot)))
            {
                cost += PartRepair(HangarEconomy.GunLine(stats, Build.Guns[slot], slot).Cost, tunes);
                days += tunes.PartRepairDays;
            }
        }

        if (DamagedParts.HasFlag(PlaneParts.Engine))
        {
            cost += PartRepair(HangarEconomy.EngineLine(Build.Airframe, Build.Engine).Cost, tunes);
            days += tunes.PartRepairDays;
        }

        return new RepairQuote(cost, days);
    }

    /// <summary>Makes this plane a wreck: every zone emptied, the engine and every mounted gun
    /// damaged. It stays in the hangar, to be repaired at full cost.</summary>
    public void Wreck()
    {
        foreach (var zone in Enum.GetValues<ArmourZone>())
        {
            SetZoneDamage(zone, ZoneCapacity(zone));
        }

        DamagedParts |= PlaneParts.Engine;
        for (int slot = 0; slot < CustomPlaneDef.GunSlots; slot++)
        {
            if (!Build.Guns[slot].IsEmpty)
            {
                DamagedParts |= GunPart(slot);
            }
        }
    }

    /// <summary>Clears every zone's damage and every damaged part.</summary>
    public void Repair()
    {
        Array.Clear(_zoneDamage);
        DamagedParts = PlaneParts.None;
    }

    private static int PartRepair(int price, CareerTunes tunes) =>
        (int)Math.Round(price * tunes.PartRepairShare, MidpointRounding.AwayFromZero);
}
