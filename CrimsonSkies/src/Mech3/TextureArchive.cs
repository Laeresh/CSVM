using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Godot;

namespace CrimsonSkies.Mech3;

/// <summary>
/// Texture lookup over a mech3ax texture extraction — a ZIP (texture.zbd → PNGs) or a
/// directory of those same PNGs (e.g. from ExtractAssets.ps1 -Unzip).
/// Material texture names come from fixed-width 20-char fields in planes.zbd, so
/// "blo_fusalagebottom.t" must still find "blo_fusalagebottom.png" — hence the
/// prefix fallback.
/// </summary>
public sealed class TextureArchive : IDisposable
{
    private readonly ZipArchive? _zip;
    private readonly string? _dir;
    // baseName (no extension) -> the PNG's retrieval name (a zip entry's FullName, or a file name under _dir).
    private readonly Dictionary<string, string> _byBaseName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ImageTexture?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (bool HasAlpha, bool Soft)> _alphaInfo = new(StringComparer.OrdinalIgnoreCase);
    // Distinct texture names this archive failed to resolve, each already logged once.
    private readonly HashSet<string> _reportedMissing = new(StringComparer.OrdinalIgnoreCase);

    // Texture names referenced by gamez meshes that ship in NO archive of a retail
    // install — verified absent across all extracted chapters (2026-07-17). The
    // original engine tolerates them (renders neutral), so we do too: a quiet gray
    // fallback instead of the debug magenta, and a one-line data-gap note instead of
    // a lookup-failure warning. Anything NOT on this list that goes missing is likely
    // our own name-resolution failing and stays loud (magenta + the "not found" line).
    private static readonly HashSet<string> KnownAbsentFromGameData = new(StringComparer.OrdinalIgnoreCase)
    {
        "pir_spinner", // referenced by every chapter's gamez (a pirate-zeppelin spinner disc)
        "barngrill",   // C5 only
    };

    /// <summary>True if the name is a texture the retail game data itself lacks (see
    /// KnownAbsentFromGameData) — callers render a neutral fallback, not the debug magenta.</summary>
    public static bool IsKnownAbsent(string materialTextureName) =>
        KnownAbsentFromGameData.Contains(Path.GetFileNameWithoutExtension(materialTextureName));

    /// <summary>Distinct texture names this archive could not resolve, for an end-of-build
    /// summary line. Each was already reported once (one line, no stack trace) by Find.</summary>
    public IReadOnlyCollection<string> MissingTextures => _reportedMissing;

    public TextureArchive(string path)
    {
        if (Directory.Exists(path))
        {
            _dir = path;
            foreach (var file in Directory.EnumerateFiles(path, "*.png"))
                _byBaseName[Path.GetFileNameWithoutExtension(file)] = Path.GetFileName(file);
        }
        else
        {
            _zip = ZipFile.OpenRead(path);
            foreach (var entry in _zip.Entries)
                if (entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    _byBaseName[Path.GetFileNameWithoutExtension(entry.Name)] = entry.FullName;
        }
    }

    /// <summary>True if the last texture returned by Find had an alpha channel.</summary>
    public bool LastHadAlpha { get; private set; }

    /// <summary>
    /// True if the last texture's alpha is "soft": a 1-bit scissor cutout at the usual 0.5
    /// threshold would erase it entirely or reduce it to a crude stencil. True for the
    /// original's translucent overlays — baked shadow decals (max alpha ~125/255), cloud
    /// and prop-blur sprites, waterfalls, smoke — which the original engine alpha-blends;
    /// false for genuine cutouts (fences, trees, railings), which scissor correctly.
    /// </summary>
    public bool LastAlphaIsSoft { get; private set; }

    public ImageTexture? Find(string materialTextureName)
    {
        var baseName = Path.GetFileNameWithoutExtension(materialTextureName);
        // A truncated name like "blo_fusalagebottom.t" keeps its bogus extension after
        // GetFileNameWithoutExtension strips ".t"; that is exactly the prefix we want.
        if (_cache.TryGetValue(baseName, out var cached))
        {
            (LastHadAlpha, LastAlphaIsSoft) = _alphaInfo[baseName];
            return cached;
        }

        var name = Resolve(baseName);
        var bytes = name != null ? ReadBytes(name) : null;
        ImageTexture? tex = null;
        LastHadAlpha = false;
        LastAlphaIsSoft = false;
        if (bytes != null)
        {
            var img = new Image();
            if (img.LoadPngFromBuffer(bytes) == Error.Ok)
            {
                LastHadAlpha = ImageHasAlpha(img);
                LastAlphaIsSoft = LastHadAlpha && AlphaIsSoft(img); // before mipmaps: raw pixels only
                img.GenerateMipmaps();
                tex = ImageTexture.CreateFromImage(img);
            }
        }
        else if (_reportedMissing.Add(baseName))
        {
            // Report each distinct miss once as a plain line. GD.PushWarning would print
            // a full managed stack trace per call in Godot .NET, which buries real errors.
            GD.Print(IsKnownAbsent(materialTextureName)
                ? $"[textures] {materialTextureName}: absent from game data — gray fallback"
                : $"[textures] not found in archive: {materialTextureName}");
        }
        _cache[baseName] = tex;
        _alphaInfo[baseName] = (LastHadAlpha, LastAlphaIsSoft);
        return tex;
    }

    /// <summary>The raw PNG as a fresh, un-mipmapped <see cref="Image"/> — the source
    /// <see cref="PlanePainter"/> recolours. Deliberately NOT the ImageTexture cache: that
    /// one is shared across every plane and world instance and must never be mutated, and
    /// its images already carry generated mipmaps. Each call returns a new Image.</summary>
    public Image? FindImage(string materialTextureName)
    {
        var name = Resolve(Path.GetFileNameWithoutExtension(materialTextureName));
        var bytes = name != null ? ReadBytes(name) : null;
        if (bytes == null)
            return null;
        var img = new Image();
        return img.LoadPngFromBuffer(bytes) == Error.Ok ? img : null;
    }

    /// <summary>The archive's texture whose name begins with a zero-padded two-digit
    /// number, e.g. 21 → "21ace_star" — how <c>paint_decalN</c> indexes the gapless 00–49
    /// decal set every chapter ships. Null when out of range. The half-size "_1" LOD twins
    /// are excluded (they share the numeric prefix but are not the decal itself).</summary>
    public string? FindByDecalIndex(int index)
    {
        if (index is < 0 or > 99)
            return null;
        var prefix = index.ToString("00");
        foreach (var name in _byBaseName.Keys)
            if (name.Length > 2 && name.StartsWith(prefix, StringComparison.Ordinal)
                && !char.IsDigit(name[2]) && !name.EndsWith("_1", StringComparison.Ordinal))
                return name;
        return null;
    }

    // Classifies the alpha channel (see LastAlphaIsSoft). Soft when scissoring at 0.5
    // would show (almost) nothing — max alpha below ~140/255 — or when partial alpha
    // dominates and nearly-opaque texels are rare (soft sprites like clouds and smoke,
    // whose scissor cutout is a shredded stencil of only their densest texels).
    private static bool AlphaIsSoft(Image img)
    {
        var rgba = img;
        if (img.GetFormat() != Image.Format.Rgba8)
        {
            rgba = (Image)img.Duplicate();
            rgba.Convert(Image.Format.Rgba8);
        }
        var data = rgba.GetData();
        int max = 0, mid = 0, opaque = 0, total = data.Length / 4;
        if (total == 0)
            return false;
        for (int i = 3; i < data.Length; i += 4)
        {
            int a = data[i];
            if (a > max) max = a;
            if (a >= 200) opaque++;
            else if (a >= 32) mid++;
        }
        if (max < 140)
            return true;
        return opaque < total * 0.30f && mid > total * 0.35f;
    }

    private string? Resolve(string baseName)
    {
        if (_byBaseName.TryGetValue(baseName, out var exact))
            return exact;
        // mech3ax disambiguates duplicate texture-table entries as "name.-N";
        // the pixel data lives under the original name
        var m = System.Text.RegularExpressions.Regex.Match(baseName, @"^(.*)\.-\d+$");
        if (m.Success && _byBaseName.TryGetValue(m.Groups[1].Value, out var renamed))
            return renamed;
        // fixed-width truncation fallback: unique prefix match
        string? match = null;
        foreach (var (name, retrieval) in _byBaseName)
        {
            if (name.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
            {
                if (match != null)
                    return null; // ambiguous
                match = retrieval;
            }
        }
        return match;
    }

    private byte[]? ReadBytes(string retrievalName)
    {
        if (_dir != null)
        {
            var p = Path.Combine(_dir, retrievalName);
            return File.Exists(p) ? File.ReadAllBytes(p) : null;
        }
        var entry = _zip!.GetEntry(retrievalName);
        if (entry == null)
            return null;
        using var s = entry.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private static bool ImageHasAlpha(Image img) =>
        img.GetFormat() is Image.Format.Rgba8 or Image.Format.La8 or Image.Format.Rgba4444 && img.DetectAlpha() != Image.AlphaMode.None;

    public void Dispose() => _zip?.Dispose();
}
