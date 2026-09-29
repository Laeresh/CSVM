using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Net;
using CSVM.UI.Boards;
using CSVM.UI.Campaign;
using CSVM.UI.Screens;

namespace CSVM.UI.Menu.Original;

/// <summary>Which of the original's two network boxes stands.</summary>
public enum NetInfoPage
{
    /// <summary>GAME INFORMATION, a host's: the game's name, its password and its seat cap.</summary>
    Game,

    /// <summary>PLAYER INFORMATION, every player's: the callsign and the voice.</summary>
    Player,
}

/// <summary>
/// The original's GAME INFORMATION and PLAYER INFORMATION boxes, in the placements and art of
/// <c>MULTIPLAYERHOSTMODAL.SCRIPT</c> and <c>MULTIPLAYERPLAYERMODAL.SCRIPT</c>. The shell stands
/// it over whatever page asked, as it does a messagebox. A host answers Game Information and then
/// Player Information, and a joining player answers Player Information alone. OK is greyed while
/// the name box is empty, and a name of spaces alone raises the original's refusal. The Player
/// Information Password takes the join's password when the game asks one, and is greyed
/// otherwise, as the script's callback 5003 opens it. The decode is in
/// <c>docs/org/multiplayer-messages.md</c>, "Game and Player Information".
/// </summary>
public sealed class OriginalNetInfoBox
{
    /// <summary>The Game Name box.</summary>
    public const string GameNameKey = "MPI_E_GAMENAME";

    /// <summary>Game Information's Password (optional) box.</summary>
    public const string PasswordKey = "MPI_E_PASSWORD";

    /// <summary>The Maximum # of Players spinner's number.</summary>
    public const string PlayersKey = "MPI_S_PLAYERS";

    /// <summary>The spinner's up arrow.</summary>
    public const string MoreKey = "MPI_B_MORE";

    /// <summary>The spinner's down arrow.</summary>
    public const string FewerKey = "MPI_B_FEWER";

    /// <summary>The Callsign box.</summary>
    public const string CallsignKey = "MPI_E_CALLSIGN";

    /// <summary>The Voice drop-down.</summary>
    public const string VoiceKey = "MPI_D_VOICE";

    /// <summary>Player Information's Password box, live only for a join that may be asked one.
    /// </summary>
    public const string PlayerPasswordKey = "MPI_E_PLAYERPASSWORD";

    /// <summary>OK, greyed while the name box is empty.</summary>
    public const string OkKey = "MPI_B_OK";

    /// <summary>Cancel, back to the page under the box.</summary>
    public const string CancelKey = "MPI_B_CANCEL";

    private const string Prefix = "MPI_";
    private const string GameArt = "MP_GAMEINFOBACKGROUND.PNG";
    private const string PlayerArt = "MP_PLAYERINFOBACKGROUND.PNG";
    private const string SmallArt = "MP_B_SMALL.PNG";
    private const string MediumArt = "MP_B_MEDIUM.PNG";
    private const string UpArt = "MP_B_SCROLLUP.PNG";
    private const string DownArt = "MP_B_SCROLLDOWN.PNG";
    private const string ListArrowArt = "GN_B_LISTBOXARROWDOWN.PNG";

    // The scripts' own corners and sizes. Each box is 272 by 24, its label 3 left and 25 up
    // (ND.location). Its text stands 6 right and 3 down (SZ.OD). The Voice list's words stand 8 in
    // (WGA.UG), and its rows are 24 high (WGA.XB).
    private const float FieldWidth = 272f;
    private const float FieldHeight = 24f;
    private const float LabelDx = -3f;
    private const float LabelDy = -25f;
    private const float TextDx = 6f;
    private const float TextDy = 3f;
    private const float VoiceTextDx = 8f;
    private const float ArrowSize = 24f;
    private const float SpinnerWidth = 38f;
    private const float SpinnerHeight = 25f;
    private const float SpinnerArrowWidth = 16f;
    private const float SpinnerArrowHeight = 11f;
    private const float ButtonY = 372f;
    private const float OkX = 360f;
    private const float CancelX = 452f;
    private const char Mask = '*';

    private static readonly BoardTint Black = new(0, 0, 0);

    // The fills the scripts name: a greyed box's (LD) and the Voice list's rows, normal (LF), under
    // the cursor (MF) and picked (NF).
    private static readonly (byte R, byte G, byte B) DisabledFill = (163, 163, 163);
    private static readonly (byte R, byte G, byte B) ItemFill = (181, 174, 156);
    private static readonly (byte R, byte G, byte B) ItemFocused = (214, 186, 123);
    private static readonly (byte R, byte G, byte B) ItemPicked = (180, 201, 204);

    private readonly IOriginalScreenHost _host;
    private readonly MultiplayerBoardText _text;
    private readonly Action<NetPlayerInfo, bool> _remember;
    private NetPlayerInfo _draft = new();
    private NetSessionKind? _hosting;
    private Action<NetPlayerInfo>? _done;
    private int _focusBefore = -1;
    private bool _voiceOpen;
    private bool _asksPassword;

    /// <summary>A box drawn for <paramref name="host"/> in the words of the string table under
    /// <paramref name="dataRoot"/>. The last OK hands the answers to <paramref name="remember"/>,
    /// with whether Game Information was asked, so they prefill the next session.</summary>
    public OriginalNetInfoBox(IOriginalScreenHost host, string? dataRoot, Action<NetPlayerInfo, bool>? remember = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _text = new MultiplayerBoardText(host, dataRoot);
        _remember = remember ?? ((_, _) => { });
    }

    /// <summary>The box standing, or null.</summary>
    public NetInfoPage? Page { get; private set; }

    /// <summary>Whether a box stands.</summary>
    public bool IsOpen => Page != null;

    /// <summary>What the boxes hold so far.</summary>
    public NetPlayerInfo Draft => _draft;

    /// <summary>Whether the Voice list stands open under its box.</summary>
    public bool VoiceListOpen => _voiceOpen;

    /// <summary>Whether Player Information's Password box takes a password: on a join that may be
    /// asked one, never on a host's own box.</summary>
    public bool JoinPasswordLive => Page == NetInfoPage.Player && _hosting == null && _asksPassword;

    /// <summary>Whether seat 0's typed characters feed one of the boxes. They do while an edit box
    /// has the focus and no list or messagebox stands over it.</summary>
    internal bool CapturingText =>
        IsOpen && !_voiceOpen && !_host.DialogOpen
        && (_host.FocusedKey is GameNameKey or PasswordKey or CallsignKey || (_host.FocusedKey == PlayerPasswordKey && JoinPasswordLive));

    /// <summary>Whether a key is one of the boxes' rows.</summary>
    public static bool Owns(string key) => key.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Stands the boxes over the page showing, starting from <paramref name="start"/>. A
    /// host's <paramref name="hosting"/> names the kind of game, whose cap the spinner keeps. It
    /// opens on Game Information, and null opens Player Information alone. A join that
    /// <paramref name="asksPassword"/> may be asked one leaves Player Information's Password box
    /// live. <paramref name="done"/> takes the answers once the last OK stands.</summary>
    public void Open(NetSessionKind? hosting, NetPlayerInfo start, Action<NetPlayerInfo> done, bool asksPassword = false)
    {
        ArgumentNullException.ThrowIfNull(start);
        _draft = start.Copy();
        _hosting = hosting;
        _asksPassword = asksPassword;
        if (hosting == null)
        {
            _draft.Password = string.Empty;
        }

        _done = done ?? throw new ArgumentNullException(nameof(done));
        if (hosting is { } kind)
        {
            _draft.MaxPlayers = NetPlayerInfo.ClampPlayers(kind, _draft.MaxPlayers);
        }

        _focusBefore = Page == null ? _host.FocusedRow : _focusBefore;
        _voiceOpen = false;
        Page = hosting != null ? NetInfoPage.Game : NetInfoPage.Player;
        _host.FocusedRow = 0;
    }

    /// <summary>Takes the boxes down without answering, the page's focus put back where it was.
    /// </summary>
    public void Cancel()
    {
        if (!IsOpen)
        {
            return;
        }

        Page = null;
        _voiceOpen = false;
        _done = null;
        _host.FocusedRow = _focusBefore;
    }

    /// <summary>Forgets the boxes, for a page that left the screen under them.</summary>
    public void Drop()
    {
        Page = null;
        _voiceOpen = false;
        _done = null;
    }

    /// <summary>The standing box's rows in the scripts' focus order, or the Voice list's rows
    /// alone while it stands open.</summary>
    public void Rows(List<OriginalRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (Page == NetInfoPage.Game)
        {
            rows.Add(Field(GameNameKey, _draft.GameName, 264f, 176f, true));
            rows.Add(Field(PasswordKey, new string(Mask, _draft.Password.Length), 264f, 244f, true));
            rows.Add(new OriginalRow(PlayersKey, Players(), OriginalRowKind.Dropdown, 264f, 312f, SpinnerWidth, SpinnerHeight, true, 0, null));
            rows.Add(new OriginalRow(MoreKey, string.Empty, OriginalRowKind.Button, 313f, 313f, SpinnerArrowWidth, SpinnerArrowHeight,
                _draft.MaxPlayers < MaxPlayers(), 0, new BoardArt(BoardArtLibrary.Ui, UpArt, 4)));
            rows.Add(new OriginalRow(FewerKey, string.Empty, OriginalRowKind.Button, 313f, 324f, SpinnerArrowWidth, SpinnerArrowHeight,
                _draft.MaxPlayers > NetPlayerInfo.MinPlayers, 0, new BoardArt(BoardArtLibrary.Ui, DownArt, 4)));
            rows.Add(_text.Strip(OkKey, SmallArt, OkX, ButtonY, _draft.GameName.Length > 0, 0, 74f, 37f));
            rows.Add(_text.Strip(CancelKey, MediumArt, CancelX, ButtonY, true, 0, 96f, 37f));
            return;
        }

        if (Page != NetInfoPage.Player)
        {
            return;
        }

        if (_voiceOpen)
        {
            for (int i = 0; i < PilotVoices.All.Count; i++)
            {
                rows.Add(new OriginalRow($"{VoiceKey}:{i.ToString(CultureInfo.InvariantCulture)}", VoiceWord(i),
                    OriginalRowKind.ListRow, 261f, 248f + (FieldHeight * (i + 1)), FieldWidth, FieldHeight, true, 0, null));
            }

            return;
        }

        rows.Add(Field(CallsignKey, _draft.Callsign, 262f, 180f, true));
        rows.Add(new OriginalRow(VoiceKey, VoiceWord(_draft.Voice), OriginalRowKind.Dropdown, 261f, 248f, FieldWidth, FieldHeight, true, 0, null));
        rows.Add(Field(PlayerPasswordKey, JoinPasswordLive ? new string(Mask, _draft.Password.Length) : string.Empty, 261f, 316f,
            JoinPasswordLive));
        rows.Add(_text.Strip(OkKey, SmallArt, OkX, ButtonY, _draft.Callsign.Length > 0, 0, 74f, 37f));
        rows.Add(_text.Strip(CancelKey, MediumArt, CancelX, ButtonY, true, 0, 96f, 37f));
    }

    /// <summary>The boxes' answer to one of their rows.</summary>
    public void Activate(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.StartsWith(VoiceKey + ":", StringComparison.Ordinal))
        {
            if (int.TryParse(key.AsSpan(VoiceKey.Length + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int voice))
            {
                _draft.Voice = PilotVoices.Clamp(voice);
            }

            CloseDropdown();
            return;
        }

        switch (key)
        {
            case MoreKey:
                Spin(1);
                break;
            case FewerKey:
                Spin(-1);
                break;
            case VoiceKey:
                _voiceOpen = true;
                _host.FocusedRow = PilotVoices.Clamp(_draft.Voice);
                break;
            case GameNameKey:
            case PasswordKey:
            case CallsignKey:
            case PlayerPasswordKey:
            case OkKey:
                Ok();
                break;
            case CancelKey:
                Cancel();
                break;
        }
    }

    /// <summary>Back closes the Voice list, and otherwise is Cancel.</summary>
    public void Back()
    {
        if (!CloseDropdown())
        {
            Cancel();
        }
    }

    /// <summary>A sideways step on the spinner moves the cap, and on the Voice box picks the next
    /// voice. False on any other row.</summary>
    public bool StepSideways(IReadOnlyList<OriginalRow> rows, int focus, int direction)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (!IsOpen || _voiceOpen || focus < 0 || focus >= rows.Count || direction == 0)
        {
            return false;
        }

        switch (rows[focus].Key)
        {
            case PlayersKey:
                Spin(Math.Sign(direction));
                return true;
            case VoiceKey:
                int count = PilotVoices.All.Count;
                _draft.Voice = (((_draft.Voice + Math.Sign(direction)) % count) + count) % count;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Closes the Voice list onto its box. False while it is shut.</summary>
    public bool CloseDropdown()
    {
        if (!_voiceOpen)
        {
            return false;
        }

        _voiceOpen = false;
        _host.FocusKey(VoiceKey);
        return true;
    }

    /// <summary>Typed characters, a paste and Backspace into the focused edit box, each character
    /// cueing the box's keystroke, or its reject past the box's limit.</summary>
    public bool TypeText(MenuCommands commands, List<string> cues)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(cues);
        if (!CapturingText || (commands.Typed.Length == 0 && !commands.Erase && !commands.Paste))
        {
            return false;
        }

        string key = _host.FocusedKey;
        string text = key switch
        {
            GameNameKey => _draft.GameName,
            PasswordKey or PlayerPasswordKey => _draft.Password,
            _ => _draft.Callsign,
        };
        int limit = key switch
        {
            GameNameKey => NetPlayerInfo.GameNameLimit,
            PasswordKey or PlayerPasswordKey => NetPlayerInfo.PasswordLimit,
            _ => NetPlayerInfo.CallsignLimit,
        };

        string before = text;
        string typed = commands.Typed + (commands.Paste ? (MenuInput.Clipboard() ?? string.Empty).Trim() : string.Empty);
        foreach (char c in typed)
        {
            bool takes = NetPlayerInfo.Takes(c) && text.Length < limit;
            text += takes ? c.ToString() : string.Empty;
            cues.Add(takes ? OriginalCues.Text : OriginalCues.TextError);
        }

        if (commands.Erase && text.Length > 0)
        {
            text = text[..^1];
        }

        switch (key)
        {
            case GameNameKey:
                _draft.GameName = text;
                break;
            case PasswordKey:
            case PlayerPasswordKey:
                _draft.Password = text;
                break;
            default:
                _draft.Callsign = text;
                break;
        }

        return text != before;
    }

    /// <summary>The standing box over the page, with its art, its words, its boxes in their state
    /// and the open Voice list. <paramref name="rows"/> are the box's own. A focus of -1 marks
    /// nothing, which is how it draws under a messagebox.</summary>
    public void Compose(IReadOnlyList<OriginalRow> rows, int focus, BoardLayers layers)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(layers);
        if (Page is not { } page)
        {
            return;
        }

        string focused = focus >= 0 && focus < rows.Count ? rows[focus].Key : string.Empty;
        int pressedAt = _host.DialogOpen ? -1 : _host.PressedRow;
        string pressed = pressedAt >= 0 && pressedAt < rows.Count ? rows[pressedAt].Key : string.Empty;
        var fills = new List<BoardFill>();
        var pictures = new List<BoardPicture>();
        var lines = new List<BoardLine>();
        var marks = new List<BoardFill>();
        bool game = page == NetInfoPage.Game;

        // The panel's art goes under everything else the box draws, the greyed box's fill included.
        layers.Overlays.Add(new BoardPanel(Array.Empty<BoardFill>(),
            new[] { new BoardPicture(new BoardArt(BoardArtLibrary.Ui, game ? GameArt : PlayerArt), game ? 234f : 231f, game ? 99f : 105f) },
            Array.Empty<BoardLine>()));
        lines.Add(_text.Line(game ? 10032 : 10036, game ? "GAME INFORMATION" : "PLAYER INFORMATION", 260f, game ? 104f : 108f, 0f, Black));

        // The page's rows are built again closed, so the list stands over the boxes as they are.
        var closed = new List<OriginalRow>();
        bool open = _voiceOpen;
        _voiceOpen = false;
        Rows(closed);
        _voiceOpen = open;
        foreach (var row in closed)
        {
            bool isFocused = !open && row.Key == focused;
            ComposeRow(row, isFocused, !open && row.Key == pressed, fills, pictures, lines);
            if (isFocused && row.Kind is OriginalRowKind.Dropdown && row.Enabled)
            {
                marks.Add(_host.FocusMark(row));
            }
        }

        layers.Overlays.Add(new BoardPanel(fills, pictures, lines));
        if (marks.Count > 0)
        {
            layers.Overlays.Add(new BoardPanel(marks, Array.Empty<BoardPicture>(), Array.Empty<BoardLine>()));
        }

        if (open)
        {
            ComposeList(rows, focus, layers);
        }
    }

    private static int MaxPlayersFor(NetSessionKind? kind) => NetPlayerInfo.PlayerCap(kind ?? NetSessionKind.Dogfight);

    private static OriginalRow Field(string key, string text, float x, float y, bool enabled) =>
        new(key, text, OriginalRowKind.TextField, x, y, FieldWidth, FieldHeight, enabled, 0, null);

    private int MaxPlayers() => MaxPlayersFor(_hosting);

    private string Players() => _draft.MaxPlayers.ToString(CultureInfo.InvariantCulture);

    private string VoiceWord(int index)
    {
        var voice = PilotVoices.All[PilotVoices.Clamp(index)];
        return _text.Word(voice.StringId, voice.Name);
    }

    private void Spin(int by)
    {
        if (_hosting is { } kind)
        {
            _draft.MaxPlayers = NetPlayerInfo.ClampPlayers(kind, _draft.MaxPlayers + by);
        }
    }

    // The script's own order. A name of spaces alone raises the refusal and leaves the box open.
    // Game Information's OK hands on to Player Information, and that box's OK is the answer.
    private void Ok()
    {
        if (Page == NetInfoPage.Game)
        {
            if (_draft.GameName.Length == 0)
            {
                return;
            }

            if (!NetPlayerInfo.IsValidName(_draft.GameName))
            {
                _draft.GameName = string.Empty;
                Refuse(10511, "Invalid game name.");
                return;
            }

            Page = NetInfoPage.Player;
            _host.FocusedRow = 0;
            return;
        }

        if (_draft.Callsign.Length == 0)
        {
            return;
        }

        if (!NetPlayerInfo.IsValidName(_draft.Callsign))
        {
            _draft.Callsign = string.Empty;
            Refuse(10510, "Invalid Callsign.");
            return;
        }

        var answer = _draft.Copy();
        var done = _done;
        bool game = _hosting != null;
        Cancel();
        _remember(answer, game);
        done?.Invoke(answer);
    }

    private void Refuse(int id, string fallback) =>
        _host.RaiseDialog(_text.Word(id, fallback), DialogIcon.Warning,
            new OriginalDialogAnswer(OriginalShell.DialogOkKey, CampaignBoards.DialogCenterKey, _text.Word(100, "OK"), null));

    private void ComposeRow(
        OriginalRow row, bool focused, bool pressed, List<BoardFill> fills, List<BoardPicture> pictures, List<BoardLine> lines)
    {
        switch (row.Key)
        {
            case GameNameKey:
            case PasswordKey:
            case CallsignKey:
            case PlayerPasswordKey:
                ComposeField(row, focused, fills, lines);
                break;
            case PlayersKey:
                fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, DisabledFill.R, DisabledFill.G, DisabledFill.B));
                fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 0, 0, 0, Border: true));
                lines.Add(_text.Line(10530, string.Empty, row.X, row.Y + 5f, row.Width, Black, BoardJustify.Center, row.Label));
                lines.Add(_text.Line(10029, "Maximum # of Players", row.X + LabelDx, row.Y + LabelDy, 0f, Black));
                break;
            case MoreKey:
            case FewerKey:
                pictures.Add(new BoardPicture(row.Art!, row.X, row.Y, !row.Enabled ? 0 : pressed ? 3 : focused ? 2 : 1));
                break;
            case VoiceKey:
                pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, ListArrowArt, 4), row.X + row.Width - ArrowSize, row.Y,
                    pressed ? 3 : focused ? 2 : 1));
                lines.Add(_text.Line(10039, string.Empty, row.X + VoiceTextDx, row.Y + TextDy, row.Width - ArrowSize - VoiceTextDx, Black,
                    text: row.Label));
                lines.Add(_text.Line(10034, "Voice", row.X + LabelDx, row.Y + LabelDy, 0f, Black));
                break;
            case OkKey:
            case CancelKey:
                ComposeButton(row, focused, pressed, pictures, lines);
                break;
        }
    }

    // An edit box draws only its words and caret, the box itself being the panel's art. A greyed
    // box takes the script's greyed fill over the art.
    private void ComposeField(OriginalRow row, bool focused, List<BoardFill> fills, List<BoardLine> lines)
    {
        var (id, word, face) = row.Key switch
        {
            GameNameKey => (10027, "Game Name", 10523),
            PasswordKey => (10028, "Password (optional)", 10567),
            CallsignKey => (10033, "Callsign", 10526),
            _ => (10035, "Password", 10568),
        };

        // The greyed box keeps a black label, as the original's Player Information draws it.
        lines.Add(_text.Line(id, word, row.X + LabelDx, row.Y + LabelDy, 0f, Black));
        if (!row.Enabled)
        {
            fills.Add(new BoardFill(row.X + 1f, row.Y + 1f, row.Width - 2f, row.Height - 2f, DisabledFill.R, DisabledFill.G, DisabledFill.B));
            return;
        }

        var caret = focused && !_host.DialogOpen ? new BoardCaret(0, 0, 0, 1f, row.Height - 8f) : (BoardCaret?)null;
        var regular = _text.Regular(face);
        lines.Add(new BoardLine(row.Label, row.X + TextDx, row.Y + TextDy, row.Width - (2f * TextDx),
            regular?.Pixels ?? MultiplayerBoardText.TextFallback, BoardInk.Row, -1, Caret: caret, Face: regular, Colour: Black)
        {
            KeepEnd = true,
        });
    }

    private void ComposeButton(OriginalRow row, bool focused, bool pressed, List<BoardPicture> pictures, List<BoardLine> lines)
    {
        int frame = row.Enabled ? ComposedBoard.PlaqueFrame(row.Art!.Frames, focused, pressed) : 0;
        pictures.Add(new BoardPicture(row.Art!, row.X, row.Y, frame));
        bool ok = row.Key == OkKey;
        bool game = Page == NetInfoPage.Game;
        int id = ok ? (game ? 10030 : 10037) : (game ? 10031 : 10038);
        var face = _text.Regular(id);
        float size = face?.Pixels ?? MultiplayerBoardText.TextFallback;
        lines.Add(new BoardLine(_text.Word(id, ok ? "OK" : "Cancel"), row.X, row.Y + ((row.Height - size) / 2f) - 1f, row.Width, size,
            BoardInk.Row, -1, Justify: BoardJustify.Center, Face: face, Colour: MultiplayerBoardText.LabelTint(row.Enabled, focused, pressed)));
    }

    // The open Voice list under its box: every voice on the list's fill, the picked one and the
    // one under the cursor marked.
    private void ComposeList(IReadOnlyList<OriginalRow> rows, int focus, BoardLayers layers)
    {
        var fills = new List<BoardFill>();
        var lines = new List<BoardLine>();
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var fill = i == focus ? ItemFocused : i == _draft.Voice ? ItemPicked : ItemFill;
            fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, fill.R, fill.G, fill.B));
            lines.Add(_text.Line(10039 + i, row.Label, row.X + VoiceTextDx, row.Y + TextDy, row.Width - VoiceTextDx, Black));
        }

        if (rows.Count > 0)
        {
            fills.Add(new BoardFill(rows[0].X, rows[0].Y, rows[0].Width, rows[^1].Y + rows[^1].Height - rows[0].Y, 0, 0, 0, Border: true));
        }

        layers.Overlays.Add(new BoardPanel(fills, Array.Empty<BoardPicture>(), lines));
    }
}
