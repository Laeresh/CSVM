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
/// <para>Reads and writes the profile through <see cref="CampaignProfileStore"/>'s public API.
/// A mission-reward aircraft (docs/org/hangar.md, the reward table at <c>0x0061ae80</c>) is
/// recognised by the <see cref="OwnedPlane.Special"/> flag <see cref="CampaignProgression"/> sets
/// when it grants one: the original's class-2 record, which its sell handler refuses (langui 704
/// <c>IDS_PS_SPECIALPLANE</c>).</para>
/// </summary>
public sealed class HangarCampaignContext
{
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

    /// <summary>Whether the named owned plane is an unsellable reward aircraft.</summary>
    public bool IsSpecial(string planeName) =>
        Profile.Planes.Any(p => p.Special && string.Equals(p.Name, planeName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether the named owned plane can be sold right now: it is actually owned, it is
    /// not one of the five unsellable reward aircraft, and at least two planes remain afterwards
    /// (docs/org/hangar.md, langui 701, the free-slot finder's own floor).</summary>
    public bool CanSell(string planeName) =>
        Profile.Planes.Any(p => string.Equals(p.Name, planeName, StringComparison.OrdinalIgnoreCase))
        && !IsSpecial(planeName)
        && Profile.Planes.Count > 2;

    /// <summary>The profile's own aircraft as buildable defs, in the ownership list's own order,
    /// which is the campaign hangar's roster (langui 1257 INVENTORY). Ownership is what separates
    /// the two modes, not storage: an Instant Action build sits in the same
    /// <c>user://Planes/</c> directory and is absent here because the profile does not own it.</summary>
    public IReadOnlyList<CustomPlaneDef> OwnedBuilds()
    {
        var builds = new List<CustomPlaneDef>(Profile.Planes.Count);
        foreach (var owned in Profile.Planes)
        {
            builds.Add(BuildFor(owned));
        }

        return builds;
    }

    /// <summary>The full build cost of an owned plane, the decoded sell price (no depreciation,
    /// docs/org/hangar.md "The sell price is the full build cost"). A name the profile does not
    /// own is priced from the build store alone, on the campaign's starting-Devastator spec where
    /// even that is absent.</summary>
    public int SellPrice(string planeName)
    {
        var owned = Profile.Planes.FirstOrDefault(
            p => string.Equals(p.Name, planeName, StringComparison.OrdinalIgnoreCase));
        return HangarEconomy.Price(BuildFor(owned ?? new OwnedPlane { Name = planeName, Airframe = 5 }))
            .Total.Cost;
    }

    /// <summary>Deducts a completed build's total cost, records ownership and saves the profile.
    /// The build itself is already in <see cref="CustomPlaneStore"/> (the flow's own commit); this
    /// only moves money and names the plane into the profile's ownership list.</summary>
    public void Purchase(string planeName, int airframe, int cost)
    {
        Profile.Funds -= cost;
        var owned = Profile.Planes.FirstOrDefault(
            p => string.Equals(p.Name, planeName, StringComparison.OrdinalIgnoreCase));

        // ⚠ Do not add a second record for a name already owned. The commit writes one file per
        // name, so two records would name one aeroplane twice and a later sale would remove both.
        if (owned != null)
        {
            owned.Airframe = airframe;
        }
        else
        {
            Profile.Planes.Add(new OwnedPlane { Name = planeName, Airframe = airframe });
        }

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

    // What one ownership record flies. A hangar-built plane is its stored build; a reward aircraft
    // granted before the store held one falls back to its own award template (the same order
    // LaunchMenu's launch path resolves in); the two profile-seeded starters are never built at
    // all, so they take the campaign's own Devastator spec (docs/org/hangar.md, "The campaign
    // instead starts with two aircraft": engine 1, two hardpoints per wing, no armour or guns).
    private CustomPlaneDef BuildFor(OwnedPlane owned)
    {
        var built = _planes.Load(owned.Name) ?? CampaignProgression.BuildForOwned(owned);
        if (built == null)
        {
            built = new CustomPlaneDef
            {
                Airframe = owned.Airframe,
                Engine = 1,
                LeftHardpoints = 2,
                RightHardpoints = 2,
            };
        }

        built.Name = owned.Name;
        return built;
    }
}
