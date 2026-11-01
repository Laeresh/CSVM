using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight.Airframe;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Launch;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.Tooling;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>A course run's HUD and world cues. On a hand-made course the run HUD's words, its
/// flashes, the chevron and the dim highlight are read across every entry and a touch. In a C1
/// <c>--course=</c> flight the HUD and the cues are photographed into a montage for the user to judge.
/// A suite's single frame never reaches the deferred redraw, so the montage calls the two HUD
/// controls' own <c>_Draw</c> onto their cleared canvas items.</summary>
internal static class CourseHudSuites
{
    private const string Chapter = "C1";
    private const string Sample = "sample_out_and_back";
    private const int PaneWidth = 1280;
    private const int PaneHeight = 720;
    private const float LeadM = 8f;
    private const int CrossSteps = 20;
    private const float WidthM = 40f;
    private const float HeightM = 30f;
    private const float TouchHeightM = 12f;

    [Suite("course-hud",
        "a course run's HUD reads GATE n/N with the clock and penalty time, an intro line, a GATE n flash "
        + "with the split and a +3 s flash only for a touch that added time; its next entry's gate carries "
        + "a chevron pointing the way that entry is flown, posed with the bobbing ring, and the gate after it "
        + "a dim fill; in a C1 --course= flight the HUD, marker and cues are photographed into a montage")]
    internal static void CourseHud(TestContext ctx)
    {
        CheckRun(ctx);
        Montage(ctx);
    }

    // A pylon gate flown forward, a ring forward, then the pylon gate back the other way.
    private static void CheckRun(TestContext ctx)
    {
        var p = new CourseGate("p", CourseGateKind.PylonGate, new Vector3(10f, 5f, -20f), 30f, WidthM, HeightM, 0f);
        var r = new CourseGate("r", CourseGateKind.BalloonRing, new Vector3(-150f, 60f, -300f), 30f, 0f, 0f, 40f);
        var course = new Course(Chapter, "hud", new SpawnPoint(new Vector3(0f, 100f, 200f), 30f), new[] { p, r },
            new[] { new SequenceEntry("p", GateDirection.Forward), new SequenceEntry("r", GateDirection.Forward), new SequenceEntry("p", GateDirection.Reverse) });
        double now = 1.0;
        var run = StuntMission.ForCourse(course)!;
        run.SimSeconds = () => now;
        run.GateMotion = (g, t) => CourseMotion.Offset(course.Gates[g], t);
        var hud = StuntRunHud.Build(run);
        var visuals = CourseVisuals.Build(course).Follow(run);
        ctx.Host.AddChild(hud);
        ctx.Host.AddChild(visuals);
        try
        {
            visuals.Pose(now);
            ctx.Check(StuntRunHud.StatusLine(run) == "0:00.0    GATE 1/3" && hud.IntroText == StuntRunHud.CourseIntro && hud.FlashLine == null,
                $"before the first entry: '{StuntRunHud.StatusLine(run)}', intro '{hud.IntroText}'");
            Cues(ctx, visuals, course, 0, now, "before the first entry");

            Brush(run, p, now);
            ctx.Check(run.PenaltyCount == 0 && hud.PenaltyLine == null, $"a touch before the first entry adds no time and flashes nothing");

            Cross(run, p, GateDirection.Forward, now);
            visuals.Pose(now);
            ctx.Check(hud.FlashLine == "GATE 1  0:00.0" && StuntRunHud.StatusLine(run) == "0:00.0    GATE 2/3",
                $"entry 1 flashes '{hud.FlashLine}' and the line reads '{StuntRunHud.StatusLine(run)}'");
            Cues(ctx, visuals, course, 1, now, "after entry 1");

            now += 2.0;
            run.Tick(2f);
            Brush(run, p, now);
            ctx.Check(hud.PenaltyLine == "+3 s" && StuntRunHud.StatusLine(run) == "0:05.0    GATE 2/3    +3 s",
                $"a touch on the clock flashes '{hud.PenaltyLine}' and the line reads '{StuntRunHud.StatusLine(run)}'");
            hud._Process(1.0);
            string? held = hud.PenaltyLine;
            hud._Process(0.7);
            ctx.Check(held == "+3 s" && hud.PenaltyLine == null && hud.FlashLine == null, $"both flashes stand 1.6 s: at 1 s '{held}', at 1.7 s '{hud.PenaltyLine}'");

            // The ring bobs between the two times; the chevron rides with it.
            var a = Chevron(visuals, now = 10.0);
            var b = Chevron(visuals, now = 12.0);
            var expected = CourseMotion.Pose(r, 12.0).Origin;
            ctx.Check(a.DistanceTo(b) > 0.05f && b.DistanceTo(expected) < 1e-3f,
                $"the chevron rides the bobbing ring: moved {a.DistanceTo(b):0.000} m, {b.DistanceTo(expected):0.0000} m off the ring's pose");

            Cross(run, r, GateDirection.Forward, now);
            visuals.Pose(now);
            Cues(ctx, visuals, course, 2, now, "after entry 2, the reverse entry next");
            Cross(run, p, GateDirection.Reverse, now);
            visuals.Pose(now);
            ctx.Check(run.AllComplete && !visuals.Chevron!.Visible && !visuals.Highlight!.Visible && StuntRunHud.StatusLine(run).Contains("GATE 3/3"),
                $"the finished run shows no cue and reads '{StuntRunHud.StatusLine(run)}'");
            run.Reset();
            visuals.Pose(now);
            Cues(ctx, visuals, course, 0, now, "after a rerun");
        }
        finally
        {
            hud.Free();
            visuals.Free();
        }
    }

    // The chevron on entry `next`'s gate, apex along its flown way, and the dim fill on the entry after's.
    private static void Cues(TestContext ctx, CourseVisuals visuals, Course course, int next, double now, string when)
    {
        var chevron = visuals.Chevron!;
        var highlight = visuals.Highlight!;
        var entry = course.Sequence[next];
        var gate = course.Gates.First(g => g.Id == entry.GateId);
        var pose = CourseMotion.Pose(gate, now);
        var travel = (pose.Basis * Vector3.Forward).Normalized() * (entry.Direction == GateDirection.Forward ? 1f : -1f);
        var apex = -chevron.Transform.Basis.Z.Normalized();
        ctx.Check(chevron.Visible && chevron.Transform.Origin.DistanceTo(pose * Centre(gate)) < 1e-3f && apex.Dot(travel) > 0.999f,
            $"{when}: the chevron stands in {gate.Id}'s aperture pointing {entry.Direction}: shown {chevron.Visible}, apex·travel {apex.Dot(travel):0.000}");
        if (next + 1 < course.Sequence.Count)
        {
            var after = course.Gates.First(g => g.Id == course.Sequence[next + 1].GateId);
            ctx.Check(highlight.Visible && highlight.Transform.Origin.DistanceTo(CourseMotion.Pose(after, now) * Centre(after)) < 1e-3f,
                $"{when}: the gate after it, {after.Id}, is dimly filled");
        }
        else
        {
            ctx.Check(!highlight.Visible, $"{when}: no entry after the next, no fill");
        }
    }

    private static Vector3 Chevron(CourseVisuals visuals, double now)
    {
        visuals.Pose(now);
        return visuals.Chevron!.Transform.Origin;
    }

    private static Vector3 Centre(CourseGate gate) =>
        gate.Kind == CourseGateKind.PylonGate ? new Vector3(0f, gate.Height / 2f, 0f) : Vector3.Zero;

    private static void Cross(StuntMission run, CourseGate gate, GateDirection direction, double now)
    {
        var pose = CourseMotion.Pose(gate, now);
        float lead = direction == GateDirection.Forward ? 5f : -5f;
        run.Relocated();
        run.Update(pose * (Centre(gate) + (Vector3.Back * lead)));
        run.Update(pose * (Centre(gate) + (Vector3.Forward * lead)));
    }

    // Into the left course pylon's outside from clear of it, beside the gate's plane.
    private static void Brush(StuntMission run, CourseGate gate, double now)
    {
        var pose = CourseMotion.Pose(gate, now);
        float radius = Mathf.Lerp(CourseGateShape.PylonBaseRadius(gate), CourseGateShape.PylonTopRadius(gate), TouchHeightM / gate.Height);
        run.Relocated();
        run.Update(pose * new Vector3((-0.5f * gate.Width) - radius - 10f, TouchHeightM, 0.3f), 1f);
        run.Update(pose * new Vector3((-0.5f * gate.Width) - radius - 0.5f, TouchHeightM, 0.3f), 1f);
    }

    // The shipped sample flown in a C1 --det session. Its pane is photographed with the run HUD and
    // the target HUD drawn, and the ring's chevron from the side both ways round.
    private static void Montage(TestContext ctx)
    {
        var savedClock = GameClock.Current;
        string? savedShots = StuntCapture.DirectoryOverride;
        StuntCapture.DirectoryOverride = Path.Combine(ctx.ScratchDir, "course-hud");
        var spec = SessionSpec.Parse(new[] { $"--course={Chapter}/{Sample}", "--det", "--view=chase", "--mute", "--no-pads" });
        var pane = new SubViewport
        {
            Size = new Vector2I(PaneWidth, PaneHeight),
            OwnWorld3D = true,
            World3D = new World3D(),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
        };
        var camera = new Camera3D { Fov = 60f, Far = 20000f };
        var sun = new DirectionalLight3D { RotationDegrees = new Vector3(-45, 150, 0), LightEnergy = WeatherRig.DefaultEnergies.Sun, ShadowEnabled = false };
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = new ProceduralSkyMaterial() },
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = Colors.White,
            AmbientLightEnergy = WeatherRig.DefaultEnergies.Ambient,
        };
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
            Env = env,
            MenuDriven = false,
            MenuPads = null,
            ExitSession = () => { },
            RestartSession = () => { },
        });
        pane.AddChild(session);
        var views = new List<SubViewport>();
        try
        {
            bool built = session.StartSession();
            if (!built || session.SeatRigs.FirstOrDefault()?.Controller is not { } plane
                || plane.Stunt is not { Course: { } course } run
                || plane.PilotHud.StuntRun is not { } hud || plane.PilotHud.TargetHud is not { } targets
                || session.FindChild("course_visuals", true, false) is not CourseVisuals visuals)
            {
                ctx.Check(false, $"a --course={Chapter}/{Sample} flight builds with its run HUD, target HUD and course visuals");
                return;
            }

            // The spyglass disc draws through calls this frame cannot make outside the deferred redraw.
            targets.SpyglassOn = false;
            string dir = Path.Combine(ctx.ScratchDir, "course-hud");
            Directory.CreateDirectory(dir);
            var shots = new List<(string Name, Image Image)>();
            var gates = course.Gates.ToList();
            CourseGate Gate(int entry) => gates.First(g => g.Id == course.Sequence[entry].GateId);
            bool Shoot(string name, SubViewport view, bool withHud, (Vector3 Eye, Vector3 At)? seat = null)
            {
                visuals.Pose(run.SimSeconds());
                if (seat is { } placed)
                {
                    camera.LookAtFromPosition(placed.Eye, placed.At, Vector3.Up);
                }

                if (withHud)
                {
                    DrawHud(hud, targets);
                }

                Settle(view);
                if (Render(view) is not { } image)
                {
                    ctx.Note($"{name}: no image, the process draws nothing");
                    return false;
                }

                image.SavePng(Path.Combine(dir, name + ".png"));
                shots.Add((name, image));
                return true;
            }

            SubViewport Side(CourseGate gate)
            {
                var pose = CourseMotion.Pose(gate, run.SimSeconds());
                var eye = pose * (Centre(gate) + new Vector3(70f, 12f, -10f));
                var view = Eye(ctx, pane, camera, eye, pose * Centre(gate));
                views.Add(view);
                return view;
            }

            Step(plane, hud, 5);
            ctx.Check(Through(plane, hud, run, course, 0), $"the sample's first entry counts");

            // (1) On the approach to the ring, entry 2, the clock running.
            var ring = Gate(1);
            Approach(plane, hud, run, ring, GateDirection.Forward, 220f);
            visuals.Pose(run.SimSeconds());
            ctx.Check(run.CompletedCount == 1 && targets.Selected?.DisplayName == "Gate 2" && visuals.Chevron!.Visible,
                $"approaching entry 2: selected '{targets.Selected?.DisplayName}', status '{StuntRunHud.StatusLine(run)}'");
            if (!Shoot("hud-next-gate", pane, withHud: true))
            {
                return;
            }

            Shoot("side-ring-forward", Side(ring), withHud: false);
            var after = Gate(2);
            var afterPose = CourseMotion.Pose(after, run.SimSeconds());
            var fillView = Eye(ctx, pane, camera, afterPose * (Centre(after) + new Vector3(30f, 8f, 140f)), afterPose * Centre(after));
            views.Add(fillView);
            Shoot("dim-fill-next-after", fillView, withHud: false);

            // (2) Through the ring, then outside the next pylon gate's left course pylon: time added.
            ctx.Check(Through(plane, hud, run, course, 1), $"the sample's second entry counts");
            int touched = 0;
            run.ElementTouched += t => touched += t.TimeAdded ? 1 : 0;
            var pylons = Gate(2);
            var pylonPose = CourseMotion.Pose(pylons, run.SimSeconds());
            // High on the course pylon: the ground rises short of this gate, and a low pass meets it.
            var outside = new Vector3((-0.5f * pylons.Width) - 1.5f, 0.8f * pylons.Height, 0f);
            plane.RespawnAt(pylonPose * (outside + (Vector3.Back * 40f)), pylonPose * (outside + (Vector3.Forward * 40f)));
            for (int i = 0; i < 120 && touched == 0; i++)
            {
                Step(plane, hud, 1);
            }

            Step(plane, hud, 20);
            ctx.Check(touched > 0 && run.CompletedCount == 2 && hud.PenaltyLine == "+3 s",
                $"a brush of {pylons.Id}'s left course pylon adds time: flash '{hud.PenaltyLine}', status '{StuntRunHud.StatusLine(run)}'");
            // The chase camera stands in the course pylon here, so the pane's camera watches the gate.
            Shoot("hud-penalty", pane, withHud: true,
                (pylonPose * new Vector3((-0.5f * pylons.Width) - 30f, 0.7f * pylons.Height, 70f), pylonPose * Centre(pylons)));

            // (3) The last entry flies the ring back the other way.
            ctx.Check(Through(plane, hud, run, course, 2) && Through(plane, hud, run, course, 3), $"the sample's third and fourth entries count");
            Approach(plane, hud, run, ring, GateDirection.Reverse, 220f);
            ctx.Check(run.CompletedCount == 4 && course.Sequence[4].Direction == GateDirection.Reverse,
                $"approaching the reverse entry: status '{StuntRunHud.StatusLine(run)}'");
            Shoot("hud-reverse", pane, withHud: true);
            Shoot("side-ring-reverse", Side(ring), withHud: false);

            if (shots.Count > 0)
            {
                string montage = Path.Combine(dir, "montage.png");
                Grid(shots.Select(s => s.Image).ToList(), 2).SavePng(montage);
                ctx.Note($"montage {montage}: {string.Join(", ", shots.Select(s => s.Name))}");
            }
        }
        finally
        {
            foreach (var view in views)
            {
                view.Free();
            }

            session.Free();
            pane.Free();
            GameClock.Current = savedClock;
            StuntCapture.DirectoryOverride = savedShots;
        }
    }

    // The aircraft placed on the entry's way in, `distance` short of the gate, and flown a second.
    private static void Approach(FlightController plane, StuntRunHud hud, StuntMission run, CourseGate gate, GateDirection direction, float distance)
    {
        var pose = CourseMotion.Pose(gate, run.SimSeconds());
        float side = direction == GateDirection.Forward ? 1f : -1f;
        plane.RespawnAt(pose * (Centre(gate) + (Vector3.Back * distance * side)), pose * Centre(gate));
        Step(plane, hud, 60);
    }

    // The aircraft placed short of the entry's gate and flown through it, as course-launch does.
    private static bool Through(FlightController plane, StuntRunHud hud, StuntMission run, Course course, int entry)
    {
        var e = course.Sequence[entry];
        var gate = course.Gates.First(g => g.Id == e.GateId);
        float lead = e.Direction == GateDirection.Forward ? LeadM : -LeadM;
        var pose = CourseMotion.Pose(gate, run.SimSeconds());
        plane.RespawnAt(pose * (Centre(gate) + (Vector3.Back * lead)), pose * (Centre(gate) + (Vector3.Forward * lead)));
        int before = run.CompletedCount;
        for (int i = 0; i < CrossSteps && run.CompletedCount == before; i++)
        {
            Step(plane, hud, 1);
        }

        return run.CompletedCount > before;
    }

    // A --det session's clock advances only in its frame callback, which a suite must not reach. The
    // step is thus the clock's, the pilot's and its frame's, which moves the chase camera and the HUD.
    private static void Step(FlightController plane, StuntRunHud hud, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            GameClock.Current!.BeginFrame(GameClock.FixedDt);
            plane.SimStep(GameClock.FixedDt);
            plane._Process(GameClock.FixedDt);
            hud._Process(GameClock.FixedDt);
        }
    }

    private static void DrawHud(StuntRunHud hud, TargetHud targets)
    {
        hud._Process(0.0);
        RenderingServer.CanvasItemClear(hud.GetCanvasItem());
        hud._Draw();
        targets._Process(0.0);
        RenderingServer.CanvasItemClear(targets.GetCanvasItem());
        targets._Draw();
    }

    // A viewport of its own on the flight's world, with the session's environment and cull mask.
    private static SubViewport Eye(TestContext ctx, SubViewport pane, Camera3D seat, Vector3 eye, Vector3 at)
    {
        var view = new SubViewport
        {
            Size = new Vector2I(PaneWidth, PaneHeight),
            World3D = pane.FindWorld3D(),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
        };
        var camera = new Camera3D { Fov = 60f, Far = 20000f, Current = true, Environment = seat.Environment, CullMask = seat.CullMask };
        view.AddChild(camera);
        ctx.Host.AddChild(view);
        camera.MakeCurrent();
        camera.LookAtFromPosition(eye, at, Vector3.Up);
        return view;
    }

    private static Image? Render(SubViewport view)
    {
        view.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        RenderingServer.ForceDraw();
        RenderingServer.ForceDraw();
        var image = view.GetTexture()?.GetImage();
        view.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        return image == null || image.IsEmpty() ? null : image;
    }

    // A suite never reaches the frame's transform flush, so every pose a render reads is pushed first.
    private static void Settle(Node node)
    {
        (node as Node3D)?.ForceUpdateTransform();
        foreach (var child in node.GetChildren())
        {
            Settle(child);
        }
    }

    private static Image Grid(IReadOnlyList<Image> tiles, int columns)
    {
        int rows = (tiles.Count + columns - 1) / columns;
        var grid = Image.CreateEmpty(PaneWidth * columns, PaneHeight * rows, false, Image.Format.Rgb8);
        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = (Image)tiles[i].Duplicate();
            tile.Convert(Image.Format.Rgb8);
            grid.BlitRect(tile, new Rect2I(0, 0, PaneWidth, PaneHeight), new Vector2I(i % columns * PaneWidth, i / columns * PaneHeight));
        }

        return grid;
    }
}
