using System;
using System.Collections.Generic;
using CSVM.UI.Boards;
using CSVM.UI.Campaign;

namespace CSVM.UI.Menu.Original;

/// <summary>One answer of a dialog standing over a screen. It carries its row key, the messagebox
/// button row it draws at, its words, and what answering it runs.</summary>
public sealed record OriginalDialogAnswer(string Key, string LayoutKey, string Label, Action? Run);

/// <summary>A dialog standing over a screen, the original's <c>messagebox.script</c> over whatever
/// screen is showing. It carries its words, the icon its message class draws, and its one, two or
/// three answers. It also carries the widget set it is drawn from, null for the shared <c>mb_</c>
/// box. While one stands the rows are its answers alone.</summary>
public sealed record OriginalDialog(
    string Message, DialogIcon Icon, IReadOnlyList<OriginalDialogAnswer> Answers,
    CampaignBoards.DialogChrome? Chrome = null);

/// <summary>
/// The shell's standing messagebox, the original's <c>messagebox.script</c> over whatever screen is
/// showing. It holds the box standing, the focus its raise took and the answers' rows at their
/// slots, and draws the box over the screen's picture. The shell owns the one instance and
/// answers for it. While a box stands its answers are the only rows, and Back takes the declining
/// one. The screen modules only raise, through <see cref="IOriginalScreenHost.RaiseDialog"/>. The
/// answers' words come from the string table (langui 100 to 103).
/// </summary>
public sealed class OriginalShellDialog
{
    /// <summary>A one-answer dialog's OK.</summary>
    public const string OkKey = "DIALOG:OK";

    /// <summary>A two-answer dialog's confirming answer.</summary>
    public const string YesKey = "DIALOG:YES";

    /// <summary>A two-answer dialog's declining answer.</summary>
    public const string NoKey = "DIALOG:NO";

    /// <summary>A three-answer dialog's third answer, which leaves the screen as it was.</summary>
    public const string CancelKey = "DIALOG:CANCEL";

    // How far outside an answer's own rectangle its focus mark stands. The strip fills its frame
    // corner to corner but for the pill's rounded ends. A mark on the rectangle itself would be
    // drawn under the art and lost. Three pixels clear puts it on the box's black ground.
    private const float MarkOutset = 3f;

    private readonly CampaignLayout _layout;
    private readonly Func<string, (int Width, int Height)?> _measure;
    private readonly Func<CSVM.Mech3.UiStrings> _strings;
    private readonly Func<OriginalRow, float, BoardFill> _mark;

    /// <summary>The box over <paramref name="layout"/>'s messagebox chrome. Its answer plaques are
    /// sized through <paramref name="measure"/>, its words read off <paramref name="strings"/>, and
    /// the cursor's answer outlined by <paramref name="mark"/> that many pixels clear of it.</summary>
    public OriginalShellDialog(
        CampaignLayout layout,
        Func<string, (int Width, int Height)?> measure,
        Func<CSVM.Mech3.UiStrings> strings,
        Func<OriginalRow, float, BoardFill> mark)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _measure = measure ?? throw new ArgumentNullException(nameof(measure));
        _strings = strings ?? throw new ArgumentNullException(nameof(strings));
        _mark = mark ?? throw new ArgumentNullException(nameof(mark));
    }

    /// <summary>The dialog standing over the screen, or null.</summary>
    public OriginalDialog? Standing { get; private set; }

    /// <summary>The focus the last raise took, or -1 before any raise. The screen under the box
    /// still draws itself from it, and an answer puts it back.</summary>
    public int FocusBefore { get; private set; } = -1;

    /// <summary>A one-answer box's OK, in the messagebox script's own word (langui 100).</summary>
    public OriginalDialogAnswer Ok(Action? run = null) =>
        new(OkKey, CampaignBoards.DialogCenterKey, Word(100, "OK"), run);

    /// <summary>Stands a box over the screen, remembering <paramref name="focus"/> as the focus the
    /// raise took. Every raise names its icon, because <c>MESSAGEBOX.SCRIPT</c> reads the frame off
    /// the raising screen's button mask rather than off anything the box itself can see.</summary>
    public void Raise(
        CampaignBoards.DialogChrome? chrome, string message, DialogIcon icon, int focus,
        IReadOnlyList<OriginalDialogAnswer> answers)
    {
        FocusBefore = focus;
        Standing = new OriginalDialog(message, icon, answers, chrome);
    }

    /// <summary>Takes the box down on the answer keyed <paramref name="key"/>, which is null where
    /// the box carries none. False when no box stood.</summary>
    public bool Take(string key, out OriginalDialogAnswer? answer)
    {
        answer = null;
        if (Standing is not { } dialog)
        {
            return false;
        }

        Standing = null;
        foreach (var candidate in dialog.Answers)
        {
            if (candidate.Key == key)
            {
                answer = candidate;
                break;
            }
        }

        return true;
    }

    /// <summary>Drops the standing box unanswered, which a door onto a new screen does.</summary>
    public void Close() => Standing = null;

    /// <summary>The standing box's answers, at the messagebox rows they draw on, whatever screen it
    /// stands over; none while no box stands.</summary>
    public List<OriginalRow> Rows()
    {
        var rows = new List<OriginalRow>();
        if (Standing is not { } dialog)
        {
            return rows;
        }

        foreach (var answer in dialog.Answers)
        {
            var (art, x, y) = CampaignBoards.DialogSlot(answer.LayoutKey, _layout, dialog.Chrome);
            var size = OriginalWidgets.PlaqueSizeOf(art, _measure);
            rows.Add(new OriginalRow(answer.Key, answer.Label, OriginalRowKind.Button, x, y, size.Width, size.Height, true, 0, art));
        }

        return rows;
    }

    /// <summary>The standing box as the shared board component's messagebox panel, over
    /// <paramref name="rows"/> (its answers) with the cursor on <paramref name="focus"/>, the
    /// pointer's <paramref name="hover"/> and <paramref name="pressed"/> rows and its position.
    /// ⚠ Do not take the strip frame off the focus. Every raise focuses an answer, so the frame would
    /// stand on the default answer for the life of the box (docs/org/campaign-board.md).</summary>
    public void Compose(
        IReadOnlyList<OriginalRow> rows, int focus, int hover, int pressed, (float X, float Y)? pointer,
        List<BoardPanel> overlays)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(overlays);
        if (Standing is not { } dialog)
        {
            return;
        }

        var buttons = new List<CampaignBoards.DialogButton>(dialog.Answers.Count);
        var marks = new List<BoardFill>();
        for (int i = 0; i < dialog.Answers.Count; i++)
        {
            // The hit is re-checked rather than trusted. The hover index outlives the frame that
            // set it, and the answers are not the rows it was measured against.
            var row = i < rows.Count ? rows[i] : null;
            bool lit = i == hover && row != null && pointer is { } at && row.Contains(at.X, at.Y);
            bool held = i == pressed;
            if (i == focus && !lit && row != null)
            {
                marks.Add(_mark(row, MarkOutset));
            }

            // The ink is the box's own rather than the screen's, which on a paper screen would hide
            // the label on the dark strip.
            buttons.Add(new CampaignBoards.DialogButton(
                dialog.Answers[i].LayoutKey, dialog.Answers[i].Label,
                ComposedBoard.PlaqueFrame(4, lit, held), ComposedBoard.DialogInk(held)));
        }

        overlays.Add(CampaignBoards.Dialog(dialog.Message, buttons, dialog.Icon, _layout, dialog.Chrome));
        if (marks.Count > 0)
        {
            // The mark rides its own panel over the box rather than the box's fill layer. A panel
            // draws its fills before its pictures, and the messagebox's background covers the
            // whole panel. A mark inside it would be drawn and then painted over.
            overlays.Add(new BoardPanel(marks, Array.Empty<BoardPicture>(), Array.Empty<BoardLine>()));
        }
    }

    // The messagebox script's own answer words, read through whichever feature carries the string
    // table. The one-button box takes langui 100 (OK), the two-button pair 102 and 103.
    private string Word(int id, string fallback)
    {
        string word = _strings().Text(id, fallback);
        return word.Length > 0 ? word : fallback;
    }
}
