using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Applies a <see cref="PaintScheme"/> to one aircraft: paints its skins from the original's
/// own per-pattern region masks and swaps the three decal placeholders. Built per plane
/// instance (two players in the same aircraft wear different liveries), and it never mutates
/// the shared <see cref="TextureArchive"/> cache.
///
/// HOW THE ORIGINAL PAINTS, and what this now does (rewritten onto the real data —
/// see <c>docs/formats/rof.md</c>). Each pattern ships a `.BM` per aircraft skin holding a
/// near-greyscale shading map plus three per-pixel weight masks, one per paint colour slot,
/// summing to 255. The composite is
///
///     shaded_paint = shading * (w1*colour1 + w2*colour2 + w3*colour3) / 255
///
/// with the pattern's overlay layer composited over the result. The shading map used is the
/// `.BM`'s own base plane, NOT the ZBD skin of the same name — they are different images (the
/// ZBD one is the blue-gray key texture; the `.BM` one is neutral).
///
/// This replaced a hand-authored table of per-aircraft hue windows, written when the region
/// data was believed absent from the game files. Everything that approach could not do falls
/// out for free here: regions with no hue at all (the Bloodhawk's black outer wing panels),
/// the Fury (whose ZBD skin is featureless near-black yet has full masks), correct slot order,
/// and antialiased region borders that came with the data instead of needing a soft hue
/// falloff to stop them speckling.
///
/// A pattern only covers the aircraft it ships skins for; <see cref="PatternLibrary.PatternsFor"/>
/// is the per-plane list the original's paint UI offers. Parts a pattern does not carry are
/// left as the shipped ZBD texture.
/// </summary>
public sealed class PlanePainter
{
    /// <summary>The three decal slots' texture suffixes, in paint-UI order: nose, tail, wing.</summary>
    private static readonly string[] DecalSuffixes = { "_noselogo", "_taillogo", "_winglogo" };

    private readonly TextureArchive _textures;
    private readonly PatternLibrary _library;
    private readonly PaintScheme _scheme;
    private readonly string _prefix;
    // baseName -> the substitute texture (painted skin or swapped decal); null = leave as is.
    private readonly Dictionary<string, ImageTexture?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public PaintScheme Scheme => _scheme;

    /// <summary>Skins this painter actually repainted, for a one-line build summary.</summary>
    public int PaintedSkins { get; private set; }

    /// <param name="skinPrefix">The aircraft's skin-texture prefix without the underscore
    /// ("blo", "kes", …). <see cref="PrefixFor"/> derives it from the model's own materials.</param>
    public PlanePainter(TextureArchive textures, PatternLibrary library, PaintScheme scheme, string skinPrefix)
    {
        _textures = textures;
        _library = library;
        _scheme = scheme;
        _prefix = skinPrefix;
    }

    /// <summary>True when this scheme's pattern ships no skins for this aircraft, so only its
    /// decals can change. The original's UI never offers such a combination.</summary>
    public bool PatternMissesAircraft => !_library.Covers(_scheme.FolderName, _prefix);

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

        // A decal slot never falls through to the skin paint: its 16x16 placeholder shares the
        // <prefix>_ and no pattern ships a .BM for it anyway.
        int decalSlot = Array.FindIndex(DecalSuffixes,
            s => baseName.EndsWith(s, StringComparison.OrdinalIgnoreCase));
        ImageTexture? made = decalSlot >= 0 ? DecalFor(decalSlot) : PaintedSkin(baseName, original);
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

    // Paints one skin from the pattern's mask set. Null when this pattern ships no .BM for
    // that part (it stays the shipped ZBD texture), which is normal: a pattern covers the
    // airframe skins, not spinners, shadows or cockpit interiors.
    private ImageTexture? PaintedSkin(string baseName, ImageTexture? original)
    {
        if (!baseName.StartsWith(_prefix + "_", StringComparison.OrdinalIgnoreCase))
            return null;
        var bm = _library.Skin(_scheme.FolderName, baseName);
        if (bm == null)
            return null;

        int w = bm.Width, h = bm.Height, n = w * h;
        var data = new byte[n * 4];
        var c1 = _scheme.Color1;
        var c2 = _scheme.Color2;
        var c3 = _scheme.Color3;
        // Colours arrive as 0..1 floats; the masks and shading are 0..255, and the whole
        // composite is done in the DX7-era sRGB space the skin shader samples in.
        float r1 = c1.R * 255f, g1 = c1.G * 255f, b1 = c1.B * 255f;
        float r2 = c2.R * 255f, g2 = c2.G * 255f, b2 = c2.B * 255f;
        float r3 = c3.R * 255f, g3 = c3.G * 255f, b3 = c3.B * 255f;

        // ROW ORDER: `.BM` planes are stored BOTTOM-UP, the ZBD textures and Godot's Image are
        // top-down, so every source row is read from h-1-y. Without this the livery is mirrored
        // along the texture's V axis — user-reported as "the stripes are on the wrong sides of
        // the wings and tail", with the Black Swan Fury (which should read close to its
        // unpainted skin) as the clearest tell. Confirmed by matching each mask's slot-1 region
        // against the ZBD skin's own body-hue region across all four orientations: flipV wins
        // every case that can discriminate (blo_wing IoU 0.60 vs 0.36 unflipped, blo_fin 0.53
        // vs 0.25, pea_wing 0.40 vs 0.15); the cases that disagree are vertically symmetric
        // regions that score the same either way.
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int i = y * w + x;              // destination texel (top-down)
            int j = (h - 1 - y) * w + x;    // source texel (bottom-up)
            float w1 = bm.Slot1[j], w2 = bm.Slot2[j], w3 = bm.Slot3[j];
            // paint colour at this texel, 0..255 per channel
            float pr = (w1 * r1 + w2 * r2 + w3 * r3) * (1f / 255f);
            float pg = (w1 * g1 + w2 * g2 + w3 * g3) * (1f / 255f);
            float pb = (w1 * b1 + w2 * b2 + w3 * b3) * (1f / 255f);
            // modulated by the shading map (panel lines, rivets, baked shading)
            int s = j * 3;
            float outR = bm.Shading[s] * pr * (1f / 255f);
            float outG = bm.Shading[s + 1] * pg * (1f / 255f);
            float outB = bm.Shading[s + 2] * pb * (1f / 255f);

            if (bm.HasOverlay)
            {
                int o = j * 4;
                float a = bm.Overlay![o + 3] * (1f / 255f);
                if (a > 0f)
                {
                    outR = outR * (1f - a) + bm.Overlay[o] * a;
                    outG = outG * (1f - a) + bm.Overlay[o + 1] * a;
                    outB = outB * (1f - a) + bm.Overlay[o + 2] * a;
                }
            }

            int d = i * 4;
            data[d] = (byte)Mathf.Clamp((int)(outR + 0.5f), 0, 255);
            data[d + 1] = (byte)Mathf.Clamp((int)(outG + 0.5f), 0, 255);
            data[d + 2] = (byte)Mathf.Clamp((int)(outB + 0.5f), 0, 255);
            data[d + 3] = 255;
        }

        // The ZBD skin's alpha, where it has one and the grids agree: SceneBuilder already
        // chose blend-vs-scissor from that texture's alpha class, so the substitute has to
        // keep it. Three of 173 skins differ in size between .BM and ZBD (rof.md); those
        // simply come back opaque rather than mis-sampled.
        CopyAlphaFrom(original, data, w, h);

        var img = Image.CreateFromData(w, h, false, Image.Format.Rgba8, data);
        img.GenerateMipmaps();
        PaintedSkins++;
        return ImageTexture.CreateFromImage(img);
    }

    private static void CopyAlphaFrom(ImageTexture? original, byte[] data, int w, int h)
    {
        if (original == null)
            return;
        var src = original.GetImage();
        if (src == null || src.GetWidth() != w || src.GetHeight() != h)
            return;
        if (src.GetFormat() != Image.Format.Rgba8)
        {
            src = (Image)src.Duplicate();
            src.Convert(Image.Format.Rgba8);
        }
        var sd = src.GetData();
        int n = Math.Min(w * h, sd.Length / 4);
        for (int i = 0; i < n; i++)
            data[i * 4 + 3] = sd[i * 4 + 3];
    }
}
