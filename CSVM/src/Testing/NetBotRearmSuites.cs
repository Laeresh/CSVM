using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Weapons;
using CSVM.Launch;
using CSVM.Net;
using CSVM.Session.World;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>A bot's rearm run between whole sessions in one process, on the chapter's MP1, whose
/// base mp1.gw leaves on. The host flies the bot and only the host's base runtime checks it, so
/// every reading is the host's. The guest's seat and the host's own stand in the field as quarries.
/// The rig is <see cref="NetCombatSuites"/>'s.</summary>
internal static class NetBotRearmSuites
{
    private const ulong HostSeed = 0xB07AE4E0UL;

    private const int HostSeat = 0;
    private const int GuestSeat = 1;
    private const int BotSeat = 2;

    // How long a check that something does NOT start keeps watching, in sim steps.
    private const int ControlSteps = 300;

    // The most steps the run to the base is given. From the far side of the bay the bot flies past
    // the base to the gate and back, at cruise. It takes about 5300.
    private const int RunSteps = 8000;

    // Where the run starts from: this far out on the bay's closed side and this far above the node.
    private const float FarSide = 3000f;
    private const float FarAbove = 400f;

    // The most steps any shorter wait is given: a hand-back, a quarry taken, a chase, a return.
    private const int WaitSteps = 600;

    // How long a damaged bot flies its run before it is shot down, in sim steps.
    private const int DownedAfter = 120;

    // Where a quarry is put: ahead of the bot and above it, well inside the attack radius.
    private const float LureAhead = 800f;
    private const float LureAbove = 250f;

    private static readonly string[] Airframes = { "player_pfighter", "player_fbrand" };

    [Suite("net-bot-rearm",
        "a host with a bot seat and a guest seat on the chapter's MP1 over a clean loopback: with "
        + "rockets left and its hull above the threshold the bot fights on and starts no run, its guns "
        + "emptied included; every pylon emptied, it breaks off, holds no quarry with a hostile put in "
        + "its path, flies the bay's open side to the base, which restores its pylons, guns and hull, "
        + "and once clear takes a quarry and chases it again; its hull under the threshold it breaks "
        + "off with full racks, and shot down on the way it comes back with no run standing")]
    internal static void ABotBreaksOffToABaseAndRejoins(TestContext ctx)
    {
        var spec = NetCombatSuites.MatchSpec(ctx, out _, "--vs-kills=0", "--vs-time=0");
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(5105));
        var roster = new NetSeat[]
        {
            new() { PeerId = 0, SeatIndex = HostSeat, FlownHere = true, Callsign = "host", PlaneNode = Airframes[0] },
            new() { PeerId = 1, SeatIndex = GuestSeat, Callsign = "guest", PlaneNode = Airframes[1] },
            NetSeats.Bot(0, BotSeat, "bot", Airframes[0]),
        };
        NetSeats.Validate(roster, hostPeer: 0);

        var ambient = NetCombatSuites.Ambient.Save();
        NetCombatSuites.Ends? host = null;
        NetCombatSuites.Ends? guest = null;
        try
        {
            host = NetCombatSuites.Ends.Open(ctx, spec, mesh[0], isHost: true, HostSeed, roster, Airframes);
            guest = NetCombatSuites.Ends.Open(ctx, spec, mesh[1], isHost: false, HostSeed + 1, null, Airframes);
            ctx.Check(host.Built && guest.Built, $"both sessions build in one process (host {host.Built}, guest {guest.Built})");
            if (!host.Built || !guest.Built)
            {
                return;
            }

            NetStartSuites.UntilStarted(host.Session, guest.Session);
            var peers = new[] { host.Session, guest.Session };
            Lockstep(1, peers);
            var bot = host.Session.SeatRigs[BotSeat].Controller!;
            var rearm = host.Session.Dogfight?.RearmPlay;
            ctx.Check(rearm is { BaseCount: 2 } && bot.Pilot?.RearmOrder != null
                      && host.Session.SeatRigs[HostSeat].Controller!.Pilot == null,
                $"the host lists MP1's two rearm nodes and arms the bot's pilot, not its own pane, with a rearm order ({rearm?.BaseCount} bases)");
            if (rearm is not { BaseCount: 2 } || bot.Pilot?.RearmOrder == null)
            {
                return;
            }

            Lift(host.Session.SeatRigs[HostSeat].Controller!);
            Lift(guest.Session.SeatRigs[GuestSeat].Controller!);
            FightsOnAboveTheThresholds(ctx, peers, bot);
            EmptyGunsSendNothing(ctx, peers, bot);
            if (FliesToTheBase(ctx, peers, bot, rearm))
            {
                RejoinsTheFight(ctx, peers, bot);
            }

            DownedOnTheWay(ctx, peers, bot);
        }
        finally
        {
            guest?.Close();
            host?.Close();
            ambient.Restore();
        }
    }

    // ABLE-TO-FAIL CONTROL for the run below: half a load of guns and rockets and half a hull call
    // for nothing. The rocketeer's 30 s lockout lets it launch once at most in the window, so a
    // round left on every pylon cannot all go.
    private static void FightsOnAboveTheThresholds(TestContext ctx, GameSession[] peers, FlightController bot)
    {
        var order = bot.Pilot!.RearmOrder!;
        foreach (var gun in bot.Loadout!.FirableGuns)
        {
            gun.Ammo = gun.Capacity / 2;
        }

        foreach (var pylon in bot.Loadout.Hardpoints)
        {
            pylon.Ammo = Math.Max(1, pylon.Capacity / 2);
        }

        bot.Damage!.ScalePools(0.5f, 0.5f);
        bool flew = false;
        bool chased = false;
        for (int step = 0; step < ControlSteps; step++)
        {
            Lockstep(1, peers);
            flew |= order.Flying;
            chased |= bot.Pilot.Gunner!.Target != null;
        }

        ctx.Check(!flew && chased,
            $"ABLE-TO-FAIL CONTROL: with half its guns, rockets on its pylons and half its hull the bot keeps a quarry and starts no run over {ControlSteps} steps (guns {Load(bot.Loadout.FirableGuns)}, rockets {Load(bot.Loadout.Hardpoints)}, hull {bot.Damage.SummaryHealthFraction:0.00}, run {flew}, quarry {chased})");
    }

    // ABLE-TO-FAIL CONTROL for the gun trigger's removal: a dry magazine with full racks and a sound
    // hull sends nothing. The gun trigger broke off within two steps.
    private static void EmptyGunsSendNothing(TestContext ctx, GameSession[] peers, FlightController bot)
    {
        var order = bot.Pilot!.RearmOrder!;
        bot.Rearm();
        foreach (var gun in bot.Loadout!.Guns)
        {
            gun.Ammo = 0;
        }

        bool flew = false;
        for (int step = 0; step < ControlSteps; step++)
        {
            Lockstep(1, peers);
            flew |= order.Flying;
        }

        ctx.Check(!flew && bot.Loadout.Guns.All(g => g.Ammo == 0),
            $"ABLE-TO-FAIL CONTROL: its guns emptied with its racks full and its hull sound, it starts no run over {ControlSteps} steps (guns {Load(bot.Loadout.FirableGuns)}, rockets {Load(bot.Loadout.Hardpoints)}, hull {bot.Damage!.SummaryHealthFraction:0.00}, run {flew})");
    }

    // Every pylon emptied, the bot breaks off at once and takes no quarry with one put in its path.
    // The base restores it on the pass, its racks full again.
    private static bool FliesToTheBase(TestContext ctx, GameSession[] peers, FlightController bot, RearmRuntime rearm)
    {
        var pilot = bot.Pilot!;
        var order = pilot.RearmOrder!;
        var gunner = pilot.Gunner!;
        foreach (var gun in bot.Loadout!.Guns)
        {
            gun.Ammo = gun.Capacity;
        }

        EmptyPylons(bot);
        int before = rearm.Rearms;
        int steps = StepUntil(() => order.Flying && gunner.Target == null, peers, WaitSteps);
        ctx.Check(order.Flying && order.Reason == "rockets out" && gunner.Target == null && pilot.Machine!.Mode != AiMode.Pursue,
            $"every pylon emptied, with full guns, the bot breaks off for a base and drops its quarry ({order.Leg}, {order.Reason}, quarry {FlightController.TargetLabel(gunner.Target)}, {AiModeMachine.NameOf(pilot.Machine!.Mode)}, {steps} step(s))");

        // Set down on the far side of the bay from the side the run chose, the run must fly round to
        // the gate. The return clears the run, refills the pylons and mends the hull. So the pylons
        // are emptied again and the hull halved, short of its own threshold.
        var far = order.Base - (order.Approach * FarSide) + (Vector3.Up * FarAbove);
        bot.RespawnAt(far, order.Base + (Vector3.Up * FarAbove));
        bot.ArmSpawnTimers();
        EmptyPylons(bot);
        bot.Damage!.ScalePools(0.5f, 0.5f);

        Lockstep(1, peers);
        ctx.Check(order.Leg == AiRearmLeg.Gate && order.Approach.Dot(bot.WorldPosition - order.Base) < 0f,
            $"set down on the far side of the bay, it flies round to the gate on the open side ({order.Leg}, in from bearing {AiPilot.HeadingDegOf(order.Approach):0})");

        int respawns = bot.RespawnCount;
        bool lured = false;
        bool engaged = false;
        float closest = float.MaxValue;
        steps = 0;
        while (rearm.Rearms == before && order.Flying && steps < RunSteps)
        {
            Lockstep(1, peers);
            steps++;
            engaged |= gunner.Target != null || pilot.Machine!.Mode == AiMode.Pursue;
            closest = Mathf.Min(closest, bot.WorldPosition.DistanceTo(order.Base));
            if (!lured && order.Leg == AiRearmLeg.Final)
            {
                // A hostile ahead in the bot's path, which a gunner on duty would take at once.
                lured = true;
                PutAhead(peers[0].SeatRigs[HostSeat].Controller!, bot);
            }
        }

        var damage = bot.Damage!;
        bool full = bot.Loadout.Hardpoints.Count > 0
            && bot.Loadout.Hardpoints.All(h => h.Ammo == h.Capacity)
            && bot.Loadout.FirableGuns.All(g => g.Ammo == g.Capacity)
            && Mathf.IsEqualApprox(damage.WholeHealth, damage.WholeHealthMax);
        ctx.Check(rearm.Rearms == before + 1 && full && order.Leg == AiRearmLeg.Clear,
            $"it reaches the base, which restores its pylons, its guns and its hull ({rearm.Rearms - before} rearm(s), rockets {Load(bot.Loadout.Hardpoints)}, guns {Load(bot.Loadout.FirableGuns)}, health {damage.WholeHealth:0}/{damage.WholeHealthMax:0}, {order.Leg}, {steps} step(s), closest {closest:0.0} m)");
        ctx.Check(lured && !engaged,
            $"on the way it takes no quarry and chases nothing, a hostile put {LureAhead:0} m ahead in its path included (lure placed {lured}, engaged {engaged})");
        ctx.Check(bot.RespawnCount == respawns && !bot.Crashed,
            $"and flies the run without going down ({bot.RespawnCount - respawns} return(s))");
        return rearm.Rearms == before + 1;
    }

    // Clear of the base the run ends, and the gunner takes a quarry and the pilot chases it.
    private static void RejoinsTheFight(TestContext ctx, GameSession[] peers, FlightController bot)
    {
        var pilot = bot.Pilot!;
        var order = pilot.RearmOrder!;
        var node = order.Base;
        int steps = StepUntil(() => !order.Flying, peers, WaitSteps);
        var off = bot.WorldPosition - node;
        float clear = new Vector2(off.X, off.Z).Length();
        ctx.Check(!order.Flying && clear > AiRearmOrder.ClearM && !bot.Crashed,
            $"restored, it flies clear of the base before the run ends ({order.Leg}, {clear:0} m out, {steps} step(s))");

        // A quarry in range ahead, since the field's other aeroplanes may have flown far off.
        PutAhead(peers[0].SeatRigs[HostSeat].Controller!, bot);
        steps = StepUntil(() => pilot.Gunner!.Target != null && pilot.Machine!.Mode == AiMode.Pursue, peers, WaitSteps);
        ctx.Check(pilot.Gunner!.Target != null && pilot.Machine!.Mode == AiMode.Pursue && !pilot.Gunner.Disengaged,
            $"and back in the fight its gunner takes a quarry and the pilot chases it ({FlightController.TargetLabel(pilot.Gunner.Target)}, {AiModeMachine.NameOf(pilot.Machine!.Mode)}, {steps} step(s))");
    }

    // The hull alone starts a run with full racks. Shot down on the way, the bot comes back with none.
    private static void DownedOnTheWay(TestContext ctx, GameSession[] peers, FlightController bot)
    {
        var pilot = bot.Pilot!;
        var order = pilot.RearmOrder!;
        foreach (var pylon in bot.Loadout!.Hardpoints)
        {
            pylon.Ammo = pylon.Capacity;
        }

        bot.Damage!.ScalePools(0.3f, 0.3f);
        int steps = StepUntil(() => order.Flying, peers, WaitSteps);
        ctx.Check(order.Flying && order.Reason.StartsWith("hull ", StringComparison.Ordinal)
                  && bot.Loadout.Hardpoints.All(h => h.Ammo == h.Capacity),
            $"its hull under the threshold, the bot breaks off with full racks ({order.Leg}, {order.Reason}, rockets {Load(bot.Loadout.Hardpoints)}, {steps} step(s))");
        Lockstep(DownedAfter, peers);
        ctx.Check(order.Flying && pilot.Gunner!.Disengaged,
            $"ABLE-TO-FAIL CONTROL: the run still stands {DownedAfter} steps on, its gunner disengaged ({order.Leg})");

        int respawns = bot.RespawnCount;
        bot.DebugForceCrash();
        steps = StepUntil(() => bot.RespawnCount > respawns && !bot.Crashed, peers, WaitSteps);
        ctx.Check(bot.RespawnCount > respawns && !order.Flying && !pilot.Gunner!.Disengaged,
            $"shot down on the way, it comes back with no run standing and its gunner on duty ({order.Leg}, disengaged {pilot.Gunner!.Disengaged}, {steps} step(s))");
        bool flew = false;
        for (int step = 0; step < ControlSteps; step++)
        {
            Lockstep(1, peers);
            flew |= order.Flying;
        }

        ctx.Check(!flew, $"and its fresh airframe, full again, starts no run over {ControlSteps} steps");
    }

    // Rounds left of a full load, summed over the slots, for a check's message.
    private static string Load(IEnumerable<IAmmoSlot> slots)
    {
        var all = slots.ToList();
        return FormattableString.Invariant($"{all.Sum(s => s.Ammo)}/{all.Sum(s => s.Capacity)}");
    }

    private static void EmptyPylons(FlightController bot)
    {
        foreach (var pylon in bot.Loadout!.Hardpoints)
        {
            pylon.Ammo = 0;
        }
    }

    // The host's own aeroplane put ahead of the bot and above it, by its own respawn.
    private static void PutAhead(FlightController quarry, FlightController bot)
    {
        var at = bot.WorldPosition + (bot.NoseDirection * LureAhead) + (Vector3.Up * LureAbove);
        quarry.RespawnAt(at, at + bot.NoseDirection);
        quarry.ArmSpawnTimers();
    }

    // Put 500 m above where it flies, with a fresh collision window, by its owner's own respawn.
    private static void Lift(FlightController pilot)
    {
        var at = pilot.WorldPosition + (Vector3.Up * 500f);
        pilot.RespawnAt(at, at + (Vector3.Right * 100f));
        pilot.ArmSpawnTimers();
    }

    // Steps both sessions until the condition holds, and answers how many steps that took, or
    // the whole budget when it never did.
    private static int StepUntil(Func<bool> done, GameSession[] peers, int budget)
    {
        for (int step = 1; step <= budget; step++)
        {
            Lockstep(1, peers);
            if (done())
            {
                return step;
            }
        }

        return budget;
    }

    // Both sessions through the same number of fixed steps, host first, the order a listen
    // server runs in.
    private static void Lockstep(int steps, GameSession[] peers)
    {
        for (int i = 0; i < steps; i++)
        {
            foreach (var session in peers)
            {
                session._PhysicsProcess(GameClock.FixedDt);
            }
        }
    }
}
