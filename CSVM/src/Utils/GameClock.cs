using Godot;

namespace CSVM.Utils;

/// <summary>
/// The session's simulation clock, one object owning "how much sim time does this rendered
/// frame advance". GameSession translates it into requests to SessionSimulation, so pausing,
/// single-stepping and fixed-dt replay all enter one ordered step. Modes and published-instance
/// shape: this module's entry in docs/architecture.md; the shader tie-in is <c>ShaderTime.cs</c>'s
/// entry right after it.
/// ⚠ A once-per-frame presentation consumer reads <see cref="FrameDt"/>; GameSession loops
/// <see cref="Steps"/> times on <see cref="Dt"/>. Realtime authored animation uses
/// <see cref="PhysicsDt"/> to select its physics or frame callback.
/// </summary>
public sealed class GameClock
{
    /// <summary>The simulation step: sim results are a function of the step count alone, whatever
    /// the render rate.</summary>
    public const float FixedDt = 1f / 60f;

    /// <summary>The clock of the session being built or run; null between sessions. Consumers
    /// fall back to their raw frame delta when it is null, so a node that outlives (or precedes)
    /// a session still ticks.</summary>
    public static GameClock? Current;

    public RunMode Mode = RunMode.Realtime;

    /// <summary>Sim frozen in place: no steps advance until <see cref="StepOnce"/> queues one.
    /// Orthogonal to the mode, every mode can halt.</summary>
    public bool Halted;

    /// <summary>Time-scale multiplier (the animation lab's 0.1×–4× transport). 1 = real speed.</summary>
    public float Scale = 1f;

    /// <summary>The session simulation held while a cutscene definition owns the session:
    /// SessionSimulation admits no step, and <see cref="PhysicsDt"/> also keeps its realtime
    /// adapter quiet. ⚠ Do not fold this into <see cref="Halted"/> or let it change
    /// <see cref="Steps"/>. Animation and the other frame consumers must retain
    /// <see cref="FrameDt"/>, because the movie is animation.</summary>
    public bool SimHeld;

    /// <summary>Authored animation held on its current pose. Mission ending sets this with
    /// <see cref="SimHeld"/> so the last flown frame remains unchanged; a cutscene hold leaves it
    /// clear because the movie itself is authored animation.</summary>
    public bool AuthoredAnimationHeld;

    // A hitch must not unwind as a burst of catch-up steps: a quarter second (15 steps) keeps
    // slow frames honest without turning a debugger breakpoint stall into fast-forward.
    private const float MaxAccum = 0.25f;

    private float _accum;
    private bool _stepPending;

    public enum RunMode
    {
        /// <summary>One step per rendered frame, at the wall delta.</summary>
        Realtime,

        /// <summary>Whole fixed steps from a clamped wall-time accumulator.</summary>
        FixedAccum,

        /// <summary>Exactly one fixed step per rendered frame; wall time ignored.</summary>
        FixedStep,
    }

    /// <summary>Sim steps advanced since the session started.</summary>
    public long Frame { get; private set; }

    /// <summary>Sim seconds advanced since the session started.</summary>
    public double Time { get; private set; }

    /// <summary>Sub-steps this rendered frame, 0 while halted, 1 in Realtime/FixedStep,
    /// 0..15 in FixedAccum.</summary>
    public int Steps { get; private set; }

    /// <summary>Seconds per sub-step this rendered frame.</summary>
    public float Dt { get; private set; } = FixedDt;

    /// <summary>Total sim seconds this rendered frame, what a once-per-frame consumer wants.</summary>
    public float FrameDt => Dt * Steps;

    /// <summary>How far the wall clock has run into the NEXT sim step, 0..1, the render
    /// interpolation fraction for a consumer drawing between fixed steps. Meaningful only in
    /// FixedAccum (the interactive animation lab); the other modes report 1, "draw the current
    /// sim pose exactly", so a scripted FixedStep frame stays byte-identical.</summary>
    public float StepFraction => Mode == RunMode.FixedAccum ? Mathf.Clamp(_accum / FixedDt, 0f, 1f) : 1f;

    /// <summary>True when GameSession requests <see cref="Steps"/> simulation steps from its frame
    /// callback instead of one from its physics callback. <see cref="SimHeld"/> and
    /// <see cref="AuthoredAnimationHeld"/> zero <see cref="PhysicsDt"/> without setting this.</summary>
    public bool ParentDriven => Halted || Mode != RunMode.Realtime;

    /// <summary>Queue exactly one step through a halt (the <c>.</c> transport key).</summary>
    public void StepOnce() => _stepPending = true;

    /// <summary>Drops the FixedAccum residue, so a replay restarted from step 0 does not inherit
    /// a fraction of a step from the run before it.</summary>
    public void ResetAccumulator() => _accum = 0f;

    /// <summary>Decides this rendered frame's step count and step size. Called once per frame,
    /// before any consumer reads the clock.</summary>
    public void BeginFrame(double wallDelta)
    {
        if (Halted)
        {
            Dt = FixedDt;
            Steps = _stepPending ? 1 : 0;
            _stepPending = false;
        }
        else
        {
            switch (Mode)
            {
                case RunMode.FixedStep:
                    Dt = FixedDt * Scale;
                    Steps = 1;
                    break;
                case RunMode.FixedAccum:
                    Dt = FixedDt;
                    Steps = 0;
                    _accum = Mathf.Min(_accum + (float)wallDelta * Scale, MaxAccum);
                    while (_accum >= FixedDt)
                    {
                        _accum -= FixedDt;
                        Steps++;
                    }
                    break;
                default:
                    Dt = (float)wallDelta * Scale;
                    Steps = 1;
                    break;
            }
        }
        Frame += Steps;
        Time += Steps * (double)Dt;
    }

    /// <summary>The realtime authored-animation dt. Zero hands animation to its frame callback:
    /// parent-driven modes and <see cref="SimHeld"/> both need the movie's separate frame path,
    /// while <see cref="AuthoredAnimationHeld"/> keeps both paths quiet.</summary>
    public float PhysicsDt(double godotPhysicsDelta) =>
        ParentDriven || SimHeld || AuthoredAnimationHeld ? 0f : (float)godotPhysicsDelta;
}
