using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Testing;
using CSVM.UI;
using CSVM.Utils;
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
/// git-ignored Screenshots/ folder. F11 (any mode) prints the mode's subject placement as
/// ready-to-paste --pos=/--direction= args for reproducing a view.
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
///   --dump-flight[=plane]        fly a throwaway FlightModel through the manoeuvres the original
///                                was measured flying (video-decoded goldens) and print both
///                                numbers side by side, to stdout and ./.scratch/flight_dump.txt,
///                                then quit; targets exist for the Bloodhawk only
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
///   --effects-test               build the --chapter world, play every impact/destruction effect
///                                through the world-effects runtime and report which build a puffer,
///                                then quit (D32 verify: a started def that renders nothing vs one that does)
///   --destroy=name               kill a named destructible (def / anim / node name, substring) at
///                                session build so a --screenshot captures its destruction with nobody
///                                at the controls — pairs with --freecam + --pos/--direction (F42).
///                                Use --damage-test to list a chapter's destructible names
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
///   --seed=N                     the master seed every subsystem RNG derives from (gun spread,
///                                crash sound, spawn, liveries, animation dice, particles) —
///                                same seed, same run; different seeds genuinely branch. Pinned
///                                to 1 by --det/--anim-lab/--effects-test, drawn from the clock
///                                otherwise (the resolved value is logged, so it can be replayed)
///   --det                        the deterministic bundle: fixed-dt sim clock + master seed 1 +
///                                --spawn=0 + pinned liveries + --no-pads + --jitter=0, each
///                                overridable by passing it explicitly. Implied by --screenshot=,
///                                every --dump-*, and --damage-test; the resolved set is announced
///                                on one `det …` log line so a capture documents itself
///   --no-det                     opt back out — wall-clock sim clock and live randomness, even
///                                under a flag that would otherwise imply --det
///   --debug-dzpaths              build the danger-zone route ribbons (the dzpaths subtree the
///                                world skips — AI/route data the original never renders); debug only
///   --debug-select[=x,y[,up]]    (--freecam/--anim-lab) synthetic click at that screen position on
///                                the first frame, then `up` rungs of PgUp — logs the whole
///                                cs_name ancestor ladder with each rung's world-frame box
///   --debug-nodelab[=spec]       (--freecam/--anim-lab) open the node lab (N) at launch and dump
///                                its readouts to the `ui` log category. Comma-separated tokens:
///                                `deps`, `dest`, `open` (show the panel, log nothing more) and
///                                `node=<cs_name>` (select that node first — the tree's own entry,
///                                which reaches what a click cannot); default dumps both readouts
///   --mission=IA1                which mission's spawns --fly uses (default IA1 = instant action,
///                                ia.json spawn_points); story missions (M0x) fall back to
///                                objectives.json PLAYER_INIT. Pair with --chapter= to match the world
///   --scenario=zeppelin_run      which instant-action scenario's spawn list to spawn from
///                                (zeppelin_run, dogfight_ace, dogfight_squadron, stunt_flying, …)
///   --spawn=N                    force spawn index N in that list (default: random pick, like the
///                                original — relaunch to sample the others; the pick is logged)
///   --spawn-at=x,y,z             deprecated spelling of --pos in flight
///   --spawn-dir=x,y,z            deprecated spelling of --direction in flight
///   --sky-zone=zone2             which horizon zone to render in --fly: zone2 = night
///                                (moon/stars, what the original shows at the C1 airfield),
///                                zone1 = day haze (likely test-only, unfinished gray cap).
///                                Also selects which zone's distance fog (weather.json) applies.
///                                If given in static --chapter mode, the skydome + fog + cloud-
///                                band whiteout render there too (put the camera inside the map
///                                via --pos — deterministic fog/whiteout verification shots)
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
///   --no-vsync                   uncap the frame loop, so --perf's frame/fps/script report the
///                                work done instead of sitting pinned at the refresh rate. The
///                                simulation is unchanged under --det: one sim step per rendered
///                                frame, however fast the frames come
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
///   --pos=x,y,z                  place the mode's subject here: the camera in --freecam/--viewer/
///                                --anim-lab, the plane in --fly/--stunt (bypassing the mission
///                                spawn list — e.g. start just short of a target, or over water)
///   --direction=x,y,z            which way it faces there: the view direction, or the nose
///   --lookat=x,y,z               the point form of --direction; also the --viewer orbit pivot
///   --view=1-9                   hold one of the numpad flight-camera perspectives for the whole
///                                run (2 belly, 1/3 below-flank, 4/6 flank, 7/9 above-flank, 8
///                                ahead looking back); flight only, 5 is unbound
///   --campos=x,y,z               deprecated spelling of --pos in the camera modes
///   --screenshot=path            render a few frames, save a PNG, then quit
///   --menu[=mode|chapter|plane]  force the in-game launchscreen even alongside other args (it
///                                otherwise shows only on a bare no-content-arg launch); the
///                                optional screen name opens it there (a --screenshot layout aid)
/// </summary>
public partial class PlaneViewer : Node3D
{
    private const float HorizonScale = 2.5f;

    /// <summary>How many near-miss names a failed <c>--node=</c> lookup offers. A chapter holds
    /// thousands of nodes and a substring like "zep" hits dozens; the point is a usable hint, not
    /// a census.</summary>
    private const int NodeSuggestCap = 20;

    // Cloud-band whiteout color: inside a cloud reads near-white (see OriginalScreenshots/
    // "C1 IA1 whiteout at height.png"), not the 0.69 gray of distance fog. TUNE. The opacity
    // (0 at the band edges → 1 at the opaque core) comes from WeatherState.WhiteoutAmount.
    private static readonly Color WhiteoutColor = new(0.95f, 0.95f, 0.96f);

    // Everything the command line settled, parsed and resolved once (see SessionSpec). _cli is what
    // the user typed; _spec is what the LIVE session was built from — the launchscreen's pick
    // patches it — so every consumer below reads _spec and nothing re-derives a launch setting.
    private SessionSpec _cli = null!;
    private SessionSpec _spec = null!;
    // Per-player pad binding chosen in the launchscreen's join flow (null = derive from the
    // connected roster in AssignPads, which is what every CLI launch does).
    private int[][]? _menuPads;
    // The device-less menu players --debug-join= asks for, held until the launchscreen exists and
    // consumed there: a later return to the menu keeps whoever really joined.
    private int _pendingJoin;
    // Resolves each player's livery and spawn point against _spec (PLAN-planeviewer-split A3);
    // see src/Session/LiveryResolver.cs and src/Session/SpawnPicker.cs.
    private Session.LiveryResolver _liveryResolver = null!;
    private Session.SpawnPicker _spawnPicker = null!;
    // The zone actually rendered: the requested sky zone when this mission defines it, otherwise the first
    // zone its weather.json does (WeatherState.ResolveZone). C5 ships zone1+zone3, so the
    // zone2 default resolves to zone1 there — CONFIRMED correct by playtest, not
    // just a lucky fallback; C1–C4 all define zone2 and resolve to themselves.
    // Reassigned on every StartSession, so a menu rebuild never inherits the last chapter's.
    private string _activeZone = "zone2";
    private SpectatorCamera? _spectator;
    // The session's shared world selection (--freecam/--anim-lab): the clicked leaf plus its
    // cs_name ancestor ladder, which every inspect tool reads instead of picking for itself.
    private UI.SelectionService? _selection;
    // The node lab (N, --freecam/--anim-lab): tree panel, search, per-node actions and the
    // dependency readout for whatever the selection holds.
    private UI.NodeLab? _nodeLab;
    // The world damage lab (H, --freecam/--anim-lab): HP slider + kill/reset on the selection's
    // destructible pool — the interactive twin of --damage-test.
    private UI.WorldDamageLab? _worldDamageLab;
    // The effect/crash stage factory (PLAN-planeviewer-split A4) — builds the world-effects runtime
    // (D32, lazily, on demand for a plane-less session: --destroy, the damage lab's first kill) and
    // each player's crash runtime. Constructed once per session, same lifetime as _liveryResolver.
    private Session.WorldEffectsFactory _worldEffectsFactory = null!;
    // The --screenshot=/--shots=/--frames= state machine and F11/F12's placement print and
    // ad-hoc save (PLAN-planeviewer-split A2) — see src/Testing/CaptureDirector.cs's entry.
    private Testing.CaptureDirector _captureDirector = null!;
    // The master seed every subsystem generator derives from (see Utils.Rng). Pinned runs take the
    // spec's value; everything else draws from the clock, which is why it is resolved here and not
    // in the spec.
    private ulong _masterSeed;

    private Node3D? _plane;
    // The session's simulation clock (see GameClock). Also published as GameClock.Current, which
    // is how the sim consumers scattered through the tree reach it; dropped by ReturnToMenu.
    private GameClock? _clock;
    // The physics-stepped consumers this node drives itself when the clock is not realtime, in
    // the tree order Godot's physics tick would have used. Dropped by ReturnToMenu.
    private ProjectilePool? _projectiles;
    private UI.WeaponLab? _weaponLabNode;
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

    // Session lifecycle (the launchscreen's in-process world rebuild): everything a
    // session builds hangs under _worldRoot, so Esc-to-menu can free it and StartSession run again.
    // The camera, lights and global shader params live on `this` and persist across sessions.
    private Node3D? _worldRoot;    // the current session's subtree (world/plane/HUD/effects)
    private LaunchMenu? _menu;     // the in-game launchscreen (shown on a no-content-arg launch)
    private bool _menuDriven;      // launched into the menu → Esc from flight returns here, not quit
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
    private double _perfClock;
    private int _perfFrames;
    private double _perfProcess, _perfGpu, _perfCpuRender, _perfPhysics;
    private double _perfDraws, _perfPrims, _perfNodes, _perfMem;

    /// <summary>Rendered frames per <c>--perf</c> report. A frame count rather than a wall second
    /// because under the fixed clock one rendered frame is exactly one sim step, so a window is a
    /// fixed amount of <i>simulation</i> and two runs of the same scenario yield the same number
    /// of samples — which is what makes a paired A/B comparable. At the vsync cap it is also
    /// still one report a second, so an interactive run reads as it always did.</summary>
    private const int PerfWindowFrames = 60;

    /// <summary>
    /// --perf: the headless stand-in for the editor's profiler. Godot's visual profiler needs
    /// the editor GUI, but the same numbers are available at runtime — and the one that settles
    /// most questions is the per-viewport measured GPU time, which separates "our shader got
    /// more expensive" from "our C# got more expensive". Every term is a mean over the window,
    /// so a single hitch doesn't read as a regression; A/B two builds by comparing the same line.
    ///
    /// <para><c>physics</c> is Godot's <c>TIME_PHYSICS_PROCESS</c> monitor —
    /// the physics tick, which is where broadphase and narrowphase cost lands. It exists because
    /// `frame`/`fps` sit pinned at the vsync cap in nearly every run here, so they are floors
    /// and cannot show a collision change getting cheaper or dearer; the physics term can move
    /// while the frame time does not. Same caveat as `script`: it is Godot's own monitor, so
    /// trust it as an A/B ratio rather than as an absolute.</para>
    ///
    /// <para>The line carries <c>sim_frame=</c> so a parser can pin each window to the run's
    /// simulation state instead of to a wall moment, and its grammar is flat
    /// <c>key=value</c> — <c>RunTests.ps1 -Perf</c> reads it.</para>
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
        // Counts, and averaged like every other term: a single frame's draw-call count is whatever
        // was in view at the instant the window closed, which moves under a flying camera. These
        // four are the sharp end of the report — a count has no timing noise in it, so a scene that
        // starts drawing (or holding) more says so exactly, while every ms term has to clear a
        // noise band first.
        _perfDraws += Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
        _perfPrims += Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame);
        _perfNodes += Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        _perfMem += Performance.GetMonitor(Performance.Monitor.MemoryStatic);
        if (_perfFrames < PerfWindowFrames)
        {
            return;
        }
        // Locals, not one very long expression: Log takes a single interpolated string (two
        // concatenated ones are a plain string, which would already have formatted its floats in
        // the current culture and so does not compile against it).
        double n = _perfFrames;
        long simFrame = _clock?.Frame ?? 0;
        double wallMs = 1000 * _perfClock;
        double fps = n / _perfClock;
        double frameMs = wallMs / n;
        double scriptMs = 1000 * _perfProcess / n;
        double renderCpuMs = _perfCpuRender / n;
        double gpuMs = _perfGpu / n;
        double physicsMs = 1000 * _perfPhysics / n;
        double draws = _perfDraws / n;
        double prims = _perfPrims / n;
        double nodes = _perfNodes / n;
        double memMb = _perfMem / n / (1024 * 1024);
        Log.Info("perf", $"window sim_frame={simFrame} frames={_perfFrames} wall_ms={wallMs:0.00} fps={fps:0.0} frame_ms={frameMs:0.00} script_ms={scriptMs:0.00} render_cpu_ms={renderCpuMs:0.00} gpu_ms={gpuMs:0.00} physics_ms={physicsMs:0.00} draws={draws:0.0} prims={prims:0.0} nodes={nodes:0.0} mem_mb={memMb:0.00}");
        _perfClock = 0; _perfFrames = 0; _perfProcess = _perfGpu = _perfCpuRender = _perfPhysics = 0;
        _perfDraws = _perfPrims = _perfNodes = _perfMem = 0;
    }

    // Base (chapter-independent) paths + parse state, set once in _Ready; StartSession reads them
    // each (re)build and recomputes the chapter-dependent gamez/texture/mission paths from _spec.Chapter.
    private string _repoRoot = "";
    // This session's startup timing — the always-on [perf] startup line. One per StartSession,
    // published as StartupProfile.Current so the shared build code can record into it, and cleared
    // when the line is emitted.
    private StartupProfile? _startup;
    // Where extracted/ lives. Defaults to _repoRoot; overridden by --data-root= or CSVM_DATA_ROOT
    // so a git worktree can run the game — /extracted/, /CrimsonSkiesGame/ and /tools/ are
    // git-ignored, so a worktree checkout has none of them and cannot otherwise build or verify.
    private string _dataRoot = "";
    private string _planesGamezPath = "";  // extracted/planes.zip — the aircraft models (always this)
    private string _zrdrPath = "";
    private string _soundsPath = "";
    private string _interpPath = "";
    private string _messagesPath = "";
    private string _rofPath = "";          // the extracted UI archive (paint patterns)
    // Constructed once the base paths above are settled; every --dump-*/--run-tests/--*-test/
    // --destroy= probe wrapper delegates to it (see src/Testing/ProbeRunner.cs).
    private Testing.ProbeRunner _probeRunner = null!;

    /// <summary>Whether this session builds the world's colliders — the one definition every
    /// consumer reads, resolved on the spec so the labs and the C overlay cannot spell it
    /// differently from what <see cref="Mech3.WorldSession"/> actually built.</summary>
    private bool BuildsCollision => _spec.BuildsCollision;

    public override void _Ready()
    {
        // The session clock is advanced at the top of this node's _Process, and every sim consumer
        // reads it during the same frame — so this node has to tick first. Godot runs the lowest
        // priority first.
        ProcessPriority = -1000;

        // Load the optional tuning-override file first, before any module reads a Config value.
        // Missing/malformed file → in-code defaults (never throws); see src/Config.cs.
        Config.Load();

        var projectDir = ProjectSettings.GlobalizePath("res://");
        _repoRoot = Path.GetFullPath(Path.Combine(projectDir, ".."));

        // Everything the command line settles, parsed AND resolved in one place (see SessionSpec):
        // the mode arbitration, the --det bundle's membership and the placement routing are all
        // answered by the time this returns. What is left here is the part a pure value cannot do —
        // the globals it deliberately does not touch, and the complaints it holds instead of
        // logging, emitted before anything configures the log so they read in launch order.
        _cli = SessionSpec.Parse(OS.GetCmdlineUserArgs());
        _spec = _cli;
        foreach (var note in _spec.Warnings)
        {
            if (note.Category.Length == 0)
            {
                GD.Print(note.Message);
            }
            else
            {
                Log.Warn(note.Category, $"{note.Message}");
            }
        }

        // Every base path derives from the data root, so it is settled first.
        // Precedence: --data-root= beats CSVM_DATA_ROOT beats the repo root.
        _dataRoot = _repoRoot;
        var dataRootEnv = OS.GetEnvironment("CSVM_DATA_ROOT");
        if (!string.IsNullOrEmpty(dataRootEnv)) _dataRoot = Path.GetFullPath(dataRootEnv);
        if (_spec.DataRoot is { } dataRootArg) _dataRoot = Path.GetFullPath(dataRootArg);
        if (_dataRoot != _repoRoot)
            GD.Print($"data root: {_dataRoot} (repo root {_repoRoot})");

        // An override is used verbatim; everything else derives from the data root.
        var planesGamezPath = Path.Combine(_dataRoot, "extracted", "planes.zip");
        _zrdrPath = _spec.Zrdr ?? Path.Combine(_dataRoot, "extracted", "zrdr.zip");
        _soundsPath = _spec.Sounds ?? Path.Combine(_dataRoot, "extracted", "soundsh.zip");
        _interpPath = _spec.Interp ?? Path.Combine(_dataRoot, "extracted", "interp.json");
        _messagesPath = _spec.Messages ?? Path.Combine(_dataRoot, "extracted", "messages.json");
        _rofPath = _spec.Rof ?? Path.Combine(_dataRoot, "extracted", "rof");

        // The drop-in writes statics every material built afterwards reads, so it is applied here
        // rather than carried as a session value.
        foreach (string name in _spec.TexOverrides)
        {
            TextureDropIn.SetScratchDir(_repoRoot);
            TextureDropIn.AddOverride(name);
        }
        if (_spec.TexCensus)
        {
            TextureDropIn.SetScratchDir(_repoRoot);
            TextureDropIn.EnableCensus(_spec.TexCensusFilter);
        }
        // A connected pad with stick drift steers the free camera and nudges the flight model,
        // which quietly makes a "deterministic" scripted run not one. SDL's hints don't help
        // (Godot 4.7 enumerates the pad regardless), so the switch is ours.
        if (_spec.PadsDisabled)
        {
            Pads.Disabled = true;
        }
        _captureDirector = new Testing.CaptureDirector(_spec);
        _pendingJoin = _spec.DebugJoin;


        // The window is CREATED without focus (`display/window/size/no_focus` in project.godot), so
        // a scripted run never takes the desktop from whoever is using the machine — a full
        // RunTests.ps1 launches the engine about twenty times. Setting the flag here instead was
        // measured not to work: by the time any script runs the window exists and has already
        // activated, and clearing that after the fact does not hand focus back.
        //
        // So the default is inverted, and an INTERACTIVE session asks for focus explicitly. A
        // session is scripted when a flag will drive and end it by itself, or when --no-focus says
        // so outright; everything else is somebody sitting down to play or to look at something,
        // and wants the window it just launched.
        //
        if (!_spec.IsScripted)
        {
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, false);
            DisplayServer.WindowMoveToForeground();
            Log.Debug("core", $"window: focus requested (interactive session)");
        }
        else
        {
            HideScriptedWindow();
        }

        // --no-vsync: let the loop run as fast as it can. A measurement flag, not a display one —
        // with the presentation wait gone, `frame`, `fps` and `script` stop being floors pinned at
        // the refresh rate and start reporting the work actually done. Safe to combine with the
        // fixed clock precisely because that clock advances one sim step per RENDERED frame: the
        // simulation is identical frame for frame, only the wall time it takes changes.
        if (_spec.NoVsync)
        {
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            Engine.MaxFps = 0;
            Log.Info("perf", $"vsync off max_fps=0 — frame/fps/script report work done, not a refresh cap");
        }

        // --debug-anim opens the call-site gates of the anim and sound families, so it is also the
        // legacy spelling of their console filter; an explicit --log= is applied after it and can
        // still narrow either one.
        if (_spec.DebugAnim)
        {
            Log.Configure("anim:debug,sound:debug");
        }
        foreach (string logSpec in _spec.LogSpecs)
        {
            Log.Configure(logSpec);
        }
        // Opened under the session shape's name (it names the file) and before anything else can
        // log. The sink always takes every category at every level; --log= only widens what the
        // console additionally shows.
        Log.Open(_repoRoot, _spec.ModeName);
        foreach (var (old, replacement) in _spec.Deprecated)
        {
            Log.Warn("core", $"deprecated flag={old} use={replacement}");
        }
        // The --det bundle is settled on the spec; what is left here is the two things a pure value
        // cannot do — draw an unpinned seed from the clock, and announce the resolved set.
        //
        // The master seed. A deterministic run and the animation debugger (deterministic by nature
        // — its whole point is an identical replay) pin it. Everything else draws from the clock, so
        // the shipped game keeps its variety. Applied here as well as per session so the dump tools
        // — which quit before any session is built — still draw from the resolved master rather
        // than a zero one.
        _masterSeed = _spec.PinnedSeed ?? Rng.TimeSeed();
        Rng.Reset(_masterSeed, _spec.SeedPinned);
        GD.Print($"rng: master seed {_masterSeed}" + (_spec.SeedPinned ? " (pinned)" : " (--seed=N to pin)"));
        // Announce the whole resolved bundle on one line, so any capture or log carries the exact
        // conditions it was taken under instead of relying on the reader remembering what --det
        // implies. Every constituent is named with its value, including the ones a flag overrode.
        if (_spec.Det)
        {
            // The dev tuning file is git-ignored, so honouring it would make a deterministic capture
            // a function of one machine's uncommitted state: the same command gives different pixels
            // in a checkout and in a worktree, and a golden hash quietly records whatever was being
            // tuned that day. Pass --no-det to capture with your overrides applied.
            int dropped = Config.OverrideCount;
            Config.ClearOverrides();
            ulong liverySeed = _spec.PaintSeedExplicit ? _spec.PaintSeed : Rng.SeedFor(Rng.Paint);
            float dtMs = GameClock.FixedDt * 1000f;
            Log.Info("core", $"det clock=fixed dt_ms={dtMs:0.###} seed={_masterSeed} spawn={_spec.SpawnIndex} livery_seed={liverySeed} pads=off jitter={_spec.JitterDeg:0.###} config=defaults dropped_overrides={dropped} via={_spec.DetVia}");
        }
        else if (_spec.NoDet && (_spec.DetExplicit || _spec.ScriptedBy.Length > 0))
        {
            string wouldBe = _spec.DetExplicit ? "--det" : _spec.ScriptedBy;
            Log.Info("core", $"no-det: {wouldBe} would run deterministically — wall-clock sim clock, unpinned randomness seed={_masterSeed}");
        }
        // Prefer the unpacked sibling folder from ExtractAssets.ps1 -Unzip when it exists (loose
        // JSON/PNG/WAV: no zip decompression at load). Base (chapter-independent) paths resolve now;
        // the chapter-dependent gamez/texture/mission paths resolve per-session in StartSession.
        _planesGamezPath = SessionPaths.PreferUnzipped(planesGamezPath);
        if (_spec.Zrdr == null) { _zrdrPath = SessionPaths.PreferUnzipped(_zrdrPath); }
        if (_spec.Sounds == null) { _soundsPath = SessionPaths.PreferUnzipped(_soundsPath); }
        _probeRunner = new Testing.ProbeRunner(_repoRoot, _dataRoot, _zrdrPath, _soundsPath,
            _interpPath, _messagesPath, _planesGamezPath);

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
        // The shader clock every animated shader reads instead of Godot's TIME. Written each
        // frame from _Process below; registered here because Godot refuses to compile a shader
        // that references an unregistered global.
        //
        // All of these are registered before the dump branches below, which build materials of
        // their own and then quit: registering after them left every --dump-* run emitting a
        // missing-global error that poisons an error census.
        ShaderTime.RegisterGlobal();

        // --dump-markers: a pure-data report (no world, no camera) — print the marker rig table(s)
        // and quit. Placed here, once planes.zbd's path is known, so it runs whether or not any
        // content arg was given; --headless makes it windowless.
        //
        // Each dump quits with its probe's verdict, like --run-tests: a report that could not be
        // produced must not look to a caller like one that came out clean.
        //
        if (_spec.DumpMarkers)
        {
            GetTree().Quit(_probeRunner.DumpMarkers(_spec) ? 0 : 1);
            return;
        }
        // --dump-weapons: the same pure-data pattern for the typed weapons.json reader (B11) —
        // dump every def and assert no key went unmapped.
        if (_spec.DumpWeapons)
        {
            GetTree().Quit(_probeRunner.DumpWeapons(_spec) ? 0 : 1);
            return;
        }
        // --dump-loadout: bind each plane's stock loadout to its built model and report the
        // resolved gun groups + hardpoints (B12) — a missing marker is a loud error here.
        if (_spec.DumpLoadout)
        {
            GetTree().Quit(_probeRunner.DumpLoadout(_spec) ? 0 : 1);
            return;
        }
        // --dump-flight: pure data again — no world and no model, just the zrdr stats stepped
        // through the manoeuvres the original was measured flying.
        if (_spec.DumpFlight)
        {
            GetTree().Quit(_probeRunner.DumpFlight(_spec) ? 0 : 1);
            return;
        }

        // Populate Config's tuning registry by exercising the wired modules once (WarmTuningRegistry),
        // then flag any config.json key that matched no tunable. Both run on every launch, are
        // data-free, and print before flight — so a typo'd or misplaced override is caught loudly at
        // startup rather than silently doing nothing.
        Config.WarmTuningRegistry();
        Config.ReportOrphans();
        // --dump-config: write a fully-populated tuning template (every registered key + its default,
        // nested by block) to the scratch folder and quit — the copy-and-edit source for config.json.
        if (_spec.DumpConfig)
        {
            string dumpPath = Path.Combine(_repoRoot, ".scratch", "config.dump.json");
            Config.DumpConfig(dumpPath);
            GD.Print($"config: wrote {Config.RegisteredCount}-key tuning template to ./.scratch/config.dump.json");
            GetTree().Quit();
            return;
        }

        // Gamepad hotplug: every input read polls Pads.Connected() fresh, so a pad plugged in
        // mid-game works the moment the engine reports it. Log the roster at launch and every
        // connect/disconnect so a silent pad is diagnosable from the console.
        if (Pads.Disabled)
            GD.Print("gamepad: off (--no-pads, or the --det bundle), ignoring every device (keyboard/scripted input only)");
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
        _camera = new Camera3D { Fov = _spec.Fly || _spec.Freecam || _spec.AnimLab ? 62 : 50, Far = 40000f };
        AddChild(_camera);
        _orbit = new OrbitCamera(_camera);
        if (_spec.Yaw is { } argYaw) _orbit.Yaw = argYaw;
        if (_spec.Pitch is { } argPitch) _orbit.Pitch = argPitch;

        // --run-tests: the in-engine assertion suites. Dispatched here, after the camera exists (a
        // suite building a world resolves PLAYER_RANGE from it) and before any session is built —
        // the suites build exactly the world/plane each of them needs and nothing else.
        if (_spec.RunTests)
        {
            int code = _probeRunner.RunTestSuites(_spec, this, _camera, out _clock);
            GetTree().Quit(code);
            return;
        }

        // No content-selecting arg (or an explicit --menu): show the in-game launchscreen
        // (Mode → Chapter → Plane). Its selection derives the session's spec and calls StartSession,
        // so there is exactly one downstream build path. Esc from a menu-launched flight returns
        // here (see ReturnToMenu).
        if (_spec.ShowsMenu)
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
        // The startup timing line, opened before anything is built and closed when the session's
        // first frame is on screen. Published as the ambient Current so WorldSession — which the
        // test harness also drives, with no session around it — can record its phases blind.
        _startup = new StartupProfile(_spec.ModeName, Time.GetTicksMsec())
        {
            Subject = _spec.EmptyStage ? "stage=empty"
                : _spec.NodeName != null ? $"chapter={_spec.Chapter} node={_spec.NodeName}"
                : _spec.WorldMode ? $"chapter={_spec.Chapter}"
                : $"plane={_spec.PlaneName}",
        };
        StartupProfile.Current = _startup;
        _worldRoot = new Node3D { Name = "Session" };
        AddChild(_worldRoot);
        // Built fresh per session: a launchscreen relaunch replaces _spec wholesale (FromMenu),
        // so these must not be cached across a rebuild.
        _liveryResolver = new Session.LiveryResolver(_spec, _rofPath);
        _spawnPicker = new Session.SpawnPicker(_spec);
        _worldEffectsFactory = new Session.WorldEffectsFactory(_spec, _worldRoot,
            () => (_rigs.Count > 0 ? _rigs[0].Camera : _camera) is { } cam ? cam.GlobalPosition : Vector3.Zero);
        // Re-derive every subsystem RNG from the master before anything in the session draws, so a
        // rebuild (Esc to the launchscreen and back) repeats the run rather than continuing it.
        Rng.Reset(_masterSeed, _spec.SeedPinned);
        // One simulation clock per session. --det pins it to a fixed step in every mode; the
        // animation lab is fixed-dt by nature (an accumulator interactively, one step per rendered
        // frame when scripted); everything else runs at the wall delta, which is arithmetically
        // what each consumer used before the clock existed.
        _clock = new GameClock
        {
            Mode = _spec.Det || (_spec.AnimLab && _captureDirector.Pending) ? GameClock.RunMode.FixedStep
                : _spec.AnimLab ? GameClock.RunMode.FixedAccum
                : GameClock.RunMode.Realtime,
        };
        GameClock.Current = _clock;
        _camera.Fov = _spec.Fly || _spec.Freecam || _spec.AnimLab ? 62 : 50;
        // One rig per rendered view, before anything camera-anchored is built (the skydome and
        // weather visuals below are per-rig). Single player reuses the main-viewport camera.
        BuildRigs(_spec.Fly ? _spec.Players : 1);

        // The build body reads the base paths as plain locals (unchanged from when this was inline
        // in _Ready); the chapter-dependent paths are recomputed here so a new launchscreen chapter
        // selection takes effect on rebuild.
        string dataRoot = _dataRoot, zrdrPath = _zrdrPath, soundsPath = _soundsPath,
            interpPath = _interpPath, messagesPath = _messagesPath, planesGamezPath = _planesGamezPath;
        bool mute = _spec.Mute, debugCollision = _spec.DebugCollision;

        string texturesPath = _spec.Textures
            ?? SessionPaths.ChapterTextures(_dataRoot, _spec.Chapter);
        string gamezPath = _spec.Gamez
            ?? (_spec.WorldMode ? SessionPaths.ChapterGamez(_dataRoot, _spec.Chapter)
            : _planesGamezPath);
        var missionZrdrPath = SessionPaths.MissionZrdr(_dataRoot, _spec.Chapter, _spec.Mission);

        // The anim lab keeps the session archives open (WorldSession.Options.KeepArchivesOpen):
        // the AnimLab node owns their disposal so puffers/decals can build at any playhead time.
        // Until that node exists, a failed lab build must close them from the catch below — in
        // every other mode they are `using` locals inside the try, as before.
        TextureArchive? labTextures = null;
        SoundArchive? labSounds = null;
        UI.AnimLab? animLab = null;
        // The --node= subtree's world-frame box, measured at build time and kept for the framing
        // below — see FrameCamera on why the live-tree merge is the wrong instrument here.
        Aabb? nodeAabb = null;
        try
        {
            var sw = Stopwatch.StartNew();
            long mark = StartupProfile.Mark();
            var gamez = GameZ.Load(gamezPath);
            StartupProfile.Record("gamez", mark);
            mark = StartupProfile.Mark();
            var textures = new TextureArchive(texturesPath);
            StartupProfile.Record("textures", mark);
            // The texture archive stays open past this build scope: the data-driven crash bakes its
            // effect puffers lazily at crash time (the same reason --anim-lab keeps it open). The lab
            // owns its copy (labTextures, freed with the lab node); every other mode hands it to the
            // session (_sessionTextures), which ReturnToMenu disposes on teardown so a map reload drops
            // the previous archive instead of leaking it. A failed build disposes it from the catch.
            if (_spec.AnimLab)
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
            mark = StartupProfile.Mark();
            var sounds = haveSounds ? new SoundArchive(soundsPath) : null;
            StartupProfile.Record("sounds", mark);
            using var soundsScope = _spec.AnimLab ? null : sounds;
            labSounds = _spec.AnimLab ? sounds : null;
            mark = StartupProfile.Mark();
            var soundDefs = haveSounds ? SoundDefs.Load(zrdrPath) : null;
            // The SOUND_GROUPS table (weighted random destruction/impact sounds) the one-shot SOUND
            // anim events resolve through — the death explosion's air_mixed_exp_sg picks one of five.
            var soundGroups = haveSounds ? SoundDefs.LoadGroups(zrdrPath) : null;
            StartupProfile.Record("zrdr", mark);
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
            // --node=<cs_name>: resolve the request against the chapter gamez BEFORE anything is
            // built, so a miss reports its candidates and quits instead of half-building a world.
            // Matching is on the source name, never the Godot node name (which is sanitized and
            // auto-renamed); duplicates are normal, so the whole match list is logged and the first
            // is what builds.
            GameZNode? nodeSubtree = null;
            if (_spec.NodeName != null && _spec.WorldMode)
            {
                var matches = WorldBuilder.MatchNodes(gamez, _spec.NodeName);
                if (matches.Count == 0)
                {
                    var near = WorldBuilder.SuggestNodes(gamez, _spec.NodeName, NodeSuggestCap);
                    Log.Warn("world", $"--node='{_spec.NodeName}' matches no node in {_spec.Chapter}'s gamez ({gamez.Nodes.Count} nodes)");
                    if (near.Count > 0)
                    {
                        Log.Warn("world", $"--node= candidates containing '{_spec.NodeName}': {string.Join(", ", near)}");
                    }
                    else
                    {
                        Log.Warn("world", $"--node= no name in {_spec.Chapter} contains '{_spec.NodeName}' either — run the full world with --debug-names to read names off the objects");
                    }
                    _sessionTextures?.Dispose();
                    _sessionTextures = null;
                    GetTree().Quit();
                    return false;
                }
                nodeSubtree = matches[0];
                var labels = new List<string>();
                foreach (var m in matches)
                {
                    labels.Add($"{m.Name}#{m.Index}");
                }
                Log.Info("world", $"--node='{_spec.NodeName}' matched {matches.Count} node(s): {string.Join(", ", labels)}");
                if (matches.Count > 1)
                {
                    Log.Warn("world", $"--node='{_spec.NodeName}' is ambiguous — building the first ({nodeSubtree.Name}#{nodeSubtree.Index}); name a unique node or pick by eye from the list above");
                }
            }
            if (_spec.EmptyStage)
            {
                // No gamez, no mission, no animation program: a flat collidable ground plane under
                // a grid drawn in code. Flight, weapons and colliders work; nothing else is built.
                mark = StartupProfile.Mark();
                var stage = EmptyStage.Build(collision: _spec.Fly || _spec.ForceCollision);
                StartupProfile.Record("world", mark);
                _plane = stage.Root;
                meshInstances = stage.MeshInstanceCount;
                colliders = stage.ColliderCount;
                what = "empty stage";
            }
            else if (_spec.WorldMode)
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
                        Chapter = _spec.Chapter,
                        Mission = _spec.Mission,
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
                        // The damage-test needs the collidable world (its census measures which
                        // destructible geometry is solid and whether death removes it) even though it
                        // runs in the freecam (non-fly) harness. Two interactive levers switch the
                        // same build: --collision, because the collider overlay needs bodies to
                        // draw, and --debug-damage, because a kill's collider count would otherwise
                        // read zero and lie.
                        Collision = BuildsCollision,
                        DebugAnim = _spec.DebugAnim,
                        AnimLod = _spec.AnimLod,
                        DebugDzPaths = _spec.DebugDzPaths,
                        // The lab: quiet stage (ambient playback deferred to its A toggle),
                        // archives kept open for interactive effect builds.
                        KeepArchivesOpen = _spec.AnimLab,
                        AutoStart = !_spec.AnimLab,
                        // The world's dice — RANDOM_WEIGHT verdicts, SOUND_GROUPS picks, crash-debris
                        // scatter — in every mode, not just the lab.
                        RuntimeSeed = Rng.IntSeedFor(Rng.Anim),
                        // --node=: one subtree instead of the whole world (null = the full build).
                        NodeSubtree = nodeSubtree,
                    },
                    gamez, textures, sounds, soundDefs, soundGroups);
                _plane = session.Root;
                var builder = session.Builder;
                cloudDeck = session.CloudDeck;
                // Owned by the session so a teardown drops the previous world's lights.
                _worldLights = session.Lights;
                crashProgram = session.Program;
                worldScene = session.Builder.Scene;
                worldRuntime = session.Runtime;

                // --node=: the built subtree's WORLD-frame box. Computed from the built meshes and
                // the node transforms rather than from GlobalTransform, because the subtree has not
                // joined the scene tree yet — and never from the gamez child_bbox, which is stored
                // in the node's own frame.
                if (nodeSubtree != null && WorldBuilder.DetachedWorldAabb(session.Root) is { } box)
                {
                    nodeAabb = box;
                    Log.Info("world", $"node stage: '{nodeSubtree.Name}'#{nodeSubtree.Index} built, {builder.MeshInstanceCount} mesh instance(s), centre=({box.GetCenter().X:0},{box.GetCenter().Y:0},{box.GetCenter().Z:0}) size=({box.Size.X:0.#},{box.Size.Y:0.#},{box.Size.Z:0.#})");
                }
                else if (nodeSubtree != null)
                {
                    Log.Warn("world", $"node stage: '{nodeSubtree.Name}'#{nodeSubtree.Index} built no geometry at all — it is a group node; the camera framing has nothing to aim at");
                }

                // --damage-test: with the world built and its AnimRuntime bound, drive one
                // destructible's HP through its DAMAGE_SEQUENCE stages and quit — the C22 verify
                // stand-in until F40's interactive HP control lands. The world subtree is added to
                // the tree (ManualAdvance so _Process doesn't double-drive) because C26 ticks the
                // death sequences forward, which reads global transforms — invalid on an out-of-tree
                // node (`!is_inside_tree()` spam). It also makes the C25 collider positions real.
                if (_spec.DamageTest)
                {
                    _worldRoot!.AddChild(_plane);
                    session.Runtime.ManualAdvance = true;
                    _probeRunner.RunDamageTest(_spec, session.Runtime);
                    GetTree().Quit();
                    return false;
                }

                // --effects-test: build the world-effects runtime and play every impact/destruction
                // effect through it, reporting which resolve and which actually build a puffer
                // (WORLD-12: a started def that renders nothing vs one that does), then quit — the D32
                // headless verify. Added to the tree (self-ticking) so puffers spawn and the census
                // is real; the world plane subtree is added so the templates' global transforms hold.
                if (_spec.EffectsTest && worldScene != null)
                {
                    _worldRoot!.AddChild(_plane);
                    var effects = _worldEffectsFactory.BuildWorldEffectsRuntime(gamez, worldScene, textures, session.Program);
                    _probeRunner.RunEffectsTest(_spec, _camera, effects, Session.WorldEffectsFactory.EffectAnimNames);
                    GetTree().Quit();
                    return false;
                }

                // The shared world selection: click-pick plus the cs_name ancestor ladder PgUp/PgDn
                // walks, in the two modes that observe a live world with a cursor. Created here so
                // the anim lab below can bind its camera-follow to it; it joins the tree with the
                // rest of the session, and builds no HUD and no highlight until something is
                // picked, so an unadorned capture is unchanged.
                if (_spec.Freecam || _spec.AnimLab)
                {
                    _selection = new UI.SelectionService(_plane, _camera)
                    {
                        DebugPick = _spec.DebugSelect != null ? UI.SelectionService.ParseDebugPick(_spec.DebugSelect) : null,
                    };
                    // The node lab reads that selection. Its camera is resolved through a
                    // delegate: the freecam is created further down, after this point.
                    _nodeLab = new UI.NodeLab(_plane, _selection, session.Runtime, session.Program,
                        session.Builder.Scene, BuildsCollision)
                    {
                        CameraSource = () => _spectator,
                        DebugSpec = _spec.DebugNodeLab,
                        // The anim lab's timeline strip and transport panel own the bottom of the
                        // window; plain freecam has nothing there.
                        BottomMargin = _spec.AnimLab ? 252 : 16,
                    };
                    // The world damage lab reads the same selection. Its effects runtime is built
                    // on the first damage action, not now — an untouched session pays nothing.
                    var damageScene = session.Builder.Scene;
                    var damageProgram = session.Program;
                    var damageRuntime = session.Runtime;
                    _worldDamageLab = new UI.WorldDamageLab(_selection, damageRuntime, BuildsCollision)
                    {
                        SelectByName = name => _nodeLab?.SelectByName(name) ?? false,
                        EffectsSource = () => _worldEffectsFactory.EnsureWorldEffects(gamez, damageScene, textures,
                            damageProgram, damageRuntime),
                        DebugSpec = _spec.DebugDamage,
                        BottomMargin = _spec.AnimLab ? 252 : 16,
                    };
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
                if (_spec.Fly || _spec.Freecam || _spec.SkyZoneExplicit)
                {
                    mark = StartupProfile.Mark();
                    _edgeExtender = builder.CreateEdgeExtender(session.Clutter);
                    StartupProfile.Record("edge", mark);
                    if (_edgeExtender != null)
                    {
                        _plane.AddChild(_edgeExtender);
                        GD.Print("map edge: rolling mirrored-tile window active");
                    }
                }
                if (_spec.Fly || _spec.Freecam || _spec.SkyZoneExplicit)
                {
                    // The original skydome, anchored to the camera each frame. Scaled up so
                    // plain depth testing keeps it behind everything: a camera-centered dome
                    // looks identical at any scale (zero parallax), and at 2.5× (~22 km
                    // radius) it is beyond the farthest terrain (~17.4 km corner-to-corner)
                    // while well inside the camera's 40 km far plane. In static --chapter mode
                    // only an explicit --sky-zone adds it (an outside orbit view is better
                    // without the enclosing dome; with --pos inside the map it works).
                    // One dome per rig: it follows *a* camera, so each splitscreen pane needs
                    // its own on that player's visual layer.
                    //
                    // The mission's weather.json is loaded FIRST because it owns the zone
                    // table: it is what resolves --sky-zone's default against the zones this
                    // chapter actually ships (C5 has zone1+zone3, not zone2),
                    // and the dome must be built for the same zone the fog comes from.
                    mark = StartupProfile.Mark();
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
                    // is given (deterministic fog/whiteout/puff verification with --pos, same as
                    // the sky-verification path).
                    SetupWeather(textures);
                    StartupProfile.Record("weather", mark);
                }
                meshInstances = builder.MeshInstanceCount;
                colliders = builder.ColliderCount;
                // Read after the domes, since C1's daytime sky layer is a horizon child.
                if (builder.ScrollingModelCount > 0)
                    GD.Print($"texture scroll: {builder.ScrollingModelCount} model(s) animating UVs");
                what = $"chapter {_spec.Chapter} world";

                // The animation debugger (--anim-lab): the lab node owns the clock and the
                // transport; the world above is its quiet stage (AutoStart=false — reset states
                // and mission setup applied, nothing playing until A or --play-anim).
                if (_spec.AnimLab)
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
                    int effectRoots = Session.WorldEffectsFactory.BuildEffectStage(gamez, session.Builder.Scene, labStage);
                    labStage.AddChild(Session.WorldEffectsFactory.BuildCrashAnchorSet());
                    session.Root.AddChild(labStage);
                    session.Runtime.IndexStage(labStage);
                    session.Runtime.PlaceCalledTemplates = true;
                    GD.Print($"anim-lab: stage — {effectRoots} effect template(s) + player anchor set built + indexed");

                    // The spawn the mission would place the player at — the camera starts here so
                    // the interesting part of the map is in view, and (below) an optional parked
                    // plane sits on it. Resolved once so the camera and plane agree.
                    var labSpawns = SpawnPoints.LoadIa(missionZrdrPath, _spec.Scenario);
                    var (spawnPos, spawnLook) = _spawnPicker.ChooseSpawn(labSpawns, missionZrdrPath,
                        _spawnPicker.ChooseSpawnBase(labSpawns), 0, "");

                    // Camera: the freecam SpectatorCamera (RMB look, WASD/QE move), like --freecam,
                    // in place of the orbit view — the lab drives it (Frame/FollowNode) on
                    // play/pick. Starts at the mission spawn; --pos/--direction override.
                    var camPos = _spec.CamPos ?? spawnPos;
                    var camLook = _spec.CamDir is { } labDir ? camPos + labDir : _spec.LookAt ?? spawnLook;
                    var labCam = new SpectatorCamera(_camera, camPos, camLook) { ShowReadout = false };
                    // --node=: the mission spawn is meaningless on a single-subtree stage — frame
                    // the subject instead, unless the tester placed the eye themselves.
                    if (nodeAabb is { } nodeBox && _spec.CamPos == null && _spec.LookAt == null && _spec.CamDir == null)
                    {
                        labCam.Frame(nodeBox);
                    }
                    _worldRoot!.AddChild(labCam);
                    _spectator = labCam;

                    // Optional stage prop: --plane= parks that aircraft at the mission spawn
                    // point. No FlightController — unpainted by default like every static view
                    // (--paint still applies one).
                    if (_spec.PlaneNames.Count > 0)
                    {
                        mark = StartupProfile.Mark();
                        var planesGamez = GameZ.Load(planesGamezPath);
                        StartupProfile.Record("gamez", mark);
                        mark = StartupProfile.Mark();
                        var parkedBuilder = new PlaneBuilder(planesGamez, textures,
                            scheme: _liveryResolver.SchemeFor(0, zrdrPath, randomByDefault: false, _liveryResolver.NewPaintRng(),
                                _liveryResolver.PatternsForPlane(planesGamez, _spec.PlaneName)),
                            patterns: _liveryResolver.Patterns);
                        var parked = parkedBuilder.Build(_spec.PlaneName);
                        StartupProfile.Record("plane", mark);
                        meshInstances += parkedBuilder.MeshInstanceCount;
                        _worldRoot!.AddChild(parked);
                        parked.Position = spawnPos;
                        if ((spawnLook - spawnPos).LengthSquared() > 1e-6f)
                        {
                            parked.LookAtFromPosition(spawnPos, spawnLook, Vector3.Up);
                        }
                        what += $" + parked '{_spec.PlaneName}'";
                    }

                    animLab = new UI.AnimLab(session.Runtime, session.Program, labCam,
                        labStage, textures, sounds, _masterSeed, _spec.PlayAnim,
                        // On a --node= stage the subject IS the stage and is already framed; letting
                        // the lab re-aim on every Play swings the camera off the only object there
                        // (measured: the tower left the frame entirely on its own destruction).
                        autoFrame: _spec.CamPos == null && _spec.LookAt == null && _spec.CamDir == null
                                   && nodeSubtree == null)
                    {
                        // Interactive shows the whole lab UI; a scripted --screenshot hides it so
                        // the 3D shot stays byte-identical — unless --debug-anim-ui forces it on
                        // to capture the timeline (the same convention as --debug-livery).
                        ShowUi = !_captureDirector.Pending || _spec.DebugAnimUi,
                        // The lab's camera follows whichever rung of the shared selection is current.
                        Selection = _selection,
                    };
                    _worldRoot!.AddChild(animLab);
                    GD.Print($"anim-lab: quiet stage, seed {_masterSeed}, fixed dt 1/60"
                             + (_spec.PlayAnim != null ? $", playing '{_spec.PlayAnim}'" : "")
                             + " — freecam (RMB look, WASD/QE move); transport on the button panel,"
                             + " P pause · . step · R restart · F picker · N node lab; click an object to follow");
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
                mark = StartupProfile.Mark();
                var staticPatterns = _liveryResolver.PatternsForPlane(gamez, _spec.PlaneName);
                var staticScheme = _liveryResolver.SchemeFor(0, zrdrPath, randomByDefault: false, _liveryResolver.NewPaintRng(), staticPatterns);
                // In --viewer the LIVERY LAB owns the livery and applies it itself, so the
                // model is built bare and there is one write path for paint (its Repaint).
                // Everywhere else the builder paints at construction as usual.
                var builder = new PlaneBuilder(gamez, textures, damagePanels: _spec.Viewer,
                    scheme: _spec.Viewer ? null : staticScheme, patterns: _liveryResolver.Patterns);
                _plane = builder.Build(_spec.PlaneName);
                StartupProfile.Record("plane", mark);
                meshInstances = builder.MeshInstanceCount;
                what = $"'{_spec.PlaneName}'";

                // Damage lab: per-part HP sliders driving the item-10c damage visuals on the
                // parked plane — the same DamageVisuals/puffer pipeline as flight, with the
                // distance-interval trails burning in place (DamageLab). Present in every
                // --viewer session (H), opened at launch only by --damage.
                if (_spec.Viewer)
                {
                    mark = StartupProfile.Mark();
                    var stats = PlaneStats.Load(zrdrPath, _spec.PlaneName);
                    StartupProfile.Record("zrdr", mark);
                    if (stats.DestroyableParts.Count == 0)
                    {
                        GD.Print($"damage lab: '{_spec.PlaneName}' ({stats.DefName}) has no destroyable_parts");
                    }
                    else
                    {
                        var smoke = Effects.Puffer.MakePuffer(zrdrPath, textures, _worldRoot!, "pufftrails.json", "smokepuffer");
                        var fire = Effects.Puffer.MakePuffer(zrdrPath, textures, _worldRoot!, "pufftrails.json", "firepuffer");
                        var panelTrails = new List<Effects.Puffer>();
                        for (int i = 0; i < 8; i++) // pool one per pdp panel — the lab can flip all of them
                            if (Effects.Puffer.MakePuffer(zrdrPath, textures, _worldRoot!, "pufftrails.json", "firepuffer") is { } pt)
                                panelTrails.Add(pt);
                        var visuals = new DamageVisuals(builder.DamagePanels, _plane, stats, smoke, fire, panelTrails);
                        // the HUD gauge cluster as a lab toggle (user request): the damage
                        // dial mirrors the sliders, blinks on decreases like a flight hit
                        var labGauges = GaugeCluster.Build(gamez, _spec.PlaneName, textures, stats.DestroyableParts);
                        _worldRoot!.AddChild(new DamageLab(stats, visuals, _plane, _spec.DamagePreset, labGauges)
                        {
                            StartHidden = !_spec.DamageLab, // --damage opens it; plain --viewer waits for H
                        });
                        GD.Print($"damage lab: {stats.DestroyableParts.Count} part sliders, " +
                                 $"{visuals.PanelCount} panels, {panelTrails.Count} panel fire trails"
                                 + (_spec.DamageLab ? "" : " (hidden — H)"));
                        what += _spec.DamageLab ? " + damage lab" : " + damage lab (H)";
                    }
                }

                // Livery lab (--viewer): pattern / RGB colour sliders / decal slots, repainting
                // the parked plane live via PlaneBuilder.Repaint. Built hidden-by-default state
                // is "unpainted" unless --paint named a scheme, so an unadorned --viewer
                // screenshot is byte-identical to the pre-paint viewer. L toggles it.
                if (_spec.Viewer && builder.SkinPrefix != null)
                {
                    var lab = new UI.LiveryLab(builder, _liveryResolver.PaintCatalog(zrdrPath), textures, staticScheme,
                        _liveryResolver.Patterns.PatternsFor(builder.SkinPrefix))
                    {
                        DebugShow = _spec.DebugLivery.HasValue,
                        DebugPatternSteps = _spec.DebugLivery ?? 0,
                    };
                    _worldRoot!.AddChild(lab);
                    what += " + livery lab";
                }

                if (_spec.Viewer)
                    what += " + mesh lab";
            }
            _worldRoot!.AddChild(_plane);
            // The shared selection joins after the world does: its pick walk and its highlight box
            // both read GlobalTransform, which on a detached subtree is identity + error spam.
            if (_selection != null)
            {
                _worldRoot!.AddChild(_selection);
            }
            // The node lab joins after the selection, so its first _Process (which carries the
            // scripted dump) runs once the selection's own scripted pick has settled.
            if (_nodeLab != null)
            {
                _worldRoot!.AddChild(_nodeLab);
            }
            // The damage lab joins after the node lab, so a scripted script can select through the
            // node lab's name index on the frame it runs.
            if (_worldDamageLab != null)
            {
                _worldRoot!.AddChild(_worldDamageLab);
            }
            // Mesh lab (--viewer): normals / wireframe+seams / zone boxes / lighting, plus live
            // cull-mode and normal-source overrides. Like the other two labs it is built in every
            // --viewer session and starts hidden (M), so an unadorned viewer screenshot is
            // unchanged; --debug-mesh opens it and presets modes. Built AFTER the plane joins the
            // tree: it reads geometry back through GlobalTransform, which on a detached node
            // returns identity and logs an error per call rather than walking the subtree.
            if (_spec.Viewer && _plane != null)
                _worldRoot!.AddChild(new UI.MeshLab(_plane, PlaneCollider.Build(_plane),
                    _sun, _env, _camera) { DebugSpec = _spec.DebugMesh });
            // Marker overlay (--viewer --plane, key K): the firepoint / pylon / target gizmos on the
            // parked aircraft (item A3). Only on the parked plane — a chapter world has no marker rig
            // — and after the plane joins the tree, since it reads each marker's GlobalPosition. Built
            // hidden unless --markers opened it, so an unadorned viewer screenshot is unchanged.
            if (_spec.Viewer && !_spec.WorldMode && _plane != null)
            {
                _worldRoot!.AddChild(new UI.MarkerOverlay(_plane) { StartHidden = !_spec.MarkersOverlay });
                what += _spec.MarkersOverlay ? " + marker overlay" : " + marker overlay (K)";
            }
            // Weapon lab (--viewer --plane, key W): mount any of the 48 weapons on any of the plane's
            // firepoints/pylons and fire, watching the muzzle flash, tracer/rocket body and impact on a
            // stand-in target wall. Parked plane only (a chapter world has no aircraft marker rig)
            // and after the plane joins the tree — it reads each marker's GlobalTransform. Built in
            // every parked --viewer session so W always toggles it, hidden unless --weapon-lab opened it,
            // so an unadorned viewer screenshot is unchanged (the target + tracers show only while engaged).
            if (_spec.Viewer && !_spec.WorldMode && _plane != null)
            {
                mark = StartupProfile.Mark();
                var labWeapons = WeaponDefs.Load(zrdrPath, Messages.Load(messagesPath));
                StartupProfile.Record("zrdr", mark);
                // Bind the plane's stock loadout so the lab's mounts are the game's named gun groups
                // + pylons (guns fire from gun groups, hardpoints from pylons). A binding failure
                // (or a plane the table omits) leaves it null — the lab falls back to the raw rig.
                Loadout? labLoadout = null;
                foreach (var ldef in StockLoadouts.Load().All.Values)
                {
                    if (ldef.Model == _spec.PlaneName)
                    {
                        try
                        {
                            labLoadout = Loadout.Bind(ldef, _plane, labWeapons);
                        }
                        catch (Exception e)
                        {
                            GD.PushWarning($"weapon lab: could not bind loadout {ldef.Def} — {e.Message}");
                        }
                        break;
                    }
                }
                var weaponLab = new UI.WeaponLab(_plane, labWeapons, labLoadout, textures, _camera, _spec.PlaneName)
                {
                    DebugShow = _spec.WeaponLab,
                    InitialWeapon = _spec.WeaponSelect,
                    InitialMount = _spec.WeaponMount,
                    AutoFireAtStart = _spec.WeaponFire,
                };
                _worldRoot!.AddChild(weaponLab);
                _weaponLabNode = weaponLab;
                // --weapon-test: mount and fire every one of the 48 weapons once and report any that
                // throw, then quit (windowless under --headless). The report is
                // synchronous (Spawn does the muzzle math + pool insert without needing a frame), so no
                // world tick is required.
                if (_spec.WeaponTest)
                {
                    string report = weaponLab.RunSelfTest();
                    GD.Print(report);
                    _probeRunner.WriteScratch("weapon_test.txt", report);
                    GetTree().Quit();
                    return false;
                }
                what += _spec.WeaponLab ? " + weapon lab" : " + weapon lab (W)";
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
            // (or wherever --pos put it), so the interesting part of the map is
            // already in view rather than a corner of empty sea.
            if (_spec.Freecam)
            {
                Vector3 camPos, camLookAt;
                if (_spec.EmptyStage)
                {
                    // The empty stage has no mission and therefore no spawn list: look at the grid
                    // origin, which is where a --stage=empty subject is put.
                    camPos = EmptyStage.CameraPos;
                    camLookAt = Vector3.Zero;
                }
                else
                {
                    var freecamSpawns = SpawnPoints.LoadIa(missionZrdrPath, _spec.Scenario);
                    (camPos, camLookAt) = _spawnPicker.ChooseSpawn(freecamSpawns, missionZrdrPath,
                        _spawnPicker.ChooseSpawnBase(freecamSpawns), 0, "");
                }
                if (_spec.CamPos is { } cp) camPos = cp;
                // The aim: a direction from wherever the eye ended up, or the named point.
                if (_spec.CamDir is { } cd) camLookAt = camPos + cd;
                else if (_spec.LookAt is { } la) camLookAt = la;
                _spectator = new SpectatorCamera(_camera, camPos, camLookAt)
                {
                    // A scripted --screenshot run wants the frame clean of the overlay.
                    ShowReadout = !_captureDirector.Pending,
                };
                _worldRoot!.AddChild(_spectator);
                what += " + freecam";
                GD.Print($"freecam: spectator camera at ({camPos.X:0}, {camPos.Y:0}, {camPos.Z:0}) — " +
                         "hold RMB to look, WASD/QE to move, Shift boost, wheel sets speed; " +
                         "click an object to select it, PgUp/PgDn walk its ancestor ladder (Home/End jump), " +
                         "N opens the node lab, H the damage lab on whatever destructible is selected");
            }

            if (_spec.Fly)
            {
                // Session-wide flight data, loaded once and shared by every player: the aircraft
                // models' gamez, the plane's stats, the sound defs/archive. Only the built nodes
                // and the per-plane state below are per player.
                mark = StartupProfile.Mark();
                // On the empty stage the session gamez IS planes.zbd (there is no chapter world),
                // so there is nothing to load a second time.
                var planesGamez = _spec.EmptyStage ? gamez : GameZ.Load(planesGamezPath);
                StartupProfile.Record("gamez", mark);
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
                var padAssignment = _menuPads ?? Pads.AssignPads(_rigs.Count);
                if (_menuPads != null)
                    Pads.LogPads(_menuPads);
                // One livery RNG for the session, so P1..P4 draw distinct colours from one
                // stream and --paint-seed reproduces the whole field.
                var paintRng = _liveryResolver.NewPaintRng();
                // One spawn list for the session; each player takes the next index (wrapping).
                // The empty stage has no mission, so nothing to read: ChooseSpawn takes the
                // --pos/default override placed over the grid origin.
                var spawnList = _spec.EmptyStage ? null : SpawnPoints.LoadIa(missionZrdrPath, _spec.Scenario);
                int spawnBase = _spawnPicker.ChooseSpawnBase(spawnList);

                // Weapons (M3 wave B): the typed weapons.json catalogue + the stock loadouts, loaded
                // once, and ONE shared projectile/effect pool every player's guns fire into
                // (projectiles live in the shared world, so every splitscreen pane sees them). The
                // pool reuses the session texture/sound archives (tracer/muzzle textures, impact sounds)
                // and the world gamez + its SceneBuilder, so rockets instance their FLYOUT MODEL body
                // (`he_rocket` …) from the chapter's own prototype roots (B14).
                mark = StartupProfile.Mark();
                var weaponMessages = Messages.Load(messagesPath);
                var weaponDefs = WeaponDefs.Load(zrdrPath, weaponMessages);
                var stockLoadouts = StockLoadouts.Load();
                StartupProfile.Record("zrdr", mark);
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
                _projectiles = projectiles;

                // The world-effects runtime (D32): one per session, rendering the impact/destruction
                // puffer effects the world runtime cannot (its factory is gone after the build). A
                // rocket impact plays its named effect here; the world runtime routes a death's
                // CALL_ANIMATION of a curated effect here too. Needs the world's SceneBuilder to stage
                // the templates, so it is built only when the world was.
                if (worldScene != null)
                {
                    var effects = _worldEffectsFactory.BuildWorldEffectsRuntime(gamez, worldScene, textures, crashProgram!);
                    projectiles.EffectSink = (name, pt) => effects.PlayEffectAt(name, pt);
                    if (worldRuntime != null)
                        worldRuntime.ExternalEffect = (name, pt) => effects.Handles(name) && effects.PlayEffectAt(name, pt);
                }

                // Stunt run: the mission's danger-zone objectives from ia.json
                // dzones, positions resolved against this chapter world's gamez, display strings
                // from targets.json → messages.json. --stunt only. Parsed ONCE for the session —
                // every player then races an independent copy of the same zone list, so the
                // archives are read once no matter how many pilots are in.
                StuntMission? stuntZones = null;
                StuntRace? race = null;
                if (_spec.Stunt && _spec.EmptyStage)
                {
                    GD.Print("--stunt has no danger zones on the empty stage (no mission, no world) — flying free");
                }
                else if (_spec.Stunt)
                {
                    stuntZones = StuntMission.Load(gamez, missionZrdrPath, Messages.Load(messagesPath));
                    if (stuntZones == null)
                        // Expected for the chapters whose IA1 has no dzones (C1C, C2B) — a data
                        // fact, not a fault, so a plain line (log hygiene: no stack traces).
                        GD.Print($"--stunt: no danger zones for {_spec.Chapter}/{_spec.Mission} — flying free");
                    else if (_rigs.Count > 1)
                        race = new StuntRace(); // splitscreen: a race, ranked on the shared board
                }

                // The game's own HUD bitmap font (extracted/rimage/5pointhud*.png), loaded once and
                // shared across panes — the E36 weapon readout and the --hud-font-test proof overlay
                // both draw with it. Null (one log line) if the rimage atlas is absent; both are then
                // simply not built.
                HudFont? hudFont = HudFont.Load(Path.Combine(_dataRoot, "extracted", "rimage"));

                // The gun aiming reticle's pipper (E37): the game's own impact_point.png, loaded once
                // and shared across panes (it carries its own alpha — no colour-keying). Null (no file)
                // simply omits the reticle.
                Texture2D? reticleTex = ImpactReticle.LoadTexture(
                    Path.Combine(_dataRoot, "extracted", "rimage"), "impact_point.png");

                for (int pi = 0; pi < _rigs.Count; pi++)
                {
                    var rig = _rigs[pi];
                    bool verbose = pi == 0; // the per-plane detail lines are identical for every player
                    string tag = _rigs.Count > 1 ? $"P{pi + 1} " : "";
                    // Each player flies their own pick (the launchscreen's join flow / a --plane= list);
                    // with one name given, that is the same plane for everyone as before.
                    string planeName = Session.PlaneRoster.PlaneFor(_spec, pi);
                    var stats = StatsFor(planeName);

                    // Flight repaints the field on every map load: each player draws their
                    // own random livery (colours + decals) unless --paint pins one.
                    mark = StartupProfile.Mark();
                    var planeBuilder = new PlaneBuilder(planesGamez, textures, spinningProps: true,
                        scheme: _liveryResolver.SchemeFor(pi, zrdrPath, randomByDefault: false, paintRng,
                            _liveryResolver.PatternsForPlane(planesGamez, planeName)),
                        patterns: _liveryResolver.Patterns);
                    var planeModel = planeBuilder.Build(planeName);
                    StartupProfile.Record("plane", mark);
                    meshInstances += planeBuilder.MeshInstanceCount;

                    var controller = new FlightController
                    {
                        // one scripted sequence per player ('|'-separated); the last covers the rest
                        HoldSegments = _spec.HoldSets == null ? null
                            : _spec.HoldSets[Math.Min(pi, _spec.HoldSets.Length - 1)],
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
                        PinnedView = _spec.View,
                        HudParent = rig.Viewport,
                        AllowPause = _rigs.Count == 1,
                    };
                    controller.AddChild(planeModel);

                    // Guns/hardpoints: bind this plane's stock loadout (or the --loadout override) to
                    // its built model — resolves markers to muzzle nodes + weapons to WeaponDefs.
                    // Set before the controller enters the tree (its _Ready builds the fire state).
                    var loadoutDefName = _spec.LoadoutOverride ?? stats.DefName;
                    if (stockLoadouts.For(loadoutDefName) is { } ldef)
                    {
                        try
                        {
                            controller.Loadout = Loadout.Bind(ldef, planeModel, weaponDefs);
                            controller.Projectiles = projectiles;
                            controller.InfiniteAmmo = _spec.InfiniteAmmo;
                            controller.AutoFire = _spec.AutoFire;
                            controller.AutoFireRockets = _spec.AutoFireRockets;
                            controller.InitialGunSelect = _spec.GunSelect;
                            // --rocket=<wep_id>: swap every hardpoint's ordnance before the model is
                            // mounted and the ordnance-type list is built (controller._Ready). A
                            // testing hook — all 11 stock loadouts carry HE (wep_06), so this is the
                            // only way to prove the mounted model varies by rocket type.
                            if (_spec.RocketOverride != null)
                            {
                                Testing.ProbeRunner.ApplyRocketOverride(controller.Loadout, weaponDefs, _spec.RocketOverride, verbose);
                            }
                            // D44: hang the FLYOUT-model ordnance under the pylons — one body per pylon,
                            // hidden as its ammo depletes. Uses the same gamez prototype the round flies.
                            controller.Ordnance = PylonOrdnance.Build(controller.Loadout, projectiles);
                            if (verbose)
                            {
                                int groups = 0;
                                foreach (var _ in controller.Loadout.FirableGuns) { groups++; }
                                GD.Print($"weapons: {groups} gun group(s), {controller.Loadout.Hardpoints.Count} " +
                                         $"hardpoint(s), guns=Space/pad-B rockets=F/pad-A, " +
                                         $"select guns=G/dpad-L rockets=H/dpad-R" +
                                         (_spec.GunSelect != 0 ? $" [gun-select={_spec.GunSelect}]" : "") +
                                         (_spec.InfiniteAmmo ? " (infinite ammo)" : ""));
                                if (controller.Ordnance is { } ord)
                                {
                                    GD.Print($"pylon ordnance: {ord.Count} mounted rocket model(s)" +
                                             (_spec.RocketOverride != null ? $" (--rocket={_spec.RocketOverride})" : ""));
                                }
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

                    // The bitmap-font proof overlay: draw the sample string on this pane so a 1P view
                    // and a 4P pane can be compared (--hud-font-test). Set before the controller
                    // enters the tree — its _Ready adds this to the HUD canvas.
                    if (hudFont != null && _spec.HudFontTest)
                    {
                        controller.FontTest = new HudFontTest(hudFont, _spec.HudFontTestText);
                        if (verbose)
                            GD.Print($"hud-font-test: '{_spec.HudFontTestText}' via 5pointhud font");
                    }

                    // The selected-weapon text readout (E36): the gun group + rocket type and their
                    // live ammo, drawn in the game's HUD font from the MSG_HUD_GUNGAUGE/MSG_HUD_MISSLES
                    // templates. Built whenever the font loaded and the plane carries a loadout.
                    if (hudFont != null && controller.Loadout != null)
                    {
                        controller.WeaponReadout = WeaponReadout.Build(hudFont, weaponMessages);
                        if (verbose)
                            GD.Print("weapon readout: MSG_HUD_GUNGAUGE/MSG_HUD_MISSLES via 5pointhud font");
                    }

                    // The gun aiming reticle (E37): the ballistic impact point of the selected gun
                    // group at the convergence distance, drawn as the game's pipper — visibly
                    // trailing the nose in a hard turn, on the rounds in steady flight.
                    if (reticleTex != null && controller.Loadout != null)
                    {
                        controller.Reticle = ImpactReticle.Build(reticleTex, rig.Camera);
                        if (verbose)
                            GD.Print("gun reticle: ballistic impact point via impact_point.png");
                    }

                    // Visible damage: torn-skin panel flips + the low-HP smoke/fire
                    // trail (pufftrails.json → dense_firetrail's smokepuffer/firepuffer pair).
                    if (controller.Damage != null)
                    {
                        var smoke = Effects.Puffer.MakePuffer(zrdrPath, textures, controller, "pufftrails.json", "smokepuffer");
                        var fire = Effects.Puffer.MakePuffer(zrdrPath, textures, controller, "pufftrails.json", "firepuffer");
                        // per-panel fire trails (the original streams one from every damaged
                        // panel — clearly visible in OriginalScreenshots/Videos/C1 IA1 Crash.mp4)
                        var panelTrails = new List<Effects.Puffer>();
                        for (int i = 0; i < 4; i++)
                            if (Effects.Puffer.MakePuffer(zrdrPath, textures, controller, "pufftrails.json", "firepuffer") is { } pt)
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
                        controller.DebugCompleteStunt = _spec.DebugScoreboard;
                        // The objective marker HUD, one per pane: projects that player's
                        // active danger zone through THEIR camera, with the edge arrow + clock
                        // bearing + run status.
                        controller.Marker = MarkerHud.Build(controller.Stunt, rig.Camera);
                        if (race != null)
                        {
                            // Racing: no per-player splits board — the shared ranked board
                            // below covers the whole window when the last pilot is in. The marker
                            // HUD shows this player's placing meanwhile.
                            race.Add(pi, controller.Stunt, Session.PlaneRoster.PlaneDisplayName(stats));
                            controller.Race = race;
                            controller.Marker.Race = race;
                            controller.Marker.PlayerIndex = pi;
                        }
                        else
                        {
                            // Solo: the end-of-run scoreboard — per-zone splits + total +
                            // persisted best time, keyed chapter/mission/plane in
                            // user://stunt_scores.json (race totals are deliberately not recorded).
                            var scoreKey = $"{_spec.Chapter}/{_spec.Mission}/{planeName}";
                            controller.Scoreboard = StuntScoreboard.Build(controller.Stunt,
                                Session.PlaneRoster.PlaneDisplayName(stats), $"{_spec.Chapter}   ·   {Session.PlaneRoster.Humanize(_spec.Scenario)}",
                                ScoreStore.Load(), scoreKey);
                            GD.Print($"stunt scoreboard: splits + best time (key '{scoreKey}')");
                        }
                        if (verbose)
                        {
                            GD.Print("stunt marker HUD: projected marker + edge arrow + clock bearing");
                            what += $" [stunt: {controller.Stunt.TotalCount} zones]";
                        }
                    }

                    var (spawnPos, spawnLookAt) = _spawnPicker.ChooseSpawn(spawnList, missionZrdrPath, spawnBase, pi, tag);
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
                        _worldEffectsFactory.BuildFlightCrashRuntime(controller, planeBuilder, planeName, gamez,
                            worldScene, textures, crashProgram, verbose);
                    }
                }
                // The race's shared results board: one ranked row per player, over the
                // WHOLE window rather than inside a pane — the race ends for everybody at once — so
                // it goes on its own CanvasLayer above the splitscreen panes. Any player's R there
                // is a rematch, which restarts every plane, so it routes back through the session.
                if (race != null)
                {
                    var board = StuntRaceBoard.Build(race, $"{_spec.Chapter}   ·   {Session.PlaneRoster.Humanize(_spec.Scenario)}",
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
                        flown.Add($"P{pi + 1} '{Session.PlaneRoster.PlaneFor(_spec, pi)}'");
                    what += $" + splitscreen {string.Join(", ", flown)}";
                }
                else
                {
                    what += $" + '{_spec.PlaneName}' flying";
                }
            }

            // --destroy=<name>: kill a named destructible at session build so a --screenshot
            // captures its destruction with nobody at the controls. Reuses the weapon-damage path —
            // DamageAt runs the full death (the healthy→destroyed swap fires synchronously here; the
            // debris and effects play out as the runtime self-ticks through the screenshot warm-up).
            // The world subtree is already in the tree (added above), so the death's global-transform
            // reads and the effect stage are valid. Flight already built + wired the world-effects
            // runtime (to the projectile pool too); a plane-less --freecam asks for one here so the
            // destruction's fire/smoke still render — gated on --destroy, so a plain --freecam
            // regression builds nothing extra.
            if (_spec.DestroyName != null && worldRuntime != null)
            {
                if (worldScene != null)
                {
                    _worldEffectsFactory.EnsureWorldEffects(gamez, worldScene, textures, crashProgram!, worldRuntime);
                }
                int killed = Testing.ProbeRunner.TriggerDestroy(worldRuntime, _spec.DestroyName, out var destroyBounds);
                what += killed > 0 ? $" + destroyed {killed}× '{_spec.DestroyName}'"
                                   : $" + destroy '{_spec.DestroyName}' (no match)";
                // Auto-frame the plane-less freecam on what it killed, unless the tester placed the
                // camera themselves (--pos/--direction) — so a bare `--freecam --chapter=CX
                // --destroy=name --screenshot=x.png` is a complete, self-framing destruction shot.
                if (killed > 0 && _spectator != null && _spec.CamPos == null && _spec.LookAt == null
                    && _spec.CamDir == null && destroyBounds.Size.LengthSquared() > 0f)
                {
                    _spectator.Frame(destroyBounds);
                }
            }
            else if (_spec.DestroyName != null)
            {
                GD.Print($"--destroy='{_spec.DestroyName}' ignored: no chapter world " +
                         "(pair it with --freecam/--fly + --chapter=)");
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
            if (_captureDirector.Pending)
            {
                GetTree().Quit(1);
            }
            return false;
        }

        // Only the static views frame their subject; flight and the spectator camera (both
        // --freecam and --anim-lab) place their own eye (FrameCamera would yank the freecam back
        // to the world's AABB orbit).
        if (!_spec.Fly && !_spec.Freecam && !_spec.AnimLab)
            FrameCamera(nodeAabb);

        // Mesh lab on the selection (M) — the freecam/anim-lab twin of the viewer's lab above. It
        // owns no subtree until M attaches it to whatever the shared selection has, and restores
        // that subtree exactly when M lets go, so it builds and changes nothing until then.
        if (_selection != null)
        {
            _worldRoot!.AddChild(new UI.MeshLab(_selection, _sun, _env, _camera)
            {
                DebugSpec = _spec.DebugMesh,
                // A scripted capture is about the geometry, not the panel over it.
                ShowPanel = !_captureDirector.Pending,
            });
        }

        // Collider wireframes (C). Built in the modes that observe a live world: it draws what the
        // collision build produced, and says so loudly when the mode built none rather than
        // rendering an empty overlay that reads as "nothing here is solid".
        if ((_spec.Freecam || _spec.AnimLab || _spec.Fly) && _plane != null)
        {
            var planeColliders = new List<(Node3D, PlaneCollider)>();
            foreach (var rig in _rigs)
            {
                if (rig.Controller is { Collider: { } airframe, PlaneModel: { } model })
                {
                    planeColliders.Add((model, airframe));
                }
            }
            _worldRoot!.AddChild(new UI.ColliderOverlay(_plane, BuildsCollision)
            {
                DebugShow = _spec.ShowColliders,
                Planes = planeColliders,
            });
            Log.Info("world", $"collider overlay ready (C){(BuildsCollision ? "" : " — but this mode built NO collision; relaunch with --collision")}");
        }
        else if (_spec.ForceCollision && _spec.WorldMode)
        {
            // The static viewer builds the bodies but binds no overlay: C there cycles the mesh
            // lab's cull override, and silently rebinding a lab key would be worse than saying so.
            Log.Info("world", $"--collision built the world's colliders, but the C overlay is not bound in this mode (C is the mesh lab's cull cycler) — use --freecam to see them");
        }

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
            InitialMode = _spec.DebugNames == null ? UI.NodeLabels.Mode.Off : UI.NodeLabels.ParseMode(_spec.DebugNames),
            // The flown aircraft sits metres from the camera while the world is hundreds of
            // metres away, so without this it wins every label slot. Empty in --viewer, where
            // the parked aircraft IS the subject.
            Deprioritise = flownPlanes,
        });

        // The build is done; everything from here to the first drawn frame is `first_frame`.
        _startup?.EndBuild();
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
        _menu.ShowMenu(_spec.MenuStartScreen);
        // --debug-join=N: synthesize N extra device-less players so the splitscreen aircraft
        // select can be screenshot on a one-controller machine (they can never act, so the
        // shot is deterministic; the last one starts locked to show both panel states).
        if (_pendingJoin > 0)
        {
            _menu.DebugJoin(_pendingJoin);
            _pendingJoin = 0; // one-shot: a return to the menu keeps whoever really joined
        }
    }

    /// <summary>The launchscreen's players locked their picks: derive this session's spec from the
    /// pristine command line, bind each player's pad, and start the session. On a build failure,
    /// return to the menu with a note rather than leave a blank screen.
    ///
    /// <para>The spec is derived from <see cref="_cli"/>, never from the outgoing
    /// <see cref="_spec"/>, so nothing the last session settled can leak into this one. The pads are
    /// the exception on purpose: they come from the join flow rather than from args, so they stay
    /// session state here instead of becoming a spec field.</para></summary>
    private void StartSessionFromMenu(string chapter, IReadOnlyList<LaunchMenu.PlayerChoice> players,
        bool stunt)
    {
        var planes = new List<string>(players.Count);
        foreach (var p in players)
        {
            planes.Add(p.PlaneNode);
        }
        _spec = SessionSpec.FromMenu(_cli, chapter, planes, stunt);
        // Honour the join flow's device binding rather than re-deriving it from the roster: the
        // pad that joined as P2 in the menu must be the pad that flies P2. Single player keeps the
        // any-pad policy (null), so every connected pad flies the one plane, as before.
        _menuPads = null;
        if (_spec.Players > 1)
        {
            _menuPads = new int[_spec.Players][];
            for (int i = 0; i < _spec.Players; i++)
            {
                _menuPads[i] = players[i].Pads;
            }
        }
        _menu!.HideMenu();
        if (!StartSession())
        {
            ReturnToMenu();
            _menu.ShowError($"Could not load {chapter} / {string.Join(", ", _spec.PlaneNames)} — see the log.");
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
        _selection = null;
        _nodeLab = null;
        _worldDamageLab = null;
        // The clock and the consumers it drives explicitly go with the session; a null
        // GameClock.Current puts any node that outlives the teardown back on its raw frame delta.
        _clock = null;
        GameClock.Current = null;
        _projectiles = null;
        _weaponLabNode = null;
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

    /// <summary>Loads the flown mission's weather.json and resolves <see cref="_activeZone"/>:
    /// the zone the fog AND the skydome are both built from. Called before the domes, because
    /// the zone names are per chapter — C5 ships zone1+zone3, so the `zone2` default has to fall
    /// back or C5 renders with no fog and no dome at all. The default stays
    /// `zone2` deliberately; which zone a mission actually flies is in no reader, so it is the
    /// user's A/B against the original (docs/formats/weather.md).</summary>
    private void LoadWeather(string missionZrdrPath)
    {
        _weather = WeatherState.Load(missionZrdrPath);
        _activeZone = _weather?.ResolveZone(_spec.SkyZone) ?? _spec.SkyZone;
        if (_weather == null)
        {
            GD.PushWarning($"no weather.json for {_spec.Chapter}/{_spec.Mission} — flying without fog / whiteout");
            return;
        }
        if (!_activeZone.Equals(_spec.SkyZone, StringComparison.OrdinalIgnoreCase))
            // Not a fault: a chapter that numbers its zones differently resolves here every
            // flight. C5 (zone1/zone3) does so on all 8 missions, and zone1 is the confirmed
            // correct choice there — so this must not read as a missing-data warning.
            GD.Print($"weather: {_spec.Chapter}/{_spec.Mission} has no '{_spec.SkyZone}' "
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
        var fogRange = _spec.NoFog
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
        GD.Print($"weather [{_activeZone}]{(_spec.NoFog ? " --no-fog: fog + whiteout OFF, world light unchanged;" : ":")} " +
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
        // csky_time global + the camera built-ins, so it needs no _Process driving — that uniform
        // is the only handle on it, which is why a halted clock still stops the fall.
        _precip = Effects.Precipitation.Create(_weather.Precip, _weather.CloudBottom, _weather.CloudTop);
        if (_precip != null)
            _worldRoot!.AddChild(_precip);
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

    /// <summary>Smallest orbit radius a synthesized pivot may sit at, so an aim ray that passes
    /// behind the subject still leaves something to orbit rather than spinning about the eye.</summary>
    private const float MinOrbitRadius = 1f;

    /// <summary>Frames the parked plane in the orbit view. <c>--lookat</c> is a true pivot and is
    /// used verbatim; <c>--direction</c> names only an aim, so a pivot is synthesized on the aim
    /// ray — at the subject's nearest approach when an eye was given, otherwise the subject's own
    /// centre with the eye swung round to that direction. Either way the orbit still ORBITS the
    /// subject: dragging turns around it and the wheel dollies toward it.</summary>
    private void FrameCamera(Aabb? subject = null)
    {
        // The subject box. `subject` is the --node= stage's own, measured before the labs joined the
        // subtree: MeshLab parks three EMPTY overlay meshes at the session origin, which a merge
        // over the live tree folds in — harmless for a plane or a whole world (both already contain
        // the origin) and ruinous for one subtree 7 km out, whose box would stretch back to it.
        var aabb = subject ?? OrbitCamera.MergedAabb(_plane!);
        var pivot = _spec.LookAt;
        if (pivot == null && _spec.CamDir is { } dir)
        {
            if (_spec.CamPos is { } eye)
            {
                float ahead = Mathf.Max((aabb.GetCenter() - eye).Dot(dir), MinOrbitRadius);
                pivot = eye + dir * ahead;
                Log.Info("core", $"orbit pivot from --direction: --lookat={Testing.CaptureDirector.Vec3Arg(pivot.Value)} radius={ahead:0.###}");
            }
            else
            {
                // No eye: keep the AABB pivot and the framing distance Frame derives, and place the
                // eye along the aim — the inverse of OrbitCamera.Update's yaw/pitch to offset.
                _orbit.Pitch = Mathf.Asin(Mathf.Clamp(-dir.Y, -1f, 1f));
                _orbit.Yaw = Mathf.Atan2(-dir.X, -dir.Z);
                Log.Info("core", $"orbit pivot from --direction: subject centre, eye swung to the aim");
            }
        }
        _orbit.Frame(aabb, _spec.CamPos, pivot);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    private const int SwHide = 0;

    /// <summary>Takes a scripted run's window off the screen entirely. `no_focus` only stops the
    /// window taking the KEYBOARD — it still opens in front of whatever the user is working in, and
    /// a full RunTests.ps1 does that about twenty times. Godot has no lever for this: there is no
    /// always-on-bottom window flag, and --position is clamped so roughly a third of the window
    /// stays on the desktop whatever you ask for (measured: 5184 and 10000 both land at 4686 on a
    /// 5120-wide desktop). Hiding is not minimizing — a minimized window stops rendering, which
    /// turns the captures blank (SHOT-16).</summary>
    private static void HideScriptedWindow()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        nint hwnd = (nint)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle);
        if (hwnd == 0)
        {
            Log.Debug("core", $"window: no native handle, cannot hide (scripted session)");
            return;
        }
        ShowWindow(hwnd, SwHide);
        Log.Debug("core", $"window: hidden (scripted session)");
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
        else if (what == (int)NotificationExitTree)
        {
            // A run that quits inside the session build (the headless probes) never renders a
            // frame, so this is the only place its startup breakdown can still be reported.
            // Idempotent: a session that did render has already emitted and this does nothing.
            _startup?.Emit();
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

    /// <summary>Steps the consumers whose sim normally rides Godot's physics tick. They return
    /// early from <c>_PhysicsProcess</c> whenever the clock is not realtime (GameClock.PhysicsDt
    /// hands them 0), because a fixed or halted sim cannot be paced by a tick it does not own.
    /// Order is the tree order those callbacks had — the shared projectile pool before the flight
    /// controllers, the weapon lab before the pool it owns — so a round fired this frame behaves
    /// exactly as it did.</summary>
    private void DriveSimSteps(GameClock clock)
    {
        for (int i = 0; i < clock.Steps; i++)
        {
            float dt = clock.Dt;
            _projectiles?.SimStep(dt);
            foreach (var rig in _rigs)
            {
                rig.Controller?.SimStep(dt);
            }
            if (_weaponLabNode != null)
            {
                _weaponLabNode.SimStep(dt);
                _weaponLabNode.Pool.SimStep(dt);
            }
        }
    }

    /// <summary>Whether P / <c>.</c> may halt this session. Splitscreen flight says no: the freeze
    /// halts the shared world, so it is not one player's to press (the same rule
    /// <see cref="FlightController.AllowPause"/> applies to the in-flight binding).</summary>
    private bool HaltAllowed => !_spec.Fly || _rigs.Count == 1;

    public override void _UnhandledInput(InputEvent @event)
    {
        // P halts the sim, . steps it one frame — in freecam, the static viewer and the
        // launchscreen. Flight polls P itself (FlightController, so gamepad Start keeps working)
        // and the animation lab owns its own transport, so neither is handled here.
        if (!_spec.AnimLab && @event is InputEventKey { Pressed: true, Echo: false } clockKey
            && _clock != null && HaltAllowed)
        {
            if (clockKey.Keycode == Key.P && !_spec.Fly)
            {
                _clock.Halted = !_clock.Halted;
                GD.Print(_clock.Halted ? "clock: halted (P resumes, . steps one frame)" : "clock: running");
                return;
            }
            if (clockKey.Keycode == Key.Period)
            {
                _clock.Halted = true;
                _clock.StepOnce();
                return;
            }
        }
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
            Testing.CaptureDirector.SaveScreenshot(GetViewport());
            return;
        }
        // F11 anywhere: print the mode's subject placement as ready-to-paste --pos=/--direction=
        // args, so a hand-framed orbit (or a spot found while flying) can be reproduced for a
        // deterministic --screenshot run.
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F11 })
        {
            _captureDirector.PrintPlacement(_spec, _rigs, _camera, _orbit);
            return;
        }
        if (_spec.Fly || _spec.Freecam || _spec.AnimLab)
            return; // the FlightController / SpectatorCamera owns the camera; no orbit controls
        _orbit.HandleInput(@event);
    }

    public override void _Process(double delta)
    {
        // First thing in the frame (ProcessPriority): decide how much sim time this rendered frame
        // is worth, then — when the clock is not realtime — step the physics-driven consumers
        // ourselves, in the tree order Godot's physics tick would have used.
        if (_clock is { } clock)
        {
            clock.BeginFrame(delta);
            if (clock.ParentDriven)
            {
                DriveSimSteps(clock);
            }
        }
        // The startup line goes out on the frame that proves the first one was drawn.
        _startup?.Frame();
        // Publish the same instant to the shaders, so the animated surfaces (UV scroll,
        // precipitation) and the CPU sim never disagree within a frame. Written unconditionally:
        // with no session clock — the launchscreen, or the frame after a teardown — it keeps
        // running on wall time so nothing on screen stalls behind the menu.
        ShaderTime.Advance(_clock, delta);
        // The diagnostics below stay on wall time: a frame-budget report and a poll for entities
        // an animation has since placed are both instruments, and an instrument that freezes with
        // the thing it measures reports nothing.
        if (_spec.Perf)
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
                c.A = _spec.NoFog ? 0f : _weather.WhiteoutAmount(camPos.Y);
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
            rig.Puffs?.Update(_clock?.FrameDt ?? (float)delta, camPos,
                -rig.Camera.GlobalTransform.Basis.Z);
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

        _captureDirector.Tick(GetViewport(), GetTree(), _spec, _clock, _orbit, _camera, _plane,
            _menu is { Visible: true });
    }

}
