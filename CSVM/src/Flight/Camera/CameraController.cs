using System;
using CSVM.Bindings;
using CSVM.Flight.Airframe;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Camera;

/// <summary>Which camera drew this pilot's frame, the identity the view breadcrumb names. Not a
/// <see cref="PilotViewMode"/>: that is the view the pilot SELECTED, while this is the camera that
/// actually held the frame, including the ones a held key, the right stick or the aircraft's own
/// destruction puts it into. The breadcrumb writes a name and never one of these values, so the
/// order here is free to read well.</summary>
public enum CameraView
{
    /// <summary>The following chase camera, logged under the pad's unbound middle digit.</summary>
    Chase,

    /// <summary>The chase camera swung off its settled pose by the pilot's head: the numpad snap
    /// cluster, the centre key or the mouse. Where it went is in the offset and aim the breadcrumb
    /// prints beside this name.</summary>
    Look,

    /// <summary>The look-behind view: Look Back held (numpad 0 or the right-stick click by
    /// default), or <c>--view=back</c>.</summary>
    Back,

    /// <summary>The pad look-around, a continuously variable twin of the numbered views rather
    /// than one of their digits.</summary>
    PadLook,

    /// <summary>The two SELECTED first-person views, which are modes rather than held keys: a
    /// scripted <c>--view=cockpit</c>/<c>--view=nose</c> run reads its own mode back off the
    /// breadcrumb, and a capture proves which view it framed.</summary>
    Cockpit,

    Nose,

    /// <summary>The two STATIC cameras a scripted run can be in: the flyby a key or
    /// <c>--view=flyby</c> entered, and the death camera the player's own destruction cuts to. Both
    /// hold a WORLD point, so the breadcrumb's plane-frame offset grows as the aircraft leaves,
    /// which is how a held camera is told from one riding the aeroplane.</summary>
    Flyby,

    Death,
}

/// <summary>
/// Drives the flown aircraft's camera: the roll-following chase camera and the head that swings
/// it. It also holds the pilot's SELECTED view mode (<see cref="ViewMode"/>: Chase, Cockpit or
/// Nose) and the free orbit the debug halt uses. Steers a <see cref="Camera3D"/> it does not
/// own, like <see cref="OrbitCamera"/> does for the static viewer.
/// Deliberately passive: no clock and no input devices of its own, see <see cref="Chase"/> and
/// <see cref="Orbit"/> for which clock each uses. The chase RADIUS is dynamic per plane, and its
/// DIRECTION and aim are camparam's authored rig, <see cref="AuthoredRig"/>. The
/// enhanced presentation's one chase cue, its speed widening of the external FOV, enters through
/// <see cref="StepEnhancedCues"/> alone.
/// </summary>
public sealed class CameraController
{
    /// <summary>The <c>--view=back</c> sentinel (out of the numpad's 1–9 digit range): pin the
    /// look-behind view for the whole run, the scripted twin of holding numpad 0.</summary>
    public const int PinnedBackView = 10;

    /// <summary>The <c>--view=flyby</c> sentinel: start in the flyby camera and re-enter it after
    /// every respawn, the scripted twin of pressing the flyby key.</summary>
    public const int PinnedFlybyView = 11;

    // The authored rig's literal scale on its plane-frame vector, 1.0145 (0x3f81db23), so the
    // settled camera sits about 2.4% beyond the radius (docs/org/cameraViews.md, "The chase rig").
    private const float AuthoredRigScale = 1.0145f;

    // The literal 1.977872 (0x3ffd2ae9): both catch-up rates gain this per metre the rig's offset
    // sits off the flight path. A flank view therefore eases about three times as fast as the
    // settled one (docs/org/cameraViews.md, "The chase rig").
    private const float SwungCatchUpGain = 1.977872f;

    private const float OrbitRateDeg = 70f;     // paused orbit-camera slew (deg/s)
    private const float OrbitZoomRate = 1.6f;   // paused orbit-camera dolly (1/s, exponential)
    private const float OrbitMinDist = 4f, OrbitMaxDist = 150f;

    // The numpad +/- zoom axis: the target moves at 2/s, clamped [0, 1]; the shown trim chases
    // it at 1.5/s. 0 is the pose the view rests at, never a mid-point (docs/org/cameraViews.md).
    private const float ZoomAxisRate = 2f, ZoomSmoothRate = 1.5f;

    // How far OUT a fully held zoom axis carries the external camera, in metres. An authored-
    // distance fraction would be wrong: the original adds this flat span whatever the airframe.
    private const float ZoomSpanMetres = 10f;

    private const float ChaseLogInterval = 0.25f; // sim-s between chase-distance breadcrumb lines

    // The enhanced FOV widening, TUNE: 6° wider at rated max, nothing at or below 0.6 of it. Ordinary
    // cruise then reads as it does on the faithful path.
    private const float SpeedFovWidenMaxDeg = 6f;
    private const float SpeedFovCruiseFrac = 0.6f;

    // The decoded per-mode BASE horizontal FOV, in degrees (org/cameraViews.md, "FOV constants
    // and aspect correction": 1.0471976 rad / 1.3962634 rad, exactly 60°/80°). The 60° is the
    // base every camera mode takes; the 80° is the single exception the cockpit interior view
    // (mode 6) gets. Nose and every external view alike read the base.
    private const float BaseHorizontalFovDeg = 60f;
    private const float CockpitHorizontalFovDeg = 80f;

    // The original ran 4:3 only, so the bases above are its horizontal angles at 4:3
    // (org/cameraViews.md, "FOV constants and aspect correction"). The verticals they convert to
    // there are the angles every other aspect holds, so extra width buys world at the sides.
    // ⚠ Do not divide by the live aspect; that pins the horizontal angle instead and crops a
    // wide pane's canopy rails and panel.
    private const float AssumedAspect = 4f / 3f;

    private readonly Camera3D _camera;

    // The view-selection controls' last readings, one slot per control (StepViewKeys).
    private readonly bool[] _viewKeyPrev = new bool[5];

    // A named action as this player's keymap resolves it. A rebound Look Back or zoom follows the
    // binding, and the controller never learns about devices or window focus.
    private readonly Func<InputAction, bool> _held;

    // The view pinned for the whole run (--view=): 0 is nothing pinned, PinnedBackView
    // (--view=back) the look-behind and PinnedFlybyView the flyby. A pinned numpad DIGIT is a
    // held snap direction and is read by the host with the live keys, not here.
    private readonly int _pinnedView;

    // This plane's whole resolved camera block (camparam): every authored figure the views read,
    // from the chase distance and its bounds to the crash offset and the clearance fields the
    // crash cut hands the shared probe. Held as the block, so this plane's tuning has one copy
    // and not two, which is how Statics takes the dozen fields it reads as well.
    // See docs/formats/camparam.md.
    private readonly CamParams _camParams;

    // The plane-local offset of this aircraft's authored cockpit_camera marker (PlaneBuilder,
    // fallback (0,0,0) when the plane has none), both first-person views share it, there is no
    // separate nose marker (docs/org/cameraViews.md).
    private readonly Vector3 _cockpitCameraOffset;

    // The dynamic chase radius: `dist` + `dist_factor`·V + the throttle transient. Advanced by
    // UpdateDynamics on the sim clock, and read by both the chase camera and the fixed views:
    // they are one number, so a numpad snap changes the angle and nothing else.
    private float _radius;
    private float _laggedSpeed;                  // the transient's state: speed's own lagged copy
    private float _distExcess;                   // this step's transient, metres beyond d(V)
    private float _simTime, _logAccum;           // chase breadcrumb bookkeeping

    // The numpad +/- zoom axis's own state: target then shown, both [0, 1], 0 = the rest pose.
    // Kept apart from _radius above; EffectiveRadius is where the two combine.
    private float _zoomTarget, _zoomShown;

    // The two eased aircraft frames the external rig is built on: the offset turns by _posFrame,
    // the aim by _lookFrame. ⚠ Do not ease the camera's WORLD position instead. That trails by
    // V/rate, several chase radii at speed. A chase or look-behind frame seeds them off the live
    // attitude, as the original's mode change does, and every other pose unseeds them.
    private Quaternion _posFrame = Quaternion.Identity, _lookFrame = Quaternion.Identity;
    private bool _framesSeeded;

    // The enhanced cue's state, this frame's FOV widening in degrees. Only StepEnhancedCues writes
    // it, and only under the enhanced presentation, so on the faithful path it stays 0. ⚠ Enhanced
    // has no attitude lag of its own. It rides the decoded catch-up, which already trails a roll.
    // A second lag in its place or on top would part the two presentations in every roll.
    private float _fovWidenDeg;

    private float _orbitYaw, _orbitPitch, _orbitDist; // free orbit-camera state while paused
    private CameraView _viewPrev = CameraView.Chase; // the view the last logged frame was drawn from

    public CameraController(Camera3D camera, CamParams cam, Func<InputAction, bool> held, int pinnedView,
        PilotViewMode viewMode = PilotViewMode.Chase, Vector3 cockpitCameraOffset = default,
        Func<float>? staticDraw = null)
    {
        _camera = camera;
        _held = held;
        _pinnedView = pinnedView;
        ViewMode = viewMode;
        _camParams = cam;
        _radius = cam.Dist;
        _cockpitCameraOffset = cockpitCameraOffset;
        Statics = new StaticCameras(cam, staticDraw ?? Rng.Stream(Rng.Camera).Randf);
        FlybyActive = pinnedView == PinnedFlybyView;
    }

    /// <summary>The vertical FOV every camera outside the cockpit interior draws at: the decoded
    /// 60° horizontal base through <see cref="HorizontalToVerticalFovDeg"/>, 46.8° at the 4:3 the
    /// original ran, and held at every viewport shape. Public because a session builds its cameras
    /// before any controller owns them, so both take the angle from this one place rather than from
    /// a number written beside the camera.</summary>
    public static float ExternalFovDeg => HorizontalToVerticalFovDeg(BaseHorizontalFovDeg);

    /// <summary>Which view this pilot has SELECTED, Chase, Cockpit or Nose. State, not a held
    /// key: it survives until the cycle key or another selection changes it, and a held look-behind
    /// overrides it for as long as that key is down without changing it (see
    /// <see cref="PilotView.Effective"/>). Seeded from <c>--view=cockpit</c>/<c>=nose</c>.</summary>
    public PilotViewMode ViewMode { get; set; }

    /// <summary>The crash, death and flyby cameras' shared placement and world clearance. Exposed
    /// so a suite reads the chosen point and the flyby's own re-site bookkeeping off the law itself
    /// rather than off the camera transform it produced.</summary>
    public StaticCameras Statics { get; }

    /// <summary>Whether the flyby camera holds this pilot's view. Not a
    /// <see cref="PilotViewMode"/>: the original's view selector rejects the flyby exactly as it
    /// rejects the death camera, so it is a state the aeroplane is put into and not one of the
    /// three views a cycle key walks.</summary>
    public bool FlybyActive { get; private set; }

    /// <summary>Where the owned camera's eye is in the world this frame, for a pass that draws
    /// relative to it (<see cref="Hud.CockpitOverlay"/>).</summary>
    public Vector3 EyePosition => _camera.GlobalPosition;

    /// <summary>The whole eye frame, position and aim, for a caller that has to place geometry in
    /// front of the view rather than merely measure from it. The canopy-hole overlay's
    /// <c>AT_NODE camera1</c> reads it through <c>FlightController.EnsureViewCameraProxy</c>.</summary>
    public Transform3D EyePose => _camera.GlobalTransform;

    /// <summary>The pilot's head: snap, free-look and the center key, smoothed to the angles
    /// <see cref="FirstPersonView"/> aims with and <see cref="Chase"/> swings by. ONE head for
    /// every view, as the original has (docs/org/cameraViews.md), so a bearing taken in the cockpit
    /// is the bearing the chase camera shows; only its elevation floor changes with the view
    /// placing the frame (<see cref="StepHead"/>). The host steps it on the SIM clock.</summary>
    public HeadLook Head { get; } = new HeadLook();

    /// <summary>Whether the SELECTED view is one of the two first-person ones, what the anim
    /// data's <c>PLAYER_1ST_PERSON</c> condition is answered with. Reads the selection, not the
    /// momentary override: a numpad key held for a frame does not make the pilot leave the
    /// cockpit.</summary>
    public bool FirstPerson => PilotView.IsFirstPerson(ViewMode);

    // The radius every forward-facing external pose reads. _radius itself never carries either
    // term; the look-behind takes its own bounds and no zoom at all.
    private float EffectiveRadius =>
        ExternalRadius(_radius, _camParams.DistMin, _camParams.DistMax, _zoomShown);

    /// <summary>One press of the original's "Cycle Cockpit Views" key advances the three-stop
    /// cycle: Cockpit → Nose → Chase → Cockpit. It also leaves the flyby, which is how the
    /// original's own selector ends a camera mode it will not accept as a selection.</summary>
    public void CycleCockpitViews()
    {
        FlybyActive = false;
        ViewMode = PilotView.Cycle(ViewMode);
    }

    /// <summary>Select the chase view directly, without walking the three-stop cycle. Leaves the
    /// flyby for the same reason <see cref="CycleCockpitViews"/> does.</summary>
    public void SelectChase()
    {
        FlybyActive = false;
        ViewMode = PilotViewMode.Chase;
    }

    /// <summary>Enter the flyby camera: it takes a fresh spot at once rather than resuming the
    /// deadline the last pass left behind. A no-op while it is already running, so holding the key
    /// does not re-site every frame.</summary>
    public void EnterFlyby()
    {
        if (FlybyActive)
        {
            return;
        }
        FlybyActive = true;
        Statics.ResetFlyby();
    }

    /// <summary>One frame of the view-selection controls, each acted on once per press. The cycle
    /// (<see cref="CycleCockpitViews"/>) and the direct Chase select come from each device half on
    /// its own edge, so a pad-only pilot reaches every view. The flyby comes last. The bindings are
    /// this port's; the original's binding menu also puts its cycle on a joystick button.</summary>
    public void StepViewKeys(bool cycleKey, bool chaseKey, bool cyclePad, bool chasePad, bool flyby)
    {
        if (ViewKeyEdge(0, cycleKey))
            CycleCockpitViews();
        if (ViewKeyEdge(1, chaseKey))
            SelectChase();
        if (ViewKeyEdge(2, cyclePad))
            CycleCockpitViews();
        if (ViewKeyEdge(3, chasePad))
            SelectChase();
        if (ViewKeyEdge(4, flyby))
            EnterFlyby();
    }

    /// <summary>Enter the death camera: one spot chosen on the next step and held for the whole
    /// fall. The pilot's own destruction is what calls it, never a key.</summary>
    public void EnterDeathView()
    {
        FlybyActive = false;
        Statics.Arm();
    }

    /// <summary>Both writes the destroy def's <c>CALLBACK 3</c> makes: the SELECTED view goes back
    /// to the chase camera and the head-look angles return to level and forward
    /// (docs/formats/anim-definitions/cutscenes.md). The selection is what outlives the death, so a
    /// pilot shot down in the cockpit respawns behind the aeroplane.</summary>
    public void ResetToChase()
    {
        SelectChase();
        Head.Reset();
    }

    /// <summary>The look-behind view is on: <see cref="InputAction.LookBack"/> held on any control
    /// it is bound to, or the run pinned it with <c>--view=back</c>. It beats the snap cluster, as
    /// the original's own placement does: its look-behind arm never reaches the head-look
    /// controller.</summary>
    public bool BackActive() => _held(InputAction.LookBack) || _pinnedView == PinnedBackView;

    /// <summary>One frame of the pilot's head, in the shape the original's own controller takes it
    /// (<c>FUN_0042d010(floor, autohead)</c>): the view placing the frame hands the elevation floor
    /// it uses, level in the cockpit and <see cref="HeadLook.ChaseElevationFloor"/> on the chase
    /// camera, and the one shared head takes the input. ⚠ The look-behind steps this head only in
    /// first person, to dead astern while held; the chase camera's look-behind is a rigid pose of
    /// its own, and the original's arm for it never reaches the controller.</summary>
    public void StepHead(float dt, in HeadLookInput input, float elevationFloor)
    {
        Head.ElevationFloor = elevationFloor;
        Head.Step(dt, input);
    }

    /// <summary>The look-behind view (numpad 0, or <c>--view=back</c>): ahead of the nose looking
    /// back at the plane. Distance is the speed-driven radius clamped into the authored
    /// <c>[back_dist_min, back_dist_max]</c>, its own pair. ⚠ The zoom axis is deliberately absent
    /// here; the original applies it only to the forward-facing camera. It rides the chase
    /// camera's eased frames at the unscaled catch-up rates. Switching between the two therefore
    /// never re-seeds the lag (docs/org/cameraViews.md, "The chase rig").</summary>
    public void BackView(float dt, in Transform3D renderPose)
    {
        StepFrames(dt, renderPose.Basis, 1f);
        float r = Mathf.Clamp(_radius, _camParams.BackDistMin, _camParams.BackDistMax);
        var dir = new Vector3(0f, 0f, -1f);     // ahead of the nose, plane frame
        _camera.Position = renderPose.Origin + (new Basis(_posFrame) * (dir * r));
        _camera.Basis = new Basis(_lookFrame) * Basis.LookingAt(-dir, Vector3.Up);
    }

    // Kept beside the methods that swing by it rather than up with the constructor, the same
    // SA1204 trade FirstPersonPose below makes.
#pragma warning disable SA1204
    /// <summary>The analog look-around's swing, in the PLANE's frame: the filtered right stick over
    /// the shared <see cref="HeadLook.PadLookYawMaxDeg"/>/<see cref="HeadLook.PadLookPitchMaxDeg"/>
    /// envelope, through <see cref="ChaseSwing"/>. Stick right carries the camera to starboard and
    /// stick up looks up. A centred stick returns the exact identity. ⚠ Outside free-look the stick
    /// aims ABSOLUTELY here, never through the head; free-look turns the head with it instead
    /// (docs/controls.md).</summary>
    public static Basis PadSwing(float stickX, float stickY) =>
        ChaseSwing(Mathf.DegToRad(-stickY * HeadLook.PadLookPitchMaxDeg),
            Mathf.DegToRad(stickX * HeadLook.PadLookYawMaxDeg));

    /// <summary>The chase rig's swing, in the PLANE's frame: elevation about the plane's right
    /// axis, then azimuth about its up axis. It takes the angle pair <see cref="FirstPersonPose"/>
    /// aims the head with. The authored rig turns its offset and its aim by it, so the camera orbits
    /// the aeroplane and keeps its framing. A zero pair returns the identity. ⚠ Positive azimuth carries the camera to STARBOARD: a pilot looking left is what puts the
    /// camera on the right.</summary>
    public static Basis ChaseSwing(float elevation, float azimuth) =>
        new Basis(Vector3.Up, azimuth) * new Basis(Vector3.Right, elevation);

    /// <summary>The authored chase rig for one head pose, in the PLANE's frame. It returns the offset
    /// per metre of radius and the camera's basis, which looks down its −Z. The pitch rides the
    /// head's elevation. The height shrinks by the swing's squared quaternion scalar, so it vanishes
    /// dead ahead (docs/org/cameraViews.md, "The chase rig"). Pure, so it unit-tests.</summary>
    public static (Vector3 Offset, Basis Aim) AuthoredRig(float elevation, float azimuth,
        float thirdpHeight, float thirdpPitchRad)
    {
        float el = elevation + thirdpPitchRad;
        float w = Mathf.Cos(el * 0.5f) * Mathf.Cos(azimuth * 0.5f);
        var swing = ChaseSwing(el, azimuth);
        var raw = new Vector3(0f, thirdpHeight * w * w * AuthoredRigScale, AuthoredRigScale);
        return (swing * raw, swing);
    }

    /// <summary>The factor on both chase catch-up rates: <c>1 + 1.977872·√(x² + y²)</c>. The root
    /// is how far one <see cref="AuthoredRig"/> offset per metre sits off the flight path. About
    /// 1.27 settled on the default block, 3 on a flank, 1 dead ahead
    /// (docs/org/cameraViews.md, "The chase rig"). Pure, so it unit-tests.</summary>
    public static float CatchUpScale(Vector3 rigOffset) =>
        1f + (SwungCatchUpGain * Mathf.Sqrt((rigOffset.X * rigOffset.X) + (rigOffset.Y * rigOffset.Y)));

    /// <summary>One step of an eased aircraft frame: <c>rate·dt</c> of the way to the live attitude
    /// by the shorter arc. It lands outright once that reaches 1. ⚠ The fraction is linear in
    /// dt, not <c>1 − e^(−rate·dt)</c>. The original's quaternion ease is first-order, unlike its
    /// scalar one. The rate is per real second. Pure, so it unit-tests.</summary>
    public static Quaternion EaseFrame(Quaternion frame, Quaternion live, float rate, float dt)
    {
        float t = rate * dt;
        if (t >= 1f)
        {
            return live;
        }
        return t <= 0f ? frame : frame.Slerp(live, t).Normalized();
    }
#pragma warning restore SA1204

    // Kept beside the instance method that calls it, ahead of the property it's declared after
    // in source, rather than up with the constructor, SA1204 would put it before every instance
    // property, which reads worse than the one local suppression here.
#pragma warning disable SA1204
    /// <summary>The pure first-person placement law: <c>camera_world = plane_pos + plane_rotation
    /// × offset</c>, aimed by the head's own angles. Azimuth turns about the plane's up axis first.
    /// Elevation then turns about the axis that yaw produced, so a sideways look pitches through
    /// the head's own horizon. The fixed tilt, <see cref="Mech3.PlaneBuilder.HeadPitchOffsetRad"/>,
    /// rides the elevation axis as in the original. Static and engine-free so the math is unit-testable;
    /// <see cref="FirstPersonView"/> is the thin write onto a live <see cref="Camera3D"/>.</summary>
    public static (Vector3 Position, Basis Basis) FirstPersonPose(Vector3 planePos, Basis attitude,
        Vector3 cockpitCameraOffset, float elevation = 0f, float azimuth = 0f) =>
        (planePos + (attitude * cockpitCameraOffset),
         attitude * new Basis(Vector3.Up, azimuth) * new Basis(Vector3.Right, elevation + Mech3.PlaneBuilder.HeadPitchOffsetRad));
#pragma warning restore SA1204

    /// <summary>First-person placement (Cockpit mode 6 / Nose mode 7): rigidly mounted at the
    /// plane's <c>cockpit_camera</c> marker via <see cref="FirstPersonPose"/>. No smoothing and no
    /// camera-side shake. The original places this camera from the vehicle's own matrix, so the
    /// wobble turns the interior and never the view (docs/org/shakes.md, "Camera attachment").
    /// The aim is <see cref="Head"/>'s current angles, so a head panned away from the
    /// nose keeps its bearing while the aircraft manoeuvres under it.</summary>
    public void FirstPersonView(in Transform3D renderPose)
    {
        var (position, basis) = FirstPersonPose(renderPose.Origin, renderPose.Basis, _cockpitCameraOffset,
            Head.Elevation, Head.Azimuth);
        _framesSeeded = false;
        _camera.Position = position;
        _camera.Basis = basis;
    }

    // Kept beside the instance method that calls it, for the same SA1204 reason as
    // FirstPersonPose above.
#pragma warning disable SA1204
    /// <summary>The horizontal→vertical FOV law (org/cameraViews.md, "FOV constants and aspect
    /// correction"): <c>vertical = atan(tan(H/2) / assumedAspect)</c>, doubled for the FULL angle
    /// Godot's <see cref="Camera3D.Fov"/> expects (this project sets no <c>keep_aspect</c>
    /// anywhere, so Fov is always the vertical angle). 60°H → 46.8°V and 80°H → 64.4°V, the pinned
    /// values, and every viewport shape holds them while its horizontal angle grows with its
    /// width. Pure, so it unit-tests engine-free.</summary>
    public static float HorizontalToVerticalFovDeg(float horizontalDeg)
    {
        float halfH = Mathf.DegToRad(horizontalDeg) * 0.5f;
        float halfV = Mathf.Atan(Mathf.Tan(halfH) / AssumedAspect);
        return Mathf.RadToDeg(halfV) * 2f;
    }
#pragma warning restore SA1204

    /// <summary>Put the derived per-mode vertical FOV onto the owned camera. It does not depend on
    /// the viewport's shape, so a splitscreen pane and a fullscreen window take the same angle and
    /// each pilot's picture stays independent of the others (Decision 5: no splitscreen-specific
    /// code, the per-pilot camera already owns its own FOV value). Call alongside every <see
    /// cref="FirstPersonView"/> site; <see cref="RestoreExternalFov"/> is the undo for every other
    /// pose.</summary>
    public void ApplyFirstPersonFov() => _camera.Fov = FirstPersonFovDeg(ViewMode);

    // Beside its one caller for the same SA1204 reason as FirstPersonPose above.
#pragma warning disable SA1204
    /// <summary>The vertical FOV a first-person view takes: the per-mode horizontal base through
    /// <see cref="HorizontalToVerticalFovDeg"/>. Public because a second camera drawing the same
    /// eye must take the same angle from the same table rather than a copy of it (<see
    /// cref="Hud.CockpitOverlay"/>).</summary>
    public static float FirstPersonFovDeg(PilotViewMode mode) =>
        HorizontalToVerticalFovDeg(
            mode == PilotViewMode.Cockpit ? CockpitHorizontalFovDeg : BaseHorizontalFovDeg);
#pragma warning restore SA1204

    /// <summary>Put the camera back on <see cref="ExternalFovDeg"/>, the base every view but the
    /// cockpit interior draws at. Every non-first-person pose calls this (a held numpad key or
    /// look-behind while the SELECTION is Cockpit/Nose is an external pose and gets the external
    /// FOV while held, back to first-person FOV on release, same as any other override). Under the
    /// enhanced presentation the speed widening rides on top of that base; it is 0 on the faithful
    /// path and on every static cut, which take <see cref="ApplyDecodedExternalFov"/>.</summary>
    public void RestoreExternalFov() => _camera.Fov = ExternalFovDeg + _fovWidenDeg;

    // Beside the law its one caller drives, the same SA1204 trade FirstPersonPose above makes.
#pragma warning disable SA1204
    /// <summary>The enhanced widening of the external FOV, in degrees: 0 at and below
    /// <see cref="SpeedFovCruiseFrac"/> of the airframe's rated max speed, rising linearly to
    /// <see cref="SpeedFovWidenMaxDeg"/> at rated max and held there above it, so a dive past the
    /// rating does not keep opening the view. Pure, so the ramp unit-tests without a
    /// camera.</summary>
    public static float SpeedFovWiden(float speedFraction) =>
        SpeedFovWidenMaxDeg * Mathf.Clamp(
            (speedFraction - SpeedFovCruiseFrac) / (1f - SpeedFovCruiseFrac), 0f, 1f);
#pragma warning restore SA1204

    /// <summary>Advance the enhanced presentation's chase cue one frame: the widening
    /// <see cref="RestoreExternalFov"/> adds to the external FOV. The pose is the decoded one in
    /// both presentations. Off Enhanced, a live switch away included, it holds the widening at 0.
    /// That keeps the faithful pose and FOV bit-identical.</summary>
    public void StepEnhancedCues(float speedFraction) =>
        _fovWidenDeg = GraphicsMode.Enhanced ? SpeedFovWiden(speedFraction) : 0f;

    /// <summary>The death camera: a spot chosen once from the <c>death_*</c> fields on the frame
    /// the pilot's aircraft is destroyed, held for the whole fall while the view re-aims at the
    /// wreck. Takes the DRAWN pose, like every other per-frame write, and the world probe so the
    /// spot clears the terrain it was chosen over.</summary>
    public void DeathView(in Transform3D renderPose, float speed, IWorldQuery? world,
        Godot.Collections.Array<Rid>? exclude)
    {
        ApplyDecodedExternalFov(); // the death cut is an external framing, whatever view was selected
        AimStatic(Statics.StepDeath(renderPose.Origin, renderPose.Basis, speed, world, exclude),
            renderPose.Origin);
    }

    /// <summary>The flyby camera: a spot out on the aircraft's flank and ahead of it, held while
    /// the aeroplane runs past, then re-sited once the drawn watch time is up and the drawn switch
    /// distance is exceeded. Takes SIM time, so the watch deadline survives a frame-rate change and
    /// a halted sim freezes the pass rather than ending it.</summary>
    public void FlybyView(float simTime, in Transform3D renderPose, float speed, IWorldQuery? world,
        Godot.Collections.Array<Rid>? exclude)
    {
        ApplyDecodedExternalFov();
        AimStatic(
            Statics.StepFlyby(simTime, renderPose.Origin, renderPose.Basis, speed, world, exclude),
            renderPose.Origin);
    }

    /// <summary>The authored crash camera (<c>crash_horiz</c>/<c>crash_y</c>): on a fatal crash
    /// the original hard-cuts to a static elevated vantage looking down at the impact point, then
    /// lifts that vantage clear of the terrain through the same probe the death camera and the
    /// flyby use. Framing decoded off the original's crash footage, docs/formats/camparam.md.</summary>
    public void CrashView(Vector3 impact, Vector3 travelDir, IWorldQuery? world = null,
        Godot.Collections.Array<Rid>? exclude = null)
    {
        ApplyDecodedExternalFov(); // the crash cut is always an external framing, whatever view was selected
        var alongH = new Vector3(travelDir.X, 0f, travelDir.Z);
        Vector3 behind;
        if (alongH.LengthSquared() > 1e-4f)
        {
            behind = -alongH.Normalized();
        }
        else
        {
            // A perfectly vertical dive has no horizontal flight direction, keep the camera's
            // current bearing from the impact so the cut still lands behind the approach.
            var camH = new Vector3(_camera.Position.X - impact.X, 0f, _camera.Position.Z - impact.Z);
            behind = camH.LengthSquared() > 1e-6f ? camH.Normalized() : Vector3.Back;
        }
        AimStatic(
            StaticCameras.LiftClearOfWorld(
                impact + (behind * _camParams.CrashHoriz) + (Vector3.Up * _camParams.CrashY),
                _camParams, world, exclude),
            impact);
    }

    // Kept beside its one caller, for the same SA1204 reason as FirstPersonPose above.
#pragma warning disable SA1204
    /// <summary>The distance a forward-facing external pose sits at: the speed-driven radius held
    /// inside the authored <c>[dist_min, dist_max]</c>, then the zoom axis carried OUTWARD from
    /// there by up to <see cref="ZoomSpanMetres"/>. The clamp comes first, so an aircraft at rest
    /// sits on its near bound and the axis has nowhere inward to go (docs/org/cameraViews.md).
    /// Pure, so the whole law unit-tests without a camera.</summary>
    public static float ExternalRadius(float radius, float distMin, float distMax, float zoomShown) =>
        Mathf.Clamp(radius, distMin, distMax) + (zoomShown * ZoomSpanMetres);

    /// <summary>The zoom axis's raw target: moves toward 1 while <paramref name="zoomOut"/> is
    /// held and toward 0 while <paramref name="zoomIn"/> is held, at <see cref="ZoomAxisRate"/>,
    /// clamped to [0, 1]. Holding both cancels, the same as a plain axis. Pure, so the rate and
    /// clamp unit-test without a camera.</summary>
    public static float ZoomTarget(float target, bool zoomIn, bool zoomOut, float dt) =>
        Mathf.Clamp(target + (((zoomOut ? 1f : 0f) - (zoomIn ? 1f : 0f)) * ZoomAxisRate * dt), 0f, 1f);

    /// <summary>The throttle transient in metres, <c>dist_vary·(V − V̄)</c>, with <c>V̄</c> the
    /// lagged copy of speed handed back for the next step (docs/formats/camparam.md).
    /// ⚠ Read the term BEFORE easing the lag, the order the original uses, and do not convert
    /// <paramref name="distCatchUp"/>: it is authored per real second, which this dt is. The
    /// original's look-behind inversion has no port here. Pure, so the whole law unit-tests
    /// without a camera.</summary>
    public static float DistTransient(float laggedSpeed, float speed, float distVary,
        float distCatchUp, float dt, out float laggedNext)
    {
        laggedNext = HeadLook.Approach(laggedSpeed, speed, distCatchUp, dt);
        return distVary * (speed - laggedSpeed);
    }
#pragma warning restore SA1204

    /// <summary>Advance the numpad +/- zoom axis one frame: the target via <see
    /// cref="ZoomTarget"/>, then the shown trim chasing it at <see cref="ZoomSmoothRate"/> (the
    /// same law <see cref="HeadLook.Approach"/> names). Reads the two zoom actions through the
    /// injected keymap, like <see cref="BackActive"/> does. ⚠ Call only where <see cref="Orbit"/>
    /// is not also running this frame, the weapon lab's held orbit reads the same two actions for
    /// its own dolly.</summary>
    public void UpdateZoom(float dt)
    {
        _zoomTarget = ZoomTarget(_zoomTarget, _held(InputAction.ZoomIn), _held(InputAction.ZoomOut), dt);
        _zoomShown = HeadLook.Approach(_zoomShown, _zoomTarget, ZoomSmoothRate, dt);
    }

    /// <summary>Advance the dynamic chase radius one SIM step: <c>d = dist + dist_factor·V</c>
    /// plus the authored throttle transient <see cref="DistTransient"/>, every term from camparam
    /// (docs/formats/camparam.md). Raw here; <see cref="ExternalRadius"/> is what applies the
    /// authored bounds. Called by the host once per SIM step, never per render frame, so the
    /// speed lag advances on one cadence; a halted or crashed sim takes no steps.</summary>
    public void UpdateDynamics(float dt, float speed)
    {
        if (dt <= 0f)
        {
            return;
        }
        _distExcess = DistTransient(_laggedSpeed, speed, _camParams.DistVary, _camParams.DistCatchUp,
            dt, out _laggedSpeed);
        _radius = _camParams.Dist + (_camParams.DistFactor * speed) + _distExcess;

        // Measurement breadcrumb (file sink always writes debug): a sim-time series of the
        // realized radius, from which a plateau law fit or a transient decay fit can be made
        // without instrumenting a build.
        _simTime += dt;
        _logAccum += dt;
        if (_logAccum >= ChaseLogInterval)
        {
            _logAccum = 0f;
            Log.Debug("flight", $"chase t={_simTime:0.00} v={speed:0.00} d={_radius:0.000} excess={_distExcess:0.000} zoom={_zoomShown:0.000}");
        }
    }

    /// <summary>Chase camera: the authored rig at the dynamic radius on two eased aircraft frames.
    /// The offset's eases at <c>pos_catch_up</c>, the aim's at <c>look_catch_up</c>, both times
    /// <see cref="CatchUpScale"/>. That scale is taken from the head's swing only, deliberately not the
    /// look stick's <see cref="PadSwing"/>, a held glance and not a rig change. Only the attitude eases,
    /// never the head's swing; the stick's swing then turns the finished pose about those frames.
    /// ⚠ Takes SIM dt but the DRAWN pose; a sim/render gap then shows as plane jitter, which a world lerp would mask.</summary>
    public void Chase(float dt, Vector3 planePos, Basis attitude, float stickX = 0f, float stickY = 0f)
    {
        var (offset, aim) = AuthoredRig(Head.Elevation, Head.Azimuth, _camParams.ThirdpHeight,
            _camParams.ThirdpPitchRad);
        StepFrames(dt, attitude, CatchUpScale(offset));
        // ⚠ Do not fold the stick into the eased frames: they would trail it. A centred stick
        // skips the turn entirely.
        if (stickX != 0f || stickY != 0f)
        {
            var stick = PadSwing(stickX, stickY);
            offset = stick * offset;
            aim = stick * aim;
        }
        _camera.Position = planePos + (new Basis(_posFrame) * offset * EffectiveRadius);
        _camera.Basis = new Basis(_lookFrame) * aim;
    }

    /// <summary>The chase view of another aircraft, the out-of-lives pilot's spectating camera. A
    /// <paramref name="fresh"/> target is taken at its settled pose, so a switch cuts rather than
    /// sweeping across the field.</summary>
    public void Watch(float dt, in Transform3D pose, bool fresh)
    {
        if (fresh)
        {
            Head.Reset();
            RestoreExternalFov();
            // The eased frames still follow the pilot's own wreck; seeding them takes the new
            // target's settled pose.
            _framesSeeded = false;
        }
        Chase(dt, pose.Origin, pose.Basis);
    }

    /// <summary>Place the camera at its settled pose immediately, spawn, respawn and the weapon
    /// lab's re-park, where there is nothing to interpolate from. Re-bases the dynamic radius on
    /// the given speed with the transient zeroed: a teleport is not an acceleration.</summary>
    public void Snap(Vector3 planePos, Basis attitude, float speed, in Transform3D renderPose)
    {
        _laggedSpeed = speed;
        _distExcess = 0f;
        _radius = _camParams.Dist + (_camParams.DistFactor * speed);
        // The enhanced widening re-reads the next stepped frame's speed, so a respawn opens on the
        // decoded angle.
        _fovWidenDeg = 0f;
        _posFrame = _lookFrame = attitude.GetRotationQuaternion();
        _framesSeeded = true;
        // A settle-immediately pose starts the pilot looking where the aircraft is going; a head
        // left panned across a respawn would frame the spawn from over the pilot's shoulder.
        Head.Reset();
        RestoreExternalFov(); // default; the first-person arm below overrides it
        // A pinned flyby re-enters on every respawn, and any live one starts its pass over. The
        // spot itself is left to the next stepped frame, which is the one holding the world probe
        // the placement has to clear the terrain through.
        if (_pinnedView == PinnedFlybyView)
        {
            FlybyActive = true;
        }
        if (FlybyActive)
        {
            Statics.ResetFlyby();
        }
        if (FirstPerson)
        {
            // Snap is the settle-immediately path: without this arm a respawn into Cockpit/Nose
            // shows one chase-pose frame. Above the look-behind on purpose, in first person that
            // input is a head look-back, so a spawn never flashes the outside camera.
            FirstPersonView(renderPose);
            ApplyFirstPersonFov();
            return;
        }
        if (BackActive())
        {
            BackView(0f, renderPose);
            return;
        }
        // The head was just reset and the frames seeded, so a zero step is the settled pose.
        Chase(0f, planePos, attitude);
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

    /// <summary>Free orbit camera, run only while the weapon lab holds this airframe: the host
    /// mixes WASD/arrows (or the gamepad left stick) into <paramref name="yawIn"/>/<paramref
    /// name="pitchIn"/> and numpad +/- (or the triggers) into <paramref name="zoomIn"/>, all in
    /// [−1, 1]. The plane stays put, so every angle frames the same pose for side-by-side
    /// screenshots. Takes WALL dt: the subject is standing still while the world runs on.</summary>
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
        _framesSeeded = false;
        _camera.Position = focus + (dir * _orbitDist);
        _camera.LookAt(focus, Vector3.Up);
    }

    /// <summary>One line per frame a view other than the chase camera is held, plus one on the
    /// frame it is released, read back off the camera's own transform so it reports where the
    /// camera ENDED UP rather than the values that were fed to it. A swung chase camera reports
    /// under <see cref="CameraView.Look"/>, whose offset and aim say where the head took it.
    /// Silent (and free) on an ordinary chase-camera flight.</summary>
    public void LogView(CameraView view, Vector3 planePos, Basis attitude)
    {
        if (view == CameraView.Chase && _viewPrev == CameraView.Chase)
        {
            return;
        }
        _viewPrev = view;
        var toPlane = attitude.Inverse();
        var offset = toPlane * (_camera.Position - planePos);
        var aim = toPlane * -_camera.Basis.Z;   // the camera's forward axis, in the plane's frame
        string n = ViewName(view);
        Log.Debug("flight", $"view n={n} offset=({offset.X:0.000},{offset.Y:0.000},{offset.Z:0.000}) dist={offset.Length():0.000} aim=({aim.X:0.000},{aim.Y:0.000},{aim.Z:0.000})");
    }

    // The breadcrumb's name for each view, the one place a scripted capture's own spelling is
    // written: the settled chase camera reads back as 0, the unbound middle of the pad, and a head
    // swinging it as "look", while the rest take the word the CLI and the docs use.
    private static string ViewName(CameraView view) => view switch
    {
        CameraView.Look => "look",
        CameraView.Back => "back",
        CameraView.PadLook => "padlook",
        CameraView.Cockpit => PilotView.Name(PilotViewMode.Cockpit),
        CameraView.Nose => PilotView.Name(PilotViewMode.Nose),
        CameraView.Flyby => "flyby",
        CameraView.Death => "death",
        _ => "0",
    };

    // The decoded external FOV alone, no enhanced widening. ⚠ The three static cuts take this
    // rather than RestoreExternalFov: they hold a framing of the aeroplane instead of riding it, so
    // the speed it was carrying must not open them up, and the cut reads the same in both
    // presentations.
    private void ApplyDecodedExternalFov() => _camera.Fov = ExternalFovDeg;

    // The write every static camera shares: sit on the held world point and look at the aircraft.
    // ⚠ Godot's LookAt rejects an up parallel to the view direction, which a camera lifted to
    // straight overhead reaches, so the up axis swings to world back there rather than throwing.
    private void AimStatic(Vector3 eye, Vector3 target)
    {
        _framesSeeded = false;
        _camera.Position = eye;
        var toTarget = target - eye;
        if (toTarget.LengthSquared() < 1e-6f)
        {
            return;
        }
        var up = Mathf.Abs(toTarget.Normalized().Y) > 0.999f ? Vector3.Back : Vector3.Up;
        _camera.LookAt(target, up);
    }

    // One frame of the two eased aircraft frames the external rig turns by, seeding both off the
    // live attitude when nothing has held them. They roll fully with the plane, so inverted flight
    // shows the world upside down. Both presentations take this one ease.
    private void StepFrames(float dt, Basis attitude, float scale)
    {
        if (!_framesSeeded)
        {
            _posFrame = _lookFrame = attitude.GetRotationQuaternion();
        }
        else
        {
            var live = attitude.GetRotationQuaternion();
            _posFrame = EaseFrame(_posFrame, live, _camParams.PosCatchUp * scale, dt);
            _lookFrame = EaseFrame(_lookFrame, live, _camParams.LookCatchUp * scale, dt);
        }
        _framesSeeded = true;
    }

    // One view-selection control's press edge against its own slot, so each press acts once.
    private bool ViewKeyEdge(int slot, bool down)
    {
        bool pressed = down && !_viewKeyPrev[slot];
        _viewKeyPrev[slot] = down;
        return pressed;
    }
}
