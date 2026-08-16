using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The egen generator reader (docs/formats/mission-entities.md): fixture units for the three
/// record shapes and the [null] file, plus golden counts over the install so a reader or
/// extraction change moves a test instead of silently drifting.
/// </summary>
public class EnemyGeneratorsTests
{
    private static readonly string[] Chapters = { "C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5" };

    // Chapter-scope dirs that are not mission dirs (the same discovery rule Probes.Ai uses).
    private static readonly HashSet<string> ChapterScopeDirs =
        new(StringComparer.OrdinalIgnoreCase) { "gamez", "texture", "cam_anim", "zrdr" };

    [Fact]
    public void TheThreeShapesParseFromTheFixture()
    {
        var defs = EnemyGenerators.Load(TestData.Fixture("egen"));
        Assert.Equal(3, defs.Count);

        var zep = defs[0];
        Assert.Equal("testzep", zep.Node);
        Assert.True(zep.IsZeppelin);
        Assert.Equal("test_open_doors", zep.OpenAnim);
        Assert.Equal("test_close_doors", zep.CloseAnim);
        Assert.Equal("testbay", zep.Origin);
        Assert.Equal(new Godot.Vector3(-90f, 0f, 0f), zep.RotationDeg);
        Assert.Equal(150f, zep.MinAltitude);
        Assert.Equal("Testzep_params", zep.VehicleParams);
        Assert.Equal(new[] { "TestNetA", "TestNetB" }, zep.Nets);
        Assert.False(zep.ChooseNetsRandom);
        Assert.Equal(0, zep.Capacity);
        Assert.Equal(4, zep.MaxActive);
        Assert.Equal(3, zep.WaveSize);
        Assert.Equal(10f, zep.WavePeriod);
        Assert.Equal(2f, zep.IndPeriod);
        Assert.Null(zep.HealthyNode);
        Assert.False(zep.MovingPath);

        var plain = defs[1];
        Assert.Equal("testfield", plain.Node);
        Assert.False(plain.IsZeppelin);
        Assert.Null(plain.VehicleParams);   // 8 of 23 shipped generators author no params
        Assert.Null(plain.MinAltitude);     // absent = gate skipped (the -1.0 sentinel case)
        Assert.Null(plain.Origin);
        Assert.True(plain.ChooseNetsRandom);
        Assert.Equal(5, plain.Capacity);

        var sub = defs[2];
        Assert.Equal("testsub", sub.Node);
        Assert.True(sub.MovingPath);        // authored as a bare "moving_path", null pair
        Assert.Equal("testsubhealthy", sub.HealthyNode);
        Assert.False(sub.IsZeppelin);
    }

    [Fact]
    public void ANullFileIsNoGeneratorsNotAnError()
    {
        Assert.Empty(EnemyGenerators.Load(TestData.Fixture("egen-null")));
    }

    // ---- Goldens over the install ---------------------------------------------------------------

    [ExtractedDataFact]
    public void TheInstallWideCensusMatchesTheScopingCounts()
    {
        int files = 0, nullFiles = 0, zeppelin = 0, moving = 0, plain = 0;
        int capacityZero = 0, withParams = 0;
        var waveSizes = new Dictionary<int, int>();
        foreach (var (chapter, mission) in Missions())
        {
            var defs = EnemyGenerators.Load(SessionPaths.MissionZrdr(TestData.DataRoot!, chapter, mission));
            files++;
            if (defs.Count == 0)
            {
                nullFiles++;
                continue;
            }
            var netNames = new HashSet<string>(
                AiNets.LoadIndex(SessionPaths.ChapterZrdr(TestData.DataRoot!, chapter)).Values,
                StringComparer.OrdinalIgnoreCase);
            foreach (var def in defs)
            {
                if (def.IsZeppelin)
                {
                    zeppelin++;
                }
                else if (def.MovingPath)
                {
                    moving++;
                }
                else
                {
                    plain++;
                }
                if (def.Capacity == 0)
                {
                    capacityZero++;
                }
                if (def.VehicleParams != null)
                {
                    withParams++;
                }
                waveSizes.TryGetValue(def.WaveSize, out int c);
                waveSizes[def.WaveSize] = c + 1;
                // Every authored net resolves against its chapter's neindex names, and no
                // authored choose_nets is random. Zeppelin generators all carry the launch gate.
                Assert.NotEmpty(def.Nets);
                foreach (var net in def.Nets)
                {
                    Assert.Contains(net, netNames);
                }
                Assert.False(def.ChooseNetsRandom);
                if (def.IsZeppelin)
                {
                    Assert.NotNull(def.Origin);
                    Assert.InRange(def.MinAltitude!.Value, 100f, 300f);
                }
            }
        }
        Assert.Equal(53, files);
        Assert.Equal(33, nullFiles);
        Assert.Equal(17, zeppelin);
        Assert.Equal(5, plain);
        Assert.Equal(1, moving);
        Assert.Equal(23, capacityZero);   // the capacity puzzle: 0 on every shipped generator
        Assert.Equal(15, withParams);
        Assert.Equal(22, waveSizes[1]);
        Assert.Equal(1, waveSizes[3]);
    }

    [ExtractedDataFact]
    public void TheWorkedExampleGeneratorReadsExactlyAsTheScopingDocMeasuredIt()
    {
        // C1/M04 generator 1 of 2: the plan's worked example.
        var defs = EnemyGenerators.Load(SessionPaths.MissionZrdr(TestData.DataRoot!, "C1", "M04"));
        Assert.Equal(2, defs.Count);
        var gen = defs[0];
        Assert.Equal("eairg31", gen.Node);
        Assert.Equal("Eairg31_params", gen.VehicleParams);
        Assert.Equal(new[] { "M4Reinf4", "M4Reinf3" }, gen.Nets);
        Assert.Equal(0, gen.Capacity);
        Assert.Equal(4, gen.MaxActive);
        Assert.Equal(1, gen.WaveSize);
        Assert.Equal(10f, gen.WavePeriod);
        Assert.Equal(10f, gen.IndPeriod);
        Assert.False(gen.IsZeppelin);
    }

    [ExtractedDataFact]
    public void TheParamsLabelsResolveInTheAivHeaderExceptTheOneShippedTypo()
    {
        // C1/M04's one shipped typo is the expected miss; see
        // docs/formats/mission-entities/enemy-generators.md "shipped typo".
        var misses = new List<string>();
        foreach (var (chapter, mission) in Missions())
        {
            var missionZrdr = SessionPaths.MissionZrdr(TestData.DataRoot!, chapter, mission);
            foreach (var def in EnemyGenerators.Load(missionZrdr))
            {
                if (def.VehicleParams == null)
                {
                    continue;
                }
                var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var aiv = Zrdr.LoadFile(missionZrdr, "aiv.json");
                if (aiv.Count > 0 && aiv[0] is List<object?> header)
                {
                    foreach (var h in header)
                    {
                        if (h is string label)
                        {
                            labels.Add(label);
                        }
                    }
                }
                if (!labels.Contains(def.VehicleParams))
                {
                    misses.Add($"{chapter}/{mission}: {def.VehicleParams}");
                }
            }
        }
        Assert.Equal(new[] { "C1/M04: Eairg32_params" }, misses);
    }

    private static IEnumerable<(string Chapter, string Mission)> Missions()
    {
        foreach (var chapter in Chapters)
        {
            var dir = Path.Combine(TestData.DataRoot!, "extracted", chapter);
            if (!Directory.Exists(dir))
            {
                continue;
            }
            var missions = new List<string>();
            foreach (var d in Directory.EnumerateDirectories(dir))
            {
                var name = Path.GetFileName(d);
                if (!ChapterScopeDirs.Contains(name)
                    && !name.StartsWith("rtexture", StringComparison.OrdinalIgnoreCase))
                {
                    missions.Add(name);
                }
            }
            missions.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var m in missions)
            {
                yield return (chapter, m);
            }
        }
    }
}
