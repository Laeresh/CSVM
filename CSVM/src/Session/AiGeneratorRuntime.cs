using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>
/// Runs a mission's enemy generators (M4 B6): each loaded <see cref="EnemyGeneratorDef"/> gets a
/// <see cref="GeneratorCycle"/> and spawns AI aircraft through the session's runtime spawn seam
/// (<c>GameSession.SpawnAiAircraft</c>) as its waves come due. Load-time semantics follow the
/// decode (docs/formats/mission-entities.md "The generator cycle"): a generator whose host node
/// does not resolve, or whose ENTIRE nets list fails to resolve, is dropped at load, never
/// loaded inert. Every load drop and every spawn prints an <c>egen:</c> line, which is the
/// <c>--generators</c> flag's observability.
///
/// <para>⚠ Stand-ins until later waves land, each named at its site: the host altitude for the
/// <c>min_altitude</c> gate is the host NODE's live world Y (correct today while zeppelins are
/// static, and it keeps tracking the node once F17 moves them); HOST DEATH is stubbed (nothing
/// can kill a zeppelin or the submarine's <c>subhealthy</c> node yet, so
/// <see cref="GeneratorCycle.HostDied"/> has no caller until F18/F20 wire the real death sources);
/// and the DOOR choreography is skipped entirely (F20, it needs a zeppelin model; the hardcoded
/// timings wait as <see cref="GeneratorCycle"/> constants). Spawned aircraft patrol their
/// generator's cyclic net pick through <see cref="AiNetFollower"/> (B5).</para>
/// </summary>
public sealed partial class AiGeneratorRuntime : Node
{
    /// <summary>The most recent spawn's net pick, per generator node.</summary>
    public readonly Dictionary<string, string> SpawnedNet = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<LiveGenerator> _live = new();
    private readonly string _planeName;
    private readonly Func<string, Vector3, Vector3, AiPilot, FlightController?> _spawn;

    public AiGeneratorRuntime(IReadOnlyList<EnemyGeneratorDef> defs, AnimRuntime? worldRuntime,
        IReadOnlyList<AiNet> chapterNets, string planeName,
        Func<string, Vector3, Vector3, AiPilot, FlightController?> spawn)
    {
        Name = "ai_generators";
        _planeName = planeName;
        _spawn = spawn;
        foreach (var def in defs)
        {
            // Load-drop 1: unresolved host node (decoded rule: dropped, not loaded inert).
            Node3D? host = worldRuntime?.FindNodes(def.Node) is { Count: > 0 } hits ? hits[0] : null;
            if (host == null)
            {
                GD.Print($"egen: generator '{def.Node}' dropped: host node unresolved" +
                         (worldRuntime == null ? " (no world runtime on this stage)" : ""));
                continue;
            }
            // Load-drop 2: none of the nets resolve against the chapter's neindex names.
            var nets = new List<AiNet>();
            foreach (var net in def.Nets)
            {
                if (AiNets.ByName(chapterNets, net) is { } resolved)
                {
                    nets.Add(resolved);
                }
            }
            if (nets.Count == 0)
            {
                GD.Print($"egen: generator '{def.Node}' dropped: no net of " +
                         $"[{string.Join(", ", def.Nets)}] resolves");
                continue;
            }
            // The zeppelin drop point; a miss falls back to the host node (not a drop condition).
            Node3D? origin = def.Origin != null
                && worldRuntime!.FindNodes(def.Origin, host) is { Count: > 0 } o ? o[0] : null;
            _live.Add(new LiveGenerator(def, new GeneratorCycle(def), host, origin, nets));
            string kind = def.IsZeppelin ? "zeppelin launch" : def.MovingPath ? "moving spawner" : "spawner";
            var netNames = new List<string>(nets.Count);
            foreach (var n in nets)
            {
                netNames.Add(n.Name);
            }
            GD.Print($"egen: generator '{def.Node}' live ({kind}), " +
                     $"wave {def.WaveSize} every {def.IndPeriod + def.WavePeriod:0.#} s (ind {def.IndPeriod:0.#} + wave {def.WavePeriod:0.#}), " +
                     $"max_active {def.MaxActive}, nets [{string.Join(", ", netNames)}]" +
                     (def.MinAltitude is { } gate ? $", launch gate {gate:0} m" : ""));
        }
    }

    /// <summary>Generators that survived the load drops.</summary>
    public int LiveCount => _live.Count;

    public override void _PhysicsProcess(double delta)
    {
        float dt = GameClock.Current?.PhysicsDt(delta) ?? (float)delta;
        if (dt <= 0f)
        {
            return;   // the session drives SimStep itself this frame (see GameClock.PhysicsDt)
        }
        SimStep(dt);
    }

    /// <summary>One step of every live cycle. Public for the same reason the pool's is: a fixed
    /// or halted clock has the session drive it.</summary>
    public void SimStep(float dt)
    {
        foreach (var gen in _live)
        {
            // Host death is a stub: no death source exists for these hosts until F18/F20, so
            // gen.Cycle.HostDied() is never called yet. The altitude read is live, though;
            // the min_altitude gate holds (not cancels) whenever the host node sits below it.
            if (gen.Cycle.Step(dt, gen.Host.GlobalPosition.Y))
            {
                Spawn(gen);
            }
        }
    }

    private void Spawn(LiveGenerator gen)
    {
        var anchor = gen.Origin ?? gen.Host;
        var pos = anchor.GlobalPosition;
        var forward = -gen.Host.GlobalTransform.Basis.Z;
        forward.Y = 0f;
        forward = forward.LengthSquared() > 1e-6f ? forward.Normalized() : Vector3.Forward;

        // The cyclic net pick (choose_nets is cyclic on every authored file; 'random' falls back
        // to cyclic here until something authors it). The spawned pilot patrols it.
        var net = gen.Nets[gen.NetCursor % gen.Nets.Count];
        gen.NetCursor++;
        SpawnedNet[gen.Def.Node] = net.Name;

        var pilot = AiPilot.HoldingCourse(pos, pos + forward);
        pilot.Throttle = AiPilot.PatrolThrottle;
        pilot.Patrol = new AiNetFollower(net, Rng.NewSystemRandom(Rng.Ai));
        var controller = _spawn(_planeName, pos, pos + forward, pilot);
        if (controller == null)
        {
            gen.Cycle.SpawnRemoved();   // the slot was counted before the spawn could fail
            return;
        }
        gen.SpawnCount++;
        controller.Downed += (_, _) => gen.Cycle.SpawnRemoved();
        GD.Print($"egen: '{gen.Def.Node}' spawn #{gen.SpawnCount}: '{_planeName}' patrolling " +
                 $"net '{net.Name}', active {gen.Cycle.Active}/{gen.Def.MaxActive}" +
                 (gen.Def.VehicleParams != null ? $", params '{gen.Def.VehicleParams}'" : ""));
    }

    private sealed class LiveGenerator
    {
        public LiveGenerator(EnemyGeneratorDef def, GeneratorCycle cycle, Node3D host,
            Node3D? origin, List<AiNet> nets)
        {
            Def = def;
            Cycle = cycle;
            Host = host;
            Origin = origin;
            Nets = nets;
        }

        public EnemyGeneratorDef Def { get; }

        public GeneratorCycle Cycle { get; }

        public Node3D Host { get; }

        public Node3D? Origin { get; }

        public List<AiNet> Nets { get; }

        public int NetCursor { get; set; }

        public int SpawnCount { get; set; }
    }
}
