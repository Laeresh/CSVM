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
    public void AJoinToNoListedGameClosesItsSocketWithTheReason()
    {
        var hub = new MasterHub(new MasterOptions(), new ManualClock());
        var unknown = new FakeClient();
        var garbled = new FakeClient();

        hub.Receive(unknown, new MasterMessage { T = MasterWire.Join, Code = "ZZZ-ZZZ" });
        hub.Receive(garbled, new MasterMessage { T = MasterWire.Join, Code = "not a code" });

        Assert.Equal(MasterHub.NoGameWhy, unknown.ClosedWhy);
        Assert.Equal(MasterHub.NoGameWhy, garbled.ClosedWhy);
        Assert.Empty(unknown.Inbox);
        Assert.Empty(garbled.Inbox);
    }

    [Fact]
    public void AGameTakesNoMoreThanItsPendingGuestsAndOneThatLeavesFreesItsPlace()
    {
        var hub = new MasterHub(new MasterOptions { MaxPendingGuests = 3 }, new ManualClock());
        var host = new FakeClient();
        hub.Receive(host, new MasterMessage { T = MasterWire.Host, Game = Listing() });
        string code = host.Last.Code!;
        var guests = Enumerable.Range(0, 4).Select(i => new FakeClient($"198.51.100.{i + 10}")).ToList();

        foreach (var guest in guests)
        {
            hub.Receive(guest, new MasterMessage { T = MasterWire.Join, Code = code });
        }

        Assert.All(guests.Take(3), guest => Assert.Equal(MasterWire.Joined, guest.Last.T));
        Assert.Equal(MasterWire.Error, guests[3].Last.T);
        Assert.Null(guests[3].ClosedWhy);
        Assert.Equal(3, host.Of(MasterWire.Incoming).Count());

        // A linked guest closes its socket, which takes it off the game's pending guests.
        hub.Closed(guests[0]);
        var late = new FakeClient("198.51.100.20");
        hub.Receive(late, new MasterMessage { T = MasterWire.Join, Code = code });
        Assert.Equal(MasterWire.Joined, late.Last.T);
    }

    [Fact]
    public void OneAddressHoldsOnlyItsShareOfAGamesPendingGuests()
    {
        var hub = new MasterHub(new MasterOptions { MaxPendingGuestsPerAddress = 2 }, new ManualClock());
        var host = new FakeClient();
        var other = new FakeClient("203.0.113.6");
        hub.Receive(host, new MasterMessage { T = MasterWire.Host, Game = Listing() });
        hub.Receive(other, new MasterMessage { T = MasterWire.Host, Game = Listing("Other") });
        string code = host.Last.Code!;
        var crowd = Enumerable.Range(0, 3).Select(_ => new FakeClient("198.51.100.7")).ToList();

        foreach (var guest in crowd)
        {
            hub.Receive(guest, new MasterMessage { T = MasterWire.Join, Code = code });
        }

        Assert.Equal(MasterWire.Joined, crowd[0].Last.T);
        Assert.Equal(MasterWire.Joined, crowd[1].Last.T);
        Assert.Equal((MasterWire.Error, MasterHub.AddressPendingWhy), (crowd[2].Last.T, crowd[2].Last.Why));
        Assert.Equal(2, host.Of(MasterWire.Incoming).Count());

        // Another address still joins that game, and the same address another game.
        var stranger = new FakeClient("198.51.100.8");
        hub.Receive(stranger, new MasterMessage { T = MasterWire.Join, Code = code });
        Assert.Equal(MasterWire.Joined, stranger.Last.T);
        var elsewhere = new FakeClient("198.51.100.7");
        hub.Receive(elsewhere, new MasterMessage { T = MasterWire.Join, Code = other.Last.Code });
        Assert.Equal(MasterWire.Joined, elsewhere.Last.T);

        // Players behind one NAT joining one after another: each linked guest's socket closes.
        hub.Closed(crowd[0]);
        var next = new FakeClient("198.51.100.7");
        hub.Receive(next, new MasterMessage { T = MasterWire.Join, Code = code });
        Assert.Equal(MasterWire.Joined, next.Last.T);
    }

    [Fact]
    public void AnAddressMintsOnlyItsHourlyTurnCredentialsAndTheHostsForItsJoinsCountAgainstIt()
    {
        var clock = new ManualClock();
        var options = new MasterOptions { Turn = "turn:turn.example.org:3478", TurnSecret = "s", TurnMintsPerHour = 5 };
        var hub = new MasterHub(options, clock);
        var host = new FakeClient();
        hub.Receive(host, new MasterMessage { T = MasterWire.Host, Game = Listing() });
        string code = host.Last.Code!;

        // Two mints a join, so the third would take the address to six of five.
        var joins = Enumerable.Range(0, 3).Select(_ => new FakeClient("198.51.100.7")).ToList();
        foreach (var guest in joins)
        {
            hub.Receive(guest, new MasterMessage { T = MasterWire.Join, Code = code });
            hub.Closed(guest);
        }

        Assert.Single(joins[0].Of(MasterWire.Joined));
        Assert.Single(joins[1].Of(MasterWire.Joined));
        Assert.Equal((MasterWire.Error, MasterHub.TurnSpentWhy), (joins[2].Last.T, joins[2].Last.Why));
        Assert.Equal(2, host.Of(MasterWire.Incoming).Count());

        // The host's own allowance is untouched, so another address still joins its game.
        var stranger = new FakeClient("198.51.100.8");
        hub.Receive(stranger, new MasterMessage { T = MasterWire.Join, Code = code });
        Assert.Equal(MasterWire.Joined, stranger.Last.T);

        // An hour after the joins their mints have left the window, and not a second before.
        clock.Advance(MasterHub.MintWindow.TotalSeconds - 1.0);
        hub.Receive(host, new MasterMessage { T = MasterWire.Update });
        var early = new FakeClient("198.51.100.7");
        hub.Receive(early, new MasterMessage { T = MasterWire.Join, Code = code });
        Assert.Equal(MasterHub.TurnSpentWhy, early.Last.Why);

        clock.Advance(1.0);
        hub.Receive(host, new MasterMessage { T = MasterWire.Update });
        var later = new FakeClient("198.51.100.7");
        hub.Receive(later, new MasterMessage { T = MasterWire.Join, Code = code });
        Assert.Equal(MasterWire.Joined, later.Last.T);
    }

    [Fact]
    public void WithoutTurnNoJoinIsCountedAgainstAnAddress()
    {
        var hub = new MasterHub(new MasterOptions { Stun = "stun:turn.example.org:3478", TurnMintsPerHour = 2 }, new ManualClock());
        var host = new FakeClient();
        hub.Receive(host, new MasterMessage { T = MasterWire.Host, Game = Listing() });
        string code = host.Last.Code!;

        for (int i = 0; i < 5; i++)
        {
            var guest = new FakeClient("198.51.100.7");
            hub.Receive(guest, new MasterMessage { T = MasterWire.Join, Code = code });
            Assert.Equal(MasterWire.Joined, guest.Last.T);
            hub.Closed(guest);
        }
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

    [Fact]
    public void ABuildNamingNoProtocolIsServedAtTheDefaultMinimum()
    {
        var hub = new MasterHub(new MasterOptions(), new ManualClock());
        var host = new FakeClient();
        var guest = new FakeClient("198.51.100.7");

        hub.Receive(host, new MasterMessage { T = MasterWire.Host, Game = Listing() });
        hub.Receive(guest, new MasterMessage { T = MasterWire.Join, Code = host.Last.Code });

        Assert.Equal(1, hub.Oldest);
        Assert.Equal(MasterWire.Hosted, host.Of(MasterWire.Hosted).Single().T);
        Assert.Equal(MasterWire.Joined, guest.Last.T);
    }

    [Fact]
    public void ABuildBelowARaisedMinimumIsRefusedOnHostAndJoinAndStaysRefused()
    {
        var hub = new MasterHub(new MasterOptions { OldestProtocol = 2 }, new ManualClock());
        var current = new FakeClient("203.0.113.6");
        var oldHost = new FakeClient();
        var oldGuest = new FakeClient("198.51.100.7");
        var newGuest = new FakeClient("198.51.100.8");
        hub.Receive(current, new MasterMessage { T = MasterWire.Host, Game = Listing("Current"), Protocol = 2 });
        string code = current.Last.Code!;

        hub.Receive(oldHost, new MasterMessage { T = MasterWire.Host, Game = Listing("Old") });
        string refusal = oldHost.Last.Why!;
        hub.Receive(oldHost, new MasterMessage { T = MasterWire.Update, Game = Listing("Old") });
        hub.Receive(oldGuest, new MasterMessage { T = MasterWire.Join, Code = code, Protocol = 1 });
        hub.Receive(newGuest, new MasterMessage { T = MasterWire.Join, Code = code, Protocol = 2 });

        Assert.Contains("Update CSVM", refusal, System.StringComparison.Ordinal);
        Assert.All(oldHost.Inbox, message => Assert.Equal((MasterWire.Error, refusal), (message.T, message.Why)));
        Assert.Equal(2, oldHost.Inbox.Count);
        Assert.Null(oldHost.ClosedWhy);
        Assert.Equal((MasterWire.Error, refusal), (oldGuest.Last.T, oldGuest.Last.Why));
        Assert.Equal(MasterWire.Joined, newGuest.Last.T);
        Assert.Equal("Current", Assert.Single(hub.List().Games).Name);
        Assert.Equal(2, hub.List().Oldest);
    }

    [Fact]
    public void AStrayJoinFromAListedHostIsNotReadAsAnOutdatedBuild()
    {
        var clock = new ManualClock();
        var hub = new MasterHub(new MasterOptions { OldestProtocol = 2 }, clock);
        var host = new FakeClient();
        hub.Receive(host, new MasterMessage { T = MasterWire.Host, Game = Listing(), Protocol = 2 });
        string code = host.Last.Code!;

        hub.Receive(host, new MasterMessage { T = MasterWire.Join, Code = code });
        hub.Receive(host, new MasterMessage { T = MasterWire.Update, Game = Listing(players: 2) });

        Assert.NotEqual(MasterHub.OutdatedWhy, host.Last.Why);
        Assert.Equal(2, Assert.Single(hub.List().Games).Players);
        Assert.Equal(1, hub.Seen().Values.Sum());
    }

    [Fact]
    public void TheServerCountsTheProtocolsItIsAskedInWithEveryNewerOneInOneBucket()
    {
        var hub = new MasterHub(new MasterOptions(), new ManualClock());
        var host = new FakeClient();
        int newer = MasterWire.ProtocolVersion + 1;
        hub.Receive(host, new MasterMessage { T = MasterWire.Host, Game = Listing() });
        hub.Receive(new FakeClient("198.51.100.7"), new MasterMessage { T = MasterWire.Join, Code = host.Last.Code, Protocol = 1 });
        hub.Receive(new FakeClient("198.51.100.8"), new MasterMessage { T = MasterWire.Join, Code = host.Last.Code, Protocol = newer });
        hub.Receive(new FakeClient("198.51.100.9"), new MasterMessage { T = MasterWire.Join, Code = host.Last.Code, Protocol = int.MaxValue });
        hub.Receive(host, new MasterMessage { T = MasterWire.Update, Protocol = newer });

        var seen = hub.Seen();

        Assert.Equal(2, seen[1]);
        Assert.Equal(2, seen[newer]);
        Assert.Equal(2, seen.Count);
    }

    private static MasterGame Listing(string name = "Skies", int players = 1) => new()
    {
        Name = name, Kind = MasterWire.DogfightKind, Players = players, Cap = 8, Status = MasterWire.Waiting, Version = "0.2",
    };
}
