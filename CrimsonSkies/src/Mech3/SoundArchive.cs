using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Godot;

namespace CrimsonSkies.Mech3;

/// <summary>
/// Sound lookup over a mech3ax sound extraction (soundsh/soundsl.zbd → WAVs),
/// from a ZIP or a directory. Decodes via <see cref="WavFile"/> (the game's WAVs
/// are MS ADPCM, which Godot can't load) into cached <see cref="AudioStreamWav"/>s.
/// </summary>
public sealed class SoundArchive : IDisposable
{
    private readonly ZipArchive? _zip;
    private readonly string? _dir;
    private readonly Dictionary<string, ZipArchiveEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AudioStreamWav?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public SoundArchive(string path)
    {
        if (Directory.Exists(path))
        {
            _dir = path;
        }
        else
        {
            _zip = ZipFile.OpenRead(path);
            foreach (var entry in _zip.Entries)
                if (entry.Name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    _entries[entry.Name] = entry;
        }
    }

    /// <summary>Loads a WAV by file name (e.g. "bloodhawk.wav"); null if missing/undecodable.
    /// <paramref name="looped"/> marks the whole stream as a forward loop.</summary>
    public AudioStreamWav? Find(string wavName, bool looped)
    {
        var key = $"{wavName}|{looped}";
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        AudioStreamWav? stream = null;
        var bytes = ReadBytes(wavName);
        if (bytes == null)
        {
            GD.PushWarning($"sound not found in archive: {wavName}");
        }
        else
        {
            try
            {
                var wav = WavFile.Parse(bytes);
                var data = new byte[wav.Samples.Length * 2];
                Buffer.BlockCopy(wav.Samples, 0, data, 0, data.Length);
                stream = new AudioStreamWav
                {
                    Data = data,
                    Format = AudioStreamWav.FormatEnum.Format16Bits,
                    MixRate = wav.SampleRate,
                    Stereo = wav.Channels == 2,
                    LoopMode = looped ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled,
                    LoopEnd = wav.Frames,
                };
            }
            catch (Exception e)
            {
                GD.PushWarning($"sound '{wavName}' failed to decode: {e.Message}");
            }
        }
        _cache[key] = stream;
        return stream;
    }

    private byte[]? ReadBytes(string wavName)
    {
        if (_dir != null)
        {
            var path = Path.Combine(_dir, wavName);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        if (!_entries.TryGetValue(wavName, out var entry))
            return null;
        using var s = entry.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public void Dispose() => _zip?.Dispose();
}
