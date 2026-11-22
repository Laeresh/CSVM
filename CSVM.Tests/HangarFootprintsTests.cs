using System.Linq;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The hangar floors the Rich vegetation keeps off (<see cref="HangarFootprints"/>): which nodes are hangars, their
/// hull and the point test.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class HangarFootprintsTests
{
    /// <summary>A rearm base building and a numbered flag base are hangars; the flag and the rearm node are not.</summary>
    [Fact]
    public void HangarsAreTheRearmAndFlagBases()
    {
        Assert.True(HangarFootprints.IsHangar("rabaset1"));
        Assert.True(HangarFootprints.IsHangar("CTF_2"));
        Assert.False(HangarFootprints.IsHangar("ctf_"));
        Assert.False(HangarFootprints.IsHangar("cs_flag_1"));
        Assert.False(HangarFootprints.IsHangar("rearm_node_1"));
        Assert.False(HangarFootprints.IsHangar("ctf_1x"));
    }

    /// <summary>The hull drops interior and collinear points and runs counter-clockwise.</summary>
    [Fact]
    public void HullKeepsTheOutlineCounterClockwise()
    {
        var hull = HangarFootprints.Hull(new[]
        {
            new Vector2(0, 0), new Vector2(10, 0), new Vector2(5, 0), new Vector2(10, 4), new Vector2(0, 4), new Vector2(3, 2), new Vector2(0, 4),
        });
        Assert.Equal(4, hull.Length);
        float area = 0f;
        for (int i = 0; i < hull.Length; i++)
            area += hull[i].Cross(hull[(i + 1) % hull.Length]);
        Assert.Equal(80f, area, 3);
        Assert.Empty(HangarFootprints.Hull(new[] { new Vector2(0, 0), new Vector2(1, 1), new Vector2(2, 2) }));
    }

    /// <summary>A point inside covers, one outside does not unless the margin reaches it, and a negative margin shrinks it.</summary>
    [Fact]
    public void CoversInsideAndWithinTheMargin()
    {
        var floors = new HangarFootprints(new[] { HangarFootprints.Hull(new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 4), new Vector2(0, 4) }) });
        Assert.True(floors.Covers(5f, 2f));
        Assert.True(floors.Covers(new Vector3(9.9f, 50f, 0.1f)));
        Assert.False(floors.Covers(11f, 2f));
        Assert.True(floors.Covers(11f, 2f, 1.5f));
        Assert.False(floors.Covers(0.05f, 2f, -0.1f));
        Assert.False(HangarFootprints.None.Covers(5f, 2f));
        Assert.Equal(6, floors.Triangles.Count);
    }

    /// <summary>A hangar stands when it and every ancestor are on: a script switch on its group or its island takes it.</summary>
    [Fact]
    public void AHangarStandsWhenItAndEveryAncestorAreOn()
    {
        var gamez = MissionSetupTests.World();
        Assert.Equal((true, true), Standing(HangarFootprints.Of(gamez)));
        Assert.Equal((false, true), Standing(HangarFootprints.Of(gamez, MissionSetupTests.Script("FindNode base", "NodeSetActive off"))));
        Assert.Equal((true, false), Standing(HangarFootprints.Of(gamez, MissionSetupTests.Script("DeleteTree island"))));
        Assert.Equal((true, true), Standing(HangarFootprints.Of(gamez, MissionSetupTests.Script("FindNode base", "NodeSetActive off", "FindNode base", "NodeSetActive on"))));
    }

    /// <summary>With no script switch a node reads its built switch. An area toggle switches on a hangar its build left
    /// off. A hangar never built does not stand.</summary>
    [Fact]
    public void AnAreaToggleStandsAHangarItsBuildLeftOff()
    {
        var gamez = MissionSetupTests.World();
        var built = Enumerable.Range(0, 6).ToDictionary(i => i, i => i != 4);
        Assert.Equal((true, false), Standing(HangarFootprints.Of(gamez, null, built)));
        var setup = MissionSetupTests.Script($"WorldPartitionSetActive on {MissionSetupTests.CellZero}");
        setup.BindPartitions(gamez);
        Assert.Equal((true, true), Standing(HangarFootprints.Of(gamez, setup, built)));
        built.Remove(5);
        Assert.Equal((true, false), Standing(HangarFootprints.Of(gamez, setup, built)));
    }

    /// <summary>A script switch on an ancestor the world never built reaches nothing, as in the bootstrap, so the hangar
    /// under it still stands. Read with no built world, the same switch takes it.</summary>
    [Fact]
    public void ASwitchOnAnAncestorNeverBuiltLeavesTheHangarStanding()
    {
        var gamez = MissionSetupTests.World();
        var setup = MissionSetupTests.Script("FindNode base", "NodeSetActive off");
        var built = Enumerable.Range(0, 6).Where(i => i != 1).ToDictionary(i => i, _ => true);
        Assert.Equal((true, true), Standing(HangarFootprints.Of(gamez, setup, built)));
        Assert.Equal((false, true), Standing(HangarFootprints.Of(gamez, setup)));
    }

    /// <summary>A crown stands only where its reach, its scale times its shape's, misses every floor.</summary>
    [Fact]
    public void ACrownStandsOnlyWhereItsReachMissesEveryFloor()
    {
        var floors = new HangarFootprints(new[] { HangarFootprints.Hull(new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 4), new Vector2(0, 4) }) });
        var at = new Vector3(12f, 30f, 2f);
        var crowns = new[]
        {
            new CanopyClumps.NearCrown(at, 1f, false, Colors.White, 0),
            new CanopyClumps.NearCrown(at, 2f, false, Colors.White, 0),
            new CanopyClumps.NearCrown(at, 1f, false, Colors.White, 1),
        };
        Assert.Equal(new[] { crowns[0] }, CanopyClumps.OffHangars(crowns, new[] { 1.5f, 3f }, floors));
    }

    // Whether the fixture's rabaset1 floor (at the origin) and ctf_1 floor (20 m along x) stand.
    private static (bool Rearm, bool Flag) Standing(HangarFootprints floors) => (floors.Covers(5f, 2f), floors.Covers(25f, 2f));
}
