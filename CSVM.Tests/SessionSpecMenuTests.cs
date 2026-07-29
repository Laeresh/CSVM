using System.Collections.Generic;
using CSVM;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// What a launchscreen launch resolves to. The launchscreen is the one path with no automated
/// coverage anywhere else — the pixel goldens never open it and the resolution baseline drives the
/// CLI only — so these are the only facts standing behind it.
/// </summary>
public class SessionSpecMenuTests
{
    private static SessionSpec Cli(params string[] args) => SessionSpec.Parse(args);

    private static SessionSpec Menu(SessionSpec cli, string chapter, bool stunt, params string[] planes)
        => SessionSpec.FromMenu(cli, chapter, planes, stunt);

    [Fact]
    public void AMenuLaunchIsAlwaysFlightOverAChapterWorld()
    {
        var spec = Menu(Cli(), "C4", stunt: false, "player_fury");

        Assert.Equal(SessionMode.Fly, spec.Mode);
        Assert.True(spec.Fly);
        Assert.True(spec.WorldMode);
        Assert.False(spec.Viewer);
        Assert.Equal("C4", spec.Chapter);
        Assert.Equal("player_fury", spec.PlaneName);
        Assert.Equal(1, spec.Players);
    }

    /// <summary>A bare launch resolves to the menu, and the menu's pick turns it into flight —
    /// the CLI spec itself is never flight, so this is the whole reason the factory exists.</summary>
    [Fact]
    public void ABareLaunchShowsTheMenuAndTheMenuPickFliesIt()
    {
        var cli = Cli();
        Assert.Equal(SessionMode.Menu, cli.Mode);
        Assert.True(cli.ShowsMenu);

        Assert.Equal(SessionMode.Fly, Menu(cli, "C1", stunt: false, "player_bhawk").Mode);
    }

    /// <summary>The menu launches flight even when the command line asked for a static view: the
    /// pick is an answer, not another vote. `--viewer --menu` is the launch that proves it.</summary>
    [Fact]
    public void TheMenuBeatsAViewerCommandLine()
    {
        var cli = Cli("--viewer", "--menu", "--plane=player_kestrel");
        Assert.Equal(SessionMode.Viewer, cli.Mode);

        var spec = Menu(cli, "C2", stunt: false, "player_fury");
        Assert.Equal(SessionMode.Fly, spec.Mode);
        Assert.False(spec.Viewer);
        Assert.Equal("player_fury", spec.PlaneName);
    }

    [Fact]
    public void OnePlanePerPlayerStatesThePlayerCount()
    {
        var spec = Menu(Cli(), "C1", stunt: false, "player_bhawk", "player_fury", "player_kestrel");

        Assert.Equal(3, spec.Players);
        Assert.Equal(new[] { "player_bhawk", "player_fury", "player_kestrel" }, spec.PlaneNames);
        // Every single-plane path reads PlaneName, which is the first pick.
        Assert.Equal("player_bhawk", spec.PlaneName);
    }

    [Fact]
    public void AStuntPickTakesTheStuntSpawnListAndAFreeFlightPickTakesTheDefault()
    {
        Assert.Equal("stunt_flying", Menu(Cli(), "C1", stunt: true, "player_bhawk").Scenario);
        Assert.True(Menu(Cli(), "C1", stunt: true, "player_bhawk").Stunt);

        Assert.Equal("zeppelin_run", Menu(Cli(), "C1", stunt: false, "player_bhawk").Scenario);
        Assert.False(Menu(Cli(), "C1", stunt: false, "player_bhawk").Stunt);
    }

    /// <summary>A tester who pinned a scenario alongside a bare launch keeps it, in both modes —
    /// the re-derivation is a default, not an override.</summary>
    [Fact]
    public void AnExplicitScenarioSurvivesEitherMenuMode()
    {
        var cli = Cli("--scenario=hangar_run");

        Assert.Equal("hangar_run", Menu(cli, "C1", stunt: false, "player_bhawk").Scenario);
        Assert.Equal("hangar_run", Menu(cli, "C1", stunt: true, "player_bhawk").Scenario);
    }

    /// <summary>Nothing a previous launch settled reaches the next one. Every field the factory
    /// writes is asserted against the SECOND pick, not the first — this is the property that used
    /// to rest on three hand-written patches in the caller. Each expected value differs from BOTH
    /// the first pick and the parse default, so a dropped write cannot pass by landing on one.</summary>
    [Fact]
    public void ASecondLaunchKeepsNothingFromTheFirst()
    {
        var cli = Cli();
        var first = Menu(cli, "C4", stunt: true, "player_fury", "player_bhawk");
        Assert.Equal("stunt_flying", first.Scenario);
        Assert.Equal(2, first.Players);

        var second = SessionSpec.FromMenu(cli, "C2", new[] { "player_kestrel" }, stunt: false);
        Assert.Equal("C2", second.Chapter);
        Assert.Equal("player_kestrel", second.PlaneName);
        Assert.Equal(new[] { "player_kestrel" }, second.PlaneNames);
        Assert.Equal(1, second.Players);
        Assert.False(second.Stunt);
        Assert.Equal("zeppelin_run", second.Scenario);
    }

    /// <summary>Deriving from the pristine spec and patching the live one agree on every field the
    /// factory writes, which is what makes the change from one to the other behaviour-neutral. The
    /// second launch is the case that could have differed: it is the first one whose base carries a
    /// previous pick.</summary>
    [Fact]
    public void DerivingFreshAgreesWithPatchingTheLiveSpec()
    {
        var cli = Cli("--menu");
        var first = Menu(cli, "C4", stunt: true, "player_fury", "player_bhawk");

        var fresh = SessionSpec.FromMenu(cli, "C2", new[] { "player_kestrel" }, stunt: false);
        var patched = SessionSpec.FromMenu(first, "C2", new[] { "player_kestrel" }, stunt: false);

        Assert.Equal(fresh.Chapter, patched.Chapter);
        Assert.Equal(fresh.PlaneName, patched.PlaneName);
        Assert.Equal(fresh.PlaneNames, patched.PlaneNames);
        Assert.Equal(fresh.Players, patched.Players);
        Assert.Equal(fresh.Stunt, patched.Stunt);
        Assert.Equal(fresh.Mode, patched.Mode);
        Assert.Equal(fresh.WorldMode, patched.WorldMode);
        Assert.Equal(fresh.Scenario, patched.Scenario);
    }

    /// <summary>The command line still settles everything the pick does not name — the pick is a
    /// chapter, some planes and a mode, not a whole session.</summary>
    [Fact]
    public void EverythingThePickDoesNotNameStillComesFromTheCommandLine()
    {
        var cli = Cli("--menu", "--mission=M02", "--seed=7", "--mute", "--no-fog", "--anim-lod=1");
        var spec = Menu(cli, "C3", stunt: false, "player_bhawk");

        Assert.Equal("M02", spec.Mission);
        Assert.Equal(7ul, spec.Seed);
        Assert.True(spec.Mute);
        Assert.True(spec.NoFog);
        Assert.Equal(1, spec.AnimLod);
    }

    /// <summary>Splitscreen is capped by the rig, not by the pick.</summary>
    [Fact]
    public void ThePlayerCountIsClampedToTheRigsCapacity()
    {
        var many = new List<string>();
        for (int i = 0; i < 9; i++)
        {
            many.Add("player_bhawk");
        }
        Assert.Equal(UI.SplitScreen.MaxPlayers, SessionSpec.FromMenu(Cli(), "C1", many, stunt: false).Players);
    }
}
