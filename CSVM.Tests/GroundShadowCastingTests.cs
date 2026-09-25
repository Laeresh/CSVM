using System.Collections.Generic;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Which world meshes stay out of the Enhanced sun's shadow map
/// (<see cref="WorldBuilder.IsShadowlessGround"/>). A flat sheet that casts shadows itself in
/// bands under a low sun. So the ground and the water must not cast, while buildings and the
/// tiles carrying their walls must.
/// </summary>
public class GroundShadowCastingTests
{
    private const float Cell = 1024f;

    [Fact]
    public void AGroundTileCastsNoShadow()
    {
        Assert.True(Shadowless(Sheet(Cell, Cell), "grass1.tif", "sand2.tif"));
    }

    /// <summary>A coastal tile mixes shore and sea in one mesh, so the tile test covers it, not
    /// the water test.</summary>
    [Fact]
    public void ACoastalTileCastsNoShadow()
    {
        Assert.True(Shadowless(Sheet(Cell, Cell), "sand2.tif", "water1.tif"));
    }

    /// <summary>Water smaller than a tile (a river, a completion strip) is still flat water.</summary>
    [Fact]
    public void AWaterSheetOfAnySizeCastsNoShadow()
    {
        Assert.True(Shadowless(Sheet(200f, 300f), "water1.tif"));
    }

    /// <summary>C1's airfield tile carries a hangar's walls, and C2's city tiles carry block
    /// sides; their shadows are the ones Enhanced exists to draw.</summary>
    [Theory]
    [InlineData("hangar32.tif")]
    [InlineData("bldgside1.tif")]
    public void AGroundTileWithBuildingWallsKeepsCasting(string wall)
    {
        Assert.False(Shadowless(Sheet(Cell, Cell), "grass1.tif", wall));
    }

    /// <summary>⚠ <c>cblock*</c> is the city GROUND texture even though it classifies as
    /// buildings, so a city ground tile must not keep casting on its account.</summary>
    [Fact]
    public void ACityGroundTileCastsNoShadow()
    {
        Assert.True(Shadowless(Sheet(Cell, Cell), "cblock3.tif"));
    }

    [Fact]
    public void APlacedObjectKeepsCasting()
    {
        Assert.False(Shadowless(Sheet(60f, 40f), "rock1.tif"));
    }

    [Fact]
    public void AMeshWithoutPolygonsKeepsCasting()
    {
        Assert.False(WorldBuilder.IsShadowlessGround(new List<Vector3>(), new List<string?>(), Cell, Cell));
    }

    // A flat horizontal quad of the given extents, centred on its own origin.
    private static List<Vector3> Sheet(float x, float z) => new()
    {
        new Vector3(-x / 2f, 0f, -z / 2f),
        new Vector3(x / 2f, 0f, -z / 2f),
        new Vector3(x / 2f, 0f, z / 2f),
        new Vector3(-x / 2f, 0f, z / 2f),
    };

    private static bool Shadowless(List<Vector3> vertices, params string?[] textures) =>
        WorldBuilder.IsShadowlessGround(vertices, textures, Cell, Cell);
}
