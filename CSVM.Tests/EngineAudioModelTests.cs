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

    /// <summary>The airframe rattle, unlike the whine, IS assigned on every airframe, and its whole
    /// law is one number: the speed fraction it starts at. The shipped block puts that at 1.0, the
    /// plane's own rated maximum, so every airframe rattles from rated max upward.
    /// <c>volume_range</c> is deliberately absent from <see cref="PlaneStats"/>: the original parses
    /// it into a global no instruction reads (docs/org/shakes.md).</summary>
    [ExtractedDataFact]
    public void EveryAirframeRattlesFromItsOwnRatedMaximum()
    {
        foreach (var node in AllPlaneNodeNames)
        {
            var stats = PlaneStats.Load(SharedZrdr, node);
            Assert.Equal("snd_planeshake", stats.RattleSound);
            Assert.Equal(1f, stats.RattleSpeedGate, 4);
        }
    }

    /// <summary>The rattle is a GATE, not a ramp: silent below the authored fraction of fd_speed and
    /// at its one level from it upward, however far past it the dive goes. A ramp here is what left
    /// the loop inaudible at the speeds a dive actually reaches, so the flat top is the pin that
    /// matters, not just the edge. The level is the port's 1.3, a chosen step over the original's
    /// 1.0.</summary>
    [Fact]
    public void RattleGainIsAGateAtFullLevelRatherThanARamp()
    {
        var stats = new PlaneStats { EngineSound = "snd_normal", RattleSpeedGate = 1f };

        float level = EngineAudioCurves.RattleLevel();
        Assert.Equal(1.3f, level);
        Assert.Equal(0f, EngineAudioCurves.Rattle(stats, 0f));
        Assert.Equal(0f, EngineAudioCurves.Rattle(stats, 0.999f));
        Assert.Equal(level, EngineAudioCurves.Rattle(stats, 1f));
        Assert.Equal(level, EngineAudioCurves.Rattle(stats, 1.05f));
        Assert.Equal(level, EngineAudioCurves.Rattle(stats, 1.2f));
        Assert.Equal(level, EngineAudioCurves.Rattle(stats, 3f));

        // The gate moves with the authored fraction rather than being pinned at 1.
        var late = new PlaneStats { EngineSound = "snd_normal", RattleSpeedGate = 1.5f };
        Assert.Equal(0f, EngineAudioCurves.Rattle(late, 1.2f));
        Assert.Equal(level, EngineAudioCurves.Rattle(late, 1.5f));
    }

    /// <summary>The damaged engine is a DEFINITION SWAP on slot 0 with a pitch multiplier drawn once
    /// per swap, never a second loop blended over the healthy one. The install authors exactly one
    /// entry, inherited from basic_airplane by every plane, and its flag byte is set with the range
    /// 0.0 to 1.0, so a damaged engine can be drawn anywhere from the frequency floor to normal.</summary>
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
    /// plane gaining or losing its own loop does not fail this, a reader collapsing every plane onto
    /// one inherited default does.</summary>
    [ExtractedDataFact]
    public void EngineDefinitionsArePerAirframe()
    {
        var distinct = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var node in AllPlaneNodeNames)
            distinct.Add(PlaneStats.Load(SharedZrdr, node).EngineSound);

        Assert.True(distinct.Count >= 8, $"only {distinct.Count} distinct engine definitions: {string.Join(", ", distinct)}");
    }

    /// <summary>D31: every one of the eleven player airframes authors <c>cockpit_engine_sound</c>
    /// (either its own or `basic_airplane`'s inherited fallback), so
    /// <see cref="PlaneStats.CockpitEngineSound"/> being null is a defensive branch for a plane the
    /// shipped install does not actually contain, not a live case any of the 11 hit.</summary>
    [ExtractedDataFact]
    public void EveryAirframeAuthorsACockpitEngineDefinition()
    {
        foreach (var node in AllPlaneNodeNames)
        {
            foreach (var stats in new[]
                     {
                         PlaneStats.Load(SharedZrdr, node), PlaneStats.LoadForAi(SharedZrdr, node),
                     })
            {
                Assert.False(string.IsNullOrEmpty(stats.CockpitEngineSound),
                    $"{node}: no cockpit_engine_sound authored");
                Assert.EndsWith("_cp", stats.CockpitEngineSound);
            }
        }
    }

    /// <summary>D31's def-selection rule, pure and headless: the cockpit swap applies only when the
    /// airframe authors it, yields to the damaged swap when both conditions are live (no def authors
    /// a damaged-cockpit variant), and both fall back to the plain <c>engine_sound</c> definition.
    /// <c>DamagedEnginePitchRandom</c> stays false so the random draw branch (needing a live
    /// <c>RandomNumberGenerator</c>) is never reached, untouched by this precedence rule.</summary>
    [Fact]
    public void EngineDefForPicksDamagedOverCockpitOverNormal()
    {
        var stats = new PlaneStats
        {
            EngineSound = "snd_normal",
            CockpitEngineSound = "snd_cockpit",
            DamagedEngineSound = "snd_damaged",
            DamagedEnginePitchRandom = false,
        };

        Assert.Equal(("snd_normal", 1f),
            EngineAudioCurves.EngineDefFor(stats, damaged: false, rng: null!, cockpitView: false));
        Assert.Equal(("snd_cockpit", 1f),
            EngineAudioCurves.EngineDefFor(stats, damaged: false, rng: null!, cockpitView: true));
        Assert.Equal(("snd_damaged", 1f),
            EngineAudioCurves.EngineDefFor(stats, damaged: true, rng: null!, cockpitView: false));
        Assert.Equal(("snd_damaged", 1f),
            EngineAudioCurves.EngineDefFor(stats, damaged: true, rng: null!, cockpitView: true));
    }

    /// <summary>The decoded damage gate: the swap needs the worst zone BELOW a quarter health, so a
    /// graze keeps the healthy loop. Engine-out is the mask's other modelled bit and swaps on its
    /// own however healthy the airframe is.</summary>
    [Fact]
    public void EngineDamagedNeedsAQuarterHealthOrAnEngineOut()
    {
        Assert.False(EngineAudioCurves.EngineDamaged(1f));
        Assert.False(EngineAudioCurves.EngineDamaged(0.9f));
        Assert.False(EngineAudioCurves.EngineDamaged(0.25f)); // the test is strict: at is not below
        Assert.True(EngineAudioCurves.EngineDamaged(0.2499f));
        Assert.True(EngineAudioCurves.EngineDamaged(0f));
        Assert.True(EngineAudioCurves.EngineDamaged(1f, engineDead: true));
    }

    /// <summary>The pitch law is a linear DRAW across the entry's own two floats, not a derivation
    /// from damage: the draw's ends are the range's ends, its middle is the range's middle, and a
    /// CLEAR flag byte holds the multiplier at 1 instead of drawing. The last case is what keeps an
    /// unflagged entry off the frequency floor rather than pinning it there.</summary>
    [Fact]
    public void DamagedEnginePitchIsDrawnLinearlyAcrossTheAuthoredRange()
    {
        var drawn = new PlaneStats
        {
            EngineSound = "snd_normal",
            DamagedEngineSound = "snd_damaged",
            DamagedEnginePitchRandom = true,
            DamagedEnginePitchLo = 0.4f,
            DamagedEnginePitchHi = 0.9f,
        };

        Assert.Equal(0.4f, EngineAudioCurves.DamagedPitchMul(drawn, 0f), 4);
        Assert.Equal(0.65f, EngineAudioCurves.DamagedPitchMul(drawn, 0.5f), 4);
        Assert.Equal(0.9f, EngineAudioCurves.DamagedPitchMul(drawn, 1f), 4);

        // The shipped entry, whose range reaches the mixer's frequency floor.
        var shipped = new PlaneStats
        {
            EngineSound = "snd_normal",
            DamagedEngineSound = "snd_damagedengine",
            DamagedEnginePitchRandom = true,
            DamagedEnginePitchLo = 0f,
            DamagedEnginePitchHi = 1f,
        };
        Assert.Equal(0.75f, EngineAudioCurves.DamagedPitchMul(shipped, 0.75f), 4);

        var unflagged = new PlaneStats
        {
            EngineSound = "snd_normal",
            DamagedEngineSound = "snd_damaged",
            DamagedEnginePitchRandom = false,
            DamagedEnginePitchLo = 0.4f,
            DamagedEnginePitchHi = 0.9f,
        };
        Assert.Equal(1f, EngineAudioCurves.DamagedPitchMul(unflagged, 0f), 4);
        Assert.Equal(1f, EngineAudioCurves.DamagedPitchMul(unflagged, 1f), 4);
        Assert.Equal(("snd_damaged", 1f),
            EngineAudioCurves.EngineDefFor(unflagged, damaged: true, rng: null!));
    }

    /// <summary>An airframe with no <c>cockpit_engine_sound</c> of its own keeps the normal loop
    /// in the Cockpit view rather than going silent or erroring, the same "keep the normal def"
    /// fallback the plan calls for.</summary>
    [Fact]
    public void EngineDefForFallsBackToNormalWithNoCockpitDefinition()
    {
        var stats = new PlaneStats { EngineSound = "snd_normal" };

        Assert.Equal(("snd_normal", 1f),
            EngineAudioCurves.EngineDefFor(stats, damaged: false, rng: null!, cockpitView: true));
    }

    /// <summary>C22 (BL-459): the re-arm timer stays quiet below its 3 s floor. It fires once the
    /// total crosses the threshold drawn from <c>u</c>, and resets to zero when it does, the
    /// original's own def+0x88 accumulator.
    /// Decode: docs/formats/vehicle.md, "What makes an airframe damaged".</summary>
    [Fact]
    public void DamagedRearmWaitsOutItsThresholdThenResets()
    {
        var timer = new DamagedEngineTimer();

        // u=0 draws the floor, 3.0 s exactly.
        Assert.False(EngineAudioCurves.AdvanceDamagedRearm(timer, 1f, 0f));
        Assert.False(EngineAudioCurves.AdvanceDamagedRearm(timer, 1f, 0f));
        Assert.Equal(2f, timer.Elapsed, 4);
        Assert.False(EngineAudioCurves.AdvanceDamagedRearm(timer, 0.9f, 0f));
        Assert.True(EngineAudioCurves.AdvanceDamagedRearm(timer, 0.2f, 0f));
        Assert.Equal(0f, timer.Elapsed, 4);
    }

    /// <summary>The threshold's own span is 3 to 5 s (<c>3.0 + 2*rand()/32767</c>). A draw of
    /// <c>u=1</c> holds off a full 5 s, one tick short of firing at 4.99 s.</summary>
    [Fact]
    public void DamagedRearmThresholdReachesFiveSecondsAtTheTopOfTheDraw()
    {
        var timer = new DamagedEngineTimer();

        Assert.False(EngineAudioCurves.AdvanceDamagedRearm(timer, 4.99f, 1f));
        Assert.True(EngineAudioCurves.AdvanceDamagedRearm(timer, 0.02f, 1f));
    }

    /// <summary>The re-arm timer sits on the airframe DEFINITION, not the per-spawn instance.
    /// Roster durability, the enemy difficulty scale, the per-spawn jitter and a hangar engine
    /// swap each clone one loaded <see cref="PlaneStats"/>. Every clone carries the SAME
    /// <see cref="DamagedEngineTimer"/> reference forward, never a copy.</summary>
    [Fact]
    public void EveryPerSpawnCloneSharesTheSameDamagedTimer()
    {
        var loaded = new PlaneStats { EngineSound = "snd_normal" };

        var rostered = loaded.WithRosterDurability(50f, 10f);
        var scaled = loaded.WithEnemyDurability(1.5f);
        var jittered = loaded.WithAiSpawnJitter(new System.Random(1));
        var repowered = loaded.WithEnginePower(2f);
        var chained = loaded.WithRosterDurability(50f, 10f).WithEnemyDurability(1.5f)
            .WithAiSpawnJitter(new System.Random(2));

        Assert.Same(loaded.DamagedTimer, rostered.DamagedTimer);
        Assert.Same(loaded.DamagedTimer, scaled.DamagedTimer);
        Assert.Same(loaded.DamagedTimer, jittered.DamagedTimer);
        Assert.Same(loaded.DamagedTimer, repowered.DamagedTimer);
        Assert.Same(loaded.DamagedTimer, chained.DamagedTimer);
    }

    /// <summary>...and two SEPARATELY loaded airframes never share one. Each load is its own
    /// definition, so an unrelated plane's timer must not move when this one's does.</summary>
    [ExtractedDataFact]
    public void TwoSeparatelyLoadedAirframesDoNotShareADamagedTimer()
    {
        var a = PlaneStats.LoadForAi(SharedZrdr, "player_fury");
        var b = PlaneStats.LoadForAi(SharedZrdr, "player_fury");

        Assert.NotSame(a.DamagedTimer, b.DamagedTimer);
    }
}
