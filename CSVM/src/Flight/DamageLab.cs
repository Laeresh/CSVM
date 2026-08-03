using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// What the lab's sliders actually drive. The parked viewer has no HP model — its sliders
/// ARE the damage state, feeding <see cref="DamageVisuals"/> directly. In flight the aircraft's
/// own <see cref="PlaneDamage"/> is the state, so the sliders write it and read back from it.
/// </summary>
public interface IDamageLabTarget
{
    /// <summary>Suffix naming which aircraft the panel is bound to ("P1"), or null when there
    /// is only one thing it could mean.</summary>
    string? Subtitle { get; }

    /// <summary>The model's own fraction for a part, which the sliders mirror — null from a
    /// target that holds no state of its own (then the sliders are the only truth).</summary>
    float? Fraction(string part);

    /// <summary>Writes the slider state through. <paramref name="rebuildVisuals"/> is set only
    /// when the crossed-threshold set changed, so a mid-band drag doesn't restart the fires.</summary>
    void Apply(IReadOnlyDictionary<string, float> fractions, bool rebuildVisuals);

    /// <summary>Per-frame work the target owns.</summary>
    void Tick(float dt);
}

/// <summary>The parked plane (--viewer): the sliders are the state and the visuals are the whole
/// effect. The trails burn in place at a virtual speed because a parked plane travels no distance
/// (see DamageVisuals.UpdateStatic), on sim time — so a halted clock freezes them for a still capture.
/// </summary>
public sealed class ViewerDamageTarget : IDamageLabTarget
{
    private readonly DamageVisuals _visuals;
    private readonly Node3D _plane;

    public ViewerDamageTarget(DamageVisuals visuals, Node3D plane)
    {
        _visuals = visuals;
        _plane = plane;
    }

    public string? Subtitle => null;

    public float? Fraction(string part) => null;

    public void Apply(IReadOnlyDictionary<string, float> fractions, bool rebuildVisuals)
    {
        if (!rebuildVisuals)
            return;
        _visuals.Reset();
        foreach (var (name, frac) in fractions)
            _visuals.OnPartDamage(name, frac);
    }

    public void Tick(float dt) =>
        _visuals.UpdateStatic(dt, _plane.GlobalPosition, _plane.GlobalTransform.Basis);
}

/// <summary>The flown aircraft (--fly/--stunt): the sliders write the real per-part HP, so the
/// HUD's DMG line, the damaged-engine mix and the gauge dial all follow — and a critical part at
/// 0 leaves the plane one hit from down, exactly as a graze would. PlaneDamage only spends HP
/// (the data has no repair), so an absolute slider state is expressed as Reset + spend.
/// Nothing to tick: FlightController drives the visuals with the plane's live pose every frame,
/// and UpdateStatic would fight it.</summary>
public sealed class FlightDamageTarget : IDamageLabTarget
{
    private readonly FlightController _controller;

    public FlightDamageTarget(FlightController controller, string? subtitle)
    {
        _controller = controller;
        Subtitle = subtitle;
    }

    public string? Subtitle { get; }

    public float? Fraction(string part) =>
        _controller.Damage is { } damage && damage.Parts.TryGetValue(part, out var state)
            ? state.Fraction
            : null;

    public void Apply(IReadOnlyDictionary<string, float> fractions, bool rebuildVisuals)
    {
        if (_controller.Damage is not { } damage)
            return;
        damage.Reset();
        foreach (var (name, frac) in fractions)
            if (damage.Parts.TryGetValue(name, out var state))
                damage.Apply(name, (1f - frac) * state.Def.MaxHp);
        if (rebuildVisuals && _controller.Visuals is { } visuals)
        {
            visuals.Reset();
            foreach (var (name, frac) in fractions)
                visuals.OnPartDamage(name, frac);
        }
    }

    public void Tick(float dt)
    {
    }
}

/// <summary>
/// The damage lab: one HP slider per destroyable part (vehicle.json destroyable_parts)
/// driving the <see cref="DamageVisuals"/> pipeline through an <see cref="IDamageLabTarget"/>.
/// Dragging a part's HP below an injure_anims threshold plays the visual — the pdpanelN
/// torn-skin flip with its panel fire — and ≤ 10 % on any part starts the nose dense_firetrail
/// smoke/fire pair. Raising a slider back above a threshold restores the healthy skin: the
/// visuals are rebuilt from scratch whenever the set of crossed thresholds changes (rebuilding
/// only on set changes keeps a slider drag from restarting the fires at every pixel of travel).
///
/// Two hosts, one panel. In --viewer the sliders ARE the damage state on a parked plane
/// (ViewerDamageTarget). In --fly/--stunt they write P1's real PlaneDamage
/// (FlightDamageTarget) while the sim keeps running, so a dialled-in state can be flown, and
/// they mirror it back — a graze moves the sliders, and a respawn returns them to 100 %.
///
/// F5 toggles the lab — panel AND gauges together, so it is genuinely present or absent
/// (clean F12 shots). Every viewer and flight session builds one: with --damage it opens
/// straight away, otherwise it waits hidden behind F5, which is what makes F5 mean something in
/// a plain launch (previously the lab only existed when --damage was passed, so the key
/// silently did nothing — user-reported). --damage=part:frac,… presets the sliders, so
/// --screenshot runs capture damage states deterministically in either mode.
/// </summary>
public sealed partial class DamageLab : Node
{
    private readonly PlaneStats _stats;
    private readonly IDamageLabTarget _target;
    private readonly IReadOnlyList<(string Part, float Frac)> _preset;
    private readonly GaugeCluster? _gauges;

    private readonly Dictionary<string, HSlider> _sliders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Label> _readouts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> _lastFractions = new(StringComparer.OrdinalIgnoreCase);
    private CanvasLayer _ui = null!;
    private CanvasLayer? _gaugeLayer;
    private HashSet<string> _applied = new(StringComparer.OrdinalIgnoreCase);
    private bool _gaugesWanted = true; // the panel's HUD-gauges checkbox, remembered across F5
    private int _dragging; // sliders under the mouse right now — the read-back leaves those alone

    public DamageLab(PlaneStats stats, IDamageLabTarget target,
        IReadOnlyList<(string Part, float Frac)>? preset = null, GaugeCluster? gauges = null)
    {
        _stats = stats;
        _target = target;
        _preset = preset ?? Array.Empty<(string, float)>();
        _gauges = gauges;
        Name = "damage_lab";
    }

    /// <summary>Build the lab but keep it out of sight until F5. Set for a plain launch
    /// (no --damage), so the lab is always THERE to toggle while an unadorned viewer or
    /// flight screenshot stays byte-identical to one with no lab at all.</summary>
    public bool StartHidden { get; init; }

    /// <summary>Hang the panel off the right edge instead of the left. Set in flight, where the
    /// top-left corner is the HUD's own text block — SPD/ALT/THR plus the stall, impact, DMG,
    /// paused and crashed lines, which grow to about six and would print straight through the
    /// sliders. The right edge is clear above the dials.</summary>
    public bool RightAligned { get; init; }

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
        if (StartHidden)
            SetLabVisible(false);
    }

    /// <summary>Mirrors whatever the target's own model says before letting it tick, on sim time —
    /// so a halted clock freezes the parked plane's fires for a still capture.</summary>
    public override void _Process(double delta)
    {
        SyncFromTarget();
        _target.Tick(GameClock.Current?.FrameDt ?? (float)delta);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F5 })
        {
            SetLabVisible(!_ui.Visible);
            GetViewport().SetInputAsHandled();
        }
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

    /// <summary>Pulls the target's own damage state back into the sliders — in flight that is
    /// every hit taken while the panel is up, and the respawn that repairs them. Sliders under
    /// the mouse are left alone: a drag that fights the read-back is unusable. The visuals are
    /// NOT re-derived here; whoever moved the model (FlightController's hit path) already did.
    /// </summary>
    private void SyncFromTarget()
    {
        if (_dragging > 0)
            return;
        bool moved = false;
        foreach (var (name, slider) in _sliders)
        {
            if (_target.Fraction(name) is not float frac)
                continue;
            double want = Math.Round(frac * 100.0);
            if (Math.Abs(slider.Value - want) < 0.5)
                continue;
            slider.SetValueNoSignal(want); // no Reapply — this IS the model's state
            moved = true;
        }
        if (!moved)
            return;
        var fractions = ReadSliders();
        UpdateReadouts(fractions);
        foreach (var (name, frac) in fractions)
            _lastFractions[name] = frac; // a live hit already blinked the dial; don't blink twice
        _applied = TargetAnims(fractions);
    }

    /// <summary>Shows or hides the whole lab — slider panel and HUD gauges together. H is
    /// "is the damage lab here", not "is one of its two layers here"; the panel's own
    /// checkbox still controls the gauges independently while the lab is up, and its state
    /// is remembered across a hide/show.</summary>
    private void SetLabVisible(bool on)
    {
        _ui.Visible = on;
        if (_gaugeLayer != null)
            _gaugeLayer.Visible = on && _gaugesWanted;
    }

    private void BuildUi()
    {
        _ui = new CanvasLayer { Layer = 1 };
        var panel = new PanelContainer
        {
            Position = new Vector2(8, 8),
            SelfModulate = new Color(1, 1, 1, 0.85f),
        };
        if (RightAligned)
        {
            // Anchored rather than positioned: the panel's width is content-derived (the part
            // names and threshold lines differ per plane), so only the right edge is knowable here.
            panel.AnchorLeft = panel.AnchorRight = 1f;
            panel.GrowHorizontal = Control.GrowDirection.Begin;
            panel.OffsetRight = -8;
            panel.OffsetTop = 8;
        }
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 10);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 3);

        box.AddChild(new Label
        {
            Text = $"DAMAGE LAB — {_stats.DefName}" +
                   (_target.Subtitle is { } who ? $"  ·  {who}" : ""),
        });
        box.AddChild(Small("drag a part's HP over its thresholds · F5 hides this panel"));

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
            slider.DragStarted += () => _dragging++;
            slider.DragEnded += _ => _dragging--;
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
            hud.Toggled += on => { _gaugesWanted = on; _gaugeLayer.Visible = on && _ui.Visible; };
            box.AddChild(hud);
        }

        margin.AddChild(box);
        panel.AddChild(margin);
        _ui.AddChild(panel);
        AddChild(_ui);
    }

    private Dictionary<string, float> ReadSliders()
    {
        var fractions = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, slider) in _sliders)
            fractions[name] = (float)(slider.Value / 100.0);
        return fractions;
    }

    private void UpdateReadouts(Dictionary<string, float> fractions)
    {
        foreach (var part in _stats.DestroyableParts)
            if (_readouts.TryGetValue(part.Name, out var readout))
                readout.Text = $"{part.Name}  {fractions[part.Name] * 100:0}%  " +
                               $"({fractions[part.Name] * part.MaxHp:0.#}/{part.MaxHp:0} hp)";
    }

    /// <summary>Writes the sliders through to the target. The visual rebuild (Reset +
    /// re-crossing every threshold) only runs when the set of crossed anims actually
    /// changed — mid-band drags just move HP and update the readouts.</summary>
    private void Reapply()
    {
        var fractions = ReadSliders();

        // a slider going DOWN = the part "took damage" — start its gauge blink,
        // exactly like a flight hit (repairs don't blink)
        foreach (var (name, frac) in fractions)
        {
            if (_lastFractions.TryGetValue(name, out float prev) && frac < prev)
                _gauges?.OnPartDamage(name);
            _lastFractions[name] = frac;
        }

        UpdateReadouts(fractions);

        var target = TargetAnims(fractions);
        bool rebuild = !target.SetEquals(_applied);
        _applied = target;
        _target.Apply(fractions, rebuild);
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
