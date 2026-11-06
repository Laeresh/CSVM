using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSVM.Effects;
using CSVM.Session.World;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.UI.Labs;

/// <summary>
/// The lighting lab (Shift+F5), built in <c>--freecam</c> alone. Its right-edge panel holds a slider
/// per <see cref="ZoneLighting"/> field for the camera's zone or one the dropdown holds
/// (<see cref="WeatherRig.ForceZone"/>). An edit reaches the session
/// sun, the Environment and the cockpit and spyglass suns through the weather rig. Save writes this
/// zone's chapter block or the mission's sparse block into <c>lighting.json</c> from the source tree,
/// and is off in an exported build. A field the mission overrides is marked, with a reset to the
/// chapter's value. A parked plane ahead of the camera turns through the sun on its sliders.
/// </summary>
public sealed partial class LightingLab : Node
{
    // Seconds after a click or wheel step before the edit reaches the world; a drag applies on release.
    private const float ApplyDelay = 0.25f;

    private const float StatusPeriod = 0.5f;

    // Metres ahead of the camera the plane parks, shortened by geometry in the way but never under the floor.
    private const float ParkDistance = 40f;
    private const float ParkFloor = 12f;

    private static readonly Color Amber = new(1f, 0.93f, 0.35f);
    private static readonly Color Dim = new(0.62f, 0.62f, 0.68f);

    private readonly string _chapter;
    private readonly string _mission;
    private readonly WeatherRig _weather;
    private readonly Camera3D _camera;
    private readonly IReadOnlyList<string> _planes;
    private readonly Func<string, Node3D> _buildPlane;
    private readonly Dictionary<string, ZoneLighting> _edits = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _overrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Row> _rows = new();
    private readonly HSlider?[] _attitudeSliders = new HSlider?[3];

    private string _plane;
    private string? _pick;
    private string _rowsZone = "";
    private CanvasLayer? _layer;
    private Label? _status;
    private OptionButton? _zones;
    private CheckButton? _off;
    private bool _open;
    private bool _syncing;
    private float _pending = -1f;
    private float _statusTimer;
    private string _note = "";

    private Node3D? _holder;
    private Node3D? _model;
    private bool _parkPending;
    // Pitch, heading and bank in degrees; a NaN heading takes the camera's at the first park.
    private Vector3 _attitude = new(0f, float.NaN, 0f);

    /// <summary>A lab over <paramref name="chapter"/>'s <paramref name="mission"/>, lit through
    /// <paramref name="weather"/>. The plane parks ahead of <paramref name="camera"/>, built by
    /// <paramref name="buildPlane"/> from <paramref name="planes"/>. A null <paramref name="savePath"/> turns Save off.</summary>
    public LightingLab(string chapter, string mission, WeatherRig weather, Camera3D camera, string? savePath,
        IReadOnlyList<string> planes, string plane, Func<string, Node3D> buildPlane)
    {
        _chapter = chapter;
        _mission = mission;
        _weather = weather;
        _camera = camera;
        SavePath = savePath;
        _planes = planes;
        _plane = plane;
        _buildPlane = buildPlane;
        Name = "lighting_lab";
    }

    /// <summary>Gets or sets the file Save writes, null where Save is off. A suite points it at a scratch copy.</summary>
    public string? SavePath { get; set; }

    /// <summary>The <c>--debug-lighting</c> value, applied when the lab enters the tree.</summary>
    public string? Debug { get; init; }

    /// <summary>Whether the panel is showing. Nothing is built until it first opens.</summary>
    public bool IsOpen => _open;

    /// <summary>The weather rig the lab lights through.</summary>
    public WeatherRig Weather => _weather;

    /// <summary>The zone the sliders edit: the one the dropdown holds, else the camera's.</summary>
    public string ShownZone => _pick ?? _weather.LiveZone ?? _weather.ZoneNames[0];

    /// <summary>The tune the shown zone lights with, as last applied or pending.</summary>
    public ZoneLighting Edited => EditOf(ShownZone);

    /// <summary>The fields this mission overrides in the shown zone, the marked rows.</summary>
    public IReadOnlyCollection<string> Overrides => OverridesOf(ShownZone);

    /// <summary>The parked plane's holder, null until it first parks.</summary>
    public Node3D? ParkedPlane => _holder;

    /// <summary>The parked airframe's node name.</summary>
    public string Plane => _plane;

    public override void _Ready()
    {
        SetPhysicsProcess(false);
        SetProcess(false);
        if (Debug is not { Length: > 0 } debug)
            return;
        if (LightingDebug.Word(debug, "zone") is { } zone)
            PickZone(zone);
        if (LightingDebug.SetsFields(debug))
        {
            Edit(LightingDebug.WithFields(Edited, debug));
            Commit();
        }
        if (LightingDebug.Has(debug, "off"))
            SetOff(true);
        string? plane = LightingDebug.Word(debug, "plane");
        float? heading = LightingDebug.Angle(debug, "heading"), pitch = LightingDebug.Angle(debug, "pitch"), bank = LightingDebug.Angle(debug, "bank");
        if (plane != null || heading != null || pitch != null || bank != null)
        {
            _attitude = new Vector3(pitch ?? 0f, heading ?? float.NaN, bank ?? 0f);
            _plane = plane ?? _plane;
            RequestPark();
        }
        if (LightingDebug.Has(debug, "open"))
            Toggle();
        Log.Info("ui", $"lighting lab: --debug-lighting={debug}, {ShownZone} tune={Edited.Describe()}{(_weather.LightingOff ? " (off)" : "")}");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F5, ShiftPressed: true })
        {
            Toggle();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (_pending >= 0f)
        {
            _pending -= (float)delta;
            if (_pending < 0f)
                Commit();
        }
        if (!_open)
        {
            SetProcess(_pending >= 0f);
            return;
        }
        _statusTimer += (float)delta;
        if (_statusTimer >= StatusPeriod)
        {
            _statusTimer = 0f;
            if (!_rowsZone.Equals(ShownZone, StringComparison.OrdinalIgnoreCase))
                SyncRows();
            UpdateStatus();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        SetPhysicsProcess(false);
        if (_parkPending)
            Park();
    }

    /// <summary>Shows or hides the panel, building it and parking the plane on the first open.</summary>
    public void Toggle()
    {
        if (_layer == null)
        {
            BuildUi();
            if (_holder == null)
                RequestPark();
        }
        _open = !_open;
        _layer!.Visible = _open;
        SetProcess(_open || _pending >= 0f);
        if (_open)
        {
            SyncRows();
            UpdateStatus();
        }
        Log.Info("ui", $"lighting lab {(_open ? "open" : "closed")} {_chapter}/{_mission} {ShownZone} tune={Edited.Describe()}");
    }

    /// <summary>Holds the world in <paramref name="zone"/>, or with null follows the camera again.</summary>
    public void PickZone(string? zone)
    {
        _weather.ForceZone(zone);
        _pick = _weather.ForcedZone;
        _note = _pick == null ? "following the camera's zone" : $"holding the world in {_pick}";
        SyncRows();
        UpdateStatus();
    }

    /// <summary>Sets one field of the shown zone by its key and applies at once, as a released slider does.</summary>
    public void Set(string key, float value)
    {
        var field = ZoneLighting.Find(key) ?? throw new ArgumentException($"'{key}' is not a lighting field", nameof(key));
        Edit(field.With(Edited, value));
        Commit();
    }

    /// <summary>One overridden field back to the chapter's value, no longer the mission's (not saved).</summary>
    public void ResetToChapter(string key)
    {
        var field = ZoneLighting.Find(key) ?? throw new ArgumentException($"'{key}' is not a lighting field", nameof(key));
        string zone = ShownZone;
        OverridesOf(zone).Remove(field.Key);
        _edits[zone] = field.With(Edited, field.Get(ChapterSaved(zone)));
        _note = $"{field.Key} back to the chapter's value (not saved)";
        Commit();
    }

    /// <summary>The shown zone back to the file's tune and overrides, applied at once.</summary>
    public void RevertToSaved()
    {
        string zone = ShownZone;
        _edits.Remove(zone);
        _overrides.Remove(zone);
        _note = "reverted to the saved tune";
        Commit();
    }

    /// <summary>The untuned look on or off, the before/after switch. The edits stay.</summary>
    public void SetOff(bool off)
    {
        _weather.LightingOff = off;
        if (_off != null && _off.ButtonPressed != off)
        {
            _syncing = true;
            _off.ButtonPressed = off;
            _syncing = false;
        }
        Log.Info("ui", $"lighting lab: tune {(off ? "off, untuned look" : "on")}");
        UpdateStatus();
    }

    /// <summary>Writes the shown zone's chapter block: every field the mission does not override goes to
    /// the chapter, and the overrides stay the mission's. Returns the path written, or null.</summary>
    public string? SaveToChapter()
    {
        string zone = ShownZone;
        var edit = Edited;
        var overrides = OverridesOf(zone);
        var chapter = ChapterSaved(zone);
        foreach (var f in ZoneLighting.Fields)
        {
            if (!overrides.Contains(f.Key))
                chapter = f.With(chapter, f.Get(edit));
        }
        return Write(zone, chapter, "chapter");
    }

    /// <summary>Writes the shown zone's mission block: every field that differs from the chapter's.
    /// Returns the path written, or null.</summary>
    public string? SaveToMission()
    {
        string zone = ShownZone;
        OverridesOf(zone).Clear();
        foreach (var key in Edited.Differences(ChapterSaved(zone)).Keys)
            OverridesOf(zone).Add(key);
        return Write(zone, ChapterSaved(zone), "mission");
    }

    /// <summary>Parks <paramref name="plane"/> (or the current airframe) ahead of the camera on the next
    /// physics step, keeping its attitude.</summary>
    public void RequestPark(string? plane = null)
    {
        if (plane != null && !plane.Equals(_plane, StringComparison.OrdinalIgnoreCase))
        {
            _plane = plane;
            DropModel();
        }
        _parkPending = true;
        SetPhysicsProcess(true);
    }

    /// <summary>The parked plane's heading, pitch and bank in degrees.</summary>
    public void SetAttitude(float heading, float pitch, float bank)
    {
        _attitude = new Vector3(pitch, heading, bank);
        if (_holder != null)
            _holder.RotationDegrees = _attitude;
    }

    private static Label Small(string text, Color? colour = null)
    {
        var label = new Label { Text = text, Modulate = colour ?? new Color(1, 1, 1, 0.75f), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", 11);
        return label;
    }

    private ZoneLighting EditOf(string zone) =>
        _edits.TryGetValue(zone, out var edit) ? edit : _weather.Lighting.For(_chapter, _mission, zone);

    private HashSet<string> OverridesOf(string zone)
    {
        if (!_overrides.TryGetValue(zone, out var set))
            _overrides[zone] = set = new HashSet<string>(_weather.Lighting.MissionFields(_chapter, _mission, zone).Keys, StringComparer.OrdinalIgnoreCase);
        return set;
    }

    private ZoneLighting ChapterSaved(string zone) => _weather.Lighting.ChapterLighting(_chapter, zone);

    // A new value for the shown zone; an overridden field keeps its mark and takes the value.
    private void Edit(ZoneLighting edit) => _edits[ShownZone] = edit.Clamped();

    // Hands the shown zone's edit to the weather rig, which lights the zone with it.
    private void Commit()
    {
        _pending = -1f;
        string zone = ShownZone;
        _weather.SetLighting(zone, _edits.TryGetValue(zone, out var edit) ? edit : null);
        SyncRows();
        UpdateStatus();
        Log.Info("ui", $"lighting lab: {_chapter}/{_mission} {zone} tune={Edited.Describe()} overrides=[{string.Join(",", Overrides)}]");
    }

    private string? Write(string zone, ZoneLighting chapter, string what)
    {
        if (SavePath == null)
        {
            _note = "Save is off: an exported build carries its lighting inside the pck";
            UpdateStatus();
            return null;
        }
        var edit = Edited;
        var mission = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in OverridesOf(zone))
            mission[key] = ZoneLighting.Find(key)!.Get(edit);
        try
        {
            LightingTable.Save(SavePath, _chapter, _mission, zone, chapter, mission);
            var table = LightingTable.Parse(File.ReadAllText(SavePath, Encoding.UTF8));
            _weather.ReloadLighting(table);
            _overrides[zone] = new HashSet<string>(table.MissionFields(_chapter, _mission, zone).Keys, StringComparer.OrdinalIgnoreCase);
            _note = $"saved {_chapter} {zone} to the {what}, {_overrides[zone].Count} mission override(s), to {SavePath}";
            Log.Info("ui", $"lighting lab: saved {_chapter}/{_mission} {zone} to the {what}: chapter={table.ChapterLighting(_chapter, zone).Describe()} overrides=[{string.Join(",", _overrides[zone])}]");
            Commit();
            return SavePath;
        }
        catch (InvalidDataException ex)
        {
            _note = $"save refused: {ex.Message}; fix or delete {SavePath} first";
            Log.Warn("ui", $"lighting lab: left {SavePath} unwritten, {ex.Message}");
            UpdateStatus();
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _note = $"save failed: {ex.Message}";
            Log.Error("ui", $"lighting lab: could not write {SavePath}", ex);
            UpdateStatus();
            return null;
        }
    }

    // The plane on the camera's level line, at its altitude, as far ahead as the world leaves clear.
    private void Park()
    {
        _parkPending = false;
        if (!_camera.IsInsideTree())
            return;
        if (_holder == null)
        {
            _holder = new Node3D { Name = "lighting_lab_plane" };
            AddChild(_holder);
        }
        if (_model == null)
        {
            _model = _buildPlane(_plane);
            _holder.AddChild(_model);
        }
        var from = _camera.GlobalPosition;
        var ahead = -_camera.GlobalBasis.Z;
        ahead.Y = 0f;
        ahead = ahead.LengthSquared() < 1e-6f ? Vector3.Forward : ahead.Normalized();
        var (reach, _) = LabParking.ClearReach(_camera.GetWorld3D().DirectSpaceState, from, ahead, ParkDistance, ParkFloor);
        _holder.GlobalPosition = from + (ahead * reach);
        // An unset heading faces the camera's way, so the first park shows the plane from behind.
        if (float.IsNaN(_attitude.Y))
            _attitude.Y = Mathf.Round(Mathf.RadToDeg(Mathf.Atan2(-ahead.X, -ahead.Z)));
        _holder.RotationDegrees = _attitude;
        SyncAttitude();
        var at = _holder.GlobalPosition;
        Log.Info("ui", $"lighting lab: parked {_plane} {reach:0} m ahead at ({at.X:0.0},{at.Y:0.0},{at.Z:0.0}), heading {_attitude.Y:0} pitch {_attitude.X:0} bank {_attitude.Z:0}");
    }

    private void DropModel()
    {
        if (_model == null)
            return;
        _model.GetParent()?.RemoveChild(_model);
        _model.QueueFree();
        _model = null;
    }

    private void BuildUi()
    {
        _layer = new CanvasLayer { Layer = HudLayers.Lab, Visible = false };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        // Down the right edge, clear of the freecam readout above; the node lab owns the left.
        var panel = new PanelContainer { SelfModulate = new Color(1, 1, 1, 0.88f) };
        panel.SetAnchorsPreset(Control.LayoutPreset.RightWide);
        panel.OffsetLeft = -412;
        panel.OffsetRight = -8;
        panel.OffsetTop = 40;
        panel.OffsetBottom = -16;

        var margin = new MarginContainer();
        foreach (string side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 8);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        box.AddChild(new Label { Text = $"LIGHTING LAB  {_chapter}/{_mission}", Modulate = Amber });

        var top = new HBoxContainer();
        _zones = new OptionButton { FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _zones.AddItem("Follow camera");
        foreach (var zone in _weather.ZoneNames)
            _zones.AddItem(zone.ToUpperInvariant());
        _zones.ItemSelected += i => PickZone(i == 0 ? null : _weather.ZoneNames[(int)i - 1]);
        top.AddChild(_zones);
        _off = new CheckButton { Text = "Entry off", FocusMode = Control.FocusModeEnum.None, ButtonPressed = _weather.LightingOff };
        _off.Toggled += on =>
        {
            if (!_syncing)
                SetOff(on);
        };
        top.AddChild(_off);
        box.AddChild(top);

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        var rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", 2);
        AddPlaneRows(rows);
        string group = "";
        foreach (var field in ZoneLighting.Fields)
        {
            if (field.Group != group)
            {
                group = field.Group;
                rows.AddChild(new Label { Text = group.ToUpperInvariant(), Modulate = Amber });
            }
            AddRow(rows, field);
        }
        scroll.AddChild(rows);
        box.AddChild(scroll);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 4);
        buttons.AddChild(Btn("Revert to saved", RevertToSaved));
        var chapter = Btn("Save to chapter", () => SaveToChapter());
        var mission = Btn("Save to this mission", () => SaveToMission());
        chapter.Disabled = mission.Disabled = SavePath == null;
        buttons.AddChild(chapter);
        buttons.AddChild(mission);
        box.AddChild(buttons);
        if (SavePath == null)
            box.AddChild(Small("Save is off in an exported build: its lighting lives inside the pck.", Dim));
        _status = Small("");
        box.AddChild(_status);
        box.AddChild(Small("* marks a field this mission overrides · RMB looks, the sliders take the left button · Shift+F5 hides this panel", Dim));

        margin.AddChild(box);
        panel.AddChild(margin);
        root.AddChild(panel);
        _layer.AddChild(root);
        AddChild(_layer);
    }

    private void AddPlaneRows(VBoxContainer rows)
    {
        rows.AddChild(new Label { Text = "PARKED PLANE", Modulate = Amber });
        var line = new HBoxContainer();
        var planes = new OptionButton { FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var names = _planes.Contains(_plane, StringComparer.OrdinalIgnoreCase) ? _planes : _planes.Prepend(_plane).ToList();
        for (int i = 0; i < names.Count; i++)
        {
            planes.AddItem(names[i]);
            if (names[i].Equals(_plane, StringComparison.OrdinalIgnoreCase))
                planes.Select(i);
        }
        planes.ItemSelected += i => RequestPark(names[(int)i]);
        line.AddChild(planes);
        line.AddChild(Btn("Re-park", () => RequestPark()));
        rows.AddChild(line);
        string[] labels = { "Heading (deg)", "Pitch (deg)", "Bank (deg)" };
        float[] ranges = { 180f, 90f, 180f };
        for (int axis = 0; axis < 3; axis++)
        {
            rows.AddChild(Small(labels[axis]));
            var slider = new HSlider
            {
                MinValue = -ranges[axis],
                MaxValue = ranges[axis],
                Step = 1,
                FocusMode = Control.FocusModeEnum.None,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            slider.ValueChanged += _ =>
            {
                if (!_syncing)
                    SetAttitude((float)_attitudeSliders[0]!.Value, (float)_attitudeSliders[1]!.Value, (float)_attitudeSliders[2]!.Value);
            };
            _attitudeSliders[axis] = slider;
            rows.AddChild(slider);
        }
        SyncAttitude();
    }

    private void AddRow(VBoxContainer rows, ZoneLighting.Field field)
    {
        var head = new HBoxContainer();
        var name = Small(field.Label);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.AutowrapMode = TextServer.AutowrapMode.Off;
        head.AddChild(name);
        var value = Small("");
        value.AutowrapMode = TextServer.AutowrapMode.Off;
        head.AddChild(value);
        var reset = Btn("reset", () => ResetToChapter(field.Key));
        reset.AddThemeFontSizeOverride("font_size", 10);
        head.AddChild(reset);
        rows.AddChild(head);

        // No keyboard focus, so the arrow keys and WASD keep flying the camera after a click.
        var slider = new HSlider
        {
            MinValue = field.Min,
            MaxValue = field.Max,
            Step = field.Step,
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        slider.ValueChanged += v =>
        {
            if (_syncing)
                return;
            Edit(field.With(Edited, (float)v));
            value.Text = ValueText(field);
            _pending = ApplyDelay;
            SetProcess(true);
        };
        slider.DragEnded += changed =>
        {
            if (changed)
                Commit();
        };
        rows.AddChild(slider);
        if (field.Note.Length > 0)
            rows.AddChild(Small(field.Note, Dim));
        _rows.Add(new Row(field, slider, value, reset));
    }

    private string ValueText(ZoneLighting.Field field)
    {
        bool marked = Overrides.Contains(field.Key);
        return $"{(marked ? "* " : "")}{field.Format(field.Get(Edited))}   (chapter {field.Format(field.Get(ChapterSaved(ShownZone)))})";
    }

    // The rows and the dropdown from the shown zone's edit, which a zone change, reset or revert moves.
    private void SyncRows()
    {
        _rowsZone = ShownZone;
        _syncing = true;
        foreach (var row in _rows)
        {
            row.Slider.Value = row.Field.Get(Edited);
            row.Value.Text = ValueText(row.Field);
            row.Reset.Disabled = !Overrides.Contains(row.Field.Key);
        }
        if (_zones != null)
        {
            int at = _pick == null ? 0 : 1 + _weather.ZoneNames.ToList().FindIndex(z => z.Equals(_pick, StringComparison.OrdinalIgnoreCase));
            if (_zones.Selected != at)
                _zones.Select(at);
        }
        _syncing = false;
    }

    private void SyncAttitude()
    {
        if (_attitudeSliders[0] == null || float.IsNaN(_attitude.Y))
            return;
        _syncing = true;
        _attitudeSliders[0]!.Value = _attitude.Y;
        _attitudeSliders[1]!.Value = _attitude.X;
        _attitudeSliders[2]!.Value = _attitude.Z;
        _syncing = false;
    }

    private void UpdateStatus()
    {
        if (_status == null)
            return;
        string zone = _pick != null ? $"{ShownZone} held" : $"{ShownZone}, the camera's";
        string saved = Edited == _weather.Lighting.For(_chapter, _mission, ShownZone) ? "matches the saved tune" : "unsaved changes";
        string off = _weather.LightingOff ? " · tune OFF (untuned look)" : "";
        _status.Text = $"{zone} · {Overrides.Count} override(s) · {saved}{off}{(_note.Length > 0 ? $"\n{_note}" : "")}";
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

    private sealed record Row(ZoneLighting.Field Field, HSlider Slider, Label Value, Button Reset);
}
