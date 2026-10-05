using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.UI.Boards;
using Godot;

namespace CSVM.UI.Overlays;

/// <summary>
/// One pane's held Display Scores: the standings stand in the pane whose seat holds the action
/// (<c>FlightController.ScoresShown</c>) and go on release. Under the Original presentation they are
/// the original's HUD text, yellow Courier New lines at the top left. A flag carrier's line has an
/// "F" before it. Under Built-in they are a chrome table centred in the pane. A source with no race
/// and no Dogfight draws nothing. Module entry: docs/architecture/UI.md; decode:
/// docs/org/multiplayer-scoring.md "The in-flight scores".
/// </summary>
public sealed partial class ScoresOverlay : Control
{
    // The original's HUD frame is 640 by 480; the HUD's 1440p reference is three times it. The
    // lines start at (50, 30) and step 10, the flag column at x 40 (FUN_004565d0). The hudNetPlay
    // font is a 12-pixel cell 8 wide (extracted/zrdr/fonts.zrd.json).
    private const float RefTextX = 150f;
    private const float RefFlagX = 120f;
    private const float RefTopY = 90f;
    private const float RefLinePitch = 30f;
    private const float RefCellWidth = 24f;
    private const int RefFontSize = 36;
    private const int FaceWeight = 600;
    private const float RefPad = 20f;
    private const float RefColumnGap = 26f;
    private const float RefRowGap = 6f;

    // hudNetPlay's ink is 255, 250, 66. The handler's table holds the flag colours of teams 1 and 2,
    // read as the GDI colour words the console's scorecolors packs.
    private static readonly Color LineColor = new(1f, 250f / 255f, 66f / 255f);
    private static readonly Color[] FlagColors = { new(170f / 255f, 170f / 255f, 0f), new(170f / 255f, 0f, 0f) };

    // The Built-in table's sizes and inks, chrome type scale rungs in the HUD's reference.
    private static readonly float RefHeaderFont = ChromeType.InReference(ChromeSize.Note, HudMetrics.ReferenceHeight);
    private static readonly float RefRowFont = ChromeType.InReference(ChromeSize.Text, HudMetrics.ReferenceHeight);
    private static readonly Color Backdrop = new(0f, 0f, 0f, 0.6f);
    private static readonly Color HeaderColor = new(0.72f, 0.78f, 0.85f);
    private static readonly Color RowColor = new(1f, 1f, 1f);
    private static readonly Color TeamColor = new(1f, 0.85f, 0.4f);

    private static Font? _face;
    private static bool _faceLooked;

    private PlayerRig _pane = null!;
    private ScoresSource _source = null!;
    private IReadOnlyList<OriginalScoresLine> _original = Array.Empty<OriginalScoresLine>();
    private ScoresTable? _table;
    private bool _drawnShown;

    /// <summary>Whether this pane draws the original's text rather than the chrome table.</summary>
    public bool Original { get; private set; }

    /// <summary>Whether the standings are up this frame, as the last <see cref="Refresh"/> found.
    /// </summary>
    public bool Shown { get; private set; }

    /// <summary>What the last <see cref="Refresh"/> put up, one string per line: the Original text,
    /// or the chrome table's rows with their cells joined. Empty while hidden.</summary>
    public IReadOnlyList<string> Lines { get; private set; } = Array.Empty<string>();

    /// <summary>Builds the overlay for <paramref name="pane"/> on a layer of its own over that pane's
    /// HUD. It reads the seat in the pane each frame, so an airframe swap keeps it.</summary>
    public static ScoresOverlay Attach(PlayerRig pane, ScoresSource source, bool original)
    {
        ArgumentNullException.ThrowIfNull(pane);
        ArgumentNullException.ThrowIfNull(source);
        var overlay = new ScoresOverlay
        {
            _pane = pane,
            _source = source,
            Original = original,
            Name = "scores",
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
        };
        var layer = new CanvasLayer { Name = "scores", Layer = HudLayers.Hud };
        layer.AddChild(overlay);
        pane.HudParent.AddChild(layer);
        return overlay;
    }

    /// <summary>Reads whether the pane's seat holds Display Scores and, if so, the standings as they
    /// stand. Each frame runs it; a suite in one frame calls it directly.</summary>
    public void Refresh()
    {
        Shown = _source.HasScores && _pane.Controller is { ScoresShown: true };
        _original = Shown && Original ? _source.OriginalLines() : Array.Empty<OriginalScoresLine>();
        _table = Shown && !Original ? _source.Table() : null;
        Lines = Original
            ? _original.Select(l => l.Text).ToList()
            : _table?.Rows.Select(r => string.Join("  ", r.Cells)).ToList() ?? (IReadOnlyList<string>)Array.Empty<string>();
    }

    public override void _Process(double delta)
    {
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
        Refresh();
        // Standings move under a held key, so it redraws every frame it is up and once on release.
        if (Shown || _drawnShown)
        {
            _drawnShown = Shown;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        float s = !Shown || Size.Y <= 0f ? 0f : HudMetrics.Scale(this);
        if (s <= 0f)
        {
            return;
        }

        if (Original)
        {
            DrawOriginal(s);
        }
        else if (_table != null)
        {
            DrawTable(_table, s);
        }
    }

    // The original's face, Courier New at hudNetPlay's weight, or the theme's where it is missing.
    private static Font? CourierNew()
    {
        if (!_faceLooked)
        {
            _faceLooked = true;
            if (OS.GetSystemFontPath("Courier New", FaceWeight).Length > 0)
            {
                _face = new SystemFont { FontNames = new[] { "Courier New" }, FontWeight = FaceWeight, GenerateMipmaps = true };
            }
        }

        return _face;
    }

    // Every character on the 8-pixel cell the original's monospaced face sets, so the columns hold
    // whatever face stands in for it.
    private void DrawOriginal(float s)
    {
        var font = CourierNew() ?? GetThemeDefaultFont();
        float text = HudMetrics.StatusTextScale;
        int fontSize = Mathf.Max(1, Mathf.RoundToInt(RefFontSize * s * text));
        float cell = RefCellWidth * s * text;
        var origin = HudMetrics.ReadingBox(this).Position;
        for (int i = 0; i < _original.Count; i++)
        {
            float y = origin.Y + (RefTopY * s) + (RefLinePitch * s * text * i) + font.GetAscent(fontSize);
            var line = _original[i];
            if (line.Flag is >= 1 and <= 2)
            {
                DrawCell(font, fontSize, new Vector2(origin.X + (RefFlagX * s), y), "F", FlagColors[line.Flag - 1]);
            }

            for (int c = 0; c < line.Text.Length; c++)
            {
                if (line.Text[c] != ' ')
                {
                    DrawCell(font, fontSize, new Vector2(origin.X + (RefTextX * s) + (cell * c), y), line.Text[c].ToString(), LineColor);
                }
            }
        }
    }

    // One character with hudNetPlay's drop shadow, the one-pixel offset the chat panel draws.
    private void DrawCell(Font font, int fontSize, Vector2 baseline, string glyph, Color ink)
    {
        DrawChar(font, baseline + Vector2.One, glyph, fontSize, MarkerDraw.Shadow);
        DrawChar(font, baseline, glyph, fontSize, ink);
    }

    private void DrawTable(ScoresTable table, float s)
    {
        var font = ChromeType.Face(this);
        float text = HudMetrics.StatusTextScale;
        int headerSize = Mathf.Max(1, Mathf.RoundToInt(RefHeaderFont * s * text));
        int rowSize = Mathf.Max(1, Mathf.RoundToInt(RefRowFont * s * text));
        int columns = table.Headers.Count;
        var widths = new float[columns];
        for (int c = 0; c < columns; c++)
        {
            widths[c] = font.GetStringSize(table.Headers[c], HorizontalAlignment.Left, -1f, headerSize).X;
            foreach (var row in table.Rows)
            {
                widths[c] = Mathf.Max(widths[c], font.GetStringSize(row.Cells[c], HorizontalAlignment.Left, -1f, rowSize).X);
            }
        }

        float pad = RefPad * s, gap = RefColumnGap * s;
        float headerHeight = font.GetHeight(headerSize) + (RefRowGap * s);
        float rowHeight = font.GetHeight(rowSize) + (RefRowGap * s);
        float width = widths.Sum() + (gap * (columns - 1)) + (2f * pad);
        float height = headerHeight + (rowHeight * table.Rows.Count) + (2f * pad);
        var corner = new Vector2((Size.X - width) / 2f, (Size.Y - height) / 2f);
        DrawRect(new Rect2(corner, new Vector2(width, height)), Backdrop);
        float y = corner.Y + pad;
        DrawCells(font, headerSize, table.Headers, table.RightAligned, widths, corner.X + pad, y + font.GetAscent(headerSize), gap, _ => HeaderColor);
        y += headerHeight;
        foreach (var row in table.Rows)
        {
            Color Ink(int column) => row.Team ? TeamColor : column == 1 && row.Seat >= 0 ? SplitScreen.PlayerColor(row.Seat) : RowColor;
            DrawCells(font, rowSize, row.Cells, table.RightAligned, widths, corner.X + pad, y + font.GetAscent(rowSize), gap, Ink);
            y += rowHeight;
        }
    }

    // One table line, each cell left- or right-set in its column, over a one-pixel shadow.
    private void DrawCells(Font font, int fontSize, IReadOnlyList<string> cells, IReadOnlyList<bool> right,
        float[] widths, float left, float baseline, float gap, Func<int, Color> ink)
    {
        float x = left;
        for (int c = 0; c < cells.Count; c++)
        {
            float w = font.GetStringSize(cells[c], HorizontalAlignment.Left, -1f, fontSize).X;
            var at = new Vector2(right[c] ? x + widths[c] - w : x, baseline);
            DrawString(font, at + Vector2.One, cells[c], HorizontalAlignment.Left, -1f, fontSize, MarkerDraw.Shadow);
            DrawString(font, at, cells[c], HorizontalAlignment.Left, -1f, fontSize, ink(c));
            x += widths[c] + gap;
        }
    }
}
