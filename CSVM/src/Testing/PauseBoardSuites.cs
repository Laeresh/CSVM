using System.Text;
using CSVM.Flight.Modes;
using CSVM.UI.Boards;
using CSVM.UI.Screens;
using Godot;

namespace CSVM.Testing;

/// <summary>The Built-in presentation's pause board, the one free flight raises. The real board
/// stands over a real <see cref="PauseState"/>, and its rows are pointed at through the hit test
/// the owner's mouse reaches them by.</summary>
internal static class PauseBoardSuites
{
    // The rows a board with every door wired carries, in the order its menu lists them.
    private const int ResumeRow = 0;
    private const int PhotoRow = 1;
    private const int PreferencesRow = 2;
    private const int RestartRow = 3;
    private const int ExitRow = 4;
    private const int RowCount = 5;

    [Suite("pause-board",
        "the Built-in pause board free flight raises, pointed at with the mouse: a real PauseBoard "
        + "follows PauseState.Changed with its cursor on Resume, a pointer already resting on a middle "
        + "row when the board appears (where a flight's released capture leaves it) leaves the cursor "
        + "on Resume, entering each row moves the shared cursor onto it, a press released on "
        + "Preferences or Photo Mode fires that row and leaves the halt and the board standing, a "
        + "press let go off its row fires nothing, a button still down through Reprime is not a "
        + "click, a click on Resume resumes and takes the board away, the board writes no mouse "
        + "mode of its own, and a pad pauser's board reads no pointer at all while the keyboard seat's "
        + "board reads the mouse")]
    internal static void PauseBoardPointer(TestContext ctx)
    {
        var report = new StringBuilder();
        var mode = Input.MouseMode;
        var layer = new CanvasLayer { Name = "pause_board_suite", Layer = HudLayers.Board };
        ctx.Host.AddChild(layer);
        var pause = new PauseState();
        var board = PauseBoard.Build(
            pause, exitsToMenu: true, _ => new MenuInput { Keyboard = false, Pads = System.Array.Empty<int>() });
        int photos = 0, preferences = 0, restarts = 0, exits = 0;
        board.PhotoMode = () => photos++;
        board.Preferences = () => preferences++;
        board.Restart = () => restarts++;
        board.Exit = () => exits++;
        layer.AddChild(board);
        try
        {
            ctx.Check(!board.Visible, $"the board is hidden before a pause");
            pause.TryToggle(0);
            ctx.Check(board.Visible, $"the pause raises the board");
            ctx.Same(ResumeRow, board.FocusedRow, $"the cursor rests on Resume");
            ctx.Check(board.PointerSource() == null,
                $"a pad pauser's board reads no pointer, the mouse being the keyboard seat's alone");
            ctx.Check(KeyboardSeatReadsPointer(layer),
                $"ABLE-TO-FAIL CONTROL: the keyboard seat's own board reads the mouse (mode {Input.MouseMode})");
            (float X, float Y, bool Pressed)? pointer = null;
            board.PointerSource = () => pointer;

            board._Process(0.0);
            Settle(board);
            if (!RowsLaidOut(ctx, board, report))
            {
                return;
            }

            (float X, float Y) At(int row) => (board.Rows!.RowRect(row).GetCenter().X, board.Rows!.RowRect(row).GetCenter().Y);

            pointer = (At(RestartRow).X, At(RestartRow).Y, false);
            board._Process(0.0);
            ctx.Same(ResumeRow, board.FocusedRow,
                $"a pointer already resting on Restart when the board appears leaves the cursor on Resume");

            int walked = 0;
            for (int row = RowCount - 1; row >= 0; row--)
            {
                pointer = (At(row).X, At(row).Y, false);
                board._Process(0.0);
                walked += board.FocusedRow == row ? 1 : 0;
            }

            ctx.Same(RowCount, walked, $"entering each row moves the shared cursor onto it");

            Click(board, ref pointer, At(PreferencesRow));
            ctx.Same(1, preferences, $"a press released on Preferences opens the options");
            ctx.Check(board.Visible && pause.Paused, $"and the board and the halt both stand");
            Click(board, ref pointer, At(PhotoRow));
            ctx.Same(1, photos, $"a press released on Photo Mode opens photo mode");
            ctx.Check(board.Visible && pause.Paused, $"and the board and the halt both stand");

            pointer = (At(ExitRow).X, At(ExitRow).Y, true);
            board._Process(0.0);
            pointer = (0f, 0f, true);
            board._Process(0.0);
            pointer = (0f, 0f, false);
            board._Process(0.0);
            ctx.Same(0, exits, $"a press on Exit let go off the row fires nothing");

            pointer = (At(RestartRow).X, At(RestartRow).Y, true);
            board.Reprime();
            board._Process(0.0);
            pointer = (At(RestartRow).X, At(RestartRow).Y, false);
            board._Process(0.0);
            ctx.Same(0, restarts, $"a button still down through Reprime is not a click on Restart");

            Click(board, ref pointer, At(ResumeRow));
            ctx.Check(!pause.Paused && !board.Visible, $"a click on Resume resumes and takes the board away");
            ctx.Same(0, exits + restarts, $"and nothing destructive fired on the way");
            ctx.Check(Input.MouseMode == mode,
                $"the board wrote no mouse mode of its own, the flight owning the capture ({Input.MouseMode}, was {mode})");
            report.AppendLine(
                $"walked {walked} rows; fired preferences={preferences} photo={photos} restart={restarts} exit={exits}");
        }
        finally
        {
            layer.RemoveChild(board);
            board.QueueFree();
            ctx.Host.RemoveChild(layer);
            layer.QueueFree();
            Input.MouseMode = mode;
        }

        ctx.WriteArtifact($"test-pause-board.txt", report.ToString());
        ctx.Note($"pointed at the Built-in pause board's rows over a live pause state");
    }

    // A container sorts its children on a deferred call, which a suite inside one frame never
    // reaches. So every anchored control is resized and every container sorted, parents first.
    internal static void Settle(Control node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is not Control control)
            {
                continue;
            }

            if (node is not Container)
            {
                control.OffsetTop += 1f;
                control.OffsetTop -= 1f;
            }

            if (control is Container container)
            {
                container.Notification((int)Container.NotificationSortChildren);
            }

            Settle(control);
        }
    }

    // A board over its own pause state whose pauser holds the keyboard seat, read once while it
    // stands. The harness never captures the mouse, so that seat's pointer has a position to report.
    private static bool KeyboardSeatReadsPointer(Node parent)
    {
        var pause = new PauseState();
        var board = PauseBoard.Build(pause, exitsToMenu: true, _ => new MenuInput { Keyboard = true });
        parent.AddChild(board);
        try
        {
            pause.TryToggle(0);
            return board.PointerSource() != null;
        }
        finally
        {
            pause.ForceResume();
            parent.RemoveChild(board);
            board.QueueFree();
        }
    }

    // One press and release on the same point, a frame each, the way a player clicks.
    private static void Click(PauseBoard board, ref (float X, float Y, bool Pressed)? pointer, (float X, float Y) at)
    {
        pointer = (at.X, at.Y, true);
        board._Process(0.0);
        pointer = (at.X, at.Y, false);
        board._Process(0.0);
    }

    // Every row stands on a line of its own, top to bottom, so a point is on one row at most.
    private static bool RowsLaidOut(TestContext ctx, PauseBoard board, StringBuilder report)
    {
        if (board.Rows is not { } rows)
        {
            ctx.Check(false, $"the raised board carries its menu");
            return false;
        }

        int stacked = 0;
        for (int row = 0; row < RowCount; row++)
        {
            var rect = rows.RowRect(row);
            report.AppendLine($"row {row}: {rect}");
            bool below = row == 0 || rect.Position.Y >= rows.RowRect(row - 1).End.Y;
            stacked += rect.Size.X > 0f && rect.Size.Y > 0f && below && rows.RowAt(rect.GetCenter().X, rect.GetCenter().Y) == row ? 1 : 0;
        }

        ctx.Same(RowCount, stacked, $"the five rows lay out one under another and each answers the hit test for its own middle");
        ctx.Same(-1, rows.RowAt(0f, 0f), $"the window's corner is on no row");
        return stacked == RowCount;
    }
}
