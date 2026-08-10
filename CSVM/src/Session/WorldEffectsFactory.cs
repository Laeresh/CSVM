using System;
using System.Collections.Generic;
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

    // How many copies of each effect template the stage holds, one per pool slot, so overlapping
    // calls to one effect each keep their own (with a single copy, the one template would be
    // relocated and the first blast's trails would jump to the second's site). A call takes the
    // next slot and the cursor wraps, so this is the concurrency the pool covers before two calls
    // share a copy again.
    // ⚠ INVENTED, not decoded — the original copies its templates per call and has no such
    // number. Which is exactly why the sizes live in `CSVM/data/effect_pools.json` (EffectPools)
    // rather than in a const here: per ROOT, scaled by the session's player count, editable
    // without a rebuild. TUNE values; the runtime counts (and names once)
    // every call that wraps onto a live slot — AnimRuntime.PoolRecycles is what to size against.

    // The player crash-anchor set: meshless nodes named exactly the crash def's targets (its
    // anim_root 'player' plus healthy/destroyed/pieces). Built into the lab stage so a played crash
    // def anchors to this 'player' and resolves 'healthy'/'destroyed' HERE — locally, in front of
    // the camera — rather than falling through to one of the world's 217 generic 'healthy' nodes.
    // The effects (relocated templates) attach to these; the plane/wreck geometry is the crash
    // plan's Layer-2 work.
    // ⚠ The four airframe parts are here because the lab stands in for the PLANE: the damage shims
    // (EffectCatalogue.PlaneDamageEffectAnims) are authored NAME=`nose`/`tail`/`leftwing`/`rightwing`,
    // which the real rig resolves on the bound controller's model (all 22 plane models carry them).
    // Without them the lab's closure resolves `nose` nowhere and --anim-lab fails to start on every
    // chapter whose world gamez has no node of that name — C1/C1B/C1C/C2/C2B. The other three only
    // ever "resolved" on C3-C5 by matching a stray scenery node, which is not the plane either.
    private static readonly string[] CrashAnchorNodes =
    {
        "healthy", "destroyed", "dontmove", "markers", "piece1", "piece2", "piece3", "piece4",
        "shadow", "cockpit1", "nose", "tail", "leftwing", "rightwing",
    };

    private readonly SessionSpec _spec;
    private readonly Node3D _worldRoot;
    private readonly Func<Vector3> _playerPosition;
    // B6: the session's wind, handed to every Puffer this factory's emitter factories build.
    private readonly EffectAmbience _ambience;

    // The authored per-root pool sizes (see the EffectPoolSlots remark above). Read once per
    // session, like the rest of this factory's inputs.
    private readonly EffectPools _pools = EffectPools.Load();

    private AnimRuntime? _worldEffects;

    public WorldEffectsFactory(SessionSpec spec, Node3D worldRoot, Func<Vector3> playerPosition,
        EffectAmbience? ambience = null)
    {
        _spec = spec;
        _worldRoot = worldRoot;
        _playerPosition = playerPosition;
        _ambience = ambience ?? EffectAmbience.Still;
    }

    /// <summary>The session's <see cref="UI.ScreenFlash"/> sink, handed to every runtime this
    /// factory builds — an <c>FBFX_COLOR_FROM_TO</c> wash is screen-space and session-owned, and
    /// the runtimes built here are the ones that play the defs carrying one (<c>he_ground_effect</c>,
    /// <c>ap_ground_effect</c>, <c>flak_effect</c>). Set once, before the first build; null leaves
    /// the event undrawn.</summary>
    public Action<Color, Color, float>? ScreenFlash { get; set; }

    /// <summary>The world-effects template stage — the subtree
    /// <see cref="EffectCatalogue.WorldStageRoots"/>' roots are built into, one
    /// <c>pool&lt;N&gt;</c> container per slot. Null until
    /// <see cref="EnsureWorldEffects"/> has built the runtime. Exposed so the <c>--effects-test</c>
    /// census can report the MESH half: a puffer count says nothing about whether the
    /// template's meshes are visible, and they are half of what an effect looks like. Observation
    /// only — the stage is owned here and hangs under the world root.</summary>
    public Node3D? EffectStage { get; private set; }

    /// <summary>The effect-template ROOT names the world-effects stage builds — the set
    /// <see cref="EffectPools"/> sizes, exposed so the committed pool config can be checked
    /// against what a bound chapter actually stages (a root renamed on one side and not the other
    /// would otherwise size nothing, silently). Forwards to
    /// <see cref="EffectCatalogue.WorldStageRoots"/>: there is no hand table left to read, so the
    /// answer needs the bound program and that chapter's gamez, exactly as the build does.</summary>
    public static IReadOnlyList<string> EffectStageRootNames(AnimProgram program, GameZ gamez) =>
        EffectCatalogue.WorldStageRoots(program, StageRootResolver(gamez));

    /// <summary>Builds the named template ROOTS from the world gamez as children of
    /// <paramref name="parent"/> (a pool slot, the crash root, the lab stage), each reset to sit at
    /// the stage origin — a CALL_ANIMATION relocates them onto the call site. Returns how many
    /// built. The roots are always a derivation's output
    /// (<see cref="EffectCatalogue.WorldStageRoots"/>/<see cref="EffectCatalogue.CrashStageRoots"/>),
    /// never a hand list, so a def anchored on a root nobody stages fails the build instead of
    /// playing nothing.</summary>
    public static int BuildEffectStage(GameZ gamez, SceneBuilder scene, Node3D parent,
        IEnumerable<string> roots)
    {
        int n = 0;
        foreach (var rootName in roots)
        {
            // An effect template is pure presentation and gets NO colliders: several carry
            // intersect_surface (he_ringer, the splash models), and the authored ring scales to
            // 7-20x, so a collidable copy relocated onto an impact point would leave an invisible
            // plate up to ~170 m across floating at the blast site.
            if (gamez.FindByName(rootName) is { } node
                && scene.BuildSubtree(node, collisionSkip: _ => true) is { } built)
            {
                built.Transform = Transform3D.Identity; // sit at the stage; reposition moves it on call
                parent.AddChild(built);
                n++;
            }
        }
        return n;
    }

    /// <summary>The anchor lookup both binds hand <see cref="EffectCatalogue.StageRootsFor"/>
    /// (WORLD-21 — one resolver, two scopes): a parentless gamez node is a template ROOT the bind
    /// must build; any other gamez node of that name rides inside one already staged above it
    /// (<c>ap_cracks</c> under <c>ap_effect</c>); a name the bind's own <paramref name="scope"/>
    /// already carries — the crash rig's <c>player</c> scaffold, its wreck, the plane's own parts —
    /// is satisfied without a template; anything else resolves nowhere. Gamez roots are tested FIRST,
    /// so a template the scope has already staged still reads as a root it needs.</summary>
    public static Func<string, AnchorPlacement> StageRootResolver(GameZ gamez, Node3D? scope = null)
    {
        var parented = new HashSet<int>();
        foreach (var n in gamez.Nodes)
            foreach (var c in n.Children)
                parented.Add(c);
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in gamez.Nodes)
        {
            known.Add(n.Name);
            if (!parented.Contains(n.Index))
                roots.Add(n.Name);
        }
        var inScope = scope != null ? NamesUnder(scope) : null;
        return name =>
        {
            if (roots.Contains(name))
                return AnchorPlacement.Stage;
            if (inScope != null && inScope.Contains(name))
                return AnchorPlacement.InScope;
            return known.Contains(name) ? AnchorPlacement.InScope : AnchorPlacement.Missing;
        };
    }

    /// <summary>What the per-player crash rig stages, for a scope it has not built yet — the same
    /// call <see cref="BuildFlightCrashRuntime"/> makes, exposed so the <c>effects-census</c> suite
    /// can ask it on a replica rig (the wreck and part names vary by airframe, so the answer is
    /// per-plane).</summary>
    public static IReadOnlyList<string> CrashStageRootNames(AnimProgram program, GameZ gamez,
        Node3D rigScope) =>
        EffectCatalogue.CrashStageRoots(program, StageRootResolver(gamez, rigScope));

    /// <summary>Stages the crash rig's pooled effect-template copies under
    /// <paramref name="crashRoot"/> — one <c>poolN</c> slot container per depth level, every copy
    /// hidden, exactly as the world-effects stage stages its templates (D31): the flake/gunhit
    /// family carries meshes its own defs never deactivate, so an unhidden copy draws stacked at
    /// the plane's centre for the whole session. The stage's reveal ritual (<c>Shown</c>) lights
    /// the CALL's own copy while its effect plays. One staging for the production rig and the
    /// <c>crash-rig-anchors</c> suite. Returns (copies staged, of which in slot 0).</summary>
    public static (int Roots, int Slot0) StageCrashTemplates(GameZ gamez, SceneBuilder worldScene,
        Node3D crashRoot, IReadOnlyList<string> rootNames, Utils.EffectPools pools)
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
                rootNames.Where(r => pools.CrashSlotsFor(r) > at));
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
    /// staged hidden like the world-effects stage (the flake/gunhit family carries meshes its own
    /// defs never deactivate — before BL-288 pooled them here they lived in the world-effects
    /// stage, whose reveal ritual was what kept them dark), and place-exempt for the
    /// airframe-scoped anchor NAMEs (<see cref="EffectCatalogue.AirframeScopedAnchors"/>): the
    /// player damage/reset defs are authored NAME=<c>player_pfighter</c>, which on the Devastator
    /// is the aircraft's own model root, and a placing call must resolve it (the defs' node ops run
    /// on the plane) without ever relocating it. One factory for the production rig and the
    /// <c>crash-rig-anchors</c> suite, so the two cannot drift apart on this policy.</summary>
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

    /// <summary>The session's one world-effects runtime (the only way to reach
    /// <see cref="BuildWorldEffectsRuntime"/> — see its own doc for the two-runtimes bug a second
    /// entry point caused), built on first demand and wired into the world runtime's
    /// <see cref="AnimRuntime.ExternalEffect"/> and, when <paramref name="projectiles"/> is given,
    /// the pool's <c>EffectSink</c> — both gated on "unset" so a caller that already wired one (or
    /// calls again on a later demand) leaves it alone. A plane-less <c>--freecam</c>/<c>--anim-lab</c>
    /// passes no <paramref name="projectiles"/> and gets none wired, which is why a kill there draws
    /// nothing until something asks. Returns null when the build fails — the HP/kill/swap/reset
    /// mechanics do not depend on it.</summary>
    public AnimRuntime? EnsureWorldEffects(GameZ gamez, SceneBuilder worldScene,
        TextureArchive textures, AnimProgram worldProgram, AnimRuntime worldRuntime,
        ProjectilePool? projectiles = null)
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
            worldRuntime.ExternalEffect = (name, pt, node) => effects.Handles(name) && effects.PlayEffectAt(name, pt, node);
            worldRuntime.ExternalEffectStop = name => effects.Stop(name);
        }
        if (projectiles != null && projectiles.EffectSink == null)
        {
            projectiles.EffectSink = (name, pt, ttl) => effects.PlayEffectAt(name, pt, null, ttl);
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
    /// the wreck + templates until <see cref="FlightController.Crash"/> plays the def. The same
    /// runtime also carries <c>startprops</c>/<c>stopprops</c> (<see cref="EffectCatalogue.PropChoreographyAnims"/>)
    /// — their own NAME never resolves on any airframe, so <c>FlightController</c>'s direct
    /// <c>Play</c> calls pass the plane model itself as the fallback anchor.</summary>
    public void BuildFlightCrashRuntime(FlightController controller, PlaneBuilder planeBuilder,
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
        // splitscreen crashes do not collide. Staged hidden below, revealed per call. Derived from
        // the defs this rig is about to bind, against this aircraft's own scope: an anchor that
        // resolves nowhere throws EffectAnchorException naming the def and the node, which is the
        // whole point — the failure it replaces was a def silently anchored on nothing. Asked
        // BEFORE the templates and the wreck go in, so the answer cannot depend on what a previous
        // step of this same build happened to add. ⚠ The crash root is parented FIRST for exactly
        // this: `player` is the crash defs' own anchor and lives nowhere in a chapter's gamez, so a
        // scope without it reports the whole rig unanchorable. Parenting it here rather than after
        // the wreck leaves both subtrees' child order untouched — the templates still go in before
        // the wreck, and the crash root still sits between the plane model and the runtime.
        controller.AddChild(crashRoot);
        var rootNames = CrashStageRootNames(crashProgram, gamez, controller);
        // Staged in pool slots like the world-effects stage: the
        // damage-stage menu CALLs one template from up to eight distinct anchors (each pdpanelN
        // onto its own pdpN, the crash defs onto their four pieceN), and a single shared copy
        // would be relocated-and-restarted onto every new tear, discarding the previous panel's
        // burst mid-flight. Sizes per root from effect_pools.json's crash section — most crash
        // templates stay single-copy in slot 0; the per-panel family gets one copy per authored
        // call anchor. The runtime's caller-slot assignment (AnimRuntime.AssignCallerSlot) pins
        // each call anchor to its own slot on the first tear.
        var (effectRoots, slot0Roots) = StageCrashTemplates(gamez, worldScene, crashRoot,
            rootNames, _pools);

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

        // The scoped crash runtime: no ambient start (nothing runs until the crash Plays the def),
        // puffers baked lazily via the session textures (kept open above), effect templates
        // relocated onto the call site, and the crash def's non-portable node ptrs resolved by name.
        // ⚠ The emitter factory parents at the WORLD root, NOT the crash root (its own doc carries
        // the measurement). Each crash runtime still makes its own emitter instances at
        // its own crash site, so splitscreen crashes stay independent. The SOUND either variant
        // reaches (snd_exp_ground_a on the dirt def itself, snd_exp_water_a inside the sea dive's
        // plane_big_splash) is already played by FlightAudio from Crash(); this runtime has no audio
        // session, so dispatching it here would only emit the "silent for the session" warning —
        // render effects, not sound. The seed drives wreckage scatter and the
        // crash def's RANDOM_WEIGHT verdicts: one draw per player off the advancing crash stream, so
        // splitscreen crashes differ from each other but repeat run to run.
        // The template stage, sealed before the runtime exists: pooled,
        // because the slot containers above are what the caller-slot assignment picks a copy out of
        // (a template staged single-copy, which is most of the crash set, behaves identically);
        // relocating its called templates onto the call site; staged hidden with the reveal
        // ritual on, because the flake/gunhit family's meshes have no authored deactivation (see
        // NewCrashTemplateStage); and place-exempt for the airframe-scoped anchor NAMEs, so the
        // Devastator's own model — the one airframe those defs' NAME resolves on — is never
        // relocated like a template.
        var crashRuntime = AnimRuntime.ForCrashRig(
            NewCrashTemplateStage(_spec.DebugAnim),
            Rng.NewIntSeed(Rng.Crash),
            new PufferEmitterFactory(textures, _worldRoot, _ambience), _spec.DebugAnim);
        // Excuses the ground splash from the momentum nudge FlightController.Crash sets on
        // InheritedWorldVelocity — set once here, unlike the velocity itself (which is
        // per-crash), because which defs are exempt never changes across a session.
        crashRuntime.InheritedVelocityExempt =
            new HashSet<string>(EffectCatalogue.GroundSplashAnimNames, StringComparer.OrdinalIgnoreCase);
        // The crash def's own surface-hugging sub-effects (the water splash's flat rings/spray
        // column, the dirt burst's dust plane) level to world axes instead of inheriting the plane's
        // impact attitude — set once here, like the exempt set above, since the named
        // defs only ever play from within a crash sequence; never reached by the in-flight
        // damage-stage effects this same runtime also plays, so no per-crash toggle is needed.
        crashRuntime.LevelPlacedTemplateNames =
            new HashSet<string>(EffectCatalogue.CrashSurfaceLevelAnimNames, StringComparer.OrdinalIgnoreCase);
        // The wreck pieces are `do_intersections: true` — `player_crash_dirt`'s `piece1`-`4` are 4
        // of the 16 (def, node) pairs in all 8 chapters that ask for the original's collider test
        // AND stay in the world. Handing the mask over is what turns their sweep on; a session that
        // builds no colliders hands nothing and they keep flying their authored clock out, which is
        // exactly the behaviour every golden capture recorded.
        if (_spec.BuildsCollision)
        {
            crashRuntime.ContactMask = CollisionLayers.World;
            // `player_crash_dirt`'s own pieces author no `water` branch — the wet crash is a
            // separate def — so this changes nothing for them today. Bound anyway: the rig plays
            // both crash variants, and the hook is what keeps the branch choice reading the same
            // classifier the crash surface itself was picked with.
            crashRuntime.SurfaceIsWater =
                body => ProjectilePool.ClassifySurface(body as Node) == SurfaceClass.Water;
        }
        // Bind only the named defs' transitive CALL_ANIMATION closures (Subset), never the whole
        // world program: the full 800+ defs include ~150 generic-named world defs that would
        // mis-anchor onto this plane's parts and run their reset states on the aircraft. The set
        // (EffectCatalogue.CrashRigAnimNames) is the crash defs, the damage-effect shims, the prop
        // choreography and the authored damage-stage menu — every def that plays ON this aircraft.
        // Both crash variants are bound because the surface is only known at the moment of impact
        // (FlightController.ClassifySurface); Air stays out, having no trigger.
        crashRuntime.Bind(controller, crashProgram.Subset(EffectCatalogue.CrashRigAnimNames));
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
        if (slot0Roots != rootNames.Count)
            Log.Warn("anim", $"crash rig '{planeName}': staged {slot0Roots} of {rootNames.Count} template root(s) the bound defs anchor on — the rest built nothing from this chapter's gamez, so their defs play nothing");
        foreach (var unknown in _pools.UnknownCrashRoots(rootNames))
            Log.Warn("anim", $"effect pools: crash root '{unknown}' is not staged by this rig — it sizes nothing");
        if (verbose)
            GD.Print($"data-crash: {effectRoots} effect template cop(ies) over {_pools.CrashDepthFor(rootNames)} pool slot(s) "
                     + $"+ {restPoses.Count} wreck node(s) — crash runtime bound (scoped, no auto-start)");
    }

    /// <summary>Every name a bind's own scope answers — the Godot node name and the gamez
    /// <see cref="AnimRuntime.NameMeta"/> both, since name resolution reads the meta.</summary>
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

    /// <summary>Builds the one world-effects runtime — the world-scoped generalization of the
    /// per-player crash runtime. It stages the impact/destruction effect templates under a
    /// dedicated subtree so their names resolve locally without colliding with the world or the crash
    /// roots, keeps a live <c>IEmitterFactory</c> over the session textures, and binds the closure of
    /// <see cref="EffectCatalogue.EffectAnimNames"/>. <see cref="AnimRuntime.PlayEffectAt"/> then stages any of those
    /// effects at a hit or death point: <c>ProjectilePool.EffectSink</c> calls it on a weapon impact,
    /// and the world runtime's <see cref="AnimRuntime.ExternalEffect"/> routes a death's
    /// CALL_ANIMATION here. Puffers parent at world level (the crash lesson) so the stage does
    /// not suppress them.
    ///
    /// <para>The stage itself is visible and each template ROOT starts hidden
    /// (<see cref="Mech3.Anim.TemplateStage{TNode}.Shown"/> reveals one for as long as an effect
    /// plays on it): a template's meshes are half the effect — the rocket's authored per-type rings, the
    /// fireball facades, the splash models — and hiding the whole stage would render none of
    /// them. Inside a revealed root the data still decides what shows: every ring is reset
    /// INACTIVE or opacity-OFF at bootstrap and its own def turns it on.</para>
    ///
    /// <para>⚠ Private — <see cref="EnsureWorldEffects"/> is the only way in. A caller that builds its
    /// own copy alongside the cached one recreates the two-runtimes bug: a <c>--fly --destroy=</c> session's
    /// direct call here, followed by the damage lab's own <see cref="EnsureWorldEffects"/> lookup
    /// finding the cache empty, built two runtimes, each carrying <c>EffectPoolSlots</c> × ~38
    /// template subtrees.</para></summary>
    private AnimRuntime BuildWorldEffectsRuntime(GameZ gamez, SceneBuilder worldScene,
        TextureArchive textures, AnimProgram worldProgram)
    {
        var stage = new Node3D { Name = "world_effects" };
        _worldRoot.AddChild(stage);
        EffectStage = stage;
        // The pool: each root staged in as many copies as effect_pools.json sizes it for
        // THIS session's player count, one copy per slot container, and AnimRuntime hands the next
        // slot to each call. The containers carry only the slot meta and no cs_name, so they are
        // invisible to name resolution — what keeps the copies apart is the slot, read off the
        // anchor a call runs on. Sizes differ per root, so the deeper slots hold only the roots
        // sized that deep (the shared gun family lives in slot 0 alone); a def whose root has no
        // copy in its slot falls back to one that exists.
        // What to stage is DERIVED from the names about to be bound:
        // every definition their call closure reaches, anchored on the gamez root its NAME names.
        // An anchor that resolves nowhere throws here, naming the def and the node, instead of
        // leaving that def anchored on nothing and playing nothing at all — EnsureWorldEffects
        // turns the throw into its "runtime could not be built" warning, carrying the anchor list.
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
        // The impact/death SOUND an effect def carries is already played by the projectile pool
        // or the world runtime; this runtime only renders the puffers. Several gun
        // effects gate their puffer behind RANDOM_WEIGHT, so this runtime's dice — its own stream
        // off the master seed — decide which effects render at all.
        // Puffer.Create pairs the depth fade with the blend it derives — off for MIX, whose dark
        // sprites emit at these ground-level sites and measured near-invisible with it on (the
        // damage-stage black smoke), on for additive fire, which leaks through the fade anyway.
        // The template stage, sealed before the runtime exists:
        // pooled over the slot containers built above, staged hidden so the reveal ritual lights the
        // one root a call lands on (the ⚠ above), and relocating a called template onto the call
        // site. Do not write stage options onto the runtime after this factory call returns — that
        // is a sealing leak that only works when the option happens to be read after Bind.
        var effects = AnimRuntime.ForEffects(
            AnimRuntime.NewTemplateStage(pooled: true, shown: true, placesCalled: true,
                debugMotions: _spec.DebugAnim),
            Rng.IntSeedFor(Rng.Effects),
            new PufferEmitterFactory(textures, _worldRoot, _ambience),
            _spec.DebugAnim, EffectRuntimeTtl, _playerPosition);
        effects.ScreenFlash = ScreenFlash;
        // Bind name resolution to the template stage — so the effect names resolve to these
        // templates and not to the world's or the crash roots' same-named nodes — but parent the
        // runtime node itself under the visible world root, a plain logic node that self-ticks.
        effects.Bind(stage, worldProgram.Subset(EffectCatalogue.EffectAnimNames));
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
                 + $"{EffectCatalogue.EffectAnimNames.Length} effect name(s) bound");
        foreach (var unknown in _pools.UnknownRoots(roots))
            Log.Warn("anim", $"effect pools: '{unknown}' is not an effect stage root — it sizes nothing");
        return effects;
    }
}
