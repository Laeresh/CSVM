using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CSVM.Mech3;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.UI.Labs;

/// <summary>
/// The vegetation lab (Shift+F2), built in <c>--freecam</c> alone, the ocean lab's twin for the
/// vegetation sidecar. A panel down the right edge holds the chapter's seed and a slider per field
/// (<see cref="VegetationKind.Fields"/>) of each listed kind. An edit reaches the session on release
/// or a moment after a click, so a drag rebuilds no clutter per frame. Save writes this chapter's
/// entry into the project's <c>vegetation.json</c> from the source tree and is off in an exported
/// build. The sliders take no keyboard focus and the freecam looks on the right button.
/// </summary>
public sealed partial class VegetationLab : Node
{
    // Seconds after a click or wheel step before the edit reaches the clutter; a drag applies on release.
    private const float ApplyDelay = 0.25f;

    // A seed slider's reach; any integer is a seed, and a larger saved one widens it.
    private const int SeedSpan = 999;

    private static readonly Color Amber = new(1f, 0.93f, 0.35f);
    private static readonly Color Dim = new(0.62f, 0.62f, 0.68f);

    private readonly string _chapter;
    private readonly Action<VegetationEntry> _apply;
    private readonly string? _savePath;
    private readonly List<(string Model, VegetationKind.Field Field, HSlider Slider, Label Value)> _rows = new();

    private VegetationEntry _saved;
    private VegetationEntry _edit;
    private CanvasLayer? _layer;
    private HSlider? _seedSlider;
    private Label? _seedValue;
    private Label? _status;
    private bool _open;
    private bool _syncing;
    private float _pending = -1f;
    private string _note = "";

    /// <summary>A lab over <paramref name="chapter"/>'s vegetation: <paramref name="saved"/> is the file's
    /// entry and the panel's start. <paramref name="apply"/> hands an edit to the session, which rebuilds
    /// the clutter from it. A null <paramref name="savePath"/> turns Save off.</summary>
    public VegetationLab(string chapter, VegetationEntry saved, Action<VegetationEntry> apply, string? savePath)
    {
        _chapter = chapter;
        _saved = saved;
        _edit = saved;
        _apply = apply;
        _savePath = savePath;
        Name = "vegetation_lab";
    }

    /// <summary>Whether the panel is showing. Nothing is built until it first opens.</summary>
    public bool IsOpen => _open;

    /// <summary>The entry the panel holds, as last applied or pending.</summary>
    public VegetationEntry Edited => _edit;

    /// <summary>The chapter's entry as the file holds it, after any Save.</summary>
    public VegetationEntry Saved => _saved;

    public override void _Ready() => SetProcess(false);

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F2, ShiftPressed: true })
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
        SetProcess(_pending >= 0f);
    }

    /// <summary>Shows or hides the panel, building it on the first open.</summary>
    public void Toggle()
    {
        if (_layer == null)
            BuildUi();
        _open = !_open;
        _layer!.Visible = _open;
        if (_open)
            SyncRows();
        Log.Info("ui", $"vegetation lab {(_open ? "open" : "closed")} {_chapter}, {_edit.Kinds.Count} kind(s), seed {_edit.Seed}");
    }

    /// <summary>Sets one kind's field by the model and the field's key and applies at once, as a
    /// released slider does.</summary>
    public void Set(string model, string key, float value)
    {
        var field = VegetationKind.Fields.FirstOrDefault(f => f.Key == key)
            ?? throw new ArgumentException($"'{key}' is not a vegetation field", nameof(key));
        _edit = _edit.With(model, field, value);
        Commit();
    }

    /// <summary>Sets the seed salt and applies at once.</summary>
    public void SetSeed(int seed)
    {
        _edit = _edit.WithSeed(seed);
        Commit();
    }

    /// <summary>Every value back to the file's entry, applied at once.</summary>
    public void RevertToSaved()
    {
        _edit = _saved;
        _note = "reverted to the saved entry";
        Commit();
    }

    /// <summary>Writes this chapter's entry into the source tree's file. Returns the path written, or
    /// null where Save is off, the file there is not readable JSON, or the write failed.</summary>
    public string? Save()
    {
        if (_savePath == null)
        {
            _note = "Save is off: an exported build carries its sidecar inside the pck";
            UpdateStatus();
            return null;
        }
        Commit();
        try
        {
            VegetationSidecar.Save(_savePath, _chapter, _edit);
            _saved = _edit;
            _note = $"saved {_chapter} to {_savePath}";
            Log.Info("ui", $"vegetation lab: saved {_chapter} to {_savePath}");
            UpdateStatus();
            return _savePath;
        }
        catch (InvalidDataException ex)
        {
            _note = $"save refused: {ex.Message}; fix or delete {_savePath} first";
            Log.Warn("ui", $"vegetation lab: left {_savePath} unwritten, {ex.Message}");
            UpdateStatus();
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _note = $"save failed: {ex.Message}";
            Log.Error("ui", $"vegetation lab: could not write {_savePath}", ex);
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

    private static string Format(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    // Hands the edit to the session and shows it.
    private void Commit()
    {
        _pending = -1f;
        _apply(_edit);
        SyncRows();
        Log.Info("ui", $"vegetation lab: {_chapter} edited, seed {_edit.Seed}");
    }

    private void Schedule()
    {
        _pending = ApplyDelay;
        SetProcess(true);
    }

    private void BuildUi()
    {
        _layer = new CanvasLayer { Layer = HudLayers.Lab, Visible = false };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        // Down the right edge as the ocean lab is; the node lab owns the left.
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
        box.AddChild(new Label { Text = $"VEGETATION LAB  {_chapter}", Modulate = Amber });

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        var rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", 2);
        AddSeedRow(rows);
        foreach (var kind in _edit.Kinds)
        {
            rows.AddChild(new Label { Text = kind.Model, Modulate = Amber });
            foreach (var field in VegetationKind.Fields)
                AddRow(rows, kind.Model, field);
        }
        if (_edit.Kinds.Count == 0)
            rows.AddChild(Small($"{_chapter} lists no vegetation kind in the sidecar.", Dim));
        scroll.AddChild(rows);
        box.AddChild(scroll);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 4);
        buttons.AddChild(Btn("Revert to saved", RevertToSaved));
        var save = Btn($"Save for {_chapter}", () => Save());
        save.Disabled = _savePath == null;
        buttons.AddChild(save);
        box.AddChild(buttons);
        if (_savePath == null)
            box.AddChild(Small("Save is off in an exported build: its sidecar lives inside the pck.", Dim));
        _status = Small("");
        box.AddChild(_status);
        box.AddChild(Small("RMB looks, the sliders take the left button · Shift+F2 hides this panel", Dim));

        margin.AddChild(box);
        panel.AddChild(margin);
        root.AddChild(panel);
        _layer.AddChild(root);
        AddChild(_layer);
    }

    private void AddSeedRow(VBoxContainer rows)
    {
        var head = new HBoxContainer();
        var name = Small("seed (rerolls the chapter's variation)");
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(name);
        var value = Small("");
        value.AutowrapMode = TextServer.AutowrapMode.Off;
        head.AddChild(value);
        rows.AddChild(head);
        _seedValue = value;
        var slider = new HSlider
        {
            MinValue = 0,
            MaxValue = Math.Max(SeedSpan, _saved.Seed),
            Step = 1,
            Value = _edit.Seed,
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        slider.ValueChanged += v =>
        {
            if (_syncing)
                return;
            _edit = _edit.WithSeed((int)v);
            value.Text = _edit.Seed.ToString(CultureInfo.InvariantCulture);
            Schedule();
        };
        slider.DragEnded += changed =>
        {
            if (changed)
                Commit();
        };
        rows.AddChild(slider);
        _seedSlider = slider;
    }

    private void AddRow(VBoxContainer rows, string model, VegetationKind.Field field)
    {
        var head = new HBoxContainer();
        var name = Small(field.Key);
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
            MinValue = 0,
            MaxValue = field.Max,
            Step = field.Max / 100f,
            Value = field.Get(_edit.Find(model)!),
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        slider.ValueChanged += v =>
        {
            if (_syncing)
                return;
            _edit = _edit.With(model, field, (float)v);
            value.Text = ValueText(model, field);
            Schedule();
        };
        slider.DragEnded += changed =>
        {
            if (changed)
                Commit();
        };
        rows.AddChild(slider);
        _rows.Add((model, field, slider, value));
    }

    private string ValueText(string model, VegetationKind.Field field) =>
        $"{Format(field.Get(_edit.Find(model)!))}   (saved {Format(_saved.Find(model) is { } k ? field.Get(k) : 0f)})";

    // The sliders and their readouts from the edit, which a revert may have moved.
    private void SyncRows()
    {
        _syncing = true;
        foreach (var (model, field, slider, value) in _rows)
        {
            slider.Value = field.Get(_edit.Find(model)!);
            value.Text = ValueText(model, field);
        }
        if (_seedSlider != null && _seedValue != null)
        {
            _seedSlider.Value = _edit.Seed;
            _seedValue.Text = $"{_edit.Seed.ToString(CultureInfo.InvariantCulture)}   (saved {_saved.Seed.ToString(CultureInfo.InvariantCulture)})";
        }
        _syncing = false;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_status == null)
            return;
        // Equal when Save would write the same text, so a slider dragged back reads as saved.
        bool same = VegetationSidecar.WithEntry(null, _chapter, _edit) == VegetationSidecar.WithEntry(null, _chapter, _saved);
        _status.Text = $"{(same ? "matches the saved entry" : "unsaved changes")}{(_note.Length > 0 ? $"\n{_note}" : "")}";
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
