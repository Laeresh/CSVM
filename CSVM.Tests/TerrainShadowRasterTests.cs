using System;
using System.Collections.Generic;
using CSVM.Effects;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The baked terrain shadow's texels. A wall casts the exact grazing line and blocker distance along
/// the sun's azimuth. A slope toward the sun is lit, a slope away steeper than the sun is shaded.
/// The band count moves no float.
/// </summary>
public class TerrainShadowRasterTests
{
    private static readonly float Cell = TerrainShadowRaster.Cell;

    // The westmost texel centre of a 16 m block standing at x = 0. Its ground is the last to win, so
    // a ray's distance counts from it.
    private static readonly float BlockWest = Cell / 2f;

    // A sun from +X at 30 degrees: the shadow falls toward -X and the ray drops tan(30) per metre.
    private static readonly Vector3 SunEast = new Vector3(MathF.Cos(MathF.PI / 6f), MathF.Sin(MathF.PI / 6f), 0f);

    [Fact]
    public void AWallCastsTheGrazingLineAndItsDistance()
    {
        var tris = new List<Vector3>();
        AddQuad(tris, -1024f, -1024f, 2048f, 0f);
        // A 16 m wide, 100 m tall block across the whole sheet.
        AddQuad(tris, 0f, -1024f, 16f, 100f, 2048f);
        var r = TerrainShadowRaster.Run(tris.ToArray(), SunEast, 3);
        float tan = MathF.Tan(MathF.PI / 6f);
        for (int k = 1; k <= 20; k++)
        {
            float x = BlockWest - (k * Cell);
            Assert.InRange(r.Line[At(r, x, 100f)], 100f - (k * Cell * tan) - 0.01f, 100f - (k * Cell * tan) + 0.01f);
            Assert.InRange(r.Distance[At(r, x, 100f)], (k * Cell) - 0.01f, (k * Cell) + 0.01f);
        }
        // East of the block the line is the flat ground's own grazing ray, one texel's drop under it.
        Assert.InRange(r.Line[At(r, 500f, 100f)], -(Cell * tan) - 0.01f, -(Cell * tan) + 0.01f);
        Assert.Equal(100f, r.Ground[At(r, 12f, 0f)]);
        Assert.Equal(0f, r.Ground[At(r, 500f, 0f)]);
    }

    // The sweep reads the previous column between two texels. An oblique sun over a wall spanning
    // the sheet then gives the exact line at the distance travelled along the azimuth.
    [Fact]
    public void AnObliqueSunCastsAlongItsAzimuth()
    {
        var tris = new List<Vector3>();
        AddQuad(tris, -2048f, -2048f, 4096f, 0f);
        AddQuad(tris, 0f, -2048f, 16f, 100f, 4096f);
        float az = MathF.PI / 7f, el = MathF.PI / 8f;
        var sun = new Vector3(MathF.Cos(el) * MathF.Cos(az), MathF.Sin(el), MathF.Cos(el) * MathF.Sin(az));
        var r = TerrainShadowRaster.Run(tris.ToArray(), sun, 4);
        // Within 241 m along the azimuth the ray is still over the ground. Past that it has met the
        // ground, whose own grazing line takes over, so k stays well inside it.
        for (int k = 2; k <= 23; k += 7)
        {
            // A texel centre k columns west of the block's last column. Its ray travelled
            // k * Cell / cos(az) metres from there.
            float x = BlockWest - (k * Cell), d = k * Cell / MathF.Cos(az);
            int i = At(r, x, -(k * Cell * MathF.Tan(az)));
            Assert.InRange(r.Line[i], 100f - (d * MathF.Tan(el)) - 0.05f, 100f - (d * MathF.Tan(el)) + 0.05f);
            Assert.InRange(r.Distance[i], d - 0.05f, d + 0.05f);
        }
    }

    // A 45 degree ramp rising toward +X. Under a sun from -X it faces the light and its line stays
    // under its ground. Under a sun from +X at 30 degrees it is steeper than the sun, so its ridge
    // shades the whole slope. The line then stands over the ground, its blocker the ridge's last texel.
    [Fact]
    public void ARampIsLitFacingTheSunAndShadedBehindItsRidge()
    {
        var tris = new List<Vector3>();
        AddSlope(tris, -512f, -512f, 1024f, MathF.Tan(MathF.PI / 4f));
        float drop = Cell * MathF.Tan(MathF.PI / 6f);
        var lit = TerrainShadowRaster.Run(tris.ToArray(), new Vector3(-SunEast.X, SunEast.Y, 0f), 2);
        int i = At(lit, 100f, 0f);
        // The texel upstream stands one cell lower, and its ray drops one step more.
        Assert.InRange(lit.Line[i], lit.Ground[i] - Cell - drop - 0.01f, lit.Ground[i] - Cell - drop + 0.01f);
        Assert.InRange(lit.Distance[i], Cell - 0.01f, Cell + 0.01f);
        var shaded = TerrainShadowRaster.Run(tris.ToArray(), SunEast, 2);
        i = At(shaded, 100f, 0f);
        // The ridge's last texel centre casts the line down the slope, one drop per texel, to the
        // texel holding x = 100.
        float ridge = 512f - (Cell / 2f);
        float at = shaded.Origin.X + ((MathF.Floor((100f - shaded.Origin.X) / Cell) + 0.5f) * Cell);
        float line = ridge + 512f - ((ridge - at) / Cell * drop);
        Assert.InRange(shaded.Line[i], line - 0.01f, line + 0.01f);
        Assert.InRange(shaded.Distance[i], ridge - at - 0.01f, ridge - at + 0.01f);
    }

    [Fact]
    public void EveryBandCountGivesTheSameFloats()
    {
        var tris = new List<Vector3>();
        var rng = new Random(3);
        AddQuad(tris, -2048f, -2048f, 4096f, 0f);
        for (int i = 0; i < 200; i++)
        {
            float x = (rng.NextSingle() * 3800f) - 1900f, z = (rng.NextSingle() * 3800f) - 1900f;
            AddQuad(tris, x, z, 20f + (rng.NextSingle() * 200f), rng.NextSingle() * 300f, 20f + (rng.NextSingle() * 200f));
        }
        var one = TerrainShadowRaster.Run(tris.ToArray(), SunEast, 1);
        var many = TerrainShadowRaster.Run(tris.ToArray(), SunEast, 8);
        Assert.Equal(8, many.Bands);
        Assert.True(one.Ground.AsSpan().SequenceEqual(many.Ground));
        Assert.True(one.Line.AsSpan().SequenceEqual(many.Line));
    }

    // The lit share. A fragment half a softness over the line is lit, half a softness under it is
    // shaded. The softness grows with the blocker distance over a floor.
    [Fact]
    public void TheLitShareRampsOverTheSoftness()
    {
        Assert.Equal(1f, TerrainShadowRaster.Lit(10f, 0f, 12f, 0.02f, 4f));
        Assert.Equal(0f, TerrainShadowRaster.Lit(10f, 0f, 8f, 0.02f, 4f));
        Assert.Equal(0.5f, TerrainShadowRaster.Lit(10f, 0f, 10f, 0.02f, 4f));
        Assert.Equal(0.75f, TerrainShadowRaster.Lit(10f, 1000f, 15f, 0.02f, 4f), 3);
        Assert.Equal(1f, TerrainShadowRaster.Lit(TerrainShadowRaster.Empty, 0f, -50f, 0.02f, 4f));
    }

    private static int At(TerrainShadowRaster.Result r, float x, float z) =>
        ((int)MathF.Floor((z - r.Origin.Y) / Cell) * r.Width) + (int)MathF.Floor((x - r.Origin.X) / Cell);

    private static void AddQuad(List<Vector3> tris, float x, float z, float sizeX, float y, float? sizeZ = null)
    {
        float sz = sizeZ ?? sizeX;
        var a = new Vector3(x, y, z);
        var b = new Vector3(x + sizeX, y, z);
        var d = new Vector3(x, y, z + sz);
        var e = new Vector3(x + sizeX, y, z + sz);
        tris.AddRange(new[] { a, b, e, a, e, d });
    }

    // A plane rising g metres per metre of +X.
    private static void AddSlope(List<Vector3> tris, float x, float z, float size, float g)
    {
        var a = new Vector3(x, 0f, z);
        var b = new Vector3(x + size, size * g, z);
        var d = new Vector3(x, 0f, z + size);
        var e = new Vector3(x + size, size * g, z + size);
        tris.AddRange(new[] { a, b, e, a, e, d });
    }
}
