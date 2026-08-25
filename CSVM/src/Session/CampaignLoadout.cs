using CSVM.Flight;

namespace CSVM.Session;

/// <summary>
/// The bridge between a campaign profile's stored picks and a flying aircraft's fit: one
/// <see cref="OwnedPlane"/>'s <see cref="OwnedPlane.Ammo"/> and <see cref="OwnedPlane.Ordnance"/>
/// arrays as the <see cref="LoadoutChoice"/> the launch hands the session, which
/// <see cref="Loadout.Bind"/> then lays over the aircraft's own base fit. Engine-free, so the two
/// encodings test off engine; both are the plan's C25 section, not the save format's (the
/// original's per-pylon ordnance id is undecoded).
/// </summary>
public static class CampaignLoadout
{
    /// <summary>How many rows the Ammo Selection screen's rocket table has
    /// (<c>docs/formats/campaign-screens.md</c>), which closes the ordnance vocabulary.</summary>
    public const int PylonRows = 12;

    /// <summary>What <see cref="OwnedPlane.Ammo"/>'s index 0..3 names, in the gun matrix's own
    /// order. Index 4 is the record's no-gun marker, which is not an ammunition at all.</summary>
    public static readonly string[] AmmoNames = { "slug", "dumdum", "ap", "magnesium" };

    // The row an unset pylon stands for: high explosive, the universal stock fit (loadouts.md).
    private const int StockPylonRow = 1;

    // The profile record's "this slot mounts no gun" marker, the fifth value of a four-value
    // ammunition field (docs/formats/campaign-screens.md, "Ammo selection").
    private const int NoGun = 4;

    /// <summary>The rocket-table row (0..11) a stored pylon value names. CSVM's own
    /// <see cref="OwnedPlane.Ordnance"/> encoding is one-based with 0 meaning "never picked", the
    /// stand-in the plan's C25 chose over the save format's plain table index; every reader of the
    /// field goes through here so the two spaces cannot drift apart on one screen.</summary>
    public static int PylonRow(int stored) =>
        stored >= 1 && stored <= PylonRows ? stored - 1 : StockPylonRow;

    /// <summary>The plane's stored picks as one choice. <paramref name="stock"/> is the table an
    /// ordnance row's <c>wep_*</c> id comes from; without it the pylons keep the base's fit rather
    /// than being cleared, since a table index means nothing without the table. An unset pylon
    /// (stored 0) is also left to the base: the base's stock fit is the universal high-explosive
    /// load that unset stands for, so writing it back changes nothing.</summary>
    public static LoadoutChoice For(OwnedPlane plane, StockLoadouts? stock)
    {
        var fit = new LoadoutChoice();
        for (int slot = 0; slot < LoadoutChoice.MaxGunSlot && slot < plane.Ammo.Length; slot++)
        {
            int ammo = plane.Ammo[slot];
            if (ammo >= 0 && ammo < AmmoNames.Length)
            {
                fit.SetGunAmmo(slot + 1, AmmoNames[ammo]);
            }
            else if (ammo == NoGun)
            {
                fit.SetGunAmmo(slot + 1, LoadoutChoice.None);
            }
        }

        var table = stock?.Options.PylonOrdnance;
        if (table == null || table.Count == 0)
        {
            return fit;
        }

        // Cells 0-3 are the left wing and 4-7 the right, which is physical pylon cell+1: pylons
        // 1-4 left, 5-8 right, the split Loadout.PylonFillOrder itself carries.
        for (int cell = 0; cell < LoadoutChoice.MaxPylon && cell < plane.Ordnance.Length; cell++)
        {
            int row = plane.Ordnance[cell] - 1;
            if (row >= 0 && row < table.Count)
            {
                fit.SetPylon(cell + 1, table[row].Id);
            }
        }

        return fit;
    }
}
