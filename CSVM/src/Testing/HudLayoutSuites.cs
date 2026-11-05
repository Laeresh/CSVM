using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Bindings;
using CSVM.Flight.Airframe;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The HUD layout against real <see cref="FlightHud"/> panes on live viewports, with no game
/// data. Each pane is a SubViewport in the tree, sized against a 1440p window to stand for one pane
/// shape. Bare dial and tape controls stand in for the textured ones; only placement is read, and
/// every node is freed before the frame draws. The options file is a scratch store. Each suite puts
/// the process-wide layout back in a finally.</summary>
internal static class HudLayoutSuites
{
    private const string EditorDescription =
        "the HUD layout editor on a live pane: Edit HUD Layout's press opens it over the pressing seat's "
        + "pane, raising the Editing halt, silencing every seat and showing the dials, tape and plate "
        + "even in the None mode with the plate's box off, and a held key opens nothing more; a grab on "
        + "the right column and a drag move it and every pane previews the draft; an arrow nudge moves "
        + "the selected element and Tab selects the next; S loads Sides; Enter's save makes Custom the "
        + "running layout, writes hudLayout and hudLayoutCustom, posts 'HUD Layout: Custom' in every "
        + "pane, drops the halt and gives every seat its keys back; a second edit cancelled leaves the "
        + "saved layout; a --det save writes no file; a halted, network or headset session opens no "
        + "editor; and on a 21:9 pane, a 32:9 pane and a 2P side-by-side pane of a 32:9 window the right "
        + "column drags out to the pane's real edge, past the reading box where the pane is wider than it";

    // The window every suite here measures against, set on the root for the suite's length. A
    // headless root is otherwise a size nobody flies at.
    private static readonly Vector2I ReferenceWindow = new(2560, 1440);

    // A 32:9 window, which splits 2P side by side (SplitScreen.SideBySide).
    private static readonly Vector2I SuperUltrawideWindow = new(5120, 1440);

    [Suite("hud-layout-placement",
        "the HUD layout on live panes of seven shapes cut from a 16:9 and a 32:9 window (single 16:9, "
        + "ultrawide 21:9, 2P stacked, 4P grid; single 32:9, 2P side by side and 4P grid on 32:9): a pane given Original hands no layout to "
        + "its dials, tape and plate and the tape stands where a pane never given a layout puts it; "
        + "Sides hands its layout to all three and puts every element whole inside the pane and apart from "
        + "the rest, the columns at the reading box's sides and centred on its height, nothing over the "
        + "bottom centre, the tape at the top right; a custom placement anchored to the pane's right "
        + "edge keeps its margin there on every shape, past the reading box on a wider pane; and the cockpit view and the text block are untouched")]
    internal static void HudLayoutPlacementSuite(TestContext ctx)
    {
        var root = ctx.Host.GetTree().Root;
        var before = root.Size;
        try
        {
            // A 16:9 window holds the full-screen shapes and its splits; a 32:9 one holds its own splits.
            foreach (var window in new[] { ReferenceWindow, SuperUltrawideWindow })
            {
                root.Size = window;
                ctx.Check(root.Size == window, $"the window measures {root.Size}, the {window} every shape here is cut from");
                Shapes(ctx, root.Size);
            }
        }
        finally
        {
            root.Size = before;
        }
    }

    [Suite("hud-layout-editor",
        EditorDescription)]
    internal static void HudLayoutEditorSuite(TestContext ctx)
    {
        var root = ctx.Host.GetTree().Root;
        var before = root.Size;
        root.Size = ReferenceWindow;
        try
        {
            Editor(ctx);
        }
        finally
        {
            root.Size = before;
        }
    }

    private static void Shapes(TestContext ctx, Vector2I window)
    {
        float h = window.Y;
        float w = window.X;
        bool wide = window == SuperUltrawideWindow;
        var shapes = wide
            ? new (string Name, Vector2 Size)[]
            {
                ("single 32:9", new Vector2(w, h)),
                ("2P side by side on 32:9", new Vector2(Mathf.Floor((w - 2f) / 2f), h)),
                ("4P grid on 32:9", new Vector2(Mathf.Floor((w - 2f) / 2f), Mathf.Floor((h - 2f) / 2f))),
            }
            : new (string Name, Vector2 Size)[]
            {
                ("single 16:9", new Vector2(w, h)),
                ("ultrawide 21:9", new Vector2(Mathf.Round(h * 21f / 9f), h)),
                ("2P stacked", new Vector2(w, Mathf.Floor((h - 2f) / 2f))),
                ("4P grid", new Vector2(Mathf.Floor((w - 2f) / 2f), Mathf.Floor((h - 2f) / 2f))),
            };
        var custom = HudLayoutPlacement.Sides.With(HudElement.RightDials,
            new HudPlacement(HudAnchor.End, HudAnchor.Start, -40f, 200f)).InPane();
        foreach (var (name, size) in shapes)
        {
            var view = new SubViewport { Size = new Vector2I((int)size.X, (int)size.Y), Disable3D = true };
            ctx.Host.AddChild(view);
            try
            {
                var hud = Pane(view);
                var untouched = Pane(view);
                Shape(ctx, name, hud, untouched, custom);
            }
            finally
            {
                view.Free();
            }
        }
    }

    private static void Editor(TestContext ctx)
    {
        var (kind, saved, mode) = (HudLayoutSetting.Current, HudLayoutSetting.Custom, HudModeSetting.Current);
        string? previousDir = OptionsStore.DirectoryOverride;
        string dir = Path.GetFullPath(Path.Combine(ctx.ScratchDir, "hud-layout-editor"));
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }

        Directory.CreateDirectory(dir);
        OptionsStore.DirectoryOverride = dir;
        var made = new List<GodotObject>();
        try
        {
            HudLayoutSetting.Current = HudLayoutKind.Original;
            HudLayoutSetting.Custom = null;
            HudModeSetting.Current = HudMode.None;
            var window = ctx.Host.GetTree().Root.Size;
            var views = new[]
            {
                View(ctx, new Vector2I(window.X, (window.Y - 2) / 2), made),
                View(ctx, new Vector2I(window.X, (window.Y - 2) / 2), made),
            };
            var seats = new[] { Seat(0, views[0], made), Seat(1, views[1], made) };
            var pause = new PauseState();
            var store = OptionsStore.UserOptions();
            HudLayoutEditor? editor = null;
            foreach (var seat in seats)
            {
                seat.EditHudLayout = p => editor = HudLayoutEditor.Open(p, seats, pause, store);
            }

            editor = OpenByKey(ctx, seats, pause, () => editor);
            if (editor == null)
            {
                return;
            }

            Edit(ctx, editor, seats);
            Save(ctx, editor, seats, pause, store);
            Cancel(ctx, seats, pause, store);
            Deterministic(ctx, seats, pause);
            Refusals(ctx, seats);
            WideDrags(ctx, made);
        }
        finally
        {
            (HudLayoutSetting.Current, HudLayoutSetting.Custom, HudModeSetting.Current) = (kind, saved, mode);
            ctx.Host.GetTree().Root.Size = ReferenceWindow;
            OptionsStore.DirectoryOverride = previousDir;
            foreach (var node in made)
            {
                if (GodotObject.IsInstanceValid(node))
                {
                    node.Free();
                }
            }
        }
    }

    // One shape's checks over two panes on the same viewport: one walked through the layouts and one
    // never given a layout at all.
    private static void Shape(TestContext ctx, string name, FlightHud hud, FlightHud untouched, HudLayout custom)
    {
        var tape = hud.Compass!;
        var (box, pane, scale) = Frame(tape);
        StepTape(untouched.Compass!);
        var plain = untouched.Compass!.Position;

        hud.SetHudLayout(null);
        StepTape(tape);
        ctx.Check(hud.Gauges!.Layout == null && tape.Layout == null && hud.ThrottlePlate!.Layout == null
            && tape.Position == plain,
            $"{name} {pane}: Original hands no layout on and the tape stands where it always did ({tape.Position} vs {plain})");
        ctx.Check(HudLayout.Elements.All(e => HudLayoutPlacement.Shift(e, null, box, pane, scale) == Vector2.Zero),
            $"…and moves no element by any amount");

        hud.SetHudLayout(HudLayoutPlacement.Sides);
        StepTape(tape);
        var rects = HudLayout.Elements.ToDictionary(e => e, e => HudLayoutPlacement.Place(e, HudLayoutPlacement.Sides, box, pane, scale));
        ctx.Check(hud.Gauges!.Layout == HudLayoutPlacement.Sides && hud.ThrottlePlate!.Layout == HudLayoutPlacement.Sides
            && tape.Position.IsEqualApprox(rects[HudElement.CompassTape].Position),
            $"{name}: Sides reaches all three, the live tape at {tape.Position} where the layout puts it ({rects[HudElement.CompassTape].Position})");
        var whole = new Rect2(Vector2.Zero, pane);
        ctx.Check(rects.Values.All(whole.Encloses),
            $"…every element whole inside the pane ({Describe(rects)})");
        ctx.Check(rects.All(a => rects.All(b => a.Key == b.Key || !a.Value.Intersects(b.Value))),
            $"…and apart from the rest");
        float margin = HudLayoutPlacement.SidesMargin * scale;
        var left = rects[HudElement.LeftDials];
        var right = rects[HudElement.RightDials];
        ctx.Check(Mathf.IsEqualApprox(left.Position.X, box.Position.X + margin) && Mathf.IsEqualApprox(right.End.X, box.End.X - margin)
            && Mathf.Abs(left.GetCenter().Y - box.GetCenter().Y) < 0.5f && Mathf.Abs(right.GetCenter().Y - box.GetCenter().Y) < 0.5f,
            $"…the columns at the reading box's sides ({box}) and centred on its height ({left}, {right})");
        var bottomCentre = new Rect2(box.Position.X + (box.Size.X / 3f), box.GetCenter().Y, box.Size.X / 3f, box.Size.Y / 2f);
        var compass = rects[HudElement.CompassTape];
        ctx.Check(!rects.Values.Any(r => r.Intersects(bottomCentre)) && compass.GetCenter().X > box.GetCenter().X
            && compass.End.Y <= right.Position.Y,
            $"…nothing over the bottom centre, and the tape at the top right above the right column ({compass})");

        hud.SetHudLayout(custom);
        var placed = HudLayoutPlacement.Place(HudElement.RightDials, custom, box, pane, scale);
        ctx.Check(hud.Gauges!.Layout == custom && whole.Encloses(placed)
            && Mathf.IsEqualApprox(placed.End.X, pane.X - (40f * scale)),
            $"{name}: a custom right column keeps its 40 px margin from the pane's right edge ({placed}, pane {pane})");
        ctx.Check(pane.X <= box.Size.X + 1f || placed.End.X > box.End.X,
            $"…past the reading box Sides keeps to on a pane wider than it ({placed}, box {box})");

        hud.SetCockpitView(true);
        untouched.SetCockpitView(true);
        ctx.Check(!hud.Gauges!.Visible && !hud.Compass!.Visible && !hud.ThrottlePlate!.Visible
            && hud.Reticle!.Visible == untouched.Reticle!.Visible && hud.TextBlock?.Visible == untouched.TextBlock?.Visible,
            $"…and the cockpit view under a layout shows what it showed under none");
    }

    // A pane with every part, attached on a canvas inside the viewport. Processing is off, so no bare
    // control steps itself; the suite steps the tape alone, which reads no texture to place itself.
    private static FlightHud Pane(SubViewport view)
    {
        var canvas = new CanvasLayer();
        view.AddChild(canvas);
        var hud = new FlightHud
        {
            Compass = new CompassTape { ProcessMode = Node.ProcessModeEnum.Disabled },
            Gauges = new GaugeCluster { ProcessMode = Node.ProcessModeEnum.Disabled },
            ThrottlePlate = ThrottlePlate.Build(),
            Reticle = new ImpactReticle { ProcessMode = Node.ProcessModeEnum.Disabled },
            TargetHud = new TargetHud { ProcessMode = Node.ProcessModeEnum.Disabled },
        };
        hud.Attach(canvas, canvas, null, null);
        if (hud.ThrottlePlate != null)
        {
            hud.ThrottlePlate.ProcessMode = Node.ProcessModeEnum.Disabled;
        }

        hud.SetThrottlePlateShown(true);
        return hud;
    }

    private static void StepTape(CompassTape tape) => tape._Process(0.0);

    private static (Rect2 Box, Vector2 Pane, float Scale) Frame(Control control) =>
        (HudMetrics.ReadingBox(control), control.GetViewportRect().Size, HudMetrics.Scale(control));

    private static string Describe(Dictionary<HudElement, Rect2> rects) =>
        string.Join(", ", rects.Select(r => $"{r.Key} {r.Value}"));

    private static SubViewport View(TestContext ctx, Vector2I size, List<GodotObject> made)
    {
        var view = new SubViewport { Size = size, Disable3D = true };
        ctx.Host.AddChild(view);
        made.Add(view);
        return view;
    }

    // A seat the way a splitscreen roster builds one, its HUD on a canvas inside its own pane.
    private static FlightController Seat(int player, SubViewport view, List<GodotObject> made)
    {
        var rig = new FlightController
        {
            PlayerIndex = player,
            UseKeyboard = player == 0,
            PadDevices = Array.Empty<int>(),
        };
        made.Add(rig);
        var canvas = new CanvasLayer();
        view.AddChild(canvas);
        var hud = rig.PilotHud;
        hud.Compass = new CompassTape { ProcessMode = Node.ProcessModeEnum.Disabled };
        hud.Gauges = new GaugeCluster { ProcessMode = Node.ProcessModeEnum.Disabled };
        hud.ThrottlePlate = ThrottlePlate.Build();
        if (hud.ThrottlePlate != null)
        {
            hud.ThrottlePlate.ProcessMode = Node.ProcessModeEnum.Disabled;
        }

        hud.Attach(canvas, canvas, null, null);
        return rig;
    }

    private static HudLayoutEditor? OpenByKey(TestContext ctx, FlightController[] seats, PauseState pause,
        Func<HudLayoutEditor?> editor)
    {
        foreach (var seat in seats)
        {
            seat.PollHudModeCycleForTest();
        }

        var hud = seats[1].PilotHud;
        ctx.Check(!hud.Gauges!.Visible && !hud.Compass!.Visible, $"in the None mode the second pane draws no dials or tape before the edit");
        seats[1].HoldActionForTest(InputAction.EditHudLayout, true);
        seats[1].PollHudModeCycleForTest();
        var open = editor();
        ctx.Check(open != null && open.IsOpen && open.GetParent() != null && hud.LayoutEditing,
            $"a press of Edit HUD Layout on the second seat opens the editor over its pane");
        if (open == null)
        {
            return null;
        }

        ctx.Check(pause.Reasons == HaltReason.Editing && pause.ClockHeld && seats.All(s => s.Pause.Silenced),
            $"…raising the Editing halt ({pause.Reasons}) and silencing every seat");
        ctx.Check(hud.Gauges!.Visible && hud.Compass!.Visible && hud.ThrottlePlate is not { Visible: false }
            && !seats[0].PilotHud.Gauges!.Visible,
            $"…showing the dials, tape and plate over the edited pane in None with the plate's box off, the other pane as it was");
        seats[1].HoldActionForTest(InputAction.EditHudLayout, true);
        seats[1].PollHudModeCycleForTest();
        ctx.Check(ReferenceEquals(editor(), open), $"…and the key still held opens nothing more");
        seats[1].HoldActionForTest(InputAction.EditHudLayout, false);
        seats[1].PollHudModeCycleForTest();
        return open;
    }

    private static void Edit(TestContext ctx, HudLayoutEditor editor, FlightController[] seats)
    {
        var (box, pane, scale) = Frame(editor);
        var start = editor.Draft;
        var right = HudLayoutPlacement.Place(HudElement.RightDials, start, box, pane, scale);
        bool grabbed = editor.Grab(right.GetCenter());
        var to = new Vector2(pane.X - (right.Size.X / 2f) - (10f * scale), pane.Y / 2f);
        editor.DragTo(to);
        editor.Drop();
        var moved = HudLayoutPlacement.Place(HudElement.RightDials, editor.Draft, box, pane, scale);
        ctx.Check(grabbed && editor.Selected == HudElement.RightDials && moved.GetCenter().DistanceTo(to) <= scale + 0.01f,
            $"a grab on the right column ({right}) and a drag to {to} move it there ({moved})");
        ctx.Check(editor.Draft[HudElement.RightDials].X == HudAnchor.End && editor.Draft[HudElement.LeftDials] == start[HudElement.LeftDials],
            $"…anchored to the right edge, the other elements where they were ({editor.Draft.Format()})");
        foreach (var seat in seats)
        {
            seat.PollHudModeCycleForTest();
        }

        ctx.Check(seats.All(s => s.PilotHud.Layout == editor.Draft),
            $"…and every pane previews the draft on its next frame");
        editor.Nudge(new Vector2(0f, -HudLayoutEditor.NudgeStep));
        var nudged = HudLayoutPlacement.Place(HudElement.RightDials, editor.Draft, box, pane, scale);
        ctx.Check(Mathf.Abs(moved.Position.Y - nudged.Position.Y - (HudLayoutEditor.NudgeStep * scale)) <= scale,
            $"an arrow nudge moves the selected column {HudLayoutEditor.NudgeStep} reference px up ({moved.Position.Y} to {nudged.Position.Y})");
        editor.SelectNext();
        ctx.Check(editor.Selected == HudElement.CompassTape, $"Tab selects the next element ({editor.Selected})");
        editor.Load(HudLayoutPlacement.Sides);
        ctx.Check(editor.Draft.PaneRelative && HudLayout.Elements.All(e =>
                HudLayoutPlacement.Place(e, editor.Draft, box, pane, scale).Position.DistanceTo(
                    HudLayoutPlacement.Place(e, HudLayoutPlacement.Sides, box, pane, scale).Position) <= scale),
            $"S loads the Sides preset whole, re-measured from the pane's edges ({editor.Draft.Format()})");
        editor.Grab(HudLayoutPlacement.Place(HudElement.CompassTape, editor.Draft, box, pane, scale).GetCenter());
        editor.DragTo(new Vector2(pane.X / 2f, pane.Y / 2f));
        editor.Drop();
        ctx.Check(editor.Draft[HudElement.CompassTape].X == HudAnchor.Centre && editor.Draft[HudElement.CompassTape].Y == HudAnchor.Centre,
            $"…and the tape dragged to the pane's middle anchors to the centre ({editor.Draft[HudElement.CompassTape]})");
    }

    private static void Save(TestContext ctx, HudLayoutEditor editor, FlightController[] seats, PauseState pause, OptionsStore store)
    {
        var draft = editor.Draft;
        bool? closed = null;
        editor.Closed += saved => closed = saved;
        editor.Close(save: true);
        string line = HudLayoutSetting.Line(HudLayoutKind.Custom);
        ctx.Check(closed == true && !editor.IsOpen && HudLayoutSetting.Current == HudLayoutKind.Custom
            && HudLayoutSetting.Custom == draft && HudLayoutEditor.Preview == null,
            $"Enter's save closes the editor with Custom the running layout");
        ctx.Check(store.Load().HudLayout == "custom" && store.Load().HudLayoutCustom == draft.Format(),
            $"…written to the options file ({store.Load().HudLayout}: {store.Load().HudLayoutCustom})");
        ctx.Check(seats.All(s => s.PilotHud.Layout == draft && s.PilotHud.MessageStack!.LineAt(0) == line),
            $"…every pane on the saved layout with '{line}' posted");
        ctx.Check(pause.Reasons == HaltReason.None && seats.All(s => !s.Pause.Silenced) && !seats[1].PilotHud.LayoutEditing
            && !seats[1].PilotHud.Gauges!.Visible,
            $"…the halt dropped, every seat's keys back, and the None pane's dials hidden again");
    }

    private static void Cancel(TestContext ctx, FlightController[] seats, PauseState pause, OptionsStore store)
    {
        var saved = HudLayoutSetting.Custom;
        var editor = HudLayoutEditor.Open(seats[0], seats, pause, store);
        ctx.Check(editor != null && editor.Draft == saved, $"a second edit starts from the saved layout");
        if (editor == null)
        {
            return;
        }

        editor.Load(HudLayoutPlacement.OriginalLayout);
        editor.Close(save: false);
        ctx.Check(HudLayoutSetting.Custom == saved && store.Load().HudLayoutCustom == saved!.Format()
            && seats.All(s => s.PilotHud.Layout == saved) && pause.Reasons == HaltReason.None,
            $"…and cancelled leaves the saved layout on every pane and in the file");
    }

    private static void Deterministic(TestContext ctx, FlightController[] seats, PauseState pause)
    {
        string? before = OptionsStore.UserOptions().Load().HudLayoutCustom;
        var editor = HudLayoutEditor.Open(seats[0], seats, pause, null);
        editor?.Load(HudLayoutPlacement.Sides);
        var draft = editor?.Draft;
        editor?.Close(save: true);
        ctx.Check(editor != null && HudLayoutSetting.Custom == draft
            && OptionsStore.UserOptions().Load().HudLayoutCustom == before,
            $"a --det save runs the layout and writes no file");
    }

    // The editor's drag range on panes wider than the reading box. Those are a 21:9 and a 32:9 single
    // pane and a 2P side-by-side pane of a 32:9 window. The right column dragged hard right reaches the
    // pane's real edge, past the reading box where the pane is wider. A split pane is its own box.
    private static void WideDrags(TestContext ctx, List<GodotObject> made)
    {
        var root = ctx.Host.GetTree().Root;
        var cases = new (string Name, Vector2I Window, Vector2I Pane)[]
        {
            ("ultrawide 21:9", new Vector2I(3360, 1440), new Vector2I(3360, 1440)),
            ("single 32:9", SuperUltrawideWindow, SuperUltrawideWindow),
            ("2P side by side on 32:9", SuperUltrawideWindow, new Vector2I((SuperUltrawideWindow.X - 2) / 2, SuperUltrawideWindow.Y)),
        };
        foreach (var (name, window, size) in cases)
        {
            root.Size = window;
            var view = View(ctx, size, made);
            var seat = Seat(0, view, made);
            var editor = HudLayoutEditor.Open(seat, new[] { seat }, new PauseState(), null);
            if (editor == null)
            {
                ctx.Check(false, $"{name}: the editor opens");
                continue;
            }

            var (box, pane, scale) = Frame(editor);
            var right = HudLayoutPlacement.Place(HudElement.RightDials, editor.Draft, box, pane, scale);
            editor.Grab(right.GetCenter());
            editor.DragTo(new Vector2(pane.X + 500f, right.GetCenter().Y));
            editor.Drop();
            var moved = HudLayoutPlacement.Place(HudElement.RightDials, editor.Draft, box, pane, scale);
            ctx.Check(Mathf.Abs(moved.End.X - pane.X) <= scale && (pane.X <= box.Size.X + 1f || moved.End.X > box.End.X + 1f)
                && editor.Draft.PaneRelative && editor.Draft[HudElement.RightDials].X == HudAnchor.End,
                $"{name} {pane}: the right column dragged hard right reaches the pane's edge ({moved.End.X}), past any reading box narrower than it ({box})");
            editor.Close(save: false);
        }
    }

    private static void Refusals(TestContext ctx, FlightController[] seats)
    {
        var halted = new PauseState();
        halted.TryToggle(0);
        var network = new PauseState { Overlay = true };
        ctx.Check(HudLayoutEditor.Open(seats[0], seats, halted, null) == null
            && HudLayoutEditor.Open(seats[0], seats, network, null) == null
            && HudLayoutEditor.Open(seats[0], seats, null, null) == null
            && halted.Reasons == HaltReason.Paused && network.Reasons == HaltReason.None,
            $"a paused session, a network session and a seat with no shared pause open no editor");
    }
}
