using System;
using System.IO;
using CSVM.Launch;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The headset launch's shader cache sweep over a scratch user directory. It deletes what was
/// written since the launch's process started, and the flat launches' older files stay. The end
/// is once only. A marker a dead launch left is swept by the next boot, while a live one's is not.
/// Every file time is set by hand, so nothing here waits on a clock.
/// </summary>
[Trait("Tier", "Quick")]
public class XrShaderCacheTests
{
    private static readonly DateTime Start = new(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TheEndSweepsWhatTheLaunchWroteAndKeepsTheFlatLaunchesFiles()
    {
        string user = TestData.TempDir();
        string flat = Cache(user, "SceneForwardClusteredShaderRD/aa/flat.vulkan.cache", Start.AddHours(-1));
        string stereo = Cache(user, "VrsShaderRD/bb/stereo.vulkan.cache", Start.AddSeconds(2));
        string atStart = Cache(user, "CopyShaderRD/cc/first.vulkan.cache", Start);
        string other = Cache(user, "VrsShaderRD/bb/notes.txt", Start.AddSeconds(3));

        var watch = XrShaderCache.Begin(user, 4242, Start);
        Assert.True(File.Exists(Path.Combine(user, XrShaderCache.MarkerName)));
        Assert.Equal(2, watch.End());

        Assert.True(File.Exists(flat));
        Assert.False(File.Exists(stereo));
        Assert.False(File.Exists(atStart));
        Assert.True(File.Exists(other));
        Assert.False(File.Exists(Path.Combine(user, XrShaderCache.MarkerName)));

        // Once only: what a restarted child writes after the sweep is never the parent's to take.
        string child = Cache(user, "VrsShaderRD/bb/child.vulkan.cache", Start.AddMinutes(1));
        Assert.Equal(0, watch.End());
        Assert.True(File.Exists(child));
    }

    [Fact]
    public void AHeadsetBootOverADeadHeadsetLaunchsMarkerSweepsItFirst()
    {
        string user = TestData.TempDir();
        string flat = Cache(user, "A/x/flat.vulkan.cache", Start.AddMinutes(-5));
        string crashed = Cache(user, "A/x/crashed.vulkan.cache", Start.AddMinutes(5));
        XrShaderCache.Begin(user, 4242, Start);
        var restart = Start.AddHours(1);

        var watch = XrShaderCache.Boot(user, stereo: true, 5151, restart, _ => null, out int? recovered);

        Assert.Equal(1, recovered);
        Assert.True(File.Exists(flat));
        Assert.False(File.Exists(crashed));
        Assert.Equal($"5151 {restart.Ticks}", File.ReadAllText(Path.Combine(user, XrShaderCache.MarkerName)));
        Assert.NotNull(watch);
        Assert.Equal(0, watch!.End());
    }

    [Fact]
    public void AFlatBootSweepsTheDeadMarkerAndOpensNoWatch()
    {
        string user = TestData.TempDir();
        string crashed = Cache(user, "A/x/crashed.vulkan.cache", Start.AddMinutes(5));
        XrShaderCache.Begin(user, 4242, Start);

        Assert.Null(XrShaderCache.Boot(user, stereo: false, 5151, Start.AddHours(1), _ => null, out int? recovered));
        Assert.Equal(1, recovered);
        Assert.False(File.Exists(crashed));
        Assert.False(File.Exists(Path.Combine(user, XrShaderCache.MarkerName)));
    }

    [Fact]
    public void ARestartWhoseStartFailedOpensTheWatchAgainSoTheLaunchsOwnEndStillSweeps()
    {
        string user = TestData.TempDir();
        string before = Cache(user, "A/x/before.vulkan.cache", Start.AddSeconds(5));
        var watch = XrShaderCache.Begin(user, 4242, Start);

        Assert.Equal(-1, watch.EndThenStart(() => -1, out int swept));
        Assert.Equal(1, swept);
        Assert.False(File.Exists(before));
        Assert.Equal($"4242 {Start.Ticks}", File.ReadAllText(Path.Combine(user, XrShaderCache.MarkerName)));

        // The launch carries on in the headset and compiles more; its end takes those too.
        string after = Cache(user, "A/x/after.vulkan.cache", Start.AddMinutes(10));
        Assert.Equal(1, watch.End());
        Assert.False(File.Exists(after));
        Assert.False(File.Exists(Path.Combine(user, XrShaderCache.MarkerName)));
    }

    [Fact]
    public void ARestartWhoseStartSucceededLeavesTheChildsFilesAlone()
    {
        string user = TestData.TempDir();
        var watch = XrShaderCache.Begin(user, 4242, Start);
        bool sweptFirst = false;

        Assert.Equal(777, watch.EndThenStart(() =>
        {
            sweptFirst = !File.Exists(Path.Combine(user, XrShaderCache.MarkerName));
            return 777;
        }, out _));
        string child = Cache(user, "A/x/child.vulkan.cache", Start.AddMinutes(10));

        Assert.True(sweptFirst);
        Assert.Equal(0, watch.End());
        Assert.True(File.Exists(child));
        Assert.False(File.Exists(Path.Combine(user, XrShaderCache.MarkerName)));
    }

    [Fact]
    public void ABootSweepsWhatADeadHeadsetLaunchLeftAndDropsItsMarker()
    {
        string user = TestData.TempDir();
        string flat = Cache(user, "A/x/flat.vulkan.cache", Start.AddMinutes(-5));
        string stereo = Cache(user, "A/x/stereo.vulkan.cache", Start.AddMinutes(5));
        XrShaderCache.Begin(user, 4242, Start);

        Assert.Equal(1, XrShaderCache.Recover(user, _ => null));
        Assert.True(File.Exists(flat));
        Assert.False(File.Exists(stereo));
        Assert.False(File.Exists(Path.Combine(user, XrShaderCache.MarkerName)));
        Assert.Null(XrShaderCache.Recover(user, _ => null));
    }

    [Fact]
    public void ABootLeavesALiveHeadsetLaunchAloneButNotAReusedProcessId()
    {
        string user = TestData.TempDir();
        string stereo = Cache(user, "A/x/stereo.vulkan.cache", Start.AddMinutes(5));
        XrShaderCache.Begin(user, 4242, Start);

        Assert.Null(XrShaderCache.Recover(user, pid => pid == 4242 ? Start : null));
        Assert.True(File.Exists(stereo));

        // The same id on a process started at another time is somebody else's.
        Assert.Equal(1, XrShaderCache.Recover(user, _ => Start.AddDays(1)));
        Assert.False(File.Exists(stereo));
    }

    [Fact]
    public void AMalformedMarkerIsDroppedWithoutSweeping()
    {
        string user = TestData.TempDir();
        string stereo = Cache(user, "A/x/stereo.vulkan.cache", Start.AddMinutes(5));
        File.WriteAllText(Path.Combine(user, XrShaderCache.MarkerName), "not a marker");

        Assert.Equal(0, XrShaderCache.Recover(user, _ => null));
        Assert.True(File.Exists(stereo));
        Assert.False(File.Exists(Path.Combine(user, XrShaderCache.MarkerName)));
    }

    [Fact]
    public void ACacheThatDoesNotExistSweepsNothing()
    {
        Assert.Equal(0, XrShaderCache.Sweep(Path.Combine(TestData.TempDir(), "absent"), Start));
    }

    private static string Cache(string user, string relative, DateTime writtenUtc)
    {
        string path = Path.Combine(user, XrShaderCache.CacheFolder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[] { 0x47, 0x44, 0x53, 0x43 });
        File.SetLastWriteTimeUtc(path, writtenUtc);
        return path;
    }
}
