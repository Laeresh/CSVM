using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using Godot;

namespace CSVM;

/// <summary>
/// Milestone 2 vertical-slice viewer: loads one aircraft from the player's own
/// extracted game data and renders it with orbit controls — or, with --fly,
/// free flight over the chapter world with arcade controls.
///
/// Launched with no content-selecting arg (a bare launch, e.g. RunGame.ps1), it shows the
/// in-game launchscreen (Mode → Chapter → Plane; see src/UI/LaunchMenu.cs) instead; the menu's
/// selection feeds the same StartSession build path the CLI drives, and Esc from a menu-launched
/// flight returns to the launchscreen (ReturnToMenu). Any explicit content arg bypasses the menu.
///
/// F12 (any mode) saves the current frame to a timestamped PNG under the repo's
/// git-ignored Screenshots/ folder. F11 (any mode) prints the current camera pose as
/// ready-to-paste --campos=/--lookat= args for reproducing a view.
///
/// User args (after "--" on the command line):
///   --plane=player_bhawk         which aircraft root node to build. A comma-separated list gives
///                                one plane per splitscreen player (--plane=player_bhawk,player_fury)
///                                and implies --players= that count unless --players says otherwise
///   --damage[=part:frac,…]       (static --plane mode) damage lab: one HP slider per
///                                destroyable part drives the item-10c damage visuals on
///                                the parked plane — torn-skin panel flips, panel fires
///                                burning in place, the ≤10% nose smoke/fire pair.
///                                Optional presets (fraction 0–1, or a percent when >1):
///                                --damage=leftwing:0.25,nose:40. H hides the sliders;
///                                combine with --screenshot for deterministic damage shots
///   --markers                    (implies --viewer) open the marker overlay at launch: the
///                                firepoint / pylon / target gizmos on the parked aircraft,
///                                shared mounts flagged. Toggle in a plain --viewer with K
///   --dump-markers[=plane]       print each player airframe's firepoint / pylon / target rig
///                                (name, plane-frame position, gun pair, shared mounts) to
///                                stdout and ./.scratch/markers_dump.txt, then quit; the
///                                optional value filters to one plane (model or display name)
///   --dump-weapons[=id|name]     print the typed weapons.json table (WeaponDefs) — one block
///                                per def with ballistics, flags and FIRE/FLYOUT/IMPACT bindings
///                                — to stdout and ./.scratch/weapons_dump.txt, then quit; a clean
///                                run reports no unhandled keys. Optional filter by id / NAME
///   --dump-loadout[=plane]       build each plane and bind its stock loadout (Loadout), printing
///                                the resolved gun groups + hardpoints to ./.scratch/loadout_dump.txt,
///                                then quit; a missing marker prints a loud error. Filter by plane
///   --loadout=<def>              bind this loadout def instead of the plane's own (testing override;
///                                exercises the missing-marker error with --dump-loadout; in flight
///                                the flown plane carries that loadout)
///   --fire                       hold the gun trigger down (scripted firing runs); in interactive
///                                flight the gun trigger is Space / gamepad B
///   --fire-rockets               hold the rocket trigger down (scripted runs); in interactive flight
///                                the rocket trigger is F / gamepad A (one rocket per pull, 1 s cooldown)
///   --gun-select=N               initial gun group (0-based; 0 = first group, the default). Only one
///                                group fires at a time (a testing hook; interactively cycle with G / D-pad Left)
///   --infinite-ammo              guns/hardpoints fire without depleting (weapon testing)
///   --damage-test[=name]         build the --chapter world, then drive one destructible's HP from
///                                full to zero, logging which DAMAGE_SEQUENCE stage effect fires at
///                                which health, then quit (C22 verify until F40; optional name filter)
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
///                                spawn list. Completion shows in the text HUD
///   --anim-lab                   the animation debugger: the chapter world
///                                as a quiet stage (reset states applied, nothing playing) under
///                                a deterministic fixed-dt clock with def-playback transport
///                                controls — see UI.AnimLab. Wins over every other mode
///   --play-anim=name             (implies --anim-lab) play this def at launch and auto-frame
///                                the orbit camera on its anchor; composes with --screenshot
///   --seed=N                     the lab's pinned RNG seed (default AnimLab.DefaultSeed) —
///                                same seed, same dice, identical replay
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
///   --players=N                  splitscreen: fly N planes (1–4) in one shared
///                                world, each in its own pane with its own camera, sky, HUD and
///                                input device. 2P = stacked top/bottom, 3–4P = 2×2 grid. P1 =
///                                keyboard + the first pad, P2–P4 = the next pads in order.
///                                Requires --fly/--stunt; N=1 is the normal single-player path.
///                                Launched from the menu instead, the join flow binds the pads
///   --debug-join=N               (launchscreen only) add N device-less players to the join strip
///                                so the splitscreen aircraft select can be screenshot without N
///                                controllers; they can never act (and the last starts locked, so
///                                both panel states show), making the shot deterministic
///   --hold=pitch,roll,yaw,thr    scripted flight input instead of the keyboard (automated runs);
///                                ';'-separated segments with '@seconds' durations sequence inputs
///                                (e.g. --hold=1,0,0,0.5@3;0,0,0,0 — pull 3 s, then release), the
///                                last segment holds forever, respawn restarts the sequence.
///                                '|' separates PER-PLAYER sequences for --players (the last one
///                                covers any remaining players)
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
///   --menu[=mode|chapter|plane]  force the in-game launchscreen even alongside other args (it
///                                otherwise shows only on a bare no-content-arg launch); the
///                                optional screen name opens it there (a --screenshot layout aid)
/// </summary>
public partial class PlaneViewer : Node3D
{
    private const float HorizonScale = 2.5f;

    // Splitscreen with a --spawn-at override: lateral offset between players so they don't spawn
    // inside each other (the mission spawn lists already place players apart). TUNE.
    private const float SpawnAbreast = 60f;

    // Cloud-band whiteout color: inside a cloud reads near-white (see OriginalScreenshots/
    // "C1 IA1 whiteout at height.png"), not the 0.69 gray of distance fog. TUNE. The opacity
    // (0 at the band edges → 1 at the opaque core) comes from WeatherState.WhiteoutAmount.
    private static readonly Color WhiteoutColor = new(0.95f, 0.95f, 0.96f);

    private string _planeName = "player_bhawk";
    // Splitscreen: one plane per player, from the launchscreen's simultaneous pick
    // or a comma-separated --plane= list. Empty = everyone flies _planeName (the 1P/CLI default).
    private readonly List<string> _planeNames = new();
    // Per-player pad binding chosen in the launchscreen's join flow (null = derive from the
    // connected roster in AssignPads, which is what every CLI launch does).
    private int[][]? _menuPads;
    private int _debugJoin;            // --debug-join=N: extra device-less menu players (screenshot aid)
    // Aircraft paint. --paint= names a shipped pattern / "random" / "none",
    // comma-separated per player like --plane=. Null = the mode default: free flight and
    // stunt runs randomize a livery per player on every map load (the original gives every
    // aircraft in the world its own squadron colours), static --plane views stay unpainted
    // so existing orbit/damage-lab screenshots are unchanged.
    private string[]? _paintNames;
    private Color[]? _paintColorOverride;  // --paint-color=r,g,b/r,g,b/r,g,b
    private int[]? _paintDecalOverride;    // --paint-decal=nose,tail,wing
    private ulong _paintSeed;              // --paint-seed=N: reproducible random liveries
    private bool _paintSeedExplicit;
    private List<PaintScheme>? _paintCatalog;  // the 12 shipped patterns, loaded on demand
    private string _rofPath = "";              // --rof=: the extracted UI archive (paint patterns)
    private PatternLibrary? _patternLibrary;   // the per-pattern region masks, loaded once
    private bool _damageLab;           // --damage: per-part HP sliders driving the 10c visuals (static --plane mode)
    private List<(string, float)>? _damagePreset; // --damage=part:frac,… preset fractions
    private string _skyZone = "zone2"; // the sky the original shows at the C1 airfield (night)
    private bool _skyZoneExplicit;     // --sky-zone given: render the horizon even in static --chapter mode
    // The zone actually rendered: _skyZone when this mission defines it, otherwise the first
    // zone its weather.json does (WeatherState.ResolveZone). C5 ships zone1+zone3, so the
    // zone2 default resolves to zone1 there — CONFIRMED correct by playtest, not
    // just a lucky fallback; C1–C4 all define zone2 and resolve to themselves.
    // Reassigned on every StartSession, so a menu rebuild never inherits the last chapter's.
    private string _activeZone = "zone2";
    private string _chapter = "C1";    // which chapter's world to build (--chapter=): C1, C1B, C1C, C2, C2B, C3, C4, C5
    private bool _worldMode;           // render the chapter world instead of a single plane
    private bool _fly;
    // --viewer: the static inspection view. Flight is the default for any
    // content arg, so this is how you get the parked-plane orbit — and it is where the
    // damage lab (--damage) and the livery lab (L) live.
    private bool _viewerMode;
    private bool _chapterGiven;        // --chapter/--chapter= seen: --viewer renders that world
    private string _mission = "IA1";   // which mission's spawns to fly from (--mission=): IA1, M01, …
    private string _scenario = "zeppelin_run"; // which instant-action scenario's spawn list (--scenario=)
    private bool _scenarioExplicit;    // --scenario= given (so --stunt doesn't override it)
    private bool _stunt;               // --stunt: stunt-flying mode (= --fly + stunt_flying spawns + StuntMission)
    // --freecam: spectator mode — the live chapter world (sky,
    // weather, edge continuation, animations) with NO aircraft, observed from a free-flying
    // camera. The testing view for animation work: park in front of a moving object and watch.
    private bool _freecam;
    private SpectatorCamera? _spectator;
    // --anim-lab: the animation debugger — the chapter world as a
    // quiet stage under a deterministic fixed-dt clock with def-playback transport (UI.AnimLab).
    // The most specific mode of all, so it wins outright when combined with any other.
    private bool _animLab;
    private string? _playAnim;                      // --play-anim=<name>: play at launch (implies --anim-lab)
    private int _labSeed = UI.AnimLab.DefaultSeed;  // --seed=N: the lab's pinned RNG seed
    // --debug-anim-ui: force the lab's picker + timeline visible in a scripted --screenshot run
    // (the timeline-verification path — the UI is otherwise hidden there so shots stay
    // byte-identical), the same house convention as --debug-livery.
    private bool _debugAnimUi;
    // --debug-anim: log every live animation motion once a second (headless verification
    // that the train/doors actually move, without flying a camera at them).
    private bool _debugAnim;
    // --anim-lod=N: our answer to the data's ANIMATION_LOD condition, a quality setting
    // rather than a fact about the world. Default = the highest tier the data asks for, so
    // every LOD-gated branch runs; lower it only to A/B what the original hid on slow
    // hardware. See AnimRuntime.QualityLod.
    private int _animLod = AnimRuntime.HighLod;
    private bool _debugDzPaths;        // --debug-dzpaths: build the dzpaths route ribbons (debug-only geometry)
    private bool _debugScoreboard;     // --debug-scoreboard: force-complete the stunt run to screenshot the results board
    // --debug-livery[=N]: open the livery lab panel (hidden by default) and optionally step
    // the pattern N times, so one screenshot exercises the panel and its stepper. Null = off.
    private int? _debugLivery;
    private string? _debugMesh;  // --debug-mesh[=spec]: open the mesh lab at launch, preset modes
    private string? _debugNames; // --debug-names[=meshes|all]: switch node labels on at launch
    private bool _markersOverlay;      // --markers: open the firepoint/pylon overlay at launch (--viewer)
    private bool _dumpMarkers;         // --dump-markers[=plane]: print the marker rig table(s) and quit
    private string _dumpMarkersPlane = ""; // the optional --dump-markers= filter (model or display name)
    private bool _dumpWeapons;         // --dump-weapons[=wep_NN]: print the typed weapons.json table and quit
    private string _dumpWeaponsFilter = ""; // the optional --dump-weapons= filter (id or NAME substring)
    private bool _dumpLoadout;         // --dump-loadout[=plane]: bind each plane's stock loadout to its model and quit
    private string _dumpLoadoutFilter = ""; // the optional --dump-loadout= filter (def/model/display substring)
    private bool _damageTest;          // --damage-test[=name]: sweep one destructible's HP through its DAMAGE_SEQUENCE stages and quit
    private string _damageTestFilter = ""; // the optional --damage-test= filter (destructible NAME substring)
    private float _damageHd;           // --damage-hd=N: discrete-hit mode — apply N HEALTH_DAMAGE per hit via DamageAt, count hits to destruction (C23)
    private string? _loadoutOverride;  // --loadout=<def>: bind this loadout def instead of the plane's own (testing)
    private bool _infiniteAmmo;         // --infinite-ammo: guns/hardpoints never deplete
    private bool _autoFire;             // --fire: hold the gun trigger (scripted screenshots / soak runs)
    private bool _autoFireRockets;      // --fire-rockets: hold the rocket trigger (scripted screenshots / soak runs)
    private int _gunSelect;             // --gun-select=N: initial gun group (0-based; only one fires at a time)
    private int _spawnIndex = -1;      // --spawn=N forces a spawn; <0 = random pick (like the original)
    private Vector3? _spawnAt;         // --spawn-at=x,y,z: override the mission spawn position (debug/testing)
    private Vector3? _spawnDir;        // --spawn-dir=x,y,z: nose direction there (world space; default -Z)
    private int _players = 1;          // --players=N: splitscreen panes/planes; 1 = single player
    private (FlightInput, float)[][]? _holdSets; // --hold: one scripted sequence per player ('|'-separated)
    private Vector3? _camPos, _lookAt;
    private string? _screenshotPath;
    private int _screenshotFrames = 15;
    private int _screenshotShots = 1;  // --shots=N: consecutive frames to capture (z-fight debug)
    private int _shotIndex;            // 0-based index of the shot being written
    private float _jitterDeg = -1f;    // --jitter=<deg> burst camera dither; <0 = auto per --shots
    private Transform3D? _shotBaseXform;  // camera pose captured at the first burst frame
    private Vector3 _shotPivot;           // micro-orbit centre (keeps the subject framed)

    private Node3D? _plane;
    private Mech3.MapEdgeExtender? _edgeExtender; // rolling mirrored-tile window past the map edge
    private Vector3 _deckCenter;       // the deck geometry's original AABB centre (to re-anchor it)
    private WeatherState? _weather;    // per-mission fog + cloud band (--fly only)
    private Effects.Precipitation? _precip; // rain/snow field (self-animating, per-view for free)
    // One rig per rendered view: its camera plus the camera-anchored copies only it
    // sees (skydome / cloud deck / cloud puffs / whiteout). Exactly one entry in single player,
    // wrapping the main-viewport _camera below — so the 1P render path is unchanged.
    private readonly List<PlayerRig> _rigs = new();
    private readonly List<Vector3> _focusPoints = new(); // scratch: rig camera positions for the edge extender
    private UI.SplitScreen? _split;    // the splitscreen pane rig (null in single player)
    private Camera3D _camera = null!;
    // Built once in _Ready and kept across sessions (like the camera). The mesh lab steers
    // both — sun direction/energy and the ambient — so held here rather than local to
    // SetupLighting.
    private DirectionalLight3D _sun = null!;
    private Godot.Environment? _env;
    // The static inspection view's orbit camera (LMB orbit, wheel zoom, AABB framing); created in
    // _Ready and kept across sessions, like _camera. --yaw=/--pitch= seed its initial angles.
    private OrbitCamera _orbit = null!;
    private float? _argYaw, _argPitch;

    // Session lifecycle (the launchscreen's in-process world rebuild): everything a
    // session builds hangs under _worldRoot, so Esc-to-menu can free it and StartSession run again.
    // The camera, lights and global shader params live on `this` and persist across sessions.
    private Node3D? _worldRoot;    // the current session's subtree (world/plane/HUD/effects)
    private LaunchMenu? _menu;     // the in-game launchscreen (shown on a no-content-arg launch)
    private bool _menuDriven;      // launched into the menu → Esc from flight returns here, not quit
    private bool _forceMenu;       // --menu: show the launchscreen even alongside other args
    private string _menuStartScreen = ""; // --menu=<mode|chapter|plane>: open the menu on that screen (screenshot aid)
    private bool _inSession;       // a world is currently built
    private WorldLights? _worldLights; // the session's LIGHT_STATE point lights (see WorldLights)
    // The session-owned texture archive, kept open past the build scope so the data-driven crash can
    // bake its effect puffers lazily at crash time (the same reason --anim-lab keeps it open, but that
    // path hands it to the AnimLab node instead). Disposed by ReturnToMenu on teardown so a map reload
    // drops the previous archive instead of leaking it. Null in the anim-lab (lab-owned) case.
    private TextureArchive? _sessionTextures;
    // The world whose origin-parked entities are still being watched, and the poll accumulator.
    // See WorldBuilder.HideUnplacedEntities / RestorePlacedEntities.
    private WorldBuilder? _unplacedWatch;
    private double _unplacedRecheck;
    private bool _perf;            // --perf: log the CPU/GPU frame-time split once a second
    private double _perfClock;
    private int _perfFrames;
    private double _perfProcess, _perfGpu, _perfCpuRender, _perfPhysics;

    /// <summary>
    /// --perf: the headless stand-in for the editor's profiler. Godot's visual profiler needs
    /// the editor GUI, but the same numbers are available at runtime — and the one that settles
    /// most questions is the per-viewport measured GPU time, which separates "our shader got
    /// more expensive" from "our C# got more expensive". Averaged over a second so a single
    /// hitch doesn't read as a regression; A/B two builds by comparing the same line.
    ///
    /// <para><c>physics</c> is Godot's <c>TIME_PHYSICS_PROCESS</c> monitor —
    /// the physics tick, which is where broadphase and narrowphase cost lands. It exists because
    /// `frame`/`fps` sit pinned at the vsync cap in nearly every run here, so they are floors
    /// and cannot show a collision change getting cheaper or dearer; the physics term can move
    /// while the frame time does not. Same caveat as `script`: it is Godot's own monitor, so
    /// trust it as an A/B ratio rather than as an absolute.</para>
    /// </summary>
    private void ReportPerf(double delta)
    {
        var vp = GetViewport().GetViewportRid();
        RenderingServer.ViewportSetMeasureRenderTime(vp, true);
        _perfFrames++;
        _perfClock += delta;
        _perfProcess += Performance.GetMonitor(Performance.Monitor.TimeProcess);
        _perfPhysics += Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess);
        _perfCpuRender += RenderingServer.ViewportGetMeasuredRenderTimeCpu(vp);
        _perfGpu += RenderingServer.ViewportGetMeasuredRenderTimeGpu(vp);
        if (_perfClock < 1.0)
            return;
        double n = _perfFrames;
        GD.Print($"perf: {n / _perfClock:0.0} fps | frame {1000 * _perfClock / n:0.00} ms"
                 + $" = script {1000 * _perfProcess / n:0.00} + render-cpu {_perfCpuRender / n:0.00}"
                 + $" + gpu {_perfGpu / n:0.00} ms"
                 + $" | physics {1000 * _perfPhysics / n:0.00} ms"
                 + $" | draws {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):0}");
        _perfClock = 0; _perfFrames = 0; _perfProcess = _perfGpu = _perfCpuRender = _perfPhysics = 0;
    }

    // Base (chapter-independent) paths + parse state, set once in _Ready; StartSession reads them
    // each (re)build and recomputes the chapter-dependent gamez/texture/mission paths from _chapter.
    private string _repoRoot = "";
    // Where extracted/ lives. Defaults to _repoRoot; overridden by --data-root= or CSVM_DATA_ROOT
    // so a git worktree can run the game — /extracted/, /CrimsonSkiesGame/ and /tools/ are
    // git-ignored, so a worktree checkout has none of them and cannot otherwise build or verify.
    private string _dataRoot = "";
    private string _planesGamezPath = "";  // extracted/planes.zip — the aircraft models (always this)
    private string _zrdrPath = "";
    private string _soundsPath = "";
    private string _interpPath = "";
    private string _messagesPath = "";
    private string _gamezPath = "";         // --gamez= override value (used verbatim when set)
    private string _texturesPath = "";      // --textures= override value (verbatim when set)
    private bool _gamezOverridden, _texturesOverridden, _zrdrOverridden, _soundsOverridden;
    private bool _mute, _debugCollision;
    // --no-focus (also implied by --screenshot): set the NoFocus window flag
    // (WS_EX_NOACTIVATE on Windows) so the window doesn't steal foreground focus.
    private bool _noFocus;
    // --no-fog: an inspection aid, not a weather zone. Neutralises the distance-fog RANGE and
    // the cloud-band whiteout so geometry is visible to the horizon. Deliberately leaves
    // WorldLight and FOG_COLOR alone — the point is to remove obscuration without changing how
    // anything is *lit*, so a shading question stays answerable while fog is off.
    private bool _noFog;

    public override void _Ready()
    {
        var projectDir = ProjectSettings.GlobalizePath("res://");
        _repoRoot = Path.GetFullPath(Path.Combine(projectDir, ".."));

        // Resolved before the main arg loop below, because every base path is derived from it.
        // Precedence: --data-root= beats CSVM_DATA_ROOT beats the repo root.
        _dataRoot = _repoRoot;
        var dataRootEnv = OS.GetEnvironment("CSVM_DATA_ROOT");
        if (!string.IsNullOrEmpty(dataRootEnv)) _dataRoot = Path.GetFullPath(dataRootEnv);
        foreach (var arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--data-root="))
                _dataRoot = Path.GetFullPath(arg["--data-root=".Length..]);
        if (_dataRoot != _repoRoot)
            GD.Print($"data root: {_dataRoot} (repo root {_repoRoot})");

        var planesGamezPath = Path.Combine(_dataRoot, "extracted", "planes.zip");
        _zrdrPath = Path.Combine(_dataRoot, "extracted", "zrdr.zip");
        _soundsPath = Path.Combine(_dataRoot, "extracted", "soundsh.zip");
        _interpPath = Path.Combine(_dataRoot, "extracted", "interp.json");
        _messagesPath = Path.Combine(_dataRoot, "extracted", "messages.json");
        _rofPath = Path.Combine(_dataRoot, "extracted", "rof");

        // A content-selecting arg (--plane/--chapter/--fly/--stunt/--viewer/--damage/--screenshot)
        // builds directly and bypasses the launchscreen; a bare launch (none of them) shows the menu.
        //
        // FLIGHT IS THE DEFAULT: a content arg with no --viewer flies. So
        // `--plane=player_fury` flies the Fury and `--chapter=C4` flies over C4, where both
        // used to open a static orbit view. `--viewer` asks for that static inspection view
        // back, and is where the damage and livery labs live. `--fly` is still accepted and
        // still means exactly this — it is simply redundant now.
        bool hasContentArg = false;
        bool playersExplicit = false; // --players= given (else a --plane= list implies the count)
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--plane=")) { ParsePlanes(arg["--plane=".Length..]); hasContentArg = true; }
            else if (arg == "--viewer") { _viewerMode = true; hasContentArg = true; }
            else if (arg == "--damage") { _damageLab = true; _viewerMode = true; hasContentArg = true; }
            else if (arg.StartsWith("--damage=")) { _damageLab = true; _viewerMode = true; _damagePreset = ParseDamagePreset(arg["--damage=".Length..]); hasContentArg = true; }
            else if (arg == "--chapter") { _chapterGiven = true; hasContentArg = true; }
            else if (arg.StartsWith("--chapter=")) { _chapter = arg["--chapter=".Length..]; _chapterGiven = true; hasContentArg = true; }
            else if (arg == "--fly") { _fly = true; hasContentArg = true; }
            else if (arg == "--stunt") { _stunt = true; hasContentArg = true; }
            else if (arg == "--freecam") { _freecam = true; hasContentArg = true; }
            else if (arg == "--anim-lab") { _animLab = true; hasContentArg = true; }
            else if (arg.StartsWith("--play-anim=")) { _playAnim = arg["--play-anim=".Length..]; _animLab = true; hasContentArg = true; }
            else if (arg.StartsWith("--seed=")) { _labSeed = int.Parse(arg["--seed=".Length..]); }
            else if (arg == "--debug-anim-ui") { _debugAnimUi = true; _animLab = true; hasContentArg = true; }
            else if (arg == "--debug-anim") _debugAnim = true;
            // A connected pad with stick drift steers the free camera and nudges the flight
            // model, which quietly makes a "deterministic" scripted screenshot not one. SDL's
            // hints don't help (Godot 4.7 enumerates the pad regardless), so the switch is ours.
            else if (arg == "--no-pads") Pads.Disabled = true;
            else if (arg == "--perf") _perf = true;
            else if (arg.StartsWith("--anim-lod=")) _animLod = int.Parse(arg["--anim-lod=".Length..]);
            else if (arg == "--menu") _forceMenu = true; // force the launchscreen even with other args
            else if (arg.StartsWith("--menu=")) { _forceMenu = true; _menuStartScreen = arg["--menu=".Length..]; } // open on a screen (screenshot aid)
            else if (arg == "--debug-dzpaths") _debugDzPaths = true;
            else if (arg.StartsWith("--debug-join=")) _debugJoin = int.Parse(arg["--debug-join=".Length..]);
            else if (arg.StartsWith("--paint=")) _paintNames = arg["--paint=".Length..].Split(',', StringSplitOptions.TrimEntries);
            else if (arg.StartsWith("--paint-color=")) _paintColorOverride = ParsePaintColors(arg["--paint-color=".Length..]);
            else if (arg.StartsWith("--paint-decal=")) _paintDecalOverride = ParsePaintDecals(arg["--paint-decal=".Length..]);
            else if (arg.StartsWith("--paint-seed=")) { _paintSeed = ulong.Parse(arg["--paint-seed=".Length..]); _paintSeedExplicit = true; }
            else if (arg.StartsWith("--rof=")) _rofPath = arg["--rof=".Length..];
            else if (arg == "--debug-scoreboard") _debugScoreboard = true;
            else if (arg == "--debug-livery") _debugLivery ??= 0;
            else if (arg.StartsWith("--debug-livery=")) _debugLivery = int.Parse(arg["--debug-livery=".Length..]);
            else if (arg == "--debug-mesh") _debugMesh ??= "";
            else if (arg.StartsWith("--debug-mesh=")) _debugMesh = arg["--debug-mesh=".Length..];
            else if (arg == "--debug-names") _debugNames ??= "meshes";
            else if (arg.StartsWith("--debug-names=")) _debugNames = arg["--debug-names=".Length..];
            else if (arg == "--markers") { _markersOverlay = true; _viewerMode = true; hasContentArg = true; }
            else if (arg == "--dump-markers") _dumpMarkers = true;
            else if (arg.StartsWith("--dump-markers=")) { _dumpMarkers = true; _dumpMarkersPlane = arg["--dump-markers=".Length..]; }
            else if (arg == "--dump-weapons") _dumpWeapons = true;
            else if (arg.StartsWith("--dump-weapons=")) { _dumpWeapons = true; _dumpWeaponsFilter = arg["--dump-weapons=".Length..]; }
            else if (arg == "--dump-loadout") _dumpLoadout = true;
            else if (arg.StartsWith("--dump-loadout=")) { _dumpLoadout = true; _dumpLoadoutFilter = arg["--dump-loadout=".Length..]; }
            // Builds the chapter world (via the freecam path) so a bound AnimRuntime exists, then
            // sweeps one destructible's HP after build; --freecam gives it the world without a plane.
            else if (arg == "--damage-test") { _damageTest = true; _freecam = true; hasContentArg = true; }
            else if (arg.StartsWith("--damage-test=")) { _damageTest = true; _damageTestFilter = arg["--damage-test=".Length..]; _freecam = true; hasContentArg = true; }
            else if (arg.StartsWith("--damage-hd=")) _damageHd = float.Parse(arg["--damage-hd=".Length..], System.Globalization.CultureInfo.InvariantCulture);
            else if (arg.StartsWith("--loadout=")) _loadoutOverride = arg["--loadout=".Length..];
            else if (arg == "--infinite-ammo") _infiniteAmmo = true;
            else if (arg == "--fire") _autoFire = true;
            else if (arg == "--fire-rockets") _autoFireRockets = true;
            else if (arg.StartsWith("--gun-select=")) _gunSelect = int.Parse(arg["--gun-select=".Length..]);
            else if (arg.StartsWith("--mission=")) _mission = arg["--mission=".Length..];
            else if (arg.StartsWith("--scenario=")) { _scenario = arg["--scenario=".Length..]; _scenarioExplicit = true; }
            else if (arg.StartsWith("--spawn=")) _spawnIndex = int.Parse(arg["--spawn=".Length..]);
            else if (arg.StartsWith("--spawn-at=")) _spawnAt = ParseVec3(arg["--spawn-at=".Length..]);
            else if (arg.StartsWith("--spawn-dir=")) _spawnDir = ParseVec3(arg["--spawn-dir=".Length..]);
            else if (arg.StartsWith("--sky-zone=")) { _skyZone = arg["--sky-zone=".Length..]; _skyZoneExplicit = true; }
            else if (arg.StartsWith("--data-root=")) { /* resolved before this loop — every base path derives from it */ }
            else if (arg.StartsWith("--gamez=")) { _gamezPath = arg["--gamez=".Length..]; _gamezOverridden = true; }
            else if (arg.StartsWith("--textures=")) { _texturesPath = arg["--textures=".Length..]; _texturesOverridden = true; }
            else if (arg.StartsWith("--zrdr=")) { _zrdrPath = arg["--zrdr=".Length..]; _zrdrOverridden = true; }
            else if (arg.StartsWith("--interp=")) _interpPath = arg["--interp=".Length..];
            else if (arg.StartsWith("--sounds=")) { _soundsPath = arg["--sounds=".Length..]; _soundsOverridden = true; }
            else if (arg.StartsWith("--messages=")) _messagesPath = arg["--messages=".Length..];
            else if (arg == "--no-fog") _noFog = true;
            else if (arg == "--no-focus") _noFocus = true;
            else if (arg == "--mute") _mute = true;
            else if (arg == "--debug-collision") _debugCollision = true;
            else if (arg.StartsWith("--players=")) { _players = int.Parse(arg["--players=".Length..]); playersExplicit = true; }
            else if (arg.StartsWith("--hold=")) _holdSets = ParseHold(arg["--hold=".Length..]);
            else if (arg.StartsWith("--frames=")) _screenshotFrames = int.Parse(arg["--frames=".Length..]);
            else if (arg.StartsWith("--shots=")) _screenshotShots = Math.Max(1, int.Parse(arg["--shots=".Length..]));
            else if (arg.StartsWith("--jitter=")) _jitterDeg = float.Parse(arg["--jitter=".Length..], System.Globalization.CultureInfo.InvariantCulture);
            else if (arg.StartsWith("--screenshot=")) { _screenshotPath = arg["--screenshot=".Length..]; hasContentArg = true; }
            else if (arg.StartsWith("--yaw=")) _argYaw = float.Parse(arg["--yaw=".Length..], System.Globalization.CultureInfo.InvariantCulture);
            else if (arg.StartsWith("--pitch=")) _argPitch = float.Parse(arg["--pitch=".Length..], System.Globalization.CultureInfo.InvariantCulture);
            else if (arg.StartsWith("--campos=")) _camPos = ParseVec3(arg["--campos=".Length..]);
            else if (arg.StartsWith("--lookat=")) _lookAt = ParseVec3(arg["--lookat=".Length..]);
        }

        // Don't steal the user's foreground focus. Screenshot mode always opts in (it renders a
        // few frames and quits, needing no input); --no-focus is the manual lever (e.g. --freecam
        // observation). Sets WS_EX_NOACTIVATE on Windows so the window won't hold or re-grab focus.
        // Rendering is unaffected — a non-minimized background window still composites, so the
        // capture stays valid. Set here (earliest we know the flags), before any world build.
        if (_noFocus || _screenshotPath != null)
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);

        // --stunt is free flight over the mission's danger zones: force the flight path and the
        // stunt_flying spawn list (unless the tester pinned another scenario for a specific spawn).
        if (_stunt)
        {
            _fly = true;
            if (!_scenarioExplicit)
                _scenario = "stunt_flying";
        }
        // --anim-lab is the animation debugger's stage: the chapter world under the lab's own
        // clock, with no flight controller. The most specific mode of all, so it wins outright
        // — combining it with a flight/viewer/spectator mode is a contradiction.
        if (_animLab && (_fly || _viewerMode || _freecam))
        {
            GD.Print("--anim-lab is the animation lab; ignoring --fly/--stunt/--viewer/--damage/--freecam");
            _fly = _stunt = _viewerMode = _damageLab = _freecam = false;
        }
        // --freecam is the spectator world view: not flight (no aircraft) and not the parked-
        // plane viewer. It is the most specific of the three modes, so it wins outright when
        // combined — asking for a plane-less world AND a plane is a contradiction either way.
        if (_freecam && (_fly || _viewerMode))
        {
            GD.Print("--freecam is a world view with no aircraft; ignoring --fly/--stunt/--viewer/--damage");
            _fly = _stunt = _viewerMode = _damageLab = false;
        }
        // Flight is the default for any content arg; --viewer opts out into the static
        // inspection view. Asking for both is a contradiction — the explicit --viewer wins,
        // since a bare --fly is now just the default spelled out.
        if (_viewerMode && _fly)
        {
            GD.Print("--viewer and --fly/--stunt are opposites (flight is the default); using --viewer");
            _fly = _stunt = false;
        }
        if (hasContentArg && !_viewerMode && !_freecam && !_animLab)
            _fly = true;
        // The static viewer shows a chapter world when asked for one, else the parked plane.
        // Flight, the spectator view and the anim lab always need the world built.
        _worldMode = _fly || _freecam || _animLab || (_viewerMode && _chapterGiven);
        // A --plane= list of several aircraft states the player count on its own (the
        // scripted-verification path: --fly --plane=player_bhawk,player_fury = a 2P session with
        // different planes); an explicit --players= still wins.
        if (!playersExplicit && _planeNames.Count > 1)
            _players = _planeNames.Count;
        // Splitscreen is a flight mode: it needs planes to fly. Clamp to the rig's
        // capacity and fall back to single player for any static/orbit view.
        _players = Mathf.Clamp(_players, 1, UI.SplitScreen.MaxPlayers);
        if (_players > 1 && !_fly)
        {
            GD.Print($"--players={_players} needs flight (nothing to fly in --viewer); using 1");
            _players = 1;
        }
        if (_damageLab && _worldMode)
        {
            GD.Print("--damage is the plane lab (use --viewer --plane without --chapter); ignoring");
            _damageLab = false;
        }
        // Burst captures dither the camera by default so z-fighting flickers across frames;
        // a single shot never jitters. --jitter=<deg> overrides (0 disables).
        if (_jitterDeg < 0f)
            _jitterDeg = _screenshotShots > 1 ? 0.15f : 0f;
        // Prefer the unpacked sibling folder from ExtractAssets.ps1 -Unzip when it exists (loose
        // JSON/PNG/WAV: no zip decompression at load). Base (chapter-independent) paths resolve now;
        // the chapter-dependent gamez/texture/mission paths resolve per-session in StartSession.
        _planesGamezPath = SessionPaths.PreferUnzipped(planesGamezPath);
        if (!_zrdrOverridden) _zrdrPath = SessionPaths.PreferUnzipped(_zrdrPath);
        if (!_soundsOverridden) _soundsPath = SessionPaths.PreferUnzipped(_soundsPath);

        // --dump-markers: a pure-data report (no world, no camera) — print the marker rig table(s)
        // and quit. Placed here, once planes.zbd's path is known, so it runs whether or not any
        // content arg was given; --headless makes it windowless.
        if (_dumpMarkers)
        {
            DumpMarkers();
            GetTree().Quit();
            return;
        }
        // --dump-weapons: the same pure-data pattern for the typed weapons.json reader (B11) —
        // dump every def and assert no key went unmapped.
        if (_dumpWeapons)
        {
            DumpWeapons();
            GetTree().Quit();
            return;
        }
        // --dump-loadout: bind each plane's stock loadout to its built model and report the
        // resolved gun groups + hardpoints (B12) — a missing marker is a loud error here.
        if (_dumpLoadout)
        {
            DumpLoadout();
            GetTree().Quit();
            return;
        }

        // Register the distance-fog global shader parameters SceneBuilder's world/aircraft
        // shader references, before any material using it is built. Defaults are a no-op
        // (nothing fades) — only --fly overrides them from the mission's weather.json below.
        RenderingServer.GlobalShaderParameterAdd("csky_fog_color",
            RenderingServer.GlobalShaderParameterType.Vec3, new Vector3(0.69f, 0.69f, 0.69f));
        RenderingServer.GlobalShaderParameterAdd("csky_fog_range",
            RenderingServer.GlobalShaderParameterType.Vec2, new Vector2(1e8f, 1e9f));
        RenderingServer.GlobalShaderParameterAdd("csky_fog_alt",
            RenderingServer.GlobalShaderParameterType.Vec2, new Vector2(1e8f, 1e9f));
        // The fullbright world's per-mission brightness from the weather's SUNLIGHT:
        // 1.0 = fullbright (no darkening) for static views / missions without weather; --fly
        // overrides it from WeatherState.WorldLight below.
        RenderingServer.GlobalShaderParameterAdd("csky_world_light",
            RenderingServer.GlobalShaderParameterType.Float, 1.0f);
        // The animated world's LIGHT_STATE point lights. Defaults to an empty set, so a session
        // with no lit animations renders exactly as it did before they existed.
        WorldLights.RegisterGlobals();

        // Gamepad hotplug: every input read polls Pads.Connected() fresh, so a pad plugged in
        // mid-game works the moment the engine reports it. Log the roster at launch and every
        // connect/disconnect so a silent pad is diagnosable from the console.
        if (Pads.Disabled)
            GD.Print("gamepad: --no-pads, ignoring every device (keyboard/scripted input only)");
        else
            Input.Singleton.JoyConnectionChanged += (device, connected) =>
                GD.Print(connected
                    ? $"gamepad connected: device {device} \"{Input.GetJoyName((int)device)}\" guid={Input.GetJoyGuid((int)device)}"
                    : $"gamepad disconnected: device {device}");
        var padsAtLaunch = Pads.Connected();
        if (Pads.Disabled)
        {
            // nothing more to report — the roster is deliberately empty
        }
        else if (padsAtLaunch.Count == 0)
            GD.Print("gamepad: none at launch (hotplug live — connect any time)");
        else
            foreach (int p in padsAtLaunch)
                GD.Print($"gamepad: device {p} \"{Input.GetJoyName(p)}\" guid={Input.GetJoyGuid(p)} info={Input.GetJoyInfo(p)}");

        SetupLighting();
        _camera = new Camera3D { Fov = _fly || _freecam || _animLab ? 62 : 50, Far = 40000f };
        AddChild(_camera);
        _orbit = new OrbitCamera(_camera);
        if (_argYaw is { } argYaw) _orbit.Yaw = argYaw;
        if (_argPitch is { } argPitch) _orbit.Pitch = argPitch;

        // No content-selecting arg (or an explicit --menu): show the in-game launchscreen
        // (Mode → Chapter → Plane). Its selection fills in _chapter/_planeName/_stunt and calls
        // StartSession, so there is exactly one downstream build path. Esc from a menu-launched
        // flight returns here (see ReturnToMenu).
        if (_forceMenu || !hasContentArg)
        {
            _menuDriven = true;
            ShowLaunchMenu();
            return;
        }
        StartSession();
    }

    /// <summary>Builds one flight/view session from the current fields (mode, chapter, plane, spawn,
    /// …) into a fresh <see cref="_worldRoot"/> so Esc-to-menu can tear it all down and StartSession
    /// can run again — the launchscreen's in-process world rebuild. The camera, lights and global
    /// shader params live on <c>this</c> and persist across sessions. Returns true on success; false
    /// (leaving the partial _worldRoot for the caller to free) when the build threw.</summary>
    private bool StartSession()
    {
        _worldRoot = new Node3D { Name = "Session" };
        AddChild(_worldRoot);
        _camera.Fov = _fly || _freecam || _animLab ? 62 : 50;
        // One rig per rendered view, before anything camera-anchored is built (the skydome and
        // weather visuals below are per-rig). Single player reuses the main-viewport camera.
        BuildRigs(_fly ? _players : 1);

        // The build body reads the base paths as plain locals (unchanged from when this was inline
        // in _Ready); the chapter-dependent paths are recomputed here so a new launchscreen chapter
        // selection takes effect on rebuild.
        string dataRoot = _dataRoot, zrdrPath = _zrdrPath, soundsPath = _soundsPath,
            interpPath = _interpPath, messagesPath = _messagesPath, planesGamezPath = _planesGamezPath;
        bool mute = _mute, debugCollision = _debugCollision;

        string texturesPath = _texturesOverridden ? _texturesPath
            : SessionPaths.ChapterTextures(_dataRoot, _chapter);
        string gamezPath = _gamezOverridden ? _gamezPath
            : _worldMode ? SessionPaths.ChapterGamez(_dataRoot, _chapter)
            : _planesGamezPath;
        var missionZrdrPath = SessionPaths.MissionZrdr(_dataRoot, _chapter, _mission);

        // The anim lab keeps the session archives open (WorldSession.Options.KeepArchivesOpen):
        // the AnimLab node owns their disposal so puffers/decals can build at any playhead time.
        // Until that node exists, a failed lab build must close them from the catch below — in
        // every other mode they are `using` locals inside the try, as before.
        TextureArchive? labTextures = null;
        SoundArchive? labSounds = null;
        UI.AnimLab? animLab = null;
        try
        {
            var sw = Stopwatch.StartNew();
            var gamez = GameZ.Load(gamezPath);
            var textures = new TextureArchive(texturesPath);
            // The texture archive stays open past this build scope: the data-driven crash bakes its
            // effect puffers lazily at crash time (the same reason --anim-lab keeps it open). The lab
            // owns its copy (labTextures, freed with the lab node); every other mode hands it to the
            // session (_sessionTextures), which ReturnToMenu disposes on teardown so a map reload drops
            // the previous archive instead of leaking it. A failed build disposes it from the catch.
            if (_animLab)
            {
                labTextures = textures;
            }
            else
            {
                _sessionTextures = textures;
            }
            // Opened before the world build rather than with the flight audio below, because the
            // animation bootstrap builds the world's ambient SOUND_NODE emitters (the waterfall,
            // the train, the sirens) and needs the archive while it runs. Same lifetime rule as
            // the puffer factory: the decoded streams outlive this scope, the zip handle does not.
            bool haveSounds = !mute && (File.Exists(soundsPath) || Directory.Exists(soundsPath));
            var sounds = haveSounds ? new SoundArchive(soundsPath) : null;
            using var soundsScope = _animLab ? null : sounds;
            labSounds = _animLab ? sounds : null;
            var soundDefs = haveSounds ? SoundDefs.Load(zrdrPath) : null;
            if (!mute && !haveSounds)
                GD.PushWarning($"sound archive not found, flying silent: {soundsPath}");
            int meshInstances;
            int colliders = 0;
            string what;
            Node3D? cloudDeck = null; // the world's cloudlayer overcast (copied per player below)
            // The world's merged anim program + SceneBuilder, captured for the per-player crash
            // runtime (Layer 2): the program holds player_crash_dirt + the effect defs, the scene
            // builds the effect-template roots from the world gamez.
            AnimProgram? crashProgram = null;
            SceneBuilder? worldScene = null;
            AnimRuntime? worldRuntime = null;   // the world's anim runtime — C23 routes weapon damage through it
            if (_worldMode)
            {
                // Build the world (world1) and bind its animation program: WorldBuilder, clutter,
                // mission setup, AnimProgram + the AnimRuntime collaborator wiring, bind, sound
                // prewarm — extracted to WorldSession so --anim-lab builds the same
                // world+runtime. The per-view steps below (unplaced watch, edge extender, per-rig
                // horizon + weather) stay here and read the returned builder. WorldSession does NOT
                // add its Root to the tree — _worldRoot.AddChild(_plane) below still owns that.
                var session = WorldSession.Build(
                    new WorldSession.Options
                    {
                        DataRoot = dataRoot,
                        Chapter = _chapter,
                        Mission = _mission,
                        ZrdrPath = zrdrPath,
                        InterpPath = interpPath,
                        MissionZrdrPath = missionZrdrPath,
                        EffectsParent = _worldRoot!,
                        // PLAYER_RANGE conditions + the sound listener measure from player 1's
                        // camera — the honest answer in every mode (chase cam, free camera, orbit
                        // eye); resolved per call because none of those cameras exist yet here.
                        PlayerPosition = () => (_rigs.Count > 0 ? _rigs[0].Camera : _camera) is { } cam
                            ? cam.GlobalPosition
                            : Vector3.Zero,
                        // The damage-test needs the collidable world (its C25 census measures which
                        // destructible geometry is solid and whether death removes it) even though it
                        // runs in the freecam (non-fly) harness.
                        Collision = _fly || _damageTest,
                        DebugAnim = _debugAnim,
                        AnimLod = _animLod,
                        DebugDzPaths = _debugDzPaths,
                        // The lab: quiet stage (ambient playback deferred to its A toggle),
                        // pinned RNG, archives kept open for interactive effect builds.
                        KeepArchivesOpen = _animLab,
                        AutoStart = !_animLab,
                        RuntimeSeed = _animLab ? _labSeed : null,
                    },
                    gamez, textures, sounds, soundDefs);
                _plane = session.Root;
                var builder = session.Builder;
                cloudDeck = session.CloudDeck;
                // Owned by the session so a teardown drops the previous world's lights.
                _worldLights = session.Lights;
                crashProgram = session.Program;
                worldScene = session.Builder.Scene;
                worldRuntime = session.Runtime;

                // --damage-test: with the world built and its AnimRuntime bound, drive one
                // destructible's HP through its DAMAGE_SEQUENCE stages and quit — the C22 verify
                // stand-in until F40's interactive HP control lands. The world subtree is added to
                // the tree (ManualAdvance so _Process doesn't double-drive) because C26 ticks the
                // death sequences forward, which reads global transforms — invalid on an out-of-tree
                // node (`!is_inside_tree()` spam). It also makes the C25 collider positions real.
                if (_damageTest)
                {
                    _worldRoot!.AddChild(_plane);
                    session.Runtime.ManualAdvance = true;
                    RunDamageTest(session.Runtime);
                    GetTree().Quit();
                    return false;
                }

                // Every mechanism that places or hides a world entity has now run (the mission's
                // interp setup script as bootstrap pass 0, then the ON_STARTUP definitions), so
                // anything still sitting on the world origin is content this mission never placed
                // — the chapter build script parks every vehicle there (see
                // WorldBuilder.HideUnplacedEntities). Retail data leaves a few switched on:
                // C5/IA1's piratezep is the reported "zeppelin buried in the ground".
                // Switched off immediately so none of it is ever seen; _Process then polls
                // RestorePlacedEntities, which puts back anything a time-based motion moves.
                _unplacedWatch = builder;
                if (builder.HideUnplacedEntities() is { Count: > 0 } unplaced)
                {
                    GD.Print($"world: {unplaced.Count} unplaced entit(y/ies) left at the origin, "
                             + "switched off: " + string.Join(", ", unplaced));
                }

                // Map-edge continuation: a rolling window of mirrored terrain tiles (WITH the
                // chapter's clutter) that follows the plane past the map boundary, so the world
                // continues indefinitely under the fog like the original's tile-reload grid
                // (see MapEdgeExtender). On in --fly and in static weathered views (--sky-zone,
                // for edge-verification shots); off for plain orbit viewing (honest data view).
                if (_fly || _freecam || _skyZoneExplicit)
                {
                    _edgeExtender = builder.CreateEdgeExtender(session.Clutter);
                    if (_edgeExtender != null)
                    {
                        _plane.AddChild(_edgeExtender);
                        GD.Print("map edge: rolling mirrored-tile window active");
                    }
                }
                if (_fly || _freecam || _skyZoneExplicit)
                {
                    // The original skydome, anchored to the camera each frame. Scaled up so
                    // plain depth testing keeps it behind everything: a camera-centered dome
                    // looks identical at any scale (zero parallax), and at 2.5× (~22 km
                    // radius) it is beyond the farthest terrain (~17.4 km corner-to-corner)
                    // while well inside the camera's 40 km far plane. In static --chapter mode
                    // only an explicit --sky-zone adds it (an outside orbit view is better
                    // without the enclosing dome; with --campos inside the map it works).
                    // One dome per rig: it follows *a* camera, so each splitscreen pane needs
                    // its own on that player's visual layer.
                    //
                    // The mission's weather.json is loaded FIRST because it owns the zone
                    // table: it is what resolves --sky-zone's default against the zones this
                    // chapter actually ships (C5 has zone1+zone3, not zone2),
                    // and the dome must be built for the same zone the fog comes from.
                    LoadWeather(missionZrdrPath);
                    foreach (var rig in _rigs)
                    {
                        var dome = builder.BuildHorizon(_activeZone);
                        if (dome == null)
                            break;
                        dome.Scale = Vector3.One * HorizonScale;
                        if (rig.VisualLayer != 0)
                            UI.SplitScreen.SetVisualLayer(dome, rig.VisualLayer);
                        _worldRoot!.AddChild(dome);
                        rig.Horizon = dome;
                    }
                    // Weather (the flown mission's weather.json): distance fog for the rendered
                    // zone + the cloud-band whiteout + the ambient cloud puffs. Applied whenever
                    // the world+dome are shown — in --fly, and in static --chapter when --sky-zone
                    // is given (deterministic fog/whiteout/puff verification with --campos, same as
                    // the sky-verification path).
                    SetupWeather(textures);
                }
                meshInstances = builder.MeshInstanceCount;
                colliders = builder.ColliderCount;
                // Read after the domes, since C1's daytime sky layer is a horizon child.
                if (builder.ScrollingModelCount > 0)
                    GD.Print($"texture scroll: {builder.ScrollingModelCount} model(s) animating UVs");
                what = $"chapter {_chapter} world";

                // The animation debugger (--anim-lab): the lab node owns the clock and the
                // transport; the world above is its quiet stage (AutoStart=false — reset states
                // and mission setup applied, nothing playing until A or --play-anim).
                if (_animLab)
                {
                    // The lab feeds Advance itself in fixed 1/60 s steps. ManualAdvance, not
                    // SetProcess(false): Godot re-enables processing at READY for nodes that
                    // override _Process, and the runtime enters the tree (with _plane, below)
                    // after this line — a SetProcess here is silently undone and the world runs
                    // at 2× (fixed steps + wall dt). See AnimRuntime.ManualAdvance.
                    session.Runtime.ManualAdvance = true;

                    // Build the lab's effect/crash stage under ONE staging node the lab moves in
                    // front of the camera: the effect-template roots WorldBuilder skips (the
                    // fireball/spark/smokeball/fire/trail/dirt roots the world never renders
                    // ambiently), plus a meshless player crash-anchor set (healthy/destroyed/pieces
                    // the crash def targets). Indexed so a played crash/effect def resolves its
                    // puffer hosts AND its healthy/destroyed anchors HERE — scoped to this subtree —
                    // instead of onto one of C1's 217 generic 'healthy' world nodes. With
                    // PlaceCalledTemplates on, a CALL_ANIMATION relocates the called template onto
                    // the (in-front-of-camera) call site. IndexStage runs the reset states, hiding
                    // the templates.
                    var labStage = new Node3D { Name = "lab_stage_anchor" };
                    int effectRoots = BuildEffectStage(gamez, session.Builder.Scene, labStage);
                    labStage.AddChild(BuildCrashAnchorSet());
                    session.Root.AddChild(labStage);
                    session.Runtime.IndexStage(labStage);
                    session.Runtime.PlaceCalledTemplates = true;
                    GD.Print($"anim-lab: stage — {effectRoots} effect template(s) + player anchor set built + indexed");

                    // The spawn the mission would place the player at — the camera starts here so
                    // the interesting part of the map is in view, and (below) an optional parked
                    // plane sits on it. Resolved once so the camera and plane agree.
                    var labSpawns = SpawnPoints.LoadIa(missionZrdrPath, _scenario);
                    var (spawnPos, spawnLook) = ChooseSpawn(labSpawns, missionZrdrPath,
                        ChooseSpawnBase(labSpawns), 0, "");

                    // Camera: the freecam SpectatorCamera (RMB look, WASD/QE move), like --freecam,
                    // in place of the orbit view — the lab drives it (Frame/FollowNode) on
                    // play/pick. Starts at the mission spawn; --campos/--lookat override.
                    var camPos = _camPos ?? spawnPos;
                    var camLook = _lookAt ?? spawnLook;
                    var labCam = new SpectatorCamera(_camera, camPos, camLook) { ShowReadout = false };
                    _worldRoot!.AddChild(labCam);
                    _spectator = labCam;

                    // Optional stage prop: --plane= parks that aircraft at the mission spawn
                    // point. No FlightController — unpainted by default like every static view
                    // (--paint still applies one).
                    if (_planeNames.Count > 0)
                    {
                        var planesGamez = GameZ.Load(planesGamezPath);
                        var parkedBuilder = new PlaneBuilder(planesGamez, textures,
                            scheme: SchemeFor(0, zrdrPath, randomByDefault: false, NewPaintRng(),
                                PatternsForPlane(planesGamez, _planeName)),
                            patterns: Patterns);
                        var parked = parkedBuilder.Build(_planeName);
                        meshInstances += parkedBuilder.MeshInstanceCount;
                        _worldRoot!.AddChild(parked);
                        parked.Position = spawnPos;
                        if ((spawnLook - spawnPos).LengthSquared() > 1e-6f)
                        {
                            parked.LookAtFromPosition(spawnPos, spawnLook, Vector3.Up);
                        }
                        what += $" + parked '{_planeName}'";
                    }

                    animLab = new UI.AnimLab(session.Runtime, session.Program, labCam, session.Root,
                        labStage, textures, sounds, _labSeed, _playAnim,
                        autoFrame: _camPos == null && _lookAt == null,
                        fixedFrameStep: _screenshotPath != null)
                    {
                        // Interactive shows the whole lab UI; a scripted --screenshot hides it so
                        // the 3D shot stays byte-identical — unless --debug-anim-ui forces it on
                        // to capture the timeline (the same convention as --debug-livery).
                        ShowUi = _screenshotPath == null || _debugAnimUi,
                    };
                    _worldRoot!.AddChild(animLab);
                    GD.Print($"anim-lab: quiet stage, seed {_labSeed}, fixed dt 1/60"
                             + (_playAnim != null ? $", playing '{_playAnim}'" : "")
                             + " — freecam (RMB look, WASD/QE move); transport on the button panel,"
                             + " P pause · . step · R restart · F picker; click an object to follow");
                    what += " + anim lab";
                }
            }
            else
            {
                // The damage lab needs the pdpN torn-skin panels the plain viewer skips.
                // Every --viewer session gets one now, so H always has something
                // to toggle; --damage only decides whether it opens straight away. Built
                // hidden otherwise, and the panels are built hidden regardless, so a plain
                // --viewer still renders byte-identically.
                // Static views build unpainted unless --paint asks (randomByDefault: false),
                // so every existing orbit/damage screenshot renders exactly as before.
                // Resolved once: the livery lab below opens on exactly the scheme the plane
                // wears, not a second roll of --paint=random.
                var staticPatterns = PatternsForPlane(gamez, _planeName);
                var staticScheme = SchemeFor(0, zrdrPath, randomByDefault: false, NewPaintRng(), staticPatterns);
                // In --viewer the LIVERY LAB owns the livery and applies it itself, so the
                // model is built bare and there is one write path for paint (its Repaint).
                // Everywhere else the builder paints at construction as usual.
                var builder = new PlaneBuilder(gamez, textures, damagePanels: _viewerMode,
                    scheme: _viewerMode ? null : staticScheme, patterns: Patterns);
                _plane = builder.Build(_planeName);
                meshInstances = builder.MeshInstanceCount;
                what = $"'{_planeName}'";

                // Damage lab: per-part HP sliders driving the item-10c damage visuals on the
                // parked plane — the same DamageVisuals/puffer pipeline as flight, with the
                // distance-interval trails burning in place (DamageLab). Present in every
                // --viewer session (H), opened at launch only by --damage.
                if (_viewerMode)
                {
                    var stats = PlaneStats.Load(zrdrPath, _planeName);
                    if (stats.DestroyableParts.Count == 0)
                    {
                        GD.Print($"damage lab: '{_planeName}' ({stats.DefName}) has no destroyable_parts");
                    }
                    else
                    {
                        var smoke = MakePuffer(zrdrPath, textures, _worldRoot!, "pufftrails.json", "smokepuffer");
                        var fire = MakePuffer(zrdrPath, textures, _worldRoot!, "pufftrails.json", "firepuffer");
                        var panelTrails = new List<Effects.Puffer>();
                        for (int i = 0; i < 8; i++) // pool one per pdp panel — the lab can flip all of them
                            if (MakePuffer(zrdrPath, textures, _worldRoot!, "pufftrails.json", "firepuffer") is { } pt)
                                panelTrails.Add(pt);
                        var visuals = new DamageVisuals(builder.DamagePanels, _plane, stats, smoke, fire, panelTrails);
                        // the HUD gauge cluster as a lab toggle (user request): the damage
                        // dial mirrors the sliders, blinks on decreases like a flight hit
                        var labGauges = GaugeCluster.Build(gamez, _planeName, textures, stats.DestroyableParts);
                        _worldRoot!.AddChild(new DamageLab(stats, visuals, _plane, _damagePreset, labGauges)
                        {
                            StartHidden = !_damageLab, // --damage opens it; plain --viewer waits for H
                        });
                        GD.Print($"damage lab: {stats.DestroyableParts.Count} part sliders, " +
                                 $"{visuals.PanelCount} panels, {panelTrails.Count} panel fire trails"
                                 + (_damageLab ? "" : " (hidden — H)"));
                        what += _damageLab ? " + damage lab" : " + damage lab (H)";
                    }
                }

                // Livery lab (--viewer): pattern / RGB colour sliders / decal slots, repainting
                // the parked plane live via PlaneBuilder.Repaint. Built hidden-by-default state
                // is "unpainted" unless --paint named a scheme, so an unadorned --viewer
                // screenshot is byte-identical to the pre-paint viewer. L toggles it.
                if (_viewerMode && builder.SkinPrefix != null)
                {
                    var lab = new UI.LiveryLab(builder, PaintCatalog(zrdrPath), textures, staticScheme,
                        Patterns.PatternsFor(builder.SkinPrefix))
                    {
                        DebugShow = _debugLivery.HasValue,
                        DebugPatternSteps = _debugLivery ?? 0,
                    };
                    _worldRoot!.AddChild(lab);
                    what += " + livery lab";
                }

                if (_viewerMode)
                    what += " + mesh lab";
            }
            _worldRoot!.AddChild(_plane);
            // Mesh lab (--viewer): normals / wireframe+seams / zone boxes / lighting, plus live
            // cull-mode and normal-source overrides. Like the other two labs it is built in every
            // --viewer session and starts hidden (M), so an unadorned viewer screenshot is
            // unchanged; --debug-mesh opens it and presets modes. Built AFTER the plane joins the
            // tree: it reads geometry back through GlobalTransform, which on a detached node
            // returns identity and logs an error per call rather than walking the subtree.
            if (_viewerMode && _plane != null)
                _worldRoot!.AddChild(new UI.MeshLab(_plane, PlaneCollider.Build(_plane),
                    _sun, _env, _camera) { DebugSpec = _debugMesh });
            // Marker overlay (--viewer --plane, key K): the firepoint / pylon / target gizmos on the
            // parked aircraft (item A3). Only on the parked plane — a chapter world has no marker rig
            // — and after the plane joins the tree, since it reads each marker's GlobalPosition. Built
            // hidden unless --markers opened it, so an unadorned viewer screenshot is unchanged.
            if (_viewerMode && !_worldMode && _plane != null)
            {
                _worldRoot!.AddChild(new UI.MarkerOverlay(_plane) { StartHidden = !_markersOverlay });
                what += _markersOverlay ? " + marker overlay" : " + marker overlay (K)";
            }
            // The deck is now in the tree at its original position; remember its centre so
            // _Process can re-anchor it under each player every frame, and give every rig past
            // the first its own copy (the deck follows *a* camera — see AssignCloudDecks).
            if (cloudDeck != null)
            {
                _deckCenter = OrbitCamera.MergedAabb(cloudDeck).GetCenter();
                AssignCloudDecks(cloudDeck);
            }

            // Spectator mode (--freecam): the live world with no aircraft at all, observed from
            // a free-flying camera. It starts where the mission would have spawned the player
            // (or wherever --campos/--spawn-at put it), so the interesting part of the map is
            // already in view rather than a corner of empty sea.
            if (_freecam)
            {
                var freecamSpawns = SpawnPoints.LoadIa(missionZrdrPath, _scenario);
                var (camPos, camLookAt) = ChooseSpawn(freecamSpawns, missionZrdrPath,
                    ChooseSpawnBase(freecamSpawns), 0, "");
                if (_camPos is { } cp) camPos = cp;
                if (_lookAt is { } la) camLookAt = la;
                _spectator = new SpectatorCamera(_camera, camPos, camLookAt)
                {
                    // A scripted --screenshot run wants the frame clean of the overlay.
                    ShowReadout = _screenshotPath == null,
                };
                _worldRoot!.AddChild(_spectator);
                what += " + freecam";
                GD.Print($"freecam: spectator camera at ({camPos.X:0}, {camPos.Y:0}, {camPos.Z:0}) — " +
                         "hold RMB to look, WASD/QE to move, Shift boost, wheel sets speed");
            }

            if (_fly)
            {
                // Session-wide flight data, loaded once and shared by every player: the aircraft
                // models' gamez, the plane's stats, the sound defs/archive. Only the built nodes
                // and the per-plane state below are per player.
                var planesGamez = GameZ.Load(planesGamezPath);
                // Stats are per plane, not per player (splitscreen players can pick
                // different aircraft) — load each distinct one once, logging it as it appears.
                var statsCache = new Dictionary<string, PlaneStats>();
                PlaneStats StatsFor(string plane)
                {
                    if (statsCache.TryGetValue(plane, out var cached))
                        return cached;
                    var loaded = PlaneStats.Load(zrdrPath, plane);
                    statsCache[plane] = loaded;
                    GD.Print($"flight stats [{loaded.DefName}]: fd_speed={loaded.FdSpeed} m/s " +
                             $"weight={loaded.VehWeight} engine={loaded.EnginePower:0.00} " +
                             $"torques=({loaded.PitchTorque},{loaded.RollTorque},{loaded.RudderTorque})");
                    return loaded;
                }
                // Splitscreen: several own-ship engine stacks in one mix — equal-power scale them.
                float mixGain = 1f / Mathf.Sqrt(_rigs.Count);
                // The launchscreen's join flow binds the pads; a CLI launch derives them
                // from the connected roster instead.
                var padAssignment = _menuPads ?? AssignPads(_rigs.Count);
                if (_menuPads != null)
                    LogPads(_menuPads);
                // One livery RNG for the session, so P1..P4 draw distinct colours from one
                // stream and --paint-seed reproduces the whole field.
                var paintRng = NewPaintRng();
                // One spawn list for the session; each player takes the next index (wrapping).
                var spawnList = SpawnPoints.LoadIa(missionZrdrPath, _scenario);
                int spawnBase = ChooseSpawnBase(spawnList);

                // Weapons (M3 wave B): the typed weapons.json catalogue + the stock loadouts, loaded
                // once, and ONE shared projectile/effect pool every player's guns fire into
                // (projectiles live in the shared world, so every splitscreen pane sees them). The
                // pool reuses the session texture/sound archives (tracer/muzzle textures, impact sounds)
                // and the world gamez + its SceneBuilder, so rockets instance their FLYOUT MODEL body
                // (`he_rocket` …) from the chapter's own prototype roots (B14).
                var weaponDefs = WeaponDefs.Load(zrdrPath, Messages.Load(messagesPath));
                var stockLoadouts = StockLoadouts.Load();
                var projectiles = new ProjectilePool(textures, sounds, soundDefs,
                    flyoutGamez: gamez, flyoutScene: worldScene)
                {
                    Listener = _rigs.Count > 0 ? _rigs[0].Camera : _camera,
                    // Route weapon hits to the world's destructibles (C23): the pool's raycast
                    // reports the struck collider, the runtime resolves it to a destructible and
                    // spends the weapon's HEALTH_DAMAGE. Null runtime ⇒ impacts stay cosmetic.
                    DamageSink = worldRuntime != null ? worldRuntime.DamageAt : null,
                };
                _worldRoot!.AddChild(projectiles);

                // Stunt run: the mission's danger-zone objectives from ia.json
                // dzones, positions resolved against this chapter world's gamez, display strings
                // from targets.json → messages.json. --stunt only. Parsed ONCE for the session —
                // every player then races an independent copy of the same zone list, so the
                // archives are read once no matter how many pilots are in.
                StuntMission? stuntZones = null;
                StuntRace? race = null;
                if (_stunt)
                {
                    stuntZones = StuntMission.Load(gamez, missionZrdrPath, Messages.Load(messagesPath));
                    if (stuntZones == null)
                        // Expected for the chapters whose IA1 has no dzones (C1C, C2B) — a data
                        // fact, not a fault, so a plain line (log hygiene: no stack traces).
                        GD.Print($"--stunt: no danger zones for {_chapter}/{_mission} — flying free");
                    else if (_rigs.Count > 1)
                        race = new StuntRace(); // splitscreen: a race, ranked on the shared board
                }

                for (int pi = 0; pi < _rigs.Count; pi++)
                {
                    var rig = _rigs[pi];
                    bool verbose = pi == 0; // the per-plane detail lines are identical for every player
                    string tag = _rigs.Count > 1 ? $"P{pi + 1} " : "";
                    // Each player flies their own pick (the launchscreen's join flow / a --plane= list);
                    // with one name given, that is the same plane for everyone as before.
                    string planeName = PlaneFor(pi);
                    var stats = StatsFor(planeName);

                    // Flight repaints the field on every map load: each player draws their
                    // own random livery (colours + decals) unless --paint pins one.
                    var planeBuilder = new PlaneBuilder(planesGamez, textures, spinningProps: true,
                        scheme: SchemeFor(pi, zrdrPath, randomByDefault: false, paintRng,
                            PatternsForPlane(planesGamez, planeName)),
                        patterns: Patterns);
                    var planeModel = planeBuilder.Build(planeName);
                    meshInstances += planeBuilder.MeshInstanceCount;

                    var controller = new FlightController
                    {
                        // one scripted sequence per player ('|'-separated); the last covers the rest
                        HoldSegments = _holdSets == null ? null
                            : _holdSets[Math.Min(pi, _holdSets.Length - 1)],
                        DebugCollision = debugCollision,
                        PlaneModel = planeModel,
                        Props = PropAnimator.Build(planeModel), // spin the propeller/rotor blur discs
                        WingLights = WingLightBlinker.Build(planeBuilder.WingFlares), // blink the wingtip flares
                        Surfaces = ControlSurfaceAnimator.Build(planeModel), // deflect ailerons/elevators/rudders
                        Collider = PlaneCollider.Build(planeModel), // swept airframe boxes (wingtip/tail collision)
                        // per-part HP from destroyable_parts — collisions below
                        // the crash threshold damage the struck part instead of crashing
                        Damage = stats.DestroyableParts.Count > 0 ? new PlaneDamage(stats.DestroyableParts) : null,
                        // C27: flying into a WeaponOrCollideHit object (the 44 facades/windows/agyrobus)
                        // breaks it and passes through; every other collision stays solid.
                        CollideDamageSink = worldRuntime != null ? worldRuntime.CollideDamageAt : null,
                        // splitscreen: this player's own device(s), own pane for the HUD,
                        // and no debug freeze (it would halt the shared world for everyone)
                        PadDevices = padAssignment?[pi],
                        UseKeyboard = pi == 0,
                        HudParent = rig.Viewport,
                        AllowPause = _rigs.Count == 1,
                    };
                    controller.AddChild(planeModel);

                    // Guns/hardpoints: bind this plane's stock loadout (or the --loadout override) to
                    // its built model — resolves markers to muzzle nodes + weapons to WeaponDefs.
                    // Set before the controller enters the tree (its _Ready builds the fire state).
                    var loadoutDefName = _loadoutOverride ?? stats.DefName;
                    if (stockLoadouts.For(loadoutDefName) is { } ldef)
                    {
                        try
                        {
                            controller.Loadout = Loadout.Bind(ldef, planeModel, weaponDefs);
                            controller.Projectiles = projectiles;
                            controller.InfiniteAmmo = _infiniteAmmo;
                            controller.AutoFire = _autoFire;
                            controller.AutoFireRockets = _autoFireRockets;
                            controller.InitialGunSelect = _gunSelect;
                            if (verbose)
                            {
                                int groups = 0;
                                foreach (var _ in controller.Loadout.FirableGuns) { groups++; }
                                GD.Print($"weapons: {groups} gun group(s), {controller.Loadout.Hardpoints.Count} " +
                                         $"hardpoint(s), guns=Space/pad-B rockets=F/pad-A, " +
                                         $"select guns=G/dpad-L rockets=H/dpad-R" +
                                         (_gunSelect != 0 ? $" [gun-select={_gunSelect}]" : "") +
                                         (_infiniteAmmo ? " (infinite ammo)" : ""));
                            }
                        }
                        catch (Exception e)
                        {
                            GD.PushWarning($"weapons: loadout bind failed for '{loadoutDefName}': {e.Message}");
                        }
                    }
                    else if (verbose)
                    {
                        GD.Print($"weapons: no stock loadout for '{loadoutDefName}' — unarmed");
                    }
                    if (verbose && controller.Props != null)
                        GD.Print($"props: {controller.Props.Count} spinning blur nodes");
                    if (verbose && controller.WingLights != null)
                        GD.Print($"wing lights: {controller.WingLights.Count} blinking flares");
                    if (verbose && controller.Surfaces != null)
                        GD.Print($"control surfaces: {controller.Surfaces.Count} deflecting nodes");
                    if (controller.Collider != null)
                    {
                        if (verbose)
                            GD.Print($"plane collider: {controller.Collider.Summary}");
                    }
                    else
                    {
                        GD.PushWarning("no airframe collision boxes — falling back to the center ray");
                    }
                    if (verbose && controller.Damage != null)
                    {
                        var partDescs = new List<string>();
                        foreach (var p in stats.DestroyableParts)
                            partDescs.Add($"{p.Name} {p.MaxHp:0}hp{(p.Critical ? "*" : "")}{(p.Engine ? " engine" : "")}");
                        GD.Print($"damage parts: {string.Join(", ", partDescs)} (* = critical)");
                    }

                    // The original's heading tape, rebuilt from the chapter's own HUD
                    // textures (compassticks2/compasstxt ship in every chapter's archive).
                    controller.Compass = CompassTape.Build(textures);
                    if (verbose && controller.Compass != null)
                        GD.Print("compass: heading tape from compassticks2/compasstxt");

                    // The cockpit dials (altimeter / speedometer / damage display), rebuilt
                    // from the plane's own gauges subtree in planes.zbd + the chapter's
                    // HUD textures (needle/lowalt/stall/<plane>_damage/hilite/hatchptrn).
                    controller.Gauges = GaugeCluster.Build(planesGamez, planeName, textures,
                        stats.DestroyableParts);
                    if (controller.Gauges != null)
                    {
                        var damage = controller.Damage;
                        if (damage != null)
                            controller.Gauges.PartFraction = name =>
                                damage.Parts.TryGetValue(name, out var s) ? s.Fraction : 1f;
                        if (verbose)
                            GD.Print("gauges: altimeter/speedometer/damage dial from the plane's gauges subtree");
                    }

                    // Visible damage: torn-skin panel flips + the low-HP smoke/fire
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
                        if (verbose)
                            GD.Print($"damage visuals: {controller.Visuals.PanelCount} panels, " +
                                     $"smoke={(smoke != null ? "on" : "off")} fire={(fire != null ? "on" : "off")}, " +
                                     $"{panelTrails.Count} panel fire trails");
                    }

                    // The data-driven crash rig is built AFTER the controller enters the tree
                    // (below), so the crash def's reset states read valid global transforms.

                    if (sounds != null && soundDefs != null)
                    {
                        var audio = new FlightAudio { MixGain = mixGain };
                        audio.Setup(sounds, soundDefs, stats);
                        controller.Audio = audio;
                        controller.AddChild(audio);
                        if (verbose)
                            GD.Print($"audio: engine={stats.EngineSound} whine={stats.WhineSound} " +
                                     $"rattle={stats.RattleSound}" +
                                     (mixGain < 1f ? $" (per-player mix gain {mixGain:0.00})" : ""));
                    }
                    // This player's stunt run: player 1 flies the loaded instance, everyone else an
                    // independent copy of the same zones — own progress, own clock.
                    if (stuntZones != null)
                    {
                        controller.Stunt = pi == 0 ? stuntZones : stuntZones.ForAnotherPlayer();
                        controller.Stunt.LogTag = tag; // "P2 " in a race — one shared world, four runs
                        controller.PlayerIndex = pi;
                        controller.DebugCompleteStunt = _debugScoreboard;
                        // The objective marker HUD, one per pane: projects that player's
                        // active danger zone through THEIR camera, with the edge arrow + clock
                        // bearing + run status.
                        controller.Marker = MarkerHud.Build(controller.Stunt, rig.Camera);
                        if (race != null)
                        {
                            // Racing: no per-player splits board — the shared ranked board
                            // below covers the whole window when the last pilot is in. The marker
                            // HUD shows this player's placing meanwhile.
                            race.Add(pi, controller.Stunt, PlaneDisplayName(stats));
                            controller.Race = race;
                            controller.Marker.Race = race;
                            controller.Marker.PlayerIndex = pi;
                        }
                        else
                        {
                            // Solo: the end-of-run scoreboard — per-zone splits + total +
                            // persisted best time, keyed chapter/mission/plane in
                            // user://stunt_scores.json (race totals are deliberately not recorded).
                            var scoreKey = $"{_chapter}/{_mission}/{planeName}";
                            controller.Scoreboard = StuntScoreboard.Build(controller.Stunt,
                                PlaneDisplayName(stats), $"{_chapter}   ·   {Humanize(_scenario)}",
                                ScoreStore.Load(), scoreKey);
                            GD.Print($"stunt scoreboard: splits + best time (key '{scoreKey}')");
                        }
                        if (verbose)
                        {
                            GD.Print("stunt marker HUD: projected marker + edge arrow + clock bearing");
                            what += $" [stunt: {controller.Stunt.TotalCount} zones]";
                        }
                    }

                    var (spawnPos, spawnLookAt) = ChooseSpawn(spawnList, missionZrdrPath, spawnBase, pi, tag);
                    controller.Setup(new FlightModel(stats), rig.Camera, spawnPos, spawnLookAt);
                    controller.Name = $"player{pi + 1}";
                    rig.Controller = controller;
                    _worldRoot!.AddChild(controller);

                    // Data-driven crash (Layer 2): a per-player crash AnimRuntime that PLAYS
                    // player_crash_dirt on a crash — the wreck breaking apart, the pieceN ballistics
                    // and every authored effect, from the compiled def. Built here, once the
                    // controller (and its plane model) are in the tree, so the crash def's reset
                    // states resolve valid global transforms. The standard (and only) crash path.
                    if (crashProgram != null && worldScene != null)
                    {
                        BuildFlightCrashRuntime(controller, planeBuilder, planeName, gamez,
                            worldScene, textures, crashProgram, verbose);
                    }
                }
                // The race's shared results board: one ranked row per player, over the
                // WHOLE window rather than inside a pane — the race ends for everybody at once — so
                // it goes on its own CanvasLayer above the splitscreen panes. Any player's R there
                // is a rematch, which restarts every plane, so it routes back through the session.
                if (race != null)
                {
                    var board = StuntRaceBoard.Build(race, $"{_chapter}   ·   {Humanize(_scenario)}",
                        exitsToMenu: _menuDriven);
                    var boardLayer = new CanvasLayer { Name = "race_board", Layer = 10 };
                    boardLayer.AddChild(board);
                    _worldRoot!.AddChild(boardLayer);
                    foreach (var rig in _rigs)
                        if (rig.Controller != null)
                            rig.Controller.RestartRace = () => RestartRace(race);
                    GD.Print($"stunt race: {_rigs.Count} pilots over {stuntZones!.TotalCount} danger zones, " +
                             "own progress + clock each, shared ranked board");
                }

                if (_rigs.Count > 1)
                {
                    var flown = new List<string>(_rigs.Count);
                    for (int pi = 0; pi < _rigs.Count; pi++)
                        flown.Add($"P{pi + 1} '{PlaneFor(pi)}'");
                    what += $" + splitscreen {string.Join(", ", flown)}";
                }
                else
                {
                    what += $" + '{_planeName}' flying";
                }
            }

            GD.Print($"loaded {what}: {gamez.Nodes.Count} gamez nodes, " +
                     $"{meshInstances} mesh instances, {colliders} colliders, " +
                     $"{Mech3.SceneBuilder.ClampedSurfaceTotal} uv-clamped surfaces, {sw.ElapsedMilliseconds} ms");
            if (_rigs.Count > 1)
                foreach (var rig in _rigs)
                    GD.Print($"view P{rig.Index + 1}: layer {Mathf.Log(rig.VisualLayer) / Mathf.Log(2) + 1:0} " +
                             $"cull 0x{rig.Camera.CullMask:X5}, sky={(rig.Horizon != null ? "own" : "none")} " +
                             $"deck={(rig.Deck != null ? "own" : "none")} " +
                             $"puffs={(rig.Puffs != null ? "own" : "none")} " +
                             $"whiteout={(rig.Whiteout != null ? "own" : "none")}");
            if (textures.MissingTextures.Count > 0)
                GD.Print($"[textures] {textures.MissingTextures.Count} referenced texture(s) absent from this install: " +
                         string.Join(", ", textures.MissingTextures));
        }
        catch (Exception e)
        {
            GD.PrintErr($"failed to load session: {e}");
            // A failed build: nothing yet owns the archives kept open past the build scope (the
            // AnimLab node in the lab case, ReturnToMenu's teardown otherwise), so dispose them here.
            if (animLab == null)
            {
                labTextures?.Dispose();
                labSounds?.Dispose();
            }
            _sessionTextures?.Dispose();
            _sessionTextures = null;
            if (_screenshotPath != null)
                GetTree().Quit(1);
            return false;
        }

        // Only the static views frame their subject; flight and the spectator camera (both
        // --freecam and --anim-lab) place their own eye (FrameCamera would yank the freecam back
        // to the world's AABB orbit).
        if (!_fly && !_freecam && !_animLab)
            FrameCamera();

        // Node-name labels (T) — in BOTH the viewer and flight: reading a misplaced object's
        // name off it as you fly past is the fast way to identify it. Covers the whole session
        // subtree, so it labels the world and the aircraft alike. Off until pressed (and it
        // builds nothing until then), so no screenshot changes. In splitscreen the selection
        // follows P1's camera; the labels themselves render in every pane.
        var flownPlanes = new List<Node3D>();
        foreach (var rig in _rigs)
            if (rig.Controller?.PlaneModel is { } flown)
                flownPlanes.Add(flown);
        _worldRoot!.AddChild(new UI.NodeLabels(_worldRoot!, _rigs.Count > 0 ? _rigs[0].Camera : _camera)
        {
            InitialMode = _debugNames == null ? UI.NodeLabels.Mode.Off : UI.NodeLabels.ParseMode(_debugNames),
            // The flown aircraft sits metres from the camera while the world is hundreds of
            // metres away, so without this it wins every label slot. Empty in --viewer, where
            // the parked aircraft IS the subject.
            Deprioritise = flownPlanes,
        });

        _inSession = true;
        return true;
    }

    /// <summary>Creates this session's <see cref="PlayerRig"/>s — one per rendered view.
    /// One player keeps PlaneViewer's own main-viewport camera and the default visual
    /// layers, so the single-player render path is byte-for-byte what it was. Two or more build
    /// the <see cref="SplitScreen"/> pane rig: the main camera stands down (the panes cover the
    /// screen) and each pane gets its own camera, culling every other player's private
    /// sky/deck/puff layer.</summary>
    private void BuildRigs(int count)
    {
        _rigs.Clear();
        _split = null;
        if (count <= 1)
        {
            _camera.Current = true;
            _rigs.Add(new PlayerRig { Index = 0, Camera = _camera, HudParent = _worldRoot!, VisualLayer = 0 });
            return;
        }

        _camera.Current = false; // the panes render the world now; nothing draws the main viewport's 3D
        _split = SplitScreen.Build(count, GetViewport());
        _worldRoot!.AddChild(_split);
        for (int i = 0; i < count; i++)
        {
            var view = _split.Views[i];
            var camera = new Camera3D
            {
                Name = $"camera{i + 1}",
                Fov = 62,
                Far = _camera.Far,
                CullMask = SplitScreen.PlayerCullMask(i),
                Current = true,
            };
            view.AddChild(camera);
            _rigs.Add(new PlayerRig
            {
                Index = i,
                Camera = camera,
                Viewport = view,
                HudParent = view,
                VisualLayer = SplitScreen.PlayerVisualLayer(i),
            });
        }
        GD.Print($"splitscreen: {count} panes sharing one world " +
                 $"({(count == 2 ? "stacked top/bottom" : "2×2 grid")})");
    }

    /// <summary>Gives every rig a cloudlayer deck to anchor under its own camera: rig 0 takes the
    /// world's own deck, the rest get copies alongside it, each moved onto its player's visual
    /// layer. Duplicating drops the SceneBuilder instance uniforms (they are RenderingServer
    /// instance state, not plain properties), so they are re-applied from the source.</summary>
    private void AssignCloudDecks(Node3D deck)
    {
        _rigs[0].Deck = deck;
        if (_rigs[0].VisualLayer != 0)
            SplitScreen.SetVisualLayer(deck, _rigs[0].VisualLayer);
        var parent = deck.GetParent();
        for (int i = 1; i < _rigs.Count; i++)
        {
            var copy = (Node3D)deck.Duplicate();
            copy.Name = $"cloud_deck{i + 1}";
            CopyInstanceShaderParams(deck, copy);
            SplitScreen.SetVisualLayer(copy, _rigs[i].VisualLayer);
            parent.AddChild(copy);
            _rigs[i].Deck = copy;
        }
    }

    // Node.Duplicate() copies plain properties but not per-instance shader parameters, which
    // SceneBuilder relies on for the coplanar draw order (node_bias) and the fog opt-outs. Walk
    // both trees in lockstep (Duplicate preserves child order) and re-apply them.
    // **This must list every instance uniform SceneBuilder can set**, or a duplicated subtree
    // silently renders with the shader's default instead of the value the source was given.
    // `csky_opacity` was once missing from this list.
    // ⚠ **That omission was LATENT, not live — the claimed symptom is disproven.** The claim
    // was that `OBJECT_OPACITY_STATE` poses cloud decks, so panes 2–4 would show an opaque deck
    // where pane 1 shows 0.6. Measured in a 2-player C1 session: the opacity-animated node is
    // `world1/g27816/l2586/cloudparent` (0.6, as C1's reader-only `clouds.zrd.json` authors), and
    // it sits in **`world1`, which every pane shares** — it is not duplicated at all. The subtree
    // this loop actually copies (WorldBuilder's flat `cloudlayer` deck) carries **no**
    // `csky_opacity` on any node, in any chapter. So nothing diverges today; the entry is here to
    // keep the list complete, and the ordering rule below is the reason completeness matters.
    private static readonly string[] InstanceShaderParams =
        { "node_bias", "csky_fog_on", "csky_light_fade", Mech3.SceneBuilder.OpacityParam };

    private static void CopyInstanceShaderParams(Node source, Node copy)
    {
        if (source is GeometryInstance3D from && copy is GeometryInstance3D to)
            foreach (var name in InstanceShaderParams)
            {
                var value = from.GetInstanceShaderParameter(name);
                if (value.VariantType != Variant.Type.Nil)
                    to.SetInstanceShaderParameter(name, value);
            }
        int n = Math.Min(source.GetChildCount(), copy.GetChildCount());
        for (int i = 0; i < n; i++)
            CopyInstanceShaderParams(source.GetChild(i), copy.GetChild(i));
    }

    /// <summary>Splits the connected gamepads across the players: P1 gets the first
    /// pad (plus the keyboard, wired separately), P2–P4 the next ones in roster order. Null for a
    /// single player — that keeps the any-pad reads, so every pad flies the one plane. A player
    /// with no pad left gets an empty list and simply sits still (logged) — P1 still has the
    /// keyboard, so a 2P session with no controller at all is still half-flyable.</summary>
    private static int[][]? AssignPads(int players)
    {
        if (players <= 1)
            return null;
        var pads = Pads.Connected();
        var assignment = new int[players][];
        for (int i = 0; i < players; i++)
            assignment[i] = i < pads.Count ? new[] { pads[i] } : Array.Empty<int>();
        LogPads(assignment);
        return assignment;
    }

    /// <summary>Log who flies what, for either source of the binding (the roster split above or
    /// the launchscreen's join flow) — a silent plane is otherwise hard to diagnose.</summary>
    private static void LogPads(int[][] assignment)
    {
        for (int i = 0; i < assignment.Length; i++)
        {
            var pads = new List<string>(assignment[i].Length);
            foreach (int pad in assignment[i])
                pads.Add($"pad {pad} \"{Input.GetJoyName(pad)}\"");
            GD.Print($"player {i + 1} input: {(i == 0 ? "keyboard" : "")}" +
                     (pads.Count > 0
                         ? $"{(i == 0 ? " + " : "")}{string.Join(" + ", pads)}"
                         : i == 0 ? "" : "NO DEVICE (connect a pad and relaunch)"));
        }
    }

    /// <summary>Shows the launchscreen (building it on first use) and wiring its Launch/Quit
    /// callbacks. Re-shown by <see cref="ReturnToMenu"/> after Esc-from-flight.</summary>
    private void ShowLaunchMenu()
    {
        if (_menu == null)
        {
            _menu = LaunchMenu.Build(_zrdrPath);
            _menu.Launch = StartSessionFromMenu;
            _menu.Quit = () => GetTree().Quit();
            AddChild(_menu);
        }
        _menu.ShowMenu(_menuStartScreen);
        // --debug-join=N: synthesize N extra device-less players so the splitscreen aircraft
        // select can be screenshot on a one-controller machine (they can never act, so the
        // shot is deterministic; the last one starts locked to show both panel states).
        if (_debugJoin > 0)
        {
            _menu.DebugJoin(_debugJoin);
            _debugJoin = 0; // one-shot: a return to the menu keeps whoever really joined
        }
    }

    /// <summary>The launchscreen's players locked their picks: fill the build fields — chapter,
    /// one plane per player, each player's pad — and start the session. On a build failure,
    /// return to the menu with a note rather than leave a blank screen.</summary>
    private void StartSessionFromMenu(string chapter, IReadOnlyList<LaunchMenu.PlayerChoice> players,
        bool stunt)
    {
        _chapter = chapter;
        _planeNames.Clear();
        foreach (var p in players)
            _planeNames.Add(p.PlaneNode);
        _planeName = _planeNames.Count > 0 ? _planeNames[0] : _planeName;
        _players = Mathf.Clamp(players.Count, 1, UI.SplitScreen.MaxPlayers);
        // Honour the join flow's device binding rather than re-deriving it from the roster: the
        // pad that joined as P2 in the menu must be the pad that flies P2. Single player keeps the
        // any-pad policy (null), so every connected pad flies the one plane, as before.
        _menuPads = null;
        if (_players > 1)
        {
            _menuPads = new int[_players][];
            for (int i = 0; i < _players; i++)
                _menuPads[i] = players[i].Pads;
        }
        _stunt = stunt;
        _fly = true;
        _worldMode = true;
        _viewerMode = false; // the menu always launches flight, even after a --viewer --menu launch
        // Derive the spawn scenario from the mode each rebuild (a previous stunt run may have left
        // it set), unless the tester pinned one with --scenario= alongside the bare launch.
        if (!_scenarioExplicit)
            _scenario = stunt ? "stunt_flying" : "zeppelin_run";
        _menu!.HideMenu();
        if (!StartSession())
        {
            ReturnToMenu();
            _menu.ShowError($"Could not load {chapter} / {string.Join(", ", _planeNames)} — see the log.");
        }
    }

    /// <summary>Rematch from the shared race board (R): every player's zones, clock and
    /// placing cleared, then every plane back to its own spawn — same chapter, aircraft and spawn
    /// points. The board retires itself once the placings are gone. The session owns the planes, so
    /// the restart lands here rather than in the FlightController that read the button.</summary>
    private void RestartRace(StuntRace race)
    {
        GD.Print("stunt race: rematch — fresh clocks and zones for every pilot");
        race.Restart();
        foreach (var rig in _rigs)
            rig.Controller?.Respawn();
    }

    /// <summary>Tears down the current session (frees <see cref="_worldRoot"/> and drops every cached
    /// session node) and shows the launchscreen again — the in-process rebuild path for
    /// Esc-from-flight and failed builds. The camera / lights / shader globals persist on
    /// <c>this</c>.</summary>
    private void ReturnToMenu()
    {
        if (_worldRoot != null)
        {
            _worldRoot.QueueFree();
            _worldRoot = null;
        }
        // Drop the cached session references so _Process (which may run once more before the
        // deferred QueueFree lands) skips them via its null guards. The rigs go with the session
        // (their cameras live in the freed SubViewports); the main-viewport camera is ours and
        // comes back on for whatever the next session builds.
        _plane = null;
        _precip = null;
        _edgeExtender = null;
        _weather = null;
        // Clears csky_light_count, so the next world does not inherit this one's spill for the
        // frame between teardown and the new runtime's first tick.
        _worldLights?.Dispose();
        _worldLights = null;
        // The session-owned texture archive, kept open past its build scope so the data-driven crash
        // could bake puffers lazily; drop it here so a map reload disposes the previous one.
        _sessionTextures?.Dispose();
        _sessionTextures = null;
        // Stop polling the torn-down world's origin-parked entities (their nodes are going away).
        _unplacedWatch = null;
        _unplacedRecheck = 0.0;
        _rigs.Clear();
        _split = null;
        _camera.Current = true;
        _inSession = false;
        ShowLaunchMenu();
    }

    /// <summary>Parse the scripted hold argument: '|' separates one sequence per player (the
    /// last one covers any remaining players, so the old single-sequence form still drives
    /// everyone), ';' separates that sequence's segments, each "pitch,roll,yaw,throttle" with an
    /// optional "@seconds" duration. The last segment (or one without a duration) holds forever —
    /// so the plain "--hold=p,r,y,thr" form keeps its old constant-input meaning.</summary>
    private static (FlightInput, float)[][] ParseHold(string s)
    {
        static float F(string v) => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
        var players = new List<(FlightInput, float)[]>();
        foreach (var perPlayer in s.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var segments = new List<(FlightInput, float)>();
            foreach (var seg in perPlayer.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var at = seg.Split('@');
                var p = at[0].Split(',');
                segments.Add((new FlightInput { Pitch = F(p[0]), Roll = F(p[1]), Yaw = F(p[2]), Throttle = F(p[3]) },
                              at.Length > 1 ? F(at[1]) : 0f));
            }
            players.Add(segments.ToArray());
        }
        return players.ToArray();
    }

    /// <summary>Parse --plane=: one node name, or a comma-separated list — one plane per player
    /// for splitscreen (the launchscreen's simultaneous pick produces the same list). The
    /// first entry stays <see cref="_planeName"/>, which every single-plane code path uses.</summary>
    private void ParsePlanes(string value)
    {
        _planeNames.Clear();
        foreach (var name in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
            _planeNames.Add(name.Trim());
        if (_planeNames.Count > 0)
            _planeName = _planeNames[0];
    }

    /// <summary>The plane player <paramref name="index"/> flies: their own pick when the
    /// launchscreen (or a --plane= list) gave one, else the last one named — so a single
    /// --plane= puts everybody in the same aircraft.</summary>
    private static Color[]? ParsePaintColors(string spec)
    {
        var parts = spec.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var outc = new Color[3];
        for (int i = 0; i < 3; i++)
        {
            var v = ParseVec3(parts[Math.Min(i, parts.Length - 1)]);
            outc[i] = PaintScheme.FromBytes((int)v.X, (int)v.Y, (int)v.Z);
        }
        return outc;
    }

    private static int[]? ParsePaintDecals(string spec)
    {
        var parts = spec.Split(',', StringSplitOptions.TrimEntries);
        var outd = new int[3];
        for (int i = 0; i < 3; i++)
            outd[i] = int.Parse(parts[Math.Min(i, parts.Length - 1)]);
        return outd;
    }

    /// <summary>The 12 named schemes shipped in vehicle.json, loaded once per session.
    /// Empty on a read failure — paint is cosmetic and must never block a build.</summary>
    private List<PaintScheme> PaintCatalog(string zrdrPath)
    {
        if (_paintCatalog != null)
            return _paintCatalog;
        try
        {
            _paintCatalog = PaintScheme.LoadCatalog(zrdrPath);
            GD.Print($"[paint] {_paintCatalog.Count} shipped patterns: "
                + string.Join(", ", _paintCatalog.ConvertAll(s => s.Pattern)));
        }
        catch (Exception e)
        {
            GD.Print($"[paint] vehicle.json paint catalog unavailable ({e.Message}) — flying unpainted");
            _paintCatalog = new List<PaintScheme>();
        }
        return _paintCatalog;
    }

    /// <summary>The livery player <paramref name="index"/> flies, or null to build the
    /// shipped unpainted skins. <paramref name="randomByDefault"/> is set for flight modes,
    /// where every player gets a fresh random livery on each map load unless --paint says
    /// otherwise; static views default to unpainted.</summary>
    /// <summary>The original's per-pattern paint region masks, scanned once per session from
    /// the extracted UI archive. Empty (and a one-line note) when ExtractRof.ps1 has not been
    /// run — aircraft then build unpainted rather than failing.</summary>
    private PatternLibrary Patterns => _patternLibrary ??= PatternLibrary.Load(_rofPath);

    /// <summary>The patterns this aircraft has masks for — the list the original's paint UI
    /// offers for that plane. Empty when the model carries no skin prefix to key on.</summary>
    private List<string> PatternsForPlane(GameZ planesGamez, string planeNode)
    {
        var root = planesGamez.FindByName(planeNode);
        var prefix = root != null ? PlanePainter.PrefixFor(planesGamez, root) : null;
        return prefix != null ? Patterns.PatternsFor(prefix) : new List<string>();
    }

    private static bool ContainsPattern(IReadOnlyList<string> list, string name)
    {
        foreach (var p in list)
            if (string.Equals(p, name, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private PaintScheme? SchemeFor(int index, string zrdrPath, bool randomByDefault, RandomNumberGenerator rng,
        IReadOnlyList<string>? available = null)
    {
        // --paint= takes one name per player like --plane=; the last covers any remainder.
        string? name = _paintNames is { Length: > 0 }
            ? _paintNames[Math.Min(index, _paintNames.Length - 1)]
            : null;

        // Resolve the no-paint cases before touching vehicle.json, so an unpainted static
        // view does no extra work and logs nothing (it is the pre-paint behaviour verbatim).
        if (string.Equals(name, "none", StringComparison.OrdinalIgnoreCase))
            return null;
        if (name == null && !randomByDefault)
            return null;

        var catalog = PaintCatalog(zrdrPath);
        PaintScheme? scheme;
        if (name == null || string.Equals(name, "random", StringComparison.OrdinalIgnoreCase))
        {
            // A pattern is per aircraft, so a random livery draws from the ones THIS plane
            // actually has masks for — picking one it does not carry would paint nothing.
            scheme = PaintScheme.Random(rng, catalog, available);
        }
        else
        {
            // Named: take the catalog's canonical colours when vehicle.json knows the pattern,
            // else a bare scheme (BROADWAY/ITSTAXI ship masks but no vehicle def names them).
            var known = catalog.Find(s => string.Equals(s.Pattern, name, StringComparison.OrdinalIgnoreCase)
                                       || string.Equals(s.FolderName, name, StringComparison.OrdinalIgnoreCase));
            bool haveMasks = available == null || available.Count == 0
                || ContainsPattern(available, name)
                || (known != null && ContainsPattern(available, known.FolderName));
            if (known == null && !haveMasks)
            {
                var offer = available is { Count: > 0 } ? string.Join(", ", available) : "(no pattern library)";
                GD.Print($"[paint] unknown pattern '{name}' — this aircraft has: {offer}, random, none");
                return null;
            }
            scheme = known ?? new PaintScheme { Pattern = name };
            if (!haveMasks)
                GD.Print($"[paint] pattern '{name}' ships no skins for this aircraft — "
                    + $"it has: {string.Join(", ", available!)}; painting decals only");
        }

        // An explicit colour/decal list overrides whatever the scheme brought, so a single
        // colour can be dialled in against a chosen pattern.
        if (scheme != null && (_paintColorOverride != null || _paintDecalOverride != null))
        {
            scheme = new PaintScheme
            {
                Pattern = scheme.Pattern,
                Color1 = _paintColorOverride?[0] ?? scheme.Color1,
                Color2 = _paintColorOverride?[1] ?? scheme.Color2,
                Color3 = _paintColorOverride?[2] ?? scheme.Color3,
                NoseDecal = _paintDecalOverride?[0] ?? scheme.NoseDecal,
                TailDecal = _paintDecalOverride?[1] ?? scheme.TailDecal,
                WingDecal = _paintDecalOverride?[2] ?? scheme.WingDecal,
            };
        }
        return scheme;
    }

    /// <summary>The RNG the session's random liveries draw from. Seeded from the clock so
    /// each map load repaints the field, or from --paint-seed for a reproducible run
    /// (scripted screenshots need the same aircraft colours every time).</summary>
    private RandomNumberGenerator NewPaintRng()
    {
        var rng = new RandomNumberGenerator();
        if (_paintSeedExplicit)
            rng.Seed = _paintSeed;
        else
            rng.Randomize();
        return rng;
    }

    private string PlaneFor(int index) =>
        _planeNames.Count == 0 ? _planeName : _planeNames[Math.Min(index, _planeNames.Count - 1)];

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

    /// <summary>A readable plane name for the stunt scoreboard from the vehicle.json def
    /// name — the player defs are "p&lt;name&gt;" (pbloodhawk, ppeacemaker, pfury, …), so strip the
    /// leading p and title-case → "Bloodhawk". Falls back to the node name. (Placeholder until the
    /// launchscreen gets a proper data-driven roster of display names.)</summary>
    private static string PlaneDisplayName(PlaneStats stats)
    {
        var d = stats.DefName;
        string name = d.Length > 1 && (d[0] == 'p' || d[0] == 'P') ? d[1..]
            : d.Length > 0 ? d
            : stats.NodeName;
        return Humanize(name);
    }

    /// <summary>"stunt_flying" → "Stunt Flying": underscores to spaces, each word title-cased.</summary>
    private static string Humanize(string s)
    {
        var words = s.Split(new[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
            words[i] = char.ToUpperInvariant(words[i][0]) + words[i][1..].ToLowerInvariant();
        return string.Join(' ', words);
    }

    // The crash/effect template roots (world-gamez nodes WorldBuilder skips, because the world
    // never renders them ambiently — they exist to be instanced onto a kill/crash site). Built into
    // the anim lab's stage so a played effect def resolves the puffer host that rides its own root.
    private static readonly string[] EffectTemplateRoots =
        { "yellow_spark_01", "flame_ball_01", "black_smoke_ball_01", "fire_here", "carnage_trails", "flydirt" };

    // The player crash-anchor set: meshless nodes named exactly the crash def's targets (its
    // anim_root 'player' plus healthy/destroyed/pieces). Built into the lab stage so a played crash
    // def anchors to this 'player' and resolves 'healthy'/'destroyed' HERE — locally, in front of
    // the camera — rather than falling through to one of the world's 217 generic 'healthy' nodes.
    // The effects (relocated templates) attach to these; the plane/wreck geometry is the crash
    // plan's Layer-2 work.
    private static readonly string[] CrashAnchorNodes =
        { "healthy", "destroyed", "dontmove", "markers", "piece1", "piece2", "piece3", "piece4", "shadow", "cockpit1" };

    /// <summary>Builds the <see cref="EffectTemplateRoots"/> from the world gamez as children of
    /// <paramref name="parent"/> (the lab stage), each reset to sit at the stage origin — a
    /// CALL_ANIMATION relocates them onto the call site. Returns how many built.</summary>
    private static int BuildEffectStage(GameZ gamez, SceneBuilder scene, Node3D parent)
    {
        int n = 0;
        foreach (var rootName in EffectTemplateRoots)
        {
            if (gamez.FindByName(rootName) is { } node && scene.BuildSubtree(node) is { } built)
            {
                built.Transform = Transform3D.Identity; // sit at the stage; reposition moves it on call
                parent.AddChild(built);
                n++;
            }
        }
        return n;
    }

    /// <summary>Builds the per-player crash runtime.
    /// Under a <c>player</c> crash root parented to the controller it builds the
    /// effect-template roots (from the world gamez) and the plane's real <c>destroyed</c> wreck
    /// subtree, then binds a NON-auto-start <see cref="AnimRuntime"/> to the <b>controller</b> — so
    /// the crash def resolves <c>healthy</c> (in the plane model), <c>destroyed</c>/<c>pieceN</c>
    /// (the wreck) and the effect hosts, all scoped to this one plane where every name is unique.
    /// <see cref="AnimRuntime.NameResolveFallback"/> handles the crash def's non-portable node
    /// ptrs (they index planes.zbd at slots this build never uses); reset states (run at bind) hide
    /// the wreck + templates until <see cref="FlightController.Crash"/> plays the def.</summary>
    private void BuildFlightCrashRuntime(FlightController controller, PlaneBuilder planeBuilder,
        string planeName, GameZ gamez, SceneBuilder worldScene, TextureArchive textures,
        AnimProgram crashProgram, bool verbose)
    {
        // The crash root: the def's "player" anim-root anchor. Sits in the plane model's frame so
        // the wreck subtree (built relative to the plane root) lands where the plane is (the wreck's
        // planePose × chain). The effect templates position by AT_NODE global, so the crash root's
        // own transform is irrelevant to them.
        var crashRoot = new Node3D { Name = "player" };
        crashRoot.SetMeta(AnimRuntime.NameMeta, "player");
        if (controller.PlaneModel != null)
            crashRoot.Transform = controller.PlaneModel.Transform;

        // Effect-template roots (world gamez nodes WorldBuilder skips) — one instance per player, so
        // splitscreen crashes do not collide. Hidden by their reset states at bind.
        int effectRoots = BuildEffectStage(gamez, worldScene, crashRoot);

        // The plane's destroyed wreck (pieceN meshes), built hidden; the crash def shows + flings it.
        var destroyed = planeBuilder.BuildDestroyed(planeName);
        var restPoses = new List<(Node3D, Transform3D)>();
        if (destroyed != null)
        {
            destroyed.Visible = false;
            crashRoot.AddChild(destroyed);
            // Every wreck node's rest pose, so respawn can re-home the flung pieces (a RESET_STATE
            // re-poses only what it names, and the pieces have no reset event).
            CollectRestPoses(destroyed, restPoses);
        }
        controller.AddChild(crashRoot);

        // The scoped crash runtime: no ambient start (nothing runs until the crash Plays the def),
        // puffers baked lazily via the session textures (kept open above), effect templates
        // relocated onto the call site, and the crash def's non-portable node ptrs resolved by name.
        // ⚠ PufferParent is the WORLD root, NOT the crash root: a PUFFER_STATE emitter goes TopLevel
        // (world-space) the moment it emits, and parenting it under the per-player controller subtree
        // left it drawn-but-unrendered (every particle correctly positioned, IsVisibleInTree true, yet
        // nothing on screen — measured). Parenting at world level, exactly like the world runtime's
        // own PufferFactory, renders it. Each crash runtime still makes its own emitter instances at
        // its own crash site, so splitscreen crashes stay independent.
        var crashRuntime = new AnimRuntime
        {
            AutoStart = false,
            DebugMotions = _debugAnim,
            PufferParent = _worldRoot,
            PufferFactory = st => Effects.Puffer.Create(st, textures, sustained: true),
            PlaceCalledTemplates = true,
            NameResolveFallback = true,
        };
        // Bind only the crash def's transitive CALL_ANIMATION closure (Subset), not the whole world
        // program: the full 800+ defs include ~150 generic-named world defs that would mis-anchor
        // onto this plane's parts and run their reset states on the aircraft.
        crashRuntime.Bind(controller, crashProgram.Subset("player_crash_dirt"));
        controller.AddChild(crashRuntime);
        controller.CrashRuntime = crashRuntime;
        controller.CrashAnchor = crashRoot;
        controller.CrashRestPoses = restPoses;
        // The plane model's built visibility, so respawn can undo the crash def's healthy/markers
        // hides (its RESET_STATE only restores dontmove). Captured pristine, before any crash.
        var planeVis = new List<(Node3D, bool)>();
        if (controller.PlaneModel != null)
            CollectVisibility(controller.PlaneModel, planeVis);
        controller.CrashPlaneVisibility = planeVis;
        if (verbose)
            GD.Print($"data-crash: {effectRoots} effect template(s) + {restPoses.Count} wreck node(s) — "
                     + "crash runtime bound (scoped, no auto-start)");
    }

    private static void CollectRestPoses(Node3D node, List<(Node3D, Transform3D)> into)
    {
        into.Add((node, node.Transform));
        foreach (var child in node.GetChildren())
            if (child is Node3D c)
            {
                CollectRestPoses(c, into);
            }
    }

    private static void CollectVisibility(Node3D node, List<(Node3D, bool)> into)
    {
        into.Add((node, node.Visible));
        foreach (var child in node.GetChildren())
            if (child is Node3D c)
            {
                CollectVisibility(c, into);
            }
    }

    /// <summary>Builds the meshless <see cref="CrashAnchorNodes"/> under a 'player' root — the crash
    /// def's local anchor set (see the field remark).</summary>
    private static Node3D BuildCrashAnchorSet()
    {
        var set = new Node3D { Name = "player" };
        set.SetMeta(AnimRuntime.NameMeta, "player");
        foreach (var name in CrashAnchorNodes)
        {
            var node = new Node3D { Name = name };
            node.SetMeta(AnimRuntime.NameMeta, name);
            set.AddChild(node);
        }
        return set;
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

    /// <summary>Loads the flown mission's weather.json and resolves <see cref="_activeZone"/>:
    /// the zone the fog AND the skydome are both built from. Called before the domes, because
    /// the zone names are per chapter — C5 ships zone1+zone3, so the `zone2` default has to fall
    /// back or C5 renders with no fog and no dome at all. The default stays
    /// `zone2` deliberately; which zone a mission actually flies is in no reader, so it is the
    /// user's A/B against the original (docs/formats/weather.md).</summary>
    private void LoadWeather(string missionZrdrPath)
    {
        _weather = WeatherState.Load(missionZrdrPath);
        _activeZone = _weather?.ResolveZone(_skyZone) ?? _skyZone;
        if (_weather == null)
        {
            GD.PushWarning($"no weather.json for {_chapter}/{_mission} — flying without fog / whiteout");
            return;
        }
        if (!_activeZone.Equals(_skyZone, StringComparison.OrdinalIgnoreCase))
            // Not a fault: a chapter that numbers its zones differently resolves here every
            // flight. C5 (zone1/zone3) does so on all 8 missions, and zone1 is the confirmed
            // correct choice there — so this must not read as a missing-data warning.
            GD.Print($"weather: {_chapter}/{_mission} has no '{_skyZone}' "
                     + $"(zones: {string.Join("/", _weather.ZoneNames)}) — rendering '{_activeZone}'");
    }

    /// <summary>Applies the loaded weather: sets the distance-fog global shader parameters for
    /// the rendered sky zone (all world + aircraft surfaces pick them up), and builds the
    /// full-screen cloud-band whiteout overlay (its opacity is driven each frame from the camera
    /// altitude in <see cref="_Process"/>). No-op if the mission has no weather.json — the fog
    /// globals keep their registered no-op range. Call <see cref="LoadWeather"/> first.</summary>
    private void SetupWeather(TextureArchive textures)
    {
        if (_weather == null)
            return;
        var fog = _weather.Fog(_activeZone);
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
        // --no-fog pushes the range out of reach instead of touching `csky_fog_on`. That uniform
        // would work — every shader still honours it — but it is an INSTANCE uniform declared at
        // index 1 in SceneBuilder's shader and index 0 in Clutter's, and Godot merges that mapping
        // per GeometryInstance3D. Writing it would make a latent index mismatch live (the
        // unfogged-hilltops bug; see Clutter.ShaderCode's comment). `csky_fog_range` is
        // a GLOBAL uniform every fogged shader reads, so one write covers the world, the clutter
        // sprites, the solid city blocks and the dome with no ordering hazard at all.
        var fogRange = _noFog
            ? new Vector2(1e8f, 1e9f)   // same no-op range Weather.NoFog uses
            : new Vector2(fog.FogNear, fog.FogFar) / fogRangeFactor;
        RenderingServer.GlobalShaderParameterSet("csky_fog_range", fogRange);
        // FOG_ALTITUDE: the fog cylinder's vertical extent — full fog below FogLow, fading to
        // none at FogHigh (fragment altitude; see SceneBuilder's fog shader block). Absolute
        // altitudes, so the range factor doesn't apply.
        RenderingServer.GlobalShaderParameterSet("csky_fog_alt", new Vector2(fog.FogLow, fog.FogHigh));
        // World brightness from the zone's SUNLIGHT (see WeatherState.WorldLight): the original
        // dims the baked-vertex world by the mission's ambient+diffuse; we apply it as a scalar
        // on the fullbright world/deck/dome (the fog color, set above, is unaffected — it mixes
        // in after). 1.0 for bright/day missions, < 1 for overcast/night.
        // Apply the dimming in GAMMA space (the DX7 chain texel×vtx×light is all sRGB-space),
        // consistent with the gamma-space vertex modulate: the shader multiplies LINEAR ALBEDO,
        // so feed the linearised factor — linear_ALBEDO · srgbToLinear(f) == gamma-space · f.
        // (Applied in linear space, 0.80 only reaches 210→190; gamma-space lands the deck 210→169.)
        float worldLightLinear = new Color(fog.WorldLight, fog.WorldLight, fog.WorldLight).SrgbToLinear().R;
        RenderingServer.GlobalShaderParameterSet("csky_world_light", worldLightLinear);
        GD.Print($"weather [{_activeZone}]{(_noFog ? " --no-fog: fog + whiteout OFF, world light unchanged;" : ":")} " +
                 $"fog {fog.FogColor.R:0.00} gray {fog.FogNear:0}–{fog.FogFar:0} m, " +
                 $"altitude {fog.FogLow:0}–{fog.FogHigh:0} m; world light {fog.WorldLight:0.00}; " +
                 $"cloud band {_weather.CloudBottom:0}–{_weather.CloudTop:0} m (±{_weather.CloudThickness:0})");

        if (_weather.HasCloudBand)
        {
            // Both of these follow *a* camera, so each rig gets its own: in splitscreen
            // the overlay must dim only the pane whose player is inside the cloud, and the puff
            // field must sit around that player.
            foreach (var rig in _rigs)
            {
                // A pane-filling overlay so the whiteout swallows everything (terrain, plane,
                // clouds) uniformly, like the original. Layer 0 keeps it behind the HUD (layer 1).
                var canvas = new CanvasLayer { Layer = 0, Name = "whiteout" };
                rig.Whiteout = new ColorRect
                {
                    Color = new Color(WhiteoutColor, 0f),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                rig.Whiteout.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                canvas.AddChild(rig.Whiteout);
                rig.HudParent.AddChild(canvas);

                // Ambient cloud puffs: the soft wisps that drift past the plane at altitude
                // (OriginalScreenshots/"C1 IA1 Cloud Puffs and Moon.png"). A hand-tuned field
                // gated to the cloud band and drifting with the weather WIND; _Process advances it
                // each frame from the camera. Added at world identity (its instance positions are
                // absolute world coords).
                rig.Puffs = Effects.CloudPuffs.Create(textures, _weather.WindStatic,
                    _weather.CloudBottom, _weather.CloudTop);
                if (rig.Puffs == null)
                    continue;
                if (rig.VisualLayer != 0)
                    SplitScreen.SetVisualLayer(rig.Puffs, rig.VisualLayer);
                _worldRoot!.AddChild(rig.Puffs);
                if (rig.Index == 0)
                    GD.Print("cloud puffs: ambient field active over the cloud band");
            }
        }

        // Precipitation (rain/snow) — only the missions whose weather.json carries a TYPE block
        // get a field (C4 snow, C1C/C2B rain). It shows only below the CLOUD_COVER band (the
        // rain falls from the cloud base — none above the overcast). Self-animating from the
        // shader's TIME + camera built-ins, so it needs no _Process driving.
        _precip = Effects.Precipitation.Create(_weather.Precip, _weather.CloudBottom, _weather.CloudTop);
        if (_precip != null)
            _worldRoot!.AddChild(_precip);
    }

    /// <summary>The spawn index player 1 starts from: --spawn=N if given, else a random pick per
    /// launch like the original. Each further player takes the next index in list order (wrapping),
    /// so splitscreen players never share a spawn point.</summary>
    private int ChooseSpawnBase(IReadOnlyList<SpawnPoint>? spawns)
    {
        if (spawns == null || spawns.Count == 0)
            return 0;
        return _spawnIndex >= 0
            ? Mathf.Clamp(_spawnIndex, 0, spawns.Count - 1)
            : (int)(GD.Randi() % (uint)spawns.Count);
    }

    /// <summary>Picks one player's flight spawn: a world position + a look-at point one unit
    /// ahead along the spawn heading. Instant-action missions (IA1) draw from ia.json's scenario
    /// spawn list at <paramref name="spawnBase"/> + the player index (wrapping). Story missions
    /// (M0x, no ia.json) fall back to objectives.json PLAYER_INIT. A fixed C1 spawn is the last
    /// resort if neither is present.</summary>
    private (Vector3 pos, Vector3 lookAt) ChooseSpawn(IReadOnlyList<SpawnPoint>? spawns,
        string missionZrdrPath, int spawnBase, int playerIndex, string tag)
    {
        // Debug/testing override: place the plane exactly (position + nose direction), bypassing
        // the mission spawn list — lets a scripted run start just short of a target pointed at it,
        // so a neutral --hold flies a straight, deterministic path (no complex maneuvering).
        if (_spawnAt is { } at)
        {
            var dir = _spawnDir ?? Vector3.Forward;
            if (dir.LengthSquared() < 1e-6f)
                dir = Vector3.Forward;
            dir = dir.Normalized();
            // Splitscreen: fan the players out abreast so they don't spawn inside each other.
            at += dir.Cross(Vector3.Up).Normalized() * (playerIndex * SpawnAbreast);
            GD.Print($"spawn [{tag}override]: pos=({at.X:0},{at.Y:0},{at.Z:0}) " +
                     $"dir=({dir.X:0.00},{dir.Y:0.00},{dir.Z:0.00})");
            return (at, at + dir);
        }

        if (spawns is { Count: > 0 })
        {
            int i = (spawnBase + playerIndex) % spawns.Count;
            return LogSpawn($"{tag}{_scenario} #{i} of {spawns.Count}", spawns[i]);
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
        _sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-45, 150, 0), // shine onto the -Z (nose) side
            LightEnergy = 1.6f,
            ShadowEnabled = true,
        };
        AddChild(_sun);

        _env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = new ProceduralSkyMaterial() },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.9f,
        };
        AddChild(new WorldEnvironment { Environment = _env });
    }

    private void FrameCamera() => _orbit.Frame(OrbitCamera.MergedAabb(_plane!), _camPos, _lookAt);

    /// <summary>--dump-markers[=plane]: print each player airframe's firepoint / pylon / target
    /// rig — name, plane-frame position, gun-pair grouping and shared mounts — to stdout and
    /// <c>./.scratch/markers_dump.txt</c>, then quit (see <see cref="Mech3.MarkerRig"/>). This is
    /// the committed instrument the <c>docs/formats/markers.md</c> tables regenerate from, so the
    /// user can see and name every mount when handing back the airframe gun-group table (item A3).
    /// An optional value filters to one plane by model node (<c>player_bhawk</c>) or display name
    /// (<c>Bloodhawk</c>), matched case-insensitively as a substring.</summary>
    private void DumpMarkers()
    {
        // Windowed launches shouldn't steal focus for a report that renders nothing and quits.
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
        GameZ gamez;
        try
        {
            gamez = GameZ.Load(_planesGamezPath);
        }
        catch (Exception e)
        {
            GD.PrintErr($"--dump-markers: could not load planes gamez ({_planesGamezPath}): {e.Message}");
            return;
        }

        var wanted = new List<(string Model, string Display)>();
        foreach (var plane in Mech3.MarkerRig.PlayerAirframes)
        {
            if (_dumpMarkersPlane.Length == 0
                || plane.Model.Contains(_dumpMarkersPlane, StringComparison.OrdinalIgnoreCase)
                || plane.Display.Contains(_dumpMarkersPlane, StringComparison.OrdinalIgnoreCase))
            {
                wanted.Add(plane);
            }
        }
        if (wanted.Count == 0)
        {
            var names = new List<string>();
            foreach (var p in Mech3.MarkerRig.PlayerAirframes)
                names.Add($"{p.Display} ({p.Model})");
            GD.PrintErr($"--dump-markers: '{_dumpMarkersPlane}' matched no airframe. Available: "
                        + string.Join(", ", names));
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Aircraft marker rig — {_planesGamezPath}");
        sb.AppendLine("# Positions are plane frame, metres (nose -Z, right +X, up +Y). See docs/formats/markers.md.");
        sb.AppendLine();
        int done = 0;
        foreach (var (model, display) in wanted)
        {
            var rig = Mech3.MarkerRig.Extract(gamez, model);
            if (rig == null)
            {
                sb.AppendLine($"=== {display} ({model}) — root node not found ===").AppendLine();
                continue;
            }
            sb.Append(rig.Format(display)).AppendLine();
            done++;
        }
        var text = sb.ToString();
        GD.Print(text);

        // ./.scratch/ inside the workspace, per CLAUDE.md — never the OS temp dir.
        var scratch = Path.Combine(_repoRoot, ".scratch");
        Directory.CreateDirectory(scratch);
        var outPath = Path.Combine(scratch, "markers_dump.txt");
        File.WriteAllText(outPath, text);
        GD.Print($"markers dump: {done} airframe(s) → ./.scratch/markers_dump.txt");
    }

    /// <summary>--dump-weapons[=id|name]: load the typed <see cref="Flight.WeaponDefs"/> reader
    /// (B11) over <c>weapons.json</c>, print one line per def (id, name, key ballistics, flags,
    /// bindings) to stdout and <c>./.scratch/weapons_dump.txt</c>, and report any unmapped keys,
    /// then quit. The committed verification instrument the weapons.md table is checked against —
    /// a clean run (no UNHANDLED lines) is the B11 pass. An optional value filters by id
    /// (<c>wep_06</c>) or <c>NAME</c> substring, matched case-insensitively.</summary>
    private void DumpWeapons()
    {
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
        // Locale-independent output: float.ToString() is culture-sensitive, so without this a
        // German machine writes "6,25" where an invariant one writes "6.25" — the dump is a
        // committed verification artifact and must read the same everywhere.
        System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        Flight.WeaponDefs weapons;
        try
        {
            var messages = Messages.Load(_messagesPath);
            weapons = Flight.WeaponDefs.Load(_zrdrPath, messages);
        }
        catch (Exception e)
        {
            GD.PrintErr($"--dump-weapons: could not load weapons.json ({_zrdrPath}): {e.Message}");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# weapons.json — {_zrdrPath}");
        sb.AppendLine($"# {weapons.All.Count} BALLISTICS entries; empty-clip sound = {weapons.EmptyClipSound}");
        sb.AppendLine("# See docs/formats/weapons.md.");
        sb.AppendLine();

        int shown = 0, unhandledTotal = 0;
        foreach (var w in weapons.All)
        {
            if (_dumpWeaponsFilter.Length > 0
                && !w.Id.Contains(_dumpWeaponsFilter, StringComparison.OrdinalIgnoreCase)
                && !w.Name.Contains(_dumpWeaponsFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            shown++;
            var flags = new List<string>();
            if (w.IsCannon) { flags.Add("CANNON"); }
            if (w.IsRocket) { flags.Add("ROCKET"); }
            if (w.HighExplosive) { flags.Add("HE"); }
            if (w.Sonic) { flags.Add("SONIC"); }
            if (w.Flash) { flags.Add("FLASH"); }
            if (w.BeeperSeeker) { flags.Add("BEEPER_SEEKER"); }
            if (w.Rear) { flags.Add("REAR"); }
            if (w.Torpedo) { flags.Add("TORPEDO"); }
            if (w.Targetable) { flags.Add("TARGETABLE"); }
            if (w.DamagesZeppelin) { flags.Add("DMG_ZEP"); }
            if (w.ShakesCamera) { flags.Add("SHAKE"); }
            if (w.Crater) { flags.Add("CRATER"); }
            sb.Append($"{w.Id}  {w.Name,-8}  \"{w.DisplayName}\"");
            sb.Append($"\n    cal={Opt(w.Caliber)} rate={w.FireRate} vel={Opt(w.Velocity)} range={Opt(w.Range)}"
                      + $" acc={Opt(w.Acceleration)} turn={Opt(w.TurnRate)} spread={Opt(w.CannonSpread)}");
            sb.Append($"\n    dmg armor={Opt(w.ArmorDamage)} health={Opt(w.HealthDamage)} combined={Opt(w.Damage)}"
                      + $" | cluster={Opt(w.ClusterSize)} ammo_limit={Opt(w.AmmoLimit)}");
            sb.Append($"\n    lock={Opt(w.LockOn)} det_dist={Opt(w.DetonationDistance)} proximity={Opt(w.ImpactProximity)}"
                      + $" priority={Opt(w.Priority)}");
            sb.Append($"\n    flags: [{string.Join(", ", flags)}]");
            sb.Append($"\n    fire={FmtEffect(w.Fire)} flyout={FmtFlyout(w.Flyout)} looped={w.LoopedSoundName ?? "-"}");
            sb.Append("\n    impact:");
            foreach (var kv in w.Impact)
            {
                sb.Append($" {kv.Key}={FmtEffect(kv.Value)}");
            }
            if (w.Impact.Count == 0)
            {
                sb.Append(" (none)");
            }
            if (w.UnhandledKeys.Count > 0)
            {
                unhandledTotal += w.UnhandledKeys.Count;
                sb.Append($"\n    !! UNHANDLED KEYS: {string.Join(", ", w.UnhandledKeys)}");
            }
            sb.AppendLine();
            sb.AppendLine();
        }

        var text = sb.ToString();
        GD.Print(text);
        var scratch = Path.Combine(_repoRoot, ".scratch");
        Directory.CreateDirectory(scratch);
        File.WriteAllText(Path.Combine(scratch, "weapons_dump.txt"), text);
        GD.Print(unhandledTotal == 0
            ? $"weapons dump: {shown} entr(y/ies), NO unhandled keys → ./.scratch/weapons_dump.txt"
            : $"weapons dump: {shown} entr(y/ies), {unhandledTotal} UNHANDLED key(s) — see the !! lines above");
    }

    /// <summary>--dump-loadout[=plane]: for each plane in <c>stock_loadouts.json</c>, build its
    /// model and bind the stock loadout (<see cref="Flight.Loadout"/>, B12), reporting the resolved
    /// gun groups (mount, weapon, per-group ammo, muzzle nodes) and hardpoints — or the loud error
    /// if a marker doesn't resolve. Writes to stdout and <c>./.scratch/loadout_dump.txt</c>, then
    /// quits. <c>--loadout=&lt;def&gt;</c> binds that def's loadout instead of each plane's own (a
    /// cross-binding test — e.g. binding a def that wants <c>firepoint8</c> to the Kestrel proves
    /// the missing-marker error fires). An optional value filters by def / model / display.</summary>
    private void DumpLoadout()
    {
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
        Flight.StockLoadouts stock;
        Flight.WeaponDefs weapons;
        GameZ planesGamez;
        TextureArchive textures;
        try
        {
            stock = Flight.StockLoadouts.Load();
            weapons = Flight.WeaponDefs.Load(_zrdrPath, Messages.Load(_messagesPath));
            planesGamez = GameZ.Load(_planesGamezPath);
            // Any texture archive resolves the (meshless) markers; the C1 set is the viewer default.
            textures = new TextureArchive(SessionPaths.ChapterTextures(_dataRoot, "C1"));
        }
        catch (Exception e)
        {
            GD.PrintErr($"--dump-loadout: could not load inputs: {e.Message}");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Stock loadouts bound to models — CSVM/data/stock_loadouts.json");
        if (_loadoutOverride != null)
        {
            sb.AppendLine($"# --loadout override: binding every plane to '{_loadoutOverride}'");
        }
        sb.AppendLine();

        int ok = 0, failed = 0;
        using (textures)
        {
            foreach (var def in stock.All.Values)
            {
                if (_dumpLoadoutFilter.Length > 0
                    && !def.Def.Contains(_dumpLoadoutFilter, StringComparison.OrdinalIgnoreCase)
                    && !def.Model.Contains(_dumpLoadoutFilter, StringComparison.OrdinalIgnoreCase)
                    && !def.Display.Contains(_dumpLoadoutFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var bindDef = _loadoutOverride != null ? stock.For(_loadoutOverride) : def;
                sb.Append($"=== {def.Display} ({def.Def} / {def.Model}) ===");
                if (bindDef == null)
                {
                    sb.AppendLine($"\n  !! --loadout='{_loadoutOverride}' is not a known loadout def");
                    sb.AppendLine();
                    failed++;
                    continue;
                }
                Node3D? plane = null;
                try
                {
                    plane = new PlaneBuilder(planesGamez, textures).Build(def.Model);
                    var loadout = Flight.Loadout.Bind(bindDef, plane, weapons);
                    sb.Append("\n  guns:");
                    foreach (var g in loadout.Guns)
                    {
                        var names = new List<string>();
                        foreach (var m in g.Muzzles)
                        {
                            names.Add(m.HasMeta(AnimRuntime.NameMeta) ? m.GetMeta(AnimRuntime.NameMeta).AsString() : m.Name);
                        }
                        sb.Append($"\n    slot{g.Slot} {g.Mount,-22} {g.Weapon.Id} ({g.Weapon.Name})"
                                  + $" ammo {g.Ammo}{(g.IsTurret ? "  [TURRET, inert]" : "")}"
                                  + $"  muzzles: {string.Join(", ", names)}");
                    }
                    if (loadout.Hardpoints.Count > 0)
                    {
                        var hp = loadout.Hardpoints[0];
                        int total = 0;
                        foreach (var h in loadout.Hardpoints) { total += h.Capacity; }
                        sb.Append($"\n  hardpoints: {loadout.Hardpoints.Count} x {hp.Weapon.Id} ({hp.Weapon.Name}),"
                                  + $" {hp.Capacity} per pylon = {total} total  (pylon1..pylon{loadout.Hardpoints.Count})");
                    }
                    else
                    {
                        sb.Append("\n  hardpoints: none");
                    }
                    sb.AppendLine();
                    ok++;
                }
                catch (Exception e)
                {
                    sb.AppendLine($"\n  !! {e.Message}");
                    failed++;
                }
                finally
                {
                    plane?.Free();
                }
                sb.AppendLine();
            }
        }

        var text = sb.ToString();
        GD.Print(text);
        var scratch = Path.Combine(_repoRoot, ".scratch");
        Directory.CreateDirectory(scratch);
        File.WriteAllText(Path.Combine(scratch, "loadout_dump.txt"), text);
        GD.Print(failed == 0
            ? $"loadout dump: {ok} plane(s) bound, every marker resolved → ./.scratch/loadout_dump.txt"
            : $"loadout dump: {ok} ok, {failed} FAILED — see the !! lines above");
    }

    /// <summary>The C22 verification (until F40's interactive HP control lands): for one live
    /// destructible instance per distinct DAMAGE_SEQUENCE-carrying def (optionally filtered by
    /// NAME), sweep its HP from full to zero and record which stage effect the DAMAGE_SEQUENCE
    /// fires at which health. Subscribing to <see cref="Mech3.AnimRuntime.OnInstanceStarted"/> is
    /// how each fired CALL_ANIMATION is observed; the runtime's already-live guard means each
    /// effect starts once, so its first-seen HP is its threshold. Reports to stdout and
    /// <c>./.scratch/damage_test.txt</c>.</summary>
    private void RunDamageTest(Mech3.AnimRuntime runtime)
    {
        static bool HasDamage(Mech3.AnimDefinition d) => d.Sequences.Any(s =>
            string.Equals(s.Name, "DAMAGE_SEQUENCE", StringComparison.OrdinalIgnoreCase));

        // Colliders (C25): SceneBuilder attaches a StaticBody3D "col" with a CollisionShape3D per
        // collidable mesh, and SetSubtreeActive toggles that shape's Disabled as it swaps
        // healthy→destroyed. The swap targets nodes via the compiled symbol table (NodeRefs), which
        // can resolve to geometry OUTSIDE the small anim anchor — so counting under the anchor misses
        // it. Census the WHOLE world instead and report the per-kill delta: a quiet harness kills one
        // object at a time, so (enabled before − after) is exactly the collision it switched off.
        static HashSet<CollisionShape3D> EnabledColliders(Node root)
        {
            var set = new HashSet<CollisionShape3D>();
            void Walk(Node n)
            {
                if (n is CollisionShape3D cs && !cs.Disabled)
                {
                    set.Add(cs);
                }
                foreach (var c in n.GetChildren())
                {
                    Walk(c);
                }
            }
            Walk(root);
            return set;
        }

        // The top Node3D above an anchor — the world subtree root, so the census excludes the UI /
        // Window and only walks placed + partition geometry.
        static Node3D WorldRoot(Node3D n)
        {
            var t = n;
            while (t.GetParent() is Node3D p)
            {
                t = p;
            }
            return t;
        }

        // One representative instance per distinct def — a wildcard NAME binds many identical
        // towers, and sweeping every one would just repeat the same result and start hundreds of
        // effects. Capped so a chapter full of destructibles stays a readable report.
        var chosen = new List<Mech3.DestructibleRegistry.Instance>();
        var seenDefs = new HashSet<Mech3.AnimDefinition>();
        foreach (var inst in runtime.Destructibles.All)
        {
            // Continuous-sweep mode (C22) only makes sense for staged DAMAGE_SEQUENCE defs; the
            // discrete-kill mode (C23/C24/C25) applies to EVERY destructible — the doors and gates
            // instant-die with no stages, so gating them out would hide exactly C25's cases.
            if (_damageHd <= 0f && !HasDamage(inst.Def))
            {
                continue;
            }
            if (_damageTestFilter.Length > 0
                && !inst.Def.Name.Contains(_damageTestFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (seenDefs.Add(inst.Def))
            {
                chosen.Add(inst);
            }
            if (chosen.Count >= 16)
            {
                break;
            }
        }

        var sb = new System.Text.StringBuilder();
        string mode = _damageHd > 0f ? $"weapon hits, {_damageHd:0.##} HEALTH_DAMAGE each" : "continuous HP sweep";
        string kind = _damageHd > 0f ? "destructible def(s)" : "DAMAGE_SEQUENCE def(s)";
        sb.AppendLine($"damage-test: chapter {_chapter}, filter '{_damageTestFilter}', mode = {mode} — "
            + $"{chosen.Count} {kind} of "
            + $"{runtime.Destructibles.Count} destructible instance(s)");
        foreach (var inst in chosen)
        {
            // Discrete-hit mode (C23): spend a fixed HEALTH_DAMAGE per hit through DamageAt and
            // count hits to destruction. DamageAt resolves a struck node to its AUTHORITATIVE
            // instance, so drive the resolved one (the compiled def wins a shared node) — driving
            // the picked reader twin would damage the compiled instance and never see HP fall.
            var target = _damageHd > 0f ? (runtime.Destructibles.Resolve(inst.Anchor) ?? inst) : inst;
            var fired = new List<(string At, string Effect)>();
            var started = new List<(string? Anim, Node3D? Anchor)>();
            int hit = 0;
            float atHp = target.MaxHealth;
            void OnStarted(Mech3.AnimDefinition def, Node3D? anchor)
            {
                fired.Add((_damageHd > 0f ? $"hit {hit}" : $"HP≤{atHp:0.##}", def.AnimName ?? def.Name));
                started.Add((def.AnimName, anchor));
            }

            target.Health = target.MaxHealth;
            target.Status = Mech3.DestructibleRegistry.State.Healthy;
            target.DamageStage = 0;
            var colBefore = _damageHd > 0f ? EnabledColliders(WorldRoot(target.Anchor)) : new HashSet<CollisionShape3D>();
            int debrisBefore = runtime.BallisticMotionsLaunched;
            runtime.OnInstanceStarted += OnStarted;
            if (_damageHd > 0f)
            {
                int cap = (int)(target.MaxHealth / _damageHd) + 4;   // a few past the expected kill
                while (target.Status != Mech3.DestructibleRegistry.State.Destroyed && hit < cap)
                {
                    hit++;
                    runtime.DamageAt(target.Anchor, _damageHd);
                }
            }
            else
            {
                // Fine enough to land on the round-fraction thresholds exactly (0.60/0.30 of HEALTH …).
                const int steps = 240;
                for (int i = 0; i <= steps; i++)
                {
                    atHp = target.MaxHealth * (1f - i / (float)steps);
                    target.Health = atHp;
                    runtime.ApplyDamageStages(target);
                }
            }
            runtime.OnInstanceStarted -= OnStarted;

            // Walk-up resolution check (C23): resolving from a deep descendant of the anchor —
            // the kind of node a projectile's raycast actually strikes (a collider sits under the
            // mesh under the anchor) — must land back on this same destructible.
            Node3D deep = target.Anchor;
            while (deep.GetChildCount() > 0 && deep.GetChild(0) is Node3D child)
            {
                deep = child;
            }
            var back = runtime.Destructibles.Resolve(deep);
            string resolve = back?.Anchor == target.Anchor ? "resolve✓" : $"resolve✗({back?.Def.Name ?? "null"})";

            // Death-swap check (C24): once killed, the healthy subtree should be hidden and the
            // destroyed subtree shown. Scan the anchor's descendants by cs_name — a test
            // diagnostic (the mechanism keys off the def's own OBJECT_ACTIVE_STATE, not names).
            string swap = "";
            if (_damageHd > 0f && target.Status == Mech3.DestructibleRegistry.State.Destroyed)
            {
                int hVis = 0, hAll = 0, dVis = 0, dAll = 0;
                void Walk(Node3D n)
                {
                    string cs = n.HasMeta(Mech3.AnimRuntime.NameMeta)
                        ? n.GetMeta(Mech3.AnimRuntime.NameMeta).AsString()
                        : n.Name.ToString();
                    if (cs.Contains("healthy", StringComparison.OrdinalIgnoreCase)) { hAll++; if (n.Visible) hVis++; }
                    if (cs.Contains("destroyed", StringComparison.OrdinalIgnoreCase)) { dAll++; if (n.Visible) dVis++; }
                    foreach (var c in n.GetChildren())
                    {
                        if (c is Node3D c3)
                        {
                            Walk(c3);
                        }
                    }
                }
                Walk(target.Anchor);
                if (hAll > 0 || dAll > 0)
                {
                    swap = $"swap[healthy {hVis}/{hAll} shown, destroyed {dVis}/{dAll} shown]; ";
                }
                // Collider census (C25), split by direction: how many colliders this kill switched
                // OFF (the healthy door/building collision that stops blocking flight) vs. ON (the
                // wreck/debris the death — and any chained animation — brings solid). A net count
                // hides the door removal when the death also spawns a solid wreck.
                var colAfter = EnabledColliders(WorldRoot(target.Anchor));
                int off = colBefore.Count(cs => !colAfter.Contains(cs));
                int on = colAfter.Count(cs => !colBefore.Contains(cs));
                swap += $"col[off {off}, on {on}]; ";
                // Debris tumble (C26): the death's ballistic OBJECT_MOTION bodies — the wreck pieces
                // that arc out under gravity and tumble (translation_range/forward_rotation). They are
                // SCHEDULED (the water tower's at t=2.2 s), so advance the death forward past the
                // schedule to let them launch — done AFTER swap/col so those stay the immediate
                // post-death state (pre-tick). The world is in the tree (see the --damage-test hook,
                // ManualAdvance) so the ticked global-transform reads are valid.
                for (int i = 0; i < 7; i++)
                {
                    runtime.Advance(0.5f);   // 3.5 s — past the ~2.2 s schedule, into the tumble
                }
                int debris = runtime.BallisticMotionsLaunched - debrisBefore;
                swap += $"debris[{debris} launched]; ";
            }
            // Stop the effects this run started, AFTER the C26 tick so the debris actually launches
            // first: reader-wildcard and compiled per-instance defs bind the SAME tower nodes (C21),
            // so a leftover live effect would make the twin's identical CALL_ANIMATION a no-op and
            // read as "no stage effect fired".
            foreach (var (anim, anchor) in started)
            {
                runtime.Stop(anim, anchor);
            }

            // C27 collide-gate probe: reset and apply a plane COLLISION via CollideDamageAt. Only a
            // WeaponOrCollideHit destructible (the 44 facades/windows/agyrobus) accepts it and breaks;
            // a WeaponHit object (tower, gate) ignores the collision and stands (decision 6).
            string collide = "";
            if (_damageHd > 0f)
            {
                target.Health = target.MaxHealth;
                target.Status = Mech3.DestructibleRegistry.State.Healthy;
                target.DamageStage = 0;
                bool accepted = runtime.CollideDamageAt(target.Anchor, target.MaxHealth + 1f);
                bool broke = target.Status == Mech3.DestructibleRegistry.State.Destroyed;
                collide = $"collide[{(accepted ? (broke ? "✓ broke" : "✓ hit, survived") : "✗ ignored")}, {target.Def.Activation}]; ";
            }

            // C28 reset/restore check: from a destroyed state, ResetDestructible returns the object to
            // healthy (full HP, healthy subtree visible, destroyed hidden, debris flown home), and an
            // identical second kill takes the same hits — proving destroy→reset→destroy is idempotent.
            string reset = "";
            if (_damageHd > 0f)
            {
                int cap2 = (int)(target.MaxHealth / _damageHd) + 4;
                while (target.Status != Mech3.DestructibleRegistry.State.Destroyed && cap2-- > 0)
                {
                    runtime.DamageAt(target.Anchor, _damageHd);   // ensure dead before resetting
                }
                runtime.ResetDestructible(target);
                bool backHp = target.Status == Mech3.DestructibleRegistry.State.Healthy
                    && target.Health >= target.MaxHealth - 1e-3f;
                int hVis = 0, hAll = 0, dVis = 0, dAll = 0;
                void Scan(Node3D n)
                {
                    string cs = n.HasMeta(Mech3.AnimRuntime.NameMeta)
                        ? n.GetMeta(Mech3.AnimRuntime.NameMeta).AsString() : n.Name.ToString();
                    if (cs.Contains("healthy", StringComparison.OrdinalIgnoreCase)) { hAll++; if (n.Visible) { hVis++; } }
                    if (cs.Contains("destroyed", StringComparison.OrdinalIgnoreCase)) { dAll++; if (n.Visible) { dVis++; } }
                    foreach (var c in n.GetChildren())
                    {
                        if (c is Node3D c3) { Scan(c3); }
                    }
                }
                Scan(target.Anchor);
                bool backVis = hAll == 0 || (hVis == hAll && dVis == 0);   // healthy shown, destroyed hidden
                int rekap = (int)(target.MaxHealth / _damageHd) + 4;
                int hits2 = 0;
                while (target.Status != Mech3.DestructibleRegistry.State.Destroyed && hits2 < rekap)
                {
                    hits2++;
                    runtime.DamageAt(target.Anchor, _damageHd);
                }
                reset = $"reset[healthy={(backHp && backVis ? "✓" : "✗")} (h{hVis}/{hAll},d{dVis}/{dAll}), "
                    + $"rekill {hits2}h {(hits2 == hit ? "✓" : $"✗ vs {hit}")}]; ";
            }

            string src = target.Def.Archive != null ? "compiled" : "reader";
            string stages = fired.Count == 0
                ? "no stage effect fired"
                : string.Join(", ", fired.Select(f => $"{f.At} → {f.Effect}"));
            string outcome = _damageHd > 0f
                ? (target.Status == Mech3.DestructibleRegistry.State.Destroyed
                    ? $"DESTROYED in {hit} hit(s); {swap}{collide}{reset}"
                    : $"SURVIVED {hit} hit(s); {collide}{reset}")
                : "";
            sb.AppendLine($"  {target.Def.Name} (HEALTH {target.MaxHealth:0.##}, {src}) {resolve}: {outcome}{stages}");
        }

        var text = sb.ToString();
        GD.Print(text);
        var scratch = Path.Combine(_repoRoot, ".scratch");
        Directory.CreateDirectory(scratch);
        File.WriteAllText(Path.Combine(scratch, "damage_test.txt"), text);

        // C25 diagnostic: the world's collidable-geometry inventory, so we can confirm destructible
        // roles (healthy / destroyed / door*) are among the solid geometry — collision is built here
        // only because --damage-test forces it (--freecam alone builds none). Counts per owning-mesh
        // cs_name (a per-name census is enough to see what is solid; positions are not needed).
        if (chosen.Count > 0)
        {
            var byName = new SortedDictionary<string, int>();
            int cols = 0;
            void Walk(Node n, string parentName)
            {
                string name = n is Node3D n3 && n3.HasMeta(Mech3.AnimRuntime.NameMeta)
                    ? n3.GetMeta(Mech3.AnimRuntime.NameMeta).AsString()
                    : n.Name.ToString();
                if (n is StaticBody3D body && body.Name.ToString() == "col")
                {
                    cols++;
                    byName.TryGetValue(parentName, out int c);
                    byName[parentName] = c + 1;
                }
                foreach (var c in n.GetChildren())
                {
                    Walk(c, name);
                }
            }
            Walk(WorldRoot(chosen[0].Anchor), "");
            var inv = new System.Text.StringBuilder($"{cols} collidable meshes, by owner cs_name:\n");
            foreach (var (nm, c) in byName)
            {
                inv.AppendLine($"  {c,4}  {nm}");
            }
            File.WriteAllText(Path.Combine(scratch, "world_colliders.txt"), inv.ToString());
            GD.Print($"damage-test: {cols} collidable meshes → ./.scratch/world_colliders.txt");
        }
        GD.Print($"damage-test: {chosen.Count} def(s) swept → ./.scratch/damage_test.txt");
    }

    private static string Opt<T>(T? v) where T : struct => v.HasValue ? v.Value.ToString() ?? "-" : "-";

    private static string FmtEffect(Flight.WeaponEffect? e)
    {
        if (e == null)
        {
            return "-";
        }
        var parts = new List<string>();
        if (e.Animation != null) { parts.Add($"anim:{e.Animation}"); }
        if (e.SurfaceAnimation != null) { parts.Add($"surf:{e.SurfaceAnimation}"); }
        if (e.Effect != null) { parts.Add($"fx:{e.Effect}"); }
        if (e.Sound != null) { parts.Add($"snd:{e.Sound}"); }
        return "{" + string.Join("/", parts) + "}";
    }

    private static string FmtFlyout(Flight.WeaponFlyout? f)
    {
        if (f == null)
        {
            return "-";
        }
        var parts = new List<string>();
        if (f.Model != null) { parts.Add($"model:{f.Model}"); }
        if (f.ModelAnimation != null) { parts.Add($"anim:{f.ModelAnimation}"); }
        if (f.Sound != null) { parts.Add($"snd:{f.Sound}"); }
        return "{" + string.Join("/", parts) + "}";
    }

    private static Vector3 ParseVec3(string s)
    {
        var parts = s.Split(',');
        return new Vector3(
            float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
    }

    // ---- Focus mute --------------------------------------------------------------------------
    //
    // Alt-tabbing away silences the game; alt-tabbing back restores it. The mute is an
    // AudioServer *master-bus* mute rather than a factor threaded through the audio code,
    // because there are two entirely independent audio paths and only one of them has any
    // gain plumbing at all:
    //   - Flight.FlightAudio  — own-plane engine/whine/rattle loops (has MixGain) plus the
    //     crash and prop one-shots, which deliberately bypass MixGain ("one-shots stay global").
    //   - Mech3.WorldSounds   — the ambient SOUND_NODE 3D emitters, whose VolumeDb comes
    //     straight from the sound def. No MixGain, no shared gate, nothing to multiply.
    // A `MixGain = 0` mute would therefore leave the whole animated world audible, and would
    // also have to be un-set to exactly the right per-player value on the way back.
    //
    // The bus mute has a second property we specifically want: it does not touch
    // FlightAudio's state at all. The engine loop keeps Playing, `_engineRamp` stays at 1, and
    // FlightAudio.Update keeps writing the throttle curve into VolumeDb every frame while
    // muted — so on focus-in the loop is already at its correct level and does NOT re-ramp
    // from the -60f a fresh AudioStreamPlayer is constructed with (FlightAudio.cs:79), which
    // is what stopping/restarting the players would have caused.
    //
    // `--mute` is unrelated and cannot be reused for this: it is a load-time switch that
    // simply never constructs FlightAudio/WorldSounds, so there is nothing to toggle.
    private bool _focusMuted;

    /// <summary>Master-bus index. This project ships no bus layout, so Master is the only bus
    /// and everything (both audio paths) is on it by default.</summary>
    private const int MasterBus = 0;

    public override void _Notification(int what)
    {
        // The APPLICATION_* pair, not the WM_WINDOW_* pair: the application-level notifications
        // are what a real focus change delivers here. Measured on Windows 11 / Godot 4.7 while
        // implementing this: another app taking the foreground sends 1005
        // (WM_WINDOW_FOCUS_OUT) then 2017 (APPLICATION_FOCUS_OUT), and coming back sends 2016
        // then 1004. Note that *minimising* the window from another process delivers neither —
        // only the mouse enter/exit pair — so a manual test must alt-tab, not minimise.
        // Godot 4's constants are longs; _Notification hands us an int.
        if (what == (int)NotificationApplicationFocusOut)
        {
            SetFocusMuted(true);
        }
        else if (what == (int)NotificationApplicationFocusIn)
        {
            SetFocusMuted(false);
        }
    }

    /// <summary>Mutes/unmutes the master bus and gates pad reads, on window focus. Idempotent —
    /// the notification can arrive more than once — and it only ever clears a mute it set itself,
    /// so it cannot stomp on a mute from anywhere else.</summary>
    private void SetFocusMuted(bool muted)
    {
        if (_focusMuted == muted)
        {
            return;
        }
        _focusMuted = muted;
        AudioServer.SetBusMute(MasterBus, muted);
        // Pad *reads* follow focus too (Pads.For / Pads.InputBlocked): a stick held or drifting
        // while the player is alt-tabbed must not fly the plane, steer the free camera or scroll
        // the launchscreen. The pad *roster* (Pads.Connected) deliberately does not follow focus
        // — an unfocused pad has not disconnected; see the Pads class remarks. The keyboard needs
        // no gate: Godot releases held keys on focus loss, while joypads are polled from SDL
        // regardless of focus, which is the whole reason this exists.
        Pads.Focused = !muted;
        GD.Print(muted
            ? "focus: lost — audio muted, pad reads gated"
            : "focus: regained — audio restored, pad reads live");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            // While the launchscreen is up it owns Esc (back / quit from the Mode screen).
            if (_menu is { Visible: true })
                return;
            // Esc out of a menu-launched flight tears the world down and returns to the
            // launchscreen; a CLI-launched run just quits, as before.
            if (_menuDriven && _inSession)
            {
                ReturnToMenu();
                return;
            }
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
        if (_fly || _freecam || _animLab)
            return; // the FlightController / SpectatorCamera owns the camera; no orbit controls
        _orbit.HandleInput(@event);
    }

    public override void _Process(double delta)
    {
        if (_perf)
            ReportPerf(delta);
        // Entities switched off as unplaced (see WorldBuilder.HideUnplacedEntities) that have
        // since been moved off the world origin are put back: a motion starting is the proof
        // that a definition owns them. Polled rather than deferred once, because an OnCall
        // definition can start its motion at any time. Goes quiet for good once the list drains.
        if (_unplacedWatch != null)
        {
            _unplacedRecheck += delta;
            if (_unplacedRecheck >= 1.0)
            {
                _unplacedRecheck = 0.0;
                if (_unplacedWatch.RestorePlacedEntities() is { Count: > 0 } restored)
                {
                    GD.Print($"world: {restored.Count} entit(y/ies) moved off the origin after all, "
                             + "restored: " + string.Join(", ", restored));
                }
            }
        }
        // Everything below is anchored to *a* camera, so it runs once per rig — one in single
        // player, one per pane in splitscreen (each on that player's own visual layer).
        foreach (var rig in _rigs)
        {
            var camPos = rig.Camera.Position;

            // Keep the skydome centered on the camera in ALL axes (a pure zero-parallax
            // backdrop, like the original): the moon then stays at its designed 28° elevation
            // against the dark dome cap — whose color its painted background matches — instead
            // of sliding down into the bright horizon band as the plane climbs.
            // (One-frame lag vs the flight camera is invisible at 22 km.)
            if (rig.Horizon != null)
                rig.Horizon.Position = camPos;

            // Cloud-band whiteout: fade the overlay in as the camera altitude enters the band.
            if (rig.Whiteout != null && _weather != null)
            {
                var c = rig.Whiteout.Color;
                // --no-fog covers the whiteout too: flying into the cloud band would otherwise
                // still white the pane out, which reads as "fog is not actually off".
                c.A = _noFog ? 0f : _weather.WhiteoutAmount(camPos.Y);
                rig.Whiteout.Color = c;
            }

            // Cloud deck follows the player: centered on the camera x/z and pinned to a fixed
            // altitude at the whiteout-band centre. You climb toward it as a fixed ceiling (floor
            // once above) and pass through it exactly where the whiteout is fully opaque, so the
            // ceiling→floor transition is hidden.
            if (rig.Deck != null && _weather is { HasCloudBand: true })
            {
                float mid = (_weather.CloudTop + _weather.CloudBottom) * 0.5f;
                rig.Deck.Position = new Vector3(
                    camPos.X - _deckCenter.X,
                    mid - _deckCenter.Y,
                    camPos.Z - _deckCenter.Z);
            }

            // Ambient cloud puffs: keep the drifting field around the plane (world-anchored,
            // recycled at the shell edge — see CloudPuffs). Forward is the camera's -Z look dir,
            // so fresh puffs spawn ahead and the plane flies into them.
            rig.Puffs?.Update((float)delta, camPos, -rig.Camera.GlobalTransform.Basis.Z);
        }

        // Map-edge continuation: re-center the mirrored-tile window on the cameras. One window
        // serves every pane (the union of the rings around each player), so two players at
        // opposite edges both get continued terrain. Cheap no-op until one of them crosses a cell
        // boundary (1024 m), then ~a window row rebuilds.
        if (_edgeExtender != null)
        {
            _focusPoints.Clear();
            foreach (var rig in _rigs)
                _focusPoints.Add(rig.Camera.Position);
            _edgeExtender.Update(_focusPoints);
        }

        if (_screenshotPath == null)
            return;
        // Nothing built yet: only shoot once a session's plane exists — unless the launchscreen is
        // up (--menu --screenshot captures the menu itself for layout verification).
        if (_plane == null && _menu is not { Visible: true })
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
            _shotPivot = _orbit.OrbitCenter;   // the point the orbit camera aims at
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
    /// FrameCamera consumes. In orbit mode the look-at is the framed point (_orbit.OrbitCenter); in
    /// --fly it is a point one unit ahead along the view ray — either reproduces the same
    /// framing (FrameCamera reconstructs pitch/yaw from the pos→look-at direction).</summary>
    /// <summary>How far ahead of a free-look camera F11 places the printed --lookat point
    /// (metres). Only the direction matters to every consumer; the distance is about surviving
    /// the 3-decimal rounding of the printed args.</summary>
    private const float PoseLookAtDistance = 100f;

    private void PrintCameraPose()
    {
        var pos = _camera.GlobalPosition;
        // Free-look modes have no framed point, so the look-at is projected along the view
        // direction. Both flight and the spectator camera (--freecam) are free-look; only the
        // static orbit view has a real pivot. Previously --freecam fell into the orbit
        // branch and printed _orbitCenter, which it never sets — every pose aimed at the world
        // origin. Projected a long way out because the args round to 3 decimals: at world
        // coordinates in the thousands, a 1 m offset quantises the reconstructed direction to
        // ~0.06°, which is visible when the pose is pasted back.
        var lookAt = _fly || _freecam || _animLab
            ? pos - _camera.GlobalTransform.Basis.Z * PoseLookAtDistance
            : _orbit.OrbitCenter;
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
