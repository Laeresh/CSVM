using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using CSVM.Extraction;
using CSVM.Utils;

namespace CSVM.Mech3;

/// <summary>
/// A species' clump tiles: a strip of equal tiles rendered from its 3D model. They fill clump atlas slots in
/// place of tiles composed from the cards (<see cref="CanopyClumps"/>). The sidecar gives a tile's width in
/// metres and each tile's tree count; the height follows from the image. The optional side map gives each
/// tree's sun side. Read from a model pack, else shipped beside the model, else none. formats/model-packs.md has it.
/// </summary>
public sealed record ClumpTile(string Source, byte[] Image, byte[]? Side, float WidthM, IReadOnlyList<int> Trees)
{
    /// <summary>The tile of <paramref name="card"/> on <paramref name="chapter"/> from the first of
    /// <paramref name="sources"/> holding one, or null. Image, sidecar and side map all come from that one
    /// source. A source with one of image and sidecar alone, or a bad sidecar, is warned and passed over. A
    /// broken replacement then keeps the shipped tile.</summary>
    public static ClumpTile? Resolve(string chapter, string card, params Func<string, (byte[] Bytes, string Source)?>[] sources)
    {
        string stem = $"{chapter}/{card}";
        foreach (var read in sources)
        {
            var image = read(stem + HdModels.ClumpSuffixes[0]);
            var sidecar = read(stem + HdModels.ClumpSuffixes[1]);
            if (image == null && sidecar == null)
                continue;
            if (image is not { } png || sidecar is not { } json)
            {
                Log.Warn("world", $"clump tile {stem}: {(image ?? sidecar)!.Value.Source} has no {(image == null ? "image" : "sidecar")} beside it, skipped");
                continue;
            }
            try
            {
                var (width, trees) = ParseSidecar(json.Bytes, json.Source);
                return new ClumpTile(png.Source, png.Bytes, read(stem + HdModels.ClumpSuffixes[2])?.Bytes, width, trees);
            }
            catch (FormatException e)
            {
                Log.Warn("world", $"clump tile {stem} skipped: {e.Message}");
            }
        }

        return null;
    }

    /// <summary>A sidecar's <c>width_m</c> (above 0, at most <see cref="CanopyClumps.TileWidthM"/>) and
    /// <c>trees</c> (one whole count of at least 1 per tile). Keys starting <c>_</c> are prose; any other key
    /// fails. Throws a <see cref="FormatException"/> naming <paramref name="source"/>.</summary>
    public static (float WidthM, int[] Trees) ParseSidecar(byte[] json, string source)
    {
        FormatException Bad(string why) => new($"{source}: {why}");
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw Bad($"not readable JSON ({e.Message})");
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw Bad("not a JSON object");
            float? width = null;
            int[]? trees = null;
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (p.Name == "width_m")
                {
                    width = p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetSingle(out float w) && float.IsFinite(w)
                        && w > 0f && w <= CanopyClumps.TileWidthM ? w
                        : throw Bad(string.Create(CultureInfo.InvariantCulture, $"width_m must be a number above 0 and at most {CanopyClumps.TileWidthM} m"));
                }
                else if (p.Name == "trees")
                {
                    if (p.Value.ValueKind != JsonValueKind.Array || p.Value.GetArrayLength() == 0)
                        throw Bad("trees must be a list of one count per tile");
                    var list = new List<int>();
                    foreach (var t in p.Value.EnumerateArray())
                        list.Add(t.ValueKind == JsonValueKind.Number && t.TryGetInt32(out int n) && n >= 1 ? n : throw Bad($"trees holds {t}, not a whole count of at least 1"));
                    trees = list.ToArray();
                }
                else if (!p.Name.StartsWith('_'))
                {
                    throw Bad($"unknown key \"{p.Name}\"");
                }
            }

            return (width ?? throw Bad("width_m is missing"), trees ?? throw Bad("trees is missing"));
        }
    }
}
