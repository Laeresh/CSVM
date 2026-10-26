using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using CSVM.Mech3;
using AlphaClass = CSVM.Mech3.TextureArchive.AlphaClass;

namespace CSVM.Tests.Oracle;

/// <summary>The engine's former read of a texture archive: the extraction's <c>.zip</c> or its
/// unpacked folder, one PNG per texture beside <c>manifest.json</c>. It is the oracle
/// <see cref="TextureZbd.Read"/> is compared against. Test-only; PNGs decode engine-free.</summary>
public static class TexturesLegacy
{
    private static readonly Dictionary<string, TextureSet> Rimages = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The <c>rimage</c> art extracted under <paramref name="dataRoot"/>, for a unit that
    /// hands a screen an extracted data root; empty for null or no extraction. Read once per root.</summary>
    public static TextureSet Rimage(string? dataRoot)
    {
        string? folder = dataRoot == null ? null : Path.Combine(dataRoot, "extracted", "rimage");
        if (folder == null || !Directory.Exists(folder))
        {
            return TextureSet.Empty;
        }

        lock (Rimages)
        {
            if (!Rimages.TryGetValue(folder, out var set))
            {
                set = new TextureSet("rimage", Load(folder));
                Rimages[folder] = set;
            }

            return set;
        }
    }

    /// <summary>The records of the archive at <paramref name="zipOrFolder"/>, in the manifest's
    /// order: its per-texture fields, and each PNG's pixels in the layout its colour type gives.</summary>
    public static List<TextureImage> Load(string zipOrFolder)
    {
        using var archive = Directory.Exists(zipOrFolder) ? null : ZipFile.OpenRead(zipOrFolder);
        byte[] Read(string name)
        {
            if (archive == null)
            {
                return File.ReadAllBytes(Path.Combine(zipOrFolder, name));
            }

            var entry = archive.GetEntry(name) ?? throw new FileNotFoundException(name);
            using var stream = entry.Open();
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }

        using var manifest = JsonDocument.Parse(Read("manifest.json"));
        var textures = new List<TextureImage>();
        foreach (var info in manifest.RootElement.GetProperty("texture_infos").EnumerateArray())
        {
            string name = info.GetProperty("name").GetString()!;
            string? rename = info.TryGetProperty("rename", out var r) ? r.GetString() : null;
            var png = Read((rename ?? name) + ".png");
            if (PngImage.ReadGamma(png) != null)
            {
                throw new InvalidDataException($"{zipOrFolder}: {name}.png carries a gAMA chunk");
            }

            var image = PngImage.Decode(png) ?? throw new InvalidDataException($"{zipOrFolder}: {name}.png would not decode");
            bool alphaChannel = png[25] == 6; // IHDR colour type: 6 RGBA, 2 RGB
            if ((info.GetProperty("width").GetInt32(), info.GetProperty("height").GetInt32()) != (image.Width, image.Height))
            {
                throw new InvalidDataException($"{zipOrFolder}: {name}.png is not the manifest's size");
            }

            textures.Add(new TextureImage(
                name,
                rename,
                image.Width,
                image.Height,
                Enum.Parse<AlphaClass>(info.GetProperty("alpha").GetString()!),
                Stretch(info.GetProperty("stretch").GetString()!),
                alphaChannel,
                alphaChannel ? image.Rgba : DropAlpha(image.Rgba)));
        }

        return textures;
    }

    // The extraction's spelling of the render-flags word. ⚠ Vertical is 1 and Horizontal 2; the
    // engine's former reader had the two swapped, which nothing read.
    private static int Stretch(string spelling) => spelling switch
    {
        "None" => 0,
        "Vertical" => 1,
        "Horizontal" => 2,
        "Both" => 3,
        _ when spelling.StartsWith("Unk", StringComparison.Ordinal) => int.Parse(spelling[3..]),
        _ => throw new InvalidDataException($"stretch {spelling}"),
    };

    private static byte[] DropAlpha(byte[] rgba)
    {
        var rgb = new byte[rgba.Length / 4 * 3];
        for (int i = 0, o = 0; i < rgba.Length; i += 4)
        {
            rgb[o++] = rgba[i];
            rgb[o++] = rgba[i + 1];
            rgb[o++] = rgba[i + 2];
        }

        return rgb;
    }
}
