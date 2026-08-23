using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The PAINT screen (PLAN-hangar C25, reworked onto the original's own model in E43): the pattern
/// row stepping only the patterns this airframe's availability mask allows and loading that
/// entry's six colour/shade defaults, a colour row and a shade row per slot walking the 27-row
/// swatch table and its ramps, three decal rows over the 00-49 texture set, and the live preview
/// composed through the original's region masks (docs/formats/paint.md).
/// </summary>
public class HangarPaintPageTests : IDisposable
{
    private readonly string _dir;
    private readonly CustomPlaneStore _store;

    public HangarPaintPageTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "csvm-paint-" + Guid.NewGuid().ToString("N"));
        _store = new CustomPlaneStore(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Ten rows: the pattern, then each slot's colour and shade together, then the three
    /// decals in the dropdowns' own nose/tail/wing order.</summary>
    [Fact]
    public void OffersThePatternEachSlotsPairAndTheThreeDecals()
    {
        var flow = OpenOnPaint();

        Assert.Equal(10, flow.Page.RowCount);
        Assert.StartsWith("Pattern:", flow.Page.RowText(0), StringComparison.Ordinal);
        Assert.StartsWith("Paint 1 (body):", flow.Page.RowText(1), StringComparison.Ordinal);
        Assert.StartsWith("Shade 1:", flow.Page.RowText(2), StringComparison.Ordinal);
        Assert.StartsWith("Paint 2 (dark trim):", flow.Page.RowText(3), StringComparison.Ordinal);
        Assert.StartsWith("Shade 2:", flow.Page.RowText(4), StringComparison.Ordinal);
        Assert.StartsWith("Paint 3 (light trim):", flow.Page.RowText(5), StringComparison.Ordinal);
        Assert.StartsWith("Shade 3:", flow.Page.RowText(6), StringComparison.Ordinal);
        Assert.StartsWith("Nose decal:", flow.Page.RowText(7), StringComparison.Ordinal);
        Assert.StartsWith("Tail decal:", flow.Page.RowText(8), StringComparison.Ordinal);
        Assert.StartsWith("Wing decal:", flow.Page.RowText(9), StringComparison.Ordinal);
    }

    /// <summary>The pattern row is the airframe's availability mask, not the whole table: the
    /// Hoplite (airframe 0) wears exactly blackhat, fortune, studio and itstaxi.</summary>
    [Fact]
    public void ThePatternRowStepsOnlyThisAirframesPatterns()
    {
        var flow = OpenOnPaint();
        var seen = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            flow.Step(1);
            seen.Add(flow.Scratch.PaintPattern);
        }

        Assert.Equal(new[] { 4, 11, 13, 0 }, seen);
    }

    /// <summary>The Fury's four (blckswan, fortune, hughes, studio) are the four paint.md's
    /// per-aircraft table lists, read here off the masks rather than off an extraction.</summary>
    [Fact]
    public void TheFuryOffersItsOwnFour()
    {
        var flow = OpenOnPaint();
        flow.Scratch.Airframe = 7;
        var seen = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            flow.Step(1);
            seen.Add(flow.Scratch.PaintPattern);
        }

        Assert.Equal(new[] { 1, 4, 6, 11, }, seen);
    }

    /// <summary>Selecting a pattern copies its six colour/shade defaults over the plane's own,
    /// which is all the original's SET handler does. fortune's resolve to red, the white ramp's
    /// darkest shade, and white: player_fortune's triple, reached through the tables.</summary>
    [Fact]
    public void PickingAPatternLoadsItsSixDefaults()
    {
        var flow = OpenOnPaint();

        Assert.True(flow.Step(1)); // blackhat -> fortune

        Assert.Equal(4, flow.Scratch.PaintPattern);
        Assert.Equal(new[] { 1, 26, 26 }, flow.Scratch.PaintColours);
        Assert.Equal(new[] { 8, 0, 9 }, flow.Scratch.PaintShades);
        Assert.Equal(new PaintColour(223, 0, 41), flow.Scratch.Colour1);
        Assert.Equal(new PaintColour(25, 25, 25), flow.Scratch.Colour2);
        Assert.Equal(new PaintColour(255, 255, 255), flow.Scratch.Colour3);
    }

    /// <summary>blackhat's defaults resolve to its shipped scheme triple exactly, which is the
    /// pattern table and the swatch table checked against vehicle.json.</summary>
    [Fact]
    public void BlackhatsDefaultsResolveToItsShippedTriple()
    {
        var flow = OpenOnPaint();
        flow.Scratch.LoadPatternDefaults(0);

        Assert.Equal(new PaintColour(177, 130, 66), flow.Scratch.Colour1);
        Assert.Equal(new PaintColour(119, 74, 43), flow.Scratch.Colour2);
        Assert.Equal(new PaintColour(66, 39, 15), flow.Scratch.Colour3);
    }

    /// <summary>A colour row walks the 27 swatch rows, and picking one resets that slot's shade to
    /// the row's own default: the original's reset at 0x0040d402, which is why a colour pick lands
    /// on the artist's brightness rather than keeping the last one.</summary>
    [Fact]
    public void AColourPickResetsItsSlotsShade()
    {
        var flow = OpenOnPaint();
        flow.Scratch.LoadPatternDefaults(4);
        flow.Move(1);

        Assert.True(flow.Step(1)); // swatch 1 -> 2

        Assert.Equal(2, flow.Scratch.PaintColours[0]);
        Assert.Equal(8, flow.Scratch.PaintShades[0]); // swatch 2's own default
        Assert.Equal(new PaintColour(227, 59, 34), flow.Scratch.Colour1);
        Assert.Equal(26, flow.Scratch.PaintColours[1]); // the other slots stand
    }

    /// <summary>A shade row walks its colour's own dark-to-light ramp and nothing else.</summary>
    [Fact]
    public void AShadeRowWalksItsColoursOwnRamp()
    {
        var flow = OpenOnPaint();
        flow.Scratch.LoadPatternDefaults(4);
        flow.Move(2);

        Assert.True(flow.Step(-1)); // slot 1's red, one shade darker

        Assert.Equal(1, flow.Scratch.PaintColours[0]);
        Assert.Equal(7, flow.Scratch.PaintShades[0]);
        Assert.Equal(new PaintColour(204, 0, 41), flow.Scratch.Colour1);
    }

    /// <summary>Ramps differ in length, so a shade cycle is the row's own: swatch 26's is ten long
    /// and wraps from its darkest to its lightest.</summary>
    [Fact]
    public void ShadeSteppingWrapsWithinTheRow()
    {
        var flow = OpenOnPaint();
        flow.Scratch.LoadPatternDefaults(4);
        flow.Move(4); // slot 2's shade: swatch 26, shade 0

        Assert.True(flow.Step(-1));
        Assert.Equal(9, flow.Scratch.PaintShades[1]);
        Assert.Equal(new PaintColour(255, 255, 255), flow.Scratch.Colour2);
    }

    /// <summary>A fresh build's decal slots keep the shipped placeholder, which the original
    /// cannot store; the first step enters the 0-49 set and the cycle stays inside it.</summary>
    [Fact]
    public void DecalRowsStepTheFiftyTextureSet()
    {
        var flow = OpenOnPaint();

        Assert.Equal(-1, flow.Scratch.NoseDecal);
        Assert.Contains("placeholder", flow.Page.RowText(7), StringComparison.Ordinal);

        flow.Move(7);
        Assert.True(flow.Step(-1));
        Assert.Equal(49, flow.Scratch.NoseDecal);
        Assert.Equal("Nose decal: 49Ace_Star2", flow.Page.RowText(7));

        Assert.True(flow.Step(1));
        Assert.Equal(0, flow.Scratch.NoseDecal);
        Assert.Equal("Nose decal: 00BBomber_logo1", flow.Page.RowText(7));
    }

    /// <summary>Each decal row edits its own slot.</summary>
    [Fact]
    public void EachDecalRowEditsItsOwnSlot()
    {
        var flow = OpenOnPaint();
        flow.Page.Step(8, 1);

        Assert.Equal(-1, flow.Scratch.NoseDecal);
        Assert.Equal(0, flow.Scratch.TailDecal);
        Assert.Equal(-1, flow.Scratch.WingDecal);
    }

    /// <summary>The pattern label is the dropdown's own langui string (3425 + index), with the
    /// internal name standing in when the table is missing.</summary>
    [Fact]
    public void ThePatternLabelIsLangui3425PlusTheIndex()
    {
        Assert.Equal("Pattern: BLACKHAT", OpenOnPaint().Page.RowText(0));

        var strings = UiStrings.Parse("[{\"id\":3425,\"text\":\"Black Hat Brigade\",\"dll\":\"langui\"}]");
        Assert.Equal("Pattern: Black Hat Brigade", OpenOnPaint(strings: strings).Page.RowText(0));
    }

    /// <summary>Confirm advances to the name screen without editing the paint.</summary>
    [Fact]
    public void AcceptAdvancesWithoutEditing()
    {
        var flow = OpenOnPaint();
        flow.Scratch.LoadPatternDefaults(4);
        flow.Accept();

        Assert.Equal(HangarScreen.Name, flow.Screen);
        Assert.Equal(4, flow.Scratch.PaintPattern);
        Assert.Equal(new PaintColour(223, 0, 41), flow.Scratch.Colour1);
    }

    /// <summary>No data root is no art, as on every other screen: the hangar runs without an
    /// extraction.</summary>
    [Fact]
    public void ArtIsNullWithoutADataRoot() => Assert.Null(OpenOnPaint().Page.Art);

    /// <summary>The preview is the original's own composite: the three mask weights blend the
    /// three colours, the shading map modulates that, and the `.BM`'s bottom-up rows are flipped.
    /// Pinned against a hand-built mask set whose expected pixels are arithmetic, not a
    /// screenshot.</summary>
    [Fact]
    public void ThePreviewComposesTheMasksAndTheColours()
    {
        string root = Path.Combine(_dir, "root");
        WriteMask(root, "FORTUNE", "BLO_WING");
        var flow = OpenOnPaint(root);
        flow.Scratch.Airframe = 3;          // Bloodhawk, skin prefix blo
        flow.Scratch.LoadPatternDefaults(4); // fortune: red, 25/25/25, white

        var art = flow.Page.Art;
        Assert.NotNull(art);
        Assert.Equal(2, art!.Image.Width);
        Assert.Equal(2, art.Image.Height);
        Assert.Contains("FORTUNE", art.Caption, StringComparison.Ordinal);

        // Top row is the mask's bottom row: slot 3 then slot 1 at full shading.
        AssertPixel(art.Image, 0, 255, 255, 255);
        AssertPixel(art.Image, 1, 223, 0, 41);
        // Bottom row: slot 1 at half shading, then slot 2 at full.
        AssertPixel(art.Image, 2, 112, 0, 21);
        AssertPixel(art.Image, 3, 25, 25, 25);
    }

    /// <summary>Every scratch edit refreshes the preview, and nothing else does: the shell rebuilds
    /// its texture when the page hands over a different image, so an unchanged screen must hand
    /// over the same one.</summary>
    [Fact]
    public void EveryEditRefreshesThePreview()
    {
        string root = Path.Combine(_dir, "root");
        WriteMask(root, "FORTUNE", "BLO_WING");
        var flow = OpenOnPaint(root);
        flow.Scratch.Airframe = 3;
        flow.Scratch.LoadPatternDefaults(4);

        var first = flow.Page.Art;
        Assert.NotNull(first);
        Assert.Same(first!.Image, flow.Page.Art!.Image);

        flow.Move(1);
        Assert.True(flow.Step(1));
        Assert.NotSame(first.Image, flow.Page.Art!.Image);
    }

    /// <summary>The shipped masks compose for every airframe wearing every pattern its own mask
    /// allows, which is the sweep that would catch a wrong airframe-to-skin-prefix row or a mask
    /// offering a pattern that ships no skins for that plane.</summary>
    [ExtractedDataFact]
    public void EveryAirframeComposesEveryPatternItsMaskAllows()
    {
        var flow = OpenOnPaint(TestData.DataRoot);
        for (int airframe = 0; airframe <= CustomPlaneDef.MaxAirframe; airframe++)
        {
            flow.Scratch.Airframe = airframe;
            for (int pattern = 0; pattern < HangarPaintTables.PatternCount; pattern++)
            {
                if (!HangarPaintTables.Default.Available(pattern, airframe))
                {
                    continue;
                }

                flow.Scratch.LoadPatternDefaults(pattern);
                var art = flow.Page.Art;
                Assert.True(art != null && art.Image.Width > 1, $"airframe {airframe} pattern {pattern}");
            }
        }
    }

    // One texel's RGB out of the composed preview.
    private static void AssertPixel(TgaImage image, int pixel, int r, int g, int b)
    {
        Assert.Equal(r, image.Rgba[pixel * 4]);
        Assert.Equal(g, image.Rgba[(pixel * 4) + 1]);
        Assert.Equal(b, image.Rgba[(pixel * 4) + 2]);
    }

    // A 2x2 `.BM` mask set (u16 height, u16 width, then shading RGB, then the three weight
    // planes): bottom-left slot 1 at half shading, bottom-right slot 2, top-left slot 3,
    // top-right slot 1. Rows are stored bottom-up, which is what the flip has to undo.
    private static void WriteMask(string dataRoot, string pattern, string skin)
    {
        var dir = Path.Combine(dataRoot, "extracted", "rof", "ASSETS", "GRAPHICS", pattern);
        Directory.CreateDirectory(dir);
        var bytes = new byte[4 + (6 * 4)];
        bytes[0] = 2; // height
        bytes[2] = 2; // width
        for (int j = 0; j < 4; j++)
        {
            byte shade = j == 0 ? (byte)128 : (byte)255;
            bytes[4 + (j * 3)] = shade;
            bytes[5 + (j * 3)] = shade;
            bytes[6 + (j * 3)] = shade;
        }

        byte[] slot = { 1, 2, 3, 1 }; // which paint slot owns each texel
        for (int j = 0; j < 4; j++)
        {
            bytes[4 + 12 + ((slot[j] - 1) * 4) + j] = 255;
        }

        File.WriteAllBytes(Path.Combine(dir, skin + ".BM"), bytes);
    }

    // A flow standing on the PAINT screen with a fresh scratch plane.
    private HangarFlow OpenOnPaint(string? dataRoot = null, UiStrings? strings = null)
    {
        var flow = new HangarFlow(_store, strings ?? UiStrings.Empty, dataRoot);
        for (int guard = 0; flow.Screen != HangarScreen.Paint && guard < HangarFlow.Order.Length; guard++)
        {
            flow.AnswerDefaultsAsk(false); // a no-op except on the airframe-defaults ask (E41)
            flow.Accept();
        }

        Assert.Equal(HangarScreen.Paint, flow.Screen);
        return flow;
    }
}
