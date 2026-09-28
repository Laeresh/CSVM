using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSVM.Flight.Camera;
using CSVM.Mech3;
using CSVM.Mech3.Anim;
using CSVM.Net;
using CSVM.Session.Campaign;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>
/// The host-decided range gate. C4/M03's Blacke drop polls a 64 m <c>PLAYER_RANGE</c> gate on its
/// marker, and the definitions it starts raise cutscene codes. In network co-op the host answers
/// that gate over its own field and a guest replays the verdict. The suite joins two
/// BUILT C4/M03 worlds over a lossy loopback and moves each machine's human field by hand.
/// </summary>
internal static class NetRangeGateSuites
{
    private const string Chapter = "C4";

    private const string Mission = "M03";

    private const string Gated = "blacke_drop";

    private const string Marker = "blacke_marker";

    private const float StepDt = 1f / 60f;

    // Long enough for the drop's 0.2 s poll and the link's resends at a quarter lost.
    private const float PhaseS = 3f;

    // Inside the drop's 64 m gate (compiled 4096 is metres squared), and far outside it.
    private const float InsideM = 30f;

    private const float FarM = 2000f;

    private const ulong Seed = 0xC4030045UL;

    private static readonly string[] Airframes = { "player_bhawk", "player_pfighter" };

    // What the drop's gated branch starts, and so what a replayed start is read by.
    private static readonly string[] DropCallees = { "bdplayer", "bdchute", "bdrop_ew_cam", "bdrop_we_cam" };

    [Suite("net-range-gate",
        "two BUILT C4/M03 worlds joined by a 30 ms, 25 per cent lossy loopback: the Blacke drop's "
        + "64 m range gate raises cutscene codes, so both ends take it as the host's; with the "
        + "guest's own human inside the gate and the host's field outside it, the guest polls the "
        + "gate, answers it with the host's verdict and starts nothing; once the host's copy of the "
        + "guest is inside, the host starts the drop and the guest starts the same definitions on "
        + "the host's decision, never before it, and takes every verdict the host sent")]
    internal static void RangeGate(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(SessionPaths.MissionZrdr(ctx.DataRoot, Chapter, Mission), $"{Chapter}/{Mission} zrdr");
        var report = new StringBuilder();
        var savedClock = GameClock.Current;
        ctx.CutsceneRoots = true;
        try
        {
            ctx.WithWorld(Chapter, collision: false, Mission, hostWorld =>
                ctx.WithWorld(Chapter, collision: false, Mission, guestWorld =>
                {
                    if (ReferenceEquals(hostWorld.Runtime, guestWorld.Runtime))
                    {
                        throw new SuiteSkippedException($"{Chapter}/{Mission} is this run's cached world, so a second build is the same one");
                    }

                    Drive(ctx, hostWorld.Runtime, guestWorld.Runtime, report);
                }));
        }
        finally
        {
            ctx.CutsceneRoots = false;
            GameClock.Current = savedClock;
        }

        ctx.WriteArtifact($"test-net-range-gate-{Chapter}-{Mission}.txt", report.ToString());
        ctx.Note($"{report.ToString().TrimEnd().Replace(System.Environment.NewLine, "; ")}");
    }

    private static void Drive(TestContext ctx, AnimRuntime host, AnimRuntime guest, StringBuilder report)
    {
        var def = host.DefsFor(Gated).FirstOrDefault()
            ?? throw new SuiteSkippedException($"{Chapter}/{Mission} carries no '{Gated}' definition");
        var hostMarker = host.FindNodes(Marker).FirstOrDefault();
        var guestMarker = guest.FindNodes(Marker).FirstOrDefault();
        if (hostMarker == null || guestMarker == null)
        {
            ctx.Check(false, $"both worlds build the drop's '{Marker}'");
            return;
        }

        ctx.Check(host.RangeGates.HostDecides(def) && guest.RangeGates.HostDecides(guest.DefsFor(Gated)[0]),
            $"both ends take the range gate of '{Gated}' as the host's, since its call closure raises a CALLBACK");

        GameClock.Current = new GameClock { Mode = GameClock.RunMode.FixedStep };
        var mesh = LoopbackTransport.Mesh(2, new LoopbackConditions(0.03, 0.01, 0.25), new Random(4545));
        var roster = new NetSeat[]
        {
            new() { PeerId = 0, SeatIndex = 0, IsLocal = true, Callsign = "host", PlaneNode = Airframes[0] },
            new() { PeerId = 1, SeatIndex = 1, Callsign = "guest", PlaneNode = Airframes[1] },
        };
        var hostNet = NetSession.Host(mesh[0], roster, Seed, null, Airframes);
        var guestNet = NetSession.Guest(mesh[1], Airframes);
        for (int i = 0; i < 200 && !guestNet.Joined; i++)
        {
            hostNet.Step(StepDt);
            guestNet.Step(StepDt);
        }

        ctx.Check(guestNet.Joined && guestNet.LocalSeat == 1, $"the guest joins as seat 1 over the lossy link");

        var at = AnimRuntime.WorldTransform(hostMarker, out _).Origin;
        var far = at + (Vector3.Up * FarM);
        var inside = at + (Vector3.Up * InsideM);
        // Each machine's field: its own seat and its copy of the other, set by hand per phase.
        var hostField = new[] { far, far };
        var guestField = new[] { far, far };
        var ends = new[] { host, guest };
        var saved = ends.Select(rt => (rt.PlayerPositions, rt.OnInstanceStarted)).ToArray();
        var started = new[] { new Dictionary<string, float>(), new Dictionary<string, float>() };
        float now = 0f;
        var asked = new List<string>();
        try
        {
            host.PlayerPositions = () => hostField;
            guest.PlayerPositions = () => guestField;
            for (int e = 0; e < ends.Length; e++)
            {
                var into = started[e];
                ends[e].OnInstanceStarted = (d, _) =>
                {
                    if (d.AnimName is { } name && DropCallees.Contains(name) && !into.ContainsKey(name))
                    {
                        into[name] = now;
                    }
                };
            }

            var hostLink = NetPositionalStartLink.Open(hostNet, () => Array.Empty<PlayerRig>(), null, null, host);
            var guestLink = NetPositionalStartLink.Open(guestNet, () => Array.Empty<PlayerRig>(), null, null, guest);
            var replay = guest.RangeGates.HostVerdict!;
            float firstPass = float.NaN;
            guest.RangeGates.HostVerdict = gate =>
            {
                asked.Add(gate);
                bool passed = replay(gate);
                if (passed && float.IsNaN(firstPass))
                {
                    firstPass = now;
                }

                return passed;
            };
            float firstSent = float.NaN;
            var send = host.RangeGates.Decided!;
            host.RangeGates.Decided = (gate, passed) =>
            {
                if (passed && float.IsNaN(firstSent))
                {
                    firstSent = now;
                }

                send(gate, passed);
            };

            // ABLE-TO-FAIL CONTROL: the guest's own human sits inside the gate, where its own read
            // would pass. The host's copy of that human is outside it.
            guestField[1] = inside;
            Run(PhaseS, () => false);
            string gateName = RangeGateAuthority.GateName(guest.DefsFor(Gated)[0], Marker, 4096f);
            int polled = asked.Count(g => g == gateName);
            report.AppendLine($"guest inside, host's copy outside: guest polled '{gateName}' {polled} time(s); "
                + $"host started [{string.Join(", ", started[0].Keys)}], guest started [{string.Join(", ", started[1].Keys)}]");
            ctx.Check(polled > 0 && started[1].Count == 0,
                $"CONTROL: with its own human {InsideM:0} m from '{Marker}', the guest polls the gate {polled} time(s), takes the host's verdict and starts none of the drop");
            ctx.Check(started[0].Count == 0 && hostLink.GateVerdicts == 0,
                $"and the host, whose field is outside the gate, starts nothing and sends no verdict");

            // The host's copy of the guest arrives inside: the host decides, the guest replays.
            hostField[1] = inside;
            Run(PhaseS, () => started[1].ContainsKey("bdplayer"));
            float hostAt = started[0].TryGetValue("bdplayer", out float h) ? h : float.NaN;
            float guestAt = started[1].TryGetValue("bdplayer", out float g) ? g : float.NaN;
            report.AppendLine($"host's copy inside: host started [{string.Join(", ", started[0].Keys)}] at {hostAt:0.000}s, "
                + $"guest started [{string.Join(", ", started[1].Keys)}] at {guestAt:0.000}s; host sent its pass at {firstSent:0.000}s, "
                + $"guest first read it at {firstPass:0.000}s; verdicts sent {hostLink.GateVerdicts}, taken {guestLink.GateVerdicts}");
            ctx.Check(started[0].ContainsKey("bdplayer") && hostLink.GateVerdicts > 0,
                $"over its own field the host passes the gate, starts the drop and sends its verdict");
            // Strictly later: the link holds every verdict at least its 30 ms, so a same-step start
            // on the guest could only be its own read.
            ctx.Check(!float.IsNaN(guestAt) && guestAt > hostAt && firstPass > firstSent,
                $"and the guest starts the drop on the host's decision, {(guestAt - hostAt) * 1000f:0} ms after it and never before");

            // Everyone leaves; the settle lets the last resend land.
            hostField[1] = far;
            guestField[1] = far;
            Run(PhaseS, () => false);
            var hostSet = started[0].Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            var guestSet = started[1].Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            report.AppendLine($"settled: verdicts sent {hostLink.GateVerdicts}, taken {guestLink.GateVerdicts}; "
                + $"host [{string.Join(", ", hostSet)}], guest [{string.Join(", ", guestSet)}]");
            ctx.Check(guestLink.GateVerdicts == hostLink.GateVerdicts,
                $"the guest takes every verdict the host sent over the lossy link ({guestLink.GateVerdicts} of {hostLink.GateVerdicts})");
            ctx.Check(hostSet.SequenceEqual(guestSet),
                $"and both ends started the same drop definitions ([{string.Join(", ", guestSet)}])");
        }
        finally
        {
            for (int e = 0; e < ends.Length; e++)
            {
                ends[e].PlayerPositions = saved[e].PlayerPositions;
                ends[e].OnInstanceStarted = saved[e].OnInstanceStarted;
                ends[e].RangeGates.Decided = null;
                ends[e].RangeGates.HostVerdict = null;
            }
        }

        // Both ends through the window, the host first as a listen server runs, until the
        // condition holds. The clock every start and verdict is stamped with advances per step.
        void Run(float seconds, Func<bool> done)
        {
            for (float t = 0f; t < seconds && !done(); t += StepDt)
            {
                now += StepDt;
                GameClock.Current!.BeginFrame(StepDt);
                hostNet.Step(StepDt);
                guestNet.Step(StepDt);
                host.Advance(StepDt);
                guest.Advance(StepDt);
            }
        }
    }
}
