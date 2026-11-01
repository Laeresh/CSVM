using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using CSVM.Spec;
using CSVM.UI.Menu;
using CSVM.UI.Screens;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Instant Action's Course option over a stand-in course list. It covers the chapters it makes
/// eligible for stunt flying, its rows, the solo rule and its exit. The spec that exit derives is
/// set against the one <c>--stunt --course=</c> builds.
/// </summary>
public class InstantActionCourseTests
{
    private const string Sample = "sample_out_and_back";

    private static readonly MenuSeatChoice Seat = new("player_fury", Array.Empty<int>());

    [Fact]
    public void ACourseMakesAChapterWithoutDangerZonesEligibleAndLeavesTheLobbyRuleAlone()
    {
        var bare = Feature(new Dictionary<string, string[]>());
        var withCourse = Feature(new Dictionary<string, string[]> { ["C2B"] = new[] { "cloud_loop" } });

        Assert.False(bare.StuntOffered("C2B"));
        Assert.False(bare.AllowsEnvironment(1, InstantActionFeature.StuntKey));
        Assert.True(withCourse.StuntOffered("C2B"));
        Assert.True(withCourse.AllowsEnvironment(1, InstantActionFeature.StuntKey));
        // The local lobby's Stunt Race and the static rule read the Danger Zones alone.
        Assert.False(MenuChapters.DangerZonesFor("C2B"));
        Assert.False(InstantActionFeature.EnvironmentAllowed(1, InstantActionFeature.StuntKey));
        Assert.DoesNotContain(InstantActionFeature.MissionTypesFor("C2B"), m => m.Key == InstantActionFeature.StuntKey);

        withCourse.SelectEnvironment(1);
        withCourse.ConfirmEnvironment();
        Assert.Contains(withCourse.MissionTypes, m => m.Key == InstantActionFeature.StuntKey);
        // No Danger Zones to fall back on, so the chapter opens on its first course with no such row.
        Assert.Equal(new[] { "cloud_loop" }, withCourse.CourseOptions);
        Assert.Equal("cloud_loop", withCourse.Course);
    }

    [Fact]
    public void TheCourseRowsAreTheDangerZonesThenTheChaptersCoursesByName()
    {
        var ia = StuntOnC1();

        Assert.Equal(new[] { InstantActionFeature.DangerZonesLabel, "a_loop", Sample }, ia.CourseOptions);
        Assert.Null(ia.Course);
        Assert.Equal(0, ia.CourseIndex);
        ia.SelectCourse(2);
        Assert.Equal(Sample, ia.Course);
        Assert.Equal(2, ia.CourseIndex);
        ia.SelectCourse(0);
        Assert.Null(ia.Course);
        Assert.Throws<ArgumentOutOfRangeException>(() => ia.SelectCourse(3));

        // A chapter without the picked course drops the pick; a discard drops it too.
        ia.SelectCourse(2);
        ia.SelectEnvironment(2);
        ia.ConfirmEnvironment();
        Assert.Null(ia.Course);
        ia.SelectEnvironment(0);
        ia.ConfirmEnvironment();
        ia.SelectCourse(1);
        ia.Discard();
        Assert.Null(ia.Course);
    }

    [Fact]
    public void ACourseIsOfferedToOneSeatWithNoBotRowsOnAStuntRunAlone()
    {
        var ia = StuntOnC1();
        ia.SelectCourse(2);

        Assert.True(ia.OffersCourse(1, 0));
        Assert.Equal(Sample, ia.LaunchCourse(1, 0));
        Assert.False(ia.OffersCourse(2, 0));
        Assert.Null(ia.LaunchCourse(2, 0));
        Assert.False(ia.OffersCourse(1, 1));
        Assert.Null(ia.LaunchCourse(1, 1));

        ia.SelectMissionType(0);
        Assert.False(ia.OffersCourse(1, 0));
        Assert.Null(ia.LaunchCourse(1, 0));
    }

    [Fact]
    public void ASoloCourseLeavesAsTheCourseWithNoDefAndASecondPilotOrABotFliesTheDangerZones()
    {
        var ia = StuntOnC1();
        ia.SelectCourse(2);

        var solo = ia.BuildExit(new[] { Seat });
        Assert.Equal("C1", solo.Chapter);
        Assert.Equal(MenuMode.Stunt, solo.Mode);
        Assert.Equal(Sample, solo.Course);
        Assert.Null(solo.InstantAction);
        Assert.Null(solo.Bots);
        Assert.Same(MenuReturnDestination.InstantAction, MenuReturnDestination.ForLaunch(solo));

        var two = ia.BuildExit(new[] { Seat, Seat });
        Assert.Null(two.Course);
        Assert.NotNull(two.InstantAction);

        var bot = new VsBotEntry("player_kestrel", Net.NetBotSkill.Veteran, 0, "Bot 1");
        var withBot = ia.BuildExit(new[] { Seat }, bots: new[] { bot });
        Assert.Null(withBot.Course);
        Assert.Equal(InstantActionFeature.StuntKey, withBot.InstantAction!.MissionType);
    }

    [Fact]
    public void TheCourseExitDerivesTheSpecTheCommandLineBuilds()
    {
        var ia = StuntOnC1();
        ia.SelectCourse(2);
        var exit = ia.BuildExit(new[] { Seat });

        var menu = SessionSpec.FromMenu(SessionSpec.Parse(Array.Empty<string>()), exit.Chapter,
            exit.Seats.Select(s => s.PlaneNode).ToList(), exit.Mode, exit.InstantAction, bots: exit.Bots, course: exit.Course);
        var cli = SessionSpec.Parse(new[] { "--stunt", $"--course=C1/{Sample}", $"--plane={Seat.PlaneNode}" });

        Assert.Equal(cli.Course, menu.Course);
        Assert.Null(menu.CourseRefusal);
        Assert.Equal(cli.CourseRefusal, menu.CourseRefusal);
        Assert.Equal(cli.Chapter, menu.Chapter);
        Assert.Equal(cli.Mission, menu.Mission);
        Assert.Equal(cli.Stunt, menu.Stunt);
        Assert.Equal(cli.Versus, menu.Versus);
        Assert.Equal(cli.Players, menu.Players);
        Assert.Equal(cli.PlaneName, menu.PlaneName);
        Assert.Equal(cli.IaDef, menu.IaDef);
        Assert.Equal(cli.VsBots.Count, menu.VsBots.Count);
    }

    [Fact]
    public void AMenuCourseKeepsTheCommandLinesRefusalsAndTheCommandLinesCourseRidesNoMenuLaunch()
    {
        var bare = SessionSpec.Parse(Array.Empty<string>());
        var two = SessionSpec.FromMenu(bare, "C1", new[] { "player_fury", "player_kestrel" }, MenuMode.Stunt, course: Sample);
        Assert.Equal(("C1", Sample), two.Course);
        Assert.NotNull(two.CourseRefusal);

        var flown = SessionSpec.Parse(new[] { $"--course=C1/{Sample}" });
        Assert.NotNull(flown.Course);
        var free = SessionSpec.FromMenu(flown, "C3", new[] { "player_fury" }, MenuMode.Free);
        Assert.Null(free.Course);
        Assert.Null(free.CourseRefusal);
        Assert.Equal("C3", free.Chapter);
    }

    [Fact]
    public void EveryPresetKeepsItsMissionTypeWhereCoursesWidenTheList()
    {
        var plain = new InstantActionFeature(_ => InstantAction.Defaults());
        var everywhere = new InstantActionFeature(_ => InstantAction.Defaults(), _ => new[] { "any" });
        for (int i = 0; i < InstantActionFeature.Presets.Count; i++)
        {
            plain.ApplyPreset(i);
            everywhere.ApplyPreset(i);
            Assert.Equal(plain.MissionType.Key, everywhere.MissionType.Key);
        }
    }

    [Fact]
    public void ACourseLoadScreenWritesItsNameAndOneLineInTheBlurbsPlace()
    {
        var lines = LoadScreens.Texts(null, "SAMPLE_OUT_AND_BACK", ZrdrSet.None, new Messages(), "Fly through every gate in order.");

        Assert.Equal(2, lines.Count);
        Assert.Equal(("SAMPLE_OUT_AND_BACK", 70f, 35f), (lines[0].Text, lines[0].X, lines[0].Y));
        Assert.Equal(("Fly through every gate in order.", 360f, 135f, 400f), (lines[1].Text, lines[1].X, lines[1].Y, lines[1].Width));
        Assert.Single(LoadScreens.Texts(null, "FREE FLIGHT", ZrdrSet.None, new Messages()));
    }

    private static InstantActionFeature Feature(Dictionary<string, string[]> courses) =>
        new(_ => InstantAction.Defaults(), code => courses.TryGetValue(code, out var names) ? names : Array.Empty<string>());

    // Stunt flying on the first environment, C1, which ships Danger Zones and two stand-in courses.
    private static InstantActionFeature StuntOnC1()
    {
        var ia = Feature(new Dictionary<string, string[]> { ["C1"] = new[] { "a_loop", Sample } });
        ia.ConfirmEnvironment();
        ia.SelectMissionType(2);
        Assert.Equal(InstantActionFeature.StuntKey, ia.MissionType.Key);
        return ia;
    }
}
