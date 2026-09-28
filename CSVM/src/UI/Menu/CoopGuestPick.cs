using System;
using CSVM.Net;

namespace CSVM.UI.Menu;

/// <summary>
/// A co-op guest's own pick from its host's hangar: the airframe and the fit it flies with. It also
/// says whether the guest means to be Ready and whether it walked out of the flight. It lasts the
/// joined session across flights, and a new join starts on the starter and the stock fit. The door
/// sends it under the host's round of picks, and again only when it changed.
/// </summary>
public sealed class CoopGuestPick
{
    /// <summary>The airframe a co-op guest flies when its own pick is not one the host offers: the
    /// campaign's first aeroplane, which every hangar holds.</summary>
    public const byte StarterAirframe = 5;

    private CoopPickMessage? _sent;
    private bool _left;

    /// <summary>The airframe this guest picked from its host's hangar.</summary>
    public byte Airframe { get; private set; } = StarterAirframe;

    /// <summary>Whether this guest means to be Ready, sent or not yet sent. A new round of picks
    /// clears it.</summary>
    public bool Ready { get; private set; }

    /// <summary>The ammunition and ordnance the pick carries to the host.</summary>
    public CoopFit Fit { get; private set; }

    /// <summary>Picks <paramref name="airframe"/> from the host's hangar, whether it is Ready, and
    /// the fit it flies with. Reaches the host on the door's next step, under the host's current
    /// round.</summary>
    public void Set(int airframe, bool ready, CoopFit fit = default)
    {
        Airframe = (byte)Math.Clamp(airframe, 0, byte.MaxValue);
        Ready = ready;
        Fit = fit;
    }

    // Ready counts only once the host has heard it under the round it names now.
    internal bool ReadyUnder(byte round) => Ready && _sent is { } sent && sent.Epoch == round;

    // The mark belongs to the flight's round and clears when the host names another.
    internal void Leave() => _left = true;

    // A guest's Ready belongs to one round: a new round clears it, and the pick goes out again
    // under the new one. Null when the host already has this pick.
    internal CoopPickMessage? Follow(byte round, string name)
    {
        if (_sent is { } last && last.Epoch != round)
        {
            Ready = false;
            _left = false;
        }

        var pick = new CoopPickMessage(round, Ready, Airframe, Fit, name, _left);
        return _sent == pick ? null : pick;
    }

    internal void MarkSent(CoopPickMessage pick) => _sent = pick;

    internal void Forget()
    {
        _sent = null;
        Ready = false;
        Airframe = StarterAirframe;
        Fit = default;
        _left = false;
    }
}
