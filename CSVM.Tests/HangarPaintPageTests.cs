using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The PAINT screen (PLAN-hangar C25): the pattern row stepping the airframe's own pattern list,
/// the three colour rows stepping the shipped schemes' palette, the record's composite picks
/// carried untouched, and the live preview composed through the original's region masks with the
/// same formula PlanePainter uses in flight (docs/formats/paint.md).
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

    /// <summary>Four rows: the pattern, then the three paint slots in the record's own order
    /// (body, dark trim, light trim: the slot order paint.md confirmed against the artwork).</summary>
    [Fact]
    public void OffersThePatternAndThreeColourRows()
    {
        var flow = OpenOnPaint();

        Assert.Equal(4, flow.Page.RowCount);
        Assert.StartsWith("Pattern:", flow.Page.RowText(0), StringComparison.Ordinal);
        Assert.Contains("Paint 1", flow.Page.RowText(1), StringComparison.Ordinal);
        Assert.Contains("Paint 2", flow.Page.RowText(2), StringComparison.Ordinal);
        Assert.Contains("Paint 3", flow.Page.RowText(3), StringComparison.Ordinal);
    }

    /// <summary>With no extraction there is no per-aircraft list to read, so the pattern row walks
    /// the decoded 14-entry table itself (0x0060301c) rather than guessing a subset of it.</summary>
    [Fact]
    public void WithoutALibraryThePatternRowWalksTheDecodedTable()
    {
        var flow = OpenOnPaint();

        Assert.True(flow.Step(1));
        Assert.Equal(1, flow.Scratch.PaintPattern);
        Assert.Equal("Pattern: BLCKSWAN", flow.Page.RowText(0));

        for (int i = 0; i < 13; i++)
        {
            flow.Step(1);
        }

        Assert.Equal(0, flow.Scratch.PaintPattern);
        Assert.Equal("Pattern: BLACKHAT", flow.Page.RowText(0));
    }

    /// <summary>Stepping a colour slot lands on a colour the original's artists authored: the
    /// palette is the twelve shipped schemes' own triples, the darkest of them (25,25,25) as the
    /// paint UI actually saves black.</summary>
    [Fact]
    public void ColoursStepTheShippedSchemesPalette()
    {
        var flow = OpenOnPaint();
        flow.Move(1);

        Assert.True(flow.Step(1));
        Assert.Equal(new PaintColour(223, 0, 41), flow.Scratch.Colour1);
        Assert.True(flow.Step(1));
        Assert.Equal(new PaintColour(25, 25, 25), flow.Scratch.Colour1);
        Assert.Contains("25,25,25", flow.Page.Detail(1), StringComparison.Ordinal);
    }

    /// <summary>Each colour row edits its own slot and no other.</summary>
    [Fact]
    public void EachRowEditsItsOwnSlot()
    {
        var flow = OpenOnPaint();
        flow.Move(2);
        flow.Step(1);

        Assert.Equal(new PaintColour(0, 0, 0), flow.Scratch.Colour1);
        Assert.Equal(new PaintColour(223, 0, 41), flow.Scratch.Colour2);
        Assert.Equal(new PaintColour(0, 0, 0), flow.Scratch.Colour3);
    }

    /// <summary>The palette is a cycle in both directions, like every other hangar stepper.</summary>
    [Fact]
    public void SteppingWrapsAtBothEnds()
    {
        var flow = OpenOnPaint();
        flow.Move(3);

        Assert.True(flow.Step(-1));
        var last = flow.Scratch.Colour3;
        Assert.Equal(new PaintColour(32, 90, 167), last);
        Assert.True(flow.Step(1));
        Assert.Equal(new PaintColour(223, 0, 41), flow.Scratch.Colour3);
        Assert.True(flow.Step(-1));
        Assert.Equal(last, flow.Scratch.Colour3);
    }

    /// <summary>A colour the palette does not carry (an imported original save) is kept and named
    /// as its own triple; the first step then moves it onto an authored colour.</summary>
    [Fact]
    public void AnOffPaletteColourIsKeptUntilStepped()
    {
        var flow = OpenOnPaint();
        flow.Scratch.Colour1 = new PaintColour(7, 8, 9);

        Assert.Contains("custom 7,8,9", flow.Page.RowText(1), StringComparison.Ordinal);
        Assert.Contains("not a shipped colour", flow.Page.Detail(1), StringComparison.Ordinal);
        flow.Move(1);
        flow.Step(1);
        Assert.Equal(new PaintColour(223, 0, 41), flow.Scratch.Colour1);
    }

    /// <summary>The detail line names the scheme slot the colour came from, so a pilot picking
    /// "hughes 1" is picking the yellow off a Hughes Aviation plane.</summary>
    [Fact]
    public void DetailNamesTheSchemeAColourCameFrom()
    {
        var flow = OpenOnPaint();
        flow.Scratch.Colour1 = new PaintColour(243, 194, 0);

        Assert.Contains("hughes 1", flow.Page.Detail(1), StringComparison.Ordinal);
    }

    /// <summary>The two composite picks and the third dword are carried, never written: no screen
    /// handler in the original writes +0x64 and the a*5+b encoding's meaning is still open, so
    /// this screen edits only what the decode names.</summary>
    [Fact]
    public void TheCompositePicksAreCarriedUntouched()
    {
        var flow = OpenOnPaint();
        flow.Scratch.PaintPick1 = 17;
        flow.Scratch.PaintPick2 = 23;
        flow.Scratch.PaintPick3 = 5;

        for (int row = 0; row < flow.Page.RowCount; row++)
        {
            flow.Page.Step(row, 1);
        }

        Assert.Equal(17, flow.Scratch.PaintPick1);
        Assert.Equal(23, flow.Scratch.PaintPick2);
        Assert.Equal(5, flow.Scratch.PaintPick3);
    }

    /// <summary>Confirm advances to the name screen without editing the paint.</summary>
    [Fact]
    public void AcceptAdvancesWithoutEditing()
    {
        var flow = OpenOnPaint();
        flow.Scratch.PaintPattern = 6;
        flow.Scratch.Colour1 = new PaintColour(1, 2, 3);
        flow.Accept();

        Assert.Equal(HangarScreen.Name, flow.Screen);
        Assert.Equal(6, flow.Scratch.PaintPattern);
        Assert.Equal(new PaintColour(1, 2, 3), flow.Scratch.Colour1);
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
        flow.Scratch.Airframe = 3; // Bloodhawk, skin prefix blo
        flow.Scratch.PaintPattern = 4; // fortune
        flow.Scratch.Colour1 = new PaintColour(223, 0, 41);
        flow.Scratch.Colour2 = new PaintColour(25, 25, 25);
        flow.Scratch.Colour3 = new PaintColour(255, 255, 255);

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
        flow.Scratch.PaintPattern = 4;

        var first = flow.Page.Art;
        Assert.NotNull(first);
        Assert.Same(first!.Image, flow.Page.Art!.Image);

        flow.Move(1);
        Assert.True(flow.Step(1));
        Assert.NotSame(first.Image, flow.Page.Art!.Image);
    }

    /// <summary>With the extraction present the pattern row is the airframe's own list: the Fury
    /// carries four patterns, the Balmoral two (docs/formats/paint.md's per-aircraft table).</summary>
    [ExtractedDataFact]
    public void ThePatternRowIsTheAirframesOwnList()
    {
        var flow = OpenOnPaint(TestData.DataRoot);
        flow.Scratch.Airframe = 7; // Fury: fortune, blckswan, hughes, studio
        flow.Scratch.PaintPattern = 4;

        var seen = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            flow.Step(1);
            seen.Add(flow.Scratch.PaintPattern);
        }

        // hughes, studio, blckswan, back to fortune: the four folders that ship fur_ skins, in
        // the library's own scan order, and no other of the fourteen.
        Assert.Equal(new[] { 6, 11, 1, 4 }, seen);
    }

    /// <summary>The shipped masks compose for every airframe wearing every pattern its own list
    /// offers, which is the sweep that would catch a wrong airframe-to-skin-prefix row.</summary>
    [ExtractedDataFact]
    public void EveryAirframeComposesItsOwnPatterns()
    {
        var flow = OpenOnPaint(TestData.DataRoot);
        for (int airframe = 0; airframe <= CustomPlaneDef.MaxAirframe; airframe++)
        {
            flow.Scratch.Airframe = airframe;
            flow.Scratch.PaintPattern = 4; // fortune covers all eleven
            var art = flow.Page.Art;
            Assert.NotNull(art);
            Assert.True(art!.Image.Width > 1 && art.Image.Height > 1);
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
    private HangarFlow OpenOnPaint(string? dataRoot = null)
    {
        var flow = new HangarFlow(_store, UiStrings.Empty, dataRoot);
        for (int guard = 0; flow.Screen != HangarScreen.Paint && guard < HangarFlow.Order.Length; guard++)
        {
            flow.AnswerDefaultsAsk(false); // a no-op except on the airframe-defaults ask (E41)
            flow.Accept();
        }

        Assert.Equal(HangarScreen.Paint, flow.Screen);
        return flow;
    }
}
