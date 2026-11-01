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
        + "last, every drawn gate standing at the pose its crossing is scored against")]
    internal static void CourseLaunch(TestContext ctx)
    {
        Sources(ctx);
        FlyCourse(ctx);
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
        var spec = SessionSpec.Parse(new[] { $"--course={Chapter}/{Sample}", "--det", "--mute", "--no-pads" });
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
        var centre = gate.Kind == CourseGateKind.PylonGate ? new Vector3(0f, gate.Height / 2f, 0f) : Vector3.Zero;
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
