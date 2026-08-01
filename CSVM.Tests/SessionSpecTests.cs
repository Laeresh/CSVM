using System.Linq;
using CSVM;
using CSVM.Utils;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The launch-argument resolution truth table: what each command line settles, rule by rule.
///
/// <para>Everything here used to be statement order inside a 571-line <c>_Ready</c>, reachable only
/// by launching the engine, and checked only by a whole-command-line baseline that could report
/// THAT a row moved but never which rule moved it. These facts name the rule, and they are what
/// paid for deleting that baseline's instrument — so this file is now the only thing standing
/// behind the launch surface. Add the fact with the rule.</para>
///
/// <para>Two known defects are asserted AS THEY ARE, marked below. Both are reproduced on purpose;
/// fixing either is a behaviour change and needs its own item.</para>
/// </summary>
public class SessionSpecTests
{
    // ---- Mode arbitration ----------------------------------------------------------------------

    [Fact]
    public void ABareLaunchIsTheMenu()
    {
        var s = S();
        Assert.Equal(SessionMode.Menu, s.Mode);
        Assert.False(s.HasContentArg);
        Assert.True(s.ShowsMenu);
    }

    [Theory]
    [InlineData("--plane=player_fury")]
    [InlineData("--chapter=C4")]
    [InlineData("--chapter")]
    [InlineData("--stage=empty")]
    [InlineData("--screenshot=shot.png")]
    [InlineData("--fly")]
    [InlineData("--stunt")]
    public void AnyContentArgFliesByDefault(string arg)
    {
        var s = S(arg);
        Assert.True(s.HasContentArg);
        Assert.Equal(SessionMode.Fly, s.Mode);
        Assert.False(s.ShowsMenu);
    }

    [Fact]
    public void ViewerOptsOutOfTheFlightDefault()
    {
        Assert.Equal(SessionMode.Viewer, S("--viewer", "--plane=player_fury").Mode);
    }

    /// <summary>Anim lab &gt; freecam &gt; viewer &gt; fly, and the winner clears the modifiers of
    /// everything it beat.</summary>
    [Fact]
    public void TheAnimLabBeatsEveryOtherMode()
    {
        var s = S("--anim-lab", "--fly", "--stunt", "--viewer", "--damage", "--freecam");
        Assert.Equal(SessionMode.AnimLab, s.Mode);
        Assert.False(s.Stunt);
        Assert.False(s.DamageLab);
    }

    /// <summary>⚠ The modifiers are the assertion that bites here. <c>Mode</c> alone cannot tell
    /// this rule from the viewer-beats-fly one below — with `--viewer` also on the line, that rule
    /// clears the same modifiers and the mode ternary reaches Freecam either way. So the case that
    /// isolates the freecam rule carries NO other mode flag: the stunt and damage-lab modifiers have
    /// nobody else to clear them.</summary>
    [Fact]
    public void FreecamBeatsFlightAndTheViewer()
    {
        var s = S("--freecam", "--fly", "--stunt", "--viewer", "--damage");
        Assert.Equal(SessionMode.Freecam, s.Mode);
        Assert.False(s.Stunt);
        Assert.False(s.DamageLab);

        var stunting = S("--freecam", "--stunt");
        Assert.Equal(SessionMode.Freecam, stunting.Mode);
        Assert.False(stunting.Stunt);

        var damaged = S("--freecam", "--damage");
        Assert.Equal(SessionMode.Freecam, damaged.Mode);
        Assert.False(damaged.DamageLab);
    }

    [Fact]
    public void TheViewerBeatsFlight()
    {
        var s = S("--viewer", "--fly", "--stunt");
        Assert.Equal(SessionMode.Viewer, s.Mode);
        Assert.False(s.Stunt);
    }

    [Fact]
    public void ANodeStageForcesTheViewerAndItsChapter()
    {
        var s = S("--node=hk_zep", "--fly", "--freecam");
        Assert.Equal(SessionMode.Viewer, s.Mode);
        // The subtree comes out of the chapter's gamez, so asking for one asks for that world.
        Assert.True(s.ChapterGiven);
    }

    [Fact]
    public void ANodeStageDoesNotBeatTheAnimLab()
    {
        Assert.Equal(SessionMode.AnimLab, S("--anim-lab", "--node=hk_zep").Mode);
    }

    [Theory]
    [InlineData("--damage")]
    [InlineData("--markers")]
    [InlineData("--weapon-lab")]
    [InlineData("--weapon-mount=firepoint0")]
    [InlineData("--weapon-fire")]
    [InlineData("--weapon-test")]
    public void TheseFlagsVoteForTheViewer(string arg) => Assert.Equal(SessionMode.Viewer, S(arg).Mode);

    [Theory]
    [InlineData("--damage-test")]
    [InlineData("--effects-test")]
    public void TheseProbesVoteForTheFreecam(string arg) => Assert.Equal(SessionMode.Freecam, S(arg).Mode);

    [Theory]
    [InlineData("--play-anim=waterfall")]
    [InlineData("--debug-anim-ui")]
    public void TheseFlagsVoteForTheAnimLab(string arg) => Assert.Equal(SessionMode.AnimLab, S(arg).Mode);

    /// <summary>The four mode bools are computed from <c>Mode</c>, so exactly one is ever true —
    /// which is the property four hand-written predicates used to get wrong one term at a time.</summary>
    [Theory]
    [InlineData("--fly")]
    [InlineData("--viewer")]
    [InlineData("--freecam")]
    [InlineData("--anim-lab")]
    public void AtMostOneModeBoolIsEverTrue(string arg)
    {
        var s = S(arg);
        int on = (s.Fly ? 1 : 0) + (s.Viewer ? 1 : 0) + (s.Freecam ? 1 : 0) + (s.AnimLab ? 1 : 0);
        Assert.True(on <= 1, $"{arg} lit {on} mode bools");
    }

    [Fact]
    public void TheProbeThatCoercedTheModeIsNamed()
    {
        Assert.Equal(SessionProbe.DamageTest, S("--damage-test").Probe);
        Assert.Equal(SessionProbe.EffectsTest, S("--effects-test").Probe);
        Assert.Equal(SessionProbe.WeaponTest, S("--weapon-test").Probe);
        Assert.Equal(SessionProbe.None, S("--freecam").Probe);
    }

    // ---- Step order IS the behaviour -----------------------------------------------------------

    /// <summary>`--stunt` moves the spawn list BEFORE arbitration can clear the stunt flag, so a
    /// mode that beats flight still inherits the stunt spawn list. Reordering those two steps is a
    /// behaviour change that looks like tidying.</summary>
    [Fact]
    public void StuntPicksItsSpawnListBeforeArbitrationCanClearIt()
    {
        var s = S("--anim-lab", "--stunt");
        Assert.Equal(SessionMode.AnimLab, s.Mode);
        Assert.False(s.Stunt);
        Assert.Equal("stunt_flying", s.Scenario);
    }

    [Fact]
    public void APinnedScenarioSurvivesStunt()
    {
        var s = S("--stunt", "--scenario=hangar_run");
        Assert.True(s.Stunt);
        Assert.Equal("hangar_run", s.Scenario);
        Assert.True(s.ScenarioExplicit);
    }

    // ---- The launchscreen decision -------------------------------------------------------------

    [Fact]
    public void MenuForcesTheLaunchscreenAlongsideAContentArg()
    {
        var s = S("--menu", "--chapter=C4");
        Assert.Equal(SessionMode.Fly, s.Mode);
        Assert.True(s.ShowsMenu);
    }

    /// <summary>⚠ KNOWN DEFECT, asserted as it is: `--run-tests` resolves to a session that would
    /// show the launchscreen. The harness is saved only by returning before the menu branch, so the
    /// correctness of the test runner rests on statement order in the caller.</summary>
    [Fact]
    public void RunTestsStillResolvesToShowingTheMenu()
    {
        var s = S("--run-tests");
        Assert.False(s.HasContentArg);
        Assert.True(s.ShowsMenu);
    }

    // ---- ModeName: the log file's name and the startup line's ----------------------------------

    [Theory]
    [InlineData("anim-lab", "--anim-lab")]
    [InlineData("test", "--damage-test")]
    [InlineData("test", "--run-tests")]
    [InlineData("dump", "--dump-markers")]
    [InlineData("dump", "--dump-config")]
    [InlineData("freecam", "--freecam")]
    [InlineData("viewer", "--viewer")]
    [InlineData("stunt", "--stunt")]
    [InlineData("fly", "--fly")]
    public void TheSessionShapeNamesItself(string expected, string arg)
        => Assert.Equal(expected, S(arg).ModeName);

    /// <summary>⚠ KNOWN DEFECT, asserted as it is: `--dump-flight` is missing from the "dump" arm,
    /// so it logs as a menu session.</summary>
    [Fact]
    public void DumpFlightIsMissingFromTheDumpArm()
        => Assert.Equal("menu", S("--dump-flight").ModeName);

    // ---- Scripted sessions ---------------------------------------------------------------------

    [Fact]
    public void TheFlagDrivingTheSessionIsNamedInPrecedenceOrder()
    {
        Assert.Equal("--screenshot", S("--screenshot=a.png", "--run-tests", "--damage-test").ScriptedBy);
        Assert.Equal("--damage-test", S("--damage-test", "--run-tests").ScriptedBy);
        Assert.Equal("--run-tests", S("--run-tests").ScriptedBy);
        Assert.Equal("", S("--fly").ScriptedBy);
    }

    [Theory]
    [InlineData("--no-focus")]
    [InlineData("--screenshot=a.png")]
    [InlineData("--run-tests")]
    [InlineData("--dump-markers")]
    [InlineData("--dump-weapons")]
    [InlineData("--dump-loadout")]
    [InlineData("--dump-config")]
    [InlineData("--damage-test")]
    [InlineData("--effects-test")]
    [InlineData("--weapon-test")]
    public void AScriptedSessionHidesItsWindow(string arg) => Assert.True(S(arg).IsScripted);

    [Theory]
    [InlineData("--fly")]
    [InlineData("--viewer")]
    [InlineData("--freecam")]
    [InlineData("--anim-lab")]
    public void AnInteractiveSessionAsksForFocus(string arg) => Assert.False(S(arg).IsScripted);

    /// <summary>⚠ KNOWN DRIFT, asserted as it is: `--dump-flight` drives and ends the session (so it
    /// turns the <c>--det</c> bundle on) yet is not a term of <see cref="SessionSpec.IsScripted"/>,
    /// so its window still asks for focus. Same omission as the ModeName arm above.</summary>
    [Fact]
    public void DumpFlightTurnsDetOnButIsNotCountedAsScripted()
    {
        var s = S("--dump-flight");
        Assert.Equal("--dump-flight", s.ScriptedBy);
        Assert.True(s.Det);
        Assert.False(s.IsScripted);
    }

    // ---- The --det bundle ----------------------------------------------------------------------

    [Theory]
    [InlineData("--screenshot=a.png")]
    [InlineData("--dump-markers")]
    [InlineData("--damage-test")]
    [InlineData("--run-tests")]
    [InlineData("--det")]
    public void TheseTurnTheDeterministicBundleOn(string arg) => Assert.True(S(arg).Det);

    /// <summary>The boundary the bundle must never cross: a bare interactive launch keeps its
    /// randomness.</summary>
    [Fact]
    public void ABareFlightIsNotDeterministic()
    {
        var s = S("--fly");
        Assert.False(s.Det);
        Assert.False(s.SeedPinned);
        Assert.False(s.PadsDisabled);
        Assert.True(s.SpawnIndex < 0);
    }

    [Fact]
    public void NoDetBeatsBothTheImplicationAndAnExplicitDet()
    {
        Assert.False(S("--screenshot=a.png", "--no-det").Det);
        Assert.False(S("--det", "--no-det").Det);
        Assert.False(S("--no-det", "--det").Det);
    }

    [Fact]
    public void WhatTurnedTheBundleOnIsAnnounced()
    {
        Assert.Equal("--det", S("--det").DetVia);
        Assert.Equal("--screenshot", S("--screenshot=a.png").DetVia);
        // An explicit --det wins the attribution even when a scripted flag would also imply it.
        Assert.Equal("--det", S("--det", "--run-tests").DetVia);
    }

    /// <summary>A pinned CHOICE beats a pinned dice roll — a seeded pick still moves when the
    /// mission's spawn list grows. An explicit `--spawn=N` still wins.</summary>
    [Fact]
    public void DetPinsTheSpawnToIndexZeroUnlessOneWasNamed()
    {
        Assert.Equal(0, S("--det").SpawnIndex);
        Assert.Equal(3, S("--det", "--spawn=3").SpawnIndex);
        Assert.Equal(-1, S("--fly").SpawnIndex);
    }

    /// <summary>A burst dithers the camera so z-fighting flickers across frames; `--det` defaults it
    /// off instead, because bit-identical frames are the one property a deterministic run is for.</summary>
    [Fact]
    public void TheJitterDefaultFollowsTheBurstAndTheBundle()
    {
        Assert.Equal(0f, S("--fly").JitterDeg);
        Assert.Equal(0.15f, S("--shots=4", "--no-det").JitterDeg);
        Assert.Equal(0f, S("--shots=4", "--det").JitterDeg);
        Assert.Equal(0.5f, S("--shots=4", "--jitter=0.5").JitterDeg);
    }

    [Fact]
    public void EveryPadIsIgnoredWhenAskedOrWhenDeterministic()
    {
        Assert.True(S("--no-pads").PadsDisabled);
        Assert.True(S("--det").PadsDisabled);
        Assert.False(S("--fly").PadsDisabled);
    }

    [Fact]
    public void TheMasterSeedIsPinnedByASeedByDetAndByTheAnimLab()
    {
        Assert.True(S("--seed=9").SeedPinned);
        Assert.True(S("--det").SeedPinned);
        // The animation debugger is deterministic by nature: its whole point is an identical replay.
        Assert.True(S("--anim-lab").SeedPinned);
        Assert.False(S("--freecam").SeedPinned);
    }

    /// <summary>An unpinned seed is drawn from the clock by the CALLER, not here — a spec that read
    /// the clock would not be a function of its args, and a baseline would differ from itself
    /// (DET-9).</summary>
    [Fact]
    public void AnUnpinnedSeedIsNullRatherThanDrawnHere()
    {
        Assert.Null(S("--fly").PinnedSeed);
        Assert.Equal(Rng.DefaultSeed, S("--det").PinnedSeed);
        Assert.Equal(9ul, S("--seed=9").PinnedSeed);
        // Same args, same answer — twice, because that is the property at stake.
        Assert.Equal(S("--fly").PinnedSeed, S("--fly").PinnedSeed);
    }

    // ---- BuildsCollision: the predicate three consumers used to spell for themselves ------------

    [Theory]
    [InlineData("--fly")]
    [InlineData("--damage-test")]
    [InlineData("--collision", "--freecam")]
    [InlineData("--debug-damage", "--freecam")]
    public void TheseSessionsBuildTheWorldsColliders(params string[] args)
        => Assert.True(S(args).BuildsCollision);

    [Theory]
    [InlineData("--freecam")]
    [InlineData("--viewer")]
    [InlineData("--anim-lab")]
    public void TheseDoNotBuildColliders(string arg) => Assert.False(S(arg).BuildsCollision);

    /// <summary>`--debug-damage` is dropped outside the two observation modes, so it stops being a
    /// term there — flight still builds colliders on its own account.</summary>
    [Fact]
    public void DebugDamageIsNotATermWhereItIsDropped()
    {
        var s = S("--viewer", "--debug-damage");
        Assert.Null(s.DebugDamage);
        Assert.False(s.BuildsCollision);
    }

    // ---- WorldMode and the empty stage ---------------------------------------------------------

    [Theory]
    [InlineData("--fly")]
    [InlineData("--freecam")]
    [InlineData("--anim-lab")]
    public void FlightAndTheObservationModesAlwaysBuildTheWorld(string arg)
        => Assert.True(S(arg).WorldMode);

    [Fact]
    public void TheViewerBuildsAWorldOnlyWhenAChapterWasAskedFor()
    {
        Assert.False(S("--viewer", "--plane=player_fury").WorldMode);
        Assert.True(S("--viewer", "--chapter=C4").WorldMode);
        // --node= asks for a chapter implicitly.
        Assert.True(S("--node=hk_zep").WorldMode);
    }

    [Fact]
    public void TheEmptyStageReplacesTheWorldEntirely()
    {
        var s = S("--stage=empty");
        Assert.True(s.EmptyStage);
        Assert.False(s.WorldMode);
    }

    [Fact]
    public void AnUnknownStageIsIgnoredLoudly()
    {
        var s = S("--stage=nowhere");
        Assert.False(s.EmptyStage);
        Assert.Contains(s.Warnings, w => w.Message.Contains("not a known stage"));
    }

    /// <summary>The empty stage has no gamez to inspect, which is what the viewer, the anim lab and
    /// the node stage exist for.</summary>
    [Theory]
    [InlineData("--viewer")]
    [InlineData("--anim-lab")]
    [InlineData("--node=hk_zep")]
    public void TheEmptyStageIsRefusedWhereThereIsNothingToInspect(string arg)
    {
        var s = S("--stage=empty", arg);
        Assert.False(s.EmptyStage);
        Assert.Contains(s.Warnings, w => w.Message.Contains("no gamez to inspect"));
    }

    // ---- Players and the plane list ------------------------------------------------------------

    [Fact]
    public void APlaneListStatesThePlayerCountAndAnExplicitCountStillWins()
    {
        Assert.Equal(2, S("--fly", "--plane=player_bhawk,player_fury").Players);
        Assert.Equal(1, S("--fly", "--plane=player_bhawk,player_fury", "--players=1").Players);
        Assert.Equal("player_bhawk", S("--fly", "--plane=player_bhawk,player_fury").PlaneName);
    }

    [Fact]
    public void ThePlayerCountIsClampedToTheRigsCapacity()
        => Assert.Equal(UI.SplitScreen.MaxPlayers, S("--fly", "--players=9").Players);

    /// <summary>Splitscreen is a flight mode: it needs planes to fly.</summary>
    [Fact]
    public void AStaticViewFallsBackToOnePlayer()
    {
        var s = S("--viewer", "--players=3");
        Assert.Equal(1, s.Players);
        Assert.Contains(s.Warnings, w => w.Message.Contains("needs flight"));
    }

    [Fact]
    public void TheDamageLabIsThePlaneLabAndIsDroppedOverAWorld()
    {
        Assert.True(S("--damage").DamageLab);
        var s = S("--damage", "--chapter=C4");
        Assert.False(s.DamageLab);
        Assert.Contains(s.Warnings, w => w.Message.Contains("--damage is the plane lab"));
    }

    // ---- Tools that only exist in some modes ---------------------------------------------------

    /// <summary>The numpad views orbit a FLYING plane; every other mode places its camera with
    /// `--pos`/`--direction` instead.</summary>
    [Fact]
    public void TheNumpadViewsAreDroppedOutsideFlight()
    {
        Assert.Equal(2, S("--fly", "--view=2").View);
        var s = S("--viewer", "--view=2");
        Assert.Equal(0, s.View);
        Assert.Contains(s.Warnings, w => w.Category == "core" && w.Message.Contains("flight camera"));
    }

    [Fact]
    public void AViewDigitWithNoPerspectiveFallsBackToTheChaseCamera()
    {
        var s = S("--fly", "--view=5");
        Assert.Equal(0, s.View);
        Assert.Contains(s.Warnings, w => w.Message.Contains("not a numpad view"));
    }

    /// <summary>The shared selection lives in the two world-observation modes: the viewer's LMB is
    /// already the orbit drag, and flight has no cursor.</summary>
    [Theory]
    [InlineData("--debug-select")]
    [InlineData("--debug-nodelab")]
    [InlineData("--debug-damage")]
    public void TheSelectionToolsExistOnlyInFreecamAndTheAnimLab(string tool)
    {
        Assert.NotNull(Value(S("--freecam", tool), tool));
        Assert.NotNull(Value(S("--anim-lab", tool), tool));
        Assert.Null(Value(S("--viewer", tool), tool));
        Assert.Null(Value(S("--fly", tool), tool));

        static string? Value(SessionSpec s, string tool) => tool switch
        {
            "--debug-select" => s.DebugSelect,
            "--debug-nodelab" => s.DebugNodeLab,
            _ => s.DebugDamage,
        };
    }

    /// <summary>A lab's spec value is filtered through that lab's own token grammar, so an unknown
    /// token is dropped as data rather than reaching the lab.</summary>
    [Fact]
    public void ALabSpecKeepsOnlyTheTokensItsLabUnderstands()
    {
        var s = S("--freecam", "--debug-nodelab=deps,bogus,dest");
        Assert.Equal("deps,dest", s.DebugNodeLab);
        Assert.Contains(s.Warnings, w => w.Category == "ui" && w.Message.Contains("bogus"));
    }

    // ---- Placement -----------------------------------------------------------------------------

    [Fact]
    public void ThePlacementPairMovesThePlaneInFlight()
    {
        var s = S("--fly", "--pos=10,20,30", "--direction=0,0,-1");
        Assert.Equal(new Vector3(10, 20, 30), s.SpawnAt);
        Assert.Equal(new Vector3(0, 0, -1), s.SpawnDir);
        Assert.Null(s.CamPos);
        Assert.Null(s.CamDir);
    }

    [Fact]
    public void ThePlacementPairMovesTheCameraEverywhereElse()
    {
        var s = S("--freecam", "--pos=10,20,30", "--direction=0,0,-1");
        Assert.Equal(new Vector3(10, 20, 30), s.CamPos);
        Assert.Equal(new Vector3(0, 0, -1), s.CamDir);
        Assert.Null(s.SpawnAt);
        Assert.Null(s.SpawnDir);
    }

    /// <summary>`--lookat` names a POINT and `--direction` a VECTOR; the conversion is one-way and
    /// flight-only, because the orbit view PIVOTS on the point and no direction can express that.</summary>
    [Fact]
    public void ALookAtPointBecomesADirectionInFlightOnly()
    {
        var flying = S("--fly", "--pos=0,0,0", "--lookat=0,0,-5");
        Assert.Equal(new Vector3(0, 0, -1), flying.SpawnDir);

        var looking = S("--viewer", "--pos=0,0,0", "--lookat=0,0,-5");
        Assert.Null(looking.CamDir);
        Assert.Equal(new Vector3(0, 0, -5), looking.LookAt);
    }

    [Fact]
    public void ADirectionIsNormalisedAndADegenerateOneIsDropped()
    {
        Assert.Equal(new Vector3(1, 0, 0), S("--fly", "--pos=0,0,0", "--direction=7,0,0").SpawnDir);
        Assert.Null(S("--fly", "--pos=0,0,0", "--direction=0,0,0").Direction);
    }

    /// <summary>The superseded spellings keep their old per-mode reach: `--campos` places a camera
    /// and never the plane, in any mode.</summary>
    [Fact]
    public void CamposNeverPlacesThePlane()
    {
        var s = S("--fly", "--campos=1,2,3");
        Assert.Equal(new Vector3(1, 2, 3), s.CamPos);
        Assert.Null(s.SpawnAt);
        Assert.Contains(s.Deprecated, d => d.Old == "--campos" && d.New == "--pos");
    }

    [Fact]
    public void ADeprecatedSpellingIsReportedOnceWithItsReplacement()
    {
        var s = S("--spawn-at=1,2,3", "--spawn-at=4,5,6", "--spawn-dir=0,0,-1");
        Assert.Equal(new[] { "--spawn-at", "--spawn-dir" }, s.Deprecated.Select(d => d.Old));
    }

    [Fact]
    public void AmmoSetsTheCap()
    {
        var s = S("--ammo=3");
        Assert.Equal(3, s.AmmoCap);
        Assert.False(s.InfiniteAmmo);
    }

    /// <summary>Mutually exclusive with `--infinite-ammo`: whichever comes last on the command line
    /// wins, and the loser is logged.</summary>
    [Fact]
    public void AmmoAndInfiniteAmmoAreMutuallyExclusiveByOrder()
    {
        var ammoWins = S("--infinite-ammo", "--ammo=3");
        Assert.Equal(3, ammoWins.AmmoCap);
        Assert.False(ammoWins.InfiniteAmmo);
        Assert.Contains(ammoWins.Warnings, w => w.Message.Contains("--ammo="));

        var infiniteWins = S("--ammo=3", "--infinite-ammo");
        Assert.Null(infiniteWins.AmmoCap);
        Assert.True(infiniteWins.InfiniteAmmo);
        Assert.Contains(infiniteWins.Warnings, w => w.Message.Contains("--infinite-ammo"));
    }

    /// <summary>A nose direction with nothing to place it on is a silently ignored argument, so it
    /// says so.</summary>
    [Fact]
    public void ADirectionWithoutAPositionIsCalledOutInFlight()
    {
        var s = S("--fly", "--spawn-dir=0,0,-1");
        Assert.Contains(s.Warnings, w => w.Message.Contains("--direction ignored"));
    }

    /// <summary>The empty stage has no mission spawn list, so the subject starts over the grid
    /// origin — through the same fields `--pos` resolves into, so an explicit placement still wins.</summary>
    [Fact]
    public void TheEmptyStageDefaultsThePlacementItHasNoSpawnListFor()
    {
        Assert.Equal(new Vector3(0f, Mech3.EmptyStage.SpawnAltitude, 0f), S("--stage=empty").SpawnAt);
        Assert.Equal(Mech3.EmptyStage.CameraPos, S("--stage=empty", "--freecam").CamPos);
        Assert.Equal(new Vector3(1, 2, 3), S("--stage=empty", "--pos=1,2,3").SpawnAt);
    }

    // ---- Parse conventions ---------------------------------------------------------------------

    [Fact]
    public void TheLastOccurrenceOfAFlagWins()
    {
        Assert.Equal("C5", S("--chapter=C1", "--chapter=C5").Chapter);
        Assert.Equal(7ul, S("--seed=1", "--seed=7").Seed);
    }

    [Fact]
    public void AnUnrecognisedArgIsIgnoredSilently()
    {
        var s = S("--fly", "--no-such-flag", "nonsense");
        Assert.Equal(SessionMode.Fly, s.Mode);
        Assert.Empty(s.Warnings);
    }

    /// <summary>Path flags are override VALUES, null when unset — the default arithmetic belongs to
    /// the caller, which is why nothing here builds a path.</summary>
    [Fact]
    public void PathFlagsAreOverrideValuesOnly()
    {
        var bare = S("--fly");
        Assert.Null(bare.Gamez);
        Assert.Null(bare.Textures);
        Assert.Null(bare.DataRoot);

        var s = S("--gamez=g.zip", "--textures=t.zip", "--data-root=/somewhere");
        Assert.Equal("g.zip", s.Gamez);
        Assert.Equal("t.zip", s.Textures);
        Assert.Equal("/somewhere", s.DataRoot);
    }

    /// <summary>Globals are recorded, never applied — that is what keeps the type reachable from
    /// here, with no engine under it.</summary>
    [Fact]
    public void TheSpecRecordsGlobalsRatherThanApplyingThem()
    {
        var s = S("--tex-override=sky=ff00ff", "--log=anim:debug", "--no-pads");
        Assert.Equal(new[] { "sky=ff00ff" }, s.TexOverrides);
        Assert.Equal(new[] { "anim:debug" }, s.LogSpecs);
        Assert.True(s.NoPads);
    }

    /// <summary>`--debug-anim`'s implied "anim:debug,sound:debug" is the caller's, not a parsed
    /// spec — the flag records itself and nothing more.</summary>
    [Fact]
    public void DebugAnimDoesNotSynthesiseALogSpec()
    {
        var s = S("--debug-anim");
        Assert.True(s.DebugAnim);
        Assert.Empty(s.LogSpecs);
    }

    private static SessionSpec S(params string[] args) => SessionSpec.Parse(args);
}
