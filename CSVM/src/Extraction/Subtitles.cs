using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CSVM.Utils;

namespace CSVM.Extraction;

/// <summary>
/// The subtitle table: the words a voice clip speaks, looked up by the clip's WAV name and the
/// seconds played. The original ships no transcript, so the table is read from a folder a player
/// drops files into under the data root. Loose files only: a zip in the folder is not read. Each <c>*.json</c> file maps a WAV name to a speaker and text,
/// or to timed segments for a long clip. Files load in name order and a later one wins.
/// Format: docs/formats/sounds.md "Subtitles".
/// </summary>
public sealed class Subtitles
{
    /// <summary>The folder a launch with nothing configured reads.</summary>
    public const string DefaultFolder = "Subtitles";

    /// <summary>The <c>config.json</c> key naming the folder.</summary>
    public const string ConfigKey = "subtitles";

    /// <summary>Seconds a segment stays up past its end while the next one has not started. A
    /// tight timestamp would otherwise take the words away mid-read.</summary>
    public const double Hold = 1.0;

    private readonly Dictionary<string, Segment[]> _clips;

    private Subtitles(Dictionary<string, Segment[]> clips) => _clips = clips;

    /// <summary>The table the session reads, empty until the launcher opens a folder.</summary>
    public static Subtitles Current { get; set; } = new(new());

    /// <summary>How many clips carry a caption.</summary>
    public int Count => _clips.Count;

    /// <summary>The table under <paramref name="folder"/>, relative to the data root unless rooted;
    /// empty when the folder is absent. A file that does not parse is skipped with a warning.</summary>
    public static Subtitles Open(string dataRoot, string folder)
    {
        var clips = new Dictionary<string, Segment[]>(StringComparer.Ordinal);
        string dir = Path.Combine(dataRoot, folder);
        if (!Directory.Exists(dir))
        {
            return new(clips);
        }

        var files = Directory.GetFiles(dir, "*.json");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            try
            {
                if (Parse(File.ReadAllText(file), clips) == 0)
                {
                    Log.Warn("sound", $"subtitles: {file} holds no entry with \"text\" or \"segments\"");
                }
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
            {
                Log.Warn("sound", $"subtitles: skipped {file}: {e.Message}");
            }
        }

        Log.Info("sound", $"subtitles: {clips.Count} clips from {dir}");
        return new(clips);
    }

    /// <summary>The line <paramref name="wavName"/> speaks <paramref name="seconds"/> into it, or
    /// null for none. A segment shows from its start until the next one starts, but no longer than
    /// <see cref="Hold"/> past its own end.</summary>
    public Line? At(string? wavName, double seconds)
    {
        if (wavName is not { Length: > 0 } || !_clips.TryGetValue(HdSounds.KeyFor(wavName), out var segments))
        {
            return null;
        }

        for (int i = segments.Length - 1; i >= 0; i--)
        {
            if (segments[i].Start > seconds)
            {
                continue;
            }

            double until = i + 1 < segments.Length ? segments[i + 1].Start : double.PositiveInfinity;
            return seconds < Math.Min(until, segments[i].End + Hold) ? segments[i].Line : null;
        }

        return null;
    }

    // Adds one file's clips over any already read and returns how many it read. A clip's segments
    // win over its text; a segment without a speaker takes the clip's, one without an end lasts
    // until the next starts.
    private static int Parse(string json, Dictionary<string, Segment[]> into)
    {
        int read = 0;
        using var doc = JsonDocument.Parse(json);
        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string? speaker = Text(entry.Value, "speaker");
            var segments = new List<Segment>();
            if (entry.Value.TryGetProperty("segments", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in list.EnumerateArray())
                {
                    if (Text(s, "text") is { } words)
                    {
                        segments.Add(new Segment(Seconds(s, "start") ?? 0, Seconds(s, "end") ?? double.PositiveInfinity,
                            new Line(Text(s, "speaker") ?? speaker, words)));
                    }
                }

                segments.Sort((a, b) => a.Start.CompareTo(b.Start));
            }
            else if (Text(entry.Value, "text") is { } whole)
            {
                segments.Add(new Segment(0, double.PositiveInfinity, new Line(speaker, whole)));
            }

            if (segments.Count > 0)
            {
                into[HdSounds.KeyFor(entry.Name)] = segments.ToArray();
                read++;
            }
        }

        return read;
    }

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s : null;

    private static double? Seconds(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    /// <summary>One caption: who speaks, null for an unnamed voice, and what they say.</summary>
    public readonly record struct Line(string? Speaker, string Text);

    // A caption timed within its clip, in seconds from the clip's start.
    private readonly record struct Segment(double Start, double End, Line Line);
}
