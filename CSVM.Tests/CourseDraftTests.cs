using System.Linq;
using CSVM.Flight.Modes;
using CSVM.UI.Labs;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>The course lab's edits, engine-free: ids, heading from the camera, placement, the ring's
/// height above ground, moving, deleting, and where a save goes.</summary>
public class CourseDraftTests
{
    [Fact]
    public void IdsCountUpPerKindPastTheHighestNumber()
    {
        var draft = Draft();
        Assert.Equal("p1", draft.Place(CourseGateKind.PylonGate, Vector3.Zero, 0f).Id);
        Assert.Equal("p2", draft.Place(CourseGateKind.PylonGate, Vector3.Zero, 0f).Id);
        Assert.Equal("r1", draft.Place(CourseGateKind.BalloonRing, Vector3.Zero, 0f).Id);
        draft.Delete("p1");
        Assert.Equal("p3", draft.Place(CourseGateKind.PylonGate, Vector3.Zero, 0f).Id);
        var other = draft.Course.Gates[0];
        Assert.Equal("p1", CourseDraft.NextId(new[] { other with { Id = "gate7" }, other with { Id = "p" } }, CourseGateKind.PylonGate));
        Assert.Equal("r10", CourseDraft.NextId(new[] { other with { Id = "r9" }, other with { Id = "p12" } }, CourseGateKind.BalloonRing));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(90f)]
    [InlineData(-135f)]
    public void HeadingIsTheCameraYawOnTheSpawnConvention(float yaw)
    {
        // A camera turned by yaw and pitched down looks along the spawn convention's forward for that heading.
        var camera = new Basis(Vector3.Up, Mathf.DegToRad(yaw)) * new Basis(Vector3.Right, Mathf.DegToRad(-30f));
        float heading = CourseDraft.HeadingOf(camera);
        Assert.Equal(yaw, heading, 3);
        var forward = new SpawnPoint(Vector3.Zero, heading).Forward;
        var look = (-camera.Z with { Y = 0f }).Normalized();
        Assert.True(forward.IsEqualApprox(look), $"{forward} against {look}");
    }

    [Fact]
    public void HeadingLookingStraightDownIsTheScreensUp()
    {
        var down = new Basis(Vector3.Up, Mathf.DegToRad(90f)) * new Basis(Vector3.Right, Mathf.DegToRad(-90f));
        Assert.Equal(90f, CourseDraft.HeadingOf(down), 3);
    }

    [Fact]
    public void PlacingAppendsAForwardEntryAndSelectsTheGate()
    {
        var draft = Draft();
        int changes = 0;
        draft.Changed += () => changes++;
        var ground = new Vector3(10f, 5f, -20f);
        var pylons = draft.Place(CourseGateKind.PylonGate, ground, 30f);
        var ring = draft.Place(CourseGateKind.BalloonRing, ground, 30f);
        Assert.Equal(ground, pylons.Position);
        Assert.Equal((CourseDraft.PylonWidthM, CourseDraft.PylonHeightM), (pylons.Width, pylons.Height));
        Assert.Equal(ground.Y + CourseDraft.RingAboveGroundM, ring.Position.Y);
        Assert.Equal(CourseDraft.RingDiameterM, ring.Diameter);
        Assert.Equal(new[] { new SequenceEntry("p1", GateDirection.Forward), new SequenceEntry("r1", GateDirection.Forward) }, draft.Course.Sequence);
        Assert.Equal("r1", draft.SelectedId);
        draft.PlaceStart(ground, 45f);
        Assert.Equal(new SpawnPoint(ground + (Vector3.Up * CourseDraft.StartAboveGroundM), 45f), draft.Course.Start);
        Assert.Equal(3, changes);
        var exact = new SpawnPoint(new Vector3(10f, 250f, -40f), -30f);
        draft.SetStart(exact);
        Assert.Equal(exact, draft.Course.Start);
        Assert.Equal(4, changes);
    }

    [Fact]
    public void RingHeightAboveGroundIsStoredAsWorldY()
    {
        var ring = Draft().Place(CourseGateKind.BalloonRing, new Vector3(0f, 100f, 0f), 0f);
        var raised = CourseDraft.WithAboveGround(ring, 70f, 120f);
        Assert.Equal(190f, raised.Position.Y);
        Assert.Equal(70f, raised.Position.Y - 120f);
    }

    [Fact]
    public void MovingKeepsARingsHeightAboveGroundAndStandsAPylonGateOnIt()
    {
        var draft = Draft();
        var pylons = draft.Place(CourseGateKind.PylonGate, new Vector3(0f, 100f, 0f), 0f);
        var ring = draft.Place(CourseGateKind.BalloonRing, new Vector3(0f, 100f, 0f), 0f);
        var sea = new Vector3(500f, 0f, 300f);
        Assert.Equal(sea, CourseDraft.MovedTo(pylons, sea, 100f).Position);
        Assert.Equal(sea + (Vector3.Up * CourseDraft.RingAboveGroundM), CourseDraft.MovedTo(ring, sea, 100f).Position);
    }

    [Fact]
    public void DeletingRemovesEveryEntryNamingTheGateAndItsSelection()
    {
        var p = new CourseGate("p1", CourseGateKind.PylonGate, Vector3.Zero, 0f, 40f, 30f, 0f);
        var r = new CourseGate("r1", CourseGateKind.BalloonRing, Vector3.Up * 50f, 0f, 0f, 0f, 40f);
        var draft = new CourseDraft(new Course("C1", "t", default, new[] { p, r }, new[]
        {
            new SequenceEntry("r1", GateDirection.Forward),
            new SequenceEntry("p1", GateDirection.Forward),
            new SequenceEntry("r1", GateDirection.Reverse),
        }));
        draft.Select("r1");
        Assert.True(draft.Delete("r1"));
        Assert.Equal(new[] { p }, draft.Course.Gates);
        Assert.Equal(new[] { new SequenceEntry("p1", GateDirection.Forward) }, draft.Course.Sequence);
        Assert.Null(draft.SelectedId);
        Assert.False(draft.Delete("r1"));
        Assert.NotNull(Course.Parse("C1", "t", draft.Course.ToJson(), out string error));
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void UpdateReplacesTheGateWithItsIdAndSelectIgnoresAnUnknownOne()
    {
        var draft = Draft();
        var gate = draft.Place(CourseGateKind.PylonGate, Vector3.Zero, 0f);
        Assert.True(draft.Update(gate with { Width = 55f }));
        Assert.Equal(55f, draft.Selected!.Width);
        Assert.False(draft.Update(gate with { Id = "nope" }));
        draft.Select("nope");
        Assert.Null(draft.Selected);
    }

    [Fact]
    public void PlacingInsertsAfterTheSelectedEntryAndSelectsTheNewOne()
    {
        var draft = Draft();
        draft.Place(CourseGateKind.PylonGate, Vector3.Zero, 0f);
        draft.Place(CourseGateKind.PylonGate, Vector3.Zero, 0f);
        draft.SelectEntry(0);
        draft.Place(CourseGateKind.BalloonRing, Vector3.Zero, 0f);
        Assert.Equal(new[] { "p1", "r1", "p2" }, Ids(draft));
        Assert.Equal((1, "r1"), (draft.SelectedEntry, draft.SelectedId));

        // A gate selected alone, as a click on it in the world does, keeps the insertion point.
        draft.Select("p2");
        Assert.Equal(1, draft.SelectedEntry);
        draft.Place(CourseGateKind.PylonGate, Vector3.Zero, 0f);
        Assert.Equal(new[] { "p1", "r1", "p3", "p2" }, Ids(draft));

        // With no entry selected it appends.
        draft.SelectEntry(-1);
        Assert.Equal((-1, (string?)null), (draft.SelectedEntry, draft.SelectedId));
        draft.Place(CourseGateKind.PylonGate, Vector3.Zero, 0f);
        Assert.Equal("p4", Ids(draft)[^1]);
    }

    [Fact]
    public void MovingAnEntryStopsAtTheEndsAndTheSelectionFollowsIt()
    {
        var draft = Sequenced("a", "b", "c");
        int changes = 0;
        draft.Changed += () => changes++;
        Assert.False(draft.MoveEntry(0, -1));
        Assert.False(draft.MoveEntry(2, 1));
        Assert.False(draft.MoveEntry(1, 2));
        Assert.False(draft.MoveEntry(-1, 1));
        Assert.Equal(0, changes);
        Assert.True(draft.MoveEntry(2, -1));
        Assert.Equal(new[] { "a", "c", "b" }, Ids(draft));
        Assert.Equal((1, "c"), (draft.SelectedEntry, draft.SelectedId));
        Assert.True(draft.MoveEntry(1, -1));
        Assert.True(draft.MoveEntry(1, 1));
        Assert.Equal(new[] { "c", "b", "a" }, Ids(draft));
        Assert.Equal((2, "a"), (draft.SelectedEntry, draft.SelectedId));
        Assert.Equal(3, changes);
    }

    [Fact]
    public void AGateAddedAgainGoesAfterTheSelectedEntryWithItsDirection()
    {
        var draft = Sequenced("a", "b", "c");
        draft.SelectEntry(2);
        draft.Select("b");
        Assert.True(draft.AddEntry("b", GateDirection.Reverse));
        draft.Select("a");
        Assert.True(draft.AddEntry("a", GateDirection.Reverse));
        Assert.Equal(new[] { "a", "b", "c", "b", "a" }, Ids(draft));
        Assert.Equal(new[] { GateDirection.Forward, GateDirection.Forward, GateDirection.Forward, GateDirection.Reverse, GateDirection.Reverse },
            draft.Course.Sequence.Select(e => e.Direction));
        Assert.Equal((4, "a"), (draft.SelectedEntry, draft.SelectedId));
        Assert.False(draft.AddEntry("nope", GateDirection.Forward));
        Assert.Equal("1, 5", CourseDraft.EntryNumbers(draft.Course, "a"));
        Assert.Equal("2, 4", CourseDraft.EntryNumbers(draft.Course, "b"));
        Assert.Equal("3", CourseDraft.EntryNumbers(draft.Course, "c"));
        Assert.Equal(draft.Course.ToJson(), Course.Parse("C1", "t", draft.Course.ToJson(), out _)!.ToJson());
    }

    [Fact]
    public void FlippingTurnsTheEntryAndItsFlownWay()
    {
        var draft = Sequenced("a", "b");
        Assert.True(draft.Flip(1));
        Assert.Equal(GateDirection.Reverse, draft.Course.Sequence[1].Direction);
        Assert.Equal((1, "b"), (draft.SelectedEntry, draft.SelectedId));
        var gate = draft.Course.Gates[1];
        Assert.True(gate.FlownWay(draft.Course.Sequence[1].Direction).IsEqualApprox(-gate.Forward));
        Assert.True(draft.Flip(1));
        Assert.Equal(GateDirection.Forward, draft.Course.Sequence[1].Direction);
        Assert.False(draft.Flip(2));
    }

    [Fact]
    public void RemovingAnEntryKeepsTheGateAndSelectsTheEntryNowInItsPlace()
    {
        var draft = Sequenced("a", "b", "c");
        Assert.True(draft.RemoveEntry(1));
        Assert.Equal(new[] { "a", "c" }, Ids(draft));
        Assert.Equal(3, draft.Course.Gates.Count);
        Assert.Equal((1, "c"), (draft.SelectedEntry, draft.SelectedId));
        Assert.True(draft.RemoveEntry(1));
        Assert.Equal((0, "a"), (draft.SelectedEntry, draft.SelectedId));
        Assert.True(draft.RemoveEntry(0));
        Assert.Empty(draft.Course.Sequence);
        Assert.Equal(-1, draft.SelectedEntry);
        Assert.Equal("a", draft.SelectedId);
        Assert.False(draft.RemoveEntry(0));
        Assert.Equal(string.Empty, CourseDraft.EntryNumbers(draft.Course, "a"));
    }

    [Fact]
    public void DeletingAGateFlownSeveralTimesKeepsTheSelectedEntryValid()
    {
        var draft = Sequenced("a", "b", "c");
        draft.AddEntry("a", GateDirection.Reverse); // nothing selected: appended
        draft.SelectEntry(1);
        draft.AddEntry("a", GateDirection.Forward);
        Assert.Equal(new[] { "a", "b", "a", "c", "a" }, Ids(draft));

        // The selected entry (c, index 3) stays selected at its new place once every entry of a goes.
        draft.SelectEntry(3);
        Assert.True(draft.Delete("a"));
        Assert.Equal(new[] { "b", "c" }, Ids(draft));
        Assert.Equal((1, "c"), (draft.SelectedEntry, draft.SelectedId));

        // An entry of the deleted gate takes the selection with it.
        Assert.True(draft.Delete("c"));
        Assert.Equal((-1, (string?)null), (draft.SelectedEntry, draft.SelectedId));
        Assert.Equal(new[] { "b" }, Ids(draft));
    }

    [Fact]
    public void LoadingClearsTheSelectedEntryAndCentresSitMidOpening()
    {
        var draft = Sequenced("a");
        draft.SelectEntry(0);
        draft.Load(draft.Course);
        Assert.Equal((-1, (string?)null), (draft.SelectedEntry, draft.SelectedId));
        var pylons = new CourseGate("p1", CourseGateKind.PylonGate, new Vector3(1f, 2f, 3f), 0f, 40f, 30f, 0f);
        var ring = new CourseGate("r1", CourseGateKind.BalloonRing, new Vector3(1f, 52f, 3f), 0f, 0f, 0f, 40f);
        Assert.Equal(new Vector3(1f, 17f, 3f), CourseDraft.Centre(pylons));
        Assert.Equal(ring.Position, CourseDraft.Centre(ring));
    }

    [Theory]
    [InlineData(true, true, CourseSource.Shipped)]
    [InlineData(true, false, CourseSource.Shipped)]
    [InlineData(false, true, CourseSource.GameFolder)]
    [InlineData(false, false, null)]
    public void SaveGoesToTheShippedRootFromTheEditorAndTheGameFolderFromAnExport(bool editor, bool gameFolder, CourseSource? expected) =>
        Assert.Equal(expected, CourseDraft.SaveTarget(editor, gameFolder));

    [Theory]
    [InlineData(CourseSource.Shipped, CourseSource.GameFolder, true, CourseSource.GameFolder)] // a dev build saves it back
    [InlineData(CourseSource.GameFolder, CourseSource.GameFolder, true, CourseSource.GameFolder)]
    [InlineData(null, CourseSource.GameFolder, false, null)] // no game folder read, so Save is off
    [InlineData(CourseSource.Shipped, CourseSource.Shipped, true, CourseSource.Shipped)]
    [InlineData(CourseSource.GameFolder, CourseSource.Shipped, true, CourseSource.GameFolder)]
    [InlineData(CourseSource.Shipped, null, true, CourseSource.Shipped)] // a new course
    [InlineData(null, null, false, null)]
    public void AGameFolderCourseSavesBackToTheGameFolderAndAnyOtherWhereTheBuildSaves(
        CourseSource? build, CourseSource? openedFrom, bool gameFolder, CourseSource? expected) =>
        Assert.Equal(expected, CourseDraft.SaveTarget(build, openedFrom, gameFolder));

    private static CourseDraft Draft() => new(CourseDraft.Empty("C1", "t", new SpawnPoint(Vector3.Zero, 0f)));

    // A course of pylon gates with these ids, each flown once forward in that order, nothing selected.
    private static CourseDraft Sequenced(params string[] ids) =>
        new(new Course("C1", "t", default,
            ids.Select((id, i) => new CourseGate(id, CourseGateKind.PylonGate, new Vector3(100f * i, 0f, 0f), 90f * i, 40f, 30f, 0f)).ToList(),
            ids.Select(id => new SequenceEntry(id, GateDirection.Forward)).ToList()));

    private static string[] Ids(CourseDraft draft) => draft.Course.Sequence.Select(e => e.GateId).ToArray();
}
