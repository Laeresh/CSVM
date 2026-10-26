using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CSVM.Extraction;

/// <summary>The data track of a <c>.cue</c> sheet (docs/formats/disc-image.md). The sheet's first
/// track must be data in a <c>BINARY</c> file; a later track in another file is fine, as a
/// per-track dump has. Only <c>FILE</c>, <c>TRACK</c> and <c>INDEX</c> are read, in any case;
/// every other command is skipped. A malformed line throws <see cref="InvalidDataException"/>
/// naming it.</summary>
internal static class CueSheet
{
    private const int FramesPerSecond = 75;

    /// <summary>The first track of the sheet <paramref name="cue"/>, with its image file found
    /// beside it (<see cref="RawCdImage.Beside"/>).</summary>
    public static RawDataTrack DataTrack(string cue)
    {
        var tracks = Parse(cue);
        if (tracks.Count == 0)
        {
            throw new InvalidDataException($"{cue}: no TRACK");
        }

        var first = tracks[0];
        if (first.Mode.Equals("AUDIO", StringComparison.OrdinalIgnoreCase))
        {
            var data = tracks.Where(t => !t.Mode.Equals("AUDIO", StringComparison.OrdinalIgnoreCase)).Select(t => t.Number);
            throw new InvalidDataException(AudioFirst(cue, first.Number, data));
        }

        (int size, int offset) = first.Mode.ToUpperInvariant() switch
        {
            "MODE1/2048" or "MODE2/2048" => (2048, 0),
            "MODE1/2352" => (RawCdImage.RawSector, 16),
            "MODE2/2352" => (RawCdImage.RawSector, 24),
            "MODE2/2336" => (2336, 8),
            _ => throw new InvalidDataException($"{cue}: track {first.Number} is {first.Mode}; MODE1/2048, MODE1/2352, MODE2/2336 and MODE2/2352 are read"),
        };
        if (!first.FileType.Equals("BINARY", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"{cue}: the data track's file '{first.File}' is {first.FileType}, expected BINARY");
        }

        long start = first.Index1 ?? throw new InvalidDataException($"{cue}: track {first.Number} has no INDEX 01");
        long? end = tracks.Count > 1 && tracks[1].File == first.File ? tracks[1].Index0 ?? tracks[1].Index1 : null;
        return new RawDataTrack(RawCdImage.Beside(cue, first.File), start * size, end * size, size, offset);
    }

    /// <summary>The refusal for a sheet whose track <paramref name="first"/> is audio, naming the
    /// data tracks that follow, or saying there are none.</summary>
    internal static string AudioFirst(string sheet, long first, IEnumerable<long> data) =>
        data.FirstOrDefault() is > 0 and long later
            ? $"{sheet}: track {first} is audio and the data is track {later}; a data track after audio (CD-Extra) is not read"
            : $"{sheet}: audio tracks only, no data track";

    private static List<Track> Parse(string cue)
    {
        var tracks = new List<Track>();
        (string Name, string Type)? file = null;
        string[] lines = RawCdImage.SheetLines(cue);
        for (int i = 0; i < lines.Length; i++)
        {
            var words = Words(lines[i], cue, i + 1);
            if (words.Count == 0)
            {
                continue;
            }

            string where = $"{cue}, line {i + 1}";
            switch (words[0].ToUpperInvariant())
            {
                case "FILE":
                    Need(words, 3, where);
                    file = (words[1], words[2]);
                    break;
                case "TRACK":
                    Need(words, 3, where);
                    var (name, type) = file ?? throw new InvalidDataException($"{where}: TRACK before any FILE");
                    tracks.Add(new Track(Integer(words[1], where), words[2], name, type, null, null));
                    break;
                case "INDEX":
                    Need(words, 3, where);
                    if (tracks.Count == 0)
                    {
                        throw new InvalidDataException($"{where}: INDEX before any TRACK");
                    }

                    long frames = Frames(words[2], where);
                    tracks[^1] = Integer(words[1], where) switch
                    {
                        0 => tracks[^1] with { Index0 = frames },
                        1 => tracks[^1] with { Index1 = frames },
                        _ => tracks[^1],
                    };
                    break;
            }
        }

        return tracks;
    }

    // A word is a run of non-blank characters, or a quoted string with its quotes removed.
    private static List<string> Words(string line, string cue, int number)
    {
        var words = new List<string>();
        int at = 0;
        while (at < line.Length)
        {
            if (char.IsWhiteSpace(line[at]))
            {
                at++;
            }
            else if (line[at] == '"')
            {
                int close = line.IndexOf('"', at + 1);
                if (close < 0)
                {
                    throw new InvalidDataException($"{cue}, line {number}: a quote is not closed");
                }

                words.Add(line.Substring(at + 1, close - at - 1));
                at = close + 1;
            }
            else
            {
                int stop = at;
                while (stop < line.Length && !char.IsWhiteSpace(line[stop]))
                {
                    stop++;
                }

                words.Add(line[at..stop]);
                at = stop;
            }
        }

        return words;
    }

    private static void Need(List<string> words, int count, string where)
    {
        if (words.Count < count)
        {
            throw new InvalidDataException($"{where}: {words[0]} needs {count - 1} arguments");
        }
    }

    private static int Integer(string word, string where) =>
        int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw new InvalidDataException($"{where}: '{word}' is not a number");

    // mm:ss:ff, a CD's minutes, seconds and 1/75 s frames, counted here in frames (sectors).
    private static long Frames(string word, string where)
    {
        string[] parts = word.Split(':');
        if (parts.Length != 3)
        {
            throw new InvalidDataException($"{where}: '{word}' is not mm:ss:ff");
        }

        int minutes = Integer(parts[0], where);
        int seconds = Integer(parts[1], where);
        int frames = Integer(parts[2], where);
        if (seconds >= 60 || frames >= FramesPerSecond)
        {
            throw new InvalidDataException($"{where}: '{word}' is not mm:ss:ff");
        }

        return ((((long)minutes * 60) + seconds) * FramesPerSecond) + frames;
    }

    private readonly record struct Track(long Number, string Mode, string File, string FileType, long? Index0, long? Index1);
}
