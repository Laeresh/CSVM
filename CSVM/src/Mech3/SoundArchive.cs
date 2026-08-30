using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Sound lookup over a mech3ax sound extraction (soundsh/soundsl.zbd → WAVs),
/// from a ZIP or a directory. Decodes via <see cref="WavFile"/> (the game's WAVs
/// are MS ADPCM, which Godot can't load) into cached <see cref="AudioStreamWav"/>s.
/// ⚠ <see cref="Dispose"/> releases the OS handle but does NOT end the archive's usable life:
/// a later <see cref="Find"/> reopens the zip for that one read. See <see cref="Dispose"/>.
/// </summary>
public sealed class SoundArchive : IDisposable
{
    private readonly string? _zipPath;
    private readonly string? _dir;

    // Name -> in-zip path, built once and kept across Dispose: it is what lets a post-Dispose read
    // find its entry in a transient archive without reopening to rebuild the map first.
    private readonly Dictionary<string, string> _entryPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AudioStreamWav?> _cache = new(StringComparer.OrdinalIgnoreCase);

    private ZipArchive? _zip;
    private int _reopensLogged;

    public SoundArchive(string path)
    {
        if (Directory.Exists(path))
        {
            _dir = path;
        }
        else
        {
            _zipPath = path;
            _zip = ZipFile.OpenRead(path);
            foreach (var entry in _zip.Entries)
                if (entry.Name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    _entryPaths[entry.Name] = entry.FullName;
        }
    }

    /// <summary>Loads a WAV by file name (e.g. "bloodhawk.wav"); null if missing/undecodable.
    /// <paramref name="looped"/> marks the whole stream as a forward loop. <paramref name="warn"/>
    /// is false for speculative bulk decodes (the sound prewarm): a per-chapter archive legitimately
    /// lacks WAVs the program can reference, and the authoritative "silent for the session" report
    /// happens at the point of use, not here.</summary>
    public AudioStreamWav? Find(string wavName, bool looped, bool warn = true)
    {
        var key = $"{wavName}|{looped}";
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        AudioStreamWav? stream = null;
        var bytes = ReadBytes(wavName);
        if (bytes == null)
        {
            if (warn)
            {
                GD.PushWarning($"sound not found in archive: {wavName}");
            }
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

    /// <summary>The raw bytes of one WAV, or null when the archive lacks it or cannot read it.
    /// The read primitive under <see cref="Find"/>, public because <see cref="Find"/> itself
    /// returns a Godot resource that a unit test cannot construct — this is where the
    /// reopen-after-<see cref="Dispose"/> contract is asserted.</summary>
    public byte[]? ReadWavBytes(string wavName) => ReadBytes(wavName);

    /// <summary>Releases the held zip handle. Idempotent, and NOT the end of the archive's usable
    /// life: a later <see cref="Find"/> reopens the file for that one read.
    /// ⚠ Do not restore a one-way close. The build-scoped handle rested on the prewarm being
    /// complete, it is not, and the throw that produced was invisible on an unpacked tree. The
    /// lifetime rule and how it shipped: this module's entry in docs/architecture.md.</summary>
    public void Dispose()
    {
        _zip?.Dispose();
        _zip = null;
    }

    private static byte[] Read(ZipArchive archive, string entryPath)
    {
        var entry = archive.GetEntry(entryPath) ?? throw new FileNotFoundException(entryPath);
        using var s = entry.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private byte[]? ReadBytes(string wavName)
    {
        if (_dir != null)
        {
            var path = Path.Combine(_dir, wavName);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        if (!_entryPaths.TryGetValue(wavName, out var entryPath))
            return null;
        try
        {
            // Open-read-close per miss once the session's handle is gone, rather than holding a
            // reopened one: the entry map above is what makes that cheap, Find caches the decoded
            // stream, so each distinct sound pays this at most once.
            if (_zip != null)
            {
                return Read(_zip, entryPath);
            }
            ReportReopen(wavName);
            using var archive = ZipFile.OpenRead(_zipPath!);
            return Read(archive, entryPath);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ObjectDisposedException
                                       or UnauthorizedAccessException)
        {
            // A sound that cannot be read is silence, never a throw: this runs inside the session
            // step, where an escaping exception skips every later phase (the AI included).
            Log.Warn("sound", $"sound read failed name={wavName} zip={_zipPath} error={e.GetType().Name}: {e.Message}");
            return null;
        }
    }

    // Each reopen is a sound the build-time prewarm did not cover. Capped, because the paths that
    // reach here (gun loops, impacts, spawns) fire every time the trigger is pulled.
    private void ReportReopen(string wavName)
    {
        if (_reopensLogged >= 8)
        {
            return;
        }
        _reopensLogged++;
        Log.Info("sound", $"archive reopened after close for name={wavName} — prewarm missed it");
    }
}
