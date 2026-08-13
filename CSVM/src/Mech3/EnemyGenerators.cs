using System.Collections.Generic;
using System.IO;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Reads a mission's enemy generators (<c>egen.zrd.json</c> in the mission zrdr scope): the
/// hosts that feed AI aircraft into a live mission. Three shipped shapes: zeppelin hangar
/// launch (17), plain spawner (5), moving spawner (1, the submarine); plus <c>[null]</c> on
/// the 33 missions that author none. Format page: docs/formats/mission-entities.md.
/// </summary>
public static class EnemyGenerators
{
    /// <summary>Loads every generator of one mission, in file order. Empty when the mission's
    /// file is <c>[null]</c>; throws <see cref="FileNotFoundException"/> when the mission scope
    /// carries no egen file at all (unseen in this install, all 53 mission dirs have one).</summary>
    public static List<EnemyGeneratorDef> Load(string missionZrdrPath)
    {
        var root = Zrdr.LoadFile(missionZrdrPath, "egen.json");
        var defs = new List<EnemyGeneratorDef>();
        // The whole reader is one outer element: [[record0, record1, …]], or [null] for none.
        if (root.Count == 0 || root[0] is not List<object?> records)
        {
            return defs;
        }
        foreach (var entry in records)
        {
            if (entry is List<object?> record)
            {
                defs.Add(ParseRecord(defs.Count, record));
            }
        }
        return defs;
    }

    private static EnemyGeneratorDef ParseRecord(int index, List<object?> record)
    {
        var d = ZrdrDict.FromAlternating(record);
        string node = d.Str("node")
            ?? throw new InvalidDataException($"egen record {index}: no 'node' key");
        var vehicle = d.Dict("vehicle")
            ?? throw new InvalidDataException($"egen '{node}': no 'vehicle' block");

        var nets = new List<string>();
        foreach (var n in vehicle.List("nets") ?? new List<object?>())
        {
            if (n is string netName)
            {
                nets.Add(netName);
            }
        }

        return new EnemyGeneratorDef
        {
            Node = node,
            VehicleParams = vehicle.Str("params"),
            Nets = nets,
            // The engine reads only the first letter: 'c'yclic (every authored file) or 'r'andom.
            ChooseNetsRandom = vehicle.Str("choose_nets") is { Length: > 0 } c
                && char.ToLowerInvariant(c[0]) == 'r',
            Capacity = (int)d.Float("capacity"),
            MaxActive = (int)d.Float("max_active"),
            WaveSize = (int)d.Float("wave_size"),
            WavePeriod = d.Float("wave_period"),
            IndPeriod = d.Float("ind_period"),
            IsZeppelin = d.Has("zeppelin"),
            OpenAnim = d.Str("open_anim"),
            CloseAnim = d.Str("close_anim"),
            Origin = d.Str("origin"),
            RotationDeg = d.List("rotation") is { Count: 3 } r
                && r[0] is float rx && r[1] is float ry && r[2] is float rz
                ? new Vector3(rx, ry, rz) : null,
            // The engine's unset sentinel is -1.0; the 17 authored values run 100–300.
            MinAltitude = d.TryFloat("min_altitude", out float alt) && alt >= 0f ? alt : null,
            HealthyNode = d.Str("healthy"),
            MovingPath = d.Has("moving_path"),
        };
    }
}

/// <summary>
/// One <c>egen.zrd.json</c> generator. The common block is on all 23 shipped records; the
/// zeppelin keys (<see cref="IsZeppelin"/> through <see cref="MinAltitude"/>) on 17; the
/// submarine adds <see cref="HealthyNode"/> and <see cref="MovingPath"/>.
/// </summary>
public sealed class EnemyGeneratorDef
{
    /// <summary>The host world node: a zeppelin, a ground airfield (<c>eairg31</c>), a ship or
    /// the submarine. A generator whose node does not resolve is DROPPED at load, never loaded
    /// inert (decoded rule; docs/formats/mission-entities.md "The generator cycle").</summary>
    public required string Node { get; init; }

    /// <summary>The roster param-set the spawned vehicles are configured from: a designer label
    /// in the mission's <c>aiv.zrd.json</c> HEADER (e.g. <c>Eairg31_params</c>), not a vehicle
    /// block name; the header pairs it with a block index (docs/formats/ai-rosters.md). Null on
    /// 8 of 23 (the spawn then has no authored roster block).</summary>
    public string? VehicleParams { get; init; }

    /// <summary>Chapter net names (the <c>neindex</c> join key, <see cref="AiNets"/>) handed to
    /// spawned aircraft in turn. A generator where NONE resolve is dropped at load, like an
    /// unresolved <see cref="Node"/>.</summary>
    public required IReadOnlyList<string> Nets { get; init; }

    /// <summary>False = cyclic (every authored file); true = random, which the engine's parser
    /// also accepts.</summary>
    public bool ChooseNetsRandom { get; init; }

    /// <summary>The lifetime spawn budget. It is <c>0</c> on all 23 shipped generators, and the
    /// decoded blocking rule would then hold every generator forever. ⚠ Unresolved discrepancy;
    /// see docs/formats/mission-entities.md "The capacity puzzle" for the remake's stand-in.</summary>
    public int Capacity { get; init; }

    /// <summary>Concurrent live spawns (1/4/5/6/10).</summary>
    public int MaxActive { get; init; }

    /// <summary>Planes per wave (1, once 3).</summary>
    public int WaveSize { get; init; }

    /// <summary>Seconds between waves. COMPOSES with <see cref="IndPeriod"/>: the gap between
    /// waves is <c>ind_period + wave_period</c>, never this alone.</summary>
    public float WavePeriod { get; init; }

    /// <summary>Seconds between individuals inside a wave.</summary>
    public float IndPeriod { get; init; }

    /// <summary>The zeppelin-hangar variant (authored as <c>zeppelin [1]</c> on 17 of 23).</summary>
    public bool IsZeppelin { get; init; }

    /// <summary>Hangar-door animations, run before and after a wave. Null when unauthored; the
    /// engine then defaults them from the node name (<c>&lt;node&gt;_open_&lt;nn&gt;</c> /
    /// <c>close_&lt;nn&gt;</c>), which is F20's to reproduce with the door choreography.</summary>
    public string? OpenAnim { get; init; }

    /// <summary>See <see cref="OpenAnim"/>.</summary>
    public string? CloseAnim { get; init; }

    /// <summary>The node the fighters appear at (<c>cargobay</c> on 16 of 17 zeppelins).</summary>
    public string? Origin { get; init; }

    /// <summary>The drop attitude, three angles in degrees (<c>[-90, 0, 0]</c> throughout; the
    /// engine converts all three to radians at load).</summary>
    public Vector3? RotationDeg { get; init; }

    /// <summary>The launch gate, metres: while the host is below it, generation is HELD (timer
    /// and wave counter untouched), never cancelled. Null when unauthored (the engine's
    /// <c>-1.0</c> sentinel skips the gate entirely).</summary>
    public float? MinAltitude { get; init; }

    /// <summary>The node whose destruction stops the generator (the submarine's
    /// <c>subhealthy</c>); zeppelin generators use the host's own destroyed flag instead.</summary>
    public string? HealthyNode { get; init; }

    /// <summary>Bare flag on the submarine record; undecoded beyond its presence.</summary>
    public bool MovingPath { get; init; }
}
