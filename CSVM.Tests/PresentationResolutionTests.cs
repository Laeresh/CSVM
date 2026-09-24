using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The resolution contract: fixed precedence force-Built-in → CLI override → the Original
/// default, availability checked only after the request is picked, and the requested value never
/// changes because of a fallback.</summary>
public class PresentationResolutionTests
{
    [Fact]
    public void NoInputsResolveToTheDefault()
    {
        var (active, reason) = PresentationResolution.Resolve(false, null, Always);

        Assert.Equal(PresentationResolution.Default, active);
        Assert.Null(reason);
    }

    [Fact]
    public void NoInputsFallBackToBuiltIn_WhenTheDefaultIsUnavailable()
    {
        var (active, reason) = PresentationResolution.Resolve(false, null, Never);

        Assert.Equal(PresentationResolution.BuiltIn, active);
        Assert.NotNull(reason);
    }

    [Fact]
    public void ForceBuiltIn_BeatsTheCliOverride()
    {
        var (active, reason) = PresentationResolution.Resolve(true, "original", Always);

        Assert.Equal(PresentationResolution.BuiltIn, active);
        Assert.NotNull(reason);
    }

    [Fact]
    public void ForceBuiltIn_BeatsTheDefault()
    {
        var (active, reason) = PresentationResolution.Resolve(true, null, Always);

        Assert.Equal(PresentationResolution.BuiltIn, active);
        Assert.NotNull(reason);
    }

    [Fact]
    public void CliOverride_BeatsTheDefault()
    {
        var (active, reason) = PresentationResolution.Resolve(false, PresentationResolution.BuiltIn, Always);

        Assert.Equal(PresentationResolution.BuiltIn, active);
        Assert.Null(reason);
    }

    [Fact]
    public void CliBuiltIn_NeedsNoAvailabilityCheck()
    {
        var (active, reason) = PresentationResolution.Resolve(false, PresentationResolution.BuiltIn, Never);

        Assert.Equal(PresentationResolution.BuiltIn, active);
        Assert.Null(reason);
    }

    [Fact]
    public void UnavailableRequest_FallsBackToBuiltInWithAReason()
    {
        var (active, reason) = PresentationResolution.Resolve(false, "original", Never);

        Assert.Equal(PresentationResolution.BuiltIn, active);
        Assert.NotNull(reason);
    }

    /// <summary>A temporary availability failure must never look like the request changed:
    /// <see cref="PresentationResolution.Requested"/> still answers "original" on the same input
    /// that resolved to Built-in above.</summary>
    [Fact]
    public void RequestedStaysOriginal_EvenWhenActiveFallsBackToBuiltIn()
    {
        PresentationResolution.Resolve(false, null, Never);

        Assert.Equal("original", PresentationResolution.Requested(null));
    }

    [Fact]
    public void RequestedStaysOriginal_EvenWhenForceBuiltInWinsActive()
    {
        var (active, _) = PresentationResolution.Resolve(true, null, Always);

        Assert.Equal(PresentationResolution.BuiltIn, active);
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
