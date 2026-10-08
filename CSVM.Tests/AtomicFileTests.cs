using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="AtomicFile"/>, the one write path for player data. The target ends with the new text
/// and nothing beside it. A temp file a killed write left behind does not get in the way, and a
/// write that fails leaves the previous content whole.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class AtomicFileTests
{
    [Fact]
    public void AWriteReplacesTheTargetAndLeavesNoTempFile()
    {
        string dir = TestData.TempDir();
        string path = Path.Combine(dir, "save.json");

        AtomicFile.WriteAllText(path, "first");
        Assert.Equal("first", File.ReadAllText(path));

        AtomicFile.WriteAllText(path, "second ü");
        Assert.Equal("second ü", File.ReadAllText(path));
        Assert.NotEqual(0xEF, File.ReadAllBytes(path)[0]);
        Assert.Equal(new[] { "save.json" }, Directory.GetFiles(dir).Select(Path.GetFileName));
    }

    [Fact]
    public void ATempFileAKilledWriteLeftDoesNotBreakTheNextWrite()
    {
        string dir = TestData.TempDir();
        string path = Path.Combine(dir, "save.json");
        File.WriteAllText(path, "old");
        File.WriteAllText(path + ".tmp", "half of a longer save that was never fini");

        AtomicFile.WriteAllText(path, "new");

        Assert.Equal("new", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void AFailedWriteLeavesThePreviousContent()
    {
        string dir = TestData.TempDir();
        string path = Path.Combine(dir, "save.json");
        File.WriteAllText(path, "old");

        // A directory where the temp file goes makes the first step fail before the target is touched.
        Directory.CreateDirectory(path + ".tmp");

        Assert.ThrowsAny<System.Exception>(() => AtomicFile.WriteAllText(path, "new"));
        Assert.Equal("old", File.ReadAllText(path));
    }

    /// <summary>A store answers defaults when its read fails, so saving them would erase the file it
    /// could not read. The write is skipped until a read of the path succeeds again.</summary>
    [Fact]
    public void AFileWhoseReadFailedIsNotWrittenUntilARereadSucceeds()
    {
        string path = Path.Combine(TestData.TempDir(), "save.json");
        File.WriteAllText(path, "the player's");
        var lines = new List<string>();

        using (Log.PushConsoleSink(lines.Add))
        {
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.Null(AtomicFile.ReadAllText(path));
            }

            AtomicFile.WriteAllText(path, "defaults");
            Assert.Equal("the player's", File.ReadAllText(path));

            Assert.Equal("the player's", AtomicFile.ReadAllText(path));
            AtomicFile.WriteAllText(path, "edited");
        }

        Assert.Equal("edited", File.ReadAllText(path));
        Assert.Contains(lines, l => l.Contains("unreadable") && l.Contains(path));
        Assert.Contains(lines, l => l.Contains("not saved") && l.Contains(path));
    }

    [Fact]
    public void ASetAsideFileReplacesAnOlderBadCopyAndFreesThePath()
    {
        string path = Path.Combine(TestData.TempDir(), "save.json");
        File.WriteAllText(path + ".bad", "older");
        File.WriteAllText(path, "{ broken");

        AtomicFile.SetAside(path, "test");
        AtomicFile.WriteAllText(path, "fresh");

        Assert.Equal("{ broken", File.ReadAllText(path + ".bad"));
        Assert.Equal("fresh", File.ReadAllText(path));
    }
}
