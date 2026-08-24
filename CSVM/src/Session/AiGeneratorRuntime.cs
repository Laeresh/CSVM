using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>Runs a mission's enemy generators (M4 B6 + F20): each loaded
/// <see cref="EnemyGeneratorDef"/> gets a <see cref="GeneratorCycle"/> and spawns AI aircraft
/// through <c>GameSession.SpawnAiAircraft</c> as its waves come due, dropping at the origin
/// node's live position in the authored drop attitude and patrolling the cyclic net pick through
/// <see cref="AiNetFollower"/>. A generator whose host or whole nets list fails to resolve is
/// dropped at load, never loaded inert (docs/formats/mission-entities.md). Every load drop, door
/// transition and spawn prints an <c>egen:</c> line.
/// <see cref="GeneratorCycle.DoorOpen"/> drives the authored door anims; unauthored doors run the
/// timing machine log-only. <see cref="NotifyHostDied"/> is F18's seam: the dead host's
/// generators disable permanently.
/// ⚠ <see cref="UseInstantActionLaunches"/>/<see cref="GrantWaveCapacity"/> are F12's seam — see
/// this module's entry in docs/architecture.md before touching either.</summary>
public sealed partial class AiGeneratorRuntime : Node
{
    /// <summary>The most recent spawn's net pick, per generator node.</summary>
    public readonly Dictionary<string, string> SpawnedNet = new(StringComparer.OrdinalIgnoreCase);

    // The spawn basis needs a horizontal component (Basis.LookingAt with world up),
    // so the authored −90° drop pitch is clamped this far shy of vertical. Invented margin;
    // the −90° drop attitude itself is the authored `rotation`.
    private const float MaxDropPitchDeg = 80f;

    // FUN_00452450 preserves the carrier's world velocity, then adds this vertical component.
    private const float CarrierLaunchDownwardSpeed = 22.352f;

    private readonly List<LiveGenerator> _live = new();
    private readonly string _planeName;
    private readonly Func<string, Vector3, Vector3, AiPilot, FlightController?> _spawn;
    private readonly Func<string, Node3D, int>? _playAnim;
    private readonly Action<string, Node3D>? _stopAnim;
    private readonly Func<AiNet, Func<Vector3?>?>? _trailerTarget;

    /// <param name="trailerTarget">Where an anchored net's trailer target is, per net;
    /// null leaves every generated patroller on its net's authored coordinates. One generator's
    /// nets are player-anchored in the shipped data (C5's <c>M4Miles</c>), so this is not
    /// hypothetical.</param>
    public AiGeneratorRuntime(IReadOnlyList<EnemyGeneratorDef> defs,
        Func<string, Node3D?, Node3D?>? resolveNode, IReadOnlyList<AiNet> chapterNets,
        string planeName, Func<string, Vector3, Vector3, AiPilot, FlightController?> spawn,
        Func<string, Node3D, int>? playAnim = null, Action<string, Node3D>? stopAnim = null,
        Func<AiNet, Func<Vector3?>?>? trailerTarget = null)
    {
        Name = "ai_generators";
        _planeName = planeName;
        _spawn = spawn;
        _playAnim = playAnim;
        _stopAnim = stopAnim;
        _trailerTarget = trailerTarget;
        foreach (var def in defs)
        {
            // Load-drop 1: unresolved host node (decoded rule: dropped, not loaded inert).
            Node3D? host = resolveNode?.Invoke(def.Node, null);
            if (host == null)
            {
                GD.Print($"egen: generator '{def.Node}' dropped: host node unresolved" +
                         (resolveNode == null ? " (no world runtime on this stage)" : ""));
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
            Node3D? origin = def.Origin != null ? resolveNode!(def.Origin, host) : null;
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
                     (def.MinAltitude is { } gate ? $", launch gate {gate:0} m" : "") +
                     (def.OpenAnim != null ? $", doors '{def.OpenAnim}'/'{def.CloseAnim}'"
                         : ", doors unnamed (timing runs log-only)"));
        }
    }

    /// <summary>Generators that survived the load drops.</summary>
    public int LiveCount => _live.Count;

    /// <summary>Instant Action's zeppelin arm: every generator hosted on
    /// <paramref name="hostNode"/> goes onto the wave-credit budget and releases an already-built
    /// wave member through <paramref name="release"/> instead of spawning a fresh aircraft — the
    /// decoded shape (this module's entry in docs/architecture.md). A null release is accounted
    /// like a failed spawn. Returns how many generators this claimed; 0 means the selected
    /// zeppelin carries no generator, so nothing on that mode will ever launch.</summary>
    public int UseInstantActionLaunches(string hostNode,
        Func<Vector3, Vector3, Vector3, FlightController?> release)
    {
        int claimed = 0;
        foreach (var gen in _live)
        {
            if (!gen.Def.Node.Equals(hostNode, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            gen.Release = release;
            gen.Cycle.UseWaveCredits();
            claimed++;
        }
        return claimed;
    }

    /// <summary>The decoded per-wave top-up (<c>FUN_0045b9d0</c>'s type-2 arm, F12): adds
    /// <paramref name="count"/> to the remaining capacity of every generator hosted on
    /// <paramref name="hostNode"/>. Returns how many generators were credited. ⚠ The original's
    /// counter advances whether or not the top-up lands, so a wave whose zeppelin has no generator
    /// is simply lost — the caller reports it rather than compensating.</summary>
    public int GrantWaveCapacity(string hostNode, int count)
    {
        int fed = 0;
        foreach (var gen in _live)
        {
            if (!gen.Def.Node.Equals(hostNode, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            gen.Cycle.GrantCapacity(count);
            fed++;
        }
        return fed;
    }

    /// <summary>The named host died: permanently disable every generator whose host node (or
    /// authored <c>healthy</c> node, the submarine's) carries this name — the decoded rule.
    /// Fed by <c>ZeppelinRuntime.ZeppelinKilled</c> (F18); fixed-installation hosts still have
    /// no death source. The door keeps its last state: the decoded loop early-outs a disabled
    /// generator before any door rule runs. Returns how many generators this disabled.</summary>
    public int NotifyHostDied(string nodeName)
    {
        int disabled = 0;
        foreach (var gen in _live)
        {
            if (gen.Cycle.Disabled
                || (!gen.Def.Node.Equals(nodeName, StringComparison.OrdinalIgnoreCase)
                    && !(gen.Def.HealthyNode?.Equals(nodeName, StringComparison.OrdinalIgnoreCase) ?? false)))
            {
                continue;
            }
            gen.Cycle.HostDied();
            disabled++;
            GD.Print($"egen: generator '{gen.Def.Node}' disabled permanently: host died");
        }
        return disabled;
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

    /// <summary>One step of every live cycle, door transitions included. Public for the same
    /// reason the pool's is: a fixed or halted clock has the session drive it.</summary>
    public void SimStep(float dt)
    {
        foreach (var gen in _live)
        {
            var hostPosition = gen.Host.GlobalPosition;
            gen.HostVelocity = dt > 0f ? (hostPosition - gen.LastHostPosition) / dt : Vector3.Zero;
            gen.LastHostPosition = hostPosition;
            // The altitude read is live off the host node, so the min_altitude gate holds (not
            // cancels) whenever F17's flown zeppelin sits below it.
            bool spawned = gen.Cycle.Step(dt, hostPosition.Y);
            if (gen.Cycle.DoorOpen != gen.DoorOpen)
            {
                gen.DoorOpen = gen.Cycle.DoorOpen;
                PlayDoor(gen, opening: gen.DoorOpen);
            }
            if (spawned)
            {
                Spawn(gen);
            }
        }
    }

    private void PlayDoor(LiveGenerator gen, bool opening)
    {
        string? anim = opening ? gen.Def.OpenAnim : gen.Def.CloseAnim;
        string what = opening ? "open" : "close";
        if (anim == null)
        {
            GD.Print($"egen: '{gen.Def.Node}' door {what} (no authored anim)");
            return;
        }
        // Stop the opposite motion first: a fast cycle can otherwise leave both from-to motions
        // writing. Hooks are scoped to the host's subtree — C1 has three 'hangerdoors' namesakes.
        string? other = opening ? gen.Def.CloseAnim : gen.Def.OpenAnim;
        if (other != null && other != anim)
        {
            _stopAnim?.Invoke(other, gen.Host);
        }
        int started = _playAnim?.Invoke(anim, gen.Host) ?? 0;
        GD.Print($"egen: '{gen.Def.Node}' door {what} (anim '{anim}'" +
                 (started > 0 ? $", {started} instance(s))" : ", not in this program)"));
    }

    // pos/drop below are computed once and shared by the release and net-pick branches; the
    // F12 zeppelin arm must not re-derive its own drop point.
    private void Spawn(LiveGenerator gen)
    {
        var anchor = gen.Origin ?? gen.Host;
        var pos = anchor.GlobalPosition;
        Vector3? launchVelocity = gen.Def.IsZeppelin
            ? gen.HostVelocity + Vector3.Down * CarrierLaunchDownwardSpeed
            : null;
        var forward = -gen.Host.GlobalTransform.Basis.Z;
        forward.Y = 0f;
        forward = forward.LengthSquared() > 1e-6f ? forward.Normalized() : Vector3.Forward;

        // The authored drop attitude: rotation's pitch about the host's right axis, clamped shy
        // of vertical so Basis.LookingAt keeps a horizontal component.
        var drop = forward;
        if (gen.Def.RotationDeg is { } rot && Mathf.Abs(rot.X) > 0.01f)
        {
            float pitch = Mathf.DegToRad(Mathf.Clamp(rot.X, -MaxDropPitchDeg, MaxDropPitchDeg));
            // Rotating about the host's RIGHT axis (forward × up) by a negative angle pitches
            // the vector down, matching the authored sign (−90 = drop).
            drop = forward.Rotated(forward.Cross(Vector3.Up).Normalized(), pitch).Normalized();
        }

        // Instant Action's zeppelin arm (F12): release a parked wave member instead of building a
        // new aircraft. No net pick — a released member is a wave enemy carrying its own
        // primary_target, not a generator-authored patroller.
        if (gen.Release is { } release)
        {
            var released = release(pos, pos + drop, launchVelocity ?? Vector3.Zero);
            if (released == null)
            {
                gen.Cycle.SpawnRemoved();   // the slot was counted before the release could fail
                GD.Print($"egen: '{gen.Def.Node}' launch skipped: no parked wave member left");
                return;
            }
            gen.SpawnCount++;
            released.Downed += (_, _) => gen.Cycle.SpawnRemoved();
            GD.Print($"egen: '{gen.Def.Node}' launch #{gen.SpawnCount}: IA wave member '" +
                     $"{released.Name}' released at ({pos.X:0},{pos.Y:0},{pos.Z:0}), " +
                     $"active {gen.Cycle.Active}/{gen.Def.MaxActive}, " +
                     $"{gen.Cycle.CapacityRemaining} of this wave's credit left");
            return;
        }

        // The cyclic net pick (choose_nets is cyclic on every authored file; 'random' falls back
        // to cyclic here until something authors it). The spawned pilot patrols it.
        var net = gen.Nets[gen.NetCursor % gen.Nets.Count];
        gen.NetCursor++;
        SpawnedNet[gen.Def.Node] = net.Name;

        var pilot = AiPilot.HoldingCourse(pos, pos + forward);
        pilot.Patrol = new AiNetFollower(net, Rng.NewSystemRandom(Rng.Ai),
            trailerTarget: _trailerTarget?.Invoke(net));
        var controller = _spawn(_planeName, pos, pos + drop, pilot);
        if (controller == null)
        {
            gen.Cycle.SpawnRemoved();   // the slot was counted before the spawn could fail
            return;
        }
        if (launchVelocity is { } velocity)
            controller.Activate(pos, pos + drop, velocity, carrierDrop: true);
        gen.SpawnCount++;
        controller.Downed += (_, _) => gen.Cycle.SpawnRemoved();
        GD.Print($"egen: '{gen.Def.Node}' spawn #{gen.SpawnCount}: '{_planeName}' dropped at " +
                 $"({pos.X:0},{pos.Y:0},{pos.Z:0}) patrolling net '{net.Name}', " +
                 $"active {gen.Cycle.Active}/{gen.Def.MaxActive}" +
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
            LastHostPosition = host.GlobalPosition;
        }

        public EnemyGeneratorDef Def { get; }

        public GeneratorCycle Cycle { get; }

        public Node3D Host { get; }

        public Node3D? Origin { get; }

        public Vector3 LastHostPosition { get; set; }

        public Vector3 HostVelocity { get; set; }

        public List<AiNet> Nets { get; }

        /// <summary>F12's Instant Action launch hook: non-null once
        /// <see cref="AiGeneratorRuntime.UseInstantActionLaunches"/> has claimed this generator,
        /// and then it replaces the plane spawn entirely.</summary>
        public Func<Vector3, Vector3, Vector3, FlightController?>? Release { get; set; }

        public int NetCursor { get; set; }

        public int SpawnCount { get; set; }

        public bool DoorOpen { get; set; }
    }
}
