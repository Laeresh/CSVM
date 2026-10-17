using System;
using System.Collections.Generic;
using CSVM.Flight.Modes;
using CSVM.UI.Boards;

namespace CSVM.UI.Overlays;

/// <summary>What one session's held Display Scores reads: its race or its Dogfight, the seats' names
/// and flags, and the original's header words. A source with neither mode shows nothing, which is
/// every solo and campaign flight.</summary>
public sealed class ScoresSource
{
    /// <summary>The race, or null outside one.</summary>
    public StuntRace? Race { get; init; }

    /// <summary>The Dogfight, or null outside one.</summary>
    public VersusMatch? Match { get; init; }

    /// <summary>A seat's name: the network callsign, else the splitscreen tag.</summary>
    public Func<int, string> Name { get; init; } = SplitScreen.PlayerTag;

    /// <summary>The team whose flag a seat carries, 0 for none; null outside Capture the Flag.</summary>
    public Func<int, int>? Carried { get; init; }

    /// <summary>The original's header words.</summary>
    public OriginalScoresWords Words { get; init; } = OriginalScoresWords.Fallback;

    /// <summary>Whether a mode with scores stands behind this source.</summary>
    public bool HasScores => Race != null || Match != null;

    /// <summary>The Original text, empty with no mode.</summary>
    public IReadOnlyList<OriginalScoresLine> OriginalLines() =>
        Race != null ? OriginalScoresText.Race(Race, Words)
        : Match != null ? OriginalScoresText.Dogfight(Match, Name, Carried, Words)
        : Array.Empty<OriginalScoresLine>();
}
