using System.Collections.Generic;
using CSVM.Extraction;
using CSVM.Flight.Modes;
using CSVM.Testing;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The start count a stunt run opens with: its figures and their beats, GO, and the controls held
/// until GO has handed over. Its walk ends on the spawn pose whatever the spawn speed. The walk is
/// driven over two real missions' spawns whose authored speeds differ, so no one mission's number
/// can stand in for the rule.
/// </summary>
public class StartCountTests
{
    private const float Dt = 1f / 60f;

    [ExtractedDataFact]
    public void The_walk_ends_on_the_spawn_pose_bit_for_bit_at_any_spawn_speed()
    {
        var slow = SpawnOf("C1", "IA1");
        var fast = SpawnOf("C1C", "M01");
        Assert.NotEqual(slow.Speed, fast.Speed);
        foreach (var (pos, attitude, speed) in new[] { slow, fast })
        {
            var count = new StartCount();
            count.Begin(StartCount.Rerun);
            var (first, firstAttitude) = count.WalkPose(pos, attitude, speed);
            Assert.Equal(speed * count.Duration, first.DistanceTo(pos), 2);
            Assert.True((pos - first).Normalized().Dot(-attitude.Z) > 0.9999f,
                $"the walk starts behind the spawn along its nose ({speed:0.#} m/s)");
            Assert.Equal(attitude, firstAttitude);

            var last = first;
            while (count.Running)
            {
                count.Advance(Dt);
                var (at, walkAttitude) = count.WalkPose(pos, attitude, speed);
                if (count.Running)
                {
                    // The walk moves at the spawn speed, the velocity the flight model takes at GO.
                    Assert.Equal(speed * Dt, at.DistanceTo(last), 2);
                    Assert.Equal(attitude, walkAttitude);
                }
                last = at;
            }

            var (goPos, goAttitude) = count.WalkPose(pos, attitude, speed);
            Assert.True(goPos == pos && goAttitude == attitude,
                $"GO is the spawn pose exactly at {speed:0.#} m/s: {goPos} against {pos}");
            Assert.Equal(speed * Dt, pos.DistanceTo(PoseOneStepBeforeGo(pos, attitude, speed)), 2);
        }
    }

    [Fact]
    public void The_rerun_count_beats_three_two_one_a_second_apart_then_goes()
    {
        var count = new StartCount();
        count.Begin(StartCount.Rerun);
        Assert.Equal("3", count.Figure);
        var cues = Run(count, out int goStep);
        Assert.Equal(new[] { (1, "3"), (60, "2"), (120, "1") }, cues);
        Assert.Equal(180, goStep);
        Assert.False(count.Running);
        Assert.Equal(StartCount.GoLabel, count.Figure);

        // GO stands for its own length after the hand-over, then the figure is gone.
        int stood = 0;
        while (count.Figure != null)
        {
            Assert.Equal(StartCountCue.None, count.Advance(Dt));
            stood++;
        }
        Assert.InRange(stood, Mathf.RoundToInt(StartCount.GoSeconds / Dt) - 1, Mathf.RoundToInt(StartCount.GoSeconds / Dt) + 1);
    }

    [Fact]
    public void The_opening_count_stands_ready_first_and_goes_after_its_whole_length()
    {
        var count = new StartCount();
        count.Begin(StartCount.Opening(2f));
        Assert.Equal(5f, count.Duration, 4);
        Assert.Equal("READY", count.Figure);
        var cues = Run(count, out int goStep);
        Assert.Equal(new[] { (1, "READY"), (120, "3"), (180, "2"), (240, "1") }, cues);
        Assert.Equal(300, goStep);
    }

    [Fact]
    public void The_controls_are_held_through_the_go_step_and_released_on_the_step_after()
    {
        // A seat reads Running before it steps, as FlightController does.
        var count = new StartCount();
        count.Begin(StartCount.Rerun);
        int held = 0;
        StartCountCue cue;
        do
        {
            Assert.True(count.Running, $"step {held + 1} is still the count's");
            held++;
            cue = count.Advance(Dt);
        }
        while (cue != StartCountCue.Go);
        Assert.Equal(180, held);
        Assert.False(count.Running);
    }

    [Fact]
    public void A_cancelled_count_holds_nothing_and_shows_nothing()
    {
        var count = new StartCount();
        count.Begin(StartCount.Rerun);
        count.Advance(Dt);
        count.Cancel();
        Assert.False(count.Running);
        Assert.Null(count.Figure);
        Assert.Equal(0f, count.Remaining);
        var pos = new Vector3(10f, 300f, -40f);
        Assert.Equal(pos, count.WalkPose(pos, Basis.Identity, 50f).Position);
    }

    // Steps a begun count to GO, returning each beat as (step, figure shown) and GO's step.
    private static List<(int Step, string Figure)> Run(StartCount count, out int goStep)
    {
        var cues = new List<(int, string)>();
        for (int step = 1; step < 10000; step++)
        {
            var cue = count.Advance(Dt);
            if (cue == StartCountCue.Go)
            {
                goStep = step;
                return cues;
            }
            if (cue == StartCountCue.Beat)
                cues.Add((step, count.Figure!));
        }
        goStep = -1;
        return cues;
    }

    // The walk's pose on the last step before GO, from a fresh count stepped that far.
    private static Vector3 PoseOneStepBeforeGo(Vector3 pos, Basis attitude, float speed)
    {
        var count = new StartCount();
        count.Begin(StartCount.Rerun);
        for (int step = 1; step < 180; step++)
            count.Advance(Dt);
        Assert.True(count.Running);
        return count.WalkPose(pos, attitude, speed).Position;
    }

    // A mission's own spawn, attitude and speed, read the way the session reads them.
    private static (Vector3 Pos, Basis Attitude, float Speed) SpawnOf(string chapter, string mission)
    {
        var start = SpawnPoints.LoadPlayerInit(SessionPaths.MissionZrdr(TestData.DataRoot!, chapter, mission));
        Assert.True(start.HasValue, $"{chapter}/{mission} authors a PLAYER_INIT");
        var spawn = start!.Value.Spawn;
        var lookAt = spawn.Position + spawn.Forward;
        return (spawn.Position, Basis.LookingAt((lookAt - spawn.Position).Normalized(), Vector3.Up), start.Value.SpeedMps);
    }
}
