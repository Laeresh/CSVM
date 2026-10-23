using System;
using System.Collections.Generic;
using CSVM.Utils;

namespace CSVM.Extraction;

/// <summary>One replacement model: its bytes and whether they are a binary <c>.glb</c>. A loose
/// <c>.gltf</c> also carries its path, so the importer can resolve the files beside it.</summary>
public readonly record struct HdModel(byte[] Bytes, bool IsGlb, string? GltfPath);

/// <summary>One airframe's <c>rig.json</c> from a model pack: its bytes, and the label every
/// validation message names (the file's path, or the zip and its entry).</summary>
public readonly record struct HdRig(byte[] Bytes, string Source);

/// <summary>
/// Replacement meshes for gamez nodes, read through a <see cref="PackIndex"/> from the model folder
/// (<c>models.hd</c>, <c>--hd-models=</c>, default <c>HDModels</c>). A <c>.glb</c> is read from zips
/// and subfolders, a <c>.gltf</c> from subfolders only. Below each pack's top folder a key names one
/// chapter (<c>C3/he_rocket</c>), one airframe (<c>planes/player_bhawk/prop1</c>) or all
/// (<c>he_rocket</c>). The more specific key wins, an airframe is its model node, and its
/// <c>rig.json</c> is read (<see cref="RigFor"/>), as is a clump tile (<see cref="Clump"/>).
/// A node is its original gamez name, matched without case. A trailing <c>.flt</c> is dropped from
/// the node name and the file name alike, so <c>he_rocket.glb</c> and <c>he_rocket.flt.glb</c> each
/// replace <c>he_rocket</c> and <c>he_rocket.flt</c>, engine-free.
/// </summary>
public sealed class HdModels : IDisposable
{
    /// <summary>A clump tile's image, its sidecar and its optional side map, in that order.</summary>
    public static readonly IReadOnlyList<string> ClumpSuffixes = new[] { ".clump.png", ".clump.json", ".clump.side.png" };

    private const string Flt = ".flt";
    private const string Json = ".json";
    private const string Planes = "planes";
    private const string RigName = "rig";

    private static readonly IReadOnlySet<string> Extensions = new HashSet<string> { ".glb", ".gltf", Json, ".png" };

    private readonly PackIndex _index;

    private HdModels(PackIndex index)
    {
        _index = index;
        foreach (string key in index.Keys)
        {
            if (IsClumpKey(key))
            {
                ClumpCount++;
            }
            else if (key.EndsWith(Json, StringComparison.Ordinal))
            {
                RigCount++;
            }
            else
            {
                Count++;
            }
        }
    }

    /// <summary>The replacements every builder consults, settled once at launch.</summary>
    public static HdModels Current { get; set; } = new(PackIndex.Empty());

    /// <summary>How many replacement models the folder offers.</summary>
    public int Count { get; }

    /// <summary>How many airframe rigs the folder offers.</summary>
    public int RigCount { get; }

    /// <summary>How many clump tile files (images, sidecars and side maps) the folder offers.</summary>
    public int ClumpCount { get; }

    /// <summary>The replacements under <paramref name="folder"/>, relative to the data root unless
    /// rooted; empty when the folder is absent. Packs load in name order and a later one wins.</summary>
    public static HdModels Open(string dataRoot, string folder)
    {
        var models = new HdModels(PackIndex.Open(dataRoot, folder, Extensions, KeyOfFile));
        if (models._index.Root is { } root)
        {
            Log.Info("world", $"HD models: {models.Count} replacements, {models.RigCount} rigs, {models.ClumpCount} clump tile files from {root}");
        }

        return models;
    }

    /// <summary>The replacement for the gamez node named <paramref name="node"/>, or null. The
    /// airframe's own key beats the chapter's, which beats the bare name. Pass the chapter folder
    /// (<c>C3</c>) for a chapter's gamez and the airframe for the planes gamez.</summary>
    public HdModel? For(string node, string? chapter = null, string? airframe = null) =>
        KeyFor(node, chapter, airframe) is { } key ? Read(key) : null;

    /// <summary>The pack key <see cref="For"/> would read for the node, or null, without reading the
    /// file. Every node resolving to one key gets the same replacement.</summary>
    public string? KeyFor(string node, string? chapter = null, string? airframe = null)
    {
        string name = NodeKey(node);
        if (_index.Count == 0 || node.Length == 0 || IsClumpKey(name))
        {
            return null;
        }

        return (airframe is { Length: > 0 } ? Known("planes/" + airframe.ToLowerInvariant() + "/" + name) : null)
            ?? (chapter is { Length: > 0 } ? Known(chapter.ToLowerInvariant() + "/" + name) : null)
            ?? Known(name);
    }

    /// <summary>The replacement under the pack key <paramref name="key"/>, or null.</summary>
    public HdModel? Read(string key) =>
        _index.Find(key) is { } entry && _index.Read(key) is { } bytes
            ? new HdModel(bytes, entry.Extension == ".glb", entry.Extension == ".gltf" ? entry.LoosePath : null)
            : null;

    /// <summary>The <c>rig.json</c> of the airframe whose model node is <paramref name="airframe"/>
    /// (<c>player_bhawk</c>), matched without case, or null. A rig needs no replacement model beside it.</summary>
    public HdRig? RigFor(string airframe)
    {
        if (RigCount == 0 || airframe.Length == 0)
        {
            return null;
        }

        string key = RigKey(airframe.ToLowerInvariant());
        return _index.Read(key) is { } bytes ? new HdRig(bytes, _index.Origin(key)!) : null;
    }

    /// <summary>The clump tile file under <paramref name="key"/> (<c>C1/firtree1.clump.png</c>, matched
    /// without case) with the label a message names it by, or null.</summary>
    public (byte[] Bytes, string Source)? Clump(string key)
    {
        key = key.Replace('\\', '/').ToLowerInvariant();
        return IsClumpKey(key) && _index.Read(key) is { } bytes ? (bytes, _index.Origin(key)!) : null;
    }

    public void Dispose() => _index.Dispose();

    /// <summary>Whether a key names a clump tile file. Such a key keeps its extension, as a rig's does.</summary>
    internal static bool IsClumpKey(string key)
    {
        foreach (string suffix in ClumpSuffixes)
        {
            if (key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The key part a gamez node name matches: lower case, a trailing <c>.flt</c> dropped,
    /// any other dot kept.</summary>
    internal static string NodeKey(string node) => DropFlt(node.ToLowerInvariant());

    // A .gltf names its buffers by relative path, which a zip entry has no folder to resolve against.
    // A .json is read only as planes/<airframe>/rig.json, keyed with its extension so no model collides.
    // A clump file keeps its whole path as its key, so no node name reaches it; any other .png is refused.
    private static string? KeyOfFile(PackFile file)
    {
        if (IsClumpKey(file.RelativePath))
        {
            return file.RelativePath.Replace('\\', '/').ToLowerInvariant();
        }

        if (file.RelativePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (file.RelativePath.EndsWith(Json, StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = PackIndex.KeyOf(file.RelativePath).Split('/');
            return parts.Length == 3 && parts[0] == Planes && parts[1].Length > 0 && parts[2] == RigName
                ? RigKey(parts[1])
                : null;
        }

        if (file.InZip && file.RelativePath.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))
        {
            Log.Warn("world", $"HD model pack holds a .gltf in a zip, skipped (pack it as .glb or unzip it): {file.Pack} {file.RelativePath}");
            return null;
        }

        return DropFlt(PackIndex.KeyOf(file.RelativePath));
    }

    private static string RigKey(string airframe) => $"{Planes}/{airframe}/{RigName}{Json}";

    private static string DropFlt(string key) =>
        key.EndsWith(Flt, StringComparison.Ordinal) ? key[..^Flt.Length] : key;

    private string? Known(string key) => _index.Find(key) != null ? key : null;
}
