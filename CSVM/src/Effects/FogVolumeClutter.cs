using System;
using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// The original's ambient cloud field: the chapter's own <c>fogvol.zrd</c> clutter table scattered
/// through its gamez <c>fvol*</c> volumes. Schema, per-chapter numbers and the decoded/undecoded
/// split: docs/formats/fogvol.md.
/// Built once, world-anchored, zero per-frame cost, one <see cref="MultiMeshInstance3D"/> per
/// sprite kind, shared by every splitscreen pane; the shader billboards, fades and culls per view.
/// ⚠ Do not re-introduce a hand-tuned cloud field. Every count, radius, size, opacity and band
/// margin is authored data read here; this file holds exactly one TUNE constant, marked at
/// its own field.
/// ⚠ C1B, C2 and C3 render nothing here on purpose, see fogvol.md. Their ambient sky is the
/// world's own placed <c>cloudparent</c> sprites, built elsewhere.
/// </summary>
public sealed partial class FogVolumeClutter : Node3D
{
    // A runaway guard, not a tuning knob: the shipped chapters place up to ~22k sprites (base
    // field plus the map-edge continuation), so this is several times the largest real field. It
    // can only bind if a future extraction reports a `distance` near zero or a volume far larger
    // than a map, both of which should be seen rather than swallowed.
    private const int MaxPlacements = 80_000;

    // Shape-classification threshold (docs/formats/fogvol.md): a volume no more than this many
    // card-heights thick reads as a sheet and is TOP-ANCHORED; taller volumes keep the full-height
    // UNIFORM draw. A judgement call from a clean gap in the volumes' own measured thickness, not
    // authored data, see the docs page for the measurements.
    private const float TopAnchorHeightFactor = 1.5f;

    /// <summary>Sprites placed, summed over every kind, the authored volumes' own placements
    /// plus the map-edge continuation (<see cref="ExtensionCount"/>). Zero means nothing was
    /// built and <see cref="Create"/> returned null.</summary>
    public int InstanceCount { get; private set; }

    /// <summary>Sprites placed by the map-edge continuation alone, zero in every chapter whose
    /// <c>fvol*</c> volumes carry no map-spanning slab (C1B,
    /// C2, C3 render nothing at all; C5's strips and C1C's build-ups are local geometry and are
    /// never extended). <see cref="InstanceCount"/> minus this is the count the authored volumes
    /// alone would have produced.</summary>
    public int ExtensionCount { get; private set; }

    /// <summary>Sprites placed inside the authored volumes alone, <see cref="InstanceCount"/>
    /// minus <see cref="ExtensionCount"/>, i.e. what the in-volume scatter produces before the
    /// continuation runs. Unaffected by the extension, which runs after the base draw and never
    /// reads the shared RNG stream the base draw uses.</summary>
    public int BaseCount => InstanceCount - ExtensionCount;

    /// <summary>Per-kind counts of the build, e.g. "cloudsprite1 x4471 (cloud1.tif, fade
    /// 2000-3000..3100-3500 m)", the evidence that the authored weights and both endpoints of the
    /// per-sprite band reached the field.</summary>
    public string Summary { get; private set; } = "";

    /// <summary>Builds the chapter's ambient cloud field, or null when the data asks for none:
    /// no <c>fogvol.zrd</c>, no <c>fvol*</c> volume, no resolvable template, or a
    /// <c>distance</c> that is not a usable mean spacing. Add the result to the world root at
    /// identity, its instance transforms are absolute world coordinates.</summary>
    public static FogVolumeClutter? Create(GameZ gamez, TextureArchive textures,
        FogVolumeSpec? spec, IReadOnlyList<FogVolumeBox> volumes)
    {
        if (spec == null)
        {
            return null;
        }
        if (volumes.Count == 0 || spec.Clutter.Count == 0)
        {
            // Said out loud: "this chapter has no ambient clouds" is a conclusion drawn from
            // separate absences, and a silent one reads exactly like a reader that broke.
            Log.Info("world", $"fogvol: no ambient cloud field volumes={volumes.Count} clutter={spec.Clutter.Count} clutter_key={spec.HasClutterKey}");
            return null;
        }
        if (spec.Distance < 1f)
        {
            Log.Info("world", $"fogvol: unusable scatter period distance={spec.Distance}");
            return null;
        }

        var kinds = ResolveKinds(gamez, textures, spec);
        if (kinds.Count == 0)
        {
            return null;
        }

        var field = new FogVolumeClutter();
        field.Scatter(spec, volumes, kinds);
        if (field.InstanceCount == 0)
        {
            field.QueueFree();
            return null;
        }
        field.Build(gamez, kinds);
        return field;
    }

    // The gamez side of one clutter alternative: the sprite card its template root carries.
    // Resolved by ClutterBuilder's own template rule, a parentless Object3d of that name whose
    // first meshed descendant is the card (docs/formats/clutter.md).
    private static List<Kind> ResolveKinds(GameZ gamez, TextureArchive textures, FogVolumeSpec spec)
    {
        var kinds = new List<Kind>();
        foreach (var block in spec.Clutter)
        {
            foreach (var reference in block.Nodes)
            {
                var root = ClutterBuilder.FindTemplateRoot(gamez, reference.Node);
                var card = root == null ? null : FirstWithMesh(gamez, root);
                if (card == null)
                {
                    // Retail-data-normal for C1B/C2/C3, see the class remarks.
                    Log.Info("world", $"fogvol clutter template not in gamez template={reference.Node}");
                    continue;
                }
                var mesh = gamez.Meshes[card.MeshIndex];
                var texture = FirstTexture(gamez, mesh);
                kinds.Add(new Kind
                {
                    Name = reference.Node,
                    Block = block,
                    Weight = block.Weight * reference.Weight,
                    MeshIndex = card.MeshIndex,
                    Texture = texture == null ? null : textures.Find(texture),
                    TextureName = texture ?? "?",
                    Lit = mesh.Lighting,
                    Fogged = mesh.Fog,
                    Radius = CardRadius(mesh),
                });
            }
        }
        return kinds;
    }

    private static GameZNode? FirstWithMesh(GameZ gamez, GameZNode node)
    {
        if (node.MeshIndex >= 0 && node.MeshIndex < gamez.Meshes.Count
            && gamez.Meshes[node.MeshIndex].Polygons.Count > 0)
        {
            return node;
        }
        foreach (var childIndex in node.Children)
        {
            if (childIndex >= 0 && childIndex < gamez.Nodes.Count
                && FirstWithMesh(gamez, gamez.Nodes[childIndex]) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    private static string? FirstTexture(GameZ gamez, GameZMesh mesh)
    {
        foreach (var poly in mesh.Polygons)
        {
            if (poly.MaterialIndex >= 0 && poly.MaterialIndex < gamez.Materials.Count
                && gamez.Materials[poly.MaterialIndex].TextureName is { } tex)
            {
                return tex;
            }
        }
        return null;
    }

    // Half the card's largest local extent, what the billboard can swing outside the MultiMesh's
    // static AABB, so it sizes ExtraCullMargin.
    private static float CardRadius(GameZMesh mesh)
    {
        float radius = 0f;
        foreach (var v in mesh.Vertices)
        {
            radius = Mathf.Max(radius, Mathf.Max(Mathf.Abs(v.X), Mathf.Max(Mathf.Abs(v.Y), Mathf.Abs(v.Z))));
        }
        return radius;
    }

    private static float Lerp(Vector2 range, float t) => range.X + ((range.Y - range.X) * t);

    // One sprite's shader custom data: the polygon normal its draw distance is scaled against, and
    // the single draw `t` its fade band is interpolated with. The normal is remapped onto [0, 1]
    // because a MultiMesh custom-data slot is a Color, whose range a future Godot may narrow.
    private static Color BandData(Vector3 normal, float t) => new(
        (normal.X * 0.5f) + 0.5f, (normal.Y * 0.5f) + 0.5f, (normal.Z * 0.5f) + 0.5f, t);

    // The card's own source geometry (verts, UVs and the authored vertex colours), triangulated by
    // the same fan/strip rule as SceneBuilder.EmitPolygon, the cloud cards are tri-strips.
    // Recentred on the quad's centroid so the billboard pivots at its middle, like the placed cloud
    // sprites (SceneBuilder recenters those the same way); the shipped cards are already centred to
    // within 3 mm, so this moves nothing in this install and keeps a future one honest.
    private static ArrayMesh BuildCardMesh(GameZ gamez, int meshIndex)
    {
        var mesh = gamez.Meshes[meshIndex];
        var centre = Vector3.Zero;
        foreach (var v in mesh.Vertices)
        {
            centre += v;
        }
        if (mesh.Vertices.Count > 0)
        {
            centre /= mesh.Vertices.Count;
        }

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var poly in mesh.Polygons)
        {
            int n = poly.VertexIndices.Count;
            int first = poly.TriangleStrip ? 0 : 1;
            int last = poly.TriangleStrip ? n - 3 : n - 2;
            for (int i = first; i <= last; i++)
            {
                Span<int> corners = poly.TriangleStrip
                    ? stackalloc int[] { i, i + 1, i + 2 }
                    : stackalloc int[] { 0, i, i + 1 };
                foreach (int corner in corners)
                {
                    st.SetNormal(Vector3.Back);
                    // The authored colour, unscaled. ⚠ Never scale it: nothing in the original
                    // touches a card's colour, the texture is a constant-RGB alpha mask, and a
                    // brightness gap on this population is coverage (docs/org/cloudCards.md).
                    st.SetColor(poly.VertexColors != null && corner < poly.VertexColors.Count
                        ? poly.VertexColors[corner]
                        : Colors.White);
                    if (poly.UvCoords != null && corner < poly.UvCoords.Count)
                    {
                        st.SetUV(poly.UvCoords[corner]);
                    }
                    st.AddVertex(mesh.Vertices[poly.VertexIndices[corner]] - centre);
                }
            }
        }
        var arrayMesh = new ArrayMesh();
        st.Commit(arrayMesh);
        return arrayMesh;
    }

    // Camera-facing billboard (hand-rolled: a MultiMesh cannot use Godot's billboard flag), plus
    // the clutter fade, whose draw distance is scaled by the viewing angle against each sprite's
    // own polygon normal. `cull` collapses the quad to a point once the fade has dropped it, so a
    // sprite outside its draw distance costs no fragments, what lets the whole field be one static
    // MultiMesh with no streaming. ⚠ The fade distance is the true 3D one, not the fog's
    // horizontal cylinder: a cloud overhead is as far away as one on the horizon.
    private static string ShaderCode(bool lit, bool fogged)
    {
        string light = lit ? " * csky_world_light" : string.Empty;
        string albedo = fogged
            ? "    vec3 fog_world = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;\n"
              + "    float fog_amt = csky_fog_amount(fog_world, CAMERA_POSITION_WORLD);\n"
              + $"    ALBEDO = mix(col.rgb{light}, csky_fog_color, fog_amt);"
            : $"    ALBEDO = col.rgb{light};";
        return $$"""
            shader_type spatial;
            render_mode blend_mix, unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

            uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_disable;
            uniform vec2 far_fade_0 = vec2(1e8, 1e9);  // far_fade_range[0], metres: band start, gone
            uniform vec2 far_fade_1 = vec2(1e8, 1e9);  // far_fade_range[1], the band's other end

            #include "res://shaders/csky_atmosphere.gdshaderinc"
            #include "res://shaders/csky_srgb.gdshaderinc"
            #include "res://shaders/csky_clutter_fade.gdshaderinc"
            // The chapter's mip LOD bias is one device render state in the original, so a card
            // picks its level exactly as every other mip-mapped arm does.
            #include "res://shaders/csky_mip_bias.gdshaderinc"

            varying flat float v_alpha;

            void vertex() {
                vec3 origin = MODEL_MATRIX[3].xyz;
                // INSTANCE_CUSTOM: xyz the sprite's own polygon normal off the [0,1] encoding,
                // w the one draw its whole fade band is interpolated with.
                vec2 band = mix(far_fade_0, far_fade_1, INSTANCE_CUSTOM.w);
                vec2 band_sq = band * band;
                vec4 fade = vec4(band_sq, 1.0 / max(band_sq.y - band_sq.x, 1.0), 0.0);
                v_alpha = csky_clutter_fade_alpha_angled(
                    origin, CAMERA_POSITION_WORLD, fade, INSTANCE_CUSTOM.xyz * 2.0 - 1.0);
                float cull = step(0.004, v_alpha);
                MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
                    INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
                MODELVIEW_MATRIX[0] *= length(MODEL_MATRIX[0].xyz) * cull;
                MODELVIEW_MATRIX[1] *= length(MODEL_MATRIX[1].xyz) * cull;
                MODELVIEW_MATRIX[2] *= length(MODEL_MATRIX[2].xyz);
            }

            void fragment() {
                vec4 col = vec4(csky_srgb_to_linear(COLOR.rgb), COLOR.a) * csky_sample_albedo(albedo_tex, UV);
            {{albedo}}
                ALPHA = col.a * v_alpha;
            }
            """;
    }

    // The scatter: each volume is cut into `distance` x `distance` cells anchored on the world
    // origin, and each cell gets ONE placement drawn uniformly inside it, weighted over the
    // resolved clutter table. `distance` is the field's areal DENSITY, an authored mean spacing,
    // not a lattice phase. See docs/formats/fogvol.md for the density arithmetic and evidence.
    private void Scatter(FogVolumeSpec spec, IReadOnlyList<FogVolumeBox> volumes, List<Kind> kinds)
    {
        Name = "fog_volume_clutter";
        float period = spec.Distance;
        float totalWeight = 0f;
        foreach (var kind in kinds)
        {
            totalWeight += Mathf.Max(kind.Weight, 0f);
        }
        if (totalWeight <= 0f)
        {
            return;
        }

        // The authored card's own extent (docs/formats/fogvol.md's "card size", e.g. 132.3 m for
        // C1/C1C/C2B/C4, 70 m for C5), every kind in a chapter shares one, so the largest among
        // them is that chapter's card height for the TopAnchorHeightFactor test below.
        float cardHeight = 0f;
        foreach (var kind in kinds)
        {
            cardHeight = Mathf.Max(cardHeight, kind.Radius * 2f);
        }

        // One draw sequence off the master seed's cloud stream, fixed volume/cell order, so
        // the whole field is a function of the seed. No camera, pane or frame is read
        // here, so world-anchoring and determinism are the same property.
        var rng = Rng.NewSystemRandom(Rng.Clouds);
        float Rand(float a, float b) => a + ((float)rng.NextDouble() * (b - a));

        // ⚠ Keep the band draws off Rng.Clouds entirely: one there, even taken before the loop,
        // reseeds the placements of any later field built in the same process, which is how a
        // suite that builds two chapters sees the second one's counts move.
        var bandRng = Rng.NewSystemRandom(Rng.CloudBands);

        // One pass per volume, not per clutter block, weights are already flattened into
        // Kind.Weight. Overlapping volumes (C1C's build-ups over its own slab) each get their own
        // fill; cells anchor on the world origin, not the volume.
        foreach (var volume in volumes)
        {
            var box = volume.Box;
            // Sheet-thin volumes (the slab's own AABB height) anchor their draw at the
            // volume's own top; tall ones keep filling uniformly, see
            // TopAnchorHeightFactor and docs/formats/fogvol.md for the evidence.
            bool topAnchored = box.End.Y - box.Position.Y <= cardHeight * TopAnchorHeightFactor;
            int gx0 = Mathf.CeilToInt(box.Position.X / period), gx1 = Mathf.FloorToInt(box.End.X / period);
            int gz0 = Mathf.CeilToInt(box.Position.Z / period), gz1 = Mathf.FloorToInt(box.End.Z / period);
            for (int gx = gx0; gx <= gx1 && InstanceCount < MaxPlacements; gx++)
            {
                // Cells tile the volume exactly (outermost cell of each axis takes the
                // remainder), never a Poisson draw over the footprint, that would open holes
                // in what must read as a continuous overcast.
                float x0 = gx == gx0 ? box.Position.X : (gx * period) - (period * 0.5f);
                float x1 = gx == gx1 ? box.End.X : (gx * period) + (period * 0.5f);
                for (int gz = gz0; gz <= gz1 && InstanceCount < MaxPlacements; gz++)
                {
                    float z0 = gz == gz0 ? box.Position.Z : (gz * period) - (period * 0.5f);
                    float z1 = gz == gz1 ? box.End.Z : (gz * period) + (period * 0.5f);

                    float x = Rand(x0, x1);
                    float z = Rand(z0, z1);
                    // Top-anchored: Y is the volume's own top; perp_dist_range still adds after
                    // containment, as for a uniform draw. ⚠ Never sample it before containment,
                    // that would reject the whole field.
                    float y = topAnchored ? box.End.Y : Rand(box.Position.Y, box.End.Y);

                    // The volume is its AUTHORED shape, not its bounding box, exact for C1/C2B/C4's
                    // slabs, approximate-by-rejection for C1C's frusta and C5's prisms (fogvol.md).
                    // A rejected draw places nothing rather than crowding the surplus inward.
                    if (!volume.Contains(new Vector3(x, y, z)))
                    {
                        continue;
                    }

                    // Weighted draw over the whole table (block weight x node weight).
                    float pick = Rand(0f, totalWeight);
                    var kind = kinds[kinds.Count - 1];
                    foreach (var candidate in kinds)
                    {
                        pick -= Mathf.Max(candidate.Weight, 0f);
                        if (pick <= 0f)
                        {
                            kind = candidate;
                            break;
                        }
                    }

                    // Perturbation is applied AFTER containment, so a placement can sit up to
                    // perturb_dist_range.y outside its own volume's wall, that is what a
                    // perturbation means; the volume bounds the field, not each sprite.
                    var block = kind.Block;
                    float bearing = Rand(0f, Mathf.Tau);
                    float perturb = Lerp(block.PerturbDistRange, (float)rng.NextDouble());
                    float scale = Lerp(block.ScaleRange, (float)rng.NextDouble());
                    kind.Placements.Add(new Transform3D(
                        Basis.Identity.Scaled(new Vector3(scale, scale, scale)),
                        new Vector3(
                            x + (Mathf.Sin(bearing) * perturb),
                            y + Lerp(block.PerpDistRange, (float)rng.NextDouble()),
                            z + (Mathf.Cos(bearing) * perturb))));
                    // A top-anchored draw sits ON the volume's top face, so its normal is +Y by
                    // construction; a uniform draw has no face of its own and takes the one it
                    // fell nearest, which is the face the authored scatter would have used.
                    kind.Bands.Add(BandData(
                        topAnchored ? Vector3.Up : volume.NearestFaceNormal(new Vector3(x, y, z)),
                        (float)bandRng.NextDouble()));
                    InstanceCount++;
                }
            }
        }

        // Continue the map-spanning slab's own cell field past the base map. Runs after every
        // authored volume above has drawn everything it draws, and touches no state the loop above
        // reads, it never calls `rng` at all, so it cannot realign the interior placements.
        ExtendPastMapEdge(volumes, kinds, period, cardHeight, totalWeight);
    }

    // Identifies the chapter's map-spanning slab from data (FogVolumeSpec.FindMapSpanningSlab,
    // pure geometry, never a chapter name or a hardcoded fvol1..9 range) and, if one exists, tiles
    // its own `distance`-cell field outward past the map rim. C1C's build-up frusta and C5's
    // street strips fail the top-anchored test and are never extended; C1B/C2/C3 ship no fvol* at
    // all. See docs/formats/fogvol.md's Map-edge continuation section for the verification.
    private void ExtendPastMapEdge(IReadOnlyList<FogVolumeBox> volumes, List<Kind> kinds,
        float period, float cardHeight, float totalWeight)
    {
        var (found, skipReason) = FogVolumeSpec.FindMapSpanningSlab(volumes, cardHeight, TopAnchorHeightFactor);
        if (skipReason != null)
        {
            string msg = $"fogvol: map-edge continuation skipped, {skipReason}";
            Log.Info("world", $"{msg}");
        }
        if (found is not { } slab)
        {
            return;
        }

        // Bounded at the largest authored far_fade, not at MapEdgeExtender's own reach: the shader
        // collapses a sprite once its band has dropped it, and the view-angle law reaches at most
        // half that band horizontally, so a wider ring buys nothing (docs/formats/fogvol.md).
        float radius = 0f;
        foreach (var kind in kinds)
        {
            radius = Mathf.Max(radius, kind.Block.FarFade.Y);
        }
        if (radius <= 0f)
        {
            return;
        }

        float bx0 = slab.MinX, bx1 = slab.MaxX, bz0 = slab.MinZ, bz1 = slab.MaxZ, topY = slab.TopY;
        // Eight regions tile the radius-margin ring with no gap and no overlap: four edge strips,
        // four corner squares. Every inner edge is exactly bx0/bx1/bz0/bz1, the same coordinate
        // the interior loop clips its outermost cell to, so the join is exact by construction.
        EmitExtensionRegion(bx0 - radius, bx0, bz0, bz1, period, topY, kinds, totalWeight); // west
        EmitExtensionRegion(bx1, bx1 + radius, bz0, bz1, period, topY, kinds, totalWeight); // east
        EmitExtensionRegion(bx0, bx1, bz0 - radius, bz0, period, topY, kinds, totalWeight); // south
        EmitExtensionRegion(bx0, bx1, bz1, bz1 + radius, period, topY, kinds, totalWeight); // north
        EmitExtensionRegion(bx0 - radius, bx0, bz0 - radius, bz0, period, topY, kinds, totalWeight); // sw
        EmitExtensionRegion(bx1, bx1 + radius, bz0 - radius, bz0, period, topY, kinds, totalWeight); // se
        EmitExtensionRegion(bx0 - radius, bx0, bz1, bz1 + radius, period, topY, kinds, totalWeight); // nw
        EmitExtensionRegion(bx1, bx1 + radius, bz1, bz1 + radius, period, topY, kinds, totalWeight); // ne
    }

    // One rectangular slice of the extension ring, tiled with the same distance x distance cells
    // as the interior loop. Two deliberate differences: every cell is accepted unconditionally,
    // there is no authored shape out here to test against, and each cell draws off its own
    // hashed generator (`Rng.NewSystemRandom(Rng.Clouds, gx, gz)`), not the interior's shared
    // stream, so `--det` stays stable regardless of how many cells the enumeration bounds admit.
    // Y is the slab's own constant top plus `perp_dist_range`, inheriting the interior's rule.
    private void EmitExtensionRegion(float x0, float x1, float z0, float z1, float period,
        float topY, List<Kind> kinds, float totalWeight)
    {
        if (x1 <= x0 || z1 <= z0)
        {
            return;
        }
        int gx0 = Mathf.CeilToInt(x0 / period), gx1 = Mathf.FloorToInt(x1 / period);
        int gz0 = Mathf.CeilToInt(z0 / period), gz1 = Mathf.FloorToInt(z1 / period);
        // A region thinner than one period (never true of any shipped chapter's far_fade against
        // its 80-130 m `distance`) still gets exactly one remainder cell rather than none.
        if (gx1 < gx0)
        {
            gx1 = gx0 = Mathf.RoundToInt((x0 + x1) * 0.5f / period);
        }
        if (gz1 < gz0)
        {
            gz1 = gz0 = Mathf.RoundToInt((z0 + z1) * 0.5f / period);
        }

        for (int gx = gx0; gx <= gx1 && InstanceCount < MaxPlacements; gx++)
        {
            float cx0 = gx == gx0 ? x0 : (gx * period) - (period * 0.5f);
            float cx1 = gx == gx1 ? x1 : (gx * period) + (period * 0.5f);
            for (int gz = gz0; gz <= gz1 && InstanceCount < MaxPlacements; gz++)
            {
                float cz0 = gz == gz0 ? z0 : (gz * period) - (period * 0.5f);
                float cz1 = gz == gz1 ? z1 : (gz * period) + (period * 0.5f);

                var rng = Rng.NewSystemRandom(Rng.Clouds, gx, gz);
                float Rand(float a, float b) => a + ((float)rng.NextDouble() * (b - a));

                float x = Rand(cx0, cx1);
                float z = Rand(cz0, cz1);

                float pick = Rand(0f, totalWeight);
                var kind = kinds[kinds.Count - 1];
                foreach (var candidate in kinds)
                {
                    pick -= Mathf.Max(candidate.Weight, 0f);
                    if (pick <= 0f)
                    {
                        kind = candidate;
                        break;
                    }
                }

                var block = kind.Block;
                float bearing = Rand(0f, Mathf.Tau);
                float perturb = Lerp(block.PerturbDistRange, (float)rng.NextDouble());
                float scale = Lerp(block.ScaleRange, (float)rng.NextDouble());
                kind.Placements.Add(new Transform3D(
                    Basis.Identity.Scaled(new Vector3(scale, scale, scale)),
                    new Vector3(
                        x + (Mathf.Sin(bearing) * perturb),
                        topY + Lerp(block.PerpDistRange, (float)rng.NextDouble()),
                        z + (Mathf.Cos(bearing) * perturb))));
                // The slab's own top face continues out here, so +Y like the interior's
                // top-anchored draw. ⚠ The band draw comes last, after every draw a placement
                // reads, so adding it left the ring's positions where they were.
                kind.Bands.Add(BandData(Vector3.Up, (float)rng.NextDouble()));
                InstanceCount++;
                ExtensionCount++;
            }
        }
    }

    private void Build(GameZ gamez, List<Kind> kinds)
    {
        var parts = new List<string>();
        var meshCache = new Dictionary<int, ArrayMesh>();
        foreach (var kind in kinds)
        {
            if (kind.Placements.Count == 0)
            {
                continue;
            }
            if (!meshCache.TryGetValue(kind.MeshIndex, out var mesh))
            {
                meshCache[kind.MeshIndex] = mesh = BuildCardMesh(gamez, kind.MeshIndex);
            }
            var mat = new ShaderMaterial { Shader = new Shader { Code = ShaderCode(kind.Lit, kind.Fogged) } };
            if (kind.Texture != null)
            {
                mat.SetShaderParameter("albedo_tex", kind.Texture);
            }
            mat.SetShaderParameter("far_fade_0", kind.Block.FarFadeNear);
            mat.SetShaderParameter("far_fade_1", kind.Block.FarFade);

            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                // ⚠ Before InstanceCount, or the band has no slot in the buffer.
                UseCustomData = true,
                Mesh = mesh,
                InstanceCount = kind.Placements.Count,
            };
            for (int i = 0; i < kind.Placements.Count; i++)
            {
                mm.SetInstanceTransform(i, kind.Placements[i]);
                mm.SetInstanceCustomData(i, kind.Bands[i]);
            }
            AddChild(new MultiMeshInstance3D
            {
                Multimesh = mm,
                MaterialOverride = mat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                // The billboard swings vertices outside the instances' static AABB.
                ExtraCullMargin = kind.Radius * Mathf.Max(kind.Block.ScaleRange.Y, 1f),
                Name = kind.Name,
            });
            parts.Add($"{kind.Name} x{kind.Placements.Count} ({kind.TextureName}, "
                      + $"fade {kind.Block.FarFadeNear.X:0}-{kind.Block.FarFadeNear.Y:0}"
                      + $"..{kind.Block.FarFade.X:0}-{kind.Block.FarFade.Y:0} m)");
        }
        Summary = string.Join(", ", parts);
    }

    // One alternative of the resolved scatter table: a clutter block paired with one of the
    // template nodes it names, and everything the gamez says about that node's sprite card.
    private sealed class Kind
    {
        public readonly List<Transform3D> Placements = new();

        // Parallel to Placements, one entry per instance, see BandData.
        public readonly List<Color> Bands = new();

        public string Name = "";
        public FogClutter Block = null!;
        public float Weight;
        public int MeshIndex;
        public ImageTexture? Texture;
        public string TextureName = "";
        public bool Lit = true;
        public bool Fogged = true;
        public float Radius;
    }
}
