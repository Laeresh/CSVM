using CSVM.Flight.Camera;
using CSVM.Launch;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The engine-free halves of the OpenXR boot path: whether a launch asked for OpenXR
/// (<see cref="XrLaunch.Requested"/>), where the headset's origin sits (<see cref="XrMount"/>).
/// Also what the once-a-second pacing line reports (<see cref="XrFrameWindow"/>).
/// </summary>
public class XrBootTests
{
    private const string Exe = @"Z:\CSVM\tools\godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe";

    [Fact]
    public void AFlatLaunchFollowsTheProjectSetting()
    {
        string[] args = { Exe, "--path", "CSVM", "res://scenes/Main.tscn", "--", "--stage=empty" };
        Assert.False(XrLaunch.Requested(args, projectEnabled: false));
        Assert.True(XrLaunch.Requested(args, projectEnabled: true));
    }

    [Fact]
    public void XrModeOnBeforeTheSeparatorRequestsOpenXr()
    {
        string[] args = { Exe, "--path", "CSVM", "--xr-mode", "on", "res://scenes/Main.tscn", "--", "--stage=empty" };
        Assert.True(XrLaunch.Requested(args, projectEnabled: false));
    }

    [Fact]
    public void XrModeAfterTheSeparatorIsAUserArgumentGodotIgnores()
    {
        string[] args = { Exe, "--path", "CSVM", "res://scenes/Main.tscn", "--", "--xr-mode", "on" };
        Assert.False(XrLaunch.Requested(args, projectEnabled: false));
    }

    [Fact]
    public void TheLastXrModeWinsAndDefaultDefersToTheSetting()
    {
        Assert.False(XrLaunch.Requested(new[] { Exe, "--xr-mode", "on", "--xr-mode", "off" }, projectEnabled: true));
        Assert.True(XrLaunch.Requested(new[] { Exe, "--xr-mode", "ON" }, projectEnabled: false));
        Assert.True(XrLaunch.Requested(new[] { Exe, "--xr-mode", "default" }, projectEnabled: true));
        Assert.False(XrLaunch.Requested(new[] { Exe, "--xr-mode" }, projectEnabled: false));
    }

    [Fact]
    public void AFirstPersonMountTakesTheEyeAtTheAircraftsAttitude()
    {
        var attitude = new Basis(Vector3.Up, 0.7f) * new Basis(Vector3.Right, 0.2f) * new Basis(Vector3.Back, 0.3f);
        // The flat eye carries a pitched head; the mount must not.
        var eye = new Transform3D(attitude * new Basis(Vector3.Right, -0.08203f), new Vector3(100f, 50f, -20f));
        var mount = XrMount.For(eye, attitude);
        Assert.True(mount.Origin.IsEqualApprox(eye.Origin));
        Assert.True(mount.Basis.IsEqualApprox(attitude));
    }

    [Fact]
    public void AnyOtherViewKeepsTheEyeWithItsHeadingOnly()
    {
        var eye = new Transform3D(new Basis(Vector3.Up, 1.1f) * new Basis(Vector3.Right, -0.4f) * new Basis(Vector3.Back, 0.5f),
            new Vector3(3f, 4f, 5f));
        var mount = XrMount.For(eye, null);
        Assert.True(mount.Origin.IsEqualApprox(eye.Origin));
        Assert.True(mount.Basis.Y.IsEqualApprox(Vector3.Up));
        Assert.True(mount.Basis.IsEqualApprox(new Basis(Vector3.Up, 1.1f)));
    }

    [Fact]
    public void AnEyeLookingStraightDownTakesItsHeadingFromItsUpAxis()
    {
        // Facing +X, then pitched to look straight down: the top of the view points along +X.
        var down = new Basis(Vector3.Up, -Mathf.Pi / 2f) * new Basis(Vector3.Right, -Mathf.Pi / 2f);
        var level = XrMount.Level(down);
        Assert.True((-level.Z).IsEqualApprox(Vector3.Right));
    }

    [Fact]
    public void ASteadyHeadsetRateHasNoLateFrames()
    {
        var window = new XrFrameWindow();
        window.SetRefreshHz(90.0);
        bool closed = false;
        for (int i = 0; i < 90 && !closed; i++)
        {
            closed = window.Add(1000.0 / 90.0 + 0.001);
        }

        Assert.True(closed);
        Assert.Equal(90, window.Last.Frames);
        Assert.Equal(0, window.Last.Late);
        Assert.Equal(0, window.Last.Missed);
        Assert.Equal(1000.0 / 90.0, window.Last.MeanMs, 2);
    }

    [Fact]
    public void ALongIntervalCountsTheRefreshesItSkipped()
    {
        var window = new XrFrameWindow(windowSeconds: 0.1);
        window.SetRefreshHz(90.0);
        double period = 1000.0 / 90.0;
        window.Add(period * 2.0);  // one refresh shown twice
        window.Add(period * 3.0);  // two refreshes skipped
        window.Add(period * 1.2);  // jitter, not late
        Assert.True(window.Add(period * 4.0));
        Assert.Equal(4, window.Last.Frames);
        Assert.Equal(3, window.Last.Late);
        Assert.Equal(1 + 2 + 3, window.Last.Missed);
        Assert.Equal(period * 4.0, window.Last.MaxMs, 6);
    }

    [Fact]
    public void AnUnknownRefreshRateCountsNothingLate()
    {
        var window = new XrFrameWindow(windowSeconds: 0.05);
        Assert.True(window.Add(60.0));
        Assert.Equal(0, window.Last.Late);
        Assert.Equal(0, window.Last.Missed);
        Assert.Equal(60.0, window.Last.MaxMs);
    }
}
