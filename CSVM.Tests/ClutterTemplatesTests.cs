using System.Collections.Generic;
using System.IO;
using CSVM;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The <c>templates.zrd</c> reader (<see cref="ClutterTemplateSpec"/>): the per-decoration
/// properties the original's clutter stamper reads. Decode: docs/formats/templates.md,
/// docs/org/clutter.md. The grammar half runs on hand-authored lists for cases no chapter may
/// ship; the data half pins all eight chapters, since five of eleven keys being authored by
/// nobody (docs/org/clutter.md) is a fact only a whole-install census can see.
/// </summary>
public class ClutterTemplatesTests
{
    /// <summary>Per chapter: <see cref="ClutterTemplateSpec.Census"/>. Every number is measured off
    /// the shipped files. The five all-zero columns are the finding:
    /// no chapter authors <c>translate_uv_range</c>, <c>rotation_range</c>, <c>min_slope</c>,
    /// <c>max_slope</c> or <c>align_normal</c>, and none authors a damage block either.</summary>
    public static TheoryData<string, string> ChapterTemplateCensus => new()
    {
        { "C1", "blocks=5 kinds=5 duplicates=0 scale_range=5 far_fade_range=5 substitute=3" },
        { "C1B", "blocks=3 kinds=3 duplicates=0 scale_range=3 far_fade_range=3 substitute=0" },
        { "C1C", "blocks=0 kinds=0 duplicates=0 scale_range=0 far_fade_range=0 substitute=0" },
        { "C2", "blocks=50 kinds=50 duplicates=0 scale_range=50 far_fade_range=50 substitute=2" },
        { "C2B", "blocks=0 kinds=0 duplicates=0 scale_range=0 far_fade_range=0 substitute=0" },
        { "C3", "blocks=3 kinds=3 duplicates=0 scale_range=3 far_fade_range=3 substitute=1" },
        { "C4", "blocks=4 kinds=4 duplicates=0 scale_range=4 far_fade_range=4 substitute=1" },
        // C5's 78 blocks cover 77 distinct models: cb05det01.flt has two, and the FIRST — the one
        // carrying the substitute — is the one the engine's linear scan finds.
        { "C5", "blocks=78 kinds=77 duplicates=1 scale_range=78 far_fade_range=78 substitute=34" },
    };

    /// <summary>The tail of every chapter's census: the keys NO chapter authors. Split from the row
    /// above so the zero is asserted as its own statement rather than buried in a long string.</summary>
    public static TheoryData<string> EveryChapter => new()
    {
        "C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5",
    };

    [Fact]
    public void ABlocksKeysAreReadWithTheEnginesOwnUnitsAndGrouping()
    {
        var spec = ClutterTemplateSpec.Parse(new List<object?>
        {
            new List<object?>
            {
                "node", new List<object?> { "firtree1.flt" },
                "scale_range", new List<object?> { 0.9f, 1.5f },
                // Nested pairs are grouped by BOUND, not by band: [[nearMin, farMin], [nearMax,
                // farMax]] — the grouping the engine's own fade lerps prove.
                "far_fade_range", new List<object?>
                {
                    new List<object?> { 300f, 600f },
                    new List<object?> { 1000f, 2000f },
                },
                "translate_uv_range", new List<object?>
                {
                    new List<object?> { -0.25f, -0.5f },
                    new List<object?> { 0.25f, 0.5f },
                },
                "rotation_range", new List<object?>
                {
                    new List<object?> { 0f, -180f, 0f },
                    new List<object?> { 0f, 180f, 0f },
                },
                "min_slope", new List<object?> { 0f },
                "max_slope", new List<object?> { 60f },
                "align_normal",
            },
        });

        var kind = Assert.Single(spec.Kinds);
        Assert.Equal("firtree1.flt", kind.Node);
        Assert.Equal(new Vector2(0.9f, 1.5f), kind.ScaleRange);
        Assert.Equal(new Vector2(300f, 600f), kind.FarFadeMin);
        Assert.Equal(new Vector2(1000f, 2000f), kind.FarFadeMax);
        Assert.Equal(new Vector2(-0.25f, -0.5f), kind.TranslateUvMin);
        Assert.Equal(new Vector2(0.25f, 0.5f), kind.TranslateUvMax);
        // Degrees in the file, radians in the block.
        Assert.Equal(-Mathf.Pi, kind.RotationMin.Y, 5);
        Assert.Equal(Mathf.Pi, kind.RotationMax.Y, 5);
        Assert.Equal(0f, kind.RotationMin.X);
        // The inversion: min_slope 0° is the UPPER bound on the normal's Y (cos 0 = 1), max_slope
        // 60° the LOWER (cos 60 = 0.5). Naming them for the key would invert the test.
        Assert.Equal(1f, kind.NormalYMax, 5);
        Assert.Equal(0.5f, kind.NormalYMin, 5);
        Assert.True(kind.AlignNormal);
        Assert.Empty(kind.UnknownKeys);
    }

    [Fact]
    public void AnUnauthoredBlockIsEveryEngineDefault()
    {
        var spec = ClutterTemplateSpec.Parse(new List<object?>
        {
            new List<object?> { "node", new List<object?> { "bush1.flt" } },
        });

        // The engine's own initialiser, field for field — the state the whole install's
        // slope/jitter/rotation behaviour actually runs on, since no chapter authors those keys.
        var kind = Assert.Single(spec.Kinds);
        Assert.Equal(Vector2.One, kind.ScaleRange);
        Assert.Equal(Vector2.Zero, kind.FarFadeMin);
        Assert.Equal(Vector2.Zero, kind.FarFadeMax);
        Assert.Equal(Vector2.Zero, kind.TranslateUvMin);
        Assert.Equal(Vector3.Zero, kind.RotationMax);
        Assert.Equal(-1f, kind.NormalYMin);
        Assert.Equal(1f, kind.NormalYMax);
        Assert.False(kind.AlignNormal);
        Assert.Empty(kind.Substitutes);
        Assert.Null(kind.OnWeaponHit);
    }

    [Fact]
    public void SubstituteWeightsAreRelativeAndTheReaderNormalisesThem()
    {
        // The trap this exists for: 9.0/1.0 is 90/10, not "nine of something". The file never
        // normalises; the engine sums the list and divides as it stores.
        var spec = ClutterTemplateSpec.Parse(new List<object?>
        {
            new List<object?>
            {
                "node", new List<object?> { "firtree1.flt" },
                "substitute", new List<object?>
                {
                    new List<object?> { 9f, "firtree1.flt" },
                    new List<object?> { 1f, "firtree2.flt" },
                },
            },
        });

        var subs = Assert.Single(spec.Kinds).Substitutes;
        Assert.Equal(2, subs.Count);
        Assert.Equal(new ClutterSubstitute(9f, 0.9f, "firtree1.flt"), subs[0]);
        Assert.Equal(new ClutterSubstitute(1f, 0.1f, "firtree2.flt"), subs[1]);
    }

    [Fact]
    public void ADamageBlockIsArmedByItsHealthKeyAndCarriesItsAnimAndModel()
    {
        // Decoded from the binary and shipped by nothing — the reader exists so "no chapter
        // authors these" is a measurement over a reader that WOULD have read them.
        var spec = ClutterTemplateSpec.Parse(new List<object?>
        {
            new List<object?>
            {
                "node", new List<object?> { "cb00a.flt" },
                "OnWeaponHit", new List<object?>
                {
                    "health", new List<object?> { 50f },
                    "anim", new List<object?> { "cb00a_destroy" },
                    "model", new List<object?> { "cb00a_wreck.flt" },
                },
                "OnCollide", new List<object?> { "model", new List<object?> { "nothing.flt" } },
            },
        });

        var kind = Assert.Single(spec.Kinds);
        Assert.Equal(new ClutterDamageResponse(50f, "cb00a_destroy", "cb00a_wreck.flt"), kind.OnWeaponHit);
        Assert.Null(kind.OnCrater);
        Assert.Null(kind.OnCollide);   // no health key: the engine never arms it
    }

    [Fact]
    public void ADuplicateNodeResolvesToItsFirstBlock()
    {
        // C5's cb05det01.flt, in miniature. The engine's lookup is a linear scan of
        // the load order that stops at the first strcmp match, so the later block is unreachable —
        // and in C5 the unreachable one is the one WITHOUT the substitute.
        var spec = ClutterTemplateSpec.Parse(new List<object?>
        {
            new List<object?>
            {
                "node", new List<object?> { "cb05det01.flt" },
                "substitute", new List<object?> { new List<object?> { 5f, "cb05det02.flt" } },
            },
            new List<object?> { "node", new List<object?> { "cb05det01.flt" } },
        });

        Assert.Equal(2, spec.Kinds.Count);
        Assert.Equal("cb05det01.flt", Assert.Single(spec.DuplicateNodes));
        Assert.Single(spec.Find("cb05det01.flt")!.Substitutes);
    }

    [Fact]
    public void AnUnknownKeyIsKeptRatherThanSwallowed()
    {
        var spec = ClutterTemplateSpec.Parse(new List<object?>
        {
            new List<object?>
            {
                "node", new List<object?> { "bush1.flt" },
                "not_a_real_key", new List<object?> { 1f },
            },
        });

        Assert.Equal("not_a_real_key", Assert.Single(Assert.Single(spec.Kinds).UnknownKeys));
    }

    [Fact]
    public void AnEmptyFileIsAnEmptySpecAndAnAbsentOneIsNull()
    {
        // C1C and C2B ship the four bytes `null`, which is a chapter that authors nothing — a
        // different fact from "this chapter has no such file", and the two must not collapse.
        var dir = TestData.TempDir();
        File.WriteAllText(Path.Combine(dir, "templates.zrd.json"), "null");

        var empty = ClutterTemplateSpec.Load(dir);
        Assert.NotNull(empty);
        Assert.Empty(empty!.Kinds);
        Assert.Null(ClutterTemplateSpec.Load(TestData.TempDir()));
    }

    [ExtractedDataTheory]
    [MemberData(nameof(ChapterTemplateCensus))]
    public void EveryChaptersTemplateCensusIsWhatTheDataSays(string chapter, string expected)
    {
        var spec = ClutterTemplateSpec.Load(SessionPaths.ChapterZrdr(TestData.DataRoot!, chapter));

        Assert.NotNull(spec);
        Assert.StartsWith(expected, spec!.Census());
    }

    [ExtractedDataTheory]
    [MemberData(nameof(EveryChapter))]
    public void NoChapterAuthorsJitterRotationAlignmentOrASlopeCull(string chapter)
    {
        // The load-bearing negative, asserted per chapter: with none of these five authored, the
        // install has no random input affecting position or orientation (docs/formats/templates.md).
        var spec = ClutterTemplateSpec.Load(SessionPaths.ChapterZrdr(TestData.DataRoot!, chapter));

        Assert.NotNull(spec);
        Assert.EndsWith(
            "translate_uv_range=0 rotation_range=0 min_slope=0 max_slope=0 align_normal=0 "
            + "damage_blocks=0 unknown_keys=0",
            spec!.Census());
    }

    [ExtractedDataFact]
    public void TheInstallsAuthoredRangesAreMultipliersAndMetresRatherThanRadians()
    {
        // Bounds are the whole install's measured span, asserted as a band so this stays a units
        // check rather than a second census (e.g. scale_range 0.9-1.5 is plausible, radians is not).
        int kinds = 0, ordered = 0;
        float scaleLo = float.MaxValue, scaleHi = float.MinValue;
        float fadeLo = float.MaxValue, fadeHi = float.MinValue;
        foreach (var chapter in new[] { "C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5" })
        {
            var spec = ClutterTemplateSpec.Load(SessionPaths.ChapterZrdr(TestData.DataRoot!, chapter));
            Assert.NotNull(spec);
            foreach (var kind in spec!.Kinds)
            {
                kinds++;
                scaleLo = Mathf.Min(scaleLo, kind.ScaleRange.X);
                scaleHi = Mathf.Max(scaleHi, kind.ScaleRange.Y);
                fadeLo = Mathf.Min(fadeLo, Mathf.Min(kind.FarFadeMin.X, kind.FarFadeMin.Y));
                fadeHi = Mathf.Max(fadeHi, Mathf.Max(kind.FarFadeMax.X, kind.FarFadeMax.Y));
                // Every authored range runs low→high, and the near distance never outruns the far
                // one — the corroboration that the min-pair/max-pair grouping is the right one.
                if (kind.ScaleRange.X <= kind.ScaleRange.Y
                    && kind.FarFadeMin.X <= kind.FarFadeMax.X
                    && kind.FarFadeMin.Y <= kind.FarFadeMax.Y
                    && kind.FarFadeMax.X <= kind.FarFadeMax.Y)
                {
                    ordered++;
                }
            }
        }

        Assert.Equal(143, kinds);
        Assert.Equal(143, ordered);
        Assert.Equal(0.5f, scaleLo, 3);
        Assert.Equal(3.0f, scaleHi, 3);
        Assert.Equal(50f, fadeLo, 3);
        Assert.Equal(2000f, fadeHi, 3);
    }
}
