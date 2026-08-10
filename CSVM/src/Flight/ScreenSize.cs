namespace CSVM.Flight;

using System.Collections.Generic;
using Godot;

/// <summary>Screen-space sizing math for world-space sprites, split out from
/// <see cref="ProjectilePool"/> so the splitscreen rule it feeds can be unit-tested without a live
/// Godot camera.</summary>
public static class ScreenSize
{
    /// <summary>The minimum world-space size (m) that projects to <paramref name="pixels"/> on
    /// screen at <paramref name="distance"/> from a camera — the inverse of Godot's default vertical
    /// (KEEP_HEIGHT) perspective projection, whose forward form is
    /// <c>screenPx = worldSize * viewportHeight / (2 * distance * tan(fov/2))</c>.
    /// Returns 0 for a degenerate distance, pixel target or viewport, which callers read as
    /// "no floor" rather than as a size.</summary>
    /// <param name="pixels">The screen footprint to guarantee.</param>
    /// <param name="distance">Camera-to-object distance, m.</param>
    /// <param name="fovDeg">The camera's vertical field of view, degrees.</param>
    /// <param name="viewportHeight">The camera's OWN viewport height in pixels — a splitscreen pane
    /// is shorter than the window, and using the window's height there inflates the result by the
    /// pane factor.</param>
    public static float MinWorldSizeForPixels(float pixels, float distance, float fovDeg, float viewportHeight)
    {
        if (distance <= 0f || pixels <= 0f || viewportHeight <= 0f)
        {
            return 0f;
        }

        return pixels * 2f * distance * Mathf.Tan(Mathf.DegToRad(fovDeg) * 0.5f) / viewportHeight;
    }

    /// <summary>The floor to apply to ONE shared world-space mesh seen by several viewers: the
    /// SMALLEST size that meets <paramref name="pixels"/> for any of them — the nearest viewer's.
    ///
    /// <para>Minimum, not maximum, and that is the whole point. A world-space size satisfies exactly
    /// one distance; in every closer view it over-covers by the ratio of the distances. Sizing for
    /// the FARTHEST viewer therefore inflates the object in every nearer pane (the splitscreen bug),
    /// while sizing for the nearest can only leave it under-floored further away, which is simply the
    /// un-floored look. Degenerate viewers (zero distance or no viewport) are skipped; an empty or
    /// wholly degenerate set means no floor at all.</para></summary>
    public static float NearestFloor(float pixels, IReadOnlyList<ViewerSample> viewers)
    {
        float best = 0f;
        bool any = false;
        for (int i = 0; i < viewers.Count; i++)
        {
            var v = viewers[i];
            float size = MinWorldSizeForPixels(pixels, v.Distance, v.FovDeg, v.ViewportHeight);
            if (size <= 0f)
            {
                continue;
            }

            if (!any || size < best)
            {
                best = size;
                any = true;
            }
        }

        return best;
    }

    /// <summary>One viewer's projection inputs for <see cref="NearestFloor"/>: how far it is from
    /// the object, and the FOV and pane height it would draw that object through.</summary>
    public readonly record struct ViewerSample(float Distance, float FovDeg, float ViewportHeight);
}
