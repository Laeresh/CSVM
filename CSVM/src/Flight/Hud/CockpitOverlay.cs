using System.Collections.Generic;
using CSVM.Flight.Camera;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Hud;

/// <summary>
/// Draws the cockpit interior in a world of its own, camera and interior both at the origin, over
/// the main view. The projection is the main world's: only the camera-relative half of
/// <see cref="CameraController.FirstPersonPose"/> survives, since the plane position and the
/// <c>cockpit_camera</c> offset cancel between eye and panel. What is left is small, so float32
/// rounds it far below a pixel, which the interior's chapter-scale world transform does not.
/// A flat body cockpit's pass takes its transplanted panel and twins of the body instead (<see cref="BuildBody"/>).
/// The flat game's path; <c>--no-cockpit-pass</c> leaves the interior in the main world instead,
/// as the stereo path always does (<see cref="InteriorInPass"/>).
/// One per player, built on that player's own HUD parent (<see cref="PlayerRig.HudParent"/>).
/// </summary>
public sealed partial class CockpitOverlay : CanvasLayer
{
    /// <summary>The panel material parameter carrying the world position of the pass's origin.</summary>
    public const string LightOriginParam = "light_origin";

    private readonly SubViewport _view;
    private readonly Camera3D _camera;
    private readonly Node3D _interior;
    private readonly DirectionalLight3D? _light;
    private readonly DirectionalLight3D? _sun;
    private readonly List<OmniLight3D> _flashes = new();

    // The moved node's pose under the shake pivot, in the aircraft's frame, and the eye there, which
    // the pass's origin stands on. The stock interior's mount keeps only its basis: its origin is the eye.
    private readonly Transform3D _mount;
    private readonly Vector3 _eye;

    // A body cockpit's model root, which its twins' originals hang under, and the panel's own pose on it.
    private readonly Node3D? _owner;
    private readonly Transform3D _panelLocal;

    // Each body mesh with its twin under the panel, which takes its pose and visibility in Sync.
    private readonly List<Mech3.BodyTwin> _mirrored = new();

    // The panel's own shader materials, whose point-light term needs the world position of this
    // pass's origin (SceneBuilder's `light_origin`).
    private readonly List<ShaderMaterial> _materials = new();

    // The shadow-quality revision the light last copied the sun at (FollowSunShadow).
    private int _shadowRevision = Utils.ShadowQualitySetting.Revision;

    private CockpitOverlay(SubViewport view, Camera3D camera, Node3D interior,
        DirectionalLight3D? light, DirectionalLight3D? sun, Node3D? owner, Vector3 eye)
    {
        _view = view;
        _camera = camera;
        _interior = interior;
        _light = light;
        _sun = sun;
        _owner = owner;
        _panelLocal = interior.Transform;
        _mount = owner == null
            ? new Transform3D(interior.Transform.Basis, Vector3.Zero)
            : owner.Transform * interior.Transform;
        _eye = owner == null ? Vector3.Zero : eye;
        CollectMaterials(interior, _materials);
    }

    /// <summary>The camera looking at the interior from that world's origin. The overlay's own,
    /// never the pilot's: the pilot's camera stays in the main world drawing everything else.</summary>
    public Camera3D Camera => _camera;

    /// <summary>The interior as this pass holds it: re-parented out of the plane model, into the
    /// overlay world, at its offset from the eye. That is zero for <c>cockpit1</c> and small for a body cockpit's panel.</summary>
    public Node3D Interior => _interior;

    /// <summary>This pass's own cloned sun, so <c>WeatherRig.RegisterExtraLighting</c>
    /// can keep its LEVEL in step with future zone changes; its bearing is <see cref="Sync"/>'s,
    /// re-taken from the world sun every frame. Null when the caller passed no sun (a suite rig
    /// with no lighting).</summary>
    public DirectionalLight3D? Sun => _light;

    /// <summary>This pass's own cloned Environment, for the same registration. Null when the
    /// caller passed no Environment.</summary>
    public Godot.Environment? Env => _view.World3D.Environment;

    /// <summary>Whether a session draws its interiors through this pass: asked for
    /// (<paramref name="asked"/>, which <c>--no-cockpit-pass</c> clears) and not on the stereo path.
    /// A stereo viewport draws no canvas layer, so there the interior stays in the main world
    /// (<see cref="XrCockpit"/>). Engine-free, so it unit-tests.</summary>
    public static bool InteriorInPass(bool asked, bool stereo) => asked && !stereo;

    /// <summary>Moves <paramref name="interior"/> into a private world under
    /// <paramref name="parent"/> and returns the pass drawing it, or null when the node has no
    /// parent to be taken from. <paramref name="sun"/> and <paramref name="env"/> are the main
    /// world's, copied so the panel is lit as it was; the pass keeps the sun to re-aim its copy
    /// per frame, since the interior's world frame turns with the aircraft.</summary>
    public static CockpitOverlay? Build(Node parent, Node3D interior, DirectionalLight3D? sun,
        Godot.Environment? env) => Build(parent, interior, sun, env, body: false, Vector3.Zero);

    /// <summary>A flat body cockpit's pass: <paramref name="panel"/> (<see cref="Mech3.PlaneBuilder.CockpitPanel"/>)
    /// moved into the private world, with the eye at <paramref name="eye"/> on its model root. Each of
    /// <paramref name="twins"/> (<see cref="Mech3.PlaneBuilder.FlatBody"/>) takes its original's pose and visibility on
    /// every <see cref="Sync"/>. Its original joins <paramref name="visibility"/>'s reseated list, so it leaves the
    /// pilot's pane while the panel draws.</summary>
    public static CockpitOverlay? BuildBody(Node parent, Node3D panel, Vector3 eye,
        IEnumerable<Mech3.BodyTwin> twins, CockpitVisibility? visibility,
        DirectionalLight3D? sun, Godot.Environment? env)
    {
        if (Build(parent, panel, sun, env, body: true, eye) is not { } overlay)
        {
            return null;
        }

        foreach (var pair in twins)
        {
            overlay._mirrored.Add(pair);
            visibility?.Reseat(pair.Original);
        }

        return overlay;
    }

    /// <summary>The interior's basis for one frame: the wobble pivot's rotation over the mount
    /// basis, exactly as the pivot applies it over the model. The rotation is YXZ Euler radians,
    /// as <see cref="Camera.PlaneShake.Rotation"/> gives it. Public so the suite can assert the
    /// wobble reaches the panel without a rig.</summary>
    public static Basis WobbledMount(Basis mount, Vector3 shakeRotation) =>
        Basis.FromEuler(shakeRotation) * mount;

    /// <summary>A main-world point in the pass's frame: the pass keeps the world's orientation and
    /// puts the eye at its origin, so a light at <paramref name="world"/> lands at its offset from
    /// the eye. Public for the suite.</summary>
    public static Vector3 ToOverlay(Vector3 world, Vector3 eye) => world - eye;

    /// <summary>Point the overlay camera where the pilot's head points and re-light the panel for
    /// this frame's attitude. The pass shows or hides with the interior's own visibility, so
    /// <see cref="CockpitVisibility"/> keeps deciding which views draw a cockpit. Called every
    /// frame the rig owns its camera. <paramref name="attitude"/> is the DRAWN plane basis.
    /// <paramref name="shakeRotation"/> is the wobble pivot's rotation, which the interior takes
    /// here through <see cref="WobbledMount"/>.</summary>
    public void Sync(Basis attitude, CameraController camera, Vector3 shakeRotation,
        IEnumerable<(Vector3 Position, float Range, Color Color, float Energy)>? flashes = null)
    {
        MirrorFlashes(flashes, camera.EyePosition);
        if (!Shown(GodotObject.IsInstanceValid(_interior) && _interior.Visible
            && (_owner == null || GodotObject.IsInstanceValid(_owner))))
        {
            return;
        }
        // The attitude stays and only the translation goes: a rotation at the origin rounds far
        // below a pixel, and keeping it means the sun, the sky radiance and the flashes all sit
        // where the main world has them, with nothing re-aimed.
        var attitudeOnly = attitude.Orthonormalized();
        // A body turns about the shake pivot, as its originals do. The stock interior's zero origin turns about the eye.
        _interior.Transform = new Transform3D(attitudeOnly * WobbledMount(_mount.Basis, shakeRotation),
            attitudeOnly * ((Basis.FromEuler(shakeRotation) * _mount.Origin) - _eye));
        if (_owner != null)
        {
            // Each twin takes its original's pose on the model root and its visibility.
            Mech3.BodyTwins.Mirror(_mirrored, _owner, _panelLocal);
        }

        var (_, basis) = CameraController.FirstPersonPose(Vector3.Zero, attitudeOnly, Vector3.Zero,
            camera.Head.Elevation, camera.Head.Azimuth);
        _camera.Transform = new Transform3D(basis, Vector3.Zero);
        _camera.Fov = CameraController.FirstPersonFovDeg(camera.ViewMode);
        // The world's point lights sit at world positions; the panel sits at the eye's offset.
        foreach (var material in _materials)
            material.SetShaderParameter(LightOriginParam, camera.EyePosition);
        if (_light != null && _sun != null && GodotObject.IsInstanceValid(_sun))
        {
            _light.Basis = _sun.GlobalBasis;
            FollowSunShadow();
        }
    }

    /// <summary>Re-copy the world sun's shadow onto this pass's light. It does so only where the
    /// shadow quality has moved the sun since the last copy. The per-frame <see cref="Sync"/>
    /// calls it, and it is public so the suite can drive it without a rig.</summary>
    public void FollowSunShadow()
    {
        if (_light == null || _sun == null || !GodotObject.IsInstanceValid(_sun)
            || _shadowRevision == Utils.ShadowQualitySetting.Revision)
        {
            return;
        }

        _shadowRevision = Utils.ShadowQualitySetting.Revision;
        SunShadow.Copy(_sun, _light, _camera.Far);
    }

    /// <summary>Re-take the session sun's colour, specular and shadows after a live graphics-mode
    /// switch re-dressed it, clamped to this pass's far plane as at build. The zone's energies
    /// arrive after this through <c>WeatherRig.RegisterExtraLighting</c>.</summary>
    public void FollowSun()
    {
        if (_light != null && _sun != null && GodotObject.IsInstanceValid(_sun))
        {
            _light.LightColor = _sun.LightColor;
            _light.LightSpecular = _sun.LightSpecular;
            _shadowRevision = Utils.ShadowQualitySetting.Revision;
            SunShadow.Copy(_sun, _light, _camera.Far);
        }
    }

    /// <summary>Take the pass off the screen for a caller that has stopped syncing it. The crash
    /// cut is that caller: it leaves first person while the rig's per-frame camera work is already
    /// halted, so nothing would reach <see cref="Sync"/> to notice the interior went away.</summary>
    public void Deactivate() => Shown(false);

    // A body cockpit keeps its model root, which its twins' poses are read under.
    private static CockpitOverlay? Build(Node parent, Node3D interior, DirectionalLight3D? sun,
        Godot.Environment? env, bool body, Vector3 eye)
    {
        if (interior.GetParent() is not { } owner || (body && owner is not Node3D))
        {
            return null;
        }
        var overlay = NewOverlay(interior, sun, env, body ? (Node3D)owner : null, eye);
        owner.RemoveChild(interior);
        overlay.AddViewportChildren(interior);
        parent.AddChild(overlay);
        return overlay;
    }

    // The whole node graph, assembled before anything is added to a tree so a failed build frees
    // cleanly. The viewport owns its World3D outright: sharing the main one would put the interior
    // back at chapter-scale coordinates, which is the defect this pass exists to avoid.
    private static CockpitOverlay NewOverlay(Node3D interior, DirectionalLight3D? sun,
        Godot.Environment? env, Node3D? owner, Vector3 eye)
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
        // Asked like every 3D viewport, and answered with nothing: the pass is transparent, which
        // the temporal and upscaling resolves do not keep (see ViewportQuality.Apply).
        Utils.ViewportQuality.Apply(view);
        if (env != null)
        {
            // The sky stays: a transparent viewport never paints its background, and clearing it
            // leaves the pass's reflected light with no radiance map. Duplicated so the pass owns
            // its copy, whose ambient RegisterExtraLighting then keeps on the flown zone's value.
            view.World3D.Environment = (Godot.Environment)env.Duplicate();
        }
        // Every layer, since a body cockpit's panel wears the pilot's flare band, past Godot's default 20.
        var camera = new Camera3D { Name = "interior_camera", Near = 0.01f, Far = 100f, Current = true, CullMask = uint.MaxValue };
        DirectionalLight3D? light = null;
        if (sun != null)
        {
            light = new DirectionalLight3D
            {
                Name = "interior_sun",
                LightEnergy = sun.LightEnergy,
                LightColor = sun.LightColor,
                // The zone's specular, which the enhanced zone apply writes on the sun before this
                // pass is registered to take it. The faithful sun keeps Godot's default.
                LightSpecular = sun.LightSpecular,
            };
            // Copied off the live sun, which by this point in the build already carries the flown
            // zone's settings (WeatherRig.Build runs ahead of BuildCockpitPasses). Clamped to this
            // pass's far plane, since a zone's fog far would push every split past the pass.
            SunShadow.Copy(sun, light, camera.Far);
        }
        return new CockpitOverlay(view, camera, interior, light, sun, owner, eye)
        {
            Name = "cockpit_pass",
            Layer = UI.Boards.HudLayers.CockpitPass,
        };
    }

    // Every distinct shader material the subtree draws with, override or mesh surface.
    private static void CollectMaterials(Node node, List<ShaderMaterial> into)
    {
        if (node is MeshInstance3D mi && mi.Mesh is { } mesh)
        {
            if (mi.MaterialOverride is ShaderMaterial over && !into.Contains(over))
                into.Add(over);
            for (int s = 0; s < mesh.GetSurfaceCount(); s++)
            {
                if ((mi.GetSurfaceOverrideMaterial(s) ?? mesh.SurfaceGetMaterial(s)) is ShaderMaterial m
                    && !into.Contains(m))
                {
                    into.Add(m);
                }
            }
        }
        foreach (var child in node.GetChildren())
            CollectMaterials(child, into);
    }

    // Whether the pass draws this frame, as the two writes that decide it: the layer's own
    // visibility and the viewport's update mode, which together also stop it re-rendering.
    private bool Shown(bool shown)
    {
        Visible = shown;
        _view.RenderTargetUpdateMode = shown
            ? SubViewport.UpdateMode.Always
            : SubViewport.UpdateMode.Disabled;
        return shown;
    }

    private void AddViewportChildren(Node3D interior)
    {
        var container = new SubViewportContainer
        {
            Name = "interior_pane",
            Stretch = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            // A transparent 3D viewport writes premultiplied colour; composited as straight alpha
            // the gunsight glass takes its alpha twice and reads darker than in the main world.
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.PremultAlpha },
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
        interior.Transform = new Transform3D(_mount.Basis, _mount.Origin - _eye);
        _view.AddChild(interior);
    }

    // The omni muzzle flashes live in the main world, which this world cannot see, so each lit one
    // gets a twin here at the same eye-relative place; twins past the lit count go dark. Only the
    // enhanced interior is shaded and takes them; the original-mode interior takes the point term.
    private void MirrorFlashes(
        IEnumerable<(Vector3 Position, float Range, Color Color, float Energy)>? flashes, Vector3 eye)
    {
        int used = 0;
        if (flashes != null)
        {
            foreach (var f in flashes)
            {
                if (used >= _flashes.Count)
                {
                    var twin = new OmniLight3D
                    {
                        Name = $"interior_flash_{used}",
                        ShadowEnabled = false,
                        OmniAttenuation = Mech3.WorldLights.OmniAttenuation,
                    };
                    _view.AddChild(twin);
                    _flashes.Add(twin);
                }
                var light = _flashes[used++];
                light.Position = ToOverlay(f.Position, eye);
                light.OmniRange = f.Range;
                light.LightColor = f.Color;
                light.LightEnergy = f.Energy;
                light.Visible = true;
            }
        }
        for (int i = used; i < _flashes.Count; i++)
            _flashes[i].Visible = false;
    }
}
