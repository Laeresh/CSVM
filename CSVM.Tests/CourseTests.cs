using System;
using System.IO;
using System.Linq;
using CSVM.Flight.Modes;
using CSVM.Launch;
using CSVM.Net;
using CSVM.Session.Roster;
using CSVM.Spec;
using Godot;
using Xunit;

namespace CSVM.Tests;

public class CourseTests
{
    private const string Json = """
        {
          "version": 1,
          "start": { "pos": [10, 120, -40.5], "heading": 90 },
          "gates": [
            { "id": "a", "kind": "pylons", "pos": [0, 12, -200], "heading": 0, "width": 40, "height": 30 },
            { "id": "b", "kind": "ring", "pos": [150.25, 80, -400], "heading": 45, "diameter": 25 }
          ],
          "sequence": [
            { "gate": "a", "dir": "forward" },
            { "gate": "b", "dir": "forward" },
            { "gate": "a", "dir": "reverse" }
          ]
        }
        """;

    private readonly string _shipped = TestData.TempDir();
    private readonly string _game = TestData.TempDir();

    [Fact]
    public void ParsesTheGatePoolAndASequenceThatReusesAGate()
    {
        var course = Parse(Json);

        Assert.Equal(new Vector3(10f, 120f, -40.5f), course.Start.Position);
        Assert.Equal(90f, course.Start.HeadingDeg);
        Assert.Equal(2, course.Gates.Count);
        Assert.Equal(new CourseGate("a", CourseGateKind.PylonGate, new Vector3(0f, 12f, -200f), 0f, 40f, 30f, 0f), course.Gates[0]);
        Assert.Equal(new CourseGate("b", CourseGateKind.BalloonRing, new Vector3(150.25f, 80f, -400f), 45f, 0f, 0f, 25f), course.Gates[1]);
        Assert.Equal(
            new[]
            {
                new SequenceEntry("a", GateDirection.Forward),
                new SequenceEntry("b", GateDirection.Forward),
                new SequenceEntry("a", GateDirection.Reverse),
            },
            course.Sequence);
    }

    [Fact]
    public void RoundTripsThroughJson()
    {
        var course = Parse(Json);
        var again = Parse(course.ToJson());

        Assert.Equal(course.Start, again.Start);
        Assert.Equal(course.Gates, again.Gates);
        Assert.Equal(course.Sequence, again.Sequence);
        Assert.Equal(course.ToJson(), again.ToJson());
    }

    [Fact]
    public void ATitleRoundTripsAndABlankOneShowsTheFileName()
    {
        var titled = Parse(Json.Replace("\"version\": 1,", "\"version\": 1, \"title\": \"Harbour Dash\",", StringComparison.Ordinal));
        var again = Parse(titled.ToJson());

        Assert.Equal(("Harbour Dash", "Harbour Dash"), (titled.Title, titled.DisplayName));
        Assert.Equal(titled.Title, again.Title);
        Assert.Equal(titled.ToJson(), again.ToJson());
        Assert.Equal(("", "Harbour"), (Parse(Json).Title, Parse(Json).DisplayName));
        var empty = Parse(Json.Replace("\"version\": 1,", "\"version\": 1, \"title\": \"\",", StringComparison.Ordinal));
        Assert.Equal("Harbour", empty.DisplayName);
        Assert.DoesNotContain("title", empty.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void ATitleIsNotInTheHash()
    {
        var course = Parse(Json);

        Assert.Equal(course.Hash, (course with { Title = "Harbour Dash" }).Hash);
        Assert.Equal(course.Hash, (course with { Title = "Something Else" }).Hash);
    }

    [Fact]
    public void APlanesListRoundTripsLimitsTheRulesAndStaysOutOfTheHash()
    {
        var limited = Parse(Json.Replace("\"version\": 1,", "\"version\": 1, \"planes\": [\"player_fury\", \"player_autogyro\"],", StringComparison.Ordinal));
        var again = Parse(limited.ToJson());

        Assert.Equal(new[] { 7, 0 }, limited.Planes);
        Assert.All(Enumerable.Range(0, 11), a => Assert.Equal(new[] { a }, Parse(Json.Replace("\"version\": 1,",
            $"\"version\": 1, \"planes\": [\"{Flight.Hangar.StockAirframes.Node(a)}\"],", StringComparison.Ordinal)).Planes));
        Assert.Equal(limited.Planes, again.Planes);
        Assert.Equal(limited.ToJson(), again.ToJson());
        Assert.Contains("\"player_fury\"", limited.ToJson(), StringComparison.Ordinal);
        Assert.Equal(Parse(Json).Hash, limited.Hash);
        Assert.True(limited.Admits(7) && limited.Admits(0) && !limited.Admits(2));
        Assert.Equal("it admits only player_fury, player_autogyro, not player_balmoral",
            limited.Refuses(new[] { "player_fury", "player_balmoral" }));
        Assert.Null(limited.Refuses(new[] { "player_fury", "no_such_node" }));

        // The bars refuse the airframe whatever Outlaw Components says, and leave the list as it stood.
        var rules = new NetPlaneRules(AllowCustom: true, Outlawing: false, Outlawed: 1UL << NetPlaneRules.NitroFlag);
        var raced = limited.Limits(rules);
        Assert.Equal(PlaneRefusal.Airframe, raced.Refuses(2, null));
        Assert.Equal((rules.AllowCustom, rules.Outlawing, rules.Outlawed), (raced.AllowCustom, raced.Outlawing, raced.Outlawed));
        Assert.Equal(PlaneRefusal.None, raced.Refuses(7, new NetPlaneBuild { Airframe = 7, Engine = 4 }));
        Assert.Equal(PlaneRefusal.Airframe, raced.Refuses(7, new NetPlaneBuild { Airframe = 2, Engine = 1 }));

        // ABLE-TO-FAIL CONTROL: a file without the list admits every plane, writes none and bars nothing.
        var open = Parse(Json);
        Assert.Null(open.Planes);
        Assert.DoesNotContain("planes", open.ToJson(), StringComparison.Ordinal);
        Assert.True(Enumerable.Range(0, 11).All(open.Admits));
        Assert.Equal(rules, open.Limits(rules));
        Assert.Equal(PlaneRefusal.None, open.Limits(rules).Refuses(2, null));
    }

    [Fact]
    public void ACommandLineLaunchOnAPlaneTheCourseRefusesIsRefusedAndItsRandomBotsDrawAroundIt()
    {
        var limited = Json.Replace("\"version\": 1,", "\"version\": 1, \"planes\": [\"player_fury\"],", StringComparison.Ordinal);
        WriteCourse(_shipped, "C2", "Limited", limited);
        WriteCourse(_shipped, "C2", "Open", Json);
        var library = new CourseLibrary(_shipped, null);
        var course = library.Load("C2", "Limited", out _)!;
        SessionSpec Cli(string name, params string[] args) => SessionSpec.Parse(args.Prepend($"--course=C2/{name}").ToArray());

        // What GameSession.ResolveCourse refuses the launch with, exit 1 on a command line.
        Assert.Equal("it admits only player_fury, not player_balmoral",
            SeatFields.CoursePlaneRefusal(Cli("Limited", "--plane=player_balmoral"), course));
        Assert.Equal("it admits only player_fury, not player_balmoral",
            SeatFields.CoursePlaneRefusal(Cli("Limited", "--plane=player_fury", "--vs-bot=player_fury,player_balmoral"), course));

        // The Launcher's local roster: a Random bot draws under the course's bar.
        var random = Cli("Limited", "--plane=player_fury", "--vs-bot=random");
        Assert.Null(SeatFields.CoursePlaneRefusal(random, course));
        var rules = SeatFields.CourseRules(random, library);
        Assert.True(rules is { } barred && barred.AirframeAllowed(7) && !barred.AirframeAllowed(2));
        Assert.All(Enumerable.Range(0, 50), seed => Assert.Equal("player_fury",
            BotSeats.Resolve(random.VsBots, Array.Empty<string>(), Array.Empty<string>(), new Random(seed), rules: rules)[0].Plane));

        // ABLE-TO-FAIL CONTROL: a course without the list bars nothing and refuses no plane.
        Assert.Null(SeatFields.CourseRules(Cli("Open", "--plane=player_fury", "--vs-bot=random"), library));
        Assert.Null(SeatFields.CoursePlaneRefusal(Cli("Open", "--plane=player_balmoral"), library.Load("C2", "Open", out _)!));
    }

    [Fact]
    public void LibraryDisplayNameFallsBackToTheFileName()
    {
        var library = new CourseLibrary(_shipped, _game);
        WriteCourse(_shipped, "C1", "titled", Json.Replace("\"version\": 1,", "\"version\": 1, \"title\": \"Harbour Dash\",", StringComparison.Ordinal));
        WriteCourse(_shipped, "C1", "plain", Json);
        WriteCourse(_shipped, "C1", "broken", "{ \"start\": 3 }");

        Assert.Equal("Harbour Dash", library.DisplayName("C1", "titled"));
        Assert.Equal("plain", library.DisplayName("C1", "plain"));
        Assert.Equal("broken", library.DisplayName("C1", "broken"));
        Assert.Equal("absent", library.DisplayName("C1", "absent"));
    }

    [Fact]
    public void HeadingZeroCrossesTowardMinusZ()
    {
        var forward = Parse(Json).Gates[0].Forward;

        Assert.True(forward.IsEqualApprox(Vector3.Forward), forward.ToString());
    }

    [Fact]
    public void HashFollowsTheLayoutNotTheNameOrIds()
    {
        var course = Parse(Json);
        var moved = course with { Gates = new[] { course.Gates[0] with { Position = course.Gates[0].Position + Vector3.Right }, course.Gates[1] } };
        var reversed = course with { Sequence = new[] { course.Sequence[0], course.Sequence[1], course.Sequence[0] } };
        var renamedIds = Parse(Json.Replace("\"a\"", "\"first\"", StringComparison.Ordinal));

        Assert.Equal(16, course.Hash.Length);
        Assert.Equal(course.Hash, Parse(Json).Hash);
        Assert.Equal(course.Hash, (course with { Name = "Renamed", Chapter = "C9" }).Hash);
        Assert.Equal(course.Hash, renamedIds.Hash);
        Assert.NotEqual(course.Hash, moved.Hash);
        Assert.NotEqual(course.Hash, reversed.Hash);
        Assert.NotEqual(course.Hash, (course with { Start = new SpawnPoint(course.Start.Position, 91f) }).Hash);
    }

    [Theory]
    [InlineData("\"gate\": \"b\", \"dir\": \"forward\"", "\"gate\": \"zz\", \"dir\": \"forward\"", "unknown gate \"zz\"")]
    [InlineData("\"diameter\": 25", "\"diam\": 25", "has no \"diameter\"")]
    [InlineData("\"diameter\": 25", "\"diameter\": 0", "must be above 0")]
    [InlineData("\"kind\": \"ring\"", "\"kind\": \"hoop\"", "gate \"b\" \"kind\" is \"hoop\", expected pylons or ring")]
    [InlineData("\"dir\": \"reverse\"", "\"dir\": \"backwards\"", "expected forward or reverse")]
    [InlineData("\"id\": \"b\"", "\"id\": \"a\"", "used twice")]
    [InlineData("\"start\": { \"pos\": [10, 120, -40.5],", "\"start\": { \"pos\": [10, 120],", "is not [x, y, z]")]
    [InlineData("\"heading\": 90", "\"heading\": \"east\"", "start \"heading\" is not a number")]
    [InlineData("\"version\": 1", "\"version\": 2", "newer than this build")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"title\": 7,", "course \"title\" is not a string")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"title\": null,", "course \"title\" is not a string")]
    [InlineData("\"sequence\": [", "\"sequence\": [ }", "")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"planes\": [],", "course \"planes\" is not a list of one or more")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"planes\": \"player_fury\",", "course \"planes\" is not a list of one or more")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"planes\": [\"player_fury\", \"Fury\"],", "names unknown plane \"Fury\", expected one of player_autogyro")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"planes\": [7],", "names unknown plane \"7\"")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"planes\": [\"player_fury\", \"PLAYER_FURY\"],", "names \"PLAYER_FURY\" twice")]
    public void RejectsAMalformedFileWithAReason(string from, string to, string reason)
    {
        Assert.Contains(from, Json, StringComparison.Ordinal);
        var course = Course.Parse("C1", "bad", Json.Replace(from, to, StringComparison.Ordinal), out string error);

        Assert.Null(course);
        Assert.NotEmpty(error);
        Assert.Contains(reason, error, StringComparison.Ordinal);
    }

    [Fact]
    public void GameFolderCourseWinsAClashAndBothSourcesList()
    {
        var library = new CourseLibrary(_shipped, _game);
        WriteCourse(_shipped, "C1", "Harbour", Json);
        WriteCourse(_shipped, "C1", "Shipped Only", Json);
        WriteCourse(_game, "C1", "harbour", Json.Replace("\"heading\": 90", "\"heading\": 180", StringComparison.Ordinal));
        WriteCourse(_game, "C2", "Elsewhere", Json);

        var list = library.List("C1");
        var harbour = library.Load("C1", "Harbour", out string error);

        Assert.Equal(new[] { "harbour", "Shipped Only" }, list.Select(f => f.Name));
        Assert.Equal(new[] { CourseSource.GameFolder, CourseSource.Shipped }, list.Select(f => f.Source));
        Assert.NotNull(harbour);
        Assert.Equal(string.Empty, error);
        Assert.Equal(180f, harbour!.Start.HeadingDeg);
        Assert.Empty(library.List("C3"));
    }

    [Fact]
    public void DetLaunchReadsNoGameFolder()
    {
        Assert.Null(CourseLibrary.ForLaunch(_game, det: true).GameFolder);
        Assert.Equal(Path.Combine(_game, "Courses"), CourseLibrary.ForLaunch(_game, det: false).GameFolder);
    }

    [Fact]
    public void LoadNamesTheFileOfABrokenOrMissingCourse()
    {
        var library = new CourseLibrary(_shipped, _game);
        WriteCourse(_game, "C1", "Broken", "{ \"start\": 3 }");

        Assert.Null(library.Load("C1", "Broken", out string broken));
        Assert.Contains("Broken.json", broken, StringComparison.Ordinal);
        Assert.Null(library.Load("C1", "Absent", out string absent));
        Assert.Equal("no course C1/Absent", absent);
    }

    [Fact]
    public void SaveWritesWhatLoadReadsBack()
    {
        var library = new CourseLibrary(_shipped, _game);
        var course = Parse(Json) with { Name = "Saved" };

        string path = library.Save(course, CourseSource.GameFolder);
        var loaded = library.Load("C1", "Saved", out _);

        Assert.Equal(Path.Combine(_game, "C1", "Saved.json"), path);
        Assert.Equal(course.Hash, loaded!.Hash);
        Assert.Throws<InvalidOperationException>(() => new CourseLibrary(_shipped, null).Save(course, CourseSource.GameFolder));
        Assert.Throws<ArgumentException>(() => library.Save(course with { Name = "a/b" }, CourseSource.GameFolder));
    }

    private static Course Parse(string json) =>
        Course.Parse("C1", "Harbour", json, out string error) ?? throw new InvalidOperationException(error);

    private static void WriteCourse(string root, string chapter, string name, string json)
    {
        Directory.CreateDirectory(Path.Combine(root, chapter));
        File.WriteAllText(Path.Combine(root, chapter, name + ".json"), json);
    }
}
