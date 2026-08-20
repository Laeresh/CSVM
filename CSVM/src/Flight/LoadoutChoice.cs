using System;
using System.Collections.Generic;

namespace CSVM.Flight;

/// <summary>One row of an Ammo Selection dropdown: the value the rig consumes and the label the
/// original's screen shows for it. Both are authored because they diverge — the gun matrix's
/// <c>magnesium</c> reads "Explosive" on the screen, and a pylon's <c>wep_NN</c> has no
/// player-facing name at all.</summary>
public readonly record struct LoadoutOption(string Id, string Label);

/// <summary>The Ammo Selection screen's two dropdown rosters, in the original's own order
/// (<c>OriginalScreenshots/Ammo Selection *.png</c>). Authored, never derived: the pylon roster is
/// NOT the <c>wep_04</c>–<c>15</c> tier, since the incendiary <c>wep_04</c> is a named weapon the
/// screen does not offer, and the order is not id order either. The screen renders these lists as
/// they stand and must not sort them.</summary>
public sealed class LoadoutOptions
{
    /// <summary>The ammo dropdown, one entry per <see cref="StockLoadouts"/> ammo name.</summary>
    public IReadOnlyList<LoadoutOption> GunAmmo { get; init; } = Array.Empty<LoadoutOption>();

    /// <summary>The rocket dropdown, one entry per offered <c>wep_NN</c>.</summary>
    public IReadOnlyList<LoadoutOption> PylonOrdnance { get; init; } = Array.Empty<LoadoutOption>();
}

/// <summary>
/// One pilot's edits to a fit, keyed by <b>slot identity</b> and never by position in a def's
/// arrays: gun slots 1–4 and pylons 1–8 are the formats' own ceilings (eight firepoints taken in
/// pairs, <see cref="Loadout.PylonFillOrder"/>'s eight). That is what lets one choice apply to a
/// base it was not built against — a custom plane's saved fit as much as a stock one — with a
/// pick for a slot the base lacks simply dropped.
///
/// <para>A null entry means "as the base authored it", which is what makes reset-to-stock a clear
/// rather than a rebuild. <see cref="None"/> is a different thing: an explicit empty mount.</para>
/// </summary>
public sealed class LoadoutChoice
{
    /// <summary>The explicit empty pick, last on both of the original's dropdowns. Distinct from a
    /// null entry, which is no choice at all.</summary>
    public const string None = "none";

    /// <summary>Gun slots W1–W4, the eight firepoints taken in pairs.</summary>
    public const int MaxGunSlot = 4;

    /// <summary>Pylons 1–8, <see cref="Loadout.PylonFillOrder"/>'s length.</summary>
    public const int MaxPylon = 8;

    private readonly string?[] _gunAmmo = new string?[MaxGunSlot];
    private readonly string?[] _pylons = new string?[MaxPylon];

    /// <summary>Whether nothing has been picked, so applying this would return the base unchanged.</summary>
    public bool IsStock
    {
        get
        {
            foreach (var a in _gunAmmo)
            {
                if (a != null)
                {
                    return false;
                }
            }

            foreach (var p in _pylons)
            {
                if (p != null)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>The ammo picked for gun slot <paramref name="slot"/> (1-based), null for none
    /// picked. A slot outside 1–<see cref="MaxGunSlot"/> reads null rather than throwing, so a
    /// base with an unexpected slot number is dropped instead of crashing the menu.</summary>
    public string? GunAmmoFor(int slot) =>
        slot >= 1 && slot <= MaxGunSlot ? _gunAmmo[slot - 1] : null;

    /// <summary>Picks gun slot <paramref name="slot"/>'s ammo; null clears it back to the base's.</summary>
    public void SetGunAmmo(int slot, string? ammo)
    {
        if (slot >= 1 && slot <= MaxGunSlot)
        {
            _gunAmmo[slot - 1] = ammo;
        }
    }

    /// <summary>The weapon picked for physical pylon <paramref name="pylon"/> (1-based), null for
    /// none picked. Out of range reads null, for the same reason <see cref="GunAmmoFor"/> does.</summary>
    public string? PylonFor(int pylon) =>
        pylon >= 1 && pylon <= MaxPylon ? _pylons[pylon - 1] : null;

    /// <summary>Picks physical pylon <paramref name="pylon"/>'s weapon; null clears it.</summary>
    public void SetPylon(int pylon, string? weaponId)
    {
        if (pylon >= 1 && pylon <= MaxPylon)
        {
            _pylons[pylon - 1] = weaponId;
        }
    }

    /// <summary>Drops every pick, so the next <see cref="ApplyTo"/> returns the base's own fit.</summary>
    public void ResetToStock()
    {
        Array.Clear(_gunAmmo);
        Array.Clear(_pylons);
    }

    /// <summary>This choice laid over <paramref name="stock"/>, as a new def — the base is never
    /// mutated. The base is handed in rather than looked up so a custom plane's saved fit works
    /// here unchanged. A gun slot picked <see cref="None"/> is omitted from the result entirely
    /// rather than built with no rounds, so it never occupies a slot in the gun cycle.</summary>
    public LoadoutDef ApplyTo(LoadoutDef stock)
    {
        var def = new LoadoutDef { Def = stock.Def, Model = stock.Model, Display = stock.Display };
        foreach (var gun in stock.Guns)
        {
            // A turret is not on the screen (built inert), so it keeps the base's ammo whatever
            // a stale pick for its slot says.
            string? pick = gun.Turret ? null : GunAmmoFor(gun.Slot);
            if (pick == None)
            {
                continue;
            }

            def.Guns.Add(CloneGun(gun, pick));
        }

        if (stock.Hardpoints is { } hardpoints)
        {
            def.Hardpoints = ApplyPylons(hardpoints);
        }

        return def;
    }

    private static GunSpec CloneGun(GunSpec gun, string? ammo)
    {
        var spec = new GunSpec
        {
            Slot = gun.Slot,
            Mount = gun.Mount,
            Caliber = gun.Caliber,
            Ammo = ammo ?? gun.Ammo,
            Turret = gun.Turret,
            Rounds = gun.Rounds,

            // A picked ammo has to resolve through caliber + ammo, so an inherited explicit id
            // must go: Loadout.Bind prefers WeaponId and would otherwise ignore the pick.
            WeaponId = ammo == null ? gun.WeaponId : null,
        };
        spec.Markers.AddRange(gun.Markers);
        return spec;
    }

    private HardpointSpec ApplyPylons(HardpointSpec hardpoints)
    {
        var stock = new string[hardpoints.Count];
        for (int i = 0; i < stock.Length; i++)
        {
            // Entry i sits on PylonFillOrder[i] physically. A None keeps its entry as the
            // sentinel instead of shortening the array, because dropping one would slide every
            // later pylon onto a different wing.
            int pylon = i < Loadout.PylonFillOrder.Length ? Loadout.PylonFillOrder[i] : 0;
            stock[i] = PylonFor(pylon) ?? (i < hardpoints.Stock.Length ? hardpoints.Stock[i] : None);
        }

        return new HardpointSpec { Count = hardpoints.Count, Stock = stock, Rounds = hardpoints.Rounds };
    }
}
