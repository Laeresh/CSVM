using System;
using System.Collections.Generic;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Utils;
using Godot;

namespace CSVM.Session.World;

/// <summary>
/// The stunt race bots this machine flies, each on its own <see cref="StuntBotObjective"/> over its
/// own run of the course. Each step it steers a bot for its next zone or hands the zone to the pilot's
/// ribbon run. A completed run starts again while the window is open, as a person reruns. The run's
/// own gate test counts the zones and the race hears them as any seat's. A guest flies no bot.
/// </summary>
internal sealed class StuntRaceBots
{
    // How far below a zone's entry the fill-time probe looks for the built world, metres.
    private const float WorldProbeM = 3000f;

    // How far past a struck body the route's cast resumes, metres, and how many bodies one chord's
    // cast may pass through. Neither is a game value.
    private const float StruckStepM = 0.5f;
    private const int StruckPerChord = 8;

    // How near a zone's gate an optional target stands, metres. Not a game value.
    private const float OptionalReachM = 50f;

    // Structures a bot shoots on the run past them though its route clears them. Remake-only: C2's
    // Ramses Tomb wall. The Hollywood sign's letters frame dzpath7's gate as closely, and stand.
    private static readonly string[] OptionalTargets = ["ramses"];

    private readonly StuntRace _race;
    private readonly IReadOnlyList<PlayerRig> _seats;
    private readonly List<(int Seat, StuntBotObjective Objective)> _bots;
    private readonly BotOrderLog _log = new();
    private readonly Dictionary<int, int> _respawns = new();

    // Each zone's targets, found when a bot first takes it. The blocking structure each route end
    // cannot see, found when a pick first reads that end.
    private readonly Dictionary<StuntZone, List<DestructibleRegistry.Instance>> _targets = new();
    private readonly Dictionary<(DangerZoneRibbon, bool), DestructibleRegistry.Instance?> _unseen = new();

    // The bot whose world queries decide the ends, every bot's world being the same.
    private readonly FlightController _lead;

    private StuntRaceBots(StuntRace race, IReadOnlyList<PlayerRig> seats, List<(int, StuntBotObjective)> bots, bool runsSawWorld, FlightController lead)
    {
        _race = race;
        _seats = seats;
        _bots = bots;
        _lead = lead;
        RunsSawWorld = runsSawWorld;
        foreach (var (_, objective) in bots)
        {
            objective.Shut = Shut;
        }
    }

    /// <summary>Whether the bots take their objectives. Off, each holds its course and flies no zone;
    /// a suite's control turns it off.</summary>
    public bool BotObjective { get; set; } = true;

    /// <summary>Whether an end no round reaches a standing structure from is shut to the bots. Off,
    /// they take either end; a suite's control turns it off.</summary>
    public bool EndsShut { get; set; } = true;

    /// <summary>Whether the world ray the run table was decided with struck the built world below the
    /// first zone's entry when the table was filled. A ray that strikes nothing refuses no end.</summary>
    public bool RunsSawWorld { get; }

    /// <summary>One objective per bot seat flown here with a run of the course and its tier read off
    /// <paramref name="roster"/>. Every fluff is drawn on one seeded stream, and each bot's own approach
    /// on its seat's. Null with none. Every zone
    /// end's run is decided here, once for every bot, against the built world.</summary>
    public static StuntRaceBots? For(StuntRace race, IReadOnlyList<PlayerRig> seats, IReadOnlyList<NetSeat> roster)
    {
        var rng = new Random(Rng.IntSeedFor(Rng.StuntBots));
        var bots = new List<(int, StuntBotObjective)>();
        StuntBotObjective.RunTable? runs = null;
        FlightController? lead = null;
        bool sawWorld = false;
        for (int seat = 0; seat < seats.Count && seat < roster.Count; seat++)
        {
            if (seats[seat].Controller is { IsBotSeat: true, Pilot: { } pilot, Stunt: { } run } bot)
            {
                // Every run's zones share the course's routes, so one bot's run keys the table for all.
                if (runs == null)
                {
                    lead = bot;
                    Func<Vector3, Vector3, bool> blocked = bot.WorldBlocksLine;
                    long mark = StartupProfile.Mark();
                    runs = StuntBotObjective.RunsFrom(run.Zones, blocked);
                    StartupProfile.Record("lead_ins", mark);
                    sawWorld = SeesWorld(run.Zones, blocked);
                }

                var objective = new StuntBotObjective(roster[seat].Skill, rng) { Runs = runs, Approach = StuntBotObjective.Spread(new Random(Rng.IntSeedFor($"{Rng.StuntBotApproaches}{seat}"))) };
                pilot.CruiseThrottle = objective.CruiseThrottle;
                bots.Add((seat, objective));
            }
        }

        return lead == null ? null : new StuntRaceBots(race, seats, bots, sawWorld, lead);
    }

    /// <summary>One step of every bot, before the seats step. A bot behind its count, down, or in a
    /// race that ended takes no order.</summary>
    public void Step()
    {
        if (_race.Ended)
        {
            return;
        }

        foreach (var (seat, objective) in _bots)
        {
            if (_seats[seat].Controller is not { Pilot: { } pilot, Stunt: { } run } bot
                || !GodotObject.IsInstanceValid(bot) || bot.Crashed || bot.StartCount.Running)
            {
                continue;
            }

            var at = bot.WorldPosition;
            if (_respawns.GetValueOrDefault(seat) != bot.RespawnCount)
            {
                _respawns[seat] = bot.RespawnCount;
                objective.Returned();
                Rejoin(seat, bot, run, at);
            }

            if (run.AllComplete && _race.MayStartRun && BotObjective)
            {
                Log.Info("flight", $"stunt race: bot seat {seat} completed a run in {StuntMission.FormatTime(run.Elapsed)}, running again");
                bot.Rerun();
                continue;
            }

            var order = BotObjective ? objective.Decide(run.Zones, at, bot.WorldVelocity, pilot.ZoneRun != null)
                : new StuntBotOrder(StuntBotLeg.None, -1, at, false, BotOrderLog.ObjectiveOff);
            if (order.StartsRun)
            {
                pilot.FlyDangerZone(order.Ribbon!, order.FarEnd, at, "race bot");
            }
            else if (order.Leg is StuntBotLeg.Steer or StuntBotLeg.Fluff)
            {
                pilot.TargetHeadingDeg = AiPilot.HeadingDegOf(order.Point - at);
                pilot.TargetAltitude = order.Point.Y;
            }

            // The gunner takes the nearest target of the zone the bot steers for or runs. With no
            // zone it holds no target and never fires, since a race's rounds have nothing else to strike.
            if (pilot.Gunner is { } gunner)
            {
                gunner.Target = order.Zone >= 0 && bot.Destructibles is { } structures
                    ? Nearest(bot, run.Zones[order.Zone], structures) : null;
            }

            if (_log.Admit(seat, order.Why))
            {
                string zone = order.Zone >= 0 ? $" {run.Zones[order.Zone].PathName}" : "";
                Log.Info("flight", $"stunt race: bot seat {seat} {order.Why}{zone}");
            }
        }
    }

    /// <summary>The live weapon-hit structures <paramref name="zone"/>'s bots shoot: any optional
    /// target beside its gates, then those its route runs into (<see cref="Blocking"/>).</summary>
    internal static List<DestructibleRegistry.Instance> Targets(StuntZone zone, DestructibleRegistry structures,
        Func<Vector3, Vector3, (Node Body, Vector3 At)?> struck)
    {
        var targets = new List<DestructibleRegistry.Instance>();
        if (zone.Route is not { } route)
        {
            return targets;
        }

        foreach (var s in structures.All)
        {
            if (Array.IndexOf(OptionalTargets, s.Def.Name) >= 0 && Live(s, out var at)
                && Mathf.Min(at.DistanceTo(zone.GreenGate.Center), at.DistanceTo(zone.RedGate.Center)) < OptionalReachM)
            {
                targets.Add(s);
            }
        }

        foreach (var s in Blocking(route, structures, struck))
        {
            if (!targets.Contains(s))
            {
                targets.Add(s);
            }
        }

        return targets;
    }

    /// <summary>The live weapon-hit structures <paramref name="route"/> runs into, cast chord by chord
    /// through <paramref name="struck"/>. A struck body belongs to the nearest group up its chain
    /// that anchors one. A collide-hit structure (a facade panel) breaks as a plane flies through,
    /// so it never blocks.</summary>
    internal static List<DestructibleRegistry.Instance> Blocking(DangerZoneRibbon route, DestructibleRegistry structures,
        Func<Vector3, Vector3, (Node Body, Vector3 At)?> struck)
    {
        var across = new List<DestructibleRegistry.Instance>();
        foreach (var body in Struck(route, false, struck))
        {
            if (Owner(body, structures) is { } s && !across.Contains(s))
            {
                across.Add(s);
            }
        }

        return across;
    }

    /// <summary>The first structure <paramref name="route"/> runs into, entered from the end
    /// <paramref name="far"/> names, whose damage node it does not strike first, or null. A hit
    /// counts only on that node, so C2's closed hangar gate shields its propane tank from one side.</summary>
    internal static DestructibleRegistry.Instance? Unseen(DangerZoneRibbon route, bool far, DestructibleRegistry structures,
        Func<Vector3, Vector3, (Node Body, Vector3 At)?> struck)
    {
        var met = new List<DestructibleRegistry.Instance>();
        foreach (var body in Struck(route, far, struck))
        {
            if (Owner(body, structures) is { } s && !met.Contains(s))
            {
                if (structures.Resolve(body) != s)
                {
                    return s;
                }

                met.Add(s);
            }
        }

        return null;
    }

    // Every body a cast along `route` strikes, resuming past each strike. It runs chord by chord as
    // a run from the end `far` names flies them.
    private static IEnumerable<Node> Struck(DangerZoneRibbon route, bool far, Func<Vector3, Vector3, (Node Body, Vector3 At)?> struck)
    {
        for (int c = 0; c < route.Segments.Count; c++)
        {
            int i = far ? route.Segments.Count - 1 - c : c;
            var from = route.PointAt(i, far ? route.Segments[i].Length : 0f);
            var to = route.PointAt(i, far ? 0f : route.Segments[i].Length);
            var step = (to - from).Normalized() * StruckStepM;
            for (int hits = 0; hits < StruckPerChord && struck(from, to) is { } hit; hits++)
            {
                yield return hit.Body;
                from = hit.At + step;
            }
        }
    }

    // The live weapon-hit pool a struck body belongs to: the nearest node up its chain that anchors one.
    private static DestructibleRegistry.Instance? Owner(Node body, DestructibleRegistry structures)
    {
        for (Node? n = body; n != null; n = n.GetParent())
        {
            foreach (var s in structures.PoolsOn(n))
            {
                if (s.Def.Activation.Equals("WeaponHit", StringComparison.OrdinalIgnoreCase) && Live(s, out _))
                {
                    return s;
                }
            }
        }

        return null;
    }

    private static bool Live(DestructibleRegistry.Instance s, out Vector3 at) =>
        TargetPool.TryTargetGeometry(s, out at, out _, out _, out bool live) && live;

    // The table's own ray, cast down from the first zone's entry. A world missing from the physics
    // space refuses no lead-in, and the table would still look sound.
    private static bool SeesWorld(IReadOnlyList<StuntZone> zones, Func<Vector3, Vector3, bool> blocked)
    {
        foreach (var zone in zones)
        {
            if (zone.Route is { } route)
            {
                var entry = route.End(false);
                if (blocked(entry, entry + (Vector3.Down * WorldProbeM)))
                {
                    return true;
                }

                Log.Warn("flight", $"stunt race: the run table's world ray strikes nothing {WorldProbeM:0} m below {zone.PathName}'s entry, so it refused no lead-in");
                return false;
            }
        }

        return false;
    }

    // The live target of `zone` nearest the bot, or null.
    private DestructibleRegistry.Instance? Nearest(FlightController bot, StuntZone zone, DestructibleRegistry structures)
    {
        var at = bot.WorldPosition;
        if (!_targets.TryGetValue(zone, out var near))
        {
            _targets[zone] = near = Targets(zone, structures, bot.WorldStruckAlong);
            foreach (var s in near)
            {
                Log.Info("flight", $"stunt race: bots on {zone.PathName} shoot '{s.Def.Name}' ({s.Def.AnimName})");
            }
        }

        DestructibleRegistry.Instance? best = null;
        float bestSq = float.MaxValue;
        foreach (var s in near)
        {
            if (Live(s, out var pos) && pos.DistanceSquaredTo(at) < bestSq)
            {
                best = s;
                bestSq = pos.DistanceSquaredTo(at);
            }
        }

        return best;
    }

    // An end is shut while a structure its route runs into stands where no round from its entry
    // reaches it. The bot then comes round to the other end, shoots it from there and flies through.
    private bool Shut(DangerZoneRibbon route, bool far)
    {
        if (!EndsShut || !GodotObject.IsInstanceValid(_lead) || _lead.Destructibles is not { } structures)
        {
            return false;
        }

        if (!_unseen.TryGetValue((route, far), out var unseen))
        {
            _unseen[(route, far)] = unseen = Unseen(route, far, structures, _lead.WorldStruckAlong);
            if (unseen != null)
            {
                Log.Info("flight", $"stunt race: no round from {route.Name}'s {(far ? "far" : "near")} end reaches '{unseen.Def.Name}', so bots enter from the other while it stands");
            }
        }

        return unseen != null && Live(unseen, out _);
    }

    // A crash's return puts the bot on the route of the zone it cleared last, often low between
    // walls. It rides that ribbon on out as the route goes, rather than flying free from there.
    private void Rejoin(int seat, FlightController bot, StuntMission run, Vector3 at)
    {
        if (!BotObjective || run.AllComplete || run.ReturnPose() is not { } pose || at.DistanceTo(pose.Position) > 1f)
        {
            return;
        }

        StuntZone? last = null;
        foreach (var zone in run.Zones)
        {
            if (zone.Completed && (last == null || zone.CompletionOrder > last.CompletionOrder))
            {
                last = zone;
            }
        }

        if (last?.Route is { } route && bot.Pilot!.RejoinDangerZone(route, at, bot.GlobalTransform.Basis.Orthonormalized(), bot.WorldVelocity, "returned onto the route"))
        {
            Log.Info("flight", $"stunt race: bot seat {seat} returned onto {route.Name} and rides it out");
        }
    }
}
