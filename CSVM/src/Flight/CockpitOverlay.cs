using Godot;

namespace CSVM.Flight;

/// <summary>
/// Draws the cockpit interior in a world of its own, with the camera and the interior both at the
/// origin, and composites the result over the main view. The projection is the same one the main
/// world uses: only the camera-relative half of <see cref="CameraController.FirstPersonPose"/>
/// survives the move, because the plane position and the <c>cockpit_camera</c> offset cancel
/// between the eye and the panel. What is left is small, so float32 rounds it far below a pixel,
/// which the interior's world transform at chapter-scale coordinates does not.
/// Behind <c>--cockpit-pass</c>: default off, so the shipped picture is the main world's.
/// One per player, built on that player's own HUD parent (<see cref="PlayerRig.HudParent"/>).
/// </summary>
public sealed partial class CockpitOverlay : CanvasLayer
{
    private readonly SubViewport _view;
    private readonly Camera3D _camera;
    private readonly Node3D _interior;
    private readonly DirectionalLight3D? _light;
    private readonly DirectionalLight3D? _sun;

    private CockpitOverlay(SubViewport view, Camera3D camera, Node3D interior,
        DirectionalLight3D? light, DirectionalLight3D? sun)
    {
        _view = view;
        _camera = camera;
        _interior = interior;
        _light = light;
        _sun = sun;
    }

    /// <summary>The camera looking at the interior from that world's origin. The overlay's own,
    /// never the pilot's: the pilot's camera stays in the main world drawing everything else.</summary>
    public Camera3D Camera => _camera;

    /// <summary>The interior as this pass holds it: re-parented out of the plane model, into the
    /// overlay world, at zero translation.</summary>
    public Node3D Interior => _interior;

    /// <summary>Moves <paramref name="interior"/> into a private world under
    /// <paramref name="parent"/> and returns the pass drawing it, or null when the node has no
    /// parent to be taken from. <paramref name="sun"/> and <paramref name="env"/> are the main
    /// world's, copied so the panel is lit as it was; the pass keeps the sun to re-aim its copy
    /// per frame, since the interior's world frame turns with the aircraft.</summary>
    public static CockpitOverlay? Build(Node parent, Node3D interior, DirectionalLight3D? sun,
        Godot.Environment? env)
    {
        if (interior.GetParent() is not { } owner)
        {
            return null;
        }
        var overlay = NewOverlay(interior, sun, env);
        owner.RemoveChild(interior);
        overlay.AddViewportChildren(interior);
        parent.AddChild(overlay);
        return overlay;
    }

    /// <summary>Point the overlay camera where the pilot's head points and re-light the panel for
    /// this frame's attitude, then show or hide the pass to match the interior's own visibility so
    /// <see cref="CockpitVisibility"/> keeps deciding which views draw a cockpit. Called every
    /// frame the rig owns its camera; <paramref name="attitude"/> is the DRAWN plane basis.</summary>
    public void Sync(Basis attitude, CameraController camera)
    {
        bool shown = GodotObject.IsInstanceValid(_interior) && _interior.Visible;
        Visible = shown;
        _view.RenderTargetUpdateMode = shown
            ? SubViewport.UpdateMode.Always
            : SubViewport.UpdateMode.Disabled;
        if (!shown)
        {
            return;
        }
        var (_, basis) = CameraController.FirstPersonPose(Vector3.Zero, Basis.Identity, Vector3.Zero,
            camera.Head.Elevation, camera.Head.Azimuth);
        _camera.Transform = new Transform3D(basis, Vector3.Zero);
        var size = _view.GetVisibleRect().Size;
        _camera.Fov = CameraController.FirstPersonFovDeg(camera.ViewMode,
            size.Y > 0f ? size.X / size.Y : 16f / 9f);
        if (_light != null && _sun != null && GodotObject.IsInstanceValid(_sun))
        {
            // The interior's frame is the aircraft's in the main world and identity here, so the
            // shading direction has to lose the attitude the panel no longer carries.
            _light.Basis = attitude.Orthonormalized().Transposed() * _sun.GlobalBasis;
        }
    }

    // The whole node graph, assembled before anything is added to a tree so a failed build frees
    // cleanly. The viewport owns its World3D outright: sharing the main one would put the interior
    // back at chapter-scale coordinates, which is the defect this pass exists to avoid.
    private static CockpitOverlay NewOverlay(Node3D interior, DirectionalLight3D? sun,
        Godot.Environment? env)
    {
        var view = new SubViewport
        {
            Name = "interior_view",
            World3D = new World3D(),
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            HandleInputLocally = false,
            AudioListenerEnable3D = false,
            Msaa3D = (Viewport.Msaa)(int)ProjectSettings.GetSetting(
                "rendering/anti_aliasing/quality/msaa_3d", 0).AsInt32(),
        };
        if (env != null)
        {
            // Duplicated rather than shared, because the background has to go: a sky drawn here
            // would paint over the main view. Ambient still reads the Sky resource it keeps.
            var copy = (Godot.Environment)env.Duplicate();
            copy.BackgroundMode = Godot.Environment.BGMode.ClearColor;
            view.World3D.Environment = copy;
        }
        var camera = new Camera3D { Name = "interior_camera", Near = 0.01f, Far = 100f, Current = true };
        DirectionalLight3D? light = null;
        if (sun != null)
        {
            light = new DirectionalLight3D
            {
                Name = "interior_sun",
                LightEnergy = sun.LightEnergy,
                LightColor = sun.LightColor,
                ShadowEnabled = false,
            };
        }
        return new CockpitOverlay(view, camera, interior, light, sun)
        {
            Name = "cockpit_pass",
            Layer = UI.HudLayers.CockpitPass,
        };
    }

    private void AddViewportChildren(Node3D interior)
    {
        var container = new SubViewportContainer
        {
            Name = "interior_pane",
            Stretch = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        container.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(container);
        container.AddChild(_view);
        _view.AddChild(_camera);
        if (_light != null)
        {
            _view.AddChild(_light);
        }
        // Everything but the translation is kept: the head-pitch tilt and the interior scale are
        // the mount PlaneBuilder built, and the pass changes only where the frame's origin is.
        interior.Transform = new Transform3D(interior.Transform.Basis, Vector3.Zero);
        _view.AddChild(interior);
    }
}
