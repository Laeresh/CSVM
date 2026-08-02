using System.IO;
using System.Text;
using CSVM.Session;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The effect-template pool sizes (<c>CSVM/data/effect_pools.json</c>, <c>BL-225</c>/<c>BL-231</c>) —
/// committed engine config, so these run without an extraction. Parsing is tested through
/// <see cref="EffectPools.Parse"/> rather than <c>Load</c>: the sizing decision is pure, while
/// <c>Load</c> adds file IO and engine warnings that need a session.
/// </summary>
public class EffectPoolsTests
{
    private static string ConfigPath =>
        Path.Combine(TestData.RepoRoot, "CSVM", "data", "effect_pools.json");

    [Fact]
    public void TheShippedFileSizesTheGunFamilyAtOneAndTheDamageSputtersDeeperThanTheDefault()
    {
        var pools = Load();
        // The gun roots stay SHARED on purpose (C8's throttle owns them), so a pool would be
        // copies nothing uses — this is the one deliberate 1.
        Assert.Equal(1, pools.SlotsFor("gunhit", players: 1));
        Assert.Equal(1, pools.SlotsFor("dum_gunhit", players: 1));
        Assert.Equal(1, pools.SlotsFor("mag_gunhit", players: 1));
        // Damage-stage smoke is the measured many-at-once case (five simultaneous kills wrapped a
        // 4-slot pool), so its root is sized above the default.
        Assert.True(pools.SlotsFor("partial_damage_obj", 1) > pools.SlotsFor("he_trails", 1));
    }

    [Fact]
    public void AnUnnamedRootTakesTheDefaultSize()
    {
        var pools = Load();
        // Only the deviations are authored; every other stage root reads the default entry.
        Assert.Equal(pools.Default.Base, pools.SlotsFor("he_ring", players: 1));
        Assert.Equal(pools.Default.Base, pools.SlotsFor("not_a_root_at_all", players: 1));
    }

    [Fact]
    public void ExtraPlayersDeepenThePoolButTheSharedRootsStayShared()
    {
        var pools = Load();
        int solo = pools.SlotsFor("he_trails", players: 1);
        // Splitscreen/multiplayer is the reason the per-player term exists: every extra aircraft is
        // another rocket landing somewhere else.
        Assert.True(pools.SlotsFor("he_trails", 4) > solo);
        Assert.True(pools.SlotsFor("he_trails", 16) > pools.SlotsFor("he_trails", 4));
        // ...but a root whose perExtraPlayer is 0 does not grow with the session.
        Assert.Equal(1, pools.SlotsFor("gunhit", 16));
    }

    [Fact]
    public void NoRootEverExceedsTheAuthoredCeiling()
    {
        var pools = Load();
        // The cap is the memory guard — each slot is one more copy of that root's subtree.
        Assert.Equal(pools.MaxSlots, pools.SlotsFor("partial_damage_obj", players: 64));
        Assert.Equal(pools.MaxSlots, pools.SlotsFor("he_trails", players: 64));
    }

    [Fact]
    public void APoolIsNeverSizedBelowOneCopy()
    {
        // A hand-edit to 0 (or a negative) would stage no template at all, which reads in-game as
        // "the effect is broken" rather than "the pool is off" — clamp instead.
        var pools = EffectPools.Parse(Json("{\"default\":{\"base\":0},\"roots\":{\"he_ring\":{\"base\":-3}}}"));
        Assert.Equal(1, pools.SlotsFor("he_ring", 1));
        Assert.Equal(1, pools.SlotsFor("anything", 1));
    }

    [Fact]
    public void APartialFileOverridesOnlyWhatItNames()
    {
        // Overriding one root must not silently drop the built-in default for every other.
        var pools = EffectPools.Parse(Json("{\"roots\":{\"he_ring\":{\"base\":9}}}"));
        Assert.Equal(9, pools.SlotsFor("he_ring", 1));
        Assert.Equal(EffectPools.Fallback.Default.Base, pools.SlotsFor("he_trails", 1));
    }

    [Fact]
    public void ARootEntryWithoutABaseIsIgnoredRatherThanReadAsZero()
    {
        var pools = EffectPools.Parse(Json("{\"roots\":{\"he_ring\":{\"why\":\"prose only\"}}}"));
        Assert.Equal(pools.Default.Base, pools.SlotsFor("he_ring", 1));
    }

    [Fact]
    public void ARootNameThatIsNotAStageRootIsReported()
    {
        // A typo sizes nothing; the build says so rather than leaving it invisible.
        var pools = EffectPools.Parse(Json("{\"roots\":{\"he_ring\":{\"base\":2},\"he_rong\":{\"base\":2}}}"));
        Assert.Equal(new[] { "he_rong" }, pools.UnknownRoots(new[] { "he_ring", "he_trails" }));
    }

    [Fact]
    public void TheShippedFileNamesOnlyRealStageRoots()
    {
        // The committed file is checked against the live stage-root table, so a rename on either
        // side fails here rather than in a silently unsized session.
        Assert.Empty(Load().UnknownRoots(WorldEffectsFactory.EffectStageRootNames));
    }

    [Fact]
    public void TheStageDepthIsTheDeepestRootNotTheDefault()
    {
        var pools = Load();
        var roots = WorldEffectsFactory.EffectStageRootNames;
        int depth = pools.DepthFor(roots, players: 1);
        Assert.Equal(pools.SlotsFor("partial_damage_obj", 1), depth);
        Assert.True(depth > pools.Default.Base);
    }

    private static byte[] Json(string s) => Encoding.UTF8.GetBytes(s);

    private static EffectPools Load() => EffectPools.Parse(File.ReadAllBytes(ConfigPath));
}
