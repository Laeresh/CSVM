using CSVM.Flight;

namespace CSVM.Session;

/// <summary>The record of which authored anims are playable effects, and what their defs need
/// staged. Owns the name tables every effect producer must stay inside — <see cref="WorldEffectsFactory"/>
/// consumes these names to build and stage the runtime; it no longer owns the naming itself.</summary>
public static class EffectCatalogue
{
    // The impact/destruction effect ANIMATION names the world-effects runtime (D32) is bound to —
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

    // The per-player rig's non-crash defs (B4): the four `<part>_damage_effects` shims the
    // Devastator's 0.99 injure_anims entry names. Bound alongside the crash def because they need
    // exactly what the crash rig already has — the `player` anim root, the plane's own `pdpN`
    // panels as INPUT_NODEs, and a live puffer factory. Each is a one-event shim calling
    // `random_gun_impact`, which the closure pulls in with `yellow_sparks_follow` under it.
    public static readonly string[] PlaneDamageEffectAnims =
        { "nose_damage_effects", "tail_damage_effects", "leftwing_damage_effects", "rightwing_damage_effects" };

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
}
