using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Which floor a painted polygon takes (<see cref="CityGround"/>). A clear one stands its own set, a flagged one the clear
/// layer beneath, else <c>none</c>. Off the 256 m frame, or where two placements disagree, it keeps the paint.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class CityGroundTests
{
    private const string LayoutJson = """
        { "paints": {
            "cblock7": { "streets": [ { "family": "u", "centre": 0.5, "half": 0.07 }, { "family": "v", "centre": 0.494, "half": 0.068 } ],
                         "cell": { "family": ["u", "v"], "origin": [0.5, 0.5] },
                         "sets": [ { "set": "cblock7", "on_street": 0.003 }, { "set": "cblock4", "on_street": 0.001 }, { "set": "none", "on_street": 0.0 } ] },
            "cblock1": { "streets": [ { "family": "u", "centre": 0.502, "half": 0.064 }, { "family": "v", "centre": 0.498, "half": 0.064 } ],
                         "cell": { "family": ["u", "v"], "origin": [0.5, 0.5] },
                         "sets": [ { "set": "cblock1", "on_street": 0.0 }, { "set": "cblock2", "on_street": 0.378 } ] },
            "cblock3": { "streets": [ { "family": "u-v", "centre": 0.5, "half": 0.064 } ], "cell": { "family": ["u-v"], "origin": [0.5] },
                         "sets": [ { "set": "cblock3", "on_street": 0.0 } ] },
            "cblock4": { "streets": [ { "family": "u", "centre": 0.5, "half": 0.07 } ], "cell": { "family": ["u"], "origin": [0.5] } } } }
        """;

    /// <summary>A clear paint stands its own set, a flagged one the clear layer under it, else none. Over a flagged layer
    /// it stands none, and ground in a 45 m frame keeps the paint.</summary>
    [Fact]
    public void EachPaintedPolygonTakesTheSetStandingOnIt()
    {
        var gamez = World(out var polys);
        var found = CityGround.Resolve(gamez, CityGround.Layout.Parse(LayoutJson), out var census);
        var ground = gamez.Nodes[1];
        Assert.Equal(("cblock7", "cblock7"), found[(ground, polys["clear"])]);
        Assert.Equal(("cblock7", "cblock4"), found[(ground, polys["over base"])]);
        Assert.Equal(("cblock7", "none"), found[(ground, polys["bare"])]);
        Assert.Equal(("cblock7", "none"), found[(ground, polys["over flagged"])]);
        Assert.Equal(("cblock1", "cblock2"), found[(ground, polys["over clear cblock2"])]);
        Assert.Equal(("cblock7", (string?)null), found[(ground, polys["45 m"])]);
        Assert.False(found.ContainsKey((ground, polys["base"])));
        Assert.Equal(3, census.OtherFrame);
        Assert.Equal(0, census.TwoFrames);
    }

    /// <summary>A flagged polygon turned a quarter on a street cross takes the set beneath, or none over bare ground. It
    /// reads its floor at world XZ. Turned clear ground, or a turned diagonal paint, keeps the paint.</summary>
    [Fact]
    public void ATurnedOverlayOnACrossTakesTheSetBeneath()
    {
        var gamez = World(out var polys);
        var found = CityGround.Resolve(gamez, CityGround.Layout.Parse(LayoutJson), out var census);
        var ground = gamez.Nodes[1];
        Assert.Equal(("cblock1", "none"), found[(ground, polys["turned bare"])]);
        Assert.Equal(("cblock1", "cblock2"), found[(ground, polys["turned over clear cblock2"])]);
        Assert.Equal(new[] { (ground, polys["turned over clear cblock2"]), (ground, polys["turned bare"]) }.ToHashSet(), census.Turned);
        Assert.Null(found[(ground, polys["turned clear"])].Set);
        Assert.Null(found[(ground, polys["turned diagonal"])].Set);
    }

    /// <summary>Each placement of a mesh takes its own set (#366). A copy off the whole-tile grid keeps the paint. A copy
    /// turned a quarter, its base turned with it, stands the base's set turned. The first placement keeps its sets.</summary>
    [Fact]
    public void EachPlacementOfAMeshTakesItsOwnSet()
    {
        var gamez = World(out var polys);
        gamez.Nodes[0].Children.Add(gamez.Nodes.Count);
        gamez.Nodes.Add(new GameZNode { Kind = "Object3d", Name = "off grid", MeshIndex = 0, Index = 2, Local = new Transform3D(Basis.Identity, new Vector3(100f, 0f, 0f)) });
        gamez.Nodes[0].Children.Add(gamez.Nodes.Count);
        gamez.Nodes.Add(new GameZNode { Kind = "Object3d", Name = "turned", MeshIndex = 0, Index = 3, Local = new Transform3D(Basis.Identity.Rotated(Vector3.Up, Mathf.Pi / 2f), new Vector3(0f, 0f, 8192f)) });
        var found = CityGround.Resolve(gamez, CityGround.Layout.Parse(LayoutJson), out var census);
        var (ground, offGrid, turned) = (gamez.Nodes[1], gamez.Nodes[2], gamez.Nodes[3]);
        Assert.Equal(("cblock7", "cblock7"), found[(ground, polys["clear"])]);
        Assert.Null(found[(offGrid, polys["clear"])].Set);
        Assert.Equal(("cblock7", "cblock4"), found[(ground, polys["over base"])]);
        Assert.Equal(("cblock7", "cblock4"), found[(turned, polys["over base"])]);
        Assert.Contains((turned, polys["over base"]), census.Turned);
        Assert.DoesNotContain((ground, polys["over base"]), census.Turned);
        Assert.Equal(0, census.TwoFrames);
    }

    /// <summary>Placements of one mesh with one floor share a key, one with another floor takes the next, and one that
    /// floors nothing takes none.</summary>
    [Fact]
    public void PlacementsShareAKeyOnlyWhereTheirFloorsAgree()
    {
        var gamez = World(out var polys);
        GameZNode Node(int i) => new() { Kind = "Object3d", Name = $"n{i}", MeshIndex = 0, Index = i };
        var (a, b, c, d) = (Node(10), Node(11), Node(12), Node(13));
        var uv2 = new Dictionary<(GameZNode, GameZPolygon), Vector2>
        {
            [(a, polys["clear"])] = new(0f, 4f), [(b, polys["clear"])] = new(0f, 4f),
            [(c, polys["clear"])] = new(4f, -3f), [(d, polys["clear"])] = Vector2.Zero,
        };
        var keys = CityGround.Keys(gamez, uv2);
        Assert.Equal((1, 1, 2), (keys[a], keys[b], keys[c]));
        Assert.False(keys.ContainsKey(d));
    }

    /// <summary>The frame test holds for whole UVs on any whole tile and fails a quarter tile off.</summary>
    [Fact]
    public void TheFrameIsOne256MetreRepeatOnWholeTiles()
    {
        var mesh = new GameZMesh();
        var poly = Quad(mesh, 0, 512f, 0f, 256f, noClutter: false, uvOffset: new Vector2(-7f, 3f));
        Assert.True(CityGround.InFrame(mesh, poly, Transform3D.Identity));
        Assert.True(CityGround.InFrame(mesh, poly, new Transform3D(Basis.Identity, new Vector3(-2048f, 5f, 768f))));
        Assert.False(CityGround.InFrame(mesh, poly, new Transform3D(Basis.Identity, new Vector3(64f, 0f, 0f))));
        Assert.False(CityGround.InFrame(mesh, poly, new Transform3D(Basis.Identity.Rotated(Vector3.Up, Mathf.Pi / 2f), Vector3.Zero)));
        // A quarter turn puts u along -Z and v along +X: the fourth frame.
        Assert.True(CityGround.InFrame(mesh, poly, new Transform3D(Basis.Identity.Rotated(Vector3.Up, Mathf.Pi / 2f), Vector3.Zero), 3));
        Assert.False(CityGround.InFrame(mesh, poly, new Transform3D(Basis.Identity.Rotated(Vector3.Up, Mathf.Pi / 2f), Vector3.Zero), 1));
    }

    /// <summary>A hand-built tile is a flagged cblock polygon on a node with a mesh below it on the nearest LOD. A bare
    /// flagged tile, one whose only building is a far LOD, a clear tile with a building and a flagged road are none.
    /// One over a stamping layer keeps its paint.</summary>
    [Fact]
    public void AHandBuiltTileIsFlaggedCityGroundCarryingItsOwnBuildings()
    {
        var gamez = new GameZ();
        var world = new GameZNode { Kind = "World", Name = "world1" };
        gamez.Nodes.Add(world);
        gamez.Materials.Add(new GameZMaterial { TextureName = "cblock1.tif" });
        gamez.Materials.Add(new GameZMaterial { TextureName = "road.tif" });
        var building = new GameZMesh();
        building.Vertices.Add(Vector3.Zero);
        gamez.Meshes.Add(building);

        // A tile at x0 on a node of its own, with a building under a LOD whose near edge is lodMin, or none.
        (GameZNode, GameZPolygon) Tile(float x0, int material, bool noClutter, float? lodMin)
        {
            var mesh = new GameZMesh();
            var poly = Quad(mesh, material, x0, 5f, 256f, noClutter, Vector2.Zero);
            gamez.Meshes.Add(mesh);
            var node = new GameZNode { Kind = "Object3d", Name = $"tile{x0}", MeshIndex = gamez.Meshes.Count - 1, Index = gamez.Nodes.Count };
            world.Children.Add(gamez.Nodes.Count);
            gamez.Nodes.Add(node);
            if (lodMin is not { } min)
                return (node, poly);
            node.Children.Add(gamez.Nodes.Count);
            var lod = new GameZNode { Kind = "Lod", Name = "l3", LodRangeMin = min, Index = gamez.Nodes.Count };
            gamez.Nodes.Add(lod);
            lod.Children.Add(gamez.Nodes.Count);
            gamez.Nodes.Add(new GameZNode { Kind = "Object3d", Name = "g55", MeshIndex = 0, Index = gamez.Nodes.Count });
            return (node, poly);
        }

        Tile(0f, 0, noClutter: false, lodMin: null);                 // the clear ground the stamps stand on
        var overStamps = Tile(0f, 0, noClutter: true, lodMin: 0f);   // the hand-built tile over it
        var bare = Tile(512f, 0, noClutter: true, lodMin: null);
        Tile(1024f, 0, noClutter: true, lodMin: 500f);
        Tile(1536f, 0, noClutter: false, lodMin: 0f);
        Tile(2048f, 1, noClutter: true, lodMin: 0f);
        var alone = Tile(2560f, 0, noClutter: true, lodMin: 0f);     // hand-built with nothing beneath

        var tiles = CityGround.HandBuilt(gamez);
        Assert.Equal(12, tiles.Count);
        Assert.True(CityGround.OnHandBuilt(tiles, new Vector3(100f, 5f, 200f)));
        foreach (float x in new[] { 300f, 600f, 1100f, 1600f, 2100f })
            Assert.False(CityGround.OnHandBuilt(tiles, new Vector3(x, 5f, 100f)));

        // Over a stamping layer the tile keeps its paint; with nothing beneath it floors bare, as any flagged tile.
        var found = CityGround.Resolve(gamez, CityGround.Layout.Parse(LayoutJson), out var census);
        Assert.Null(found[overStamps].Set);
        Assert.Equal(("cblock1", "none"), found[bare]);
        Assert.Equal(("cblock1", "none"), found[alone]);
        Assert.Equal(new[] { overStamps }, census.HandBuilt);
        Assert.Equal(0, census.OtherFrame);
    }

    /// <summary>The shipped layout parses: cblock7's two streets and its square cell, and no floor for a base paint.</summary>
    [Fact]
    public void TheShippedLayoutParses()
    {
        var layout = CityGround.Layout.Parse(File.ReadAllText(Path.Combine(TestData.RepoRoot, "CSVM", "data", "city_models", "C5", "ground_layout.json")));
        var paint = layout.Paints["cblock7"];
        Assert.Equal(new[] { new Vector2(1f, 0f), new Vector2(0f, 1f) }, paint.Streets.Select(s => s.Axis));
        Assert.Equal(0.07031f, paint.Streets[0].Half, 5);
        Assert.Equal(new[] { 0.5f, 0.5f }, paint.Cells.Select(c => c.Origin));
        Assert.Contains(paint.Sets, s => s.Set == "none");
        Assert.Equal(new Vector2(1f, -1f), layout.Paints["cblock3"].Streets[0].Axis);
        Assert.False(layout.Paints.ContainsKey("cblock4"));
    }

    /// <summary>The floor index names each combination's files in layer order.</summary>
    [Fact]
    public void TheIndexNamesEachCombinationsFiles()
    {
        var files = CityGround.Files("""{ "combinations": { "cblock7-cblock7": { "files": ["a-0.png.bin", "a-1.png.bin"] }, "cblock7-none": { "variants": 0 } } }""");
        Assert.Equal(new[] { "a-0.png.bin", "a-1.png.bin" }, files["cblock7-cblock7"]);
        Assert.False(files.ContainsKey("cblock7-none"));
        Assert.Equal("cblock7", CityGround.Stem("CBLOCK7.tif"));
    }

    // One world node over one mesh of 256 m tiles in the U+X V+Z frame, each overlay 2 cm above what it covers.
    private static GameZ World(out Dictionary<string, GameZPolygon> polys)
    {
        var gamez = new GameZ();
        var world = new GameZNode { Kind = "World", Name = "world1" };
        world.Children.Add(1);
        gamez.Nodes.Add(world);
        gamez.Nodes.Add(new GameZNode { Kind = "Object3d", Name = "ground", MeshIndex = 0, Index = 1 });
        foreach (string t in new[] { "cblock7.tif", "cblock4.tif", "cblock1.tif", "cblock2.tif", "cblock3.tif" })
            gamez.Materials.Add(new GameZMaterial { TextureName = t });
        var mesh = new GameZMesh();
        polys = new Dictionary<string, GameZPolygon>
        {
            ["clear"] = Quad(mesh, 0, 0f, 0f, 256f, noClutter: false, uvOffset: new Vector2(3f, -2f)),
            ["base"] = Quad(mesh, 1, 256f, 0f, 256f, noClutter: false, uvOffset: Vector2.Zero),
            ["over base"] = Quad(mesh, 0, 256f, 0.02f, 256f, noClutter: true, uvOffset: Vector2.Zero),
            ["bare"] = Quad(mesh, 0, 1024f, 0f, 256f, noClutter: true, uvOffset: Vector2.Zero),
            ["flagged base"] = Quad(mesh, 1, 1536f, 0f, 256f, noClutter: true, uvOffset: Vector2.Zero),
            ["over flagged"] = Quad(mesh, 0, 1536f, 0.02f, 256f, noClutter: true, uvOffset: Vector2.Zero),
            ["clear cblock2"] = Quad(mesh, 3, 2048f, 0f, 256f, noClutter: false, uvOffset: Vector2.Zero),
            ["over clear cblock2"] = Quad(mesh, 2, 2048f, 0.02f, 256f, noClutter: true, uvOffset: Vector2.Zero),
            ["45 m"] = Quad(mesh, 0, 4096f, 0f, 45f, noClutter: false, uvOffset: Vector2.Zero),
            ["clear cblock2 b"] = Quad(mesh, 3, 2560f, 0f, 256f, noClutter: false, uvOffset: Vector2.Zero),
            ["turned over clear cblock2"] = Quad(mesh, 2, 2560f, 0.02f, 256f, noClutter: true, uvOffset: new Vector2(4f, 1f), turned: true),
            ["turned clear"] = Quad(mesh, 0, 3072f, 0f, 256f, noClutter: false, uvOffset: Vector2.Zero, turned: true),
            ["turned diagonal"] = Quad(mesh, 4, 3584f, 0f, 256f, noClutter: true, uvOffset: Vector2.Zero, turned: true),
            ["turned bare"] = Quad(mesh, 2, 5120f, 0f, 256f, noClutter: true, uvOffset: Vector2.Zero, turned: true),
        };
        gamez.Meshes.Add(mesh);
        return gamez;
    }

    // A 256 m square at x0, its UVs one unit per `metresPerUv` from world zero, shifted by uvOffset. Turned lays U+Z V-X.
    private static GameZPolygon Quad(GameZMesh mesh, int material, float x0, float y, float metresPerUv, bool noClutter, Vector2 uvOffset, bool turned = false)
    {
        int first = mesh.Vertices.Count;
        var corners = new[] { new Vector3(x0, y, 0f), new Vector3(x0 + 256f, y, 0f), new Vector3(x0 + 256f, y, 256f), new Vector3(x0, y, 256f) };
        var poly = new GameZPolygon { MaterialIndex = material, NoClutter = noClutter, UvCoords = new List<Vector2>() };
        foreach (var c in corners)
        {
            mesh.Vertices.Add(c);
            poly.UvCoords.Add(((turned ? new Vector2(c.Z, -c.X) : new Vector2(c.X, c.Z)) / metresPerUv) + uvOffset);
            poly.VertexIndices.Add(first + poly.VertexIndices.Count);
        }
        mesh.Polygons.Add(poly);
        return poly;
    }
}
