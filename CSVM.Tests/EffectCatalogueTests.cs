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
/// existing B5 battery).
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
}
