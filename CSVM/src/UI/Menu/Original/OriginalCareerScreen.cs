using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CSVM.Session.Career;
using CSVM.UI.Boards;
using CSVM.UI.Campaign;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The career's screens, remake-only and drawn on the open scrapbook as the join board is. They are
/// the gang roster, the bridge (the career's hub), the contract board, the hangar's repairs and the
/// ledger. Every screen reads and acts through
/// <see cref="CareerFeature"/>, which saves after each change. Flying a contract waits for the
/// sortie launch, so its plaque stands disabled.
/// </summary>
public sealed class OriginalCareerScreen : IOriginalScreenModule
{
    public const string GangRowPrefix = "CR_GANG_";
    public const string NameKey = "CR_E_NAME";
    public const string StartKey = "CR_START";
    public const string RosterBackKey = "CR_BACK";
    public const string ContractsKey = "CB_CONTRACTS";
    public const string HangarKey = "CB_HANGAR";
    public const string LedgerKey = "CB_LEDGER";
    public const string WaitKey = "CB_WAIT";
    public const string ScuttleKey = "CB_SCUTTLE";
    public const string BridgeBackKey = "CB_BACK";
    public const string OfferRowPrefix = "CC_OFFER_";
    public const string FlyKey = "CC_FLY";
    public const string AbandonKey = "CC_ABANDON";
    public const string PlaneRowPrefix = "CH_PLANE_";
    public const string PageBackKey = "CP_BACK";

    /// <summary>The scrapbook page every career screen is drawn on.</summary>
    public const string Background = "SB_BackGround.jpg";

    private const int ListedGangs = 8;
    private const int ListedEntries = 14;
    private const float FallbackPlaqueWidth = 162f;
    private const float FallbackPlaqueHeight = 28f;

    // The left page: a heading, then lines or list rows at one pitch. The right page: a heading,
    // its lines, and a plaque column down its lower half.
    private const float LeftX = 80f;
    private const float RightX = 440f;
    private const float HeadingY = 82f;
    private const float BodyTop = 130f;
    private const float LinePitch = 24f;
    private const float RowWidth = 300f;
    private const float RowHeight = 22f;
    private const float PlaqueX = 520f;
    private const float PlaqueTop = 260f;
    private const float PlaquePitch = 40f;
    private const float BoxWidth = 220f;
    private const float BoxHeight = 22f;

    private static readonly float HeadingFont = ChromeType.Size(ChromeSize.Heading);
    private static readonly float BodyFont = ChromeType.Size(ChromeSize.Body);
    private static readonly float SmallFont = ChromeType.Size(ChromeSize.Caption);
    private static readonly BoardTint Black = new(0, 0, 0);

    private readonly CareerFeature _career;
    private readonly IOriginalScreenHost _host;
    private readonly BoardArt? _plaque;

    // The new gang's name as typed so far.
    private string _draft = string.Empty;

    /// <summary>The career screens over <paramref name="career"/>, drawn with
    /// <paramref name="layout"/>'s paper plaque strip, calling back into <paramref name="host"/>.</summary>
    public OriginalCareerScreen(CareerFeature career, MenuLayout layout, IOriginalScreenHost host)
    {
        _career = career ?? throw new ArgumentNullException(nameof(career));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        ArgumentNullException.ThrowIfNull(layout);
        var plaqueRow = layout.Screen("FlightCheck")?.Widget("FC_B_CHANGEPLANE");
        _plaque = plaqueRow is { Art.Count: > 0 }
            ? new BoardArt(BoardArtLibrary.Ui, plaqueRow.Art[0], plaqueRow.Frames)
            : null;
    }

    /// <summary>The new gang's name as typed so far.</summary>
    public string Draft => _draft;

    /// <summary>Whether seat 0's typed characters feed the name box.</summary>
    internal bool CapturingText =>
        _host.Screen == OriginalScreen.CareerRoster && !_host.DialogOpen && _host.FocusedKey == NameKey;

    /// <summary>Opens the gang roster, the career's door from the main menu.</summary>
    public void Open()
    {
        _career.Close();
        _draft = string.Empty;
        _host.Open(OriginalScreen.CareerRoster);
    }

    /// <inheritdoc/>
    public bool Owns(OriginalScreen screen) => screen is OriginalScreen.CareerRoster or OriginalScreen.CareerBridge
        or OriginalScreen.CareerContracts or OriginalScreen.CareerHangar or OriginalScreen.CareerLedger;

    /// <inheritdoc/>
    public void BuildRows(List<OriginalRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var state = _career.Current;
        switch (_host.Screen)
        {
            case OriginalScreen.CareerRoster:
                var gangs = _career.Careers.Take(ListedGangs).ToList();
                for (int i = 0; i < gangs.Count; i++)
                {
                    rows.Add(ListRow(GangRowPrefix, i, gangs[i], LeftX, BodyTop + (i * LinePitch)));
                }

                rows.Add(new OriginalRow(NameKey, _draft, OriginalRowKind.TextField, RightX, BodyTop + LinePitch,
                    BoxWidth, BoxHeight, true, 0, null));
                rows.Add(Plaque(StartKey, "START", 0, _career.NameRefusal(_draft) == null));
                rows.Add(Plaque(RosterBackKey, "BACK", 4));
                break;
            case OriginalScreen.CareerBridge when state != null:
                rows.Add(Plaque(ContractsKey, "CONTRACTS", 0));
                rows.Add(Plaque(HangarKey, "HANGAR", 1));
                rows.Add(Plaque(LedgerKey, "LEDGER", 2));
                rows.Add(Plaque(WaitKey, "WAIT A DAY", 3));
                rows.Add(Plaque(ScuttleKey, "SCUTTLE", 4));
                rows.Add(Plaque(BridgeBackKey, "MAIN MENU", 5));
                break;
            case OriginalScreen.CareerContracts when state != null:
                for (int i = 0; i < state.Board.Offers.Count; i++)
                {
                    rows.Add(ListRow(OfferRowPrefix, i, OfferText(state.Board.Offers[i]), LeftX, BodyTop + (i * LinePitch * 2f),
                        state.ActiveContract == null));
                }

                rows.Add(Plaque(FlyKey, "FLY", 2, false));
                rows.Add(Plaque(AbandonKey, "ABANDON", 3, state.ActiveContract != null));
                rows.Add(Plaque(PageBackKey, "BACK", 4));
                break;
            case OriginalScreen.CareerHangar when state != null:
                for (int i = 0; i < state.Planes.Count; i++)
                {
                    rows.Add(ListRow(PlaneRowPrefix, i, state.Planes[i].Build.Name, LeftX, BodyTop + (i * LinePitch * 3f),
                        state.Planes[i].NeedsRepair));
                }

                rows.Add(Plaque(PageBackKey, "BACK", 4));
                break;
            case OriginalScreen.CareerLedger when state != null:
                rows.Add(Plaque(PageBackKey, "BACK", 4));
                break;
        }
    }

    /// <inheritdoc/>
    public void Lists(List<OriginalList> lists)
    {
    }

    /// <inheritdoc/>
    public bool StepSideways(IReadOnlyList<OriginalRow> rows, int focus, int direction) => false;

    /// <inheritdoc/>
    public bool CloseDropdown() => false;

    /// <inheritdoc/>
    public MenuExit? Activate(OriginalRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (OriginalWidgets.Indexed(row.Key, GangRowPrefix) is { } gang)
        {
            if (gang < _career.Careers.Count && _career.Open(_career.Careers[gang]))
            {
                _host.Open(OriginalScreen.CareerBridge);
            }

            return null;
        }

        if (OriginalWidgets.Indexed(row.Key, OfferRowPrefix) is { } offer && _career.Current is { } withBoard)
        {
            if (offer < withBoard.Board.Offers.Count)
            {
                _career.Accept(withBoard.Board.Offers[offer].Id);
            }

            return null;
        }

        if (OriginalWidgets.Indexed(row.Key, PlaneRowPrefix) is { } plane)
        {
            _career.Repair(plane);
            return null;
        }

        switch (row.Key)
        {
            case StartKey:
                if (_career.Start(_draft))
                {
                    _draft = string.Empty;
                    _host.Open(OriginalScreen.CareerBridge);
                }

                break;
            case RosterBackKey:
            case BridgeBackKey:
                _career.Close();
                _host.Open(OriginalScreen.TopLevel);
                break;
            case ContractsKey:
                _host.Open(OriginalScreen.CareerContracts);
                break;
            case HangarKey:
                _host.Open(OriginalScreen.CareerHangar);
                break;
            case LedgerKey:
                _host.Open(OriginalScreen.CareerLedger);
                break;
            case WaitKey:
                _career.Wait();
                break;
            case ScuttleKey:
                AskScuttle();
                break;
            case AbandonKey:
                _career.Abandon();
                break;
            case PageBackKey:
                _host.Open(OriginalScreen.CareerBridge);
                break;
        }

        return null;
    }

    /// <inheritdoc/>
    public bool Back()
    {
        switch (_host.Screen)
        {
            case OriginalScreen.CareerRoster:
            case OriginalScreen.CareerBridge:
                _career.Close();
                _host.Open(OriginalScreen.TopLevel);
                break;
            default:
                _host.Open(OriginalScreen.CareerBridge);
                break;
        }

        return true;
    }

    /// <inheritdoc/>
    public void Compose(IReadOnlyList<OriginalRow> rows, int focus, BoardLayers layers)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(layers);
        layers.Backdrop.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, Background), 0f, 0f));
        var state = _career.Current;
        switch (_host.Screen)
        {
            case OriginalScreen.CareerRoster:
                ComposeRoster(layers);
                break;
            case OriginalScreen.CareerBridge when state != null:
                ComposeBridge(state, layers);
                break;
            case OriginalScreen.CareerContracts when state != null:
                ComposeContracts(state, layers);
                break;
            case OriginalScreen.CareerHangar when state != null:
                ComposeHangar(state, layers);
                break;
            case OriginalScreen.CareerLedger when state != null:
                ComposeLedger(state, layers);
                break;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            bool on = i == focus && !_host.DialogOpen;
            switch (row.Kind)
            {
                case OriginalRowKind.TextField:
                    ComposeBox(row, on, layers);
                    break;
                case OriginalRowKind.ListRow:
                    ComposeListRow(row, on, layers);
                    break;
                default:
                    _host.ComposeGenericRow(row, on, i == _host.PressedRow, i, layers);
                    break;
            }
        }
    }

    /// <summary>Typed characters and Backspace into the name box.</summary>
    internal bool TypeText(MenuCommands commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (!CapturingText || (commands.Typed.Length == 0 && !commands.Erase))
        {
            return false;
        }

        foreach (char c in commands.Typed)
        {
            if (c is >= ' ' and < (char)127 && _draft.Length < CareerFeature.NameLimit)
            {
                _draft += c;
            }
        }

        if (commands.Erase && _draft.Length > 0)
        {
            _draft = _draft[..^1];
        }

        return true;
    }

    private static string Money(int amount) => string.Format(CultureInfo.InvariantCulture, "${0:N0}", amount);

    private static string OfferText(Contract c) => string.Format(
        CultureInfo.InvariantCulture,
        "{0} {1}  {2}  {3}, until day {4}",
        c.Job == JobType.Patrol ? "PATROL" : "STRIKE",
        new string('*', c.Threat),
        Money(c.Fee),
        c.Employer,
        c.ExpiresOn);

    private static string PlaneText(PlaneInstance plane, CareerTunes tunes)
    {
        int armour = Enum.GetValues<ArmourZone>().Sum(plane.ZoneArmour);
        int capacity = Enum.GetValues<ArmourZone>().Sum(plane.ZoneCapacity);
        var quote = plane.QuoteRepair(tunes);
        string parts = plane.DamagedParts == PlaneParts.None ? "guns and engine sound" : "damaged: " + plane.DamagedParts;
        string repair = quote.IsNothing
            ? "nothing to repair"
            : string.Format(CultureInfo.InvariantCulture, "repair {0}, {1} day(s)", Money(quote.Cost), quote.Days);
        return string.Format(CultureInfo.InvariantCulture, "armour {0}/{1}, {2}; {3}", armour, capacity, parts, repair);
    }

    private static void Line(BoardLayers layers, string text, float x, float y, float size, BoardInk ink, bool bold = false) =>
        layers.Lines.Add(new BoardLine(text, x, y, 0f, size, ink, Bold: bold));

    private void ComposeRoster(BoardLayers layers)
    {
        Line(layers, "THE GANGS", LeftX, HeadingY, HeadingFont, BoardInk.Heading, bold: true);
        if (_career.Careers.Count == 0)
        {
            Line(layers, "No gang flies yet.", LeftX, BodyTop, BodyFont, BoardInk.Detail);
        }

        Line(layers, "A NEW GANG", RightX, HeadingY, HeadingFont, BoardInk.Heading, bold: true);
        Line(layers, "Name the gang:", RightX, BodyTop, SmallFont, BoardInk.Detail);
        if (_draft.Length > 0 && _career.NameRefusal(_draft) is { } refusal)
        {
            Line(layers, refusal, RightX, BodyTop + (LinePitch * 2.5f), SmallFont, BoardInk.Detail);
        }
    }

    private void ComposeBridge(CareerState state, BoardLayers layers)
    {
        Line(layers, state.Name.ToUpperInvariant(), LeftX, HeadingY, HeadingFont, BoardInk.Heading, bold: true);
        var lines = new List<string>
        {
            string.Format(CultureInfo.InvariantCulture, "Day {0}, week {1}", state.Day, state.Week + 1),
            "Cash " + Money(state.Ledger.Cash),
            "Debt " + Money(state.Ledger.Debt),
            string.Format(CultureInfo.InvariantCulture, "Next payday: day {0}", state.NextPayday),
            state.ActiveContract is { } active ? "Contract: " + OfferText(active) : "No contract taken",
            string.Empty,
        };
        lines.AddRange(state.Planes.Select(p => p.Build.Name + (p.CanFly ? (p.NeedsRepair ? ", damaged" : ", ready") : ", grounded")));
        for (int i = 0; i < lines.Count; i++)
        {
            Line(layers, lines[i], LeftX, BodyTop + (i * LinePitch), BodyFont, BoardInk.Row);
        }

        Line(layers, "THE BRIDGE", RightX, HeadingY, HeadingFont, BoardInk.Heading, bold: true);
    }

    private void ComposeContracts(CareerState state, BoardLayers layers)
    {
        Line(layers, "CONTRACT BOARD", LeftX, HeadingY, HeadingFont, BoardInk.Heading, bold: true);
        Line(layers, "THE JOB", RightX, HeadingY, HeadingFont, BoardInk.Heading, bold: true);
        string job = state.ActiveContract is { } active ? OfferText(active) : "Take a job from the board.";
        Line(layers, job, RightX, BodyTop, SmallFont, BoardInk.Row);
        if (state.ActiveContract != null)
        {
            Line(layers, "Flying a contract comes with the sortie launch.", RightX, BodyTop + LinePitch, SmallFont, BoardInk.Detail);
        }
    }

    private void ComposeHangar(CareerState state, BoardLayers layers)
    {
        Line(layers, "THE HANGAR", LeftX, HeadingY, HeadingFont, BoardInk.Heading, bold: true);
        for (int i = 0; i < state.Planes.Count; i++)
        {
            Line(layers, PlaneText(state.Planes[i], state.Tunes), LeftX, BodyTop + (i * LinePitch * 3f) + LinePitch, SmallFont, BoardInk.Detail);
        }

        Line(layers, "THE HOLD", RightX, HeadingY, HeadingFont, BoardInk.Heading, bold: true);
        var stock = state.Hold.Rounds
            .Select(p => string.Format(CultureInfo.InvariantCulture, "{0} rounds .{1} {2}", p.Value, (p.Key.Calibre + 3) * 10, p.Key.Kind))
            .Concat(state.Hold.Rockets.Select(p => string.Format(CultureInfo.InvariantCulture, "{0} x {1}", p.Value, p.Key)))
            .DefaultIfEmpty("Empty. Slug rounds are free at the port.")
            .Take(5)
            .ToList();
        for (int i = 0; i < stock.Count; i++)
        {
            Line(layers, stock[i], RightX, BodyTop + (i * LinePitch), SmallFont, BoardInk.Row);
        }
    }

    private void ComposeLedger(CareerState state, BoardLayers layers)
    {
        Line(layers, "THE LEDGER", LeftX, HeadingY, HeadingFont, BoardInk.Heading, bold: true);
        var recent = state.Ledger.Entries.Reverse().Take(ListedEntries).ToList();
        for (int i = 0; i < recent.Count; i++)
        {
            var e = recent[i];
            string amount = e.Cash != 0 ? Money(e.Cash) : "debt " + Money(e.Debt);
            Line(layers, string.Format(CultureInfo.InvariantCulture, "day {0}  {1}  {2}", e.Day, e.Category, amount),
                LeftX, BodyTop + (i * LinePitch * 0.8f), SmallFont, BoardInk.Row);
        }

        Line(layers, "THIS WEEK", RightX, HeadingY, HeadingFont, BoardInk.Heading, bold: true);
        int weekStart = state.Week * CareerTunes.DaysPerWeek;
        var week = state.Ledger.Entries.Where(e => e.Day >= weekStart).ToList();
        Line(layers, "In " + Money(week.Where(e => e.Cash > 0).Sum(e => e.Cash)), RightX, BodyTop, BodyFont, BoardInk.Row);
        Line(layers, "Out " + Money(-week.Where(e => e.Cash < 0).Sum(e => e.Cash)), RightX, BodyTop + LinePitch, BodyFont, BoardInk.Row);
    }

    private void AskScuttle()
    {
        if (_career.Current is not { } state)
        {
            return;
        }

        string name = state.Name;
        _host.RaiseDialog(
            "Scuttle " + name + "? The gang and its save are gone for good.",
            DialogIcon.Query,
            new OriginalDialogAnswer(OriginalShell.DialogYesKey, CampaignBoards.DialogLeftKey, "Scuttle", () =>
            {
                _career.Delete(name);
                _host.Open(OriginalScreen.CareerRoster);
            }),
            new OriginalDialogAnswer(OriginalShell.DialogNoKey, CampaignBoards.DialogRightKey, "Keep", null));
    }

    private OriginalRow Plaque(string key, string label, int slot, bool enabled = true)
    {
        var size = OriginalWidgets.StripSize(_plaque, _host.Measure, FallbackPlaqueWidth, FallbackPlaqueHeight);
        return new OriginalRow(key, label, OriginalRowKind.TextButton, PlaqueX, PlaqueTop + (slot * PlaquePitch),
            size.Width, size.Height, enabled, 0, _plaque);
    }

    private OriginalRow ListRow(string prefix, int index, string label, float x, float y, bool enabled = true) =>
        new(prefix + index.ToString(CultureInfo.InvariantCulture), label, OriginalRowKind.ListRow, x, y, RowWidth, RowHeight, enabled, 0, null);

    private void ComposeListRow(OriginalRow row, bool focused, BoardLayers layers)
    {
        if (focused)
        {
            layers.Fills.Add(_host.FocusMark(row));
        }

        layers.Lines.Add(new BoardLine(row.Label, row.X + 2f, row.Y + 2f, row.Width - 4f, BodyFont,
            row.Enabled ? BoardInk.Row : BoardInk.Detail));
    }

    private void ComposeBox(OriginalRow row, bool focused, BoardLayers layers)
    {
        layers.Fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 209, 180, 120));
        layers.Fills.Add(new BoardFill(row.X, row.Y, row.Width, row.Height, 0, 0, 0, Border: true));
        var caret = focused ? new BoardCaret(0, 0, 0, 1f, row.Height - 4f) : (BoardCaret?)null;
        layers.Lines.Add(new BoardLine(row.Label, row.X + 4f, row.Y + 3f, row.Width - 8f, BodyFont, BoardInk.Row, -1,
            Caret: caret, Colour: Black));
    }
}
