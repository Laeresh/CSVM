using System.IO;
using System.Linq;
using CSVM.Session.Career;
using CSVM.UI.Menu;
using Xunit;

namespace CSVM.Tests;

/// <summary>The career's menu feature over a scratch store. A name must be new, non-empty and
/// short. Starting saves at once, and every action that changes the career saves it again. Deleting
/// the open career closes it.</summary>
public class CareerFeatureTests
{
    [Fact]
    public void Start_SavesANewCareerAndOpensIt()
    {
        var (feature, store) = Feature();

        Assert.True(feature.Start("  Rusty Gulls "));

        Assert.Equal("Rusty Gulls", feature.Current!.Name);
        Assert.Equal(new[] { "Rusty Gulls" }, feature.Careers);
        Assert.Equal(7UL, store.Load("Rusty Gulls")!.Seed);
        Assert.Equal(2, feature.Current.Planes.Count);
        // Without stock loadouts a starter is named for its place and keeps no guns.
        Assert.Equal("Plane 1", feature.Current.Planes[0].Build.Name);
    }

    [Theory]
    [InlineData("", "Name the gang first")]
    [InlineData("   ", "Name the gang first")]
    [InlineData("A name far longer than any gang needs", "That name is too long")]
    [InlineData("rusty gulls", "A gang of that name already flies")]
    public void NameRefusal_GuardsTheSaveFile(string name, string refusal)
    {
        var (feature, _) = Feature();
        feature.Start("Rusty Gulls");

        Assert.Equal(refusal, feature.NameRefusal(name));
        Assert.False(feature.Start(name));
        Assert.Single(feature.Careers);
    }

    [Fact]
    public void Actions_SaveTheCareer()
    {
        var (feature, store) = Feature();
        feature.Start("Rusty Gulls");
        int offer = feature.Current!.Board.Offers[0].Id;

        feature.Wait();
        Assert.True(feature.Accept(offer));

        var saved = store.Load("Rusty Gulls")!;
        Assert.Equal(1, saved.Day);
        Assert.Equal(offer, saved.ActiveContract!.Id);

        feature.Abandon();
        Assert.Null(store.Load("Rusty Gulls")!.ActiveContract);
    }

    [Fact]
    public void Repair_SavesAndRefusesAPlaneNotThere()
    {
        var (feature, store) = Feature();
        feature.Start("Rusty Gulls");

        Assert.False(feature.Repair(5));
        Assert.True(feature.Repair(0));
        Assert.False(store.Load("Rusty Gulls")!.Planes[0].NeedsRepair);
    }

    [Fact]
    public void BuyingSlug_FillsTheHoldForFree()
    {
        var (feature, _) = Feature();
        feature.Start("Rusty Gulls");
        var slug = new AmmoLine(2, AmmoKind.Slug);

        Assert.True(feature.BuyRounds(slug, 3));
        Assert.Equal(300, feature.Current!.Hold.RoundsOf(slug));
        Assert.Equal(CareerTunes.Default.StartingCash, feature.Current.Ledger.Cash);
    }

    [Fact]
    public void Open_AndDelete_FollowTheStore()
    {
        var (feature, _) = Feature();
        feature.Start("Rusty Gulls");
        feature.Close();
        Assert.Null(feature.Current);

        Assert.True(feature.Open("Rusty Gulls"));
        Assert.False(feature.Open("Nobody"));
        Assert.Null(feature.Current);

        feature.Open("Rusty Gulls");
        feature.Delete("Rusty Gulls");
        Assert.Null(feature.Current);
        Assert.Empty(feature.Careers);
    }

    [Fact]
    public void WithNoCareerOpen_ActionsDoNothing()
    {
        var (feature, _) = Feature();
        feature.Wait();
        Assert.False(feature.Accept(1));
        Assert.False(feature.Repair(0));
        Assert.Null(feature.Current);
    }

    private static (CareerFeature Feature, CareerStore Store) Feature()
    {
        var store = new CareerStore(Path.Combine(TestData.TempDir(), "Careers"));
        return (new CareerFeature(() => store, newSeed: () => 7), store);
    }
}
