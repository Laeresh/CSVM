using System;
using System.IO;
using System.Linq;
using CSVM.Flight.Modes;
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
    [InlineData("\"sequence\": [", "\"sequence\": [ }", "")]
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
