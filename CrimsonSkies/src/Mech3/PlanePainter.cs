using System;
using System.Collections.Generic;
using Godot;

namespace CrimsonSkies.Mech3;

/// <summary>
/// Applies a <see cref="PaintScheme"/> to one aircraft: recolours its shipped skin
/// textures and swaps the three decal placeholders for the chosen numbered decals.
/// Built per plane instance (two players flying the same aircraft in different liveries
/// each get their own painter and their own painted textures), and it never mutates the
/// shared <see cref="TextureArchive"/> cache.
///
/// WHY A RECOLOUR AT ALL: the shipped skins are not finished paint. They are shading maps
/// carrying paint-region keys — the Bloodhawk's is desaturated blue-gray where the original
/// flies a red one — and the engine writes the scheme's colours into them at load time.
/// See <c>docs/formats/paint.md</c>.
///
/// HOW REGIONS ARE IDENTIFIED — and how this differs from the original. The original engine
/// knows which texels belong to which paint slot from an engine-side table that exists in no
/// extracted file (it is in no zrdr and no plaintext string in the exe; see paint.md "Open").
/// We substitute <see cref="Regions"/>: a hand-authored per-aircraft table of hue windows,
/// measured off the shipped skins and checked against the reference screenshots. Each window
/// is one paint slot; saturated texels inside it are recoloured, desaturated texels
/// (unpainted structure — cowl metal, canopy frames, panel lines, baked shadow) are left
/// alone. Within a window the texel's VALUE is preserved as position along the paint colour's
/// ramp, which is what keeps the baked shading intact.
/// </summary>
public sealed class PlanePainter
{
    /// <summary>One paint slot's key: the hue window in the shipped skin that this slot
    /// owns, as a centre and half-width in degrees.</summary>
    private readonly record struct Region(float Hue, float HalfWidth);

    // Per-aircraft paint regions, keyed by the aircraft's skin-texture prefix (blo_wing,
    // kes_fuselage, …). Order IS the paint slot order: [0] = colour 1 = the body.
    //
    // Measured 2026-07-20 over every skin of each aircraft (saturated-texel hue histogram,
    // C1 texture.zbd — the skins are byte-identical in all eight chapters, verified, so the
    // table is chapter-independent). Windows are wide enough to swallow each region's own
    // hue spread (the Firebrand's purple runs 230–270°) without touching its neighbour.
    //
    // The Fury is deliberately absent: its skins are a near-black greyscale shading map with
    // NO saturated texels at all (median value 0.00 on fur_wing/fur_fusalage1), so there is
    // no key to recolour and it flies in its shipped black. That is a real gap against the
    // original — which paints a Fury blue for `secfury` — and is recorded in paint.md.
    private static readonly Dictionary<string, Region[]> Regions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["blo"] = new[] { new Region(217f, 22f), new Region(58f, 16f) },   // blue body, olive swoosh
        ["kes"] = new[] { new Region(199f, 24f), new Region(58f, 16f) },   // blue body, olive flap
        ["pea"] = new[] { new Region(219f, 22f), new Region(55f, 16f) },
        ["bri"] = new[] { new Region(200f, 22f) },
        ["war"] = new[] { new Region(31f, 18f) },                          // orange body
        ["bal"] = new[] { new Region(35f, 18f) },
        ["hel"] = new[] { new Region(250f, 26f), new Region(48f, 16f) },   // purple body, amber trim
        ["fir"] = new[] { new Region(252f, 28f) },                         // purple body (wide spread)
        ["dev"] = new[] { new Region(1f, 20f) },                           // red body
        ["agyro"] = new[] { new Region(58f, 20f) },                        // olive body
    };

    // A texel must be at least this saturated to read as paint rather than structure.
    // Membership fades in across [SatLo, SatHi] so antialiased region borders cross over
    // smoothly — a hard threshold here speckles every edge (verified during development).
    private const float SatLo = 0.12f;
    private const float SatHi = 0.33f;

    // Hue membership fades from full at HueInner to nothing at the window edge.
    private const float HueFalloff = 8f;

    // The region's highlight level: the value that maps to the scheme colour exactly.
    // Taken as a high percentile rather than the max so a handful of specular texels don't
    // drag the whole region dark. Texels above it push toward white (the paint's sheen).
    private const float RefPercentile = 0.92f;
    private const float HighlightGain = 1.4f;

    /// <summary>The three decal slots' texture suffixes, in paint-UI order: nose, tail, wing.</summary>
    private static readonly string[] DecalSuffixes = { "_noselogo", "_taillogo", "_winglogo" };

    private readonly TextureArchive _textures;
    private readonly PaintScheme _scheme;
    private readonly Region[] _regions;
    private readonly string _prefix;
    // baseName -> the substitute texture (painted skin or swapped decal); null = leave as is.
    private readonly Dictionary<string, ImageTexture?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public PaintScheme Scheme => _scheme;

    /// <param name="skinPrefix">The aircraft's skin-texture prefix without the underscore
    /// ("blo", "kes", …). <see cref="PrefixFor"/> derives it from the model's own materials.</param>
    public PlanePainter(TextureArchive textures, PaintScheme scheme, string skinPrefix)
    {
        _textures = textures;
        _scheme = scheme;
        _prefix = skinPrefix;
        _regions = Regions.TryGetValue(skinPrefix, out var r) ? r : Array.Empty<Region>();
    }

    /// <summary>True when this aircraft has no known paint regions, so only its decals can
    /// change (the Fury — see the <see cref="Regions"/> note).</summary>
    public bool SkinIsUnkeyed => _regions.Length == 0;

    /// <summary>The aircraft's skin-texture prefix, read off the model's own material names
    /// (every skin of one aircraft shares it: blo_wing, blo_fin, blo_noselogo…). Data-driven
    /// rather than a node-name table, so it survives node renames. Null if nothing matches.</summary>
    public static string? PrefixFor(GameZ gamez, GameZNode root)
    {
        // The decal placeholders are the reliable marker: an aircraft's skins are the only
        // textures named <prefix>_noselogo / _taillogo / _winglogo. Any of the three will
        // do — the Firebrand ships no fir_noselogo (paint.md said all eleven had one; it is
        // ten), so keying on the nose slot alone left it unpainted.
        string? found = null;
        void Walk(GameZNode n)
        {
            if (found != null)
                return;
            if (n.MeshIndex >= 0 && n.MeshIndex < gamez.Meshes.Count && gamez.Meshes[n.MeshIndex] is { } mesh)
                foreach (var poly in mesh.Polygons)
                {
                    if (poly.MaterialIndex < 0 || poly.MaterialIndex >= gamez.Materials.Count)
                        continue;
                    var tex = gamez.Materials[poly.MaterialIndex].TextureName;
                    if (tex == null)
                        continue;
                    foreach (var marker in DecalSuffixes)
                    {
                        int cut = tex.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                        if (cut > 0)
                        {
                            found = tex[..cut];
                            return;
                        }
                    }
                }
            foreach (int c in n.Children)
                Walk(gamez.Nodes[c]);
        }
        Walk(root);
        return found;
    }

    /// <summary>The texture to use in place of <paramref name="original"/>, or the original
    /// itself when this scheme does not touch it. Called for every material the plane's
    /// <see cref="SceneBuilder"/> resolves.</summary>
    public ImageTexture? Substitute(string materialTextureName, ImageTexture? original)
    {
        var baseName = System.IO.Path.GetFileNameWithoutExtension(materialTextureName);
        if (_cache.TryGetValue(baseName, out var cached))
            return cached ?? original;

        // A decal slot never falls through to the recolour: its 16x16 placeholder starts with
        // the same <prefix>_ and would otherwise be pointlessly repainted when the scheme
        // names no decal for that slot (index -1, or an index the archive lacks).
        int decalSlot = Array.FindIndex(DecalSuffixes,
            s => baseName.EndsWith(s, StringComparison.OrdinalIgnoreCase));
        ImageTexture? made = decalSlot >= 0 ? DecalFor(decalSlot) : PaintedSkin(baseName);
        _cache[baseName] = made;
        return made ?? original;
    }

    // <prefix>_noselogo / _taillogo / _winglogo are 16x16 placeholders, never artwork: the
    // engine swaps the chosen 64x64 decal onto these slots at load. Both are alpha=Full, so
    // the substitution does not disturb SceneBuilder's alpha classification.
    private ImageTexture? DecalFor(int slot)
    {
        int index = slot switch { 0 => _scheme.NoseDecal, 1 => _scheme.TailDecal, _ => _scheme.WingDecal };
        var texName = _textures.FindByDecalIndex(index);
        if (texName == null)
            return null;
        var img = _textures.FindImage(texName);
        if (img == null)
            return null;
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    }

    // Recolours one skin texture. Returns null when the texture is not a skin of this
    // aircraft, or carries no texel of any of its paint regions (the plane's greyscale
    // structure textures — spinners, shadow maps — come back untouched).
    private ImageTexture? PaintedSkin(string baseName)
    {
        if (_regions.Length == 0)
            return null;
        if (!baseName.StartsWith(_prefix + "_", StringComparison.OrdinalIgnoreCase))
            return null;

        var img = _textures.FindImage(baseName);
        if (img == null)
            return null;
        if (img.GetFormat() != Image.Format.Rgba8)
            img.Convert(Image.Format.Rgba8);

        var data = img.GetData();
        int px = data.Length / 4;
        if (px == 0)
            return null;

        // Pass 1: decode to HSV once, then per-region membership weight + the region's
        // reference (highlight) value.
        var hues = new float[px];
        var sats = new float[px];
        var values = new float[px];
        for (int i = 0; i < px; i++)
            RgbToHsv(data[i * 4] / 255f, data[i * 4 + 1] / 255f, data[i * 4 + 2] / 255f,
                out hues[i], out sats[i], out values[i]);

        var weight = new float[_regions.Length][];
        var refValue = new float[_regions.Length];
        var sample = new List<float>();
        for (int r = 0; r < _regions.Length; r++)
        {
            weight[r] = new float[px];
            sample.Clear();
            for (int i = 0; i < px; i++)
            {
                float dist = Mathf.Abs(Mathf.Wrap(hues[i] - _regions[r].Hue, -180f, 180f));
                float w = SmoothStep(_regions[r].HalfWidth, _regions[r].HalfWidth - HueFalloff, dist)
                          * SmoothStep(SatLo, SatHi, sats[i]);
                weight[r][i] = w;
                if (w > 0.5f)
                    sample.Add(values[i]);
            }
            refValue[r] = Percentile(sample, RefPercentile);
        }

        // Pass 2: blend each region's paint over the source by its membership weight.
        // Earlier slots win where windows overlap, so a texel is never painted twice.
        bool touched = false;
        for (int i = 0; i < px; i++)
        {
            float remaining = 1f;
            float outR = data[i * 4] / 255f, outG = data[i * 4 + 1] / 255f, outB = data[i * 4 + 2] / 255f;
            for (int r = 0; r < _regions.Length && remaining > 0.001f; r++)
            {
                float w = Mathf.Min(weight[r][i], remaining);
                if (w <= 0.001f || refValue[r] <= 0.001f)
                    continue;
                remaining -= w;
                touched = true;

                var c = _scheme.ColorFor(r);
                // The texel's value relative to the region's highlight is its position on
                // the paint ramp: below it the colour scales toward black (preserving the
                // baked shading exactly), above it the colour lifts toward white.
                float t = Mathf.Min(values[i] / refValue[r], 1.6f);
                float k = Mathf.Min(t, 1f);
                float over = Mathf.Max(t - 1f, 0f) * HighlightGain;
                float pr = Mathf.Clamp(c.R * k + (1f - c.R * k) * over, 0f, 1f);
                float pg = Mathf.Clamp(c.G * k + (1f - c.G * k) * over, 0f, 1f);
                float pb = Mathf.Clamp(c.B * k + (1f - c.B * k) * over, 0f, 1f);
                outR = Mathf.Lerp(outR, pr, w);
                outG = Mathf.Lerp(outG, pg, w);
                outB = Mathf.Lerp(outB, pb, w);
            }
            data[i * 4] = (byte)Mathf.RoundToInt(outR * 255f);
            data[i * 4 + 1] = (byte)Mathf.RoundToInt(outG * 255f);
            data[i * 4 + 2] = (byte)Mathf.RoundToInt(outB * 255f);
        }
        if (!touched)
            return null;

        var painted = Image.CreateFromData(img.GetWidth(), img.GetHeight(), false, Image.Format.Rgba8, data);
        painted.GenerateMipmaps();
        return ImageTexture.CreateFromImage(painted);
    }

    private static float Percentile(List<float> sorted, float q)
    {
        if (sorted.Count == 0)
            return 0f;
        sorted.Sort();
        int i = Mathf.Clamp(Mathf.RoundToInt(q * (sorted.Count - 1)), 0, sorted.Count - 1);
        return sorted[i];
    }

    /// <summary>Godot's smoothstep with edges in either order (edge0 &gt; edge1 gives a
    /// falling ramp) — Mathf.SmoothStep divides by (edge1 - edge0) and misbehaves when that
    /// is negative, which is exactly the hue-falloff case here.</summary>
    private static float SmoothStep(float edge0, float edge1, float x)
    {
        if (Mathf.IsEqualApprox(edge0, edge1))
            return x < edge0 ? 0f : 1f;
        float t = Mathf.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static void RgbToHsv(float r, float g, float b, out float h, out float s, out float v)
    {
        float max = Mathf.Max(r, Mathf.Max(g, b));
        float min = Mathf.Min(r, Mathf.Min(g, b));
        float d = max - min;
        v = max;
        s = max > 1e-6f ? d / max : 0f;
        if (d < 1e-6f)
        {
            h = 0f;
            return;
        }
        h = max == r ? Mathf.PosMod((g - b) / d, 6f)
            : max == g ? (b - r) / d + 2f
            : (r - g) / d + 4f;
        h *= 60f;
    }
}
