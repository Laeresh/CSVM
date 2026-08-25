using System.Collections.Generic;

namespace CSVM.Mech3;

/// <summary>One AI range volume: a radius and an altitude band above and below the vehicle,
/// metres. Zero on every slot is "unauthored" and falls through to whatever sits under it (the
/// net's own, the airframe def, then <c>min_ai_active_dist</c>); docs/formats/ai-rosters.md
/// "The three unnamed slots" and docs/org/aiPilot.md "Net assignment".</summary>
public readonly record struct AiVolume(float Radius, float Upper, float Lower)
{
    /// <summary>Whether any of the three values is authored non-zero.</summary>
    public bool IsAuthored => Radius != 0f || Upper != 0f || Lower != 0f;

    /// <summary>This volume with <paramref name="over"/>'s non-zero values written over it, the
    /// engine's own per-field non-zero test.</summary>
    public AiVolume Overlaid(AiVolume over) => new(
        over.Radius != 0f ? over.Radius : Radius,
        over.Upper != 0f ? over.Upper : Upper,
        over.Lower != 0f ? over.Lower : Lower);
}

/// <summary>The three volumes an AI vehicle carries, activation, attack and return, in the order
/// both the roster block (slots 8–19) and a net record (elements 2–10) author them.</summary>
public readonly record struct AiVolumeSet(AiVolume Activation, AiVolume Attack, AiVolume Return)
{
    // The roster block's first volume slot; each volume takes four slots, the fourth a flag no
    // shipped block authors (docs/formats/ai-rosters.md "The three unnamed slots").
    private const int RosterFirstSlot = 8;
    private const int RosterSlotsPerVolume = 4;

    // The net record's first volume element: nine floats after the 10.0 arrival floor
    // (docs/formats/ai-nets.md "Net data").
    private const int NetFirstElement = 2;

    /// <summary>All zero: nothing authored.</summary>
    public static AiVolumeSet None => default;

    /// <summary>Whether any of the nine values is authored non-zero.</summary>
    public bool IsAuthored => Activation.IsAuthored || Attack.IsAuthored || Return.IsAuthored;

    /// <summary>Reads a roster block's twelve volume slots (8–19). A short block or a
    /// non-numeric slot reads 0, which is "unauthored".</summary>
    public static AiVolumeSet FromRosterSlots(IReadOnlyList<object?> fields) =>
        new(
            VolumeAt(fields, RosterFirstSlot, 1),
            VolumeAt(fields, RosterFirstSlot + RosterSlotsPerVolume, 1),
            VolumeAt(fields, RosterFirstSlot + (2 * RosterSlotsPerVolume), 1));

    /// <summary>Reads a net record's nine volume floats (elements 2–10): activation, attack and
    /// return, each as radius, upper, lower, the order of the vehicle fields the net assignment
    /// writes them into (docs/org/aiPilot.md "Net assignment").</summary>
    public static AiVolumeSet FromNetRecord(IReadOnlyList<object?> record) =>
        new(
            VolumeAt(record, NetFirstElement, 1),
            VolumeAt(record, NetFirstElement + 3, 1),
            VolumeAt(record, NetFirstElement + 6, 1));

    /// <summary>This set with <paramref name="over"/>'s non-zero values written over it, field by
    /// field: the block over the net, in the original's order.</summary>
    public AiVolumeSet Overlaid(AiVolumeSet over) => new(
        Activation.Overlaid(over.Activation),
        Attack.Overlaid(over.Attack),
        Return.Overlaid(over.Return));

    private static AiVolume VolumeAt(IReadOnlyList<object?> list, int first, int stride) =>
        new(Num(list, first), Num(list, first + stride), Num(list, first + (2 * stride)));

    private static float Num(IReadOnlyList<object?> list, int index) =>
        index < list.Count && list[index] is float f ? f : 0f;
}
