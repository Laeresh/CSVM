using CSVM.Flight.Weapons;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The turret gunner's cost (0x004ac720 under FUN_0041f9c0, docs/org/targeting.md "The picker's
/// cost"): distance + weight × 1200 + the vehicle pass's target_bias, minimised. A human weighs 0.8,
/// a wingman 1.4, a TARGETABLE round 0.6 on an airship's gun, everything else 1.0.
/// </summary>
[Trait("Tier", "Quick")]
public class TurretCostTests
{
    [Fact]
    public void OrdinaryGunTakesAnotherTargetOverThePlayerOnlyWhenItIs240MetresNearer()
    {
        Assert.True(Other(759f) < Human(1000f));
        Assert.False(Other(761f) < Human(1000f));
    }

    [Fact]
    public void AirshipGunTakesATorpedoOverThePlayerWhenItIsLessThan240MetresFarther()
    {
        Assert.True(Torpedo(739f, airshipGun: true) < Human(500f));
        Assert.False(Torpedo(741f, airshipGun: true) < Human(500f));
    }

    [Fact]
    public void OrdinaryGunWeighsATorpedoLikeAnyOtherTarget()
    {
        Assert.Equal(Other(600f), Torpedo(600f, airshipGun: false));
    }

    [Fact]
    public void HumanWeightComesBeforeWingmanAndReplacesTheBase()
    {
        Assert.Equal(0.8f, TurretController.VehicleWeight(true, true));
        Assert.Equal(1.4f, TurretController.VehicleWeight(false, true));
        Assert.Equal(1f, TurretController.VehicleWeight(false, false));
    }

    [Fact]
    public void TargetBiasGoesInRawAsMetres()
    {
        // player_pfighter's -300 against a structure's 0: the structure must be 540 m nearer.
        float player = TurretController.Cost(1000f, TurretController.PlayerWeight, -300f);
        Assert.True(TurretController.Cost(459f, 1f) < player);
        Assert.False(TurretController.Cost(461f, 1f) < player);
    }

    private static float Human(float d) => TurretController.Cost(d, TurretController.VehicleWeight(true, false));

    private static float Other(float d) => TurretController.Cost(d, TurretController.VehicleWeight(false, false));

    private static float Torpedo(float d, bool airshipGun) =>
        TurretController.Cost(d, airshipGun ? TurretController.OrdnanceWeight : 1f);
}
