using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Hud;

/// <summary>
/// Where a HUD layout puts each instrument on a pane, engine-free and stateless. Every rectangle is in
/// pane pixels, from the reading box, the pane size and the HUD scale (<see cref="HudMetrics"/>). A
/// null layout is the original's own formulas, so <see cref="Shift"/> is exactly zero for it. A layout
/// measures in reference pixels from the reading box, or the pane's edges for a custom one, and keeps
/// every element inside the pane. The preset is <see cref="Sides"/>, the editor's first draft
/// <see cref="OriginalLayout"/>; docs/architecture/Flight.md says what to read next.
/// </summary>
public static class HudLayoutPlacement
{
    /// <summary>The Sides preset's border from the reading box's edge, in reference pixels: the
    /// columns hug the sides and stand centred on the pane's height.</summary>
    public const float SidesMargin = 24f;

    /// <summary>The ready-made Sides preset. Both dial columns stand against the reading box's sides,
    /// centred on its height. The plate stands inboard of the right column, level with the speedometer,
    /// and the tape at the top right. The centre and the bottom of the pane stay clear.</summary>
    public static readonly HudLayout Sides = new(new[]
    {
        new HudPlacement(HudAnchor.Start, HudAnchor.Centre, SidesMargin, 0f),
        new HudPlacement(HudAnchor.End, HudAnchor.Centre, -SidesMargin, 0f),
        new HudPlacement(HudAnchor.End, HudAnchor.Start, -SidesMargin, CompassTape.RefTopMargin),
        new HudPlacement(HudAnchor.End, HudAnchor.Centre, -(SidesMargin + GaugeCluster.RightColumnWidth + ThrottlePlate.GapRef), 0f),
    });

    /// <summary>The original's placement spelt as a layout, which the editor starts from while no
    /// custom layout is saved. It matches the original on every pane 16:9 or wider. A narrower pane's
    /// column outdent is the one rule a layout does not carry.</summary>
    public static readonly HudLayout OriginalLayout = new(new[]
    {
        new HudPlacement(HudAnchor.Start, HudAnchor.End, GaugeCluster.LeftColumnMargin, GaugeCluster.ColumnBottomY - HudMetrics.ReferenceHeight),
        new HudPlacement(HudAnchor.End, HudAnchor.End, -GaugeCluster.RightColumnMargin, GaugeCluster.ColumnBottomY - HudMetrics.ReferenceHeight),
        new HudPlacement(HudAnchor.Centre, HudAnchor.Start, 0f, CompassTape.RefTopMargin),
        new HudPlacement(HudAnchor.End, HudAnchor.End,
            -(GaugeCluster.SpdCenterFromRight + GaugeCluster.SpdRadius + ThrottlePlate.GapRef),
            GaugeCluster.SpdCenterY + GaugeCluster.SpdRadius - HudMetrics.ReferenceHeight),
    });

    /// <summary>The placement a layout kind names. Original is null, drawn by the original's own
    /// formulas. Custom is the saved placement, or the original's until one is saved.</summary>
    public static HudLayout? Resolve(HudLayoutKind kind, HudLayout? custom) => kind switch
    {
        HudLayoutKind.Sides => Sides,
        HudLayoutKind.Custom => custom ?? OriginalLayout,
        _ => null,
    };

    /// <summary>The HUD scale of a pane <paramref name="paneSize"/> inside a window
    /// <paramref name="windowSize"/>, <see cref="HudMetrics.Scale"/>'s rule over plain values.</summary>
    public static float PaneScale(Vector2 paneSize, Vector2 windowSize)
    {
        float windowH = windowSize.Y >= 1f ? windowSize.Y : paneSize.Y;
        float factor = windowSize.Y < 1f || paneSize.Y < 1f ? 1f : Mathf.Sqrt(Mathf.Clamp(paneSize.Y / windowSize.Y, 0.01f, 1f));
        return windowH / HudMetrics.ReferenceHeight * factor;
    }

    /// <summary>Where the original draws <paramref name="element"/>, by the formula its control draws
    /// with. The columns stand on <see cref="GaugeCluster.ColumnAnchors"/> and the plate on
    /// <see cref="ThrottlePlate.Placement"/>. The tape is centred at the pane's top.</summary>
    public static Rect2 OriginalRect(HudElement element, Rect2 box, Vector2 paneSize, float scale)
    {
        float top = GaugeCluster.FromBottom(GaugeCluster.ColumnTopY, scale, box.End.Y);
        float height = (GaugeCluster.ColumnBottomY - GaugeCluster.ColumnTopY) * scale;
        switch (element)
        {
            case HudElement.LeftDials:
                {
                    float left = GaugeCluster.ColumnAnchors(box, scale).Left;
                    return new Rect2(left + (GaugeCluster.LeftColumnMargin * scale), top,
                        GaugeCluster.LeftColumnWidth * scale, height);
                }

            case HudElement.RightDials:
                {
                    float right = GaugeCluster.ColumnAnchors(box, scale).Right;
                    return new Rect2(right - ((GaugeCluster.RightColumnMargin + GaugeCluster.RightColumnWidth) * scale), top,
                        GaugeCluster.RightColumnWidth * scale, height);
                }

            case HudElement.ThrottlePlate:
                {
                    var (origin, unit) = ThrottlePlate.Placement(box, scale);
                    return new Rect2(origin, new Vector2(ThrottlePlate.SvgWidth, ThrottlePlate.SvgHeight) * unit);
                }

            default:
                {
                    var size = new Vector2(CompassTape.RefBarWidth, CompassTape.RefBarHeight) * scale;
                    return new Rect2(new Vector2((paneSize.X - size.X) / 2f, CompassTape.RefTopMargin * scale), size);
                }
        }
    }

    /// <summary>Where <paramref name="layout"/> puts <paramref name="element"/>, the original's own
    /// rectangle for a null layout. A layout keeps the element's size and stands it inside the pane.
    /// A placement saved on a large screen therefore still shows whole on a split pane.</summary>
    public static Rect2 Place(HudElement element, HudLayout? layout, Rect2 box, Vector2 paneSize, float scale)
    {
        var original = OriginalRect(element, box, paneSize, scale);
        if (layout == null)
        {
            return original;
        }

        var p = layout[element];
        var frame = FrameOf(layout, box, paneSize);
        var fraction = new Vector2(HudPlacement.Fraction(p.X), HudPlacement.Fraction(p.Y));
        var anchor = frame.Position + (frame.Size * fraction) + (new Vector2(p.Dx, p.Dy) * scale);
        return Inside(new Rect2(anchor - (original.Size * fraction), original.Size), paneSize);
    }

    /// <summary>What <paramref name="layout"/> measures from: the pane's own rectangle for a
    /// pane-relative layout, the reading box for any other.</summary>
    public static Rect2 FrameOf(HudLayout layout, Rect2 box, Vector2 paneSize) =>
        layout.PaneRelative ? new Rect2(Vector2.Zero, paneSize) : box;

    /// <summary><paramref name="layout"/> re-measured from the pane's own edges, every element left
    /// where it stands on this pane to the reference pixel. The editor's drafts are all pane-relative.</summary>
    public static HudLayout ToPane(HudLayout layout, Rect2 box, Vector2 paneSize, float scale)
    {
        if (layout.PaneRelative)
        {
            return layout;
        }

        var pane = new Rect2(Vector2.Zero, paneSize);
        var moved = layout.InPane();
        foreach (var element in HudLayout.Elements)
        {
            moved = moved.With(element, PlacementFor(Place(element, layout, box, paneSize, scale), pane, scale));
        }

        return moved;
    }

    /// <summary>How far <paramref name="layout"/> moves <paramref name="element"/> from the original's
    /// place. Exactly zero for a null layout, so a control adding it draws the original's pixels.</summary>
    public static Vector2 Shift(HudElement element, HudLayout? layout, Rect2 box, Vector2 paneSize, float scale) =>
        layout == null
            ? Vector2.Zero
            : Place(element, layout, box, paneSize, scale).Position - OriginalRect(element, box, paneSize, scale).Position;

    /// <summary>The placement that puts <paramref name="element"/> at <paramref name="rect"/>'s corner,
    /// the editor's drop, measured from <paramref name="frame"/>. Each axis anchors to the third of the
    /// frame the element's centre is in. A column dropped by the right edge then keeps its margin from
    /// that edge on a wider pane. Offsets are whole reference pixels.</summary>
    public static HudPlacement PlacementFor(Rect2 rect, Rect2 frame, float scale)
    {
        var x = AnchorOf(rect.GetCenter().X - frame.Position.X, frame.Size.X);
        var y = AnchorOf(rect.GetCenter().Y - frame.Position.Y, frame.Size.Y);
        var fraction = new Vector2(HudPlacement.Fraction(x), HudPlacement.Fraction(y));
        var offset = (rect.Position + (rect.Size * fraction) - (frame.Position + (frame.Size * fraction))) / Mathf.Max(scale, 1e-3f);
        return new HudPlacement(x, y, Offset(offset.X), Offset(offset.Y));
    }

    /// <summary><paramref name="rect"/> moved the least distance that puts it inside a pane of
    /// <paramref name="paneSize"/>, its top-left corner kept on a pane too small to hold it.</summary>
    public static Rect2 Inside(Rect2 rect, Vector2 paneSize)
    {
        float x = Mathf.Clamp(rect.Position.X, 0f, Mathf.Max(0f, paneSize.X - rect.Size.X));
        float y = Mathf.Clamp(rect.Position.Y, 0f, Mathf.Max(0f, paneSize.Y - rect.Size.Y));
        return new Rect2(new Vector2(x, y), rect.Size);
    }

    private static HudAnchor AnchorOf(float along, float span) =>
        along < span / 3f ? HudAnchor.Start : along > span * 2f / 3f ? HudAnchor.End : HudAnchor.Centre;

    private static float Offset(float value) =>
        Mathf.Clamp(Mathf.Round(value), -HudLayout.MaxOffset, HudLayout.MaxOffset);
}
