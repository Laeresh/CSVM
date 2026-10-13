using System;
using System.Collections.Generic;

namespace CSVM.UI.Boards;

/// <summary>
/// Where a pad or arrow press moves a cursor among rectangles drawn on a board. It reads where they
/// stand, not the order they were built in. Every Original screen and the pause sheet step
/// through this one rule, so a row added later cannot reorder a screen. Engine-free.
/// </summary>
public static class SpatialFocus
{
    // Remake-only rule: ahead outside the band, the gap across a press counts double the gap
    // along it. A row level with the cursor then beats a nearer one far off its line. Right from
    // Instant Action's lower contents rows takes the dropdown beside them, not Change Weapons below.
    private const float AcrossGapWeight = 2f;

    /// <summary>The stop a press moves to from <paramref name="from"/>, which stands at
    /// <paramref name="at"/>. A null stop takes no focus. Vertical wins a diagonal. The nearest
    /// stop ahead in the same band, else the band's far end, else the nearest ahead anywhere, else
    /// the farthest behind. A band is the stops overlapping <paramref name="at"/> across the move,
    /// and <see cref="Ahead"/> says which way a stop lies.</summary>
    public static int Step(
        IReadOnlyList<(float X, float Y, float Width, float Height)?> stops, int from,
        (float X, float Y, float Width, float Height) at, int moveX, int moveY)
    {
        ArgumentNullException.ThrowIfNull(stops);
        if (moveX == 0 && moveY == 0)
        {
            return from;
        }

        bool vertical = moveY != 0;
        int dir = Math.Sign(vertical ? moveY : moveX);
        return Nearest(stops, from, at, vertical, dir, banded: true, ahead: true)
            ?? Nearest(stops, from, at, vertical, dir, banded: true, ahead: false)
            ?? Nearest(stops, from, at, vertical, dir, banded: false, ahead: true)
            ?? Nearest(stops, from, at, vertical, dir, banded: false, ahead: false)
            ?? from;
    }

    /// <summary>The stop a press leaving a band moves to. The centre decides which stops lie
    /// ahead, the edge gap ranks the nearest as in <see cref="Step"/>, else the farthest behind by
    /// centre. A remake-only rule, so a press out of a column reaches a button level with its foot
    /// (docs/menu-presentations.md). The caller drops the band's own stops first.</summary>
    public static int Leave(
        IReadOnlyList<(float X, float Y, float Width, float Height)?> stops, int from,
        (float X, float Y, float Width, float Height) at, int moveX, int moveY)
    {
        ArgumentNullException.ThrowIfNull(stops);
        if (moveX == 0 && moveY == 0)
        {
            return from;
        }

        bool vertical = moveY != 0;
        int dir = Math.Sign(vertical ? moveY : moveX);
        return Nearest(stops, from, at, vertical, dir, banded: false, ahead: true, byCentre: true)
            ?? Nearest(stops, from, at, vertical, dir, banded: false, ahead: false, byCentre: true)
            ?? from;
    }

    /// <summary>Whether <paramref name="to"/> lies in the pressed direction from
    /// <paramref name="at"/>: wholly past its middle, or, for two rectangles that overlap, with its
    /// centre past (the scrapbook's photographs). Vertical wins a diagonal. A row below the cursor
    /// and a little to one side is then not beside it.</summary>
    public static bool Ahead(
        (float X, float Y, float Width, float Height) at, (float X, float Y, float Width, float Height) to,
        int moveX, int moveY) =>
        Lies(at, to, moveY != 0, Math.Sign(moveY != 0 ? moveY : moveX), byCentre: false);

    /// <summary>Whether two rectangles overlap across a press, which puts them in one band.</summary>
    public static bool InBand(
        (float X, float Y, float Width, float Height) at, (float X, float Y, float Width, float Height) to,
        bool vertical) =>
        vertical
            ? to.X < at.X + at.Width && at.X < to.X + to.Width
            : to.Y < at.Y + at.Height && at.Y < to.Y + to.Height;

    /// <summary>A rectangle's centre.</summary>
    public static (float X, float Y) Centre((float X, float Y, float Width, float Height) box) =>
        (box.X + (box.Width / 2f), box.Y + (box.Height / 2f));

    // Whether rectangle to stands ahead of rectangle at in direction dir: wholly past at's middle,
    // or by centre where the two overlap or byCentre asks. A remake-only rule: the middle keeps a
    // press off a row a little to one side in the same column (docs/menu-presentations.md).
    private static bool Lies(
        (float X, float Y, float Width, float Height) at, (float X, float Y, float Width, float Height) to,
        bool vertical, int dir, bool byCentre)
    {
        float mid = Mid(at, vertical);
        if (byCentre || (InBand(at, to, vertical) && InBand(at, to, !vertical)))
        {
            return (Mid(to, vertical) - mid) * dir > 0f;
        }

        var (min, max) = Span(to, vertical);
        return dir > 0 ? min > mid : max < mid;
    }

    private static (float Min, float Max) Span((float X, float Y, float Width, float Height) box, bool vertical) =>
        vertical ? (box.Y, box.Y + box.Height) : (box.X, box.X + box.Width);

    private static float Mid((float X, float Y, float Width, float Height) box, bool vertical) =>
        vertical ? box.Y + (box.Height / 2f) : box.X + (box.Width / 2f);

    // The empty space between two spans, zero where they meet or overlap.
    private static float Gap((float Min, float Max) a, (float Min, float Max) b) =>
        Math.Max(0f, Math.Max(a.Min, b.Min) - Math.Min(a.Max, b.Max));

    // One pass of Step's or Leave's search. In a band and on the wraps the signed centre distance
    // ranks, so the nearest ahead and the farthest behind are both its minimum. Ahead outside the
    // band, the gap between edges ranks, the gap across weighted by AcrossGapWeight. Across, then
    // index, settle a tie.
    private static int? Nearest(
        IReadOnlyList<(float X, float Y, float Width, float Height)?> stops, int from,
        (float X, float Y, float Width, float Height) at, bool vertical, int dir, bool banded, bool ahead,
        bool byCentre = false)
    {
        int? best = null;
        (float Rank, float Across) bestKey = default;
        for (int i = 0; i < stops.Count; i++)
        {
            if (i == from || stops[i] is not { } stop)
            {
                continue;
            }

            if (!Lies(at, stop, vertical, ahead ? dir : -dir, byCentre) || (banded && !InBand(at, stop, vertical)))
            {
                continue;
            }

            float across = Math.Abs(Mid(stop, !vertical) - Mid(at, !vertical));
            float rank = !banded && ahead
                ? Gap(Span(at, vertical), Span(stop, vertical)) + (AcrossGapWeight * Gap(Span(at, !vertical), Span(stop, !vertical)))
                : (Mid(stop, vertical) - Mid(at, vertical)) * dir;
            if (best is null || rank < bestKey.Rank || (rank == bestKey.Rank && across < bestKey.Across))
            {
                best = i;
                bestKey = (rank, across);
            }
        }

        return best;
    }
}
