using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CrimsonSkies.Flight;

/// <summary>
/// The static plane viewer's damage lab (--plane + --damage, no --fly; a Run-2
/// item-10 tuning aid): one HP slider per destroyable part (vehicle.json
/// destroyable_parts) driving the same <see cref="DamageVisuals"/> pipeline the
/// flight build uses. Dragging a part's HP below an injure_anims threshold plays
/// the visual — the pdpanelN torn-skin flip, with its panel fire burning in
/// place (the parked plane doesn't move, so the distance-interval trails are
/// spent at a virtual burn speed; see DamageVisuals.UpdateStatic) — and ≤ 10 %
/// on any part starts the nose dense_firetrail smoke/fire pair. Raising a
/// slider back above a threshold restores the healthy skin: the visuals are
/// rebuilt from scratch whenever the set of crossed thresholds changes (the
/// lab's undo, which flight never needs; rebuilding only on set changes keeps a
/// slider drag from restarting the fires at every pixel of travel).
///
/// H toggles the slider panel (clean F12 shots). --damage=part:frac,… presets
/// the sliders, so --screenshot runs capture damage states deterministically.
/// </summary>
public sealed partial class DamageLab : Node
{
    private readonly PlaneStats _stats;
    private readonly DamageVisuals _visuals;
    private readonly Node3D _plane;
    private readonly IReadOnlyList<(string Part, float Frac)> _preset;
    private readonly GaugeCluster? _gauges;

    private readonly Dictionary<string, HSlider> _sliders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Label> _readouts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> _lastFractions = new(StringComparer.OrdinalIgnoreCase);
    private CanvasLayer _ui = null!;
    private CanvasLayer? _gaugeLayer;
    private HashSet<string> _applied = new(StringComparer.OrdinalIgnoreCase);

    public DamageLab(PlaneStats stats, DamageVisuals visuals, Node3D plane,
        IReadOnlyList<(string Part, float Frac)>? preset = null, GaugeCluster? gauges = null)
    {
        _stats = stats;
        _visuals = visuals;
        _plane = plane;
        _preset = preset ?? Array.Empty<(string, float)>();
        _gauges = gauges;
        Name = "damage_lab";
    }

    public override void _Ready()
    {
        // The HUD gauge cluster (user request): the damage dial mirrors the sliders —
        // colors from the fractions, the 5 s post-hit blink from slider decreases.
        // Altimeter/speedometer draw at rest (no flight data). Toggled from the panel.
        if (_gauges != null)
        {
            _gauges.PartFraction = name =>
                _sliders.TryGetValue(name, out var s) ? (float)(s.Value / 100.0) : 1f;
            _gaugeLayer = new CanvasLayer { Layer = 0 };
            _gaugeLayer.AddChild(_gauges);
            AddChild(_gaugeLayer);
        }
        BuildUi();
        foreach (var (part, frac) in _preset)
            if (_sliders.TryGetValue(part, out var slider))
                slider.Value = frac * 100.0; // fires ValueChanged → Reapply
            else
                GD.Print($"damage lab: --damage names unknown part '{part}'");
        // presets are an initial state, not fresh hits — cancel the blink their
        // slider moves triggered so --screenshot damage shots stay deterministic
        _gauges?.Reset();
        Reapply(); // sync the readouts even when no preset moved a slider
    }

    /// <summary>Burns the assigned trails in place at the parked plane.</summary>
    public override void _Process(double delta) =>
        _visuals.UpdateStatic((float)delta, _plane.GlobalPosition, _plane.GlobalTransform.Basis);

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.H })
            _ui.Visible = !_ui.Visible;
    }

    private void BuildUi()
    {
        _ui = new CanvasLayer { Layer = 1 };
        var panel = new PanelContainer
        {
            Position = new Vector2(8, 8),
            SelfModulate = new Color(1, 1, 1, 0.85f),
        };
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 10);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 3);

        box.AddChild(new Label { Text = $"DAMAGE LAB — {_stats.DefName}" });
        box.AddChild(Small("drag a part's HP over its thresholds · H hides this panel"));

        foreach (var part in _stats.DestroyableParts)
        {
            var readout = new Label();
            _readouts[part.Name] = readout;
            box.AddChild(readout);

            var slider = new HSlider
            {
                MinValue = 0,
                MaxValue = 100,
                Step = 1,
                Value = 100,
                TickCount = 11,
                TicksOnBorders = true,
                CustomMinimumSize = new Vector2(250, 0),
            };
            slider.ValueChanged += _ => Reapply();
            _sliders[part.Name] = slider;
            box.AddChild(slider);

            // The part's thresholds, high→low: pN = the wired pdpanelN torn-skin flip,
            // green/yellow/red = the unwired cockpit-indicator cycle (listed for context).
            if (part.InjureAnims.Count > 0)
                box.AddChild(Small(string.Join(" · ",
                    part.InjureAnims.Select(e => $"{e.Frac * 100:0} {ShortAnim(part.Name, e.Anim)}"))));
        }
        if (_stats.VehicleInjureAnims.Count > 0)
            box.AddChild(Small("any part: " + string.Join(" · ",
                _stats.VehicleInjureAnims.Select(e => $"{e.Frac * 100:0} {e.Anim}"))));

        var repair = new Button { Text = "repair all" };
        repair.Pressed += () =>
        {
            foreach (var slider in _sliders.Values)
                slider.Value = 100;
        };
        box.AddChild(repair);

        if (_gaugeLayer != null)
        {
            var hud = new CheckButton { Text = "HUD gauges", ButtonPressed = true };
            hud.Toggled += on => _gaugeLayer.Visible = on;
            box.AddChild(hud);
        }

        margin.AddChild(box);
        panel.AddChild(margin);
        _ui.AddChild(panel);
        AddChild(_ui);
    }

    /// <summary>Threshold-line shorthand: pdpanel4 → p4 (the wired torn-skin flip),
    /// leftwing_damage_yellow → yellow (the cockpit-indicator cycle).</summary>
    private static string ShortAnim(string part, string anim)
    {
        if (anim.StartsWith("pdpanel", StringComparison.OrdinalIgnoreCase))
            return "p" + anim["pdpanel".Length..];
        var prefix = part + "_damage_";
        return anim.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? anim[prefix.Length..] : anim;
    }

    private static Label Small(string text)
    {
        var label = new Label { Text = text, Modulate = new Color(1, 1, 1, 0.65f) };
        label.AddThemeFontSizeOverride("font_size", 11);
        return label;
    }

    /// <summary>Re-derives the damage visuals from the sliders. The full rebuild
    /// (Reset + re-crossing every threshold) only runs when the set of crossed
    /// anims actually changed — mid-band drags just update the readouts.</summary>
    private void Reapply()
    {
        var fractions = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, slider) in _sliders)
            fractions[name] = (float)(slider.Value / 100.0);

        // a slider going DOWN = the part "took damage" — start its gauge blink,
        // exactly like a flight hit (repairs don't blink)
        foreach (var (name, frac) in fractions)
        {
            if (_lastFractions.TryGetValue(name, out float prev) && frac < prev)
                _gauges?.OnPartDamage(name);
            _lastFractions[name] = frac;
        }

        foreach (var part in _stats.DestroyableParts)
            if (_readouts.TryGetValue(part.Name, out var readout))
                readout.Text = $"{part.Name}  {fractions[part.Name] * 100:0}%  " +
                               $"({fractions[part.Name] * part.MaxHp:0.#}/{part.MaxHp:0} hp)";

        var target = TargetAnims(fractions);
        if (target.SetEquals(_applied))
            return;
        _applied = target;
        _visuals.Reset();
        foreach (var (name, frac) in fractions)
            _visuals.OnPartDamage(name, frac);
    }

    /// <summary>The anim set the current fractions demand — mirrors DamageVisuals'
    /// crossing rule (an entry applies when the fraction ≤ its threshold; the
    /// def-level anims read the worst part).</summary>
    private HashSet<string> TargetAnims(Dictionary<string, float> fractions)
    {
        var target = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in _stats.DestroyableParts)
            if (fractions.TryGetValue(part.Name, out float frac))
                foreach (var (thresh, anim) in part.InjureAnims)
                    if (frac <= thresh)
                        target.Add(anim);
        if (fractions.Count > 0)
        {
            float worst = fractions.Values.Min();
            foreach (var (thresh, anim) in _stats.VehicleInjureAnims)
                if (worst <= thresh)
                    target.Add(anim);
        }
        return target;
    }
}
