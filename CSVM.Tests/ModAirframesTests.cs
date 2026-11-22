using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Mech3;
using Godot;
using Xunit;
using Part = CSVM.Mech3.ModAirframes.Part;

namespace CSVM.Tests;

/// <summary>
/// A plane mod's node tree grafted into the aircraft archive (<see cref="ModAirframes.Graft"/>). Each
/// glTF node is a gamez node under the mod's root, with its triangles. An export's lone airframe root is
/// dropped, and a stock cockpit is copied in when the file holds none. Each test grafts into its own archive.
/// </summary>
public class ModAirframesTests
{
    private static readonly Transform3D Moved = new(Basis.Identity, new Vector3(1f, 2f, 3f));

    [Fact]
    public void EveryFileNodeBecomesAGamezNodeUnderTheModRoot()
    {
        var planes = Donor();
        int before = planes.Nodes.Count;
        var parts = new List<Part>
        {
            Node("geometry", children: new[] { 1 }),
            Node("l_aileron1", local: Moved, triangle: true),
            Node("firepoint1"),
        };
        ModAirframes.Graft(planes, "testmod", "player_brigand", parts, new[] { 0, 2 });

        var root = planes.FindByName("testmod")!;
        Assert.Equal(before, root.Index);
        Assert.Equal(new[] { "geometry", "firepoint1", "cockpit1" }, planes.ChildrenOf(root).Select(n => n.Name));
        var aileron = planes.ChildrenOf(planes.ChildrenOf(root)[0]).Single();
        Assert.Equal("l_aileron1", aileron.Name);
        Assert.Equal(Moved, aileron.Local);
        var polygon = planes.Meshes[aileron.MeshIndex].Polygons.Single();
        Assert.Equal(new[] { 2, 1, 0 }, polygon.VertexIndices);
        Assert.Null(planes.Materials[polygon.MaterialIndex].TextureName);
        Assert.Equal(-1, planes.ChildrenOf(root)[1].MeshIndex);
        // The parent map is rebuilt over the grown table.
        Assert.Same(root, planes.ParentOf(planes.ChildrenOf(root)[0]));
    }

    [Fact]
    public void AnExportsLoneAirframeRootStandsForTheModRoot()
    {
        var planes = Donor();
        var parts = new List<Part> { Node("player_brigand", local: Moved, children: new[] { 1 }), Node("geometry") };
        ModAirframes.Graft(planes, "testmod", "player_brigand", parts, new[] { 0 });

        var root = planes.FindByName("testmod")!;
        Assert.Null(root.Local);
        Assert.Equal("geometry", planes.ChildrenOf(root)[0].Name);
    }

    [Fact]
    public void TheDonorCockpitIsCopiedOnlyWhenTheFileHasNone()
    {
        var planes = Donor();
        var donorInterior = planes.ChildrenOf(planes.FindByName("player_brigand")!).Single();
        ModAirframes.Graft(planes, "copied", "player_brigand", new List<Part> { Node("geometry") }, new[] { 0 });
        var copied = planes.ChildrenOf(planes.FindByName("copied")!).Single(n => n.Name == "cockpit1");
        Assert.NotSame(donorInterior, copied);
        Assert.Equal(planes.ChildrenOf(donorInterior).Single().MeshIndex, planes.ChildrenOf(copied).Single().MeshIndex);
        // The stock interior keeps its own parent.
        Assert.Same(planes.FindByName("player_brigand"), planes.ParentOf(donorInterior));

        ModAirframes.Graft(planes, "own", "player_brigand", new List<Part> { Node("cockpit1") }, new[] { 0 });
        Assert.Single(planes.ChildrenOf(planes.FindByName("own")!));
    }

    [Fact]
    public void ATakenIdIsRefusedAndALoopingFileIsCut()
    {
        var planes = Donor();
        Assert.Throws<InvalidDataException>(() =>
            ModAirframes.Graft(planes, "PLAYER_BRIGAND", "player_brigand", new List<Part>(), Array.Empty<int>()));

        var loop = new List<Part> { Node("a", children: new[] { 1 }), Node("b", children: new[] { 0, 1 }) };
        ModAirframes.Graft(planes, "looped", "player_brigand", loop, new[] { 0 });
        var a = planes.ChildrenOf(planes.FindByName("looped")!)[0];
        var b = planes.ChildrenOf(a).Single();
        Assert.Equal("b", b.Name);
        Assert.Empty(planes.ChildrenOf(b));
    }

    // A stock root holding one cockpit1 with one meshed child, as planes.zbd nests an interior.
    private static GameZ Donor()
    {
        var planes = new GameZ();
        planes.Meshes.Add(new GameZMesh());
        planes.Nodes.Add(new GameZNode { Kind = "Object3d", Name = "player_brigand", Index = 0, Children = { 1 } });
        planes.Nodes.Add(new GameZNode { Kind = "Object3d", Name = "cockpit1", Index = 1, Children = { 2 } });
        planes.Nodes.Add(new GameZNode { Kind = "Object3d", Name = "gauges", Index = 2, MeshIndex = 0 });
        return planes;
    }

    // A file node, its children by index in the test's part list; `triangle` gives it one triangle.
    private static Part Node(string name, Transform3D? local = null, int[]? children = null, bool triangle = false) =>
        new(name, local ?? Transform3D.Identity, children ?? Array.Empty<int>(),
            triangle ? new[] { Vector3.Zero, Vector3.Right, Vector3.Up } : Array.Empty<Vector3>(),
            triangle ? new[] { 2, 1, 0 } : Array.Empty<int>());
}
