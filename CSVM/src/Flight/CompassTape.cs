using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>The original's heading tape at the top of the screen, rebuilt from its own
/// compassticks2/compasstxt HUD textures (docs/formats/hud.md). A cylindrical drum seen edge-on:
/// a mark Δ° from the current heading sits at <c>center − R·sin(Δ)</c>, brightness falls off as
/// <c>cos(Δ)</c>, and headings increase to the LEFT, a real whiskey-compass card. Ticks render
/// point-sampled while labels are smooth, so labels live on a bilinear child layer on top.
/// </summary>
public sealed partial class CompassTape : Control
{
    // Screen metrics from the 1440p reference screenshot, scaled by viewport height.
    private const float RefBarWidth = 263f, RefBarHeight = 40f, RefTopMargin = 35f;
    private const float RefDrumRadius = 127.6f; // fit of every tall tick: x = c − R·sin(Δ)
    private const float RefLabelTop = 3f;       // label box top below the bar top
    private const float RefLabelHeight = 20f;   // 32 src px drawn at 20 → scale 0.625
    private const float RimGain = 1.5f;         // the bar-end silhouette ticks, drawn from
                                                // the tall tick's soft side column (~192 in
                                                // the original vs its ~128 texels)
    private const float TileDegrees = 15f;      // one compassticks2 tile
    // The original draws the tile ~25% taller than the bar, bottom-aligned (its empty
    // top rows overflow the bar and clip): that is what puts the tall ticks at 77% of
    // the bar height and the minors at 42% — full-height mapping leaves them stubby
    // (measured 30 px / 17 px vs 24 px / 12 px in a 39 px bar).
    private const float TileOverscan = 1.25f;

    private static readonly Vector2 TileSrcSize = new(64, 16);

    // compasstxt atlas: "NE SE SW NW" pairs at x 1–25 / 27–51 / 52–83 / 84–115.
    private static readonly Rect2[] LabelSrc =
    {
        new(84, 0, 12, 32), // N (from NW)
        new(1, 0, 25, 32),  // NE
        new(40, 0, 12, 32), // E (from SE)
        new(27, 0, 25, 32), // SE
        new(52, 0, 12, 32), // S (from SW)
        new(52, 0, 32, 32), // SW
        new(64, 0, 20, 32), // W (from SW)
        new(84, 0, 32, 32), // NW
    };

    private Texture2D _ticks = null!;
    private Texture2D _labels = null!;
    private LabelLayer _labelLayer = null!;

    /// <summary>Current heading in degrees, 0 = north (−Z), 90 = east (+X); set
    /// each frame by the flight controller.</summary>
    public float HeadingDeg { get; set; }

    /// <summary>Null when the chapter's texture archive lacks the two HUD textures
    /// (the archive itself logs the miss).</summary>
    public static CompassTape? Build(TextureArchive textures)
    {
        var ticks = textures.Find("compassticks2");
        var labels = textures.Find("compasstxt");
        if (ticks == null || labels == null)
            return null;
        var tape = new CompassTape
        {
            _ticks = ticks,
            _labels = labels,
            MouseFilter = MouseFilterEnum.Ignore,
            ClipContents = true, // rim caps, tile overscan + rim-faded labels clip at the bar
            TextureFilter = TextureFilterEnum.Nearest, // the ticks' hard-edged comb
        };
        tape._labelLayer = new LabelLayer
        {
            Tape = tape,
            MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = TextureFilterEnum.Linear, // the labels stay smooth
        };
        tape._labelLayer.SetAnchorsPreset(LayoutPreset.FullRect);
        tape.AddChild(tape._labelLayer);
        return tape;
    }

    public override void _Process(double delta)
    {
        // Track the viewport each frame (resizable window) and repaint at the current
        // heading; the tape is a dozen quads, so the unconditional redraw is negligible.
        var vp = GetViewportRect().Size;
        float s = HudMetrics.Scale(this);
        Position = new Vector2((vp.X - RefBarWidth * s) / 2f, RefTopMargin * s);
        Size = new Vector2(RefBarWidth * s, RefBarHeight * s);
        QueueRedraw();
        _labelLayer.QueueRedraw();
    }

    public override void _Draw()
    {
        float s = HudMetrics.Scale(this);
        float w = Size.X, h = Size.Y;

        DrawRect(new Rect2(0, 0, w, h), Colors.Black);

        // Tick tiles at every 15° edge. Tiles straddling ±90° are skipped: past the rim sin folds
        // back and the drum's far side would draw reversed.
        float tileH = h * TileOverscan, tileY = h - tileH; // bottom-aligned, top clipped
        float first = Mathf.Ceil((HeadingDeg - 90f) / TileDegrees) * TileDegrees;
        for (float a = first; a + TileDegrees <= HeadingDeg + 90f; a += TileDegrees)
        {
            float d0 = a - HeadingDeg, d1 = d0 + TileDegrees;
            // d1 lands left of d0; the tile pattern is mirror-symmetric, so no flip
            float x0 = DrumX(d1), x1 = DrumX(d0);
            float m = Mathf.Cos(Mathf.DegToRad((d0 + d1) / 2f));
            DrawTextureRectRegion(_ticks, new Rect2(x0, tileY, x1 - x0, tileH),
                new Rect2(Vector2.Zero, TileSrcSize), new Color(m, m, m));
        }

        // The drum rims: the original caps both bar ends with a bright tall tick
        // (the drum's silhouette edge), drawn from the tile's tall-tick side column.
        var rimSrc = new Rect2(0, 0, 2, 16);
        var rim = new Color(RimGain, RimGain, RimGain);
        DrawTextureRectRegion(_ticks, new Rect2(0, tileY, 2f * s, tileH), rimSrc, rim);
        DrawTextureRectRegion(_ticks, new Rect2(w - 2f * s, tileY, 2f * s, tileH), rimSrc, rim);
    }

    // Screen x of a mark Δ° off the current heading — the drum projection;
    // increasing headings run leftward (whiskey card).
    private float DrumX(float deltaDeg) =>
        Size.X / 2f - RefDrumRadius * HudMetrics.Scale(this)
                    * Mathf.Sin(Mathf.DegToRad(deltaDeg));

    // Octant labels every 45°, centred on their drum position but NOT
    // drum-compressed (the original billboards them upright), fading with the same
    // cos as the ticks — the atlas' own cream colour shows through. A child layer
    // only so the letters filter bilinearly while the ticks stay point-sampled.
    private sealed partial class LabelLayer : Control
    {
        public CompassTape Tape = null!;

        public override void _Draw()
        {
            var t = Tape;
            float s = HudMetrics.Scale(this);
            float scale = RefLabelHeight / 32f * s;
            float firstLabel = Mathf.Ceil((t.HeadingDeg - 90f) / 45f) * 45f;
            for (float a = firstLabel; a <= t.HeadingDeg + 90f; a += 45f)
            {
                float d = a - t.HeadingDeg;
                if (Mathf.Abs(d) >= 90f)
                    continue;
                float fade = Mathf.Cos(Mathf.DegToRad(d));
                var src = LabelSrc[(int)Mathf.PosMod(a / 45f, 8f)];
                var dest = new Rect2(t.DrumX(d) - src.Size.X * scale / 2f,
                                     RefLabelTop * s,
                                     src.Size.X * scale, src.Size.Y * scale);
                DrawTextureRectRegion(t._labels, dest, src, new Color(fade, fade, fade));
            }
        }
    }
}
