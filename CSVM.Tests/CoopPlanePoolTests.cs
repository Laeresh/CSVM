using CSVM.Session.Campaign;
using Xunit;

namespace CSVM.Tests;

/// <summary>The rule that settles the co-op sortie's plane picks. One seat holds a plane, compared
/// by name, and the earlier seat keeps a plane two seats picked. A seat left without one flies the
/// stock Devastator, and the wingman comes after every human.</summary>
public class CoopPlanePoolTests
{
    private const int Stock = CoopPlanePool.Stock;
    private const int Unpicked = CoopPlanePool.Unpicked;
    private static readonly string[] Hangar = { "A", "B", "C", "D" };

    [Fact]
    public void EverySeatKeepsAFreePickOfItsOwn()
    {
        Assert.Equal(new[] { 0, 3, 1, 2 }, CoopPlanePool.Resolve(Hangar, new[] { 0, 3, 1, 2 }));
    }

    [Fact]
    public void OnAClashTheEarlierSeatKeepsThePlaneAndTheLaterTakesTheFirstFree()
    {
        Assert.Equal(new[] { 0, 2, 1 }, CoopPlanePool.Resolve(Hangar, new[] { 0, 2, 2 }));

        // ABLE-TO-FAIL CONTROL: the host's own pick is never moved by a guest's.
        Assert.Equal(new[] { 2, 0 }, CoopPlanePool.Resolve(Hangar, new[] { 2, 2 }));
    }

    [Fact]
    public void ASeatWithNoPickTakesTheFirstFreePlaneThenTheStockDevastator()
    {
        Assert.Equal(new[] { 1, 0, 2, 3, Stock }, CoopPlanePool.Resolve(Hangar, new[] { 1, Unpicked, Unpicked, Unpicked, Unpicked }));
    }

    [Fact]
    public void TheStockDevastatorIsSharedAndHoldsNothing()
    {
        Assert.Equal(new[] { Stock, Stock, 0 }, CoopPlanePool.Resolve(Hangar, new[] { Stock, Stock, Unpicked }));
    }

    [Fact]
    public void APickOutsideTheHangarIsNoPick()
    {
        Assert.Equal(new[] { 0, 1 }, CoopPlanePool.Resolve(Hangar, new[] { 0, 9 }));
    }

    [Fact]
    public void PlanesAreComparedByNameAsTheFlightChecksClashRuleCompares()
    {
        string[] twins = { "Twin", "Twin", "Other" };
        Assert.Equal(new[] { 0, 2 }, CoopPlanePool.Resolve(twins, new[] { 0, 1 }));
    }

    [Fact]
    public void TheWingmanKeepsItsSavedPlaneWhileNoHumanHoldsIt()
    {
        Assert.Equal(1, CoopPlanePool.Wingman(Hangar, new[] { 0, 2 }, 1));
    }

    [Fact]
    public void AHeldWingmanPlaneGivesWayToTheFirstFreeThenTheStockDevastator()
    {
        Assert.Equal(2, CoopPlanePool.Wingman(Hangar, new[] { 0, 1 }, 1));
        Assert.Equal(Stock, CoopPlanePool.Wingman(Hangar, new[] { 0, 1, 2, 3 }, 1));

        // ABLE-TO-FAIL CONTROL: a seat on the stock Devastator holds no hangar plane.
        Assert.Equal(1, CoopPlanePool.Wingman(Hangar, new[] { 0, Stock }, 1));
    }
}
