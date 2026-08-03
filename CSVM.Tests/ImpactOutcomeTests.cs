using System.Collections.Generic;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="ImpactOutcome.Resolve"/> — the decision "what should happen when this weapon hits
/// this surface", taken apart from performing it. Nothing here builds a scene: the rule cases run
/// on hand-built <see cref="WeaponDef"/>s, and the data cases read the shipped
/// <c>weapons.zrd.json</c> so the table lookup is checked against the real <c>IMPACT</c> shapes
/// (a class that is present but all-null, a class the reader skips, a class that binds only
/// <c>SURFACE_ANIMATION</c>) rather than against fixtures that agree with the reader by
/// construction.
/// </summary>
public class ImpactOutcomeTests
{
    /// <summary>The surfaces a round can actually strike in M3. Not the whole
    /// <see cref="SurfaceClass"/> enum: <c>Player</c>/<c>Enemy</c> are the already-documented
    /// unreachable pair (no aircraft physics body), and <c>Quicksand</c> turns out to be a third —
    /// <c>ProjectilePool.ClassifySurface</c> (<c>Projectile.cs:380</c>) and the collider tagger
    /// behind it, <c>SceneBuilder.ClassifySurface(string?)</c> (<c>SceneBuilder.cs:389</c>), only
    /// ever stamp <c>"water"</c> or <c>"buildings"</c> and default everything else — including
    /// quicksand terrain — to <c>Default</c>. A weapon's <c>quicksand</c> IMPACT entry (e.g. <c>wep_04</c>'s) is real data the
    /// reader must still parse correctly, but no code path ever asks <see cref="ImpactOutcome.Resolve"/>
    /// for it — a suite case for it would be invented coverage, the exact trap B3 named for
    /// <c>Player</c>/<c>Enemy</c>.</summary>
    private static readonly SurfaceClass[] ReachableSurfaces =
    {
        SurfaceClass.Default, SurfaceClass.Water, SurfaceClass.Buildings,
    };

    private static string SharedZrdr =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    // ---- the stand-in ladder ------------------------------------------------------------------

    /// <summary>An instanced authored model beats every stand-in: when the effect name resolved to
    /// a real gamez node, the model IS the effect and nothing else draws.</summary>
    [Theory]
    [InlineData(SurfaceClass.Default)]
    [InlineData(SurfaceClass.Water)]
    [InlineData(SurfaceClass.Buildings)]
    public void AResolvedModelLeavesNoStandIn(SurfaceClass surface)
    {
        var outcome = ImpactOutcome.Resolve(Gun(), surface, modelResolved: true, hasEffectsRuntime: true);

        Assert.Equal(ImpactStandIn.None, outcome.StandIn);
    }

    /// <summary>Dirt — the unclassified terrain every chapter is mostly made of — gets the tumbling
    /// debris burst, gun or rocket alike.</summary>
    [Fact]
    public void ARoundOnDirtPicksTheDebrisBurst()
    {
        Assert.Equal(ImpactStandIn.DirtDebris,
            ImpactOutcome.Resolve(Gun(), SurfaceClass.Default, false, hasEffectsRuntime: true).StandIn);
        Assert.Equal(ImpactStandIn.DirtDebris,
            ImpactOutcome.Resolve(Rocket(), SurfaceClass.Default, false, hasEffectsRuntime: true).StandIn);
    }

    /// <summary>A gun round off a building ricochets; a rocket on the same wall does not — the
    /// ricochet stands in for a gun-specific authored asset that is missing from the install.</summary>
    [Fact]
    public void OnlyAGunRicochetsOffABuilding()
    {
        Assert.Equal(ImpactStandIn.Ricochet,
            ImpactOutcome.Resolve(Gun(), SurfaceClass.Buildings, false, hasEffectsRuntime: true).StandIn);
        Assert.Equal(ImpactStandIn.Spark,
            ImpactOutcome.Resolve(Rocket(), SurfaceClass.Buildings, false, hasEffectsRuntime: true).StandIn);
    }

    /// <summary>Water and the surfaces M3 cannot reach fall to the single spark. This is the
    /// existing else-branch, not a case authored for <c>Player</c>/<c>Enemy</c>.</summary>
    [Theory]
    [InlineData(SurfaceClass.Water)]
    [InlineData(SurfaceClass.Player)]
    [InlineData(SurfaceClass.Enemy)]
    [InlineData(SurfaceClass.Quicksand)]
    public void EverySurfaceWithNoLookOfItsOwnFallsToTheSpark(SurfaceClass surface)
    {
        Assert.Equal(ImpactStandIn.Spark,
            ImpactOutcome.Resolve(Gun(), surface, false, hasEffectsRuntime: true).StandIn);
    }

    /// <summary>With no world-effects runtime to build its real fireball, a hardpoint weapon shows
    /// the explosion stand-in — and it outranks the surface's own look, so the blast is visible in a
    /// scene-less pool whatever it hit. A gun never takes that branch.</summary>
    [Theory]
    [InlineData(SurfaceClass.Default)]
    [InlineData(SurfaceClass.Water)]
    [InlineData(SurfaceClass.Buildings)]
    public void AHardpointWeaponWithNoEffectsRuntimeExplodesInstead(SurfaceClass surface)
    {
        Assert.Equal(ImpactStandIn.Explosion,
            ImpactOutcome.Resolve(Rocket(), surface, false, hasEffectsRuntime: false).StandIn);
        Assert.NotEqual(ImpactStandIn.Explosion,
            ImpactOutcome.Resolve(Gun(), surface, false, hasEffectsRuntime: false).StandIn);
    }

    // ---- the damage numbers ------------------------------------------------------------------

    /// <summary>An authored radius is a damage blast only when the weapon also carries positive
    /// health damage; the flash/flare specials carry a large radius and no damage at all.</summary>
    [Fact]
    public void ARadiusIsABlastOnlyAlongsidePositiveDamage()
    {
        var rocket = new WeaponDef { Id = "r", IsRocket = true, HealthDamage = 60f, ImpactProximity = 15f };
        var flash = new WeaponDef { Id = "f", IsRocket = true, ImpactProximity = 450f };
        var contact = new WeaponDef { Id = "c", IsRocket = true, HealthDamage = 60f };

        Assert.True(ImpactOutcome.Resolve(rocket, SurfaceClass.Default, false, true).HasBlastDamage);
        Assert.False(ImpactOutcome.Resolve(flash, SurfaceClass.Default, false, true).HasBlastDamage);
        Assert.False(ImpactOutcome.Resolve(contact, SurfaceClass.Default, false, true).HasBlastDamage);
        Assert.Equal(450f, ImpactOutcome.Resolve(flash, SurfaceClass.Default, false, true).BlastRadius);
        Assert.Equal(0f, ImpactOutcome.Resolve(contact, SurfaceClass.Default, false, true).BlastRadius);
    }

    /// <summary>A weapon with no damage keys at all resolves to zeroes rather than to nulls a
    /// caller has to re-default.</summary>
    [Fact]
    public void AWeaponWithNoDamageKeysResolvesToZeroes()
    {
        var outcome = ImpactOutcome.Resolve(Gun(), SurfaceClass.Default, false, true);

        Assert.Equal(0f, outcome.Damage);
        Assert.Equal(0f, outcome.BlastRadius);
        Assert.False(outcome.HasBlastDamage);
    }

    // ---- the IMPACT table lookup, against the shipped data ------------------------------------

    /// <summary>The 30 cal slug's own table: <c>default</c> and <c>water</c> each bind a name and a
    /// sound, and <c>buildings</c> binds a name with no <c>SOUND</c> — a class entry is taken whole,
    /// so the building hit is silent rather than borrowing the default's sound.</summary>
    [ExtractedDataFact]
    public void TheSlugsTableIsReadPerClassAndNotMerged()
    {
        var slug = WeaponDefs.Load(SharedZrdr).Get("wep_00")!;

        var dirt = ImpactOutcome.Resolve(slug, SurfaceClass.Default, false, true);
        Assert.Equal("3040slug_gunhit", dirt.EffectName);
        Assert.Equal("snd_grnd_bullet", dirt.Sound);

        var water = ImpactOutcome.Resolve(slug, SurfaceClass.Water, false, true);
        Assert.Equal("splash1.flt", water.EffectName);
        Assert.Equal("snd_water_bullet", water.Sound);

        var wall = ImpactOutcome.Resolve(slug, SurfaceClass.Buildings, false, true);
        Assert.Equal("bld_damage.flt", wall.EffectName);
        Assert.Null(wall.Sound);
    }

    /// <summary>The two classes M3 cannot reach are read exactly as authored, with nothing supplied
    /// for them. The slug binds neither — its <c>enemy</c> value is the data's "no effect on that
    /// surface" null and its <c>player</c> entry has every slot null, both of which the reader drops
    /// — so both take the same <c>default</c> fallback every unbound class takes. The AA flak rocket
    /// does bind <c>player</c>, and that entry is read rather than the default.</summary>
    [ExtractedDataFact]
    public void TheUnreachableClassesAreReadOffTheTableLikeAnyOther()
    {
        var weapons = WeaponDefs.Load(SharedZrdr);
        var slug = weapons.Get("wep_00")!;

        Assert.False(slug.Impact.ContainsKey(SurfaceClass.Player));
        Assert.False(slug.Impact.ContainsKey(SurfaceClass.Enemy));
        Assert.Equal("3040slug_gunhit", ImpactOutcome.Resolve(slug, SurfaceClass.Player, false, true).EffectName);
        Assert.Equal("3040slug_gunhit", ImpactOutcome.Resolve(slug, SurfaceClass.Enemy, false, true).EffectName);

        var flak = weapons.Get("wep_07")!;
        Assert.Equal("flak_effect", ImpactOutcome.Resolve(flak, SurfaceClass.Default, false, true).EffectName);
        Assert.Equal("flak_effectplayer", ImpactOutcome.Resolve(flak, SurfaceClass.Player, false, true).EffectName);
    }

    /// <summary>A class that binds only <c>SURFACE_ANIMATION</c> — the armour-piercing rocket's
    /// ground effect — resolves through it, since <c>ANIMATION</c> is preferred but optional.</summary>
    [ExtractedDataFact]
    public void ASurfaceAnimationStandsInForAMissingAnimation()
    {
        var armour = WeaponDefs.Load(SharedZrdr).Get("wep_05")!;

        var outcome = ImpactOutcome.Resolve(armour, SurfaceClass.Default, false, true);
        Assert.Equal("ap_ground_effect", outcome.EffectName);
        Assert.Equal("snd_missile_pierce", outcome.Sound);
        Assert.Equal(40f, outcome.Damage);
        Assert.Equal(15f, outcome.BlastRadius);
    }

    /// <summary>The one shipped weapon with no <c>IMPACT</c> block at all resolves to nothing
    /// bound, without the lookup faulting on the empty table.</summary>
    [ExtractedDataFact]
    public void TheWeaponWithNoImpactBlockResolvesToNothingBound()
    {
        var fake = WeaponDefs.Load(SharedZrdr).Get("wep_26")!;
        Assert.Empty(fake.Impact);

        var outcome = ImpactOutcome.Resolve(fake, SurfaceClass.Default, false, true);
        Assert.Null(outcome.EffectName);
        Assert.Null(outcome.Sound);
        Assert.Equal(ImpactStandIn.DirtDebris, outcome.StandIn);
    }

    // ---- B5: the 48 weapons x the reachable surfaces -----------------------------------------

    /// <summary>The rule, not a snapshot: every one of the 48 shipped weapons, at every reachable
    /// surface, resolves an outcome that is coherent by three checks that hold regardless of which
    /// weapon or surface it is — never a table of expected per-row values, which is exactly the
    /// form the plan's trap warns would break on the next weapon-polish item.</summary>
    [ExtractedDataFact]
    public void Every48WeaponsResolvesACoherentOutcomeAtEveryReachableSurface()
    {
        var weapons = WeaponDefs.Load(SharedZrdr);
        Assert.Equal(48, weapons.All.Count);

        var sounds = SoundDefs.Load(SharedZrdr);
        var groups = SoundDefs.LoadGroups(SharedZrdr);
        var violations = new List<string>();

        foreach (var weapon in weapons.All)
        {
            // HasBlastDamage's rule, re-derived from the raw fields rather than read back off the
            // outcome it is meant to check — a positive-damage weapon with an authored radius owes
            // a blast; a zero-damage flash/flare special's radius is an effect radius only.
            var expectedBlast = weapon.HealthDamage is > 0f && weapon.ImpactProximity is > 0f;

            foreach (var surface in ReachableSurfaces)
            {
                var outcome = ImpactOutcome.Resolve(weapon, surface, modelResolved: false, hasEffectsRuntime: true);
                var where = $"{weapon.Id} ({weapon.Name}) / {surface}";

                if (outcome.EffectName == null && outcome.StandIn == ImpactStandIn.None)
                    violations.Add($"{where}: neither an effect name nor a stand-in");

                // A SOUND token in the IMPACT table names either a plain SETS def (snd_*) or a
                // SOUND_GROUPS entry (e.g. bullet_hit_sg) resolved through the group table at play
                // time (ImpactOutcome's own doc comment) — a real key can be absent from SoundDefs
                // and still be valid, so the coherent check is the union of both tables.
                if (outcome.Sound != null && !sounds.ContainsKey(outcome.Sound) && !groups.ContainsKey(outcome.Sound))
                    violations.Add($"{where}: sound '{outcome.Sound}' is neither a SoundDefs entry nor a SOUND_GROUPS name");

                if (outcome.HasBlastDamage != expectedBlast)
                    violations.Add($"{where}: HasBlastDamage was {outcome.HasBlastDamage}, expected {expectedBlast}");
            }
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    /// <summary>The third hand-picked case the review named: a rocket's authored splash model at
    /// water beats the stand-in ladder entirely, same as the synthetic case in
    /// <see cref="AResolvedModelLeavesNoStandIn"/> but against the shipped incendiary rocket's own
    /// table row rather than a hand-built weapon.</summary>
    [ExtractedDataFact]
    public void ARocketOnWaterResolvesTheGamezSplashModel()
    {
        var incendiary = WeaponDefs.Load(SharedZrdr).Get("wep_04")!;

        var outcome = ImpactOutcome.Resolve(incendiary, SurfaceClass.Water, modelResolved: true, hasEffectsRuntime: true);

        Assert.Equal("bsplsh.flt", outcome.EffectName);
        Assert.Equal("snd_bsplash", outcome.Sound);
        Assert.Equal(ImpactStandIn.None, outcome.StandIn);
        Assert.True(outcome.HasBlastDamage);
    }

    private static WeaponDef Gun() => new() { Id = "test_gun", Caliber = 30, IsCannon = true };

    private static WeaponDef Rocket() => new() { Id = "test_rocket", IsRocket = true };
}
