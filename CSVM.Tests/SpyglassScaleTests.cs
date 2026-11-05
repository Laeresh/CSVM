using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Utils;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The player's spyglass size, engine-free. It covers the four Game Options words, the factor each
/// spells, the clamp and the options file's round trip. At 100% every disc placement is bit for bit
/// the unscaled one. At a larger step the radius, the picture's side and the anchor's inset grow by
/// the factor. So do the shaft's start and the label's offset from the disc, and the gaps do not.
/// </summary>
public class SpyglassScaleTests
{
    private const float PaneHeight = 1440f;

    [Fact]
    public void TheWordsAreTheFourStepsAndANeverSetWordIsTheDecodedSize()
    {
        Assert.Equal(new[] { "100", "125", "150", "200" }, SpyglassScaleSetting.Words);
        Assert.Equal(new[] { "100%", "125%", "150%", "200%" }, SpyglassScaleSetting.Labels());
        Assert.Equal(1f, SpyglassScaleSetting.FactorOf("100"));
        Assert.Equal(1.25f, SpyglassScaleSetting.FactorOf("125"));
        Assert.Equal(1.5f, SpyglassScaleSetting.FactorOf("150"));
        Assert.Equal(2f, SpyglassScaleSetting.FactorOf("200"));
        Assert.Equal(1f, SpyglassScaleSetting.FactorOf(null));
        Assert.Equal(1f, SpyglassScaleSetting.FactorOf("175"));
        Assert.Equal(SettingSource.Default, SpyglassScaleSetting.Resolve(null).Source);
        Assert.Equal(SpyglassScaleSetting.Default, SpyglassScaleSetting.Resolve("300").Word);
        Assert.Equal(SettingSource.Saved, SpyglassScaleSetting.Resolve("150").Source);
    }

    [Fact]
    public void TheFactorIsClampedToTheOfferedRange()
    {
        Assert.Equal(1f, HudMetrics.ClampSpyglassScale(0.5f));
        Assert.Equal(1.5f, HudMetrics.ClampSpyglassScale(1.5f));
        Assert.Equal(2f, HudMetrics.ClampSpyglassScale(4f));
        Assert.Equal(1f, HudMetrics.SpyglassScale);
    }

    [Theory]
    [InlineData("100")]
    [InlineData("125")]
    [InlineData("150")]
    [InlineData("200")]
    public void TheOptionsFileCarriesEachStep(string word)
    {
        var read = OptionsStore.Deserialize(OptionsStore.Serialize(new OptionsDef { SpyglassScale = word }));
        Assert.Equal(word, read!.SpyglassScale);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("175")]
    [InlineData("big")]
    public void AnAbsentOrHandEditedWordReadsNeverSet(string? word)
    {
        var read = OptionsStore.Deserialize(OptionsStore.Serialize(new OptionsDef { SpyglassScale = word }));
        Assert.Null(read!.SpyglassScale);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(0.5f)]
    [InlineData(0.7071f)]
    [InlineData(1.333f)]
    [InlineData(1.496f)]
    [InlineData(1.629f)]
    public void AtOneHundredPercentEveryPlacementIsTheUnscaledOne(float s)
    {
        var anchor = new Vector2(400f, 300f);
        var low = new Vector2(400f, 1420f);
        var dir = new Vector2(0.6f, -0.8f);
        Assert.Equal(Spyglass.RefRadius * s, TargetHud.DiscRadius(s, 1f));
        Assert.Equal(Mathf.Max(1, Mathf.RoundToInt(Spyglass.RefWindow * s)), TargetHud.DiscSide(s, 1f));
        Assert.Equal(anchor + (dir * (Spyglass.RefRadius * s)), TargetHud.ShaftTail(anchor, dir, s, disc: true, 1f));
        Assert.Equal(new Vector2(400f, anchor.Y + (Spyglass.RefRadius * s) + (3f * s)),
            TargetHud.EdgeLabelAnchor(anchor, PaneHeight, s, disc: true, 1f));
        Assert.Equal(new Vector2(400f, low.Y - ((Spyglass.RefRadius + 45f) * s)),
            TargetHud.EdgeLabelAnchor(low, PaneHeight, s, disc: true, 1f));
    }

    [Theory]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void ALargerStepGrowsTheDiscAndWhatIsPlacedFromItsRadius(float factor)
    {
        const float s = 1f;
        Assert.Equal(Spyglass.RefRadius * factor, TargetHud.DiscRadius(s, factor), 4);
        Assert.Equal(Mathf.RoundToInt(Spyglass.RefWindow * factor), TargetHud.DiscSide(s, factor));
        Assert.Equal(2f * TargetHud.DiscRadius(s, factor), TargetHud.DiscSide(s, factor), 0);

        // The anchor sits one radius further in, so the bigger disc still stands inside the pane.
        var pane = new Vector2(2560f, PaneHeight);
        var far = new Vector2(9000f, 720f);
        var bare = EdgeMarker.Resolve(far, behind: false, pane);
        var withDisc = EdgeMarker.Resolve(far, behind: false, pane, TargetHud.DiscRadius(s, factor));
        Assert.Equal(bare.Anchor.X - (Spyglass.RefRadius * factor), withDisc.Anchor.X, 3);
        Assert.Equal(bare.Tip, withDisc.Tip);

        // The shaft leaves the scaled rim.
        var tail = TargetHud.ShaftTail(withDisc.Anchor, withDisc.Dir, s, disc: true, factor);
        Assert.Equal(Spyglass.RefRadius * factor, tail.DistanceTo(withDisc.Anchor), 3);

        // The label keeps its 3-pixel gap under the scaled bottom, and its 45 over the scaled top.
        var high = new Vector2(400f, 300f);
        Assert.Equal(300f + (Spyglass.RefRadius * factor) + 3f,
            TargetHud.EdgeLabelAnchor(high, PaneHeight, s, disc: true, factor).Y, 3);
        var low = new Vector2(400f, 1400f);
        Assert.Equal(1400f - (Spyglass.RefRadius * factor) - 45f,
            TargetHud.EdgeLabelAnchor(low, PaneHeight, s, disc: true, factor).Y, 3);

        // Low on the pane the block flips over the disc, 45 over its scaled top.
        var floor = new Vector2(400f, 1430f);
        Assert.Equal(1430f - (Spyglass.RefRadius * factor) - 45f,
            TargetHud.EdgeLabelAnchor(floor, PaneHeight, s, disc: true, factor).Y, 3);
        Assert.Equal(1430f - (((Spyglass.RefRadius * factor) + 45f) * 1.5f),
            TargetHud.EdgeLabelAnchor(floor, PaneHeight, 1.5f, disc: true, factor).Y, 3);

        // The bare marker and the arrowhead take no part in the disc's factor.
        Assert.Equal(TargetHud.EdgeLabelAnchor(high, PaneHeight, s), TargetHud.EdgeLabelAnchor(high, PaneHeight, s, disc: false, factor));
        Assert.Equal(high, TargetHud.ShaftTail(high, Vector2.Right, s, disc: false, factor));
    }

    [Fact]
    public void ThePictureSideStaysTheDiscsDiameterAtEveryHudScale()
    {
        // 720p, a 2P pane at 1080p and a 4K window, each at 200%.
        foreach (float s in new[] { 0.5f, 0.75f * 0.7071f, 1.5f })
        {
            Assert.Equal(Mathf.RoundToInt(Spyglass.RefWindow * s * 2f), TargetHud.DiscSide(s, 2f));
            Assert.InRange(TargetHud.DiscSide(s, 2f) - (2 * TargetHud.DiscSide(s, 1f)), -1, 1);
        }

        Assert.Equal(0, TargetHud.DiscSide(0f, 2f));
    }
}
