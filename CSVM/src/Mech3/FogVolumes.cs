using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>One <c>fvol*</c> node of the world: the authored volume a fog volume occupies, in
/// world coordinates. No visible geometry, <see cref="WorldBuilder.SkipWorldNode"/> excludes it
/// from the render. Decode: docs/formats/fogvol.md.
/// ⚠ <see cref="Box"/> is the axis-aligned bounds only; use <see cref="Contains"/> for the
/// authored shape. Named <c>…Box</c> because <c>Godot.FogVolume</c> is a real engine type this is
/// not, see <see cref="FogVolumeWhiteout"/> for what actually renders from it.</summary>
/// <param name="Name">The gamez node's own name, e.g. <c>fvol10</c>.</param>
/// <param name="Box">World-space axis-aligned bounds of the node's mesh.</param>
/// <param name="Faces">The mesh's distinct face planes, outward-facing, see
/// <see cref="Contains"/>.</param>
/// <param name="Polygons">The mesh's own faces, the surfaces the cloud lattice is laid on
/// (<see cref="FogVolumeFace"/>). Null for a hand-built volume.</param>
public readonly record struct FogVolumeBox(
    string Name, Aabb Box, IReadOnlyList<Plane> Faces, IReadOnlyList<FogVolumeFace>? Polygons = null)
{
    // Slack on the face test, in metres. A placement drawn AT a face, which any rule that anchors
    // the spread to a wall does by construction, must read as inside, and float error at map
    // scale (coordinates to 16 km) is four orders of magnitude below this.
    private const float Slack = 0.05f;

    // ExteriorDistance's projection loop. The tolerance is a whole cycle's movement, in metres:
    // 1e-4 is four orders below the tightest authored ramp this feeds (C5's 16 m fog_fade_dist),
    // so the ramp cannot see it. 64 cycles is a guard against a pathological shape, not the usual
    // exit, an axis-aligned box converges in one and every shipped volume in a handful.
    private const float ProjectionTolerance = 1e-4f;
    private const int MaxProjectionCycles = 64;

    // How many face planes ExteriorDistance keeps its corrections on the stack for. The widest
    // shipped volume is far under this (C1C's fvol9 is the busiest at 5 distinct planes over 55
    // polygons), so no shipped chapter ever takes the heap fallback.
    private const int MaxStackFaces = 32;

    /// <summary>Is this world point inside the authored volume? A half-space test over the mesh's
    /// own faces, EXACT because every shipped volume is convex (docs/formats/fogvol.md). A sloped
    /// frustum face narrows the test with height on its own, independent of the vertical placement
    /// rule.</summary>
    public bool Contains(Vector3 point)
    {
        foreach (var face in Faces)
        {
            if (face.DistanceTo(point) > Slack)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>The binary's own signed distance to this volume: outside-positive,
    /// inside-negative, the max over the face planes. EXACT inside, the penetration depth
    /// <see cref="FogVolumeWhiteout"/>'s interior ramp decays over, but only a LOWER BOUND
    /// outside, understating near an edge or corner; <see cref="ExteriorDistance"/> is exact
    /// there. <see cref="float.MaxValue"/> for a volume with no faces (never shipped).
    /// Decode: docs/formats/fogvol.md.</summary>
    public float SignedDistance(Vector3 point)
    {
        if (Faces.Count == 0)
        {
            return float.MaxValue;
        }
        float worst = float.MinValue;
        foreach (var face in Faces)
        {
            float d = face.DistanceTo(point);
            if (d > worst)
            {
                worst = d;
            }
        }
        return worst;
    }

    /// <summary>The Euclidean distance from an outside point to this volume's authored shape, 0
    /// inside, the binary's approach-ramp quantity. Dykstra's alternating projection onto the
    /// face half-spaces: exact for the convex hull, unlike a face-plane-only bound
    /// (<see cref="SignedDistance"/>), which understates near an edge or corner.
    /// ⚠ Do not replace the iterative loop with a closed form; only a box is exact in one cycle.
    /// Decode + measurements: docs/formats/fogvol.md.</summary>
    public float ExteriorDistance(Vector3 point)
    {
        int n = Faces.Count;
        if (n == 0)
        {
            return 0f;
        }
        float bound = SignedDistance(point);
        if (bound <= 0f)
        {
            return 0f;   // inside: the ramp's interior half owns this point
        }
        if (n == 1)
        {
            return bound;   // one half-space: the plane distance IS the projection
        }

        Span<Vector3> corrections = stackalloc Vector3[MaxStackFaces];
        if (n > MaxStackFaces)
        {
            corrections = new Vector3[n];
        }
        corrections = corrections[..n];
        corrections.Clear();

        var x = point;
        for (int cycle = 0; cycle < MaxProjectionCycles; cycle++)
        {
            float moved = 0f;
            for (int i = 0; i < n; i++)
            {
                var face = Faces[i];
                var y = x + corrections[i];
                float over = face.DistanceTo(y);
                var projected = over > 0f ? y - (face.Normal * over) : y;
                corrections[i] = y - projected;
                moved += projected.DistanceSquaredTo(x);
                x = projected;
            }
            if (moved <= ProjectionTolerance * ProjectionTolerance)
            {
                break;
            }
        }
        return point.DistanceTo(x);
    }

    /// <summary>True when this volume's authored shape IS its own axis-aligned bounds, every
    /// corner of <see cref="Box"/> passes <see cref="Contains"/>. Per-chapter census:
    /// docs/formats/fogvol.md. Shared by the test census and
    /// <see cref="FogVolumeSpec.FindMapSpanningSlab"/>, so it lives on the type rather than being
    /// duplicated at each call site.</summary>
    public bool IsAxisAlignedBox()
    {
        for (int corner = 0; corner < 8; corner++)
        {
            if (!Contains(Box.GetEndpoint(corner)))
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>One authored face of an <c>fvol</c> volume, in world coordinates: the surface the cloud
/// lattice is laid over, and the normal every sprite it carries scales its draw distance against.
/// Triangle strips are already split and the <c>no_clutter</c> polygons already dropped, so this
/// list is exactly what the original's own polygon walk hands its scatter.
/// Decode: docs/org/cloudCards.md.</summary>
/// <param name="Vertices">The polygon's own vertex loop, at least three, world space.</param>
/// <param name="Normal">Unit, pointing away from the volume's interior.</param>
public readonly record struct FogVolumeFace(IReadOnlyList<Vector3> Vertices, Vector3 Normal)
{
    // Slack on the outline test, in metres, the same tolerance FogVolumeBox.Contains gives its
    // half-spaces: a lattice point landing exactly on an edge must read as inside, and float error
    // at map scale (coordinates to 16 km) is orders of magnitude below this.
    private const float Slack = 0.05f;

    /// <summary>Is this in-plane point inside the face's own outline? The signed perpendicular
    /// distance to each edge, taken against <see cref="Normal"/>: inside is one consistent sign for
    /// every edge. EXACT for a convex face, which every shipped <c>fvol</c> polygon is, and the
    /// winding is not assumed, since a strip's triangles alternate it.</summary>
    public bool Contains(Vector3 point)
    {
        int n = Vertices.Count;
        float lowest = float.MaxValue, highest = float.MinValue;
        for (int i = 0; i < n; i++)
        {
            var a = Vertices[i];
            var edge = Vertices[(i + 1) % n] - a;
            float length = edge.Length();
            if (length <= 0f)
            {
                continue;   // a repeated corner bounds nothing
            }
            float side = (edge / length).Cross(point - a).Dot(Normal);
            lowest = Mathf.Min(lowest, side);
            highest = Mathf.Max(highest, side);
        }
        return lowest >= -Slack || highest <= Slack;
    }
}

/// <summary>One chapter's map-spanning cloud slab, the combined footprint of every top-anchored,
/// axis-aligned <c>fvol*</c> volume that tiles it exactly (see
/// <see cref="FogVolumeSpec.FindMapSpanningSlab"/>). <see cref="MinX"/>/<see cref="MaxX"/>/
/// <see cref="MinZ"/>/<see cref="MaxZ"/> are the combined bounds (C1/C1C/C2B/C4: the base map's
/// own <c>World.area</c>, to the metre, the nine pieces partition it exactly 3x3); <see cref="TopY"/>
/// is the one authored altitude every piece's own top agrees on.</summary>
public readonly record struct MapSpanningSlab(float MinX, float MaxX, float MinZ, float MaxZ, float TopY);

/// <summary>One weighted template reference of a clutter block's <c>nodes</c> list: the gamez
/// template node to scatter, and its relative weight among the block's alternatives.</summary>
public readonly record struct FogClutterNode(float Weight, string Node);

/// <summary>The chapter's in-volume whiteout: the camera-space density the original computes every
/// frame from the <c>fvol*</c> volumes when <c>fogvol.zrd</c>'s <c>fog_zone</c> is set (C5 only).
/// Two linear ramps over <see cref="FogVolumeBox.SignedDistance"/>: approach outside, decay inside.
/// Volumes union as <c>a + b − a·b</c>. Pure and off-engine
/// (<c>CSVM.Tests/FogVolumeWhiteoutTests.cs</c>); <c>Session/WeatherRig.Tick</c> is the one
/// consumer. Decode: docs/formats/fogvol.md.
/// ⚠ Do not invert the interior ramp to "fix" its backwards reading, it is a transition curtain,
/// and <c>ZONE3</c>'s own fog carries the interior look once the camera is inside.</summary>
public sealed class FogVolumeWhiteout
{
    /// <summary>The loader's defaults for a chapter that arms <c>fog_zone</c> but authors no
    /// distances. No shipped chapter is in that state, C5 authors 16/16,
    /// but the defaults are decoded, so they are stated rather than invented at the call site.</summary>
    public const float DefaultFadeDist = 400f;

    /// <inheritdoc cref="DefaultFadeDist"/>
    public const float DefaultInteriorFadeDist = 20f;

    /// <summary>The seven chapters that do not arm <c>fog_zone</c>, and every world built before
    /// one is loaded: <see cref="Density"/> is 0 at every position, with no geometry walked.</summary>
    public static readonly FogVolumeWhiteout Disarmed =
        new(false, DefaultFadeDist, DefaultInteriorFadeDist, null, Array.Empty<FogVolumeBox>());

    private readonly IReadOnlyList<FogVolumeBox> _volumes;

    private FogVolumeWhiteout(
        bool armed, float fadeDist, float interiorFadeDist, Color? color, IReadOnlyList<FogVolumeBox> volumes)
    {
        Armed = armed;
        FadeDist = fadeDist;
        InteriorFadeDist = interiorFadeDist;
        Color = color;
        _volumes = volumes;
    }

    /// <summary>Whether this chapter's <c>fogvol.zrd</c> arms the whiteout at all
    /// (<see cref="FogVolumeSpec.FogZoneArmed"/>). False short-circuits <see cref="Density"/> to 0
    /// before any geometry is touched, C1's nine volumes must never whiteout.</summary>
    public bool Armed { get; }

    /// <summary><c>fog_fade_dist</c>, the approach ramp's length in metres (C5: 16).</summary>
    public float FadeDist { get; }

    /// <summary><c>interior_fog_fade_dist</c>, the interior decay's depth in metres (C5: 16).</summary>
    public float InteriorFadeDist { get; }

    /// <summary>The authored <c>fog_color</c>, normalized 0..1, or null when the file omits it (the
    /// original's default is the mission's <c>CLOUD_COVER</c> <c>TOP_COLOR</c>, resolved by the
    /// caller, <c>WeatherRig.Tick</c>). Normalized by the same integer-vs-float rule
    /// <c>WeatherState.ParseColor</c> uses: any component above 1 means the triple is 0–255.
    /// ⚠ A DX7 framebuffer (sRGB) value; never linearise it, the overlay is a <c>ColorRect</c>,
    /// not a shader input. Decode: docs/formats/fogvol.md.</summary>
    public Color? Color { get; }

    /// <summary>Builds a chapter's whiteout from its parsed <c>fogvol.zrd</c> and its gamez volume
    /// census. A null or disarmed spec gives <see cref="Disarmed"/>, so "this chapter has no
    /// in-volume whiteout" is one object rather than a flag every caller re-tests.</summary>
    public static FogVolumeWhiteout From(FogVolumeSpec? spec, IReadOnlyList<FogVolumeBox> volumes)
    {
        if (spec is not { FogZoneArmed: true })
        {
            return Disarmed;
        }
        Color? color = null;
        if (spec.FogColor is { } c)
        {
            // The same integer-vs-float rule WeatherState.ParseColor uses; C5's [16,16,16] is a
            // 0-255 triple.
            float scale = c.X > 1f || c.Y > 1f || c.Z > 1f ? 1f / 255f : 1f;
            color = new Color(c.X * scale, c.Y * scale, c.Z * scale);
        }
        return new FogVolumeWhiteout(
            true,
            spec.FogFadeDist ?? DefaultFadeDist,
            spec.InteriorFogFadeDist ?? DefaultInteriorFadeDist,
            color,
            volumes);
    }

    /// <summary>One volume's own whiteout density at a point, 0..1, the two decompiled ramps over
    /// <see cref="FogVolumeBox.SignedDistance"/>. Public and static: this is the RULE the tests
    /// assert; <see cref="Density"/> only unions it. Takes the cheap signed-distance bound first and
    /// pays for <see cref="FogVolumeBox.ExteriorDistance"/> only when that bound is inside the
    /// ramp.</summary>
    public static float VolumeDensity(
        in FogVolumeBox volume, Vector3 point, float fadeDist, float interiorFadeDist)
    {
        float signed = volume.SignedDistance(point);
        if (signed <= 0f)
        {
            // Inside. Penetration depth = -signed, exact for a convex polytope; the ramp decays
            // from full AT the wall to nothing at interiorFadeDist deep.
            if (interiorFadeDist <= 0f)
            {
                return signed < 0f ? 0f : 1f;   // a zero-depth curtain: only the wall itself
            }
            return Mathf.Clamp((interiorFadeDist + signed) / interiorFadeDist, 0f, 1f);
        }
        if (fadeDist <= 0f || signed >= fadeDist)
        {
            return 0f;   // beyond the ramp, signed under-estimates the true distance, so this is safe
        }
        float distance = volume.ExteriorDistance(point);
        return distance >= fadeDist ? 0f : (fadeDist - distance) / fadeDist;
    }

    /// <summary>The whiteout density at a camera position, 0..1: every volume's own ramp unioned as
    /// <c>a + b − a·b</c>. 0 when the chapter does not arm <c>fog_zone</c>, without walking any
    /// geometry.</summary>
    public float Density(Vector3 camera)
    {
        if (!Armed)
        {
            return 0f;
        }
        float acc = 0f;
        foreach (var volume in _volumes)
        {
            float d = VolumeDensity(volume, camera, FadeDist, InteriorFadeDist);
            if (d <= 0f)
            {
                continue;
            }
            acc += d - (acc * d);
            if (acc >= 1f)
            {
                return 1f;   // saturated; no later volume can change it
            }
        }
        return acc;
    }
}

/// <summary>
/// One <c>clutter</c> block of <c>fogvol.zrd</c>, a weighted alternative in the chapter's
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

    /// <summary><c>far_fade_range[0]</c>, one endpoint of the per-sprite fade band (metres, start
    /// then gone). The scatter draws one <c>t</c> per placement and interpolates this pair with
    /// <see cref="FarFade"/>, so neither pair is ever rendered on its own.</summary>
    public Vector2 FarFadeNear { get; init; }

    /// <summary><c>far_fade_range[1]</c>, the other endpoint of the per-sprite band, and the
    /// widest band any sprite can draw. Decode: docs/org/cloudCards.md.</summary>
    public Vector2 FarFade { get; init; }

    /// <summary><c>perp_dist_range</c>, the placement's offset perpendicular to the volume's
    /// horizontal plane, i.e. vertical metres (min, max).</summary>
    public Vector2 PerpDistRange { get; init; }

    /// <summary><c>perturb_dist_range</c>, how far the placement is displaced from the point drawn
    /// in its cell, within that plane (min, max metres).</summary>
    public Vector2 PerturbDistRange { get; init; }

    /// <summary><c>scale_range</c>, the multiplier on the template sprite's own authored size.</summary>
    public Vector2 ScaleRange { get; init; } = Vector2.One;
}

/// <summary>
/// The chapter's <c>zrdr/fogvol.zrd</c>: the fog volumes' parameters and the clutter table the
/// original scatters through them. Full schema, per-chapter values and the decoded/undecoded
/// split: docs/formats/fogvol.md.
///
/// <para>The volumes themselves are gamez geometry, not part of this file, see
/// <see cref="VolumesOf"/>.</para>
/// </summary>
public sealed class FogVolumeSpec
{
    /// <summary><c>fog_zone</c>, present in the five chapters that ship fog volumes (0, except
    /// C5's 1). A BOOL arming the in-volume whiteout and camera state 3 (the loader stores
    /// <c>value != 0</c>), consumed through <see cref="FogZoneArmed"/>. ⚠ It is NOT the
    /// sky/fog zone selector, see docs/formats/fogvol.md.</summary>
    public int? FogZone { get; init; }

    /// <summary>Whether this chapter's fogvol arms the in-volume whiteout + camera state 3
    /// (the loader stores <c>value != 0</c>). C5 (<c>FogZone</c> 1) is the only chapter
    /// this is true for, C1/C1C/C2B/C4 ship <c>FogZone</c> 0 (present, disarmed) and C1B/C2/C3
    /// carry no <c>fog_zone</c> key at all (<see cref="FogZone"/> null). Consumed by
    /// <see cref="WeatherState.CameraWeatherState"/>'s state-3 test.</summary>
    public bool FogZoneArmed => FogZone is { } zone && zone != 0;

    /// <summary><c>distance</c>, the scatter's mean spacing in metres, i.e. the side of the cell
    /// that carries one placement (130 in C1/C1C/C2B/C4, 80 in C5; the degenerate copies carry
    /// 206.25). An areal density, not a lattice phase, docs/formats/fogvol.md.</summary>
    public float Distance { get; init; }

    /// <summary><c>fog_fade_dist</c> / <c>interior_fog_fade_dist</c> / <c>fog_color</c>, C5 only
    /// (16 / 16 / [16,16,16]), the in-volume whiteout's approach ramp, interior decay depth and
    /// colour. Consumed by <see cref="FogVolumeWhiteout"/>, which also holds the loader's
    /// defaults for a chapter that arms <c>fog_zone</c> without authoring them
    /// (docs/formats/fogvol.md).</summary>
    public float? FogFadeDist { get; init; }

    /// <inheritdoc cref="FogFadeDist"/>
    public float? InteriorFogFadeDist { get; init; }

    /// <inheritdoc cref="FogFadeDist"/>
    public Vector3? FogColor { get; init; }

    /// <summary>The scatter table: one entry per <c>clutter</c> block, in file order.</summary>
    public IReadOnlyList<FogClutter> Clutter { get; init; } = Array.Empty<FogClutter>();

    /// <summary>False when the file carries its clutter block as a bare list with no
    /// <c>clutter</c> key, the shape C1B/C2/C3 ship. Those same three chapters have no
    /// <c>fvol*</c> nodes and no <c>cloudsprite*</c> template in their gamez, so the block is
    /// inert whichever way it is read; the flag exists so the log can say which shape was read
    /// rather than leaving "no clouds here" unexplained.</summary>
    public bool HasClutterKey { get; init; }

    /// <summary>Loads a chapter's <c>fogvol.zrd</c>. Null when the chapter's zrdr scope has no
    /// such file, degrades rather than throwing, like every other optional reader here.</summary>
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

    /// <summary>The world's fog volumes, every <c>fvol*</c> node, with its world-space bounds and
    /// face planes. Empty in the three chapters that ship none (C1B, C2, C3). Static over a
    /// <see cref="GameZ"/>, computed from mesh vertices and node transforms, so it is testable
    /// off-engine, same shape as <see cref="WorldBuilder.HorizonZonesOf"/>.</summary>
    public static IReadOnlyList<FogVolumeBox> VolumesOf(GameZ gamez)
    {
        var volumes = new List<FogVolumeBox>();
        foreach (var node in gamez.Nodes)
        {
            if (!WorldBuilder.IsFogVolumeNode(node))
            {
                continue;
            }
            var shape = new Shape();
            Collect(gamez, node, node.Local ?? Transform3D.Identity, shape);
            if (shape.Bounds is { } box)
            {
                volumes.Add(new FogVolumeBox(
                    node.Name, box, shape.OutwardFaces(), shape.ScatterFaces()));
            }
        }
        return volumes;
    }

    /// <summary>The chapter's map-spanning cloud slab, if it has one, data-driven, never a
    /// chapter name or an <c>fvol1..9</c> convention. Qualifies: axis-aligned
    /// (<see cref="FogVolumeBox.IsAxisAlignedBox"/>), top-anchored the same way
    /// <c>FogVolumeClutter.Scatter</c> classifies volumes, and the set exactly tiles its combined
    /// bounds. <c>(null, reason)</c> when a candidate set fails the tiling check.
    /// Decode + measurements: docs/formats/fogvol.md.</summary>
    public static (MapSpanningSlab? Slab, string? SkipReason) FindMapSpanningSlab(
        IReadOnlyList<FogVolumeBox> volumes, float cardHeight, float topAnchorHeightFactor)
    {
        List<FogVolumeBox>? pieces = null;
        foreach (var volume in volumes)
        {
            var box = volume.Box;
            bool topAnchored = box.End.Y - box.Position.Y <= cardHeight * topAnchorHeightFactor;
            if (topAnchored && volume.IsAxisAlignedBox())
            {
                (pieces ??= new List<FogVolumeBox>()).Add(volume);
            }
        }
        if (pieces == null)
        {
            return (null, null);
        }

        float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
        float topY = pieces[0].Box.End.Y;
        double footprint = 0;
        foreach (var piece in pieces)
        {
            var b = piece.Box;
            x0 = Mathf.Min(x0, b.Position.X);
            x1 = Mathf.Max(x1, b.End.X);
            z0 = Mathf.Min(z0, b.Position.Z);
            z1 = Mathf.Max(z1, b.End.Z);
            footprint += (double)(b.End.X - b.Position.X) * (b.End.Z - b.Position.Z);
            // One flat authored sheet cut into pieces for the gamez: every piece's own top is the
            // SAME altitude. A mismatch means the "one slab" premise is wrong for this chapter,
            // surfaced rather than guessed at with an average.
            if (Mathf.Abs(b.End.Y - topY) > 0.01f)
            {
                return (null, $"slab candidate '{piece.Name}' top {b.End.Y} disagrees with "
                              + $"'{pieces[0].Name}' top {topY}");
            }
        }

        double rectArea = (double)(x1 - x0) * (z1 - z0);
        // Test (3) above, checked rather than assumed: 0.1% covers float rounding on 60+ authored
        // vertices at map scale (coordinates to 16 km), a real gap or overlap between pieces is
        // orders of magnitude bigger than that.
        if (rectArea <= 0 || Mathf.Abs((float)(footprint / rectArea) - 1f) > 0.001f)
        {
            return (null, $"{pieces.Count} top-anchored box volume(s) do not exactly tile their "
                          + $"own bounds ({footprint:0}/{rectArea:0}) — not a map-spanning slab");
        }

        return (new MapSpanningSlab(x0, x1, z0, z1, topY), null);
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

    // A subtree's mesh geometry gathered in the frame `xf` expresses: the vertex bounds, the
    // vertex centroid, and one plane per polygon. The shipped fvol nodes are childless boxes at
    // identity, but the walk costs nothing and a placed volume would otherwise be measured in the
    // wrong frame.
    private static void Collect(GameZ gamez, GameZNode node, Transform3D xf, Shape shape)
    {
        if (node.MeshIndex >= 0 && node.MeshIndex < gamez.Meshes.Count)
        {
            var mesh = gamez.Meshes[node.MeshIndex];
            foreach (var v in mesh.Vertices)
            {
                shape.AddVertex(xf * v);
            }
            foreach (var poly in mesh.Polygons)
            {
                shape.AddPolygon(mesh, poly, xf);
                shape.AddScatterFaces(mesh, poly, xf);
            }
        }
        foreach (var childIndex in node.Children)
        {
            if (childIndex < 0 || childIndex >= gamez.Nodes.Count)
            {
                continue;
            }
            var child = gamez.Nodes[childIndex];
            Collect(gamez, child, xf * (child.Local ?? Transform3D.Identity), shape);
        }
    }

    // The world-space geometry of one fvol subtree, accumulated as it is walked. Faces are
    // gathered unoriented because a mesh's winding convention is not guaranteed; the vertex
    // centroid, which lies inside any convex body, decides which way is out, once, at the end.
    private sealed class Shape
    {
        // Two faces closer than this in normal AND offset are the same plane. C1C's fvol9 carries
        // its twelve build-up footprints as coplanar polygons cut into its own top face, so its 55
        // polygons collapse to 5 distinct planes; without the merge the test would repeat one
        // constraint fifty times.
        private const float SameNormal = 1e-4f;
        private const float SameOffset = 1e-2f;

        private readonly List<Plane> _faces = new();
        private readonly List<Vector3[]> _outlines = new();
        private Vector3 _sum;
        private int _count;

        public Aabb? Bounds { get; private set; }

        public void AddVertex(Vector3 p)
        {
            Bounds = Bounds is { } a ? a.Expand(p) : new Aabb(p, Vector3.Zero);
            _sum += p;
            _count++;
        }

        public void AddPolygon(GameZMesh mesh, GameZPolygon poly, Transform3D xf)
        {
            // Newell's method: the area-weighted normal of a polygon, which unlike a cross product
            // of two edges is immune to a collinear corner and averages a slightly non-planar face.
            var normal = Vector3.Zero;
            var first = Vector3.Zero;
            int n = poly.VertexIndices.Count;
            for (int i = 0; i < n; i++)
            {
                int ai = poly.VertexIndices[i], bi = poly.VertexIndices[(i + 1) % n];
                if (ai < 0 || ai >= mesh.Vertices.Count || bi < 0 || bi >= mesh.Vertices.Count)
                {
                    return;
                }
                var a = xf * mesh.Vertices[ai];
                var b = xf * mesh.Vertices[bi];
                normal += new Vector3(
                    (a.Y - b.Y) * (a.Z + b.Z),
                    (a.Z - b.Z) * (a.X + b.X),
                    (a.X - b.X) * (a.Y + b.Y));
                if (i == 0)
                {
                    first = a;
                }
            }
            if (normal.LengthSquared() < 1e-12f)
            {
                return; // degenerate: no area, so no half-space
            }
            normal = normal.Normalized();
            _faces.Add(new Plane(normal, normal.Dot(first)));
        }

        // The faces the cloud lattice is laid over, the original's own polygon walk: a polygon
        // carrying the no_clutter bit (raw 0x800) is skipped, a triangle strip is split into its
        // triangles, and everything else is taken whole. Kept apart from AddPolygon's planes, which
        // merge coplanar faces and must keep the skipped ones, since they still bound the volume.
        public void AddScatterFaces(GameZMesh mesh, GameZPolygon poly, Transform3D xf)
        {
            if (poly.NoClutter)
            {
                return;
            }
            int n = poly.VertexIndices.Count;
            if (poly.TriangleStrip)
            {
                for (int i = 0; i + 2 < n; i++)
                {
                    AddOutline(mesh, poly, xf, i, 3);
                }
                return;
            }
            if (n >= 3)
            {
                AddOutline(mesh, poly, xf, 0, n);
            }
        }

        /// <summary>The scatter faces, every normal pointing away from the body, degenerate ones
        /// dropped. The winding is not trusted: a strip's triangles alternate it, so the vertex
        /// centroid decides which way is out, exactly as <see cref="OutwardFaces"/> does.</summary>
        public IReadOnlyList<FogVolumeFace> ScatterFaces()
        {
            var centre = _count > 0 ? _sum / _count : Vector3.Zero;
            var faces = new List<FogVolumeFace>(_outlines.Count);
            foreach (var outline in _outlines)
            {
                if (Newell(outline) is not { } normal)
                {
                    continue;
                }
                if (normal.Dot(outline[0] - centre) < 0f)
                {
                    normal = -normal;
                }
                faces.Add(new FogVolumeFace(outline, normal));
            }
            return faces;
        }

        /// <summary>The distinct face planes, every normal pointing away from the body.</summary>
        public IReadOnlyList<Plane> OutwardFaces()
        {
            var centre = _count > 0 ? _sum / _count : Vector3.Zero;
            var distinct = new List<Plane>();
            foreach (var face in _faces)
            {
                float inside = face.DistanceTo(centre);
                if (Mathf.Abs(inside) < SameOffset)
                {
                    continue; // the centroid lies on it: a flat volume, nothing to bound
                }
                var oriented = inside > 0f ? new Plane(-face.Normal, -face.D) : face;
                bool known = false;
                foreach (var seen in distinct)
                {
                    if (seen.Normal.DistanceSquaredTo(oriented.Normal) < SameNormal * SameNormal
                        && Mathf.Abs(seen.D - oriented.D) < SameOffset)
                    {
                        known = true;
                        break;
                    }
                }
                if (!known)
                {
                    distinct.Add(oriented);
                }
            }
            return distinct;
        }

        private static Vector3? Newell(IReadOnlyList<Vector3> loop)
        {
            var normal = Vector3.Zero;
            int n = loop.Count;
            for (int i = 0; i < n; i++)
            {
                var a = loop[i];
                var b = loop[(i + 1) % n];
                normal += new Vector3(
                    (a.Y - b.Y) * (a.Z + b.Z),
                    (a.Z - b.Z) * (a.X + b.X),
                    (a.X - b.X) * (a.Y + b.Y));
            }
            return normal.LengthSquared() < 1e-12f ? null : normal.Normalized();
        }

        private void AddOutline(GameZMesh mesh, GameZPolygon poly, Transform3D xf, int start, int count)
        {
            var loop = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                int vi = poly.VertexIndices[start + i];
                if (vi < 0 || vi >= mesh.Vertices.Count)
                {
                    return;
                }
                loop[i] = xf * mesh.Vertices[vi];
            }
            _outlines.Add(loop);
        }
    }
}
