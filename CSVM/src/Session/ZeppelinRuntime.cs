using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>
/// Runs a mission's zeppelins (M4 F17, behind <c>--zeppelins</c>): each
/// <see cref="ZeppelinDef"/> whose world node and net resolve gets a
/// <see cref="ZeppelinMotion"/> on B5's <see cref="AiNetFollower"/>, is placed at its authored
/// position/yaw/pitch, and the NODE is flown along the net under the record's limits — an
/// anim/world node moved kinematically, not a FlightController (zeppelins have no flight
/// model). Every place/skip/hold and every node capture prints a <c>zep:</c> line, which is
/// the flag's observability; <c>--debug-ainets=&lt;net&gt;</c> draws the route it flies.
///
/// <para>⚠ Deliberately unwired, each named at its site: a <c>deactivated</c> record is placed
/// but HELD (mission script would activate it; out of M4's scope); engine loss is live through
/// <see cref="ZeppelinMotion.AliveEngines"/> but has no caller until F18's damage aggregator;
/// stop nodes are NOT implemented — the per-node tags ride along raw because their encoding is
/// still undecoded (F17's open item). Effect templates snap to absolute world points and do not
/// track a moving host, so a hit effect on a flying zeppelin stays where the hit happened —
/// F18/F19 inherit that constraint; nothing here fights it.</para>
/// </summary>
public sealed partial class ZeppelinRuntime : Node
{
    private readonly List<LiveZeppelin> _live = new();
    private float _sinceLog;

    public ZeppelinRuntime(IReadOnlyList<ZeppelinDef> defs, Func<string, Node3D?> resolveNode,
        IReadOnlyList<AiNet> chapterNets)
    {
        Name = "zeppelins";
        foreach (var def in defs)
        {
            var host = resolveNode(def.Node);
            if (host == null)
            {
                GD.Print($"zep: '{def.Node}' skipped: world node unresolved");
                continue;
            }
            var net = AiNets.ByName(chapterNets, def.Net);
            if (net == null)
            {
                // Still placed: the authored pose is real even without a route (and B6's
                // generator altitude gate reads the node's live Y).
                Place(host, def.Position, Mathf.DegToRad(def.YawDeg), Mathf.DegToRad(def.PitchDeg));
                GD.Print($"zep: '{def.Node}' placed but held: net '{def.Net}' not in neindex");
                continue;
            }
            // The capture radius must clear the turning circle (v/ω plus headroom for the
            // rate ramp-in), or a slow wide zeppelin orbits a node forever; same invented-
            // radius caveat as AiNetFollower.DefaultArrivalRadius.
            float turnCircle = def.MaxSpeed / Mathf.Max(Mathf.DegToRad(def.MaxRateYawDeg), 1e-3f);
            float arrival = Mathf.Max(AiNetFollower.DefaultArrivalRadius, 1.5f * turnCircle);
            var follower = new AiNetFollower(net, Rng.NewSystemRandom(Rng.Ai), arrival);
            var motion = new ZeppelinMotion(def, follower);
            Place(host, motion.Position, motion.YawRad, motion.PitchRad);
            _live.Add(new LiveZeppelin(def, motion, host));
            GD.Print($"zep: '{def.Node}' placed at ({def.Position.X:0},{def.Position.Y:0}," +
                     $"{def.Position.Z:0}) on net '{net.Name}' ({net.Nodes.Count} nodes), " +
                     $"max_speed {def.MaxSpeed:0.#} m/s, engines {motion.TotalEngines}" +
                     (def.Deactivated ? " — deactivated, holding" : ""));
        }
    }

    /// <summary>Zeppelins placed on a resolved net (a held <c>deactivated</c> one counts — it
    /// is placed and would fly when a script layer wakes it).</summary>
    public int LiveCount => _live.Count;

    /// <summary>The live motions by node name, the F18 seam's lookup (damage writes
    /// <see cref="ZeppelinMotion.AliveEngines"/>).</summary>
    public ZeppelinMotion? MotionFor(string node)
    {
        foreach (var zep in _live)
        {
            if (zep.Def.Node.Equals(node, StringComparison.OrdinalIgnoreCase))
            {
                return zep.Motion;
            }
        }
        return null;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = GameClock.Current?.PhysicsDt(delta) ?? (float)delta;
        if (dt <= 0f)
        {
            return;   // the session drives SimStep itself this frame (see GameClock.PhysicsDt)
        }
        SimStep(dt);
    }

    /// <summary>One step of every active motion, written onto the world nodes. Public for the
    /// same reason the generators' is: a fixed or halted clock has the session drive it.</summary>
    public void SimStep(float dt)
    {
        _sinceLog += dt;
        bool log = _sinceLog >= 10f;
        if (log)
        {
            _sinceLog = 0f;
        }
        foreach (var zep in _live)
        {
            if (zep.Def.Deactivated)
            {
                continue;   // placed, holding for a mission-script wake-up (out of M4 scope)
            }
            int before = zep.Motion.Follower.CurrentIndex;
            zep.Motion.Step(dt);
            Place(zep.Host, zep.Motion.Position, zep.Motion.YawRad, zep.Motion.PitchRad);
            if (zep.Motion.Follower.CurrentIndex != before && before >= 0)
            {
                GD.Print($"zep: '{zep.Def.Node}' captured node {before}, next " +
                         $"{zep.Motion.Follower.CurrentIndex} of '{zep.Motion.Follower.Net.Name}'");
            }
            if (log)
            {
                var p = zep.Motion.Position;
                GD.Print($"zep: '{zep.Def.Node}' at ({p.X:0},{p.Y:0},{p.Z:0}) " +
                         $"speed {zep.Motion.Speed:0.#}/{zep.Motion.EffectiveMaxSpeed:0.#} m/s " +
                         $"toward node {zep.Motion.Follower.CurrentIndex}");
            }
        }
    }

    // Yaw about Y (mission convention, 0 = −Z) then pitch about X; zeppelins never bank.
    private static void Place(Node3D host, Vector3 position, float yawRad, float pitchRad)
    {
        host.GlobalTransform = new Transform3D(
            Basis.FromEuler(new Vector3(pitchRad, yawRad, 0f)), position);
    }

    private sealed class LiveZeppelin
    {
        public LiveZeppelin(ZeppelinDef def, ZeppelinMotion motion, Node3D host)
        {
            Def = def;
            Motion = motion;
            Host = host;
        }

        public ZeppelinDef Def { get; }

        public ZeppelinMotion Motion { get; }

        public Node3D Host { get; }
    }
}
