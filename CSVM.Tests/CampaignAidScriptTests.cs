using CSVM.UI.Boards;
using CSVM.UI.Campaign;
using CSVM.UI.Menu;
using Xunit;

namespace CSVM.Tests;

/// <summary>The input script a campaign screenshot aid's <c>--menu=</c> colon argument spells. It
/// covers the counted verbs and button words, and the bare step count every older command means.
/// It covers the refusal of a verb the presentation binds no press for.</summary>
public class CampaignAidScriptTests
{
    [Fact]
    public void ABareNumberIsStillThatManyStepsDown()
    {
        var presses = CampaignAidScript.Parse("3");

        Assert.Single(presses);
        Assert.Equal('d', presses[0].Verb);
        Assert.Equal(3, presses[0].Count);
        Assert.Equal(BoardButton.None, presses[0].Button);
    }

    [Fact]
    public void ADecimalCountKeepsTheWholeNumberTheFloatReadGave()
    {
        var presses = CampaignAidScript.Parse("2.75");

        Assert.Single(presses);
        Assert.Equal('d', presses[0].Verb);
        Assert.Equal(2, presses[0].Count);
    }

    [Fact]
    public void CountsBindToTheVerbTheyPrecede()
    {
        var presses = CampaignAidScript.Parse("4da");

        Assert.Equal(2, presses.Count);
        Assert.Equal('d', presses[0].Verb);
        Assert.Equal(4, presses[0].Count);
        Assert.Equal('a', presses[1].Verb);
        Assert.Equal(1, presses[1].Count);
    }

    [Fact]
    public void ASeparatorJoinsSegmentsWithoutChangingThem()
    {
        Assert.Equal(CampaignAidScript.Parse("4da"), CampaignAidScript.Parse("4d-a"));
    }

    [Fact]
    public void ExportNamesTheExportPlanePress()
    {
        var presses = CampaignAidScript.Parse(CampaignAidProfiles.ExportArgument);

        Assert.Single(presses);
        Assert.Equal(BoardButton.ExportPlane, presses[0].Button);
    }

    [Fact]
    public void AButtonIsAlsoNamedByItsOwnMemberName()
    {
        var presses = CampaignAidScript.Parse("2d-changeammo");

        Assert.Equal(2, presses.Count);
        Assert.Equal('d', presses[0].Verb);
        Assert.Equal(BoardButton.ChangeAmmo, presses[1].Button);
    }

    [Fact]
    public void AnArgumentNoVerbOrButtonReadsSpellsNoPressAtAll()
    {
        Assert.Empty(CampaignAidScript.Parse(string.Empty));
        Assert.Empty(CampaignAidScript.Parse("seat-plane"));
        Assert.Empty(CampaignAidScript.Parse("2dz"));
    }

    [Fact]
    public void TheSecondaryVerbParsesWhicheverPresentationIsRunning()
    {
        var presses = CampaignAidScript.Parse("2d-x");

        Assert.Equal(2, presses.Count);
        Assert.Equal('d', presses[0].Verb);
        Assert.Equal('x', presses[1].Verb);
    }

    [Fact]
    public void APresentationWithoutTheSecondaryPressRefusesTheWholeScript()
    {
        Assert.Null(CampaignAidScript.Presses("2d-x", CampaignAidScript.VerbsWithoutSecondary, "Original's controls"));
    }

    [Fact]
    public void EveryOtherVerbStillReplaysWithoutASecondaryPress()
    {
        const string script = "3d1u-2l-r-a-b-export";

        Assert.Equal(
            CampaignAidScript.Parse(script),
            CampaignAidScript.Presses(script, CampaignAidScript.VerbsWithoutSecondary, "Original's controls"));
    }
}
