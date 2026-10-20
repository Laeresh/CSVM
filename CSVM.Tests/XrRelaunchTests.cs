using System.Collections.Generic;
using System.Linq;
using CSVM.Launch;
using CSVM.Spec;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// VR mode's restart decisions, engine-free. They are which <c>--xr-mode</c> a command line
/// carries, and the child's arguments with the engine flag ahead of <c>--</c>. Then the boot's and
/// the Options apply's restart, the flat-only runs and the menu's notices. None of
/// this starts a process or a runtime. The launcher hands these functions the process's own
/// arguments and acts on the answer.
/// </summary>
[Trait("Tier", "Quick")]
public class XrRelaunchTests
{
    // A dev launch the way RunGame.ps1 builds it: the editor binary's project and scene, then the
    // user arguments behind the separator.
    private static readonly string[] DevLaunch =
    {
        "--path", "Z:\\CSVM\\CSVM", "res://scenes/Main.tscn", "--", "--volume=1.0",
    };

    [Fact]
    public void TheModeIsTheLastXrModeAheadOfTheSeparator()
    {
        Assert.Null(XrRelaunch.Mode(DevLaunch));
        Assert.Null(XrRelaunch.Mode(System.Array.Empty<string>()));
        Assert.True(XrRelaunch.Mode(new[] { "--xr-mode", "on", "--path", "x" }));
        Assert.False(XrRelaunch.Mode(new[] { "--xr-mode", "off" }));
        Assert.Null(XrRelaunch.Mode(new[] { "--xr-mode", "default" }));
        Assert.False(XrRelaunch.Mode(new[] { "--xr-mode", "on", "--xr-mode", "off" }));
        Assert.Null(XrRelaunch.Mode(new[] { "--path", "x", "--", "--xr-mode", "on" }));
    }

    [Fact]
    public void TurningOnPutsTheEngineFlagFirstAndCarriesEveryArgument()
    {
        var child = XrRelaunch.Arguments(DevLaunch, on: true, skipIntro: false);
        Assert.Equal(new[] { "--xr-mode", "on" }.Concat(DevLaunch), child);
        Assert.True(child.ToList().IndexOf("--xr-mode") < child.ToList().IndexOf("--"));
        Assert.True(XrRelaunch.Mode(child));
    }

    [Fact]
    public void TurningOffDropsEveryEngineXrModeAndLeavesTheUserArgumentsAlone()
    {
        var running = new[] { "--xr-mode", "on", "--path", "p", "--xr-mode", "on", "--", "--xr-mode", "x" };
        var child = XrRelaunch.Arguments(running, on: false, skipIntro: false);
        Assert.Equal(new[] { "--path", "p", "--", "--xr-mode", "x" }, child);
        Assert.Null(XrRelaunch.Mode(child));
    }

    [Fact]
    public void AMenuRelaunchSkipsTheIntroOnceWithASeparatorWhereNoneStood()
    {
        Assert.Equal(new[] { "--xr-mode", "on", "--", "--skip-intro" },
            XrRelaunch.Arguments(System.Array.Empty<string>(), on: true, skipIntro: true));
        Assert.Equal(DevLaunch.Concat(new[] { "--skip-intro" }),
            XrRelaunch.Arguments(DevLaunch, on: false, skipIntro: true));
        var skipped = DevLaunch.Concat(new[] { "--skip-intro" }).ToArray();
        Assert.Equal(skipped, XrRelaunch.Arguments(skipped, on: false, skipIntro: true));
    }

    [Fact]
    public void TheProgramNameIsDropped()
    {
        Assert.Equal(DevLaunch, XrRelaunch.WithoutProgram(new[] { "Godot.exe" }.Concat(DevLaunch).ToArray()));
        Assert.Empty(XrRelaunch.WithoutProgram(new[] { "CSVM.exe" }));
        Assert.Empty(XrRelaunch.WithoutProgram(System.Array.Empty<string>()));
    }

    [Fact]
    public void ABootRelaunchesOnceForASavedOnAndNeverInALoop()
    {
        Assert.True(XrRelaunch.AtBoot(saved: true, mode: null, flatOnly: null));
        Assert.False(XrRelaunch.AtBoot(saved: null, mode: null, flatOnly: null));
        Assert.False(XrRelaunch.AtBoot(saved: false, mode: null, flatOnly: null));
        Assert.False(XrRelaunch.AtBoot(saved: true, mode: false, flatOnly: null));
        Assert.False(XrRelaunch.AtBoot(saved: true, mode: null, flatOnly: "--det"));

        // The child carries the flag, so its own boot does not restart again. A child whose
        // runtime failed stays put rather than retrying.
        var child = XrRelaunch.Arguments(System.Array.Empty<string>(), on: true, skipIntro: false);
        Assert.False(XrRelaunch.AtBoot(saved: true, mode: XrRelaunch.Mode(child), flatOnly: null));
    }

    [Theory]
    [InlineData(null, true, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(true, false, true, false)]
    [InlineData(true, null, true, false)]
    [InlineData(true, true, false, null)]
    [InlineData(null, null, false, null)]
    [InlineData(false, true, true, null)]
    [InlineData(true, false, false, null)]
    public void AnApplyRelaunchesOnlyForAChangeThisLaunchDoesNotAlreadyRun(
        bool? before, bool? chosen, bool requested, bool? expected)
    {
        Assert.Equal(expected, XrRelaunch.OnApply(before, chosen, requested, flatOnly: null));
    }

    [Fact]
    public void AnApplyInAFlatOnlyRunNeverRelaunches()
    {
        Assert.Null(XrRelaunch.OnApply(before: false, chosen: true, requested: false, flatOnly: "--det"));
    }

    [Theory]
    [InlineData("--det")]
    [InlineData("--run-tests")]
    [InlineData("--run-tests=menu")]
    [InlineData("--screenshot=.scratch/x.png")]
    [InlineData("--dump-weapons")]
    [InlineData("--dump-flight")]
    [InlineData("--viewer")]
    [InlineData("--freecam")]
    [InlineData("--anim-lab")]
    [InlineData("--weapon-lab")]
    [InlineData("--xr-sim")]
    [InlineData("--net-host=127.0.0.1:7777")]
    public void TheDevRunsAreFlatOnly(string flag)
    {
        var spec = SessionSpec.Parse(new[] { flag });
        Assert.NotNull(XrRelaunch.FlatOnly(spec, headless: false, windows: true));
    }

    [Fact]
    public void APlayersLaunchMayRelaunchOnWindowsAlone()
    {
        foreach (var args in new[] { System.Array.Empty<string>(), new[] { "--volume=1.0" }, new[] { "--skip-intro" }, new[] { "--chapter=C1" } })
        {
            var spec = SessionSpec.Parse(args);
            Assert.Null(XrRelaunch.FlatOnly(spec, headless: false, windows: true));
            Assert.NotNull(XrRelaunch.FlatOnly(spec, headless: true, windows: true));
            Assert.NotNull(XrRelaunch.FlatOnly(spec, headless: false, windows: false));
        }
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void TheFallbackNoticeStandsWhenOpenXrWasAskedForAndIsNotLive(bool requested, bool live, bool notice)
    {
        Assert.Equal(notice, XrRelaunch.FellBack(requested, live));
    }

    [Fact]
    public void AFailedStartPostsItsOwnNoticeAheadOfTheFallback()
    {
        string intoHeadset = XrRelaunch.StartFailedNotice(on: true);
        string outOfHeadset = XrRelaunch.StartFailedNotice(on: false);

        Assert.Equal(intoHeadset, XrRelaunch.MenuNotice(requested: false, openXrLive: false, failedStart: true));
        Assert.Equal(outOfHeadset, XrRelaunch.MenuNotice(requested: true, openXrLive: true, failedStart: false));
        Assert.Equal(intoHeadset, XrRelaunch.MenuNotice(requested: true, openXrLive: false, failedStart: true));
        Assert.Contains("plays flat", intoHeadset, System.StringComparison.Ordinal);
        Assert.Contains("stays in VR", outOfHeadset, System.StringComparison.Ordinal);
        Assert.NotEqual(XrRelaunch.FallbackNotice, intoHeadset);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void WithNoFailedStartTheMenuOwesTheFallbackNoticeAlone(bool requested, bool live, bool notice)
    {
        Assert.Equal(notice ? XrRelaunch.FallbackNotice : null, XrRelaunch.MenuNotice(requested, live, failedStart: null));
    }
}
