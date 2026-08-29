using System.Collections.Generic;
using System.IO;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>D18: the scrapbook's per-spread scrap composition, read from <c>SCRAPBOOK.CSV</c>
/// rather than invented. Parser-level behaviour (extension resolution, the item-1-and-up
/// enumeration, the <c>Objective</c> gate, the capture skip and the draw-order stack) is exercised
/// against hand-authored fixtures, never a copy of the shipped file
/// (<c>CSVM.Tests/TestData.cs</c>'s own rule). The two claims that need the real shipped file
/// (every spread's art present, CM01's story page reproduced) are <see cref="ExtractedDataFact"/>
/// tests against the player's own extraction.</summary>
public class ScrapbookCompositionTests
{
    // Mirrors the shipped file's own shape: a [SCRAPBOOK] section, a column-header comment, and
    // rows keyed <mission>_<spread>_<item>, values comma-split with one quoted region field that
    // sits past every column this parser reads.
    private const string Fixture =
        "[SCRAPBOOK]\n" +
        ";Mission_Spread_Item=Objective,ResourceID,ImageName,ImageType,X,Y,Alpha,Width,Height,DrawOrder,\"Left,Top,Right,Bottom\",Zoom,ZoomX,ZoomY,TitleResID,TextResID\n" +
        "1_1_1=0,0,SB_01_01_coin,PJ,49,250,1,0,0,100,\"0,0,0,0\",A,0,0,0,0\n" +
        "1_1_2=0,0,SB_01_01_news3,P0,49,70,2,0,0,60,\"0,0,0,0\",B,0,0,0,0\n" +
        "1_1_3=0,0,SB_01_02_mag2,P0,135,197,2,0,0,90,\"0,0,0,0\",M,0,0,0,0\n" +
        "2_1_1=3,0,SB_02_01_gated,PJ,10,20,1,0,0,10,\"0,0,0,0\",A,0,0,0,0\n" +
        "2_1_2=-3,0,SB_02_01_notgated,PJ,30,40,1,0,0,20,\"0,0,0,0\",A,0,0,0,0\n" +
        "3_1_1=0,0,SB_03_01_bmp,B0,1,2,1,0,0,10,\"0,0,0,0\",0,0,0,0,0\n" +
        "4_1_1=0,,Snap_4_1,PP,5,6,0,0,0,10,\"0,0,0,0\",Q,0,0,0,0\n" +
        "4_1_2=0,,DZ_generic_corners,P0,5,6,1,0,0,11,\"0,0,0,0\",0,0,0,0,0\n" +
        "5_1_1=0,0,SB_05_01_jpg,J0,1,2,1,0,0,10,\"0,0,0,0\",0,0,0,0,0\n";

    [Fact]
    public void EnumeratesItemsContiguouslyFromOneAndStopsAtTheFirstGap()
    {
        string root = Root();
        var items = ScrapbookComposition.Items(root, 1, 1);

        Assert.Equal(3, items.Count);
        Assert.Equal("SB_01_01_coin", items[0].ImageName);
        Assert.Equal("SB_01_01_news3", items[1].ImageName);
        Assert.Equal("SB_01_02_mag2", items[2].ImageName);
    }

    [Fact]
    public void AnUnpopulatedSpreadIsEmptyRatherThanThrowing()
    {
        Assert.Empty(ScrapbookComposition.Items(Root(), 1, 2));
        Assert.Empty(ScrapbookComposition.Items(Root(), 99, 1));
    }

    [Fact]
    public void ANullDataRootReadsAsNoComposition()
    {
        Assert.Empty(ScrapbookComposition.Items(null, 1, 1));
        Assert.Empty(ScrapbookComposition.Pictures(null, 1, 1, bestMask: -1, scrap => true));
    }

    [Theory]
    [InlineData(1, "PNG")] // "PJ": the first letter alone picks the page extension, not the second
    [InlineData(3, "BMP")] // "B0"
    [InlineData(5, "JPG")] // "J0"
    public void TheImageTypesFirstLetterAlonePicksThePageExtension(int mission, string extension)
    {
        var items = ScrapbookComposition.Items(Root(), mission, 1);
        Assert.Contains(items, i => i.Extension == extension);
    }

    [Fact]
    public void PositionAndDrawOrderComeThroughUnchanged()
    {
        var coin = ScrapbookComposition.Items(Root(), 1, 1)[0];
        Assert.Equal(49f, coin.X);
        Assert.Equal(250f, coin.Y);
        Assert.Equal(100, coin.DrawOrder);
    }

    [Fact]
    public void PicturesStackByAscendingDrawOrderRegardlessOfItemOrder()
    {
        // Item order is coin(100), news3(60), mag2(90); stacked bottom first that is news3, mag2,
        // coin -- A1's own CM01 reading (coin over magazine over the Aloha Daily).
        var pictures = ScrapbookComposition.Pictures(Root(), 1, 1, bestMask: 1, scrap => true);

        Assert.Equal(3, pictures.Count);
        Assert.EndsWith("news3.PNG", pictures[0].Art.Name);
        Assert.EndsWith("mag2.PNG", pictures[1].Art.Name);
        Assert.EndsWith("coin.PNG", pictures[2].Art.Name);
    }

    [Fact]
    public void AnObjectiveOfZeroAlwaysDraws()
    {
        Assert.True(ScrapbookComposition.Visible(0, bestMask: 0));
    }

    [Fact]
    public void APositiveObjectiveNeedsTheMissionWonAndItsOwnBitSet()
    {
        const int primary = 1; // bit 0
        const int objective = 3;
        int bit = 1 << objective;

        Assert.False(ScrapbookComposition.Visible(objective, bestMask: 0)); // never won
        Assert.False(ScrapbookComposition.Visible(objective, bestMask: primary)); // won, bit clear
        Assert.True(ScrapbookComposition.Visible(objective, bestMask: primary | bit));
    }

    [Fact]
    public void ANegativeObjectiveNeedsTheMissionWonAndItsOwnBitClear()
    {
        const int primary = 1;
        const int objective = -3;
        int bit = 1 << 3;

        Assert.False(ScrapbookComposition.Visible(objective, bestMask: 0)); // never won
        Assert.False(ScrapbookComposition.Visible(objective, bestMask: primary | bit)); // bit set
        Assert.True(ScrapbookComposition.Visible(objective, bestMask: primary));
    }

    [Fact]
    public void ThePicturesGateFollowsVisibleRowByRow()
    {
        const int primary = 1;
        var withoutBit3 = ScrapbookComposition.Pictures(Root(), 2, 1, primary, scrap => true);
        Assert.Single(withoutBit3); // only the -3 row (bit 3 clear) draws; the +3 row's bit is unset
        Assert.Equal("SCRAPBOOK/SB_02_01_notgated.PNG", withoutBit3[0].Art.Name);

        var withBit3 = ScrapbookComposition.Pictures(Root(), 2, 1, primary | (1 << 3), scrap => true);
        Assert.Single(withBit3); // now the +3 row's gate passes and the -3 row's fails: still one
        Assert.Equal("SCRAPBOOK/SB_02_01_gated.PNG", withBit3[0].Art.Name);
    }

    [Fact]
    public void ACaptureIsSkippedWhenItsFileIsNotOnDisk()
    {
        var withoutFile = ScrapbookComposition.Pictures(Root(), 4, 1, bestMask: 1, scrap => false);
        Assert.Single(withoutFile); // just the corner mount, not the missing Snap_

        var withFile = ScrapbookComposition.Pictures(Root(), 4, 1, bestMask: 1, scrap => true);
        Assert.Equal(2, withFile.Count);
    }

    [Fact]
    public void ACaptureIsRecognisedByItsSnapPrefixAlone()
    {
        var snap = ScrapbookComposition.Items(Root(), 4, 1)[0];
        Assert.True(snap.IsCapture);
        Assert.Equal("Snap_4_1.PNG", snap.FileName);
        Assert.False(ScrapbookComposition.Items(Root(), 4, 1)[1].IsCapture); // the corner mount
    }

    // Writes the fixture into a fresh temp dataRoot's extracted/rof/ASSETS/SCRAPBOOK.CSV, the
    // path ScrapbookComposition.Items resolves against -- never the shipped file itself.
    private static string Root()
    {
        string root = TestData.TempDir();
        string dir = Path.Combine(root, "extracted", "rof", "ASSETS");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SCRAPBOOK.CSV"), Fixture);
        return root;
    }
}

/// <summary>The two claims D18's Verify step makes against the shipped install: every populated
/// spread's art is present, and CM01's story page reproduces scrap for scrap
/// (<c>OriginalScreenshots/Campaign Scrapbook CM01 Story Scraps.png</c>).</summary>
public class ScrapbookCompositionExtractedTests
{
    // Slots 1-24, spreads 1-3 (A1: 8 missions have one, 10 have two, 6 have three).
    [ExtractedDataFact]
    public void EveryPopulatedSpreadsArtIsPresentOnDisk()
    {
        string root = TestData.DataRoot!;
        string graphics = Path.Combine(root, "extracted", "rof", "ASSETS", "GRAPHICS", "SCRAPBOOK");
        int checkedFiles = 0;

        for (int mission = 0; mission <= 24; mission++)
        {
            for (int spread = 1; spread <= 3; spread++)
            {
                foreach (var scrap in ScrapbookComposition.Items(root, mission, spread))
                {
                    if (scrap.IsCapture)
                    {
                        continue; // a player capture, not shipped art (docs/org/debrief.md)
                    }

                    string path = Path.Combine(graphics, scrap.FileName);
                    Assert.True(File.Exists(path), $"missing {path} ({mission}_{spread})");
                    checkedFiles++;
                }
            }
        }

        // 461 rows total, 167 of them Snap_ captures (docs/formats/campaign-screens.md): the
        // remaining 294 are exactly A1's own "all 294 page images ... are present" count.
        Assert.Equal(294, checkedFiles);
    }

    [ExtractedDataFact]
    public void Cm01SStoryPageReproducesRows1To7()
    {
        string root = TestData.DataRoot!;
        var items = ScrapbookComposition.Items(root, mission: 1, spread: 2);

        Assert.Equal(7, items.Count);
        Assert.Equal("SB_01_01_wanted1", items[0].ImageName);
        Assert.Equal("SB_01_01_doc2", items[1].ImageName);
        Assert.Equal("SB_01_01_doc3", items[2].ImageName);
        Assert.Equal("DZ_generic_corners", items[3].ImageName);
        Assert.Equal("DZ_generic_corners", items[4].ImageName);
        Assert.Equal("Snap_1_18", items[5].ImageName);
        Assert.Equal("Snap_1_21", items[6].ImageName);
        Assert.True(items[5].IsCapture);
        Assert.True(items[6].IsCapture);

        // Every bit these rows gate on set: the three shipped scraps and both corner mounts draw;
        // the captures still do not, since CSVM saves none yet (D21's known gap, not invented).
        int mask = 1 | (1 << 1) | (1 << 3) | (1 << 12);
        var pictures = ScrapbookComposition.Pictures(root, 1, 2, mask, scrap => false);
        Assert.Equal(5, pictures.Count);
    }
}
