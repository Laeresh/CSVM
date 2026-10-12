using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CSVM.Utils;

/// <summary>
/// Every shader text a suite hands to Godot, for the test harness. A headless process compiles none
/// of them. The report carries each suite's texts, so a windowed shard can be checked for every text
/// a headless one made. Collects only between <see cref="Begin"/> and
/// <see cref="Take"/>, so outside a test run a note costs one null check. Main thread only.
/// </summary>
public static class ShaderCensus
{
    private static HashSet<string>? _texts;

    /// <summary>Starts a suite's collection.</summary>
    public static void Begin() => _texts = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Notes a text given to a <c>Shader</c>, whether made or reused.</summary>
    public static void Note(string text) => _texts?.Add(text);

    /// <summary>The suite's texts as sorted 16-hex SHA-256 prefixes, and stops collecting.</summary>
    public static IReadOnlyList<string> Take()
    {
        var texts = _texts ?? new HashSet<string>();
        _texts = null;
        return texts.Select(t => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(t)))[..16].ToLowerInvariant())
            .OrderBy(h => h, StringComparer.Ordinal).ToList();
    }
}
