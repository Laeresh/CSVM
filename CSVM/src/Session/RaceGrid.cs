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
    /// <summary>Default metres between neighbouring grid slots, measured across the line;
    /// overridable as <c>raceGrid.slotSpacing</c>. Referenced by
    /// <see cref="Config.WarmTuningRegistry"/> so <c>--dump-config</c> lists the key on a launch
    /// that never builds a race.</summary>
    /// <remarks>TUNE, not a fact: 60 m is the figure already in this tree (<c>SpawnAbreast</c>, a
    /// different job on the <c>--pos</c> path) and there is no evidence for another yet — a
    /// fallback nobody has judged at the controls, not a decision. Config is what settles it:
    /// the owed playtest dials the key and the spawn log below reports what was in force.
    /// <c>SpawnAbreast</c> stays where it is and keeps its own value.</remarks>
    public const float SlotSpacingDefault = 60f;

    /// <summary>Default metres of air the lowest slot must have under it before the field is left
    /// alone; overridable as <c>raceGrid.groundClearance</c>.</summary>
    /// <remarks>TUNE, not a fact, and deliberately loose. An aircraft's collision shapes are boxes
    /// that overhang the airframe, which already produces false terrain contacts in close stunt
    /// flying; tightening this until it hugs the terrain would spawn a race into that known defect
    /// rather than clear it. Dial it at the playtest, not from a screenshot.</remarks>
    public const float GroundClearanceDefault = 100f;

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
        // Read-through at the point of use, once per session: the grid is constructed at session
        // build and asked exactly once, so this is also the moment the values in force are logged.
        float spacing = Config.GetFloat("raceGrid.slotSpacing", SlotSpacingDefault);
        float clearance = Config.GetFloat("raceGrid.groundClearance", GroundClearanceDefault);

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
            slots[i] = anchorPos + across * ((i - (playerCount - 1) / 2f) * spacing);
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
            lift = Mathf.Max(lift, ground + clearance - slot.Y);
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

        // The heading every slot shares, back in the units the spawn data states it in, so the
        // grid's lines read against the anchor's own line above them. The +360 before the wrap is
        // what keeps a due-north field from printing IEEE negative zero as "heading=-0°".
        float headingDeg = Mathf.PosMod(Mathf.RadToDeg(Mathf.Atan2(-dir.X, -dir.Z)) + 360f, 360f);

        // ⚠ The per-slot line below is the primary field instrument for this geometry, not a
        // convenience: a grid is not photographable. The panes are chase-cam only, so at this
        // spacing a neighbour is outside the frustum and no screenshot of a race can show whether
        // the field is abreast, evenly spaced, on one heading or at one altitude. Everything the
        // grid is judged by therefore has to be readable here — the slot, the point it landed on,
        // the lift the field took as a whole, and the spacing in force, which is what makes an
        // overridden raceGrid.slotSpacing visible at the controls without a rebuild.
        var starts = new FlightStart[playerCount];
        for (int i = 0; i < playerCount; i++)
        {
            var pos = slots[i] + Vector3.Up * lift;
            starts[i] = new FlightStart(pos, pos + dir);
            Log.Info("flight", $"spawn [P{i + 1} grid slot {i + 1} of {playerCount}] pos=({pos.X:0},{pos.Y:0},{pos.Z:0}) heading={headingDeg:0}° spacing={spacing:0.#}m lift={lift:0.#}m");
        }
        return starts;
    }
}
