using CSVM.Session.Campaign;
using Xunit;

namespace CSVM.Tests;

/// <summary>The co-op allocation. One seat holds each unique hangar plane, the host's and the
/// wingman's first, and a seat with nothing left flies a stock Devastator.</summary>
public class CoopPlanePoolTests
{
    [Fact]
    public void GuestsClaimDistinctPlanesAfterTheHostAndTheWingman()
    {
        var profile = Hangar("A", "B", "C", "D", "E");
        profile.SelectedPlane = 2;
        profile.WingmanPlane = 0;

        Assert.Equal(new[] { 2, 1, 3, 4 }, CoopPlanePool.Allocate(profile, 4));
    }

    [Fact]
    public void ASeatBeyondThePoolFliesStock()
    {
        var profile = Hangar("A", "B", "C");
        profile.WingmanPlane = 1;

        Assert.Equal(new[] { 0, 2, CoopPlanePool.Stock, CoopPlanePool.Stock }, CoopPlanePool.Allocate(profile, 4));
    }

    [Fact]
    public void ANewProfileLeavesAGuestNothingButStock()
    {
        var allocated = CoopPlanePool.Allocate(CampaignProfileDef.NewProfile("Zachary"), 2);

        Assert.Equal(new[] { 0, CoopPlanePool.Stock }, allocated);
    }

    // The wingman's plane is held even when it names the host's own, and an index outside the
    // hangar holds nothing.
    [Fact]
    public void AWingmanOnTheHostsPlaneOrOutsideTheHangarFreesTheRest()
    {
        var profile = Hangar("A", "B");
        profile.WingmanPlane = 0;
        Assert.Equal(new[] { 0, 1 }, CoopPlanePool.Allocate(profile, 2));

        profile.WingmanPlane = 9;
        Assert.Equal(new[] { 0, 1 }, CoopPlanePool.Allocate(profile, 2));

        // ABLE-TO-FAIL CONTROL: a wingman on the other plane takes it.
        profile.WingmanPlane = 1;
        Assert.Equal(new[] { 0, CoopPlanePool.Stock }, CoopPlanePool.Allocate(profile, 2));
    }

    // Uniqueness is by name, so two records the hangar shows as one aircraft seat one human.
    [Fact]
    public void TwoRecordsSharingANameAreOnePlane()
    {
        var profile = Hangar("A", "B", "B", "C");
        profile.WingmanPlane = 3;

        Assert.Equal(new[] { 0, 1, CoopPlanePool.Stock }, CoopPlanePool.Allocate(profile, 3));
    }

    // A local splitscreen field and the network guests after it read the same prefix.
    [Fact]
    public void ASmallerSortieReadsTheSamePlanesAsTheWidest()
    {
        var profile = Hangar("A", "B", "C", "D");
        profile.WingmanPlane = 1;
        var widest = CoopPlanePool.Allocate(profile, 4);

        for (int seats = 1; seats <= 4; seats++)
        {
            Assert.Equal(widest[..seats], CoopPlanePool.Allocate(profile, seats));
        }
    }

    [Fact]
    public void NoProfileOrAnEmptyHangarIsAllStock()
    {
        Assert.Equal(new[] { CoopPlanePool.Stock, CoopPlanePool.Stock }, CoopPlanePool.Allocate(null, 2));
        Assert.Equal(new[] { CoopPlanePool.Stock }, CoopPlanePool.Allocate(new CampaignProfileDef(), 1));
        Assert.Empty(CoopPlanePool.Allocate(Hangar("A"), 0));
    }

    private static CampaignProfileDef Hangar(params string[] names)
    {
        var profile = new CampaignProfileDef { Name = "Zachary" };
        foreach (var name in names)
        {
            profile.Planes.Add(new OwnedPlane { Name = name, Airframe = 5 });
        }

        return profile;
    }
}
