using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Session;

namespace CSVM.UI;

/// <summary>
/// What one campaign aircraft is actually carrying: how many barrels of each calibre, how many
/// hardpoints, how much armour and which engine. Resolved from the plane's hangar build when it has
/// one, and from its airframe's stock fit when it does not, which is the case for the two
/// profile-seeded starters and every granted reward aircraft (<c>docs/org/hangar.md</c>, "the
/// campaign wallet"). Engine-free, so the screens that draw it test off engine.
/// </summary>
public sealed class PlaneFit
{
    // A record with no build carries no engine of its own, so its rating reads at the middle tier's
    // factor: the stock fit names guns and hardpoints and says nothing about the engine.
    private const int StockEngine = 1;

    private PlaneFit(Dictionary<int, int> barrels, int hardpoints, int armourUnits, int engine)
    {
        Barrels = barrels;
        Hardpoints = hardpoints;
        ArmourUnits = armourUnits;
        Engine = engine;
    }

    /// <summary>How many barrels of each calibre in millimetres (30 to 70), by calibre.</summary>
    public IReadOnlyDictionary<int, int> Barrels { get; }

    /// <summary>How many underwing hardpoints, both wings.</summary>
    public int Hardpoints { get; }

    /// <summary>How many armour units are fitted, zero for a plane with no build.</summary>
    public int ArmourUnits { get; }

    /// <summary>The engine id, or the middle tier for a plane with no build.</summary>
    public int Engine { get; }

    /// <summary>Resolves a record. <paramref name="build"/> is its hangar build or null;
    /// <paramref name="stock"/> is its airframe's stock fit, used only when there is no build.
    /// ⚠ The caller resolves the build, never this: a stock record is named for its airframe, and
    /// looking one up by name would fit it with a hangar plane that happens to share the name
    /// (<see cref="CampaignFlightField.IsStock"/>).</summary>
    public static PlaneFit For(CustomPlaneDef? build, LoadoutDef? stock)
    {
        var barrels = new Dictionary<int, int>();
        if (build != null)
        {
            foreach (var gun in build.Guns)
            {
                if (gun.Calibre is { } calibre)
                {
                    int mm = 30 + (calibre * 10);
                    barrels[mm] = barrels.GetValueOrDefault(mm) + (gun.Twin ? 2 : 1);
                }
            }

            int armour = build.ArmourNose + build.ArmourTail + build.ArmourLeftWing + build.ArmourRightWing;
            return new PlaneFit(
                barrels, build.LeftHardpoints + build.RightHardpoints, armour, build.Engine);
        }

        foreach (var gun in stock?.Guns ?? new List<GunSpec>())
        {
            barrels[gun.Caliber] = barrels.GetValueOrDefault(gun.Caliber) + Math.Max(1, gun.Markers.Count);
        }

        return new PlaneFit(barrels, stock?.Hardpoints?.Count ?? 0, 0, StockEngine);
    }
}
