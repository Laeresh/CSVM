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
using SV = CSVM.Testing.Probes.SessionValues;

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

    // Splitscreen with a --pos override: lateral offset between players so they don't spawn
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
    // --stage=<name>: replace the chapter world with a synthetic test stage. 'empty' is the only
    // one — no gamez at all, a generated grid over a collidable ground plane (see EmptyStage).
    private string? _stage;
    private bool _emptyStage;
    // --node=<cs_name>: build ONLY that gamez subtree (--viewer / --anim-lab), camera framed on it.
    private string? _nodeName;
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
    // The session's shared world selection (--freecam/--anim-lab): the clicked leaf plus its
    // cs_name ancestor ladder, which every inspect tool reads instead of picking for itself.
    private UI.SelectionService? _selection;
    // The node lab (N, --freecam/--anim-lab): tree panel, search, per-node actions and the
    // dependency readout for whatever the selection holds.
    private UI.NodeLab? _nodeLab;
    // The world damage lab (H, --freecam/--anim-lab): HP slider + kill/reset on the selection's
    // destructible pool — the interactive twin of --damage-test.
    private UI.WorldDamageLab? _worldDamageLab;
    // The one world-effects runtime a plane-less session builds on demand (--destroy, the damage
    // lab's first kill), so a death's fire and smoke render outside flight.
    private AnimRuntime? _worldEffects;
    // --anim-lab: the animation debugger — the chapter world as a
    // quiet stage under a deterministic fixed-dt clock with def-playback transport (UI.AnimLab).
    // The most specific mode of all, so it wins outright when combined with any other.
    private bool _animLab;
    private string? _playAnim;                      // --play-anim=<name>: play at launch (implies --anim-lab)
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
    private string? _debugSelect; // --debug-select[=x,y[,up]]: scripted click + ladder walk (freecam/anim-lab)
    private string? _debugNodeLab; // --debug-nodelab[=spec]: open the node lab and dump its readouts
    // --collision[=show]: build the world's colliders in a mode that otherwise builds none
    // (freecam/anim-lab/viewer), so the C wireframe overlay has something to draw; =show opens it.
    private bool _forceCollision, _showColliders;
    private string? _debugDamage;  // --debug-damage[=script]: open the world damage lab and run an ordered hp/kill/reset/tick script
    private bool _markersOverlay;      // --markers: open the firepoint/pylon overlay at launch (--viewer)
    private bool _dumpMarkers;         // --dump-markers[=plane]: print the marker rig table(s) and quit
    private string _dumpMarkersPlane = ""; // the optional --dump-markers= filter (model or display name)
    private bool _dumpWeapons;         // --dump-weapons[=wep_NN]: print the typed weapons.json table and quit
    private string _dumpWeaponsFilter = ""; // the optional --dump-weapons= filter (id or NAME substring)
    private bool _dumpLoadout;         // --dump-loadout[=plane]: bind each plane's stock loadout to its model and quit
    private string _dumpLoadoutFilter = ""; // the optional --dump-loadout= filter (def/model/display substring)
    private bool _dumpFlight;          // --dump-flight[=plane]: fly the measured manoeuvres against the original's numbers and quit
    private string _dumpFlightPlane = ""; // the optional --dump-flight= plane (node name); default the flown/default plane
    private bool _dumpConfig;          // --dump-config: write a populated tuning-config template and quit
    // --dump-session: print every resolved launch setting and quit. The one dump that must stay
    // invisible to the resolution it reports — see DumpSession.
    private bool _dumpSession;
    private bool _damageTest;          // --damage-test[=name]: sweep one destructible's HP through its DAMAGE_SEQUENCE stages and quit
    private string _damageTestFilter = ""; // the optional --damage-test= filter (destructible NAME substring)
    private float _damageHd;           // --damage-hd=N: discrete-hit mode — apply N HEALTH_DAMAGE per hit via DamageAt, count hits to destruction (C23)
    private bool _effectsTest;         // --effects-test: play every impact/destruction effect through the world-effects runtime, report which build a puffer, quit (D32)
    private string? _destroyName;      // --destroy=<def|anim|node>: kill matching destructibles at session build so a --screenshot captures the destruction (F42)
    private string? _loadoutOverride;  // --loadout=<def>: bind this loadout def instead of the plane's own (testing)
    private bool _infiniteAmmo;         // --infinite-ammo: guns/hardpoints never deplete
    private bool _autoFire;             // --fire: hold the gun trigger (scripted screenshots / soak runs)
    private bool _autoFireRockets;      // --fire-rockets: hold the rocket trigger (scripted screenshots / soak runs)
    private int _gunSelect;             // --gun-select=N: initial gun group (0-based; only one fires at a time)
    private int _view;                  // --view=N: numpad flight-camera perspective held for the whole run (0 = chase)
    private string? _rocketOverride;    // --rocket=<wep_id>: swap every hardpoint's ordnance (testing — proves the pylon model varies by type; stock is all HE)
    private bool _hudFontTest;          // --hud-font-test: overlay the E34 bitmap-font sample on each pane
    private string _hudFontTestText = "GUNS 30: 2000  ROCKETS 06: 9"; // the sample string
    private bool _weaponLab;            // --weapon-lab[=id]: open the --viewer weapon lab at launch
    private string? _weaponSelect;      // --weapon-lab=<wep_id>: the weapon selected at launch
    private string? _weaponMount;       // --weapon-mount=<name>: the mount (all/firepointN/pylonN) at launch
    private bool _weaponFire;           // --weapon-fire: start the weapon lab auto-firing (clean firing screenshots)
    private bool _weaponTest;           // --weapon-test: mount+fire all 48 weapons once, report, quit
    private bool _runTests;             // --run-tests[=filter]: run the in-engine assertion suites and quit
    private string _runTestsFilter = ""; // the optional --run-tests= suite-name filter
    private int _spawnIndex = -1;      // --spawn=N forces a spawn; <0 = random pick (like the original)
    private Vector3? _spawnAt;         // the flight spawn override --pos resolves into (deprecated --spawn-at)
    private Vector3? _spawnDir;        // the nose direction there, world space, default -Z (deprecated --spawn-dir)
    private int _players = 1;          // --players=N: splitscreen panes/planes; 1 = single player
    private (FlightInput, float)[][]? _holdSets; // --hold: one scripted sequence per player ('|'-separated)
    // The camera eye --pos resolves into (deprecated --campos), and --lookat: a POINT, which is the
    // orbit view's pivot and the freecam's aim when no direction was given.
    private Vector3? _camPos, _lookAt;
    // --pos=x,y,z / --direction=x,y,z: the one placement pair, whatever the mode. They place the
    // SUBJECT — the camera in --freecam/--viewer/--anim-lab, the plane (spawn position + nose
    // direction) in --fly/--stunt — and ResolvePlacement routes them onto the per-mode plumbing
    // below, so nothing downstream has to ask which mode it is in.
    private Vector3? _pos;
    private Vector3? _direction;
    // --direction in a camera mode: the aim, held apart from _lookAt because a direction names no
    // pivot and the orbit view needs one (see FrameCamera).
    private Vector3? _camDir;
    // Deprecated spellings seen on the command line, reported once each with their replacement.
    private readonly List<(string Old, string New)> _deprecated = new();
    private string? _screenshotPath;
    private int _screenshotFrames = 15;
    private int _screenshotShots = 1;  // --shots=N: consecutive frames to capture (z-fight debug)
    private int _shotIndex;            // 0-based index of the shot being written
    private float _jitterDeg = -1f;    // --jitter=<deg> burst camera dither; <0 = auto per --shots
    private Transform3D? _shotBaseXform;  // camera pose captured at the first burst frame
    private Vector3 _shotPivot;           // micro-orbit centre (keeps the subject framed)

    // --det: the deterministic bundle — fixed-dt sim clock, pinned master seed, spawn index 0,
    // pinned liveries, no gamepads, no camera dither — so frame N is the same sim state, and the
    // same pixels, on every run whatever the render rate. Scripted runs turn it on themselves.
    private bool _det;
    // --no-det: opt back out, so a scripted run measures wall-clock behaviour and live randomness.
    private bool _noDet;
    // --seed=N: the master seed every subsystem generator derives from (see Utils.Rng). Null until
    // resolved in _Ready: pinned runs take Rng.DefaultSeed, everything else draws from the clock.
    private ulong? _seed;
    private ulong _masterSeed;
    private bool _seedPinned;

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
    private bool _noVsync;         // --no-vsync: uncap the loop so the frame timings stop being floors
    private bool _perf;            // --perf: log the CPU/GPU frame-time split once per window
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
    // each (re)build and recomputes the chapter-dependent gamez/texture/mission paths from _chapter.
    private string _repoRoot = "";
    // The session shape (fly / freecam / viewer / …), settled once the args are parsed: it names
    // the log file and identifies the startup timing line.
    private string _mode = "";
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

    /// <summary>Whether this session builds the world's colliders — the one definition every
    /// consumer reads. Flight needs them to fly into things; the headless damage sweep and the
    /// world damage lab need them because a kill's collider count would otherwise read zero and
    /// lie; <c>--collision</c> is the interactive request for them in a mode that builds none.
    /// The labs and the C overlay must agree with what <see cref="Mech3.WorldSession"/> actually
    /// built, or they report an absence they created themselves.</summary>
    private bool BuildsCollision => _fly || _damageTest || _forceCollision || _debugDamage != null;

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
        var logSpecs = new List<string>(); // applied after the loop, so --debug-anim's implied one comes first
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--plane=")) { ParsePlanes(arg["--plane=".Length..]); hasContentArg = true; }
            else if (arg == "--viewer") { _viewerMode = true; hasContentArg = true; }
            else if (arg == "--damage") { _damageLab = true; _viewerMode = true; hasContentArg = true; }
            else if (arg.StartsWith("--damage=")) { _damageLab = true; _viewerMode = true; _damagePreset = ParseDamagePreset(arg["--damage=".Length..]); hasContentArg = true; }
            else if (arg == "--chapter") { _chapterGiven = true; hasContentArg = true; }
            else if (arg.StartsWith("--chapter=")) { _chapter = arg["--chapter=".Length..]; _chapterGiven = true; hasContentArg = true; }
            else if (arg.StartsWith("--stage=")) { _stage = arg["--stage=".Length..]; hasContentArg = true; }
            else if (arg.StartsWith("--node=")) { _nodeName = arg["--node=".Length..]; hasContentArg = true; }
            else if (arg == "--fly") { _fly = true; hasContentArg = true; }
            else if (arg == "--stunt") { _stunt = true; hasContentArg = true; }
            else if (arg == "--freecam") { _freecam = true; hasContentArg = true; }
            else if (arg == "--anim-lab") { _animLab = true; hasContentArg = true; }
            else if (arg.StartsWith("--play-anim=")) { _playAnim = arg["--play-anim=".Length..]; _animLab = true; hasContentArg = true; }
            else if (arg.StartsWith("--seed=")) { _seed = ulong.Parse(arg["--seed=".Length..]); }
            else if (arg == "--debug-anim-ui") { _debugAnimUi = true; _animLab = true; hasContentArg = true; }
            else if (arg == "--debug-anim") _debugAnim = true;
            // A connected pad with stick drift steers the free camera and nudges the flight
            // model, which quietly makes a "deterministic" scripted screenshot not one. SDL's
            // hints don't help (Godot 4.7 enumerates the pad regardless), so the switch is ours.
            else if (arg == "--no-pads") Pads.Disabled = true;
            else if (arg == "--det") _det = true;
            else if (arg == "--no-det") _noDet = true;
            else if (arg == "--perf") _perf = true;
            else if (arg.StartsWith("--log=")) logSpecs.Add(arg["--log=".Length..]);
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
            else if (arg == "--debug-select") _debugSelect ??= "";
            else if (arg.StartsWith("--debug-select=")) _debugSelect = arg["--debug-select=".Length..];
            else if (arg == "--debug-nodelab") _debugNodeLab ??= "";
            else if (arg.StartsWith("--debug-nodelab=")) _debugNodeLab = UI.NodeLab.ParseDebugSpec(arg["--debug-nodelab=".Length..]);
            else if (arg == "--collision") _forceCollision = true;
            else if (arg.StartsWith("--collision="))
            {
                _forceCollision = true;
                string want = arg["--collision=".Length..];
                _showColliders = want == "show";
                if (!_showColliders && want.Length > 0)
                {
                    Log.Warn("world", $"--collision='{want}' is not a value it takes (only '=show', which opens the C overlay) — building collision anyway");
                }
            }
            // The scripted C press on its own, deliberately WITHOUT forcing the build: it is what
            // proves the overlay reports "this mode built no collision" instead of drawing nothing.
            else if (arg == "--debug-colliders") _showColliders = true;
            else if (arg == "--debug-damage") _debugDamage ??= "";
            else if (arg.StartsWith("--debug-damage=")) _debugDamage = UI.WorldDamageLab.ParseDebugSpec(arg["--debug-damage=".Length..]);
            else if (arg == "--markers") { _markersOverlay = true; _viewerMode = true; hasContentArg = true; }
            else if (arg == "--dump-markers") _dumpMarkers = true;
            else if (arg.StartsWith("--dump-markers=")) { _dumpMarkers = true; _dumpMarkersPlane = arg["--dump-markers=".Length..]; }
            else if (arg == "--dump-weapons") _dumpWeapons = true;
            else if (arg.StartsWith("--dump-weapons=")) { _dumpWeapons = true; _dumpWeaponsFilter = arg["--dump-weapons=".Length..]; }
            else if (arg == "--dump-loadout") _dumpLoadout = true;
            else if (arg.StartsWith("--dump-loadout=")) { _dumpLoadout = true; _dumpLoadoutFilter = arg["--dump-loadout=".Length..]; }
            else if (arg == "--dump-flight") _dumpFlight = true;
            else if (arg.StartsWith("--dump-flight=")) { _dumpFlight = true; _dumpFlightPlane = arg["--dump-flight=".Length..]; }
            else if (arg == "--dump-config") _dumpConfig = true;
            else if (arg == "--dump-session") _dumpSession = true;
            // Builds the chapter world (via the freecam path) so a bound AnimRuntime exists, then
            // sweeps one destructible's HP after build; --freecam gives it the world without a plane.
            else if (arg == "--damage-test") { _damageTest = true; _freecam = true; hasContentArg = true; }
            else if (arg.StartsWith("--damage-test=")) { _damageTest = true; _damageTestFilter = arg["--damage-test=".Length..]; _freecam = true; hasContentArg = true; }
            else if (arg.StartsWith("--damage-hd=")) _damageHd = float.Parse(arg["--damage-hd=".Length..], System.Globalization.CultureInfo.InvariantCulture);
            else if (arg == "--effects-test") { _effectsTest = true; _freecam = true; hasContentArg = true; }
            // A pure modifier (like --damage-hd): needs a chapter world to have anything to destroy,
            // but forces no mode — the caller composes it with --freecam (+ --pos/--direction) for a
            // framed, controller-less destruction shot, or with flight for a chase-cam one.
            else if (arg.StartsWith("--destroy=")) _destroyName = arg["--destroy=".Length..];
            else if (arg.StartsWith("--loadout=")) _loadoutOverride = arg["--loadout=".Length..];
            else if (arg == "--infinite-ammo") _infiniteAmmo = true;
            else if (arg == "--fire") _autoFire = true;
            else if (arg == "--fire-rockets") _autoFireRockets = true;
            else if (arg.StartsWith("--gun-select=")) _gunSelect = int.Parse(arg["--gun-select=".Length..]);
            else if (arg.StartsWith("--rocket=")) _rocketOverride = arg["--rocket=".Length..];
            else if (arg == "--hud-font-test") _hudFontTest = true;
            else if (arg.StartsWith("--hud-font-test=")) { _hudFontTest = true; _hudFontTestText = arg["--hud-font-test=".Length..]; }
            else if (arg == "--weapon-lab") { _weaponLab = true; _viewerMode = true; hasContentArg = true; }
            else if (arg.StartsWith("--weapon-lab=")) { _weaponLab = true; _weaponSelect = arg["--weapon-lab=".Length..]; _viewerMode = true; hasContentArg = true; }
            else if (arg.StartsWith("--weapon-mount=")) { _weaponMount = arg["--weapon-mount=".Length..]; _viewerMode = true; hasContentArg = true; }
            else if (arg == "--weapon-fire") { _weaponFire = true; _viewerMode = true; hasContentArg = true; }
            else if (arg == "--weapon-test") { _weaponTest = true; _viewerMode = true; hasContentArg = true; }
            else if (arg == "--run-tests") _runTests = true;
            else if (arg.StartsWith("--run-tests=")) { _runTests = true; _runTestsFilter = arg["--run-tests=".Length..]; }
            else if (arg.StartsWith("--mission=")) _mission = arg["--mission=".Length..];
            else if (arg.StartsWith("--scenario=")) { _scenario = arg["--scenario=".Length..]; _scenarioExplicit = true; }
            else if (arg.StartsWith("--spawn=")) _spawnIndex = int.Parse(arg["--spawn=".Length..]);
            else if (arg.StartsWith("--spawn-at=")) { _spawnAt = ParseVec3(arg["--spawn-at=".Length..]); Deprecated("--spawn-at", "--pos"); }
            else if (arg.StartsWith("--spawn-dir=")) { _spawnDir = ParseVec3(arg["--spawn-dir=".Length..]); Deprecated("--spawn-dir", "--direction"); }
            else if (arg.StartsWith("--sky-zone=")) { _skyZone = arg["--sky-zone=".Length..]; _skyZoneExplicit = true; }
            else if (arg.StartsWith("--data-root=")) { /* resolved before this loop — every base path derives from it */ }
            else if (arg.StartsWith("--gamez=")) { _gamezPath = arg["--gamez=".Length..]; _gamezOverridden = true; }
            else if (arg.StartsWith("--textures=")) { _texturesPath = arg["--textures=".Length..]; _texturesOverridden = true; }
            else if (arg.StartsWith("--zrdr=")) { _zrdrPath = arg["--zrdr=".Length..]; _zrdrOverridden = true; }
            else if (arg.StartsWith("--interp=")) _interpPath = arg["--interp=".Length..];
            else if (arg.StartsWith("--sounds=")) { _soundsPath = arg["--sounds=".Length..]; _soundsOverridden = true; }
            else if (arg.StartsWith("--messages=")) _messagesPath = arg["--messages=".Length..];
            else if (arg == "--no-fog") _noFog = true;
            else if (arg.StartsWith("--tex-override=")) { TextureDropIn.SetScratchDir(_repoRoot); TextureDropIn.AddOverride(arg["--tex-override=".Length..]); }
            else if (arg == "--tex-census") { TextureDropIn.SetScratchDir(_repoRoot); TextureDropIn.EnableCensus(""); }
            else if (arg.StartsWith("--tex-census=")) { TextureDropIn.SetScratchDir(_repoRoot); TextureDropIn.EnableCensus(arg["--tex-census=".Length..]); }
            else if (arg == "--no-focus") _noFocus = true;
            else if (arg == "--no-vsync") _noVsync = true;
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
            else if (arg.StartsWith("--pos=")) _pos = ParseVec3(arg["--pos=".Length..]);
            else if (arg.StartsWith("--direction=")) _direction = ParseVec3(arg["--direction=".Length..]);
            else if (arg.StartsWith("--campos=")) { _camPos = ParseVec3(arg["--campos=".Length..]); Deprecated("--campos", "--pos"); }
            else if (arg.StartsWith("--lookat=")) _lookAt = ParseVec3(arg["--lookat=".Length..]);
            else if (arg.StartsWith("--view=")) _view = ParseView(arg["--view=".Length..]);
        }

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
        // --dump-session is deliberately NOT a term of this predicate, even though it drives and
        // ends a session by itself: it reports the predicate, so a run carrying it must resolve
        // exactly as the same command line without it. It still must not grab focus, so the
        // observer is applied to the DECISION instead of folded into the rule.
        bool scriptedSession = _noFocus || _screenshotPath != null || _runTests
            || _dumpMarkers || _dumpWeapons || _dumpLoadout || _dumpConfig
            || _damageTest || _effectsTest || _weaponTest;
        if (!scriptedSession && !_dumpSession)
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
        if (_noVsync)
        {
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            Engine.MaxFps = 0;
            Log.Info("perf", $"vsync off max_fps=0 — frame/fps/script report work done, not a refresh cap");
        }

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
        // --node= is a single-subtree INSPECTION stage: the static viewer unless the anim lab was
        // asked for. Neither flight nor the spectator view has anything to do with one object, so
        // an explicit combination is a contradiction and the stage wins.
        if (_nodeName != null && !_animLab)
        {
            if (_fly || _stunt || _freecam)
            {
                GD.Print("--node= is a single-subtree inspection stage; ignoring --fly/--stunt/--freecam");
                _fly = _stunt = _freecam = false;
            }
            _viewerMode = true;
            _chapterGiven = true; // the subtree comes out of the chapter's gamez
        }
        if (hasContentArg && !_viewerMode && !_freecam && !_animLab)
            _fly = true;
        // The numpad views orbit a FLYING plane; the other modes have their own cameras (the
        // viewer's orbit, the spectator freecam) placed with --pos/--direction instead.
        if (_view != 0 && !_fly)
        {
            Log.Warn("core", $"--view={_view} is a flight camera; ignoring it outside --fly/--stunt");
            _view = 0;
        }
        // The shared selection lives in the two world-observation modes; the static viewer's LMB is
        // already the orbit drag, and flight has no cursor.
        if (_debugSelect != null && !_freecam && !_animLab)
        {
            Log.Warn("ui", $"--debug-select is a --freecam/--anim-lab tool; ignoring it here");
            _debugSelect = null;
        }
        if (_debugNodeLab != null && !_freecam && !_animLab)
        {
            Log.Warn("ui", $"--debug-nodelab is a --freecam/--anim-lab tool; ignoring it here");
            _debugNodeLab = null;
        }
        if (_debugDamage != null && !_freecam && !_animLab)
        {
            Log.Warn("ui", $"--debug-damage is a --freecam/--anim-lab tool; ignoring it here (--damage-test is the headless twin)");
            _debugDamage = null;
        }
        // --stage= replaces the chapter world outright, so it is a flight/spectator affair: there
        // is no gamez to inspect, which is what the static viewer and the anim lab exist for.
        if (_stage != null)
        {
            if (!string.Equals(_stage, "empty", StringComparison.OrdinalIgnoreCase))
            {
                GD.Print($"--stage='{_stage}' is not a known stage (only 'empty'); ignoring");
            }
            else if (_viewerMode || _animLab || _nodeName != null)
            {
                GD.Print("--stage=empty has no gamez to inspect; ignoring it in --viewer/--anim-lab/--node=");
            }
            else
            {
                _emptyStage = true;
            }
        }
        // The static viewer shows a chapter world when asked for one, else the parked plane.
        // Flight, the spectator view and the anim lab always need the world built.
        _worldMode = !_emptyStage && (_fly || _freecam || _animLab || (_viewerMode && _chapterGiven));
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
        // --debug-anim opens the call-site gates of the anim and sound families, so it is also the
        // legacy spelling of their console filter; an explicit --log= is applied after it and can
        // still narrow either one.
        if (_debugAnim)
        {
            Log.Configure("anim:debug,sound:debug");
        }
        foreach (string spec in logSpecs)
        {
            Log.Configure(spec);
        }
        // Opened once the mode is settled (it names the file) and before anything else can log.
        // The sink always takes every category at every level; --log= only widens what the
        // console additionally shows.
        _mode = _animLab ? "anim-lab"
            : _damageTest || _effectsTest || _weaponTest || _runTests ? "test"
            : _dumpMarkers || _dumpWeapons || _dumpLoadout || _dumpConfig ? "dump"
            : _freecam ? "freecam"
            : _viewerMode ? "viewer"
            : _stunt ? "stunt"
            : _fly ? "fly"
            : "menu";
        Log.Open(_repoRoot, _mode);
        // The --det bundle, resolved in one place. A scripted run — a capture, a dump report, a
        // test harness — has no operator at the controls and wants to be reproducible, so those
        // flags turn --det on by themselves and noise becomes opt-in through --no-det.
        //
        // The boundary the bundle must never cross is the interactive one: a bare --fly keeps its
        // random spawn, random liveries and live gamepads, because playtest variety is a feature.
        bool detExplicit = _det;
        string scriptedBy =
            _screenshotPath != null ? "--screenshot"
            : _dumpMarkers ? "--dump-markers"
            : _dumpWeapons ? "--dump-weapons"
            : _dumpLoadout ? "--dump-loadout"
            : _dumpFlight ? "--dump-flight"
            : _dumpConfig ? "--dump-config"
            : _damageTest ? "--damage-test"
            : _effectsTest ? "--effects-test"
            : _weaponTest ? "--weapon-test"
            : _runTests ? "--run-tests"
            : "";
        if (scriptedBy.Length > 0 && !_noDet)
        {
            _det = true;
        }
        // The explicit opt-out wins over the implication and over an explicit --det alike: there is
        // one way to ask for wall-clock behaviour, whatever else is on the command line.
        if (_noDet)
        {
            _det = false;
        }
        string detVia = detExplicit ? "--det" : scriptedBy;
        if (_det)
        {
            // A pinned CHOICE beats a pinned dice roll: a seeded pick still moves if the mission's
            // spawn list grows, index 0 does not. An explicit --spawn=N still wins.
            if (_spawnIndex < 0)
            {
                _spawnIndex = 0;
            }
            // A connected pad with stick drift steers the free camera and nudges the flight model.
            Pads.Disabled = true;
        }
        // Burst captures dither the camera by default so z-fighting flickers across frames;
        // a single shot never jitters. --det defaults it off instead: the dither exists to defeat
        // bit-identical frames, which is the one property a deterministic run is for.
        // --jitter=<deg> overrides either way (0 disables).
        if (_jitterDeg < 0f)
        {
            _jitterDeg = _screenshotShots > 1 && !_det ? 0.15f : 0f;
        }
        // The master seed. A deterministic run and the animation debugger (deterministic by nature
        // — its whole point is an identical replay) pin it. Everything else draws from the clock, so
        // the shipped game keeps its variety.
        _seedPinned = _seed != null || _det || _animLab;
        _masterSeed = _seed ?? (_seedPinned ? Rng.DefaultSeed : Rng.TimeSeed());
        // Applied here as well as per session so the dump tools — which quit before any session is
        // built — still draw from the resolved master rather than a zero one.
        Rng.Reset(_masterSeed, _seedPinned);
        GD.Print($"rng: master seed {_masterSeed}" + (_seedPinned ? " (pinned)" : " (--seed=N to pin)"));
        // Announce the whole resolved bundle on one line, so any capture or log carries the exact
        // conditions it was taken under instead of relying on the reader remembering what --det
        // implies. Every constituent is named with its value, including the ones a flag overrode.
        if (_det)
        {
            // The dev tuning file is git-ignored, so honouring it would make a deterministic capture
            // a function of one machine's uncommitted state: the same command gives different pixels
            // in a checkout and in a worktree, and a golden hash quietly records whatever was being
            // tuned that day. Pass --no-det to capture with your overrides applied.
            int dropped = Config.OverrideCount;
            Config.ClearOverrides();
            ulong liverySeed = _paintSeedExplicit ? _paintSeed : Rng.SeedFor(Rng.Paint);
            float dtMs = GameClock.FixedDt * 1000f;
            Log.Info("core", $"det clock=fixed dt_ms={dtMs:0.###} seed={_masterSeed} spawn={_spawnIndex} livery_seed={liverySeed} pads=off jitter={_jitterDeg:0.###} config=defaults dropped_overrides={dropped} via={detVia}");
        }
        else if (_noDet && (detExplicit || scriptedBy.Length > 0))
        {
            string wouldBe = detExplicit ? "--det" : scriptedBy;
            Log.Info("core", $"no-det: {wouldBe} would run deterministically — wall-clock sim clock, unpinned randomness seed={_masterSeed}");
        }
        ResolvePlacement();
        // The empty stage has no mission spawn list to draw from, so the subject starts over the
        // grid origin. Routed through the same _spawnAt/_camPos fields --pos resolves into, which
        // is why this runs after ResolvePlacement — an explicit placement still wins.
        if (_emptyStage)
        {
            if (_fly)
            {
                _spawnAt ??= new Vector3(0f, EmptyStage.SpawnAltitude, 0f);
            }
            else
            {
                _camPos ??= EmptyStage.CameraPos;
            }
        }
        // Prefer the unpacked sibling folder from ExtractAssets.ps1 -Unzip when it exists (loose
        // JSON/PNG/WAV: no zip decompression at load). Base (chapter-independent) paths resolve now;
        // the chapter-dependent gamez/texture/mission paths resolve per-session in StartSession.
        _planesGamezPath = SessionPaths.PreferUnzipped(planesGamezPath);
        if (!_zrdrOverridden) _zrdrPath = SessionPaths.PreferUnzipped(_zrdrPath);
        if (!_soundsOverridden) _soundsPath = SessionPaths.PreferUnzipped(_soundsPath);

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
        // --dump-session goes first, so it can report a command line that carries another dump
        // (the dump flags are part of what it resolves) rather than being pre-empted by it.
        if (_dumpSession)
        {
            GetTree().Quit(DumpSession(hasContentArg, playersExplicit, scriptedSession,
                detExplicit, scriptedBy, detVia) ? 0 : 1);
            return;
        }
        if (_dumpMarkers)
        {
            GetTree().Quit(DumpMarkers() ? 0 : 1);
            return;
        }
        // --dump-weapons: the same pure-data pattern for the typed weapons.json reader (B11) —
        // dump every def and assert no key went unmapped.
        if (_dumpWeapons)
        {
            GetTree().Quit(DumpWeapons() ? 0 : 1);
            return;
        }
        // --dump-loadout: bind each plane's stock loadout to its built model and report the
        // resolved gun groups + hardpoints (B12) — a missing marker is a loud error here.
        if (_dumpLoadout)
        {
            GetTree().Quit(DumpLoadout() ? 0 : 1);
            return;
        }
        // --dump-flight: pure data again — no world and no model, just the zrdr stats stepped
        // through the manoeuvres the original was measured flying.
        if (_dumpFlight)
        {
            GetTree().Quit(DumpFlight() ? 0 : 1);
            return;
        }

        // Populate Config's tuning registry by exercising the wired modules once (WarmTuningRegistry),
        // then flag any config.json key that matched no tunable. Both run on every launch, are
        // data-free, and print before flight — so a typo'd or misplaced override is caught loudly at
        // startup rather than silently doing nothing.
        WarmTuningRegistry();
        Config.ReportOrphans();
        // --dump-config: write a fully-populated tuning template (every registered key + its default,
        // nested by block) to the scratch folder and quit — the copy-and-edit source for config.json.
        if (_dumpConfig)
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
        _camera = new Camera3D { Fov = _fly || _freecam || _animLab ? 62 : 50, Far = 40000f };
        AddChild(_camera);
        _orbit = new OrbitCamera(_camera);
        if (_argYaw is { } argYaw) _orbit.Yaw = argYaw;
        if (_argPitch is { } argPitch) _orbit.Pitch = argPitch;

        // --run-tests: the in-engine assertion suites. Dispatched here, after the camera exists (a
        // suite building a world resolves PLAYER_RANGE from it) and before any session is built —
        // the suites build exactly the world/plane each of them needs and nothing else.
        if (_runTests)
        {
            RunTestSuites();
            return;
        }

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
        // The startup timing line, opened before anything is built and closed when the session's
        // first frame is on screen. Published as the ambient Current so WorldSession — which the
        // test harness also drives, with no session around it — can record its phases blind.
        _startup = new StartupProfile(_mode, Time.GetTicksMsec())
        {
            Subject = _emptyStage ? "stage=empty"
                : _nodeName != null ? $"chapter={_chapter} node={_nodeName}"
                : _worldMode ? $"chapter={_chapter}"
                : $"plane={_planeName}",
        };
        StartupProfile.Current = _startup;
        _worldRoot = new Node3D { Name = "Session" };
        AddChild(_worldRoot);
        // Re-derive every subsystem RNG from the master before anything in the session draws, so a
        // rebuild (Esc to the launchscreen and back) repeats the run rather than continuing it.
        Rng.Reset(_masterSeed, _seedPinned);
        // One simulation clock per session. --det pins it to a fixed step in every mode; the
        // animation lab is fixed-dt by nature (an accumulator interactively, one step per rendered
        // frame when scripted); everything else runs at the wall delta, which is arithmetically
        // what each consumer used before the clock existed.
        _clock = new GameClock
        {
            Mode = _det || (_animLab && _screenshotPath != null) ? GameClock.RunMode.FixedStep
                : _animLab ? GameClock.RunMode.FixedAccum
                : GameClock.RunMode.Realtime,
        };
        GameClock.Current = _clock;
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
            mark = StartupProfile.Mark();
            var sounds = haveSounds ? new SoundArchive(soundsPath) : null;
            StartupProfile.Record("sounds", mark);
            using var soundsScope = _animLab ? null : sounds;
            labSounds = _animLab ? sounds : null;
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
            if (_nodeName != null && _worldMode)
            {
                var matches = WorldBuilder.MatchNodes(gamez, _nodeName);
                if (matches.Count == 0)
                {
                    var near = WorldBuilder.SuggestNodes(gamez, _nodeName, NodeSuggestCap);
                    Log.Warn("world", $"--node='{_nodeName}' matches no node in {_chapter}'s gamez ({gamez.Nodes.Count} nodes)");
                    if (near.Count > 0)
                    {
                        Log.Warn("world", $"--node= candidates containing '{_nodeName}': {string.Join(", ", near)}");
                    }
                    else
                    {
                        Log.Warn("world", $"--node= no name in {_chapter} contains '{_nodeName}' either — run the full world with --debug-names to read names off the objects");
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
                Log.Info("world", $"--node='{_nodeName}' matched {matches.Count} node(s): {string.Join(", ", labels)}");
                if (matches.Count > 1)
                {
                    Log.Warn("world", $"--node='{_nodeName}' is ambiguous — building the first ({nodeSubtree.Name}#{nodeSubtree.Index}); name a unique node or pick by eye from the list above");
                }
            }
            if (_emptyStage)
            {
                // No gamez, no mission, no animation program: a flat collidable ground plane under
                // a grid drawn in code. Flight, weapons and colliders work; nothing else is built.
                mark = StartupProfile.Mark();
                var stage = EmptyStage.Build(collision: _fly || _forceCollision);
                StartupProfile.Record("world", mark);
                _plane = stage.Root;
                meshInstances = stage.MeshInstanceCount;
                colliders = stage.ColliderCount;
                what = "empty stage";
            }
            else if (_worldMode)
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
                        // The damage-test needs the collidable world (its census measures which
                        // destructible geometry is solid and whether death removes it) even though it
                        // runs in the freecam (non-fly) harness. Two interactive levers switch the
                        // same build: --collision, because the collider overlay needs bodies to
                        // draw, and --debug-damage, because a kill's collider count would otherwise
                        // read zero and lie.
                        Collision = BuildsCollision,
                        DebugAnim = _debugAnim,
                        AnimLod = _animLod,
                        DebugDzPaths = _debugDzPaths,
                        // The lab: quiet stage (ambient playback deferred to its A toggle),
                        // archives kept open for interactive effect builds.
                        KeepArchivesOpen = _animLab,
                        AutoStart = !_animLab,
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
                if (_damageTest)
                {
                    _worldRoot!.AddChild(_plane);
                    session.Runtime.ManualAdvance = true;
                    RunDamageTest(session.Runtime);
                    GetTree().Quit();
                    return false;
                }

                // --effects-test: build the world-effects runtime and play every impact/destruction
                // effect through it, reporting which resolve and which actually build a puffer (rule
                // 76: a started def that renders nothing vs one that does), then quit — the D32
                // headless verify. Added to the tree (self-ticking) so puffers spawn and the census
                // is real; the world plane subtree is added so the templates' global transforms hold.
                if (_effectsTest && worldScene != null)
                {
                    _worldRoot!.AddChild(_plane);
                    var effects = BuildWorldEffectsRuntime(gamez, worldScene, textures, session.Program);
                    RunEffectsTest(effects);
                    GetTree().Quit();
                    return false;
                }

                // The shared world selection: click-pick plus the cs_name ancestor ladder PgUp/PgDn
                // walks, in the two modes that observe a live world with a cursor. Created here so
                // the anim lab below can bind its camera-follow to it; it joins the tree with the
                // rest of the session, and builds no HUD and no highlight until something is
                // picked, so an unadorned capture is unchanged.
                if (_freecam || _animLab)
                {
                    _selection = new UI.SelectionService(_plane, _camera)
                    {
                        DebugPick = _debugSelect != null ? UI.SelectionService.ParseDebugPick(_debugSelect) : null,
                    };
                    // The node lab reads that selection. Its camera is resolved through a
                    // delegate: the freecam is created further down, after this point.
                    _nodeLab = new UI.NodeLab(_plane, _selection, session.Runtime, session.Program,
                        session.Builder.Scene, BuildsCollision)
                    {
                        CameraSource = () => _spectator,
                        DebugSpec = _debugNodeLab,
                        // The anim lab's timeline strip and transport panel own the bottom of the
                        // window; plain freecam has nothing there.
                        BottomMargin = _animLab ? 252 : 16,
                    };
                    // The world damage lab reads the same selection. Its effects runtime is built
                    // on the first damage action, not now — an untouched session pays nothing.
                    var damageScene = session.Builder.Scene;
                    var damageProgram = session.Program;
                    var damageRuntime = session.Runtime;
                    _worldDamageLab = new UI.WorldDamageLab(_selection, damageRuntime, BuildsCollision)
                    {
                        SelectByName = name => _nodeLab?.SelectByName(name) ?? false,
                        EffectsSource = () => EnsureWorldEffects(gamez, damageScene, textures,
                            damageProgram, damageRuntime),
                        DebugSpec = _debugDamage,
                        BottomMargin = _animLab ? 252 : 16,
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
                if (_fly || _freecam || _skyZoneExplicit)
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
                if (_fly || _freecam || _skyZoneExplicit)
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
                    // play/pick. Starts at the mission spawn; --pos/--direction override.
                    var camPos = _camPos ?? spawnPos;
                    var camLook = _camDir is { } labDir ? camPos + labDir : _lookAt ?? spawnLook;
                    var labCam = new SpectatorCamera(_camera, camPos, camLook) { ShowReadout = false };
                    // --node=: the mission spawn is meaningless on a single-subtree stage — frame
                    // the subject instead, unless the tester placed the eye themselves.
                    if (nodeAabb is { } nodeBox && _camPos == null && _lookAt == null && _camDir == null)
                    {
                        labCam.Frame(nodeBox);
                    }
                    _worldRoot!.AddChild(labCam);
                    _spectator = labCam;

                    // Optional stage prop: --plane= parks that aircraft at the mission spawn
                    // point. No FlightController — unpainted by default like every static view
                    // (--paint still applies one).
                    if (_planeNames.Count > 0)
                    {
                        mark = StartupProfile.Mark();
                        var planesGamez = GameZ.Load(planesGamezPath);
                        StartupProfile.Record("gamez", mark);
                        mark = StartupProfile.Mark();
                        var parkedBuilder = new PlaneBuilder(planesGamez, textures,
                            scheme: SchemeFor(0, zrdrPath, randomByDefault: false, NewPaintRng(),
                                PatternsForPlane(planesGamez, _planeName)),
                            patterns: Patterns);
                        var parked = parkedBuilder.Build(_planeName);
                        StartupProfile.Record("plane", mark);
                        meshInstances += parkedBuilder.MeshInstanceCount;
                        _worldRoot!.AddChild(parked);
                        parked.Position = spawnPos;
                        if ((spawnLook - spawnPos).LengthSquared() > 1e-6f)
                        {
                            parked.LookAtFromPosition(spawnPos, spawnLook, Vector3.Up);
                        }
                        what += $" + parked '{_planeName}'";
                    }

                    animLab = new UI.AnimLab(session.Runtime, session.Program, labCam,
                        labStage, textures, sounds, _masterSeed, _playAnim,
                        // On a --node= stage the subject IS the stage and is already framed; letting
                        // the lab re-aim on every Play swings the camera off the only object there
                        // (measured: the tower left the frame entirely on its own destruction).
                        autoFrame: _camPos == null && _lookAt == null && _camDir == null
                                   && nodeSubtree == null)
                    {
                        // Interactive shows the whole lab UI; a scripted --screenshot hides it so
                        // the 3D shot stays byte-identical — unless --debug-anim-ui forces it on
                        // to capture the timeline (the same convention as --debug-livery).
                        ShowUi = _screenshotPath == null || _debugAnimUi,
                        // The lab's camera follows whichever rung of the shared selection is current.
                        Selection = _selection,
                    };
                    _worldRoot!.AddChild(animLab);
                    GD.Print($"anim-lab: quiet stage, seed {_masterSeed}, fixed dt 1/60"
                             + (_playAnim != null ? $", playing '{_playAnim}'" : "")
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
                var staticPatterns = PatternsForPlane(gamez, _planeName);
                var staticScheme = SchemeFor(0, zrdrPath, randomByDefault: false, NewPaintRng(), staticPatterns);
                // In --viewer the LIVERY LAB owns the livery and applies it itself, so the
                // model is built bare and there is one write path for paint (its Repaint).
                // Everywhere else the builder paints at construction as usual.
                var builder = new PlaneBuilder(gamez, textures, damagePanels: _viewerMode,
                    scheme: _viewerMode ? null : staticScheme, patterns: Patterns);
                _plane = builder.Build(_planeName);
                StartupProfile.Record("plane", mark);
                meshInstances = builder.MeshInstanceCount;
                what = $"'{_planeName}'";

                // Damage lab: per-part HP sliders driving the item-10c damage visuals on the
                // parked plane — the same DamageVisuals/puffer pipeline as flight, with the
                // distance-interval trails burning in place (DamageLab). Present in every
                // --viewer session (H), opened at launch only by --damage.
                if (_viewerMode)
                {
                    mark = StartupProfile.Mark();
                    var stats = PlaneStats.Load(zrdrPath, _planeName);
                    StartupProfile.Record("zrdr", mark);
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
            // Weapon lab (--viewer --plane, key W): mount any of the 48 weapons on any of the plane's
            // firepoints/pylons and fire, watching the muzzle flash, tracer/rocket body and impact on a
            // stand-in target wall. Parked plane only (a chapter world has no aircraft marker rig)
            // and after the plane joins the tree — it reads each marker's GlobalTransform. Built in
            // every parked --viewer session so W always toggles it, hidden unless --weapon-lab opened it,
            // so an unadorned viewer screenshot is unchanged (the target + tracers show only while engaged).
            if (_viewerMode && !_worldMode && _plane != null)
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
                    if (ldef.Model == _planeName)
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
                var weaponLab = new UI.WeaponLab(_plane, labWeapons, labLoadout, textures, _camera, _planeName)
                {
                    DebugShow = _weaponLab,
                    InitialWeapon = _weaponSelect,
                    InitialMount = _weaponMount,
                    AutoFireAtStart = _weaponFire,
                };
                _worldRoot!.AddChild(weaponLab);
                _weaponLabNode = weaponLab;
                // --weapon-test: mount and fire every one of the 48 weapons once and report any that
                // throw, then quit (windowless under --headless). The report is
                // synchronous (Spawn does the muzzle math + pool insert without needing a frame), so no
                // world tick is required.
                if (_weaponTest)
                {
                    string report = weaponLab.RunSelfTest();
                    GD.Print(report);
                    WriteScratch("weapon_test.txt", report);
                    GetTree().Quit();
                    return false;
                }
                what += _weaponLab ? " + weapon lab" : " + weapon lab (W)";
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
            if (_freecam)
            {
                Vector3 camPos, camLookAt;
                if (_emptyStage)
                {
                    // The empty stage has no mission and therefore no spawn list: look at the grid
                    // origin, which is where a --stage=empty subject is put.
                    camPos = EmptyStage.CameraPos;
                    camLookAt = Vector3.Zero;
                }
                else
                {
                    var freecamSpawns = SpawnPoints.LoadIa(missionZrdrPath, _scenario);
                    (camPos, camLookAt) = ChooseSpawn(freecamSpawns, missionZrdrPath,
                        ChooseSpawnBase(freecamSpawns), 0, "");
                }
                if (_camPos is { } cp) camPos = cp;
                // The aim: a direction from wherever the eye ended up, or the named point.
                if (_camDir is { } cd) camLookAt = camPos + cd;
                else if (_lookAt is { } la) camLookAt = la;
                _spectator = new SpectatorCamera(_camera, camPos, camLookAt)
                {
                    // A scripted --screenshot run wants the frame clean of the overlay.
                    ShowReadout = _screenshotPath == null,
                };
                _worldRoot!.AddChild(_spectator);
                what += " + freecam";
                GD.Print($"freecam: spectator camera at ({camPos.X:0}, {camPos.Y:0}, {camPos.Z:0}) — " +
                         "hold RMB to look, WASD/QE to move, Shift boost, wheel sets speed; " +
                         "click an object to select it, PgUp/PgDn walk its ancestor ladder (Home/End jump), " +
                         "N opens the node lab, H the damage lab on whatever destructible is selected");
            }

            if (_fly)
            {
                // Session-wide flight data, loaded once and shared by every player: the aircraft
                // models' gamez, the plane's stats, the sound defs/archive. Only the built nodes
                // and the per-plane state below are per player.
                mark = StartupProfile.Mark();
                // On the empty stage the session gamez IS planes.zbd (there is no chapter world),
                // so there is nothing to load a second time.
                var planesGamez = _emptyStage ? gamez : GameZ.Load(planesGamezPath);
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
                var padAssignment = _menuPads ?? AssignPads(_rigs.Count);
                if (_menuPads != null)
                    LogPads(_menuPads);
                // One livery RNG for the session, so P1..P4 draw distinct colours from one
                // stream and --paint-seed reproduces the whole field.
                var paintRng = NewPaintRng();
                // One spawn list for the session; each player takes the next index (wrapping).
                // The empty stage has no mission, so nothing to read: ChooseSpawn takes the
                // --pos/default override placed over the grid origin.
                var spawnList = _emptyStage ? null : SpawnPoints.LoadIa(missionZrdrPath, _scenario);
                int spawnBase = ChooseSpawnBase(spawnList);

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
                    var effects = BuildWorldEffectsRuntime(gamez, worldScene, textures, crashProgram!);
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
                if (_stunt && _emptyStage)
                {
                    GD.Print("--stunt has no danger zones on the empty stage (no mission, no world) — flying free");
                }
                else if (_stunt)
                {
                    stuntZones = StuntMission.Load(gamez, missionZrdrPath, Messages.Load(messagesPath));
                    if (stuntZones == null)
                        // Expected for the chapters whose IA1 has no dzones (C1C, C2B) — a data
                        // fact, not a fault, so a plain line (log hygiene: no stack traces).
                        GD.Print($"--stunt: no danger zones for {_chapter}/{_mission} — flying free");
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
                Texture2D? reticleTex = LoadRimageTexture(
                    Path.Combine(_dataRoot, "extracted", "rimage"), "impact_point.png");

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
                    mark = StartupProfile.Mark();
                    var planeBuilder = new PlaneBuilder(planesGamez, textures, spinningProps: true,
                        scheme: SchemeFor(pi, zrdrPath, randomByDefault: false, paintRng,
                            PatternsForPlane(planesGamez, planeName)),
                        patterns: Patterns);
                    var planeModel = planeBuilder.Build(planeName);
                    StartupProfile.Record("plane", mark);
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
                        PinnedView = _view,
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
                            // --rocket=<wep_id>: swap every hardpoint's ordnance before the model is
                            // mounted and the ordnance-type list is built (controller._Ready). A
                            // testing hook — all 11 stock loadouts carry HE (wep_06), so this is the
                            // only way to prove the mounted model varies by rocket type.
                            if (_rocketOverride != null)
                            {
                                ApplyRocketOverride(controller.Loadout, weaponDefs, _rocketOverride, verbose);
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
                                         (_gunSelect != 0 ? $" [gun-select={_gunSelect}]" : "") +
                                         (_infiniteAmmo ? " (infinite ammo)" : ""));
                                if (controller.Ordnance is { } ord)
                                {
                                    GD.Print($"pylon ordnance: {ord.Count} mounted rocket model(s)" +
                                             (_rocketOverride != null ? $" (--rocket={_rocketOverride})" : ""));
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
                    if (hudFont != null && _hudFontTest)
                    {
                        controller.FontTest = new HudFontTest(hudFont, _hudFontTestText);
                        if (verbose)
                            GD.Print($"hud-font-test: '{_hudFontTestText}' via 5pointhud font");
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

            // --destroy=<name>: kill a named destructible at session build so a --screenshot
            // captures its destruction with nobody at the controls. Reuses the weapon-damage path —
            // DamageAt runs the full death (the healthy→destroyed swap fires synchronously here; the
            // debris and effects play out as the runtime self-ticks through the screenshot warm-up).
            // The world subtree is already in the tree (added above), so the death's global-transform
            // reads and the effect stage are valid. Flight already built + wired the world-effects
            // runtime (to the projectile pool too); a plane-less --freecam asks for one here so the
            // destruction's fire/smoke still render — gated on --destroy, so a plain --freecam
            // regression builds nothing extra.
            if (_destroyName != null && worldRuntime != null)
            {
                if (worldScene != null)
                {
                    EnsureWorldEffects(gamez, worldScene, textures, crashProgram!, worldRuntime);
                }
                int killed = TriggerDestroy(worldRuntime, _destroyName, out var destroyBounds);
                what += killed > 0 ? $" + destroyed {killed}× '{_destroyName}'"
                                   : $" + destroy '{_destroyName}' (no match)";
                // Auto-frame the plane-less freecam on what it killed, unless the tester placed the
                // camera themselves (--pos/--direction) — so a bare `--freecam --chapter=CX
                // --destroy=name --screenshot=x.png` is a complete, self-framing destruction shot.
                if (killed > 0 && _spectator != null && _camPos == null && _lookAt == null
                    && _camDir == null && destroyBounds.Size.LengthSquared() > 0f)
                {
                    _spectator.Frame(destroyBounds);
                }
            }
            else if (_destroyName != null)
            {
                GD.Print($"--destroy='{_destroyName}' ignored: no chapter world " +
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
            if (_screenshotPath != null)
                GetTree().Quit(1);
            return false;
        }

        // Only the static views frame their subject; flight and the spectator camera (both
        // --freecam and --anim-lab) place their own eye (FrameCamera would yank the freecam back
        // to the world's AABB orbit).
        if (!_fly && !_freecam && !_animLab)
            FrameCamera(nodeAabb);

        // Mesh lab on the selection (M) — the freecam/anim-lab twin of the viewer's lab above. It
        // owns no subtree until M attaches it to whatever the shared selection has, and restores
        // that subtree exactly when M lets go, so it builds and changes nothing until then.
        if (_selection != null)
        {
            _worldRoot!.AddChild(new UI.MeshLab(_selection, _sun, _env, _camera)
            {
                DebugSpec = _debugMesh,
                // A scripted capture is about the geometry, not the panel over it.
                ShowPanel = _screenshotPath == null,
            });
        }

        // Collider wireframes (C). Built in the modes that observe a live world: it draws what the
        // collision build produced, and says so loudly when the mode built none rather than
        // rendering an empty overlay that reads as "nothing here is solid".
        if ((_freecam || _animLab || _fly) && _plane != null)
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
                DebugShow = _showColliders,
                Planes = planeColliders,
            });
            Log.Info("world", $"collider overlay ready (C){(BuildsCollision ? "" : " — but this mode built NO collision; relaunch with --collision")}");
        }
        else if (_forceCollision && _worldMode)
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
            InitialMode = _debugNames == null ? UI.NodeLabels.Mode.Off : UI.NodeLabels.ParseMode(_debugNames),
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
        _selection = null;
        _nodeLab = null;
        _worldDamageLab = null;
        _worldEffects = null;
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

    /// <summary>The RNG the session's random liveries draw from: the master seed's paint stream,
    /// so an unpinned launch repaints the field and a pinned one repeats it. --paint-seed=N
    /// overrides the derived seed, pinning liveries alone in an otherwise random run.</summary>
    private RandomNumberGenerator NewPaintRng() => new()
    {
        Seed = _paintSeedExplicit ? _paintSeed : Rng.SeedFor(Rng.Paint),
    };

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

    // The impact/destruction effect ANIMATION names the world-effects runtime (D32) is bound to —
    // the closure of these is staged and playable via PlayEffectAt. IMPACT names come from
    // weapons.json (the non-model `default`/`buildings` effects of rockets/ordnance; the gun
    // `*_gunhit` family is bound so a later guns pass can reach it, but is not fired per-round);
    // destruction names are the ones death sequences CALL_ANIMATION. `random_gun_impact` (root
    // `player`, a player-plane hit) is excluded — unreachable in M3 and its generic root would
    // mis-anchor. Verified against extracted/*/cam_anim: every name resolves in all 8 chapters.
    private static readonly string[] EffectAnimNames =
    {
        // rocket / ordnance IMPACT (default + buildings), puffer-bearing and otherwise
        "large_fireball", "small_fireball", "he_ground_effect", "ap_ground_effect", "flak_effect",
        "flash_effect", "sonic_ground_effect", "scatter_effect", "torpedo_ground_effect",
        "rear_flash_effect", "torpedo_water_effect",
        // gun IMPACT family (bound for a later guns pass; see ProjectilePool.EffectSink)
        "3040slug_gunhit", "3040ap_gunhit", "3040dum_gunhit", "3040mag_gunhit",
        "5060slug_gunhit", "5060ap_gunhit", "5060dum_gunhit", "5060mag_gunhit",
        "70slug_gunhit", "70ap_gunhit", "70dum_gunhit", "70mag_gunhit",
        // destruction effects death sequences call
        "large_30sec_fire", "great_balls_of_fire", "large_black_smokeball", "biggun_flying_parts",
        "big_splash",
    };

    // The gamez template roots those effects' puffers ride — staged (hidden) under the world-effects
    // stage so a PlayEffectAt relocates one onto the hit/death point. Union of the anim defs'
    // anchor roots; all present in every chapter's gamez (checked). The stage is hidden, so the
    // roots' own meshes (gunhit debris bits, the he_ring/splash models) do not render — the puffers,
    // parented at world level, do; the mesh half is a documented follow-up.
    private static readonly string[] EffectStageRoots =
    {
        "gunhit", "dum_gunhit", "mag_gunhit", "flame_ball_01", "flame_ball_02", "he_ring",
        "ap_effect", "flak_control", "flash_control", "sonic_effect", "scatter_trails",
        "torp_effects", "rear_flash_control", "fire_here", "moving_fire_ball_01",
        "black_smoke_ball_01", "zep_ng_dstry1.flt", "huge_splash_model",
    };

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
    private static int BuildEffectStage(GameZ gamez, SceneBuilder scene, Node3D parent) =>
        BuildEffectStage(gamez, scene, parent, EffectTemplateRoots);

    private static int BuildEffectStage(GameZ gamez, SceneBuilder scene, Node3D parent,
        IEnumerable<string> roots)
    {
        int n = 0;
        foreach (var rootName in roots)
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

    // A stop-less sustained effect (large_30sec_fire) would emit for the whole session; the
    // world-effects runtime bounds every PlayEffectAt instance to this many seconds (past the 30 s
    // fire, so it completes), then tears its puffers down.
    private const float EffectRuntimeTtl = 32f;

    /// <summary>Builds the one world-effects runtime (D32) — the world-scoped generalization of the
    /// per-player crash runtime. It stages the impact/destruction effect templates (hidden) under a
    /// dedicated subtree so their names resolve locally without colliding with the world or the crash
    /// roots, keeps a live <c>PufferFactory</c> over the session textures, and binds the closure of
    /// <see cref="EffectAnimNames"/>. <see cref="AnimRuntime.PlayEffectAt"/> then stages any of those
    /// effects at a hit or death point: <c>ProjectilePool.EffectSink</c> calls it on a rocket impact,
    /// and the world runtime's <see cref="AnimRuntime.ExternalEffect"/> routes a death's
    /// CALL_ANIMATION here. Puffers parent at world level (the crash lesson) so the hidden stage does
    /// not suppress them.</summary>
    private AnimRuntime BuildWorldEffectsRuntime(GameZ gamez, SceneBuilder worldScene,
        TextureArchive textures, AnimProgram worldProgram)
    {
        var stage = new Node3D { Name = "world_effects", Visible = false };
        _worldRoot!.AddChild(stage);
        int staged = BuildEffectStage(gamez, worldScene, stage, EffectStageRoots);
        var effects = new AnimRuntime
        {
            AutoStart = false,
            DebugMotions = _debugAnim,
            PufferParent = _worldRoot,
            PufferFactory = st => Effects.Puffer.Create(st, textures, sustained: true),
            PlaceCalledTemplates = true,
            NameResolveFallback = true,
            EffectTtl = EffectRuntimeTtl,
            // The impact/death SOUND an effect def carries is already played by the projectile pool
            // (D30) or the world runtime (D31); this runtime only renders the puffers.
            SoundHandledElsewhere = true,
            // Several gun effects gate their puffer behind RANDOM_WEIGHT, so this runtime's dice
            // decide which effects render at all — its own stream off the master seed.
            Seed = Rng.IntSeedFor(Rng.Effects),
            PlayerPosition = () => (_rigs.Count > 0 ? _rigs[0].Camera : _camera) is { } cam
                ? cam.GlobalPosition
                : Vector3.Zero,
        };
        // Bind name resolution to the (hidden) template stage — so the effect names resolve to
        // these templates and not to the world's or the crash roots' same-named nodes — but parent
        // the runtime node itself under the visible world root, a plain logic node that self-ticks.
        effects.Bind(stage, worldProgram.Subset(EffectAnimNames));
        _worldRoot.AddChild(effects);
        GD.Print($"world-effects runtime: {staged}/{EffectStageRoots.Length} effect template(s) staged, "
                 + $"{EffectAnimNames.Length} effect name(s) bound");
        return effects;
    }

    /// <summary>The session's one world-effects runtime, built on first demand and wired into the
    /// world runtime's <see cref="AnimRuntime.ExternalEffect"/> so a death's CALL_ANIMATION renders.
    /// Flight builds one during the session build and wires it to the projectile pool as well, so
    /// this leaves an existing wiring alone; a plane-less <c>--freecam</c>/<c>--anim-lab</c> builds
    /// none of its own, which is why a kill there draws nothing until something asks. Returns null
    /// when the build fails — the HP/kill/swap/reset mechanics do not depend on it.</summary>
    private AnimRuntime? EnsureWorldEffects(GameZ gamez, SceneBuilder worldScene,
        TextureArchive textures, AnimProgram worldProgram, AnimRuntime worldRuntime)
    {
        if (_worldEffects == null)
        {
            try
            {
                _worldEffects = BuildWorldEffectsRuntime(gamez, worldScene, textures, worldProgram);
            }
            catch (Exception e)
            {
                Log.Warn("anim", $"world-effects runtime could not be built: {e.Message}");
                return null;
            }
        }
        var effects = _worldEffects;
        if (worldRuntime.ExternalEffect == null)
        {
            worldRuntime.ExternalEffect = (name, pt) => effects.Handles(name) && effects.PlayEffectAt(name, pt);
        }
        return effects;
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
            // The crash def's only SOUND (snd_exp_ground_a) is already played by FlightAudio via
            // Crash() -> OnGroundExplosion(); this runtime has no audio session, so dispatching it
            // here would only emit the "silent for the session" warning. Render effects, not sound.
            SoundHandledElsewhere = true,
            // Wreckage scatter and the crash def's RANDOM_WEIGHT verdicts. One seed per player off
            // the crash stream, so splitscreen crashes differ from each other but repeat run to run.
            Seed = Rng.NewIntSeed(Rng.Crash),
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
    /// <summary>Loads a single PNG from the extracted <c>rimage</c> UI set as a texture (the reticle
    /// pipper); null (with one log line) when the file is absent. These images carry their own alpha,
    /// so no colour-keying is needed — unlike the HUD font atlas.</summary>
    private static Texture2D? LoadRimageTexture(string rimageDir, string file)
    {
        var path = Path.Combine(rimageDir, file);
        if (!File.Exists(path))
        {
            GD.Print($"[reticle] no {file} in {rimageDir} — gun reticle off (run ExtractRof.ps1)");
            return null;
        }
        var img = Image.LoadFromFile(path);
        return img != null ? ImageTexture.CreateFromImage(img) : null;
    }

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
        // csky_time global + the camera built-ins, so it needs no _Process driving — that uniform
        // is the only handle on it, which is why a halted clock still stops the fall.
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
            : (int)(Rng.Stream(Rng.Spawn).Randi() % (uint)spawns.Count);
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
            Log.Info("flight", $"spawn [{tag}override] pos=({at.X:0},{at.Y:0},{at.Z:0}) dir=({dir.X:0.000},{dir.Y:0.000},{dir.Z:0.000})");
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
        Log.Info("flight", $"spawn [{_chapter}/{_mission} {label}] pos=({s.Position.X:0},{s.Position.Y:0},{s.Position.Z:0}) heading={s.HeadingDeg:0}°");
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
        var pivot = _lookAt;
        if (pivot == null && _camDir is { } dir)
        {
            if (_camPos is { } eye)
            {
                float ahead = Mathf.Max((aabb.GetCenter() - eye).Dot(dir), MinOrbitRadius);
                pivot = eye + dir * ahead;
                Log.Info("core", $"orbit pivot from --direction: --lookat={Vec3Arg(pivot.Value)} radius={ahead:0.###}");
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
        _orbit.Frame(aabb, _camPos, pivot);
    }

    /// <summary>Exercise each Config-wired module's tunable reads once, with a throwaway instance and
    /// no game data, so Config's registry knows the full key set. That lets <see cref="Config.ReportOrphans"/>
    /// flag config.json typos at startup and <c>--dump-config</c> emit a complete template — without a
    /// built world. Read-through means the reads register on execution, so a single dummy step is the
    /// cheapest way to run them. Add a line here as each module is wired to Config.</summary>
    private static void WarmTuningRegistry()
    {
        try
        {
            var fm = new FlightModel(new PlaneStats());
            fm.Reset(Vector3.Zero, Basis.Identity, 100f, 1f);
            fm.Step(default, 1f / 60f);
            // ProjectilePool reads this only on a live rocket shot, which the warmup never fires —
            // register it here so --dump-config still documents the weapon-fire tunable.
            Config.GetFloat("weapons.rocketSpeedScale", ProjectilePool.RocketSpeedScale);
        }
        catch (Exception e)
        {
            GD.PushWarning($"config: tuning-registry warmup failed ({e.Message}); --dump-config may be incomplete");
        }
    }

    /// <summary>--dump-session: print every setting this command line resolved to — mode
    /// arbitration, the <c>--det</c> bundle, placement, paths, every probe and debug flag — as
    /// sorted <c>key = value</c> text, to stdout and <c>./.scratch/session_dump.txt</c>, then quit.
    ///
    /// <para><b>This instrument must stay invisible to the rules it reports.</b> It is absent from
    /// the <c>--det</c> implication list and from the <c>_mode</c> chain, even though it drives and
    /// ends a session by itself and so meets the membership rule for both. A dump that implied
    /// <c>--det</c> would report <c>det.on = true</c> on every line of a matrix whose whole subject
    /// is which command lines turn the bundle on; one that joined the <c>_mode</c> chain would
    /// report the observer's mode instead of the session's. The window-focus decision is the single
    /// exception, and it is applied outside the predicate rather than folded into it.</para>
    ///
    /// <para>Paths render relative to <c>{data}</c>/<c>{repo}</c>: a baseline holding absolute
    /// paths only reproduces on the machine that captured it.</para></summary>
    /// <returns>Whether the report was produced; the caller turns this into the exit code.</returns>
    private bool DumpSession(bool hasContentArg, bool playersExplicit, bool scriptedSession,
        bool detExplicit, string scriptedBy, string detVia)
    {
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
        string Rel(string p)
        {
            if (p.Length == 0)
            {
                return SV.Absent;
            }
            string norm = p.Replace('\\', '/');
            string data = _dataRoot.Replace('\\', '/');
            string repo = _repoRoot.Replace('\\', '/');
            // Data root first: it defaults to the repo root, and when it does not, a path under it
            // is the more specific fact.
            if (data.Length > 0 && norm.StartsWith(data, StringComparison.OrdinalIgnoreCase))
            {
                return "{data}" + norm[data.Length..];
            }
            if (repo.Length > 0 && norm.StartsWith(repo, StringComparison.OrdinalIgnoreCase))
            {
                return "{repo}" + norm[repo.Length..];
            }
            return norm;
        }
        var f = new List<(string, string)>
        {
            // Mode arbitration — the outcome of the five mutating if-blocks in _Ready.
            ("mode.name", SV.Str(_mode)),
            ("mode.fly", SV.Bool(_fly)),
            ("mode.viewer", SV.Bool(_viewerMode)),
            ("mode.freecam", SV.Bool(_freecam)),
            ("mode.animLab", SV.Bool(_animLab)),
            ("mode.stunt", SV.Bool(_stunt)),
            ("mode.damageLab", SV.Bool(_damageLab)),
            ("mode.world", SV.Bool(_worldMode)),
            ("mode.emptyStage", SV.Bool(_emptyStage)),
            ("mode.hasContentArg", SV.Bool(hasContentArg)),
            ("mode.forceMenu", SV.Bool(_forceMenu)),
            ("mode.menuStartScreen", SV.Str(_menuStartScreen)),
            // What _Ready is about to do: the launchscreen, or a direct session build.
            ("mode.showsMenu", SV.Bool(_forceMenu || !hasContentArg)),
            ("run.scripted", SV.Bool(scriptedSession)),

            // The world the session builds.
            ("world.chapter", SV.Str(_chapter)),
            ("world.chapterGiven", SV.Bool(_chapterGiven)),
            ("world.mission", SV.Str(_mission)),
            ("world.scenario", SV.Str(_scenario)),
            ("world.scenarioExplicit", SV.Bool(_scenarioExplicit)),
            ("world.node", SV.Str(_nodeName)),
            ("world.stage", SV.Str(_stage)),
            ("world.skyZone", SV.Str(_skyZone)),
            ("world.skyZoneExplicit", SV.Bool(_skyZoneExplicit)),
            ("world.noFog", SV.Bool(_noFog)),
            ("world.animLod", SV.Num(_animLod)),
            ("world.destroy", SV.Str(_destroyName)),

            // The aircraft and who flies them.
            ("plane.name", SV.Str(_planeName)),
            ("plane.names", SV.List(_planeNames)),
            ("plane.players", SV.Num(_players)),
            ("plane.playersExplicit", SV.Bool(playersExplicit)),
            ("plane.loadout", SV.Str(_loadoutOverride)),
            ("plane.rocket", SV.Str(_rocketOverride)),
            ("plane.gunSelect", SV.Num(_gunSelect)),
            ("plane.infiniteAmmo", SV.Bool(_infiniteAmmo)),
            ("plane.autoFire", SV.Bool(_autoFire)),
            ("plane.autoFireRockets", SV.Bool(_autoFireRockets)),
            ("plane.holdSets", SV.Num(_holdSets?.Length ?? 0)),

            // Liveries.
            ("paint.names", SV.List(_paintNames)),
            // '|' between the three slots (body / dark trim / light trim), because a flat list of
            // nine numbers cannot be read back as the three colours it is.
            ("paint.colors", _paintColorOverride == null ? SV.Absent
                : "[" + string.Join("|", _paintColorOverride.Select(c => SV.Col(c))) + "]"),
            ("paint.decals", SV.List(_paintDecalOverride?.Select(d => SV.Num(d)))),
            ("paint.seed", SV.Num(_paintSeed)),
            ("paint.seedExplicit", SV.Bool(_paintSeedExplicit)),

            // The --det bundle and everything it pins.
            ("det.on", SV.Bool(_det)),
            ("det.explicit", SV.Bool(detExplicit)),
            ("det.noDet", SV.Bool(_noDet)),
            ("det.scriptedBy", SV.Str(scriptedBy)),
            ("det.via", SV.Str(detVia)),
            ("det.seedArg", SV.Opt(_seed)),
            // The value only when it is pinned. An unpinned master is drawn from the clock, so
            // printing it would make every non-deterministic row of a baseline differ from itself
            // on the next capture — and the reportable fact there is that it came from the clock.
            ("det.masterSeed", _seedPinned ? SV.Num(_masterSeed) : "<clock>"),
            ("det.seedPinned", SV.Bool(_seedPinned)),
            ("det.spawnIndex", SV.Num(_spawnIndex)),
            ("det.padsDisabled", SV.Bool(Pads.Disabled)),
            ("det.jitterDeg", SV.Num(_jitterDeg)),

            // Placement, AFTER ResolvePlacement has routed --pos/--direction onto the per-mode
            // plumbing — the raw args are kept beside the resolved fields so the routing shows.
            ("place.pos", SV.Vec(_pos)),
            ("place.direction", SV.Vec(_direction)),
            ("place.lookAt", SV.Vec(_lookAt)),
            ("place.spawnAt", SV.Vec(_spawnAt)),
            ("place.spawnDir", SV.Vec(_spawnDir)),
            ("place.camPos", SV.Vec(_camPos)),
            ("place.camDir", SV.Vec(_camDir)),
            ("place.view", SV.Num(_view)),
            ("place.yaw", SV.Opt(_argYaw)),
            ("place.pitch", SV.Opt(_argPitch)),
            ("place.deprecated", SV.List(_deprecated.Select(d => d.Old))),

            // Capture.
            ("shot.path", SV.Str(_screenshotPath)),
            ("shot.frames", SV.Num(_screenshotFrames)),
            ("shot.shots", SV.Num(_screenshotShots)),

            // The probes that drive and end a session themselves.
            ("probe.dumpMarkers", SV.Bool(_dumpMarkers)),
            ("probe.dumpMarkersFilter", SV.Str(_dumpMarkersPlane)),
            ("probe.dumpWeapons", SV.Bool(_dumpWeapons)),
            ("probe.dumpWeaponsFilter", SV.Str(_dumpWeaponsFilter)),
            ("probe.dumpLoadout", SV.Bool(_dumpLoadout)),
            ("probe.dumpLoadoutFilter", SV.Str(_dumpLoadoutFilter)),
            ("probe.dumpFlight", SV.Bool(_dumpFlight)),
            ("probe.dumpFlightPlane", SV.Str(_dumpFlightPlane)),
            ("probe.dumpConfig", SV.Bool(_dumpConfig)),
            ("probe.damageTest", SV.Bool(_damageTest)),
            ("probe.damageTestFilter", SV.Str(_damageTestFilter)),
            ("probe.damageHd", SV.Num(_damageHd)),
            ("probe.effectsTest", SV.Bool(_effectsTest)),
            ("probe.weaponTest", SV.Bool(_weaponTest)),
            ("probe.runTests", SV.Bool(_runTests)),
            ("probe.runTestsFilter", SV.Str(_runTestsFilter)),
            ("probe.hudFontTest", SV.Bool(_hudFontTest)),

            // Inspection overlays and labs.
            ("debug.anim", SV.Bool(_debugAnim)),
            ("debug.animUi", SV.Bool(_debugAnimUi)),
            ("debug.playAnim", SV.Str(_playAnim)),
            ("debug.dzPaths", SV.Bool(_debugDzPaths)),
            ("debug.scoreboard", SV.Bool(_debugScoreboard)),
            ("debug.livery", SV.Opt(_debugLivery)),
            ("debug.mesh", SV.Str(_debugMesh)),
            ("debug.names", SV.Str(_debugNames)),
            ("debug.select", SV.Str(_debugSelect)),
            ("debug.nodeLab", SV.Str(_debugNodeLab)),
            ("debug.damage", SV.Str(_debugDamage)),
            ("debug.join", SV.Num(_debugJoin)),
            ("debug.markersOverlay", SV.Bool(_markersOverlay)),
            ("debug.weaponLab", SV.Bool(_weaponLab)),
            ("debug.weaponSelect", SV.Str(_weaponSelect)),
            ("debug.weaponMount", SV.Str(_weaponMount)),
            ("debug.weaponFire", SV.Bool(_weaponFire)),

            // Collision — the predicate three consumers used to spell for themselves.
            ("collision.builds", SV.Bool(BuildsCollision)),
            ("collision.force", SV.Bool(_forceCollision)),
            ("collision.show", SV.Bool(_showColliders)),
            ("collision.debug", SV.Bool(_debugCollision)),

            // Where the data comes from. Relative, so the baseline is not one machine's.
            ("path.dataRootOverridden", SV.Bool(_dataRoot != _repoRoot)),
            ("path.planesGamez", Rel(_planesGamezPath)),
            ("path.zrdr", Rel(_zrdrPath)),
            ("path.zrdrOverridden", SV.Bool(_zrdrOverridden)),
            ("path.sounds", Rel(_soundsPath)),
            ("path.soundsOverridden", SV.Bool(_soundsOverridden)),
            ("path.interp", Rel(_interpPath)),
            ("path.messages", Rel(_messagesPath)),
            ("path.rof", Rel(_rofPath)),
            ("path.gamez", Rel(_gamezPath)),
            ("path.gamezOverridden", SV.Bool(_gamezOverridden)),
            ("path.textures", Rel(_texturesPath)),
            ("path.texturesOverridden", SV.Bool(_texturesOverridden)),

            // Texture drop-in — parse-time statics, not fields, but launch settings all the same.
            ("tex.census", SV.Bool(Mech3.TextureDropIn.CensusActive)),
            ("tex.overrides", SV.List(Mech3.TextureDropIn.OverrideNames)),

            // Everything else the command line settles.
            ("misc.mute", SV.Bool(_mute)),
            ("misc.noVsync", SV.Bool(_noVsync)),
            ("misc.perf", SV.Bool(_perf)),
            ("misc.noFocus", SV.Bool(_noFocus)),
        };
        var r = Testing.Probes.Session(string.Join(" ", OS.GetCmdlineUserArgs()), f);
        GD.Print(r.Text);
        WriteScratch("session_dump.txt", r.Text);
        GD.Print($"{r.Summary} → ./.scratch/session_dump.txt");
        return r.Ok;
    }

    /// <summary>--dump-markers[=plane]: print each player airframe's firepoint / pylon / target
    /// rig — name, plane-frame position, gun-pair grouping and shared mounts — to stdout and
    /// <c>./.scratch/markers_dump.txt</c>, then quit (see <see cref="Mech3.MarkerRig"/>). This is
    /// the committed instrument the <c>docs/formats/markers.md</c> tables regenerate from, so the
    /// user can see and name every mount when handing back the airframe gun-group table (item A3).
    /// An optional value filters to one plane by model node (<c>player_bhawk</c>) or display name
    /// (<c>Bloodhawk</c>), matched case-insensitively as a substring.</summary>
    /// <returns>Whether the report was produced; the caller turns this into the exit code.</returns>
    private bool DumpMarkers()
    {
        // Windowed launches shouldn't steal focus for a report that renders nothing and quits.
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
        var r = Testing.Probes.Markers(_planesGamezPath, _dumpMarkersPlane);
        if (r.Error != null)
        {
            GD.PrintErr($"--dump-markers: {r.Error}");
            return false;
        }
        GD.Print(r.Text);
        // ./.scratch/ inside the workspace, per CLAUDE.md — never the OS temp dir.
        WriteScratch("markers_dump.txt", r.Text);
        GD.Print($"{r.Summary} → ./.scratch/markers_dump.txt");
        return true;
    }

    /// <summary>Writes one report into the workspace scratch folder, by absolute path. Relative
    /// paths resolve against the process working directory, not the repo, so a run launched from
    /// anywhere else would silently scatter its artifacts.</summary>
    private void WriteScratch(string fileName, string text)
    {
        var scratch = Path.Combine(_repoRoot, ".scratch");
        Directory.CreateDirectory(scratch);
        File.WriteAllText(Path.Combine(scratch, fileName), text);
    }

    /// <summary>--run-tests[=filter]: boot the engine, run the registered assertion suites, print
    /// the PASS/FAIL/SKIP table plus <c>./.scratch/test-report.json</c>, and quit with a nonzero
    /// exit code if any suite failed (see <see cref="Testing.TestHarness"/>).</summary>
    private void RunTestSuites()
    {
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
        // A suite that ticks the sim must see the same clock a session gives it. Fixed-step, since
        // a test run is deterministic by nature: sim state is a function of the step count.
        _clock = new GameClock { Mode = GameClock.RunMode.FixedStep };
        GameClock.Current = _clock;
        var host = new Node3D { Name = "TestHost" };
        AddChild(host);
        var ctx = new Testing.TestContext
        {
            RepoRoot = _repoRoot,
            DataRoot = _dataRoot,
            Chapter = _chapter,
            Mission = _mission,
            ZrdrPath = _zrdrPath,
            MessagesPath = _messagesPath,
            PlanesGamezPath = _planesGamezPath,
            InterpPath = _interpPath,
            SoundsPath = _soundsPath,
            PlaneName = _planeName,
            Mute = _mute,
            LoadoutOverride = _loadoutOverride,
            Host = host,
            Camera = _camera,
        };
        if (DisplayServer.GetName() == "headless")
        {
            // Rule 82's sibling: the dummy renderer compiles no shaders, so a shader error cannot
            // occur — and therefore cannot be screened. Say so rather than letting the clean error
            // census read as proof.
            Log.Warn("test", $"headless display — no shaders compiled, so the error screen cannot see a shader error");
        }
        int code = Testing.TestHarness.Run(ctx, _runTestsFilter);
        host.Free();
        GameClock.Current = null;
        GetTree().Quit(code);
    }

    /// <summary>--dump-weapons[=id|name]: load the typed <see cref="Flight.WeaponDefs"/> reader
    /// (B11) over <c>weapons.json</c>, print one line per def (id, name, key ballistics, flags,
    /// bindings) to stdout and <c>./.scratch/weapons_dump.txt</c>, and report any unmapped keys,
    /// then quit. The committed verification instrument the weapons.md table is checked against —
    /// a clean run (no UNHANDLED lines) is the B11 pass. An optional value filters by id
    /// (<c>wep_06</c>) or <c>NAME</c> substring, matched case-insensitively.</summary>
    /// <returns>Whether the report was produced; the caller turns this into the exit code.</returns>
    private bool DumpWeapons()
    {
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
        var r = Testing.Probes.Weapons(_zrdrPath, _messagesPath, _dumpWeaponsFilter);
        if (r.Error != null)
        {
            GD.PrintErr($"--dump-weapons: {r.Error}");
            return false;
        }
        GD.Print(r.Text);
        WriteScratch("weapons_dump.txt", r.Text);
        GD.Print($"{r.Summary} → ./.scratch/weapons_dump.txt");
        return true;
    }

    /// <summary>--dump-flight[=plane]: step a throwaway <see cref="FlightModel"/> through the
    /// manoeuvres the original was recorded flying and print its numbers beside the video-decoded
    /// ones (<c>analysis/video-flight-calibration/</c>), to stdout and
    /// <c>./.scratch/flight_dump.txt</c>, then quit. The instrument behind the
    /// <c>flight-envelope</c> suite, and the only way to see what a flight-constant change did to
    /// the whole envelope rather than to the one number that was edited.</summary>
    /// <returns>Whether the report was produced; the caller turns this into the exit code.</returns>
    private bool DumpFlight()
    {
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
        string plane = _dumpFlightPlane.Length > 0 ? _dumpFlightPlane : _planeName;
        var r = Testing.Probes.FlightEnvelope(_zrdrPath, plane);
        if (r.Error != null)
        {
            GD.PrintErr($"--dump-flight: {r.Error}");
            return false;
        }
        GD.Print(r.Text);
        WriteScratch("flight_dump.txt", r.Text);
        GD.Print($"{r.Summary} → ./.scratch/flight_dump.txt");
        return true;
    }

    /// <summary>Applies the <c>--rocket=&lt;wep_id&gt;</c> testing override: replaces every hardpoint's
    /// ordnance with the named weapon, resetting each pylon's capacity/ammo to that weapon's
    /// <c>CLUSTER_SIZE</c>. A no-op (with a warning) if the id is unknown. Must run before the pylon
    /// models are mounted and the controller's ordnance-type list is built. All 11 stock loadouts
    /// carry HE (wep_06), so this is the only way to exercise a different pylon model.</summary>
    private static void ApplyRocketOverride(Loadout loadout, WeaponDefs weapons, string wepId, bool verbose)
    {
        if (weapons.Get(wepId) is not { } weapon)
        {
            GD.PushWarning($"--rocket='{wepId}' is not a known weapon id — hardpoints keep their stock ordnance");
            return;
        }
        int per = weapon.ClusterSize ?? 0;
        foreach (var hp in loadout.Hardpoints)
        {
            hp.Weapon = weapon;
            hp.Capacity = per;
            hp.Ammo = per;
        }
        if (verbose)
        {
            GD.Print($"--rocket: hardpoints -> {weapon.Id} ({weapon.Name}), " +
                     $"flyout model '{weapon.Flyout?.Model ?? "-"}', {per}/pylon");
        }
    }

    /// <summary>--dump-loadout[=plane]: for each plane in <c>stock_loadouts.json</c>, build its
    /// model and bind the stock loadout (<see cref="Flight.Loadout"/>, B12), reporting the resolved
    /// gun groups (mount, weapon, per-group ammo, muzzle nodes) and hardpoints — or the loud error
    /// if a marker doesn't resolve. Writes to stdout and <c>./.scratch/loadout_dump.txt</c>, then
    /// quits. <c>--loadout=&lt;def&gt;</c> binds that def's loadout instead of each plane's own (a
    /// cross-binding test — e.g. binding a def that wants <c>firepoint8</c> to the Kestrel proves
    /// the missing-marker error fires). An optional value filters by def / model / display.</summary>
    /// <returns>Whether the report was produced; the caller turns this into the exit code.</returns>
    private bool DumpLoadout()
    {
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
        var r = Testing.Probes.Loadouts(_zrdrPath, _messagesPath, _planesGamezPath, _dataRoot,
            _dumpLoadoutFilter, _loadoutOverride);
        if (r.Error != null)
        {
            GD.PrintErr($"--dump-loadout: {r.Error}");
            return false;
        }
        GD.Print(r.Text);
        WriteScratch("loadout_dump.txt", r.Text);
        GD.Print($"{r.Summary} → ./.scratch/loadout_dump.txt");
        return true;
    }

    /// <summary>The D32 headless verify: play every impact/destruction effect through the
    /// world-effects runtime at the camera point and report whether each RESOLVES (its def is bound)
    /// and whether it BUILDS a puffer (rule 76 — a started def whose factory/textures are missing
    /// renders nothing). Each effect is stopped before the next so effects sharing a template root
    /// (the gun family shares <c>gunhit</c>) get an independent count. Reports to stdout and
    /// <c>./.scratch/effects_test.txt</c>.</summary>
    private void RunEffectsTest(Mech3.AnimRuntime effects)
    {
        // Play each effect at the player point so range-gated ones (gunhit's PLAYER_RANGE) pass.
        var p = _camera.GlobalPosition;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"effects-test: chapter {_chapter}, {EffectAnimNames.Length} effect name(s), "
                      + $"point ({p.X:0},{p.Y:0},{p.Z:0})");
        int resolved = 0, puffered = 0;
        foreach (var name in EffectAnimNames)
        {
            int before = effects.PuffersBuilt;
            bool matched = effects.PlayEffectAt(name, p);
            // Fixed ticks so the t=0 PUFFER_STATE emits and any one-CallSequence-deep puffer
            // (large_black_smokeball's p1trail) reaches its first batch.
            for (int f = 0; f < 30; f++)
                effects.Advance(1f / 60f);
            int built = effects.PuffersBuilt - before;
            if (!matched)
                sb.AppendLine($"  {name,-22} UNRESOLVED — no def bound");
            else if (built > 0)
            {
                puffered++;
                sb.AppendLine($"  {name,-22} puffer[{built}] rendered");
            }
            else
                sb.AppendLine($"  {name,-22} started, built no puffer (light/model/container effect)");
            if (matched)
                resolved++;
            // Full reset before the next name: these effects share puffer names (trailpuffer2) and
            // template roots, so a lingering instance would let the next effect read as "no puffer".
            effects.StopAll();
        }
        sb.AppendLine($"effects-test: {resolved}/{EffectAnimNames.Length} resolved, "
                      + $"{puffered} built a puffer, {resolved - puffered} started but built none");
        if (effects.UnhandledEventCounts.Count > 0)
        {
            sb.AppendLine("  reasons a start built no puffer: "
                          + string.Join(", ", effects.UnhandledEventCounts
                              .OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}×{kv.Value}")));
        }
        GD.Print(sb.ToString());
        WriteScratch("effects_test.txt", sb.ToString());
    }

    /// <summary>--damage-test[=name] / --damage-hd=N: sweep one live destructible instance per
    /// distinct def through its damage stages (or through discrete weapon hits) and report what
    /// each check found — see <see cref="Testing.Probes.Damage"/>, which the <c>damage-stages</c> /
    /// <c>damage-hd</c> suites assert on. Reports to stdout, <c>./.scratch/damage_test.txt</c> and
    /// <c>./.scratch/world_colliders.txt</c>.</summary>
    private void RunDamageTest(Mech3.AnimRuntime runtime)
    {
        var r = Testing.Probes.Damage(runtime, _chapter, _damageTestFilter, _damageHd);
        GD.Print(r.Text);
        WriteScratch("damage_test.txt", r.Text);
        if (r.CollidableMeshes > 0)
        {
            WriteScratch("world_colliders.txt", r.CollidersText);
            GD.Print($"damage-test: {r.CollidableMeshes} collidable meshes → ./.scratch/world_colliders.txt");
        }
        GD.Print($"{r.Summary} → ./.scratch/damage_test.txt");
    }

    /// <summary>--destroy=&lt;name&gt; (F42): kill every destructible whose def name, animation name or
    /// anchor <c>cs_name</c> contains <paramref name="name"/> (case-insensitive), so a --screenshot
    /// captures the destruction with nobody at the controls. Reuses the weapon-hit path exactly
    /// (<see cref="Mech3.AnimRuntime.DamageAt"/> — the healthy→destroyed swap, debris and effects the
    /// same as a rocket kill); it just spends more than the object's HP. Resolves each match to its
    /// authoritative instance and dedupes by anchor, so a wildcard def that binds one physical object
    /// through several pools is killed once. Returns how many distinct objects were destroyed.</summary>
    private static int TriggerDestroy(Mech3.AnimRuntime runtime, string name, out Aabb bounds)
    {
        bounds = default;
        static string AnchorName(Node3D n) =>
            n.HasMeta(Mech3.AnimRuntime.NameMeta) ? n.GetMeta(Mech3.AnimRuntime.NameMeta).AsString()
                                                  : n.Name.ToString();

        // Distinct physical objects to kill, keyed by authoritative anchor so a def bound through
        // both its reader wildcard and its compiled twin counts once.
        var targets = new Dictionary<ulong, Mech3.DestructibleRegistry.Instance>();
        foreach (var inst in runtime.Destructibles.All)
        {
            bool match =
                inst.Def.Name.Contains(name, StringComparison.OrdinalIgnoreCase)
                || (inst.Def.AnimName?.Contains(name, StringComparison.OrdinalIgnoreCase) ?? false)
                || AnchorName(inst.Anchor).Contains(name, StringComparison.OrdinalIgnoreCase);
            if (!match)
            {
                continue;
            }
            var target = runtime.Destructibles.Resolve(inst.Anchor) ?? inst;
            targets[target.Anchor.GetInstanceId()] = target;
        }

        if (targets.Count == 0)
        {
            // No match: list a sample of what IS destructible here so the tester can correct the name
            // without a separate --damage-test run (that report is still the full list).
            var sample = new List<string>();
            var seen = new HashSet<string>();
            foreach (var inst in runtime.Destructibles.All)
            {
                if (seen.Add(inst.Def.Name))
                {
                    sample.Add(inst.Def.Name);
                }
                if (sample.Count >= 20)
                {
                    break;
                }
            }
            GD.Print($"--destroy='{name}': no destructible matched. "
                     + $"{runtime.Destructibles.DistinctAnchors} object(s) present; some def names: "
                     + string.Join(", ", sample) + " (--damage-test lists them all)");
            return 0;
        }

        // A cap so naming a common wildcard (many towers/panels) can't start hundreds of death
        // sequences in one frame; the framed object is what the screenshot needs. Loud when it bites.
        const int cap = 64;
        int killed = 0;
        bool haveBounds = false;
        foreach (var target in targets.Values)
        {
            if (killed >= cap)
            {
                GD.Print($"--destroy='{name}': capped at {cap} of {targets.Count} matches "
                         + "(name a more specific def/node to kill fewer)");
                break;
            }
            if (target.Status == Mech3.DestructibleRegistry.State.Destroyed)
            {
                continue;
            }
            // The first killed object's world-space bounds, captured before the kill: the caller
            // auto-frames the freecam on it (the anchor's own origin is often far from its geometry).
            // MeshInstance-based, so it merges the healthy + destroyed variants either way.
            if (!haveBounds)
            {
                bounds = UI.OrbitCamera.MergedAabb(target.Anchor);
                haveBounds = true;
            }
            // Spend more than the whole health pool so a single call kills it outright (DamageAt runs
            // the death sequence at zero). Feeding the anchor node is exactly how --damage-hd drives it.
            runtime.DamageAt(target.Anchor, target.MaxHealth + 1f);
            killed++;
        }
        var c = bounds.GetCenter();
        string at = haveBounds ? $" near ({c.X:0}, {c.Y:0}, {c.Z:0})" : "";
        GD.Print($"--destroy='{name}': destroyed {killed} object(s){at} "
                 + $"({string.Join(", ", targets.Values.Take(killed).Select(t => t.Def.Name).Distinct())})");
        return killed;
    }

    private static Vector3 ParseVec3(string s)
    {
        var parts = s.Split(',');
        return new Vector3(
            float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
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
    /// turns the captures blank (rule 121).</summary>
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

    /// <summary>The numpad view digit for --view=. 5 has no perspective of its own (the middle of
    /// the pad is the chase camera), and anything outside 1–9 is a typo — both fall back to the
    /// chase camera loudly rather than picking a neighbour.</summary>
    private static int ParseView(string s)
    {
        if (int.TryParse(s, System.Globalization.NumberStyles.Integer,
                         System.Globalization.CultureInfo.InvariantCulture, out int n)
            && n >= 1 && n <= 9 && n != 5)
        {
            return n;
        }
        Log.Warn("core", $"--view={s} is not a numpad view (1-4, 6-9) — using the chase camera");
        return 0;
    }

    /// <summary>Notes a superseded flag so <see cref="ResolvePlacement"/> can name its replacement
    /// once per run. Repeating the flag does not repeat the notice.</summary>
    private void Deprecated(string old, string replacement)
    {
        if (!_deprecated.Exists(d => d.Old == old))
        {
            _deprecated.Add((old, replacement));
        }
    }

    /// <summary>Resolves <c>--pos</c>/<c>--direction</c> — the one placement pair — onto the
    /// per-mode plumbing that already carries placement: the plane's spawn override in
    /// <c>--fly</c>/<c>--stunt</c>, the camera's placement in <c>--freecam</c>/<c>--viewer</c>/
    /// <c>--anim-lab</c>. Routing happens HERE, in one place, so no consumer downstream has to ask
    /// what mode it is in or whether it holds a point or a vector.
    ///
    /// <para><c>--pos</c> wins over the flag it replaces in its own mode; the superseded spellings
    /// keep their old per-mode meaning, so <c>--campos</c> still places only a camera (never the
    /// plane) and <c>--spawn-at</c> still moves the anim lab's parked stage prop as well as the
    /// camera.</para>
    ///
    /// <para><c>--lookat</c> names a POINT and <c>--direction</c> a VECTOR. The conversion is
    /// one-way and lives here: flight steers by direction, so a point is converted against the
    /// subject's position. The camera modes keep the point — <c>--freecam</c> only ever uses its
    /// direction (either form is lossless there) and the <c>--viewer</c> orbit PIVOTS on it, which
    /// no direction can express.</para></summary>
    private void ResolvePlacement()
    {
        foreach (var (old, replacement) in _deprecated)
        {
            Log.Warn("core", $"deprecated flag={old} use={replacement}");
        }
        if (_fly && _direction == null && _lookAt is { } aimPoint && (_pos ?? _spawnAt) is { } eye)
        {
            _direction = aimPoint - eye;
        }
        if (_direction is { } aim)
        {
            _direction = aim.LengthSquared() > 1e-6f ? aim.Normalized() : null;
        }
        if (_pos is { } place)
        {
            if (_fly)
            {
                _spawnAt = place;
            }
            else
            {
                _camPos = place;
            }
        }
        if (_direction is { } dir)
        {
            if (_fly)
            {
                _spawnDir = dir;
            }
            else
            {
                _camDir = dir;
            }
        }
        // A nose direction with nothing to place it on is a silently ignored argument: the spawn
        // override only engages when a position was given.
        if (_fly && _spawnDir != null && _spawnAt == null)
        {
            Log.Warn("core", $"--direction ignored: flight steers the nose from the spawn override, which needs --pos");
        }
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
    private bool HaltAllowed => !_fly || _rigs.Count == 1;

    public override void _UnhandledInput(InputEvent @event)
    {
        // P halts the sim, . steps it one frame — in freecam, the static viewer and the
        // launchscreen. Flight polls P itself (FlightController, so gamepad Start keeps working)
        // and the animation lab owns its own transport, so neither is handled here.
        if (!_animLab && @event is InputEventKey { Pressed: true, Echo: false } clockKey
            && _clock != null && HaltAllowed)
        {
            if (clockKey.Keycode == Key.P && !_fly)
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
            SaveScreenshot();
            return;
        }
        // F11 anywhere: print the mode's subject placement as ready-to-paste --pos=/--direction=
        // args, so a hand-framed orbit (or a spot found while flying) can be reproduced for a
        // deterministic --screenshot run.
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F11 })
        {
            PrintPlacement();
            return;
        }
        if (_fly || _freecam || _animLab)
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
        // The sim frame is part of what the capture IS: under the fixed clock one rendered frame is
        // exactly one sim step, so this number pins the moment the shot shows.
        long simFrame = _clock?.Frame ?? 0;
        double simTime = _clock?.Time ?? 0.0;
        Log.Info("core", $"screenshot saved: {path} sim_frame={simFrame} sim_time={simTime:0.###}");
        // The golden-image tripwire's whole input: a hash of the RAW pixels (never the PNG, whose
        // encoded bytes differ between identical images), the size that hash is only valid at, and
        // the adapter that drew it. Emitted on every capture so any shot can become a golden.
        Log.Info("core", $"shot pixmd5={GoldenShot.PixelHash(img)} size={img.GetWidth()}x{img.GetHeight()} gpu={GoldenShot.Adapter()}");
        // No-op unless --tex-census: reads the frame just saved back as per-texture pixel counts.
        TextureDropIn.CountShot(img, path);
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

    /// <summary>Print the mode's SUBJECT placement as ready-to-paste arguments (F11, any mode) —
    /// the same pair that placed it, so a pose found by hand reproduces in a deterministic
    /// --screenshot run. In flight that subject is the PLANE (player 1's position and nose), not
    /// the chase camera, because that is what --pos/--direction place there. The orbit view prints
    /// --lookat rather than --direction: its framed point is a pivot, and only the point
    /// reproduces the orbit radius as well as the angle.</summary>
    private void PrintPlacement()
    {
        if (_fly && _rigs.Count > 0 && _rigs[0].Controller is { } controller)
        {
            var xform = controller.GlobalTransform;
            Log.Info("core", $"placement: --pos={Vec3Arg(xform.Origin)} --direction={DirArg(-xform.Basis.Z)}");
            return;
        }
        var pos = _camera.GlobalPosition;
        if (_freecam || _animLab || _fly)
        {
            Log.Info("core", $"placement: --pos={Vec3Arg(pos)} --direction={DirArg(-_camera.GlobalTransform.Basis.Z)}");
            return;
        }
        Log.Info("core", $"placement: --pos={Vec3Arg(pos)} --lookat={Vec3Arg(_orbit.OrbitCenter)}");
    }

    /// <summary>Format a vector as the "x,y,z" argument value ParseVec3 reads back (invariant
    /// culture, trimmed to 3 decimals).</summary>
    private static string Vec3Arg(Vector3 v) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0:0.###},{1:0.###},{2:0.###}", v.X, v.Y, v.Z);

    /// <summary>Same, for a direction — normalized, and finer, since a unit vector's components
    /// are small enough that 3 decimals would quantise the aim to ~0.03°.</summary>
    private static string DirArg(Vector3 v) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0:0.#####},{1:0.#####},{2:0.#####}", v.X, v.Y, v.Z);

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
