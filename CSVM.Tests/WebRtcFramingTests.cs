using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Net;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The WebRTC carrier's frame and its sequenced discard, over plain bytes. Also the merged host
/// that runs ENet and WebRTC guests as one roster, over two loopback meshes standing in for the
/// two carriers. Neither needs an engine.
/// </summary>
[Trait("Tier", "Quick")]
public class WebRtcFramingTests
{
    private static readonly LoopbackConditions Clean = new(0.0, 0.0, 0.0);

    [Fact]
    public void AFrameCarriesItsChannelAndPayload()
    {
        var sender = new WebRtcFraming();
        var receiver = new WebRtcFraming();

        byte[] frame = sender.Frame(2, 17, NetReliability.Reliable, new byte[] { 9, 8, 7 });

        Assert.Equal(WebRtcFraming.HeaderBytes + 3, frame.Length);
        Assert.True(receiver.TryOpen(1, frame, out int channel, out var payload));
        Assert.Equal(17, channel);
        Assert.Equal(new byte[] { 9, 8, 7 }, payload.ToArray());
    }

    [Fact]
    public void ASequencedPayloadNotNewerThanTheNewestIsDiscarded()
    {
        var sender = new WebRtcFraming();
        var receiver = new WebRtcFraming();
        var frames = Enumerable.Range(0, 3).Select(i => sender.Frame(5, 3, NetReliability.UnreliableSequenced, new[] { (byte)i })).ToList();

        Assert.True(receiver.TryOpen(1, frames[0], out _, out _));
        Assert.True(receiver.TryOpen(1, frames[2], out _, out _));
        Assert.False(receiver.TryOpen(1, frames[1], out _, out _));
        Assert.False(receiver.TryOpen(1, frames[2], out _, out _));
        Assert.Equal(2, receiver.DiscardedStale);
    }

    [Fact]
    public void SequencesAreKeptPerSenderAndPerChannel()
    {
        var a = new WebRtcFraming();
        var receiver = new WebRtcFraming();
        byte[] late = a.Frame(1, 3, NetReliability.UnreliableSequenced, new byte[] { 1 });
        byte[] newer = a.Frame(1, 3, NetReliability.UnreliableSequenced, new byte[] { 2 });
        byte[] otherChannel = a.Frame(1, 4, NetReliability.UnreliableSequenced, new byte[] { 3 });

        Assert.True(receiver.TryOpen(7, newer, out _, out _));
        Assert.True(receiver.TryOpen(7, otherChannel, out _, out _));
        Assert.True(receiver.TryOpen(8, late, out _, out _));
        Assert.False(receiver.TryOpen(7, late, out _, out _));
    }

    [Fact]
    public void AnUnsequencedPayloadIsNeverDiscardedAndAShortFrameIsDropped()
    {
        var sender = new WebRtcFraming();
        var receiver = new WebRtcFraming();
        byte[] first = sender.Frame(1, 0, NetReliability.Unreliable, new byte[] { 1 });

        Assert.True(receiver.TryOpen(2, first, out _, out _));
        Assert.True(receiver.TryOpen(2, first, out _, out _));
        Assert.False(receiver.TryOpen(2, new byte[] { 0, 0 }, out _, out _));
    }

    [Fact]
    public void TheSequenceWrapsWithoutReadingAsStale()
    {
        var sender = new WebRtcFraming();
        var receiver = new WebRtcFraming();
        byte[] last = Array.Empty<byte>();
        for (int i = 0; i <= ushort.MaxValue; i++)
        {
            last = sender.Frame(1, 2, NetReliability.UnreliableSequenced, ReadOnlySpan<byte>.Empty);
        }

        Assert.True(receiver.TryOpen(4, last, out _, out _));
        byte[] wrapped = sender.Frame(1, 2, NetReliability.UnreliableSequenced, ReadOnlySpan<byte>.Empty);
        Assert.Equal(0, BitConverter.ToUInt16(wrapped, 2));
        Assert.True(receiver.TryOpen(4, wrapped, out _, out _));
    }

    [Fact]
    public void AForgottenPeerStartsItsSequencesAfresh()
    {
        var sender = new WebRtcFraming();
        var receiver = new WebRtcFraming();
        receiver.TryOpen(3, sender.Frame(9, 1, NetReliability.UnreliableSequenced, new byte[] { 1 }), out _, out _);
        receiver.TryOpen(3, sender.Frame(9, 1, NetReliability.UnreliableSequenced, new byte[] { 2 }), out _, out _);

        receiver.Forget(3);
        sender.Forget(9);

        Assert.True(receiver.TryOpen(3, sender.Frame(9, 1, NetReliability.UnreliableSequenced, new byte[] { 3 }), out _, out _));
    }

    [Fact]
    public void AMergedHostNumbersEveryCarriersGuestsAsOneRoster()
    {
        var enet = LoopbackTransport.Mesh(2, Clean, new Random(3));
        var webRtc = LoopbackTransport.Mesh(3, Clean, new Random(4));
        var merged = new MergedTransport(enet[0], webRtc[0]);
        var heard = new Recorder();
        merged.Bind(heard);

        Assert.Equal(1, merged.LocalPeer);
        Assert.Equal(new[] { 2, 3, 4 }, merged.Peers);
        Assert.Equal(merged.Peers, heard.Joined);

        // A payload from the WebRTC side's second guest arrives under that guest's merged id.
        var guestHears = new Recorder();
        webRtc[2].Bind(guestHears);
        webRtc[2].Send(webRtc[0].LocalPeer, new byte[] { 42 }, NetReliability.Reliable, 5);
        webRtc[2].Step(0.0);
        merged.Step(0.0);
        Assert.Equal((4, 5, 42), heard.Payloads.Single());

        // And a send to that id reaches it alone.
        merged.Send(4, new byte[] { 7 }, NetReliability.Reliable, 1);
        merged.Step(0.0);
        webRtc[2].Step(0.0);
        Assert.Equal((webRtc[0].LocalPeer, 1, 7), guestHears.Payloads.Single());

        merged.Disconnect(2);
        merged.Step(0.0);
        Assert.Equal(new[] { 2 }, heard.Left);
        Assert.Equal(new[] { 3, 4 }, merged.Peers);
    }

    private sealed class Recorder : INetTransportListener
    {
        public List<int> Joined { get; } = new();

        public List<int> Left { get; } = new();

        public List<(int Peer, int Channel, byte First)> Payloads { get; } = new();

        public void OnPeerConnected(int peer) => Joined.Add(peer);

        public void OnPeerDisconnected(int peer) => Left.Add(peer);

        public void OnPayload(int peer, int channel, ReadOnlySpan<byte> payload) => Payloads.Add((peer, channel, payload[0]));
    }
}
