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

        var bytes = new byte[RaceStandingMessage.MaxSize];
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
        Assert.Equal(36, line.Write(bytes));
        Assert.Equal(new byte[] { 1, 3, 2, 3, 5, 0, 3, 0 }, bytes[4..12]);
        Assert.Equal(41.25f, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(12)));
        Assert.Equal(new byte[] { 3, 1 }, bytes[20..22]);
        Assert.Equal(RaceStandingMessage.NoSplit, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(28)));
        Assert.True(RaceStandingMessage.TryRead(bytes.AsSpan(0, 36), out var lineBack));
        Assert.Equal(line, lineBack);
        Assert.False(RaceStandingMessage.TryRead(bytes.AsSpan(0, 35), out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(24)]
    [InlineData(25)]
    [InlineData(255)]
    public void ARacerLineCarriesExactlyAsManySplitsAsItsCount(int count)
    {
        var splits = Enumerable.Range(0, count).Select(i => i % 7 == 3 ? RaceStandingMessage.NoSplit : 1.5f * (i + 1)).ToArray();
        var line = new RaceStandingMessage(2, 1, inRun: false, completed: true, 4, 2, 99.5f, 99.5f, (byte)count, 0, splits);
        var bytes = new byte[RaceStandingMessage.MaxSize + 8];

        int length = line.Write(bytes);

        Assert.Equal(24 + (4 * count), length);
        Assert.Equal(length, line.Size);
        Assert.Equal(count, bytes[7]);
        Assert.True(RaceStandingMessage.TryRead(bytes.AsSpan(0, length), out var back));
        Assert.Equal(line, back);
        Assert.Equal(splits, back.Splits);
        Assert.True(RaceStandingMessage.MaxSize <= NetSession.SendBufferBytes);

        // ABLE-TO-FAIL CONTROL: a count that names one split more, or one fewer, than the line holds
        // is refused, whichever way the header's length is told.
        bytes[7] = (byte)(count + 1);
        Assert.False(RaceStandingMessage.TryRead(bytes.AsSpan(0, length), out _));
        if (count > 0)
        {
            bytes[7] = (byte)(count - 1);
            Assert.False(RaceStandingMessage.TryRead(bytes.AsSpan(0, length), out _));
        }

        bytes[7] = (byte)count;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), (ushort)(length + 4));
        Assert.False(RaceStandingMessage.TryRead(bytes.AsSpan(0, length + 4), out _));
        Assert.False(RaceStandingMessage.TryRead(bytes.AsSpan(0, RaceStandingMessage.FixedSize - 1), out _));
    }

    [Fact]
    public void ARacerLinePastTheZoneBoundIsCutNotOverrun()
    {
        var wide = new RaceStandingMessage(0, 0, false, false, 0, 0, 0f, 0f, 0, 0, new float[300]);
        Assert.Equal(RaceStandingMessage.MaxZones, wide.Splits.Count);
        Assert.Equal(RaceStandingMessage.MaxSize, wide.Write(new byte[RaceStandingMessage.MaxSize]));
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
    public void AGuestsRunLostInTheFinalRunStopsOnTheHostWhichEndsTheRaceOnBothMachines()
    {
        var f = Field();
        var run = StuntRaceTests.FakeMission(Zones);
        f.Guest.Feed(1, run);
        run.Tick(Dt);
        f.Pump();
        f.Step();
        Assert.True(f.HostRace.Of(1)!.InRun);

        Advance(f.HostRace, Window + 1f);
        Advance(f.GuestRace, Window + 1f);
        f.Step();
        Assert.Equal((StuntRacePhase.FinalRun, false), (f.GuestRace.Phase, f.GuestRace.MayRespawn));

        // ABLE-TO-FAIL CONTROL: the host waits on the guest's run until it is lost.
        Assert.False(f.HostRace.Ended);
        run.Stop();
        f.Pump();
        Assert.Equal((false, true), (f.HostRace.Of(1)!.InRun, f.HostRace.Ended));
        f.Step();
        Assert.True(f.GuestRace.Ended);
        Assert.DoesNotContain(f.GuestRace.Racers, r => r.InRun);
    }

    [Fact]
    public void AGuestsHoldBeforeTimeUpGrantsItARunAfterItOnBothBoards()
    {
        var f = Field();
        var run = StuntRaceTests.FakeMission(Zones);
        f.Guest.Feed(1, run);
        run.Tick(Dt);
        run.Reset();
        f.Pump();

        // The hold reaches this build's host as the abandon and the approach, both taken.
        Assert.Equal((3, 0), (f.Host.ReportsTaken, f.Host.ReportsRefused));
        Advance(f.HostRace, Window + 1f);
        Advance(f.GuestRace, Window + 1f);
        f.Step();
        var hosted = f.HostRace.Of(1)!;
        Assert.Equal((StuntRacePhase.FinalRun, true, true, 1), (f.HostRace.Phase, hosted.InRun, hosted.OnApproach, hosted.RunsStarted));
        Assert.True(f.GuestRace.Of(1)!.InRun);

        // The run starts after time up and counts on both boards.
        run.Tick(Dt);
        for (int zone = 0; zone < Zones; zone++)
        {
            f.Guest.Report(1, NetRaceRun.Zone, zone, 1f + zone);
        }

        f.Guest.Report(1, NetRaceRun.Finished, runTime: Zones);
        f.Pump();
        Assert.Equal((2, 1, (float?)Zones), (hosted.RunsStarted, hosted.RunsFinished, hosted.BestTime));
        Assert.True(f.HostRace.Ended);
        f.Step();
        Assert.Equal(((float?)Zones, 2), (f.GuestRace.Of(1)!.BestTime, f.GuestRace.Of(1)!.RunsStarted));
        Assert.True(f.GuestRace.Ended);
    }

    [Fact]
    public void AGuestsCrashOnItsApproachEndsTheRaceAtOnceAndAHoldReachingTheHostLateIsAnAbandon()
    {
        var f = Field();
        var run = StuntRaceTests.FakeMission(Zones);
        f.Guest.Feed(1, run);
        run.Tick(Dt);
        run.Reset();
        f.Pump();
        Advance(f.HostRace, Window + 1f);
        Advance(f.GuestRace, Window + 1f);
        f.Step();

        // ABLE-TO-FAIL CONTROL: the host waits on the guest's approach.
        Assert.Equal((false, true), (f.HostRace.Ended, f.HostRace.Of(1)!.OnApproach));
        run.Stop();
        f.Pump();
        Assert.Equal((false, 1, true), (f.HostRace.Of(1)!.InRun, f.HostRace.Of(1)!.RunsStarted, f.HostRace.Ended));
        f.Step();
        Assert.True(f.GuestRace.Ended);

        // A hold sent before the guest's own time up that reaches the host after its own.
        var late = Field();
        var lateRun = StuntRaceTests.FakeMission(Zones);
        late.Guest.Feed(1, lateRun);
        lateRun.Tick(Dt);
        late.Pump();
        Advance(late.HostRace, Window + 1f);
        Assert.Equal((StuntRacePhase.FinalRun, true), (late.HostRace.Phase, late.HostRace.Of(1)!.InRun));
        lateRun.Reset();
        late.Pump();
        Assert.Equal((false, false, true), (late.HostRace.Of(1)!.InRun, late.HostRace.Of(1)!.OnApproach, late.HostRace.Ended));
        Assert.Equal((3, 0), (late.Host.ReportsTaken, late.Host.ReportsRefused));
    }

    [Fact]
    public void AHostThatKnowsNoApproachTakesAHoldAsAPlainAbandonAndEndsTheRaceAtTimeUp()
    {
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(2267));
        var roster = new[]
        {
            new NetSeat { PeerId = 0, SeatIndex = 0, FlownHere = true, Callsign = "Red" },
            new NetSeat { PeerId = 1, SeatIndex = 1, Callsign = "Blue" },
        };
        var sessions = new[] { NetSession.Host(mesh[0], roster, 0x5EEDUL), NetSession.Guest(mesh[1]) };
        Pump(sessions);
        var races = new[] { new StuntRace(Window, Zones), new StuntRace(Window, Zones) };
        foreach (var race in races)
        {
            race.Add(0, "Bloodhawk");
            race.Add(1, "Kestrel");
        }

        // An intake that knows no approach. A start must carry a newer run and any other report must name
        // the newest; a kind it does not know counts nothing.
        var hostRace = races[0];
        var heard = new List<NetRaceRun>();
        int newest = 0;
        int refused = 0;
        sessions[0].On<RaceRunMessage>((_, report) =>
        {
            heard.Add(report.Kind);
            if (!(report.Kind == NetRaceRun.Started ? report.Run > newest : newest != 0 && report.Run == newest))
            {
                refused++;
                return;
            }

            switch (report.Kind)
            {
                case NetRaceRun.Started:
                    newest = report.Run;
                    hostRace.RunStarted(report.Seat);
                    break;
                case NetRaceRun.Abandoned:
                    hostRace.RunAbandoned(report.Seat);
                    break;
                default:
                    refused++;
                    break;
            }
        });
        var guestLink = NetRaceLink.Open(sessions[1], races[1], () => 0.0, null, null, Dt);
        foreach (var race in races)
        {
            race.BeginOpening(0f);
        }

        var run = StuntRaceTests.FakeMission(Zones);
        guestLink.Feed(1, run);
        run.Tick(Dt);
        run.Reset();
        Pump(sessions);
        Assert.Equal(new[] { NetRaceRun.Started, NetRaceRun.Abandoned, NetRaceRun.Approach }, heard);
        Assert.Equal((1, false), (refused, hostRace.Of(1)!.InRun));

        // On that host nobody is in a run at time up and the race ends. A crash on
        // the approach names a run that host never took.
        Advance(hostRace, Window + 1f);
        Assert.True(hostRace.Ended);
        run.Stop();
        Pump(sessions);
        Assert.Equal((NetRaceRun.Abandoned, 2), (heard[^1], refused));
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
        var bytes = new byte[RaceStandingMessage.MaxSize];
        var call = new RaceCallMessage(NetRaceCall.Lobby, 3);
        Assert.Equal(8, call.Write(bytes));
        Assert.Equal(new byte[] { 2, 3, 0, 0 }, bytes[4..8]);
        Assert.True(RaceCallMessage.TryRead(bytes.AsSpan(0, 8), out var back));
        Assert.Equal(call, back);
        Assert.False(RaceCallMessage.TryRead(bytes.AsSpan(0, 7), out _));
        Assert.Equal(new[] { 1, 2, 3 }, new[] { NetRaceCall.Rerun, NetRaceCall.Lobby, NetRaceCall.Leave }.Select(c => (int)c));

        var line = new RaceStandingMessage(1, 0, inRun: false, completed: true, 2, 1, 9f, 9f, 3, 0, new[] { 3f, 6f, 9f }, left: true);
        Assert.Equal(36, line.Write(bytes));
        Assert.Equal(0x06, bytes[5]);
        Assert.True(RaceStandingMessage.TryRead(bytes.AsSpan(0, 36), out var lineBack));
        Assert.True(lineBack.Left);
        Assert.Equal(line, lineBack);

        // ABLE-TO-FAIL CONTROL: a line of a pilot still racing carries no mark.
        new RaceStandingMessage(1, 0, false, true, 2, 1, 9f, 9f, 3, 0, new[] { 3f, 6f, 9f }).Write(bytes);
        Assert.Equal(0x02, bytes[5]);
    }

    [Fact]
    public void TheRaceTouchRoundTripsAtItsDocumentedOffsets()
    {
        Assert.Equal(0x6C, (int)NetMessageType.RaceTouch);
        Assert.Equal(NetReliability.Reliable, NetMessage.ReliabilityOf(NetMessageType.RaceTouch));
        var bytes = new byte[RaceTouchMessage.Size];
        var touch =new RaceTouchMessage(2, 3, 0x0105, RaceTouchMessage.RightPylon, 12.5f, 77.25f);
        Assert.Equal(20, touch.Write(bytes));
        Assert.Equal(new byte[] { 2, 3, 0x05, 0x01, 1, 0, 0, 0 }, bytes[4..12]);
        Assert.Equal(12.5f, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(12)));
        Assert.Equal(77.25f, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(16)));
        Assert.True(RaceTouchMessage.TryRead(bytes.AsSpan(0, 20), out var back));
        Assert.Equal(touch, back);

        // ABLE-TO-FAIL CONTROL: a short buffer reads nothing.
        Assert.False(RaceTouchMessage.TryRead(bytes.AsSpan(0, 19), out _));
    }

    [Fact]
    public void EverySeatsCoursePylonTouchIsShownOnEveryOtherMachineAndScoredNowhere()
    {
        var f = TouchField();
        var shown = new List<(int Machine, int Gate, CourseElement Element, float Height, double At)>[3];
        for (int m = 0; m < 3; m++)
        {
            int machine = m;
            shown[m] = new();
            f.Links[m].TouchShown = (gate, element, height, at) => shown[machine].Add((machine, gate, element, height, at));
        }

        // Guest A's seat 1 touches: the host and guest B draw it, A itself gets nothing back.
        f.Links[1].Touched(1, Touch(2, CourseElement.RightCoursePylon, 9f), 10.0);
        f.Pump();
        Assert.Equal(new[] { (0, 2, CourseElement.RightCoursePylon, 9f, 10.0) }, shown[0]);
        Assert.Equal(new[] { (2, 2, CourseElement.RightCoursePylon, 9f, 10.0) }, shown[2]);
        Assert.Empty(shown[1]);

        // The host's own seat 0 touches: both guests draw it.
        f.Links[0].Touched(0, Touch(0, CourseElement.LeftCoursePylon, 4f), 11.0);
        f.Pump();
        Assert.Equal((0, CourseElement.LeftCoursePylon), (shown[1].Single().Gate, shown[1].Single().Element));
        Assert.Equal(2, shown[2].Count);

        // A rim or balloon touch sends nothing.
        int sent = f.Sessions.Sum(s => s.Sent);
        f.Links[2].Touched(2, Touch(1, CourseElement.RingRim, 0f), 12.0);
        f.Links[2].Touched(2, Touch(1, CourseElement.Balloon, 0f), 12.0);
        Assert.Equal(sent, f.Sessions.Sum(s => s.Sent));

        // Nothing it carries reaches any race: no racer moved.
        Assert.All(f.Races, race => Assert.All(race.Racers, r => Assert.Equal((0, 0, false), (r.RunsStarted, r.MostZones, r.InRun))));
        Assert.Equal((0, 0), (f.Links[0].ReportsTaken, f.Links[0].ReportsRefused));

        // ABLE-TO-FAIL CONTROLS: a forged seat, another window, a bad pylon and a NaN height are drawn
        // nowhere. The reader refuses the NaN, so the link counts only two drops.
        int before = shown.Sum(s => s.Count);
        f.Sessions[2].Send(f.Sessions[2].HostPeer, new RaceTouchMessage(1, 0, 0, RaceTouchMessage.LeftPylon, 3f, 1f), NetChannels.Events);
        f.Sessions[2].Send(f.Sessions[2].HostPeer, new RaceTouchMessage(2, 1, 0, RaceTouchMessage.LeftPylon, 3f, 1f), NetChannels.Events);
        f.Sessions[2].Send(f.Sessions[2].HostPeer, new RaceTouchMessage(2, 0, 0, 7, 3f, 1f), NetChannels.Events);
        f.Sessions[2].Send(f.Sessions[2].HostPeer, new RaceTouchMessage(2, 0, 0, RaceTouchMessage.LeftPylon, float.NaN, 1f), NetChannels.Events);
        f.Pump();
        Assert.Equal(before, shown.Sum(s => s.Count));
        Assert.Equal(1, f.Sessions[0].Forged);
        Assert.Equal(2, f.Links[0].TouchesDropped);
        Assert.Equal(2, f.Links[1].TouchesDropped);
    }

    [Fact]
    public void ATouchCrossesOnTheHostsClockAndIsNeverDrawnAheadOfTheReceiversOwn()
    {
        double hostNow = 100.0;
        double guestNow = 100.0;
        var f = TouchField(() => hostNow, () => guestNow, new NetClockSlew(5.0));
        var host = new List<double>();
        var guest = new List<double>();
        f.Links[0].TouchShown = (_, _, _, at) => host.Add(at);
        f.Links[1].TouchShown = (_, _, _, at) => guest.Add(at);

        // The guest's 10 s is the host's 15 s; the host's 20 s is the guest's 15 s.
        f.Links[1].Touched(1, Touch(0, CourseElement.LeftCoursePylon, 5f), 10.0);
        f.Links[0].Touched(0, Touch(0, CourseElement.LeftCoursePylon, 5f), 20.0);
        f.Pump();
        Assert.Equal(new[] { 15.0 }, host);
        Assert.Equal(new[] { 15.0 }, guest);

        // ABLE-TO-FAIL CONTROL: a touch stamped past the receiver's clock is drawn at its now.
        hostNow = 12.0;
        f.Links[1].Touched(1, Touch(0, CourseElement.LeftCoursePylon, 5f), 50.0);
        f.Pump();
        Assert.Equal(12.0, host[^1]);
    }

    [Fact]
    public void TheHostsRerunOpensTheGuestsNextWindowAheadOfItsLinesAndAnOldLineOrCallOpensNothing()
    {
        var f = Field();
        int reruns = 0;
        f.Guest.Rerun = () =>
        {
            reruns++;
            f.GuestRace.Rerun();
            f.GuestRace.BeginOpening(0f);
        };
        f.Guest.Report(1, NetRaceRun.Started);
        f.Guest.Report(1, NetRaceRun.Finished, runTime: 9f);
        f.Pump();
        f.Step();
        Assert.Equal(9f, f.GuestRace.Of(1)!.BestTime);

        f.HostRace.Rerun();
        f.HostRace.BeginOpening(0f);
        f.Host.CallRerun();
        f.Step();
        Assert.Equal((1, 1, 1), (reruns, f.HostRace.Window, f.GuestRace.Window));
        Assert.Null(f.GuestRace.Of(1)!.BestTime);

        // The old window's line, arriving now, is dropped; so is the same rerun heard again.
        int lines = f.Guest.Lines;
        f.HostSession.Broadcast(new RaceStandingMessage(1, 0, false, true, 1, 1, 9f, 9f, 3, 0, new[] { 3f, 6f, 9f }), NetChannels.Events);
        f.HostSession.Broadcast(new RaceCallMessage(NetRaceCall.Rerun, 1), NetChannels.Events);
        f.HostSession.Broadcast(new RaceCallMessage(NetRaceCall.Lobby, 0), NetChannels.Events);
        f.Pump();
        Assert.Equal((lines, 1, false), (f.Guest.Lines, reruns, f.Guest.LobbyCalled));
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
            new NetSeat { PeerId = 0, SeatIndex = 0, FlownHere = true, Callsign = "Red" },
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
        var bytes = new byte[RaceStandingMessage.MaxSize];
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
            new NetSeat { PeerId = 0, SeatIndex = 0, FlownHere = true, Callsign = "Red" },
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

    private static CourseTouch Touch(int gate, CourseElement element, float height) =>
        new(gate, element, 0, Godot.Vector3.Zero, height, TimeAdded: true);

    // The host on seat 0 and two guests on seats 1 and 2, each machine's race holding all three.
    private static TouchRace TouchField(Func<double>? hostClock = null, Func<double>? guestClock = null, NetClockSlew? slew = null)
    {
        var mesh = LoopbackTransport.Mesh(3, LoopbackConditions.Perfect, new Random(308));
        var roster = new[]
        {
            new NetSeat { PeerId = 0, SeatIndex = 0, FlownHere = true, Callsign = "Red" },
            new NetSeat { PeerId = 1, SeatIndex = 1, Callsign = "Blue" },
            new NetSeat { PeerId = 2, SeatIndex = 2, Callsign = "Green" },
        };
        var sessions = new[] { NetSession.Host(mesh[0], roster, 0x5EEDUL), NetSession.Guest(mesh[1]), NetSession.Guest(mesh[2]) };
        Pump(sessions);
        Assert.True(sessions[1].Joined && sessions[2].Joined);
        var races = new[] { new StuntRace(Window, Zones), new StuntRace(Window, Zones), new StuntRace(Window, Zones) };
        foreach (var race in races)
        {
            for (int seat = 0; seat < 3; seat++)
            {
                race.Add(seat, "Bloodhawk");
            }
        }

        var links = new[]
        {
            NetRaceLink.Open(sessions[0], races[0], hostClock ?? (() => 100.0), null, null, Dt),
            NetRaceLink.Open(sessions[1], races[1], guestClock ?? (() => 100.0), slew, null, Dt),
            NetRaceLink.Open(sessions[2], races[2], guestClock ?? (() => 100.0), slew, null, Dt),
        };
        return new TouchRace(sessions, links, races);
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

    private sealed record TouchRace(NetSession[] Sessions, NetRaceLink[] Links, StuntRace[] Races)
    {
        public void Pump() => NetStuntRaceTests.Pump(Sessions);
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
