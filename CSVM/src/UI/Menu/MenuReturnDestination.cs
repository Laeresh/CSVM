namespace CSVM.UI.Menu;

/// <summary>
/// Where the menu should stand when it comes back, said semantically so the host never names a
/// screen. Each presentation maps a destination into its own graph on
/// <see cref="IMenuPresentation.Activate"/>: two presentations may land the same destination on
/// entirely different screens, and a destination a graph lacks maps to the nearest one it has.
/// The hierarchy is closed: only these three destinations exist.
/// </summary>
public abstract record MenuReturnDestination
{
    /// <summary>The presentation's own top level: a cold start, a quit to menu, and always the
    /// landing point after a presentation switch.</summary>
    public static readonly MenuReturnDestination TopLevel = new TopLevelReturn();

    private protected MenuReturnDestination()
    {
    }
}

/// <summary>The presentation's own top level. Use <see cref="MenuReturnDestination.TopLevel"/>.</summary>
public sealed record TopLevelReturn : MenuReturnDestination;

/// <summary>Back to the campaign hub for the named profile, after leaving a campaign screen or
/// abandoning a mission.</summary>
public sealed record CabinReturn(string Profile) : MenuReturnDestination;

/// <summary>Back to the campaign's results for one flown mission: the named profile, opened on
/// the story position just flown.</summary>
public sealed record DebriefReturn(string Profile, int MissionSeq) : MenuReturnDestination;
