using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CSVM.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSVM.Master.Tests;

/// <summary>
/// The whole server on an in-memory host: the list and health calls over HTTP, and a host and a
/// guest negotiating over real WebSockets through the server's own socket loop. No port is opened
/// and no other machine is asked anything.
/// </summary>
public sealed class MasterEndpointTests : IAsyncLifetime
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private WebApplication _app = null!;
    private TestServer _server = null!;

    public async Task InitializeAsync()
    {
        _app = MasterApp.Build(Array.Empty<string>(), builder =>
        {
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Master:Stun"] = "stun:turn.example.org:3478",
                ["Master:Turn"] = "turn:turn.example.org:3478?transport=udp",
                ["Master:TurnSecret"] = "test-secret",
                ["Master:ListPerMinute"] = "5",
            });
        });
        await _app.StartAsync();
        _server = _app.GetTestServer();
    }

    public async Task DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task AnEmptyServerListsNoGamesAndIsHealthy()
    {
        using var http = _server.CreateClient();

        string list = await http.GetStringAsync(MasterWire.GamesPath);
        string health = await http.GetStringAsync(MasterWire.HealthPath);

        Assert.Empty(MasterWire.TryReadList(list, out int oldest)!);
        Assert.Equal(MasterHub.OldestProtocol, oldest);
        Assert.Contains($"\"protocol\":{MasterWire.ProtocolVersion}", list, StringComparison.Ordinal);
        Assert.Contains("\"ok\":true", health, StringComparison.Ordinal);
        Assert.Contains($"\"protocol\":{MasterWire.ProtocolVersion}", health, StringComparison.Ordinal);
        Assert.Contains($"\"oldest\":{MasterHub.OldestProtocol}", health, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheListIsRateLimitedPerAddress()
    {
        using var http = _server.CreateClient();
        var codes = new List<HttpStatusCode>();
        for (int i = 0; i < 7; i++)
        {
            using var answer = await http.GetAsync(MasterWire.GamesPath);
            codes.Add(answer.StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, codes[0]);
        Assert.Equal(HttpStatusCode.TooManyRequests, codes[^1]);
    }

    [Fact]
    public async Task AHostListsItsGameAndAGuestNegotiatesThroughTheServer()
    {
        using var host = await Open();
        await Send(host, new MasterMessage
        {
            T = MasterWire.Host,
            Game = new MasterGame { Name = "Pirates", Kind = MasterWire.DogfightKind, Players = 1, Cap = 4, Status = MasterWire.Waiting, Version = "0.2" },
        });
        var hosted = await Next(host);
        Assert.Equal(MasterWire.Hosted, hosted.T);

        using var http = _server.CreateClient();
        var listed = Assert.Single(MasterWire.TryReadList(await http.GetStringAsync(MasterWire.GamesPath))!);
        Assert.Equal(hosted.Code, listed.Code);
        Assert.Equal("Pirates", listed.Name);

        using var guest = await Open();
        await Send(guest, new MasterMessage { T = MasterWire.Join, Code = hosted.Code });
        var joined = await Next(guest);
        Assert.Equal(MasterWire.Joined, joined.T);
        Assert.Equal(2, joined.Ice!.Count);
        var incoming = await Next(host);
        Assert.Equal(MasterWire.Incoming, incoming.T);
        Assert.Equal(joined.Peer, incoming.Peer);

        await Send(host, new MasterMessage { T = MasterWire.Signal, To = joined.Peer, Kind = MasterWire.Offer, Sdp = "v=0 offer" });
        var offer = await Next(guest);
        Assert.Equal(MasterHub.HostPeer, offer.From);
        Assert.Equal("v=0 offer", offer.Sdp);

        await Send(guest, new MasterMessage { T = MasterWire.Signal, To = MasterHub.HostPeer, Kind = MasterWire.Answer, Sdp = "v=0 answer" });
        var answer = await Next(host);
        Assert.Equal(joined.Peer, answer.From);

        await guest.CloseAsync(WebSocketCloseStatus.NormalClosure, "linked", CancellationToken.None);
        var left = await Next(host);
        Assert.Equal(MasterWire.Left, left.T);

        await host.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
        var hub = _app.Services.GetRequiredService<MasterHub>();
        await WaitUntil(() => Task.FromResult(hub.Count == 0));
    }

    [Fact]
    public async Task AnUnlistedGameIsLeftOutOfTheListAndJoinedByItsCode()
    {
        using var host = await Open();
        await Send(host, new MasterMessage
        {
            T = MasterWire.Host,
            Game = new MasterGame { Name = "Hidden", Kind = MasterWire.CoopKind, Players = 1, Cap = 4, Status = MasterWire.Waiting, Version = "0.2", Unlisted = true },
        });
        var hosted = await Next(host);
        Assert.Equal(MasterWire.Hosted, hosted.T);

        using var http = _server.CreateClient();
        string list = await http.GetStringAsync(MasterWire.GamesPath);
        Assert.Empty(MasterWire.TryReadList(list)!);
        Assert.DoesNotContain("Hidden", list, StringComparison.Ordinal);

        using var guest = await Open();
        await Send(guest, new MasterMessage { T = MasterWire.Join, Code = hosted.Code });
        Assert.Equal(MasterWire.Joined, (await Next(guest)).T);
        Assert.Equal(MasterWire.Incoming, (await Next(host)).T);
    }

    [Fact]
    public async Task AMessagePastTheCapClosesTheSocket()
    {
        using var socket = await Open();
        var big = new MasterMessage { T = MasterWire.Host, Game = new MasterGame { Name = new string('x', MasterWire.MaxMessageBytes) } };

        await socket.SendAsync(Encoding.UTF8.GetBytes(MasterWire.Write(big)), WebSocketMessageType.Text, true, CancellationToken.None);
        var buffer = new byte[1024];
        using var wait = new CancellationTokenSource(Patience);
        var result = await socket.ReceiveAsync(buffer, wait.Token);

        Assert.Equal(WebSocketMessageType.Close, result.MessageType);
        Assert.Equal(WebSocketCloseStatus.MessageTooBig, socket.CloseStatus);
    }

    [Fact]
    public async Task TextThatIsNotAMessageIsAnsweredWithAnError()
    {
        using var socket = await Open();

        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"nope\":1}"), WebSocketMessageType.Text, true, CancellationToken.None);

        Assert.Equal(MasterWire.Error, (await Next(socket)).T);
    }

    private static async Task Send(WebSocket socket, MasterMessage message) =>
        await socket.SendAsync(Encoding.UTF8.GetBytes(MasterWire.Write(message)), WebSocketMessageType.Text, true, CancellationToken.None);

    private static async Task<MasterMessage> Next(WebSocket socket)
    {
        var buffer = new byte[MasterWire.MaxMessageBytes];
        int length = 0;
        using var wait = new CancellationTokenSource(Patience);
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), wait.Token);
            length += result.Count;
        }
        while (!result.EndOfMessage);

        Assert.True(MasterWire.TryRead(Encoding.UTF8.GetString(buffer, 0, length), out var message));
        return message;
    }

    private static async Task WaitUntil(Func<Task<bool>> done)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (!await done())
        {
            Assert.True(DateTime.UtcNow < deadline, "the condition never held");
            await Task.Delay(20);
        }
    }

    private async Task<WebSocket> Open()
    {
        var client = _server.CreateWebSocketClient();
        return await client.ConnectAsync(new Uri(_server.BaseAddress, MasterWire.SocketPath.TrimStart('/')).ToWsUri(), CancellationToken.None);
    }
}

/// <summary>The in-memory host's base address as the WebSocket scheme its client expects.</summary>
internal static class UriExtensions
{
    public static Uri ToWsUri(this Uri uri) => new UriBuilder(uri) { Scheme = uri.Scheme == "https" ? "wss" : "ws" }.Uri;
}
