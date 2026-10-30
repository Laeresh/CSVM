using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Extraction;

namespace CSVM.Mech3;

/// <summary>
/// One zrdr scope, the root's, a chapter's or a mission's, as its reader files: what every engine
/// consumer reads in place of an archive path. Built from a <c>zrdr.zbd</c> (<see cref="Load"/>),
/// in code for a synthetic run, or from an unzbd zip or folder (<see cref="FromExtracted"/>, the
/// modding path). The five reads keep the contracts the path entry points of <see cref="Zrdr"/>
/// have. Each hands out a fresh copy of the stored lists, so a scope one session reads never
/// carries another session's edits. The lookup rule is in docs/formats/zrdr.md.
/// </summary>
public sealed class ZrdrSet
{
    /// <summary>No archive: every read finds nothing, and <see cref="LoadFile"/> throws.</summary>
    public static readonly ZrdrSet None = new("no zrdr archive", Array.Empty<ZrdrFile>(), exists: false);

    /// <summary>The scope holding <paramref name="files"/>, labelled <paramref name="archive"/>
    /// (<c>ZBD/C1/zrdr.zbd</c>) in messages.</summary>
    public ZrdrSet(string archive, IReadOnlyList<ZrdrFile> files)
        : this(archive, files, exists: true)
    {
    }

    private ZrdrSet(string archive, IReadOnlyList<ZrdrFile> files, bool exists)
    {
        Archive = archive;
        Files = files;
        Exists = exists;
    }

    /// <summary>Where the scope came from, for messages.</summary>
    public string Archive { get; }

    /// <summary>The reader files in table order. Read-only by contract: a consumer reads through the
    /// copying members.</summary>
    public IReadOnlyList<ZrdrFile> Files { get; }

    /// <summary>Whether there is an archive at all. A scope that exists may still hold no
    /// readers, as the synthetic chapter does.</summary>
    public bool Exists { get; }

    /// <summary>The scope of the archive at <paramref name="path"/> (<c>ZBD/C1/M02/zrdr.zbd</c>),
    /// <see cref="None"/> without a source or without the file.</summary>
    public static ZrdrSet Load(IGameFileSource? game, string path) =>
        ZrdrZbd.Load(game, path) is { } files ? new ZrdrSet(path, files) : None;

    /// <summary>The scope of an unzbd zip or its unpacked folder, read through <see cref="Zrdr"/>;
    /// <see cref="None"/> when neither exists.</summary>
    public static ZrdrSet FromExtracted(string path) =>
        Zrdr.ReadScope(path) is { } files ? new ZrdrSet(path, files) : None;

    /// <summary>Whether the scope holds <paramref name="fileName"/> under any spelling
    /// <see cref="ZrdrZbd.Find"/> accepts.</summary>
    public bool HasFile(string fileName) => ZrdrZbd.Find(Files, fileName) != null;

    /// <summary>One reader's root list. Throws <see cref="FileNotFoundException"/> when the file is
    /// absent and <see cref="InvalidDataException"/> when the reader is empty.</summary>
    public List<object?> LoadFile(string fileName)
    {
        var root = Stored(fileName);
        return root.Count > 0 ? Copy(root) : throw new InvalidDataException($"'{fileName}' is not a reader list");
    }

    /// <summary>One reader's root list, empty for an empty reader. Throws
    /// <see cref="FileNotFoundException"/> when the file is absent.</summary>
    public List<object?> LoadFileOrEmpty(string fileName) => Copy(Stored(fileName));

    /// <summary>Every non-empty reader holding a string that contains
    /// <paramref name="contentFilter"/>, as (<c>vehicle.zrd.json</c>, root list). An empty filter
    /// takes every non-empty reader.</summary>
    public IEnumerable<(string Name, List<object?> Root)> LoadMatchingFiles(string contentFilter)
    {
        foreach (var file in Files)
        {
            if (file.Root.Count > 0 && (contentFilter.Length == 0 || Holds(file.Root, contentFilter)))
            {
                yield return (file.Key + ".json", Copy(file.Root));
            }
        }
    }

    /// <summary>Every non-empty reader whose name (<c>vehicle.zrd.json</c>) passes
    /// <paramref name="namePredicate"/>, as (name, root list).</summary>
    public IEnumerable<(string Name, List<object?> Root)> LoadFilesNamed(Func<string, bool> namePredicate)
    {
        foreach (var file in Files)
        {
            string name = file.Key + ".json";
            if (file.Root.Count > 0 && namePredicate(name))
            {
                yield return (name, Copy(file.Root));
            }
        }
    }

    /// <inheritdoc/>
    public override string ToString() => Archive;

    private static List<object?> Copy(List<object?> list)
    {
        var copy = new List<object?>(list.Count);
        foreach (var value in list)
        {
            copy.Add(value is List<object?> nested ? Copy(nested) : value);
        }

        return copy;
    }

    // The walk stands in for the extraction's raw-text sniff: a string anywhere in the tree.
    private static bool Holds(List<object?> list, string filter)
    {
        foreach (var value in list)
        {
            if ((value is string s && s.Contains(filter, StringComparison.Ordinal))
                || (value is List<object?> nested && Holds(nested, filter)))
            {
                return true;
            }
        }

        return false;
    }

    private List<object?> Stored(string fileName) =>
        ZrdrZbd.Find(Files, fileName)?.Root ?? throw new FileNotFoundException($"'{fileName}' missing from '{Archive}'");
}
