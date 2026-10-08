using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CSVM.Extraction;
using CSVM.Tooling;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The synthetic tree's Original shell: the <c>extracted/rof/</c> files its availability check and
/// its screens read. The hand-authored <c>fixtures/menu-layout-original/LAYOUT.CSV</c> goes through
/// the extraction's own decoder into <c>menu_layout.json</c>, joined to the invented string rows of
/// <c>fixtures/synthetic/rof/ui_strings.json</c>. A row with a symbol is a layout string; one
/// without is a row the code reads by id. Every art file is generated at the size
/// <c>fixtures/synthetic/rof/art.json</c> gives it, so a widget's size is a record and its pixels
/// are code. A file the manifest requires and the record does not size stops the build.
/// </summary>
public static class SyntheticShell
{
    /// <summary>Every family of the synthetic tree, the tooling ones and then this one. A tooling
    /// family may not name a UI type, so the whole list is composed here, the lowest family that
    /// names both. Every caller of <see cref="SyntheticData.Build"/> hands it in.</summary>
    public static readonly IReadOnlyList<SyntheticFamily> TreeFamilies =
        SyntheticData.Families.Append(new SyntheticFamily("original-shell", WriteShell)).ToArray();

    private const string LayoutFixture = "menu-layout-original/LAYOUT.CSV";
    private const string StringsFixture = "synthetic/rof/ui_strings.json";
    private const string ArtFixture = "synthetic/rof/art.json";

    /// <summary>Writes the shell's files under <paramref name="tree"/>.</summary>
    public static void WriteShell(SyntheticTree tree)
    {
        string strings = tree.CopyFixture(StringsFixture, ExtractionRun.RofFolder + "/ui_strings.json");
        var rows = Rows(strings);
        var art = Art(tree.Fixture(ArtFixture));
        var input = new MenuLayoutInput
        {
            LayoutCsv = File.ReadAllText(tree.Fixture(LayoutFixture), Encoding.ASCII),
            ResourceHeader = string.Concat(rows.Where(r => r.Symbol != null).Select(r => $"#define {r.Symbol} {r.Id}\n")),
            Strings = rows.ToDictionary(r => r.Id, r => r.Text),
            ArchiveFiles = art.Keys.Select(name => "GRAPHICS/" + name).ToList(),
        };
        string json = MenuLayoutDecoder.ToJson(MenuLayoutDecoder.Decode(input));
        File.WriteAllText(tree.Under(ExtractionRun.RofFolder + "/menu_layout.json"), json, new UTF8Encoding(false));

        // A required file with no size would pass the build and refuse Original at run time, far
        // from the record that lacks it.
        var manifest = OriginalAssetManifest.Derive(MenuLayout.Parse(json));
        foreach (var asset in manifest.Assets)
        {
            if (asset.Need == OriginalAssetNeed.Required && !OriginalAvailability.IsMovie(asset.Name) && !art.ContainsKey(asset.Name))
            {
                throw new InvalidDataException($"{ArtFixture} gives no size for {asset.Name}, which [{asset.Section}] {asset.Row} requires");
            }
        }

        foreach (var (name, (width, height)) in art)
        {
            tree.WriteBytes(ExtractionRun.RofFolder + "/" + OriginalAvailability.RelativeArtPath(name), Encode(name, width, height));
        }
    }

    // The picture in the format its extension names, since a loader picks its decoder that way.
    private static byte[] Encode(string name, int width, int height) =>
        Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".png" => PngWriter.EncodeRgb(width, height, SyntheticTextures.Checker(name, width, height)),
            ".jpg" => SyntheticImages.Jpeg(name, width, height),
            ".tga" => SyntheticImages.Tga(name, width, height),
            _ => throw new InvalidDataException($"{ArtFixture}: {name} is not a picture this tree can write"),
        };

    private static List<UiStringRow> Rows(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        var rows = new List<UiStringRow>();
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            rows.Add(new UiStringRow(
                row.GetProperty("id").GetInt32(),
                row.GetProperty("symbol").GetString(),
                null,
                row.GetProperty("text").GetString() ?? string.Empty,
                row.GetProperty("dll").GetString() ?? string.Empty));
        }

        return rows;
    }

    // Name to size, in record order. A missing field is an error, not a default.
    private static Dictionary<string, (int Width, int Height)> Art(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        var art = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in doc.RootElement.GetProperty("art").EnumerateArray())
        {
            string name = entry.GetProperty("name").GetString() ?? throw new InvalidDataException($"{path}: an entry has no name");
            if (!art.TryAdd(name, (entry.GetProperty("width").GetInt32(), entry.GetProperty("height").GetInt32())))
            {
                throw new InvalidDataException($"{path}: {name} is listed twice");
            }
        }

        return art;
    }
}
