using System.IO;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Session.Career;
using Xunit;

namespace CSVM.Tests;

/// <summary>The career core's contract. Draws are pure in the seed, and the weekly bill follows
/// the tunes. A broke payday becomes debt, and damage persists until a paid repair. The shelf and
/// the board roll the same for the same seed, and a career round-trips through its save. Expected
/// figures are computed by hand from <see cref="CareerTunes.Default"/>.</summary>
public class CareerTests
{
    private const ulong Seed = 0xC5_2026;

    [Fact]
    public void Dice_ArePureInSeedStreamAndKeys()
    {
        var a = CareerDice.For(Seed, "board", 3, 1);
        var b = CareerDice.For(Seed, "board", 3, 1);
        var c = CareerDice.For(Seed, "shelf", 3, 1);
        Assert.Equal(a.Next(0), b.Next(0));
        Assert.NotEqual(a.Next(0), c.Next(0));
        Assert.NotEqual(a.Next(0), a.Next(1));
        Assert.All(Enumerable.Range(0, 500), i => Assert.InRange(a.Range(i, 2, 5), 2, 5));
        Assert.All(Enumerable.Range(0, 500), i => Assert.InRange(a.Unit(i), 0.0, 0.9999999));
    }

    [Fact]
    public void NewCareer_StartsWithTheTunedMoneyAndWornPlanes()
    {
        var state = NewCareer();

        Assert.Equal(6000, state.Ledger.Cash);
        Assert.Equal(25000, state.Ledger.Debt);
        Assert.Equal(2, state.Planes.Count);
        // Four presses of nose armour is 20 units; a quarter of it is lost.
        Assert.Equal(5, state.Planes[0].ZoneDamage(ArmourZone.Nose));
        Assert.Equal(15, state.Planes[0].ZoneArmour(ArmourZone.Nose));
        Assert.Equal(5, state.Board.Offers.Count);
    }

    [Fact]
    public void Payday_ChargesWagesUpkeepInterestAndInstalment()
    {
        var state = NewCareer();
        state.AdvanceDays(6);
        Assert.Equal(6000, state.Ledger.Cash);

        state.AdvanceDays(1);

        // 6000 - 900 wages - 500 upkeep - 1000 instalment; debt + 2 % of 25000 - 1000.
        Assert.Equal(3600, state.Ledger.Cash);
        Assert.Equal(24500, state.Ledger.Debt);
        Assert.Equal(14, state.NextPayday);
    }

    [Fact]
    public void BrokePayday_TurnsTheShortfallIntoDebt()
    {
        var tunes = new CareerTunes { StartingCash = 1000, StartingDebt = 0 };
        var state = CareerState.New("Broke", Seed, new[] { Warhawk() }, tunes);

        state.AdvanceDays(7);

        // 900 wages paid, 100 of the 500 upkeep paid, 400 owed, then 2 % interest on it.
        Assert.Equal(0, state.Ledger.Cash);
        Assert.Equal(408, state.Ledger.Debt);
        Assert.Contains(state.Ledger.Entries, e => e.Category == LedgerCategory.Arrears && e.Debt == 400);
    }

    [Fact]
    public void Repair_PaysTheQuotePassesItsDaysAndClearsTheDamage()
    {
        var state = NewCareer();
        var plane = state.Planes[0];
        plane.DamagedParts = PlaneInstance.GunPart(0);
        var quote = plane.QuoteRepair(state.Tunes);

        // $5 per unit lost plus half of a .50 wing gun's $410; one day of armour, one of the gun.
        Assert.Equal((plane.TotalZoneDamage * 5) + 205, quote.Cost);
        Assert.Equal(2, quote.Days);

        Assert.True(state.Repair(0));

        Assert.False(plane.NeedsRepair);
        Assert.Equal(6000 - quote.Cost, state.Ledger.Cash);
        Assert.Equal(2, state.Day);
        Assert.False(state.Repair(0));
    }

    [Fact]
    public void AGroundedGang_RepairsOnCredit()
    {
        var state = CareerState.New("Grounded", Seed, new[] { Warhawk() }, new CareerTunes { StartingCash = 100, StartingDebt = 0 });
        state.Planes[0].Wreck();
        Assert.False(state.AnyPlaneCanFly);
        int cost = state.Planes[0].QuoteRepair(state.Tunes).Cost;

        Assert.True(state.Repair(0));

        Assert.True(state.Planes[0].CanFly);
        Assert.Equal(0, state.Ledger.Cash);
        Assert.True(state.Ledger.Debt >= cost - 100);
    }

    [Fact]
    public void AGangThatCanFly_GetsNoCredit()
    {
        var state = CareerState.New("Flying", Seed, new[] { Warhawk(), Brigand() }, new CareerTunes { StartingCash = 100 });
        state.Planes[0].Wreck();
        Assert.True(state.AnyPlaneCanFly);

        Assert.False(state.Repair(0));
        Assert.Equal(100, state.Ledger.Cash);
    }

    [Fact]
    public void ADamagedGun_DoesNotGroundAPlane()
    {
        var plane = new PlaneInstance(Warhawk()) { DamagedParts = PlaneParts.Gun1 };
        Assert.True(plane.CanFly);
        plane.DamagedParts |= PlaneParts.Engine;
        Assert.False(plane.CanFly);
    }

    [Fact]
    public void Damage_IsClampedToWhatTheZoneHolds()
    {
        var plane = new PlaneInstance(Warhawk());
        plane.Damage(ArmourZone.LeftWing, 1000);
        Assert.Equal(plane.ZoneCapacity(ArmourZone.LeftWing), plane.ZoneDamage(ArmourZone.LeftWing));
        Assert.Equal(0, plane.ZoneDamage(ArmourZone.RightWing));
    }

    [Fact]
    public void Shelf_SellsSlugWithoutLimitAndSpecialistsFromStock()
    {
        var state = NewCareer();
        var slug = new AmmoLine(2, AmmoKind.Slug);

        Assert.True(state.Shelf.BuyRounds(slug, 10, state.Day, state.Hold, state.Ledger, state.Tunes));
        Assert.Equal(1000, state.Hold.RoundsOf(slug));
        Assert.Equal(6000, state.Ledger.Cash);

        var (line, rounds) = state.Shelf.SpecialistStock.First();
        int lots = rounds / PortShelf.RoundsPerLot;
        Assert.False(state.Shelf.BuyRounds(line, lots + 1, state.Day, state.Hold, state.Ledger, state.Tunes));
        Assert.True(state.Shelf.BuyRounds(line, lots, state.Day, state.Hold, state.Ledger, state.Tunes));
        Assert.Equal(0, state.Shelf.LotsOf(line));
    }

    [Fact]
    public void Shelf_RefusesAPurchaseCashCannotCover()
    {
        var state = CareerState.New("Poor", Seed, new[] { Warhawk() }, new CareerTunes { StartingCash = 10 });
        var line = state.Shelf.SpecialistStock.First().Key;
        Assert.False(state.Shelf.BuyRounds(line, 1, 0, state.Hold, state.Ledger, state.Tunes));
        Assert.Equal(10, state.Ledger.Cash);
        Assert.Equal(0, state.Hold.RoundsOf(line));

        // Slug is free, so even a gang with no cash can arm its planes.
        var slug = new AmmoLine(4, AmmoKind.Slug);
        Assert.True(state.Shelf.BuyRounds(slug, 5, 0, state.Hold, state.Ledger, state.Tunes));
        Assert.Equal(10, state.Ledger.Cash);
        Assert.Equal(500, state.Hold.RoundsOf(slug));
    }

    [Fact]
    public void Shelf_RestockIsPureInSeedAndWeek()
    {
        var a = new PortShelf();
        var b = new PortShelf();
        a.Restock(Seed, 3, CareerTunes.Default);
        b.Restock(Seed, 3, CareerTunes.Default);
        Assert.Equal(a.SpecialistStock, b.SpecialistStock);
        Assert.Equal(a.RocketStock, b.RocketStock);
    }

    [Fact]
    public void Board_OffersAreDeterministicWithinTheTunes()
    {
        var a = NewCareer();
        var b = NewCareer();
        a.AdvanceDays(20);
        b.AdvanceDays(20);

        Assert.Equal(a.Board.Offers, b.Board.Offers);
        Assert.All(a.Board.Offers, c =>
        {
            var (min, max) = CareerTunes.Default.FeeBand(c.Threat);
            Assert.InRange(c.Fee, min, max);
            Assert.InRange(c.Threat, CareerTunes.Default.ThreatFloor, 5);
            Assert.True(c.ExpiresOn >= a.Day);
        });
    }

    [Fact]
    public void Board_ThreatFloorHolds()
    {
        var state = CareerState.New("Hard", Seed, new[] { Warhawk() }, new CareerTunes { ThreatFloor = 4 });
        state.AdvanceDays(30);
        Assert.All(state.Board.Offers, c => Assert.InRange(c.Threat, 4, 5));
    }

    [Fact]
    public void Accept_TakesOneContractAtATime()
    {
        var state = NewCareer();
        int first = state.Board.Offers[0].Id;
        int second = state.Board.Offers[1].Id;

        Assert.Equal(first, state.Accept(first)?.Id);
        Assert.Null(state.Accept(second));
        Assert.DoesNotContain(state.Board.Offers, c => c.Id == first);

        state.AbandonContract();
        Assert.Equal(second, state.Accept(second)?.Id);
    }

    [Fact]
    public void Store_RoundTripsACareerExactly()
    {
        var state = NewCareer();
        state.Shelf.BuyRounds(new AmmoLine(1, AmmoKind.Slug), 3, state.Day, state.Hold, state.Ledger, state.Tunes);
        state.Planes[1].DamagedParts = PlaneParts.Engine;
        state.Planes[1].Kills = 2;
        state.Accept(state.Board.Offers[2].Id);
        state.AdvanceDays(9);
        string text = CareerStore.Serialize(state);

        var dir = TestData.TempDir();
        new CareerStore(dir).Save(state);
        var loaded = new CareerStore(dir).Load("Test Gang");

        Assert.NotNull(loaded);
        Assert.Equal(text, CareerStore.Serialize(loaded!));
        Assert.Equal(new[] { "Test Gang" }, new CareerStore(dir).List());
    }

    [Fact]
    public void Store_FallsBackToTheBackupWhenTheFileIsDamaged()
    {
        var dir = TestData.TempDir();
        var store = new CareerStore(dir);
        var state = NewCareer();
        store.Save(state);
        state.AdvanceDays(3);
        store.Save(state);
        File.WriteAllText(store.PathFor("Test Gang"), "{ not json");

        var loaded = store.Load("Test Gang");

        Assert.NotNull(loaded);
        Assert.Equal(0, loaded!.Day);
    }

    [Fact]
    public void Store_RefusesAnotherVersionAndMalformedText()
    {
        string text = CareerStore.Serialize(NewCareer());
        Assert.Null(CareerStore.Deserialize(text.Replace("\"version\": 1", "\"version\": 99")));
        Assert.Null(CareerStore.Deserialize("[]"));
        Assert.Null(CareerStore.Deserialize("{\"version\": 1}"));
    }

    [Fact]
    public void Store_DeleteRemovesTheCareerAndItsBackup()
    {
        var dir = TestData.TempDir();
        var store = new CareerStore(dir);
        var state = NewCareer();
        store.Save(state);
        store.Save(state);

        store.Delete("Test Gang");

        Assert.Empty(Directory.GetFiles(dir));
        Assert.Null(store.Load("Test Gang"));
    }

    private static CareerState NewCareer() =>
        CareerState.New("Test Gang", Seed, new[] { Warhawk(), Brigand() });

    private static CustomPlaneDef Warhawk()
    {
        var def = new CustomPlaneDef
        {
            Name = "Rusty",
            Airframe = 10,
            Engine = 1,
            ArmourNose = 4,
            ArmourTail = 3,
            ArmourLeftWing = 4,
            ArmourRightWing = 4,
            LeftHardpoints = 2,
            RightHardpoints = 2,
        };
        def.Guns[0] = new GunChoice(2, false);
        def.Guns[1] = new GunChoice(1, false);
        return def;
    }

    private static CustomPlaneDef Brigand()
    {
        var def = new CustomPlaneDef { Name = "Old Faithful", Airframe = 4, Engine = 0, ArmourNose = 2, ArmourTail = 2 };
        def.Guns[0] = new GunChoice(3, false);
        return def;
    }
}
