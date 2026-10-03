using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Net;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The link shaping a real carrier is flown under, over a perfect loopback pair. The wrapper's own
/// delays and losses are then all there is. One end is shaped, as a joining machine is,
/// and both of its directions are read: what it sends and what it is sent. The clock is a field
/// the test moves, standing in for the wall clock a launch gives it.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class ShapedTransportTests
{
    [Fact]
    public void Both_directions_wait_their_latency_before_anything_moves()
    {
        var (host, guest, clock) = Pair(new LoopbackConditions(0.1, 0.0, 0.0), new Random(1));
        var hostHeard = new Recorder();
        var guestHeard = new Recorder();
        host.Bind(hostHeard);
        guest.Bind(guestHeard);

        host.Send(1, Tag(1), NetReliability.Reliable);
        guest.Send(0, Tag(2), NetReliability.Reliable);
        clock.Now = 0.05;
        guest.Step(0.05);
        host.Step(0.05);
        Assert.Empty(guestHeard.Tags);
        Assert.Empty(hostHeard.Tags);

        // The host's payload reached the guest's carrier at 0.05 and waits from there.
        clock.Now = 0.11;
        guest.Step(0.06);
        host.Step(0.06);
        Assert.Equal(new[] { 2 }, hostHeard.Tags);
        Assert.Empty(guestHeard.Tags);

        clock.Now = 0.16;
        guest.Step(0.05);
        Assert.Equal(new[] { 1 }, guestHeard.Tags);
    }

    [Fact]
    public void Loss_takes_only_the_unreliable_classes_in_either_direction()
    {
        var (host, guest, clock) = Pair(new LoopbackConditions(0.0, 0.0, 1.0), new Random(2));
        var hostHeard = new Recorder();
        var guestHeard = new Recorder();
        host.Bind(hostHeard);
        guest.Bind(guestHeard);

        foreach (var (reliability, tag) in new[] { (NetReliability.Unreliable, 1), (NetReliability.UnreliableSequenced, 2), (NetReliability.Reliable, 3) })
        {
            host.Send(1, Tag(tag), reliability, 1);
            guest.Send(0, Tag(tag + 10), reliability, 1);
        }

        clock.Now = 1.0;
        guest.Step(1.0);
        host.Step(1.0);
        guest.Step(0.0);

        Assert.Equal(new[] { 3 }, guestHeard.Tags);
        Assert.Equal(new[] { 13 }, hostHeard.Tags);
        Assert.Equal(4, guest.Lost);
    }

    [Fact]
    public void A_reliable_stream_keeps_its_order_both_ways_under_wide_jitter_and_total_loss()
    {
        var (host, guest, clock) = Pair(new LoopbackConditions(0.05, 0.04, 1.0), new Random(3));
        var hostHeard = new Recorder();
        var guestHeard = new Recorder();
        host.Bind(hostHeard);
        guest.Bind(guestHeard);

        for (int tag = 1; tag <= 20; tag++)
        {
            host.Send(1, Tag(tag), NetReliability.Reliable);
            guest.Send(0, Tag(tag), NetReliability.Reliable);
            clock.Now += 0.002;
            guest.Step(0.002);
            host.Step(0.002);
        }

        clock.Now += 1.0;
        guest.Step(1.0);
        host.Step(1.0);

        Assert.Equal(Enumerable.Range(1, 20).ToArray(), guestHeard.Tags);
        Assert.Equal(Enumerable.Range(1, 20).ToArray(), hostHeard.Tags);
    }

    [Theory]
    [InlineData(NetReliability.UnreliableSequenced)]
    [InlineData(NetReliability.Unreliable)]
    public void Jitter_reorders_and_only_a_sequenced_payload_is_discarded_for_it(NetReliability reliability)
    {
        var (host, guest, clock) = Pair(new LoopbackConditions(0.05, 0.05, 0.0), new Random(4));
        var hostHeard = new Recorder();
        var guestHeard = new Recorder();
        host.Bind(hostHeard);
        guest.Bind(guestHeard);

        for (int tag = 1; tag <= 40; tag++)
        {
            host.Send(1, Tag(tag), reliability, 2);
            guest.Send(0, Tag(tag), reliability, 2);
            clock.Now += 0.005;
            guest.Step(0.005);
            host.Step(0.005);
        }

        clock.Now += 1.0;
        guest.Step(1.0);
        host.Step(1.0);

        foreach (var heard in new[] { guestHeard.Tags, hostHeard.Tags })
        {
            if (reliability == NetReliability.UnreliableSequenced)
            {
                Assert.True(heard.Zip(heard.Skip(1), (a, b) => b > a).All(rising => rising), string.Join(",", heard));
                Assert.InRange(heard.Length, 1, 39);
            }
            else
            {
                Assert.Equal(40, heard.Length);
                Assert.NotEqual(heard.OrderBy(tag => tag).ToArray(), heard);
            }
        }

        Assert.Equal(reliability == NetReliability.UnreliableSequenced, guest.DiscardedStale > 0);
    }

    [Fact]
    public void Perfect_conditions_deliver_what_the_bare_carrier_delivers()
    {
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(0));
        var (host, guest, _) = Pair(LoopbackConditions.Perfect, new Random(5));
        var bare = Exchange(mesh[0], mesh[1]);
        var shaped = Exchange(host, guest);

        Assert.Equal(bare.Guest.Payloads, shaped.Guest.Payloads);
        Assert.Equal(bare.Host.Payloads, shaped.Host.Payloads);
        Assert.Equal(6, shaped.Guest.Payloads.Count);
        Assert.Equal(0, guest.Lost + guest.DiscardedStale);

        static (Recorder Host, Recorder Guest) Exchange(INetTransport host, INetTransport guest)
        {
            var hostHeard = new Recorder();
            var guestHeard = new Recorder();
            host.Bind(hostHeard);
            guest.Bind(guestHeard);
            for (int tag = 1; tag <= 6; tag++)
            {
                var reliability = (NetReliability)(tag % 3);
                host.Send(1, Tag(tag), reliability, tag % 2);
                guest.Send(0, Tag(tag + 10), reliability, tag % 2);
                guest.Step(0.0);
                host.Step(0.0);
            }

            return (hostHeard, guestHeard);
        }
    }

    [Fact]
    public void A_hang_up_with_only_unreliable_payloads_waiting_forgets_them_and_reaches_the_session_at_once()
    {
        var (host, guest, clock) = Pair(new LoopbackConditions(0.2, 0.0, 0.0), new Random(6));
        var guestHeard = new Recorder();
        host.Bind(new Recorder());
        guest.Bind(guestHeard);
        Assert.Equal(new[] { 0 }, guestHeard.Connected);

        host.Send(1, Tag(1), NetReliability.Unreliable);
        host.Send(1, Tag(2), NetReliability.UnreliableSequenced, 1);
        guest.Step(0.0);
        host.Disconnect(1);
        Assert.Equal(new[] { 0 }, guestHeard.Disconnected);

        clock.Now = 1.0;
        guest.Step(1.0);
        Assert.Empty(guestHeard.Tags);
        Assert.Empty(guest.Peers);
    }

    [Fact]
    public void A_hang_up_waits_behind_the_reliable_payloads_the_carrier_already_delivered()
    {
        var (host, guest, clock) = Pair(new LoopbackConditions(0.2, 0.0, 0.0), new Random(8));
        var guestHeard = new Recorder();
        host.Bind(new Recorder());
        guest.Bind(guestHeard);

        host.Send(1, Tag(1), NetReliability.Reliable);
        host.Send(1, Tag(2), NetReliability.Unreliable);
        host.Send(1, Tag(3), NetReliability.Reliable);
        guest.Step(0.0);
        host.Disconnect(1);
        clock.Now = 0.1;
        guest.Step(0.1);
        Assert.Equal(new[] { "joined 0" }, guestHeard.Events);

        clock.Now = 0.25;
        guest.Step(0.15);
        Assert.Equal(new[] { "joined 0", "payload 1", "payload 3", "left 0" }, guestHeard.Events);
    }

    [Fact]
    public void Closing_hands_the_waiting_reliable_sends_to_the_carrier_and_forgets_the_rest()
    {
        var (host, guest, _) = Pair(new LoopbackConditions(0.2, 0.0, 0.0), new Random(9));
        var hostHeard = new Recorder();
        host.Bind(hostHeard);
        guest.Bind(new Recorder());

        guest.Send(0, Tag(1), NetReliability.Reliable);
        guest.Send(0, Tag(2), NetReliability.Unreliable);
        guest.Send(0, Tag(3), NetReliability.UnreliableSequenced, 1);
        guest.Send(0, Tag(4), NetReliability.Reliable);
        guest.Dispose();
        host.Step(0.0);

        Assert.Equal(new[] { 1, 4 }, hostHeard.Tags);
    }

    [Fact]
    public void A_carrier_that_names_no_class_has_its_arrivals_carried_as_reliable()
    {
        var clock = new Clock();
        var inner = new Scripted();
        var shaped = new ShapedTransport(inner, new LoopbackConditions(0.0, 0.0, 1.0), new Random(7), () => clock.Now);
        var heard = new Recorder();
        shaped.Bind(heard);

        inner.ArriveUnclassed(0, Tag(9));
        shaped.Step(0.0);

        Assert.Equal(new[] { 9 }, heard.Tags);
        Assert.Equal(0, shaped.Lost);
    }

    [Fact]
    public void Hanging_up_hands_the_peer_its_waiting_reliable_sends_first()
    {
        var clock = new Clock();
        var inner = new Scripted();
        var shaped = new ShapedTransport(inner, new LoopbackConditions(0.2, 0.0, 0.0), new Random(10), () => clock.Now);
        shaped.Bind(new Recorder());
        inner.Join(0);

        shaped.Send(0, Tag(1), NetReliability.Reliable);
        shaped.Send(0, Tag(2), NetReliability.Unreliable);
        shaped.Send(0, Tag(3), NetReliability.Reliable);
        shaped.Disconnect(0);
        clock.Now = 1.0;
        shaped.Step(1.0);

        Assert.Equal(new[] { "send 1", "send 3", "hang up 0" }, inner.Calls);
    }

    [Fact]
    public void An_id_back_while_its_old_departure_is_held_settles_the_old_peer_first()
    {
        var clock = new Clock();
        var inner = new Scripted();
        var shaped = new ShapedTransport(inner, new LoopbackConditions(0.2, 0.0, 0.0), new Random(11), () => clock.Now);
        var heard = new Recorder();
        shaped.Bind(heard);
        inner.Join(0);

        inner.Arrive(0, Tag(1), NetReliability.Reliable);
        inner.Leave(0);
        inner.Join(0);
        inner.Arrive(0, Tag(2), NetReliability.Reliable);
        clock.Now = 1.0;
        shaped.Step(1.0);

        Assert.Equal(new[] { "joined 0", "payload 1", "left 0", "joined 0", "payload 2" }, heard.Events);
    }

    [Fact]
    public void One_seed_replays_the_same_shaped_link()
    {
        Assert.Equal(Run(new Random(11)), Run(new Random(11)));

        static int[] Run(Random rng)
        {
            var (host, guest, clock) = Pair(new LoopbackConditions(0.05, 0.03, 0.35), rng);
            var guestHeard = new Recorder();
            guest.Bind(guestHeard);
            for (int tag = 1; tag <= 40; tag++)
            {
                host.Send(1, Tag(tag), NetReliability.Unreliable);
                clock.Now += 0.01;
                guest.Step(0.01);
            }

            clock.Now += 1.0;
            guest.Step(1.0);
            return guestHeard.Tags;
        }
    }

    [Theory]
    [InlineData("50,10,5", 0.05, 0.01, 0.05)]
    [InlineData(" 100 , 20 , 10 ", 0.10, 0.02, 0.10)]
    [InlineData("0,0,0", 0.0, 0.0, 0.0)]
    [InlineData("12.5,0,100", 0.0125, 0.0, 1.0)]
    [InlineData("soak50", 0.05, 0.01, 0.05)]
    [InlineData("soak100", 0.10, 0.02, 0.10)]
    [InlineData("SOAK200", 0.20, 0.04, 0.20)]
    public void A_shape_reads_milliseconds_and_per_cent_or_a_soak_cell(string text, double latency, double jitter, double loss)
    {
        Assert.True(ShapedTransport.TryParse(text, out var conditions));
        Assert.Equal(latency, conditions.Latency, 9);
        Assert.Equal(jitter, conditions.Jitter, 9);
        Assert.Equal(loss, conditions.Loss, 9);
    }

    [Theory]
    [InlineData("")]
    [InlineData("50,10")]
    [InlineData("50,10,5,1")]
    [InlineData("-1,0,0")]
    [InlineData("50,-1,0")]
    [InlineData("50,10,101")]
    [InlineData("fifty,10,5")]
    [InlineData("50,10,NaN")]
    [InlineData("50,10,Infinity")]
    [InlineData("soak75")]
    public void An_unreadable_shape_is_refused(string text)
    {
        Assert.False(ShapedTransport.TryParse(text, out _));
    }

    [Fact]
    public void A_shape_describes_itself_in_the_units_it_was_given_in()
    {
        Assert.Equal("latency 50 ms, jitter 10 ms, loss 5 %", ShapedTransport.Describe(new LoopbackConditions(0.05, 0.01, 0.05)));
    }

    private static (LoopbackTransport Host, ShapedTransport Guest, Clock Clock) Pair(LoopbackConditions conditions, Random rng)
    {
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(0));
        var clock = new Clock();
        return (mesh[0], new ShapedTransport(mesh[1], conditions, rng, () => clock.Now), clock);
    }

    private static byte[] Tag(int tag) => new[] { (byte)tag };

    private sealed class Clock
    {
        public double Now { get; set; }
    }

    // A carrier the test drives by hand: it raises joins, departures and arrivals when told to, and
    // records what the wrapper hands it.
    private sealed class Scripted : INetTransport
    {
        private readonly List<int> _peers = new();
        private INetTransportListener? _listener;

        public int LocalPeer => 1;

        public IReadOnlyList<int> Peers => _peers;

        public List<string> Calls { get; } = new();

        public void Bind(INetTransportListener listener) => _listener = listener;

        public void Send(int peer, ReadOnlySpan<byte> payload, NetReliability reliability, int channel = 0) =>
            Calls.Add($"send {payload[0]}");

        public void Disconnect(int peer) => Calls.Add($"hang up {peer}");

        public void Step(double dt)
        {
        }

        public void Join(int peer)
        {
            _peers.Add(peer);
            _listener!.OnPeerConnected(peer);
        }

        public void Leave(int peer)
        {
            _peers.Remove(peer);
            _listener!.OnPeerDisconnected(peer);
        }

        public void Arrive(int peer, byte[] payload, NetReliability reliability) =>
            ((INetClassedListener)_listener!).OnPayload(peer, 0, reliability, payload);

        public void ArriveUnclassed(int peer, byte[] payload) => _listener!.OnPayload(peer, 1, payload);
    }

    private sealed class Recorder : INetTransportListener
    {
        public List<int> Connected { get; } = new();

        public List<int> Disconnected { get; } = new();

        public List<(int Peer, int Channel, int Tag)> Payloads { get; } = new();

        public List<string> Events { get; } = new();

        public int[] Tags => Payloads.Select(payload => payload.Tag).ToArray();

        public void OnPeerConnected(int peer)
        {
            Connected.Add(peer);
            Events.Add($"joined {peer}");
        }

        public void OnPeerDisconnected(int peer)
        {
            Disconnected.Add(peer);
            Events.Add($"left {peer}");
        }

        public void OnPayload(int peer, int channel, ReadOnlySpan<byte> payload)
        {
            Payloads.Add((peer, channel, payload[0]));
            Events.Add($"payload {payload[0]}");
        }
    }
}
