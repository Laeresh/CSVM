using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CSVM.Extraction;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Applies an airframe's <see cref="PlaneRig"/> to its freshly built tree, before anything reads a
/// marker or classifies a part. It moves marker nodes and adds a target or reference marker the
/// model lacks. It stamps prop and surface overrides as the metas <see cref="PropParts"/> and
/// <see cref="ControlSurfaces"/> read. Rig vectors are plane-frame, below the root, and are turned
/// into each node's own frame here. A name the model does not carry is an <see cref="InvalidDataException"/>
/// naming the rig file and entry. ⚠ Do not let it change what a plane carries; a pylon keeps its side.
/// </summary>
public static class PlaneRigPatch
{
    // The non-weapon nodes of the original's markers group (docs/formats/markers.md, Marker group),
    // which a rig may add to a model that lacks one.
    private static readonly HashSet<string> ReferenceMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        "cockpit_camera", "exhaust1", "exhaust2", "ground_level", "ladder_pos", "cf_light",
    };

    // The alternate-state subtrees MarkerRig and PlaneBuilder skip: a name found only inside them is
    // not the airframe's, since the patch never reaches those copies.
    private static readonly HashSet<string> AltStateSubtrees = new(StringComparer.OrdinalIgnoreCase)
    {
        "cockpit1", "cockpit2", "destroyed", "player_damage_off",
    };

    /// <summary>The pack's rig for the airframe whose model node is <paramref name="airframe"/>,
    /// parsed and load-checked; null when the pack has none.</summary>
    public static PlaneRig? Load(HdModels models, string airframe)
    {
        if (models.RigFor(airframe) is not { } file)
        {
            return null;
        }

        using var reader = new StreamReader(new MemoryStream(file.Bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return PlaneRig.Parse(reader.ReadToEnd(), file.Source);
    }

    /// <summary>The rig's plane-frame position for <paramref name="marker"/>, matched without case,
    /// or null where the original's stands.</summary>
    public static Vector3? MarkerPosition(PlaneRig? rig, string marker)
    {
        foreach (var (name, pos) in rig?.Markers ?? new Dictionary<string, Vector3>())
        {
            if (name.Equals(marker, StringComparison.OrdinalIgnoreCase))
            {
                return pos;
            }
        }

        return null;
    }

    /// <summary>Applies <paramref name="rig"/> to <paramref name="built"/>, the tree built from
    /// <paramref name="root"/>, and returns the wingtip flare nodes it chooses. A name the model
    /// carries in a subtree this build skipped is accepted and changes nothing.</summary>
    public static FlareChoice Apply(PlaneRig rig, Node3D built, GameZ gamez, GameZNode root)
    {
        var known = ModelNames(gamez, root);
        var byName = new Dictionary<string, List<Node3D>>(StringComparer.OrdinalIgnoreCase);
        Index(built, byName);
        int added = MoveMarkers(rig, built, known, byName);
        StampProps(rig, built, known, byName);
        StampSurfaces(rig, built, known, byName);
        var flares = ChooseFlares(rig, built, known, byName);
        Log.Info("world", $"plane rig {rig.SourceName}: {rig.Markers.Count} markers ({added} added), {rig.Props.Count} props, {rig.Surfaces.Count} surfaces, {flares.Flares.Count} flares");
        return flares;
    }

    /// <summary>A plane-frame axis in the frame of a node whose plane-frame transform is
    /// <paramref name="frame"/>, unit length, as the classifiers' node-local axes are.</summary>
    internal static Vector3 ToNodeAxis(Transform3D frame, Vector3 planeAxis) =>
        (frame.Basis.Inverse() * planeAxis).Normalized();

    /// <summary>The node's transform in the plane frame: every local below the built root, its own included.</summary>
    internal static Transform3D PlaneFrame(Node3D node, Node3D built)
    {
        var acc = Transform3D.Identity;
        for (Node3D? n = node; n != null && n != built; n = n.GetParent() as Node3D)
        {
            acc = n.Transform * acc;
        }

        return acc;
    }

    // A marker moves to its rig position by rewriting its local origin under the parent's plane frame.
    // A missing one is added under the markers group, so Loadout's cs_name walk finds it like any other.
    private static int MoveMarkers(PlaneRig rig, Node3D built, HashSet<string> known, Dictionary<string, List<Node3D>> byName)
    {
        int added = 0;
        foreach (var (name, pos) in rig.Markers)
        {
            if (byName.TryGetValue(name, out var nodes))
            {
                var node = nodes[0];
                var parent = node.GetParent() as Node3D ?? built;
                node.Position = PlaneFrame(parent, built).AffineInverse() * pos;
            }
            else if (MayAdd(name))
            {
                var parent = byName.TryGetValue("markers", out var groups) ? groups[0] : built;
                var marker = new Node3D { Name = name };
                marker.SetMeta(AnimRuntime.NameMeta, name);
                parent.AddChild(marker);
                marker.Position = PlaneFrame(parent, built).AffineInverse() * pos;
                byName[name] = new List<Node3D> { marker };
                added++;
            }
            else if (!known.Contains(name))
            {
                throw Fail(rig, "markers." + name, "no such node on the model, and not a marker a rig may add");
            }
        }

        return added;
    }

    // A prop on a spinning name class keeps that class's axis and rate where the rig is silent. Blur
    // picks the family's blur disc or main disc. Any other node spins only as the rig spells out.
    private static void StampProps(PlaneRig rig, Node3D built, HashSet<string> known, Dictionary<string, List<Node3D>> byName)
    {
        for (int i = 0; i < rig.Props.Count; i++)
        {
            var prop = rig.Props[i];
            string label = Label("props", i, prop.Node);
            var kind = PropParts.Classify(prop.Node);
            if (prop.Blur is { } blur)
            {
                kind = kind switch
                {
                    PropParts.Kind.PropMain or PropParts.Kind.PropGhost => blur ? PropParts.Kind.PropGhost : PropParts.Kind.PropMain,
                    PropParts.Kind.RotorMain or PropParts.Kind.RotorGhost => blur ? PropParts.Kind.RotorGhost : PropParts.Kind.RotorMain,
                    _ => kind,
                };
            }

            bool spins = PropParts.Spin(kind, out var axis, out float rate);
            if (!spins && (prop.Axis == null || prop.DegPerSec == null))
            {
                throw Fail(rig, label, "the node's name gives it no spin, so it needs both 'axis' and 'deg_per_sec'");
            }

            foreach (var node in Find(rig, label, prop.Node, known, byName))
            {
                node.SetMeta(PropParts.SpinAxisMeta, prop.Axis is { } a ? ToNodeAxis(PlaneFrame(node, built), a) : axis);
                node.SetMeta(PropParts.SpinRateMeta, prop.DegPerSec ?? rate);
            }
        }
    }

    // The hinge is written plane-frame and stored node-local, the frame the animator turns a surface in.
    // ⚠ Rudder side stays without effect: the classifier has one sideless rudder kind.
    private static void StampSurfaces(PlaneRig rig, Node3D built, HashSet<string> known, Dictionary<string, List<Node3D>> byName)
    {
        for (int i = 0; i < rig.Surfaces.Count; i++)
        {
            var surface = rig.Surfaces[i];
            foreach (var node in Find(rig, Label("surfaces", i, surface.Node), surface.Node, known, byName))
            {
                var frame = PlaneFrame(node, built);
                node.SetMeta(ControlSurfaces.KindMeta, (int)surface.ClassifierKind);
                if (surface.HingeAxis is { } axis)
                {
                    node.SetMeta(ControlSurfaces.HingeAxisMeta, ToNodeAxis(frame, axis));
                }

                if (surface.HingeOrigin is { } origin)
                {
                    node.SetMeta(ControlSurfaces.HingePivotMeta, frame.AffineInverse() * origin);
                }
            }
        }
    }

    // Each original flare's side is read off its plane-frame x, port below zero, never off its digit.
    // A rig side replaces that side's original flares, which then stay dark.
    private static FlareChoice ChooseFlares(PlaneRig rig, Node3D built, HashSet<string> known, Dictionary<string, List<Node3D>> byName)
    {
        var chosen = new HashSet<Node3D>();
        var displaced = new HashSet<Node3D>();
        if (rig.LeftFlare == null && rig.RightFlare == null)
        {
            return new FlareChoice(chosen, displaced);
        }

        foreach (var list in byName.Values)
        {
            foreach (var node in list)
            {
                if (WingLights.IsFlare(node.Name))
                {
                    bool port = PlaneFrame(node, built).Origin.X < 0f;
                    ((port ? rig.LeftFlare : rig.RightFlare) == null ? chosen : displaced).Add(node);
                }
            }
        }

        foreach (var (side, name) in new[] { ("flares.left", rig.LeftFlare), ("flares.right", rig.RightFlare) })
        {
            if (name != null)
            {
                chosen.UnionWith(Find(rig, side, name, known, byName));
            }
        }

        displaced.ExceptWith(chosen);
        return new FlareChoice(chosen, displaced);
    }

    // ⚠ Never add a firepoint or pylon. Loadout.BindAi arms an AI over every one the tree carries,
    // so each added one is an added gun or store.
    private static bool MayAdd(string name) =>
        MarkerRig.Classify(name, out var kind, out _)
            ? kind == MarkerRig.MarkerKind.Target
            : ReferenceMarkers.Contains(name);

    // The built nodes the rig names: every one, since gamez names repeat; none when this build skipped them.
    private static List<Node3D> Find(PlaneRig rig, string label, string name, HashSet<string> known, Dictionary<string, List<Node3D>> byName)
    {
        if (byName.TryGetValue(name, out var nodes))
        {
            return nodes;
        }

        return known.Contains(name) ? new List<Node3D>() : throw Fail(rig, label, $"no node '{name}' on the model");
    }

    private static void Index(Node node, Dictionary<string, List<Node3D>> byName)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D n3d)
            {
                string name = AnimRuntime.NameOf(n3d);
                if (!byName.TryGetValue(name, out var list))
                {
                    byName[name] = list = new List<Node3D>();
                }

                list.Add(n3d);
            }

            Index(child, byName);
        }
    }

    private static HashSet<string> ModelNames(GameZ gamez, GameZNode root)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Walk(GameZNode node)
        {
            if (AltStateSubtrees.Contains(node.Name))
            {
                return;
            }

            names.Add(node.Name);
            foreach (int c in node.Children)
            {
                if (c >= 0 && c < gamez.Nodes.Count)
                {
                    Walk(gamez.Nodes[c]);
                }
            }
        }

        foreach (int c in root.Children)
        {
            if (c >= 0 && c < gamez.Nodes.Count)
            {
                Walk(gamez.Nodes[c]);
            }
        }

        return names;
    }

    private static string Label(string section, int index, string node) =>
        $"{section}[{index.ToString(CultureInfo.InvariantCulture)}] ({node})";

    private static InvalidDataException Fail(PlaneRig rig, string label, string problem) =>
        new($"plane rig '{rig.SourceName}': {label}: {problem}");
}

/// <summary>The wingtip flares a rig chooses: <see cref="Flares"/> blink in place of the name match,
/// and <see cref="Displaced"/>, the originals a rig side replaced, stay dark. Both are empty for a
/// rig with no flare section, which keeps the name match.</summary>
public sealed record FlareChoice(IReadOnlySet<Node3D> Flares, IReadOnlySet<Node3D> Displaced);
