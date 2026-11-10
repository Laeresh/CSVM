using System;
using System.Collections.Generic;
using CSVM.Effects;
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

    private string LayerText() => _editsClouds
        ? "Editing the clouds layer (applies while Volumetric Clouds is on)"
        : "Editing the base tune (Volumetric Clouds off)";

    private string Describe() => _clouds() is { } c
        ? string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{c.Tune.Describe()} drift=({c.Drift.X:0.#},{c.Drift.Y:0.#})m/s tint={(c.Tint != Vector3.Zero ? "on" : "off")}")
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
        _layer.AddChild(root);
        AddChild(_layer);
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
