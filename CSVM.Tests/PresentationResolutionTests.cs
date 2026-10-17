using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The resolution contract: the CLI override, else the Original default. Availability is
/// checked only after the request is picked, and an unavailable request falls back to the default.
/// The requested value never changes because of a fallback.</summary>
public class PresentationResolutionTests
{
    [Fact]
    public void NoInputsResolveToTheDefault()
    {
        var (active, reason) = PresentationResolution.Resolve(null, Always);

        Assert.Equal(PresentationResolution.Default, active);
        Assert.Null(reason);
    }

    [Fact]
    public void AnUnavailableDefaultStaysActiveWithAReason()
    {
        var (active, reason) = PresentationResolution.Resolve(null, Never);

        Assert.Equal(PresentationResolution.Default, active);
        Assert.NotNull(reason);
    }

    [Fact]
    public void CliOverride_BeatsTheDefault()
    {
        var (active, reason) = PresentationResolution.Resolve("modern", Always);

        Assert.Equal("modern", active);
        Assert.Null(reason);
    }

    [Fact]
    public void AnUnknownOverride_FallsBackToTheDefaultWithAReason()
    {
        var (active, reason) = PresentationResolution.Resolve("built-in", id => id == PresentationResolution.Default);

        Assert.Equal(PresentationResolution.Default, active);
        Assert.Contains("built-in", reason);
    }

    /// <summary>A temporary availability failure must never look like the request changed:
    /// <see cref="PresentationResolution.Requested"/> still answers the override on the same input
    /// that resolved to the default above.</summary>
    [Fact]
    public void RequestedStaysTheOverride_EvenWhenActiveFallsBack()
    {
        PresentationResolution.Resolve("modern", Never);

        Assert.Equal("modern", PresentationResolution.Requested("modern"));
        Assert.Equal("original", PresentationResolution.Requested(null));
    }

    [Fact]
    public void EmptyStringsAreTreatedAsAbsent()
    {
        Assert.Equal(PresentationResolution.Default, PresentationResolution.Requested(""));
    }

    private static bool Always(string _) => true;

    private static bool Never(string _) => false;
}
