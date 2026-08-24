using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>Suites over the mid-mission world behaviours the shipped data drives: the area-selected
/// node toggle C3's story missions switch their map with, and the scripted-path follower that taxis
/// an authored vehicle off a runway and hands it to the flight model.</summary>
internal static class WorldFidelitySuites
{
    // The only chapter authoring the area verb, and the mission that switches the third area off.
    private const string AreaChapter = "C3";

    private const string AreaMission = "M01";

    // The chapter and mission whose roster puts four aircraft on takeoff paths, and the path the
    // first of them is given.
    private const string PathChapter = "C1";

    private const string PathMission = "M04";

    private const string PathName = "pp1";

    private const string PathVehicle = "blakepeace_2_3";

    // The three rectangles all 25 uses resolve to, as the scripts spell them.
    private static readonly (float X1, float Z1, float X2, float Z2)[] Areas =
    {
        (-10240f, -2048f, -2048f, -6144f),
        (-8192f, -6144f, -2048f, -8192f),
        (-15360f, -7168f, -8192f, -14336f),
    };

    internal static void PartitionAreas(TestContext ctx)
    {
        ctx.WithWorld(AreaChapter, collision: false, AreaMission, world =>
        {
            var grid = WorldPartitionGrid.Of(world.Gamez.FindByName("world1"));
            ctx.Check(grid != null, $"{AreaChapter} carries a usable partition grid");
            if (grid == null)
            {
                return;
            }

            var selections = new List<IReadOnlyList<int>>();
            for (int i = 0; i < Areas.Length; i++)
            {
                var (x1, z1, x2, z2) = Areas[i];
                var span = grid.CellsIn(x1, z1, x2, z2);
                var nodes = grid.NodesIn(x1, z1, x2, z2);
                selections.Add(nodes);
                ctx.Note($"area {i + 1} cells x[{span.ColMin},{span.ColMax}) z[{span.RowMin},{span.RowMax}) -> {nodes.Count} node(s)");
                ctx.Check(nodes.Count > 0, $"area {i + 1} selects world content");
                // The half-open rule: the rectangle's own maximum row and column are excluded, so a
                // rectangle that lands inside one cell selects nothing at all.
                ctx.Check(span.ColMax > span.ColMin && span.RowMax > span.RowMin,
                    $"area {i + 1} spans more than one cell on both axes");
            }

            ctx.Same(0, grid.NodesIn(-5000f, -5000f, -4900f, -4900f).Count,
                $"nodes a rectangle inside one {AreaChapter} cell selects");

            // Corner order is normalised by the engine, and C3 authors the second corner
            // below-left of the first on every one of the three rectangles.
            var (ax1, az1, ax2, az2) = Areas[0];
            ctx.Same(selections[0].Count, grid.NodesIn(ax2, az2, ax1, az1).Count,
                $"nodes area 1 selects with its corners swapped");

            int off = 0, on = 0;
            foreach (int idx in selections[2])
            {
                if (world.Runtime.FindNodeByIndex(idx) is { Visible: false })
                {
                    off++;
                }
            }

            foreach (int idx in selections[0])
            {
                if (world.Runtime.FindNodeByIndex(idx) is { Visible: true })
                {
                    on++;
                }
            }

            ctx.Note($"{AreaMission}: {off} of {selections[2].Count} area-3 node(s) switched off, {on} of {selections[0].Count} area-1 node(s) left on");
            ctx.Check(off > 0, $"{AreaMission}'s area-3 'off' reached built nodes");
            ctx.Check(on > 0, $"{AreaMission} leaves area 1 alone");
        });
    }

    internal static void ScriptedPathTaxi(TestContext ctx)
    {
        ctx.WithWorld(PathChapter, collision: false, PathMission, world =>
        {
            var path = ScriptedPath.Resolve(PathName, name => world.Runtime.FindNodes(name));
            ctx.Check(path != null, $"{PathChapter} carries the authored path '{PathName}'");
            if (path == null)
            {
                return;
            }

            ctx.Note($"{PathName}: {path.Waypoints.Count} waypoint(s), first {path.Waypoints[0]}, last {path.Waypoints[^1]}");
            var body = new Node3D { Name = "taxi_body" };
            world.Stage.AddChild(body);
            body.GlobalPosition = path.Waypoints[0];
            var registry = new ScriptedPathVehicles(name => world.Runtime.FindNodes(name));
            float handoffSpeed = -1f;
            ctx.Check(registry.Place(PathVehicle, PathName, body, s => handoffSpeed = s),
                $"'{PathVehicle}' is placed on its authored path");

            // Placed and waiting: the spawner's freeze holds it on the first waypoint until a
            // mission goal releases it, however long the mission steps.
            var parked = body.GlobalPosition;
            for (int i = 0; i < 60; i++)
            {
                registry.Step(1f / 30f);
            }

            ctx.Check(registry.IsFrozen(PathVehicle), $"'{PathVehicle}' is frozen while placed");
            ctx.Check(body.GlobalPosition.IsEqualApprox(parked),
                $"'{PathVehicle}' does not move while frozen");

            ctx.Check(registry.Release(PathVehicle), $"START_TAXI releases '{PathVehicle}'");
            var before = body.GlobalPosition;
            registry.Step(1f / 30f);
            float rolled = before.DistanceTo(body.GlobalPosition) * 30f;
            ctx.Note($"first released tick: {rolled:F2} m/s of a {PathFollower.TaxiSpeed:F2} m/s taxi");
            ctx.Check(rolled > 0f && rolled <= PathFollower.TaxiSpeed + 0.01f,
                $"'{PathVehicle}' rolls on release, never faster than the taxi speed");

            float peak = 0f, topY = body.GlobalPosition.Y;
            for (int i = 0; i < 6000 && registry.Count > 0; i++)
            {
                var was = body.GlobalPosition;
                registry.Step(1f / 30f);
                peak = Mathf.Max(peak, was.DistanceTo(body.GlobalPosition) * 30f);
                topY = Mathf.Max(topY, body.GlobalPosition.Y);
            }

            ctx.Note($"run: peak {peak:F2} m/s, climbed to y={topY:F1} from y={path.Waypoints[0].Y:F1}, handoff at {handoffSpeed:F2} m/s");
            ctx.Same(0, registry.Count, $"vehicles left on a path '{PathName}' has finished");
            ctx.Check(handoffSpeed > PathFollower.TaxiSpeed,
                $"the final leg hands '{PathVehicle}' over above the taxi speed");
            ctx.Check(peak > PathFollower.TaxiSpeed,
                $"'{PathVehicle}' really moves faster on the final leg than while taxiing");
            ctx.Check(topY > path.Waypoints[0].Y,
                $"the final leg's climb term lifts '{PathVehicle}' off the strip");
            body.QueueFree();
        });
    }
}
