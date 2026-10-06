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
/// builds it at boot and calls <see cref="Place"/> each frame after every camera writer. That puts
/// the origin at the pane's eye (<see cref="XrMount"/>) and the pilot's stereo interior with it. The pane's sky follows <see cref="Head"/> (<see cref="PlayerRig.SkyAnchor"/>).
/// The flat camera stays current: its planes, Environment and cull mask serve the stereo render.
/// </summary>
public sealed class XrRig
{
    private readonly Camera3D _fallback;
    private readonly Func<FlightController, IXrInterior?> _interiorOf;
    private PlayerRig? _pane;

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

    /// <summary>The headset's tracking space in the world.</summary>
    public XROrigin3D Origin { get; }

    /// <summary>The tracked head under <see cref="Origin"/>, where the player's eyes are.</summary>
    public XRCamera3D Head { get; }

    /// <summary>The pane the headset flies, or null at the menu.</summary>
    public PlayerRig? Pane => _pane;

    /// <summary>Makes <paramref name="pane"/> the one the headset flies.</summary>
    public void Bind(PlayerRig pane)
    {
        if (_pane != null && _pane != pane)
        {
            _pane.Xr = null;
        }

        _pane = pane;
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
    /// camera with none, and the pane's stereo interior with it. Returns the origin's pose.</summary>
    public Transform3D Place()
    {
        var pane = _pane;
        var camera = pane != null && GodotObject.IsInstanceValid(pane.Camera) ? pane.Camera : _fallback;
        var pilot = pane?.Controller is { } controller && GodotObject.IsInstanceValid(controller) ? controller : null;
        Basis? attitude = pilot is { FirstPersonView: true, Crashed: false } ? pilot.GlobalTransform.Basis : null;
        return Place(camera.GlobalTransform, pilot, attitude, pilot != null ? _interiorOf(pilot) : null);
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
}
