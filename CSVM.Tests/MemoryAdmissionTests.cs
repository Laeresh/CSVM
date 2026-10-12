using CSVM.Tooling;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The engine's half of the memory ledger: which launches it judges, under which kind, and the
/// floor below which it refuses to start. <c>MemoryLedger.ps1 -SelfTest</c> checks the same rules
/// on the PowerShell side.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class MemoryAdmissionTests
{
    [Fact]
    public void OnlyALaunchThatEndsItselfIsJudged()
    {
        Assert.True(MemoryAdmission.IsNonInteractive(["--det"]));
        Assert.True(MemoryAdmission.IsNonInteractive(["--run-tests=tier:quick"]));
        Assert.True(MemoryAdmission.IsNonInteractive(["--plane=player_bhawk", "--frames=9", "--screenshot=a.png"]));
        Assert.True(MemoryAdmission.IsNonInteractive(["--shots=3"]));

        // ABLE-TO-FAIL CONTROL: interactive play, with content args, is never judged.
        Assert.False(MemoryAdmission.IsNonInteractive(["--plane=player_bhawk", "--chapter=C4"]));
        Assert.False(MemoryAdmission.IsNonInteractive([]));
    }

    [Fact]
    public void KindsMatchTheLedgerScript()
    {
        Assert.Equal("engine-shard", MemoryAdmission.Kind(["--run-tests", "--net-port-base=40000"]));
        Assert.Equal("capture-xr", MemoryAdmission.Kind(["--det", "--xr-sim=eye=2112x2304", "--graphics=enhanced"]));
        Assert.Equal("capture-enhanced", MemoryAdmission.Kind(["--det", "--graphics=enhanced", "--frames=9"]));
        Assert.Equal("perf", MemoryAdmission.Kind(["--det", "--perf", "--frames=9"]));
        Assert.Equal("hitch", MemoryAdmission.Kind(["--hitch-inject=50@300", "--frames=9"]));
        Assert.Equal("probe", MemoryAdmission.Kind(["--det", "--frames=9", "--screenshot=a.png"]));
    }

    [Fact]
    public void ALaunchBelowTheFloorIsRefusedWithTheTripwireCode()
    {
        Assert.Equal(8.0, MemoryAdmission.FloorGb(gaming: false));
        Assert.Equal(16.0, MemoryAdmission.FloorGb(gaming: true));
        string? refusal = MemoryAdmission.Refusal(2.0, 8.0);
        Assert.NotNull(refusal);
        Assert.Contains("2.0 GB available is below the 8 GB floor", refusal);
        Assert.Contains($"exit {MemoryAdmission.TripwireExitCode}", refusal);

        // ABLE-TO-FAIL CONTROL: at the floor the launch starts, and so does one whose figure is unreadable.
        Assert.Null(MemoryAdmission.Refusal(8.0, 8.0));
        Assert.Null(MemoryAdmission.Refusal(null, 8.0));
    }
}
