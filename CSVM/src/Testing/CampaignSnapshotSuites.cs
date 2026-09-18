using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.UI;
using Godot;

namespace CSVM.Testing;

/// <summary>The campaign Danger Zone photograph over C3/M01's own gates: the mission's
/// <c>dzones.zrd</c> binds <c>dzpath1</c> to objective 18, <c>SCRAPBOOK.CSV</c> row <c>1_2_6</c>
/// names <c>Snap_1_18</c>, and a crossing of the authored gates writes exactly that file into a
/// scratch profile directory for the book to draw. The stunt camera's own run is the control: it
/// writes its photographs under their own names beside the saves and no <c>Snap_</c> file
/// anywhere.</summary>
internal static class CampaignSnapshotSuites
{
    private const string Chapter = "C3";
    private const string Mission = "M01";
    private const string Pilot = "Zachary";
    private const string Zone = "dzpath1";
    private const string MountName = "DZ_generic_corners";

    // Well outside every stunt marker's radius, so the control run's latch is a crossing.
    private static readonly Vector3 Elsewhere = new(0f, 60000f, 0f);

    [Suite("campaign-danger-zone-snapshot",
        "the campaign Danger Zone photograph over C3/M01's own dzpath1 gates: the mission's "
        + "dzones.zrd binds the zone to the objective number SCRAPBOOK.CSV's Snap_1_18 row names, "
        + "a crossing of the authored gates stages the pilot's photograph under that name in a scratch "
        + "profile directory, winning the mission keeps it at the row's own name and sets the "
        + "zone's mask bit, the written file is the forced 164x123 region, the scrapbook resolves "
        + "and draws it under its photo-corner mount, a spread composed without the file on disk "
        + "skips the row, a stunt Instant Action run writes its own shots and no Snap_ file, and a "
        + "frame landing after the mission-end sweep takes that sweep's keep or drop")]
    internal static void CampaignDangerZoneSnapshot(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        if (MissionAt(CampaignSequence.Load(ctx.ZrdrPath)) is not { } mission)
        {
            throw new SuiteSkippedException($"{Chapter}/{Mission} is not in cm_sequence");
        }

        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, Chapter, Mission);
        ctx.RequireData(missionZrdr, $"{Chapter}/{Mission} zrdr");
        var script = ObjectiveScript.Load(missionZrdr);
        string root = Path.Combine(ctx.ScratchDir, "CampaignSnapshot");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        var store = new CampaignProfileStore(Path.Combine(root, "Profiles"));
        var profile = CampaignProfileDef.NewProfile(Pilot);
        var report = new StringBuilder();
        try
        {
            ctx.WithWorld(Chapter, collision: false, Mission, world =>
                Fly(ctx, world, script, mission, profile, store, missionZrdr, report));
            StuntRunWritesNoSnap(ctx, root, store.DirFor(Pilot), report);
            LandsAfterCommit(ctx, Path.Combine(root, "Late"), mission.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }

        ctx.WriteArtifact("test-campaign-danger-zone-snapshot.txt", report.ToString());
        ctx.Note($"{Chapter}/{Mission}: {Zone} photographed into the profile under its scrapbook row's own name");
    }

    private static void Fly(TestContext ctx, TestWorld world, ObjectiveScript script,
        CampaignMission mission, CampaignProfileDef profile, CampaignProfileStore store,
        string missionZrdr, StringBuilder report)
    {
        var zones = CampaignDangerZones.Load(script, world.Gamez, missionZrdr);
        if (zones == null || !zones.TryZone(Zone, out int objective, out bool snapshot))
        {
            ctx.Check(false, $"the chapter world arms '{Zone}' off {Chapter}/{Mission}'s own graph");
            return;
        }

        ctx.Check(objective >= 18 && objective <= 31,
            $"'{Zone}' carries the mission's own objective number {objective}, inside the band dzones.zrd assigns");
        ctx.Check(snapshot, $"…and the mission's nosnapshot list does not opt it out of photographing");

        string fileName = CampaignSnapshot.FileName(mission.Ordinal, objective);
        if (RowFor(ctx, mission.Ordinal, fileName) is not { } row)
        {
            ctx.Check(false, $"SCRAPBOOK.CSV carries a capture row named '{fileName}'");
            return;
        }

        string forced = $"{CampaignSnapshot.Width}x{CampaignSnapshot.Height}";
        ctx.Check(row.Scrap.Region == null,
            $"row {mission.Ordinal}_{row.Spread}_{row.Scrap.Item} forces no region of its own, so the engine's {forced} applies");
        report.AppendLine($"{Chapter}/{Mission} ordinal {mission.Ordinal}: '{Zone}' -> objective {objective} "
            + $"-> row {mission.Ordinal}_{row.Spread}_{row.Scrap.Item} '{row.Scrap.ImageName}'");

        string dir = store.DirFor(Pilot);
        string staged = Path.Combine(dir, $"Snap_{mission.Ordinal}_{objective}.PN_");
        string kept = Path.Combine(dir, fileName);
        int panes = 0;
        var director = CampaignDirector.Create(script, mission, profile, store, missionZrdr);
        director.Attach(new CampaignDirector.WorldInputs
        {
            Runtime = world.Runtime,
            Gamez = world.Gamez,
            PlayerPane = landed =>
            {
                panes++;
                landed(Pane());
                return true;
            },
        });

        Cross(ctx, script, world, missionZrdr, director);
        ctx.Same(1, panes, $"the crossing read the pilot's pane exactly once");
        ctx.Check(File.Exists(staged), $"the latch staged the photograph as '{Path.GetFileName(staged)}'");
        ctx.Check(!File.Exists(kept),
            $"and nothing stands under the scrapbook's own '{fileName}' while the mission is still being flown");

        Win(ctx, script, director, mission, profile, objective, report);
        ctx.Check(File.Exists(kept) && new FileInfo(kept).Length > 0,
            $"winning the mission keeps the photograph as '{fileName}'");
        ctx.Check(!File.Exists(staged), $"…and the staged file is gone, the original's own mission-end sweep");
        ctx.Same(1, Directory.GetFiles(dir, "Snap_*").Length,
            $"exactly one Snap_ file stands in the profile directory");
        if (Image.LoadFromFile(kept) is { } written)
        {
            ctx.Same(CampaignSnapshot.Width, written.GetWidth(), $"the file is the forced region's width");
            ctx.Same(CampaignSnapshot.Height, written.GetHeight(), $"…and its height");
        }

        Draws(ctx, store, profile, mission, row, kept, report);
    }

    // The real gate geometry driving the real latch: a fresh tracker so one zone's probe jump
    // cannot cross another's plane, and the director's own notify as the sink.
    private static void Cross(TestContext ctx, ObjectiveScript script, TestWorld world,
        string missionZrdr, CampaignDirector director)
    {
        var solo = CampaignDangerZones.Load(script, world.Gamez, missionZrdr)!;
        ctx.Check(solo.TryGateProbe(Zone, out var gc, out var gn, out var rc, out var rn),
            $"'{Zone}' resolved a green/red gate pair out of the chapter world");
        solo.Update(gc - gn * 5f, director.NotifyDangerZoneCompleted);
        solo.Update(gc + gn * 5f, director.NotifyDangerZoneCompleted);
        solo.Update(rc - rn * 5f, director.NotifyDangerZoneCompleted);
        solo.Update(rc + rn * 5f, director.NotifyDangerZoneCompleted);
    }

    private static void Win(TestContext ctx, ObjectiveScript script, CampaignDirector director,
        CampaignMission mission, CampaignProfileDef profile, int objective, StringBuilder report)
    {
        int winner = 0;
        foreach (var def in script.Objectives)
        {
            if (def.InstantWin && winner == 0)
            {
                winner = def.Number;
            }
        }

        ctx.Check(winner > 0, $"{Chapter}/{Mission} authors an INSTANTWIN objective to end on");
        director.Graph!.Wake(winner);
        for (float t = 0f; t < 3f; t += 0.1f)
        {
            director.Step(0.1f);
        }

        ctx.Check(director.Result is { Outcome: MissionOutcome.Won }, $"the mission ended won");
        int mask = director.Result?.Attempt.CompletedMask ?? 0;
        ctx.Check((mask & (1 << objective)) != 0,
            $"the completed zone's own objective bit {objective} reached the recorded mask 0x{mask:x}");
        report.AppendLine($"recorded mask 0x{mask:x}, profile best "
            + $"0x{CampaignProgression.ResultOf(profile, mission.Seq)?.Best.CompletedMask ?? 0:x}");
    }

    // The read half, over the real flow: the seated profile's own directory resolves the row, the
    // spread draws it as a loose picture, and a spread composed with no file on disk skips it.
    private static void Draws(TestContext ctx, CampaignProfileStore store, CampaignProfileDef profile,
        CampaignMission mission, (int Spread, ScrapbookScrap Scrap) row, string kept, StringBuilder report)
    {
        var flow = new CampaignFlow(store, UiStrings.TryLoad(ctx.DataRoot) ?? UiStrings.Empty, ctx.DataRoot);
        flow.SelectProfile(profile);
        ctx.Check(string.Equals(flow.CapturePath(row.Scrap), kept, StringComparison.OrdinalIgnoreCase),
            $"the scrapbook row resolves to the file the latch wrote");

        int best = CampaignProgression.ResultOf(profile, mission.Seq)?.Best.CompletedMask ?? 0;
        ctx.Check(ScrapbookComposition.Visible(row.Scrap.Objective, best),
            $"the row's own objective gate passes off the flown mission's best mask");
        var drawn = ScrapbookComposition.Pictures(
            ctx.DataRoot, mission.Ordinal, row.Spread, best, flow.CapturePath);
        ctx.Check(IndexOfLoose(drawn, kept) >= 0, $"the spread draws the capture as a loose picture");
        var without = ScrapbookComposition.Pictures(ctx.DataRoot, mission.Ordinal, row.Spread, best, _ => null);
        ctx.Same(drawn.Count - 2, without.Count,
            $"a spread composed with no file on disk drops the capture and its grime frame");

        // The photo-corner mount is the row's own neighbour at identical coordinates, one step
        // higher in draw order, so it stands over the photograph rather than under it.
        var all = ScrapbookComposition.Pictures(
            ctx.DataRoot, mission.Ordinal, row.Spread, best, flow.CapturePath, revealAll: true);
        int capture = IndexOfLoose(all, kept);
        int mount = IndexOfMount(all, row.Scrap);
        ctx.Check(capture >= 0 && mount > capture,
            $"the photo-corner mount draws over the photograph (capture at {capture}, mount at {mount})");
        report.AppendLine($"spread {row.Spread}: {drawn.Count} pictures gated, {all.Count} with every row revealed");
    }

    // The control: a stunt run's camera writes under screenshots/stunts/ and touches no Snap_ name,
    // in the profile directory or in its own.
    private static void StuntRunWritesNoSnap(TestContext ctx, string root, string profileDir, StringBuilder report)
    {
        string gamezPath = SessionPaths.ChapterGamez(ctx.DataRoot, "C4");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C4", "IA1");
        if (!File.Exists(gamezPath) || !Directory.Exists(missionZrdr))
        {
            return;
        }

        var run = StuntMission.Load(GameZ.Load(gamezPath), missionZrdr, Messages.Load(ctx.MessagesPath));
        if (run == null)
        {
            return;
        }

        string? previous = StuntCapture.DirectoryOverride;
        try
        {
            StuntCapture.DirectoryOverride = Path.Combine(root, "Stunts");
            var capture = new StuntCapture(run, "C4", landed =>
            {
                landed(Pane());
                return true;
            });
            foreach (var zone in run.Zones)
            {
                run.Tick(1f);
                capture.Update(zone.Position);
                capture.Update(Elsewhere);
            }

            ctx.Check(capture.Count > 0, $"the Instant Action run latched {capture.Count} stunt photograph(s)");
            ctx.Same(0, Directory.GetFiles(StuntCapture.ShotDir(), "Snap_*").Length,
                $"none of them is a Snap_ file: the stunt camera writes its own names");
            ctx.Same(0, Directory.Exists(profileDir) ? Directory.GetFiles(profileDir, "Snap_*.PN_").Length : 0,
                $"and the run staged nothing into the profile directory");
            report.AppendLine($"C4/IA1: {capture.Count} stunt shot(s) under {StuntCapture.ShotDir()}, no Snap_ file");
        }
        finally
        {
            StuntCapture.DirectoryOverride = previous;
        }
    }

    // The readback lands frames after the crossing, so a zone that ends the mission is committed
    // before its file exists: the verdict waits for the landing instead of missing the file.
    private static void LandsAfterCommit(TestContext ctx, string dir, int mission)
    {
        foreach (bool won in new[] { true, false })
        {
            int objective = won ? 18 : 19;
            Action<Image?>? held = null;
            string? staged = CampaignSnapshot.Stage(dir, mission, objective, landed =>
            {
                held = landed;
                return true;
            });
            string kept = Path.Combine(dir, CampaignSnapshot.FileName(mission, objective));
            ctx.Check(staged != null && !File.Exists(staged),
                $"a {(won ? "won" : "lost")} mission's crossing stages nothing on disk until its frame lands");
            ctx.Same(1, CampaignSnapshot.Commit(dir, mission, won),
                $"…and the mission-end sweep still counts the photograph on its way");
            held?.Invoke(Pane());
            ctx.Check(!File.Exists(staged ?? "") && File.Exists(kept) == won,
                $"the late frame of a {(won ? "won" : "lost")} mission takes the sweep's verdict: kept={File.Exists(kept)}");
        }
    }

    private static int IndexOfLoose(IReadOnlyList<BoardPicture> pictures, string path)
    {
        for (int i = 0; i < pictures.Count; i++)
        {
            if (pictures[i].Art.Library == BoardArtLibrary.Loose
                && string.Equals(pictures[i].Art.Name, path, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfMount(IReadOnlyList<BoardPicture> pictures, ScrapbookScrap capture)
    {
        for (int i = 0; i < pictures.Count; i++)
        {
            if (pictures[i].Art.Name.Contains(MountName, StringComparison.OrdinalIgnoreCase)
                && Mathf.Abs(pictures[i].X - capture.X) < 8f
                && Mathf.Abs(pictures[i].Y - capture.Y) < 8f)
            {
                return i;
            }
        }

        return -1;
    }

    // The mission slot's own spreads, walked the way the book walks them, for the row that names
    // this file. Nothing declares how many spreads a mission has; item 1 missing ends the walk.
    private static (int Spread, ScrapbookScrap Scrap)? RowFor(TestContext ctx, int mission, string fileName)
    {
        for (int spread = 1; ; spread++)
        {
            var items = ScrapbookComposition.Items(ctx.DataRoot, mission, spread);
            if (items.Count == 0)
            {
                return null;
            }

            foreach (var scrap in items)
            {
                if (scrap.IsCapture && string.Equals(scrap.FileName, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return (spread, scrap);
                }
            }
        }
    }

    private static CampaignMission? MissionAt(IReadOnlyList<CampaignMission> missions)
    {
        foreach (var m in missions)
        {
            if (m.ChapterFolder.Equals(Chapter, StringComparison.OrdinalIgnoreCase)
                && m.MissionFolder.Equals(Mission, StringComparison.OrdinalIgnoreCase))
            {
                return m;
            }
        }

        return null;
    }

    // The pane both halves photograph: a solid frame, so the verdict rests on which file is
    // written rather than on what a headless host happened to render.
    private static Image Pane()
    {
        var img = Image.CreateEmpty(320, 240, false, Image.Format.Rgba8);
        img.Fill(new Color(0.3f, 0.5f, 0.2f));
        return img;
    }
}
