using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// The throttle-slam exhaust smoke (`CAP-21` re-read): a large, sudden throttle INCREASE streams
/// dark trail smoke from the engine's exhaust nodes for a few seconds; a single small step, a
/// sustained high setting, and any throttle decrease all show nothing. The shape is
/// <c>plane_props.json</c>'s <c>nitro_boost</c> puffers (<c>nitropuff1..4</c>, AT_NODE
/// <c>exhaust1..4</c>) reused WITHOUT the rest of that def — no <c>nitropropN</c> discs, no
/// <c>snd_nitrostart</c>, no nitro spin — since the capture shows the same trail-smoke shape on a
/// plain throttle jump with no boost involved.
///
/// <para>The gate is edge-triggered on a continuous rise: <see cref="Update"/> tracks the throttle
/// value at the start of the current unbroken climb and fires once per climb, the instant the
/// cumulative rise crosses <see cref="SlamThreshold"/> — never again for the same climb (however
/// far it continues), and never on a flat or falling throttle. A climb resets the moment the
/// throttle stops rising, so tapping up one notch at a time (each tap separated by a flat/falling
/// frame) is evaluated fresh every time and never accumulates across taps.</para>
/// </summary>
public sealed class ThrottleSlamSmoke
{
    // TUNE: the capture only bounds this between the two jump sizes it filmed — idle→5/8 (0.625)
    // fires, a single 1/8 (0.125) step does not, and 2/8-4/8 is unobserved. The smallest threshold
    // consistent with both endpoints is anything just over 0.125; 0.25 (a two-notch jump) is the
    // smallest round number that clears it.
    private const float SlamThreshold = 0.25f;

    // The capture's plume is visible ≤0.5 s after the jump (onset here is 0 — the gate fires the
    // trigger frame itself) and gone by ~2.5-3 wall-s after the idle→8/8 slam (t≈11.7 s to a clean
    // t≈14.5 s sheet). Sim-seconds = wall-seconds × 1.390 (DET-11 — the wall figure runs 39% fast).
    private const float DurationSimSeconds = 2.8f * 1.390f;

    private const float RiseEpsilon = 1e-5f;

    private readonly List<(Node3D Node, Puffer Trail)> _exhausts;

    private float _lastThrottle;
    private float _riseStartThrottle;
    private bool _rising;
    private bool _firedThisRise;
    private float _smokeRemaining;

    private ThrottleSlamSmoke(List<(Node3D, Puffer)> exhausts, float throttle)
    {
        _exhausts = exhausts;
        _lastThrottle = _riseStartThrottle = throttle;
    }

    /// <summary>Resolves the plane's <c>exhaust1..4</c> marker nodes and builds a trail puffer per
    /// one found from <c>nitro_boost</c>'s own <c>nitropuffN</c> definition — the same authored
    /// puffer the boost uses, just driven independently of it. Null when the plane carries no
    /// exhaust nodes or the reader/textures are unavailable (every call site null-checks).</summary>
    public static ThrottleSlamSmoke? Build(Node3D planeRoot, string zrdrPath, TextureArchive textures,
        Node parent, float initialThrottle)
    {
        var exhausts = new List<(Node3D, Puffer)>();
        for (int i = 1; i <= 4; i++)
        {
            if (FindNode(planeRoot, $"exhaust{i}") is not { } node)
                continue;
            if (Puffer.MakePuffer(zrdrPath, textures, parent, "plane_props.json", $"nitropuff{i}") is { } trail)
                exhausts.Add((node, trail));
        }
        return exhausts.Count > 0 ? new ThrottleSlamSmoke(exhausts, initialThrottle) : null;
    }

    /// <summary>Per-frame drive: feed the live throttle (0-1) every frame flight is running. Not
    /// called while crashed/paused — see <see cref="Reset"/> for what a crash/respawn needs
    /// instead.</summary>
    public void Update(float dt, float throttle)
    {
        if (throttle > _lastThrottle + RiseEpsilon)
        {
            if (!_rising)
            {
                _rising = true;
                _riseStartThrottle = _lastThrottle;
                _firedThisRise = false;
            }
            if (!_firedThisRise && throttle - _riseStartThrottle >= SlamThreshold)
            {
                _firedThisRise = true;
                _smokeRemaining = DurationSimSeconds;
                Log.Info("flight", $"throttle slam: {_riseStartThrottle:0.000} -> {throttle:0.000} (Delta {throttle - _riseStartThrottle:0.000}) - exhaust smoke for {DurationSimSeconds:0.00} sim-s");
            }
        }
        else
        {
            // Flat or falling: the current climb (if any) is over, and a fresh one starts fresh —
            // never fires on a decrease, and never carries a rise's magnitude across a pause.
            _rising = false;
        }
        _lastThrottle = throttle;

        if (_smokeRemaining > 0f)
        {
            _smokeRemaining -= dt;
            foreach (var (node, trail) in _exhausts)
                trail.TrailAdvance(node.GlobalPosition);
            if (_smokeRemaining <= 0f)
                foreach (var (_, trail) in _exhausts)
                    trail.TrailEnd();
        }
    }

    /// <summary>Crash and respawn both call this: stops any in-progress plume outright (a wreck or
    /// a freshly spawned aircraft shows none) and re-anchors the climb tracker on the given
    /// throttle so the respawn's own throttle assignment is never read as a rise.</summary>
    public void Reset(float throttle)
    {
        _lastThrottle = _riseStartThrottle = throttle;
        _rising = false;
        _firedThisRise = false;
        if (_smokeRemaining > 0f)
        {
            _smokeRemaining = 0f;
            foreach (var (_, trail) in _exhausts)
                trail.Clear();
        }
    }

    private static Node3D? FindNode(Node root, string name)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Node3D n3d && n3d.HasMeta(AnimRuntime.NameMeta)
                && n3d.GetMeta(AnimRuntime.NameMeta).AsString().Equals(name, System.StringComparison.OrdinalIgnoreCase))
            {
                return n3d;
            }
            if (FindNode(child, name) is { } found)
                return found;
        }
        return null;
    }
}
