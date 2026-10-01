using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CSVM.Tooling;

/// <summary>
/// The synthetic tree's sound archive. Its definitions are the hand-authored
/// <c>fixtures/synthetic/zrdr/sounds.json</c>, copied as it stands. Its WAVs sit in
/// <c>soundsh/</c>, the unpacked form of <c>soundsh.zip</c>. Each is generated MS ADPCM, one per
/// entry of <c>fixtures/synthetic/soundsh/manifest.json</c>, which gives a length and a tone or
/// noise. The manifest must name exactly the WAVs the <c>SETS</c> block names, so the two cannot
/// drift. Shapes: <c>docs/formats/sounds.md</c>.
/// </summary>
public static class SyntheticSounds
{
    /// <summary>The sample rate every WAV is written at, the 22050 Hz the format page says the
    /// shipped archive holds.</summary>
    public const int Rate = 22050;

    // Where the two records sit under the fixtures root, and where they land under extracted/.
    private const string SoundsFixture = "synthetic/zrdr/sounds.json";
    private const string ManifestFixture = "synthetic/" + ArchiveFolder + "/manifest.json";
    private const string ArchiveFolder = "soundsh";

    // The peak level of a generated signal, as a share of full scale. It reads plainly in a capture
    // and stays far enough under the clip that the ADPCM step never saturates.
    private const double Level = 0.3;

    /// <summary>Writes <c>zrdr/sounds.json</c> and the WAV archive under <paramref name="tree"/>.
    /// Throws when a WAV the definitions name has no manifest entry, or the other way round.</summary>
    public static void WriteArchive(SyntheticTree tree)
    {
        string sounds = tree.CopyFixture(SoundsFixture, "zrdr/sounds.json");
        var entries = Entries(tree.Fixture(ManifestFixture));
        // Through the loader a session uses, so the names checked are the ones a run resolves.
        var named = new HashSet<string>(
            Mech3.SoundDefs.Load(Path.GetDirectoryName(sounds)!).Values.Select(d => d.WavName),
            StringComparer.OrdinalIgnoreCase);
        var listed = new HashSet<string>(entries.Select(e => e.Name), StringComparer.OrdinalIgnoreCase);
        var unlisted = named.Where(n => !listed.Contains(n)).ToList();
        var unused = listed.Where(n => !named.Contains(n)).ToList();
        if (unlisted.Count > 0 || unused.Count > 0)
        {
            throw new InvalidDataException(
                $"{ManifestFixture} and {SoundsFixture} disagree: no manifest entry for [{string.Join(", ", unlisted)}], no definition for [{string.Join(", ", unused)}]");
        }

        foreach (var (name, seconds, hz) in entries)
        {
            tree.WriteBytes($"{ArchiveFolder}/{name}", WavWriter.MsAdpcm(Signal(name, seconds, hz), Rate));
        }
    }

    /// <summary>The samples for one manifest entry: a tone at <paramref name="hz"/> with its octave
    /// at a quarter weight, or seeded white noise when <paramref name="hz"/> is 0. The noise is
    /// seeded by the name, so a rebuild writes the same bytes.</summary>
    public static short[] Signal(string name, double seconds, double hz)
    {
        int count = Math.Max((int)Math.Round(seconds * Rate), 2);
        var samples = new short[count];
        uint state = 2166136261;
        foreach (char c in name)
        {
            state = (state ^ c) * 16777619;
        }

        for (int i = 0; i < count; i++)
        {
            double value;
            if (hz > 0)
            {
                double phase = 2 * Math.PI * hz * i / Rate;
                value = (0.75 * Math.Sin(phase)) + (0.25 * Math.Sin(2 * phase));
            }
            else
            {
                state = (state * 1664525) + 1013904223;
                value = ((state >> 8) / (double)(1 << 24) * 2) - 1;
            }

            samples[i] = (short)Math.Round(value * Level * short.MaxValue);
        }

        return samples;
    }

    // Every manifest entry's WAV name, length and tone. A missing field is an error in the record,
    // since a guessed length would be read as the authored one.
    private static List<(string Name, double Seconds, double Hz)> Entries(string manifestPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var entries = new List<(string, double, double)>();
        foreach (var wav in doc.RootElement.GetProperty("wavs").EnumerateArray())
        {
            string name = wav.GetProperty("name").GetString()
                ?? throw new InvalidDataException($"{manifestPath}: a wavs entry has a null name");
            entries.Add((name, wav.GetProperty("seconds").GetDouble(), wav.GetProperty("hz").GetDouble()));
        }

        return entries;
    }
}
