using System.IO;
using System.Linq;
using CSVM.Mech3;
using CSVM.Session.Campaign;
using CSVM.UI.Campaign;
using CSVM.UI.Menu;
using Xunit;

namespace CSVM.Tests;

/// <summary>The human field a co-op campaign sortie flies. It covers the walk through the flight
/// checks and the rule that one human at a time flies each plane of the hangar. It also covers the
/// copy that keeps a guest's ammunition edits out of the seated profile.</summary>
public class CampaignFlightFieldTests
{
    [Fact]
    public void ASoloCampaignHasNoGuestsAndNowhereToAdvanceTo()
    {
        var flow = NewFlow(out _);

        Assert.Equal(1, flow.Field.Players);
        Assert.Empty(flow.Field.Guests);
        Assert.False(flow.Field.Advance());
        Assert.True(flow.Field.Locked);
    }

    [Fact]
    public void TheWalkVisitsEveryJoinedPlayerOnceAndThenStops()
    {
        var flow = NewFlow(out _);
        flow.SetPlayers(3);

        Assert.Equal(0, flow.Field.Current);
        Assert.True(flow.Field.Advance());
        Assert.Equal(1, flow.Field.Current);
        Assert.True(flow.Field.Advance());
        Assert.Equal(2, flow.Field.Current);
        Assert.False(flow.Field.Advance());
    }

    // The field is joinable up to and including the seated player's FLY MISSION, and
    // that press is now what starts the walk rather than what leaves the screen.
    [Fact]
    public void TheFieldLocksOnceTheWalkStartsAndUnlocksWhenItIsAbandoned()
    {
        var flow = NewFlow(out _);
        flow.SetPlayers(2);

        Assert.False(flow.Field.Locked);
        Assert.True(flow.Field.Advance());
        Assert.True(flow.Field.Locked);
        Assert.True(flow.Field.Retreat());
        Assert.True(flow.Field.Locked);
        Assert.False(flow.Field.Retreat());

        flow.Field.Rewind();

        Assert.False(flow.Field.Locked);
    }

    [Fact]
    public void RewindPutsTheSeatedPlayerBackOnTheirOwnCheck()
    {
        var flow = NewFlow(out _);
        flow.SetPlayers(4);
        flow.Field.Advance();
        flow.Field.Advance();

        flow.Field.Rewind();

        Assert.Equal(0, flow.Field.Current);
    }

    // A guest leaving mid-sequence drops the trailing record and pulls the cursor back into the
    // field, so the screen can never be showing a check nobody is flying.
    [Fact]
    public void AShrinkingFieldDropsTheTrailingGuestAndClampsTheCursor()
    {
        var flow = NewFlow(out _);
        flow.SetPlayers(3);
        flow.Field.Advance();
        flow.Field.Advance();

        flow.SetPlayers(2);

        Assert.Single(flow.Field.Guests);
        Assert.Equal(1, flow.Field.Current);
        Assert.True(flow.Field.Locked);
    }

    // A guest's roster is a copy of each of the seated profile's planes, then the stock Devastator.
    [Fact]
    public void AGuestsRosterIsTheHangarThenTheStockDevastator()
    {
        var flow = NewFlow(out _, extra: 1);
        flow.SetPlayers(2);

        var guest = flow.Field.Guests[0];

        Assert.Equal(new[] { "Gypsy Magic", "The Knave", "Extra 0" }, guest.Choices.Take(3).Select(p => p.Name));
        Assert.Equal(3, guest.StockChoice);
        Assert.True(flow.Field.IsStock(guest.Choices[guest.StockChoice]));
        Assert.Equal(CoopPlanePool.StockAirframe, guest.Choices[guest.StockChoice].Airframe);
        Assert.False(flow.Field.IsStock(guest.Choices[0]));
    }

    // With no pick of their own, guests open on the first plane nobody holds, in seat order. Past
    // the hangar they open on the stock Devastator, which any number of seats fly.
    [Fact]
    public void GuestsOpenOnTheFirstFreePlaneThenTheStockDevastator()
    {
        var flow = NewFlow(out _);
        flow.SetPlayers(4);

        Assert.Equal("The Knave", flow.Field.Plane(1)!.Name);
        Assert.Equal(CoopPlanePool.Stock, flow.Field.Guests[1].HangarPick);
        Assert.Equal(CoopPlanePool.Stock, flow.Field.Guests[2].HangarPick);
        Assert.True(flow.Field.IsStock(flow.Field.Plane(2)!));
        Assert.Equal(-1, flow.Field.Holder(3, flow.Field.Guests[2].StockChoice));
    }

    // A plane another human flies is refused, and the refusal names that human's seat.
    [Fact]
    public void APlaneAnotherHumanFliesIsRefusedNamingTheirSeat()
    {
        var flow = NewFlow(out _);
        flow.SetPlayers(3);

        Assert.Equal(0, flow.Field.Holder(2, 0));
        Assert.Equal(1, flow.Field.Holder(2, 1));
        Assert.False(flow.Field.Choose(2, 1));
        Assert.False(flow.Field.Choose(2, 0));
        Assert.True(flow.Field.IsStock(flow.Field.Plane(2)!));

        // The seated player's picker asks the same rule.
        Assert.Equal(1, flow.Field.HolderOf(0, flow.Profile!.Planes[1]));
    }

    // A free plane is what Choose is for: the picker's ACCEPT, moving one guest's own pick.
    [Fact]
    public void ChoosingAFreePlaneMovesThatGuestsPickAndNobodyElses()
    {
        var flow = NewFlow(out var profile, extra: 1);
        flow.SetPlayers(3);
        int before = flow.Field.Guests[1].Choice;

        Assert.True(flow.Field.Choose(1, flow.Field.Guests[0].StockChoice));
        Assert.True(flow.Field.Choose(2, 1));

        Assert.Equal("The Knave", flow.Field.Plane(2)!.Name);
        Assert.NotEqual(before, flow.Field.Guests[1].Choice);
        Assert.True(flow.Field.IsStock(flow.Field.Plane(1)!));
        Assert.Equal(0, profile.SelectedPlane);
    }

    // ⚠ A guest flying one of the seated profile's aircraft flies a COPY. A reference here would
    // put anything written to its record into the profile's own.
    [Fact]
    public void AGuestFlyingAProfileAircraftFliesACopyOfIt()
    {
        var flow = NewFlow(out var profile);
        flow.SetPlayers(2);
        var copy = flow.Field.Plane(1)!;

        Assert.Equal("The Knave", copy.Name);
        Assert.NotSame(profile.Planes[1], copy);
        copy.Ammo[0] = 3;
        Assert.Equal(0, profile.Planes[1].Ammo[0]);
    }

    // Two guests never share a record either, the stock Devastator included.
    [Fact]
    public void TwoGuestsHoldSeparateRecords()
    {
        var flow = NewFlow(out _);
        flow.SetPlayers(3);

        var first = flow.Field.Guests[0];
        var second = flow.Field.Guests[1];

        Assert.NotSame(first.Choices[1], second.Choices[1]);
        Assert.NotSame(first.Choices[first.StockChoice], second.Choices[second.StockChoice]);
    }

    private static CampaignFlow NewFlow(out CampaignProfileDef profile, int extra = 0)
    {
        var store = new CampaignProfileStore(Path.Combine(TestData.TempDir(), "Profiles"));
        var seeded = CampaignProfileDef.NewProfile("Zachary");
        for (int i = 0; i < extra; i++)
        {
            seeded.Planes.Add(new OwnedPlane { Name = $"Extra {i}", Airframe = 7 + i });
        }

        store.Save(seeded);
        var flow = new CampaignFlow(store, UiStrings.Empty);
        profile = store.Load("Zachary")!;
        flow.SelectProfile(profile);
        flow.SetMission(0);
        return flow;
    }
}
