using System.Collections.Generic;
using System.IO;
using CSVM.Flight.Camera;
using CSVM.Launch;
using CSVM.Spec;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The engine-free halves of the <c>--xr-sim</c> stand-in headset. They are the flag's grammar
/// (<see cref="XrSimSpec"/>) and the pinned eyes it draws through (<see cref="XrSimEyes"/>). The
/// project settings the launch scripts' feature tag switches are checked here too.
/// </summary>
[Trait("Tier", "Quick")]
public class XrSimTests
{
    private const float Tol = 1e-5f;

    [Fact]
    public void ABareFlagIsTheSeatedEyeWithTwoGoldenSizedEyes()
    {
        var spec = SessionSpec.Parse(new[] { "--fly", "--xr-sim" }).XrSim;
        Assert.Equal(XrSimSpec.Default, spec);
        Assert.Equal(Vector3.Zero, spec!.Head);
        Assert.Equal(0f, spec.YawDeg);
        Assert.Equal(0f, spec.PitchDeg);
        Assert.Equal(new Vector2I(1280, 720), spec.ShotPx);
    }

    [Fact]
    public void NoFlagNoStandIn()
    {
        Assert.Null(SessionSpec.Parse(new[] { "--fly" }).XrSim);
    }

    [Fact]
    public void EveryTokenParsesOverTheDefault()
    {
        var problems = new List<string>();
        var spec = XrSimSpec.Parse("yaw=-30.5, pitch=10,roll=-20,head=0.1/-0.05/0.2,ipd=70,eye=1920x1080,fov=100,shot=right", problems);
        Assert.Empty(problems);
        Assert.Equal(-30.5f, spec.YawDeg);
        Assert.Equal(10f, spec.PitchDeg);
        Assert.Equal(-20f, spec.RollDeg);
        Assert.Equal(new Vector3(0.1f, -0.05f, 0.2f), spec.Head);
        Assert.Equal(70f, spec.IpdMm);
        Assert.Equal(new Vector2I(1920, 1080), spec.EyePx);
        Assert.Equal(100f, spec.FovDeg);
        Assert.Equal(XrSimShot.Right, spec.Shot);
        Assert.Equal(new Vector2I(1920, 1080), spec.ShotPx);
    }

    [Fact]
    public void ABadTokenKeepsItsDefaultAndIsReported()
    {
        var parsed = SessionSpec.Parse(new[] { "--fly", "--xr-sim=yaw=400,fov=wide,eye=10x10,tilt=5,shot=left" });
        Assert.Equal(XrSimSpec.Default with { Shot = XrSimShot.Left }, parsed.XrSim);
        Assert.Equal(4, parsed.Warnings.Count);
    }

    [Fact]
    public void YawTurnsTheHeadRightAndPitchTipsItUp()
    {
        var right = new XrSimEyes(90f, 0f, Vector3.Zero, 63f, 90f).Head.Basis;
        Assert.True((-right.Z).IsEqualApprox(Vector3.Right));
        var up = new XrSimEyes(0f, 30f, Vector3.Zero, 63f, 90f).Head.Basis;
        Assert.True((-up.Z).IsEqualApprox(new Vector3(0f, 0.5f, -Mathf.Sqrt(3f) / 2f)));
        // Yaw first, about world up: a turned and raised head keeps its right axis level.
        var both = new XrSimEyes(45f, 20f, Vector3.Zero, 63f, 90f).Head.Basis;
        Assert.Equal(0f, both.X.Y, 5);
    }

    [Fact]
    public void RollTiltsTheHeadAboutItsOwnLineOfSight()
    {
        // A tilt toward the right shoulder takes the head's right axis down and leaves the gaze.
        var tilted = new XrSimEyes(0f, 0f, Vector3.Zero, 63f, 90f, rollDeg: 30f).Head.Basis;
        Assert.True((-tilted.Z).IsEqualApprox(Vector3.Forward));
        Assert.True(tilted.X.IsEqualApprox(new Vector3(Mathf.Sqrt(3f) / 2f, -0.5f, 0f)));
        // Applied last: a turned, raised and tilted head looks where the untilted one does.
        var level = new XrSimEyes(40f, 25f, Vector3.Zero, 63f, 90f).Head.Basis;
        var rolled = new XrSimEyes(40f, 25f, Vector3.Zero, 63f, 90f, rollDeg: -15f).Head.Basis;
        Assert.True(rolled.Z.IsEqualApprox(level.Z));
        Assert.Equal(15f, Mathf.RadToDeg(rolled.X.AngleTo(level.X)), 3);
    }

    [Fact]
    public void TheEyesStraddleTheHeadByHalfTheIpdEach()
    {
        var eyes = new XrSimEyes(90f, 0f, new Vector3(0f, 0.1f, 0f), 64f, 90f);
        var left = eyes.View(0, Transform3D.Identity, Transform3D.Identity, 1.0);
        var right = eyes.View(1, Transform3D.Identity, Transform3D.Identity, 1.0);
        // Turned right, the head's own +x points back along world +z.
        Assert.True(left.Origin.IsEqualApprox(new Vector3(0f, 0.1f, -0.032f)));
        Assert.True(right.Origin.IsEqualApprox(new Vector3(0f, 0.1f, 0.032f)));
        Assert.True(left.Basis.IsEqualApprox(eyes.Head.Basis));
    }

    [Fact]
    public void AZeroPoseWithZeroIpdIsTheOriginItself()
    {
        var origin = new Transform3D(new Basis(Vector3.Up, 0.7f) * new Basis(Vector3.Right, 0.2f), new Vector3(-6350f, 300f, -4144f));
        var eyes = new XrSimEyes(0f, 0f, Vector3.Zero, 0f, 90f);
        var view = eyes.View(0, origin, Transform3D.Identity, 1.0);
        Assert.True(view.Origin.IsEqualApprox(origin.Origin));
        Assert.True(view.Basis.IsEqualApprox(origin.Basis));
    }

    [Fact]
    public void WorldScaleScalesTranslationsAndTheRecenterFrameSitsBetween()
    {
        var eyes = new XrSimEyes(0f, 0f, new Vector3(0f, 0f, 0.1f), 60f, 90f);
        var origin = new Transform3D(Basis.Identity, new Vector3(10f, 0f, 0f));
        var reference = new Transform3D(Basis.Identity, new Vector3(0f, 2f, 0f));
        var view = eyes.View(1, origin, reference, 2.0);
        Assert.True(view.Origin.IsEqualApprox(new Vector3(10.06f, 2f, 0.2f)));
        Assert.True(eyes.Camera(2.0).Origin.IsEqualApprox(new Vector3(0f, 0f, 0.2f)));
    }

    [Fact]
    public void TheProjectionIsSymmetricAtTheGivenHorizontalAngle()
    {
        var m = new XrSimEyes(0f, 0f, Vector3.Zero, 63f, 90f).Projection(640.0 / 720.0, 0.05, 4000.0);
        Assert.Equal(16, m.Length);
        // Column-major: [0] is x scale, [5] y scale, [8] and [9] the off-centre terms.
        Assert.Equal(1.0, m[0], 4);
        Assert.Equal(640.0 / 720.0, m[5], 4);
        Assert.Equal(0.0, m[8], 6);
        Assert.Equal(0.0, m[9], 6);
        Assert.Equal(-1.0, m[11], 6);
    }

    [Fact]
    public void TheProjectSwitchesTheStereoVariantsAndTheShaderCacheOnTheTagAlone()
    {
        string godot = File.ReadAllText(Path.Combine(TestData.RepoRoot, "CSVM", "project.godot"));
        Assert.Contains("\nshaders/enabled=false", godot.Replace("\r\n", "\n"));
        Assert.Contains($"\nshaders/enabled.{XrLaunch.SimFeature}=true", godot.Replace("\r\n", "\n"));
        Assert.Contains($"\nshader_compiler/shader_cache/enabled.{XrLaunch.SimFeature}=false", godot.Replace("\r\n", "\n"));
        Assert.Contains("\nopenxr/enabled=false", godot.Replace("\r\n", "\n"));
        Assert.DoesNotContain($"openxr/enabled.{XrLaunch.SimFeature}", godot);
    }
}
