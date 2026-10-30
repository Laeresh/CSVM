using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Net;
using CSVM.Session.Roster;
using CSVM.Spec;

namespace CSVM.UI.Menu;

/// <summary>One line of the lobby's chat panel: who typed it and what.</summary>
public readonly record struct DogfightChatLine(string Name, string Text);

/// <summary>One line on the Game Scores page after a match: a name and its final points, kills and
/// deaths. A team's line sets <paramref name="IsTeam"/>, and a computer pilot's sets
/// <paramref name="IsBot"/>, which the page tags.</summary>
public readonly record struct DogfightScore(string Name, int Points, int Kills, int Deaths, bool IsTeam = false, bool IsBot = false);

/// <summary>One bot row a Dogfight host keeps. Its id stays with the row while the rows around it
/// come and go. It carries a callsign, a stock airframe or
/// <see cref="DogfightLobbySeat.RandomAirframe"/>, a skill tier and a lobby team (0 for none).
/// <paramref name="Placed"/> marks a team the host picked. The lobby balances any other.
/// </summary>
public readonly record struct DogfightBot(int Id, string Callsign, byte Airframe, NetBotSkill Skill, byte Team, bool Placed = false)
{
    /// <summary>Whether the host resolves this bot's plane at launch.</summary>
    public bool RandomPlane => Airframe == DogfightLobbySeat.RandomAirframe;
}

/// <summary>One pilot's line of a stunt race's table, every column already in its words. They are
/// the place and callsign, the aircraft, the best or furthest run, the gap and the runs. A pilot
/// who left the race carries <paramref name="Left"/>, and the table draws the row grey.</summary>
public sealed record RaceTableRow(string Pilot, string Aircraft, string Best, string Gap, string Runs, bool Left = false);

/// <summary>
/// The Multiplayer Lobby of a Dogfight, on both of its ends, engine-free. The host owns the Mission
/// Options, the plane rules and the player list with its bot rows, and each guest reads them off
/// the wire. Every pilot picks a stock airframe or one of its custom planes, and a fit, and marks
/// itself Ready on a plane the rules admit. The host launches once every pilot is Ready. A guest's
/// pick travels as a
/// <see cref="CoopPickMessage"/> under the round the host's options name, so an option change clears
/// every Ready. A match flown from here lands back on it with its scores and its bot rows.
/// </summary>
public sealed class DogfightLobby
{
    /// <summary>How many environments the Environment box lists.</summary>
    public const int EnvironmentCount = 7;

    /// <summary>The Time box's value when a lobby opens, the original's own default.</summary>
    public const int DefaultTimeMinutes = 10;

    /// <summary>The Score box's value when a lobby opens, the original's own default.</summary>
    public const int DefaultScore = 40;

    /// <summary>The Lives box's value once Limited Lives is ticked. The one invented number in the
    /// lives rule: the original leaves the box empty (docs/org/multiplayer-scoring.md).</summary>
    public const int DefaultLives = 3;

    /// <summary>The largest time the two-character wide box takes, in minutes.</summary>
    public const int MaxTimeMinutes = 99;

    /// <summary>The largest score the Score box takes.</summary>
    public const int MaxScore = 999;

    /// <summary>The largest life count the two-character Lives box takes.</summary>
    public const int MaxLives = 99;

    /// <summary>The largest team count the Restrict Number of Teams boxes take, the script's
    /// <c>WF = 16</c> for a Deathmatch.</summary>
    public const int MaxTeams = 16;

    /// <summary>How many teams a Capture the Flag launch takes, one per flag. It fixes the Restrict
    /// Number of Teams boxes at this, and langui 10519 says "one of two teams".</summary>
    public const int CtfTeams = 2;

    /// <summary>The name of the team the bots open when one team alone stands in a team match.
    /// Remake-only rule: the original has no bots.</summary>
    public const string BotTeamName = "Bots";

    /// <summary>How many types the Type box lists: the string table's three and Stunt Race.</summary>
    public const int TypeCount = 4;

    /// <summary>The Type box's word for Stunt Race, a remake-only mode, so no string table id
    /// names it.</summary>
    public const string StuntRaceName = "Stunt Race";

    /// <summary>The Time box's value when the Type turns to Stunt Race, the race window's default.
    /// </summary>
    public const int StuntRaceDefaultMinutes = Mech3.InstantActionDef.DefaultRaceWindowMinutes;

    /// <summary>How many chat lines the panel keeps; older lines scroll away.</summary>
    public const int ChatDepth = 64;

    /// <summary>The airframe a pilot flies until it picks one: the Devastator, the original's first
    /// row.</summary>
    public const byte DefaultAirframe = CoopGuestPick.StarterAirframe;

    /// <summary>How many stock airframes the Select Plane list offers.</summary>
    public const int AirframeCount = 11;

    /// <summary>The pilot count Fill-to-N's box opens on: the eight the original's colour table and
    /// respawn fan were authored for. A remake-only choice.</summary>
    public const int DefaultFillTo = 8;

    // The Environment box's words in the string table's order, and the chapter each is flown on.
    private static readonly string[] EnvironmentNames =
    {
        "Above the Clouds", "Hawai'ian Islands", "Hollywood Studio", "Manhattan",
        "NW Boeing Field", "NW Lighthouse", "Sky Haven (Rockies)",
    };

    // ⚠ Above the Clouds is C1C, not Instant Action's clouds chapter C2B. The two ship the same MP1
    // and MP3 layouts, so the data alone cannot tell them apart; the row's number (below) does.
    private static readonly string[] EnvironmentChapters = { "C1C", "C3", "C2", "C5", "C1", "C1B", "C4" };

    // The number each row writes to the environment global, the jump table at 0x413c08. Each is its
    // row's chapter's world number.
    private static readonly int[] EnvironmentNumbers = { 3, 6, 4, 8, 1, 2, 7 };

    private readonly NetLobby _wire;
    private readonly Func<string> _name;
    private readonly int _hostPeer;
    private readonly List<DogfightChatLine> _chat = new();
    private readonly Dictionary<int, DogfightOptionsMessage> _optionsSent = new();
    private readonly Dictionary<int, DogfightRosterMessage> _rosterSent = new();
    private readonly Dictionary<int, LobbyPlaneRulesMessage> _rulesSent = new();
    private readonly Dictionary<int, LobbyTeamsMessage> _teamsSent = new();
    private readonly List<PlaneRefusal> _refusals = new();
    private readonly NetTeamBook _teams;
    private readonly DogfightBots _bots = new();

    // Each seat's pick by its place among this machine's seats, seat 0 first.
    private readonly SeatPicked[] _picks = SeatPicked.All();
    private DogfightOptionsMessage _options = OpeningOptions;

    // Clear until a lobby screen first stands on a host, which ticks Allow Custom Planes (Show).
    private NetPlaneRules _rules;
    private bool _ready;
    private byte _readyEpoch;

    // How many seats flew when this machine marked Ready. A change in them clears the mark.
    private int _readySeats;
    private string[] _launchNames = Array.Empty<string>();
    private DogfightScore[] _scores = Array.Empty<DogfightScore>();
    private RaceTableRow[] _raceScores = Array.Empty<RaceTableRow>();

    /// <summary>A host's lobby over <paramref name="wire"/>, its pilot named by
    /// <paramref name="name"/>.</summary>
    public DogfightLobby(NetLobby wire, Func<string> name)
        : this(wire, name, -1)
    {
    }

    /// <summary>A guest's lobby over <paramref name="wire"/>, following the host at
    /// <paramref name="hostPeer"/>, its pilot named by <paramref name="name"/>.</summary>
    public DogfightLobby(NetLobby wire, Func<string> name, int hostPeer)
    {
        _wire = wire ?? throw new ArgumentNullException(nameof(wire));
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _hostPeer = hostPeer;
        _teams = new NetTeamBook(team => _bots.Rows.Any(bot => bot.Team == team));
    }

    /// <summary>The Mission Options a lobby opens on: a Deathmatch on the first environment, won on
    /// <see cref="DefaultTimeMinutes"/> with <see cref="DefaultScore"/> in the Score box. A local
    /// match opens on them too.</summary>
    public static DogfightOptionsMessage OpeningOptions => new(
        1, 0, (byte)DogfightMissionType.Deathmatch, DogfightVictory.Time, DefaultTimeMinutes, DefaultScore,
        false, DefaultLives, true);

    /// <summary>Whether a host lists and tells a peer on the wire. Every peer is, unless the door
    /// refused it, as it refuses a guest past its chosen cap.</summary>
    public Func<int, bool> Seated { get; init; } = _ => true;

    /// <summary>The voice byte this pilot's pick carries (<see cref="CoopPickMessage.Voice"/>).
    /// </summary>
    public Func<byte> Voice { get; init; } = () => CoopPickMessage.NoVoice;

    /// <summary>How many seats this machine flies, its splitscreen seats included. A host lists each
    /// on a row of its own, and a guest asks its host for each. A machine's seats fly on its team.
    /// </summary>
    public Func<int> LocalSeats { get; init; } = () => 1;

    /// <summary>How many seats a host gave the guest at a peer: its first, and each further one the
    /// player cap left room for. One each unless the door says otherwise.</summary>
    public Func<int, int> SeatsOf { get; init; } = _ => 1;

    /// <summary>The callsigns a new bot row draws from, the shipped pilot names
    /// (<c>Session/Roster/BotSeats.cs</c>). Empty, every bot is called <c>Bot n</c>.</summary>
    public IReadOnlyList<string> CallsignPool
    {
        get => _bots.CallsignPool;
        set => _bots.CallsignPool = value;
    }

    /// <summary>The draws a new bot row's callsign takes, the launch's own bot-field stream unless a
    /// caller pins one.</summary>
    public Random BotDraws
    {
        get => _bots.Draws;
        init => _bots.Draws = value;
    }

    /// <summary>Whether this end owns the options and launches the match.</summary>
    public bool IsHost => _hostPeer < 0;

    /// <summary>The host's bot rows in the order they were added, the order they are listed and
    /// seated in. A person seated past <see cref="NetSeats.MaxPlayers"/> pilots takes the last
    /// row's place on the next <see cref="Step"/>. Empty on a guest, which reads them as rows of
    /// <see cref="Players"/>.</summary>
    public IReadOnlyList<DogfightBot> Bots => _bots.Rows;

    /// <summary>How many pilots the field holds: every seat each machine flies and one per bot, a
    /// row each. A guest counts the host's rows.</summary>
    public int FieldSeats => IsHost ? SeatOwners.Count + _bots.Count : Players.Count;

    /// <summary>How many seats this machine flies in the match, out of its <see cref="LocalSeats"/>.
    /// Its rows stand side by side from <see cref="You"/>. A guest flies as many as its host gave
    /// it, read off the host's list.</summary>
    public int SeatsFlown => Math.Clamp(
        IsHost || _wire.DogfightRoster is not { } roster ? LocalSeats() : Math.Min(roster.YourSeats, LocalSeats()),
        1, _picks.Length);

    /// <summary>The machine and the place among its seats behind each person's row on a host's list.
    /// The host's own seats come first, then each seated guest's given seats side by side. A launch
    /// seats them in this order, so a row's place is its seat's. Empty on a guest.</summary>
    public IReadOnlyList<(int Peer, int Local)> SeatOwners
    {
        get
        {
            var owners = new List<(int Peer, int Local)>();
            if (!IsHost)
            {
                return owners;
            }

            for (int local = 0; local < SeatsFlown; local++)
            {
                owners.Add((_wire.LocalPeer, local));
            }

            foreach (int peer in SeatedPeers())
            {
                for (int local = 0; local < Math.Max(1, SeatsOf(peer)); local++)
                {
                    owners.Add((peer, local));
                }
            }

            return owners;
        }
    }

    /// <summary>How many more bots the field takes before it holds <see cref="NetSeats.MaxPlayers"/>
    /// pilots. None on a guest.</summary>
    public int BotRoom => IsHost ? DogfightBots.Room(FieldSeats) : 0;

    /// <summary>Whether the host may add, edit or remove a bot row: on the host while it is not
    /// Ready, the gate its options have.</summary>
    public bool EditsBots => IsHost && !Ready;

    /// <summary>The bot rows as the launch seats them, under every mission type. Capture the Flag,
    /// Zeppelin vs Zeppelin and the Stunt Race fly them on their bot objectives. A Random plane is null,
    /// which the host draws (<c>BotSeats.Resolve</c>), and a stock one is its node. Empty on a guest.</summary>
    public IReadOnlyList<VsBotEntry> LaunchBots => _bots.LaunchEntries();

    /// <summary>Whether a guest has heard the host's options yet. Always true on the host.</summary>
    public bool HasOptions => IsHost || _wire.DogfightOptions.HasValue;

    /// <summary>The Mission Options as this end stands on them: its own on the host, the host's word
    /// on a guest. A guest that has heard nothing reads the opening defaults. A host's carries
    /// <see cref="DogfightOptionsMessage.HostLandsOnScores"/> while a lobby screen stands on it.</summary>
    public DogfightOptionsMessage Options => IsHost
        ? _options with { HostLandsOnScores = Shown }
        : _wire.DogfightOptions ?? _options with { Epoch = 0 };

    /// <summary>The stock airframe this pilot picked, an index into the eleven.</summary>
    public byte Airframe => _picks[0].Airframe;

    /// <summary>The ammunition and ordnance this pilot picked, stock when all zero.</summary>
    public CoopFit Fit => _picks[0].Fit;

    /// <summary>The fit this pilot flies: its pick as <see cref="NetPlaneRules.Enforce"/> leaves it.
    /// The host reads a guest's fit through the same rules, so both ends agree.</summary>
    public CoopFit LaunchFit => Rules.Enforce(Fit);

    /// <summary>The custom plane this pilot picked, or null on a stock airframe.</summary>
    public NetPlaneBuild? Build => _picks[0].Build;

    /// <summary>The plane rules as this end stands on them: its own on the host, the host's word on
    /// a guest. A guest that has heard nothing reads cleared rules, which allow no custom plane.
    /// </summary>
    public NetPlaneRules Rules => IsHost ? _rules : _wire.PlaneRules?.Rules ?? default;

    /// <summary>Why the rules refuse this pilot's plane, or none.</summary>
    public PlaneRefusal Refusal => Rules.Refuses(Airframe, Build);

    /// <summary>Whether this machine marked itself Ready under the current round, on a plane the
    /// rules admit, for the seats it flies now. A seat given or taken since clears the mark.</summary>
    public bool Ready => _ready && _readyEpoch == Options.Epoch && _readySeats == SeatsFlown && Refusal == PlaneRefusal.None;

    /// <summary>Why the last <see cref="SetReady"/> was refused, in the order the original lists
    /// them. Empty after a mark that took.</summary>
    public IReadOnlyList<PlaneRefusal> ReadyRefusals => _refusals;

    /// <summary>The chat panel's lines, oldest first.</summary>
    public IReadOnlyList<DogfightChatLine> Chat => _chat;

    /// <summary>The player list, the host first: each pilot's name, airframe and Ready mark.</summary>
    public IReadOnlyList<DogfightLobbySeat> Players => IsHost ? HostRows() : _wire.DogfightRoster?.Rows ?? GuestAlone();

    /// <summary>This pilot's own row in <see cref="Players"/>.</summary>
    public int You => IsHost ? 0 : _wire.DogfightRoster?.You ?? 0;

    /// <summary>Whether every pilot in the lobby is Ready under the current round, the host included.
    /// A guest reads the host's list for it.</summary>
    public bool AllReady
    {
        get
        {
            foreach (var player in Players)
            {
                if (!player.Ready)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Whether LAUNCH! is live: on the host, once every pilot is Ready. The press still
    /// asks <see cref="LaunchRefusal"/>, as the original's LAUNCH! asks its team counts.</summary>
    public bool CanLaunch => IsHost && AllReady;

    /// <summary>The teams standing, in the order they were created: the host's book on the host,
    /// the host's word on a guest.</summary>
    public IReadOnlyList<LobbyTeamName> Teams
    {
        get
        {
            if (!IsHost)
            {
                return _wire.Teams?.Teams ?? Array.Empty<LobbyTeamName>();
            }

            var teams = new List<LobbyTeamName>(_teams.Count);
            foreach (var team in _teams.Teams)
            {
                teams.Add(new LobbyTeamName(team.Number, team.Name));
            }

            return teams;
        }
    }

    /// <summary>The team this pilot is on, 0 for none.</summary>
    public byte OwnTeam => You < Players.Count ? Players[You].Team : (byte)0;

    /// <summary>Whether any pilot on the list is on a team, which flies a Deathmatch as a team
    /// one.</summary>
    public bool Teamed
    {
        get
        {
            foreach (var row in Players)
            {
                if (row.Team != 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Why the host's launch is refused on its teams, or none. Each row counts one player,
    /// a bot's and each seat of a machine's included.</summary>
    public TeamLaunchRefusal LaunchRefusal
    {
        get
        {
            // A race flies no teams, so whatever the greyed boxes stood on cannot hold it up.
            if (IsStuntRace(Options))
            {
                return TeamLaunchRefusal.None;
            }

            var weighed = Weighed();
            var options = Options;
            var refusal = NetTeamBook.Check(weighed, options.RestrictTeams, options.MinTeams, options.MaxTeams);

            // A host fielding bots sets the odds itself, four people against eight bots included.
            if (refusal == TeamLaunchRefusal.Unbalanced && BotsListed > 0)
            {
                refusal = TeamLaunchRefusal.None;
            }

            if (refusal != TeamLaunchRefusal.None || !IsCtf(options))
            {
                return refusal;
            }

            // FUN_00495d40 builds flag n for team number n, and the maps lay out two. A team
            // numbered past them would fly with no flag.
            foreach (var (team, _) in weighed)
            {
                if (team > CtfTeams)
                {
                    return TeamLaunchRefusal.TooFewTeams;
                }
            }

            return TeamLaunchRefusal.None;
        }
    }

    /// <summary>Whether a lobby screen stands on this lobby. Until one does, its guest sends no
    /// pick and its flight ends on the results board.</summary>
    public bool Shown { get; private set; }

    /// <summary>The last match's lines for the Game Scores page, best first, or empty before any
    /// match has been flown from this lobby.</summary>
    public IReadOnlyList<DogfightScore> Scores => _scores;

    /// <summary>The last stunt race's table for the Game Scores page, best first, or empty when the
    /// last flight from this lobby was no race.</summary>
    public IReadOnlyList<RaceTableRow> RaceScores => _raceScores;

    /// <summary>Whether a flight from this lobby has landed anything on the Game Scores page.</summary>
    public bool HasScores => _scores.Length > 0 || _raceScores.Length > 0;

    /// <summary>The players named at the last launch, in the list's order. That is seat order only
    /// while every machine flies one seat; the session's seat roster names a seat for certain.
    /// </summary>
    public IReadOnlyList<string> LaunchNames => _launchNames;

    /// <summary>The people in <see cref="Players"/>, a row for every seat each machine flies, which
    /// the list's header counts against the session's cap.</summary>
    public int PeopleListed => Players.Count - BotsListed;

    /// <summary>The bot rows in <see cref="Players"/>, which the list's header names apart from the
    /// people.</summary>
    public int BotsListed
    {
        get
        {
            int bots = 0;
            foreach (var row in Players)
            {
                bots += row.IsBot ? 1 : 0;
            }

            return bots;
        }
    }

    /// <summary>The environment's words as the Environment box and the games list write them, or ""
    /// outside the seven.</summary>
    public static string EnvironmentName(int environment) =>
        environment is >= 0 and < EnvironmentCount ? EnvironmentNames[environment] : "";

    /// <summary>The chapter an environment is flown on.</summary>
    public static string ChapterOf(int environment) => EnvironmentChapters[Math.Clamp(environment, 0, EnvironmentCount - 1)];

    /// <summary>The environment flown on <paramref name="chapter"/>, or -1 for a chapter the
    /// Environment box does not list.</summary>
    public static int EnvironmentOf(string chapter) => Array.IndexOf(EnvironmentChapters, chapter);

    /// <summary>The environment number the original's session setup writes for an Environment row,
    /// which completes the row's <c>loading_m</c> dialog names (docs/org/loading-screen.md).
    /// ⚠ Keyed by the row: <c>escape.zrd</c> numbers Above the Clouds 5, and this answers 3, the
    /// number <c>Loading.zrd</c> keys that row's dialogs by.</summary>
    public static int EnvironmentNumber(int environment) =>
        EnvironmentNumbers[Math.Clamp(environment, 0, EnvironmentCount - 1)];

    /// <summary>The original's 1-based multiplayer mode for a launch (docs/org/multiplayer-spawn.md).
    /// Capture the Flag is 3 and Zeppelin vs Zeppelin 4. A Deathmatch is 2 once any pilot is on a
    /// team, else 1. Stunt Race, which the original never had, reads as a Deathmatch.</summary>
    public static int ModeOf(DogfightMissionType type, bool teamed) => type switch
    {
        DogfightMissionType.CaptureTheFlag => 3,
        DogfightMissionType.ZeppelinVsZeppelin => 4,
        _ => teamed ? 2 : 1,
    };

    /// <summary>The line a refused launch raises, the original's langui 10518 to 10520 or the
    /// remake-only line for a teamless player or unbalanced teams. Empty for none.</summary>
    public static string RefusalText(TeamLaunchRefusal refusal) => refusal switch
    {
        TeamLaunchRefusal.TooManyTeams => "There are too many teams.",
        TeamLaunchRefusal.TooFewTeams => "Each player must be on one of two teams to play.",
        TeamLaunchRefusal.NotEnoughPlayers => "There are not enough players in the game.",
        TeamLaunchRefusal.Teamless => "Every player must be on a team to play.",
        TeamLaunchRefusal.Unbalanced => "The teams must not differ by more than one player.",
        _ => string.Empty,
    };

    /// <summary>The Game Scores lines for a match's standings, best first. Each seat is named from
    /// <paramref name="names"/>, the list as it stood at the launch, or by its player tag past it.
    /// </summary>
    public static DogfightScore[] ScoresOf(IEnumerable<CSVM.Flight.Modes.VersusStanding> standings, IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        return ScoresOf(standings, seat => (NameIn(names, seat), false));
    }

    /// <summary>The Game Scores lines for a finished match. A team match lists each team's totals,
    /// best first, with its pilots under it best first. A free-for-all is
    /// <see cref="ScoresOf(IEnumerable{CSVM.Flight.Modes.VersusStanding}, IReadOnlyList{string})"/>.
    /// Each seat is named from <paramref name="names"/>, the lobby's list at the launch. That list
    /// names a seat right only while every machine flies one seat in list order.</summary>
    public static DogfightScore[] ScoresOf(CSVM.Flight.Modes.VersusMatch match, IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        return ScoresOf(match, seat => (NameIn(names, seat), false));
    }

    /// <summary>The Game Scores lines for a finished match, laid out as
    /// <see cref="ScoresOf(CSVM.Flight.Modes.VersusMatch, IReadOnlyList{string})"/> lays them out.
    /// Each seat is named by its callsign on the session's seat roster, and a bot's line is marked.
    /// A seat past the roster takes its player tag.</summary>
    public static DogfightScore[] ScoresOf(CSVM.Flight.Modes.VersusMatch match, IReadOnlyList<NetSeat> seats)
    {
        ArgumentNullException.ThrowIfNull(seats);
        return ScoresOf(match, seat => seat >= 0 && seat < seats.Count
            ? (seats[seat].Callsign, seats[seat].IsBot)
            : (NameIn(Array.Empty<string>(), seat), false));
    }

    /// <summary>The chat notice a host posts when a bot gives its place to a person who joined a
    /// full field. It extends langui 10506's form, "[%1!s! left the game.]", with the reason.
    /// </summary>
    public static string YieldLine(string bot) => $"[{bot} left the game to make room for a player.]";

    /// <summary>Whether the Type box offers a mission type as live. All four fly.</summary>
    public static bool Flies(DogfightMissionType type) =>
        type is DogfightMissionType.Deathmatch or DogfightMissionType.CaptureTheFlag
            or DogfightMissionType.ZeppelinVsZeppelin or DogfightMissionType.StuntRace;

    /// <summary>Whether the Environment box offers <paramref name="environment"/> under
    /// <paramref name="type"/>. Capture the Flag greys Above the Clouds and NW Lighthouse, the two
    /// chapters with no <c>MP2</c> map, as the mission script's type change does. Stunt Race greys
    /// every chapter whose Instant Action data ships no Danger Zones, which is Above the Clouds.</summary>
    public static bool Offers(DogfightMissionType type, int environment) =>
        environment is >= 0 and < EnvironmentCount && type switch
        {
            DogfightMissionType.CaptureTheFlag => EnvironmentChapters[environment] is not ("C1C" or "C1B"),
            DogfightMissionType.StuntRace => MenuChapters.DangerZonesFor(EnvironmentChapters[environment]),
            _ => true,
        };

    /// <summary>The type <paramref name="options"/> name. A byte past the box's four, which only a
    /// newer build writes, reads as itself and flies nothing (<see cref="Flies"/>).</summary>
    public static DogfightMissionType TypeOf(DogfightOptionsMessage options) => (DogfightMissionType)options.MissionType;

    /// <summary>Whether <paramref name="options"/> fly Capture the Flag.</summary>
    public static bool IsCtf(DogfightOptionsMessage options) => TypeOf(options) == DogfightMissionType.CaptureTheFlag;

    /// <summary>Whether <paramref name="options"/> fly Zeppelin vs Zeppelin.</summary>
    public static bool IsZvz(DogfightOptionsMessage options) => TypeOf(options) == DogfightMissionType.ZeppelinVsZeppelin;

    /// <summary>Whether <paramref name="options"/> fly a Stunt Race, whose Mission Options keep only
    /// the Time box.</summary>
    public static bool IsStuntRace(DogfightOptionsMessage options) => TypeOf(options) == DogfightMissionType.StuntRace;

    /// <summary>The menu mode a launch under <paramref name="options"/> leaves in: a stunt launch for
    /// a Stunt Race, a Dogfight for the other three.</summary>
    public static MenuMode LaunchMode(DogfightOptionsMessage options) =>
        IsStuntRace(options) ? MenuMode.Stunt : MenuMode.Versus;

    /// <summary>Whether <paramref name="options"/>' type fixes Restrict Number of Teams at two, as
    /// Capture the Flag's and Zeppelin vs Zeppelin's type changes do.</summary>
    public static bool FixesTeams(DogfightOptionsMessage options) => IsCtf(options) || IsZvz(options);

    /// <summary>The match rules a launch carries: the victory condition as a kill target, a match
    /// clock or both, and the lives rule. They say too which type it flies. A limit not chosen is
    /// off. With both, the first one reached ends the match. A Stunt Race carries its Time box alone,
    /// the race window.</summary>
    public static VersusRules RulesOf(DogfightOptionsMessage options)
    {
        if (IsStuntRace(options))
        {
            return new VersusRules(0, options.TimeMinutes, MissionType: DogfightMissionType.StuntRace);
        }

        return new VersusRules(
            options.Victory != DogfightVictory.Time ? options.Score : 0,
            options.Victory != DogfightVictory.Score ? options.TimeMinutes : 0,
            options.LimitedLives ? ClampLives(options.Lives) : 0,
            options.AutoRespawn,
            IsCtf(options) || IsZvz(options) ? TypeOf(options) : DogfightMissionType.Deathmatch,
            IsCtf(options) && options.FlagHomeToCapture);
    }

    /// <summary>Whether <paramref name="victory"/> arms <paramref name="limit"/>, Time or Score.
    /// </summary>
    public static bool Arms(DogfightVictory victory, DogfightVictory limit) => victory == limit || victory == DogfightVictory.Both;

    /// <summary>The victory condition a press on the <paramref name="limit"/> radio leaves: it
    /// arms that limit beside the other, or disarms it while the other stays. The last armed limit
    /// is never cleared, so a match always has an end.</summary>
    public static DogfightVictory Toggled(DogfightVictory victory, DogfightVictory limit)
    {
        var other = limit == DogfightVictory.Time ? DogfightVictory.Score : DogfightVictory.Time;
        if (victory == DogfightVictory.Both)
        {
            return other;
        }

        return victory == limit ? victory : DogfightVictory.Both;
    }

    /// <summary><paramref name="options"/> with the Time box at <paramref name="minutes"/>, or null
    /// outside 1 to 99. The local lobby's box takes the same range.</summary>
    public static DogfightOptionsMessage? WithTimeMinutes(DogfightOptionsMessage options, int minutes) =>
        minutes is >= 1 and <= MaxTimeMinutes ? options with { TimeMinutes = (byte)minutes } : null;

    /// <summary><paramref name="options"/> with the Score box at <paramref name="score"/>, or null
    /// outside 1 to 999 and in a Stunt Race.</summary>
    public static DogfightOptionsMessage? WithScore(DogfightOptionsMessage options, int score) =>
        score is >= 1 and <= MaxScore && !IsStuntRace(options) ? options with { Score = (ushort)score } : null;

    /// <summary><paramref name="options"/> with the Lives box at <paramref name="lives"/>, clamped to
    /// 1..99, or null in a Stunt Race and while Limited Lives is clear.</summary>
    public static DogfightOptionsMessage? WithLives(DogfightOptionsMessage options, int lives) =>
        options.LimitedLives && !IsStuntRace(options) ? options with { Lives = (byte)ClampLives(lives) } : null;

    /// <summary>A life count held to 1..99. The original clamps nothing, so its empty box launches
    /// on 0 lives and a pilot on 0 counts as alive after its first death.</summary>
    public static int ClampLives(int lives) => Math.Clamp(lives, 1, MaxLives);

    /// <summary>The lobby notice of one team event, the original's langui 10503 to 10505 lines.
    /// </summary>
    public static string TeamLine(NetTeamEventKind kind, string player, string team) => kind switch
    {
        NetTeamEventKind.Joined => $"[{player} joined team {team}.]",
        NetTeamEventKind.Left => $"[{player} left team {team}.]",
        _ => $"[{team} disbanded.]",
    };

    /// <summary>Picks the environment. Refused on a guest, outside the seven, and on one the type
    /// greys (<see cref="Offers"/>).</summary>
    public bool SetEnvironment(int environment) =>
        Offers(TypeOf(_options), environment) && Change(_options with { Environment = (byte)environment });

    /// <summary>Picks the mission type. Refused on a guest and for a greyed type. Capture the Flag
    /// and Zeppelin vs Zeppelin tick Restrict Number of Teams at two teams exactly. Stunt Race arms
    /// the Time box alone at <see cref="StuntRaceDefaultMinutes"/>. A type moves off an environment
    /// it greys to the first it offers.</summary>
    public bool SetMissionType(DogfightMissionType type)
    {
        if (!Flies(type))
        {
            return false;
        }

        if (type == DogfightMissionType.Deathmatch)
        {
            return Change(_options with { MissionType = (byte)type });
        }

        int environment = _options.Environment;
        while (!Offers(type, environment))
        {
            environment = (environment + 1) % EnvironmentCount;
        }

        if (type == DogfightMissionType.StuntRace)
        {
            return Change(_options with
            {
                MissionType = (byte)type,
                Environment = (byte)environment,
                Victory = DogfightVictory.Time,
                TimeMinutes = IsStuntRace(_options) ? _options.TimeMinutes : (byte)StuntRaceDefaultMinutes,
            });
        }

        return Change(_options with
        {
            MissionType = (byte)type,
            Environment = (byte)environment,
            RestrictTeams = true,
            MinTeams = CtfTeams,
            MaxTeams = CtfTeams,
        });
    }

    /// <summary>Picks how the match is won: Time, Score or both. Refused on a guest and in a Stunt
    /// Race, which runs on the clock alone.</summary>
    public bool SetVictory(DogfightVictory victory) =>
        victory is DogfightVictory.Time or DogfightVictory.Score or DogfightVictory.Both
        && !IsStuntRace(_options) && Change(_options with { Victory = victory });

    /// <summary>Checks or clears Restrict Number of Teams. Refused on a guest, in a type that fixes
    /// it (<see cref="FixesTeams"/>) and in a Stunt Race.</summary>
    public bool SetRestrictTeams(bool restrict) => TeamsEditable() && Change(_options with { RestrictTeams = restrict });

    /// <summary>Sets the minimum team count, held to 0 up to the maximum as the script's box binds
    /// it. Refused on a guest, while Restrict Number of Teams is clear, and where the teams are not live.</summary>
    public bool SetMinTeams(int teams) =>
        _options.RestrictTeams && TeamsEditable() && Change(_options with { MinTeams = (byte)Math.Clamp(teams, 0, _options.MaxTeams) });

    /// <summary>Sets the maximum team count, held to the minimum up to <see cref="MaxTeams"/>.
    /// Refused on a guest, while Restrict Number of Teams is clear, and where the teams are not live.</summary>
    public bool SetMaxTeams(int teams) =>
        _options.RestrictTeams && TeamsEditable() && Change(_options with { MaxTeams = (byte)Math.Clamp(teams, _options.MinTeams, MaxTeams) });

    /// <summary>Checks or clears Capture the Flag's own-flag-home rule: an enemy flag scores only
    /// while the carrier's own flag stands at its base. Refused on a guest and outside Capture the
    /// Flag.</summary>
    public bool SetFlagHomeToCapture(bool required) =>
        IsCtf(_options) && Change(_options with { FlagHomeToCapture = required });

    /// <summary>Creates a team under <paramref name="name"/> with this pilot its captain. A guest
    /// asks its host. Refused while this pilot is Ready or on a team, and for a blank name.</summary>
    public bool CreateTeam(string name)
    {
        if (Ready || OwnTeam != 0 || !NetPlayerInfo.IsValidName(name))
        {
            return false;
        }

        return Act(new LobbyTeamActionMessage(NetTeamAction.Create, 0, name.Trim()));
    }

    /// <summary>Joins team <paramref name="team"/>. A guest asks its host. Refused while this pilot
    /// is Ready or on a team, and for a team that does not stand.</summary>
    public bool JoinTeam(byte team)
    {
        bool stands = false;
        foreach (var named in Teams)
        {
            stands |= named.Number == team;
        }

        return !Ready && OwnTeam == 0 && stands && Act(new LobbyTeamActionMessage(NetTeamAction.Join, team, ""));
    }

    /// <summary>Leaves this pilot's team; a captain's leave disbands it. A guest asks its host.
    /// Refused while this pilot is Ready or on no team.</summary>
    public bool LeaveTeam() =>
        !Ready && OwnTeam != 0 && Act(new LobbyTeamActionMessage(NetTeamAction.Leave, OwnTeam, ""));

    /// <summary>The team the pilot at <paramref name="peer"/> is on, 0 for none. On the host, which
    /// alone holds the teams; the host's own seats go by its local peer.</summary>
    public byte TeamOfPeer(int peer) => IsHost ? _teams.TeamOf(peer) : (byte)0;

    /// <summary>Whether this is a guest held in the lobby while its host flies a match. Its host's
    /// advert reads <paramref name="status"/> In mission, and no options have come. It takes a seat
    /// once that match lands back here.</summary>
    public bool WaitsOnMatch(NetSessionStatus? status) => !IsHost && !HasOptions && status == NetSessionStatus.InMission;

    /// <summary>Adds a bot row on a Random plane at veteran, its callsign drawn from
    /// <see cref="CallsignPool"/> where no row holds it. The lobby balances it across the teams
    /// (<see cref="Arrange"/>) until the host picks its team. Refused on a guest, while the host is
    /// Ready and on a full field.</summary>
    public bool AddBot()
    {
        if (!EditsBots || BotRoom == 0)
        {
            return false;
        }

        _bots.Add(PeopleNames(), 0);
        return Arrange();
    }

    /// <summary>Adds bot rows as <see cref="AddBot"/> does until the field holds
    /// <paramref name="pilots"/> pilots, people and bots together, or
    /// <see cref="NetSeats.MaxPlayers"/>. A field already that large takes none, and no row is
    /// removed. Answers how many it added.</summary>
    public int FillTo(int pilots) => DogfightBots.FillTo(pilots, () => FieldSeats, AddBot);

    /// <summary>Removes the bot row <paramref name="id"/>. Refused as <see cref="AddBot"/> is, and
    /// for a row that is not there.</summary>
    public bool RemoveBot(int id) => EditsBots && _bots.Remove(id) && Arrange();

    /// <summary>Renames bot row <paramref name="id"/>, cut at the Callsign box's 12 characters.
    /// Refused for a blank name and for one another row holds.</summary>
    public bool SetBotCallsign(int id, string callsign) => EditsBots && _bots.Rename(id, callsign, PeopleNames());

    /// <summary>Puts bot row <paramref name="id"/> on one of the eleven stock airframes or on
    /// <see cref="DogfightLobbySeat.RandomAirframe"/>. Refused on an airframe the plane rules refuse
    /// a person's stock pick of.</summary>
    public bool SetBotAirframe(int id, int airframe) => EditsBots && AdmitsBotAirframe(airframe) && _bots.SetAirframe(id, airframe);

    /// <summary>Whether <see cref="SetBotAirframe"/> takes <paramref name="airframe"/> under the
    /// plane rules, so the bot's plane list greys and steps over what it would refuse.</summary>
    public bool AdmitsBotAirframe(int airframe) => airframe == DogfightLobbySeat.RandomAirframe || _rules.AdmitsStock(airframe);

    /// <summary>Sets bot row <paramref name="id"/>'s skill tier.</summary>
    public bool SetBotSkill(int id, NetBotSkill skill) => EditsBots && _bots.SetSkill(id, skill);

    /// <summary>Keeps bot row <paramref name="id"/> on standing team <paramref name="team"/> whatever
    /// the balancing does, or with 0 hands it back to the balancing. A team left with no person and
    /// no bot disbands.</summary>
    public bool SetBotTeam(int id, byte team) =>
        (team == 0 || _teams.Find(team) != null) && EditsBots && _bots.SetTeam(id, team, placed: team != 0) && Arrange();

    /// <summary>The bot row <paramref name="id"/> as the host holds it, or null.</summary>
    public DogfightBot? BotById(int id) => _bots.ById(id);

    /// <summary>The id of the bot on a host's player list row. It is -1 for a person's row, for a
    /// row past the list, and on a guest.</summary>
    public int BotAt(int row)
    {
        int index = IsHost ? row - SeatOwners.Count : -1;
        return index >= 0 && index < _bots.Count ? _bots.Rows[index].Id : -1;
    }

    /// <summary>Sets the Time box, in minutes. Refused on a guest and outside 1 to 99.</summary>
    public bool SetTimeMinutes(int minutes) => WithTimeMinutes(_options, minutes) is { } options && Change(options);

    /// <summary>Sets the Score box. Refused on a guest, outside 1 to 999 and in a Stunt Race.</summary>
    public bool SetScore(int score) => WithScore(_options, score) is { } options && Change(options);

    /// <summary>Checks or clears Limited Lives. Refused on a guest and in a Stunt Race.</summary>
    public bool SetLimitedLives(bool limited) => !IsStuntRace(_options) && Change(_options with { LimitedLives = limited });

    /// <summary>Sets the Lives box, clamped to 1..99. Refused on a guest, in a Stunt Race and while
    /// Limited Lives is clear, which is when the box is greyed.</summary>
    public bool SetLives(int lives) => WithLives(_options, lives) is { } options && Change(options);

    /// <summary>Checks or clears Auto Respawn. Refused on a guest and in a Stunt Race.</summary>
    public bool SetAutoRespawn(bool auto) => !IsStuntRace(_options) && Change(_options with { AutoRespawn = auto });

    /// <summary>Checks or clears Allow Custom Planes. Refused on a guest and in a Stunt Race, whose
    /// plane rules stand as the type change found them.</summary>
    public bool SetAllowCustomPlanes(bool allow) => !IsStuntRace(_options) && ChangeRules(_rules with { AllowCustom = allow });

    /// <summary>Checks or clears Outlaw Components, which puts the outlaw list in force. A toggle
    /// either way empties the list in the same round, as the original's 5044 does. Refused on a
    /// guest and in a Stunt Race.</summary>
    public bool SetOutlawComponents(bool outlaw) =>
        !IsStuntRace(_options) && ChangeRules(outlaw == _rules.Outlawing ? _rules : _rules with { Outlawing = outlaw, Outlawed = 0 });

    /// <summary>Sets or clears one flag of the outlaw list (<see cref="NetPlaneRules"/> names them).
    /// Refused on a guest, outside the list and in a Stunt Race.</summary>
    public bool SetOutlawed(int flag, bool outlawed) =>
        flag is >= 0 and < NetPlaneRules.Flags && !IsStuntRace(_options) && ChangeRules(_rules.With(flag, outlawed));

    /// <summary>Picks this pilot's stock airframe and fit, live whether or not it is Ready. A changed
    /// pick clears this pilot's own Ready. Refused outside the eleven stock airframes.</summary>
    public bool Pick(int airframe, CoopFit fit)
    {
        if (airframe is < 0 or >= AirframeCount)
        {
            return false;
        }

        Take(0, (byte)airframe, null, fit);
        return true;
    }

    /// <summary>Picks one of this machine's custom planes and its fit for its seat
    /// <paramref name="local"/>, seat 0 unless named. Refused past the fourth seat and on an airframe
    /// outside the eleven. Whether the rules admit it is asked at Ready, as the original asks.</summary>
    public bool PickCustom(NetPlaneBuild build, CoopFit fit, int local = 0)
    {
        ArgumentNullException.ThrowIfNull(build);
        if (local < 0 || local >= _picks.Length || build.Airframe >= AirframeCount)
        {
            return false;
        }

        Take(local, build.Airframe, build.Copy(), fit);
        return true;
    }

    /// <summary>Changes this pilot's fit alone, keeping the plane it stands on.</summary>
    public void Refit(CoopFit fit) => Take(0, Airframe, Build, fit);

    /// <summary>The airframe, fit and custom plane (null on a stock one) this machine's seat
    /// <paramref name="local"/> picked, counted from 0 among its own. Seat 0's are
    /// <see cref="Airframe"/>, <see cref="Fit"/> and <see cref="Build"/>.</summary>
    public (byte Airframe, CoopFit Fit, NetPlaneBuild? Build) SeatPick(int local)
    {
        var seat = _picks[Math.Clamp(local, 0, _picks.Length - 1)];
        return (seat.Airframe, seat.Fit, seat.Build);
    }

    /// <summary>Picks a further seat's stock airframe and fit, as <see cref="Pick"/> does seat 0's;
    /// <see cref="PickCustom"/> picks its custom plane. A changed pick clears this machine's Ready,
    /// which stands for every seat. Refused for seat 0, past the fourth seat and outside the eleven.</summary>
    public bool PickSeat(int local, int airframe, CoopFit fit)
    {
        if (local < 1 || local >= _picks.Length || airframe is < 0 or >= AirframeCount)
        {
            return false;
        }

        Take(local, (byte)airframe, null, fit);
        return true;
    }

    /// <summary>Marks this pilot Ready or not under the current round. A guest's mark reaches the host
    /// on the next step. False, and no mark, when the rules refuse this pilot's plane or a picked
    /// ammunition or rocket; <see cref="ReadyRefusals"/> then names each reason.</summary>
    public bool SetReady(bool ready)
    {
        _refusals.Clear();
        if (ready && ReadyCheck())
        {
            _ready = false;
            return false;
        }

        _ready = ready;
        _readyEpoch = Options.Epoch;
        _readySeats = SeatsFlown;
        return true;
    }

    /// <summary>A lobby screen now stands on this lobby, so a guest's pick and name go to the host.
    /// A host's first showing ticks Allow Custom Planes, as the original's lobby opens
    /// (<c>docs/org/multiplayer-messages.md</c>, "Custom planes"). An unshown host stays clear.</summary>
    public void Show()
    {
        if (!Shown)
        {
            ChangeRules(_rules with { AllowCustom = true });
        }

        Shown = true;
    }

    /// <summary>The launch leaves: the player list as it stands is kept, so the match's seats can be
    /// named on the Game Scores page afterwards.</summary>
    public void Launched()
    {
        var players = Players;
        _launchNames = new string[players.Count];
        for (int i = 0; i < players.Count; i++)
        {
            _launchNames[i] = players[i].Name;
        }
    }

    /// <summary>Back from a match: its scores stand on the Game Scores page and this pilot's Ready
    /// clears. The host opens a new round, which clears every guest's mark too. A stunt race lands its
    /// table in <paramref name="race"/> instead.</summary>
    public void Land(IReadOnlyList<DogfightScore> scores, IReadOnlyList<RaceTableRow>? race = null)
    {
        ArgumentNullException.ThrowIfNull(scores);
        _scores = new List<DogfightScore>(scores).ToArray();
        _raceScores = race != null ? new List<RaceTableRow>(race).ToArray() : Array.Empty<RaceTableRow>();
        _ready = false;
        if (IsHost)
        {
            _options = _options with { Epoch = NextEpoch() };
        }
    }

    /// <summary>Sends one chat line under this pilot's name and shows it here at once. Refused when
    /// empty or longer than the edit box holds.</summary>
    public bool Say(string text)
    {
        string line = (text ?? "").Trim();
        if (line.Length == 0 || line.Length > LobbyChatMessage.MaxChars)
        {
            return false;
        }

        var message = new LobbyChatMessage(OwnName(), line);
        Add(message);
        if (IsHost)
        {
            foreach (int peer in SeatedPeers())
            {
                _wire.Tell(peer, message);
            }
        }
        else if (_hostPeer >= 0)
        {
            _wire.Tell(_hostPeer, message);
        }

        return true;
    }

    /// <summary>Posts a notice under no name in this host's chat and every seated guest's, as the
    /// original posts its lobby notices (langui 10499 to 10506). Refused on a guest.</summary>
    public bool Announce(string text)
    {
        if (!IsHost || string.IsNullOrEmpty(text))
        {
            return false;
        }

        var notice = new LobbyChatMessage("", text);
        Add(notice);
        foreach (int peer in SeatedPeers())
        {
            _wire.Tell(peer, notice);
        }

        return true;
    }

    /// <summary>The peer a host's player list row names, any of its machine's seats. It is -1 for
    /// the host's own rows, for a bot's or a row past the list, and on a guest.</summary>
    public int PeerAt(int row)
    {
        var owners = SeatOwners;
        return row >= 0 && row < owners.Count && owners[row].Peer != _wire.LocalPeer ? owners[row].Peer : -1;
    }

    /// <summary>One menu frame, after the socket was stepped. The host relays each guest's chat to the
    /// others. It lets the newest bots go while the field holds more than sixteen pilots, posting
    /// <see cref="YieldLine"/> for each. It sends each guest its options and list when they change.
    /// A guest takes the host's chat and sends its own pick.</summary>
    public void Step()
    {
        // A seat given or taken since the mark clears it for good, so the seats come back unready.
        if (_readySeats != SeatsFlown)
        {
            _ready = false;
        }

        foreach (var (from, line) in _wire.TakeChat())
        {
            Add(line);
            if (!IsHost)
            {
                continue;
            }

            foreach (int peer in SeatedPeers())
            {
                if (peer != from)
                {
                    _wire.Tell(peer, line);
                }
            }
        }

        if (IsHost)
        {
            TakeTeamActions();
            YieldToPeople();
            Arrange();
            SendToGuests();
        }
        else
        {
            SendPick();
        }
    }

    private static DogfightScore[] ScoresOf(IEnumerable<CSVM.Flight.Modes.VersusStanding> standings, Func<int, (string Name, bool IsBot)> pilot)
    {
        ArgumentNullException.ThrowIfNull(standings);
        var lines = new List<(int Rank, DogfightScore Line)>();
        foreach (var standing in standings)
        {
            var (name, bot) = pilot(standing.PlayerIndex);
            lines.Add((standing.Rank, new DogfightScore(name, standing.Score, standing.Kills, standing.Deaths, IsBot: bot)));
        }

        lines.Sort((a, b) => a.Rank.CompareTo(b.Rank));
        return lines.ConvertAll(line => line.Line).ToArray();
    }

    private static DogfightScore[] ScoresOf(CSVM.Flight.Modes.VersusMatch match, Func<int, (string Name, bool IsBot)> pilot)
    {
        ArgumentNullException.ThrowIfNull(match);
        if (!match.Teamed)
        {
            return ScoresOf(match.Standings(), pilot);
        }

        var lines = new List<DogfightScore>();
        foreach (var team in match.TeamStandings())
        {
            lines.Add(new DogfightScore(team.Name, team.Score, team.Kills, team.Deaths, IsTeam: true));
            lines.AddRange(ScoresOf(Members(match, team.Team), pilot));
        }

        // A seat on no team, which a team launch refuses, still gets its line.
        lines.AddRange(ScoresOf(Members(match, 0), pilot));
        return lines.ToArray();
    }

    // A seat's name in a list by seat, or its player tag past the list.
    private static string NameIn(IReadOnlyList<string> names, int seat) =>
        seat >= 0 && seat < names.Count ? names[seat] : "P" + (seat + 1).ToString(CultureInfo.InvariantCulture);

    private static List<CSVM.Flight.Modes.VersusStanding> Members(CSVM.Flight.Modes.VersusMatch match, int team)
    {
        var members = new List<CSVM.Flight.Modes.VersusStanding>();
        foreach (var standing in match.Standings())
        {
            if (match.TeamOf(standing.PlayerIndex) == team)
            {
                members.Add(standing);
            }
        }

        return members;
    }

    private static bool Contains(IReadOnlyList<int> peers, int peer)
    {
        for (int i = 0; i < peers.Count; i++)
        {
            if (peers[i] == peer)
            {
                return true;
            }
        }

        return false;
    }

    // The airframe a guest seat's pick names, or the mark of a pick the host has not heard.
    private static byte PickedAirframe(CoopPickMessage? pick) =>
        pick is not { } heard ? DogfightLobbySeat.NoPick : heard.Airframe < AirframeCount ? heard.Airframe : DefaultAirframe;

    // Every row's team, one player a row: a bot, or one seat of a machine.
    private List<(byte Team, int Seats)> Weighed()
    {
        var players = Players;
        var weighed = new List<(byte Team, int Seats)>(players.Count);
        foreach (var player in players)
        {
            weighed.Add((player.Team, 1));
        }

        return weighed;
    }

    // ⚠ Only the bots the host has not placed move here. A team match wants two sides. Where one team
    // alone holds a person or a placed bot, the bots take a team of their own. Each then joins
    // the side with the fewest pilots, the first created of a tie. With nobody on a team they fly
    // the free-for-all, and a bots' team left empty is pruned. The host's placements are its odds.
    private bool Arrange()
    {
        if (!IsHost)
        {
            return true;
        }

        var sizes = new Dictionary<byte, int>();
        foreach (var player in Players)
        {
            if (!player.IsBot && player.Team != 0)
            {
                sizes[player.Team] = sizes.GetValueOrDefault(player.Team) + 1;
            }
        }

        foreach (var bot in _bots.Rows)
        {
            if (bot.Placed)
            {
                sizes[bot.Team] = sizes.GetValueOrDefault(bot.Team) + 1;
            }
        }

        var free = _bots.Rows.Where(bot => !bot.Placed).Select(bot => bot.Id).ToList();
        var sides = _teams.Teams.Select(team => team.Number).Where(sizes.ContainsKey).ToList();
        if (IsStuntRace(_options))
        {
            sides.Clear();
        }
        else if (sides.Count == 1 && free.Count > 0
            && (_teams.Teams.FirstOrDefault(team => !sizes.ContainsKey(team.Number))?.Number ?? _teams.Open(BotTeamName)) is var own and > 0)
        {
            sides.Add(own);
            sizes[own] = 0;
        }

        foreach (int id in free)
        {
            byte side = sides.Count == 0 ? (byte)0 : sides.MinBy(team => sizes[team]);
            _bots.SetTeam(id, side, placed: false);
            if (side != 0)
            {
                sizes[side]++;
            }
        }

        Post(_teams.Prune());
        return true;
    }

    // The people's names as the list writes them, the host's and every seated guest's.
    private List<string> PeopleNames()
    {
        var names = new List<string>();
        foreach (var row in HostRows())
        {
            if (!row.IsBot)
            {
                names.Add(row.Name);
            }
        }

        return names;
    }

    // ⚠ Do not call this outside Step. A host's door steps its lobby only while the lobby stands.
    // A player who joins mid-match then waits, and no seat changes hands in flight or on a Restart.
    // One bot goes per seat past the cap, newest first, so waiting people take the newest bots'
    // places in join order. A leaver's seat is not refilled. A bot whose callsign a seated person
    // holds draws a new one, since the death lines and Game Scores name seats by callsign.
    private void YieldToPeople()
    {
        while (_bots.Count > 0 && FieldSeats > NetSeats.MaxPlayers)
        {
            string callsign = _bots.Rows[^1].Callsign;
            _bots.DropNewest();
            Announce(YieldLine(callsign));
        }

        _bots.YieldNames(PeopleNames());
    }

    private List<int> SeatedPeers()
    {
        var peers = new List<int>();
        foreach (int peer in _wire.AllPeers)
        {
            if (Seated(peer))
            {
                peers.Add(peer);
            }
        }

        return peers;
    }

    // A guest's seat counts as Ready only under this round and on a plane the rules admit. The
    // guest's own lobby asks the same, but the host does not take its word for it. Each seat's
    // custom plane is the one that seat's build named.
    private bool Admits(int peer, CoopPickMessage pick, int local = 0) =>
        pick.Ready && pick.Epoch == _options.Epoch
        && _rules.Refuses(pick.Airframe, _wire.BuildAt(peer, local)) == PlaneRefusal.None;

    // The original's Ready check over every seat this machine flies, true when it refuses. An
    // outlawed ammunition or rocket also resets every gun's or pylon's to none, so the next Ready
    // takes; Outlaw All resets without refusing.
    private bool ReadyCheck()
    {
        var rules = Rules;
        for (int local = 0; local < SeatsFlown; local++)
        {
            var seat = _picks[local];
            Refuse(rules.Refuses(seat.Airframe, seat.Build));
            Refuse(rules.AmmoOutlawed(seat.Fit) && !rules.Has(NetPlaneRules.AllAmmoFlag) ? PlaneRefusal.Ammo : PlaneRefusal.None);
            Refuse(rules.RocketsOutlawed(seat.Fit) && !rules.Has(NetPlaneRules.AllRocketsFlag) ? PlaneRefusal.Rockets : PlaneRefusal.None);
            seat.Fit = rules.Enforce(seat.Fit);
        }

        return _refusals.Count > 0;
    }

    // One reason a Ready check refuses, named once however many seats share it.
    private void Refuse(PlaneRefusal why)
    {
        if (why != PlaneRefusal.None && !_refusals.Contains(why))
        {
            _refusals.Add(why);
        }
    }

    // ⚠ Readiness is consent to the plane as it stood, so the host must not launch a changed one.
    private void Take(int local, byte airframe, NetPlaneBuild? build, CoopFit fit)
    {
        var seat = _picks[local];
        if (airframe != seat.Airframe || fit != seat.Fit || !Equals(build, seat.Build))
        {
            _ready = false;
        }

        seat.Airframe = airframe;
        seat.Build = build;
        seat.Fit = fit;
    }

    // A rules change is a new round, as an options change is.
    private bool ChangeRules(NetPlaneRules changed)
    {
        if (!IsHost)
        {
            return false;
        }

        if (changed != _rules)
        {
            _rules = changed;
            _options = _options with { Epoch = NextEpoch() };
            _ready = false;
            // A bot has no Ready for the rules to refuse, so its pick is reset here instead.
            foreach (var bot in new List<DogfightBot>(_bots.Rows))
            {
                if (!bot.RandomPlane && !changed.AdmitsStock(bot.Airframe))
                {
                    _bots.SetAirframe(bot.Id, DogfightLobbySeat.RandomAirframe);
                }
            }
        }

        return true;
    }

    // Every host change is a new round: the Ready marks under the old options clear.
    private bool Change(DogfightOptionsMessage changed)
    {
        if (!IsHost)
        {
            return false;
        }

        if (changed == _options)
        {
            return true;
        }

        _options = changed with { Epoch = NextEpoch() };
        _ready = false;
        return true;
    }

    // The team boxes take a press only where the type neither fixes them nor flies without teams.
    private bool TeamsEditable() => !FixesTeams(_options) && !IsStuntRace(_options);

    // Round 0 is a guest's "heard nothing", so the count wraps past it.
    private byte NextEpoch() => (byte)(_options.Epoch == byte.MaxValue ? 1 : _options.Epoch + 1);

    private string OwnName() => NetSeats.ShownName(_name(), IsHost);

    private void Add(LobbyChatMessage line)
    {
        if (_chat.Count >= ChatDepth)
        {
            _chat.RemoveAt(0);
        }

        _chat.Add(new DogfightChatLine(line.Name, line.Text));
    }

    private List<DogfightLobbySeat> HostRows()
    {
        int self = _wire.LocalPeer;
        var rows = new List<DogfightLobbySeat>();
        foreach (var (peer, local) in SeatOwners)
        {
            // ⚠ Do not map a row to its seat by name; only SeatOwners order does.
            bool own = peer == self;
            var pick = own ? null : _wire.PickAt(peer, local);
            string name = local > 0 ? NetSeats.FurtherSeatName(own ? _name() : _wire.PickAt(peer, 0)?.Name, host: own, local: local)
                : own ? OwnName()
                : pick is { Name.Length: > 0 } named ? named.Name
                : "Pilot " + (rows.Count + 1).ToString(CultureInfo.InvariantCulture);
            string plane = (own ? _picks[local].Build : _wire.BuildAt(peer, local))?.Name ?? "";
            rows.Add(new DogfightLobbySeat(name, own ? _picks[local].Airframe : PickedAirframe(pick),
                own ? Ready : pick is { } heard && Admits(peer, heard, local), own && local == 0, _teams.TeamOf(peer),
                local == 0 && _teams.IsCaptain(peer), Plane: plane));
        }

        // A bot is always Ready: it has no plane of its own to consent to.
        foreach (var bot in _bots.Rows)
        {
            rows.Add(new DogfightLobbySeat(bot.Callsign, bot.Airframe, true, false, bot.Team, false, NetPilot.Bot, bot.Skill));
        }

        return rows;
    }

    // One team action: a guest asks its host, the host applies its own at once.
    private bool Act(LobbyTeamActionMessage action)
    {
        if (IsHost)
        {
            return Apply(_wire.LocalPeer, action);
        }

        if (_hostPeer < 0)
        {
            return false;
        }

        _wire.Tell(_hostPeer, action);
        return true;
    }

    // ⚠ The host alone decides, on its own book: a guest's word names only what it asks for. A
    // guest's Disband is ignored, since only a captain's Leave disbands.
    private bool Apply(int member, LobbyTeamActionMessage action)
    {
        var events = action.Action switch
        {
            NetTeamAction.Create when _teams.TeamOf(member) == 0 && NetPlayerInfo.IsValidName(action.Name) => _teams.Create(member, action.Name),
            NetTeamAction.Join => _teams.Join(member, action.Team),
            NetTeamAction.Leave => _teams.Leave(member),
            _ => Array.Empty<NetTeamEvent>(),
        };

        Post(events);
        Arrange();
        return events.Count > 0;
    }

    // Each guest's asked actions, then every member no longer seated taken off its team: a leaver's
    // or a booted captain's team is disbanded.
    private void TakeTeamActions()
    {
        var seated = SeatedPeers();
        foreach (var (peer, action) in _wire.TakeTeamActions())
        {
            if (Contains(seated, peer))
            {
                Apply(peer, action);
            }
        }

        var present = new List<int>(seated) { _wire.LocalPeer };
        Post(_teams.Keep(present));
    }

    // ⚠ A disbanded team's bots go teamless here. A bot is no member of the book. A later team takes
    // the lowest free number, so a bot still holding the old number would join it unasked.
    private void Post(IReadOnlyList<NetTeamEvent> events)
    {
        foreach (var happened in events)
        {
            Announce(TeamLine(happened.Kind, MemberName(happened.Member), happened.TeamName));
            if (happened.Kind != NetTeamEventKind.Disbanded)
            {
                continue;
            }

            _bots.ClearTeam(happened.Team);
        }
    }

    // A member's name as the player list writes it, the host's own included.
    private string MemberName(int member)
    {
        if (member == _wire.LocalPeer)
        {
            return OwnName();
        }

        return _wire.Picks.TryGetValue(member, out var pick) && pick.Name is { Length: > 0 } named ? named : "A pilot";
    }

    // A guest that has not heard the host's list yet shows its own seats alone.
    private DogfightLobbySeat[] GuestAlone()
    {
        var rows = new DogfightLobbySeat[SeatsFlown];
        rows[0] = new DogfightLobbySeat(OwnName(), Airframe, Ready, false, Plane: Build?.Name ?? "");
        for (int local = 1; local < rows.Length; local++)
        {
            rows[local] = new DogfightLobbySeat(NetSeats.FurtherSeatName(_name(), host: IsHost, local: local), _picks[local].Airframe, Ready, false,
                Plane: _picks[local].Build?.Name ?? "");
        }

        return rows;
    }

    private void SendToGuests()
    {
        var peers = SeatedPeers();
        var rows = HostRows();
        var owners = SeatOwners;
        var teams = new LobbyTeamsMessage(Teams);
        for (int i = 0; i < peers.Count; i++)
        {
            int peer = peers[i];

            // The rules go first, so a guest reading a new round reads it under the rules it has.
            var rules = new LobbyPlaneRulesMessage(_options.Epoch, _rules);
            if (!_rulesSent.TryGetValue(peer, out var told) || told != rules)
            {
                _wire.Tell(peer, rules);
                _rulesSent[peer] = rules;
            }

            if (!_optionsSent.TryGetValue(peer, out var sent) || sent != Options)
            {
                _wire.Tell(peer, Options);
                _optionsSent[peer] = Options;
            }

            // The team list goes before the rows that name its teams.
            if (!_teamsSent.TryGetValue(peer, out var teamed) || teamed != teams)
            {
                _wire.Tell(peer, teams);
                _teamsSent[peer] = teams;
            }

            int first = owners.TakeWhile(owner => owner.Peer != peer).Count();
            int given = owners.Count(owner => owner.Peer == peer);
            var roster = new DogfightRosterMessage(_options.Epoch, (byte)first, rows, (byte)given);
            if (!_rosterSent.TryGetValue(peer, out var listed) || listed != roster)
            {
                _wire.Tell(peer, roster);
                _rosterSent[peer] = roster;
            }
        }

        foreach (int gone in new List<int>(_optionsSent.Keys))
        {
            if (!Contains(peers, gone))
            {
                _optionsSent.Remove(gone);
                _rosterSent.Remove(gone);
                _rulesSent.Remove(gone);
                _teamsSent.Remove(gone);
            }
        }
    }

    // A Ready mark stands under the round it was given in, so a new round from the host clears it.
    // The pick goes out again under the new round.
    private void SendPick()
    {
        if (_hostPeer < 0 || !Shown || _wire.DogfightOptions is not { } options)
        {
            return;
        }

        // One pick per seat this machine asks for, each marking whether another follows. Only the
        // first has a Player Information answer. ⚠ Mark Ready only a seat that flew when the mark
        // was given; a seat given since has picked nothing.
        int seats = Math.Clamp(LocalSeats(), 1, _picks.Length);
        for (int local = 0; local < seats; local++)
        {
            // Each seat's build goes before its pick, so the host never reads a Ready without the
            // plane behind it.
            if (_picks[local].FollowBuild(local) is { } built)
            {
                _wire.Tell(_hostPeer, built);
            }

            bool first = local == 0;
            if (_picks[local].Follow(options.Epoch, Ready && local < SeatsFlown, first ? OwnName() : "",
                    first ? Voice() : CoopPickMessage.NoVoice, local, local + 1 < seats) is { } pick)
            {
                _wire.Tell(_hostPeer, pick);
            }
        }
    }

    // One seat's pick at this machine: the stock airframe, the custom plane on it and the fit. It
    // keeps what last went to the host.
    private sealed class SeatPicked
    {
        private CoopPickMessage? _sent;
        private NetPlaneBuild? _buildSent;

        public byte Airframe { get; set; } = DefaultAirframe;

        public NetPlaneBuild? Build { get; set; }

        public CoopFit Fit { get; set; }

        // One per seat a machine may fly, the pick message's four places.
        public static SeatPicked[] All()
        {
            var picks = new SeatPicked[CoopPickMessage.MaxLocal + 1];
            for (int i = 0; i < picks.Length; i++)
            {
                picks[i] = new SeatPicked();
            }

            return picks;
        }

        // This seat's pick under the host's round, or null when the host already has it.
        public CoopPickMessage? Follow(byte round, bool ready, string name, byte voice, int local, bool more)
        {
            var pick = new CoopPickMessage(round, ready, Airframe, Fit, name, Voice: voice, Local: (byte)local, More: more);
            if (_sent == pick)
            {
                return null;
            }

            _sent = pick;
            return pick;
        }

        // This seat's build as the host should hold it, or null when it already does. ⚠ Name the
        // seat's own place; under one byte for all, a second seat's build overwrites the first's.
        public PlaneBuildMessage? FollowBuild(int local)
        {
            if (Equals(Build, _buildSent))
            {
                return null;
            }

            _buildSent = Build?.Copy();
            return new PlaneBuildMessage(PlaneBuildMessage.MineAt(local), Build);
        }
    }
}
