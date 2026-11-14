using System;
using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Session.World;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.UI.Labs;

/// <summary>
/// The cloud lab (Shift+F4), built in <c>--freecam</c> alone. A panel down the right edge holds a
/// slider per cloud-shadow tune (<see cref="CloudShadowPattern.Tune"/>) and the drift's speed and
/// heading. A red tint shows the pattern and its drift, and every edit applies at once. Save writes
/// the tunes and the drift into <see cref="CloudShadowChapters"/>, the clouds layer while the volumetric
/// clouds stand, else the base tune. A layer change drops unsaved edits. A button steps the volumetric clouds through
/// off, quarter, half and full for this flight without saving the video option.
/// </summary>
internal sealed partial class CloudLab : Node
{
    /// <summary>The tint's light under a shadow's core, a strong red no ground texture hides.</summary>
    public static readonly Vector3 TintLight = new(1.2f, 0.15f, 0.15f);

    private const float StatusPeriod = 0.25f;

    private static readonly Color Amber = new(1f, 0.93f, 0.35f);
    private static readonly Color Dim = new(0.62f, 0.62f, 0.68f);

    // PROTOTYPE (clouds-look): the look's options as sliders. Each is a uniform with its label, range and step.
    private static readonly (string Key, string Label, float Min, float Max, float Step)[] LookRows =
    {
        ("depth_m", "Layer depth (m)", 100f, 3000f, 5f),
        ("shape_m", "Shape tile (m)", 500f, 6000f, 50f),
        ("detail_tile_m", "Detail tile (m)", 50f, 1000f, 10f),
        ("detail_erode", "Detail erosion", 0f, 1f, 0.01f),
        ("base_erode", "Base erosion", 0f, 2f, 0.01f),
        ("base_rag", "Ragged base", 0f, 0.5f, 0.01f),
        ("head_soft", "Head softness", 0.05f, 1f, 0.01f),
        ("heads_gain", "Heads gain", 0f, 2f, 0.01f),
        ("cover_max", "Most cover", 0f, 1f, 0.01f),
        ("pw_density", "Density", 0.005f, 0.2f, 0.005f),
        ("edge_cut", "Edge cut", 0f, 0.9f, 0.01f),
        ("edge_k", "Edge hardness", 1f, 10f, 0.1f),
        ("deck_scale", "Deck: tile scale", 0.2f, 3f, 0.05f),
        ("deck_k", "Deck: density", 0f, 2f, 0.05f),
        ("deck_erode", "Deck: erosion", 0f, 1f, 0.01f),
        ("sun_gain", "Light: sun gain", 0f, 5f, 0.05f),
        ("light_k", "Light: self-shadow", 0f, 5f, 0.05f),
        ("light_step_m", "Light: shadow step (m)", 5f, 100f, 1f),
        ("phase_g", "Light: forward scatter", 0f, 0.95f, 0.01f),
        ("phase_back", "Light: back scatter", 0f, 1f, 0.01f),
        ("powder_k", "Light: powder", 0f, 2f, 0.05f),
        ("ms_a", "Light: multi-scatter a", 0f, 1f, 0.01f),
        ("ms_b", "Light: multi-scatter b", 0f, 1f, 0.01f),
        ("ms_c", "Light: multi-scatter c", 0f, 1f, 0.01f),
        ("amb_gain", "Ambient: gain", 0f, 3f, 0.05f),
        ("amb_floor", "Ambient: floor", 0f, 1f, 0.01f),
        ("amb_occ", "Ambient: occlusion", 0f, 1f, 0.01f),
        ("amb_od_k", "Ambient: depth falloff", 0f, 1f, 0.01f),
        ("amb_fog_mix", "Ambient: fog mix", 0f, 1f, 0.01f),
        ("cloud_fog", "Fog over the clouds", 0f, 1f, 0.01f),
        ("shaft_steps", "Shaft steps", 8f, 64f, 1f),
        ("temporal", "March: temporal resolve (0 or 1)", 0f, 1f, 1f),
        ("cloud_min_m", "March: least step in cloud (m)", 2f, 40f, 1f),
        ("pix_k", "March: step per pixel", 0.001f, 0.02f, 0.0005f),
        ("od_step", "March: optical-depth step", 0f, 1f, 0.05f),
        ("lit_max_m", "March: largest lit step (m)", 2f, 64f, 1f),
        ("lit_growth", "March: lit step growth", 0f, 0.1f, 0.005f),
        ("step_growth", "March: step growth", 0f, 0.15f, 0.005f),
        ("light_every_a", "March: re-light per opacity", 0f, 0.2f, 0.005f),
        ("search_gap_m", "Search: gap (m)", 0f, 2000f, 50f),
        ("search_pix_k", "Search: step per pixel", 0.001f, 0.02f, 0.0005f),
        ("cover_gap_m", "Search: cover gap (m)", 100f, 3000f, 50f),
        ("cover_stride", "Search: cover stride", 1f, 4f, 0.5f),
        ("retreat_k", "Search: retreat", 0f, 3f, 0.1f),
        ("bisect_n", "Search: bisections", 0f, 10f, 1f),
        ("step_budget", "Step budget", 100f, 3000f, 50f),
        ("dither_k", "Dither", 0f, 1f, 0.05f),
    };

    private readonly Func<CloudShadow?> _clouds;
    private readonly string _chapter;
    private readonly string? _savePath;
    private readonly List<(Row Row, HSlider Slider, Label Value)> _rows = new();
    private float _heading;
    private CloudShadowPattern.Tune _saved = CloudShadowPattern.Tune.Default;
    // The layer the sliders edit and Save writes: the clouds layer, else the base tune.
    private bool _editsClouds;
    private string _note = "";
    private CanvasLayer? _layer;
    private CheckButton? _tint;
    private Button? _volumetric;
    private Button? _look;
    private Label? _layerLabel;
    private Label? _status;
    private bool _open;
    private bool _syncing;
    private float _statusTimer;

    /// <summary>A lab over the session's cloud shadows, read through <paramref name="clouds"/>, saving
    /// <paramref name="chapter"/>'s entry to <paramref name="savePath"/>; a null path turns Save off.</summary>
    public CloudLab(Func<CloudShadow?> clouds, string chapter, string? savePath)
    {
        _clouds = clouds;
        _chapter = chapter;
        _savePath = savePath;
        Name = "cloud_lab";
    }

    /// <summary>Whether the panel is showing. Nothing is built until it first opens.</summary>
    public bool IsOpen => _open;

    /// <summary>Gets what the Volumetric Clouds button calls: the option's next word for this flight, the
    /// video option unsaved. Null hides the button.</summary>
    public Action<string>? SetVolumetric { get; init; }

    private string EditedLayer => _editsClouds ? "clouds layer" : "base tune";

    public override void _Ready() => SetProcess(false);

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F4, ShiftPressed: true })
        {
            Toggle();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        FollowLayer();
        _statusTimer += (float)delta;
        if (_statusTimer >= StatusPeriod)
        {
            _statusTimer = 0f;
            UpdateStatus();
        }
    }

    /// <summary>Shows or hides the panel, building it on the first open.</summary>
    public void Toggle()
    {
        if (_layer == null)
            BuildUi();
        _open = !_open;
        _layer!.Visible = _open;
        SetProcess(_open);
        if (_open)
        {
            FollowLayer();
            SyncRows();
            UpdateStatus();
        }
        Log.Info("ui", $"cloud lab {(_open ? "open" : "closed")} {EditedLayer} {Describe()}");
    }

    /// <summary>Every tune back to the chapter's saved entry in the edited layer, the tint off.</summary>
    public void RevertToSaved()
    {
        if (_clouds() is not { } clouds)
            return;
        clouds.Tune = _saved;
        clouds.Drift = _saved.Drift(clouds.Authored);
        _heading = SavedHeading();
        clouds.Tint = Vector3.Zero;
        SyncRows();
        Log.Info("ui", $"cloud lab: reverted {Describe()}");
    }

    /// <summary>Re-reads which layer the Volumetric Clouds word makes the edited one. When it moved, the
    /// shadows take that layer's saved tune, dropping every unsaved edit made over the other.</summary>
    public void FollowLayer()
    {
        if (_clouds() is not { } clouds || !IsInstanceValid(clouds))
            return;
        clouds.FollowLayer();
        if (clouds.CloudsLayer == _editsClouds)
            return;
        _editsClouds = clouds.CloudsLayer;
        _saved = clouds.Saved;
        _heading = SavedHeading();
        _note = $"editing the {EditedLayer}, unsaved edits dropped";
        if (_layerLabel != null)
            _layerLabel.Text = LayerText();
        SyncRows();
        UpdateStatus();
        Log.Info("ui", $"cloud lab: {_chapter} now edits the {EditedLayer} {Describe()}");
    }

    /// <summary>Writes the standing tunes into the edited layer of the chapter's entry. Returns the path
    /// written, or null where Save is off or the file there is not a JSON object.</summary>
    public string? Save()
    {
        if (_clouds() is not { } clouds)
            return null;
        if (_savePath == null)
        {
            _note = "Save is off: an exported build carries the file inside the pck";
            UpdateStatus();
            return null;
        }
        FollowLayer();
        try
        {
            clouds.Tune = clouds.Tune with { DriftSpeed = clouds.Drift.Length(), Heading = _heading };
            CloudShadowChapters.Save(_savePath, _chapter, clouds.Tune, _editsClouds);
            clouds.Reload(CloudShadowChapters.Parse(System.IO.File.ReadAllText(_savePath, System.Text.Encoding.UTF8)));
        }
        catch (Exception ex) when (ex is System.IO.InvalidDataException or System.IO.IOException or UnauthorizedAccessException)
        {
            _note = $"not saved: {ex.Message}";
            Log.Warn("ui", $"cloud lab: {_note}");
            UpdateStatus();
            return null;
        }
        _saved = clouds.Saved;
        _note = $"saved {_chapter}'s {EditedLayer} to {_savePath}";
        Log.Info("ui", $"cloud lab: saved {_chapter}'s {EditedLayer} {Describe()} to {_savePath}");
        SyncRows();
        UpdateStatus();
        return _savePath;
    }

    /// <summary>PROTOTYPE (clouds-look): writes the standing look options that differ from the preset into
    /// the chapter's look block. Returns the path written, or null where nothing was saved.</summary>
    public string? SaveLook()
    {
        if (_savePath == null || _clouds() is not { } clouds || !VolumetricClouds.LookOn || VolumetricClouds.LookValue("od_step") == null)
        {
            // An empty save would drop the chapter's block, so one with no prototype slab writes nothing.
            _note = "look not saved: needs the source tree and the prototype look standing";
            UpdateStatus();
            return null;
        }
        var look = new Dictionary<string, float>();
        foreach (var r in LookRows)
        {
            // The temporal switch is the session's march mode, so no chapter saves it.
            if (r.Key != "temporal" && VolumetricClouds.LookValue(r.Key) is { } v
                && VolumetricClouds.LookValue(r.Key, 2) is { } preset && !Mathf.IsEqualApprox(v, preset))
            {
                look[r.Key] = v;
            }
        }
        try
        {
            CloudShadowChapters.SaveLook(_savePath, _chapter, look);
            clouds.Reload(CloudShadowChapters.Parse(System.IO.File.ReadAllText(_savePath, System.Text.Encoding.UTF8)));
        }
        catch (Exception ex) when (ex is System.IO.InvalidDataException or System.IO.IOException or UnauthorizedAccessException)
        {
            _note = $"look not saved: {ex.Message}";
            Log.Warn("ui", $"cloud lab: {_note}");
            UpdateStatus();
            return null;
        }
        // The saved values now build the slab, so the live edits over them go.
        VolumetricClouds.LabClear();
        _note = $"saved {_chapter}'s look ({look.Count} option(s) off the preset) to {_savePath}";
        Log.Info("ui", $"cloud lab: {_note}");
        SyncRows();
        UpdateStatus();
        return _savePath;
    }

    private static Label Small(string text, Color? colour = null)
    {
        var label = new Label { Text = text, Modulate = colour ?? new Color(1, 1, 1, 0.75f), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", 11);
        return label;
    }

    private static string VolumetricText() =>
        $"Volumetric Clouds: {VolumetricCloudsSetting.Lookup.Label(VolumetricCloudsSetting.Word)} (this flight, not saved)";

    private static string ValueText(Row row, CloudShadow c) =>
        $"{row.Field.Show(row.Get(c))}   (saved {row.Field.Show(row.Saved())})";

    private static string LookText() => $"Look: {(VolumetricClouds.LookOn ? "prototype" : "today's")} (Ctrl+Shift+V)";

    // A look slider reads and writes the slab itself, and its saved value is the preset's.
    private static Row LookRow((string Key, string Label, float Min, float Max, float Step) r) => new(
        new CloudShadowPattern.Field(r.Key, $"{r.Label}  [{r.Key}]", r.Min, r.Max, r.Step, "0.####", _ => null, (t, _) => t),
        _ => VolumetricClouds.LookValue(r.Key) ?? 0f,
        (_, v) => VolumetricClouds.LabSet(r.Key, v),
        () => VolumetricClouds.LookValue(r.Key, 1) ?? 0f);

    private string LayerText() => _editsClouds
        ? "Editing the clouds layer (applies while Volumetric Clouds is on)"
        : "Editing the base tune (Volumetric Clouds off)";

    private string Describe() => _clouds() is { } c
        ? string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{c.Tune.Describe()} drift=({c.Drift.X:0.#},{c.Drift.Y:0.#})m/s tint={(c.Tint != Vector3.Zero ? "on" : "off")} look=[{VolumetricClouds.LabSetText}]")
        : "no cloud shadows standing";

    // The saved heading, else the session's starting drift's, so a saved speed of 0 keeps a direction.
    private float SavedHeading() => _saved.Heading ?? CloudShadowPattern.HeadingOf(Authored());

    // The mission's own drift, before a saved speed or heading turns it.
    private Vector2 Authored() => _clouds()?.Authored ?? Vector2.Zero;

    // A row per tune. Speed and heading move the live drift, whose heading the lab holds itself, so a
    // speed taken through 0 keeps its direction.
    private List<Row> Rows()
    {
        var rows = new List<Row>();
        foreach (var f in CloudShadowPattern.Fields)
        {
            if (f == CloudShadowPattern.SpeedField)
            {
                rows.Add(new(f, c => c.Drift.Length(), (c, v) => c.Drift = CloudShadowPattern.Toward(_heading, v),
                    () => _saved.Drift(Authored()).Length()));
            }
            else if (f == CloudShadowPattern.HeadingField)
            {
                rows.Add(new(f, _ => _heading, (c, v) =>
                {
                    _heading = v;
                    c.Drift = CloudShadowPattern.Toward(v, c.Drift.Length());
                }, SavedHeading));
            }
            else
            {
                rows.Add(new(f, c => f.Get(c.Tune) ?? 0f, (c, v) => c.Tune = f.With(c.Tune, v), () => f.Get(_saved) ?? 0f));
            }
        }
        return rows;
    }

    private void BuildUi()
    {
        if (_clouds() is { } clouds)
        {
            clouds.FollowLayer();
            _editsClouds = clouds.CloudsLayer;
            _saved = clouds.Saved;
        }
        _heading = SavedHeading();
        _layer = new CanvasLayer { Layer = HudLayers.Lab, Visible = false };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        // Down the right edge, clear of the freecam readout above; the node lab owns the left.
        var panel = new PanelContainer { SelfModulate = new Color(1, 1, 1, 0.88f) };
        panel.SetAnchorsPreset(Control.LayoutPreset.RightWide);
        panel.OffsetLeft = -392;
        panel.OffsetRight = -8;
        panel.OffsetTop = 40;
        panel.OffsetBottom = -16;

        var margin = new MarginContainer();
        foreach (string side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 8);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        box.AddChild(new Label { Text = $"CLOUD LAB  cloud shadows  {_chapter}", Modulate = Amber });
        _layerLabel = Small(LayerText(), Amber);
        box.AddChild(_layerLabel);

        foreach (var row in Rows())
            AddRow(box, row);

        _tint = new CheckButton { Text = "Tint the shadows (red heads, blue deck)", FocusMode = Control.FocusModeEnum.None };
        _tint.Toggled += on =>
        {
            if (_syncing || _clouds() is not { } c)
                return;
            c.Tint = on ? TintLight : Vector3.Zero;
            Log.Info("ui", $"cloud lab: {Describe()}");
        };
        box.AddChild(_tint);
        if (SetVolumetric is { } setVolumetric)
        {
            // Off, quarter, half and full in turn, for comparing the march resolutions live.
            _volumetric = new Button { FocusMode = Control.FocusModeEnum.None };
            _volumetric.Pressed += () =>
            {
                var words = VolumetricCloudsSetting.Words;
                setVolumetric(words[(VolumetricCloudsSetting.Lookup.IndexOf(VolumetricCloudsSetting.Word) + 1) % words.Count]);
                _volumetric.Text = VolumetricText();
                Log.Info("ui", $"cloud lab: volumetric clouds {VolumetricCloudsSetting.Word} for this flight");
                FollowLayer();
            };
            box.AddChild(_volumetric);
        }

        var buttons = new HBoxContainer();
        var save = new Button { Text = $"Save for {_chapter}", FocusMode = Control.FocusModeEnum.None, Disabled = _savePath == null };
        save.Pressed += () => Save();
        buttons.AddChild(save);
        var revert = new Button { Text = "Revert to saved", FocusMode = Control.FocusModeEnum.None };
        revert.Pressed += RevertToSaved;
        buttons.AddChild(revert);
        box.AddChild(buttons);
        _status = Small("");
        box.AddChild(_status);
        box.AddChild(Small("Save writes the tunes and the drift's speed and heading into the edited layer for this chapter; the tint stays live only. RMB looks, the sliders take the left button · Shift+F4 hides this panel", Dim));

        margin.AddChild(box);
        panel.AddChild(margin);
        root.AddChild(panel);
        root.AddChild(LookPanel());
        _layer.AddChild(root);
        AddChild(_layer);
    }

    // PROTOTYPE (clouds-look): the look's options in a second column, left of the shadows'.
    private PanelContainer LookPanel()
    {
        var panel = new PanelContainer { SelfModulate = new Color(1, 1, 1, 0.88f) };
        panel.SetAnchorsPreset(Control.LayoutPreset.RightWide);
        panel.OffsetLeft = -792;
        panel.OffsetRight = -400;
        panel.OffsetTop = 40;
        panel.OffsetBottom = -16;
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 4);
        box.AddChild(new Label { Text = "CLOUD LOOK  volumetric clouds", Modulate = Amber });
        box.AddChild(Small($"Each edit applies at once. Save writes what differs from the preset into {_chapter}'s look block, Reset returns to the saved look. Copy puts the edits on the clipboard as a CSVM_CLOUD_SET string.", Dim));

        var buttons = new HBoxContainer();
        _look = new Button { Text = LookText(), FocusMode = Control.FocusModeEnum.None };
        _look.Pressed += () =>
        {
            VolumetricClouds.ToggleLook();
            SyncRows();
        };
        buttons.AddChild(_look);
        var save = new Button { Text = "Save", FocusMode = Control.FocusModeEnum.None, Disabled = _savePath == null };
        save.Pressed += () => SaveLook();
        buttons.AddChild(save);
        var reset = new Button { Text = "Reset", FocusMode = Control.FocusModeEnum.None };
        reset.Pressed += () =>
        {
            VolumetricClouds.LabClear();
            SyncRows();
        };
        buttons.AddChild(reset);
        var copy = new Button { Text = "Copy", FocusMode = Control.FocusModeEnum.None };
        copy.Pressed += () => DisplayServer.ClipboardSet(VolumetricClouds.LabSetText);
        buttons.AddChild(copy);
        box.AddChild(buttons);

        foreach (var row in LookRows)
            AddRow(box, LookRow(row));
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.AddChild(box);
        panel.AddChild(scroll);
        return panel;
    }

    private void AddRow(VBoxContainer box, Row row)
    {
        var head = new HBoxContainer();
        var name = Small(row.Field.Label);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.AutowrapMode = TextServer.AutowrapMode.Off;
        head.AddChild(name);
        var value = Small("");
        value.AutowrapMode = TextServer.AutowrapMode.Off;
        head.AddChild(value);
        box.AddChild(head);

        // No keyboard focus, so WASD keeps flying the camera after a click.
        var slider = new HSlider
        {
            MinValue = row.Field.Min,
            MaxValue = row.Field.Max,
            Step = row.Field.Step,
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        slider.ValueChanged += v =>
        {
            if (_syncing || _clouds() is not { } c)
                return;
            row.Set(c, (float)v);
            value.Text = ValueText(row, c);
        };
        slider.DragEnded += _ => Log.Info("ui", $"cloud lab: {Describe()}");
        box.AddChild(slider);
        _rows.Add((row, slider, value));
    }

    private void SyncRows()
    {
        if (_volumetric != null)
            _volumetric.Text = VolumetricText();
        if (_look != null)
            _look.Text = LookText();
        if (_clouds() is not { } c)
            return;
        _syncing = true;
        foreach (var (row, slider, value) in _rows)
        {
            slider.Value = row.Get(c);
            value.Text = ValueText(row, c);
        }
        if (_tint != null)
            _tint.ButtonPressed = c.Tint != Vector3.Zero;
        _syncing = false;
    }

    private void UpdateStatus()
    {
        if (_status == null)
            return;
        if (_clouds() is not { } c || !IsInstanceValid(c))
        {
            _status.Text = "no cloud shadows standing in this session";
            return;
        }
        string draws = c.Draws
            ? "drawing"
            : "not drawing (needs Enhanced, the Cloud Shadows option on, a cloudy mission and a day zone)";
        var offset = c.Offset();
        _status.Text = string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{draws} · drift from {c.Source}\npattern offset ({offset.X:0} m, {offset.Y:0} m) · clock {ShaderTime.Unwrapped:0.0} s")
            + (_note.Length > 0 ? $"\n{_note}" : "");
    }

    // One slider: its tune, how it reads and writes the shadows, and its saved value.
    private sealed record Row(CloudShadowPattern.Field Field, Func<CloudShadow, float> Get, Action<CloudShadow, float> Set,
        Func<float> Saved);
}
