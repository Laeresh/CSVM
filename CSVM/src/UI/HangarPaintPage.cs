using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;

namespace CSVM.UI;

/// <summary>
/// The PAINT screen, the original's own model: a pattern, three (colour, shade) index pairs and
/// three decals, over a live preview composed through the original's region masks. The pattern row
/// steps only the patterns this airframe's availability mask allows and loads that entry's six
/// colour/shade defaults on the way; a colour row steps the 27-row swatch table and resets its
/// slot's shade, a shade row walks that colour's own ramp, and the three decal rows step the
/// gapless 00-49 texture set. Every table is decoded data in <c>CSVM/data</c>
/// (docs/formats/paint.md, "The swatch table and the pattern defaults").
/// </summary>
public sealed class HangarPaintPage : HangarPage
{
    /// <summary>The pattern row; then a colour and a shade row per slot, then the three decals.</summary>
    public const int PatternRow = 0;

    /// <summary>The first decal row (nose); tail and wing follow.</summary>
    public const int NoseDecalRow = 7;

    // The pattern dropdown's labels are langui 3425 + pattern index (callback 2235).
    private const int PatternStringBase = 3425;

    // Pattern index 0-13 (record +0x40) is a row of the engine's 14-entry pattern-name table at
    // 0x0060301c, which is also the archive folder name. Read out of crimson.exe; the four
    // indices seven genuine saves pin (blckswan 1, fortune 4, hughes 6, studio 11) all match.
    private static readonly string[] PatternNames =
    {
        "blackhat", "blckswan", "blake", "british", "fortune", "hollywd", "hughes",
        "medusas", "cccp", "sactrust", "german", "studio", "broadway", "itstaxi",
    };

    // Airframe id 0-10 to the skin-texture prefix its masks are named after. Confirmed against
    // the shipped icon sets: each PX_ICON_<af>_<pattern>_* set is exactly the pattern list
    // PatternsFor gives that prefix (itstaxi aside, which ships no icons at all).
    private static readonly string[] SkinPrefixes =
    {
        "agyro", "hel", "bal", "blo", "bri", "dev", "fir", "fur", "kes", "pea", "war",
    };

    // The skin to preview, first match wins: the wing carries all three slots on every aircraft
    // that has one, and the Hoplite (which has no wing skin) falls through to its fuselage.
    private static readonly string[] PreviewParts =
    {
        "_WING", "_WINGTOP", "_FUSALAGE1", "_FUSELAGE", "_FUSLAGE", "_FUSALAGETOP",
    };

    private readonly Dictionary<int, List<int>> _rosters = new();
    private readonly Dictionary<int, HangarArt?> _icons = new();
    private PatternLibrary? _library;
    private HangarArt? _art;
    private (int Airframe, int Pattern, PaintColour C1, PaintColour C2, PaintColour C3)? _artKey;

    /// <summary>Binds the page to its flow.</summary>
    public HangarPaintPage(HangarFlow flow)
        : base(flow)
    {
    }

    /// <inheritdoc/>
    public override HangarScreen Screen => HangarScreen.Paint;

    /// <inheritdoc/>
    public override int RowCount => NoseDecalRow + HangarPaintTables.Slots;

    /// <summary>The preview of the scratch plane's own paint, recomposed whenever any of the five
    /// fields it draws from changes and handed over as the same image until then, so the shell
    /// rebuilds its texture on an edit and on nothing else.</summary>
    public override HangarArt? Art
    {
        get
        {
            var key = (Scratch.Airframe, Scratch.PaintPattern, Scratch.Colour1, Scratch.Colour2,
                Scratch.Colour3);
            if (_artKey is { } seen && seen.Equals(key))
            {
                return _art;
            }

            _artKey = key;
            _art = LivePreview() ?? IconFor(Scratch.PaintPattern);
            return _art;
        }
    }

    /// <summary>The swatch, pattern and decal tables every row on this screen reads.</summary>
    private static HangarPaintTables Tables => HangarPaintTables.Default;

    // The masks live under a folder named for the pattern; an absent extraction is a library with
    // no patterns rather than an error. Probed before loading because PatternLibrary reports a
    // missing folder through Godot, and this page stays engine-free.
    private PatternLibrary Library
    {
        get
        {
            if (_library != null)
            {
                return _library;
            }

            string? rof = Flow.DataRoot is { } root ? Path.Combine(root, "extracted", "rof") : null;
            _library = rof != null && Directory.Exists(Path.Combine(rof, "ASSETS", "GRAPHICS"))
                ? PatternLibrary.Load(rof)
                : PatternLibrary.Empty;
            return _library;
        }
    }

    /// <summary>The archive folder / table name for a pattern index, or "" outside 0-13.</summary>
    public static string PatternName(int pattern) =>
        pattern >= 0 && pattern < PatternNames.Length ? PatternNames[pattern] : string.Empty;

    /// <summary>The skin-texture prefix an airframe's masks are named after.</summary>
    public static string SkinPrefix(int airframe) =>
        SkinPrefixes[Math.Clamp(airframe, 0, SkinPrefixes.Length - 1)];

    /// <inheritdoc/>
    public override string RowText(int row)
    {
        if (row == PatternRow)
        {
            return "Pattern: " + PatternLabel(Scratch.PaintPattern);
        }

        if (row >= NoseDecalRow)
        {
            int decal = DecalOf(row);
            return $"{DecalSlotName(row)}: {DecalLabel(decal)}";
        }

        int slot = SlotOf(row);
        if (IsShadeRow(row))
        {
            var rgb = Scratch.PaintColourAt(slot);
            return $"Shade {slot + 1}: {Scratch.PaintShades[slot] + 1}/{ShadeCount(slot)}   {rgb.R},{rgb.G},{rgb.B}";
        }

        var chip = Tables.Resolve(Scratch.PaintColours[slot], Tables.DefaultShadeFor(Scratch.PaintColours[slot]));
        return $"{SlotName(slot)}: swatch {Scratch.PaintColours[slot]}   {chip.R},{chip.G},{chip.B}";
    }

    /// <inheritdoc/>
    public override string Detail(int row)
    {
        if (row == PatternRow)
        {
            var roster = Roster();
            int seat = roster.IndexOf(Scratch.PaintPattern);
            string where = $"{roster.Count} of {HangarPaintTables.PatternCount} patterns fit this airframe";
            return seat < 0 ? $"{where}   (this one does not)" : $"{where}   {seat + 1}/{roster.Count}";
        }

        if (row >= NoseDecalRow)
        {
            return row == NoseDecalRow
                ? "nose art, the 21-49 half of the set"
                : "squadron and nation logos, the 00-20 half";
        }

        int slot = SlotOf(row);
        var paint = Scratch.PaintColourAt(slot);
        return IsShadeRow(row)
            ? $"{paint.R},{paint.G},{paint.B}   the colour's own dark-to-light ramp"
            : $"{Tables.Swatches.Count} swatches   picking one resets this slot's shade to its default";
    }

    /// <inheritdoc/>
    public override bool Step(int row, int dir)
    {
        if (row == PatternRow)
        {
            return StepPattern(dir);
        }

        if (row >= NoseDecalRow)
        {
            return StepDecal(row, dir);
        }

        return IsShadeRow(row) ? StepShade(SlotOf(row), dir) : StepColour(SlotOf(row), dir);
    }

    // Exactly the original's per-texel composite (docs/formats/paint.md): the three mask weights
    // blend the three colours, the shading map modulates the result, the pattern's overlay goes
    // over that. ⚠ `.BM` rows are bottom-up, so destination row y reads source row h-1-y.
    private static byte[] Compose(PaintBitmap bm, PaintColour c1, PaintColour c2, PaintColour c3)
    {
        int w = bm.Width, h = bm.Height;
        var rgba = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = ((y * w) + x) * 4;
                int j = ((h - 1 - y) * w) + x;
                float w1 = bm.Slot1[j], w2 = bm.Slot2[j], w3 = bm.Slot3[j];
                int s = j * 3;
                float r = bm.Shading[s] * (((w1 * c1.R) + (w2 * c2.R) + (w3 * c3.R)) / 255f) / 255f;
                float g = bm.Shading[s + 1] * (((w1 * c1.G) + (w2 * c2.G) + (w3 * c3.G)) / 255f) / 255f;
                float b = bm.Shading[s + 2] * (((w1 * c1.B) + (w2 * c2.B) + (w3 * c3.B)) / 255f) / 255f;
                if (bm.HasOverlay)
                {
                    int o = j * 4;
                    float a = bm.Overlay![o + 3] / 255f;
                    r = (r * (1f - a)) + (bm.Overlay[o] * a);
                    g = (g * (1f - a)) + (bm.Overlay[o + 1] * a);
                    b = (b * (1f - a)) + (bm.Overlay[o + 2] * a);
                }

                rgba[i] = Round(r);
                rgba[i + 1] = Round(g);
                rgba[i + 2] = Round(b);
                rgba[i + 3] = 255;
            }
        }

        return rgba;
    }

    private static byte Round(float v) => (byte)Math.Clamp((int)(v + 0.5f), 0, 255);

    // The art seam speaks TgaImage, which has no factory for pixels a page composed itself and
    // is not this item's file to extend: wrapping the composite in a 32-bit top-down TGA is the
    // way in, and it runs the same decoder the blueprints already come through.
    private static TgaImage? AsImage(int width, int height, byte[] rgba)
    {
        var tga = new byte[18 + rgba.Length];
        tga[2] = 2;                                  // uncompressed truecolour
        tga[12] = (byte)(width & 0xff);
        tga[13] = (byte)(width >> 8);
        tga[14] = (byte)(height & 0xff);
        tga[15] = (byte)(height >> 8);
        tga[16] = 32;
        tga[17] = 0x28;                              // 8 alpha bits, top-down rows
        for (int p = 0; p < rgba.Length; p += 4)
        {
            tga[18 + p] = rgba[p + 2];
            tga[18 + p + 1] = rgba[p + 1];
            tga[18 + p + 2] = rgba[p];
            tga[18 + p + 3] = rgba[p + 3];
        }

        return TgaImage.Decode(tga);
    }

    private static string SlotName(int slot) => slot switch
    {
        0 => "Paint 1 (body)",
        1 => "Paint 2 (dark trim)",
        _ => "Paint 3 (light trim)",
    };

    private static string DecalSlotName(int row) => row switch
    {
        NoseDecalRow => "Nose decal",
        NoseDecalRow + 1 => "Tail decal",
        _ => "Wing decal",
    };

    // Rows 1-6 are colour, shade, colour, shade, colour, shade: each slot's pair together, the
    // way the original's screen pairs its two dropdowns per slot.
    private static bool IsShadeRow(int row) => row % 2 == 0;

    private static int SlotOf(int row) => (row - 1) / 2;

    private static int Wrap(int at, int dir, int count) => ((at + dir) % count + count) % count;

    private int ShadeCount(int slot) => Math.Max(1, Tables.ShadeCount(Scratch.PaintColours[slot]));

    private int DecalOf(int row) => row switch
    {
        NoseDecalRow => Scratch.NoseDecal,
        NoseDecalRow + 1 => Scratch.TailDecal,
        _ => Scratch.WingDecal,
    };

    private string DecalLabel(int decal)
    {
        if (decal < 0)
        {
            return "none (shipped placeholder)";
        }

        string name = Tables.DecalName(decal);
        return name.Length == 0 ? $"Decal {decal:00}" : name;
    }

    // The dropdown's own label (langui 3425 + index); the internal name, which is also the
    // pattern's archive folder, stands in when the string table is missing.
    private string PatternLabel(int pattern)
    {
        string name = PatternName(pattern);
        return Flow.Strings.Text(
            PatternStringBase + pattern,
            name.Length == 0 ? $"pattern {pattern}" : name.ToUpperInvariant());
    }

    // The patterns this airframe may wear, as record indices, from the pattern table's own
    // availability masks, cached per airframe. With no table loaded every pattern is offered,
    // which is the hangar's missing-data idiom rather than an empty screen.
    private List<int> Roster()
    {
        int airframe = Math.Clamp(Scratch.Airframe, 0, SkinPrefixes.Length - 1);
        if (_rosters.TryGetValue(airframe, out var cached))
        {
            return cached;
        }

        var roster = new List<int>();
        for (int pattern = 0; pattern < HangarPaintTables.PatternCount; pattern++)
        {
            if (Tables.Available(pattern, airframe))
            {
                roster.Add(pattern);
            }
        }

        _rosters[airframe] = roster;
        return roster;
    }

    // Selecting a pattern copies its six colour/shade defaults over the plane's own and touches
    // nothing else, which is all the original's SET handler does.
    private bool StepPattern(int dir)
    {
        var roster = Roster();
        if (roster.Count == 0)
        {
            return false;
        }

        int at = roster.IndexOf(Scratch.PaintPattern);
        int next = at < 0 ? (dir > 0 ? 0 : roster.Count - 1) : Wrap(at, dir, roster.Count);
        if (roster[next] == Scratch.PaintPattern)
        {
            return false;
        }

        Scratch.LoadPatternDefaults(roster[next]);
        return true;
    }

    private bool StepColour(int slot, int dir)
    {
        int count = Math.Max(1, Tables.Swatches.Count);
        int next = Wrap(Scratch.PaintColours[slot], dir, count);
        if (next == Scratch.PaintColours[slot])
        {
            return false;
        }

        Scratch.SetPaintColour(slot, next);
        return true;
    }

    private bool StepShade(int slot, int dir)
    {
        int count = ShadeCount(slot);
        int next = Wrap(Scratch.PaintShades[slot], dir, count);
        if (next == Scratch.PaintShades[slot])
        {
            return false;
        }

        Scratch.PaintShades[slot] = next;
        return true;
    }

    // A fresh build's slot holds the keep-the-placeholder sentinel, which the original cannot
    // store: the first step enters the 0-49 set, and the cycle stays inside it after that.
    private bool StepDecal(int row, int dir)
    {
        int current = DecalOf(row);
        int next = current < 0
            ? (dir > 0 ? 0 : HangarPaintTables.DecalCount - 1)
            : Wrap(current, dir, HangarPaintTables.DecalCount);
        if (next == current)
        {
            return false;
        }

        switch (row)
        {
            case NoseDecalRow: Scratch.NoseDecal = next; break;
            case NoseDecalRow + 1: Scratch.TailDecal = next; break;
            default: Scratch.WingDecal = next; break;
        }

        return true;
    }

    // The scratch plane's paint on its own airframe, composed from the pattern's masks. Null when
    // there is no extraction, or when the pattern ships no skin for this aircraft, which is the
    // only case the icon fallback exists for.
    private HangarArt? LivePreview()
    {
        var library = Library;
        if (library.IsEmpty)
        {
            return null;
        }

        int airframe = Math.Clamp(Scratch.Airframe, 0, SkinPrefixes.Length - 1);
        string folder = PatternName(Scratch.PaintPattern).ToUpperInvariant();
        foreach (string part in PreviewParts)
        {
            if (library.Skin(folder, SkinPrefixes[airframe] + part) is not { } bm)
            {
                continue;
            }

            var rgba = Compose(bm, Scratch.Colour1, Scratch.Colour2, Scratch.Colour3);
            if (AsImage(bm.Width, bm.Height, rgba) is { } image)
            {
                return new HangarArt(image, $"{PatternLabel(Scratch.PaintPattern)}   {Flow.AirframeName(airframe)}");
            }
        }

        return null;
    }

    // The shipped icon art, for the airframe/pattern pair. ⚠ The icon sets are sparse per pattern
    // (A2): only pattern 4 exists for every airframe, so a pattern with no set of its own falls
    // back to that one rather than showing nothing.
    private HangarArt? IconFor(int pattern)
    {
        int airframe = Math.Clamp(Scratch.Airframe, 0, SkinPrefixes.Length - 1);
        int key = (airframe * 100) + pattern;
        if (_icons.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var art = IconAt(airframe, pattern) ?? IconAt(airframe, 4);
        _icons[key] = art;
        return art;
    }

    private HangarArt? IconAt(int airframe, int pattern)
    {
        if (Flow.DataRoot is not { } root)
        {
            return null;
        }

        var path = Path.Combine(root, "extracted", "rof", "ASSETS", "GRAPHICS",
            $"PX_ICON_{airframe}_{pattern}_0.TGA");
        return TgaImage.TryLoad(path) is { } image
            ? new HangarArt(image, $"{PatternLabel(pattern)}   {Flow.AirframeName(airframe)}")
            : null;
    }

}
