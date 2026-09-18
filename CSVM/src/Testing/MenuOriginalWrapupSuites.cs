using System.Collections.Generic;
using System.Linq;
using CSVM.Session;
using CSVM.UI;
using CSVM.UI.Menu;
using CSVM.UI.Menu.BuiltIn;
using CSVM.UI.Menu.Original;

namespace CSVM.Testing;

/// <summary>
/// The Original presentation's Instant Action wrap-up page over the install's decoded layout: a
/// real <see cref="InstantActionRuntime"/> is ended and held, the frozen snapshot crosses the
/// session boundary the way the launcher hands it over, the page draws the four decoded rows and
/// the further lines the built-in board carries, and CONTINUE lands back on the Instant Action
/// screen. Readings: docs/formats/instant-action/wrap-up.md.
/// </summary>
internal static class MenuOriginalWrapupSuites
{
    [Suite("menu-original-wrapup",
        "Original's Instant Action wrap-up page end to end over the install's decoded layout: a real "
        + "runtime's ending freezes the four counters and holds, nothing leaves for the menu until "
        + "the hold ends, the handover then opens the decoded [@IA_WrapUp@] page with the magazine "
        + "spread, the four brushstrokes, the heading and the eight row lines off the snapshot, the "
        + "context and the stunt splits (without their total) on a yellow post-it and a ticked box "
        + "above CONTINUE, a later ending's numbers never reach the page that is already standing, a "
        + "failed run leaves the box empty, and CONTINUE and Back both return to the Instant Action "
        + "screen with the run forgotten")]
    internal static void MenuOriginalWrapup(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        var exits = new List<MenuExit>();
        var seat = new ScriptedSeat();
        var registry = new PresentationRegistry();
        registry.Register(PresentationId.BuiltIn, () => new BuiltInPresentation(
            ctx.Host, ctx.ZrdrPath, ctx.DataRoot, string.Empty, new MenuInput { Keyboard = true }));
        registry.Register(PresentationId.Original, () => new OriginalPresentation(
            ctx.Host, ctx.DataRoot, layout, string.Empty, new MenuInput { Keyboard = true }));
        var host = new MenuHost(registry, new MenuSuiteHost.SilentMenuAudio(), exits.Add);
        MenuSuiteHost.AddFeatures(host, ctx.DataRoot);
        host.AddSeat(seat);
        try
        {
            host.Select(forceBuiltIn: false, cliOverride: "original");
            host.Show(MenuReturnDestination.TopLevel);
            var shell = (host.Active as OriginalPresentation)?.Shell;
            ctx.Check(shell != null, $"Show activates Original ({host.Active?.Id})");
            if (shell == null)
            {
                return;
            }

            var size = ctx.Host.GetViewport().GetVisibleRect().Size;
            var fit = BoardFit.For(size.X, size.Y);
            Handover(ctx, host, shell);
            Page(ctx, shell);
            Frozen(ctx, host, shell);
            Doors(ctx, host, seat, shell, fit);
        }
        finally
        {
            host.Deactivate();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
        }
    }

    // The seam the director and the launcher make between them: the numbers are read once at the
    // ending, the hold runs with the world still up, and only the hold's end sends them to the menu.
    private static void Handover(TestContext ctx, MenuHost host, OriginalShell shell)
    {
        var run = new InstantActionRuntime(
            InstantActionSuites.EndDef(ctx, "wrapup-original", "dogfight_ace"));
        IaWrapupSnapshot? frozen = null;
        run.MissionEnded += outcome => frozen = new IaWrapupSnapshot(
            outcome == InstantActionOutcome.Won, "C1   ·   Ace Duel", run.Elapsed, 4, 3, 27,
            new[] { "1.  Pier   12.4   12.4", "TOTAL   12.4" });
        run.WrapupDue += _ =>
        {
            if (frozen is { } snapshot)
            {
                host.Show(new InstantActionWrapupReturn(snapshot));
            }
        };

        run.Advance(186f);
        run.ReportObjective(InstantActionObjective.AceDown);
        ctx.Check(frozen != null && shell.Screen != OriginalScreen.InstantActionWrapup,
            $"the ending freezes the numbers with the world still up and no page yet ({shell.Screen})");
        run.Advance(InstantActionRuntime.WrapupHoldS);
        ctx.Check(shell.Screen == OriginalScreen.InstantActionWrapup && shell.Wrapup.Snapshot != null,
            $"the hold's end hands the snapshot to the menu's own page ({shell.Screen})");
        ctx.Check(shell.Wrapup.Snapshot?.Elapsed == 186f,
            $"carrying the mission clock as the ending stopped it ({shell.Wrapup.Snapshot?.Elapsed})");
    }

    // What the page draws, read off the shell's own composition rather than the page's builder.
    private static void Page(TestContext ctx, OriginalShell shell)
    {
        var board = shell.Compose();
        var text = board.Lines.Select(l => l.Text).ToList();
        ctx.Check(text.Contains("Instant Action"), $"the decoded heading stands over the page ({text.Count} lines)");
        ctx.Check(text.Contains("Time to Complete Mission") && text.Contains("03:06"),
            $"the time row reads the frozen clock ({string.Join(" / ", text)})");
        ctx.Check(text.Contains("Enemies Shot Down") && text.Contains("4"), $"the kill row reads the frozen tally");
        ctx.Check(text.Contains("Danger Zones Completed") && text.Contains("3"), $"the zone row reads the frozen count");
        ctx.Check(text.Contains("Shot %") && text.Contains("27%"), $"the shot row reads the frozen percentage");

        var backdrop = board.Backdrop.FirstOrDefault();
        ctx.Check(backdrop != null && backdrop.Art.Name.Length > 0,
            $"the magazine spread is the backdrop ({backdrop?.Art.Name})");
        ctx.Check(board.Pictures.Count >= 4, $"a brushstroke stands under each of the four rows ({board.Pictures.Count})");

        var note = board.Notes.FirstOrDefault();
        ctx.Check(board.Notes.Count == 1 && note != null && note.Entries.Count == 2
                && note.Entries[0] == "C1  ·  Ace Duel" && note.Entries[1] == "1.  Pier  12.4  12.4",
            $"the context and the splits are the further lines, with no headline and no second time ({(note == null ? "none" : string.Join(" / ", note.Entries))})");
        ctx.Check(note != null && note.Shrink && board.Fills.Any(f => f.X <= note.X && f.Y <= note.Y
                && f.X + f.Width >= note.X + note.Width && f.Y + f.Height >= note.Y + note.Height),
            $"written on a post-it that holds them ({board.Fills.Count} fills)");
        float plaqueY = shell.Rows.Count > 0 ? shell.Rows[0].Y : 0f;
        ctx.Check(board.Strokes.Count > 8 && board.Strokes.All(s => s.Y1 < plaqueY && s.Y2 < plaqueY),
            $"a ticked box stands above CONTINUE on a win ({board.Strokes.Count} strokes, plaque at y {plaqueY})");
        ctx.Check(board.Plaques.Any(p => p.Row == 0) && shell.Rows.Count == 1
                && shell.Rows[0].Key == OriginalWrapupScreen.ContinueKey,
            $"with CONTINUE as the page's one row ({shell.Rows.Count} rows)");
    }

    // The page shows the run it was handed, and a later ending cannot edit the one already drawn.
    private static void Frozen(TestContext ctx, MenuHost host, OriginalShell shell)
    {
        var stale = shell.Wrapup.Snapshot;
        var later = new InstantActionRuntime(
            InstantActionSuites.EndDef(ctx, "wrapup-original-later", "dogfight_ace"));
        later.Advance(600f);
        later.ReportObjective(InstantActionObjective.AceDown);
        var board = shell.Compose();
        ctx.Check(board.Lines.Any(l => l.Text == "03:06") && !board.Lines.Any(l => l.Text == "10:00"),
            $"a second mission ending elsewhere leaves the drawn page alone");
        ctx.Check(ReferenceEquals(shell.Wrapup.Snapshot, stale), $"the page still holds its own run");

        host.Show(new InstantActionWrapupReturn(new IaWrapupSnapshot(
            false, "C3   ·   Dogfight", 74.5f, 1, 0, 8)));
        var failed = shell.Compose();
        ctx.Check(failed.Notes.Count == 1 && failed.Notes[0].Entries.Count == 1
                && failed.Notes[0].Entries[0] == "C3  ·  Dogfight",
            $"a failed run carries only its context and no splits ({string.Join(" / ", failed.Notes[0].Entries)})");
        ctx.Check(failed.Strokes.Count == 8, $"and leaves the tick box empty ({failed.Strokes.Count} strokes)");
        ctx.Check(failed.Lines.Any(l => l.Text == "01:14") && failed.Lines.Any(l => l.Text == "8%"),
            $"and its own numbers");
    }

    // Both ways off the page, and what they leave behind.
    private static void Doors(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit)
    {
        var back = shell.Step(new MenuCommands { Back = true });
        ctx.Check(shell.Screen == OriginalScreen.InstantAction && shell.Wrapup.Snapshot == null && back.Exit == null,
            $"Back returns to the Instant Action screen with the run forgotten ({shell.Screen})");

        host.Show(new InstantActionWrapupReturn(InstantActionWrapupPage.Sample(won: true)));
        ctx.Check(shell.Screen == OriginalScreen.InstantActionWrapup, $"the page stands again ({shell.Screen})");
        var plaque = shell.Rows.FirstOrDefault(r => r.Key == OriginalWrapupScreen.ContinueKey);
        ctx.Check(plaque != null, $"CONTINUE is on the page");
        if (plaque == null)
        {
            return;
        }

        Click(host, seat, fit, plaque.X + 5f, plaque.Y + 5f);
        ctx.Check(shell.Screen == OriginalScreen.InstantAction && shell.Wrapup.Snapshot == null,
            $"a click on CONTINUE lands on the Instant Action screen ({shell.Screen})");
        ctx.Check(shell.Rows.Any(r => r.Key == OriginalInstantActionScreen.ExitKey),
            $"the sortie's own rows are back ({shell.Rows.Count} rows)");
    }

    // One click as the shell reads it: the press arms the row and the release on it fires.
    private static void Click(MenuHost host, ScriptedSeat seat, BoardFit fit, float x, float y)
    {
        var down = new MenuCommands { Pointer = new MenuPointer(fit.X(x), fit.Y(y), true, true) };
        seat.Enqueue(down);
        host.Tick(1f / 60f);
        seat.Enqueue(down with { Pointer = down.Pointer!.Value with { Pressed = false, Clicked = false } });
        host.Tick(1f / 60f);
    }

    // A seat whose frames a suite writes, and which reads nothing of its own.
    private sealed class ScriptedSeat : IMenuInputSource
    {
        private readonly Queue<MenuCommands> _frames = new();

        public string DeviceLabel => "scripted";

        public bool CapturingText { get; set; }

        public void Enqueue(MenuCommands frame) => _frames.Enqueue(frame);

        public MenuCommands Poll(float dt) => _frames.Count > 0 ? _frames.Dequeue() : MenuCommands.None;

        public void Prime()
        {
        }
    }
}
