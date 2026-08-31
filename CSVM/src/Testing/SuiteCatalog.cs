using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CSVM.Testing;

/// <summary>The registry of the in-engine assertion suites, discovered from the
/// <see cref="SuiteAttribute"/> each body carries in the <c>*Suites.cs</c> modules. It holds no
/// per-suite table of its own: a suite's name and description live on the suite, so adding one
/// touches one file.</summary>
public static class SuiteCatalog
{
    // The suites `--run-tests=tier:quick` runs: one representative per failure surface, checked in
    // here rather than inferred from a diff. Membership is a judgement about the SET, which is why
    // it stays a list rather than moving onto the bodies. The selection rule is in docs/tooling.md;
    // the tier is partial and the full catalog stays the landing gate.
    public static readonly IReadOnlyList<string> QuickTier = new[]
    {
        "puffer-modes",
        "loadout-bind",
        "weapons-fire",
        "air-to-air",
        "ai-actor",
        "instant-action",
        "damage-stages",
        "damage-hd",
        "effect-template-mesh",
        "collision-visibility",
        "target-selection",
        "campaign-objectives",
        "music-states",
    };

    /// <summary>The suite names a tier holds, or null when no tier carries that name (which a
    /// selector must treat as a miss, not as an empty selection).</summary>
    public static IReadOnlyList<string>? Tier(string name) =>
        name.Equals("quick", StringComparison.OrdinalIgnoreCase) ? QuickTier : null;

    /// <summary>Every marked body, ordered by name. ⚠ Keep this order deterministic and
    /// machine-independent: <see cref="SuiteShards"/> breaks balancer ties on registry position and
    /// sorts each shard by it, so a rerun divides the same way only if the order does not move.
    /// Reflection order does not qualify; alphabetical does. No suite depends on running after
    /// another.</summary>
    internal static IReadOnlyList<TestHarness.Suite> Discover()
    {
        var found = new List<TestHarness.Suite>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in typeof(SuiteCatalog).Assembly.GetTypes())
        {
            const BindingFlags Where = BindingFlags.Static | BindingFlags.Public
                | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var method in type.GetMethods(Where))
            {
                if (method.GetCustomAttribute<SuiteAttribute>() is not { } mark)
                {
                    continue;
                }
                found.Add(new TestHarness.Suite(mark.Name, mark.What, Body(method, mark, seen)));
            }
        }
        found.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return found;
    }

    // A malformed declaration throws rather than being skipped: a suite that quietly fails to
    // register runs nowhere and is reported by nothing, which is the failure the registry exists to
    // prevent.
    private static Action<TestContext> Body(MethodInfo method, SuiteAttribute mark, HashSet<string> seen)
    {
        string where = $"{method.DeclaringType?.Name}.{method.Name}";
        if (mark.Name.Length == 0 || mark.What.Length == 0)
        {
            throw new InvalidOperationException($"[Suite] on {where} carries an empty name or description");
        }
        if (!seen.Add(mark.Name))
        {
            throw new InvalidOperationException($"[Suite] name '{mark.Name}' is registered twice, at {where}");
        }
        if (method.ReturnType != typeof(void)
            || method.GetParameters() is not [{ ParameterType: var only }]
            || only != typeof(TestContext))
        {
            throw new InvalidOperationException(
                $"[Suite] on {where} must sit on a static void {method.Name}(TestContext) body");
        }
        return (Action<TestContext>)method.CreateDelegate(typeof(Action<TestContext>));
    }
}
