using System.IO;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>The scrapbook's results page: opened on the mission a finished mission just flew,
/// drawing the results block and kill stamps off it, with the page/mission arrows and the Current
/// Mission bookmark browsing the rest of the book and REPLAY MISSION acting on whichever mission
/// is currently shown rather than a fixed one.</summary>
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

    /// <summary>A never-attempted mission draws no art (nothing to compose without a data root) but
    /// still names itself "Not yet flown" (D20) rather than drawing an empty results block.</summary>
    [Fact]
    public void ANeverAttemptedMissionShowsNotYetFlownRatherThanThrowing()
    {
        var flow = OpenedOnScrapbook(CampaignProfileDef.NewProfile("Zachary"), seq: 5);

        Assert.Contains(flow.Page.Captions, l => l.Text == "Not yet flown");
        Assert.Empty(flow.Page.Pictures);
    }

    /// <summary>An openable scrap on spread 1 gets its own row after the two buttons, and
    /// confirming it opens the detail view naming exactly that scrap.</summary>
    [Fact]
    public void AnOpenableScrapGetsARowThatOpensItsDetailView()
    {
        string root = ScrapbookCompositionFixture.WriteMinimalOpenableScrap(TestData.TempDir(), mission: 1);
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0));
        var flow = OpenedOnScrapbook(profile, seq: 0, dataRoot: root);

        Assert.Equal(3, flow.Page.RowCount); // Replay, Cabin, the one openable scrap

        flow.FocusRow(2);
        Assert.Equal("IDS_TEST_TITLE", flow.Page.Detail(2));
        flow.Accept();

        Assert.Equal(CampaignScreen.ScrapbookZoom, flow.Screen);
        Assert.Equal((1, 1, 1), flow.ZoomTarget);
    }

    /// <summary>A scrap row draws no list text of its own: its picture already stands at its
    /// authored position.</summary>
    [Fact]
    public void AScrapRowDrawsNoRowTextOfItsOwn()
    {
        string root = ScrapbookCompositionFixture.WriteMinimalOpenableScrap(TestData.TempDir(), mission: 1);
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0));
        var flow = OpenedOnScrapbook(profile, seq: 0, dataRoot: root);

        Assert.Equal(string.Empty, flow.Page.RowText(2));
    }

    /// <summary>The forward arrow steps within mission 1's two spreads, then rolls to mission 2's
    /// and mission 3's own spread 1, and offers no further forward step past the book's last
    /// page.</summary>
    [Fact]
    public void ArrowsStepForwardThenRollToTheNextMission()
    {
        string root = ScrapbookCompositionFixture.WriteBook(TestData.TempDir());
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0));
        var flow = OpenedOnScrapbook(profile, seq: 0, dataRoot: root);

        StepNext(flow); // (1,1) -> (1,2)
        Assert.EndsWith("01_02_a.PNG", flow.Page.Pictures[0].Art.Name);

        StepNext(flow); // (1,2) -> (2,1)
        Assert.EndsWith("02_01_a.PNG", flow.Page.Pictures[0].Art.Name);

        StepNext(flow); // (2,1) -> (3,1)
        Assert.EndsWith("03_01_a.PNG", flow.Page.Pictures[0].Art.Name);

        Assert.Equal(-1, RowOf(flow.Page, BoardButton.ScrapbookNext)); // no page past the book's end
    }

    /// <summary>The back arrow steps within a mission's own spreads, then rolls into the previous
    /// mission's own last spread rather than its first, and offers no further back step at the
    /// front of the book.</summary>
    [Fact]
    public void ArrowsStepBackAndRollToThePreviousMissionsLastSpread()
    {
        string root = ScrapbookCompositionFixture.WriteBook(TestData.TempDir());
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0));
        var flow = OpenedOnScrapbook(profile, seq: 0, dataRoot: root);

        Assert.Equal(-1, RowOf(flow.Page, BoardButton.ScrapbookPrev)); // front of the book

        StepNext(flow); // (1,1) -> (1,2)
        StepNext(flow); // (1,2) -> (2,1)

        StepPrev(flow); // (2,1) -> (1,2), the previous mission's own last spread, not its first
        Assert.EndsWith("01_02_a.PNG", flow.Page.Pictures[0].Art.Name);
    }

    /// <summary>The Current Mission bookmark shows only while the browsed mission differs from the
    /// campaign's own current one, and jumps back to that mission's spread 1.</summary>
    [Fact]
    public void BookmarkAppearsOnlyAwayFromTheCurrentMissionAndJumpsBackToIt()
    {
        string root = ScrapbookCompositionFixture.WriteBook(TestData.TempDir());
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0));
        var flow = OpenedOnScrapbook(profile, seq: 0, dataRoot: root);

        Assert.Equal(-1, RowOf(flow.Page, BoardButton.CurrentMission)); // opened on the current mission

        StepNext(flow); // (1,1) -> (1,2), still mission 1
        Assert.Equal(-1, RowOf(flow.Page, BoardButton.CurrentMission));

        StepNext(flow); // (1,2) -> (2,1), no longer the current mission
        int bookmarkRow = RowOf(flow.Page, BoardButton.CurrentMission);
        Assert.NotEqual(-1, bookmarkRow);

        flow.FocusRow(bookmarkRow);
        flow.Accept();
        Assert.EndsWith("01_01_a.PNG", flow.Page.Pictures[0].Art.Name); // back to mission 1, spread 1
        Assert.Equal(-1, RowOf(flow.Page, BoardButton.CurrentMission));
    }

    /// <summary>REPLAY MISSION acts on whichever mission is browsed, not
    /// <see cref="CampaignFlow.MissionSeq"/>: pressing it after stepping away from the flown mission
    /// re-enters the mission the page is now showing.</summary>
    [Fact]
    public void ReplayMissionActsOnTheBrowsedMissionRatherThanMissionSeq()
    {
        string root = ScrapbookCompositionFixture.WriteBook(TestData.TempDir());
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0));
        var flow = OpenedOnScrapbook(profile, seq: 0, dataRoot: root);

        StepNext(flow); // (1,1) -> (1,2)
        StepNext(flow); // (1,2) -> (2,1), mission slot 2 -- seq 1

        flow.FocusRow(CampaignScrapbookPage.ReplayRow);
        flow.Accept();

        Assert.Equal(CampaignScreen.Briefing, flow.Screen);
        Assert.Equal(1, flow.MissionSeq); // the browsed mission, not the one the book opened on
    }

    /// <summary>A spread-1 view of a mission with no recorded attempt shows the "Not yet flown"
    /// placeholder rather than a results block or nothing at all.</summary>
    [Fact]
    public void AnUnflownMissionsResultsPageShowsNotYetFlown()
    {
        string root = ScrapbookCompositionFixture.WriteBook(TestData.TempDir());
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0)); // only mission 1 (seq 0) is flown
        var flow = OpenedOnScrapbook(profile, seq: 0, dataRoot: root);

        StepNext(flow); // (1,1) -> (1,2)
        StepNext(flow); // (1,2) -> (2,1), never flown

        Assert.Contains(flow.Page.Captions, l => l.Text == "Not yet flown");
    }

    /// <summary>The danger-zone slot (D21): the photo-corner mount always draws, but the capture it
    /// frames is skipped until the profile directory actually carries the file
    /// (<c>CampaignScrapbookPage.CaptureExists</c>, resolved against
    /// <c>CampaignProfileStore.DirFor</c>) -- exactly the gate <c>ScrapbookComposition</c> already
    /// applies, exercised here end to end through the page rather than the composition alone.</summary>
    [Fact]
    public void TheDangerZoneCaptureDrawsOnlyOnceItsFileExistsInTheProfileDirectory()
    {
        string root = ScrapbookCompositionFixture.WriteDangerZoneSpread(TestData.TempDir(), mission: 1);
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0));
        var flow = OpenedOnScrapbook(profile, seq: 0, dataRoot: root);

        Assert.Single(flow.Page.Pictures); // just the corner mount, no capture on disk yet

        File.WriteAllBytes(Path.Combine(flow.Store.DirFor(profile.Name), "Snap_1_18.PNG"), new byte[] { 0 });
        Assert.Equal(2, flow.Page.Pictures.Count); // the capture, then the mount painted over it
        Assert.EndsWith("Snap_1_18.PNG", flow.Page.Pictures[0].Art.Name);
        Assert.EndsWith("DZ_generic_corners.PNG", flow.Page.Pictures[1].Art.Name);
    }

    // Presses whichever row currently carries the named arrow/bookmark button, failing loudly if
    // the layout ever stops offering it where a test expects one.
    private static void StepNext(CampaignFlow flow) => Press(flow, BoardButton.ScrapbookNext);

    private static void StepPrev(CampaignFlow flow) => Press(flow, BoardButton.ScrapbookPrev);

    private static void Press(CampaignFlow flow, BoardButton button)
    {
        int row = RowOf(flow.Page, button);
        Assert.NotEqual(-1, row);
        flow.FocusRow(row);
        flow.Accept();
    }

    // The row layout shifts as arrows/bookmark come and go, so tests locate a button by scanning
    // rather than assuming a fixed index.
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

    private static CampaignFlow OpenedOnScrapbook(CampaignProfileDef profile, int seq, string? dataRoot = null)
    {
        var dir = Path.Combine(TestData.TempDir(), "Profiles");
        Directory.CreateDirectory(dir);
        var store = new CampaignProfileStore(dir);
        store.Save(profile);
        var flow = new CampaignFlow(store, UiStrings.Empty, dataRoot);
        flow.SelectProfile(store.Load(profile.Name)!);
        flow.SetMission(seq);
        flow.GoTo(CampaignScreen.Scrapbook);
        return flow;
    }
}
