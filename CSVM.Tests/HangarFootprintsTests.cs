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
}
