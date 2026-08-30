using System.IO;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>The scrapbook's table of contents: one 80-pixel row per completed mission in <c>seq</c>
/// order with its icon and three lines, a four-row window with a scrollbar past that, VIEW SELECTED
/// as a no-op, REPLAY MISSION opening the briefing without advancing the campaign, and RETURN TO
/// CABIN never stacking a second cabin.</summary>
public class CampaignPreviousMissionsPageTests
{
    [Fact]
    public void ListsOnlyFinishedMissionsInSeqOrder()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(2));
        CampaignProgression.Record(profile, Attempt(0));
        CampaignProgression.Record(profile, Attempt(1, mask: 4)); // no primary: not "finished"
        var flow = OpenedOnPreviousMissions(profile);

        // Two finished missions (0 and 2), then VIEW SELECTED, REPLAY MISSION, the CURRENT MISSION
        // bookmark and RETURN TO CABIN.
        Assert.Equal(6, flow.Page.RowCount);
        Assert.Equal(string.Empty, flow.Page.RowText(0)); // a mission row draws its own three lines
        Assert.Equal("VIEW SELECTED", flow.Page.RowText(2));
        Assert.Equal("Mission 1", flow.Page.Detail(0)); // seq 0 (1-based ordinal 1) comes first
        Assert.Equal("Mission 3", flow.Page.Detail(1)); // seq 2 (1-based ordinal 3) comes second
    }

    /// <summary>A row is the icon at the listbox's own X, then three lines 20 apart from 10 down
    /// the 80-pixel row: the mission's short name, its area and the plane that flew it.</summary>
    [Fact]
    public void ARowIsAnIconAndThreeLinesAtTheListboxsOwnGeometry()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0));
        CampaignProgression.Record(profile, Attempt(1));
        var flow = OpenedOnPreviousMissions(profile);

        var icons = flow.Page.Pictures;
        Assert.Equal(2, icons.Count);
        Assert.EndsWith("FC_PlaneIcons.png", icons[0].Art.Name);
        Assert.Equal(5, icons[0].Frame); // the airframe the recorded run flew
        Assert.Equal(422f, icons[0].X);
        Assert.Equal(140f, icons[0].Y);
        Assert.Equal(220f, icons[1].Y); // one 80-pixel row down

        // The two header widgets, then three lines per row.
        var rows = flow.Page.Captions;
        Assert.Equal(8, rows.Count);
        Assert.Equal(520f, rows[2].X);
        Assert.Equal(new[] { 150f, 170f, 190f }, new[] { rows[2].Y, rows[3].Y, rows[4].Y });
        Assert.Equal("Gypsy Magic", rows[4].Text); // the plane, the row's third line
        Assert.Equal(230f, rows[5].Y); // the next row's first line
    }

    /// <summary>The picked row washes and outlines, the focused one takes the same outline over a
    /// fainter wash, and both are the list's own colours.</summary>
    [Fact]
    public void ThePickedAndFocusedRowsAreWashedAndOutlined()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0));
        CampaignProgression.Record(profile, Attempt(1));
        var flow = OpenedOnPreviousMissions(profile);

        // Row 0 is focused and nothing is picked yet: one wash and one outline.
        var focused = flow.Page.Fills;
        Assert.Equal(2, focused.Count);
        Assert.Equal(140f, focused[0].Y);
        Assert.Equal(0x40 / 255f, focused[0].Opacity);
        Assert.True(focused[1].Border);
        Assert.Equal(0xdd, focused[1].R);

        flow.Accept(); // pick row 0
        Assert.Equal(0x80 / 255f, flow.Page.Fills[0].Opacity);
    }

    /// <summary>Past four rows the window scrolls to keep the cursor's row on screen and the
    /// scrollbar appears, its thumb landing flush at the bottom on the last row.</summary>
    [Fact]
    public void TheWindowScrollsAndTheScrollbarAppearsPastFourRows()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        for (int seq = 0; seq < 6; seq++)
        {
            CampaignProgression.Record(profile, Attempt(seq));
        }

        var flow = OpenedOnPreviousMissions(profile);

        // Four icons plus the two arrows and the thumb, never more than the window holds.
        Assert.Equal(7, flow.Page.Pictures.Count);
        Assert.Equal("Mission 1", flow.Page.Captions[2].Text); // still at the top of the list

        flow.FocusRow(5); // the last mission row
        Assert.Equal("Mission 3", flow.Page.Captions[2].Text); // seq 2 heads the window now
        Assert.Equal(150f, flow.Page.Captions[2].Y); // the window's first slot, wherever it stands

        var thumb = flow.Page.Pictures[6];
        Assert.Equal(730f, thumb.X);
        Assert.Equal(16f, thumb.Width);
        Assert.Equal(449f, thumb.Y + thumb.Height); // flush against the lower arrow
    }

    /// <summary>A second confirm on the row already picked is REPLAY MISSION's own press, so a
    /// mission is replayed without walking down to the button.</summary>
    [Fact]
    public void ASecondConfirmOnThePickedRowReplaysIt()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0));
        CampaignProgression.Record(profile, Attempt(1));
        var flow = OpenedOnPreviousMissions(profile);

        flow.FocusRow(1);
        flow.Accept(); // picks it
        Assert.Equal(CampaignScreen.PreviousMissions, flow.Screen);

        flow.Accept(); // replays it
        Assert.Equal(CampaignScreen.Briefing, flow.Screen);
        Assert.Equal(1, flow.MissionSeq);
    }

    [Fact]
    public void ReplayMissionOpensTheBriefingOnTheSelectedSeqWithoutAdvancing()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0));
        CampaignProgression.Record(profile, Attempt(1));
        var flow = OpenedOnPreviousMissions(profile);
        int before = profile.MissionsCompleted;

        flow.FocusRow(1); // the seq-1 row (second finished mission, still row 1 in seq order)
        flow.Accept();    // select it
        flow.FocusRow(3); // REPLAY MISSION (2 mission rows + VIEW SELECTED before it)
        flow.Accept();

        Assert.Equal(CampaignScreen.Briefing, flow.Screen);
        Assert.Equal(1, flow.MissionSeq);
        Assert.Equal(before, profile.MissionsCompleted);
    }

    [Fact]
    public void ReplayMissionWithNothingPickedFallsBackToTheFirstFinishedMission()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0));
        var flow = OpenedOnPreviousMissions(profile);

        flow.FocusRow(2); // REPLAY MISSION (1 mission row + VIEW SELECTED before it)
        flow.Accept();

        Assert.Equal(CampaignScreen.Briefing, flow.Screen);
        Assert.Equal(0, flow.MissionSeq);
    }

    /// <summary>Replay Mission is offered only where <c>uiData</c> 2411 offers it, so a profile
    /// with nothing flown carries no such row rather than a row that refuses.</summary>
    [Fact]
    public void WithNoFinishedMissionsThereIsNoReplayRowAtAll()
    {
        var flow = OpenedOnPreviousMissions(CampaignProfileDef.NewProfile("Zachary"));

        Assert.Equal(-1, RowOf(flow.Page, BoardButton.ReplayMission));
        Assert.Equal(3, flow.Page.RowCount); // VIEW SELECTED, the bookmark, RETURN TO CABIN
    }

    /// <summary>VIEW SELECTED opens the book at the picked mission's first spread, which is the
    /// original's <c>uiData</c> 2405 mode 1 and the only way the table of contents reaches it.</summary>
    [Fact]
    public void ViewSelectedOpensTheBookAtThePickedMission()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0));
        CampaignProgression.Record(profile, Attempt(1));
        var flow = OpenedOnPreviousMissions(profile);

        flow.FocusRow(1); // the seq-1 row
        flow.Accept();    // pick it
        Press(flow, BoardButton.ViewMission);

        Assert.Equal(CampaignScreen.Scrapbook, flow.Screen);
        Assert.Equal(1, flow.MissionSeq);
    }

    /// <summary>The CURRENT MISSION bookmark opens the book at the campaign's own next mission,
    /// whatever the list's cursor is on.</summary>
    [Fact]
    public void TheBookmarkOpensTheBookAtTheCampaignsCurrentMission()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0));
        var flow = OpenedOnPreviousMissions(profile);

        Press(flow, BoardButton.CurrentMission);

        Assert.Equal(CampaignScreen.Scrapbook, flow.Screen);
        Assert.Equal(1, flow.MissionSeq); // the win advanced the campaign past seq 0
    }

    /// <summary>RETURN TO CABIN lands on the cabin already on the stack (from
    /// <see cref="CampaignFlow.SelectProfile"/>) rather than pushing a second copy: backing out of
    /// it once more reaches the roster, the flow's own first screen, not a second cabin.</summary>
    [Fact]
    public void ReturnToCabinGoesBackWithoutStackingASecondCabin()
    {
        var flow = OpenedOnPreviousMissions(CampaignProfileDef.NewProfile("Zachary"));

        flow.FocusRow(flow.Page.RowCount - 1); // RETURN TO CABIN
        flow.Accept();

        Assert.Equal(CampaignScreen.Cabin, flow.Screen);
        flow.Back();
        Assert.Equal(CampaignScreen.Roster, flow.Screen);
    }

    // The button rows shift as Replay comes and goes, so a test presses one by name.
    private static void Press(CampaignFlow flow, BoardButton button)
    {
        int row = RowOf(flow.Page, button);
        Assert.NotEqual(-1, row);
        flow.FocusRow(row);
        flow.Accept();
    }

    private static int RowOf(ICampaignPage page, BoardButton button)
    {
        for (int row = 0; row < page.RowCount; row++)
        {
            if (page.Button(row).Button == button)
            {
                return row;
            }
        }

        return -1;
    }

    private static MissionAttempt Attempt(int seq, int mask = 1) =>
        new(seq, CompletedMask: mask, TimeMs: 40000, Shots: 10, Hits: 5, Money: 0,
            Airframe: 5, PlaneName: "Gypsy Magic");

    private static CampaignFlow OpenedOnPreviousMissions(CampaignProfileDef profile)
    {
        var dir = Path.Combine(TestData.TempDir(), "Profiles");
        Directory.CreateDirectory(dir);
        var flow = new CampaignFlow(new CampaignProfileStore(dir), UiStrings.Empty);
        flow.SelectProfile(profile);
        flow.GoTo(CampaignScreen.PreviousMissions);
        return flow;
    }
}
