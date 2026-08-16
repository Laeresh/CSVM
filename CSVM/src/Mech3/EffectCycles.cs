using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Utils;

namespace CSVM.Mech3;

/// <summary>
/// The <c>EFFECTS</c> block of the shared <c>effects.zrd</c> — the original's second source of
/// texture flipbooks, and the one that lights the fires. Two entries exist install-wide,
/// <c>fire1.flt</c> (12 maps @ 10 fps) and <c>fire2.flt</c> (6 @ 5 fps).
///
/// <para>An entry names a NODE, but what it animates is that node's MATERIAL. The engine's reader
/// (<c>zeff_ini.c</c>, <c>FUN_00523ac0</c>) resolves the node, walks down to the first mesh under
/// it, takes surface 0's material, and installs the frame list on the material record
/// (<c>gmod_matl.c</c>'s <c>SetCycleTextureCount/Map/Loop/Speed</c>). The draw loop then tests the
/// material's own cycled bit per polygon and samples whichever frame the cycle is on. Materials
/// are one record per texture — C1's 570-entry table has exactly one duplicate pair — so the
/// flipbook reaches every polygon that references that material, not just the node that named it.
/// <c>fire1.flt</c> is a parentless proxy the world never draws, exactly like the interp scripts'
/// <c>watersetup</c>/<c>surfsetup</c> nodes; the visible burning is C1's refinery vent
/// <c>flame01</c> and the muzzle bursts, which share material 88 with it.</para>
///
/// <para>So this pass patches the <see cref="GameZMaterial"/> the entry points at, before the
/// world is built. <c>SceneBuilder.RegisterCycle</c> then picks it up unchanged, for every
/// ShaderMaterial that source material produces, which is what makes the vent animate without any
/// of the fire machinery knowing it exists. Running before the build is also the engine's own
/// ordering: <c>zeff_ini</c> patches the material at load and the draw loop reads it later.</para>
/// </summary>
public static class EffectCycles
{
    /// <summary>Installs every EFFECTS flipbook onto the gamez material it targets, and returns one
    /// summary line per entry for the build log. A missing or unreadable reader is not an error —
    /// the world simply keeps its static base textures.</summary>
    public static List<string> Apply(GameZ gamez, string sharedZrdrPath)
    {
        var applied = new List<string>();
        List<object?> root;
        try
        {
            root = Zrdr.LoadFile(sharedZrdrPath, "effects.zrd.json");
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException or InvalidDataException)
        {
            Log.Warn("world", $"effects.zrd.json not readable under {sharedZrdrPath} — no effect flipbooks");
            return applied;
        }

        if (root.Count == 0 || root[0] is not List<object?> outer)
            return applied;
        if (ZrdrDict.FromAlternating(outer).List("EFFECTS") is not { } entries)
            return applied;

        foreach (var e in entries)
        {
            // Entry shape: [node, "NAME", [name], "SPEED", [fps], "LOOPING", ["ON"], "MAPS", [tif...]].
            // The leading node name has no value list, so FromAlternating reads it as a bare flag
            // and the keyed fields still line up; take it positionally rather than by that accident.
            if (e is not List<object?> { Count: > 0 } entry || entry[0] is not string nodeName)
                continue;
            var d = ZrdrDict.FromAlternating(entry);
            var maps = d.List("MAPS");
            if (maps == null || maps.Count < 2)
                continue; // a one-frame "cycle" is a static texture

            if (FindMaterial(gamez, nodeName) is not { } hit)
            {
                Log.Warn("world", $"effect {nodeName}: no node, mesh or material to cycle — skipped");
                continue;
            }

            var (materialIndex, material) = hit;
            material.CycleTextures.Clear();
            foreach (var m in maps)
                if (m is string tif)
                    material.CycleTextures.Add(tif);
            material.CycleSpeed = d.Float("SPEED");
            // The engine string-compares LOOPING against "ON" and loops only on an exact match.
            material.CycleLooping = string.Equals(d.Str("LOOPING"), "ON", StringComparison.OrdinalIgnoreCase);
            applied.Add($"{nodeName}→mat{materialIndex}"
                        + $" {material.TextureName ?? "?"}×{material.CycleTextures.Count}@{material.CycleSpeed:0.#}");
        }
        return applied;
    }

    // The engine's own resolution: the named node, then the first mesh at or under it
    // (depth-first, `FUN_00525d40`), then that mesh's first polygon's material.
    private static (int Index, GameZMaterial Material)? FindMaterial(GameZ gamez, string nodeName)
    {
        int nodeIndex = gamez.Nodes.FindIndex(n => n.Name.Equals(nodeName, StringComparison.OrdinalIgnoreCase));
        if (nodeIndex < 0)
            return null;
        int materialIndex = FirstMaterial(gamez, nodeIndex, depth: 0);
        if (materialIndex < 0 || materialIndex >= gamez.Materials.Count)
            return null;
        return (materialIndex, gamez.Materials[materialIndex]);
    }

    private static int FirstMaterial(GameZ gamez, int nodeIndex, int depth)
    {
        if (depth > 16 || nodeIndex < 0 || nodeIndex >= gamez.Nodes.Count)
            return -1; // a malformed tree must not recurse forever
        var node = gamez.Nodes[nodeIndex];
        if (node.MeshIndex >= 0 && node.MeshIndex < gamez.Meshes.Count)
        {
            var mesh = gamez.Meshes[node.MeshIndex];
            if (mesh.Polygons.Count > 0 && mesh.Polygons[0].MaterialIndex >= 0)
                return mesh.Polygons[0].MaterialIndex;
        }
        foreach (int child in node.Children)
        {
            int found = FirstMaterial(gamez, child, depth + 1);
            if (found >= 0)
                return found;
        }
        return -1;
    }
}
