using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The near model's grade (<see cref="VegetationModels"/>). The card texture's mean is over its solid texels, the
/// model's over the surface its triangles cover. The grade is the per-channel gain between them.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class VegetationModelsTests
{
    private static readonly byte[] Red = { 255, 0, 0, 255 };
    private static readonly byte[] Green = { 0, 255, 0, 255 };
    private static readonly byte[] Clear = { 0, 0, 255, 0 };

    /// <summary>The mean reads solid texels alone, in linear light, and is null with none.</summary>
    [Fact]
    public void TheCardMeanIsLinearOverItsSolidTexels()
    {
        var mean = VegetationModels.MeanLinear(Pixels(Red, Clear, Clear, new byte[] { 128, 128, 128, 200 }))!.Value;
        float mid = 0.2158605f;
        Assert.Equal((1f + mid) / 2f, mean.X, 4);
        Assert.Equal(mid / 2f, mean.Y, 4);
        Assert.Equal(mid / 2f, mean.Z, 4);
        Assert.Null(VegetationModels.MeanLinear(Pixels(Clear, Clear)));
    }

    /// <summary>The model's mean reads the texels its triangles cover, weighted by their world area. A texel no
    /// triangle reaches counts nothing, and a triangle twice the size weighs twice.</summary>
    [Fact]
    public void TheModelMeanIsWeightedByTheSurfaceItsTrianglesCover()
    {
        // A 4 x 1 texture: red, green, clear, and an unused red.
        byte[] texture = Pixels(Red, Green, Clear, Red);
        var positions = new[]
        {
            new Vector3(0, 0, 0), new Vector3(2, 0, 0), new Vector3(0, 2, 0),
            new Vector3(0, 0, 1), new Vector3(1, 0, 1), new Vector3(0, 1, 1),
        };
        static Vector2 U(float u) => new(u, 0.5f);
        var indices = new[] { 0, 1, 2, 3, 4, 5 };

        // The big triangle (area 2) on red and the small one (area 0.5) on green: red weighs four times.
        var bigOnRed = new[] { U(0.05f), U(0.2f), U(0.1f), U(0.3f), U(0.45f), U(0.35f) };
        var mixed = VegetationModels.SurfaceMean(texture, 4, 1, positions, bigOnRed, indices)!.Value;
        Assert.Equal(0.8f, mixed.X, 4);
        Assert.Equal(0.2f, mixed.Y, 4);

        // Both on red: the unused red texel and the green one change nothing.
        var red = VegetationModels.SurfaceMean(texture, 4, 1, positions, new[] { U(0.1f), U(0.2f), U(0.15f), U(0.1f), U(0.2f), U(0.15f) }, indices)!.Value;
        Assert.Equal(1f, red.X, 4);
        Assert.Equal(0f, red.Y, 4);

        // A triangle on the clear texel counts nothing, and all clear is no mean.
        var clearToo = VegetationModels.SurfaceMean(texture, 4, 1, positions,
            new[] { U(0.55f), U(0.7f), U(0.6f), U(0.1f), U(0.2f), U(0.15f) }, indices)!.Value;
        Assert.Equal(1f, clearToo.X, 4);
        Assert.Equal(0f, clearToo.Y, 4);
        Assert.Null(VegetationModels.SurfaceMean(texture, 4, 1, positions,
            new[] { U(0.55f), U(0.7f), U(0.6f), U(0.55f), U(0.7f), U(0.6f) }, indices));
        Assert.Null(VegetationModels.SurfaceMean(texture, 4, 1, positions, new Vector2[3], indices));
    }

    /// <summary>The grade puts the model's mean on the card's per channel, held to 0.25-4, and is one where
    /// either mean is unknown.</summary>
    [Fact]
    public void TheGradePutsTheModelOnTheCard()
    {
        Assert.Equal(new Vector3(0.5f, 2f, 1f), VegetationModels.Grade(new Vector3(0.1f, 0.4f, 0.3f), new Vector3(0.2f, 0.2f, 0.3f)));
        Assert.Equal(new Vector3(0.25f, 4f, 1f), VegetationModels.Grade(new Vector3(0.01f, 0.9f, 0.5f), new Vector3(0.9f, 0.01f, 0f)));
        Assert.Equal(Vector3.One, VegetationModels.Grade(null, new Vector3(0.2f, 0.2f, 0.2f)));
        Assert.Equal(Vector3.One, VegetationModels.Grade(new Vector3(0.2f, 0.2f, 0.2f), null));
    }

    private static byte[] Pixels(params byte[][] texels)
    {
        var bytes = new byte[texels.Length * 4];
        for (int i = 0; i < texels.Length; i++)
            texels[i].CopyTo(bytes, i * 4);
        return bytes;
    }
}
