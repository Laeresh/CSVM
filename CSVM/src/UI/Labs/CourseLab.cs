using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CSVM.Flight.Camera;
using CSVM.Flight.Modes;
using CSVM.UI.Boards;
using CSVM.UI.Screens;
using CSVM.Utils;
using Godot;

namespace CSVM.UI.Labs;

/// <summary>
/// The course editor lab (Shift+F3), built in <c>--freecam</c> alone and opened at launch by
/// <c>--course-edit</c>. A panel down the right edge lists, opens, starts and saves the chapter's courses, and
/// a second one beside it edits the sequence. A left click on the world selects the course gate under the
/// cursor. Off a gate it places the chosen kind on the ground, facing the camera's way. Holding the drag key
/// moves the selected gate over the ground under the cursor. Every edit goes through <see cref="Draft"/>, and on
/// each change <see cref="CourseVisuals"/> and <see cref="CourseSequenceOverlay"/> draw the course again.
/// </summary>
public sealed partial class CourseLab : Node
{
    /// <summary>The name a new course takes until it is renamed.</summary>
    public const string NewName = "new_course";

    /// <summary>Held, moves the selected gate over the ground under the cursor. No freecam tool reads it.</summary>
    public const Key DragKey = Key.H;

    // A down ray for the ground under a point starts this high, above any chapter's terrain.
    private const float RayTopM = 20000f;

    // A row click's flight, TUNE. It eases over this long to an eye behind the gate on the entry's approach.
    // The eye stands back this share of the gate's largest size plus a margin, raised this share of that.
    private const float FlySeconds = 0.6f;
    private const float FlyBackShare = 2.5f;
    private const float FlyBackM = 30f;
    private const float FlyRise = 0.3f;

    private const int SequenceWidth = 280;

    private static readonly Color Amber = new(1f, 0.93f, 0.35f);
    private static readonly Color Dim = new(0.62f, 0.62f, 0.68f);

    private readonly string _chapter;
    private readonly Func<Vector3, Vector3, Vector3?> _groundHit;
    private readonly Dictionary<string, (HSlider Slider, Label Value)> _sliders = new();

    private CanvasLayer? _layer;
    private Control? _panel;
    private CourseVisuals? _visuals;
    private MeshInstance3D? _box;
    private OptionButton? _courses;
    private LineEdit? _name;
    private Label? _info;
    private Label? _selectedTitle;
    private Control? _selected;
    private Control? _pylonRows;
    private Control? _ringRows;
    private Label? _status;
    private VBoxContainer? _sequence;
    private IReadOnlyList<SequenceEntry>? _shownSequence;
    private Button[] _entryButtons = Array.Empty<Button>();
    private Button[] _addButtons = Array.Empty<Button>();
    private CourseSequenceOverlay? _overlay;
    private string _note = "";
    private bool _open;
    private bool _syncing;
    private bool _dragging;
    private Vector2? _groundXz;
    private float _ground;
    private float _flyT = -1f;
    private Vector3 _flyFromEye;
    private Vector3 _flyFromLook;
    private Vector3 _flyEye;
    private Vector3 _flyLook;

    /// <summary>A lab over <paramref name="chapter"/>'s courses in <paramref name="library"/>, saving to
    /// <paramref name="saveTarget"/> (null turns Save off). <paramref name="groundHit"/> rays the world's own
    /// triangles, origin and direction in, nearest point out. A <c>--freecam</c> world builds no colliders.</summary>
    public CourseLab(string chapter, CourseLibrary library, CourseSource? saveTarget, Func<Vector3, Vector3, Vector3?> groundHit)
    {
        _chapter = chapter;
        Library = library;
        SaveTarget = saveTarget;
        _groundHit = groundHit;
        Draft = new CourseDraft(CourseDraft.Empty(chapter, NewName, default));
        Draft.Changed += OnChanged;
        Name = "course_lab";
    }

    /// <summary>What a left click on the ground places.</summary>
    public enum PlaceKind
    {
        /// <summary>Nothing: a click off a course gate is the freecam's selection.</summary>
        Off,

        PylonGate,

        BalloonRing,

        StartPoint,
    }

    /// <summary>The course being edited and its selected gate, the seam a later gizmo or sequence panel edits through.</summary>
    public CourseDraft Draft { get; }

    /// <summary>Where courses are listed, opened and saved. A suite swaps in scratch roots.</summary>
    public CourseLibrary Library { get; set; }

    /// <summary>The root Save writes under, or null where Save is off.</summary>
    public CourseSource? SaveTarget { get; set; }

    /// <summary>What a left click on the ground places.</summary>
    public PlaceKind Place { get; set; } = PlaceKind.PylonGate;

    /// <summary>Opens the panel when the lab enters the tree.</summary>
    public bool StartOpen { get; init; }

    /// <summary>The course the first open loads; null starts a new one.</summary>
    public string? StartCourse { get; init; }

    /// <summary>Whether the panel is showing. Nothing is built until it first opens.</summary>
    public bool IsOpen => _open;

    /// <summary>The drawn course, rebuilt on every change; null until the lab first opens.</summary>
    public CourseVisuals? Visuals => _visuals;

    /// <summary>The entry numbers and sequence line over the world; null until the lab first opens.</summary>
    public CourseSequenceOverlay? Overlay => _overlay;

    /// <summary>The freecam a row click flies, kept in step with the move; without one the camera is set directly.</summary>
    public SpectatorCamera? Spectator { get; init; }

    public override void _Ready()
    {
        if (StartOpen)
        {
            Toggle();
        }
    }

    public override void _Process(double delta)
    {
        if (_flyT < 0f || GetViewport().GetCamera3D() is not { } camera)
        {
            return;
        }

        _flyT = Mathf.Min(1f, _flyT + ((float)delta / FlySeconds));
        float s = Mathf.SmoothStep(0f, 1f, _flyT);
        var eye = _flyFromEye.Lerp(_flyEye, s);
        var look = _flyFromLook.Lerp(_flyLook, s);
        if (Spectator != null)
        {
            Spectator.LookFrom(eye, look);
        }
        else
        {
            camera.LookAtFromPosition(eye, look, Vector3.Up);
        }

        _flyT = _flyT >= 1f ? -1f : _flyT;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F3, ShiftPressed: true })
        {
            Toggle();
            GetViewport().SetInputAsHandled();
            return;
        }

        // Unhandled only, so a click on the panel's own controls never also places on the world.
        if (!_open)
        {
            return;
        }

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
        {
            _flyT = -1f; // a look drag takes the camera back; left unhandled for the freecam
        }

        if (@event is InputEventKey { Echo: false, Keycode: DragKey } key)
        {
            _dragging = key.Pressed && Draft.Selected != null;
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } button && ClickAt(button.Position))
        {
            GetViewport().SetInputAsHandled();
        }
        else if (_dragging && @event is InputEventMouseMotion motion)
        {
            DragTo(motion.Position);
        }
    }

    /// <summary>Shows or hides the panel and the course, building both on the first open.</summary>
    public void Toggle()
    {
        bool first = _layer == null;
        if (first)
        {
            BuildUi();
        }

        _open = !_open;
        _layer!.Visible = _open;
        _dragging = false;
        _flyT = -1f;
        if (first && (StartCourse == null || !Open(StartCourse)))
        {
            // A broken file keeps its name to itself, so a save cannot overwrite what failed to load.
            bool broken = StartCourse != null && Library.List(_chapter).Any(f => string.Equals(f.Name, StartCourse, StringComparison.OrdinalIgnoreCase));
            string note = _note;
            New(StartCourse != null && !broken ? StartCourse : NewName);
            _note = broken ? note : _note;
            UpdateStatus();
        }

        if (_visuals != null)
        {
            _visuals.Visible = _open;
            _overlay!.Visible = _open;
        }

        if (_box != null)
        {
            _box.Visible = _open && Draft.Selected != null;
        }

        Log.Info("ui", $"course lab {(_open ? "open" : "closed")} {_chapter}, {Draft.Course.Name}");
    }

    /// <summary>Loads <paramref name="name"/> from the library. Returns false, the reason on the status line,
    /// when it is missing or broken.</summary>
    public bool Open(string name)
    {
        if (Library.Load(_chapter, name, out string error) is not { } course)
        {
            _note = error;
            Log.Warn("ui", $"course lab: {error}");
            UpdateStatus();
            return false;
        }

        _note = $"opened {_chapter}/{course.Name}";
        Draft.Load(course);
        return true;
    }

    /// <summary>Starts an empty course named <paramref name="name"/>, its start point the camera's pose.</summary>
    public void New(string name)
    {
        var camera = IsInsideTree() ? GetViewport().GetCamera3D() : null;
        var start = camera == null ? default : new SpawnPoint(camera.GlobalPosition, CourseDraft.HeadingOf(camera.GlobalBasis));
        _note = $"new course {_chapter}/{name}, its start at the camera until one is placed";
        Draft.Load(CourseDraft.Empty(_chapter, name, start));
    }

    /// <summary>A left click at <paramref name="screen"/> with the panel open. It selects the course gate under
    /// it, else places <see cref="Place"/> on the ground there facing the camera's way. Returns false,
    /// leaving the click to the freecam, when it hits no gate and nothing is to be placed.</summary>
    public bool ClickAt(Vector2 screen)
    {
        if (GetViewport().GetCamera3D() is not { } camera)
        {
            return false;
        }

        var from = camera.ProjectRayOrigin(screen);
        var dir = camera.ProjectRayNormal(screen);
        if (GateUnder(from, dir) is { } id)
        {
            Draft.Select(id);
            return true;
        }

        if (Place == PlaceKind.Off)
        {
            return false;
        }

        if (_groundHit(from, dir) is not { } ground)
        {
            _note = "no ground under the cursor";
            UpdateStatus();
            return true;
        }

        float heading = CourseDraft.HeadingOf(camera.GlobalBasis);
        if (Place == PlaceKind.StartPoint)
        {
            Draft.PlaceStart(ground, heading);
        }
        else
        {
            Draft.Place(Place == PlaceKind.PylonGate ? CourseGateKind.PylonGate : CourseGateKind.BalloonRing, ground, heading);
        }

        return true;
    }

    /// <summary>Moves the selected gate over the ground under <paramref name="screen"/>, as a held drag does.
    /// A ring keeps its height above the ground. Returns false with nothing selected or no ground there.</summary>
    public bool DragTo(Vector2 screen)
    {
        var camera = GetViewport().GetCamera3D();
        if (Draft.Selected is not { } gate || camera == null
            || _groundHit(camera.ProjectRayOrigin(screen), camera.ProjectRayNormal(screen)) is not { } ground)
        {
            return false;
        }

        float under = GroundUnder(gate);
        return Draft.Update(CourseDraft.MovedTo(gate, ground, under));
    }

    /// <summary>The ground's height under <paramref name="point"/>, by a ray straight down the world's
    /// triangles; null off the world. Over the sea it is the sea surface.</summary>
    public float? GroundBelow(Vector3 point) =>
        _groundHit(new Vector3(point.X, RayTopM, point.Z), Vector3.Down)?.Y;

    /// <summary>A sequence row's click: selects entry <paramref name="index"/> and its gate. The camera eases to an
    /// eye behind the gate on that entry's approach, looking at the opening's middle.</summary>
    public void ChooseEntry(int index)
    {
        Draft.SelectEntry(index);
        if (Draft.SelectedEntry < 0 || Draft.Selected is not { } gate || GetViewport().GetCamera3D() is not { } camera)
        {
            return;
        }

        var centre = CourseDraft.Centre(gate);
        float back = (FlyBackShare * Mathf.Max(gate.Width, Mathf.Max(gate.Height, gate.Diameter))) + FlyBackM;
        _flyEye = centre - (CourseDraft.FlownWay(gate, Draft.Course.Sequence[index]) * back) + (Vector3.Up * (FlyRise * back));
        _flyLook = centre;
        _flyFromEye = camera.GlobalPosition;
        _flyFromLook = camera.GlobalPosition - (camera.GlobalBasis.Z * camera.GlobalPosition.DistanceTo(centre));
        _flyT = 0f;
    }

    /// <summary>Deletes the selected gate and every sequence entry naming it.</summary>
    public void DeleteSelected()
    {
        if (Draft.SelectedId is { } id && Draft.Delete(id))
        {
            _note = $"deleted {id}";
            UpdateStatus();
        }
    }

    /// <summary>Writes the course, named from the name field, under <see cref="SaveTarget"/>. Returns the
    /// path written, or null where Save is off or the write failed, the reason on the status line.</summary>
    public string? Save()
    {
        if (SaveTarget is not { } target)
        {
            _note = "Save is off: this exported build reads no game folder (--det)";
            UpdateStatus();
            return null;
        }

        if (_name != null && _name.Text.Trim() is { Length: > 0 } typed && typed != Draft.Course.Name)
        {
            Draft.Rename(typed);
        }

        try
        {
            string path = Library.Save(Draft.Course, target);
            _note = $"saved {path}";
            RefreshList();
            UpdateStatus();
            return path;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            _note = $"save failed: {ex.Message}";
            Log.Warn("ui", $"course lab: {_note}");
            UpdateStatus();
            return null;
        }
    }

    private static Label Small(string text, Color? colour = null)
    {
        var label = new Label { Text = text, Modulate = colour ?? new Color(1, 1, 1, 0.75f), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", 11);
        return label;
    }

    private static string Format(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    private static Button Named(Button button, string name)
    {
        button.Name = name;
        return button;
    }

    // The pool gate one of whose drawn parts' boxes a ray meets, the nearest box centre winning; null for none.
    private string? GateUnder(Vector3 from, Vector3 dir)
    {
        if (_visuals == null)
        {
            return null;
        }

        string? best = null;
        float bestDistance = float.MaxValue;
        var to = from + (dir * RayTopM);
        var nodes = _visuals.GateNodes;
        for (int i = 0; i < nodes.Count; i++)
        {
            foreach (var part in nodes[i].FindChildren("*", nameof(MeshInstance3D), true, false).Cast<MeshInstance3D>())
            {
                var box = part.GlobalTransform * part.GetAabb();
                if (part.IsVisibleInTree() && box.IntersectsSegment(from, to) && from.DistanceTo(box.GetCenter()) < bestDistance)
                {
                    bestDistance = from.DistanceTo(box.GetCenter());
                    best = Draft.Course.Gates[i].Id;
                }
            }
        }

        return best;
    }

    // The ground under a gate, sampled again only when the gate has moved across the ground.
    private float GroundUnder(CourseGate gate)
    {
        var xz = new Vector2(gate.Position.X, gate.Position.Z);
        if (_groundXz != xz)
        {
            _groundXz = xz;
            _ground = GroundBelow(gate.Position) ?? 0f;
        }

        return _ground;
    }

    private void OnChanged()
    {
        if (_visuals != null)
        {
            RemoveChild(_visuals);
            _visuals.QueueFree();
        }

        _visuals = CourseVisuals.Build(Draft.Course, withStart: true);
        _visuals.Visible = _open;
        AddChild(_visuals);
        if (Draft.Selected is { } gate)
        {
            _box ??= NewBox();
            var node = _visuals.GateNodes[Draft.Course.Gates.ToList().IndexOf(gate)];
            SelectionService.DrawBox(_box, node, SelectionService.SubtreeWorldAabb(node));
            _box.Visible = _open;
        }
        else if (_box != null)
        {
            _box.Visible = false;
        }

        if (_overlay == null)
        {
            _overlay = new CourseSequenceOverlay();
            AddChild(_overlay);
        }

        _overlay.Visible = _open;
        _overlay.Draw(Draft.Course, _visuals.GateNodes, Draft.SelectedId);
        SyncPanel();
    }

    // The selected gate's outline, drawn through the ground and never picked by the freecam.
    private MeshInstance3D NewBox()
    {
        var box = SelectionService.NewBoxInstance("course_selection", Amber, priority: 10);
        box.SetMeta(SelectionService.OverlayMeta, true);
        AddChild(box);
        return box;
    }

    private void BuildUi()
    {
        _layer = new CanvasLayer { Layer = HudLayers.Lab, Visible = false };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        // Down the right edge as the ocean and vegetation labs are; the node lab owns the left.
        var panel = new PanelContainer { SelfModulate = new Color(1, 1, 1, 0.88f) };
        _panel = panel;
        panel.SetAnchorsPreset(Control.LayoutPreset.RightWide);
        panel.OffsetLeft = -392;
        panel.OffsetRight = -8;
        panel.OffsetTop = 40;
        panel.OffsetBottom = -16;
        var margin = new MarginContainer();
        foreach (string side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 8);
        }

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        box.AddChild(new Label { Text = $"COURSE LAB  {_chapter}", Modulate = Amber });

        var openRow = new HBoxContainer();
        _courses = new OptionButton { Name = "course_list", FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        openRow.AddChild(_courses);
        openRow.AddChild(Btn("Open", () =>
        {
            if (_courses.Selected >= 0)
            {
                Open(_courses.GetItemMetadata(_courses.Selected).AsString());
            }
        }));
        box.AddChild(openRow);
        var newRow = new HBoxContainer();
        _name = new LineEdit { Name = "course_name", PlaceholderText = "course name", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _name.TextSubmitted += _ => _name.ReleaseFocus();
        newRow.AddChild(_name);
        newRow.AddChild(Btn("New", () => New(_name.Text.Trim() is { Length: > 0 } typed ? typed : NewName)));
        box.AddChild(newRow);

        box.AddChild(new Label { Text = "LEFT CLICK PLACES", Modulate = Amber });
        var place = new OptionButton { Name = "place_kind", FocusMode = Control.FocusModeEnum.None };
        foreach (string kind in new[] { "Nothing (click selects)", "Pylon gate", "Balloon ring", "Start point" })
        {
            place.AddItem(kind);
        }

        place.Selected = (int)Place;
        place.ItemSelected += i => Place = (PlaceKind)(int)i;
        box.AddChild(place);
        _info = Small("");
        box.AddChild(_info);

        var selected = new VBoxContainer();
        _selected = selected;
        _selectedTitle = new Label { Modulate = Amber };
        selected.AddChild(_selectedTitle);
        var pylonRows = new VBoxContainer();
        _pylonRows = pylonRows;
        AddSlider(pylonRows, "pylon_width", "width (m)", 5f, 200f, 1f, (g, v) => g with { Width = v });
        AddSlider(pylonRows, "pylon_height", "height (m)", 5f, 120f, 1f, (g, v) => g with { Height = v });
        selected.AddChild(pylonRows);
        var ringRows = new VBoxContainer();
        _ringRows = ringRows;
        AddSlider(ringRows, "ring_above", "height above ground (m)", 5f, 500f, 1f, (g, v) => CourseDraft.WithAboveGround(g, v, GroundUnder(g)));
        AddSlider(ringRows, "ring_diameter", "diameter (m)", 5f, 200f, 1f, (g, v) => g with { Diameter = v });
        selected.AddChild(ringRows);
        AddSlider(selected, "heading", "heading (deg)", -180f, 180f, 1f, (g, v) => g with { Heading = v });
        selected.AddChild(Btn("Delete gate", DeleteSelected));
        box.AddChild(selected);

        var save = Btn(SaveTarget == CourseSource.GameFolder ? "Save to the game folder" : "Save to the shipped courses", () => Save());
        save.Name = "save";
        save.Disabled = SaveTarget == null;
        box.AddChild(save);
        box.AddChild(Small(SaveTarget switch
        {
            CourseSource.Shipped => $"Writes {_chapter}/<name>.json under CSVM/data/courses, the shipped courses (dev build).",
            CourseSource.GameFolder => $"Writes {Library.GameFolder}/{_chapter}/<name>.json.",
            _ => "Save is off: this exported build reads no game folder (--det).",
        }, Dim));
        _status = Small("");
        box.AddChild(_status);
        box.AddChild(Small($"Left click a gate to select it · hold {DragKey} and move the mouse to drag it · RMB looks · Shift+F3 hides this panel", Dim));

        margin.AddChild(box);
        panel.AddChild(margin);
        root.AddChild(panel);
        root.AddChild(BuildSequencePanel());
        _layer.AddChild(root);
        AddChild(_layer);
        RefreshList();
    }

    // The sequence panel, left of the course panel: the entries in order, the selected one the insertion point.
    private PanelContainer BuildSequencePanel()
    {
        var panel = new PanelContainer { SelfModulate = new Color(1, 1, 1, 0.88f) };
        panel.SetAnchorsPreset(Control.LayoutPreset.RightWide);
        panel.OffsetLeft = -400 - SequenceWidth;
        panel.OffsetRight = -400;
        panel.OffsetTop = 40;
        panel.OffsetBottom = -16;
        var margin = new MarginContainer();
        foreach (string side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 8);
        }

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        box.AddChild(new Label { Text = "SEQUENCE", Modulate = Amber });
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _sequence = new VBoxContainer { Name = "sequence_rows", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _sequence.AddThemeConstantOverride("separation", 0);
        scroll.AddChild(_sequence);
        box.AddChild(scroll);

        var entryRow = new HBoxContainer();
        _entryButtons = new[]
        {
            Named(Btn("Up", () => Draft.MoveEntry(Draft.SelectedEntry, -1)), "entry_up"),
            Named(Btn("Down", () => Draft.MoveEntry(Draft.SelectedEntry, 1)), "entry_down"),
            Named(Btn("Flip", () => Draft.Flip(Draft.SelectedEntry)), "entry_flip"),
            Named(Btn("Remove", () => Draft.RemoveEntry(Draft.SelectedEntry)), "entry_remove"),
        };
        foreach (var button in _entryButtons)
        {
            entryRow.AddChild(button);
        }

        box.AddChild(entryRow);
        box.AddChild(Small("Add the selected gate again after the selected entry:"));
        var addRow = new HBoxContainer();
        _addButtons = new[]
        {
            Named(Btn("Forward", () => AddAgain(GateDirection.Forward)), "add_forward"),
            Named(Btn("Reverse", () => AddAgain(GateDirection.Reverse)), "add_reverse"),
        };
        foreach (var button in _addButtons)
        {
            addRow.AddChild(button);
        }

        box.AddChild(addRow);
        box.AddChild(Small("Click a row to select it and fly there. A placed gate's entry goes after the selected one; "
            + "a click on a gate in the world keeps it. Remove takes the entry only; Delete gate takes every entry naming it.", Dim));
        margin.AddChild(box);
        panel.AddChild(margin);
        return panel;
    }

    private void AddAgain(GateDirection direction)
    {
        if (Draft.SelectedId is { } id)
        {
            Draft.AddEntry(id, direction);
        }
    }

    // The rows from the sequence, rebuilt only when it changed. An edit that leaves the sequence alone keeps its
    // list instance, so a slider drag rebuilds no rows. The selected entry is the selected row.
    private void SyncSequence()
    {
        if (_sequence == null)
        {
            return;
        }

        var course = Draft.Course;
        if (!ReferenceEquals(_shownSequence, course.Sequence))
        {
            _shownSequence = course.Sequence;
            foreach (var old in _sequence.GetChildren())
            {
                _sequence.RemoveChild(old);
                old.QueueFree();
            }

            for (int i = 0; i < course.Sequence.Count; i++)
            {
                var entry = course.Sequence[i];
                bool pylons = course.Gates.First(g => g.Id == entry.GateId).Kind == CourseGateKind.PylonGate;
                int index = i;
                var row = Btn($"{i + 1}.  {entry.GateId}  {(pylons ? "pylon gate" : "balloon ring")}  "
                    + (entry.Direction == GateDirection.Forward ? "forward" : "reverse"), () => ChooseEntry(index));
                row.Name = $"row_{i}";
                row.Flat = true;
                row.Alignment = HorizontalAlignment.Left;
                row.AddThemeFontSizeOverride("font_size", 12);
                _sequence.AddChild(row);
            }
        }

        // A flat row draws no pressed box, so the selected one is told by its colour.
        foreach (var row in _sequence.GetChildren().Cast<Button>())
        {
            row.Modulate = row.GetIndex() == Draft.SelectedEntry ? Amber : Colors.White;
        }

        foreach (var button in _entryButtons)
        {
            button.Disabled = Draft.SelectedEntry < 0;
        }

        foreach (var button in _addButtons)
        {
            button.Disabled = Draft.SelectedId == null;
        }
    }

    // A slider over one field of the selected gate, applied as it moves.
    private void AddSlider(VBoxContainer rows, string name, string label, float min, float max, float step, Func<CourseGate, float, CourseGate> edit)
    {
        var head = new HBoxContainer();
        var title = Small(label);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.AutowrapMode = TextServer.AutowrapMode.Off;
        head.AddChild(title);
        var value = Small("");
        value.AutowrapMode = TextServer.AutowrapMode.Off;
        head.AddChild(value);
        rows.AddChild(head);
        var slider = new HSlider
        {
            Name = name,
            MinValue = min,
            MaxValue = max,
            Step = step,
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        slider.ValueChanged += v =>
        {
            if (!_syncing && Draft.Selected is { } gate)
            {
                Draft.Update(edit(gate, (float)v));
            }
        };
        rows.AddChild(slider);
        _sliders[name] = (slider, value);
    }

    private void RefreshList()
    {
        if (_courses == null)
        {
            return;
        }

        _courses.Clear();
        foreach (var file in Library.List(_chapter))
        {
            _courses.AddItem($"{file.Name} ({(file.Source == CourseSource.Shipped ? "shipped" : "game folder")})");
            _courses.SetItemMetadata(_courses.ItemCount - 1, file.Name);
        }
    }

    // The panel from the draft: the course line, and the selected gate's rows with their values.
    private void SyncPanel()
    {
        if (_info == null)
        {
            return;
        }

        var course = Draft.Course;
        if (_name != null && !_name.HasFocus())
        {
            _name.Text = course.Name;
        }

        var s = course.Start;
        _info.Text = $"{course.Name}: {course.Gates.Count} gate(s), {course.Sequence.Count} sequence entr{(course.Sequence.Count == 1 ? "y" : "ies")}"
            + $"\nstart ({Format(s.Position.X)}, {Format(s.Position.Y)}, {Format(s.Position.Z)}) heading {Format(s.HeadingDeg)}";
        var gate = Draft.Selected;
        _selected!.Visible = gate != null;
        if (gate != null)
        {
            bool pylons = gate.Kind == CourseGateKind.PylonGate;
            _selectedTitle!.Text = $"SELECTED  {gate.Id}, {(pylons ? "pylon gate" : "balloon ring")}";
            _pylonRows!.Visible = pylons;
            _ringRows!.Visible = !pylons;
            _syncing = true;
            Show("pylon_width", gate.Width);
            Show("pylon_height", gate.Height);
            Show("ring_above", gate.Position.Y - GroundUnder(gate));
            Show("ring_diameter", gate.Diameter);
            Show("heading", gate.Heading);
            _syncing = false;
        }

        SyncSequence();
        UpdateStatus();
    }

    private void Show(string name, float v)
    {
        var (slider, value) = _sliders[name];
        slider.Value = v;
        value.Text = Format(v);
    }

    private void UpdateStatus()
    {
        if (_status != null)
        {
            _status.Text = _note;
        }
    }

    private Button Btn(string text, Action pressed)
    {
        // FocusMode None keeps a clicked button from swallowing the camera's keys afterwards.
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        b.Pressed += () =>
        {
            GetViewport().GuiReleaseFocus();
            pressed();
        };
        return b;
    }
}
