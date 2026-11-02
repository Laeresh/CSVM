using System;
using System.Globalization;
using System.IO;
using System.Linq;
using CSVM.Flight.Airframe;
using CSVM.Flight.Modes;
using CSVM.Launch;
using CSVM.Spec;
using CSVM.Tooling;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The shipped sample course read through both library sources in the engine. Then it is
/// flown in a <c>--stunt --course=</c> C1 session, the aircraft placed just short of each gate.</summary>
internal static class CourseLaunchSuites
{
    private const string Chapter = "C1";
    private const string Sample = "sample_out_and_back";

    // How far short of a gate the aircraft is placed, and how many steps it may take to cross.
    private const float LeadM = 8f;
    private const int CrossSteps = 20;

    [Suite("course-launch",
        "the shipped sample course lists and loads from res:// and a game-folder file of its name wins "
        + "it; a --det --stunt --course= C1 session spawns on the start point, draws the gates and flies "
        + "them in sequence order only, the clock standing until the first entry and stopping on the "
        + "last, every drawn gate standing at the pose its crossing is scored against; the finished run "
        + "records its best, penalty count and splits under the course key in the launch's --scores= file, an "
        + "old-format Danger Zone entry there loading and surviving untouched, and takes no photograph; a "
        + "slower rerun's gate flashes carry the signed delta to the stored splits and leave them stored")]
    internal static void CourseLaunch(TestContext ctx)
    {
        Sources(ctx);
        FlyCourse(ctx);
    }

    [Suite("course-race-local",
        "a --stunt --course=C1/sample_out_and_back race of two split screen panes, and a menu-built one of a "
        + "pane and an ace race bot: every seat flies its own run of the course, the drawn course carries the first "
        + "pane's cues, and each pane flown through every entry records its best under the course key in the launch's "
        + "--scores= file; the bot's touch cuts a course pylon and the bot's rerun stands it whole")]
    internal static void CourseRaceLocal(TestContext ctx)
    {
        ctx.RequireData(ctx.ChapterGamez(Chapter), $"{Chapter} gamez");
        var ambient = NetCombatSuites.Ambient.Save();
        try
        {
            string split = Scores(ctx, "split");
            var panes = SessionSpec.Parse(new[] { "--stunt", $"--course={Chapter}/{Sample}", "--plane=player_pfighter,player_fury", "--mute", "--no-pads", $"--scores={split}" });
            LocalRace(ctx, "split screen", panes, roster: null, split, flown: new[] { 0, 1 });

            string botted = Scores(ctx, "bot");
            var bot = SessionSpec.FromMenu(SessionSpec.Parse(new[] { "--mute", "--no-pads", $"--scores={botted}" }), Chapter, new[] { "player_pfighter" },
                MenuMode.Stunt, vsTimeMinutes: 5, missionType: DogfightMissionType.StuntRace,
                bots: new[] { new VsBotEntry("player_fury", Net.NetBotSkill.Ace, 0, "Bot 1") }, course: Sample);
            LocalRace(ctx, "race bot", bot, SeatFields.LocalVersusField(bot, ctx.Data.Messages), botted, flown: new[] { 0 });
        }
        finally
        {
            ambient.Restore();
        }
    }

    // The library's two reads in the engine: res:// for the shipped file, an OS folder for the game's.
    private static void Sources(TestContext ctx)
    {
        var shipped = new CourseLibrary(CourseLibrary.ShippedRoot, null);
        var listed = shipped.List(Chapter).FirstOrDefault(f => f.Name == Sample);
        ctx.Check(listed.Source == CourseSource.Shipped && listed.Path == $"{CourseLibrary.ShippedRoot}/{Chapter}/{Sample}.json",
            $"res:// lists the shipped sample: {listed.Path}");
        var course = shipped.Load(Chapter, Sample, out string error);
        ctx.Check(course is { Gates.Count: > 0, Sequence.Count: > 0 }, $"…and loads it: {error}");
        if (course == null)
        {
            return;
        }

        string folder = Path.Combine(ctx.ScratchDir, "course-launch", CourseLibrary.GameFolderName);
        var both = new CourseLibrary(CourseLibrary.ShippedRoot, folder);
        var moved = course with { Start = new SpawnPoint(course.Start.Position + (Vector3.Up * 10f), course.Start.HeadingDeg) };
        both.Save(moved, CourseSource.GameFolder);
        var winner = both.List(Chapter).FirstOrDefault(f => f.Name == Sample);
        var read = both.Load(Chapter, Sample, out error);
        ctx.Check(winner.Source == CourseSource.GameFolder && read?.Hash == moved.Hash && moved.Hash != course.Hash,
            $"a game-folder file of the same name wins it: {winner.Source}, hash {read?.Hash} for {moved.Hash} {error}");
    }

    private static void FlyCourse(TestContext ctx)
    {
        var savedClock = GameClock.Current;
        string? savedShots = StuntCapture.DirectoryOverride;
        StuntCapture.DirectoryOverride = Path.Combine(ctx.ScratchDir, "course-launch");
        if (Directory.Exists(StuntCapture.ShotDir()))
        {
            Directory.Delete(StuntCapture.ShotDir(), recursive: true);
        }

        string scores = Path.Combine(ctx.ScratchDir, "course-launch", "stunt_scores.json");
        var spec = SessionSpec.Parse(new[] { $"--course={Chapter}/{Sample}", "--det", "--mute", "--no-pads", $"--scores={scores}" });
        // An old-format file holding this plane's own Danger Zone best, which the course run must leave be.
        string dzKey = $"{Chapter}/IA1/{spec.PlaneName}";
        Directory.CreateDirectory(Path.GetDirectoryName(scores)!);
        File.WriteAllText(scores, $"{{\n  \"{dzKey}\": {{ \"best\": 300.0, \"date\": \"2000-01-01\" }}\n}}");
        var pane = new SubViewport
        {
            Size = new Vector2I(320, 180),
            OwnWorld3D = true,
            World3D = new World3D(),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
        };
        var camera = new Camera3D();
        var sun = new DirectionalLight3D();
        pane.AddChild(camera);
        pane.AddChild(sun);
        ctx.Host.AddChild(pane);
        var paths = new BasePaths(RepoRoot: ctx.RepoRoot, DataRoot: ctx.DataRoot) { Game = ctx.Game, Data = ctx.Data };
        var session = new GameSession(spec, new LauncherContext
        {
            Decode = ctx.Decode,
            Paths = paths,
            ProbeRunner = new ProbeRunner(paths),
            CaptureDirector = new CaptureDirector(spec),
            MasterSeed = 1,
            Camera = camera,
            Orbit = new Flight.Camera.OrbitCamera(camera),
            Sun = sun,
            Env = new Godot.Environment(),
            MenuDriven = false,
            MenuPads = null,
            ExitSession = () => { },
            RestartSession = () => { },
        });
        pane.AddChild(session);
        try
        {
            bool built = session.StartSession();
            ctx.Check(built && spec.Stunt && spec.Mission == "IA1", $"a --course={Chapter}/{Sample} stunt flight builds over {spec.Chapter}/{spec.Mission}");
            if (!built || session.SeatRigs.FirstOrDefault()?.Controller is not { } plane
                || plane.Stunt is not { Course: { } course } run)
            {
                ctx.Check(false, $"the session's pilot flies a course run");
                return;
            }

            ctx.Check(course.Name == Sample && run.TotalCount == course.Sequence.Count,
                $"the pilot's run is the course, {run.TotalCount} entries in place of the Danger Zones");
            ctx.Check(plane.WorldPosition.DistanceTo(course.Start.Position) < 1e-3f && plane.NoseDirection.Dot(course.Start.Forward) > 0.999f,
                $"the aircraft spawns on the start point heading its way: {plane.WorldPosition.DistanceTo(course.Start.Position):0.000} m off");
            var visuals = session.FindChild("course_visuals", true, false) as CourseVisuals;
            ctx.Check(visuals?.GateNodes.Count == course.Gates.Count, $"the course's gates are drawn in the world: {visuals?.GateNodes.Count} of {course.Gates.Count}");
            if (visuals == null)
            {
                return;
            }

            // A camera whose pane accepts every request, so only the course rule can keep it shut.
            // Each pass below starts inside a Danger Zone marker's radius of the gate centre.
            int photographs = 0;
            plane.StuntShots = new StuntCapture(run, Chapter, _ => ++photographs > 0);
            ctx.Check(LeadM < StuntMission.DzRadius, $"each pass starts {LeadM} m from the gate centre, inside the {StuntMission.DzRadius} m camera radius");

            Step(plane, 5);
            Ground(ctx, pane, course);
            plane.Rerun();
            ctx.Check(plane.WorldPosition.DistanceTo(course.Start.Position) < 1e-3f && run.CompletedCount == 0,
                $"a rerun lands on the start point: {plane.WorldPosition.DistanceTo(course.Start.Position):0.000} m off");

            // Out of order and the wrong way: the second entry's gate, then the first gate backwards.
            var first = course.Sequence[0];
            Through(plane, run, course, course.Sequence[1]);
            Through(plane, run, course, first with { Direction = Flip(first.Direction) });
            ctx.Check(run.CompletedCount == 0 && run.Elapsed == 0f,
                $"ABLE-TO-FAIL CONTROL: the second entry's gate and the first gate flown backwards count nothing, and the clock stands at {run.Elapsed}");

            const float dt = GameClock.FixedDt;
            float atLast = 0f;
            for (int i = 0; i < course.Sequence.Count; i++)
            {
                var entry = course.Sequence[i];
                bool crossed = Through(plane, run, course, entry);
                ctx.Check(crossed && run.CompletedCount == i + 1,
                    $"entry {i + 1}, {entry.GateId} {entry.Direction}, counts in order: {run.CompletedCount}/{run.TotalCount}, crashed={plane.Crashed}");
                if (i == 0)
                {
                    ctx.Check(run.Elapsed <= 2f * dt, $"…and the clock starts on it: {run.Elapsed:0.0000} s");
                    ctx.Check(plane.PilotHud.StuntRun?.FlashLine == StuntRunHud.ClearedFlash(run, run.Zones[0]),
                        $"…and with no best stored its flash carries no delta: '{plane.PilotHud.StuntRun?.FlashLine}'");
                    run.AddPenalty(CourseElements.TouchPenaltySeconds); // a touch, for the stored count
                }

                int g = course.Gates.ToList().FindIndex(x => x.Id == entry.GateId);
                visuals._Process(0.0);
                var scored = CourseMotion.Rest(course.Gates[g]) * run.GateMotion(g, run.SimSeconds());
                ctx.Check(visuals.GateNodes[g].GlobalTransform.IsEqualApprox(scored),
                    $"…its gate drawn at the pose it is scored against at {run.SimSeconds():0.000} s");
                if (course.Gates[g].Kind == CourseGateKind.BalloonRing)
                {
                    ctx.Check(!scored.IsEqualApprox(CourseMotion.Rest(course.Gates[g])), $"…a ring away from rest, so the motion is bound");
                }

                atLast = run.Elapsed;
            }

            Step(plane, 30);
            ctx.Check(run.AllComplete && run.Elapsed == atLast && atLast > 0f,
                $"the last entry completes the run and stops the clock at {StuntMission.FormatTime(atLast)}");
            string courseKey = $"course/{Chapter}/{Sample}/{course.Hash}/{spec.PlaneName}";
            Recorded(ctx, scores, dzKey, courseKey, run);
            ctx.Check(photographs == 0 && plane.StuntShots?.Count == 0 && !Directory.Exists(StuntCapture.ShotDir()),
                $"the course run takes no Danger Zone photograph: {photographs} request(s), {plane.StuntShots?.Count} latched");
            AgainstTheBest(ctx, plane, run, course, scores, courseKey);
        }
        finally
        {
            session.Free();
            pane.Free();
            GameClock.Current = savedClock;
            StuntCapture.DirectoryOverride = savedShots;
        }
    }

    // The aircraft placed short of the entry's gate centre and flown through it the entry's way, at
    // the gate's pose now. True once the run counts a further entry.
    private static bool Through(FlightController plane, StuntMission run, Course course, SequenceEntry entry)
    {
        var gate = course.Gates.First(x => x.Id == entry.GateId);
        var centre = CourseGateShape.OpeningCentre(gate);
        float lead = entry.Direction == GateDirection.Forward ? LeadM : -LeadM;
        var pose = CourseMotion.Pose(gate, run.SimSeconds());
        plane.RespawnAt(pose * (centre + (Vector3.Back * lead)), pose * (centre + (Vector3.Forward * lead)));
        int before = run.CompletedCount;
        for (int i = 0; i < CrossSteps && run.CompletedCount == before; i++)
        {
            Step(plane, 1);
        }

        return run.CompletedCount > before;
    }

    // The solo board's record, read back from the file. The course key holds the run's total and
    // penalty count; the seeded Danger Zone entry is the only other key.
    private static void Recorded(TestContext ctx, string scores, string dzKey, string courseKey, StuntMission run)
    {
        var store = ScoreStore.Load(scores);
        ctx.Check(run.PenaltyCount >= 1 && store.GetBest(courseKey) == run.Elapsed && store.GetPenalties(courseKey) == run.PenaltyCount,
            $"the run's best is stored under '{courseKey}' with its penalties: {store.GetBest(courseKey)} s for {run.Elapsed}, {store.GetPenalties(courseKey)} penalties for {run.PenaltyCount}");
        var splits = store.GetSplits(courseKey);
        ctx.Check(splits != null && splits.SequenceEqual(ScoreStore.SplitsOf(run)!) && splits.Length == run.TotalCount,
            $"…and its split at each entry: [{string.Join(", ", splits ?? System.Array.Empty<float>())}]");
        ctx.Check(store.GetBest(dzKey) == 300f && store.GetPenalties(dzKey) == null && store.GetSplits(dzKey) == null,
            $"the old-format Danger Zone entry '{dzKey}' loads unchanged: {store.GetBest(dzKey)} s, penalties {store.GetPenalties(dzKey)?.ToString() ?? "none"}, no splits");
        var keys = Godot.Json.ParseString(File.ReadAllText(scores)).AsGodotDictionary().Keys.Select(k => k.AsString()).OrderBy(k => k).ToArray();
        ctx.Check(keys.SequenceEqual(new[] { dzKey, courseKey }.OrderBy(k => k)),
            $"…and the file holds those two keys only: {string.Join(", ", keys)}");
    }

    // A rerun against the first run's stored splits. The first took a touch after entry 1, this one
    // two after entry 2: ahead at entry 2, behind from entry 3. Slower overall, it leaves the stored
    // best and its splits as they were.
    private static void AgainstTheBest(TestContext ctx, FlightController plane, StuntMission run, Course course, string scores, string courseKey)
    {
        var stored = ScoreStore.Load(scores);
        var best = stored.GetSplits(courseKey);
        float? bestTotal = stored.GetBest(courseKey);
        if (plane.PilotHud.StuntRun is not { } hud || best == null || run.TotalCount < 3)
        {
            ctx.Check(false, $"a second run needs the pilot's run HUD and the first run's splits: hud={plane.PilotHud.StuntRun != null}, splits={best != null}");
            return;
        }

        plane.Rerun();
        var flashes = new string?[run.TotalCount];
        for (int i = 0; i < run.TotalCount; i++)
        {
            Through(plane, run, course, course.Sequence[i]);
            flashes[i] = hud.FlashLine;
            if (i == 1)
            {
                run.AddPenalty(CourseElements.TouchPenaltySeconds);
                run.AddPenalty(CourseElements.TouchPenaltySeconds);
            }
        }

        Step(plane, 30);
        var deltas = run.Zones.Select(z => StuntRunHud.SplitDelta(run, z, best)).ToArray();
        ctx.Check(run.AllComplete && flashes.Select((f, i) => f == StuntRunHud.ClearedFlash(run, run.Zones[i], best)).All(x => x),
            $"the second run's every flash carries its delta to the stored split: {string.Join(" | ", flashes)}");
        ctx.Check(deltas[0] is > -0.5f and < 0.5f && deltas[1] is > -3.5f and < -2.5f && deltas[2] is > 2.5f and < 3.5f
                && flashes[1]!.EndsWith($"  {StuntRunHud.DeltaText(deltas[1]!.Value)}", System.StringComparison.Ordinal),
            $"…level at entry 1, ahead at entry 2 and behind at entry 3: {string.Join(", ", deltas)}");
        ctx.Check(!StuntRunHud.Behind(deltas[1]!.Value) && StuntRunHud.Behind(deltas[2]!.Value),
            $"…entry 2's flash draws green and entry 3's red");
        var after = ScoreStore.Load(scores);
        ctx.Check(run.Elapsed > bestTotal && after.GetBest(courseKey) == bestTotal && after.GetSplits(courseKey)?.SequenceEqual(best) == true,
            $"the slower run ({run.Elapsed} s against {bestTotal} s) leaves the stored best and its splits");
    }

    private static string Scores(TestContext ctx, string cell)
    {
        string path = Path.Combine(ctx.ScratchDir, "course-race-local", cell + "_scores.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Delete(path);
        return path;
    }

    // The race built from `spec`, each seat on its own course run. Once the window opens the `flown`
    // panes are placed through every entry, and their bests are read back from the file.
    private static void LocalRace(TestContext ctx, string cell, SessionSpec spec, Net.NetSeat[]? roster, string scores, int[] flown)
    {
        ctx.Check(spec is { Course: not null, CourseRefusal: null, Stunt: true } && spec.Players + spec.VsBots.Count == 2,
            $"[{cell}] the spec flies {spec.Course} with {spec.Players} pane(s) and {spec.VsBots.Count} bot(s), refusal {spec.CourseRefusal ?? "none"}");
        var end = NetCombatSuites.Ends.Open(ctx, spec, transport: null, isHost: true, 1, roster);
        try
        {
            var session = end.Session;
            var seats = end.Built ? session.SeatRigs.Select(r => r.Controller).ToArray() : Array.Empty<FlightController?>();
            var runs = seats.Select(s => s?.Stunt).ToArray();
            if (session.RaceDirector?.Race is not { } race || runs.Length != 2 || runs[0]?.Course is not { } course
                || runs[1]?.Course != course || ReferenceEquals(runs[0], runs[1]))
            {
                ctx.Check(false, $"[{cell}] the session builds a race of two seats, each on its own run of the course ({runs.Length} seat(s))");
                return;
            }

            if (session.FindChild("course_visuals", true, false) is not CourseVisuals { Cross: not null } visuals)
            {
                ctx.Check(false, $"[{cell}] the course is drawn, with the first pane's cues");
                return;
            }

            var peers = new[] { session };
            bool opened = CourseFlight.Until(peers, () => race.MayStartRun);
            foreach (int i in flown)
            {
                var plane = seats[i]!;
                plane.UseKeyboard = false;
                plane.PadDevices = Array.Empty<int>();
                int crossed = course.Sequence.Count(entry => CourseFlight.Through(peers, plane, runs[i]!, course, entry));
                CourseFlight.Until(peers, () => race.Of(i)?.BestTime != null);
                string key = race.Of(i)?.ScoreKey ?? "";
                float? board = race.Of(i)?.BestTime;
                float? best = ScoreStore.Load(scores).GetBest(key);
                ctx.Check(opened && crossed == course.Sequence.Count && key.StartsWith($"course/{Chapter}/{Sample}/{course.Hash}/", StringComparison.Ordinal)
                        && best is > 0f && best == board,
                    $"[{cell}] seat {i} crosses {crossed} of {course.Sequence.Count} entries and records its best under '{key}' ({Seconds(best)}, board {Seconds(board)})");
            }

            if (seats[1] is { IsBotSeat: true })
            {
                BotCutsItsOwn(ctx, cell, visuals, runs[1]!, course);
            }
        }
        finally
        {
            end.Close();
        }
    }

    // The bot's run brushes a pylon gate's left course pylon. The drawing tracks the bot's run, so
    // the course pylon is cut, and the run's reset, the path every rerun takes, stands it whole.
    private static void BotCutsItsOwn(TestContext ctx, string cell, CourseVisuals visuals, StuntMission run, Course course)
    {
        int g = course.Gates.ToList().FindIndex(x => x.Kind == CourseGateKind.PylonGate);
        if (g < 0)
        {
            ctx.Check(false, $"[{cell}] the course has a pylon gate to brush");
            return;
        }

        var gate = course.Gates[g];
        var stump = (ShaderMaterial)visuals.GateNodes[g].GetNode<MeshInstance3D>("pylon_left").MaterialOverride;
        float Top() => stump.GetShaderParameter("clip_hi_m").AsSingle();
        float height = 0.4f * gate.Height;
        float radius = Mathf.Lerp(CourseGateShape.PylonBaseRadius(gate), CourseGateShape.PylonTopRadius(gate), height / gate.Height);
        var pose = CourseMotion.Pose(gate, run.SimSeconds());
        run.Relocated();
        run.Update(pose * new Vector3((-0.5f * gate.Width) - radius - 10f, height, 0.3f), 1f);
        run.Update(pose * new Vector3((-0.5f * gate.Width) - radius - 0.5f, height, 0.3f), 1f);
        double now = run.SimSeconds();
        visuals.Pose(now + 0.1);
        ctx.Check(Top() < gate.Height, $"[{cell}] the bot's touch cuts {gate.Id}'s left course pylon at {Top().ToString("0.00", CultureInfo.InvariantCulture)} m");
        run.Reset();
        visuals.Pose(now + 0.2);
        ctx.Check(Top() == CoursePylonCut.NoClipM, $"[{cell}] the bot's rerun stands the course pylon it cut whole");
    }

    private static string Seconds(float? time) => time?.ToString("0.000", CultureInfo.InvariantCulture) ?? "none";

    private static GateDirection Flip(GateDirection d) => d == GateDirection.Forward ? GateDirection.Reverse : GateDirection.Forward;

    // A --det session's clock advances only in its frame callback, which a suite must not reach.
    // The step is thus the clock's and the pilot's own.
    private static void Step(FlightController plane, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            GameClock.Current!.BeginFrame(GameClock.FixedDt);
            plane.SimStep(GameClock.FixedDt);
        }
    }
    // The sample's heights against the C1 ground: a pylon gate stands on it, a ring clears it.
    private static void Ground(TestContext ctx, SubViewport pane, Course course)
    {
        var space = pane.FindWorld3D().DirectSpaceState;
        foreach (var gate in course.Gates)
        {
            var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                new Vector3(gate.Position.X, 4000f, gate.Position.Z), new Vector3(gate.Position.X, -500f, gate.Position.Z), CollisionLayers.World));
            if (hit.Count == 0)
            {
                ctx.Note($"{gate.Id}: no ground hit under ({gate.Position.X:0}, {gate.Position.Z:0})");
                continue;
            }

            float ground = hit["position"].AsVector3().Y;
            ctx.Note($"{gate.Id}: ground {ground:0.0} m under ({gate.Position.X:0}, {gate.Position.Z:0}), gate at {gate.Position.Y:0.0}");
            ctx.Check(gate.Kind == CourseGateKind.PylonGate ? Mathf.Abs(gate.Position.Y - ground) < 3f : gate.Position.Y - (gate.Diameter / 2f) > ground + 10f,
                $"{gate.Id} {(gate.Kind == CourseGateKind.PylonGate ? "stands on" : "clears")} the C1 ground at {ground:0.0} m");
        }
    }
}
