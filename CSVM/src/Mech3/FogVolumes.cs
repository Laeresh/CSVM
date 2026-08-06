using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>One <c>fvol*</c> node of the world: the authored box a fog volume occupies, in world
/// coordinates. These carry no visible geometry — <see cref="WorldBuilder.SkipWorldNode"/> has
/// excluded them from the render since the world build was written — they are the shape the
/// original fills with the <c>fogvol.zrd</c> clutter (see docs/formats/fogvol.md).
///
/// <para>Named <c>…Box</c> because <c>Godot.FogVolume</c> is a real engine type (volumetric fog),
/// which this is not: it is authored data, and nothing in the remake renders fog from it.</para>
/// </summary>
public readonly record struct FogVolumeBox(string Name, Aabb Box);

/// <summary>One weighted template reference of a clutter block's <c>nodes</c> list: the gamez
/// template node to scatter, and its relative weight among the block's alternatives.</summary>
public readonly record struct FogClutterNode(float Weight, string Node);

/// <summary>
/// One <c>clutter</c> block of <c>fogvol.zrd</c> — a weighted alternative in the chapter's
/// scatter table, with the per-placement ranges that apply when it is the one drawn. Schema and
/// the per-chapter values: docs/formats/fogvol.md.
/// </summary>
public sealed class FogClutter
{
    /// <summary>The block's weight among the spec's alternatives (every shipped block: 1.0).</summary>
    public float Weight { get; init; } = 1f;

    /// <summary>The template nodes this block may place, with their relative weights. Every
    /// shipped block names exactly one.</summary>
    public IReadOnlyList<FogClutterNode> Nodes { get; init; } = Array.Empty<FogClutterNode>();

    /// <summary><c>far_fade_range[0]</c> — the nearer of the two authored fade bands (metres,
    /// start then gone). Read but not rendered; see <see cref="FarFade"/>.</summary>
    public Vector2 FarFadeNear { get; init; }

    /// <summary><c>far_fade_range[1]</c> — the farther authored fade band (metres, start then
    /// gone), which is what the remake renders. The pair mirrors <c>templates.zrd</c>'s ground
    /// clutter, where the same key carries two bands per decoration; the remake draws at the
    /// farther one because it has no reduced-detail mode to select the nearer with.</summary>
    public Vector2 FarFade { get; init; }

    /// <summary><c>perp_dist_range</c> — the placement's offset perpendicular to the volume's
    /// horizontal plane, i.e. vertical metres (min, max).</summary>
    public Vector2 PerpDistRange { get; init; }

    /// <summary><c>perturb_dist_range</c> — how far the placement is displaced from its grid point
    /// within that plane (min, max metres).</summary>
    public Vector2 PerturbDistRange { get; init; }

    /// <summary><c>scale_range</c> — the multiplier on the template sprite's own authored size.</summary>
    public Vector2 ScaleRange { get; init; } = Vector2.One;
}

/// <summary>
/// The chapter's <c>zrdr/fogvol.zrd</c>: the fog volumes' parameters and the clutter table the
/// original scatters through them. Full schema, per-chapter values and the decoded/undecoded
/// split: docs/formats/fogvol.md.
///
/// <para>The volumes themselves are gamez geometry, not part of this file — see
/// <see cref="VolumesOf"/>.</para>
/// </summary>
public sealed class FogVolumeSpec
{
    /// <summary><c>fog_zone</c> — present in the five chapters that ship fog volumes (0, except
    /// C5's 1). Read and reported; nothing consumes it. It is NOT the sky/fog zone selector
    /// (<c>BL-277</c>/<c>BL-100</c>) — see the open question in docs/formats/fogvol.md.</summary>
    public int? FogZone { get; init; }

    /// <summary><c>distance</c> — the scatter's world-space grid period in metres (130 in
    /// C1/C1C/C2B/C4, 80 in C5; the degenerate copies carry 206.25).</summary>
    public float Distance { get; init; }

    /// <summary><c>fog_fade_dist</c> / <c>interior_fog_fade_dist</c> / <c>fog_color</c> — C5 only,
    /// the volume's own interior fog. Read and reported; rendering interior fog is not part of the
    /// clutter this class feeds (docs/formats/fogvol.md).</summary>
    public float? FogFadeDist { get; init; }

    /// <inheritdoc cref="FogFadeDist"/>
    public float? InteriorFogFadeDist { get; init; }

    /// <inheritdoc cref="FogFadeDist"/>
    public Vector3? FogColor { get; init; }

    /// <summary>The scatter table: one entry per <c>clutter</c> block, in file order.</summary>
    public IReadOnlyList<FogClutter> Clutter { get; init; } = Array.Empty<FogClutter>();

    /// <summary>False when the file carries its clutter block as a bare list with no
    /// <c>clutter</c> key — the shape C1B/C2/C3 ship. Those same three chapters have no
    /// <c>fvol*</c> nodes and no <c>cloudsprite*</c> template in their gamez, so the block is
    /// inert whichever way it is read; the flag exists so the log can say which shape was read
    /// rather than leaving "no clouds here" unexplained.</summary>
    public bool HasClutterKey { get; init; }

    /// <summary>Loads a chapter's <c>fogvol.zrd</c>. Null when the chapter's zrdr scope has no
    /// such file — degrades rather than throwing, like every other optional reader here.</summary>
    public static FogVolumeSpec? Load(string chapterZrdrPath)
    {
        try
        {
            return Parse(Zrdr.LoadFile(chapterZrdrPath, "fogvol.json"));
        }
        catch (Exception e) when (e is System.IO.FileNotFoundException
                                      or System.IO.DirectoryNotFoundException
                                      or System.IO.InvalidDataException
                                      or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>Parses an already-loaded reader list. The walk is by hand rather than through
    /// <see cref="ZrdrDict"/> because three chapters put a clutter block in the root list with no
    /// key in front of it, and the dict view silently drops an unkeyed value.</summary>
    public static FogVolumeSpec Parse(List<object?> root)
    {
        int? fogZone = null;
        float distance = 0f;
        float? fogFade = null, interiorFogFade = null;
        Vector3? fogColor = null;
        var clutter = new List<FogClutter>();
        bool hasClutterKey = false;

        for (int i = 0; i < root.Count; i++)
        {
            if (root[i] is not string key)
            {
                // An unkeyed block: C1B/C2/C3's degenerate copy of the clutter entry.
                if (root[i] is List<object?> bare && ParseClutter(bare) is { } orphan)
                {
                    clutter.Add(orphan);
                }
                continue;
            }
            var value = i + 1 < root.Count ? root[i + 1] as List<object?> : null;
            if (value == null)
            {
                continue; // a bare flag; none is shipped, but the grammar allows one
            }
            i++;
            switch (key.ToLowerInvariant())
            {
                case "fog_zone":
                    fogZone = Scalar(value, 0) is { } zone ? (int)zone : null;
                    break;
                case "distance":
                    distance = Scalar(value, 0) ?? 0f;
                    break;
                case "fog_fade_dist":
                    fogFade = Scalar(value, 0);
                    break;
                case "interior_fog_fade_dist":
                    interiorFogFade = Scalar(value, 0);
                    break;
                case "fog_color":
                    fogColor = value.Count >= 3
                        ? new Vector3(Scalar(value, 0) ?? 0f, Scalar(value, 1) ?? 0f, Scalar(value, 2) ?? 0f)
                        : null;
                    break;
                case "clutter":
                    hasClutterKey = true;
                    foreach (var entry in value)
                    {
                        if (entry is List<object?> block && ParseClutter(block) is { } parsed)
                        {
                            clutter.Add(parsed);
                        }
                    }
                    break;
            }
        }

        return new FogVolumeSpec
        {
            FogZone = fogZone,
            Distance = distance,
            FogFadeDist = fogFade,
            InteriorFogFadeDist = interiorFogFade,
            FogColor = fogColor,
            Clutter = clutter,
            HasClutterKey = hasClutterKey,
        };
    }

    /// <summary>The world's fog volumes — every <c>fvol*</c> node, with the world-space AABB of
    /// its own box geometry. Empty in the three chapters that ship none (C1B, C2, C3), which is
    /// exactly the set whose <c>fogvol.zrd</c> is the degenerate copy.
    ///
    /// <para>Static over a <see cref="GameZ"/> and computed from the mesh vertices plus the node
    /// transforms, so it needs no built scene and is testable off-engine — the same shape as
    /// <see cref="WorldBuilder.HorizonZonesOf"/>.</para></summary>
    public static IReadOnlyList<FogVolumeBox> VolumesOf(GameZ gamez)
    {
        var volumes = new List<FogVolumeBox>();
        foreach (var node in gamez.Nodes)
        {
            if (!WorldBuilder.IsFogVolumeNode(node))
            {
                continue;
            }
            if (SubtreeBox(gamez, node, node.Local ?? Transform3D.Identity) is { } box)
            {
                volumes.Add(new FogVolumeBox(node.Name, box));
            }
        }
        return volumes;
    }

    private static float? Scalar(List<object?> list, int index) =>
        index < list.Count && list[index] is float f ? f : null;

    private static Vector2 Pair(List<object?>? list) =>
        list == null ? Vector2.Zero : new Vector2(Scalar(list, 0) ?? 0f, Scalar(list, 1) ?? 0f);

    private static FogClutter? ParseClutter(List<object?> block)
    {
        var dict = ZrdrDict.FromAlternating(block);
        var nodes = new List<FogClutterNode>();
        if (dict.List("nodes") is { } nodeList)
        {
            foreach (var entry in nodeList)
            {
                if (entry is List<object?> pair && pair.Count >= 2
                    && pair[0] is float weight && pair[1] is string name)
                {
                    nodes.Add(new FogClutterNode(weight, name));
                }
            }
        }
        if (nodes.Count == 0)
        {
            return null;
        }
        var fades = dict.List("far_fade_range");
        return new FogClutter
        {
            Weight = dict.Float("weight", 1f),
            Nodes = nodes,
            FarFadeNear = Pair(fades is { Count: > 0 } ? fades[0] as List<object?> : null),
            FarFade = Pair(fades is { Count: > 1 } ? fades[1] as List<object?> : null),
            PerpDistRange = Pair(dict.List("perp_dist_range")),
            PerturbDistRange = Pair(dict.List("perturb_dist_range")),
            ScaleRange = dict.List("scale_range") is { } scale ? Pair(scale) : Vector2.One,
        };
    }

    // The union of a subtree's mesh vertex bounds, in the frame `xf` expresses. The shipped
    // fvol nodes are childless boxes at identity, but the walk costs nothing and a placed volume
    // would otherwise be measured in the wrong frame.
    private static Aabb? SubtreeBox(GameZ gamez, GameZNode node, Transform3D xf)
    {
        Aabb? total = null;
        if (node.MeshIndex >= 0 && node.MeshIndex < gamez.Meshes.Count)
        {
            foreach (var v in gamez.Meshes[node.MeshIndex].Vertices)
            {
                var p = xf * v;
                total = total is { } a ? a.Expand(p) : new Aabb(p, Vector3.Zero);
            }
        }
        foreach (var childIndex in node.Children)
        {
            if (childIndex < 0 || childIndex >= gamez.Nodes.Count)
            {
                continue;
            }
            var child = gamez.Nodes[childIndex];
            if (SubtreeBox(gamez, child, xf * (child.Local ?? Transform3D.Identity)) is { } sub)
            {
                total = total is { } a ? a.Merge(sub) : sub;
            }
        }
        return total;
    }
}
