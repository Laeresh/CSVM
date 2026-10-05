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

        // Once only: what a relaunched child writes after the sweep is never the parent's to take.
        string child = Cache(user, "VrsShaderRD/bb/child.vulkan.cache", Start.AddMinutes(1));
        Assert.Equal(0, watch.End());
        Assert.True(File.Exists(child));
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
