using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CSVM.Testing;

/// <summary>Which shard of how many a run is, as <c>--run-tests=shard:&lt;index&gt;/&lt;count&gt;</c>
/// spells it. <see cref="Index"/> is 1-based so the flag reads the way a human counts.</summary>
public readonly record struct ShardSpec(int Index, int Count);

/// <summary>Deterministic weighted division of a selected suite set into shards, and the
/// <c>shard:</c> term that asks for one. Pure over its inputs (no Godot, no clock, no file access
/// outside <see cref="Load"/>), so a plan can be proved outside the engine.</summary>
public static class SuiteShards
{
    private const string ShardPrefix = "shard:";

    /// <summary>Pulls the one <c>shard:i/n</c> term out of a <c>--run-tests=</c> value, leaving the
    /// selector terms in <paramref name="remainder"/>. Returns null with a null
    /// <paramref name="error"/> when the value carries no shard term at all; a malformed or
    /// out-of-range one sets <paramref name="error"/> rather than quietly running everything.</summary>
    public static ShardSpec? Parse(string spec, out string remainder, out string? error)
    {
        error = null;
        var kept = new List<string>();
        ShardSpec? found = null;
        foreach (string raw in spec.Split(','))
        {
            string term = raw.Trim();
            if (term.Length == 0)
            {
                continue;
            }
            if (!term.StartsWith(ShardPrefix, StringComparison.OrdinalIgnoreCase))
            {
                kept.Add(term);
                continue;
            }
            if (found != null)
            {
                error = $"more than one shard term in '{spec}'";
            }
            var parts = term[ShardPrefix.Length..].Split('/');
            if (parts.Length != 2
                || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
                || count < 1 || index < 1 || index > count)
            {
                error ??= $"malformed shard term '{term}': expected shard:<index>/<count>, 1 <= index <= count";
                remainder = string.Join(",", kept);
                return null;
            }
            found = new ShardSpec(index, count);
        }
        remainder = string.Join(",", kept);
        return error == null ? found : null;
    }

    /// <summary>Divides <paramref name="items"/> into <paramref name="shardCount"/> shards, longest
    /// unit first onto the lightest shard so far. Deterministic: ties break on the item's own
    /// position, so the same inputs always produce the same plan. Each shard keeps the input's
    /// order, which for a suite list is registry order.</summary>
    public static IReadOnlyList<IReadOnlyList<T>> Plan<T>(IReadOnlyList<T> items,
        Func<T, string> name, SuiteWeights weights, int shardCount)
    {
        if (shardCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(shardCount));
        }
        var shards = new List<T>[shardCount];
        var load = new double[shardCount];
        for (int i = 0; i < shardCount; i++)
        {
            shards[i] = new List<T>();
        }
        var positions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < items.Count; i++)
        {
            positions[name(items[i])] = i;
        }

        // One unit per suite, except that a group's present members become a single unit: the
        // balancer may not split a set the weights file says must share a process.
        var unitOf = GroupKeys(items, name, weights, positions);
        var units = items
            .GroupBy(item => unitOf[name(item)])
            .Select(g => new
            {
                Items = g.ToList(),
                Weight = g.Sum(item => weights.For(name(item))),
                First = g.Min(item => positions[name(item)]),
            })
            .OrderByDescending(u => u.Weight)
            .ThenBy(u => u.First)
            .ToList();

        foreach (var unit in units)
        {
            int target = 0;
            for (int i = 1; i < shardCount; i++)
            {
                if (load[i] < load[target])
                {
                    target = i;
                }
            }
            shards[target].AddRange(unit.Items);
            load[target] += unit.Weight;
        }
        foreach (var shard in shards)
        {
            shard.Sort((a, b) => positions[name(a)].CompareTo(positions[name(b)]));
        }
        return shards;
    }

    /// <summary>The names <paramref name="weights"/> carries no timing for. Reported on the run so a
    /// suite added without re-measuring shows up as an unweighted default rather than skewing the
    /// balance invisibly.</summary>
    public static IReadOnlyList<string> Unweighted(IEnumerable<string> names, SuiteWeights weights) =>
        names.Where(n => !weights.Seconds.ContainsKey(n)).ToList();

    /// <summary>Reads the weights file. A missing or unreadable file yields
    /// <see cref="SuiteWeights.Empty"/> (every suite equally weighted), because a balance that is
    /// merely even is still a correct division of the catalog.</summary>
    public static SuiteWeights Load(string path)
    {
        if (!File.Exists(path))
        {
            return SuiteWeights.Empty;
        }
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            var seconds = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("suites", out var suites))
            {
                foreach (var entry in suites.EnumerateObject())
                {
                    seconds[entry.Name] = entry.Value.GetDouble();
                }
            }
            var groups = new List<IReadOnlyList<string>>();
            if (root.TryGetProperty("groups", out var groupList))
            {
                foreach (var group in groupList.EnumerateArray())
                {
                    groups.Add(group.EnumerateArray().Select(e => e.GetString() ?? "").ToList());
                }
            }
            return new SuiteWeights
            {
                DefaultSeconds = root.TryGetProperty("defaultSeconds", out var d) ? d.GetDouble() : 1.0,
                Seconds = seconds,
                Groups = groups,
                Source = path,
            };
        }
        catch (Exception e) when (e is IOException or JsonException or FormatException)
        {
            return SuiteWeights.Empty;
        }
    }

    private static Dictionary<string, string> GroupKeys<T>(IReadOnlyList<T> items, Func<T, string> name,
        SuiteWeights weights, Dictionary<string, int> positions)
    {
        var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            keys[name(item)] = name(item);
        }
        foreach (var group in weights.Groups)
        {
            var present = group.Where(positions.ContainsKey).OrderBy(n => positions[n]).ToList();
            if (present.Count < 2)
            {
                continue;
            }
            foreach (string member in present)
            {
                keys[member] = present[0];
            }
        }
        return keys;
    }
}

/// <summary>The checked-in per-suite timings the balancer divides by, plus the suites that must
/// stay together in one shard. Read from <c>analysis/engine-suite-weights.json</c>, which is
/// written from a warm full-catalog <c>test-report.json</c>; a suite the file does not name is
/// charged <see cref="DefaultSeconds"/> and reported, never silently treated as free.</summary>
public sealed class SuiteWeights
{
    public static readonly SuiteWeights Empty = new()
    {
        DefaultSeconds = 1.0,
        Seconds = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
        Groups = Array.Empty<IReadOnlyList<string>>(),
    };

    public required double DefaultSeconds { get; init; }

    public required IReadOnlyDictionary<string, double> Seconds { get; init; }

    /// <summary>Sets of suite names that must land in the same shard, whatever the balance costs.
    /// The two seven-world censuses are one such set: whichever runs second reads its chapters out
    /// of the process-scoped <c>DecodeCache</c> instead of decoding them again.</summary>
    public required IReadOnlyList<IReadOnlyList<string>> Groups { get; init; }

    public string Source { get; init; } = "";

    public double For(string name) => Seconds.TryGetValue(name, out double s) ? s : DefaultSeconds;
}
