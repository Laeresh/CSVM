using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;

namespace CSVM.Session;

/// <summary>One flown mission's raw statistics, as a mission director hands them over at mission
/// end. <c>Seq</c> is the <c>cm_sequence</c> story position; <c>CompletedMask</c> is the run's
/// completed-objective bitmask, whose bit 0 is the primary objective. <c>Kills</c> and
/// <c>AceKills</c> are <see cref="CampaignProgression.AirframeCount"/>-slot per-airframe kill
/// tallies, plain and ace (<c>docs/org/debrief.md#what-the-tallies-count</c>); a caller that omits
/// them reports a mission with no aircraft kills.</summary>
public readonly record struct MissionAttempt(
    int Seq, int CompletedMask, int TimeMs, int Shots, int Hits, int Money,
    int Airframe, string PlaneName, int[]? Kills = null, int[]? AceKills = null)
{
    public int[] Kills { get; init; } = Kills ?? new int[CampaignProgression.AirframeCount];

    public int[] AceKills { get; init; } = AceKills ?? new int[CampaignProgression.AirframeCount];
}

/// <summary>What recording an attempt changed: whether it completed the primary objective, whether
/// it raised the campaign position, the airframe ids of any aircraft awards it granted, the builds
/// those awards fly, which the caller persists to the build store its launches read, and whether
/// this failure was the one that raises the skip offer, which the screen showing the result asks
/// the player and answers through <see cref="CampaignProgression.AcceptSkip"/>.</summary>
public readonly record struct MissionRecorded(
    bool PrimaryCompleted, bool Advanced, IReadOnlyList<int> AwardedAirframes,
    IReadOnlyList<CustomPlaneDef> AwardedBuilds, bool SkipOffered = false);

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
    /// <summary>The completed-objective mask bit that means the mission was won, and with it the
    /// primary objective. It gates the whole best-of merge and the advance. Every other bit is an
    /// <c>IDENTITY</c> priority, which is why this one is free to mean the mission: the shipped
    /// data numbers priorities from 1 (docs/formats/objectives.md, "IDENTITY and the objectives
    /// display").</summary>
    public const int PrimaryObjectiveMask = 1;

    /// <summary>The eleven airframes the debrief's kill tallies index, in the engine's own order
    /// (Hoplite, Hellhound, Balmoral, Bloodhawk, Brigand, Devastator, Firebrand, Fury, Kestrel,
    /// Peacemaker, Warhawk; docs/org/debrief.md#what-the-tallies-count).</summary>
    public const int AirframeCount = 11;

    /// <summary>How many failed attempts at an uncompleted mission raise the offer to skip it. The
    /// original tests <c>counter % 4 == 0</c> on a counter it never resets while the mission stands
    /// uncompleted, so the offer returns on every fourth failure and this is a total rather than a
    /// consecutive-failure count (<c>docs/org/debrief.md</c>, "The four-attempt skip offer").</summary>
    public const int SkipOfferAttempts = 4;

    /// <summary>⚠ A PLACEHOLDER wording, not the original's. The offer is langui string 191, which
    /// is absent from the shipped extraction (its ids jump from 136 to 200), so no original text
    /// exists to quote here. Formatted with <see cref="SkipOfferAttempts"/>, which is the literal
    /// the original passes its own string.</summary>
    public const string SkipOfferPrompt =
        "You have failed this mission {0} times. Skip it and go on with the campaign?";

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

    // The gun-slot dropdown's explicit empty row, one past the last calibre, as the record stores
    // it (docs/formats/paint.md).
    private const int AwardGunEmpty = 5;

    // One award's build, in the 204-byte record's own field order (docs/formats/paint.md, "Saved
    // custom planes"): airframe, engine, the two wing hardpoint counts, the four armour zones in
    // units, the twin-mount bit field, then the four gun ids with 5 for an empty slot.
    private static readonly int[][] AwardTemplates =
    {
        new[] { 0, 1, 1, 1, 3, 3, 3, 3, 1, 0, 5, 5, 5 },
        new[] { 2, 1, 4, 4, 8, 7, 5, 5, 3, 2, 2, 0, 0 },
        new[] { 3, 4, 1, 1, 4, 4, 4, 4, 3, 1, 0, 5, 5 },
        new[] { 7, 1, 2, 1, 5, 5, 4, 4, 3, 4, 0, 5, 5 },
        new[] { 10, 1, 4, 4, 6, 6, 6, 6, 3, 4, 2, 5, 5 },
    };

    // The paint the five templates share, byte for byte: pattern, the three swatch rows, their
    // shade variants, and the nose, tail and wing decals.
    private static readonly int[] AwardPaint = { 4, 1, 26, 26, 8, 0, 9, 40, 8, 7 };

    /// <summary>The build a granted award flies, out of the original's special-plane template array
    /// at <c>0x0061a9b8</c>: class-2 204-byte records matched on airframe id, which
    /// <c>FUN_00405f00</c> copies whole into the profile's new plane slot before naming it. Null
    /// for an airframe the table does not award. A fresh def per call, so a caller may name and
    /// store it. The Blue Streak's engine 4 is the nitrous tier a stock Bloodhawk never has.</summary>
    public static CustomPlaneDef? AwardBuild(int airframe)
    {
        foreach (var row in AwardTemplates)
        {
            if (row[0] != airframe)
            {
                continue;
            }

            var def = new CustomPlaneDef
            {
                Airframe = row[0],
                Engine = row[1],
                LeftHardpoints = row[2],
                RightHardpoints = row[3],
                ArmourNose = row[4],
                ArmourTail = row[5],
                ArmourLeftWing = row[6],
                ArmourRightWing = row[7],
                PaintPattern = AwardPaint[0],
                NoseDecal = AwardPaint[7],
                TailDecal = AwardPaint[8],
                WingDecal = AwardPaint[9],
            };
            for (int slot = 0; slot < HangarPaintTables.Slots; slot++)
            {
                def.PaintColours[slot] = AwardPaint[1 + slot];
                def.PaintShades[slot] = AwardPaint[4 + slot];
            }

            for (int slot = 0; slot < def.Guns.Length; slot++)
            {
                int id = row[9 + slot];
                def.Guns[slot] = new GunChoice(id == AwardGunEmpty ? null : id, (row[8] & (1 << slot)) != 0);
            }

            return def;
        }

        return null;
    }

    /// <summary>The build an owned plane flies when the build store holds none under its name: an
    /// award's own template, since in the original the profile's plane record IS the build and
    /// there is no second place to look. Null for anything else, which leaves a hangar-built plane
    /// and the two starter Devastators on the store's own answer.</summary>
    public static CustomPlaneDef? BuildForOwned(OwnedPlane plane)
    {
        ArgumentNullException.ThrowIfNull(plane);
        if (!plane.Special || AwardBuild(plane.Airframe) is not { } build)
        {
            return null;
        }

        build.Name = plane.Name;
        return build;
    }

    /// <summary>Records one attempt: its statistics into the mission's record, and, when it
    /// completed the primary objective, the best-of merge, the money, the position raise and any
    /// aircraft award. An attempt that leaves bit 0 clear records statistics, counts itself against
    /// the skip offer and nothing else.</summary>
    public static MissionRecorded Record(CampaignProfileDef profile, MissionAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var result = ResultFor(profile, attempt.Seq);
        result.Latest = RunOf(attempt);
        bool primary = (attempt.CompletedMask & PrimaryObjectiveMask) != 0;
        if (!primary)
        {
            return new MissionRecorded(
                false, false, Array.Empty<int>(), Array.Empty<CustomPlaneDef>(), CountFailure(result));
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

        return new MissionRecorded(true, advanced, awarded.Airframes, awarded.Builds);
    }

    /// <summary>Takes the skip offer <see cref="MissionRecorded.SkipOffered"/> raised. The original
    /// answers Yes by setting the campaign win flag and re-entering the debrief, so the skip is a
    /// synthetic win and nothing more: the same attempt is recorded again with the primary bit set,
    /// which merges the best-of, banks the money, pays whatever the mask awards and advances the
    /// position. <paramref name="capture"/> is the failed attempt's world state, which the win flag
    /// is what makes the save gate pass on (<c>docs/org/debrief.md</c>).</summary>
    public static MissionRecorded AcceptSkip(
        CampaignProfileDef profile, MissionAttempt attempt, int chapter,
        IEnumerable<PersistedObject>? capture = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var recorded = Record(
            profile, attempt with { CompletedMask = attempt.CompletedMask | PrimaryObjectiveMask });
        if (capture != null)
        {
            profile.PersistLog.Merge(chapter, attempt.Seq, capture);
        }

        return recorded;
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
    // better hit ratio, accumulate the money, take the plane of a run that completed more, and
    // merge both kill tallies per index by maximum.
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
        MergeMax(best.Kills, attempt.Kills);
        MergeMax(best.AceKills, attempt.AceKills);
    }

    // The skip offer's gate, whose two halves are both the original's: the counter moves only while
    // the mission has never been completed, so a lost replay of a finished mission never offers a
    // skip, and it is never reset, so a fifth failure counts 5 rather than starting again at 1.
    private static bool CountFailure(MissionResult result)
    {
        if ((result.Best.CompletedMask & PrimaryObjectiveMask) != 0)
        {
            return false;
        }

        result.Attempts++;
        return result.Attempts % SkipOfferAttempts == 0;
    }

    private static void MergeMax(int[] best, int[] attempt)
    {
        for (int i = 0; i < AirframeCount; i++)
        {
            if (attempt[i] > best[i])
            {
                best[i] = attempt[i];
            }
        }
    }

    private static (List<int> Airframes, List<CustomPlaneDef> Builds) GrantAwards(
        CampaignProfileDef profile, MissionAttempt attempt, int maskBefore)
    {
        var awarded = new List<int>();
        var builds = new List<CustomPlaneDef>();
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
            if (AwardBuild(award.Airframe) is { } build)
            {
                build.Name = award.Name;
                builds.Add(build);
            }
        }

        return (awarded, builds);
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
        Kills = (int[])attempt.Kills.Clone(),
        AceKills = (int[])attempt.AceKills.Clone(),
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
