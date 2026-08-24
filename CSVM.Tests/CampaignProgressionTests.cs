using System.Linq;
using CSVM.Session;
using Xunit;

namespace CSVM.Tests;

/// <summary>The progression rules over a profile: the two-half mission record and its best-of
/// merge, the monotonic position that only a completed primary objective raises, the replay rule
/// Previous Missions flies under, and the five aircraft awards granted once per profile.</summary>
public class CampaignProgressionTests
{
    [Fact]
    public void CompletingTheFirstMissionAdvancesOneStep()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");

        var recorded = CampaignProgression.Record(profile, Attempt(0, mask: 1, timeMs: 45000));

        Assert.True(recorded.PrimaryCompleted);
        Assert.True(recorded.Advanced);
        Assert.Equal(1, profile.MissionsCompleted);
        Assert.Equal(1, CampaignProgression.NextMissionSeq(profile));
    }

    /// <summary>Bit 0 gates everything: an attempt that completes secondary objectives only records
    /// its statistics as the latest attempt, merges nothing, and leaves the position alone.</summary>
    [Fact]
    public void AnAttemptWithoutThePrimaryObjectiveRecordsStatisticsAndNothingElse()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");

        var recorded = CampaignProgression.Record(profile, Attempt(0, mask: 4, timeMs: 61000));

        Assert.False(recorded.PrimaryCompleted);
        Assert.False(recorded.Advanced);
        Assert.Equal(0, profile.MissionsCompleted);
        var result = Assert.Single(profile.MissionResults);
        Assert.Equal(4, result.Latest.CompletedMask);
        Assert.Equal(0, result.Best.CompletedMask);
        Assert.Empty(CampaignProgression.CompletedSeqs(profile));
    }

    /// <summary>Previous Missions: replaying a finished mission records the attempt and merges the
    /// best-of, but the position cannot move for a mission at or below the progress already
    /// stored.</summary>
    [Fact]
    public void ReplayingAFinishedMissionNeverAdvances()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0, mask: 1, timeMs: 45000));
        CampaignProgression.Record(profile, Attempt(1, mask: 1, timeMs: 50000));

        var replay = CampaignProgression.Record(profile, Attempt(0, mask: 3, timeMs: 40000));

        Assert.True(replay.PrimaryCompleted);
        Assert.False(replay.Advanced);
        Assert.Equal(2, profile.MissionsCompleted);
        Assert.True(CampaignProgression.CanFly(profile, 0));
        Assert.True(CampaignProgression.CanFly(profile, 2));
        Assert.False(CampaignProgression.CanFly(profile, 3));
    }

    [Fact]
    public void TheBestOfMergeKeepsTheBetterHalfOfEachField()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, new MissionAttempt(0, 1, 45000, 120, 30, 900, 5, "Gypsy Magic"));

        CampaignProgression.Record(profile, new MissionAttempt(0, 5, 61000, 40, 30, 900, 3, "The Knave"));

        var result = Assert.Single(profile.MissionResults);
        Assert.Equal(5, result.Best.CompletedMask);
        Assert.Equal(45000, result.Best.TimeMs);
        Assert.Equal(40, result.Best.Shots);
        Assert.Equal(30, result.Best.Hits);
        Assert.Equal(1800, result.Best.Money);
        Assert.Equal(3, result.Best.Airframe);
        Assert.Equal("The Knave", result.Best.PlaneName);
        Assert.Equal(61000, result.Latest.TimeMs);
    }

    /// <summary>The award is marked per airframe, so the second flight of the same mission grants
    /// nothing however it is reached.</summary>
    [Fact]
    public void AnAircraftAwardIsGrantedOncePerProfile()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0, mask: 1, timeMs: 45000));

        var first = CampaignProgression.Record(profile, Attempt(1, mask: 3, timeMs: 50000));
        var second = CampaignProgression.Record(profile, Attempt(1, mask: 3, timeMs: 48000));

        Assert.Equal(new[] { 2 }, first.AwardedAirframes.ToArray());
        Assert.Empty(second.AwardedAirframes);
        Assert.Equal(new[] { 2 }, profile.GrantedAircraft.ToArray());
        var awarded = Assert.Single(profile.Planes, p => p.Name == "Jumping Jane");
        Assert.True(awarded.Special);
        Assert.Equal(2, awarded.Airframe);
    }

    /// <summary>The award's own objective bit gates it: mission 2 pays on bit 1, so completing the
    /// mission without that objective grants nothing and leaves the award still owed.</summary>
    [Fact]
    public void AnAwardWhoseObjectiveWasNotCompletedIsNotGranted()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        CampaignProgression.Record(profile, Attempt(0, mask: 1, timeMs: 45000));

        var recorded = CampaignProgression.Record(profile, Attempt(1, mask: 1, timeMs: 50000));

        Assert.Empty(recorded.AwardedAirframes);
        Assert.Empty(profile.GrantedAircraft);
        Assert.Equal(new[] { 2, 7, 13, 17, 19 },
            CampaignProgression.AircraftAwards.Select(a => a.Ordinal).ToArray());
    }

    [Fact]
    public void TheCampaignEndsAfterTwentyFourMissions()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        for (int seq = 0; seq < 24; seq++)
        {
            CampaignProgression.Record(profile, Attempt(seq, mask: 1, timeMs: 45000));
        }

        Assert.Equal(24, profile.MissionsCompleted);
        Assert.True(CampaignProgression.Complete(profile));
        Assert.Equal(23, CampaignProgression.NextMissionSeq(profile));
        Assert.False(CampaignProgression.CanFly(profile, 24));
    }

    private static MissionAttempt Attempt(int seq, int mask, int timeMs) =>
        new(seq, mask, timeMs, 100, 25, 0, 5, "Gypsy Magic");
}
