using System;
using System.Globalization;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The in-volume whiteout (<see cref="FogVolumeWhiteout"/>): the decompiled approach/interior
/// ramps, their union, the authored colour — and the exterior-distance helper they rest on
/// (<see cref="FogVolumeBox.ExteriorDistance"/>), pinned against closed-form distances so "the
/// projection converged" is a measurement rather than a hope.
///
/// <para>⚠ The interior ramp DECAYS inward: full AT the wall, 0 at
/// <c>interior_fog_fade_dist</c> deep. That is the decompiled shape and it
/// reads backwards on its own — the volume is a transition curtain, and <c>ZONE3</c>'s own fog is
/// what carries the interior look. A future session "fixing" it by inverting the
/// ramp fails these tests, which is the point of pinning both halves.</para>
///
/// <para>Fixture shapes follow <c>FogVolumeTests</c>'s precedent: hand-built
/// <see cref="FogVolumeBox"/>es with explicit outward planes for the rule, and the real extraction
/// for the per-chapter facts (which chapter arms it, and what the golden pose measures).</para>
/// </summary>
public class FogVolumeWhiteoutTests
{
    // A 100 m cube centred on the origin: walls at ±50 on every axis. Every ramp assertion below
    // is placed against ITS wall, so the numbers are readable without re-deriving the shape.
    private const float Half = 50f;

    [Fact]
    public void TheApproachRampIsFullAtTheWallHalfAtHalfFadeAndGoneBeyondIt()
    {
        var box = Cube();

        // Outside, straight out from the +X wall (a face's own Voronoi region, so the plane
        // distance and the hull distance agree — the edge/corner cases are pinned separately).
        Assert.Equal(1f, Density(box, new Vector3(Half, 0f, 0f)), 4);           // AT the wall
        Assert.Equal(0.75f, Density(box, new Vector3(Half + 4f, 0f, 0f)), 4);   // a quarter out
        Assert.Equal(0.5f, Density(box, new Vector3(Half + 8f, 0f, 0f)), 4);    // half fade
        Assert.Equal(0f, Density(box, new Vector3(Half + 16f, 0f, 0f)), 4);     // exactly at fade
        Assert.Equal(0f, Density(box, new Vector3(Half + 400f, 0f, 0f)), 4);    // far beyond it
    }

    [Fact]
    public void TheInteriorRampDecaysInwardFromTheWallRatherThanRisingIntoTheVolume()
    {
        var box = Cube();

        Assert.Equal(1f, Density(box, new Vector3(Half, 0f, 0f)), 4);           // AT the wall
        Assert.Equal(0.5f, Density(box, new Vector3(Half - 8f, 0f, 0f)), 4);    // half depth IN
        Assert.Equal(0f, Density(box, new Vector3(Half - 16f, 0f, 0f)), 4);     // at the decay depth
        Assert.Equal(0f, Density(box, Vector3.Zero), 4);                        // deep inside

        // The able-to-fail control the ⚠ above is about: an inverted ramp would read 0 at the wall
        // and 1 deep inside, so assert the ORDER as well as the values.
        Assert.True(Density(box, new Vector3(Half - 2f, 0f, 0f))
                    > Density(box, new Vector3(Half - 14f, 0f, 0f)));
    }

    [Fact]
    public void PenetrationIsMeasuredToTheNEARESTWallNotTheFarthest()
    {
        // A slab 40 m thick in Y and 2 km wide in X/Z: a point 4 m under the top is 4 m deep, not
        // 36 m deep and not 1000 m deep. Taking the max over planes of -signed (the FARTHEST wall)
        // instead of the min would read this point as 1000 m in and render nothing anywhere.
        var slab = BoxVolume("slab", new Vector3(-1000f, 0f, -1000f), new Vector3(1000f, 40f, 1000f));

        Assert.Equal(0.75f, Density(slab, new Vector3(0f, 36f, 0f)), 4);   // 4 m under the top
        Assert.Equal(0.75f, Density(slab, new Vector3(0f, 4f, 0f)), 4);    // 4 m over the floor
        Assert.Equal(0f, Density(slab, new Vector3(0f, 20f, 0f)), 4);      // mid-slab, 20 m from both
    }

    [Fact]
    public void AZeroLengthRampNeitherDividesByZeroNorPaintsTheWholeWorld()
    {
        var box = Cube();

        // fog_fade_dist 0 — no approach at all; interior_fog_fade_dist 0 — the wall only.
        Assert.Equal(0f, FogVolumeWhiteout.VolumeDensity(box, new Vector3(Half + 0.5f, 0f, 0f), 0f, 0f));
        Assert.Equal(1f, FogVolumeWhiteout.VolumeDensity(box, new Vector3(Half, 0f, 0f), 0f, 0f));
        Assert.Equal(0f, FogVolumeWhiteout.VolumeDensity(box, Vector3.Zero, 0f, 0f));
    }

    [Fact]
    public void TwoVolumesUnionAsTheBinaryDoesRatherThanTakingTheNearer()
    {
        // Two cubes 100 m apart in X, the point midway between their facing walls — 8 m from each
        // wall at a 16 m fade, so each contributes exactly 0.5. a + b - a·b = 0.75; picking the
        // nearer volume would give 0.5 and adding them would give 1.0, so the case separates all
        // three readings at once.
        var west = BoxVolume("west", new Vector3(-108f, -50f, -50f), new Vector3(-8f, 50f, 50f));
        var east = BoxVolume("east", new Vector3(8f, -50f, -50f), new Vector3(108f, 50f, 50f));
        var whiteout = Armed(new[] { west, east });

        Assert.Equal(0.5f, Density(west, Vector3.Zero), 4);
        Assert.Equal(0.5f, Density(east, Vector3.Zero), 4);
        Assert.Equal(0.75f, whiteout.Density(Vector3.Zero), 4);
    }

    [Fact]
    public void ADisarmedChapterWhiteoutsNothingEvenStandingInsideAVolume()
    {
        // C1's shape: nine volumes, fog_zone 0. The camera inside one of them is a state-1 camera
        // in clear air — the whole reason fog_zone is a bool and not a geometry test.
        var spec = FogVolumeSpec.Parse(new System.Collections.Generic.List<object?>
        {
            "fog_zone", new System.Collections.Generic.List<object?> { 0f },
        });
        var whiteout = FogVolumeWhiteout.From(spec, new[] { Cube() });

        Assert.False(whiteout.Armed);
        Assert.Equal(0f, whiteout.Density(Vector3.Zero));
        Assert.Equal(0f, whiteout.Density(new Vector3(Half, 0f, 0f)));
        Assert.Equal(0f, FogVolumeWhiteout.Disarmed.Density(Vector3.Zero));
    }

    [Fact]
    public void TheAuthoredColourIsNormalisedAndAnAbsentOneDefersToTheMission()
    {
        var volumes = new[] { Cube() };

        var authored = FogVolumeWhiteout.From(
            FogVolumeSpec.Parse(new System.Collections.Generic.List<object?>
            {
                "fog_zone", new System.Collections.Generic.List<object?> { 1f },
                "fog_color", new System.Collections.Generic.List<object?> { 16f, 32f, 48f },
            }),
            volumes);

        // A 0-255 triple, normalised the way every other weather colour is (WeatherState.ParseColor)
        // — 16/255, not 16.
        Assert.NotNull(authored.Color);
        Assert.Equal(16f / 255f, authored.Color!.Value.R, 5);
        Assert.Equal(32f / 255f, authored.Color!.Value.G, 5);
        Assert.Equal(48f / 255f, authored.Color!.Value.B, 5);
        // And the engine's own defaults where the arming file omits the distances.
        Assert.Equal(FogVolumeWhiteout.DefaultFadeDist, authored.FadeDist);
        Assert.Equal(FogVolumeWhiteout.DefaultInteriorFadeDist, authored.InteriorFadeDist);

        // No fog_color: null, so WeatherRig falls back to the mission's CLOUD_COVER TOP_COLOR
        // (the engine's own default) rather than this chapter-scope object reaching for mission data.
        var bare = FogVolumeWhiteout.From(
            FogVolumeSpec.Parse(new System.Collections.Generic.List<object?>
            {
                "fog_zone", new System.Collections.Generic.List<object?> { 1f },
            }),
            volumes);
        Assert.True(bare.Armed);
        Assert.Null(bare.Color);
    }

    [Fact]
    public void ExteriorDistanceIsTheHullDistanceNotTheWorstFacePlane()
    {
        var box = Cube();

        // Face region: the two agree.
        Assert.Equal(10f, box.ExteriorDistance(new Vector3(Half + 10f, 0f, 0f)), 3);
        Assert.Equal(10f, box.SignedDistance(new Vector3(Half + 10f, 0f, 0f)), 3);

        // Edge region: the true distance is sqrt(2)·10 = 14.142, the face planes say 10 — a 29 %
        // under-read that would put the ramp a third too strong.
        var edge = new Vector3(Half + 10f, Half + 10f, 0f);
        Assert.Equal(MathF.Sqrt(2f) * 10f, box.ExteriorDistance(edge), 3);
        Assert.Equal(10f, box.SignedDistance(edge), 3);

        // Corner region: sqrt(3)·10 = 17.32 against the planes' 10 — 42 % under.
        var corner = new Vector3(Half + 10f, Half + 10f, Half + 10f);
        Assert.Equal(MathF.Sqrt(3f) * 10f, box.ExteriorDistance(corner), 3);

        // Inside is 0, and the wall itself is 0 — the ramps' shared boundary.
        Assert.Equal(0f, box.ExteriorDistance(Vector3.Zero));
        Assert.Equal(0f, box.ExteriorDistance(new Vector3(Half, 0f, 0f)));
    }

    [Fact]
    public void ExteriorDistanceIsExactOnANonOrthogonalShapeToo()
    {
        // A square prism turned 45° in XZ (FogVolumeTests' own rotated fixture, scaled up): the
        // four side planes are NOT mutually orthogonal, so one projection pass is not the answer
        // and Dykstra's correction terms are what make it converge to the true closest point.
        const float S = 0.70710678f;
        var faces = new[]
        {
            new Plane(new Vector3(S, 0f, S), 10f),
            new Plane(new Vector3(S, 0f, -S), 10f),
            new Plane(new Vector3(-S, 0f, S), 10f),
            new Plane(new Vector3(-S, 0f, -S), 10f),
            new Plane(Vector3.Up, 10f),
            new Plane(Vector3.Down, 10f),
        };
        var prism = new FogVolumeBox("fvoltest", new Aabb(new Vector3(-15f, -10f, -15f), new Vector3(30f, 20f, 30f)), faces);

        // Straight out along +X: the diamond's own vertex sits at (10·sqrt2, 0, 0) = 14.142, so a
        // point at x = 30 is 15.858 away. The face planes read 30·S - 10 = 11.213 — 29 % under.
        var out1 = new Vector3(30f, 0f, 0f);
        Assert.Equal(30f - (10f * MathF.Sqrt(2f)), prism.ExteriorDistance(out1), 3);
        Assert.Equal((30f * S) - 10f, prism.SignedDistance(out1), 3);

        // Perpendicular to a side face, where the two DO agree — the able-to-fail half showing the
        // helper is not simply always larger.
        var out2 = new Vector3(S, 0f, S) * 15f;
        Assert.Equal(5f, prism.ExteriorDistance(out2), 3);
        Assert.Equal(5f, prism.SignedDistance(out2), 3);

        // A top corner: 5 m above the cap AND 15.858 past the vertex, so sqrt(5² + 15.858²).
        var out3 = new Vector3(30f, 15f, 0f);
        float expected = MathF.Sqrt((5f * 5f) + MathF.Pow(30f - (10f * MathF.Sqrt(2f)), 2f));
        Assert.Equal(expected, prism.ExteriorDistance(out3), 3);
    }

    [ExtractedDataTheory]
    [InlineData("C1", "disarmed")]
    [InlineData("C1B", "disarmed")]
    [InlineData("C1C", "disarmed")]
    [InlineData("C2", "disarmed")]
    [InlineData("C2B", "disarmed")]
    [InlineData("C3", "disarmed")]
    [InlineData("C4", "disarmed")]
    [InlineData("C5", "armed|17|16|16|101010")]
    public void OnlyC5ArmsTheInVolumeWhiteoutAndTheseAreItsAuthoredValues(string chapter, string expected)
    {
        var gamez = GameZ.Load(SessionPaths.ChapterGamez(TestData.DataRoot!, chapter));
        var spec = FogVolumeSpec.Load(SessionPaths.ChapterZrdr(TestData.DataRoot!, chapter));
        var volumes = FogVolumeSpec.VolumesOf(gamez);

        var whiteout = FogVolumeWhiteout.From(spec, volumes);

        // 101010 is 16,16,16 in hex — C5's fog_color, the same 16 its ZONE3 FOG_COLOR carries, so
        // the "whiteout" is very nearly a blackout and the hand-off to ZONE3's fog is
        // colour-continuous.
        string actual = whiteout.Armed
            ? $"armed|{volumes.Count}|"
              + $"{whiteout.FadeDist.ToString("0.##", CultureInfo.InvariantCulture)}|"
              + $"{whiteout.InteriorFadeDist.ToString("0.##", CultureInfo.InvariantCulture)}|"
              + $"{whiteout.Color?.ToHtml(false) ?? "default"}"
            : "disarmed";
        Assert.Equal(expected, actual);
    }

    [ExtractedDataFact]
    public void TheC5CityNightGoldenSitsFarOutsideEveryVolumeSoC21CannotMoveIt()
    {
        // analysis/goldens/manifest.json, shot `c5-city-night`: --pos=-9256,178,-3155. C5's approach
        // ramp is 16 m long and this camera is nowhere near a street strip, so the density is
        // exactly 0 and the only golden in a fog_zone chapter must stay byte-identical.
        var gamez = GameZ.Load(SessionPaths.ChapterGamez(TestData.DataRoot!, "C5"));
        var spec = FogVolumeSpec.Load(SessionPaths.ChapterZrdr(TestData.DataRoot!, "C5"));
        var volumes = FogVolumeSpec.VolumesOf(gamez);
        var whiteout = FogVolumeWhiteout.From(spec, volumes);
        var pose = new Vector3(-9256f, 178f, -3155f);

        float nearest = float.MaxValue;
        foreach (var volume in volumes)
        {
            nearest = MathF.Min(nearest, volume.ExteriorDistance(pose));
        }

        Assert.True(whiteout.Armed);
        Assert.Equal(0f, whiteout.Density(pose));
        // Stated with the measurement, not just the verdict: the margin is what says the prediction
        // is robust rather than a knife-edge (the ramp is 16 m).
        // Measured 1797.7 m — 112x the 16 m ramp, so the prediction is not a knife-edge. The bound
        // is written well under that so an authored change to the data fails LOUDLY here rather
        // than by moving a golden nobody re-attributes.
        Assert.True(nearest > 500f, $"nearest C5 volume is {nearest:0.0} m from the golden pose");
    }

    // fog_fade_dist 16 / interior_fog_fade_dist 16 — C5's own authored pair, so every ramp
    // assertion above is read at the scale the install actually ships.
    private static float Density(in FogVolumeBox volume, Vector3 point) =>
        FogVolumeWhiteout.VolumeDensity(volume, point, 16f, 16f);

    private static FogVolumeWhiteout Armed(FogVolumeBox[] volumes) => FogVolumeWhiteout.From(
        FogVolumeSpec.Parse(new System.Collections.Generic.List<object?>
        {
            "fog_zone", new System.Collections.Generic.List<object?> { 1f },
            "fog_fade_dist", new System.Collections.Generic.List<object?> { 16f },
            "interior_fog_fade_dist", new System.Collections.Generic.List<object?> { 16f },
        }),
        volumes);

    private static FogVolumeBox Cube() =>
        BoxVolume("cube", new Vector3(-Half, -Half, -Half), new Vector3(Half, Half, Half));

    // The same hand-built axis-aligned fixture FogVolumeTests uses: six outward unit-normal planes,
    // so Contains is an exact box test and the closed-form distances above are checkable by hand.
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
