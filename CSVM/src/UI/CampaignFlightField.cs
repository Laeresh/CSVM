using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Session;

namespace CSVM.UI;

/// <summary>
/// One guest's aircraft on a co-op campaign sortie: the records they may cycle through, and which
/// one they are on. Every record here is session-scoped — a stock airframe at rest, or a COPY of
/// one of the seated profile's aircraft — so a guest's ammunition edits land on something the
/// profile store never sees.
/// </summary>
public sealed class CampaignGuest
{
    private readonly List<OwnedPlane> _choices;

    internal CampaignGuest(int player, List<OwnedPlane> choices, int stockCount)
    {
        Player = player;
        _choices = choices;
        StockCount = stockCount;
    }

    /// <summary>Which human this is: 1 for P2, up to 3 for P4. The seated player is never one.</summary>
    public int Player { get; }

    /// <summary>Every aircraft this guest may fly, the stock airframes first.</summary>
    public IReadOnlyList<OwnedPlane> Choices => _choices;

    /// <summary>How many leading entries of <see cref="Choices"/> are stock airframes rather than
    /// copies of the seated profile's aircraft.</summary>
    public int StockCount { get; }

    /// <summary>Which entry of <see cref="Choices"/> is picked.</summary>
    public int Choice { get; internal set; }

    /// <summary>The aircraft this guest flies.</summary>
    public OwnedPlane Plane => _choices[Math.Clamp(Choice, 0, _choices.Count - 1)];
}

/// <summary>
/// The humans flying one campaign sortie, as the flight check walks them: how many joined, whose
/// check is showing, and what each guest picked. The seated player is 0 and keeps the profile's own
/// aircraft; players 1 and up are guests, who bring no profile and fly the session-scoped records
/// <see cref="CampaignGuest"/> holds. Engine-free, so the no-duplicate and copy-not-reference rules
/// test off engine. The sequence re-enters <see cref="CampaignScreen.FlightCheck"/> with a player
/// index because <see cref="CampaignFlow.GoTo"/> returns to the existing page instance.
/// </summary>
public sealed class CampaignFlightField
{
    // The campaign's own starter airframe (CampaignProfileDef.NewProfile's two Devastators), which
    // is where a guest's pick opens rather than at the roster's arbitrary first row.
    private const int StarterAirframe = 5;

    // The stock roster a guest picks from: the 11-airframe stat table's own ids 0-10, the same
    // order stock_loadouts.json, PlanePickerRoster.AirframeNodes and the langui 3000 titles use.
    private static readonly int StockAirframes = HangarEconomy.Airframes.Length;

    private readonly CampaignFlow _flow;
    private readonly List<CampaignGuest> _guests = new();

    // Every stock record handed out, by reference: a screen resolving one plane's guns asks here
    // rather than reading CustomPlaneStore under a name an airframe title could collide with.
    private readonly HashSet<OwnedPlane> _stock = new();

    // The profile the guest rosters were built from. A different one (Resume re-reads it after a
    // hangar visit) rebuilds them, since a roster is a statement about that profile's aircraft.
    private CampaignProfileDef? _rosterProfile;

    private int _players = 1;

    internal CampaignFlightField(CampaignFlow flow) => _flow = flow;

    /// <summary>How many humans are flying, 1 to <see cref="SplitScreen.MaxPlayers"/>.</summary>
    public int Players => _players;

    /// <summary>Whose flight check is showing: 0 the seated player, 1 and up a guest.</summary>
    public int Current { get; private set; }

    /// <summary>Whether the field is closed to new players while a guest's flight check is shown.</summary>
    public bool Locked => Current > 0;

    /// <summary>The guests, in player order. Empty for a solo campaign.</summary>
    public IReadOnlyList<CampaignGuest> Guests
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
        _players = Math.Clamp(players, 1, SplitScreen.MaxPlayers);
        Current = Math.Clamp(Current, 0, _players - 1);
        EnsureGuests();
    }

    /// <summary>FLY MISSION: moves to the next joined player's check. False means there is none,
    /// which is the caller's cue to launch.</summary>
    public bool Advance()
    {
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
    public void Rewind() => Current = 0;

    /// <summary>The aircraft player <paramref name="player"/> flies: the profile's selected plane
    /// for the seated player, that guest's own pick for anybody else, null when neither exists.</summary>
    public OwnedPlane? Plane(int player)
    {
        if (player <= 0)
        {
            return SeatedPlane();
        }

        EnsureGuests();
        int at = player - 1;
        return at >= 0 && at < _guests.Count ? _guests[at].Plane : null;
    }

    /// <summary>Cycles a guest's aircraft, skipping whatever another player already took. Returns
    /// whether the pick moved.</summary>
    public bool Step(int player, int dir)
    {
        EnsureGuests();
        int at = player - 1;
        if (dir == 0 || at < 0 || at >= _guests.Count)
        {
            return false;
        }

        var guest = _guests[at];
        int count = guest.Choices.Count;
        int pick = guest.Choice;
        for (int tries = 0; tries < count; tries++)
        {
            pick = (((pick + dir) % count) + count) % count;
            if (pick == guest.Choice)
            {
                return false;
            }

            if (!Taken(guest, pick))
            {
                guest.Choice = pick;
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether this record is a stock airframe at rest rather than a profile aircraft.
    /// ⚠ A screen must ask here before reading <c>CustomPlaneStore</c> under the record's name: a
    /// stock record is named for its airframe, and a hangar plane sharing that name would
    /// otherwise fit a guest with somebody else's build.</summary>
    public bool IsStock(OwnedPlane plane) => _stock.Contains(plane);

    // What "another player already flies this" compares: the airframe for a stock entry, the
    // plane's own name for a profile aircraft. So two guests cannot both take the stock Devastator,
    // and neither can take the aircraft the seated player is flying.
    private static string KeyOf(OwnedPlane plane, bool stock) =>
        stock ? $"stock:{plane.Airframe}" : $"owned:{plane.Name}";

    // ⚠ A copy, never the profile's own record: a guest edits ammunition on whatever this returns,
    // and a reference here would write those edits into the seated profile.
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
        if (_flow.Profile is not { } profile || profile.Planes.Count == 0)
        {
            return null;
        }

        return profile.Planes[Math.Clamp(profile.SelectedPlane, 0, profile.Planes.Count - 1)];
    }

    private bool Taken(CampaignGuest asking, int pick)
    {
        string key = KeyOf(asking.Choices[pick], pick < asking.StockCount);
        if (SeatedPlane() is { } seated && key == KeyOf(seated, stock: false))
        {
            return true;
        }

        foreach (var other in _guests)
        {
            if (!ReferenceEquals(other, asking) && key == KeyOf(other.Plane, other.Choice < other.StockCount))
            {
                return true;
            }
        }

        return false;
    }

    // Builds whatever guests the joined count now needs, drops the trailing ones it does not, and
    // starts over whenever the seated profile changed under them.
    private void EnsureGuests()
    {
        if (!ReferenceEquals(_rosterProfile, _flow.Profile))
        {
            _rosterProfile = _flow.Profile;
            _guests.Clear();
            _stock.Clear();
        }

        while (_guests.Count > _players - 1)
        {
            _guests.RemoveAt(_guests.Count - 1);
        }

        while (_guests.Count < _players - 1)
        {
            _guests.Add(NewGuest(_guests.Count + 1));
        }
    }

    // One guest's own copy of the roster: the stock airframes, then a copy of each of the seated
    // profile's aircraft. Per guest rather than shared, so one guest's ammunition edits cannot
    // reach a record another guest later picks up.
    private CampaignGuest NewGuest(int player)
    {
        var choices = new List<OwnedPlane>(StockAirframes + (_rosterProfile?.Planes.Count ?? 0));
        for (int airframe = 0; airframe < StockAirframes; airframe++)
        {
            var record = new OwnedPlane { Name = AirframeTitle(airframe), Airframe = airframe };
            _stock.Add(record);
            choices.Add(record);
        }

        if (_rosterProfile is { } profile)
        {
            foreach (var owned in profile.Planes)
            {
                choices.Add(CopyOf(owned));
            }
        }

        // Built before the guest joins _guests, so Taken sees only the players who went before.
        var guest = new CampaignGuest(player, choices, StockAirframes);
        guest.Choice = FirstFree(guest);
        return guest;
    }

    // The starter airframe where nobody took it, else the first entry that is free: a guest opens
    // on something they can actually fly.
    private int FirstFree(CampaignGuest guest)
    {
        for (int tries = 0; tries < guest.Choices.Count; tries++)
        {
            int pick = (StarterAirframe + tries) % guest.Choices.Count;
            if (!Taken(guest, pick))
            {
                return pick;
            }
        }

        return StarterAirframe;
    }

    private string AirframeTitle(int airframe) =>
        _flow.Strings.Text(3000 + airframe, $"Airframe {airframe}");
}
