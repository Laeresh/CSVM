using System.Collections.Generic;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// Flashes a flying aircraft's wingtip flares on the original's 1.5 s cycle (see
/// <see cref="WingLights"/>) and toggles a matching <see cref="OmniLight3D"/> per side
/// alongside them. PlaneBuilder builds the flare nodes hidden and hands them over; this
/// owns the lamps it adds as their children and toggles both together. The source flash
/// is a single frame; we widen it to a short visible window (TUNE) so the blink reads
/// crisply — and is catchable in a screenshot — at any frame rate. Below the session's
/// ANIMATION_LOD quality flag the flares/lights never come on, matching the def's
/// authored low-detail branch. Cheaper than an AnimationPlayer: a flat list of nodes
/// the flight loop advances (like <see cref="PropAnimator"/>).
/// </summary>
public sealed class WingLightBlinker
{
    // TUNE: the data turns the flares on for one frame; hold them on this long so the
    // blink is clearly visible without becoming a steady glow. Measured against the
    // original's own footage at ~1 frame @ 30 fps (~0.033 s) — which is 2 ticks of the
    // engine's fixed 60 Hz sim clock; nudged a hair past 2/60 s so a tick landing exactly
    // on the boundary isn't dropped to float round-off.
    private const double FlashDuration = (2.0 / 60.0) + 0.0001;

    // TUNE: the def authors range/colour only, no light intensity.
    private const float LightEnergy = 1.0f;

    private readonly List<(Node3D Flare, OmniLight3D Light)> _lamps;
    private readonly bool _lodOk;
    private double _t;

    private WingLightBlinker(List<(Node3D, OmniLight3D)> lamps, bool lodOk)
    {
        _lamps = lamps;
        _lodOk = lodOk;
    }

    public int Count => _lamps.Count;

    /// <summary>Null if the plane has no flare nodes (so callers can skip creating one).
    /// <paramref name="animLod"/> is the session's ANIMATION_LOD quality flag
    /// (<c>SessionSpec.AnimLod</c>) — below <see cref="AnimRuntime.HighLod"/> the flares
    /// and their lights stay off for the life of this instance, the def's authored
    /// else-branch.</summary>
    public static WingLightBlinker? Build(IReadOnlyList<Node3D> flares, int animLod)
    {
        if (flares.Count == 0)
            return null;
        var lamps = new List<(Node3D, OmniLight3D)>(flares.Count);
        foreach (var flare in flares)
        {
            // AT_NODE offset is (0,0,0) in the def — the light sits exactly at the flare.
            var light = new OmniLight3D
            {
                LightColor = WingLights.FlareColor,
                OmniRange = WingLights.FlareRangeMax,
                LightEnergy = LightEnergy,
                ShadowEnabled = false,
                Visible = false,
            };
            flare.AddChild(light);
            lamps.Add((flare, light));
        }
        return new WingLightBlinker(lamps, animLod >= AnimRuntime.HighLod);
    }

    /// <summary>Advances the blink phase and shows the flares + lights during the flash
    /// window. A no-op below the LOD gate: the flares stay off (left however
    /// <see cref="Reset"/> last set them).</summary>
    public void Advance(double delta)
    {
        if (!_lodOk)
            return;
        _t = (_t + delta) % WingLights.BlinkPeriod;
        bool on = _t < FlashDuration;
        foreach (var (flare, light) in _lamps)
        {
            flare.Visible = on;
            light.Visible = on;
        }
    }

    /// <summary>Restart the cycle with the flares and lights off (called on (re)spawn).</summary>
    public void Reset()
    {
        _t = 0;
        foreach (var (flare, light) in _lamps)
        {
            flare.Visible = false;
            light.Visible = false;
        }
    }
}
