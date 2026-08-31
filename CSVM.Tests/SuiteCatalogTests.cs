using System;
using System.IO;
using System.Linq;
using CSVM.Testing;
using Xunit;

namespace CSVM.Tests;

[Trait("Tier", "Quick")]
public sealed class SuiteCatalogTests
{
    private static string[] Names => TestHarness.All.Select(suite => suite.Name).ToArray();

    [Fact]
    public void Every_registered_suite_is_named_once_and_described()
    {
        var suites = TestHarness.All;

        Assert.NotEmpty(suites);
        Assert.All(suites, suite => Assert.Matches("^[a-z0-9]+(-[a-z0-9]+)*$", suite.Name));
        Assert.All(suites, suite => Assert.NotEmpty(suite.What.Trim()));
        Assert.Equal(suites.Count, Names.Distinct().Count());
    }

    [Fact]
    public void The_registry_is_ordered_by_name()
    {
        // Not presentation: SuiteShards breaks balancer ties on registry position and sorts each
        // shard by it, so a rerun divides the same way only while this order is fixed.
        Assert.Equal(Names.OrderBy(name => name, StringComparer.Ordinal).ToArray(), Names);
    }

    [Fact]
    public void Every_weighted_name_is_a_registered_suite()
    {
        string path = Path.Combine(TestData.RepoRoot, "analysis", "engine-suite-weights.json");
        var weights = SuiteShards.Load(path);
        var registered = Names.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // A missing file loads as Empty, which would let every assertion below pass over nothing.
        Assert.True(File.Exists(path), $"the weights file is committed and must be readable: {path}");
        Assert.NotEmpty(weights.Seconds);
        Assert.All(weights.Seconds.Keys, name => Assert.Contains(name, registered));
        Assert.All(weights.Groups.SelectMany(group => group), name => Assert.Contains(name, registered));
    }

    [Fact]
    public void The_quick_tier_is_a_registered_subset()
    {
        var names = Names.ToHashSet();

        Assert.NotEmpty(SuiteCatalog.QuickTier);
        Assert.All(SuiteCatalog.QuickTier, name => Assert.Contains(name, names));
        Assert.Equal(SuiteCatalog.QuickTier.Count, SuiteCatalog.QuickTier.Distinct().Count());
        // puffer-modes covers the emitter runtime end to end for a fraction of the wall time.
        Assert.DoesNotContain("emitter-lifetime", SuiteCatalog.QuickTier);
        Assert.Null(SuiteCatalog.Tier("nosuchtier"));
    }

    [Fact]
    public void An_exact_term_selects_one_suite_and_a_substring_still_selects_many()
    {
        var exact = TestHarness.Select(TestHarness.All, "suite:weapons-fire", out var unmatched);
        Assert.Equal(new[] { "weapons-fire" }, exact.Select(s => s.Name));
        Assert.Empty(unmatched);

        var substring = TestHarness.Select(TestHarness.All, "puffer", out unmatched);
        Assert.True(substring.Count > 1);
        Assert.All(substring, suite => Assert.Contains("puffer", suite.Name));
        Assert.Empty(unmatched);
    }

    [Fact]
    public void The_tier_term_selects_the_tier_in_registry_order()
    {
        var selected = TestHarness.Select(TestHarness.All, "tier:quick", out var unmatched);

        Assert.Empty(unmatched);
        Assert.Equal(SuiteCatalog.QuickTier.Count, selected.Count);
        Assert.Equal(
            Names.Where(n => SuiteCatalog.QuickTier.Contains(n)).ToArray(),
            selected.Select(s => s.Name).ToArray());
    }

    [Fact]
    public void Several_terms_union_and_a_term_matching_nothing_is_reported()
    {
        var union = TestHarness.Select(TestHarness.All, "suite:weapons-fire, suite:air-to-air",
            out var unmatched);
        Assert.Equal(new[] { "air-to-air", "weapons-fire" }, union.Select(s => s.Name));
        Assert.Empty(unmatched);

        // A near miss on an exact name, a substring nothing carries, and an unregistered tier: each
        // is a typo the run must refuse rather than silently narrow itself over.
        TestHarness.Select(TestHarness.All, "suite:weapons", out var missedExact);
        Assert.Equal(new[] { "suite:weapons" }, missedExact);
        TestHarness.Select(TestHarness.All, "no-such-suite", out var missedSubstring);
        Assert.Equal(new[] { "no-such-suite" }, missedSubstring);
        TestHarness.Select(TestHarness.All, "tier:nosuchtier", out var missedTier);
        Assert.Equal(new[] { "tier:nosuchtier" }, missedTier);

        // One good term does not excuse the bad one beside it.
        var partial = TestHarness.Select(TestHarness.All, "suite:weapons-fire,no-such-suite",
            out var mixed);
        Assert.Equal(new[] { "weapons-fire" }, partial.Select(s => s.Name));
        Assert.Equal(new[] { "no-such-suite" }, mixed);
    }
}
