using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The resolution contract: fixed precedence force-Built-in → CLI override → saved
/// request → Built-in default, availability checked only after the request is picked, and the
/// requested value never changes because of a fallback.</summary>
public class PresentationResolutionTests
{
    [Fact]
    public void NoInputsResolveToBuiltIn()
    {
        var (active, reason) = PresentationResolution.Resolve(false, null, null, Always);

        Assert.Equal(PresentationResolution.BuiltIn, active);
        Assert.Null(reason);
    }

    [Fact]
    public void ForceBuiltIn_BeatsTheCliOverride()
    {
        var (active, reason) = PresentationResolution.Resolve(true, "original", null, Always);

        Assert.Equal(PresentationResolution.BuiltIn, active);
        Assert.NotNull(reason);
    }

    [Fact]
    public void ForceBuiltIn_BeatsTheSavedRequest()
    {
        var (active, reason) = PresentationResolution.Resolve(true, null, "original", Always);

        Assert.Equal(PresentationResolution.BuiltIn, active);
        Assert.NotNull(reason);
    }

    [Fact]
    public void CliOverride_BeatsTheSavedRequest()
    {
        Assert.Equal("original", PresentationResolution.Requested("original", "some-other"));
    }

    [Fact]
    public void CliOverride_BeatsTheBuiltInDefault()
    {
        var (active, reason) = PresentationResolution.Resolve(false, "original", null, Always);

        Assert.Equal("original", active);
        Assert.Null(reason);
    }

    [Fact]
    public void SavedRequest_BeatsTheBuiltInDefault()
    {
        var (active, reason) = PresentationResolution.Resolve(false, null, "original", Always);

        Assert.Equal("original", active);
        Assert.Null(reason);
    }

    [Fact]
    public void UnavailableRequest_FallsBackToBuiltInWithAReason()
    {
        var (active, reason) = PresentationResolution.Resolve(false, null, "original", Never);

        Assert.Equal(PresentationResolution.BuiltIn, active);
        Assert.NotNull(reason);
    }

    /// <summary>A temporary availability failure must never look like the saved request changed:
    /// <see cref="PresentationResolution.Requested"/> still answers "original" on the same inputs
    /// that resolved to Built-in above.</summary>
    [Fact]
    public void RequestedStaysOriginal_EvenWhenActiveFallsBackToBuiltIn()
    {
        PresentationResolution.Resolve(false, null, "original", Never);

        Assert.Equal("original", PresentationResolution.Requested(null, "original"));
    }

    [Fact]
    public void RequestedStaysOriginal_EvenWhenForceBuiltInWinsActive()
    {
        var (active, _) = PresentationResolution.Resolve(true, null, "original", Always);

        Assert.Equal(PresentationResolution.BuiltIn, active);
        Assert.Equal("original", PresentationResolution.Requested(null, "original"));
    }

    [Fact]
    public void EmptyStringsAreTreatedAsAbsent()
    {
        Assert.Equal(PresentationResolution.BuiltIn, PresentationResolution.Requested("", ""));
    }

    private static bool Always(string _) => true;

    private static bool Never(string _) => false;
}
