using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Effects;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Mech3.Anim;
using CSVM.Session;
using CSVM.UI;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

internal static class SuiteConstants
{
    internal const int PlayerAirframes = 11;
    internal const int WeaponDefCount = 48;

    // B4's scan fixture (aim-assist): a shooter at the origin on player 0's team, nose down world
    // -Z, firing a 500 m/s round with a 1000 m RANGE through the stock guns' 6° cone. Every gate
    // case moves ONE thing off this baseline, so a rejection can only be the gate under test.
    internal const float ScanSpeed = 500f;
    internal const float ScanRange = 1000f;
    internal const float ScanConeDeg = 6f; // the shipped CANNON_SPREAD, asserted in AimAssistShippedData

    // The fixed step `ordnance-burst-timeline` drives its three bursts at.
    // Deliberately FOUR TIMES finer than SequenceRunner.AnimFrame: the authored gaps
    // under test go down to 0.01 s, which one 1/60 s step cannot resolve at all, and nothing in
    // those three definitions is denominated in animation frames (none of them carries a
    // `LOOP`, so the AnimFrame floor is never reached). A finer step makes every authored
    // instant a real measurement instead of a rounding.
    internal const float BurstDt = 1f / 240f;

    // How far a burst's dispatch may sit from its authored instant: one step because the stamp is
    // taken after the Advance that fired it, one per level of CALL the row sits under, and one for a
    // gate landing a step late on binary float. Every authored gap in the three definitions is wider
    // than this bar two, and those two are carried by the rows behind them.
    internal const float BurstSlack = 6f * BurstDt;

    // How long each burst is driven for — past the last authored event of its longest
    // lane, with room for the lag above. Sonic is the long one: its `sonic_growlight` ends at
    // an authored 3.2 s.
    internal const float BurstSeconds = 3.5f;

    // The burst suite's instance TTL — an order of magnitude past the longest burst, so
    // the bound never truncates a timeline. Explicitly NOT `--effects-test`'s 0.3 s, which
    // exists for the gun path and would cut the 1.2 s wash off at 0.3 s while every remaining
    // assertion still passed.
    internal const float BurstTtl = 32f;

    // Gun-group slots Loadout.ForRig seats on any airframe — the
    // weapon bench fires every gun from all of them, so a drop here would quietly shrink its
    // coverage without changing the 48/48 line.
    internal const int RigGunGroups = 4;

    // Destructible instances / distinct node groups per chapter, at each chapter's default mission.
    // Authored-inactive placed roots are staged hidden and therefore contribute to this registry.
    // Instances exceed node groups where a reader wildcard def and its compiled per-instance twin
    // bind the same nodes. Both columns sit far below plain NAME matching because a compiled def
    // binds the one instance its symbol table names (AnimRuntime.Anchors). A shared reader file
    // no ANIMATION_DEFINITION_FILE list of the mission names registers nothing (AnimProgram).
    internal static readonly (string Chapter, int Instances, int Anchors)[] Census =
    {
        ("C1", 186, 117),
        ("C1B", 28, 28),
        ("C1C", 28, 28),
        ("C2", 192, 125),
        ("C2B", 28, 28),
        ("C3", 210, 133),
        ("C4", 92, 67),
        ("C5", 166, 102),
    };

    // C1 textures spanning the three alpha classes the flatten must leave alone: opaque,
    // hard cutout, and the soft overlays the builder alpha-blends.
    internal static readonly string[] DropInSamples =
    {
        "lkzepskin", "grass1", "cloudlayer", "sky1", // no alpha channel
        "firtree1", "bush1",                          // hard cutouts
        "abld_shadow",                                // soft baked shadow overlay
    };

}
