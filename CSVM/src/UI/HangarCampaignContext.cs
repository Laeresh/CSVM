using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight;
using CSVM.Session;

namespace CSVM.UI;

/// <summary>
/// The optional campaign wallet a hangar flow prices against. Absent (null) for every existing
/// door — the IA Build button and the top-level launchscreen entry — keeping those paths
/// wallet-free (PLAN-hangar Decision 2). Present only when the cabin's Plane Construction opens
/// the flow over a selected <see cref="CampaignProfileDef"/> (B13; PLAN-hangar Decision 9's seam).
///
/// <para>Reads and writes the profile through <see cref="CampaignProfileStore"/>'s existing public
/// API only — no change to that file. The five named mission-reward aircraft (docs/org/hangar.md,
/// the reward table at <c>0x0061ae80</c>) are recognised by their decoded names pending B12's own
/// per-plane ownership flag.</para>
/// </summary>
public sealed class HangarCampaignContext
{
    /// <summary>The five reward aircraft (docs/org/hangar.md, the mission reward table): class-2
    /// specials the original refuses to sell (langui 704 <c>IDS_PS_SPECIALPLANE</c>). Named by
    /// their decoded <c>langui</c> strings 513-517, not invented.</summary>
    public static readonly IReadOnlySet<string> SpecialPlaneNames = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "Jumping Jane", "Blue Streak", "Red Hot Spender", "Minx", "Accipiter Annie",
    };

    private readonly CampaignProfileStore _store;
    private readonly CustomPlaneStore _planes;

    /// <summary>Wraps an already-loaded profile. <paramref name="planes"/> is the global build
    /// store (<c>user://Planes/</c>) the profile's owned planes name into.</summary>
    public HangarCampaignContext(CampaignProfileStore store, CampaignProfileDef profile, CustomPlaneStore planes)
    {
        _store = store;
        Profile = profile;
        _planes = planes;
    }

    /// <summary>The profile this context prices and gates against. Mutated in place by
    /// <see cref="Purchase"/>/<see cref="Sell"/> and re-saved through the store's own public
    /// <c>Save</c>.</summary>
    public CampaignProfileDef Profile { get; }

    /// <summary>The wallet balance (docs/org/hangar.md "The campaign wallet"): $0 on a fresh
    /// profile, since the first mission has not yet paid out.</summary>
    public int Funds => Profile.Funds;

    /// <summary>Whether the build's total cost is affordable right now.</summary>
    public bool CanAfford(int cost) => Profile.Funds >= cost;

    /// <summary>Whether airframe <paramref name="airframe"/> is offered yet: the stat table's own
    /// availability threshold (<c>0x00619bb0+0x14</c>, <see cref="HangarEconomy.Airframes"/>)
    /// against the save's progress counter (<c>UIData +0x338</c> / <c>DAT_0064b678</c>, this
    /// profile's <see cref="CampaignProfileDef.MissionsCompleted"/>), exactly the comparison
    /// <c>FUN_00410120</c> makes (<c>DAT_0064b678 + 1</c> against the threshold). PLAN-hangar
    /// Decision 9 shipped this field wired to nothing outside Instant Action; this is the wire.</summary>
    public bool IsAirframeAvailable(int airframe) =>
        Profile.MissionsCompleted + 1 >= HangarEconomy.Airframes[airframe].Availability;

    /// <summary>Whether the named owned plane is one of the five unsellable reward aircraft.</summary>
    public bool IsSpecial(string planeName) => SpecialPlaneNames.Contains(planeName);

    /// <summary>Whether the named owned plane can be sold right now: it is actually owned, it is
    /// not one of the five unsellable reward aircraft, and at least two planes remain afterwards
    /// (docs/org/hangar.md, langui 701, the free-slot finder's own floor).</summary>
    public bool CanSell(string planeName) =>
        Profile.Planes.Any(p => string.Equals(p.Name, planeName, StringComparison.OrdinalIgnoreCase))
        && !IsSpecial(planeName)
        && Profile.Planes.Count > 2;

    /// <summary>The full build cost of an owned plane, the decoded sell price (no depreciation,
    /// docs/org/hangar.md "The sell price is the full build cost"). Falls back to the campaign's
    /// own starting-Devastator spec (airframe from the ownership record, engine 1, two hardpoints
    /// per wing, no armour or guns — "The campaign instead starts with two aircraft") when the
    /// name never went through <see cref="CustomPlaneStore"/>, true only of the two profile-seeded
    /// starters, which are never hangar-built.</summary>
    public int SellPrice(string planeName)
    {
        var built = _planes.Load(planeName);
        if (built != null)
        {
            return HangarEconomy.Price(built).Total.Cost;
        }

        var owned = Profile.Planes.FirstOrDefault(
            p => string.Equals(p.Name, planeName, StringComparison.OrdinalIgnoreCase));
        var starter = new CustomPlaneDef
        {
            Name = planeName,
            Airframe = owned?.Airframe ?? 5,
            Engine = 1,
            LeftHardpoints = 2,
            RightHardpoints = 2,
        };
        return HangarEconomy.Price(starter).Total.Cost;
    }

    /// <summary>Deducts a completed build's total cost, records ownership and saves the profile.
    /// The build itself is already in <see cref="CustomPlaneStore"/> (the flow's own commit); this
    /// only moves money and names the plane into the profile's ownership list.</summary>
    public void Purchase(string planeName, int airframe, int cost)
    {
        Profile.Funds -= cost;
        Profile.Planes.Add(new OwnedPlane { Name = planeName, Airframe = airframe });
        _store.Save(Profile);
    }

    /// <summary>Credits the wallet at the plane's full build cost, removes it from the profile's
    /// ownership list and deletes the underlying build (the original's sell clears the slot).
    /// Returns false without changing anything when <see cref="CanSell"/> would refuse; callers
    /// that already checked get a defensive no-op rather than a double credit.</summary>
    public bool Sell(string planeName)
    {
        if (!CanSell(planeName))
        {
            return false;
        }

        int price = SellPrice(planeName);
        Profile.Funds += price;
        Profile.Planes.RemoveAll(p => string.Equals(p.Name, planeName, StringComparison.OrdinalIgnoreCase));
        _store.Save(Profile);
        _planes.Delete(planeName);
        return true;
    }
}
