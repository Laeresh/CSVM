using CSVM.Tooling;
using Xunit;

namespace CSVM.Tests;

/// <summary>The screenshot folder choice, the pure half of <see cref="CaptureDirector.ShotDir"/>.</summary>
public class CaptureDirectorTests
{
    [Fact]
    public void ARepoRunWritesToTheRepoRootsScreenshotsFolder()
    {
        Assert.Equal(@"Z:\CSVM\Screenshots",
            CaptureDirector.ShotDirFor(@"Z:\CSVM\CSVM\", @"Z:\CSVM\tools\godot", exported: false));
    }

    [Fact]
    public void AnExportedBuildWritesBesideItsExecutableNotInTheFolderAboveIt()
    {
        // res:// resolves to the exe's folder in an export, so the repo rule would climb out of it.
        Assert.Equal(@"C:\Games\CSVM\Screenshots",
            CaptureDirector.ShotDirFor(@"C:\Games\CSVM\", @"C:\Games\CSVM", exported: true));
    }
}
