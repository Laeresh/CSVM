using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The <c>fogvol.zrd</c> reader (<see cref="FogVolumeSpec"/>) and the gamez census it pairs with
/// (<see cref="FogVolumeSpec.VolumesOf"/>) — the two halves of the authored ambient cloud field
/// (docs/formats/fogvol.md).
///
/// <para>The grammar half runs on a hand-authored fixture; the data half pins all eight chapters,
/// because the interesting fact about this format is a per-chapter SPLIT: five chapters carry
/// volumes, a <c>clutter</c> key and <c>cloudsprite1</c>/<c>cloudsprite2</c> templates, and three
/// carry a degenerate copy naming a <c>cloudsprite</c> that exists in no chapter's gamez. A reader
/// that quietly turned the second group into the first would render clouds where the original has
/// none, and only a whole-install census can see that.</para>
/// </summary>
public class FogVolumeTests
{
    /// <summary>Per chapter: the fvol volume census, and the spec the reader gets. Format is
    /// "volumes|fog_zone|distance|clutter_key|blocks" then the resolved template names — so one
    /// row shows both the file and the geometry it needs, which is what the split turns on.</summary>
    public static TheoryData<string, string> ChapterFogVolumes => new()
    {
        { "C1", "9|0|130|keyed|cloudsprite1:present cloudsprite2:present" },
        { "C1B", "0|-|206.25|bare|cloudsprite:absent" },
        { "C1C", "21|0|130|keyed|cloudsprite1:present cloudsprite2:present" },
        { "C2", "0|-|206.25|bare|cloudsprite:absent" },
        { "C2B", "9|0|130|keyed|cloudsprite1:present cloudsprite2:present" },
        { "C3", "0|-|206.25|bare|cloudsprite:absent" },
        { "C4", "9|0|130|keyed|cloudsprite1:present cloudsprite2:present" },
        { "C5", "17|1|80|keyed|cloudsprite1:present cloudsprite2:present" },
    };

    /// <summary>Per chapter: "volumes|volumes whose authored shape IS their bounding box". The
    /// second number is the decode the scatter turns on — only C1/C2B/C4's map-spanning slabs are
    /// boxes, and filling the bounds of anything else puts cloud where the data authors none.
    /// C1C's nine slab volumes are boxes and its twelve build-ups are rotated tapering frusta;
    /// C5 has two boxes among seventeen, the rest being polygonal street prisms and three strips
    /// with a ramped top.</summary>
    public static TheoryData<string, string> ChapterFogVolumeShapes => new()
    {
        { "C1", "9|9" },
        { "C1B", "0|0" },
        { "C1C", "21|9" },
        { "C2", "0|0" },
        { "C2B", "9|9" },
        { "C3", "0|0" },
        { "C4", "9|9" },
        { "C5", "17|2" },
    };

    /// <summary>Per chapter: the map-spanning slab the map-edge continuation would extend, or
    /// "none". <c>cardHeight</c> is the authored card size docs/formats/fogvol.md documents
    /// (132.3 m for the four deck chapters, 70 m for C5) and 1.5 is
    /// <c>TopAnchorHeightFactor</c> — real per-chapter numbers, not a synthetic fixture. Bounds and
    /// top are the exact <c>fvol1</c> <c>model_bbox</c> values read from each chapter's own
    /// <c>extracted/&lt;ch&gt;/gamez/nodes.json</c> (C1/C2B's slabs and C4's share the world's own
    /// [-12288, 0] area to the metre — the exact 3x3 partition).</summary>
    public static TheoryData<string, float, string> ChapterMapSpanningSlab => new()
    {
        { "C1", 132.3f, "found|-12288|0|-12288|0|1090.55" },
        { "C1B", 206.25f, "none" },
        { "C1C", 132.3f, "found|-12288|0|-12288|0|1091.28" },
        { "C2", 206.25f, "none" },
        { "C2B", 132.3f, "found|-12288|0|-12288|0|1090.55" },
        { "C3", 206.25f, "none" },
        { "C4", 132.3f, "found|-12288|0|-12288|0|1180.55" },
        { "C5", 70.0f, "none" },
    };

    [Fact]
    public void TheHeaderKeysAndTheWeightedClutterBlockAreRead()
    {
        var spec = FogVolumeSpec.Load(TestData.Fixture("zrdr"));

        Assert.NotNull(spec);
        Assert.Equal(0, spec!.FogZone);
        Assert.Equal(100f, spec.Distance);
        Assert.Equal(16f, spec.FogFadeDist);
        Assert.Equal(12f, spec.InteriorFogFadeDist);
        Assert.Equal(new Vector3(16f, 32f, 48f), spec.FogColor);
        Assert.True(spec.HasClutterKey);

        var block = Assert.Single(spec.Clutter);
        Assert.Equal(3f, block.Weight);
        Assert.Equal(
            new[] { new FogClutterNode(1f, "testsprite1"), new FogClutterNode(2f, "testsprite2") },
            block.Nodes);
        // Both fade bands are kept; the renderer draws the farther one (no reduced-detail mode).
        Assert.Equal(new Vector2(1000f, 1500f), block.FarFadeNear);
        Assert.Equal(new Vector2(2000f, 2500f), block.FarFade);
        Assert.Equal(new Vector2(-5f, 10f), block.PerpDistRange);
        Assert.Equal(new Vector2(10f, 20f), block.PerturbDistRange);
        Assert.Equal(new Vector2(0.5f, 1.5f), block.ScaleRange);
    }

    [Fact]
    public void AClutterBlockWithNoClutterKeyIsStillRead()
    {
        // C1B/C2/C3's shape: the block sits in the root list with no key in front of it, which
        // ZrdrDict would drop as a stray value. Reading it is what lets the "renders nothing"
        // outcome be PROVEN by the template lookup instead of hidden by the parser.
        var spec = FogVolumeSpec.Parse(new List<object?>
        {
            "distance",
            new List<object?> { 206.25f },
            new List<object?>
            {
                "weight", new List<object?> { 1f },
                "nodes", new List<object?> { new List<object?> { 1f, "cloudsprite" } },
            },
        });

        Assert.False(spec.HasClutterKey);
        Assert.Equal(206.25f, spec.Distance);
        Assert.Null(spec.FogZone);
        Assert.Equal("cloudsprite", Assert.Single(Assert.Single(spec.Clutter).Nodes).Node);
    }

    [Fact]
    public void AClutterBlockNamingNoNodeIsDropped()
    {
        // A block with nothing to place is not a block: dropping it here keeps the weighted draw
        // in FogVolumeClutter from having to defend against a zero-alternative entry.
        var spec = FogVolumeSpec.Parse(new List<object?>
        {
            "clutter",
            new List<object?> { new List<object?> { "weight", new List<object?> { 1f } } },
        });

        Assert.True(spec.HasClutterKey);
        Assert.Empty(spec.Clutter);
    }

    [Fact]
    public void AnAbsentFileIsNullRatherThanAThrow()
    {
        Assert.Null(FogVolumeSpec.Load(TestData.TempDir()));
    }

    [ExtractedDataTheory]
    [MemberData(nameof(ChapterFogVolumes))]
    public void EveryChapterFogVolumeCensusIsWhatTheDataSays(string chapter, string expected)
    {
        var gamez = GameZ.Load(SessionPaths.ChapterGamez(TestData.DataRoot!, chapter));
        var spec = FogVolumeSpec.Load(SessionPaths.ChapterZrdr(TestData.DataRoot!, chapter));
        var volumes = FogVolumeSpec.VolumesOf(gamez);

        Assert.NotNull(spec);
        var templates = new List<string>();
        foreach (var block in spec!.Clutter)
        {
            foreach (var node in block.Nodes)
            {
                templates.Add(ClutterBuilder.FindTemplateRoot(gamez, node.Node) == null
                    ? $"{node.Node}:absent"
                    : $"{node.Node}:present");
            }
        }

        // Invariant formatting: a German decimal comma would make the row a machine property
        // rather than a data one.
        Assert.Equal(
            expected,
            $"{volumes.Count}|{spec.FogZone?.ToString(CultureInfo.InvariantCulture) ?? "-"}|"
            + $"{spec.Distance.ToString("0.##", CultureInfo.InvariantCulture)}|"
            + $"{(spec.HasClutterKey ? "keyed" : "bare")}|{string.Join(" ", templates)}");
    }

    [ExtractedDataTheory]
    [MemberData(nameof(ChapterFogVolumeShapes))]
    public void HalfTheInstallsFogVolumesAreNotTheirBoundingBox(string chapter, string expected)
    {
        var gamez = GameZ.Load(SessionPaths.ChapterGamez(TestData.DataRoot!, chapter));
        var volumes = FogVolumeSpec.VolumesOf(gamez);

        int boxes = 0;
        foreach (var volume in volumes)
        {
            // A volume IS its bounding box exactly when every corner of that box is inside the
            // authored shape — an integer test with no tolerance to argue about. Same predicate
            // the map-edge continuation uses to pick slab candidates (FindMapSpanningSlab).
            if (volume.IsAxisAlignedBox())
            {
                boxes++;
            }
        }

        Assert.Equal(expected, $"{volumes.Count}|{boxes}");
    }

    [ExtractedDataTheory]
    [MemberData(nameof(ChapterMapSpanningSlab))]
    public void TheMapSpanningSlabIsFoundOnlyForTheFourFlatDeckChapters(
        string chapter, float cardHeight, string expected)
    {
        var gamez = GameZ.Load(SessionPaths.ChapterGamez(TestData.DataRoot!, chapter));
        var volumes = FogVolumeSpec.VolumesOf(gamez);

        var (slab, skipReason) = FogVolumeSpec.FindMapSpanningSlab(volumes, cardHeight, 1.5f);

        string actual = slab is { } s
            ? $"found|{s.MinX.ToString("0.##", CultureInfo.InvariantCulture)}|"
              + $"{s.MaxX.ToString("0.##", CultureInfo.InvariantCulture)}|"
              + $"{s.MinZ.ToString("0.##", CultureInfo.InvariantCulture)}|"
              + $"{s.MaxZ.ToString("0.##", CultureInfo.InvariantCulture)}|"
              + $"{s.TopY.ToString("0.##", CultureInfo.InvariantCulture)}"
            : "none";
        Assert.Equal(expected, actual);
        // No shipped chapter's slab candidates disagree on their top or fail to tile exactly —
        // both failure branches are exercised only by the synthetic fixtures below.
        Assert.Null(skipReason);
    }

    [Fact]
    public void TwoAxisAlignedTopAnchoredPiecesThatTileExactlyAreFoundAsOneSlab()
    {
        // Two 1000x1000 boxes side by side in X, sharing the seam at x=0, both 50 m thick (a
        // "sheet" against a 132.3 m card) at the same top altitude — C1's nine slab pieces,
        // shrunk to two for a hand-checkable fixture.
        var west = BoxVolume("west", new Vector3(-1000, 900, -500), new Vector3(0, 950, 500));
        var east = BoxVolume("east", new Vector3(0, 900, -500), new Vector3(1000, 950, 500));

        var (slab, reason) = FogVolumeSpec.FindMapSpanningSlab(
            new[] { west, east }, cardHeight: 132.3f, topAnchorHeightFactor: 1.5f);

        Assert.Null(reason);
        Assert.True(slab.HasValue);
        var s = slab!.Value;
        Assert.Equal(-1000f, s.MinX);
        Assert.Equal(1000f, s.MaxX);
        Assert.Equal(-500f, s.MinZ);
        Assert.Equal(500f, s.MaxZ);
        Assert.Equal(950f, s.TopY);
    }

    [Fact]
    public void APieceThatLeavesAGapFailsTheTilingCheckAndReportsWhy()
    {
        var west = BoxVolume("west", new Vector3(-1000, 900, -500), new Vector3(0, 950, 500));
        // A 10 m gap at the seam instead of sharing it: the combined bounding rectangle is still
        // 2000x1000, but the pieces now cover only 1990x1000 of it.
        var east = BoxVolume("east", new Vector3(10, 900, -500), new Vector3(1000, 950, 500));

        var (slab, reason) = FogVolumeSpec.FindMapSpanningSlab(new[] { west, east }, 132.3f, 1.5f);

        Assert.Null(slab);
        Assert.NotNull(reason);
        Assert.Contains("do not exactly tile", reason);
    }

    [Fact]
    public void APieceWithADifferentTopIsReportedRatherThanAveraged()
    {
        var west = BoxVolume("west", new Vector3(-1000, 900, -500), new Vector3(0, 950, 500));
        var east = BoxVolume("east", new Vector3(0, 900, -500), new Vector3(1000, 955, 500)); // 5 m off

        var (slab, reason) = FogVolumeSpec.FindMapSpanningSlab(new[] { west, east }, 132.3f, 1.5f);

        Assert.Null(slab);
        Assert.NotNull(reason);
        Assert.Contains("disagrees", reason);
    }

    [Fact]
    public void ATallBoxIsExcludedByTheTopAnchoredTestEvenThoughItIsAnAxisAlignedBox()
    {
        // A 1000x1000 sheet plus a much taller box off to the side (a C1C build-up stand-in,
        // except axis-aligned): the tall one fails TOP-ANCHORED and must not join the slab, even
        // though it passes IsAxisAlignedBox on its own — the classification composes.
        var sheet = BoxVolume("sheet", new Vector3(-1000, 900, -500), new Vector3(1000, 950, 500));
        var tower = BoxVolume("tower", new Vector3(2000, 900, -500), new Vector3(2500, 1500, 500));

        var (slab, reason) = FogVolumeSpec.FindMapSpanningSlab(new[] { sheet, tower }, 132.3f, 1.5f);

        Assert.Null(reason);
        Assert.True(slab.HasValue);
        var s = slab!.Value;
        Assert.Equal(-1000f, s.MinX);
        Assert.Equal(1000f, s.MaxX); // the tower's footprint is not part of the slab
    }

    [Fact]
    public void NoTopAnchoredBoxVolumesMeansNoSlabAndNoSkipReason()
    {
        var (slab, reason) = FogVolumeSpec.FindMapSpanningSlab(Array.Empty<FogVolumeBox>(), 132.3f, 1.5f);

        Assert.Null(slab);
        Assert.Null(reason);
    }

    [Fact]
    public void ARotatedVolumeExcludesTheCornersOfItsBoundingBox()
    {
        // A square prism turned 45° in the XZ plane: its bounding box is 2x2, its own footprint is
        // the diamond inscribed in it. Nothing here is chapter data — it is the property the
        // scatter depends on, stated where it cannot silently stop holding.
        const float S = 0.70710678f;
        var faces = new[]
        {
            new Plane(new Vector3(S, 0f, S), 1f),
            new Plane(new Vector3(S, 0f, -S), 1f),
            new Plane(new Vector3(-S, 0f, S), 1f),
            new Plane(new Vector3(-S, 0f, -S), 1f),
            new Plane(Vector3.Up, 1f),
            new Plane(Vector3.Down, 1f),
        };
        var volume = new FogVolumeBox("fvoltest", new Aabb(new Vector3(-1f, -1f, -1f),
            new Vector3(2f, 2f, 2f)), faces);

        Assert.True(volume.Contains(Vector3.Zero));
        Assert.True(volume.Contains(new Vector3(0.9f, 0.9f, 0f)));
        Assert.False(volume.Contains(new Vector3(0.9f, 0f, 0.9f)));   // a bounding-box corner
        Assert.False(volume.Contains(new Vector3(0f, 1.5f, 0f)));     // above the top face
    }

    // A hand-built axis-aligned FogVolumeBox: the 6 outward-facing unit-normal planes of [min,
    // max], so IsAxisAlignedBox() is trivially true and Contains() is an exact box test — the
    // fixture shape FindMapSpanningSlab's synthetic tests above compose.
    private static FogVolumeBox BoxVolume(string name, Vector3 min, Vector3 max) => new(
        name,
        new Aabb(min, max - min),
        new[]
        {
            new Plane(new Vector3(1, 0, 0), max.X),
            new Plane(new Vector3(-1, 0, 0), -min.X),
            new Plane(new Vector3(0, 1, 0), max.Y),
            new Plane(new Vector3(0, -1, 0), -min.Y),
            new Plane(new Vector3(0, 0, 1), max.Z),
            new Plane(new Vector3(0, 0, -1), -min.Z),
        });
}
