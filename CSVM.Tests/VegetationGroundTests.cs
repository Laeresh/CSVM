using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The ground under a texture pick (<see cref="VegetationGround"/>). Where coplanar layers are painted
/// over each other, a pick names the layer that takes stamps, never a <c>no_clutter</c> overlay.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class VegetationGroundTests
{
    /// <summary>Every layer over the point is found, nearest in height first.</summary>
    [Fact]
    public void EveryLayerOverThePointIsFoundNearestFirst()
    {
        var layers = VegetationGround.LayersAt(World(), new Vector3(5f, 0.1f, 5f));
        Assert.Equal(new[] { "overlay", "base", "hill" }, layers.ConvertAll(l => l.Texture));
        Assert.True(layers[0].NoClutter);
        Assert.Empty(VegetationGround.LayersAt(World(), new Vector3(15f, 0f, 5f)));
    }

    /// <summary>The flagged overlay gives the pick to the base beneath it, and a flagged layer alone,
    /// ground the original leaves bare, gives none.</summary>
    [Fact]
    public void APickNamesTheLayerThatTakesStamps()
    {
        var gamez = World();
        Assert.Equal("base", VegetationGround.Decorating(VegetationGround.LayersAt(gamez, new Vector3(5f, 0f, 5f)))?.Texture);
        Assert.Equal("hill", VegetationGround.Decorating(VegetationGround.LayersAt(gamez, new Vector3(5f, 49f, 5f)))?.Texture);
        Assert.Null(VegetationGround.Decorating(VegetationGround.LayersAt(gamez, new Vector3(25f, 0f, 5f))));
    }

    // One world node over one mesh. A flagged overlay lies a few centimetres above its base over x in [0, 10].
    // A lone flagged quad covers x in [20, 30], and a hilltop sits 50 m above the first square.
    private static GameZ World()
    {
        var gamez = new GameZ();
        var world = new GameZNode { Kind = "World", Name = "world1" };
        world.Children.Add(1);
        gamez.Nodes.Add(world);
        gamez.Nodes.Add(new GameZNode { Kind = "Object3d", Name = "tile", MeshIndex = 0 });
        foreach (string t in new[] { "overlay", "base", "bare", "hill" })
            gamez.Materials.Add(new GameZMaterial { TextureName = t });
        var mesh = new GameZMesh();
        Quad(mesh, 0, 0f, 0.02f, noClutter: true);
        Quad(mesh, 1, 0f, 0f, noClutter: false);
        Quad(mesh, 2, 20f, 0f, noClutter: true);
        Quad(mesh, 3, 0f, 50f, noClutter: false);
        gamez.Meshes.Add(mesh);
        return gamez;
    }

    private static void Quad(GameZMesh mesh, int material, float x0, float y, bool noClutter)
    {
        int first = mesh.Vertices.Count;
        mesh.Vertices.Add(new Vector3(x0, y, 0f));
        mesh.Vertices.Add(new Vector3(x0 + 10f, y, 0f));
        mesh.Vertices.Add(new Vector3(x0 + 10f, y, 10f));
        mesh.Vertices.Add(new Vector3(x0, y, 10f));
        var poly = new GameZPolygon { MaterialIndex = material, NoClutter = noClutter };
        for (int i = 0; i < 4; i++)
            poly.VertexIndices.Add(first + i);
        mesh.Polygons.Add(poly);
    }
}
