using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>One <c>fvol*</c> node of the world: the authored volume a fog volume occupies, in
/// world coordinates. These carry no visible geometry — <see cref="WorldBuilder.SkipWorldNode"/>
/// has excluded them from the render since the world build was written — they are the shape the
/// original fills with the <c>fogvol.zrd</c> clutter (see docs/formats/fogvol.md).
///
/// <para><see cref="Box"/> is the axis-aligned bounds, which is what the scatter walks cells over.
/// <see cref="Contains"/> is the authored shape itself, and the two are the same thing only in
/// C1/C2B/C4, whose map-spanning slabs are axis-aligned boxes. C1C's twelve build-ups are rotated,
/// TAPERING frusta whose horizontal cross-section shrinks with height, and C5's street strips are
/// polygonal prisms — filling the bounds instead of the shape puts cloud where the data authors
/// none.</para>
///
/// <para>Named <c>…Box</c> because <c>Godot.FogVolume</c> is a real engine type (volumetric fog),
/// which this is not: it is authored data. What the remake does render from it is a camera-space
/// screen overlay, not volumetric fog — <see cref="FogVolumeWhiteout"/> (C21) plus the <c>ZONE3</c>
/// fog globals the camera state flips to inside a volume (C22).</para>
/// </summary>
/// <param name="Name">The gamez node's own name, e.g. <c>fvol10</c>.</param>
/// <param name="Box">World-space axis-aligned bounds of the node's mesh.</param>
/// <param name="Faces">The mesh's distinct face planes, outward-facing — see
/// <see cref="Contains"/>.</param>
public readonly record struct FogVolumeBox(string Name, Aabb Box, IReadOnlyList<Plane> Faces)
{
    // Slack on the face test, in metres. A placement drawn AT a face — which any rule that anchors
    // the spread to a wall does by construction — must read as inside, and float error at map
    // scale (coordinates to 16 km) is four orders of magnitude below this.
    private const float Slack = 0.05f;

    // ExteriorDistance's projection loop. The tolerance is a whole cycle's movement, in metres:
    // 1e-4 is four orders below the tightest authored ramp this feeds (C5's 16 m fog_fade_dist),
    // so the ramp cannot see it. 64 cycles is a guard against a pathological shape, not the usual
    // exit — an axis-aligned box converges in one and every shipped volume in a handful.
    private const float ProjectionTolerance = 1e-4f;
    private const int MaxProjectionCycles = 64;

    // How many face planes ExteriorDistance keeps its corrections on the stack for. The widest
    // shipped volume is far under this (C1C's fvol9 is the busiest at 5 distinct planes over 55
    // polygons), so no shipped chapter ever takes the heap fallback.
    private const int MaxStackFaces = 32;

    /// <summary>Is this world point inside the authored volume?
    ///
    /// <para>A half-space test over the mesh's own faces, which is EXACT here rather than an
    /// approximation: every <c>fvol*</c> volume of every shipped chapter is convex (verified
    /// across all 65 of them — slabs, frusta and street prisms alike). Because the faces of a
    /// frustum slope, the test narrows with height on its own, which is what makes a vertical
    /// placement rule and this containment rule independent of each other.</para></summary>
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

    /// <summary>The binary's own outside-positive / inside-negative distance to this volume
    /// (<c>FUN_0044e6f0</c>): the largest signed distance to any face plane.
    ///
    /// <para><b>Inside it is EXACT and it is the distance to the boundary</b> — for a convex
    /// polytope the nearest wall is the least-negative face plane, so <c>-SignedDistance</c> is the
    /// penetration depth <see cref="FogVolumeWhiteout"/>'s interior ramp decays over. <b>Outside it
    /// is only a LOWER BOUND</b> on the Euclidean distance (the body lies inside every one of its
    /// half-spaces, so the distance to the body is at least the distance to any of them), which is
    /// exact only when the closest point lies in that face's own Voronoi region and understates it
    /// near an edge or a corner — that is what <see cref="ExteriorDistance"/> is for, and why the
    /// two are separate methods rather than one signed number.</para>
    ///
    /// <para>A volume with no face planes at all (a degenerate flat shape — <c>OutwardFaces</c>
    /// returns nothing when the vertex centroid lies on every polygon) answers
    /// <see cref="float.MaxValue"/>: it has no wall to ramp from, so it whiteouts nothing. No
    /// shipped volume is in that state; <see cref="Contains"/> would call such a shape "inside
    /// everywhere", so the two deliberately differ rather than propagating it.</para></summary>
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

    /// <summary>The Euclidean distance from an OUTSIDE point to this volume's authored shape —
    /// 0 for a point inside it. The quantity <c>FUN_0044e6f0</c>'s approach ramp runs over.
    ///
    /// <para><b>Method: Dykstra's alternating projection onto the face half-spaces.</b> The volume
    /// is the intersection of its outward face half-spaces (exact, because every one of the 65
    /// shipped <c>fvol*</c> volumes is convex — the same property <see cref="Contains"/> rests on),
    /// and Dykstra's algorithm — cyclic projection with a per-set correction term — converges to
    /// the projection onto an intersection of closed convex sets, unlike plain cyclic projection
    /// (POCS), which only reaches *some* point of it. So this is the true closest point on the
    /// convex hull, not a face-plane approximation: the half-space distances alone
    /// (<see cref="SignedDistance"/>) understate it wherever the nearest point is on an edge or a
    /// vertex.</para>
    ///
    /// <para><b>Exactness is iterative, and bounded rather than assumed.</b> For a set of mutually
    /// orthogonal half-spaces (an axis-aligned box — C1/C2B/C4's slabs and two of C5's strips) one
    /// cycle is already exact, and the loop stops on the next. Otherwise it runs until a whole
    /// cycle moves the point less than <see cref="ProjectionTolerance"/> (1e-4 m) or
    /// <see cref="MaxProjectionCycles"/> cycles have run; convergence for a polyhedron is linear,
    /// so the cap is a guard, not the usual exit. `CSVM.Tests/FogVolumeWhiteoutTests.cs` pins the
    /// result against closed-form distances for a box (face, edge and corner regions) and for a
    /// 45°-rotated prism, which is where a face-plane-only answer would be wrong by up to 41 %.</para>
    ///
    /// <para><b>Allocation-free</b> for every shipped volume: the per-half-space corrections live in
    /// a <c>stackalloc</c> buffer of <see cref="MaxStackFaces"/> entries (the widest shipped volume
    /// has far fewer), and only a hypothetical wider one falls back to the heap.</para></summary>
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

    /// <summary>True when this volume's authored shape IS its own axis-aligned bounds — every
    /// corner of <see cref="Box"/> lies inside <see cref="Contains"/>. Exact for C1/C2B/C4's
    /// map-spanning slabs and C1C's own <c>fvol1</c>-<c>fvol9</c> (hull/AABB 1.000); false for
    /// C1C's twelve rotated build-up frusta and most of C5's polygonal street prisms — see the
    /// per-chapter shape census in docs/formats/fogvol.md. Used both by
    /// <c>CSVM.Tests/FogVolumeTests.cs</c>'s census and by <see cref="FogVolumeSpec.FindMapSpanningSlab"/>
    /// (A5's map-edge continuation), which is why it lives on the type rather than being
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

/// <summary>One chapter's map-spanning cloud slab — the combined footprint of every top-anchored,
/// axis-aligned <c>fvol*</c> volume that tiles it exactly (see
/// <see cref="FogVolumeSpec.FindMapSpanningSlab"/>). <see cref="MinX"/>/<see cref="MaxX"/>/
/// <see cref="MinZ"/>/<see cref="MaxZ"/> are the combined bounds (C1/C1C/C2B/C4: the base map's
/// own <c>World.area</c>, to the metre — A1's "exact 3x3 partition" finding); <see cref="TopY"/>
/// is the one authored altitude every piece's own top agrees on.</summary>
public readonly record struct MapSpanningSlab(float MinX, float MaxX, float MinZ, float MaxZ, float TopY);

/// <summary>One weighted template reference of a clutter block's <c>nodes</c> list: the gamez
/// template node to scatter, and its relative weight among the block's alternatives.</summary>
public readonly record struct FogClutterNode(float Weight, string Node);

/// <summary>The chapter's in-volume whiteout: the camera-space density <c>FUN_0044e6f0</c>
/// computes every frame from the <c>fvol*</c> volumes when <c>fogvol.zrd</c>'s <c>fog_zone</c> is
/// set, and the colour it paints (<c>PLAN-weather-decompile-match</c> C21, docs/formats/fogvol.md).
/// <b>C5 is the only chapter in the install that arms it.</b>
///
/// <para><b>Two linear ramps over one signed distance</b> (<see cref="FogVolumeBox.SignedDistance"/>,
/// outside-positive/inside-negative):</para>
/// <list type="bullet">
/// <item><b>Approach</b> — outside, density rises linearly from 0 at <see cref="FadeDist"/> metres
/// out to 1 AT the wall, over the true Euclidean distance to the authored hull
/// (<see cref="FogVolumeBox.ExteriorDistance"/>).</item>
/// <item><b>Interior</b> — inside, density DECAYS from 1 at the wall to 0 at
/// <see cref="InteriorFadeDist"/> metres of penetration. ⚠ That reads backwards on its own and must
/// not be "fixed" by inverting it: the volume is a transition CURTAIN, and what carries the look
/// once the camera is properly inside is <c>ZONE3</c>'s own fog (state 3, <c>C22</c>).</item>
/// </list>
///
/// <para>Volumes union as <c>a + b − a·b</c>, the binary's own combiner, so a camera near a corner
/// where two strips meet is whited out by both rather than by the nearer one.</para>
///
/// <para>Pure and off-engine (no nodes, no shader state), so the whole rule is unit-testable —
/// <c>CSVM.Tests/FogVolumeWhiteoutTests.cs</c>. <c>Session/WeatherRig.Tick</c> is the one consumer:
/// it blends <see cref="Density"/> onto the same screen overlay the <c>CLOUD_COVER</c> band
/// whiteout uses (Decision 6 of the plan — one camera-space density blended into the frame, never
/// per-volume fog meshes).</para></summary>
public sealed class FogVolumeWhiteout
{
    /// <summary>The loader's defaults for a chapter that arms <c>fog_zone</c> but authors no
    /// distances (<c>FUN_0044e010</c>). No shipped chapter is in that state — C5 authors 16/16 —
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
    /// before any geometry is touched — C1's nine volumes must never whiteout.</summary>
    public bool Armed { get; }

    /// <summary><c>fog_fade_dist</c> — the approach ramp's length in metres (C5: 16).</summary>
    public float FadeDist { get; }

    /// <summary><c>interior_fog_fade_dist</c> — the interior decay's depth in metres (C5: 16).</summary>
    public float InteriorFadeDist { get; }

    /// <summary>The authored <c>fog_color</c>, normalized 0..1, or null when the file omits it —
    /// in which case the engine's default is the mission's <c>CLOUD_COVER</c> <c>TOP_COLOR</c>,
    /// which is mission data this chapter-scope object deliberately does not reach for (the caller
    /// resolves it — <c>WeatherRig.Tick</c>).
    ///
    /// <para>Normalized by <see cref="FogVolumeSpec"/>'s own integer-vs-float rule, the same one
    /// <c>WeatherState.ParseColor</c> applies to every weather colour: any component above 1 means
    /// the triple is 0–255. C5's <c>[16,16,16]</c> is therefore 0.063 — this "whiteout" is very
    /// nearly a BLACKOUT, matching C5's <c>ZONE3</c> <c>FOG_COLOR</c> of the same 16 that takes
    /// over inside. It is a DX7 framebuffer (sRGB) value and stays in that space: the overlay it
    /// paints is a <c>ColorRect</c>, not a shader input, so unlike the fog globals
    /// (<c>WeatherRig.ApplyZone</c>) it is never linearised — same as the band whiteout's own
    /// <c>WeatherState.WhiteoutColor</c> beside it.</para></summary>
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

    /// <summary>One volume's own whiteout density at a point, 0..1 — the two decompiled ramps over
    /// <see cref="FogVolumeBox.SignedDistance"/>. Public and static because it is the RULE, and the
    /// rule is what the tests assert; <see cref="Density"/> only unions it.
    ///
    /// <para>The exterior branch takes <see cref="FogVolumeBox.SignedDistance"/> as a cheap lower
    /// bound first and only pays for <see cref="FogVolumeBox.ExteriorDistance"/>'s projection when
    /// that bound is inside the ramp — which, at C5's 16 m, is almost never.</para></summary>
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
            return 0f;   // beyond the ramp — signed under-estimates the true distance, so this is safe
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

    /// <summary><c>perturb_dist_range</c> — how far the placement is displaced from the point drawn
    /// in its cell, within that plane (min, max metres).</summary>
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
    /// C5's 1). A BOOL arming the in-volume whiteout and camera state 3 (<c>FUN_0044e010</c>
    /// stores <c>value != 0</c>), consumed through <see cref="FogZoneArmed"/>. It is NOT the
    /// sky/fog zone selector (<c>BL-100</c>) — see docs/formats/fogvol.md.</summary>
    public int? FogZone { get; init; }

    /// <summary>Whether this chapter's fogvol arms the in-volume whiteout + camera state 3
    /// (<c>FUN_0044e010</c>: stores <c>value != 0</c>). C5 (<c>FogZone</c> 1) is the only chapter
    /// this is true for — C1/C1C/C2B/C4 ship <c>FogZone</c> 0 (present, disarmed) and C1B/C2/C3
    /// carry no <c>fog_zone</c> key at all (<see cref="FogZone"/> null). Consumed by
    /// <see cref="WeatherState.CameraWeatherState"/>'s state-3 test
    /// (<c>PLAN-weather-decompile-match</c> A2).</summary>
    public bool FogZoneArmed => FogZone is { } zone && zone != 0;

    /// <summary><c>distance</c> — the scatter's mean spacing in metres, i.e. the side of the cell
    /// that carries one placement (130 in C1/C1C/C2B/C4, 80 in C5; the degenerate copies carry
    /// 206.25). An areal density, not a lattice phase — docs/formats/fogvol.md.</summary>
    public float Distance { get; init; }

    /// <summary><c>fog_fade_dist</c> / <c>interior_fog_fade_dist</c> / <c>fog_color</c> — C5 only
    /// (16 / 16 / [16,16,16]), the in-volume whiteout's approach ramp, interior decay depth and
    /// colour. Consumed by <see cref="FogVolumeWhiteout"/> (C21), which also holds the loader's
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

    /// <summary>The world's fog volumes — every <c>fvol*</c> node, with the world-space bounds AND
    /// the face planes of its own geometry. Empty in the three chapters that ship none (C1B, C2,
    /// C3), which is exactly the set whose <c>fogvol.zrd</c> is the degenerate copy.
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
            var shape = new Shape();
            Collect(gamez, node, node.Local ?? Transform3D.Identity, shape);
            if (shape.Bounds is { } box)
            {
                volumes.Add(new FogVolumeBox(node.Name, box, shape.OutwardFaces()));
            }
        }
        return volumes;
    }

    /// <summary>The chapter's map-spanning cloud slab, if it has one — the data-driven test A5
    /// (docs/plans/PLAN-overcast-match.md) uses to decide which volumes may continue past the map edge,
    /// kept here (pure geometry, no RNG, no render state) so it is testable off-engine like
    /// <see cref="VolumesOf"/> beside it. A volume qualifies only if ALL THREE hold:
    /// <list type="number">
    /// <item>its authored shape IS its own AABB (<see cref="FogVolumeBox.IsAxisAlignedBox"/>) —
    /// no sloped or rotated wall a straight continuation would misrepresent;</item>
    /// <item>it is TOP-ANCHORED by the SAME rule <c>FogVolumeClutter.Scatter</c> classifies
    /// volumes with (A3) — a build-up or a street strip is local geometry, not a field to tile
    /// outward;</item>
    /// <item>together with every other volume passing (1)+(2), the set's footprints exactly TILE
    /// their own combined bounding rectangle — no gap, no overlap (A1's "exact 3x3 partition of
    /// World.area" finding, re-checked from the volumes' own extents rather than assumed).</item>
    /// </list>
    /// Verified against the shipped data: C1/C2B/C4's nine slab pieces and C1C's own map-spanning
    /// <c>fvol1</c>-<c>fvol9</c> pass; C1C's twelve build-up frusta fail (2) (shortest is 299.7 m,
    /// ratio 2.27 against the 1.5x cut); C5's seventeen street strips fail (2) too (646 m, ratio
    /// 9.23); C1B/C2/C3 ship no <c>fvol*</c> at all, so <paramref name="volumes"/> is empty and
    /// this returns <c>(null, null)</c> — no slab, and nothing to report.
    ///
    /// <para>Returns <c>(null, reason)</c> rather than throwing when a candidate set is found but
    /// fails (2)/(3)'s cross-check, so the caller can log why rather than silently doing
    /// nothing (verification.md DIAG-15) — no shipped chapter is expected to hit this branch.</para></summary>
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
            // SAME altitude. A mismatch means the "one slab" premise is wrong for this chapter —
            // surfaced rather than guessed at with an average.
            if (Mathf.Abs(b.End.Y - topY) > 0.01f)
            {
                return (null, $"slab candidate '{piece.Name}' top {b.End.Y} disagrees with "
                              + $"'{pieces[0].Name}' top {topY}");
            }
        }

        double rectArea = (double)(x1 - x0) * (z1 - z0);
        // Test (3) above, checked rather than assumed: 0.1% covers float rounding on 60+ authored
        // vertices at map scale (coordinates to 16 km) — a real gap or overlap between pieces is
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
    // centroid — which lies inside any convex body — decides which way is out, once, at the end.
    private sealed class Shape
    {
        // Two faces closer than this in normal AND offset are the same plane. C1C's fvol9 carries
        // its twelve build-up footprints as coplanar polygons cut into its own top face, so its 55
        // polygons collapse to 5 distinct planes; without the merge the test would repeat one
        // constraint fifty times.
        private const float SameNormal = 1e-4f;
        private const float SameOffset = 1e-2f;

        private readonly List<Plane> _faces = new();
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
    }
}
