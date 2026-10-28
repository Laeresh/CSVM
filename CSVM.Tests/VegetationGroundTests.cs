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

    /// <summary>A ray meets the nearest solid triangle ahead from either side. It misses past an edge,
    /// behind its origin, along a face and through a cloud.</summary>
    [Fact]
    public void ARayMeetsTheNearestTriangleAhead()
    {
        // A cloud above the first square builds no collider, so the ray passes it.
        var gamez = World();
        gamez.Materials.Add(new GameZMaterial { TextureName = "cloud1" });
        var sky = new GameZMesh();
        Quad(sky, 4, 0f, 900f, noClutter: false);
        gamez.Meshes.Add(sky);
        gamez.Nodes[0].Children.Add(gamez.Nodes.Count);
        gamez.Nodes.Add(new GameZNode { Kind = "Object3d", Name = "cloudparent", MeshIndex = 1 });
        var tris = VegetationGround.Triangles(gamez);
        Assert.Equal(4 * 2 * 3, tris.Length);
        Near(new Vector3(5f, 50f, 5f), VegetationGround.RayHit(tris, new Vector3(5f, 100f, 5f), Vector3.Down));
        Near(new Vector3(5f, 0f, 5f), VegetationGround.RayHit(tris, new Vector3(5f, -10f, 5f), Vector3.Up));
        Near(new Vector3(8f, 50f, 5f), VegetationGround.RayHit(tris, new Vector3(0f, 58f, 5f), new Vector3(1f, -1f, 0f).Normalized()));
        Assert.Null(VegetationGround.RayHit(tris, new Vector3(15f, 100f, 5f), Vector3.Down));
        Assert.Null(VegetationGround.RayHit(tris, new Vector3(5f, 100f, 5f), Vector3.Up));
        Assert.Null(VegetationGround.RayHit(tris, new Vector3(-5f, 0f, 5f), Vector3.Right));
    }

    /// <summary>The painted triangles are those whose texture is accepted, a flagged overlay included.</summary>
    [Fact]
    public void PaintedTrianglesFollowTheirTextureWhateverTheFlag()
    {
        var tris = VegetationGround.Painted(World(), t => t is "overlay" or "hill");
        Assert.Equal(2 * 2 * 3, tris.Length);
        Assert.Contains(tris, v => v.Y == 50f);
        Assert.Contains(tris, v => v.Y == 0.02f);
        Assert.Empty(VegetationGround.Painted(World(), _ => false));
    }

    /// <summary>A polygon of another texture covers a foot on it or within the reach of it, flagged or not.
    /// It covers from a little below the foot to under the rise above it, and the ground's own textures cover nothing.</summary>
    [Fact]
    public void AnOverlayCoversAFootOnItAndWithinTheReach()
    {
        float reach = VegetationGround.OverlayReachM, rise = VegetationGround.OverlayRiseM;
        var overlays = VegetationGround.OverlaysOn(World(), t => t == "base");
        Assert.True(overlays.Covers(new Vector3(5f, 0f, 5f)));
        Assert.True(overlays.Covers(new Vector3(10f + (reach * 0.9f), 0f, 5f)));
        Assert.True(overlays.Covers(new Vector3(-reach * 0.9f, 0f, 5f)));
        Assert.False(overlays.Covers(new Vector3(10f + (reach * 1.1f), 0f, 5f)));
        Assert.False(overlays.Covers(new Vector3(5f, 0.3f, 5f)));
        Assert.True(overlays.Covers(new Vector3(5f, 50.1f - rise, 5f)));
        Assert.False(overlays.Covers(new Vector3(5f, 49.9f - rise, 5f)));
        Assert.True(overlays.Covers(new Vector3(25f, 0f, 5f)));
        Assert.False(VegetationGround.OverlaysOn(World(), t => t is "base" or "overlay" or "hill" or "bare").Covers(new Vector3(5f, 0f, 5f)));
    }

    /// <summary>The install's three spellings of a ground transition are one, and a rail or road is none.</summary>
    [Fact]
    public void ATransitionIsNamedSo()
    {
        Assert.All(new[] { "terpat02_trans1.tif", "cliff01-trans1.tif", "hilltrans2.tif", "cliff1_watertrans2.t" },
            t => Assert.True(VegetationGround.IsTransition(t)));
        Assert.All(new[] { "gantry_tracks2.tif", "arcroad01.tif", "terpat03.tif" }, t => Assert.False(VegetationGround.IsTransition(t)));
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

    private static void Near(Vector3 expected, Vector3? hit) =>
        Assert.True(hit is { } h && h.DistanceTo(expected) < 1e-3f, $"expected {expected}, hit {hit}");

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
