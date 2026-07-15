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

    public ImageTexture? Find(string materialTextureName)
    {
        var baseName = Path.GetFileNameWithoutExtension(materialTextureName);
        // A truncated name like "blo_fusalagebottom.t" keeps its bogus extension after
        // GetFileNameWithoutExtension strips ".t"; that is exactly the prefix we want.
        if (_cache.TryGetValue(baseName, out var cached))
        {
            LastHadAlpha = cached != null && ImageHasAlpha(cached.GetImage());
            return cached;
        }

        var name = Resolve(baseName);
        var bytes = name != null ? ReadBytes(name) : null;
        ImageTexture? tex = null;
        if (bytes != null)
        {
            var img = new Image();
            if (img.LoadPngFromBuffer(bytes) == Error.Ok)
            {
                img.GenerateMipmaps();
                tex = ImageTexture.CreateFromImage(img);
                LastHadAlpha = ImageHasAlpha(img);
            }
        }
        else
        {
            GD.PushWarning($"texture not found in archive: {materialTextureName}");
            LastHadAlpha = false;
        }
        _cache[baseName] = tex;
        return tex;
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
