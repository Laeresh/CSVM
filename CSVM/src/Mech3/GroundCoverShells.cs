using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CSVM.Utils;
using Godot;
using Cover = CSVM.Mech3.GroundCover.Cover;

namespace CSVM.Mech3;

/// <summary>
/// Procedural ground cover near the camera, the Ground Cover word's <c>shells</c> (Rich only). Stacked
/// alpha-tested shells over the sidecar's <c>cover</c> textures draw tufts and crop rows. The shells are the
/// picked world triangles cut on the brush mask's texel grid, so a cleared texel (the landing strips) grows none.
/// Each 256 m chunk of a texture is one MultiMesh drawing its mesh once per layer. A blade takes the ground's
/// own texture and vertex colour under the world's light, so it reads as the ground it grows from.
/// Mech3.md has the rules.
/// </summary>
public static class GroundCoverShells
{
    /// <summary>Shells per piece of ground, bottom to top.</summary>
    public const int Layers = 8;

    /// <summary>The edge of the square chunks the camera culls by.</summary>
    public const float ChunkM = 256f;

    /// <summary>The node name the session's shells stand under.</summary>
    public const string NodeName = "GroundCoverShells";

    /// <summary>The row field's cells along each side of a crop texture.</summary>
    public const int RowGrid = 32;

    /// <summary>The cut's cell edge where the chapter has no brush mask, the C1 mask's own texel.</summary>
    public const float UnmaskedCellM = 12f;

    private static readonly Dictionary<(Cover, bool), Shader> Shaders = new();

    /// <summary>The band of <paramref name="cover"/>, near and far in metres, the cards' (<see cref="GroundCover.Band"/>).
    /// Inside near it draws whole, and it dithers out by far, eye to fragment, so it ends with altitude too.</summary>
    public static Vector2 Band(Cover cover) => GroundCover.Band(cover);

    /// <summary>The share of the cover's height shell <paramref name="layer"/> stands at; the top one at full.</summary>
    public static float LayerShare(int layer) => (layer + 1f) / Layers;

    /// <summary>The pieces of triangle <paramref name="a"/>, <paramref name="b"/>, <paramref name="c"/> (X and Z)
    /// inside each square cell of a grid at <paramref name="origin"/> that <paramref name="keep"/> takes. A
    /// piece's corners are barycentric weights on the triangle's corners, so any attribute interpolates.</summary>
    public static List<Piece> Clip(Vector2 a, Vector2 b, Vector2 c, Vector2 origin, float cell, Func<int, int, bool> keep)
    {
        var pieces = new List<Piece>();
        int i0 = (int)MathF.Floor((Math.Min(a.X, Math.Min(b.X, c.X)) - origin.X) / cell);
        int i1 = (int)MathF.Floor((Math.Max(a.X, Math.Max(b.X, c.X)) - origin.X) / cell);
        int j0 = (int)MathF.Floor((Math.Min(a.Y, Math.Min(b.Y, c.Y)) - origin.Y) / cell);
        int j1 = (int)MathF.Floor((Math.Max(a.Y, Math.Max(b.Y, c.Y)) - origin.Y) / cell);
        for (int j = j0; j <= j1; j++)
        {
            for (int i = i0; i <= i1; i++)
            {
                if (!keep(i, j))
                    continue;
                var poly = new List<(Vector2 P, Vector3 W)> { (a, Vector3.Right), (b, Vector3.Up), (c, Vector3.Back) };
                float x0 = origin.X + (i * cell), z0 = origin.Y + (j * cell);
                poly = Cut(poly, p => p.X - x0);
                poly = Cut(poly, p => -(p.X - (x0 + cell)));
                poly = Cut(poly, p => p.Y - z0);
                poly = Cut(poly, p => -(p.Y - (z0 + cell)));
                if (Solid(poly))
                    pieces.Add(new Piece(i, j, poly.Select(v => v.W).ToArray()));
            }
        }

        return pieces;
    }

    /// <summary>The convex parts of a piece of triangle <paramref name="a"/>, <paramref name="b"/>, <paramref name="c"/>
    /// (its corners as barycentric weights) that no overlay lies over. Each of <paramref name="overlays"/> (three corners
    /// each) is cut out grown by <see cref="VegetationGround.OverlayReachM"/> across. It is cut only where it lies in the
    /// cards' height window over the piece (<see cref="VegetationGround.Overlaid"/>). A rail keeps a half-metre margin.</summary>
    public static List<Vector3[]> KeepOff(Vector3[] weights, Vector3 a, Vector3 b, Vector3 c, IReadOnlyList<Vector3> overlays)
    {
        Vector3 At(Vector3 w) => (a * w.X) + (b * w.Y) + (c * w.Z);
        var parts = new List<List<(Vector2 P, Vector3 W)>> { weights.Select(w => (Flat(At(w)), w)).ToList() };
        const float reach = VegetationGround.OverlayReachM;
        var whole = parts[0];
        Vector2 lo = parts[0].Aggregate(parts[0][0].P, (m, v) => m.Min(v.P)), hi = parts[0].Aggregate(parts[0][0].P, (m, v) => m.Max(v.P));
        for (int t = 0; t + 2 < overlays.Count && parts.Count > 0; t += 3)
        {
            Vector3 o0 = overlays[t], o1 = overlays[t + 1], o2 = overlays[t + 2];
            Vector2 p0 = Flat(o0), p1 = Flat(o1), p2 = Flat(o2);
            Vector2 olo = p0.Min(p1).Min(p2), ohi = p0.Max(p1).Max(p2);
            float area = (p1 - p0).Cross(p2 - p0);
            if (MathF.Abs(area) < 1e-4f || olo.X - reach > hi.X || ohi.X + reach < lo.X || olo.Y - reach > hi.Y || ohi.Y + reach < lo.Y)
                continue;
            float sign = MathF.Sign(area);
            // The grown triangle, positive inside: its edges pushed out by the reach, and its box grown by the reach.
            // The box blunts a sliver's sharp corners, which on a rail's long thin triangle reach metres past its end.
            Func<Vector2, float> Edge(Vector2 from, Vector2 along) => p => (sign * along.Cross(p - from) / along.Length()) + reach;
            var inside = new[]
            {
                Edge(p0, p1 - p0), Edge(p1, p2 - p1), Edge(p2, p0 - p2),
                p => p.X - (olo.X - reach), p => (ohi.X + reach) - p.X, p => p.Y - (olo.Y - reach), p => (ohi.Y + reach) - p.Y,
            };
            // The overlay misses the piece when all its corners lie outside one side, the common case, cut for nothing.
            if (inside.Any(side => whole.TrueForAll(v => side(v.P) <= 0f)))
                continue;
            var next = new List<List<(Vector2 P, Vector3 W)>>();
            foreach (var part in parts)
            {
                var under = part;
                foreach (var side in inside)
                    under = under.Count >= 3 ? Cut(under, side) : under;
                if (!Solid(under) || !Over(under))
                {
                    next.Add(part);
                    continue;
                }
                // The part outside the grown triangle, as convex pieces: outside the first side, then inside it and
                // outside the second, and so on.
                var rest = part;
                foreach (var side in inside)
                {
                    if (rest.Count < 3)
                        break;
                    if (Cut(rest, p => -side(p)) is var outside && Solid(outside))
                        next.Add(outside);
                    rest = Cut(rest, side);
                }
            }
            parts = next;

            // Whether the overlay lies over the ground at the cut part's centre, in the cards' height window.
            bool Over(List<(Vector2 P, Vector3 W)> under)
            {
                var ground = At(under.Aggregate(Vector3.Zero, (s, v) => s + v.W) / under.Count);
                var n = (o1 - o0).Cross(o2 - o0);
                if (MathF.Abs(n.Y) < 1e-6f)
                    return false;
                float y = o0.Y - (((n.X * (ground.X - o0.X)) + (n.Z * (ground.Z - o0.Z))) / n.Y);
                return y > ground.Y - VegetationGround.OverlaySinkM && y < ground.Y + VegetationGround.OverlayRiseM;
            }
        }

        return parts.Select(p => p.Select(v => v.W).ToArray()).ToList();
    }

    /// <summary>The crop rows' direction field over an RGBA8 texture, one vector per cell of a <paramref name="grid"/>
    /// square. Each is the luminance structure tensor over the cell and its eight neighbours, wrapping as the texture
    /// repeats. It reads ((Jxx - Jyy), 2 Jxy) over (Jxx + Jyy). Its angle is twice the painted stripes' gradient angle and its
    /// length their coherence, so a flat or mixed field_patch reads near zero. Rows run across the gradient.</summary>
    public static Vector2[] RowField(byte[] rgba, int width, int height, int grid)
    {
        var lum = new float[width * height];
        for (int i = 0; i < lum.Length; i++)
            lum[i] = (0.2126f * rgba[i * 4]) + (0.7152f * rgba[(i * 4) + 1]) + (0.0722f * rgba[(i * 4) + 2]);
        var sums = new (double Xx, double Yy, double Xy)[grid * grid];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float gx = lum[(y * width) + ((x + 1) % width)] - lum[(y * width) + ((x + width - 1) % width)];
                float gy = lum[(((y + 1) % height) * width) + x] - lum[(((y + height - 1) % height) * width) + x];
                ref var s = ref sums[(y * grid / height * grid) + (x * grid / width)];
                s = (s.Xx + (gx * gx), s.Yy + (gy * gy), s.Xy + (gx * gy));
            }
        }
        var field = new Vector2[grid * grid];
        for (int cy = 0; cy < grid; cy++)
        {
            for (int cx = 0; cx < grid; cx++)
            {
                double xx = 0, yy = 0, xy = 0;
                for (int k = 0; k < 9; k++)
                {
                    var s = sums[(((cy + (k / 3) - 1 + grid) % grid) * grid) + ((cx + (k % 3) - 1 + grid) % grid)];
                    xx += s.Xx;
                    yy += s.Yy;
                    xy += s.Xy;
                }
                double sum = xx + yy;
                field[(cy * grid) + cx] = sum > 1e-6 ? new Vector2((float)((xx - yy) / sum), (float)(2 * xy / sum)) : Vector2.Zero;
            }
        }

        return field;
    }

    /// <summary>The shells over the ground <paramref name="entry"/>'s <c>cover</c> picks on <paramref name="gamez"/>, a
    /// cards-only pick left out, none where <paramref name="mask"/> is bare. Null when nothing is picked or left. With no mask
    /// the cut runs on an <see cref="UnmaskedCellM"/> grid and keeps every piece.</summary>
    public static Node3D? Build(GameZ gamez, VegetationMask? mask, TextureArchive textures, VegetationEntry entry, string worldName = "world1")
    {
        // The shells' own picks: a cards-only one neither grows shells nor counts as their ground, so it changes nothing here.
        entry = new VegetationEntry { Cover = entry.Cover.Where(c => !c.CardsOnly).ToList() };
        if (entry.Cover.Count == 0)
            return null;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var triangles = VegetationGround.PaintedSurface(gamez, t => entry.CoverFor(t) != null, worldName);
        if (triangles.Count == 0)
            return null;
        byte[]? texels = mask?.ToBytes();
        int side = mask?.Side ?? 0;
        float cell = mask != null ? (mask.MaxX - mask.MinX) / side : UnmaskedCellM;
        var origin = mask != null ? new Vector2(mask.MinX, mask.MinZ) : Vector2.Zero;
        bool Keep(int i, int j) => texels == null || i < 0 || j < 0 || i >= side || j >= side || texels[(j * side) + i] > 0;
        var overlays = GroundCover.OverlaysFor(gamez, entry, worldName);

        var chunks = new Dictionary<(string Texture, bool Lit, int Ci, int Cj), Chunk>();
        var metres = new Dictionary<string, List<float>>(StringComparer.OrdinalIgnoreCase);
        var area = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        int pieces = 0;
        foreach (var t in triangles)
        {
            Vector3 a = t.A.At, b = t.B.At, c = t.C.At;
            float uvArea = Math.Abs((t.B.Uv - t.A.Uv).Cross(t.C.Uv - t.A.Uv));
            if (uvArea > 1e-9f)
            {
                if (!metres.TryGetValue(t.Texture, out var list))
                    metres[t.Texture] = list = new List<float>();
                list.Add(MathF.Sqrt((b - a).Cross(c - a).Length() / uvArea));
            }
            var cut = Clip(Flat(a), Flat(b), Flat(c), origin, cell, Keep);

            // The overlays binned around one piece, from the overlays' own 64 m bins.
            List<Vector3> NearPiece(Vector3[] weights)
            {
                var at = weights.Select(w => Flat((a * w.X) + (b * w.Y) + (c * w.Z))).ToList();
                var grow = Vector2.One * VegetationGround.OverlayReachM;
                return overlays.Near(at.Aggregate((m, v) => m.Min(v)) - grow, at.Aggregate((m, v) => m.Max(v)) + grow);
            }
            foreach (var weights in cut.SelectMany(piece => KeepOff(piece.Weights, a, b, c, NearPiece(piece.Weights))))
            {
                var corners = weights.Select(w => (At: (a * w.X) + (b * w.Y) + (c * w.Z), W: w)).ToList();
                var centre = corners.Aggregate(Vector3.Zero, (s, v) => s + v.At) / corners.Count;
                var key = (t.Texture, t.Lit, (int)MathF.Floor(centre.X / ChunkM), (int)MathF.Floor(centre.Z / ChunkM));
                if (!chunks.TryGetValue(key, out var chunk))
                    chunks[key] = chunk = new Chunk(new Vector3((key.Item3 + 0.5f) * ChunkM, centre.Y, (key.Item4 + 0.5f) * ChunkM));
                chunk.Add(corners.Select(v => (v.At, Lerp(t, v.W))).ToList(), centre);
                area[t.Texture] = area.GetValueOrDefault(t.Texture) + PolygonArea(corners.Select(v => new Vector2(v.At.X, v.At.Z)).ToList());
                pieces++;
            }
        }
        if (chunks.Count == 0)
            return null;

        var materials = new Dictionary<(string, bool), ShaderMaterial>();
        var root = new Node3D { Name = NodeName };
        foreach (var ((tex, lit, ci, cj), chunk) in chunks.OrderBy(k => k.Key.Texture, StringComparer.Ordinal).ThenBy(k => k.Key.Cj).ThenBy(k => k.Key.Ci))
        {
            if (!materials.TryGetValue((tex, lit), out var material))
            {
                var pick = entry.CoverFor(tex)!;
                float mpu = Median(metres.GetValueOrDefault(tex)) is > 0f and var found ? found : ChunkM;
                materials[(tex, lit)] = material = Material(pick.Grows, lit, textures.Find(tex), mpu, pick.Grows == Cover.Crop ? RowTexture(textures, tex) : null);
                if (GateTexture(textures, tex, pick) is { } gate)
                {
                    material.SetShaderParameter("gate_tex", gate);
                    material.SetShaderParameter("gated", true);
                }
            }
            root.AddChild(chunk.Draw(material, Band(entry.CoverFor(tex)!.Grows).Y, string.Create(CultureInfo.InvariantCulture, $"{System.IO.Path.GetFileNameWithoutExtension(tex)}_{ci}_{cj}")));
        }
        string areas = string.Join(", ", area.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => string.Create(CultureInfo.InvariantCulture,
            $"{k.Key} {k.Value / 1e6:0.00} km2 at {Median(metres.GetValueOrDefault(k.Key)):0.#} m/uv")));
        int unlit = triangles.Count(t => !t.Lit);
        Log.Info("world", $"ground cover shells: {pieces} pieces in {chunks.Count} chunks, {Layers} layers, bands crops {Band(Cover.Crop).X:0}-{Band(Cover.Crop).Y:0} m and grass {Band(Cover.Grass).X:0}-{Band(Cover.Grass).Y:0} m, {areas}, {unlit} unlit triangles, {watch.ElapsedMilliseconds} ms");
        return root;
    }

    private static float Median(List<float>? values) => values is { Count: > 0 } ? values.OrderBy(v => v).ElementAt(values.Count / 2) : 0f;

    // Keeps the side of a clip polygon where `side` is at least zero, cutting the edges that cross it.
    private static List<(Vector2 P, Vector3 W)> Cut(List<(Vector2 P, Vector3 W)> poly, Func<Vector2, float> side)
    {
        var kept = new List<(Vector2 P, Vector3 W)>();
        for (int k = 0; k < poly.Count; k++)
        {
            var (p, q) = (poly[k], poly[(k + 1) % poly.Count]);
            float dp = side(p.P), dq = side(q.P);
            if (dp >= 0f)
                kept.Add(p);
            if ((dp >= 0f) != (dq >= 0f))
            {
                float s = dp / (dp - dq);
                kept.Add((p.P.Lerp(q.P, s), p.W.Lerp(q.W, s)));
            }
        }

        return kept;
    }

    private static bool Solid(List<(Vector2 P, Vector3 W)> poly) => poly.Count >= 3 && PolygonArea(poly.Select(v => v.P).ToList()) > 1e-4f;

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    // Summed about the first corner. Summed about the origin, world coordinates in the thousands round a small part's
    // area away, and an overlay's sliver read as no overlap.
    private static float PolygonArea(List<Vector2> poly)
    {
        float sum = 0f;
        for (int k = 1; k + 1 < poly.Count; k++)
            sum += (poly[k] - poly[0]).Cross(poly[k + 1] - poly[0]);
        return Math.Abs(sum) / 2f;
    }

    // A piece corner's UV, colour and normal, interpolated on its triangle.
    private static (Vector2 Uv, Color Color, Vector3 Normal) Lerp(SurfaceTriangle t, Vector3 w) =>
        ((t.A.Uv * w.X) + (t.B.Uv * w.Y) + (t.C.Uv * w.Z),
            new Color((t.A.Color.R * w.X) + (t.B.Color.R * w.Y) + (t.C.Color.R * w.Z), (t.A.Color.G * w.X) + (t.B.Color.G * w.Y) + (t.C.Color.G * w.Z),
                (t.A.Color.B * w.X) + (t.B.Color.B * w.Y) + (t.C.Color.B * w.Z)),
            ((t.A.Normal * w.X) + (t.B.Normal * w.Y) + (t.C.Normal * w.Z)).Normalized());

    // The crop texture's row field as an RG8 texture, read from the live texture at load; nothing is kept on disk.
    private static ImageTexture? RowTexture(TextureArchive textures, string tex)
    {
        if (textures.FindImage(tex, authored: true) is not { } image)
            return null;
        image.Decompress();
        image.Convert(Image.Format.Rgba8);
        var field = RowField(image.GetData(), image.GetWidth(), image.GetHeight(), RowGrid);
        image.Dispose();
        var bytes = new byte[field.Length * 2];
        for (int i = 0; i < field.Length; i++)
        {
            bytes[i * 2] = (byte)Mathf.RoundToInt(Math.Clamp((field[i].X * 0.5f) + 0.5f, 0f, 1f) * 255f);
            bytes[(i * 2) + 1] = (byte)Mathf.RoundToInt(Math.Clamp((field[i].Y * 0.5f) + 0.5f, 0f, 1f) * 255f);
        }
        return TextureUpload.Create(RowGrid, RowGrid, Image.Format.Rg8, bytes);
    }

    // The pick's gate (GroundCover.Surface.GateMap) as an R8 texture of the authored image's 2 x 2 blocks, read unfiltered.
    // Grey rock and pale paths stay bare whatever texture pack draws the ground. Null for a pick with no gate.
    private static ImageTexture? GateTexture(TextureArchive textures, string tex, CoverPick pick)
    {
        if ((pick.MinSaturation <= 0f && pick.MaxValue >= 1f) || textures.FindImage(tex, authored: true) is not { } image)
            return null;
        image.Convert(Image.Format.Rgba8);
        var grows = GroundCover.Surface.GateMap(image.GetData(), image.GetWidth(), image.GetHeight(), pick.MinSaturation, pick.MaxValue);
        int w = Math.Max(1, image.GetWidth() / GroundCover.BlurTexels), h = Math.Max(1, image.GetHeight() / GroundCover.BlurTexels);
        image.Dispose();
        return TextureUpload.Create(w, h, Image.Format.R8, grows.Select(g => g ? (byte)255 : (byte)0).ToArray());
    }

    private static ShaderMaterial Material(Cover cover, bool lit, Texture2D? ground, float metresPerUv, Texture2D? rows)
    {
        if (!Shaders.TryGetValue((cover, lit), out var shader))
            Shaders[(cover, lit)] = shader = new Shader { Code = ShaderCode(cover, lit) };
        var material = new ShaderMaterial { Shader = shader };
        if (ground != null)
            material.SetShaderParameter("albedo_tex", ground);
        material.SetShaderParameter("metres_per_uv", metresPerUv);
        if (rows != null)
            material.SetShaderParameter("row_tex", rows);
        return material;
    }

    private static string F(float v) => v.ToString("0.0#####", CultureInfo.InvariantCulture);

    // The shell shader. Each layer lifts its pieces by its share of the cover's height; a piece past the band
    // collapses in the vertex stage. A fragment keeps the tuft or row it lies in, judged in the texture's own metres
    // so the pattern moves with the painted ground. Past a few pixels a cell, it dithers at that layer's mean cover
    // instead, which does not shimmer. The blade is the ground's texel under the world's light, darker at its foot.
    // ⚠ Never give it an instance uniform: every chunk would take 16 slots of Godot's global buffer.
    private static string ShaderCode(Cover cover, bool lit)
    {
        bool crop = cover == Cover.Crop;
        float height = crop ? 0.9f : 0.5f;
        // The pattern's cell: a tuft's spacing, or the row spacing.
        float cell = crop ? 0.75f : 0.45f;
        var band = Band(cover);
        string pattern = crop ? """
                vec2 g = texture(row_tex, UV).rg * 2.0 - 1.0;
                // The painted fields run along the texture's axes: snap to the nearer one, then let the rows wander.
                vec2 across = g.x < -0.15 && abs(g.x) > abs(g.y) ? vec2(0.0, 1.0) : vec2(1.0, 0.0);
                float s_across = dot(p, across);
                float s_along = dot(p, vec2(-across.y, across.x));
                // Patches of a few metres thin and lower the crop, so neither the near rows nor the far cover is even.
                float field_patch = vnoise(p / 7.0);
                float a = s_across / CELL + (vnoise(p / 23.0 + 17.0) - 0.5) * 1.6;
                float row = floor(a);
                vec4 rr = cover_hash(vec2(row, 913.0));
                a += (vnoise(vec2(s_along / 3.0 + rr.w * 50.0, row)) - 0.5) * 0.3;
                float b = s_along / PLANT + rr.y * 7.0;
                vec4 r = cover_hash(vec2(floor(a), floor(b)));
                float density = mix(0.6, 1.0, field_patch);
                float tall = mix(0.55, 1.0, r.x) * mix(0.7, 1.0, field_patch);
                float off = 0.5 + (rr.x - 0.5) * 0.3 + (r.z - 0.5) * 0.25;
                float strand = cover_hash(floor(vec2(a * 7.0, b * 2.0)) + 7.0).y;
                float half_w = (0.08 + 0.3 * (1.0 - pow(h / tall, 2.0))) * mix(0.7, 1.35, r.y) * mix(0.5, 1.0, strand);
                hit = r.w < density && h <= tall && abs(fract(a) - off) < half_w;
                cover = density * (1.0 - clamp((h - 0.85 * mix(0.7, 1.0, field_patch)) / 0.3, 0.0, 1.0)) * 2.0 * (0.08 + 0.3 * (1.0 - h * h)) * 0.8;
                shade = mix(0.68, 1.26, h) * (0.9 + 0.2 * r.z) * mix(0.93, 1.05, field_patch);
            """ : """
                vec2 g = p / CELL;
                vec2 nearest = floor(g + 0.5);
                hit = false;
                shade = 1.0;
                for (int k = 0; k < 4 && !hit; k++) {
                    vec2 c = nearest - vec2(float(k & 1), float(k >> 1));
                    vec4 r = cover_hash(c);
                    float tall = mix(0.5, 1.0, r.x);
                    vec2 off = g - (c + 0.5 + (r.yz - 0.5) * 0.5);
                    float s = h / tall;
                    float blades = 0.5 + 0.5 * sin(atan(off.y, off.x) * 9.0 + r.w * 6.2832);
                    hit = s <= 1.0 && length(off) < 0.3 + 0.3 * s && blades > 0.85 * pow(s, 1.5);
                    shade = mix(0.53, 1.27, h) * (0.85 + 0.3 * r.w);
                }
                float pt = clamp((1.0 - h) / 0.5, 0.0, 1.0);
                float sm = h / 0.75;
                cover = clamp(pt * 3.1416 * pow(0.3 + 0.3 * sm, 2.0) * (1.0 - 0.85 * pow(min(sm, 1.0), 1.5)), 0.0, 1.0);
            """;
        return $$"""
            shader_type spatial;
            render_mode cull_disabled, shadows_disabled{{(lit ? "" : ", unshaded")}};

            uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
            {{(crop ? "uniform sampler2D row_tex : filter_linear, repeat_enable;" : "")}}
            uniform float metres_per_uv = 256.0;
            uniform sampler2D gate_tex : filter_nearest, repeat_enable;
            uniform bool gated = false;

            #include "res://shaders/csky_atmosphere.gdshaderinc"
            #include "res://shaders/csky_srgb.gdshaderinc"
            #include "res://shaders/csky_clutter_fade.gdshaderinc"
            #include "res://shaders/csky_time.gdshaderinc"

            const float HEIGHT = {{F(height)}};
            const float CELL = {{F(cell)}};
            const float NEAR_M = {{F(band.X)}};
            const float FAR_M = {{F(band.Y)}};
            const float PLANT = 0.2;
            const float DETAIL_LO = {{(crop ? "4.0" : "2.0")}};
            const float DETAIL_SPAN = {{(crop ? "6.0" : "3.0")}};
            const float LAYERS = {{Layers}}.0;

            varying flat float v_h;
            varying vec3 v_world;
            varying vec3 v_normal;

            vec4 cover_hash(vec2 c) {
                uvec2 q = uvec2(ivec2(c) + ivec2(1 << 20));
                uint h = (q.x * 0x9E3779B1u) ^ (q.y * 0x85EBCA77u);
                h ^= h >> 16u;
                h *= 0x7FEB352Du;
                h ^= h >> 15u;
                h *= 0x846CA68Bu;
                h ^= h >> 16u;
                return vec4(uvec4(h, h >> 8u, h >> 16u, h >> 24u) & uvec4(255u)) / 255.0;
            }

            float vnoise(vec2 x) {
                vec2 i = floor(x);
                vec2 f = fract(x);
                f = f * f * (3.0 - 2.0 * f);
                return mix(mix(cover_hash(i).x, cover_hash(i + vec2(1.0, 0.0)).x, f.x),
                    mix(cover_hash(i + vec2(0.0, 1.0)).x, cover_hash(i + vec2(1.0, 1.0)).x, f.x), f.y);
            }

            void vertex() {
                v_h = INSTANCE_CUSTOM.x;
                // A piece's corners share its centre (UV2), so a piece past the band collapses whole.
                vec3 centre = (MODEL_MATRIX * vec4(UV2.x, VERTEX.y, UV2.y, 1.0)).xyz;
                float keep = step(distance(centre, CAMERA_POSITION_WORLD), FAR_M + 18.0);
                VERTEX.y += v_h * HEIGHT;
                VERTEX *= keep;
                v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
                v_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
            }

            void fragment() {
                float h = v_h;
                vec2 p = UV * metres_per_uv;
                p += vec2(sin(csky_time * 1.9 + dot(p, vec2(0.21, 0.17))), cos(csky_time * 1.4 + dot(p, vec2(-0.13, 0.23)))) * (0.05 * h * h);
                float px = max(length(dFdx(p)), length(dFdy(p)));
                float d = distance(v_world, CAMERA_POSITION_WORLD);
                vec2 fc = FRAGCOORD.xy + vec2(h * LAYERS * 3.0, h * LAYERS * 5.0);
                bool hit;
                float cover;
                float shade;
            {{pattern}}
                float detail = clamp((CELL / max(px, 1e-5) - DETAIL_LO) / DETAIL_SPAN, 0.0, 1.0);
                bool keep = csky_clutter_dither_keep(fc.yx, detail) ? hit : csky_clutter_dither_keep(fc, cover);
                vec3 ground = texture(albedo_tex, UV, 1.5).rgb;
                // The pick's gate, the cards' own map of the authored texels: grey rock and pale paths grow none.
                bool grows = !gated || texture(gate_tex, UV).r > 0.5;
                if (!keep || !grows || !csky_clutter_dither_keep(FRAGCOORD.xy, 1.0 - smoothstep(NEAR_M, FAR_M, d))) {
                    discard;
                }
                ALBEDO = csky_srgb_to_linear(COLOR.rgb) * ground * shade;
                float fog_amt = csky_fog_amount(v_world, CAMERA_POSITION_WORLD);
            {{(lit ? """
                NORMAL = normalize((VIEW_MATRIX * vec4(v_normal, 0.0)).xyz);
                ROUGHNESS = 1.0;
                METALLIC = 0.0;
                SPECULAR = 0.0;
                FOG = vec4(csky_fog_color_at(CAMERA_POSITION_WORLD), fog_amt);
            """ : """
                ALBEDO = mix(ALBEDO, csky_fog_color_at(CAMERA_POSITION_WORLD), fog_amt);
            """)}}
            }
            """;
    }

    /// <summary>One piece of a triangle inside grid cell (<paramref name="I"/>, <paramref name="J"/>), its corners
    /// as barycentric weights.</summary>
    public readonly record struct Piece(int I, int J, Vector3[] Weights);

    // One chunk's pieces, relative to its centre, each a fan carrying its own centre in UV2.
    private sealed class Chunk
    {
        private readonly Vector3 _at;
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<Color> _colors = new();
        private readonly List<Vector2> _uvs = new();
        private readonly List<Vector2> _centres = new();
        private readonly List<int> _indices = new();
        private Vector3 _lo = new(float.MaxValue, float.MaxValue, float.MaxValue), _hi = new(float.MinValue, float.MinValue, float.MinValue);

        public Chunk(Vector3 at)
        {
            _at = at;
        }

        public void Add(List<(Vector3 At, (Vector2 Uv, Color Color, Vector3 Normal) A)> corners, Vector3 centre)
        {
            int first = _vertices.Count;
            foreach (var (at, attr) in corners)
            {
                var local = at - _at;
                _vertices.Add(local);
                _normals.Add(attr.Normal);
                _colors.Add(attr.Color);
                _uvs.Add(attr.Uv);
                _centres.Add(new Vector2(centre.X - _at.X, centre.Z - _at.Z));
                _lo = _lo.Min(local);
                _hi = _hi.Max(local);
            }
            for (int k = 1; k + 1 < corners.Count; k++)
                _indices.AddRange(new[] { first, first + k, first + k + 1 });
        }

        public MultiMeshInstance3D Draw(ShaderMaterial material, float farM, string name)
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = _vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = _normals.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = _colors.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = _uvs.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV2] = _centres.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = _indices.ToArray();
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
            var box = new Aabb(_lo, (_hi - _lo) + new Vector3(0f, 1f, 0f));
            // Godot ranges a node by its distance to the eye, so the chunk stands at its ground and reaches its farthest corner.
            float reach = new Vector3(Math.Max(-_lo.X, _hi.X), Math.Max(-_lo.Y, _hi.Y) + 1f, Math.Max(-_lo.Z, _hi.Z)).Length();
            return new MultiMeshInstance3D
            {
                Name = name,
                Position = _at,
                Multimesh = layers,
                CustomAabb = box,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                VisibilityRangeEnd = farM + reach,
            };
        }
    }
}
