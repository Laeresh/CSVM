using System.Linq;
using CSVM.Testing;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="TestHarness.Screen"/> — the error classifier the in-engine harness applies to the
/// run's engine log. It is the one part of the harness that can run outside Godot, and the part
/// that most needs proving: an allowlist that quietly absorbed an unfamiliar error would turn the
/// whole harness into a rubber stamp.
/// </summary>
public class TestHarnessScreenTests
{
    private const string KnownDet = "ERROR: Condition \"det == 0\" is true.";
    private const string KnownTree = "ERROR: Condition \"!is_inside_tree()\" is true. Returning: Transform3D()";

    [Fact]
    public void PlainOutputScreensClean()
    {
        var screen = TestHarness.Screen(new[]
        {
            "Godot Engine v4.7.stable.mono",
            "[test] run-tests suites=7/7",
            "WARNING: 1 ObjectDB instance was leaked at exit",
        });
        Assert.Equal(0, screen.Total);
        Assert.True(screen.Ok);
    }

    [Fact]
    public void KnownEngineErrorsAreAllowedAndCounted()
    {
        var screen = TestHarness.Screen(new[] { KnownDet, "   at: invert (core/math/basis.cpp:47)", KnownTree });
        Assert.Equal(2, screen.Total);
        Assert.Equal(2, screen.Allowed);
        Assert.Empty(screen.Unexpected);
        Assert.True(screen.Ok);
        // The counts are what makes an allowance auditable rather than a blind pass.
        Assert.Equal(2, screen.AllowedCounts.Values.Sum());
    }

    [Fact]
    public void AnUnknownEngineErrorFails()
    {
        var screen = TestHarness.Screen(new[]
        {
            KnownDet,
            "ERROR: Shader compilation failed",
            "   at: _update_shader (servers/rendering/shader.cpp:1)",
        });
        Assert.False(screen.Ok);
        string only = Assert.Single(screen.Unexpected);
        // The location line is folded into the error it belongs to, so a failure is diagnosable
        // from the report alone.
        Assert.Contains("Shader compilation failed", only);
        Assert.Contains("shader.cpp:1", only);
    }

    [Fact]
    public void AnAllowedPatternOverItsCapFails()
    {
        var allowance = TestHarness.ErrorAllowlist.First(a => a.Pattern.Contains("det == 0"));
        var lines = Enumerable.Repeat(KnownDet, allowance.Max + 1);
        var screen = TestHarness.Screen(lines);
        Assert.Empty(screen.Unexpected);
        Assert.False(screen.Ok);
        Assert.Single(screen.OverCap);
    }

    [Fact]
    public void ExactlyTheCapStillPasses()
    {
        var allowance = TestHarness.ErrorAllowlist.First(a => a.Pattern.Contains("det == 0"));
        var screen = TestHarness.Screen(Enumerable.Repeat(KnownDet, allowance.Max));
        Assert.True(screen.Ok);
        Assert.Equal(allowance.Max, screen.Allowed);
    }

    [Fact]
    public void LogsOwnErrorLinesAreNotEngineErrors()
    {
        // Log writes "ERROR [cat] …" with no colon. Those are the harness's own failures, already
        // counted by the suite that emitted them — screening them again would double-report.
        var screen = TestHarness.Screen(new[] { "ERROR [test] FAIL weapon defs expected=48 actual=47" });
        Assert.Equal(0, screen.Total);
        Assert.True(screen.Ok);
    }

    [Fact]
    public void EveryAllowanceNamesWhyAndHasAFiniteCap()
    {
        Assert.NotEmpty(TestHarness.ErrorAllowlist);
        foreach (var a in TestHarness.ErrorAllowlist)
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Why));
            Assert.InRange(a.Max, 1, 64);
        }
    }
}
