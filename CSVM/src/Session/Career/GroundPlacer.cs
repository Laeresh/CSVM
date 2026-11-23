using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CSVM.Session.Career;

/// <summary>
/// The placer's read of the built world, so the rules unit-test over a hand-made ground. The
/// session's adapter answers from the world query and the ocean mask. A sample is null off the
/// world. <see cref="Occupied"/> says whether an original node stands near a ground point.
/// </summary>
public interface IGroundProbe
{
    GroundSample? Sample(float x, float z);

    bool Occupied(float x, float z, float radius);
}

/// <summary>What the ground is at one point: its height, and whether it is water.</summary>
public readonly record struct GroundSample(float Height, bool Water);

/// <summary>A target the placer stood on the ground: its prefab, position and yaw.</summary>
public readonly record struct PlacedTarget(string Prefab, Vector3 Position, float YawDeg, int Value);

/// <summary>The placer's answer: what stands, and the requests it had to drop.</summary>
public sealed record PlacementResult(IReadOnlyList<PlacedTarget> Placed, IReadOnlyList<TargetPlacement> Dropped)
{
    /// <summary>Whether every target that is not flak was dropped, which leaves a strike nothing to
    /// strike; the caller then rejects the spec.</summary>
    public bool NoTargets => !Placed.Any(p => p.Prefab != MissionGenerator.FlakPrefab);
}

/// <summary>
/// Stands a spec's ground targets on the built world. A spot must be land, flat within the slope
/// limit across the prefab's footprint, clear of the original's nodes, and clear of targets already
/// placed. A spot that fails is searched outward in rings; a target with no good spot nearby is
/// dropped, never floated or buried. Every draw comes from the spec's seed.
/// </summary>
public static class GroundPlacer
{
    /// <summary>Points sampled around a footprint's edge, and bearings tried on each search ring.</summary>
    public const int RingPoints = 8;

    /// <summary>Places every target of <paramref name="spec"/> in order, targets before flak.</summary>
    public static PlacementResult Place(MissionSpec spec, IGroundProbe ground, CareerTunes? tunes = null)
    {
        tunes ??= CareerTunes.Default;
        var dice = CareerDice.For(spec.Seed, "placement");
        var placed = new List<PlacedTarget>();
        var dropped = new List<TargetPlacement>();
        int index = 0;
        foreach (var target in spec.Targets.OrderBy(t => t.Prefab == MissionGenerator.FlakPrefab))
        {
            float footprint = Footprint(target.Prefab, tunes);
            if (FindSpot(target.X, target.Z, footprint, ground, placed, tunes) is { } spot)
            {
                float yaw = (float)(dice.Unit(index) * 360.0);
                placed.Add(new PlacedTarget(target.Prefab, spot, yaw, target.Value));
            }
            else
            {
                dropped.Add(target);
            }

            index++;
        }

        return new PlacementResult(placed, dropped);
    }

    /// <summary>The footprint radius of <paramref name="prefab"/> in metres.</summary>
    public static float Footprint(string prefab, CareerTunes tunes) =>
        tunes.PrefabFootprints.TryGetValue(prefab, out float r) ? r : tunes.DefaultFootprint;

    /// <summary>Whether a footprint of <paramref name="radius"/> at (x, z) may hold a target, and
    /// the ground point it stands on. The height is the lowest sample, so no corner floats.</summary>
    public static Vector3? Fits(float x, float z, float radius, IGroundProbe ground, CareerTunes tunes)
    {
        if (ground.Sample(x, z) is not { Water: false } centre)
        {
            return null;
        }

        float low = centre.Height;
        float high = centre.Height;
        for (int i = 0; i < RingPoints; i++)
        {
            double a = Math.Tau * i / RingPoints;
            if (ground.Sample(x + (radius * (float)Math.Sin(a)), z + (radius * (float)Math.Cos(a))) is not { Water: false } edge)
            {
                return null;
            }

            low = Math.Min(low, edge.Height);
            high = Math.Max(high, edge.Height);
        }

        // Rise across the whole footprint against the rise the slope limit allows over it.
        float allowed = 2f * radius * MathF.Tan(Mathf.DegToRad(tunes.MaxPlacementSlopeDeg));
        if (high - low > allowed || ground.Occupied(x, z, radius))
        {
            return null;
        }

        return new Vector3(x, low, z);
    }

    private static Vector3? FindSpot(float x, float z, float radius, IGroundProbe ground, List<PlacedTarget> placed, CareerTunes tunes)
    {
        for (float shift = 0f; shift <= tunes.MaxPlacementShift; shift += tunes.PlacementSearchStep)
        {
            int bearings = shift == 0f ? 1 : RingPoints;
            for (int i = 0; i < bearings; i++)
            {
                double a = Math.Tau * i / RingPoints;
                float px = x + (shift * (float)Math.Sin(a));
                float pz = z + (shift * (float)Math.Cos(a));
                if (Apart(px, pz, radius, placed, tunes) && Fits(px, pz, radius, ground, tunes) is { } spot)
                {
                    return spot;
                }
            }
        }

        return null;
    }

    private static bool Apart(float x, float z, float radius, List<PlacedTarget> placed, CareerTunes tunes) =>
        placed.All(p =>
        {
            float min = radius + Footprint(p.Prefab, tunes);
            float dx = p.Position.X - x;
            float dz = p.Position.Z - z;
            return (dx * dx) + (dz * dz) >= min * min;
        });
}
