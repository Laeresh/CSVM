using System;
using System.IO;
using Xunit;

namespace CSVM.Tests;

/// <summary>Where a test's throwaway input lives and who takes it away again. Every
/// <see cref="TestData.TempDir"/> sits inside one per-run root, and the run that made it deletes
/// the root as the test host exits, so a suite that mints hundreds of them leaves none behind.
/// </summary>
public class TestDataTempDirTests
{
    [Fact]
    public void EveryTempDirSitsInsideThisRunsOwnRoot()
    {
        string first = TestData.TempDir();
        string second = TestData.TempDir();

        Assert.NotEqual(first, second);
        Assert.Equal(TestData.TempRoot, Path.GetDirectoryName(first));
        Assert.Equal(TestData.TempRoot, Path.GetDirectoryName(second));
    }

    [Fact]
    public void ATempDirArrivesEmptyAndOnDisk()
    {
        string dir = TestData.TempDir();

        Assert.True(Directory.Exists(dir));
        Assert.Empty(Directory.GetFileSystemEntries(dir));
    }

    /// <summary>The run root carries this process's id under one shared folder: the id is what
    /// tells a root a killed run abandoned from one a live run is still writing into, and the
    /// shared folder is the single name <c>CleanScratch.ps1</c> sweeps.</summary>
    [Fact]
    public void TheRunRootIsNamedForThisProcessUnderTheSweptFolder()
    {
        string parent = Path.GetDirectoryName(TestData.TempRoot)!;

        Assert.Equal("csvm-tests", Path.GetFileName(parent));
        Assert.Equal(Trimmed(Path.GetTempPath()), Trimmed(Path.GetDirectoryName(parent)!));
        Assert.StartsWith(
            $"run-{Environment.ProcessId}-", Path.GetFileName(TestData.TempRoot), StringComparison.Ordinal);
    }

    [Fact]
    public void ARemovalTakesTheWholeTreeBelowIt()
    {
        string dir = TestData.TempDir();
        Directory.CreateDirectory(Path.Combine(dir, "nested"));
        File.WriteAllText(Path.Combine(dir, "nested", "input.txt"), "x");

        TestData.DeleteDirectory(dir);

        Assert.False(Directory.Exists(dir));
    }

    /// <summary>The removal answers nothing and throws nothing, because it runs from a process-exit
    /// hook: a directory already gone, or one a file handle is still held under, must not fail a
    /// run that is over.</summary>
    [Fact]
    public void RemovingSomethingAlreadyGoneIsNotAFailure()
    {
        string dir = TestData.TempDir();
        TestData.DeleteDirectory(dir);

        TestData.DeleteDirectory(dir);

        Assert.False(Directory.Exists(dir));
    }

    // GetTempPath keeps its trailing separator and GetDirectoryName drops one, so the two
    // spellings of the same folder only compare equal once both are trimmed.
    private static string Trimmed(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
