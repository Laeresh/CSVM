using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Utils;

namespace CSVM.Extraction;

/// <summary>
/// Higher-resolution replacements for the extracted art, read through a <see cref="PackIndex"/>
/// from the HD folder (<c>textures.hd</c>, <c>--hd=</c>, default <c>HD</c>).
/// Each pack's own top folder is dropped. A path left with a folder replaces that file under
/// <c>extracted/</c>, matched without case or extension: <c>rimage/c1m1.png</c>,
/// <c>C3/rtexture12/cliff01.png</c>. A bare name replaces that world texture in every archive.
/// Engine-free; the callers decode and size what this returns.
/// </summary>
public sealed class HdTextures : IDisposable
{
    private static readonly IReadOnlySet<string> Extensions = new HashSet<string> { ".png", ".jpg", ".jpeg" };

    private readonly PackIndex _index;
    private readonly string _extracted;

    private HdTextures(PackIndex index, string extracted)
    {
        _index = index;
        _extracted = extracted;
    }

    /// <summary>The replacements every loader consults, settled once at launch.</summary>
    public static HdTextures Current { get; set; } = new(PackIndex.Empty(), "");

    /// <summary>How many replacements the folder offers.</summary>
    public int Count => _index.Count;

    /// <summary>The replacements under <paramref name="folder"/>, relative to the data root unless
    /// rooted; empty when the folder is absent. Sources load in name order and a later one wins, so
    /// a small fix pack named after the main one overrides it.</summary>
    public static HdTextures Open(string dataRoot, string folder)
    {
        var index = PackIndex.Open(dataRoot, folder, Extensions, file => PackIndex.KeyOf(file.RelativePath));
        var hd = new HdTextures(index, Path.Combine(dataRoot, ExtractionRun.ExtractedFolder));
        if (index.Root is { } root)
        {
            Log.Info("world", $"HD textures: {hd.Count} replacements from {root}");
        }

        return hd;
    }

    /// <summary>The replacement for one texture of the archive at <paramref name="archivePath"/>
    /// (its folder or <c>.zip</c>), by its archive name, or null. The archive's own entry beats the
    /// bare name, which is how a pack carries a chapter's own version of a shared name.
    /// ⚠ Never gate a bare name on its resemblance to the original. The thumbnail distances of
    /// correct redraws and of another chapter's same-named texture overlap at every size.</summary>
    public byte[]? ForWorld(string archivePath, string archiveBaseName)
    {
        if (_index.Count == 0)
        {
            return null;
        }

        string name = archiveBaseName.ToLowerInvariant();
        return (Relative(archivePath) is { } archive ? _index.Read(PackIndex.KeyOf(archive) + "/" + name) : null) ?? _index.Read(name);
    }

    /// <summary>The replacement for the file at <paramref name="originalPath"/> under
    /// <c>extracted/</c>, or null.</summary>
    public byte[]? ForExtracted(string originalPath) =>
        _index.Count > 0 && Relative(originalPath) is { } rel ? _index.Read(PackIndex.KeyOf(rel)) : null;

    public void Dispose() => _index.Dispose();

    private string? Relative(string path)
    {
        string rel = Path.GetRelativePath(_extracted, path);
        return rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel) ? null : rel;
    }
}
