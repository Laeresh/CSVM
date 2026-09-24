using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;

namespace CSVM.UI.Menu;

/// <summary>One seat's confirmed selection inside a launch: the plane node to build, the pad
/// devices that seat claimed in the join flow, its loadout edits (null for the stock fit) and its
/// hangar build (null for a stock pick). A store-picked custom plane is resolved to its def before
/// the exit is built, so the consumer never reads a store.</summary>
public sealed record MenuSeatChoice(
    string PlaneNode,
    IReadOnlyList<int> Pads,
    LoadoutChoice? Fit = null,
    CustomPlaneDef? Custom = null);

/// <summary>
/// The one typed way any presentation leaves the menu, handed to <see cref="IMenuHost.Exit"/> and
/// consumed by <c>Launcher</c>. A presentation names what it wants (<see cref="LaunchExit"/>,
/// <see cref="CampaignMissionExit"/>, <see cref="QuitExit"/>, <see cref="OptionsApplyExit"/>)
/// and never constructs a session, hides itself or reads the launch machinery. The hierarchy is
/// closed: only these four exist.
/// </summary>
public abstract record MenuExit
{
    private protected MenuExit()
    {
    }
}

/// <summary>The player backed out of the top level: the host quits the process.</summary>
public sealed record QuitExit : MenuExit;

/// <summary>The player applied an Options screen: the consumer persists every choice it carries,
/// then shows the active presentation again at its top level. Which presentation that is stays the
/// command line's, never an option. Carried are a <see cref="Utils.GraphicsMode"/> word saved and
/// no more, a <see cref="Flight.Difficulty.Word"/> the next launch reads, and the rest of
/// <see cref="Utils.OptionsDef"/>'s own fields, null where never set. A screen showing none of them
/// hands back what it read, since the consumer writes every field it is given. ⚠ All fourteen ride
/// the exit, not the screen's own save, so the options file keeps one writer, and none is
/// defaulted. ⚠ A field no screen offers is dropped rather than left riding as a null, since a null
/// the consumer saves wipes the file's value.</summary>
public sealed record OptionsApplyExit(
    string Graphics,
    string Difficulty,
    string? MonitorIndex,
    string? Resolution,
    string? DisplayMode,
    string? VSync,
    int? AudioMaster,
    int? AudioMusic,
    int? AudioEffects,
    int? AudioVoice,
    bool? NearestAfterKill,
    bool? Rumble,
    string? DefaultView,
    bool? AutoHeadTurn) : MenuExit;

/// <summary>Dogfight's two match rules as a screen set them: the kill target that ends a match
/// early and the match clock in MINUTES, 0 on either disabling that limit. The consumer applies
/// them under the command line, so an explicit <c>--vs-kills=</c>/<c>--vs-time=</c> still wins.</summary>
public sealed record VersusRules(int KillTarget, int TimeLimitMinutes);

/// <summary>A non-campaign launch: the chapter, one <see cref="MenuSeatChoice"/> per joined seat
/// in seat order, and the picked <see cref="MenuMode"/>. Instant Action alone adds the wizard's
/// built <see cref="InstantActionDef"/> and the wingmen's edited fit (null for the stock fit).
/// Dogfight alone adds the match rules; a null <paramref name="Match"/> leaves the command
/// line's own kill target and time limit.</summary>
public sealed record LaunchExit(
    string Chapter,
    IReadOnlyList<MenuSeatChoice> Seats,
    MenuMode Mode,
    InstantActionDef? InstantAction = null,
    VersusRules? Match = null,
    LoadoutChoice? WingmanLoadout = null) : MenuExit;

/// <summary>A campaign mission launch: the seated profile's name, the <c>cm_sequence</c> story
/// position, and one <see cref="MenuSeatChoice"/> per joined human in seat order. Seat 0 is the
/// seated profile's pilot; later seats are guests whose records never touch the profile store.</summary>
public sealed record CampaignMissionExit(
    string Profile,
    int MissionSeq,
    IReadOnlyList<MenuSeatChoice> Seats) : MenuExit;
