using System.Globalization;
using System.IO;
using System.Threading;
using CSVM.Tooling;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>The screenshot folder choice, the pure half of <see cref="CaptureDirector.ShotDir"/>,
/// and the F11 line format, the pure half of <see cref="CaptureDirector.PrintPlacement"/>.</summary>
public class CaptureDirectorTests
{
    private const float BaseFov = 46.8f;
    private const string CameraAt = "--pos=\"100.5,20,-30.25\" --direction=\"0.6,-0.8,0\"";
    private const string PlaneAt = "--pos=\"1,2,3\" --direction=\"-0.6,-0.48,-0.64\"";
    private static readonly Vector3 Eye = new(100.5f, 20f, -30.25f);
    private static readonly Vector3 Forward = new(3f, -4f, 0f);   // unnormalized on purpose
    private static readonly Transform3D Plane = new(
        new Basis(new Vector3(0.8f, 0f, -0.6f), new Vector3(-0.36f, 0.8f, -0.48f), new Vector3(0.6f, 0.48f, 0.64f)),
        new Vector3(1f, 2f, 3f));

    // Built from the host's own root: a drive-letter path is relative off Windows, so
    // GetFullPath would resolve it against the working directory.
    private static readonly string Root = Path.GetPathRoot(Path.GetTempPath())!;

    [Fact]
    public void ARepoRunWritesToTheRepoRootsScreenshotsFolder()
    {
        var repo = Path.Combine(Root, "CSVM");
        Assert.Equal(Path.Combine(repo, "Screenshots"),
            CaptureDirector.ShotDirFor(Path.Combine(repo, "CSVM") + Path.DirectorySeparatorChar,
                Path.Combine(repo, "tools", "godot"), exported: false));
    }

    [Fact]
    public void AnExportedBuildWritesBesideItsExecutableNotInTheFolderAboveIt()
    {
        // res:// resolves to the exe's folder in an export, so the repo rule would climb out of it.
        var build = Path.Combine(Root, "Games", "CSVM");
        Assert.Equal(Path.Combine(build, "Screenshots"),
            CaptureDirector.ShotDirFor(build + Path.DirectorySeparatorChar, build, exported: true));
    }

    /// <summary>The chase camera sits behind the aircraft, so the frame reproduces only from the
    /// camera's own pose; the aircraft's placement follows it for --fly.</summary>
    [Fact]
    public void AFlightPanePrintsItsCameraThenItsAircraft()
    {
        var lines = CaptureDirector.PlacementLines("--freecam", "--chapter=C3",
            new[] { new CaptureDirector.PanePose(Eye, Forward, BaseFov, Plane) }, BaseFov);

        Assert.Equal(new[]
        {
            $"placement camera: --freecam --chapter=C3 {CameraAt}",
            $"placement aircraft: --fly --chapter=C3 {PlaneAt}",
        }, lines);
    }

    /// <summary>The free camera draws at the external base, so a cockpit angle travels with the
    /// line. Without it the replay frames wider or narrower than the screen did.</summary>
    [Fact]
    public void ACockpitAngleIsCarriedAsFov()
    {
        var lines = CaptureDirector.PlacementLines("--freecam", "--chapter=C1",
            new[] { new CaptureDirector.PanePose(Eye, Forward, 55.125f, null) }, BaseFov);

        Assert.Equal(new[] { $"placement camera: --freecam --chapter=C1 {CameraAt} --fov=55.125" }, lines);
    }

    [Fact]
    public void SplitscreenLabelsEveryPaneRatherThanTakingPlayerOnes()
    {
        var lines = CaptureDirector.PlacementLines("--freecam", "--chapter=C2", new[]
        {
            new CaptureDirector.PanePose(Eye, Forward, BaseFov, Plane),
            new CaptureDirector.PanePose(Eye, Forward, BaseFov, null),
        }, BaseFov);

        Assert.Equal(new[]
        {
            $"placement P1 camera: --freecam --chapter=C2 {CameraAt}",
            $"placement P1 aircraft: --fly --chapter=C2 {PlaneAt}",
            $"placement P2 camera: --freecam --chapter=C2 {CameraAt}",
        }, lines);
    }

    [Fact]
    public void TheLineStaysInvariantUnderAGermanCulture()
    {
        var before = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            var lines = CaptureDirector.PlacementLines("--anim-lab", "--stage=empty",
                new[] { new CaptureDirector.PanePose(Eye, Forward, 55.125f, null) }, BaseFov);
            Assert.Equal(new[] { $"placement camera: --anim-lab --stage=empty {CameraAt} --fov=55.125" }, lines);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = before;
        }
    }
}
