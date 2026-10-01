using System.Collections.Generic;
using CSVM.Net;

namespace CSVM.Testing;

/// <summary>
/// The master server's socket side in one process, for the WebRTC suites. A host is given a code.
/// A guest naming it is numbered from 2 and announced to the host. A signal goes to the one end it
/// names, with the sender written as its source. It hands out no ICE servers, so a link stands on
/// host candidates alone. The list and the expiry are the server's and its own tests'.
/// </summary>
internal sealed class LoopbackMaster
{
    private readonly Dictionary<string, Game> _games = new();
    private int _codes;

    /// <summary>How many messages it has carried, both ways.</summary>
    public int Carried { get; private set; }

    /// <summary>Opens one socket, already open.</summary>
    public IMasterSocket Open() => new End(this);

    private void Route(End from, MasterMessage message)
    {
        Carried++;
        switch (message.T)
        {
            case MasterWire.Host:
                string code = MasterWire.Written(new string(MasterWire.CodeAlphabet[_codes++ % MasterWire.CodeAlphabet.Length], MasterWire.CodeLength));
                var game = new Game(from);
                _games[code] = game;
                from.Role = (game, 1);
                Deliver(from, new MasterMessage { T = MasterWire.Hosted, Code = code });
                break;
            case MasterWire.Join when MasterWire.TryCode(message.Code, out string asked) && _games.TryGetValue(asked, out var joined):
                int peer = joined.NextPeer++;
                joined.Guests[peer] = from;
                from.Role = (joined, peer);
                Deliver(from, new MasterMessage { T = MasterWire.Joined, Code = asked, Peer = peer, Ice = new() });
                Deliver(joined.Host, new MasterMessage { T = MasterWire.Incoming, Peer = peer, Addr = "127.0.0.1", Ice = new() });
                break;
            case MasterWire.Join:
                Deliver(from, new MasterMessage { T = MasterWire.Error, Why = "no game is listed under that code" });
                break;
            case MasterWire.Signal when from.Role is { } role:
                End? to = role.Peer == 1
                    ? message.To is int guest && role.Game.Guests.TryGetValue(guest, out var found) ? found : null
                    : message.To == 1 ? role.Game.Host : null;
                if (to != null)
                {
                    Deliver(to, new MasterMessage
                    {
                        T = MasterWire.Signal,
                        From = role.Peer,
                        To = message.To,
                        Kind = message.Kind,
                        Sdp = message.Sdp,
                        Mid = message.Mid,
                        Index = message.Index,
                    });
                }

                break;
        }
    }

    private void Closed(End end)
    {
        if (end.Role is not { } role)
        {
            return;
        }

        if (role.Peer != 1 && role.Game.Guests.Remove(role.Peer))
        {
            Deliver(role.Game.Host, new MasterMessage { T = MasterWire.Left, Peer = role.Peer });
        }
    }

    private void Deliver(End to, MasterMessage message)
    {
        if (to.State == MasterSocketState.Open)
        {
            Carried++;
            to.Inbox.Enqueue(message);
        }
    }

    private sealed class Game
    {
        public Game(End host) => Host = host;

        public End Host { get; }

        public Dictionary<int, End> Guests { get; } = new();

        public int NextPeer { get; set; } = 2;
    }

    private sealed class End : IMasterSocket
    {
        private readonly LoopbackMaster _master;

        public End(LoopbackMaster master) => _master = master;

        public MasterSocketState State { get; private set; } = MasterSocketState.Open;

        public string Fault => "";

        public Queue<MasterMessage> Inbox { get; } = new();

        public (Game Game, int Peer)? Role { get; set; }

        public void Send(MasterMessage message)
        {
            if (State == MasterSocketState.Open)
            {
                _master.Route(this, message);
            }
        }

        public bool TryReceive(out MasterMessage message) => Inbox.TryDequeue(out message!);

        public void Dispose()
        {
            if (State != MasterSocketState.Closed)
            {
                State = MasterSocketState.Closed;
                _master.Closed(this);
            }
        }
    }
}
