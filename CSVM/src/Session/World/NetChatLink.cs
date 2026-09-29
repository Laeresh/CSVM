using System;
using System.Collections.Generic;
using CSVM.Flight.Hud;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Utils;
using Godot;

namespace CSVM.Session.World;

/// <summary>
/// The in-flight chat over the wire, one per network session. A line typed here is posted to this
/// machine's <see cref="FlightChat"/> as the original's echo and sent as a
/// <see cref="FlightChatMessage"/>. A guest sends it to the host, which relays it on. An all-chat
/// reaches every machine, and a team line only the machines flying the typist's lobby team. Each
/// machine takes one copy however many seats it flies. The keys are this class's too, since the
/// typing pane routes no input of its own (<c>docs/org/multiplayer-messages.md</c>).
/// </summary>
internal sealed class NetChatLink
{
    /// <summary>The prompt an all-chat's entry opens under, "To All:".</summary>
    internal const string AllPromptKey = "MSG_GLOBAL_MESSAGE";

    /// <summary>The prompt a team line's entry opens under, "To Team:".</summary>
    internal const string TeamPromptKey = "MSG_SQUADRON_MESSAGE";

    private readonly NetSession _net;
    private readonly Messages? _strings;
    private readonly HashSet<Key> _held = new();
    private int _typist = NetMessage.NoSeat;

    private NetChatLink(NetSession net, Messages? strings)
    {
        _net = net;
        _strings = strings;
    }

    /// <summary>This machine's chat, which every local pane draws.</summary>
    internal FlightChat Chat { get; } = new();

    /// <summary>Lines this end put on the wire, its own and none it relayed.</summary>
    internal int LinesSent { get; private set; }

    /// <summary>Whether the keyboard belongs to the chat: a line is open, or a key pressed into one
    /// is still down. The typist's seat reads its keys idle for exactly as long.</summary>
    internal bool HoldsKeyboard => Chat.Typing || _held.Count > 0;

    /// <summary>Wires the chat to <paramref name="net"/>. <paramref name="strings"/> words the two
    /// prompts and the unknown sender, and each falls back to its key without it.</summary>
    internal static NetChatLink Open(NetSession net, Messages? strings)
    {
        ArgumentNullException.ThrowIfNull(net);
        var link = new NetChatLink(net, strings);
        net.On<FlightChatMessage>(link.Take);
        if (net.IsHost)
        {
            // The original's own addressing: a team line reaches each machine flying the typist's
            // team (0x00499af5), an all-chat every machine. The seat must be the sender's own.
            net.RelayToPeers<FlightChatMessage>((line, from, to) =>
                net.PeerOfSeat(line.Seat) == from && (!line.Team || net.FliesOnTeam(to, link.TeamOf(line.Seat))));
        }

        return link;
    }

    /// <summary>Opens the entry for <paramref name="seat"/>, to its team when
    /// <paramref name="team"/>. A seat on no lobby team opens an all-chat either way, as the
    /// original's team key does in a match without teams (<c>0x004a834d</c>).</summary>
    internal void Open(int seat, bool team)
    {
        bool toTeam = team && TeamOf(seat) != 0;
        _typist = seat;
        Chat.Open(toTeam, Text(toTeam ? TeamPromptKey : AllPromptKey));
    }

    /// <summary>Closes the entry and sends what was typed, if anything. True when a line left.
    /// </summary>
    internal bool Submit()
    {
        bool team = Chat.ToTeam;
        string prompt = Chat.Prompt;
        string? line = Chat.Submit();
        if (line == null || _typist == NetMessage.NoSeat)
        {
            return false;
        }

        Say(_typist, team, prompt, line);
        return true;
    }

    /// <summary>One key event while this machine's keyboard seat flies. True when the chat took
    /// it, which is every key while a line is open and the release of each key pressed into it.
    /// Enter sends, Escape drops the line, Backspace takes a character back.</summary>
    internal bool TakeKey(Key key, bool pressed, char typed)
    {
        if (!Chat.Typing)
        {
            return !pressed && _held.Remove(key);
        }

        if (!pressed)
        {
            _held.Remove(key);
            return true;
        }

        _held.Add(key);
        switch (key)
        {
            case Key.Enter or Key.KpEnter:
                Submit();
                break;
            case Key.Escape:
                Chat.Cancel();
                break;
            case Key.Backspace:
                Chat.Erase();
                break;
            default:
                Chat.Type(typed);
                break;
        }

        return true;
    }

    // The lobby team a seat flies for, 0 for none or for a seat this roster does not hold.
    private int TeamOf(int seat)
    {
        foreach (var entry in _net.Seats)
        {
            if (entry.SeatIndex == seat)
            {
                return entry.TeamId;
            }
        }

        return 0;
    }

    // A line from this end. A host sends a team line to each machine flying a teammate. A guest
    // sends any line to the host, which forwards it the same way.
    private void Say(int seat, bool team, string prompt, string text)
    {
        var message = new FlightChatMessage((byte)seat, team, text);
        Chat.Post(FlightChat.Echo(prompt, text));
        LinesSent++;
        if (!_net.IsHost)
        {
            _net.Send(_net.HostPeer, message, NetChannels.Events);
        }
        else if (!team)
        {
            _net.Broadcast(message, NetChannels.Events);
        }
        else
        {
            int lobbyTeam = TeamOf(seat);
            foreach (int peer in _net.Peers)
            {
                if (_net.FliesOnTeam(peer, lobbyTeam))
                {
                    _net.Send(peer, message, NetChannels.Events);
                }
            }
        }

        Log.Info("core", $"net chat: seat {seat} to {(team ? $"team {TeamOf(seat)}" : "everyone")}: {text}");
    }

    // An arrival. A host shows a team line only when it flies the typist's team itself. A guest is
    // sent only what it should show, and checks the team anyway.
    private void Take(int peer, FlightChatMessage line)
    {
        if (_net.IsHost && _net.PeerOfSeat(line.Seat) != peer)
        {
            Log.Warn("core", $"net chat: a line for seat {line.Seat} from a peer that does not fly it, ignored");
            return;
        }

        if (line.Team && !_net.FliesOnTeam(_net.LocalPeer, TeamOf(line.Seat)))
        {
            return;
        }

        Chat.Post(FlightChat.Received(NameOf(line.Seat), line.Text));
    }

    // The sender's callsign, or the original's "Unknown" (6007) for a seat it cannot find.
    private string NameOf(int seat)
    {
        foreach (var entry in _net.Seats)
        {
            if (entry.SeatIndex == seat && entry.Callsign.Length > 0)
            {
                return entry.Callsign;
            }
        }

        return Text(HudMessages.UnknownKey);
    }

    private string Text(string key) => _strings?.Get(key) ?? key;
}
