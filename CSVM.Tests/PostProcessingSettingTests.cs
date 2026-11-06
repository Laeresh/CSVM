using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The Post-processing word: on with nothing set, off under <c>--det</c>, the flag beating both.</summary>
public class PostProcessingSettingTests
{
    [Fact]
    public void TheWordIsOnWithNothingSetAndOffUnderDet()
    {
        Assert.Equal(PostProcessingSetting.On, PostProcessingSetting.FallbackFor(det: false).Word);
        Assert.Equal(PostProcessingSetting.Off, PostProcessingSetting.FallbackFor(det: true).Word);
        Assert.Equal(PostProcessingSetting.DetRule, PostProcessingSetting.FallbackFor(det: true).Rule);
        var resolved = PostProcessingSetting.Lookup.Resolve(PostProcessingSetting.On, null, null, PostProcessingSetting.Off);
        Assert.Equal(SettingSource.Flag, resolved.Source);
        Assert.Equal(PostProcessingSetting.On, resolved.Word);
        Assert.Contains(PostProcessingSetting.Lookup, WordSettings.All);
    }
}