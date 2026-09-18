using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using Godot;

namespace CSVM.Testing;

/// <summary>The Danger Zone camera over C4/IA1's fourteen authored markers: one photograph per
/// marker per run, the sting with it, no second latch while the aircraft stays inside the radius
/// and none on a later pass through the same marker, and the scoreboard's thumbnail strip in
/// marker order. Writes into the run's own scratch directory, never beside the player's
/// saves.</summary>
internal static class StuntCaptureSuites
{
    private const string Chapter = "C4";
    private const string Mission = "IA1";
    private const string StingDef = "snd_dangerzone_camera";

    // Well outside every marker's 15 m radius, so a step through here leaves all of them.
    private static readonly Vector3 Elsewhere = new(0f, 60000f, 0f);

    [Suite("stunt-capture",
        "the Danger Zone camera over a C4/IA1 stunt run: crossing inside DzRadius of each dzN "
        + "marker latches exactly one photograph for that pilot and plays the camera sting "
        + "once, lingering inside the radius latches nothing more, a later pass over a "
        + "photographed marker latches nothing more, the files land under screenshots/stunts/ "
        + "named by chapter, marker and run clock, a rerun makes every marker photographable "
        + "again, the scoreboard's thumbnail strip lists the run's shots in marker order, and a "
        + "frame landing after the crossing (and after the board woke) writes its file and fills "
        + "its cell while the sting and the pass mark stay on the crossing, a marker latched on "
        + "the frame the run completes joins the strip of the board that frame woke, and Instant "
        + "Action's wrap-up board draws player 1's shots the same way (marker order, a held frame "
        + "filled on landing, no strip without a camera or a shot)")]
    internal static void StuntCaptureRun(TestContext ctx)
    {
        string gamezPath = SessionPaths.ChapterGamez(ctx.DataRoot, Chapter);
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, Chapter, Mission);
        ctx.RequireData(gamezPath, $"{Chapter} gamez");
        ctx.RequireData(missionZrdr, $"{Chapter}/{Mission} zrdr");
        ctx.RequireData(ctx.MessagesPath, $"messages.json");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");

        var run = StuntMission.Load(GameZ.Load(gamezPath), missionZrdr, Messages.Load(ctx.MessagesPath))
            ?? throw new SuiteSkippedException($"{Chapter}/{Mission} ships no Danger Zones");

        // The sting has something to play: the definition is a data orphan no SOUND_GROUPS entry
        // and no world data names, so nothing else in the build would notice it going missing.
        var defs = SoundDefs.Load(ctx.ZrdrPath);
        ctx.Check(defs.TryGetValue(StingDef, out var sting) && sting.WavName.Length > 0,
            $"the camera sting '{StingDef}' is a real sounds.json definition");
        if (defs.TryGetValue(StingDef, out var def))
        {
            using var archive = new SoundArchive(ctx.SoundsPath);
            ctx.Check(archive.Find(def.WavName, looped: false, warn: false) != null,
                $"…and its '{def.WavName}' decodes out of the sound archive");
        }

        string root = Path.Combine(ctx.ScratchDir, "StuntCapture");
        string? previous = StuntCapture.DirectoryOverride;
        try
        {
            StuntCapture.DirectoryOverride = root;
            if (Directory.Exists(StuntCapture.ShotDir()))
            {
                Directory.Delete(StuntCapture.ShotDir(), recursive: true);
            }
            Drive(ctx, run);
        }
        finally
        {
            StuntCapture.DirectoryOverride = previous;
        }
    }

    private static void Drive(TestContext ctx, StuntMission run)
    {
        int stings = 0;
        var pane = new PaneRig();
        var capture = new StuntCapture(run, Chapter, pane.Request);
        capture.Sting = () => stings++;

        var latched = new List<(string DzName, float At)>();
        for (int i = 0; i < run.Zones.Count; i++)
        {
            var zone = run.Zones[i];
            run.Tick(2f);
            float at = run.Elapsed;
            capture.Update(zone.Position);
            latched.Add((zone.DzName, at));
            if (i == 0)
            {
                ctx.Check(capture.Count == 1 && stings == 1,
                    $"the first crossing latches one photograph and one sting: shots={capture.Count} stings={stings}");
            }

            // Still inside, a metre off the centre: the latch is the crossing, not the presence.
            run.Tick(1f);
            capture.Update(zone.Position + new Vector3(1f, 0f, 0f));
            ctx.Check(capture.Count == i + 1,
                $"{zone.DzName}: lingering inside the radius latches nothing more (shots={capture.Count})");

            // Out and back in during the same run: one latch per marker per run.
            run.Tick(1f);
            capture.Update(Elsewhere);
            capture.Update(zone.Position);
            ctx.Check(capture.Count == i + 1,
                $"{zone.DzName}: a second pass through the same marker latches nothing more (shots={capture.Count})");
            capture.Update(Elsewhere);
        }

        ctx.Same(run.TotalCount, capture.Count, $"the run latches one photograph per marker");
        ctx.Same(run.TotalCount, stings, $"…and plays the camera sting once per marker");
        ctx.Same(run.TotalCount, pane.Grabs, $"…reading the pane exactly once per latch");
        CheckFiles(ctx, latched);
        CheckOrder(ctx, run, capture);
        CheckRerun(ctx, run, capture);
        CheckStrip(ctx, run, capture, pane, () => stings);
        CheckLatchAfterCompletion(ctx, run, capture);
        CheckWrapupStrip(ctx, run, capture, pane);
        ctx.Note($"{Chapter}/{Mission}: {run.TotalCount} markers photographed into {StuntCapture.ShotDir()}");
    }

    // Each shot is on disk under screenshots/stunts/ with the name its chapter, marker and run
    // clock make, which is the deterministic name a scripted run writes every time.
    private static void CheckFiles(TestContext ctx, IReadOnlyList<(string DzName, float At)> latched)
    {
        int found = 0;
        var missing = new List<string>();
        foreach (var (dzName, at) in latched)
        {
            string path = Path.Combine(StuntCapture.ShotDir(), StuntCapture.FileName(Chapter, dzName, at));
            if (File.Exists(path) && new FileInfo(path).Length > 0)
            {
                found++;
            }
            else
            {
                missing.Add(Path.GetFileName(path));
            }
        }

        ctx.Same(latched.Count, found,
            $"every photograph is a non-empty PNG under screenshots/stunts/{(missing.Count > 0 ? $" (missing {string.Join(", ", missing)})" : "")}");
    }

    private static void CheckOrder(TestContext ctx, StuntMission run, StuntCapture capture)
    {
        var shots = new List<string>();
        foreach (var shot in capture.InMarkerOrder())
        {
            shots.Add(shot.DzName);
        }

        var markers = new List<string>();
        foreach (var zone in run.Zones)
        {
            markers.Add(zone.DzName);
        }

        ctx.Check(string.Join(",", shots) == string.Join(",", markers),
            $"the run's shots read back in marker order: [{string.Join(" ", shots)}]");
    }

    // A rerun makes every marker photographable again, but not while the aircraft is still standing
    // inside one: a restart under a marker must fly through it again to photograph it.
    private static void CheckRerun(TestContext ctx, StuntMission run, StuntCapture capture)
    {
        var zone = run.Zones[0];
        capture.Update(zone.Position);
        capture.Reset();
        ctx.Check(capture.Count == 0, $"a rerun clears the run's photographs: shots={capture.Count}");

        capture.Update(zone.Position);
        ctx.Check(capture.Count == 0,
            $"…and a rerun under a marker does not photograph it without flying through it: shots={capture.Count}");

        capture.Update(Elsewhere);
        capture.Update(zone.Position);
        ctx.Check(capture.Count == 1,
            $"…while the next crossing of that marker photographs it again: shots={capture.Count}");
    }

    // The strip on the real board: built from the run's own shots when the run completes, one cell
    // per photograph, captioned and ordered by marker rather than by the order they were flown. The
    // last marker's frame is held back past the board waking, the way a live readback lands frames
    // after the crossing that completes the run.
    private static void CheckStrip(TestContext ctx, StuntMission run, StuntCapture capture, PaneRig pane,
        System.Func<int> stings)
    {
        capture.Reset();
        capture.Update(Elsewhere);  // out of the marker the rerun check left the aircraft inside
        var last = run.Zones[run.Zones.Count - 1];
        foreach (var zone in run.Zones)
        {
            run.Tick(1f);
            pane.Hold = zone == last;
            int before = stings();
            capture.Update(zone.Position);
            capture.Update(Elsewhere);
            if (pane.Hold)
            {
                ctx.Check(stings() == before + 1 && capture.Count == run.Zones.Count,
                    $"the sting and the pass mark land on the crossing, before the frame does (shots={capture.Count})");
            }
        }

        pane.Hold = false;
        capture.Settle();
        var held = capture.InMarkerOrder().Last();
        ctx.Check(!held.Landed && !File.Exists(held.Path),
            $"{held.DzName}: a frame still on its way has written no file and has no thumbnail");

        string storePath = Path.Combine(ctx.ScratchDir, "StuntCapture", "stunt_scores.json");
        var board = StuntScoreboard.Build(run, "Test Plane", $"{Chapter}", ScoreStore.Load(storePath),
            $"stunt-capture/{Mission}/player_test", exitsToMenu: true, new PauseState(),
            _ => new MenuInput());
        board.Shots = capture;
        ctx.Host.AddChild(board);
        try
        {
            run.DebugCompleteAll();
            ctx.Check(board.Visible, $"the run completing wakes the scoreboard");
            var captions = Captions(board);
            var markers = new List<string>();
            foreach (var zone in run.Zones)
            {
                markers.Add(zone.DzName);
            }

            ctx.Same(markers.Count, captions.Count, $"the strip carries one cell per photograph");
            ctx.Check(string.Join(",", captions) == string.Join(",", markers),
                $"the strip lists the run's shots in marker order: [{string.Join(" ", captions)}]");
            ctx.Same(markers.Count - 1, Pictures(board), $"…with a picture in every cell whose frame has landed");

            pane.Release(Pane());
            capture.Settle();
            ctx.Check(held.Landed && held.Thumb != null && File.Exists(held.Path),
                $"{held.DzName}: the late frame completes the shot's record and writes its file");
            ctx.Same(markers.Count, Pictures(board), $"…and fills its cell on the board already showing");
        }
        finally
        {
            board.Free();
        }
    }

    // FlightController tests the run before the camera on one physics frame, so the gate pair that
    // completes the run wakes the board before the camera latches a marker crossed on that frame.
    private static void CheckLatchAfterCompletion(TestContext ctx, StuntMission run, StuntCapture capture)
    {
        run.Reset();
        capture.Reset();
        capture.Update(Elsewhere);
        var last = run.Zones[run.Zones.Count - 1];
        foreach (var zone in run.Zones)
        {
            if (zone != last)
            {
                run.Tick(1f);
                capture.Update(zone.Position);
                capture.Update(Elsewhere);
            }
        }
        capture.Settle();

        string storePath = Path.Combine(ctx.ScratchDir, "StuntCapture", "stunt_scores.json");
        var board = StuntScoreboard.Build(run, "Test Plane", $"{Chapter}", ScoreStore.Load(storePath),
            $"stunt-capture/{Mission}/player_test", exitsToMenu: true, new PauseState(),
            _ => new MenuInput());
        board.Shots = capture;
        ctx.Host.AddChild(board);
        try
        {
            run.DebugCompleteAll();
            capture.Update(last.Position);
            var captions = Captions(board);
            ctx.Check(captions.Count == run.Zones.Count && captions[^1] == last.DzName,
                $"a marker latched on the frame the run completes, after the board woke, joins the strip: [{string.Join(" ", captions)}]");
            capture.Settle();
            ctx.Same(run.Zones.Count, Pictures(board), $"…and its picture fills its cell once it lands");
        }
        finally
        {
            board.Free();
        }
    }

    // Instant Action's shared wrap-up board over player 1's camera: the markers are flown last to
    // first so marker order and flown order differ, and the first one flown is held back past the
    // board appearing. A board handed no camera, or one with no shot, draws no strip.
    private static void CheckWrapupStrip(TestContext ctx, StuntMission run, StuntCapture capture, PaneRig pane)
    {
        run.Reset();
        capture.Reset();
        capture.Update(Elsewhere);
        var summary = new StuntSummary(run, 0f, null, false);
        var board = IaWrapupBoard.Build("stunt-capture", exitsToMenu: true, new PauseState(), _ => new MenuInput());
        ctx.Host.AddChild(board);
        try
        {
            board.Present(won: false, 1f, 0, 0, 0, summary);
            ctx.Check(board.FindChild(StuntShotStrip.StripName, recursive: true, owned: false) == null,
                $"the wrap-up board handed no camera draws no strip");
            board.Present(won: false, 1f, 0, 0, 0, summary, capture);
            ctx.Check(board.FindChild(StuntShotStrip.StripName, recursive: true, owned: false)?.GetParent()?.GetParent() is StuntShotStrip { Visible: false },
                $"…and one handed a camera with no shot hides its strip");

            var first = run.Zones[run.Zones.Count - 1];
            for (int i = run.Zones.Count - 1; i >= 0; i--)
            {
                run.Tick(1f);
                pane.Hold = run.Zones[i] == first;
                capture.Update(run.Zones[i].Position);
                capture.Update(Elsewhere);
                pane.Hold = false;
            }
            capture.Settle();
            run.DebugCompleteAll();
            board.Present(won: true, run.Elapsed, 0, run.CompletedCount, 0,
                new StuntSummary(run, run.Elapsed, null, false), capture);

            var captions = Captions(board);
            var markers = run.Zones.Select(z => z.DzName).ToList();
            ctx.Check(string.Join(",", captions) == string.Join(",", markers),
                $"the wrap-up board's strip lists player 1's shots in marker order, not the flown order: [{string.Join(" ", captions)}]");
            ctx.Same(markers.Count - 1, Pictures(board), $"…with the held frame's cell drawn empty");

            pane.Release(Pane());
            capture.Settle();
            ctx.Same(markers.Count, Pictures(board), $"…and filled once its frame lands on the board already showing");
        }
        finally
        {
            board.Free();
        }
    }

    // How many strip cells show a picture.
    private static int Pictures(Node board)
    {
        int count = 0;
        if (board.FindChild(StuntShotStrip.StripName, recursive: true, owned: false) is { } strip)
        {
            foreach (var cell in strip.GetChildren())
            {
                foreach (var part in cell.GetChildren())
                {
                    if (part is TextureRect { Texture: not null })
                    {
                        count++;
                    }
                }
            }
        }
        return count;
    }

    // The strip's own captions, read off the named container so the splits table's rows above
    // cannot be mistaken for them.
    private static List<string> Captions(Node board)
    {
        var captions = new List<string>();
        if (board.FindChild(StuntShotStrip.StripName, recursive: true, owned: false) is not { } strip)
        {
            return captions;
        }
        foreach (var cell in strip.GetChildren())
        {
            foreach (var part in cell.GetChildren())
            {
                if (part is Label label)
                {
                    captions.Add(label.Text);
                }
            }
        }
        return captions;
    }

    // The pane this suite photographs: a small solid frame, so the suite's verdict rests on the
    // latch policy and the file it writes rather than on what a test host happened to render.
    private static Image Pane()
    {
        var img = Image.CreateEmpty(64, 48, false, Image.Format.Rgba8);
        img.Fill(new Color(0.2f, 0.4f, 0.6f));
        return img;
    }

    // The suite's pane request: lands the synthetic frame at once, or holds it until Release, which
    // stands in for a live readback landing frames after the crossing.
    private sealed class PaneRig
    {
        private System.Action<Image?>? _held;

        public int Grabs { get; private set; }

        public bool Hold { get; set; }

        public bool Request(System.Action<Image?> landed)
        {
            Grabs++;
            if (Hold)
            {
                _held = landed;
            }
            else
            {
                landed(Pane());
            }
            return true;
        }

        public void Release(Image frame)
        {
            _held?.Invoke(frame);
            _held = null;
        }
    }
}
