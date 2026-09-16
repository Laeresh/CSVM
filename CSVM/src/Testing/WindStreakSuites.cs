using System;
using CSVM.Effects;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

internal static class WindStreakSuites
{
    // One sim step at the fixed clock's rate, the cadence the field is driven at in flight.
    private const float Step = 1f / 60f;

    // A speed the cruise gate must silence the field at, and one at rated max it must not.
    private const float CruiseSpeedFraction = 0.6f;
    private const float FullSpeedFraction = 1f;

    // The airframe scale the fractions above are quoted on, only to turn them into a velocity.
    private const float RatedSpeed = 113f;

    [Suite("wind-streaks",
        "the enhanced wind-streak field builds only under the enhanced presentation, and its "
        + "uniforms follow the flight: zero alpha and a hidden draw at a cruise fraction of rated "
        + "max, rising to the master opacity at full throttle, a pull adding on top of that, the "
        + "streak length growing with airspeed, the long axis tracking the velocity, and the "
        + "accumulated drift staying inside the wrap cell over a long run at speed")]
    internal static void WindStreaksFollowTheFlight(TestContext ctx)
    {
        bool wasEnhanced = GraphicsMode.Enhanced;
        try
        {
            GraphicsMode.Resolve(GraphicsMode.Default);
            var faithful = WindStreaks.Create();
            ctx.Check(faithful == null, $"the faithful presentation builds no streak field");
            faithful?.Free();

            GraphicsMode.Resolve(GraphicsMode.EnhancedWord);
            var field = WindStreaks.Create();
            ctx.Check(field != null, $"the enhanced presentation builds one");
            if (field == null)
                return;
            try
            {
                CheckShaderClock(ctx, field);
                CheckSpeedRamp(ctx, field);
                CheckPull(ctx, field);
                CheckDriftStaysInTheCell(ctx, field);
            }
            finally
            {
                field.Free();
            }
        }
        finally
        {
            GraphicsMode.Resolve(wasEnhanced ? GraphicsMode.EnhancedWord : GraphicsMode.Default);
        }
    }

    // Straight and level along −Z at a given fraction of rated max.
    private static void Level(WindStreaks field, float speedFraction, float loadFactorG)
    {
        var velocity = Vector3.Forward * (speedFraction * RatedSpeed);
        field.Update(Step, velocity, speedFraction, loadFactorG);
    }

    private static float PeakAlpha(WindStreaks field) =>
        (float)field.Material.GetShaderParameter("peak_alpha");

    // The rain field's header forbids Godot's TIME because a halted clock must halt the effect;
    // this field takes the same prohibition and adds its own, since a speed-scaled rate times any
    // clock jumps the whole population whenever the throttle moves.
    private static void CheckShaderClock(TestContext ctx, WindStreaks field)
    {
        string code = field.Material.Shader?.Code ?? string.Empty;
        ctx.Check(!code.Contains("TIME", StringComparison.Ordinal)
                  && !code.Contains("csky_time", StringComparison.Ordinal),
            $"the streak shader reads no clock, the drift arrives as an accumulated uniform");
    }

    private static void CheckSpeedRamp(TestContext ctx, WindStreaks field)
    {
        Level(field, CruiseSpeedFraction, 1f);
        float cruise = PeakAlpha(field);
        float cruiseLength = field.LengthMeters;
        ctx.Check(cruise == 0f, $"cruise at {CruiseSpeedFraction:0.00} of rated max draws nothing, alpha {cruise:0.0000}");
        ctx.Check(!IsDrawn(field), $"and the draw itself is off at cruise");

        Level(field, FullSpeedFraction, 1f);
        float full = PeakAlpha(field);
        ctx.Check(full > 0f, $"full throttle raises it, alpha {full:0.0000}");
        ctx.Check(IsDrawn(field), $"and the draw is on at full throttle");
        ctx.Check(field.LengthMeters > cruiseLength,
            $"the streak lengthens with airspeed, {cruiseLength:0.00} m to {field.LengthMeters:0.00} m");

        // Monotone between the two, so the cue tracks the throttle rather than snapping on.
        float previous = -1f;
        int falls = 0;
        for (int i = 0; i <= 10; i++)
        {
            float fraction = CruiseSpeedFraction + ((FullSpeedFraction - CruiseSpeedFraction) * i / 10f);
            Level(field, fraction, 1f);
            float alpha = PeakAlpha(field);
            if (alpha < previous)
                falls++;
            previous = alpha;
        }
        ctx.Same(0, falls, $"alpha never falls as the speed fraction rises");

        // The long axis is the flight path's, which is what makes a streak read as motion.
        var velocity = new Vector3(3f, 0f, -4f) * 25f;
        field.Update(Step, velocity, FullSpeedFraction, 1f);
        var dir = (Vector3)field.Material.GetShaderParameter("stream_dir");
        ctx.Check(dir.Normalized().Dot(velocity.Normalized()) > 0.999f,
            $"the streak axis tracks the velocity direction, dot {dir.Normalized().Dot(velocity.Normalized()):0.0000}");
    }

    private static void CheckPull(TestContext ctx, WindStreaks field)
    {
        Level(field, 0.85f, 1f);
        float level = PeakAlpha(field);
        Level(field, 0.85f, 4f);
        float pulling = PeakAlpha(field);
        ctx.Check(pulling > level, $"a 4 G pull thickens the field at one speed, {level:0.0000} to {pulling:0.0000}");

        // The gate is the speed one: no pull puts streaks on a cruising aeroplane.
        Level(field, CruiseSpeedFraction, 5f);
        ctx.Check(PeakAlpha(field) == 0f, $"a pull below the cruise gate still draws nothing");
    }

    // The drift is a running sum in metres, so an unwrapped one would lose resolution over a
    // sortie and the field would coarsen. It is wrapped into the box cell every step.
    private static void CheckDriftStaysInTheCell(TestContext ctx, WindStreaks field)
    {
        var box = (Vector3)field.Material.GetShaderParameter("box_half");
        float cell = box.X * 2f;
        field.Reset();
        for (int i = 0; i < 6000; i++)
            Level(field, 1.3f, 1f);
        var drift = (Vector3)field.Material.GetShaderParameter("drift");
        ctx.Check(drift.X >= 0f && drift.X < cell && drift.Y >= 0f && drift.Y < cell
                  && drift.Z >= 0f && drift.Z < cell,
            $"100 s at 1.3x rated max leaves the drift inside the {cell:0} m cell, {drift}");

        field.Reset();
        ctx.Check(PeakAlpha(field) == 0f && !IsDrawn(field),
            $"a respawn reset hides the field and forgets the drift");
    }

    private static bool IsDrawn(WindStreaks field)
    {
        foreach (var child in field.GetChildren())
        {
            if (child is MultiMeshInstance3D mmi)
                return mmi.Visible;
        }
        return false;
    }
}
