using System;
using System.Collections.Generic;
using CrimsonSkies.Effects;
using Godot;

namespace CrimsonSkies.Flight;

/// <summary>
/// Visible damage on the flying aircraft (Run-2 item 10c), driven by the data's
/// thresholds: as a part's HP fraction crosses an entry of its 'injure_anims'
/// (see <see cref="DestroyablePart"/>), the named pdpanelN anim flips the
/// torn-skin panel — pdpN shown, its healthy pdpN_h twin (a real airframe
/// section like the Bloodhawk wingtip) hidden. The original's pdpanelN anims
/// only ACTIVE the pdpN node and layer it over the healthy skin by draw order;
/// hiding the _h twin as well is our equivalent without relying on coplanar
/// layering. The *_damage_green/yellow/red entries are the cockpit indicator's
/// texture cycle and stay unwired until a cockpit exists.
///
/// The def-level injure_anims add whole-plane effects: player_smoketrail at
/// 0.10 starts the dense_firetrail pair — a black-smoke trail (COLORS ramp,
/// born orange) plus a fire trail (fire_f01–06 flipbook) emitted per meter of
/// motion at the nose (see the trail support in <see cref="Puffer"/>).
/// player_fuelleak (0.85) is not wired yet. FlightController notifies
/// <see cref="OnPartDamage"/> after each hit, drives <see cref="Update"/> per
/// frame, and calls <see cref="Reset"/> on respawn.
/// </summary>
public sealed class DamageVisuals
{
    private readonly Dictionary<string, Node3D> _panels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DestroyablePart> _parts = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _applied = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(float Frac, string Anim)> _vehicleInjure;
    private readonly Puffer? _smokeTrail, _fireTrail;
    private readonly List<Puffer> _panelTrailPool;
    private readonly List<(Node3D Panel, Puffer Trail)> _panelTrails = new();
    private bool _smoking;

    private const float NoseOffset = 3.5f; // m ahead of center — the data emits the trail
                                           // at prop1 (the nose engine); TUNE per plane

    private const float StaticBurnSpeed = 15f; // m/s of virtual motion the damage lab's parked
                                               // plane spends on its in-place trails; TUNE

    /// <param name="panels">PlaneBuilder.DamagePanels — pdpN (hidden) + pdpN_h nodes.</param>
    /// <param name="smokeTrail">dense_firetrail's smokepuffer, trail-capable; optional.</param>
    /// <param name="fireTrail">dense_firetrail's firepuffer; optional.</param>
    /// <param name="panelTrails">a pool of firepuffer trail emitters, one assigned per
    /// flipped panel — the original streams a discrete-puff fire trail from every
    /// damaged panel (its pdpanelN anims call short_firetrail WITH_NODE pdpN; clearly
    /// visible in OriginalScreenshots/C1 IA1 Crash.mp4).</param>
    public DamageVisuals(IEnumerable<Node3D> panels, PlaneStats stats,
        Puffer? smokeTrail, Puffer? fireTrail, List<Puffer>? panelTrails = null)
    {
        foreach (var p in panels)
            _panels[p.Name] = p;
        foreach (var part in stats.DestroyableParts)
            _parts[part.Name] = part;
        _vehicleInjure = stats.VehicleInjureAnims;
        _smokeTrail = smokeTrail;
        _fireTrail = fireTrail;
        _panelTrailPool = panelTrails ?? new List<Puffer>();
    }

    public int PanelCount => _panels.Count;

    /// <summary>Applies every visual whose threshold the part's new HP fraction has
    /// crossed (fraction ≤ entry). Idempotent per anim name.</summary>
    public void OnPartDamage(string partName, float fraction)
    {
        if (_parts.TryGetValue(partName, out var def))
            foreach (var (frac, anim) in def.InjureAnims)
            {
                if (fraction > frac || !_applied.Add(anim))
                    continue;
                if (anim.StartsWith("pdpanel", StringComparison.OrdinalIgnoreCase))
                {
                    string n = anim["pdpanel".Length..];
                    if (_panels.TryGetValue("pdp" + n, out var torn))
                    {
                        torn.Visible = true;
                        GD.Print($"damage panel: {anim} on ({partName} {fraction * 100f:0}%)");
                        // the panel burns: a discrete-puff fire trail streams from it
                        if (_panelTrailPool.Count > _panelTrails.Count)
                            _panelTrails.Add((torn, _panelTrailPool[_panelTrails.Count]));
                    }
                    if (_panels.TryGetValue("pdp" + n + "_h", out var healthy))
                        healthy.Visible = false;
                }
                // else: *_damage_green/yellow/red cockpit indicator, *_damage_effects,
                // got-hit sparks — unwired (no cockpit / effect-call playback yet)
            }

        // whole-plane thresholds (any part qualifies — see PlaneStats.VehicleInjureAnims)
        foreach (var (frac, anim) in _vehicleInjure)
        {
            if (fraction > frac || !_applied.Add(anim))
                continue;
            if (anim.Equals("player_smoketrail", StringComparison.OrdinalIgnoreCase))
            {
                _smoking = true;
                GD.Print($"smoke trail: on ({partName} {fraction * 100f:0}%)");
            }
        }
    }

    /// <summary>Feeds the trail emitters their emit points; call each frame while
    /// flying (not crashed/paused).</summary>
    public void Update(Vector3 position, Basis attitude)
    {
        foreach (var (panel, trail) in _panelTrails)
            trail.TrailAdvance(panel.GlobalPosition);
        if (!_smoking)
            return;
        var nose = position + attitude * new Vector3(0f, 0f, -NoseOffset);
        _smokeTrail?.TrailAdvance(nose);
        _fireTrail?.TrailAdvance(nose);
    }

    /// <summary>Damage-lab drive (static viewer): the parked plane never moves, so the
    /// distance-interval trails would emit nothing — burn them in place instead, at
    /// <see cref="StaticBurnSpeed"/> of virtual motion per second (panel fires and,
    /// when smoking, the nose smoke/fire pair rise from the standing plane).</summary>
    public void UpdateStatic(float dt, Vector3 position, Basis attitude)
    {
        foreach (var (panel, trail) in _panelTrails)
            trail.TrailBurnAt(panel.GlobalPosition, dt, StaticBurnSpeed);
        if (!_smoking)
            return;
        var nose = position + attitude * new Vector3(0f, 0f, -NoseOffset);
        _smokeTrail?.TrailBurnAt(nose, dt, StaticBurnSpeed);
        _fireTrail?.TrailBurnAt(nose, dt, StaticBurnSpeed);
    }

    /// <summary>Back to pristine: torn panels hidden, healthy twins shown, trails
    /// cleared. Called on respawn.</summary>
    public void Reset()
    {
        foreach (var (name, node) in _panels)
            node.Visible = name.EndsWith("_h", StringComparison.OrdinalIgnoreCase);
        _applied.Clear();
        _smoking = false;
        _smokeTrail?.Clear();
        _fireTrail?.Clear();
        foreach (var (_, trail) in _panelTrails)
            trail.Clear();
        _panelTrails.Clear();
    }
}
