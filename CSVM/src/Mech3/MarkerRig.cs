using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// A player aircraft's weapon marker rig, read from planes.zbd GameZ: walks a
/// <c>player_*</c> root, accumulating locals down to each <c>firepoint*</c>/<c>pylon*</c>/
/// <c>target</c>, and reports plane-frame positions plus co-located groups. See
/// <c>docs/formats/markers.md</c> for the full decode.
/// This is the committed instrument <c>markers.md</c> regenerates from; <c>--dump-markers</c>
/// and <c>UI.Overlays.MarkerOverlay</c> share it so the gizmos and the dumped table agree.
/// </summary>
public sealed class MarkerRig
{
    /// <summary>Positions closer than this (metres) count as one physical mount. The data reuses
    /// the exact same coordinate for a shared mount, so any small positive tolerance separates
    /// "same point" from the next-nearest firepoint (the closest distinct pair here is ~0.5 m).</summary>
    public const float CoLocateTolerance = 1e-2f;

    /// <summary>The 11 player airframes, model-node name → display name (see markers.md's mapping
    /// table). The dump tool iterates this when no plane is named; a display name only makes the
    /// table readable, so an airframe missing from the data is skipped, not an error.</summary>
    public static readonly IReadOnlyList<(string Model, string Display)> PlayerAirframes = new[]
    {
        ("player_bhawk", "Bloodhawk"),
        ("player_pfighter", "Devastator"),
        ("player_fury", "Fury"),
        ("player_warhawk", "Warhawk"),
        ("player_autogyro", "Hoplite"),
        ("player_avenger", "Hellhound"),
        ("player_balmoral", "Balmoral"),
        ("player_brigand", "Brigand"),
        ("player_fbrand", "Firebrand"),
        ("player_kestrel", "Kestrel"),
        ("player_peacemaker", "Peacemaker"),
    };

    // Alternate-state subtrees that may carry their own same-named reference node distinct from
    // the plane's one authored marker in the top-level `markers` group, the cockpit interior
    // models and the wreck. Mirrors PlaneBuilder's own skip list for exactly this reason: without
    // it, a first-match walk over the whole subtree could resolve to a `cockpit1`/`cockpit2` copy
    // of `cockpit_camera` instead of the authored one (docs/formats/markers.md).
    private static readonly HashSet<string> AltStateSubtrees = new(StringComparer.OrdinalIgnoreCase)
    {
        "cockpit1", "cockpit2", "destroyed", "player_damage_off",
    };

    private MarkerRig(string planeRoot, List<Marker> markers, List<IReadOnlyList<int>> coLocated, List<GaugePose>? cockpit)
    {
        PlaneRoot = planeRoot;
        Markers = markers;
        CoLocated = coLocated;
        Cockpit = cockpit;
    }

    public enum MarkerKind { Firepoint, Pylon, Target }

    public string PlaneRoot { get; }

    /// <summary>Every firepoint/pylon/target under the plane, sorted kind then ordinal.</summary>
    public IReadOnlyList<Marker> Markers { get; }

    /// <summary>Groups of ≥2 indices into <see cref="Markers"/> that share a position, two gun
    /// groups on one mount. Empty for the airframes whose every firepoint is distinct.</summary>
    public IReadOnlyList<IReadOnlyList<int>> CoLocated { get; }

    /// <summary>The body cockpit template's poses (<see cref="CockpitPoses"/>); null when the airframe
    /// has no <c>cockpit1</c>.</summary>
    public IReadOnlyList<GaugePose>? Cockpit { get; }

    /// <summary>Classifies a node name as a weapon marker. Returns false for everything else in
    /// the markers group (cockpit_camera, exhaust*, ground_level, …) and for the bare, unnumbered
    /// <c>firepoint</c>/<c>pylon</c> the AI airframes carry, a numeric suffix is required, which
    /// every player firepoint/pylon has.</summary>
    public static bool Classify(string name, out MarkerKind kind, out int ordinal)
    {
        kind = MarkerKind.Target;
        ordinal = 0;
        if (name.Equals("target", StringComparison.OrdinalIgnoreCase))
        {
            kind = MarkerKind.Target;
            return true;
        }
        if (TrySuffix(name, "firepoint", out ordinal))
        {
            kind = MarkerKind.Firepoint;
            return true;
        }
        if (TrySuffix(name, "pylon", out ordinal))
        {
            kind = MarkerKind.Pylon;
            return true;
        }
        return false;
    }

    /// <summary>Reads a plane's marker rig from GameZ, or null when the root node is absent.
    /// Walks the subtree below the plane root, accumulating each node's local transform, so a
    /// marker's reported position is its origin in the airframe frame.</summary>
    public static MarkerRig? Extract(GameZ gamez, string planeRoot)
    {
        var root = gamez.FindByName(planeRoot);
        if (root == null)
        {
            return null;
        }
        var markers = new List<Marker>();
        void Walk(GameZNode node, Transform3D acc)
        {
            acc *= node.Local ?? Transform3D.Identity;
            if (Classify(node.Name, out var kind, out int ordinal))
            {
                markers.Add(new Marker(node.Name, kind, ordinal, acc.Origin));
            }
            foreach (int c in node.Children)
            {
                if (c >= 0 && c < gamez.Nodes.Count)
                {
                    Walk(gamez.Nodes[c], acc);
                }
            }
        }
        // Below the root only: the root's own local transform places the airframe in a scene,
        // which is not part of the plane frame the markers.md positions are quoted in.
        foreach (int c in root.Children)
        {
            if (c >= 0 && c < gamez.Nodes.Count)
            {
                Walk(gamez.Nodes[c], Transform3D.Identity);
            }
        }
        markers.Sort(static (a, b) => a.Kind != b.Kind
            ? a.Kind.CompareTo(b.Kind)
            : a.Ordinal.CompareTo(b.Ordinal));
        var coLocated = GroupCoLocated(markers.ConvertAll(m => m.Local), CoLocateTolerance);
        return new MarkerRig(planeRoot, markers, coLocated, CockpitPoses(gamez, root));
    }

    /// <summary>Where the flat view shows each of <c>cockpit1</c>'s instruments, in the plane frame.
    /// Every child of its <c>gauges</c> node comes in file order, then the sight glass under
    /// <see cref="RigCockpit.GlassKey"/> at <see cref="CockpitTransplant.GlassFrame"/>. Each is carried
    /// through <see cref="PlaneBuilder.InteriorToPlane"/> on the gamez <c>cockpit_camera</c>. An assembly
    /// holding the glass stands in for the key, which a rig may not name beside it. Null without
    /// <c>cockpit1</c>.</summary>
    public static List<GaugePose>? CockpitPoses(GameZ gamez, GameZNode root)
    {
        var interior = gamez.ChildrenOf(root).Find(n => n.Name.Equals("cockpit1", StringComparison.OrdinalIgnoreCase));
        if (interior == null)
        {
            return null;
        }

        var marker = FindNamedMarker(gamez, root.Name, "cockpit_camera");
        var poses = new List<GaugePose>();
        bool held = false;
        // Below cockpit1, whose own local the built mount replaces; depth-first, as the transplant searches.
        if (FindGauges(gamez, interior, Transform3D.Identity) is var (gauges, at))
        {
            foreach (var assembly in gamez.ChildrenOf(gauges))
            {
                held |= GunsightReticle.HoldsGlass(gamez, assembly);
                poses.Add(new GaugePose(assembly.Name, PlaneBuilder.InteriorToPlane(at * (assembly.Local ?? Transform3D.Identity), marker)));
            }
        }

        if (!held && CockpitTransplant.GlassFrame(GunsightReticle.Find(gamez, interior)) is { } glass)
        {
            poses.Add(new GaugePose(RigCockpit.GlassKey, PlaneBuilder.InteriorToPlane(glass, marker)));
        }

        return poses;
    }

    /// <summary>Reads one non-weapon marker node's plane-frame position by name, e.g.
    /// <c>cockpit_camera</c>, by the same accumulate-from-below-root walk <see cref="Extract"/>
    /// uses for weapon markers, skipping the alternate-state subtrees above. Returns
    /// <paramref name="fallback"/> (default the origin) when the plane root or the named node is
    /// absent, the original's own fallback for a plane with no such node.</summary>
    public static Vector3 FindNamedMarker(GameZ gamez, string planeRoot, string nodeName, Vector3 fallback = default)
    {
        var root = gamez.FindByName(planeRoot);
        if (root == null)
        {
            return fallback;
        }
        Vector3? found = null;
        void Walk(GameZNode node, Transform3D acc)
        {
            if (found != null || AltStateSubtrees.Contains(node.Name))
            {
                return;
            }
            acc *= node.Local ?? Transform3D.Identity;
            if (node.Name.Equals(nodeName, StringComparison.OrdinalIgnoreCase))
            {
                found = acc.Origin;
                return;
            }
            foreach (int c in node.Children)
            {
                if (found != null)
                {
                    return;
                }
                if (c >= 0 && c < gamez.Nodes.Count)
                {
                    Walk(gamez.Nodes[c], acc);
                }
            }
        }
        // Below the root only, same convention as Extract: the root's own transform places the
        // airframe in a scene, which is not part of the plane frame the offset is quoted in.
        foreach (int c in root.Children)
        {
            if (found != null)
            {
                break;
            }
            if (c >= 0 && c < gamez.Nodes.Count)
            {
                Walk(gamez.Nodes[c], Transform3D.Identity);
            }
        }
        return found ?? fallback;
    }

    /// <summary>Groups indices whose positions fall within <paramref name="tolerance"/> of each
    /// other, returning only the groups of two or more. Shared by the dump and the viewer overlay
    /// so both flag the same co-located mounts. O(n²), which is nothing for ~17 markers.</summary>
    public static List<IReadOnlyList<int>> GroupCoLocated(IReadOnlyList<Vector3> positions, float tolerance)
    {
        float tolSq = tolerance * tolerance;
        var groups = new List<IReadOnlyList<int>>();
        var claimed = new bool[positions.Count];
        for (int i = 0; i < positions.Count; i++)
        {
            if (claimed[i])
            {
                continue;
            }
            List<int>? group = null;
            for (int j = i + 1; j < positions.Count; j++)
            {
                if (!claimed[j] && positions[i].DistanceSquaredTo(positions[j]) <= tolSq)
                {
                    group ??= new List<int> { i };
                    group.Add(j);
                    claimed[j] = true;
                }
            }
            if (group != null)
            {
                claimed[i] = true;
                groups.Add(group);
            }
        }
        return groups;
    }

    /// <summary>One human-readable dump block for this plane: a header line, then every marker
    /// with its plane-frame position and, for firepoints, its mirror pair, and, for any
    /// co-located marker, the other names sharing that mount. This is the text the
    /// <c>--dump-markers</c> tool prints and the markers.md tables regenerate from.</summary>
    public string Format(string? display = null)
    {
        int firepoints = 0, pylons = 0;
        bool hasTarget = false;
        foreach (var m in Markers)
        {
            switch (m.Kind)
            {
                case MarkerKind.Firepoint: firepoints++; break;
                case MarkerKind.Pylon: pylons++; break;
                case MarkerKind.Target: hasTarget = true; break;
            }
        }
        // index → the group's other member names, for the ≡ annotation
        var shared = new Dictionary<int, string>();
        foreach (var group in CoLocated)
        {
            foreach (int idx in group)
            {
                var others = new List<string>();
                foreach (int other in group)
                {
                    if (other != idx)
                    {
                        others.Add(Markers[other].Name);
                    }
                }
                shared[idx] = string.Join(", ", others);
            }
        }
        var ordinals = new HashSet<int>();
        foreach (var m in Markers)
        {
            if (m.Kind == MarkerKind.Firepoint)
            {
                ordinals.Add(m.Ordinal);
            }
        }

        var sb = new StringBuilder();
        string title = display != null ? $"{display} ({PlaneRoot})" : PlaneRoot;
        sb.Append("=== ").Append(title).Append(": ")
            .Append(firepoints).Append(" firepoints, ")
            .Append(pylons).Append(" pylons")
            .Append(hasTarget ? ", target" : ", no target")
            .Append(CoLocated.Count > 0 ? $", {CoLocated.Count} co-located pair(s)" : "")
            .AppendLine(" ===");
        for (int i = 0; i < Markers.Count; i++)
        {
            var m = Markers[i];
            sb.Append("  ").Append(m.Name.PadRight(12)).Append(Vec(m.Local));
            if (m.Kind == MarkerKind.Firepoint)
            {
                int partner = MirrorPartner(m.Ordinal);
                sb.Append(ordinals.Contains(partner)
                    ? $"   pair(fp{Math.Min(m.Ordinal, partner)},fp{Math.Max(m.Ordinal, partner)})"
                    : "   (centreline)");
            }
            if (shared.TryGetValue(i, out var others))
            {
                sb.Append("   ≡ ").Append(others);
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>A starting <c>rig.json</c> for this plane, in the schema <see cref="PlaneRig.Parse"/> reads.
    /// Its <c>markers</c> section holds every marker's plane-frame position. With a <c>cockpit1</c>, a body
    /// <c>cockpit</c> section poses every instrument where <c>cockpit1</c> drew it. Props,
    /// surfaces and flares are left out, since an absent section keeps the original's values.</summary>
    public string FormatRigTemplate() => RigTemplate(Markers, Cockpit);

    // A repeated name keeps its first position, since a rig names each marker once. Values print as
    // the shortest text that reads back to the same float, so an unedited template moves nothing.
    internal static string RigTemplate(IEnumerable<Marker> markers, IReadOnlyList<GaugePose>? cockpit = null)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();
        foreach (var m in markers)
        {
            if (seen.Add(m.Name))
            {
                lines.Add($"    {JsonSerializer.Serialize(m.Name)}: {Vec3(m.Local)}");
            }
        }
        var sb = new StringBuilder();
        sb.Append("{\n  \"markers\": {");
        sb.Append(lines.Count == 0 ? "" : "\n" + string.Join(",\n", lines) + "\n  ");
        sb.Append('}');
        if (cockpit != null)
        {
            // Rounded to a micrometre and a ten-thousandth of a degree, below anything a pilot sees.
            // Float noise such as 2E-14 then never reaches the author.
            var gauges = cockpit.Select(g => $"      {JsonSerializer.Serialize(g.Name)}: {{ \"position\": {Vec3(g.Pose.Origin, 6)}, "
                + $"\"rotation\": {Vec3(g.RotationDeg, 4)}, \"scale\": {Num(Round(g.Scale, 7))} }}").ToList();
            sb.Append(",\n  \"cockpit\": {\n    \"from\": \"body\",\n    \"gauges\": {");
            sb.Append(gauges.Count == 0 ? "" : "\n" + string.Join(",\n", gauges) + "\n    ");
            sb.Append("}\n  }");
        }
        sb.Append("\n}\n");
        return sb.ToString();
    }

    private static (GameZNode Node, Transform3D At)? FindGauges(GameZ gamez, GameZNode node, Transform3D at)
    {
        foreach (var child in gamez.ChildrenOf(node))
        {
            var childAt = at * (child.Local ?? Transform3D.Identity);
            if (child.Name.Equals("gauges", StringComparison.OrdinalIgnoreCase))
            {
                return (child, childAt);
            }

            if (FindGauges(gamez, child, childAt) is { } hit)
            {
                return hit;
            }
        }

        return null;
    }

    private static string Vec3(Vector3 v) => $"[{Num(v.X)}, {Num(v.Y)}, {Num(v.Z)}]";

    private static string Vec3(Vector3 v, int digits) => Vec3(new Vector3(Round(v.X, digits), Round(v.Y, digits), Round(v.Z, digits)));

    // Adding zero turns a rounded -0 into 0.
    private static float Round(float v, int digits) => (float)Math.Round(v, digits) + 0f;

    private static string Num(float v) => v.ToString("R", CultureInfo.InvariantCulture);

    private static bool TrySuffix(string name, string prefix, out int ordinal)
    {
        ordinal = 0;
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return int.TryParse(name.AsSpan(prefix.Length), NumberStyles.None,
            CultureInfo.InvariantCulture, out ordinal) && ordinal > 0;
    }

    // The mirror partner ordinal of a firepoint: gun pairs are consecutive
    // (fp1,fp2)(fp3,fp4)…, so odd n pairs with n+1 and even n with n−1. The Kestrel's fp7 is the
    // one centreline mount with no fp8, its partner simply isn't in the rig.
    private static int MirrorPartner(int ordinal) => (ordinal % 2 == 1) ? ordinal + 1 : ordinal - 1;

    private static string Vec(Vector3 v) => string.Format(CultureInfo.InvariantCulture,
        "( {0,7:+0.00;-0.00; 0.00}, {1,7:+0.00;-0.00; 0.00}, {2,7:+0.00;-0.00; 0.00} )", v.X, v.Y, v.Z);

    /// <param name="Ordinal">The trailing number (<c>firepoint7</c> → 7); 0 for <c>target</c>.</param>
    /// <param name="Local">Plane-frame position (metres), relative to the airframe origin.</param>
    public readonly record struct Marker(string Name, MarkerKind Kind, int Ordinal, Vector3 Local);

    /// <param name="Name">A <c>gauges</c> assembly, or <see cref="RigCockpit.GlassKey"/>.</param>
    /// <param name="Pose">Plane-frame transform where the flat view shows it.</param>
    public readonly record struct GaugePose(string Name, Transform3D Pose)
    {
        /// <summary>The rotation as a rig writes it, Euler degrees in YXZ order (<see cref="RigGaugePose"/>).</summary>
        public Vector3 RotationDeg => Pose.Basis.Orthonormalized().GetEuler(EulerOrder.Yxz) * (180f / Mathf.Pi);

        /// <summary>The uniform scale as a rig writes it, the mean of the basis's three axis lengths.</summary>
        public float Scale => (Pose.Basis.Scale.X + Pose.Basis.Scale.Y + Pose.Basis.Scale.Z) / 3f;
    }
}
