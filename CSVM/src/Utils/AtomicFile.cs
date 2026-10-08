using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CSVM.Utils;

/// <summary>
/// Whole-file replacement for player data. The text lands in a sibling temp file, and one rename
/// puts it over the target. A kill or a full disk mid-write therefore leaves the previous file, or
/// none on a first save, never a truncated one. ⚠ Do not persist player data with
/// <c>File.WriteAllText</c> on the target itself: it truncates the file before writing.
/// A store that falls back to defaults reads through <see cref="ReadAllText"/> and
/// <see cref="SetAside"/>. Then saving those defaults never replaces a file it could not read.
/// </summary>
public static class AtomicFile
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    // Full paths whose last read failed. Process-wide, because every store builds a fresh instance
    // per call and the guard must outlive it.
    private static readonly HashSet<string> Unread = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Writes <paramref name="text"/> to <paramref name="path"/> as BOM-less UTF-8 through
    /// <c>path.tmp</c>. A temp file a killed write left behind is overwritten. The directory must
    /// exist; an IO failure reaches the caller, whose policy it is. A path whose last
    /// <see cref="ReadAllText"/> failed is not written, and the skip is logged.</summary>
    public static void WriteAllText(string path, string text)
    {
        if (IsUnread(path))
        {
            Log.Warn("core", $"player file {path}: not saved, since this session could not read it and would replace it with defaults");
            return;
        }

        string temp = path + ".tmp";
        File.WriteAllText(temp, text, Utf8);
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>The text of <paramref name="path"/>, or null when there is no such file. A read that
    /// fails (a lock, a denied ACL) is logged and also answers null. The path then stays unwritable
    /// until a later read of it succeeds, so a transient failure costs one load, never the file.
    /// </summary>
    public static string? ReadAllText(string path)
    {
        try
        {
            string? text = File.Exists(path) ? File.ReadAllText(path) : null;
            MarkUnread(path, false);
            return text;
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            MarkUnread(path, false);
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            MarkUnread(path, true);
            Log.Warn("core", $"player file {path}: unreadable, defaults stand in and it will not be saved over this session ({e.Message})");
            return null;
        }
    }

    /// <summary>Moves a player file its store cannot parse to <c>path.bad</c>, replacing an older
    /// one, and logs <paramref name="reason"/>. The next save writes a fresh file and the player's
    /// edit survives beside it. A move that fails leaves the path unwritable, as a failed read does.
    /// </summary>
    public static void SetAside(string path, string reason)
    {
        string bad = path + ".bad";
        try
        {
            File.Move(path, bad, overwrite: true);
            MarkUnread(path, false);
            Log.Warn("core", $"player file {path}: unreadable ({reason}); moved to {bad}, defaults stand in");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            MarkUnread(path, true);
            Log.Warn("core", $"player file {path}: unreadable ({reason}) and not movable to {bad} ({e.Message}); it will not be saved over this session");
        }
    }

    private static bool IsUnread(string path)
    {
        lock (Unread)
        {
            return Unread.Contains(Path.GetFullPath(path));
        }
    }

    private static void MarkUnread(string path, bool unread)
    {
        string full = Path.GetFullPath(path);
        lock (Unread)
        {
            if (unread)
            {
                Unread.Add(full);
            }
            else
            {
                Unread.Remove(full);
            }
        }
    }
}
