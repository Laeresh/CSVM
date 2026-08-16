using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The decoded engine-audio slot model, pinned against the shipped vehicle data rather than against
/// the code that reads it: which slots an airframe actually names, and with what. Two of the three
/// facts here refuted a live reading, so a reader change that quietly restores one (a whine def out
/// of nowhere, a damaged engine that stops being a swap) fails here. The curve maths and the cull
/// are in-engine, in the <c>ai-damage-stages</c> and flight suites.
/// </summary>
public class EngineAudioModelTests
{
    private static readonly string[] AllPlaneNodeNames =
    {
        "player_bhawk", "player_fury", "player_peacemaker", "player_kestrel", "player_fbrand",
        "player_warhawk", "player_balmoral", "player_pfighter", "player_autogyro",
        "player_avenger", "player_brigand",
    };

    private static string SharedZrdr =>
        SessionPaths.PreferUnzipped(System.IO.Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    /// <summary>Slot 1, the overspeed whine, is unassigned on every airframe in the install: the KEY
    /// is real and read (<c>prop_sound</c>, VDEF+0x74), and no shipped def authors it, so the
    /// original plays no whine at all. A non-null here means either the data was misread or a
    /// default was invented, and both would put a loop in a dive that the original has not got.</summary>
    [ExtractedDataFact]
    public void NoAirframeNamesAWhineDefinition()
    {
        var named = new List<string>();
        foreach (var node in AllPlaneNodeNames)
        {
            if (PlaneStats.Load(SharedZrdr, node).WhineSound is { } player)
                named.Add($"{node} (player) → {player}");
            if (PlaneStats.LoadForAi(SharedZrdr, node).WhineSound is { } ai)
                named.Add($"{node} (ai) → {ai}");
        }

        Assert.True(named.Count == 0, string.Join("\n", named));
    }

    /// <summary>The damaged engine is a DEFINITION SWAP on slot 0 with a pitch multiplier drawn once
    /// per swap, never a second loop blended over the healthy one. The install authors exactly one
    /// entry, inherited from basic_airplane by every plane, and its flag byte is set with the range
    /// 0.0 to 1.0 — so a damaged engine can be drawn anywhere from the frequency floor to normal.</summary>
    [ExtractedDataFact]
    public void EveryAirframeSwapsOneDamagedEngineDefinitionWithARandomisedPitch()
    {
        foreach (var node in AllPlaneNodeNames)
        {
            foreach (var stats in new[]
                     {
                         PlaneStats.Load(SharedZrdr, node), PlaneStats.LoadForAi(SharedZrdr, node),
                     })
            {
                Assert.Equal("snd_damagedengine", stats.DamagedEngineSound);
                Assert.True(stats.DamagedEnginePitchRandom, $"{node}: the swap's pitch flag is clear");
                Assert.Equal(0f, stats.DamagedEnginePitchLo);
                Assert.Equal(1f, stats.DamagedEnginePitchHi);
                Assert.False(string.IsNullOrEmpty(stats.EngineSound), $"{node}: no engine definition");
            }
        }
    }

    /// <summary>Slot 0's healthy definition is the airframe's own, not one shared default: eleven
    /// planes name at least eight distinct engine loops between them. The count is a floor, so a
    /// plane gaining or losing its own loop does not fail this — a reader collapsing every plane onto
    /// one inherited default does.</summary>
    [ExtractedDataFact]
    public void EngineDefinitionsArePerAirframe()
    {
        var distinct = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var node in AllPlaneNodeNames)
            distinct.Add(PlaneStats.Load(SharedZrdr, node).EngineSound);

        Assert.True(distinct.Count >= 8, $"only {distinct.Count} distinct engine definitions: {string.Join(", ", distinct)}");
    }
}
