using System;
using System.Linq;
using CSVM.Launch;
using CSVM.Net;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The start of a network flight: nobody's simulation runs until every seated machine
/// has built its world. The rig is <see cref="NetCombatSuites"/>'s, one session per peer in one
/// process. A slow guest is a guest whose session is built only after the host has been stepped
/// on its own for a while.</summary>
internal static class NetStartSuites
{
    private const ulong HostSeed = 0xC0FFEE21UL;

    // How long the host is stepped alone before the slow guest builds, in sim steps. Three
    // seconds is well past anything a held start could round away.
    private const int SlowLoadSteps = 180;

    // How many steps a released start is given to reach every machine. A reliable word crosses
    // the latency link below in three.
    private const int ReleaseSteps = 30;

    // The most mission clock the host may have run when the slow guest's aeroplane first moves.
    // It is the latency there and back, with room to spare.
    private const float StartSkewSeconds = 0.25f;

    // How far an aeroplane may drift and still count as standing still, in metres.
    private const float StillMetres = 0.01f;

    [Suite("net-start-together",
        "a Dogfight host and guests in one process whose guests build late: the host's mission "
        + "clock and aeroplane stand still while a guest is still loading, and start together "
        + "with the guest's once it has built, and a guest that drops out while loading releases "
        + "the host and the guest already waiting")]
    internal static void EveryoneStartsTogether(TestContext ctx)
    {
        var spec = NetCombatSuites.MatchSpec(ctx, out _);
        var ambient = NetCombatSuites.Ambient.Save();
        try
        {
            SlowGuest(ctx, spec);
            DroppedWhileLoading(ctx, spec);
        }
        finally
        {
            ambient.Restore();
        }
    }

    // Steps every session, host first, until none holds its start, and says how many steps that
    // took. For a suite whose first reading needs a flight that runs, over a link with latency.
    internal static int UntilStarted(params GameSession[] sessions)
    {
        int steps = 0;
        for (; steps < SlowLoadSteps && sessions.Any(s => s.StartHeld); steps++)
        {
            Step(1, sessions);
        }

        return steps;
    }

    // One host, one guest built three seconds late over a link with latency.
    private static void SlowGuest(TestContext ctx, SessionSpec spec)
    {
        var mesh = LoopbackTransport.Mesh(2, new LoopbackConditions(0.05, 0.0, 0.0), new Random(4301));
        NetCombatSuites.Ends? host = null;
        NetCombatSuites.Ends? guest = null;
        try
        {
            host = NetCombatSuites.Ends.Open(ctx, spec, mesh[0], isHost: true, HostSeed, NetCombatSuites.Roster(2));
            ctx.Check(host.Built, $"[slow guest] the host builds ({host.Built})");
            if (!host.Built || Own(host.Session) is not { } hostPlane || host.Session.Versus is not { } match)
            {
                return;
            }

            var hostFrom = hostPlane.GlobalPosition;
            Step(SlowLoadSteps, host.Session);
            float drift = hostPlane.GlobalPosition.DistanceTo(hostFrom);
            ctx.Check(match.Elapsed == 0f && drift < StillMetres,
                $"[slow guest] a host stepped {SlowLoadSteps} times while its guest loads runs no mission clock and flies nowhere ({match.Elapsed:0.00} s, {drift:0.00} m)");

            guest = NetCombatSuites.Ends.Open(ctx, spec, mesh[1], isHost: false, HostSeed + 1, null);
            ctx.Check(guest.Built, $"[slow guest] the guest builds late ({guest.Built})");
            if (!guest.Built || Own(guest.Session) is not { } guestPlane)
            {
                return;
            }

            var guestFrom = guestPlane.GlobalPosition;
            float hostClockAtStart = -1f;
            for (int i = 0; i < ReleaseSteps && hostClockAtStart < 0f; i++)
            {
                host.Session._PhysicsProcess(GameClock.FixedDt);
                guest.Session._PhysicsProcess(GameClock.FixedDt);
                if (guestPlane.GlobalPosition.DistanceTo(guestFrom) >= StillMetres)
                {
                    hostClockAtStart = match.Elapsed;
                }
            }

            ctx.Check(hostClockAtStart >= 0f && hostClockAtStart <= StartSkewSeconds,
                $"[slow guest] the guest's aeroplane starts within {ReleaseSteps} steps of its build, with the host's mission clock at most {StartSkewSeconds:0.00} s in ({hostClockAtStart:0.00} s)");
            ctx.Check(hostPlane.GlobalPosition.DistanceTo(hostFrom) >= StillMetres,
                $"[slow guest] and the host's own aeroplane is flying by then");
            ctx.Check(host.Session.StartGate?.Release == NetStartRelease.Everyone && guest.Session.StartGate?.Release == NetStartRelease.Started,
                $"[slow guest] the host opened on every guest loaded and the guest on the host's word ({host.Session.StartGate?.Release}, {guest.Session.StartGate?.Release})");
        }
        finally
        {
            guest?.Close();
            host?.Close();
        }
    }

    // One host and two guests on a star, the second of which never builds and then drops.
    private static void DroppedWhileLoading(TestContext ctx, SessionSpec spec)
    {
        var mesh = LoopbackTransport.Mesh(3, LoopbackConditions.Perfect, new Random(4303));
        mesh[1].Disconnect(2);
        NetCombatSuites.Ends? host = null;
        NetCombatSuites.Ends? first = null;
        try
        {
            host = NetCombatSuites.Ends.Open(ctx, spec, mesh[0], isHost: true, HostSeed, NetCombatSuites.Roster(3));
            first = NetCombatSuites.Ends.Open(ctx, spec, mesh[1], isHost: false, HostSeed + 1, null);
            ctx.Check(host.Built && first.Built, $"[dropped] the host and the first guest build ({host.Built}, {first.Built})");
            if (!host.Built || !first.Built || host.Session.Versus is not { } match
                || Own(first.Session) is not { } firstPlane)
            {
                return;
            }

            var firstFrom = firstPlane.GlobalPosition;
            Step(SlowLoadSteps, host.Session, first.Session);
            float drift = firstPlane.GlobalPosition.DistanceTo(firstFrom);
            ctx.Check(match.Elapsed == 0f && drift < StillMetres,
                $"[dropped] while the second guest is still loading, neither the host's mission clock nor the first guest's aeroplane moves ({match.Elapsed:0.00} s, {drift:0.00} m)");

            mesh[0].Disconnect(2);
            Step(ReleaseSteps, host.Session, first.Session);
            drift = firstPlane.GlobalPosition.DistanceTo(firstFrom);
            ctx.Check(match.Elapsed > 0f && drift >= StillMetres,
                $"[dropped] the second guest's link dropping releases the host and the first guest ({match.Elapsed:0.00} s, {drift:0.00} m)");
            ctx.Check(host.Session.StartGate?.Release == NetStartRelease.Left,
                $"[dropped] and the host names the drop as what released it ({host.Session.StartGate?.Release})");
        }
        finally
        {
            first?.Close();
            host?.Close();
        }
    }

    // The aeroplane this machine flies, read off its own seat's rig.
    private static Node3D? Own(GameSession session) =>
        session.NetLink is { LocalSeat: >= 0 } link && link.LocalSeat < session.SeatRigs.Count
            ? session.SeatRigs[link.LocalSeat].Controller
            : null;

    // Every session through the same number of fixed steps, host first.
    private static void Step(int steps, params GameSession[] sessions)
    {
        for (int i = 0; i < steps; i++)
        {
            foreach (var session in sessions)
            {
                session._PhysicsProcess(GameClock.FixedDt);
            }
        }
    }
}
