using System.IO;
using CSVM.Flight.Hud;
using Godot;

namespace CSVM.Testing;

/// <summary>The throttle plate's two SVGs through Godot's own rasteriser, the one the HUD uses.
/// Each check is in the suite's description. It writes a preview of three lever positions, since a
/// look is the user's to judge.</summary>
internal static class ThrottlePlateSuites
{
    [Suite("throttle-plate",
        "the throttle plate's SVGs rasterise at 1440p and 4K sizes, the plate is opaque inside and "
        + "clear at its corner, the knob is a cut-out, the knob rides from IDLE to MAX and clamps, "
        + "and the plate stands inboard of the speedometer inside a 16:9 pane")]
    internal static void ThrottlePlateDraws(TestContext ctx)
    {
        string plateSvg = Godot.FileAccess.GetFileAsString("res://data/hud/throttle_plate.svg.bin");
        string knobSvg = Godot.FileAccess.GetFileAsString("res://data/hud/throttle_knob.svg.bin");
        ctx.Check(plateSvg.Length > 0 && knobSvg.Length > 0,
            $"both SVGs ship: plate {plateSvg.Length} chars, knob {knobSvg.Length} chars");

        Image? preview = null;
        foreach (float height in new[] { 1440f, 2160f })
        {
            var (origin, unit) = ThrottlePlate.Placement(new Rect2(0f, 0f, height * 16f / 9f, height), height / HudMetrics.ReferenceHeight);
            var plate = Raster(plateSvg, unit);
            var knob = Raster(knobSvg, unit);
            if (plate == null || knob == null)
            {
                ctx.Check(false, $"both SVGs rasterise at {unit:0.000} px per unit ({height:0}p)");
                continue;
            }

            int expected = Mathf.RoundToInt((ThrottlePlate.SvgWidth + (2f * ThrottlePlate.SvgPad)) * unit);
            ctx.Check(Mathf.Abs(plate.GetWidth() - expected) <= 1,
                $"{height:0}p: the plate rasterises {plate.GetWidth()}x{plate.GetHeight()} px (width {expected} expected)");
            var inside = plate.GetPixel(plate.GetWidth() / 2, plate.GetHeight() / 2);
            var corner = plate.GetPixel(1, 1);
            ctx.Check(inside.A > 0.99f && corner.A < 0.05f,
                $"…opaque at its centre (alpha {inside.A:0.00}) and clear at its outer corner (alpha {corner.A:0.00})");
            var knobCentre = knob.GetPixel(knob.GetWidth() / 2, knob.GetHeight() / 2);
            ctx.Check(knobCentre.A > 0.99f && knob.GetPixel(0, 0).A < 0.05f,
                $"…and the knob is a cut-out: alpha {knobCentre.A:0.00} at its centre, {knob.GetPixel(0, 0).A:0.00} at its corner");

            float plateRight = origin.X + (ThrottlePlate.SvgWidth * unit);
            float s = height / HudMetrics.ReferenceHeight;
            float bezelLeft = (height * 16f / 9f) - ((GaugeCluster.SpdCenterFromRight + GaugeCluster.SpdRadius) * s);
            ctx.Check(plateRight < bezelLeft && origin.Y > 0f && origin.Y + (ThrottlePlate.SvgHeight * unit) < height,
                $"…standing inboard of the speedometer (right edge {plateRight:0}, bezel {bezelLeft:0}) and inside the pane (top {origin.Y:0})");

            if (height > 2000f)
                preview = Preview(plate, knob, unit);
        }

        ctx.Check(Mathf.IsEqualApprox(ThrottlePlate.KnobY(0f), ThrottlePlate.IdleY)
            && Mathf.IsEqualApprox(ThrottlePlate.KnobY(1f), ThrottlePlate.MaxY)
            && Mathf.IsEqualApprox(ThrottlePlate.KnobY(0.5f), (ThrottlePlate.IdleY + ThrottlePlate.MaxY) / 2f)
            && Mathf.IsEqualApprox(ThrottlePlate.KnobY(1.4f), ThrottlePlate.MaxY)
            && Mathf.IsEqualApprox(ThrottlePlate.KnobY(-0.2f), ThrottlePlate.IdleY),
            $"the knob sits on IDLE at 0, on MAX at 1, halfway at 0.5, and clamps past either end");

        if (preview != null)
        {
            string path = Path.Combine(ctx.ScratchDir, "throttle_plate_preview.png");
            Directory.CreateDirectory(ctx.ScratchDir);
            preview.SavePng(path);
            ctx.Note($"preview at idle, half and full lever: {path}");
        }
    }

    private static Image? Raster(string svg, float unit)
    {
        var image = new Image();
        return image.LoadSvgFromString(svg, unit) == Error.Ok ? image : null;
    }

    // The plate three times on a dark ground, its knob at idle, half and full.
    private static Image Preview(Image plate, Image knob, float unit)
    {
        int margin = 24;
        int w = plate.GetWidth(), h = plate.GetHeight();
        var canvas = Image.CreateEmpty((3 * w) + (4 * margin), h + (2 * margin), false, Image.Format.Rgba8);
        canvas.Fill(new Color(0.08f, 0.1f, 0.12f));
        float[] levers = { 0f, 0.5f, 1f };
        for (int i = 0; i < levers.Length; i++)
        {
            var at = new Vector2I(margin + (i * (w + margin)), margin);
            canvas.BlendRect(plate, new Rect2I(Vector2I.Zero, plate.GetSize()), at);
            var centre = new Vector2(ThrottlePlate.SvgPad + ThrottlePlate.SlotX, ThrottlePlate.SvgPad + ThrottlePlate.KnobY(levers[i])) * unit;
            var knobAt = at + new Vector2I(Mathf.RoundToInt(centre.X - (knob.GetWidth() / 2f)), Mathf.RoundToInt(centre.Y - (knob.GetHeight() / 2f)));
            canvas.BlendRect(knob, new Rect2I(Vector2I.Zero, knob.GetSize()), knobAt);
        }
        return canvas;
    }
}
