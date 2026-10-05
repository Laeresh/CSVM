using System;
using System.Globalization;
using System.IO;

namespace CSVM.Launch;

/// <summary>
/// Keeps a headset launch's shader cache writes away from the flat launches after it. A flat
/// launch misreads a cache file holding a variant it has disabled. <c>--xr-mode on</c> has no
/// feature tag to switch the cache off with. So a launch that compiled the stereo variants deletes,
/// as it ends, every cache file written since its process started. A marker stands while it runs,
/// so a launch that never reached its end is swept by the next launch to boot. The misread is
/// LOG-25 in the verification doc.
/// </summary>
public sealed class XrShaderCache
{
    /// <summary>The marker's file name in the user directory: the process id and its start time.</summary>
    public const string MarkerName = "xr_shader_cache.pending";

    /// <summary>Godot's shader cache folder under the user directory (<c>RendererCompositorRD</c>).</summary>
    public const string CacheFolder = "shader_cache";

    private readonly string _userDir;
    private readonly DateTime _sinceUtc;
    private bool _open = true;

    private XrShaderCache(string userDir, DateTime sinceUtc)
    {
        _userDir = userDir;
        _sinceUtc = sinceUtc;
    }

    /// <summary>Opens the stereo launch's watch: writes the marker naming <paramref name="pid"/>
    /// and <paramref name="startUtc"/>, the process start every later sweep counts from. Godot
    /// writes its built-in shaders before any game code runs, which is why it is not this call's
    /// own time.</summary>
    public static XrShaderCache Begin(string userDir, int pid, DateTime startUtc)
    {
        ArgumentException.ThrowIfNullOrEmpty(userDir);
        try
        {
            Directory.CreateDirectory(userDir);
            File.WriteAllText(Path.Combine(userDir, MarkerName), string.Create(CultureInfo.InvariantCulture,
                $"{pid} {startUtc.ToUniversalTime().Ticks}"));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The end-of-launch sweep still runs; only a crash's recovery is lost.
        }

        return new XrShaderCache(userDir, startUtc.ToUniversalTime());
    }

    /// <summary>A launch's boot over a marker another process left. When that process is gone, it
    /// sweeps from that start and drops the marker. The callback answers a live process's start
    /// by id, or null for none. Null for no marker or a live session.</summary>
    public static int? Recover(string userDir, Func<int, DateTime?> liveStartUtc)
    {
        ArgumentException.ThrowIfNullOrEmpty(userDir);
        ArgumentNullException.ThrowIfNull(liveStartUtc);
        string marker = Path.Combine(userDir, MarkerName);
        string text;
        try
        {
            if (!File.Exists(marker))
            {
                return null;
            }

            text = File.ReadAllText(marker);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int pid)
            || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long ticks)
            || ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
        {
            TryDelete(marker);
            return 0;
        }

        var since = new DateTime(ticks, DateTimeKind.Utc);
        if (liveStartUtc(pid) is { } live && live.ToUniversalTime() == since)
        {
            return null;
        }

        int swept = Sweep(Path.Combine(userDir, CacheFolder), since);
        TryDelete(marker);
        return swept;
    }

    /// <summary>Deletes every <c>.cache</c> file under <paramref name="cacheDir"/> last written at
    /// or after <paramref name="sinceUtc"/>, and answers how many. A file another launch holds open
    /// is left; a flat launch rewrites any file it compiles.</summary>
    public static int Sweep(string cacheDir, DateTime sinceUtc)
    {
        if (!Directory.Exists(cacheDir))
        {
            return 0;
        }

        var since = sinceUtc.ToUniversalTime();
        int swept = 0;
        foreach (string file in Directory.EnumerateFiles(cacheDir, "*.cache", SearchOption.AllDirectories))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) >= since)
                {
                    File.Delete(file);
                    swept++;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Held open by another launch at this moment, so it stays; it is not counted.
            }
        }

        return swept;
    }

    /// <summary>The start time of a live process by id, the answer <see cref="Recover"/> asks for;
    /// null where none runs or it cannot be read.</summary>
    public static DateTime? LiveStartUtc(int pid)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return process.HasExited ? null : process.StartTime.ToUniversalTime();
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException
            or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Ends the watch once: deletes every cache file written since the process started,
    /// then the marker, and answers how many files went. A second call does nothing, so a relaunch
    /// that swept before starting its child never sweeps what the child writes.</summary>
    public int End()
    {
        if (!_open)
        {
            return 0;
        }

        _open = false;
        int swept = Sweep(Path.Combine(_userDir, CacheFolder), _sinceUtc);
        TryDelete(Path.Combine(_userDir, MarkerName));
        return swept;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A marker left behind is swept again by the next boot, which is harmless.
        }
    }
}
