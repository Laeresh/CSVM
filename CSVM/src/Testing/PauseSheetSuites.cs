using System.Collections.Generic;
using System.IO;
using System.Text;
using CSVM.Mech3;
using CSVM.UI;
using CSVM.UI.Menu;

namespace CSVM.Testing;

/// <summary>Suites over the Original presentation's pause screen: the real board over a real
/// <see cref="Flight.PauseState"/>, composed from the chapter's own <c>escape.zrd</c> dialogs, with
/// every bitmap it names checked against the extraction. Decode: docs/org/pause-screen.md.</summary>
internal static class PauseSheetSuites
{
    // The filmed mission, whose composition the four CAP-45 stills pin exactly.
    private const string FilmedChapter = "C3";
    private const string FilmedMission = "M01";

    /// <summary>The chapter's campaign pause sheets end to end: every dialog resolves, every
    /// bitmap it draws is extracted, and the board follows the pause state it was given.</summary>
    [Suite("pause-sheet",
        "the Original presentation's pause screen against the chapter's own escape.zrd dialogs: "
        + "each campaign mission's sheet resolves its map, memento, parchment and four strips, "
        + "every bitmap the composition names exists in the extraction (a lowercasing or naming "
        + "slip draws nothing and is otherwise silent), a real OriginalPauseBoard follows "
        + "PauseState.Changed with its cursor resting on RESUME, the map is drawn at its authored "
        + "source crop rather than scaled, an OWNSHIP icon placed by world position lands inside "
        + "the map's own screen rectangle and one off the window draws nothing, the parchment's "
        + "marks follow the completed rows across the four filmed poses, and C3/M01's own sheet "
        + "matches the reference stills flag for flag")]
    internal static void PauseSheetScreen(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");

        // Every campaign mission, not the run's own chapter: no world is built here, so the whole
        // sequence is affordable, and the filmed mission has to be in it whatever chapter is up.
        var missions = CampaignSequence.Load(ctx.ZrdrPath);
        if (missions.Count == 0)
        {
            throw new SuiteSkippedException($"cm_sequence names no campaign mission");
        }

        var report = new StringBuilder();
        var sheets = new List<(CampaignMission Mission, PauseSheet Sheet)>();
        foreach (var mission in missions)
        {
            string key = EscapeDialog.CampaignKey(mission.Campaign, mission.Mission);
            var sheet = PauseSheet.Load(ctx.ZrdrPath, ctx.MessagesPath, key, instantAction: false);
            if (sheet == null)
            {
                ctx.Check(false, $"{mission.ChapterFolder}/{mission.MissionFolder} resolves {key}");
                continue;
            }

            sheets.Add((mission, sheet));
            report.AppendLine(
                $"{mission.ChapterFolder}/{mission.MissionFolder} {key} map={sheet.State.Map?.Bitmap ?? "-"} "
                + $"pins={CountPins(sheet)} steps={sheet.State.Steps.Count}");
        }

        ctx.Same(missions.Count, sheets.Count, $"every campaign mission resolves its own pause dialog");
        CheckEveryPinPlaced(ctx, sheets, report);
        CheckArtExists(ctx, sheets, report);
        int filmed = Filmed(sheets);
        ctx.Check(filmed >= 0, $"{FilmedChapter}/{FilmedMission}, the filmed mission, is in the sequence");
        DriveBoard(ctx, sheets[filmed < 0 ? 0 : filmed], report);
        CheckFilmedPoses(ctx, sheets, filmed, report);

        ctx.WriteArtifact($"test-pause-sheet.txt", report.ToString());
        ctx.Note($"composed {sheets.Count} pause sheets and drove one over a live pause state");
    }

    // Every pin the dialog's script authors has to be placed and visible. Seven of the 24 dialogs
    // put their pins past an authored Wait, which a reveal nobody advances never releases, so a
    // sheet built and left alone draws those maps with no flags on them at all.
    private static void CheckEveryPinPlaced(
        TestContext ctx, List<(CampaignMission Mission, PauseSheet Sheet)> sheets, StringBuilder report)
    {
        int authored = 0, placed = 0, waits = 0, bare = 0;
        foreach (var (mission, sheet) in sheets)
        {
            int scripted = 0;
            foreach (var step in sheet.State.Steps)
            {
                scripted += step.Op == BriefingOp.Pict
                    && step.Id.StartsWith("OBJPIN", System.StringComparison.Ordinal) ? 1 : 0;
                waits += step.Op == BriefingOp.Wait ? 1 : 0;
            }

            int live = CountPins(sheet);
            authored += scripted;
            placed += live;
            bare += scripted > 0 && live == 0 ? 1 : 0;
            if (scripted != live)
            {
                report.AppendLine($"  {mission.MissionFolder}: {scripted} pins authored, {live} placed");
            }
        }

        report.AppendLine($"{authored} pins authored across the sequence, {placed} placed, {waits} waits");
        ctx.Same(authored, placed, $"every authored flag pin reaches the sheet it belongs to");
        ctx.Same(0, bare, $"no dialog that authors pins composes a chart with none on it");
        ctx.Check(waits > 0, $"the sequence does author waits, so the settle is doing work ({waits})");
    }

    // Every bitmap the composition names, against the extraction's own rimage folder. A name the
    // extraction does not carry draws nothing and logs nothing, so it is invisible without this.
    private static void CheckArtExists(
        TestContext ctx, List<(CampaignMission Mission, PauseSheet Sheet)> sheets, StringBuilder report)
    {
        string rimage = Path.Combine(ctx.DataRoot, "extracted", "rimage");
        var missing = new List<string>();
        int named = 0;
        foreach (var (mission, sheet) in sheets)
        {
            var board = PauseScreens.For(sheet, Readout(ctx, mission, sheet, 0), 0, false);
            foreach (string name in ArtNames(board))
            {
                named++;
                if (!File.Exists(Path.Combine(rimage, name.ToLowerInvariant() + ".png")))
                {
                    missing.Add($"{mission.MissionFolder}:{name}");
                }
            }
        }

        report.AppendLine($"{named} bitmap references, {missing.Count} missing");
        foreach (string gap in missing)
        {
            report.AppendLine($"  missing {gap}");
        }

        ctx.Same(0, missing.Count, $"every bitmap the chapter's pause sheets name is extracted ({named} named)");
    }

    // The real board over a real pause state: hidden until the pause, composing on it, cursor on
    // RESUME, and the map drawn at its source crop rather than stretched.
    private static void DriveBoard(
        TestContext ctx, (CampaignMission Mission, PauseSheet Sheet) entry, StringBuilder report)
    {
        var pause = new Flight.PauseState();
        var board = Flight.OriginalPauseBoard.Build(
            pause,
            _ => new MenuInput { Keyboard = false, Pads = System.Array.Empty<int>() },
            ctx.DataRoot,
            entry.Sheet,
            () => Readout(ctx, entry.Mission, entry.Sheet, 0));
        ctx.Host.AddChild(board);
        try
        {
            ctx.Check(!board.Visible && board.Shown == null, $"the board is hidden and composes nothing before a pause");
            pause.TryToggle(0);
            ctx.Check(board.Visible, $"the pause raises the board");
            ctx.Same(PauseScreens.ResumeRow, board.FocusedRow, $"the cursor rests on RESUME");

            var shown = board.Shown;
            ctx.Check(shown != null, $"the raised board composes a sheet");
            if (shown != null && entry.Sheet.State.Map is { } map)
            {
                var chart = shown.Pictures[0];
                ctx.Check(
                    chart.Crop == new BoardCrop(map.Clip.X0, map.Clip.Y0, map.Clip.Width, map.Clip.Height)
                    && chart.X == map.Position.X && chart.Y == map.Position.Y,
                    $"the chart is drawn at its authored source crop ({chart.Crop}) and position");
                ctx.Check(chart.Width == 0f && chart.Height == 0f,
                    $"the chart is cropped rather than stretched to a size of its own");
                ctx.Same(4, shown.Plaques.Count, $"the sheet carries the four authored strips");
                CheckWorldIcons(ctx, entry.Sheet, map, report);
            }

            board._Process(0.0);
            pause.ForceResume();
            ctx.Check(!board.Visible, $"the resume takes the board away");
        }
        finally
        {
            ctx.Host.RemoveChild(board);
            board.QueueFree();
        }
    }

    // An icon inside the window lands on the map's own screen rectangle; one outside it draws
    // nothing, which is the original's answer rather than an icon clamped to an edge.
    private static void CheckWorldIcons(
        TestContext ctx, PauseSheet sheet, EscapeMap map, StringBuilder report)
    {
        float midX = (map.World.X0 + map.World.X1) / 2f;
        float midZ = -((map.World.Y0 + map.World.Y1) / 2f);
        var inside = new PauseWorldIcon(sheet.Shared.OwnShip, midX, midZ, 0f);
        var shown = PauseScreens.For(
            sheet, new PauseReadout(System.Array.Empty<PauseObjective>(), string.Empty, new[] { inside }),
            0, false);
        var placed = FindArt(shown, sheet.Shared.OwnShip);
        ctx.Check(
            placed != null && placed.Centered
            && placed.X >= map.ScreenX0 && placed.X <= map.ScreenX1
            && placed.Y >= map.ScreenY0 && placed.Y <= map.ScreenY1,
            $"the window's middle places OWNSHIP inside the map's screen rectangle ({placed?.X}, {placed?.Y})");
        report.AppendLine(
            $"ownship at world ({midX}, {midZ}) -> ({placed?.X}, {placed?.Y}) "
            + $"in [{map.ScreenX0}..{map.ScreenX1}] x [{map.ScreenY0}..{map.ScreenY1}]");

        var offMap = new PauseWorldIcon(sheet.Shared.OwnShip, midX + 1e6f, midZ, 0f);
        var hidden = PauseScreens.For(
            sheet, new PauseReadout(System.Array.Empty<PauseObjective>(), string.Empty, new[] { offMap }),
            0, false);
        ctx.Check(FindArt(hidden, sheet.Shared.OwnShip) == null, $"a position off the window draws no icon");
    }

    // The four filmed poses: rows 1 to 3 completing in turn, which must move the marks and nothing
    // else, and C3/M01's own flags, which the stills pin exactly.
    private static void CheckFilmedPoses(
        TestContext ctx,
        List<(CampaignMission Mission, PauseSheet Sheet)> sheets,
        int at,
        StringBuilder report)
    {
        if (at < 0)
        {
            report.AppendLine($"{FilmedChapter}/{FilmedMission} is absent; poses not checked");
            return;
        }

        var filmed = sheets[at];

        var pins = new List<string>();
        foreach (var element in filmed.Sheet.Reveal.Elements)
        {
            if (element.Id.StartsWith("OBJPIN", System.StringComparison.Ordinal))
            {
                pins.Add(element.Bitmap);
            }
        }

        ctx.Check(
            pins.Count == 4 && pins[0] == "pin6" && pins[1] == "pin6" && pins[2] == "pin6"
            && pins[3] == "pin4",
            $"the filmed mission's flags are three question marks and one numbered four ({string.Join(",", pins)})");

        int lastMarked = -1;
        for (int done = 0; done < 4; done++)
        {
            var board = PauseScreens.For(
                filmed.Sheet, Readout(ctx, filmed.Mission, filmed.Sheet, done), 0, false);
            int marked = 0;
            foreach (var note in board.Notes)
            {
                foreach (bool flag in note.Marked ?? System.Array.Empty<bool>())
                {
                    marked += flag ? 1 : 0;
                }
            }

            report.AppendLine($"pose {done}: {marked} marked row(s), {board.Pictures.Count} pictures");
            ctx.Same(done, marked, $"pose {done} marks exactly the rows that completed");
            ctx.Check(marked > lastMarked, $"pose {done} marks more rows than the pose before it");
            lastMarked = marked;
        }
    }

    // The mission's own objectives in the order its dialog's script indexes them, with the first
    // few marked so the parchment's marks are exercised.
    private static PauseReadout Readout(
        TestContext ctx, CampaignMission mission, PauseSheet sheet, int completed)
    {
        var objectives = BriefingObjectives.Load(
            Zrdr.LoadFile(
                SessionPaths.MissionZrdr(ctx.DataRoot, mission.ChapterFolder, mission.MissionFolder),
                "objectives.json"),
            Messages.Load(ctx.MessagesPath));
        var rows = new List<PauseObjective>(objectives.Count);
        for (int i = 0; i < objectives.Count; i++)
        {
            rows.Add(new PauseObjective(objectives[i].Text, i < completed));
        }

        return new PauseReadout(rows, "ms_p_initialpinup1", System.Array.Empty<PauseWorldIcon>());
    }

    private static int Filmed(List<(CampaignMission Mission, PauseSheet Sheet)> sheets) =>
        sheets.FindIndex(s =>
            s.Mission.ChapterFolder.Equals(FilmedChapter, System.StringComparison.OrdinalIgnoreCase)
            && s.Mission.MissionFolder.Equals(FilmedMission, System.StringComparison.OrdinalIgnoreCase));

    private static int CountPins(PauseSheet sheet)
    {
        int pins = 0;
        foreach (var element in sheet.Reveal.Elements)
        {
            pins += element.Id.StartsWith("OBJPIN", System.StringComparison.Ordinal) ? 1 : 0;
        }

        return pins;
    }

    private static BoardPicture? FindArt(ComposedBoard board, string name)
    {
        foreach (var picture in board.Pictures)
        {
            if (picture.Art.Name == name)
            {
                return picture;
            }
        }

        return null;
    }

    private static IEnumerable<string> ArtNames(ComposedBoard board)
    {
        foreach (var picture in board.Backdrop)
        {
            yield return picture.Art.Name;
        }

        foreach (var picture in board.Pictures)
        {
            yield return picture.Art.Name;
        }

        foreach (var plaque in board.Plaques)
        {
            yield return plaque.Art.Name;
        }

        foreach (var note in board.Notes)
        {
            if (note.Mark is { } mark)
            {
                yield return mark.Name;
            }
        }
    }
}
