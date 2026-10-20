using CSVM.Bindings;
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

    /// <summary>The interaction profile the stand-in's hands report, Meta Touch's, the first profile
    /// the action map serves.</summary>
    public const string HandProfile = "/interaction_profiles/oculus/touch_controller";

    private readonly Vector2 _eyePx;
    private readonly float _ipdMm;
    private readonly float _fovDeg;
    // Builds the foveated shading-rate image Godot asks for when the root's VRS mode is XR.
    private readonly Xrvrs _vrs = new();

    // Swapped whole, never edited, so a render-thread read sees one pose or the other.
    private volatile Pinned _pinned;
    private XRPositionalTracker? _head;
    private XRControllerTracker? _left;
    private XRControllerTracker? _right;
    private bool _initialized;
    // Read on the render thread as well, so it is one word written whole.
    private volatile float _sizeMultiplier = 1f;

    /// <summary>Required by Godot's script instancing; never used.</summary>
    public XrSimInterface()
        : this(XrSimSpec.Default)
    {
    }

    /// <summary>The headset <paramref name="spec"/> describes.</summary>
    public XrSimInterface(XrSimSpec spec)
    {
        _ipdMm = spec.IpdMm;
        _fovDeg = spec.FovDeg;
        _pinned = new Pinned(new XrSimEyes(spec.YawDeg, spec.PitchDeg, spec.Head, spec.IpdMm, spec.FovDeg, spec.RollDeg));
        _eyePx = spec.EyePx;
    }

    /// <summary>The stand-in's twin of OpenXR's <c>render_target_size_multiplier</c>: the eye size
    /// the flag gives, scaled. The main viewport follows it on its next frame, as under OpenXR.</summary>
    public float RenderTargetSizeMultiplier
    {
        get => _sizeMultiplier;
        set => _sizeMultiplier = value;
    }

    private XrSimEyes Eyes => _pinned.Eyes;

    /// <summary>Moves the pinned head as a player shifting in the seat would, for a suite: a launch's
    /// head stays where its flag put it. The tracker reports the new pose at once.</summary>
    public void MoveHead(float yawDeg, float pitchDeg, Vector3 head, float rollDeg = 0f)
    {
        _pinned = new Pinned(new XrSimEyes(yawDeg, pitchDeg, head, _ipdMm, _fovDeg, rollDeg));
        PublishHead();
    }

    /// <summary>Connects the two hand controllers a suite points and presses with, as trackers named
    /// as OpenXR names them and bound to <see cref="HandProfile"/>. A launch has none, so nothing it
    /// draws carries a pointer. Neither hand is aimed until <see cref="AimHand"/>.</summary>
    public void ConnectHands()
    {
        _left ??= Hand(XrHand.Left);
        _right ??= Hand(XrHand.Right);
    }

    /// <summary>Points one connected hand: its aim pose in tracking space, the ray along its -Z. The
    /// pose reaches the XR origin's frame through the reference frame and world scale, as OpenXR's
    /// does.</summary>
    public void AimHand(XrHand hand, Transform3D aim) =>
        HandTracker(hand)?.SetPose("aim", aim, Vector3.Zero, Vector3.Zero, XRPose.TrackingConfidenceEnum.High);

    /// <summary>Sets one action map action on a connected hand, as the runtime reports it: a bool, a
    /// float or a <see cref="Vector2"/> (<see cref="XrControls"/> names the actions).</summary>
    public void SetHandInput(XrHand hand, string action, Variant value)
    {
        // The store OpenXR writes into, flagged by Godot 4.7 for a move its C# glue does not carry.
#pragma warning disable CS0618
        HandTracker(hand)?.SetInput(action, value);
#pragma warning restore CS0618
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
        PublishHead();
        XRServer.AddTracker(_head);
        XRServer.PrimaryInterface = this;
        // A recenter reaches XRCamera3D only through the head's next pose, which OpenXR reports
        // every frame and this pinned head never would.
        XRServer.ReferenceFrameChanged += PublishHead;
        _initialized = true;
        return true;
    }

    public override void _Uninitialize()
    {
        if (!_initialized)
        {
            return;
        }

        XRServer.ReferenceFrameChanged -= PublishHead;
        if (_head != null)
        {
            XRServer.RemoveTracker(_head);
            _head = null;
        }

        foreach (var hand in new[] { _left, _right })
        {
            if (hand != null)
            {
                XRServer.RemoveTracker(hand);
            }
        }

        _left = null;
        _right = null;

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

    public override Vector2 _GetRenderTargetSize() => (_eyePx * _sizeMultiplier).Round();

    public override uint _GetViewCount() => 2;

    public override Rid _GetVrsTexture() => _vrs.MakeVrsTexture(_GetRenderTargetSize(), new[] { Vector2.Zero, Vector2.Zero });

    public override XRInterface.VrsTextureFormat _GetVrsTextureFormat() => XRInterface.VrsTextureFormat.Unified;

    // The two below run on the render thread. XRServer hands that thread its own copy of the
    // origin, recenter frame and scale, and the pinned eyes are swapped whole, so nothing here races.
    public override Transform3D _GetCameraTransform() => Eyes.Camera(XRServer.WorldScale);

    public override Transform3D _GetTransformForView(uint view, Transform3D camTransform) =>
        Eyes.View(view, camTransform, XRServer.GetReferenceFrame(), XRServer.WorldScale);

    public override double[] _GetProjectionForView(uint view, double aspect, double zNear, double zFar) =>
        Eyes.Projection(aspect, zNear, zFar);

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

    // One hand as OpenXR builds it: a controller tracker under its standard name, carrying a profile.
    private static XRControllerTracker Hand(XrHand hand)
    {
        var tracker = new XRControllerTracker
        {
            Type = XRServer.TrackerType.Controller,
            Name = hand == XrHand.Left ? "left_hand" : "right_hand",
            Description = "xr-sim hand",
            Hand = hand == XrHand.Left ? XRPositionalTracker.TrackerHand.Left : XRPositionalTracker.TrackerHand.Right,
            Profile = HandProfile,
        };
        XRServer.AddTracker(tracker);
        return tracker;
    }

    private XRControllerTracker? HandTracker(XrHand hand) => hand == XrHand.Left ? _left : _right;

    // The head tracker's pose, which XRCamera3D takes with the recenter frame applied.
    private void PublishHead() =>
        _head?.SetPose("default", Eyes.Head, Vector3.Zero, Vector3.Zero, XRPose.TrackingConfidenceEnum.High);

    private sealed record Pinned(XrSimEyes Eyes);
}
