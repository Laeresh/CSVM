using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The shadow level's fallback by GPU and pane count, and the ladder over it. Engine-free:
/// the GPU is handed in rather than asked of a renderer, which the unit host does not have.</summary>
public class ShadowQualitySettingTests
{
    /// <summary>A discrete GPU keeps Ultra at every count. An integrated one runs High at one and
    /// two panes and drops the sun's shadow at three and four.</summary>
    [Theory]
    [InlineData(false, 1, ShadowQualitySetting.Ultra)]
    [InlineData(false, 2, ShadowQualitySetting.Ultra)]
    [InlineData(false, 3, ShadowQualitySetting.Ultra)]
    [InlineData(false, 4, ShadowQualitySetting.Ultra)]
    [InlineData(true, 1, ShadowQualitySetting.High)]
    [InlineData(true, 2, ShadowQualitySetting.High)]
    [InlineData(true, 3, ShadowQualitySetting.Off)]
    [InlineData(true, 4, ShadowQualitySetting.Off)]
    public void FallbackFollowsTheGpuAndThePaneCount(bool integrated, int panes, string expected)
    {
        Assert.Equal(expected, ShadowQualitySetting.FallbackFor(integrated, panes));
    }

    /// <summary>The fallback's source names the rule that chose it, so a log line can tell the
    /// splitscreen rule from the one-pane integrated default.</summary>
    [Theory]
    [InlineData(false, 4, "default")]
    [InlineData(true, 2, "default_integrated_gpu")]
    [InlineData(true, 4, "default_integrated_gpu_panes")]
    public void NothingSetRunsTheFallbackUnderItsOwnSource(bool integrated, int panes, string source)
    {
        string fallback = ShadowQualitySetting.FallbackFor(integrated, panes);
        var plan = ShadowQualitySetting.Pick(null, null, fallback, fallback);
        Assert.Equal(fallback, plan.Word);
        Assert.Equal(source, plan.Source);
    }

    /// <summary>A level the player saved beats the four-pane rule, and so do the flag and the
    /// config key. The rule moves a default, never a choice.</summary>
    [Fact]
    public void AChoiceBeatsTheFourPaneRule()
    {
        string fallback = ShadowQualitySetting.FallbackFor(integrated: true, panes: 4);

        var saved = ShadowQualitySetting.Pick(null, ShadowQualitySetting.High, fallback, fallback);
        Assert.Equal((ShadowQualitySetting.High, "options.json"), (saved.Word, saved.Source));

        var flag = ShadowQualitySetting.Pick(ShadowQualitySetting.Medium, null, fallback, fallback);
        Assert.Equal((ShadowQualitySetting.Medium, "--shadow-quality"), (flag.Word, flag.Source));

        var key = ShadowQualitySetting.Pick(null, null, ShadowQualitySetting.High, fallback);
        Assert.Equal((ShadowQualitySetting.High, ShadowQualitySetting.Key), (key.Word, key.Source));
    }
}
