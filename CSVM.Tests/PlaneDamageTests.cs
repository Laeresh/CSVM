using System.Collections.Generic;
using CSVM.Flight;
using Xunit;

namespace CSVM.Tests;

/// <summary>The two-pool zone model: armor absorbs first and the share it could not absorb
/// carries into health within the same shot. Values are the stock Bloodhawk's (20 hp / 20 armor
/// per zone) and the weapons data's 30-calibre ammo matrix — AP `wep_32` 4.5 armor / 1.5 health,
/// dum-dum `wep_31` 1.5 / 4.5.</summary>
public class PlaneDamageTests
{
    [Fact]
    public void APartStartsWithBothPoolsFull()
    {
        var damage = Bloodhawk();
        var nose = damage.Parts["nose"];
        Assert.Equal(20f, nose.Hp);
        Assert.Equal(20f, nose.Armor);
        Assert.Equal(1f, nose.Fraction, 4);
        Assert.Equal(1f, damage.WorstFraction, 4);
        Assert.Equal("", damage.Summary());
    }

    [Fact]
    public void ArmorIsSpentBeforeHealth()
    {
        var damage = Bloodhawk();
        var nose = damage.Apply("nose", 4.5f, 4.5f)!;
        Assert.Equal(15.5f, nose.Armor, 4);
        Assert.Equal(20f, nose.Hp, 4);
    }

    [Fact]
    public void TheShareArmorCouldNotAbsorbCarriesIntoHealthInTheSameShot()
    {
        var damage = Bloodhawk();
        // Four AP rounds strip 18 of 20 armor; the fifth finds 2 left, so 2/4.5 of it is absorbed
        // and the remaining 5/9 of the round lands on health as 5/9 of its 1.5 health magnitude.
        for (int i = 0; i < 4; i++)
            damage.Apply("nose", 1.5f, 4.5f);
        var nose = damage.Parts["nose"];
        Assert.Equal(2f, nose.Armor, 4);
        Assert.Equal(20f, nose.Hp, 4);

        damage.Apply("nose", 1.5f, 4.5f);
        Assert.Equal(0f, nose.Armor, 4);
        Assert.Equal(20f - 1.5f * (2.5f / 4.5f), nose.Hp, 4);
    }

    [Fact]
    public void AStrippedZoneTakesTheFullHealthMagnitude()
    {
        var damage = Bloodhawk();
        damage.Apply("nose", 0f, 20f);          // strip the armor exactly
        var nose = damage.Parts["nose"];
        Assert.Equal(0f, nose.Armor, 4);
        Assert.Equal(20f, nose.Hp, 4);

        damage.Apply("nose", 4.5f, 1.5f);       // dum-dum on bare airframe
        Assert.Equal(15.5f, nose.Hp, 4);
        damage.Apply("nose", 1.5f, 4.5f);       // AP on bare airframe: little damage
        Assert.Equal(14f, nose.Hp, 4);
    }

    [Fact]
    public void AZoneWithNoArmorPoolTakesFullHealthDamageFromTheFirstShot()
    {
        var damage = Unarmored();
        var nose = damage.Apply("nose", 4.5f, 4.5f)!;
        Assert.Equal(0f, nose.Armor, 4);
        Assert.Equal(15.5f, nose.Hp, 4);
        Assert.Equal(15.5f / 20f, nose.Fraction, 4);
    }

    [Fact]
    public void ASingleMagnitudeSpendsExactlyThatMuchAcrossBothPools()
    {
        // A collision: player.json's crash block gives armor and health the same range, so the
        // pools behave as one 40-point pool spent armor end first.
        var damage = Bloodhawk();
        var nose = damage.Apply("nose", 30f)!;
        Assert.Equal(0f, nose.Armor, 4);
        Assert.Equal(10f, nose.Hp, 4);
        Assert.Equal(10f / 40f, nose.Fraction, 4);
    }

    [Fact]
    public void NeitherPoolGoesNegative()
    {
        var damage = Bloodhawk();
        var nose = damage.Apply("nose", 500f, 500f)!;
        Assert.Equal(0f, nose.Armor);
        Assert.Equal(0f, nose.Hp);
        Assert.Equal(0f, nose.Fraction, 4);
    }

    /// <summary>Armor at 0 is a stripped zone, not a dead one — only exhausted health counts
    /// toward the kill, which is what FlightController tests.</summary>
    [Fact]
    public void StrippingArmorDoesNotEmptyHealth()
    {
        var damage = Bloodhawk();
        var nose = damage.Apply("nose", 1.5f, 100f)!;
        Assert.Equal(0f, nose.Armor);
        Assert.True(nose.Hp > 0f);
    }

    /// <summary>The decoded kill rule (A4/D14): the vehicle dies when whole-vehicle health —
    /// the summary recompute over the parts — reaches zero, never when one critical part does.
    /// The flag stays parsed and is deliberately not consulted here.</summary>
    [Fact]
    public void OneDeadCriticalPartIsNotDestroyedButAllHealthGoneIs()
    {
        var damage = Bloodhawk();
        damage.Apply("nose", 500f, 500f); // the critical nose, dead
        Assert.False(damage.IsDestroyed);
        Assert.Equal(0.5f, damage.SummaryHealthFraction, 4);
        damage.Apply("tail", 500f, 500f); // the last zone's health
        Assert.True(damage.IsDestroyed);
        Assert.Equal(0f, damage.SummaryHealthFraction, 4);
        damage.Reset();
        Assert.False(damage.IsDestroyed);
        Assert.Equal(1f, damage.SummaryHealthFraction, 4);
    }

    [Fact]
    public void TheFractionIsTheCombinedProgressionThroughArmorThenHealth()
    {
        var damage = Bloodhawk();
        var nose = damage.Parts["nose"];
        damage.Apply("nose", 10f);              // half the armor
        Assert.Equal(0.75f, nose.Fraction, 4);
        Assert.Equal(0.5f, nose.ArmorFraction, 4);
        Assert.Equal(1f, nose.HealthFraction, 4);

        damage.Apply("nose", 10f);              // the rest of it
        Assert.Equal(0.5f, nose.Fraction, 4);
        Assert.Equal(0f, nose.ArmorFraction, 4);
        Assert.Equal(1f, nose.HealthFraction, 4);

        damage.Apply("nose", 10f);              // now into the airframe
        Assert.Equal(0.25f, nose.Fraction, 4);
        Assert.Equal(0.5f, nose.HealthFraction, 4);
    }

    [Fact]
    public void WorstFractionIsTheLowestPartAcrossBothPools()
    {
        var damage = Bloodhawk();
        damage.Apply("nose", 10f);
        damage.Apply("tail", 30f);
        Assert.Equal(0.25f, damage.WorstFraction, 4);
    }

    [Fact]
    public void ResetRefillsBothPools()
    {
        var damage = Bloodhawk();
        damage.Apply("nose", 35f);
        damage.Reset();
        var nose = damage.Parts["nose"];
        Assert.Equal(20f, nose.Armor);
        Assert.Equal(20f, nose.Hp);
        Assert.Equal("", damage.Summary());
    }

    [Fact]
    public void SummaryShowsBothPoolsForHurtPartsOnly()
    {
        var damage = Bloodhawk();
        damage.Apply("nose", 10f);
        Assert.Equal("nose a50%", damage.Summary());
        damage.Apply("nose", 20f);
        Assert.Equal("nose a0% h50%", damage.Summary());
        damage.Apply("tail", 30f);
        Assert.Equal("nose a0% h50% · tail a0% h50%", damage.Summary());
    }

    [Fact]
    public void AnUnknownPartTracksNothing()
    {
        var damage = Bloodhawk();
        Assert.Null(damage.Apply("leftwing", 5f, 5f));
    }

    private static PlaneDamage Bloodhawk() => new(new List<DestroyablePart>
    {
        new() { Name = "nose", MaxHp = 20f, MaxArmor = 20f, Critical = true },
        new() { Name = "tail", MaxHp = 20f, MaxArmor = 20f, Critical = true, Engine = true },
    });

    private static PlaneDamage Unarmored() => new(new List<DestroyablePart>
    {
        new() { Name = "nose", MaxHp = 20f, MaxArmor = 0f, Critical = true },
    });
}
