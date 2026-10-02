using System.Collections.Generic;
using CSVM.Flight.Modes;

namespace CSVM.Session.InstantAction;

/// <summary>
/// The in-flight wrap-up board as <see cref="InstantActionDirector"/> drives it. The launch builds
/// one, hidden, while the director wires the ending, and the director calls
/// <see cref="Present"/> once, when the wrap-up is due. The board itself is a screen and lives
/// above this family, so the director holds it only through this.
/// </summary>
public interface IIaWrapupBoard
{
    /// <summary>Shows the board with the numbers the ending froze. The <paramref name="stunt"/> run
    /// is player 1's on a stunt mission and null elsewhere. The <paramref name="shots"/> camera is
    /// that pilot's, whose strip fills in as late frames land.</summary>
    void Present(IaWrapupSnapshot shown, StuntSummary? stunt, StuntCapture? shots);
}

/// <summary>
/// The final numbers one ended Instant Action mission hands its wrap-up. They are the outcome, the
/// context line naming the chapter and mission type, and the four counters the wrap-up draws. The
/// stunt run's split table comes already flattened to text, with player 1's Danger Zone
/// photographs in marker order. Every number is read at the ending and never again, so the record
/// can outlive the session node that made it. The splits are strings rather than a
/// <see cref="StuntSummary"/> for the same reason. The photographs are the camera's own records,
/// which a thumbnail still on its way completes in place after the session is gone
/// (<see cref="StuntShot.Landed"/>).
/// </summary>
public sealed record IaWrapupSnapshot(
    bool Won, string Context, float Elapsed, int EnemiesShotDown, int ZonesCompleted, int ShotPercent,
    IReadOnlyList<string>? StuntLines = null, IReadOnlyList<StuntShot>? Shots = null);
