using CSVM.Flight;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The belt indicator's yellow tier belongs to guns only — a per-pylon hardpoint steps
/// straight from green to red at empty, matching the original. A damage zone's
/// four colour bands walk the combined armor+health fraction, armor spent first, matching the
/// game manual's Crispen Mark V description. Every member here is a pure static with no engine
/// dependency.
/// </summary>
public class GaugeColoursTests
{
    [Fact]
    public void HardpointColourIsNeverYellowAndStepsGreenToRed()
    {
        for (float frac = 0f; frac <= 1f; frac += 0.01f)
        {
            Assert.True(GaugeCluster.HardpointIndicatorColor(frac) != 1, $"hardpoint colour never yellow at frac={frac:0.00}");
        }
        Assert.Equal(2, GaugeCluster.HardpointIndicatorColor(0f));
        Assert.Equal(0, GaugeCluster.HardpointIndicatorColor(1f));
    }

    [Fact]
    public void GunColourStepsGreenYellowRedAtTheLowThreshold()
    {
        Assert.Equal(1, GaugeCluster.GunIndicatorColor(GaugeCluster.IndicatorLowFrac));
        Assert.Equal(0, GaugeCluster.GunIndicatorColor(GaugeCluster.IndicatorLowFrac + 0.01f));
        Assert.Equal(2, GaugeCluster.GunIndicatorColor(0f));
    }

    [Fact]
    public void AnUnfittedBeltPositionReadsRedNotDark()
    {
        // An unfitted belt position (index past the end of the loadout) reads red, not dark — the
        // original lights every position on the dial. A 2-gun plane on a 4-position gun dial:
        // slots 0-1 follow their ammo, 2-3 are red.
        float[] twoGuns = { 1f, 0f };
        for (int i = 0; i < 4; i++)
        {
            int want = i == 0 ? 0 : 2; // slot 0 full → green; slot 1 spent and slots 2-3 unfitted → red
            Assert.Equal(want, GaugeCluster.SlotIndicatorColor(twoGuns, i, isGun: true));
        }
        Assert.Equal(2, GaugeCluster.SlotIndicatorColor(new float[0], 7, isGun: false));
    }

    /// <summary>A damage zone's colour is the COMBINED
    /// armor+health fraction (<see cref="PlaneDamage.PartState.Fraction"/>), armor spent first,
    /// against the shipped thresholds (docs/formats/hud.md "Thresholds") — never a synthetic split
    /// from one pool. The three checkpoints reproduce the reconciliation with the
    /// game manual's four bands as a regression: on a stock zone (armor == hp), 56% of the armor
    /// gone lands exactly on the shipped yellow threshold, armor-zero-plus-8%-airframe on orange,
    /// and 60%-airframe on red. Each pool is spent through its own single-pool
    /// <see cref="PlaneDamage.Apply"/> call (armor's with healthDamage=0, health's with
    /// armorDamage=0) — the same technique DamageLab's two independent sliders use.</summary>
    [Fact]
    public void DamageZoneColourWalksTheCombinedArmorAndHealthFraction()
    {
        var zonePart = new DestroyablePart { Name = "nose", MaxHp = 20f, MaxArmor = 20f };
        var zoneDamage = new PlaneDamage(new[] { zonePart });
        var zoneState = zoneDamage.Parts["nose"];
        const float yellowAt = 0.72f, orangeAt = 0.46f, redAt = 0.20f;

        Assert.Equal(0, GaugeCluster.DamageZoneColor(zoneState.Fraction, yellowAt, orangeAt, redAt));

        zoneDamage.Apply("nose", 0f, 11.2f); // 56% of the armor gone, health untouched
        Assert.Equal(yellowAt, zoneState.Fraction, 3);
        Assert.Equal(1, GaugeCluster.DamageZoneColor(zoneState.Fraction, yellowAt, orangeAt, redAt));

        zoneDamage.Reset();
        zoneDamage.Apply("nose", 0f, 20f);  // armor fully spent...
        zoneDamage.Apply("nose", 1.6f, 0f); // ...plus 8% of the airframe
        Assert.Equal(orangeAt, zoneState.Fraction, 3);
        Assert.Equal(2, GaugeCluster.DamageZoneColor(zoneState.Fraction, yellowAt, orangeAt, redAt));

        zoneDamage.Reset();
        zoneDamage.Apply("nose", 0f, 20f); // armor fully spent...
        zoneDamage.Apply("nose", 12f, 0f); // ...plus 60% of the airframe
        Assert.Equal(redAt, zoneState.Fraction, 3);
        Assert.Equal(3, GaugeCluster.DamageZoneColor(zoneState.Fraction, yellowAt, orangeAt, redAt));
    }
}
