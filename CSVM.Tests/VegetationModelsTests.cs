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
        var mean = VegetationModels.MeanLinear(Pixels(Red, Clear, Clear, new byte[] { 128, 128, 128, 200 }), 4, 1)!.Value;
        float mid = 0.2158605f;
        Assert.Equal((1f + mid) / 2f, mean.X, 4);
        Assert.Equal(mid / 2f, mean.Y, 4);
        Assert.Equal(mid / 2f, mean.Z, 4);
        Assert.Null(VegetationModels.MeanLinear(Pixels(Clear, Clear), 2, 1));
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

    /// <summary>The card's crown mean reads the top share of the rows its solid texels span. A card of a clear
    /// row, a green crown and a red trunk reads green at a half and both at the whole.</summary>
    [Fact]
    public void TheCardsCrownMeanReadsItsTopRows()
    {
        byte[] card = Pixels(Clear, Green, Green, Red, Red);
        Assert.Equal(new Vector3(0f, 1f, 0f), VegetationModels.MeanLinear(card, 1, 5, 0.5f));
        Assert.Equal(new Vector3(0.5f, 0.5f, 0f), VegetationModels.MeanLinear(card, 1, 5, 1f));
    }

    /// <summary>The model's crown mean reads only triangles centred in the top share of its height.</summary>
    [Fact]
    public void TheModelsCrownMeanReadsItsTopTriangles()
    {
        byte[] texture = Pixels(Red, Green);
        static Vector2 U(float u) => new(u, 0.5f);
        var positions = new[]
        {
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0),
            new Vector3(0, 9, 0), new Vector3(1, 9, 0), new Vector3(0, 10, 0),
        };
        var uvs = new[] { U(0.1f), U(0.2f), U(0.3f), U(0.6f), U(0.7f), U(0.8f) };
        var indices = new[] { 0, 1, 2, 3, 4, 5 };
        var crown = VegetationModels.SurfaceMean(texture, 2, 1, positions, uvs, indices, 0.5f)!.Value;
        Assert.Equal(0f, crown.X, 4);
        Assert.Equal(1f, crown.Y, 4);
        Assert.Equal(0.5f, VegetationModels.SurfaceMean(texture, 2, 1, positions, uvs, indices)!.Value.X, 4);
    }

    /// <summary>The grade is the luminance ratio, each channel held within <see cref="VegetationModels.MaxChroma"/>
    /// of it. A model then keeps its hue, and a blue far off the card's moves no more than that. The ratio is held to
    /// 0.25-4, and the grade is one where either mean is unknown.</summary>
    [Fact]
    public void TheGradeMeetsTheCardsBrightnessAndKeepsTheModelsHue()
    {
        var even = VegetationModels.Grade(new Vector3(0.1f, 0.1f, 0.1f), new Vector3(0.2f, 0.2f, 0.2f));
        Assert.Equal(0.5f, even.X, 4);
        Assert.Equal(0.5f, even.Z, 4);

        // The card is bluer than the model: the luminance ratio is about 1, and blue moves by the clamp alone.
        var hue = VegetationModels.Grade(new Vector3(0.1f, 0.1f, 0.4f), new Vector3(0.1f, 0.1f, 0.1f));
        float s = ((0.2126f * 0.1f) + (0.7152f * 0.1f) + (0.0722f * 0.4f)) / 0.1f;
        Assert.Equal(s * (1f + VegetationModels.MaxChroma), hue.Z, 4);
        Assert.Equal(s * (1f - VegetationModels.MaxChroma), hue.X, 4);

        Assert.Equal(new Vector3(4f, 4f, 4f), VegetationModels.Grade(new Vector3(0.9f, 0.9f, 0.9f), new Vector3(0.01f, 0.01f, 0.01f)));
        Assert.Equal(Vector3.One, VegetationModels.Grade(null, new Vector3(0.2f, 0.2f, 0.2f)));
        Assert.Equal(Vector3.One, VegetationModels.Grade(new Vector3(0.2f, 0.2f, 0.2f), null));

        // Unbounded, every channel meets the card's, as C3's crown model meets the crowns.
        var full = VegetationModels.Grade(new Vector3(0.1f, 0.1f, 0.4f), new Vector3(0.1f, 0.1f, 0.1f), float.PositiveInfinity);
        Assert.Equal(1f, full.X, 4);
        Assert.Equal(1f, full.Y, 4);
        Assert.Equal(4f, full.Z, 4);
    }
    private static byte[] Pixels(params byte[][] texels)
    {
        var bytes = new byte[texels.Length * 4];
        for (int i = 0; i < texels.Length; i++)
            texels[i].CopyTo(bytes, i * 4);
        return bytes;
    }
}
