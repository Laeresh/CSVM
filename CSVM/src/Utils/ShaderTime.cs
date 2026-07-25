using System;
using Godot;

namespace CSVM.Utils;

/// <summary>
/// The clock the <i>shaders</i> read: the global shader uniform <c>csky_time</c> (seconds),
/// written once per rendered frame from the session's <see cref="GameClock"/>. Every animated
/// shader this project generates reads it in place of Godot's <c>TIME</c> built-in, which is
/// what makes a halted clock a true freeze-frame and makes a fixed-step run's pixel output a
/// function of the rendered-frame count rather than of the machine's frame rate.
///
/// <para>Between sessions — the launchscreen, and the frame after a teardown — there is no
/// clock; the value then keeps advancing on the wall delta from wherever the last session left
/// it, so no on-screen animation stalls while the menu is up.</para>
/// </summary>
public static class ShaderTime
{
    /// <summary>The uniform's name, declared in <c>res://shaders/csky_time.gdshaderinc</c>.</summary>
    public const string Param = "csky_time";

    /// <summary>The wrap period, matching Godot's own <c>TIME</c> rollover
    /// (<c>rendering/limits/time/time_rollover_secs</c>, 3600 s by default).
    /// <para>⚠ This value is a contract, not a tuning constant: every UV scroll rate in this
    /// install (0.07 / 0.4 / 0.5 / 0.7 / 1.0 units/s) times 3600 is a whole number of texture
    /// repeats, so the wrap lands on an identical frame and is invisible. Any other period puts
    /// a jump on every scrolling surface once an hour.</para></summary>
    public const double RolloverSecs = 3600.0;

    private static double _time;

    /// <summary>Declares the uniform. Must run before the first shader that reads it is built —
    /// Godot refuses to compile a shader referencing an unregistered global.</summary>
    public static void RegisterGlobal()
    {
        RenderingServer.GlobalShaderParameterAdd(Param,
            RenderingServer.GlobalShaderParameterType.Float, 0.0f);
    }

    /// <summary>Publishes this rendered frame's shader time. Inside a session the value <i>is</i>
    /// the clock's sim time, so a halt freezes it and a fixed step pins it to the frame count;
    /// with no clock it advances on <paramref name="wallDelta"/>.</summary>
    public static void Advance(GameClock? clock, double wallDelta)
    {
        _time = clock != null ? clock.Time : _time + wallDelta;
        RenderingServer.GlobalShaderParameterSet(Param, (float)Wrap(_time));
    }

    private static double Wrap(double t) => t - (Math.Floor(t / RolloverSecs) * RolloverSecs);
}
