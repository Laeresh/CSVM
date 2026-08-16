using CSVM.Mech3;

namespace CSVM.Flight;

/// <summary>Which hand-authored burst stands in for an impact when no authored asset renders it.
/// <c>None</c> means an authored gamez model was instanced and nothing stands in; <c>Spark</c> is
/// the single-sprite base case every surface falls back to.</summary>
public enum ImpactStandIn { None, Spark, Explosion, Ricochet }

/// <summary>What should happen when one weapon hits one surface — the effect name, the sound, the
/// stand-in and the damage numbers, as a value with no <c>Node3D</c> and no physics space behind
/// it. <see cref="Resolve"/> decides; a caller performs.</summary>
public readonly record struct ImpactOutcome
{
    /// <summary>The authored effect selected out of the weapon's <c>IMPACT</c> table for this
    /// surface id (its <c>ANIMATION</c>, else its <c>SURFACE_ANIMATION</c>), or null when the row
    /// binds nothing. It is both the gamez node a caller tries to instance and, when that misses,
    /// the name it hands to the world-effects runtime.</summary>
    public string? EffectName { get; init; }

    /// <summary>The struck surface's own <c>SOUND</c>, null when it authors no row. A
    /// <c>SOUND_GROUPS</c> name resolves through the group table at play time, not here.</summary>
    public string? Sound { get; init; }

    /// <summary>Which stand-in burst applies — <c>None</c> once an authored model has rendered.</summary>
    public ImpactStandIn StandIn { get; init; }

    /// <summary>The weapon's <c>HEALTH_DAMAGE</c>, 0 when it carries none.</summary>
    public float Damage { get; init; }

    /// <summary>The weapon's <c>IMPACT_PROXIMITY</c> — the blast/effect radius in m, 0 when it
    /// carries none. A radius on a zero-damage weapon is an effect radius only.</summary>
    public float BlastRadius { get; init; }

    /// <summary>Whether the authored radius is also a positive-health damage blast, i.e. whether a
    /// caller owes the surrounding-bodies query rather than just damaging what it struck.</summary>
    public bool HasBlastDamage => Damage > 0f && BlastRadius > 0f;

    /// <summary>Decide the impact. Pure: it reads the weapon's <c>IMPACT</c> table at the struck
    /// material's numeric surface id (<see cref="SceneBuilder.SurfaceIdMeta"/>, the registry index
    /// <c>FUN_005acf60</c> hands <c>FUN_005ad100</c>) and picks, touching no scene, no sink and no
    /// sound archive. The caller supplies the id, including the original's null-material-to-0 arm.
    ///
    /// <para><paramref name="modelResolved"/> is an input rather than something this discovers,
    /// because whether <see cref="EffectName"/> names a real gamez node is a question only a caller
    /// holding the chapter's scene can answer — and when it does, the authored model IS the effect
    /// and no stand-in applies. <paramref name="hasEffectsRuntime"/> is the same kind of fact about
    /// the caller: a scene-less pool (the weapon lab) has nowhere to build a hardpoint weapon's real
    /// fireball, so the explosion stand-in carries the blast there and only there.</para>
    ///
    /// <para><b>Whether an unauthored id falls back to <c>default</c> is settled in
    /// <c>WeaponDefs.InheritDefaultRow</c>, not here</b> — it does, and the table arrives already
    /// filled, so this is an index and nothing more. Do not re-add a fallback arm at the lookup:
    /// two of them would resurrect <c>default</c>'s effect on the ids a weapon names and
    /// deliberately binds nothing on (the slug's <c>player</c>(6)).</para>
    ///
    /// <para><c>player</c>(6) is a struck <c>AircraftBody</c>; <c>enemy</c>(7) stays unreachable
    /// until something non-player flies. Neither gets a case of its own here: both read out of the
    /// table like any other id and fall to <see cref="ImpactStandIn.Spark"/>, which is the
    /// existing else-branch and not a behaviour invented for them — the struck plane's damage is
    /// the caller's business, not this record's.</para></summary>
    public static ImpactOutcome Resolve(WeaponDef weapon, int surfaceId, bool modelResolved,
        bool hasEffectsRuntime)
    {
        // The struck id's IMPACT row — already carrying `default`'s binding if the weapon named no
        // block for this id (WeaponDefs.InheritDefaultRow).
        var effect = weapon.ImpactFor(surfaceId);

        return new ImpactOutcome
        {
            EffectName = effect != null ? effect.Animation ?? effect.SurfaceAnimation : null,
            Sound = effect?.Sound,
            StandIn = StandInFor(weapon, surfaceId, modelResolved, hasEffectsRuntime),
            Damage = weapon.HealthDamage ?? 0f,
            BlastRadius = weapon.ImpactProximity ?? 0f,
        };
    }

    // The stand-in ladder, in its order: an instanced model beats everything; a hardpoint
    // weapon with nowhere to build its fireball gets the explosion; a gun off a building gets
    // ricochet sparks; everything else, ground included, gets the single spark.
    //
    // The ladder is ours, not the original's — it stands in for authored assets that do not
    // render here.
    //
    // Ground has no arm of its own (BL-313, 2026-08-15). It used to take a tumbling
    // chip burst on the `bit01`–`bit04` textures, inferred from the gunhit def's
    // zero-vertex `bit1`–`bit3` nodes. The original draws no such chips: those nodes each
    // carry one light record and render a single 1-pixel near-black point (`FUN_005524d0` /
    // `FUN_00554550` gate the light block on the light count alone), invisible against ground
    // at gameplay zoom. Ground must still return a non-`None` stand-in, because
    // `ProjectilePool` gates the world-effects `EffectSink` call on
    // `StandIn != None` — a `None` here would take the `blacksmokepuffer` with
    // it.
    private static ImpactStandIn StandInFor(WeaponDef weapon, int surfaceId, bool modelResolved,
        bool hasEffectsRuntime)
    {
        if (modelResolved)
            return ImpactStandIn.None;
        if (!weapon.IsGun && !hasEffectsRuntime)
            return ImpactStandIn.Explosion;
        if (surfaceId == SurfaceRegistry.Buildings && weapon.IsGun)
            return ImpactStandIn.Ricochet;
        return ImpactStandIn.Spark;
    }
}
