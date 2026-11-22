using System;
using System.Collections.Generic;
using System.Diagnostics;
using CSVM.Extraction;
using CSVM.Flight.Hangar;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.UI.Hangar;

/// <summary>
/// The hangar's live plane picture under Enhanced: a transparent <see cref="SubViewport"/> with a
/// world of its own, holding the plane <see cref="PlaneBuilder"/> builds for a
/// <see cref="HangarPreviewPose"/>. The HE rockets hang on its opened pylons, built as flight builds
/// them, and the pose's paint goes on through <see cref="PlaneBuilder.Repaint"/>. Two white lights
/// ride the camera, so the plane reads as a lit toy turning in front of the board's own blueprint
/// grid. It wears the flight materials as built. Its <see cref="HangarPreviewCamera"/> turns it,
/// swings to an edit and follows a drag.
/// </summary>
public sealed partial class HangarPreviewStage : SubViewport
{
    // The chapter the skins and the rocket prototype are read from. Every chapter's archive
    // carries both (docs/formats/weapon-effects/ordnance.md), and C1 is the one a player meets first.
    private const string Chapter = "C1";
    private const string Mission = "M01";

    // TUNE: the camera's vertical field of view, degrees, and how far inside the bounding sphere's
    // own fit it stands. The sphere is loose round a plane, so a little under 1 fills the rect.
    private const float FieldOfView = 30f;
    private const float Snug = 0.8f;

    // TUNE: the toy-model lights, white so the swatches keep their hue.
    private const float KeyEnergy = 1.1f;
    private const float FillEnergy = 0.7f;
    private const float AmbientEnergy = 0.35f;
    private const float ShadowBlur = 2.5f;

    // The stick's full deflection as orbit speed, radians a second, and a drag's radians per
    // authored pixel. TUNE.
    private const float StickRate = 2.5f;
    private const float DragRate = 0.012f;
    private const float StickDeadzone = 0.2f;

    private readonly GameZ _planes;
    private readonly TextureArchive _textures;
    private readonly PatternLibrary _patterns;
    private readonly SceneBuilder? _rocketScene;
    private readonly GameZNode? _rocket;
    private readonly Node3D _stand = new() { Name = "stand" };
    private readonly Camera3D _camera = new() { Name = "camera", Fov = FieldOfView };
    private readonly DirectionalLight3D _key;
    private readonly List<Node3D> _rockets = new();
    private PlaneBuilder? _builder;
    private Node3D? _model;
    private float _radius = 5f;
    private float _portSide = -1f;

    private HangarPreviewStage(GameZ planes, TextureArchive textures, PatternLibrary patterns, GameZ? chapterGamez, string? rocketModel)
    {
        Name = "hangar_preview";
        _planes = planes;
        _textures = textures;
        _patterns = patterns;
        if (chapterGamez != null && rocketModel != null && chapterGamez.FindByName(rocketModel) is { } rocket)
        {
            _rocket = rocket;
            _rocketScene = ProjectilePool.PylonScene(chapterGamez, textures, Chapter);
        }

        OwnWorld3D = true;
        TransparentBg = true;
        Msaa3D = Msaa.Msaa4X;
        RenderTargetUpdateMode = UpdateMode.WhenVisible;
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = Colors.White,
                AmbientLightEnergy = AmbientEnergy,
            },
        });
        AddChild(_stand);
        AddChild(_camera);
        // Both lights ride the camera, so the plane is lit the same from wherever it is seen. The key
        // comes from above the camera's left, the fill from below its right.
        _key = new DirectionalLight3D
        {
            Name = "key",
            LightEnergy = KeyEnergy,
            ShadowEnabled = true,
            ShadowBlur = ShadowBlur,
            // ⚠ One map, no fade. The default four splits put a split edge and the fade band across
            // the plane. Its shadows then popped as parts crossed them on the turntable.
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
            DirectionalShadowFadeStart = 1f,
            RotationDegrees = new Vector3(-40f, -25f, 0f),
        };
        _camera.AddChild(_key);
        _camera.AddChild(new DirectionalLight3D
        {
            Name = "fill",
            LightEnergy = FillEnergy,
            RotationDegrees = new Vector3(10f, 35f, 0f),
        });
    }

    /// <summary>The orbit the camera stands on.</summary>
    public HangarPreviewCamera Orbit { get; } = new();

    /// <summary>The pose last shown, null before the first.</summary>
    public HangarPreviewPose? Shown { get; private set; }

    /// <summary>The built plane, null before the first pose or after a build failed.</summary>
    public Node3D? Model => _model;

    /// <summary>The rocket bodies hanging now, one per opened pylon.</summary>
    public IReadOnlyList<Node3D> Rockets => _rockets;

    /// <summary>How many times the paint went on without a rebuild.</summary>
    public int Repaints { get; private set; }

    /// <summary>The stage over <paramref name="data"/>'s planes and its first chapter's skins and
    /// rocket, or null without planes. The first open decodes that chapter's archives, which is what
    /// the logged time measures.</summary>
    public static HangarPreviewStage? Open(GameData data, string? dataRoot)
    {
        if (data.Planes is not { } planes)
        {
            Log.Warn("ui", $"hangar preview: the game data holds no planes, so the live picture stays empty");
            return null;
        }

        long start = Stopwatch.GetTimestamp();
        var chapter = data.LoadChapter(Chapter, Mission);
        var textures = new TextureArchive(chapter.Textures);
        double texturesMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        string? rocketModel = WeaponDefs.Load(data.Zrdr).Get(Loadout.StockOrdnance)?.Flyout?.Model;
        var stage = new HangarPreviewStage(planes, textures, PatternLibrary.Load(RofFiles.For(dataRoot)), chapter.Gamez, rocketModel);
        Log.Info("ui",
            $"hangar preview: opened over {Chapter} in {Stopwatch.GetElapsedTime(start).TotalMilliseconds:0} ms ({texturesMs:0} ms skins, rocket '{rocketModel}' {(stage._rocket != null ? "found" : "absent")})");
        return stage;
    }

    /// <summary>Shows <paramref name="pose"/>: a new airframe rebuilds the plane, a new hardpoint
    /// count rehangs the rockets, and a new paint repaints in place. An edit swings the camera to
    /// what it changed (<see cref="HangarPreviewPose.FocusFor"/>). Call it once the stage is in the tree.</summary>
    public void Show(HangarPreviewPose pose)
    {
        var before = Shown;
        if (pose == before)
        {
            return;
        }

        Shown = pose;
        if (before == null || before.Node != pose.Node || _builder == null)
        {
            Build(pose);
            return;
        }

        if (!before.SamePaint(pose))
        {
            _builder.Repaint(pose.Paint());
            Repaints++;
        }

        if (before.LeftHardpoints != pose.LeftHardpoints || before.RightHardpoints != pose.RightHardpoints)
        {
            Hang(pose);
        }

        Orbit.Focus(HangarPreviewPose.FocusFor(before, pose), _portSide);
    }

    /// <summary>One step: <paramref name="dragX"/> and <paramref name="dragY"/> are this frame's drag
    /// in authored pixels, and every pad's right stick orbits too. The camera then takes its pose.</summary>
    public void Step(float dt, float dragX, float dragY)
    {
        float stickX = 0f, stickY = 0f;
        foreach (int pad in Bindings.Pads.For(null))
        {
            stickX += Deadzoned(Input.GetJoyAxis(pad, JoyAxis.RightX));
            stickY += Deadzoned(Input.GetJoyAxis(pad, JoyAxis.RightY));
        }

        Orbit.Orbit((dragX * DragRate) + (stickX * StickRate * dt), (dragY * DragRate) + (stickY * StickRate * dt));
        Orbit.Advance(dt);
        Frame();
    }

    public override void _ExitTree() => _textures.Dispose();

    private static float Deadzoned(float axis) => MathF.Abs(axis) < StickDeadzone ? 0f : axis;

    private static IEnumerable<MeshInstance3D> Meshes(Node root)
    {
        if (root is MeshInstance3D mesh)
        {
            yield return mesh;
        }

        foreach (var child in root.GetChildren())
        {
            foreach (var inner in Meshes(child))
            {
                yield return inner;
            }
        }
    }

    private void Build(HangarPreviewPose pose)
    {
        _rockets.Clear();
        if (_model != null)
        {
            _stand.RemoveChild(_model);
            _model.QueueFree();
            _model = null;
        }

        long start = Stopwatch.GetTimestamp();
        string node = pose.Node;
        try
        {
            // No spinningProps: the static flight pose, still disc and neutral surfaces.
            _builder = new PlaneBuilder(_planes, _textures, scheme: pose.Paint(), patterns: _patterns);
            _model = _builder.Build(node);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.IO.InvalidDataException)
        {
            Log.Warn("ui", $"hangar preview: {node} does not build, the picture stays empty: {e.Message}");
            _builder = null;
            return;
        }

        _stand.AddChild(_model);
        Center(_model);
        Hang(pose);
        Frame();
        Log.Info("ui",
            $"hangar preview: built {node} in {Stopwatch.GetElapsedTime(start).TotalMilliseconds:0} ms, radius {_radius:0.0} m");
    }

    // Moves the model so its bounds' middle is the stand's origin. Then reads the radius the camera
    // fits and the side the left wing's first pylon lies on.
    private void Center(Node3D model)
    {
        Aabb? bounds = null;
        var toStand = _stand.GlobalTransform.AffineInverse();
        foreach (var mesh in Meshes(model))
        {
            if (mesh.IsVisibleInTree())
            {
                var box = toStand * mesh.GlobalTransform * mesh.GetAabb();
                bounds = bounds is { } b ? b.Merge(box) : box;
            }
        }

        var whole = bounds ?? new Aabb(Vector3.Zero, Vector3.One);
        model.Position -= whole.GetCenter();
        _radius = MathF.Max(0.5f * whole.Size.Length(), 0.1f);
        string port = $"pylon{Loadout.LeftWingPylons[0]}";
        if (Loadout.CollectMarkers(model).TryGetValue(port, out var pylon))
        {
            float x = (_stand.GlobalTransform.AffineInverse() * pylon.GlobalTransform).Origin.X;
            _portSide = x >= 0f ? 1f : -1f;
        }
    }

    // One HE body per opened pylon, hung at identity under its marker as PylonOrdnance hangs it.
    // Flight also poses it at its def's RESET_STATE. HE's reset hides only its root, which the mount
    // shows again (docs/org/ordnanceTypes.md), so the menu needs no anim program.
    private void Hang(HangarPreviewPose pose)
    {
        foreach (var body in _rockets)
        {
            body.GetParent()?.RemoveChild(body);
            body.QueueFree();
        }

        _rockets.Clear();
        if (_model == null || _rocketScene == null || _rocket == null)
        {
            return;
        }

        var markers = Loadout.CollectMarkers(_model);
        foreach (int pylon in CustomPlaneBuild.OpenPylons(pose.LeftHardpoints, pose.RightHardpoints))
        {
            if (!markers.TryGetValue($"pylon{pylon}", out var marker) || ProjectilePool.InstanceBody(_rocketScene, _rocket) is not { } body)
            {
                continue;
            }

            body.Name = $"ordnance{pylon}";
            marker.AddChild(body);
            body.Transform = Transform3D.Identity;
            body.Visible = true;
            _rockets.Add(body);
        }
    }

    // The camera on the orbit, at the distance that fits the plane's sphere in the narrower of the
    // rect's two angles. The key light's shadow reaches just past the plane.
    private void Frame()
    {
        float aspect = Size.Y > 0 ? Size.X / (float)Size.Y : 1f;
        float half = Mathf.DegToRad(FieldOfView / 2f);
        float narrow = MathF.Min(half, MathF.Atan(MathF.Tan(half) * aspect));
        float distance = _radius / MathF.Sin(narrow) * Snug;
        float yaw = Orbit.Yaw, pitch = Orbit.Pitch;
        var at = new Vector3(MathF.Sin(yaw) * MathF.Cos(pitch), MathF.Sin(pitch), -MathF.Cos(yaw) * MathF.Cos(pitch)) * distance;
        _camera.Transform = new Transform3D(Basis.LookingAt(-at, Vector3.Up), at);
        _camera.Near = MathF.Max(0.05f, distance - (_radius * 2f));
        _camera.Far = distance + (_radius * 2f);
        _key.DirectionalShadowMaxDistance = distance + (_radius * 2f);
    }
}
