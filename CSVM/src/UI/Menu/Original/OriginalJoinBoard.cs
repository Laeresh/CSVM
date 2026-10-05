using System;
using System.Collections.Generic;
using CSVM.UI.Boards;
using CSVM.UI.Screens;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The join board, the one screen a pad signs onto a seat from, drawn as the open scrapbook.
/// The left page holds the CREW MANIFEST, an entry per seat; the right, the ARTICLES OF THE CREW
/// and a Dogfight's bot rows (<see cref="OriginalBotPanel"/>). A signs on, B on a seated pad gives
/// that seat up, and the captain's Start casts off. The three are read raw off the devices by
/// <see cref="IJoinRoster"/>, since a pad with no seat has no commands to read. The keyboard is
/// never listed: it holds seat 1 whatever the manifest says, and the first pad to sign on shares
/// that seat. An entry is a pad's claim, not a player's existence, and the roster every later
/// screen reads is the one this board writes.
/// </summary>
public sealed class OriginalJoinBoard : IOriginalScreenModule
{
    /// <summary>The way on with the manifest as it stands, the plaque the keyboard and the mouse
    /// press and the captain's Start reaches.</summary>
    public const string ContinueKey = "JB_CONTINUE";

    /// <summary>The way out, which signs everyone off again.</summary>
    public const string BackKey = "JB_BACK";

    /// <summary>Add Bot: one bot row (<see cref="PlayerSetupFeature.AddBot"/>).</summary>
    public const string AddBotKey = "JB_BOT_ADD";

    /// <summary>Fill to: bot rows until the field holds the count box's pilots.</summary>
    public const string FillKey = "JB_BOT_FILL";

    /// <summary>Fill to's count box, a total of pilots, seats and bots together.</summary>
    public const string FillCountKey = "JB_BOT_E_FILL";

    /// <summary>The prefix of the count box's arrows: <c>+</c> and <c>-</c> follow it.</summary>
    public const string FillArrowPrefix = "JB_BOT_FILL_";

    /// <summary>The prefix of a bot's row, followed by its place in the list, which a press picks
    /// for the Edit Bot panel.</summary>
    public const string BotRowPrefix = "JB_BOT_ROW_";

    /// <summary>The picked bot's Callsign box.</summary>
    public const string BotNameKey = "JB_BOT_E_NAME";

    /// <summary>The picked bot's plane box: Random, then the eleven stock airframes.</summary>
    public const string BotPlaneKey = "JB_BOT_D_PLANE";

    /// <summary>The picked bot's skill box: Novice, Veteran, Ace.</summary>
    public const string BotSkillKey = "JB_BOT_D_SKILL";

    /// <summary>Removes the picked bot's row.</summary>
    public const string RemoveBotKey = "JB_BOT_REMOVE";

    /// <summary>Lets the picked bot go, back to the articles and the Bots block.</summary>
    public const string BotDoneKey = "JB_BOT_DONE";

    /// <summary>The scrapbook page the board is drawn on.</summary>
    public const string Background = "SB_BackGround.jpg";

    private const int Entries = PlayerSetupFeature.MaxSeats;
    private const float FallbackPlaqueWidth = 162f;
    private const float FallbackPlaqueHeight = 28f;

    // The left page: the heading, four entries down it at one pitch, then each entry's chip, words
    // and rule.
    private const float ManifestX = 80f;
    private const float ManifestY = 82f;
    private const float SubtitleY = 118f;
    private const float EntryTop = 165f;
    private const float EntryPitch = 82f;
    private const float ChipWidth = 34f;
    private const float ChipHeight = 26f;
    private const float TagDrop = 5f;
    private const float WordsX = 126f;
    private const float WordsWidth = 250f;
    private const float DeviceDrop = 2f;
    private const float StatusDrop = 28f;
    private const float RuleDrop = 54f;
    private const float RuleRight = 372f;

    // The right page: the articles under their own heading, the cast-off line below them, and the
    // two plaques on the page's lower right.
    private const float ArticlesX = 440f;
    private const float ArticlesY = 82f;
    private const float ArticleTop = 130f;
    private const float ArticlePitch = 36f;
    private const float CastOffY = 250f;
    private const float PlaqueY = 484f;
    private const float BackX = 500f;
    private const float ContinueX = 620f;

    // The rule under an entry, the scrapbook's own faded brown.
    private const byte RuleR = 90;
    private const byte RuleG = 80;
    private const byte RuleB = 60;
    private const float RuleOpacity = 0.55f;

    // The board's sizes, rungs of the chrome type scale in its own authored pixels.
    private static readonly float HeadingFont = ChromeType.Size(ChromeSize.Heading);
    private static readonly float ArticlesFont = ChromeType.Size(ChromeSize.Title);
    private static readonly float DeviceFont = ChromeType.Size(ChromeSize.Body);
    private static readonly float RuleFont = ChromeType.Size(ChromeSize.Text);
    private static readonly float SmallFont = ChromeType.Size(ChromeSize.Caption);

    private readonly IOriginalScreenHost _host;
    private readonly IJoinRoster? _roster;
    private readonly BoardArt? _plaque;
    private readonly OriginalBotPanel _bots;

    private PosedRoster? _posed;

    /// <summary>A board over <paramref name="layout"/>'s paper plaque strip and the pad roster
    /// <paramref name="roster"/>, calling back into <paramref name="host"/> for the shell state.
    /// Without a roster every entry draws open, which is what an engine-free test sees. The bot rows
    /// are <paramref name="setup"/>'s, their words and pilot names read under
    /// <paramref name="dataRoot"/> and a stock plane's ratings off <paramref name="stock"/>.</summary>
    public OriginalJoinBoard(MenuLayout layout, IOriginalScreenHost host, IJoinRoster? roster, PlayerSetupFeature setup,
        string? dataRoot = null, Func<CSVM.Flight.Weapons.StockLoadouts?>? stock = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(setup);
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _roster = roster;
        var plaqueRow = layout.Screen("FlightCheck")?.Widget("FC_B_CHANGEPLANE");
        _plaque = plaqueRow is { Art.Count: > 0 }
            ? new BoardArt(BoardArtLibrary.Ui, plaqueRow.Art[0], plaqueRow.Frames)
            : null;
        _bots = new OriginalBotPanel(host, setup, dataRoot, stock, key => Plaque(key, PlaqueLabel(key), 0f, 0f));
    }

    /// <summary>The palette the scrapbook's paper takes.</summary>
    public static BoardPalette Palette => BoardPalette.Album;

    /// <summary>The id of the bot whose row is picked for the Edit Bot panel, or -1.</summary>
    public int PickedBot => _bots.Picked;

    /// <summary>The pilots Fill to fills the field to, its count box's value.</summary>
    public int FillCount => _bots.FillCount;

    /// <summary>The bot box whose list stands open, or null.</summary>
    public string? OpenDropdown => _bots.OpenDropdown;

    /// <summary>Whether seat 0's typed characters feed the callsign or the count box.</summary>
    internal bool CapturingText => _bots.CapturingText;

    // The manifest the board draws: the posed one while a screenshot asks for it, else the real
    // pads. A shell built without a roster draws nothing at all.
    private IJoinRoster? Roster => _posed ?? _roster;

    /// <summary>Opens the board, the top level's door.</summary>
    public void Open()
    {
        _posed = null;
        _bots.Reset();
        _host.Open(OriginalScreen.JoinBoard);
    }

    /// <summary>Opens the board with <paramref name="pads"/> entries drawn as signed on, for a
    /// screenshot with nobody at the controls. ⚠ The pose is drawing alone: no pad is claimed and no
    /// seat joined. A shot posed this way says nothing about what a launch would carry.</summary>
    public void Pose(int pads)
    {
        _bots.Reset();
        _host.Open(OriginalScreen.JoinBoard);
        _posed = new PosedRoster(Math.Clamp(pads, 0, Entries));
    }

    /// <summary>Picks bot row <paramref name="id"/> and stands the Edit Bot panel on it, as a press
    /// on its row does, the screenshot aids' door. Nothing is picked for a row not there.</summary>
    public void ShowBot(int id) => _bots.Pick(id);

    /// <summary><see cref="Pose"/> with five bot rows standing, real rows of the player setup, and
    /// with <paramref name="editor"/> the Edit Bot panel open on a stock-plane one.</summary>
    public void PoseBots(int pads, bool editor)
    {
        Pose(pads);
        int id = _bots.PoseRows();
        if (editor)
        {
            ShowBot(id);
        }
    }

    /// <summary>The captain's Start: the board is done with and the manifest stands. Returns
    /// whether the screen moved, so a press on another screen changes nothing.</summary>
    public bool CastOff()
    {
        if (_host.Screen != OriginalScreen.JoinBoard)
        {
            return false;
        }

        Continue();
        return true;
    }

    /// <inheritdoc/>
    public bool Owns(OriginalScreen screen) => screen == OriginalScreen.JoinBoard;

    /// <inheritdoc/>
    public void BuildRows(List<OriginalRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var bots = new List<OriginalRow>();
        if (_bots.BuildRows(bots))
        {
            rows.AddRange(bots);
            return;
        }

        // CONTINUE first, so the board opens with the focus on the press that keeps the manifest
        // rather than on the one that drops it.
        rows.Add(Plaque(ContinueKey, "CONTINUE", ContinueX, PlaqueY));
        rows.Add(Plaque(BackKey, "BACK", BackX, PlaqueY));
        rows.AddRange(bots);
    }

    /// <inheritdoc/>
    public void Lists(List<OriginalList> lists)
    {
    }

    /// <inheritdoc/>
    public bool StepSideways(IReadOnlyList<OriginalRow> rows, int focus, int direction) =>
        _bots.StepSideways(rows ?? throw new ArgumentNullException(nameof(rows)), focus, direction);

    /// <inheritdoc/>
    public bool CloseDropdown() => _bots.CloseDropdown();

    /// <inheritdoc/>
    public MenuExit? Activate(OriginalRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (_bots.Activate(row))
        {
            return null;
        }

        switch (row.Key)
        {
            case ContinueKey:
                Continue();
                break;
            case BackKey:
                Leave();
                break;
        }

        return null;
    }

    /// <inheritdoc/>
    public bool Back()
    {
        if (!_bots.Back())
        {
            Leave();
        }

        return true;
    }

    /// <inheritdoc/>
    public void Compose(IReadOnlyList<OriginalRow> rows, int focus, BoardLayers layers)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(layers);
        layers.Backdrop.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, Background), 0f, 0f));
        layers.Lines.Add(new BoardLine("CREW MANIFEST", ManifestX, ManifestY, 0f, HeadingFont, BoardInk.Heading, Bold: true));
        layers.Lines.Add(new BoardLine("four seats, no stowaways", ManifestX, SubtitleY, 0f, SmallFont, BoardInk.Detail, Italic: true));
        for (int entry = 0; entry < Entries; entry++)
        {
            ComposeEntry(entry, layers);
        }

        // The Edit Bot panel stands in the articles' place.
        if (_bots.Picked < 0)
        {
            ComposeArticles(layers);
        }

        _bots.Compose(rows, focus, layers);
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Key is ContinueKey or BackKey)
            {
                _host.ComposeGenericRow(rows[i], i == focus, i == _host.PressedRow, i, layers);
            }
        }

        // An open list stands alone in the rows, so the plaques under it are drawn as they stand.
        if (_bots.OpenDropdown != null)
        {
            _host.ComposeGenericRow(Plaque(ContinueKey, "CONTINUE", ContinueX, PlaqueY), false, false, -1, layers);
            _host.ComposeGenericRow(Plaque(BackKey, "BACK", BackX, PlaqueY), false, false, -1, layers);
        }
    }

    /// <summary>Typed characters and Backspace into the focused bot box.</summary>
    internal bool TypeText(MenuCommands commands) => _bots.TypeText(commands);

    // One article: the words with the button drawn where the slot stands in them. The gap around
    // the picture is the renderer's, since only it can measure one.
    private static void Article(BoardLayers layers, string text, GlyphKey glyph, float y)
    {
        layers.Lines.Add(new BoardLine(text, ArticlesX, y, 0f, RuleFont, BoardInk.Detail, Italic: true)
        {
            Glyph = glyph,
        });
    }

    // The bot plaques' words, the remake's own as the lobby's are, in the board's capitals.
    private static string PlaqueLabel(string key) => key switch
    {
        AddBotKey => "ADD BOT",
        FillKey => "FILL TO",
        RemoveBotKey => "REMOVE",
        BotDoneKey => "ACCEPT",
        _ => string.Empty,
    };

    private void Continue() => _host.Open(OriginalScreen.TopLevel);

    private void Leave()
    {
        Roster?.DropSignOns();
        _host.Open(OriginalScreen.TopLevel);
    }

    private OriginalRow Plaque(string key, string label, float x, float y)
    {
        var size = OriginalWidgets.StripSize(_plaque, _host.Measure, FallbackPlaqueWidth, FallbackPlaqueHeight);
        return new OriginalRow(key, label, OriginalRowKind.TextButton, x, y, size.Width, size.Height, true, 0, _plaque);
    }

    // One seat's entry: the chip in that seat's own colour, faint while the seat is open. Then
    // either the device over "signed on" or the open seat over the press that takes it.
    private void ComposeEntry(int entry, BoardLayers layers)
    {
        var roster = Roster;
        bool signed = roster?.SignedOn(entry) == true;
        float y = EntryTop + (entry * EntryPitch);
        layers.Fills.Add(new BoardFill(ManifestX, y, ChipWidth, ChipHeight, 0, 0, 0,
            signed ? 1f : 0.25f, Ink: SeatStrip.Ink(entry)));
        layers.Lines.Add(new BoardLine(SplitScreen.PlayerTag(entry), ManifestX, y + TagDrop, ChipWidth, SmallFont,
            BoardInk.Dialog, Justify: BoardJustify.Center, Bold: true));
        if (signed)
        {
            layers.Lines.Add(new BoardLine(roster!.Device(entry), WordsX, y + DeviceDrop, WordsWidth, DeviceFont,
                BoardInk.Row, Italic: true));
            layers.Lines.Add(new BoardLine("signed on", WordsX, y + StatusDrop, 0f, SmallFont,
                BoardInk.Detail, Italic: true));
        }
        else
        {
            layers.Lines.Add(new BoardLine("open seat", WordsX, y + DeviceDrop, WordsWidth, DeviceFont,
                BoardInk.Detail, Italic: true));
            layers.Lines.Add(new BoardLine(BoardLine.GlyphSlot + " to sign on", WordsX, y + StatusDrop, 0f,
                SmallFont, BoardInk.Detail, Italic: true)
            {
                Glyph = ControlGlyphs.PadA,
            });
        }

        layers.Strokes.Add(new BoardStroke(ManifestX, y + RuleDrop, RuleRight, y + RuleDrop,
            RuleR, RuleG, RuleB, RuleOpacity));
    }

    private void ComposeArticles(BoardLayers layers)
    {
        layers.Lines.Add(new BoardLine("ARTICLES OF THE CREW", ArticlesX, ArticlesY, 0f, ArticlesFont,
            BoardInk.Heading, Bold: true));
        Article(layers, "First " + BoardLine.GlyphSlot + " takes the captain's chair.",
            ControlGlyphs.PadA, ArticleTop);
        Article(layers, BoardLine.GlyphSlot + " and your name goes in the log.",
            ControlGlyphs.PadA, ArticleTop + ArticlePitch);
        Article(layers, BoardLine.GlyphSlot + " and you walk the plank.",
            ControlGlyphs.PadB, ArticleTop + (2f * ArticlePitch));
        Article(layers, "Captain, press " + BoardLine.GlyphSlot + " to cast off.",
            ControlGlyphs.PadStart, CastOffY);
    }

    // A manifest with nobody behind it, for a posed shot: the entries read as signed on and the
    // devices carry stand-in names. It signs nobody off, since it holds nothing to give back.
    private sealed class PosedRoster : IJoinRoster
    {
        private static readonly string[] Devices =
        {
            "Xbox Wireless Controller", "DualSense Wireless Controller", "8BitDo Ultimate 2", "Xbox 360 Controller",
        };

        private readonly int _pads;

        internal PosedRoster(int pads) => _pads = pads;

        public bool SignedOn(int entry) => entry >= 0 && entry < _pads;

        public string Device(int entry) => SignedOn(entry) ? Devices[entry] : string.Empty;

        public void DropSignOns()
        {
        }
    }
}
