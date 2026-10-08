using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Effects;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.UI.Labs;

/// <summary>
/// The ocean lab (Shift+F1), built in <c>--freecam</c> alone. A panel down the right edge holds a
/// slider per sea field (<see cref="SeaState"/>), grouped as the record groups them, with its value
/// and default. An edit reaches the standing ocean on release or a moment after a click, so a drag
/// compiles no shader per frame. Save writes this chapter's entry into the project's
/// <c>ocean_seas.json</c> from the source tree and is off in an exported build. The sliders take no
/// keyboard focus and the freecam looks on the right button, so flying and tuning do not fight.
/// </summary>
public sealed partial class OceanLab : Node
{
    // Seconds after a click or wheel step before the edit reaches the ocean; a drag applies on release.
    private const float ApplyDelay = 0.25f;

    private const float StatusPeriod = 0.5f;

    private static readonly Color Amber = new(1f, 0.93f, 0.35f);
    private static readonly Color Dim = new(0.62f, 0.62f, 0.68f);

    private readonly string _chapter;
    private readonly Func<Ocean?> _ocean;
    private readonly Action<SeaState> _apply;
    private readonly string? _savePath;
    private readonly List<(SeaState.Field Field, HSlider Slider, Label Value)> _rows = new();

    private SeaState _saved;
    private SeaState _edit;
    private CanvasLayer? _layer;
    private Label? _status;
    private bool _open;
    private bool _syncing;
    private float _pending = -1f;
    private float _statusTimer;
    private string _note = "";

    /// <summary>A lab over <paramref name="chapter"/>'s sea: <paramref name="saved"/> is the file's
    /// entry, <paramref name="start"/> what the ocean draws now. <paramref name="apply"/> hands an
    /// edit to the session; a null <paramref name="savePath"/> turns Save off.</summary>
    public OceanLab(string chapter, SeaState saved, SeaState start, Func<Ocean?> ocean, Action<SeaState> apply,
        string? savePath)
    {
        _chapter = chapter;
        _saved = saved;
        _edit = start;
        _ocean = ocean;
        _apply = apply;
        _savePath = savePath;
        Name = "ocean_lab";
    }

    /// <summary>Open the panel at launch, the <c>open</c> token of <c>--debug-ocean</c>.</summary>
    public bool StartOpen { get; init; }

    /// <summary>Whether the panel is showing. Nothing is built until it first opens.</summary>
    public bool IsOpen => _open;

    /// <summary>The sea the panel holds, as last applied or pending.</summary>
    public SeaState Edited => _edit;

    /// <summary>The chapter's entry as the file holds it, after any Save.</summary>
    public SeaState Saved => _saved;

    public override void _Ready()
    {
        if (StartOpen)
        {
            Toggle();
            return;
        }
        SetProcess(false);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F1, ShiftPressed: true })
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
        SetProcess(_open || _pending >= 0f);
        if (_open)
        {
            SyncRows();
            UpdateStatus();
        }
        Log.Info("ui", $"ocean lab {(_open ? "open" : "closed")} {_chapter} sea={_edit.Describe()}");
    }

    /// <summary>Sets one field by its key and applies at once, as a released slider does.</summary>
    public void Set(string key, float value)
    {
        var field = SeaState.Find(key) ?? throw new ArgumentException($"'{key}' is not a sea field", nameof(key));
        _edit = field.With(_edit, value);
        Commit();
    }

    /// <summary>Every field back to the ocean's tune, applied at once.</summary>
    public void ResetToDefaults()
    {
        _edit = SeaState.Default;
        _note = "reset to the defaults (not saved)";
        Commit();
    }

    /// <summary>Every field back to the file's entry, applied at once.</summary>
    public void RevertToSaved()
    {
        _edit = _saved;
        _note = "reverted to the saved sea";
        Commit();
    }

    /// <summary>Writes this chapter's entry into the source tree's file, the fields that differ from
    /// the defaults alone. Returns the path written, or null where Save is off or the write failed.</summary>
    public string? Save()
    {
        if (_savePath == null)
        {
            _note = "Save is off: an exported build carries its seas inside the pck";
            UpdateStatus();
            return null;
        }
        Commit();
        try
        {
            int fields = OceanSeas.Save(_savePath, _chapter, _edit);
            _saved = _edit;
            _note = $"saved {_chapter}, {fields} field(s) off the defaults, to {_savePath}";
            Log.Info("ui", $"ocean lab: saved {_chapter} sea={_edit.Describe()} to {_savePath}");
            UpdateStatus();
            return _savePath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _note = $"save failed: {ex.Message}";
            Log.Error("ui", $"ocean lab: could not write {_savePath}", ex);
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

    // Clamps the edit, hands it to the session and shows what the ocean now draws.
    private void Commit()
    {
        _pending = -1f;
        _edit = _edit.Clamped();
        _apply(_edit);
        SyncRows();
        UpdateStatus();
        Log.Info("ui", $"ocean lab: {_chapter} sea={_edit.Describe()}");
    }

    private void BuildUi()
    {
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
        box.AddChild(new Label { Text = $"OCEAN LAB  {_chapter}", Modulate = Amber });

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        var rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", 2);
        string group = "";
        foreach (var field in SeaState.Fields)
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
        buttons.AddChild(Btn("Reset to defaults", ResetToDefaults));
        buttons.AddChild(Btn("Revert to saved", RevertToSaved));
        var save = Btn($"Save for {_chapter}", () => Save());
        save.Disabled = _savePath == null;
        buttons.AddChild(save);
        box.AddChild(buttons);
        if (_savePath == null)
            box.AddChild(Small("Save is off in an exported build: its seas live inside the pck.", Dim));
        _status = Small("");
        box.AddChild(_status);
        box.AddChild(Small("RMB looks, the sliders take the left button · Shift+F1 hides this panel", Dim));

        margin.AddChild(box);
        panel.AddChild(margin);
        root.AddChild(panel);
        _layer.AddChild(root);
        AddChild(_layer);
    }

    private void AddRow(VBoxContainer rows, SeaState.Field field)
    {
        var head = new HBoxContainer();
        var name = Small(field.Label);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.AutowrapMode = TextServer.AutowrapMode.Off;
        head.AddChild(name);
        var value = Small("");
        value.AutowrapMode = TextServer.AutowrapMode.Off;
        head.AddChild(value);
        rows.AddChild(head);

        // No keyboard focus, so the arrow keys and WASD keep flying the camera after a click.
        var slider = new HSlider
        {
            MinValue = field.Min,
            MaxValue = field.Max,
            Step = field.Step,
            Value = field.Get(_edit),
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        slider.ValueChanged += v =>
        {
            if (_syncing)
                return;
            _edit = field.With(_edit, (float)v);
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
        _rows.Add((field, slider, value));
    }

    private string ValueText(SeaState.Field field) =>
        $"{field.Format(field.Get(_edit))}   (default {field.Format(field.Default)})";

    // The sliders and their readouts from the edit, which a clamp or a reset may have moved.
    private void SyncRows()
    {
        _syncing = true;
        foreach (var (field, slider, value) in _rows)
        {
            slider.Value = field.Get(_edit);
            value.Text = ValueText(field);
        }
        _syncing = false;
    }

    private void UpdateStatus()
    {
        if (_status == null)
            return;
        string ocean = _ocean() is { } o && IsInstanceValid(o) && o.IsInsideTree()
            ? "ocean standing"
            : "no ocean standing (Original, flat water or --no-ocean); it takes these values when built";
        string saved = _edit == _saved ? "matches the saved sea" : "unsaved changes";
        _status.Text = $"{ocean} · {saved}{(_note.Length > 0 ? $"\n{_note}" : "")}";
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
