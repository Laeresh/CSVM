using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CSVM.Net;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The game's two master-server rules with no server. One is the games list's fetch cadence and
/// the rows it makes. The other is a host's registration, which says when its listing goes out.
/// The fetch is a delegate and the socket a fake that keeps what it was sent.
/// </summary>
[Trait("Tier", "Quick")]
public class MasterDirectoryTests
{
    private const string OneGame =
        "{\"games\":[{\"code\":\"K7Q-X3M\",\"name\":\"Friday Fliers\",\"kind\":\"coop\",\"players\":2,\"cap\":4,"
        + "\"password\":true,\"status\":\"inmission\",\"mission\":7,\"version\":\"0.2\"}]}";

    [Fact]
    public void AListedGameIsARowJoinedByItsCode()
    {
        var row = MasterDirectory.ToGame(MasterWire.TryReadList(OneGame)![0]);

        Assert.Equal("K7Q-X3M", row.Code);
        Assert.Equal("K7Q-X3M", row.Address);
        Assert.Equal(0, row.Port);
        Assert.Equal(NetSessionKind.CampaignCoop, row.Advert.Kind);
        Assert.Equal(NetSessionStatus.InMission, row.Advert.Status);
        Assert.Equal(2, row.Advert.Players);
        Assert.Equal(4, row.Advert.Cap);
        Assert.Equal(7, row.Advert.MissionSeq);
        Assert.True(row.Advert.Password);
        Assert.Equal("Friday Fliers", row.Advert.Host);
        Assert.Equal(new NetBuildVersion(0, 2), row.Version);
    }

    [Fact]
    public void AHostsAdvertListsAsTheRowAGuestReadsBack()
    {
        var advert = new SessionAdvertMessage(NetSessionKind.Dogfight, SessionAdvertMessage.NoMission, 3, "Pirates", NetSessionStatus.Full, 3, false);

        var listing = MasterDirectory.ListingOf(advert, new NetBuildVersion(0, 2));
        listing.Code = "ABC-DEF";
        var row = MasterDirectory.ToGame(listing);

        Assert.Equal(-1, listing.Mission);
        Assert.Equal(MasterWire.Full, listing.Status);
        Assert.Equal("0.2", listing.Version);
        Assert.Equal(advert, row.Advert);
    }

    [Fact]
    public void APrivateHostsListingIsUnlistedAndItsAdvertIsUnchanged()
    {
        var advert = new SessionAdvertMessage(NetSessionKind.CampaignCoop, 7, 1, "Zachary", NetSessionStatus.Waiting, 4, false);

        var hidden = MasterDirectory.ListingOf(advert, new NetBuildVersion(0, 2), unlisted: true);
        var open = MasterDirectory.ListingOf(advert, new NetBuildVersion(0, 2));
        hidden.Code = "ABC-DEF";

        Assert.True(hidden.Unlisted);
        Assert.False(open.Unlisted);
        Assert.Equal(advert, MasterDirectory.ToGame(hidden).Advert);
    }

    [Fact]
    public void TheServerIsAskedAtMostOnceEveryRefresh()
    {
        int asked = 0;
        var directory = new MasterDirectory(_ =>
        {
            asked++;
            return Task.FromResult(OneGame);
        });

        directory.Ask();
        Assert.True(directory.Poll(0.0));
        Assert.Single(directory.Games);
        for (int i = 0; i < 4; i++)
        {
            directory.Ask();
            directory.Poll(1.0);
        }

        Assert.Equal(1, asked);
        directory.Poll(1.5);
        directory.Ask();
        Assert.Equal(2, asked);
        Assert.Equal(1, directory.Answers);
        directory.Poll(0.0);
        Assert.Equal(2, directory.Answers);
    }

    [Fact]
    public void AFailedFetchKeepsTheRowsAndSaysWhy()
    {
        bool fail = false;
        var directory = new MasterDirectory(_ => fail ? Task.FromException<string>(new InvalidOperationException("refused")) : Task.FromResult(OneGame));
        directory.Ask();
        directory.Poll(0.0);

        fail = true;
        directory.Poll(MasterDirectory.RefreshSeconds);
        directory.Ask();
        Assert.True(directory.Poll(0.0));

        Assert.Contains("refused", directory.Fault, StringComparison.Ordinal);
        Assert.Single(directory.Games);
    }

    [Fact]
    public void AServerPastThisBuildsProtocolListsNoGamesAndSaysWhy()
    {
        string newer = OneGame.Insert(OneGame.Length - 1, ",\"protocol\":2,\"oldest\":2");
        var directory = new MasterDirectory(_ => Task.FromResult(newer));

        directory.Ask();
        Assert.True(directory.Poll(0.0));

        Assert.True(directory.Outdated);
        Assert.Empty(directory.Games);
        Assert.Contains("too old", directory.Fault, StringComparison.Ordinal);
        directory.Forget();
        Assert.False(directory.Outdated);
    }

    [Fact]
    public void ForgettingCancelsTheFetchAndDropsItsAnswer()
    {
        var answer = new TaskCompletionSource<string>();
        CancellationToken seen = default;
        var directory = new MasterDirectory(cancel =>
        {
            seen = cancel;
            return answer.Task;
        });
        directory.Ask();
        Assert.True(directory.Asking);

        directory.Forget();
        answer.SetResult(OneGame);
        directory.Poll(0.0);

        Assert.True(seen.IsCancellationRequested);
        Assert.False(directory.Asking);
        Assert.Empty(directory.Games);
    }

    [Fact]
    public void ARegistrationWaitsForTheSocketThenHostsOnce()
    {
        var socket = new FakeMasterSocket { State = MasterSocketState.Connecting };
        var registration = new MasterRegistration(socket);
        registration.List(Listing(1));

        registration.Step(1.0);
        Assert.Empty(socket.Sent);

        socket.State = MasterSocketState.Open;
        registration.Step(0.1);
        registration.Step(0.1);

        var host = Assert.Single(socket.Sent);
        Assert.Equal(MasterWire.Host, host.T);
        Assert.Equal("Pirates", host.Game!.Name);
        Assert.Equal(MasterWire.ProtocolVersion, host.Protocol);
    }

    [Fact]
    public void AChangedListingGoesAtOnceAndAnUnchangedOneOnTheHeartbeat()
    {
        var socket = new FakeMasterSocket();
        var registration = new MasterRegistration(socket);
        registration.List(Listing(1));
        registration.Step(0.0);

        registration.List(Listing(1));
        registration.Step(MasterWire.HeartbeatSeconds - 1.0);
        Assert.Single(socket.Sent);

        registration.List(Listing(2));
        registration.Step(0.1);
        Assert.Equal(2, socket.Sent.Count);
        Assert.Equal(MasterWire.Update, socket.Sent[1].T);
        Assert.Equal(2, socket.Sent[1].Game!.Players);

        registration.Step(MasterWire.HeartbeatSeconds);
        Assert.Equal(3, socket.Sent.Count);
        Assert.Equal(3, registration.Sent);
    }

    [Fact]
    public void ARegistrationTakesItsCodeOrItsRefusal()
    {
        var socket = new FakeMasterSocket();
        var registration = new MasterRegistration(socket);

        Assert.False(registration.Take(new MasterMessage { T = MasterWire.Incoming, Peer = 2 }));
        Assert.True(registration.Take(new MasterMessage { T = MasterWire.Error, Why = "full" }));
        Assert.Equal("full", registration.Fault);
        Assert.True(registration.Take(new MasterMessage { T = MasterWire.Hosted, Code = "k7q-x3m" }));
        Assert.Equal("K7Q-X3M", registration.Code);
        Assert.Equal("", registration.Fault);

        // Once listed, an error is about something else and is not the registration's.
        Assert.False(registration.Take(new MasterMessage { T = MasterWire.Error, Why = "no such peer" }));
        socket.State = MasterSocketState.Closed;
        socket.Fault = "the master server is unreachable";
        Assert.Equal("the master server is unreachable", registration.Fault);
    }

    private static MasterGame Listing(int players) => new()
    {
        Name = "Pirates", Kind = MasterWire.DogfightKind, Players = players, Cap = 8, Status = MasterWire.Waiting, Version = "0.2",
    };
}

/// <summary>A master socket that keeps what it was sent and hands back what a test queued.</summary>
internal sealed class FakeMasterSocket : IMasterSocket
{
    public MasterSocketState State { get; set; } = MasterSocketState.Open;

    public string Fault { get; set; } = "";

    public List<MasterMessage> Sent { get; } = new();

    public Queue<MasterMessage> Arriving { get; } = new();

    public bool Disposed { get; private set; }

    public void Send(MasterMessage message) => Sent.Add(message);

    public bool TryReceive(out MasterMessage message) => Arriving.TryDequeue(out message!);

    public void Dispose()
    {
        Disposed = true;
        State = MasterSocketState.Closed;
    }
}
