using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Modes;
using CSVM.Net;
using CSVM.Session.World;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// A network stunt race off-engine. The tests cover the three race messages on the wire and the
/// host's intake of a guest's run reports. They cover the host's lines and clock reaching a guest's
/// replica, and a guest's opening catching up to the host's. Two sessions share a perfect loopback.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class NetStuntRaceTests
{
    private const float Dt = 1f / 60f;
    private const int Zones = 3;
    private const float Window = 10f;

    [Fact]
    public void TheThreeRaceMessagesRoundTripAtTheirDocumentedOffsets()
    {
        Assert.Equal(0x67, (int)NetMessageType.RaceRun);
        Assert.Equal(0x68, (int)NetMessageType.RaceState);
        Assert.Equal(0x69, (int)NetMessageType.RaceStanding);
        foreach (var type in new[] { NetMessageType.RaceRun, NetMessageType.RaceState, NetMessageType.RaceStanding })
        {
            Assert.Equal(NetReliability.Reliable, NetMessage.ReliabilityOf(type));
        }

        var bytes = new byte[RaceStandingMessage.Size];
        var run = new RaceRunMessage(3, NetRaceRun.Zone, 2, 1, 7, 12.5f);
        Assert.Equal(16, run.Write(bytes));
        Assert.Equal(new byte[] { 3, 2, 2, 1, 7, 0, 0, 0 }, bytes[4..12]);
        Assert.Equal(12.5f, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(12)));
        Assert.True(RaceRunMessage.TryRead(bytes.AsSpan(0, 16), out var runBack));
        Assert.Equal(run, runBack);
        Assert.False(RaceRunMessage.TryRead(bytes.AsSpan(0, 15), out _));

        var state = new RaceStateMessage(NetRacePhase.FinalRun, 4, 301.5f, 300f, 77.25f);
        Assert.Equal(20, state.Write(bytes));
        Assert.Equal(new byte[] { 2, 4, 0, 0 }, bytes[4..8]);
        Assert.Equal(301.5f, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(8)));
        Assert.Equal(300f, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(12)));
        Assert.Equal(77.25f, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(16)));
        Assert.True(RaceStateMessage.TryRead(bytes.AsSpan(0, 20), out var stateBack));
        Assert.Equal(state, stateBack);

        var line = new RaceStandingMessage(1, 2, inRun: true, completed: true, 5, 3, 41.25f, 12f, 3, 1,
            new[] { 10f, RaceStandingMessage.NoSplit, 41.25f });
        Assert.Equal(120, line.Write(bytes));
        Assert.Equal(new byte[] { 1, 3, 2, 3, 5, 0, 3, 0 }, bytes[4..12]);
        Assert.Equal(41.25f, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(12)));
        Assert.Equal(new byte[] { 3, 1 }, bytes[20..22]);
        Assert.Equal(RaceStandingMessage.NoSplit, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(28)));
        Assert.True(RaceStandingMessage.TryRead(bytes, out var lineBack));
        Assert.Equal(line, lineBack);
        Assert.False(RaceStandingMessage.TryRead(bytes.AsSpan(0, 119), out _));

        // ABLE-TO-FAIL CONTROL: a line past the widest course is cut, not overrun.
        var wide = new RaceStandingMessage(0, 0, false, false, 0, 0, 0f, 0f, 0, 0, new float[40]);
        Assert.Equal(RaceStandingMessage.MaxZones, wide.Splits.Count);
        Assert.Equal(120, wide.Write(bytes));
    }

    [Fact]
    public void AGuestsRunReachesTheHostsRaceAndComesBackToTheGuestsBoardWithItsSplits()
    {
        var f = Field();
        var bests = new List<int>();
        f.GuestRace.BestImproved += racer => bests.Add(racer.Index);
        f.Guest.Report(1, NetRaceRun.Started);
        f.Guest.Report(1, NetRaceRun.Zone, 0, 4f);
        f.Guest.Report(1, NetRaceRun.Zone, 2, 6f);
        f.Guest.Report(1, NetRaceRun.Zone, 1, 9f);
        f.Guest.Report(1, NetRaceRun.Finished, runTime: 9f);
        f.Pump();

        var hosted = f.HostRace.Of(1)!;
        Assert.Equal(9f, hosted.BestTime);
        Assert.Equal(new float?[] { 4f, 9f, 6f }, hosted.Splits);
        Assert.Equal(5, f.Host.ReportsTaken);

        f.Step();
        var replicated = f.GuestRace.Of(1)!;
        Assert.Equal(9f, replicated.BestTime);
        Assert.Equal(new float?[] { 4f, 9f, 6f }, replicated.Splits);
        Assert.Equal((1, 1, false), (replicated.RunsStarted, replicated.RunsFinished, replicated.InRun));
        Assert.Equal(new[] { 1, 0 }, f.GuestRace.Standings().Select(r => r.Index));
        Assert.Equal(f.HostRace.Standings().Select(r => r.Index), f.GuestRace.Standings().Select(r => r.Index));

        // The guest records its own best off the host's line, once.
        Assert.Equal(new[] { 1 }, bests);

        // ABLE-TO-FAIL CONTROL: the guest's own calls count nothing in its replica.
        Assert.False(f.GuestRace.RunStarted(0));
        f.GuestRace.RunFinished(1, 1f);
        Assert.Equal(9f, f.GuestRace.Of(1)!.BestTime);
    }

    [Fact]
    public void ARepeatedStartAndAReportOfASupersededRunCountNothing()
    {
        var f = Field();
        f.Guest.Report(1, NetRaceRun.Started);
        f.Pump();
        f.SendRaw(new RaceRunMessage(1, NetRaceRun.Started, RaceRunMessage.NoZone, 0, 1, 0f));
        Assert.Equal(1, f.HostRace.Of(1)!.RunsStarted);
        Assert.Equal(1, f.Host.ReportsRefused);

        f.Guest.Report(1, NetRaceRun.Abandoned);
        f.Guest.Report(1, NetRaceRun.Started);
        f.Pump();
        f.SendRaw(new RaceRunMessage(1, NetRaceRun.Zone, 0, 0, 1, 3f));
        f.SendRaw(new RaceRunMessage(1, NetRaceRun.Finished, RaceRunMessage.NoZone, 0, 1, 3f));
        var racer = f.HostRace.Of(1)!;
        Assert.Equal((2, 0, 0, true), (racer.RunsStarted, racer.CurrentZones, racer.RunsFinished, racer.InRun));
        Assert.Equal(3, f.Host.ReportsRefused);

        // ABLE-TO-FAIL CONTROL: the same zone under the run in progress counts.
        f.SendRaw(new RaceRunMessage(1, NetRaceRun.Zone, 0, 0, 2, 3f));
        Assert.Equal(1, racer.CurrentZones);
    }

    [Fact]
    public void AReportUnderAnotherWindowOrForASeatTheSenderDoesNotFlyCountsNothing()
    {
        var f = Field();
        f.SendRaw(new RaceRunMessage(0, NetRaceRun.Started, RaceRunMessage.NoZone, 0, 1, 0f));
        f.SendRaw(new RaceRunMessage(1, NetRaceRun.Started, RaceRunMessage.NoZone, 9, 1, 0f));
        Assert.False(f.HostRace.Of(0)!.InRun);
        Assert.False(f.HostRace.Of(1)!.InRun);
        Assert.Equal(2, f.Host.ReportsRefused);

        // ABLE-TO-FAIL CONTROL: the right seat under the right window counts.
        f.SendRaw(new RaceRunMessage(1, NetRaceRun.Started, RaceRunMessage.NoZone, 0, 1, 0f));
        Assert.True(f.HostRace.Of(1)!.InRun);
    }

    [Fact]
    public void AFinishAfterTimeUpCountsInsideTheCapAndARunStartAfterTimeUpDoesNot()
    {
        var f = Field();
        float? bestAtTheEnd = -1f;
        f.GuestRace.RaceCompleted += () => bestAtTheEnd = f.GuestRace.Of(1)!.BestTime;
        f.Guest.Report(1, NetRaceRun.Started);
        f.Pump();
        f.Step();
        Advance(f.HostRace, Window + 1f);
        f.Step();
        Assert.Equal(StuntRacePhase.FinalRun, f.HostRace.Phase);
        f.Guest.Report(1, NetRaceRun.Zone, 0, 8f);
        f.Guest.Report(1, NetRaceRun.Zone, 1, 9f);
        f.Guest.Report(1, NetRaceRun.Zone, 2, 10f);
        f.Guest.Report(1, NetRaceRun.Finished, runTime: 10.5f);
        f.Pump();
        Assert.Equal(10.5f, f.HostRace.Of(1)!.BestTime);
        Assert.True(f.HostRace.Ended);

        // The finish ended the host's race, and its line reached the guest ahead of the ending.
        Assert.True(f.GuestRace.Ended);
        Assert.Equal(10.5f, bestAtTheEnd);

        // A start that reaches the host in the final run is refused by its race, though the link took
        // it. The host's own pilot keeps the final run going.
        var late = Field();
        Assert.True(late.HostRace.RunStarted(0));
        Advance(late.HostRace, Window + 1f);
        Assert.Equal(StuntRacePhase.FinalRun, late.HostRace.Phase);
        late.Guest.Report(1, NetRaceRun.Started);
        late.Pump();
        Assert.Equal(1, late.Host.ReportsTaken);
        Assert.Equal((0, false), (late.HostRace.Of(1)!.RunsStarted, late.HostRace.Of(1)!.InRun));
    }

    [Fact]
    public void AFinishPastTheCapIsRefusedAndTheGuestEndsOnlyOnTheHostsWord()
    {
        var f = Field();
        f.Guest.Report(1, NetRaceRun.Started);
        f.Pump();
        f.Step();
        Advance(f.HostRace, Window + 1f);
        f.Step();
        Assert.Equal(StuntRacePhase.FinalRun, f.GuestRace.Phase);

        // The guest's own clock runs past the cap first, and its replica keeps waiting.
        Advance(f.GuestRace, Window + StuntRace.FinalRunCap + 5f);
        Assert.Equal(StuntRacePhase.FinalRun, f.GuestRace.Phase);

        Advance(f.HostRace, StuntRace.FinalRunCap);
        Assert.True(f.HostRace.Ended);
        Assert.False(f.GuestRace.Ended);
        f.Step();
        Assert.True(f.GuestRace.Ended);

        // The guest's finish past the cap reaches a race that has ended, and counts nowhere.
        f.Guest.Report(1, NetRaceRun.Finished, runTime: 130f);
        f.Pump();
        f.Step();
        Assert.Null(f.HostRace.Of(1)!.BestTime);
        Assert.Null(f.GuestRace.Of(1)!.BestTime);
        Assert.Equal(0, f.GuestRace.Of(1)!.RunsFinished);
    }

    [Fact]
    public void AGuestOpeningCatchesUpToTheHostsInWholeStepsNeverBackAndNeverPastItsGo()
    {
        var race = new StuntRace(Window, Zones);
        race.Add(0, "Bloodhawk");
        race.Replicate();
        race.BeginOpening(5f);
        for (int i = 0; i < 10; i++)
        {
            race.Advance(Dt);
        }

        // The host's reading is taken after its step and this one before, so one step is left over.
        Assert.Equal(9 * Dt, race.TakeHostClock(StuntRacePhase.Opening, (20 * Dt) + 0.004, Dt), 5);
        Assert.Equal(0f, race.TakeHostClock(StuntRacePhase.Opening, 5 * Dt, Dt));
        Assert.Equal(0f, race.TakeHostClock(StuntRacePhase.Opening, 20 * Dt, Dt));
        float last = race.TakeHostClock(StuntRacePhase.Open, 3.0, Dt);
        Assert.Equal(280 * Dt, last, 4);
        Assert.Equal(StuntRacePhase.Opening, race.Phase);
        race.Advance(Dt);
        Assert.Equal(StuntRacePhase.Open, race.Phase);

        // ABLE-TO-FAIL CONTROL: a host's race takes no clock at all.
        var host = new StuntRace(Window, Zones);
        host.BeginOpening(5f);
        Assert.Equal(0f, host.TakeHostClock(StuntRacePhase.Opening, 3.0, Dt));
    }

    [Fact]
    public void TheGuestReadsTheHostsClockForwardByItsLatencyAndItsCountsSkipTheSameSeconds()
    {
        double guestClock = 1.6;
        var f = Field(opening: 5f, guestClock: () => guestClock, slew: new NetClockSlew(0.5));
        var skipped = new List<float>();
        f.Guest.CatchUp = skipped.Add;
        for (int i = 0; i < 30; i++)
        {
            f.GuestRace.Advance(Dt);
        }

        // Host time is guest + 0.5, so a reading stamped 2.0 arrives 0.1 s late: 66.5 steps of
        // opening. Whole steps count, and this machine's next step takes one.
        f.HostSession.Broadcast(new RaceStateMessage(NetRacePhase.Opening, 0, 1.0f + (Dt / 2f), Window, 2.0f), NetChannels.Events);
        f.Pump();
        Assert.Single(skipped);
        Assert.Equal(35 * Dt, skipped[0], 4);
        Assert.Equal(65 * Dt, f.GuestRace.OpeningElapsed, 4);

        // ABLE-TO-FAIL CONTROL: a count caught up by the same seconds goes that many steps sooner.
        var count = new StartCount();
        count.Begin(StartCount.Opening(2f));
        int steps = 30;
        for (int i = 0; i < 30; i++)
        {
            count.Advance(Dt);
        }

        count.CatchUp(skipped[0]);
        while (count.Advance(Dt) != StartCountCue.Go)
        {
            steps++;
        }

        Assert.Equal(300 - 35, steps + 1);
    }

    [Fact]
    public void TheRaceCallAndALeftLineRoundTripAtTheirDocumentedOffsets()
    {
        Assert.Equal(0x6A, (int)NetMessageType.RaceCall);
        Assert.Equal(NetReliability.Reliable, NetMessage.ReliabilityOf(NetMessageType.RaceCall));
        var bytes = new byte[RaceStandingMessage.Size];
        var call = new RaceCallMessage(NetRaceCall.Lobby, 3);
        Assert.Equal(8, call.Write(bytes));
        Assert.Equal(new byte[] { 2, 3, 0, 0 }, bytes[4..8]);
        Assert.True(RaceCallMessage.TryRead(bytes.AsSpan(0, 8), out var back));
        Assert.Equal(call, back);
        Assert.False(RaceCallMessage.TryRead(bytes.AsSpan(0, 7), out _));
        Assert.Equal(new[] { 1, 2, 3 }, new[] { NetRaceCall.Restart, NetRaceCall.Lobby, NetRaceCall.Leave }.Select(c => (int)c));

        var line = new RaceStandingMessage(1, 0, inRun: false, completed: true, 2, 1, 9f, 9f, 3, 0, new[] { 3f, 6f, 9f }, left: true);
        Assert.Equal(120, line.Write(bytes));
        Assert.Equal(0x06, bytes[5]);
        Assert.True(RaceStandingMessage.TryRead(bytes, out var lineBack));
        Assert.True(lineBack.Left);
        Assert.Equal(line, lineBack);

        // ABLE-TO-FAIL CONTROL: a line of a pilot still racing carries no mark.
        new RaceStandingMessage(1, 0, false, true, 2, 1, 9f, 9f, 3, 0, new[] { 3f, 6f, 9f }).Write(bytes);
        Assert.Equal(0x02, bytes[5]);
    }

    [Fact]
    public void TheHostsRestartOpensTheGuestsNextWindowAheadOfItsLinesAndAnOldLineOrCallOpensNothing()
    {
        var f = Field();
        int restarts = 0;
        f.Guest.Restarted = () =>
        {
            restarts++;
            f.GuestRace.Restart();
            f.GuestRace.BeginOpening(0f);
        };
        f.Guest.Report(1, NetRaceRun.Started);
        f.Guest.Report(1, NetRaceRun.Finished, runTime: 9f);
        f.Pump();
        f.Step();
        Assert.Equal(9f, f.GuestRace.Of(1)!.BestTime);

        f.HostRace.Restart();
        f.HostRace.BeginOpening(0f);
        f.Host.CallRestart();
        f.Step();
        Assert.Equal((1, 1, 1), (restarts, f.HostRace.Round, f.GuestRace.Round));
        Assert.Null(f.GuestRace.Of(1)!.BestTime);

        // The old window's line, arriving now, is dropped; so is the same restart heard again.
        int lines = f.Guest.Lines;
        f.HostSession.Broadcast(new RaceStandingMessage(1, 0, false, true, 1, 1, 9f, 9f, 3, 0, new[] { 3f, 6f, 9f }), NetChannels.Events);
        f.HostSession.Broadcast(new RaceCallMessage(NetRaceCall.Restart, 1), NetChannels.Events);
        f.HostSession.Broadcast(new RaceCallMessage(NetRaceCall.Lobby, 0), NetChannels.Events);
        f.Pump();
        Assert.Equal((lines, 1, false), (f.Guest.Lines, restarts, f.Guest.LobbyCalled));
        Assert.Null(f.GuestRace.Of(1)!.BestTime);

        // ABLE-TO-FAIL CONTROL: the new window's own line is taken, and so is its runs' report.
        f.Guest.Report(1, NetRaceRun.Started);
        f.Pump();
        f.Step();
        Assert.Equal((1, true), (f.GuestRace.Of(1)!.RunsStarted, f.HostRace.Of(1)!.InRun));
    }

    [Fact]
    public void TheHostCallsTheLobbyOnlyFromAnEndedRaceAndAGuestsLeaveReachesTheHost()
    {
        var f = Field();
        var left = new List<int>();
        f.Host.GuestLeft += left.Add;
        f.Host.Leave(toLobby: true);
        f.Pump();
        Assert.False(f.Guest.LobbyCalled);

        Advance(f.HostRace, Window + 1f);
        f.Step();
        Assert.True(f.GuestRace.Ended);
        f.Host.Leave(toLobby: false);
        f.Pump();
        Assert.False(f.Guest.LobbyCalled);
        f.Host.Leave(toLobby: true);
        f.Pump();
        Assert.True(f.Guest.LobbyCalled);

        f.Guest.Leave(toLobby: true);
        f.Pump();
        Assert.Equal(new[] { f.Sessions[1].LocalPeer }, left);

        // ABLE-TO-FAIL CONTROL: the host's own leave reaches no guest as a leave.
        Assert.Equal(1, f.Host.CallsTaken);
    }

    [Fact]
    public void ASeatThatLeftKeepsItsBestMarkedOnTheGuestsBoardAndItsLaterReportsCountNothing()
    {
        var f = Field();
        f.Guest.Report(1, NetRaceRun.Started);
        f.Guest.Report(1, NetRaceRun.Finished, runTime: 7f);
        f.Pump();
        f.Step();

        f.Host.SeatLeft(1);
        f.Pump();
        var copy = f.GuestRace.Of(1)!;
        Assert.Equal((true, 7f), (copy.Left, copy.BestTime));
        Assert.Equal(new[] { 1, 0 }, f.GuestRace.Standings().Select(r => r.Index));

        f.Guest.Report(1, NetRaceRun.Started);
        f.Pump();
        Assert.Equal((1, false), (f.HostRace.Of(1)!.RunsStarted, f.HostRace.Of(1)!.InRun));

        // ABLE-TO-FAIL CONTROL: the host's own pilot, still in, starts a run.
        Assert.True(f.HostRace.RunStarted(0));
    }

    [Fact]
    public void ALateJoinerWaitsInTheLobbyAndNeverReachesTheRace()
    {
        var carrier = new RecordingCarrier(localPeer: 0);
        carrier.Connect(1);
        var lobby = new NetLobby(carrier);
        var roster = new[]
        {
            new NetSeat { PeerId = 0, SeatIndex = 0, IsLocal = true, Callsign = "Red" },
            new NetSeat { PeerId = 1, SeatIndex = 1, Callsign = "Blue" },
        };
        var host = NetSession.Host(lobby, roster, 0x5EEDUL);
        var race = new StuntRace(Window, Zones);
        race.Add(0, "Bloodhawk");
        race.Add(1, "Kestrel");
        var link = NetRaceLink.Open(host, race, () => 0.0, null, null, Dt);
        var left = new List<int>();
        link.GuestLeft += left.Add;
        race.BeginOpening(0f);

        carrier.Connect(7);
        int before = carrier.Sent.Count;
        link.Step();
        Assert.Contains(carrier.Sent.Skip(before), sent => sent.Peer == 1);
        Assert.DoesNotContain(carrier.Sent.Skip(before), sent => sent.Peer == 7);

        carrier.Deliver(7, Bytes(new RaceRunMessage(1, NetRaceRun.Started, RaceRunMessage.NoZone, 0, 1, 0f)));
        carrier.Deliver(7, Bytes(new RaceCallMessage(NetRaceCall.Leave, 0)));
        Assert.Equal((2, false, 0, 0), (race.Racers.Count, race.Of(1)!.InRun, link.ReportsRefused, left.Count));

        // ABLE-TO-FAIL CONTROL: the seated guest's same report counts.
        carrier.Deliver(1, Bytes(new RaceRunMessage(1, NetRaceRun.Started, RaceRunMessage.NoZone, 0, 1, 0f)));
        Assert.True(race.Of(1)!.InRun);
    }

    private static byte[] Bytes<T>(in T message)
        where T : struct, INetMessage<T>
    {
        var bytes = new byte[RaceStandingMessage.Size];
        int length = message.Write(bytes);
        return bytes.AsSpan(0, length).ToArray();
    }

    private static void Advance(StuntRace race, float seconds)
    {
        for (float t = 0f; t < seconds; t += Dt)
        {
            race.Advance(Dt);
        }
    }

    // Host on seat 0, the guest on seat 1, each machine's race holding both, the window open unless
    // an opening is asked for.
    private static RaceField Field(float opening = 0f, Func<double>? guestClock = null, NetClockSlew? slew = null)
    {
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(2267));
        var roster = new[]
        {
            new NetSeat { PeerId = 0, SeatIndex = 0, IsLocal = true, Callsign = "Red" },
            new NetSeat { PeerId = 1, SeatIndex = 1, Callsign = "Blue" },
        };
        var host = NetSession.Host(mesh[0], roster, 0x5EEDUL);
        var guest = NetSession.Guest(mesh[1]);
        var sessions = new[] { host, guest };
        Pump(sessions);
        Assert.True(guest.Joined);
        var races = new[] { new StuntRace(Window, Zones), new StuntRace(Window, Zones) };
        foreach (var race in races)
        {
            race.Add(0, "Bloodhawk").Callsign = "Red";
            race.Add(1, "Kestrel").Callsign = "Blue";
        }

        var hostLink = NetRaceLink.Open(host, races[0], () => 0.0, null, null, Dt);
        var guestLink = NetRaceLink.Open(guest, races[1], guestClock ?? (() => 0.0), slew, null, Dt);
        foreach (var race in races)
        {
            race.BeginOpening(opening);
        }

        return new RaceField(sessions, hostLink, guestLink, races[0], races[1]);
    }

    private static void Pump(NetSession[] sessions)
    {
        for (int i = 0; i < 4; i++)
        {
            foreach (var session in sessions)
            {
                session.Step(0.016);
            }
        }
    }

    // A carrier that records its sends and connects or delivers on demand.
    private sealed class RecordingCarrier : INetTransport
    {
        private readonly List<int> _peers = new();
        private INetTransportListener? _listener;

        public RecordingCarrier(int localPeer) => LocalPeer = localPeer;

        public List<(int Peer, byte[] Bytes)> Sent { get; } = new();

        public int LocalPeer { get; }

        public IReadOnlyList<int> Peers => _peers;

        public void Bind(INetTransportListener listener) => _listener = listener;

        public void Connect(int peer)
        {
            _peers.Add(peer);
            _listener?.OnPeerConnected(peer);
        }

        public void Deliver(int peer, byte[] payload) => _listener?.OnPayload(peer, NetChannels.Events, payload);

        public void Send(int peer, ReadOnlySpan<byte> payload, NetReliability reliability, int channel = 0) =>
            Sent.Add((peer, payload.ToArray()));

        public void Disconnect(int peer) => _peers.Remove(peer);

        public void Step(double dt)
        {
        }
    }

    private sealed record RaceField(NetSession[] Sessions, NetRaceLink Host, NetRaceLink Guest, StuntRace HostRace, StuntRace GuestRace)
    {
        public NetSession HostSession => Sessions[0];

        public void Pump() => NetStuntRaceTests.Pump(Sessions);

        // One host step's sends, delivered.
        public void Step()
        {
            Host.Step();
            Pump();
        }

        public void SendRaw(in RaceRunMessage report)
        {
            Sessions[1].Send(Sessions[1].HostPeer, report, NetChannels.Events);
            Pump();
        }
    }
}
