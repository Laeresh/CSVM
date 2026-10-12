using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Which water textures are the open-sea base sheet the Enhanced wave ocean replaces
/// (<see cref="SceneBuilder.IsOceanBaseTexture"/>). C1 and C1C paint their sea with
/// <c>water1</c>, and C1's opaque coast tiles <c>water1_trans1/2</c> sit beside it and must stay.
/// </summary>
public class OceanBaseTextureTests
{
    [Theory]
    [InlineData("wtr00000.tif")]
    [InlineData("WTR00000.TIF")]
    [InlineData("wtr00000")]
    [InlineData("water1.tif")]
    [InlineData("water1")]
    [InlineData("Water1.tif")]
    public void TheBaseSheetIsMatched(string texture) => Assert.True(SceneBuilder.IsOceanBaseTexture(texture));

    [Theory]
    [InlineData("water1_trans1.tif")]
    [InlineData("water1_trans2.tif")]
    [InlineData("water10.tif")]
    [InlineData("water2.tif")]
    [InlineData("srf0001.tif")]
    [InlineData("wakefront1.tif")]
    [InlineData("z3_foggrad.tif")]
    [InlineData("beach1.tif")]
    [InlineData("")]
    public void CoastTilesSurfAndOverlaysAreNot(string texture) => Assert.False(SceneBuilder.IsOceanBaseTexture(texture));
}
