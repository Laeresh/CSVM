using System;
using System.IO;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Mech3;
using CSVM.Session.Campaign;
using CSVM.UI.Menu;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The wallet gate B13 wires at the hangar's Purchase/Sell seam. A campaign build refuses an
/// unaffordable build, an unavailable airframe and a purchase past the decoded slot cap. A sale
/// credits the decoded full price, with the special-plane and two-plane floor refusals. A
/// wallet-free <see cref="HangarFeature"/> never consults any of it.
/// </summary>
public class CampaignWalletTests : IDisposable
{
    private readonly string _planesDir;
    private readonly string _profilesDir;
    private readonly CustomPlaneStore _planes;
    private readonly CampaignProfileStore _profiles;

    public CampaignWalletTests()
    {
        _planesDir = Path.Combine(Path.GetTempPath(), "csvm-campaign-planes-" + Guid.NewGuid().ToString("N"));
        _profilesDir = Path.Combine(Path.GetTempPath(), "csvm-campaign-profiles-" + Guid.NewGuid().ToString("N"));
        _planes = new CustomPlaneStore(_planesDir);
        _profiles = new CampaignProfileStore(_profilesDir);
    }

    public void Dispose()
    {
        foreach (var dir in new[] { _planesDir, _profilesDir })
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>A fresh profile is $0 (docs/org/hangar.md "starting funds are $0"): the cheapest
    /// legal build (a bare, engineless-but-fixed airframe with an engine) is still refused.</summary>
    [Fact]
    public void BuyIsRefusedUnderFunds()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        var campaign = new CampaignWallet(_profiles, profile, _planes);
        var flow = Open(campaign);
        flow.StartNewPlane();
        flow.Scratch.Airframe = 5; // Devastator: availability 1, so a fresh profile clears the threshold
        flow.Scratch.Engine = 0;
        flow.Scratch.Name = "Broke Build";

        Assert.False(flow.Commit());
        Assert.Contains("INSUFFICIENT FUNDS", flow.Message, StringComparison.Ordinal);
        Assert.Empty(_planes.List());
        Assert.Equal(0, profile.Funds);
    }

    /// <summary>The 11-airframe threshold field, wired here: a fresh
    /// profile's progress (0 missions + 1) is below the Balmoral's availability (3), so it is
    /// refused even with unlimited funds.</summary>
    [Fact]
    public void BuyIsRefusedUnderThreshold()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        profile.Funds = 1_000_000;
        var campaign = new CampaignWallet(_profiles, profile, _planes);
        Assert.False(campaign.IsAirframeAvailable(2)); // Balmoral, availability 3

        var flow = Open(campaign);
        flow.StartNewPlane();
        flow.Scratch.Airframe = 2;
        flow.Scratch.Engine = 0;
        flow.Scratch.Name = "Too Soon";

        Assert.False(flow.Commit());
        Assert.Contains("not available yet", flow.Message, StringComparison.Ordinal);
        Assert.Empty(_planes.List());
        Assert.Equal(1_000_000, profile.Funds);
    }

    /// <summary>The decoded slot cap: a profile already holding its 20 bought planes is refused the
    /// next purchase in the original's own words (langui 204), with the wallet, the ownership list
    /// and the build store all untouched.</summary>
    [Fact]
    public void BuyIsRefusedAtTheSlotCap()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        profile.Funds = 1_000_000;
        profile.MissionsCompleted = 20;
        FillBought(profile, CampaignWallet.PurchasedPlaneCap);

        var flow = CampaignFlow(profile);
        flow.StartNewPlane();
        flow.Scratch.Airframe = 10;
        flow.Scratch.Engine = 0;
        flow.Scratch.Name = "One Too Many";

        Assert.False(flow.Commit());
        Assert.Contains("hangar limit", flow.Message, StringComparison.Ordinal);
        Assert.Equal(1_000_000, profile.Funds);
        Assert.Equal(CampaignWallet.PurchasedPlaneCap, profile.Planes.Count);
        Assert.Null(_planes.Load("One Too Many"));
    }

    /// <summary>The five reward aircraft sit outside the cap, as the original's own arithmetic has
    /// them: 19 bought planes plus all five awards still leaves the slot for a twentieth purchase,
    /// which fills the 25-record array exactly and is the last one the profile can make.</summary>
    [Fact]
    public void RewardAircraftDoNotCountAgainstTheSlotCap()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        profile.Funds = 1_000_000;
        FillBought(profile, CampaignWallet.PurchasedPlaneCap - 1);
        for (int i = 0; i < 5; i++)
        {
            profile.Planes.Add(new OwnedPlane { Name = "Award " + i, Airframe = 2, Special = true });
        }

        var campaign = new CampaignWallet(_profiles, profile, _planes);
        Assert.Equal(24, profile.Planes.Count);
        Assert.True(campaign.HasFreeSlot);

        campaign.Purchase("Last One", 10, 500);

        Assert.Equal(25, profile.Planes.Count);
        Assert.False(campaign.HasFreeSlot);
    }

    /// <summary>Enough funds and progress: the build lands, the wallet is debited the exact total
    /// the economy priced, and the plane is now owned.</summary>
    [Fact]
    public void BuyWithFundsAndThresholdMetSucceeds()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        profile.Funds = 10_000;
        profile.MissionsCompleted = 20; // clears every airframe's threshold
        var campaign = new CampaignWallet(_profiles, profile, _planes);

        var flow = Open(campaign);
        flow.StartNewPlane();
        flow.Scratch.Airframe = 10; // Warhawk, cheapest airframe
        flow.Scratch.Engine = 0;
        flow.Scratch.Name = "Paid For";
        int cost = HangarEconomy.Price(flow.Scratch).Total.Cost;

        Assert.True(flow.Commit());
        Assert.Equal(10_000 - cost, profile.Funds);
        Assert.Contains(profile.Planes, p => p.Name == "Paid For" && p.Airframe == 10);
        Assert.NotNull(_planes.Load("Paid For"));
    }

    /// <summary>Selling credits exactly the decoded full build cost (no depreciation,
    /// docs/org/hangar.md "The sell price is the full build cost"): buying then selling the same
    /// build leaves the wallet where it started.</summary>
    [Fact]
    public void SellCreditsExactlyTheDecodedPrice()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        profile.Funds = 10_000;
        profile.MissionsCompleted = 20;
        var campaign = new CampaignWallet(_profiles, profile, _planes);

        var flow = Open(campaign);
        flow.StartNewPlane();
        flow.Scratch.Airframe = 10;
        flow.Scratch.Engine = 0;
        flow.Scratch.Name = "Round Trip";
        flow.Commit();
        int fundsAfterBuy = profile.Funds;
        int price = campaign.SellPrice("Round Trip");

        // A third plane so the two-plane floor does not refuse this sale.
        Assert.True(campaign.Sell("Round Trip"));

        Assert.Equal(fundsAfterBuy + price, profile.Funds);
        Assert.Equal(10_000, profile.Funds); // the exact round trip: no loss, no gain
        Assert.DoesNotContain(profile.Planes, p => p.Name == "Round Trip");
        Assert.Null(_planes.Load("Round Trip"));
    }

    /// <summary>A fresh profile owns exactly the two starters: selling either would drop below the
    /// decoded floor (docs/org/hangar.md, langui 701), so both are refused.</summary>
    [Fact]
    public void SellIsRefusedAtTheTwoPlaneFloor()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        var campaign = new CampaignWallet(_profiles, profile, _planes);

        Assert.False(campaign.CanSell("Gypsy Magic"));
        Assert.False(campaign.Sell("Gypsy Magic"));
        Assert.Equal(0, profile.Funds);
        Assert.Equal(2, profile.Planes.Count);
    }

    /// <summary>A reward aircraft (docs/org/hangar.md, the mission reward table; the record's
    /// Special flag) cannot be sold at all, even with plenty of planes to spare.</summary>
    [Fact]
    public void SpecialPlanesCannotBeSold()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        profile.Planes.Add(new OwnedPlane { Name = "Jumping Jane", Airframe = 2, Special = true });
        profile.Planes.Add(new OwnedPlane { Name = "Extra", Airframe = 10 });
        var campaign = new CampaignWallet(_profiles, profile, _planes);

        Assert.True(campaign.IsSpecial("Jumping Jane"));
        Assert.False(campaign.CanSell("Jumping Jane"));
        Assert.False(campaign.Sell("Jumping Jane"));
        Assert.Equal(4, profile.Planes.Count); // the two starters plus Jumping Jane plus Extra
    }

    /// <summary>A wallet-free door passes no campaign context, so funds and threshold are never
    /// consulted: an otherwise-legal build on a campaign-gated airframe commits.</summary>
    [Fact]
    public void InstantActionHangarStaysWalletFree()
    {
        var flow = Open(null);
        Assert.Null(flow.Wallet);
        flow.StartNewPlane();
        flow.Scratch.Airframe = 2; // Balmoral: campaign-gated above, unrestricted here
        flow.Scratch.Engine = 0;
        flow.Scratch.Name = "IA Balmoral";

        Assert.True(flow.Commit());
        Assert.Equal("IA Balmoral", flow.BuiltPlaneName);
    }

    /// <summary>A fresh profile owns exactly the two seeded Devastators (docs/org/hangar.md, "The
    /// campaign instead starts with two aircraft"), and neither was ever hangar-built, so the
    /// roster has to resolve them from the ownership records themselves.</summary>
    [Fact]
    public void AFreshProfileListsExactlyItsTwoStarters()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        var flow = CampaignFlow(profile);

        Assert.Equal(new[] { "Gypsy Magic", "The Knave" }, flow.Saved.Select(p => p.Name));
        Assert.All(flow.Saved, p => Assert.Equal(5, p.Airframe));
    }

    /// <summary>Ownership separates the two modes, not storage: an Instant Action build sits in
    /// the same user://Planes/ directory and the campaign roster does not list it, while Instant
    /// Action's own door still does.</summary>
    [Fact]
    public void AnInstantActionBuildDoesNotAppearInTheCampaignList()
    {
        _planes.Save(new CustomPlaneDef { Name = "IA Kestrel", Airframe = 8, Engine = 0 });
        var profile = CampaignProfileDef.NewProfile("Zachary");

        Assert.DoesNotContain(CampaignFlow(profile).Saved, p => p.Name == "IA Kestrel");
        Assert.Contains(Open(null).Saved, p => p.Name == "IA Kestrel");
    }

    /// <summary>A purchase debits the wallet, names the build into the profile and shows up in the
    /// campaign roster the next time the door opens.</summary>
    [Fact]
    public void APurchaseAddsTheBuildToTheProfileListAndDebitsTheWallet()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        profile.Funds = 10_000;
        profile.MissionsCompleted = 20;
        var flow = CampaignFlow(profile);

        flow.StartNewPlane();
        flow.Scratch.Airframe = 10;
        flow.Scratch.Engine = 0;
        flow.Scratch.Name = "Bought";
        int cost = HangarEconomy.Price(flow.Scratch).Total.Cost;
        Assert.True(flow.Commit());

        Assert.Equal(10_000 - cost, profile.Funds);
        Assert.Contains(profile.Planes, p => p.Name == "Bought" && p.Airframe == 10);
        Assert.Equal(
            new[] { "Gypsy Magic", "The Knave", "Bought" },
            CampaignFlow(profile).Saved.Select(p => p.Name));
    }

    /// <summary>A campaign deletion is a sale at the decoded full build cost. It drops the plane from
    /// the profile and the build store together.</summary>
    [Fact]
    public void ADeletionSellsThePlaneAndCreditsTheFullBuildCost()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        profile.Planes.Add(new OwnedPlane { Name = "Spare", Airframe = 10 });
        _planes.Save(new CustomPlaneDef { Name = "Spare", Airframe = 10, Engine = 0 });
        var campaign = new CampaignWallet(_profiles, profile, _planes);
        var flow = Open(campaign);
        int price = campaign.SellPrice("Spare");
        Assert.True(price > 0);

        Assert.True(flow.DeleteSaved("Spare"));

        Assert.Equal(price, profile.Funds);
        Assert.DoesNotContain(profile.Planes, p => p.Name == "Spare");
        Assert.Null(_planes.Load("Spare"));
        Assert.Equal(new[] { "Gypsy Magic", "The Knave" }, flow.Saved.Select(p => p.Name));
    }

    /// <summary>A reward aircraft's sale is refused in the original's words (docs/org/hangar.md,
    /// langui 704).</summary>
    [Fact]
    public void ADeletionRefusesARewardAircraft()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        profile.Planes.Add(new OwnedPlane { Name = "Jumping Jane", Airframe = 2, Special = true });
        profile.Planes.Add(new OwnedPlane { Name = "Spare", Airframe = 10 });
        var flow = CampaignFlow(profile);

        Assert.False(flow.DeleteSaved("Jumping Jane"));

        Assert.Contains("cannot be sold", flow.Message, StringComparison.Ordinal);
        Assert.Equal(0, profile.Funds);
        Assert.Equal(4, profile.Planes.Count);
    }

    /// <summary>The two-aircraft floor holds on a deletion too: a fresh profile's starters are
    /// refused and the wallet never moves (langui 701).</summary>
    [Fact]
    public void ADeletionHoldsTheTwoAircraftFloor()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        var flow = CampaignFlow(profile);

        Assert.False(flow.DeleteSaved("Gypsy Magic"));

        Assert.Contains("at least two planes", flow.Message, StringComparison.Ordinal);
        Assert.Equal(0, profile.Funds);
        Assert.Equal(2, profile.Planes.Count);
    }

    /// <summary>A campaign build cannot silently overwrite an Instant Action build: the taken-name
    /// check runs over the whole build directory, not over the visible campaign roster.</summary>
    [Fact]
    public void ACampaignFlowSeesInstantActionNamesAsTaken()
    {
        _planes.Save(new CustomPlaneDef { Name = "IA Kestrel", Airframe = 8, Engine = 0 });
        var flow = CampaignFlow(CampaignProfileDef.NewProfile("Zachary"));

        Assert.DoesNotContain(flow.Saved, p => p.Name == "IA Kestrel");
        Assert.True(flow.IsNameTaken("IA Kestrel"));
        Assert.True(flow.IsNameTaken("Gypsy Magic"));
        Assert.False(flow.IsNameTaken("Nothing Named This"));
    }

    /// <summary>Rebuilding under a name the profile already owns leaves one ownership record: the
    /// commit writes one file per name, so a second record would name one aeroplane twice and a
    /// later sale would remove both.</summary>
    [Fact]
    public void RebuildingAnOwnedNameKeepsOneOwnershipRecord()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        profile.Funds = 10_000;
        var campaign = new CampaignWallet(_profiles, profile, _planes);

        campaign.Purchase("Gypsy Magic", 7, 500);

        Assert.Equal(2, profile.Planes.Count);
        Assert.Equal(7, profile.Planes[0].Airframe);
        Assert.Equal(9_500, profile.Funds);
    }

    /// <summary>A wallet-free deletion removes the file and credits nothing.</summary>
    [Fact]
    public void AWalletFreeDeletionRemovesTheFile()
    {
        _planes.Save(new CustomPlaneDef { Name = "IA Kestrel", Airframe = 8, Engine = 0 });
        var flow = Open(null);

        Assert.True(flow.DeleteSaved("IA Kestrel"));
        Assert.Null(_planes.Load("IA Kestrel"));
        Assert.Empty(flow.Saved);
    }

    // Ownership records up to a wanted total, the two seeded starters counting towards it.
    private static void FillBought(CampaignProfileDef profile, int total)
    {
        for (int i = profile.Planes.Count; i < total; i++)
        {
            profile.Planes.Add(new OwnedPlane { Name = "Bought " + i, Airframe = 10 });
        }
    }

    // A feature opened over the scratch store, funded by the wallet or wallet-free under null.
    private HangarFeature Open(CampaignWallet? campaign)
    {
        var feature = new HangarFeature(UiStrings.Empty, StockAirframes.Node);
        feature.Open(_planes, campaign);
        return feature;
    }

    private HangarFeature CampaignFlow(CampaignProfileDef profile) => Open(new CampaignWallet(_profiles, profile, _planes));
}
