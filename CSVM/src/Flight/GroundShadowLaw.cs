using System;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// The original's projected aircraft ground shadow as a rule: which way it projects, where it
/// lands, how large, how dark, and when it is not drawn at all. Every constant here is decoded
/// from the retail executable and carries its address in
/// <see href="../../../docs/org/shadows.md">docs/org/shadows.md</see>; nothing is fitted. The
/// drawing half is <see cref="GroundShadowPass"/>, which is also where the remake-only rules live.
/// </summary>
public static class GroundShadowLaw
{
    /// <summary>Horizontal distance out to which another aircraft's shadow is at full strength.
    /// Pushed per caller by the shadow pass, with the Spruce Goose taking 300 instead.</summary>
    public const float NearDistance = 1f;

    /// <summary>Horizontal distance at which another aircraft's shadow is gone; the Spruce
    /// Goose takes 600. The player's own aircraft is exempt from the distance fade.</summary>
    public const float FarDistance = 200f;

    /// <summary>Height above the ground up to which the shadow is at full strength and its
    /// footprint untouched. The Spruce Goose takes 180.</summary>
    public const float FullShadowAltitude = 60f;

    /// <summary>Height above the ground at which the shadow is dropped outright rather than
    /// drawn transparent. The Spruce Goose takes 750.</summary>
    public const float CutoffAltitude = 250f;

    /// <summary>How far the player's own shadow direction is skewed along the horizontal part of
    /// its nose, which lands the shadow this many times its altitude ahead of it.</summary>
    public const float PlayerSkew = 1.5f;

    /// <summary>What the player's own footprint grows to at <see cref="CutoffAltitude"/>; every
    /// other aircraft's stays at 1.</summary>
    public const float PlayerMaxScale = 3f;

    /// <summary>The fixed factor on the darkening term, the one number in the colour that is
    /// neither authored nor derived.</summary>
    public const float Darkening = 0.8f;

    /// <summary>The live shadow texture's edge, in texels: the raster, the spread and the ramp
    /// all run over a buffer this size.</summary>
    public const int TextureSize = 32;

    /// <summary>Entries in the colour ramp the spread indexes, white at 0 and the shadow colour
    /// at 10.</summary>
    public const int RampSteps = 11;

    // What the silhouette raster writes into a covered texel, and what the spread adds to a
    // covered texel and to each of its eight neighbours. The cover mark is odd and both addends
    // are even, so a texel stays readable as covered however much its neighbours have added.
    private const int CoverMark = 1;
    private const int SpreadSelf = 4;
    private const int SpreadNeighbour = 2;

    /// <summary>The direction the shadow projects along. Straight down for every aircraft, that
    /// being the <c>SHADOW_ANGLES</c> all 53 shipped weather files author, with the player's own
    /// skewed along its nose so the shadow runs ahead of it.</summary>
    public static Vector3 Direction(bool isPlayer, Vector3 nose)
    {
        var dir = Vector3.Down;
        if (!isPlayer)
            return dir;
        // The horizontal part of the nose only: the vertical component is left alone, so the
        // projection ratio becomes the skew itself. ⚠ The original subtracts row 2 of the
        // orientation matrix, and that row is MINUS the nose (docs/org/flightModel.md).
        var skewed = new Vector3(
            dir.X + (nose.X * PlayerSkew), dir.Y, dir.Z + (nose.Z * PlayerSkew));
        return skewed.Normalized();
    }

    /// <summary>The distance fade, linear in SQUARED horizontal distance from the player, so it
    /// is weighted hard toward the far end. Vertical separation does not enter it.</summary>
    public static float DistanceFactor(Vector3 position, Vector3 player)
    {
        float dx = position.X - player.X;
        float dz = position.Z - player.Z;
        float d2 = (dx * dx) + (dz * dz);
        float near2 = NearDistance * NearDistance;
        float far2 = FarDistance * FarDistance;
        if (d2 <= near2)
            return 1f;
        return d2 >= far2 ? 0f : (far2 - d2) / (far2 - near2);
    }

    /// <summary>The altitude ramp: full below <see cref="FullShadowAltitude"/>, gone at
    /// <see cref="CutoffAltitude"/>, linear between.</summary>
    public static float AltitudeFactor(float altitude)
    {
        if (altitude <= FullShadowAltitude)
            return 1f;
        return altitude >= CutoffAltitude
            ? 0f
            : (CutoffAltitude - altitude) / (CutoffAltitude - FullShadowAltitude);
    }

    /// <summary>What the footprint is scaled by about its projected origin: 1 for every aircraft
    /// but the player's own, which spreads to <see cref="PlayerMaxScale"/> as it climbs.</summary>
    public static float FootprintScale(bool isPlayer, float altitudeFactor) =>
        isPlayer ? PlayerMaxScale - (2f * altitudeFactor) : 1f;

    /// <summary>One point flattened onto the ground plane along <paramref name="direction"/>,
    /// which is how both the shadow's origin and each corner of its footprint are placed.</summary>
    public static Vector3 Project(Vector3 point, float groundY, Vector3 direction)
    {
        float travel = (groundY - point.Y) / direction.Y;
        return new Vector3(
            point.X + (travel * direction.X), groundY, point.Z + (travel * direction.Z));
    }

    /// <summary>The shadow colour as a multiplier on the ground, per channel, faded toward white
    /// by strength. It is the light the aircraft blocks, so it follows the mission's authored
    /// <c>SUNLIGHT_DIFFUSE</c>/<c>SUNLIGHT_AMBIENT</c> pair and lightens as ambient rises.
    /// Returned in the original's own gamma space, one unit being its 255.</summary>
    public static Color Colour(Vector3 diffuse, Vector3 ambient, Vector3 direction, float strength)
        => new(
            Channel(diffuse.X, ambient.X, direction.Y, strength),
            Channel(diffuse.Y, ambient.Y, direction.Y, strength),
            Channel(diffuse.Z, ambient.Z, direction.Y, strength));

    /// <summary>The original's fixed spread over a coverage mask, in place: every covered texel
    /// adds to itself and to each of its eight neighbours. The border ring is never scanned, so
    /// a silhouette touching the edge spreads inward only. Returns the ramp index per texel.
    /// </summary>
    public static int[] Spread(bool[] covered, int width, int height)
    {
        var acc = new int[covered.Length];
        for (int i = 0; i < covered.Length; i++)
            acc[i] = covered[i] ? CoverMark : 0;
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                int at = (y * width) + x;
                if ((acc[at] & 1) == 0)
                    continue;
                acc[at] += SpreadSelf;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx != 0 || dy != 0)
                            acc[at + (dy * width) + dx] += SpreadNeighbour;
                    }
                }
            }
        }

        for (int i = 0; i < acc.Length; i++)
            acc[i] = Math.Min(acc[i] / 2, RampSteps - 1);
        return acc;
    }

    /// <summary>Where a ramp index sits between white and the shadow colour. The original's
    /// 11-entry table is exactly this mix, built once per frame per channel.</summary>
    public static float Coverage(int rampIndex) => rampIndex / (float)(RampSteps - 1);

    // One channel of the colour. k is the physical ratio "ambient alone" over "ambient plus
    // diffuse at normal incidence" for flat ground. ⚠ Keep the zero guards: with no ambient at
    // all, or a denominator that cancels, the original falls back to the raw ambient value
    // rather than to a ratio, which is the darkest shadow that channel can take.
    private static float Channel(float diffuse, float ambient, float directionY, float strength)
    {
        float denominator = ambient - (diffuse * directionY);
        float k = ambient == 0f || denominator == 0f ? ambient : ambient / denominator;
        return Math.Clamp((1f - strength) + (Darkening * strength * k), 0f, 1f);
    }
}
