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

namespace CSVM.Session;

/// <summary>
/// The per-launch session node: loads one aircraft from the player's own
/// extracted game data and renders it with orbit controls — or, with --fly,
/// free flight over the chapter world with arcade controls.
///
/// <see cref="Launcher"/> (Main.tscn's root) owns the bootstrap, the launchscreen and the
/// persistent camera/lighting, and instantiates one of these per launch — the menu's selection
/// and the CLI feed the same StartSession build path — constructed with the launch's settled
/// <see cref="SessionSpec"/> and a <see cref="LauncherContext"/> carrying the persistent
/// references. Esc from a menu-launched flight frees it again (Launcher.ReturnToMenu QueueFrees
/// this node; disposal duties that outlive its child subtree run in <c>_Notification</c> on
/// <c>NotificationExitTree</c>).
/// </summary>
public partial class GameSession : Node3D
{
    private const float HorizonScale = 2.5f;

    /// How many near-miss names a failed <c>--node=</c> lookup offers. A chapter holds
    /// thousands of nodes and a substring like "zep" hits dozens; the point is a usable hint, not
    /// a census.
    private const int NodeSuggestCap = 20;

    // Everything this launch settled, parsed and resolved once (see SessionSpec) — the command
    // line verbatim, or the launchscreen's pick (SessionSpec.FromMenu). Every consumer below reads
    // it and nothing re-derives a launch setting; the pristine command line stays on the Launcher.
    private readonly SessionSpec _spec;
    // Per-player pad binding chosen in the launchscreen's join flow (null = derive from the
    // connected roster in AssignPads, which is what every CLI launch does).
    private readonly int[][]? _menuPads;
    // Resolves each player's livery and spawn point against _spec (PLAN-planeviewer-split A3);
    // see src/Session/LiveryResolver.cs and src/Session/SpawnPicker.cs.
    private LiveryResolver _liveryResolver = null!;
    private SpawnPicker _spawnPicker = null!;
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
    private WorldEffectsFactory _worldEffectsFactory = null!;
    // Loads/applies the flown mission's weather and drives its per-frame rig state
    // (PLAN-planeviewer-split A5) — see src/Session/WeatherRig.cs's entry. Constructed once per
    // session (same lifetime as _worldEffectsFactory); null before the first weathered build and
    // nulled by ReturnToMenu so _Process's null guard covers the frame before the deferred free.
    private WeatherRig? _weatherRig;
    // The --screenshot=/--shots=/--frames= state machine (PLAN-planeviewer-split A2) — see
    // src/Testing/CaptureDirector.cs's entry. Process-scoped and owned by the Launcher (which
    // Ticks it); held here for the Pending reads that gate display choices during the build.
    private readonly Testing.CaptureDirector _captureDirector;
    // The master seed every subsystem generator derives from (see Utils.Rng), resolved by the
    // Launcher once per process (pinned runs take the spec's value; everything else draws from
    // the clock) and re-applied here at each session build.
    private readonly ulong _masterSeed;

    private Node3D? _plane;
    // The session's simulation clock (see GameClock). Also published as GameClock.Current, which
    // is how the sim consumers scattered through the tree reach it; dropped by ReturnToMenu.
    private GameClock? _clock;
    // The physics-stepped consumers this node drives itself when the clock is not realtime, in
    // the tree order Godot's physics tick would have used. Dropped by ReturnToMenu.
    private ProjectilePool? _projectiles;
    private UI.WeaponLab? _weaponLabNode;
    // rolling mirrored-tile window past the map edge
    private Mech3.MapEdgeExtender? _edgeExtender; 
    // One rig per rendered view: its camera plus the camera-anchored copies only it
    // sees (skydome / cloud deck / cloud puffs / whiteout). Exactly one entry in single player,
    // wrapping the main-viewport _camera below — so the 1P render path is unchanged.
    private readonly List<PlayerRig> _rigs = new();
    // scratch: rig camera positions for the edge extender
    private readonly List<Vector3> _focusPoints = new(); 
    // the splitscreen pane rig (null in single player)
    private UI.SplitScreen? _split;    
    // The persistent rendering nodes, owned by the Launcher and kept across sessions; this node
    // only configures them. The mesh lab steers the sun and ambient, which is why both ride the
    // context rather than staying local to the Launcher's lighting setup.
    private readonly Camera3D _camera;
    private readonly DirectionalLight3D _sun;
    private readonly Godot.Environment? _env;
    // The static inspection view's orbit camera (LMB orbit, wheel zoom, AABB framing); the
    // Launcher creates it once and seeds --yaw=/--pitch= into its initial angles.
    private readonly OrbitCamera _orbit;

    // Session lifecycle (the launchscreen's in-process world rebuild): everything a
    // session builds hangs under _worldRoot, so Esc-to-menu can free it and a new session node
    // build again. The camera, lights and global shader params live on the Launcher and persist.
    private Node3D? _worldRoot;    
    // launched into the menu → Esc from flight returns there, not quit
    private readonly bool _menuDriven; 

    /// <summary>Whether the build completed — the Launcher's Esc routing reads it (return to the
    /// launchscreen only once a world is actually up).</summary>
    public bool InSession { get; private set; }
    // the session's LIGHT_STATE point lights (see WorldLights)
    private WorldLights? _worldLights; 
    // The session-owned texture archive, kept open past the build scope so the data-driven crash can
    // bake its effect puffers lazily at crash time (the same reason --anim-lab keeps it open, but that
    // path hands it to the AnimLab node instead). Disposed by ReturnToMenu on teardown so a map reload
    // drops the previous archive instead of leaking it. Null in the anim-lab (lab-owned) case.
    private TextureArchive? _sessionTextures;
    // The world whose origin-parked entities are still being watched, and the poll accumulator.
    // See WorldBuilder.HideUnplacedEntities / RestorePlacedEntities.
    private WorldBuilder? _unplacedWatch;
    private double _unplacedRecheck;
    // Base (chapter-independent) paths, settled by the Launcher once per process and handed in via
    // the context; StartSession reads them each build and recomputes the chapter-dependent
    // gamez/texture/mission paths from _spec.Chapter.
    private readonly string _repoRoot;
    // This session's startup timing — the always-on [perf] startup line. One per StartSession,
    // published as StartupProfile.Current so the shared build code can record into it, and cleared
    // when the line is emitted.
    private StartupProfile? _startup;
    // Where extracted/ lives. Defaults to _repoRoot; overridden by --data-root= or CSVM_DATA_ROOT
    // so a git worktree can run the game — /extracted/, /CrimsonSkiesGame/ and /tools/ are
    // git-ignored, so a worktree checkout has none of them and cannot otherwise build or verify.
    private readonly string _dataRoot;
    private readonly string _planesGamezPath;  
    private readonly string _zrdrPath;
    private readonly string _soundsPath;
    private readonly string _interpPath;
    private readonly string _messagesPath;
    // the extracted UI archive (paint patterns)
    private readonly string _rofPath;          
    // Process-scoped, owned by the Launcher; the --damage-test/--effects-test/--weapon-test/
    // --destroy= probe wrappers below delegate to it (see src/Testing/ProbeRunner.cs).
    private readonly Testing.ProbeRunner _probeRunner;

    /// <summary>Whether this session builds the world's colliders — the one definition every
    /// consumer reads, resolved on the spec so the labs and the C overlay cannot spell it
    /// differently from what <see cref="Mech3.WorldSession"/> actually built.</summary>
    private bool BuildsCollision => _spec.BuildsCollision;

    /// <summary>Constructs the session node for one launch. <paramref name="spec"/> is what this
    /// session is built from (the command line verbatim, or the launchscreen's pick);
    /// <paramref name="ctx"/> carries the Launcher's settled paths, the persistent rendering
    /// nodes, the process-scoped services and the join flow's pad binding. The caller adds the
    /// node to the tree and then runs <see cref="StartSession"/>.</summary>
    public GameSession(SessionSpec spec, LauncherContext ctx)
    {
        // The session clock is advanced at the top of this node's _Process, and every sim consumer
        // reads it during the same frame — so this node has to tick first. Godot runs the lowest
        // priority first (the Launcher sits one notch behind at -999).
        ProcessPriority = -1000;
        Name = "GameSession";
        _spec = spec;
        _repoRoot = ctx.RepoRoot;
        _dataRoot = ctx.DataRoot;
        _planesGamezPath = ctx.PlanesGamezPath;
        _zrdrPath = ctx.ZrdrPath;
        _soundsPath = ctx.SoundsPath;
        _interpPath = ctx.InterpPath;
        _messagesPath = ctx.MessagesPath;
        _rofPath = ctx.RofPath;
        _probeRunner = ctx.ProbeRunner;
        _captureDirector = ctx.CaptureDirector;
        _masterSeed = ctx.MasterSeed;
        _camera = ctx.Camera;
        _orbit = ctx.Orbit;
        _sun = ctx.Sun;
        _env = ctx.Env;
        _menuDriven = ctx.MenuDriven;
        _menuPads = ctx.MenuPads;
    }

    /// <summary>The session's per-player rigs — the Launcher's F11 placement print reads them.</summary>
    internal List<PlayerRig> Rigs => _rigs;

    /// <summary>The session's subject plane (null until the build lands one) — the Launcher's
    /// capture tick reads it, because CaptureDirector only shoots once a plane exists.</summary>
    internal Node3D? Plane => _plane;


    /// <summary>Builds one flight/view session from the spec (mode, chapter, plane, spawn, …)
    /// into a fresh <see cref="_worldRoot"/> so Esc-to-menu can tear it all down and a new session
    /// node build again — the launchscreen's in-process world rebuild. The camera, lights and
    /// global shader params live on the Launcher and persist across sessions. Called by the
    /// Launcher once this node is in the tree. Returns true on success; false (leaving the partial
    /// _worldRoot for the caller to free) when the build threw.</summary>
    public bool StartSession()
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
        _liveryResolver = new LiveryResolver(_spec, _rofPath);
        _spawnPicker = new SpawnPicker(_spec);
        _worldEffectsFactory = new WorldEffectsFactory(_spec, _worldRoot,
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
            AnimRuntime? worldRuntime = null;   // the world's anim runtime
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
                    _probeRunner.RunEffectsTest(_spec, _camera, effects, WorldEffectsFactory.EffectAnimNames);
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

                    mark = StartupProfile.Mark();
                    _weatherRig = new WeatherRig(_spec, _worldRoot!);
                    _weatherRig.Build(missionZrdrPath, _rigs, textures, activeZone =>
                    {
                        foreach (var rig in _rigs)
                        {
                            var dome = builder.BuildHorizon(activeZone);
                            if (dome == null)
                                break;
                            dome.Scale = Vector3.One * HorizonScale;
                            if (rig.VisualLayer != 0)
                                UI.SplitScreen.SetVisualLayer(dome, rig.VisualLayer);
                            _worldRoot!.AddChild(dome);
                            rig.Horizon = dome;
                        }
                    });
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
                    int effectRoots = WorldEffectsFactory.BuildEffectStage(gamez, session.Builder.Scene, labStage);
                    labStage.AddChild(WorldEffectsFactory.BuildCrashAnchorSet());
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
                _weatherRig?.SetDeckCenter(OrbitCamera.MergedAabb(cloudDeck).GetCenter());
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
                    string planeName = PlaneRoster.PlaneFor(_spec, pi);
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
                            race.Add(pi, controller.Stunt, PlaneRoster.PlaneDisplayName(stats));
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
                                PlaneRoster.PlaneDisplayName(stats), $"{_spec.Chapter}   ·   {PlaneRoster.Humanize(_spec.Scenario)}",
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
                    var board = StuntRaceBoard.Build(race, $"{_spec.Chapter}   ·   {PlaneRoster.Humanize(_spec.Scenario)}",
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
                        flown.Add($"P{pi + 1} '{PlaneRoster.PlaneFor(_spec, pi)}'");
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
        InSession = true;
        return true;
    }

    /// <summary>Creates this session's <see cref="PlayerRig"/>s — one per rendered view.
    /// One player keeps the session's own main-viewport camera and the default visual
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


    public override void _Notification(int what)
    {
        // The focus mute lives on the Launcher (it is process state, not session state). What is
        // left here is the session's teardown: every duty that QueueFree does NOT cover on its own.
        // Return-to-menu is now a bare QueueFree (Launcher.ReturnToMenu) — the whole session subtree
        // (world, plane, HUD, rigs, effects) hangs under _worldRoot, a child of this node, so it
        // frees atomically with us and needs no manual null-out. Only the non-child duties run here.
        if (what == (int)NotificationExitTree)
        {
            // A run that quits inside the session build (the headless probes) never renders a
            // frame, so this is the only place its startup breakdown can still be reported.
            // Idempotent: a session that did render has already emitted and this does nothing.
            _startup?.Emit();
            // The published clock is a static pointer, not a child: null it so any node that
            // outlives this teardown falls back to its raw frame delta (GameClock.Current == null).
            // A later session sets it again in StartSession; the menu-relaunch happens in a frame
            // after this node has exited, so the two never race.
            GameClock.Current = null;
            // Clears csky_light_count so the next world does not inherit this one's light spill;
            // idempotent, and null-guarded (a failed build never set it).
            _worldLights?.Dispose();
            _worldLights = null;
            // The session-owned texture archive, kept open past its build scope so the data-driven
            // crash could bake puffers lazily. Not a node, so QueueFree cannot reach it; dispose it
            // here. Null-guarded — a failed build disposed and nulled it already, so no double free.
            _sessionTextures?.Dispose();
            _sessionTextures = null;
            // Restore the persistent (Launcher-owned) main camera: splitscreen stood it down while
            // the panes rendered, and the launchscreen and the next session expect it current.
            _camera.Current = true;
        }
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
        // Esc (menu-or-quit routing) and the F11/F12 capture keys are the Launcher's: they are
        // meaningful at the launchscreen too, where no session node exists.
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
        // The shader clock (ShaderTime.Advance), the --perf report and the capture pipeline tick
        // on the Launcher, one priority notch behind this node — the same point in the frame they
        // ran at when both lived on one root, and alive at the launchscreen where this node is not.
        //
        // The poll below stays on wall time: it is an instrument, and an instrument that freezes
        // with the thing it measures reports nothing.
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
        // player, one per pane in splitscreen (each on that player's own visual layer). See
        // src/Session/WeatherRig.cs's Tick (PLAN-planeviewer-split A5).
        _weatherRig?.Tick(_rigs, _clock?.FrameDt ?? (float)delta);

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
    }

}
