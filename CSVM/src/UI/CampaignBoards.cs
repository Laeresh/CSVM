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

    private static readonly BoardArt PaperButton = Ui("SB_B_PaperButton.png", StripFrames);
    private static readonly BoardArt ReturnToCabinArt = Ui("GN_B_ReturntoCabin.png", StripFrames);

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
            new BoardSlot(BoardButton.ReturnToCabin, 0, ReturnToCabinArt, 593, 561),
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
        var pictures = new List<BoardPicture>();
        if (Chrome.TryGetValue(page.Screen, out var chrome))
        {
            pictures.AddRange(chrome);
        }

        pictures.AddRange(page.Pictures);
        var lines = new List<BoardLine>(page.Captions);
        var plaques = new List<BoardPlaque>();
        var slots = Buttons.TryGetValue(page.Screen, out var found) ? found : Array.Empty<BoardSlot>();
        int listIndex = 0;
        for (int row = 0; row < page.RowCount; row++)
        {
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

        return new ComposedBoard(pictures, page.Strokes, lines, plaques, page.Notes);
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
            CampaignScreen.PreviousMissions => (420f, 140f + (index * 24f), 325f),
            // One heading per crew slot, at the PILOT and WINGMAN widgets.
            CampaignScreen.FlightCheck => (138f, index == 0 ? 102f : 320f, 400f),
            // Four gun groups down the ammunition panel, then eight pylons in two columns.
            CampaignScreen.Ammo => index < 4
                ? (142f, 105f + (index * 42f), 200f)
                : (index < 8 ? 135f : 410f, 320f + ((index - 4) % 4 * 28f), 152f),
            _ => (20f, 20f + (index * 20f), 400f),
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
