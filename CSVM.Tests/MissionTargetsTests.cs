using System.IO;
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
        Assert.Equal(4, targets.Count); // two nodes from the first entry, one each from the second and fourth
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
        // no mapping — 4 nodes from 4 entries, not 5.
        Assert.Equal(4, Load().Count);
    }

    [Fact]
    public void ANestedNodeListIsOnePathKeyedLikeTheScriptsTargets()
    {
        // [probe_tail, probe_hook] is the docking hook under the tail, the shape
        // ADD_OBJECTIVE_TARGET [[wv_tailhook, peoplehook]] names; the two tables meet on one key.
        var targets = Load();
        var hook = targets.For("probe_tail/probe_hook");
        Assert.Equal("MSG_PROBE_HOOK", hook.Description);
        Assert.Equal("MSG_PROBE_DOCK", hook.HelpLabel);
        Assert.Null(targets.For("probe_hook").Description);
        Assert.Null(targets.For("probe_tail").Description);
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

    [Fact]
    public void AMissionWithNoTargetsFileTakesTheChaptersWhole()
    {
        // The reader search path: the mission's zrdr, then the chapter's. C1C/M01 is the shipped
        // case, labelled entirely by C1C/zrdr/targets.zrd.
        var fromChapter = MissionTargets.Load(TestData.TempDir(), TestData.Fixture("zrdr"));
        Assert.Equal(4, fromChapter.Count);
        Assert.Equal("MSG_PROBE_POINT", fromChapter.For("probe_point").Description);
    }

    [Fact]
    public void AMissionsOwnTargetsFileWinsOverTheChaptersRatherThanMerging()
    {
        var missionZrdr = TestData.TempDir();
        File.WriteAllText(Path.Combine(missionZrdr, "targets.json"),
            "[[[\"description\", \"MSG_OWN\"], [\"nodes\", [\"own_node\"]]]]");
        var targets = MissionTargets.Load(missionZrdr, TestData.Fixture("zrdr"));
        Assert.Equal(1, targets.Count);
        Assert.Equal("MSG_OWN", targets.For("own_node").Description);
        Assert.Null(targets.For("probe_point").Description);
    }

    private static MissionTargets Load() => MissionTargets.Load(TestData.Fixture("zrdr"));
}
