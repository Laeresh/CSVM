using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CSVM.Testing;

/// <summary>
/// Which kind of process runs a suite in the local battery, and the <c>render:</c> term that narrows
/// a run to some kinds. A suite <c>analysis/headless-limits.json</c> names runs windowed: it needs a
/// renderer, or it is one of the suites whose shaders a windowed process must compile. A
/// <see cref="SuiteCatalog.CiTier"/> suite runs synthetic, and every other suite runs headless.
/// Pure over its inputs outside <see cref="Load"/>.
/// </summary>
public static class SuiteRender
{
    public const string Windowed = "windowed";
    public const string Headless = "headless";
    public const string Synthetic = "synthetic";

    /// <summary>The maps of <c>headless-limits.json</c> whose suites run windowed.</summary>
    internal static readonly string[] WindowedMaps = { "headlessOnly", "headlessDiffers", "headlessShaders" };

    private const string Prefix = "render:";
    private static readonly string[] Kinds = { Windowed, Headless, Synthetic };

    /// <summary>Pulls the one <c>render:&lt;kind&gt;[+&lt;kind&gt;]</c> term out of a
    /// <c>--run-tests=</c> value, leaving the other terms in <paramref name="remainder"/>. Null with
    /// a null <paramref name="error"/> when there is none.</summary>
    public static IReadOnlySet<string>? Parse(string spec, out string remainder, out string? error)
    {
        error = null;
        var kept = new List<string>();
        HashSet<string>? found = null;
        foreach (string raw in spec.Split(','))
        {
            string term = raw.Trim();
            if (!term.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                if (term.Length > 0)
                {
                    kept.Add(term);
                }
                continue;
            }
            if (found != null)
            {
                error = $"more than one render term in '{spec}'";
            }
            found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string kind in term[Prefix.Length..].Split('+'))
            {
                if (!Kinds.Contains(kind, StringComparer.OrdinalIgnoreCase))
                {
                    error ??= $"malformed render term '{term}': expected render:<kind>[+<kind>] of {string.Join(", ", Kinds)}";
                }
                found.Add(kind);
            }
        }
        remainder = string.Join(",", kept);
        return error == null ? found : null;
    }

    /// <summary>The kind of process that runs <paramref name="name"/>.</summary>
    public static string KindOf(string name, HeadlessLimits limits) =>
        limits.Windowed.Contains(name) ? Windowed
        : SuiteCatalog.CiTier.Contains(name, StringComparer.OrdinalIgnoreCase) ? Synthetic
        : Headless;

    /// <summary>Reads the file, or null when it is missing or unreadable. Without it no suite can be
    /// placed, and a headless line cannot be told from a new error.</summary>
    public static HeadlessLimits? Load(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            var windowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string map in WindowedMaps)
            {
                if (root.TryGetProperty(map, out var entries))
                {
                    windowed.UnionWith(entries.EnumerateObject().Select(e => e.Name));
                }
            }
            var errors = root.TryGetProperty("headlessEngineErrors", out var patterns)
                ? patterns.EnumerateObject().Select(p => new ErrorAllowance(p.Name, int.MaxValue, p.Value.GetString() ?? "")).ToList()
                : new List<ErrorAllowance>();
            return new HeadlessLimits(windowed, errors);
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}

/// <summary>What <c>analysis/headless-limits.json</c> says: the suites that run windowed, and the
/// engine error lines a headless process prints that a windowed one does not.</summary>
public sealed record HeadlessLimits(IReadOnlySet<string> Windowed, IReadOnlyList<ErrorAllowance> EngineErrors);
