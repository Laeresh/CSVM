using System.Threading;
using CSVM.Mech3;
using Godot;

namespace CSVM.Launch;

/// <summary>
/// Hands the stereo cockpit interior (<c>Flight/Hud/XrCockpit.cs</c>) the eyes its frame is drawn
/// with. Godot locates the eye poses on the render thread as a frame begins, in OpenXR's
/// <c>pre_render</c>. Every initialised interface's pre-render runs there, in the order added. This
/// tracking-only interface is added after the headset's, so it reads the poses just located. It
/// writes each eye's offset from the XR origin, and the origin itself, into global uniforms, which
/// Godot uploads before it culls that same frame. The eyes' distance apart sizes the stereo cutscene
/// pair (<see cref="InterocularM"/>); the interface is never primary and draws nothing.
/// </summary>
public partial class XrEyeFeed : XRInterfaceExtension
{
    /// <summary>The name the interface registers under with <see cref="XRServer"/>.</summary>
    public const string InterfaceName = "CSVM xr eye feed";

    private static readonly StringName Left = SceneBuilder.XrEyeLeftGlobal;
    private static readonly StringName Right = SceneBuilder.XrEyeRightGlobal;
    private static readonly StringName Origin = SceneBuilder.XrOriginGlobal;

    // Written on the render thread, read on the main one.
    private static float _interocularM = UI.Boards.StereoPair.DefaultInterocularM;

    private bool _initialized;

    /// <summary>The distance between the eyes the runtime last located, in world metres, so it
    /// follows the headset's own IPD setting. A typical adult's until the first frame.</summary>
    public static float InterocularM => Volatile.Read(ref _interocularM);

    public override StringName _GetName() => InterfaceName;

    public override uint _GetCapabilities() => 0;

    public override bool _IsInitialized() => _initialized;

    public override bool _Initialize()
    {
        _initialized = true;
        return true;
    }

    public override void _Uninitialize() => _initialized = false;

    // ⚠ Runs on the render thread under the separate thread model. Read only what XRServer keeps
    // for that thread (the origin and the recenter frame) and the headset's per-view poses.
    public override void _PreRender()
    {
        if (XRServer.PrimaryInterface is not { } primary || primary == this)
        {
            return;
        }

        // An identity camera leaves each eye in the tracking space, recentred and world-scaled.
        var origin = XRServer.WorldOrigin;
        var left = primary.GetTransformForView(0, Transform3D.Identity);
        var right = primary.GetTransformForView(1, Transform3D.Identity);
        var leftEye = origin.Basis * left.Origin;
        var rightEye = origin.Basis * right.Origin;
        RenderingServer.GlobalShaderParameterSet(Left, leftEye);
        RenderingServer.GlobalShaderParameterSet(Right, rightEye);
        RenderingServer.GlobalShaderParameterSet(Origin, origin.Origin);
        Volatile.Write(ref _interocularM, leftEye.DistanceTo(rightEye));
    }
}
