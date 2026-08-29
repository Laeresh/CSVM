using System.IO;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>The scrapbook's results page (C17): opened on the mission a finished mission just
/// flew, drawing C15/C16's results block and kill stamps off it, with REPLAY MISSION re-entering
/// that same mission (not the campaign's current position) and RETURN TO CABIN landing on the
/// cabin already on the stack.</summary>
public class CampaignScrapbookPageTests
{
    [Fact]
    public void DrawsTheFlownMissionsResultsAndStamps()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, new MissionAttempt(
            0, CompletedMask: 1, TimeMs: 215000, Shots: 25, Hits: 4, Money: 0,
            Airframe: 5, PlaneName: "Gypsy Magic", Kills: KillsOf((8, 3))));
        var flow = OpenedOnScrapbook(profile, seq: 0);

        Assert.Contains(flow.Page.Captions, l => l.Text == "Mission Completed");
        Assert.Contains(flow.Page.Captions, l => l.Text == "03:35");
        Assert.Contains(flow.Page.Captions, l => l.Text == "16%");
        Assert.Contains(flow.Page.Captions, l => l.Text == "3"); // the one kill-stamp count
        Assert.Single(flow.Page.Pictures);
        Assert.Equal(8, flow.Page.Pictures[0].Frame); // Kestrel, plain
    }

    /// <summary>C17's own trap: after a win the campaign position advances past the flown mission,
    /// so REPLAY MISSION must not derive its target from the profile's progress.</summary>
    [Fact]
    public void ReplayMissionReEntersTheFlownMissionNotTheCurrentPosition()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0));
        var flow = OpenedOnScrapbook(profile, seq: 0);
        Assert.Equal(1, CampaignProgression.NextMissionSeq(profile)); // the win advanced it

        flow.FocusRow(CampaignScrapbookPage.ReplayRow);
        flow.Accept();

        Assert.Equal(CampaignScreen.Briefing, flow.Screen);
        Assert.Equal(0, flow.MissionSeq); // the mission just flown, not seq 1
    }

    [Fact]
    public void ReturnToCabinLandsOnTheCabinAlreadyOnTheStack()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0));
        var flow = OpenedOnScrapbook(profile, seq: 0);

        flow.FocusRow(CampaignScrapbookPage.CabinRow);
        flow.Accept();

        Assert.Equal(CampaignScreen.Cabin, flow.Screen);
        flow.Back();
        Assert.Equal(CampaignScreen.Roster, flow.Screen); // no second cabin stacked underneath
    }

    [Fact]
    public void ANeverAttemptedMissionDrawsNothingRatherThanThrowing()
    {
        var flow = OpenedOnScrapbook(CampaignProfileDef.NewProfile("Zachary"), seq: 5);

        Assert.Empty(flow.Page.Captions);
        Assert.Empty(flow.Page.Pictures);
    }

    private static MissionAttempt Attempt(int seq) =>
        new(seq, CompletedMask: 1, TimeMs: 40000, Shots: 10, Hits: 5, Money: 0,
            Airframe: 5, PlaneName: "Gypsy Magic");

    private static int[] KillsOf(params (int Index, int Count)[] kills)
    {
        var array = new int[CampaignProgression.AirframeCount];
        foreach (var (index, count) in kills)
        {
            array[index] = count;
        }

        return array;
    }

    private static CampaignFlow OpenedOnScrapbook(CampaignProfileDef profile, int seq)
    {
        var dir = Path.Combine(TestData.TempDir(), "Profiles");
        Directory.CreateDirectory(dir);
        var store = new CampaignProfileStore(dir);
        store.Save(profile);
        var flow = new CampaignFlow(store, UiStrings.Empty);
        flow.SelectProfile(store.Load(profile.Name)!);
        flow.SetMission(seq);
        flow.GoTo(CampaignScreen.Scrapbook);
        return flow;
    }
}
