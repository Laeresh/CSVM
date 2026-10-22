using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Forest mass under Rich: impostor clumps, cards that each show a few trees, placed per
/// <see cref="CanopyMask"/> cell at the card forest's own trees per area. Past a handover band the
/// clumps carry the forest and the listed cards fade out. The far forest keeps the near one's density
/// and colour, and continues past the card fade and the map edge (<see cref="Field.Copy"/>).
/// The clump art is composed at load from the chapter's listed card textures into one atlas of
/// <see cref="Variants"/> tiles. <see cref="Place"/> and <see cref="Layout"/> are pure. Mech3.md has the rules.
/// </summary>
public static class CanopyClumps
{
    /// <summary>The atlas tiles, each a different layout of trees.</summary>
    public const int Variants = 6;

    /// <summary>A clump tile's width in metres at a stamp's own scale: about three fir crowns across.</summary>
    public const float TileWidthM = 24f;

    /// <summary>TUNE. The camera distance in metres the handover starts at: inside it the cards alone draw,
    /// past <see cref="HandoverFarM"/> the clumps alone. Plain metres, so no View Distance moves it.</summary>
    public const float HandoverNearM = 1000f, HandoverFarM = 1600f;

    /// <summary>The fixed seed of the placement and layout hash, so every launch draws the same clumps.</summary>
    public const int Seed = 0x5EED_C1;

    /// <summary>The shader parameter both the clumps and the listed cards' Rich material read the band from.</summary>
    public const string HandoverParam = "canopy_handover";

    // How far a tree at the back of a tile stands above the front row, in metres. That is a row 16 m
    // deeper, seen from about 30 degrees above. The back rows are drawn first.
    private const float DepthRiseM = 8f;

    // Trees in one tile, and the tile's pixels per metre (a 10 m fir is about 100 px, near its card's 128).
    // Few trees a tile, so the clumps spread the forest about as evenly as the one-a-cell card lattice.
    private const int TreesMin = 2, TreesMax = 4;
    private const float PixelsPerM = 10f;

    // How far a clump's base is sunk below the lowest ground under its width, in metres. The base sits
    // that low so a cross slope shows no gap beneath its front row; a share of its height cut its trees short.
    private const float SinkM = 0.5f;

    // The clump instances are split into square chunks of this edge, so the frustum culls them.
    private const float ChunkM = 2048f;

    private static Shader? _shader;

    /// <summary>Whether a chapter draws clumps when <c>--canopy-clumps</c> does not say. C1 only: the other
    /// stamp-mask chapters were checked and left off, and C3's painted canopy is its own volume.</summary>
    public static bool DefaultOn(string? chapter) => string.Equals(chapter, "C1", StringComparison.OrdinalIgnoreCase);

    /// <summary>The clumps each cell of <paramref name="mask"/> holds on average: the kept card stamps planted in
    /// it over <paramref name="meanTrees"/>. The clumps then carry the cards' trees per area cell by cell. The
    /// mask's density is held at one fir a cell, so a dense stand would lose trees through it.</summary>
    public static float[] Rate(CanopyMask mask, IEnumerable<Vector3> stamps, float meanTrees)
    {
        var rate = new float[mask.Density.Count];
        foreach (var p in stamps)
        {
            if (mask.CellAt(p.X, p.Z) is int c and >= 0)
                rate[c] += 1f / meanTrees;
        }

        return rate;
    }

    /// <summary>The clumps over <paramref name="mask"/>. A covered cell holds its <paramref name="rate"/>
    /// rounded by a draw. Each sits at a jittered point in the cell, on the lowest ground under its width
    /// interpolated between covered cells. A clump's size is its tile scaled by the cell's top less its ground
    /// over <paramref name="cardHeight"/>. Its colour spreads as the Rich cards' does, by
    /// <paramref name="brightness"/> overall and <paramref name="tint"/> per channel.</summary>
    public static List<Clump> Place(CanopyMask mask, IReadOnlyList<float> rate, float cardHeight, float tileHeight,
        float brightness = 0f, float tint = 0f, int seed = Seed)
    {
        var clumps = new List<Clump>();
        for (int c = 0; c < mask.Density.Count; c++)
        {
            float rise = mask.Top[c] - mask.Ground[c];
            if (mask.Density[c] <= 0f || rate[c] <= 0f || rise <= 0f || cardHeight <= 0f)
                continue;
            int n = (int)MathF.Floor(rate[c] + U(seed, c, 0));
            float s = rise / cardHeight;
            for (int k = 0; k < n; k++)
            {
                int draw = 1 + (k * 8);
                float x = mask.MinX + (((c % mask.Width) + U(seed, c, draw)) * CanopyMask.CellM);
                float z = mask.MinZ + (((c / mask.Width) + U(seed, c, draw + 1)) * CanopyMask.CellM);
                float h = tileHeight * s, half = TileWidthM * s / 2f;
                float b = 1f + (brightness * S(seed, c, draw + 4));
                var colour = new Color(b * (1f + (tint * S(seed, c, draw + 5))), b * (1f + (tint * S(seed, c, draw + 6))),
                    b * (1f + (tint * S(seed, c, draw + 7))));
                float ground = Math.Min(GroundAt(mask, x, z, c), Math.Min(
                    Math.Min(GroundAt(mask, x - half, z, c), GroundAt(mask, x + half, z, c)),
                    Math.Min(GroundAt(mask, x, z - half, c), GroundAt(mask, x, z + half, c))));
                clumps.Add(new Clump(new Vector3(x, ground - SinkM, z), TileWidthM * s, h,
                    Math.Min(Variants - 1, (int)(U(seed, c, draw + 2) * Variants)), U(seed, c, draw + 3) < 0.5f, colour));
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
            trees.Add(new Tree(card, x, depth * DepthRiseM, scale, U(seed, cell, draw + 4) < 0.5f));
        }

        return trees.OrderByDescending(t => t.Base).ToList();
    }

    /// <summary>The mean tree count of the atlas tiles, what a clump carries of the cards' trees.</summary>
    public static float MeanTrees(IReadOnlyList<float> weights, IReadOnlyList<Vector2> sizes) =>
        (float)Enumerable.Range(0, Variants).Average(v => Layout(v, weights, sizes).Count);

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

    /// <summary>Hands the listed cards over to the clumps, or back. Their Rich material fades them out over the
    /// band while <paramref name="on"/>, and draws them at every distance otherwise.</summary>
    public static void Handover(IReadOnlyList<ClutterBuilder.KindExport>? kinds, bool on)
    {
        foreach (var kind in kinds ?? Array.Empty<ClutterBuilder.KindExport>())
            (kind.Rich?.Material as ShaderMaterial)?.SetShaderParameter(HandoverParam, on ? new Vector2(HandoverNearM, HandoverFarM) : Vector2.Zero);
    }

    /// <summary>Draws the clumps over <paramref name="mask"/>, their atlas composed from the listed card kinds in
    /// <paramref name="kinds"/>, their colour spread from <paramref name="entry"/>. Hands the cards over to them.
    /// Null when no listed card resolves.</summary>
    public static Field? Build(CanopyMask mask, IReadOnlyList<ClutterBuilder.KindExport> kinds, VegetationEntry entry,
        TextureArchive textures)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var cards = kinds.Where(k => k.Rich != null && !k.Solid && k.Height > 0f)
            .Select(k => (Kind: k, Kept: k.Rich!.Placements.Count(p => p.Basis.Y.LengthSquared() > 0f)))
            .Where(k => k.Kept > 0)
            .GroupBy(k => k.Kind.Texture, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Texture: g.Key, Size: new Vector2(g.First().Kind.Width, g.First().Kind.Height), Weight: (float)g.Sum(k => k.Kept),
                Look: entry.Find(g.First().Kind.Model), Image: textures.FindImage(g.Key)))
            .Where(c => c.Image != null)
            .ToList();
        if (cards.Count == 0)
            return null;
        var sizes = cards.Select(c => c.Size).ToList();
        var weights = cards.Select(c => c.Weight).ToList();
        float stamps = weights.Sum();
        float tileHeight = TileHeight(sizes);
        float cardHeight = cards.Sum(c => c.Weight * c.Size.Y) / stamps;
        float meanTrees = MeanTrees(weights, sizes);
        var rate = Rate(mask, kinds.Where(k => k.Rich != null && !k.Solid && k.Height > 0f)
            .SelectMany(k => k.Rich!.Placements.Where(p => p.Basis.Y.LengthSquared() > 0f).Select(p => p.Origin)), meanTrees);
        float brightness = cards.Sum(c => c.Weight * (c.Look?.Brightness ?? 0f)) / stamps;
        float tint = cards.Sum(c => c.Weight * (c.Look?.Tint ?? 0f)) / stamps;
        var (atlas, side) = Compose(cards.Select(c => c.Image!).ToList(), sizes, weights, tileHeight);
        var clumps = Place(mask, rate, cardHeight, tileHeight, brightness, tint);

        var material = new ShaderMaterial { Shader = _shader ??= new Shader { Code = ShaderCode() } };
        material.SetShaderParameter("albedo_tex", TextureUpload.Create(atlas));
        material.SetShaderParameter("side_tex", TextureUpload.Create(side));
        material.SetShaderParameter("variants", (float)Variants);
        material.SetShaderParameter(HandoverParam, new Vector2(HandoverNearM, HandoverFarM));
        var field = new Field(clumps, material, clumps.Count > 0 ? clumps.Max(c => c.Width) : 0f);
        foreach (var chunk in clumps.GroupBy(c => (MathF.Floor(c.Base.X / ChunkM), MathF.Floor(c.Base.Z / ChunkM))))
            field.Root.AddChild(field.Draw(chunk.ToList(), Transform3D.Identity, string.Create(CultureInfo.InvariantCulture, $"chunk_{chunk.Key.Item1}_{chunk.Key.Item2}")));
        Handover(kinds, true);

        Log.Info("world",
            $"canopy clumps: {clumps.Count} over {field.Root.GetChildCount()} chunks from {cards.Count} card textures ({string.Join(", ", cards.Select(c => c.Texture))}), {stamps:0} card stamps at {meanTrees:0.##} trees a clump, tile {TileWidthM:0.#}x{tileHeight:0.#} m at card height {cardHeight:0.#} m, handover {HandoverNearM:0}-{HandoverFarM:0} m, {watch.ElapsedMilliseconds} ms");
        return field;
    }

    // The atlas, one tile per variant side by side, and its side map. Each tree is its card resized to the
    // tile's metres, mirrored, and blended back row first. Mipmapped, since clumps are seen far off.
    private static (Image Atlas, Image Side) Compose(List<Image> images, List<Vector2> sizes, List<float> weights, float tileHeight)
    {
        int tw = (int)(TileWidthM * PixelsPerM), th = (int)MathF.Ceiling(tileHeight * PixelsPerM);
        var atlas = Image.CreateEmpty(tw * Variants, th, false, Image.Format.Rgba8);
        var side = new byte[tw * Variants * th * 2];
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
                int x = (v * tw) + (int)((tree.X * PixelsPerM) - (pw / 2f));
                int y = th - (int)(tree.Base * PixelsPerM) - ph;
                atlas.BlendRect(src, new Rect2I(0, 0, pw, ph), new Vector2I(x, y));
                Side(side, tw * Variants, th, src.GetData(), pw, ph, x, y);
                src.Dispose();
            }
        }
        Bleed(atlas);
        atlas.GenerateMipmaps();
        var sideImage = Image.CreateFromData(tw * Variants, th, false, Image.Format.Rg8, side);
        sideImage.GenerateMipmaps();
        return (atlas, sideImage);
    }

    // Writes one tree's sun side into the side map over its solid texels, front rows over back ones. Red is
    // the card shader's sun-side share on the tree's right half, green on its left. Each is full a sixth of
    // the tree's width off its axis. Two channels, so a mip level averages each half's share, not their sum.
    private static void Side(byte[] side, int width, int height, byte[] px, int pw, int ph, int x0, int y0)
    {
        for (int j = 0; j < ph; j++)
        {
            for (int i = 0; i < pw; i++)
            {
                int x = x0 + i, y = y0 + j;
                if (x < 0 || y < 0 || x >= width || y >= height || px[(((j * pw) + i) * 4) + 3] < 128)
                    continue;
                float s = Math.Clamp((((i + 0.5f) / pw) - 0.5f) * 6f, -1f, 1f);
                int o = ((y * width) + x) * 2;
                side[o] = (byte)(255f * Math.Max(s, 0f));
                side[o + 1] = (byte)(255f * Math.Max(-s, 0f));
            }
        }
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

    // A symmetric draw in [-1, 1), the spread the Rich cards' tint takes.
    private static float S(int seed, int cell, int draw) => (2f * U(seed, cell, draw)) - 1f;

    // The Rich card's look on a clump: the same face, sun side, fog and tint lines. The fade is the card's
    // handover turned round, in past the band. The tile comes off the custom data's first channel, and each
    // tree's sun side off the side map, swapped for a mirrored clump.
    private static string ShaderCode() => $$"""
        shader_type spatial;
        render_mode skip_vertex_transform, unshaded, cull_disabled, shadows_disabled, blend_mix, depth_prepass_alpha;

        uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_disable;
        uniform sampler2D side_tex : filter_linear_mipmap, repeat_disable;
        uniform float variants = 1.0;
        uniform vec2 {{HandoverParam}};

        #include "res://shaders/csky_atmosphere.gdshaderinc"
        #include "res://shaders/csky_srgb.gdshaderinc"
        #include "res://shaders/csky_clutter_fade.gdshaderinc"
        #include "res://shaders/csky_mip_bias.gdshaderinc"
        #include "res://shaders/csky_instance_uniforms.gdshaderinc"

        varying flat float v_alpha;
        varying flat float v_mirror;
        varying flat float v_sun;

        void vertex() {
            vec3 origin = MODEL_MATRIX[3].xyz;
            v_alpha = {{ClutterBuilder.HandoverShare}};
            float keep = step(0.004, v_alpha);
        {{ClutterBuilder.FaceBasisLines(false)}}
            v_mirror = MODEL_MATRIX[0].x;
            vec3 local = vec3(VERTEX.x * MODEL_MATRIX[0].x, VERTEX.y * MODEL_MATRIX[1].y, 0.0);
        {{ClutterBuilder.SunLine}}    UV.x = (UV.x + INSTANCE_CUSTOM.x) / variants;
            VERTEX = (VIEW_MATRIX * vec4(origin + face * local * keep, 1.0)).xyz;
        }

        void fragment() {
            vec4 col = vec4(csky_srgb_to_linear(COLOR.rgb), COLOR.a) * {{SceneBuilder.SampleAlbedo("UV")}};
            ALBEDO = col.rgb;
            vec2 halves = v_mirror < 0.0 ? texture(side_tex, UV).gr : texture(side_tex, UV).rg;
            float v_side = (v_sun >= 0.0 ? halves.x : -halves.y) / 3.0;
        {{ClutterBuilder.RichFragmentLines}}{{ClutterBuilder.FogLines}}{{SceneBuilder.TintLine}}
            ALPHA = col.a * v_alpha;
        }
        """;

    /// <summary>One clump: its base on the ground, its size in metres, its atlas tile and mirror, and its
    /// instance colour.</summary>
    public readonly record struct Clump(Vector3 Base, float Width, float Height, int Variant, bool Mirror, Color Colour);

    /// <summary>One tree in a tile: its card, its centre and base in tile metres, its scale on the card, and
    /// whether it is mirrored.</summary>
    public readonly record struct Tree(int Card, float X, float Base, float Scale, bool Mirror);

    /// <summary>The built clumps on one material: the node drawing them over the map, and copies of a map
    /// area for the map edge (<see cref="MapEdgeExtender"/>).</summary>
    public sealed class Field
    {
        /// <summary>The name of a map-edge cell's clump node.</summary>
        public const string CopyName = "clumps";

        private static readonly QuadMesh Quad = new() { Size = Vector2.One, CenterOffset = new Vector3(0f, 0.5f, 0f) };

        private readonly List<Clump> _clumps;
        private readonly ShaderMaterial _material;
        private readonly float _margin;

        // The clumps standing in each source area a copy was asked for, kept since the edge asks again per cell.
        private readonly Dictionary<Rect2, List<Clump>> _areas = new();

        internal Field(List<Clump> clumps, ShaderMaterial material, float margin)
        {
            _clumps = clumps;
            _material = material;
            _margin = margin;
        }

        /// <summary>The node drawing the clumps over the map, one MultiMesh per chunk.</summary>
        public Node3D Root { get; } = new() { Name = "CanopyClumps" };

        /// <summary>Every clump over the map.</summary>
        public IReadOnlyList<Clump> Clumps => _clumps;

        /// <summary>The clumps standing in <paramref name="source"/>, drawn moved by <paramref name="mirror"/>;
        /// null where none stands.</summary>
        public MultiMeshInstance3D? Copy(Rect2 source, Transform3D mirror)
        {
            if (!_areas.TryGetValue(source, out var list))
                _areas[source] = list = _clumps.Where(c => source.HasPoint(new Vector2(c.Base.X, c.Base.Z))).ToList();
            return list.Count > 0 ? Draw(list, mirror, CopyName) : null;
        }

        internal MultiMeshInstance3D Draw(List<Clump> list, Transform3D moved, string name)
        {
            // ⚠ The colour and custom-data flags must be set before InstanceCount, or the buffer has no slot.
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                UseCustomData = true,
                Mesh = Quad,
                InstanceCount = list.Count,
            };
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                var basis = new Basis(new Vector3(c.Mirror ? -c.Width : c.Width, 0f, 0f), new Vector3(0f, c.Height, 0f), Vector3.Back);
                mm.SetInstanceTransform(i, new Transform3D(basis, moved * c.Base));
                mm.SetInstanceColor(i, c.Colour);
                mm.SetInstanceCustomData(i, new Color(c.Variant, 0f, 0f, 0f));
            }

            return new MultiMeshInstance3D
            {
                Multimesh = mm,
                MaterialOverride = _material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                ExtraCullMargin = _margin,
                Name = name,
            };
        }
    }
}
