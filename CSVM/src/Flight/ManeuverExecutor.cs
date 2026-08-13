using System;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>Plays one maneuver's timed step program as a sequence of <see cref="FlightInput"/>
/// values — the maneuver counterpart of <c>FlightController.NextHoldInput</c>'s scripted
/// segments, shaped like <see cref="AiPilot"/> so wave D's state machine consumes it the same
/// way (<c>Next(model, dt)</c> each sim step until <see cref="Done"/>).
///
/// <para><b>Step values are target attitudes in degrees, not stick deflections or rates</b>
/// (docs/formats/ai-rosters.md): magnitudes run to 180, the loop/immelman/split_s sequences are
/// the Euler waypoints of those maneuvers, and a rate reading would make every zero-duration
/// step a no-op. Targets compose onto the entry frame — the level entry-heading frame normally,
/// the full entry attitude for a <c>relative</c> maneuver — as yaw·pitch·roll. A positive-yaw
/// step turns LEFT here (matching <see cref="FlightInput"/>'s sign); whether the original
/// mirrors a maneuver left/right at selection time is D11's question, not this class's.</para>
///
/// <para>A step with a positive duration is held for that many sim seconds; a zero-duration
/// step advances when the attitude is within <see cref="StepToleranceDeg"/> — or after
/// <see cref="ZeroDurationTimeoutS"/>, an invented safety net so a target the placeholder
/// control law cannot reach (near-vertical Euler poles) skips instead of wedging the program.
/// The tracking law itself (proportional body-frame attitude error with rate damping) is a
/// placeholder in <see cref="AiPilot"/>'s style, not original behaviour.</para>
///
/// <para>Pure over the model state and its own fields — no clocks, no node reads, no
/// randomness — so a fixed-dt run is deterministic (<c>ManeuverExecutorTests</c>).</para></summary>
public sealed class ManeuverExecutor
{
    /// <summary>"Attitude reached" for a zero-duration step, degrees of total rotation error.</summary>
    public const float StepToleranceDeg = 15f;

    /// <summary>Invented safety net: a zero-duration step advances after this long even when
    /// its attitude was never captured. Not an original value.</summary>
    public const float ZeroDurationTimeoutS = 6f;

    /// <summary>Throttle flown for the whole program. Mutable like <see cref="AiPilot"/>'s
    /// orders; the shipped data carries no per-step throttle.</summary>
    public float Throttle = 1f;

    // Placeholder tracking gains (AiPilot's style): stick per radian of body-frame attitude
    // error, with anticipated body rate subtracted so the capture does not overshoot.
    private const float Gain = 1.5f;
    private const float RateLead = 0.35f;

    private Basis _reference = Basis.Identity;
    private bool _begun;
    private float _stepElapsed;

    public ManeuverExecutor(Maneuver maneuver)
    {
        if (maneuver.IsStub)
            throw new ArgumentException($"'{maneuver.Name}' is a stub (no steps) and cannot be flown");
        Maneuver = maneuver;
    }

    public Maneuver Maneuver { get; }

    /// <summary>The step currently being flown; == <c>Maneuver.Steps.Count</c> once done.</summary>
    public int StepIndex { get; private set; }

    /// <summary>True once every step has run. <see cref="Next"/> then holds level in the entry
    /// frame; the owner (D11's state machine) is expected to switch input sources.</summary>
    public bool Done => StepIndex >= Maneuver.Steps.Count;

    /// <summary>One sim step's stick and throttle. The first call captures the entry frame:
    /// the level entry-heading frame, or the full entry attitude when the maneuver is
    /// <c>relative</c>.</summary>
    public FlightInput Next(FlightModel model, float dt)
    {
        if (!_begun)
        {
            _begun = true;
            var att = model.Attitude.Orthonormalized();
            _reference = Maneuver.Relative
                ? att
                : new Basis(Vector3.Up, Mathf.DegToRad(AiPilot.HeadingDegOf(-att.Z)));
        }

        var error = Vector3.Zero;
        while (!Done)
        {
            error = BodyFrameError(model.Attitude, TargetBasis(Maneuver.Steps[StepIndex]));
            var step = Maneuver.Steps[StepIndex];
            // Half a frame of slack so an accumulated float clock advances on the nearest
            // frame rather than one late (60 × 1/60f sums just under 1.0).
            bool advance = step.DurationS > 0f
                ? _stepElapsed + 0.5f * dt >= step.DurationS
                : Mathf.RadToDeg(error.Length()) <= StepToleranceDeg
                    || _stepElapsed + 0.5f * dt >= ZeroDurationTimeoutS;
            if (!advance)
                break;
            StepIndex++;
            _stepElapsed = 0f;
        }
        _stepElapsed += dt;

        if (Done)
            error = BodyFrameError(model.Attitude, _reference);

        return new FlightInput
        {
            Pitch = Mathf.Clamp((error.X - model.BodyRates.X * RateLead) * Gain, -1f, 1f),
            Yaw = Mathf.Clamp((error.Y - model.BodyRates.Y * RateLead) * Gain, -1f, 1f),
            Roll = Mathf.Clamp((error.Z - model.BodyRates.Z * RateLead) * Gain, -1f, 1f),
            Throttle = Mathf.Clamp(Throttle, 0f, 1f),
        };
    }

    /// <summary>The rotation from the current attitude to the target as a body-frame
    /// axis·angle vector (radians), whose components line up with the stick axes.</summary>
    private static Vector3 BodyFrameError(Basis attitude, Basis target)
    {
        var q = (attitude.Orthonormalized().Inverse() * target).GetRotationQuaternion();
        if (q.W < 0f)
            q = -q; // the short way round
        float angle = 2f * Mathf.Acos(Mathf.Clamp(q.W, -1f, 1f));
        var axis = new Vector3(q.X, q.Y, q.Z);
        float len = axis.Length();
        return len < 1e-6f ? Vector3.Zero : axis * (angle / len);
    }

    /// <summary>The step's target attitude in the world frame: entry frame · yaw · pitch · roll
    /// (degrees; body axes — pitch +up about X, yaw +left about Y, roll +left about Z).</summary>
    private Basis TargetBasis(ManeuverStep step) =>
        _reference
        * new Basis(Vector3.Up, Mathf.DegToRad(step.YawDeg))
        * new Basis(Vector3.Right, Mathf.DegToRad(step.PitchDeg))
        * new Basis(Vector3.Back, Mathf.DegToRad(step.RollDeg));
}
