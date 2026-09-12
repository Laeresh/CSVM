using System;
using Godot;

namespace CSVM.Flight;

/// <summary>One frame of look input, in the head's own conventions: a snap direction as a
/// composed (x, y) with +x right and +y forward, a free-look direction with +right and +up, the
/// center key, and the pad's absolute aim with the same +right/+up signs.
/// The snap and free-look directions are read for DIRECTION only, so a half-deflected input
/// pans exactly as fast as a full one, which is what the original's hat-switch input does. That
/// rule does NOT bind <paramref name="PadRight"/>/<paramref name="PadUp"/>: those carry a
/// MAGNITUDE, because the pad aims absolutely (docs/controls.md). Defaulted, so a caller forcing
/// one of the other paths cannot leave a stale deflection on the frame.</summary>
public readonly record struct HeadLookInput(
    float SnapX, float SnapY, float FreeRight, float FreeUp, bool Center,
    float PadRight = 0f, float PadUp = 0f);

/// <summary>
/// The pilot's head, in the two first-person views and on the chase camera alike: where it is
/// being told to look (the TARGET angles, set by a snap direction, integrated by free-look, or
/// zeroed by the center key) and where it is actually looking (the SHOWN angles, chasing the
/// targets exponentially at the decoded rates). Elevation is 0 at level and +π/2 straight up,
/// clamped to <see cref="ElevationFloor"/>..π/2, the floor the placing view sets; azimuth is 0
/// straight ahead, positive to the left, and CLAMPED to ±π, the head stops at dead astern and
/// never pans past it, the original's own stop confirmed at its controls. Both bounds bind the
/// relative paths (snap, free-look, center); the absolute pad aim carries its own envelope, see
/// <see cref="PadAimTargets"/>. Engine-free apart from <see cref="Mathf"/>: the shown angles become
/// a basis in <see cref="CameraController.FirstPersonPose"/> and a chase offset in <see cref="CameraController.ChaseSwing"/>.
/// </summary>
public sealed class HeadLook
{
    /// <summary>Free-look pan rate, rad/s, in whichever direction the input points
    /// (docs/org/cameraViews.md, head-look controller: <c>angle += 2·dt·…</c>).</summary>
    public const float FreeLookRate = 2f;

    /// <summary>Exponential catch-up rate of the shown elevation toward its target, 1/s
    /// (τ ≈ 0.33 s). Elevation is deliberately slower than azimuth, as in the original.</summary>
    public const float ElevationSmoothRate = 3f;

    /// <summary>Exponential catch-up rate of the shown azimuth toward its target, 1/s
    /// (τ ≈ 0.20 s).</summary>
    public const float AzimuthSmoothRate = 5f;

    /// <summary>The highest the head looks: straight up. The original's own ceiling, shared by
    /// every caller of the controller.</summary>
    public const float MaxElevation = Mathf.Pi / 2f;

    /// <summary>The elevation floor a first-person frame sets: level. The head never looks below
    /// the horizon there (docs/org/cameraViews.md, head-look controller).</summary>
    public const float FirstPersonElevationFloor = 0f;

    /// <summary>The elevation floor a chase-camera frame sets: straight down, the literal
    /// <c>0xbfc90fdb</c> the chase placement passes the controller, so the view can look below
    /// level as well as above it (docs/org/cameraViews.md).</summary>
    public const float ChaseElevationFloor = -Mathf.Pi / 2f;

    /// <summary>The snap elevation for a diagonal direction: 45° up (<c>0.7853982</c>).</summary>
    public const float DiagonalElevation = Mathf.Pi / 4f;

    /// <summary>The pad look envelope: where a fully deflected right stick aims, left/right and
    /// up/down. NOT decoded (the original binds no absolute stick), a UX call for this port, and
    /// shared with <see cref="CameraController.PadLook"/> so the chase camera and the first-person
    /// head cannot drift apart. ⚠ The pitch bound is the CHASE camera's gimbal margin, not the
    /// head's: at 90° that camera sits over the plane and <c>Basis.LookingAt</c>'s up hint goes
    /// parallel to the view. Widening it here gimbals chase.</summary>
    public const float PadLookYawMaxDeg = 150f, PadLookPitchMaxDeg = 60f;

    // How near a direction must be to dead ahead, and to a 45° diagonal, for the snap to lift the
    // head: 0.1° and 0.09° respectively. Windows rather than equalities because the original's
    // input is a POV angle in hundredths of a degree; a digital key cluster hits them exactly.
    private const float ForwardWindowRad = 0.001745f;
    private const float DiagonalWindowRad = 0.001571f;

    // How near centre counts as at rest, 0.1°: the window Settled reads.
    private const float SettledWindowRad = 0.001745f;

    /// <summary>The floor this head starts on; the placing view sets it per frame afterwards.
    /// </summary>
    public HeadLook(float elevationFloor = FirstPersonElevationFloor) =>
        ElevationFloor = elevationFloor;

    /// <summary>Consulted on any frame with no look input at all, and its answer
    /// becomes the targets directly. It sets elevation past <see cref="ElevationFloor"/> on
    /// purpose, autohead's own floor is below level, so the value is taken as given and only the
    /// azimuth is clamped. Null (the default) returns the idle head to straight ahead, the
    /// original's own idle rule in its default snap-look mode; a head that stays where it was
    /// parked is the J smooth-look mode, filed, not this.</summary>
    public Func<(float Elevation, float Azimuth)?>? IdleAim { get; set; }

    /// <summary>The lowest elevation this head may be told to look at:
    /// <see cref="FirstPersonElevationFloor"/> in the cockpit and <see cref="ChaseElevationFloor"/>
    /// on the chase camera. Settable rather than fixed because the original's one head serves both
    /// placements and they differ only in this, so the view placing the frame sets it before
    /// <see cref="Step"/> (<see cref="CameraController.StepHead"/>). A lowered target above the new
    /// floor is left alone; the next input path re-clamps it.</summary>
    public float ElevationFloor { get; set; }

    /// <summary>Whether the head is at rest, straight ahead and level, to within a tenth of a
    /// degree. A released head decays toward centre asymptotically and never lands on it exactly,
    /// so this is a window rather than an equality.</summary>
    public bool Settled =>
        Mathf.Abs(Elevation) < SettledWindowRad && Mathf.Abs(Azimuth) < SettledWindowRad;

    /// <summary>Where the head is being told to look, above level.</summary>
    public float TargetElevation { get; private set; }

    /// <summary>Where the head is being told to look, left of the nose.</summary>
    public float TargetAzimuth { get; private set; }

    /// <summary>Where the head is actually looking, above level, the angle a view basis is built
    /// from.</summary>
    public float Elevation { get; private set; }

    /// <summary>Where the head is actually looking, left of the nose.</summary>
    public float Azimuth { get; private set; }

    /// <summary>The snap mapping: a direction becomes an azimuth (its own angle, mirrored so that
    /// pointing right looks right) and one of three elevations, dead ahead looks straight UP, a
    /// 45° diagonal looks 45° up, anything else looks level. Returns null for no direction at all.
    /// Pure; <paramref name="dirX"/> is +right and <paramref name="dirY"/> +forward.</summary>
    public static (float Elevation, float Azimuth)? SnapTargets(float dirX, float dirY)
    {
        if (dirX == 0f && dirY == 0f)
        {
            return null;
        }
        float angle = Mathf.Atan2(dirX, dirY);      // 0 = dead ahead, positive = to the right
        float a = Mathf.Abs(angle);
        float elevation = 0f;
        if (a <= ForwardWindowRad)
        {
            elevation = MaxElevation;
        }
        else if (Mathf.Abs(a - (Mathf.Pi / 4f)) <= DiagonalWindowRad
              || Mathf.Abs(a - (3f * Mathf.Pi / 4f)) <= DiagonalWindowRad)
        {
            elevation = DiagonalElevation;
        }
        return (elevation, Wrap(-angle));
    }

    /// <summary>The absolute pad mapping: stick position IS head position, scaled by the shared
    /// <see cref="PadLookYawMaxDeg"/>/<see cref="PadLookPitchMaxDeg"/> envelope. Deliberately
    /// ignores <see cref="ElevationFloor"/>: that floor bounds the original's relative controls,
    /// and first person floors it at level, which would leave the lower half of an absolute
    /// stick's travel inert. Pure; <paramref name="right"/> is +right, <paramref name="up"/> +up,
    /// both expected in [−1, 1].</summary>
    public static (float Elevation, float Azimuth) PadAimTargets(float right, float up) =>
        (Mathf.Clamp(up, -1f, 1f) * Mathf.DegToRad(PadLookPitchMaxDeg),
         Mathf.Clamp(-right, -1f, 1f) * Mathf.DegToRad(PadLookYawMaxDeg));

    /// <summary>The idle-frame lean into the plane's own velocity, local-frame X/Y only
    /// (forward speed dropped, why, and the (elevation, azimuth) derivation, are
    /// docs/formats/vehicle/player-globals.md's autohead row). Scaled by <paramref
    /// name="turnTime"/>, capped in magnitude at <paramref name="turnMax"/>, floored at <paramref
    /// name="minPitch"/>, below <see cref="ElevationFloor"/> on purpose, <see cref="Step"/>'s idle
    /// branch bypasses it. Null when the lean is negligible.</summary>
    public static (float Elevation, float Azimuth)? AutoheadTarget(
        Vector3 localVelocity, float turnTime, float turnMax, float minPitch)
    {
        Vector2 lean = new Vector2(localVelocity.X, localVelocity.Y) * turnTime;
        float mag = lean.Length();
        if (mag <= 1e-4f)
        {
            return null;
        }
        if (mag > turnMax)
        {
            lean *= turnMax / mag;
        }
        float elevation = Mathf.Max(lean.Y, minPitch);
        float azimuth = ClampAzimuth(-lean.X);
        return (elevation, azimuth);
    }

    /// <summary>The decoded smoothing law: <c>shown = target + (shown − target)·e^(−rate·dt)</c>.
    /// Frame-rate independent by construction, and pure so it unit-tests at the decoded
    /// rates.</summary>
    public static float Approach(float shown, float target, float rate, float dt) =>
        target + ((shown - target) * Mathf.Exp(-rate * dt));

    /// <summary>An angle folded into ±π, used to normalise an INPUT angle before it becomes a
    /// target. The live azimuth is clamped, not wrapped: see <see cref="ClampAzimuth"/>.</summary>
    public static float Wrap(float angle) => Mathf.PosMod(angle + Mathf.Pi, Mathf.Tau) - Mathf.Pi;

    /// <summary>The azimuth hard stop: the head reaches dead astern (±π) and goes no further,
    /// so free-look cannot wind past the tail and come round the other side.</summary>
    public static float ClampAzimuth(float angle) => Mathf.Clamp(angle, -Mathf.Pi, Mathf.Pi);

    /// <summary>One frame: pick this frame's target from the input, then chase it. The center key
    /// beats a snap, a snap beats the pad's absolute aim, that beats free-look, and a frame with
    /// none of them is the idle frame <see cref="IdleAim"/> owns. A centred pad claims no frame,
    /// so a plugged-in stick blocks neither the mouse nor autohead. The chase runs whatever the
    /// input was, so every path (snap, pad, free-look, center, autohead) reaches the eye through
    /// the same law.</summary>
    public void Step(float dt, in HeadLookInput input)
    {
        var snap = SnapTargets(input.SnapX, input.SnapY);
        if (input.Center)
        {
            SetTargets(0f, 0f);
        }
        else if (snap != null)
        {
            SetTargets(snap.Value.Elevation, snap.Value.Azimuth);
        }
        else if (input.PadRight != 0f || input.PadUp != 0f)
        {
            // Above free-look and below the discrete commands: an absolute aim is a standing
            // instruction, so a momentary snap or center press still overrides it while held.
            // Assigned rather than SetTargets'd, since this path owns its own bounds.
            var (elevation, azimuth) = PadAimTargets(input.PadRight, input.PadUp);
            TargetElevation = elevation;
            TargetAzimuth = azimuth;
        }
        else if (input.FreeRight != 0f || input.FreeUp != 0f)
        {
            FreeLook(input.FreeRight, input.FreeUp, dt);
        }
        else
        {
            // No look input at all: autohead owns the frame when enabled, else the head returns
            // to straight ahead, the original's idle rule in its default snap-look mode, for
            // snap and free-look alike. The smoothing below makes it a swing, not a cut.
            var idle = IdleAim?.Invoke();
            if (idle != null)
            {
                TargetElevation = idle.Value.Elevation;
                TargetAzimuth = ClampAzimuth(idle.Value.Azimuth);
            }
            else
            {
                SetTargets(0f, 0f);
            }
        }

        Elevation = Approach(Elevation, TargetElevation, ElevationSmoothRate, dt);
        // A plain chase, no wrap: targets are clamped to ±π, so the head swings back through the
        // front to reach the other side, which is exactly what a hard-stopped head does.
        Azimuth = Approach(Azimuth, TargetAzimuth, AzimuthSmoothRate, dt);
    }

    /// <summary>Put the head straight ahead at once, targets and shown angles together, what a
    /// fresh spawn wants, as against the center key's smoothed return.</summary>
    public void Reset()
    {
        SetTargets(0f, 0f);
        Elevation = 0f;
        Azimuth = 0f;
    }

    // Integrate the targets at the fixed pan rate along the input direction. The direction is
    // normalised first: the rate is the original's whole law here, so a barely-deflected stick and
    // a hard one pan alike, and the caller may pass raw pixels or a curved axis.
    private void FreeLook(float right, float up, float dt)
    {
        float length = Mathf.Sqrt((right * right) + (up * up));
        if (length <= 0f)
        {
            return;
        }
        float step = FreeLookRate * dt;
        SetTargets(TargetElevation + (step * (up / length)), TargetAzimuth - (step * (right / length)));
    }

    private void SetTargets(float elevation, float azimuth)
    {
        TargetElevation = Mathf.Clamp(elevation, ElevationFloor, MaxElevation);
        TargetAzimuth = ClampAzimuth(azimuth);
    }
}
