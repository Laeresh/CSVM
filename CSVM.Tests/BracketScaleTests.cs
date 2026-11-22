using CSVM.Flight.Hud;
using CSVM.Utils;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The player's target bracket size, engine-free. It covers the four Game Options words, the factor
/// each spells, the clamp and the options file's round trip. At 100% the box and its label block are
/// bit for bit the unscaled ones. At a larger step the box's half width, half height and arms grow by
/// the factor. The label block keeps its gaps to the bigger box, and the HUD scale still multiplies
/// all of it.
/// </summary>
public class BracketScaleTests
{
    private const float PaneHeight = 1440f;

    // The decoded box (0x00607a0c / 0x00607a14 / 0x00607a10) and the label's gaps, in reference pixels.
    private const float HalfW = 10f;
    private const float HalfH = 8f;
    private const float Arm = 4f;
    private const float Gap = 3f;
    private const float Block = 33f;
    private const float Above = 30f;

    [Fact]
    public void TheWordsAreTheFourStepsAndANeverSetWordIsTheDecodedSize()
    {
        Assert.Equal(new[] { "100", "125", "150", "200" }, BracketScaleSetting.Words);
        Assert.Equal(new[] { "100%", "125%", "150%", "200%" }, BracketScaleSetting.Labels);
        Assert.Equal(1f, BracketScaleSetting.FactorOf("100"));
        Assert.Equal(1.25f, BracketScaleSetting.FactorOf("125"));
        Assert.Equal(1.5f, BracketScaleSetting.FactorOf("150"));
        Assert.Equal(2f, BracketScaleSetting.FactorOf("200"));
        Assert.Equal(1f, BracketScaleSetting.FactorOf(null));
        Assert.Equal(1f, BracketScaleSetting.FactorOf("175"));
        var lookup = BracketScaleSetting.Lookup;
        Assert.Equal(SettingSource.Default, lookup.Resolve(null, null, null, BracketScaleSetting.Default).Source);
        Assert.Equal(BracketScaleSetting.Default, lookup.Resolve(null, "300", null, BracketScaleSetting.Default).Word);
        Assert.Equal(SettingSource.Saved, lookup.Resolve(null, "150", null, BracketScaleSetting.Default).Source);
        Assert.Equal(BracketScaleSetting.Default, lookup.Fallback(new SettingContext(false, true, 1)).Word);
        Assert.Null(lookup.Flag);
        Assert.Equal("hud.bracketScale", lookup.Key);
        Assert.Equal("bracketScale", lookup.SavedField);
        Assert.Contains(lookup, WordSettings.All);
    }

    [Fact]
    public void TheFactorIsClampedToTheOfferedRange()
    {
        Assert.Equal(1f, HudMetrics.ClampBracketScale(0.5f));
        Assert.Equal(1.5f, HudMetrics.ClampBracketScale(1.5f));
        Assert.Equal(2f, HudMetrics.ClampBracketScale(4f));
        Assert.Equal(1f, HudMetrics.BracketScale);
    }

    [Theory]
    [InlineData("100")]
    [InlineData("125")]
    [InlineData("150")]
    [InlineData("200")]
    public void TheOptionsFileCarriesEachStep(string word)
    {
        var read = OptionsStore.Deserialize(OptionsStore.Serialize(new OptionsDef { Words = { [BracketScaleSetting.Lookup] = word } }));
        Assert.Equal(word, read!.Words[BracketScaleSetting.Lookup]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("175")]
    [InlineData("big")]
    public void AnAbsentOrHandEditedWordReadsNeverSet(string? word)
    {
        var read = OptionsStore.Deserialize(OptionsStore.Serialize(new OptionsDef { Words = { [BracketScaleSetting.Lookup] = word } }));
        Assert.Null(read!.Words[BracketScaleSetting.Lookup]);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(0.5f)]
    [InlineData(0.7071f)]
    [InlineData(1.333f)]
    [InlineData(1.496f)]
    [InlineData(1.629f)]
    public void AtOneHundredPercentTheBoxAndItsLabelAreTheUnscaledOnes(float s)
    {
        Assert.Equal(HalfW * s, TargetHud.BracketHalfW(s, 1f));
        Assert.Equal(HalfH * s, TargetHud.BracketHalfH(s, 1f));
        Assert.Equal(Arm * s, TargetHud.BracketArm(s, 1f));

        // The label block under the box, and flipped over it near the pane's bottom, as drawn before.
        var high = new Vector2(400f, 300f);
        Assert.Equal(new Vector2(400f, high.Y + ((HalfH + Gap) * s)), TargetHud.InViewLabelAnchor(high, PaneHeight, s, 1f));
        var low = new Vector2(400f, PaneHeight - 1f);
        Assert.Equal(new Vector2(400f, low.Y - ((HalfH + Above) * s)), TargetHud.InViewLabelAnchor(low, PaneHeight, s, 1f));
    }

    [Theory]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void ALargerStepGrowsTheBoxAndTheLabelKeepsItsGaps(float factor)
    {
        foreach (float s in new[] { 1f, 0.5f, 1.5f })
        {
            // The HUD scale still multiplies the player's factor.
            Assert.Equal(HalfW * s * factor, TargetHud.BracketHalfW(s, factor), 4);
            Assert.Equal(HalfH * s * factor, TargetHud.BracketHalfH(s, factor), 4);
            Assert.Equal(Arm * s * factor, TargetHud.BracketArm(s, factor), 4);

            // 3 under the bigger box's bottom.
            var high = new Vector2(400f, 300f);
            Assert.Equal(300f + TargetHud.BracketHalfH(s, factor) + (Gap * s),
                TargetHud.InViewLabelAnchor(high, PaneHeight, s, factor).Y, 3);

            // Flipped, 30 over the bigger box's top. The flip test measures from the bigger box too.
            float flipAt = PaneHeight - TargetHud.BracketHalfH(s, factor) - (Block * s) + 1f;
            var low = new Vector2(400f, flipAt);
            Assert.Equal(flipAt - TargetHud.BracketHalfH(s, factor) - (Above * s),
                TargetHud.InViewLabelAnchor(low, PaneHeight, s, factor).Y, 3);
            var justFits = new Vector2(400f, flipAt - 2f);
            Assert.Equal(justFits.Y + TargetHud.BracketHalfH(s, factor) + (Gap * s),
                TargetHud.InViewLabelAnchor(justFits, PaneHeight, s, factor).Y, 3);
        }
    }
}
