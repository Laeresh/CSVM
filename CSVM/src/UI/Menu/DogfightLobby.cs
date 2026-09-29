using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Net;

namespace CSVM.UI.Menu;

/// <summary>The three mission types the lobby's Type box lists, in the string table's order.
/// Only the Deathmatch is flown; it is the Dogfight.</summary>
public enum DogfightMissionType : byte
{
    /// <summary>Capture the Flag, drawn greyed.</summary>
    CaptureTheFlag = 0,

    /// <summary>Deathmatch, the Dogfight every lobby flies.</summary>
    Deathmatch = 1,

    /// <summary>Zeppelin vs Zeppelin, drawn greyed.</summary>
    ZeppelinVsZeppelin = 2,
}

/// <summary>One line of the lobby's chat panel: who typed it and what.</summary>
public readonly record struct DogfightChatLine(string Name, string Text);

/// <summary>One line on the Game Scores page after a match: a pilot's name, or a team's when
/// <paramref name="IsTeam"/>, and its final points, kills and deaths.</summary>
public readonly record struct DogfightScore(string Name, int Points, int Kills, int Deaths, bool IsTeam = false);

/// <summary>
/// The Multiplayer Lobby of a Dogfight, on both of its ends, engine-free. The host owns the Mission
/// Options, the plane rules and the player list, and each guest reads them off the wire. Every pilot
/// picks a stock airframe or one of its custom planes, and a fit, and marks itself Ready on a plane
/// the rules admit. The host launches once every pilot is Ready. A guest's pick travels as a
/// <see cref="CoopPickMessage"/> under the round the host's options name, so an option change clears
/// every Ready. A match flown from here lands back on it with its scores.
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

    /// <summary>How many chat lines the panel keeps; older lines scroll away.</summary>
    public const int ChatDepth = 64;

    /// <summary>The airframe a pilot flies until it picks one: the Devastator, the original's first
    /// row.</summary>
    public const byte DefaultAirframe = CoopGuestPick.StarterAirframe;

    /// <summary>How many stock airframes the Select Plane list offers.</summary>
    public const int AirframeCount = 11;

    /// <summary>Why a Built-in host's launch waits: a lobby guest is not Ready yet.</summary>
    public const string GuestsNotReady = "waiting for every lobby pilot to be Ready";

    /// <summary>Why a Built-in host's launch waits: a lobby guest flies only the seven environments.
    /// </summary>
    public const string MapUnlisted = "a lobby pilot flies only the seven lobby environments";

    // The Environment box's words in the string table's order, and the chapter each is flown on.
    private static readonly string[] EnvironmentNames =
    {
        "Above the Clouds", "Hawai'ian Islands", "Hollywood Studio", "Manhattan",
        "NW Boeing Field", "NW Lighthouse", "Sky Haven (Rockies)",
    };

    private static readonly string[] EnvironmentChapters = { "C2B", "C3", "C2", "C5", "C1", "C1B", "C4" };

    private readonly NetLobby _wire;
    private readonly Func<string> _name;
    private readonly int _hostPeer;
    private readonly List<DogfightChatLine> _chat = new();
    private readonly Dictionary<int, DogfightOptionsMessage> _optionsSent = new();
    private readonly Dictionary<int, DogfightRosterMessage> _rosterSent = new();
    private readonly Dictionary<int, LobbyPlaneRulesMessage> _rulesSent = new();
    private readonly Dictionary<int, LobbyTeamsMessage> _teamsSent = new();
    private readonly List<PlaneRefusal> _refusals = new();
    private readonly NetTeamBook _teams = new();
    private DogfightOptionsMessage _options = new(
        1, 0, (byte)DogfightMissionType.Deathmatch, DogfightVictory.Time, DefaultTimeMinutes, DefaultScore,
        false, DefaultLives, true);

    // The original's settings block starts zeroed, so a new lobby allows no custom plane.
    private NetPlaneRules _rules;
    private byte _airframe = DefaultAirframe;
    private NetPlaneBuild? _build;
    private NetPlaneBuild? _buildSent;
    private CoopFit _fit;
    private bool _ready;
    private byte _readyEpoch;
    private CoopPickMessage? _pickSent;
    private string[] _launchNames = Array.Empty<string>();
    private DogfightScore[] _scores = Array.Empty<DogfightScore>();

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
    }

    /// <summary>Whether a host lists and tells a peer on the wire. Every peer is, unless the door
    /// refused it, as it refuses a guest past its chosen cap.</summary>
    public Func<int, bool> Seated { get; init; } = _ => true;

    /// <summary>The voice byte this pilot's pick carries (<see cref="CoopPickMessage.Voice"/>).
    /// </summary>
    public Func<byte> Voice { get; init; } = () => CoopPickMessage.NoVoice;

    /// <summary>How many seats the host's own machine flies, its splitscreen seats included. They
    /// all fly on the host's team, and the balance check counts each as a player. A guest flies one.
    /// </summary>
    public Func<int> LocalSeats { get; init; } = () => 1;

    /// <summary>Whether this end owns the options and launches the match.</summary>
    public bool IsHost => _hostPeer < 0;

    /// <summary>Whether a guest has heard the host's options yet. Always true on the host.</summary>
    public bool HasOptions => IsHost || _wire.DogfightOptions.HasValue;

    /// <summary>The Mission Options as this end stands on them: its own on the host, the host's word
    /// on a guest. A guest that has heard nothing reads the opening defaults.</summary>
    public DogfightOptionsMessage Options => IsHost ? _options : _wire.DogfightOptions ?? _options with { Epoch = 0 };

    /// <summary>The stock airframe this pilot picked, an index into the eleven.</summary>
    public byte Airframe => _airframe;

    /// <summary>The ammunition and ordnance this pilot picked, stock when all zero.</summary>
    public CoopFit Fit => _fit;

    /// <summary>The fit this pilot flies: its pick as <see cref="NetPlaneRules.Enforce"/> leaves it.
    /// The host reads a guest's fit through the same rules, so both ends agree.</summary>
    public CoopFit LaunchFit => Rules.Enforce(_fit);

    /// <summary>The custom plane this pilot picked, or null on a stock airframe.</summary>
    public NetPlaneBuild? Build => _build;

    /// <summary>The plane rules as this end stands on them: its own on the host, the host's word on
    /// a guest. A guest that has heard nothing reads a new lobby's, which allow no custom plane.
    /// </summary>
    public NetPlaneRules Rules => IsHost ? _rules : _wire.PlaneRules?.Rules ?? default;

    /// <summary>Why the rules refuse this pilot's plane, or none.</summary>
    public PlaneRefusal Refusal => Rules.Refuses(_airframe, _build);

    /// <summary>Whether this pilot marked itself Ready under the current round, on a plane the rules
    /// admit.</summary>
    public bool Ready => _ready && _readyEpoch == Options.Epoch && Refusal == PlaneRefusal.None;

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

    /// <summary>Why the host's launch is refused on its teams, or none. Each row counts one player,
    /// the host's row every seat its machine flies (<see cref="LocalSeats"/>).</summary>
    public TeamLaunchRefusal LaunchRefusal
    {
        get
        {
            var players = Players;
            var weighed = new List<(byte Team, int Seats)>(players.Count);
            for (int i = 0; i < players.Count; i++)
            {
                weighed.Add((players[i].Team, players[i].IsHost ? Math.Max(1, LocalSeats()) : 1));
            }

            var options = Options;
            return NetTeamBook.Check(weighed, options.RestrictTeams, options.MinTeams, options.MaxTeams);
        }
    }

    /// <summary>Whether a lobby screen stands on this lobby. A Built-in Network board never shows
    /// one, so its guest sends no pick and its flight ends the way it always has.</summary>
    public bool Shown { get; private set; }

    /// <summary>The last match's lines for the Game Scores page, best first, or empty before any
    /// match has been flown from this lobby.</summary>
    public IReadOnlyList<DogfightScore> Scores => _scores;

    /// <summary>The players named at the last launch, in seat order.</summary>
    public IReadOnlyList<string> LaunchNames => _launchNames;

    /// <summary>The environment's words as the Environment box and the games list write them, or ""
    /// outside the seven.</summary>
    public static string EnvironmentName(int environment) =>
        environment is >= 0 and < EnvironmentCount ? EnvironmentNames[environment] : "";

    /// <summary>The chapter an environment is flown on.</summary>
    public static string ChapterOf(int environment) => EnvironmentChapters[Math.Clamp(environment, 0, EnvironmentCount - 1)];

    /// <summary>The environment flown on <paramref name="chapter"/>, or -1 for a chapter the
    /// Environment box does not list.</summary>
    public static int EnvironmentOf(string chapter) => Array.IndexOf(EnvironmentChapters, chapter);

    /// <summary>The Game Scores lines for a match's standings, best first. Each seat is named from
    /// <paramref name="names"/>, the list as it stood at the launch, or by its player tag past it.
    /// </summary>
    public static DogfightScore[] ScoresOf(IEnumerable<CSVM.Flight.Modes.VersusStanding> standings, IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(standings);
        ArgumentNullException.ThrowIfNull(names);
        var lines = new List<(int Rank, DogfightScore Line)>();
        foreach (var standing in standings)
        {
            int seat = standing.PlayerIndex;
            string name = seat >= 0 && seat < names.Count ? names[seat] : "P" + (seat + 1).ToString(CultureInfo.InvariantCulture);
            lines.Add((standing.Rank, new DogfightScore(name, standing.Score, standing.Kills, standing.Deaths)));
        }

        lines.Sort((a, b) => a.Rank.CompareTo(b.Rank));
        return lines.ConvertAll(line => line.Line).ToArray();
    }

    /// <summary>The Game Scores lines for a finished match. A team match lists each team's totals,
    /// best first, with its pilots under it best first. A free-for-all is
    /// <see cref="ScoresOf(IEnumerable{CSVM.Flight.Modes.VersusStanding}, IReadOnlyList{string})"/>.
    /// </summary>
    public static DogfightScore[] ScoresOf(CSVM.Flight.Modes.VersusMatch match, IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(names);
        if (!match.Teamed)
        {
            return ScoresOf(match.Standings(), names);
        }

        var lines = new List<DogfightScore>();
        foreach (var team in match.TeamStandings())
        {
            lines.Add(new DogfightScore(team.Name, team.Score, team.Kills, team.Deaths, IsTeam: true));
            lines.AddRange(ScoresOf(Members(match, team.Team), names));
        }

        // A seat on no team, which a team launch refuses, still gets its line.
        lines.AddRange(ScoresOf(Members(match, 0), names));
        return lines.ToArray();
    }

    /// <summary>Whether the Type box offers a mission type as live. Capture the Flag and Zeppelin vs
    /// Zeppelin are drawn greyed.</summary>
    public static bool Flies(DogfightMissionType type) => type == DogfightMissionType.Deathmatch;

    /// <summary>The match rules a launch carries: the victory condition as a kill target, a match
    /// clock or both, and the lives rule. A limit not chosen is off. With both, the first one reached
    /// ends the match.</summary>
    public static VersusRules RulesOf(DogfightOptionsMessage options) => new(
        options.Victory != DogfightVictory.Time ? options.Score : 0,
        options.Victory != DogfightVictory.Score ? options.TimeMinutes : 0,
        options.LimitedLives ? ClampLives(options.Lives) : 0,
        options.AutoRespawn);

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

    /// <summary>Picks the environment. Refused on a guest and outside the seven.</summary>
    public bool SetEnvironment(int environment) =>
        environment is >= 0 and < EnvironmentCount && Change(_options with { Environment = (byte)environment });

    /// <summary>Picks the mission type. Refused on a guest and for a greyed type.</summary>
    public bool SetMissionType(DogfightMissionType type) => Flies(type) && Change(_options with { MissionType = (byte)type });

    /// <summary>Picks how the match is won: Time, Score or both. Refused on a guest.</summary>
    public bool SetVictory(DogfightVictory victory) =>
        victory is DogfightVictory.Time or DogfightVictory.Score or DogfightVictory.Both && Change(_options with { Victory = victory });

    /// <summary>Checks or clears Restrict Number of Teams. Refused on a guest.</summary>
    public bool SetRestrictTeams(bool restrict) => Change(_options with { RestrictTeams = restrict });

    /// <summary>Sets the minimum team count, held to 0 up to the maximum as the script's box binds
    /// it. Refused on a guest and while Restrict Number of Teams is clear.</summary>
    public bool SetMinTeams(int teams) =>
        _options.RestrictTeams && Change(_options with { MinTeams = (byte)Math.Clamp(teams, 0, _options.MaxTeams) });

    /// <summary>Sets the maximum team count, held to the minimum up to <see cref="MaxTeams"/>.
    /// Refused on a guest and while Restrict Number of Teams is clear.</summary>
    public bool SetMaxTeams(int teams) =>
        _options.RestrictTeams && Change(_options with { MaxTeams = (byte)Math.Clamp(teams, _options.MinTeams, MaxTeams) });

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

    /// <summary>Sets the Time box, in minutes. Refused on a guest and outside 1 to 99.</summary>
    public bool SetTimeMinutes(int minutes) =>
        minutes is >= 1 and <= MaxTimeMinutes && Change(_options with { TimeMinutes = (byte)minutes });

    /// <summary>Sets the Score box. Refused on a guest and outside 1 to 999.</summary>
    public bool SetScore(int score) => score is >= 1 and <= MaxScore && Change(_options with { Score = (ushort)score });

    /// <summary>Checks or clears Limited Lives. Refused on a guest.</summary>
    public bool SetLimitedLives(bool limited) => Change(_options with { LimitedLives = limited });

    /// <summary>Sets the Lives box, clamped to 1..99. Refused on a guest and while Limited Lives is
    /// clear, which is when the box is greyed.</summary>
    public bool SetLives(int lives) =>
        _options.LimitedLives && Change(_options with { Lives = (byte)ClampLives(lives) });

    /// <summary>Checks or clears Auto Respawn. Refused on a guest.</summary>
    public bool SetAutoRespawn(bool auto) => Change(_options with { AutoRespawn = auto });

    /// <summary>Checks or clears Allow Custom Planes. Refused on a guest.</summary>
    public bool SetAllowCustomPlanes(bool allow) => ChangeRules(_rules with { AllowCustom = allow });

    /// <summary>Checks or clears Outlaw Components, which puts the outlaw list in force. A toggle
    /// either way empties the list in the same round, as the original's 5044 does. Refused on a
    /// guest.</summary>
    public bool SetOutlawComponents(bool outlaw) =>
        ChangeRules(outlaw == _rules.Outlawing ? _rules : _rules with { Outlawing = outlaw, Outlawed = 0 });

    /// <summary>Sets or clears one flag of the outlaw list (<see cref="NetPlaneRules"/> names them).
    /// Refused on a guest and outside the list.</summary>
    public bool SetOutlawed(int flag, bool outlawed) =>
        flag is >= 0 and < NetPlaneRules.Flags && ChangeRules(_rules.With(flag, outlawed));

    /// <summary>Picks this pilot's stock airframe and fit, live whether or not it is Ready. A changed
    /// pick clears this pilot's own Ready. Refused outside the eleven stock airframes.</summary>
    public bool Pick(int airframe, CoopFit fit)
    {
        if (airframe is < 0 or >= AirframeCount)
        {
            return false;
        }

        Take((byte)airframe, null, fit);
        return true;
    }

    /// <summary>Picks one of this pilot's custom planes and its fit. Refused on an airframe outside
    /// the eleven. Whether the rules admit it is asked at Ready, as the original asks.</summary>
    public bool PickCustom(NetPlaneBuild build, CoopFit fit)
    {
        ArgumentNullException.ThrowIfNull(build);
        if (build.Airframe >= AirframeCount)
        {
            return false;
        }

        Take(build.Airframe, build.Copy(), fit);
        return true;
    }

    /// <summary>Changes this pilot's fit alone, keeping the plane it stands on.</summary>
    public void Refit(CoopFit fit) => Take(_airframe, _build, fit);

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
        return true;
    }

    /// <summary>A lobby screen now stands on this lobby, so a guest's pick and name go to the host.
    /// </summary>
    public void Show() => Shown = true;

    /// <summary>A Built-in host's launch against the guests that sent a pick, which only a lobby
    /// screen sends. It waits for their Ready and writes the map and rules into the options. Null
    /// when the launch may go, otherwise why not.</summary>
    public string? CheckBuiltInLaunch(string chapter, VersusRules rules)
    {
        bool picked = false;
        foreach (int peer in SeatedPeers())
        {
            if (!_wire.Picks.TryGetValue(peer, out var pick))
            {
                continue;
            }

            picked = true;
            if (!Admits(peer, pick))
            {
                return GuestsNotReady;
            }
        }

        return AdoptLaunch(chapter, rules) || !picked ? null : MapUnlisted;
    }

    /// <summary>A Built-in host's launch written into the options, so an Original guest flies the same
    /// map and rules. False on a guest and for a chapter the Environment box does not list.</summary>
    public bool AdoptLaunch(string chapter, VersusRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        int environment = EnvironmentOf(chapter);
        if (!IsHost || environment < 0)
        {
            return false;
        }

        var adopted = _options with
        {
            Environment = (byte)environment,
            Victory = rules.KillTarget > 0
                ? rules.TimeLimitMinutes > 0 ? DogfightVictory.Both : DogfightVictory.Score
                : DogfightVictory.Time,
            TimeMinutes = (byte)Math.Clamp(rules.TimeLimitMinutes > 0 ? rules.TimeLimitMinutes : _options.TimeMinutes, 1, MaxTimeMinutes),
            Score = (ushort)Math.Clamp(rules.KillTarget > 0 ? rules.KillTarget : _options.Score, 1, MaxScore),
            LimitedLives = rules.Lives > 0,
            Lives = (byte)ClampLives(rules.Lives > 0 ? rules.Lives : _options.Lives),
            AutoRespawn = rules.AutoRespawn,
        };

        // Written without a new round: the guests' Ready marks are what let this launch go.
        _options = adopted with { Epoch = _options.Epoch };
        return true;
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
    /// clears. The host opens a new round, which clears every guest's mark too.</summary>
    public void Land(IReadOnlyList<DogfightScore> scores)
    {
        ArgumentNullException.ThrowIfNull(scores);
        _scores = new List<DogfightScore>(scores).ToArray();
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

    /// <summary>Shows a line in this pilot's own chat panel under <paramref name="name"/>, sent to
    /// nobody. The host's address goes here, where the lobby already has room for a sentence.
    /// </summary>
    public void Note(string name, string text) => Add(new LobbyChatMessage(name ?? "", text ?? ""));

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

    /// <summary>The peer a host's player list row names. It is -1 for the host's own row, for a row
    /// past the list, and on a guest.</summary>
    public int PeerAt(int row)
    {
        if (!IsHost || row < 1)
        {
            return -1;
        }

        var peers = SeatedPeers();
        return row - 1 < peers.Count ? peers[row - 1] : -1;
    }

    /// <summary>One menu frame, after the socket was stepped. The host relays each guest's chat to the
    /// others and sends each guest its options and list when they change. A guest takes the host's
    /// chat and sends its own pick.</summary>
    public void Step()
    {
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
            SendToGuests();
        }
        else
        {
            SendPick();
        }
    }

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

    // A guest counts as Ready only under this round and on a plane the rules admit. The guest's
    // own lobby asks the same, but the host does not take its word for it.
    private bool Admits(int peer, CoopPickMessage pick) =>
        pick.Ready && pick.Epoch == _options.Epoch
        && _rules.Refuses(pick.Airframe, _wire.PickBuilds.TryGetValue(peer, out var build) ? build : null) == PlaneRefusal.None;

    // The original's Ready check, true when it refuses. An outlawed ammunition or rocket also resets
    // every gun's or pylon's to none, so the next Ready takes; Outlaw All resets without refusing.
    private bool ReadyCheck()
    {
        var rules = Rules;
        if (rules.Refuses(_airframe, _build) is var plane and not PlaneRefusal.None)
        {
            _refusals.Add(plane);
        }

        if (rules.AmmoOutlawed(_fit) && !rules.Has(NetPlaneRules.AllAmmoFlag))
        {
            _refusals.Add(PlaneRefusal.Ammo);
        }

        if (rules.RocketsOutlawed(_fit) && !rules.Has(NetPlaneRules.AllRocketsFlag))
        {
            _refusals.Add(PlaneRefusal.Rockets);
        }

        _fit = rules.Enforce(_fit);
        return _refusals.Count > 0;
    }

    // ⚠ Readiness is consent to the plane as it stood, so the host must not launch a changed one.
    private void Take(byte airframe, NetPlaneBuild? build, CoopFit fit)
    {
        if (airframe != _airframe || fit != _fit || !Equals(build, _build))
        {
            _ready = false;
        }

        _airframe = airframe;
        _build = build;
        _fit = fit;
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

    // Round 0 is a guest's "heard nothing", so the count wraps past it.
    private byte NextEpoch() => (byte)(_options.Epoch == byte.MaxValue ? 1 : _options.Epoch + 1);

    private string OwnName()
    {
        string name = _name() ?? "";
        return name.Length > 0 ? name : IsHost ? "Host" : "Pilot";
    }

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
        var rows = new List<DogfightLobbySeat> { new(OwnName(), _airframe, Ready, true, _teams.TeamOf(self), _teams.IsCaptain(self)) };
        var peers = SeatedPeers();
        for (int i = 0; i < peers.Count; i++)
        {
            bool picked = _wire.Picks.TryGetValue(peers[i], out var pick);
            string name = picked && pick.Name is { Length: > 0 } named
                ? named
                : "Pilot " + (rows.Count + 1).ToString(CultureInfo.InvariantCulture);
            byte airframe = picked && pick.Airframe < AirframeCount ? pick.Airframe : DefaultAirframe;
            rows.Add(new DogfightLobbySeat(name, airframe, picked && Admits(peers[i], pick), false,
                _teams.TeamOf(peers[i]), _teams.IsCaptain(peers[i])));
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

    private void Post(IReadOnlyList<NetTeamEvent> events)
    {
        foreach (var happened in events)
        {
            Announce(TeamLine(happened.Kind, MemberName(happened.Member), happened.TeamName));
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

    // A guest that has not heard the host's list yet shows itself alone.
    private DogfightLobbySeat[] GuestAlone() => new[] { new DogfightLobbySeat(OwnName(), _airframe, Ready, false) };

    private void SendToGuests()
    {
        var peers = SeatedPeers();
        var rows = HostRows();
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

            if (!_optionsSent.TryGetValue(peer, out var sent) || sent != _options)
            {
                _wire.Tell(peer, _options);
                _optionsSent[peer] = _options;
            }

            // The team list goes before the rows that name its teams.
            if (!_teamsSent.TryGetValue(peer, out var teamed) || teamed != teams)
            {
                _wire.Tell(peer, teams);
                _teamsSent[peer] = teams;
            }

            var roster = new DogfightRosterMessage(_options.Epoch, (byte)(i + 1), rows);
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

        // The build goes before the pick, so the host never reads a Ready without the plane behind it.
        if (!Equals(_build, _buildSent))
        {
            _wire.Tell(_hostPeer, new PlaneBuildMessage(PlaneBuildMessage.Mine, _build));
            _buildSent = _build?.Copy();
        }

        var pick = new CoopPickMessage(options.Epoch, Ready, _airframe, _fit, OwnName(), Voice: Voice());
        if (_pickSent != pick)
        {
            _wire.Tell(_hostPeer, pick);
            _pickSent = pick;
        }
    }
}
