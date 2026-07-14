using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Godot;

namespace CrimsonSkies.Mech3;

/// <summary>
/// Texture lookup over a mech3ax texture extraction ZIP (texture.zbd → PNGs).
/// Material texture names come from fixed-width 20-char fields in planes.zbd, so
/// "blo_fusalagebottom.t" must still find "blo_fusalagebottom.png" — hence the
/// prefix fallback.
/// </summary>
public sealed class TextureArchive : IDisposable
{
    private readonly ZipArchive _zip;
    private readonly Dictionary<string, ZipArchiveEntry> _byBaseName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ImageTexture?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public TextureArchive(string zipPath)
    {
        _zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in _zip.Entries)
        {
            if (entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                _byBaseName[Path.GetFileNameWithoutExtension(entry.Name)] = entry;
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

        var entry = Resolve(baseName);
        ImageTexture? tex = null;
        if (entry != null)
        {
            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var img = new Image();
            if (img.LoadPngFromBuffer(ms.ToArray()) == Error.Ok)
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

    private ZipArchiveEntry? Resolve(string baseName)
    {
        if (_byBaseName.TryGetValue(baseName, out var exact))
            return exact;
        // mech3ax disambiguates duplicate texture-table entries as "name.-N";
        // the pixel data lives under the original name
        var m = System.Text.RegularExpressions.Regex.Match(baseName, @"^(.*)\.-\d+$");
        if (m.Success && _byBaseName.TryGetValue(m.Groups[1].Value, out var renamed))
            return renamed;
        // fixed-width truncation fallback: unique prefix match
        ZipArchiveEntry? match = null;
        foreach (var (name, entry) in _byBaseName)
        {
            if (name.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
            {
                if (match != null)
                    return null; // ambiguous
                match = entry;
            }
        }
        return match;
    }

    private static bool ImageHasAlpha(Image img) =>
        img.GetFormat() is Image.Format.Rgba8 or Image.Format.La8 or Image.Format.Rgba4444 && img.DetectAlpha() != Image.AlphaMode.None;

    public void Dispose() => _zip.Dispose();
}
