using CSVM.Flight;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The head-look laws (docs/PLAN-cockpit-view.md, C21): the snap direction table, the 2 rad/s
/// free-look integration, the elevation clamp and azimuth wrap, and the exponential smoothing at
/// the decoded rates (elevation 3.0/s, azimuth 5.0/s). <see cref="HeadLook"/> is engine-free, so
/// none of this needs a live camera.
/// </summary>
public class HeadLookTests
{
    private const float Tol = 1e-4f;

    // C22: HeadLook.AutoheadTarget's shipped constants (extracted/zrdr/player.zrd.json via
    // PlaneStatsFlightGlobalsTests' own loader assertion), reused by the pure-vector-law tests
    // near the bottom of this file.
    private const float ShippedTurnTime = 0.75f;
    private const float ShippedTurnMax = 0.0998f;
    private const float ShippedMinPitch = -0.0524f;

    private static HeadLookInput Idle => default;

    [Fact]
    public void DeadAheadSnapsTheHeadStraightUp()
    {
        var t = HeadLook.SnapTargets(0f, 1f);
        Assert.NotNull(t);
        Assert.Equal(Mathf.Pi / 2f, t!.Value.Elevation, Tol);
        Assert.Equal(0f, t.Value.Azimuth, Tol);
    }

    [Theory]
    [InlineData(1f, 1f)]      // forward-right
    [InlineData(-1f, 1f)]     // forward-left
    [InlineData(1f, -1f)]     // aft-right
    [InlineData(-1f, -1f)]    // aft-left
    public void EveryDiagonalSnapsTo45DegreesUp(float x, float y)
    {
        var t = HeadLook.SnapTargets(x, y);
        Assert.NotNull(t);
        Assert.Equal(Mathf.Pi / 4f, t!.Value.Elevation, Tol);
    }

    [Theory]
    [InlineData(1f, 0f, -Mathf.Pi / 2f)]   // looking right is a negative azimuth
    [InlineData(-1f, 0f, Mathf.Pi / 2f)]   // looking left is positive
    [InlineData(0f, -1f, Mathf.Pi)]        // dead astern, either way round the wrap
    public void TheFlanksAndAsternSnapLevelAtTheDirectionsOwnAzimuth(float x, float y, float azimuth)
    {
        var t = HeadLook.SnapTargets(x, y);
        Assert.NotNull(t);
        Assert.Equal(0f, t!.Value.Elevation, Tol);
        Assert.Equal(0f, HeadLook.Wrap(t.Value.Azimuth - azimuth), Tol);
    }

    [Fact]
    public void NoSnapDirectionIsNoSnapAtAll()
    {
        Assert.Null(HeadLook.SnapTargets(0f, 0f));
    }

    [Fact]
    public void ReleasingASnapReturnsTheTargetsToStraightAhead()
    {
        var head = new HeadLook();
        head.Step(0.1f, Snap(-1f, 0f));
        Assert.True(head.TargetAzimuth > 1f);
        head.Step(0.1f, Idle);
        Assert.Equal(0f, head.TargetAzimuth, Tol);
        Assert.Equal(0f, head.TargetElevation, Tol);
    }

    [Fact]
    public void FreeLookIntegratesAtTwoRadiansPerSecond()
    {
        var head = new HeadLook();
        head.Step(0.5f, Free(0f, 1f));      // straight up for half a second
        Assert.Equal(1f, head.TargetElevation, Tol);
        head = new HeadLook();
        head.Step(0.5f, Free(-1f, 0f));     // and to the left, the positive azimuth direction
        Assert.Equal(1f, head.TargetAzimuth, Tol);
    }

    [Fact]
    public void FreeLookReadsDirectionOnlySoAHalfDeflectedStickPansAsFast()
    {
        var soft = new HeadLook();
        var hard = new HeadLook();
        soft.Step(0.25f, Free(0f, 0.2f));
        hard.Step(0.25f, Free(0f, 1f));
        Assert.Equal(hard.TargetElevation, soft.TargetElevation, Tol);
    }

    [Fact]
    public void FreeLookNeverDrivesTheHeadBelowLevelOrPastStraightUp()
    {
        var head = new HeadLook();
        for (int i = 0; i < 60; i++)
        {
            head.Step(0.1f, Free(0f, 1f));
        }
        Assert.Equal(Mathf.Pi / 2f, head.TargetElevation, Tol);
        for (int i = 0; i < 60; i++)
        {
            head.Step(0.1f, Free(0f, -1f));
        }
        Assert.Equal(0f, head.TargetElevation, Tol);
    }

    [Fact]
    public void TheElevationFloorIsAParameterSoTheChaseCallersMinusHalfPiStillFits()
    {
        var head = new HeadLook(-Mathf.Pi / 2f);
        for (int i = 0; i < 60; i++)
        {
            head.Step(0.1f, Free(0f, -1f));
        }
        Assert.Equal(-Mathf.Pi / 2f, head.TargetElevation, Tol);
    }

    [Fact]
    public void AzimuthWrapsRatherThanWindingUp()
    {
        var head = new HeadLook();
        for (int i = 0; i < 40; i++)
        {
            head.Step(0.1f, Free(-1f, 0f));    // 8 rad of left pan, more than a full turn
        }
        Assert.InRange(head.TargetAzimuth, -Mathf.Pi, Mathf.Pi);
        Assert.Equal(HeadLook.Wrap(8f), head.TargetAzimuth, 1e-3f);
    }

    [Fact]
    public void TheCenterKeyZeroesBothTargetsAtOnceAndBeatsAHeldSnap()
    {
        var head = new HeadLook();
        head.Step(0.1f, Free(-1f, 1f));
        Assert.NotEqual(0f, head.TargetAzimuth);
        head.Step(0.1f, new HeadLookInput(-1f, 0f, 0f, 0f, true));
        Assert.Equal(0f, head.TargetAzimuth, Tol);
        Assert.Equal(0f, head.TargetElevation, Tol);
    }

    [Fact]
    public void TheShownAnglesChaseTheTargetsAtTheDecodedRates()
    {
        const float dt = 0.1f;
        var head = new HeadLook();
        head.Step(dt, Snap(0f, 1f));           // target elevation π/2, azimuth 0
        float expected = HeadLook.Approach(0f, Mathf.Pi / 2f, HeadLook.ElevationSmoothRate, dt);
        Assert.Equal(expected, head.Elevation, Tol);
        Assert.Equal(3f, HeadLook.ElevationSmoothRate);
        Assert.Equal(5f, HeadLook.AzimuthSmoothRate);
    }

    [Fact]
    public void SmoothingIsExponentialAndFrameRateIndependent()
    {
        // One 0.2 s step and two 0.1 s steps land on the same angle: e^(−r·2dt) = (e^(−r·dt))².
        var oneStep = new HeadLook();
        var twoSteps = new HeadLook();
        oneStep.Step(0.2f, Snap(0f, 1f));
        twoSteps.Step(0.1f, Snap(0f, 1f));
        twoSteps.Step(0.1f, Snap(0f, 1f));
        Assert.Equal(oneStep.Elevation, twoSteps.Elevation, Tol);
    }

    [Fact]
    public void AzimuthSmoothingTakesTheShortArcAcrossTheWrap()
    {
        var head = new HeadLook();
        for (int i = 0; i < 200; i++)
        {
            head.Step(0.05f, Snap(-1f, -1f));  // settle looking aft-left, azimuth +3π/4
        }
        float before = head.Azimuth;
        Assert.Equal(3f * Mathf.Pi / 4f, before, 1e-3f);
        head.Step(0.05f, Snap(1f, -1f));       // now aft-right, −3π/4: a quarter turn the LEFT way
        Assert.True(HeadLook.Wrap(head.Azimuth - before) > 0f);
    }

    [Fact]
    public void TheIdleHookOwnsAFrameWithNoLookInputAndBypassesTheInputFloor()
    {
        var head = new HeadLook { IdleAim = () => (-0.0524f, 0.2f) };   // C22's −3° autohead floor
        head.Step(0.1f, Idle);
        Assert.Equal(-0.0524f, head.TargetElevation, Tol);
        Assert.Equal(0.2f, head.TargetAzimuth, Tol);
    }

    [Fact]
    public void TheIdleHookIsSilentWhileAnyLookInputIsHeld()
    {
        int calls = 0;
        var head = new HeadLook
        {
            IdleAim = () =>
            {
                calls++;
                return (0.5f, 0.5f);
            },
        };
        head.Step(0.1f, Free(1f, 0f));
        head.Step(0.1f, Snap(0f, 1f));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void ResetPutsTheHeadStraightAheadWithNothingLeftToSmooth()
    {
        var head = new HeadLook();
        head.Step(0.5f, Free(-1f, 1f));
        head.Reset();
        Assert.Equal(0f, head.Elevation, Tol);
        Assert.Equal(0f, head.Azimuth, Tol);
        Assert.Equal(0f, head.TargetAzimuth, Tol);
    }

    [Fact]
    public void TheComposedBasisYawsFirstThenPitchesAboutTheHeadsOwnRightAxis()
    {
        // Looking 90° left and 45° up: the view direction must be up-and-left, and the head's own
        // right axis must have swung round with the yaw rather than staying the plane's.
        var (_, basis) = CameraController.FirstPersonPose(Vector3.Zero, Basis.Identity, Vector3.Zero,
            Mathf.Pi / 4f, Mathf.Pi / 2f);
        var forward = -basis.Z;
        Assert.True(forward.X < 0f);           // left
        Assert.True(forward.Y > 0.5f);         // and up
        Assert.True(Mathf.Abs(basis.X.Z) > 0.9f); // the right axis now runs along the plane's Z
    }

    [Fact]
    public void ZeroHeadAnglesLeaveTheA2PlacementExactlyAsItWas()
    {
        var attitude = new Basis(Vector3.Up, 0.4f);
        var (position, basis) = CameraController.FirstPersonPose(Vector3.One, attitude, Vector3.Up);
        // The A2 law with no head angles at all: the fixed −4.70° tilt about the plane's right axis.
        var expected = attitude * new Basis(Vector3.Right, -0.08203f);
        Assert.True(position.IsEqualApprox(Vector3.One + (attitude * Vector3.Up)));
        Assert.True(basis.Z.IsEqualApprox(expected.Z));
        Assert.True(basis.X.IsEqualApprox(expected.X));
    }

    // C22: HeadLook.AutoheadTarget, the idle-frame lean law fed to IdleAim, exercised as a pure
    // vector law directly — no PlaneStats in the loop.

    [Fact]
    public void AutoheadIsNullWhenTheLocalVelocityIsNegligible()
    {
        Assert.Null(HeadLook.AutoheadTarget(Vector3.Zero, ShippedTurnTime, ShippedTurnMax, ShippedMinPitch));
        Assert.Null(HeadLook.AutoheadTarget(new Vector3(0f, 0f, -1e-5f), ShippedTurnTime, ShippedTurnMax, ShippedMinPitch));
    }

    [Fact]
    public void AutoheadIgnoresPureForwardSpeedEntirely()
    {
        // Straight and level at full cruise speed, nose along the plane's own −Z: the forward
        // component is dropped before scaling (the class doc's port decision), so this reads
        // exactly as negligible — the same null a parked aircraft returns.
        Assert.Null(HeadLook.AutoheadTarget(new Vector3(0f, 0f, -100f), ShippedTurnTime, ShippedTurnMax, ShippedMinPitch));
    }

    [Fact]
    public void AutoheadPinsTheDecodedMinusThreeDegreeFloorOnAHardDive()
    {
        // A steep dive at speed: the raw lean angle is well past −3°, so the floor — not the
        // magnitude cap's direction — decides the shown elevation. This is the trap's own pin:
        // the −3° floor sits below C21's [0, π/2] input floor and must survive here.
        var t = HeadLook.AutoheadTarget(new Vector3(0f, -50f, -100f), ShippedTurnTime, ShippedTurnMax, ShippedMinPitch);
        Assert.NotNull(t);
        Assert.Equal(ShippedMinPitch, t!.Value.Elevation, Tol);
    }

    [Fact]
    public void AutoheadCapsTheLeanVectorsMagnitudeAtTurnMax()
    {
        // A climb well past the cap once scaled by turnTime (5 m/s × 0.75 = 3.75, against a
        // 0.0998 rad cap): the resulting elevation must sit exactly at the cap, not the
        // uncapped 3.75.
        var t = HeadLook.AutoheadTarget(new Vector3(0f, 5f, -200f), ShippedTurnTime, ShippedTurnMax, ShippedMinPitch);
        Assert.NotNull(t);
        Assert.Equal(ShippedTurnMax, t!.Value.Elevation, Tol);
        Assert.Equal(0f, t.Value.Azimuth, Tol);
    }

    [Fact]
    public void AutoheadLeavesASmallLeanUncappedBelowTurnMax()
    {
        // Well under the cap once scaled: the components pass straight through as the direct
        // (elevation, azimuth) angles, not through an arctangent — the cap having any effect at
        // all on the visible angle depends on this.
        var t = HeadLook.AutoheadTarget(new Vector3(0.01f, 0.01f, -100f), ShippedTurnTime, ShippedTurnMax, ShippedMinPitch);
        Assert.NotNull(t);
        Assert.Equal(0.01f * ShippedTurnTime, t!.Value.Elevation, Tol);
        Assert.Equal(-0.01f * ShippedTurnTime, t.Value.Azimuth, Tol);
    }

    [Fact]
    public void AutoheadLooksRightWhenTheVelocityDriftsRightOfTheNose()
    {
        // Forward with a rightward drift (local +X): the codebase's convention is positive azimuth
        // = LEFT, so a rightward drift must read NEGATIVE, matching SnapTargets' own mirroring.
        var t = HeadLook.AutoheadTarget(new Vector3(50f, 0f, -100f), ShippedTurnTime, ShippedTurnMax, ShippedMinPitch);
        Assert.NotNull(t);
        Assert.True(t!.Value.Azimuth < 0f);
    }

    private static HeadLookInput Snap(float x, float y) => new(x, y, 0f, 0f, false);

    private static HeadLookInput Free(float right, float up) => new(0f, 0f, right, up, false);
}
