using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using CSVM.Net;

namespace CSVM.Testing;

/// <summary>The WebRTC carrier hosting and joining itself inside this one process. It negotiates
/// through <see cref="LoopbackMaster"/> with no STUN or TURN, so the link stands on this machine's
/// host candidates. Skipped without the webrtc-native extension, which <c>InstallWebRtc.ps1</c>
/// installs.</summary>
internal static class WebRtcTransportSuites
{
    // The give-up for a link or a payload. ICE over host candidates on one machine takes well
    // under a second; this is the ceiling, not the budget.
    private const double WaitSeconds = 10.0;

    [Suite("webrtc-transport",
        "the WebRTC carrier hosts and joins itself in one process through an in-process master: the "
        + "host lists and is given a code, a guest joining by it is numbered 2 and both ends are told "
        + "the other joined, the guest closes its master socket once linked, a reliable payload round "
        + "trips on its own channel, a sequenced burst is never delivered behind a newer payload, a "
        + "plain unreliable payload carries, a hang-up drops the guest's link with a reason, and a "
        + "code nobody listed fails the join with the master's reason")]
    internal static void HostAndJoinThroughALoopbackMaster(TestContext ctx)
    {
        if (!WebRtcTransport.Available)
        {
            throw new SuiteSkippedException(
                $"the webrtc-native extension is not loaded ({WebRtcTransport.ExtensionClass} is not a class); run InstallWebRtc.ps1");
        }

        var master = new LoopbackMaster();
        WebRtcTransport? host = null;
        WebRtcTransport? guest = null;
        try
        {
            host = WebRtcTransport.Host(master.Open, 3);
            host.List(new MasterGame { Name = "Suite", Kind = MasterWire.DogfightKind, Players = 1, Cap = 4, Status = MasterWire.Waiting });
            var atHost = new Recorder();
            host.Bind(atHost);
            Pump(host, null, () => host.JoinCode != null);
            ctx.Check(host.JoinCode != null, $"the host is given a code once it lists ({host.JoinCode ?? "none"}, {host.ListingFault})");
            if (host.JoinCode == null)
            {
                return;
            }

            guest = WebRtcTransport.Join(master.Open(), host.JoinCode, NetBuildVersion.Unknown);
            var atGuest = new Recorder();
            guest.Bind(atGuest);
            ctx.Check(guest.LinkState == NetLinkState.Connecting, $"a join opens connecting ({guest.LinkState})");
            double linked = Pump(host, guest, () => atHost.Connected.Count > 0 && atGuest.Connected.Count > 0);
            ctx.Check(atHost.Connected.SequenceEqual(new[] { 2 }) && guest.LocalPeer == 2,
                $"the host is told guest 2 joined, the id the guest goes by ({string.Join(",", atHost.Connected)}, guest {guest.LocalPeer}, {guest.Fault})");
            ctx.Check(atGuest.Connected.SequenceEqual(new[] { 1 }), $"the guest is told it reached the host ({string.Join(",", atGuest.Connected)})");
            ctx.Check(host.LinkState == NetLinkState.Up && guest.LinkState == NetLinkState.Up,
                $"both links read up ({host.LinkState}, {guest.LinkState})");
            ctx.Check(host.AddressOf(2) == "127.0.0.1", $"the host names the guest by the address the master gave ({host.AddressOf(2)})");
            if (atGuest.Connected.Count == 0)
            {
                return;
            }

            int before = master.Carried;
            Pump(host, guest, () => false, 0.2);
            ctx.Check(master.Carried == before, $"nothing more crosses the master once the link stands ({master.Carried - before} messages)");

            guest.Send(1, new byte[] { 1, 2, 3 }, NetReliability.Reliable, NetChannels.Events);
            Pump(host, guest, () => atHost.Payloads.Count > 0);
            ctx.Check(atHost.Payloads.Count == 1 && atHost.Payloads[0].Peer == 2 && atHost.Payloads[0].Channel == NetChannels.Events
                && atHost.Payloads[0].Bytes.SequenceEqual(new byte[] { 1, 2, 3 }),
                $"a reliable payload lands at the host with its sender, channel and bytes ({atHost.Payloads.Count})");

            int channel = NetChannels.ForSeat(1);
            for (int i = 0; i < 20; i++)
            {
                host.Send(2, new[] { (byte)i }, NetReliability.UnreliableSequenced, channel);
            }

            Pump(host, guest, () => atGuest.Payloads.Count >= 20, 1.0);
            var order = atGuest.Payloads.Where(p => p.Channel == channel).Select(p => (int)p.Bytes[0]).ToList();
            ctx.Check(order.Count > 0 && order.Zip(order.Skip(1), (a, b) => b > a).All(newer => newer),
                $"a sequenced burst arrives in rising order, none behind a newer one ({order.Count} of 20 arrived)");

            host.Send(2, new byte[] { 99 }, NetReliability.Unreliable, NetChannels.ForFire(1));
            Pump(host, guest, () => atGuest.Payloads.Any(p => p.Bytes[0] == 99), 2.0);
            ctx.Check(atGuest.Payloads.Any(p => p.Bytes[0] == 99 && p.Channel == NetChannels.ForFire(1)),
                $"a plain unreliable payload carries on its channel");

            host.Disconnect(2);
            Pump(host, guest, () => guest.LinkState == NetLinkState.Down && atHost.Disconnected.Count > 0);
            ctx.Check(atHost.Disconnected.SequenceEqual(new[] { 2 }), $"the host reports the guest it hung up on as gone ({string.Join(",", atHost.Disconnected)})");
            ctx.Check(guest.LinkState == NetLinkState.Down && guest.Fault.Length > 0,
                $"the guest's link goes down with a reason ({guest.LinkState}, '{guest.Fault}')");
            ctx.Note($"linked in {linked:0.000} s over host candidates, {master.Carried} master messages");
        }
        finally
        {
            guest?.Dispose();
            host?.Dispose();
        }

        using var lost = WebRtcTransport.Join(master.Open(), "ZZZ-ZZZ", NetBuildVersion.Unknown);
        Pump(lost, null, () => lost.LinkState == NetLinkState.Down);
        ctx.Check(lost.LinkState == NetLinkState.Down && lost.Fault.Contains("no game", StringComparison.Ordinal),
            $"a code nobody listed fails the join with the master's reason ('{lost.Fault}')");
    }

    [Suite("webrtc-left-before-link",
        "a host told a guest left before its own end reports the link keeps negotiating: the guest "
        + "still links and both ends are told, and a guest that never links after leaving is dropped "
        + "once the grace runs out")]
    internal static void ALeftBeforeTheLinkDoesNotCutIt(TestContext ctx)
    {
        if (!WebRtcTransport.Available)
        {
            throw new SuiteSkippedException(
                $"the webrtc-native extension is not loaded ({WebRtcTransport.ExtensionClass} is not a class); run InstallWebRtc.ps1");
        }

        var master = new LoopbackMaster();
        WebRtcTransport? host = null;
        WebRtcTransport? guest = null;
        WebRtcTransport? silent = null;
        try
        {
            host = WebRtcTransport.Host(master.Open, 3);
            host.List(new MasterGame { Name = "Suite", Kind = MasterWire.CoopKind, Players = 1, Cap = 4, Status = MasterWire.Waiting });
            var atHost = new Recorder();
            host.Bind(atHost);
            Pump(host, null, () => host.JoinCode != null);
            if (host.JoinCode == null)
            {
                ctx.Check(false, $"the host is given a code once it lists ({host.ListingFault})");
                return;
            }

            guest = WebRtcTransport.Join(master.Open(), host.JoinCode, NetBuildVersion.Unknown);
            var atGuest = new Recorder();
            guest.Bind(atGuest);
            master.TellHostLeft(2);
            Pump(host, guest, () => atHost.Connected.Count > 0 && atGuest.Connected.Count > 0);
            ctx.Check(atHost.Connected.SequenceEqual(new[] { 2 }) && atGuest.Connected.SequenceEqual(new[] { 1 }),
                $"a guest the host heard leave before the link still links ({string.Join(",", atHost.Connected)}; guest '{guest.Fault}')");
            ctx.Check(guest.LinkState == NetLinkState.Up, $"the guest's link reads up ({guest.LinkState}, '{guest.Fault}')");

            silent = WebRtcTransport.Join(master.Open(), host.JoinCode, NetBuildVersion.Unknown);
            host.Step(0.0);
            ctx.Check(host.Negotiating == 1, $"the host negotiates with the next guest ({host.Negotiating})");
            master.TellHostLeft(3);
            host.Step(0.0);
            host.Step(WebRtcTransport.LeftGraceSeconds - 1.0);
            ctx.Check(host.Negotiating == 1, $"inside the grace the host still holds it ({host.Negotiating})");
            host.Step(2.0);
            ctx.Check(host.Negotiating == 0, $"past the grace a guest that never linked is dropped ({host.Negotiating})");
            ctx.Check(host.Peers.SequenceEqual(new[] { 2 }), $"the linked guest is untouched ({string.Join(",", host.Peers)})");
        }
        finally
        {
            silent?.Dispose();
            guest?.Dispose();
            host?.Dispose();
        }
    }

    // Steps both ends on the wall clock until the condition holds or the wait runs out, and
    // answers how long it took.
    private static double Pump(INetTransport a, INetTransport? b, Func<bool> done, double wait = WaitSeconds)
    {
        var clock = Stopwatch.StartNew();
        double last = 0.0;
        while (!done() && clock.Elapsed.TotalSeconds < wait)
        {
            double now = clock.Elapsed.TotalSeconds;
            a.Step(now - last);
            b?.Step(now - last);
            last = now;
            Thread.Sleep(5);
        }

        return clock.Elapsed.TotalSeconds;
    }

    private sealed class Recorder : INetTransportListener
    {
        public List<int> Connected { get; } = new();

        public List<int> Disconnected { get; } = new();

        public List<(int Peer, int Channel, byte[] Bytes)> Payloads { get; } = new();

        public void OnPeerConnected(int peer) => Connected.Add(peer);

        public void OnPeerDisconnected(int peer) => Disconnected.Add(peer);

        public void OnPayload(int peer, int channel, ReadOnlySpan<byte> payload) => Payloads.Add((peer, channel, payload.ToArray()));
    }
}
