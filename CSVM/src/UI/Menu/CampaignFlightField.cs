using System;
using System.Collections.Generic;
using CSVM.Session.Campaign;

namespace CSVM.UI.Menu;

/// <summary>
/// The humans flying one campaign sortie, as the flight check walks them: how many joined, whose
/// check is showing, and which aeroplane each guest flies. The seated player is 0 and keeps the
/// profile's own aircraft. Players 1 and up are guests, who bring no profile and fly what the co-op
/// allocation (<see cref="CoopPlanePool"/>) gives their seat. That is a COPY of one of the seated
/// profile's aircraft with its own fit, or a stock Devastator. A network guest draws from the same
/// pool after these seats. Engine-free and presentation-neutral, and owned by the
/// <see cref="CampaignFeature"/>, so both presentations walk the same allocation and player index.
/// </summary>
public sealed class CampaignFlightField
{
    private readonly CampaignFeature _feature;
    private readonly List<OwnedPlane> _guests = new();

    // Every stock record handed out, by reference: a screen resolving one plane's guns asks here
    // rather than reading CustomPlaneStore under a name an airframe title could collide with.
    private readonly HashSet<OwnedPlane> _stock = new();

    // What the guest records were built from. The allocation reads only the profile instance, its
    // two crew picks, its plane count and the joined count. A change to any of them rebuilds.
    private (CampaignProfileDef? Profile, int Selected, int Wingman, int Planes, int Players) _builtFor;

    private int _players = 1;
    private bool _locked;

    internal CampaignFlightField(CampaignFeature feature) => _feature = feature;

    /// <summary>How many humans are flying, 1 to <see cref="PlayerSetupFeature.MaxSeats"/>.</summary>
    public int Players => _players;

    /// <summary>Whose flight check is showing: 0 the seated player, 1 and up a guest.</summary>
    public int Current { get; private set; }

    /// <summary>Whether the seated player has committed the field by pressing FLY MISSION.</summary>
    public bool Locked => _locked;

    /// <summary>The aeroplane each guest flies, in player order. Empty for a solo campaign.</summary>
    public IReadOnlyList<OwnedPlane> Guests
    {
        get
        {
            EnsureGuests();
            return _guests;
        }
    }

    /// <summary>Takes the joined-player count from the shell. A count that shrank drops the
    /// trailing guests and pulls the cursor back into the field, so a guest leaving mid-sequence
    /// cannot leave the screen showing a check nobody is flying.</summary>
    public void SetPlayers(int players)
    {
        _players = Math.Clamp(players, 1, PlayerSetupFeature.MaxSeats);
        Current = Math.Clamp(Current, 0, _players - 1);
        EnsureGuests();
    }

    /// <summary>FLY MISSION: moves to the next joined player's check. False means there is none,
    /// which is the caller's cue to launch.</summary>
    public bool Advance()
    {
        _locked = true;
        if (Current + 1 >= _players)
        {
            return false;
        }

        Current++;
        EnsureGuests();
        return true;
    }

    /// <summary>Back: returns to the previous player's check. False at the seated player's own,
    /// which lets the flow leave the screen the way it always did.</summary>
    public bool Retreat()
    {
        if (Current <= 0)
        {
            return false;
        }

        Current--;
        return true;
    }

    /// <summary>Abandons the sequence and puts the seated player back on their own check: RETURN
    /// TO BRIEFING, which is a decision to start the whole walk again.</summary>
    public void Rewind()
    {
        Current = 0;
        _locked = false;
    }

    /// <summary>The aircraft player <paramref name="player"/> flies: the profile's selected plane
    /// for the seated player, the allocation's answer for anybody else, null when neither exists.</summary>
    public OwnedPlane? Plane(int player)
    {
        if (player <= 0)
        {
            return SeatedPlane();
        }

        EnsureGuests();
        return player - 1 < _guests.Count ? _guests[player - 1] : null;
    }

    /// <summary>Whether this record is a stock airframe at rest rather than a profile aircraft.
    /// ⚠ A screen must ask here before reading <c>CustomPlaneStore</c> under the record's name: a
    /// stock record is named for its airframe, and a hangar plane sharing that name would
    /// otherwise fit a guest with somebody else's build.</summary>
    public bool IsStock(OwnedPlane plane) => _stock.Contains(plane);

    // ⚠ A copy, never the profile's own record. A guest's record belongs to the sortie, and a
    // reference here would let anything written to it land in the seated profile.
    private static OwnedPlane CopyOf(OwnedPlane plane) => new()
    {
        Name = plane.Name,
        Airframe = plane.Airframe,
        Ammo = (int[])plane.Ammo.Clone(),
        Ordnance = (int[])plane.Ordnance.Clone(),
        Special = plane.Special,
    };

    private OwnedPlane? SeatedPlane()
    {
        if (_feature.Profile is not { } profile || profile.Planes.Count == 0)
        {
            return null;
        }

        return profile.Planes[Math.Clamp(profile.SelectedPlane, 0, profile.Planes.Count - 1)];
    }

    // Rebuilds every guest's record whenever what the allocation reads has moved. A hangar visit
    // replaces the profile, and a crew change on the seated player's check frees or takes a plane.
    private void EnsureGuests()
    {
        var profile = _feature.Profile;
        var key = (profile, profile?.SelectedPlane ?? 0, profile?.WingmanPlane ?? 0, profile?.Planes.Count ?? 0, _players);
        if (key == _builtFor && _guests.Count == _players - 1)
        {
            return;
        }

        _builtFor = key;
        _guests.Clear();
        _stock.Clear();
        int[] allocated = CoopPlanePool.Allocate(profile, _players);
        for (int player = 1; player < _players; player++)
        {
            _guests.Add(allocated[player] >= 0 ? CopyOf(profile!.Planes[allocated[player]]) : StockDevastator());
        }
    }

    private OwnedPlane StockDevastator()
    {
        int airframe = CoopPlanePool.StockAirframe;
        var record = new OwnedPlane
        {
            Name = _feature.Strings.Text(3000 + airframe, $"Airframe {airframe}"),
            Airframe = airframe,
        };
        _stock.Add(record);
        return record;
    }
}
