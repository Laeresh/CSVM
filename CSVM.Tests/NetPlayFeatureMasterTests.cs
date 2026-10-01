using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CSVM.Net;
using CSVM.UI.Menu;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The multiplayer door with a master server. The games list carries the server's games after the
/// LAN's, and a listed game and a typed code both join through the code opener. A host hands its
/// carrier the listing it advertises, and its lobby names the code. A door with no master server
/// behaves as before. The server is a canned list and the carriers loopback ends.
/// </summary>
[Trait("Tier", "Quick")]
public class NetPlayFeatureMasterTests
{
    private const string Listed =
        "{\"games\":[{\"code\":\"K7Q-X3M\",\"name\":\"Pirates\",\"kind\":\"dogfight\",\"players\":1,\"cap\":8,"
        + "\"status\":\"waiting\",\"version\":\"unknown\"}]}";

    private static readonly LoopbackConditions Clean = new(0.0, 0.0, 0.0);

    [Fact]
    public void TheGamesListCarriesTheMasterServersGames()
    {
        var mesh = LoopbackTransport.Mesh(1, Clean, new Random(1));
        var door = new NetPlayFeature((_, _, _) => mesh[0], (_, _) => mesh[0])
        {
            Master = new MasterDirectory(_ => Task.FromResult(Listed)),
        };

        Assert.True(door.CanSearch);
        Assert.False(door.Searching);
        door.Search();
        int before = door.Revision;
        door.Step(0.016);

        Assert.True(door.Searching);
        var game = Assert.Single(door.Games);
        Assert.Equal("K7Q-X3M", game.Code);
        Assert.Equal("Pirates", game.Advert.Host);
        Assert.True(door.Revision > before);

        door.StopSearch();
        Assert.Empty(door.Games);
        Assert.False(door.Searching);
    }

    [Fact]
    public void AListedGameJoinsByItsCode()
    {
        var mesh = LoopbackTransport.Mesh(2, Clean, new Random(2));
        string? opened = null;
        var door = new NetPlayFeature((_, _, _) => mesh[0], (_, _) => throw new InvalidOperationException("no direct join"))
        {
            Master = new MasterDirectory(_ => Task.FromResult(Listed)),
            OpenCode = code =>
            {
                opened = code;
                return mesh[1];
            },
        };
        door.Search();
        door.Step(0.016);

        door.JoinGame(door.Games[0]);
        door.Step(0.016);

        Assert.Equal("K7Q-X3M", opened);
        Assert.Equal(NetDoorStage.Joined, door.Stage);
        Assert.Equal("K7Q-X3M", door.JoinName);
    }

    [Fact]
    public void ATypedCodeJoinsThroughTheMasterServerAndAnAddressDoesNot()
    {
        var mesh = LoopbackTransport.Mesh(2, Clean, new Random(3));
        var asked = new List<string>();
        var door = new NetPlayFeature((_, _, _) => mesh[0], (address, _) =>
        {
            asked.Add($"direct {address}");
            return mesh[1];
        })
        {
            OpenCode = code =>
            {
                asked.Add($"code {code}");
                return mesh[1];
            },
        };

        Retype(door, "k7q-x3m");
        door.OpenJoin();
        door.Close();
        Retype(door, "k7qx3m");
        door.OpenJoin();
        door.Close();

        Assert.Equal(new[] { "code K7Q-X3M", "direct k7qx3m" }, asked);
    }

    [Fact]
    public void WithNoMasterServerACodeIsAnAddress()
    {
        var mesh = LoopbackTransport.Mesh(2, Clean, new Random(4));
        string? direct = null;
        var door = new NetPlayFeature((_, _, _) => mesh[0], (address, _) =>
        {
            direct = address;
            return mesh[1];
        });

        Retype(door, "K7Q-X3M");
        door.OpenJoin();

        Assert.Equal("K7Q-X3M", direct);
        Assert.False(door.CanSearch);
        Assert.Null(door.JoinCode);
    }

    [Fact]
    public void AHostListsItsAdvertAndItsLobbyNamesTheCode()
    {
        var mesh = LoopbackTransport.Mesh(1, Clean, new Random(5));
        var listed = new ListedCarrier(mesh[0]);
        var door = new NetPlayFeature((_, _, _) => listed, (_, _) => mesh[0]) { Version = new NetBuildVersion(0, 2) };
        door.Take(new NetPlayerInfo { GameName = "Pirates", Callsign = "Laeresh", MaxPlayers = 6 }, game: true);

        door.OpenDogfightHost(NetSeats.MaxPlayers - 1);
        door.Step(0.016);
        Assert.Equal("Pirates", listed.Listing!.Name);
        Assert.Equal(MasterWire.DogfightKind, listed.Listing.Kind);
        Assert.Equal(6, listed.Listing.Cap);
        Assert.Equal("0.2", listed.Listing.Version);
        Assert.Null(door.JoinCode);

        listed.JoinCode = "K7Q-X3M";
        door.Step(0.016);
        door.Step(0.016);

        Assert.Equal("K7Q-X3M", door.JoinCode);
        Assert.Single(door.Dogfight!.Chat, line => line.Text == CoopDoorText.JoinCodeNote("K7Q-X3M"));
    }

    [Fact]
    public void ACarriersOwnReasonNamesAFailedJoin()
    {
        var mesh = LoopbackTransport.Mesh(1, Clean, new Random(6));
        var down = new ListedCarrier(mesh[0]) { State = EnetLinkState.Down, Why = "no game is listed under that code" };
        var door = new NetPlayFeature((_, _, _) => mesh[0], (_, _) => mesh[0]) { OpenCode = _ => down };

        Retype(door, "ABC-DEF");
        door.OpenJoin();
        door.Step(0.016);

        Assert.Equal(NetDoorStage.Failed, door.Stage);
        Assert.Equal("ABC-DEF: no game is listed under that code", door.Fault);
    }

    private static void Retype(NetPlayFeature door, string address)
    {
        while (door.Address.Length > 0)
        {
            door.EraseAddress();
        }

        door.TypeAddress(address);
    }

    // A loopback end that also lists and reports a link, as the merged WebRTC host does.
    private sealed class ListedCarrier : INetTransport, INetListing, INetLink
    {
        private readonly INetTransport _inner;

        public ListedCarrier(INetTransport inner) => _inner = inner;

        public MasterGame? Listing { get; private set; }

        public string? JoinCode { get; set; }

        public string ListingFault => "";

        public EnetLinkState State { get; set; } = EnetLinkState.Up;

        public string Why { get; set; } = "";

        public EnetLinkState LinkState => State;

        public int PendingPayloads => 0;

        public string LinkFault => Why;

        public int LocalPeer => _inner.LocalPeer;

        public IReadOnlyList<int> Peers => _inner.Peers;

        public void List(MasterGame listing) => Listing = listing;

        public void Bind(INetTransportListener listener) => _inner.Bind(listener);

        public void Send(int peer, ReadOnlySpan<byte> payload, NetReliability reliability, int channel = 0) =>
            _inner.Send(peer, payload, reliability, channel);

        public void Disconnect(int peer) => _inner.Disconnect(peer);

        public void Step(double dt) => _inner.Step(dt);
    }
}
