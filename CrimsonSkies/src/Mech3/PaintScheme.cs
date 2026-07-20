using System;
using System.Collections.Generic;
using Godot;

namespace CrimsonSkies.Mech3;

/// <summary>
/// One aircraft paint scheme: a named pattern, three paint colours and three decal
/// indices — the seven-field record the original stores under the <c>paint_*</c> prefix
/// on a vehicle.json def, <c>ace_*</c> in a mission's ia.json, and binary in a saved
/// <c>.pln</c> file. See <c>docs/formats/paint.md</c>.
///
/// Colour triples in this record are ALWAYS integer 0–255 (unlike weather.json, which
/// mixes in a normalized-float encoding), so no >1 test is needed on read.
///
/// The colours are applied to the shipped skin textures by <see cref="PlanePainter"/>;
/// the decal indices name textures in the chapter archive's gapless 00–49 set.
/// </summary>
public sealed class PaintScheme
{
    /// <summary>Named pattern — and since 2026-07-20 this selects real data, not just a label:
    /// it names a folder of per-skin region masks in the UI resource archive (see
    /// <see cref="PatternLibrary"/> and <c>docs/formats/rof.md</c>), which is what decides
    /// WHERE the three colours go. Patterns are per aircraft: <c>FORTUNE</c> covers all
    /// eleven, the rest one to three, so a scheme is only meaningful together with a plane.
    /// vehicle.json spells the names lower-case (<c>hughes</c>, <c>player_fortune</c>) and the
    /// archive upper-case (<c>HUGHES</c>, <c>FORTUNE</c>); <see cref="FolderName"/> bridges them.</summary>
    public string Pattern = "";

    /// <summary>The archive folder this pattern's masks live in. vehicle.json's
    /// <c>player_fortune</c> is the archive's <c>FORTUNE</c>; every other name maps by
    /// upper-casing.</summary>
    public string FolderName => string.Equals(Pattern, "player_fortune", StringComparison.OrdinalIgnoreCase)
        ? "FORTUNE"
        : Pattern.ToUpperInvariant();

    /// <summary>Paint slot 1–3, applied to the skin's first/second/third keyed region
    /// (see <see cref="PlanePainter"/>). Slot 1 is the body.</summary>
    public Color Color1 = new(0.58f, 0.64f, 0.76f);
    public Color Color2 = Colors.White;
    public Color Color3 = Colors.White;

    /// <summary>Decal index into the chapter texture archive's 00–49 set; -1 leaves the
    /// plane's shipped placeholder in place. Slot 1 = nose, 2 = tail, 3 = wing (the paint
    /// UI's three dropdowns, in that order). Slot 1 draws from the 21–49 nose-art range,
    /// slots 2/3 from the 00–20 squadron-logo range.</summary>
    public int NoseDecal = -1;
    public int TailDecal = -1;
    public int WingDecal = -1;

    public Color ColorFor(int slot) => slot switch { 0 => Color1, 1 => Color2, _ => Color3 };

    /// <summary>Display label for HUD/menu use — the pattern name if the scheme came from
    /// the shipped catalog, else a colour summary.</summary>
    public string Label => string.IsNullOrEmpty(Pattern) ? "custom" : Pattern;

    public override string ToString() =>
        $"{Label} [{Fmt(Color1)} {Fmt(Color2)} {Fmt(Color3)}] decals {NoseDecal}/{TailDecal}/{WingDecal}";

    private static string Fmt(Color c) => $"{(int)Math.Round(c.R * 255)},{(int)Math.Round(c.G * 255)},{(int)Math.Round(c.B * 255)}";

    /// <summary>The 12 named schemes shipped in vehicle.json, deduplicated by pattern name
    /// (the <c>_2</c>/<c>_3</c>/<c>_5</c> per-chapter roster duplicates repeat their base
    /// def's scheme verbatim). Patterns carrying a name but no colours — <c>player_fortune</c>
    /// on <c>devastator</c>/<c>wingman</c>, whose colours live engine-side — are given the
    /// Fortune Hunters red/white/white seen in the original's paint UI so the catalog entry
    /// is usable. Ordered by first appearance in the file.</summary>
    public static List<PaintScheme> LoadCatalog(string zrdrPath)
    {
        var root = Zrdr.LoadFile(zrdrPath, "vehicle.json")[0] as List<object?>
            ?? throw new InvalidOperationException("vehicle.json: unexpected root shape");

        var byPattern = new Dictionary<string, PaintScheme>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        for (int i = 0; i + 1 < root.Count; i += 2)
        {
            if (root[i] is not string || root[i + 1] is not List<object?> props)
                continue;
            var d = ZrdrDict.FromAlternating(props);
            if (d.Str("paint_pattern") is not { } pattern)
                continue;
            // First def to name a pattern wins; later defs repeat it verbatim. A def that
            // carries colours beats an earlier colourless one (player_fortune).
            bool hasColors = d.Has("paint_color1");
            if (byPattern.TryGetValue(pattern, out var existing) && !(hasColors && existing.Color1 == FortuneRed))
                continue;
            if (!byPattern.ContainsKey(pattern))
                order.Add(pattern);
            byPattern[pattern] = new PaintScheme
            {
                Pattern = pattern,
                Color1 = ReadColor(d, "paint_color1", FortuneRed),
                Color2 = ReadColor(d, "paint_color2", FortuneTrim),
                Color3 = ReadColor(d, "paint_color3", FortuneFlash),
                NoseDecal = (int)d.Float("paint_decal1", 21f),
                TailDecal = (int)d.Float("paint_decal2", 7f),   // 07fhunter_logo1
                WingDecal = (int)d.Float("paint_decal3", 7f),
            };
        }

        var list = new List<PaintScheme>();
        foreach (var p in order)
            list.Add(byPattern[p]);
        return list;
    }

    // player_fortune ships a pattern name and NO colours (they come from wherever the engine
    // keeps its pattern defaults — still not located), so the catalog entry has to supply
    // them. Red from the paint UI's own swatch and a saved .pln at 0x68; black + white for
    // slots 2/3 read off the reference top view in
    // OriginalScreenshots/CustomPlane Paint1 Bloodhawk.png, where the Bloodhawk's outer wing
    // panels are BLACK and the swoosh dividing them WHITE. That is the same shape every
    // shipped scheme has (identity colour, dark trim, light trim) — `hughes` is
    // yellow/black/white — and rendering all three combinations against the reference singled
    // this one out: white/white loses the black wing entirely, black in slot 3 puts it on the
    // swoosh instead of the panel. See paint.md.
    private static readonly Color FortuneRed = FromBytes(223, 0, 41);
    private static readonly Color FortuneTrim = FromBytes(0, 0, 0);
    private static readonly Color FortuneFlash = FromBytes(255, 255, 255);

    private static Color ReadColor(ZrdrDict d, string key, Color fallback)
    {
        var l = d.List(key);
        if (l == null || l.Count < 3)
            return fallback;
        return FromBytes(Byte(l[0]), Byte(l[1]), Byte(l[2]));
    }

    private static int Byte(object? o) => o switch
    {
        long v => (int)v,
        int v => v,
        double v => (int)Math.Round(v),
        float v => (int)Math.Round(v),
        _ => 0,
    };

    /// <summary>Integer 0–255 triple → Color. The stored values are DX7-era sRGB, the same
    /// space the fullbright skin shader samples in, so no linearization here.</summary>
    public static Color FromBytes(int r, int g, int b) =>
        new(Mathf.Clamp(r, 0, 255) / 255f, Mathf.Clamp(g, 0, 255) / 255f, Mathf.Clamp(b, 0, 255) / 255f);

    /// <summary>A random livery: a named pattern from the catalog with randomized colours
    /// and decals. Deliberately NOT three independent random RGBs — that produces clown
    /// planes. The shipped twelve all follow one shape (see paint.md's table): slot 1 is the
    /// squadron's identity colour, slot 2 a dark trim and slot 3 a light one, so a random
    /// scheme is drawn the same way, with the catalog's own colours in the pool. Nose decals
    /// come from the 21–49 nose-art range and tail/wing from the 00–20 squadron logos,
    /// matching how every shipped def uses the three slots.</summary>
    /// <param name="patterns">The pattern names this aircraft actually has masks for
    /// (<see cref="PatternLibrary.PatternsFor"/>) — a pattern is per-plane, so drawing one the
    /// aircraft does not carry would paint nothing. Null/empty falls back to the catalog's
    /// names, which is only right when no library is loaded.</param>
    public static PaintScheme Random(RandomNumberGenerator rng, IReadOnlyList<PaintScheme>? catalog,
        IReadOnlyList<string>? patterns = null)
    {
        // RandiRange, not Randi() % n: Randi returns a full uint, and casting it to int
        // overflows negative for half the range, which indexes out of the catalog.
        var basis = catalog is { Count: > 0 } ? catalog[rng.RandiRange(0, catalog.Count - 1)] : new PaintScheme();
        string pattern = patterns is { Count: > 0 }
            ? patterns[rng.RandiRange(0, patterns.Count - 1)]
            : basis.Pattern;
        return new PaintScheme
        {
            Pattern = pattern,
            Color1 = IdentityColor(rng, catalog),
            Color2 = TrimColor(rng, catalog, dark: true),
            Color3 = TrimColor(rng, catalog, dark: false),
            NoseDecal = rng.RandiRange(21, 49),   // nose art
            TailDecal = rng.RandiRange(0, 20),    // squadron logos
            WingDecal = rng.RandiRange(0, 20),
        };
    }

    // Slot 1: half the time an actual shipped squadron colour, half a fresh hue held to the
    // saturation/value band those colours occupy (measured over the twelve: s 0.15–1.0,
    // v 0.09–0.95, but the vivid ones cluster around s 0.7 / v 0.75).
    private static Color IdentityColor(RandomNumberGenerator rng, IReadOnlyList<PaintScheme>? catalog)
    {
        if (catalog is { Count: > 0 } && rng.Randf() < 0.5f)
            return catalog[rng.RandiRange(0, catalog.Count - 1)].Color1;
        return Color.FromHsv(rng.Randf(), rng.RandfRange(0.45f, 0.95f), rng.RandfRange(0.42f, 0.9f));
    }

    // Slots 2/3: the trims. Mostly near-black / near-white neutrals, as in hughes
    // (black + white), blackhat, british, blckswan, german and studio; otherwise a muted
    // tint so the occasional coloured trim (cccp's red and yellow) stays reachable.
    private static Color TrimColor(RandomNumberGenerator rng, IReadOnlyList<PaintScheme>? catalog, bool dark)
    {
        float roll = rng.Randf();
        if (roll < 0.55f)
            return dark
                ? Color.FromHsv(rng.Randf(), rng.RandfRange(0f, 0.2f), rng.RandfRange(0.06f, 0.25f))
                : Color.FromHsv(rng.Randf(), rng.RandfRange(0f, 0.12f), rng.RandfRange(0.82f, 1f));
        if (catalog is { Count: > 0 } && roll < 0.8f)
        {
            var s = catalog[rng.RandiRange(0, catalog.Count - 1)];
            return dark ? s.Color2 : s.Color3;
        }
        return Color.FromHsv(rng.Randf(), rng.RandfRange(0.5f, 0.95f),
            dark ? rng.RandfRange(0.25f, 0.5f) : rng.RandfRange(0.7f, 0.95f));
    }
}
