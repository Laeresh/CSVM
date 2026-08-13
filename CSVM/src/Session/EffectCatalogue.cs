using System;
using System.Collections.Generic;
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
    // The crash def vector's prefix: slot i is "player_crash_" + SurfaceRegistry.Names[i], the
    // vector the original concatenates in FUN_00476250 and indexes with the struck material's
    // surface id. Only default/dirt/water ship a def, so the other eleven slots fall back to slot 0
    // — asked of the bound program by SurfaceDefTable rather than listed here, because "this slot
    // names no def" IS the original's mechanism.
    public const string CrashDefPrefix = "player_crash_";

    // The bare anim name the selection cascade falls to when the vector cannot answer at all (empty
    // vector, or a slot 0 naming no def) — the crash defs' own anim root. Unreachable in this
    // install: all eight chapters' cam_anim carry player_crash_default, so slot 0 always resolves.
    public const string CrashAnimRoot = "player";

    // The AI aircraft family's vector prefix: slot i is "ai_crash_" + SurfaceRegistry.Names[i].
    // The original builds it in the AI vehicle-params constructor FUN_00478a00 (string 0x00627d40,
    // registry walk FUN_00559650/FUN_00559660, interned via FUN_00523820) into the params object at
    // +0x160/+0x164; the params parser FUN_00479240 sets the bare last resort at +0x158 to the
    // interned VEHICLE NAME (0x0047b110-0x0047b11b, intern of params+0x4); and the params→vehicle
    // bind FUN_00475820 copies both onto the live vehicle (+0x158→+0x6d0, vector→+0x6e0/+0x6e4) —
    // the very fields the ONE crash selector FUN_0048b920 indexes with the struck material's
    // surface id. The player setup FUN_00476250 then REPLACES that vector with player_crash_* (and
    // +0x6d0 with "player") only on the vehicle named "player", so every AI aircraft crashes
    // through this family and the cascade is shared, not duplicated.
    public const string AiCrashDefPrefix = "ai_crash_";

    // The ai_crash_* defs' authored NAME/anim-root: `kestrel`, the AI airframe they were written
    // against (the player family's counterpart is `player`). All 24 shipped defs (3 per chapter)
    // carry it. The AI crash rig stages a meshless scaffold of this name so the anchor closure
    // resolves on every airframe; on the actual Kestrel the name resolves to the aircraft model
    // itself, which is why it is also in CrashScaffoldAnchors (never placed like a template).
    public const string AiCrashScaffoldName = "kestrel";

    // The graze family's vector prefix: slot i is "touchdown_" + SurfaceRegistry.Names[i], built by
    // FUN_004735b0 exactly as the crash vector is (prepend the literal 0x006274ec to each registry
    // name, intern it via FUN_00523820, push THAT handle) and indexed by FUN_0048d2c0 with the same
    // cascade. Unlike the crash family it has NO bare last-resort anim: when the vector cannot
    // answer at all the handler skips its play call entirely, which is why TouchdownDefTable passes
    // a null lastResort. The vector is a GLOBAL built once at level init where the crash vector is
    // per-plane, mirrored here by building this one against the world program.
    public const string TouchdownDefPrefix = "touchdown_";

    // The impact/destruction effect ANIMATION names the world-effects runtime is bound to —
    // the closure of these is staged and playable via PlayEffectAt. IMPACT names come from
    // weapons.json (the non-model `default`/`buildings` effects of rockets/ordnance, plus the gun
    // `*_gunhit` family, which a gun hit plays throttled and time-bounded);
    // destruction roots are closed against all 2,360 install-wide destruction-slot calls
    // (analysis/death-effect-closure/): Subset handles 8/30 targets. The other 22 names are
    // fail-closed as LOCAL_CHOREOGRAPHY there: their definitions move/toggle live object subtrees
    // or wrap handled calls, so relocation would detach the work from the destroyed object.
    // `random_gun_impact` (root `player`) is excluded too — nothing reaches it, and its generic
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
        // The graze reaction's touchdown_* defs are NOT listed here. They are a surface-indexed
        // vector, so WorldEffectAnimNames appends whichever slots the bound program can play.
    };

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

    // The crash rigs' own anim-root scaffold NAMEs. The `player_crash_*` defs,
    // `player_destruction_reset`, `cpejectstop` and `random_gun_impact` are all authored
    // NAME=`player` — the crash root the rig builds — and a relocating CALL reaching any of them
    // (the dirt crash CALLs `cpejectstop` live) must never place that scaffold like a template:
    // TopLevel-pinning it at the first crash site takes the wreck and every pooled template copy
    // with it, so every later crash's destroyed plane and dirt burst replay at the FIRST crash's
    // position (`crash-rig-anchors`' crash→respawn→move→crash leg is the regression test).
    // `kestrel` is the ai_crash_* family's scaffold NAME (see AiCrashScaffoldName) — on the actual
    // Kestrel airframe it resolves to the aircraft model, the player_pfighter shape exactly.
    public static readonly string[] CrashScaffoldAnchors = { "player", AiCrashScaffoldName };

    /// <summary>The crash-def vector this program can play, built over the whole surface registry
    /// with <see cref="CrashDefPrefix"/> — what <c>BuildFlightCrashRuntime</c> binds and what
    /// <c>FlightController.Crash</c> indexes with the struck material's surface id. A slot whose
    /// def this install does not ship is empty and falls back to slot 0, which is the original's
    /// own mechanism rather than a list of exceptions (see <see cref="SurfaceDefTable"/>).</summary>
    public static SurfaceDefTable CrashDefTable(AnimProgram program) =>
        new(CrashDefPrefix, CrashAnimRoot, DefExistsIn(program));

    /// <summary>The AI aircraft counterpart of <see cref="CrashDefTable"/> — the
    /// <c>ai_crash_*</c> vector <c>FUN_00478a00</c> builds per AI vehicle-params object, selected
    /// by the SAME cascade (<c>FUN_0048b920</c>, via the <c>FUN_00475820</c> copy onto the
    /// vehicle). Same fallback arms as the player family, including the bare last resort — which
    /// the original sets to the interned VEHICLE NAME (<c>FUN_00479240</c> at <c>0x0047b11b</c>),
    /// so the caller passes the plane's own name. Unreachable in this install: all eight chapters
    /// ship <c>ai_crash_default</c>, so slot 0 always resolves.</summary>
    public static SurfaceDefTable AiCrashDefTable(AnimProgram program, string planeName) =>
        new(AiCrashDefPrefix, planeName, DefExistsIn(program));

    /// <summary>The family a controller's crash indexes: the original keys it on the vehicle —
    /// <c>FUN_00476250</c> swaps in <c>player_crash_*</c> only on the vehicle named
    /// <c>player</c>; every other vehicle keeps the <c>ai_crash_*</c> vector its params carried.
    /// Ours keys the same split on who is at the controls.</summary>
    public static SurfaceDefTable CrashDefTableFor(AnimProgram program, bool humanPiloted,
        string planeName) =>
        humanPiloted ? CrashDefTable(program) : AiCrashDefTable(program, planeName);

    /// <summary>Everything the per-player crash rig binds — every playable crash-vector slot (the
    /// struck surface is only known at impact, so the whole vector is bound), the four damage
    /// shims, the prop choreography and the authored damage-stage menu, i.e. every def that plays
    /// ON one aircraft — and therefore the name set whose anchor-root closure that rig's own
    /// template stage must satisfy (<see cref="CrashStageRoots"/>).</summary>
    public static IReadOnlyList<string> CrashRigAnimNames(SurfaceDefTable crashDefs)
    {
        var names = new List<string>(crashDefs.PlayableDefs);
        names.AddRange(PlaneDamageEffectAnims);
        names.AddRange(PropChoreographyAnims);
        names.AddRange(PlayerDamageStageAnims);
        return names;
    }

    /// <summary>The graze family's def vector — the original's <c>touchdown_</c> global
    /// (<c>FUN_004735b0</c> into <c>DAT_0071c2e8</c>), which <c>FlightController.GrazeReaction</c>
    /// indexes with the scraped material's surface id exactly as the crash does. Built against the
    /// WORLD program, because that vector is built once at level init rather than per plane.
    /// <b>No last resort:</b> where the crash cascade ends at a bare anim name, this one ends at
    /// "play nothing". <see cref="SurfaceDefTable"/>'s remarks carry the address.</summary>
    public static SurfaceDefTable TouchdownDefTable(AnimProgram program) =>
        new(TouchdownDefPrefix, lastResort: null, DefExistsIn(program));

    /// <summary>The surface id a struck body of each registry id actually <b>resolves</b> to on
    /// contact: itself where a touch cascade ships a def of its own for it, else slot 0, which is
    /// the empty-slot arm <see cref="SurfaceDefTable"/> implements (<c>FUN_0048b920</c>
    /// <c>0x0048bac5</c>–<c>0x0048bb00</c>). Index == surface id, length
    /// <see cref="SurfaceRegistry.Names"/>; an id outside that range is the cascade's own
    /// out-of-range arm and resolves slot 0 too, which is the caller's bounds test to make.
    ///
    /// <para>Both touch families are asked, because either one shipping a def for an id is enough
    /// to make that id behave as itself; this install ships <c>default</c>/<c>dirt</c>/<c>water</c>
    /// in each, so they agree. The weapon IMPACT table is deliberately NOT asked: an id it authors
    /// no row for plays nothing rather than falling back to row 0 (<c>FUN_005ad100</c> gates on the
    /// row's own variant count), so it has no resolved id to contribute.</para>
    ///
    /// <para>Written for the collider overlay's colour key (<c>BL-345</c>), which draws what a
    /// touch will select rather than the raw stamp.</para></summary>
    public static IReadOnlyList<int> ResolvedSurfaceIds(Func<string, bool> defExists)
    {
        var crash = new SurfaceDefTable(CrashDefPrefix, CrashAnimRoot, defExists);
        var touchdown = new SurfaceDefTable(TouchdownDefPrefix, lastResort: null, defExists);
        var resolved = new int[SurfaceRegistry.Names.Count];
        for (int id = 0; id < resolved.Length; id++)
        {
            // The cascade itself answers "is this slot occupied": the registry names are pairwise
            // distinct, so it returns prefix + this id's own name only when the slot is filled.
            string name = SurfaceRegistry.Names[id];
            bool ownDef = crash.DefForSurfaceId(id) == CrashDefPrefix + name
                          || touchdown.DefForSurfaceId(id) == TouchdownDefPrefix + name;
            resolved[id] = ownDef ? id : SurfaceRegistry.Default;
        }
        return resolved;
    }

    /// <inheritdoc cref="ResolvedSurfaceIds(Func{string, bool})"/>
    public static IReadOnlyList<int> ResolvedSurfaceIds(AnimProgram program) =>
        ResolvedSurfaceIds(DefExistsIn(program));

    /// <summary>Every effect animation the world-effects runtime binds: the fixed
    /// <see cref="EffectAnimNames"/> plus whichever <see cref="TouchdownDefTable"/> slots this
    /// program can actually play. One call serves the bind, the stage closure and the
    /// <c>--effects-test</c>/<c>effects-census</c> sweeps, so a graze def can never be playable but
    /// unstaged, or swept but unbound.</summary>
    public static IReadOnlyList<string> WorldEffectAnimNames(AnimProgram program)
    {
        var names = new List<string>(EffectAnimNames);
        names.AddRange(TouchdownDefTable(program).PlayableDefs);
        return names;
    }

    /// <summary>What the world-effects bind stages: the anchor-root closure of
    /// <see cref="WorldEffectAnimNames"/> against the bound world program. This IS the stage's
    /// source — <c>WorldEffectsFactory</c> builds a copy of every name it returns, per pool slot.</summary>
    public static IReadOnlyList<string> WorldStageRoots(AnimProgram program,
        Func<string, AnchorPlacement> resolveRoot) =>
        StageRootsFor(program, WorldEffectAnimNames(program), resolveRoot);

    /// <summary>The same for the per-plane crash rig: the closure of
    /// <see cref="CrashRigAnimNames"/> against that rig's own scope. The rig's
    /// <paramref name="resolveRoot"/> is scoped to the bound controller, so a name its
    /// plane/wreck already carries needs no template. <paramref name="crashDefs"/> is the rig's
    /// own family (player or AI); the no-table overload keeps the player family for callers that
    /// predate the split.</summary>
    public static IReadOnlyList<string> CrashStageRoots(AnimProgram program,
        Func<string, AnchorPlacement> resolveRoot, SurfaceDefTable crashDefs) =>
        StageRootsFor(program, CrashRigAnimNames(crashDefs), resolveRoot);

    /// <inheritdoc cref="CrashStageRoots(AnimProgram, Func{string, AnchorPlacement}, SurfaceDefTable)"/>
    public static IReadOnlyList<string> CrashStageRoots(AnimProgram program,
        Func<string, AnchorPlacement> resolveRoot) =>
        CrashStageRoots(program, resolveRoot, CrashDefTable(program));

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

    // "This program defines that anim" — the one existence test all three surface vectors are built
    // on, so a def the runtime could not play can never count as a filled slot in one of them and
    // not the others.
    private static Func<string, bool> DefExistsIn(AnimProgram program) =>
        name => program.ByAnimName(name).Count > 0;

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
