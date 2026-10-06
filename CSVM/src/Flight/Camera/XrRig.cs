using System;
using CSVM.Flight.Airframe;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Camera;

/// <summary>
/// What the VR seat needs of a stereo cockpit interior (<c>Flight/Hud/XrCockpit.cs</c>). It needs
/// the seated eye's offset in the aircraft's frame, and a placement under the XR origin each frame.
/// An interface because this namespace names nothing in Flight but the airframe.
/// </summary>
public interface IXrInterior
{
    /// <summary>The seated eye's offset from the interior's mount, in the aircraft's frame.</summary>
    Vector3 EyeOffset { get; }

    /// <summary>Places the interior for an origin standing at the eye on <paramref name="aircraft"/>
    /// with its attitude.</summary>
    void Follow(in Transform3D origin, Node3D aircraft);
}

/// <summary>
/// The stereo flavour of a <see cref="PlayerRig"/>: the one VR seat's <see cref="XROrigin3D"/> and
/// tracked <see cref="XRCamera3D"/>, and the pane a session binds to them. <c>Launch/XrSession.cs</c>
/// builds it at boot and calls <see cref="Place()"/> each frame after every camera writer. That puts
/// the origin where the pane's view asks (<see cref="XrAnchor"/>, <see cref="XrMount"/>) and the
/// pilot's stereo interior with it, and raises <see cref="Cut"/> where the view jumps. The pane's
/// sky follows <see cref="Head"/> (<see cref="PlayerRig.SkyAnchor"/>).
/// The flat camera stays current: its planes, Environment and cull mask serve the stereo render.
/// </summary>
public sealed class XrRig
{
    private readonly Camera3D _fallback;
    private readonly Func<FlightController, IXrInterior?> _interiorOf;
    private PlayerRig? _pane;

    // The anchor the pilot's camera last asked for, which a frame nothing wrote keeps.
    private XrAnchor _written = XrAnchor.Level;

    // What the last placed frame stood on, so a change in any of it is a cut.
    private (FlightController? Pilot, bool Owns, int Shot, XrAnchor Anchor) _shot;

    // The level heading the origin keeps while a cutscene plays on the theater screen.
    private Basis _screenHeading = Basis.Identity;

    /// <summary>The rig over <paramref name="origin"/> and its child <paramref name="head"/>. The
    /// origin stands at <paramref name="fallback"/>, the persistent flat camera, while no pane is
    /// bound; <paramref name="interiorOf"/> finds a pilot's stereo interior, or null.</summary>
    public XrRig(XROrigin3D origin, XRCamera3D head, Camera3D fallback, Func<FlightController, IXrInterior?> interiorOf)
    {
        Origin = origin;
        Head = head;
        _fallback = fallback;
        _interiorOf = interiorOf;
    }

    /// <summary>Raised on a frame the origin cuts rather than moves on. That is a view change, a
    /// respawn, a static camera's new spot, or another owner taking or returning the camera. A
    /// comfort fade hangs on it; the argument names the cause for the log.</summary>
    public event Action<string>? Cut;

    /// <summary>The headset's tracking space in the world.</summary>
    public XROrigin3D Origin { get; }

    /// <summary>The tracked head under <see cref="Origin"/>, where the player's eyes are.</summary>
    public XRCamera3D Head { get; }

    /// <summary>The pane the headset flies, or null at the menu.</summary>
    public PlayerRig? Pane => _pane;

    /// <summary>How the origin took the pane's camera on the last <see cref="Place()"/>.</summary>
    public XrAnchor Anchor { get; private set; } = XrAnchor.Level;

    /// <summary>Whether the bound pane's camera now frames a presenting cutscene, which the theater
    /// screen plays as a stereo pair rather than around the seat. Set by the composition; unset
    /// reads false.</summary>
    public Func<bool>? CutscenePlays { get; set; }

    /// <summary>Whether the last <see cref="Place()"/> stood the origin for a cutscene on the theater
    /// screen. It stands at the cutscene camera's eye, on the level heading held since the cutscene
    /// took the camera. The world behind the surround then never turns about the seat, and the sky
    /// still centres on the eye the pair is drawn from.</summary>
    public bool Screening { get; private set; }

    /// <summary>Makes <paramref name="pane"/> the one the headset flies.</summary>
    public void Bind(PlayerRig pane)
    {
        if (_pane != null && _pane != pane)
        {
            _pane.Xr = null;
        }

        _pane = pane;
        _written = XrAnchor.Level;
        pane.Xr = this;
    }

    /// <summary>Lets <paramref name="pane"/> go as its session ends. A later session's pane, bound
    /// already, stays.</summary>
    public void Release(PlayerRig pane)
    {
        pane.Xr = null;
        if (_pane == pane)
        {
            _pane = null;
        }
    }

    /// <summary>Places the origin for this frame at the bound pane's eye, or at the persistent
    /// camera with none, and the pane's stereo interior with it. A camera the pilot's own controller
    /// wrote is taken as that write asks (<see cref="XrAnchor"/>). One another owner drives is taken
    /// by its heading, or for a cutscene as <see cref="Screening"/> says. A frame nothing wrote keeps
    /// the last anchor, levelled once the aircraft is down. Returns the origin's pose.</summary>
    public Transform3D Place()
    {
        var pane = _pane;
        var camera = pane != null && GodotObject.IsInstanceValid(pane.Camera) ? pane.Camera : _fallback;
        var pilot = pane?.Controller is { } controller && GodotObject.IsInstanceValid(controller) ? controller : null;
        var views = pilot is { CameraOwned: false } ? pilot.CameraControl : null;
        bool screening = pilot is { CameraOwned: true } && CutscenePlays?.Invoke() == true;
        if (screening && !Screening)
        {
            _screenHeading = XrMount.Level(Origin.GlobalTransform.Basis);
        }

        Screening = screening;
        XrAnchor anchor;
        if (views == null)
        {
            anchor = XrAnchor.Level;
        }
        else if (views.TakeXrWrite() is { } written)
        {
            anchor = _written = written;
        }
        else
        {
            anchor = pilot!.Crashed ? XrAnchor.Level : _written;
        }

        // A seat stands only on a live aircraft in a first-person view; a wreck's attitude tumbles.
        if (anchor == XrAnchor.Seat && pilot is not { FirstPersonView: true, Crashed: false })
        {
            anchor = XrAnchor.Level;
        }

        var interior = pilot != null ? _interiorOf(pilot) : null;
        var mount = screening
            ? XrMount.Screened(camera.GlobalTransform.Origin, _screenHeading)
            : XrMount.For(camera.GlobalTransform, anchor, pilot?.GlobalTransform.Basis ?? Basis.Identity,
                interior?.EyeOffset ?? Vector3.Zero);
        NoteCut(pilot, views, anchor, mount);
        Origin.GlobalTransform = mount;
        if (pilot != null && interior != null)
        {
            interior.Follow(mount, pilot);
        }

        return mount;
    }

    /// <summary>The placement with its inputs given: the flat camera's <paramref name="eye"/>, the
    /// flown <paramref name="aircraft"/> and its <paramref name="attitude"/> in a first-person view
    /// (null otherwise), and its stereo <paramref name="interior"/>. A first-person origin stands at
    /// the cockpit marker plus the interior's <see cref="IXrInterior.EyeOffset"/>.</summary>
    public Transform3D Place(in Transform3D eye, Node3D? aircraft, Basis? attitude, IXrInterior? interior)
    {
        var mount = XrMount.For(eye, attitude, interior?.EyeOffset ?? Vector3.Zero);
        Origin.GlobalTransform = mount;
        if (aircraft != null && interior != null)
        {
            interior.Follow(mount, aircraft);
        }

        return mount;
    }

    /// <summary>Brings the seated eye back onto the origin: the head's current position becomes the
    /// origin and its heading the origin's, its tilt kept. The runtime applies it to the eyes at once
    /// and to <see cref="Head"/> on its next pose.</summary>
    public void Recenter(string why)
    {
        XRServer.CenterOnHmd(XRServer.RotationMode.ResetButKeepTilt, false);
        Log.Info("core", $"xr: recentered ({why})");
    }

    // Raises Cut when the frame stands on something new: another aircraft, the camera changing
    // hands, or the pilot's camera cutting. An anchor change counts only where it moves the origin.
    // A crash cut's held pose read again by heading is the same pose, so it is no second cut.
    private void NoteCut(FlightController? pilot, CameraController? views, XrAnchor anchor, in Transform3D mount)
    {
        var was = _shot;
        var wasMount = Origin.GlobalTransform;
        _shot = (pilot, views != null, views?.Shot ?? 0, anchor);
        Anchor = anchor;
        string? why = !ReferenceEquals(pilot, was.Pilot) ? "a new aircraft"
            : _shot.Owns != was.Owns ? (_shot.Owns ? "the pilot's camera back" : "another owner took the camera")
            : _shot.Shot != was.Shot ? "the camera cut"
            : anchor != was.Anchor && !mount.IsEqualApprox(wasMount) ? $"{was.Anchor} to {anchor}"
            : null;
        if (why == null)
        {
            return;
        }

        Log.Debug("core", $"xr: cut ({why}) anchor={anchor}");
        Cut?.Invoke(why);
    }
}
