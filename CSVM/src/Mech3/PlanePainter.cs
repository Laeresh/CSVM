using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Applies a <see cref="PaintScheme"/> to one aircraft: paints its skins from the original's
/// per-pattern region masks and swaps the three decal placeholders. Built per plane instance,
/// since two players in the same aircraft can wear different liveries, and it never mutates the
/// shared <see cref="TextureArchive"/> cache.
/// Composite formula, mask/decal layout and <see cref="PatternLibrary.PatternsFor"/>'s per-plane
/// pattern list: <c>docs/formats/paint.md</c> and <c>docs/formats/rof.md</c>.
/// </summary>
public sealed class PlanePainter
{
    // The three decal slots' texture suffixes, in paint-UI order: nose, tail, wing.
    private static readonly string[] DecalSuffixes = { "_noselogo", "_taillogo", "_winglogo" };

    private readonly TextureArchive _textures;
    private readonly PatternLibrary _library;
    private readonly PaintScheme _scheme;
    private readonly string _prefix;
    // baseName -> the substitute texture (painted skin or swapped decal); null = leave as is.
    private readonly Dictionary<string, ImageTexture?> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="skinPrefix">The aircraft's skin-texture prefix without the underscore
    /// ("blo", "kes", …). <see cref="PrefixFor"/> derives it from the model's own materials.</param>
    public PlanePainter(TextureArchive textures, PatternLibrary library, PaintScheme scheme, string skinPrefix)
    {
        _textures = textures;
        _library = library;
        _scheme = scheme;
        _prefix = skinPrefix;
    }

    public PaintScheme Scheme => _scheme;

    /// <summary>Skins this painter actually repainted, for a one-line build summary.</summary>
    public int PaintedSkins { get; private set; }

    /// <summary>True when this scheme's pattern ships no skins for this aircraft, so only its
    /// decals can change. The original's UI never offers such a combination.</summary>
    public bool PatternMissesAircraft => !_library.Covers(_scheme.FolderName, _prefix);

    /// <summary>The aircraft's skin-texture prefix, read off the model's own material names
    /// (every skin of one aircraft shares it: blo_wing, blo_fin, blo_noselogo…). Data-driven
    /// rather than a node-name table, so it survives node renames. Null if nothing matches.</summary>
    public static string? PrefixFor(GameZ gamez, GameZNode root)
    {
        // ⚠ Match any of the three decal placeholders, never the nose alone. The Firebrand
        // ships no fir_noselogo, so a nose-only test leaves it unpainted (docs/formats/paint.md).
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

        // ⚠ `.BM` rows are bottom-up, Godot's Image is top-down; read every source row from
        // h-1-y or the livery mirrors along V (docs/formats/rof.md).
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

        // Carries the ZBD skin's alpha so SceneBuilder's blend-vs-scissor choice stays valid.
        // A size mismatch (rof.md) comes back opaque rather than mis-sampled.
        CopyAlphaFrom(original, data, w, h);

        var img = Image.CreateFromData(w, h, false, Image.Format.Rgba8, data);
        img.GenerateMipmaps();
        PaintedSkins++;
        return ImageTexture.CreateFromImage(img);
    }
}
