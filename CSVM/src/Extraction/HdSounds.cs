using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Utils;

namespace CSVM.Extraction;

/// <summary>The audio container a sound pack file holds, read from its leading bytes.</summary>
public enum HdSoundFormat
{
    /// <summary>A RIFF WAVE file, which <c>Mech3.WavFile</c> decodes.</summary>
    Wav,

    /// <summary>An Ogg Vorbis stream, which Godot decodes.</summary>
    Ogg,
}

/// <summary>One replacement sound: its bytes, their container, and the label a message names it
/// by (the file's path, or the zip and its entry).</summary>
public readonly record struct HdSound(byte[] Bytes, HdSoundFormat Format, string Source);

/// <summary>
/// Replacement sounds read through a <see cref="PackIndex"/> from the sound pack folder
/// (<c>sounds.hd</c>, <c>--hd-sounds=</c>, default <c>HDSounds</c>). A <c>.wav</c> or <c>.ogg</c>
/// replaces the original WAV of its file name, matched without case, extension or folder, so
/// <c>Explosion_1.ogg</c> replaces <c>explosion_1.wav</c>. A pack's top-level
/// <c>enhanced_sound.json</c> overrides the shipped Enhanced Sound values, the last pack's
/// winning. Only Enhanced Sound plays a pack (docs/formats/sounds.md, "Sound packs"). Engine-free.
/// </summary>
public sealed class HdSounds : IDisposable
{
    /// <summary>The name of the override file at a pack's top.</summary>
    public const string OverrideName = "enhanced_sound.json";

    /// <summary>The folder a launch with nothing configured reads.</summary>
    public const string DefaultFolder = "HDSounds";

    /// <summary>The <c>config.json</c> key naming the folder.</summary>
    public const string ConfigKey = "sounds.hd";

    // A slash never survives Path.GetFileName, so no sound's key can take this one.
    private const string OverrideKey = "/override";

    private static readonly IReadOnlySet<string> Extensions = new HashSet<string> { ".wav", ".ogg", ".json" };

    private readonly PackIndex _index;

    private HdSounds(PackIndex index)
    {
        _index = index;
        Count = index.Count - (index.Find(OverrideKey) != null ? 1 : 0);
    }

    /// <summary>The pack every sound lookup consults, settled once at launch. Empty until then, so
    /// a suite or a tool that never opens one plays the original's sounds.</summary>
    public static HdSounds Current { get; set; } = new(PackIndex.Empty());

    /// <summary>How many replacement sounds the folder offers.</summary>
    public int Count { get; }

    /// <summary>Whether a pack carries an override file.</summary>
    public bool HasOverride => _index.Find(OverrideKey) != null;

    /// <summary>Where the override file is read from, as a message names it. Null without one.</summary>
    public string? OverrideSource => _index.Origin(OverrideKey);

    /// <summary>The override file's path on disk, which a tuner may write. Null without one, or
    /// when it sits inside a zip.</summary>
    public string? OverridePath => _index.Find(OverrideKey)?.LoosePath;

    /// <summary>The folder a launch reads: <paramref name="flag"/> when <c>--hd-sounds=</c> named
    /// one, else none under <paramref name="det"/>, else <paramref name="configured"/>, the
    /// <see cref="ConfigKey"/> value. An empty name reads no pack.</summary>
    public static string? FolderFor(string? flag, bool det, string configured) =>
        flag ?? (det ? null : configured);

    /// <summary>The replacements under <paramref name="folder"/>, relative to the data root unless
    /// rooted; empty when the folder is absent. Packs load in name order and a later one wins.</summary>
    public static HdSounds Open(string dataRoot, string folder)
    {
        var sounds = new HdSounds(PackIndex.Open(dataRoot, folder, Extensions, KeyOfFile));
        if (sounds._index.Root is { } root)
        {
            Log.Info("sound", $"HD sounds: {sounds.Count} replacements{(sounds.HasOverride ? $", override {sounds.OverrideSource}" : string.Empty)} from {root}");
        }

        return sounds;
    }

    /// <summary>The key an original WAV is replaced under: its file name's stem in lower case.</summary>
    public static string KeyFor(string wavName) => PackIndex.KeyOf(Path.GetFileName(wavName.Replace('\\', '/')));

    /// <summary>The container <paramref name="bytes"/> hold, by their leading bytes, or null for
    /// neither. The extension never decides, so a mislabelled file is still read right.</summary>
    public static HdSoundFormat? Sniff(byte[] bytes)
    {
        if (bytes.Length >= 12 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F'
            && bytes[8] == 'W' && bytes[9] == 'A' && bytes[10] == 'V' && bytes[11] == 'E')
        {
            return HdSoundFormat.Wav;
        }

        if (bytes.Length >= 4 && bytes[0] == 'O' && bytes[1] == 'g' && bytes[2] == 'g' && bytes[3] == 'S')
        {
            return HdSoundFormat.Ogg;
        }

        return null;
    }

    /// <summary>Whether the pack holds a file for the original WAV <paramref name="wavName"/>, read
    /// from the index alone.</summary>
    public bool Replaces(string wavName) => Count > 0 && _index.Find(KeyFor(wavName)) != null;

    /// <summary>The replacement for the original WAV <paramref name="wavName"/>, or null. A file
    /// that is neither WAV nor Ogg is passed over with a warning, so the original plays.</summary>
    public HdSound? For(string wavName)
    {
        if (Count == 0)
        {
            return null;
        }

        string key = KeyFor(wavName);
        if (_index.Read(key) is not { } bytes)
        {
            return null;
        }

        string source = _index.Origin(key) ?? key;
        if (Sniff(bytes) is not { } format)
        {
            Log.Warn("sound", $"HD sound {source} is neither WAV nor Ogg Vorbis, {wavName} plays the original");
            return null;
        }

        return new HdSound(bytes, format, source);
    }

    /// <summary>The override file's bytes, or null without one.</summary>
    public byte[]? OverrideBytes() => _index.Read(OverrideKey);

    public void Dispose() => _index.Dispose();

    // A sound keys by its file name alone, so a pack may sort its files into folders. Only a JSON
    // file at the pack's top and named for the override is read; every other JSON is refused.
    private static string? KeyOfFile(PackFile file)
    {
        string unix = file.RelativePath.Replace('\\', '/');
        if (Path.GetExtension(unix).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return unix.Equals(OverrideName, StringComparison.OrdinalIgnoreCase) ? OverrideKey : null;
        }

        return KeyFor(unix);
    }
}
