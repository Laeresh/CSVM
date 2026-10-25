using System;
using System.IO;
using CSVM.Extraction;
using Xunit;

namespace CSVM.Tests;

public class GameFileSourceTests
{
    private static readonly InstallSearchRoots NoMachine = new(true, null, Array.Empty<string>(), Array.Empty<string>());

    [Fact]
    public void OpenAcceptsOnlyAnInstallLayout()
    {
        string install = MakeInstall(TestData.TempDir());
        Assert.NotNull(GameFileSource.Open(install));
        Assert.Null(GameFileSource.Open(Path.Combine(install, "ZBD")));
        Assert.Null(GameFileSource.Open(Path.Combine(install, "absent")));
    }

    [Fact]
    public void FilesMatchWithoutRegardToCase()
    {
        string install = MakeInstall(TestData.TempDir());
        Directory.CreateDirectory(Path.Combine(install, "ZBD", "c1"));
        File.WriteAllBytes(Path.Combine(install, "ZBD", "c1", "ZRDR.ZBD"), new byte[] { 1, 2, 3 });
        var source = GameFileSource.Open(install)!;

        Assert.True(source.Exists("zbd/C1/zrdr.zbd"));
        Assert.True(source.Exists(@"ZBD\c1\ZRDR.zbd"));
        using (var stream = source.Open("Zbd/C1/Zrdr.Zbd")!)
        {
            Assert.True(stream.CanSeek);
            Assert.Equal(3, stream.Length);
        }

        Assert.Equal(new byte[] { 1, 2, 3 }, source.ReadAllBytes("zbd/c1/zrdr.zbd"));
    }

    [Fact]
    public void AnAbsentOrEscapingPathIsNotFound()
    {
        string parent = TestData.TempDir();
        string install = MakeInstall(Path.Combine(parent, "game"));
        File.WriteAllText(Path.Combine(parent, "outside.txt"), "x");
        var source = GameFileSource.Open(install)!;

        Assert.False(source.Exists("ZBD/none.zbd"));
        Assert.Null(source.Open("ZBD/none.zbd"));
        Assert.False(source.Exists("../outside.txt"));
        Assert.False(source.Exists("ZBD"));
        var missing = Assert.Throws<FileNotFoundException>(() => source.ReadAllBytes("ZBD/none.zbd"));
        Assert.Contains(install, missing.Message);
    }

    [Fact]
    public void LocateTakesTheOverrideThenTheDataRootThenTheRemembered()
    {
        string over = MakeInstall(TestData.TempDir());
        string dataRoot = TestData.TempDir();
        string copy = MakeInstall(Path.Combine(dataRoot, GameFileSource.DataRootFolder));
        string remembered = MakeInstall(TestData.TempDir());
        string notInstall = TestData.TempDir();

        Assert.Equal(over, GameFileSource.Locate(over, dataRoot, remembered, NoMachine)!.Location);
        Assert.Equal(copy, GameFileSource.Locate(notInstall, dataRoot, remembered, NoMachine)!.Location);
        Assert.Equal(remembered, GameFileSource.Locate(null, notInstall, remembered, NoMachine)!.Location);
        Assert.Null(GameFileSource.Locate(notInstall, notInstall, notInstall, NoMachine));
        Assert.Null(GameFileSource.Locate(null, null, null, NoMachine));
    }

    // The two folders InstallLocator.IsInstall asks for, in the case an install spells them.
    private static string MakeInstall(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "ZBD"));
        Directory.CreateDirectory(Path.Combine(root, "GOSDATA", "ASSETS"));
        return Path.GetFullPath(root);
    }
}
