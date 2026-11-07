using System.Collections.Generic;
using System.IO;
using CSVM.Mech3;
using Godot;
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

    [Fact]
    public void AUvEntryMovesItsCornersAndKeepsThePriority()
    {
        var gamez = Build("a7", "ground.tif", "rail.tif");
        var warnings = new List<string>();

        Assert.Equal(1, gamez.ApplyPolygonPatches("C1", """{ "C1": [ { "node": "a7", "polygon": 1, "texture": "rail.tif", "uv": { "2": [0.25, 0.75] } } ] }""", warnings));
        Assert.Empty(warnings);
        Assert.Equal(new[] { Vector2.Zero, Vector2.Zero, new Vector2(0.25f, 0.75f) }, gamez.Meshes[0].Polygons[1].UvCoords);
        Assert.Equal(new[] { 0, 0 }, Priorities(gamez));
    }

    [Fact]
    public void AUvEntryPastThePolygonsCornersWarnsAndPatchesNothing()
    {
        var gamez = Build("a7", "ground.tif", "rail.tif");
        var warnings = new List<string>();

        Assert.Equal(0, gamez.ApplyPolygonPatches("C1", """{ "C1": [ { "node": "a7", "polygon": 1, "texture": "rail.tif", "uv": { "3": [0.25, 0.75] } } ] }""", warnings));
        Assert.Single(warnings);
        Assert.All(gamez.Meshes[0].Polygons[1].UvCoords!, uv => Assert.Equal(Vector2.Zero, uv));
    }

    /// <summary>Every shipped entry finds its polygon in the install it was written against. The
    /// Devastator's wing fix lands on both copies of its left wing in planes.zbd.</summary>
    [GameFilesFact]
    public void EveryShippedEntryMatchesTheInstall()
    {
        string json = File.ReadAllText(Path.Combine(TestData.RepoRoot, "CSVM", "data", "polygon_patches.json"));
        var data = CSVM.Spec.GameData.Load(GameFilesData.Source);
        var gamez = data.LoadChapter("C1", "M01").Gamez!;
        var warnings = new List<string>();

        Assert.Equal(3, gamez.ApplyPolygonPatches("C1", json, warnings));
        Assert.Equal(2, data.Planes!.ApplyPolygonPatches("planes", json, warnings));
        Assert.Empty(warnings);
    }

    private static GameZ Build(string node, params string[] textures)
    {
        var gamez = new GameZ();
        var mesh = new GameZMesh();
        for (int i = 0; i < textures.Length; i++)
        {
            gamez.Materials.Add(new GameZMaterial { TextureName = textures[i] });
            mesh.Polygons.Add(new GameZPolygon { MaterialIndex = i, UvCoords = new List<Vector2> { Vector2.Zero, Vector2.Zero, Vector2.Zero } });
        }

        gamez.Meshes.Add(mesh);
        gamez.Nodes.Add(new GameZNode { Name = node, MeshIndex = 0 });
        return gamez;
    }

    private static int[] Priorities(GameZ gamez) => gamez.Meshes[0].Polygons.ConvertAll(p => p.Priority).ToArray();
}
