using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Session;

namespace CSVM.UI.Menu.Original;

/// <summary>One answer of a dialog standing over a campaign screen: its row key, the messagebox
/// button row it draws at, its words, and what answering it runs.</summary>
public sealed record OriginalDialogAnswer(string Key, string LayoutKey, string Label, Action? Run);

/// <summary>A dialog standing over a campaign screen, the original's <c>messagebox.script</c>
/// over whatever screen was showing: its words, the icon its message class draws, its one or two
/// answers and the widget set it is drawn from (null for the shared <c>mb_</c> box). While one
/// stands the rows are its answers alone.</summary>
public sealed record OriginalDialog(
    string Message, DialogIcon Icon, IReadOnlyList<OriginalDialogAnswer> Answers,
    CampaignBoards.DialogChrome? Chrome = null);

/// <summary>
/// The Original campaign, the shell's partial over the shared <see cref="CampaignFeature"/>: the
/// decoded profile, cabin, table of contents, flight check, ammo, plane selection, scrapbook and
/// zoom screens and the briefing dialog. The screen graph, the pointer's hit rectangles, the
/// rollover and pressed frames, the cues, the dialogs and every door out are this file's; what
/// each screen draws is the shared board component, <c>CampaignBoards.For</c> over the campaign
/// pages, which this shell hosts in a <c>CampaignFlow</c> of its own built over the feature and
/// the layout it already holds. That flow is never walked: its screen is moved to mirror the one
/// showing here, its row to mirror the focus, and a page that names a destination or raises a
/// dialog has both read off it and re-entered through this graph. The pages' own editing state (a
/// working loadout, a pick, a page turn) stays theirs, since a commit is what the feature writes.
/// </summary>
public sealed partial class OriginalShell
{
    /// <summary>The Campaign row's key on the top level.</summary>
    public const string CampaignKey = "MM_B_CAMPAIGN";

    /// <summary>A one-answer dialog's OK.</summary>
    public const string DialogOkKey = "DIALOG:OK";

    /// <summary>A two-answer dialog's confirming answer.</summary>
    public const string DialogYesKey = "DIALOG:YES";

    /// <summary>A two-answer dialog's declining answer.</summary>
    public const string DialogNoKey = "DIALOG:NO";

    // A page row that is neither a button nor a field: a roster name, a mission row, a scrap.
    private const string RowKeyPrefix = "ROW:";

    // A page row carrying a drop-down field.
    private const string FieldKeyPrefix = "FIELD:";

    // One entry of an open drop-down list, keyed by its index into the list.
    private const string EntryKeyPrefix = "ENTRY:";

    // The roster's own list colours, CAMPAIGN.SCRIPT's sub-script VB: the selection bar behind the
    // picked row (0xff800000) and the frame around the row under the pointer (0xffff0000).
    private const byte RosterBarRed = 0x80;
    private const byte RosterFrameRed = 0xff;

    // A plaque whose art the measurer cannot see: the briefing's brief_button1 is mission art
    // under extracted/rimage, 196x32 in the shipped file; every other plaque is a rof strip.
    private const float BriefPlaqueWidth = 196f;
    private const float BriefPlaqueHeight = 32f;

    // A capture with no authored region is forced to 164x123 and drawn at a quarter on the page.
    private const float CaptureRegionWidth = 41f;
    private const float CaptureRegionHeight = 31f;

    // The name box's own height where the row carries none.
    private const float FallbackFieldHeight = 20f;

    private readonly CampaignFeature? _campaign;
    private readonly Func<CampaignProfileStore>? _profiles;
    private readonly Func<CSVM.Flight.StockLoadouts?>? _stock;
    private readonly string? _dataRoot;
    private readonly CampaignLayout _campaignLayout;

    // The pages' host, mirrored to the screen showing and never walked (see the class summary).
    private CampaignFlow? _flow;
    private OriginalDialog? _dialog;
    private int _focusBeforeDialog = -1;
    private OriginalScreen _briefingReturn = OriginalScreen.CampaignCabin;
    private OriginalScreen _bookReturn = OriginalScreen.CampaignCabin;

    /// <summary>Whether the screen showing is one of the campaign's.</summary>
    public bool IsCampaignScreen => _screen >= OriginalScreen.CampaignRoster && _screen <= OriginalScreen.CampaignScrapbookZoom;

    /// <summary>Whether a campaign is open on this shell.</summary>
    public bool CampaignOpen => _flow != null;

    /// <summary>The campaign page composing the screen showing, or null off the campaign.</summary>
    public ICampaignPage? CampaignContent => IsCampaignScreen ? _flow?.Page : null;

    /// <summary>Which campaign board the screen showing wears, or null when it wears none; what
    /// the presentation picks the board's palette by. The per-seat aircraft screen wears the
    /// plane-selection board off the campaign.</summary>
    public CampaignScreen? CampaignPage => IsCampaignScreen ? CampaignScreenOf(_screen)
        : _screen == OriginalScreen.SeatPlane ? CampaignScreen.PlaneSelection
        : null;

    /// <summary>The dialog standing over the screen, or null.</summary>
    public OriginalDialog? Dialog => _dialog;

    /// <summary>The name in the roster's box.</summary>
    public string RosterName => RosterEntry?.Text ?? string.Empty;

    /// <summary>How many times the briefing showing has asked for its narration, or 0 off the
    /// briefing; the presentation starts playback whenever this rises.</summary>
    public int NarrationStarts => _screen == OriginalScreen.CampaignBriefing ? _campaign?.Briefing?.NarrationStarts ?? 0 : 0;

    /// <summary>The briefing's narration wav, or "" when there is none or the briefing is not showing.</summary>
    public string NarrationWav => _screen == OriginalScreen.CampaignBriefing ? _campaign?.Briefing?.NarrationWav ?? string.Empty : string.Empty;

    /// <summary>The seated profile as the hangar's wallet, or null with nobody seated: what the
    /// cabin's PLANE CONSTRUCTION opens the hangar over, and what the campaign-hangar aid takes.</summary>
    public IHangarWallet? CampaignWallet => _campaign?.Wallet();

    private CampaignTextEntry? RosterEntry => _flow?.Page is CampaignRosterPage roster ? roster.TextEntry : null;

    // The focused row as a page row, for the pages that read the flow's cursor.
    private int PageFocus
    {
        get
        {
            if (_flow == null || _dialog != null)
            {
                return _focusBeforeDialog;
            }

            int focus = _focus[(int)_screen];
            return focus >= 0 && focus < _flow.Page.RowCount ? focus : -1;
        }
    }

    // The open drop-down list, on a campaign screen or the per-seat aircraft screen, or null.
    private CampaignCombo? OpenCombo
    {
        get
        {
            if (_screen == OriginalScreen.SeatPlane)
            {
                return _seatPage?.List is { Open: true } list ? list : null;
            }

            return _flow != null && _dialog == null && PageFocus >= 0 && _flow.Page.Combo(PageFocus) is { Open: true } combo ? combo : null;
        }
    }

    // The screens whose fields are board combos, so a list of theirs can stand open.
    private bool IsComboScreen => IsCampaignScreen || _screen == OriginalScreen.SeatPlane;

    // The flight check's screens: the check itself and the ammo and plane screens its own rows
    // open, which no other door reaches. All three stand for one player at a time, so the seat the
    // field names owns them; the guest that opened ammo selection is still the seat whose aircraft
    // it edits.
    private bool OnCampaignCheck =>
        _campaign != null && _screen is OriginalScreen.CampaignFlightCheck
            or OriginalScreen.CampaignAmmo or OriginalScreen.CampaignPlaneSelection;

    // The seat whose check is showing, as its index, or -1 off the check's screens.
    private int CheckSeat => OnCampaignCheck && _campaign is { } campaign ? campaign.Field.Current : -1;

    /// <summary>The Campaign row's door: opens the campaign over the user's profile store and lands
    /// on the profile screen. Nothing happens when the shell has no feature or no store.</summary>
    public void OpenCampaign()
    {
        if (_profiles != null)
        {
            OpenCampaignOver(_profiles());
        }
    }

    /// <summary>Opens the campaign over <paramref name="store"/>, the aids' and the suites' door,
    /// and lands on the profile screen with the name box pre-filled with the last player seated,
    /// the way <c>CAMPAIGN.SCRIPT</c> pre-fills it from the registry. <paramref name="planes"/>
    /// stands in for the shell's build store while this campaign is open, which is how an aid's
    /// EXPORT writes into a scratch store.</summary>
    public void OpenCampaignOver(CampaignProfileStore store, CSVM.Flight.CustomPlaneStore? planes = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (_campaign == null)
        {
            return;
        }

        _campaign.Open(store, planes ?? _planes, _stock?.Invoke(), _dataRoot);
        _flow = new CampaignFlow(_campaign, _campaignLayout);
        _dialog = null;
        _briefingReturn = OriginalScreen.CampaignCabin;
        _bookReturn = OriginalScreen.CampaignCabin;
        string last = _campaign.LastPlayed;
        RosterEntry?.Set(RosterHas(last) ? last : string.Empty);
        ShowCampaign(OriginalScreen.CampaignRoster);
    }

    /// <summary>Seats the named profile re-read from the store and lands on the cabin, the door a
    /// flight return and the campaign screenshot aids take; false, on the profile screen, when the
    /// profile cannot be read. True says the profile is seated, not that the cabin is showing: a
    /// chapter cinema plays first where one is due, and the cabin arrives when it stops.</summary>
    public bool ShowCabin(string profile)
    {
        if (_flow == null || _campaign == null || !_campaign.SeatProfile(profile))
        {
            return false;
        }

        OpenCabin();
        return true;
    }

    /// <summary>Seats the named profile and opens the book on a mission's first spread with the
    /// cabin on its far side, the mission end's own door and the book's screenshot aid; false when
    /// the profile cannot be read. True says the profile is seated, not that the book is showing:
    /// the closing cinema plays first for a finished campaign, and the book arrives when it
    /// stops.</summary>
    public bool ShowScrapbook(string profile, int seq)
    {
        if (_flow == null || _campaign == null || !_campaign.SeatProfile(profile))
        {
            return false;
        }

        if (_campaign.ClosingCinema is { } cinema && _campaign.Profile is { } seated)
        {
            cinema.OpenScrapbook(seated, () => OpenBook(seq));
            return true;
        }

        OpenBook(seq);
        return true;
    }

    /// <summary>Names <paramref name="name"/> in the profile screen's box and presses DELETE PLAYER,
    /// so the two-answer messagebox stands over the screen: the screenshot aid's door. Nothing
    /// happens off the profile screen.</summary>
    public void ShowDeleteConfirm(string name)
    {
        if (_screen != OriginalScreen.CampaignRoster || RosterEntry is not { } entry)
        {
            return;
        }

        entry.Set(name);
        BeginDelete();
    }

    /// <summary>Opens one of the screens past the cabin on the seated profile's next mission, the
    /// aids' door: the table of contents, the briefing, the flight check, ammo selection on the
    /// pilot's aircraft or plane selection on the pilot's slot. Nothing happens with nobody seated.</summary>
    public void ShowMissionScreen(OriginalScreen screen)
    {
        if (_flow == null || _campaign?.Profile == null || !IsCampaign(screen))
        {
            return;
        }

        _campaign.SetMission(_campaign.NextMissionSeq);
        _campaign.SetAmmoSlot(0);
        _campaign.SetPlaneSlot(0);
        _briefingReturn = OriginalScreen.CampaignCabin;
        ShowCampaign(screen);
    }

    /// <summary>Presses the pilot's EXPORT on the plane-selection screen showing, so the one-button
    /// messagebox stands over it: the screenshot aid's door. Nothing happens off that screen or
    /// while a dialog already stands.</summary>
    public void PressExport() => PressBoardButton(BoardButton.ExportPlane);

    /// <summary>Replays a screenshot aid's colon argument on the campaign screen showing, the words
    /// <see cref="CampaignAidScript"/> reads for Built-in, so one string poses both presentations.
    /// The flow here is never walked, so a cursor verb is one command frame through this graph and
    /// a button word is that button's row taking the focus and the confirm. False for a script
    /// spelling the secondary verb x, which is refused whole because Original binds no such press,
    /// its lists selecting by click; its caller ends the run.</summary>
    public bool RunAidScript(string script)
    {
        if (CampaignAidScript.Presses(script, CampaignAidScript.VerbsWithoutSecondary, "the Original presentation")
            is not { } presses)
        {
            return false;
        }

        foreach (CampaignAidScript.Step press in presses)
        {
            for (int i = 0; i < press.Count; i++)
            {
                PressAidStep(press);
            }
        }

        return true;
    }

    /// <summary>Moves the briefing's reveal on by a frame's worth of seconds while the briefing
    /// shows, the presentation's clock; returns whether the reveal is still running, which is when
    /// the board has to repaint on the frame clock.</summary>
    public bool AdvanceBriefing(double seconds)
    {
        if (_screen != OriginalScreen.CampaignBriefing || _campaign?.Briefing is not { } briefing)
        {
            return false;
        }

        briefing.Advance(seconds);
        return !briefing.Complete;
    }

    /// <summary>Drops the open campaign, if any: the feature's transient state and the pages, and
    /// re-reads the build store into the sortie roster, since an EXPORT inside may have crossed a
    /// plane into it. Every door out of the campaign and every return to the top level comes
    /// through here.</summary>
    public void CloseCampaign()
    {
        _campaign?.Discard();
        _flow = null;
        _dialog = null;

        // An EXPORT inside the campaign just crossed a plane into the sortie lists, and those are
        // reached from the top level without another Activate to re-read the store on.
        if (_planes != null)
        {
            _setup.SetRoster(OriginalRosters.Roster(_planes.List()));
        }
    }

    private static bool IsCampaign(OriginalScreen screen) =>
        screen >= OriginalScreen.CampaignRoster && screen <= OriginalScreen.CampaignScrapbookZoom;

    private static CampaignScreen CampaignScreenOf(OriginalScreen screen) => screen switch
    {
        OriginalScreen.CampaignRoster => CampaignScreen.Roster,
        OriginalScreen.CampaignCabin => CampaignScreen.Cabin,
        OriginalScreen.CampaignPreviousMissions => CampaignScreen.PreviousMissions,
        OriginalScreen.CampaignBriefing => CampaignScreen.Briefing,
        OriginalScreen.CampaignFlightCheck => CampaignScreen.FlightCheck,
        OriginalScreen.CampaignAmmo => CampaignScreen.Ammo,
        OriginalScreen.CampaignPlaneSelection => CampaignScreen.PlaneSelection,
        OriginalScreen.CampaignScrapbook => CampaignScreen.Scrapbook,
        _ => CampaignScreen.ScrapbookZoom,
    };

    private static OriginalScreen OriginalOf(CampaignScreen screen) => screen switch
    {
        CampaignScreen.Roster => OriginalScreen.CampaignRoster,
        CampaignScreen.Cabin => OriginalScreen.CampaignCabin,
        CampaignScreen.PreviousMissions => OriginalScreen.CampaignPreviousMissions,
        CampaignScreen.Briefing => OriginalScreen.CampaignBriefing,
        CampaignScreen.FlightCheck => OriginalScreen.CampaignFlightCheck,
        CampaignScreen.Ammo => OriginalScreen.CampaignAmmo,
        CampaignScreen.PlaneSelection => OriginalScreen.CampaignPlaneSelection,
        CampaignScreen.Scrapbook => OriginalScreen.CampaignScrapbook,
        _ => OriginalScreen.CampaignScrapbookZoom,
    };

    // A row key for a page row: the authored button it presses (with its crew slot), else the
    // kind of row it is with its index.
    private static string CampaignRowKey(ICampaignPage page, int row)
    {
        var reference = page.Button(row);
        if (reference.Button != BoardButton.None)
        {
            return reference.Slot > 0
                ? reference.Button + ":" + reference.Slot.ToString(CultureInfo.InvariantCulture)
                : reference.Button.ToString();
        }

        return (page.Combo(row) != null ? FieldKeyPrefix : RowKeyPrefix) + row.ToString(CultureInfo.InvariantCulture);
    }

    private static bool HoverOnly(OriginalRow row) => row.Key.StartsWith(EntryKeyPrefix, StringComparison.Ordinal);

    private static int? Entry(string key) =>
        key.StartsWith(EntryKeyPrefix, StringComparison.Ordinal)
            && int.TryParse(key.AsSpan(EntryKeyPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int entry)
            ? entry
            : null;

    // The messagebox script's own answer words: it hands its buttons langui 100 (OK) for the
    // one-button box and 102 and 103 (Yes, No) for the two-button pair, read here through
    // whichever feature carries the string table.
    private OriginalDialogAnswer Ok(Action? run = null) =>
        new(DialogOkKey, CampaignBoards.DialogCenterKey, DialogWord(100, "OK"), run);

    private OriginalDialogAnswer Yes(Action run) =>
        new(DialogYesKey, CampaignBoards.DialogLeftKey, DialogWord(102, "Yes"), run);

    private OriginalDialogAnswer No() =>
        new(DialogNoKey, CampaignBoards.DialogRightKey, DialogWord(103, "No"), null);

    private string DialogWord(int id, string fallback)
    {
        var strings = _campaign?.Strings ?? _hangar?.Strings;
        string word = strings?.Text(id, fallback) ?? fallback;
        return word.Length > 0 ? word : fallback;
    }

    // A standing dialog's answers, at the messagebox rows they draw on, whatever screen it stands over.
    private List<OriginalRow> DialogRows()
    {
        var rows = new List<OriginalRow>();
        if (_dialog is not { } dialog)
        {
            return rows;
        }

        foreach (var answer in dialog.Answers)
        {
            var (art, x, y) = CampaignBoards.DialogSlot(answer.LayoutKey, _campaignLayout, dialog.Chrome);
            var size = PlaqueSizeOf(art);
            rows.Add(new OriginalRow(answer.Key, answer.Label, OriginalRowKind.Button, x, y, size.Width, size.Height, true, 0, art));
        }

        return rows;
    }

    // The standing dialog as the shared board component's messagebox panel, each answer in the
    // frame of its state under the cursor and the pointer, and in the box's own ink rather than
    // the screen's, which on a paper screen would hide the label on the dark strip.
    private BoardPanel ComposeDialog(IReadOnlyList<OriginalRow> rows, int focus)
    {
        var dialog = _dialog!;
        var buttons = new List<CampaignBoards.DialogButton>(dialog.Answers.Count);
        for (int i = 0; i < dialog.Answers.Count; i++)
        {
            bool focused = i == focus;
            bool held = i == _pressed;
            buttons.Add(new CampaignBoards.DialogButton(
                dialog.Answers[i].LayoutKey, dialog.Answers[i].Label,
                ComposedBoard.PlaqueFrame(4, focused, held), ComposedBoard.DialogInk(held)));
        }

        return CampaignBoards.Dialog(dialog.Message, buttons, dialog.Icon, _campaignLayout, dialog.Chrome);
    }

    private bool RosterHas(string name)
    {
        if (_campaign == null || name.Length == 0)
        {
            return false;
        }

        foreach (string stored in _campaign.Roster)
        {
            if (stored == name)
            {
                return true;
            }
        }

        return false;
    }

    // Enters a campaign screen: the pages' host is moved onto its page (its cursor landing on the
    // page's own opening row, which is where this screen's focus opens too), no dialog stands, and
    // the pointer state starts afresh. A way back onto a screen keeps the focus where it stood, on
    // the plaque that opened what is being left, so a round trip lands where it started.
    private void ShowCampaign(OriginalScreen screen, bool keepFocus = false)
    {
        if (_flow == null)
        {
            return;
        }

        _flow.GoTo(CampaignScreenOf(screen));
        _dialog = null;
        Open(screen);
        if (!keepFocus || _focus[(int)screen] < 0)
        {
            _focus[(int)screen] = _flow.Row;
        }
    }

    private void EnterBriefing(OriginalScreen returnTo)
    {
        _briefingReturn = returnTo;
        ShowCampaign(OriginalScreen.CampaignBriefing);
    }

    // Every door out of the campaign: CANCEL and Back on the profile screen, RETURN TO MAIN MENU.
    private void LeaveCampaign()
    {
        CloseCampaign();
        Open(OriginalScreen.TopLevel);
    }

    // Back from the hangar the cabin opened: the profile is re-read so a purchase or a sale shows.
    private void ResumeCampaign()
    {
        _campaign?.Resume();
        OpenCabin(keepFocus: true);
    }

    // The joined humans on the flight check, once a frame: the seats are the shared setup's.
    private void SyncCampaignField()
    {
        if (_screen == OriginalScreen.CampaignFlightCheck && _campaign != null)
        {
            _campaign.Field.SetPlayers(_setup.Seats.Count);
        }
    }

    // The seat picking on the per-seat screen as its index, else the seat whose flight check
    // shows, else none: the strip's focused line.
    private int StripFocus()
    {
        if (_screen == OriginalScreen.SeatPlane)
        {
            return PickingSeat;
        }

        return _screen == OriginalScreen.CampaignFlightCheck && _campaign != null ? _campaign.Field.Current : -1;
    }

    // Typed characters and Backspace into the roster's name box, the campaign's own character set
    // and cap, each character cueing the box's keystroke or reject sound.
    private bool TypeRosterName(MenuCommands commands, List<string> cues)
    {
        if (RosterEntry is not { } entry || _dialog != null)
        {
            return false;
        }

        bool changed = false;
        foreach (char c in commands.Typed)
        {
            if (entry.Type(c.ToString()))
            {
                changed = true;
                cues.Add(OriginalCues.Text);
            }
            else
            {
                cues.Add(OriginalCues.TextError);
            }
        }

        if (commands.Erase && entry.Backspace())
        {
            changed = true;
        }

        return changed;
    }

    // Every raise names its icon, because MESSAGEBOX.SCRIPT reads the frame off the raising
    // screen's button mask rather than off anything the box itself can see. A default here would
    // be a rule of "one button means the warning", which the original's 0x2 boxes break.
    private void RaiseDialog(string message, DialogIcon icon, params OriginalDialogAnswer[] answers) =>
        RaiseDialog(null, message, icon, answers);

    // The same raise in another widget set, which the credits screen's About box is drawn from.
    private void RaiseDialog(
        CampaignBoards.DialogChrome? chrome, string message, DialogIcon icon, params OriginalDialogAnswer[] answers)
    {
        _focusBeforeDialog = _focus[(int)_screen];
        _dialog = new OriginalDialog(message, icon, answers, chrome);
        _hover = -1;
        _pressed = -1;
        _armed = null;
        // A box opens on its first answer, the left button MESSAGEBOX.SCRIPT focuses for the plain
        // 0x4 mask. Back still takes the declining one, so a mistake has a way out.
        _focus[(int)_screen] = 0;
    }

    private void AnswerDialog(string key)
    {
        if (_dialog is not { } dialog)
        {
            return;
        }

        _dialog = null;
        _focus[(int)_screen] = _focusBeforeDialog;
        foreach (var answer in dialog.Answers)
        {
            if (answer.Key == key)
            {
                answer.Run?.Invoke();
                return;
            }
        }
    }

    private void BuildCampaignRows(List<OriginalRow> rows)
    {
        if (_flow != null)
        {
            BuildPageRows(_flow.Page, rows);
        }
    }

    // The rows of a screen drawn by the shared board component: one row per page row at the
    // rectangle the component draws it at (a button's slot and strip, a field's box, a list row's
    // slot), a row with no rectangle keeping its index unseen and unhit and a row the page refuses
    // focus on disabled, then the open list's entries where a field is open.
    private void BuildPageRows(ICampaignPage page, List<OriginalRow> rows)
    {
        var screen = page.Screen;
        int listIndex = 0;
        for (int row = 0; row < page.RowCount; row++)
        {
            string key = CampaignRowKey(page, row);
            bool enabled = page.Focusable(row) && RowEnabled(page, row);
            if (page.Combo(row) is { } combo)
            {
                rows.Add(new OriginalRow(key, combo.Text, OriginalRowKind.Dropdown, combo.X, combo.Y, combo.Width,
                    CampaignBoards.ComboFieldHeight, enabled, 0, null));
                continue;
            }

            var reference = page.Button(row);
            if (reference.Button != BoardButton.None)
            {
                if (CampaignBoards.SlotOf(screen, reference, _campaignLayout) is { } slot)
                {
                    var size = PlaqueSizeOf(slot.Art);
                    rows.Add(new OriginalRow(key, page.RowText(row), OriginalRowKind.Button, slot.X, slot.Y, size.Width, size.Height, enabled, 0, slot.Art));
                }
                else
                {
                    rows.Add(new OriginalRow(key, page.RowText(row), OriginalRowKind.Button, 0f, 0f, 0f, 0f, false, 0, null, false));
                }

                continue;
            }

            // A focusable row with no rectangle (a mission row scrolled out of its window) keeps
            // its place for the keyboard, unseen and unhit.
            var box = ListRowBox(page, row, listIndex++);
            var kind = screen == CampaignScreen.Roster && row == 0 ? OriginalRowKind.TextField : OriginalRowKind.ListRow;
            rows.Add(box is { } b
                ? new OriginalRow(key, page.RowText(row), kind, b.X, b.Y, b.Width, b.Height, enabled, 0, null)
                : new OriginalRow(key, page.RowText(row), kind, 0f, 0f, 0f, 0f, enabled, 0, null, false));
        }

        if (OpenCombo is { } open)
        {
            float top = open.Y + CampaignBoards.ComboFieldHeight;
            for (int seen = 0; seen < open.Visible; seen++)
            {
                int entry = open.First + seen;
                if (entry >= open.Entries.Count)
                {
                    break;
                }

                rows.Add(new OriginalRow(EntryKeyPrefix + entry.ToString(CultureInfo.InvariantCulture), open.Entries[entry],
                    OriginalRowKind.ListRow, open.X, top + (seen * open.RowHeight), open.Width, open.RowHeight, true, 0, null));
            }
        }
    }

    // The cabin's NEXT MISSION is disabled once the campaign is finished, the script's own
    // mail(10000) when uiData 2600 answers 0; every other button is live as the page offers it.
    private bool RowEnabled(ICampaignPage page, int row) =>
        !(page.Screen == CampaignScreen.Cabin && page.Button(row).Button == BoardButton.NextMission && _campaign?.CampaignComplete == true);

    // Where a list or text row sits: the roster's box and its list rows at the layout's own item
    // height, a mission row inside the table of contents' window, and on the book a scrap at its
    // authored region (or its picture's bounds where the row authors none), else whatever art the
    // row draws itself with, at that strip's frame. The book's answer is per row and not per scrap
    // because a row the page offers and the pointer cannot reach is a control the player has lost.
    private (float X, float Y, float Width, float Height)? ListRowBox(ICampaignPage page, int row, int listIndex)
    {
        switch (page)
        {
            case CampaignRosterPage:
                {
                    var (x, y, width) = CampaignBoards.TextSlot(CampaignScreen.Roster, listIndex, _campaignLayout);
                    float height = row == 0
                        ? _campaignLayout.Int(CampaignLayout.RosterSection, "CM_E_NAME", "Height", (int)FallbackFieldHeight)
                        : _campaignLayout.Int(CampaignLayout.RosterSection, "CM_L_PLAYERS", "ItemHeight", 20);
                    return (x, y, width, height);
                }

            case CampaignPreviousMissionsPage contents:
                return contents.RowBox(row);
            case CampaignScrapbookPage book:
                if (book.ScrapOf(row) is { } scrap)
                {
                    return ScrapBox(scrap);
                }

                if (book.ArtOf(row) is not { } drawn)
                {
                    return null;
                }

                var frame = PlaqueSizeOf(drawn.Art);
                return (drawn.X, drawn.Y, frame.Width, frame.Height);
            default:
                return null;
        }
    }

    // A scrap's clickable region: the one SCRAPBOOK.CSV authors, else the fixed region a capture
    // stands in, else the shipped image's own bounds. Null where the file is not there to measure,
    // which leaves the row keyboard-only rather than hit at a guessed size.
    private (float X, float Y, float Width, float Height)? ScrapBox(ScrapbookScrap scrap)
    {
        if (scrap.Region is { } region)
        {
            return region;
        }

        if (scrap.IsCapture)
        {
            return (scrap.X, scrap.Y, CaptureRegionWidth, CaptureRegionHeight);
        }

        if (Measure($"SCRAPBOOK/{scrap.FileName}") is { } size)
        {
            return (scrap.X, scrap.Y, size.Width, size.Height);
        }

        return null;
    }

    // A plaque's one-frame size: the strip measured where the file is a rof bitmap, the shipped
    // size for the briefing's rimage plaque, the roster's CONTINUE strip as the fallback otherwise.
    private (float Width, float Height) PlaqueSizeOf(BoardArt art)
    {
        if (art.Library == BoardArtLibrary.Rimage)
        {
            return (BriefPlaqueWidth, BriefPlaqueHeight);
        }

        return StripSize(art, 113f, 34f);
    }

    private int RowIndexOf(string key)
    {
        var rows = Rows;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Key == key)
            {
                return i;
            }
        }

        return -1;
    }

    private void HighlightComboEntry(OriginalRow row)
    {
        if (OpenCombo is { } combo && Entry(row.Key) is { } entry)
        {
            combo.Move(entry - combo.Highlight);
        }
    }

    // The campaign's lists for the pointer: an open drop-down's list first, since it hangs over
    // the screen (the per-seat screen's too, which has no flow), then the contents' mission list.
    private void CampaignLists(List<OriginalList> lists)
    {
        if (OpenCombo is { } combo && CampaignBoards.ComboWindow(combo) is { } open)
        {
            lists.Add(new OriginalList(EntryKeyPrefix + "LIST", open, top => combo.ScrollTo(top)));
        }

        if (_flow == null)
        {
            return;
        }

        // The contents page pulls its window over the flow's cursor, which mirrors this graph's
        // focus only on a compose, so the mirror is brought up to date first.
        if (PageFocus >= 0)
        {
            _flow.FocusRow(PageFocus);
        }

        if (_flow.Page is CampaignPreviousMissionsPage contents && contents.PointerWindow is { } window)
        {
            lists.Add(new OriginalList("CONTENTS", window, top => ScrollContents(contents, top)));
        }
    }

    // The contents list's window moved to top: the page pulls its own cursor inside, and this
    // graph's focus takes the cursor back, so the window is not pulled home on the next compose.
    private void ScrollContents(CampaignPreviousMissionsPage contents, int top)
    {
        int focus = PageFocus;
        if (focus >= 0)
        {
            _flow!.FocusRow(focus);
        }

        contents.ScrollTo(top);
        if (focus >= 0)
        {
            _focus[(int)_screen] = _flow!.Row;
        }
    }

    private bool CloseCampaignCombo() => IsComboScreen && OpenCombo is { } combo && combo.Collapse();

    private bool MoveCampaignCombo(IReadOnlyList<OriginalRow> rows, int focus, int direction) =>
        IsComboScreen && OpenCombo is { } combo && combo.Move(direction);

    // A sideways step on a closed field picks its next entry, the page's own stepper, which the
    // plane selection may refuse with its dialog.
    private bool StepCampaignSideways(IReadOnlyList<OriginalRow> rows, int focus, int direction)
    {
        if (_flow == null || _dialog != null || focus < 0 || focus >= rows.Count || rows[focus].Kind != OriginalRowKind.Dropdown)
        {
            return false;
        }

        var before = _flow.Screen;
        _flow.FocusRow(focus);
        _flow.Page.Step(focus, direction);
        SyncAfterPage(before);
        return true;
    }

    // A press handed to the page: the page edits its own state, and whatever it named on the way
    // (a destination, a dialog, a refusal) is read off the host and re-entered through this graph.
    private void PagePress(int pageRow)
    {
        if (_flow == null)
        {
            return;
        }

        var before = _flow.Screen;
        _flow.FocusRow(pageRow);
        _flow.Page.Accept(pageRow);
        SyncAfterPage(before);
    }

    // One script step as this graph's own presses. A cursor verb is a command frame, so the focus,
    // the cues and every door out are the ones a player's press takes. x never arrives here, the
    // script carrying one having been refused whole.
    private void PressAidStep(CampaignAidScript.Step press)
    {
        if (press.Button != BoardButton.None)
        {
            PressBoardButton(press.Button);
            return;
        }

        switch (press.Verb)
        {
            case 'd':
                Step(new MenuCommands { MoveY = 1 });
                break;
            case 'u':
                Step(new MenuCommands { MoveY = -1 });
                break;
            case 'l':
                Step(new MenuCommands { MoveX = -1 });
                break;
            case 'r':
                Step(new MenuCommands { MoveX = 1 });
                break;
            case 'a':
                Step(new MenuCommands { Accept = true });
                break;
            case 'b':
                Step(new MenuCommands { Back = true });
                break;
        }
    }

    // A named button's own press: the row carrying it takes the focus and the confirm, which is
    // what clicking that plaque does. A screen without the button is left alone, and so is one with
    // a dialog standing, since the box's answers are the rows then.
    private void PressBoardButton(BoardButton button)
    {
        if (_flow == null || _dialog != null || !IsCampaignScreen)
        {
            return;
        }

        for (int row = 0; row < _flow.Page.RowCount; row++)
        {
            if (_flow.Page.Button(row).Button == button)
            {
                _focus[(int)_screen] = row;
                Step(new MenuCommands { Accept = true });
                return;
            }
        }
    }

    private void SyncAfterPage(CampaignScreen before)
    {
        if (_flow == null)
        {
            return;
        }

        if (_flow.TakeModal() is { } modal)
        {
            RaiseDialog(modal.Message, modal.Icon, Ok(modal.Confirm));
        }
        else if (_flow.TakeMessage() is { Length: > 0 } message)
        {
            // A refusal band raised as a box is the plane screen's langui 710 and the sell path's
            // 701, both of them 0x1 masks, so it takes the warning.
            RaiseDialog(message, DialogIcon.Warning, Ok());
        }

        if (_flow.Screen != before)
        {
            EnterFromPage(_flow.Screen, before);
        }
        else if (_dialog == null)
        {
            // The page may have moved the cursor onto the control just pressed (a page turn).
            _focus[(int)_screen] = _flow.Row;
        }
    }

    // The destination a page named, entered through this graph with its own return remembered:
    // a replay's briefing goes back to where it was pressed, the book remembers whether the
    // table of contents or the cabin opened it.
    private void EnterFromPage(CampaignScreen to, CampaignScreen from)
    {
        switch (to)
        {
            case CampaignScreen.Briefing:
                EnterBriefing(OriginalOf(from));
                break;
            case CampaignScreen.Scrapbook:
                if (from == CampaignScreen.PreviousMissions)
                {
                    _bookReturn = OriginalScreen.CampaignPreviousMissions;
                }
                else if (from is not (CampaignScreen.Scrapbook or CampaignScreen.ScrapbookZoom))
                {
                    _bookReturn = OriginalScreen.CampaignCabin;
                }

                ShowCampaign(OriginalScreen.CampaignScrapbook);
                break;
            case CampaignScreen.FlightCheck:
                // ACCEPT or CANCEL on the ammo and plane screens: back onto the plaque that opened them.
                ShowCampaign(OriginalScreen.CampaignFlightCheck, keepFocus: from is CampaignScreen.Ammo or CampaignScreen.PlaneSelection);
                break;
            default:
                ShowCampaign(OriginalOf(to));
                break;
        }
    }

    private MenuExit? ActivateCampaign(OriginalRow row)
    {
        if (_flow == null || _campaign == null)
        {
            return null;
        }

        if (Entry(row.Key) != null)
        {
            HighlightComboEntry(row);
            PagePress(PageFocus);
            return null;
        }

        int pageRow = RowIndexOf(row.Key);
        if (pageRow < 0)
        {
            return null;
        }

        // RETURN TO CABIN wherever it is pressed, one door on three screens. ⚠ Not the page's own
        // press through PagePress. A film defers the arrival, and SyncAfterPage mirrors only a move
        // already made, so a deferred one strands this graph on the screen just left.
        if (row.Key == nameof(BoardButton.ReturnToCabin))
        {
            OpenCabin();
            return null;
        }

        switch (_screen)
        {
            case OriginalScreen.CampaignRoster:
                ActivateRoster(row, pageRow);
                return null;
            case OriginalScreen.CampaignCabin:
                ActivateCabin(row);
                return null;
            case OriginalScreen.CampaignBriefing:
                ActivateBriefing(row);
                return null;
            case OriginalScreen.CampaignFlightCheck:
                return ActivateFlightCheck(row);
            default:
                PagePress(pageRow);
                return null;
        }
    }

    // The profile screen: the box and CONTINUE both start on the name in the box (Enter in the
    // box is the script's own commit path), a roster row fills the box and a second press on the
    // filled row starts (the double-click), DELETE PLAYER asks, CANCEL leaves.
    private void ActivateRoster(OriginalRow row, int pageRow)
    {
        if (RosterEntry is not { } entry || _campaign == null)
        {
            return;
        }

        switch (row.Key)
        {
            case nameof(BoardButton.Continue):
                ContinuePlayer();
                return;
            case nameof(BoardButton.DeletePlayer):
                BeginDelete();
                return;
            case nameof(BoardButton.CancelProfile):
                LeaveCampaign();
                return;
        }

        if (pageRow == 0)
        {
            ContinuePlayer();
            return;
        }

        int index = pageRow - 1;
        if (index >= 0 && index < _campaign.Roster.Count)
        {
            string name = _campaign.Roster[index];
            if (entry.Text == name)
            {
                ContinuePlayer();
                return;
            }

            entry.Set(name);
        }
    }

    private void ContinuePlayer()
    {
        if (_campaign == null || RosterEntry is not { } entry)
        {
            return;
        }

        if (_campaign.ContinuePlayer(entry.Text) is { } refusal)
        {
            // CAMPAIGN.SCRIPT raises the missing-name and unknown-name refusals on the 0x1 mask.
            RaiseDialog(refusal, DialogIcon.Warning, Ok());
            return;
        }

        entry.Set(_campaign.Profile?.Name ?? entry.Text.Trim());
        OpenCabin();
    }

    // Every door onto the cabin, inside the campaign and out: CONTINUE above, the flight return and
    // the screenshot aids through ShowCabin, and each screen's RETURN TO CABIN and back press. The
    // seated profile's chapter cinema plays first where one is due, and the cabin opens on the frame
    // the film stops. A position inside a chapter, and a shell with no cinema (every suite), opens
    // the cabin straight away. keepFocus is the way back's, landing on the plaque that was left.
    private void OpenCabin(bool keepFocus = false)
    {
        if (_campaign?.ChapterCinema is { } cinema && _campaign.Profile is { } seated)
        {
            cinema.OpenCabin(seated, () => ShowCampaign(OriginalScreen.CampaignCabin, keepFocus));
            return;
        }

        ShowCampaign(OriginalScreen.CampaignCabin, keepFocus);
    }

    // A way back that may land on the cabin, which is then a cabin door with its film in front.
    private void BackTo(OriginalScreen screen)
    {
        if (screen == OriginalScreen.CampaignCabin)
        {
            OpenCabin(keepFocus: true);
            return;
        }

        ShowCampaign(screen, keepFocus: true);
    }

    // The book as a finished mission leaves it, with the cabin on its far side. Separate from
    // ShowScrapbook so the closing cinema can defer it to the frame the film stops.
    private void OpenBook(int seq)
    {
        _campaign?.EnterScrapbook(seq);
        _bookReturn = OriginalScreen.CampaignCabin;
        ShowCampaign(OriginalScreen.CampaignScrapbook);
    }

    // DELETE PLAYER: the original's question (langui 201) as the two-answer box, on CAMPAIGN.SCRIPT's
    // 0x4 mask and so under the query icon, the deletion taking the profile's own directory alone
    // and clearing the box.
    private void BeginDelete()
    {
        if (_campaign == null || RosterEntry is not { } entry)
        {
            return;
        }

        string name = entry.Text.Trim();
        if (name.Length == 0 || !_campaign.HasPlayer(name))
        {
            RaiseDialog($"There is no player named \"{name}\".", DialogIcon.Warning, Ok());
            return;
        }

        RaiseDialog(
            _campaign.Strings.Text(201, "Are you sure you want to delete this player and all associated saved games?"),
            DialogIcon.Query,
            Yes(() =>
            {
                _campaign.DeletePlayer(name);
                entry.Set(string.Empty);
                _focus[(int)_screen] = 0;
            }),
            No());
    }

    private void ActivateCabin(OriginalRow row)
    {
        if (_campaign?.Profile is not { } profile)
        {
            LeaveCampaign();
            return;
        }

        switch (row.Key)
        {
            case nameof(BoardButton.NextMission):
                _campaign.SetMission(CampaignProgression.NextMissionSeq(profile));
                EnterBriefing(OriginalScreen.CampaignCabin);
                break;
            case nameof(BoardButton.PreviousMissions):
                ShowCampaign(OriginalScreen.CampaignPreviousMissions);
                break;
            case nameof(BoardButton.PlaneConstruction):
                OpenHangar(_campaign.Wallet());
                break;
            case nameof(BoardButton.ReturnToMainMenu):
                LeaveCampaign();
                break;
        }
    }

    private void ActivateBriefing(OriginalRow row)
    {
        switch (row.Key)
        {
            case nameof(BoardButton.ReplayBriefing):
                _campaign?.Briefing?.Restart();
                break;
            case nameof(BoardButton.GoToFlightCheck):
                ShowCampaign(OriginalScreen.CampaignFlightCheck);
                break;
        }
    }

    // The flight check: CHANGE AMMO and CHANGE PLANE name their crew slot and open their screen,
    // RETURN TO BRIEFING abandons the walk, and FLY MISSION advances to the next joined human's
    // check or, on the last, leaves as the feature's launch with every seat's devices.
    private MenuExit? ActivateFlightCheck(OriginalRow row)
    {
        if (_campaign == null || _flow == null)
        {
            return null;
        }

        int pageRow = RowIndexOf(row.Key);
        var reference = pageRow >= 0 ? _flow.Page.Button(pageRow) : BoardButtonRef.None;
        switch (reference.Button)
        {
            case BoardButton.ChangeAmmo:
                _campaign.SetAmmoSlot(reference.Slot);
                ShowCampaign(OriginalScreen.CampaignAmmo);
                return null;
            case BoardButton.ChangePlane:
                _campaign.SetPlaneSlot(reference.Slot);
                ShowCampaign(OriginalScreen.CampaignPlaneSelection);
                return null;
            case BoardButton.ReturnToBriefing:
                _campaign.Field.Rewind();
                ShowCampaign(OriginalScreen.CampaignBriefing);
                return null;
            case BoardButton.FlyMission:
                _campaign.Field.SetPlayers(_setup.Seats.Count);
                if (_campaign.Field.Advance())
                {
                    ShowCampaign(OriginalScreen.CampaignFlightCheck);
                    return null;
                }

                var pads = new List<IReadOnlyList<int>>(_setup.Seats.Count);
                foreach (var seat in _setup.Seats)
                {
                    pads.Add(_flightDevices(seat));
                }

                return _campaign.BuildExit(pads);
            default:
                return null;
        }
    }

    // Back through the campaign's own graph: an open list closes, a guest's check retreats to the
    // player before, and each screen returns to the one that opened it, the profile screen
    // leaving the campaign.
    private void BackCampaign()
    {
        if (_flow == null || _campaign == null)
        {
            return;
        }

        switch (_screen)
        {
            case OriginalScreen.CampaignRoster:
                LeaveCampaign();
                break;
            case OriginalScreen.CampaignCabin:
                ShowCampaign(OriginalScreen.CampaignRoster, keepFocus: true);
                break;
            case OriginalScreen.CampaignPreviousMissions:
                BackTo(OriginalScreen.CampaignCabin);
                break;
            case OriginalScreen.CampaignBriefing:
                BackTo(_briefingReturn);
                break;
            case OriginalScreen.CampaignFlightCheck:
                if (_campaign.Field.Retreat())
                {
                    ShowCampaign(OriginalScreen.CampaignFlightCheck);
                }
                else
                {
                    _campaign.Field.Rewind();
                    ShowCampaign(OriginalScreen.CampaignBriefing, keepFocus: true);
                }

                break;
            case OriginalScreen.CampaignAmmo:
            case OriginalScreen.CampaignPlaneSelection:
                // The page's own Back: an open list closes, else the working copy is dropped or
                // the picks restored, and the screen falls back to the check.
                _flow.FocusRow(Math.Max(0, PageFocus));
                if (!_flow.Page.Back())
                {
                    ShowCampaign(OriginalScreen.CampaignFlightCheck, keepFocus: true);
                }

                break;
            case OriginalScreen.CampaignScrapbook:
                BackTo(_bookReturn);
                break;
            case OriginalScreen.CampaignScrapbookZoom:
                ShowCampaign(OriginalScreen.CampaignScrapbook, keepFocus: true);
                break;
        }
    }

    // The screen as the shared board component composes it over the page, with this graph's own
    // additions: the roster's list colours and its box's words, and a standing dialog.
    private void ComposeCampaign(
        IReadOnlyList<OriginalRow> rows, int focus, List<BoardPicture> backdrop, List<BoardPicture> pictures,
        List<BoardFill> fills, List<BoardStroke> strokes, List<BoardLine> lines, List<BoardPlaque> plaques,
        List<BoardNote> notes, List<BoardPanel> overlays)
    {
        if (_flow == null)
        {
            return;
        }

        var page = _flow.Page;
        int pageFocus = PageFocus;
        if (pageFocus >= 0)
        {
            _flow.FocusRow(pageFocus);
        }

        bool pressed = _dialog == null && _pressed >= 0 && _pressed == focus;
        string detail = page.Screen == CampaignScreen.Ammo && pageFocus >= 0 ? page.Detail(pageFocus) : string.Empty;
        var board = CampaignBoards.For(page, pageFocus, pressed, detail, null, _campaignLayout);
        backdrop.AddRange(board.Backdrop);
        fills.AddRange(board.Fills);
        pictures.AddRange(board.Pictures);
        strokes.AddRange(board.Strokes);
        plaques.AddRange(board.Plaques);
        notes.AddRange(board.Notes);
        overlays.AddRange(board.Overlays);
        if (page.Screen == CampaignScreen.Roster)
        {
            ComposeRoster(rows, focus, board.Lines, fills, lines);
        }
        else
        {
            lines.AddRange(board.Lines);
        }

        if (CampaignSeatPanel() is { } strip)
        {
            overlays.Add(strip);
        }

        if (_dialog != null)
        {
            overlays.Add(ComposeDialog(rows, focus));
        }
    }

    // The profile screen's own list drawing, CAMPAIGN.SCRIPT's sub-script: the selection bar
    // behind the row the box names and the frame around the row under the pointer, over the
    // list rows the board component wrote; the box shows the typed name itself with a caret while
    // it is the focused row.
    private void ComposeRoster(IReadOnlyList<OriginalRow> rows, int focus, IReadOnlyList<BoardLine> composed, List<BoardFill> fills, List<BoardLine> lines)
    {
        string name = RosterName;
        int picked = -1;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Kind == OriginalRowKind.ListRow && rows[i].Label.TrimStart('✓', ' ') == name && name.Length > 0)
            {
                picked = i;
            }
        }

        if (picked >= 0)
        {
            var bar = rows[picked];
            fills.Add(new BoardFill(bar.X, bar.Y, bar.Width, bar.Height, RosterBarRed, 0, 0));
        }

        if (_dialog == null && _hover >= 0 && _hover < rows.Count && rows[_hover].Kind == OriginalRowKind.ListRow)
        {
            var framed = rows[_hover];
            fills.Add(new BoardFill(framed.X, framed.Y, framed.Width, framed.Height, RosterFrameRed, 0, 0, Border: true));
        }

        foreach (var line in composed)
        {
            if (line.Row == 0)
            {
                bool caret = _dialog == null && focus == 0;
                lines.Add(line with { Text = caret ? name + "_" : name });
            }
            else if (line.Row > 0 && line.Text.StartsWith("✓ ", StringComparison.Ordinal))
            {
                lines.Add(line with { Text = line.Text[2..] });
            }
            else
            {
                lines.Add(line);
            }
        }
    }
}
