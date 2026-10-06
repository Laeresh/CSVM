using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Flight.Hangar;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session.Roster;
using CSVM.UI.Boards;
using CSVM.UI.Hangar;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The join board's bot rows, drawn on its right page. Under the articles stands the Bots block:
/// Add Bot, Fill to and its count, then the rows in two columns. While a row is picked, the Edit
/// Bot panel stands in the articles' place (Callsign, Plane, Skill, Remove, Accept). The rows are
/// the shared player setup's (<see cref="PlayerSetupFeature.Bots"/>), so the Dogfight screen counts
/// them and its launch carries them. The words, boxes and lists take the Multiplayer Lobby's bot
/// faces. A local Dogfight has no teams, so the panel offers none.
/// </summary>
internal sealed class OriginalBotPanel
{
    // The lobby's faces: the Mission Options dropdowns' for boxes, lists and rows, their titles' for
    // labels. The section headings' face titles the block, and the type description's its hint.
    private const int BoxFace = 10558;
    private const int LabelFace = 10096;
    private const int HeadingFace = 10099;
    private const int HintFace = 10123;
    private const int EditorFace = 10114;

    private const string ArrowArt = "MP_B_LISTBOXARROW.PNG";
    private const string UpArt = "MP_B_SCROLLUP.PNG";
    private const string DownArt = "MP_B_SCROLLDOWN.PNG";
    private const string IconArt = "MP_PLANEICONSTOPFRONT.PNG";
    private const int IconFrames = 11;
    private const float DisabledArrow = 0.45f;
    private const float FallbackText = 12f;

    // The right page's column, the articles' own left edge.
    private const float PageX = 440f;

    // The Bots block: its title and hint, the plaques with the count box, then the rows.
    private const float BlockY = 280f;
    private const float HintY = 300f;
    private const float ControlsY = 318f;
    private const float PlaqueGap = 6f;
    private const float CountWidth = 30f;
    private const float RowsY = 356f;
    private const float RowPitch = 14f;
    private const float RowWidth = 150f;
    private const float SecondColumnX = 600f;
    private const int RowsPerColumn = 8;

    // The Edit Bot panel: the heading over three labelled boxes, Remove and Accept, then the plane.
    private const float EditorY = 82f;
    private const float FieldTop = 106f;
    private const float FieldPitch = 46f;
    private const float FieldWidth = 200f;
    private const float FieldHeight = 22f;
    private const float LabelLift = 18f;
    private const float EditorPlaqueY = 250f;
    private const float PlaneY = 292f;
    private const float IconY = 326f;
    private const float RatingsY = 366f;
    private const float RatingPitch = 15f;

    private static readonly BoardTint Black = new(0, 0, 0);
    private static readonly BoardTint Greyed = new(128, 128, 128);
    private static readonly string[] RatingLabels = { "TOP SPEED:", "ARMOR:", "AGILITY:", "OFFENSE:" };
    private static readonly string[] RatingWords = { "Poor", "Fair", "Average", "Good", "Excellent" };

    // The picked row's fill and a box's own, the Multiplayer Lobby's.
    private static readonly (byte R, byte G, byte B) PickedRow = (209, 180, 120);
    private static readonly (byte R, byte G, byte B) BoxFill = (222, 207, 156);
    private static readonly (byte R, byte G, byte B) EditFill = (209, 180, 120);
    private static readonly (byte R, byte G, byte B) GreyFill = (181, 174, 156);
    private static readonly (byte R, byte G, byte B) ListPicked = (180, 147, 78);

    private readonly IOriginalScreenHost _host;
    private readonly PlayerSetupFeature _setup;
    private readonly MultiplayerBoardText _text;
    private readonly Func<StockLoadouts?> _stock;
    private readonly Func<string, OriginalRow> _plaque;
    private readonly string? _dataRoot;
    private bool _poolRead;
    private int _picked = -1;
    private int _fillTo = DogfightLobby.DefaultFillTo;
    private string? _open;
    private int _listTop;
    private string? _typing;
    private string _draft = string.Empty;

    /// <summary>A panel over <paramref name="setup"/>'s bot rows, its plaques built by
    /// <paramref name="plaque"/> (the board's own paper strip, placed by the caller). The words and
    /// pilot names come from the tables under <paramref name="dataRoot"/>.</summary>
    internal OriginalBotPanel(IOriginalScreenHost host, PlayerSetupFeature setup, string? dataRoot,
        Func<StockLoadouts?>? stock, Func<string, OriginalRow> plaque)
    {
        _host = host;
        _setup = setup;
        _dataRoot = dataRoot;
        _text = new MultiplayerBoardText(host, dataRoot);
        _stock = stock ?? (() => null);
        _plaque = plaque;
    }

    /// <summary>The id of the picked row, which the Edit Bot panel stands on, or -1.</summary>
    internal int Picked => _setup.Bots.ById(_picked) != null ? _picked : -1;

    /// <summary>The pilots Fill to fills the field to, its count box's value.</summary>
    internal int FillCount => _fillTo;

    /// <summary>The dropdown whose list stands open, or null.</summary>
    internal string? OpenDropdown => _open;

    /// <summary>Whether seat 0's typed characters feed the callsign or the count box.</summary>
    internal bool CapturingText =>
        _host.Screen == OriginalScreen.JoinBoard && !_host.DialogOpen && _open == null && IsBox(_host.FocusedKey);

    /// <summary>The row key of the bot listed <paramref name="place"/>-th.</summary>
    internal static string RowKey(int place) => OriginalJoinBoard.BotRowPrefix + place.ToString(CultureInfo.InvariantCulture);

    /// <summary>Lets the picked row and any open list go, as a door onto the board does.</summary>
    internal void Reset()
    {
        _picked = -1;
        _open = null;
        _typing = null;
    }

    /// <summary>Picks row <paramref name="id"/> and stands the Edit Bot panel on it, as a press on
    /// its row does; nothing for a row not there.</summary>
    internal void Pick(int id)
    {
        _open = null;
        _typing = null;
        _picked = _setup.Bots.ById(id) != null ? id : -1;
        if (_picked >= 0)
        {
            _host.FocusKey(OriginalJoinBoard.BotNameKey);
        }
    }

    /// <summary>Five bot rows for a screenshot, the second on a stock Fury at ace and the third at
    /// novice so the rows read apart. Answers the second's id, or -1 on a field with no room.
    /// </summary>
    internal int PoseRows()
    {
        PoolOnce();
        _setup.Bots.Clear();
        for (int i = 0; i < 5; i++)
        {
            _setup.AddBot();
        }

        var rows = _setup.Bots.Rows;
        if (rows.Count < 3)
        {
            return -1;
        }

        _setup.Bots.SetAirframe(rows[1].Id, StockAirframes.IdOf("player_fury") ?? 0);
        _setup.Bots.SetSkill(rows[1].Id, NetBotSkill.Ace);
        _setup.Bots.SetSkill(rows[2].Id, NetBotSkill.Novice);
        return rows[1].Id;
    }

    /// <summary>The panel's rows in focus order, or the open list's items alone while one stands.
    /// </summary>
    internal bool BuildRows(List<OriginalRow> rows)
    {
        if (OpenList() is { } drop)
        {
            _listTop = OriginalDropLists.Top(drop, _listTop, _host.FocusedRow);
            OriginalDropLists.AddRows(drop, _listTop, rows,
                (art, width, height) => OriginalWidgets.StripSize(art, _host.Measure, width, height));
            return true;
        }

        Widgets(rows);
        return false;
    }

    /// <summary>A sideways step on a live dropdown picks its next value.</summary>
    internal bool StepSideways(IReadOnlyList<OriginalRow> rows, int focus, int direction)
    {
        if (_open != null || focus < 0 || focus >= rows.Count || direction == 0)
        {
            return false;
        }

        var row = rows[focus];
        if (row.Kind != OriginalRowKind.Dropdown || !row.Enabled || DropdownFor(row.Key) is not { } list)
        {
            return false;
        }

        int count = list.Items.Count;
        list.Select(((list.Current + Math.Sign(direction)) % count + count) % count);
        return true;
    }

    /// <summary>Closes the open list and puts the focus back on its box.</summary>
    internal bool CloseDropdown()
    {
        if (_open is not { } key)
        {
            return false;
        }

        _open = null;
        _host.FocusKey(key);
        return true;
    }

    /// <summary>Back: an open list closes, then the picked row is let go. False with neither, so
    /// the board's own Back leaves it.</summary>
    internal bool Back()
    {
        if (CloseDropdown())
        {
            return true;
        }

        if (Picked < 0)
        {
            return false;
        }

        _picked = -1;
        _host.FocusKey(OriginalJoinBoard.AddBotKey);
        return true;
    }

    /// <summary>The panel's answer to one of its rows; false for a row that is not its own.</summary>
    internal bool Activate(OriginalRow row)
    {
        _typing = null;
        if (row.Key.IndexOf(':', StringComparison.Ordinal) is var colon and > 0)
        {
            PickFromList(row.Key[..colon], row.Key[(colon + 1)..]);
            return true;
        }

        if (OriginalWidgets.Indexed(row.Key, OriginalJoinBoard.BotRowPrefix) is { } place)
        {
            var rows = _setup.Bots.Rows;
            if (place >= 0 && place < rows.Count)
            {
                int id = rows[place].Id;
                if (id == Picked)
                {
                    _picked = -1;
                }
                else
                {
                    Pick(id);
                }
            }

            return true;
        }

        if (row.Key.StartsWith(OriginalJoinBoard.FillArrowPrefix, StringComparison.Ordinal))
        {
            _fillTo = Math.Clamp(_fillTo + (row.Key.EndsWith('+') ? 1 : -1), 2, NetSeats.MaxPlayers);
            return true;
        }

        switch (row.Key)
        {
            case OriginalJoinBoard.AddBotKey:
                PoolOnce();
                _setup.AddBot();
                return true;
            case OriginalJoinBoard.FillKey:
                PoolOnce();
                _setup.FillBots(_fillTo);
                return true;
            case OriginalJoinBoard.FillCountKey:
            case OriginalJoinBoard.BotNameKey:
                return true;
            case OriginalJoinBoard.RemoveBotKey:
                _setup.Bots.Remove(Picked);
                _picked = -1;
                _host.FocusKey(OriginalJoinBoard.AddBotKey);
                return true;
            case OriginalJoinBoard.BotDoneKey:
                _picked = -1;
                _host.FocusKey(OriginalJoinBoard.AddBotKey);
                return true;
        }

        if (row.Kind == OriginalRowKind.Dropdown && DropdownFor(row.Key) is { } list)
        {
            _open = row.Key;
            _listTop = 0;
            _host.FocusedRow = Math.Max(0, list.Current);
            return true;
        }

        return false;
    }

    /// <summary>Typed characters and Backspace into the focused box. The callsign keeps a draft and
    /// sets each name the rows take as it is typed; the count takes digits within 2 to 16.</summary>
    internal bool TypeText(MenuCommands commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (!CapturingText || (commands.Typed.Length == 0 && !commands.Erase))
        {
            return false;
        }

        string key = _host.FocusedKey;
        bool named = key == OriginalJoinBoard.BotNameKey;
        string draft = _typing == key ? _draft : BoxValue(key);
        int width = named ? BotSeats.CallsignLimit : 2;
        foreach (char c in commands.Typed)
        {
            bool takes = named ? c is >= ' ' and < (char)127 : char.IsAsciiDigit(c);
            if (takes && draft.Length < width)
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
        if (named)
        {
            _setup.RenameBot(Picked, draft);
        }
        else if (int.TryParse(draft, NumberStyles.None, CultureInfo.InvariantCulture, out int pilots))
        {
            _fillTo = Math.Clamp(pilots, 2, NetSeats.MaxPlayers);
        }

        return true;
    }

    /// <summary>The panel as drawn: the Bots block, or the Edit Bot panel while a row is picked,
    /// with an open list over it.</summary>
    internal void Compose(IReadOnlyList<OriginalRow> rows, int focus, BoardLayers layers)
    {
        var widgets = new List<OriginalRow>();
        Widgets(widgets);
        string focused = _open ?? (focus >= 0 && focus < rows.Count && !_host.DialogOpen ? rows[focus].Key : string.Empty);
        int pressedAt = _host.DialogOpen || _open != null ? -1 : _host.PressedRow;
        string pressed = pressedAt >= 0 && pressedAt < rows.Count ? rows[pressedAt].Key : string.Empty;
        if (_setup.Bots.ById(Picked) is { } bot)
        {
            ComposeEditor(bot, layers);
        }
        else
        {
            ComposeBlock(layers);
        }

        foreach (var row in widgets)
        {
            bool on = row.Key == focused;
            switch (row.Kind)
            {
                case OriginalRowKind.Dropdown:
                    ComposeDropdown(row, on, layers);
                    break;
                case OriginalRowKind.TextField:
                    ComposeBox(row, on, layers);
                    break;
                case OriginalRowKind.ListRow:
                    ComposeBotRow(row, on, layers);
                    break;
                default:
                    int index = -1;
                    for (int i = 0; i < rows.Count && index < 0; i++)
                    {
                        index = rows[i].Key == row.Key ? i : -1;
                    }

                    _host.ComposeGenericRow(row, on, row.Key == pressed, index, layers);
                    break;
            }
        }

        if (_open != null)
        {
            ComposeOpenList(rows, focus, layers);
        }
    }

    // IDS_IA_DIFFICULTY's three words for the tiers, first letter raised as the lobby's are.
    private static string SkillWord(MultiplayerBoardText text, NetBotSkill skill)
    {
        string word = skill switch
        {
            NetBotSkill.Novice => text.Word(3695, "novice"),
            NetBotSkill.Ace => text.Word(3697, "ace"),
            _ => text.Word(3696, "veteran"),
        };

        return word.Length > 0 ? char.ToUpperInvariant(word[0]) + word[1..] : word;
    }

    // A stock airframe's name as the sortie screens list it.
    private static string AirframeName(int airframe)
    {
        string node = StockAirframes.Node(airframe);
        foreach (var row in OriginalRosters.Airframes)
        {
            if (row.Node == node)
            {
                return row.Name;
            }
        }

        return node;
    }

    private static bool IsBox(string key) => key is OriginalJoinBoard.BotNameKey or OriginalJoinBoard.FillCountKey;

    private static OriginalRow Arrow(string key, string art, float x, float y, bool enabled) =>
        new(key, string.Empty, OriginalRowKind.Button, x, y, 16f, 11f, enabled, 0, new BoardArt(BoardArtLibrary.Ui, art, 4));

    // No langui row words a Random plane, so it is the remake's own.
    private static string RandomWord() => "Random";

    private string PlaneWord(int airframe) => _text.Word(10565, "Stock") + " " + AirframeName(airframe);

    // The shipped pilot names, read once off the message table under the data root before the
    // first row draws one. A missing table leaves every bot "Bot n".
    private void PoolOnce()
    {
        if (_poolRead)
        {
            return;
        }

        _poolRead = true;
        if (_dataRoot != null && _setup.Bots.CallsignPool.Count == 0)
        {
            _setup.Bots.CallsignPool = BotSeats.CallsignPool(Messages.Load(System.IO.Path.Combine(_dataRoot, "extracted", "messages.json")));
        }
    }

    private string BoxValue(string key) => key == OriginalJoinBoard.BotNameKey
        ? _setup.Bots.ById(Picked)?.Callsign ?? string.Empty
        : _fillTo.ToString(CultureInfo.InvariantCulture);

    private string BoxText(string key) => _typing == key ? _draft : BoxValue(key);

    // The panel's rows: the editor's while a row is picked, else the block's controls. Then comes a
    // row per bot, the first column down and then the second.
    private void Widgets(List<OriginalRow> rows)
    {
        if (_setup.Bots.ById(Picked) is { } bot)
        {
            string plane = bot.RandomPlane ? RandomWord() : PlaneWord(bot.Airframe);
            rows.Add(Field(OriginalJoinBoard.BotNameKey, BoxText(OriginalJoinBoard.BotNameKey), 0, OriginalRowKind.TextField));
            rows.Add(Field(OriginalJoinBoard.BotPlaneKey, plane, 1, OriginalRowKind.Dropdown));
            rows.Add(Field(OriginalJoinBoard.BotSkillKey, SkillWord(_text, bot.Skill), 2, OriginalRowKind.Dropdown));
            var remove = _plaque(OriginalJoinBoard.RemoveBotKey) with { X = PageX, Y = EditorPlaqueY };
            rows.Add(remove);
            rows.Add(_plaque(OriginalJoinBoard.BotDoneKey) with { X = PageX + remove.Width + PlaqueGap, Y = EditorPlaqueY });
            return;
        }

        var add = _plaque(OriginalJoinBoard.AddBotKey) with { X = PageX, Y = ControlsY, Enabled = _setup.BotRoom > 0 };
        float fillX = PageX + add.Width + PlaqueGap;
        float countX = fillX + add.Width + PlaqueGap;
        rows.Add(add);
        rows.Add(_plaque(OriginalJoinBoard.FillKey) with { X = fillX, Y = ControlsY, Enabled = _setup.FieldPilots < _fillTo });
        rows.Add(new OriginalRow(OriginalJoinBoard.FillCountKey, BoxText(OriginalJoinBoard.FillCountKey), OriginalRowKind.TextField,
            countX, ControlsY + 3f, CountWidth, FieldHeight, true, 0, null));
        rows.Add(Arrow(OriginalJoinBoard.FillArrowPrefix + "+", UpArt, countX + CountWidth, ControlsY + 3f, _fillTo < NetSeats.MaxPlayers));
        rows.Add(Arrow(OriginalJoinBoard.FillArrowPrefix + "-", DownArt, countX + CountWidth, ControlsY + 14f, _fillTo > 2));
        var bots = _setup.Bots.Rows;
        for (int i = 0; i < bots.Count && i < 2 * RowsPerColumn; i++)
        {
            float x = i < RowsPerColumn ? PageX : SecondColumnX;
            float y = RowsY + ((i % RowsPerColumn) * RowPitch);
            rows.Add(new OriginalRow(RowKey(i), bots[i].Callsign, OriginalRowKind.ListRow, x, y, RowWidth, RowPitch, true, 0, null));
        }
    }

    private OriginalRow Field(string key, string label, int place, OriginalRowKind kind) =>
        new(key, label, kind, PageX, FieldTop + (place * FieldPitch) + LabelLift, FieldWidth, FieldHeight, true, 0, null);

    // The list behind a dropdown: its words, the value it stands on and how a pick is written.
    private BotList? DropdownFor(string key)
    {
        if (_setup.Bots.ById(Picked) is not { } bot)
        {
            return null;
        }

        switch (key)
        {
            case OriginalJoinBoard.BotPlaneKey:
                {
                    var items = new string[DogfightLobby.AirframeCount + 1];
                    items[0] = RandomWord();
                    for (int i = 1; i < items.Length; i++)
                    {
                        items[i] = PlaneWord(i - 1);
                    }

                    return new BotList(items, bot.RandomPlane ? 0 : bot.Airframe + 1,
                        i => _setup.Bots.SetAirframe(bot.Id, i == 0 ? DogfightLobbySeat.RandomAirframe : i - 1));
                }

            case OriginalJoinBoard.BotSkillKey:
                return new BotList(
                    new[] { SkillWord(_text, NetBotSkill.Novice), SkillWord(_text, NetBotSkill.Veteran), SkillWord(_text, NetBotSkill.Ace) },
                    (int)bot.Skill, i => _setup.Bots.SetSkill(bot.Id, (NetBotSkill)i));
        }

        return null;
    }

    private OpenDropList? OpenList()
    {
        if (_open is not { } key || DropdownFor(key) is not { } list)
        {
            return null;
        }

        var closed = new List<OriginalRow>();
        Widgets(closed);
        var box = closed.Find(row => row.Key == key);
        return box == null ? null : OriginalDropLists.Over(key, null, list.Items, (box.X, box.Y, box.Width, box.Height));
    }

    private void PickFromList(string key, string suffix)
    {
        if (DropdownFor(key) is { } list && int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
            && index >= 0 && index < list.Items.Count)
        {
            list.Select(index);
        }

        _open = null;
        _host.FocusKey(key);
    }

    // The block's own words: its title and how to use it, which a bot row's absence changes.
    private void ComposeBlock(BoardLayers layers)
    {
        layers.Lines.Add(_text.Line(-1, "Bots", PageX, BlockY, 0f, Black, faceId: HeadingFace));
        string hint = _setup.Bots.Count > 0 ? "Dogfight only. Pick a bot to edit it." : "Dogfight only. Add one to fly against.";
        layers.Lines.Add(_text.Line(-1, hint, PageX + 50f, BlockY + 3f, 0f, Black, faceId: HintFace));
    }

    // Edit Bot in the articles' place: its labels over the boxes. The page's lower half shows the
    // stock plane's caption, icon and ratings, or says a Random plane is drawn at launch.
    private void ComposeEditor(DogfightBot bot, BoardLayers layers)
    {
        layers.Lines.Add(_text.Line(-1, "Edit Bot", PageX, EditorY, 0f, Black, faceId: EditorFace));
        string[] labels = { "Callsign", "Plane", "Skill" };
        for (int i = 0; i < labels.Length; i++)
        {
            layers.Lines.Add(_text.Line(-1, labels[i], PageX, FieldTop + (i * FieldPitch), 0f, Black, faceId: LabelFace));
        }

        string caption = _text.Word(10566, "Plane:") + " " + (bot.RandomPlane ? RandomWord() : _text.Word(10565, "Stock"));
        layers.Lines.Add(_text.Line(-1, caption, PageX, PlaneY, 0f, Black, faceId: BoxFace));
        if (bot.RandomPlane)
        {
            layers.Lines.Add(_text.Line(-1, "Drawn at launch", PageX, PlaneY + 15f, 0f, Black, faceId: BoxFace));
            return;
        }

        layers.Lines.Add(_text.Line(3000 + bot.Airframe, AirframeName(bot.Airframe), PageX, PlaneY + 15f, 300f, Black, faceId: BoxFace));
        layers.Pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, IconArt, IconFrames), PageX + 20f, IconY, bot.Airframe));
        var ratings = PlaneRatings.For(PlaneFit.For(bot.Airframe, null, _stock()?.ForModel(StockAirframes.Node(bot.Airframe))));
        for (int i = 0; i < RatingLabels.Length; i++)
        {
            int at = Math.Clamp(ratings[i], 0, RatingWords.Length - 1);
            string text = $"{RatingLabels[i]}  {_text.Word(501 + at, RatingWords[at])}";
            layers.Lines.Add(_text.Line(1008, string.Empty, PageX, RatingsY + (RatingPitch * i), 0f, Black, text: text));
        }
    }

    // A bot's row: the picked one filled, the focused one marked, its callsign and its tier.
    private void ComposeBotRow(OriginalRow row, bool focused, BoardLayers layers)
    {
        int place = OriginalWidgets.Indexed(row.Key, OriginalJoinBoard.BotRowPrefix) ?? -1;
        var bots = _setup.Bots.Rows;
        if (place < 0 || place >= bots.Count)
        {
            return;
        }

        if (bots[place].Id == Picked)
        {
            layers.Fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, PickedRow.R, PickedRow.G, PickedRow.B));
        }

        if (focused)
        {
            layers.Fills.Add(_host.FocusMark(row));
        }

        var face = _text.Regular(BoxFace);
        float size = face?.Pixels ?? FallbackText;
        float y = row.Y + ((row.Height - size) / 2f) - 1f;
        layers.Lines.Add(new BoardLine(row.Label, row.X + 2f, y, row.Width - 4f, size, BoardInk.Row, -1, Face: face, Colour: Black));
        layers.Lines.Add(new BoardLine(SkillWord(_text, bots[place].Skill), row.X + 2f, y, row.Width - 6f, size, BoardInk.Row, -1,
            Justify: BoardJustify.Right, Face: face, Colour: Greyed));
    }

    private void ComposeDropdown(OriginalRow row, bool focused, BoardLayers layers)
    {
        var fill = row.Enabled ? BoxFill : GreyFill;
        layers.Fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, fill.R, fill.G, fill.B));
        layers.Fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 0, 0, 0, Border: true));
        if (focused && row.Enabled)
        {
            layers.Fills.Add(_host.FocusMark(row));
        }

        var face = _text.Regular(BoxFace);
        float size = face?.Pixels ?? FallbackText;
        layers.Lines.Add(new BoardLine(row.Label, row.X + 8f, row.Y + ((row.Height - size) / 2f) - 1f, row.Width - 34f, size,
            BoardInk.Row, -1, Face: face, Colour: Black));
        layers.Pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, ArrowArt), row.X + row.Width - 24f, row.Y + 1f,
            Opacity: row.Enabled ? 1f : DisabledArrow));
    }

    private void ComposeBox(OriginalRow row, bool focused, BoardLayers layers)
    {
        var face = _text.Regular(BoxFace);
        float size = face?.Pixels ?? FallbackText;
        var fill = row.Enabled ? EditFill : GreyFill;
        layers.Fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, fill.R, fill.G, fill.B));
        layers.Fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 0, 0, 0, Border: true));
        var caret = focused && row.Enabled && !_host.DialogOpen ? new BoardCaret(0, 0, 0, 1f, row.Height - 4f) : (BoardCaret?)null;
        layers.Lines.Add(new BoardLine(row.Label, row.X + 4f, row.Y + ((row.Height - size) / 2f) - 1f, row.Width - 8f, size,
            BoardInk.Row, -1, Caret: caret, Face: face, Colour: Black));
    }

    // The open list over the finished page: its items on the box's own fill, the picked one marked.
    private void ComposeOpenList(IReadOnlyList<OriginalRow> rows, int focus, BoardLayers layers)
    {
        var fills = new List<BoardFill>();
        var lines = new List<BoardLine>();
        int picked = _open != null && DropdownFor(_open) is { } open ? open.Current : -1;
        var face = _text.Regular(BoxFace);
        float size = face?.Pixels ?? FallbackText;
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
            fills.Add(new BoardFill(left, top, width, bottom - top, BoxFill.R, BoxFill.G, BoxFill.B));
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
                fills.Add(new BoardFill(item.X, item.Y, item.Width, item.Height, ListPicked.R, ListPicked.G, ListPicked.B));
            }
            else if (i == focus)
            {
                fills.Add(new BoardFill(item.X, item.Y, item.Width, item.Height, EditFill.R, EditFill.G, EditFill.B));
            }

            lines.Add(new BoardLine(item.Label, item.X + 8f, item.Y + ((item.Height - size) / 2f) - 1f, item.Width - 12f, size,
                BoardInk.Row, i, Face: face, Colour: Black));
        }

        layers.Overlays.Add(new BoardPanel(fills, Array.Empty<BoardPicture>(), lines));
    }

    private sealed record BotList(IReadOnlyList<string> Items, int Current, Func<int, bool> Pick)
    {
        public void Select(int index) => Pick(index);
    }
}
