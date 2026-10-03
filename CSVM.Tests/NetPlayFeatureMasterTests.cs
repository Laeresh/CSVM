using System;
using System.Collections.Generic;
using System.Linq;
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

        Assert.True(door.AwaitingCode);
        Assert.Equal(CoopDoorText.AwaitingCode, CoopDoorText.HostCodeLine(door));

        listed.JoinCode = "K7Q-X3M";
        door.Step(0.016);
        door.Step(0.016);

        Assert.Equal("K7Q-X3M", door.JoinCode);
        Assert.False(door.AwaitingCode);
        Assert.Equal($"Internet code K7Q-X3M, public, on the games list. {CoopDoorText.CopyPress} copies it.", CoopDoorText.HostCodeLine(door));
        Assert.False(listed.Listing!.Unlisted);
    }

    [Fact]
    public void ACoopHostIsPrivateUnlessItsBoxSaysPublicAndADogfightHostTheOtherWayRound()
    {
        // Every open takes a fresh end, since a carrier binds one lobby in its life.
        ListedCarrier listed = null!;
        var door = new NetPlayFeature((_, _, _) => listed = new ListedCarrier(End(7)), (_, _) => End(7));

        door.OpenCoopHost(NetPlayFeature.CoopHumans - 1);
        door.Step(0.016);
        Assert.True(door.Private);
        Assert.True(listed.Listing!.Unlisted);
        door.Close();

        door.Take(new NetPlayerInfo { GameName = "Friends", Callsign = "Zachary", Private = false }, game: true);
        door.OpenCoopHost(NetPlayFeature.CoopHumans - 1);
        door.Step(0.016);
        Assert.False(listed.Listing!.Unlisted);
        door.Close();

        door.OpenDogfightHost(NetSeats.MaxPlayers - 1);
        door.Step(0.016);
        Assert.False(listed.Listing!.Unlisted);
        door.Close();

        door.Take(new NetPlayerInfo { GameName = "Friends", Callsign = "Zachary", Private = true }, game: true);
        door.OpenDogfightHost(NetSeats.MaxPlayers - 1);
        door.Step(0.016);
        Assert.True(listed.Listing!.Unlisted);
        listed.JoinCode = "K7Q-X3M";
        Assert.Contains("private, not on the games list", CoopDoorText.HostCodeLine(door), StringComparison.Ordinal);
    }

    [Fact]
    public void TheCopyKeyCopiesNothingWhileTheMasterAnswersThenTheCodeAndTheAddressAfterAFault()
    {
        var mesh = LoopbackTransport.Mesh(1, Clean, new Random(8));
        var listed = new ListedCarrier(mesh[0]);
        var copied = new List<string>();
        var door = new NetPlayFeature((_, _, _) => listed, (_, _) => mesh[0])
        {
            StableIpv6 = () => "2001:db8::7",
            CopyText = copied.Add,
        };
        door.OpenCoopHost(NetPlayFeature.CoopHumans - 1);

        // While the master server answers no line names the address, so the key copies nothing.
        Assert.True(door.AwaitingCode);
        Assert.False(door.CopyForGuests());
        Assert.Equal("", CoopDoorText.CopyTarget(door));
        Assert.Empty(copied);

        listed.JoinCode = "K7Q-X3M";
        Assert.Contains("Ctrl+C", CoopDoorText.HostBand(door), StringComparison.Ordinal);
        Assert.True(door.CopyForGuests());
        Assert.Equal(new[] { "K7Q-X3M" }, copied);
        Assert.StartsWith("NETWORK OPEN  0 guests  CODE K7Q-X3M  copied", CoopDoorText.HostBand(door), StringComparison.Ordinal);

        // ABLE-TO-FAIL CONTROL: after a fault the address is named again, and the key copies it.
        listed.JoinCode = null;
        listed.Fault = "the master server refused the listing";
        Assert.False(door.AwaitingCode);
        Assert.True(door.CopyForGuests());
        Assert.Equal(new[] { "K7Q-X3M", "2001:db8::7" }, copied);

        // A shut door copies nothing.
        door.Close();
        Assert.False(door.CopyForGuests());
        Assert.Equal(2, copied.Count);
    }

    [Fact]
    public void OnlyAKeyboardIsToldTheCopyKeyAndTheControlCopiesWhatTheLineNames()
    {
        const string stable = "2001:db8::7";
        var mesh = LoopbackTransport.Mesh(1, Clean, new Random(9));
        var listed = new ListedCarrier(mesh[0]);
        var door = new NetPlayFeature((_, _, _) => listed, (_, _) => mesh[0])
        {
            StableIpv6 = () => stable,
            CopyText = _ => { },
        };
        Assert.Equal("", CoopDoorText.CopyTarget(door));
        door.OpenDogfightHost(NetSeats.MaxPlayers - 1);

        // While the master server answers no line carries the mark.
        Assert.Equal("", CoopDoorText.CopyTarget(door));

        // After a fault the address line carries the mark, and the control copies that address.
        listed.Fault = "the master server refused the listing";
        Assert.Equal(stable, CoopDoorText.CopyTarget(door));
        Assert.Equal($"IPv6  {stable}  {CoopDoorText.CopyPress}", CoopDoorText.HostAddressLine(door, CopyWay.Keys));
        Assert.Equal($"IPv6  {stable}", CoopDoorText.HostAddressLine(door, CopyWay.Pad));
        Assert.Equal($"IPv6  {stable}", CoopDoorText.HostAddressLine(door, CopyWay.Pointer));

        listed.Fault = "";
        listed.JoinCode = "K7Q-X3M";
        Assert.Equal("K7Q-X3M", CoopDoorText.CopyTarget(door));
        const string line = "Internet code K7Q-X3M, public, on the games list.";
        Assert.Equal($"{line} {CoopDoorText.CopyPress} copies it.", CoopDoorText.HostCodeLine(door, CopyWay.Keys));
        Assert.Equal(line, CoopDoorText.HostCodeLine(door, CopyWay.Pad));
        Assert.Equal(new[] { line }, CoopDoorText.HostLobbyLines(door, CopyWay.Pointer));

        // ABLE-TO-FAIL CONTROL: once copied, every device reads the copied state.
        Assert.True(door.CopyForGuests());
        Assert.Equal($"{line} It is copied.", CoopDoorText.HostCodeLine(door, CopyWay.Pad));
        Assert.Equal($"{line} It is copied.", CoopDoorText.HostCodeLine(door, CopyWay.Keys));
    }

    [Fact]
    public void ACoopBandWaitsForTheCodeThenShowsItInPlaceOfTheAddressAndTheAddressWithItsReasonWithout()
    {
        const string stable = "2001:db8::7";
        var mesh = LoopbackTransport.Mesh(1, Clean, new Random(10));
        var listed = new ListedCarrier(mesh[0]) { JoinCode = "K7Q-X3M" };
        var mapped = new UpnpPortMapResult(UpnpPortMapOutcome.Mapped, NetPlayFeature.DefaultPort, "203.0.113.9", "mapped");
        var coded = new NetPlayFeature((_, _, _) => listed, (_, _) => mesh[0], new RouterAccess(port => mapped with { Port = port }, _ => { }))
        {
            StableIpv6 = () => stable,
            CopyText = _ => { },
        };
        coded.OpenCoopHost(NetPlayFeature.CoopHumans - 1);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (coded.Router.PortMap == null && DateTime.UtcNow < deadline)
        {
            coded.Step(0.016);
        }

        string band = CoopDoorText.HostBand(coded);
        Assert.Equal(
            $"NETWORK OPEN  0 guests  CODE K7Q-X3M  {CoopDoorText.CopyPress}\nPRIVATE  internet guests need the code", band);
        Assert.DoesNotContain(stable, band, StringComparison.Ordinal);
        Assert.DoesNotContain("203.0.113.9", band, StringComparison.Ordinal);

        // The address waits for the master server's outcome: while it is answering, the wait alone.
        var awaited = new ListedCarrier(End(16));
        var asking = new NetPlayFeature((_, _, _) => awaited, (_, _) => End(16)) { StableIpv6 = () => stable };
        asking.OpenCoopHost(NetPlayFeature.CoopHumans - 1);
        Assert.True(asking.AwaitingCode);
        string[] waiting = CoopDoorText.HostBand(asking).Split('\n');
        Assert.Equal(2, waiting.Length);
        Assert.StartsWith("NETWORK OPEN  port", waiting[0], StringComparison.Ordinal);
        Assert.Equal(CoopDoorText.AwaitingCode, waiting[1]);
        Assert.DoesNotContain(stable, CoopDoorText.HostBand(asking), StringComparison.Ordinal);
        Assert.DoesNotContain(CoopDoorText.CopyPress, CoopDoorText.HostBand(asking), StringComparison.Ordinal);

        // ABLE-TO-FAIL CONTROL: the outcome lands, a code and then a fault, and each replaces the wait.
        awaited.JoinCode = "K7Q-X3M";
        Assert.StartsWith("NETWORK OPEN  0 guests  CODE K7Q-X3M", CoopDoorText.HostBand(asking), StringComparison.Ordinal);
        awaited.JoinCode = null;
        awaited.Fault = "the server refused the listing";
        Assert.Equal(
            new[] { CoopDoorText.HostAddressLine(asking), "No internet code: the server refused the listing" },
            CoopDoorText.HostBand(asking).Split('\n').Skip(1));
        Assert.Contains(stable, CoopDoorText.HostBand(asking), StringComparison.Ordinal);

        var offline = new NetPlayFeature((_, _, _) => End(11), (_, _) => End(11))
        {
            StableIpv6 = () => stable,
            Master = new MasterDirectory(_ => Task.FromResult(Listed)),
        };
        offline.OpenCoopHost(NetPlayFeature.CoopHumans - 1);
        string[] lines = CoopDoorText.HostBand(offline).Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("NETWORK OPEN  port", lines[0], StringComparison.Ordinal);
        Assert.Equal(CoopDoorText.HostAddressLine(offline), lines[1]);
        Assert.Contains(stable, lines[1], StringComparison.Ordinal);
        Assert.Equal($"No internet code: {CoopDoorText.NoWebRtc}", lines[2]);
    }

    [Fact]
    public void ADogfightLobbyWaitsForTheCodeThenPinsItAloneAndTheAddressWithItsReasonWithout()
    {
        const string stable = "2001:db8::7";
        string address = $"IPv6  {stable}  {CoopDoorText.CopyPress}";
        var mesh = LoopbackTransport.Mesh(1, Clean, new Random(14));
        var listed = new ListedCarrier(mesh[0]);
        var door = new NetPlayFeature((_, _, _) => listed, (_, _) => mesh[0]) { StableIpv6 = () => stable };
        door.OpenDogfightHost(NetSeats.MaxPlayers - 1);
        door.Step(0.016);

        // The address waits for the master server's outcome: none while it is still answering.
        Assert.True(door.AwaitingCode);
        Assert.Equal(new[] { CoopDoorText.AwaitingCode }, CoopDoorText.HostLobbyLines(door));
        Assert.DoesNotContain(CoopDoorText.HostLobbyLines(door), line => line.Contains(stable, StringComparison.Ordinal));

        listed.JoinCode = "K7Q-X3M";
        Assert.Equal(new[] { CoopDoorText.HostCodeLine(door) }, CoopDoorText.HostLobbyLines(door));
        Assert.DoesNotContain(CoopDoorText.HostLobbyLines(door), line => line.Contains(stable, StringComparison.Ordinal));

        listed.JoinCode = null;
        listed.Fault = "the server refused the listing";
        Assert.Equal(new[] { address, "No internet code: the server refused the listing" }, CoopDoorText.HostLobbyLines(door));

        var offline = new NetPlayFeature((_, _, _) => End(15), (_, _) => End(15))
        {
            StableIpv6 = () => stable,
            Master = new MasterDirectory(_ => Task.FromResult(Listed)),
        };
        offline.OpenDogfightHost(NetSeats.MaxPlayers - 1);
        Assert.Equal(new[] { address, $"No internet code: {CoopDoorText.NoWebRtc}" }, CoopDoorText.HostLobbyLines(offline));

        // The pinned rows are the only place the address shows: no chat note was posted.
        Assert.Empty(door.Dogfight!.Chat);
        Assert.Empty(offline.Dogfight!.Chat);
    }

    [Fact]
    public void AHostWithAMasterServerButNoListingSaysWhyAndOneWithoutSaysNothing()
    {
        var mesh = LoopbackTransport.Mesh(1, Clean, new Random(9));
        var offline = new NetPlayFeature((_, _, _) => mesh[0], (_, _) => mesh[0])
        {
            Master = new MasterDirectory(_ => Task.FromResult(Listed)),
        };
        offline.OpenCoopHost(NetPlayFeature.CoopHumans - 1);
        Assert.Equal(CoopDoorText.NoWebRtc, offline.InternetFault);
        Assert.Equal($"No internet code: {CoopDoorText.NoWebRtc}", CoopDoorText.InternetLine(offline));

        var refused = new ListedCarrier(End(12)) { Fault = "the server lists as many games as it can; try again later" };
        var full = new NetPlayFeature((_, _, _) => refused, (_, _) => mesh[0]);
        full.OpenCoopHost(NetPlayFeature.CoopHumans - 1);
        Assert.False(full.AwaitingCode);
        Assert.Equal("No internet code: the server lists as many games as it can; try again later", CoopDoorText.InternetLine(full));

        refused.Fault = new string('x', 200);
        Assert.True(CoopDoorText.InternetLine(full).Length <= 76);
        Assert.EndsWith("...", CoopDoorText.InternetLine(full), StringComparison.Ordinal);

        // ABLE-TO-FAIL CONTROL: no master server set, so nothing is wrong and nothing is said.
        var lan = new NetPlayFeature((_, _, _) => End(13), (_, _) => End(13));
        lan.OpenCoopHost(NetPlayFeature.CoopHumans - 1);
        Assert.Equal("", lan.InternetFault);
        Assert.Equal("", CoopDoorText.InternetLine(lan));
        Assert.Equal("", CoopDoorText.HostCodeLine(lan));
    }

    [Fact]
    public void ACarriersOwnReasonNamesAFailedJoin()
    {
        var mesh = LoopbackTransport.Mesh(1, Clean, new Random(6));
        var down = new ListedCarrier(mesh[0]) { State = NetLinkState.Down, Why = "no game is listed under that code" };
        var door = new NetPlayFeature((_, _, _) => mesh[0], (_, _) => mesh[0]) { OpenCode = _ => down };

        Retype(door, "ABC-DEF");
        door.OpenJoin();
        door.Step(0.016);

        Assert.Equal(NetDoorStage.Failed, door.Stage);
        Assert.Equal("ABC-DEF: no game is listed under that code", door.Fault);
    }

    private static INetTransport End(int seed) => LoopbackTransport.Mesh(1, Clean, new Random(seed))[0];

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

        public string Fault { get; set; } = "";

        public string ListingFault => Fault;

        public NetLinkState State { get; set; } = NetLinkState.Up;

        public string Why { get; set; } = "";

        public NetLinkState LinkState => State;

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
