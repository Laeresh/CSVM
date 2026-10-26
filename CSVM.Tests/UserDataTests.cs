using System.IO;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>Portable mode's switch: <c>--portable</c> or a <c>portable.txt</c> in the run's root
/// asks for a <c>userdata/</c> folder there, under <c>.scratch/</c> in a repo run. Neither keeps
/// Godot's user folder.</summary>
public class UserDataTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "csvm-run");

    [Fact]
    public void NeitherFlagNorMarkerKeepsGodotsFolder()
    {
        Assert.Null(UserData.PortableFolder(Root, true, new[] { "--no-pads" }, _ => false));
    }

    [Fact]
    public void TheFlagPutsAnExportedBuildsDataBesideTheExecutable()
    {
        Assert.Equal(Path.Combine(Root, "userdata"), UserData.PortableFolder(Root, true, new[] { "--portable" }, _ => false));
    }

    [Fact]
    public void TheMarkerBesideTheExecutableTurnsItOn()
    {
        string marker = Path.Combine(Root, "portable.txt");
        Assert.Equal(Path.Combine(Root, "userdata"), UserData.PortableFolder(Root, true, [], path => path == marker));
    }

    [Fact]
    public void ARepoRunKeepsItUnderScratch()
    {
        Assert.Equal(Path.Combine(Root, ".scratch", "userdata"), UserData.PortableFolder(Root, false, new[] { "--portable" }, _ => false));
    }
}
