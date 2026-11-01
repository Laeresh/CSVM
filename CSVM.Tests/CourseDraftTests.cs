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

    [Theory]
    [InlineData(true, true, CourseSource.Shipped)]
    [InlineData(true, false, CourseSource.Shipped)]
    [InlineData(false, true, CourseSource.GameFolder)]
    [InlineData(false, false, null)]
    public void SaveGoesToTheShippedRootFromTheEditorAndTheGameFolderFromAnExport(bool editor, bool gameFolder, CourseSource? expected) =>
        Assert.Equal(expected, CourseDraft.SaveTarget(editor, gameFolder));

    private static CourseDraft Draft() => new(CourseDraft.Empty("C1", "t", new SpawnPoint(Vector3.Zero, 0f)));
}
