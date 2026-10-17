using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Airframe;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Hud;

/// <summary>
/// Moves the pilot's own wingtip lights and propeller discs onto the cockpit interior for the
/// Cockpit view, a remake-only fix in both presentations. The original keeps the outside model's
/// <c>dontmove</c> group in that view. The interior is a separate model with its own fins and
/// nacelles, so those parts float off it (#149). Each re-seated part gets a copy under the
/// interior, which <see cref="Sync"/> keeps on its original's visibility and spin. The
/// <see cref="Hidden"/> originals leave the pilot's own pane while the interior is drawn
/// (<see cref="CockpitVisibility"/>). Rules: docs/architecture/Flight.md.
/// </summary>
public sealed class CockpitReseat
{
    // Hand-fitted: the nose of each nacelle in the interior's own units, keyed by prop station. The
    // Warhawk's left nose is the centroid of its forward face; the right mirrors it across the
    // symmetric interior. Its centre prop sits behind the panel and stays.
    internal static readonly Dictionary<string, Dictionary<int, Vector3>> PropSeats =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["player_warhawk"] = new()
            {
                [2] = new(-164.5f, -35.7f, -156.9f),
                [3] = new(164.5f, -35.7f, -156.9f),
            },
        };

    // Metres a light may sit off its outside fin's peak and still count as mounted on it.
    private const float OnFinPeak = 0.15f;

    // Lights whose interior has no wingtip under them, so they hang in open air beyond its edge.
    private static readonly HashSet<string> OpenAirFlares = new(StringComparer.OrdinalIgnoreCase)
    {
        "player_fbrand/wing_flare1", "player_fbrand/wing_flare2",
        "player_pfighter/wing_flare1", "player_pfighter/wing_flare2",
        "player_kestrel/wing_flare1",
    };

    private readonly List<(Node3D Source, Node3D Copy, Basis Mount)> _pairs;

    private CockpitReseat(List<(Node3D, Node3D, Basis)> pairs, List<Node3D> hidden)
    {
        _pairs = pairs;
        Hidden = hidden;
    }

    /// <summary>The outside-model nodes the Cockpit view leaves out of the pilot's own pane. That is
    /// every re-seated original, and the lights of an airframe with nothing to seat them on.</summary>
    public IReadOnlyList<Node3D> Hidden { get; }

    /// <summary>The copies under the interior, for the suite.</summary>
    public IEnumerable<Node3D> Copies => _pairs.Select(p => p.Copy);

    /// <summary>Seats the copies on one built model whose <paramref name="interior"/> is still its
    /// child, before any pass re-parents it. Null when nothing on this airframe moves. Call after
    /// <see cref="PlaneBuilder.Build"/>, whose flare walk the copies must not join.</summary>
    public static CockpitReseat? Bind(Node3D? planeModel, Node3D? interior, string planeName)
    {
        if (planeModel == null || interior == null
            || FindGroup(planeModel, "dontmove", Transform3D.Identity) is not var (dontmove, above))
        {
            return null;
        }

        var pairs = new List<(Node3D, Node3D, Basis)>();
        var hidden = new List<Node3D>();
        var interiorPeaks = FinPeaks(interior);
        var outsidePeaks = FinPeaks(planeModel);
        var props = PropSeats.GetValueOrDefault(planeName);
        foreach (var (node, parent) in Parts(dontmove, above))
        {
            string name = node.Name.ToString();
            var at = parent * node.Transform;
            if (WingLights.IsFlare(name))
            {
                // A light on its outside fin's peak moves to the same side's interior fin peak.
                var seat = outsidePeaks.Any(p => p.DistanceTo(at.Origin) < OnFinPeak)
                    ? interiorPeaks.Where(p => Mathf.Sign(p.X) == Mathf.Sign(at.Origin.X))
                        .Select(p => (Vector3?)p).FirstOrDefault()
                    : null;
                if (seat is { } peak)
                {
                    var copy = Seat(interior, node, parent, at.Origin, peak);
                    copy.AddChild(WingLightBlinker.NewLamp(visible: true));
                    pairs.Add((node, copy, parent.Basis));
                    hidden.Add(node);
                }
                else if (OpenAirFlares.Contains($"{planeName}/{name}"))
                {
                    hidden.Add(node);
                }
            }
            else if (props != null && PropStation(name) is { } station
                && props.TryGetValue(station, out var nose)
                && StationOrigin(dontmove, above, station) is { } origin)
            {
                pairs.Add((node, Seat(interior, node, parent, origin, nose), parent.Basis));
                hidden.Add(node);
            }
        }
        return hidden.Count == 0 ? null : new CockpitReseat(pairs, hidden);
    }

    /// <summary>Each rudder's fin peak under <paramref name="root"/>, in its own frame: the highest
    /// fixed vertex on that rudder's hinge line, where the fin's trailing edge ends. The interior is
    /// skipped unless it is the root, and so are the hidden states and the prop and marker groups.</summary>
    public static List<Vector3> FinPeaks(Node3D root)
    {
        var hinges = new List<(Vector3 Origin, Vector3 Axis, float Span)>();
        var fixedPoints = new List<Vector3>();
        void Walk(Node node, Transform3D acc, bool moving)
        {
            foreach (var child in node.GetChildren())
            {
                if (child is not Node3D n3d || !n3d.Visible || IsInterior(n3d) || IsLoose(n3d))
                    continue;
                var at = acc * n3d.Transform;
                var kind = ControlSurfaces.Classify(n3d.Name.ToString());
                if (kind == ControlSurfaces.Kind.Rudder)
                {
                    var axis = (at.Basis * ControlSurfaces.HingeAxis(kind)).Normalized();
                    var along = n3d.GetChildren().OfType<MeshInstance3D>()
                        .SelectMany(Vertices).Select(v => (acc * n3d.Transform * v - at.Origin).Dot(axis))
                        .DefaultIfEmpty(0f).ToList();
                    hinges.Add((at.Origin, axis, along.Max() - along.Min()));
                }
                bool surface = moving || kind != ControlSurfaces.Kind.None;
                if (n3d is MeshInstance3D { Mesh: ArrayMesh } mi && !surface)
                    fixedPoints.AddRange(Vertices(mi).Select(v => acc * v));
                Walk(n3d, at, surface);
            }
        }
        Walk(root, Transform3D.Identity, false);

        var peaks = new List<Vector3>();
        foreach (var (origin, axis, span) in hinges)
        {
            // The fin's own trailing-edge vertices lie on the hinge line to the authored digit.
            float onLine = 0.02f * span;
            var reach = fixedPoints.Select(p => (Along: (p - origin).Dot(axis), Off: p - origin))
                .Where(q => (q.Off - q.Along * axis).Length() < onLine)
                .Select(q => (float?)q.Along).Max();
            if (reach is { } top)
                peaks.Add(origin + top * axis);
        }
        return peaks;
    }

    /// <summary>Each copy takes its original's visibility and spin. Called every frame the interior
    /// is on the screen.</summary>
    public void Sync()
    {
        foreach (var (source, copy, mount) in _pairs)
        {
            if (!GodotObject.IsInstanceValid(source))
                continue;
            copy.Visible = source.Visible;
            copy.Basis = mount * source.Basis;
        }
    }

    // The copy hangs under a seat node that undoes the interior's scale, so it keeps its authored
    // size in airframe metres. The interior's tilt stays, so a disc faces along the nacelle.
    // `reference` is the airframe point that lands on `anchor`. A station's layers share it, so
    // they keep their offsets from one another.
    private static Node3D Seat(Node3D interior, Node3D source, Transform3D parent, Vector3 reference,
        Vector3 anchor)
    {
        var seat = new Node3D
        {
            Name = $"cockpit_seat_{source.Name}",
            Transform = new Transform3D(Basis.Identity.Scaled(Vector3.One / PlaneBuilder.InteriorScale), anchor),
        };
        // ⚠ SceneCopy, never Duplicate: duplicating geometry under the render thread crashes it.
        var copy = SceneCopy.Of(source);
        // ⚠ Prefixed, never suffixed: WingLights.IsFlare and the anim name lookups match the front.
        copy.Name = $"cockpit_{source.Name}";
        Unindex(copy);
        OwnMaterials(copy);
        var at = parent * source.Transform;
        copy.Transform = new Transform3D(at.Basis, at.Origin - reference);
        seat.AddChild(copy);
        interior.AddChild(seat);
        return copy;
    }

    // ⚠ A copy carries the gamez index and source name, and AnimRuntime resolves a node by those.
    // Left on, the copy is taken for its original and the spin and nitro defs drive it.
    private static void Unindex(Node node)
    {
        node.RemoveMeta(AnimRuntime.IndexMeta);
        node.RemoveMeta(AnimRuntime.NameMeta);
        foreach (var child in node.GetChildren())
            Unindex(child);
    }

    // ⚠ The cockpit pass writes `light_origin` on every material under the interior. A material
    // shared with the outside props would carry the eye's offset into their lighting too.
    private static void OwnMaterials(Node node)
    {
        if (node is MeshInstance3D { Mesh: { } mesh } mi)
        {
            if (mi.MaterialOverride is ShaderMaterial over)
                mi.MaterialOverride = ShaderTwins.Copy(over);
            for (int s = 0; s < mesh.GetSurfaceCount(); s++)
                if ((mi.GetSurfaceOverrideMaterial(s) ?? mesh.SurfaceGetMaterial(s)) is ShaderMaterial m)
                    mi.SetSurfaceOverrideMaterial(s, ShaderTwins.Copy(m));
        }
        foreach (var child in node.GetChildren())
            OwnMaterials(child);
    }

    // Every direct part of `dontmove`, with its parent's transform in the airframe frame.
    private static IEnumerable<(Node3D Node, Transform3D Parent)> Parts(Node3D group, Transform3D above)
    {
        var acc = above * group.Transform;
        foreach (var child in group.GetChildren())
        {
            if (child is not Node3D n3d)
                continue;
            string name = n3d.Name.ToString();
            if (WingLights.IsFlare(name) || PropStation(name) != null)
                yield return (n3d, acc);
            else
                foreach (var part in Parts(n3d, acc))
                    yield return part;
        }
    }

    // The station number a propeller layer belongs to (prop2, prop2b, staticprop2, nitroprop2).
    private static int? PropStation(string name)
    {
        var kind = PropParts.Classify(name);
        if (kind is PropParts.Kind.None or PropParts.Kind.RotorMain or PropParts.Kind.RotorGhost
            || name.StartsWith("staticrotor", StringComparison.OrdinalIgnoreCase))
            return null;
        string digits = new(name.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out int n) ? n : null;
    }

    // The still disc's airframe position, the point a station's anchor is fitted to.
    private static Vector3? StationOrigin(Node3D dontmove, Transform3D above, int station) =>
        Parts(dontmove, above)
            .Where(p => p.Node.Name.ToString().Equals($"staticprop{station}", StringComparison.OrdinalIgnoreCase))
            .Select(p => (Vector3?)(p.Parent * p.Node.Transform).Origin)
            .FirstOrDefault();

    private static IEnumerable<Vector3> Vertices(MeshInstance3D mi)
    {
        if (mi.Mesh is not ArrayMesh mesh)
            yield break;
        for (int s = 0; s < mesh.GetSurfaceCount(); s++)
            foreach (var v in mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                yield return mi.Transform * v;
    }

    // The airframe's own group of this name with the transform above it, outside the interior.
    private static (Node3D Node, Transform3D Above)? FindGroup(Node3D root, string name, Transform3D above)
    {
        foreach (var n3d in root.GetChildren().OfType<Node3D>())
        {
            string own = AnimRuntime.NameOf(n3d);
            if (own.Equals(name, StringComparison.OrdinalIgnoreCase))
                return (n3d, above);
            if (!IsInterior(n3d) && FindGroup(n3d, name, above * n3d.Transform) is { } hit)
                return hit;
        }
        return null;
    }

    private static bool IsInterior(Node3D node) =>
        AnimRuntime.NameOf(node).StartsWith("cockpit", StringComparison.OrdinalIgnoreCase);

    // Groups that hang off the airframe rather than form it: the props and lights, and the markers.
    private static bool IsLoose(Node3D node) =>
        AnimRuntime.NameOf(node) is var name
        && (name.Equals("dontmove", StringComparison.OrdinalIgnoreCase)
            || name.Equals("markers", StringComparison.OrdinalIgnoreCase));
}
