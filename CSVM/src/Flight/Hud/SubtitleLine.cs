using System;
using System.Collections.Generic;
using CSVM.Extraction;
using Godot;

namespace CSVM.Flight.Hud;

/// <summary>A voice subtitle: the words of the clip sounding, centred near the foot of the pane
/// with the speaker's name before them. The mission radio and the briefing narration each feed one. The original captions nothing, so this is remake-only. It
/// draws only a clip the subtitle table carries, so a launch with no subtitle folder draws nothing.
/// Every pane carries its own copy over the one shared radio. Table format: docs/formats/sounds.md
/// "Subtitles".</summary>
public sealed partial class SubtitleLine : Control
{
    // TUNE: remake layout, nothing to decode. The last line sits at 86 % of the pane height and
    // wraps at 70 % of its width. The font is the respawn prompt's 16 px cell less a fifth.
    private const float BaseFraction = 0.86f;
    private const float WidthFraction = 0.7f;
    private const int RefFontSize = 40;
    private const int RefOutline = 6;

    // The prompt line's pale yellow for the name, so a speaker reads as HUD chrome.
    private static readonly Color SpeakerColor = new(1f, 1f, 64f / 255f);
    private static readonly Color TextColor = new(1f, 1f, 1f);
    private static readonly Color OutlineColor = new(0f, 0f, 0f, 0.85f);

    private readonly Func<(string? Wav, double At)> _playing;
    private readonly Subtitles _table;
    private Subtitles.Line? _line;

    /// <summary>A subtitle over <paramref name="playing"/>, the WAV sounding (null for none) and
    /// the seconds played into it, worded from <paramref name="table"/>.</summary>
    public SubtitleLine(Func<(string? Wav, double At)> playing, Subtitles table)
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
        var (wav, at) = _playing();
        var line = _table.At(wav, at);
        if (line == _line)
        {
            return;
        }

        _line = line;
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
        float y = area.End.Y - ((lines.Count - 1) * pitch);
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
