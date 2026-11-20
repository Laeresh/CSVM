using System;
using System.Collections.Generic;
using CSVM.Extraction;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Hud;

/// <summary>A voice subtitle: the clip's words after the speaker's name, centred at the foot of the
/// pane, its top, or nowhere (<see cref="CSVM.Utils.SubtitlesSetting"/>).
/// The mission radio and the briefing narration each feed one. It draws, and shows, only a clip the
/// subtitle table carries, so a launch with no subtitle folder draws nothing. Every pane carries
/// its own copy over the one shared radio. Table format: docs/formats/sounds.md "Subtitles".</summary>
public sealed partial class SubtitleLine : Control
{
    // TUNE: remake layout, nothing to decode. The last line sits at 86 % of the pane height and
    // wraps at 70 % of its width. The font is the respawn prompt's 16 px cell less a fifth.
    // Under High the first line's top sits at 8 % of the height. That is under the compass tape
    // (35 to 75 of 1440) and above the kill messages at 20 %.
    private const float BaseFraction = 0.86f;
    private const float TopFraction = 0.08f;
    private const float WidthFraction = 0.7f;
    private const int RefFontSize = 40;
    private const int RefOutline = 6;

    // The prompt line's pale yellow for the name, so a speaker reads as HUD chrome.
    private static readonly Color SpeakerColor = new(1f, 1f, 64f / 255f);
    private static readonly Color TextColor = new(1f, 1f, 1f);
    private static readonly Color OutlineColor = new(0f, 0f, 0f, 0.85f);

    private readonly Func<(string? Wav, double At, string? Speaker)> _playing;
    private readonly Subtitles _table;
    private Subtitles.Line? _line;
    private bool _top;

    /// <summary>A subtitle worded from <paramref name="table"/>. The source gives the WAV sounding
    /// (null for none), the seconds played into it, and who speaks it where the table names nobody.
    /// </summary>
    public SubtitleLine(Func<(string? Wav, double At, string? Speaker)> playing, Subtitles table)
    {
        _playing = playing;
        _table = table;
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
    }

    /// <summary>Where the caption goes in a pane of the given size. It centres across the
    /// rectangle, wraps at its width and ends on its bottom edge. Null takes the flight layout.</summary>
    public Func<Vector2, Rect2>? Area { get; init; }

    /// <summary>The caption this frame draws, or null for none: the readback a suite asserts.</summary>
    public string? Caption => _line is { } line ? Compose(line) : null;

    /// <summary>A canvas layer of number <paramref name="layer"/> holding one subtitle over
    /// <paramref name="playing"/>, laid out in <paramref name="area"/> (null for the flight layout).
    /// </summary>
    public static CanvasLayer OnLayer(int layer, string name, Func<(string? Wav, double At, string? Speaker)> playing,
        Subtitles table, Func<Vector2, Rect2>? area = null)
    {
        var canvas = new CanvasLayer { Layer = layer, Name = name };
        canvas.AddChild(new SubtitleLine(playing, table) { Area = area, Visible = false });
        return canvas;
    }

    /// <summary>The table's line with <paramref name="speaker"/> standing in for a name it lacks:
    /// a combat bark is a pilot's voice take, named by the aircraft speaking it.</summary>
    public static Subtitles.Line? Named(Subtitles.Line? line, string? speaker) =>
        line is { Speaker: null } bare && speaker is { Length: > 0 } ? bare with { Speaker = speaker } : line;

    /// <summary>One line as it reads: the speaker's name and a colon before the words.</summary>
    public static string Compose(Subtitles.Line line) =>
        line.Speaker is { } who ? $"{who}: {line.Text}" : line.Text;

    /// <summary>Splits <paramref name="text"/> at spaces into lines no wider than
    /// <paramref name="maxWidth"/> by <paramref name="width"/>. A word wider than that keeps a line
    /// of its own rather than being cut.</summary>
    public static List<string> Wrap(string text, float maxWidth, Func<string, float> width)
    {
        var lines = new List<string>();
        string current = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string joined = current.Length == 0 ? word : current + " " + word;
            if (current.Length > 0 && width(joined) > maxWidth)
            {
                lines.Add(current);
                joined = word;
            }

            current = joined;
        }

        if (current.Length > 0)
        {
            lines.Add(current);
        }

        return lines;
    }

    public override void _Process(double delta)
    {
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
        var (wav, at, speaker) = _playing();
        var line = SubtitlesSetting.Word == SubtitlesSetting.Off ? null : Named(_table.At(wav, at), speaker);
        bool top = Area == null && SubtitlesSetting.Word == SubtitlesSetting.High;
        if (line == _line && top == _top)
        {
            return;
        }

        _line = line;
        _top = top;
        // Hidden while silent: the headset's HUD panel shows while any layer on it holds a visible item.
        Visible = line != null;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float s = Size.Y <= 0f ? 0f : HudMetrics.Scale(this);
        if (_line is not { } line || s <= 0f)
        {
            return;
        }

        var font = GetThemeDefaultFont();
        int size = Mathf.Max(1, Mathf.RoundToInt(RefFontSize * s * HudMetrics.StatusTextScale));
        int outline = Mathf.Max(1, Mathf.RoundToInt(RefOutline * s));
        float Width(string t) => font.GetStringSize(t, HorizontalAlignment.Left, -1, size).X;
        var area = Area?.Invoke(Size)
            ?? new Rect2(Size.X * (1f - WidthFraction) / 2f, 0f, Size.X * WidthFraction, Size.Y * BaseFraction);
        var lines = Wrap(Compose(line), area.Size.X, Width);
        float pitch = font.GetHeight(size);
        // Low ends on the area's bottom edge and grows up; High hangs from TopFraction and grows down.
        float y = _top ? (Size.Y * TopFraction) + font.GetAscent(size) : area.End.Y - ((lines.Count - 1) * pitch);
        float middle = area.Position.X + (area.Size.X / 2f);
        string head = line.Speaker is { } who ? who + ":" : string.Empty;
        for (int i = 0; i < lines.Count; i++, y += pitch)
        {
            string text = lines[i];
            var at = new Vector2(middle - (Width(text) / 2f), y);
            DrawStringOutline(font, at, text, HorizontalAlignment.Left, -1, size, outline, OutlineColor);
            // The name colours only where the wrap left it whole at the head of the first line.
            if (i == 0 && head.Length > 0 && text.StartsWith(head, StringComparison.Ordinal))
            {
                DrawString(font, at, head, HorizontalAlignment.Left, -1, size, SpeakerColor);
                DrawString(font, at + new Vector2(Width(head), 0f), text[head.Length..],
                    HorizontalAlignment.Left, -1, size, TextColor);
                continue;
            }

            DrawString(font, at, text, HorizontalAlignment.Left, -1, size, TextColor);
        }
    }
}
