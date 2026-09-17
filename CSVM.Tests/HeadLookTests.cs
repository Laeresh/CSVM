using CSVM.Flight;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The head-look laws: the snap direction table, the 2 rad/s
/// free-look integration, the elevation clamp and azimuth wrap, the padlock's target bearing and
/// its exit scan, and the exponential smoothing at the decoded rates (elevation 3.0/s, azimuth
/// 5.0/s). <see cref="HeadLook"/> is engine-free, so none of this needs a live camera.
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

    // The original's two mode keys, K and J, as the frame they are pressed on.
    private static HeadLookInput SnapKey =>
        new(0f, 0f, 0f, 0f, false, 0f, 0f, false, true);

    private static HeadLookInput SmoothKey =>
        new(0f, 0f, 0f, 0f, false, 0f, 0f, false, false, true);

    // Track Target, L, as the frame it is pressed on.
    private static HeadLookInput PadlockKey =>
        new(0f, 0f, 0f, 0f, false, 0f, 0f, false, false, false, true);

    // The eight direction slots as the original's own key-binding menu labels them
    // (OriginalScreenshots/Keybinds Views 2.png), each read as the composed direction its key
    // contributes: the head must land where its label says. Kp1/Kp3 are "Up/…/Rear", which is what
    // settles that ALL four diagonals lift the head, not only the forward pair.
    [Theory]
    [InlineData(-1f, 1f, Mathf.Pi / 4f, Mathf.Pi / 4f)]              // Kp7 Look Up/Left
    [InlineData(0f, 1f, Mathf.Pi / 2f, 0f)]                          // Kp8 Look Up
    [InlineData(1f, 1f, Mathf.Pi / 4f, -Mathf.Pi / 4f)]              // Kp9 Look Up/Right
    [InlineData(-1f, 0f, 0f, Mathf.Pi / 2f)]                         // Kp4 Look Left
    [InlineData(1f, 0f, 0f, -Mathf.Pi / 2f)]                         // Kp6 Look Right
    [InlineData(-1f, -1f, Mathf.Pi / 4f, 3f * Mathf.Pi / 4f)]        // Kp1 Look Up/Left/Rear
    [InlineData(0f, -1f, 0f, Mathf.Pi)]                              // Kp2 Look Back
    [InlineData(1f, -1f, Mathf.Pi / 4f, -3f * Mathf.Pi / 4f)]        // Kp3 Look Up/Right/Rear
    public void TheEightSlotsSnapWhereTheOriginalsOwnLabelsSay(float x, float y, float elevation, float azimuth)
    {
        var t = HeadLook.SnapTargets(x, y);
        Assert.NotNull(t);
        Assert.Equal(elevation, t!.Value.Elevation, Tol);
        Assert.Equal(0f, HeadLook.Wrap(t.Value.Azimuth - azimuth), Tol);
    }

    [Fact]
    public void NoSnapDirectionIsNoSnapAtAll()
    {
        Assert.Null(HeadLook.SnapTargets(0f, 0f));
    }

    // The pad aims absolutely: a deflection IS an angle, scaled by the envelope the chase camera
    // swings through. Half a stick is half the angle, which is exactly what the relative
    // free-look path below does NOT do.
    [Theory]
    [InlineData(1f, 0f, 0f, -HeadLook.PadLookYawMaxDeg)]
    [InlineData(-1f, 0f, 0f, HeadLook.PadLookYawMaxDeg)]
    [InlineData(0f, 1f, HeadLook.PadLookPitchMaxDeg, 0f)]
    [InlineData(0f, -1f, -HeadLook.PadLookPitchMaxDeg, 0f)]
    [InlineData(0.5f, 0.5f, HeadLook.PadLookPitchMaxDeg / 2f, -HeadLook.PadLookYawMaxDeg / 2f)]
    public void ThePadAimsAtTheStickPosition(float right, float up, float elevationDeg, float azimuthDeg)
    {
        var (elevation, azimuth) = HeadLook.PadAimTargets(right, up);
        Assert.Equal(Mathf.DegToRad(elevationDeg), elevation, Tol);
        Assert.Equal(Mathf.DegToRad(azimuthDeg), azimuth, Tol);
    }

    // An absolute stick with first person's decoded level floor would leave the bottom half of its
    // travel inert, so the pad path carries its own symmetric bound instead. The floor still binds
    // every other path, asserted below.
    [Fact]
    public void ThePadLooksBelowLevelWhereTheOtherPathsCannot()
    {
        var head = new HeadLook();          // first person: ElevationFloor 0, level
        head.Step(0.1f, Pad(0f, -1f));
        Assert.Equal(Mathf.DegToRad(-HeadLook.PadLookPitchMaxDeg), head.TargetElevation, Tol);

        head = new HeadLook();
        head.Step(0.5f, Free(0f, -1f));     // the decoded relative path stops at the floor
        Assert.Equal(0f, head.TargetElevation, Tol);
    }

    // Precedence: the discrete commands stay on top of a standing absolute aim, and a centred
    // stick claims no frame at all, so it blocks neither the mouse nor the idle rule.
    [Fact]
    public void ADiscreteCommandBeatsTheStickAndACentredStickClaimsNothing()
    {
        var head = new HeadLook();
        head.Step(0.1f, new HeadLookInput(0f, 1f, 0f, 0f, false, 1f, 0f));   // Kp8 with the stick over
        Assert.Equal(HeadLook.MaxElevation, head.TargetElevation, Tol);
        Assert.Equal(0f, head.TargetAzimuth, Tol);

        head = new HeadLook();
        head.Step(0.5f, new HeadLookInput(0f, 0f, 0f, 1f, false, 0f, 0f));   // mouse only, pad centred
        Assert.Equal(1f, head.TargetElevation, Tol);
    }

    // Release is the idle frame, so the head returns to straight ahead on its own: the stick
    // position is the whole state, and nothing of it survives letting go.
    [Fact]
    public void ReleasingTheStickReturnsTheHeadToStraightAhead()
    {
        var head = new HeadLook();
        head.Step(0.1f, Pad(-1f, 0.5f));
        Assert.True(head.TargetAzimuth > 1f);
        head.Step(0.1f, Idle);
        Assert.Equal(0f, head.TargetAzimuth, Tol);
        Assert.Equal(0f, head.TargetElevation, Tol);
    }

    // The pad reaches the same angles the chase camera swings through, which is the whole
    // request: one envelope, read by both, so they cannot drift apart.
    [Fact]
    public void ThePadSharesTheChaseCamerasEnvelope()
    {
        Assert.Equal(150f, HeadLook.PadLookYawMaxDeg);
        Assert.Equal(60f, HeadLook.PadLookPitchMaxDeg);
        Assert.True(HeadLook.PadLookPitchMaxDeg < Mathf.RadToDeg(HeadLook.MaxElevation),
            "the shared pitch bound must stay inside the head's own ceiling");
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

    // The chase camera's own floor, the literal the chase placement passes the original's
    // controller: the same head pans a further quarter turn down there than it can in the cockpit.
    [Fact]
    public void TheChaseFloorLetsTheSameHeadLookStraightDown()
    {
        Assert.Equal(0f, HeadLook.FirstPersonElevationFloor);
        Assert.Equal(-Mathf.Pi / 2f, HeadLook.ChaseElevationFloor, Tol);

        var head = new HeadLook(HeadLook.ChaseElevationFloor);
        for (int i = 0; i < 60; i++)
        {
            head.Step(0.1f, Free(0f, -1f));
        }
        Assert.Equal(HeadLook.ChaseElevationFloor, head.TargetElevation, Tol);
    }

    // The floor is per frame, not per head: the placing view sets it every step, which is how one
    // shared head can be floored at level in the cockpit and at straight down on the chase camera.
    [Fact]
    public void TheFloorFollowsTheViewThatPlacesTheFrame()
    {
        var head = new HeadLook(HeadLook.ChaseElevationFloor);
        head.Step(0.5f, Free(0f, -1f));
        Assert.True(head.TargetElevation < -0.9f);

        head.ElevationFloor = HeadLook.FirstPersonElevationFloor;
        head.Step(0.1f, Free(0f, -1f));
        Assert.Equal(0f, head.TargetElevation, Tol);
    }

    [Fact]
    public void AzimuthStopsHardAtDeadAstern()
    {
        // The original's head reaches directly behind and goes no further (confirmed at its
        // controls); 8 rad of commanded left pan therefore parks the target exactly at +pi.
        var head = new HeadLook();
        for (int i = 0; i < 40; i++)
        {
            head.Step(0.1f, Free(-1f, 0f));
        }
        Assert.Equal(Mathf.Pi, head.TargetAzimuth, 1e-3f);
        head.Step(0.1f, Free(1f, 0f));         // one step back off the stop moves it again
        Assert.True(head.TargetAzimuth < Mathf.Pi - 0.1f);
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
    public void AzimuthSwingsBackThroughTheFrontNeverAcrossTheStop()
    {
        // A hard-stopped head cannot cross dead astern: retargeting from aft-left (+3π/4) to
        // aft-right (−3π/4) swings the long way round through the front, so the shown azimuth
        // moves TOWARD zero first and stays inside ±π throughout.
        var head = new HeadLook();
        for (int i = 0; i < 200; i++)
        {
            head.Step(0.05f, Snap(-1f, -1f));
        }
        float before = head.Azimuth;
        Assert.Equal(3f * Mathf.Pi / 4f, before, 1e-3f);
        head.Step(0.05f, Snap(1f, -1f));
        Assert.True(head.Azimuth < before);
        Assert.InRange(head.Azimuth, -Mathf.Pi, Mathf.Pi);
    }

    // The two modes differ exactly here, which is the whole reason the original carries the state
    // byte: a released pan holds the head where it was pointed in free-look, and the snap key puts
    // the head back on the mode whose released direction returns it to straight ahead.
    [Fact]
    public void AReleasedPanHoldsTheHeadInFreeLookAndTheSnapKeyBringsItBack()
    {
        var head = new HeadLook();
        for (int i = 0; i < 10; i++)
        {
            head.Step(0.1f, Free(-1f, 1f));
        }
        Assert.Equal(LookMode.FreeLook, head.Mode);
        float elevation = head.TargetElevation, azimuth = head.TargetAzimuth;
        Assert.NotEqual(0f, azimuth);

        head.Step(0.1f, Idle);
        Assert.Equal(elevation, head.TargetElevation, Tol);
        Assert.Equal(azimuth, head.TargetAzimuth, Tol);

        head.Step(0.1f, SnapKey);
        Assert.Equal(LookMode.Snap, head.Mode);
        Assert.Equal(0f, head.TargetElevation, Tol);
        Assert.Equal(0f, head.TargetAzimuth, Tol);
    }

    // The mode keys, the original's Access Snap Look Mode and Access Smooth Look Mode: each states
    // which behaviour is live, and the smooth one centres the head on the way in, targets and shown
    // angles together, as its own handler does.
    [Fact]
    public void TheModeKeysStateTheModeAndTheSmoothOneCentresTheHead()
    {
        var head = new HeadLook();
        Assert.Equal(LookMode.Snap, head.Mode);

        head.Step(0.5f, Free(-1f, 1f));
        Assert.NotEqual(0f, head.Azimuth);
        head.Step(0.1f, SmoothKey);
        Assert.Equal(LookMode.FreeLook, head.Mode);
        Assert.Equal(0f, head.TargetAzimuth, Tol);
        Assert.Equal(0f, head.Azimuth, Tol);
        Assert.Equal(0f, head.Elevation, Tol);

        head.Step(0.1f, SnapKey);
        Assert.Equal(LookMode.Snap, head.Mode);
    }

    // The device inference stays live over the key, in both directions: the key states a default
    // and the next device to move takes the mode back off it.
    [Fact]
    public void AMouseMoveAfterTheSnapKeyAndASnapAfterTheSmoothKeyBothSwitchTheMode()
    {
        var head = new HeadLook();
        head.Step(0.1f, SnapKey);
        head.Step(0.5f, Free(0f, 1f));
        Assert.Equal(LookMode.FreeLook, head.Mode);
        Assert.Equal(HeadLook.FreeLookRate * 0.5f, head.TargetElevation, Tol);

        head = new HeadLook();
        head.Step(0.1f, SmoothKey);
        head.Step(0.1f, Snap(-1f, 0f));
        Assert.Equal(LookMode.Snap, head.Mode);
        Assert.Equal(Mathf.Pi / 2f, head.TargetAzimuth, Tol);
    }

    // One mode owns the frame, so a numpad direction and a mouse pan arriving together do not both
    // move the head: the snap direction decides, and the pan is not integrated on top of it.
    [Fact]
    public void OneModeOwnsTheFrameWhenBothDevicesMoveAtOnce()
    {
        var both = new HeadLook();
        var snapOnly = new HeadLook();
        var input = new HeadLookInput(-1f, 0f, 1f, 1f, false);
        both.Step(0.5f, input);
        snapOnly.Step(0.5f, Snap(-1f, 0f));

        Assert.Equal(LookMode.Snap, both.Mode);
        Assert.Equal(snapOnly.TargetElevation, both.TargetElevation, Tol);
        Assert.Equal(snapOnly.TargetAzimuth, both.TargetAzimuth, Tol);

        // ABLE-TO-FAIL CONTROL: the same pan without the direction is the free-look frame it was
        // held out of, so the assertion above is about the mode and not about an inert input.
        var panOnly = new HeadLook();
        panOnly.Step(0.5f, Free(1f, 1f));
        Assert.Equal(LookMode.FreeLook, panOnly.Mode);
        Assert.NotEqual(0f, panOnly.TargetElevation);
    }

    // Autohead is gated on the snap state in the original, so the idle hook is not consulted at all
    // while free-look owns the head: that mode's idle frame holds the pose instead.
    [Fact]
    public void TheIdleHookIsSilentInFreeLookMode()
    {
        int calls = 0;
        var head = new HeadLook { IdleAim = () => { calls++; return (0.5f, 0.5f); } };
        head.Step(0.1f, SmoothKey);
        head.Step(0.1f, Idle);
        Assert.Equal(0, calls);

        // Back in snap, both frames consult it: the key's own frame carries no look input either.
        head.Step(0.1f, SnapKey);
        head.Step(0.1f, Idle);
        Assert.Equal(2, calls);
    }

    // The held control is its own claim on the free-look arm: a mouse that has stopped moving is
    // not a released button, so the head stays where the mouse put it and the shown angles settle
    // onto that pose rather than chasing the centre.
    [Fact]
    public void AHeldControlOverAStillMouseHoldsTheLook()
    {
        var head = new HeadLook();
        for (int i = 0; i < 10; i++)
        {
            head.Step(0.1f, Looking(-1f, 1f));
        }
        float elevation = head.TargetElevation, azimuth = head.TargetAzimuth;
        Assert.NotEqual(0f, azimuth);
        for (int i = 0; i < 30; i++)
        {
            head.Step(0.1f, Looking());
        }
        Assert.Equal(elevation, head.TargetElevation, Tol);
        Assert.Equal(azimuth, head.TargetAzimuth, Tol);
        Assert.Equal(azimuth, head.Azimuth, 1e-3f);
        Assert.Equal(elevation, head.Elevation, 1e-3f);
    }

    // Releasing the control leaves the head where the mouse put it, the free-look mode it entered
    // when the mouse first moved, and the centre key is what brings it back there: the original's
    // state 1 zeroes the angles on that key alone and on nothing else.
    [Fact]
    public void ReleasingTheHeldControlHoldsTheLookAndTheCentreKeyBringsItBack()
    {
        var head = new HeadLook();
        for (int i = 0; i < 10; i++)
        {
            head.Step(0.1f, Looking(-1f, 1f));
        }
        float azimuth = head.TargetAzimuth;
        Assert.NotEqual(0f, azimuth);

        head.Step(0.1f, Idle);
        Assert.Equal(LookMode.FreeLook, head.Mode);
        Assert.Equal(azimuth, head.TargetAzimuth, Tol);

        head.Step(0.1f, new HeadLookInput(0f, 0f, 0f, 0f, true));
        Assert.Equal(0f, head.TargetElevation, Tol);
        Assert.Equal(0f, head.TargetAzimuth, Tol);
        Assert.Equal(LookMode.FreeLook, head.Mode);
    }

    // The head still takes the delta while the control is held: the flag decides who owns the
    // frame, not how far the pan goes, so a held pan reads exactly as the motion-only path did.
    [Fact]
    public void AHeldControlStillPansAtTheDecodedRate()
    {
        var held = new HeadLook();
        var motionOnly = new HeadLook();
        held.Step(0.5f, Looking(0f, 1f));
        motionOnly.Step(0.5f, Free(0f, 1f));
        Assert.Equal(HeadLook.FreeLookRate * 0.5f, held.TargetElevation, Tol);
        Assert.Equal(motionOnly.TargetElevation, held.TargetElevation, Tol);
    }

    // The arms above free-look keep their place: the centre key and the pad's absolute aim both
    // claim the frame off a held control, so neither is stranded while the button is down.
    [Fact]
    public void TheCentreKeyAndThePadStillBeatAHeldControl()
    {
        var centred = new HeadLook();
        centred.Step(0.5f, Looking(-1f, 1f));
        centred.Step(0.1f, new HeadLookInput(0f, 0f, 0f, 0f, true, 0f, 0f, true));
        Assert.Equal(0f, centred.TargetAzimuth, Tol);

        var aimed = new HeadLook();
        aimed.Step(0.1f, new HeadLookInput(0f, 0f, 0f, 0f, false, 1f, 0f, true));
        Assert.Equal(-Mathf.DegToRad(HeadLook.PadLookYawMaxDeg), aimed.TargetAzimuth, Tol);
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
        // With no head angles, use the fixed −4.70° tilt about the plane's right axis.
        var expected = attitude * new Basis(Vector3.Right, -0.08203f);
        Assert.True(position.IsEqualApprox(Vector3.One + (attitude * Vector3.Up)));
        Assert.True(basis.Z.IsEqualApprox(expected.Z));
        Assert.True(basis.X.IsEqualApprox(expected.X));
    }

    // C22: HeadLook.AutoheadTarget, the idle-frame lean law fed to IdleAim, exercised as a pure
    // vector law directly, no PlaneStats in the loop.

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
        // exactly as negligible, the same null a parked aircraft returns.
        Assert.Null(HeadLook.AutoheadTarget(new Vector3(0f, 0f, -100f), ShippedTurnTime, ShippedTurnMax, ShippedMinPitch));
    }

    [Fact]
    public void AutoheadPinsTheDecodedMinusThreeDegreeFloorOnAHardDive()
    {
        // A steep dive at speed: the raw lean angle is well past −3°, so the floor, not the
        // magnitude cap's direction, decides the shown elevation. This is the trap's own pin:
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
        // (elevation, azimuth) angles, not through an arctangent, the cap having any effect at
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

    // Track Target (the original's state 2): the head holds the selected target's own bearing every
    // frame, with no rate of its own, and the shown angles do all the smoothing there is.

    [Theory]
    [InlineData(0f, 0f, -100f, 0f, 0f)]                          // dead ahead
    [InlineData(-100f, 0f, 0f, 0f, Mathf.Pi / 2f)]               // abeam to port, azimuth is +left
    [InlineData(100f, 0f, 0f, 0f, -Mathf.Pi / 2f)]               // abeam to starboard
    [InlineData(0f, 0f, 100f, 0f, Mathf.Pi)]                     // dead astern
    [InlineData(0f, 100f, -100f, Mathf.Pi / 4f, 0f)]             // 45° up, dead ahead
    [InlineData(0f, -100f, -100f, -Mathf.Pi / 4f, 0f)]           // 45° down, dead ahead
    public void ThePadlockBearingIsTheTargetsOwnAngleInThePlanesFrame(
        float x, float y, float z, float elevation, float azimuth)
    {
        var (e, a) = HeadLook.PadlockTargets(new Vector3(x, y, z));
        Assert.Equal(elevation, e, Tol);
        Assert.Equal(0f, HeadLook.Wrap(a - azimuth), Tol);
    }

    [Fact]
    public void TrackTargetTogglesIntoPadlockAndBackToSnapNeverToFreeLook()
    {
        var head = new HeadLook();
        head.Step(0.1f, SmoothKey);
        Assert.Equal(LookMode.FreeLook, head.Mode);

        head.Step(0.1f, PadlockKey);
        Assert.Equal(LookMode.Padlock, head.Mode);
        head.Step(0.1f, Idle);                          // held, not a second press edge
        Assert.Equal(LookMode.Padlock, head.Mode);

        head.Step(0.1f, PadlockKey);
        Assert.Equal(LookMode.Snap, head.Mode);
    }

    [Fact]
    public void APadlockedHeadTurnsOntoATargetAbeamAtTheDecodedAzimuthRate()
    {
        const float dt = 0.1f;
        // The toggle's own frame is already a padlock frame, as the original's is: its command
        // handler writes the state before the controller reads it.
        var head = new HeadLook { TargetOffset = () => new Vector3(-100f, 0f, 0f) };
        head.Step(dt, PadlockKey);
        Assert.Equal(Mathf.Pi / 2f, head.TargetAzimuth, Tol);
        Assert.Equal(HeadLook.Approach(0f, Mathf.Pi / 2f, HeadLook.AzimuthSmoothRate, dt),
            head.Azimuth, Tol);

        for (int i = 0; i < 100; i++)
        {
            head.Step(dt, Idle);
        }

        Assert.Equal(Mathf.Pi / 2f, head.Azimuth, 1e-3f);
    }

    // The only bound the padlock law carries is the placing view's own floor, so a target below a
    // cockpit head holds at level while the chase camera's head follows it down.
    [Fact]
    public void TheViewsFloorIsTheOnlyClampAPadlockedHeadTakes()
    {
        var cockpit = Padlocked(new Vector3(0f, -100f, -100f));
        cockpit.Step(0.1f, Idle);
        Assert.Equal(HeadLook.FirstPersonElevationFloor, cockpit.TargetElevation, Tol);

        var chase = Padlocked(new Vector3(0f, -100f, -100f), HeadLook.ChaseElevationFloor);
        chase.Step(0.1f, Idle);
        Assert.Equal(-Mathf.Pi / 4f, chase.TargetElevation, Tol);
    }

    // A target crossing dead astern flips the bearing's sign, and the head crosses the ±π seam by
    // the short arc rather than unwinding through the nose: the wrap the original applies before
    // every chase, which only this state reaches.
    [Fact]
    public void ATargetPassingBehindIsFollowedAcrossTheTailNotThroughTheFront()
    {
        var offset = new Vector3(-10f, 0f, 100f);                // aft and a little to port
        var head = new HeadLook { TargetOffset = () => offset };
        head.Step(0.1f, PadlockKey);
        for (int i = 0; i < 200; i++)
        {
            head.Step(0.05f, Idle);
        }

        float before = head.Azimuth;
        Assert.True(before > 3f, $"settled aft-left, read {before}");

        offset = new Vector3(10f, 0f, 100f);                     // now aft and a little to starboard
        head.Step(0.05f, Idle);
        Assert.True(head.Azimuth > before,
            $"the first step goes on toward the tail, not back through the nose (read {head.Azimuth} against {before})");
        for (int i = 0; i < 200; i++)
        {
            head.Step(0.05f, Idle);
        }

        Assert.True(head.Azimuth < -3f, $"and settles aft-right, read {head.Azimuth}");
        Assert.InRange(head.Azimuth, -Mathf.Pi, Mathf.Pi);
    }

    // No target is the state's own idle frame: the angles zero, and the autohead hook owns it, the
    // second arm of the original's own gate.
    [Fact]
    public void APadlockWithNothingSelectedCentresTheHeadAndReachesTheIdleHook()
    {
        var bare = new HeadLook();
        bare.Step(0.1f, PadlockKey);
        bare.Step(0.5f, Free(-1f, 1f));
        // The exit writes SNAP whichever slot caused it, so a pan out of padlock lands there and
        // the free-look state is one further input away.
        Assert.Equal(LookMode.Snap, bare.Mode);

        bare = new HeadLook();
        bare.Step(0.1f, PadlockKey);
        bare.Step(0.1f, Idle);
        Assert.Equal(0f, bare.TargetAzimuth, Tol);
        Assert.Equal(0f, bare.TargetElevation, Tol);

        int calls = 0;
        var leaning = new HeadLook { IdleAim = () => { calls++; return (-0.0524f, 0.2f); } };
        leaning.Step(0.1f, PadlockKey);
        leaning.Step(0.1f, Idle);
        Assert.Equal(2, calls);                                  // the toggle's own frame is one
        Assert.Equal(0.2f, leaning.TargetAzimuth, Tol);
        Assert.Equal(-0.0524f, leaning.TargetElevation, Tol);
    }

    // The exit scan is the eight direction slots and nothing else: the centre key and the pad's
    // absolute aim are polled and discarded while padlocked, as the original's own arm does.
    [Fact]
    public void ASnapDirectionLeavesPadlockWhileTheCentreKeyAndThePadDoNot()
    {
        var head = Padlocked(new Vector3(-100f, 0f, 0f));
        head.Step(0.1f, new HeadLookInput(0f, 0f, 0f, 0f, true));
        Assert.Equal(LookMode.Padlock, head.Mode);
        Assert.Equal(Mathf.Pi / 2f, head.TargetAzimuth, Tol);

        head.Step(0.1f, Pad(1f, 0f));
        Assert.Equal(LookMode.Padlock, head.Mode);
        Assert.Equal(Mathf.Pi / 2f, head.TargetAzimuth, Tol);

        // The exit frame still aims at the target; the snap state owns the frame after it.
        head.Step(0.1f, Snap(0f, 1f));
        Assert.Equal(LookMode.Snap, head.Mode);
        Assert.Equal(Mathf.Pi / 2f, head.TargetAzimuth, Tol);
        head.Step(0.1f, Snap(0f, 1f));
        Assert.Equal(HeadLook.MaxElevation, head.TargetElevation, Tol);
        Assert.Equal(0f, head.TargetAzimuth, Tol);
    }

    // A head already in padlock, fed a target: the helper every case above starts from.
    private static HeadLook Padlocked(Vector3 offset,
        float floor = HeadLook.FirstPersonElevationFloor)
    {
        var head = new HeadLook(floor) { TargetOffset = () => offset };
        head.Step(0.1f, PadlockKey);
        return head;
    }

    private static HeadLookInput Snap(float x, float y) => new(x, y, 0f, 0f, false);

    private static HeadLookInput Free(float right, float up) => new(0f, 0f, right, up, false);

    private static HeadLookInput Pad(float right, float up) => new(0f, 0f, 0f, 0f, false, right, up);

    // The free-look control held, carrying whatever the mouse moved this frame: zero is the still
    // mouse a held button cannot otherwise be told apart from a released one.
    private static HeadLookInput Looking(float right = 0f, float up = 0f) =>
        new(0f, 0f, right, up, false, 0f, 0f, true);
}
