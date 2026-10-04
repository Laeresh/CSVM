using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Flight;
using CSVM.Flight.Hangar;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session;
using CSVM.Session.Campaign;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.UI.Campaign;
using CSVM.UI.Hangar;

namespace CSVM.UI.Menu.Original;

/// <summary>The Multiplayer Lobby's four tabs, in the order the tab strip draws them.</summary>
public enum LobbyTab
{
    /// <summary>Mission Options: environment, type, victory, teams, lives and planes.</summary>
    Mission,

    /// <summary>Select Plane: the stock airframe or custom plane this pilot flies.</summary>
    Plane,

    /// <summary>Select Ammo: the gun ammunition and the rocket on each wing cell.</summary>
    Ammo,

    /// <summary>Game Scores, greyed until a match has been flown.</summary>
    Scores,
}

/// <summary>
/// The original's Multiplayer Lobby over <see cref="DogfightLobby"/>, drawn from the lobby scripts'
/// own placements (<c>docs/org/menu-inventory.md</c>). The player list, Ready, chat, Boot and Leave
/// Game stand on every tab, Boot removing the guest whose row a host picked. Only a Dogfight flies,
/// so Capture the Flag, the zeppelin mode and the teams draw greyed. Select... opens the outlaw list
/// over the tab page while Outlaw Components is ticked. The host's options lock while the host is
/// Ready. Every pilot's own picks stay live, and changing one clears that pilot's Ready.
/// </summary>
public sealed class OriginalLobbyScreen : IOriginalScreenModule
{
    /// <summary>The Mission Options tab.</summary>
    public const string MissionTabKey = "MPL_TAB_MISSION";

    /// <summary>The Select Plane tab.</summary>
    public const string PlaneTabKey = "MPL_TAB_PLANE";

    /// <summary>The Select Ammo tab.</summary>
    public const string AmmoTabKey = "MPL_TAB_AMMO";

    /// <summary>The Game Scores tab, greyed until a match has been flown from this lobby.</summary>
    public const string ScoresTabKey = "MPL_TAB_SCORES";

    /// <summary>The Mission Environment box.</summary>
    public const string EnvironmentKey = "MPL_D_ENVIRONMENT";

    /// <summary>The Mission Type box.</summary>
    public const string TypeKey = "MPL_D_TYPE";

    /// <summary>The line under the Type box for a Stunt Race, the remake's own type, so no string
    /// table id carries it. It is written in the register of the original's three.</summary>
    public const string StuntRaceDescription =
        "Race the chapter's Danger Zone course against the clock. Fly as many runs as the time allows. The fastest complete run wins.";

    /// <summary>LAUNCH!, live on the host once every pilot is Ready.</summary>
    public const string LaunchKey = "MPL_B_LAUNCH";

    /// <summary>The Time victory radio.</summary>
    public const string TimeRadioKey = "MPL_R_TIME";

    /// <summary>The Score victory radio.</summary>
    public const string ScoreRadioKey = "MPL_R_SCORE";

    /// <summary>The Time box, in minutes.</summary>
    public const string TimeKey = "MPL_E_TIME";

    /// <summary>The Score box.</summary>
    public const string ScoreKey = "MPL_E_SCORE";

    /// <summary>Restrict Number of Teams, live on the host.</summary>
    public const string TeamsKey = "MPL_C_TEAMS";

    /// <summary>The minimum team count box, live while Restrict Number of Teams is ticked.</summary>
    public const string MinTeamsKey = "MPL_E_MINTEAMS";

    /// <summary>The maximum team count box, live while Restrict Number of Teams is ticked.</summary>
    public const string MaxTeamsKey = "MPL_E_MAXTEAMS";

    /// <summary>Capture the Flag's own-flag-home rule, the host's option the original lacks. Shown
    /// only while the type is Capture the Flag.</summary>
    public const string FlagHomeKey = "MPL_C_FLAGHOME";

    /// <summary>The prefix of a team count box's arrows: <c>MIN+</c>, <c>MIN-</c>, <c>MAX+</c> and
    /// <c>MAX-</c> follow it.</summary>
    public const string TeamArrowPrefix = "MPL_B_TEAMS_";

    /// <summary>The prefix of a player list's team row, followed by the team's number, which a press
    /// picks for Join Team.</summary>
    public const string TeamRowKeyPrefix = "MPL_R_TEAM_";

    /// <summary>The Limited Lives checkbox.</summary>
    public const string LimitedLivesKey = "MPL_C_LIVES";

    /// <summary>The Lives box.</summary>
    public const string LivesKey = "MPL_E_LIVES";

    /// <summary>The Auto Respawn checkbox.</summary>
    public const string AutoRespawnKey = "MPL_C_RESPAWN";

    /// <summary>Allow Custom Planes, live on the host.</summary>
    public const string CustomPlanesKey = "MPL_C_CUSTOM";

    /// <summary>Outlaw Components, live on the host.</summary>
    public const string OutlawKey = "MPL_C_OUTLAW";

    /// <summary>The outlaw list's Select..., View... on a guest, live while Outlaw Components is
    /// ticked.</summary>
    public const string SelectKey = "MPL_B_SELECT";

    /// <summary>The Default Planes sub-tab.</summary>
    public const string DefaultPlanesKey = "MPL_TAB_DEFAULT";

    /// <summary>The Custom Planes sub-tab, live while the host allows custom planes.</summary>
    public const string CustomTabKey = "MPL_TAB_CUSTOM";

    /// <summary>The Select Plane box.</summary>
    public const string PlaneKey = "MPL_D_PLANE";

    /// <summary>The Guns sub-tab.</summary>
    public const string GunsTabKey = "MPL_TAB_GUNS";

    /// <summary>The Rockets sub-tab.</summary>
    public const string RocketsTabKey = "MPL_TAB_ROCKETS";

    /// <summary>Boot, live on the host while a guest's row is picked. The script's KDA, which its
    /// refresh 1301 mails live on the host with a player row picked; its press is <c>$$A$$</c> 1005.
    /// </summary>
    public const string BootKey = "MPL_B_BOOT";

    /// <summary>The prefix of a host's player list row, which a press picks for Boot.</summary>
    public const string PlayerKeyPrefix = "MPL_R_PLAYER_";

    /// <summary>The team button: Create Team on no team, Join Team with a team row picked, Leave
    /// Team on a team. Live while this pilot is not Ready, as the script's <c>GDA</c> gates it.
    /// </summary>
    public const string TeamKey = "MPL_B_TEAM";

    /// <summary>The large Ready checkbox.</summary>
    public const string ReadyKey = "MPL_C_READY";

    /// <summary>The chat edit box.</summary>
    public const string ChatKey = "MPL_E_CHAT";

    /// <summary>Send, which sends the chat box's line.</summary>
    public const string SendKey = "MPL_B_SEND";

    /// <summary>Leave Game, back to the Connection page.</summary>
    public const string LeaveKey = "MPL_B_LEAVE";

    /// <summary>The remake's COPY control on a host's first pinned Network row, which copies its
    /// join code or address. The row spans the line, so a click or a tap on the code copies it.
    /// </summary>
    public const string CopyKey = "MPL_B_COPY";

    /// <summary>How many rows the player list shows.</summary>
    public const int VisiblePlayers = 11;

    private const string GunKeyPrefix = "MPL_D_GUN_";
    private const string CellKeyPrefix = "MPL_D_CELL_";

    private const string Background = "MP_LOBBY_BACKGROUND.JPG";
    private const string SmallArt = "MP_B_SMALL.PNG";
    private const string MediumArt = "MP_B_MEDIUM.PNG";
    private const string LargeArt = "MP_B_LARGE.PNG";
    private const string ReadyArt = "MP_B_CHECKBOXLARGE.PNG";
    private const string RadioArt = "MP_B_RADIO8STATESSM.PNG";
    private const string CheckArt = "MP_B_CHECKBOX8STATES.PNG";
    private const string MarkArt = "MP_B_CHECKBOX.PNG";
    private const string ArrowArt = "MP_B_LISTBOXARROW.PNG";
    private const string UpArt = "MP_B_SCROLLUP.PNG";
    private const string DownArt = "MP_B_SCROLLDOWN.PNG";
    private const float MemberIndent = 12f;
    private const string TabLargeArt = "MP_LOBBY_TABLARGE.PNG";
    private const string TabSmallArt = "MP_LOBBY_TABSMALL.PNG";
    private const string IconArt = "MP_PLANEICONSTOPFRONT.PNG";

    // The tab page's corner and the two list geometries every tab shares.
    private const float PageX = 314f;
    private const float PageY = 26f;
    private const float ListX = 34f;
    private const float ListY = 83f;
    private const float ListPitch = 20f;
    private const float NameWidth = 230f;
    private const float ChatX = 34f;
    private const float ChatY = 373f;
    private const float ChatWidth = 735f;
    private const float ChatHeight = 165f;
    private const float ChatNameColumn = 100f;

    // The lines' column in the chat pane, ending clear of the scroll bar the background paints at
    // its right. That bar's border starts 6 px inside the pane's right edge.
    private const float ChatTextWidth = ChatWidth - ChatNameColumn - 10f;
    private const float CopyWidth = 48f;
    private const float DisabledArrow = 0.45f;
    private const int IconFrames = 11;

    private static readonly string[] TabKeys = { MissionTabKey, PlaneTabKey, AmmoTabKey, ScoresTabKey };
    private static readonly string[] TabPages =
        { "MP_LOBBY_MISSION.PNG", "MP_LOBBY_PLANE.PNG", "MP_LOBBY_AMMO.PNG", "MP_LOBBY_STATSCREEN.PNG" };

    private static readonly float[] TabX = { 324f, 442f, 548f, 656f };
    private static readonly float[] TabWidth = { 110f, 100f, 100f, 100f };
    private static readonly int[] TabIds = { 10094, 10114, 10119, 10507 };
    private static readonly string[] TabNames = { "Mission Options", "Select Plane", "Select Ammo", "Game Scores" };
    private static readonly string[] ShortNames =
    {
        "Hoplite", "Hellhound", "Balmoral", "Bloodhawk", "Brigand", "Devastator", "Firebrand", "Fury",
        "Kestrel", "Peacemaker", "Warhawk",
    };

    private static readonly string[] RatingLabels = { "TOP SPEED:", "ARMOR:", "AGILITY:", "OFFENSE:" };
    private static readonly string[] RatingWords = { "Poor", "Fair", "Average", "Good", "Excellent" };
    private static readonly int[] Calibres = { 70, 60, 50, 40, 30 };
    // The string table's three types; Stunt Race, the fourth, is the remake's and has no id.
    private static readonly string[] TypeNames = { "Capture the Flag", "Deathmatch", "Zeppelin vs Zeppelin" };
    private static readonly int[] TypeDescriptions = { 10124, 10123, 10125 };
    private static readonly int[] ScoreHeaderIds = { 10542, 10543, 10544, 10545, 10546 };
    private static readonly string[] ScoreHeaders = { "Team Name", "Points", "Kills", "Deaths", "Hits %" };
    private static readonly (float X, float Y)[] ScoreHeaderAt = { (21f, 43f), (179f, 44f), (242f, 44f), (303f, 44f), (362f, 44f) };

    // The scripts' colours beyond the plaque labels' four: the tab ink, the picked sub-tab's red,
    // the Ready? label, the LAUNCH! blink and the own name.
    private static readonly BoardTint TabDisabled = new(128, 128, 128);
    private static readonly BoardTint Black = new(0, 0, 0);
    private static readonly BoardTint Picked = new(142, 0, 0);
    private static readonly BoardTint ReadyLabel = new(255, 255, 170);
    private static readonly BoardTint LaunchBlink = new(255, 50, 25);
    private static readonly BoardTint OwnName = new(255, 0, 0);

    // The remake's pinned internet line, in the picked sub-tab's dark red so it reads apart from chat.
    private static readonly BoardTint Pinned = Picked;

    // The picked row's fill, the script's KEA.NF on the player list (MULTIPLAYERLOBBY_READY.SCRIPT).
    private static readonly (byte R, byte G, byte B) PickedRow = (209, 180, 120);

    private readonly Func<NetPlayFeature?> _net;
    private readonly IOriginalScreenHost _host;
    private readonly MultiplayerBoardText _text;
    private readonly Func<StockLoadouts?> _stock;
    private readonly Func<IReadOnlyList<int>> _pads;
    private readonly Func<string?> _pilotName;
    private readonly Func<IReadOnlyList<CustomPlaneDef>> _customs;
    private readonly OriginalOutlawList _outlaw;
    private readonly OriginalTeamBox _teamBox;
    private string? _open;

    // The team row picked for Join Team, 0 for none. A pick is a team or a player, never both.
    private byte _pickedTeam;

    // The name the last Create Team took, which the box opens on next, as the script's 2142 keeps it.
    private string _lastTeamName = string.Empty;
    private int _listTop;
    private string _chat = string.Empty;
    private string? _typing;
    private string _draft = string.Empty;
    private double _clock;
    private int _blink;
    private int _heard;

    // The peer whose row the host picked for Boot, -1 for none. Held by peer, so a row moving up
    // when another guest leaves never boots the wrong pilot.
    private int _picked = -1;

    /// <summary>A lobby module over the door <paramref name="net"/> answers. Its words come from the
    /// string table under <paramref name="dataRoot"/>, its fits from <paramref name="stock"/>. Seat
    /// 0's flight devices are <paramref name="pads"/>, and the pilot's name is
    /// <paramref name="pilotName"/>, null for none. The pilot's saved custom planes are
    /// <paramref name="customs"/>, none when null.</summary>
    public OriginalLobbyScreen(
        Func<NetPlayFeature?> net, IOriginalScreenHost host, string? dataRoot, Func<StockLoadouts?>? stock,
        Func<IReadOnlyList<int>>? pads = null, Func<string?>? pilotName = null,
        Func<IReadOnlyList<CustomPlaneDef>>? customs = null)
    {
        _customs = customs ?? (() => Array.Empty<CustomPlaneDef>());
        _net = net ?? throw new ArgumentNullException(nameof(net));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _text = new MultiplayerBoardText(_host, dataRoot);
        _outlaw = new OriginalOutlawList(_text);
        _teamBox = new OriginalTeamBox(_host, _text);
        _stock = stock ?? (() => null);
        _pads = pads ?? (() => Array.Empty<int>());
        _pilotName = pilotName ?? (() => null);
    }

    private enum TeamButtonAction
    {
        Create,
        Join,
        Leave,
    }

    /// <summary>The tab showing.</summary>
    public LobbyTab Tab { get; private set; } = LobbyTab.Mission;

    /// <summary>Whether the Select Ammo tab shows its Rockets page rather than its Guns page.</summary>
    public bool Rockets { get; private set; }

    /// <summary>Whether the Select Plane tab lists the pilot's custom planes rather than the stock
    /// ones.</summary>
    public bool CustomPlanes { get; private set; }

    /// <summary>The dropdown whose list stands open, or null.</summary>
    public string? OpenDropdown => _open;

    /// <summary>Whether the outlaw list stands over the tab page.</summary>
    public bool OutlawListOpen => _outlaw.IsOpen;

    /// <summary>The outlaw list's sub-tab showing.</summary>
    public OutlawPage OutlawPage => _outlaw.Page;

    /// <summary>The chat box's line as typed so far.</summary>
    public string ChatDraft => _chat;

    /// <summary>The rows pinned over the chat under <see cref="CoopDoorText.NoteName"/>
    /// (<see cref="CoopDoorText.HostLobbyLines"/>). They hold a host's join code or its wait for one,
    /// else its address and why there is no code. The copy hint follows the seat's device. Empty on
    /// a guest.</summary>
    public IReadOnlyList<string> NetworkRows =>
        _net() is { } net ? CoopDoorText.HostLobbyLines(net, _host.CopyWay) : Array.Empty<string>();

    /// <summary>The peer whose row the host picked for Boot, or -1 while none is picked or that
    /// guest has left.</summary>
    public int PickedPeer => Lobby is { IsHost: true } lobby && RowOf(lobby, _picked) > 0 ? _picked : -1;

    /// <summary>Whether the CREATE TEAM box stands over the lobby.</summary>
    public bool TeamBoxOpen => _teamBox.IsOpen;

    /// <summary>The team row picked for Join Team, 0 while none is picked or that team is gone.
    /// </summary>
    public byte PickedTeam => Lobby is { } lobby && HasTeam(lobby, _pickedTeam) ? _pickedTeam : (byte)0;

    /// <summary>Whether seat 0's typed characters feed one of the lobby's boxes. That holds while
    /// the lobby shows with a box focused and nothing over it, or the CREATE TEAM box's name.
    /// </summary>
    internal bool CapturingText =>
        _host.Screen == OriginalScreen.Lobby && !_host.DialogOpen && _open == null
        && (_teamBox.IsOpen ? _teamBox.CapturingText : IsBox(_host.FocusedKey));


    private DogfightLobby? Lobby => _net()?.Dogfight;

    // The lobby while the outlaw list stands over its tab page, else null.
    private DogfightLobby? Outlawing => _outlaw.IsOpen ? Lobby : null;

    /// <summary>A host's player list row's key by its place in the list, the host's own being 0.
    /// </summary>
    public static string PlayerKey(int row) => PlayerKeyPrefix + row.ToString(CultureInfo.InvariantCulture);

    /// <summary>A player list's team row's key by the team's number.</summary>
    public static string TeamRowKey(int team) => TeamRowKeyPrefix + team.ToString(CultureInfo.InvariantCulture);

    /// <summary>What a team row reads: the team's name and its member count, the players' own
    /// splitscreen seats not counted.</summary>
    public static string TeamRowText(string name, int members) =>
        $"{name} ({members.ToString(CultureInfo.InvariantCulture)})";

    /// <summary>The line a refused launch raises, the original's langui 10518 to 10520 or the
    /// remake's own for a teamless player or unbalanced teams. Empty for none.</summary>
    public static string RefusalFallback(TeamLaunchRefusal refusal) => refusal switch
    {
        TeamLaunchRefusal.TooManyTeams => "There are too many teams.",
        TeamLaunchRefusal.TooFewTeams => "Each player must be on one of two teams to play.",
        TeamLaunchRefusal.NotEnoughPlayers => "There are not enough players in the game.",
        TeamLaunchRefusal.Teamless => "Every player must be on a team to play.",
        TeamLaunchRefusal.Unbalanced => "The teams must not differ by more than one player.",
        _ => string.Empty,
    };

    /// <summary>A gun box's key by its zero-based slot.</summary>
    public static string GunKey(int slot) => GunKeyPrefix + slot.ToString(CultureInfo.InvariantCulture);

    /// <summary>A rocket box's key by its zero-based wing cell.</summary>
    public static string CellKey(int cell) => CellKeyPrefix + cell.ToString(CultureInfo.InvariantCulture);

    /// <summary>Whether a screen is this module's.</summary>
    public bool Owns(OriginalScreen screen) => screen == OriginalScreen.Lobby;

    /// <summary>The Connection page's Host and the games list's Create Game: a listen server with
    /// the lobby standing on it. The host goes by the callsign Player Information set, else by the
    /// pilot's own name. A socket that will not open says why over the page it was asked from.</summary>
    public void OpenHost()
    {
        if (_net() is not { } net)
        {
            return;
        }

        net.StopSearch();
        if (net.Stage is NetDoorStage.Failed)
        {
            net.Close();
        }

        TakePilotName(net);
        net.OpenDogfightHost(NetSeats.MaxPlayers - 1);
        if (net.Dogfight == null)
        {
            _host.RaiseDialog(net.Fault.Length > 0 ? net.Fault : "The game could not be hosted.", DialogIcon.Warning,
                new OriginalDialogAnswer(OriginalShell.DialogOkKey, CampaignBoards.DialogCenterKey, _text.Word(100, "OK"), null));
            return;
        }

        Enter();
    }

    /// <summary>A joined Dogfight guest's way in, once its host's advert names a Dogfight.</summary>
    public void OpenGuest()
    {
        if (_net() is { } net)
        {
            TakePilotName(net);
            net.Dogfight?.Show();
        }

        Enter();
    }

    /// <summary>Back from a match onto its lobby's Game Scores page, every Ready cleared. False when
    /// the door no longer holds a lobby, and the caller then shows the Connection page.</summary>
    public bool Land(IReadOnlyList<DogfightScore> scores)
    {
        ArgumentNullException.ThrowIfNull(scores);
        if (_net() is not { CanLaunch: true } || Lobby is not { Shown: true } lobby)
        {
            return false;
        }

        lobby.Land(scores);
        Enter();
        Tab = LobbyTab.Scores;
        return true;
    }

    /// <summary>Shows a tab directly, the screenshot aids' door. It opens Game Scores too, which
    /// the pointer cannot.</summary>
    public void ShowTab(LobbyTab tab, bool rockets = false)
    {
        _open = null;
        _outlaw.Close();
        Tab = tab;
        Rockets = rockets;
    }

    /// <summary>Opens the outlaw list on a sub-tab directly over Mission Options, the screenshot
    /// aids' door. Nothing opens without a lobby.</summary>
    public void ShowOutlawList(OutlawPage page)
    {
        ShowTab(LobbyTab.Mission);
        if (Lobby is { } lobby)
        {
            _outlaw.Open(lobby);
            _outlaw.Show(page);
        }
    }

    /// <summary>Stands the CREATE TEAM box over the lobby, as the team button's Create Team does.
    /// Nothing opens without a lobby or while this pilot may not create a team.</summary>
    public void ShowTeamBox()
    {
        if (Lobby is { } lobby && TeamButton(lobby) == TeamButtonAction.Create && !lobby.Ready)
        {
            _open = null;
            _teamBox.Open(_lastTeamName);
        }
    }

    /// <summary>The showing tab's rows, or the open list's items alone while one stands open, or
    /// the CREATE TEAM box's alone while it stands.</summary>
    public void BuildRows(List<OriginalRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (_teamBox.IsOpen)
        {
            _teamBox.Rows(rows);
            return;
        }

        if (OpenList() is { } drop)
        {
            _listTop = OriginalDropLists.Top(drop, _listTop, _host.FocusedRow);
            OriginalDropLists.AddRows(drop, _listTop, rows,
                (art, width, height) => OriginalWidgets.StripSize(art, _host.Measure, width, height));
            return;
        }

        Widgets(rows);
    }

    /// <summary>Every dropdown shows all its items, so only the outlaw list's long pages scroll.</summary>
    public void Lists(List<OriginalList> lists)
    {
        ArgumentNullException.ThrowIfNull(lists);
        if (_open == null)
        {
            _outlaw.Lists(lists);
        }
    }

    /// <summary>A sideways step on a live dropdown picks its next allowed value.</summary>
    public bool StepSideways(IReadOnlyList<OriginalRow> rows, int focus, int direction)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (_open != null || focus < 0 || focus >= rows.Count || direction == 0)
        {
            return false;
        }

        var row = rows[focus];
        if (row.Kind != OriginalRowKind.Dropdown || !row.Enabled || DropdownFor(row.Key) is not { } list || list.Items.Count == 0)
        {
            return false;
        }

        int step = Math.Sign(direction);
        int at = list.Current;
        for (int i = 0; i < list.Items.Count; i++)
        {
            at = ((at + step) % list.Items.Count + list.Items.Count) % list.Items.Count;
            if (list.Allowed(at))
            {
                list.Select(at);
                return true;
            }
        }

        return true;
    }

    /// <summary>Closes the open list and puts the focus back on its box.</summary>
    public bool CloseDropdown()
    {
        if (_open is not { } key)
        {
            return false;
        }

        _open = null;
        _host.FocusKey(key);
        return true;
    }

    /// <summary>The lobby's answer to an activated row.</summary>
    public MenuExit? Activate(OriginalRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _typing = null;
        var lobby = Lobby;
        if (OriginalTeamBox.Owns(row.Key))
        {
            if (_teamBox.Activate(row.Key) is { } name)
            {
                _lastTeamName = name;
                lobby?.CreateTeam(name);
            }

            if (!_teamBox.IsOpen)
            {
                _host.FocusKey(TeamKey);
            }

            return null;
        }

        if (row.Key.IndexOf(':', StringComparison.Ordinal) is var colon and > 0)
        {
            PickFromList(row.Key[..colon], row.Key[(colon + 1)..]);
            return null;
        }

        if (OriginalWidgets.Indexed(row.Key, PlayerKeyPrefix) is { } listed)
        {
            // A second press on the picked row lets it go, as the script's row mailbox does.
            int peer = lobby?.PeerAt(listed) ?? -1;
            _picked = peer == _picked ? -1 : peer;
            _pickedTeam = 0;
            return null;
        }

        if (OriginalWidgets.Indexed(row.Key, TeamRowKeyPrefix) is { } team)
        {
            _pickedTeam = team == _pickedTeam ? (byte)0 : (byte)team;
            _picked = -1;
            return null;
        }

        if (row.Key.StartsWith(TeamArrowPrefix, StringComparison.Ordinal) && lobby != null)
        {
            string arrow = row.Key[TeamArrowPrefix.Length..];
            int by = arrow.EndsWith('+') ? 1 : -1;
            _ = arrow.StartsWith("MIN", StringComparison.Ordinal)
                ? lobby.SetMinTeams(lobby.Options.MinTeams + by)
                : lobby.SetMaxTeams(lobby.Options.MaxTeams + by);
            return null;
        }

        if (OriginalOutlawList.Owns(row.Key))
        {
            _outlaw.Activate(lobby, row.Key);
            if (!_outlaw.IsOpen)
            {
                _host.FocusKey(SelectKey);
            }

            return null;
        }

        switch (row.Key)
        {
            case SelectKey:
                if (lobby is { Rules.Outlawing: true })
                {
                    _outlaw.Open(lobby);
                    _host.FocusKey(OriginalOutlawList.TabKey(OutlawPage.Airframes));
                }

                return null;
            case MissionTabKey:
            case PlaneTabKey:
            case AmmoTabKey:
            case ScoresTabKey:
                Tab = (LobbyTab)Array.IndexOf(TabKeys, row.Key);
                return null;
            case GunsTabKey:
                Rockets = false;
                return null;
            case RocketsTabKey:
                Rockets = true;
                return null;
            case DefaultPlanesKey:
                CustomPlanes = false;
                return null;
            case CustomTabKey:
                CustomPlanes = true;
                return null;
            case CustomPlanesKey:
                lobby?.SetAllowCustomPlanes(!lobby.Rules.AllowCustom);
                return null;
            case OutlawKey:
                lobby?.SetOutlawComponents(!lobby.Rules.Outlawing);
                return null;
            case TimeRadioKey:
                lobby?.SetVictory(DogfightLobby.Toggled(lobby.Options.Victory, DogfightVictory.Time));
                return null;
            case ScoreRadioKey:
                lobby?.SetVictory(DogfightLobby.Toggled(lobby.Options.Victory, DogfightVictory.Score));
                return null;
            case TeamsKey:
                lobby?.SetRestrictTeams(!lobby.Options.RestrictTeams);
                return null;
            case FlagHomeKey:
                lobby?.SetFlagHomeToCapture(!lobby.Options.FlagHomeToCapture);
                return null;
            case TeamKey:
                PressTeam(lobby);
                return null;
            case LimitedLivesKey:
                lobby?.SetLimitedLives(!lobby.Options.LimitedLives);
                return null;
            case AutoRespawnKey:
                lobby?.SetAutoRespawn(!lobby.Options.AutoRespawn);
                return null;
            case ReadyKey:
                if (lobby != null && !lobby.SetReady(!lobby.Ready))
                {
                    Refused(lobby.ReadyRefusals);
                }

                return null;
            case ChatKey:
            case SendKey:
                if (lobby != null && lobby.Say(_chat))
                {
                    _chat = string.Empty;
                }

                return null;
            case LaunchKey:
                if (lobby is { CanLaunch: true } && lobby.LaunchRefusal is var refusal and not TeamLaunchRefusal.None)
                {
                    RefuseLaunch(refusal);
                    return null;
                }

                return _net() is { } net && lobby is { CanLaunch: true } ? Exit(net, lobby) : null;
            case LeaveKey:
                Leave();
                return null;
            case CopyKey:
                _net()?.CopyForGuests();
                return null;
            case BootKey:
                // The script re-presses the picked row after the boot, which lets the pick go.
                if (PickedPeer >= 0)
                {
                    _net()?.Boot(_picked);
                }

                _picked = -1;
                return null;
        }

        if (row.Kind == OriginalRowKind.Dropdown && DropdownFor(row.Key) is { } list)
        {
            _open = row.Key;
            _listTop = 0;
            _host.FocusedRow = Math.Max(0, list.Current);
        }

        return null;
    }

    /// <summary>Back closes an open list, then the outlaw list as its Cancel does, and otherwise
    /// leaves the game as Leave Game does.</summary>
    public bool Back()
    {
        if (_teamBox.IsOpen)
        {
            _teamBox.Close();
            _host.FocusKey(TeamKey);
            return true;
        }

        if (CloseDropdown())
        {
            return true;
        }

        if (_outlaw.IsOpen)
        {
            _outlaw.Activate(Lobby, OriginalOutlawList.CancelKey);
            _host.FocusKey(SelectKey);
            return true;
        }

        Leave();
        return true;
    }

    /// <summary>The lobby as drawn: the background, the tab page, the players, the chat and the
    /// plaques. An open list stands over all of it.</summary>
    public void Compose(IReadOnlyList<OriginalRow> rows, int focus, BoardLayers layers)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(layers);

        // The widgets are rebuilt closed, so the page under an open list is drawn as it stands.
        var widgets = new List<OriginalRow>();
        Widgets(widgets);
        bool boxed = _teamBox.IsOpen;
        string focused = boxed ? string.Empty : _open ?? (focus >= 0 && focus < rows.Count && !_host.DialogOpen ? rows[focus].Key : string.Empty);
        int pressedAt = _host.DialogOpen || _open != null || boxed ? -1 : _host.PressedRow;
        string pressed = pressedAt >= 0 && pressedAt < rows.Count ? rows[pressedAt].Key : string.Empty;

        layers.Backdrop.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, Background), 0f, 0f));
        // The page is backdrop so the boxes' fills land over it and under their words.
        layers.Backdrop.Add(Outlawing is { }
            ? OriginalOutlawList.Pane
            : new BoardPicture(new BoardArt(BoardArtLibrary.Ui, TabPages[(int)Tab]), PageX, PageY));
        ComposePlayers(layers);
        ComposeChat(layers);
        foreach (var row in widgets)
        {
            if (Outlawing is { } lobby && OriginalOutlawList.Owns(row.Key))
            {
                _outlaw.ComposeWidget(lobby, row, row.Key == focused, row.Key == pressed, layers);
                continue;
            }

            ComposeWidget(row, row.Key == focused, row.Key == pressed, layers);
        }

        if (Outlawing != null)
        {
            _outlaw.ComposePage(layers);
        }
        else
        {
            ComposePage(layers);
        }
        if (_open != null)
        {
            ComposeOpenList(rows, focus, layers);
        }

        if (boxed)
        {
            _teamBox.Compose(rows, _host.DialogOpen ? -1 : focus, layers);
        }

        if (_host.SeatPanel(false) is { } strip)
        {
            layers.Overlays.Add(strip);
        }
    }

    /// <summary>One menu frame's upkeep while the lobby shows, after the door was stepped. It moves
    /// the LAUNCH! blink, and it notices a changed list, option or chat line. Returns whether the
    /// picture changed.</summary>
    public bool Tick(double dt)
    {
        if (_host.Screen != OriginalScreen.Lobby)
        {
            return false;
        }

        if (_typing != null && _host.FocusedKey != _typing)
        {
            _typing = null;
        }

        _clock += dt;
        int blink = (int)(_clock * 4.0) % 2;
        bool changed = blink != _blink && Lobby is { CanLaunch: true } && Tab == LobbyTab.Mission;
        _blink = blink;
        int heard = Hash(Lobby);
        changed |= heard != _heard;
        _heard = heard;
        return changed;
    }

    /// <summary>A Dogfight guest's launch once its host has launched, taken after the door was
    /// stepped, or null.</summary>
    public MenuExit? GuestLaunch() =>
        _net() is { DogfightLaunchDue: true } net && net.Dogfight is { } lobby ? Exit(net, lobby) : null;

    /// <summary>Typed characters and Backspace into the focused box. The chat box takes printable
    /// characters up to its width. The number boxes take digits, and a value inside the box's range
    /// is set as it is typed.</summary>
    internal bool TypeText(MenuCommands commands, List<string>? cues = null)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (_teamBox.IsOpen)
        {
            return CapturingText && _teamBox.TypeText(commands, cues ?? new List<string>());
        }

        if (!CapturingText || Lobby is not { } lobby || (commands.Typed.Length == 0 && !commands.Erase))
        {
            return false;
        }

        string key = _host.FocusedKey;
        if (key == ChatKey)
        {
            string before = _chat;
            foreach (char c in commands.Typed)
            {
                if (c is >= ' ' and < (char)127 && _chat.Length < LobbyChatMessage.MaxChars)
                {
                    _chat += c;
                }
            }

            if (commands.Erase && _chat.Length > 0)
            {
                _chat = _chat[..^1];
            }

            return _chat != before;
        }

        string draft = _typing == key ? _draft : BoxValue(key, lobby);
        foreach (char c in commands.Typed)
        {
            if (char.IsAsciiDigit(c) && draft.Length < BoxWidth(key))
            {
                draft += c;
            }
        }

        if (commands.Erase && draft.Length > 0)
        {
            draft = draft[..^1];
        }

        _typing = key;
        _draft = draft;
        if (int.TryParse(draft, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
        {
            _ = key switch
            {
                TimeKey => lobby.SetTimeMinutes(value),
                ScoreKey => lobby.SetScore(value),
                MinTeamsKey => lobby.SetMinTeams(value),
                MaxTeamsKey => lobby.SetMaxTeams(value),
                _ => lobby.SetLives(value),
            };
        }

        return true;
    }

    private static bool IsBox(string key) => key is ChatKey or TimeKey or ScoreKey or LivesKey or MinTeamsKey or MaxTeamsKey;

    private static bool HasTeam(DogfightLobby lobby, byte team)
    {
        foreach (var named in lobby.Teams)
        {
            if (named.Number == team && team != 0)
            {
                return true;
            }
        }

        return false;
    }

    // A team count box's arrow, the script's BG and CG beside the box: 16 by 11, four frames.
    private static OriginalRow Arrow(string key, string art, float x, float y, bool enabled) =>
        new(key, string.Empty, OriginalRowKind.Button, x, y, 16f, 11f, enabled, 1, new BoardArt(BoardArtLibrary.Ui, art, 4));

    // The player list as drawn: each team's row followed by its members, then every player on no
    // team. The ready script's rows of kind 1 and kind 0 stand so. Player is the index into Players,
    // -1 for a team row.
    private static List<ListEntry> ListEntries(DogfightLobby lobby)
    {
        var players = lobby.Players;
        var entries = new List<ListEntry>(players.Count + lobby.Teams.Count);
        var listed = new bool[players.Count];
        foreach (var team in lobby.Teams)
        {
            int members = 0;
            foreach (var player in players)
            {
                members += player.Team == team.Number ? 1 : 0;
            }

            entries.Add(new ListEntry(team.Number, -1, TeamRowText(team.Name, members)));
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Team == team.Number)
                {
                    listed[i] = true;
                    entries.Add(new ListEntry(team.Number, i, players[i].Name));
                }
            }
        }

        for (int i = 0; i < players.Count; i++)
        {
            if (!listed[i])
            {
                entries.Add(new ListEntry(0, i, players[i].Name));
            }
        }

        return entries;
    }

    // The row the host's list names a peer on, or -1 while it names it on none.
    private static int RowOf(DogfightLobby lobby, int peer)
    {
        if (peer < 0)
        {
            return -1;
        }

        for (int row = 1; row < lobby.Players.Count; row++)
        {
            if (lobby.PeerAt(row) == peer)
            {
                return row;
            }
        }

        return -1;
    }

    private static int BoxWidth(string key) => key == ScoreKey ? 3 : 2;

    private static string BoxValue(string key, DogfightLobby lobby) => key switch
    {
        TimeKey => lobby.Options.TimeMinutes.ToString(CultureInfo.InvariantCulture),
        ScoreKey => lobby.Options.Score.ToString(CultureInfo.InvariantCulture),
        MinTeamsKey => lobby.Options.MinTeams.ToString(CultureInfo.InvariantCulture),
        MaxTeamsKey => lobby.Options.MaxTeams.ToString(CultureInfo.InvariantCulture),
        _ => lobby.Options.Lives.ToString(CultureInfo.InvariantCulture),
    };

    private static int Hash(DogfightLobby? lobby)
    {
        if (lobby == null)
        {
            return 0;
        }

        var hash = default(HashCode);
        hash.Add(lobby.Options);
        hash.Add(lobby.Rules);
        hash.Add(lobby.HasOptions);
        hash.Add(lobby.Ready);
        hash.Add(lobby.You);
        hash.Add(lobby.Chat.Count);
        hash.Add(lobby.Scores.Count);
        foreach (var player in lobby.Players)
        {
            hash.Add(player);
        }

        foreach (var team in lobby.Teams)
        {
            hash.Add(team);
        }

        return hash.ToHashCode() | 1;
    }

    private static int StateFrame(OriginalRow row, bool focused, bool pressed) =>
        !row.Enabled ? 0 : pressed ? 3 : focused ? 2 : 1;

    private static GunSpec? GunFor(LoadoutDef? def, int slot)
    {
        if (def == null)
        {
            return null;
        }

        foreach (var gun in def.Guns)
        {
            if (gun.Slot == slot && !gun.Turret)
            {
                return gun;
            }
        }

        return null;
    }

    private static CoopFit Refit(CoopFit fit, int slot, int ammo, int cell, int ordnance)
    {
        var ammoPicks = new int[CoopFit.GunSlots];
        for (int i = 0; i < ammoPicks.Length; i++)
        {
            ammoPicks[i] = i == slot ? ammo : fit.AmmoAt(i);
        }

        var cells = new int[CoopFit.Cells];
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i] = i == cell ? ordnance : fit.OrdnanceAt(i);
        }

        return CoopFit.Of(ammoPicks, cells);
    }

    private static string Item(DropdownList list) =>
        list.Current >= 0 && list.Current < list.Items.Count ? list.Items[list.Current] : string.Empty;

    // What the team button does for this pilot, the ready script's 1200 to 1202. It is Leave Team on
    // a team, Join Team with a team row picked, else Create Team.
    private TeamButtonAction TeamButton(DogfightLobby lobby) =>
        lobby.OwnTeam != 0 ? TeamButtonAction.Leave : PickedTeam != 0 ? TeamButtonAction.Join : TeamButtonAction.Create;

    private void PressTeam(DogfightLobby? lobby)
    {
        if (lobby == null || lobby.Ready)
        {
            return;
        }

        switch (TeamButton(lobby))
        {
            case TeamButtonAction.Leave:
                lobby.LeaveTeam();
                break;
            case TeamButtonAction.Join:
                lobby.JoinTeam(PickedTeam);
                _pickedTeam = 0;
                break;
            default:
                ShowTeamBox();
                break;
        }
    }

    // The team refusals raise the original's OK messagebox, as the mission script's LAUNCH! does.
    private void RefuseLaunch(TeamLaunchRefusal refusal)
    {
        int id = refusal switch
        {
            TeamLaunchRefusal.TooManyTeams => 10518,
            TeamLaunchRefusal.TooFewTeams => 10519,
            TeamLaunchRefusal.NotEnoughPlayers => 10520,
            _ => 0,
        };

        string text = id != 0 ? _text.Word(id, RefusalFallback(refusal)) : RefusalFallback(refusal);
        _host.RaiseDialog(text, DialogIcon.Warning,
            new OriginalDialogAnswer(OriginalShell.DialogOkKey, CampaignBoards.DialogCenterKey, _text.Word(100, "OK"), null));
    }

    // The callsign Player Information set names the pilot, and a door that asked none goes by the
    // pilot's own name.
    private void TakePilotName(NetPlayFeature net)
    {
        if (net.PlayerName.Length == 0)
        {
            net.PlayerName = _pilotName() ?? string.Empty;
        }
    }

    private void Enter()
    {
        _picked = -1;
        _pickedTeam = 0;
        _teamBox.Drop();
        _open = null;
        _outlaw.Close();
        _typing = null;
        _chat = string.Empty;
        Tab = LobbyTab.Mission;
        Rockets = false;
        CustomPlanes = false;
        _host.CloseDialog();
        _host.Open(OriginalScreen.Lobby);
    }

    // The original's own refusal at Ready: langui 10517, then 10514, 10515 and 10516 for each
    // reason, on its OK dialog.
    private void Refused(IReadOnlyList<PlaneRefusal> why)
    {
        string text = _text.Word(10517, "You cannot select Ready yet.");
        foreach (var refusal in why)
        {
            text += " " + refusal switch
            {
                PlaneRefusal.Ammo => _text.Word(10515, "You have not selected the necessary ammunition for your guns."),
                PlaneRefusal.Rockets => _text.Word(10516, "You have not selected the necessary rockets for your plane's hardpoints."),
                _ => _text.Word(10514, "You have not selected a valid plane."),
            };
        }

        _host.RaiseDialog(text, DialogIcon.Warning,
            new OriginalDialogAnswer(OriginalShell.DialogOkKey, CampaignBoards.DialogCenterKey, _text.Word(100, "OK"), null));
    }

    private void Leave()
    {
        _picked = -1;
        _pickedTeam = 0;
        _teamBox.Drop();
        _open = null;
        _outlaw.Close();
        _typing = null;
        _chat = string.Empty;
        _net()?.Close();
        _host.CloseDialog();
        _host.Open(OriginalScreen.Connection);
    }

    // Seat 0 on the plane and fit this pilot picked, on the chapter and the rules the host's options
    // name. The wire rides out with it. ⚠ A custom plane flies as its wire build reads back, the
    // copy every other machine builds, never the stored def.
    private LaunchExit Exit(NetPlayFeature net, DogfightLobby lobby)
    {
        var options = lobby.Options;
        var seat = new MenuSeatChoice(
            StockAirframes.Node(lobby.Airframe), _pads(), CampaignLoadout.For(lobby.LaunchFit, _stock()),
            CSVM.Flight.Hangar.CustomPlaneWire.Def(lobby.Build));
        return new LaunchExit(
            DogfightLobby.ChapterOf(options.Environment), new[] { seat }, DogfightLobby.LaunchMode(options),
            Match: DogfightLobby.RulesOf(options), Net: net.BuildLaunch());
    }

    private OriginalRow Check(string key, string art, float x, float y, float hitWidth, float size, bool enabled) =>
        new(key, string.Empty, OriginalRowKind.Radio, x, y, hitWidth, size, enabled, 1, new BoardArt(BoardArtLibrary.Ui, art, 8));

    private OriginalRow Drop(string key, string label, float x, float y, float width, float height, bool enabled) =>
        new(key, label, OriginalRowKind.Dropdown, x, y, width, height, enabled, 1, null);

    private OriginalRow Box(string key, string label, float x, float y, float width, float height, bool enabled, int column = 1) =>
        new(key, label, OriginalRowKind.TextField, x, y, width, height, enabled, column, null);


    // The COPY control over the first pinned Network row, where the code or the address with the
    // copy mark stands. Null while the host shows nothing to copy, while the master server is still
    // answering (the row names neither yet), and on a guest.
    private OriginalRow? CopyRow()
    {
        if (_net() is not { } net || net.AwaitingCode || CoopDoorText.CopyTarget(net).Length == 0)
        {
            return null;
        }

        float size = _text.Regular(10575)?.Pixels ?? MultiplayerBoardText.TextFallback;
        float height = size + 2f;
        return new OriginalRow(CopyKey, CoopDoorText.CopyButton, OriginalRowKind.TextButton, ChatX + ChatNameColumn,
            BoardLine.CapsBoxTop(ChatY, size, height), ChatTextWidth, height, true, 0, null);
    }

    // Every widget of the showing tab and its frame, in focus order. The tabs and the page come
    // first, then the player list's plaques, the chat line and Leave Game. The outlaw list stands
    // in the page's place with the tabs greyed, as the Select... press's mail(1) to each does.
    private void Widgets(List<OriginalRow> rows)
    {
        var lobby = Lobby;
        for (int i = 0; i < TabKeys.Length; i++)
        {
            rows.Add(new OriginalRow(TabKeys[i], string.Empty, OriginalRowKind.TextButton, TabX[i], 24f, TabWidth[i], 25f,
                lobby != null && !_outlaw.IsOpen && (i != (int)LobbyTab.Scores || lobby.Scores.Count > 0), 1, null));
        }

        if (lobby != null && _outlaw.IsOpen)
        {
            _outlaw.Widgets(lobby, rows);
        }
        else if (lobby != null)
        {
            switch (Tab)
            {
                case LobbyTab.Mission:
                    MissionRows(lobby, rows);
                    break;
                case LobbyTab.Plane:
                    PlaneRows(lobby, rows);
                    break;
                case LobbyTab.Ammo:
                    AmmoRows(lobby, rows);
                    break;
            }
        }

        // A team row is picked by anyone, for Join Team. A player row is picked only on the host,
        // for Boot; the host's own row is never booted (FUN_00413090 refuses it).
        if (lobby != null && !_outlaw.IsOpen)
        {
            var entries = ListEntries(lobby);
            for (int slot = 0; slot < entries.Count && slot < VisiblePlayers; slot++)
            {
                var entry = entries[slot];
                string key = entry.Player < 0 ? TeamRowKey(entry.Team) : PlayerKey(entry.Player);
                if (entry.Player < 0 || (lobby.IsHost && entry.Player >= 1))
                {
                    rows.Add(new OriginalRow(key, entry.Text, OriginalRowKind.ListRow, ListX, ListY + (slot * ListPitch),
                        NameWidth + ListPitch, ListPitch, true, 0, null));
                }
            }
        }

        rows.Add(_text.Strip(BootKey, SmallArt, 19f, 325f, PickedPeer >= 0, 0, 74f, 37f));
        rows.Add(_text.Strip(TeamKey, LargeArt, 105f, 325f, lobby is { Ready: false } && !_outlaw.IsOpen, 0, 131f, 37f));
        rows.Add(new OriginalRow(ReadyKey, string.Empty, OriginalRowKind.Radio, 250f, 325f, 58f, 37f,
            lobby is { HasOptions: true }, 0, new BoardArt(BoardArtLibrary.Ui, ReadyArt, 8)));
        if (CopyRow() is { } copy)
        {
            rows.Add(copy);
        }

        rows.Add(Box(ChatKey, _chat, 88f, 552f, 481f, 18f, lobby != null, 0));
        rows.Add(_text.Strip(SendKey, SmallArt, 577f, 548f, lobby != null, 1, 74f, 37f));
        rows.Add(_text.Strip(LeaveKey, LargeArt, 655f, 548f, true, 1, 131f, 37f));
    }

    private void MissionRows(DogfightLobby lobby, List<OriginalRow> rows)
    {
        var options = lobby.Options;
        bool live = lobby.IsHost && !lobby.Ready;
        rows.Add(Drop(EnvironmentKey, EnvironmentWord(options.Environment), PageX + 25f, PageY + 63f, 175f, 22f, live));
        rows.Add(Drop(TypeKey, TypeWord(options.MissionType), PageX + 25f, PageY + 108f, 175f, 22f, live));
        rows.Add(_text.Strip(LaunchKey, LargeArt, PageX + 47f, PageY + 284f, lobby.CanLaunch, 1, 131f, 37f));
        // A Stunt Race keeps the Time box alone, the race window; every other option greys.
        bool race = DogfightLobby.IsStuntRace(options);
        bool rules = live && !race;
        rows.Add(Check(TimeRadioKey, RadioArt, PageX + 241f, PageY + 70f, 120f, 12f, rules));
        rows.Add(Box(TimeKey, BoxText(TimeKey, lobby), PageX + 370f, PageY + 68f, 73f, 18f, live && DogfightLobby.Arms(options.Victory, DogfightVictory.Time)));
        rows.Add(Check(ScoreRadioKey, RadioArt, PageX + 241f, PageY + 92f, 120f, 12f, rules));
        rows.Add(Box(ScoreKey, BoxText(ScoreKey, lobby), PageX + 370f, PageY + 92f, 73f, 18f, rules && DogfightLobby.Arms(options.Victory, DogfightVictory.Score)));
        // Capture the Flag and Zeppelin vs Zeppelin fix the team count at two, as their type change's
        // mail(5) and mail(1109) do. The remake's own-flag-home option stands under the boxes.
        bool ctf = DogfightLobby.IsCtf(options);
        bool fixedTeams = DogfightLobby.FixesTeams(options);
        rows.Add(Check(TeamsKey, CheckArt, PageX + 241f, PageY + 133f, 180f, 11f, rules && !fixedTeams));
        bool counts = rules && options.RestrictTeams && !fixedTeams;
        if (ctf)
        {
            rows.Add(Check(FlagHomeKey, CheckArt, PageX + 241f, PageY + 177f, 180f, 11f, live));
        }

        rows.Add(Box(MinTeamsKey, BoxText(MinTeamsKey, lobby), PageX + 300f, PageY + 150f, 42f, 22f, counts));
        rows.Add(Arrow(TeamArrowPrefix + "MIN+", UpArt, PageX + 342f, PageY + 150f, counts && options.MinTeams < options.MaxTeams));
        rows.Add(Arrow(TeamArrowPrefix + "MIN-", DownArt, PageX + 342f, PageY + 161f, counts && options.MinTeams > 0));
        rows.Add(Box(MaxTeamsKey, BoxText(MaxTeamsKey, lobby), PageX + 387f, PageY + 150f, 42f, 22f, counts));
        rows.Add(Arrow(TeamArrowPrefix + "MAX+", UpArt, PageX + 429f, PageY + 150f, counts && options.MaxTeams < DogfightLobby.MaxTeams));
        rows.Add(Arrow(TeamArrowPrefix + "MAX-", DownArt, PageX + 429f, PageY + 161f, counts && options.MaxTeams > options.MinTeams));
        rows.Add(Check(LimitedLivesKey, CheckArt, PageX + 241f, PageY + 192f, 110f, 11f, rules));
        rows.Add(Box(LivesKey, BoxText(LivesKey, lobby), PageX + 360f, PageY + 193f, 25f, 22f, rules && options.LimitedLives));
        rows.Add(Check(AutoRespawnKey, CheckArt, PageX + 241f, PageY + 207f, 110f, 11f, rules));
        rows.Add(Check(CustomPlanesKey, CheckArt, PageX + 241f, PageY + 247f, 150f, 11f, rules));
        rows.Add(Check(OutlawKey, CheckArt, PageX + 265f, PageY + 262f, 150f, 11f, rules));
        // Live on either end while the tick stands and greyed while it is clear, a Ready host's
        // included. The mission script's refresh 1015 mails 2 or 1 to it on that alone.
        rows.Add(_text.Strip(SelectKey, MediumArt, PageX + 295f, PageY + 277f, lobby.Rules.Outlawing && !race, 1, 96f, 37f));
    }

    private void PlaneRows(DogfightLobby lobby, List<OriginalRow> rows)
    {
        bool customs = lobby.Rules.AllowCustom;
        CustomPlanes &= customs;
        rows.Add(_text.Strip(DefaultPlanesKey, TabLargeArt, PageX + 194f, PageY + 44f, true, 1, 119f, 23f));
        rows.Add(_text.Strip(CustomTabKey, TabLargeArt, PageX + 316f, PageY + 44f, customs, 1, 119f, 23f));
        string label = lobby.Build is { } build ? build.Name : PlaneWord(lobby.Airframe);
        rows.Add(Drop(PlaneKey, label, PageX + 11f, PageY + 80f, 195f, 36f, !CustomPlanes || SavedPlanes().Count > 0));
    }

    // The pilot's custom planes the pickers offer: every saved one the campaign has exported.
    private List<CustomPlaneDef> SavedPlanes()
    {
        var planes = new List<CustomPlaneDef>();
        foreach (var def in _customs())
        {
            if (!def.AwaitingExport)
            {
                planes.Add(def);
            }
        }

        return planes;
    }

    private void AmmoRows(DogfightLobby lobby, List<OriginalRow> rows)
    {
        rows.Add(_text.Strip(GunsTabKey, TabSmallArt, PageX + 287f, PageY + 46f, true, 1, 69f, 23f));
        rows.Add(_text.Strip(RocketsTabKey, TabSmallArt, PageX + 363f, PageY + 46f, true, 1, 69f, 23f));
        var def = StockDef(lobby.Airframe);
        if (!Rockets)
        {
            for (int slot = 0; slot < CoopFit.GunSlots; slot++)
            {
                bool gun = GunFor(def, slot + 1) != null;
                string label = gun && DropdownFor(GunKey(slot)) is { } list ? Item(list) : _text.Word(10144, "<none>");
                rows.Add(Drop(GunKey(slot), label, PageX + 11f, PageY + 100f + (50f * slot), 195f, 22f, gun));
            }

            return;
        }

        for (int cell = 0; cell < CoopFit.Cells; cell++)
        {
            bool hung = Loadout.PylonForCell(cell, def?.Hardpoints) != 0;
            string label = hung && DropdownFor(CellKey(cell)) is { } list ? Item(list) : _text.Word(10144, "<none>");
            rows.Add(Drop(CellKey(cell), label, PageX + 25f, PageY + 80f + (30f * cell), 195f, 22f, hung));
        }
    }

    private string BoxText(string key, DogfightLobby lobby) => _typing == key ? _draft : BoxValue(key, lobby);

    private string EnvironmentWord(int environment) => _text.Word(10558 + environment, DogfightLobby.EnvironmentName(environment));

    private string TypeWord(int type) => type switch
    {
        >= 0 and < 3 => _text.Word(10555 + type, TypeNames[type]),
        (int)DogfightMissionType.StuntRace => DogfightLobby.StuntRaceName,
        _ => string.Empty,
    };

    private string PlaneWord(int airframe) =>
        _text.Word(10565, "Stock") + " " + ShortNames[Math.Clamp(airframe, 0, ShortNames.Length - 1)];

    // The fit the Select Ammo tab edits over: a custom plane's own guns and pylons, or the stock ones.
    private LoadoutDef? StockDef(int airframe)
    {
        var stock = _stock()?.ForModel(StockAirframes.Node(airframe));
        return stock != null && CSVM.Flight.Hangar.CustomPlaneWire.Def(Lobby?.Build) is { } custom
            ? CustomPlaneBuild.LoadoutFor(custom, stock)
            : stock;
    }

    // The list behind a dropdown: its words, the value it stands on, which values can be picked and
    // how a pick is written. Null for a key that is not a live dropdown.
    private DropdownList? DropdownFor(string key)
    {
        if (Lobby is not { } lobby)
        {
            return null;
        }

        switch (key)
        {
            case EnvironmentKey:
                {
                    var items = new string[DogfightLobby.EnvironmentCount];
                    for (int i = 0; i < items.Length; i++)
                    {
                        items[i] = EnvironmentWord(i);
                    }

                    return new DropdownList(items, lobby.Options.Environment,
                        i => DogfightLobby.Offers(DogfightLobby.TypeOf(lobby.Options), i), i => lobby.SetEnvironment(i));
                }

            case TypeKey:
                {
                    var types = new string[DogfightLobby.TypeCount];
                    for (int i = 0; i < types.Length; i++)
                    {
                        types[i] = TypeWord(i);
                    }

                    return new DropdownList(types, lobby.Options.MissionType,
                        i => DogfightLobby.Flies((DogfightMissionType)i), i => lobby.SetMissionType((DogfightMissionType)i));
                }

            case PlaneKey when CustomPlanes:
                {
                    var saved = SavedPlanes();
                    var items = new string[saved.Count];
                    int current = -1;
                    for (int i = 0; i < items.Length; i++)
                    {
                        items[i] = saved[i].Name;
                        current = lobby.Build?.Name == saved[i].Name ? i : current;
                    }

                    return new DropdownList(items, current, _ => true, i =>
                        lobby.PickCustom(CSVM.Flight.Hangar.CustomPlaneWire.Build(saved[i])!,
                            saved[i].HasLoadout ? CoopFit.Of(saved[i].Ammo, saved[i].Ordnance) : default));
                }

            case PlaneKey:
                {
                    var items = new string[DogfightLobby.AirframeCount];
                    for (int i = 0; i < items.Length; i++)
                    {
                        items[i] = PlaneWord(i);
                    }

                    return new DropdownList(items, lobby.Airframe, _ => true, i =>
                    {
                        if (i != lobby.Airframe || lobby.Build != null)
                        {
                            lobby.Pick(i, default);
                        }
                    });
                }
        }

        var def = StockDef(lobby.Airframe);
        if (key.StartsWith(GunKeyPrefix, StringComparison.Ordinal) && GunFor(def, (OriginalWidgets.Indexed(key, GunKeyPrefix) ?? -1) + 1) is { } gun)
        {
            int slot = (OriginalWidgets.Indexed(key, GunKeyPrefix) ?? -1);
            var options = _stock()?.Options.GunAmmo;
            var items = new string[CampaignLoadout.AmmoNames.Length];
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = options != null && i < options.Count ? options[i].Label : CampaignLoadout.AmmoNames[i];
            }

            int stored = lobby.Fit.AmmoAt(slot);
            int current = stored is >= 0 and < 4 ? stored : Math.Max(0, Array.IndexOf(CampaignLoadout.AmmoNames, gun.Ammo));
            return new DropdownList(items, current, _ => true, i => lobby.Refit(Refit(lobby.Fit, slot, i, -1, 0)));
        }

        if (key.StartsWith(CellKeyPrefix, StringComparison.Ordinal) && _stock()?.Options.PylonOrdnance is { Count: > 0 } table)
        {
            int cell = (OriginalWidgets.Indexed(key, CellKeyPrefix) ?? -1);
            int pylon = Loadout.PylonForCell(cell, def?.Hardpoints);
            if (pylon == 0)
            {
                return null;
            }

            var items = new string[table.Count];
            int current = -1;
            int entry = Array.IndexOf(Loadout.PylonFillOrder, pylon);
            string? stock = def?.Hardpoints is { } hp && entry >= 0 && entry < hp.Stock.Length ? hp.Stock[entry] : null;
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = table[i].Label;
                if (table[i].Id == stock)
                {
                    current = i;
                }
            }

            int stored = lobby.Fit.OrdnanceAt(cell);
            current = stored is > 0 && stored <= table.Count ? stored - 1 : current;
            return new DropdownList(items, current, _ => true, i => lobby.Refit(Refit(lobby.Fit, -1, 0, cell, i + 1)));
        }

        return null;
    }

    // The open dropdown's list hung under its box. The Select Plane box is taller than its items,
    // so its list starts under the box at the items' own height.
    private OpenDropList? OpenList()
    {
        if (_open is not { } key || DropdownFor(key) is not { } list)
        {
            return null;
        }

        var closed = new List<OriginalRow>();
        Widgets(closed);
        var box = closed.Find(row => row.Key == key);
        if (box == null)
        {
            return null;
        }

        float item = Math.Min(box.Height, 22f);
        return OriginalDropLists.Over(key, null, list.Items, (box.X, box.Y + box.Height - item, box.Width, item), list.Allowed);
    }

    private void PickFromList(string key, string suffix)
    {
        if (DropdownFor(key) is { } list && int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
            && index >= 0 && index < list.Items.Count && list.Allowed(index))
        {
            list.Select(index);
        }

        _open = null;
        _host.FocusKey(key);
    }

    private void ComposePlayers(BoardLayers layers)
    {
        var lobby = Lobby;
        var players = lobby?.Players ?? Array.Empty<DogfightLobbySeat>();
        layers.Lines.Add(_text.Line(10046, "MULTIPLAYER LOBBY", 60f, 22f, 0f, Black));
        int cap = _net()?.SessionCap ?? NetSeats.MaxPlayers;
        string count = _text.Strings.Format(10048, players.Count, cap);
        layers.Lines.Add(_text.Line(10048, string.Empty, 34f, 54f, 0f, Black,
            text: count.Length > 0 ? count : $"Players ({players.Count} of {cap})"));
        layers.Lines.Add(_text.Line(10052, "Ready", 256f, 54f, 0f, Black));
        var face = _text.Regular(10575);
        float size = face?.Pixels ?? MultiplayerBoardText.TextFallback;
        var mark = new BoardArt(BoardArtLibrary.Ui, MarkArt, 4);
        var entries = lobby != null ? ListEntries(lobby) : new List<ListEntry>();
        for (int slot = 0; slot < entries.Count && slot < VisiblePlayers; slot++)
        {
            var entry = entries[slot];
            float y = ListY + (slot * ListPitch);
            if (entry.Player < 0)
            {
                // A team row takes its team's colour, the table the original indexes by team slot.
                uint rgb = NetSeats.SeatColor((entry.Team - 1) % NetSeats.SeatCapacity);
                var tint = new BoardTint((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
                layers.Lines.Add(new BoardLine(entry.Text, ListX + 2f, y + ((ListPitch - size) / 2f), NameWidth - 4f, size,
                    BoardInk.Row, -1, Face: face, Colour: tint));
                continue;
            }

            float indent = entry.Team != 0 ? MemberIndent : 0f;
            var colour = lobby != null && entry.Player == lobby.You ? OwnName : Black;
            layers.Lines.Add(new BoardLine(entry.Text, ListX + 2f + indent, y + ((ListPitch - size) / 2f), NameWidth - 4f - indent, size,
                BoardInk.Row, -1, Face: face, Colour: colour));
            layers.Pictures.Add(new BoardPicture(mark, ListX + NameWidth, y + 6f, players[entry.Player].Ready ? 3 : 1));
        }
    }

    private void ComposeChat(BoardLayers layers)
    {
        var lobby = Lobby;
        var chat = lobby?.Chat ?? Array.Empty<DogfightChatLine>();
        string own = lobby != null && lobby.You < lobby.Players.Count ? lobby.Players[lobby.You].Name : string.Empty;
        var face = _text.Regular(10575);
        float size = face?.Pixels ?? MultiplayerBoardText.TextFallback;
        float pitch = size + 2f;
        int fits = Math.Max(1, (int)(ChatHeight / pitch));

        // ⚠ Do not post the host's address as a chat note; it would outlive the code that replaces it.
        // The pinned rows follow the door each frame, so the address shows only while there is no code.
        var pinned = NetworkRows;
        int top = Math.Min(pinned.Count, fits - 1);
        float copy = CopyRow() != null ? CopyWidth + 6f : 0f;
        for (int i = 0; i < top; i++)
        {
            float y = ChatY + (i * pitch);
            if (i == 0)
            {
                layers.Lines.Add(new BoardLine(CoopDoorText.NoteName, ChatX + 4f, y, ChatNameColumn - 8f, size, BoardInk.Row, -1,
                    Face: face, Colour: Black));
            }

            layers.Lines.Add(new BoardLine(pinned[i], ChatX + ChatNameColumn, y, ChatTextWidth - (i == 0 ? copy : 0f), size,
                BoardInk.Row, -1, Face: face, Colour: Pinned));
        }

        int first = Math.Max(0, chat.Count - (fits - top));
        for (int i = first; i < chat.Count; i++)
        {
            float y = ChatY + ((i - first + top) * pitch);
            layers.Lines.Add(new BoardLine(chat[i].Name, ChatX + 4f, y, ChatNameColumn - 8f, size, BoardInk.Row, -1,
                Face: face, Colour: chat[i].Name == own ? OwnName : Black));
            layers.Lines.Add(new BoardLine(chat[i].Text, ChatX + ChatNameColumn, y, ChatTextWidth, size,
                BoardInk.Row, -1, Face: face, Colour: Black));
        }
    }

    // One widget in its state, and the words the script writes beside it.
    private void ComposeWidget(OriginalRow row, bool focused, bool pressed, BoardLayers layers)
    {
        if (row.Key == CopyKey)
        {
            ComposeCopy(row, focused, pressed, layers);
            return;
        }

        // A player row's name is the list's own line, so the row draws only its pick and focus.
        if (OriginalWidgets.Indexed(row.Key, PlayerKeyPrefix) is { } listed)
        {
            if (Lobby is { } lobby && lobby.PeerAt(listed) is var peer and >= 0 && peer == _picked)
            {
                layers.Fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, PickedRow.R, PickedRow.G, PickedRow.B));
            }

            if (focused)
            {
                layers.Fills.Add(_host.FocusMark(row));
            }

            return;
        }

        if (OriginalWidgets.Indexed(row.Key, TeamRowKeyPrefix) is { } team)
        {
            if (team != 0 && team == PickedTeam)
            {
                layers.Fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, PickedRow.R, PickedRow.G, PickedRow.B));
            }

            if (focused)
            {
                layers.Fills.Add(_host.FocusMark(row));
            }

            return;
        }

        switch (row.Kind)
        {
            case OriginalRowKind.TextButton:
                int tab = Array.IndexOf(TabKeys, row.Key);
                layers.Lines.Add(_text.Line(TabIds[tab], TabNames[tab], row.X, row.Y + 6f, row.Width, row.Enabled ? Black : TabDisabled,
                    BoardJustify.Center));
                break;
            case OriginalRowKind.Dropdown:
                ComposeDropdown(row, focused, layers);
                break;
            case OriginalRowKind.TextField:
                ComposeBox(row, focused, layers);
                break;
            case OriginalRowKind.Radio:
                ComposeCheck(row, focused, pressed, layers);
                break;
            default:
                ComposePlaque(row, focused, pressed, layers);
                break;
        }
    }

    // The COPY box at the right end of the line its row spans, the line itself being the pinned
    // row's words. The word is drawn on that line and the box stands round it, so the two share a
    // baseline. The focus outlines the box, which is what a pad presses.
    private void ComposeCopy(OriginalRow row, bool focused, bool pressed, BoardLayers layers)
    {
        var box = row with { X = row.X + row.Width - CopyWidth, Width = CopyWidth };
        var fill = pressed ? PickedRow : (R: (byte)222, G: (byte)207, B: (byte)156);
        layers.Fills.Add(new BoardFill(box.X, box.Y, box.Width, box.Height, fill.R, fill.G, fill.B));
        layers.Fills.Add(new BoardFill(box.X, box.Y, box.Width, box.Height, 0, 0, 0, Border: true));
        if (focused)
        {
            layers.Fills.Add(_host.FocusMark(box));
        }

        var face = _text.Regular(10575);
        float size = face?.Pixels ?? MultiplayerBoardText.TextFallback;
        layers.Lines.Add(new BoardLine(CoopDoorText.CopyButton, box.X, ChatY, box.Width, size, BoardInk.Row, -1,
            Justify: BoardJustify.Center, Face: face, Colour: Pinned));
    }

    private void ComposeDropdown(OriginalRow row, bool focused, BoardLayers layers)
    {
        var fill = row.Enabled ? (R: (byte)222, G: (byte)207, B: (byte)156) : (R: (byte)181, G: (byte)174, B: (byte)156);
        layers.Fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, fill.R, fill.G, fill.B));
        layers.Fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 0, 0, 0, Border: true));
        if (focused && row.Enabled)
        {
            layers.Fills.Add(_host.FocusMark(row));
        }

        int faceId = row.Key is EnvironmentKey or TypeKey ? 10558 : 10144;
        var face = _text.Regular(faceId);
        float size = face?.Pixels ?? MultiplayerBoardText.TextFallback;
        float textY = row.Key == PlaneKey ? row.Y + 5f : row.Y + ((row.Height - size) / 2f) - 1f;
        layers.Lines.Add(new BoardLine(row.Label, row.X + 8f, textY, row.Width - 34f, size, BoardInk.Row, -1, Face: face, Colour: Black));
        layers.Pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, ArrowArt), row.X + row.Width - 24f, row.Y + 1f,
            Opacity: row.Enabled ? 1f : DisabledArrow));

        // The Environment and Type boxes carry their titles, and a gun or a rocket box its own.
        switch (row.Key)
        {
            case EnvironmentKey:
                layers.Lines.Add(_text.Line(10096, "Mission Environment", row.X, row.Y - 20f, 0f, Black));
                break;
            case TypeKey:
                layers.Lines.Add(_text.Line(10097, "Mission Type", row.X, row.Y - 20f, 0f, Black));
                break;
            case var gun when gun.StartsWith(GunKeyPrefix, StringComparison.Ordinal):
                layers.Lines.Add(_text.Line(1008, string.Empty, row.X, row.Y - 23f, 0f, Black, text: GunTitle((OriginalWidgets.Indexed(gun, GunKeyPrefix) ?? -1))));
                break;
            case var cell when cell.StartsWith(CellKeyPrefix, StringComparison.Ordinal):
                string place = ((OriginalWidgets.Indexed(cell, CellKeyPrefix) ?? -1) + 1).ToString(CultureInfo.InvariantCulture) + ")";
                layers.Lines.Add(_text.Line(1008, string.Empty, row.X - 15f, row.Y + 3f, 0f, Black, text: place));
                break;
        }
    }

    private string GunTitle(int slot)
    {
        var gun = Lobby is { } lobby ? GunFor(StockDef(lobby.Airframe), slot + 1) : null;
        if (gun == null)
        {
            return _text.Word(10548, "No Gun");
        }

        int idx = Math.Clamp((gun.Caliber - 30) / 10, 0, 4);
        return _text.Word(3320 + idx, $".{30 + (idx * 10)}-cal.");
    }

    private void ComposeBox(OriginalRow row, bool focused, BoardLayers layers)
    {
        var face = _text.Regular(row.Key == ChatKey ? 10575 : 10105);
        float size = face?.Pixels ?? MultiplayerBoardText.TextFallback;
        if (row.Key == ChatKey)
        {
            layers.Lines.Add(_text.Line(10060, "Chat:", row.X - 42f, row.Y, 0f, Black));
        }
        else
        {
            var fill = row.Enabled ? (R: (byte)209, G: (byte)180, B: (byte)120) : (R: (byte)181, G: (byte)174, B: (byte)156);
            layers.Fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, fill.R, fill.G, fill.B));
            layers.Fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 0, 0, 0, Border: true));
        }

        var caret = focused && row.Enabled && !_host.DialogOpen ? new BoardCaret(0, 0, 0, 1f, row.Height - 4f) : (BoardCaret?)null;
        layers.Lines.Add(new BoardLine(row.Label, row.X + 4f, row.Y + ((row.Height - size) / 2f) - 1f, row.Width - 8f, size,
            BoardInk.Row, -1, Caret: caret, Face: face, Colour: Black));
    }

    private void ComposeCheck(OriginalRow row, bool focused, bool pressed, BoardLayers layers)
    {
        var lobby = Lobby;
        var options = lobby?.Options;
        bool on = row.Key switch
        {
            TimeRadioKey => options is { } time && DogfightLobby.Arms(time.Victory, DogfightVictory.Time),
            ScoreRadioKey => options is { } score && DogfightLobby.Arms(score.Victory, DogfightVictory.Score),
            TeamsKey => options?.RestrictTeams == true,
            FlagHomeKey => options?.FlagHomeToCapture == true,
            LimitedLivesKey => options?.LimitedLives == true,
            AutoRespawnKey => options?.AutoRespawn == true,
            CustomPlanesKey => lobby?.Rules.AllowCustom == true,
            OutlawKey => lobby?.Rules.Outlawing == true,
            ReadyKey => lobby?.Ready == true,
            _ => false,
        };

        layers.Pictures.Add(new BoardPicture(row.Art!, row.X, row.Y, (on ? 4 : 0) + StateFrame(row, focused, pressed)));
        var (id, word) = row.Key switch
        {
            TimeRadioKey => (10105, "Time (mins):"),
            ScoreRadioKey => (10107, "Score"),
            TeamsKey => (10108, "Restrict Number of Teams"),

            // No langui row words it, so the fallback is always what shows.
            FlagHomeKey => (-1, "Own Flag Home to Capture"),
            LimitedLivesKey => (10110, "Limited Lives"),
            AutoRespawnKey => (10111, "Auto Respawn"),
            CustomPlanesKey => (10112, "Allow Custom Planes"),
            OutlawKey => (10113, "Outlaw Components"),
            _ => (0, string.Empty),
        };

        if (row.Key == ReadyKey)
        {
            bool host = lobby?.IsHost == true;
            layers.Lines.Add(_text.Line(host ? 10063 : 10059, "Ready?", row.X - 100f, row.Y - 20f, 160f, ReadyLabel, BoardJustify.Right));
        }
        else if (id != 0)
        {
            float lift = row.Key == AutoRespawnKey ? 3f : 2f;
            layers.Lines.Add(_text.Line(id, word, row.X + (row.Key is TimeRadioKey or ScoreRadioKey ? 25f : 20f), row.Y - lift, 0f,
                row.Enabled || row.Key is TimeRadioKey or ScoreRadioKey or TeamsKey or FlagHomeKey or LimitedLivesKey or AutoRespawnKey or CustomPlanesKey or OutlawKey
                    ? Black
                    : TabDisabled));
        }
    }

    private void ComposePlaque(OriginalRow row, bool focused, bool pressed, BoardLayers layers)
    {
        bool subTab = row.Key is DefaultPlanesKey or CustomTabKey or GunsTabKey or RocketsTabKey;
        bool picked = (row.Key == DefaultPlanesKey && !CustomPlanes) || (row.Key == CustomTabKey && CustomPlanes)
            || (row.Key == GunsTabKey && !Rockets) || (row.Key == RocketsTabKey && Rockets);
        int frame = !row.Enabled ? 0 : subTab && picked ? 3 : ComposedBoard.PlaqueFrame(row.Art!.Frames, focused, pressed);
        layers.Pictures.Add(new BoardPicture(row.Art!, row.X, row.Y, frame));
        var (id, word) = row.Key switch
        {
            LaunchKey => (10102, "LAUNCH!"),
            SelectKey => Lobby?.IsHost == false ? (10508, "View...") : (10103, "Select..."),
            DefaultPlanesKey => (10115, "Default Planes"),
            CustomTabKey => (10116, "Custom Planes"),
            GunsTabKey => (10120, "Guns"),
            RocketsTabKey => (10121, "Rockets"),
            BootKey => (10054, "Boot"),
            TeamKey => Lobby is { } lobby ? TeamButton(lobby) switch
            {
                TeamButtonAction.Leave => (10058, "Leave Team"),
                TeamButtonAction.Join => (10057, "Join Team"),
                _ => (10056, "Create Team"),
            } : (10056, "Create Team"),
            SendKey => (10061, "Send"),
            LeaveKey => (10062, "Leave Game"),
            _ => (0, string.Empty),
        };

        if (id == 0)
        {
            return;
        }

        BoardTint tint;
        if (subTab)
        {
            tint = !row.Enabled ? MultiplayerBoardText.LabelDisabled : picked ? Picked : Black;
        }
        else if (row.Key == LaunchKey && row.Enabled && !focused && !pressed)
        {
            tint = _blink == 0 ? LaunchBlink : MultiplayerBoardText.LabelNormal;
        }
        else
        {
            tint = MultiplayerBoardText.LabelTint(row.Enabled, focused, pressed);
        }

        var face = _text.Regular(id);
        float size = face?.Pixels ?? MultiplayerBoardText.TextFallback;
        layers.Lines.Add(new BoardLine(_text.Word(id, word), row.X, row.Y + ((row.Height - size) / 2f) - 1f, row.Width, size,
            BoardInk.Row, -1, Justify: BoardJustify.Center, Face: face, Colour: tint));
    }

    // The words and pictures a tab page carries beside its widgets.
    private void ComposePage(BoardLayers layers)
    {
        if (Lobby is not { } lobby)
        {
            return;
        }

        switch (Tab)
        {
            case LobbyTab.Mission:
                ComposeMission(lobby, layers);
                break;
            case LobbyTab.Plane:
                ComposePlane(lobby, layers);
                break;
            case LobbyTab.Ammo:
                layers.Lines.Add(_text.Line(10549, "Select Ammo", PageX + 11f, PageY + 43f, 0f, Black));
                layers.Pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, IconArt, IconFrames), PageX + 251f, PageY + 102f,
                    lobby.Airframe));
                layers.Lines.Add(_text.Line(10550, "No Information Available", PageX + 251f, PageY + 176f, 192f, Black,
                    BoardJustify.Center, faceId: 10144));
                break;
            case LobbyTab.Scores:
                ComposeScores(lobby, layers);
                break;
        }
    }

    private void ComposeMission(DogfightLobby lobby, BoardLayers layers)
    {
        var options = lobby.Options;
        if (DogfightLobby.IsStuntRace(options))
        {
            // The remake's own line, in the face the string table's three are drawn in.
            layers.Lines.Add(_text.Line(10123, string.Empty, PageX + 25f, PageY + 134f, 200f, Black, text: StuntRaceDescription));
        }
        else
        {
            int type = Math.Clamp((int)options.MissionType, 0, TypeDescriptions.Length - 1);
            layers.Lines.Add(_text.Line(TypeDescriptions[type], string.Empty, PageX + 25f, PageY + 134f, 200f, Black));
        }

        layers.Lines.Add(_text.Line(10098, "Victory Conditions", PageX + 241f, PageY + 46f, 0f, Black));
        layers.Lines.Add(_text.Line(10099, "Teams", PageX + 241f, PageY + 110f, 0f, Black));
        layers.Lines.Add(_text.Line(10100, "Lives", PageX + 241f, PageY + 170f, 0f, Black));
        layers.Lines.Add(_text.Line(10101, "Planes", PageX + 241f, PageY + 227f, 0f, Black));

        // The team count boxes are widgets; "to" stands between them, 20 left of the second.
        layers.Lines.Add(_text.Line(10109, "to", PageX + 367f, PageY + 154f, 0f, Black));
    }

    private void ComposePlane(DogfightLobby lobby, BoardLayers layers)
    {
        layers.Lines.Add(_text.Line(10114, "Select Plane", PageX + 11f, PageY + 43f, 0f, Black));
        int airframe = lobby.Airframe;
        string plane = _text.Word(10566, "Plane:") + " " + _text.Word(10565, "Stock");
        layers.Lines.Add(_text.Line(10566, "Plane:", PageX + 225f, PageY + 75f, 0f, Black, text: plane));
        layers.Lines.Add(_text.Line(3000 + airframe, ShortNames[airframe], PageX + 225f, PageY + 90f, 220f, Black, faceId: 10566));
        layers.Pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, IconArt, IconFrames), PageX + 251f, PageY + 102f, airframe));
        var fit = PlaneFit.For(airframe, null, StockDef(airframe));
        var ratings = PlaneRatings.For(fit);
        for (int i = 0; i < RatingLabels.Length; i++)
        {
            int at = Math.Clamp(ratings[i], 0, RatingWords.Length - 1);
            string text = $"{RatingLabels[i]}  {_text.Word(501 + at, RatingWords[at])}";
            layers.Lines.Add(_text.Line(1008, string.Empty, PageX + 225f, PageY + 170f + (15f * i), 0f, Black, text: text));
        }

        float y = PageY + 230f;
        foreach (int calibre in Calibres)
        {
            if (fit.Barrels.TryGetValue(calibre, out int barrels))
            {
                int idx = Math.Clamp((calibre - 30) / 10, 0, 4);
                string text = $"({barrels}) {_text.Word(3320 + idx, $".{calibre}-cal.")}";
                layers.Lines.Add(_text.Line(1008, string.Empty, PageX + 225f, y, 0f, Black, text: text));
                y += 15f;
            }
        }

        if (fit.Hardpoints > 0)
        {
            layers.Lines.Add(_text.Line(1008, "Hardpoints", PageX + 225f, y, 0f, Black,
                text: $"({fit.Hardpoints}) {_text.Word(1008, "Hardpoints")}"));
        }
    }

    // The scores page: the headers over the last match's lines, best first. A team match lists
    // each team's line with its pilots indented under it. Hits % stays blank, since no end counts a
    // pilot's hits.
    private void ComposeScores(DogfightLobby lobby, BoardLayers layers)
    {
        for (int i = 0; i < ScoreHeaderIds.Length; i++)
        {
            layers.Lines.Add(_text.Line(ScoreHeaderIds[i], ScoreHeaders[i], PageX + ScoreHeaderAt[i].X, PageY + ScoreHeaderAt[i].Y, 0f, Black));
        }

        var scores = lobby.Scores;
        bool teams = System.Linq.Enumerable.Any(scores, line => line.IsTeam);
        for (int i = 0; i < scores.Count && i < VisiblePlayers; i++)
        {
            float y = PageY + 69f + (ListPitch * i);
            var line = scores[i];
            float indent = teams && !line.IsTeam ? 12f : 0f;
            layers.Lines.Add(_text.Line(10575, string.Empty, PageX + 24f + indent, y, 150f - indent, Black, text: line.Name));
            int[] numbers = { line.Points, line.Kills, line.Deaths };
            for (int column = 0; column < numbers.Length; column++)
            {
                layers.Lines.Add(_text.Line(10575, string.Empty, PageX + ScoreHeaderAt[column + 1].X, y, 0f, Black,
                    text: numbers[column].ToString(CultureInfo.InvariantCulture)));
            }
        }
    }

    // The open list over the finished page: its items on the box's own fill, the picked one marked.
    private void ComposeOpenList(IReadOnlyList<OriginalRow> rows, int focus, BoardLayers layers)
    {
        var fills = new List<BoardFill>();
        var lines = new List<BoardLine>();
        int picked = _open != null && DropdownFor(_open) is { } open ? open.Current : -1;
        var face = _text.Regular(_open is EnvironmentKey or TypeKey ? 10558 : 10144);
        float size = face?.Pixels ?? MultiplayerBoardText.TextFallback;
        float top = float.MaxValue, bottom = float.MinValue, left = 0f, width = 0f;
        foreach (var row in rows)
        {
            if (row.Visible && row.Kind == OriginalRowKind.ListRow)
            {
                top = Math.Min(top, row.Y);
                bottom = Math.Max(bottom, row.Y + row.Height);
                left = row.X;
                width = row.Width;
            }
        }

        if (top < bottom)
        {
            fills.Add(new BoardFill(left, top, width, bottom - top, 222, 207, 156));
            fills.Add(new BoardFill(left, top, width, bottom - top, 0, 0, 0, Border: true));
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var item = rows[i];
            if (!item.Visible || item.Kind != OriginalRowKind.ListRow)
            {
                continue;
            }

            if (OriginalDropLists.IndexOf(item.Key) == picked)
            {
                fills.Add(new BoardFill(item.X, item.Y, item.Width, item.Height, 180, 147, 78));
            }
            else if (i == focus)
            {
                fills.Add(new BoardFill(item.X, item.Y, item.Width, item.Height, 209, 180, 120));
            }

            lines.Add(new BoardLine(item.Label, item.X + 8f, item.Y + ((item.Height - size) / 2f) - 1f, item.Width - 12f, size,
                BoardInk.Row, i, Face: face, Colour: item.Enabled ? Black : TabDisabled));
        }

        layers.Overlays.Add(new BoardPanel(fills, Array.Empty<BoardPicture>(), lines));
    }

    private readonly record struct ListEntry(byte Team, int Player, string Text);

    private sealed record DropdownList(IReadOnlyList<string> Items, int Current, Func<int, bool> Allowed, Action<int> Select);
}
