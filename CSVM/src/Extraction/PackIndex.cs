using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using CSVM.Utils;

namespace CSVM.Extraction;

/// <summary>One file a pack offers, as a key function sees it: the pack it came from (a subfolder or
/// a <c>.zip</c>), its path with the pack's top folder dropped, and whether it sits in a zip.</summary>
internal readonly record struct PackFile(string Pack, string RelativePath, bool InZip);

/// <summary>Where one indexed file is read from: its lower-case extension, and its path on disk
/// when it is a loose file rather than a zip entry.</summary>
internal readonly record struct PackEntry(string Extension, string? LoosePath);

/// <summary>
/// The files a replacement pack folder offers, read in place from its zips and subfolders and
/// filtered to the accepted extensions. Packs load in name order and so do the files within one
/// pack, and a later file wins its key. Two files differing only in case or extension therefore
/// resolve by name, never by enumeration order. Each pack's own top folder is dropped. The
/// caller's key function maps what is left to a key, or to null to refuse the file.
/// Engine-free; <see cref="HdTextures"/> and <see cref="HdModels"/> are its callers.
/// </summary>
internal sealed class PackIndex : IDisposable
{
    // Ignoring case first, then ordinal, so the order is total on a case-sensitive disk too.
    private static readonly Comparison<string> NameOrder = (a, b) =>
    {
        int c = StringComparer.OrdinalIgnoreCase.Compare(a, b);
        return c != 0 ? c : StringComparer.Ordinal.Compare(a, b);
    };

    private readonly Dictionary<string, (ZipArchive? Zip, string Path, string Extension)> _byKey = new(StringComparer.Ordinal);
    private readonly List<ZipArchive> _zips = new();

    private PackIndex(string? root)
    {
        Root = root;
    }

    /// <summary>The folder that was read, or null when there was none to read.</summary>
    public string? Root { get; }

    /// <summary>How many keys the folder offers.</summary>
    public int Count => _byKey.Count;

    /// <summary>An index of no folder, for a launch that reads none.</summary>
    public static PackIndex Empty() => new(null);

    /// <summary>The packs under <paramref name="folder"/>, relative to the data root unless rooted;
    /// <see cref="Empty"/> when the name is empty or the folder absent.</summary>
    public static PackIndex Open(string dataRoot, string folder, IReadOnlySet<string> extensions, Func<PackFile, string?> keyOf)
    {
        string root = Path.Combine(dataRoot, folder);
        if (folder.Length == 0 || !Directory.Exists(root))
        {
            return Empty();
        }

        var index = new PackIndex(root);
        var sources = new List<string>(Directory.EnumerateFileSystemEntries(root));
        sources.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (string source in sources)
        {
            if (Directory.Exists(source))
            {
                var files = new List<string>(Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories));
                files.Sort(NameOrder);
                foreach (string file in files)
                {
                    index.Add(new PackFile(source, Path.GetRelativePath(source, file), false), null, file, extensions, keyOf);
                }
            }
            else if (source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                index.AddZip(source, extensions, keyOf);
            }
        }

        return index;
    }

    /// <summary><c>rimage\C1M1.PNG</c> to <c>rimage/c1m1</c>: case and extension never decide a
    /// match, since the texture packs write <c>.png</c> over originals that are <c>.JPG</c>,
    /// <c>.TGA</c> and <c>.TIF</c>.</summary>
    public static string KeyOf(string relativePath)
    {
        string unix = relativePath.Replace('\\', '/');
        int dot = unix.LastIndexOf('.');
        return (dot > unix.LastIndexOf('/') ? unix[..dot] : unix).ToLowerInvariant();
    }

    /// <summary>Where the file under <paramref name="key"/> is read from, or null.</summary>
    public PackEntry? Find(string key) =>
        _byKey.TryGetValue(key, out var source)
            ? new PackEntry(source.Extension, source.Zip == null ? source.Path : null)
            : null;

    /// <summary>The bytes of the file under <paramref name="key"/>, or null.</summary>
    public byte[]? Read(string key)
    {
        if (!_byKey.TryGetValue(key, out var source))
        {
            return null;
        }

        // ⚠ Do not read unlocked: a ZipArchive is not safe from two threads, and the world archive
        // and the menus may both reach here.
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

    public void Dispose()
    {
        foreach (var zip in _zips)
        {
            zip.Dispose();
        }

        _zips.Clear();
        _byKey.Clear();
    }

    private static string DropTop(string entryPath)
    {
        int slash = entryPath.IndexOf('/');
        return slash < 0 ? entryPath : entryPath[(slash + 1)..];
    }

    private void AddZip(string source, IReadOnlySet<string> extensions, Func<PackFile, string?> keyOf)
    {
        try
        {
            var zip = ZipFile.OpenRead(source);
            _zips.Add(zip);
            var names = new List<string>();
            foreach (var entry in zip.Entries)
            {
                names.Add(entry.FullName);
            }

            names.Sort((a, b) => NameOrder(DropTop(a), DropTop(b)) is var c and not 0 ? c : NameOrder(a, b));
            foreach (string name in names)
            {
                Add(new PackFile(source, DropTop(name), true), zip, name, extensions, keyOf);
            }
        }
        catch (InvalidDataException e)
        {
            Log.Warn("world", $"HD pack is not a readable zip, skipped: {source} ({e.Message})");
        }
    }

    private void Add(PackFile file, ZipArchive? zip, string path, IReadOnlySet<string> extensions, Func<PackFile, string?> keyOf)
    {
        string ext = Path.GetExtension(file.RelativePath).ToLowerInvariant();
        if (!extensions.Contains(ext) || keyOf(file) is not { } key)
        {
            return;
        }

        _byKey[key] = (zip, path, ext);
    }
}
