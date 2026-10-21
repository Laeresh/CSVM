using System;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The Rich vegetation's per-stamp variation for one listed kind (<see cref="VegetationKind"/>).
/// A jitter in texture space is clamped back into the source triangle. A horizontal mirror stands
/// in for the yaw a card cannot show, and a tint rides the MultiMesh instance colour. It is pure;
/// the drawing is in docs/architecture/Mech3.md, the Clutter.cs entry.
/// ⚠ Draw only from the stream <see cref="Seed"/> seeds, never the substitute, scale or fade streams.
/// One extra draw there moves every later species and scale of the Original stamp.
/// </summary>
public static class VegetationVariation
{
    /// <summary>Draws one stamp takes whatever its values, so a value edit never reshuffles another stamp.</summary>
    public const int DrawsPerStamp = 8;

    // The Rich stream's label beside the placement seed, so it never replays the Original's draws.
    private const int RichStream = 0x2C3B9A4D;

    // A clamp halves the jitter until the stamp is inside; past this many halvings it stays unmoved.
    private const int MaxHalvings = 10;

    /// <summary>The Rich stream's seed from the placement seed and the chapter's salt. Fixed
    /// arithmetic, never a runtime hash, so it is identical on every launch and machine.</summary>
    public static int Seed(int placementSeed, int salt) =>
        unchecked((placementSeed ^ RichStream) + (salt * (int)0x9E3779B1));

    /// <summary>The variation of the stamp at texture coordinate (<paramref name="u"/>,
    /// <paramref name="v"/>) of <paramref name="tri"/>, which must contain it. The jitter moves it
    /// by up to <c>JitterUv</c> per UV axis, halved back inside the triangle where it would leave.
    /// A stamp mirrors with probability <c>YawDeg</c>/180.</summary>
    public static Stamp Draw(ClutterBuilder.UvTriangle tri, double u, double v, VegetationKind kind, Random rng)
    {
        double du = Spread(rng) * kind.JitterUv;
        double dv = Spread(rng) * kind.JitterUv;
        bool mirror = rng.NextDouble() < kind.YawDeg / 180.0;
        float brightness = 1f + (kind.Brightness * (float)Spread(rng));
        float r = brightness * (1f + (kind.Tint * (float)Spread(rng)));
        float g = brightness * (1f + (kind.Tint * (float)Spread(rng)));
        float b = brightness * (1f + (kind.Tint * (float)Spread(rng)));
        // The brush mask's keep draw, taken whatever the mask holds so a mask edit reshuffles nothing.
        double keep = rng.NextDouble();

        // The triangle is convex and holds (u, v), so a shorter step along the same jitter stays inside.
        double t = 1.0;
        for (int h = 1; !tri.Contains(u + (t * du), v + (t * dv)); h++)
            t = h <= MaxHalvings ? t * 0.5 : 0.0;
        return new Stamp(u + (t * du), v + (t * dv), mirror, new Color(r, g, b), t < 1.0, keep);
    }

    // A symmetric unit draw in [-1, 1).
    private static double Spread(Random rng) => (2.0 * rng.NextDouble()) - 1.0;

    /// <summary>One stamp's variation: its texture coordinate, its mirror and its instance colour.
    /// <c>Clamped</c> says the jitter was shortened to stay in the triangle. <c>Keep</c> is the uniform
    /// draw in [0, 1) the brush mask keeps or drops the stamp by (<see cref="VegetationMask.Keeps"/>).</summary>
    public readonly record struct Stamp(double U, double V, bool Mirror, Color Tint, bool Clamped, double Keep);
}
