using Godot;

namespace CSVM.Flight;

/// <summary>
/// The engine-audio slot maths every aircraft shares: which definition sits on the engine slot,
/// and what pitch and gain the two loops take this frame. The original runs one per-frame function
/// for the player and for every AI vehicle, forking only on the distance cull and on the pitch
/// multiplier, so the player's <see cref="FlightAudio"/> and the positional
/// <see cref="AiEngineAudio"/> read their numbers from here rather than each keeping a copy.
/// The curves themselves are player.json globals carried on <see cref="PlaneStats"/>; the
/// definition names are the airframe's own vehicle.json keys (docs/formats/vehicle.md).
/// </summary>
internal static class EngineAudioCurves
{
    /// <summary>Distance from the listener past which an AI aircraft's engine and whine are both
    /// stopped, and inside which they start again: 2000 world units, the square root of the
    /// executable's own `4000000.0`. Own-ship audio is never culled, so only
    /// <see cref="AiEngineAudio"/> reads this and the squared form below.</summary>
    internal const float CullDistance = 2000f;

    /// <summary>The comparison form: the cull test is on squared distance, as the original's is.</summary>
    internal const float CullDistanceSq = CullDistance * CullDistance;

    // The mixer clamps the played frequency rather than letting a multiplier reach zero; Godot has
    // no such floor, and a PitchScale of 0 stalls the stream instead of bottoming out.
    private const float MinPitch = 0.01f;

    /// <summary>The engine slot's definition and its pitch multiplier. A damaged airframe swaps the
    /// slot onto <c>damaged_engine_sound</c> and draws the multiplier once, so it holds for as long
    /// as the swap does; an undamaged one runs its own <c>engine_sound</c> at multiplier 1.</summary>
    internal static (string Name, float PitchMul) EngineDefFor(
        PlaneStats stats, bool damaged, RandomNumberGenerator rng)
    {
        if (!damaged || stats.DamagedEngineSound is not { } damagedName)
        {
            return (stats.EngineSound, 1f);
        }
        float mul = stats.DamagedEnginePitchRandom
            ? stats.DamagedEnginePitchLo
              + ((stats.DamagedEnginePitchHi - stats.DamagedEnginePitchLo) * rng.Randf())
            : 1f;
        return (damagedName, mul);
    }

    /// <summary>Slot 0, the engine loop: both curves run on throttle, and the pitch carries the
    /// damaged-swap multiplier. The returned volume is the curve alone — the caller still applies
    /// the definition's own VOLUME, its start ramp and any own-ship mix gain.
    /// ⚠ Throttle is only the original's FIRST term. It also adds a turn-rate and a climb-attitude
    /// term to each curve's normalised parameter (docs/formats/vehicle.md, "The engine slot's pitch
    /// and gain are not throttle alone"), which this does not yet carry.</summary>
    internal static (float Pitch, float Volume) Engine(PlaneStats stats, float throttle, float pitchMul) =>
        (Mathf.Max(MinPitch, stats.EnginePitch.Eval(throttle) * pitchMul),
         stats.EngineVolume.Eval(throttle));

    /// <summary>Slot 1, the overspeed whine: both curves run on speed / fd_speed, so the loop is
    /// silent below fd_speed and only opens up in a dive. No pitch multiplier reaches this slot.
    /// ⚠ No shipped airframe names a definition for it (see <see cref="PlaneStats.WhineSound"/>), so
    /// this is reached only if one ever does.</summary>
    internal static (float Pitch, float Volume) Whine(PlaneStats stats, float speedFrac) =>
        (Mathf.Max(MinPitch, stats.WhinePitch.Eval(speedFrac)), stats.WhineVolume.Eval(speedFrac));
}
