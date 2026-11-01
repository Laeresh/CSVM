using System.IO;
using CSVM.Extraction;
using CSVM.Flight.Modes;
using CSVM.Mech3;
using CSVM.Testing;
using CSVM.Utils;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>The score key a course run records under, and the penalty count beside its best. The
/// store's file round trip is the engine's (<c>course-launch</c>), since it reads through Godot.</summary>
public class CourseScoreKeyTests
{
    private const string Plane = "player_bhawk";

    private static readonly CourseGate A = new("a", CourseGateKind.PylonGate, Vector3.Zero, 0f, 40f, 30f, 0f);
    private static readonly CourseGate B = new("b", CourseGateKind.BalloonRing, new Vector3(0f, 50f, -200f), 0f, 0f, 0f, 20f);

    private static readonly Course Sample = new("C1", "sample", new SpawnPoint(new Vector3(0f, 15f, 300f), 0f),
        new[] { A, B }, new[] { new SequenceEntry("a", GateDirection.Forward), new SequenceEntry("b", GateDirection.Forward) });

    [Fact]
    public void ACourseKeyNamesChapterNameHashAndPlane()
    {
        Assert.Equal($"course/C1/sample/{Sample.Hash}/{Plane}", Key(Sample));
    }

    [Fact]
    public void TheKeySurvivesAFileRoundTrip()
    {
        var again = Course.Parse(Sample.Chapter, Sample.Name, Sample.ToJson(), out string error);

        Assert.True(again != null, error);
        Assert.Equal(Key(Sample), Key(again!));
    }

    [Fact]
    public void AnEditedLayoutIsAFreshBoard()
    {
        var moved = Sample with { Gates = new[] { A with { Position = new Vector3(5f, 0f, 0f) }, B } };
        var reversed = Sample with { Sequence = new[] { new SequenceEntry("a", GateDirection.Reverse), Sample.Sequence[1] } };

        Assert.NotEqual(Key(Sample), Key(moved));
        Assert.NotEqual(Key(Sample), Key(reversed));
    }

    // The hash leaves the name out. The key carries it, as the plan decided for bests, so a renamed
    // copy is a board of its own.
    [Fact]
    public void ARenameIsAFreshBoardWithTheSameHash()
    {
        var renamed = Sample with { Name = "sample_copy" };

        Assert.Equal(Sample.Hash, renamed.Hash);
        Assert.NotEqual(Key(Sample), Key(renamed));
    }

    [Fact]
    public void ARetitleKeepsTheBoard()
    {
        var retitled = Sample with { Title = "Out and Back" };

        Assert.Equal(Key(Sample), Key(retitled));
        Assert.Equal(Key(Sample), Key(retitled with { Title = "Another Title" }));
    }

    [Fact]
    public void ACourseRunNeverKeysUnderItsDangerZoneMission()
    {
        // The launch flies a course over the chapter's IA1 world, the Danger Zone key's mission.
        string key = Key(Sample);

        Assert.StartsWith("course/", key);
        Assert.NotEqual($"C1/IA1/{Plane}", key);
    }

    [Fact]
    public void ACourseRunRecordsItsPenaltyCountAndShowsIt()
    {
        var run = StuntMission.ForCourse(Sample)!;
        Assert.Equal(0, ScoreStore.PenaltiesOf(run));

        run.Update(new Vector3(0f, 15f, 1f));
        run.Update(new Vector3(0f, 15f, -1f)); // the first entry starts the clock
        run.AddPenalty(CourseElements.TouchPenaltySeconds);
        run.AddPenalty(CourseElements.TouchPenaltySeconds);

        Assert.Equal(2, ScoreStore.PenaltiesOf(run));
        var summary = new StuntSummary(run, run.Elapsed, null, false);
        Assert.Equal($"TOTAL   {StuntMission.FormatTime(run.Elapsed)}   (2 penalties)", summary.TotalLine());
        Assert.Contains(summary.TotalLine(), summary.Lines());
    }

    [Fact]
    public void ACourseRunRecordsItsClockAtEachEntryPenaltiesIncluded()
    {
        var run = StuntMission.ForCourse(Sample)!;
        run.Update(new Vector3(0f, 15f, 1f));
        run.Update(new Vector3(0f, 15f, -1f));
        run.Tick(4f);
        run.AddPenalty(CourseElements.TouchPenaltySeconds);
        run.Update(new Vector3(0f, 50f, -195f));
        run.Update(new Vector3(0f, 50f, -205f));

        Assert.True(run.AllComplete);
        Assert.Equal(new[] { run.Zones[0].CompletedAt, 7f }, ScoreStore.SplitsOf(run));
    }

    [ExtractedDataFact]
    public void ADangerZoneRunKeepsItsKeyAndRecordsNoPenalties()
    {
        string gamez = SessionPaths.ChapterGamez(TestData.ExtractedRoot!, "C4");
        var run = StuntMission.Load(GameZ.Load(gamez), TestZrdr.Mission("C4", "IA1"),
            Messages.FromExtracted(Path.Combine(TestData.ExtractedRoot!, "messages.json")))!;

        Assert.Equal($"C4/IA1/{Plane}", ScoreStore.KeyFor(run, "C4", "IA1", Plane));
        Assert.Null(ScoreStore.PenaltiesOf(run));
        Assert.Null(ScoreStore.SplitsOf(run));
        Assert.Equal($"TOTAL   {StuntMission.FormatTime(0f)}", new StuntSummary(run, 0f, null, false).TotalLine());

        // Its cleared flash ignores any splits handed to it.
        var zone = run.Zones[0];
        Assert.Equal(StuntRunHud.ClearedFlash(run, zone), StuntRunHud.ClearedFlash(run, zone, new[] { 1f, 2f }));
        Assert.EndsWith("CLEARED", StuntRunHud.ClearedFlash(run, zone, new[] { 1f, 2f }));
        Assert.Null(StuntRunHud.SplitDelta(run, zone, new[] { 1f, 2f }));
    }

    private static string Key(Course course) =>
        ScoreStore.KeyFor(StuntMission.ForCourse(course)!, course.Chapter, "IA1", Plane);
}
