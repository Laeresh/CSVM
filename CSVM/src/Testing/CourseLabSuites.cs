using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight.Modes;
using CSVM.Launch;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.Tooling;
using CSVM.UI.Labs;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The course lab in a <c>--course-edit</c> C1 freecam, driven through the calls its clicks, drag
/// key and sliders make, from rays through known screen points. It saves to scratch roots only. The lab's
/// panel over the edited course is photographed for the user; no pixel is asserted.</summary>
internal static class CourseLabSuites
{
    private const string Chapter = "C1";
    private const string Sample = "sample_out_and_back";
    private const string Edited = "course_lab_suite";
    private const int PaneWidth = 1280;
    private const int PaneHeight = 720;

    // A clicked point must lie on its camera ray, and stand on the ground there, within this.
    private const float OnM = 0.5f;

    [Suite("course-lab",
        "--course-edit=C1/<name> opens the course lab on that course in a freecam; from rays through screen points "
        + "a click places pylon gates and a ring on the ground facing the camera's way, ids p1, p2, r1 flown forward "
        + "in placing order, and the start point; a click on a gate selects it; the ring's height-above-ground and "
        + "diameter sliders and a held drag move what they should; a pylon gate placed over the sea stands on the sea "
        + "surface; a delete drops the gate's sequence entries; Save writes the scratch root alone and the file "
        + "reloads to the same course; the panel over the course is photographed")]
    internal static void CourseLabEdits(TestContext ctx)
    {
        var savedClock = GameClock.Current;
        var spec = SessionSpec.Parse(new[] { $"--course-edit={Chapter}/{Sample}", "--det", "--mute", "--no-pads" });
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
        try
        {
            bool built = session.StartSession();
            if (!built || session.FindChild("course_lab", true, false) is not CourseLab lab)
            {
                ctx.Check(false, $"a --course-edit={Chapter}/{Sample} freecam builds with a course lab (built={built})");
                return;
            }

            ctx.Check(lab.IsOpen && lab.Draft.Course is { Name: Sample, Gates.Count: 4, Sequence.Count: 5 } && lab.Visuals?.GateNodes.Count == 4,
                $"the lab opens at launch on the shipped {Sample} and draws its 4 gates: open {lab.IsOpen}, {lab.Draft.Course.Name}, {lab.Visuals?.GateNodes.Count} drawn");
            Edit(ctx, pane, camera, lab);
        }
        finally
        {
            session.Free();
            pane.Free();
            GameClock.Current = savedClock;
        }
    }

    private static void Edit(TestContext ctx, SubViewport pane, Camera3D camera, CourseLab lab)
    {
        string root = Path.Combine(ctx.ScratchDir, "course-lab");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        var library = new CourseLibrary(Path.Combine(root, "shipped"), Path.Combine(root, CourseLibrary.GameFolderName));
        lab.Library = library;
        lab.SaveTarget = CourseSource.Shipped;
        lab.New(Edited);
        var draft = lab.Draft;

        // West over the sample's first pylon gate, pitched down, every click left of the panel.
        camera.GlobalTransform = new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(90f)) * new Basis(Vector3.Right, Mathf.DegToRad(-35f)),
            new Vector3(-5150f, 330f, -3861f));
        float heading = CourseDraft.HeadingOf(camera.GlobalBasis);
        ctx.Check(Mathf.IsEqualApprox(heading, 90f), $"the camera looks west, heading {heading:0.00}");
        var clicks = new[] { new Vector2(250f, 470f), new Vector2(620f, 470f), new Vector2(430f, 360f), new Vector2(430f, 620f) };
        var kinds = new[] { CourseLab.PlaceKind.PylonGate, CourseLab.PlaceKind.PylonGate, CourseLab.PlaceKind.BalloonRing, CourseLab.PlaceKind.StartPoint };
        for (int i = 0; i < clicks.Length; i++)
        {
            lab.Place = kinds[i];
            ctx.Check(lab.ClickAt(clicks[i]), $"click {i} lands on the ground");
        }

        var gates = draft.Course.Gates;
        ctx.Check(gates.Select(g => g.Id).SequenceEqual(new[] { "p1", "p2", "r1" })
                && draft.Course.Sequence.SequenceEqual(gates.Select(g => new SequenceEntry(g.Id, GateDirection.Forward))),
            $"three gates placed as p1, p2, r1 and flown forward in that order: {string.Join(", ", draft.Course.Sequence)}");
        if (gates.Count != 3)
        {
            return;
        }

        for (int i = 0; i < 3; i++)
        {
            var at = gates[i].Kind == CourseGateKind.PylonGate ? gates[i].Position : gates[i].Position - (Vector3.Up * CourseDraft.RingAboveGroundM);
            float ground = lab.GroundBelow(at) ?? float.NaN;
            ctx.Check(OffRay(camera, clicks[i], at) < OnM && Mathf.Abs(at.Y - ground) < OnM && gates[i].Heading == heading,
                $"{gates[i].Id} stands on the ground under its click, facing the camera's way: {OffRay(camera, clicks[i], at):0.00} m off the ray, {at.Y - ground:0.00} m off the ground {ground:0.0}, heading {gates[i].Heading:0.0}");
        }

        var start = draft.Course.Start;
        ctx.Check(OffRay(camera, clicks[3], start.Position - (Vector3.Up * CourseDraft.StartAboveGroundM)) < OnM && start.HeadingDeg == heading,
            $"the start point stands {CourseDraft.StartAboveGroundM} m over its click, heading {start.HeadingDeg:0.0}: {start.Position}");

        // A click on a drawn gate selects it, whatever kind is armed, and places nothing.
        lab.Place = CourseLab.PlaceKind.PylonGate;
        lab.ClickAt(camera.UnprojectPosition(CourseMotion.Rest(gates[0]) * (CourseGateShape.PylonBase(gates[0], 1) + (Vector3.Up * (0.5f * gates[0].Height)))));
        ctx.Check(draft.SelectedId == "p1" && draft.Course.Gates.Count == 3, $"a click on p1's course pylon selects it: {draft.SelectedId}, {draft.Course.Gates.Count} gates");
        lab.ClickAt(camera.UnprojectPosition(gates[2].Position));
        ctx.Check(draft.SelectedId == "r1" && draft.Course.Gates.Count == 3, $"a click on r1 selects it: {draft.SelectedId}");

        // The ring's sliders, set as a drag of them would.
        float groundUnderRing = lab.GroundBelow(gates[2].Position) ?? float.NaN;
        Slider(lab, "ring_above").Value = 70.0;
        Slider(lab, "ring_diameter").Value = 50.0;
        var ring = draft.Selected!;
        ctx.Check(Mathf.IsEqualApprox(ring.Position.Y, groundUnderRing + 70f) && ring.Diameter == 50f && ring.Position with { Y = 0f } == gates[2].Position with { Y = 0f },
            $"the height-above-ground slider stands r1's centre 70 m over the ground ({ring.Position.Y - groundUnderRing:0.00} m) and the diameter slider sizes it ({ring.Diameter})");

        // A held drag moves p1 over the ground under the cursor; then the ring keeps its height above ground.
        draft.Select("p1");
        var dragTo = new Vector2(330f, 560f);
        ctx.Check(lab.DragTo(dragTo), $"a drag moves the selected gate");
        var p1 = draft.Selected!;
        ctx.Check(OffRay(camera, dragTo, p1.Position) < OnM && Mathf.Abs(p1.Position.Y - (lab.GroundBelow(p1.Position) ?? float.NaN)) < OnM,
            $"p1 follows the drag onto the ground under the cursor: {OffRay(camera, dragTo, p1.Position):0.00} m off the ray");
        draft.Select("r1");
        lab.DragTo(new Vector2(500f, 330f));
        ring = draft.Selected!;
        ctx.Check(Mathf.Abs(ring.Position.Y - (lab.GroundBelow(ring.Position) ?? float.NaN) - 70f) < OnM,
            $"a dragged ring keeps its 70 m over the ground it moved to ({ring.Position.Y - (lab.GroundBelow(ring.Position) ?? 0f):0.00} m)");

        OverSea(ctx, camera, lab);

        // p2 is flown once; deleting it drops that entry and the gate.
        draft.Select("p2");
        lab.DeleteSelected();
        ctx.Check(draft.Course.Gates.Select(g => g.Id).SequenceEqual(new[] { "p1", "r1" }) && draft.Course.Sequence.Select(e => e.GateId).SequenceEqual(new[] { "p1", "r1" })
                && draft.SelectedId == null && lab.Visuals?.GateNodes.Count == 2,
            $"deleting p2 drops it and its sequence entry, and the drawing follows: {string.Join(", ", draft.Course.Sequence)}");

        string? path = lab.Save();
        string expected = Path.Combine(root, "shipped", Chapter, Edited + ".json");
        string real = Path.Combine(ctx.RepoRoot, "CSVM", "data", "courses", Chapter, Edited + ".json");
        ctx.Check(path != null && Path.GetFullPath(path) == Path.GetFullPath(expected) && File.Exists(expected) && !File.Exists(real),
            $"Save writes the scratch shipped root and nothing in the source tree: {path}");
        ctx.Note($"saved {path}: {string.Join(", ", draft.Course.Gates.Select(g => $"{g.Id} ({g.Position.X:0}, {g.Position.Y:0.0}, {g.Position.Z:0})"))}, start {draft.Course.Start.Position}");
        var saved = draft.Course;
        var back = new CourseLibrary(library.Shipped, library.GameFolder).Load(Chapter, Edited, out string error);
        ctx.Check(back != null && back.ToJson() == saved.ToJson() && back.Hash == saved.Hash, $"the saved file reloads to the same course, hash {back?.Hash} for {saved.Hash} {error}");
        lab.New("scratch_other");
        ctx.Check(lab.Open(Edited) && draft.Course.ToJson() == saved.ToJson() && lab.Visuals?.GateNodes.Count == 2,
            $"the lab opens it again from its list: {draft.Course.Name}, {draft.Course.Gates.Count} gates");

        Photograph(ctx, pane, camera, lab);
    }

    // The ground ray stops on the sea-level sheet over open sea, so a pylon gate placed there stands on the surface.
    private static void OverSea(TestContext ctx, Camera3D camera, CourseLab lab)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Vector3? sea = null;
        for (float z = -16000f; z <= 16000f && sea == null; z += 1600f)
        {
            for (float x = -16000f; x <= 16000f && sea == null; x += 1600f)
            {
                var p = new Vector3(x, 0f, z);
                bool open = new[] { Vector3.Zero, Vector3.Right * 150f, Vector3.Back * 150f }
                    .All(d => lab.GroundBelow(p + d) is { } y && Mathf.Abs(y) < Effects.OceanMask.HideHeight);
                sea = open ? p with { Y = lab.GroundBelow(p)!.Value } : null;
            }
        }

        if (sea is not { } surface)
        {
            ctx.Check(false, $"a C1 point over open sea is found in {watch.ElapsedMilliseconds} ms");
            return;
        }

        var saved = camera.GlobalTransform;
        camera.GlobalTransform = new Transform3D(new Basis(Vector3.Right, Mathf.DegToRad(-60f)), surface + new Vector3(0f, 300f, 170f));
        lab.Place = CourseLab.PlaceKind.PylonGate;
        var screen = camera.UnprojectPosition(surface);
        lab.ClickAt(screen);
        var gate = lab.Draft.Selected;
        ctx.Check(gate is { Id: "p3" } && Mathf.Abs(gate.Position.Y) < Effects.OceanMask.HideHeight && OffRay(camera, screen, gate.Position) < OnM,
            $"a pylon gate placed over the sea at ({surface.X:0}, {surface.Z:0}) stands on the sea surface: y {gate?.Position.Y:0.00} (sea found in {watch.ElapsedMilliseconds} ms)");
        ctx.Note($"sea placement at ({surface.X:0}, {surface.Z:0}): {gate?.Id} at y {gate?.Position.Y:0.000}, sea found in {watch.ElapsedMilliseconds} ms");
        lab.DeleteSelected();
        camera.GlobalTransform = saved;
    }

    // The lab open with p1 selected, the course in view, and its panel laid out and drawn this frame.
    private static void Photograph(TestContext ctx, SubViewport pane, Camera3D camera, CourseLab lab)
    {
        lab.Draft.Select("p1");
        lab.Save(); // again, so the status line shows the path written
        var p1 = lab.Draft.Selected!;
        camera.LookAtFromPosition(p1.Position + new Vector3(260f, 140f, 120f), p1.Position + new Vector3(-150f, 20f, 0f), Vector3.Up);
        Settle(pane);
        foreach (var ui in lab.FindChildren("*", nameof(Control), true, false).Cast<Control>().Where(c => c.GetParent() is CanvasLayer))
        {
            // A wrapped label measures its height at the width the pass before gave it. Its cached minimum is
            // dropped on a deferred call, so each pass drops every cache first.
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (var control in ui.FindChildren("*", nameof(Control), true, false).Cast<Control>())
                {
                    control.UpdateMinimumSize();
                }

                ui.OffsetTop += 1f;
                ui.OffsetTop -= 1f;
                PauseBoardSuites.Settle(ui);
            }

            Draw(ui);
        }

        pane.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        RenderingServer.ForceDraw();
        RenderingServer.ForceDraw();
        var image = pane.GetTexture()?.GetImage();
        pane.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        if (image == null || image.IsEmpty())
        {
            ctx.Note($"lab photograph: no image, the process draws nothing");
            return;
        }

        string dir = Path.Combine(ctx.ScratchDir, "course-lab");
        Directory.CreateDirectory(dir);
        string png = Path.Combine(dir, "lab.png");
        image.SavePng(png);
        ctx.Note($"lab photograph {png}");
    }

    // A control draws on a deferred call a suite's one frame never reaches. Its draw notification is sent
    // here instead, onto a cleared canvas item, parents first.
    private static void Draw(Control control)
    {
        RenderingServer.CanvasItemClear(control.GetCanvasItem());
        control.Notification((int)CanvasItem.NotificationDraw);
        foreach (var child in control.GetChildren().OfType<Control>())
        {
            Draw(child);
        }
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

    private static HSlider Slider(CourseLab lab, string name) => (HSlider)lab.FindChild(name, true, false);

    // How far a point lies from the camera ray through a screen point.
    private static float OffRay(Camera3D camera, Vector2 screen, Vector3 point)
    {
        var from = camera.ProjectRayOrigin(screen);
        var dir = camera.ProjectRayNormal(screen);
        var along = from + (dir * (point - from).Dot(dir));
        return along.DistanceTo(point);
    }
}
