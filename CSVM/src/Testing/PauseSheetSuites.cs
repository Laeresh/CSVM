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

    // The mission whose note rows are numbered against their priorities: its priority 1 row is
    // OBJECTIVE15, while OBJECTIVE1 is a conditionless two-second wake objective. A parchment that
    // asked the runtime by priority would check that row two seconds into the mission.
    private const string NumberedChapter = "C3";
    private const string NumberedMission = "M04";

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
        + "marks follow the completed rows across the four filmed poses, a mark follows the row's "
        + "own OBJECTIVEn number rather than its briefing priority (C3/M04's priority 1 row is "
        + "OBJECTIVE15, and its two-second wake objective must not check it), C3/M01's own "
        + "sheet matches the reference stills flag for flag, and a pointer over a strip moves the "
        + "shared cursor onto it and fires it on the release while a press let go elsewhere fires "
        + "nothing")]
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
        CheckNumberedMarks(ctx, sheets, report);

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

            // The dialog's own pointer is drawn only under a live pointer, so it reaches no
            // composition here; both its bitmaps still have to be in the extraction.
            foreach (string name in Cursors(sheet))
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
            DrivePointer(ctx, board, entry.Sheet, report);
            pause.ForceResume();
            ctx.Check(!board.Visible, $"the resume takes the board away");
        }
        finally
        {
            ctx.Host.RemoveChild(board);
            board.QueueFree();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
        }
    }

    // The pointer over the raised board: the third strip, which is PREFERENCES and so leaves the
    // board standing when it fires, driven through the same frame the pad's own step runs in.
    private static void DrivePointer(
        TestContext ctx, Flight.OriginalPauseBoard board, PauseSheet sheet, StringBuilder report)
    {
        var strip = sheet.Shared.Button(PauseScreens.ButtonKeys[PauseScreens.PreferencesRow]);
        if (strip == null)
        {
            ctx.Check(false, $"the shared block authors the third strip");
            return;
        }

        (float X, float Y, bool Pressed)? pointer = null;
        int fired = 0;
        board.PointerSource = () => pointer;
        board.Preferences = () => fired++;
        float onX = strip.At.X + (PauseScreens.StripWidth / 2f);
        float onY = strip.At.Y + (PauseScreens.StripHeight / 2f);
        int before = board.FocusedRow;

        pointer = (onX, onY, false);
        board._Process(0.0);
        ctx.Same(PauseScreens.PreferencesRow, board.FocusedRow,
            $"a pointer on the third strip moves the shared cursor onto it (from {before})");
        ctx.Check(StripArt(board, PauseScreens.PreferencesRow) == strip.Rollover,
            $"and that strip wears the rollover bitmap ({StripArt(board, PauseScreens.PreferencesRow)})");
        ctx.Check(CursorArt(board) == sheet.State.Cursor?.Rollover,
            $"and the dialog's own cursor wears its rollover ({CursorArt(board)})");

        pointer = (onX, onY, true);
        board._Process(0.0);
        ctx.Check(StripArt(board, PauseScreens.PreferencesRow) == strip.Activate && fired == 0,
            $"the press holds the strip on its activate bitmap and fires nothing while it is down");

        pointer = (onX, onY, false);
        board._Process(0.0);
        ctx.Same(1, fired, $"the release on the strip fires that strip's action");

        pointer = (400f, 300f, false);
        board._Process(0.0);
        ctx.Same(PauseScreens.PreferencesRow, board.FocusedRow,
            $"a pointer on no strip leaves the cursor where it stands");
        ctx.Check(CursorArt(board) == sheet.State.Cursor?.Bitmap,
            $"and the cursor goes back to its plain bitmap ({CursorArt(board)})");

        pointer = (onX, onY, true);
        board._Process(0.0);
        pointer = (400f, 300f, true);
        board._Process(0.0);
        pointer = (400f, 300f, false);
        board._Process(0.0);
        ctx.Same(1, fired, $"a press released off the strip it took hold of fires nothing");

        // The pad's own path with no pointer at all: the cursor stays put and no strip is held.
        pointer = null;
        board._Process(0.0);
        ctx.Same(PauseScreens.PreferencesRow, board.FocusedRow,
            $"the cursor survives the pointer leaving the screen");
        ctx.Check(StripArt(board, PauseScreens.PreferencesRow) == strip.Rollover,
            $"and the focused strip is back on its rollover bitmap with none held");
        report.AppendLine(
            $"pointer: strip {PauseScreens.PreferencesRow} at ({onX}, {onY}) focused, "
            + $"fired {fired} time(s), cursor {sheet.State.Cursor?.Bitmap ?? "-"}/"
            + $"{sheet.State.Cursor?.Rollover ?? "-"}");
    }

    private static string? StripArt(Flight.OriginalPauseBoard board, int row) =>
        board.Shown is { } shown && row < shown.Plaques.Count ? shown.Plaques[row].Art.Name : null;

    private static string? CursorArt(Flight.OriginalPauseBoard board)
    {
        if (board.Shown is not { } shown)
        {
            return null;
        }

        foreach (var panel in shown.Overlays)
        {
            foreach (var picture in panel.Pictures)
            {
                return picture.Art.Name;
            }
        }

        return null;
    }

    private static IEnumerable<string> Cursors(PauseSheet sheet)
    {
        if (sheet.State.Cursor is not { } cursor)
        {
            yield break;
        }

        yield return cursor.Bitmap;
        if (cursor.Rollover != cursor.Bitmap)
        {
            yield return cursor.Rollover;
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

    // The marks against the live objectives runtime, on the mission where the note's row order and
    // the script's numbering disagree: the rows are OBJECTIVE15 then OBJECTIVE14, so a mark taken
    // from a row's priority would check row one when OBJECTIVE1's two-second wake completes.
    private static void CheckNumberedMarks(
        TestContext ctx, List<(CampaignMission Mission, PauseSheet Sheet)> sheets, StringBuilder report)
    {
        int at = sheets.FindIndex(s =>
            s.Mission.ChapterFolder.Equals(NumberedChapter, System.StringComparison.OrdinalIgnoreCase)
            && s.Mission.MissionFolder.Equals(NumberedMission, System.StringComparison.OrdinalIgnoreCase));
        if (at < 0)
        {
            ctx.Check(false, $"{NumberedChapter}/{NumberedMission} is in the sequence");
            return;
        }

        var entry = sheets[at];
        var reader = Zrdr.LoadFile(
            SessionPaths.MissionZrdr(
                ctx.DataRoot, entry.Mission.ChapterFolder, entry.Mission.MissionFolder),
            "objectives.json");
        var note = BriefingObjectives.Load(reader, Messages.Load(ctx.MessagesPath));
        ctx.Check(
            note.Count == 2 && note[0].Priority == 1 && note[0].Number == 15
            && note[1].Priority == 2 && note[1].Number == 14,
            $"the note's two rows are OBJECTIVE15 at priority 1 and OBJECTIVE14 at priority 2");

        int rowOne = 0;
        foreach (int index in entry.Sheet.Reveal.RevealedObjectives)
        {
            rowOne += index == 0 ? 1 : 0;
        }

        ctx.Check(rowOne > 0, $"the sheet's own script reveals the priority 1 row ({rowOne} time(s))");

        var graph = new Session.ObjectiveGraph(Session.ObjectiveScript.Parse(reader), new NoWorld());
        for (float t = 0f; t < 3f; t += 0.1f)
        {
            graph.Step(0.1f);
        }

        var open = PauseReadout.Rows(note, graph.CompletedOf);
        float wakeAt = graph.Elapsed;
        ctx.Check(graph.CompletedOf(1), $"OBJECTIVE1, the conditionless two-second wake, has completed");
        ctx.Check(!graph.CompletedOf(15), $"OBJECTIVE15, which the priority 1 row stands for, has not");
        ctx.Check(!open[0].Completed && !open[1].Completed, $"so neither parchment row reads done");
        ctx.Same(0, Marks(entry.Sheet, open), $"and the composed parchment carries no mark");

        graph.Wake(15);
        graph.Step(0.1f);
        var done = PauseReadout.Rows(note, graph.CompletedOf);
        ctx.Check(graph.CompletedOf(15), $"a wake completes the conditionless OBJECTIVE15");
        ctx.Check(done[0].Completed && !done[1].Completed, $"which marks the priority 1 row and only it");
        ctx.Same(rowOne, Marks(entry.Sheet, done), $"and the composed parchment marks that row");
        report.AppendLine(
            $"{NumberedChapter}/{NumberedMission} rows: "
            + $"O{note[0].Number}/p{note[0].Priority}, O{note[1].Number}/p{note[1].Priority}; "
            + $"at {wakeAt:0.0} s O1 alone is done and the parchment marks {Marks(entry.Sheet, open)}, "
            + $"with O15 done it marks {Marks(entry.Sheet, done)}");
    }

    private static int Marks(PauseSheet sheet, IReadOnlyList<PauseObjective> rows)
    {
        var board = PauseScreens.For(
            sheet,
            new PauseReadout(rows, string.Empty, System.Array.Empty<PauseWorldIcon>()),
            0,
            false);
        int marked = 0;
        foreach (var note in board.Notes)
        {
            foreach (bool flag in note.Marked ?? System.Array.Empty<bool>())
            {
                marked += flag ? 1 : 0;
            }
        }

        return marked;
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

    // The objectives runtime's world seam with no world behind it: the families a session cannot
    // answer report null, exactly as the live adapter does, and every world-touching action is a
    // no-op. Enough to run a mission's own script for its timing and its chaining.
    private sealed class NoWorld : Session.IObjectiveWorld
    {
        public bool? NodeInactive(IReadOnlyList<string> path) => null;

        public int AnimState(string anim) => 0;

        public int? GroupLiveCount(int group, string? generator) => null;

        public void WidenGroupEngagement(int group)
        {
        }

        public bool? TravelersMet(Session.TravelersSpec spec) => null;

        public void WakeupEnemies(IReadOnlyList<string> names)
        {
        }

        public void WakeupTurrets(IReadOnlyList<string> patterns)
        {
        }

        public void WakeupZepTurrets(IReadOnlyList<string> nodes)
        {
        }

        public void WakeupGenerator(string name, int count)
        {
        }

        public void WakeAnim(string anim, string? node)
        {
        }

        public void PlaySoundGroup(string group)
        {
        }

        public void StopQueuedSounds(IReadOnlyList<string> names)
        {
        }

        public void WarpVehicle(string vehicle, IReadOnlyList<Session.WarpPoint> points)
        {
        }

        public void SetAiTeam(IReadOnlyList<(string Name, int Team)> entries)
        {
        }

        public void SetAiNet(IReadOnlyList<(string Name, string Net)> entries)
        {
        }

        public void SetAiAttackRadius(IReadOnlyList<(string Name, float Radius)> entries)
        {
        }

        public void CompletedZepcannons(IReadOnlyList<(string Zeppelin, int Flag)> entries)
        {
        }

        public void CompletedStoppoint(IReadOnlyList<(string Net, int Stop, int Flag)> entries)
        {
        }

        public void StartTaxi(IReadOnlyList<string> names)
        {
        }
    }
}
