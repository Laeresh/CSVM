using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>The abreast starting grid: one anchor spawn, every pilot fanned symmetrically about it
/// on that anchor's heading, and the whole field raised as one to clear the ground. Placing a race
/// field this way is what makes finishing order reflect flying rather than which entry of the
/// mission's spawn list a pane happened to draw.
///
/// <para>⚠ <b>Not a splitscreen spawner.</b> Dogfight is splitscreen too and deliberately does not
/// use this — its scattered spawns are the mode working as intended, and four dogfighters abreast
/// on one heading is an instant head-on merge every round. The name says <i>race</i> because races
/// are the only thing that may construct it.</para>
///
/// <para>The grid knows nothing about where spawns come from. It asks <see cref="SpawnPicker"/> for
/// player 0's start and treats the answer as the anchor, so the ia.json list, objectives.json's
/// PLAYER_INIT, the C1 last-resort fallback and the <c>--pos</c> debug override all keep working
/// exactly as they do without a grid — including <c>--pos</c> beating the spawn list, which is a
/// property of the order of tests inside <c>ChooseSpawn</c>, not of anything here.</para>
///
/// <para>Terrain comes in through an injected sampler rather than a physics space so the fan and
/// the lift are testable off-engine. Whether a probe that finds nothing means "no ground" or "the
/// physics space has not ticked yet" is a question about the live space, and is decided where that
/// space is — see the production closure built at session build, not this class.</para></summary>
public sealed class RaceGrid : IFlightStarts
{
    /// <summary>Metres between neighbouring grid slots, measured across the line.</summary>
    /// <remarks>TUNE, not a fact: 60 m is the figure already in this tree (<c>SpawnAbreast</c>, a
    /// different job on the <c>--pos</c> path) and there is no evidence for another yet. A later
    /// item moves this to <c>Config.GetFloat</c> so the owed playtest can dial it without a
    /// rebuild; <c>SpawnAbreast</c> itself stays where it is and keeps its own value.</remarks>
    private const float SlotSpacing = 60f;

    /// <summary>Metres of air the lowest slot must have under it before the field is left alone.</summary>
    /// <remarks>TUNE, not a fact, and deliberately loose. An aircraft's collision shapes are boxes
    /// that overhang the airframe, which already produces false terrain contacts in close stunt
    /// flying; tightening this until it hugs the terrain would spawn a race into that known defect
    /// rather than clear it. A later item moves this to <c>Config.GetFloat</c> beside the
    /// spacing.</remarks>
    private const float GroundClearance = 100f;

    private readonly SpawnPicker _picker;
    private readonly Func<Vector3, float?> _groundAt;

    /// <param name="picker">Resolves the anchor. The grid delegates to it rather than reimplementing
    /// any part of spawn resolution.</param>
    /// <param name="groundAt">World height of the ground under a point, or null when nothing was
    /// found there.</param>
    public RaceGrid(SpawnPicker picker, Func<Vector3, float?> groundAt)
    {
        _picker = picker;
        _groundAt = groundAt;
    }

    /// <summary>The whole field as one starting line: slot <c>i</c> of <c>n</c> sits
    /// <c>(i - (n-1)/2) * SlotSpacing</c> metres along the line perpendicular to the anchor heading,
    /// so four players sit at ±0.5 and ±1.5 spacings and the field is centred on the anchor whatever
    /// the count. Every pilot faces the anchor's own heading.
    ///
    /// <para>The ground is then probed under every slot and the <b>entire field</b> raised by
    /// whatever the single worst slot needs. Raising each plane by its own ground was rejected: it
    /// starts a race at four different altitudes, which is the same unfairness this grid exists to
    /// remove, wearing a different coordinate — and unlike a bad fan it is invisible in a
    /// screenshot.</para></summary>
    public IReadOnlyList<FlightStart> ChooseStarts(IReadOnlyList<SpawnPoint>? spawns,
        string missionZrdrPath, int spawnBase, int playerCount)
    {
        var (anchorPos, anchorLookAt) = _picker.ChooseSpawn(spawns, missionZrdrPath, spawnBase, 0, "grid anchor ");

        // The heading comes from the anchor's own pos→look-at pair, never from the spawn's
        // HeadingDeg: the --pos override has no heading field, and re-reading the list entry would
        // silently ignore --direction.
        var dir = anchorLookAt - anchorPos;
        dir = dir.LengthSquared() < 1e-6f ? Vector3.Forward : dir.Normalized();
        var across = dir.Cross(Vector3.Up);
        // Straight up or down has no perpendicular in the horizontal plane. Nothing in the spawn
        // data can produce it (every emitted spawn is level), but --direction=0,1,0 can.
        across = across.LengthSquared() < 1e-6f ? Vector3.Right : across.Normalized();

        var slots = new Vector3[playerCount];
        for (int i = 0; i < playerCount; i++)
        {
            slots[i] = anchorPos + across * ((i - (playerCount - 1) / 2f) * SlotSpacing);
        }

        float lift = 0f;
        int unprobed = 0;
        foreach (var slot in slots)
        {
            if (_groundAt(slot) is not { } ground)
            {
                unprobed++;
                continue;
            }
            lift = Mathf.Max(lift, ground + GroundClearance - slot.Y);
        }
        // A slot with no ground under it asks for no lift — but says so. The anchor is an authored,
        // flyable point, so with no terrain answer the honest move is to leave the field at the
        // altitude the spawn data chose rather than invent a correction from nothing. Silence is
        // what this must not do: a missing answer and flat ground at sea level would both read as
        // zero, and a whole field of nulls means the sampler is broken, not that the map is empty.
        if (unprobed > 0)
        {
            Log.Warn("flight", $"grid found no ground under {unprobed}/{playerCount} slots — field not raised on their account");
        }
        lift = Mathf.Max(lift, 0f);

        var starts = new FlightStart[playerCount];
        for (int i = 0; i < playerCount; i++)
        {
            var pos = slots[i] + Vector3.Up * lift;
            starts[i] = new FlightStart(pos, pos + dir);
        }
        return starts;
    }
}
