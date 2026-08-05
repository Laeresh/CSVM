using System;
using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Mech3.Anim;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>Builds the impact/destruction effect stages and the per-player crash runtime
/// (PLAN-planeviewer-split A4, moved verbatim off <c>GameSession</c>): the world-effects runtime
/// (D32, one per session, lazily built on first demand) and <see cref="BuildFlightCrashRuntime"/>
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
    // calls to one effect each keep their own (BL-225 — before this, the one copy was relocated
    // and the first blast's trails jumped to the second's site). A call takes the next slot and
    // the cursor wraps, so this is the concurrency the pool covers before two calls share a copy
    // again.
    // ⚠ INVENTED, not decoded — the original copies its templates per call and has no such
    // number. Which is exactly why the sizes live in `CSVM/data/effect_pools.json` (EffectPools)
    // rather than in a const here: per ROOT, scaled by the session's player count, editable
    // without a rebuild. Recorded in backlog.md's TUNE list; the runtime counts (and names once)
    // every call that wraps onto a live slot — AnimRuntime.PoolRecycles is what to size against.

    // The crash/effect template roots (world-gamez nodes WorldBuilder skips, because the world
    // never renders them ambiently — they exist to be instanced onto a kill/crash site). Built into
    // the anim lab's stage so a played effect def resolves the puffer host that rides its own root.
    // This is the FULL anchor-root set both crash variants' call closures need: the first seven are
    // the dirt crash's, the last four the sea dive's (analysis/effect-anchor-roots/, re-run for
    // player_crash_water — each a single parentless root in all 8 chapters). A root left out leaves
    // every def anchored on it unanchored, so it plays nothing at all and says nothing about it.
    private static readonly string[] EffectTemplateRoots =
    {
        "yellow_spark_01", "yellow_spark_02", "flame_ball_01", "black_smoke_ball_01", "fire_here",
        "carnage_trails", "flydirt",
        // the sea dive: plane_big_splash's model, its hg_splasher puffer host and plane_big_ripple's
        // ring set, plus large_steam_spray's own root
        "huge_splash_model", "hg_splash", "ripple", "white_water_impact",
    };

    // The gamez template roots those effects' meshes and puffers ride — staged under the
    // world-effects stage so a PlayEffectAt relocates one onto the hit/death point. This is the
    // FULL set of anchor roots the EffectCatalogue.EffectAnimNames call closure needs (derived from the reader
    // defs by analysis/effect-anchor-roots/); a root left out leaves every def anchored on it
    // unanchored, so it plays nothing at all — which is how the rocket explosion lost its
    // per-type rings (ring_ap/ring_he/ring_sonic), its trail columns and the torpedo ripple.
    // All present as a single parentless root in every chapter's gamez (checked, all 8).
    private static readonly string[] EffectStageRoots =
    {
        "gunhit", "dum_gunhit", "mag_gunhit", "flame_ball_01", "flame_ball_02", "he_ring",
        "ap_effect", "flak_control", "flash_control", "sonic_effect", "scatter_trails",
        "torp_effects", "rear_flash_control", "fire_here", "moving_fire_ball_01",
        "black_smoke_ball_01", "zep_ng_dstry1.flt", "huge_splash_model", "partial_damage_obj",
        // the rocket-explosion rings and their companions (D31): the HE upper ring, the four
        // rising sonic rings + the falling one, the AP/HE/flak/torpedo smoke-trail columns, the
        // sonic puff clusters, and the torpedo ring/ripple/splash.
        "he_ring1", "sonic_ring1", "sonic_ring2", "sonic_ring3", "sonic_ring4", "sonic_ring5",
        "ap_trails", "he_trails", "flak_trails", "carnage_trails", "carnage_ring",
        "sonic_puff1", "sonic_puff2", "hg_splash", "ripple",
        // the graze reaction's three anchor roots plus the spark cluster `touchdown_default`
        // CALL_ANIMATIONs (small_yellow_sparks anchors on `yellow_spark_01`). All four exist as a
        // single parentless root in every chapter (analysis/effect-anchor-roots/).
        "spark_touchdown", "dust_touchdown", "splash_touchdown", "yellow_spark_01",
    };

    // The player crash-anchor set: meshless nodes named exactly the crash def's targets (its
    // anim_root 'player' plus healthy/destroyed/pieces). Built into the lab stage so a played crash
    // def anchors to this 'player' and resolves 'healthy'/'destroyed' HERE — locally, in front of
    // the camera — rather than falling through to one of the world's 217 generic 'healthy' nodes.
    // The effects (relocated templates) attach to these; the plane/wreck geometry is the crash
    // plan's Layer-2 work.
    private static readonly string[] CrashAnchorNodes =
        { "healthy", "destroyed", "dontmove", "markers", "piece1", "piece2", "piece3", "piece4", "shadow", "cockpit1" };

    // Roots the derivation asks for that the tables above do not stage — real silent misses, each
    // named so the equality tripwire reports a KNOWN gap instead of drifting, and each a behaviour
    // change to close rather than a table typo to fix.
    //
    // `ballflare.flt` is the torpedo explosion's flare, called by torpedo_ground_effect and
    // torpedo_water_effect. The offline instrument could not see it: it keys definitions by
    // ANIMATION_NAME, and this definition declares only a NAME, so it sat in that script's null
    // bucket and never entered the closure. A single parentless root in all 8 chapters, and its
    // only node is itself — unlike the AT_NODE case in EffectCatalogue.CallSuppliedAnchors, the
    // retarget target's subtree cannot supply it, so today the flare plays nothing at all.
    private static readonly string[] WorldStageRootGaps = { "ballflare.flt" };

    // `rem_pas` (anchor `apassengers`) is called by both crash variants with NO AT_NODE, so it
    // anchors on its own template root — which this rig has never staged, so the crash's
    // passenger-removal step plays nothing. Listed by analysis/effect-anchor-roots/FINDINGS.md
    // among the 11 roots the crash closure needs; the table shipped with 11 that swapped it for
    // `yellow_spark_02`.
    private static readonly string[] CrashTemplateRootGaps = { "apassengers" };

    private readonly SessionSpec _spec;
    private readonly Node3D _worldRoot;
    private readonly Func<Vector3> _playerPosition;

    // The authored per-root pool sizes (see the EffectPoolSlots remark above). Read once per
    // session, like the rest of this factory's inputs.
    private readonly EffectPools _pools = EffectPools.Load();

    private AnimRuntime? _worldEffects;

    public WorldEffectsFactory(SessionSpec spec, Node3D worldRoot, Func<Vector3> playerPosition)
    {
        _spec = spec;
        _worldRoot = worldRoot;
        _playerPosition = playerPosition;
    }

    /// <summary>The effect-template ROOT names the world-effects stage builds — the set
    /// <see cref="EffectPools"/> sizes, exposed read-only so the committed pool config can be
    /// checked against the live table (a root renamed on one side and not the other would
    /// otherwise size nothing, silently).</summary>
    public static IReadOnlyList<string> EffectStageRootNames => EffectStageRoots;

    /// <summary>The world-effects template stage — the subtree <see cref="EffectStageRoots"/> is
    /// built into, one <c>pool&lt;N&gt;</c> container per slot. Null until
    /// <see cref="EnsureWorldEffects"/> has built the runtime. Exposed so the <c>--effects-test</c>
    /// census can report the MESH half (`BL-061`): a puffer count says nothing about whether the
    /// template's meshes are visible, and they are half of what an effect looks like. Observation
    /// only — the stage is owned here and hangs under the world root.</summary>
    public Node3D? EffectStage { get; private set; }

    /// <summary>Builds the <see cref="EffectTemplateRoots"/> from the world gamez as children of
    /// <paramref name="parent"/> (the lab stage), each reset to sit at the stage origin — a
    /// CALL_ANIMATION relocates them onto the call site. Returns how many built.</summary>
    public static int BuildEffectStage(GameZ gamez, SceneBuilder scene, Node3D parent) =>
        BuildEffectStage(gamez, scene, parent, EffectTemplateRoots);

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

    /// <summary>The world-effects bind's equality tripwire: does the derivation still say what
    /// <see cref="EffectStageRoots"/> says, up to <see cref="WorldStageRootGaps"/>? Null when it
    /// agrees, else the difference, named — a root the hand table would have to gain, or one it
    /// carries that nothing anchors on. Run as an <c>effects-census</c> condition, per chapter,
    /// because the answer is chapter data.</summary>
    public static string? WorldStageRootDrift(AnimProgram program, GameZ gamez) =>
        Drift("EffectStageRoots", EffectStageRoots,
            () => EffectCatalogue.StageRootsFor(program, EffectCatalogue.EffectAnimNames,
                StageRootResolver(gamez)),
            WorldStageRootGaps);

    /// <summary>The crash bind's half of the same tripwire, against
    /// <see cref="EffectTemplateRoots"/>. Per-plane: <paramref name="rigScope"/> is the bound
    /// controller subtree, whose wreck and part names vary by airframe.
    /// <see cref="CrashTemplateRootGaps"/> is subtracted — a root the derivation asks for that the
    /// rig knowingly does not stage.</summary>
    public static string? CrashStageRootDrift(AnimProgram program, GameZ gamez, Node3D rigScope)
    {
        var rigAnims = new List<string>(EffectCatalogue.CrashDefNames);
        rigAnims.AddRange(EffectCatalogue.PlaneDamageEffectAnims);
        return Drift("EffectTemplateRoots", EffectTemplateRoots,
            () => EffectCatalogue.StageRootsFor(program, rigAnims, StageRootResolver(gamez, rigScope)),
            CrashTemplateRootGaps);
    }

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
    /// entry point caused, `BL-232`), built on first demand and wired into the world runtime's
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
    /// the wreck + templates until <see cref="FlightController.Crash"/> plays the def.</summary>
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
        // ⚠ The emitter factory parents at the WORLD root, NOT the crash root (its own doc carries
        // the measurement). Each crash runtime still makes its own emitter instances at
        // its own crash site, so splitscreen crashes stay independent. The SOUND either variant
        // reaches (snd_exp_ground_a on the dirt def itself, snd_exp_water_a inside the sea dive's
        // plane_big_splash) is already played by FlightAudio from Crash(); this runtime has no audio
        // session, so dispatching it here would only emit the "silent for the session" warning —
        // render effects, not sound. The seed drives wreckage scatter and the
        // crash def's RANDOM_WEIGHT verdicts: one draw per player off the advancing crash stream, so
        // splitscreen crashes differ from each other but repeat run to run.
        var crashRuntime = AnimRuntime.ForCrashRig(Rng.NewIntSeed(Rng.Crash),
            new PufferEmitterFactory(textures, _worldRoot), _spec.DebugAnim);
        // Bind only the named defs' transitive CALL_ANIMATION closures (Subset), never the whole
        // world program: the full 800+ defs include ~150 generic-named world defs that would
        // mis-anchor onto this plane's parts and run their reset states on the aircraft. The set is
        // the crash defs plus the damage-effect shims — every def that plays ON this aircraft.
        // Both crash variants are bound because the surface is only known at the moment of impact
        // (FlightController.ClassifySurface); Air stays out, having no trigger.
        var rigAnims = new List<string>(EffectCatalogue.CrashDefNames);
        rigAnims.AddRange(EffectCatalogue.PlaneDamageEffectAnims);
        crashRuntime.Bind(controller, crashProgram.Subset(rigAnims));
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
        // The staged set against the one the bound defs actually ask for, per plane (the wreck and
        // part names vary by airframe). A silent drift here is the failure mode EffectTemplateRoots'
        // own remark names, so it is said out loud at the bind rather than left to a suite.
        if (CrashStageRootDrift(crashProgram, gamez, controller) is { } drift)
            Log.Warn("anim", $"crash rig '{planeName}': {drift}");
        if (verbose)
            GD.Print($"data-crash: {effectRoots} effect template(s) + {restPoses.Count} wreck node(s) — "
                     + "crash runtime bound (scoped, no auto-start)");
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

    /// <summary>Derived vs hand table, as one line or null. The derivation's own structured error is
    /// reported rather than thrown: this is a tripwire, and a chapter whose data cannot satisfy an
    /// anchor is exactly what it exists to say.</summary>
    private static string? Drift(string table, IEnumerable<string> hand,
        Func<IReadOnlyList<string>> derive, IEnumerable<string> knownGaps)
    {
        IReadOnlyList<string> derived;
        try
        {
            derived = derive();
        }
        catch (EffectAnchorException e)
        {
            return $"{table}: derivation failed — {e.Message}";
        }
        var staged = new HashSet<string>(hand, StringComparer.OrdinalIgnoreCase);
        var wanted = new HashSet<string>(derived, StringComparer.OrdinalIgnoreCase);
        var gaps = new HashSet<string>(knownGaps, StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();
        foreach (var name in derived)
            if (!staged.Contains(name) && !gaps.Contains(name))
                missing.Add(name);
        var spare = new List<string>();
        foreach (var name in hand)
            if (!wanted.Contains(name))
                spare.Add(name);
        if (missing.Count == 0 && spare.Count == 0)
            return null;
        var parts = new List<string>();
        if (missing.Count > 0)
            parts.Add($"needs but does not stage: {string.Join(", ", missing)}");
        if (spare.Count > 0)
            parts.Add($"stages but nothing anchors on: {string.Join(", ", spare)}");
        return $"{table}: {string.Join("; ", parts)}";
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

    /// <summary>Builds the one world-effects runtime (D32) — the world-scoped generalization of the
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
    /// (<see cref="AnimRuntime.ShowPlacedTemplates"/> reveals one for as long as an effect plays on
    /// it): a template's meshes are half the effect — the rocket's authored per-type rings, the
    /// fireball facades, the splash models — and hiding the whole stage rendered none of them
    /// (D31). Inside a revealed root the data still decides what shows: every ring is reset
    /// INACTIVE or opacity-OFF at bootstrap and its own def turns it on.</para>
    ///
    /// <para>⚠ Private — <see cref="EnsureWorldEffects"/> is the only way in. A caller that built its
    /// own copy alongside the cached one is exactly `BL-232`: a <c>--fly --destroy=</c> session's
    /// direct call here, followed by the damage lab's own <see cref="EnsureWorldEffects"/> lookup
    /// finding the cache empty, built two runtimes, each carrying <c>EffectPoolSlots</c> × ~38
    /// template subtrees.</para></summary>
    private AnimRuntime BuildWorldEffectsRuntime(GameZ gamez, SceneBuilder worldScene,
        TextureArchive textures, AnimProgram worldProgram)
    {
        var stage = new Node3D { Name = "world_effects" };
        _worldRoot.AddChild(stage);
        EffectStage = stage;
        // The pool (BL-225): each root staged in as many copies as effect_pools.json sizes it for
        // THIS session's player count, one copy per slot container, and AnimRuntime hands the next
        // slot to each call. The containers carry only the slot meta and no cs_name, so they are
        // invisible to name resolution — what keeps the copies apart is the slot, read off the
        // anchor a call runs on. Sizes differ per root, so the deeper slots hold only the roots
        // sized that deep (the shared gun family lives in slot 0 alone); a def whose root has no
        // copy in its slot falls back to one that exists.
        int players = Math.Max(1, _spec.Players);
        int depth = _pools.DepthFor(EffectStageRoots, players);
        int staged = 0;
        for (int slot = 0; slot < depth; slot++)
        {
            var pool = new Node3D { Name = $"pool{slot}" };
            pool.SetMeta(AnimRuntime.PoolSlotMeta, slot);
            stage.AddChild(pool);
            int at = slot;
            staged += BuildEffectStage(gamez, worldScene, pool,
                Array.FindAll(EffectStageRoots, r => _pools.SlotsFor(r, players) > at));
            foreach (var child in pool.GetChildren())
                if (child is Node3D root)
                    root.Visible = false;
        }
        // The impact/death SOUND an effect def carries is already played by the projectile pool
        // (D30) or the world runtime (D31); this runtime only renders the puffers. Several gun
        // effects gate their puffer behind RANDOM_WEIGHT, so this runtime's dice — its own stream
        // off the master seed — decide which effects render at all.
        // Puffer.Create pairs the depth fade with the blend it derives — off for MIX, whose dark
        // sprites emit at these ground-level sites and measured near-invisible with it on (the
        // damage-stage black smoke), on for additive fire, which leaks through the fade anyway.
        var effects = AnimRuntime.ForEffects(Rng.IntSeedFor(Rng.Effects),
            new PufferEmitterFactory(textures, _worldRoot),
            _spec.DebugAnim, EffectRuntimeTtl, _playerPosition);
        // Bind name resolution to the template stage — so the effect names resolve to these
        // templates and not to the world's or the crash roots' same-named nodes — but parent the
        // runtime node itself under the visible world root, a plain logic node that self-ticks.
        effects.ShowPlacedTemplates = true;
        effects.PooledTemplates = true;
        effects.Bind(stage, worldProgram.Subset(EffectCatalogue.EffectAnimNames));
        _worldRoot.AddChild(effects);
        int wanted = 0;
        foreach (var r in EffectStageRoots)
            wanted += _pools.SlotsFor(r, players);
        // Name the sizes, not just the total: "143 staged" cannot say whether a root the tester
        // just re-sized actually got its copies. Grouped by size so the line stays one line.
        var bySize = new SortedDictionary<int, List<string>>();
        foreach (var r in EffectStageRoots)
            bySize.TryAdd(_pools.SlotsFor(r, players), new List<string>());
        foreach (var r in EffectStageRoots)
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
        foreach (var unknown in _pools.UnknownRoots(EffectStageRoots))
            Log.Warn("anim", $"effect pools: '{unknown}' is not an effect stage root — it sizes nothing");
        return effects;
    }
}
