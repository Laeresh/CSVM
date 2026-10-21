using System.Collections.Generic;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The per-axis unit-square test behind the hairline-seam clamp
/// (<see cref="SceneBuilder.UvAxesWithinUnitSquare"/>). The C1B shoreline case is the shape
/// that motivated the axis split: U spans [0,1] across the surf strip while V tiles along
/// the shore, so only U is safe to clamp, a both-axes test rejects the surface
/// outright and leaves the U wrap bleeding the texture's opaque edge into the water.
/// </summary>
public class UvClampTests
{
    [Fact]
    public void BothAxesInsideTheUnitSquareClampBoth()
    {
        var polys = Poly((0f, 0f), (1f, 0f), (1f, 1f));
        Assert.Equal(UvClampAxes.Both, SceneBuilder.UvAxesWithinUnitSquare(polys, 0));
    }

    [Fact]
    public void ShorelineShapeClampsOnlyTheCrossAxis()
    {
        // The C1B surf strip: U 0..1 across the shore, V tiling 0..3.9 along it.
        var polys = Poly((0f, 1.9375f), (1f, 1.9375f), (1f, 0.910f), (0f, 3.914f));
        Assert.Equal(UvClampAxes.U, SceneBuilder.UvAxesWithinUnitSquare(polys, 0));
    }

    [Fact]
    public void TilingUWithUnitVClampsOnlyV()
    {
        var polys = Poly((0f, 0f), (7.5f, 0f), (7.5f, 1f));
        Assert.Equal(UvClampAxes.V, SceneBuilder.UvAxesWithinUnitSquare(polys, 0));
    }

    [Fact]
    public void TilingBothAxesClampsNeither()
    {
        var polys = Poly((0f, 0f), (7.5f, 0f), (7.5f, 3.2f));
        Assert.Equal(UvClampAxes.None, SceneBuilder.UvAxesWithinUnitSquare(polys, 0));
    }

    [Fact]
    public void ExactEdgeValuesStillCountAsInside()
    {
        // The mirrored-fold terrain hits exactly 0.0 and 1.0; only float noise is absorbed.
        var polys = Poly((0f, 0f), (1f, 1f), (1.0000005f, 0.5f));
        Assert.Equal(UvClampAxes.Both, SceneBuilder.UvAxesWithinUnitSquare(polys, 0));
    }

    [Fact]
    public void NoUvsMeansNothingToClamp()
    {
        var polys = new List<GameZPolygon> { new() };
        Assert.Equal(UvClampAxes.None, SceneBuilder.UvAxesWithinUnitSquare(polys, 0));
    }

    [Fact]
    public void ASurfaceArrayFollowsTheSameRule()
    {
        // A model pack surface brings its own UV array, with no gamez polygon behind it.
        Assert.Equal(UvClampAxes.Both, SceneBuilder.UvAxesWithinUnitSquare(new[] { new Vector2(0f, 0f), new Vector2(1f, 1f) }));
        Assert.Equal(UvClampAxes.U, SceneBuilder.UvAxesWithinUnitSquare(new[] { new Vector2(0.5f, 0f), new Vector2(1f, 3.9f) }));
        Assert.Equal(UvClampAxes.None, SceneBuilder.UvAxesWithinUnitSquare(new[] { new Vector2(2f, 0f), new Vector2(0f, -1f) }));
        Assert.Equal(UvClampAxes.None, SceneBuilder.UvAxesWithinUnitSquare(System.Array.Empty<Vector2>()));
        Assert.Equal(UvClampAxes.None, SceneBuilder.UvAxesWithinUnitSquare((IReadOnlyList<Vector2>?)null));
    }

    [Fact]
    public void AScrollingAxisKeepsRepeating()
    {
        Assert.Equal((true, UvClampAxes.None), SceneBuilder.ClampFor(UvClampAxes.Both, Vector2.Zero));
        Assert.Equal((false, UvClampAxes.V), SceneBuilder.ClampFor(UvClampAxes.Both, new Vector2(0.1f, 0f)));
        Assert.Equal((false, UvClampAxes.U), SceneBuilder.ClampFor(UvClampAxes.U, Vector2.Zero));
        Assert.Equal((false, UvClampAxes.None), SceneBuilder.ClampFor(UvClampAxes.None, Vector2.Zero));
    }

    private static List<GameZPolygon> Poly(params (float U, float V)[] uvs)
    {
        var coords = new List<Vector2>();
        foreach (var (u, v) in uvs)
            coords.Add(new Vector2(u, v));
        return new List<GameZPolygon> { new() { UvCoords = coords } };
    }
}
