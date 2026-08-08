using System;
using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// The original's ambient cloud field: the chapter's own <c>zrdr/fogvol.zrd</c> clutter table
/// scattered through the <c>fvol*</c> boxes its gamez authors — the wisps the plane flies through
/// at the overcast (see <c>OriginalScreenshots/C1 IA1 Cloud Puffs and Moon.png</c>). Schema, the
/// per-chapter numbers and the decoded/undecoded split: docs/formats/fogvol.md.
///
/// <para><b>Everything here comes off disk.</b> Which sprite templates
/// (<c>cloudsprite1</c>/<c>cloudsprite2</c>, resolved as gamez clutter template roots by
/// <see cref="ClutterBuilder.FindTemplateRoot"/>, the same lookup the trees use), their relative
/// weights, the mean spacing (<c>distance</c>), the per-placement jitter
/// (<c>perturb_dist_range</c> in the plane, <c>perp_dist_range</c> vertically), the size
/// multiplier (<c>scale_range</c>), the draw distance (<c>far_fade_range</c>) and the shape the
/// field occupies (the <c>fvol*</c> volumes' own geometry) are all authored. The sprite's size,
/// texture, billboard mode and its <c>lighting</c>/<c>fog</c> render flags come from the gamez
/// model. This class holds <b>no TUNE constant</b> — do not re-introduce a hand-tuned cloud field
/// (count, radius, size, opacity, band margins, vertical fades); every one of those is authored
/// data read above. The one exception is <see cref="TopAnchorHeightFactor"/> (A3): not authored
/// data, but a shape-classification threshold decided from the authored volumes' own thickness
/// gap between the sheet-thin slabs and the tall build-ups/strips — see its own remarks.</para>
///
/// <para><b>Model — world-anchored, built once, zero per-frame cost.</b> The field is static
/// geometry, not a camera-following pool: the volumes are fixed authored shapes and the cells are
/// anchored on the world origin, so every placement is decided at load and nothing recycles. One
/// <see cref="MultiMeshInstance3D"/> per sprite kind (two draw calls in every shipped chapter); a
/// spatial shader billboards each quad toward the camera, fades it out over the authored
/// <c>far_fade_range</c> and collapses it to a degenerate quad past the far end, so a sprite
/// outside its draw distance costs no fragments. That is also why it is ONE field shared by every
/// splitscreen pane rather than a copy each — nothing about it is anchored to a camera, and the
/// fade is evaluated per view inside the shader.</para>
///
/// <para><b>Three chapters render nothing, and that is the data.</b> C1B, C2 and C3 ship no
/// <c>fvol*</c> node, no <c>cloudsprite*</c> template, and a degenerate <c>fogvol.zrd</c> whose
/// clutter block has lost its <c>clutter</c> key and names a <c>cloudsprite</c> that exists in no
/// chapter's gamez. Their ambient sky is the world's own placed <c>cloudparent</c> sprites (C1B
/// has 70; C2 and C3 have none) — which is the "singles at all heights, but not on every map" the
/// playtest saw, and it is drawn by the ordinary world build, not here.</para>
/// </summary>
public sealed partial class FogVolumeClutter : Node3D
{
    // A runaway guard, not a tuning knob: the shipped chapters place 8.9k-19k sprites, so this is
    // several times the largest real field. It can only bind if a future extraction reports a
    // `distance` near zero or a volume far larger than a map, both of which should be seen rather
    // than swallowed.
    private const int MaxPlacements = 80_000;

    // A3's per-volume-shape rule (docs/formats/fogvol.md, docs/PLAN-overcast-match.md A3): a
    // volume no more than this many card-heights thick reads as a sheet and is TOP-ANCHORED;
    // anything taller keeps the old full-height UNIFORM draw. The evidence is a clean gap, not a
    // tuned edge — measured off extracted/{C1,C1C,C2B,C4,C5}/gamez/nodes.json: C1/C2B/C4's slabs
    // and C1C's own map-spanning fvol1-9 are 120.5-120.6 m thick against a 132.3 m card (ratio
    // 0.91, comfortably under 1x); C1C's twelve build-up frusta start at 299.7 m (ratio 2.27) and
    // C5's seventeen street strips are 646 m (ratio 9.23 against their 70 m card). 1.5x sits in
    // that ~2.5x gap with margin on both sides, so nothing near the boundary is a runtime coin
    // flip.
    private const float TopAnchorHeightFactor = 1.5f;

    /// <summary>Sprites placed, summed over every kind. Zero means nothing was built and
    /// <see cref="Create"/> returned null.</summary>
    public int InstanceCount { get; private set; }

    /// <summary>Per-kind counts of the build, e.g. "cloudsprite1 x4471 (cloud1.tif, fade
    /// 3100-3500 m)" — the evidence that the authored weights and fades reached the field.</summary>
    public string Summary { get; private set; } = "";

    /// <summary>Builds the chapter's ambient cloud field, or null when the data asks for none:
    /// no <c>fogvol.zrd</c>, no <c>fvol*</c> volume, no resolvable template, or a
    /// <c>distance</c> that is not a usable mean spacing. Add the result to the world root at
    /// identity — its instance transforms are absolute world coordinates.</summary>
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
    // Resolved by ClutterBuilder's own template rule — a parentless Object3d of that name whose
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
                    // Retail-data-normal for C1B/C2/C3 — see the class remarks.
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

    // Half the card's largest local extent — what the billboard can swing outside the MultiMesh's
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

    // The card's own source geometry (verts, UVs and the authored vertex colours), triangulated by
    // the same fan/strip rule as SceneBuilder.EmitPolygon — the cloud cards are tri-strips.
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

    // Camera-facing billboard keeping the instance scale (the same hand-rolled billboard as
    // SceneBuilder's cloud-sprite shader — a MultiMesh cannot use Godot's billboard flag), plus
    // the authored far fade. `cull` collapses the quad to a point past the fade's far end, so a
    // sprite outside its draw distance is discarded before rasterization rather than costing a
    // screenful of alpha-0 fragments — which is what lets the whole map's field be one static
    // MultiMesh with no streaming. The fade distance is the true 3D one, not the fog's horizontal
    // cylinder: this is the sprite's own LOD range, and a cloud directly overhead is as far away
    // as one on the horizon.
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
            uniform vec2 far_fade = vec2(1e8, 1e9);  // metres: alpha 1 below x, 0 at y

            #include "res://shaders/csky_atmosphere.gdshaderinc"
            #include "res://shaders/csky_srgb.gdshaderinc"

            varying flat float v_alpha;

            void vertex() {
                vec3 origin = MODEL_MATRIX[3].xyz;
                float d = distance(origin, CAMERA_POSITION_WORLD);
                v_alpha = 1.0 - smoothstep(far_fade.x, far_fade.y, d);
                float cull = step(d, far_fade.y);
                MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
                    INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
                MODELVIEW_MATRIX[0] *= length(MODEL_MATRIX[0].xyz) * cull;
                MODELVIEW_MATRIX[1] *= length(MODEL_MATRIX[1].xyz) * cull;
                MODELVIEW_MATRIX[2] *= length(MODEL_MATRIX[2].xyz);
            }

            void fragment() {
                vec4 col = vec4(csky_srgb_to_linear(COLOR.rgb), COLOR.a) * texture(albedo_tex, UV);
            {{albedo}}
                ALPHA = col.a * v_alpha;
            }
            """;
    }

    // The scatter itself: each volume is cut into `distance` x `distance` cells anchored on the
    // world origin, and each cell gets ONE placement drawn uniformly inside it from the weighted
    // clutter table. `distance` is the field's areal DENSITY — its mean spacing — not a lattice
    // phase, so nothing about the field repeats: C1's 9,025 placements over the 12,288 m map are a
    // mean spacing of 129.3 m against the authored 130, and that number is invariant under the
    // randomisation. The authored `perturb_dist_range` still displaces each placement on top.
    //
    // ⚠ Cells, not N uniform draws over the whole footprint. One placement per cell is what keeps
    // the sheet CONTINUOUS: a Poisson field at this density opens holes big enough to see through,
    // and the thing being reproduced is an overcast.
    //
    // ⚠ ONE pass per VOLUME, not per clutter block. The blocks carry a `weight` and their `nodes`
    // lists carry weights of their own, which is a two-level weighted table — running the cells
    // once per block instead would double the authored density and stack cloudsprite1 on
    // cloudsprite2 in every cell. See docs/formats/fogvol.md for the density this reading produces
    // (C1: 1.6 sprite-areas of cover per unit of layer, i.e. an overcast one sprite deep).
    //
    // ⚠ Per volume, and overlapping volumes each get their own fill. C1C is why: its fvol1-9 tile
    // the whole map at 971-1091 m, and fvol10-23 are twelve smaller volumes sitting ON TOP of that
    // footprint, reaching 1391-1688 m. Taking only the first containing volume per cell drops all
    // twelve — the authored build-ups over specific places — and renders a flat deck instead.
    // The cells are anchored on the world origin (not on each volume), so a cell shared by two
    // volumes is the same X/Z in both and the stack is vertical, as authored.
    //
    // ⚠ Vertical placement is TOP-ANCHORED for sheet-thin volumes, UNIFORM for tall ones (A3,
    // docs/formats/fogvol.md). A volume no more than TopAnchorHeightFactor card-heights thick —
    // C1/C2B/C4's slabs and C1C's own fvol1-9 tiling — draws Y at the volume's own top and lets
    // `perp_dist_range` spread it afterward, matching the measured C1/C4 card bottoms (CAP-12).
    // Sampling AT the top rather than inventing a band works because `Contains` already runs the
    // EXACT face test (A2): for a sloped/tapered top the XZ drawn in the cell is simply rejected
    // when it falls outside the true top footprint at that height, so the accepted shape follows
    // the volume's own geometry with no separate per-column top lookup. C1C's twelve build-up
    // frusta and C5's seventeen street strips are far taller than a card and keep the old
    // full-height uniform draw — top-anchoring them would cap the build-ups into hollow shells and
    // lift C5's ground-level haze into an empty-streets sheet near the strip tops.
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
        // C1/C1C/C2B/C4, 70 m for C5) — every kind in a chapter shares one, so the largest among
        // them is that chapter's card height for the TopAnchorHeightFactor test below.
        float cardHeight = 0f;
        foreach (var kind in kinds)
        {
            cardHeight = Mathf.Max(cardHeight, kind.Radius * 2f);
        }

        // One draw sequence off the master seed's cloud stream, in a fixed volume/cell order, so
        // the whole field is a function of the seed — which is what makes a cloud shot reproducible.
        // Nothing here reads a camera, a pane count or a frame, so world-anchoring and determinism
        // are the same property: there is no per-view cell to hash.
        var rng = Rng.NewSystemRandom(Rng.Clouds);
        float Rand(float a, float b) => a + ((float)rng.NextDouble() * (b - a));

        foreach (var volume in volumes)
        {
            var box = volume.Box;
            // A3: sheet-thin volumes (a slab's own AABB height, not the field's overall extent)
            // anchor their draw at the volume's own top; tall ones keep filling uniformly. See the
            // method's own remarks above and TopAnchorHeightFactor's remarks for the evidence.
            bool topAnchored = box.End.Y - box.Position.Y <= cardHeight * TopAnchorHeightFactor;
            int gx0 = Mathf.CeilToInt(box.Position.X / period), gx1 = Mathf.FloorToInt(box.End.X / period);
            int gz0 = Mathf.CeilToInt(box.Position.Z / period), gz1 = Mathf.FloorToInt(box.End.Z / period);
            for (int gx = gx0; gx <= gx1 && InstanceCount < MaxPlacements; gx++)
            {
                // The cell centred on this multiple of the period, trimmed to the volume's own
                // bounds — and the outermost cell of each axis takes the remainder, so the cells
                // TILE the volume exactly. Cell count (hence density) is therefore unchanged by
                // the randomisation, and no strip along a volume wall is left without placements.
                float x0 = gx == gx0 ? box.Position.X : (gx * period) - (period * 0.5f);
                float x1 = gx == gx1 ? box.End.X : (gx * period) + (period * 0.5f);
                for (int gz = gz0; gz <= gz1 && InstanceCount < MaxPlacements; gz++)
                {
                    float z0 = gz == gz0 ? box.Position.Z : (gz * period) - (period * 0.5f);
                    float z1 = gz == gz1 ? box.End.Z : (gz * period) + (period * 0.5f);

                    float x = Rand(x0, x1);
                    float z = Rand(z0, z1);
                    // A3: top-anchored volumes draw Y at the volume's own top (box.End.Y) rather
                    // than across the full height; `perp_dist_range` is still added AFTER
                    // containment below, exactly as for a uniform draw — sampling at top+perp
                    // BEFORE the containment test would reject the whole field (A2's ordering
                    // trap), so the offset stays where it always was.
                    float y = topAnchored ? box.End.Y : Rand(box.Position.Y, box.End.Y);

                    // The volume is its AUTHORED shape, not its bounding box. Exact for
                    // C1/C2B/C4's slabs, so their fields are untouched by this; C1C's rotated
                    // tapering frusta hold 24 % of their bounds and C5's polygonal street prisms
                    // 84 %, and a cell whose draw lands outside places nothing — which is what
                    // preserves the authored spacing instead of crowding the surplus inward. For a
                    // top-anchored volume this is also what makes a sloped/tapered top narrow the
                    // accepted XZ on its own (see the method's remarks above).
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

                    // In-plane perturbation off the drawn point: an authored distance at a free
                    // bearing. It is applied AFTER containment, so a placement can sit up to
                    // perturb_dist_range.y outside its own volume's wall — which is what a
                    // perturbation means. The volume bounds where the field is placed; it does not
                    // clip each sprite.
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
                    InstanceCount++;
                }
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
            mat.SetShaderParameter("far_fade", kind.Block.FarFade);

            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = mesh,
                InstanceCount = kind.Placements.Count,
            };
            for (int i = 0; i < kind.Placements.Count; i++)
            {
                mm.SetInstanceTransform(i, kind.Placements[i]);
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
                      + $"fade {kind.Block.FarFade.X:0}-{kind.Block.FarFade.Y:0} m)");
        }
        Summary = string.Join(", ", parts);
    }

    // One alternative of the resolved scatter table: a clutter block paired with one of the
    // template nodes it names, and everything the gamez says about that node's sprite card.
    private sealed class Kind
    {
        public readonly List<Transform3D> Placements = new();

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
