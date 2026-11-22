using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
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
/// grid. A lacquer copy of each material is this stage's alone, never a flight material. Its
/// <see cref="HangarPreviewCamera"/> turns it, swings to an edit and follows a drag.
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

    // TUNE: the lacquer. Smoother than the flown skin's 0.85, with a clear coat's 0.04 reflectance.
    private const string LacquerRoughness = "0.4";
    private const string LacquerF0 = "0.04";
    private const string LacquerSpecular = "0.5";

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

    private static readonly Regex Specular = new(@"SPECULAR = [0-9.]+;", RegexOptions.Compiled);
    private static readonly Regex LightF0 = new(@"ROUGHNESS,\s*[0-9.]+, SPECULAR_AMOUNT", RegexOptions.Compiled);

    private readonly GameZ _planes;
    private readonly TextureArchive _textures;
    private readonly PatternLibrary _patterns;
    private readonly SceneBuilder? _rocketScene;
    private readonly GameZNode? _rocket;
    private readonly Node3D _stand = new() { Name = "stand" };
    private readonly Camera3D _camera = new() { Name = "camera", Fov = FieldOfView };
    private readonly DirectionalLight3D _key;
    private readonly Dictionary<ShaderMaterial, ShaderMaterial?> _lacquer = new();
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

    /// <summary>How many surfaces wear the stage's lacquer copy.</summary>
    public int Lacquered { get; private set; }

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
        if (before == null || before.Airframe != pose.Airframe || _builder == null)
        {
            Build(pose);
            return;
        }

        if (!before.SamePaint(pose))
        {
            _builder.Repaint(pose.Paint());
            foreach (var (source, copy) in _lacquer)
            {
                copy?.SetShaderParameter("albedo_tex", source.GetShaderParameter("albedo_tex"));
            }

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

    // The material a lacquered surface wears, the source's text and parameters with a smoother,
    // glossier finish and no world sun share or fog. Null for a text without the airframe arm's
    // roughness line, which keeps its own material.
    internal static string? LacquerCode(string code)
    {
        const string matte = "    ROUGHNESS = 0.85;";
        if (!code.Contains(matte, StringComparison.Ordinal))
        {
            return null;
        }

        code = code.Replace(matte, "    ROUGHNESS = " + LacquerRoughness + ";", StringComparison.Ordinal)
            .Replace("LIGHT_IS_DIRECTIONAL ? v_plane_sun : 1.0", "1.0", StringComparison.Ordinal)
            .Replace("csky_fog_on * fog_amt", "0.0", StringComparison.Ordinal);
        code = Specular.Replace(code, "SPECULAR = " + LacquerSpecular + ";");
        return LightF0.Replace(code, "ROUGHNESS, " + LacquerF0 + ", SPECULAR_AMOUNT");
    }

    private static float Deadzoned(float axis) => MathF.Abs(axis) < StickDeadzone ? 0f : axis;

    private static ShaderMaterial CopyOnto(ShaderMaterial source, Shader shader)
    {
        var copy = new ShaderMaterial { Shader = shader };
        foreach (var uniform in source.Shader.GetShaderUniformList())
        {
            var name = uniform.AsGodotDictionary()["name"].AsStringName();
            copy.SetShaderParameter(name, source.GetShaderParameter(name));
        }

        return copy;
    }

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
        _lacquer.Clear();
        Lacquered = 0;
        if (_model != null)
        {
            _stand.RemoveChild(_model);
            _model.QueueFree();
            _model = null;
        }

        long start = Stopwatch.GetTimestamp();
        string node = StockAirframes.Node(pose.Airframe);
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
        Lacquer(_model);
        Center(_model);
        Hang(pose);
        Frame();
        Log.Info("ui",
            $"hangar preview: built {node} in {Stopwatch.GetElapsedTime(start).TotalMilliseconds:0} ms, radius {_radius:0.0} m, {Lacquered} lacquered surface(s)");
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
            Lacquer(body);
            _rockets.Add(body);
        }
    }

    // Each shaded surface onto its lacquer copy, made once per source material.
    private void Lacquer(Node root)
    {
        foreach (var mesh in Meshes(root))
        {
            for (int surface = 0; surface < mesh.GetSurfaceOverrideMaterialCount(); surface++)
            {
                if (mesh.GetActiveMaterial(surface) is not ShaderMaterial { Shader: { } shader } source)
                {
                    continue;
                }

                if (!_lacquer.TryGetValue(source, out var copy))
                {
                    copy = LacquerCode(shader.Code) is { } code ? CopyOnto(source, ShaderTwins.Pooled(code, "hangar-lacquer")) : null;
                    _lacquer[source] = copy;
                }

                if (copy != null)
                {
                    mesh.SetSurfaceOverrideMaterial(surface, copy);
                    Lacquered++;
                }
            }
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
