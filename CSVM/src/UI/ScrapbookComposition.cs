using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CSVM.Session;

namespace CSVM.UI;

/// <summary>One scrap authored on a scrapbook spread: <c>extracted\rof\ASSETS\SCRAPBOOK.CSV</c>'s
/// own columns (<c>docs/formats/campaign-screens.md#scrapbookcsv</c>). <see cref="Extension"/> is
/// resolved from <c>ImageType</c>'s first letter (<c>B</c> BMP, <c>J</c> JPG, else PNG).</summary>
public readonly record struct ScrapbookScrap(
    int Objective, string ImageName, string Extension, float X, float Y, int DrawOrder,
    char Zoom, float ZoomX, float ZoomY, string CaptionKey, string TitleKey, string TextKey)
{
    /// <summary>The row's own 1-based item ordinal within its spread, for re-finding this scrap
    /// later (<see cref="ScrapbookComposition.Items"/> is re-read fresh rather than cached by a
    /// caller).</summary>
    public int Item { get; init; }

    /// <summary>A player capture rather than shipped art: resolved against the profile directory,
    /// not <c>assets\graphics\</c>, and skipped when the file is not on disk
    /// (<c>docs/org/debrief.md#navigation-and-page-composition-a-data-file-not-code</c>).</summary>
    public bool IsCapture => ImageName.StartsWith("Snap_", StringComparison.Ordinal);

    /// <summary>The capture's own file name, for the profile-directory lookup
    /// <see cref="IsCapture"/> callers make.</summary>
    public string FileName => $"{ImageName}.{Extension}";

    /// <summary>Whether this scrap opens into a detail view at all: the <c>Zoom</c> column is a
    /// real family letter rather than <c>0</c>. Every photo-corner mount (<c>DZ_generic_corners</c>,
    /// 167 rows) carries <c>Zoom=0</c> and never opens; every capture and every ordinary scrap that
    /// authors a family letter does.</summary>
    public bool Opens => Zoom != '0';

    /// <summary>The zoom inset's own extension, resolved the same three-way way the page image's
    /// is (<c>ImageType</c>'s second letter), meaningful only while <see cref="Opens"/>.</summary>
    public string ZoomExtension { get; init; }

    /// <summary>The inset image's file name for the zoom view.</summary>
    public string ZoomFileName => $"{ImageName}.{ZoomExtension}";
}

/// <summary>One zoom family's three text boxes (title, caption, body), <c>LAYOUT.CSV</c>'s
/// <c>SBZ_T_TITLE&lt;letter&gt;</c>/<c>CAPTION&lt;letter&gt;</c>/<c>TEXT&lt;letter&gt;</c> rows: each
/// box's authored top-left and wrap width. Colour and font-index columns are not carried: a
/// <see cref="BoardLine"/> has no colour of its own (ink is a role, not a literal), and two of the
/// 26 families' colour fields are typo'd and would not parse anyway
/// (<c>docs/formats/campaign-screens.md#resolving-a-row-to-a-file</c>).</summary>
public readonly record struct ScrapbookZoomFamily(
    float TitleX, float TitleY, float TitleWidth,
    float CaptionX, float CaptionY, float CaptionWidth,
    float TextX, float TextY, float TextWidth);

/// <summary>
/// The scrapbook's per-spread scrap layout, read from the shipped <c>SCRAPBOOK.CSV</c> rather than
/// invented: one <c>[SCRAPBOOK]</c> section keyed <c>&lt;mission&gt;_&lt;spread&gt;_&lt;item&gt;</c>,
/// enumerated upward from item 1 and stopped at the first missing key, the way the original's own
/// reader does (<c>docs/org/debrief.md#navigation-and-page-composition-a-data-file-not-code</c>).
/// Whether a scrap opens into a detail view is the <c>Zoom</c> column alone
/// (<see cref="ScrapbookScrap.Opens"/>), not <c>ImageType</c>'s second letter
/// (<c>docs/formats/campaign-screens.md</c>, "The scrapbook"). Parsed rows are cached per file
/// path, misses included (<see cref="PlaneDiagrams"/>'s own precedent).
/// </summary>
public static class ScrapbookComposition
{
    private static readonly Dictionary<string, Dictionary<string, string[]>?> Files =
        new(StringComparer.Ordinal);

    private static readonly Dictionary<string, Dictionary<char, ScrapbookZoomFamily>?> ZoomFamilies =
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

            items.Add(Parse(fields) with { Item = item });
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
        var visible = Filtered(dataRoot, mission, spread, bestMask, captureExists);
        visible.Sort((a, b) => a.DrawOrder.CompareTo(b.DrawOrder));

        var pictures = new List<BoardPicture>(visible.Count);
        foreach (var scrap in visible)
        {
            pictures.Add(new BoardPicture(
                new BoardArt(BoardArtLibrary.Ui, $"SCRAPBOOK/{scrap.FileName}"), scrap.X, scrap.Y));
        }

        return pictures;
    }

    /// <summary>The spread's scraps a player can open into detail, in item order: the same gate
    /// <see cref="Pictures"/> applies, narrowed to <see cref="ScrapbookScrap.Opens"/>.</summary>
    public static IReadOnlyList<ScrapbookScrap> Openable(
        string? dataRoot, int mission, int spread, int bestMask, Func<ScrapbookScrap, bool> captureExists)
    {
        var visible = Filtered(dataRoot, mission, spread, bestMask, captureExists);
        visible.RemoveAll(s => !s.Opens);
        return visible;
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

    /// <summary>One zoom family's three text boxes, or null when the letter carries none (no such
    /// family, or the extraction lacks <c>LAYOUT.CSV</c>). Cached per file path, misses
    /// included.</summary>
    public static ScrapbookZoomFamily? ZoomFamily(string? dataRoot, char letter)
    {
        var families = dataRoot == null ? null : LoadZoomFamilies(Path.Combine(
            dataRoot, "extracted", "rof", "ASSETS", "LAYOUT.CSV"));
        return families != null && families.TryGetValue(letter, out var family) ? family : null;
    }

    private static List<ScrapbookScrap> Filtered(
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

        return visible;
    }

    private static ScrapbookScrap Parse(string[] fields)
    {
        int objective = int.Parse(fields[0].Trim(), CultureInfo.InvariantCulture);
        string caption = fields[1].Trim();
        string imageName = fields[2].Trim();
        string imageType = fields[3].Trim();
        float x = float.Parse(fields[4].Trim(), CultureInfo.InvariantCulture);
        float y = float.Parse(fields[5].Trim(), CultureInfo.InvariantCulture);
        int drawOrder = int.Parse(fields[9].Trim(), CultureInfo.InvariantCulture);
        char zoom = fields[11].Trim() is { Length: 1 } letter ? letter[0] : '0';
        float zoomX = ParseFloat(fields[12]);
        float zoomY = ParseFloat(fields[13]);
        string title = fields[14].Trim();
        string text = fields[15].Trim();

        return new ScrapbookScrap(
            objective, imageName, ExtensionOf(imageType, 0), x, y, drawOrder,
            zoom, zoomX, zoomY, caption, title, text)
        {
            ZoomExtension = ExtensionOf(imageType, 1),
        };
    }

    // 7_1_2 (SB_07_01_ilsanote) ships ZoomX/ZoomY blank despite a real Zoom letter; not traced
    // further, so a blank field reads as 0 rather than throwing.
    private static float ParseFloat(string field) =>
        float.TryParse(field.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : 0f;

    // ImageType's letter at `index` (0 page, 1 zoom inset): B/b -> BMP, J/j -> JPG, anything else
    // (including a missing character) -> PNG, matching docs/formats/campaign-screens.md's rule.
    private static string ExtensionOf(string imageType, int index)
    {
        char letter = imageType.Length > index ? imageType[index] : '\0';
        return letter switch
        {
            'B' or 'b' => "BMP",
            'J' or 'j' => "JPG",
            _ => "PNG",
        };
    }

    // The [SCRAPBOOK] section as a flat key->fields map: comment (';') and section ('[') lines
    // skipped, everything else split on the key's '=' then the value's ',' -- quote-aware, since
    // the one quoted field (the clickable region, unread here) sits before the Zoom/ZoomX/ZoomY/
    // TitleResID/TextResID columns and would misalign every naive split past it.
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

                rows[trimmed[..eq]] = SplitRespectingQuotes(trimmed[(eq + 1)..]);
            }
        }

        Files[path] = rows;
        return rows;
    }

    // A comma split that treats one "..." run as a single field, unquoted -- this file's shape,
    // not general CSV (no escaped quotes inside a quoted field).
    private static string[] SplitRespectingQuotes(string value)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool quoted = false;
        foreach (char c in value)
        {
            if (c == '"')
            {
                quoted = !quoted;
            }
            else if (c == ',' && !quoted)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }

    // LAYOUT.CSV's SBZ_T_TITLE<letter>/CAPTION<letter>/TEXT<letter> rows, `Name=T,!,X,Y,?,Width,
    // Height,Colour,FontIndex`; only X, Y and Width are read (docs/formats/campaign-screens.md,
    // "Resolving a row to a file": two families' colour fields are typo'd and would not parse, and
    // a BoardLine has no colour column of its own to hand one to regardless).
    private static Dictionary<char, ScrapbookZoomFamily>? LoadZoomFamilies(string path)
    {
        if (ZoomFamilies.TryGetValue(path, out var cached))
        {
            return cached;
        }

        Dictionary<char, ScrapbookZoomFamily>? families = null;
        if (File.Exists(path))
        {
            var titles = new Dictionary<char, (float X, float Y, float W)>();
            var captions = new Dictionary<char, (float X, float Y, float W)>();
            var texts = new Dictionary<char, (float X, float Y, float W)>();
            foreach (var line in File.ReadAllLines(path))
            {
                string trimmed = line.Trim();
                int eq = trimmed.IndexOf('=');
                if (eq < 0)
                {
                    continue;
                }

                string key = trimmed[..eq].TrimEnd();
                Dictionary<char, (float X, float Y, float W)>? target = key switch
                {
                    _ when key.StartsWith("SBZ_T_TITLE", StringComparison.Ordinal) => titles,
                    _ when key.StartsWith("SBZ_T_CAPTION", StringComparison.Ordinal) => captions,
                    _ when key.StartsWith("SBZ_T_TEXT", StringComparison.Ordinal) => texts,
                    _ => null,
                };
                if (target == null || key[^1] is < 'A' or > 'Z')
                {
                    continue;
                }

                var fields = trimmed[(eq + 1)..].Split(',');
                if (fields.Length < 7)
                {
                    continue;
                }

                target[key[^1]] = (
                    float.Parse(fields[2].Trim(), CultureInfo.InvariantCulture),
                    float.Parse(fields[3].Trim(), CultureInfo.InvariantCulture),
                    float.Parse(fields[5].Trim(), CultureInfo.InvariantCulture));
            }

            families = new Dictionary<char, ScrapbookZoomFamily>();
            foreach (var letter in titles.Keys)
            {
                if (!captions.TryGetValue(letter, out var caption) || !texts.TryGetValue(letter, out var text))
                {
                    continue;
                }

                var title = titles[letter];
                families[letter] = new ScrapbookZoomFamily(
                    title.X, title.Y, title.W, caption.X, caption.Y, caption.W, text.X, text.Y, text.W);
            }
        }

        ZoomFamilies[path] = families;
        return families;
    }
}
