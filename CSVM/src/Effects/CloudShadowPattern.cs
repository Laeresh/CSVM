using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// The Enhanced cloud shadows' tunes and drift, with no engine object touched. The pattern itself
/// lives only in <c>csky_cloud_shadow.gdshaderinc</c>: value noise over a world-anchored lattice of
/// <see cref="PatternSize"/> cells, wrapping every <see cref="Period"/> metres. The drift is chosen
/// from the mission (<see cref="ChooseDrift"/>). A remake-only rule: the original casts no cloud
/// shadow. <see cref="CloudShadow"/> publishes the result.
/// </summary>
public static class CloudShadowPattern
{
    /// <summary>⚠ TUNE, owed a look at the controls under Enhanced. The lattice cell in metres, about
    /// the width of one shadow.</summary>
    public const float PatternSize = 900f;

    /// <summary>⚠ TUNE, owed a look at the controls. The noise level a shadow's edge is centred on is
    /// one less this, so a larger value shades more ground.</summary>
    public const float Coverage = 0.42f;

    /// <summary>⚠ TUNE, owed a look at the controls. The share of the sun a shadow's core takes away.
    /// The ambient light is untouched, so the ground darkens by less than this.</summary>
    public const float Darkness = 0.3f;

    /// <summary>TUNE, owed a look at the controls. The share of the ambient light a shadow's core takes
    /// away, through the fragment's AO. 0 leaves the ambient whole.</summary>
    public const float Ambient = 0f;

    /// <summary>TUNE: the noise band a shadow's edge ramps across, half each side of its centre.</summary>
    public const float Softness = 0.12f;

    /// <summary>TUNE: the drift's speed in metres per second, along the direction the mission gives
    /// (<see cref="ChooseDrift"/>). 0 keeps the mission's own speed.</summary>
    public const float DriftSpeed = 0f;

    /// <summary>TUNE: the fastest drift a ceiling scroll gives, metres per second. The scroll's speed
    /// in metres rests on an estimate of the ceiling's height (<see cref="CeilingDrift"/>).</summary>
    public const float MaxCeilingDrift = 20f;

    /// <summary>The cells one lattice repeats over. The shader wraps its corners at this and twice it.</summary>
    public const int Cells = 256;

    // Below this, metres per second, a source offers no drift and the next one is read.
    private const float MinDrift = 0.05f;

    /// <summary>Where the mission's drift came from, the first that exists in this order.</summary>
    public enum DriftSource
    {
        /// <summary>The scrolling below-deck ceiling.</summary>
        Ceiling,

        /// <summary>The horizontal part of the authored <c>WIND</c> <c>STATIC_VELOCITY</c>.</summary>
        Wind,

        /// <summary><see cref="FixedDrift"/>.</summary>
        Fixed,
    }

    /// <summary>TUNE: the drift where the mission offers no ceiling scroll and no sideways wind, metres
    /// per second along X and Z.</summary>
    public static Vector2 FixedDrift => new(3.2f, 2.4f);

    /// <summary>The distance in metres after which the pattern repeats exactly, along X and Z.</summary>
    public static float Period => Cells * PatternSize;

    /// <summary>The noise band a shadow's edge ramps over, low then high.</summary>
    public static Vector2 Edge => Tune.Default.Edge;

    /// <summary>The drift and its source. It is the <paramref name="ceiling"/> scroll in metres per
    /// second where there is one, capped at <see cref="MaxCeilingDrift"/>. Else it is the horizontal
    /// part of <paramref name="staticWind"/>, else <see cref="FixedDrift"/>.</summary>
    public static (Vector2 Velocity, DriftSource Source) ChooseDrift(Vector2? ceiling, Vector3 staticWind)
    {
        if (ceiling is { } c && c.Length() >= MinDrift)
            return (c.LimitLength(MaxCeilingDrift), DriftSource.Ceiling);
        var sideways = new Vector2(staticWind.X, staticWind.Z);
        if (sideways.Length() >= MinDrift)
            return (sideways, DriftSource.Wind);
        return (FixedDrift, DriftSource.Fixed);
    }

    /// <summary>The ground velocity in metres per second of a scrolling ceiling's texture, or null
    /// where its triangles carry none. The triangles are <paramref name="corners"/> in the dome's frame,
    /// with <paramref name="uvs"/>, scrolled by the shader's <paramref name="scroll"/> UV units a second.
    /// Each triangle's UV-to-X/Z map carries the scroll, weighted by its area seen from above. The dome
    /// rides the camera, so its metres scale to put its top at <paramref name="ceilingAltitude"/>.</summary>
    public static Vector2? CeilingDrift(IReadOnlyList<Vector3> corners, IReadOnlyList<Vector2> uvs, Vector2 scroll,
        float ceilingAltitude)
    {
        var sum = Vector2.Zero;
        float area = 0f, top = 0f;
        for (int i = 0; i + 2 < corners.Count && i + 2 < uvs.Count; i += 3)
        {
            var p0 = corners[i];
            var e1 = new Vector2(corners[i + 1].X - p0.X, corners[i + 1].Z - p0.Z);
            var e2 = new Vector2(corners[i + 2].X - p0.X, corners[i + 2].Z - p0.Z);
            var d1 = uvs[i + 1] - uvs[i];
            var d2 = uvs[i + 2] - uvs[i];
            float det = (d1.X * d2.Y) - (d1.Y * d2.X);
            float seen = MathF.Abs((e1.X * e2.Y) - (e1.Y * e2.X)) * 0.5f;
            if (MathF.Abs(det) < 1e-9f || seen < 1e-6f)
                continue;
            // The texture moves against the scroll the shader adds to its UV. The edge weights solve
            // d1 * w.x + d2 * w.y = -scroll, and carry the same weights of the X/Z edges.
            var s = -scroll;
            var w = new Vector2((d2.Y * s.X) - (d2.X * s.Y), (d1.X * s.Y) - (d1.Y * s.X)) / det;
            sum += ((e1 * w.X) + (e2 * w.Y)) * seen;
            area += seen;
            top = MathF.Max(top, MathF.Max(p0.Y, MathF.Max(corners[i + 1].Y, corners[i + 2].Y)));
        }
        if (area <= 0f || top <= 0f || ceilingAltitude <= 0f)
            return null;
        var drift = sum / area * (ceilingAltitude / top);
        return drift.Length() >= MinDrift ? drift : null;
    }

    /// <summary>The offset the pattern is read at after <paramref name="seconds"/> of
    /// <paramref name="velocity"/>, wrapped into one <see cref="Period"/>. A point reads the pattern
    /// at its own X/Z plus this, so the pattern moves with the velocity.</summary>
    public static Vector2 Offset(Vector2 velocity, double seconds)
    {
        double period = Period;
        return new Vector2((float)Wrap(-velocity.X * seconds, period), (float)Wrap(-velocity.Y * seconds, period));
    }

    /// <summary>A drift of <paramref name="speed"/> metres per second toward <paramref name="degrees"/>,
    /// measured from +X toward +Z as the sea's wind is.</summary>
    public static Vector2 Toward(float degrees, float speed)
    {
        float r = Mathf.DegToRad(degrees);
        return new Vector2(MathF.Cos(r), MathF.Sin(r)) * speed;
    }

    /// <summary>The heading of <paramref name="drift"/> in degrees from +X toward +Z, 0 for no drift.</summary>
    public static float HeadingOf(Vector2 drift) =>
        drift == Vector2.Zero ? 0f : Mathf.PosMod(Mathf.RadToDeg(MathF.Atan2(drift.Y, drift.X)), 360f);

    private static double Wrap(double x, double period) => x - (Math.Floor(x / period) * period);

    /// <summary>The pattern's tunes as one session draws them. The constants are the defaults; the
    /// cloud lab edits a live copy. A null <see cref="Heading"/> keeps the mission's direction.</summary>
    public readonly record struct Tune(float Darkness, float PatternSize, float Coverage, float Softness, float Ambient,
        float DriftSpeed, float? Heading)
    {
        /// <summary>The shipped tunes.</summary>
        public static Tune Default => new(CloudShadowPattern.Darkness, CloudShadowPattern.PatternSize,
            CloudShadowPattern.Coverage, CloudShadowPattern.Softness, CloudShadowPattern.Ambient, CloudShadowPattern.DriftSpeed, null);

        /// <summary>The noise band a shadow's edge ramps over, low then high.</summary>
        public Vector2 Edge => new(1f - Coverage - Softness, 1f - Coverage + Softness);

        /// <summary>Each tune held to the range the shader draws sensibly, the heading wrapped into a turn.</summary>
        public Tune Clamped() => new(Math.Clamp(Darkness, 0f, 1f), Math.Clamp(PatternSize, 50f, 5000f),
            Math.Clamp(Coverage, 0f, 1f), Math.Clamp(Softness, 0.005f, 0.5f), Math.Clamp(Ambient, 0f, 1f),
            Math.Clamp(DriftSpeed, 0f, 100f), Heading is { } h ? Mathf.PosMod(h, 360f) : null);

        /// <summary>The <paramref name="mission"/> drift turned to <see cref="Heading"/> and set to
        /// <see cref="DriftSpeed"/>; a null heading or a speed of 0 keeps the mission's.</summary>
        public Vector2 Drift(Vector2 mission) =>
            Toward(Heading ?? HeadingOf(mission), DriftSpeed > 0f ? DriftSpeed : mission.Length());
    }
}
