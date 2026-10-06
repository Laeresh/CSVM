using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Bindings;

/// <summary>The two hand controllers as raw state: whether a hand is there, one action map action's
/// reading on it, and its aim pose. The seam <see cref="XrControllers"/> reads through, so the binding
/// model over it unit-tests without a runtime.</summary>
public interface IXrHands
{
    /// <summary>Whether that hand's controller is bound to an interaction profile right now.
    /// </summary>
    bool Present(XrHand hand);

    /// <summary>One action's reading on that hand: a boolean as 0 or 1, a float as itself, one
    /// component of a vector (<paramref name="component"/> 0 or 1). Zero for an action it lacks.
    /// </summary>
    float Read(XrHand hand, string action, int component);

    /// <summary>That hand's aim pose in the XR origin's frame, or null while it is not tracked.
    /// The ray it points runs along its -Z.</summary>
    Transform3D? Aim(XrHand hand);
}

/// <summary>
/// The headset's hand controllers behind the binding seam: an <see cref="IDeviceState"/> answering
/// for <see cref="DeviceId.Xr"/> through the control table <see cref="XrControls"/>, and the aim
/// pose the screen pointer casts from. Every other identity reads nothing.
/// ⚠ A hand that is absent reads zero, and so does every hand while <see cref="SessionLive"/> is
/// false. A flat launch therefore makes no XR call at all. A controller switched off mid-session
/// cannot leave its last button held, though OpenXR stops updating an unbound tracker's inputs.
/// </summary>
public sealed class XrControllers : IDeviceState
{
    private readonly IXrHands _hands;

    /// <summary>A reader over <paramref name="hands"/>.</summary>
    public XrControllers(IXrHands hands) => _hands = hands ?? throw new ArgumentNullException(nameof(hands));

    /// <summary>The live reader over Godot's XR trackers, the one every seat's reader passes XR
    /// identities to (<see cref="SeatDeviceState"/>).</summary>
    public static XrControllers Live { get; } = new(new GodotXrHands());

    /// <summary>Whether a stereo session (OpenXR or the stand-in) is live in this process. Set once by
    /// the session's boot. Until then <see cref="Live"/> reads nothing, and a rebinding screen hides
    /// XR controls it has nothing to capture them from.</summary>
    public static bool SessionLive { get; set; }

    /// <summary>Whether that hand's controller is there.</summary>
    public bool Present(XrHand hand) => _hands.Present(hand);

    /// <summary>That hand's aim pose in the XR origin's frame, or null while it is absent or untracked.
    /// </summary>
    public Transform3D? Aim(XrHand hand) => _hands.Present(hand) ? _hands.Aim(hand) : null;

    public bool IsKeyDown(DeviceId device, int keyCode) => false;

    public bool IsMouseButtonDown(DeviceId device, int button) => false;

    public bool IsButtonDown(DeviceId device, int button) =>
        device.Kind == DeviceKind.Xr && XrControls.Button(button) is { } input
        && _hands.Present(input.Hand) && _hands.Read(input.Hand, input.Action, -1) >= 0.5f;

    public float AxisValue(DeviceId device, int axis)
    {
        if (device.Kind != DeviceKind.Xr || XrControls.Axis(axis) is not { } input || !_hands.Present(input.Hand))
        {
            return 0f;
        }

        float value = _hands.Read(input.Hand, input.Action, input.Component);
        return float.IsFinite(value) ? Math.Clamp(value, -1f, 1f) : 0f;
    }

    public HatDirection HatState(DeviceId device, int hat) => HatDirection.None;

    // The trackers OpenXR names left_hand and right_hand, read through XRServer. The StringNames are
    // made on the first live read, so a unit test touching the static reader never reaches the engine.
    private sealed class GodotXrHands : IXrHands
    {
        // OpenXR's own placeholder for a hand no interaction profile binds.
        private const string NoProfile = "/interaction_profiles/none";

        private readonly Dictionary<string, StringName> _names = new();

        public bool Present(XrHand hand) =>
            SessionLive && Tracker(hand) is { } tracker
            && tracker.Profile is { Length: > 0 } profile && profile != NoProfile;

        public float Read(XrHand hand, string action, int component)
        {
            if (!SessionLive || Tracker(hand) is not XRControllerTracker tracker)
            {
                return 0f;
            }

            // The store OpenXR writes every action into. Godot 4.7 flags it for a move onto
            // XRControllerTracker that its C# glue does not carry yet.
#pragma warning disable CS0618
            var value = tracker.GetInput(Name(action));
#pragma warning restore CS0618
            return value.VariantType switch
            {
                Variant.Type.Bool => value.AsBool() ? 1f : 0f,
                Variant.Type.Float => (float)value.AsDouble(),
                Variant.Type.Vector2 when component is 0 or 1 => value.AsVector2()[component],
                _ => 0f,
            };
        }

        public Transform3D? Aim(XrHand hand)
        {
            if (!SessionLive || Tracker(hand) is not { } tracker || tracker.GetPose(Name("aim")) is not { } pose
                || !pose.HasTrackingData)
            {
                return null;
            }

            return pose.GetAdjustedTransform();
        }

        private XRPositionalTracker? Tracker(XrHand hand) =>
            XRServer.GetTracker(Name(hand == XrHand.Left ? "left_hand" : "right_hand")) as XRPositionalTracker;

        private StringName Name(string text)
        {
            if (!_names.TryGetValue(text, out var name))
            {
                name = new StringName(text);
                _names[text] = name;
            }

            return name;
        }
    }
}
