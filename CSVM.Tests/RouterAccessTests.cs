using System.Collections.Generic;
using System.Threading;
using CSVM.Net;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The router's two leases over stub calls: what a host asks for as it opens, and what it gives
/// back on close. Nothing here reaches a router.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class RouterAccessTests
{
    private const int Port = 47512;
    private const string Address = "2001:db8::24";

    [Fact]
    public void Close_gives_back_a_mapping_and_a_pinhole_no_poll_read()
    {
        var unmapped = new List<int>();
        var closed = new List<int>();
        using var answer = new ManualResetEventSlim();
        var router = new RouterAccess(
            port =>
            {
                answer.Wait();
                return new UpnpPortMapResult(UpnpPortMapOutcome.Mapped, port, "203.0.113.7", "mapped");
            },
            unmapped.Add,
            port => new UpnpPinholeResult(UpnpPinholeOutcome.Opened, port, Address, "opened"),
            closed.Add);

        router.Open(Port);
        answer.Set();
        router.Close();

        Assert.Equal(new[] { Port }, unmapped);
        Assert.Equal(new[] { Port }, closed);
    }

    // ABLE-TO-FAIL CONTROL for the one above: a lease the router refused is never given back.
    [Fact]
    public void Close_gives_back_nothing_the_router_refused()
    {
        var unmapped = new List<int>();
        var closed = new List<int>();
        var router = new RouterAccess(
            port => new UpnpPortMapResult(UpnpPortMapOutcome.Refused, port, "", "refused"),
            unmapped.Add,
            port => new UpnpPinholeResult(UpnpPinholeOutcome.Disallowed, port, Address, "not allowed"),
            closed.Add);

        router.Open(Port);
        router.Close();

        Assert.Empty(unmapped);
        Assert.Empty(closed);
    }

    [Fact]
    public void A_router_with_no_calls_asks_nothing_and_shows_nothing()
    {
        var router = new RouterAccess();
        router.Open(Port);
        router.Poll();
        Assert.Null(router.PortMap);
        Assert.Null(router.Pinhole);
        router.Close();
    }
}
