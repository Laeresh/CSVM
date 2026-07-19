using System;
using System.Collections.Generic;
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
/// git-ignored Screenshots/ folder. F11 (any mode) prints the current camera pose as
/// ready-to-paste --campos=/--lookat= args for reproducing a view.
///
/// User args (after "--" on the command line):
///   --plane=player_bhawk         which aircraft root node to build
///   --damage[=part:frac,…]       (static --plane mode) damage lab: one HP slider per
///                                destroyable part drives the item-10c damage visuals on
///                                the parked plane — torn-skin panel flips, panel fires
///                                burning in place, the ≤10% nose smoke/fire pair.
///                                Optional presets (fraction 0–1, or a percent when >1):
///                                --damage=leftwing:0.25,nose:40. H hides the sliders;
///                                combine with --screenshot for deterministic damage shots
///   --chapter[=C1]               build a chapter's world (its single "world1") instead of one
///                                plane; takes C1, C1B, C1C, C2, C2B, C3, C4, C5. Drives the
///                                default gamez + textures to ../extracted/<chapter>/…
///   --fly                        free flight: chapter world + original skydome + aircraft +
///                                arcade controls (WASD/arrows pitch+roll, Q/E rudder,
///                                Shift/Ctrl throttle, R respawn; gamepad: left stick,
///                                LB/RB rudder, RT/LT throttle, Y respawn).
///                                Combine with --chapter= to fly a different chapter (default C1)
///   --stunt                      stunt-flying mode: --fly + the mission's danger zones (ia.json
///                                dzones) as fly-through objectives, spawning from the stunt_flying
///                                spawn list. Completion shows in the text HUD (marker HUD is item 2)
///   --debug-dzpaths              build the danger-zone route ribbons (the dzpaths subtree the
///                                world skips — AI/route data the original never renders); debug only
///   --mission=IA1                which mission's spawns --fly uses (default IA1 = instant action,
///                                ia.json spawn_points); story missions (M0x) fall back to
///                                objectives.json PLAYER_INIT. Pair with --chapter= to match the world
///   --scenario=zeppelin_run      which instant-action scenario's spawn list to spawn from
///                                (zeppelin_run, dogfight_ace, dogfight_squadron, stunt_flying, …)
///   --spawn=N                    force spawn index N in that list (default: random pick, like the
///                                original — relaunch to sample the others; the pick is logged)
///   --spawn-at=x,y,z             debug: override the spawn position (bypasses the mission spawn
///                                list) — e.g. start just short of a target for a deterministic run
///   --spawn-dir=x,y,z            debug: nose direction at --spawn-at (world space; default -Z)
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
///   --interp=path                interp.zbd extraction (default: ../extracted/interp.json) — the
///                                boot scripts naming each chapter's clutter templates (forest
///                                trees, river bushes) placed onto matching-textured terrain
///   --sounds=path                sound extraction zip/dir (default: ../extracted/soundsh.zip)
///   --messages=path              message string table (default: ../extracted/messages.json) —
///                                resolves targets.json MSG_* keys for the stunt marker text
///   --mute                       skip flight audio (engine loop, overspeed whine, rattle, crash)
///   --debug-collision            draw the plane's collision probe (the swept ray of the
///                                crash test; green, red on impact)
///   --hold=pitch,roll,yaw,thr    scripted flight input instead of the keyboard (automated runs);
///                                ';'-separated segments with '@seconds' durations sequence inputs
///                                (e.g. --hold=1,0,0,0.5@3;0,0,0,0 — pull 3 s, then release), the
///                                last segment holds forever, respawn restarts the sequence
///   --frames=N                   frames to render before --screenshot fires (default 15)
///   --shots=N                    capture N consecutive frames (default 1); for z-fighting
///                                debugging — flicker is only visible across frames. N>1 writes
///                                indexed files (foo.png -> foo_00.png, foo_01.png, …)
///   --jitter=deg                 per-frame camera dither for --shots bursts (default 0.15°
///                                when N>1, else 0). Micro-orbits the eye around the framed
///                                point so a still camera doesn't render bit-identical frames;
///                                0 disables. Only applies in static (non-fly) mode.
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
    private bool _damageLab;           // --damage: per-part HP sliders driving the 10c visuals (static --plane mode)
    private List<(string, float)>? _damagePreset; // --damage=part:frac,… preset fractions
    private string _skyZone = "zone2"; // the sky the original shows at the C1 airfield (night)
    private bool _skyZoneExplicit;     // --sky-zone given: render the horizon even in static --chapter mode
    private string _chapter = "C1";    // which chapter's world to build (--chapter=): C1, C1B, C1C, C2, C2B, C3, C4, C5
    private bool _worldMode;           // render the chapter world instead of a single plane
    private bool _fly;
    private string _mission = "IA1";   // which mission's spawns to fly from (--mission=): IA1, M01, …
    private string _scenario = "zeppelin_run"; // which instant-action scenario's spawn list (--scenario=)
    private bool _scenarioExplicit;    // --scenario= given (so --stunt doesn't override it)
    private bool _stunt;               // --stunt: stunt-flying mode (= --fly + stunt_flying spawns + StuntMission)
    private bool _debugDzPaths;        // --debug-dzpaths: build the dzpaths route ribbons (debug-only geometry)
    private int _spawnIndex = -1;      // --spawn=N forces a spawn; <0 = random pick (like the original)
    private Vector3? _spawnAt;         // --spawn-at=x,y,z: override the mission spawn position (debug/testing)
    private Vector3? _spawnDir;        // --spawn-dir=x,y,z: nose direction there (world space; default -Z)
    private (FlightInput, float)[]? _holdSegments;
    private Vector3? _camPos, _lookAt;
    private string? _screenshotPath;
    private int _screenshotFrames = 15;
    private int _screenshotShots = 1;  // --shots=N: consecutive frames to capture (z-fight debug)
    private int _shotIndex;            // 0-based index of the shot being written
    private float _jitterDeg = -1f;    // --jitter=<deg> burst camera dither; <0 = auto per --shots
    private Transform3D? _shotBaseXform;  // camera pose captured at the first burst frame
    private Vector3 _shotPivot;           // micro-orbit centre (keeps the subject framed)

    private Node3D? _plane;
    private Node3D? _horizon;
    private Mech3.MapEdgeExtender? _edgeExtender; // rolling mirrored-tile window past the map edge
    private Node3D? _deck;             // the cloudlayer deck, moved to follow the player
    private Vector3 _deckCenter;       // the deck geometry's original AABB centre (to re-anchor it)
    private WeatherState? _weather;    // per-mission fog + cloud band (--fly only)
    private ColorRect? _whiteout;      // full-screen cloud-band whiteout overlay
    private Effects.CloudPuffs? _puffs; // ambient drifting cloud sprites at altitude
    private Effects.Precipitation? _precip; // rain/snow field (self-animating; no _Process driving)
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
        var interpPath = Path.Combine(repoRoot, "extracted", "interp.json");
        var messagesPath = Path.Combine(repoRoot, "extracted", "messages.json");
        bool mute = false;
        bool debugCollision = false;

        bool gamezOverridden = false, texturesOverridden = false, zrdrOverridden = false, soundsOverridden = false;
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--plane=")) _planeName = arg["--plane=".Length..];
            else if (arg == "--damage") _damageLab = true;
            else if (arg.StartsWith("--damage=")) { _damageLab = true; _damagePreset = ParseDamagePreset(arg["--damage=".Length..]); }
            else if (arg == "--chapter") _worldMode = true;
            else if (arg.StartsWith("--chapter=")) { _chapter = arg["--chapter=".Length..]; _worldMode = true; }
            else if (arg == "--fly") _fly = true;
            else if (arg == "--stunt") _stunt = true;
            else if (arg == "--debug-dzpaths") _debugDzPaths = true;
            else if (arg.StartsWith("--mission=")) _mission = arg["--mission=".Length..];
            else if (arg.StartsWith("--scenario=")) { _scenario = arg["--scenario=".Length..]; _scenarioExplicit = true; }
            else if (arg.StartsWith("--spawn=")) _spawnIndex = int.Parse(arg["--spawn=".Length..]);
            else if (arg.StartsWith("--spawn-at=")) _spawnAt = ParseVec3(arg["--spawn-at=".Length..]);
            else if (arg.StartsWith("--spawn-dir=")) _spawnDir = ParseVec3(arg["--spawn-dir=".Length..]);
            else if (arg.StartsWith("--sky-zone=")) { _skyZone = arg["--sky-zone=".Length..]; _skyZoneExplicit = true; }
            else if (arg.StartsWith("--gamez=")) { gamezPath = arg["--gamez=".Length..]; gamezOverridden = true; }
            else if (arg.StartsWith("--textures=")) { texturesPath = arg["--textures=".Length..]; texturesOverridden = true; }
            else if (arg.StartsWith("--zrdr=")) { zrdrPath = arg["--zrdr=".Length..]; zrdrOverridden = true; }
            else if (arg.StartsWith("--interp=")) interpPath = arg["--interp=".Length..];
            else if (arg.StartsWith("--sounds=")) { soundsPath = arg["--sounds=".Length..]; soundsOverridden = true; }
            else if (arg.StartsWith("--messages=")) messagesPath = arg["--messages=".Length..];
            else if (arg == "--mute") mute = true;
            else if (arg == "--debug-collision") debugCollision = true;
            else if (arg.StartsWith("--hold=")) _holdSegments = ParseHold(arg["--hold=".Length..]);
            else if (arg.StartsWith("--frames=")) _screenshotFrames = int.Parse(arg["--frames=".Length..]);
            else if (arg.StartsWith("--shots=")) _screenshotShots = Math.Max(1, int.Parse(arg["--shots=".Length..]));
            else if (arg.StartsWith("--jitter=")) _jitterDeg = float.Parse(arg["--jitter=".Length..], System.Globalization.CultureInfo.InvariantCulture);
            else if (arg.StartsWith("--screenshot=")) _screenshotPath = arg["--screenshot=".Length..];
            else if (arg.StartsWith("--yaw=")) _yaw = float.Parse(arg["--yaw=".Length..], System.Globalization.CultureInfo.InvariantCulture);
            else if (arg.StartsWith("--pitch=")) _pitch = float.Parse(arg["--pitch=".Length..], System.Globalization.CultureInfo.InvariantCulture);
            else if (arg.StartsWith("--campos=")) _camPos = ParseVec3(arg["--campos=".Length..]);
            else if (arg.StartsWith("--lookat=")) _lookAt = ParseVec3(arg["--lookat=".Length..]);
        }

        // --stunt is free flight over the mission's danger zones: force the flight path and the
        // stunt_flying spawn list (unless the tester pinned another scenario for a specific spawn).
        if (_stunt)
        {
            _fly = true;
            if (!_scenarioExplicit)
                _scenario = "stunt_flying";
        }
        if (_fly)
            _worldMode = true;
        if (_damageLab && _worldMode)
        {
            GD.Print("--damage is the static plane viewer's lab (use --plane without --chapter/--fly); ignoring");
            _damageLab = false;
        }
        // Burst captures dither the camera by default so z-fighting flickers across frames;
        // a single shot never jitters. --jitter=<deg> overrides (0 disables).
        if (_jitterDeg < 0f)
            _jitterDeg = _screenshotShots > 1 ? 0.15f : 0f;
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
        // shader references, before any material using it is built. Defaults are a no-op
        // (nothing fades) — only --fly overrides them from the mission's weather.json below.
        RenderingServer.GlobalShaderParameterAdd("csky_fog_color",
            RenderingServer.GlobalShaderParameterType.Vec3, new Vector3(0.69f, 0.69f, 0.69f));
        RenderingServer.GlobalShaderParameterAdd("csky_fog_range",
            RenderingServer.GlobalShaderParameterType.Vec2, new Vector2(1e8f, 1e9f));
        RenderingServer.GlobalShaderParameterAdd("csky_fog_alt",
            RenderingServer.GlobalShaderParameterType.Vec2, new Vector2(1e8f, 1e9f));
        // The fullbright world's per-mission brightness from the weather's SUNLIGHT (item 6):
        // 1.0 = fullbright (no darkening) for static views / missions without weather; --fly
        // overrides it from WeatherState.WorldLight below.
        RenderingServer.GlobalShaderParameterAdd("csky_world_light",
            RenderingServer.GlobalShaderParameterType.Float, 1.0f);

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

                // --debug-dzpaths: the mission's danger-zone route ribbons (world build skips
                // them — AI/route data the original never renders). Debug inspection only.
                if (_debugDzPaths && builder.BuildDzPaths() is { } dzpaths)
                {
                    _plane.AddChild(dzpaths);
                    GD.Print("debug: dzpaths route ribbons built");
                }

                // Clutter: forest trees / river bushes. The chapter's boot script names the
                // templates; ClutterBuilder stamps them onto every matching-textured world
                // polygon (see Clutter.cs). Solid in flight, like the original.
                ClutterBuilder? clutterBuilder = null;
                var clutterNames = ClutterBuilder.TemplateNames(interpPath, _chapter);
                if (clutterNames.Count > 0)
                {
                    clutterBuilder = new ClutterBuilder(gamez, textures);
                    if (clutterBuilder.Build(clutterNames, collision: _fly) is { } clutter)
                    {
                        _plane.AddChild(clutter);
                        GD.Print($"clutter: {clutterBuilder.InstanceCount} sprites ({clutterBuilder.Summary})" +
                                 (_fly ? $", {clutterBuilder.ColliderTriangles} collision tris" : ""));
                    }
                }
                else
                {
                    GD.Print($"clutter: no templates for {_chapter} ({interpPath})");
                }

                // Mission start states (anim-state engine part 1): replay the mission's
                // animation base/start states onto the built world — hides the destroyed
                // building variants (coplanar flicker), the zeppelins/trains this mission
                // deactivates (zepstate.json), and poses the startanims end states
                // (hangar doors open). Uses the same three zrdr scopes the original
                // compiles into mis_anim.zbd: shared + chapter + mission.
                var chapterZrdrPath = PreferUnzipped(Path.Combine(repoRoot, "extracted", _chapter, "zrdr.zip"));
                MissionState.Apply(_plane, zrdrPath, chapterZrdrPath, missionZrdrPath);

                // Map-edge continuation: a rolling window of mirrored terrain tiles (WITH the
                // chapter's clutter) that follows the plane past the map boundary, so the world
                // continues indefinitely under the fog like the original's tile-reload grid
                // (see MapEdgeExtender). On in --fly and in static weathered views (--sky-zone,
                // for edge-verification shots); off for plain orbit viewing (honest data view).
                if (_fly || _skyZoneExplicit)
                {
                    _edgeExtender = builder.CreateEdgeExtender(clutterBuilder);
                    if (_edgeExtender != null)
                    {
                        _plane.AddChild(_edgeExtender);
                        GD.Print("map edge: rolling mirrored-tile window active");
                    }
                }
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
                    // zone + the cloud-band whiteout + the ambient cloud puffs. Applied whenever
                    // the world+dome are shown — in --fly, and in static --chapter when --sky-zone
                    // is given (deterministic fog/whiteout/puff verification with --campos, same as
                    // the sky-verification path).
                    SetupWeather(missionZrdrPath, textures);
                }
                meshInstances = builder.MeshInstanceCount;
                colliders = builder.ColliderCount;
                what = $"chapter {_chapter} world";
            }
            else
            {
                // The damage lab needs the pdpN torn-skin panels the plain viewer skips.
                var builder = new PlaneBuilder(gamez, textures, damagePanels: _damageLab);
                _plane = builder.Build(_planeName);
                meshInstances = builder.MeshInstanceCount;
                what = $"'{_planeName}'";

                // Damage lab (--damage): per-part HP sliders driving the item-10c damage
                // visuals on the parked plane — the same DamageVisuals/puffer pipeline as
                // flight, with the distance-interval trails burning in place (DamageLab).
                if (_damageLab)
                {
                    var stats = PlaneStats.Load(zrdrPath, _planeName);
                    if (stats.DestroyableParts.Count == 0)
                    {
                        GD.Print($"damage lab: '{_planeName}' ({stats.DefName}) has no destroyable_parts");
                    }
                    else
                    {
                        var smoke = MakePuffer(zrdrPath, textures, this, "pufftrails.json", "smokepuffer");
                        var fire = MakePuffer(zrdrPath, textures, this, "pufftrails.json", "firepuffer");
                        var panelTrails = new List<Effects.Puffer>();
                        for (int i = 0; i < 8; i++) // pool one per pdp panel — the lab can flip all of them
                            if (MakePuffer(zrdrPath, textures, this, "pufftrails.json", "firepuffer") is { } pt)
                                panelTrails.Add(pt);
                        var visuals = new DamageVisuals(builder.DamagePanels, _plane, stats, smoke, fire, panelTrails);
                        // the HUD gauge cluster as a lab toggle (user request): the damage
                        // dial mirrors the sliders, blinks on decreases like a flight hit
                        var labGauges = GaugeCluster.Build(gamez, _planeName, textures, stats.DestroyableParts);
                        AddChild(new DamageLab(stats, visuals, _plane, _damagePreset, labGauges));
                        GD.Print($"damage lab: {stats.DestroyableParts.Count} part sliders, " +
                                 $"{visuals.PanelCount} panels, {panelTrails.Count} panel fire trails");
                        what += " + damage lab";
                    }
                }
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
                    HoldSegments = _holdSegments,
                    DebugCollision = debugCollision,
                    PlaneModel = planeModel,
                    Props = PropAnimator.Build(planeModel), // spin the propeller/rotor blur discs
                    WingLights = WingLightBlinker.Build(planeBuilder.WingFlares), // blink the wingtip flares
                    Surfaces = ControlSurfaceAnimator.Build(planeModel), // deflect ailerons/elevators/rudders
                    Collider = PlaneCollider.Build(planeModel), // swept airframe boxes (wingtip/tail collision)
                    // per-part HP from destroyable_parts (item 10b) — collisions below
                    // the crash threshold damage the struck part instead of crashing
                    Damage = stats.DestroyableParts.Count > 0 ? new PlaneDamage(stats.DestroyableParts) : null,
                };
                controller.AddChild(planeModel);
                if (controller.Props != null)
                    GD.Print($"props: {controller.Props.Count} spinning blur nodes");
                if (controller.WingLights != null)
                    GD.Print($"wing lights: {controller.WingLights.Count} blinking flares");
                if (controller.Surfaces != null)
                    GD.Print($"control surfaces: {controller.Surfaces.Count} deflecting nodes");
                if (controller.Collider != null)
                    GD.Print($"plane collider: {controller.Collider.Summary}");
                else
                    GD.PushWarning("no airframe collision boxes — falling back to the center ray");
                if (controller.Damage != null)
                {
                    var partDescs = new List<string>();
                    foreach (var p in stats.DestroyableParts)
                        partDescs.Add($"{p.Name} {p.MaxHp:0}hp{(p.Critical ? "*" : "")}{(p.Engine ? " engine" : "")}");
                    GD.Print($"damage parts: {string.Join(", ", partDescs)} (* = critical)");
                }

                // The original's heading tape, rebuilt from the chapter's own HUD
                // textures (compassticks2/compasstxt ship in every chapter's archive).
                controller.Compass = CompassTape.Build(textures);
                if (controller.Compass != null)
                    GD.Print("compass: heading tape from compassticks2/compasstxt");

                // The cockpit dials (altimeter / speedometer / damage display), rebuilt
                // from the plane's own gauges subtree in planes.zbd + the chapter's
                // HUD textures (needle/lowalt/stall/<plane>_damage/hilite/hatchptrn).
                controller.Gauges = GaugeCluster.Build(planesGamez, _planeName, textures,
                    stats.DestroyableParts);
                if (controller.Gauges != null)
                {
                    var damage = controller.Damage;
                    if (damage != null)
                        controller.Gauges.PartFraction = name =>
                            damage.Parts.TryGetValue(name, out var s) ? s.Fraction : 1f;
                    GD.Print("gauges: altimeter/speedometer/damage dial from the plane's gauges subtree");
                }

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

                // Visible damage (item 10c): torn-skin panel flips + the low-HP smoke/fire
                // trail (pufftrails.json → dense_firetrail's smokepuffer/firepuffer pair).
                if (controller.Damage != null)
                {
                    var smoke = MakePuffer(zrdrPath, textures, controller, "pufftrails.json", "smokepuffer");
                    var fire = MakePuffer(zrdrPath, textures, controller, "pufftrails.json", "firepuffer");
                    // per-panel fire trails (the original streams one from every damaged
                    // panel — clearly visible in OriginalScreenshots/Videos/C1 IA1 Crash.mp4)
                    var panelTrails = new List<Effects.Puffer>();
                    for (int i = 0; i < 4; i++)
                        if (MakePuffer(zrdrPath, textures, controller, "pufftrails.json", "firepuffer") is { } pt)
                            panelTrails.Add(pt);
                    controller.Visuals = new DamageVisuals(planeBuilder.DamagePanels, planeModel, stats, smoke, fire, panelTrails);
                    GD.Print($"damage visuals: {controller.Visuals.PanelCount} panels, " +
                             $"smoke={(smoke != null ? "on" : "off")} fire={(fire != null ? "on" : "off")}, " +
                             $"{panelTrails.Count} panel fire trails");
                }

                // Crash breakup (item 10d): the plane's destroyed-subtree wreck pieces +
                // the player_plane_destruct wreck fire and a rising black-smoke column.
                var wreckFire = MakePuffer(zrdrPath, textures, controller, "player_plane_destruct.json", "fire_n_smoke", duration: 10f);
                var wreckSmoke = MakePuffer(zrdrPath, textures, controller, "pufftrails.json", "black_smoke");
                controller.Breakup = CrashBreakup.Create(
                    planeBuilder.BuildDestroyed(_planeName), wreckFire, wreckSmoke);
                if (controller.Breakup != null)
                {
                    controller.AddChild(controller.Breakup.WreckRoot);
                    GD.Print($"crash breakup: {controller.Breakup.PieceCount} wreck pieces, " +
                             $"fire={(wreckFire != null ? "on" : "off")} smoke={(wreckSmoke != null ? "on" : "off")}");
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
                // Stunt run (M2.5 item 1): the mission's danger-zone objectives from ia.json
                // dzones, positions resolved against this chapter world's gamez, display
                // strings from targets.json → messages.json. --stunt only.
                if (_stunt)
                {
                    controller.Stunt = StuntMission.Load(gamez, missionZrdrPath, Messages.Load(messagesPath));
                    if (controller.Stunt == null)
                        GD.PushWarning($"--stunt: no danger zones for {_chapter}/{_mission} — flying free");
                    else
                        what += $" [stunt: {controller.Stunt.TotalCount} zones]";
                }

                var (spawnPos, spawnLookAt) = ChooseSpawn(missionZrdrPath);
                controller.Setup(new FlightModel(stats), _camera, spawnPos, spawnLookAt);
                AddChild(controller);
                what += $" + '{_planeName}' flying";
            }

            GD.Print($"loaded {what}: {gamez.Nodes.Count} gamez nodes, " +
                     $"{meshInstances} mesh instances, {colliders} colliders, {sw.ElapsedMilliseconds} ms");
            if (textures.MissingTextures.Count > 0)
                GD.Print($"[textures] {textures.MissingTextures.Count} referenced texture(s) absent from this install: " +
                         string.Join(", ", textures.MissingTextures));
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

    /// <summary>Parse a scripted hold sequence: segments separated by ';', each
    /// "pitch,roll,yaw,throttle" with an optional "@seconds" duration. The last
    /// segment (or one without a duration) holds forever — so the plain single
    /// "--hold=p,r,y,thr" form keeps its old constant-input meaning.</summary>
    private static (FlightInput, float)[] ParseHold(string s)
    {
        static float F(string v) => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
        var segments = new List<(FlightInput, float)>();
        foreach (var seg in s.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var at = seg.Split('@');
            var p = at[0].Split(',');
            segments.Add((new FlightInput { Pitch = F(p[0]), Roll = F(p[1]), Yaw = F(p[2]), Throttle = F(p[3]) },
                          at.Length > 1 ? F(at[1]) : 0f));
        }
        return segments.ToArray();
    }

    /// <summary>Parse --damage= presets: "nose:0.25,leftwing:40" — part:fraction pairs,
    /// values > 1 read as percent. Malformed pairs are skipped with a note.</summary>
    private static List<(string, float)> ParseDamagePreset(string s)
    {
        var list = new List<(string, float)>();
        foreach (var item in s.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = item.Split(':');
            if (kv.Length == 2 && float.TryParse(kv[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float v))
                list.Add((kv[0].Trim(), Mathf.Clamp(v > 1f ? v / 100f : v, 0f, 1f)));
            else
                GD.Print($"--damage: cannot parse '{item}' (want part:fraction)");
        }
        return list;
    }

    /// <summary>Loads a named PUFFER_STATE from a zrdr effects reader and builds its
    /// emitter under <paramref name="parent"/>; null (logged by PufferState.Load) when
    /// the reader or its textures are missing. Shared by the flight assembly and the
    /// static damage lab.</summary>
    private static Effects.Puffer? MakePuffer(string zrdrPath, TextureArchive textures, Node parent,
        string file, string name, float duration = 0.3f)
    {
        var state = Effects.PufferState.Load(zrdrPath, file, name);
        var puffer = state != null ? Effects.Puffer.Create(state, textures, duration) : null;
        if (puffer != null)
            parent.AddChild(puffer);
        return puffer;
    }

    /// <summary>Loads the flown mission's weather.json and applies it: sets the distance-fog
    /// global shader parameters for the rendered sky zone (all world + aircraft surfaces pick
    /// them up), and builds the full-screen cloud-band whiteout overlay (its opacity is driven
    /// each frame from the camera altitude in <see cref="_Process"/>). No-op if the mission has
    /// no weather.json — the fog globals keep their registered no-op range.</summary>
    private void SetupWeather(string missionZrdrPath, TextureArchive textures)
    {
        _weather = WeatherState.Load(missionZrdrPath);
        if (_weather == null)
        {
            GD.PushWarning($"no weather.json for {_chapter}/{_mission} — flying without fog / whiteout");
            return;
        }
        var fog = _weather.Fog(_skyZone);
        // FOG_COLOR is a DX7-era framebuffer (sRGB) value: the original's fully-fogged pixels
        // are exactly 0.69·255 = 176 gray (measured in OriginalScreenshots/"C1 IA1 Cloudcoverage
        // 1.png", flat regions std 0). The shader mixes ALBEDO in linear space, so convert —
        // feeding 0.69 in raw made saturated fog render as 216, a washed-out near-white.
        var fogLinear = fog.FogColor.SrgbToLinear();
        RenderingServer.GlobalShaderParameterSet("csky_fog_color",
            new Vector3(fogLinear.R, fogLinear.G, fogLinear.B));
        //Range is halved because this does not seem to be radius but diameter. See Screenshot C1 IA1 Fog Range.png vs Screenshots\Fog Range.png
        // NOTE: that calibration predates the fog-color sRGB fix below (the old washed-out
        // near-white read weaker than true 176 gray) — worth a fresh in-game A/B; factor 1
        // makes the overcast deck's texture persist further down toward the horizon.
        float fogRangeFactor = 2.0f;
        RenderingServer.GlobalShaderParameterSet("csky_fog_range", new Vector2(fog.FogNear, fog.FogFar)/fogRangeFactor);
        // FOG_ALTITUDE: the fog cylinder's vertical extent — full fog below FogLow, fading to
        // none at FogHigh (fragment altitude; see SceneBuilder's fog shader block). Absolute
        // altitudes, so the range factor doesn't apply.
        RenderingServer.GlobalShaderParameterSet("csky_fog_alt", new Vector2(fog.FogLow, fog.FogHigh));
        // World brightness from the zone's SUNLIGHT (see WeatherState.WorldLight): the original
        // dims the baked-vertex world by the mission's ambient+diffuse; we apply it as a scalar
        // on the fullbright world/deck/dome (the fog color, set above, is unaffected — it mixes
        // in after). 1.0 for bright/day missions, < 1 for overcast/night. (item 6.)
        // Apply the dimming in GAMMA space (the DX7 chain texel×vtx×light is all sRGB-space),
        // consistent with the gamma-space vertex modulate: the shader multiplies LINEAR ALBEDO,
        // so feed the linearised factor — linear_ALBEDO · srgbToLinear(f) == gamma-space · f.
        // (Applied in linear space, 0.80 only reaches 210→190; gamma-space lands the deck 210→169.)
        float worldLightLinear = new Color(fog.WorldLight, fog.WorldLight, fog.WorldLight).SrgbToLinear().R;
        RenderingServer.GlobalShaderParameterSet("csky_world_light", worldLightLinear);
        GD.Print($"weather [{_skyZone}]: fog {fog.FogColor.R:0.00} gray {fog.FogNear:0}–{fog.FogFar:0} m, " +
                 $"altitude {fog.FogLow:0}–{fog.FogHigh:0} m; world light {fog.WorldLight:0.00}; " +
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

            // Ambient cloud puffs: the soft wisps that drift past the plane at altitude
            // (OriginalScreenshots/"C1 IA1 Cloud Puffs and Moon.png"). A hand-tuned field
            // gated to the cloud band and drifting with the weather WIND; _Process advances it
            // each frame from the camera. Added at world identity (its instance positions are
            // absolute world coords).
            _puffs = Effects.CloudPuffs.Create(textures, _weather.WindStatic,
                _weather.CloudBottom, _weather.CloudTop);
            if (_puffs != null)
            {
                AddChild(_puffs);
                GD.Print("cloud puffs: ambient field active over the cloud band");
            }
        }

        // Precipitation (rain/snow) — only the missions whose weather.json carries a TYPE block
        // get a field (C4 snow, C1C/C2B rain). It shows only below the CLOUD_COVER band (the
        // rain falls from the cloud base — none above the overcast). Self-animating from the
        // shader's TIME + camera built-ins, so it needs no _Process driving.
        _precip = Effects.Precipitation.Create(_weather.Precip, _weather.CloudBottom, _weather.CloudTop);
        if (_precip != null)
            AddChild(_precip);
    }

    /// <summary>Picks the flight spawn for the current mission: a world position + a look-at
    /// point one unit ahead along the spawn heading. Instant-action missions (IA1) draw from
    /// ia.json's scenario spawn list — random per launch like the original, or forced by
    /// --spawn=N. Story missions (M0x, no ia.json) fall back to objectives.json PLAYER_INIT.
    /// A fixed C1 spawn is the last resort if neither is present.</summary>
    private (Vector3 pos, Vector3 lookAt) ChooseSpawn(string missionZrdrPath)
    {
        // Debug/testing override: place the plane exactly (position + nose direction), bypassing
        // the mission spawn list — lets a scripted run start just short of a target pointed at it,
        // so a neutral --hold flies a straight, deterministic path (no complex maneuvering).
        if (_spawnAt is { } at)
        {
            var dir = _spawnDir ?? Vector3.Forward;
            if (dir.LengthSquared() < 1e-6f)
                dir = Vector3.Forward;
            GD.Print($"spawn [override]: pos=({at.X:0},{at.Y:0},{at.Z:0}) " +
                     $"dir=({dir.X:0.00},{dir.Y:0.00},{dir.Z:0.00})");
            return (at, at + dir.Normalized());
        }

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
        // F11 anywhere: print the current camera pose as ready-to-paste --campos=/--lookat=
        // args, so a hand-framed orbit (or in-flight) view can be reproduced for a
        // deterministic --screenshot run.
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F11 })
        {
            PrintCameraPose();
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

        // Ambient cloud puffs: keep the drifting field around the plane (world-anchored,
        // recycled at the shell edge — see CloudPuffs). Forward is the camera's -Z look dir,
        // so fresh puffs spawn ahead and the plane flies into them.
        _puffs?.Update((float)delta, _camera.Position, -_camera.GlobalTransform.Basis.Z);

        // Map-edge continuation: re-center the mirrored-tile window on the camera. Cheap
        // no-op until a cell boundary (1024 m) is crossed, then ~a window row rebuilds.
        _edgeExtender?.Update(_camera.Position);

        if (_screenshotPath == null || _plane == null)
            return;
        if (--_screenshotFrames > 0)      // still counting down the warm-up delay
            return;
        // Delay elapsed: grab one frame per _Process call for _screenshotShots frames,
        // then quit. A single shot keeps the original path verbatim; a burst (for z-fight
        // debugging, where flicker only shows across frames) writes indexed files. The
        // captured image is the PREVIOUS frame's render, so file _00 is the un-jittered
        // baseline and _01.. carry the dither applied below — all distinct, which is all
        // the flip-through needs.
        var img = GetViewport().GetTexture().GetImage();
        var path = _screenshotShots > 1 ? IndexedShotPath(_screenshotPath, _shotIndex) : _screenshotPath;
        img.SavePng(path);
        GD.Print($"screenshot saved: {path}");
        if (++_shotIndex >= _screenshotShots)
        {
            _screenshotPath = null;
            GetTree().Quit();
            return;
        }
        if (_jitterDeg > 0f && !_fly)
            ApplyShotJitter();
    }

    /// <summary>Rotate the burst camera a hair around the framed point each --shots frame so
    /// coplanar surfaces re-decide the depth test and z-fighting flicker surfaces across the
    /// sequence (a dead-still camera can render bit-identical frames). The eye micro-orbits
    /// the pivot — depths change, but the camera keeps looking at the pivot so the subject
    /// stays centred. Static mode only: in --fly the FlightController owns the camera each
    /// frame (and the plane's own motion already surfaces the fight).</summary>
    private void ApplyShotJitter()
    {
        if (_shotBaseXform is not { } baseX)
        {
            baseX = _camera.GlobalTransform;
            _shotBaseXform = baseX;
            _shotPivot = _orbitCenter;   // the point UpdateCamera aims at
        }
        // Golden-angle spread so consecutive frames differ maximally.
        float mag = Mathf.DegToRad(_jitterDeg);
        float phase = _shotIndex * 2.399963f;
        var rot = new Basis(Vector3.Up, mag * Mathf.Cos(phase))
                * new Basis(baseX.Basis.X.Normalized(), mag * Mathf.Sin(phase));
        // Rigidly rotate the whole camera about the pivot: rotating both the eye offset and
        // the basis by the same rotation preserves the aim exactly, so framing is kept.
        var origin = _shotPivot + rot * (baseX.Origin - _shotPivot);
        _camera.GlobalTransform = new Transform3D(rot * baseX.Basis, origin);
    }

    /// <summary>Insert a zero-padded frame index before the extension:
    /// foo.png -> foo_00.png. Used for --shots=N burst capture.</summary>
    private static string IndexedShotPath(string path, int index)
    {
        var dir = Path.GetDirectoryName(path) ?? "";
        var stem = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        return Path.Combine(dir, $"{stem}_{index:D2}{ext}");
    }

    /// <summary>Print the camera's current world pose as ready-to-paste --campos=/--lookat=
    /// arguments (F11, any mode). Reproducing a hand-framed orbit or in-flight vantage for a
    /// deterministic --screenshot run is otherwise fiddly; this prints exactly what
    /// FrameCamera consumes. In orbit mode the look-at is the framed point (_orbitCenter); in
    /// --fly it is a point one unit ahead along the view ray — either reproduces the same
    /// framing (FrameCamera reconstructs pitch/yaw from the pos→look-at direction).</summary>
    private void PrintCameraPose()
    {
        var pos = _camera.GlobalPosition;
        var lookAt = _fly ? pos - _camera.GlobalTransform.Basis.Z : _orbitCenter;
        GD.Print($"camera pose: --campos={Vec3Arg(pos)} --lookat={Vec3Arg(lookAt)}");
    }

    /// <summary>Format a vector as the "x,y,z" argument value --campos=/--lookat= parse
    /// (invariant culture, matching ParseVec3; trimmed to 3 decimals).</summary>
    private static string Vec3Arg(Vector3 v) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0:0.###},{1:0.###},{2:0.###}", v.X, v.Y, v.Z);

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
