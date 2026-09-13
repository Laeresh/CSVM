using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;

namespace CSVM.UI.Menu.Original;

/// <summary>What a standalone screen module reads off the shell and calls back into it for: the
/// focus, pointer and dialog state tied to whatever screen is showing, the shared dialog-raising
/// and focus-setting seams, the shell-wide chrome a module draws over (the seat strip, the plate
/// pages' row rule), and the points where one family's flow crosses into another (the door a build
/// opens on, a return that resumes one of them, the hangar the Build button opens and the per-seat
/// walk FLY MISSION begins). Every module stands on it, and so does each module's test fake.</summary>
public interface IOriginalScreenHost
{
    /// <summary>The screen showing.</summary>
    OriginalScreen Screen { get; }

    /// <summary>Whether a dialog stands over the screen.</summary>
    bool DialogOpen { get; }

    /// <summary>The focused row's key, or "".</summary>
    string FocusedKey { get; }

    /// <summary>The showing screen's focus index, shared with every other family.</summary>
    int FocusedRow { get; set; }

    /// <summary>The row index a pointer press is holding, or -1.</summary>
    int PressedRow { get; }

    /// <summary>The row index the pointer stands on, or -1.</summary>
    int HoveredRow { get; }

    /// <summary>The pointer's last authored position, or null when the seat has none.</summary>
    (float X, float Y)? Pointer { get; }

    /// <summary>The campaign's own build store, where a purchase over the cabin's wallet writes.</summary>
    CustomPlaneStore? CampaignPlanes { get; }

    /// <summary>The string table the menu reads its words from, empty where none is loaded.</summary>
    UiStrings MenuStrings { get; }

    /// <summary>Whether a build can be made at all: a hangar feature and a build store together.</summary>
    bool CanBuildPlane { get; }

    /// <summary>Opens a screen directly, the shell's own graph switch.</summary>
    void Open(OriginalScreen screen);

    /// <summary>Puts the focus on the row carrying a key, when the current rows have it.</summary>
    void FocusKey(string key);

    /// <summary>Raises a messagebox over whatever screen is showing.</summary>
    void RaiseDialog(string message, DialogIcon icon, params OriginalDialogAnswer[] answers);

    /// <summary>An art name's strip pixel size, or null when the file is not there.</summary>
    (int Width, int Height)? Measure(string art);

    /// <summary>Re-reads the cabin's profile after a purchase or a sale through its wallet.</summary>
    void ResumeCampaign();

    /// <summary>Re-reads Instant Action's Pilot Plane list off the build store.</summary>
    void RefreshInstantActionRoster();

    /// <summary>Re-reads the sortie roster off the build store after it changes.</summary>
    void RefreshRosterFromStore();

    /// <summary>Opens the hangar wallet-free, Instant Action's Build Custom Plane.</summary>
    void OpenHangar();

    /// <summary>Starts the per-seat aircraft walk from Instant Action, the launch itself where
    /// nobody is left to pick.</summary>
    MenuExit? BeginSeatWalk();

    /// <summary>The seat strip drawn over a board once a second seat has joined, or null;
    /// <paramref name="onPaper"/> puts it on a light ground for a paper page.</summary>
    BoardPanel? SeatPanel(bool onPaper);

    /// <summary>Composes a row kind a module has no special drawing for, on the shell's own
    /// plate-page rule.</summary>
    void ComposeGenericRow(
        OriginalRow row, bool focused, bool pressed, int index,
        List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques, List<BoardPicture> pictures);
}

/// <summary>The other side of the same seam: what the shell calls on a module once
/// <see cref="OriginalShell.Screen"/> is one the module owns. The shell holds its modules as these
/// alone and asks each which screens it answers for, so every dispatch site names one module through
/// one lookup and a further family is one more entry rather than another range check.</summary>
public interface IOriginalScreenModule
{
    /// <summary>Whether a screen is one of this module's.</summary>
    bool Owns(OriginalScreen screen);

    /// <summary>The showing screen's rows, in focus order.</summary>
    void BuildRows(List<OriginalRow> rows);

    /// <summary>The showing screen's scrolling lists as the pointer sees them, the topmost first.</summary>
    void Lists(List<OriginalList> lists);

    /// <summary>A sideways step on the focused row where it changes a value there; false where it
    /// does not, so the step crosses columns instead.</summary>
    bool StepSideways(IReadOnlyList<OriginalRow> rows, int focus, int direction);

    /// <summary>Closes an open list of this module's and puts the focus back on its box; false when
    /// none is open.</summary>
    bool CloseDropdown();

    /// <summary>The showing screen's answer to an activated row.</summary>
    MenuExit? Activate(OriginalRow row);

    /// <summary>Back on the showing screen, true when the module answered it. False leaves the
    /// screen's own way back to the shell.</summary>
    bool Back();

    /// <summary>The showing screen as drawn, into the board's own layers.</summary>
    void Compose(
        IReadOnlyList<OriginalRow> rows, int focus, List<BoardPicture> backdrop, List<BoardPicture> pictures,
        List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques, List<BoardNote> notes,
        List<BoardPanel> overlays);
}
