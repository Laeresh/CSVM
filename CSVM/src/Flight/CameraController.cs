using System;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// Drives the flown aircraft's camera: the roll-following chase camera, the numpad fixed views,
/// and the free orbit used while the debug freeze holds the world still. Steers a
/// <see cref="Camera3D"/> it does not own, like <see cref="CSVM.UI.OrbitCamera"/> does for the
/// static viewer.
/// Deliberately passive: no clock and no input devices of its own — see <see cref="Chase"/> and
/// <see cref="Orbit"/> for which clock each uses. The chase RADIUS is dynamic per plane; see
/// <see cref="UpdateDynamics"/>. The offset's DIRECTION is hand-picked, not in the data — see
/// <see cref="CamParams"/> and docs/formats/camparam.md.
/// </summary>
public sealed class CameraController
{
    /// <summary>The <c>--view=back</c> sentinel (out of the numpad's 1–9 digit range): pin the
    /// look-behind view for the whole run, the scripted twin of holding numpad 0.</summary>
    public const int PinnedBackView = 10;

    /// <summary>LogView's marker for the look-behind view (the numpad views log their digit).</summary>
    public const int BackViewLog = -2;

    /// <summary>LogView's marker for the pad look-around (BL-372) — a continuously variable
    /// twin of the numbered views rather than one of their digits.</summary>
    public const int PadLookLog = -3;

    // The chase offset's DIRECTION: behind and above the nose, at atan2(4.5, 16) ≈ 15.7° of
    // elevation. Hand-picked and still a TUNE — camparam ships a distance per plane, not an angle,
    // so only the radius below comes from the data.
    private const float BaseBack = 16f, BaseUp = 4.5f;

    // E42's pad look-around range: how far the right stick swings the view left/right and up/down
    // from the ordinary chase direction. Not decoded — the original binds no such control — so
    // this is a UX judgement call for the port, not authored data. Kept well short of vertical
    // (baseDir sits ~74° off the up axis; ±60° pitch leaves a comfortable margin before
    // Basis.LookingAt's up hint goes parallel to the view direction).
    private const float PadLookYawMaxDeg = 150f, PadLookPitchMaxDeg = 60f;
    private const float CamLookAhead = 40f;
    private const float CamSmooth = 8f;         // 1/s — position catch-up
    private const float CamRotSmooth = 7f;      // 1/s — orientation (basis) catch-up; a touch of
                                                // lag on fast rolls so they read dynamic (TUNE)
    private const float OrbitRateDeg = 70f;     // paused orbit-camera slew (deg/s)
    private const float OrbitZoomRate = 1.6f;   // paused orbit-camera dolly (1/s, exponential)
    private const float OrbitMinDist = 4f, OrbitMaxDist = 150f;

    private const float Diag = 0.70710678f;     // sin/cos 45° — the four diagonal views' components

    // The throttle transient's relaxation rate, in 1/SIM-second. MEASURED off the original's
    // Bloodhawk staircase clips, NOT authored, and it matches no authored camparam constant.
    // ⚠ Applied per SIM dt: the equivalent wall-second figure is 0.90, so feeding wall time here
    // would run the relaxation 39% off. See docs/formats/camparam.md.
    private const float DistTransientRelax = 0.65f;

    // Steady-state excess distance per unit of along-path acceleration, from the same clips:
    // +0.28% of d per (mph/sim-s) = 0.105 m per (m/s²), residual-vs-dV/dt correlation −0.79 to
    // −0.85 in all four takes. MEASURED, not authored — a full-throttle slam peaks ~+15% of the
    // radius and a full cut ~−7%, which is the term the eye actually sees.
    private const float DistTransientPerAccel = 0.105f;

    private const float ChaseLogInterval = 0.25f; // sim-s between chase-distance breadcrumb lines

    // The offset the direction above works out to at unit... i.e. the length of (BaseBack, BaseUp),
    // ≈ 16.62 m. Only used to normalise that direction against the data's own distance.
    private static readonly float BaseDist = Mathf.Sqrt((BaseBack * BaseBack) + (BaseUp * BaseUp));

    // The numpad's fixed views, keyed by the pad's layout: 2 below the plane, 1/3 45° up-left/right,
    // 4/6 level flanks, 7/9 45° above those, 8 ahead looking back; 5 is unbound (the chase camera).
    // `Dir` is the offset direction, `Up` the image up, both in the PLANE's frame (+x right, +y up,
    // −z nose) so every pose banks and rolls with the aircraft. The belly view uses the nose as up
    // since the plane's own up is the view axis there.
    private static readonly (Key Key, int Digit, Vector3 Dir, Vector3 Up)[] Views =
    {
        (Key.Kp1, 1, new Vector3(-Diag, -Diag, 0f), Vector3.Up),
        (Key.Kp2, 2, new Vector3(0f, -1f, 0f), Vector3.Forward),
        (Key.Kp3, 3, new Vector3(Diag, -Diag, 0f), Vector3.Up),
        (Key.Kp4, 4, new Vector3(-1f, 0f, 0f), Vector3.Up),
        (Key.Kp6, 6, new Vector3(1f, 0f, 0f), Vector3.Up),
        (Key.Kp7, 7, new Vector3(-Diag, Diag, 0f), Vector3.Up),
        (Key.Kp8, 8, new Vector3(0f, 0f, -1f), Vector3.Up),
        (Key.Kp9, 9, new Vector3(Diag, Diag, 0f), Vector3.Up),
    };

    private readonly Camera3D _camera;

    // A key, already gated on whether this player flies the keyboard at all — so the controller
    // never learns about pad devices or window focus.
    private readonly Func<Key, bool> _keyDown;

    // The numpad view held for the whole run (--view=); 0 is the chase camera and
    // PinnedBackView (--view=back) the look-behind.
    private readonly int _pinnedView;

    // The authored special-camera geometry (camparam): the crash camera's offset and the
    // look-behind view's distance bounds.
    private readonly float _crashHoriz, _crashY, _backMin, _backMax;

    // The authored base distance and speed factor (camparam, per plane). The fixed numpad views
    // take the same dynamic radius as the chase camera, so a snap changes the angle and nothing
    // else — they are one number, and moving only one desyncs the two cameras.
    private readonly float _dist, _distFactor;

    // The dynamic chase radius: _dist + _distFactor·V + the acceleration transient. Advanced by
    // UpdateDynamics on the sim clock; read by both the chase camera and the fixed views.
    private float _radius;
    private float _distExcess;                   // the transient's state, metres beyond d(V)
    private float _prevSpeed;                    // last sim step's speed (accel derivative)
    private float _simTime, _logAccum;           // chase breadcrumb bookkeeping

    // The smoothed plane→camera offset, world space. The offset eases, never the world position:
    // the original's footage shows its apparent size at 300 mph within 0.5% of its 118 mph value
    // once dist_factor is accounted for, which a first-order WORLD-position follower cannot do —
    // it would trail by V/rate, several chase radii at speed.
    private Vector3 _offset;

    private float _orbitYaw, _orbitPitch, _orbitDist; // free orbit-camera state while paused
    private int _viewPrev = -1;                  // index into Views last applied (-1 = chase camera)

    public CameraController(Camera3D camera, CamParams cam, Func<Key, bool> keyDown, int pinnedView)
    {
        _camera = camera;
        _keyDown = keyDown;
        _pinnedView = pinnedView;
        _dist = cam.Dist;
        _distFactor = cam.DistFactor;
        _radius = cam.Dist;
        _crashHoriz = cam.CrashHoriz;
        _crashY = cam.CrashY;
        _backMin = cam.BackDistMin;
        _backMax = cam.BackDistMax;
    }

    /// <summary>The look-behind view is on: numpad 0 held, the run pinned it with
    /// <c>--view=back</c>, or (BL-372) <paramref name="padClick"/> — this player's right-stick
    /// click, read by the host the same way it reads every other pad button. A held numpad 1–9 key
    /// still wins (the host checks <see cref="ActiveView"/> first), same rule as the pinned numpad
    /// views.</summary>
    public bool BackActive(bool padClick = false) =>
        _keyDown(Key.Kp0) || _pinnedView == PinnedBackView || padClick;

    /// <summary>Which fixed view the camera should hold this frame, as an index into
    /// <see cref="Views"/>, or −1 for the chase camera. A held numpad key beats the scripted
    /// pinned view so a pinned run can still be explored at the controls; with several keys down
    /// the lowest digit wins, which keeps the choice deterministic. There is one keyboard, so in
    /// splitscreen this is player 1's control — the host's key reader returns false for the
    /// others; the D-pad is taken by the weapon selectors, so there is no pad binding.</summary>
    public int ActiveView()
    {
        for (int i = 0; i < Views.Length; i++)
        {
            if (_keyDown(Views[i].Key))
            {
                return i;
            }
        }
        if (_pinnedView != 0)
        {
            for (int i = 0; i < Views.Length; i++)
            {
                if (Views[i].Digit == _pinnedView)
                {
                    return i;
                }
            }
        }
        return -1;
    }

    /// <summary>Snap the camera to one of the fixed perspectives: out along the view's plane-frame
    /// direction at the chase radius, aimed back at the plane. Offset and basis both ride the
    /// plane's attitude, so the pose rolls with the aircraft and inverted flight renders upside
    /// down. Instant, no smoothing — a scripted capture must not depend on catch-up frames.</summary>
    public void FixedView(int view, in Transform3D renderPose)
    {
        var (_, _, dir, up) = Views[view];
        // Rigid views ride the DRAWN pose, not the raw sim pose — the two differ on the realtime
        // clock (render interpolation), and mixing them would jitter the plane inside a view
        // whose whole point is to be bolted to it. Identical on a parent-driven clock.
        _camera.Position = renderPose.Origin + (renderPose.Basis * (dir * _radius));
        _camera.Basis = renderPose.Basis * Basis.LookingAt(-dir, up);
    }

    /// <summary>The look-behind view (numpad 0, or <c>--view=back</c>): ahead of the nose looking
    /// back at the plane. Distance is the chase radius clamped into the authored
    /// <c>[back_dist_min, back_dist_max]</c> — see docs/formats/camparam.md for why that reads as
    /// bounds rather than a law of its own. Rigid and instant, like the numpad views, for the same
    /// scripted-capture reason.</summary>
    public void BackView(in Transform3D renderPose)
    {
        float r = Mathf.Clamp(_radius, _backMin, _backMax);
        var dir = new Vector3(0f, 0f, -1f);     // ahead of the nose, plane frame
        _camera.Position = renderPose.Origin + (renderPose.Basis * (dir * r));
        _camera.Basis = renderPose.Basis * Basis.LookingAt(-dir, Vector3.Up);
    }

    /// <summary>Analog look-around (BL-372): the right stick swings the view around the plane at
    /// the same dynamic radius the chase camera and numpad views share. <paramref name="stickX"/>/
    /// <paramref name="stickY"/> arrive pre-curved and dead-zoned, so both at 0 reduces to the
    /// ordinary chase direction. Rigid and instant like <see cref="FixedView"/>; releasing it lets
    /// <see cref="Chase"/> resume its own catch-up next frame. Not a decode — see
    /// docs/controls.md.</summary>
    public void PadLook(in Transform3D renderPose, float stickX, float stickY)
    {
        float yaw = Mathf.DegToRad(stickX * PadLookYawMaxDeg);
        float pitch = Mathf.DegToRad(-stickY * PadLookPitchMaxDeg);  // stick up = look up
        var baseDir = new Vector3(0f, BaseUp, BaseBack).Normalized();
        var dir = new Basis(Vector3.Up, yaw) * (new Basis(Vector3.Right, pitch) * baseDir);
        _camera.Position = renderPose.Origin + (renderPose.Basis * (dir * _radius));
        _camera.Basis = renderPose.Basis * Basis.LookingAt(-dir, Vector3.Up);
    }

    /// <summary>The authored crash camera (<c>crash_horiz</c>/<c>crash_y</c>): on a fatal crash
    /// the original hard-cuts to a static elevated vantage looking down at the impact point.
    /// Framing decoded off the original's crash footage — see docs/formats/camparam.md.
    /// ⚠ <c>crash_elev</c> and <c>crash_chord_y</c> are NOT wired and are capture-gated
    /// (<c>BL-260</c>); do not guess them into the pose.</summary>
    public void CrashView(Vector3 impact, Vector3 travelDir)
    {
        var alongH = new Vector3(travelDir.X, 0f, travelDir.Z);
        Vector3 behind;
        if (alongH.LengthSquared() > 1e-4f)
        {
            behind = -alongH.Normalized();
        }
        else
        {
            // A perfectly vertical dive has no horizontal flight direction — keep the camera's
            // current bearing from the impact so the cut still lands behind the approach.
            var camH = new Vector3(_camera.Position.X - impact.X, 0f, _camera.Position.Z - impact.Z);
            behind = camH.LengthSquared() > 1e-6f ? camH.Normalized() : Vector3.Back;
        }
        _camera.Position = impact + (behind * _crashHoriz) + (Vector3.Up * _crashY);
        _camera.LookAt(impact, Vector3.Up);
    }

    /// <summary>Advance the dynamic chase radius one SIM step: <c>d = dist + dist_factor·V</c>
    /// (both authored) plus a first-order acceleration transient relaxing at the measured 0.65
    /// /sim-s — see docs/formats/camparam.md. ⚠ Deliberately NOT clamped into
    /// <c>[dist_min, dist_max]</c>. Called by the host once per SIM step, never per render frame,
    /// so the acceleration derivative stays clean; a halted or crashed sim takes no steps.</summary>
    public void UpdateDynamics(float dt, float speed)
    {
        if (dt <= 0f)
        {
            return;
        }
        float accel = (speed - _prevSpeed) / dt;
        _prevSpeed = speed;
        float t = 1f - Mathf.Exp(-DistTransientRelax * dt);
        _distExcess += ((DistTransientPerAccel * accel) - _distExcess) * t;
        _radius = _dist + (_distFactor * speed) + _distExcess;

        // Measurement breadcrumb (file sink always writes debug): a sim-time series of the
        // realized radius, from which a plateau law fit or a transient decay fit can be made
        // without instrumenting a build.
        _simTime += dt;
        _logAccum += dt;
        if (_logAccum >= ChaseLogInterval)
        {
            _logAccum = 0f;
            Log.Debug("flight", $"chase t={_simTime:0.00} v={speed:0.00} d={_radius:0.000} excess={_distExcess:0.000}");
        }
    }

    /// <summary>Chase camera: ride the plane exactly, smoothing only the plane-frame OFFSET
    /// toward the dynamic radius, then slerp orientation toward a look-at ahead of the nose.
    /// Smoothing the offset (not world position) matches the original's speed-flat apparent size;
    /// smoothing the basis lets the horizon roll through inverted flight while keeping fast rolls
    /// dynamic. ⚠ Takes SIM dt, but the DRAWN pose — riding the plane exactly means a sim/render
    /// pose gap becomes visible plane jitter, which a world-position lerp would instead mask.</summary>
    public void Chase(float dt, Vector3 planePos, Basis attitude)
    {
        float tPos = 1f - Mathf.Exp(-CamSmooth * dt);
        _offset = _offset.Lerp(DesiredOffset(attitude, out var camUp), tPos);
        _camera.Position = planePos + _offset;

        var toTarget = planePos - (attitude.Z * CamLookAhead) - _camera.Position;
        if (toTarget.LengthSquared() < 1e-6f)
            return; // camera sitting on the look target (degenerate) — keep last orientation
        // Basis.LookingAt needs the up not parallel to the view direction; the plane's up is ⟂
        // to its nose so this practically never trips, but guard against extreme catch-up poses.
        var up = Mathf.Abs(toTarget.Normalized().Dot(camUp)) > 0.999f ? Vector3.Up : camUp;
        var desired = Basis.LookingAt(toTarget, up);
        float tRot = 1f - Mathf.Exp(-CamRotSmooth * dt);
        // GetRotationQuaternion re-orthonormalizes each side; Basis.Slerp's raw feed lets
        // orthonormality drift compound frame over frame until it trips the "not normalized" assert.
        var current = _camera.Basis.GetRotationQuaternion();
        _camera.Basis = new Basis(current.Slerp(desired.GetRotationQuaternion(), tRot));
    }

    /// <summary>Place the camera at its settled pose immediately — spawn, respawn and the weapon
    /// lab's re-park, where there is nothing to interpolate from. Re-bases the dynamic radius on
    /// the given speed with the transient zeroed: a teleport is not an acceleration.</summary>
    public void Snap(Vector3 planePos, Basis attitude, float speed, in Transform3D renderPose)
    {
        _prevSpeed = speed;
        _distExcess = 0f;
        _radius = _dist + (_distFactor * speed);
        int view = ActiveView();
        if (view >= 0)
        {
            FixedView(view, renderPose);
            return;
        }
        if (BackActive())
        {
            BackView(renderPose);
            return;
        }
        _offset = DesiredOffset(attitude, out var camUp);
        _camera.Position = planePos + _offset;
        _camera.LookAt(planePos - (attitude.Z * CamLookAhead), camUp);
    }

    /// <summary>On entering the paused screenshot freeze, initialise the orbit angles
    /// and distance from the current camera position so it starts where the chase
    /// camera left off (no jump).</summary>
    public void SeedOrbit(Vector3 planePos)
    {
        var v = _camera.Position - planePos;
        _orbitDist = Mathf.Clamp(v.Length(), OrbitMinDist, OrbitMaxDist);
        _orbitYaw = Mathf.Atan2(v.X, v.Z);
        _orbitPitch = _orbitDist > 1e-3f ? Mathf.Asin(Mathf.Clamp(v.Y / _orbitDist, -1f, 1f)) : 0f;
    }

    /// <summary>Free orbit camera used only while paused: the host mixes WASD/arrows (or the
    /// gamepad left stick) into <paramref name="yawIn"/>/<paramref name="pitchIn"/> and numpad
    /// +/- (or the triggers) into <paramref name="zoomIn"/>, all in [−1, 1]. The plane stays put,
    /// so every angle frames the same pose for side-by-side screenshots. Takes WALL dt — the
    /// point of the freeze is to fly the camera around a stopped world.</summary>
    public void Orbit(float dt, Vector3 focus, float yawIn, float pitchIn, float zoomIn)
    {
        float rate = Mathf.DegToRad(OrbitRateDeg);
        _orbitYaw += rate * yawIn * dt;
        _orbitPitch = Mathf.Clamp(_orbitPitch + (rate * pitchIn * dt),
                                  Mathf.DegToRad(-85f), Mathf.DegToRad(85f));
        _orbitDist = Mathf.Clamp(_orbitDist * Mathf.Exp(OrbitZoomRate * zoomIn * dt),
                                 OrbitMinDist, OrbitMaxDist);

        float cp = Mathf.Cos(_orbitPitch);
        var dir = new Vector3(cp * Mathf.Sin(_orbitYaw), Mathf.Sin(_orbitPitch), cp * Mathf.Cos(_orbitYaw));
        _camera.Position = focus + (dir * _orbitDist);
        _camera.LookAt(focus, Vector3.Up);
    }

    /// <summary>One line per frame a fixed view (or the look-behind, <see cref="BackViewLog"/>)
    /// is held, plus one on the frame it is released — read back off the camera's own transform,
    /// so it reports where the camera ENDED UP rather than the values that were fed to it.
    /// Silent (and free) on an ordinary chase-camera flight.</summary>
    public void LogView(int view, Vector3 planePos, Basis attitude)
    {
        if (view == -1 && _viewPrev == -1)
        {
            return;
        }
        _viewPrev = view;
        var toPlane = attitude.Inverse();
        var offset = toPlane * (_camera.Position - planePos);
        var aim = toPlane * -_camera.Basis.Z;   // the camera's forward axis, in the plane's frame
        string n = view == BackViewLog ? "back"
            : view == PadLookLog ? "padlook"
            : (view < 0 ? "0" : Views[view].Digit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Log.Debug("flight", $"view n={n} offset=({offset.X:0.000},{offset.Y:0.000},{offset.Z:0.000}) dist={offset.Length():0.000} aim=({aim.X:0.000},{aim.Y:0.000},{aim.Z:0.000})");
    }

    // Chase from behind and above the nose in the plane's own frame, so the offset (and the
    // camera) roll fully with the plane — inverted flight shows the world upside down. The
    // hand-picked direction (BaseBack, BaseUp), normalised and scaled to the dynamic radius.
    private Vector3 DesiredOffset(Basis attitude, out Vector3 camUp)
    {
        var nose = -attitude.Z;
        camUp = attitude.Y;
        return ((nose * -BaseBack) + (camUp * BaseUp)) * (_radius / BaseDist);
    }
}
