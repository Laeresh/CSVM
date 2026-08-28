using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="StartupProfile.Phases"/> — the read-only view B11's <c>TestContext.BuildWorld</c>
/// feeds into <c>PhaseAttribution.Categorize</c> without emitting the <c>[perf] startup …</c> line.
/// Godot-free (the class has no Godot dependency), so this is provable outside the engine.
/// </summary>
[Trait("Tier", "Quick")]
public class StartupProfilePhasesTests
{
    [Fact]
    public void PhasesReflectsAddCallsAndAccumulatesRepeats()
    {
        var profile = new StartupProfile("test-suite", bootMs: 0);
        profile.Add("gamez", 10);
        profile.Add("gamez", 5);
        profile.Add("world", 2);
        Assert.Equal(15, profile.Phases["gamez"]);
        Assert.Equal(2, profile.Phases["world"]);
    }

    [Fact]
    public void PhasesIsReadableWithoutCallingEndBuildOrEmit()
    {
        // TestContext.BuildWorld reads Phases mid-build, before either lifecycle method runs —
        // this is the contract that makes that legal.
        var profile = new StartupProfile("test-suite", bootMs: 0);
        profile.Add("anim", 3);
        Assert.Equal(3, profile.Phases["anim"]);
    }

    [Fact]
    public void CurrentIsNullByDefaultSoRecordIsANoOpUntilInstalled()
    {
        StartupProfile.Current = null;
        // Record must not throw with no session installed — the same guarantee the harness
        // deliberately relies on for the eight-world census case (docs/architecture.md).
        StartupProfile.Record("gamez", StartupProfile.Mark());
    }
}
