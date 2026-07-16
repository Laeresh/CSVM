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
/// F12 (any mode) saves the current frame to a timestamped PNG under the repo's
/// git-ignored Screenshots/ folder.
///
/// User args (after "--" on the command line):
///   --plane=player_bhawk         which aircraft root node to build
///   --chapter[=C1]               build a chapter's world (its single "world1") instead of one
///                                plane; takes C1, C1B, C1C, C2, C2B, C3, C4, C5. Drives the
///                                default gamez + textures to ../extracted/<chapter>/…
///   --fly                        free flight: chapter world + original skydome + aircraft +
///                                arcade controls (WASD/arrows pitch+roll, Q/E rudder,
///                                Shift/Ctrl throttle, R respawn; gamepad: left stick,
///                                LB/RB rudder, RT/LT throttle, Y respawn).
///                                Combine with --chapter= to fly a different chapter (default C1)
///   --mission=IA1                which mission's spawns --fly uses (default IA1 = instant action,
///                                ia.json spawn_points); story missions (M0x) fall back to
///                                objectives.json PLAYER_INIT. Pair with --chapter= to match the world
///   --scenario=zeppelin_run      which instant-action scenario's spawn list to spawn from
///                                (zeppelin_run, dogfight_ace, dogfight_squadron, stunt_flying, …)
///   --spawn=N                    force spawn index N in that list (default: random pick, like the
///                                original — relaunch to sample the others; the pick is logged)
///   --sky-zone=zone2             which horizon zone to render in --fly: zone2 = night
///                                (moon/stars, what the original shows at the C1 airfield),
///                                zone1 = day haze (likely test-only, unfinished gray cap).
///                                Also selects which zone's distance fog (weather.json) applies.
///                                If given in static --chapter mode, the skydome + fog + cloud-
///                                band whiteout render there too (put the camera inside the map
///                                via --campos — deterministic fog/whiteout verification shots)
///   --gamez=path                 GameZ zip/dir (default: ../extracted/planes.zip;
///                                in --chapter/--fly modes: the chapter's gamez, default C1/gamez.zip)
///   --textures=path              texture zip (default: ../extracted/<chapter>/texture.zip, chapter=C1)
///   --zrdr=path                  zrdr extraction zip/dir with plane stats (default: ../extracted/zrdr.zip)
///   --sounds=path                sound extraction zip/dir (default: ../extracted/soundsh.zip)
///   --mute                       skip flight audio (engine loop, overspeed whine, rattle, crash)
///   --debug-collision            draw the plane's collision probe (the swept ray of the
///                                crash test; green, red on impact)
///   --hold=pitch,roll,yaw,thr    constant flight input instead of the keyboard (automated runs)
///   --frames=N                   frames to render before --screenshot fires (default 15)
///   --campos=x,y,z               place the camera here instead of auto-framing
///   --lookat=x,y,z               orbit/look target (default: model AABB center)
///   --screenshot=path            render a few frames, save a PNG, then quit
/// </summary>
public partial class PlaneViewer : Node3D
{
    private const float HorizonScale = 2.5f;

    // Cloud-band whiteout color: inside a cloud reads near-white (see OriginalScreenshots/
    // "C1 IA1 whiteout at height.png"), not the 0.69 gray of distance fog. TUNE. The opacity
    // (0 at the band edges → 1 at the opaque core) comes from WeatherState.WhiteoutAmount.
    private static readonly Color WhiteoutColor = new(0.95f, 0.95f, 0.96f);

    private string _planeName = "player_bhawk";
    private string _skyZone = "zone2"; // the sky the original shows at the C1 airfield (night)
    private bool _skyZoneExplicit;     // --sky-zone given: render the horizon even in static --chapter mode
    private string _chapter = "C1";    // which chapter's world to build (--chapter=): C1, C1B, C1C, C2, C2B, C3, C4, C5
    private bool _worldMode;           // render the chapter world instead of a single plane
    private bool _fly;
    private string _mission = "IA1";   // which mission's spawns to fly from (--mission=): IA1, M01, …
    private string _scenario = "zeppelin_run"; // which instant-action scenario's spawn list (--scenario=)
    private int _spawnIndex = -1;      // --spawn=N forces a spawn; <0 = random pick (like the original)
    private FlightInput? _holdInput;
    private Vector3? _camPos, _lookAt;
    private string? _screenshotPath;
    private int _screenshotFrames = 15;

    private Node3D? _plane;
    private Node3D? _horizon;
    private Node3D? _deck;             // the cloudlayer deck, moved to follow the player
    private Vector3 _deckCenter;       // the deck geometry's original AABB centre (to re-anchor it)
    private WeatherState? _weather;    // per-mission fog + cloud band (--fly only)
    private ColorRect? _whiteout;      // full-screen cloud-band whiteout overlay
    private Camera3D _camera = null!;
    private Vector3 _orbitCenter;
    private float _orbitDistance = 20f;
    private float _yaw = 2.5f, _pitch = 0.3f; // default: front-left three-quarter view (nose is -Z)
    private bool _dragging;

    public override void _Ready()
    {
       
        var projectDir = ProjectSettings.GlobalizePath("res://");
        var repoRoot = Path.GetFullPath(Path.Combine(projectDir, ".."));
        var gamezPath = Path.Combine(repoRoot, "extracted", "planes.zip");
        var planesGamezPath = gamezPath;
        var texturesPath = Path.Combine(repoRoot, "extracted", "C1", "texture.zip");
        var zrdrPath = Path.Combine(repoRoot, "extracted", "zrdr.zip");
        var soundsPath = Path.Combine(repoRoot, "extracted", "soundsh.zip");
        bool mute = false;
        bool debugCollision = false;

        bool gamezOverridden = false, texturesOverridden = false, zrdrOverridden = false, soundsOverridden = false;
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--plane=")) _planeName = arg["--plane=".Length..];
            else if (arg == "--chapter") _worldMode = true;
            else if (arg.StartsWith("--chapter=")) { _chapter = arg["--chapter=".Length..]; _worldMode = true; }
            else if (arg == "--fly") _fly = true;
            else if (arg.StartsWith("--mission=")) _mission = arg["--mission=".Length..];
            else if (arg.StartsWith("--scenario=")) _scenario = arg["--scenario=".Length..];
            else if (arg.StartsWith("--spawn=")) _spawnIndex = int.Parse(arg["--spawn=".Length..]);
            else if (arg.StartsWith("--sky-zone=")) { _skyZone = arg["--sky-zone=".Length..]; _skyZoneExplicit = true; }
            else if (arg.StartsWith("--gamez=")) { gamezPath = arg["--gamez=".Length..]; gamezOverridden = true; }
            else if (arg.StartsWith("--textures=")) { texturesPath = arg["--textures=".Length..]; texturesOverridden = true; }
            else if (arg.StartsWith("--zrdr=")) { zrdrPath = arg["--zrdr=".Length..]; zrdrOverridden = true; }
            else if (arg.StartsWith("--sounds=")) { soundsPath = arg["--sounds=".Length..]; soundsOverridden = true; }
            else if (arg == "--mute") mute = true;
            else if (arg == "--debug-collision") debugCollision = true;
            else if (arg.StartsWith("--hold=")) _holdInput = ParseHold(arg["--hold=".Length..]);
            else if (arg.StartsWith("--frames=")) _screenshotFrames = int.Parse(arg["--frames=".Length..]);
            else if (arg.StartsWith("--screenshot=")) _screenshotPath = arg["--screenshot=".Length..];
            else if (arg.StartsWith("--yaw=")) _yaw = float.Parse(arg["--yaw=".Length..], System.Globalization.CultureInfo.InvariantCulture);
            else if (arg.StartsWith("--pitch=")) _pitch = float.Parse(arg["--pitch=".Length..], System.Globalization.CultureInfo.InvariantCulture);
            else if (arg.StartsWith("--campos=")) _camPos = ParseVec3(arg["--campos=".Length..]);
            else if (arg.StartsWith("--lookat=")) _lookAt = ParseVec3(arg["--lookat=".Length..]);
        }

        if (_fly)
            _worldMode = true;
        // The chapter drives both the world's gamez and its texture archive. (The static
        // plane viewer keeps textures at C1: C1's texture.zbd also carries every player-plane
        // skin, so it is the right default even when not building a world.)
        if (!texturesOverridden)
            texturesPath = Path.Combine(repoRoot, "extracted", _chapter, "texture.zip");
        if (_worldMode && !gamezOverridden)
            gamezPath = Path.Combine(repoRoot, "extracted", _chapter, "gamez.zip");
        // Instant-action spawns come from the mission's own zrdr (ia.json), a different
        // archive than --zrdr (which holds the shared vehicle/player/engine/sound defs).
        var missionZrdrPath = Path.Combine(repoRoot, "extracted", _chapter, _mission, "zrdr.zip");

        // Prefer the unpacked sibling folder from ExtractAssets.ps1 -Unzip when it exists
        // (loose JSON/PNG/WAV: no zip decompression at load, and greppable in the editor);
        // fall back to the .zip. Skip paths the user set explicitly via --gamez=/etc.
        static string PreferUnzipped(string zipPath)
        {
            var dir = Path.Combine(Path.GetDirectoryName(zipPath)!, Path.GetFileNameWithoutExtension(zipPath));
            return Directory.Exists(dir) ? dir : zipPath;
        }
        planesGamezPath = PreferUnzipped(planesGamezPath);
        if (!gamezOverridden) gamezPath = PreferUnzipped(gamezPath);
        if (!texturesOverridden) texturesPath = PreferUnzipped(texturesPath);
        if (!zrdrOverridden) zrdrPath = PreferUnzipped(zrdrPath);
        if (!soundsOverridden) soundsPath = PreferUnzipped(soundsPath);
        missionZrdrPath = PreferUnzipped(missionZrdrPath);

        // Register the distance-fog global shader parameters SceneBuilder's world/aircraft
        // shader references, before any material using it is built. Default range is a no-op
        // (nothing fades) — only --fly overrides it from the mission's weather.json below.
        RenderingServer.GlobalShaderParameterAdd("csky_fog_color",
            RenderingServer.GlobalShaderParameterType.Vec3, new Vector3(0.69f, 0.69f, 0.69f));
        RenderingServer.GlobalShaderParameterAdd("csky_fog_range",
            RenderingServer.GlobalShaderParameterType.Vec2, new Vector2(1e8f, 1e9f));

        SetupLighting();
        _camera = new Camera3D { Fov = _fly ? 62 : 50, Far = 40000f };
        AddChild(_camera);

        try
        {
            var sw = Stopwatch.StartNew();
            var gamez = GameZ.Load(gamezPath);
            using var textures = new TextureArchive(texturesPath);
            int meshInstances;
            int colliders = 0;
            string what;
            if (_worldMode)
            {
                var builder = new WorldBuilder(gamez, textures, collision: _fly);
                _plane = builder.Build("world1"); // every chapter has exactly one world node
                _deck = builder.CloudDeck;         // the cloudlayer overcast, moved to follow the player
                if (_fly || _skyZoneExplicit)
                {
                    // The original skydome, anchored to the camera each frame. Scaled up so
                    // plain depth testing keeps it behind everything: a camera-centered dome
                    // looks identical at any scale (zero parallax), and at 2.5× (~22 km
                    // radius) it is beyond the farthest terrain (~17.4 km corner-to-corner)
                    // while well inside the camera's 40 km far plane. In static --chapter mode
                    // only an explicit --sky-zone adds it (an outside orbit view is better
                    // without the enclosing dome; with --campos inside the map it works).
                    _horizon = builder.BuildHorizon(_skyZone);
                    if (_horizon != null)
                    {
                        _horizon.Scale = Vector3.One * HorizonScale;
                        AddChild(_horizon);
                    }
                    // Weather (the flown mission's weather.json): distance fog for the rendered
                    // zone + the cloud-band whiteout. Applied whenever the world+dome are shown —
                    // in --fly, and in static --chapter when --sky-zone is given (deterministic
                    // fog/whiteout verification with --campos, same as the sky-verification path).
                    SetupWeather(missionZrdrPath);
                }
                meshInstances = builder.MeshInstanceCount;
                colliders = builder.ColliderCount;
                what = $"chapter {_chapter} world";
            }
            else
            {
                var builder = new PlaneBuilder(gamez, textures);
                _plane = builder.Build(_planeName);
                meshInstances = builder.MeshInstanceCount;
                what = $"'{_planeName}'";
            }
            AddChild(_plane);
            // The deck is now in the tree at its original position; remember its centre so
            // _Process can re-anchor it under the player each frame (see UpdateCloudDeck).
            if (_deck != null)
                _deckCenter = ComputeAabb(_deck).GetCenter();

            if (_fly)
            {
                var planesGamez = GameZ.Load(planesGamezPath);
                var planeBuilder = new PlaneBuilder(planesGamez, textures, spinningProps: true);
                var planeModel = planeBuilder.Build(_planeName);
                meshInstances += planeBuilder.MeshInstanceCount;

                var stats = PlaneStats.Load(zrdrPath, _planeName);
                GD.Print($"flight stats [{stats.DefName}]: fd_speed={stats.FdSpeed} m/s " +
                         $"weight={stats.VehWeight} engine={stats.EnginePower:0.00} " +
                         $"torques=({stats.PitchTorque},{stats.RollTorque},{stats.RudderTorque})");

                var controller = new FlightController
                {
                    HoldInput = _holdInput,
                    DebugCollision = debugCollision,
                    PlaneModel = planeModel,
                    Props = PropAnimator.Build(planeModel), // spin the propeller/rotor blur discs
                    WingLights = WingLightBlinker.Build(planeBuilder.WingFlares), // blink the wingtip flares
                };
                controller.AddChild(planeModel);
                if (controller.Props != null)
                    GD.Print($"props: {controller.Props.Count} spinning blur nodes");
                if (controller.WingLights != null)
                    GD.Print($"wing lights: {controller.WingLights.Count} blinking flares");

                // Crash fireball: the game's large_fireball (flame_ball.json → fierypuffer),
                // its flipbook frames from the same texture archive. Built here while the
                // archive is open; the FlightController fires it at the impact point.
                var pufferState = Effects.PufferState.Load(zrdrPath, "flame_ball.json", "fierypuffer");
                if (pufferState != null && Effects.Puffer.Create(pufferState, textures) is { } fireball)
                {
                    controller.CrashEffect = fireball;
                    controller.AddChild(fireball);
                    GD.Print($"crash effect: {pufferState.Name} ({pufferState.Number} sprites, " +
                             $"{pufferState.TextureSequence.Count} frames)");
                }
                else
                {
                    GD.PushWarning("crash fireball not loaded (flame_ball.json / fire_f textures missing)");
                }

                if (!mute && (File.Exists(soundsPath) || Directory.Exists(soundsPath)))
                {
                    // streams decode fully into memory, so the archive can close right after
                    using var sounds = new SoundArchive(soundsPath);
                    var audio = new FlightAudio();
                    audio.Setup(sounds, SoundDefs.Load(zrdrPath), stats);
                    controller.Audio = audio;
                    controller.AddChild(audio);
                    GD.Print($"audio: engine={stats.EngineSound} whine={stats.WhineSound} rattle={stats.RattleSound}");
                }
                else if (!mute)
                {
                    GD.PushWarning($"sound archive not found, flying silent: {soundsPath}");
                }
                var (spawnPos, spawnLookAt) = ChooseSpawn(missionZrdrPath);
                controller.Setup(new FlightModel(stats), _camera, spawnPos, spawnLookAt);
                AddChild(controller);
                what += $" + '{_planeName}' flying";
            }

            GD.Print($"loaded {what}: {gamez.Nodes.Count} gamez nodes, " +
                     $"{meshInstances} mesh instances, {colliders} colliders, {sw.ElapsedMilliseconds} ms");
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

    /// <summary>Loads the flown mission's weather.json and applies it: sets the distance-fog
    /// global shader parameters for the rendered sky zone (all world + aircraft surfaces pick
    /// them up), and builds the full-screen cloud-band whiteout overlay (its opacity is driven
    /// each frame from the camera altitude in <see cref="_Process"/>). No-op if the mission has
    /// no weather.json — the fog globals keep their registered no-op range.</summary>
    private void SetupWeather(string missionZrdrPath)
    {
        _weather = WeatherState.Load(missionZrdrPath);
        if (_weather == null)
        {
            GD.PushWarning($"no weather.json for {_chapter}/{_mission} — flying without fog / whiteout");
            return;
        }
        var fog = _weather.Fog(_skyZone);
        RenderingServer.GlobalShaderParameterSet("csky_fog_color",
            new Vector3(fog.FogColor.R, fog.FogColor.G, fog.FogColor.B));
        //Range is halved because this does not seem to be radius but diameter. See Screenshot C1 IA1 Fog Range.png vs Screenshots\Fog Range.png
        float fogRangeFactor = 2.0f;
        RenderingServer.GlobalShaderParameterSet("csky_fog_range", new Vector2(fog.FogNear, fog.FogFar)/fogRangeFactor);
        GD.Print($"weather [{_skyZone}]: fog {fog.FogColor.R:0.00} gray {fog.FogNear:0}–{fog.FogFar:0} m; " +
                 $"cloud band {_weather.CloudBottom:0}–{_weather.CloudTop:0} m (±{_weather.CloudThickness:0})");

        if (_weather.HasCloudBand)
        {
            // A full-screen overlay so the whiteout swallows everything (terrain, plane, clouds)
            // uniformly, like the original. Layer 0 keeps it behind the flight HUD (layer 1).
            var canvas = new CanvasLayer { Layer = 0 };
            _whiteout = new ColorRect
            {
                Color = new Color(WhiteoutColor, 0f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _whiteout.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            canvas.AddChild(_whiteout);
            AddChild(canvas);
        }
    }

    /// <summary>Picks the flight spawn for the current mission: a world position + a look-at
    /// point one unit ahead along the spawn heading. Instant-action missions (IA1) draw from
    /// ia.json's scenario spawn list — random per launch like the original, or forced by
    /// --spawn=N. Story missions (M0x, no ia.json) fall back to objectives.json PLAYER_INIT.
    /// A fixed C1 spawn is the last resort if neither is present.</summary>
    private (Vector3 pos, Vector3 lookAt) ChooseSpawn(string missionZrdrPath)
    {
        var spawns = SpawnPoints.LoadIa(missionZrdrPath, _scenario);
        if (spawns != null)
        {
            int i = _spawnIndex >= 0
                ? Mathf.Clamp(_spawnIndex, 0, spawns.Count - 1)
                : (int)(GD.Randi() % (uint)spawns.Count);
            return LogSpawn($"{_scenario} #{i} of {spawns.Count}", spawns[i]);
        }
        // No instant-action spawns (only IA1 folders have ia.json) — use the story-mission
        // spawn from objectives.json PLAYER_INIT (position + heading).
        if (SpawnPoints.LoadPlayerInit(missionZrdrPath) is { } init)
            return LogSpawn("PLAYER_INIT", init);

        GD.PushWarning($"no ia.json / PLAYER_INIT spawn for {_chapter}/{_mission} — using fallback spawn");
        return (new Vector3(-6200, 500, -3300), new Vector3(-5700, 350, -6300));
    }

    /// <summary>Turns a spawn (position + heading) into a (position, look-at) pair — the nose
    /// (-Z) rotated by the heading (yaw about up) — and logs it for cross-checking the data.</summary>
    private (Vector3 pos, Vector3 lookAt) LogSpawn(string label, SpawnPoint s)
    {
        var forward = new Basis(Vector3.Up, Mathf.DegToRad(s.HeadingDeg)) * Vector3.Forward;
        GD.Print($"spawn [{_chapter}/{_mission} {label}]: " +
                 $"pos=({s.Position.X:0},{s.Position.Y:0},{s.Position.Z:0}) heading={s.HeadingDeg:0}°");
        return (s.Position, s.Position + forward);
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
        // F12 anywhere (orbit view or free flight): grab the current frame to a file.
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F12 })
        {
            SaveScreenshot();
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
        // Keep the skydome centered on the camera in ALL axes (a pure zero-parallax
        // backdrop, like the original): the moon then stays at its designed 28° elevation
        // against the dark dome cap — whose color its painted background matches — instead
        // of sliding down into the bright horizon band as the plane climbs.
        // (One-frame lag vs the flight camera is invisible at 22 km.)
        if (_horizon != null)
            _horizon.Position = _camera.Position;

        // Cloud-band whiteout: fade the overlay in as the camera altitude enters the band.
        if (_whiteout != null && _weather != null)
        {
            var c = _whiteout.Color;
            c.A = _weather.WhiteoutAmount(_camera.Position.Y);
            _whiteout.Color = c;
        }

        // Cloud deck follows the player: centered on the camera x/z and pinned to a fixed
        // altitude at the whiteout-band centre. You climb toward it as a fixed ceiling (floor
        // once above) and pass through it exactly where the whiteout is fully opaque, so the
        // ceiling→floor transition is hidden.
        if (_deck != null && _weather is { HasCloudBand: true })
        {
            float mid = (_weather.CloudTop + _weather.CloudBottom) * 0.5f;
            _deck.Position = new Vector3(
                _camera.Position.X - _deckCenter.X,
                mid - _deckCenter.Y,
                _camera.Position.Z - _deckCenter.Z);
        }

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

    /// <summary>Save the current frame to a timestamped PNG under the repo's Screenshots/
    /// folder (git-ignored — rendered frames are game-derived). Bound to F12 in both the
    /// orbit viewer and free flight; the full viewport is captured, HUD overlay included.</summary>
    private void SaveScreenshot()
    {
        var projectDir = ProjectSettings.GlobalizePath("res://");
        var dir = Path.GetFullPath(Path.Combine(projectDir, "..", "Screenshots"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"crimsonskies_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.png");
        var img = GetViewport().GetTexture().GetImage();
        var err = img.SavePng(path);
        if (err == Error.Ok)
            GD.Print($"screenshot saved: {path}");
        else
            GD.PrintErr($"screenshot failed ({err}): {path}");
    }
}
