using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>
/// Runs a mission's enemy generators (M4 B6 + F20): each loaded <see cref="EnemyGeneratorDef"/>
/// gets a <see cref="GeneratorCycle"/> and spawns AI aircraft through the session's runtime
/// spawn seam (<c>GameSession.SpawnAiAircraft</c>) as its waves come due. Load-time semantics
/// follow the decode (docs/formats/mission-entities.md "The generator cycle"): a generator whose
/// host node does not resolve, or whose ENTIRE nets list fails to resolve, is dropped at load,
/// never loaded inert. Every load drop, door transition and spawn prints an <c>egen:</c> line,
/// which is the <c>--generators</c> flag's observability.
///
/// <para><b>The hangar door (F20).</b> <see cref="GeneratorCycle.DoorOpen"/> drives the authored
/// <c>open_anim</c>/<c>close_anim</c> mission animations (OnCall defs over the zeppelin's
/// <c>door_left</c>/<c>door_right</c> nodes) through the play/stop hooks; a transition whose
/// anim the program does not carry still logs, so the timing law stays observable. A generator
/// authoring neither name runs the timing machine log-only — the engine would default
/// <c>&lt;node&gt;_open_&lt;nn&gt;</c>/<c>close_&lt;nn&gt;</c>, but no such def ships for any
/// non-zeppelin host in this install, so that default is not reproduced.</para>
///
/// <para><b>The F18 seam:</b> <see cref="NotifyHostDied"/> — the zeppelin death aggregator (or
/// the submarine's <c>healthy</c>-node death) calls it with the dead node's name and the
/// matching generators disable permanently (decoded rule). Nothing calls it until F18 lands.</para>
///
/// <para>Spawned aircraft drop at the origin node's LIVE position (it rides F17's moving
/// zeppelin) in the authored drop attitude — <c>rotation</c>'s pitch, clamped shy of vertical
/// so the spawn basis stays valid — and patrol their generator's cyclic net pick through
/// <see cref="AiNetFollower"/> (B5).</para>
/// </summary>
public sealed partial class AiGeneratorRuntime : Node
{
    /// <summary>The most recent spawn's net pick, per generator node.</summary>
    public readonly Dictionary<string, string> SpawnedNet = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The spawn basis needs a horizontal component (Basis.LookingAt with world up),
    /// so the authored −90° drop pitch is clamped this far shy of vertical. Invented margin;
    /// the −90° drop attitude itself is the authored <c>rotation</c>.</summary>
    private const float MaxDropPitchDeg = 80f;

    /// <summary>INVENTED clearance below the origin node. The authored <c>cargobay</c> sits on
    /// the bay floor inside the hull, so an airframe spawned exactly there overlaps the bay
    /// geometry and crashes on frame one (measured: C1B/M03's drop dies into the vostok's own
    /// <c>g459</c>). The binary carries two untraced launch timers (BL-350 trap b) that are NOT
    /// interpreted here; instead the fighter appears this far straight below the doors, in
    /// open air under the hull.</summary>
    private const float DropClearanceM = 12f;

    private readonly List<LiveGenerator> _live = new();
    private readonly string _planeName;
    private readonly Func<string, Vector3, Vector3, AiPilot, FlightController?> _spawn;
    private readonly Func<string, Node3D, int>? _playAnim;
    private readonly Action<string, Node3D>? _stopAnim;

    public AiGeneratorRuntime(IReadOnlyList<EnemyGeneratorDef> defs,
        Func<string, Node3D?, Node3D?>? resolveNode, IReadOnlyList<AiNet> chapterNets,
        string planeName, Func<string, Vector3, Vector3, AiPilot, FlightController?> spawn,
        Func<string, Node3D, int>? playAnim = null, Action<string, Node3D>? stopAnim = null)
    {
        Name = "ai_generators";
        _planeName = planeName;
        _spawn = spawn;
        _playAnim = playAnim;
        _stopAnim = stopAnim;
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
                         : ", doors unauthored (timing runs log-only)"));
        }
    }

    /// <summary>Generators that survived the load drops.</summary>
    public int LiveCount => _live.Count;

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
            // Host death arrives through NotifyHostDied (zeppelin hosts, via F18's kill
            // event); fixed-installation hosts still have no death source. The altitude read
            // is live off the host node, so the min_altitude gate holds (not cancels)
            // whenever F17's flown zeppelin sits below it.
            bool spawned = gen.Cycle.Step(dt, gen.Host.GlobalPosition.Y);
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
        // Stop the opposite motion first: the 4 s minimum-open sits inside the 5 s authored
        // door travel, so a fast cycle can otherwise leave both from-to motions writing. Both
        // hooks are scoped to the HOST's subtree — C1 has three 'hangerdoors' namesakes and
        // only the zeppelin's own may swing.
        string? other = opening ? gen.Def.CloseAnim : gen.Def.OpenAnim;
        if (other != null)
        {
            _stopAnim?.Invoke(other, gen.Host);
        }
        int started = _playAnim?.Invoke(anim, gen.Host) ?? 0;
        GD.Print($"egen: '{gen.Def.Node}' door {what} (anim '{anim}'" +
                 (started > 0 ? $", {started} instance(s))" : ", not in this program)"));
    }

    private void Spawn(LiveGenerator gen)
    {
        var anchor = gen.Origin ?? gen.Host;
        var pos = anchor.GlobalPosition;
        if (gen.Def.IsZeppelin)
        {
            pos += Vector3.Down * DropClearanceM;   // clear the bay floor + door swing
        }
        var forward = -gen.Host.GlobalTransform.Basis.Z;
        forward.Y = 0f;
        forward = forward.LengthSquared() > 1e-6f ? forward.Normalized() : Vector3.Forward;

        // The authored drop attitude: rotation's pitch (−90° = straight down on all 17
        // zeppelin generators) about the host's right axis, clamped shy of vertical so
        // Basis.LookingAt keeps a horizontal component. The plane dives out of the bay and
        // the pilot's own law pulls it onto its net.
        var drop = forward;
        if (gen.Def.RotationDeg is { } rot && Mathf.Abs(rot.X) > 0.01f)
        {
            float pitch = Mathf.DegToRad(Mathf.Clamp(rot.X, -MaxDropPitchDeg, MaxDropPitchDeg));
            // Rotating about the host's RIGHT axis (forward × up) by a negative angle pitches
            // the vector down, matching the authored sign (−90 = drop).
            drop = forward.Rotated(forward.Cross(Vector3.Up).Normalized(), pitch).Normalized();
        }

        // The cyclic net pick (choose_nets is cyclic on every authored file; 'random' falls back
        // to cyclic here until something authors it). The spawned pilot patrols it.
        var net = gen.Nets[gen.NetCursor % gen.Nets.Count];
        gen.NetCursor++;
        SpawnedNet[gen.Def.Node] = net.Name;

        var pilot = AiPilot.HoldingCourse(pos, pos + forward);
        pilot.Throttle = AiPilot.PatrolThrottle;
        pilot.Patrol = new AiNetFollower(net, Rng.NewSystemRandom(Rng.Ai));
        var controller = _spawn(_planeName, pos, pos + drop, pilot);
        if (controller == null)
        {
            gen.Cycle.SpawnRemoved();   // the slot was counted before the spawn could fail
            return;
        }
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
        }

        public EnemyGeneratorDef Def { get; }

        public GeneratorCycle Cycle { get; }

        public Node3D Host { get; }

        public Node3D? Origin { get; }

        public List<AiNet> Nets { get; }

        public int NetCursor { get; set; }

        public int SpawnCount { get; set; }

        public bool DoorOpen { get; set; }
    }
}
