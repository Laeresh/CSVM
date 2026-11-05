using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// The Enhanced cloud shadows' map, with no engine object touched. Per texel it holds the share of the
/// sun the mission's clouds let through, cast along the sun onto the sea-level plane. A point reads the texel its
/// own sun ray crosses at sea level (<see cref="Foot"/>), so one map serves every ground height. Each
/// cloud is a camera-facing card, so the sun sees its authored mask, and overlapping cards multiply what
/// they let through. Every count, size and opacity is the casters' authored data; nothing is tuned per
/// mission. A remake-only rule: the original casts no cloud shadow. <see cref="CloudShadow"/> publishes it.
/// </summary>
public static class CloudShadowRaster
{
    /// <summary>⚠ TUNE, owed a look at the controls under Enhanced. The share of the sun an opaque cloud
    /// takes away. The ambient light is untouched, so the ground darkens by less than this.</summary>
    public const float Darkness = 0.3f;

    /// <summary>The smallest texel edge in metres, a tenth of the smallest shipped card.</summary>
    public const float MinCell = 7f;

    /// <summary>The most texels along either side. A wider field takes larger texels.</summary>
    public const int MaxSide = 2048;

    /// <summary>The lowest sun that casts, as the up part of its unit direction (about 6 degrees). Lower,
    /// a card's shadow would stretch over kilometres.</summary>
    public const float MinSunHeight = 0.1f;

    /// <summary>The metres a sun ray runs along X and Z per metre it rises. A sun below
    /// <see cref="MinSunHeight"/> gives zero. <paramref name="sun"/> points at the sun.</summary>
    public static Vector2 Run(Vector3 sun)
    {
        sun = sun.Normalized();
        return sun.Y >= MinSunHeight ? new Vector2(sun.X / sun.Y, sun.Z / sun.Y) : Vector2.Zero;
    }

    /// <summary>Where the sun ray through <paramref name="world"/> crosses sea level, the X/Z the map is
    /// read at. <c>csky_cloud_sun</c> spells the same.</summary>
    public static Vector2 Foot(Vector3 world, Vector2 run) =>
        new(world.X - (world.Y * run.X), world.Z - (world.Y * run.Y));

    /// <summary>The share of the sun <paramref name="through"/> the clouds lets reach the ground once
    /// <see cref="Darkness"/> applies, as <c>csky_cloud_sun</c> returns it.</summary>
    public static float Sun(float through) => 1f - (Darkness * (1f - through));

    /// <summary>The map of <paramref name="casters"/> under a sun pointing along <paramref name="sun"/>, or
    /// null where none casts: no caster, a sun below <see cref="MinSunHeight"/>, or no opacity.</summary>
    public static Result? Cast(IReadOnlyList<Caster> casters, Vector3 sun)
    {
        sun = sun.Normalized();
        if (casters.Count == 0 || sun.Y < MinSunHeight)
            return null;
        var run = Run(sun);
        // A card faces the sun: its right stays level, its up leans back from the sun.
        var right = Vector3.Up.Cross(sun);
        right = right.LengthSquared() < 1e-8f ? Vector3.Right : right.Normalized();
        var up = sun.Cross(right);
        var across = Flatten(right, run);
        var along = Flatten(up, run);
        float det = (across.X * along.Y) - (across.Y * along.X);
        var lo = new Vector2(float.MaxValue, float.MaxValue);
        var hi = new Vector2(float.MinValue, float.MinValue);
        foreach (var c in casters)
        {
            if (!Casts(c))
                continue;
            var reach = (Abs(across) + Abs(along)) * (c.Size * 0.5f);
            var foot = Foot(c.Centre, run);
            lo = new Vector2(MathF.Min(lo.X, foot.X - reach.X), MathF.Min(lo.Y, foot.Y - reach.Y));
            hi = new Vector2(MathF.Max(hi.X, foot.X + reach.X), MathF.Max(hi.Y, foot.Y + reach.Y));
        }
        if (lo.X > hi.X || MathF.Abs(det) < 1e-6f)
            return null;
        var extent = hi - lo;
        float cell = MathF.Max(MinCell, MathF.Max(extent.X, extent.Y) / MaxSide);
        int width = Math.Max(1, (int)MathF.Ceiling(extent.X / cell));
        int height = Math.Max(1, (int)MathF.Ceiling(extent.Y / cell));
        var through = new float[width * height];
        Array.Fill(through, 1f);
        foreach (var c in casters)
        {
            if (Casts(c))
                Lay(through, width, height, lo, cell, c, Foot(c.Centre, run), across, along, det);
        }
        return new Result(lo, cell, width, height, through);
    }

    /// <summary>The share <paramref name="map"/> lets through at <paramref name="world"/>, read bilinear
    /// between texel centres as the shader reads it, 1 off the map.</summary>
    public static float Through(Result map, Vector3 world, Vector2 run)
    {
        var foot = Foot(world, run);
        float tx = ((foot.X - map.Origin.X) / map.Cell) - 0.5f;
        float ty = ((foot.Y - map.Origin.Y) / map.Cell) - 0.5f;
        if (tx < -0.5f || ty < -0.5f || tx > map.Width - 0.5f || ty > map.Height - 0.5f)
            return 1f;
        int ix = (int)MathF.Floor(tx), iy = (int)MathF.Floor(ty);
        float fx = tx - ix, fy = ty - iy;
        float a = map.At(ix, iy), b = map.At(ix + 1, iy), c = map.At(ix, iy + 1), d = map.At(ix + 1, iy + 1);
        return Lerp(Lerp(a, b, fx), Lerp(c, d, fx), fy);
    }

    // A card direction's run across sea level when its sun ray is followed down.
    private static Vector2 Flatten(Vector3 v, Vector2 run) => new(v.X - (v.Y * run.X), v.Z - (v.Y * run.Y));

    private static bool Casts(Caster c) => c.Size > 0f && c.Opacity > 0f;

    private static Vector2 Abs(Vector2 v) => new(MathF.Abs(v.X), MathF.Abs(v.Y));

    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);

    // Multiplies one card's transmittance into every texel whose centre its shadow covers. A texel maps
    // back to the card's own right/up by inverting the two flattened axes.
    private static void Lay(float[] through, int width, int height, Vector2 origin, float cell, Caster c,
        Vector2 foot, Vector2 across, Vector2 along, float det)
    {
        var reach = (Abs(across) + Abs(along)) * (c.Size * 0.5f);
        int x0 = Math.Max(0, (int)MathF.Floor((foot.X - reach.X - origin.X) / cell));
        int x1 = Math.Min(width - 1, (int)MathF.Floor((foot.X + reach.X - origin.X) / cell));
        int y0 = Math.Max(0, (int)MathF.Floor((foot.Y - reach.Y - origin.Y) / cell));
        int y1 = Math.Min(height - 1, (int)MathF.Floor((foot.Y + reach.Y - origin.Y) / cell));
        float inv = 1f / (c.Size * det);
        for (int y = y0; y <= y1; y++)
        {
            float dy = origin.Y + ((y + 0.5f) * cell) - foot.Y;
            for (int x = x0; x <= x1; x++)
            {
                float dx = origin.X + ((x + 0.5f) * cell) - foot.X;
                float u = ((dx * along.Y) - (dy * along.X)) * inv;
                float v = ((across.X * dy) - (across.Y * dx)) * inv;
                if (u < -0.5f || u > 0.5f || v < -0.5f || v > 0.5f)
                    continue;
                // The image's first row is the card's top.
                float opacity = c.Opacity * c.Mask.Sample(u + 0.5f, 0.5f - v);
                through[(y * width) + x] *= 1f - Math.Clamp(opacity, 0f, 1f);
            }
        }
    }

    /// <summary>One cloud card: its centre in the world, its edge in metres, its authored opacity, and
    /// the mask whose alpha shapes it.</summary>
    public readonly record struct Caster(Vector3 Centre, float Size, float Opacity, Mask Mask);

    /// <summary>A card's alpha, row by row from the top, read bilinear between texel centres.</summary>
    public sealed class Mask
    {
        private readonly float[] _alpha;

        /// <summary>Takes <paramref name="alpha"/>, <paramref name="width"/> by <paramref name="height"/>,
        /// each in [0, 1].</summary>
        public Mask(int width, int height, float[] alpha)
        {
            if (width < 1 || height < 1 || alpha.Length != width * height)
                throw new ArgumentException($"a {width}x{height} mask needs {width * height} values, not {alpha.Length}");
            Width = width;
            Height = height;
            _alpha = alpha;
        }

        /// <summary>A card opaque from edge to edge.</summary>
        public static Mask Solid { get; } = new(1, 1, new[] { 1f });

        public int Width { get; }

        public int Height { get; }

        /// <summary>The alpha at card UV (<paramref name="s"/>, <paramref name="t"/>), both in [0, 1].</summary>
        public float Sample(float s, float t)
        {
            float tx = (s * Width) - 0.5f, ty = (t * Height) - 0.5f;
            int ix = (int)MathF.Floor(tx), iy = (int)MathF.Floor(ty);
            float fx = tx - ix, fy = ty - iy;
            return Lerp(Lerp(At(ix, iy), At(ix + 1, iy), fx), Lerp(At(ix, iy + 1), At(ix + 1, iy + 1), fx), fy);
        }

        private float At(int x, int y) =>
            _alpha[(Math.Clamp(y, 0, Height - 1) * Width) + Math.Clamp(x, 0, Width - 1)];
    }

    /// <summary>A cast map: texel (x, y)'s share of the sun let through, its corner at
    /// <see cref="Origin"/> plus <see cref="Cell"/> times (x, y) on the sea-level plane.</summary>
    public sealed record Result(Vector2 Origin, float Cell, int Width, int Height, float[] Through)
    {
        /// <summary>The extent in metres along X and Z.</summary>
        public Vector2 Size => new(Width * Cell, Height * Cell);

        /// <summary>Texel (<paramref name="x"/>, <paramref name="y"/>), clamped to the edge.</summary>
        public float At(int x, int y) =>
            Through[(Math.Clamp(y, 0, Height - 1) * Width) + Math.Clamp(x, 0, Width - 1)];
    }
}
