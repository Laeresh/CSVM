using Godot;

namespace CSVM.Flight;

/// <summary>Everything the engine slot's curves read this frame. The original's per-frame routine
/// takes the throttle lever plus two things off the airframe's own state, so this carries all three
/// rather than letting each audio path re-derive them.</summary>
/// <param name="Throttle">The lever, [0..1], which the curves' own control points are in terms of.</param>
/// <param name="TurnRate">rad/s about everything EXCEPT the nose axis.</param>
/// <param name="ClimbAttitude">The original's <c>a</c>: negative climbing, positive diving.</param>
/// <param name="Boosting">⚠ Always false today; CSVM has no nitro system to set it.</param>
public readonly record struct EngineDrive(
    float Throttle, float TurnRate, float ClimbAttitude, bool Boosting = false);

/// <summary>
/// The engine-audio slot maths every aircraft shares: which definition sits on the engine slot,
/// and what pitch and gain the two loops take this frame. The original runs one per-frame function
/// for the player and for every AI vehicle, forking only on the distance cull and on the pitch
/// multiplier, so the player's <see cref="FlightAudio"/> and the positional
/// <see cref="AiEngineAudio"/> read their numbers from here rather than each keeping a copy.
/// The curves themselves are player.json globals carried on <see cref="PlaneStats"/>; the
/// definition names are the airframe's own vehicle.json keys (docs/formats/vehicle.md).
/// </summary>
public static class EngineAudioCurves
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

    // The two added terms and the clamp they sit under, all read out of the image rather than tuned
    // (docs/formats/vehicle.md carries each address). Volume and pitch differ only in the turn-rate
    // coefficient; the climb term is the same 0.15 on both.
    // ⚠ TurnRateIntoVolume is INERT on the shipped data and is not a dead constant to delete: the
    // engine volume curve is flat 1.0, so the remap multiplies the parameter it moves by a zero span.
    private const float TurnRateIntoVolume = 0.26f;
    private const float TurnRateIntoPitch = 0.25f;
    private const float ClimbAttitudeIntoParam = 0.15f;

    // The headroom above a curve's own 1.0 is authored, not slack: it is what the overshoot at a
    // hard pull and the boost pins below both live in.
    private const float ParamMax = 1.5f;

    // Boost REPLACES both parameters rather than adding to them.
    private const float BoostVolumeParam = 1.17f;
    private const float BoostPitchParam = 1.25f;

    /// <summary>The engine slot's definition and its pitch multiplier: damaged swaps onto
    /// <c>damaged_engine_sound</c> with a drawn multiplier; else <paramref name="cockpitView"/>
    /// (own-ship only, the full Cockpit view — the original leaves the Nose view on the plain def,
    /// confirmed at the controls of the original) swaps onto <c>cockpit_engine_sound</c> at
    /// multiplier 1; else the plain <c>engine_sound</c>. Precedence is a port decision — no def
    /// authors a damaged cockpit variant, so damage keeps the more important cue.</summary>
    public static (string Name, float PitchMul) EngineDefFor(
        PlaneStats stats, bool damaged, RandomNumberGenerator rng, bool cockpitView = false)
    {
        if (damaged && stats.DamagedEngineSound is { } damagedName)
        {
            float mul = stats.DamagedEnginePitchRandom
                ? stats.DamagedEnginePitchLo
                  + ((stats.DamagedEnginePitchHi - stats.DamagedEnginePitchLo) * rng.Randf())
                : 1f;
            return (damagedName, mul);
        }
        if (cockpitView && stats.CockpitEngineSound is { } cockpitName)
        {
            return (cockpitName, 1f);
        }
        return (stats.EngineSound, 1f);
    }

    /// <summary>The two quantities the engine slot reads off the airframe, in the original's own
    /// terms. Written once here because both are frame-convention traps: ours is nose −Z where the
    /// original's row 2 is −nose, so the climb term needs no negation and looks wrong.</summary>
    internal static EngineDrive DriveFrom(FlightModel model, bool boosting = false) => new(
        model.Throttle,
        // ⚠ Pitch and yaw ONLY. The original squares the two orientation rows perpendicular to the
        // nose and drops the nose-axis one, so a roll does not raise the note however fast it goes.
        Mathf.Sqrt((model.BodyRates.X * model.BodyRates.X) + (model.BodyRates.Y * model.BodyRates.Y)),
        // The original reads orientation row 2's Y, and row 2 is −nose; our nose is −Attitude.Z, so
        // its `a` IS Attitude.Z.Y with no sign flip. Negative climbing, as its own decode says.
        model.Attitude.Z.Y,
        boosting);

    /// <summary>Slot 0, the engine loop. Each curve's normalised parameter is the throttle lever
    /// plus a turn-rate term and a climb-attitude term, clamped to [0, 1.5] BEFORE the curve maps it
    /// to an output (docs/formats/vehicle.md, "The engine slot's pitch and gain are not throttle
    /// alone"); the pitch then carries the damaged-swap multiplier. The returned volume is the curve
    /// alone — the caller still applies the definition's own VOLUME, its ramp and any mix gain.</summary>
    internal static (float Pitch, float Volume) Engine(PlaneStats stats, in EngineDrive drive, float pitchMul)
    {
        float tVol = drive.Boosting ? BoostVolumeParam
            : Mathf.Clamp(stats.EngineVolume.Frac(drive.Throttle) + (TurnRateIntoVolume * drive.TurnRate)
                - (ClimbAttitudeIntoParam * drive.ClimbAttitude), 0f, ParamMax);
        float tPitch = drive.Boosting ? BoostPitchParam
            : Mathf.Clamp(stats.EnginePitch.Frac(drive.Throttle) + (TurnRateIntoPitch * drive.TurnRate)
                - (ClimbAttitudeIntoParam * drive.ClimbAttitude), 0f, ParamMax);
        return (Mathf.Max(MinPitch, stats.EnginePitch.Remap(tPitch) * pitchMul),
                stats.EngineVolume.Remap(tVol));
    }

    /// <summary>Slot 1, the overspeed whine: both curves run on speed / fd_speed, so the loop is
    /// silent below fd_speed and only opens up in a dive. No pitch multiplier reaches this slot.
    /// ⚠ No shipped airframe names a definition for it (see <see cref="PlaneStats.WhineSound"/>), so
    /// this is reached only if one ever does.</summary>
    internal static (float Pitch, float Volume) Whine(PlaneStats stats, float speedFrac) =>
        (Mathf.Max(MinPitch, stats.WhinePitch.Eval(speedFrac)), stats.WhineVolume.Eval(speedFrac));
}
