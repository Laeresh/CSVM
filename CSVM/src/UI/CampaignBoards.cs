using System;
using System.Collections.Generic;

namespace CSVM.UI;

/// <summary>Which authored button a page row presses, and which of its repeats: the flight check
/// carries one CHANGE AMMO per crew slot, 0 the pilot and 1 the wingman.</summary>
public readonly record struct BoardButtonRef(BoardButton Button, int Slot = 0)
{
    /// <summary>A row that is list text rather than a button.</summary>
    public static readonly BoardButtonRef None = default;
}

/// <summary>
/// The authored geometry of the six campaign screens, and the composer that turns a page plus a
/// cursor into a <see cref="ComposedBoard"/>. Every coordinate here is the original's own, in the
/// 800x600 dialog space: the five script-driven screens from <c>ASSETS\LAYOUT.CSV</c>
/// (<c>docs/formats/campaign-screens.md</c>), the briefing from <c>Briefing.zrd</c>'s own chrome
/// (<c>docs/formats/briefing.md</c>). The handful the shipped layout leaves as unresolved authoring
/// macros were measured off the reference screenshots; <c>docs/org/campaign-board.md</c> says which
/// and how.
/// </summary>
public static class CampaignBoards
{
    // The original's own button strips: four stacked frames, disabled / normal / rollover /
    // depressed, in the column order LAYOUT.CSV's own colour fields carry them.
    private const int StripFrames = 4;

    // The list font, in authored pixels: the listbox row heights the layout carries are 20 to 24,
    // so a 14px face is what fits a row without touching the one under it.
    private const float ListFont = 14f;

    // The objectives note's own face. Its entries are written sentences in a 185-wide column, so
    // they take a smaller face than a listbox row does.
    private const float NoteFont = 11f;

    // A drop-down field's height and face: the sixteen pixels the reference screenshot draws
    // between one field's top edge and its bottom, and a face that fits inside them.
    private const float ComboFieldHeight = 16f;
    private const float ComboFont = 11f;

    // The arrow strip is three 16-pixel frames; the scroll arrows are four 11-pixel ones, which is
    // the same disabled/normal/rollover/depressed order every button strip uses.
    private const int ComboArrowFrames = 3;
    private const int ScrollArrowFrames = 4;
    private const float ComboArrowSize = 16f;

    // The scrollbar thumb's own art height, and where a field's words sit inside its box.
    private const float ScrollThumbHeight = 12f;
    private const float ComboTextInset = 4f;
    private const float ComboTextDrop = 1f;

    // A drop-down's own colours, measured off OriginalScreenshots/Campaign Flight Check Change
    // Plane Combo Box.png rather than decoded: LAYOUT.CSV's D rows carry art and item height but no
    // colour column, the engine's own list class drawing the field and the picked row's bar.
    private static readonly byte[] ComboPaper = { 200, 212, 230 };
    private static readonly byte[] ComboPicked = { 167, 185, 215 };

    private static readonly BoardArt ComboDownArrow = Ui("GN_B_ListboxarrowSMALLdown.png", ComboArrowFrames);
    private static readonly BoardArt ComboUpArrow = Ui("GN_B_ListboxarrowSMALLup.png", ComboArrowFrames);
    private static readonly BoardArt ScrollUp = Ui("FC_B_ScrollUp.png", ScrollArrowFrames);
    private static readonly BoardArt ScrollDown = Ui("FC_B_ScrollDown.png", ScrollArrowFrames);
    private static readonly BoardArt ScrollThumb = Ui("FC_B_ScrollBar.png");

    private static readonly BoardArt PaperButton = Ui("SB_B_PaperButton.png", StripFrames);
    private static readonly BoardArt ReturnToCabinArt = Ui("GN_B_ReturntoCabin.png", StripFrames);

    // The results card's two tabs share one strip. Which of them draws over the card and which
    // behind it is the whole of the selection, so the page draws the unselected one itself
    // (CampaignScrapbookPage.Pictures) and only the selected one reaches this slot table.
    private static readonly BoardArt StatCardTab = Ui("SB_B_Statcardtab.png", StripFrames);

    // SB_B_CURRENT and SBTOC_B_CURRENT are the same bookmark at the same place on both scrapbook
    // screens, and both open the book on the campaign's own current mission.
    private static readonly BoardArt CurrentMissionTab = Ui("SB_B_Currentmissiontab.png", StripFrames);

    // SB_B_NEXT, the book's forward page tab. The table of contents is a page of that same book, so
    // it takes the tab at the same place; [@ScrapBook_TOC@] authors no arrow of its own, leaving
    // that page with nothing a pad can turn forward by.
    private static readonly BoardArt MoreTab = Ui("SB_B_more_tab.png", StripFrames);

    // The briefing's plaque is the one that ships beside the mission art rather than with the
    // screen chrome, and the one that carries no words: its label is drawn over it in one of three
    // fonts, which is the whole of its focus state.
    private static readonly BoardArt BriefButton = new(BoardArtLibrary.Rimage, "brief_button1");

    // Where each screen's buttons sit, keyed by screen then by button and crew slot. Nothing here
    // is derived: it is the layout row's own X,Y, or the measurement docs/org/campaign-board.md
    // records where the shipped row leaves a macro unresolved.
    private static readonly Dictionary<CampaignScreen, BoardSlot[]> Buttons = new()
    {
        [CampaignScreen.Roster] = new[]
        {
            new BoardSlot(BoardButton.Continue, 0, Ui("CM_B_Start.png", StripFrames), 474, 293),
            new BoardSlot(BoardButton.DeletePlayer, 0, Ui("CM_B_DeletePlayer.png", StripFrames), 193, 547),
            new BoardSlot(BoardButton.CancelProfile, 0, Ui("CM_B_Cancel.png", StripFrames), 444, 547),
        },
        [CampaignScreen.Cabin] = new[]
        {
            new BoardSlot(BoardButton.NextMission, 0, Ui("PC_B_NextMission.png", StripFrames), 87, 504),
            new BoardSlot(BoardButton.PreviousMissions, 0, Ui("PC_B_PreviousMissions.png", StripFrames), 361, 539),
            new BoardSlot(BoardButton.PlaneConstruction, 0, Ui("PC_B_PlaneConstruction.png", StripFrames), 452, 431),
            new BoardSlot(BoardButton.ReturnToMainMenu, 0, Ui("PC_B_ReturnMainMenu.png", StripFrames), 593, 561),
        },
        [CampaignScreen.PreviousMissions] = new[]
        {
            new BoardSlot(BoardButton.ViewMission, 0, PaperButton, 440, 505, true),
            new BoardSlot(BoardButton.ReplayMission, 0, PaperButton, 596, 505, true),
            new BoardSlot(BoardButton.ScrapbookNext, 0, MoreTab, 708, 465),
            new BoardSlot(BoardButton.CurrentMission, 0, CurrentMissionTab, 558, 7, true),
            new BoardSlot(BoardButton.ReturnToCabin, 0, ReturnToCabinArt, 593, 561),
        },
        [CampaignScreen.Scrapbook] = new[]
        {
            new BoardSlot(BoardButton.ReplayMission, 0, PaperButton, 594, 505, true),
            new BoardSlot(BoardButton.ViewAllMissions, 0, Ui("SB_B_ViewAllMissions.png", StripFrames), 375, 560),
            new BoardSlot(BoardButton.ReturnToCabin, 0, ReturnToCabinArt, 593, 561),
            new BoardSlot(BoardButton.ScrapbookPrev, 0, Ui("SB_B_back_tab.png", StripFrames), 0, 465),
            new BoardSlot(BoardButton.ScrapbookNext, 0, MoreTab, 708, 465),
            new BoardSlot(BoardButton.CurrentMission, 0, CurrentMissionTab, 558, 7, true),
            new BoardSlot(BoardButton.BestTab, 0, StatCardTab, 432, 283, true),
            new BoardSlot(BoardButton.MostTab, 0, StatCardTab, 594, 283, true),
        },
        [CampaignScreen.ScrapbookZoom] = new[]
        {
            new BoardSlot(BoardButton.CloseZoom, 0, Ui("GN_B_Continue.png", StripFrames), 640, 519),
            new BoardSlot(BoardButton.ExportScrap, 0, Ui("SB_B_ExportToDesktop.png", StripFrames), 593, 561),
        },
        [CampaignScreen.Briefing] = new[]
        {
            new BoardSlot(BoardButton.ReplayBriefing, 0, BriefButton, 197, 560, true),
            new BoardSlot(BoardButton.ReturnToCabin, 0, BriefButton, 397, 560, true),
            new BoardSlot(BoardButton.GoToFlightCheck, 0, BriefButton, 597, 560, true),
        },
        [CampaignScreen.FlightCheck] = new[]
        {
            new BoardSlot(BoardButton.ChangePlane, 0, Ui("FC_B_PaperButton.png", StripFrames), 128, 131, true),
            new BoardSlot(BoardButton.ChangeAmmo, 0, Ui("FC_B_PaperButton.png", StripFrames), 274, 131, true),
            new BoardSlot(BoardButton.ChangePlane, 1, Ui("FC_B_PaperButton.png", StripFrames), 128, 349, true),
            new BoardSlot(BoardButton.ChangeAmmo, 1, Ui("FC_B_PaperButton.png", StripFrames), 274, 349, true),
            new BoardSlot(BoardButton.ReturnToBriefing, 0, Ui("FC_B_ReturnToBriefing.png", StripFrames), 341, 553),
            new BoardSlot(BoardButton.FlyMission, 0, Ui("FC_B_FlyMission.png", StripFrames), 551, 553),
        },
        [CampaignScreen.Ammo] = new[]
        {
            new BoardSlot(BoardButton.AcceptLoadout, 0, Ui("OL_B_AcceptLoadout.png", StripFrames), 341, 553),
            new BoardSlot(BoardButton.CancelLoadout, 0, Ui("OL_B_CancelLoadout.png", StripFrames), 551, 553),
        },
    };

    // The screen chrome that is neither the page's own art nor a button: the profile screen's
    // dialog panel and the briefing's objectives parchment, both at their authored positions.
    private static readonly Dictionary<CampaignScreen, BoardPicture[]> Chrome = new()
    {
        // The profile dialog stands over the main menu it opened from: the title mark over the
        // flag movie. The movie is not decoded and its shipped still is a placeholder, so the mark
        // stands alone and the ground behind it stays plain.
        [CampaignScreen.Roster] = new[]
        {
            new BoardPicture(Ui("MM_Logo.png"), 134, 13),
            new BoardPicture(Ui("CM_BackGround.png"), 193, 251),
        },
        [CampaignScreen.PreviousMissions] = new[] { new BoardPicture(Ui("SB_BackgroundTOC.jpg"), 0, 0) },
        [CampaignScreen.Scrapbook] = new[] { new BoardPicture(Ui("SB_BackGround.jpg"), 0, 0) },
        [CampaignScreen.FlightCheck] = new[] { new BoardPicture(Ui("FC_BackGround.jpg"), 0, 0) },
        [CampaignScreen.Ammo] = new[] { new BoardPicture(Ui("OL_BackGround.jpg"), 0, 0) },
    };

    /// <summary>The board for a page with the cursor on <paramref name="focusedRow"/>. A row the
    /// page names a button for becomes that button's plaque; every other row lists down the
    /// screen's own authored text slots. <paramref name="pressed"/> draws the focused plaque in
    /// its depressed frame for the frames a confirm is held.</summary>
    public static ComposedBoard For(
        ICampaignPage page, int focusedRow, bool pressed = false, string detail = "")
    {
        var backdrop = Chrome.TryGetValue(page.Screen, out var chrome)
            ? chrome
            : Array.Empty<BoardPicture>();
        var lines = new List<BoardLine>(page.Captions);
        var plaques = new List<BoardPlaque>();
        var fills = new List<BoardFill>(page.Fills);
        var pictures = new List<BoardPicture>(page.Pictures);
        var overlays = new List<BoardPanel>();
        var slots = Buttons.TryGetValue(page.Screen, out var found) ? found : Array.Empty<BoardSlot>();
        int listIndex = 0;
        for (int row = 0; row < page.RowCount; row++)
        {
            if (page.Combo(row) is { } combo)
            {
                ComposeCombo(combo, row == focusedRow, fills, pictures, lines, overlays);
                continue;
            }

            var reference = page.Button(row);
            if (reference.Button != BoardButton.None && Find(slots, reference) is { } slot)
            {
                bool focused = row == focusedRow;
                plaques.Add(new BoardPlaque(
                    slot.Art, slot.X, slot.Y, row,
                    ComposedBoard.PlaqueFrame(slot.Art.Frames, focused, focused && pressed),
                    slot.Labelled ? page.RowText(row) : string.Empty,
                    ComposedBoard.PlaqueInk(focused, focused && pressed)));
                continue;
            }

            var (x, y, width) = TextSlot(page.Screen, listIndex++);
            lines.Add(new BoardLine(
                page.RowText(row), x, y, width, ListFont,
                row == focusedRow ? BoardInk.RowFocused : BoardInk.Row, row));
        }

        if (detail.Length > 0 && DetailSlot(page.Screen) is { } note)
        {
            lines.Add(new BoardLine(detail, note.X, note.Y, note.Width, ListFont, BoardInk.Detail));
        }

        return new ComposedBoard(
            pictures, page.Strokes, lines, plaques, page.Notes, backdrop, fills, overlays);
    }

    /// <summary>Where one of a screen's authored buttons sits and what art it draws, for a page
    /// that has to draw that button itself rather than let it become a plaque: the results card's
    /// unselected tab, which the original puts behind the card. Null when the screen has no such
    /// button.</summary>
    public static (BoardArt Art, float X, float Y)? SlotOf(CampaignScreen screen, BoardButton button)
    {
        var slots = Buttons.TryGetValue(screen, out var found) ? found : Array.Empty<BoardSlot>();
        return Find(slots, new BoardButtonRef(button)) is { } slot ? (slot.Art, slot.X, slot.Y) : null;
    }

    /// <summary>The briefing parchment's objectives list, at the <c>LIST</c> widget's own authored
    /// geometry: <c>POSITION [35, 335]</c>, <c>WORDWRAP [185, 240]</c>, <c>SPACING [5]</c>. Its
    /// entries are written sentences in a narrow column, so they take a smaller face than a listbox
    /// row and a wrapped one pushes the next entry down instead of being given a fixed pitch.</summary>
    public static BoardNote ObjectivesNote(IReadOnlyList<string> entries) =>
        new(entries, 35f, 335f, 185f, 240f, 5f, NoteFont, BoardInk.Row);

    /// <summary>The authored panel a screen writes the focused row's description into, or null
    /// where the screen has none and the shell's own hint line has to carry it. The ammo screen has
    /// one, its description column, which is what that widget is for.</summary>
    public static (float X, float Y, float Width)? DetailSlot(CampaignScreen screen) => screen switch
    {
        CampaignScreen.Ammo => (566f, 92f, 172f),
        _ => null,
    };

    /// <summary>Where the <paramref name="index"/>-th list row of a screen sits, as x, y and wrap
    /// width in authored pixels. Each screen's own text widgets, walked in their authored order;
    /// a screen with more rows than widgets keeps stepping by the last one's line height.</summary>
    public static (float X, float Y, float Width) TextSlot(CampaignScreen screen, int index) =>
        screen switch
        {
            // The name field, then the roster listbox's seven visible rows.
            CampaignScreen.Roster => index == 0
                ? (243f, 297f, 221f)
                : (245f, 356f + ((index - 1) * 20f), 305f),
            // One heading per crew slot, at the PILOT and WINGMAN widgets.
            CampaignScreen.FlightCheck => (138f, index == 0 ? 102f : 320f, 400f),
            // Four gun groups down the ammunition panel, then eight pylons in two columns.
            CampaignScreen.Ammo => index < 4
                ? (142f, 105f + (index * 42f), 200f)
                : (index < 8 ? 135f : 410f, 320f + ((index - 4) % 4 * 28f), 152f),
            _ => (20f, 20f + (index * 20f), 400f),
        };

    // One drop-down: the field goes into the screen's own layers, and an open list goes into an
    // overlay instead, because it hangs across whatever the screen draws under it.
    private static void ComposeCombo(
        CampaignCombo combo, bool focused, List<BoardFill> fills, List<BoardPicture> pictures,
        List<BoardLine> lines, List<BoardPanel> overlays)
    {
        fills.AddRange(Box(combo.X, combo.Y, combo.Width, ComboFieldHeight, ComboPaper));
        pictures.Add(new BoardPicture(
            combo.Open ? ComboUpArrow : ComboDownArrow,
            combo.X + combo.Width - ComboArrowSize, combo.Y));
        lines.Add(FieldText(combo.Text, combo.X, combo.Y, combo.Width - ComboArrowSize,
            focused ? BoardInk.RowFocused : BoardInk.Row));
        if (combo.Open)
        {
            overlays.Add(OpenList(combo));
        }
    }

    // The open list under its field: the box, the picked row's bar, the visible entries, and the
    // scrollbar when the entries outrun the window.
    private static BoardPanel OpenList(CampaignCombo combo)
    {
        float top = combo.Y + ComboFieldHeight;
        float height = combo.Visible * combo.RowHeight;
        var fills = new List<BoardFill>(Box(combo.X, top, combo.Width, height, ComboPaper));
        var pictures = new List<BoardPicture>();
        var lines = new List<BoardLine>();
        for (int seen = 0; seen < combo.Visible; seen++)
        {
            int entry = combo.First + seen;
            if (entry >= combo.Entries.Count)
            {
                break;
            }

            float y = top + (seen * combo.RowHeight);
            if (entry == combo.Highlight)
            {
                fills.Add(new BoardFill(
                    combo.X + 1f, y, combo.Width - 2f, combo.RowHeight,
                    ComboPicked[0], ComboPicked[1], ComboPicked[2]));
            }

            lines.Add(FieldText(combo.Entries[entry], combo.X, y, combo.Width, BoardInk.Row));
        }

        if (combo.Scrolls)
        {
            float bar = combo.X + combo.Width - ComboArrowSize;
            pictures.Add(new BoardPicture(ScrollUp, bar, top, 1));
            pictures.Add(new BoardPicture(ScrollDown, bar, top + height - ComboArrowSize, 1));
            pictures.Add(new BoardPicture(ScrollThumb, bar, ThumbY(combo, top, height)));
        }

        return new BoardPanel(fills, pictures, lines);
    }

    // Where the thumb sits in the track between the two arrows: the window's own position in the
    // list, so a full list's thumb is at the bottom and an unscrolled one's is at the top.
    private static float ThumbY(CampaignCombo combo, float top, float height)
    {
        float track = height - (ComboArrowSize * 2f) - ScrollThumbHeight;
        int span = Math.Max(1, combo.Entries.Count - combo.RowsDisplayed);
        return top + ComboArrowSize + (Math.Max(0f, track) * combo.First / span);
    }

    // A field's words, inset from its left edge and sat on the row's own baseline the way the
    // reference draws them: the text is vertically centred in a 16-pixel field at an 11-pixel face.
    private static BoardLine FieldText(string text, float x, float y, float width, BoardInk ink) =>
        new(text, x + ComboTextInset, y + ComboTextDrop, width - ComboTextInset, ComboFont, ink);

    // A filled rectangle and its one-pixel outline, which is what every field and list box on these
    // screens is: the engine's own list class draws no art for either.
    private static BoardFill[] Box(float x, float y, float width, float height, byte[] paper) =>
        new[]
        {
            new BoardFill(x, y, width, height, paper[0], paper[1], paper[2]),
            new BoardFill(x, y, width, height, 0, 0, 0, 1f, Border: true),
        };

    private static BoardArt Ui(string name, int frames = 1) =>
        new(BoardArtLibrary.Ui, name, frames);

    private static BoardSlot? Find(BoardSlot[] slots, BoardButtonRef reference)
    {
        foreach (var slot in slots)
        {
            if (slot.Button == reference.Button && slot.Slot == reference.Slot)
            {
                return slot;
            }
        }

        return null;
    }

    // Labelled says the layout row carries a ResID, so the plaque's words are drawn over blank
    // art. Every screen-specific button bakes its own words into its strip instead.
    private readonly record struct BoardSlot(
        BoardButton Button, int Slot, BoardArt Art, float X, float Y, bool Labelled = false);
}
