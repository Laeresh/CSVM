using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using CSVM.Mech3.Anim;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The mission setup script's dry run (<see cref="MissionSetup.Switches"/>) on a hand-built gamez. It switches by
/// name, by scope and by area, and only built nodes. It leaves the built world as <see cref="MissionSetup.Apply"/> does.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class MissionSetupTests
{
    /// <summary>The area toggle's rectangle: partition cell 0 alone, which holds the island.</summary>
    internal const string CellZero = "0 100 100 0";

    /// <summary>Each node keeps its last switch: by name in either case, by a scope and its sub-node, by a deleted
    /// tree. A name matching no node and a verb that switches nothing leave no entry.</summary>
    [Fact]
    public void EachNodeKeepsItsLastSwitch()
    {
        var setup = Script(
            "FindNode Base", "NodeSetActive off",
            "FindNode base", "FindSubNode flag", "NodeSetActive off",
            "FindNode flag", "NodeSetActive on",
            "DeleteTree island",
            "FindNode nothing", "NodeSetActive off",
            "FindNode rabaset1", "Object3DTranslate 1 2 3");
        Assert.Equal(new Dictionary<int, bool> { [1] = false, [3] = true, [4] = false }, Sorted(setup.Switches(World())));
    }

    /// <summary>An area toggle switches its cells' nodes once its rectangles are bound, and nothing before.</summary>
    [Fact]
    public void AnAreaToggleSwitchesItsCellsNodesOnceBound()
    {
        var gamez = World();
        var setup = Script($"WorldPartitionSetActive off {CellZero}", $"WorldPartitionSetActive on {CellZero}");
        Assert.Empty(setup.Switches(gamez));
        setup.BindPartitions(gamez);
        Assert.Equal(new Dictionary<int, bool> { [4] = true }, Sorted(setup.Switches(gamez)));
    }

    /// <summary>Only a built node takes a switch. A name resolves among the built nodes, and an area toggle skips the
    /// others. A sub-node under a host never built is not reached.</summary>
    [Fact]
    public void OnlyABuiltNodeTakesASwitch()
    {
        var gamez = World();
        var setup = Script(
            "FindNode base", "NodeSetActive off",
            "FindNode base", "FindSubNode flag", "NodeSetActive off",
            "FindNode rabaset1", "NodeSetActive on",
            $"WorldPartitionSetActive off {CellZero}");
        setup.BindPartitions(gamez);
        Assert.Equal(new Dictionary<int, bool> { [1] = false, [2] = true, [3] = false, [4] = false }, Sorted(setup.Switches(gamez)));
        Assert.Equal(new Dictionary<int, bool> { [2] = true }, Sorted(setup.Switches(gamez, i => i is not (1 or 4))));
    }

    /// <summary>The dry run over the records leaves every built node as <see cref="MissionSetup.Apply"/> leaves it,
    /// through a resolver indexed as the bootstrap indexes the built world. Base is never built.</summary>
    [Fact]
    public void SwitchesAgreesWithApply()
    {
        var gamez = World();
        var setup = Script(
            "FindNode base", "NodeSetActive off",
            "FindNode base", "FindSubNode rabaset1", "NodeSetActive off",
            "FindNode rabaset1.flt", "NodeSetActive off",
            "FindNode flag", "NodeSetActive off",
            "FindNode world1", "FindSubNode flag", "NodeSetActive on",
            "DeleteTree island",
            $"WorldPartitionSetActive on {CellZero}",
            "FindNode ctf_1", "Object3DRotate 0 1 0");
        setup.BindPartitions(gamez);

        // The built world: base was never built, so its children stand under the world root.
        var resolver = new NameResolver<Built>();
        var world = new Built(0);
        resolver.Add(world, "world1", null, 0);
        resolver.Add(new Built(2), "rabaset1", world, 2);
        resolver.Add(new Built(3), "flag", world, 3);
        var island = new Built(4);
        resolver.Add(island, "island", world, 4);
        resolver.Add(new Built(5), "ctf_1", island, 5);
        var applied = new Dictionary<int, bool>();
        setup.Apply<Built>(resolver.FindAll, (n, on) => applied[n.Index] = on, (_, _) => { }, (_, _) => { },
            (i, on) =>
            {
                if (resolver.ByGamezIndex(i) is { } n)
                    applied[n.Index] = on;
            });

        Assert.Equal(new Dictionary<int, bool> { [3] = true, [4] = true }, Sorted(applied));
        Assert.Equal(Sorted(applied), Sorted(setup.Switches(gamez, i => i != 1)));
    }

    /// <summary>The fixture: world1 (0) holds base (1) over rabaset1 (2), a 10 x 4 m floor at the origin, and flag
    /// (3). Partition cell 0 holds island (4) over ctf_1 (5), the same floor 20 m along x.</summary>
    internal static GameZ World()
    {
        var gamez = new GameZ();
        var world = new GameZNode
        {
            Kind = "World", Name = "world1", Index = 0, PartitionRows = 2, PartitionCols = 2,
            PartitionOriginX = 0f, PartitionOriginZ = 100f, PartitionCellX = 50f, PartitionCellZ = 50f,
            PartitionNodes = new List<int> { 4 },
            PartitionCellNodes = new List<List<int>> { new() { 4 }, new(), new(), new() },
        };
        world.Children.Add(1);
        gamez.Nodes.Add(world);
        gamez.Nodes.Add(new GameZNode { Kind = "Object3d", Name = "base", Index = 1, Children = { 2, 3 } });
        gamez.Nodes.Add(new GameZNode { Kind = "Object3d", Name = "rabaset1", Index = 2, MeshIndex = 0 });
        gamez.Nodes.Add(new GameZNode { Kind = "Object3d", Name = "flag", Index = 3 });
        gamez.Nodes.Add(new GameZNode { Kind = "Object3d", Name = "island", Index = 4, Children = { 5 } });
        gamez.Nodes.Add(new GameZNode { Kind = "Object3d", Name = "ctf_1", Index = 5, MeshIndex = 1 });
        foreach (float x in new[] { 0f, 20f })
        {
            var floor = new GameZMesh();
            floor.Vertices.AddRange(new[] { new Vector3(x, 0f, 0f), new Vector3(x + 10f, 0f, 0f), new Vector3(x + 10f, 0f, 4f), new Vector3(x, 0f, 4f) });
            gamez.Meshes.Add(floor);
        }
        return gamez;
    }

    /// <summary>A mission setup read from <paramref name="lines"/>.</summary>
    internal static MissionSetup Script(params string[] lines) =>
        MissionSetup.Load(new InterpScripts(new List<InterpScript> { new("support\\c9\\mp1.gw", lines) }), "C9", "MP1")!;

    private static SortedDictionary<int, bool> Sorted(IReadOnlyDictionary<int, bool> switches) => new(switches.ToDictionary(s => s.Key, s => s.Value));

    // A built node, compared by reference as the bootstrap's Node3D is.
    private sealed class Built
    {
        public Built(int index) => Index = index;

        public int Index { get; }
    }
}
