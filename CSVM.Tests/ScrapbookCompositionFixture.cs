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
}
