using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight.Airframe;
using CSVM.Flight.Modes;
using CSVM.Launch;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.Tooling;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The course visuals on a hand-made course and in a C1 flight. The gate nodes stand at
/// <see cref="CourseMotion"/>'s pose. The montage of the look is written to the run's scratch for
/// the user to judge; no pixel is asserted.</summary>
internal static class CourseVisualsSuites
{
    private const string Chapter = "C1";
    private const int SettleSteps = 3;
    private const int TileWidth = 640;
    private const int TileHeight = 360;
    private const float PylonWidthM = 40f;
    private const float PylonHeightM = 30f;
    private const float RingDiameterM = 40f;
    private const float CutHeightM = 13f;
    private const float TouchHeightM = 12f;
    private const double TouchAt = 10.0;

    [Suite("course-visuals",
        "a course's procedural gates build two course pylons per pylon gate and a ring, three balloons and "
        + "three tethers per balloon ring, every gate node standing at CourseMotion's pose for the sim time "
        + "it is posed at, on a shader reading csky_time and never TIME; a run's touch of a course pylon "
        + "cuts that course pylon alone at the touch's height, its top vanishing as it lands, whole again at the regrow time or on the run's reset; "
        + "in a C1 flight they are photographed close, wide, a second apart and through a cut into montages")]
    internal static void CourseVisualsDraw(TestContext ctx)
    {
        CheckBuild(ctx);
        CheckCut(ctx);
        Montage(ctx);
    }

    // A run's real touch, brushing the left course pylon's outside from clear of the gate, drives the
    // cut. The uniforms are read back from both course pylons' materials at sim times around it.
    private static void CheckCut(TestContext ctx)
    {
        var gate = new CourseGate("p", CourseGateKind.PylonGate, new Vector3(10f, 5f, -20f), 30f, PylonWidthM, PylonHeightM, 0f);
        var course = new Course(Chapter, "cut", new SpawnPoint(new Vector3(0f, 100f, 0f), 0f), new[] { gate },
            new[] { new SequenceEntry("p", GateDirection.Forward) });
        double now = 0.0;
        var run = StuntMission.ForCourse(course)!;
        run.SimSeconds = () => now;
        var touches = new List<CourseTouch>();
        run.ElementTouched += touches.Add;
        var visuals = CourseVisuals.Build(course).Follow(run);
        ctx.Host.AddChild(visuals);
        try
        {
            var left = visuals.GateNodes[0].GetNode<MeshInstance3D>("pylon_left");
            var top = left.GetNode<MeshInstance3D>("top");
            var stump = (ShaderMaterial)left.MaterialOverride;
            var falling = (ShaderMaterial)top.MaterialOverride;
            var right = (ShaderMaterial)visuals.GateNodes[0].GetNode<MeshInstance3D>("pylon_right").MaterialOverride;
            float Uniform(ShaderMaterial m, string name) => m.GetShaderParameter(name).AsSingle();

            var rest = CourseMotion.Rest(gate);
            float radius = Mathf.Lerp(CourseGateShape.PylonBaseRadius(gate), CourseGateShape.PylonTopRadius(gate), TouchHeightM / PylonHeightM);
            var clear = rest * new Vector3((-0.5f * PylonWidthM) - radius - 10f, TouchHeightM, 0.3f);
            var brush = rest * new Vector3((-0.5f * PylonWidthM) - radius - 0.5f, TouchHeightM, 0.3f);
            run.Update(clear, 1f);
            now = TouchAt;
            run.Update(brush, 1f);
            ctx.Same(1, touches.Count, $"one touch of the gate");
            if (touches.Count != 1 || touches[0].Element != CourseElement.LeftCoursePylon)
            {
                ctx.Check(false, $"the touch is of the left course pylon: {touches.Count} touches, first {touches.FirstOrDefault().Element}");
                return;
            }

            float cut = touches[0].PylonHeight;
            visuals.Pose(TouchAt);
            ctx.Check(Mathf.IsEqualApprox(Uniform(stump, "clip_hi_m"), cut) && Mathf.IsEqualApprox(Uniform(falling, "clip_lo_m"), cut) && top.Visible,
                $"the stump ends and the falling top starts at the touch's {cut:0.00} m: stump {Uniform(stump, "clip_hi_m"):0.00}, top {Uniform(falling, "clip_lo_m"):0.00}, shown {top.Visible}");
            ctx.Check(Uniform(right, "clip_hi_m") == CoursePylonCut.NoClipM, $"the right course pylon stays whole");

            double landing = Math.Sqrt(2.0 * cut / CoursePylonCut.FallAccel);
            visuals.Pose(TouchAt + landing - 0.05);
            float drop = Uniform(falling, "drop_m");
            ctx.Check(top.Visible && drop > 0f && drop < cut, $"the top is still falling just before it lands at {landing:0.00} s: dropped {drop:0.00} m of {cut:0.00}");
            visuals.Pose(TouchAt + landing + 0.01);
            ctx.Check(!top.Visible, $"the top vanishes as it lands");

            visuals.Pose(TouchAt + 2.0);
            var mid = CoursePylonCut.At(PylonHeightM, cut, CoursePylonCut.NoClipM, 2.0);
            ctx.Check(Mathf.IsEqualApprox(Uniform(stump, "clip_hi_m"), mid.StumpTopM) && Uniform(stump, "sag_m") > 0f
                    && mid.StumpTopM > cut && mid.StumpTopM < PylonHeightM && !top.Visible,
                $"mid-regrow the stump stands at {Uniform(stump, "clip_hi_m"):0.00} m of {PylonHeightM}, sagging {Uniform(stump, "sag_m"):0.00} m, the top gone");
            visuals.Pose(TouchAt + CoursePylonCut.RegrowSeconds - 0.02);
            ctx.Check(Uniform(stump, "clip_hi_m") < PylonHeightM, $"still regrowing just short of {CoursePylonCut.RegrowSeconds} s");
            visuals.Pose(TouchAt + CoursePylonCut.RegrowSeconds);
            ctx.Check(Uniform(stump, "clip_hi_m") == CoursePylonCut.NoClipM && Uniform(stump, "sag_m") == 0f && !top.Visible,
                $"whole at {CoursePylonCut.RegrowSeconds} s");

            visuals.Cut(0, CourseElement.RingRim, TouchHeightM, TouchAt + 4.0);
            visuals.Cut(0, CourseElement.Balloon, TouchHeightM, TouchAt + 4.0);
            visuals.Pose(TouchAt + 4.0);
            ctx.Check(Uniform(stump, "clip_hi_m") == CoursePylonCut.NoClipM && Uniform(right, "clip_hi_m") == CoursePylonCut.NoClipM,
                $"a rim or balloon touch cuts nothing");

            run.Update(clear, 1f);
            now = TouchAt + 5.0;
            run.Update(brush, 1f);
            visuals.Pose(now + 0.2);
            ctx.Check(touches.Count == 2 && Uniform(stump, "clip_hi_m") < PylonHeightM && top.Visible, $"a second touch, after leaving, cuts again");
            run.Reset();
            visuals.Pose(now + 0.3);
            ctx.Check(Uniform(stump, "clip_hi_m") == CoursePylonCut.NoClipM && Uniform(stump, "sag_m") == 0f && !top.Visible,
                $"the run's reset stands the course pylon whole mid-regrow");
        }
        finally
        {
            visuals.Free();
        }
    }

    private static void CheckBuild(TestContext ctx)
    {
        var gates = new[]
        {
            new CourseGate("p", CourseGateKind.PylonGate, new Vector3(10f, 5f, -20f), 30f, PylonWidthM, PylonHeightM, 0f),
            new CourseGate("r", CourseGateKind.BalloonRing, new Vector3(-50f, 60f, -300f), 200f, 0f, 0f, RingDiameterM),
        };
        var course = new Course(Chapter, "suite", new SpawnPoint(new Vector3(0f, 100f, 0f), 0f), gates,
            new[] { new SequenceEntry("p", GateDirection.Forward), new SequenceEntry("r", GateDirection.Reverse) });
        var visuals = CourseVisuals.Build(course, withStart: true);
        ctx.Host.AddChild(visuals);
        try
        {
            var nodes = visuals.GateNodes;
            ctx.Same(2, nodes.Count, $"one node per pool gate");
            ctx.Same(2, nodes[0].GetChildCount(), $"a pylon gate draws two course pylons");
            ctx.Same(1 + (2 * CourseGateShape.BalloonAnglesDeg.Length), nodes[1].GetChildCount(), $"a ring draws its torus, balloons and tethers");
            ctx.Same(3, visuals.GetChildCount(), $"the two gates and the start marker");
            foreach (double t in new[] { 0.0, 1.0, 37.25 })
            {
                visuals.Pose(t);
                for (int i = 0; i < gates.Length; i++)
                {
                    ctx.Check(nodes[i].GlobalTransform.IsEqualApprox(CourseMotion.Pose(gates[i], t)),
                        $"gate {gates[i].Id} is drawn at its motion pose at {t} s");
                }
            }

            visuals.Pose(0.0);
            var rest = nodes[1].Transform;
            visuals.Pose(2.0);
            ctx.Check(!nodes[1].Transform.IsEqualApprox(rest) && nodes[0].Transform.IsEqualApprox(CourseMotion.Rest(gates[0])),
                $"the ring moves between 0 s and 2 s and the pylon gate stands at rest");
            var materials = visuals.FindChildren("*", nameof(MeshInstance3D), true, false)
                .Select(n => ((MeshInstance3D)n).Mesh.SurfaceGetMaterial(0)).OfType<ShaderMaterial>().ToList();
            ctx.Check(materials.Count > 0 && materials.All(m => m.Shader.Code.Contains("csky_time", StringComparison.Ordinal)
                    && !m.Shader.Code.Contains("TIME", StringComparison.Ordinal)),
                $"every part draws on csky_time, never TIME ({materials.Count} materials)");
        }
        finally
        {
            visuals.Free();
        }
    }

    // A C1 flight in its own pane with a pylon gate and two rings ahead of the spawn. Five views and
    // the one-second pair's difference make the montage. The aircraft stays in the wide view for scale.
    private static void Montage(TestContext ctx)
    {
        var savedClock = GameClock.Current;
        var spec = SessionSpec.Parse(new[] { $"--chapter={Chapter}", "--mute", "--no-pads" });
        var pane = new SubViewport
        {
            Size = new Vector2I(TileWidth, TileHeight),
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
        CourseVisuals? visuals = null;
        var views = new List<SubViewport>();
        try
        {
            bool built = session.StartSession();
            ctx.Check(built, $"a {Chapter} flight builds");
            if (!built || session.SeatRigs.FirstOrDefault()?.Controller is not { } plane)
            {
                return;
            }

            for (int i = 0; i < SettleSteps; i++)
            {
                session._PhysicsProcess(GameClock.FixedDt);
            }

            var course = AheadOf(ctx, pane, plane.GlobalTransform);
            visuals = CourseVisuals.Build(course, withStart: true);
            pane.AddChild(visuals);
            var pylons = course.Gates[0];
            var ring = course.Gates[1];
            var f = pylons.Forward;
            var r = f.Cross(Vector3.Up);
            var ringTop = ring.Position + (Vector3.Up * 18f);
            double t0 = WidestSecond(ring);
            var shots = new (string Name, Vector3 Eye, Vector3 At, double Time)[]
            {
                ("pylon-gate-close", pylons.Position - (r * 55f) - (f * 45f) + (Vector3.Up * 10f), pylons.Position + (Vector3.Up * 14f), t0),
                ("ring-close", ring.Position - (f * 110f) + (r * 30f) + (Vector3.Up * 6f), ringTop, t0),
                ("wide", plane.GlobalPosition - (f * 90f) + (r * 40f) + (Vector3.Up * 30f), ring.Position, t0),
                ("ring-bob-t0", ring.Position - (f * 90f), ring.Position, t0),
                ("ring-bob-t1", ring.Position - (f * 90f), ring.Position, t0 + 1.0),
            };
            string dir = Path.Combine(ctx.ScratchDir, "course-visuals");
            Directory.CreateDirectory(dir);
            List<Image>? Shoot(IEnumerable<(string Name, Vector3 Eye, Vector3 At, double Time)> list)
            {
                var taken = new List<Image>();
                foreach (var shot in list)
                {
                    visuals!.Pose(shot.Time);
                    RenderingServer.GlobalShaderParameterSet(ShaderTime.Param, (float)shot.Time);
                    var view = Eye(ctx, pane, camera, shot.Eye, shot.At);
                    views.Add(view);
                    Settle(pane);
                    if (Render(view) is not { } image)
                    {
                        ctx.Note($"{shot.Name}: no image, the process draws nothing");
                        return null;
                    }

                    image.SavePng(Path.Combine(dir, shot.Name + ".png"));
                    taken.Add(image);
                }

                return taken;
            }

            if (Shoot(shots) is not { } images)
            {
                return;
            }

            ctx.Check(visuals.GateNodes[1].GlobalTransform.IsEqualApprox(CourseMotion.Pose(ring, t0 + 1.0)),
                $"the photographed ring stands at its motion pose");
            images.Add(Difference(images[3], images[4]));
            string montage = Path.Combine(dir, "montage.png");
            Grid(images, 3).SavePng(montage);
            ctx.Note($"montage {montage}; bob pair at {t0:0.00} s and {t0 + 1.0:0.00} s, ring moved {CourseMotion.Offset(ring, t0).Origin.DistanceTo(CourseMotion.Offset(ring, t0 + 1.0).Origin):0.00} m");

            // The left course pylon cut a little under half way up, seen from the close view's sunlit side.
            // It is whole, then its top tips away and falls as the stump sags. The top is gone on landing,
            // and the stump regrows and stands whole.
            double cutAt = t0 + 5.0;
            double lands = Math.Sqrt(2.0 * CutHeightM / CoursePylonCut.FallAccel);
            visuals.Cut(0, CourseElement.LeftCoursePylon, CutHeightM, cutAt);
            var leftBase = CourseMotion.Rest(pylons) * CourseGateShape.PylonBase(pylons, 0);
            var cutEye = leftBase - (f * 45f) - (r * 30f) + (Vector3.Up * 10f);
            var cutLook = leftBase + (Vector3.Up * 12f);
            var cutShots = new (string Name, Vector3 Eye, Vector3 At, double Time)[]
            {
                ("cut-whole", cutEye, cutLook, cutAt - 1.0),
                ("cut-tipping", cutEye, cutLook, cutAt + 0.5),
                ("cut-falling", cutEye, cutLook, cutAt + lands - 0.1),
                ("cut-landed", cutEye, cutLook, cutAt + lands + 0.05),
                ("cut-regrowing", cutEye, cutLook, cutAt + 2.3),
                ("cut-regrown", cutEye, cutLook, cutAt + CoursePylonCut.RegrowSeconds + 0.1),
            };
            if (Shoot(cutShots) is { } cut)
            {
                string cutMontage = Path.Combine(dir, "cut-montage.png");
                Grid(cut, 3).SavePng(cutMontage);
                ctx.Note($"cut montage {cutMontage}; cut at {CutHeightM} m, shots at -1, 0.5, {lands - 0.1:0.00}, {lands + 0.05:0.00}, 2.3 and {CoursePylonCut.RegrowSeconds + 0.1} s");
            }
        }
        finally
        {
            foreach (var view in views)
            {
                view.Free();
            }

            visuals?.Free();
            session.Free();
            pane.Free();
            GameClock.Current = savedClock;
        }
    }

    // A pylon gate 300 m ahead on the ground, a ring 550 m ahead and right, one 800 m ahead and
    // left. All face the aircraft's heading. The start point is the aircraft's own pose.
    private static Course AheadOf(TestContext ctx, SubViewport pane, Transform3D pose)
    {
        var f = -pose.Basis.Z with { Y = 0f };
        f = f.Normalized();
        var r = f.Cross(Vector3.Up);
        float heading = Mathf.RadToDeg(Mathf.Atan2(-f.X, -f.Z));
        var space = pane.FindWorld3D().DirectSpaceState;
        float Ground(Vector3 at)
        {
            var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                new Vector3(at.X, 4000f, at.Z), new Vector3(at.X, -500f, at.Z), CollisionLayers.World));
            if (hit.Count > 0)
            {
                return hit["position"].AsVector3().Y;
            }

            ctx.Note($"no ground under ({at.X:0}, {at.Z:0}), standing it 80 m below the aircraft");
            return pose.Origin.Y - 80f;
        }

        var p = pose.Origin + (f * 300f);
        var a = pose.Origin + (f * 550f) + (r * 80f);
        var b = pose.Origin + (f * 800f) - (r * 60f);
        var gates = new[]
        {
            new CourseGate("g1", CourseGateKind.PylonGate, p with { Y = Ground(p) }, heading, PylonWidthM, PylonHeightM, 0f),
            new CourseGate("g2", CourseGateKind.BalloonRing, a with { Y = Ground(a) + 50f }, heading, 0f, 0f, RingDiameterM),
            new CourseGate("g3", CourseGateKind.BalloonRing, b with { Y = Ground(b) + 70f }, heading, 0f, 0f, RingDiameterM),
        };
        return new Course(Chapter, "montage", new SpawnPoint(pose.Origin, heading), gates,
            gates.Select(g => new SequenceEntry(g.Id, GateDirection.Forward)).ToList());
    }

    // The start of the second, in a 10 s window, over which the ring moves furthest.
    private static double WidestSecond(CourseGate ring) =>
        Enumerable.Range(0, 100).Select(i => 10.0 + (i * 0.1))
            .MaxBy(t => CourseMotion.Offset(ring, t).Origin.DistanceTo(CourseMotion.Offset(ring, t + 1.0).Origin));

    // A viewport of its own on the flight's world, with the session's environment and cull mask.
    private static SubViewport Eye(TestContext ctx, SubViewport pane, Camera3D seat, Vector3 eye, Vector3 at)
    {
        var view = new SubViewport
        {
            Size = new Vector2I(TileWidth, TileHeight),
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
        Settle(view);
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

    // Four times the absolute difference, so a one-second move shows as a bright outline.
    private static Image Difference(Image a, Image b)
    {
        var diff = Image.CreateEmpty(a.GetWidth(), a.GetHeight(), false, Image.Format.Rgb8);
        for (int y = 0; y < a.GetHeight(); y++)
        {
            for (int x = 0; x < a.GetWidth(); x++)
            {
                var pa = a.GetPixel(x, y);
                var pb = b.GetPixel(x, y);
                diff.SetPixel(x, y, new Color(Math.Min(1f, 4f * Math.Abs(pa.R - pb.R)),
                    Math.Min(1f, 4f * Math.Abs(pa.G - pb.G)), Math.Min(1f, 4f * Math.Abs(pa.B - pb.B))));
            }
        }

        return diff;
    }

    private static Image Grid(IReadOnlyList<Image> tiles, int columns)
    {
        int rows = (tiles.Count + columns - 1) / columns;
        var grid = Image.CreateEmpty(TileWidth * columns, TileHeight * rows, false, Image.Format.Rgb8);
        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = (Image)tiles[i].Duplicate();
            tile.Convert(Image.Format.Rgb8);
            grid.BlitRect(tile, new Rect2I(0, 0, TileWidth, TileHeight), new Vector2I(i % columns * TileWidth, i / columns * TileHeight));
        }

        return grid;
    }
}
