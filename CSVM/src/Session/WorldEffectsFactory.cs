using System;
using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Flight;
using CSVM.Mech3;
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
    // The impact/destruction effect ANIMATION names the world-effects runtime (D32) is bound to —
    // the closure of these is staged and playable via PlayEffectAt. IMPACT names come from
    // weapons.json (the non-model `default`/`buildings` effects of rockets/ordnance; the gun
    // `*_gunhit` family is bound so a later guns pass can reach it, but is not fired per-round);
    // destruction names are the ones death sequences CALL_ANIMATION. `random_gun_impact` (root
    // `player`, a player-plane hit) is excluded — unreachable in M3 and its generic root would
    // mis-anchor. Verified against extracted/*/cam_anim: every name resolves in all 8 chapters.
    public static readonly string[] EffectAnimNames =
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
        // progressive damage-stage effects DAMAGE_SEQUENCEs call (the smoke/fire sputter at the
        // 0.60/0.30 HP stages). The install-wide DAMAGE_SEQUENCE call set is exactly these two
        // plus C4's one-off `b_steamtrail`, which is excluded: its anim root is the live train
        // subtree, not a relocatable effect template.
        "sputter_black_smoke_obj", "sputter_fire_smoke_obj",
        // the airframe's per-surface graze reaction (touchdown.zrd): sparks off a hard surface,
        // dust off terrain, a splash off water. FlightController.SurviveHit plays one per contact.
        "touchdown_default", "touchdown_dirt", "touchdown_water",
    };

    // A stop-less sustained effect (large_30sec_fire) would emit for the whole session; the
    // world-effects runtime bounds every PlayEffectAt instance to this many seconds (past the 30 s
    // fire, so it completes), then tears its puffers down.
    private const float EffectRuntimeTtl = 32f;

    // The crash/effect template roots (world-gamez nodes WorldBuilder skips, because the world
    // never renders them ambiently — they exist to be instanced onto a kill/crash site). Built into
    // the anim lab's stage so a played effect def resolves the puffer host that rides its own root.
    private static readonly string[] EffectTemplateRoots =
        { "yellow_spark_01", "yellow_spark_02", "flame_ball_01", "black_smoke_ball_01", "fire_here", "carnage_trails", "flydirt" };

    // The per-player rig's non-crash defs (B4): the four `<part>_damage_effects` shims the
    // Devastator's 0.99 injure_anims entry names. Bound alongside the crash def because they need
    // exactly what the crash rig already has — the `player` anim root, the plane's own `pdpN`
    // panels as INPUT_NODEs, and a live puffer factory. Each is a one-event shim calling
    // `random_gun_impact`, which the closure pulls in with `yellow_sparks_follow` under it.
    private static readonly string[] PlaneDamageEffectAnims =
        { "nose_damage_effects", "tail_damage_effects", "leftwing_damage_effects", "rightwing_damage_effects" };

    // The gamez template roots those effects' meshes and puffers ride — staged under the
    // world-effects stage so a PlayEffectAt relocates one onto the hit/death point. This is the
    // FULL set of anchor roots the EffectAnimNames call closure needs (derived from the reader
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

    private readonly SessionSpec _spec;
    private readonly Node3D _worldRoot;
    private readonly Func<Vector3> _playerPosition;
    private AnimRuntime? _worldEffects;

    public WorldEffectsFactory(SessionSpec spec, Node3D worldRoot, Func<Vector3> playerPosition)
    {
        _spec = spec;
        _worldRoot = worldRoot;
        _playerPosition = playerPosition;
    }

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

    /// <summary>Builds the one world-effects runtime (D32) — the world-scoped generalization of the
    /// per-player crash runtime. It stages the impact/destruction effect templates under a
    /// dedicated subtree so their names resolve locally without colliding with the world or the crash
    /// roots, keeps a live <c>PufferFactory</c> over the session textures, and binds the closure of
    /// <see cref="EffectAnimNames"/>. <see cref="AnimRuntime.PlayEffectAt"/> then stages any of those
    /// effects at a hit or death point: <c>ProjectilePool.EffectSink</c> calls it on a rocket impact,
    /// and the world runtime's <see cref="AnimRuntime.ExternalEffect"/> routes a death's
    /// CALL_ANIMATION here. Puffers parent at world level (the crash lesson) so the stage does
    /// not suppress them.
    ///
    /// <para>The stage itself is visible and each template ROOT starts hidden
    /// (<see cref="AnimRuntime.ShowPlacedTemplates"/> reveals one for as long as an effect plays on
    /// it): a template's meshes are half the effect — the rocket's authored per-type rings, the
    /// fireball facades, the splash models — and hiding the whole stage rendered none of them
    /// (D31). Inside a revealed root the data still decides what shows: every ring is reset
    /// INACTIVE or opacity-OFF at bootstrap and its own def turns it on.</para></summary>
    public AnimRuntime BuildWorldEffectsRuntime(GameZ gamez, SceneBuilder worldScene,
        TextureArchive textures, AnimProgram worldProgram)
    {
        var stage = new Node3D { Name = "world_effects" };
        _worldRoot.AddChild(stage);
        int staged = BuildEffectStage(gamez, worldScene, stage, EffectStageRoots);
        foreach (var child in stage.GetChildren())
            if (child is Node3D root)
                root.Visible = false;
        // The impact/death SOUND an effect def carries is already played by the projectile pool
        // (D30) or the world runtime (D31); this runtime only renders the puffers. Several gun
        // effects gate their puffer behind RANDOM_WEIGHT, so this runtime's dice — its own stream
        // off the master seed — decide which effects render at all.
        // Puffer.Create pairs the depth fade with the blend it derives — off for MIX, whose dark
        // sprites emit at these ground-level sites and measured near-invisible with it on (the
        // damage-stage black smoke), on for additive fire, which leaks through the fade anyway.
        var effects = AnimRuntime.ForEffects(Rng.IntSeedFor(Rng.Effects), _worldRoot,
            st => Puffer.Create(st, textures, sustained: true),
            _spec.DebugAnim, EffectRuntimeTtl, _playerPosition);
        // Bind name resolution to the template stage — so the effect names resolve to these
        // templates and not to the world's or the crash roots' same-named nodes — but parent the
        // runtime node itself under the visible world root, a plain logic node that self-ticks.
        effects.ShowPlacedTemplates = true;
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
    public AnimRuntime? EnsureWorldEffects(GameZ gamez, SceneBuilder worldScene,
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
            worldRuntime.ExternalEffect = (name, pt, node) => effects.Handles(name) && effects.PlayEffectAt(name, pt, node);
            worldRuntime.ExternalEffectStop = name => effects.Stop(name);
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
        // ⚠ PufferParent is the WORLD root, NOT the crash root: a PUFFER_STATE emitter goes TopLevel
        // (world-space) the moment it emits, and parenting it under the per-player controller subtree
        // left it drawn-but-unrendered (every particle correctly positioned, IsVisibleInTree true, yet
        // nothing on screen — measured). Parenting at world level, exactly like the world runtime's
        // own PufferFactory, renders it. Each crash runtime still makes its own emitter instances at
        // its own crash site, so splitscreen crashes stay independent. The crash def's only SOUND
        // (snd_exp_ground_a) is already played by FlightAudio via Crash() -> OnGroundExplosion(); this
        // runtime has no audio session, so dispatching it here would only emit the "silent for the
        // session" warning — render effects, not sound. The seed drives wreckage scatter and the
        // crash def's RANDOM_WEIGHT verdicts: one draw per player off the advancing crash stream, so
        // splitscreen crashes differ from each other but repeat run to run.
        var crashRuntime = AnimRuntime.ForCrashRig(Rng.NewIntSeed(Rng.Crash), _worldRoot,
            st => Puffer.Create(st, textures, sustained: true), _spec.DebugAnim);
        // Bind only the named defs' transitive CALL_ANIMATION closures (Subset), never the whole
        // world program: the full 800+ defs include ~150 generic-named world defs that would
        // mis-anchor onto this plane's parts and run their reset states on the aircraft. The set is
        // the crash def plus the damage-effect shims — every def that plays ON this aircraft.
        var rigAnims = new List<string> { "player_crash_dirt" };
        rigAnims.AddRange(PlaneDamageEffectAnims);
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
}
