using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using Godot;

namespace CSVM.Session;

/// <summary>
/// The engine-side sequencing of one Instant Action mission: the mission's wave state, the ace,
/// and (as the deepening proceeds) the actor build phases, the sequencer tick and the end-condition
/// wiring. The decoded rules stay engine-free in <see cref="InstantActionRuntime"/> and
/// <see cref="InstantActionWaves"/>; this class is where they meet the engine. Held as
/// <c>GameSession</c>'s one nullable director field — null outside a mission, which is what keeps
/// every other session mode (free flight, Dogfight) untouched by its existence.
///
/// Not a Node: <c>GameSession</c> owns the tick order (its ⚠ on <c>DriveSimSteps</c>) and calls
/// the phases at its own decoded points, and every node built on this mission's behalf parents
/// under the session's <c>_worldRoot</c>, so the session's no-Teardown rule holds unchanged.
/// </summary>
public sealed class InstantActionDirector
{
    // E11's wave sequencer: null on dogfight_ace (every wave count is forced to 0).
    // WaveRosters[w] is wave w+1's built (inert until activated) members, indexed the same way;
    // the sequencer tick polls the current wave's alive count and activates whatever
    // InstantActionWaves.Step hands back — through the teleport arm, or (F12, zeppelin_run)
    // through the zeppelin generator's own wave credit.
    internal InstantActionWaves? Waves;
    internal List<FlightController>[]? WaveRosters;
    internal List<SpawnPoint>? WaveSpawnList;

    // F12: the wave whose parked members the objective zeppelin's generator is releasing — the
    // decoded group stamp (FUN_0045b9d0 writes the new counter to the generator's +0x64). 0
    // outside a zeppelin run, or before the first wave becomes current.
    internal int LaunchWave;

    // G14: the authored ace, kept past the build (not just a build-phase local) so
    // --debug-scoreboard can force it down from the drive path, well after every Downed
    // subscription the end-condition block wires is in place. Null outside dogfight_ace.
    internal FlightController? Ace;

    // --debug-scoreboard (IA): single-fire, same shape as GameSession's
    // _crashFired/_versusDebugKillFired.
    internal bool DebugForceFired;

    private InstantActionDirector(InstantActionRuntime runtime)
    {
        Runtime = runtime;
    }

    /// <summary>The engine-free mission runtime: the loaded def, the objective bookkeeping and
    /// the decoded actor rules. Never null on a built director.</summary>
    public InstantActionRuntime Runtime { get; }

    // ⚠ Keep both producers (the wizard's SessionSpec.IaDef and --ia=<path>) converging on the
    // one InstantActionRuntime construction; two similar calls is the failure this avoids.
    // A failed --ia= load warns and flies without a mission rather than aborting the launch.
    public static InstantActionDirector? TryCreate(SessionSpec spec)
    {
        if (spec.IaDef is { } wizardDef)
        {
            GD.Print($"ia: wizard mission_type={wizardDef.MissionType} " +
                     $"player='{wizardDef.PlayerPlane}' ace='{wizardDef.AceName}' ({wizardDef.AcePlane})");
            return new InstantActionDirector(new InstantActionRuntime(wizardDef));
        }
        if (spec.IaPath != null)
        {
            try
            {
                var def = InstantAction.LoadFromJson(spec.IaPath);
                GD.Print($"ia: '{spec.IaPath}' mission_type={def.MissionType} " +
                         $"player='{def.PlayerPlane}' ace='{def.AceName}' ({def.AcePlane})");
                return new InstantActionDirector(new InstantActionRuntime(def));
            }
            catch (Exception e)
            {
                GD.PushWarning($"--ia={spec.IaPath}: cannot load ({e.Message}) — flying without a mission");
            }
        }
        return null;
    }
}
