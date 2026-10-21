using System;
using System.Collections.Generic;
using CSVM.Utils;

namespace CSVM.Extraction;

/// <summary>One replacement model: its bytes, whether they are a binary <c>.glb</c>, and for a loose
/// <c>.gltf</c> its path on disk, so the importer can resolve the buffers and images beside it.</summary>
public readonly record struct HdModel(byte[] Bytes, bool IsGlb, string? GltfPath);

/// <summary>
/// Replacement meshes for gamez nodes, read through a <see cref="PackIndex"/> from the model folder
/// (<c>models.hd</c>, <c>--hd-models=</c>, default <c>HDModels</c>). A <c>.glb</c> is read from zips
/// and subfolders, a <c>.gltf</c> from subfolders only. After each pack's top folder, a key is
/// <c>C3/he_rocket</c> for one chapter, <c>planes/bhawk/prop1</c> for one airframe, or a bare
/// <c>he_rocket</c> everywhere. The more specific key wins.
/// A node is its original gamez name, matched without case. A trailing <c>.flt</c> is dropped from
/// the node name and the file name alike, so <c>he_rocket.glb</c> and <c>he_rocket.flt.glb</c> each
/// replace <c>he_rocket</c> and <c>he_rocket.flt</c>. Engine-free.
/// </summary>
public sealed class HdModels : IDisposable
{
    private const string Flt = ".flt";

    private static readonly IReadOnlySet<string> Extensions = new HashSet<string> { ".glb", ".gltf" };

    private readonly PackIndex _index;

    private HdModels(PackIndex index)
    {
        _index = index;
    }

    /// <summary>The replacements every builder consults, settled once at launch.</summary>
    public static HdModels Current { get; set; } = new(PackIndex.Empty());

    /// <summary>How many replacements the folder offers.</summary>
    public int Count => _index.Count;

    /// <summary>The replacements under <paramref name="folder"/>, relative to the data root unless
    /// rooted; empty when the folder is absent. Packs load in name order and a later one wins.</summary>
    public static HdModels Open(string dataRoot, string folder)
    {
        var index = PackIndex.Open(dataRoot, folder, Extensions, KeyFor);
        if (index.Root is { } root)
        {
            Log.Info("world", $"HD models: {index.Count} replacements from {root}");
        }

        return new HdModels(index);
    }

    /// <summary>The replacement for the gamez node named <paramref name="node"/>, or null. The
    /// airframe's own key beats the chapter's, which beats the bare name. Pass the chapter folder
    /// (<c>C3</c>) for a chapter's gamez and the airframe for the planes gamez.</summary>
    public HdModel? For(string node, string? chapter = null, string? airframe = null)
    {
        if (_index.Count == 0 || node.Length == 0)
        {
            return null;
        }

        string name = NodeKey(node);
        return (airframe is { Length: > 0 } ? Read("planes/" + airframe.ToLowerInvariant() + "/" + name) : null)
            ?? (chapter is { Length: > 0 } ? Read(chapter.ToLowerInvariant() + "/" + name) : null)
            ?? Read(name);
    }

    public void Dispose() => _index.Dispose();

    /// <summary>The key part a gamez node name matches: lower case, a trailing <c>.flt</c> dropped,
    /// any other dot kept.</summary>
    internal static string NodeKey(string node) => DropFlt(node.ToLowerInvariant());

    // A .gltf names its buffers by relative path, which a zip entry has no folder to resolve against.
    private static string? KeyFor(PackFile file)
    {
        if (file.InZip && file.RelativePath.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))
        {
            Log.Warn("world", $"HD model pack holds a .gltf in a zip, skipped (pack it as .glb or unzip it): {file.Pack} {file.RelativePath}");
            return null;
        }

        return DropFlt(PackIndex.KeyOf(file.RelativePath));
    }

    private static string DropFlt(string key) =>
        key.EndsWith(Flt, StringComparison.Ordinal) ? key[..^Flt.Length] : key;

    private HdModel? Read(string key) =>
        _index.Find(key) is { } entry && _index.Read(key) is { } bytes
            ? new HdModel(bytes, entry.Extension == ".glb", entry.Extension == ".gltf" ? entry.LoosePath : null)
            : null;
}
