using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Session.Career;
using Xunit;

namespace CSVM.Tests;

/// <summary>Applying a flown sortie to a career. A met primary pays the fee plus a quarter per
/// secondary, and an unmet one pays nothing. Damage, kills and fired stock land on the planes and
/// the hold. A crash leaves a wreck in the hangar, and the contract closes.</summary>
public class SortieResultTests
{
    [Theory]
    [InlineData(true, 0, 1.0, 0.0)]
    [InlineData(true, 2, 1.0, 0.5)]
    [InlineData(false, 2, 0.0, 0.0)]
    public void Pay_FollowsThePrimaryAndTheSecondaries(bool primary, int secondaries, double feeShare, double bonusShare)
    {
        var state = Career(out _);
        int cash = state.Ledger.Cash;
        int contractFee = state.ActiveContract!.Fee;
        int fee = (int)(contractFee * feeShare);
        int bonus = (int)Math.Round(contractFee * bonusShare);

        var outcome = state.CompleteSortie(new SortieResult { PrimaryMet = primary, SecondariesMet = secondaries });

        Assert.Equal(fee, outcome.Fee);
        Assert.Equal(bonus, outcome.Bonus);
        Assert.Equal(cash + fee + bonus, state.Ledger.Cash);
        Assert.Null(state.ActiveContract);
        Assert.Equal(1, state.Day);
    }

    [Fact]
    public void Damage_KillsAndPartsLandOnThePlane()
    {
        var state = Career(out _);
        var plane = state.Planes[0];
        int nose = plane.ZoneDamage(ArmourZone.Nose);
        int wing = plane.ZoneDamage(ArmourZone.RightWing);

        state.CompleteSortie(new SortieResult
        {
            PrimaryMet = true,
            Planes =
            {
                new PlaneReport { Plane = 0, ZoneDamage = new[] { 3, 0, 0, 7 }, PartsHit = PlaneParts.Gun2, Kills = 2 },
            },
        });

        Assert.Equal(nose + 3, plane.ZoneDamage(ArmourZone.Nose));
        Assert.Equal(wing + 7, plane.ZoneDamage(ArmourZone.RightWing));
        Assert.Equal(PlaneParts.Gun2, plane.DamagedParts);
        Assert.Equal(2, plane.Kills);
        Assert.Equal(1, plane.Sorties);
        Assert.Equal(0, state.Planes[1].Sorties);
    }

    [Fact]
    public void ACrash_LeavesAWreckInTheHangar()
    {
        var state = Career(out _);
        var outcome = state.CompleteSortie(new SortieResult
        {
            PrimaryMet = true,
            Planes = { new PlaneReport { Plane = 1, Crashed = true } },
        });

        var wreck = state.Planes[1];
        Assert.Equal(new[] { 1 }, outcome.Wrecked);
        Assert.All(Enum.GetValues<ArmourZone>(), z => Assert.Equal(0, wreck.ZoneArmour(z)));
        // The Brigand mounts one gun, in its second slot, so only that gun and the engine are damaged.
        Assert.Equal(PlaneParts.Gun2 | PlaneParts.Engine, wreck.DamagedParts);
        Assert.Equal(2, state.Planes.Count);
        Assert.True(wreck.QuoteRepair(state.Tunes).Cost > 0);
    }

    [Fact]
    public void FiredStock_LeavesTheHold()
    {
        var state = Career(out var slug);
        state.Hold.AddRockets("wep_06", 4);

        var outcome = state.CompleteSortie(new SortieResult
        {
            PrimaryMet = true,
            Planes =
            {
                new PlaneReport
                {
                    Plane = 0,
                    RoundsFired = new Dictionary<AmmoLine, int> { [slug] = 350 },
                    RocketsFired = new Dictionary<string, int> { ["wep_06"] = 6 },
                },
            },
        });

        // The hold held 1,000 rounds and 4 rockets; a report claiming more takes only what was there.
        Assert.Equal(650, state.Hold.RoundsOf(slug));
        Assert.Equal(0, state.Hold.RocketsOf("wep_06"));
        Assert.Equal(350, outcome.RoundsUsed);
        Assert.Equal(4, outcome.RocketsUsed);
    }

    [Fact]
    public void WithoutAContract_ASortieIsRefused()
    {
        var state = CareerState.New("Idle", 5, new[] { Plane(10) });
        Assert.Throws<InvalidOperationException>(() => state.CompleteSortie(new SortieResult { PrimaryMet = true }));
    }

    private static CareerState Career(out AmmoLine slug)
    {
        var state = CareerState.New("Gang", 5, new[] { Plane(10), Plane(4) });
        slug = new AmmoLine(2, AmmoKind.Slug);
        state.Hold.AddRounds(slug, 1000);
        state.Accept(state.Board.Offers.First().Id);
        return state;
    }

    private static CustomPlaneDef Plane(int airframe)
    {
        var def = new CustomPlaneDef { Name = $"Plane {airframe}", Airframe = airframe, Engine = 1, ArmourNose = 4, ArmourTail = 4, ArmourLeftWing = 4, ArmourRightWing = 4 };
        def.Guns[0] = new GunChoice(2, false);
        if (airframe == 4)
        {
            def.Guns[0] = default;
            def.Guns[1] = new GunChoice(3, false);
        }

        return def;
    }
}
