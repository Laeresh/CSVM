using System.IO;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>The scrapbook's finished-missions list: one row per completed mission in <c>seq</c>
/// order, VIEW SELECTED as a no-op, REPLAY MISSION opening the briefing without advancing the
/// campaign, and RETURN TO CABIN never stacking a second cabin.</summary>
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

        // Two finished missions (0 and 2) plus VIEW SELECTED / REPLAY MISSION / RETURN TO CABIN.
        Assert.Equal(5, flow.Page.RowCount);
        Assert.Equal("Mission 1", flow.Page.RowText(0)); // seq 0 (1-based ordinal 1) comes first
        Assert.Equal("Mission 3", flow.Page.RowText(1)); // seq 2 (1-based ordinal 3) comes second
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

    [Fact]
    public void ReplayMissionWithNoFinishedMissionsRefuses()
    {
        var flow = OpenedOnPreviousMissions(CampaignProfileDef.NewProfile("Zachary"));

        flow.FocusRow(1); // REPLAY MISSION (0 mission rows + VIEW SELECTED before it)
        flow.Accept();

        Assert.Equal(CampaignScreen.PreviousMissions, flow.Screen);
        Assert.NotEqual(string.Empty, flow.Message);
    }

    [Fact]
    public void ViewSelectedConsumesThePressAndChangesNothing()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0));
        var flow = OpenedOnPreviousMissions(profile);

        flow.FocusRow(1); // VIEW SELECTED
        bool moved = flow.Accept();

        Assert.True(moved);
        Assert.Equal(CampaignScreen.PreviousMissions, flow.Screen);
        Assert.Equal(-1, flow.MissionSeq);
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
