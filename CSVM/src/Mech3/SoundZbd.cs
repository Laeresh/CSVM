using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Extraction;

namespace CSVM.Mech3;

/// <summary>One sound of a sound archive: its table entry and the collision rename of a repeated
/// name, null for a first occurrence.</summary>
public sealed record SoundFile(ZbdEntry Entry, string? Rename)
{
    /// <summary>The archive table's name, <c>bloodhawk.wav</c>.</summary>
    public string Name => Entry.Name;

    /// <summary>The name the sound is looked up by: today's zip entry name.</summary>
    public string Key => Rename ?? Entry.Name;
}

/// <summary>
/// Reads <c>soundsh.zbd</c> or <c>soundsl.zbd</c> into its <see cref="SoundFile"/>s in table order,
/// engine-free. Opening reads only the table; each WAV is read on demand, so the 128 MB archive is
/// never held whole. Owns the archive's stream, and reads may come from any thread. Lookup rule:
/// docs/formats/sounds.md.
/// </summary>
public sealed class SoundZbd : IDisposable
{
    private readonly ZbdArchive _archive;
    private readonly Dictionary<string, SoundFile> _byKey = new(StringComparer.OrdinalIgnoreCase);

    private SoundZbd(ZbdArchive archive)
    {
        _archive = archive;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var sounds = new List<SoundFile>(archive.Entries.Count);
        foreach (var entry in archive.Entries)
        {
            var sound = new SoundFile(entry, ZbdArchive.Rename(seen, entry.Name));
            sounds.Add(sound);

            // First in table order wins a case-only pair; the shipped pair's payloads are identical.
            _byKey.TryAdd(sound.Key, sound);
        }

        Sounds = sounds;
    }

    /// <summary>Every sound in table order, repeats included under their rename.</summary>
    public IReadOnlyList<SoundFile> Sounds { get; }

    /// <summary>The archive at <paramref name="path"/> (<c>ZBD/soundsh.zbd</c>), or null without a
    /// source or without the file. Throws <see cref="InvalidDataException"/> when its table does
    /// not read.</summary>
    public static SoundZbd? Load(IGameFileSource? game, string path) =>
        game != null && game.Exists(path) ? new SoundZbd(ZbdArchive.Open(game, path)) : null;

    /// <summary>Reads the table of the archive in the seekable <paramref name="stream"/>, which the
    /// result then owns. <paramref name="name"/> labels error messages.</summary>
    public static SoundZbd Read(Stream stream, string name) => new(ZbdArchive.Read(stream, name));

    /// <summary>The WAV bytes of the sound whose <see cref="SoundFile.Key"/> is
    /// <paramref name="name"/>, case ignored, or null when absent.</summary>
    public byte[]? ReadWavBytes(string name) =>
        _byKey.TryGetValue(name, out var sound) ? ReadWavBytes(sound) : null;

    /// <summary>The WAV bytes of <paramref name="sound"/>, one of <see cref="Sounds"/>.</summary>
    public byte[] ReadWavBytes(SoundFile sound) => _archive.ReadEntry(sound.Entry);

    /// <summary>Closes the archive's stream.</summary>
    public void Dispose() => _archive.Dispose();
}
