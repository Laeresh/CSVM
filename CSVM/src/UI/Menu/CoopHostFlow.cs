using System;
using System.Collections.Generic;
using CSVM.Net;

namespace CSVM.UI.Menu;

/// <summary>
/// What a co-op host names to its guests about its boards: the board, the mission, the progress,
/// the hangar it offers and the debrief's result. It also holds the film it shares. It
/// builds each guest's flow and sends one again only when it changed. The round of picks is the
/// door's, so the door advances it on <see cref="Show"/>'s answer and hands it to every send.
/// </summary>
public sealed class CoopHostFlow
{
    private readonly Dictionary<int, CoopFlowMessage> _sent = new();
    private byte _seq;
    private byte _progress;
    private ushort _airframes;
    private bool _won;
    private int _objectives;
    private int _cash;

    // The last film's ordinal, which the next one counts on from.
    private byte _filmOrdinal;

    /// <summary>The board this co-op host last named to its guests.</summary>
    public NetCoopScreen Screen { get; private set; } = NetCoopScreen.Cabin;

    /// <summary>The film this co-op host shares with its guests right now, or null while none plays.
    /// </summary>
    public CoopFilmMessage? FilmShown { get; private set; }

    internal byte MissionSeq => _seq;

    internal byte Progress => _progress;

    internal ushort Airframes => _airframes;

    /// <summary>The shared result a co-op host's debrief shows, named to every guest with the
    /// board. It says whether the mission was won, its objectives met as a mask, and its cash.
    /// </summary>
    public void ShowResult(bool won, int objectives, int cash)
    {
        _won = won;
        _objectives = objectives;
        _cash = cash;
    }

    // True when the change starts a new round of picks. A new mission does, as does a move onto a
    // board other than the briefing and flight check.
    internal bool Show(NetCoopScreen screen, int missionSeq, int progress, ushort airframes)
    {
        byte seq = (byte)Math.Clamp(missionSeq, 0, byte.MaxValue);
        bool picking = screen is NetCoopScreen.Briefing or NetCoopScreen.FlightCheck;
        bool wasPicking = Screen is NetCoopScreen.Briefing or NetCoopScreen.FlightCheck;
        bool newRound = seq != _seq || (screen != Screen && !(picking && wasPicking));
        Screen = screen;
        _seq = seq;
        _progress = (byte)Math.Clamp(progress, 0, byte.MaxValue);
        _airframes = airframes;
        return newRound;
    }

    // A pick of an airframe the host's hangar does not hold, or no pick at all, flies the starter.
    internal byte AirframeOf(byte? picked)
    {
        if (picked is { } airframe && Offers(airframe))
        {
            return airframe;
        }

        if (Offers(CoopGuestPick.StarterAirframe) || _airframes == 0)
        {
            return CoopGuestPick.StarterAirframe;
        }

        for (byte a = 0; a < 16; a++)
        {
            if (Offers(a))
            {
                return a;
            }
        }

        return CoopGuestPick.StarterAirframe;
    }

    // Each guest's flow differs only in its own player number; one goes out whenever it changed.
    internal void Send(NetLobby wire, IReadOnlyList<int> admitted, int localPlayers, byte round, Func<int, bool> readyNow)
    {
        byte mask = 0;
        for (int i = 0; i < admitted.Count; i++)
        {
            int slot = localPlayers + i;
            if (slot < 8 && readyNow(admitted[i]))
            {
                mask |= (byte)(1 << slot);
            }
        }

        byte humans = (byte)Math.Min(localPlayers + admitted.Count, byte.MaxValue);
        for (int i = 0; i < admitted.Count; i++)
        {
            int peer = admitted[i];
            var flow = new CoopFlowMessage(Screen, _seq, round, (byte)(localPlayers + i), mask, humans,
                _progress, _won, _airframes, _objectives, _cash, (byte)Math.Min(localPlayers, byte.MaxValue));
            if (_sent.TryGetValue(peer, out var sent) && sent == flow)
            {
                continue;
            }

            wire.Tell(peer, flow);
            _sent[peer] = flow;
        }

        foreach (int peer in new List<int>(_sent.Keys))
        {
            if (!Contains(admitted, peer))
            {
                _sent.Remove(peer);
            }
        }
    }

    // A wire handed back from a flight has lost what it was told, so every flow goes out again.
    internal void ClearSent() => _sent.Clear();

    internal CoopFilmMessage StartFilm(NetCoopFilm film, int chapter)
    {
        _filmOrdinal = unchecked((byte)(_filmOrdinal + 1));
        var started = new CoopFilmMessage(_filmOrdinal, true, film, (byte)Math.Clamp(chapter, 0, byte.MaxValue));
        FilmShown = started;
        return started;
    }

    // The end of the film shown, or null while none is.
    internal CoopFilmMessage? EndFilm()
    {
        if (FilmShown is not { } film)
        {
            return null;
        }

        FilmShown = null;
        return film with { Playing = false };
    }

    // The film ordinal is kept, so the next film still counts on from the last.
    internal void Forget()
    {
        _sent.Clear();
        Screen = NetCoopScreen.Cabin;
        _seq = 0;
        _progress = 0;
        _airframes = 0;
        _won = false;
        _objectives = 0;
        _cash = 0;
        FilmShown = null;
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

    private bool Offers(int airframe) => airframe is >= 0 and < 16 && (_airframes & (1 << airframe)) != 0;
}
