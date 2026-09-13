using System;
using System.Collections.Generic;
using System.Text;
using CSVM.Mech3;
using CSVM.UI;
using CSVM.UI.Menu;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>Suites over the load screen while a build holds the frame loop: the bar's fill and the
/// propeller's frame at each of the sixteen authored milestones, on a real board over a real
/// window, and the pump that draws them from inside the build.
/// Decode: docs/org/loading-screen.md.</summary>
internal static class LoadProgressSuites
{
    // The filmed mission, whose campaign sheet carries the propeller cycle the script authors.
    private const string FilmedChapter = "C3";
    private const string FilmedMission = "M01";

    // The strips the two families fill, at their extracted widths. Asserted rather than assumed:
    // the fill is a pixel clip against the bitmap's own width, so a different strip is a different
    // bar.
    private const int ChalkStripWidth = 236;
    private const int SheetStripWidth = 338;

    /// <summary>The load screen moving under a build: both families find their fill strip and
    /// their propeller in the extraction, the fill at each authored milestone is the floored pixel
    /// clip of that strip's own width, the propeller names one of its six frames at its authored
    /// point, the still composition under the overlay is untouched at every step, the board
    /// installs the pump for its own tree lifetime alone, and a reported step really does put a
    /// frame on screen from inside the build.</summary>
    [Suite("load-progress",
        "the load screen while a build holds the frame loop: the blackboard's prog_red and the "
        + "chart sheet's prog_redload both resolve at their authored width, the fill at each of "
        + "the sixteen authored milestones is floor(stripWidth * fraction) pixels and never "
        + "steps backwards, the propeller cycles the six extracted frames at its authored point "
        + "and rate, the campaign sheet's own content is unchanged under the overlay that moves, "
        + "the board owns the pump for exactly as long as it is in the tree so a CLI launch gains "
        + "no draw, and a reported step draws a frame from inside the blocking build")]
    internal static void LoadScreenMoves(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var report = new StringBuilder();

        CheckFamily(ctx, report, campaign: false, sheet: null, stripWidth: ChalkStripWidth);
        var filmed = FilmedSheet(ctx);
        CheckFamily(ctx, report, campaign: true, sheet: filmed, stripWidth: SheetStripWidth);
        CheckSheetCycleIsTheScriptsOwn(ctx, report, filmed);
        CheckPumpOwnership(ctx, report, filmed);
        CheckPumpDuringAWorldBuild(ctx, report, filmed);

        ctx.WriteArtifact($"test-load-progress.txt", report.ToString());
    }

    // The filmed mission's own sheet, or null where the extraction cannot answer for it.
    private static LoadSheet? FilmedSheet(TestContext ctx)
    {
        foreach (var mission in CampaignSequence.Load(ctx.ZrdrPath))
        {
            if (!mission.ChapterFolder.Equals(FilmedChapter, StringComparison.OrdinalIgnoreCase)
                || !mission.MissionFolder.Equals(FilmedMission, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return LoadSheet.Load(
                ctx.ZrdrPath, ctx.MessagesPath,
                SessionPaths.MissionZrdr(ctx.DataRoot, mission.ChapterFolder, mission.MissionFolder),
                EscapeDialog.CampaignKey(mission.Campaign, mission.Mission),
                Session.CampaignMementos.BitmapFor(null));
        }

        return null;
    }

    // One family's whole milestone walk, on a real board over the window so the strip's width is
    // the extraction's rather than a number restated here.
    private static void CheckFamily(
        TestContext ctx, StringBuilder report, bool campaign, LoadSheet? sheet, int stripWidth)
    {
        string family = campaign ? "chart sheet" : "blackboard";
        var board = LoadBoard.Build(
            ctx.DataRoot, ctx.ZrdrPath, ctx.MessagesPath, campaign, "FREE FLIGHT", null, sheet);
        ctx.Host.AddChild(board);
        try
        {
            if (board.GetChild(0) is not ComposedBoardView view)
            {
                ctx.Check(false, $"{family}: the board on a window builds its own view");
                return;
            }

            var still = view.Board ?? LoadScreens.Empty;
            var motion = LoadScreens.MotionFor(campaign, sheet);
            var art = view.ArtSize(new BoardArt(BoardArtLibrary.Rimage, motion.FillArt));
            ctx.Same(stripWidth, (int)art.X, $"{family}: {motion.FillArt} is the extracted strip");
            report.AppendLine(
                $"{family} fill={motion.FillArt} {(int)art.X}x{(int)art.Y} at ({motion.FillX},{motion.FillY}) "
                + $"propeller={motion.Propeller.Count} frame(s) at ({motion.PropellerX},{motion.PropellerY})");
            WalkMilestones(ctx, report, family, still, motion, art, stripWidth);
        }
        finally
        {
            ctx.Host.RemoveChild(board);
            board.QueueFree();
        }
    }

    // Every milestone in order: the fill is the floored clip, it never steps back, the propeller
    // draws one of its own frames, and nothing under the overlay moves.
    private static void WalkMilestones(
        TestContext ctx, StringBuilder report, string family, ComposedBoard still,
        LoadMotion motion, Vector2 art, int stripWidth)
    {
        int previous = -1;
        var frames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in Enum.GetValues<LoadStep>())
        {
            float fraction = LoadProgress.Milestones[(int)step];
            int frame = (int)step % LoadProgress.PropellerFrames;
            var painted = LoadScreens.Painted(still, motion, art.X, art.Y, fraction, frame);
            int lit = FillWidth(painted);
            int want = LoadProgress.FillPixels(stripWidth, fraction);
            ctx.Same(want, lit, $"{family}: {step} fills {want} px of {stripWidth} at {fraction}");
            ctx.Check(lit >= previous, $"{family}: {step}'s fill never steps back from {previous}");
            previous = lit;
            if (Propeller(painted, motion) is { } named)
            {
                frames.Add(named);
            }

            ctx.Same(
                still.Pictures.Count, painted.Pictures.Count,
                $"{family}: {step} leaves the still composition's {still.Pictures.Count} picture(s) alone");
            ctx.Same(
                still.Notes.Count, painted.Notes.Count,
                $"{family}: {step} leaves the parchment alone");
            report.AppendLine($"{family} {step} {fraction} fill={lit}px frame={frame}");
        }

        // Sixteen steps over a six-frame cycle: every frame is named, so nothing draws one still
        // picture for the whole build.
        ctx.Same(
            motion.Propeller.Count, frames.Count,
            $"{family}: the walk names every one of the cycle's frames");
    }

    // The chart sheet's propeller is the sheet's own Cycle beat, at the point and through the
    // frames its script authors, never the blackboard's placement.
    private static void CheckSheetCycleIsTheScriptsOwn(
        TestContext ctx, StringBuilder report, LoadSheet? sheet)
    {
        if (sheet == null)
        {
            ctx.Check(false, $"{FilmedChapter}/{FilmedMission} resolves its own loading sheet");
            return;
        }

        var motion = LoadScreens.MotionFor(true, sheet);
        ctx.Same(
            LoadProgress.PropellerFrames, motion.Propeller.Count,
            $"the sheet's script authors the whole six-frame cycle");
        ctx.Check(
            motion.Propeller.Count > 0 && motion.Propeller[0] == LoadScreens.Propeller[0],
            $"the sheet's cycle starts on {LoadScreens.Propeller[0]}, the frame its still draws");
        ctx.Check(
            motion.PropellerX != 0f || motion.PropellerY != 0f,
            $"the sheet's cycle sits where its script put it ({motion.PropellerX},{motion.PropellerY})");
        report.AppendLine(
            $"sheet cycle at ({motion.PropellerX},{motion.PropellerY}) frames="
            + string.Join(",", motion.Propeller));
    }

    // The pump belongs to the board and to nothing else: absent before one is up, the board's own
    // while it is, and gone again once it leaves, which is what keeps every CLI launch from
    // gaining a draw it never had.
    private static void CheckPumpOwnership(TestContext ctx, StringBuilder report, LoadSheet? sheet)
    {
        var held = LoadProgress.Current;
        LoadProgress.Current = null;
        var board = LoadBoard.Build(
            ctx.DataRoot, ctx.ZrdrPath, ctx.MessagesPath, campaign: true, string.Empty, null, sheet);
        ctx.Check(LoadProgress.Current == null, $"no board up, so a build reports to nobody");
        ctx.Host.AddChild(board);
        try
        {
            ctx.Check(LoadProgress.Current != null, $"the board in the tree owns the pump");
            var view = board.GetChild(0) as ComposedBoardView;

            // The real path, pump and forced frame included: a build reports a step from inside
            // the block that owns the loop, and the screen has to be repainted there or nowhere.
            LoadProgress.Report(LoadStep.RenderState);
            int first = view?.Board is { } opening ? FillWidth(opening) : -1;
            ctx.Check(first > 0, $"the first step lights {first} px from inside the build");

            // Past the pump's own 0.1 s throttle, so the last step draws rather than coalescing
            // into the first.
            System.Threading.Thread.Sleep((int)(LoadProgress.PumpSeconds * 1000d) + 20);
            LoadProgress.Report(LoadStep.Finished);
            int last = view?.Board is { } closing ? FillWidth(closing) : -1;
            ctx.Check(last > first, $"the last step lights {last} px, over the first's {first}");
            report.AppendLine($"pumped fill: first={first}px last={last}px");
        }
        finally
        {
            ctx.Host.RemoveChild(board);
            board.QueueFree();
        }

        ctx.Check(LoadProgress.Current == null, $"the board off the tree hands the pump back");
        LoadProgress.Current = held;
    }

    // The whole point of the item, driven for real: a world build holds the frame loop, and the
    // screen standing over it has to be repainted from inside that block or not at all. The world
    // is half-built at every forced draw, which is the case no composed-board check can reach.
    private static void CheckPumpDuringAWorldBuild(
        TestContext ctx, StringBuilder report, LoadSheet? sheet)
    {
        var held = LoadProgress.Current;
        LoadProgress.Current = null;
        var board = LoadBoard.Build(
            ctx.DataRoot, ctx.ZrdrPath, ctx.MessagesPath, campaign: true, string.Empty, null, sheet);
        ctx.Host.AddChild(board);
        try
        {
            if (LoadProgress.Current is not { } progress)
            {
                ctx.Check(false, $"the board in the tree owns the pump for the world build");
                return;
            }

            int draws = 0;
            var repaint = progress.Repaint;
            progress.Repaint = () =>
            {
                draws++;
                repaint?.Invoke();
            };
            // Private, so the build really runs rather than the cache handing a world back with no
            // phase crossed at all.
            ctx.WithPrivateWorld(ctx.Chapter, false, _ => { });
            ctx.Check(
                draws > 0, $"the build's own phases drew the screen {draws} time(s) from inside it");
            ctx.Check(
                progress.Fraction >= LoadProgress.Milestones[(int)LoadStep.MissionFiles],
                $"the world build carried the bar to {progress.Fraction}");
            report.AppendLine(
                $"world build: {draws} forced draw(s), bar at {progress.Fraction}");
        }
        finally
        {
            ctx.Host.RemoveChild(board);
            board.QueueFree();
            LoadProgress.Current = held;
        }
    }

    // The lit pixels the painted board draws, which is its overlay's cropped fill picture, or 0
    // where nothing is lit yet.
    private static int FillWidth(ComposedBoard board)
    {
        foreach (var panel in board.Overlays)
        {
            foreach (var picture in panel.Pictures)
            {
                if (picture.Crop is { } crop)
                {
                    return (int)crop.Width;
                }
            }
        }

        return 0;
    }

    // The propeller frame the painted board draws, or null where the screen carries no propeller.
    private static string? Propeller(ComposedBoard board, LoadMotion motion)
    {
        foreach (var panel in board.Overlays)
        {
            foreach (var picture in panel.Pictures)
            {
                if (picture.Crop == null
                    && picture.X == motion.PropellerX && picture.Y == motion.PropellerY)
                {
                    return picture.Art.Name;
                }
            }
        }

        return null;
    }
}
