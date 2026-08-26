using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CSVM.Effects;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Mech3.Anim;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>Builds the impact/destruction effect stages and the per-player crash runtime:
/// the world-effects runtime
/// (one per session, lazily built on first demand) and <see cref="BuildFlightCrashRuntime"/>
/// (one per player, built once its controller joins the tree). Constructed once per session
/// (<c>_worldEffectsFactory</c> in <c>GameSession.StartSession</c>, same lifetime as
/// <see cref="LiveryResolver"/>/<see cref="SpawnPicker"/>); holds the lazily-built world-effects
/// runtime itself — <c>GameSession</c> keeps its own reference only for teardown
/// (<c>ReturnToMenu</c> nulls both).</summary>
public sealed class WorldEffectsFactory
{
    // A stop-less sustained effect (large_30sec_fire) would emit for the whole session; the
    // world-effects runtime bounds every PlayEffectAt instance to this many seconds (past the 30 s
    // fire, so it completes), then tears its puffers down.
    private const float EffectRuntimeTtl = 32f;

    // ⚠ TUNE, not decoded — the original copies templates per call and has no such number.
    // Sizes live in `CSVM/data/effect_pools.json` (EffectPools), not here: per root, scaled by
    // player count. AnimRuntime.PoolRecycles counts wraps onto a live slot.

    // Meshless nodes named for the crash def's local anchors ('player' plus healthy/destroyed/
    // pieces), so a played crash def resolves locally in front of the camera instead of one of
    // the world's generic 'healthy' nodes.
    // ⚠ The four airframe parts are here because the lab stands in for the plane: the damage
    // shims (EffectCatalogue.PlaneDamageEffectAnims) are authored against `nose`/`tail`/
    // `leftwing`/`rightwing`, which every real plane model carries but no world gamez does.
    private static readonly string[] CrashAnchorNodes =
    {
        "healthy", "destroyed", "dontmove", "markers", "piece1", "piece2", "piece3", "piece4",
        "shadow", "cockpit1", "nose", "tail", "leftwing", "rightwing",
    };

    private readonly SessionSpec _spec;
    private readonly Node3D _worldRoot;
    private readonly Func<Vector3> _playerPosition;
    // Every human's position — the world-effects runtime's own PLAYER_RANGE
    // gates (the ordnance washes' `If PlayerRange`) answer to the nearest of these, not the
    // single _playerPosition above. Null (a caller with no seam, e.g. AiCrashDefs' test rig)
    // leaves the runtime on _playerPosition alone, same as before C21.
    private readonly Func<IReadOnlyList<Vector3>>? _playerPositions;
    // Whether any human pilot is in a first-person view, for this runtime's own
    // PLAYER_1ST_PERSON conditions (the muzzle-burst and cockpit-bullethole defs branch on it).
    // Null leaves them answering false, the pre-cockpit reading.
    private readonly Func<bool>? _firstPersonView;
    // The session's wind, handed to every Puffer this factory's emitter factories build.
    private readonly EffectAmbience _ambience;

    // The authored per-root pool sizes (see the EffectPoolSlots remark above). Read once per
    // session, like the rest of this factory's inputs.
    private readonly EffectPools _pools = EffectPools.Load();

    private AnimRuntime? _worldEffects;

    // The builder for the planes-gamez half of a crash rig's template stage. Kept for the session
    // rather than per rig so all rigs share one material cache, the way every rig already shares
    // the world scene builder. Unpainted: the chute is not livery-bearing.
    private SceneBuilder? _planesScene;

    public WorldEffectsFactory(SessionSpec spec, Node3D worldRoot, Func<Vector3> playerPosition,
        EffectAmbience? ambience = null, Func<IReadOnlyList<Vector3>>? playerPositions = null,
        Func<bool>? firstPersonView = null)
    {
        _spec = spec;
        _worldRoot = worldRoot;
        _playerPosition = playerPosition;
        _playerPositions = playerPositions;
        _firstPersonView = firstPersonView;
        _ambience = ambience ?? EffectAmbience.Still;
    }

    /// <summary>The session's <see cref="UI.ScreenFlash"/> sink, handed to every runtime this
    /// factory builds — the three defs carrying an <c>FBFX_COLOR_FROM_TO</c> wash play here. Set
    /// once, before the first build; null leaves the event undrawn. Signature matches
    /// <see cref="Mech3.AnimRuntime.ScreenFlash"/>: the ramp, the burst's world point, and the
    /// def's own gate-radius squared.</summary>
    public Action<Color, Color, float, Vector3, float>? ScreenFlash { get; set; }

    /// <summary>The world-effects template stage — <see cref="EffectCatalogue.WorldStageRoots"/>'
    /// roots, one <c>pool&lt;N&gt;</c> container per slot. Null until
    /// <see cref="EnsureWorldEffects"/> has built the runtime. Exposed so <c>--effects-test</c> can
    /// report the mesh half: a puffer count says nothing about whether a template's meshes are
    /// visible. Observation only; the stage stays owned here.</summary>
    public Node3D? EffectStage { get; private set; }

    /// <summary>The session's one <c>touchdown_*</c> def vector, built against the same program the
    /// world-effects runtime binds. It is the original's global, built once at level init
    /// (<c>FUN_004735b0</c>) where each plane's crash vector is built at plane setup. Null until
    /// <see cref="EnsureWorldEffects"/> has run. <c>HumanFlightAdapter</c> hands it to every
    /// controller, so all rigs index the ONE vector rather than each building its own.</summary>
    public SurfaceDefTable? TouchdownDefs { get; private set; }

    /// <summary>The effect-template ROOT names the world-effects stage builds — the set
    /// <see cref="EffectPools"/> sizes, exposed so the committed pool config can be checked
    /// against what a bound chapter actually stages (a root renamed on one side and not the other
    /// would otherwise size nothing, silently). Forwards to
    /// <see cref="EffectCatalogue.WorldStageRoots"/>: there is no hand table left to read, so the
    /// answer needs the bound program and that chapter's gamez, exactly as the build does.</summary>
    public static IReadOnlyList<string> EffectStageRootNames(AnimProgram program, GameZ gamez) =>
        EffectCatalogue.WorldStageRoots(program, StageRootResolver(gamez));

    /// <summary>Builds the named template roots from the world gamez as children of
    /// <paramref name="parent"/>, each reset to sit at the stage origin; a CALL_ANIMATION
    /// relocates them onto the call site. Returns how many built. Roots always come from
    /// <see cref="EffectCatalogue.WorldStageRoots"/>/<see cref="EffectCatalogue.CrashStageRoots"/>,
    /// never a hand list. <paramref name="altGamez"/>/<paramref name="altScene"/> are a second
    /// source, asked only for a root the first has none of (the crash rig's planes gamez).</summary>
    public static int BuildEffectStage(GameZ gamez, SceneBuilder scene, Node3D parent,
        IEnumerable<string> roots, GameZ? altGamez = null, SceneBuilder? altScene = null)
    {
        int n = 0;
        foreach (var rootName in roots)
        {
            var node = gamez.FindByName(rootName);
            var builder = scene;
            if (node == null && altGamez != null && altScene != null)
            {
                node = altGamez.FindByName(rootName);
                builder = altScene;
            }
            // Effect templates are pure presentation with no collider: several carry
            // intersect_surface, and a collidable copy relocated onto an impact point would
            // leave an invisible plate floating at the blast site.
            if (node is { } found && builder.BuildSubtree(found, collisionSkip: _ => true) is { } built)
            {
                built.Transform = Transform3D.Identity; // sit at the stage; reposition moves it on call
                parent.AddChild(built);
                n++;
            }
        }
        return n;
    }

    /// <summary>The anchor lookup <see cref="EffectCatalogue.StageRootsFor"/> binds (one resolver,
    /// two scopes): a parentless gamez node is a template root the bind must build; a name already
    /// under one, or one the bind's own <paramref name="scope"/> carries, needs no template;
    /// anything else resolves nowhere. ⚠ Gamez roots are tested first, so a template the scope
    /// already staged still reads as a root it needs, and <paramref name="altGamez"/>'s roots
    /// LAST, so a second source can only rescue a name that resolved nowhere.</summary>
    public static Func<string, AnchorPlacement> StageRootResolver(GameZ gamez, Node3D? scope = null,
        GameZ? altGamez = null)
    {
        var roots = RootNames(gamez, out var known);
        var altRoots = altGamez != null ? RootNames(altGamez, out _) : null;
        var inScope = scope != null ? NamesUnder(scope) : null;
        return name =>
        {
            if (roots.Contains(name))
                return AnchorPlacement.Stage;
            if (inScope != null && inScope.Contains(name))
                return AnchorPlacement.InScope;
            if (known.Contains(name))
                return AnchorPlacement.InScope;
            return altRoots != null && altRoots.Contains(name)
                ? AnchorPlacement.Stage : AnchorPlacement.Missing;
        };
    }

    /// <summary>What the per-player crash rig stages, for a scope it has not built yet — the same
    /// call <see cref="BuildFlightCrashRuntime"/> makes, exposed so the <c>effects-census</c> suite
    /// can ask it on a replica rig (the wreck and part names vary by airframe, so the answer is
    /// per-plane).</summary>
    public static IReadOnlyList<string> CrashStageRootNames(AnimProgram program, GameZ gamez,
        Node3D rigScope, SurfaceDefTable? crashDefs = null, string? destroyAnim = null,
        GameZ? planesGamez = null) =>
        EffectCatalogue.CrashStageRoots(program, StageRootResolver(gamez, rigScope, planesGamez),
            crashDefs ?? EffectCatalogue.CrashDefTable(program), destroyAnim);

    /// <summary>Stages the crash rig's pooled effect-template copies under
    /// <paramref name="crashRoot"/>, one <c>poolN</c> container per depth level, every copy hidden.
    /// ⚠ The flake/gunhit family's meshes have no authored deactivation, so an unhidden copy draws
    /// stacked at the plane's centre for the whole session; the stage's reveal ritual lights only
    /// the call's own copy. Shared by the production rig and the <c>crash-rig-anchors</c> suite.
    /// Returns (copies staged, of which in slot 0).</summary>
    public static (int Roots, int Slot0) StageCrashTemplates(GameZ gamez, SceneBuilder worldScene,
        Node3D crashRoot, IReadOnlyList<string> rootNames, Utils.EffectPools pools,
        GameZ? planesGamez = null, SceneBuilder? planesScene = null)
    {
        int depth = pools.CrashDepthFor(rootNames);
        int effectRoots = 0, slot0Roots = 0;
        for (int slot = 0; slot < depth; slot++)
        {
            var pool = new Node3D { Name = $"pool{slot}" };
            pool.SetMeta(AnimRuntime.PoolSlotMeta, slot);
            crashRoot.AddChild(pool);
            int at = slot;
            int built = BuildEffectStage(gamez, worldScene, pool,
                rootNames.Where(r => pools.CrashSlotsFor(r) > at), planesGamez, planesScene);
            foreach (var child in pool.GetChildren())
            {
                if (child is Node3D copy)
                    copy.Visible = false;
            }
            if (slot == 0)
                slot0Roots = built;
            effectRoots += built;
        }

        return (effectRoots, slot0Roots);
    }

    /// <summary>The crash rig's sealed template stage — pooled, relocating called templates,
    /// staged hidden, and place-exempt for the airframe-scoped anchor names
    /// (<see cref="EffectCatalogue.AirframeScopedAnchors"/>).
    /// ⚠ Those names are authored against the aircraft's own model root on some airframes (the
    /// Devastator's <c>player_pfighter</c>), and a placing call must resolve without relocating
    /// it. Shared by the production rig and the <c>crash-rig-anchors</c> suite.</summary>
    public static TemplateStage<Node3D> NewCrashTemplateStage(bool debugMotions = false) =>
        AnimRuntime.NewTemplateStage(pooled: true, shown: true, placesCalled: true,
            debugMotions: debugMotions,
            placeExempt: EffectCatalogue.AirframeScopedAnchors
                .Concat(EffectCatalogue.CrashScaffoldAnchors));

    /// <summary>Builds the meshless <see cref="CrashAnchorNodes"/> under a 'player' root — the crash
    /// def's local anchor set (see the field remark).</summary>
    public static Node3D BuildCrashAnchorSet()
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

    /// <summary>The session's one world-effects runtime — the only way to reach
    /// <see cref="BuildWorldEffectsRuntime"/>; a second entry point recreates the two-runtimes bug.
    /// Wired into <see cref="AnimRuntime.ExternalEffect"/> and (when <paramref name="projectiles"/>
    /// is given) the pool's <c>EffectSink</c>, both gated on unset. Returns null on a failed build.
    /// ⚠ Keep the world params here rather than on the factory: it is constructed before the world
    /// exists, so folding them was examined and declined.</summary>
    public AnimRuntime? EnsureWorldEffects(GameZ gamez, SceneBuilder worldScene,
        TextureArchive textures, AnimProgram worldProgram, AnimRuntime worldRuntime,
        ProjectilePool? projectiles = null)
    {
        if (_worldEffects == null)
        {
            try
            {
                _worldEffects = BuildWorldEffectsRuntime(gamez, worldScene, textures, worldProgram);
                // Built here rather than per rig: one level, one touchdown vector (see the property).
                TouchdownDefs = EffectCatalogue.TouchdownDefTable(worldProgram);
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
            worldRuntime.ExternalEffect = (name, pt, node) => effects.Handles(name) && effects.PlayEffectAt(name, pt, node);
            worldRuntime.ExternalEffectStop = name => effects.Stop(name);
        }
        if (projectiles != null && projectiles.EffectSink == null)
        {
            projectiles.EffectSink = (name, pt, orient, ttl) => effects.PlayEffectAt(name, pt, null, ttl, orient);
            // The sink's own carrier test, so an IMPACT name that is both a bound def and a gamez
            // root (ballflare.flt) plays the def rather than a static instance of its template.
            projectiles.EffectHandles = effects.Handles;
        }
        return effects;
    }

    /// <summary>Builds the per-plane crash runtime: <c>player_crash_*</c> for a human rig,
    /// <c>ai_crash_*</c> for an AI plane (<see cref="EffectCatalogue.CrashDefTableFor"/>). Builds
    /// the effect-template roots and the plane's wreck under a <c>player</c> crash root, then binds
    /// a non-auto-start <see cref="AnimRuntime"/> to the controller, scoped so every anchor is unique.
    /// ⚠ <c>startprops</c>/<c>stopprops</c> never resolve on any airframe, so callers pass the plane
    /// model as fallback anchor; <paramref name="planesGamez"/> is the second stage source.</summary>
    public void BuildFlightCrashRuntime(FlightController controller, PlaneBuilder planeBuilder,
        string planeName, GameZ gamez, SceneBuilder worldScene, TextureArchive textures,
        AnimProgram crashProgram, bool verbose, WorldSounds? worldSounds = null,
        GameZ? planesGamez = null)
    {
        // The crash root: the def's `player` anim-root anchor, in the plane model's frame so the
        // wreck (built relative to the plane root) lands at the plane. Effect templates position
        // by AT_NODE global, so this transform doesn't affect them.
        var crashRoot = new Node3D { Name = "player" };
        crashRoot.SetMeta(AnimRuntime.NameMeta, "player");
        if (controller.PlaneModel != null)
            crashRoot.Transform = controller.PlaneModel.Transform;

        // The family this plane's crash indexes: player_crash_* for a human rig, ai_crash_* for an
        // AI plane — the original's own vehicle split (EffectCatalogue.CrashDefTableFor). Built
        // before the stage derivation, because the root closure is over THIS family's defs.
        var crashDefs = EffectCatalogue.CrashDefTableFor(crashProgram, controller.IsHumanPiloted,
            planeName);

        // The other slot on the death path (org/vehicleDamage.md): the self-named destroy def,
        // played when health reaches zero while the *_crash_* family above waits for ground contact.
        var destroyAnim = EffectCatalogue.DestroyAnimFor(controller.IsHumanPiloted, planeName);
        if (destroyAnim != null && crashProgram.ByAnimName(destroyAnim).Count == 0)
        {
            Log.Warn("anim", $"crash rig '{planeName}': no destroy def '{destroyAnim}' in this chapter's program — a kill will leave no wreck");
            destroyAnim = null;
        }

        // Effect-template roots, one instance per player; an unresolved anchor throws, naming
        // the def instead of silently playing nothing.
        // ⚠ Parent the crash root first: `player` is the defs' own anchor, and an unparented scope reports the whole rig unanchorable.
        controller.AddChild(crashRoot);
        // The second source, asked only for a root this chapter's gamez has none of: the destroy
        // def's `chuteman` lives in planes.zbd. Same reference on the empty stage, where the
        // session gamez IS the plane source, so the alt arm never fires there.
        var altGamez = planesGamez != null && !ReferenceEquals(planesGamez, gamez) ? planesGamez : null;
        var altScene = altGamez != null ? PlanesScene(altGamez, textures) : null;
        var rootNames = CrashStageRootNames(crashProgram, gamez, controller, crashDefs, destroyAnim,
            altGamez);
        // ⚠ Both kinds' roots, resolved HERE and not at the check below: once the templates are
        // staged, a staged root answers InScope instead of Stage and drops out of the derivation.
        var bothKindsRoots = BothRigKindsStageRoots(crashProgram, gamez, controller, planeName,
            altGamez);
        // Staged in pool slots: a single shared copy would be relocated onto every new tear,
        // discarding the previous panel's burst mid-flight. Sizes come from effect_pools.json's
        // crash section; AnimRuntime.AssignCallerSlot pins each call anchor to its own slot.
        var (effectRoots, slot0Roots) = StageCrashTemplates(gamez, worldScene, crashRoot,
            rootNames, _pools, altGamez, altScene);

        // The plane's destroyed wreck (pieceN meshes), built hidden; the crash def shows + flings it.
        // ⚠ Keep the whole rig one name scope: a crash def spans this subtree and the plane model's
        // (`healthy`), and only the bind's rig-wide fallback tier resolves both off one context node.
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

        // Scoped crash runtime: no ambient start, puffers via the session textures, non-portable
        // crash-def node ptrs resolved by name.
        // ⚠ The emitter factory parents at the world root, not the crash root — see its own doc.
        var crashRuntime = AnimRuntime.ForCrashRig(
            NewCrashTemplateStage(_spec.DebugAnim),
            Rng.NewIntSeed(Rng.Crash),
            new PufferEmitterFactory(textures, _worldRoot, _ambience), _spec.DebugAnim);
        // ⚠ Deliberately asymmetric, do not "fix" into one branch: an AI kill's boom is its crash
        // def's own authored Sound events, played positionally, while own-ship crash audio has one
        // owner in FlightAudio.Crash(). Letting a human rig play here would double it.
        crashRuntime.SoundHandledElsewhere = controller.IsHumanPiloted;
        crashRuntime.Sounds = controller.IsHumanPiloted ? null : worldSounds;
        // Surface-hugging sub-effects (water-splash rings/spray, dirt-burst dust) level to world
        // axes instead of inheriting impact attitude. Set once; these defs only ever play from a
        // crash sequence, so no per-crash toggle is needed.
        crashRuntime.LevelPlacedTemplateNames =
            new HashSet<string>(EffectCatalogue.CrashSurfaceLevelAnimNames, StringComparer.OrdinalIgnoreCase);
        // The parachute levels for a different reason: its template is authored at identity in the
        // planes gamez, and only OUR staging hangs the copy under the crash root, so the placing
        // call would freeze the tumbling wreck's attitude into a man under a canopy.
        crashRuntime.LevelPlacedTemplateNames.UnionWith(EffectCatalogue.BailoutAnimNames);
        // A stage anchor this airframe lacks is a SOFT failure: the call lands on the airframe root
        // and still draws, so nothing else reports it (docs/org/vehicleDamage.md's anchor census).
        crashRuntime.AnchorWarnAnimNames =
            new HashSet<string>(EffectCatalogue.DamageStageAnims, StringComparer.OrdinalIgnoreCase);
        crashRuntime.AnchorWarnLabel = planeName;
        // Wreck pieces with `do_intersections: true` stay in the world; handing the mask over arms
        // their collider sweep. Only Fly-mode goldens exercise it, and none captures a completed
        // landing — analysis/object-motion-goldens/FINDINGS.md.
        if (_spec.BuildsCollision)
        {
            crashRuntime.ContactMask = CollisionLayers.World;
            // Ground-def pieces author no `water` branch of their own; bound anyway so a piece's
            // BOUNCE reads a real struck surface rather than a guess.
            crashRuntime.SurfaceIsWater = body => ProjectilePool.SurfaceIsWater(body as Node);
        }
        // Bind only the closure of names that play ON this aircraft (CrashRigAnimNames), never the
        // full ~800-def world program — its ~150 generic-named defs would mis-anchor onto this
        // plane's parts and run their reset states on it.
        crashRuntime.Bind(controller,
            crashProgram.Subset(EffectCatalogue.CrashRigAnimNames(crashDefs, destroyAnim)));
        controller.AddChild(crashRuntime);
        controller.DestroyDef = destroyAnim;
        // Which of the two families owns the landing, asked of the data rather than of who is
        // flying: a def that takes the hull over also authors its own bounce sequences.
        controller.DestroyDefFliesWreck = EffectCatalogue.FliesOwnHull(crashProgram, destroyAnim);
        // The plane model's built visibility, so respawn can undo the crash def's healthy/markers
        // hides (its RESET_STATE only restores dontmove). Captured pristine, before any crash.
        var planeVis = new List<(Node3D, bool)>();
        if (controller.PlaneModel != null)
            CollectVisibility(controller.PlaneModel, planeVis);
        // One call for the whole rig, so it cannot be half-bound. The anchor is the context node
        // both families play against: the ai_crash_* NAME `kestrel` resolves nowhere in a rig, so
        // Play falls back to it, the node the original's own caller supplies (org/vehicleDamage.md).
        controller.BindCrashRig(crashRuntime, crashDefs, crashRoot, restPoses, planeVis);
        // Phase 2 of the damage-visuals setup: the sink and the stops, which need a live rig
        // runtime and so cannot be wired where the object is built.
        WireDamageStages(controller, crashProgram);
        // Every emitter the rig's defs name is built here, off the frame that plays it, so a crash
        // or a damage stage finds its puffers and materials already made.
        var warmMark = StartupProfile.Mark();
        var warmed = controller.PlaneModel is { } model
            ? crashRuntime.PrewarmEmitters(model, crashRoot)
            : crashRuntime.PrewarmEmitters(crashRoot);
        StartupProfile.Record("emitters", warmMark);
        double warmMs = Stopwatch.GetElapsedTime(warmMark).TotalMilliseconds;
        Log.Info("anim", $"crash rig '{planeName}': pre-warmed {warmed.Built} emitter(s) in {warmMs:0} ms ({warmed.SelfHosted} call-site hosted, {warmed.Unhosted} unhosted here)");
        if (slot0Roots != rootNames.Count)
            Log.Warn("anim", $"crash rig '{planeName}': staged {slot0Roots} of {rootNames.Count} template root(s) the bound defs anchor on — the rest built nothing from this chapter's gamez, so their defs play nothing");
        // ⚠ Both kinds' roots, never this rig's alone: one crash section sizes two families that
        // stage different roots, so a per-rig test warns on every correct entry the other owns.
        // What survives is the real drift, a key naming a root neither kind stages.
        foreach (var unknown in _pools.UnknownCrashRoots(bothKindsRoots))
        {
            Log.Warn("anim", $"effect pools: crash root '{unknown}' is staged by no rig kind — it sizes nothing");
        }
        if (verbose)
            GD.Print($"data-crash: {effectRoots} effect template cop(ies) over {_pools.CrashDepthFor(rootNames)} pool slot(s) "
                     + $"+ {restPoses.Count} wreck node(s) — crash runtime bound (scoped, no auto-start)");
    }

    // Every template root either rig kind stages, for the pool-config drift check alone. Derived
    // the same way the live one is, so a root this rig does not stage still counts as known.
    private static List<string> BothRigKindsStageRoots(AnimProgram program, GameZ gamez,
        FlightController controller, string planeName, GameZ? altGamez)
    {
        var both = new List<string>();
        foreach (bool human in new[] { true, false })
        {
            var defs = EffectCatalogue.CrashDefTableFor(program, human, planeName);
            var destroy = EffectCatalogue.DestroyAnimFor(human, planeName);
            if (destroy != null && program.ByAnimName(destroy).Count == 0)
                destroy = null;
            foreach (var r in CrashStageRootNames(program, gamez, controller, defs, destroy, altGamez))
                if (!both.Contains(r))
                    both.Add(r);
        }
        return both;
    }

    // The crash runtime also carries the damage-stage menu, so a part crossing an injure_anims
    // threshold plays its authored def. This is phase 2 of a setup split across two files:
    // HumanFlightAdapter.BuildDamageVisuals is phase 1 and runs for every rig, while this half
    // exists only where a crash runtime does.
    private static void WireDamageStages(FlightController controller, AnimProgram crashProgram)
    {
        if (controller.Visuals is not { } visuals || controller.CrashRuntime is not { } rigRuntime)
            return;
        var planeModel = controller.PlaneModel;
        // This closure also arbitrates node ownership against other per-frame systems:
        // add a future contested case here by name, not as a generic scan.
        visuals.DamageEffectSink = anim =>
        {
            // applyReset:false as the crash trigger does — a reset would re-pose nodes
            // the damage state owns, not just the effect's.
            int started = rigRuntime.Play(anim, planeModel, applyReset: false).Count;
            // ⚠ started is instances, not emitters — PufferState events dispatch on the
            // runtime's next tick, so sample the puffer count later, not off this delta.
            Log.Info("anim", $"damage stage anim={anim} started={started} rig_puffers_total={rigRuntime.PuffersBuilt}");
            // player_fuelleak's ELSE branch deactivates wing_flare2 for the rest of
            // the leak (the def never re-activates it) — hand that lamp to the leak so
            // WingLightBlinker's 1.5 s cycle stops re-asserting the blink over it.
            if (anim.Equals("player_fuelleak", StringComparison.OrdinalIgnoreCase))
                controller.WingLights?.Suspend("wing_flare2");
        };
        // ⚠ The stop must cover the CALL closure, not the played roots alone, or a
        // called-onto instance never gets its NODE_ACTIVE exit. Derived from the program
        // so no hand list can rot.
        var stageClosure = new List<string>();
        foreach (var d in crashProgram.Subset(EffectCatalogue.DamageStageAnims).Defs)
        {
            var n = d.AnimName ?? d.Name;
            if (!string.IsNullOrEmpty(n) && !stageClosure.Contains(n))
                stageClosure.Add(n);
        }
        visuals.DamageEffectStop = () =>
        {
            foreach (var n in stageClosure)
                rigRuntime.Stop(n);
        };
        // The retraction a repair makes stops one stage's own closure, leaving the rest
        // live. Same derivation as above, per stage, since the whole-menu stop cannot
        // express it.
        visuals.DamageEffectStopOne = stage =>
        {
            foreach (var d in crashProgram.Subset(stage).Defs)
                if ((d.AnimName ?? d.Name) is { Length: > 0 } n)
                    rigRuntime.Stop(n);
        };
    }

    // A gamez's parentless node names (the template roots), with every node name it carries at all
    // as the out param — the two halves StageRootResolver decides on.
    private static HashSet<string> RootNames(GameZ gamez, out HashSet<string> known)
    {
        var parented = new HashSet<int>();
        foreach (var n in gamez.Nodes)
            foreach (var c in n.Children)
                parented.Add(c);
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in gamez.Nodes)
        {
            known.Add(n.Name);
            if (!parented.Contains(n.Index))
                roots.Add(n.Name);
        }
        return roots;
    }

    // Every name a bind's own scope answers — the Godot node name and the gamez
    // AnimRuntime.NameMeta both, since name resolution reads the meta.
    private static HashSet<string> NamesUnder(Node root)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<Node>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            names.Add(node.Name.ToString());
            if (node.HasMeta(AnimRuntime.NameMeta))
                names.Add(node.GetMeta(AnimRuntime.NameMeta).AsString());
            foreach (var child in node.GetChildren())
                queue.Enqueue(child);
        }
        return names;
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

    // Built once per session (see the field): cullBackfaces matches PlaneBuilder's own builder,
    // since these subtrees are authored as aircraft geometry.
    private SceneBuilder PlanesScene(GameZ planesGamez, TextureArchive textures) =>
        _planesScene ??= new SceneBuilder(planesGamez, textures, cullBackfaces: true);

    // The world-scoped generalization of the per-player crash runtime: stages effect templates
    // under a dedicated subtree, keeps a live `IEmitterFactory`, and binds
    // EffectCatalogue.EffectAnimNames so AnimRuntime.PlayEffectAt can stage any of them at a hit
    // or death point. Puffers parent at world level so the stage does not suppress them.
    // ⚠ Private — EnsureWorldEffects is the only way in. A second entry point recreates the
    // two-runtimes bug.
    private AnimRuntime BuildWorldEffectsRuntime(GameZ gamez, SceneBuilder worldScene,
        TextureArchive textures, AnimProgram worldProgram)
    {
        var stage = new Node3D { Name = "world_effects" };
        _worldRoot.AddChild(stage);
        EffectStage = stage;
        // Each root is staged in as many copies as effect_pools.json sizes it for this session.
        // ⚠ An unresolved anchor throws here; EnsureWorldEffects turns that into its "runtime
        // could not be built" warning rather than a def playing nothing silently.
        var roots = EffectCatalogue.WorldStageRoots(worldProgram, StageRootResolver(gamez));
        int players = Math.Max(1, _spec.Players);
        int depth = _pools.DepthFor(roots, players);
        int staged = 0;
        for (int slot = 0; slot < depth; slot++)
        {
            var pool = new Node3D { Name = $"pool{slot}" };
            pool.SetMeta(AnimRuntime.PoolSlotMeta, slot);
            stage.AddChild(pool);
            int at = slot;
            staged += BuildEffectStage(gamez, worldScene, pool,
                roots.Where(r => _pools.SlotsFor(r, players) > at));
            foreach (var child in pool.GetChildren())
                if (child is Node3D root)
                    root.Visible = false;
        }
        // This runtime only renders the puffers; the impact/death sound already plays elsewhere.
        // ⚠ Do not write stage options onto the returned runtime — that only works when the
        // option happens to be read after Bind, and is a sealing leak.
        var effects = AnimRuntime.ForEffects(
            AnimRuntime.NewTemplateStage(pooled: true, shown: true, placesCalled: true,
                debugMotions: _spec.DebugAnim),
            Rng.IntSeedFor(Rng.Effects),
            new PufferEmitterFactory(textures, _worldRoot, _ambience),
            _spec.DebugAnim, EffectRuntimeTtl, _playerPosition);
        effects.PlayerPositions = _playerPositions;
        effects.FirstPersonView = _firstPersonView;
        effects.ScreenFlash = ScreenFlash;
        // Bind name resolution to the template stage — so the effect names resolve to these
        // templates and not to the world's or the crash roots' same-named nodes — but parent the
        // runtime node itself under the visible world root, a plain logic node that self-ticks.
        var bound = EffectCatalogue.WorldEffectAnimNames(worldProgram);
        effects.Bind(stage, worldProgram.Subset(bound));
        _worldRoot.AddChild(effects);
        int wanted = 0;
        foreach (var r in roots)
            wanted += _pools.SlotsFor(r, players);
        // Name the sizes, not just the total: "143 staged" cannot say whether a root the tester
        // just re-sized actually got its copies. Grouped by size so the line stays one line.
        var bySize = new SortedDictionary<int, List<string>>();
        foreach (var r in roots)
            bySize.TryAdd(_pools.SlotsFor(r, players), new List<string>());
        foreach (var r in roots)
            bySize[_pools.SlotsFor(r, players)].Add(r);
        var sizes = new List<string>();
        foreach (var (size, names) in bySize)
        {
            sizes.Add(names.Count > 4
                ? $"{size}× {names.Count} root(s)"
                : $"{size}× {string.Join("/", names)}");
        }
        GD.Print($"world-effects runtime: {staged}/{wanted} effect template(s) staged over "
                 + $"{depth} pool slot(s) for {players} player(s) [{string.Join(", ", sizes)}], "
                 + $"{bound.Count} effect name(s) bound");
        foreach (var unknown in _pools.UnknownRoots(roots))
            Log.Warn("anim", $"effect pools: '{unknown}' is not an effect stage root — it sizes nothing");
        return effects;
    }
}
