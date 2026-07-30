using System.IO;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// The game's own HUD text font, rebuilt from the two atlases in <c>extracted/rimage/</c>:
/// <c>5pointhud.png</c> (the normal green glyphs) and <c>5pointhudbrite.png</c> (the brighter
/// highlight variant). Both are the same 463×6 bitmap — a proportional 1-bit font whose glyphs
/// occupy rows 0..4 (five pixels tall — hence "5point"; row 5 is blank spacing) and cover the
/// printable ASCII range <c>0x20</c>..<c>0x7e</c>. Space (<c>0x20</c>) is a blank leading cell, so
/// the atlas holds 94 ink glyphs, one per code <c>0x21</c>..<c>0x7e</c> in order; letters are drawn
/// uppercase-only (the a–z cells reuse the A–Z shapes). Layout + metrics verified against the PNG,
/// see <c>docs/formats/hud.md</c>.
///
/// <para>The two atlases are geometrically identical and differ only in green level (normal core
/// 0,150,0 · highlight core 0,255,0), so glyph metrics are read once and either texture is sampled
/// per draw. The black background is keyed to transparent at load; every non-black texel (the green
/// core plus its dim-green outline) is kept as-is, so drawing with a white modulate reproduces the
/// original's colours exactly.</para>
///
/// <para>Pure font resource, not a Node: <see cref="Draw"/> renders onto any caller's
/// <see cref="CanvasItem"/>, sized in screen pixels — route the scale through
/// <see cref="HudMetrics"/> exactly as CompassTape / GaugeCluster do. The drawing control must use
/// a <b>Nearest</b> texture filter (this is a pixel font).</para>
/// </summary>
public sealed class HudFont
{
    private const string NormalFile = "5pointhud.png";
    private const string BrightFile = "5pointhudbrite.png";
    private const int FirstCode = 0x21;         // '!' — the first ink glyph (space 0x20 is blank)
    private const int LastCode = 0x7e;          // '~'
    private const int CodeCount = LastCode - FirstCode + 1; // 94
    private const int Tracking = 1;             // atlas px of gap the renderer inserts after a glyph
    private const int SpaceAdvance = 3;         // atlas px width of a space / unknown code (pre-tracking)

    private readonly Texture2D _normal;
    private readonly Texture2D _bright;
    private readonly (int X, int W)[] _glyphs;  // by code − FirstCode; W == 0 means "no glyph"
    private readonly int _top;                  // first inked atlas row (0 for this atlas)

    private HudFont(Texture2D normal, Texture2D bright, (int X, int W)[] glyphs, int top, int height)
    {
        _normal = normal;
        _bright = bright;
        _glyphs = glyphs;
        _top = top;
        PixelHeight = height;
    }

    /// <summary>Glyph cell height in atlas pixels (the inked row span, 5 for this atlas).</summary>
    public int PixelHeight { get; }

    /// <summary>Loads the font from an extracted <c>rimage</c> directory. Null (with one log line)
    /// when the normal atlas is absent — HUD text is cosmetic and must never take a build down. The
    /// highlight atlas falls back to the normal one if missing.</summary>
    public static HudFont? Load(string rimageDir)
    {
        var glyphs = new (int X, int W)[CodeCount];
        var normal = LoadAtlas(Path.Combine(rimageDir, NormalFile), glyphs,
            out int runCount, out int top, out int height);
        if (normal == null)
        {
            GD.Print($"[hudfont] no {NormalFile} in {rimageDir} — HUD text font off (run ExtractRof.ps1)");
            return null;
        }
        if (runCount != CodeCount)
        {
            GD.PushWarning($"[hudfont] {NormalFile}: found {runCount} glyphs, expected {CodeCount} "
                           + $"({FirstCode:X2}..{LastCode:X2}) — the ASCII mapping may be off");
        }
        var bright = LoadAtlas(Path.Combine(rimageDir, BrightFile), null, out _, out _, out _) ?? normal;
        return new HudFont(normal, bright, glyphs, top, height);
    }

    /// <summary>On-screen width of a string at the given px-per-atlas-pixel scale.</summary>
    public float Measure(string text, float scale)
    {
        int adv = 0;
        foreach (char ch in text)
        {
            adv += Advance(ch);
        }
        return adv * scale;
    }

    /// <summary>The scale (px per atlas pixel) that renders the font at a target cell height.</summary>
    public float ScaleForHeight(float pixelHeight) => pixelHeight / PixelHeight;

    /// <summary>Draws <paramref name="text"/> with its top-left at <paramref name="topLeft"/>,
    /// <paramref name="scale"/> screen pixels per atlas pixel. <paramref name="bright"/> selects the
    /// highlight atlas; <paramref name="modulate"/> tints it (white keeps the atlas's own green).
    /// The caller's control must use a Nearest texture filter for a crisp pixel look.</summary>
    public void Draw(CanvasItem ci, string text, Vector2 topLeft, float scale,
        bool bright = false, Color? modulate = null)
    {
        var tex = bright ? _bright : _normal;
        var col = modulate ?? Colors.White;
        float x = topLeft.X;
        float h = PixelHeight * scale;
        foreach (char ch in text)
        {
            int idx = ch - FirstCode;
            if (idx >= 0 && idx < CodeCount && _glyphs[idx].W > 0)
            {
                var (sx, sw) = _glyphs[idx];
                ci.DrawTextureRectRegion(tex,
                    new Rect2(x, topLeft.Y, sw * scale, h),
                    new Rect2(sx, _top, sw, PixelHeight), col);
                x += (sw + Tracking) * scale;
            }
            else
            {
                x += (SpaceAdvance + Tracking) * scale; // space and any unrepresented code
            }
        }
    }

    /// <summary>Loads one atlas: keys the black background to transparent, and (when
    /// <paramref name="glyphs"/> is given) segments the ink into per-code source rects by walking
    /// columns — each maximal run of inked columns is the next glyph, assigned to codes from
    /// <see cref="FirstCode"/> upward. Returns null if the file is missing or unreadable.</summary>
    private static Texture2D? LoadAtlas(string path, (int X, int W)[]? glyphs,
        out int runCount, out int top, out int height)
    {
        runCount = 0;
        top = 0;
        height = 0;
        if (!File.Exists(path))
        {
            return null;
        }
        var img = Image.LoadFromFile(path);
        if (img == null)
        {
            return null;
        }
        img.Convert(Image.Format.Rgba8);
        int w = img.GetWidth(), h = img.GetHeight();

        // Inked-row span (the glyph cell height) and per-column ink, plus the transparency key.
        int firstRow = h, lastRow = -1;
        var colInk = new bool[w];
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var c = img.GetPixel(x, y);
                bool ink = c.R + c.G + c.B > 0.02f; // any non-black texel is glyph
                if (ink)
                {
                    colInk[x] = true;
                    if (y < firstRow) firstRow = y;
                    if (y > lastRow) lastRow = y;
                }
                else
                {
                    img.SetPixel(x, y, new Color(c.R, c.G, c.B, 0f));
                }
            }
        }
        top = lastRow >= firstRow ? firstRow : 0;
        height = lastRow >= firstRow ? lastRow - firstRow + 1 : 0;

        if (glyphs != null)
        {
            int idx = 0, x = 0;
            while (x < w)
            {
                if (!colInk[x])
                {
                    x++;
                    continue;
                }
                int start = x;
                while (x < w && colInk[x])
                {
                    x++;
                }
                if (idx < glyphs.Length)
                {
                    glyphs[idx] = (start, x - start);
                }
                idx++;
            }
            runCount = idx;
        }

        var tex = ImageTexture.CreateFromImage(img);
        return tex;
    }

    private int Advance(char ch)
    {
        int idx = ch - FirstCode;
        if (idx >= 0 && idx < CodeCount && _glyphs[idx].W > 0)
        {
            return _glyphs[idx].W + Tracking;
        }
        return SpaceAdvance + Tracking;
    }
}
