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
    /// <summary>Named scheme (<c>hughes</c>, <c>blackhat</c>, <c>player_fortune</c>, …).
    /// The pattern table itself lives engine-side in the original and is NOT in any
    /// extracted file — we only ever see the name, so it is a label here.</summary>
    public string Pattern = "";

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
                Color2 = ReadColor(d, "paint_color2", Colors.White),
                Color3 = ReadColor(d, "paint_color3", Colors.White),
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

    // The Fortune Hunters red the original's paint UI shows for the player's own plane —
    // the pattern ships without colours (they come from the engine-side pattern table),
    // and this is the value read out of a saved .pln file at 0x68. See paint.md.
    private static readonly Color FortuneRed = FromBytes(223, 0, 41);

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
    public static PaintScheme Random(RandomNumberGenerator rng, IReadOnlyList<PaintScheme>? catalog)
    {
        // RandiRange, not Randi() % n: Randi returns a full uint, and casting it to int
        // overflows negative for half the range, which indexes out of the catalog.
        var basis = catalog is { Count: > 0 } ? catalog[rng.RandiRange(0, catalog.Count - 1)] : new PaintScheme();
        return new PaintScheme
        {
            Pattern = basis.Pattern,
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
