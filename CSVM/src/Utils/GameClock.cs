using Godot;

namespace CSVM.Utils;

/// <summary>
/// The session's simulation clock — one object owning "how much sim time does this rendered
/// frame advance". Every sim consumer takes its dt from here instead of its own
/// <c>_Process</c>/<c>_PhysicsProcess</c> delta, so pausing, single-stepping and fixed-dt replay
/// all work from one place and mean the same thing everywhere. Modes and published-instance
/// shape: this module's entry in docs/architecture.md; the shader tie-in is <c>ShaderTime.cs</c>'s
/// entry right after it.
/// ⚠ A once-per-frame consumer reads <see cref="FrameDt"/>; one that must see each sub-step loops
/// <see cref="Steps"/> times on <see cref="Dt"/>. A <c>_PhysicsProcess</c> consumer calls
/// <see cref="PhysicsDt"/>, where a zero return means the session drives it explicitly this frame.
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
    /// Orthogonal to the mode — every mode can halt.</summary>
    public bool Halted;

    /// <summary>Time-scale multiplier (the animation lab's 0.1×–4× transport). 1 = real speed.</summary>
    public float Scale = 1f;

    /// <summary>The physics-stepped sim frozen while a cutscene definition owns the session:
    /// <see cref="PhysicsDt"/> answers zero, so every self-stepping node returns the way it does on
    /// a parent-driven frame. ⚠ Do not fold this into <see cref="Halted"/> or make it touch
    /// <see cref="Steps"/>: the animation runtime and the other per-frame consumers read
    /// <see cref="FrameDt"/> and must keep advancing, because the movie is animation.</summary>
    public bool SimHeld;

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

    /// <summary>Sub-steps this rendered frame — 0 while halted, 1 in Realtime/FixedStep,
    /// 0..15 in FixedAccum.</summary>
    public int Steps { get; private set; }

    /// <summary>Seconds per sub-step this rendered frame.</summary>
    public float Dt { get; private set; } = FixedDt;

    /// <summary>Total sim seconds this rendered frame — what a once-per-frame consumer wants.</summary>
    public float FrameDt => Dt * Steps;

    /// <summary>How far the wall clock has run into the NEXT sim step, 0..1 — the render
    /// interpolation fraction for a consumer drawing between fixed steps. Meaningful only in
    /// FixedAccum (the interactive animation lab); the other modes report 1, "draw the current
    /// sim pose exactly", so a scripted FixedStep frame stays byte-identical.</summary>
    public float StepFraction => Mode == RunMode.FixedAccum ? Mathf.Clamp(_accum / FixedDt, 0f, 1f) : 1f;

    /// <summary>True when the session must drive the physics-stepped consumers itself,
    /// <see cref="Steps"/> times, in tree order, because Godot's physics tick is not the sim clock
    /// this frame. <see cref="SimHeld"/> zeroes <see cref="PhysicsDt"/> without setting this.</summary>
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

    /// <summary>The sim dt for a <c>_PhysicsProcess</c> consumer. Zero means "return without
    /// stepping": Godot's physics tick is not the sim clock in any non-realtime mode, so the
    /// session steps those consumers from its own frame instead, preserving their tree order, and
    /// under <see cref="SimHeld"/> nothing steps them at all.</summary>
    public float PhysicsDt(double godotPhysicsDelta) =>
        ParentDriven || SimHeld ? 0f : (float)godotPhysicsDelta;
}
