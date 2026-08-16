using System.IO;
using CSVM.Flight;
using CSVM.Testing;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Blast falloff, authored fuse/radius independence, and zero-damage special exclusion, against
/// the retail install (engine-free: <c>WeaponDefs.Load</c>
/// and <see cref="ProjectilePool"/>'s blast statics touch no live Node).
/// </summary>
public class WeaponBlastTests
{
    private static string ZrdrPath =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    [ExtractedDataFact]
    public void TorpedoAuthoredFuseAndBlastRadiusAreIndependent()
    {
        var weapons = WeaponDefs.Load(ZrdrPath, null);
        Assert.True(weapons.TryGet("wep_14", out var torpedo), "torpedo definition loads");
        Assert.Equal(1f, torpedo!.DetonationDistance ?? -1f);
        Assert.Equal(30f, torpedo.ImpactProximity ?? -1f);
    }

    [Fact]
    public void BlastDamageFallsOffLinearlyFromFullAtCentreToZeroAtTheEdge()
    {
        Assert.Equal(200f, ProjectilePool.BlastDamage(200f, 30f, 0f));
        Assert.Equal(100f, ProjectilePool.BlastDamage(200f, 30f, 15f));
        Assert.True(Mathf.IsZeroApprox(ProjectilePool.BlastDamage(200f, 30f, 30f)));
    }

    [ExtractedDataFact]
    public void ZeroDamageSpecialsAreExcludedFromBlastDamage()
    {
        var weapons = WeaponDefs.Load(ZrdrPath, null);
        Assert.True(weapons.TryGet("wep_09", out var flash), "flash definition loads");
        Assert.True(weapons.TryGet("wep_15", out var flare), "flare definition loads");
        Assert.False(ProjectilePool.HasBlastDamage(flash!), "zero-damage FLASH radius is not a damage blast");
        Assert.False(ProjectilePool.HasBlastDamage(flare!), "zero-damage FLARE radius is not a damage blast");
    }

    [Fact]
    public void TheAuthoredFuseDotGateAcceptsAheadAndRejectsBehind()
    {
        Assert.True(ProjectilePool.FuseDotAllows(0.3f, Vector3.Forward, Vector3.Forward));
        Assert.False(ProjectilePool.FuseDotAllows(0.3f, Vector3.Forward, Vector3.Back));
    }

    // D31: the one-shot pool's distance term — linear between a sound's own RANGE,
    // full inside the near edge, silent past the far edge, 1 (skip the term) with no nearest
    // human to measure against.
    [Fact]
    public void OneShotDistanceGainIsFullInsideRangeMinAndZeroPastRangeMax()
    {
        Assert.Equal(1f, ProjectilePool.DistanceGain(0f, 20f, 200f));
        Assert.Equal(1f, ProjectilePool.DistanceGain(20f, 20f, 200f));
        Assert.Equal(0f, ProjectilePool.DistanceGain(200f, 20f, 200f));
        Assert.Equal(0f, ProjectilePool.DistanceGain(5000f, 20f, 200f));
    }

    [Fact]
    public void OneShotDistanceGainFallsOffLinearlyBetweenRangeMinAndRangeMax()
    {
        Assert.Equal(0.5f, ProjectilePool.DistanceGain(110f, 20f, 200f));
    }

    [Fact]
    public void OneShotDistanceGainSkipsTheTermWithNoPlayerPositionsWired()
    {
        Assert.Equal(1f, ProjectilePool.DistanceGain(float.MaxValue, 20f, 200f));
    }
}
