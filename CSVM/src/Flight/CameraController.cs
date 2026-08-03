using System;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// Drives the flown aircraft's camera: the roll-following chase camera, the numpad fixed views,
/// and the free orbit used while the debug freeze holds the world still. Steers a
/// <see cref="Camera3D"/> it does not own (the rig builds it), like
/// <see cref="CSVM.UI.OrbitCamera"/> does for the static viewer.
///
/// <para>Deliberately passive: it has no clock and no input devices of its own. The host feeds it
/// the dt to use and the already-mixed orbit axes, because which clock a camera runs on is a
/// behaviour, not a detail — the chase camera takes SIM time so a scripted capture is
/// frame-rate independent, while the orbit keeps WALL time so you can fly around a halted
/// world.</para>
///
/// <para>The chase RADIUS is the plane's own, from <see cref="CamParams"/> — the Balmoral sits
/// 25 m back and the Kestrel 14.5 m. The offset's DIRECTION is not in the data and stays the
/// hand-picked behind-and-above one. Nothing else camparam ships is wired: see
/// <see cref="CamParams"/>'s warnings on the undecoded dynamics before reaching for them.</para>
/// </summary>
public sealed class CameraController
{
    // The chase offset's DIRECTION: behind and above the nose, at atan2(4.5, 16) ≈ 15.7° of
    // elevation. Hand-picked and still a TUNE — camparam ships a distance per plane, not an angle,
    // so only the radius below comes from the data.
    private const float BaseBack = 16f, BaseUp = 4.5f;
    private const float CamLookAhead = 40f;
    private const float CamSmooth = 8f;         // 1/s — position catch-up
    private const float CamRotSmooth = 7f;      // 1/s — orientation (basis) catch-up; a touch of
                                                // lag on fast rolls so they read dynamic (TUNE)
    private const float OrbitRateDeg = 70f;     // paused orbit-camera slew (deg/s)
    private const float OrbitZoomRate = 1.6f;   // paused orbit-camera dolly (1/s, exponential)
    private const float OrbitMinDist = 4f, OrbitMaxDist = 150f;

    private const float Diag = 0.70710678f;     // sin/cos 45° — the four diagonal views' components

    // The offset the direction above works out to at unit... i.e. the length of (BaseBack, BaseUp),
    // ≈ 16.62 m. Only used to normalise that direction against the data's own distance.
    private static readonly float BaseDist = Mathf.Sqrt((BaseBack * BaseBack) + (BaseUp * BaseUp));

    /// <summary>The numpad's fixed camera perspectives, keyed by its own spatial layout: 2 straight
    /// under the plane, 1/3 45° up from there to the left/right, 4/6 the level flanks, 7/9 45° above
    /// those flanks, 8 ahead of the nose looking back. 5 is deliberately unbound — the middle of the
    /// pad is where the chase camera already is. <c>Dir</c> is the camera's offset direction and
    /// <c>Up</c> the image up, both unit vectors in the PLANE's frame (+x right, +y up, −z nose), so
    /// every pose banks and rolls with the aircraft. The belly view takes the nose as its up because
    /// the plane's own up is the view axis there.</summary>
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

    // The numpad view held for the whole run (--view=); 0 is the chase camera.
    private readonly int _pinnedView;

    // This airframe's own chase offset: the hand-picked direction above, scaled to the distance
    // camparam ships for it. The fixed numpad views take the same radius, so a snap changes the
    // angle and nothing else — they are one number, and moving only one desyncs the two cameras.
    private readonly float _back, _up, _viewDist;

    private float _orbitYaw, _orbitPitch, _orbitDist; // free orbit-camera state while paused
    private int _viewPrev = -1;                  // index into Views last applied (-1 = chase camera)

    public CameraController(Camera3D camera, CamParams cam, Func<Key, bool> keyDown, int pinnedView)
    {
        _camera = camera;
        _keyDown = keyDown;
        _pinnedView = pinnedView;
        _viewDist = cam.Dist;
        _back = BaseBack * (cam.Dist / BaseDist);
        _up = BaseUp * (cam.Dist / BaseDist);
    }

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
    /// direction at the chase camera's distance, aimed back at the plane. Both the offset and the
    /// whole basis are carried by the plane's attitude rather than re-derived from a world up, so
    /// the pose rolls with the aircraft and inverted flight renders upside down — the same property
    /// the chase camera's basis slerp exists to preserve. The snap is instant (no smoothing): the
    /// point of a fixed view is a repeatable pose, and a scripted capture must not depend on how
    /// many frames of catch-up it waited for.</summary>
    public void FixedView(int view, in Transform3D renderPose)
    {
        var (_, _, dir, up) = Views[view];
        // Rigid views ride the DRAWN pose, not the raw sim pose — the two differ on the realtime
        // clock (render interpolation), and mixing them would jitter the plane inside a view
        // whose whole point is to be bolted to it. Identical on a parent-driven clock.
        _camera.Position = renderPose.Origin + (renderPose.Basis * (dir * _viewDist));
        _camera.Basis = renderPose.Basis * Basis.LookingAt(-dir, up);
    }

    /// <summary>Chase camera: smooth the position toward the rigid behind-and-above offset
    /// (expressed in the plane's frame, so it banks with the plane) and slerp the orientation
    /// toward a look-at of the point ahead of the nose with the plane's own up. Smoothing the
    /// basis — rather than re-deriving a hard LookAt each frame from a near-world up — lets the
    /// horizon roll fully through inverted flight, while the rotational lag keeps fast rolls
    /// reading dynamic instead of glued. Takes the SIM clock's dt.</summary>
    public void Chase(float dt, Vector3 planePos, Basis attitude)
    {
        float tPos = 1f - Mathf.Exp(-CamSmooth * dt);
        _camera.Position = _camera.Position.Lerp(DesiredCamPos(planePos, attitude, out var camUp), tPos);

        var toTarget = planePos - (attitude.Z * CamLookAhead) - _camera.Position;
        if (toTarget.LengthSquared() < 1e-6f)
            return; // camera sitting on the look target (degenerate) — keep last orientation
        // Basis.LookingAt needs the up not parallel to the view direction; the plane's up is ⟂
        // to its nose so this practically never trips, but guard against extreme catch-up poses.
        var up = Mathf.Abs(toTarget.Normalized().Dot(camUp)) > 0.999f ? Vector3.Up : camUp;
        var desired = Basis.LookingAt(toTarget, up);
        float tRot = 1f - Mathf.Exp(-CamRotSmooth * dt);
        // Slerp via GetRotationQuaternion (which re-orthonormalizes each side) rather than
        // Basis.Slerp: the latter feeds the raw basis straight into Quaternion(), and the tiny
        // orthonormality drift that accumulates when the result is fed back frame after frame
        // eventually trips its "not normalized" assert. Re-orthonormalizing here can't compound.
        var current = _camera.Basis.GetRotationQuaternion();
        _camera.Basis = new Basis(current.Slerp(desired.GetRotationQuaternion(), tRot));
    }

    /// <summary>Place the camera at its settled pose immediately — spawn, respawn and the weapon
    /// lab's re-park, where there is nothing to interpolate from.</summary>
    public void Snap(Vector3 planePos, Basis attitude, in Transform3D renderPose)
    {
        int view = ActiveView();
        if (view >= 0)
        {
            FixedView(view, renderPose);
            return;
        }
        _camera.Position = DesiredCamPos(planePos, attitude, out var camUp);
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
    /// gamepad left stick) into <paramref name="yawIn"/>/<paramref name="pitchIn"/> and Shift/Ctrl
    /// (or the triggers) into <paramref name="zoomIn"/>, all in [−1, 1]. The plane stays put, so
    /// every angle frames the same pose for side-by-side screenshots. Takes WALL dt — the point of
    /// the freeze is to fly the camera around a stopped world.</summary>
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

    /// <summary>One line per frame a fixed view is held, plus one on the frame it is released —
    /// read back off the camera's own transform, so it reports where the camera ENDED UP rather
    /// than the values that were fed to it. Silent (and free) on an ordinary chase-camera flight.</summary>
    public void LogView(int view, Vector3 planePos, Basis attitude)
    {
        if (view < 0 && _viewPrev < 0)
        {
            return;
        }
        _viewPrev = view;
        var toPlane = attitude.Inverse();
        var offset = toPlane * (_camera.Position - planePos);
        var aim = toPlane * -_camera.Basis.Z;   // the camera's forward axis, in the plane's frame
        int digit = view < 0 ? 0 : Views[view].Digit;
        Log.Debug("flight", $"view n={digit} offset=({offset.X:0.000},{offset.Y:0.000},{offset.Z:0.000}) dist={offset.Length():0.000} aim=({aim.X:0.000},{aim.Y:0.000},{aim.Z:0.000})");
    }

    // Chase from behind and above the nose in the plane's own frame, so the offset (and the
    // camera) roll fully with the plane — inverted flight shows the world upside down.
    private Vector3 DesiredCamPos(Vector3 planePos, Basis attitude, out Vector3 camUp)
    {
        var nose = -attitude.Z;
        camUp = attitude.Y;
        return planePos - (nose * _back) + (camUp * _up);
    }
}
