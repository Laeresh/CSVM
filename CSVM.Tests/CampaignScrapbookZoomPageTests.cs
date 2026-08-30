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
        Assert.Equal(1, flow.Page.RowCount); // no file to export, so RETURN alone
    }

    /// <summary>EXPORT TO DESKTOP copies the scrap's own file, byte for byte, under its own name,
    /// and says so in langui 705's words.</summary>
    [Fact]
    public void ExportCopiesTheScrapToTheDesktopAndSaysSo()
    {
        var flow = OpenedOnZoom();
        string desktop = Path.Combine(TestData.TempDir(), "Desktop");
        Directory.CreateDirectory(desktop);
        string art = Path.Combine(
            flow.DataRoot!, "extracted", "rof", "ASSETS", "GRAPHICS", "SCRAPBOOK");
        Directory.CreateDirectory(art);
        File.WriteAllBytes(Path.Combine(art, "SB_01_01_test.JPG"), new byte[] { 1, 2, 3 });

        var page = new CampaignScrapbookZoomPage(flow, desktop);
        Assert.Equal(2, page.RowCount);
        Assert.True(page.Accept(CampaignScrapbookZoomPage.ExportRow));

        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(desktop, "SB_01_01_test.JPG")));
        Assert.Contains("SB_01_01_test.JPG", flow.Message);
        Assert.Contains("desktop", flow.Message);
    }

    /// <summary>A scrap with no file behind it offers no EXPORT row at all, which is the original
    /// deactivating the button for a zoom with no inset image.</summary>
    [Fact]
    public void ExportIsNotOfferedForAScrapWithNoFile()
    {
        var flow = OpenedOnZoom();

        Assert.Equal(1, flow.Page.RowCount); // the fixture writes no art beside its CSV
        Assert.False(flow.Page.Accept(CampaignScrapbookZoomPage.ExportRow));
    }

    private static CampaignFlow OpenedOnZoom()
    {
        var flow = FlowOnScrapbook();
        flow.FocusRow(flow.Page.RowCount - 1); // the one openable scrap, after every button
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
