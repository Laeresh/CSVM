using System.IO;
using System.Linq;
using CSVM.Testing;
using Xunit;

namespace CSVM.Tests;

[Trait("Tier", "Quick")]
public sealed class SuiteRenderTests
{
    private static readonly HeadlessLimits Limits =
        SuiteRender.Load(Path.Combine(TestData.RepoRoot, "analysis", "headless-limits.json"))!;

    [Fact]
    public void A_render_term_is_taken_out_of_the_selector_it_rides_in()
    {
        var kinds = SuiteRender.Parse("tier:quick,render:headless+synthetic,shard:2/6", out string rest, out string? error);

        Assert.Null(error);
        Assert.Equal(new[] { "headless", "synthetic" }, kinds!.Order());
        Assert.Equal("tier:quick,shard:2/6", rest);
    }

    [Fact]
    public void No_render_term_leaves_the_selector_alone()
    {
        Assert.Null(SuiteRender.Parse("suite:weapons-fire", out string rest, out string? error));
        Assert.Null(error);
        Assert.Equal("suite:weapons-fire", rest);
    }

    [Theory]
    [InlineData("render:gpu")]
    [InlineData("render:windowed+")]
    [InlineData("render:windowed,render:headless")]
    public void A_malformed_render_term_is_an_error_not_a_full_run(string spec)
    {
        Assert.Null(SuiteRender.Parse(spec, out _, out string? error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Every_suite_has_one_kind_and_a_windowed_map_outranks_the_ci_tier()
    {
        Assert.Equal("windowed", SuiteRender.KindOf("display-mode", Limits));
        Assert.Equal("windowed", SuiteRender.KindOf("wind-streaks", Limits));
        Assert.Contains("wind-streaks", SuiteCatalog.CiTier);
        Assert.Equal("synthetic", SuiteRender.KindOf(SuiteCatalog.CiTier.First(n => !Limits.Windowed.Contains(n)), Limits));
        Assert.Equal("headless", SuiteRender.KindOf("zeppelin-motion", Limits));
    }

    [Fact]
    public void The_headless_engine_errors_are_uncapped_allowances()
    {
        Assert.NotEmpty(Limits.EngineErrors);
        Assert.All(Limits.EngineErrors, a => Assert.Equal(int.MaxValue, a.Max));
    }
}
