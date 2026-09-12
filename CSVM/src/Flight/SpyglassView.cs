using Godot;

namespace CSVM.Flight;

/// <summary>The spyglass picture: a square viewport rendering the SHARED world through a camera of
/// its own, which <see cref="TargetHud"/> then draws as a disc at the off-screen marker's anchor.
/// The world is inherited rather than owned, so the target is the one in play and wears the flown
/// zone's fog; the pane's own camera lends its clip planes and cull mask, since the picture is the
/// same view from the same aeroplane. One per pane, built with the HUD, rendering only while
/// <see cref="Aim"/> is being called. Where it looks and how wide is <see cref="Spyglass"/>'s.
/// </summary>
public sealed partial class SpyglassView : SubViewport
{
    private Camera3D _camera = null!;
    private Camera3D? _pane;
    private bool _live;

    /// <summary>Whether the picture is rendering this frame.</summary>
    public bool Live => _live;

    /// <summary>The picture at <paramref name="pane"/>'s side: idle until the first
    /// <see cref="Aim"/>, and hung on the HUD control that draws it, which is what keeps it inside
    /// that pane's viewport and therefore in that pane's world.</summary>
    public static SpyglassView Build(Camera3D? pane)
    {
        int side = Mathf.RoundToInt(Spyglass.RefWindow);
        var view = new SpyglassView
        {
            Name = "spyglass_view",
            Size = new Vector2I(side, side),
            RenderTargetUpdateMode = UpdateMode.Disabled,
            HandleInputLocally = false,
            AudioListenerEnable3D = false,
            Msaa3D = (Msaa)(int)ProjectSettings.GetSetting(
                "rendering/anti_aliasing/quality/msaa_3d", 0).AsInt32(),
        };
        view._pane = pane;
        view._camera = new Camera3D { Name = "spyglass_camera", Current = true };
        view.AddChild(view._camera);
        return view;
    }

    /// <summary>Point the picture and start it rendering. <paramref name="side"/> is the disc's
    /// drawn diameter in device pixels, so the texture is rasterised at the size it is shown at.
    /// </summary>
    public void Aim(Transform3D pose, float fovDeg, int side)
    {
        if (side > 0 && Size.X != side)
        {
            Size = new Vector2I(side, side);
        }

        _camera.Transform = pose;
        _camera.Fov = fovDeg;
        if (_pane != null && GodotObject.IsInstanceValid(_pane))
        {
            // The zone apply writes the pane camera's clip pair and the pane owns the cull mask a
            // splitscreen seat draws through; the original hands camera 2 both alongside camera 1.
            _camera.Near = _pane.Near;
            _camera.Far = _pane.Far;
            _camera.CullMask = _pane.CullMask;
        }

        RenderTargetUpdateMode = UpdateMode.Always;
        _live = true;
    }

    /// <summary>Stop rendering. The gates are re-answered every frame, so this is called on every
    /// frame the disc is down and must stay cheap when it is already idle.</summary>
    public void Idle()
    {
        if (!_live)
        {
            return;
        }

        RenderTargetUpdateMode = UpdateMode.Disabled;
        _live = false;
    }
}
