using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using CSVM.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CSVM.Master;

/// <summary>
/// The master server's composition: the games list, the health check and the socket, rate limited
/// per address, over one <see cref="MasterHub"/> and a sweep that expires silent games. Plain HTTP
/// on one port; TLS is the reverse proxy's (server/README.md).
/// </summary>
public static class MasterApp
{
    /// <summary>The rate limiter policy on the games list and the health check.</summary>
    public const string ListPolicy = "list";

    /// <summary>The rate limiter policy on opening a socket.</summary>
    public const string SocketPolicy = "socket";

    /// <summary>How often the sweep looks for silent games, in seconds.</summary>
    public const double SweepSeconds = 5.0;

    /// <summary>Builds the server from <paramref name="args"/> and the environment.
    /// <paramref name="configure"/> runs on the builder before the settings are read, where a suite
    /// swaps the host for an in-memory one, adds settings and registers its own clock.</summary>
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        var options = new MasterOptions();
        builder.Configuration.GetSection("Master").Bind(options);
        builder.Services.AddSingleton(options);
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(services => new MasterHub(
            services.GetRequiredService<MasterOptions>(), services.GetRequiredService<TimeProvider>()));
        builder.Services.AddHostedService<Sweeper>();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = MasterWire.MaxMessageBytes);
        builder.Services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(ListPolicy, context => PerAddress(context, options.ListPerMinute));
            limiter.AddPolicy(SocketPolicy, context => PerAddress(context, options.SocketsPerMinute));
        });

        var app = builder.Build();
        if (options.TrustProxy)
        {
            var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor, ForwardLimit = 1 };
            forwarded.KnownNetworks.Clear();
            forwarded.KnownProxies.Clear();
            app.UseForwardedHeaders(forwarded);
        }

        app.UseRateLimiter();
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
        var open = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

        app.MapGet(MasterWire.GamesPath, (MasterHub hub) =>
            Results.Content(MasterWire.Write(hub.List()), "application/json"))
            .RequireRateLimiting(ListPolicy);
        app.MapGet(MasterWire.HealthPath, (MasterHub hub) => Results.Json(
                new { ok = true, games = hub.Count, protocol = MasterWire.ProtocolVersion, oldest = MasterHub.OldestProtocol }))
            .RequireRateLimiting(ListPolicy);
        app.Map(MasterWire.SocketPath, async (HttpContext context, MasterHub hub, MasterOptions settings, TimeProvider time) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                return Results.BadRequest("a WebSocket request is expected here");
            }

            string address = AddressOf(context);
            if (open.AddOrUpdate(address, 1, (_, count) => count + 1) > settings.MaxSocketsPerAddress)
            {
                open.AddOrUpdate(address, 0, (_, count) => Math.Max(0, count - 1));
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            }

            var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
            await SocketClient.Run(socket, address, hub, settings, time, open, context.RequestAborted).ConfigureAwait(false);
            return Results.Empty;
        }).RequireRateLimiting(SocketPolicy);

        app.Logger.LogInformation(
            "csvm-master: stun {Stun}, turn {Turn} ({Secret}), proxy trusted {Proxy}",
            options.Stun.Length > 0 ? options.Stun : "none",
            options.Turn.Length > 0 ? options.Turn : "none",
            options.TurnSecret.Length > 0 ? "secret set" : "no secret, no TURN entries",
            options.TrustProxy);
        return app;
    }

    /// <summary>Runs the server until it is stopped.</summary>
    public static void Main(string[] args) => Build(args).Run();

    /// <summary>The address a request came from, without a port; <c>unknown</c> when the host
    /// names none, as an in-memory host does.</summary>
    public static string AddressOf(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        IPAddress? remote = context.Connection.RemoteIpAddress;
        if (remote == null)
        {
            return "unknown";
        }

        return (remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote).ToString();
    }

    private static RateLimitPartition<string> PerAddress(HttpContext context, int perMinute) =>
        RateLimitPartition.GetFixedWindowLimiter(AddressOf(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, perMinute),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });

    // The expiry's clock, apart from any request, so a game whose host vanished without a close
    // still leaves the list.
    private sealed class Sweeper : BackgroundService
    {
        private readonly MasterHub _hub;
        private readonly ILogger<Sweeper> _log;

        public Sweeper(MasterHub hub, ILogger<Sweeper> log)
        {
            _hub = hub;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(SweepSeconds));
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                int dropped = _hub.Sweep();
                if (dropped > 0)
                {
                    _log.LogInformation("csvm-master: dropped {Dropped} silent game(s), {Hosted} hosted", dropped, _hub.Count);
                }
            }
        }
    }
}
