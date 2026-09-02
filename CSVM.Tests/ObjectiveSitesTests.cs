using System.Collections.Generic;
using System.IO;
using CSVM.Flight;
using CSVM.Session;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="ObjectiveSites.CollectTargets"/>'s roster-marker source (BL-635,
/// docs/formats/ai-rosters.md aiv slot 37/39): a block that authors its own objective-target flag
/// is offered exactly the way a <c>targets.zrd</c>-flagged entry is, including being dropped once
/// a completed objective's own <c>REMOVE_OBJECTIVE_TARGET</c> names its key — CM11's OBJECTIVE1
/// does this for <c>secfury_5</c>/<c>secfury_6</c> without ever adding them, since their starting
/// flag is the roster's, not the script's, which is what <see cref="ObjectiveGraph.ObjectiveTargets"/>
/// alone would miss. Pinned off-engine: none of the three types here touch Godot.
/// </summary>
public class ObjectiveSitesTests
{
    [Fact]
    public void ARosterMarkerIsOfferedAlongsideTargetsZrdAndGraphAdds()
    {
        var script = Script("\"OBJECTIVE1\",null");
        var graph = new ObjectiveGraph(script, new FakeWorld());
        var targets = new MissionTargets();
        var roster = new Dictionary<string, string> { ["secfury_5"] = "MSG_OBJ_FOLLOW" };
        var into = new List<string>();

        ObjectiveSites.CollectTargets(script, graph, targets, into, roster);

        Assert.Contains("secfury_5", into);
    }

    [Fact]
    public void ARosterMarkerIsDroppedOnceItsOwnKeyIsRemovedByACompletedObjective()
    {
        var script = Script(
            "\"OBJECTIVE1\",[\"REMOVE_OBJECTIVE_TARGET\",[\"secfury_5\",\"secfury_6\"]]");
        var graph = new ObjectiveGraph(script, new FakeWorld());
        var roster = new Dictionary<string, string>
        {
            ["secfury_5"] = "MSG_OBJ_FOLLOW",
            ["secfury_6"] = "MSG_OBJ_FOLLOW",
        };

        // Before OBJECTIVE1 completes: both offered.
        var before = new List<string>();
        ObjectiveSites.CollectTargets(script, graph, new MissionTargets(), before, roster);
        Assert.Contains("secfury_5", before);
        Assert.Contains("secfury_6", before);

        // OBJECTIVE1 is conditionless, so it completes on the first tick, exactly like CM11's own
        // primary once its gate reads true.
        graph.Step(0.1f);
        Assert.True(graph.CompletedOf(1));

        var after = new List<string>();
        ObjectiveSites.CollectTargets(script, graph, new MissionTargets(), after, roster);
        Assert.DoesNotContain("secfury_5", after);
        Assert.DoesNotContain("secfury_6", after);
    }

    [Fact]
    public void ARosterMarkerNeverAddedByTheGraphIsStillOfferedAndStillRemovable()
    {
        // The shipped shape exactly: OBJECTIVE1 REMOVEs secfury_5/6 without any objective ever
        // ADD_OBJECTIVE_TARGETing them, so ObjectiveGraph.ObjectiveTargets stays empty throughout
        // and the roster source is the only reason either key is ever offered at all.
        var script = Script(
            "\"OBJECTIVE1\",[\"BEGIN_DORMANT\",[-1.0],\"INACTIVE1\",[\"never\"]],"
            + "\"OBJECTIVE2\",[\"REMOVE_OBJECTIVE_TARGET\",[\"secfury_5\"]]");
        var graph = new ObjectiveGraph(script, new FakeWorld());
        var roster = new Dictionary<string, string> { ["secfury_5"] = "MSG_OBJ_FOLLOW" };

        Assert.Empty(graph.ObjectiveTargets);
        var into = new List<string>();
        ObjectiveSites.CollectTargets(script, graph, new MissionTargets(), into, roster);
        Assert.Contains("secfury_5", into);

        graph.Step(0.1f);
        Assert.True(graph.CompletedOf(2));
        var after = new List<string>();
        ObjectiveSites.CollectTargets(script, graph, new MissionTargets(), after, roster);
        Assert.DoesNotContain("secfury_5", after);
    }

    [Fact]
    public void ARosterMarkerDoesNotDuplicateATargetsZrdEntryOfTheSameKey()
    {
        var script = Script("\"OBJECTIVE1\",null");
        var graph = new ObjectiveGraph(script, new FakeWorld());
        var targets = TargetsWithObjectiveFlag("secfury_5");
        var roster = new Dictionary<string, string> { ["secfury_5"] = "MSG_OBJ_FOLLOW" };

        var into = new List<string>();
        ObjectiveSites.CollectTargets(script, graph, targets, into, roster);

        Assert.Single(into, key => key == "secfury_5");
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
