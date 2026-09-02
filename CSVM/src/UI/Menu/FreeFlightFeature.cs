using System;
using System.Collections.Generic;

namespace CSVM.UI.Menu;

/// <summary>
/// Free Flight as a shared feature: the chapter roster it offers, the chapter picked, the launch
/// gate, and the typed <see cref="LaunchExit"/> a launch leaves through. Free Flight is the
/// remake's own mode, so nothing here is decoded; the rules are the launchscreen's, moved. A
/// presentation offers the roster however it likes, tells the feature which chapter was picked,
/// and asks for the exit once every joined seat has confirmed an aircraft. The seats themselves
/// stay the presentation's until the shared player setup owns them.
/// </summary>
public sealed class FreeFlightFeature : IMenuFeature
{
    /// <summary>The mode every exit this feature builds carries.</summary>
    public const MenuMode Mode = MenuMode.Free;

    /// <summary>The chapters offered: all eight, none withheld, in the shared roster's order.</summary>
    public IReadOnlyList<MenuChapter> Chapters => MenuChapters.For(Mode);

    /// <summary>The picked chapter, or null until a presentation has picked one.</summary>
    public MenuChapter? Chapter { get; private set; }

    /// <summary>Picks a chapter by code. A code outside the roster throws: the roster is the whole
    /// offer, so a presentation naming anything else has a wiring error, not a choice.</summary>
    public void SelectChapter(string code)
    {
        foreach (var chapter in Chapters)
        {
            if (chapter.Code == code)
            {
                Chapter = chapter;
                return;
            }
        }

        throw new ArgumentException($"'{code}' is not a Free Flight chapter", nameof(code));
    }

    /// <summary>Why a launch is refused right now, or null when it may go: a chapter must be
    /// picked, at least one seat must be joined, and every joined seat must have confirmed. A
    /// lone seat launches; no second one is ever required.</summary>
    public string? Refusal(int joinedSeats, int confirmedSeats)
    {
        if (Chapter == null)
        {
            return "no chapter picked";
        }

        if (joinedSeats < 1)
        {
            return "no seat joined";
        }

        if (confirmedSeats != joinedSeats)
        {
            return $"{joinedSeats - confirmedSeats} of {joinedSeats} seats not confirmed";
        }

        return null;
    }

    /// <summary>Whether the launch gate is open; see <see cref="Refusal"/> for the rule.</summary>
    public bool CanLaunch(int joinedSeats, int confirmedSeats) => Refusal(joinedSeats, confirmedSeats) == null;

    /// <summary>The typed exit for the confirmed seats, in seat order. Throws when the gate is
    /// closed or a seat names no plane, so a half-built launch cannot leave the menu.</summary>
    public LaunchExit BuildExit(IReadOnlyList<MenuSeatChoice> seats)
    {
        ArgumentNullException.ThrowIfNull(seats);
        if (Refusal(seats.Count, seats.Count) is { } refusal)
        {
            throw new InvalidOperationException($"Free Flight cannot launch: {refusal}");
        }

        for (int i = 0; i < seats.Count; i++)
        {
            if (string.IsNullOrEmpty(seats[i].PlaneNode))
            {
                throw new InvalidOperationException($"Free Flight cannot launch: seat {i + 1} names no plane");
            }
        }

        return new LaunchExit(Chapter!.Value.Code, seats, Mode);
    }

    /// <summary>Drops the picked chapter. The roster is not state and stays.</summary>
    public void Discard() => Chapter = null;
}
