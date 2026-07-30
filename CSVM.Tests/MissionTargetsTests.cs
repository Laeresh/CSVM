using CSVM.Flight;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The mission <c>targets.json</c> reader (<c>docs/formats/missions.md</c>): a list of entries,
/// each a list of <c>[key, value]</c> pairs — not the flat alternating reader form.
/// Input is <c>fixtures/zrdr/targets.json</c>.
/// </summary>
public class MissionTargetsTests
{
    [Fact]
    public void OneEntryLabelsEveryNodeItLists()
    {
        var targets = Load();
        Assert.Equal(3, targets.Count); // two nodes from the first entry, one from the second
        var zone = targets.For("probe_dz1");
        Assert.Equal("MSG_PROBE_ZONE", zone.Description);
        Assert.Equal("MSG_PROBE_DZ", zone.CategoryLabel);
        Assert.Equal("MSG_PROBE_FLYTHROUGH", zone.HelpLabel);
        Assert.Equal(zone, targets.For("probe_dz1_inner"));
    }

    [Fact]
    public void AnAbsentLabelStaysNullRatherThanBecomingEmptyText()
    {
        var point = Load().For("probe_point");
        Assert.Equal("MSG_PROBE_POINT", point.Description);
        Assert.Null(point.CategoryLabel);
    }

    [Fact]
    public void AnEntryWithNoNodesLabelsNothing()
    {
        // The fixture's third entry has a description but no "nodes" list, so it contributes
        // no mapping — 3 nodes from 3 entries, not 4.
        Assert.Equal(3, Load().Count);
    }

    [Fact]
    public void AnUnlabelledNodeGivesAnAllNullTargetNotAThrow()
    {
        var missing = Load().For("probe_not_a_target");
        Assert.Null(missing.Description);
        Assert.Null(missing.CategoryLabel);
        Assert.Null(missing.HelpLabel);
    }

    [Fact]
    public void AMissionWithNoTargetsFileIsEmptyRatherThanAThrow()
    {
        Assert.Equal(0, MissionTargets.Load(TestData.TempDir()).Count);
    }

    private static MissionTargets Load() => MissionTargets.Load(TestData.Fixture("zrdr"));
}
