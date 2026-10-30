using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using CSVM.Extraction;
using CSVM.Mech3;
using Xunit;
using Xunit.Abstractions;

namespace CSVM.Tests;

/// <summary>
/// <see cref="SoundZbd"/> over archives encoded in code (CI-safe), and over both sound archives of
/// the install against today's zips. Those compare every entry's name and bytes, and every lookup
/// <see cref="SoundArchive"/> answers. Lookup rule: docs/formats/sounds.md.
/// </summary>
public class SoundZbdTests
{
    private readonly ITestOutputHelper _output;

    public SoundZbdTests(ITestOutputHelper output) => _output = output;

    /// <summary>The two sound archives, high and low quality.</summary>
    public static IEnumerable<object[]> SoundArchives() =>
        ZbdManifest.Expected.Where(IsSoundArchive).Select(path => new object[] { path });

    [Fact]
    public void ThereAreTwoSoundArchives()
    {
        Assert.Equal(new[] { "soundsh.zbd", "soundsl.zbd" }, ZbdManifest.Expected.Where(IsSoundArchive).Order());
    }

    [Fact]
    public void ARepeatIsRenamedAndTheBareNameFindsTheFirstOccurrence()
    {
        using var sounds = SoundZbd.Read(Archive(("gun.wav", "first"), ("impact.wav", "hit"), ("gun.wav", "second")), "fixture");

        Assert.Equal(new[] { "gun.wav", "impact.wav", "gun-1.wav" }, sounds.Sounds.Select(s => s.Key));
        Assert.Equal("gun.wav", sounds.Sounds[2].Name);
        Assert.Equal("first", Text(sounds.ReadWavBytes("gun.wav")));
        Assert.Equal("first", Text(sounds.ReadWavBytes("GUN.WAV")));
        Assert.Equal("second", Text(sounds.ReadWavBytes("gun-1.wav")));
        Assert.Null(sounds.ReadWavBytes("absent.wav"));
    }

    [Fact]
    public void ACaseOnlyPairKeepsBothKeysAndALookupFindsTheFirst()
    {
        using var sounds = SoundZbd.Read(Archive(("VO_Blacke_9.wav", "upper"), ("VO_blacke_9.wav", "lower")), "fixture");

        // The rename compares with case, so neither is renamed.
        Assert.Equal(new[] { "VO_Blacke_9.wav", "VO_blacke_9.wav" }, sounds.Sounds.Select(s => s.Key));
        Assert.Equal("upper", Text(sounds.ReadWavBytes("VO_blacke_9.wav")));
        Assert.Equal("lower", Text(sounds.ReadWavBytes(sounds.Sounds[1])));
    }

    [Fact]
    public void NoSourceReadsAsNoArchive()
    {
        Assert.Null(SoundZbd.Load(null, "ZBD/soundsh.zbd"));
    }

    [GameFilesTheory(AlsoExtracted = true)]
    [MemberData(nameof(SoundArchives))]
    public void TheReaderYieldsTheSoundsTheExtractionHolds(string path)
    {
        var clock = Stopwatch.StartNew();
        using var sounds = SoundZbd.Load(GameFilesData.Source, "ZBD/" + path)!;
        double openMs = clock.Elapsed.TotalMilliseconds;

        clock.Restart();
        double slowestMs = 0;
        var actual = new Dictionary<string, byte[]>();
        foreach (var sound in sounds.Sounds)
        {
            double before = clock.Elapsed.TotalMilliseconds;
            actual.Add(sound.Key, sounds.ReadWavBytes(sound));
            slowestMs = Math.Max(slowestMs, clock.Elapsed.TotalMilliseconds - before);
        }

        double readMs = clock.Elapsed.TotalMilliseconds;
        string zip = ZipPath(path);
        int compared = ModelDiff.AssertEqual(FromExtraction(zip), actual);
        Assert.True(compared > 0);

        // Every name the zip answers, in its own spelling and upper-cased, finds the same bytes
        // through SoundArchive's map as through the reader.
        using var legacy = new SoundArchive(zip);
        int lookups = 0;
        foreach (string name in actual.Keys.SelectMany(k => new[] { k, k.ToUpperInvariant() }))
        {
            Assert.True(legacy.ReadWavBytes(name).AsSpan().SequenceEqual(sounds.ReadWavBytes(name)), name);
            lookups++;
        }

        var repeats = sounds.Sounds.Where(s => s.Rename != null).ToList();
        int copies = repeats.Count(s => actual[s.Key].AsSpan().SequenceEqual(actual[s.Name]));
        _output.WriteLine($"{path}: {sounds.Sounds.Count} sounds, {repeats.Count} renamed ({copies} copies of their first), "
                          + $"opened in {openMs:F1} ms, read in {readMs:F0} ms ({readMs / sounds.Sounds.Count:F3} ms each, "
                          + $"slowest {slowestMs:F2} ms); {compared} leaf values compared, {lookups} lookups agree");
    }

    [GameFilesTheory(AlsoExtracted = true)]
    [InlineData("soundsl.zbd")]
    public void OneCorruptedByteIsFoundUnderItsSound(string path)
    {
        byte[] bytes = GameFilesData.Source!.ReadAllBytes("ZBD/" + path);
        string key;
        using (var clean = SoundZbd.Read(new MemoryStream(bytes), path))
        {
            var sound = clean.Sounds[clean.Sounds.Count / 2];
            key = sound.Key;
            bytes[sound.Entry.Offset + (sound.Entry.Length / 2)] ^= 0xFF;
        }

        using var corrupt = SoundZbd.Read(new MemoryStream(bytes), path);
        var actual = corrupt.Sounds.ToDictionary(s => s.Key, corrupt.ReadWavBytes);

        string? difference = ModelDiff.FirstDifference(FromExtraction(ZipPath(path)), actual);

        Assert.NotNull(difference);
        Assert.Contains(key, difference);
        _output.WriteLine(difference);
    }

    private static bool IsSoundArchive(string path) => path is "soundsh.zbd" or "soundsl.zbd";

    private static string ZipPath(string path) =>
        Path.Combine(TestData.ExtractedRoot!, Path.ChangeExtension(ZbdTree.Canonical(path), ".zip"));

    private static Dictionary<string, byte[]> FromExtraction(string zip)
    {
        using var archive = ZipFile.OpenRead(zip);
        var sounds = new Dictionary<string, byte[]>();
        foreach (var entry in archive.Entries.Where(e => e.FullName != "manifest.json"))
        {
            using var stream = entry.Open();
            var bytes = new byte[entry.Length];
            stream.ReadExactly(bytes);
            sounds.Add(entry.FullName, bytes);
        }

        return sounds;
    }

    private static string? Text(byte[]? bytes) => bytes == null ? null : Encoding.ASCII.GetString(bytes);

    // The container of docs/formats/zbd-archive.md: payloads, 148-byte entries, version and count.
    private static MemoryStream Archive(params (string Name, string Body)[] entries)
    {
        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        foreach (var (_, body) in entries)
        {
            writer.Write(Encoding.ASCII.GetBytes(body));
        }

        uint offset = 0;
        foreach (var (name, body) in entries)
        {
            writer.Write(offset);
            writer.Write((uint)body.Length);
            var field = new byte[140];
            Encoding.ASCII.GetBytes(name).CopyTo(field, 0);
            writer.Write(field);
            offset += (uint)body.Length;
        }

        writer.Write(1u);
        writer.Write((uint)entries.Length);
        stream.Position = 0;
        return stream;
    }
}
