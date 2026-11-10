using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using CSVM.Effects;
using CSVM.Session.World;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The shipped Enhanced lighting tune (<see cref="LightingTable"/>) and its block
/// (<see cref="ZoneLighting"/>). It ships with no entry, so every zone lights untuned. A mission block
/// reads through its chapter's, and a mission's night flag marks a moonlit mission. A mistake is a
/// warning rather than a crash, and a write keeps the file sparse. The engine suites
/// <c>moonlit-mission</c> and <c>lighting-lab</c> hold what a tune lights.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class LightingTableTests
{
    private static string ShippedPath => Path.Combine(TestData.RepoRoot, "CSVM", "data", "lighting.json");

    [Fact]
    public void TheShippedFileReadsClean()
    {
        var table = LightingTable.Parse(File.ReadAllText(ShippedPath));
        Assert.Empty(table.Warnings);
        Assert.Equal(ZoneLighting.Default, table.For("NOCHAPTER", "M01", "zone1"));
    }

    [Fact]
    public void TheDefaultsAreTheUntunedEnhancedLook()
    {
        var d = ZoneLighting.Default;
        Assert.Equal((1.07f, 1.8f, 1f, 6f), (d.SunEnergy, d.AmbientEnergy, d.Exposure, d.White));
        Assert.Equal((0.05f, 1.25f, 0.03f, 0.12f, 0.42f, 0.8f), (d.ShadowBias, d.NormalBias, d.Split1, d.Split2, d.Split3, d.FadeStart));
        Assert.Equal((1.25f, 1.1f, 1.15f), (d.GradeAgxContrast, d.GradeContrast, d.GradeSaturation));
        Assert.Equal((1f, 1f, 1f), (d.CloudR, d.CloudG, d.CloudB));
        Assert.Equal((1f, 1f, 1f), (d.SkyR, d.SkyG, d.SkyB));
        Assert.Equal((1f, 1f, 1f, 1f, 0.5f), (d.ShadowStrength, d.GodRays, d.AmbientFloor, d.SelfLitEmission, d.SdfgiEnergy));
        Assert.Equal(d, LightingTable.Parse("{}").For("C1", "M01", "zone1", LightingTable.Layers));
        Assert.False(d.TurnsSun);
        Assert.False(d.TunesEnvironment);
        Assert.True((d with { GradeSaturation = 1.3f }).TunesEnvironment);
        Assert.Equal("defaults", d.Describe());
    }

    [Fact]
    public void AMissionBlockReadsThroughItsChapter()
    {
        var table = LightingTable.Parse("""
            { "_about": "prose",
              "c5": { "ZONE1": { "exposure": 1.2, "sun_energy": 1.4, "grade_saturation": 1.3 },
                      "m02": { "night": true, "zone1": { "sun_energy": 0.9, "grade_contrast": 1.2 } } } }
            """);
        Assert.Empty(table.Warnings);
        Assert.Equal((1.25f, 1.2f, 1.3f), (table.For("C5", "M02", "zone1").GradeAgxContrast,
            table.For("C5", "M02", "zone1").GradeContrast, table.For("C5", "M02", "zone1").GradeSaturation));
        Assert.Equal(1.1f, table.For("C5", "IA1", "zone1").GradeContrast);
        var tinted = LightingTable.Parse("""{ "C1": { "ZONE2": { "cloud_b": 0.5 }, "M01": { "ZONE2": { "cloud_r": 0.3 } } } }""");
        Assert.Empty(tinted.Warnings);
        Assert.Equal((0.3f, 1f, 0.5f), (tinted.For("C1", "M01", "zone2").CloudR, tinted.For("C1", "M01", "zone2").CloudG, tinted.For("C1", "M01", "zone2").CloudB));
        Assert.Equal(1.2f, table.For("C5", "M02", "zone1").Exposure);
        Assert.Equal(0.9f, table.For("C5", "M02", "zone1").SunEnergy);
        Assert.Equal(1.4f, table.For("C5", "IA1", "zone1").SunEnergy);
        Assert.Equal(ZoneLighting.Default, table.For("C5", "M02", "zone3"));
        Assert.True(table.Night("C5", "M02"));
        Assert.False(table.Night("C5", "IA1"));
        Assert.Equal(new[] { "c5/m02" }, table.NightMissions);
    }

    [Fact]
    public void AMalformedEntryIsAWarningAndTunesNothingThere()
    {
        var table = LightingTable.Parse("""
            { "C1": 3,
              "C2": { "zone1": { "bogus": 1, "exposure": "x", "white": 99 }, "M01": { "night": "yes", "extra": 1 }, "odd": 2 } }
            """);
        Assert.Equal(7, table.Warnings.Count);
        Assert.Equal(16f, table.For("C2", "M01", "zone1").White);
        Assert.False(table.Night("C2", "M01"));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1]")]
    public void AnUnreadableFileTunesNothing(string json)
    {
        var table = LightingTable.Parse(json);
        Assert.Single(table.Warnings);
        Assert.Equal(ZoneLighting.Default, table.For("C1", "IA1", "zone1"));
    }

    [Fact]
    public void ASaveWritesTheSparseBlocksAndKeepsTheRest()
    {
        const string existing = """
            { "_about": "prose", "C1": { "ZONE2": { "white": 5 } }, "C5": { "M02": { "night": true } } }
            """;
        var chapter = ZoneLighting.Default with { Exposure = 1.25f };
        var mission = new Dictionary<string, float> { ["exposure"] = 1.25f, ["sun_r"] = 0.9f };
        string text = LightingTable.WithZone(existing, "C5", "M02", "zone1", chapter, mission);
        var root = (JsonObject)JsonNode.Parse(text)!;
        Assert.Equal(new[] { "_about", "C1", "C5" }, Keys(root));
        var c5 = (JsonObject)root["C5"]!;
        Assert.Equal(new[] { "M02", "ZONE1" }, Keys(c5));
        Assert.Equal(new[] { "exposure" }, Keys((JsonObject)c5["ZONE1"]!));
        // The mission's exposure equals the chapter's, so only its tint is an override.
        Assert.Equal(new[] { "night", "ZONE1" }, Keys((JsonObject)c5["M02"]!));
        Assert.Equal(new[] { "sun_r" }, Keys((JsonObject)c5["M02"]!["ZONE1"]!));

        var read = LightingTable.Parse(text);
        Assert.Equal(0.9f, read.For("C5", "M02", "zone1").SunR);
        Assert.Equal(1.25f, read.For("C5", "M02", "zone1").Exposure);
        Assert.True(read.Night("C5", "M02"));

        // An untuned zone drops its blocks, and a mission left with nothing drops out.
        string cleared = LightingTable.WithZone(text, "C5", "M02", "zone1", ZoneLighting.Default, new Dictionary<string, float>());
        Assert.Equal(new[] { "M02" }, Keys((JsonObject)JsonNode.Parse(cleared)!["C5"]!));
        string gone = LightingTable.WithZone(existing, "C1", "IA1", "zone2", ZoneLighting.Default, new Dictionary<string, float>());
        Assert.Equal(new[] { "_about", "C5" }, Keys((JsonObject)JsonNode.Parse(gone)!));
        Assert.Throws<InvalidDataException>(() => LightingTable.WithZone("[]", "C1", null, "zone1", chapter, mission));
    }

    [Fact]
    public void ASaveWritesOrClearsTheNightFlag()
    {
        var none = new Dictionary<string, float>();
        string on = LightingTable.WithZone("{}", "C1", "M01", "zone1", ZoneLighting.Default, none, night: true);
        Assert.True(LightingTable.Parse(on).Night("C1", "M01"));
        Assert.Equal(new[] { "night" }, Keys((JsonObject)JsonNode.Parse(on)!["C1"]!["M01"]!));
        // Null keeps the file's flag; false drops it, and the mission with it once nothing is left.
        Assert.True(LightingTable.Parse(LightingTable.WithZone(on, "C1", "M01", "zone1", ZoneLighting.Default, none)).Night("C1", "M01"));
        string off = LightingTable.WithZone(on, "C1", "M01", "zone1", ZoneLighting.Default, none, night: false);
        Assert.False(LightingTable.Parse(off).Night("C1", "M01"));
        Assert.Empty(Keys((JsonObject)JsonNode.Parse(off)!));
    }

    [Fact]
    public void ALayerAppliesOnlyWhileItsOptionStandsAndGiLandsAfterClouds()
    {
        var table = LightingTable.Parse("""
            { "C1": { "ZONE1": { "exposure": 1.2, "clouds": { "sun_energy": 0.5, "god_rays": 2 },
                                 "gi": { "sun_energy": 0.4, "sdfgi_energy": 0.8 } },
                      "M01": { "zone1": { "clouds": { "sun_energy": 0.6 }, "gi": { "ambient_floor": 0.3, "self_lit_emission": 2.5 } } } } }
            """);
        Assert.Empty(table.Warnings);
        Assert.Equal(new[] { "exposure" }, table.ChapterFields("C1", "zone1").Keys);
        Assert.Equal(new[] { "sun_energy", "god_rays" }, table.ChapterFields("C1", "zone1", LightingTable.CloudsLayer).Keys);
        Assert.Equal(new[] { "ambient_floor", "self_lit_emission" }, table.MissionFields("C1", "M01", "zone1", LightingTable.GiLayer).Keys);
        Assert.Empty(LightingTable.ActiveLayers(false, false));
        Assert.Equal(new[] { "clouds", "gi" }, LightingTable.ActiveLayers(true, true));

        var off = table.For("C1", "M01", "zone1", LightingTable.ActiveLayers(false, false));
        Assert.Equal((1.2f, 1.07f, 1f, 0.5f), (off.Exposure, off.SunEnergy, off.GodRays, off.SdfgiEnergy));
        // The mission's clouds block over the chapter's, both over the base.
        var clouds = table.For("C1", "M01", "zone1", LightingTable.ActiveLayers(true, false));
        Assert.Equal((1.2f, 0.6f, 2f, 0.5f), (clouds.Exposure, clouds.SunEnergy, clouds.GodRays, clouds.SdfgiEnergy));
        Assert.Equal(0.5f, table.For("C1", "IA1", "zone1", LightingTable.ActiveLayers(true, false)).SunEnergy);
        var gi = table.For("C1", "M01", "zone1", LightingTable.ActiveLayers(false, true));
        Assert.Equal((0.4f, 1f, 0.8f, 0.3f, 2.5f), (gi.SunEnergy, gi.GodRays, gi.SdfgiEnergy, gi.AmbientFloor, gi.SelfLitEmission));
        Assert.Equal(1f, clouds.SelfLitEmission);
        // Both on: gi applies after clouds, so its sun wins and the clouds' god rays stand.
        var both = table.For("C1", "M01", "zone1", LightingTable.ActiveLayers(true, true));
        Assert.Equal((0.4f, 2f, 0.8f, 0.3f), (both.SunEnergy, both.GodRays, both.SdfgiEnergy, both.AmbientFloor));
    }

    [Fact]
    public void AMalformedLayerIsAWarningAndTunesNothing()
    {
        var table = LightingTable.Parse("""
            { "C1": { "ZONE1": { "clouds": 3, "gi": { "bogus": 1, "gi": { "exposure": 2 }, "god_rays": "x" } } } }
            """);
        Assert.Equal(4, table.Warnings.Count);
        Assert.Equal(ZoneLighting.Default, table.For("C1", "M01", "zone1", LightingTable.Layers));
    }

    [Fact]
    public void ASaveWritesIntoALayerAndKeepsEveryOtherKeyInPlace()
    {
        const string existing = """
            { "_about": "prose",
              "C1": { "ZONE1": { "exposure": 1.2, "gi": { "sdfgi_energy": 0.8 } }, "ZONE2": { "white": 5 } },
              "C5": { "M02": { "night": true } } }
            """;
        var under = ZoneLighting.Default with { Exposure = 1.2f };
        var chapter = under with { SunEnergy = 0.5f, GodRays = 2f };
        var mission = new Dictionary<string, float> { ["sun_energy"] = 0.6f, ["god_rays"] = 2f };
        string text = LightingTable.WithZone(existing, "C1", "M01", "zone1", chapter, mission, layer: LightingTable.CloudsLayer, under: under);
        var root = (JsonObject)JsonNode.Parse(text)!;
        Assert.Equal(new[] { "_about", "C1", "C5" }, Keys(root));
        var c1 = (JsonObject)root["C1"]!;
        Assert.Equal(new[] { "ZONE1", "ZONE2", "M01" }, Keys(c1));
        // The base block's exposure and its gi layer stay; the clouds layer holds only what differs from under.
        Assert.Equal(new[] { "exposure", "gi", "clouds" }, Keys((JsonObject)c1["ZONE1"]!));
        Assert.Equal(new[] { "sun_energy", "god_rays" }, Keys((JsonObject)c1["ZONE1"]!["clouds"]!));
        Assert.Equal(new[] { "sun_energy" }, Keys((JsonObject)c1["M01"]!["ZONE1"]!["clouds"]!));

        var read = LightingTable.Parse(text);
        Assert.Empty(read.Warnings);
        Assert.Equal(under, read.For("C1", "M01", "zone1"));
        Assert.Equal((0.6f, 2f, 0.8f), (read.For("C1", "M01", "zone1", LightingTable.Layers).SunEnergy,
            read.For("C1", "M01", "zone1", LightingTable.Layers).GodRays, read.For("C1", "M01", "zone1", LightingTable.Layers).SdfgiEnergy));

        // A base save keeps both layers in their order after the fields.
        string based = LightingTable.WithZone(text, "C1", null, "zone1", ZoneLighting.Default with { White = 7f }, new Dictionary<string, float>());
        Assert.Equal(new[] { "white", "gi", "clouds" }, Keys((JsonObject)JsonNode.Parse(based)!["C1"]!["ZONE1"]!));
        Assert.Equal(0.8f, LightingTable.Parse(based).For("C1", "IA1", "zone1", LightingTable.Layers).SdfgiEnergy);

        // A layer saved equal to what it sits over drops out, and a mission left empty with it.
        string cleared = LightingTable.WithZone(text, "C1", "M01", "zone1", under, new Dictionary<string, float>(), layer: LightingTable.CloudsLayer, under: under);
        var c1Cleared = (JsonObject)JsonNode.Parse(cleared)!["C1"]!;
        Assert.Equal(new[] { "ZONE1", "ZONE2" }, Keys(c1Cleared));
        Assert.Equal(new[] { "exposure", "gi" }, Keys((JsonObject)c1Cleared["ZONE1"]!));

        // The gi layer saves the GI fields beside sdfgi_energy and reads them back.
        var giTune = under with { SdfgiEnergy = 0.8f, AmbientFloor = 0.5f, SelfLitEmission = 3f };
        string giSaved = LightingTable.WithZone(existing, "C1", null, "zone1", giTune, new Dictionary<string, float>(), layer: LightingTable.GiLayer, under: under);
        Assert.Equal(new[] { "ambient_floor", "sdfgi_energy", "self_lit_emission" },
            Keys((JsonObject)JsonNode.Parse(giSaved)!["C1"]!["ZONE1"]!["gi"]!).OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(giTune, LightingTable.Parse(giSaved).For("C1", "IA1", "zone1", new[] { LightingTable.GiLayer }));
    }

    [Fact]
    public void TheDebugGrammarKeepsWhatTheLabReads()
    {
        var rejected = new List<string>();
        string kept = LightingDebug.FilterTokens("zone:zone2, exposure:1.3, bogus:1, plane:player_fury, heading:90, pitch:x, off, open, shut", rejected);
        Assert.Equal("zone:zone2,exposure:1.3,plane:player_fury,heading:90,off,open", kept);
        Assert.Equal(new[] { "bogus:1", "pitch:x", "shut" }, rejected);
        Assert.True(LightingDebug.Has(kept, "off") && LightingDebug.Has(kept, "open"));
        Assert.Equal("zone2", LightingDebug.Word(kept, "zone"));
        Assert.Equal(90f, LightingDebug.Angle(kept, "heading"));
        Assert.Null(LightingDebug.Angle(kept, "bank"));
        Assert.True(LightingDebug.SetsFields(kept));
        Assert.Equal(1.3f, LightingDebug.WithFields(ZoneLighting.Default, kept).Exposure);
        Assert.Equal(4f, LightingDebug.WithFields(ZoneLighting.Default, "exposure:40").Exposure);
        Assert.Equal("grade_saturation:0.8", LightingDebug.FilterTokens("grade_saturation:0.8"));
        Assert.Equal(0.8f, LightingDebug.WithFields(ZoneLighting.Default, "grade_saturation:0.8").GradeSaturation);
        var clouds = LightingDebug.WithFields(ZoneLighting.Default, LightingDebug.FilterTokens("cloud_r:0.4,cloud_g:0.4,cloud_b:0.6"));
        Assert.Equal((0.4f, 0.4f, 0.6f), (clouds.CloudR, clouds.CloudG, clouds.CloudB));
        Assert.Equal("cloud_r:0.40,cloud_g:0.40,cloud_b:0.60", clouds.Describe());
    }

    private static List<string> Keys(JsonObject o)
    {
        var keys = new List<string>();
        foreach (var (k, _) in o)
            keys.Add(k);
        return keys;
    }
}
