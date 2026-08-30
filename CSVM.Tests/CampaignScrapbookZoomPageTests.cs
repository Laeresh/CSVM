using System.IO;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>One scrap's detail view, opened from a <see cref="CampaignScrapbookPage"/> row and
/// closing back to it.</summary>
public class CampaignScrapbookZoomPageTests
{
    [Fact]
    public void DrawsTheBackgroundInsetAndTextForTheTargetedScrap()
    {
        var flow = OpenedOnZoom();

        Assert.Equal("SCRAPBOOK/SB_BG_M.jpg", flow.Page.Pictures[0].Art.Name);
        Assert.Equal("SCRAPBOOK/SB_01_01_test.JPG", flow.Page.Pictures[1].Art.Name); // "PJ"'s J
        Assert.Equal(30f, flow.Page.Pictures[1].X);
        Assert.Equal(40f, flow.Page.Pictures[1].Y);
        Assert.Contains(flow.Page.Captions, l => l.Text == "IDS_TEST_TITLE");
        Assert.Contains(flow.Page.Captions, l => l.Text == "IDS_TEST_BODY");
    }

    [Fact]
    public void CloseReturnsToTheScrapbookPage()
    {
        var flow = OpenedOnZoom();

        flow.FocusRow(CampaignScrapbookZoomPage.CloseRow);
        flow.Accept();

        Assert.Equal(CampaignScreen.Scrapbook, flow.Screen);
    }

    [Fact]
    public void ANonexistentTargetDrawsNothingRatherThanThrowing()
    {
        var flow = FlowOnScrapbook();
        flow.SetScrapbookZoom(mission: 99, spread: 1, item: 1);
        flow.GoTo(CampaignScreen.ScrapbookZoom);

        Assert.Empty(flow.Page.Pictures);
        Assert.Empty(flow.Page.Captions);
    }

    private static CampaignFlow OpenedOnZoom()
    {
        var flow = FlowOnScrapbook();
        flow.FocusRow(2); // Replay, Cabin, then the one openable scrap
        flow.Accept();
        Assert.Equal(CampaignScreen.ScrapbookZoom, flow.Screen);
        return flow;
    }

    private static CampaignFlow FlowOnScrapbook()
    {
        string root = ScrapbookCompositionFixture.WriteMinimalOpenableScrap(TestData.TempDir(), mission: 1);
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, new MissionAttempt(
            0, CompletedMask: 1, TimeMs: 40000, Shots: 10, Hits: 5, Money: 0,
            Airframe: 5, PlaneName: "Gypsy Magic"));

        var dir = Path.Combine(TestData.TempDir(), "Profiles");
        Directory.CreateDirectory(dir);
        var store = new CampaignProfileStore(dir);
        store.Save(profile);
        var flow = new CampaignFlow(store, UiStrings.Empty, root);
        flow.SelectProfile(store.Load(profile.Name)!);
        flow.SetMission(0);
        flow.GoTo(CampaignScreen.Scrapbook);
        return flow;
    }
}
