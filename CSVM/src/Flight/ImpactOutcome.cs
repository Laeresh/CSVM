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

    /// <summary>Decides the impact from the weapon's <c>IMPACT</c> table at the struck surface id;
    /// touches no scene, sink or sound archive (docs/org/weaponImpact.md). <c>default</c> already
    /// backfills ids the weapon names no block for (<see cref="WeaponDefs.InheritDefaultRow"/>), so
    /// do not re-add a fallback here — it would also fire on ids a weapon names and leaves empty,
    /// like <c>player</c>(6). <paramref name="modelResolved"/> and <paramref
    /// name="hasEffectsRuntime"/> are caller facts this cannot discover on its own.</summary>
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

    // The stand-in ladder, ordered: model beats all; a hardpoint weapon with no scene gets the
    // explosion; a gun on `buildings` gets ricochet sparks; everything else gets the single spark.
    // Ground has no arm of its own (backlog.md `BL-289`/`BL-313`) but must still return non-`None`
    // — `ProjectilePool` gates the `EffectSink` call on that, and the `blacksmokepuffer` rides it.
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
