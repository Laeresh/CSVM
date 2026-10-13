using System;
using System.Collections.Generic;
using CSVM.Net;

namespace CSVM.UI.Menu;

/// <summary>
/// Who a host seats. It refuses a guest of another version, and one the lobby turned away for a
/// wrong password, a boot or the player cap. Each is told why and hung up on after a grace.
/// In co-op it seats guests in arrival order and grants each further seats from the room the first
/// seats leave. The door hands it the wire, its own seat count and the cap on every call.
/// </summary>
public sealed class NetAdmission
{
    /// <summary>How long a refused guest has to hang up on its own after the full notice, before
    /// the host hangs up on it.</summary>
    public const double RefuseGraceSeconds = 1.0;

    private readonly List<int> _admitted = new();
    private readonly Dictionary<int, int> _granted = new();
    private readonly List<(int Peer, double Waited)> _refused = new();

    // The guests a campaign host seated, in arrival order.
    internal IReadOnlyList<int> Admitted => _admitted;

    internal bool Refused(int peer)
    {
        foreach (var (refused, _) in _refused)
        {
            if (refused == peer)
            {
                return true;
            }
        }

        return false;
    }

    // How many seats this host gave a seated guest, 1 until its picks ask for more.
    internal int GrantedTo(int peer) => _granted.TryGetValue(peer, out int seats) ? seats : 1;

    // Removes a seated guest as the original's Boot does, its address banned by the lobby. False
    // for a peer that is not a seated guest.
    internal bool Boot(NetLobby lobby, int peer)
    {
        if (Refused(peer) || !Contains(lobby.AllPeers, peer))
        {
            return false;
        }

        lobby.Boot(peer);
        _admitted.Remove(peer);
        _granted.Remove(peer);
        RefuseTurnedAway(lobby);
        return true;
    }

    // A Dogfight host seats guests up to its chosen cap in arrival order and turns one past it away,
    // as a campaign host does. ⚠ Do not leave a refused guest on the peer lists until its hang-up.
    // A launch's field reads them, and ENet's hang-up lands a step later.
    internal void RefuseOverCap(NetLobby lobby, int localPlayers, int cap)
    {
        int seated = localPlayers;
        foreach (int peer in lobby.AllPeers)
        {
            if (Refused(peer))
            {
                continue;
            }

            if (seated < cap)
            {
                seated++;
                continue;
            }

            lobby.TurnAway(peer, NetCloseReason.Full);
        }

        RefuseTurnedAway(lobby);
    }

    // A campaign host seats guests in arrival order while the cap has a seat free, every seat a
    // guest was given counting. One past it is turned away as full, off every peer list at once, so
    // a bind inside its grace never seats it.
    internal void Admit(NetLobby lobby, int localPlayers, int cap)
    {
        var peers = lobby.AllPeers;
        _admitted.RemoveAll(peer => !Contains(peers, peer));
        int seats = Math.Max(0, cap - localPlayers);
        Grant(lobby, seats);
        int used = 0;
        foreach (int peer in _admitted)
        {
            used += GrantedTo(peer);
        }

        for (int i = 0; i < peers.Count; i++)
        {
            int peer = peers[i];
            if (_admitted.Contains(peer) || Refused(peer))
            {
                continue;
            }

            if (used < seats)
            {
                _admitted.Add(peer);
                _granted[peer] = 1;
                used++;
                continue;
            }

            lobby.TurnAway(peer, NetCloseReason.Full);
        }

        RefuseTurnedAway(lobby);
        Grant(lobby, seats);
    }

    // Either kind of door tells a guest of another version so. It is hung up on after the grace a
    // guest refused as full gets. The lobby has already left it off every peer list.
    internal void RefuseClashing(NetLobby lobby)
    {
        foreach (int peer in lobby.Clashing)
        {
            if (!Refused(peer))
            {
                lobby.Farewell(peer, NetCloseReason.VersionMismatch);
                _refused.Add((peer, 0.0));
            }
        }
    }

    // A peer the lobby turned away (booted, a wrong password, past the cap) is told why and hung up
    // on after the same grace.
    internal void RefuseTurnedAway(NetLobby lobby)
    {
        foreach (var (peer, why) in lobby.TurnedAway)
        {
            if (!Refused(peer))
            {
                lobby.Farewell(peer, why);
                _refused.Add((peer, 0.0));
            }
        }
    }

    // The clashing peers are off AllPeers, so what is still connected is asked of the carrier.
    internal void HangUpRefused(NetLobby lobby, double dt)
    {
        var connected = lobby.Inner.Peers;
        _refused.RemoveAll(refused => !Contains(connected, refused.Peer));
        for (int i = 0; i < _refused.Count; i++)
        {
            var (peer, waited) = _refused[i];
            if (double.IsPositiveInfinity(waited))
            {
                continue;
            }

            waited += dt;
            if (waited >= RefuseGraceSeconds)
            {
                // Kept on the list until the carrier reports it gone, so it is not seated meanwhile.
                waited = double.PositiveInfinity;
                lobby.Disconnect(peer);
            }

            _refused[i] = (peer, waited);
        }
    }

    // A guest refused as full has no seat in the match the session builds off the roster.
    internal void DisconnectRefused(NetLobby lobby)
    {
        foreach (var (peer, _) in _refused)
        {
            lobby.Disconnect(peer);
        }
    }

    internal void Clear()
    {
        _admitted.Clear();
        _granted.Clear();
        _refused.Clear();
    }

    private static bool Contains(IReadOnlyList<int> peers, int peer)
    {
        for (int i = 0; i < peers.Count; i++)
        {
            if (peers[i] == peer)
            {
                return true;
            }
        }

        return false;
    }

    // Each seated guest's seats: its first, then as many more as its picks ask, in arrival order out
    // of what the first seats leave. ⚠ Never take a guest's first seat for another's further one; a
    // machine's later pad must not unseat a player already flying. A guest cut short is told so.
    private void Grant(NetLobby lobby, int seats)
    {
        int free = seats - _admitted.Count;
        foreach (int peer in _admitted)
        {
            int extra = Math.Clamp(lobby.SeatsWanted(peer) - 1, 0, Math.Max(0, free));
            _granted[peer] = 1 + extra;
            free -= extra;
        }

        foreach (int peer in new List<int>(_granted.Keys))
        {
            if (!_admitted.Contains(peer))
            {
                _granted.Remove(peer);
            }
        }
    }
}
