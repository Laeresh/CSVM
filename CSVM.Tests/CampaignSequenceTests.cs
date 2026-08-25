using System.IO;
using System.Linq;
using CSVM;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>The campaign's mission order as the shipped <c>cm_sequence.zrd</c> authors it
/// (docs/formats/campaign-sequence.md): 24 flat entries, the storage address each one resolves to,
/// and the backwards walk to the previous mission in the same world folder that cross-mission
/// persistence carries state along.</summary>
public class CampaignSequenceTests
{
    [ExtractedDataFact]
    public void TheSequenceIsTwentyFourFlatEntriesInStoryOrder()
    {
        var missions = Load();

        Assert.Equal(CampaignSequence.MissionCount, missions.Count);
        Assert.Equal(Enumerable.Range(0, 24).ToArray(), missions.Select(m => m.Seq).ToArray());
        Assert.Equal(24, missions.Select(m => m.SaveId).Distinct().Count());
    }

    /// <summary>The three numberings on the page's own table row for the first mission: story
    /// position 0 is Hawaii's first, stored in world folder 6 (<c>c3</c>) as <c>m01</c>, saved
    /// under id 601, and flown with a wingman.</summary>
    [ExtractedDataFact]
    public void TheFirstMissionResolvesToItsStorageAddress()
    {
        var first = Load()[0];

        Assert.Equal(1, first.Ordinal);
        Assert.Equal(6, first.Campaign);
        Assert.Equal("c3", first.ChapterFolder);
        Assert.Equal("m01", first.MissionFolder);
        Assert.Equal(601, first.SaveId);
        Assert.Equal("HAWAII", first.Area);
        Assert.True(first.Wingman);
    }

    /// <summary>What the cross-mission state log is scoped by. Story position 6 (`C1/M02`) is the
    /// first mission in world folder 1, so nothing precedes it there; position 8 (`C1/M04`) follows
    /// it, and the two `C1B`/`C1C` missions between them are a different folder and are skipped.</summary>
    [ExtractedDataFact]
    public void TheBackwardsWalkFindsThePreviousMissionOfTheSameChapter()
    {
        var missions = Load();

        Assert.Null(CampaignSequence.PreviousInSameChapter(missions, 6));
        var previous = CampaignSequence.PreviousInSameChapter(missions, 8);
        Assert.NotNull(previous);
        Assert.Equal(6, previous!.Value.Seq);
        Assert.Equal(102, previous.Value.SaveId);
        Assert.Equal(1, CampaignSequence.Chapter(missions, 8));
    }

    private static System.Collections.Generic.List<CampaignMission> Load() =>
        CampaignSequence.Load(
            SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip")));
}
