using System.Linq;
using CSVM.Net;
using Xunit;

namespace CSVM.Master.Tests;

/// <summary>
/// The hub with no socket: registration, the list, expiry, join by code and the signal relay, each
/// over fake sockets that keep what they were sent and a clock moved by hand.
/// </summary>
public class MasterHubTests
{
    [Fact]
    public void AHostIsGivenACodeAndItsGameIsListedUnderIt()
    {
        var hub = new MasterHub(new MasterOptions(), new ManualClock());
        var host = new FakeClient();

        hub.Receive(host, new MasterMessage { T = MasterWire.Host, Game = Listing() });

        var hosted = host.Last;
        Assert.Equal(MasterWire.Hosted, hosted.T);
        Assert.True(MasterWire.TryCode(hosted.Code, out string code));
        Assert.Equal(code, hosted.Code);
        var listed = Assert.Single(hub.List().Games);
        Assert.Equal(code, listed.Code);
        Assert.Equal("Skies", listed.Name);
        Assert.Equal(MasterWire.DogfightKind, listed.Kind);
        Assert.Equal("0.2", listed.Version);
    }

    [Fact]
    public void AListingIsHeldToTheWiresLimits()
    {
        var hub = new MasterHub(new MasterOptions(), new ManualClock());
        var listing = Listing(new string('x', 80), players: 99);
        listing.Kind = "deathmatch";
        listing.Status = "WAITING";
        listing.Cap = -3;

        hub.Receive(new FakeClient(), new MasterMessage { T = MasterWire.Host, Game = listing });

        var listed = Assert.Single(hub.List().Games);
        Assert.Equal(MasterWire.NameLimit, listed.Name.Length);
        Assert.Equal(MasterWire.PlayerLimit, listed.Players);
        Assert.Equal(0, listed.Cap);
        Assert.Equal("", listed.Kind);
        Assert.Equal("", listed.Status);
    }

    [Fact]
    public void AnUpdateChangesTheListingAndKeepsItAlive()
    {
        var clock = new ManualClock();
        var hub = new MasterHub(new MasterOptions(), clock);
        var host = new FakeClient();
        hub.Receive(host, new MasterMessage { T = MasterWire.Host, Game = Listing() });

        clock.Advance(MasterWire.ExpirySeconds - 1.0);
        hub.Receive(host, new MasterMessage { T = MasterWire.Update, Game = Listing(players: 3) });
        clock.Advance(MasterWire.ExpirySeconds - 1.0);

        Assert.Equal(0, hub.Sweep());
        Assert.Equal(3, Assert.Single(hub.List().Games).Players);
        Assert.Null(host.ClosedWhy);
    }

    [Fact]
    public void ASilentGameLeavesTheListAndItsHostAndGuestsAreTold()
    {
        var clock = new ManualClock();
        var hub = new MasterHub(new MasterOptions(), clock);
        var host = new FakeClient();
        var guest = new FakeClient("198.51.100.7");
        hub.Receive(host, new MasterMessage { T = MasterWire.Host, Game = Listing() });
        hub.Receive(guest, new MasterMessage { T = MasterWire.Join, Code = host.Last.Code });

        clock.Advance(MasterWire.ExpirySeconds - 0.5);
        Assert.Equal(0, hub.Sweep());
        clock.Advance(1.0);

        Assert.Equal(1, hub.Sweep());
        Assert.Empty(hub.List().Games);
        Assert.NotNull(host.ClosedWhy);
        Assert.Equal(MasterWire.Closed, guest.Last.T);
    }

    [Fact]
    public void AHostWhoseSocketClosesLeavesTheList()
    {
        var hub = new MasterHub(new MasterOptions(), new ManualClock());
        var host = new FakeClient();
        var guest = new FakeClient("198.51.100.7");
        hub.Receive(host, new MasterMessage { T = MasterWire.Host, Game = Listing() });
        hub.Receive(guest, new MasterMessage { T = MasterWire.Join, Code = host.Last.Code });

        hub.Closed(host);

        Assert.Empty(hub.List().Games);
        Assert.Equal(MasterWire.Closed, guest.Last.T);
        hub.Receive(guest, new MasterMessage { T = MasterWire.Signal, To = MasterHub.HostPeer, Kind = MasterWire.Answer, Sdp = "v=0" });
        Assert.Equal(MasterWire.Error, guest.Last.T);
    }

    [Fact]
    public void AJoinByCodeNumbersTheGuestAndTellsTheHostWithItsAddressAndIce()
    {
        var options = new MasterOptions { Stun = "stun:turn.example.org:3478", Turn = "turn:turn.example.org:3478", TurnSecret = "s" };
        var hub = new MasterHub(options, new ManualClock());
        var host = new FakeClient();
        var first = new FakeClient("198.51.100.7");
        var second = new FakeClient("198.51.100.8");
        hub.Receive(host, new MasterMessage { T = MasterWire.Host, Game = Listing() });
        string code = host.Last.Code!;

        // Typed in lower case and without the dash, the way a player might.
        hub.Receive(first, new MasterMessage { T = MasterWire.Join, Code = code.Replace("-", "").ToLowerInvariant() });
        hub.Receive(second, new MasterMessage { T = MasterWire.Join, Code = code });

        Assert.Equal(MasterWire.Joined, first.Last.T);
        Assert.Equal(MasterHub.FirstGuestPeer, first.Last.Peer);
        Assert.Equal(MasterHub.FirstGuestPeer + 1, second.Last.Peer);
        Assert.Equal(2, first.Last.Ice!.Count);
        var incoming = host.Of(MasterWire.Incoming).ToList();
        Assert.Equal(2, incoming.Count);
        Assert.Equal(MasterHub.FirstGuestPeer, incoming[0].Peer);
        Assert.Equal("198.51.100.7", incoming[0].Addr);
        Assert.Equal("turn:turn.example.org:3478", incoming[0].Ice![1].Urls.Single());
    }

    [Fact]
    public void AnUnlistedGameStaysOffTheListAndIsStillJoinedByItsCode()
    {
        var hub = new MasterHub(new MasterOptions(), new ManualClock());
        var open = new FakeClient();
        var hidden = new FakeClient("203.0.113.6");
        var guest = new FakeClient("198.51.100.7");
        hub.Receive(open, new MasterMessage { T = MasterWire.Host, Game = Listing("Open") });
        var secret = Listing("Hidden");
        secret.Unlisted = true;
        hub.Receive(hidden, new MasterMessage { T = MasterWire.Host, Game = secret });
        string code = hidden.Last.Code!;

        Assert.Equal("Open", Assert.Single(hub.List().Games).Name);
        Assert.Equal(2, hub.Count);

        hub.Receive(guest, new MasterMessage { T = MasterWire.Join, Code = code });
        Assert.Equal(MasterWire.Joined, guest.Last.T);
        Assert.Equal(MasterWire.Incoming, hidden.Last.T);

        // An update can list it, or take it off the list again.
        hub.Receive(hidden, new MasterMessage { T = MasterWire.Update, Game = Listing("Hidden") });
        Assert.Equal(2, hub.List().Games.Count);
        hub.Receive(hidden, new MasterMessage { T = MasterWire.Update, Game = secret });
        Assert.Single(hub.List().Games);
    }

    [Fact]
    public void AJoinToNoListedGameIsRefused()
    {
        var hub = new MasterHub(new MasterOptions(), new ManualClock());
        var guest = new FakeClient();

        hub.Receive(guest, new MasterMessage { T = MasterWire.Join, Code = "ZZZ-ZZZ" });
        hub.Receive(guest, new MasterMessage { T = MasterWire.Join, Code = "not a code" });

        Assert.All(guest.Inbox, message => Assert.Equal(MasterWire.Error, message.T));
        Assert.Equal(2, guest.Inbox.Count);
    }

    [Fact]
    public void SignalsPassOnlyBetweenAGuestAndItsOwnHostWithTheServersFrom()
    {
        var hub = new MasterHub(new MasterOptions(), new ManualClock());
        var host = new FakeClient();
        var guest = new FakeClient("198.51.100.7");
        var other = new FakeClient("198.51.100.8");
        hub.Receive(host, new MasterMessage { T = MasterWire.Host, Game = Listing() });
        hub.Receive(guest, new MasterMessage { T = MasterWire.Join, Code = host.Last.Code });
        hub.Receive(other, new MasterMessage { T = MasterWire.Join, Code = host.Last.Code });
        int peer = guest.Last.Peer!.Value;

        hub.Receive(host, new MasterMessage { T = MasterWire.Signal, To = peer, Kind = MasterWire.Offer, Sdp = "v=0 offer", Why = "smuggled" });
        hub.Receive(guest, new MasterMessage { T = MasterWire.Signal, To = MasterHub.HostPeer, From = 99, Kind = MasterWire.Answer, Sdp = "v=0 answer" });
        hub.Receive(guest, new MasterMessage { T = MasterWire.Signal, To = MasterHub.HostPeer, Kind = MasterWire.Candidate, Sdp = "candidate:1", Mid = "0", Index = 0 });

        var offer = guest.Of(MasterWire.Signal).Single();
        Assert.Equal(MasterHub.HostPeer, offer.From);
        Assert.Equal("v=0 offer", offer.Sdp);
        Assert.Null(offer.Why);
        var fromGuest = host.Of(MasterWire.Signal).ToList();
        Assert.Equal(2, fromGuest.Count);
        Assert.All(fromGuest, signal => Assert.Equal(peer, signal.From));
        Assert.Equal("0", fromGuest[1].Mid);
        Assert.Empty(other.Of(MasterWire.Signal));

        // A guest reaches only its host, and a host only a guest negotiating with it.
        hub.Receive(guest, new MasterMessage { T = MasterWire.Signal, To = peer + 1, Kind = MasterWire.Offer, Sdp = "x" });
        Assert.Equal(MasterWire.Error, guest.Last.T);
        hub.Receive(host, new MasterMessage { T = MasterWire.Signal, To = 77, Kind = MasterWire.Offer, Sdp = "x" });
        Assert.Equal(MasterWire.Error, host.Last.T);
        hub.Receive(host, new MasterMessage { T = MasterWire.Signal, To = peer, Kind = "bye", Sdp = "x" });
        Assert.Equal(MasterWire.Error, host.Last.T);
        Assert.Empty(other.Of(MasterWire.Signal));
    }

    [Fact]
    public void AGuestWhoseSocketClosesIsReportedToItsHost()
    {
        var hub = new MasterHub(new MasterOptions(), new ManualClock());
        var host = new FakeClient();
        var guest = new FakeClient("198.51.100.7");
        hub.Receive(host, new MasterMessage { T = MasterWire.Host, Game = Listing() });
        hub.Receive(guest, new MasterMessage { T = MasterWire.Join, Code = host.Last.Code });

        hub.Closed(guest);

        Assert.Equal(MasterWire.Left, host.Last.T);
        Assert.Equal(MasterHub.FirstGuestPeer, host.Last.Peer);
        Assert.Single(hub.List().Games);
    }

    [Fact]
    public void ASocketHoldsOneRole()
    {
        var hub = new MasterHub(new MasterOptions(), new ManualClock());
        var host = new FakeClient();
        hub.Receive(host, new MasterMessage { T = MasterWire.Host, Game = Listing() });
        string code = host.Last.Code!;

        hub.Receive(host, new MasterMessage { T = MasterWire.Host, Game = Listing("Second") });
        Assert.Equal(MasterWire.Error, host.Last.T);
        hub.Receive(host, new MasterMessage { T = MasterWire.Join, Code = code });
        Assert.Equal(MasterWire.Error, host.Last.T);

        var guest = new FakeClient();
        hub.Receive(guest, new MasterMessage { T = MasterWire.Update, Game = Listing("Hijack") });
        Assert.Equal(MasterWire.Error, guest.Last.T);
        Assert.Equal("Skies", Assert.Single(hub.List().Games).Name);
    }

    [Fact]
    public void TheGameCapsHoldPerAddressAndOverall()
    {
        var hub = new MasterHub(new MasterOptions { MaxGames = 3, MaxGamesPerAddress = 2 }, new ManualClock());
        var hosts = Enumerable.Range(0, 4).Select(i => new FakeClient(i < 3 ? "203.0.113.5" : "203.0.113.6")).ToList();

        foreach (var host in hosts)
        {
            hub.Receive(host, new MasterMessage { T = MasterWire.Host, Game = Listing() });
        }

        Assert.Equal(MasterWire.Hosted, hosts[0].Last.T);
        Assert.Equal(MasterWire.Hosted, hosts[1].Last.T);
        Assert.Equal(MasterWire.Error, hosts[2].Last.T);
        Assert.Equal(MasterWire.Hosted, hosts[3].Last.T);

        var late = new FakeClient("203.0.113.9");
        hub.Receive(late, new MasterMessage { T = MasterWire.Host, Game = Listing() });
        Assert.Equal(MasterWire.Error, late.Last.T);
        Assert.Equal(3, hub.Count);
    }

    [Fact]
    public void AGuestSocketOpenPastItsLimitIsClosedByTheSweep()
    {
        var clock = new ManualClock();
        var hub = new MasterHub(new MasterOptions { GuestSocketSeconds = 30 }, clock);
        var host = new FakeClient();
        var guest = new FakeClient("198.51.100.7");
        hub.Receive(host, new MasterMessage { T = MasterWire.Host, Game = Listing() });
        hub.Receive(guest, new MasterMessage { T = MasterWire.Join, Code = host.Last.Code });

        clock.Advance(20.0);
        hub.Receive(host, new MasterMessage { T = MasterWire.Update });
        hub.Sweep();
        Assert.Null(guest.ClosedWhy);

        clock.Advance(20.0);
        hub.Receive(host, new MasterMessage { T = MasterWire.Update });
        hub.Sweep();
        Assert.NotNull(guest.ClosedWhy);
        Assert.Null(host.ClosedWhy);
    }

    [Fact]
    public void ACodeThatIsTakenIsDrawnAgain()
    {
        // The first two codes drawn are the same, so the second host's is drawn a third time.
        int draws = 0;
        var hub = new MasterHub(new MasterOptions(), new ManualClock(), _ => draws++ < 12 ? 0 : 1);
        var first = new FakeClient();
        var second = new FakeClient("203.0.113.6");

        hub.Receive(first, new MasterMessage { T = MasterWire.Host, Game = Listing() });
        hub.Receive(second, new MasterMessage { T = MasterWire.Host, Game = Listing() });

        Assert.Equal("222-222", first.Last.Code);
        Assert.Equal("333-333", second.Last.Code);
        Assert.Equal(2, hub.Count);
    }

    private static MasterGame Listing(string name = "Skies", int players = 1) => new()
    {
        Name = name, Kind = MasterWire.DogfightKind, Players = players, Cap = 8, Status = MasterWire.Waiting, Version = "0.2",
    };
}
