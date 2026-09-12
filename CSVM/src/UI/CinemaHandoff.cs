using System;

namespace CSVM.UI;

/// <summary>How a cinema reaches the screen: the film's name, the continuation to run on the frame
/// it stops (played out or skipped), and the presses that end it early.
/// <c>Session/Launcher.cs</c>'s <c>PlayCinema</c> has this shape and is handed over as itself; a
/// suite hands over a stand-in that records what it was asked for. Every flow that plays a film
/// takes this one type, so a caller writes the call once and a suite's recorder fits all of
/// them.</summary>
public delegate void CinemaPlay(string name, Action then, CinemaSkip skip);

/// <summary>What a cinema flow does with the continuation it was handed. The chapter films and the
/// closing film each open one screen when a film stops, and both open it through the same latch.
/// The boot block chains its films unwrapped, since each continuation starts the next film rather
/// than opening a screen.</summary>
public static class CinemaHandoff
{
    /// <summary>Wraps a continuation so it runs on the first call and never again. ⚠ Do not hand a
    /// continuation to a cinema unwrapped. A skip can land on the frame the film plays out and both
    /// paths end the film, which is what the original's own EC latch stands against, so the next
    /// screen opens once however many times the cinema says it stopped.</summary>
    public static Action Once(Action handoff)
    {
        bool ran = false;
        return () =>
        {
            if (ran)
            {
                return;
            }

            ran = true;
            handoff();
        };
    }
}
