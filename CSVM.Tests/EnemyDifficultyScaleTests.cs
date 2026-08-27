using System;
using CSVM.Flight;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The difficulty setting's one effect: an enemy vehicle's armour and health maxima scaled at spawn.
/// Full decode: <c>docs/org/vehicleDamage.md</c> "The difficulty scale". Pins the three factors, the
/// team gate that decides who takes them, the two naming vocabularies, and the order against the
/// per-spawn jitter, which runs after this and bands around the scaled hull.
/// </summary>
public class EnemyDifficultyScaleTests
{
    /// <summary>The decoded factors. The low tier is 0.75 and not 0.875: the branch loads
    /// <c>k = -2</c>, so the spread is a symmetric two eighths either side of 1.0.</summary>
    [Fact]
    public void TheThreeTiersScaleByThreeQuartersOneAndFiveQuarters()
    {
        Assert.Equal(0.75f, Difficulty.EnemyDurabilityFactor(Difficulty.Normal));
        Assert.Equal(1f, Difficulty.EnemyDurabilityFactor(Difficulty.Hard));
        Assert.Equal(1.25f, Difficulty.EnemyDurabilityFactor(Difficulty.Hardest));
    }

    /// <summary>Anything off the three tiers lands on the middle one, the way the engine's own
    /// setter is fed (<c>0 -&gt; 0, 2 -&gt; 2, anything else -&gt; 1</c>).</summary>
    [Fact]
    public void AnyOtherValueIsTheMiddleTier()
    {
        Assert.Equal(1f, Difficulty.EnemyDurabilityFactor(-3));
        Assert.Equal(1f, Difficulty.EnemyDurabilityFactor(7));
        Assert.Equal(Difficulty.Hard, Difficulty.Clamp(99));
    }

    /// <summary>Two vocabularies name the same three tiers: the campaign selector's
    /// <c>IDS_DIFFICULTY</c> and Instant Action's <c>IDS_IA_DIFFICULTY</c>. Normal IS novice, which
    /// is the whole reason a wave's skill can stand in as the setting.</summary>
    [Fact]
    public void BothVocabulariesNameTheSameTiers()
    {
        Assert.Equal(Difficulty.Normal, Difficulty.Parse("normal"));
        Assert.Equal(Difficulty.Normal, Difficulty.Parse("novice"));
        Assert.Equal(Difficulty.Hard, Difficulty.Parse("Hard"));
        Assert.Equal(Difficulty.Hard, Difficulty.Parse("VETERAN"));
        Assert.Equal(Difficulty.Hardest, Difficulty.Parse(" hardest "));
        Assert.Equal(Difficulty.Hardest, Difficulty.Parse("ace"));
    }

    /// <summary>An unknown name is null rather than a tier, so a caller rejects it instead of
    /// silently flying a difficulty nobody asked for.</summary>
    [Fact]
    public void AnUnknownNameIsNotATier()
    {
        Assert.Null(Difficulty.Parse("brutal"));
        Assert.Null(Difficulty.Parse(""));
        Assert.Null(Difficulty.Parse(null));
    }

    /// <summary>The gate is the engine's: team differs from the player's. A spawn carrying no team
    /// is scaled, because an AI rig's fallback team is its shooter id's band and never the
    /// player's — a wingman is spared by carrying <see cref="AimAssist.PlayerTeam"/> explicitly.</summary>
    [Fact]
    public void OnlyThePlayersOwnTeamIsSpared()
    {
        Assert.Equal(1f, Difficulty.FactorForSpawn(AimAssist.PlayerTeam, null, Difficulty.Normal));
        Assert.Equal(0.75f, Difficulty.FactorForSpawn(null, null, Difficulty.Normal));
        Assert.Equal(0.75f, Difficulty.FactorForSpawn(2, null, Difficulty.Normal));
        Assert.Equal(0.75f, Difficulty.FactorForSpawn(AimAssist.NeutralTeam, null, Difficulty.Normal));
    }

    /// <summary>A per-spawn setting outranks the session's, which is the whole of what an Instant
    /// Action wave's skill does: it stands in as the global setting for that one spawn.</summary>
    [Fact]
    public void APerSpawnSettingOutranksTheSessions()
    {
        Assert.Equal(1.25f, Difficulty.FactorForSpawn(2, Difficulty.Hardest, Difficulty.Normal));
        Assert.Equal(0.75f, Difficulty.FactorForSpawn(2, Difficulty.Normal, Difficulty.Hardest));
        Assert.Equal(1.25f, Difficulty.FactorForSpawn(2, null, Difficulty.Hardest));
    }

    /// <summary>The whole-vehicle pair scales and the per-part pools do not, the same split the
    /// jitter makes. A parts-only airframe has its pair resolved to the sum on the way out, or
    /// <see cref="PlaneDamage"/> would re-derive the unscaled hull and the scale would vanish.</summary>
    [Fact]
    public void TheHullPairScalesAndThePartPoolsDoNot()
    {
        var authored = Authored();
        Assert.Null(authored.VehicleHealth);

        var enemy = authored.WithEnemyDurability(0.75f);

        Assert.Equal(30f, enemy.VehicleHealth!.Value, 3);
        Assert.Equal(30f, enemy.VehicleArmor!.Value, 3);
        foreach (var part in enemy.DestroyableParts)
        {
            Assert.Equal(20f, part.MaxHp);
            Assert.Equal(20f, part.MaxArmor);
        }
    }

    /// <summary>An authored pair (the AI base defs carry one) is scaled where it stands.</summary>
    [Fact]
    public void AnAuthoredHullPairIsScaledInPlace()
    {
        var authored = Authored();
        authored.VehicleHealth = 72f;
        authored.VehicleArmor = 72f;

        var enemy = authored.WithEnemyDurability(1.25f);

        Assert.Equal(90f, enemy.VehicleHealth!.Value, 3);
        Assert.Equal(90f, enemy.VehicleArmor!.Value, 3);
    }

    /// <summary>The session hands every aircraft off one airframe the same cached
    /// <see cref="PlaneStats"/>, so scaling must copy rather than perturb in place.</summary>
    [Fact]
    public void TheSharedAirframeStatsAreNotScaledInPlace()
    {
        var shared = Authored();
        shared.VehicleHealth = 72f;

        var enemy = shared.WithEnemyDurability(0.75f);

        Assert.Equal(72f, shared.VehicleHealth!.Value);
        Assert.Equal(54f, enemy.VehicleHealth!.Value, 3);
    }

    /// <summary>The unscaled tier costs nothing and changes nothing: the caller's own object comes
    /// back, so a player-team spawn and a middle-tier session take no copy at all.</summary>
    [Fact]
    public void TheUnscaledTierReturnsTheSameObject()
    {
        var authored = Authored();

        Assert.Same(authored, authored.WithEnemyDurability(1f));
    }

    /// <summary>The engine's spawn order: the scale lands on the authored pools and the jitter bands
    /// around the SCALED hull, never the reverse. At Normal a 40-point hull is 30 ± 5 %, which the
    /// unscaled band around 40 cannot reach.</summary>
    [Fact]
    public void TheJitterBandsAroundTheScaledHull()
    {
        var authored = Authored();

        var spawned = authored
            .WithEnemyDurability(Difficulty.EnemyDurabilityFactor(Difficulty.Normal))
            .WithAiSpawnJitter(new Random(7));

        float health = spawned.VehicleHealth!.Value;
        Assert.True(Math.Abs(health - 30f) <= 30f * PlaneStats.AiSpawnJitterSpread + 1e-4f,
            $"hull health {health} is outside 30 ± 5 %");
    }

    // Two parts and no authored pair, so the hull sum is 40 and the arithmetic is watchable.
    private static PlaneStats Authored()
    {
        var stats = new PlaneStats { FdSpeed = 100f, EnginePower = 0.5f };
        stats.DestroyableParts.Add(new DestroyablePart { Name = "nose", MaxHp = 20f, MaxArmor = 20f });
        stats.DestroyableParts.Add(new DestroyablePart { Name = "tail", MaxHp = 20f, MaxArmor = 20f });
        return stats;
    }
}
