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

    /// <summary>A scrap's title and body are langui symbols, not text: SCRAPBOOK.CSV names them,
    /// RESRC1.H turns each into an id, and ui_strings.json holds the words. The zoom view draws the
    /// words, and a symbol no header carries still degrades to itself.</summary>
    [Fact]
    public void AScrapsOwnWordsResolveThroughResrc1HIntoTheStringTable()
    {
        string root = ScrapbookCompositionFixture.WriteResolvableScrap(TestData.TempDir(), mission: 1);
        var strings = UiStrings.Parse(
            "[{\"id\":40002,\"text\":\"[FREE18]\\nMy Dearest Nathan,\",\"dll\":\"langui\"}," +
            "{\"id\":40003,\"text\":\"Your princess, Loni Ne\",\"dll\":\"langui\"}]");
        var flow = OpenedOnZoom(root, strings);

        Assert.Contains(flow.Page.Captions, l => l.Text == "\nMy Dearest Nathan,");
        Assert.Contains(flow.Page.Captions, l => l.Text == "Your princess, Loni Ne");
        Assert.DoesNotContain(flow.Page.Captions, l => l.Text == "IDS_TEST_TITLE");
    }

    /// <summary>An ImageType whose second letter is 0 names no inset: the zoom is the family
    /// background and the words alone, with nothing for EXPORT TO DESKTOP to copy.</summary>
    [Fact]
    public void ATextScrapDrawsNoInsetAndOffersNoExport()
    {
        string root = ScrapbookCompositionFixture.WriteMinimalOpenableScrap(TestData.TempDir(), mission: 1, imageType: "P0");
        string art = Path.Combine(root, "extracted", "rof", "ASSETS", "GRAPHICS", "SCRAPBOOK");
        Directory.CreateDirectory(art);
        File.WriteAllBytes(Path.Combine(art, "SB_01_01_test.PNG"), new byte[] { 1 });
        var flow = OpenedOnZoom(root);

        var picture = Assert.Single(flow.Page.Pictures);
        Assert.Equal("SCRAPBOOK/SB_BG_M.jpg", picture.Art.Name);
        Assert.Equal(1, flow.Page.RowCount);
    }

    /// <summary>A scrap's words take the face their langui row's [FONTID] names, at that face's
    /// pixel size and pitch, in the colour and justification of the family's box row.</summary>
    [Fact]
    public void AScrapsWordsTakeTheirRowsFaceAndTheirBoxsColourAndJustification()
    {
        string root = ScrapbookCompositionFixture.WriteResolvableScrap(TestData.TempDir(), mission: 1);
        File.WriteAllText(Path.Combine(root, "extracted", "rof", "ASSETS", "SCRAPBOOK.CSV"),
            "[SCRAPBOOK]\n" +
            "1_1_1=0,0,SB_01_01_test,P0,10,20,1,0,0,5,\"0,0,0,0\",B,0,0,IDS_TEST_TITLE,IDS_TEST_BODY\n");
        var strings = UiStrings.Parse(
            "[{\"id\":40002,\"text\":\"ZACHARY NABS <B>BALMORAL<b>!\",\"font\":\"[IMP36]\",\"dll\":\"langui\"}," +
            "{\"id\":40003,\"text\":\"[TNR14]\\nNotorious corsair\",\"dll\":\"langui\"}]");
        var flow = OpenedOnZoom(root, strings, CampaignLayout.Over(MenuLayoutReaderTests.OriginalLayout()));

        var title = Assert.Single(flow.Page.Captions, l => l.Text == "ZACHARY NABS BALMORAL!");
        Assert.Equal("Impact", title.Face?.Family);
        Assert.Equal(48f, title.Size);
        Assert.Equal(48f, title.Leading);
        Assert.Equal(BoardJustify.Center, title.Justify); // SBZ_T_TITLEB's Justify 1
        Assert.Equal(new BoardTint(0, 0, 0), title.Colour);

        var body = Assert.Single(flow.Page.Captions, l => l.Text == "\nNotorious corsair");
        Assert.Equal("Times New Roman", body.Face?.Family);
        Assert.Equal(BoardJustify.Left, body.Justify); // SBZ_T_TEXTB's Justify 4, no reading of its own
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

    private static CampaignFlow OpenedOnZoom(
        string? root = null, UiStrings? strings = null, CampaignLayout? layout = null)
    {
        var flow = FlowOnScrapbook(root, strings, layout);
        flow.FocusRow(flow.Page.RowCount - 1); // the one openable scrap, after every button
        flow.Accept();
        Assert.Equal(CampaignScreen.ScrapbookZoom, flow.Screen);
        return flow;
    }

    private static CampaignFlow FlowOnScrapbook(
        string? root = null, UiStrings? strings = null, CampaignLayout? layout = null)
    {
        root ??= ScrapbookCompositionFixture.WriteMinimalOpenableScrap(TestData.TempDir(), mission: 1);
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, new MissionAttempt(
            0, CompletedMask: 1, TimeMs: 40000, Shots: 10, Hits: 5,
            Airframe: 5, PlaneName: "Gypsy Magic"));

        var dir = Path.Combine(TestData.TempDir(), "Profiles");
        Directory.CreateDirectory(dir);
        var store = new CampaignProfileStore(dir);
        store.Save(profile);
        var flow = new CampaignFlow(store, strings ?? UiStrings.Empty, root, layout: layout);
        flow.SelectProfile(store.Load(profile.Name)!);
        flow.SetMission(0);
        flow.GoTo(CampaignScreen.Scrapbook);
        return flow;
    }
}
