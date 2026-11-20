using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.UI.Boards;
using CSVM.UI.Campaign;
using CSVM.UI.Screens;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The Multiplayer Games list, the Multiplayer plaque's page, a standalone module over
/// <see cref="NetPlayFeature"/>. It lists what the LAN search and the master server hear, drawn
/// from the original's games list script, whose corners are inline (<c>docs/org/menu-inventory.md</c>).
/// The original's Connection page is left out, so the list opens directly on a fresh search. Join
/// by Code raises a box holding the Join code edit box, and Build Custom Plane opens the
/// wallet-free hangar. Create Game hosts a Dogfight's lobby once Game and Player Information are
/// answered. Every join answers Player Information first and is followed on a messagebox.
/// </summary>
public sealed class OriginalConnectionScreen : IOriginalScreenModule
{
    /// <summary>Join by Code, which raises the Join code box, or says why joining by code is
    /// unavailable. The original has no such plaque.</summary>
    public const string CodeKey = "MPG_B_CODE";

    /// <summary>The Join code edit box in the Join by Code box.</summary>
    public const string CodeBoxKey = "MPG_E_CODE";

    /// <summary>The Join by Code box's OK, which joins the typed code.</summary>
    public const string CodeOkKey = "MPG_B_CODEOK";

    /// <summary>The Join by Code box's Cancel, back to the list.</summary>
    public const string CodeCancelKey = "MPG_B_CODECANCEL";

    /// <summary>The Join by Code plaque's words and the box's title. The string table has none.</summary>
    public const string CodeName = "Join by Code";

    /// <summary>The Join code box's label.</summary>
    public const string CodeBoxLabel = "Join code:";

    /// <summary>The words under the Join code box.</summary>
    public const string CodeBoxHint =
        "Join an internet game with the code its host shares, such as K7Q-X3M. No router setup needed.";

    /// <summary>The box raised when OK finds no code in the Join code box, worded as the
    /// original's own unrecognized address (langui 10025).</summary>
    public const string CodeNotRecognized = "The join code is not recognized. A code has six letters and digits, such as K7Q-X3M.";

    /// <summary>The most characters the Join code box holds: a code and its dash.</summary>
    public const int CodeBoxLimit = MasterWire.CodeLength + 1;

    /// <summary>Build Custom Plane, which opens the wallet-free hangar and comes back here. Its
    /// builds are the lobby's Custom Planes. Greyed only on a shell with no hangar.</summary>
    public const string BuildKey = "MP_B_BUILD";

    /// <summary>The auto refresh checkbox.</summary>
    public const string RefreshKey = "MPG_C_REFRESH";

    /// <summary>Create Game, which hosts a Dogfight.</summary>
    public const string CreateKey = "MPG_B_CREATE";

    /// <summary>Join Game, live once a joinable row is picked.</summary>
    public const string JoinKey = "MPG_B_JOIN";

    /// <summary>Exit, back to the main menu.</summary>
    public const string ExitKey = "MPG_B_EXIT";

    /// <summary>How many games the list shows. It does not scroll.</summary>
    public const int VisibleGames = 12;

    /// <summary>The column the list opens sorted by, # of Players.</summary>
    public const int DefaultSort = 1;

    /// <summary>How often an empty list asks again, in seconds.</summary>
    public const double EmptyRefreshSeconds = 1.0;

    /// <summary>How often a filled list asks again while auto refresh is checked, in seconds.</summary>
    public const double AutoRefreshSeconds = 5.0;

    /// <summary>The longest mission name the Mission Environment column holds before it takes the
    /// mission's shortcode instead, in characters.</summary>
    public const int EnvironmentFit = 24;

    private const string SortKeyPrefix = "MPG_R_SORT_";
    private const string GameKeyPrefix = "MPG_L_GAME_";

    private const string GamesBackground = "MP_GAMESBACKGROUND.JPG";
    private const string CodeBackground = "MP_CREATETEAMBACKGROUND.PNG";
    private const string RadioArt = "MP_B_RADIO.PNG";
    private const string SmallArt = "MP_B_SMALL.PNG";
    private const string MediumArt = "MP_B_MEDIUM.PNG";
    private const string LargeArt = "MP_B_LARGE.PNG";
    private const string CheckboxArt = "MP_B_CHECKBOXLARGE.PNG";
    private const string BuildArt = "GN_B_BUILDCUSTOMPLANE.PNG";
    private const string SortHighlightArt = "MP_GAMESALPHA.PNG";
    private const float RadioSize = 18f;

    // ⚠ The install's Auto refresh label runs to about x 325. The three large plaques start past it
    // and end on the list's right edge. Build Custom Plane and Exit stand on a second line.
    private const float HeaderY = 141f;
    private const float ListX = 53f;
    private const float ListY = 175f;
    private const float ListWidth = 690f;
    private const float ListPitch = 25f;
    private const float ButtonLineY = 507f;
    private const float SecondLineY = 559f;
    private const float CreateX = 340f;
    private const float PlaquePitch = 135f;
    private const float RefreshX = 47f;
    private const float ExitX = ListX + ListWidth - 74f;
    private const float StatusY = ListY + (2f * ListPitch);
    private const float SortHighlightAlpha = 80f / 255f;
    private const float PickedAlpha = 0x80 / 255f;

    // The Join by Code box stands in the CREATE TEAM box's art and corners (OriginalTeamBox).
    private const float BoxX = 234f;
    private const float BoxY = 99f;
    private const float FieldX = 265f;
    private const float FieldY = 200f;
    private const float FieldWidth = 272f;
    private const float FieldHeight = 24f;
    private const float BoxButtonY = 382f;

    private static readonly float[] ColumnWidths = { 145f, 100f, 166f, 145f, 115f };
    private static readonly float[] HighlightOffsets = { 0f, 147f, 249f, 414f, 557f };
    private static readonly string[] Headers = { "Game Name", "# of Players", "Mission Type", "Mission Environment", "Status" };

    // The script's colours beyond the plaque labels' four.
    private static readonly BoardTint Black = new(0, 0, 0);
    private static readonly BoardTint White = new(255, 255, 255);
    private static readonly BoardTint Unjoinable = new(0x80, 0x80, 0x80);
    private static readonly BoardTint SearchingRed = new(255, 0, 0);
    private static readonly BoardTint CreateDisabled = new(165, 165, 164);
    private static readonly BoardTint SortTint = new(242, 208, 139);

    private readonly Func<NetPlayFeature?> _net;
    private readonly IOriginalScreenHost _host;
    private readonly MultiplayerBoardText _text;
    private readonly Action _openLobby;
    private readonly Action<NetSessionKind?, Action, bool> _ask;
    private (string Address, int Port)? _picked;
    private double _sinceAsk;
    private int _heard;
    private string _status = string.Empty;

    // Whether the cursor stands on Create Game because the list opened empty, so the first
    // answers may move it to the first game. A cursor the player moved is left alone.
    private bool _defaultFocus;

    // A join this page started, followed until it ends, and the words of the box standing for it.
    private bool _following;
    private string? _shown;

    /// <summary>A games list over the door <paramref name="net"/> answers, which is null on a shell
    /// with no multiplayer door. It reads its words from the string table under
    /// <paramref name="dataRoot"/> and calls back into <paramref name="host"/>. Create Game calls
    /// <paramref name="openLobby"/>. <paramref name="ask"/> stands the network boxes over the page
    /// before a host or a join goes ahead. It takes a host's kind, or null, what to run after OK,
    /// and whether a join may be asked a password. Without it both go ahead at once.</summary>
    public OriginalConnectionScreen(
        Func<NetPlayFeature?> net, IOriginalScreenHost host, string? dataRoot, Action? openLobby = null,
        Action<NetSessionKind?, Action, bool>? ask = null)
    {
        _openLobby = openLobby ?? (() => { });
        _ask = ask ?? ((_, then, _) => then());
        _net = net ?? throw new ArgumentNullException(nameof(net));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _text = new MultiplayerBoardText(_host, dataRoot);
    }

    /// <summary>The column the list is sorted by, 0 to 4.</summary>
    public int Sort { get; private set; } = DefaultSort;

    /// <summary>Whether auto refresh is checked. It starts checked and keeps its state for the
    /// session.</summary>
    public bool AutoRefresh { get; private set; } = true;

    /// <summary>The games the list shows, sorted and cut to <see cref="VisibleGames"/>.</summary>
    public IReadOnlyList<LanGame> Listed => Sorted();

    /// <summary>Whether the Join by Code box stands over the list.</summary>
    public bool CodeBoxOpen { get; private set; }

    /// <summary>The Join code box's text as typed, in capitals.</summary>
    public string TypedCode { get; private set; } = string.Empty;

    /// <summary>Why joining by code is shut, or "" while it is live. A shell with no network door
    /// names no master server.</summary>
    public string CodeFault => _net()?.Internet.CodeFault ?? CoopDoorText.NoMasterServer;

    /// <summary>The red line an empty list stands on: Searching, or why no game can be heard. It
    /// names a shut search, a build too old for the master server, or a master server's fault.</summary>
    public string Status
    {
        get
        {
            if (_net() is not { Searching: true } net)
            {
                return _text.Word(10022, "No local area network connection is detected.");
            }

            if (net.Internet.MasterOutdated)
            {
                return CoopDoorText.MasterOutdated;
            }

            return net.Internet.Master?.Fault is { Length: > 0 } fault
                ? char.ToUpperInvariant(fault[0]) + fault[1..] + "."
                : _text.Word(10577, "Searching ...");
        }
    }

    /// <summary>Whether seat 0's typed characters feed the Join code box: it stands with the focus
    /// and no messagebox over it.</summary>
    internal bool CapturingText =>
        _host.Screen == OriginalScreen.Connection && CodeBoxOpen && !_host.DialogOpen && _host.FocusedKey == CodeBoxKey;

    /// <summary>A game row's key by its place in <see cref="Listed"/>.</summary>
    public static string GameKey(int index) => GameKeyPrefix + index.ToString(CultureInfo.InvariantCulture);

    /// <summary>A sort radio's key by its column.</summary>
    public static string SortKey(int column) => SortKeyPrefix + column.ToString(CultureInfo.InvariantCulture);

    /// <summary>Whether a screen is this module's.</summary>
    public bool Owns(OriginalScreen screen) => screen == OriginalScreen.Connection;

    /// <summary>The Multiplayer plaque's door: the games list on a fresh search.</summary>
    public void OpenConnection()
    {
        _host.CloseDialog();
        _host.Open(OriginalScreen.Connection);
    }

    /// <summary>A game's five cells as the list writes them. A game of another version names that
    /// version in its Status cell.</summary>
    public IReadOnlyList<string> Cells(LanGame game)
    {
        var advert = game.Advert;
        return new[]
        {
            CoopDoorText.GameName(advert),
            CoopDoorText.PlayerCount(advert),
            CoopDoorText.MissionType(advert),
            CoopDoorText.Environment(advert, MissionName, name => name.Length <= EnvironmentFit, _text.Strings),
            CoopDoorText.Status(game, _net()?.Version ?? NetBuildVersion.Unknown),
        };
    }

    /// <summary>The page's rows: the sort radios, the plaques and then the games, so a plaque's
    /// place holds while games come and go. The Join by Code box's rows alone while it stands.</summary>
    public void BuildRows(List<OriginalRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (CodeBoxOpen)
        {
            rows.Add(new OriginalRow(CodeBoxKey, TypedCode, OriginalRowKind.TextField, FieldX, FieldY, FieldWidth, FieldHeight, true, 0, null));
            rows.Add(_text.Strip(CodeOkKey, SmallArt, 360f, BoxButtonY, true, 0, 74f, 37f));
            rows.Add(_text.Strip(CodeCancelKey, MediumArt, 452f, BoxButtonY, true, 0, 96f, 37f));
            return;
        }

        PageRows(rows);
    }

    /// <summary>The page does not scroll.</summary>
    public void Lists(List<OriginalList> lists)
    {
    }

    /// <summary>Nothing on the page takes a sideways step, so it moves the focus.</summary>
    public bool StepSideways(IReadOnlyList<OriginalRow> rows, int focus, int direction) => false;

    /// <summary>The page opens no list.</summary>
    public bool CloseDropdown() => false;

    /// <summary>The page's answer to an activated row.</summary>
    public MenuExit? Activate(OriginalRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        switch (row.Key)
        {
            case CodeKey:
                OpenCodeBox();
                break;
            case CodeBoxKey:
            case CodeOkKey:
                ConnectByCode();
                break;
            case CodeCancelKey:
                CloseCodeBox();
                break;
            case BuildKey:
                _host.OpenHangar(null);
                break;
            case CreateKey:
                _ask(NetSessionKind.Dogfight, _openLobby, false);
                break;
            case RefreshKey:
                AutoRefresh = !AutoRefresh;
                break;
            case JoinKey:
                JoinPicked();
                break;
            case ExitKey:
                Leave();
                break;
            case var sort when sort.StartsWith(SortKeyPrefix, StringComparison.Ordinal):
                Sort = (OriginalWidgets.Indexed(sort, SortKeyPrefix) ?? -1);
                break;
            case var game when game.StartsWith(GameKeyPrefix, StringComparison.Ordinal):
                PickOrJoin((OriginalWidgets.Indexed(game, GameKeyPrefix) ?? -1));
                break;
        }

        return null;
    }

    /// <summary>Back is the Join by Code box's Cancel while it stands, and otherwise the page's Exit.
    /// </summary>
    public bool Back()
    {
        if (CodeBoxOpen)
        {
            CloseCodeBox();
        }
        else
        {
            Leave();
        }

        return true;
    }

    /// <summary>The page as drawn, with the Join by Code box over it while it stands.</summary>
    public void Compose(IReadOnlyList<OriginalRow> rows, int focus, BoardLayers layers)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(layers);
        string focused = focus >= 0 && focus < rows.Count ? rows[focus].Key : string.Empty;
        int pressedAt = _host.DialogOpen ? -1 : _host.PressedRow;
        string pressed = pressedAt >= 0 && pressedAt < rows.Count ? rows[pressedAt].Key : string.Empty;
        ComposeGames(CodeBoxOpen ? string.Empty : focused, CodeBoxOpen ? string.Empty : pressed, layers);
        if (CodeBoxOpen)
        {
            ComposeCodeBox(rows, focused, pressed, layers);
        }

        if (_host.SeatPanel(false) is { } strip)
        {
            layers.Overlays.Add(strip);
        }
    }

    /// <summary>One menu frame's upkeep while the page shows, after the door was stepped. The list
    /// asks again on its timer, and a join started here is followed on its box. Returns whether
    /// the picture changed.</summary>
    public bool Tick(double dt)
    {
        if (_net() is not { } net || !Owns(_host.Screen))
        {
            return false;
        }

        bool changed = false;
        if (net.Searching)
        {
            _sinceAsk += dt;
            double every = net.Games.Count == 0 ? EmptyRefreshSeconds : AutoRefresh ? AutoRefreshSeconds : double.PositiveInfinity;
            if (_sinceAsk >= every)
            {
                _sinceAsk = 0.0;
                net.Search();
            }
        }

        int heard = Hash(net.Games);
        if (heard != _heard)
        {
            bool first = _heard == 0 && net.Games.Count > 0;
            _heard = heard;
            changed = true;
            if (first && _defaultFocus && !CodeBoxOpen && _host.FocusedKey == CreateKey)
            {
                _host.FocusKey(GameKey(0));
            }

            _defaultFocus &= !first;
        }

        string status = Status;
        if (status != _status)
        {
            _status = status;
            changed = true;
        }

        return FollowJoin(net) || changed;
    }

    /// <summary>The list opening, whoever opened it: a fresh search, nothing picked, and the cursor
    /// on Create Game until the first answers. The shell calls it on every open of the page.</summary>
    internal void Enter()
    {
        if (_net() is { } net)
        {
            net.StopSearch();
            net.Search();
        }

        _picked = null;
        _sinceAsk = 0.0;
        _heard = 0;
        _status = Status;
        CodeBoxOpen = false;
        _host.FocusKey(CreateKey);
        _defaultFocus = true;
    }

    /// <summary>Typed characters, a paste and Backspace into the Join code box. Each character cues
    /// the edit box's keystroke, or its reject when the box refuses it. A paste inserts the
    /// clipboard and cues once, the reject when any of it was left out.</summary>
    internal bool TypeText(MenuCommands commands, List<string> cues)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(cues);
        if (!CapturingText || (commands.Typed.Length == 0 && !commands.Erase && !commands.Paste))
        {
            return false;
        }

        string before = TypedCode;
        foreach (char c in commands.Typed)
        {
            cues.Add(TypeCode(c.ToString()) > 0 ? OriginalCues.Text : OriginalCues.TextError);
        }

        if (commands.Paste)
        {
            string text = (MenuInput.Clipboard() ?? string.Empty).Trim();
            int taken = TypeCode(text);
            cues.Add(taken == 0 || taken < text.Length ? OriginalCues.TextError : OriginalCues.Text);
        }

        if (commands.Erase && TypedCode.Length > 0)
        {
            TypedCode = TypedCode[..^1];
        }

        return TypedCode != before;
    }

    /// <summary>Appends <paramref name="typed"/> to the Join code box in capitals, answering how
    /// many characters it took. It takes only the code alphabet and the dash, up to
    /// <see cref="CodeBoxLimit"/>, and leaves the reading of a code to OK.</summary>
    internal int TypeCode(string typed)
    {
        int taken = 0;
        foreach (char c in typed ?? string.Empty)
        {
            char upper = char.ToUpperInvariant(c);
            bool allowed = char.IsAscii(c) && (upper == '-' || MasterWire.CodeAlphabet.Contains(upper, StringComparison.Ordinal));
            if (allowed && TypedCode.Length < CodeBoxLimit)
            {
                TypedCode += upper;
                taken++;
            }
        }

        return taken;
    }

    /// <summary>Empties the Join code box.</summary>
    internal void ClearCode() => TypedCode = string.Empty;

    /// <summary>Stands the Join by Code box with <paramref name="typed"/> in it and the cursor
    /// there, for a screenshot aid.</summary>
    internal void PoseCode(string typed)
    {
        ClearCode();
        TypeCode(typed);
        CodeBoxOpen = true;
        _host.FocusKey(CodeBoxKey);
    }

    /// <summary>Lets go of the join this page followed, its guest having gone on to its host's
    /// boards. The box standing for it goes with the screen change.</summary>
    internal void StopFollowing()
    {
        _following = false;
        _shown = null;
    }

    /// <summary>Follows the door's join again, for a guest coming back from its host's boards. An
    /// ended link then raises the box saying why, as a join that failed here does.</summary>
    internal void FollowAgain()
    {
        _following = true;
        _shown = null;
    }

    private static int Hash(IReadOnlyList<LanGame> games)
    {
        var hash = default(HashCode);
        hash.Add(games.Count);
        foreach (var game in games)
        {
            hash.Add(game);
        }

        return games.Count == 0 ? 0 : hash.ToHashCode() | 1;
    }

    private static float SortX(int column) => column < 2 ? 145f + (133f * column) : 148f + (133f * column);

    private static float SortY(int column) => column < 2 ? 116f : 118f;

    private static float ColumnX(int column)
    {
        float x = ListX;
        for (int i = 0; i < column; i++)
        {
            x += ColumnWidths[i];
        }

        return x;
    }

    private static int CompareBy(int column, IReadOnlyList<string> a, IReadOnlyList<string> b, LanGame left, LanGame right) =>
        column == 1
            ? right.Advert.Players.CompareTo(left.Advert.Players)
            : string.Compare(a[column], b[column], StringComparison.OrdinalIgnoreCase);

    private string MissionName(int seq) =>
        _text.Strings.Text(3450 + seq, $"Mission {(seq + 1).ToString(CultureInfo.InvariantCulture)}");

    private void PageRows(List<OriginalRow> rows)
    {
        for (int column = 0; column < Headers.Length; column++)
        {
            rows.Add(new OriginalRow(SortKey(column), string.Empty, OriginalRowKind.Radio, SortX(column), SortY(column), RadioSize, RadioSize,
                true, 0, new BoardArt(BoardArtLibrary.Ui, RadioArt, 4)));
        }

        rows.Add(new OriginalRow(RefreshKey, string.Empty, OriginalRowKind.Radio, RefreshX, ButtonLineY, 200f, 37f, true, 0,
            new BoardArt(BoardArtLibrary.Ui, CheckboxArt, 8)));
        rows.Add(_text.Strip(CreateKey, LargeArt, CreateX, ButtonLineY, _net() != null, 1, 131f, 37f));
        rows.Add(_text.Strip(JoinKey, LargeArt, CreateX + PlaquePitch, ButtonLineY, PickedGame() is { } game && CoopDoorText.Joinable(game.Advert), 1, 131f, 37f));
        rows.Add(_text.Strip(CodeKey, LargeArt, CreateX + (2f * PlaquePitch), ButtonLineY, true, 1, 131f, 37f));
        rows.Add(_text.Strip(BuildKey, BuildArt, RefreshX, SecondLineY + 2f, _host.CanBuildPlane, 0, 200f, 32f));
        rows.Add(_text.Strip(ExitKey, SmallArt, ExitX, SecondLineY, true, 1, 74f, 37f));
        var listed = Listed;
        for (int i = 0; i < listed.Count; i++)
        {
            rows.Add(new OriginalRow(GameKey(i), CoopDoorText.GameName(listed[i].Advert), OriginalRowKind.ListRow,
                ListX, ListY + (i * ListPitch), ListWidth, ListPitch, true, 0, null));
        }
    }

    private List<LanGame> Sorted()
    {
        var games = new List<LanGame>(_net()?.Games ?? Array.Empty<LanGame>());
        var cells = games.ToDictionary(game => game, Cells);
        games.Sort((left, right) =>
        {
            int by = CompareBy(Math.Clamp(Sort, 0, Headers.Length - 1), cells[left], cells[right], left, right);
            return by != 0 ? by : string.CompareOrdinal($"{left.Address}:{left.Port}", $"{right.Address}:{right.Port}");
        });
        return games.Count > VisibleGames ? games.GetRange(0, VisibleGames) : games;
    }

    private LanGame? PickedGame()
    {
        if (_picked is not { } picked)
        {
            return null;
        }

        foreach (var game in Listed)
        {
            if (game.Address == picked.Address && game.Port == picked.Port)
            {
                return game;
            }
        }

        return null;
    }

    // A shut way says why instead of standing a box that cannot join.
    private void OpenCodeBox()
    {
        if (CodeFault is { Length: > 0 } why)
        {
            _host.RaiseDialog(CoopDoorText.CodeJoinUnavailable(why), DialogIcon.Warning, Ok(null));
            return;
        }

        CodeBoxOpen = true;
        _host.FocusKey(CodeBoxKey);
    }

    private void CloseCodeBox()
    {
        CodeBoxOpen = false;
        _host.FocusKey(CodeKey);
    }

    // A code that does not read warns over the box, which stays for the fix. A code names no
    // advert, so the Password box stays live in case the host asks one.
    private void ConnectByCode()
    {
        if (_net() is not { } net)
        {
            return;
        }

        if (net.Internet.CodeFault is { Length: > 0 } why)
        {
            _host.RaiseDialog(CoopDoorText.CodeJoinUnavailable(why), DialogIcon.Warning, Ok(null));
            return;
        }

        if (!MasterWire.TryCode(TypedCode, out string code))
        {
            _host.RaiseDialog(CodeNotRecognized, DialogIcon.Warning, Ok(null));
            return;
        }

        CloseCodeBox();
        _ask(null, () =>
        {
            if (net.Stage is NetDoorStage.Failed)
            {
                net.Close();
            }

            net.JoinByCode(code);
            _following = true;
        }, true);
    }

    private void PickOrJoin(int index)
    {
        var listed = Listed;
        if (index < 0 || index >= listed.Count)
        {
            return;
        }

        var game = listed[index];
        if (PickedGame() is { } picked && picked.Address == game.Address && picked.Port == game.Port)
        {
            JoinPicked();
            return;
        }

        _picked = (game.Address, game.Port);
    }

    private void JoinPicked()
    {
        if (_net() is not { } net || PickedGame() is not { } game || !CoopDoorText.Joinable(game.Advert))
        {
            return;
        }

        // Refused before any socket opens, and the list stays up behind the box.
        if (!net.PlaysWith(game))
        {
            _host.RaiseDialog(CoopDoorText.VersionMismatch(game.Version, net.Version), DialogIcon.Warning, Ok(null));
            return;
        }

        _ask(null, () =>
        {
            if (net.Stage is NetDoorStage.Failed)
            {
                net.Close();
            }

            net.JoinGame(game);
            _following = true;
        }, game.Advert.Password);
    }

    private void Leave()
    {
        _net()?.StopSearch();
        CodeBoxOpen = false;
        _host.CloseDialog();
        _host.Open(OriginalScreen.TopLevel);
    }

    // The box a followed join stands on: connecting, waiting on the host, or why it ended, over
    // the list. Answering any of them hangs up.
    private bool FollowJoin(NetPlayFeature net)
    {
        if (!_following)
        {
            return false;
        }

        string text;
        OriginalDialogAnswer answer;
        var icon = DialogIcon.Query;
        switch (net.Stage)
        {
            case NetDoorStage.Joining:
                text = $"Connecting to {net.JoinName} ...";
                answer = new(OriginalShell.DialogCancelKey, CampaignBoards.DialogCenterKey, _text.Word(101, "Cancel"), () => EndJoin(net));
                break;
            case NetDoorStage.Joined:
                text = net.AwaitingAdmission ? CoopDoorText.AwaitingAdmission : CoopDoorText.WaitingStatus(net, MissionName);
                answer = new(OriginalShell.DialogCancelKey, CampaignBoards.DialogCenterKey, "Leave", () => EndJoin(net));
                break;
            case NetDoorStage.Failed:
                text = net.Fault;
                answer = Ok(() => EndJoin(net));
                icon = DialogIcon.Warning;
                break;
            default:
                bool standing = _shown != null;
                _following = false;
                _shown = null;
                return standing;
        }

        if (text == _shown && _host.DialogOpen)
        {
            return false;
        }

        _host.CloseDialog();
        _host.RaiseDialog(text, icon, answer);
        _shown = text;
        return true;
    }

    private void EndJoin(NetPlayFeature net)
    {
        _following = false;
        _shown = null;
        net.Close();
    }

    private OriginalDialogAnswer Ok(Action? run) =>
        new(OriginalShell.DialogOkKey, CampaignBoards.DialogCenterKey, _text.Word(100, "OK"), run);

    private void ComposeGames(string focused, string pressed, BoardLayers layers)
    {
        layers.Backdrop.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, GamesBackground), 0f, 0f));
        string title = _text.Strings.Format(10067, "Multiplayer");
        layers.Lines.Add(_text.Line(10067, string.Empty, 69f, 80f, 0f, White, text: title.Length > 0 ? title : "Multiplayer Games"));
        int sort = Math.Clamp(Sort, 0, Headers.Length - 1);
        layers.Pictures.Add(new BoardPicture(
            new BoardArt(BoardArtLibrary.Ui, SortHighlightArt), 54f + HighlightOffsets[sort], HeaderY,
            Opacity: SortHighlightAlpha, Tint: SortTint, Crop: new BoardCrop(HighlightOffsets[sort], 0f, ColumnWidths[sort], 341f)));

        // The page's own rows, also while the Join by Code box stands over them.
        var rows = new List<OriginalRow>();
        PageRows(rows);
        for (int column = 0; column < Headers.Length; column++)
        {
            var row = rows[column];
            ComposeRadio(row, column == sort, row.Key == focused, layers);
            layers.Lines.Add(_text.Line(10068, "Sort by:", row.X - 60f, row.Y, 0f, White));
            layers.Lines.Add(_text.Line(10069 + column, Headers[column], ColumnX(column), HeaderY + 8f, ColumnWidths[column], Black, BoardJustify.Center));
        }

        var listed = Listed;
        var picked = PickedGame();
        string hovered = HoveredKey();
        for (int i = 0; i < listed.Count; i++)
        {
            var game = listed[i];
            float y = ListY + (i * ListPitch);
            string key = GameKey(i);
            if (picked is { } p && p.Address == game.Address && p.Port == game.Port)
            {
                layers.Fills.Add(new BoardFill(ListX, y, ListWidth, ListPitch, 0x9f, 0x28, 0x20, PickedAlpha));
            }

            if (key == focused || key == hovered)
            {
                layers.Fills.Add(new BoardFill(ListX, y, ListWidth, ListPitch, 0xdf, 0xb8, 0x60, 1f, Border: true));
            }

            var cells = Cells(game);
            var ink = CoopDoorText.Joinable(game.Advert) && _net()?.PlaysWith(game) != false ? White : Unjoinable;
            for (int column = 0; column < cells.Count; column++)
            {
                layers.Lines.Add(new BoardLine(cells[column], ColumnX(column), y + 5f, ColumnWidths[column], MultiplayerBoardText.TextFallback,
                    BoardInk.Row, -1, Justify: BoardJustify.Center, Colour: ink));
            }
        }

        if (listed.Count == 0)
        {
            layers.Lines.Add(_text.Line(10577, string.Empty, ListX, StatusY, ListWidth, SearchingRed, BoardJustify.Center, Status));
        }

        var refresh = rows.First(row => row.Key == RefreshKey);
        int box = !refresh.Enabled ? 0 : refresh.Key == pressed ? 3 : refresh.Key == focused ? 2 : 1;
        layers.Pictures.Add(new BoardPicture(refresh.Art!, refresh.X, refresh.Y, (AutoRefresh ? 4 : 0) + box));
        layers.Lines.Add(_text.Line(10076, "Auto refresh", refresh.X + 62f, refresh.Y + 8f, 0f, new BoardTint(250, 250, 250)));

        // Join by Code has no string of its own, so it borrows Create Game's face.
        var plaques = new (string Key, int Face, string Label)[]
        {
            (CodeKey, 10074, CodeName),
            (CreateKey, 10074, _text.Word(10074, "Create Game")),
            (JoinKey, 10075, _text.Word(10075, "Join Game")),
            (ExitKey, 10077, _text.Word(10077, "Exit")),
            (BuildKey, 0, string.Empty),
        };
        foreach (var (key, face, label) in plaques)
        {
            var row = rows.First(r => r.Key == key);
            ComposePlaque(row, row.Key == focused, row.Key == pressed, layers, face, label,
                row.Key == CreateKey ? CreateDisabled : MultiplayerBoardText.LabelDisabled);
        }
    }

    // The Join by Code box over the page, drawn in the CREATE TEAM box's art.
    private void ComposeCodeBox(IReadOnlyList<OriginalRow> rows, string focused, string pressed, BoardLayers layers)
    {
        layers.Overlays.Add(new BoardPanel(Array.Empty<BoardFill>(),
            new[] { new BoardPicture(new BoardArt(BoardArtLibrary.Ui, CodeBackground), BoxX, BoxY) }, Array.Empty<BoardLine>()));
        var pictures = new List<BoardPicture>();
        var lines = new List<BoardLine>
        {
            _text.Line(10552, string.Empty, FieldX, 130f, FieldWidth, Black, BoardJustify.Center, CodeName.ToUpperInvariant()),
            _text.Line(10551, string.Empty, FieldX - 3f, FieldY - 25f, 0f, Black, text: CodeBoxLabel),
            _text.Line(10551, string.Empty, FieldX, FieldY + 40f, FieldWidth, Black, BoardJustify.Center, CodeBoxHint),
        };

        foreach (var row in rows)
        {
            if (row.Key == CodeBoxKey)
            {
                var caret = row.Key == focused && !_host.DialogOpen ? new BoardCaret(0, 0, 0, 1f, row.Height - 8f) : (BoardCaret?)null;
                var face = _text.Regular(10527);
                lines.Add(new BoardLine(row.Label, row.X + 6f, row.Y + 3f, row.Width - 12f, face?.Pixels ?? MultiplayerBoardText.TextFallback,
                    BoardInk.Row, -1, Caret: caret, Face: face, Colour: Black)
                {
                    KeepEnd = true,
                });
                continue;
            }

            bool ok = row.Key == CodeOkKey;
            int id = ok ? 10534 : 10535;
            var plaque = new BoardLayers();
            ComposePlaque(row, row.Key == focused, row.Key == pressed, plaque, id, _text.Word(id, ok ? "OK" : "Cancel"));
            pictures.AddRange(plaque.Pictures);
            lines.AddRange(plaque.Lines);
        }

        layers.Overlays.Add(new BoardPanel(Array.Empty<BoardFill>(), pictures, lines));
    }

    private void ComposeRadio(OriginalRow row, bool selected, bool focused, BoardLayers layers)
    {
        int frame = !row.Enabled ? 0 : selected ? 3 : focused ? 2 : 1;
        layers.Pictures.Add(new BoardPicture(row.Art!, row.X, row.Y, frame));
    }

    // A picture rather than a plaque, since the plaque layer stands over the text. The words are
    // drawn in the script's own colours rather than a plaque ink; a face of 0 draws none.
    private void ComposePlaque(
        OriginalRow row, bool focused, bool pressed, BoardLayers layers, int faceId, string label, BoardTint? disabled = null)
    {
        int frame = row.Enabled ? ComposedBoard.PlaqueFrame(row.Art!.Frames, focused, pressed) : 0;
        layers.Pictures.Add(new BoardPicture(row.Art!, row.X, row.Y, frame));
        if (faceId == 0)
        {
            return;
        }

        var face = _text.Regular(faceId);
        float size = face?.Pixels ?? MultiplayerBoardText.TextFallback;
        var tint = !row.Enabled && disabled is { } grey ? grey : MultiplayerBoardText.LabelTint(row.Enabled, focused, pressed);
        layers.Lines.Add(new BoardLine(label, row.X, row.Y + ((row.Height - size) / 2f) - 1f, row.Width, size,
            BoardInk.Row, -1, Justify: BoardJustify.Center, Face: face, Colour: tint));
    }

    private string HoveredKey()
    {
        if (_host.HoveredRow < 0)
        {
            return string.Empty;
        }

        var rows = new List<OriginalRow>();
        BuildRows(rows);
        int hovered = _host.HoveredRow;
        return hovered < rows.Count ? rows[hovered].Key : string.Empty;
    }
}
