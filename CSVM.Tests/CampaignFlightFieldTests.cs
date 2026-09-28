using System.IO;
using CSVM.Mech3;
using CSVM.Session.Campaign;
using CSVM.UI.Campaign;
using CSVM.UI.Menu;
using Xunit;

namespace CSVM.Tests;

/// <summary>The human field a co-op campaign sortie flies. The tests cover the walk through the
/// flight checks and the co-op allocation each guest's plane comes from. They also cover the copy
/// that keeps a guest's record out of the seated profile.</summary>
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

    // A new profile holds two planes, the seated player's and the wingman's, so a guest has
    // nothing unique left and flies the stock Devastator.
    [Fact]
    public void AGuestOverANewProfileFliesTheStockDevastator()
    {
        var flow = NewFlow(out _);
        flow.SetPlayers(2);

        var plane = flow.Field.Plane(1)!;

        Assert.Equal(CoopPlanePool.StockAirframe, plane.Airframe);
        Assert.True(flow.Field.IsStock(plane));
    }

    [Fact]
    public void GuestsTakeDistinctPlanesLeftAfterTheSeatedPlayerAndTheWingman()
    {
        var flow = NewFlow(out var profile, extra: 2);
        flow.SetPlayers(4);

        Assert.Equal(new[] { "Extra 0", "Extra 1" }, new[] { flow.Field.Plane(1)!.Name, flow.Field.Plane(2)!.Name });
        Assert.False(flow.Field.IsStock(flow.Field.Plane(1)!));
        Assert.True(flow.Field.IsStock(flow.Field.Plane(3)!));

        // The field's answer is the allocator's, seat for seat.
        var allocated = CoopPlanePool.Allocate(profile, 4);
        Assert.Equal(profile.Planes[allocated[1]].Name, flow.Field.Plane(1)!.Name);
        Assert.Equal(profile.Planes[allocated[2]].Name, flow.Field.Plane(2)!.Name);
    }

    // Freeing the wingman's plane on the seated player's check hands it to the first guest.
    [Fact]
    public void ACrewChangeReallocatesTheGuests()
    {
        var flow = NewFlow(out var profile, extra: 1);
        flow.SetPlayers(3);
        Assert.Equal("Extra 0", flow.Field.Plane(1)!.Name);
        Assert.True(flow.Field.IsStock(flow.Field.Plane(2)!));

        profile.WingmanPlane = 2;

        Assert.Equal("The Knave", flow.Field.Plane(1)!.Name);
        Assert.True(flow.Field.IsStock(flow.Field.Plane(2)!));
    }

    // ⚠ A guest flying one of the seated profile's aircraft flies a COPY. A reference here would
    // put anything written to its record into the profile's own.
    [Fact]
    public void AGuestFlyingAProfileAircraftFliesACopyOfIt()
    {
        var flow = NewFlow(out var profile, extra: 1);
        flow.SetPlayers(2);
        var copy = flow.Field.Plane(1)!;

        Assert.Equal("Extra 0", copy.Name);
        Assert.NotSame(profile.Planes[2], copy);
        copy.Ammo[0] = 3;
        Assert.Equal(0, profile.Planes[2].Ammo[0]);
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
