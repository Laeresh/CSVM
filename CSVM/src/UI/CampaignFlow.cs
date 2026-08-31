using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;

namespace CSVM.UI;

/// <summary>The campaign's out-of-mission screens, in the order the original walks them.</summary>
public enum CampaignScreen
{
    /// <summary>Pick, create or delete a player profile.</summary>
    Roster,

    /// <summary>The cabin hub the rest of the campaign hangs off.</summary>
    Cabin,

    /// <summary>The scrapbook's finished-missions list, opened from the cabin.</summary>
    PreviousMissions,

    /// <summary>The mission briefing.</summary>
    Briefing,

    /// <summary>The pilot's and wingmen's planes and loadouts before the flight.</summary>
    FlightCheck,

    /// <summary>Ammunition and ordnance for one aircraft.</summary>
    Ammo,

    /// <summary>The aircraft each crew slot flies, picked from the profile's own.</summary>
    PlaneSelection,

    /// <summary>The scrapbook's results page (spread 1), opened on the mission a finished mission
    /// just flew, with the cabin on its far side.</summary>
    Scrapbook,

    /// <summary>One scrap's detail view, opened on <see cref="CampaignFlow.ZoomTarget"/> and
    /// closing back to <see cref="Scrapbook"/>.</summary>
    ScrapbookZoom,
}

/// <summary>How a campaign flow ended, or that it is still running.</summary>
public enum CampaignExit
{
    /// <summary>Still on a screen.</summary>
    None,

    /// <summary>Left the campaign for the launchscreen.</summary>
    Cancelled,

    /// <summary>Plane Construction: the shell opens the hangar over the profile's wallet and calls
    /// <see cref="CampaignFlow.Resume"/> when it closes, which lands back on the cabin.</summary>
    OpenHangar,

    /// <summary>FLY MISSION: the shell launches the campaign session for
    /// <see cref="CampaignFlow.MissionSeq"/> over <see cref="CampaignFlow.Profile"/>.</summary>
    FlyMission,
}

/// <summary>
/// One campaign screen, as the shell draws and drives it. Everything is plain text and plain
/// indices, so a page is engine-free and testable: the shell owns every Godot control. Navigation
/// is the page's own (<see cref="Accept"/> calls <see cref="CampaignFlow.GoTo"/>), unlike the
/// hangar's linear screen order; <see cref="Back"/> returning false lets the flow leave the screen.
/// A page with a <see cref="TextEntry"/> takes the keyboard's letters as text while that field is
/// armed, and the flow's cursor axes then edit the name instead of the list.
/// </summary>
public interface ICampaignPage
{
    /// <summary>Which screen this page is.</summary>
    CampaignScreen Screen { get; }

    /// <summary>The heading.</summary>
    string Title { get; }

    /// <summary>How many rows the page draws right now.</summary>
    int RowCount { get; }

    /// <summary>The row the cursor lands on when the flow arrives.</summary>
    int OpeningRow { get; }

    /// <summary>The controls line for this screen's current state.</summary>
    string Footer { get; }

    /// <summary>The picture the shell draws beside the rows, or null for none.</summary>
    HangarArt? Art { get; }

    /// <summary>The field this page types into, or null when it has none.</summary>
    CampaignTextEntry? TextEntry { get; }

    /// <summary>The pictures this page's own data decides, under the screen's fixed chrome and in
    /// draw order: the screen background where the page picks it, the photograph behind the cabin
    /// window, the briefing's flags. Positions are authored 800x600 pixels.</summary>
    IReadOnlyList<BoardPicture> Pictures { get; }

    /// <summary>The connector lines this page's data draws over its pictures.</summary>
    IReadOnlyList<BoardStroke> Strokes { get; }

    /// <summary>Rectangles this page paints on the screen's background and under its own pictures:
    /// a list widget's selection bar and its scrollbar track, which are what the original draws
    /// with <c>ldrawrect</c> rather than with art.</summary>
    IReadOnlyList<BoardFill> Fills { get; }

    /// <summary>Text the screen carries that is not a row: a panel heading, a title widget.</summary>
    IReadOnlyList<BoardLine> Captions { get; }

    /// <summary>List widgets whose entries flow rather than sitting at a fixed pitch: the briefing
    /// parchment's objectives note, whose lines are read and never selected.</summary>
    IReadOnlyList<BoardNote> Notes { get; }

    /// <summary>Which of the screen's authored buttons row <paramref name="row"/> presses.
    /// <see cref="BoardButton.None"/> means the row is list text, which is the default.</summary>
    BoardButtonRef Button(int row);

    /// <summary>The drop-down list row <paramref name="row"/> is, or null when it is not one. ⚠ A
    /// combo may only be open while its own row is focused: the flow finds the open one by asking
    /// the focused row, so a page that leaves one open behind a moved cursor strands it.</summary>
    CampaignCombo? Combo(int row);

    /// <summary>A second, smaller picture for the focused row, or null for none.</summary>
    HangarArt? RowArt(int row);

    /// <summary>Whether the cursor may stand on row <paramref name="row"/>. False makes the row
    /// text the screen draws and never selects: the flight check's crew headings name the aircraft
    /// under the cursor's reach rather than being a thing to press.</summary>
    bool Focusable(int row);

    /// <summary>Row <paramref name="row"/>'s text.</summary>
    string RowText(int row);

    /// <summary>The detail line under the list for the focused row, or "" for none.</summary>
    string Detail(int row);

    /// <summary>The horizontal stepper on the focused row. Returns whether anything changed.</summary>
    bool Step(int row, int dir);

    /// <summary>The confirm press on the focused row. Returns whether the page handled it.</summary>
    bool Accept(int row);

    /// <summary>The secondary press on the focused row (X), the one shortcut a screen offers beside
    /// its confirm. Returns whether the page handled it; a screen with no such shortcut says
    /// false and the press is nothing.</summary>
    bool Secondary(int row);

    /// <summary>The back press. Returns true when the page consumed it (a confirm stage closing, a
    /// text field disarming); false lets the flow leave the screen.</summary>
    bool Back();
}

/// <summary>
/// The campaign's out-of-mission flow: the screens between the launchscreen and a mission, over one
/// selected <see cref="CampaignProfileDef"/>. Engine-free, so the screen graph, the profile
/// lifecycle and the cancel semantics test off engine; the launchscreen is only its renderer and
/// input source. Screens are a stack rather than the hangar's fixed order, because the campaign's
/// own navigation is a graph: the cabin opens a briefing, the briefing opens a flight check, and
/// each of them returns to what opened it. A screen with no registered page draws a placeholder.
/// </summary>
public sealed class CampaignFlow
{
    /// <summary>The roster's capacity: the original's profile block is 24 slots
    /// (docs/formats/campaign-screens.md), which is the number langui 202 states when it refuses a
    /// new player.</summary>
    public const int MaxProfiles = 24;

    // Which page draws which screen. THIS is the wave's mount point: a new screen lands as one
    // page file plus one line here, and nothing in the launchscreen changes.
    private static readonly Dictionary<CampaignScreen, Func<CampaignFlow, ICampaignPage>> Registry = new()
    {
        [CampaignScreen.Roster] = flow => new CampaignRosterPage(flow),
        [CampaignScreen.Cabin] = flow => new CampaignCabinPage(flow),
        [CampaignScreen.PreviousMissions] = flow => new CampaignPreviousMissionsPage(flow),
        [CampaignScreen.Briefing] = flow => new CampaignBriefingPage(flow),
        [CampaignScreen.FlightCheck] = flow => new CampaignFlightCheckPage(flow),
        [CampaignScreen.Ammo] = flow => new CampaignAmmoPage(flow),
        [CampaignScreen.PlaneSelection] = flow => new CampaignPlaneSelectionPage(flow),
        [CampaignScreen.Scrapbook] = flow => new CampaignScrapbookPage(flow),
        [CampaignScreen.ScrapbookZoom] = flow => new CampaignScrapbookZoomPage(flow),
    };

    private readonly Dictionary<CampaignScreen, ICampaignPage> _pages = new();

    // The screens entered, innermost last. Never empty: popping the last one ends the flow.
    private readonly List<CampaignScreen> _stack = new() { CampaignScreen.Roster };

    // The mission read for MissionSeq, and which sequence that was. -2 is "not read for any", which
    // no MissionSeq ever is: the cabin's own default is -1.
    private CampaignMission? _mission;
    private int _missionSeq = -2;

    /// <summary>Opens a flow over <paramref name="store"/>, reading its roster once.
    /// <paramref name="dataRoot"/> may be null; a page's art then simply loads none.
    /// <paramref name="planes"/> is the hangar's build store the flight check and ammo screens
    /// read an owned plane's guns and hardpoints from; null (every off-engine caller) means no
    /// hangar build exists and each plane reads as its airframe's stock fit, which is also what
    /// the two profile-seeded starters are.</summary>
    public CampaignFlow(CampaignProfileStore store, UiStrings strings, string? dataRoot = null,
        CustomPlaneStore? planes = null, StockLoadouts? stock = null)
    {
        Store = store;
        Strings = strings;
        DataRoot = dataRoot;
        Planes = planes;
        Stock = stock;
        Field = new CampaignFlightField(this);
        Roster = store.List();
        // The opening screen gets its own page's opening row too, not just the screens arrived at
        // later, or the roster would be the one screen that ignores the seam.
        Row = Page.OpeningRow;
        ClampedRow();
    }

    /// <summary>The stock-loadout table (<c>stock_loadouts.json</c>) the flight check and ammo
    /// screens read an airframe's stock fit and the ordnance roster from, or null when the caller
    /// has none; a page loads the default itself only when it needs it, since the default path
    /// is <c>res://</c> and needs the engine.</summary>
    public StockLoadouts? Stock { get; }

    /// <summary>The profile store this flow creates, reads and deletes through.</summary>
    public CampaignProfileStore Store { get; }

    /// <summary>The hangar's build store (<c>user://Planes/</c>), or null when the caller has none.
    /// Pages never open the store themselves: that call needs the engine, and a page must stay
    /// constructible off it.</summary>
    public CustomPlaneStore? Planes { get; }

    /// <summary>The langui table the screens label themselves from.</summary>
    public UiStrings Strings { get; }

    /// <summary>The humans flying this sortie: how many joined, whose flight check is showing, and
    /// what each guest picked. Solo until the shell says otherwise.</summary>
    public CampaignFlightField Field { get; }

    /// <summary>The folder <c>extracted/</c> sits in, or null when the caller has none.</summary>
    public string? DataRoot { get; }

    /// <summary>Every stored profile's name, re-read by <see cref="RefreshRoster"/>.</summary>
    public IReadOnlyList<string> Roster { get; private set; }

    /// <summary>The profile the player picked, or null while the roster screen is still open.</summary>
    public CampaignProfileDef? Profile { get; private set; }

    /// <summary>The <c>cm_sequence.zrd</c> index (0..23) of the mission the briefing, flight check
    /// and ammo screens are about: the profile's next mission after Next Mission, any finished one
    /// after Previous Missions. -1 until the cabin sets it.</summary>
    public int MissionSeq { get; private set; } = -1;

    /// <summary>Whose aircraft the ammo screen edits: 0 the pilot's, 1 the wingman's (the flight
    /// check's two rows, <c>docs/formats/campaign-screens.md</c>). The flight check sets it before
    /// opening the ammo screen.</summary>
    public int AmmoSlot { get; private set; }

    /// <summary>Which crew slot the plane selection screen opens focused on, 0 the pilot's combo
    /// and 1 the wingman's.</summary>
    public int PlaneSlot { get; private set; }

    /// <summary>How many times the book has been opened through <see cref="OpenScrapbook"/>. The
    /// page watches this rather than <see cref="MissionSeq"/> alone, so reopening it on the mission
    /// it is already browsing still lands on that mission's spread 1, which is what the original's
    /// <c>uiData</c> 2405 mode 1 does however the book is reached.</summary>
    public int ScrapbookEntry { get; private set; }

    /// <summary>The scrap <see cref="CampaignScreen.ScrapbookZoom"/> is open on: the
    /// <c>SCRAPBOOK.CSV</c> mission slot, spread and item a scrapbook page's row named. Null
    /// until <see cref="SetScrapbookZoom"/> is called.</summary>
    public (int Mission, int Spread, int Item)? ZoomTarget { get; private set; }

    /// <summary>The <c>cm_sequence.zrd</c> entry <see cref="MissionSeq"/> names, or null when the
    /// data root, the file or the entry is unavailable. Read once per mission rather than once per
    /// repaint, and here rather than on a page because more than one screen asks: the flight check
    /// and the plane selection screen both draw a wingman only when this mission carries one.</summary>
    public CampaignMission? Mission
    {
        get
        {
            if (_missionSeq != MissionSeq)
            {
                _missionSeq = MissionSeq;
                _mission = ReadMission();
            }

            return _mission;
        }
    }

    /// <summary>Whether this mission flies a wingman, its <c>cm_sequence</c> flag.</summary>
    public bool MissionHasWingman => Mission?.Wingman ?? false;

    /// <summary>The screen showing.</summary>
    public CampaignScreen Screen => _stack[^1];

    /// <summary>The page drawing the current screen.</summary>
    public ICampaignPage Page => PageFor(Screen);

    /// <summary>The focused row on that screen.</summary>
    public int Row { get; private set; }

    /// <summary>Whether the flow is still running, and how it ended if not.</summary>
    public CampaignExit Exit { get; private set; }

    /// <summary>The refusal line the last action left, or "". Cleared by any navigation.</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>The dialog standing over the screen, or null. While one stands it takes every
    /// press, so the screen under it neither moves nor changes.</summary>
    public CampaignModal? Modal { get; private set; }

    /// <summary>Whether the keyboard's letters are text right now rather than navigation. The shell
    /// reads this to decide whether to hand over its cursor axes or the pad's alone.</summary>
    public bool CapturesText => Page.TextEntry is { Active: true };

    /// <summary>The drop-down list standing open, or null. Only the focused row's own combo can be
    /// one, which is the invariant <see cref="ICampaignPage.Combo"/> states.</summary>
    public CampaignCombo? OpenCombo =>
        Page.Combo(Math.Clamp(Row, 0, Math.Max(0, Page.RowCount - 1))) is { Open: true } combo
            ? combo
            : null;

    /// <summary>Whether a screen has a page of its own yet.</summary>
    public static bool HasPage(CampaignScreen screen) => Registry.ContainsKey(screen);

    /// <summary>Moves the row cursor, wrapping like every other launchscreen list. While a text
    /// field is armed the same axis grows and shrinks the name instead, which is how a pad enters
    /// one at all.</summary>
    public bool Move(int dir)
    {
        if (Modal != null)
        {
            return false;
        }

        if (Page.TextEntry is { Active: true } entry)
        {
            return dir < 0 ? entry.Append() : entry.Backspace();
        }

        // An open drop-down owns the axis: the cursor is inside the list, not on the screen's rows.
        if (OpenCombo is { } combo)
        {
            Message = string.Empty;
            return combo.Move(dir);
        }

        int count = Page.RowCount;
        if (dir == 0 || count <= 1)
        {
            return false;
        }

        Message = string.Empty;
        Row = (((Row + dir) % count) + count) % count;
        Settle(dir);
        return true;
    }

    /// <summary>Applies the horizontal stepper: the armed text field's letter control, else the
    /// focused row's own stepper.</summary>
    public bool Step(int dir)
    {
        if (dir == 0 || Modal != null)
        {
            return false;
        }

        if (Page.TextEntry is { Active: true } entry)
        {
            return entry.StepLast(dir);
        }

        // The stepper is the closed field's shortcut. Inside an open list it means nothing, and
        // stepping the pick under the cursor would leave the two disagreeing.
        if (OpenCombo != null)
        {
            return false;
        }

        Message = string.Empty;
        return Page.Step(ClampedRow(), dir);
    }

    /// <summary>Types characters into the page's armed text field.</summary>
    public bool Type(string chars)
    {
        if (Page.TextEntry is not { Active: true } entry || chars.Length == 0)
        {
            return false;
        }

        Message = string.Empty;
        return entry.Type(chars);
    }

    /// <summary>Removes the last character of the page's armed text field.</summary>
    public bool Backspace() =>
        Page.TextEntry is { Active: true } entry && entry.Backspace();

    /// <summary>The confirm press on the focused row, which is the page's alone: campaign screens
    /// navigate by naming where they go, not by advancing through a fixed order.</summary>
    public bool Accept()
    {
        // A dialog takes the confirm. The press that raised one cannot also answer it: a page
        // raises from inside its own Accept, which this check has already passed by then.
        if (DismissModal())
        {
            return true;
        }

        Message = string.Empty;
        return Page.Accept(ClampedRow());
    }

    /// <summary>The secondary press, the pad's X: the one shortcut the screen showing offers beside
    /// its confirm, and nothing at all on a screen that offers none. A refusal already on the line
    /// survives a press no page took, since nothing happened for it to be stale about.</summary>
    public bool Secondary()
    {
        if (Modal != null)
        {
            return false;
        }

        if (!Page.Secondary(ClampedRow()))
        {
            return false;
        }

        Message = string.Empty;
        return true;
    }

    /// <summary>The back press: the page first, then leaving the screen. Backing out of the first
    /// screen ends the flow.</summary>
    public bool Back()
    {
        if (DismissModal())
        {
            return true;
        }

        Message = string.Empty;
        if (Page.Back())
        {
            return true;
        }

        if (_stack.Count <= 1)
        {
            Exit = CampaignExit.Cancelled;
            return true;
        }

        _stack.RemoveAt(_stack.Count - 1);
        Row = Page.OpeningRow;
        ClampedRow();
        return true;
    }

    /// <summary>Opens a screen. One already open is returned to, closing everything entered after
    /// it, so RETURN TO CABIN from a briefing leaves no second cabin behind the first.</summary>
    public void GoTo(CampaignScreen screen)
    {
        Message = string.Empty;
        int at = _stack.IndexOf(screen);
        if (at >= 0)
        {
            _stack.RemoveRange(at + 1, _stack.Count - at - 1);
        }
        else
        {
            _stack.Add(screen);
        }

        Row = Page.OpeningRow;
        ClampedRow();
    }

    /// <summary>Ends the flow for the launchscreen, the CANCEL and RETURN TO MAIN MENU press.</summary>
    public void Cancel() => Exit = CampaignExit.Cancelled;

    /// <summary>Hands the shell a job that leaves the flow standing: the hangar, or the mission
    /// itself. The shell reads <see cref="Exit"/>, does the job, and (for the hangar) calls
    /// <see cref="Resume"/>.</summary>
    public void Request(CampaignExit job) => Exit = job;

    /// <summary>Back from a job the shell ran on the flow's behalf: the flow stands where it was,
    /// its roster and profile re-read so a hangar purchase or sale shows on the cabin.</summary>
    public void Resume()
    {
        Exit = CampaignExit.None;
        Message = string.Empty;
        if (Profile != null && Store.Load(Profile.Name) is { } fresh)
        {
            Profile = fresh;
        }
    }

    /// <summary>Names the mission the screens after the cabin are about.</summary>
    public void SetMission(int seq) => MissionSeq = seq;

    /// <summary>Points the ammo screen at the pilot's (0) or the wingman's (1) aircraft.</summary>
    public void SetAmmoSlot(int slot) => AmmoSlot = slot;

    /// <summary>Which crew slot's CHANGE PLANE press opened the plane selection screen, the
    /// original's <c>@globals@ZQ</c> of -1 and -2. The screen draws both slots either way; this
    /// only decides which of the two combos the cursor opens on.</summary>
    public void SetPlaneSlot(int slot) => PlaneSlot = slot;

    /// <summary>Where a scrapbook capture's file is for the seated profile, or null when there is
    /// none on disk. A <c>Snap_</c> row resolves against the profile's own directory rather than
    /// the asset library (<c>docs/formats/campaign-screens.md</c>, "The scrapbook"), which is the
    /// one thing the book draws that no extraction holds.</summary>
    public string? CapturePath(ScrapbookScrap scrap)
    {
        if (Profile is not { } profile)
        {
            return null;
        }

        string path = Path.Combine(Store.DirFor(profile.Name), scrap.FileName);
        return File.Exists(path) ? path : null;
    }

    /// <summary>Names the scrap <see cref="CampaignScreen.ScrapbookZoom"/> opens on.</summary>
    public void SetScrapbookZoom(int mission, int spread, int item) => ZoomTarget = (mission, spread, item);

    /// <summary>Opens the book on a mission's first spread, the original's <c>uiData</c> 2405 mode
    /// 1: the mission-end entry, the table of contents' VIEW SELECTED and both CURRENT MISSION
    /// bookmarks all take this door.</summary>
    public void OpenScrapbook(int seq)
    {
        SetMission(seq);
        ScrapbookEntry++;
        GoTo(CampaignScreen.Scrapbook);
    }

    /// <summary>Takes the joined-player count from the shell, once a frame.</summary>
    public void SetPlayers(int players) => Field.SetPlayers(players);

    /// <summary>The record the ammo screen is editing: a guest's own session-scoped aircraft while
    /// their flight check is the screen showing, else the seated profile's plane for
    /// <see cref="AmmoSlot"/>. Null when there is nothing to fit.</summary>
    public OwnedPlane? AmmoTarget()
    {
        if (Field.Current > 0)
        {
            return Field.Plane(Field.Current);
        }

        if (Profile is not { } profile)
        {
            return null;
        }

        int at = AmmoSlot == 0 ? profile.SelectedPlane : profile.WingmanPlane;
        return at >= 0 && at < profile.Planes.Count ? profile.Planes[at] : null;
    }

    /// <summary>Seats the profile every screen after the roster reads, and opens the cabin.</summary>
    public void SelectProfile(CampaignProfileDef profile)
    {
        Profile = profile;
        Store.RecordLastPlayed(profile.Name);
        GoTo(CampaignScreen.Cabin);
    }

    /// <summary>Re-reads the store's roster, after a profile was created or deleted.</summary>
    public void RefreshRoster() => Roster = Store.List();

    /// <summary>Leaves a refusal on screen, in the original's own words where it has some.</summary>
    public void SetMessage(string message) => Message = message;

    /// <summary>Raises a dialog over the screen showing. A second raise replaces the first rather
    /// than stacking: the original's own box is one script run over the screen, not a stack.</summary>
    public void RaiseModal(string message, string button = "OK", Action? confirmed = null) =>
        Modal = new CampaignModal(message, button, confirmed);

    /// <summary>Puts the cursor on a row, clamped into the page's current list.</summary>
    public void FocusRow(int row)
    {
        Row = Math.Max(0, row);
        ClampedRow();
    }

    // The sequence entry for MissionSeq. Absent data, an unreadable file or a sequence with no such
    // entry all read as null: a screen then draws no wingman rather than refusing to open.
    private CampaignMission? ReadMission()
    {
        if (DataRoot is not { } root)
        {
            return null;
        }

        try
        {
            string zrdrPath = SessionPaths.PreferUnzipped(Path.Combine(root, "extracted", "zrdr.zip"));
            foreach (var mission in CampaignSequence.Load(zrdrPath))
            {
                if (mission.Seq == MissionSeq)
                {
                    return mission;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FileNotFoundException)
        {
            return null;
        }

        return null;
    }

    // Answers a standing dialog, running whatever was to follow it. Both the confirm and the back
    // press take this door: a one-button dialog has one answer, so skipping the callback on one of
    // the two presses would make what happens next depend on which one the player used.
    private bool DismissModal()
    {
        if (Modal is not { } modal)
        {
            return false;
        }

        Modal = null;
        modal.Confirm();
        return true;
    }

    // The page for a screen, built on first sight and kept, so a page may hold state of its own
    // (a text field's contents, a confirm stage). An unregistered screen draws the placeholder.
    private ICampaignPage PageFor(CampaignScreen screen)
    {
        if (!_pages.TryGetValue(screen, out var page))
        {
            page = Registry.TryGetValue(screen, out var make)
                ? make(this)
                : new CampaignPlaceholderPage(this, screen);
            _pages[screen] = page;
        }

        return page;
    }

    // The focused row, kept inside the page's current list: a page whose row count shrank under the
    // cursor (a deleted profile, a confirm stage opening) must not index past its own end.
    private int ClampedRow()
    {
        Row = Math.Clamp(Row, 0, Math.Max(0, Page.RowCount - 1));
        return Settle(1);
    }

    // Walks the cursor along dir until it stands on a row the page takes focus on, one lap at most:
    // a page that refuses every row keeps the cursor where it was rather than spinning.
    private int Settle(int dir)
    {
        int count = Page.RowCount;
        for (int step = 0; step < count && !Page.Focusable(Row); step++)
        {
            Row = (((Row + (dir < 0 ? -1 : 1)) % count) + count) % count;
        }

        return Row;
    }
}

/// <summary>
/// The base every campaign page shares: its flow, and defaults for everything a plain list screen
/// does not need. A page overriding nothing but <see cref="Screen"/>, <see cref="RowCount"/> and
/// <see cref="RowText"/> is a legal screen that leaves on cancel and does nothing on confirm.
/// </summary>
public abstract class CampaignPage : ICampaignPage
{
    /// <summary>Binds the page to its flow.</summary>
    protected CampaignPage(CampaignFlow flow) => Flow = flow;

    /// <inheritdoc/>
    public abstract CampaignScreen Screen { get; }

    /// <inheritdoc/>
    public abstract string Title { get; }

    /// <inheritdoc/>
    public abstract int RowCount { get; }

    /// <inheritdoc/>
    public virtual int OpeningRow => 0;

    /// <inheritdoc/>
    public virtual string Footer => "↑↓  Choose       Enter / A  Select       Esc / B  Back";

    /// <inheritdoc/>
    public virtual HangarArt? Art => null;

    /// <inheritdoc/>
    public virtual CampaignTextEntry? TextEntry => null;

    /// <inheritdoc/>
    public virtual IReadOnlyList<BoardPicture> Pictures => Array.Empty<BoardPicture>();

    /// <inheritdoc/>
    public virtual IReadOnlyList<BoardStroke> Strokes => Array.Empty<BoardStroke>();

    /// <inheritdoc/>
    public virtual IReadOnlyList<BoardFill> Fills => Array.Empty<BoardFill>();

    /// <inheritdoc/>
    public virtual IReadOnlyList<BoardLine> Captions => Array.Empty<BoardLine>();

    /// <inheritdoc/>
    public virtual IReadOnlyList<BoardNote> Notes => Array.Empty<BoardNote>();

    /// <summary>The flow this page belongs to.</summary>
    protected CampaignFlow Flow { get; }

    /// <inheritdoc/>
    public virtual BoardButtonRef Button(int row) => BoardButtonRef.None;

    /// <inheritdoc/>
    public virtual CampaignCombo? Combo(int row) => null;

    /// <inheritdoc/>
    public virtual HangarArt? RowArt(int row) => null;

    /// <inheritdoc/>
    public virtual bool Focusable(int row) => true;

    /// <inheritdoc/>
    public abstract string RowText(int row);

    /// <inheritdoc/>
    public virtual string Detail(int row) => string.Empty;

    /// <inheritdoc/>
    public virtual bool Step(int row, int dir) => false;

    /// <inheritdoc/>
    public virtual bool Accept(int row) => false;

    /// <inheritdoc/>
    public virtual bool Secondary(int row) => false;

    /// <inheritdoc/>
    public virtual bool Back() => false;
}

/// <summary>
/// A screen with no page of its own: the right heading, one row saying which screen is missing, and
/// a cancel that leaves. It edits nothing, so a flow that walks through it changes no profile.
/// </summary>
public sealed class CampaignPlaceholderPage : CampaignPage
{
    private readonly CampaignScreen _screen;

    /// <summary>Binds the page to its flow and the screen it stands in for.</summary>
    public CampaignPlaceholderPage(CampaignFlow flow, CampaignScreen screen)
        : base(flow) => _screen = screen;

    /// <inheritdoc/>
    public override CampaignScreen Screen => _screen;

    /// <inheritdoc/>
    public override string Title => _screen.ToString().ToUpperInvariant();

    /// <inheritdoc/>
    public override int RowCount => 1;

    /// <inheritdoc/>
    public override string RowText(int row) => "Back";

    /// <inheritdoc/>
    public override string Detail(int row) => $"The {_screen} screen has no page yet.";
}
