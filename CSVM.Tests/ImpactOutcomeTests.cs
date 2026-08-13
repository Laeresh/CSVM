using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="ImpactOutcome.Resolve"/> — the decision "what should happen when this weapon hits
/// this surface id", taken apart from performing it. Nothing here builds a scene: the rule cases run
/// on hand-built <see cref="WeaponDef"/>s, and the data cases read the shipped
/// <c>weapons.zrd.json</c> so the table lookup is checked against the real <c>IMPACT</c> shapes
/// (a row that is present but all-null, a row the reader skips, a row that binds only
/// <c>SURFACE_ANIMATION</c>, and the eight registry ids no weapon authors at all) rather than
/// against fixtures that agree with the reader by construction.
/// </summary>
public class ImpactOutcomeTests
{
    /// <summary>The surface ids a round can actually strike in M3, which is a measurement rather
    /// than a choice: the seven ids some shipped material carries somewhere in the eight chapters
    /// (<c>analysis/surface-classification/FINDINGS.md</c>, 2026-08-11 per-chapter area table:
    /// <c>default</c>(0), <c>water</c>(1), <c>fire</c>(5), <c>airstrip</c>(8),
    /// <c>buildings</c>(11), <c>dzone</c>(12), <c>dirt</c>(13)) plus <c>player</c>(6), which
    /// <c>ProjectilePool.SurfaceIdOf</c> answers for a struck <c>AircraftBody</c>. The remaining
    /// six ids are carried by no material and by no body, so a case for them would be invented
    /// coverage — <c>quicksand</c>(3) included, even though three weapons author a row for it.</summary>
    private static readonly int[] ReachableSurfaceIds =
    {
        SurfaceRegistry.Default, SurfaceRegistry.Water, 5, 8, SurfaceRegistry.Player,
        SurfaceRegistry.Buildings, 12, 13,
    };

    private static string SharedZrdr =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    // ---- the stand-in ladder ------------------------------------------------------------------

    /// <summary>An instanced authored model beats every stand-in: when the effect name resolved to
    /// a real gamez node, the model IS the effect and nothing else draws.</summary>
    [Theory]
    [InlineData(SurfaceRegistry.Default)]
    [InlineData(SurfaceRegistry.Water)]
    [InlineData(SurfaceRegistry.Buildings)]
    public void AResolvedModelLeavesNoStandIn(int surfaceId)
    {
        var outcome = ImpactOutcome.Resolve(Gun(), surfaceId, modelResolved: true, hasEffectsRuntime: true);

        Assert.Equal(ImpactStandIn.None, outcome.StandIn);
    }

    /// <summary>Ground gets the tumbling debris burst, gun or rocket alike — every terrain id, not
    /// only <c>default</c>(0): the ladder stands in for missing assets and keeps the meaning the
    /// texture-derived <c>Default</c> class had before B11, so <c>dirt</c>(13) ground does not
    /// start sparking because the key changed.</summary>
    [Theory]
    [InlineData(SurfaceRegistry.Default)]
    [InlineData(13)]
    [InlineData(8)]
    public void ARoundOnGroundPicksTheDebrisBurst(int surfaceId)
    {
        Assert.Equal(ImpactStandIn.DirtDebris,
            ImpactOutcome.Resolve(Gun(), surfaceId, false, hasEffectsRuntime: true).StandIn);
        Assert.Equal(ImpactStandIn.DirtDebris,
            ImpactOutcome.Resolve(Rocket(), surfaceId, false, hasEffectsRuntime: true).StandIn);
    }

    /// <summary>A gun round off a building ricochets; a rocket on the same wall does not — the
    /// ricochet stands in for a gun-specific authored asset that is missing from the install.</summary>
    [Fact]
    public void OnlyAGunRicochetsOffABuilding()
    {
        Assert.Equal(ImpactStandIn.Ricochet,
            ImpactOutcome.Resolve(Gun(), SurfaceRegistry.Buildings, false, hasEffectsRuntime: true).StandIn);
        Assert.Equal(ImpactStandIn.Spark,
            ImpactOutcome.Resolve(Rocket(), SurfaceRegistry.Buildings, false, hasEffectsRuntime: true).StandIn);
    }

    /// <summary>Water and a struck aircraft fall to the single spark. This is the existing
    /// else-branch, not a case authored for <c>player</c>/<c>enemy</c>.</summary>
    [Theory]
    [InlineData(SurfaceRegistry.Water)]
    [InlineData(SurfaceRegistry.Player)]
    [InlineData(SurfaceRegistry.Enemy)]
    public void EverySurfaceWithNoLookOfItsOwnFallsToTheSpark(int surfaceId)
    {
        Assert.Equal(ImpactStandIn.Spark,
            ImpactOutcome.Resolve(Gun(), surfaceId, false, hasEffectsRuntime: true).StandIn);
    }

    /// <summary>With no world-effects runtime to build its real fireball, a hardpoint weapon shows
    /// the explosion stand-in — and it outranks the surface's own look, so the blast is visible in a
    /// scene-less pool whatever it hit. A gun never takes that branch.</summary>
    [Theory]
    [InlineData(SurfaceRegistry.Default)]
    [InlineData(SurfaceRegistry.Water)]
    [InlineData(SurfaceRegistry.Buildings)]
    public void AHardpointWeaponWithNoEffectsRuntimeExplodesInstead(int surfaceId)
    {
        Assert.Equal(ImpactStandIn.Explosion,
            ImpactOutcome.Resolve(Rocket(), surfaceId, false, hasEffectsRuntime: false).StandIn);
        Assert.NotEqual(ImpactStandIn.Explosion,
            ImpactOutcome.Resolve(Gun(), surfaceId, false, hasEffectsRuntime: false).StandIn);
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

        Assert.True(ImpactOutcome.Resolve(rocket, SurfaceRegistry.Default, false, true).HasBlastDamage);
        Assert.False(ImpactOutcome.Resolve(flash, SurfaceRegistry.Default, false, true).HasBlastDamage);
        Assert.False(ImpactOutcome.Resolve(contact, SurfaceRegistry.Default, false, true).HasBlastDamage);
        Assert.Equal(450f, ImpactOutcome.Resolve(flash, SurfaceRegistry.Default, false, true).BlastRadius);
        Assert.Equal(0f, ImpactOutcome.Resolve(contact, SurfaceRegistry.Default, false, true).BlastRadius);
    }

    /// <summary>A weapon with no damage keys at all resolves to zeroes rather than to nulls a
    /// caller has to re-default.</summary>
    [Fact]
    public void AWeaponWithNoDamageKeysResolvesToZeroes()
    {
        var outcome = ImpactOutcome.Resolve(Gun(), SurfaceRegistry.Default, false, true);

        Assert.Equal(0f, outcome.Damage);
        Assert.Equal(0f, outcome.BlastRadius);
        Assert.False(outcome.HasBlastDamage);
    }

    // ---- the IMPACT table lookup, against the shipped data ------------------------------------

    /// <summary>The 30 cal slug's own table: <c>default</c>(0) and <c>water</c>(1) each bind a name
    /// and a sound, and <c>buildings</c>(11) binds a name with no <c>SOUND</c> — a row is taken
    /// whole, so the building hit is silent rather than borrowing the default's sound.</summary>
    [ExtractedDataFact]
    public void TheSlugsTableIsReadPerSurfaceIdAndNotMerged()
    {
        var slug = WeaponDefs.Load(SharedZrdr).Get("wep_00")!;

        var ground = ImpactOutcome.Resolve(slug, SurfaceRegistry.Default, false, true);
        Assert.Equal("3040slug_gunhit", ground.EffectName);
        Assert.Equal("snd_grnd_bullet", ground.Sound);

        var water = ImpactOutcome.Resolve(slug, SurfaceRegistry.Water, false, true);
        Assert.Equal("splash1.flt", water.EffectName);
        Assert.Equal("snd_water_bullet", water.Sound);

        var wall = ImpactOutcome.Resolve(slug, SurfaceRegistry.Buildings, false, true);
        Assert.Equal("bld_damage.flt", wall.EffectName);
        Assert.Null(wall.Sound);
    }

    /// <summary>An id the weapon authors no row for plays nothing — no effect name and no sound,
    /// and specifically NOT the <c>default</c> row (`FUN_005ad100` has no empty-row-to-row-0 arm,
    /// unlike the crash cascade; `PLAN-surface-id-weapons` Decision 3). The slug's own table is the
    /// case with consequences: no shipped weapon authors <c>dirt</c>(13), which is up to 10.2 % of
    /// a chapter's collidable ground. The stand-in still draws, because that ladder is ours and
    /// stands in for assets that do not render here.</summary>
    [ExtractedDataFact]
    public void AnUnauthoredSurfaceIdPlaysNothingRatherThanTheDefaultRow()
    {
        var slug = WeaponDefs.Load(SharedZrdr).Get("wep_00")!;

        var dirt = ImpactOutcome.Resolve(slug, 13, false, true);
        Assert.Null(dirt.EffectName);
        Assert.Null(dirt.Sound);
        Assert.Equal(ImpactStandIn.DirtDebris, dirt.StandIn);

        // ...while the id it does author still resolves, so the null above is the rule and not a
        // broken lookup.
        Assert.Equal("3040slug_gunhit", ImpactOutcome.Resolve(slug, SurfaceRegistry.Default, false, true).EffectName);
    }

    /// <summary>The ids no terrain carries are read exactly as authored, with nothing supplied for
    /// them. The slug binds neither <c>player</c>(6) — its entry has every slot null — nor
    /// <c>enemy</c>(7), whose value is the data's "no effect on that surface" null; the reader
    /// drops both, so a round striking an aircraft draws nothing authored. The AA flak rocket does
    /// bind <c>player</c>, and that row is what a struck plane selects.</summary>
    [ExtractedDataFact]
    public void TheAircraftIdsAreReadOffTheTableLikeAnyOther()
    {
        var weapons = WeaponDefs.Load(SharedZrdr);
        var slug = weapons.Get("wep_00")!;

        Assert.Null(slug.ImpactFor(SurfaceRegistry.Player));
        Assert.Null(slug.ImpactFor(SurfaceRegistry.Enemy));
        Assert.Null(ImpactOutcome.Resolve(slug, SurfaceRegistry.Player, false, true).EffectName);
        Assert.Null(ImpactOutcome.Resolve(slug, SurfaceRegistry.Enemy, false, true).EffectName);

        var flak = weapons.Get("wep_07")!;
        Assert.Equal("flak_effect", ImpactOutcome.Resolve(flak, SurfaceRegistry.Default, false, true).EffectName);
        Assert.Equal("flak_effectplayer", ImpactOutcome.Resolve(flak, SurfaceRegistry.Player, false, true).EffectName);
    }

    /// <summary>A row that binds only <c>SURFACE_ANIMATION</c> — the armour-piercing rocket's
    /// ground effect — resolves through it, since <c>ANIMATION</c> is preferred but optional.</summary>
    [ExtractedDataFact]
    public void ASurfaceAnimationStandsInForAMissingAnimation()
    {
        var armour = WeaponDefs.Load(SharedZrdr).Get("wep_05")!;

        var outcome = ImpactOutcome.Resolve(armour, SurfaceRegistry.Default, false, true);
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
        Assert.All(fake.Impact, row => Assert.Null(row));

        var outcome = ImpactOutcome.Resolve(fake, SurfaceRegistry.Default, false, true);
        Assert.Null(outcome.EffectName);
        Assert.Null(outcome.Sound);
        Assert.Equal(ImpactStandIn.DirtDebris, outcome.StandIn);
    }

    // ---- the 48 weapons x the reachable surfaces ---------------------------------------------

    /// <summary>The rule, not a snapshot: every one of the 48 shipped weapons, at every reachable
    /// surface id, resolves an outcome that is coherent by three checks that hold regardless of which
    /// weapon or surface it is — never a table of expected per-row values, which is the form that
    /// breaks on the next weapon-polish change without catching anything.
    ///
    /// <para>This loop is also the producer-range guard for the gun IMPACT family: whichever
    /// outcome names a <c>*_gunhit</c> effect (caliber × ammo, e.g. <c>3040slug_gunhit</c>) must
    /// resolve inside <see cref="EffectCatalogue.EffectAnimNames"/>, so a caliber/ammo combination
    /// the catalogue does not know breaks here instead of silently playing nothing. Not every
    /// <see cref="ImpactOutcome.EffectName"/> qualifies: several resolve to a gamez MESH name
    /// instead (e.g. the slug's own <c>splash1.flt</c>/<c>bld_damage.flt</c>), which is a model to
    /// instance, never a catalogue entry — the gunhit family is the one whose name is always
    /// handed to the effects runtime.</para></summary>
    [ExtractedDataFact]
    public void Every48WeaponsResolvesACoherentOutcomeAtEveryReachableSurfaceId()
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

            foreach (var surfaceId in ReachableSurfaceIds)
            {
                var outcome = ImpactOutcome.Resolve(weapon, surfaceId, modelResolved: false, hasEffectsRuntime: true);
                var where = $"{weapon.Id} ({weapon.Name}) / {surfaceId}/{SurfaceRegistry.NameForId(surfaceId)}";

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

                // The gun-effect range this loop also guards (see the method doc): a *_gunhit
                // name must resolve in the catalogue.
                if (outcome.EffectName is { } gunhit && gunhit.EndsWith("_gunhit", StringComparison.Ordinal)
                    && !EffectCatalogue.EffectAnimNames.Contains(gunhit))
                    violations.Add($"{where}: gun effect '{gunhit}' is not in EffectCatalogue.EffectAnimNames");
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

        var outcome = ImpactOutcome.Resolve(incendiary, SurfaceRegistry.Water, modelResolved: true, hasEffectsRuntime: true);

        Assert.Equal("bsplsh.flt", outcome.EffectName);
        Assert.Equal("snd_bsplash", outcome.Sound);
        Assert.Equal(ImpactStandIn.None, outcome.StandIn);
        Assert.True(outcome.HasBlastDamage);
    }

    private static WeaponDef Gun() => new() { Id = "test_gun", Caliber = 30, IsCannon = true };

    private static WeaponDef Rocket() => new() { Id = "test_rocket", IsRocket = true };
}
