namespace CSVM.Flight;

/// <summary>Which hand-authored burst stands in for an impact when no authored asset renders it.
/// <c>None</c> means an authored gamez model was instanced and nothing stands in; <c>Spark</c> is
/// the single-sprite base case every surface falls back to.</summary>
public enum ImpactStandIn { None, Spark, Explosion, DirtDebris, Ricochet }

/// <summary>What should happen when one weapon hits one surface — the effect name, the sound, the
/// stand-in and the damage numbers, as a value with no <c>Node3D</c> and no physics space behind
/// it. <see cref="Resolve"/> decides; a caller performs.</summary>
public readonly record struct ImpactOutcome
{
    /// <summary>The authored effect selected out of the weapon's <c>IMPACT</c> table for this
    /// surface (its <c>ANIMATION</c>, else its <c>SURFACE_ANIMATION</c>), or null when the weapon
    /// binds neither for this surface nor for <c>default</c>. It is both the gamez node a caller
    /// tries to instance and, when that misses, the name it hands to the world-effects runtime.</summary>
    public string? EffectName { get; init; }

    /// <summary>The struck surface's <c>SOUND</c>, else the weapon's <c>default</c> one. A
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

    /// <summary>Decide the impact. Pure: it reads the weapon's <c>IMPACT</c> table and the surface
    /// and picks, touching no scene, no sink and no sound archive.
    ///
    /// <para><paramref name="modelResolved"/> is an input rather than something this discovers,
    /// because whether <see cref="EffectName"/> names a real gamez node is a question only a caller
    /// holding the chapter's scene can answer — and when it does, the authored model IS the effect
    /// and no stand-in applies. <paramref name="hasEffectsRuntime"/> is the same kind of fact about
    /// the caller: a scene-less pool (the weapon lab) has nowhere to build a hardpoint weapon's real
    /// fireball, so the explosion stand-in carries the blast there and only there.</para>
    ///
    /// <para><c>Player</c> is a struck <c>AircraftBody</c>; <c>Enemy</c> stays unreachable until
    /// something non-player flies. Neither gets a case of its own here: both read out of the
    /// table like any other class and fall to <see cref="ImpactStandIn.Spark"/>, which is the
    /// existing else-branch and not a behaviour invented for them — the struck plane's damage is
    /// the caller's business, not this record's.</para></summary>
    public static ImpactOutcome Resolve(WeaponDef weapon, SurfaceClass surface, bool modelResolved,
        bool hasEffectsRuntime)
    {
        // The struck surface's IMPACT entry, else the weapon's `default`.
        if (!weapon.Impact.TryGetValue(surface, out var effect))
            weapon.Impact.TryGetValue(SurfaceClass.Default, out effect);

        return new ImpactOutcome
        {
            EffectName = effect != null ? effect.Animation ?? effect.SurfaceAnimation : null,
            Sound = effect?.Sound,
            StandIn = StandInFor(weapon, surface, modelResolved, hasEffectsRuntime),
            Damage = weapon.HealthDamage ?? 0f,
            BlastRadius = weapon.ImpactProximity ?? 0f,
        };
    }

    /// <summary>The stand-in ladder, in its load-bearing order: an instanced model beats everything;
    /// a hardpoint weapon with nowhere to build its fireball gets the explosion; dirt (unclassified
    /// terrain) gets tumbling debris; a gun off a building gets ricochet sparks; everything else
    /// gets the single spark.</summary>
    private static ImpactStandIn StandInFor(WeaponDef weapon, SurfaceClass surface, bool modelResolved,
        bool hasEffectsRuntime)
    {
        if (modelResolved)
            return ImpactStandIn.None;
        if (!weapon.IsGun && !hasEffectsRuntime)
            return ImpactStandIn.Explosion;
        if (surface == SurfaceClass.Default)
            return ImpactStandIn.DirtDebris;
        if (surface == SurfaceClass.Buildings && weapon.IsGun)
            return ImpactStandIn.Ricochet;
        return ImpactStandIn.Spark;
    }
}
