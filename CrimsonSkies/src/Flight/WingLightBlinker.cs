using System.Collections.Generic;
using CrimsonSkies.Mech3;
using Godot;

namespace CrimsonSkies.Flight;

/// <summary>
/// Flashes a flying aircraft's wingtip flares on the original's 1.5 s cycle (see
/// <see cref="WingLights"/>). PlaneBuilder builds the flare nodes hidden and hands them
/// over; this toggles their visibility. The source flash is a single frame; we widen it
/// to a short visible window (TUNE) so the blink reads crisply — and is catchable in a
/// screenshot — at any frame rate. Cheaper than an AnimationPlayer: a flat list of nodes
/// the flight loop advances (like <see cref="PropAnimator"/>).
/// </summary>
public sealed class WingLightBlinker
{
    private readonly List<Node3D> _flares;
    private double _t;

    // TUNE: the data turns the flares on for one frame; hold them on this long so the
    // blink is clearly visible without becoming a steady glow.
    private const double FlashDuration = 0.08;

    private WingLightBlinker(List<Node3D> flares) => _flares = flares;

    public int Count => _flares.Count;

    /// <summary>Null if the plane has no flare nodes (so callers can skip creating one).</summary>
    public static WingLightBlinker? Build(IReadOnlyList<Node3D> flares) =>
        flares.Count > 0 ? new WingLightBlinker(new List<Node3D>(flares)) : null;

    /// <summary>Advances the blink phase and shows the flares during the flash window.</summary>
    public void Advance(double delta)
    {
        _t = (_t + delta) % WingLights.BlinkPeriod;
        bool on = _t < FlashDuration;
        foreach (var f in _flares)
            f.Visible = on;
    }

    /// <summary>Restart the cycle with the flares off (called on (re)spawn).</summary>
    public void Reset()
    {
        _t = 0;
        foreach (var f in _flares)
            f.Visible = false;
    }
}
