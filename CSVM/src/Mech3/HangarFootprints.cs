using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The floors of the hangars a mission stands, which no Rich vegetation grows on. A hangar is a rearm
/// base's building (<c>rabaset</c>n) or a Capture the Flag base (<c>ctf_</c>n). A chapter's gamez holds
/// both, and a mission's setup script switches off the ones it does not use. A footprint is the convex
/// hull of the hangar's own geometry in world XZ, nearest LOD. Which layers keep off it: the module entry,
/// docs/architecture/Mech3.md. Names: docs/org/multiplayer-rearm.md and docs/org/multiplayer-ctf.md.
/// </summary>
public sealed class HangarFootprints
{
    private readonly List<Vector2[]> _outlines;

    /// <summary>The hangars' outlines in world XZ, each convex and counter-clockwise.</summary>
    public HangarFootprints(IEnumerable<Vector2[]> outlines)
    {
        _outlines = outlines.Where(o => o.Length >= 3).ToList();
    }

    /// <summary>No hangar: covers nothing.</summary>
    public static HangarFootprints None { get; } = new(Array.Empty<Vector2[]>());

    /// <summary>The outlines, each a convex polygon in world XZ, counter-clockwise.</summary>
    public IReadOnlyList<Vector2[]> Outlines => _outlines;

    /// <summary>The outlines fanned into triangles, three corners each at height zero, for a clip that cuts by XZ alone.</summary>
    public IReadOnlyList<Vector3> Triangles => _outlines.SelectMany(o => Enumerable.Range(1, o.Length - 2)
        .SelectMany(i => new[] { o[0], o[i], o[i + 1] })).Select(p => new Vector3(p.X, 0f, p.Y)).ToList();

    /// <summary>Whether a gamez node is a hangar by its name: <c>rabaset</c> and anything after, or <c>ctf_</c> and digits.</summary>
    public static bool IsHangar(string name) =>
        name.StartsWith("rabaset", StringComparison.OrdinalIgnoreCase)
        || (name.Length > 4 && name.StartsWith("ctf_", StringComparison.OrdinalIgnoreCase) && name[4..].All(char.IsAsciiDigit));

    /// <summary>The footprints of the hangars that stand once <paramref name="setup"/> has run. A hangar stands when it
    /// and every ancestor are on, by the script's last switch, else by its built node in <paramref name="world"/>. That
    /// is the built switch, never the record's ACTIVE bit, which a script overrides. A hangar <paramref name="world"/>
    /// never built does not stand. Every hangar stands with neither.</summary>
    public static HangarFootprints Of(GameZ gamez, MissionSetup? setup = null, Node? world = null, string worldName = "world1")
    {
        var switched = setup?.Switches(gamez) ?? new Dictionary<int, bool>();
        var built = world != null ? ClutterBuilder.BuiltByIndex(world) : null;
        var parent = new Dictionary<int, int>();
        foreach (var node in gamez.Nodes)
        {
            foreach (int c in node.Children)
                parent.TryAdd(c, node.Index);
        }
        bool On(int index) => switched.TryGetValue(index, out bool on) ? on : built?.GetValueOrDefault(index)?.Visible ?? true;
        bool Stands(GameZNode node)
        {
            if (built != null && !built.ContainsKey(node.Index))
                return false;
            for (int at = node.Index; ; at = parent[at])
            {
                if (!On(at))
                    return false;
                if (!parent.ContainsKey(at))
                    return true;
            }
        }

        // Each hangar's subtree by node index, so the world walk's transforms place its vertices.
        var owner = new Dictionary<int, int>();
        var points = new List<List<Vector2>>();
        foreach (var node in gamez.Nodes)
        {
            if (!IsHangar(node.Name) || owner.ContainsKey(node.Index) || !Stands(node))
                continue;
            points.Add(new List<Vector2>());
            var open = new Stack<int>(new[] { node.Index });
            while (open.Count > 0)
            {
                int at = open.Pop();
                if (at < 0 || at >= gamez.Nodes.Count || !owner.TryAdd(at, points.Count - 1))
                    continue;
                foreach (int c in gamez.Nodes[at].Children)
                    open.Push(c);
            }
        }
        if (points.Count == 0)
            return None;
        VegetationGround.WalkWorld(gamez, worldName, (node, mesh, xf) =>
        {
            if (!owner.TryGetValue(node.Index, out int h))
                return;
            foreach (var v in mesh.Vertices)
            {
                var p = xf * v;
                points[h].Add(new Vector2(p.X, p.Z));
            }
        });
        return new HangarFootprints(points.Select(Hull));
    }

    /// <summary>The convex hull of a point set, counter-clockwise; empty for fewer than three distinct points.</summary>
    public static Vector2[] Hull(IEnumerable<Vector2> points)
    {
        var p = points.Distinct().OrderBy(v => v.X).ThenBy(v => v.Y).ToList();
        if (p.Count < 3)
            return Array.Empty<Vector2>();
        var hull = new List<Vector2>();
        // The lower chain left to right, then the upper chain back, each dropping a corner that does not turn left.
        for (int pass = 0; pass < 2; pass++)
        {
            int start = hull.Count;
            foreach (var v in pass == 0 ? p : Enumerable.Reverse(p))
            {
                while (hull.Count >= start + 2 && (hull[^1] - hull[^2]).Cross(v - hull[^2]) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(v);
            }
            hull.RemoveAt(hull.Count - 1);
        }
        return hull.Count >= 3 ? hull.ToArray() : Array.Empty<Vector2>();
    }

    /// <summary>Whether a world point lies inside a footprint, or within <paramref name="margin"/> metres outside one.</summary>
    public bool Covers(float x, float z, float margin = 0f)
    {
        var p = new Vector2(x, z);
        foreach (var o in _outlines)
        {
            bool inside = true;
            for (int i = 0; i < o.Length && inside; i++)
            {
                var edge = o[(i + 1) % o.Length] - o[i];
                inside = edge.Cross(p - o[i]) >= -margin * edge.Length();
            }
            if (inside)
                return true;
        }
        return false;
    }

    /// <inheritdoc cref="Covers(float, float, float)"/>
    public bool Covers(Vector3 at, float margin = 0f) => Covers(at.X, at.Z, margin);
}
