using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;

namespace CSVM.UI;

/// <summary>
/// The PAINT screen: the pattern, then the three paint colours, over a live preview composed
/// through the original's own region masks. The pattern row steps
/// <see cref="PatternLibrary.PatternsFor"/>'s per-aircraft list, since patterns are per aircraft
/// (docs/formats/paint.md); the three colour rows step an ordered palette built from the twelve
/// shipped schemes' own triples, so every colour a pilot can reach is one the original's artists
/// authored. The record's two composite picks and its third dword are carried untouched: nothing
/// consumes them yet and the <c>a*5 + b</c> encoding's meaning is open (docs/org/hangar.md).
/// </summary>
public sealed class HangarPaintPage : HangarPage
{
    /// <summary>The pattern row, then paint slots 1-3.</summary>
    public const int PatternRow = 0;

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

    // The twelve shipped schemes' colour triples in first-appearance order, deduplicated, with
    // the table's (0,0,0) entries written as the (25,25,25) the paint UI's darkest shade actually
    // saves (docs/formats/paint.md). Stepping a slot therefore only ever lands on an authored
    // colour; free RGB is the livery lab's business, not a hangar screen's.
    private static readonly PaintOption[] Palette =
    {
        new(223, 0, 41, "fortune 1"),
        new(25, 25, 25, "fortune 2, darkest shade"),
        new(255, 255, 255, "fortune 3"),
        new(243, 194, 0, "hughes 1"),
        new(177, 130, 66, "blackhat 1"),
        new(119, 74, 43, "blackhat 2"),
        new(66, 39, 15, "blackhat 3"),
        new(149, 163, 195, "blake 1"),
        new(89, 114, 159, "blake 2"),
        new(233, 228, 240, "blake 3"),
        new(48, 47, 39, "british 2"),
        new(23, 23, 21, "blckswan 1"),
        new(196, 193, 186, "blckswan 3"),
        new(57, 64, 68, "cccp 1"),
        new(245, 211, 0, "cccp 3"),
        new(108, 102, 169, "hollywd 1"),
        new(67, 36, 121, "hollywd 2"),
        new(212, 202, 225, "hollywd 3"),
        new(95, 125, 143, "medusas 1"),
        new(41, 14, 21, "medusas 2"),
        new(141, 137, 93, "medusas 3"),
        new(52, 38, 107, "sactrust 1"),
        new(96, 115, 126, "german 1"),
        new(32, 90, 167, "studio 1"),
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
    public override int RowCount => 4;

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
    public override string RowText(int row) => row switch
    {
        PatternRow => "Pattern: " + PatternLabel(Scratch.PaintPattern),
        _ => $"{SlotName(row)}: {ColourLabel(ColourOf(row))}",
    };

    /// <inheritdoc/>
    public override string Detail(int row)
    {
        if (row != PatternRow)
        {
            var colour = ColourOf(row);
            int at = PaletteIndex(colour);
            string source = at < 0 ? "not a shipped colour" : Palette[at].Source;
            return $"{colour.R},{colour.G},{colour.B}   {source}";
        }

        var roster = Roster();
        string where = Library.IsEmpty
            ? "no pattern library: the decoded 14-entry table"
            : $"{roster.Count} of 14 patterns ship masks for this airframe";
        int seat = roster.IndexOf(Scratch.PaintPattern);
        return seat < 0 ? $"{where}   (this one paints nothing here)" : $"{where}   {seat + 1}/{roster.Count}";
    }

    /// <inheritdoc/>
    public override bool Step(int row, int dir) =>
        row == PatternRow ? StepPattern(dir) : StepColour(row, dir);

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

    private static string SlotName(int row) => row switch
    {
        1 => "Paint 1 (body)",
        2 => "Paint 2 (dark trim)",
        _ => "Paint 3 (light trim)",
    };

    private static string ColourLabel(PaintColour colour)
    {
        int at = PaletteIndex(colour);
        return at < 0 ? $"custom {colour.R},{colour.G},{colour.B}" : Palette[at].Source;
    }

    private static int PaletteIndex(PaintColour colour)
    {
        for (int i = 0; i < Palette.Length; i++)
        {
            if (Palette[i].R == colour.R && Palette[i].G == colour.G && Palette[i].B == colour.B)
            {
                return i;
            }
        }

        return -1;
    }

    private static int Wrap(int at, int dir, int count) => ((at + dir) % count + count) % count;

    private PaintColour ColourOf(int row) => row switch
    {
        1 => Scratch.Colour1,
        2 => Scratch.Colour2,
        _ => Scratch.Colour3,
    };

    private string PatternLabel(int pattern)
    {
        string name = PatternName(pattern);
        return name.Length == 0 ? $"pattern {pattern}" : name.ToUpperInvariant();
    }

    // The patterns this airframe actually has masks for, as record indices, cached per airframe.
    // With no extraction there is no per-aircraft list to read, so the whole decoded table is
    // offered rather than a guess at which of it this plane carries.
    private List<int> Roster()
    {
        int airframe = Math.Clamp(Scratch.Airframe, 0, SkinPrefixes.Length - 1);
        if (_rosters.TryGetValue(airframe, out var cached))
        {
            return cached;
        }

        var roster = new List<int>();
        foreach (string folder in Library.PatternsFor(SkinPrefixes[airframe]))
        {
            int index = Array.FindIndex(PatternNames, n =>
                string.Equals(n, folder, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                roster.Add(index);
            }
        }

        if (roster.Count == 0)
        {
            for (int i = 0; i < PatternNames.Length; i++)
            {
                roster.Add(i);
            }
        }

        _rosters[airframe] = roster;
        return roster;
    }

    private bool StepPattern(int dir)
    {
        var roster = Roster();
        int at = roster.IndexOf(Scratch.PaintPattern);
        int next = at < 0 ? (dir > 0 ? 0 : roster.Count - 1) : Wrap(at, dir, roster.Count);
        if (roster[next] == Scratch.PaintPattern)
        {
            return false;
        }

        Scratch.PaintPattern = roster[next];
        return true;
    }

    // A colour outside the palette (an imported original save, a fresh plane's default) is kept
    // until the pilot steps it; the first step then lands on an authored colour rather than
    // hunting for the nearest one, which would silently rewrite what was imported.
    private bool StepColour(int row, int dir)
    {
        var current = ColourOf(row);
        int at = PaletteIndex(current);
        int next = at < 0 ? (dir > 0 ? 0 : Palette.Length - 1) : Wrap(at, dir, Palette.Length);
        var chosen = new PaintColour(Palette[next].R, Palette[next].G, Palette[next].B);
        if (chosen == current)
        {
            return false;
        }

        if (row == 1)
        {
            Scratch.Colour1 = chosen;
        }
        else if (row == 2)
        {
            Scratch.Colour2 = chosen;
        }
        else
        {
            Scratch.Colour3 = chosen;
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

    /// <summary>One palette entry: an authored colour and the scheme slot it came from.</summary>
    private readonly record struct PaintOption(byte R, byte G, byte B, string Source);
}
