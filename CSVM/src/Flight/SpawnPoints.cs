using System.Collections.Generic;
using System.IO;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>One instant-action spawn: a world position + a heading (yaw, degrees).</summary>
public readonly record struct SpawnPoint(Vector3 Position, float HeadingDeg);

/// <summary>
/// Reads the player spawn from a mission's zrdr. Two schemas, keyed by mission type:
/// <list type="bullet">
/// <item><b>Instant action</b> (IA1 folders) — <c>ia.json</c> <c>spawn_points</c>: a dict
///   scenario ("zeppelin_run", "dogfight_ace", "dogfight_squadron", "stunt_flying", …) →
///   list of <c>[x, y, z, heading°]</c>; the original picks one at random per launch.</item>
/// <item><b>Story missions</b> (M0x folders — no ia.json) — <c>objectives.json</c>
///   <c>PLAYER_INIT</c> = <c>[1, [x,y,z], [pitch,yaw,roll]°, throttle, speed]</c>; we take
///   position + yaw.</item>
/// </list>
/// Both yield a <see cref="SpawnPoint"/> (position + heading). The throttle/speed fields
/// are ignored — the remake uses a fixed start (see <see cref="FlightController"/>).
/// </summary>
public static class SpawnPoints
{
    /// <summary>Loads the spawn list for <paramref name="scenario"/> from the mission's
    /// ia.json (a zrdr zip or unpacked dir). Null if the file or scenario is absent.</summary>
    public static List<SpawnPoint>? LoadIa(string missionZrdrPath, string scenario)
    {
        List<object?> root;
        try
        {
            root = Zrdr.LoadFile(missionZrdrPath, "ia.json");
        }
        catch (IOException)
        {
            return null; // no ia.json for this mission (e.g. a multiplayer-only folder)
        }

        var points = ZrdrDict.FromAlternating(root).Dict("spawn_points")?.List(scenario);
        if (points == null)
            return null;

        var spawns = new List<SpawnPoint>();
        foreach (var p in points)
            if (p is List<object?> a && a.Count >= 4
                && a[0] is float x && a[1] is float y && a[2] is float z && a[3] is float h)
                spawns.Add(new SpawnPoint(new Vector3(x, y, z), h));
        return spawns.Count > 0 ? spawns : null;
    }

    /// <summary>Loads the story-mission spawn from a mission's objectives.json
    /// <c>PLAYER_INIT</c> block (<c>[1, [x,y,z], [pitch,yaw,roll]°, throttle, speed]</c>),
    /// taking position + yaw. Null if the file or block is absent. The fallback for
    /// missions that have no instant-action ia.json (only IA1 folders do).</summary>
    public static SpawnPoint? LoadPlayerInit(string missionZrdrPath)
    {
        List<object?> root;
        try
        {
            root = Zrdr.LoadFile(missionZrdrPath, "objectives.json");
        }
        catch (IOException)
        {
            return null;
        }

        // objectives.json root wraps a single alternating key/value list
        if (root.Count == 0 || root[0] is not List<object?> inner)
            return null;
        var pi = ZrdrDict.FromAlternating(inner).List("PLAYER_INIT");
        if (pi == null || pi.Count < 3
            || pi[1] is not List<object?> pos || pos.Count < 3
            || pi[2] is not List<object?> rot || rot.Count < 2
            || pos[0] is not float x || pos[1] is not float y || pos[2] is not float z
            || rot[1] is not float yaw)
            return null;
        return new SpawnPoint(new Vector3(x, y, z), yaw);
    }
}
