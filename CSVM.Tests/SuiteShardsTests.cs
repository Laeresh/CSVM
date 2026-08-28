using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Testing;
using Xunit;

namespace CSVM.Tests;

[Trait("Tier", "Quick")]
public sealed class SuiteShardsTests
{
    [Fact]
    public void A_shard_term_is_taken_out_of_the_selector_it_rides_in()
    {
        var shard = SuiteShards.Parse("tier:quick,shard:2/4,weapons", out string rest, out string? error);

        Assert.Null(error);
        Assert.Equal(new ShardSpec(2, 4), shard);
        Assert.Equal("tier:quick,weapons", rest);
    }

    [Fact]
    public void No_shard_term_leaves_the_selector_alone()
    {
        var shard = SuiteShards.Parse("suite:weapons-fire", out string rest, out string? error);

        Assert.Null(error);
        Assert.Null(shard);
        Assert.Equal("suite:weapons-fire", rest);
    }

    [Theory]
    [InlineData("shard:0/4")]
    [InlineData("shard:5/4")]
    [InlineData("shard:2/0")]
    [InlineData("shard:2")]
    [InlineData("shard:a/4")]
    [InlineData("shard:1/2,shard:2/2")]
    public void A_malformed_or_out_of_range_shard_term_is_an_error_not_a_full_run(string spec)
    {
        var shard = SuiteShards.Parse(spec, out _, out string? error);

        Assert.NotNull(error);
        Assert.Null(shard);
    }

    [Fact]
    public void Every_selected_suite_lands_in_exactly_one_shard_in_registry_order()
    {
        var names = TestHarness.All.Select(s => s.Name).ToList();
        var weights = Weights(names.Select((n, i) => (n, i))
            .ToDictionary(p => p.n, p => 0.5 + (p.i % 7)));

        foreach (int count in new[] { 1, 2, 3, 4, 6, 8 })
        {
            var shards = SuiteShards.Plan(names, n => n, weights, count);

            Assert.Equal(count, shards.Count);
            Assert.Equal(names.OrderBy(n => names.IndexOf(n)),
                shards.SelectMany(s => s).OrderBy(n => names.IndexOf(n)));
            Assert.Equal(names.Count, shards.Sum(s => s.Count));
            Assert.Equal(names.Count, shards.SelectMany(s => s).Distinct().Count());
            foreach (var shard in shards)
            {
                var positions = shard.Select(n => names.IndexOf(n)).ToList();
                Assert.Equal(positions.OrderBy(p => p), positions);
            }
        }
    }

    [Fact]
    public void The_plan_is_the_same_every_time_and_balances_the_weight()
    {
        var names = Enumerable.Range(0, 40).Select(i => $"s{i:00}").ToList();
        var weights = Weights(names.Select((n, i) => (n, i)).ToDictionary(p => p.n, p => 1.0 + p.i));

        var first = SuiteShards.Plan(names, n => n, weights, 4);
        var again = SuiteShards.Plan(names, n => n, weights, 4);

        Assert.Equal(first.Select(s => s.ToArray()), again.Select(s => s.ToArray()));
        var loads = first.Select(s => s.Sum(weights.For)).ToList();
        double floor = names.Sum(weights.For) / 4.0;
        // Longest-first greedy is within one item's weight of the arithmetic floor; the point is
        // that no shard is left carrying the whole catalog.
        Assert.All(loads, load => Assert.True(load <= floor + weights.For(names[^1])));
    }

    [Fact]
    public void A_grouped_pair_is_never_split_across_shards()
    {
        var names = Enumerable.Range(0, 12).Select(i => $"s{i:00}").ToList();
        var weights = Weights(names.ToDictionary(n => n, _ => 1.0),
            new[] { "s00", "s11" });

        foreach (int count in new[] { 2, 3, 4 })
        {
            var shards = SuiteShards.Plan(names, n => n, weights, count);
            var withFirst = shards.Single(s => s.Contains("s00"));

            Assert.Contains("s11", withFirst);
        }
    }

    [Fact]
    public void A_group_naming_a_suite_the_selection_left_out_still_places_the_rest()
    {
        var names = new[] { "a", "b" };
        var weights = Weights(names.ToDictionary(n => n, _ => 1.0), new[] { "a", "absent" });

        var shards = SuiteShards.Plan(names, n => n, weights, 2);

        Assert.Equal(new[] { "a" }, shards[0]);
        Assert.Equal(new[] { "b" }, shards[1]);
    }

    [Fact]
    public void An_unweighted_suite_takes_the_default_and_is_reported()
    {
        var weights = Weights(new Dictionary<string, double> { ["known"] = 9.0 });

        Assert.Equal(9.0, weights.For("known"));
        Assert.Equal(1.0, weights.For("unknown"));
        Assert.Equal(new[] { "unknown" }, SuiteShards.Unweighted(new[] { "known", "unknown" }, weights));
    }

    [Fact]
    public void More_shards_than_suites_leaves_the_extra_shards_empty_rather_than_failing()
    {
        var shards = SuiteShards.Plan(new[] { "only" }, n => n, SuiteWeights.Empty, 3);

        Assert.Equal(3, shards.Count);
        Assert.Equal(new[] { "only" }, shards[0]);
        Assert.Empty(shards[1]);
        Assert.Empty(shards[2]);
    }

    [Fact]
    public void The_checked_in_weights_file_names_only_registered_suites()
    {
        string path = Path.Combine(RepoRoot(), "analysis", "engine-suite-weights.json");
        var weights = SuiteShards.Load(path);
        var registered = TestHarness.All.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.NotEmpty(weights.Seconds);
        Assert.All(weights.Seconds.Keys, name => Assert.Contains(name, registered));
        Assert.All(weights.Groups.SelectMany(g => g), name => Assert.Contains(name, registered));
        Assert.All(weights.Seconds.Values, seconds => Assert.True(seconds >= 0));
        // Every registered suite carries a measured weight, or the balance is guesswork for it.
        Assert.Empty(SuiteShards.Unweighted(registered, weights));
    }

    [Fact]
    public void A_missing_weights_file_weighs_every_suite_the_same_rather_than_throwing()
    {
        var weights = SuiteShards.Load(Path.Combine(Path.GetTempPath(), "csvm-no-such-weights.json"));

        Assert.Empty(weights.Seconds);
        Assert.Equal(1.0, weights.For("anything"));
    }

    private static SuiteWeights Weights(IDictionary<string, double> seconds,
        params string[][] groups) => new()
        {
            DefaultSeconds = 1.0,
            Seconds = new Dictionary<string, double>(seconds, StringComparer.OrdinalIgnoreCase),
            Groups = groups.Select(g => (IReadOnlyList<string>)g).ToList(),
        };

    // The test host runs out of CSVM.Tests/bin/<cfg>/<tfm>; the repo root is the first ancestor
    // carrying PROJECT_CONTEXT.md, which holds from a worktree too.
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PROJECT_CONTEXT.md")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}
