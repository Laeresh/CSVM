using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Stacked alpha-tested shells over the canopy mask's forest (<c>--canopy-shells</c>, Rich only): the
/// layered-forest technique. A card forest then reads as mass from above and past the cards' fade. Each chunk
/// of mask cells is one grid mesh at the canopy's ground, drawn once per layer by a MultiMesh. Its custom data
/// lifts each layer a share of the canopy's height. The fragment stage turns the chapter's most-stamped card
/// about its axis into crowns on a jittered lattice, kept by the mask's density. Mech3.md has the rules.
/// </summary>
public static class CanopyShells
{
    /// <summary>Shells per chunk, bottom to top.</summary>
    public const int Layers = 8;

    /// <summary>The lowest shell's share of the canopy height. It clears the ground by metres, so the shells never
    /// fight its depth; the card's lowest rows are its darkest anyway.</summary>
    public const float BaseShare = 0.15f;

    /// <summary>Mask cells along each side of one chunk: 2 km on a 32 m cell, the unit the camera culls.</summary>
    public const int ChunkCells = 64;

    /// <summary>The node name the session's shells stand under.</summary>
    public const string NodeName = "CanopyShells";

    // The crown lattice step in metres. A full mask cell holds one fir crown in cards; the shells close it.
    private const float CrownSpacing = 9f;

    /// <summary>The share of the canopy height shell <paramref name="layer"/> stands at.</summary>
    public static float LayerShare(int layer) => BaseShare + ((1f - BaseShare) * layer / (Layers - 1));

    /// <summary>The cards' mean fade band, in the custom-data form the clutter fade reads (near², far²,
    /// 1/(far² - near²), 0), from each stamp's fade. Stamps that never fade are left out. None fading gives zero,
    /// which reads as never fading, so the shells then show only by the view's angle.</summary>
    public static Vector4 FadeBand(IEnumerable<Color> fades)
    {
        double near = 0, far = 0;
        int n = 0;
        foreach (var f in fades)
        {
            if (f.G <= 0f)
                continue;
            near += f.R;
            far += f.G;
            n++;
        }
        if (n == 0)
            return Vector4.Zero;
        float nearSq = (float)(near / n), farSq = (float)(far / n);
        return new Vector4(nearSq, farSq, farSq > nearSq ? 1f / (farSq - nearSq) : 0f, 0f);
    }

    /// <summary>The chunks covering <paramref name="mask"/>'s forest, none for an empty mask.</summary>
    public static List<Chunk> Grid(CanopyMask mask)
    {
        int w = mask.Width, h = mask.Height;
        var chunks = new List<Chunk>();
        for (int cj = 0; cj < h; cj += ChunkCells)
        {
            for (int ci = 0; ci < w; ci += ChunkCells)
            {
                if (ChunkOf(mask, ci, cj) is { } chunk)
                    chunks.Add(chunk);
            }
        }
        return chunks;
    }

    /// <summary>The shells over <paramref name="mask"/>, their crowns drawn from the most-stamped card kind with a
    /// Rich variant in <paramref name="kinds"/>. Null when the mask covers nothing or no card has a texture.</summary>
    public static Node3D? Build(CanopyMask mask, IReadOnlyList<ClutterBuilder.KindExport> kinds)
    {
        var card = kinds.Where(k => k.Rich != null && !k.Solid && k.Height > 0f)
            .OrderByDescending(k => k.Placements.Count).FirstOrDefault();
        if (card?.Material is not ShaderMaterial cardMaterial
            || cardMaterial.GetShaderParameter("albedo_tex").As<Texture2D>() is not { } cardTexture)
            return null;
        var chunks = Grid(mask);
        if (chunks.Count == 0)
            return null;

        var bytes = new byte[mask.Width * mask.Height];
        for (int c = 0; c < bytes.Length; c++)
            bytes[c] = (byte)Mathf.RoundToInt(Math.Clamp(mask.Density[c], 0f, 1f) * 255f);
        var density = TextureUpload.Create(mask.Width, mask.Height, Image.Format.R8, bytes);
        var band = FadeBand(kinds.Where(k => k.Rich != null && !k.Solid).SelectMany(k => k.Fades));
        // The card's own lit flag lives only in its shader text; the shells follow it so both take the world light alike.
        bool lit = cardMaterial.Shader?.Code.Contains("csky_world_light_at", StringComparison.Ordinal) ?? false;
        var material = new ShaderMaterial { Shader = new Shader { Code = ShaderCode(lit) } };
        material.SetShaderParameter("card_tex", cardTexture);
        material.SetShaderParameter("density_tex", density);
        material.SetShaderParameter("area", new Vector4(mask.MinX, mask.MinZ,
            1f / (mask.Width * CanopyMask.CellM), 1f / (mask.Height * CanopyMask.CellM)));
        material.SetShaderParameter("card_band", band);
        material.SetShaderParameter("card_aspect", card.Width / card.Height);
        material.SetShaderParameter("card_texels", (float)cardTexture.GetWidth());

        var root = new Node3D { Name = NodeName };
        foreach (var chunk in chunks)
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = chunk.Corners;
            arrays[(int)Mesh.ArrayType.TexUV] = chunk.Heights.Select(y => new Vector2(0f, y)).ToArray();
            arrays[(int)Mesh.ArrayType.Index] = chunk.Indices;
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.SurfaceSetMaterial(0, material);
            var layers = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseCustomData = true,
                Mesh = mesh,
                InstanceCount = Layers,
            };
            for (int l = 0; l < Layers; l++)
            {
                layers.SetInstanceTransform(l, Transform3D.Identity);
                layers.SetInstanceCustomData(l, new Color(LayerShare(l), 0f, 0f, 0f));
            }
            // The vertex stage lifts each layer, so the box the camera culls by is the chunk's whole canopy.
            root.AddChild(new MultiMeshInstance3D
            {
                Multimesh = layers,
                CustomAabb = chunk.Box,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
        int corners = chunks.Sum(c => c.Corners.Length);
        int near = (int)MathF.Sqrt(band.X), far = (int)MathF.Sqrt(band.Y);
        Log.Info("world", $"canopy shells: chunks={chunks.Count} layers={Layers} corners={corners} card={card.Texture} band_m={near}-{far}");
        return root;
    }

    // One chunk's grid, null when nothing in it is drawn.
    private static Chunk? ChunkOf(CanopyMask mask, int ci, int cj)
    {
        int w = mask.Width, h = mask.Height;
        var d = mask.Density;
        bool Covered(int i, int j) => i >= 0 && j >= 0 && i < w && j < h && d[(j * w) + i] > 0f;
        var index = new Dictionary<(int, int), int>();
        var corners = new List<Vector3>();
        var heights = new List<float>();
        var indices = new List<int>();
        var quad = new (int I, int J)[] { (0, 0), (1, 0), (1, 1), (0, 1) };
        Vector3 lo = new(float.MaxValue, float.MaxValue, float.MaxValue), hi = -lo;
        for (int j = cj; j < Math.Min(cj + ChunkCells, h); j++)
        {
            for (int i = ci; i < Math.Min(ci + ChunkCells, w); i++)
            {
                bool drawn = false;
                for (int k = 0; k < 9 && !drawn; k++)
                    drawn = Covered(i + (k % 3) - 1, j + (k / 3) - 1);
                if (!drawn)
                    continue;
                var at = new int[4];
                for (int q = 0; q < 4; q++)
                {
                    var key = (i + quad[q].I, j + quad[q].J);
                    if (!index.TryGetValue(key, out at[q]))
                    {
                        var (ground, height) = Corner(mask, key.Item1, key.Item2, i, j);
                        at[q] = index[key] = corners.Count;
                        var p = new Vector3(mask.MinX + (key.Item1 * CanopyMask.CellM), ground, mask.MinZ + (key.Item2 * CanopyMask.CellM));
                        corners.Add(p);
                        heights.Add(height);
                        lo = lo.Min(p);
                        hi = hi.Max(p + new Vector3(0f, height, 0f));
                    }
                }
                indices.AddRange(new[] { at[0], at[1], at[2], at[0], at[2], at[3] });
            }
        }
        return corners.Count == 0 ? null : new Chunk(corners.ToArray(), heights.ToArray(), indices.ToArray(), new Aabb(lo, hi - lo));
    }

    // A corner's ground and canopy height, density-weighted over the covered cells touching it. A corner touching
    // none takes the nearest covered cell around the drawn cell (i, j) it belongs to, so the halo sits on its forest.
    private static (float Ground, float Height) Corner(CanopyMask mask, int ci, int cj, int i, int j)
    {
        int w = mask.Width, h = mask.Height;
        float weight = 0f, ground = 0f, top = 0f;
        for (int k = 0; k < 4; k++)
        {
            int x = ci - 1 + (k & 1), z = cj - 1 + (k >> 1);
            if (x < 0 || z < 0 || x >= w || z >= h || mask.Density[(z * w) + x] <= 0f)
                continue;
            int c = (z * w) + x;
            weight += mask.Density[c];
            ground += mask.Density[c] * mask.Ground[c];
            top += mask.Density[c] * mask.Top[c];
        }
        if (weight > 0f)
            return (ground / weight, (top - ground) / weight);
        for (int k = 0; k < 9; k++)
        {
            int x = i + (k % 3) - 1, z = j + (k / 3) - 1;
            if (x < 0 || z < 0 || x >= w || z >= h || mask.Density[(z * w) + x] <= 0f)
                continue;
            int c = (z * w) + x;
            return (mask.Ground[c], mask.Top[c] - mask.Ground[c]);
        }
        return (0f, 0f);
    }

    // The shell's shader. Unshaded and fogged as the cards are; a fragment shows its nearest kept crown's card texel.
    // Crowns sit one a lattice cell, jittered, sized 0.8 to 1.15 of the canopy height, kept below the mask's density.
    // The card's v is the fragment's height on its crown, its u the distance off the axis over the card's half width.
    private static string ShaderCode(bool lit) => $$"""
        shader_type spatial;
        render_mode unshaded, cull_disabled, shadows_disabled;

        uniform sampler2D card_tex : source_color, filter_linear_mipmap, repeat_disable;
        uniform sampler2D density_tex : filter_linear, repeat_disable;
        uniform vec4 area;
        uniform vec4 card_band;
        uniform float card_aspect;
        uniform float card_texels;

        #include "res://shaders/csky_atmosphere.gdshaderinc"
        #include "res://shaders/csky_clutter_fade.gdshaderinc"
        #include "res://shaders/csky_instance_uniforms.gdshaderinc"

        varying vec3 v_world;
        varying float v_share;
        varying float v_height;

        const float SPACING = {{CrownSpacing.ToString("0.0###", System.Globalization.CultureInfo.InvariantCulture)}};
        const float LAYER_STEP = {{((1f - BaseShare) / (Layers - 1)).ToString("0.0#####", System.Globalization.CultureInfo.InvariantCulture)}};
        const int MARCH = 3;

        vec4 crown_hash(vec2 p) {
            vec4 p4 = fract(p.xyxy * vec4(0.1031, 0.1030, 0.0973, 0.1099));
            p4 += dot(p4, p4.wzxy + 33.33);
            return fract((p4.xxyz + p4.yzzw) * p4.zywx);
        }

        void vertex() {
            v_share = INSTANCE_CUSTOM.x;
            v_height = UV.y;
            VERTEX.y += v_share * v_height;
            v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
        }

        // How far a crown at `p` has taken over: as the cards fade out, and as the eye climbs over the upright cards.
        // Height over the crown, not the ray's angle, so one view shows one density from its foreground to the fog.
        float shown_at(vec3 p, vec3 eye) {
            vec3 ray = p - eye;
            float dist2 = dot(ray, ray);
            float over = smoothstep(150.0, 300.0, -ray.y) * smoothstep(150.0 * 150.0, 300.0 * 300.0, dist2);
            return max(1.0 - csky_clutter_fade_alpha(p, eye, card_band), over);
        }

        // The card texel of the nearest kept crown at `xz` and `up` metres over the ground at `y`, alpha 0 for none.
        // A fade thins whole crowns, judged at each crown's centre, so its edge never cuts one.
        vec4 crown_at(vec2 xz, float up, float y, float lod, vec3 eye, out vec2 best_off) {
            vec2 base = floor(xz / SPACING);
            vec4 best = vec4(0.0);
            best_off = vec2(0.0);
            for (int k = 0; k < 9; k++) {
                vec2 cell = base + vec2(float(k % 3 - 1), float(k / 3 - 1));
                vec4 r = crown_hash(cell);
                vec2 centre = (cell + 0.15 + 0.7 * r.xy) * SPACING;
                float keep = texture(density_tex, (centre - area.xy) * area.zw).r * shown_at(vec3(centre.x, y, centre.y), eye);
                if (r.z >= keep) {
                    continue;
                }
                float tree = v_height * (0.8 + 0.35 * r.w);
                vec2 off = xz - centre;
                vec2 uv = vec2(0.5 + sign(off.x + 1e-4) * length(off) / (tree * card_aspect), 1.0 - up / tree);
                if (uv.y <= 0.0 || uv.x <= 0.0 || uv.x >= 1.0) {
                    continue;
                }
                vec4 t = textureLod(card_tex, uv, lod);
                if (t.a > best.a) {
                    best = t;
                    best_off = off;
                }
            }
            return best;
        }

        void fragment() {
            if (v_height < 1.0) {
                discard;
            }
            // The crowns' lookups jump between neighbours, so the mip comes from the world footprint, not the UVs.
            float lod = max(0.0, log2(max(fwidth(v_world.x), fwidth(v_world.z)) * card_texels / (v_height * card_aspect)));
            // Each shell draws the slab down to the next along the view ray. A crown seen from the side then
            // shows no gaps between its slices. A grazing ray's march stops short of its long run through the slab.
            vec3 dir = normalize(v_world - CAMERA_POSITION_WORLD);
            float slab = v_height * LAYER_STEP;
            vec3 step = dir * (slab / float(MARCH)) / max(-dir.y, 0.1);
            vec4 best = vec4(0.0);
            vec2 best_off = vec2(0.0);
            float up = v_share * v_height;
            for (int m = 0; m < MARCH && best.a < 0.5; m++) {
                vec3 p = v_world + step * float(m);
                up = v_share * v_height - (slab * float(m) / float(MARCH));
                best = crown_at(p.xz, up, p.y - up, lod, CAMERA_POSITION_WORLD, best_off);
            }
            if (best.a < 0.5) {
                discard;
            }
            // A lower shell sits in its crown's own shade, so the stack reads as depth from above. The sun side
            // gains as the Rich cards' does, so a crown and a card beside it read alike.
            float sun = dot(normalize(best_off + vec2(1e-4, 0.0)), normalize(csky_sun_dir.xz + vec2(1e-4, 0.0)));
            ALBEDO = best.rgb * mix(0.8, 1.0, up / v_height){{(lit ? " * csky_world_light_at(CAMERA_POSITION_WORLD)" : "")}};
            ALBEDO *= 1.0 + max(sun, 0.0) * clamp(csky_sun_light.y, 0.0, 1.0) * vec3(1.6, 1.5, 1.2);
            float fog_amt = csky_fog_amount(v_world, CAMERA_POSITION_WORLD);
            ALBEDO = mix(ALBEDO, csky_fog_color_at(CAMERA_POSITION_WORLD), csky_fog_on * fog_amt);
            ALBEDO = mix(ALBEDO, csky_tint.rgb, csky_tint.a);
        }
        """;

    /// <summary>One chunk's grid at the canopy's ground. A corner's ground and canopy height are density-weighted
    /// over the covered cells touching it. A cell is drawn when it or a neighbour is covered. The bilinear density
    /// then thins crowns out across the forest's edge, where a quad edge would cut them.</summary>
    public sealed record Chunk(Vector3[] Corners, float[] Heights, int[] Indices, Aabb Box);
}
