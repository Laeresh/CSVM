using Godot;

namespace CSVM.Flight;

/// <summary>The off-screen edge marker's placement rules, the one home for what
/// VersusHud and TargetHud each used to carry privately (docs/architecture.md): a world target's
/// marker sits at its projected point on screen, else clamps into the inset screen edge with an
/// outward arrow direction and a second point on the pane boundary for the arrow to reach; the
/// edge tag carries a clock-hour bearing. Engine-free, the caller projects through its own camera
/// and passes the result in, and each HUD keeps its own arrow, tag and label styling.
/// Decode: <see href="../../docs/org/spyglass.md">org/spyglass.md</see>.</summary>
public static class EdgeMarker
{
    /// <summary>The share of each pane axis the marker anchor is held inside, the original's own
    /// per-axis 5 percent (docs/org/spyglass.md "Where the anchor is"). A fraction rather than a
    /// reference-pixel count, so nothing scales it by HudMetrics.</summary>
    public const float InsetFraction = 0.05f;

    // The arrow's tip clamps to the pane less this at the far corner, the original's own 1.001
    // device pixels, so a tip never lands on the boundary its viewport rectangle excludes.
    private const float TipInset = 1.001f;

    /// <summary>Resolves a projected target to its marker placement. <paramref name="behind"/> is
    /// the caller's <c>Camera3D.IsPositionBehind</c> answer: the projection of a point behind the
    /// camera is mirrored through centre, so it is forced off screen and its direction flipped
    /// back. A projection landing on centre (dead ahead or dead astern) points down.</summary>
    public static Placement Resolve(Vector2 projected, bool behind, Vector2 paneSize)
    {
        var inset = paneSize * InsetFraction;
        var inner = new Rect2(inset, paneSize - 2f * inset);
        if (!behind && inner.HasPoint(projected))
        {
            return new Placement(true, projected, Vector2.Zero, projected);
        }

        var center = paneSize / 2f;
        var dir = projected - center;
        if (behind)
        {
            dir = -dir;
        }

        dir = dir.LengthSquared() < 1f ? Vector2.Down : dir.Normalized();
        var pane = new Rect2(Vector2.Zero, paneSize - new Vector2(TipInset, TipInset));
        // A target still inside the pane keeps its own point as the tip, which is what shortens
        // the arrow to almost nothing as it crosses the inset boundary.
        var tip = !behind && pane.HasPoint(projected) ? projected : ToBoundary(pane, center, dir);
        return new Placement(false, ToBoundary(inner, center, dir), dir, tip);
    }

    /// <summary>Relative bearing of <paramref name="targetPos"/> from the pilot's own heading in
    /// clock hours (12 = ahead, 3 = right, 6 = behind, 9 = left), the original's "N o'clock"
    /// suffix. Heading and bearing convention: 0 = north (−Z), 90 = east (+X).</summary>
    public static int ClockHour(Vector3 ownPos, float headingDeg, Vector3 targetPos)
    {
        var d = targetPos - ownPos;
        float bearing = Mathf.RadToDeg(Mathf.Atan2(d.X, -d.Z));
        float rel = Mathf.PosMod(bearing - headingDeg, 360f);
        int h = Mathf.RoundToInt(rel / 30f) % 12;
        return h == 0 ? 12 : h;
    }

    // Walk from `from` along `dir` to the first wall of `rect`. The original steps out from the
    // rectangle's centre rather than clamping each axis on its own, which is what keeps both
    // clamped points on the marker's own bearing.
    private static Vector2 ToBoundary(Rect2 rect, Vector2 from, Vector2 dir)
    {
        float t = float.MaxValue;
        if (Mathf.Abs(dir.X) > 1e-4f)
        {
            t = Mathf.Min(t, ((dir.X > 0f ? rect.End.X : rect.Position.X) - from.X) / dir.X);
        }

        if (Mathf.Abs(dir.Y) > 1e-4f)
        {
            t = Mathf.Min(t, ((dir.Y > 0f ? rect.End.Y : rect.Position.Y) - from.Y) / dir.Y);
        }

        return t == float.MaxValue ? from : from + dir * t;
    }

    /// <summary>Where a marker goes. On screen: <see cref="Anchor"/> and <see cref="Tip"/> are the
    /// projected point and <see cref="Dir"/> is zero. Off screen: <see cref="Anchor"/> sits on the
    /// inset boundary and carries the marker and its label, <see cref="Tip"/> is the same point
    /// clamped to the pane itself and is where the arrow points, and <see cref="Dir"/> is the
    /// normalized outward direction from the pane's centre.</summary>
    public readonly record struct Placement(bool OnScreen, Vector2 Anchor, Vector2 Dir, Vector2 Tip);
}
