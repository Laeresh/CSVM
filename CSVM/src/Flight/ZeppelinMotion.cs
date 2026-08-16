using System;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>The kinematic zeppelin motion law (M4 F17): flies a <see cref="ZeppelinDef"/> along
/// its net through the shared <see cref="AiNetFollower"/>, forward-only along the facing, under
/// the record's own yaw/pitch/speed limits (docs/architecture.md). Pure state — no Node, no
/// flight model; the consumer writes <see cref="Position"/>/<see cref="YawRad"/>/
/// <see cref="PitchRad"/> onto the world node. <see cref="AliveEngines"/> is the seam F18's damage
/// aggregator drives, re-scaling the live limits through <see cref="EngineFactor"/>.</summary>
public sealed class ZeppelinMotion
{
    private readonly float _maxRateYaw;      // rad/s
    private readonly float _maxRatePitch;    // rad/s
    private readonly float _accelYaw;        // rad/s²
    private readonly float _accelPitch;      // rad/s²
    private readonly float _minPitch;        // rad
    private readonly float _maxPitch;        // rad
    private float _yawRate;
    private float _pitchRate;

    public ZeppelinMotion(ZeppelinDef def, AiNetFollower follower)
    {
        Def = def;
        Follower = follower;
        Position = def.Position;
        YawRad = Mathf.DegToRad(def.YawDeg);
        // Verbatim: the original's own initial-pitch clamp never fires (unit bug — it compares
        // the already-radian pitch against still-degree bounds, docs/formats/mission-entities.md).
        PitchRad = Mathf.DegToRad(def.PitchDeg);
        _maxRateYaw = Mathf.DegToRad(def.MaxRateYawDeg);
        _maxRatePitch = Mathf.DegToRad(def.MaxRatePitchDeg);
        _accelYaw = Mathf.DegToRad(def.AccelYawDeg);
        _accelPitch = Mathf.DegToRad(def.AccelPitchDeg);
        _minPitch = Mathf.DegToRad(def.MinPitchDeg);
        _maxPitch = Mathf.DegToRad(def.MaxPitchDeg);
        TotalEngines = Math.Max(1, def.Engines.Count);
        AliveEngines = TotalEngines;
    }

    public ZeppelinDef Def { get; }

    /// <summary>B5's graph walk, shared with the aircraft consumer. Mutable on purpose:
    /// <c>SET_AI_NET</c> retargets a zeppelin at runtime, so a later mission-script layer swaps
    /// the follower rather than rebuilding the motion.</summary>
    public AiNetFollower Follower { get; set; }

    public Vector3 Position { get; private set; }

    /// <summary>Heading, radians, mission-data convention (0 = −Z; the node's Y rotation).</summary>
    public float YawRad { get; private set; }

    public float PitchRad { get; private set; }

    /// <summary>Current forward speed, m/s.</summary>
    public float Speed { get; private set; }

    /// <summary>The engine-loss denominator: the record's engine count at load (never below
    /// 1, so a record with no engines list still moves).</summary>
    public int TotalEngines { get; }

    /// <summary>THE F18 SEAM: the damage aggregator writes the surviving engine count here as
    /// nacelles die; every read below re-derives the scaled limits, so a mid-flight loss slows
    /// the zeppelin on its next step. Clamped to [0, <see cref="TotalEngines"/>] on use.</summary>
    public int AliveEngines { get; set; }

    /// <summary>Speed under the current engine loss: <c>sqrt(alive/total) · max_speed</c>.</summary>
    public float EffectiveMaxSpeed => EngineFactor(AliveEngines, TotalEngines) * Def.MaxSpeed;

    /// <summary>Acceleration under the current engine loss:
    /// <c>(0.8·sqrt(alive/total) + 0.2) · max_accel</c> — a 20 % floor while speed goes to
    /// zero at total loss.</summary>
    public float EffectiveMaxAccel =>
        ((0.8f * EngineFactor(AliveEngines, TotalEngines)) + 0.2f) * Def.MaxAccel;

    /// <summary>The forward unit vector of the current facing.</summary>
    public Vector3 Forward => new(
        -Mathf.Sin(YawRad) * Mathf.Cos(PitchRad),
        Mathf.Sin(PitchRad),
        -Mathf.Cos(YawRad) * Mathf.Cos(PitchRad));

    /// <summary>The decoded engine-loss curve: <c>f = sqrt(alive/total)</c>. NOT the design
    /// document's three 10/40/50 bands — those are design-era and refuted
    /// (docs/formats/mission-entities.md "Engine loss").</summary>
    public static float EngineFactor(int alive, int total)
    {
        if (total <= 0)
        {
            return 1f;
        }
        return Mathf.Sqrt(Math.Clamp(alive, 0, total) / (float)total);
    }

    /// <summary>One sim step: walk the net, turn toward the current node under the yaw/pitch
    /// rate and rate-acceleration limits, accelerate toward the (engine-scaled) max speed, and
    /// move forward along the facing.</summary>
    public void Step(float dt)
    {
        if (dt <= 0f)
        {
            return;
        }
        Follower.Update(Position);
        var to = Follower.CurrentTarget - Position;

        // Yaw toward the node. Inside the capture radius the bearing swings wildly, so hold
        // the heading there and let the follower advance.
        var flat = new Vector2(to.X, to.Z);
        if (flat.LengthSquared() > 1f)
        {
            float desiredYaw = Mathf.Atan2(-to.X, -to.Z);
            _yawRate = TurnRate(_yawRate, Mathf.AngleDifference(YawRad, desiredYaw), dt,
                _maxRateYaw, _accelYaw);
            YawRad = Mathf.Wrap(YawRad + (_yawRate * dt), -Mathf.Pi, Mathf.Pi);
        }

        // Pitch toward the node's altitude, inside the record's flight band.
        float desiredPitch = Mathf.Clamp(
            Mathf.Atan2(to.Y, Mathf.Max(flat.Length(), 1f)), _minPitch, _maxPitch);
        _pitchRate = TurnRate(_pitchRate, desiredPitch - PitchRad, dt, _maxRatePitch, _accelPitch);
        PitchRad = Mathf.Clamp(PitchRad + (_pitchRate * dt), _minPitch, _maxPitch);

        // Speed toward the engine-scaled maximum, at the engine-scaled acceleration.
        Speed = Mathf.MoveToward(Speed, EffectiveMaxSpeed, EffectiveMaxAccel * dt);
        Position += Forward * (Speed * dt);
    }

    // The rate the error asks for this step, reached through the rate-acceleration limit and
    // capped at the rate limit — so a turn ramps in, holds the cap, and ramps out.
    private static float TurnRate(float rate, float error, float dt, float maxRate, float accel)
    {
        float desired = Mathf.Clamp(error / Mathf.Max(dt, 1e-4f), -maxRate, maxRate);
        return Mathf.MoveToward(rate, desired, Mathf.Max(accel, 1e-4f) * dt);
    }
}
