using System;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The wallet gate B13 wires at the hangar's existing Purchase/Sell seam: a campaign flow refuses
/// an unaffordable build and an unavailable airframe, credits the wallet at the decoded full sell
/// price with the special-plane and two-plane floor refusals, and the two wallet-free doors (a
/// null <see cref="HangarFlow.Campaign"/>) never consult any of it.
/// </summary>
public class HangarCampaignContextTests : IDisposable
{
    private readonly string _planesDir;
    private readonly string _profilesDir;
    private readonly CustomPlaneStore _planes;
    private readonly CampaignProfileStore _profiles;

    public HangarCampaignContextTests()
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
        var campaign = new HangarCampaignContext(_profiles, profile, _planes);
        var flow = new HangarFlow(_planes, UiStrings.Empty, campaign: campaign);
        flow.Accept(); // New Plane
        flow.Scratch.Airframe = 5; // Devastator: availability 1, so a fresh profile clears the threshold
        flow.Scratch.Engine = 0;
        flow.Scratch.Name = "Broke Build";
        Walk(flow, HangarScreen.Purchase);

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
        var campaign = new HangarCampaignContext(_profiles, profile, _planes);
        Assert.False(campaign.IsAirframeAvailable(2)); // Balmoral, availability 3

        var flow = new HangarFlow(_planes, UiStrings.Empty, campaign: campaign);
        flow.Accept();
        flow.Scratch.Airframe = 2;
        flow.Scratch.Engine = 0;
        flow.Scratch.Name = "Too Soon";
        Walk(flow, HangarScreen.Purchase);

        Assert.False(flow.Commit());
        Assert.Contains("not available yet", flow.Message, StringComparison.Ordinal);
        Assert.Empty(_planes.List());
        Assert.Equal(1_000_000, profile.Funds);
    }

    /// <summary>Enough funds and progress: the build lands, the wallet is debited the exact total
    /// the economy priced, and the plane is now owned.</summary>
    [Fact]
    public void BuyWithFundsAndThresholdMetSucceeds()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        profile.Funds = 10_000;
        profile.MissionsCompleted = 20; // clears every airframe's threshold
        var campaign = new HangarCampaignContext(_profiles, profile, _planes);

        var flow = new HangarFlow(_planes, UiStrings.Empty, campaign: campaign);
        flow.Accept();
        flow.Scratch.Airframe = 10; // Warhawk, cheapest airframe
        flow.Scratch.Engine = 0;
        flow.Scratch.Name = "Paid For";
        Walk(flow, HangarScreen.Purchase);
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
        var campaign = new HangarCampaignContext(_profiles, profile, _planes);

        var flow = new HangarFlow(_planes, UiStrings.Empty, campaign: campaign);
        flow.Accept();
        flow.Scratch.Airframe = 10;
        flow.Scratch.Engine = 0;
        flow.Scratch.Name = "Round Trip";
        Walk(flow, HangarScreen.Purchase);
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
        var campaign = new HangarCampaignContext(_profiles, profile, _planes);

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
        var campaign = new HangarCampaignContext(_profiles, profile, _planes);

        Assert.True(campaign.IsSpecial("Jumping Jane"));
        Assert.False(campaign.CanSell("Jumping Jane"));
        Assert.False(campaign.Sell("Jumping Jane"));
        Assert.Equal(4, profile.Planes.Count); // the two starters plus Jumping Jane plus Extra
    }

    /// <summary>The two existing doors (IA Build, top-level entry) pass no campaign context, so
    /// funds and threshold are never consulted: an otherwise-legal build commits exactly as
    /// It remains available on a plane whose profile funds are $0.</summary>
    [Fact]
    public void InstantActionHangarStaysWalletFree()
    {
        var flow = new HangarFlow(_planes, UiStrings.Empty); // no campaign argument at all
        Assert.Null(flow.Campaign);
        flow.Accept();
        flow.Scratch.Airframe = 2; // Balmoral: campaign-gated above, unrestricted here
        flow.Scratch.Engine = 0;
        flow.Scratch.Name = "IA Balmoral";
        Walk(flow, HangarScreen.Purchase);

        Assert.True(flow.Commit());
        Assert.Equal(HangarExit.Built, flow.Exit);
    }

    private static void Walk(HangarFlow flow, HangarScreen target)
    {
        for (int guard = 0; flow.Screen != target && guard < HangarFlow.Order.Length + 3; guard++)
        {
            if (flow.DefaultsAsk != null)
            {
                flow.AnswerDefaultsAsk(false);
                continue;
            }

            if (flow.Screen == HangarScreen.Airframe)
            {
                flow.FocusRow(flow.Scratch.Airframe);
            }

            flow.Accept();
        }

        Assert.Equal(target, flow.Screen);
    }
}
