using CSVM.UI.Menu;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The HUD mode setting, engine-free: its words and their order, the cycle and the line a
/// press posts. The saved word round-trips through the options file and the Options row's choices,
/// and a hand-edited word reads as never set. The cycle writes the file, except to a null store (a
/// <c>--det</c> run). The pane-side visibility is the engine suites' (<c>hud-mode-parts</c>).</summary>
public class HudModeSettingTests
{
    [Fact]
    public void TheWordsSpellTheThreeModesInCycleOrderWithFullTheDefault()
    {
        Assert.Equal(new[] { "full", "minimal", "none" }, HudModeSetting.Words);
        Assert.Equal(new[] { "Full", "Minimal", "None" }, HudModeSetting.Labels());
        Assert.Equal(HudModeSetting.Words[0], HudModeSetting.Default);
        Assert.Equal(HudMode.Full, HudModeSetting.ModeOf(null));
        Assert.Equal(HudMode.Full, HudModeSetting.ModeOf("bogus"));
        Assert.Equal(HudMode.Minimal, HudModeSetting.ModeOf("minimal"));
        Assert.Equal(HudMode.None, HudModeSetting.ModeOf("none"));
        foreach (var mode in new[] { HudMode.Full, HudMode.Minimal, HudMode.None })
        {
            Assert.Equal(mode, HudModeSetting.ModeOf(HudModeSetting.WordOf(mode)));
        }

        Assert.Equal(HudMode.Minimal, HudModeSetting.Next(HudMode.Full));
        Assert.Equal(HudMode.None, HudModeSetting.Next(HudMode.Minimal));
        Assert.Equal(HudMode.Full, HudModeSetting.Next(HudMode.None));
        Assert.Equal("HUD: Minimal", HudModeSetting.Line(HudMode.Minimal));
        Assert.Equal("HUD: None", HudModeSetting.Line(HudMode.None));
        Assert.Equal("HUD: Full", HudModeSetting.Line(HudMode.Full));
    }

    // The words, the labels and the line are indexed by the enum's value. Each list holds one entry
    // per mode, in the enum's order.
    [Fact]
    public void TheWordsAndLabelsHoldOneEntryPerModeInEnumOrder()
    {
        var modes = System.Enum.GetValues<HudMode>();
        Assert.Equal(modes.Length, HudModeSetting.Words.Count);
        Assert.Equal(modes.Length, HudModeSetting.Labels().Count);
        for (int i = 0; i < modes.Length; i++)
        {
            Assert.Equal(i, (int)modes[i]);
            Assert.Equal(modes[i], HudModeSetting.ModeOf(HudModeSetting.Words[i]));
            Assert.Equal("HUD: " + HudModeSetting.Labels()[i], HudModeSetting.Line(modes[i]));
        }
    }

    // The one rule the flat targeting HUD and a stereo session's world markers share: only None
    // outside the cockpit view keeps the lead marker alone.
    [Fact]
    public void OnlyNoneOutsideTheCockpitIsLeadOnly()
    {
        Assert.True(HudModeSetting.LeadOnly(HudMode.None, cockpitView: false));
        Assert.False(HudModeSetting.LeadOnly(HudMode.None, cockpitView: true));
        foreach (var mode in new[] { HudMode.Full, HudMode.Minimal })
        {
            Assert.False(HudModeSetting.LeadOnly(mode, cockpitView: false));
            Assert.False(HudModeSetting.LeadOnly(mode, cockpitView: true));
        }
    }

    [Fact]
    public void AnAppliedWordTakesTheRunningModeWhateverTheLaunchRead()
    {
        // Shares the process-wide mode with the cycle fact below, so both run in this one class.
        var before = HudModeSetting.Current;
        try
        {
            HudModeSetting.Current = HudMode.Full;
            HudModeSetting.Apply("none");
            Assert.Equal(HudMode.None, HudModeSetting.Current);
            HudModeSetting.Apply(null);
            Assert.Equal(HudMode.Full, HudModeSetting.Current);
        }
        finally
        {
            HudModeSetting.Current = before;
        }
    }

    [Fact]
    public void TheSavedWordBeatsFullAndNothingSavedReadsFull()
    {
        Assert.Equal("full", HudModeSetting.Resolve(null).Word);
        Assert.Equal(SettingSource.Saved, HudModeSetting.Resolve("none").Source);
        Assert.Equal("none", HudModeSetting.Resolve("none").Word);
    }

    [Fact]
    public void TheWordRoundTripsThroughTheFileAndTheOptionsRow()
    {
        var store = new OptionsStore(TestData.TempDir());
        Assert.Null(store.Load().HudMode);
        Assert.Null(new OptionsChoices().HudMode);

        store.Save(new OptionsDef { HudMode = "minimal" });
        Assert.Equal("minimal", store.Load().HudMode);
        var choices = new OptionsChoices();
        choices.Load(store.Load());
        Assert.Equal("minimal", choices.ToExit().HudMode);
    }

    [Fact]
    public void AHandEditedWordReadsAsNeverSet()
    {
        var read = OptionsStore.Deserialize(OptionsStore.Serialize(new OptionsDef { HudMode = "tiny", ThrottlePlate = true }));
        Assert.Null(read!.HudMode);
        Assert.True(read.ThrottlePlate);
    }

    // One fact, since it is the only one here to move the process-wide mode, and it puts it back.
    [Fact]
    public void TheCycleStepsTheRunningModeAndSavesItUnlessThereIsNoStore()
    {
        var before = HudModeSetting.Current;
        try
        {
            var store = new OptionsStore(TestData.TempDir());
            store.Save(new OptionsDef { ThrottlePlate = true });
            HudModeSetting.Current = HudMode.Full;

            Assert.Equal(HudMode.Minimal, HudModeSetting.Cycle(store));
            Assert.Equal(HudMode.Minimal, HudModeSetting.Current);
            Assert.Equal("minimal", store.Load().HudMode);
            Assert.True(store.Load().ThrottlePlate);

            Assert.Equal(HudMode.None, HudModeSetting.Cycle(store));
            Assert.Equal("none", store.Load().HudMode);
            Assert.Equal(HudMode.Full, HudModeSetting.Cycle(store));
            Assert.Equal("full", store.Load().HudMode);

            Assert.Equal(HudMode.Minimal, HudModeSetting.Cycle(null));
            Assert.Equal(HudMode.Minimal, HudModeSetting.Current);
            Assert.Equal("full", store.Load().HudMode);
        }
        finally
        {
            HudModeSetting.Current = before;
        }
    }
}
