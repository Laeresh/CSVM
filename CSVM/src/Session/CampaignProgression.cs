using System;
using System.Collections.Generic;
using CSVM.Mech3;

namespace CSVM.Session;

/// <summary>One flown mission's raw statistics, as a mission director hands them over at mission
/// end. <c>Seq</c> is the <c>cm_sequence</c> story position; <c>CompletedMask</c> is the run's
/// completed-objective bitmask, whose bit 0 is the primary objective.</summary>
public readonly record struct MissionAttempt(
    int Seq, int CompletedMask, int TimeMs, int Shots, int Hits, int Money,
    int Airframe, string PlaneName);

/// <summary>What recording an attempt changed: whether it completed the primary objective, whether
/// it raised the campaign position, and the airframe ids of any aircraft awards it granted.</summary>
public readonly record struct MissionRecorded(
    bool PrimaryCompleted, bool Advanced, IReadOnlyList<int> AwardedAirframes);

/// <summary>One record of the campaign's aircraft-award table: the mission ordinal that pays it,
/// the objective bit it is gated on, and the airframe and plane name it grants
/// (<c>docs/org/hangar.md</c>, "The mission reward table").</summary>
public readonly record struct AircraftAward(int Ordinal, int ObjectiveBit, int Airframe, string Name);

/// <summary>
/// The campaign's progression rules over a profile: recording a mission's result with the
/// original's best-of merge, raising the position, and granting the aircraft awards. The position
/// is a single monotonic integer, never a graph, because <c>cm_sequence.zrd</c> is a flat list with
/// no predicate and no alternates (docs/formats/campaign-sequence.md).
///
/// The cash half of the reward table is deliberately not here: this class banks the money an
/// attempt reports, and which objective pays what is the hangar economy's own table.
/// </summary>
public static class CampaignProgression
{
    /// <summary>The completed-objective mask bit that means the primary objective, and with it the
    /// mission itself. It gates the whole best-of merge and the advance.</summary>
    public const int PrimaryObjectiveMask = 1;

    /// <summary>The five aircraft awards, keyed by the 1-based mission ordinal (not by <c>seq</c>
    /// and not by the save id). Each is granted once per profile: the original marks a per-airframe
    /// byte, so replaying the mission grants nothing. Names are <c>langui</c> 513-517.</summary>
    public static readonly IReadOnlyList<AircraftAward> AircraftAwards = new[]
    {
        new AircraftAward(2, 1, 2, "Jumping Jane"),
        new AircraftAward(7, 1, 3, "Blue Streak"),
        new AircraftAward(13, 0, 7, "Red Hot Spender"),
        new AircraftAward(17, 0, 0, "Minx"),
        new AircraftAward(19, 1, 10, "Accipiter Annie"),
    };

    /// <summary>Records one attempt: its statistics into the mission's record, and, when it
    /// completed the primary objective, the best-of merge, the money, the position raise and any
    /// aircraft award. An attempt that leaves bit 0 clear records statistics and nothing
    /// else.</summary>
    public static MissionRecorded Record(CampaignProfileDef profile, MissionAttempt attempt)
    {
        var result = ResultFor(profile, attempt.Seq);
        result.Latest = RunOf(attempt);
        bool primary = (attempt.CompletedMask & PrimaryObjectiveMask) != 0;
        if (!primary)
        {
            return new MissionRecorded(false, false, Array.Empty<int>());
        }

        int maskBefore = result.Best.CompletedMask;
        MergeBest(result.Best, attempt);
        profile.Funds += attempt.Money;
        var awarded = GrantAwards(profile, attempt, maskBefore);
        bool advanced = attempt.Seq + 1 > profile.MissionsCompleted
            && attempt.Seq >= 0 && attempt.Seq < CampaignSequence.MissionCount;
        if (advanced)
        {
            profile.MissionsCompleted = attempt.Seq + 1;
        }

        return new MissionRecorded(true, advanced, awarded);
    }

    /// <summary>The story position Next Mission resolves to: the profile's own progress, clamped to
    /// the last mission. The original's cabin asks with a <c>-2</c> sentinel and gets
    /// <c>progress + 1</c> as a 1-based ordinal, which is this same mission.</summary>
    public static int NextMissionSeq(CampaignProfileDef profile) =>
        Math.Clamp(profile.MissionsCompleted, 0, CampaignSequence.MissionCount - 1);

    /// <summary>Whether the campaign has been finished, which is what disables New Mission.</summary>
    public static bool Complete(CampaignProfileDef profile) =>
        profile.MissionsCompleted >= CampaignSequence.MissionCount;

    /// <summary>Whether a mission may be flown from this profile: anything up to and including the
    /// next one. A lower position is Previous Missions replaying a finished mission, which records
    /// its statistics but can never lower the position or advance it. Anything beyond the next
    /// mission is the original's cheat path, not an ordinary selection.</summary>
    public static bool CanFly(CampaignProfileDef profile, int seq) =>
        seq >= 0 && seq < CampaignSequence.MissionCount && seq <= profile.MissionsCompleted;

    /// <summary>The record of a mission this profile has attempted, or null when it has not.</summary>
    public static MissionResult? ResultOf(CampaignProfileDef profile, int seq)
    {
        foreach (var result in profile.MissionResults)
        {
            if (result.Seq == seq)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>The story positions this profile has completed, in sequence order, which is what
    /// the cabin's Previous Missions list offers.</summary>
    public static List<int> CompletedSeqs(CampaignProfileDef profile)
    {
        var seqs = new List<int>();
        foreach (var result in profile.MissionResults)
        {
            if ((result.Best.CompletedMask & PrimaryObjectiveMask) != 0)
            {
                seqs.Add(result.Seq);
            }
        }

        seqs.Sort();
        return seqs;
    }

    // The merge rules are the original's own, one per field (docs/formats/saved-games.md, "The
    // mission-result array"): OR the mask, keep the faster time ignoring 0, keep the pair with the
    // better hit ratio, accumulate the money, and take the plane of a run that completed more.
    private static void MergeBest(MissionRun best, MissionAttempt attempt)
    {
        if (attempt.TimeMs > 0 && (best.TimeMs == 0 || attempt.TimeMs < best.TimeMs))
        {
            best.TimeMs = attempt.TimeMs;
        }

        if (Ratio(attempt.Hits, attempt.Shots) > Ratio(best.Hits, best.Shots))
        {
            best.Shots = attempt.Shots;
            best.Hits = attempt.Hits;
        }

        if (Bits(attempt.CompletedMask) > Bits(best.CompletedMask))
        {
            best.Airframe = attempt.Airframe;
            best.PlaneName = attempt.PlaneName;
        }

        best.Money += attempt.Money;
        best.CompletedMask |= attempt.CompletedMask;
    }

    private static List<int> GrantAwards(
        CampaignProfileDef profile, MissionAttempt attempt, int maskBefore)
    {
        var awarded = new List<int>();
        foreach (var award in AircraftAwards)
        {
            int bit = 1 << award.ObjectiveBit;
            if (award.Ordinal != attempt.Seq + 1
                || (attempt.CompletedMask & bit) == 0
                || (maskBefore & bit) != 0
                || profile.GrantedAircraft.Contains(award.Airframe))
            {
                continue;
            }

            profile.GrantedAircraft.Add(award.Airframe);
            profile.Planes.Add(new OwnedPlane
            {
                Name = award.Name,
                Airframe = award.Airframe,
                Special = true,
            });
            awarded.Add(award.Airframe);
        }

        return awarded;
    }

    private static MissionResult ResultFor(CampaignProfileDef profile, int seq)
    {
        if (ResultOf(profile, seq) is { } existing)
        {
            return existing;
        }

        var result = new MissionResult { Seq = seq };
        profile.MissionResults.Add(result);
        return result;
    }

    private static MissionRun RunOf(MissionAttempt attempt) => new()
    {
        CompletedMask = attempt.CompletedMask,
        TimeMs = attempt.TimeMs,
        Shots = attempt.Shots,
        Hits = attempt.Hits,
        Money = attempt.Money,
        Airframe = attempt.Airframe,
        PlaneName = attempt.PlaneName ?? string.Empty,
    };

    private static float Ratio(int hits, int shots) => shots <= 0 ? 0f : hits / (float)shots;

    private static int Bits(int mask)
    {
        int count = 0;
        for (uint v = (uint)mask; v != 0; v >>= 1)
        {
            count += (int)(v & 1);
        }

        return count;
    }
}
