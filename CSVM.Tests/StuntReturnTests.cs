using System.IO;
using CSVM.Extraction;
using CSVM.Flight.Modes;
using CSVM.Mech3;
using CSVM.Testing;
using CSVM.Utils;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Where a tap of respawn returns a stunt pilot. Before any zone is cleared there is no return of
/// its own. After, it is on the route abeam the exit gate of the zone cleared LAST, heading the
/// way it was flown. Run over C1/IA1's real gates and ribbons. Its five gate pairs all sit 186 m or more
/// apart, so the exit and the entry cannot be mistaken for each other.
/// </summary>
public class StuntReturnTests
{
    [ExtractedDataFact]
    public void No_zone_cleared_has_no_return_of_its_own()
    {
        var mission = Load();
        Assert.Null(mission.ReturnPose());
    }

    [ExtractedDataFact]
    public void One_zone_returns_abeam_its_exit_heading_the_way_it_was_flown()
    {
        var forward = Load();
        var backward = forward.ForAnotherPlayer();
        Fly(forward, forward.Zones[2], reverse: false);
        Fly(backward, backward.Zones[2], reverse: true);

        var zone = forward.Zones[2];
        var greenToRed = (zone.RedGate.Center - zone.GreenGate.Center).Normalized();
        var (pos, heading) = forward.ReturnPose()!.Value;
        var (backPos, backHeading) = backward.ReturnPose()!.Value;

        // Green then red leaves through red, and the reverse through green.
        Assert.True(pos.DistanceTo(zone.RedGate.Center) < pos.DistanceTo(zone.GreenGate.Center),
            $"flown green to red, the return sits at the red gate: {pos.DistanceTo(zone.RedGate.Center):0.0} m from it, {pos.DistanceTo(zone.GreenGate.Center):0.0} m from green");
        Assert.True(backPos.DistanceTo(zone.GreenGate.Center) < backPos.DistanceTo(zone.RedGate.Center),
            $"flown red to green, the return sits at the green gate: {backPos.DistanceTo(zone.GreenGate.Center):0.0} m from it");
        Assert.True(heading.Dot(greenToRed) > 0f && backHeading.Dot(greenToRed) < 0f,
            $"each return heads on the way its zone was flown: {heading.Dot(greenToRed):0.00} and {backHeading.Dot(greenToRed):0.00} along green to red");
        Assert.InRange(heading.Length(), 0.999f, 1.001f);
    }

    [ExtractedDataFact]
    public void Three_zones_flown_out_of_order_return_through_the_last_flown()
    {
        var mission = Load();
        var alone = mission.ForAnotherPlayer();
        var thirdAlone = mission.ForAnotherPlayer();
        var firstAlone = mission.ForAnotherPlayer();

        // Flown 3, 1, 2: list order and flown order disagree, and the last flown is zone 2.
        Fly(mission, mission.Zones[2], reverse: false);
        Fly(mission, mission.Zones[0], reverse: false);
        Fly(mission, mission.Zones[1], reverse: false);
        Assert.Equal(3, mission.CompletedCount);
        Assert.Equal(2, mission.Zones[1].CompletionOrder);

        Fly(alone, alone.Zones[1], reverse: false);
        Fly(thirdAlone, thirdAlone.Zones[2], reverse: false);
        Fly(firstAlone, firstAlone.Zones[0], reverse: false);

        var pose = mission.ReturnPose()!.Value;
        var expected = alone.ReturnPose()!.Value;
        Assert.True(pose.Position.DistanceTo(expected.Position) < 1e-3f && pose.Heading.Dot(expected.Heading) > 0.9999f,
            $"the return is zone 2's own exit, as if it were the only zone cleared: {pose.Position} against {expected.Position}");
        Assert.True(pose.Position.DistanceTo(thirdAlone.ReturnPose()!.Value.Position) > 100f,
            "…not zone 3's, the first flown");
        Assert.True(pose.Position.DistanceTo(firstAlone.ReturnPose()!.Value.Position) > 100f,
            "…nor zone 1's, the first in list order");
    }

    [ExtractedDataFact]
    public void A_rerun_has_no_return_left()
    {
        var mission = Load();
        Fly(mission, mission.Zones[0], reverse: false);
        Assert.NotNull(mission.ReturnPose());
        mission.Reset();
        Assert.Null(mission.ReturnPose());
    }

    private static StuntMission Load()
    {
        var mission = StuntMission.Load(
            GameZ.Load(SessionPaths.ChapterGamez(TestData.DataRoot!, "C1")),
            SessionPaths.MissionZrdr(TestData.DataRoot!, "C1", "IA1"),
            Messages.Load(Path.Combine(TestData.ExtractedRoot!, "messages.json")));
        Assert.True(mission is { TotalCount: 5 }, "C1/IA1 loads its five danger zones");
        return mission!;
    }

    // Both gates of one zone, green first unless reversed, each crossed along its own axis in the one
    // direction of travel. Each crossing starts a fresh segment so the jump between gates crosses nothing.
    private static void Fly(StuntMission mission, StuntZone zone, bool reverse)
    {
        var travel = (zone.RedGate.Center - zone.GreenGate.Center).Normalized() * (reverse ? -1f : 1f);
        Cross(mission, reverse ? zone.RedGate : zone.GreenGate, travel);
        Cross(mission, reverse ? zone.GreenGate : zone.RedGate, travel);
        Assert.True(zone.Completed, $"{zone.PathName} completes once both gates are crossed");
    }

    private static void Cross(StuntMission mission, StuntGate gate, Vector3 travel)
    {
        var along = gate.Normal * (gate.Normal.Dot(travel) < 0f ? -20f : 20f);
        mission.Relocated();
        mission.Update(gate.Center - along);
        mission.Update(gate.Center + along);
    }
}
