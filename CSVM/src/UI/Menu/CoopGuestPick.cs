using System;
using CSVM.Net;
using CSVM.Session.Campaign;

namespace CSVM.UI.Menu;

/// <summary>
/// One seat's pick at a co-op guest: the plane of its host's hangar, the airframe and the fit it
/// flies with. It also says whether the seat means to be Ready and whether it walked out of the flight.
/// It lasts the joined session across flights, and a new join starts with no plane picked and the
/// stock fit. The door sends it under the host's round of picks, and again only when it changed.
/// </summary>
public sealed class CoopGuestPick
{
    /// <summary>The airframe a co-op guest flies before its pick names one: the campaign's first
    /// aeroplane, which every hangar holds.</summary>
    public const byte StarterAirframe = CoopPlanePool.StockAirframe;

    private CoopPickMessage? _sent;
    private bool _left;

    /// <summary>The airframe this guest flies.</summary>
    public byte Airframe { get; private set; } = StarterAirframe;

    /// <summary>The plane of the host's hangar this guest picked: an index into the hangar,
    /// <see cref="CoopPlanePool.Stock"/> or <see cref="CoopPlanePool.Unpicked"/>.</summary>
    public int Plane { get; private set; } = CoopPlanePool.Unpicked;

    /// <summary>Whether this guest means to be Ready, sent or not yet sent. A new round of picks
    /// clears it.</summary>
    public bool Ready { get; private set; }

    /// <summary>The ammunition and ordnance the pick carries to the host.</summary>
    public CoopFit Fit { get; private set; }

    /// <summary>The airframe this guest flies, whether it is Ready, and the fit it flies with.
    /// Reaches the host on the door's next step, under the host's current round.</summary>
    public void Set(int airframe, bool ready, CoopFit fit = default)
    {
        Airframe = (byte)Math.Clamp(airframe, 0, byte.MaxValue);
        Ready = ready;
        Fit = fit;
    }

    /// <summary>Picks <paramref name="plane"/> of the host's hangar, as <see cref="Plane"/> names
    /// one. Reaches the host on the door's next step.</summary>
    public void Choose(int plane) => Plane = plane >= CoopPlanePool.Stock ? plane : CoopPlanePool.Unpicked;

    // Ready counts only once the host has heard it under the round it names now.
    internal bool ReadyUnder(byte round) => Ready && _sent is { } sent && sent.Epoch == round;

    // The mark belongs to the flight's round and clears when the host names another.
    internal void Leave() => _left = true;

    // A guest's Ready belongs to one round: a new round clears it, and the pick goes out again
    // under the new one. Null when the host already has this pick.
    internal CoopPickMessage? Follow(byte round, string name, byte voice = CoopPickMessage.NoVoice,
        int local = 0, bool more = false)
    {
        if (_sent is { } last && last.Epoch != round)
        {
            Ready = false;
            _left = false;
        }

        var pick = new CoopPickMessage(round, Ready, Airframe, Fit, name, _left, CoopPickMessage.PlaneByte(Plane), voice,
            (byte)Math.Clamp(local, 0, CoopPickMessage.MaxLocal), more);
        return _sent == pick ? null : pick;
    }

    internal void MarkSent(CoopPickMessage pick) => _sent = pick;

    internal void Forget()
    {
        _sent = null;
        Ready = false;
        Airframe = StarterAirframe;
        Plane = CoopPlanePool.Unpicked;
        Fit = default;
        _left = false;
    }
}
