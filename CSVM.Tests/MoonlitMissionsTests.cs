using System.IO;
using CSVM.Session.World;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The shipped list of moonlit missions (<see cref="MoonlitMissions"/>). It ships empty, names a world
/// as chapter and mission folder in any case, and turns a mistake into a warning rather than a crash.
/// The engine suite <c>moonlit-mission</c> holds what a listed world is lit with.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class MoonlitMissionsTests
{
    private static string ShippedPath => Path.Combine(TestData.RepoRoot, "CSVM", "data", "moonlit_missions.json");

    [Fact]
    public void TheShippedFileReadsCleanAndListsNoWorld()
    {
        var list = MoonlitMissions.Parse(File.ReadAllText(ShippedPath));
        Assert.Empty(list.Warnings);
        Assert.Empty(list.Worlds);
        Assert.False(list.Lists("C1C", "M01"));
    }

    [Fact]
    public void AnEntryNamesOneWorldInAnyCase()
    {
        var list = MoonlitMissions.Parse("{ \"_about\": \"prose\", \"missions\": [\"c1c/m01\", \" C5 / IA1 \"] }");
        Assert.Empty(list.Warnings);
        Assert.True(list.Lists("C1C", "M01"));
        Assert.True(list.Lists("C5", "IA1"));
        Assert.False(list.Lists("C1C", "M02"));
        Assert.False(list.Lists("C1", "M01"));
    }

    [Fact]
    public void AMalformedEntryIsAWarningAndListsNothing()
    {
        var list = MoonlitMissions.Parse("{ \"missions\": [\"C1C\", \"C1C/M01/x\", \"/M01\", 3, \"C2/MP1\"] }");
        Assert.Equal(4, list.Warnings.Count);
        Assert.Equal(new[] { "C2/MP1" }, list.Worlds);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[\"C1C/M01\"]")]
    [InlineData("{ \"missions\": \"C1C/M01\" }")]
    public void AnUnreadableFileListsNoWorld(string json)
    {
        var list = MoonlitMissions.Parse(json);
        Assert.Single(list.Warnings);
        Assert.Empty(list.Worlds);
    }
}
