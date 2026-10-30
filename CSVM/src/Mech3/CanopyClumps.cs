using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CSVM.Extraction;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Forest mass under Rich: impostor clumps, cards that each show a few trees, placed per
/// <see cref="CanopyMask"/> cell at the card forest's own trees per area. Past a handover band the
/// clumps carry the forest and the listed cards fade out. The far forest keeps the near one's density
/// and colour, and continues past the card fade and the map edge (<see cref="Field.Copy"/>).
/// The clump art is one atlas of <see cref="Variants"/> tiles. Each is composed at load from the listed card
/// textures or is a species' own <see cref="ClumpTile"/> (<see cref="Slots"/>).
/// <see cref="Place"/> and <see cref="Layout"/> are pure. Mech3.md has the rules.
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

    /// <summary>TUNE. The fade-in band over painted canopy, where no card stands to hand over from. Inside it
    /// the painted texture alone draws; over it the clumps rise out of the canopy to their full height.</summary>
    public const float PaintedNearM = 700f, PaintedFarM = 1300f;

    /// <summary>TUNE. How far the clumps over painted canopy rise above the painted treetops, in metres.</summary>
    public const float PaintedRiseM = 16f;

    /// <summary>TUNE. The crowns a fully painted cell carries: about 14 m crowns packed edge to edge.</summary>
    public const float PaintedTreesPerCell = 6f;

    /// <summary>TUNE. The near crown models' band over painted canopy: they alone draw inside
    /// <see cref="ModelNearM"/>, then dither over to the crowns by <see cref="ModelFarM"/>. Fixed, whatever the 3D
    /// Vegetation word. A thin band: over a wide one a dithered hole shows painted ground, not a crown.</summary>
    public const float ModelNearM = 950f, ModelFarM = 1000f;

    /// <summary>TUNE. How far a near crown model's top stands above the painted treetops, in metres at scale 1.</summary>
    public const float ModelRiseM = 6f;

    /// <summary>TUNE. While the crown models draw, how high a crown clump's tile top stands over the painted
    /// treetops at every distance, in metres. Its crowns then meet the models' height, and nothing rises or sinks.</summary>
    public const float HeldTopM = 13f;

    /// <summary>TUNE. The crown model's grade target over the crowns' mean colour. The grade meets the texture's
    /// mean, and at that the model drew darker on screen than the crowns.</summary>
    public const float ModelGain = 1.1f;

    /// <summary>The near crown model's name under the chapter's vegetation models.</summary>
    public const string CrownModel = "canopytree";

    /// <summary>The fixed seed of the placement and layout hash, so every launch draws the same clumps.</summary>
    public const int Seed = 0x5EED_C1;

    /// <summary>The shader parameter both the clumps and the listed cards' Rich material read the band from.</summary>
    public const string HandoverParam = "canopy_handover";

    /// <summary>The clumps' shader parameter holding them at a share of their height at every distance; zero rises them.</summary>
    public const string HeldParam = "held";

    /// <summary>Where the shipped clump tiles sit, beside the vegetation models: <c>&lt;chapter&gt;/&lt;card&gt;.clump.*</c>.</summary>
    public const string ShippedRoot = "res://data/vegetation_models";

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

    // A crown card over painted canopy: its width in metres and the silhouettes cut per canopy texture. One
    // repeat of the canopy texture spans CrownTilePx on it, 12.8 m.
    private const float CrownWidthM = 14f;
    private const int CrownShapes = 3, CrownTilePx = 128;

    // The clump instances are split into square chunks of this edge, so the frustum culls them.
    private const float ChunkM = 2048f;

    // Crown model slots along a mask cell's side. Each slot holds one crown at its share of PaintedTreesPerCell.
    private const int CrownSlots = 3;

    private static Shader? _shader;

    /// <summary>The crown models' band: alone inside its near edge, dithered over to the clumps out to its far one.</summary>
    public static Vector2 ModelBand => new(ModelNearM, ModelFarM);

    /// <summary>Whether a chapter draws clumps when <c>--canopy-clumps</c> does not say: C1 over its cards and
    /// C3 over its painted canopy. The other stamp-mask chapters were checked and left off.</summary>
    public static bool DefaultOn(string? chapter) =>
        string.Equals(chapter, "C1", StringComparison.OrdinalIgnoreCase) || string.Equals(chapter, "C3", StringComparison.OrdinalIgnoreCase);

    /// <summary>The clumps each cell of <paramref name="mask"/> holds on average: the kept card stamps standing in
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

    /// <summary>The clumps each cell of a painted <paramref name="mask"/> holds: its covered share times
    /// <paramref name="treesPerCell"/> over <paramref name="meanTrees"/>. A painted cell's density is the share
    /// of it the canopy texture covers, never held at a cap, so the stamp trap does not apply.</summary>
    public static float[] Rate(CanopyMask mask, float treesPerCell, float meanTrees) =>
        mask.Density.Select(d => d * treesPerCell / meanTrees).ToArray();

    /// <summary>The clumps over <paramref name="mask"/>. A covered cell holds its <paramref name="rate"/>
    /// rounded by a draw. Each sits at a jittered point in the cell, on the lowest ground under its width
    /// interpolated between covered cells. A clump's size is its tile scaled by the cell's top less its ground
    /// over <paramref name="cardHeight"/>. Painted canopy's top is its ground, so it passes a <paramref name="rise"/>.
    /// Its colour spreads as the Rich cards' does, by
    /// <paramref name="brightness"/> overall and <paramref name="tint"/> per channel.</summary>
    public static List<Clump> Place(CanopyMask mask, IReadOnlyList<float> rate, float cardHeight, float tileHeight,
        float brightness = 0f, float tint = 0f, int seed = Seed, float rise = 0f)
    {
        var clumps = new List<Clump>();
        for (int c = 0; c < mask.Density.Count; c++)
        {
            float cellRise = rise > 0f ? rise : mask.Top[c] - mask.Ground[c];
            if (mask.Density[c] <= 0f || rate[c] <= 0f || cellRise <= 0f || cardHeight <= 0f)
                continue;
            int n = (int)MathF.Floor(rate[c] + U(seed, c, 0));
            float s = cellRise / cardHeight;
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

    /// <summary>The near crown models over a painted <paramref name="mask"/>, in a 3 by 3 grid of slots a cell. A slot
    /// keeps one crown at its share of <paramref name="perCell"/> over the cell's painted share.
    /// A crown is jittered in its slot and stands on the painted surface there, never off it. Scale, mirror and
    /// colour spread as the clumps' do. The draws are a hash of the cell, so a cell's crowns move with it alone.</summary>
    public static List<NearCrown> CrownModels(CanopyMask mask, float perCell, float brightness = 0f, float tint = 0f, int seed = Seed)
    {
        const int slots = CrownSlots * CrownSlots;
        var crowns = new List<NearCrown>();
        for (int c = 0; c < mask.Density.Count; c++)
        {
            float share = mask.PaintedShare(c);
            if (mask.Density[c] <= 0f || share <= 0f)
                continue;
            float keep = mask.Density[c] / share * perCell / slots;
            for (int s = 0; s < slots; s++)
            {
                // Draws past the clumps' own, so a crown and a clump in one cell are not drawn alike.
                int draw = 1000 + (s * 10);
                if (U(seed, c, draw) >= keep)
                    continue;
                float x = mask.MinX + (((c % mask.Width) + (((s % CrownSlots) + 0.15f + (0.7f * U(seed, c, draw + 1))) / CrownSlots)) * CanopyMask.CellM);
                float z = mask.MinZ + (((c / mask.Width) + (((s / CrownSlots) + 0.15f + (0.7f * U(seed, c, draw + 2))) / CrownSlots)) * CanopyMask.CellM);
                if (!mask.IsPainted(x, z))
                    continue;
                float b = 1f + (brightness * S(seed, c, draw + 4));
                var colour = new Color(b * (1f + (tint * S(seed, c, draw + 5))), b * (1f + (tint * S(seed, c, draw + 6))),
                    b * (1f + (tint * S(seed, c, draw + 7))));
                crowns.Add(new NearCrown(new Vector3(x, mask.SurfaceAt(x, z), z), 0.85f + (0.3f * U(seed, c, draw + 3)), U(seed, c, draw + 8) < 0.5f, colour));
            }
        }

        return crowns;
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

    /// <summary>The mean tree count over the atlas <paramref name="slots"/> (<see cref="Slots"/>): a composed
    /// slot's <see cref="Layout"/> of <paramref name="weights"/>, a tiled one its own tile's count in
    /// <paramref name="tileTrees"/>. A replacement tile then moves no forest's density.</summary>
    public static float MeanTrees(IReadOnlyList<int> slots, IReadOnlyList<IReadOnlyList<int>?> tileTrees, IReadOnlyList<float> weights,
        IReadOnlyList<Vector2> sizes) =>
        (float)Enumerable.Range(0, Variants).Average(v => slots[v] < 0 ? Layout(v, weights, sizes).Count
            : tileTrees[slots[v]]![Rank(slots, v) % tileTrees[slots[v]]!.Count]);

    /// <summary>What each atlas slot shows: -1 for a tile composed from the cards, else the index of a species in
    /// <paramref name="weights"/> whose own tile fills it. Each <paramref name="tiled"/> species takes its stamp
    /// share of the slots, and the untiled ones together the rest, by largest remainder. Composed slots come
    /// first, so with no tile every slot composes as before. A tiled species too rare for a slot is composed.</summary>
    public static int[] Slots(IReadOnlyList<float> weights, IReadOnlyList<bool> tiled)
    {
        float total = weights.Sum();
        var groups = new List<(int Id, float Quota)> { (-1, Enumerable.Range(0, weights.Count).Where(i => !tiled[i]).Sum(i => weights[i])) };
        groups.AddRange(Enumerable.Range(0, weights.Count).Where(i => tiled[i]).Select(i => (i, weights[i])));
        groups = groups.Select(g => (g.Id, g.Quota * Variants / total)).ToList();
        var count = groups.Select(g => (int)MathF.Floor(g.Quota)).ToArray();
        foreach (int g in Enumerable.Range(0, groups.Count).OrderByDescending(g => groups[g].Quota - count[g]).Take(Variants - count.Sum()))
            count[g]++;
        return Enumerable.Range(0, groups.Count).SelectMany(g => Enumerable.Repeat(groups[g].Id, count[g])).ToArray();
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

    /// <summary>Hands the listed cards over to the clumps, or back. Their Rich material fades them out over the
    /// band while <paramref name="on"/>, and draws them at every distance otherwise. An added kind that is not
    /// forest keeps every distance, since no clump carries it.</summary>
    public static void Handover(IReadOnlyList<ClutterBuilder.KindExport>? kinds, bool on)
    {
        foreach (var kind in kinds ?? Array.Empty<ClutterBuilder.KindExport>())
        {
            if (kind.Mass)
                (kind.Rich?.Material as ShaderMaterial)?.SetShaderParameter(HandoverParam, on ? new Vector2(HandoverNearM, HandoverFarM) : Vector2.Zero);
        }
    }

    /// <summary>The tile a species' clumps take on <paramref name="chapter"/>: the model pack's, else the one
    /// shipped beside its model under <see cref="ShippedRoot"/>. Null composes them from the cards.</summary>
    public static ClumpTile? TileFor(string chapter, string card) => ClumpTile.Resolve(chapter, card, HdModels.Current.Clump, Shipped);

    /// <summary>Draws the clumps over <paramref name="mask"/>, their atlas composed from the listed card kinds in
    /// <paramref name="kinds"/> where <paramref name="tileFor"/> gives a species no tile, their colour spread
    /// from <paramref name="entry"/>. Hands the cards over to them. Null when no listed card resolves.</summary>
    public static Field? Build(CanopyMask mask, IReadOnlyList<ClutterBuilder.KindExport> kinds, VegetationEntry entry,
        TextureArchive textures, Func<string, ClumpTile?> tileFor, string? chapter = null)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        bool painted = entry.Picks.Any(p => CanopyMask.IsCanopy(entry, p.Texture));
        // A set the remake adds counts only as forest (KindExport.Mass): scattered bushes raise no clumps.
        var cards = painted ? Crowns(entry, textures) : kinds.Where(k => k.Rich != null && !k.Solid && k.Height > 0f && k.Mass)
            .Select(k => (Kind: k, Kept: k.Rich!.Placements.Count(p => p.Basis.Y.LengthSquared() > 0f)))
            .Where(k => k.Kept > 0)
            .GroupBy(k => k.Kind.Texture, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Texture: g.Key, Size: new Vector2(g.First().Kind.Width, g.First().Kind.Height), Weight: (float)g.Sum(k => k.Kept),
                Look: entry.Find(g.First().Kind.Model), Image: textures.FindImage(g.Key), Species: HdModels.NodeKey(g.First().Kind.Model)))
            .Where(c => c.Image != null)
            .ToList();
        if (cards.Count == 0)
            return null;
        var sizes = cards.Select(c => c.Size).ToList();
        float stamps = cards.Sum(c => c.Weight);
        var species = cards.Select(c => c.Species).Distinct().ToList();
        var strips = species.Select(s => Decode(tileFor(s), s)).ToList();
        var slots = Slots(species.Select(s => cards.Where(c => c.Species == s).Sum(c => c.Weight)).ToList(), strips.Select(t => t != null).ToList());
        var weights = cards.Select(c => slots.Contains(species.IndexOf(c.Species)) ? 0f : c.Weight).ToList();
        var used = slots.Where(s => s >= 0).Distinct().Select(s => strips[s]!).ToList();
        float tileHeight = used.Select(t => t.HeightM).Append(TileHeight(sizes)).Max();
        float cardHeight = cards.Sum(c => c.Weight * c.Size.Y) / stamps;
        float meanTrees = MeanTrees(slots, strips.Select(t => t?.Tile.Trees).ToList(), weights, sizes);
        var rate = painted ? Rate(mask, PaintedTreesPerCell, meanTrees) : Rate(mask, kinds.Where(k => k.Rich != null && !k.Solid && k.Height > 0f && k.Mass)
            .SelectMany(k => k.Rich!.Placements.Where(p => p.Basis.Y.LengthSquared() > 0f).Select(p => p.Origin)), meanTrees);
        float brightness = cards.Sum(c => c.Weight * (c.Look?.Brightness ?? 0f)) / stamps;
        float tint = cards.Sum(c => c.Weight * (c.Look?.Tint ?? 0f)) / stamps;
        // A crown model is graded per channel to the crowns it hands over to, which are cut from the painted canopy.
        var crownModel = painted && chapter != null
            ? VegetationModels.ForMean(chapter, CrownModel, Mean(cards.Select(c => c.Image!)) * ModelGain, float.PositiveInfinity)
            : null;
        var (atlas, side) = Compose(cards.Select(c => c.Image!).ToList(), sizes, weights, tileHeight, slots, strips);
        var tiles = used.Select(t => $"{species[strips.IndexOf(t)]} in {slots.Count(s => s >= 0 && strips[s] == t)} slots from {t.Tile.Source}").ToList();
        var clumps = Place(mask, rate, cardHeight, tileHeight, brightness, tint, rise: painted ? PaintedRiseM : 0f);
        var band = Band(painted);

        var material = new ShaderMaterial { Shader = _shader ??= new Shader { Code = ShaderCode() } };
        material.SetShaderParameter("albedo_tex", TextureUpload.Create(atlas));
        material.SetShaderParameter("side_tex", TextureUpload.Create(side));
        material.SetShaderParameter("variants", (float)Variants);
        material.SetShaderParameter(HandoverParam, band);
        material.SetShaderParameter("grow", painted ? 1f : 0f);
        var field = new Field(clumps, material, clumps.Count > 0 ? clumps.Max(c => c.Width) : 0f) { Tiles = tiles, TreesPerClump = meanTrees };
        foreach (var chunk in clumps.GroupBy(c => (MathF.Floor(c.Base.X / ChunkM), MathF.Floor(c.Base.Z / ChunkM))))
            field.Root.AddChild(field.Draw(chunk.ToList(), Transform3D.Identity, string.Create(CultureInfo.InvariantCulture, $"chunk_{chunk.Key.Item1}_{chunk.Key.Item2}")));
        int chunks = field.Root.GetChildCount();
        if (crownModel != null)
            field.Stand(CrownModels(mask, PaintedTreesPerCell, brightness, tint), crownModel);

        // Painted canopy takes over from no card, so the listed cards (C3's beach palms) keep every distance.
        if (!painted)
            Handover(kinds, true);

        Log.Info("world",
            $"canopy clumps: {clumps.Count} over {chunks} chunks from {cards.Count} card textures ({string.Join(", ", cards.Select(c => c.Texture).Distinct())}), {(painted ? "painted canopy" : $"{stamps:0} card stamps")} at {meanTrees:0.##} trees a clump, tile {TileWidthM:0.#}x{tileHeight:0.#} m at card height {cardHeight:0.#} m, {(painted ? "fade-in" : "handover")} {band.X:0}-{band.Y:0} m, tiles: {(tiles.Count == 0 ? "all composed" : string.Join("; ", tiles))}, {watch.ElapsedMilliseconds} ms");
        return field;
    }

    /// <summary>The clumps' fade-in band: the cards' handover band, or over painted canopy its own.</summary>
    public static Vector2 Band(bool painted) =>
        painted ? new Vector2(PaintedNearM, PaintedFarM) : new Vector2(HandoverNearM, HandoverFarM);

    // Gives every clear texel the mean colour of the solid ones. A filter, a resize or a mip level then
    // blends tree colour into an edge. The clear texels' black would darken the clumps far off. A near
    // model's texture takes it too (VegetationModels).
    internal static void Bleed(Image image)
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

    // The mean linear colour of the images' solid texels in their top half, the near model grade's target.
    private static Vector3? Mean(IEnumerable<Image> images)
    {
        var means = images.Select(i => VegetationModels.MeanLinear(i.GetData(), i.GetWidth(), i.GetHeight(), VegetationModels.CrownShare))
            .OfType<Vector3>().ToList();
        return means.Count == 0 ? null : means.Aggregate((a, b) => a + b) / means.Count;
    }

    // Crown cards cut from the painted canopy's own textures, CrownShapes silhouettes each. The volume then
    // wears the colour and grain the ground shows from above. Equal weights; the spread is the listed kinds' mean.
    // A tile replaces a texture's crowns under its name (C3/forest1).
    private static List<(string Texture, Vector2 Size, float Weight, VegetationKind? Look, Image? Image, string Species)> Crowns(
        VegetationEntry entry, TextureArchive textures)
    {
        var look = entry.Kinds.Count == 0 ? null
            : new VegetationKind("", Tint: entry.Kinds.Average(k => k.Tint), Brightness: entry.Kinds.Average(k => k.Brightness));
        var size = new Vector2(CrownWidthM, PaintedRiseM);
        var cards = new List<(string, Vector2, float, VegetationKind?, Image?, string)>();
        foreach (var pick in entry.Picks.Where(p => CanopyMask.IsCanopy(entry, p.Texture)))
        {
            if (textures.FindImage(pick.Texture) is not { } image)
                continue;
            image.Decompress();
            image.Convert(Image.Format.Rgba8);
            for (int k = 0; k < CrownShapes; k++)
                cards.Add((pick.Texture, size, 1f, look, Crown(image, k), System.IO.Path.GetFileNameWithoutExtension(pick.Texture).ToLowerInvariant()));
            image.Dispose();
        }

        return cards;
    }

    // One crown card: a dome of overlapping discs over a body to the base, textured with the canopy image
    // tiled CrownTilePx to a repeat. Each disc darkens to its rim and the card darkens toward its base, so the
    // bumps read as relief rather than a flat cut-out.
    private static Image Crown(Image canopy, int shape)
    {
        int w = (int)(CrownWidthM * PixelsPerM), h = (int)(PaintedRiseM * PixelsPerM);
        int cell = -100 - shape;
        var discs = new List<(float X, float Y, float R)> { (w / 2f, (w * 0.5f) + 8f, w * 0.36f) };
        for (int d = 0; d < 7; d++)
        {
            float r = w * (0.11f + (0.11f * U(Seed, cell, (d * 4) + 1)));
            float x = r + ((w - (2f * r)) * U(Seed, cell, (d * 4) + 2));
            discs.Add((x, r + (w * 0.35f * U(Seed, cell, (d * 4) + 3)), r));
        }
        const int tile = CrownTilePx;
        var src = (Image)canopy.Duplicate();
        src.Resize(tile, tile, Image.Interpolation.Bilinear);
        byte[] tex = src.GetData();
        src.Dispose();
        int ox = (int)(U(Seed, cell, 20) * tile), oy = (int)(U(Seed, cell, 21) * tile);
        var px = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float rim = -1f;
                foreach (var (dx, dy, r) in discs)
                {
                    float q = (((x - dx) * (x - dx)) + ((y - dy) * (y - dy))) / (r * r);
                    if (q < 1f)
                        rim = Math.Max(rim, MathF.Sqrt(1f - q));
                }
                bool body = y >= discs[0].Y && MathF.Abs(x - (w / 2f)) < w * 0.45f;
                if (rim < 0f && !body)
                    continue;
                float shade = (0.8f + (0.2f * Math.Max(rim, 0.3f))) * (0.82f - (0.3f * y / h));
                int t = (((((y + oy) % tile) * tile) + ((x + ox) % tile)) * 4);
                int o = ((y * w) + x) * 4;
                for (int ch = 0; ch < 3; ch++)
                    px[o + ch] = (byte)(tex[t + ch] * shade);
                px[o + 3] = 255;
            }
        }

        return Image.CreateFromData(w, h, false, Image.Format.Rgba8, px);
    }

    // The atlas, one tile per variant side by side, and its side map. Each tree is its card resized to the
    // tile's metres, mirrored, and blended back row first. A tiled slot takes its species' tile instead, at its
    // metres, centred on the slot's floor. Mipmapped, since clumps are seen far off.
    private static (Image Atlas, Image Side) Compose(List<Image> images, List<Vector2> sizes, List<float> weights, float tileHeight,
        int[] slots, List<Strip?> strips)
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
            if (slots[v] >= 0)
            {
                var strip = strips[slots[v]]!;
                var column = new Rect2I(Rank(slots, v) % strip.Tile.Trees.Count * strip.ColumnPx, 0, strip.ColumnPx, strip.Image.GetHeight());
                int pw = Math.Min(tw, (int)(strip.Tile.WidthM * PixelsPerM)), ph = Math.Min(th, (int)(strip.HeightM * PixelsPerM));
                var src = strip.Image.GetRegion(column);
                src.Resize(pw, ph, Image.Interpolation.Lanczos);
                var map = strip.Side?.GetRegion(column);
                map?.Resize(pw, ph, Image.Interpolation.Bilinear);
                int x = (v * tw) + ((tw - pw) / 2);
                atlas.BlendRect(src, new Rect2I(0, 0, pw, ph), new Vector2I(x, th - ph));
                Side(side, tw * Variants, th, src.GetData(), pw, ph, x, th - ph, map?.GetData());
                src.Dispose();
                map?.Dispose();
                continue;
            }
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
    // A tile's own side map (RGBA, the size of px) gives its texels' shares instead; without one, it is one tree.
    private static void Side(byte[] side, int width, int height, byte[] px, int pw, int ph, int x0, int y0, byte[]? map = null)
    {
        for (int j = 0; j < ph; j++)
        {
            for (int i = 0; i < pw; i++)
            {
                int x = x0 + i, y = y0 + j;
                if (x < 0 || y < 0 || x >= width || y >= height || px[(((j * pw) + i) * 4) + 3] < 128)
                    continue;
                float s = Math.Clamp((((i + 0.5f) / pw) - 0.5f) * 6f, -1f, 1f);
                int o = ((y * width) + x) * 2, t = ((j * pw) + i) * 4;
                side[o] = map != null ? map[t] : (byte)(255f * Math.Max(s, 0f));
                side[o + 1] = map != null ? map[t + 1] : (byte)(255f * Math.Max(-s, 0f));
            }
        }
    }

    // A species' tile decoded for the atlas, or null with a warning, so that species composes from its cards.
    private static Strip? Decode(ClumpTile? tile, string species)
    {
        if (tile == null)
            return null;
        var image = new Image();
        var side = tile.Side != null ? new Image() : null;
        string? fault = image.LoadPngFromBuffer(tile.Image) != Error.Ok ? "the image is not a readable PNG"
            : image.GetWidth() % tile.Trees.Count != 0 ? $"its {image.GetWidth()} px are no {tile.Trees.Count} equal tiles"
            : side != null && side.LoadPngFromBuffer(tile.Side) != Error.Ok ? "the side map is not a readable PNG"
            : side != null && side.GetSize() != image.GetSize() ? "the side map's size differs from the image's"
            : null;
        if (fault != null)
        {
            Log.Warn("world", $"canopy clumps: the {species} tile from {tile.Source} composes from the cards, {fault}");
            return null;
        }
        image.Convert(Image.Format.Rgba8);
        Bleed(image);
        side?.Convert(Image.Format.Rgba8);
        return new Strip(tile, image, side, image.GetWidth() / tile.Trees.Count);
    }

    // The shipped tile files: an image as <name>.png.bin, which Godot does not import, as the cloud puffs ship.
    private static (byte[] Bytes, string Source)? Shipped(string relative)
    {
        string path = $"{ShippedRoot}/{relative}{(relative.EndsWith(".png", StringComparison.Ordinal) ? ".bin" : "")}";
        return FileAccess.FileExists(path) ? (FileAccess.GetFileAsBytes(path), path) : null;
    }

    // Which of its species' tiles a slot shows: the slots of that species before it.
    private static int Rank(IReadOnlyList<int> slots, int slot) => slots.Take(slot).Count(s => s == slots[slot]);

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
    // handover turned round, in past the band. Over painted canopy (grow) a clump sinks below the canopy by its
    // unfaded share instead: a part-clear clump shows as a ghost on the skyline. While the crown models draw it stands
    // at its held share. The tile comes off the custom data's first channel, and each tree's sun side off the side map,
    // swapped for a mirrored clump. Over the models' band a clump keeps the dither cells they give up.
    private static string ShaderCode() => $$"""
        shader_type spatial;
        render_mode skip_vertex_transform, unshaded, cull_disabled, shadows_disabled, blend_mix, depth_prepass_alpha;

        uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_disable;
        uniform sampler2D side_tex : filter_linear_mipmap, repeat_disable;
        uniform float variants = 1.0;
        uniform float grow = 0.0;
        uniform float {{HeldParam}} = 0.0;
        uniform vec2 {{HandoverParam}};
        uniform vec2 {{VegetationModels.BandParam}};

        #include "res://shaders/csky_atmosphere.gdshaderinc"
        #include "res://shaders/csky_srgb.gdshaderinc"
        #include "res://shaders/csky_clutter_fade.gdshaderinc"
        #include "res://shaders/csky_mip_bias.gdshaderinc"
        #include "res://shaders/csky_instance_uniforms.gdshaderinc"

        varying flat float v_alpha;
        varying flat float v_mirror;
        varying flat float v_sun;
        varying flat float v_model;

        void vertex() {
            vec3 origin = MODEL_MATRIX[3].xyz;
            v_model = {{ClutterBuilder.ModelShare}};
            v_alpha = {{ClutterBuilder.HandoverShare}};
            // A spyglass disc draws no crown model, so its clumps rise as with no models.
            float hold = {{SceneBuilder.FlatSeaEye}} ? 0.0 : {{HeldParam}};
            float stand = hold > 0.0 ? hold : v_alpha;
            float keep = step(0.004, mix(v_alpha, hold > 0.0 ? v_model : v_alpha, grow));
            origin.y -= MODEL_MATRIX[1].y * (1.0 - stand) * grow;
            v_alpha = mix(v_alpha, 1.0, grow);
        {{ClutterBuilder.FaceBasisLines(false)}}
            v_mirror = MODEL_MATRIX[0].x;
            vec3 local = vec3(VERTEX.x * MODEL_MATRIX[0].x, VERTEX.y * MODEL_MATRIX[1].y, 0.0);
        {{ClutterBuilder.SunLine}}    UV.x = (UV.x + INSTANCE_CUSTOM.x) / variants;
            VERTEX = (VIEW_MATRIX * vec4(origin + face * local * keep, 1.0)).xyz;
        }

        void fragment() {
            if ({{VegetationModels.BandParam}}.y > 0.0 && !csky_clutter_dither_keep(FRAGCOORD.xy, v_model)) {
                discard;
            }
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

    /// <summary>One near crown model over painted canopy: where it stands on the painted surface, its scale, its
    /// mirror and its instance colour.</summary>
    public readonly record struct NearCrown(Vector3 At, float Scale, bool Mirror, Color Colour);

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

        // ponytail: no crown model stands past the map edge, whose canopy inside 700 m reads flat as before.
        // The map edge's copies therefore keep no band: nothing stands there to hand over to.
        private readonly ShaderMaterial _edgeMaterial;

        // The clumps standing in each source area a copy was asked for, kept since the edge asks again per cell.
        private readonly Dictionary<Rect2, List<Clump>> _areas = new();

        internal Field(List<Clump> clumps, ShaderMaterial material, float margin)
        {
            _clumps = clumps;
            _material = material;
            _margin = margin;
            _edgeMaterial = (ShaderMaterial)material.Duplicate();
        }

        /// <summary>The node drawing the clumps over the map, one MultiMesh per chunk.</summary>
        public Node3D Root { get; } = new() { Name = "CanopyClumps" };

        /// <summary>Every clump over the map.</summary>
        public IReadOnlyList<Clump> Clumps => _clumps;

        /// <summary>The near crown models over painted canopy under <see cref="Root"/>, one MultiMesh per
        /// <see cref="VegetationModels.CellM"/> cell; null where the chapter has none.</summary>
        public Node3D? Models { get; private set; }

        /// <summary>Every near crown model over the map.</summary>
        public IReadOnlyList<NearCrown> Crowns { get; private set; } = Array.Empty<NearCrown>();

        /// <summary>Each species tile in the atlas: its species, its slots and where it was read from.</summary>
        public IReadOnlyList<string> Tiles { get; init; } = Array.Empty<string>();

        /// <summary>The trees a clump carries on average, the rate's divisor.</summary>
        public float TreesPerClump { get; init; }

        /// <summary>The clumps standing in <paramref name="source"/>, drawn moved by <paramref name="mirror"/>;
        /// null where none stands.</summary>
        public MultiMeshInstance3D? Copy(Rect2 source, Transform3D mirror)
        {
            if (!_areas.TryGetValue(source, out var list))
                _areas[source] = list = _clumps.Where(c => source.HasPoint(new Vector2(c.Base.X, c.Base.Z))).ToList();
            return list.Count > 0 ? Draw(list, mirror, CopyName, _edgeMaterial) : null;
        }

        /// <summary>The crown models and the clumps' band follow the 3D Vegetation word, with no rebuild. Off hides
        /// the models, and the clumps rise over their own band as with no models. Near and Far both draw over
        /// <see cref="ModelBand"/>, the clumps held at <see cref="HeldTopM"/>.</summary>
        public void FollowModels()
        {
            if (Models == null)
                return;
            bool on = VegetationModels.On;
            Models.Visible = on;
            _material.SetShaderParameter(VegetationModels.BandParam, on ? ModelBand : Vector2.Zero);
            _material.SetShaderParameter(HeldParam, on && _clumps.Count > 0 ? Math.Min(HeldTopM / _clumps.Max(c => c.Height), 1f) : 0f);
        }

        // Stands the crown models in cells of VegetationModels.CellM, each ranged at the band's far edge past its
        // bounds. A model's top stands ModelRiseM above the painted surface, at its scale. Not solid.
        internal void Stand(List<NearCrown> crowns, VegetationModels.Model model)
        {
            var box = model.Mesh.GetAabb();
            float sink = box.End.Y - ModelRiseM;
            var material = VegetationModels.Material(model, lit: false, fogged: true, box.Size.X, canopy: true);
            material.SetShaderParameter(VegetationModels.BandParam, ModelBand);
            Crowns = crowns;
            Models = new Node3D { Name = "canopy_models" };
            foreach (var cell in crowns.GroupBy(c => (MathF.Floor(c.At.X / VegetationModels.CellM), MathF.Floor(c.At.Z / VegetationModels.CellM))))
            {
                var list = cell.ToList();
                var bounds = new Aabb(list[0].At, Vector3.Zero);
                foreach (var c in list)
                    bounds = bounds.Expand(c.At);
                var offset = bounds.GetCenter();
                var mm = new MultiMesh
                {
                    TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                    UseColors = true,
                    Mesh = model.Mesh,
                    InstanceCount = list.Count,
                };
                for (int i = 0; i < list.Count; i++)
                {
                    var c = list[i];
                    var basis = new Basis(new Vector3(c.Mirror ? -c.Scale : c.Scale, 0f, 0f), new Vector3(0f, c.Scale, 0f), new Vector3(0f, 0f, c.Scale));
                    mm.SetInstanceTransform(i, new Transform3D(basis, c.At - new Vector3(0f, sink * c.Scale, 0f) - offset));
                    mm.SetInstanceColor(i, c.Colour);
                }
                Models.AddChild(new MultiMeshInstance3D
                {
                    Name = string.Create(CultureInfo.InvariantCulture, $"crowns_{cell.Key.Item1}_{cell.Key.Item2}"),
                    Multimesh = mm,
                    MaterialOverride = material,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    ExtraCullMargin = box.Size.X,
                    Position = offset,
                    VisibilityRangeEnd = ModelFarM + (bounds.Size * 0.5f).Length() + box.Size.X,
                    Layers = VegetationModels.Layer,
                });
            }
            Root.AddChild(Models);
            FollowModels();
            Log.Info("world", $"canopy models: {crowns.Count} crowns over {Models.GetChildCount()} cells from {model.Source}, top {ModelRiseM:0.#} m over the painted canopy, alone to {ModelNearM:0} m, dithered over to the crowns to {ModelFarM:0} m, the crowns held at a {HeldTopM:0.#} m top");
        }

        internal MultiMeshInstance3D Draw(List<Clump> list, Transform3D moved, string name, ShaderMaterial? material = null)
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
                MaterialOverride = material ?? _material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                ExtraCullMargin = _margin,
                Name = name,
            };
        }
    }

    // A decoded tile strip: its tiles side by side, ColumnPx wide each.
    private sealed record Strip(ClumpTile Tile, Image Image, Image? Side, int ColumnPx)
    {
        public float HeightM => Image.GetHeight() * Tile.WidthM / ColumnPx;
    }
}
