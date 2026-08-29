using System.Linq;
using CSVM.Testing;
using Xunit;

namespace CSVM.Tests;

[Trait("Tier", "Quick")]
public sealed class SuiteCatalogTests
{
    [Fact]
    public void Names_preserve_the_registered_order()
    {
        string[] names = TestHarness.All.Select(suite => suite.Name).ToArray();

        Assert.Equal(180, names.Length);
        Assert.Equal(SuiteCatalog.Names, names);
        Assert.Equal("emitter-lifetime", names[0]);
        Assert.Equal("airframe-hull-coverage", names[^1]);
        Assert.Equal(names.Length, names.Distinct().Count());
    }

    [Fact]
    public void The_quick_tier_is_a_registered_subset()
    {
        var names = SuiteCatalog.Names.ToHashSet();

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
            SuiteCatalog.Names.Where(n => SuiteCatalog.QuickTier.Contains(n)).ToArray(),
            selected.Select(s => s.Name).ToArray());
    }

    [Fact]
    public void Several_terms_union_and_a_term_matching_nothing_is_reported()
    {
        var union = TestHarness.Select(TestHarness.All, "suite:weapons-fire, suite:air-to-air",
            out var unmatched);
        Assert.Equal(new[] { "weapons-fire", "air-to-air" }, union.Select(s => s.Name));
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

    [Fact]
    public void An_empty_spec_selects_every_registered_suite()
    {
        var all = TestHarness.Select(TestHarness.All, "", out var unmatched);

        Assert.Equal(SuiteCatalog.Names, all.Select(s => s.Name).ToArray());
        Assert.Empty(unmatched);
    }
}
