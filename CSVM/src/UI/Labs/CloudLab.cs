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
/// heading. A tint lights the shadows red, so the pattern and its drift show plainly. Every
/// edit only writes globals, so it applies at once. Save writes the chapter's tunes and the drift's
/// speed and heading into <see cref="CloudShadowChapters"/>; the tint is never saved.
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
    private Vector2 _startDrift;
    private CloudShadowPattern.Tune _saved = CloudShadowPattern.Tune.Default;
    private string _note = "";
    private CanvasLayer? _layer;
    private CheckButton? _tint;
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
            SyncRows();
            UpdateStatus();
        }
        Log.Info("ui", $"cloud lab {(_open ? "open" : "closed")} {Describe()}");
    }

    /// <summary>Every tune back to the chapter's saved entry, the drift to the session's start, the tint off.</summary>
    public void RevertToSaved()
    {
        if (_clouds() is not { } clouds)
            return;
        clouds.Tunes = _saved;
        clouds.Drift = _saved.Drift(_startDrift);
        clouds.Tint = Vector3.Zero;
        SyncRows();
        Log.Info("ui", $"cloud lab: reverted {Describe()}");
    }

    /// <summary>Writes the standing tunes as the chapter's entry. Returns the path written, or null where
    /// Save is off or the file there is not a JSON object.</summary>
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
        try
        {
            clouds.Tunes = clouds.Tunes with { DriftSpeed = clouds.Drift.Length(), Heading = CloudShadowPattern.HeadingOf(clouds.Drift) };
            CloudShadowChapters.Save(_savePath, _chapter, clouds.Tunes);
        }
        catch (Exception ex) when (ex is System.IO.InvalidDataException or System.IO.IOException or UnauthorizedAccessException)
        {
            _note = $"not saved: {ex.Message}";
            Log.Warn("ui", $"cloud lab: {_note}");
            UpdateStatus();
            return null;
        }
        _saved = clouds.Tunes.Clamped();
        _note = $"saved {_chapter} to {_savePath}";
        Log.Info("ui", $"cloud lab: saved {_chapter} {Describe()} to {_savePath}");
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

    private static string ValueText(Row row, CloudShadow c) =>
        $"{row.Get(c).ToString(row.Format)}   (saved {row.Default().ToString(row.Format)})";

    private string Describe() => _clouds() is { } c
        ? string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"darkness={c.Tunes.Darkness:0.##} ambient={c.Tunes.Ambient:0.##} size={c.Tunes.PatternSize:0}m coverage={c.Tunes.Coverage:0.##} softness={c.Tunes.Softness:0.###} drift=({c.Drift.X:0.#},{c.Drift.Y:0.#})m/s tint={(c.Tint != Vector3.Zero ? "on" : "off")}")
        : "no cloud shadows standing";

    private List<Row> Rows()
    {
        var start = _startDrift;
        return new List<Row>
        {
            new("Darkness (sun taken under a core)", 0f, 1f, 0.01f, c => c.Tunes.Darkness,
                (c, v) => c.Tunes = c.Tunes with { Darkness = v }, () => _saved.Darkness, "0.00"),
            new("Size (one shadow, m)", 50f, 3000f, 10f, c => c.Tunes.PatternSize,
                (c, v) => c.Tunes = c.Tunes with { PatternSize = v }, () => _saved.PatternSize, "0"),
            new("Coverage (share of ground shaded)", 0f, 1f, 0.01f, c => c.Tunes.Coverage,
                (c, v) => c.Tunes = c.Tunes with { Coverage = v }, () => _saved.Coverage, "0.00"),
            new("Softness (edge width)", 0.005f, 0.4f, 0.005f, c => c.Tunes.Softness,
                (c, v) => c.Tunes = c.Tunes with { Softness = v }, () => _saved.Softness, "0.000"),
            new("Ambient taken (ambient light under a core)", 0f, 1f, 0.01f, c => c.Tunes.Ambient,
                (c, v) => c.Tunes = c.Tunes with { Ambient = v }, () => _saved.Ambient, "0.00"),
            new("Drift speed (m/s, saved)", 0f, 100f, 0.5f, c => c.Drift.Length(),
                (c, v) => c.Drift = CloudShadowPattern.Toward(CloudShadowPattern.HeadingOf(c.Drift), v),
                () => _saved.Drift(start).Length(), "0.0"),
            new("Drift heading (deg from +X toward +Z, as the sea's wind)", 0f, 359f, 1f,
                c => CloudShadowPattern.HeadingOf(c.Drift), (c, v) => c.Drift = CloudShadowPattern.Toward(v, c.Drift.Length()),
                () => CloudShadowPattern.HeadingOf(_saved.Drift(start)), "0"),
        };
    }

    private void BuildUi()
    {
        _startDrift = _clouds()?.Drift ?? Vector2.Zero;
        _saved = _clouds()?.Tunes.Clamped() ?? CloudShadowPattern.Tune.Default;
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

        foreach (var row in Rows())
            AddRow(box, row);

        _tint = new CheckButton { Text = "Tint the shadows red", FocusMode = Control.FocusModeEnum.None };
        _tint.Toggled += on =>
        {
            if (_syncing || _clouds() is not { } c)
                return;
            c.Tint = on ? TintLight : Vector3.Zero;
            Log.Info("ui", $"cloud lab: {Describe()}");
        };
        box.AddChild(_tint);

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
        box.AddChild(Small("Save writes the tunes and the drift's speed and heading for this chapter; the tint stays live only. RMB looks, the sliders take the left button · Shift+F4 hides this panel", Dim));

        margin.AddChild(box);
        panel.AddChild(margin);
        root.AddChild(panel);
        _layer.AddChild(root);
        AddChild(_layer);
    }

    private void AddRow(VBoxContainer box, Row row)
    {
        var head = new HBoxContainer();
        var name = Small(row.Label);
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
            MinValue = row.Min,
            MaxValue = row.Max,
            Step = row.Step,
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
        _status.Text = $"{draws} · drift from {c.Source}\n"
            + $"pattern offset ({offset.X:0} m, {offset.Y:0} m) · clock {ShaderTime.Unwrapped:0.0} s"
            + (_note.Length > 0 ? $"\n{_note}" : "");
    }

    // One slider: its label, range, step, how it reads and writes the shadows, and its unit.
    private sealed record Row(string Label, float Min, float Max, float Step, Func<CloudShadow, float> Get,
        Action<CloudShadow, float> Set, Func<float> Default, string Format);
}
