using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;

namespace CSVM.Session;

/// <summary>Where one definition's anchor lives, as the bind that stages it can see it. The rule is
/// uniform across the world-effects and the crash bind; only the lookup differs, which is why
/// <see cref="EffectCatalogue.StageRootsFor"/> takes it as a delegate.</summary>
public enum AnchorPlacement
{
    /// <summary>A parentless gamez template root: the bind must build a copy of it. A root left out
    /// leaves every def anchored on it unanchored, so it plays nothing at all.</summary>
    Stage,

    /// <summary>Already inside the bind's own scope — a child of a root it stages anyway
    /// (<c>ap_cracks</c> under <c>ap_effect</c>), the crash scaffold's <c>player</c>/<c>healthy</c>/
    /// <c>destroyed</c>/pieces, the plane's own parts. Satisfied; nothing to build.</summary>
    InScope,

    /// <summary>Resolves nowhere. The def would anchor on nothing and play silently — the failure
    /// this derivation exists to make loud.</summary>
    Missing,
}

/// <summary>The record of which authored anims are playable effects, and what their defs need
/// staged. Owns the name tables every effect producer must stay inside — <see cref="WorldEffectsFactory"/>
/// consumes these names to build and stage the runtime; the naming lives here, never there.</summary>
public static class EffectCatalogue
{
    // The impact/destruction effect ANIMATION names the world-effects runtime is bound to —
    // the closure of these is staged and playable via PlayEffectAt. IMPACT names come from
    // weapons.json (the non-model `default`/`buildings` effects of rockets/ordnance, plus the gun
    // `*_gunhit` family, which a gun hit plays throttled and time-bounded — C8);
    // destruction roots are closed against all 2,360 install-wide destruction-slot calls
    // (analysis/death-effect-closure/): Subset handles 8/30 targets. The other 22 names are
    // fail-closed as LOCAL_CHOREOGRAPHY there: their definitions move/toggle live object subtrees
    // or wrap handled calls, so relocation would detach the work from the destroyed object.
    // `random_gun_impact` (root `player`) is excluded too — unreachable in M3, and its generic
    // root would mis-anchor. Every handled name resolves in all 8 chapters.
    public static readonly string[] EffectAnimNames =
    {
        // rocket / ordnance IMPACT (default + buildings), puffer-bearing and otherwise
        "large_fireball", "small_fireball", "he_ground_effect", "ap_ground_effect", "flak_effect",
        "flash_effect", "sonic_ground_effect", "scatter_effect", "torpedo_ground_effect",
        "rear_flash_effect", "torpedo_water_effect",
        // gun IMPACT family — caliber (3040/5060/70) × ammo (slug/dum/ap/mag); see EffectSink
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

    // The crash rig's two variants (BuildFlightCrashRuntime) — the struck surface is only known at
    // the moment of impact (FlightController.ClassifySurface picks), so both closures are bound and
    // the crash chooses between them at play time.
    public static readonly string[] CrashDefNames = { "player_crash_dirt", "player_crash_water" };

    // The belly-slide ground splash (`flydirt_plane`, called AT_NODE `healthy` by
    // `player_crash_dirt`): its own `ObjectMotion` translation is authored as a near-zero-horizontal
    // sink (a planted decal fading into the ground), not a launch — but the data shape is
    // indistinguishable from a wreck piece's own translation (also vertical-only, relying entirely on
    // `AnimRuntime.InheritedWorldVelocity` for horizontal spread), so only the name tells them apart.
    // Wired into `InheritedVelocityExempt` so the crash's momentum nudge, which correctly scatters
    // `piece1..4`, does not also drag this splash off with the sliding wreck.
    public static readonly string[] GroundSplashAnimNames = { "flydirt_plane" };

    // The crash def's own sub-effects that are meant to lie flat on the struck surface, not co-rotate
    // with the plane's impact attitude — the ONLY crash-rig templates `AnimRuntime
    // .LevelPlacedTemplateNames` levels to world axes. `plane_big_splash` (the water splash's
    // spray column + flat rings, `huge_splash_model`) and its two own CALL_ANIMATION children
    // `plane_big_ripple` (the fading ripple rings, `ripple`) and `hg_splasher` (the water-squirt
    // puffer, no owned mesh — leveling only turns its local_velocity upright); `flydirt_plane` (the
    // dirt burst's ground-scorch dust plane, `flydirt`) is the direct ground analogue. Deliberately
    // excludes the fireball/smoke/debris family (`large_fireball`, `large_10sec_fire`,
    // `large_black_smokeball`, `call_crash_trails`) and `large_steam_spray` — see
    // `AnimRuntime.LevelPlacedTemplateNames`'s own ⚠ for why those must keep inheriting the crash
    // attitude.
    public static readonly string[] CrashSurfaceLevelAnimNames =
        { "plane_big_splash", "plane_big_ripple", "hg_splasher", "flydirt_plane" };

    // The per-player rig's non-crash defs: the four `<part>_damage_effects` shims the
    // Devastator's 0.99 injure_anims entry names. Bound alongside the crash def because they need
    // exactly what the crash rig already has — the `player` anim root, the plane's own `pdpN`
    // panels as INPUT_NODEs, and a live puffer factory. Each is a one-event shim calling
    // `random_gun_impact`, which the closure pulls in with `yellow_sparks_follow` under it.
    public static readonly string[] PlaneDamageEffectAnims =
        { "nose_damage_effects", "tail_damage_effects", "leftwing_damage_effects", "rightwing_damage_effects" };

    // The engine start/stop choreography (plane_props.zrd.json): the static blade prop cross-fades
    // to its spinning blur disc (with the startup smokepuffN burst) and the reverse on shutdown.
    // Bound alongside the crash def for the same live puffer factory; unlike the crash/damage defs
    // above, FlightController plays these directly (spawn/engine-death), never through a CALL.
    public static readonly string[] PropChoreographyAnims = { "startprops", "stopprops" };

    // The authored player damage-stage menu (player-1.zrd.json): the per-panel burn
    // (torn pdpN shown + gimmeflakes debris + the staged short_firetrail / loop_short_firetrail
    // burn-down), the partial-damage fuel vapor leak, and the heavy prop1 trail with the fire_lt
    // nose light. DamageVisuals plays these through the rig runtime as the vehicle.zrd.json
    // injure_anims thresholds cross — the tier table is authored, nothing here invents one.
    // `player_damage_trail` (short_firetrail at prop1) is played for the data's 0.10
    // `player_smoketrail` entry — see DamageVisuals.RigAnimFor for that one deliberate mapping.
    public static readonly string[] PlayerDamageStageAnims =
    {
        "pdpanel1", "pdpanel2", "pdpanel3", "pdpanel4", "pdpanel5", "pdpanel6", "pdpanel7",
        "pdpanel8", "player_fuelleak", "player_damage_trail",
    };

    // Everything the per-player crash rig binds — both crash variants, the four damage shims,
    // the prop choreography and the authored damage-stage menu, i.e. every def that plays ON one
    // aircraft — and therefore the name set whose anchor-root closure that rig's own template
    // stage must satisfy (<see cref="CrashStageRoots"/>).
    public static readonly string[] CrashRigAnimNames =
        Concat(Concat(Concat(CrashDefNames, PlaneDamageEffectAnims), PropChoreographyAnims),
            PlayerDamageStageAnims);

    // Anchors the mechanical closure below reports that no bind stages, because the CALL that
    // reaches the definition supplies its anchor instead of its own NAME. Curation, not derivation:
    // the closure walks NAMEs and cannot see either of these, so they are dropped before the
    // resolver is asked. This list is where a future "the derivation asks for a root the game
    // supplies another way" goes — never the mechanical walk itself.
    //
    // `zep_can_dstry1.flt` is `dblcannon_flying_parts`' NAME, and every call reaching it carries an
    // AT_NODE — `zep_ng_dstry1.flt` (from `biggun_flying_parts`), `zep_main_dstry1.flt`,
    // `doublecannon*` — each a zeppelin wreck whose own subtree carries the `part1`..`part8` the def
    // flings, and a seeded `--effects-test --debug-anim` reports that retarget resolving with no
    // UNRESOLVED tag. C2's gamez has no node of the name at all, so treating it as a needed root
    // would fail that one chapter for an effect that has always played.
    //
    // `warhawk` is `startprops`/`stopprops`' own NAME (plane_props.zrd.json) — a shared authoring
    // label, not a per-plane node, so it never resolves on any of the 11 airframes. That is fine:
    // FlightController's own `Play` calls always supply the plane model as the fallback anchor
    // directly (not a CALL_ANIMATION), so nothing needs `warhawk` staged anywhere.
    public static readonly string[] CallSuppliedAnchors = { "zep_can_dstry1.flt", "warhawk" };

    // The crash defs' authored airframe anchor. `plane_reset` (and `pdpanel5`) are written against
    // the Devastator's own model root, so on that airframe the rig's scope has it and on the other
    // ten it has nothing of the name and the def is simply inert. Not a staging gap: no chapter's
    // gamez carries a `player_pfighter` node at all (0 occurrences in all 8), so there is no
    // template to stage either way.
    //
    // ⚠ Also the crash stage's PLACE-EXEMPT set (WorldEffectsFactory.NewCrashTemplateStage): on
    // the Devastator these names resolve to the AIRCRAFT, and a relocating CALL treating that as
    // an effect template TopLevel-pins the whole plane at the call site — the model stays at
    // spawn while the FlightController flies away with only the crash rig's debris. The defs'
    // node ops still resolve and run on the model; only placement is refused
    // (`crash-rig-anchors` suite, TemplateStageTests.PlaceAtNeverMovesAPlaceExemptCallee).
    public static readonly string[] AirframeScopedAnchors = { "player_pfighter" };

    // The crash rig's own anim-root scaffold NAME. `player_crash_dirt`/`_water`,
    // `player_destruction_reset`, `cpejectstop` and `random_gun_impact` are all authored
    // NAME=`player` — the crash root the rig builds — and a relocating CALL reaching any of them
    // (the dirt crash CALLs `cpejectstop` live) must never place that scaffold like a template:
    // TopLevel-pinning it at the first crash site takes the wreck and every pooled template copy
    // with it, so every later crash's destroyed plane and dirt burst replay at the FIRST crash's
    // position (`crash-rig-anchors`' crash→respawn→move→crash leg is the regression test).
    public static readonly string[] CrashScaffoldAnchors = { "player" };

    /// <summary>The graze reaction's per-surface touchdown def (<c>FlightController.GrazeReaction</c>):
    /// sparks off a hard building surface, dust off unclassified terrain, a splash off water — the
    /// same three names in <see cref="EffectAnimNames"/>' graze-reaction entries above. Pure, so the
    /// switch is unit-testable without a scene.</summary>
    public static string TouchdownFor(SurfaceClass surface) => surface switch
    {
        SurfaceClass.Water => "touchdown_water",
        SurfaceClass.Buildings => "touchdown_default",
        _ => "touchdown_dirt",
    };

    /// <summary>What the world-effects bind stages: the anchor-root closure of
    /// <see cref="EffectAnimNames"/> against the bound world program. This IS the stage's source —
    /// <c>WorldEffectsFactory</c> builds a copy of every name it returns, per pool slot.</summary>
    public static IReadOnlyList<string> WorldStageRoots(AnimProgram program,
        Func<string, AnchorPlacement> resolveRoot) =>
        StageRootsFor(program, EffectAnimNames, resolveRoot);

    /// <summary>The same for the per-player crash rig: the closure of
    /// <see cref="CrashRigAnimNames"/> against that rig's own scope. The rig's
    /// <paramref name="resolveRoot"/> is scoped to the bound controller, so a name its
    /// plane/wreck already carries needs no template.</summary>
    public static IReadOnlyList<string> CrashStageRoots(AnimProgram program,
        Func<string, AnchorPlacement> resolveRoot) =>
        StageRootsFor(program, CrashRigAnimNames, resolveRoot);

    /// <summary>The anchor-root closure of <paramref name="names"/> against a bound program — what a
    /// bind must stage for every definition those names can reach to have something to anchor on.
    /// The mechanical half of <c>analysis/effect-anchor-roots/</c>, run at build instead of offline:
    /// walk the transitive CALL_ANIMATION closure (<see cref="AnimProgram.Subset(IEnumerable{string})"/>),
    /// take each reached definition's NAME — the gamez node its instance anchors on — and ask
    /// <paramref name="resolveRoot"/> where that node lives.
    ///
    /// <para><paramref name="resolveRoot"/> is the caller's lookup, so the world-effects bind and the
    /// per-player crash bind share this one function with different scopes. Anchors listed in
    /// <see cref="CallSuppliedAnchors"/>/<see cref="AirframeScopedAnchors"/> are dropped before it is
    /// asked — see their own remarks for why the closure over-reports them.</para>
    ///
    /// <para>Sorted, so no caller's staging order can depend on definition load order.</para></summary>
    /// <exception cref="EffectAnchorException">an anchor resolves nowhere: the definition would
    /// anchor on nothing and play nothing at all, silently.</exception>
    public static IReadOnlyList<string> StageRootsFor(AnimProgram program, IEnumerable<string> names,
        Func<string, AnchorPlacement> resolveRoot)
    {
        // anchor → the animation names anchored on it, so a failure can name the definition and not
        // just the missing node.
        var anchors = new SortedDictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in program.Subset(names).Defs)
        {
            if (string.IsNullOrEmpty(def.Name) || SuppliedElsewhere(def.Name))
                continue;
            if (!anchors.TryGetValue(def.Name, out var animNames))
                anchors[def.Name] = animNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            animNames.Add(def.AnimName ?? def.Name);
        }

        var staged = new List<string>();
        var missing = new List<string>();
        foreach (var (anchor, animNames) in anchors)
        {
            switch (resolveRoot(anchor))
            {
                case AnchorPlacement.Stage:
                    staged.Add(anchor);
                    break;
                case AnchorPlacement.InScope:
                    break;
                default:
                    missing.Add($"'{anchor}' (anchors {string.Join("/", animNames)})");
                    break;
            }
        }
        if (missing.Count > 0)
            throw new EffectAnchorException(missing);
        return staged;
    }

    private static string[] Concat(string[] first, string[] second)
    {
        var all = new string[first.Length + second.Length];
        Array.Copy(first, all, first.Length);
        Array.Copy(second, 0, all, first.Length, second.Length);
        return all;
    }

    private static bool SuppliedElsewhere(string anchor)
    {
        foreach (var name in CallSuppliedAnchors)
            if (string.Equals(name, anchor, StringComparison.OrdinalIgnoreCase))
                return true;
        foreach (var name in AirframeScopedAnchors)
            if (string.Equals(name, anchor, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}

/// <summary>An effect definition's anchor resolves nowhere in the bind asked to stage it. Carries
/// the anchor names with the animations that wanted them, because "which def" is the half a bare
/// node name cannot answer.</summary>
public sealed class EffectAnchorException : Exception
{
    public EffectAnchorException(IReadOnlyList<string> anchors)
        : base($"effect anchor(s) resolve nowhere: {string.Join("; ", anchors)}")
    {
        Anchors = anchors;
    }

    public EffectAnchorException()
        : this(Array.Empty<string>())
    {
    }

    public EffectAnchorException(string message)
        : base(message)
    {
        Anchors = Array.Empty<string>();
    }

    public EffectAnchorException(string message, Exception inner)
        : base(message, inner)
    {
        Anchors = Array.Empty<string>();
    }

    /// <summary>One entry per unresolvable anchor, each naming the animations anchored on it.</summary>
    public IReadOnlyList<string> Anchors { get; }
}
