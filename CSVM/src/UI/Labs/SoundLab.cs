using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight.Audio;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;
using static System.FormattableString;

namespace CSVM.UI.Labs;

/// <summary>
/// The sound lab (Shift+F9), built over a flight under <c>--sound-lab</c> alone. A panel down the
/// right edge holds a slider per <see cref="EnhancedSoundTuning.Fields"/> amount and a pick of the
/// hit confirm's group. Every edit lands in <see cref="EnhancedSoundTuning.Current"/> at once, which
/// the flown aircraft's mix reads on its next frame. A readout shows the flown aircraft's throttle
/// and the engine slot's pitch and gain. Save writes <c>enhanced_sound.json</c> from the source tree,
/// or a sound pack's override while one plays (<see cref="EnhancedSoundTuning.SaveTargetFor"/>), and is off in an exported
/// build. Sliders and buttons take no keyboard focus, so flying goes on.
/// </summary>
public sealed partial class SoundLab : Node
{
    private const float ReadoutPeriod = 0.1f;

    // The sliders move in hundredths, which reach finer than an ear tells apart on any field.
    private const double SliderStep = 0.01;

    private static readonly Color Amber = new(1f, 0.93f, 0.35f);
    private static readonly Color Dim = new(0.62f, 0.62f, 0.68f);

    private readonly Func<Reading?> _reading;
    private readonly IReadOnlyList<string> _groups;
    private readonly Dictionary<string, (EnhancedSoundTuning.Field Field, HSlider Slider, Label Value)> _rows = new();

    private EnhancedSoundTuning _saved;
    private EnhancedSoundTuning _edit;
    private CanvasLayer? _layer;
    private OptionButton? _confirmSound;
    private Label? _readout;
    private Label? _status;
    private bool _open;
    private bool _syncing;
    private float _readoutTimer;
    private string _note = "";

    /// <summary>A lab over the running flight, starting from <paramref name="target"/>'s saved
    /// tuning and <paramref name="start"/>, what plays now. The readout comes from
    /// <paramref name="reading"/>, and the group pick lists <paramref name="groups"/>.</summary>
    public SoundLab(EnhancedSoundTuning.SaveTarget target, EnhancedSoundTuning start, Func<Reading?> reading, IReadOnlyList<string> groups)
    {
        _saved = target.Saved;
        _edit = start;
        _reading = reading;
        _groups = groups;
        SavePath = target.Path;
        Under = target.Under;
        SaveOffReason = target.OffReason ?? "it has no file to write";
        Name = "sound_lab";
    }

    /// <summary>The file Save writes, null where Save is off. A suite points it at a copy.</summary>
    public string? SavePath { get; internal set; }

    /// <summary>The tuning the saved file lies over: the shipped file's, when Save writes a sound
    /// pack's override. Null when Save writes the shipped file.</summary>
    public EnhancedSoundTuning? Under { get; }

    /// <summary>Why Save is off, shown when Save is pressed.</summary>
    public string SaveOffReason { get; }

    /// <summary>Whether the panel is showing. Nothing is built until it first opens.</summary>
    public bool IsOpen => _open;

    /// <summary>The tuning the panel holds, which is also what plays.</summary>
    public EnhancedSoundTuning Edited => _edit;

    /// <summary>The tuning as the file holds it, after any Save.</summary>
    public EnhancedSoundTuning Saved => _saved;

    /// <summary>The readout's text as last written, empty before the panel first opens.</summary>
    public string ReadoutText => _readout?.Text ?? "";

    public override void _Ready() => SetProcess(false);

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F9, ShiftPressed: true })
        {
            Toggle();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        _readoutTimer += (float)delta;
        if (_readoutTimer >= ReadoutPeriod)
        {
            _readoutTimer = 0f;
            UpdateReadout();
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
            UpdateReadout();
            UpdateStatus();
        }
        Log.Info("ui", $"sound lab {(_open ? "open" : "closed")}: {_edit.Describe()}");
    }

    /// <summary>The slider of the field keyed <paramref name="key"/>, built on the first open. A
    /// suite moves it as a drag does.</summary>
    public HSlider SliderFor(string key)
    {
        if (_layer == null)
            BuildUi();
        return _rows.TryGetValue(key, out var row) ? row.Slider
            : throw new ArgumentException($"'{key}' is not an Enhanced Sound field", nameof(key));
    }

    /// <summary>Sets the hit confirm's group by name and applies it at once.</summary>
    public void SetHitConfirmSound(string group)
    {
        Apply(_edit with { HitConfirmSound = group });
        SyncRows();
    }

    /// <summary>Every amount back to Original Sound's own value, applied at once.</summary>
    public void ResetToOriginal()
    {
        _note = "reset to Original Sound's values (not saved)";
        Apply(EnhancedSoundTuning.Neutral);
        SyncRows();
        Log.Info("ui", $"sound lab: reset, {_edit.Describe()}");
    }

    /// <summary>Every amount back to the file's, applied at once.</summary>
    public void RevertToSaved()
    {
        _note = "reverted to the saved values";
        Apply(_saved);
        SyncRows();
        Log.Info("ui", $"sound lab: reverted, {_edit.Describe()}");
    }

    /// <summary>Writes every amount into the source tree's file at its saved precision, which then
    /// also plays. Returns the path written, or null where Save is off, the file there is not
    /// readable JSON, or the write failed.</summary>
    public string? Save()
    {
        if (SavePath == null)
        {
            _note = $"Save is off: {SaveOffReason}";
            UpdateStatus();
            return null;
        }
        try
        {
            var written = EnhancedSoundTuning.Save(SavePath, _edit, Under);
            _saved = written;
            Apply(written);
            SyncRows();
            _note = $"saved to {SavePath}";
            Log.Info("ui", $"sound lab: saved {written.Describe()} to {SavePath}");
            UpdateStatus();
            return SavePath;
        }
        catch (InvalidDataException ex)
        {
            _note = $"save refused: {ex.Message}; fix or delete {SavePath} first";
            Log.Warn("ui", $"sound lab: left {SavePath} unwritten, {ex.Message}");
            UpdateStatus();
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _note = $"save failed: {ex.Message}";
            Log.Error("ui", $"sound lab: could not write {SavePath}", ex);
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

    private static string Format(float value) => Invariant($"{value:0.00#}");

    // The edit plays from here: the flown mix reads Current on its next frame.
    private void Apply(EnhancedSoundTuning tuning)
    {
        _edit = tuning;
        EnhancedSoundTuning.Current = tuning;
        UpdateStatus();
    }

    private void BuildUi()
    {
        _layer = new CanvasLayer { Layer = HudLayers.Lab, Visible = false };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        // Down the right edge: the flight HUD owns the top-left corner.
        var panel = new PanelContainer { SelfModulate = new Color(1, 1, 1, 0.88f) };
        panel.SetAnchorsPreset(Control.LayoutPreset.RightWide);
        panel.OffsetLeft = -352;
        panel.OffsetRight = -8;
        panel.OffsetTop = 40;
        panel.OffsetBottom = -16;

        var margin = new MarginContainer();
        foreach (string side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 8);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        box.AddChild(new Label { Text = "SOUND LAB  Enhanced Sound", Modulate = Amber });
        _readout = Small("", Amber);
        box.AddChild(_readout);

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        var rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", 2);
        foreach (var field in EnhancedSoundTuning.Fields)
            AddRow(rows, field);
        rows.AddChild(Small("Hit confirm sound"));
        _confirmSound = new OptionButton { FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _confirmSound.ItemSelected += index =>
        {
            if (!_syncing)
                SetHitConfirmSound(_confirmSound.GetItemText((int)index));
        };
        rows.AddChild(_confirmSound);
        scroll.AddChild(rows);
        box.AddChild(scroll);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 4);
        buttons.AddChild(Btn("Original values", ResetToOriginal));
        buttons.AddChild(Btn("Revert to saved", RevertToSaved));
        var save = Btn("Save", () => Save());
        save.Disabled = SavePath == null;
        buttons.AddChild(save);
        box.AddChild(buttons);
        _status = Small("");
        box.AddChild(_status);
        box.AddChild(Small("The values play under the AUDIO page's Enhanced Sound only · Shift+F9 hides this panel", Dim));

        margin.AddChild(box);
        panel.AddChild(margin);
        root.AddChild(panel);
        _layer.AddChild(root);
        AddChild(_layer);
        // Space is the trigger in flight, and a focused button would swallow it.
        PanelFocus.Strip(_layer, "sound lab: panel built");
    }

    private void AddRow(VBoxContainer rows, EnhancedSoundTuning.Field field)
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

        // The ends sit on the step's grid inside the read range, so every stop parses back.
        var slider = new HSlider
        {
            MinValue = Math.Ceiling(field.Min / SliderStep) * SliderStep,
            MaxValue = Math.Floor(field.Max / SliderStep) * SliderStep,
            Step = SliderStep,
            Value = field.Get(_edit),
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        slider.ValueChanged += v =>
        {
            if (_syncing)
                return;
            Apply(field.With(_edit, (float)v));
            value.Text = ValueText(field);
            UpdateReadout();
        };
        slider.DragEnded += changed =>
        {
            if (changed)
                Log.Info("ui", $"sound lab: {field.Key}={Format(field.Get(_edit))}");
        };
        rows.AddChild(slider);
        _rows[field.Key] = (field, slider, value);
    }

    private string ValueText(EnhancedSoundTuning.Field field) =>
        $"{Format(field.Get(_edit))}   (Original {Format(field.Default)})";

    // The sliders, readouts and group pick from the edit, which a reset or a revert may have moved.
    private void SyncRows()
    {
        if (_layer == null)
            return;
        _syncing = true;
        foreach (var (field, slider, value) in _rows.Values)
        {
            slider.Value = field.Get(_edit);
            value.Text = ValueText(field);
        }
        if (_confirmSound != null)
        {
            var names = _groups.Contains(_edit.HitConfirmSound) ? _groups : _groups.Prepend(_edit.HitConfirmSound).ToList();
            _confirmSound.Clear();
            foreach (string group in names)
                _confirmSound.AddItem(group);
            _confirmSound.Select(names.ToList().IndexOf(_edit.HitConfirmSound));
        }
        _syncing = false;
    }

    private void UpdateReadout()
    {
        if (_readout == null)
            return;
        // Original Sound's hit confirm reads EnhancedSoundTuning.WithoutPack, which a pack's override never reaches.
        string mode = SoundModeSetting.EnhancedOn ? "Enhanced Sound"
            : Extraction.HdSounds.Current.HasOverride ? "Original Sound, so no edit here plays: the hit confirm skips the pack's override"
            : "Original Sound, so the engine, bullet hit and canopy hit sliders do nothing";
        _readout.Text = _reading() is { } r
            ? Invariant($"throttle {r.Throttle:0.00}   engine pitch {r.Pitch:0.000}   gain {r.Gain:0.000}{(r.EngineSounding ? "" : " (silent)")}\n{mode}")
            : $"no flown aircraft\n{mode}";
    }

    private void UpdateStatus()
    {
        if (_status == null)
            return;
        string saved = _edit == _saved ? "matches the saved values" : "unsaved changes";
        _status.Text = $"{saved}{(_note.Length > 0 ? $"\n{_note}" : "")}";
    }

    private Button Btn(string text, Action pressed)
    {
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        b.Pressed += () =>
        {
            GetViewport().GuiReleaseFocus();
            pressed();
        };
        return b;
    }

    /// <summary>What the readout shows of the flown aircraft: its throttle lever and the pitch and
    /// linear gain its engine slot last wrote. The flag says whether the slot is sounding.</summary>
    public readonly record struct Reading(float Throttle, float Pitch, float Gain, bool EngineSounding);
}
