using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// The Enhanced cloud shadows' tunes and drift, with no engine object touched. The pattern itself
/// lives only in <c>csky_cloud_shadow.gdshaderinc</c>: value noise over a world-anchored lattice of
/// one tuned size per cell, wrapping every <see cref="Cells"/> cells. The drift is chosen from the
/// mission (<see cref="ChooseDrift"/>). A remake-only rule: the original casts no cloud shadow.
/// <see cref="CloudShadow"/> publishes the result.
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

    /// <summary>TUNE: the fastest drift a ceiling scroll gives, metres per second. The scroll's speed
    /// in metres rests on an estimate of the ceiling's height (<see cref="CeilingDrift"/>).</summary>
    public const float MaxCeilingDrift = 20f;

    /// <summary>The cells one lattice repeats over. The shader wraps its corners at this and twice it.</summary>
    public const int Cells = 256;

    /// <summary>The drift's speed in metres per second. Unset keeps the mission's own.</summary>
    public static readonly Field SpeedField = new("speed", "Drift speed (m/s)", 0f, 100f, 0.5f, "0.0",
        t => t.DriftSpeed, (t, v) => t with { DriftSpeed = v });

    /// <summary>The drift's heading in degrees from +X toward +Z, as the sea's wind. Unset keeps the mission's own.</summary>
    public static readonly Field HeadingField = new("heading", "Drift heading (deg from +X toward +Z, as the sea's wind)", 0f, 360f, 1f, "0",
        t => t.Heading, (t, v) => t with { Heading = v }, Wraps: true);

    /// <summary>Every tune, in the order the chapters file and the cloud lab list them. The one table
    /// of each tune's key, label, range and format.</summary>
    public static readonly IReadOnlyList<Field> Fields = new[]
    {
        new Field("darkness", "Darkness (sun taken under a core)", 0f, 1f, 0.01f, "0.00",
            t => t.Darkness, (t, v) => t with { Darkness = v }),
        new Field("ambient", "Ambient taken (ambient light under a core)", 0f, 1f, 0.01f, "0.00",
            t => t.Ambient, (t, v) => t with { Ambient = v }),
        new Field("size", "Size (one shadow, m)", 50f, 3000f, 10f, "0",
            t => t.PatternSize, (t, v) => t with { PatternSize = v }),
        new Field("coverage", "Coverage (share of ground shaded)", 0f, 1f, 0.01f, "0.00",
            t => t.Coverage, (t, v) => t with { Coverage = v }),
        new Field("softness", "Softness (edge width)", 0.005f, 0.4f, 0.005f, "0.000",
            t => t.Softness, (t, v) => t with { Softness = v }),
        SpeedField,
        HeadingField,
    };

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
    /// <paramref name="velocity"/> plus a <paramref name="shiftX"/>, <paramref name="shiftZ"/> carried
    /// over from earlier drifts, wrapped into one period of <see cref="Cells"/> cells of
    /// <paramref name="patternSize"/>. A point reads the pattern at its own X/Z plus this, so the
    /// pattern moves with the velocity.</summary>
    public static Vector2 Offset(Vector2 velocity, double seconds, float patternSize, double shiftX, double shiftZ)
    {
        double period = Cells * (double)patternSize;
        return new Vector2((float)Wrap((-velocity.X * seconds) + shiftX, period), (float)Wrap((-velocity.Y * seconds) + shiftZ, period));
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
    /// cloud lab edits a live copy. A null <see cref="DriftSpeed"/> or <see cref="Heading"/> keeps
    /// the mission's own.</summary>
    public readonly record struct Tune(float Darkness, float PatternSize, float Coverage, float Softness, float Ambient,
        float? DriftSpeed, float? Heading)
    {
        /// <summary>The shipped tunes.</summary>
        public static Tune Default => new(CloudShadowPattern.Darkness, CloudShadowPattern.PatternSize,
            CloudShadowPattern.Coverage, CloudShadowPattern.Softness, CloudShadowPattern.Ambient, null, null);

        /// <summary>The noise band a shadow's edge ramps over, low then high.</summary>
        public Vector2 Edge => new(1f - Coverage - Softness, 1f - Coverage + Softness);

        /// <summary>Each set tune held to its <see cref="Field"/>'s range, the heading wrapped into a turn.</summary>
        public Tune Clamped()
        {
            var t = this;
            foreach (var f in Fields)
            {
                if (f.Get(t) is { } v)
                    t = f.With(t, f.Hold(v));
            }
            return t;
        }

        /// <summary>The <paramref name="mission"/> drift turned to <see cref="Heading"/> and set to
        /// <see cref="DriftSpeed"/>, each unset one kept as the mission's.</summary>
        public Vector2 Drift(Vector2 mission) =>
            Toward(Heading ?? HeadingOf(mission), DriftSpeed ?? mission.Length());

        /// <summary>Every tune as <c>key=value</c>, invariant, an unset drift tune as <c>mission</c>.</summary>
        public string Describe()
        {
            var t = this;
            return string.Join(" ", Fields.Select(f => $"{f.Key}={(f.Get(t) is { } v ? f.Show(v) : "mission")}"));
        }
    }

    /// <summary>One tune as the chapters file and the cloud lab name it: its key, label, range, slider
    /// step and display format. It reads and writes a <see cref="Tune"/>.</summary>
    public sealed record Field(string Key, string Label, float Min, float Max, float Step, string Format,
        Func<Tune, float?> Get, Func<Tune, float, Tune> With, bool Wraps = false)
    {
        /// <summary><paramref name="value"/> in the display format, invariant.</summary>
        public string Show(float value) => value.ToString(Format, CultureInfo.InvariantCulture);

        /// <summary><paramref name="value"/> held inside the range, or wrapped into it.</summary>
        public float Hold(float value) => Wraps ? Mathf.PosMod(value, Max) : Math.Clamp(value, Min, Max);
    }
}
