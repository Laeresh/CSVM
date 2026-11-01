using System.Collections.Generic;
using System.IO;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="GameZ.ApplyPolygonPatches"/>: a shipped entry raises one polygon's priority. An entry that
/// does not match the install (another texture, a missing node) patches nothing.
/// </summary>
public class PolygonPatchTests
{
    private const string Entry = """{ "C1": [ { "node": "a7", "polygon": 1, "texture": "rail.tif", "priority": 1, "_why": "x" } ] }""";

    [Fact]
    public void AMatchingEntryRaisesThatPolygonAlone()
    {
        var gamez = Build("a7", "ground.tif", "rail.tif");
        var warnings = new List<string>();

        Assert.Equal(1, gamez.ApplyPolygonPatches("c1", Entry, warnings));
        Assert.Empty(warnings);
        Assert.Equal(new[] { 0, 1 }, Priorities(gamez));
    }

    [Theory]
    [InlineData("a7", "ground.tif", "ground.tif")]
    [InlineData("a8", "ground.tif", "rail.tif")]
    public void AnEntryThatDoesNotMatchWarnsAndPatchesNothing(string node, string first, string second)
    {
        var gamez = Build(node, first, second);
        var warnings = new List<string>();

        Assert.Equal(0, gamez.ApplyPolygonPatches("C1", Entry, warnings));
        Assert.Single(warnings);
        Assert.Equal(new[] { 0, 0 }, Priorities(gamez));
    }

    [Fact]
    public void AnotherChapterIsUntouched()
    {
        var gamez = Build("a7", "ground.tif", "rail.tif");
        var warnings = new List<string>();

        Assert.Equal(0, gamez.ApplyPolygonPatches("C2", Entry, warnings));
        Assert.Empty(warnings);
    }

    /// <summary>Every shipped entry finds its polygon in the install it was written against.</summary>
    [GameFilesFact]
    public void EveryShippedEntryMatchesTheInstall()
    {
        string json = File.ReadAllText(Path.Combine(TestData.RepoRoot, "CSVM", "data", "polygon_patches.json"));
        var gamez = CSVM.Spec.GameData.Load(GameFilesData.Source).LoadChapter("C1", "M01").Gamez!;
        var warnings = new List<string>();

        Assert.Equal(3, gamez.ApplyPolygonPatches("C1", json, warnings));
        Assert.Empty(warnings);
    }

    private static GameZ Build(string node, params string[] textures)
    {
        var gamez = new GameZ();
        var mesh = new GameZMesh();
        for (int i = 0; i < textures.Length; i++)
        {
            gamez.Materials.Add(new GameZMaterial { TextureName = textures[i] });
            mesh.Polygons.Add(new GameZPolygon { MaterialIndex = i });
        }

        gamez.Meshes.Add(mesh);
        gamez.Nodes.Add(new GameZNode { Name = node, MeshIndex = 0 });
        return gamez;
    }

    private static int[] Priorities(GameZ gamez) => gamez.Meshes[0].Polygons.ConvertAll(p => p.Priority).ToArray();
}
