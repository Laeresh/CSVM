using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using CSVM.Utils;

namespace CSVM.Extraction;

/// <summary>
/// Higher-resolution replacements for the extracted art, read in place from the HD folder's zips
/// and files (<c>textures.hd</c>, <c>--hd=</c>, default <c>HD</c>).
/// Each pack's own top folder is dropped. A path left with a folder replaces that file under
/// <c>extracted/</c>, matched without case, its own extension first: <c>rimage/c1m1.png</c>,
/// <c>C3/rtexture12/cliff01.png</c>. A bare name replaces that world texture in every archive.
/// Engine-free; the callers decode and size what this returns.
/// </summary>
public sealed class HdTextures : IDisposable
{
    private readonly Dictionary<string, (ZipArchive? Zip, string Path)> _byKey = new(StringComparer.Ordinal);

    // The same files keyed with their extension. The scrapbook draws X.PNG on the page and X.JPG in
    // the zoom, often different pictures, and a pack may ship both.
    private readonly Dictionary<string, (ZipArchive? Zip, string Path)> _byExactKey = new(StringComparer.Ordinal);
    private readonly List<ZipArchive> _zips = new();
    private readonly string _extracted;

    private HdTextures(string extracted = "")
    {
        _extracted = extracted;
    }

    /// <summary>The replacements every loader consults, settled once at launch.</summary>
    public static HdTextures Current { get; set; } = new();

    /// <summary>How many replacements the folder offers.</summary>
    public int Count => _byKey.Count;

    /// <summary>The replacements under <paramref name="folder"/>, relative to the data root unless
    /// rooted; empty when the folder is absent. Sources load in name order and a later one wins, so
    /// a small fix pack named after the main one overrides it.</summary>
    public static HdTextures Open(string dataRoot, string folder)
    {
        var hd = new HdTextures(Path.Combine(dataRoot, ExtractionRun.ExtractedFolder));
        string root = Path.Combine(dataRoot, folder);
        if (folder.Length == 0 || !Directory.Exists(root))
        {
            return hd;
        }

        var sources = new List<string>(Directory.EnumerateFileSystemEntries(root));
        sources.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (string source in sources)
        {
            if (Directory.Exists(source))
            {
                foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                {
                    hd.Add(Path.GetRelativePath(source, file), null, file);
                }
            }
            else if (source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var zip = ZipFile.OpenRead(source);
                    hd._zips.Add(zip);
                    foreach (var entry in zip.Entries)
                    {
                        hd.Add(DropTop(entry.FullName), zip, entry.FullName);
                    }
                }
                catch (InvalidDataException e)
                {
                    Log.Warn("world", $"HD pack is not a readable zip, skipped: {source} ({e.Message})");
                }
            }
        }

        Log.Info("world", $"HD textures: {hd.Count} replacements from {root}");
        return hd;
    }

    /// <summary>The replacement for one texture of the archive at <paramref name="archivePath"/>
    /// (its folder or <c>.zip</c>), by its archive name, or null. The archive's own entry beats the
    /// bare name, which is how a pack carries a chapter's own version of a shared name.
    /// ⚠ Never gate a bare name on its resemblance to the original. The thumbnail distances of
    /// correct redraws and of another chapter's same-named texture overlap at every size.</summary>
    public byte[]? ForWorld(string archivePath, string archiveBaseName)
    {
        if (_byKey.Count == 0)
        {
            return null;
        }

        string name = archiveBaseName.ToLowerInvariant();
        return (Relative(archivePath) is { } archive ? Read(_byKey, KeyOf(archive) + "/" + name) : null) ?? Read(_byKey, name);
    }

    /// <summary>The replacement for the file at <paramref name="originalPath"/> under
    /// <c>extracted/</c>, or null. A pack file with the original's own extension beats one
    /// without.</summary>
    public byte[]? ForExtracted(string originalPath) =>
        _byKey.Count > 0 && Relative(originalPath) is { } rel
            ? Read(_byExactKey, ExactKeyOf(rel)) ?? Read(_byKey, KeyOf(rel))
            : null;

    public void Dispose()
    {
        foreach (var zip in _zips)
        {
            zip.Dispose();
        }

        _zips.Clear();
        _byKey.Clear();
        _byExactKey.Clear();
    }

    // "rimage\C1M1.PNG" -> "rimage/c1m1": case and extension never decide a match, since the
    // pack writes .png over originals that are .JPG, .TGA and .TIF.
    internal static string KeyOf(string relativePath)
    {
        string unix = relativePath.Replace('\\', '/');
        int dot = unix.LastIndexOf('.');
        return (dot > unix.LastIndexOf('/') ? unix[..dot] : unix).ToLowerInvariant();
    }

    private static string ExactKeyOf(string relativePath) => relativePath.Replace('\\', '/').ToLowerInvariant();

    private static string DropTop(string entryPath)
    {
        int slash = entryPath.IndexOf('/');
        return slash < 0 ? entryPath : entryPath[(slash + 1)..];
    }

    private string? Relative(string path)
    {
        string rel = Path.GetRelativePath(_extracted, path);
        return rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel) ? null : rel;
    }

    private void Add(string relativePath, ZipArchive? zip, string path)
    {
        string ext = Path.GetExtension(relativePath).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg"))
        {
            return;
        }

        _byKey[KeyOf(relativePath)] = (zip, path);
        _byExactKey[ExactKeyOf(relativePath)] = (zip, path);
    }

    // ⚠ Locked: a ZipArchive is not safe to read from two threads, and the world archive and the
    // menus may both reach here.
    private byte[]? Read(Dictionary<string, (ZipArchive? Zip, string Path)> map, string key)
    {
        if (!map.TryGetValue(key, out var source))
        {
            return null;
        }

        lock (_byKey)
        {
            if (source.Zip == null)
            {
                return File.Exists(source.Path) ? File.ReadAllBytes(source.Path) : null;
            }

            using var stream = source.Zip.GetEntry(source.Path)!.Open();
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            return bytes.ToArray();
        }
    }
}
