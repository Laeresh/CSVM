using CSVM.Session;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// BL-452's first cause: the letterbox fit used to solve for equality, so the card's outer edge
/// landed exactly on the frame edge at every ratio at or above 1.64211 and the world showed
/// through the boundary column. The card is C3's shipped one, read off
/// <c>extracted/C3/gamez/models.json</c>'s first record: half-width 6.6461835, half-height
/// 4.0474105, 7.5 m in front of the eye.
/// </summary>
public class CutsceneLetterboxFitTests
{
    private const float HalfWidth = 6.6461835f;
    private const float HalfHeight = 4.0474105f;
    private const float Dist = 7.5f;

    // Every ratio the project can be flown at, plus the crossover the two terms meet at. 1920x1080
    // is not listed separately: it IS 16:9 to the last bit, and a theory row that repeats another's
    // value is skipped outright by xUnit rather than run twice, which silently drops a case.
    public static TheoryData<float> Aspects => new()
    {
        4f / 3f, 1.6f, 1.64211f, 16f / 9f, 21f / 9f, 3440f / 1440f,
    };

    [Theory]
    [MemberData(nameof(Aspects))]
    public void CardOverhangsThePaneAtEveryAspect(float aspect)
    {
        var (horizontal, vertical) = Margins(Fov(aspect), aspect);

        Assert.True(horizontal > 0f, $"horizontal margin at {aspect}: {horizontal}");
        Assert.True(vertical > 0f, $"vertical margin at {aspect}: {vertical}");
    }

    // The able-to-fail half (METHOD-9/METHOD-10): with the overscan taken back out, the same
    // arithmetic reads zero horizontal margin from the crossover upwards, which is the defect.
    [Theory]
    [MemberData(nameof(Aspects))]
    public void WithoutTheOverscanTheFitIsAnEqualityFromTheCrossoverUp(float aspect)
    {
        float unmargined = Mathf.RadToDeg(
            2f * Mathf.Atan(Mathf.Min(HalfHeight, HalfWidth / aspect) / Dist));
        var (horizontal, _) = Margins(unmargined, aspect);

        if (aspect >= 1.64211f)
        {
            Assert.Equal(0f, horizontal, 4);
        }
        else
        {
            Assert.True(horizontal > 0f, $"below the crossover the height term binds: {horizontal}");
        }
    }

    // The binding axis takes exactly the declared overscan as a share of the card; the other one
    // keeps more. A card scaled up by (1 + k) leaves k / (1 + k) of itself outside the frame.
    [Fact]
    public void TheMarginIsTheDeclaredOverscan()
    {
        float expected = CutsceneController.CardOverscan / (1f + CutsceneController.CardOverscan);
        var (wideH, wideV) = Margins(Fov(16f / 9f), 16f / 9f);
        var (narrowH, narrowV) = Margins(Fov(4f / 3f), 4f / 3f);

        Assert.Equal(expected, wideH, 4);
        Assert.True(wideV > wideH);
        Assert.Equal(expected, narrowV, 4);
        Assert.True(narrowH > narrowV);
    }

    [Fact]
    public void ADegenerateCardHasNoFit()
    {
        Assert.Null(CutsceneController.FramingFovDeg(new Aabb(Vector3.Zero, Vector3.Zero), 1.7778f));
        Assert.Null(CutsceneController.FramingFovDeg(Card(), 0f));
    }

    private static Aabb Card() => new(
        new Vector3(-HalfWidth, -HalfHeight, -Dist),
        new Vector3(HalfWidth * 2f, HalfHeight * 2f, 0f));

    private static float Fov(float aspect)
    {
        float? fov = CutsceneController.FramingFovDeg(Card(), aspect);
        Assert.NotNull(fov);
        return fov!.Value;
    }

    // What fraction of the card's own half-extent lies outside the frustum at the card's plane.
    private static (float Horizontal, float Vertical) Margins(float fovDeg, float aspect)
    {
        float seenHalfHeight = Mathf.Tan(Mathf.DegToRad(fovDeg) * 0.5f) * Dist;
        return (1f - (seenHalfHeight * aspect / HalfWidth), 1f - (seenHalfHeight / HalfHeight));
    }
}
