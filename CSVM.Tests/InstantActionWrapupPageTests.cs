using System.Linq;
using CSVM.UI;
using CSVM.UI.Menu;
using Xunit;

namespace CSVM.Tests;

/// <summary>The Original presentation's Instant Action wrap-up page, read off one frozen snapshot:
/// the heading, the four decoded rows against <c>[@IA_WrapUp@]</c>'s own geometry, and the further
/// lines the built-in board carries (<c>docs/formats/instant-action/wrap-up.md</c>).</summary>
public class InstantActionWrapupPageTests
{
    // A won stunt flight: 3:06 flown, four kills, three zones, 27% of the rounds on target.
    private static readonly IaWrapupSnapshot Won =
        new(true, "C1   .   Stunt Flying", 186f, 4, 3, 27, new[] { "1.  Pier    12.4    12.4", "TOTAL   12.4" });

    private static readonly IaWrapupSnapshot Lost = new(false, "C3   .   Dogfight", 74.5f, 1, 0, 8);

    [Fact]
    public void RowsCarryTheHeadingAndTheFourDecodedValues()
    {
        var rows = InstantActionWrapupPage.Rows(Won);

        Assert.Equal(9, rows.Count);
        Assert.Equal("Instant Action", rows[0].Text);
        Assert.Contains(rows, l => l.Text == "Time to Complete Mission");
        Assert.Contains(rows, l => l.Text == "03:06");
        Assert.Contains(rows, l => l.Text == "Enemies Shot Down");
        Assert.Contains(rows, l => l.Text == "4");
        Assert.Contains(rows, l => l.Text == "Danger Zones Completed");
        Assert.Contains(rows, l => l.Text == "3");
        Assert.Contains(rows, l => l.Text == "Shot %");
        Assert.Contains(rows, l => l.Text == "27%");
    }

    [Fact]
    public void RowsStandAtTheAuthoredColumns()
    {
        var rows = InstantActionWrapupPage.Rows(Won);

        Assert.Equal((467f, 98f), (rows[0].X, rows[0].Y));
        Assert.Equal((480f, 154f), (rows[1].X, rows[1].Y));
        Assert.Equal((533f, 185f), (rows[2].X, rows[2].Y));
        Assert.Equal((533f, 369f), (rows[8].X, rows[8].Y));
    }

    [Fact]
    public void RowsAreNeverRecomputedFromTheLiveRun()
    {
        // A losing run's numbers are the snapshot's own, down to the truncated clock and a shot
        // percentage the page never divides for itself.
        var rows = InstantActionWrapupPage.Rows(Lost);

        Assert.Contains(rows, l => l.Text == "01:14");
        Assert.Contains(rows, l => l.Text == "1");
        Assert.Contains(rows, l => l.Text == "0");
        Assert.Contains(rows, l => l.Text == "8%");
    }

    [Fact]
    public void ExtraLinesOpenWithTheOutcomeAndCarryTheContext()
    {
        var won = InstantActionWrapupPage.ExtraLines(Won);
        var lost = InstantActionWrapupPage.ExtraLines(Lost);

        Assert.Equal("MISSION COMPLETE", won[0]);
        Assert.Equal("C1   .   Stunt Flying", won[1]);
        Assert.Equal(new[] { "1.  Pier    12.4    12.4", "TOTAL   12.4" }, won.Skip(2));
        Assert.Equal(new[] { "MISSION FAILED", "C3   .   Dogfight" }, lost);
    }

    [Fact]
    public void PicturesAreTheSpreadAndTheFourBrushstrokes()
    {
        var art = InstantActionWrapupPage.Pictures();

        Assert.Equal(5, art.Count);
        Assert.Equal("IA_StatScreenBackground.jpg", art[0].Art.Name);
        Assert.All(art.Skip(1), p => Assert.Equal("IA_StatScreen_Brushstroke.png", p.Art.Name));
        Assert.Equal(new[] { 173f, 226f, 297f, 359f }, art.Skip(1).Select(p => p.Y));
    }

    [Fact]
    public void TheFurtherLinesBandClearsTheRowsAndThePlaque()
    {
        var band = InstantActionWrapupPage.ExtraBox();
        var (plaqueX, plaqueY) = InstantActionWrapupPage.ContinueAt();
        var rows = InstantActionWrapupPage.Rows(Won);

        Assert.True(band.Y > rows[8].Y, "the band opens below the last value row");
        Assert.True(band.X + band.Width <= plaqueX, "the band stops clear of the CONTINUE plaque");
        Assert.True(band.Y + band.Height <= plaqueY + 40f, "the band ends at the plaque's foot");
        Assert.Equal(4, InstantActionWrapupPage.ContinueArt().Frames);
    }

    [Fact]
    public void TheDecodedLayoutMovesEveryRowWithIt()
    {
        // The fixture section authors its own positions, so a page that read them rather than the
        // built-in fallbacks answers the fixture's plaque corner.
        var layout = CampaignLayout.Over(MenuLayoutReaderTests.OriginalLayout());

        var rows = InstantActionWrapupPage.Rows(Won, layout);
        var (x, y) = InstantActionWrapupPage.ContinueAt(layout);

        Assert.Equal("Instant Action", rows[0].Text);
        Assert.Equal((600f, 449f), (x, y));
        Assert.Equal("PM_B_Continue.png", InstantActionWrapupPage.ContinueArt(layout).Name);
    }
}
