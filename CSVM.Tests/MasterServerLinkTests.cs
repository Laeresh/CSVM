extern alias master;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CSVM.Launch;
using CSVM.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Xunit;
using MasterApp = master::CSVM.Master.MasterApp;

namespace CSVM.Tests;

/// <summary>
/// The game's own master-server code against the real server on an in-memory host. It covers the
/// shipped socket and HTTP fetch, the host's registration and the games list's directory. A guest
/// negotiates through the relay. No port is opened and no other machine is asked anything.
/// </summary>
public sealed class MasterServerLinkTests : IAsyncLifetime
{
    // A wait gives up only after Patience and this many looks at its condition. A loaded machine
    // slows the polls and the server alike, so a starved wait keeps its chances. An idle hang
    // still fails at Patience, since an idle wait makes this many polls in about two seconds.
    private const int PatiencePolls = 100;

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);
    private static readonly Uri Server = new("http://localhost/");

    private WebApplication _app = null!;
    private TestServer _server = null!;

    public async Task InitializeAsync()
    {
        _app = MasterApp.Build(Array.Empty<string>(), builder => builder.WebHost.UseTestServer());
        await _app.StartAsync();
        _server = _app.GetTestServer();
    }

    public async Task DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task AHostListsAndAGuestFindsItAndNegotiatesThroughTheRelay()
    {
        using var hostSocket = Open();
        var registration = new MasterRegistration(hostSocket);
        registration.List(new MasterGame
        {
            Name = "Pirates", Kind = MasterWire.DogfightKind, Players = 1, Cap = 8, Status = MasterWire.Waiting, Version = "0.2",
        });
        await Until(() =>
        {
            registration.Step(0.016);
            while (hostSocket.TryReceive(out var message))
            {
                registration.Take(message);
            }

            return registration.Code != null;
        });

        using var http = _server.CreateClient();
        var directory = new MasterDirectory(cancel => MasterServerLink.FetchGames(http, Server, cancel));
        directory.Ask();
        await Until(() => directory.Poll(0.016));
        var row = Assert.Single(directory.Games);
        Assert.Equal(registration.Code, row.Code);
        Assert.Equal("Pirates", row.Advert.Host);

        using var guestSocket = Open();
        guestSocket.Send(new MasterMessage { T = MasterWire.Join, Code = row.Code });
        var joined = await Next(guestSocket);
        Assert.Equal(MasterWire.Joined, joined.T);
        var incoming = await Next(hostSocket);
        Assert.Equal(MasterWire.Incoming, incoming.T);
        Assert.Equal(joined.Peer, incoming.Peer);

        hostSocket.Send(new MasterMessage { T = MasterWire.Signal, To = joined.Peer, Kind = MasterWire.Offer, Sdp = "v=0" });
        var offer = await Next(guestSocket);
        Assert.Equal(1, offer.From);
        Assert.Equal("v=0", offer.Sdp);

        hostSocket.Dispose();
        await Until(() => guestSocket.TryReceive(out var closed) && closed.T == MasterWire.Closed);
    }

    [Fact]
    public async Task ASocketToNothingClosesWithAReason()
    {
        using var socket = MasterServerLink.Open(_ => Task.FromException<System.Net.WebSockets.WebSocket>(
            new System.Net.WebSockets.WebSocketException("refused")));

        await Until(() => socket.State == MasterSocketState.Closed);

        Assert.Contains("unreachable", socket.Fault, StringComparison.Ordinal);
    }

    private static async Task<MasterMessage> Next(IMasterSocket socket)
    {
        MasterMessage? got = null;
        await Until(() => socket.TryReceive(out got));
        return got!;
    }

    private static async Task Until(Func<bool> done)
    {
        var waited = Stopwatch.StartNew();
        for (int polls = 0; !done(); polls++)
        {
            Assert.True(
                waited.Elapsed < Patience || polls < PatiencePolls,
                $"the condition never held in {waited.Elapsed.TotalSeconds:F1} s over {polls} polls");

            // Off xUnit's context, whose workers run other classes' tests, onto the server's pool.
            // The polls are then starved only when the server is.
            await Task.Delay(10).ConfigureAwait(false);
        }
    }

    private IMasterSocket Open()
    {
        var at = Utils.MasterAddress.At(Server, MasterWire.SocketPath, socket: true);
        return MasterServerLink.Open(cancel => _server.CreateWebSocketClient().ConnectAsync(at, cancel));
    }
}
