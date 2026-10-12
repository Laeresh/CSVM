using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CSVM.Extraction;

namespace CSVM.Tooling;

/// <summary>
/// The synthetic tree's chapter texture archive. The hand-authored extraction manifest in
/// <c>fixtures/synthetic/C1/texture/</c> is copied as it stands, beside one generated PNG per
/// <c>texture_infos</c> entry at that entry's size. The manifest is the record and the pixels
/// are code, so a texture is added by adding a manifest entry. Its shape is the reader's in
/// <c>TextureArchive</c> and the header fields in <c>docs/org/textures.md</c>.
/// </summary>
public static class SyntheticTextures
{
    /// <summary>The chapter the archive is written for, the one every plane-only path reads.</summary>
    public const string Chapter = "C1";

    // Where the manifest sits under the fixtures root, and where the archive lands under extracted/.
    // The archive is the unpacked sibling of C1/texture.zip, which SessionPaths.ChapterTextures takes.
    private const string ManifestFixture = "synthetic/" + ArchiveFolder + "/manifest.json";
    private const string ArchiveFolder = Chapter + "/texture";

    // The checker square's side in texels; every manifest size is a multiple of it.
    private const int Square = 8;

    /// <summary>Writes the archive under <paramref name="tree"/>.</summary>
    public static void WriteChapter(SyntheticTree tree)
    {
        string manifest = tree.CopyFixture(ManifestFixture, ArchiveFolder + "/manifest.json");
        foreach (var (name, width, height) in Entries(manifest))
        {
            tree.WriteBytes($"{ArchiveFolder}/{name}.png", PngWriter.EncodeRgb(width, height, Checker(name, width, height)));
        }
    }

    /// <summary>A checker of the name's own colour against mid grey, RGB with no padding. Distinct
    /// per name, so a capture shows which texture drew where.</summary>
    public static byte[] Checker(string name, int width, int height)
    {
        uint hash = 2166136261;
        foreach (char c in name)
        {
            hash = (hash ^ c) * 16777619;
        }

        byte r = (byte)(64 + (hash & 0x7F)), g = (byte)(64 + ((hash >> 8) & 0x7F)), b = (byte)(64 + ((hash >> 16) & 0x7F));
        var rgb = new byte[width * height * 3];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = ((y * width) + x) * 3;
                bool lit = ((x / Square) + (y / Square)) % 2 == 0;
                rgb[i] = lit ? r : (byte)128;
                rgb[i + 1] = lit ? g : (byte)128;
                rgb[i + 2] = lit ? b : (byte)128;
            }
        }

        return rgb;
    }

    // Every texture_infos entry's name and size. A missing field is an error in the record, not a
    // default, since the loader would read a guessed size as the authored one.
    private static List<(string Name, int Width, int Height)> Entries(string manifestPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var entries = new List<(string, int, int)>();
        foreach (var info in doc.RootElement.GetProperty("texture_infos").EnumerateArray())
        {
            string name = info.GetProperty("name").GetString()
                ?? throw new InvalidDataException($"{manifestPath}: a texture_infos entry has a null name");
            entries.Add((name, info.GetProperty("width").GetInt32(), info.GetProperty("height").GetInt32()));
        }

        return entries;
    }
}
