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
using Handle = CSVM.UI.Labs.CourseGizmo.Handle;

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

    // West over the sample's first pylon gate, pitched down; the placing clicks are all on ground in view.
    private static readonly Transform3D PlacingPose = new(
        new Basis(Vector3.Up, Mathf.DegToRad(90f)) * new Basis(Vector3.Right, Mathf.DegToRad(-35f)), new Vector3(-5150f, 330f, -3861f));

    [Suite("course-lab",
        "--course-edit=C1/<name> opens the course lab on that course in a freecam; from rays through screen points "
        + "a click places pylon gates and a ring on the ground facing the camera's way, ids p1, p2, r1 flown forward "
        + "in placing order, and the start point; a click on a gate selects it; the ring's height-above-ground and "
        + "diameter sliders and a held drag move what they should; a pylon gate placed over the sea stands on the sea "
        + "surface; a delete drops the gate's sequence entries; Save writes the scratch root alone and the file "
        + "reloads to the same course; the panel over the course is photographed. The sequence panel: a row click "
        + "selects its entry and flies the camera onto it, a placed gate's entry goes after the selected one, Up, "
        + "Flip, Remove and Add again (reverse) edit the order, the world numbers and line follow, and the sequence "
        + "saves and reloads; the panels over the numbered course are photographed. The gizmo: a press on a handle "
        + "grabs it without placing or reselecting, the X and Y arrows move a ring along their axes and the heading ring "
        + "turns it (Ctrl snaps), with no sequence row rebuilt; a pylon gate has no Y arrow and stays on the ground; "
        + "the selected ring with its gizmo is photographed")]
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

        camera.GlobalTransform = PlacingPose;
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
        Gizmo(ctx, pane, camera, lab);
        Sequence(ctx, pane, camera, lab, library);
    }

    // The gizmo pressed and dragged from screen points on its handles: r1's X and Y arrows and heading ring, then
    // p1's X arrow. The gates are put back after, so the sequence steps see the saved course.
    private static void Gizmo(TestContext ctx, SubViewport pane, Camera3D camera, CourseLab lab)
    {
        var draft = lab.Draft;
        var gizmo = lab.Gizmo!;
        var savedP1 = draft.Course.Gates.First(g => g.Id == "p1");
        var savedR1 = draft.Course.Gates.First(g => g.Id == "r1");
        var sequence = draft.Course.Sequence;
        var rows = SequenceRows(lab);
        int pool = draft.Course.Gates.Count;
        draft.SelectEntry(1);
        camera.LookAtFromPosition(savedR1.Position + new Vector3(220f, 180f, 260f), savedR1.Position, Vector3.Up);
        Vector2 At(Handle handle, Vector3 offset = default)
        {
            gizmo.Fit(camera);
            return camera.UnprojectPosition(gizmo.Grip(handle) + offset);
        }

        lab.MoveTo(At(Handle.X));
        ctx.Check(gizmo.Visible && gizmo.Hovered == Handle.X, $"the gizmo stands on the selected r1 and lights the X arrow under the cursor: {gizmo.Hovered}, arrows {gizmo.Size:0.0} m");

        // A press on a handle with a pylon gate armed grabs it, placing nothing and keeping the selection.
        lab.Place = CourseLab.PlaceKind.PylonGate;
        ctx.Check(lab.PressAt(At(Handle.X)) && gizmo.Grabbed == Handle.X && draft.Course.Gates.Count == pool && draft is { SelectedId: "r1", SelectedEntry: 1 },
            $"a press on the X arrow grabs it and neither places nor reselects: {gizmo.Grabbed}, {draft.Course.Gates.Count} gates, {draft.SelectedId} entry {draft.SelectedEntry}");
        lab.MoveTo(At(Handle.X, Vector3.Right * 30f));
        ctx.Check(lab.ReleaseAt() && (draft.Selected!.Position - savedR1.Position).DistanceTo(Vector3.Right * 30f) < 0.1f,
            $"dragging the X arrow 30 m along it moves r1 by {draft.Selected!.Position - savedR1.Position}");

        var moved = draft.Selected!.Position;
        lab.PressAt(At(Handle.Y));
        lab.MoveTo(At(Handle.Y, Vector3.Up * 20f));
        lab.ReleaseAt();
        var ring = draft.Selected!;
        float above = ring.Position.Y - (lab.GroundBelow(ring.Position) ?? float.NaN);
        double slider = Slider(lab, "ring_above").Value;
        ctx.Check((ring.Position - moved).DistanceTo(Vector3.Up * 20f) < 0.1f && Math.Abs(slider - above) <= 0.51,
            $"dragging the Y arrow 20 m up raises r1 by {ring.Position - moved}, and its height slider follows to {slider} for {above:0.00} m");

        // The ring is gripped 45 degrees off heading 0 the near way, and swept 40 degrees, then 47 with Ctrl.
        lab.PressAt(At(Handle.Yaw));
        float radius = (gizmo.Grip(Handle.Yaw) - gizmo.Position).Length();
        Vector3 Round(float heading) => new Vector3(-Mathf.Sin(Mathf.DegToRad(heading)), 0f, -Mathf.Cos(Mathf.DegToRad(heading))) * radius;
        lab.MoveTo(camera.UnprojectPosition(gizmo.Position + Round(-5f)));
        lab.ReleaseAt();
        float turned = draft.Selected!.Heading;
        lab.PressAt(At(Handle.Yaw));
        lab.MoveTo(camera.UnprojectPosition(gizmo.Position + Round(2f)), snap: true);
        lab.ReleaseAt();
        ctx.Check(Mathf.Abs(turned - (savedR1.Heading + 40f)) < 0.1f && draft.Selected!.Heading == -180f,
            $"sweeping the heading ring 40 degrees turns r1 from {savedR1.Heading} to {turned:0.00}, and 47 more with Ctrl snaps 177 to {draft.Selected!.Heading}");
        ctx.Check(ReferenceEquals(sequence, draft.Course.Sequence) && rows.SequenceEqual(SequenceRows(lab)) && draft.Course.Gates.Count == pool,
            $"the gizmo's drags keep the sequence list, so no panel row was rebuilt ({rows.Length} rows)");

        // A pylon gate has no Y arrow, and its X arrow keeps it standing on the ground.
        draft.Select("p1");
        camera.LookAtFromPosition(savedP1.Position + new Vector3(220f, 180f, 260f), savedP1.Position, Vector3.Up);
        lab.PressAt(At(Handle.X));
        lab.MoveTo(At(Handle.X, Vector3.Right * 30f));
        lab.ReleaseAt();
        var p1 = draft.Selected!;
        float ground = lab.GroundBelow(p1.Position) ?? float.NaN;
        ctx.Check(!((Node3D)gizmo.FindChild(nameof(Handle.Y), false, false)).Visible && Mathf.Abs(p1.Position.X - savedP1.Position.X - 30f) < 0.1f
                && p1.Position.Z == savedP1.Position.Z && Mathf.Abs(p1.Position.Y - ground) < OnM,
            $"p1 shows no Y arrow, and its X arrow moves it {p1.Position.X - savedP1.Position.X:0.00} m along X onto the ground ({p1.Position.Y - ground:0.00} m off)");

        float snapped = draft.Course.Gates.First(g => g.Id == "r1").Heading;
        ctx.Note($"gizmo: r1 moved {moved - savedR1.Position} by X and {ring.Position - moved} by Y, heading {savedR1.Heading} to {turned:0.000} to {snapped} snapped; p1 moved {p1.Position - savedP1.Position}");
        draft.Update(savedP1);
        draft.Update(savedR1);
        // Aimed to the ring's right, so it sits left of the two panels; the hovered X arrow is lit.
        draft.Select("r1");
        camera.LookAtFromPosition(savedR1.Position + new Vector3(220f, 180f, 260f), savedR1.Position + new Vector3(110f, 0f, -95f), Vector3.Up);
        lab.MoveTo(At(Handle.X));
        Render(ctx, pane, lab, "gizmo.png");
        lab.MoveTo(Vector2.Zero);
    }

    // The sequence panel through its own list and buttons, from the saved p1, r1 course flown in that order.
    private static void Sequence(TestContext ctx, SubViewport pane, Camera3D camera, CourseLab lab, CourseLibrary library)
    {
        var draft = lab.Draft;
        ctx.Check(Rows(lab) is [_, var second] && second.Contains("r1", StringComparison.Ordinal),
            $"the sequence panel lists the two entries: {string.Join(" | ", Rows(lab))}");

        // A row click selects its entry and gate and flies the camera onto the entry's approach to look at it.
        camera.GlobalTransform = PlacingPose;
        Press(lab, "row_1");
        lab._Process(1.0);
        var centre = CourseDraft.Centre(draft.Selected!);
        var onScreen = camera.UnprojectPosition(centre);
        ctx.Check(draft is { SelectedEntry: 1, SelectedId: "r1" } && !camera.IsPositionBehind(centre)
                && onScreen.DistanceTo(new Vector2(PaneWidth, PaneHeight) / 2f) < 2f && camera.GlobalPosition.DistanceTo(centre) < 400f,
            $"a click on row 2 selects r1 and the camera flies to look at it: centre at {onScreen} on screen, {camera.GlobalPosition.DistanceTo(centre):0} m away");

        // A gate placed with entry 1 selected goes second, not last.
        camera.GlobalTransform = PlacingPose;
        draft.SelectEntry(0);
        lab.Place = CourseLab.PlaceKind.PylonGate;
        lab.ClickAt(new Vector2(150f, 600f));
        ctx.Check(Ids(draft) == "p1 p2 r1" && draft is { SelectedEntry: 1, SelectedId: "p2" },
            $"a pylon gate placed with entry 1 selected is flown second and selected: {Ids(draft)}, entry {draft.SelectedEntry}");

        // Up moves r1 before p2; neither end moves past itself.
        draft.SelectEntry(2);
        Press(lab, "entry_up");
        ctx.Check(Ids(draft) == "p1 r1 p2" && draft.SelectedEntry == 1 && !draft.MoveEntry(0, -1) && !draft.MoveEntry(2, 1),
            $"Up moves r1 to second, and the ends stay put: {Ids(draft)}");

        // With p2's entry selected, a click on r1 in the world keeps that entry as the insertion point. Adding r1
        // and then p1 again reversed after it turns the course back.
        draft.SelectEntry(2);
        lab.ClickAt(camera.UnprojectPosition(draft.Course.Gates.First(g => g.Id == "r1").Position));
        ctx.Check(draft is { SelectedId: "r1", SelectedEntry: 2 }, $"a world click on r1 selects it and keeps entry 3 selected: {draft.SelectedId}, entry {draft.SelectedEntry}");
        Press(lab, "add_reverse");
        draft.Select("p1");
        Press(lab, "add_reverse");
        ctx.Check(Ids(draft) == "p1 r1 p2 r1~ p1~", $"r1 and p1 added again reversed fly the course back: {Ids(draft)}");

        draft.SelectEntry(2);
        Press(lab, "entry_flip");
        draft.SelectEntry(0);
        Press(lab, "add_forward");
        int pool = draft.Course.Gates.Count;
        Press(lab, "entry_remove");
        ctx.Check(Ids(draft) == "p1 r1 p2~ r1~ p1~" && draft.Course.Gates.Count == pool && draft is { SelectedEntry: 1, SelectedId: "r1" },
            $"Flip turns p2, and Remove takes an added entry but leaves its gate: {Ids(draft)}, {draft.Course.Gates.Count} gates");
        CheckDrawn(ctx, lab, "p1:1, 5 r1:2, 4 p2:3", 5, 4);

        string? path = lab.Save();
        var saved = draft.Course;
        var back = new CourseLibrary(library.Shipped, library.GameFolder).Load(Chapter, Edited, out string error);
        ctx.Check(path != null && back != null && back.Sequence.SequenceEqual(saved.Sequence) && back.Hash == saved.Hash,
            $"the reordered sequence saves and reloads: {string.Join(", ", back?.Sequence ?? Array.Empty<SequenceEntry>())} {error}");
        lab.New("scratch_other");
        lab.Open(Edited);
        CheckDrawn(ctx, lab, "p1:1, 5 r1:2, 4 p2:3", 5, 4);

        // From east of the course, looking west and aimed to its right, so it sits left of the two panels.
        // The entry numbers are not in it: a Label3D shapes its text on a deferred call a suite never reaches.
        var mid = draft.Course.Gates.Aggregate(Vector3.Zero, (sum, g) => sum + CourseDraft.Centre(g)) / draft.Course.Gates.Count;
        camera.LookAtFromPosition(mid + new Vector3(400f, 260f, 0f), mid + new Vector3(0f, 0f, -200f), Vector3.Up);
        draft.SelectEntry(2);
        Render(ctx, pane, lab, "sequence.png");

        // Deleting r1 takes both its entries, and the panel and world follow.
        draft.Select("r1");
        lab.DeleteSelected();
        ctx.Check(Ids(draft) == "p1 p2~ p1~", $"deleting r1 drops both its entries: {Ids(draft)}");
        CheckDrawn(ctx, lab, "p1:1, 3 p2:2", 3, 2);
    }

    // The panel's rows, and the world's labels ("id:numbers" for the shown ones) and line, against the draft.
    // Each entry draws an arrow through its gate and each leg between gates a line, both with a two-barb head.
    private static void CheckDrawn(TestContext ctx, CourseLab lab, string labels, int entries, int legs)
    {
        var overlay = lab.Overlay!;
        string shown = string.Join(" ", overlay.Labels.Select((l, i) => (l, i)).Where(p => p.l.Visible)
            .Select(p => $"{lab.Draft.Course.Gates[p.i].Id}:{p.l.Text}"));
        var mesh = (ImmediateMesh)overlay.Path.Mesh;
        int vertices = mesh.GetSurfaceCount() == 1 ? mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length : 0;
        var texts = Rows(lab);
        bool rows = texts.Length == entries && Enumerable.Range(0, entries).All(i =>
            texts[i].StartsWith($"{i + 1}.  {lab.Draft.Course.Sequence[i].GateId}  ", StringComparison.Ordinal));
        ctx.Check(rows && shown == labels && vertices == 6 * (entries + legs),
            $"the panel lists {entries} rows ({string.Join(" | ", texts)}), the gates carry {labels} ({shown}), the line has {6 * (entries + legs)} vertices ({vertices})");
    }

    private static void Press(CourseLab lab, string button) =>
        ((Button)lab.FindChild(button, true, false)).EmitSignal(BaseButton.SignalName.Pressed);

    // The sequence as ids, a reversed entry marked with a tilde.
    private static string Ids(CourseDraft draft) =>
        string.Join(" ", draft.Course.Sequence.Select(e => e.Direction == GateDirection.Forward ? e.GateId : e.GateId + "~"));

    private static Node[] SequenceRows(CourseLab lab) =>
        lab.FindChild("sequence_rows", true, false).GetChildren().ToArray();

    private static string[] Rows(CourseLab lab) =>
        lab.FindChild("sequence_rows", true, false).GetChildren().Cast<Button>().Select(b => b.Text).ToArray();

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
        camera.LookAtFromPosition(p1.Position + new Vector3(260f, 140f, 120f), p1.Position + new Vector3(-150f, 20f, -150f), Vector3.Up);
        Render(ctx, pane, lab, "lab.png");
    }

    // The pane as it stands, the lab's panels laid out and drawn this frame, to a PNG in the suite's scratch.
    private static void Render(TestContext ctx, SubViewport pane, CourseLab lab, string name)
    {
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
        string png = Path.Combine(dir, name);
        image.SavePng(png);
        ctx.Note($"lab photograph {png}");
    }

    // A control draws on a deferred call a suite's one frame never reaches. Its draw notification is sent
    // here instead, onto a cleared canvas item, parents first. A scroll container's panel is drawn by a call that
    // refuses a notification sent outside the frame's own draw, so only its rows are.
    private static void Draw(Control control)
    {
        RenderingServer.CanvasItemClear(control.GetCanvasItem());
        RenderingServer.CanvasItemSetTransform(control.GetCanvasItem(), control.GetTransform());
        if (control is not ScrollContainer)
        {
            control.Notification((int)CanvasItem.NotificationDraw);
        }

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
