using Godot;

namespace CSVM.Flight;

/// <summary>The off-screen edge marker's placement rules, the one home for what
/// VersusHud and TargetHud each used to carry privately (docs/architecture.md): a world target's
/// marker sits at its projected point on screen, else clamps to the margin-inset screen edge with
/// an outward arrow direction; the edge tag carries a clock-hour bearing. Engine-free, the
/// caller projects through its own camera and passes the result in, and each HUD keeps its own
/// arrow, tag and label styling, which legitimately differs.</summary>
public static class EdgeMarker
{
    /// <summary>Keep edge markers this far off the screen border (1440p reference pixels,
    /// callers scale by HudMetrics before passing a margin to <see cref="Resolve"/>).</summary>
    public const float RefEdgeMargin = 46f;

    /// <summary>Resolves a projected target to its marker placement. <paramref name="behind"/> is
    /// the caller's <c>Camera3D.IsPositionBehind</c> answer: the projection of a point behind the
    /// camera is mirrored through centre, so it is forced off screen and its direction flipped
    /// back. A projection landing on centre (dead ahead or dead astern) points down.</summary>
    public static Placement Resolve(Vector2 projected, bool behind, Vector2 paneSize, float margin)
    {
        var inner = new Rect2(margin, margin, paneSize.X - 2f * margin, paneSize.Y - 2f * margin);
        if (!behind && inner.HasPoint(projected))
        {
            return new Placement(true, projected, Vector2.Zero);
        }

        var center = paneSize / 2f;
        var dir = projected - center;
        if (behind)
        {
            dir = -dir;
        }

        dir = dir.LengthSquared() < 1f ? Vector2.Down : dir.Normalized();
        return new Placement(false, EdgePoint(center, dir, margin), dir);
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

    // Screen-edge point along `dir` from centre, inset by the margin.
    private static Vector2 EdgePoint(Vector2 center, Vector2 dir, float margin)
    {
        float hx = center.X - margin, hy = center.Y - margin;
        float tx = Mathf.Abs(dir.X) > 1e-4f ? hx / Mathf.Abs(dir.X) : float.MaxValue;
        float ty = Mathf.Abs(dir.Y) > 1e-4f ? hy / Mathf.Abs(dir.Y) : float.MaxValue;
        return center + dir * Mathf.Min(tx, ty);
    }

    /// <summary>Where a marker goes. On screen: <see cref="Anchor"/> is the projected point and
    /// <see cref="Dir"/> is zero. Off screen: <see cref="Anchor"/> sits on the margin-inset pane
    /// boundary and <see cref="Dir"/> is the normalized outward direction the arrow points
    /// along.</summary>
    public readonly record struct Placement(bool OnScreen, Vector2 Anchor, Vector2 Dir);
}
