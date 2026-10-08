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
}
