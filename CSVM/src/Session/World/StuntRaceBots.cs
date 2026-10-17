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

    // Each zone's targets, found on its first run.
    private readonly Dictionary<StuntZone, List<DestructibleRegistry.Instance>> _obstacles = new();

    private StuntRaceBots(StuntRace race, IReadOnlyList<PlayerRig> seats, List<(int, StuntBotObjective)> bots, bool runsSawWorld)
    {
        _race = race;
        _seats = seats;
        _bots = bots;
        RunsSawWorld = runsSawWorld;
    }

    /// <summary>Whether the bots take their objectives. Off, each holds its course and flies no zone;
    /// a suite's control turns it off.</summary>
    public bool BotObjective { get; set; } = true;

    /// <summary>Whether the world ray the run table was decided with struck the built world below the
    /// first zone's entry when the table was filled. A ray that strikes nothing refuses no end.</summary>
    public bool RunsSawWorld { get; }

    /// <summary>One objective per bot seat flown here with a run of the course, its tier read off
    /// <paramref name="roster"/>, every fluff drawn on one seeded stream. Null with none. Every zone
    /// end's run is decided here, once for every bot, against the built world.</summary>
    public static StuntRaceBots? For(StuntRace race, IReadOnlyList<PlayerRig> seats, IReadOnlyList<NetSeat> roster)
    {
        var rng = new Random(Rng.IntSeedFor(Rng.StuntBots));
        var bots = new List<(int, StuntBotObjective)>();
        StuntBotObjective.RunTable? runs = null;
        bool sawWorld = false;
        for (int seat = 0; seat < seats.Count && seat < roster.Count; seat++)
        {
            if (seats[seat].Controller is { IsBotSeat: true, Pilot: { } pilot, Stunt: { } run } bot)
            {
                // Every run's zones share the course's routes, so one bot's run keys the table for all.
                if (runs == null)
                {
                    Func<Vector3, Vector3, bool> blocked = bot.WorldBlocksLine;
                    long mark = StartupProfile.Mark();
                    runs = StuntBotObjective.RunsFrom(run.Zones, blocked);
                    StartupProfile.Record("lead_ins", mark);
                    sawWorld = SeesWorld(run.Zones, blocked);
                }

                var objective = new StuntBotObjective(roster[seat].Skill, rng) { Runs = runs };
                pilot.CruiseThrottle = objective.CruiseThrottle;
                bots.Add((seat, objective));
            }
        }

        return bots.Count == 0 ? null : new StuntRaceBots(race, seats, bots, sawWorld);
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

            // On a run the gunner takes the zone's nearest target. Off one it holds no target and
            // never fires, since a race's rounds have nothing else to strike.
            if (pilot.Gunner is { } gunner)
            {
                gunner.Target = pilot.ZoneRun is { } zoneRun && bot.Destructibles is { } structures
                    ? Obstacle(bot, run, zoneRun.Ribbon.Name, structures) : null;
            }

            if (_log.Admit(seat, order.Why))
            {
                string zone = order.Zone >= 0 ? $" {run.Zones[order.Zone].PathName}" : "";
                Log.Info("flight", $"stunt race: bot seat {seat} {order.Why}{zone}");
            }
        }
    }

    /// <summary>The live weapon-hit structures <paramref name="zone"/>'s bots shoot: those its route
    /// runs into, cast chord by chord through <paramref name="struck"/>, then any optional target
    /// beside its gates. A struck body belongs to the nearest group up its chain that anchors one.
    /// A collide-hit structure (a facade panel) breaks as a plane flies through, so never blocks.</summary>
    internal static List<DestructibleRegistry.Instance> Targets(StuntZone zone, DestructibleRegistry structures,
        Func<Vector3, Vector3, (Node Body, Vector3 At)?> struck)
    {
        var across = new List<DestructibleRegistry.Instance>();
        if (zone.Route is not { } route)
        {
            return across;
        }

        foreach (var s in structures.All)
        {
            if (Array.IndexOf(OptionalTargets, s.Def.Name) >= 0 && Live(s, out var at)
                && Mathf.Min(at.DistanceTo(zone.GreenGate.Center), at.DistanceTo(zone.RedGate.Center)) < OptionalReachM)
            {
                across.Add(s);
            }
        }

        for (int i = 0; i < route.Segments.Count; i++)
        {
            var from = route.PointAt(i, 0f);
            var to = route.PointAt(i, route.Segments[i].Length);
            var step = (to - from).Normalized() * StruckStepM;
            for (int hits = 0; hits < StruckPerChord && struck(from, to) is { } hit; hits++)
            {
                if (Owner(hit.Body, structures) is { } s && !across.Contains(s))
                {
                    across.Add(s);
                }

                from = hit.At + step;
            }
        }

        return across;
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

    // The live obstacle nearest the bot on the route of the zone named, or null.
    private DestructibleRegistry.Instance? Obstacle(FlightController bot, StuntMission run, string pathName, DestructibleRegistry structures)
    {
        var at = bot.WorldPosition;
        StuntZone? zone = null;
        foreach (var z in run.Zones)
        {
            if (z.PathName == pathName)
            {
                zone = z;
            }
        }

        if (zone == null)
        {
            return null;
        }

        if (!_obstacles.TryGetValue(zone, out var near))
        {
            _obstacles[zone] = near = Targets(zone, structures, bot.WorldStruckAlong);
            foreach (var s in near)
            {
                Log.Info("flight", $"stunt race: bots on {pathName} shoot '{s.Def.Name}' ({s.Def.AnimName})");
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
