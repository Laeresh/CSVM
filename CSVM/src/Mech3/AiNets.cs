using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Godot;

namespace CSVM.Mech3;

/// <summary>One waypoint of a patrol net: a world position plus the raw per-node tag numbers.
/// Tags are undecoded — empty on most nodes, 2 or 4 values where present (the candidate
/// stop/valve markers; see docs/formats/ai-nets.md) — and are exposed verbatim, never
/// interpreted.</summary>
public readonly record struct AiNetNode(Vector3 Position, IReadOnlyList<float> Tags);

/// <summary>A net's trailer attach/follow target, raw and uninterpreted. The shipped shapes:
/// <c>[nodeIndex, "name"]</c> (76 nets, e.g. <c>[10, "player"]</c>), <c>[-1, "name"]</c> — a
/// named target with no attach node, <see cref="NodeIndex"/> −1 — (4), and index-only
/// <c>[3]</c> with a null <see cref="Name"/> (1, C2 net 33). A bare <c>[-1]</c> (133) or an
/// omitted trailer (8) is no trailer at all: <see cref="AiNet.Trailer"/> is null.</summary>
public readonly record struct AiNetTrailer(int NodeIndex, string? Name);

/// <summary>
/// Reads a chapter's patrol nets: every <c>ne0NNNNN.zrd.json</c> in the chapter zrdr scope
/// (dir or zip), joined with its <c>neindex.zrd.json</c> name. A net record is
/// <c>[null, 10.0, ×9 floats, NODES, EDGES, TRAILER?]</c> — 14 elements, 13 when the trailer
/// is omitted. Format page: docs/formats/ai-nets.md.
/// </summary>
public static class AiNets
{
    private const int NodesSlot = 11;
    private const int EdgesSlot = 12;
    private const int TrailerSlot = 13;

    /// <summary>Loads every patrol net of a chapter, sorted by id. An empty list when the
    /// chapter ships none (no such chapter in this install — all 8 carry 18–40).</summary>
    public static List<AiNet> Load(string chapterZrdrPath)
    {
        var names = LoadIndex(chapterZrdrPath);
        var nets = new List<AiNet>();
        foreach (var (fileName, root) in Zrdr.LoadFilesNamed(chapterZrdrPath, IsNetFileName))
        {
            if (root.Count == 0 || root[0] is not List<object?> record)
                throw new InvalidDataException($"'{fileName}': not a one-record net reader");
            int id = IdOf(fileName);
            nets.Add(ParseRecord(fileName, id, names.GetValueOrDefault(id, ""), record));
        }
        nets.Sort((a, b) => a.Id.CompareTo(b.Id));
        return nets;
    }

    /// <summary>The chapter's id → name map from <c>neindex.zrd.json</c>. Order is lost here;
    /// <see cref="LoadIndexPairs"/> is the ordered read and the one the engine's own table
    /// follows. Empty when the chapter has no index.</summary>
    public static Dictionary<int, string> LoadIndex(string chapterZrdrPath)
    {
        var names = new Dictionary<int, string>();
        foreach (var (id, name) in LoadIndexPairs(chapterZrdrPath))
            names[id] = name;
        return names;
    }

    /// <summary>The chapter's id → name pairs from <c>neindex.zrd.json</c>
    /// (<c>[[first, id0, "Name0", id1, "Name1", …]]</c>), in FILE ORDER. The first element is
    /// NOT the pair count. It is an allocation figure ≥ the count (C1: 46 against 29 pairs) and
    /// is skipped.
    ///
    /// <para>⚠ File order is not ascending id, and the difference is load-bearing: the engine
    /// builds its net table by walking this record forward (<c>FUN_004311c0</c>), and every
    /// consumer that says "the first net" means entry 0 of that table. C1B opens on id 29 and
    /// C1C on id 25, both against a lowest id of 11, so reading <see cref="Load"/>'s
    /// sorted-by-id list instead picks the wrong graph on two of the eight chapters.</para></summary>
    public static List<(int Id, string Name)> LoadIndexPairs(string chapterZrdrPath)
    {
        var pairs = new List<(int, string)>();
        List<object?> root;
        try
        {
            root = Zrdr.LoadFile(chapterZrdrPath, "neindex.json");
        }
        catch (IOException)
        {
            return pairs;
        }
        if (root.Count == 0 || root[0] is not List<object?> list)
            return pairs;
        for (int i = 1; i + 1 < list.Count; i += 2)
        {
            if (list[i] is float id && list[i + 1] is string name)
                pairs.Add(((int)id, name));
        }
        return pairs;
    }

    /// <summary>The chapter's FIRST net in index order (<see cref="LoadIndexPairs"/>), or null
    /// when the chapter has no index or its first id has no net file. This is the net every
    /// Instant Action actor is handed: <c>FUN_0045a390</c> writes a one-entry <c>netids</c> list
    /// holding entry 0 of the engine's table in all three of its branches (the wingmen, the ace
    /// and the waves alike; docs/formats/instant-action.md).</summary>
    public static AiNet? ChapterFirst(IReadOnlyList<AiNet> nets, string chapterZrdrPath)
    {
        var pairs = LoadIndexPairs(chapterZrdrPath);
        return pairs.Count == 0 ? null : ById(nets, pairs[0].Id);
    }

    /// <summary>The net with this id, or null. Ids are how <c>aiv</c> field 0 references nets
    /// (−1 = none).</summary>
    public static AiNet? ById(IReadOnlyList<AiNet> nets, int id)
    {
        foreach (var net in nets)
        {
            if (net.Id == id)
                return net;
        }
        return null;
    }

    /// <summary>The net with this neindex name (case-insensitive), or null. Names are how egen
    /// (<c>vehicle.nets</c>), zeppelins (<c>net</c>) and objectives reference nets.</summary>
    public static AiNet? ByName(IReadOnlyList<AiNet> nets, string name)
    {
        foreach (var net in nets)
        {
            if (net.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return net;
        }
        return null;
    }

    /// <summary>Resolves a reference in either shipped spelling: all digits reads as an id,
    /// anything else as a name.</summary>
    public static AiNet? Resolve(IReadOnlyList<AiNet> nets, string idOrName) =>
        int.TryParse(idOrName, NumberStyles.None, CultureInfo.InvariantCulture, out int id)
            ? ById(nets, id)
            : ByName(nets, idOrName);

    // ne000010.zrd.json / ne000010.json (fork vs v0.6.1 naming, same rule as
    // Zrdr.CandidateNames).
    private static bool IsNetFileName(string name)
    {
        if (!name.StartsWith("ne0", StringComparison.OrdinalIgnoreCase))
            return false;
        var stem = StripJsonSuffix(name);
        if (stem.Length <= 2)
            return false;
        foreach (char c in stem[2..])
        {
            if (!char.IsAsciiDigit(c))
                return false;
        }
        return true;
    }

    private static string StripJsonSuffix(string name)
    {
        foreach (var suffix in new[] { ".zrd.json", ".json" })
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return name[..^suffix.Length];
        }
        return name;
    }

    private static int IdOf(string fileName) =>
        int.Parse(StripJsonSuffix(fileName)[2..], CultureInfo.InvariantCulture);

    private static AiNet ParseRecord(string fileName, int id, string name, List<object?> record)
    {
        if (record.Count <= EdgesSlot)
            throw new InvalidDataException($"'{fileName}': {record.Count} elements, expected 13 or 14");
        if (record[NodesSlot] is not List<object?> rawNodes || record[EdgesSlot] is not List<object?> rawEdges)
            throw new InvalidDataException($"'{fileName}': slots {NodesSlot}/{EdgesSlot} are not the NODES/EDGES lists");

        var nodes = new List<AiNetNode>(rawNodes.Count);
        foreach (var entry in rawNodes)
        {
            if (entry is not List<object?> n || n.Count < 3
                || n[0] is not float x || n[1] is not float y || n[2] is not float z)
                throw new InvalidDataException($"'{fileName}': node {nodes.Count} is not [x,y,z,…]");
            var tags = Array.Empty<float>();
            if (n.Count > 3)
            {
                tags = new float[n.Count - 3];
                for (int i = 3; i < n.Count; i++)
                    tags[i - 3] = n[i] is float f ? f : throw new InvalidDataException(
                        $"'{fileName}': node {nodes.Count} tag {i - 3} is not a number");
            }
            nodes.Add(new AiNetNode(new Vector3(x, y, z), tags));
        }

        var edges = new List<(int A, int B)>(rawEdges.Count);
        foreach (var entry in rawEdges)
        {
            if (entry is not List<object?> e || e.Count != 2
                || e[0] is not float a || e[1] is not float b)
                throw new InvalidDataException($"'{fileName}': edge {edges.Count} is not [i,j]");
            edges.Add(((int)a, (int)b));
        }

        AiNetTrailer? trailer = null;
        if (record.Count > TrailerSlot
            && record[TrailerSlot] is List<object?> { Count: >= 1 } t
            && t[0] is float nodeIndex)
        {
            string? target = t.Count >= 2 ? t[1] as string : null;
            if (nodeIndex >= 0f || target != null)
                trailer = new AiNetTrailer((int)nodeIndex, target);
        }

        return new AiNet { Id = id, Name = name, Nodes = nodes, Edges = edges, Trailer = trailer };
    }
}

/// <summary>
/// One chapter patrol net (<c>ne0NNNNN.zrd.json</c>): a waypoint GRAPH — positions plus an
/// explicit edge list that branches and is not necessarily closed. Never assume the node order
/// is the route; only <see cref="Edges"/> is connectivity. Referenced by name from egen
/// (<c>vehicle.nets</c>), zeppelins (<c>net</c>) and objectives, and by <see cref="Id"/> from
/// aiv field 0.
/// </summary>
public sealed class AiNet
{
    /// <summary>The net id — encoded in the filename (<c>ne000010</c> → 10) and the number aiv
    /// field 0 references.</summary>
    public required int Id { get; init; }

    /// <summary>The name the chapter's <c>neindex.zrd.json</c> gives this id — the join key
    /// egen/zeppelins/objectives use. Empty when the index misses the id (unseen in this
    /// install: all 222 nets resolve 1:1).</summary>
    public required string Name { get; init; }

    public required IReadOnlyList<AiNetNode> Nodes { get; init; }

    /// <summary>Node-index pairs. The connectivity of the net — branching is normal, and a
    /// closed loop is one authoring choice, not the rule.</summary>
    public required IReadOnlyList<(int A, int B)> Edges { get; init; }

    /// <summary>The trailer attach/follow target — see <see cref="AiNetTrailer"/> for the
    /// shipped shapes. Null when the record ends with a bare <c>[-1]</c> or omits the trailer
    /// entirely.</summary>
    public AiNetTrailer? Trailer { get; init; }
}
