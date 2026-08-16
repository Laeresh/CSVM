using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The maneuver-library reader (docs/formats/ai-rosters.md): fixture units for both step
/// shapes, the flags, the stub, the eligibility cull and the signature bitmask, plus golden
/// counts and difficulties over the install — the plan's own 1–9 table, asserted so a reader
/// or extraction change moves a test instead of silently drifting.
/// </summary>
public class ManeuversTests
{
    /// <summary>The 14 design-shared difficulties plus the two shipped-only entries — the
    /// plan's table ("The maneuver library" + the 2026-08-10 delta).</summary>
    public static TheoryData<string, int> ShippedDifficulties => new()
    {
        { "nitro_evade", 0 },
        { "rudder_turn", 1 },
        { "roll", 2 },
        { "bank_turn", 2 },
        { "climb", 3 },
        { "dive", 3 },
        { "jinking", 4 },
        { "scissors", 5 },
        { "rolling_scissors", 5 },
        { "snap_roll", 6 },
        { "barrel_roll", 6 },
        { "immelman", 7 },
        { "loop", 7 },
        { "split_s", 8 },
        { "spiral_dive", 8 },
        { "lag_pursuit_roll", 9 },
    };

    private static string InstallZrdr =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    [Fact]
    public void ParsesBothStepShapesPreservingTheUndecodedExtras()
    {
        var lib = Fixtures();
        var level = lib.Single(m => m.Name == "probe_level_hold");
        Assert.Equal(2, level.Steps.Count);
        Assert.Equal(new ManeuverStep(1f, 0f, 0f, 0f, level.Steps[0].Extra), level.Steps[0]);
        Assert.Empty(level.Steps[0].Extra);

        // The 7-element variant keeps its three trailing numbers verbatim, uninterpreted.
        var seven = lib.Single(m => m.Name == "probe_seven_element");
        Assert.Equal(4.5f, seven.Steps[0].DurationS);
        Assert.Equal(new[] { 0.5f, 0f, 1f }, seven.Steps[0].Extra);
        Assert.Empty(seven.Steps[1].Extra);
        Assert.Equal(-0.5f, seven.Bias);
    }

    [Fact]
    public void FlagsParseAsPresenceNotValues()
    {
        var rel = Fixtures().Single(m => m.Name == "probe_relative_yaw");
        Assert.True(rel.Relative);
        Assert.True(rel.AutogyroAllowed);
        Assert.True(rel.Nitro);
        Assert.Equal(0, rel.Difficulty);
        Assert.Equal(0f, rel.Bias);

        var level = Fixtures().Single(m => m.Name == "probe_level_hold");
        Assert.False(level.Relative);
        Assert.False(level.AutogyroAllowed);
        Assert.False(level.Nitro);
    }

    [Fact]
    public void AStubParsesButIsNeverEligibleWhateverItsDifficulty()
    {
        // The fixture stub sits at difficulty 2 ON PURPOSE: it separates the stub cull from
        // the difficulty cull, which the shipped high_yo_yo (difficulty 99) cannot.
        var stub = Fixtures().Single(m => m.Name == "probe_stub");
        Assert.True(stub.IsStub);
        Assert.Empty(stub.Steps);
        Assert.False(stub.EligibleFor(9));
    }

    [Fact]
    public void EligibilityComparesDifficultyAgainstNaturalTouchDirectly()
    {
        var lib = Fixtures();
        // natural_touch 1: the difficulty-0 and difficulty-1 entries only.
        Assert.Equal(
            new[] { "probe_level_hold", "probe_relative_yaw" },
            lib.Where(m => m.EligibleFor(1)).Select(m => m.Name));
        // natural_touch 6 admits the difficulty-6 entry; the stub stays out at any stat.
        Assert.Equal(
            new[] { "probe_level_hold", "probe_relative_yaw", "probe_seven_element" },
            lib.Where(m => m.EligibleFor(6)).Select(m => m.Name));
    }

    [Fact]
    public void SignatureMaskDecodesInExeTableOrderNotFileOrder()
    {
        // The documented worked examples (docs/formats/ai-rosters.md): 2048 = split_s (the
        // Black Swan), 2064 = bits 4+11 = dive+split_s, 32896 = bits 7+15, 65536 = bit 16.
        Assert.Equal(new[] { "split_s" }, Maneuvers.SignatureNames(2048));
        Assert.Equal(new[] { "dive", "split_s" }, Maneuvers.SignatureNames(2064));
        Assert.Equal(new[] { "rolling_scissors", "barrel_roll" }, Maneuvers.SignatureNames(32896));
        Assert.Equal(new[] { "spiral_dive" }, Maneuvers.SignatureNames(65536));
        Assert.Empty(Maneuvers.SignatureNames(0));
        Assert.Equal(17, Maneuvers.ExeTableOrder.Count);
    }

    // ---- Goldens over the install ---------------------------------------------------------------

    [ExtractedDataFact]
    public void TheInstallShipsSeventeenManeuversAndOneStub()
    {
        var lib = Maneuvers.Load(InstallZrdr);
        Assert.Equal(17, lib.Count);
        Assert.Equal(17, lib.Select(m => m.Name).Distinct().Count());
        // Every exe-table name is a shipped entry and vice versa (different orders).
        Assert.Equal(
            Maneuvers.ExeTableOrder.OrderBy(n => n),
            lib.Select(m => m.Name).OrderBy(n => n));

        // The high_yo_yo stub: difficulty 99 against a stat capped at 9, no steps at all.
        var stub = lib.Single(m => m.Name == "high_yo_yo");
        Assert.True(stub.IsStub);
        Assert.Equal(99, stub.Difficulty);
        Assert.False(stub.EligibleFor(9));
        Assert.Single(lib.Where(m => m.IsStub));

        // nitro_evade is difficulty 0 (always available) and the only nitro-flagged entry.
        var nitro = lib.Single(m => m.Nitro);
        Assert.Equal("nitro_evade", nitro.Name);
        Assert.True(nitro.EligibleFor(1));
    }

    [ExtractedDataTheory]
    [MemberData(nameof(ShippedDifficulties))]
    public void ShippedDifficultiesMatchThePlansTable(string name, int difficulty)
    {
        Assert.Equal(difficulty, Library().Single(m => m.Name == name).Difficulty);
    }

    [ExtractedDataFact]
    public void TheWorkedExamplesReadExactlyAsTheFormatPageRecordsThem()
    {
        var lib = Library();

        // dive: one step, 60° nose-down held 4 s; deprioritised by bias −0.5.
        var dive = lib.Single(m => m.Name == "dive");
        var step = Assert.Single(dive.Steps);
        Assert.Equal(new ManeuverStep(4f, -60f, 0f, 0f, step.Extra), step);
        Assert.Empty(step.Extra);
        Assert.Equal(-0.5f, dive.Bias);
        Assert.True(dive.AutogyroAllowed);

        // The two 7-element steps in the install: barrel_roll's single step and
        // spiral_dive's second, extras [0.5, 0, 1] both — undecoded, preserved raw.
        var barrel = lib.Single(m => m.Name == "barrel_roll");
        Assert.Equal(new[] { 0.5f, 0f, 1f }, Assert.Single(barrel.Steps).Extra);
        var spiral = lib.Single(m => m.Name == "spiral_dive");
        Assert.Equal(2, spiral.Steps.Count);
        Assert.Equal(new[] { 0.5f, 0f, 1f }, spiral.Steps[1].Extra);
        int sevenElement = lib.Sum(m => m.Steps.Count(s => s.Extra.Count > 0));
        Assert.Equal(2, sevenElement);

        // rudder_turn: relative, zero-duration yaw-50 — the "advance when reached" shape.
        var rudder = lib.Single(m => m.Name == "rudder_turn");
        Assert.True(rudder.Relative);
        Assert.Equal(0f, rudder.Steps[0].DurationS);
        Assert.Equal(50f, rudder.Steps[0].YawDeg);

        // 5 of 17 are autogyro-legal.
        Assert.Equal(5, lib.Count(m => m.AutogyroAllowed));
    }

    private static List<Maneuver> Fixtures() => Maneuvers.Load(TestData.Fixture("zrdr"));

    private static List<Maneuver> Library() => Maneuvers.Load(InstallZrdr);
}
