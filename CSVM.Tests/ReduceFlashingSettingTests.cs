using CSVM.Flight.Weapons;
using CSVM.Spec;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The Reduce Flashing word and the distance a gun round's light waits for. Engine-free; the
/// lights it keeps dark are pinned by the <c>in-flight-round-lights</c> engine suite.</summary>
[Trait("Tier", "Quick")]
public class ReduceFlashingSettingTests
{
    /// <summary>The flag beats the saved word, which beats the config key, then Off, under
    /// <c>--det</c> too. It is registered, so the options file, the page and the apply carry it.</summary>
    [Fact]
    public void TheFlagBeatsTheSavedWordAndOffIsTheFallback()
    {
        var lookup = ReduceFlashingSetting.Lookup;
        Assert.Equal(new[] { ReduceFlashingSetting.Off, ReduceFlashingSetting.On }, lookup.Words);
        Assert.Equal(new ResolvedWord(ReduceFlashingSetting.On, SettingSource.Flag),
            lookup.Resolve(ReduceFlashingSetting.On, ReduceFlashingSetting.Off, null, ReduceFlashingSetting.Default));
        Assert.Equal(new ResolvedWord(ReduceFlashingSetting.On, SettingSource.Saved),
            lookup.Resolve(null, ReduceFlashingSetting.On, ReduceFlashingSetting.Off, ReduceFlashingSetting.Default));
        Assert.Equal(ReduceFlashingSetting.Off, lookup.Fallback(new SettingContext(true, true, 1)).Word);
        Assert.Equal(ReduceFlashingSetting.Off, lookup.Fallback(new SettingContext(false, false, 1)).Word);
        Assert.False(lookup.EnhancedOnly);
        Assert.Equal("reduceFlashing", lookup.SavedField);
        Assert.Equal(ReduceFlashingSetting.On, SessionSpec.Parse(new[] { "--reduce-flashing=on" }).WordFlags[lookup]);
        Assert.Null(SessionSpec.Parse(new[] { "--reduce-flashing=dim" }).WordFlags[lookup]);
        Assert.Contains(lookup, WordSettings.All);
    }

    /// <summary>The word the resolve stores is what the lights read.</summary>
    [Fact]
    public void TheResolvedWordIsWhatTheLightsRead()
    {
        var lookup = ReduceFlashingSetting.Lookup;
        string before = ReduceFlashingSetting.Word;
        var context = new SettingContext(true, true, 1);
        try
        {
            lookup.ResolveFrom(ReduceFlashingSetting.On, null, context);
            Assert.True(ReduceFlashingSetting.Active);
            lookup.ResolveFrom(null, null, context);
            Assert.False(ReduceFlashingSetting.Active);
        }
        finally
        {
            lookup.ResolveFrom(before, null, context);
        }
    }

    /// <summary>A gun round's light switches on a few tens of metres out. That is past the sphere its
    /// glow lights in a cloud, its far range times the clouds' point_reach of 3. The engine suite
    /// reads that factor from the shader's own default.</summary>
    [Fact]
    public void ARoundLightsOutsideItsOwnCloudGlow()
    {
        Assert.InRange(ProjectilePool.TracerGlowStart, ProjectilePool.TracerGlowFar * 3f, 100f);
    }
}
