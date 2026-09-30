using System;
using System.Collections.Generic;
using CSVM.Net;
using CSVM.UI.Menu;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// A host lobby's admission: the optional password it asks before a peer reaches any list. It also
/// holds the ban a boot leaves on the booted guest's address until the session closes. Both are the
/// host's checks alone, and neither changes what an admitted peer hears.
/// </summary>
public class NetLobbyAdmissionTests
{
    private static readonly LoopbackConditions Clean = new(0.0, 0.0, 0.0);

    [Fact]
    public void TheAdvertCarriesThePasswordMarkInItsOldReservedByte()
    {
        var advert = new SessionAdvertMessage(NetSessionKind.Dogfight, 3, 2, "Friday Fliers", NetSessionStatus.Waiting, 6, Password: true);
        var buffer = new byte[SessionAdvertMessage.Size];
        Assert.Equal(SessionAdvertMessage.Size, advert.Write(buffer));
        Assert.Equal(0x01, buffer[9]);
        Assert.True(SessionAdvertMessage.TryRead(buffer, out var read));
        Assert.Equal(advert, read);

        // ABLE-TO-FAIL CONTROL: the byte a build a patch older writes, zero, reads as no password.
        buffer[9] = 0;
        Assert.True(SessionAdvertMessage.TryRead(buffer, out var older));
        Assert.False(older.Password);
        Assert.Equal(advert with { Password = false }, older);
    }

    [Fact]
    public void ThePasswordWordRoundTripsAndTheNewCloseReasonsRead()
    {
        var buffer = new byte[JoinPasswordMessage.Size];
        Assert.Equal(JoinPasswordMessage.Size, new JoinPasswordMessage(false, "swordfish").Write(buffer));
        Assert.Equal(0x0060, BitConverter.ToUInt16(buffer, 0));
        Assert.True(JoinPasswordMessage.TryRead(buffer, out var answer));
        Assert.Equal(new JoinPasswordMessage(false, "swordfish"), answer);

        // Fourteen characters of three UTF-8 bytes each, the box's worst case, fit whole.
        string wide = new string('€', NetPlayerInfo.PasswordLimit);
        new JoinPasswordMessage(false, wide).Write(buffer);
        Assert.True(JoinPasswordMessage.TryRead(buffer, out var whole));
        Assert.Equal(wide, whole.Password);

        new JoinPasswordMessage(true, "").Write(buffer);
        Assert.True(JoinPasswordMessage.TryRead(buffer, out var admitted));
        Assert.True(admitted.Admitted);
        Assert.False(SessionAdvertMessage.TryRead(buffer, out _));

        var closed = new byte[SessionClosedMessage.Size];
        foreach (var reason in new[] { NetCloseReason.Booted, NetCloseReason.WrongPassword })
        {
            new SessionClosedMessage(reason).Write(closed);
            Assert.True(SessionClosedMessage.TryRead(closed, out var read));
            Assert.Equal(reason, read.Reason);
        }

        closed[4] = 6;
        Assert.True(SessionClosedMessage.TryRead(closed, out var unknown));
        Assert.Equal(NetCloseReason.Unknown, unknown.Reason);
    }

    [Fact]
    public void TheRightPasswordAdmitsAndOnlyThenDoesTheGuestReachThePeerList()
    {
        var mesh = LoopbackTransport.Mesh(2, Clean, new Random(3));
        var host = new NetLobby(mesh[0], default, "swordfish");
        var guest = new NetLobby(mesh[1], default, joinPassword: "swordfish");
        int peer = mesh[1].LocalPeer;
        Assert.Contains(peer, host.AwaitingPassword);
        Assert.Empty(host.AllPeers);

        host.Advertise(Advert(password: true));
        Pump(host, guest);

        Assert.True(guest.Admitted);
        Assert.Empty(host.AwaitingPassword);
        Assert.Equal(new[] { peer }, host.AllPeers);
        Assert.Empty(host.TurnedAway);
    }

    [Fact]
    public void AWrongPasswordIsTurnedAwayBeforeAdmissionAndNothingItSendsIsKept()
    {
        var mesh = LoopbackTransport.Mesh(2, Clean, new Random(5));
        var host = new NetLobby(mesh[0], default, "swordfish");
        var guest = new NetLobby(mesh[1], default, joinPassword: "sword");
        host.Advertise(Advert(password: true));
        guest.Step(0.016);

        // What the guest sends behind its answer is never kept, not even as a held payload.
        guest.Tell(mesh[0].LocalPeer, new CoopPickMessage(1, true, 3, default, "Sneak"));
        Pump(host, guest);

        Assert.False(guest.Admitted);
        Assert.Empty(host.AllPeers);
        Assert.Equal(new[] { (mesh[1].LocalPeer, NetCloseReason.WrongPassword) }, host.TurnedAway);
        Assert.Empty(host.Picks);
        Assert.Equal(0, host.Held);

        // A guest's own word that it is admitted admits nobody.
        var fresh = LoopbackTransport.Mesh(2, Clean, new Random(7));
        var strict = new NetLobby(fresh[0], default, "swordfish");
        var forger = new NetLobby(fresh[1]);
        forger.Tell(fresh[0].LocalPeer, new JoinPasswordMessage(true, ""));
        Pump(strict, forger);
        Assert.Contains(fresh[1].LocalPeer, strict.AwaitingPassword);
        Assert.Empty(strict.AllPeers);
    }

    [Fact]
    public void AGuestThatNeverAnswersIsTurnedAwayOnceTheWaitRunsOut()
    {
        var mesh = LoopbackTransport.Mesh(2, Clean, new Random(9));
        var host = new NetLobby(mesh[0], default, "swordfish");

        // A build a patch older reads no password mark, so it answers nothing.
        var older = new NetLobby(mesh[1]);
        host.Advertise(Advert(password: true));
        Pump(host, older);
        Assert.Contains(mesh[1].LocalPeer, host.AwaitingPassword);

        host.Step(NetLobby.PasswordWaitSeconds);
        Assert.Equal(new[] { (mesh[1].LocalPeer, NetCloseReason.WrongPassword) }, host.TurnedAway);
        Assert.Empty(host.AllPeers);
    }

    [Fact]
    public void ASessionWithoutAPasswordAsksNothingAndSendsNoPasswordWord()
    {
        var host = new AddressedTransport(localPeer: 1);
        var lobby = new NetLobby(host);
        lobby.Advertise(Advert(password: false));
        host.Connect(4, "10.0.0.4");

        Assert.False(lobby.AsksPassword);
        Assert.Equal(new[] { 4 }, lobby.AllPeers);
        Assert.DoesNotContain(host.Sent, sent => JoinPasswordMessage.TryRead(sent.Bytes, out _));

        // ABLE-TO-FAIL CONTROL: a guest answers only an advert that asks, however it was built.
        var mesh = LoopbackTransport.Mesh(2, Clean, new Random(11));
        var open = new NetLobby(mesh[0]);
        var guest = new NetLobby(mesh[1], default, joinPassword: "swordfish");
        open.Advertise(Advert(password: false));
        Pump(open, guest);
        Assert.Equal(new[] { mesh[1].LocalPeer }, open.AllPeers);
        Assert.False(guest.Admitted);
    }

    [Fact]
    public void ABootBansTheAddressUntilTheLobbyClosesAndTheRightPasswordDoesNotBuyItBack()
    {
        var host = new AddressedTransport(localPeer: 1);
        var lobby = new NetLobby(host, default, "swordfish");
        host.Connect(4, "10.0.0.4");
        host.Deliver(4, Bytes(new JoinPasswordMessage(false, "swordfish")));
        Assert.Equal(new[] { 4 }, lobby.AllPeers);

        Assert.True(lobby.Boot(4));
        Assert.Equal(1, lobby.Banned);
        Assert.True(lobby.IsBanned("10.0.0.4"));
        Assert.Empty(lobby.AllPeers);
        Assert.Equal(new[] { (4, NetCloseReason.Booted) }, lobby.TurnedAway);

        // The same machine comes back on a new connection and is refused before its password.
        host.Drop(4);
        host.Connect(9, "10.0.0.4");
        Assert.Equal(new[] { (9, NetCloseReason.Booted) }, lobby.TurnedAway);
        Assert.Empty(lobby.AwaitingPassword);
        host.Deliver(9, Bytes(new JoinPasswordMessage(false, "swordfish")));
        Assert.Empty(lobby.AllPeers);

        // ABLE-TO-FAIL CONTROL: another machine is still asked, and admitted on the password.
        host.Connect(12, "10.0.0.12");
        host.Deliver(12, Bytes(new JoinPasswordMessage(false, "swordfish")));
        Assert.Equal(new[] { 12 }, lobby.AllPeers);

        // A new session is a new lobby, whose ban list starts empty.
        var reopened = new NetLobby(new AddressedTransport(localPeer: 1));
        Assert.Equal(0, reopened.Banned);
    }

    [Fact]
    public void ABootOnACarrierWithNoAddressStillSendsTheGuestAwayButBansNothing()
    {
        var host = new AddressedTransport(localPeer: 1);
        var lobby = new NetLobby(host);
        host.Connect(4, null);

        Assert.False(lobby.Boot(4));
        Assert.Equal(0, lobby.Banned);
        Assert.Equal(new[] { (4, NetCloseReason.Booted) }, lobby.TurnedAway);
    }

    [Fact]
    public void TheGamesListReadsNeedPasswordUnlessTheGameIsFull()
    {
        Assert.Equal(CoopDoorText.NeedPassword, CoopDoorText.Status(Advert(password: true)));
        Assert.Equal(CoopDoorText.NeedPassword, CoopDoorText.Status(Advert(password: true) with { Status = NetSessionStatus.InMission }));
        Assert.Equal("Full", CoopDoorText.Status(Advert(password: true) with { Status = NetSessionStatus.Full }));
        Assert.Equal("Waiting", CoopDoorText.Status(Advert(password: false)));
        Assert.True(CoopDoorText.Joinable(Advert(password: true)));
        Assert.Equal("[Nathan was booted from the game.]", CoopDoorText.BootedLine("Nathan"));
    }

    private static SessionAdvertMessage Advert(bool password) =>
        new(NetSessionKind.Dogfight, 0, 1, "Friday Fliers", NetSessionStatus.Waiting, 16, password);

    private static byte[] Bytes<T>(T message)
        where T : struct, INetMessage<T>
    {
        var buffer = new byte[512];
        return buffer.AsSpan(0, message.Write(buffer)).ToArray();
    }

    private static void Pump(NetLobby host, NetLobby guest)
    {
        for (int frame = 0; frame < 3; frame++)
        {
            host.Step(0.016);
            guest.Step(0.016);
        }
    }

    // A host carrier whose peers connect on demand from a named address, and which keeps what it
    // was asked to send.
    private sealed class AddressedTransport : INetTransport, INetPeerAddress
    {
        private readonly List<int> _peers = new();
        private readonly Dictionary<int, string?> _addresses = new();
        private INetTransportListener? _listener;

        public AddressedTransport(int localPeer) => LocalPeer = localPeer;

        public List<(int Peer, byte[] Bytes)> Sent { get; } = new();

        public int LocalPeer { get; }

        public IReadOnlyList<int> Peers => _peers;

        public string? AddressOf(int peer) => _addresses.TryGetValue(peer, out var address) ? address : null;

        public void Bind(INetTransportListener listener) => _listener = listener;

        public void Connect(int peer, string? address)
        {
            _peers.Add(peer);
            _addresses[peer] = address;
            _listener?.OnPeerConnected(peer);
        }

        public void Deliver(int peer, byte[] payload) => _listener?.OnPayload(peer, 0, payload);

        public void Drop(int peer)
        {
            _peers.Remove(peer);
            _listener?.OnPeerDisconnected(peer);
        }

        public void Send(int peer, ReadOnlySpan<byte> payload, NetReliability reliability, int channel = 0) =>
            Sent.Add((peer, payload.ToArray()));

        public void Disconnect(int peer) => Drop(peer);

        public void Step(double dt)
        {
        }
    }
}
