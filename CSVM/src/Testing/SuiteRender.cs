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

    /// <summary>The term that narrows a run to the landing gate: no
    /// <see cref="SuiteCatalog.ReleaseTier"/> suite, and no windowed process for shader texts alone.</summary>
    public const string GateLane = "lane:gate";

    /// <summary>The maps of <c>headless-limits.json</c> whose suites run windowed.</summary>
    internal static readonly string[] WindowedMaps = { "headlessOnly", "headlessDiffers", ShaderMap };

    private const string ShaderMap = "headlessShaders";
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

    /// <summary>Pulls the <see cref="GateLane"/> term out of a <c>--run-tests=</c> value, leaving the
    /// other terms in <paramref name="remainder"/>. True when it was there.</summary>
    public static bool TakeGate(string spec, out string remainder)
    {
        var terms = spec.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
        bool gate = terms.RemoveAll(t => t.Equals(GateLane, StringComparison.OrdinalIgnoreCase)) > 0;
        remainder = string.Join(",", terms);
        return gate;
    }

    /// <summary>The kind of process that runs <paramref name="name"/>. In the landing gate a suite
    /// that is windowed only for its shader texts runs where it otherwise would.</summary>
    public static string KindOf(string name, HeadlessLimits limits, bool gate = false) =>
        limits.Windowed.Contains(name) && !(gate && limits.ShaderOnly.Contains(name)) ? Windowed
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
            var shaderOnly = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string map in WindowedMaps)
            {
                if (root.TryGetProperty(map, out var entries))
                {
                    var names = entries.EnumerateObject().Select(e => e.Name).ToList();
                    (map == ShaderMap ? shaderOnly : windowed).UnionWith(names);
                }
            }
            shaderOnly.ExceptWith(windowed);
            windowed.UnionWith(shaderOnly);
            var errors = root.TryGetProperty("headlessEngineErrors", out var patterns)
                ? patterns.EnumerateObject().Select(p => new ErrorAllowance(p.Name, int.MaxValue, p.Value.GetString() ?? "")).ToList()
                : new List<ErrorAllowance>();
            return new HeadlessLimits(windowed, shaderOnly, errors);
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}

/// <summary>What <c>analysis/headless-limits.json</c> says. <c>Windowed</c> is every suite that runs
/// windowed. <c>ShaderOnly</c> is those that pass headless and need a renderer for their shader
/// texts alone. <c>EngineErrors</c> is the error lines only a headless process prints.</summary>
public sealed record HeadlessLimits(
    IReadOnlySet<string> Windowed, IReadOnlySet<string> ShaderOnly, IReadOnlyList<ErrorAllowance> EngineErrors);
