using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>One runtime's `LIGHT_STATE`/`LIGHT_ANIMATION` events: the live point-light table, the
/// signed-delta tween and the per-frame submission to <see cref="WorldLights"/>. `AnimRuntime`
/// keeps the `LIGHT_STATE`/`LIGHT_ANIMATION` case labels and applies `_opsApplied`/the unhandled
/// count itself, since both handlers here report whether they applied through their return value
/// rather than reaching for those two callbacks directly.</summary>
internal sealed class LightChannel
{
    // Keyed by (light name, anchor). The anchor identifies the *instance* of the definition,
    // and a definition's `lights` array is its own symbol table, so two refineries each get
    // their own orange_light. It cannot be keyed by host node the way puffers are: the flicker
    // events are partial updates carrying only {name, range}, with no AT_NODE to resolve from.
    private readonly Dictionary<(string Name, Node3D? Anchor), AnimLight> _lights = new();

    private readonly Func<WorldLights?> _worldLights;

    private readonly Func<string, AnimDefinition, Node3D?, Node3D?> _resolve;

    // The viewer positions lights budget against, with the single-camera fallback already folded
    // in: null or empty from LightViewerPositions means "no viewer seam wired", not "no viewers".
    private readonly Func<IReadOnlyList<Vector3>> _viewers;

    private readonly Func<bool> _debugMotions;

    // True when another runtime owns the set's frame (see WorldLights.AddSource).
    private readonly Func<bool> _contributesOnly;

    private Action<WorldLights>? _submit;

    public LightChannel(Func<WorldLights?> lights, Func<string, AnimDefinition, Node3D?, Node3D?> resolve,
        Func<IReadOnlyList<Vector3>> viewers, Func<bool> debugMotions, Func<bool> contributesOnly)
    {
        _worldLights = lights;
        _resolve = resolve;
        _viewers = viewers;
        _debugMotions = debugMotions;
        _contributesOnly = contributesOnly;
    }

    /// <summary>How many point lights this channel has declared, for the bootstrap census.
    /// </summary>
    internal int Count => _lights.Count;

    internal int ActiveCount => _lights.Values.Count(l => l.Active);

    internal IEnumerable<string> Names => _lights.Keys.Select(k => k.Name).Distinct();

    /// <summary>Every declared light's current state, for suites that pin an authored ramp.</summary>
    internal List<(string Name, bool Active, float RangeMin, float RangeMax, Color Color)> Snapshot() =>
        _lights.Select(kv => (kv.Key.Name, kv.Value.Active, kv.Value.RangeMin, kv.Value.RangeMax, kv.Value.Color))
            .ToList();

    // Applies one LIGHT_STATE. ⚠ Treat it as a PARTIAL update: apply every field only when present
    // and never default an absent one. A flicker is a stream of {name, range} events a few
    // hundredths of a second apart that must leave position, colour and active state untouched.
    internal void HandleLightState(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        if (ev.Data.Str("name") is not { } name)
            return;
        var key = (name, anchor);
        if (!_lights.TryGetValue(key, out var light))
            _lights[key] = light = new AnimLight { Host = anchor };

        // AT_NODE arrives as translate:{AtNode:{name, pos}}, node plus a local offset, the same
        // shape (and the same frame) as a puffer's AT_NODE.
        if (ev.Data.Obj("translate")?.Obj("AtNode") is { } at)
        {
            // ⚠ Resolve the host once per light, never per event. A flicker re-issues its full
            // LIGHT_STATE every loop iteration, and the full-world scan behind an unmemoized
            // Resolve cost tens of milliseconds a frame.
            if (at.Str("name") is { } hostName)
            {
                if (light.Host == null || !string.Equals(hostName, light.HostName, StringComparison.Ordinal))
                {
                    if (_resolve(hostName, def, anchor) is { } host)
                        light.Host = host;
                    light.HostName = hostName;
                }
            }
            light.Offset = at.Vec3("pos");
        }
        if (ev.Data.Obj("range") is { } range)
        {
            light.RangeMin = range.Num("min") ?? light.RangeMin;
            light.RangeMax = range.Num("max") ?? light.RangeMax;
        }
        if (ev.Data.Obj("color") is { } color)
            light.Color = new Color(color.Num("r") ?? 0f, color.Num("g") ?? 0f, color.Num("b") ?? 0f);
        if (ev.Data.Has("active_state"))
        {
            light.Active = ev.Data.Bool("active_state");
            light.TweenLeft = 0f; // switching a light re-arms it; a half-run pulse must not carry over
        }
    }

    // Applies one LIGHT_ANIMATION: signed deltas to a light's range and colour, ramped over
    // run_time. ⚠ They are deltas, never targets; a pulse authors a negative range on its way back,
    // which is not a value a light can hold. Under `instant` the delta lands whole, matching how
    // timed motions collapse to their end pose there.
    // Returns whether NAME named a light already declared by LIGHT_STATE; false is the router's
    // cue to count the event as unhandled, matching the original's inline Count() call.
    internal bool HandleLightAnimation(AnimEvent ev, Node3D? anchor, bool instant)
    {
        if (ev.Data.Str("name") is not { } name
            || !_lights.TryGetValue((name, anchor), out var light))
        {
            return false;
        }
        var range = ev.Data.Obj("range");
        var color = ev.Data.Obj("color");
        float dMin = range?.Num("min") ?? 0f, dMax = range?.Num("max") ?? 0f;
        // Alpha 0: a delta carries no alpha, and Color's default 1 would accumulate into it.
        var dColor = new Color(color?.Num("r") ?? 0f, color?.Num("g") ?? 0f, color?.Num("b") ?? 0f, 0f);
        float runTime = ev.Data.Num("run_time") ?? 0f;

        if (instant || runTime <= 0f)
        {
            light.RangeMin += dMin;
            light.RangeMax += dMax;
            light.Color += dColor;
            light.TweenLeft = 0f;
        }
        else
        {
            light.MinRate = dMin / runTime;
            light.MaxRate = dMax / runTime;
            light.ColorRate = dColor / runTime;
            light.TweenLeft = runTime;
        }
        return true;
    }

    // Advances light tweens and, as the set's owner, submits every active light and commits the
    // frame. Per frame, because hosts move (a muzzle flash rides its turret). A contributor only
    // tweens here and registers Submit, which the owner's Commit calls.
    internal void Tick(float dt)
    {
        if (_worldLights() is not { } lights)
            return;
        foreach (var light in _lights.Values)
        {
            if (light.TweenLeft > 0f)
            {
                float step = Mathf.Min(dt, light.TweenLeft);
                light.RangeMin += light.MinRate * step;
                light.RangeMax += light.MaxRate * step;
                light.Color += light.ColorRate * step;
                light.TweenLeft -= step;
            }
        }
        if (_contributesOnly())
        {
            lights.AddSource(_submit ??= Submit);
            return;
        }
        lights.Begin();
        Submit(lights);
        lights.Commit(_viewers());
        if (_debugMotions())
            lights.LogOnce();
    }

    /// <summary>The crash rig's respawn: forgets every declared light, matching
    /// <see cref="SoundChannel.Reset"/>'s disposition for sound emitters.</summary>
    internal void Reset() => _lights.Clear();

    /// <summary>The light half of `TearDownResourcesOf`: every light this anchor's instance
    /// declared is forgotten, keyed by anchor the same way sound emitters are.</summary>
    internal void DiscardFor(Node3D? anchor)
    {
        foreach (var key in _lights.Keys.Where(k => k.Anchor == anchor).ToList())
            _lights.Remove(key);
    }

    // Every active light at its host's current world pose.
    private void Submit(WorldLights lights)
    {
        foreach (var light in _lights.Values)
        {
            if (!light.Active || light.RangeMax <= 0f)
                continue;
            // ⚠ A light inside a deactivated subtree is off. A building's destroyed variant must
            // not keep lighting the ground through its healthy twin, and a retired burst copy
            // must not keep lighting its last site.
            if (light.Host is not { } host || !GodotObject.IsInstanceValid(host) || !host.IsVisibleInTree())
                continue;
            lights.Add(host.GlobalTransform * light.Offset, light.Color, light.RangeMin, light.RangeMax);
        }
    }
}
