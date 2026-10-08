using System;
using CSVM.Spec;

namespace CSVM.Session.Roster;

/// <summary>Which aircraft each human in the field flies, read off a <see cref="SessionSpec"/>'s
/// plane roster. Pure over the spec, so these take it explicitly rather than caching one. The
/// display name of the aircraft once it is built is <see cref="Flight.Airframe.PlaneRoster"/>'s.</summary>
public static class HumanFieldPlanes
{
    /// <summary>The plane player <paramref name="index"/> flies: their own pick when the
    /// launchscreen (or a --plane= list) gave one, else the last one named. A single --plane=
    /// therefore puts everybody in the same aircraft.</summary>
    public static string PlaneFor(SessionSpec spec, int index) =>
        spec.PlaneNames.Count == 0 ? spec.PlaneName : spec.PlaneNames[Math.Min(index, spec.PlaneNames.Count - 1)];

    /// <summary>The aircraft an Instant Action mission forces on every human, or null to leave
    /// each player the pick <see cref="PlaneFor"/> returns. A def read off a file (<c>--ia=</c>,
    /// <c>ia.zrd.json</c>) names one <c>player_plane</c> that no human picked, so it binds all of
    /// them. The launchscreen wizard's def carries PLAYER 1's pick in that same field
    /// (<c>LaunchMenu.FireLaunch</c>). Honouring it would fly P2..P4 in player 1's aircraft.</summary>
    public static string? InstantActionOverride(SessionSpec spec, string? iaPlayerNode) =>
        spec.IaDef != null ? null : iaPlayerNode;
}
