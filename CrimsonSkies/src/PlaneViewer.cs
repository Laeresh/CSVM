using System;
using System.Diagnostics;
using System.IO;
using CrimsonSkies.Flight;
using CrimsonSkies.Mech3;
using Godot;

namespace CrimsonSkies;

/// <summary>
/// Milestone 2 vertical-slice viewer: loads one aircraft from the player's own
/// extracted game data and renders it with orbit controls — or, with --fly,
/// free flight over the chapter world with arcade controls.
///
/// User args (after "--" on the command line):
///   --plane=player_bhawk         which aircraft root node to build
///   --world[=world1]             build a whole chapter world instead of one plane
///                                (default gamez becomes ../extracted/c1-gamez.zip)
///   --fly                        free flight: chapter world + aircraft + arcade controls
///                                (WASD/arrows pitch+roll, Q/E rudder, Shift/Ctrl throttle, R respawn)
///   --gamez=path                 GameZ zip/dir (default: ../extracted/planes-gamez.zip;
///                                in --world/--fly modes: the world's gamez, default c1-gamez.zip)
///   --textures=path              texture zip (default: ../extracted/c1-texture.zip)
///   --zrdr=path                  zrdr extraction zip/dir with plane stats (default: ../extracted/zrdr.zip)
///   --hold=pitch,roll,yaw,thr    constant flight input instead of the keyboard (automated runs)
///   --frames=N                   frames to render before --screenshot fires (default 15)
///   --campos=x,y,z               place the camera here instead of auto-framing
///   --lookat=x,y,z               orbit/look target (default: model AABB center)
///   --screenshot=path            render a few frames, save a PNG, then quit
/// </summary>
public partial class PlaneViewer : Node3D
{
    private string _planeName = "player_bhawk";
    private string? _worldName;
    private bool _fly;
    private FlightInput? _holdInput;
    private Vector3? _camPos, _lookAt;
    private string? _screenshotPath;
    private int _screenshotFrames = 15;

    private Node3D? _plane;
    private Camera3D _camera = null!;
    private Vector3 _orbitCenter;
    private float _orbitDistance = 20f;
    private float _yaw = 2.5f, _pitch = 0.3f; // default: front-left three-quarter view (nose is -Z)
    private bool _dragging;

    public override void _Ready()
    {
        var projectDir = ProjectSettings.GlobalizePath("res://");
        var repoRoot = Path.GetFullPath(Path.Combine(projectDir, ".."));
        var gamezPath = Path.Combine(repoRoot, "extracted", "planes-gamez.zip");
        var planesGamezPath = gamezPath;
        var texturesPath = Path.Combine(repoRoot, "extracted", "c1-texture.zip");
        var zrdrPath = Path.Combine(repoRoot, "extracted", "zrdr.zip");

        bool gamezOverridden = false;
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--plane=")) _planeName = arg["--plane=".Length..];
            else if (arg == "--world") _worldName = "world1";
            else if (arg.StartsWith("--world=")) _worldName = arg["--world=".Length..];
            else if (arg == "--fly") _fly = true;
            else if (arg.StartsWith("--gamez=")) { gamezPath = arg["--gamez=".Length..]; gamezOverridden = true; }
            else if (arg.StartsWith("--textures=")) texturesPath = arg["--textures=".Length..];
            else if (arg.StartsWith("--zrdr=")) zrdrPath = arg["--zrdr=".Length..];
            else if (arg.StartsWith("--hold=")) _holdInput = ParseHold(arg["--hold=".Length..]);
            else if (arg.StartsWith("--frames=")) _screenshotFrames = int.Parse(arg["--frames=".Length..]);
            else if (arg.StartsWith("--screenshot=")) _screenshotPath = arg["--screenshot=".Length..];
            else if (arg.StartsWith("--yaw=")) _yaw = float.Parse(arg["--yaw=".Length..], System.Globalization.CultureInfo.InvariantCulture);
            else if (arg.StartsWith("--pitch=")) _pitch = float.Parse(arg["--pitch=".Length..], System.Globalization.CultureInfo.InvariantCulture);
            else if (arg.StartsWith("--campos=")) _camPos = ParseVec3(arg["--campos=".Length..]);
            else if (arg.StartsWith("--lookat=")) _lookAt = ParseVec3(arg["--lookat=".Length..]);
        }

        if (_fly)
            _worldName ??= "world1";
        if (_worldName != null && !gamezOverridden)
            gamezPath = Path.Combine(repoRoot, "extracted", "c1-gamez.zip");

        SetupLighting();
        _camera = new Camera3D { Fov = _fly ? 62 : 50, Far = 40000f };
        AddChild(_camera);

        try
        {
            var sw = Stopwatch.StartNew();
            var gamez = GameZ.Load(gamezPath);
            using var textures = new TextureArchive(texturesPath);
            int meshInstances;
            string what;
            if (_worldName != null)
            {
                var builder = new WorldBuilder(gamez, textures);
                _plane = builder.Build(_worldName);
                meshInstances = builder.MeshInstanceCount;
                what = $"world '{_worldName}'";
            }
            else
            {
                var builder = new PlaneBuilder(gamez, textures);
                _plane = builder.Build(_planeName);
                meshInstances = builder.MeshInstanceCount;
                what = $"'{_planeName}'";
            }
            AddChild(_plane);

            if (_fly)
            {
                var planesGamez = GameZ.Load(planesGamezPath);
                var planeBuilder = new PlaneBuilder(planesGamez, textures);
                var planeModel = planeBuilder.Build(_planeName);
                meshInstances += planeBuilder.MeshInstanceCount;

                var stats = PlaneStats.Load(zrdrPath, _planeName);
                GD.Print($"flight stats [{stats.DefName}]: fd_speed={stats.FdSpeed} m/s " +
                         $"weight={stats.VehWeight} engine={stats.EnginePower:0.00} " +
                         $"torques=({stats.PitchTorque},{stats.RollTorque},{stats.RudderTorque})");

                var controller = new FlightController { HoldInput = _holdInput };
                controller.AddChild(planeModel);
                // spawn between the lighthouse and the town, heading for the town
                controller.Setup(new FlightModel(stats), _camera,
                    spawnPos: new Vector3(-6200, 500, -3300),
                    spawnLookAt: new Vector3(-5700, 350, -6300));
                AddChild(controller);
                what += $" + '{_planeName}' flying";
            }

            GD.Print($"loaded {what}: {gamez.Nodes.Count} gamez nodes, " +
                     $"{meshInstances} mesh instances, {sw.ElapsedMilliseconds} ms");
        }
        catch (Exception e)
        {
            GD.PrintErr($"failed to load plane: {e}");
            if (_screenshotPath != null)
                GetTree().Quit(1);
            return;
        }

        if (!_fly)
            FrameCamera();
    }

    private static FlightInput ParseHold(string s)
    {
        var p = s.Split(',');
        float F(int i) => float.Parse(p[i], System.Globalization.CultureInfo.InvariantCulture);
        return new FlightInput { Pitch = F(0), Roll = F(1), Yaw = F(2), Throttle = F(3) };
    }

    private void SetupLighting()
    {
        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-45, 150, 0), // shine onto the -Z (nose) side
            LightEnergy = 1.6f,
            ShadowEnabled = true,
        };
        AddChild(sun);

        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = new ProceduralSkyMaterial() },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.9f,
        };
        AddChild(new WorldEnvironment { Environment = env });
    }

    private void FrameCamera()
    {
        var aabb = ComputeAabb(_plane!);
        _orbitCenter = _lookAt ?? aabb.GetCenter();
        if (_camPos is { } pos)
        {
            var offset = pos - _orbitCenter;
            _orbitDistance = offset.Length();
            if (_orbitDistance < 0.01f) { _orbitDistance = 1f; offset = Vector3.Back; }
            var dir = offset / _orbitDistance;
            _pitch = Mathf.Asin(Mathf.Clamp(dir.Y, -1f, 1f));
            _yaw = Mathf.Atan2(dir.X, dir.Z);
        }
        else
        {
            var radius = aabb.Size.Length() * 0.5f;
            if (radius < 0.01f) radius = 5f;
            _orbitDistance = radius / Mathf.Sin(Mathf.DegToRad(_camera.Fov) * 0.5f) * 0.8f;
        }
        UpdateCamera();
    }

    private static Vector3 ParseVec3(string s)
    {
        var parts = s.Split(',');
        return new Vector3(
            float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
    }

    private static Aabb ComputeAabb(Node3D root)
    {
        Aabb merged = default;
        bool first = true;
        void Walk(Node node)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                var box = mi.GlobalTransform * mi.Mesh.GetAabb();
                merged = first ? box : merged.Merge(box);
                first = false;
            }
            foreach (var child in node.GetChildren())
                Walk(child);
        }
        Walk(root);
        return merged;
    }

    private void UpdateCamera()
    {
        _pitch = Mathf.Clamp(_pitch, -1.5f, 1.5f);
        var dir = new Vector3(
            Mathf.Sin(_yaw) * Mathf.Cos(_pitch),
            Mathf.Sin(_pitch),
            Mathf.Cos(_yaw) * Mathf.Cos(_pitch));
        _camera.Position = _orbitCenter + dir * _orbitDistance;
        _camera.LookAt(_orbitCenter, Vector3.Up);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            GetTree().Quit();
            return;
        }
        if (_fly)
            return; // the FlightController owns the camera; no orbit controls
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                _dragging = mb.Pressed;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }:
                _orbitDistance *= 0.9f;
                UpdateCamera();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }:
                _orbitDistance *= 1.1f;
                UpdateCamera();
                break;
            case InputEventMouseMotion motion when _dragging:
                _yaw -= motion.Relative.X * 0.008f;
                _pitch += motion.Relative.Y * 0.008f;
                UpdateCamera();
                break;
        }
    }

    public override void _Process(double delta)
    {
        if (_screenshotPath == null || _plane == null)
            return;
        if (--_screenshotFrames > 0)
            return;
        var img = GetViewport().GetTexture().GetImage();
        img.SavePng(_screenshotPath);
        GD.Print($"screenshot saved: {_screenshotPath}");
        _screenshotPath = null;
        GetTree().Quit();
    }
}
