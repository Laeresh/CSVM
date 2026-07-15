using System.Collections.Generic;
using System.IO;
using CrimsonSkies.Mech3;
using Godot;

namespace CrimsonSkies.Flight;

/// <summary>One instant-action spawn: a world position + a heading (yaw, degrees).</summary>
public readonly record struct SpawnPoint(Vector3 Position, float HeadingDeg);

/// <summary>
/// Reads instant-action player spawns from a mission's <c>ia.json</c> (its
/// <c>spawn_points</c> block). Each scenario ("zeppelin_run", "dogfight_ace",
/// "dogfight_squadron", "stunt_flying", …) lists its spawns as <c>[x, y, z, heading°]</c>;
/// the original picks one at random per launch. Story-mission spawns instead live in
/// <c>objectives.json</c> PLAYER_INIT (position + Euler rotation + throttle + speed) —
/// a different reader, not handled here.
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
}
