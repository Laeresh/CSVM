using System.IO;

namespace CSVM.Tests;

/// <summary>Hand-authored <c>SCRAPBOOK.CSV</c>/<c>LAYOUT.CSV</c> fixtures shared by the scrapbook
/// page tests, never a copy of the shipped files (<c>TestData</c>'s own rule).</summary>
public static class ScrapbookCompositionFixture
{
    /// <summary>Writes a fresh temp <c>dataRoot</c> holding one openable scrap at
    /// <c>&lt;mission&gt;_1_1</c> (always visible, family <c>M</c>, title
    /// <c>IDS_TEST_TITLE</c>, body <c>IDS_TEST_BODY</c>) and family <c>M</c>'s three
    /// <c>LAYOUT.CSV</c> text boxes.</summary>
    public static string WriteMinimalOpenableScrap(string root, int mission)
    {
        string dir = Path.Combine(root, "extracted", "rof", "ASSETS");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SCRAPBOOK.CSV"),
            "[SCRAPBOOK]\n" +
            $"{mission}_1_1=0,0,SB_{mission:00}_01_test,PJ,10,20,1,0,0,5,\"0,0,0,0\",M,30,40,IDS_TEST_TITLE,IDS_TEST_BODY\n");
        File.WriteAllText(Path.Combine(dir, "LAYOUT.CSV"),
            "SBZ_T_TITLEM    =T,!,60,25,0,525,85,0xff000000,0\n" +
            "SBZ_T_CAPTIONM  =T,!,0,0,0,700,550,0xff000000,0\n" +
            "SBZ_T_TEXTM     =T,!,60,113,0,525,487,0xff000000,0\n");
        return root;
    }

    /// <summary>Writes a fresh temp <c>dataRoot</c> holding a three-mission book: mission 1 carries
    /// two spreads, missions 2 and 3 carry one each, so the book runs (1,1) -&gt; (1,2) -&gt; (2,1)
    /// -&gt; (3,1) with no spread beyond it -- enough to exercise both the within-mission and
    /// roll-to-the-next/previous-mission steps (D20).</summary>
    public static string WriteBook(string root)
    {
        string dir = Path.Combine(root, "extracted", "rof", "ASSETS");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SCRAPBOOK.CSV"),
            "[SCRAPBOOK]\n" +
            "1_1_1=0,0,SB_01_01_a,P0,10,10,1,0,0,10,\"0,0,0,0\",0,0,0,0,0\n" +
            "1_2_1=0,0,SB_01_02_a,P0,10,10,1,0,0,10,\"0,0,0,0\",0,0,0,0,0\n" +
            "2_1_1=0,0,SB_02_01_a,P0,10,10,1,0,0,10,\"0,0,0,0\",0,0,0,0,0\n" +
            "3_1_1=0,0,SB_03_01_a,P0,10,10,1,0,0,10,\"0,0,0,0\",0,0,0,0,0\n");
        return root;
    }

    /// <summary>Writes a fresh temp <c>dataRoot</c> holding one mission's danger-zone slot: a
    /// <c>DZ_generic_corners</c> photo-corner mount (<c>Zoom=0</c>, never opens) painted one step
    /// above a <c>Snap_</c> capture at the same coordinates, matching
    /// <c>docs/formats/campaign-screens.md</c>'s "The danger-zone slot" (D21). The capture's own
    /// file is never written here -- callers drop it into a profile directory to exercise
    /// <c>CampaignScrapbookPage.CaptureExists</c>.</summary>
    public static string WriteDangerZoneSpread(string root, int mission)
    {
        string dir = Path.Combine(root, "extracted", "rof", "ASSETS");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SCRAPBOOK.CSV"),
            "[SCRAPBOOK]\n" +
            $"{mission}_1_1=0,,DZ_generic_corners,P0,434,377,1,0,0,50,\"0,0,0,0\",0,0,0,0,0\n" +
            $"{mission}_1_2=0,,Snap_{mission}_18,PP,434,377,0,0,0,48,\"0,0,0,0\",Q,0,0,0,0\n");
        return root;
    }
}
