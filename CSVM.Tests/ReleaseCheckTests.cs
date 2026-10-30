using System.IO;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The release check's tag comparison and its reading of GitHub's answer, pure and offline. Also
/// where an exported build finds the config file that turns the check off. The request itself is
/// never sent from a test.
/// </summary>
[Trait("Tier", "Quick")]
public class ReleaseCheckTests
{
    [Theory]
    [InlineData("v0.5.0", "0.4.0", true)]
    [InlineData("v0.4.0", "0.4.0", false)]
    [InlineData("v0.4.1", "0.4.0", true)]
    [InlineData("v0.3.9", "0.4.0", false)]
    [InlineData("v0.4.0", "0.4", false)]
    [InlineData("v0.4", "0.4.0", false)]
    [InlineData("v0.4.1", "0.4", true)]
    [InlineData("nightly", "0.4.0", false)]
    [InlineData("v0.5.0-rc.1", "0.4.0", false)]
    [InlineData("v0.5.0", BuildVersion.Unknown, false)]
    public void ATagIsNewerOnlyWhenBothSidesParseAndItIsAbove(string tag, string current, bool newer) =>
        Assert.Equal(newer, ReleaseCheck.IsNewer(tag, current));

    [Fact]
    public void AnAnswerParsesToTheTagAndThisReposReleasePage()
    {
        var release = ReleaseCheck.ParseAnswer("""{"tag_name":"v0.5.0","html_url":"https://github.com/Laeresh/CSVM/releases/tag/v0.5.0"}""");
        Assert.Equal(new Release("v0.5.0", "https://github.com/Laeresh/CSVM/releases/tag/v0.5.0"), release);
    }

    [Theory]
    [InlineData("""{"tag_name":"v0.5.0","html_url":"https://example.com/Laeresh/CSVM/releases/tag/v0.5.0"}""")]
    [InlineData("""{"tag_name":"v0.5.0"}""")]
    [InlineData("""{"message":"API rate limit exceeded"}""")]
    [InlineData("[]")]
    public void AnAnswerWithoutTheTagOrWithAForeignPageParsesToNothing(string json) =>
        Assert.Null(ReleaseCheck.ParseAnswer(json));

    [Fact]
    public void AnExportedBuildReadsConfigBesideItsExecutable()
    {
        string exe = Path.Combine(Path.GetTempPath(), "CSVM-export", "CSVM.exe");
        Assert.Equal(
            Path.Combine(Path.GetTempPath(), "CSVM-export", "config.json"),
            Config.ExportedPath(Config.DefaultResPath, exe));
    }
}
