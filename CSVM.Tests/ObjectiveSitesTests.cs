using System.Collections.Generic;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="ObjectiveSites.CollectTargets"/>'s two site sources: <c>targets.zrd</c>'s own
/// <c>objective</c> entries and the graph's <c>ADD_OBJECTIVE_TARGET</c> edits, each dropped again
/// once a completed objective's own <c>REMOVE_OBJECTIVE_TARGET</c> names its key; and
/// <see cref="ObjectiveSites.LiveDespiteState"/>, the resolved-node destroyed gate a live
/// <c>Collect</c> applies beside it. A site is offered from mission start with the graph's own
/// store still empty, which is what reading <see cref="ObjectiveGraph.ObjectiveTargets"/> alone
/// would miss. A roster block's own flag is NOT collected here at all: it rides the block's
/// aircraft (<see cref="CSVM.Flight.FlightController.ObjectiveTarget"/>). Pinned off-engine: none
/// of the types here touch Godot.
/// </summary>
public class ObjectiveSitesTests
{
    [Fact]
    public void ATargetsZrdFlaggedEntryIsOfferedFromMissionStart()
    {
        var script = Script("\"OBJECTIVE1\",[\"BEGIN_DORMANT\",[-1.0],\"INACTIVE1\",[\"never\"]]");
        var graph = new ObjectiveGraph(script, new FakeWorld());
        var into = new List<string>();

        Assert.Empty(graph.ObjectiveTargets);
        ObjectiveSites.CollectTargets(script, graph, TargetsWithObjectiveFlag("rfspt1"), into);

        Assert.Contains("rfspt1", into);
    }

    [Fact]
    public void AFlaggedSiteIsDroppedOnceItsOwnKeyIsRemovedByACompletedObjective()
    {
        var script = Script("\"OBJECTIVE1\",[\"REMOVE_OBJECTIVE_TARGET\",[\"rfspt1\"]]");
        var graph = new ObjectiveGraph(script, new FakeWorld());
        var targets = TargetsWithObjectiveFlag("rfspt1");

        var before = new List<string>();
        ObjectiveSites.CollectTargets(script, graph, targets, before);
        Assert.Contains("rfspt1", before);

        // OBJECTIVE1 is conditionless, so it completes on the first tick, the way a shipped
        // primary does once its gate reads true.
        graph.Step(0.1f);
        Assert.True(graph.CompletedOf(1));

        var after = new List<string>();
        ObjectiveSites.CollectTargets(script, graph, targets, after);
        Assert.DoesNotContain("rfspt1", after);
    }

    [Fact]
    public void ASiteTheGraphAddsIsOfferedAndStillRemovable()
    {
        var script = Script(
            "\"OBJECTIVE1\",[\"ADD_OBJECTIVE_TARGET\",[\"caboose_polys\"]],"
            + "\"OBJECTIVE2\",[\"BEGIN_DORMANT\",[-1.0],\"INACTIVE1\",[\"never\"]]");
        var graph = new ObjectiveGraph(script, new FakeWorld());

        graph.Step(0.1f);
        var into = new List<string>();
        ObjectiveSites.CollectTargets(script, graph, new MissionTargets(), into);
        Assert.Contains("caboose_polys", into);
    }

    [Fact]
    public void AGraphAddDoesNotDuplicateATargetsZrdEntryOfTheSameKey()
    {
        var script = Script("\"OBJECTIVE1\",[\"ADD_OBJECTIVE_TARGET\",[\"rfspt1\"]]");
        var graph = new ObjectiveGraph(script, new FakeWorld());
        graph.Step(0.1f);

        var into = new List<string>();
        ObjectiveSites.CollectTargets(script, graph, TargetsWithObjectiveFlag("rfspt1"), into);

        Assert.Single(into, key => key == "rfspt1");
    }

    [Fact]
    public void ASiteWhoseResolvedNodeReadsDestroyedIsNoLongerLive()
    {
        // CM21's six rfspt*/lfspt* Destroy Support Beam sites: their own REMOVE_OBJECTIVE_TARGET
        // fires per-beam on that beam's own objective, not on a kill, so a beam shot down before
        // its objective completes must leave the cycle on the destroyed read alone.
        Assert.False(ObjectiveSites.LiveDespiteState(DestructibleRegistry.State.Destroyed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(DestructibleRegistry.State.Healthy)]
    [InlineData(DestructibleRegistry.State.Damaged)]
    public void ASiteNotReadDestroyedStaysLive(DestructibleRegistry.State? state)
    {
        Assert.True(ObjectiveSites.LiveDespiteState(state));
    }

    private static MissionTargets TargetsWithObjectiveFlag(string node)
    {
        var dir = TestData.TempDir();
        File.WriteAllText(Path.Combine(dir, "targets.json"),
            "[[[\"nodes\",[\"" + node + "\"]],[\"objective\",true]]]");
        return MissionTargets.Load(dir);
    }

    private static ObjectiveScript Script(string body)
    {
        var dir = TestData.TempDir();
        File.WriteAllText(Path.Combine(dir, "objectives.json"), "[[" + body + "]]");
        return ObjectiveScript.Load(dir);
    }

    // Answers only what a test sets; no condition ever reads true unless the test drives it.
    private sealed class FakeWorld : IObjectiveWorld
    {
        public bool? NodeInactive(IReadOnlyList<string> path) => false;

        public int AnimState(string anim) => 0;

        public int? GroupLiveCount(int group, string? generator) => 0;

        public bool? TravelersMet(TravelersSpec spec) => false;

        public void WakeupEnemies(IReadOnlyList<string> names)
        {
        }

        public void WakeupTurrets(IReadOnlyList<string> patterns)
        {
        }

        public void WakeupZepTurrets(IReadOnlyList<string> nodes)
        {
        }

        public void WakeupGenerator(string name, int count)
        {
        }

        public void WakeAnim(string anim, string? node)
        {
        }

        public void PlaySoundGroup(string group)
        {
        }

        public void StopQueuedSounds(IReadOnlyList<string> names)
        {
        }

        public void WarpVehicle(string vehicle, IReadOnlyList<WarpPoint> points)
        {
        }

        public void SetAiTeam(IReadOnlyList<(string Name, int Team)> entries)
        {
        }

        public void SetAiNet(IReadOnlyList<(string Name, string Net)> entries)
        {
        }

        public void SetAiAttackRadius(IReadOnlyList<(string Name, float Radius)> entries)
        {
        }

        public void CompletedZepcannons(IReadOnlyList<(string Zeppelin, int Flag)> entries)
        {
        }

        public void CompletedStoppoint(IReadOnlyList<(string Net, int Stop, int Flag)> entries)
        {
        }

        public void StartTaxi(IReadOnlyList<string> names)
        {
        }
    }
}
