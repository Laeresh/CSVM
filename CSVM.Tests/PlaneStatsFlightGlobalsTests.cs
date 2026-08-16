using System.IO;
using CSVM.Flight;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The flight globals <c>PlaneStats</c> reads from player.json (lift/AoA/G limiters, turn/yaw fade
/// curves, the pitch fade, drag_fade_speed) come off this install's AUTHORED values, not silently
/// off the executable's compiled fallbacks baked into each field's default. A key that reads back
/// as its fallback is the failure mode this test exists to catch.
/// </summary>
public class PlaneStatsFlightGlobalsTests
{
    private static string ZrdrPath =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    [ExtractedDataFact]
    public void TheAuthoredFlightGlobalsAreLoadedNotFallenBackTo()
    {
        var s = PlaneStats.Load(ZrdrPath, "player_bhawk");

        // lift_accel_rate: authored 0.75, fallback 1.2 — NOT converted (a rate, 1/s).
        Assert.Equal(0.75f, s.LiftAccelRate, 3);

        // liftAOAs [5, 9] deg, cosined at load; fallback cosines are 0.98 / 0.96.
        Assert.Equal(Mathf.Cos(Mathf.DegToRad(5f)), s.LiftAoaCosLo, 4);
        Assert.Equal(Mathf.Cos(Mathf.DegToRad(9f)), s.LiftAoaCosHi, 4);
        Assert.NotEqual(0.98f, s.LiftAoaCosLo);
        Assert.NotEqual(0.96f, s.LiftAoaCosHi);

        // maxAOA 46 deg, cosined; fallback cosine is 0.85.
        Assert.Equal(Mathf.Cos(Mathf.DegToRad(46f)), s.MaxAoaCos, 4);
        Assert.NotEqual(0.85f, s.MaxAoaCos);

        // highGs [9, 15] / lowGs [-6, -9] — plain G, NOT converted.
        Assert.Equal(9f, s.HighGStart, 3);
        Assert.Equal(15f, s.HighGMax, 3);
        Assert.Equal(-6f, s.LowGStart, 3);
        Assert.Equal(-9f, s.LowGMax, 3);

        // turn_fade_in 10 / turn_fade_out 50 mph — turn_fade_in matches its fallback value,
        // so it alone cannot prove the read; turn_fade_out (authored 50, fallback 40) can.
        Assert.Equal(10f * PhysicsConstants.MphToMs, s.TurnFadeIn, 3);
        Assert.Equal(50f * PhysicsConstants.MphToMs, s.TurnFadeOut, 3);

        // yaw_* set: authored 0.0625 / 0.17 / 10 / 50 / 400 mph against fallbacks
        // 0.05 / 0.1 / 10 / 22.5 / 45 mph — fade_in ties the fallback, the rest don't.
        Assert.Equal(0.0625f, s.YawLowSpeed, 4);
        Assert.Equal(0.17f, s.YawHighSpeed, 3);
        Assert.Equal(10f * PhysicsConstants.MphToMs, s.YawFadeIn, 3);
        Assert.Equal(50f * PhysicsConstants.MphToMs, s.YawMax, 3);
        Assert.Equal(400f * PhysicsConstants.MphToMs, s.YawFadeOut, 3);

        // high_speed_pitch_fade authored [1000, 1001] mph vs fallback [500, 600] mph.
        Assert.Equal(1000f * PhysicsConstants.MphToMs, s.HighSpeedPitchFadeLo, 2);
        Assert.Equal(1001f * PhysicsConstants.MphToMs, s.HighSpeedPitchFadeHi, 2);

        // groundblow_elev 400 / groundblow_mag 10 / ai_groundblow 0.5 against fallbacks
        // 100 / 1.5 / 0.9 — all three raw scalars, and elev is METRES, so no conversion applies
        // to any of them. A 400 read back as 400 × MphToMs would be a 179 m ray.
        Assert.Equal(400f, s.GroundBlowElev, 3);
        Assert.Equal(10f, s.GroundBlowMag, 3);
        Assert.Equal(0.5f, s.AiGroundBlow, 3);

        // bounce_factor 0.6 against a fallback of 0.8, and it lives one level down, inside the
        // `crash` block — a reader that looked for it at the top level would read the fallback back.
        Assert.Equal(0.6f, s.BounceFactor, 3);
        Assert.NotEqual(0.8f, s.BounceFactor, 3);

        // The collision damage ranges, same `crash` block: authored [50, 300] against compiled
        // fallbacks [15, 200]. ⚠ Element 0 is the FLOOR and element 1 the SCALE — read in the
        // other order a 50 HP floor silently becomes a 300 HP one (`BL-302`).
        Assert.Equal(50f, s.CollideArmorFloor, 3);
        Assert.Equal(300f, s.CollideArmorScale, 3);
        Assert.Equal(50f, s.CollideHealthFloor, 3);
        Assert.Equal(300f, s.CollideHealthScale, 3);

        // drag_fade_speed: authored 40 mph — happens to equal this field's
        // documented-as-unconfirmed fallback, so this only proves the read did not error,
        // not that the read (versus the fallback) took effect; see the field's comment.
        Assert.Equal(40f * PhysicsConstants.MphToMs, s.DragFadeSpeed, 3);
    }
}
