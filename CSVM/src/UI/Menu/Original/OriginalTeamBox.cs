using System;
using System.Collections.Generic;
using CSVM.UI.Boards;
using CSVM.UI.Campaign;
using CSVM.UI.Screens;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The original's CREATE TEAM box, in the placements and art of <c>MULTIPLAYERTEAMMODAL.SCRIPT</c>:
/// one Team Name box, OK and Cancel over <c>mp_createteambackground.png</c>. The lobby stands it
/// over its own page. OK is greyed while the box is empty, and a name of spaces alone raises the
/// original's refusal (langui 10512) and empties the box. An accepted name is handed back to the
/// lobby, which creates the team.
/// </summary>
internal sealed class OriginalTeamBox
{
    /// <summary>The Team Name box.</summary>
    public const string NameKey = "MPT_E_NAME";

    /// <summary>OK, greyed while the box is empty.</summary>
    public const string OkKey = "MPT_B_OK";

    /// <summary>Cancel, back to the lobby.</summary>
    public const string CancelKey = "MPT_B_CANCEL";

    /// <summary>The longest name the box takes, the script's <c>DHA.FD</c>.</summary>
    public const int NameLimit = 12;

    private const string Prefix = "MPT_";
    private const string Art = "MP_CREATETEAMBACKGROUND.PNG";
    private const string SmallArt = "MP_B_SMALL.PNG";
    private const string MediumArt = "MP_B_MEDIUM.PNG";

    // The script's corners: the box at 265, 200, 272 by 24. Its label stands 3 left and 25 up, and its
    // text 6 right and 3 down. OK stands at 360, 382 and Cancel at 452, 382.
    private const float FieldX = 265f;
    private const float FieldY = 200f;
    private const float FieldWidth = 272f;
    private const float FieldHeight = 24f;
    private const float ButtonY = 382f;

    private static readonly BoardTint Black = new(0, 0, 0);

    private readonly IOriginalScreenHost _host;
    private readonly MultiplayerBoardText _text;
    private int _focusBefore = -1;

    /// <summary>A box drawn for <paramref name="host"/> in the words of <paramref name="text"/>.
    /// </summary>
    public OriginalTeamBox(IOriginalScreenHost host, MultiplayerBoardText text)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <summary>Whether the box stands.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>The name typed so far.</summary>
    public string Draft { get; private set; } = string.Empty;

    /// <summary>Whether seat 0's typed characters feed the name box.</summary>
    public bool CapturingText => IsOpen && !_host.DialogOpen && _host.FocusedKey == NameKey;

    /// <summary>Whether a key is one of the box's rows.</summary>
    public static bool Owns(string key) => key.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Stands the box with <paramref name="start"/> in it and the focus on it.</summary>
    public void Open(string start)
    {
        Draft = (start ?? string.Empty).Length > NameLimit ? start![..NameLimit] : start ?? string.Empty;
        _focusBefore = IsOpen ? _focusBefore : _host.FocusedRow;
        IsOpen = true;
        _host.FocusedRow = 0;
    }

    /// <summary>Takes the box down, the lobby's focus put back where it was.</summary>
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        _host.FocusedRow = _focusBefore;
    }

    /// <summary>Forgets the box, for a lobby that left the screen under it.</summary>
    public void Drop() => IsOpen = false;

    /// <summary>The box's rows in the script's focus order.</summary>
    public void Rows(List<OriginalRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        rows.Add(new OriginalRow(NameKey, Draft, OriginalRowKind.TextField, FieldX, FieldY, FieldWidth, FieldHeight, true, 0, null));
        rows.Add(_text.Strip(OkKey, SmallArt, 360f, ButtonY, Draft.Length > 0, 0, 74f, 37f));
        rows.Add(_text.Strip(CancelKey, MediumArt, 452f, ButtonY, true, 0, 96f, 37f));
    }

    /// <summary>The box's answer to one of its rows: the name to create on an accepted OK, else
    /// null. The name box's own press is OK, as the script's 9935 is.</summary>
    public string? Activate(string key)
    {
        if (key == CancelKey)
        {
            Close();
            return null;
        }

        if (key is not (OkKey or NameKey) || Draft.Length == 0)
        {
            return null;
        }

        if (!NetPlayerInfo.IsValidName(Draft))
        {
            Draft = string.Empty;
            _host.RaiseDialog(_text.Word(10512, "Invalid team name."), DialogIcon.Warning,
                new OriginalDialogAnswer(OriginalShell.DialogOkKey, CampaignBoards.DialogCenterKey, _text.Word(100, "OK"), null));
            return null;
        }

        string name = Draft;
        Close();
        return name;
    }

    /// <summary>Typed characters, a paste and Backspace into the name box, each character cueing
    /// the box's keystroke, or its reject past the box's limit.</summary>
    public bool TypeText(MenuCommands commands, List<string> cues)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(cues);
        if (!CapturingText || (commands.Typed.Length == 0 && !commands.Erase && !commands.Paste))
        {
            return false;
        }

        string before = Draft;
        string text = Draft;
        string typed = commands.Typed + (commands.Paste ? (MenuInput.Clipboard() ?? string.Empty).Trim() : string.Empty);
        foreach (char c in typed)
        {
            bool takes = NetPlayerInfo.Takes(c) && text.Length < NameLimit;
            text += takes ? c.ToString() : string.Empty;
            cues.Add(takes ? OriginalCues.Text : OriginalCues.TextError);
        }

        if (commands.Erase && text.Length > 0)
        {
            text = text[..^1];
        }

        Draft = text;
        return text != before;
    }

    /// <summary>The standing box over the lobby: its art, title, label, name and buttons. A focus
    /// of -1 marks nothing, which is how it draws under a messagebox.</summary>
    public void Compose(IReadOnlyList<OriginalRow> rows, int focus, BoardLayers layers)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(layers);
        if (!IsOpen)
        {
            return;
        }

        string focused = focus >= 0 && focus < rows.Count ? rows[focus].Key : string.Empty;
        int pressedAt = _host.DialogOpen ? -1 : _host.PressedRow;
        string pressed = pressedAt >= 0 && pressedAt < rows.Count ? rows[pressedAt].Key : string.Empty;
        layers.Overlays.Add(new BoardPanel(Array.Empty<BoardFill>(),
            new[] { new BoardPicture(new BoardArt(BoardArtLibrary.Ui, Art), 234f, 99f) }, Array.Empty<BoardLine>()));

        var pictures = new List<BoardPicture>();
        var lines = new List<BoardLine>
        {
            _text.Line(10552, "CREATE TEAM", 371f, 130f, 0f, Black),
            _text.Line(10551, "Team Name:", FieldX - 3f, FieldY - 25f, 0f, Black),
        };

        var own = new List<OriginalRow>();
        Rows(own);
        foreach (var row in own)
        {
            if (row.Key == NameKey)
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

            bool isFocused = row.Key == focused;
            bool isPressed = row.Key == pressed;
            int frame = row.Enabled ? ComposedBoard.PlaqueFrame(row.Art!.Frames, isFocused, isPressed) : 0;
            pictures.Add(new BoardPicture(row.Art!, row.X, row.Y, frame));
            bool ok = row.Key == OkKey;
            int id = ok ? 10534 : 10535;
            var plaque = _text.Regular(id);
            float size = plaque?.Pixels ?? MultiplayerBoardText.TextFallback;
            lines.Add(new BoardLine(_text.Word(id, ok ? "OK" : "Cancel"), row.X, row.Y + ((row.Height - size) / 2f) - 1f, row.Width, size,
                BoardInk.Row, -1, Justify: BoardJustify.Center, Face: plaque,
                Colour: MultiplayerBoardText.LabelTint(row.Enabled, isFocused, isPressed)));
        }

        layers.Overlays.Add(new BoardPanel(Array.Empty<BoardFill>(), pictures, lines));
    }
}
