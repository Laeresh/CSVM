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
        // rather than a data one (verification.md DET-9).
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
            // authored shape — an integer test with no tolerance to argue about.
            bool box = true;
            for (int corner = 0; corner < 8 && box; corner++)
            {
                box = volume.Contains(volume.Box.GetEndpoint(corner));
            }
            if (box)
            {
                boxes++;
            }
        }

        Assert.Equal(expected, $"{volumes.Count}|{boxes}");
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
}
