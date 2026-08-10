using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight;
using CSVM.Session;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Producer-range tripwires for <see cref="EffectCatalogue"/>: every producer of an effect name is
/// checked here to still emit only names the catalogue knows about, engine-free.
/// <see cref="ImpactOutcomeTests"/> carries the third (the gunhit formula, folded into its
/// weapon-by-surface battery).
/// </summary>
public class EffectCatalogueTests
{
    /// <summary>Every player airframe's node name (the same 11 <see cref="ExtractedGoldenTests
    /// .ShippedChaseDistances"/> lists), so <see cref="PlaneStats.Load"/> can walk every plane's
    /// data rather than just the one (the Devastator) that happens to carry a 0.99 entry today —
    /// a future plane picking one up must be caught here too.</summary>
    private static readonly string[] AllPlaneNodeNames =
    {
        "player_bhawk", "player_fury", "player_peacemaker", "player_kestrel", "player_fbrand",
        "player_warhawk", "player_balmoral", "player_pfighter", "player_autogyro",
        "player_avenger", "player_brigand",
    };

    private static string SharedZrdr =>
        SessionPaths.PreferUnzipped(System.IO.Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    /// <summary>The graze trio: <see cref="EffectCatalogue.TouchdownFor"/>'s whole producible range
    /// — every <see cref="SurfaceClass"/> value, reachable or not (it is a pure switch with no
    /// surface excluded) — resolves a name <see cref="EffectCatalogue.EffectAnimNames"/> knows.</summary>
    [Theory]
    [InlineData(SurfaceClass.Default)]
    [InlineData(SurfaceClass.Water)]
    [InlineData(SurfaceClass.Buildings)]
    [InlineData(SurfaceClass.Player)]
    [InlineData(SurfaceClass.Enemy)]
    [InlineData(SurfaceClass.Quicksand)]
    public void TouchdownForsWholeRangeIsInTheCatalogue(SurfaceClass surface)
    {
        string effect = EffectCatalogue.TouchdownFor(surface);

        Assert.Contains(effect, EffectCatalogue.EffectAnimNames);
    }

    /// <summary>Belt-and-braces on the trio itself, independent of the per-surface theory above:
    /// the whole enum's image under <see cref="EffectCatalogue.TouchdownFor"/> is exactly the three
    /// touchdown names <see cref="EffectCatalogue.EffectAnimNames"/> carries — no fourth name
    /// sneaks in and none of the three goes missing.</summary>
    [Fact]
    public void TouchdownForNeverProducesAFourthName()
    {
        var produced = new HashSet<string>(
            Enum.GetValues<SurfaceClass>().Select(EffectCatalogue.TouchdownFor));

        Assert.Equal(new HashSet<string> { "touchdown_default", "touchdown_dirt", "touchdown_water" }, produced);
    }

    /// <summary>The per-part damage-effect shims (`DamageVisuals.OnPartDamage`'s own filter,
    /// <c>anim.EndsWith("_damage_effects")</c>): across every player airframe's
    /// <c>destroyable_parts</c>, every <c>injure_anims</c> entry that filter would fire on must be
    /// one of <see cref="EffectCatalogue.PlaneDamageEffectAnims"/> — today only the Devastator
    /// carries any (its 0.99 entries), the other ten carry none, but a future plane picking one up
    /// with a name the catalogue does not know would otherwise wire nothing, silently.</summary>
    [ExtractedDataFact]
    public void EveryPlanesDamageEffectShimIsInTheCatalogue()
    {
        var zrdr = SharedZrdr;
        var violations = new List<string>();
        int found = 0;

        foreach (var nodeName in AllPlaneNodeNames)
        {
            var stats = PlaneStats.Load(zrdr, nodeName);
            foreach (var part in stats.DestroyableParts)
            {
                foreach (var (_, anim) in part.InjureAnims)
                {
                    if (!anim.EndsWith("_damage_effects", StringComparison.OrdinalIgnoreCase))
                        continue;
                    found++;
                    if (!EffectCatalogue.PlaneDamageEffectAnims.Contains(anim))
                        violations.Add($"{nodeName}/{part.Name}: '{anim}' is not in PlaneDamageEffectAnims");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));
        // The Devastator's four parts are the shipped case (DamageVisuals' own doc: "the 10
        // aircraft whose data carries no 0.99 entry at all") — a check that found none would be
        // passing on no evidence, exactly the trap ExtractedDataFact exists to avoid.
        Assert.True(found > 0, "no plane's data carried a *_damage_effects entry — the check ran on nothing");
    }

    /// <summary>The authored damage-stage menu: across every player airframe's
    /// injure_anims — per-part AND the vehicle-level list — every entry
    /// <see cref="DamageVisuals.RigAnimFor"/> maps to a rig anim must be a name the crash rig
    /// binds (<see cref="EffectCatalogue.PlayerDamageStageAnims"/> or
    /// <see cref="EffectCatalogue.PlaneDamageEffectAnims"/>), or the threshold crossing would
    /// play nothing, silently. Also pins the one deliberate mapping: the data's 0.10
    /// player_smoketrail entry plays player_damage_trail.</summary>
    [ExtractedDataFact]
    public void EveryInjureStageAnimIsBoundOnTheRig()
    {
        var zrdr = SharedZrdr;
        var bound = new HashSet<string>(
            EffectCatalogue.PlayerDamageStageAnims.Concat(EffectCatalogue.PlaneDamageEffectAnims),
            StringComparer.OrdinalIgnoreCase);
        var violations = new List<string>();
        int found = 0;

        foreach (var nodeName in AllPlaneNodeNames)
        {
            var stats = PlaneStats.Load(zrdr, nodeName);
            var entries = stats.DestroyableParts
                .SelectMany(p => p.InjureAnims.Select(e => (Where: p.Name, e.Anim)))
                .Concat(stats.VehicleInjureAnims.Select(e => (Where: "(vehicle)", e.Anim)));
            foreach (var (where, anim) in entries)
            {
                if (DamageVisuals.RigAnimFor(anim) is not { } stage)
                    continue;
                found++;
                if (!bound.Contains(stage))
                    violations.Add($"{nodeName}/{where}: '{anim}' plays '{stage}', which the rig does not bind");
            }
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));
        Assert.True(found > 0, "no plane's data carried a stage entry — the check ran on nothing");
        Assert.Equal("player_damage_trail", DamageVisuals.RigAnimFor("player_smoketrail"));
    }

    /// <summary>The healthy↔torn candidate sets derive from the authored defs — the
    /// hideable skins are exactly the two `*_h` nodes `plane_reset` re-ACTIVEs (pdp2_h/pdp3_h,
    /// one shared def OPERAND_NODE-retargeted at every airframe) and the torn set is exactly the
    /// eight `pdpN` targets of the `pdpanelN` defs. A parse change that drops either set silently
    /// re-engages DamageVisuals' geometric fallback; this pins the derivation to the data.</summary>
    [ExtractedDataFact]
    public void PanelPairingSetsDeriveFromTheAuthoredDefs()
    {
        var zrdr = SharedZrdr;
        var defs = CSVM.Mech3.AnimDefs.LoadFileDefs(zrdr, "player_destruct_reset.json")
            .Concat(CSVM.Mech3.AnimDefs.LoadFileDefs(zrdr, "player-1.json"))
            .ToList();
        Assert.True(defs.Count > 0, "the two reader files loaded no defs — the check ran on nothing");

        var pairing = DamageVisuals.PanelPairingSets(defs);

        Assert.NotNull(pairing);
        Assert.Equal(new[] { "pdp2_h", "pdp3_h" },
            pairing!.HideableHealthy.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
        Assert.Equal(new[] { "pdp1", "pdp2", "pdp3", "pdp4", "pdp5", "pdp6", "pdp7", "pdp8" },
            pairing.TornTargets.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
    }
}
