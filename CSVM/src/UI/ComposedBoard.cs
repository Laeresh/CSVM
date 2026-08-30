using System;
using System.Collections.Generic;

namespace CSVM.UI;

/// <summary>Which library a board bitmap comes from. The two extractions keep different naming:
/// the mission art is lowercase under <c>rimage</c>, the screen chrome keeps the authored
/// filename under <c>rof</c>.</summary>
public enum BoardArtLibrary
{
    /// <summary>Mission art, <c>extracted/rimage/&lt;name&gt;.png</c>, named without its extension.</summary>
    Rimage,

    /// <summary>Screen chrome, <c>extracted/rof/ASSETS/GRAPHICS/&lt;name&gt;</c>, named with it.</summary>
    Ui,
}

/// <summary>How a piece of board text is inked. The authored colours are per widget and mostly
/// undecoded, so a board names the role and the renderer owns the palette.</summary>
public enum BoardInk
{
    /// <summary>A list row nobody is on.</summary>
    Row,

    /// <summary>The list row the cursor is on.</summary>
    RowFocused,

    /// <summary>A heading over a list.</summary>
    Heading,

    /// <summary>The line under the list describing the focused row, and the controls hint.</summary>
    Detail,

    /// <summary>A button label the pointer is not on (<c>BtnLabelNormal</c>).</summary>
    LabelNormal,

    /// <summary>A button label under the cursor (<c>BtnLabelRollover</c>).</summary>
    LabelRollover,

    /// <summary>A button label being pressed (<c>BtnLabelActivate</c>).</summary>
    LabelActivate,
}

/// <summary>The screen buttons the campaign boards carry, one member per authored widget. A page
/// names one of these per row so the board can draw that row as its own plaque instead of a text
/// line; the geometry for each is in <see cref="CampaignBoards"/>.</summary>
public enum BoardButton
{
    /// <summary>Not a button: the row is list text.</summary>
    None,

    /// <summary><c>CM_B_START</c>, the profile screen's CONTINUE.</summary>
    Continue,

    /// <summary><c>CM_B_DELETEPLAYER</c>.</summary>
    DeletePlayer,

    /// <summary><c>CM_B_CANCEL</c>.</summary>
    CancelProfile,

    /// <summary><c>PC_B_NEWMISSION</c>.</summary>
    NextMission,

    /// <summary><c>PC_B_PREVIOUS</c>.</summary>
    PreviousMissions,

    /// <summary><c>PC_B_PLANEX</c>.</summary>
    PlaneConstruction,

    /// <summary><c>PC_B_RETURNMM</c>.</summary>
    ReturnToMainMenu,

    /// <summary><c>SBTOC_B_VIEW</c>.</summary>
    ViewMission,

    /// <summary><c>SBTOC_B_REPLAY</c>.</summary>
    ReplayMission,

    /// <summary><c>SBTOC_B_RETURN</c>, also the briefing's RETURN TO CABIN.</summary>
    ReturnToCabin,

    /// <summary>The briefing's <c>REPLAY</c>.</summary>
    ReplayBriefing,

    /// <summary>The briefing's <c>FLIGHTCHECK</c>.</summary>
    GoToFlightCheck,

    /// <summary><c>FC_B_CHANGEPLANE</c>, one per crew slot.</summary>
    ChangePlane,

    /// <summary><c>FC_B_CHANGEAMMO</c>, one per crew slot.</summary>
    ChangeAmmo,

    /// <summary><c>FC_B_RETURNBRIEF</c>.</summary>
    ReturnToBriefing,

    /// <summary><c>FC_B_FLYMISSION</c>.</summary>
    FlyMission,

    /// <summary><c>OL_B_ACCEPT</c>.</summary>
    AcceptLoadout,

    /// <summary><c>OL_B_CANCEL</c>.</summary>
    CancelLoadout,

    /// <summary><c>SBZ_B_RETURN</c>, the scrap detail view's close button (D19).</summary>
    CloseZoom,

    /// <summary><c>sb_b_prev</c>, the book's page-back arrow.</summary>
    ScrapbookPrev,

    /// <summary><c>sb_b_next</c>, the book's page-forward arrow.</summary>
    ScrapbookNext,

    /// <summary><c>sb_b_current</c>, the Current Mission bookmark.</summary>
    CurrentMission,
}

/// <summary>One bitmap a board draws, and how many stacked frames it holds. A button strip is four
/// frames (disabled, normal, rollover, depressed, in that order); everything else is one.</summary>
public sealed record BoardArt(BoardArtLibrary Library, string Name, int Frames = 1);

/// <summary>A picture placed at its authored pixel position. <paramref name="Centered"/> is the
/// briefing script's own <c>center</c> flag: the coordinate is the middle, not the top left.</summary>
public sealed record BoardPicture(
    BoardArt Art, float X, float Y, int Frame = 0, bool Centered = false,
    float Opacity = 1f, float Revs = 0f, float Width = 0f, float Height = 0f);

/// <summary>A straight connector line between two authored points in its authored colour, which
/// is the briefing script's <c>Line</c> opcode and the only non-picture element any board draws.
/// <paramref name="Opacity"/> is the element's, so the line fades in with everything else.</summary>
public sealed record BoardStroke(
    float X1, float Y1, float X2, float Y2, byte R, byte G, byte B, float Opacity = 1f);

/// <summary>One button plaque: its art strip, its authored top-left, the page row it presses, the
/// strip frame to draw and the label to write over it. <see cref="Label"/> is empty where the art
/// bakes its own words in, which every screen but the briefing and the paper buttons does.</summary>
public sealed record BoardPlaque(
    BoardArt Art, float X, float Y, int Row, int Frame, string Label, BoardInk Ink);

/// <summary>One line of board text at an authored position, wrapped to <paramref name="Width"/>
/// (0 for no wrap). <paramref name="Row"/> is the page row it stands for, or -1 for chrome.
/// <paramref name="Italic"/> is the <c>I</c> of a langui row's own <c>[FONTID]</c> tag, which is
/// how the original names a slanted face; the renderer decides what it draws that with.</summary>
public sealed record BoardLine(
    string Text, float X, float Y, float Width, float Size, BoardInk Ink, int Row = -1,
    bool Italic = false);

/// <summary>
/// A list widget's entries and the box they flow inside, in authored pixels: the briefing
/// parchment's own <c>LIST</c> is <c>POSITION [35, 335]</c>, <c>WORDWRAP [185, 240]</c>,
/// <c>SPACING [5]</c> (<c>docs/formats/briefing.md</c>). It is a layer of its own rather than one
/// <see cref="BoardLine"/> per entry because how tall an entry draws is a font metric, which the
/// engine-free half does not hold, so where the next entry starts cannot be composed here.
/// </summary>
public sealed record BoardNote(
    IReadOnlyList<string> Entries, float X, float Y, float Width, float Height, float Spacing,
    float Size, BoardInk Ink)
{
    /// <summary>The entries as placed lines, stacked from the widget's top-left and stopped at its
    /// authored height. <paramref name="height"/> measures one entry wrapped to a width, in
    /// authored pixels; a fixed pitch instead draws a wrapped entry over the one under it.</summary>
    public IReadOnlyList<BoardLine> Flow(Func<string, float, float> height)
    {
        var lines = new List<BoardLine>();
        float top = Y;
        foreach (var entry in Entries)
        {
            float tall = height(entry, Width);
            if (top + tall > Y + Height)
            {
                break;
            }

            lines.Add(new BoardLine(entry, X, top, Width, Size, Ink));
            top += tall + Spacing;
        }

        return lines;
    }
}

/// <summary>
/// One campaign screen composed in the original's 800x600 dialog space: the pictures under it, the
/// text on it and the button plaques over it, all at authored pixel positions. Engine-free, so
/// what a screen is made of tests off engine; <see cref="ComposedBoardView"/> is only its renderer
/// and <see cref="BoardFit"/> is how the space meets the window.
/// </summary>
public sealed class ComposedBoard
{
    /// <summary>Builds a board out of its three ordered layers.</summary>
    public ComposedBoard(
        IReadOnlyList<BoardPicture> pictures,
        IReadOnlyList<BoardStroke> strokes,
        IReadOnlyList<BoardLine> lines,
        IReadOnlyList<BoardPlaque> plaques,
        IReadOnlyList<BoardNote>? notes = null)
    {
        Pictures = pictures;
        Strokes = strokes;
        Lines = lines;
        Plaques = plaques;
        Notes = notes ?? Array.Empty<BoardNote>();
    }

    /// <summary>Pictures in draw order, background first.</summary>
    public IReadOnlyList<BoardPicture> Pictures { get; }

    /// <summary>Connector lines, drawn over the pictures.</summary>
    public IReadOnlyList<BoardStroke> Strokes { get; }

    /// <summary>Text over the pictures, in draw order.</summary>
    public IReadOnlyList<BoardLine> Lines { get; }

    /// <summary>Button plaques, drawn over everything.</summary>
    public IReadOnlyList<BoardPlaque> Plaques { get; }

    /// <summary>List widgets whose entries flow, drawn with the text.</summary>
    public IReadOnlyList<BoardNote> Notes { get; }

    /// <summary>The strip frame a plaque draws. A four-frame strip inks the state into the art
    /// itself; a one-frame plaque keeps frame 0 and changes its label's font instead, which is
    /// what the briefing's <c>BtnLabelNormal</c>/<c>Rollover</c>/<c>Activate</c> triad is.</summary>
    public static int PlaqueFrame(int frames, bool focused, bool pressed)
    {
        if (frames < 4)
        {
            return 0;
        }

        return pressed ? 3 : focused ? 2 : 1;
    }

    /// <summary>The ink a plaque's own label takes, which is the one-frame plaque's whole focus
    /// state.</summary>
    public static BoardInk PlaqueInk(bool focused, bool pressed) =>
        pressed ? BoardInk.LabelActivate : focused ? BoardInk.LabelRollover : BoardInk.LabelNormal;
}
