using System.IO;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>The campaign flow's navigation contract: a screen stack rather than a fixed order, an
/// unregistered screen drawing a placeholder rather than crashing, returning to an open screen
/// instead of stacking a second copy of it, and backing out of the first screen ending the flow.
/// </summary>
public class CampaignFlowTests
{
    [Fact]
    public void OpensOnTheRoster()
    {
        var flow = NewFlow(out _);

        Assert.Equal(CampaignScreen.Roster, flow.Screen);
        Assert.Equal(CampaignExit.None, flow.Exit);
        Assert.IsType<CampaignRosterPage>(flow.Page);
    }

    /// <summary>The wave's mount point: the three screens with a page of their own draw it, and the
    /// two still to come draw the placeholder, which edits nothing.</summary>
    [Fact]
    public void UnregisteredScreensDrawThePlaceholder()
    {
        Assert.True(CampaignFlow.HasPage(CampaignScreen.Roster));
        Assert.True(CampaignFlow.HasPage(CampaignScreen.Cabin));
        Assert.True(CampaignFlow.HasPage(CampaignScreen.PreviousMissions));

        var flow = NewFlow(out _);
        flow.GoTo(CampaignScreen.Briefing);

        Assert.False(CampaignFlow.HasPage(CampaignScreen.Briefing));
        Assert.IsType<CampaignPlaceholderPage>(flow.Page);
    }

    [Fact]
    public void BackLeavesAScreenAndCancelsFromTheFirstOne()
    {
        var flow = NewFlow(out _);
        flow.GoTo(CampaignScreen.Cabin);
        flow.GoTo(CampaignScreen.Briefing);

        flow.Back();
        Assert.Equal(CampaignScreen.Cabin, flow.Screen);
        flow.Back();
        Assert.Equal(CampaignScreen.Roster, flow.Screen);
        Assert.Equal(CampaignExit.None, flow.Exit);

        flow.Back();
        Assert.Equal(CampaignExit.Cancelled, flow.Exit);
    }

    /// <summary>RETURN TO CABIN from a briefing must land on the cabin that opened it, not on a
    /// second one stacked over it, or every return would deepen the stack by one.</summary>
    [Fact]
    public void ReturningToAnOpenScreenPopsBackToIt()
    {
        var flow = NewFlow(out _);
        flow.GoTo(CampaignScreen.Cabin);
        flow.GoTo(CampaignScreen.Briefing);

        flow.GoTo(CampaignScreen.Cabin);
        Assert.Equal(CampaignScreen.Cabin, flow.Screen);

        flow.Back();
        Assert.Equal(CampaignScreen.Roster, flow.Screen);
    }

    internal static CampaignFlow NewFlow(out string dir)
    {
        dir = Path.Combine(TestData.TempDir(), "Profiles");
        return new CampaignFlow(new CampaignProfileStore(dir), UiStrings.Empty);
    }
}
