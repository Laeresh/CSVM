using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Flight.Modes;

namespace CSVM.UI.Screens;

/// <summary>One pilot's row of a stunt race's standings, every column in the race boards' words.
/// <c>Racer</c> is the record it was read off, for the seat and identity colour a board may wear.</summary>
public sealed record RaceRow(Racer Racer, string Place, string Name, string Aircraft, string Best, string Gap, string Runs)
{
    /// <summary>Whether the pilot left the race mid-race, which every board marks its own way.</summary>
    public bool Left => Racer.Left;
}

/// <summary>
/// The one reading of a stunt race's standings as board rows: place, name, aircraft, best, gap to the
/// winner and runs finished of runs started. The Built-in results board, both held Display Scores
/// looks and the Original scores page read these rows. Each lays them out its own way, and their
/// words agree. Engine-free. Module entry: docs/architecture/UI.md.
/// </summary>
public static class RaceRows
{
    /// <summary>The field in <paramref name="standings"/>' order, the first the winner whose best the
    /// gaps run to. <paramref name="zoneCount"/> is the course's.</summary>
    public static IReadOnlyList<RaceRow> Of(IReadOnlyList<Racer> standings, int zoneCount)
    {
        ArgumentNullException.ThrowIfNull(standings);
        float? winner = standings.Count > 0 ? standings[0].BestTime : null;
        var rows = new List<RaceRow>(standings.Count);
        for (int i = 0; i < standings.Count; i++)
        {
            var r = standings[i];
            rows.Add(new RaceRow(r, StuntRace.Ordinal(i + 1), NameText(r), r.PlaneDisplay, BestText(r, zoneCount), GapText(r, winner),
                string.Format(CultureInfo.InvariantCulture, "{0}/{1}", r.RunsFinished, r.RunsStarted)));
        }

        return rows;
    }

    /// <summary>A board's name for one pilot: the callsign, marked when the pilot left the race.</summary>
    public static string NameText(Racer racer)
    {
        ArgumentNullException.ThrowIfNull(racer);
        return racer.Left ? racer.Callsign + StuntRace.LeftSuffix : racer.Callsign;
    }

    /// <summary>A board's best column for one pilot: the best time, or the furthest run's zones out
    /// of <paramref name="zoneCount"/> for a pilot with no completed run.</summary>
    public static string BestText(Racer racer, int zoneCount)
    {
        ArgumentNullException.ThrowIfNull(racer);
        return racer.BestTime is { } best
            ? StuntMission.FormatTime(best)
            : string.Format(CultureInfo.InvariantCulture, "{0}/{1} ZONES", racer.MostZones, zoneCount);
    }

    /// <summary>A board's gap column for one pilot: behind <paramref name="winner"/>'s best, and
    /// blank for the winner. A pilot with no completed run shows the time to the furthest zones.
    /// </summary>
    public static string GapText(Racer racer, float? winner)
    {
        ArgumentNullException.ThrowIfNull(racer);
        if (racer.BestTime is { } best)
        {
            return winner is { } w && best > w ? StuntRace.FormatGap(best - w) : "";
        }

        return racer.MostZones > 0 ? "at " + StuntMission.FormatTime(racer.TimeToMostZones) : "";
    }
}
