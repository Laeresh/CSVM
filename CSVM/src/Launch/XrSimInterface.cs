using CSVM.Flight.Camera;
using CSVM.Spec;
using Godot;

namespace CSVM.Launch;

/// <summary>
/// The <c>--xr-sim</c> stand-in for a headset. This XR interface renders the main viewport in
/// stereo through Godot's own XR path, with no OpenXR runtime behind it. The head is pinned by the
/// flag (<see cref="XrSimEyes"/>), so a <c>--det</c> run draws the same pair every time. The window
/// shows both eyes side by side, undistorted; captures read the eye layers themselves
/// (<c>Tooling/CaptureDirector.cs</c>). <see cref="XrSession"/> boots it in place of OpenXR.
/// </summary>
public partial class XrSimInterface : XRInterfaceExtension
{
    /// <summary>The name the interface registers under with <see cref="XRServer"/>.</summary>
    public const string InterfaceName = "CSVM xr-sim";

    private readonly XrSimEyes _eyes;
    private readonly Vector2 _eyePx;
    private XRPositionalTracker? _head;
    private bool _initialized;

    /// <summary>Required by Godot's script instancing; never used.</summary>
    public XrSimInterface()
    {
        _eyes = new XrSimEyes(0f, 0f, Vector3.Zero, XrSimSpec.Default.IpdMm, XrSimSpec.Default.FovDeg);
        _eyePx = XrSimSpec.Default.EyePx;
    }

    /// <summary>The headset <paramref name="spec"/> describes.</summary>
    public XrSimInterface(XrSimSpec spec)
    {
        _eyes = new XrSimEyes(spec.YawDeg, spec.PitchDeg, spec.Head, spec.IpdMm, spec.FovDeg);
        _eyePx = spec.EyePx;
    }

    public override StringName _GetName() => InterfaceName;

    public override uint _GetCapabilities() => (uint)XRInterface.Capabilities.Stereo;

    public override bool _IsInitialized() => _initialized;

    public override bool _Initialize()
    {
        if (_initialized)
        {
            return true;
        }

        // XRCamera3D follows the tracker named "head", as it does under OpenXR.
        _head = new XRPositionalTracker
        {
            Type = XRServer.TrackerType.Head,
            Name = "head",
            Description = "xr-sim pinned head",
        };
        _head.SetPose("default", _eyes.Head, Vector3.Zero, Vector3.Zero, XRPose.TrackingConfidenceEnum.High);
        XRServer.AddTracker(_head);
        XRServer.PrimaryInterface = this;
        _initialized = true;
        return true;
    }

    public override void _Uninitialize()
    {
        if (!_initialized)
        {
            return;
        }

        if (_head != null)
        {
            XRServer.RemoveTracker(_head);
            _head = null;
        }

        if (XRServer.PrimaryInterface == this)
        {
            XRServer.PrimaryInterface = null;
        }

        _initialized = false;
    }

    public override Godot.Collections.Dictionary _GetSystemInfo() => new()
    {
        ["XRRuntimeName"] = InterfaceName,
        ["XRRuntimeVersion"] = string.Empty,
    };

    public override XRInterface.TrackingStatus _GetTrackingStatus() => XRInterface.TrackingStatus.NormalTracking;

    public override bool _SupportsPlayAreaMode(XRInterface.PlayAreaMode mode) => mode == XRInterface.PlayAreaMode.Sitting;

    public override XRInterface.PlayAreaMode _GetPlayAreaMode() => XRInterface.PlayAreaMode.Sitting;

    public override bool _SetPlayAreaMode(XRInterface.PlayAreaMode mode) => mode == XRInterface.PlayAreaMode.Sitting;

    public override Vector2 _GetRenderTargetSize() => _eyePx;

    public override uint _GetViewCount() => 2;

    // The two below run on the render thread. XRServer hands that thread its own copy of the
    // origin, recenter frame and scale, and the eyes are immutable, so nothing here races.
    public override Transform3D _GetCameraTransform() => _eyes.Camera(XRServer.WorldScale);

    public override Transform3D _GetTransformForView(uint view, Transform3D camTransform) =>
        _eyes.View(view, camTransform, XRServer.GetReferenceFrame(), XRServer.WorldScale);

    public override double[] _GetProjectionForView(uint view, double aspect, double zNear, double zFar) =>
        _eyes.Projection(aspect, zNear, zFar);

    public override void _PostDrawViewport(Rid renderTarget, Rect2 screenRect)
    {
        // Only the main window has a screen rect; anything else has nowhere to show.
        if (screenRect.Size.X <= 0f || screenRect.Size.Y <= 0f)
        {
            return;
        }

        var half = new Vector2I((int)(screenRect.Size.X * 0.5f), (int)screenRect.Size.Y);
        var origin = new Vector2I((int)screenRect.Position.X, (int)screenRect.Position.Y);
        var whole = new Rect2(0f, 0f, 1f, 1f);
        AddBlit(renderTarget, whole, new Rect2I(origin, half), true, 0, false, Vector2.Zero, 0.0, 0.0, 1.0, 1.0);
        AddBlit(renderTarget, whole, new Rect2I(origin + new Vector2I(half.X, 0), half), true, 1, false, Vector2.Zero, 0.0, 0.0, 1.0, 1.0);
    }
}
