using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The impostor-clump prototype for forest mass under Rich (<c>--canopy-clumps</c>): cards that each
/// show several trees, placed per <see cref="CanopyMask"/> cell. Each fades in past a near band. The
/// forest then reads as mass from a distance, while the original cards stay as they are up close.
/// The clump art is composed at load from the chapter's own listed card textures into one atlas of
/// <see cref="Variants"/> tiles. <see cref="Place"/> and <see cref="Layout"/> are pure; <see cref="Build"/>
/// draws them. Mech3.md has the rules and the plan's B13 the reasoning.
/// </summary>
public static class CanopyClumps
{
    /// <summary>The atlas tiles, each a different layout of trees.</summary>
    public const int Variants = 6;

    /// <summary>A clump tile's width in metres at a stamp's own scale: about three fir crowns across.</summary>
    public const float TileWidthM = 24f;

    /// <summary>TUNE. Clumps a full canopy cell holds on average; a cell holds its density times this.</summary>
    public const float ClumpsPerFullCell = 1.5f;

    /// <summary>TUNE. The camera distance a clump starts to fade in at, drawn per clump between these, in metres.
    /// Inside it only the original cards draw.</summary>
    public const float FadeNearMin = 350f, FadeNearMax = 900f;

    /// <summary>TUNE. How far past its near distance a clump is fully in, in metres.</summary>
    public const float FadeSpanM = 600f;

    /// <summary>The fixed seed of the placement and layout hash, so every launch draws the same clumps.</summary>
    public const int Seed = 0x5EED_C1;

    // How far a tree at the back of a tile stands above the front row, in metres. That is a row 16 m
    // deeper, seen from about 30 degrees above. The back rows are drawn first and darker.
    private const float DepthRiseM = 8f;
    private const float BackShade = 0.9f;

    // Trees in one tile, and the tile's pixels per metre (a 10 m fir is about 100 px, near its card's 128).
    private const int TreesMin = 4, TreesMax = 6;
    private const float PixelsPerM = 10f;

    // How far a clump's base is sunk below the interpolated ground, as a share of its height. A cross
    // slope under the card's width then shows no gap beneath its front row.
    private const float SinkShare = 0.18f;

    // The clump instances are split into square chunks of this edge, so the frustum culls them.
    private const float ChunkM = 2048f;

    private static Shader? _shader;


    /// <summary>The clumps over <paramref name="mask"/>. A covered cell holds its density times
    /// <see cref="ClumpsPerFullCell"/>, rounded by a draw. Each sits at a jittered point in the cell, on
    /// the ground interpolated between covered cells. A clump's size is its tile scaled by the cell's top less its
    /// ground over <paramref name="cardHeight"/>, the mean card height its tile is drawn at. Every draw is a
    /// hash of the cell, so a brush edit moves only the clumps of the cells it touched.</summary>
    public static List<Clump> Place(CanopyMask mask, float cardHeight, float tileHeight, int seed = Seed)
    {
        var clumps = new List<Clump>();
        for (int c = 0; c < mask.Density.Count; c++)
        {
            float d = mask.Density[c];
            float rise = mask.Top[c] - mask.Ground[c];
            if (d <= 0f || rise <= 0f || cardHeight <= 0f)
                continue;
            int n = (int)MathF.Floor((d * ClumpsPerFullCell) + U(seed, c, 0));
            float s = rise / cardHeight;
            for (int k = 0; k < n; k++)
            {
                int draw = 1 + (k * 8);
                float x = mask.MinX + (((c % mask.Width) + U(seed, c, draw)) * CanopyMask.CellM);
                float z = mask.MinZ + (((c / mask.Width) + U(seed, c, draw + 1)) * CanopyMask.CellM);
                float near = FadeNearMin + ((FadeNearMax - FadeNearMin) * U(seed, c, draw + 2));
                float h = tileHeight * s;
                clumps.Add(new Clump(new Vector3(x, GroundAt(mask, x, z, c) - (SinkShare * h), z), TileWidthM * s, h,
                    Math.Min(Variants - 1, (int)(U(seed, c, draw + 3) * Variants)), U(seed, c, draw + 4) < 0.5f,
                    near, near + FadeSpanM, 1.05f + (0.2f * U(seed, c, draw + 5))));
            }
        }

        return clumps;
    }

    /// <summary>The trees of atlas tile <paramref name="variant"/>, back row first. Each card is drawn by its
    /// <paramref name="weights"/> share (its stamp count), at its own <paramref name="sizes"/> in metres. The
    /// trees are spread across the tile in even strata, so no two pile on one spot.</summary>
    public static List<Tree> Layout(int variant, IReadOnlyList<float> weights, IReadOnlyList<Vector2> sizes, int seed = Seed)
    {
        int cell = -1 - variant;
        int n = TreesMin + Math.Min(TreesMax - TreesMin, (int)(U(seed, cell, 0) * (TreesMax - TreesMin + 1)));
        float total = weights.Sum();
        var trees = new List<Tree>(n);
        for (int i = 0; i < n; i++)
        {
            int draw = 1 + (i * 8);
            float pick = U(seed, cell, draw) * total;
            int card = 0;
            while (card < weights.Count - 1 && pick >= weights[card])
                pick -= weights[card++];
            float depth = U(seed, cell, draw + 1);
            float scale = 0.85f + (0.25f * U(seed, cell, draw + 2));
            float half = Math.Min(TileWidthM / 2f, sizes[card].X * scale / 2f);
            float slot = (i + U(seed, cell, draw + 3)) / n;
            float x = half + ((TileWidthM - (2f * half)) * slot);
            trees.Add(new Tree(card, x, depth * DepthRiseM, scale, U(seed, cell, draw + 4) < 0.5f, 1f - ((1f - BackShade) * depth)));
        }

        return trees.OrderByDescending(t => t.Base).ToList();
    }

    /// <summary>A tile's height in metres: the back row's rise plus the tallest card at the largest scale.</summary>
    public static float TileHeight(IReadOnlyList<Vector2> sizes) => DepthRiseM + (sizes.Max(s => s.Y) * 1.1f);

    /// <summary>The ground at a world X/Z, bilinear between the centres of the covered cells around it; the
    /// <paramref name="own"/> cell's ground where none weighs in.</summary>
    public static float GroundAt(CanopyMask mask, float x, float z, int own)
    {
        float u = ((x - mask.MinX) / CanopyMask.CellM) - 0.5f, v = ((z - mask.MinZ) / CanopyMask.CellM) - 0.5f;
        int i0 = (int)MathF.Floor(u), j0 = (int)MathF.Floor(v);
        float sum = 0f, weight = 0f;
        for (int k = 0; k < 4; k++)
        {
            int i = i0 + (k & 1), j = j0 + (k >> 1);
            if (i < 0 || j < 0 || i >= mask.Width || j >= mask.Height || mask.Density[(j * mask.Width) + i] <= 0f)
                continue;
            float w = (1f - MathF.Abs(u - i)) * (1f - MathF.Abs(v - j));
            sum += w * mask.Ground[(j * mask.Width) + i];
            weight += w;
        }

        return weight > 1e-6f ? sum / weight : mask.Ground[own];
    }

    /// <summary>Draws the clumps over <paramref name="mask"/>, their atlas composed from the listed card kinds
    /// in <paramref name="kinds"/> (a Rich variant and a card texture). Null when no listed card resolves.</summary>
    public static Node3D? Build(CanopyMask mask, IReadOnlyList<ClutterBuilder.KindExport> kinds, TextureArchive textures)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var cards = kinds.Where(k => k.Rich != null && !k.Solid && k.Placements.Count > 0 && k.Height > 0f)
            .GroupBy(k => k.Texture, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Texture: g.Key, Size: new Vector2(g.First().Width, g.First().Height), Weight: (float)g.Sum(k => k.Placements.Count),
                Image: textures.FindImage(g.Key)))
            .Where(c => c.Image != null)
            .ToList();
        if (cards.Count == 0)
            return null;
        var sizes = cards.Select(c => c.Size).ToList();
        var weights = cards.Select(c => c.Weight).ToList();
        float tileHeight = TileHeight(sizes);
        float cardHeight = cards.Sum(c => c.Weight * c.Size.Y) / weights.Sum();
        var atlas = Compose(cards.Select(c => c.Image!).ToList(), sizes, weights, tileHeight);
        var clumps = Place(mask, cardHeight, tileHeight);

        var material = new ShaderMaterial { Shader = _shader ??= new Shader { Code = ShaderCode() } };
        material.SetShaderParameter("albedo_tex", TextureUpload.Create(atlas));
        material.SetShaderParameter("variants", (float)Variants);
        var root = new Node3D { Name = "CanopyClumps" };
        var mesh = new QuadMesh { Size = Vector2.One, CenterOffset = new Vector3(0f, 0.5f, 0f) };
        float margin = clumps.Count > 0 ? clumps.Max(c => c.Width) : 0f;
        foreach (var chunk in clumps.GroupBy(c => (MathF.Floor(c.Base.X / ChunkM), MathF.Floor(c.Base.Z / ChunkM))))
        {
            var list = chunk.ToList();
            // ⚠ The colour and custom-data flags must be set before InstanceCount, or the buffer has no slot.
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                UseCustomData = true,
                Mesh = mesh,
                InstanceCount = list.Count,
            };
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                var basis = new Basis(new Vector3(c.Mirror ? -c.Width : c.Width, 0f, 0f), new Vector3(0f, c.Height, 0f), Vector3.Back);
                float n2 = c.FadeNear * c.FadeNear, f2 = c.FadeFar * c.FadeFar;
                mm.SetInstanceTransform(i, new Transform3D(basis, c.Base));
                mm.SetInstanceColor(i, new Color(c.Brightness, c.Brightness, c.Brightness));
                mm.SetInstanceCustomData(i, new Color(n2, c.Variant, 1f / (f2 - n2), 0f));
            }
            root.AddChild(new MultiMeshInstance3D
            {
                Multimesh = mm,
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                ExtraCullMargin = margin,
                Name = string.Create(CultureInfo.InvariantCulture, $"chunk_{chunk.Key.Item1}_{chunk.Key.Item2}"),
            });
        }

        Log.Info("world",
            $"canopy clumps: {clumps.Count} over {root.GetChildCount()} chunks from {cards.Count} card textures ({string.Join(", ", cards.Select(c => c.Texture))}), tile {TileWidthM:0.#}x{tileHeight:0.#} m at card height {cardHeight:0.#} m, {watch.ElapsedMilliseconds} ms");
        return root;
    }

    // The atlas: one tile per variant, side by side. Each tree is its card resized to the tile's metres,
    // mirrored and shaded by its row, and blended back row first. Mipmapped, since clumps are seen far off.
    private static Image Compose(List<Image> images, List<Vector2> sizes, List<float> weights, float tileHeight)
    {
        int tw = (int)(TileWidthM * PixelsPerM), th = (int)MathF.Ceiling(tileHeight * PixelsPerM);
        var atlas = Image.CreateEmpty(tw * Variants, th, false, Image.Format.Rgba8);
        foreach (var image in images)
        {
            image.Decompress();
            image.Convert(Image.Format.Rgba8);
            Bleed(image);
        }
        for (int v = 0; v < Variants; v++)
        {
            foreach (var tree in Layout(v, weights, sizes))
            {
                int pw = Math.Max(1, (int)(sizes[tree.Card].X * tree.Scale * PixelsPerM));
                int ph = Math.Max(1, (int)(sizes[tree.Card].Y * tree.Scale * PixelsPerM));
                var src = (Image)images[tree.Card].Duplicate();
                src.Resize(pw, ph, Image.Interpolation.Lanczos);
                if (tree.Mirror)
                    src.FlipX();
                byte[] px = src.GetData();
                for (int i = 0; i < px.Length; i += 4)
                {
                    px[i] = (byte)(px[i] * tree.Shade);
                    px[i + 1] = (byte)(px[i + 1] * tree.Shade);
                    px[i + 2] = (byte)(px[i + 2] * tree.Shade);
                }
                src.SetData(pw, ph, false, Image.Format.Rgba8, px);
                int x = (v * tw) + (int)((tree.X * PixelsPerM) - (pw / 2f));
                int y = th - (int)(tree.Base * PixelsPerM) - ph;
                atlas.BlendRect(src, new Rect2I(0, 0, pw, ph), new Vector2I(x, y));
                src.Dispose();
            }
        }
        Bleed(atlas);
        atlas.GenerateMipmaps();
        return atlas;
    }

    // Gives every clear texel the mean colour of the solid ones. A filter, a resize or a mip level then
    // blends tree colour into an edge. The clear texels' black would darken the clumps far off.
    private static void Bleed(Image image)
    {
        byte[] px = image.GetData();
        long r = 0, g = 0, b = 0, n = 0;
        for (int i = 0; i < px.Length; i += 4)
        {
            if (px[i + 3] < 128)
                continue;
            r += px[i];
            g += px[i + 1];
            b += px[i + 2];
            n++;
        }
        if (n == 0)
            return;
        for (int i = 0; i < px.Length; i += 4)
        {
            if (px[i + 3] != 0)
                continue;
            px[i] = (byte)(r / n);
            px[i + 1] = (byte)(g / n);
            px[i + 2] = (byte)(b / n);
        }
        image.SetData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, px);
    }

    // A uniform draw in [0, 1) hashed from the seed, a cell (or a negative tile id) and a draw index.
    // A hash, so a cell's draws do not depend on how many any other cell took.
    private static float U(int seed, int cell, int draw)
    {
        uint h = unchecked(((uint)cell * 0x9E3779B1u) ^ ((uint)draw * 0x85EBCA77u) ^ (uint)seed);
        h ^= h >> 16;
        h = unchecked(h * 0x7FEB352Du);
        h ^= h >> 15;
        h = unchecked(h * 0x846CA68Bu);
        h ^= h >> 16;
        return (h >> 8) / 16777216f;
    }

    // The card shader's look on a clump: the same face, fog, sun side and tint lines. The fade is turned
    // round, in past its band and gone inside it. The tile comes off the custom data's second channel.
    private static string ShaderCode() => $$"""
        shader_type spatial;
        render_mode skip_vertex_transform, unshaded, cull_disabled, shadows_disabled, blend_mix, depth_prepass_alpha;

        uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_disable;
        uniform float variants = 1.0;

        #include "res://shaders/csky_atmosphere.gdshaderinc"
        #include "res://shaders/csky_srgb.gdshaderinc"
        #include "res://shaders/csky_clutter_fade.gdshaderinc"
        #include "res://shaders/csky_mip_bias.gdshaderinc"
        #include "res://shaders/csky_instance_uniforms.gdshaderinc"

        varying flat float v_alpha;
        varying float v_side;
        varying flat float v_sun;

        void vertex() {
            vec3 origin = MODEL_MATRIX[3].xyz;
            vec3 to = origin - CAMERA_POSITION_WORLD;
            v_alpha = clamp((dot(to, to) - INSTANCE_CUSTOM.x) * INSTANCE_CUSTOM.z, 0.0, 1.0);
            float keep = step(0.004, v_alpha);
            vec2 to_cam = CAMERA_POSITION_WORLD.xz - origin.xz;
            float len = length(to_cam);
            vec2 dir = len > 1e-4 ? to_cam / len : vec2(0.0, 1.0);
            mat3 face = mat3(vec3(dir.y, 0.0, -dir.x), vec3(0.0, 1.0, 0.0), vec3(dir.x, 0.0, dir.y));
            vec3 local = vec3(VERTEX.x * MODEL_MATRIX[0].x, VERTEX.y * MODEL_MATRIX[1].y, 0.0);
            v_side = sign(local.x);
            v_sun = dot(face[0].xz, normalize(csky_sun_dir.xz + vec2(1e-4, 0.0))) * clamp(csky_sun_light.y, 0.0, 1.0);
            UV.x = (UV.x + INSTANCE_CUSTOM.y) / variants;
            VERTEX = (VIEW_MATRIX * vec4(origin + face * local * keep, 1.0)).xyz;
        }

        void fragment() {
            vec4 col = vec4(csky_srgb_to_linear(COLOR.rgb), COLOR.a) * {{SceneBuilder.SampleAlbedo("UV")}};
            ALBEDO = col.rgb;
        {{ClutterBuilder.RichFragmentLines}}{{ClutterBuilder.FogLines}}{{SceneBuilder.TintLine}}
            ALPHA = col.a * v_alpha;
        }
        """;

    /// <summary>One clump: its base on the ground, its size in metres, its atlas tile and mirror, its
    /// fade-in band and its brightness.</summary>
    public readonly record struct Clump(Vector3 Base, float Width, float Height, int Variant, bool Mirror,
        float FadeNear, float FadeFar, float Brightness);

    /// <summary>One tree in a tile: its card, its centre and base in tile metres, and its scale on the card.
    /// It may be mirrored, and its shade is 1 at the front row.</summary>
    public readonly record struct Tree(int Card, float X, float Base, float Scale, bool Mirror, float Shade);
}
