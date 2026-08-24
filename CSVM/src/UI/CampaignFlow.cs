using System;
using System.Collections.Generic;
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

    /// <summary>The mission briefing.</summary>
    Briefing,

    /// <summary>The pilot's and wingmen's planes and loadouts before the flight.</summary>
    FlightCheck,

    /// <summary>Ammunition and ordnance for one aircraft.</summary>
    Ammo,
}

/// <summary>How a campaign flow ended, or that it is still running.</summary>
public enum CampaignExit
{
    /// <summary>Still on a screen.</summary>
    None,

    /// <summary>Left the campaign for the launchscreen.</summary>
    Cancelled,
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

    /// <summary>A second, smaller picture for the focused row, or null for none.</summary>
    HangarArt? RowArt(int row);

    /// <summary>Row <paramref name="row"/>'s text.</summary>
    string RowText(int row);

    /// <summary>The detail line under the list for the focused row, or "" for none.</summary>
    string Detail(int row);

    /// <summary>The horizontal stepper on the focused row. Returns whether anything changed.</summary>
    bool Step(int row, int dir);

    /// <summary>The confirm press on the focused row. Returns whether the page handled it.</summary>
    bool Accept(int row);

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
        [CampaignScreen.Cabin] = flow => new CampaignCabinPlaceholderPage(flow),
    };

    private readonly Dictionary<CampaignScreen, ICampaignPage> _pages = new();

    // The screens entered, innermost last. Never empty: popping the last one ends the flow.
    private readonly List<CampaignScreen> _stack = new() { CampaignScreen.Roster };

    /// <summary>Opens a flow over <paramref name="store"/>, reading its roster once.
    /// <paramref name="dataRoot"/> may be null; a page's art then simply loads none.</summary>
    public CampaignFlow(CampaignProfileStore store, UiStrings strings, string? dataRoot = null)
    {
        Store = store;
        Strings = strings;
        DataRoot = dataRoot;
        Roster = store.List();
    }

    /// <summary>The profile store this flow creates, reads and deletes through.</summary>
    public CampaignProfileStore Store { get; }

    /// <summary>The langui table the screens label themselves from.</summary>
    public UiStrings Strings { get; }

    /// <summary>The folder <c>extracted/</c> sits in, or null when the caller has none.</summary>
    public string? DataRoot { get; }

    /// <summary>Every stored profile's name, re-read by <see cref="RefreshRoster"/>.</summary>
    public IReadOnlyList<string> Roster { get; private set; }

    /// <summary>The profile the player picked, or null while the roster screen is still open.</summary>
    public CampaignProfileDef? Profile { get; private set; }

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

    /// <summary>Whether the keyboard's letters are text right now rather than navigation. The shell
    /// reads this to decide whether to hand over its cursor axes or the pad's alone.</summary>
    public bool CapturesText => Page.TextEntry is { Active: true };

    /// <summary>Whether a screen has a page of its own yet.</summary>
    public static bool HasPage(CampaignScreen screen) => Registry.ContainsKey(screen);

    /// <summary>Moves the row cursor, wrapping like every other launchscreen list. While a text
    /// field is armed the same axis grows and shrinks the name instead, which is how a pad enters
    /// one at all.</summary>
    public bool Move(int dir)
    {
        if (Page.TextEntry is { Active: true } entry)
        {
            return dir < 0 ? entry.Append() : entry.Backspace();
        }

        int count = Page.RowCount;
        if (dir == 0 || count <= 1)
        {
            return false;
        }

        Message = string.Empty;
        Row = (((Row + dir) % count) + count) % count;
        return true;
    }

    /// <summary>Applies the horizontal stepper: the armed text field's letter control, else the
    /// focused row's own stepper.</summary>
    public bool Step(int dir)
    {
        if (dir == 0)
        {
            return false;
        }

        if (Page.TextEntry is { Active: true } entry)
        {
            return entry.StepLast(dir);
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
        Message = string.Empty;
        return Page.Accept(ClampedRow());
    }

    /// <summary>The back press: the page first, then leaving the screen. Backing out of the first
    /// screen ends the flow.</summary>
    public bool Back()
    {
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

    /// <summary>Seats the profile every screen after the roster reads, and opens the cabin.</summary>
    public void SelectProfile(CampaignProfileDef profile)
    {
        Profile = profile;
        GoTo(CampaignScreen.Cabin);
    }

    /// <summary>Re-reads the store's roster, after a profile was created or deleted.</summary>
    public void RefreshRoster() => Roster = Store.List();

    /// <summary>Leaves a refusal on screen, in the original's own words where it has some.</summary>
    public void SetMessage(string message) => Message = message;

    /// <summary>Puts the cursor on a row, clamped into the page's current list.</summary>
    public void FocusRow(int row)
    {
        Row = Math.Max(0, row);
        ClampedRow();
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

    /// <summary>The flow this page belongs to.</summary>
    protected CampaignFlow Flow { get; }

    /// <inheritdoc/>
    public virtual HangarArt? RowArt(int row) => null;

    /// <inheritdoc/>
    public abstract string RowText(int row);

    /// <inheritdoc/>
    public virtual string Detail(int row) => string.Empty;

    /// <inheritdoc/>
    public virtual bool Step(int row, int dir) => false;

    /// <inheritdoc/>
    public virtual bool Accept(int row) => false;

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
