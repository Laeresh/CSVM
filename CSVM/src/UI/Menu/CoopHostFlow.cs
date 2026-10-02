using System;
using System.Collections.Generic;
using CSVM.Net;
using CSVM.Session.Campaign;

namespace CSVM.UI.Menu;

/// <summary>
/// What a co-op host names to its guests about its boards: the board, the mission, the progress,
/// the hangar it offers and the debrief's result. Each hangar plane names its holding seat. It also
/// holds the film it shares. It builds each guest's flow and hangar words and sends one again only
/// when it changed. The round of picks is the door's, so the door advances it on <see cref="Show"/>'s
/// answer and hands it to every send.
/// </summary>
public sealed class CoopHostFlow
{
    private readonly Dictionary<int, CoopFlowMessage> _sent = new();
    private readonly Dictionary<int, CoopHangarMessage[]> _hangarSent = new();
    private CoopHangarMessage[] _hangar = Array.Empty<CoopHangarMessage>();
    private int[] _seatPlanes = Array.Empty<int>();
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

    /// <summary>The plane seat <paramref name="seat"/> flies as the host last settled every pick
    /// (<see cref="CoopPlanePool.Resolve"/>). An index into the hangar, or
    /// <see cref="CoopPlanePool.Stock"/> for a seat it names nothing for.</summary>
    public int PlaneOf(int seat) =>
        seat >= 0 && seat < _seatPlanes.Length && _seatPlanes[seat] < _hangar.Length ? _seatPlanes[seat] : CoopPlanePool.Stock;

    /// <summary>Hangar plane <paramref name="index"/> as the guests hear it, or null past the hangar.
    /// </summary>
    public CoopHangarMessage? HangarAt(int index) => index >= 0 && index < _hangar.Length ? _hangar[index] : null;

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

    // Takes the hangar in order with each plane's holder, and the plane each seat flies. Each word
    // is stamped with its own place and the hangar's size.
    internal void ShowHangar(IReadOnlyList<CoopHangarMessage> hangar, IReadOnlyList<int> seatPlanes)
    {
        int count = Math.Min(hangar.Count, byte.MaxValue);
        if (_hangar.Length != count)
        {
            _hangar = new CoopHangarMessage[count];
        }

        for (int at = 0; at < count; at++)
        {
            _hangar[at] = hangar[at] with { Index = (byte)at, Count = (byte)count };
        }

        _seatPlanes = new int[seatPlanes.Count];
        for (int seat = 0; seat < _seatPlanes.Length; seat++)
        {
            _seatPlanes[seat] = seatPlanes[seat];
        }
    }

    // Each guest's flow differs only in its own player number and its seat count; one goes out
    // whenever it changed. A guest's seats follow one another from its number. The hangar words go
    // first, so a guest opening its campaign on the flow already holds them.
    internal void Send(NetLobby wire, IReadOnlyList<(int Peer, int Seats)> seated, int localPlayers, byte round,
        Func<int, int, bool> readyNow)
    {
        foreach (var (peer, _) in seated)
        {
            SendHangar(wire, peer);
        }

        byte mask = 0;
        int slot = localPlayers;
        foreach (var (peer, seats) in seated)
        {
            for (int local = 0; local < seats; local++, slot++)
            {
                if (slot < 8 && readyNow(peer, local))
                {
                    mask |= (byte)(1 << slot);
                }
            }
        }

        byte humans = (byte)Math.Min(slot, byte.MaxValue);
        slot = localPlayers;
        foreach (var (peer, seats) in seated)
        {
            var flow = new CoopFlowMessage(Screen, _seq, round, (byte)Math.Min(slot, byte.MaxValue), mask, humans,
                _progress, _won, _airframes, _objectives, _cash, (byte)Math.Min(localPlayers, byte.MaxValue),
                (byte)Math.Clamp(seats - 1, 0, byte.MaxValue));
            slot += seats;
            if (_sent.TryGetValue(peer, out var sent) && sent == flow)
            {
                continue;
            }

            wire.Tell(peer, flow);
            _sent[peer] = flow;
        }

        foreach (int peer in new List<int>(_sent.Keys))
        {
            if (!Seated(seated, peer))
            {
                _sent.Remove(peer);
                _hangarSent.Remove(peer);
            }
        }
    }

    // A wire handed back from a flight has lost what it was told, so every flow goes out again.
    internal void ClearSent()
    {
        _sent.Clear();
        _hangarSent.Clear();
    }

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
        _hangarSent.Clear();
        _hangar = Array.Empty<CoopHangarMessage>();
        _seatPlanes = Array.Empty<int>();
        Screen = NetCoopScreen.Cabin;
        _seq = 0;
        _progress = 0;
        _airframes = 0;
        _won = false;
        _objectives = 0;
        _cash = 0;
        FilmShown = null;
    }

    private static bool Seated(IReadOnlyList<(int Peer, int Seats)> seated, int peer)
    {
        foreach (var (each, _) in seated)
        {
            if (each == peer)
            {
                return true;
            }
        }

        return false;
    }

    // Every word this peer has not heard as it stands now.
    private void SendHangar(NetLobby wire, int peer)
    {
        if (!_hangarSent.TryGetValue(peer, out var told) || told.Length != _hangar.Length)
        {
            told = new CoopHangarMessage[_hangar.Length];
            for (int at = 0; at < told.Length; at++)
            {
                told[at] = _hangar[at] with { Count = 0 };
            }

            _hangarSent[peer] = told;
        }

        for (int at = 0; at < _hangar.Length; at++)
        {
            if (told[at] != _hangar[at])
            {
                wire.Tell(peer, _hangar[at]);
                told[at] = _hangar[at];
            }
        }
    }
}
