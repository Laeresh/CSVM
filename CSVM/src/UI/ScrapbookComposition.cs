using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CSVM.Session;

namespace CSVM.UI;

/// <summary>One scrap authored on a scrapbook spread: <c>extracted\rof\ASSETS\SCRAPBOOK.CSV</c>'s
/// own columns, the ones D18 draws with
/// (<c>docs/formats/campaign-screens.md#scrapbookcsv</c>). <see cref="Extension"/> is already
/// resolved from <c>ImageType</c>'s page letter (<c>B</c> BMP, <c>J</c> JPG, else PNG).</summary>
public readonly record struct ScrapbookScrap(
    int Objective, string ImageName, string Extension, float X, float Y, int DrawOrder)
{
    /// <summary>A player capture rather than shipped art: resolved against the profile directory,
    /// not <c>assets\graphics\</c>, and skipped when the file is not on disk
    /// (<c>docs/org/debrief.md#navigation-and-page-composition-a-data-file-not-code</c>).</summary>
    public bool IsCapture => ImageName.StartsWith("Snap_", StringComparison.Ordinal);

    /// <summary>The capture's own file name, for the profile-directory lookup
    /// <see cref="IsCapture"/> callers make.</summary>
    public string FileName => $"{ImageName}.{Extension}";
}

/// <summary>
/// The scrapbook's per-spread scrap layout, read from the shipped <c>SCRAPBOOK.CSV</c> rather than
/// invented: one <c>[SCRAPBOOK]</c> section keyed <c>&lt;mission&gt;_&lt;spread&gt;_&lt;item&gt;</c>,
/// enumerated upward from item 1 and stopped at the first missing key, the way the original's own
/// reader does (<c>docs/org/debrief.md#navigation-and-page-composition-a-data-file-not-code</c>).
/// Both the results page (spread 1) and the story pages (spread 2 and, on 6 missions, 3) draw from
/// this: "the results page is a story page with the card laid over its right half"
/// (<c>docs/formats/campaign-screens.md</c>, "The scrapbook"). Parsed rows are cached per file path,
/// misses included (<see cref="PlaneDiagrams"/>'s own precedent), so an install without the
/// extraction probes the disk once rather than once per spread.
/// </summary>
public static class ScrapbookComposition
{
    private static readonly Dictionary<string, Dictionary<string, string[]>?> Files =
        new(StringComparer.Ordinal);

    /// <summary>The mission slot's spread, in item order (not draw order): the raw rows, with no
    /// objective gate or capture check applied. Empty when <paramref name="dataRoot"/> is null, the
    /// extraction lacks the file, or the spread has no item 1.</summary>
    public static IReadOnlyList<ScrapbookScrap> Items(string? dataRoot, int mission, int spread)
    {
        var rows = dataRoot == null ? null : Load(Path.Combine(
            dataRoot, "extracted", "rof", "ASSETS", "SCRAPBOOK.CSV"));
        if (rows == null)
        {
            return Array.Empty<ScrapbookScrap>();
        }

        var items = new List<ScrapbookScrap>();
        for (int item = 1; ; item++)
        {
            if (!rows.TryGetValue($"{mission}_{spread}_{item}", out var fields))
            {
                break;
            }

            items.Add(Parse(fields));
        }

        return items;
    }

    /// <summary>The spread's scraps as drawable pictures, gated and ordered the way the original
    /// draws them: the <c>Objective</c> gate applied against <paramref name="bestMask"/> (the
    /// mission's merged best-to-date completion mask), a capture skipped when
    /// <paramref name="captureExists"/> says its file is not on disk, and the survivors stacked by
    /// ascending <c>DrawOrder</c> (background first).</summary>
    public static IReadOnlyList<BoardPicture> Pictures(
        string? dataRoot, int mission, int spread, int bestMask, Func<ScrapbookScrap, bool> captureExists)
    {
        var visible = new List<ScrapbookScrap>();
        foreach (var scrap in Items(dataRoot, mission, spread))
        {
            if (!Visible(scrap.Objective, bestMask))
            {
                continue;
            }

            if (scrap.IsCapture && !captureExists(scrap))
            {
                continue;
            }

            visible.Add(scrap);
        }

        visible.Sort((a, b) => a.DrawOrder.CompareTo(b.DrawOrder));

        var pictures = new List<BoardPicture>(visible.Count);
        foreach (var scrap in visible)
        {
            pictures.Add(new BoardPicture(
                new BoardArt(BoardArtLibrary.Ui, $"SCRAPBOOK/{scrap.FileName}"), scrap.X, scrap.Y));
        }

        return pictures;
    }

    /// <summary>The <c>Objective</c> column's own gate: 0 always draws; any other value first
    /// requires bit 0 of <paramref name="bestMask"/> (the mission won at least once), then requires
    /// its own bit set for a positive value or clear for a negative one
    /// (<c>docs/formats/campaign-screens.md#the-objective-gate</c>). CSVM carries no unlock-flag
    /// analogue, so unlike the original this cannot be bypassed.</summary>
    public static bool Visible(int objective, int bestMask)
    {
        if (objective == 0)
        {
            return true;
        }

        if ((bestMask & CampaignProgression.PrimaryObjectiveMask) == 0)
        {
            return false;
        }

        int bit = 1 << Math.Abs(objective);
        return objective > 0 ? (bestMask & bit) != 0 : (bestMask & bit) == 0;
    }

    private static ScrapbookScrap Parse(string[] fields)
    {
        int objective = int.Parse(fields[0].Trim(), CultureInfo.InvariantCulture);
        string imageName = fields[2].Trim();
        string imageType = fields[3].Trim();
        char pageLetter = imageType.Length > 0 ? imageType[0] : '\0';
        string extension = pageLetter switch
        {
            'B' or 'b' => "BMP",
            'J' or 'j' => "JPG",
            _ => "PNG",
        };
        float x = float.Parse(fields[4].Trim(), CultureInfo.InvariantCulture);
        float y = float.Parse(fields[5].Trim(), CultureInfo.InvariantCulture);
        int drawOrder = int.Parse(fields[9].Trim(), CultureInfo.InvariantCulture);
        return new ScrapbookScrap(objective, imageName, extension, x, y, drawOrder);
    }

    // The [SCRAPBOOK] section as a flat key->fields map: comment (';') and section ('[') lines
    // skipped, everything else split on the key's '=' then the value's ','. The one quoted field
    // (the clickable region) sits past every column Parse reads, so the naive comma split never
    // needs to respect its quoting.
    private static Dictionary<string, string[]>? Load(string path)
    {
        if (Files.TryGetValue(path, out var cached))
        {
            return cached;
        }

        Dictionary<string, string[]>? rows = null;
        if (File.Exists(path))
        {
            rows = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var line in File.ReadAllLines(path))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] == ';' || trimmed[0] == '[')
                {
                    continue;
                }

                int eq = trimmed.IndexOf('=');
                if (eq < 0)
                {
                    continue;
                }

                rows[trimmed[..eq]] = trimmed[(eq + 1)..].Split(',');
            }
        }

        Files[path] = rows;
        return rows;
    }
}
