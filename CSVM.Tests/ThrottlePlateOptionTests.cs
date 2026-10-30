using CSVM.Spec;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The throttle plate's Game Options switch, engine-free. It is off with nothing saved, and on from
/// the file or <c>--throttle-plate</c>. A <c>--det</c> run never reads the file, which keeps the
/// plate out of every golden.
/// </summary>
public class ThrottlePlateOptionTests
{
    [Fact]
    public void ThePlateIsOffWithNoFlagAndNothingSaved() =>
        Assert.False(SessionSpec.Parse(new[] { "--fly" }).WithSavedThrottlePlate(null).ThrottlePlate);

    [Fact]
    public void ASavedSwitchShowsItAndTheFlagBeatsASavedOff()
    {
        Assert.True(SessionSpec.Parse(new[] { "--fly" }).WithSavedThrottlePlate(true).ThrottlePlate);
        Assert.False(SessionSpec.Parse(new[] { "--fly" }).WithSavedThrottlePlate(false).ThrottlePlate);
        Assert.True(SessionSpec.Parse(new[] { "--fly", "--throttle-plate" }).WithSavedThrottlePlate(false).ThrottlePlate);
    }

    [Fact]
    public void ADetRunReadsNoSavedSwitchButKeepsItsFlag()
    {
        Assert.False(SessionSpec.Parse(new[] { "--fly", "--det" }).WithSavedThrottlePlate(true).ThrottlePlate);
        Assert.True(SessionSpec.Parse(new[] { "--fly", "--det", "--throttle-plate" }).WithSavedThrottlePlate(false).ThrottlePlate);
    }

    [Fact]
    public void TheOptionsFileCarriesTheSwitchAndAnAbsentKeyReadsNeverSet()
    {
        Assert.True(OptionsStore.Deserialize(OptionsStore.Serialize(new OptionsDef { ThrottlePlate = true }))!.ThrottlePlate);
        Assert.Null(OptionsStore.Deserialize(OptionsStore.Serialize(new OptionsDef()))!.ThrottlePlate);
    }
}
