using System;
using System.Collections.Generic;
using System.IO;

namespace CSVM.Mech3;

/// <summary>One step of a maneuver program: a target attitude in degrees held for a duration.
/// The shipped shape is <c>[duration_s, pitch, yaw, roll]</c>; two entries (barrel_roll,
/// spiral_dive's second step) append three more numbers, kept verbatim in <see cref="Extra"/>
/// (undecoded — never interpreted here). A zero duration reads as "advance as soon as the
/// attitude is reached", not "hold for zero seconds"; see docs/formats/ai-rosters.md.</summary>
public readonly record struct ManeuverStep(
    float DurationS, float PitchDeg, float YawDeg, float RollDeg, IReadOnlyList<float> Extra);

/// <summary>
/// Reads the shared maneuver library (<c>zrdr/maneuvers.zrd</c>) and decodes the roster's
/// <c>signature_maneuvers</c> bitmask (aiv slot 32). Format page: docs/formats/ai-rosters.md.
/// </summary>
public static class Maneuvers
{
    /// <summary>The exe's internal maneuver table order — the bit order of the roster's
    /// <c>signature_maneuvers</c> mask. ⚠ NOT the order the JSON file lists the maneuvers in;
    /// read from the binary (docs/formats/ai-rosters.md, "signature_maneuvers is a bitmask").</summary>
    public static readonly IReadOnlyList<string> ExeTableOrder = new[]
    {
        "roll", "rudder_turn", "bank_turn", "climb", "dive", "jinking",
        "scissors", "rolling_scissors", "snap_roll", "immelman", "loop", "split_s",
        "lag_pursuit_roll", "high_yo_yo", "nitro_evade", "barrel_roll", "spiral_dive",
    };

    /// <summary>Loads the library in file order from the shared zrdr scope
    /// (<c>extracted/zrdr.zip</c> or its unpacked sibling): an alternating
    /// <c>name, [properties…]</c> root list, 17 entries in this install.</summary>
    public static List<Maneuver> Load(string zrdrPath)
    {
        var root = Zrdr.LoadFile(zrdrPath, "maneuvers.json");
        var maneuvers = new List<Maneuver>();
        for (int i = 0; i + 1 < root.Count; i += 2)
        {
            if (root[i] is not string name || root[i + 1] is not List<object?> props)
                throw new InvalidDataException($"maneuvers entry {maneuvers.Count}: not a name/properties pair");
            maneuvers.Add(Parse(name, ZrdrDict.FromAlternating(props)));
        }
        return maneuvers;
    }

    /// <summary>The maneuver names a <c>signature_maneuvers</c> mask (aiv slot 32) marks, in
    /// exe table order. Bits past the 17-entry table are ignored (none ship); 0 = none.</summary>
    public static IReadOnlyList<string> SignatureNames(long mask)
    {
        var names = new List<string>();
        for (int bit = 0; bit < ExeTableOrder.Count; bit++)
        {
            if ((mask & (1L << bit)) != 0)
                names.Add(ExeTableOrder[bit]);
        }
        return names;
    }

    private static Maneuver Parse(string name, ZrdrDict props)
    {
        if (!props.TryFloat("natural_touch", out float difficulty))
            throw new InvalidDataException($"maneuver '{name}': no natural_touch difficulty");

        var steps = new List<ManeuverStep>();
        if (props.List("steps") is { } rawSteps)
        {
            foreach (var entry in rawSteps)
            {
                if (entry is not List<object?> s || s.Count < 4
                    || s[0] is not float duration || s[1] is not float pitch
                    || s[2] is not float yaw || s[3] is not float roll)
                    throw new InvalidDataException(
                        $"maneuver '{name}': step {steps.Count} is not [duration, pitch, yaw, roll, …]");
                var extra = Array.Empty<float>();
                if (s.Count > 4)
                {
                    extra = new float[s.Count - 4];
                    for (int i = 4; i < s.Count; i++)
                        extra[i - 4] = s[i] is float f ? f : throw new InvalidDataException(
                            $"maneuver '{name}': step {steps.Count} extra {i - 4} is not a number");
                }
                steps.Add(new ManeuverStep(duration, pitch, yaw, roll, extra));
            }
        }

        return new Maneuver
        {
            Name = name,
            Difficulty = (int)difficulty,
            Steps = steps,
            AutogyroAllowed = props.Has("autogyro_allowed"),
            Relative = props.Has("relative"),
            Nitro = props.Has("nitro"),
            Bias = props.Float("bias"),
        };
    }
}

/// <summary>
/// One entry of the shared maneuver library (<c>zrdr/maneuvers.zrd</c>): a
/// <c>natural_touch</c> difficulty gate plus a timed attitude-step program. The library is
/// data, not code — 17 shipped entries, of which <c>high_yo_yo</c> is a stub (difficulty 99,
/// no steps; it must parse but never fly). Format page: docs/formats/ai-rosters.md.
/// </summary>
public sealed class Maneuver
{
    public required string Name { get; init; }

    /// <summary>The <c>natural_touch</c> difficulty, compared directly against the pilot's
    /// 1–9 <c>natural_touch</c> stat (no <c>ai_skill_parameters</c> interpolation — both sides
    /// are already on the same scale). Shipped range 0 (nitro_evade, always available) to 99
    /// (the high_yo_yo stub, unreachable by design).</summary>
    public required int Difficulty { get; init; }

    public required IReadOnlyList<ManeuverStep> Steps { get; init; }

    /// <summary>Legal for autogyros (5 of the 17 shipped are).</summary>
    public bool AutogyroAllowed { get; init; }

    /// <summary>Step attitudes are offsets from the attitude at maneuver entry rather than
    /// absolute pitch/roll in the entry-heading frame.</summary>
    public bool Relative { get; init; }

    /// <summary>Fires the nitro (only nitro_evade carries it).</summary>
    public bool Nitro { get; init; }

    /// <summary>Fixed adjustment to the selection rating (climb/dive ship −0.5; 0 = none).</summary>
    public float Bias { get; init; }

    /// <summary>A parse-but-never-fly entry: no steps at all. Shipped case: high_yo_yo.</summary>
    public bool IsStub => Steps.Count == 0;

    /// <summary>The selection cull: a pilot may fly this maneuver when its difficulty does not
    /// exceed the pilot's <c>natural_touch</c> stat — and a stub is never eligible, whatever
    /// the numbers say.</summary>
    public bool EligibleFor(int naturalTouch) => !IsStub && Difficulty <= naturalTouch;
}
