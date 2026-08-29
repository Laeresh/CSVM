using CSVM.Mech3.Anim;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The OBJECT_MOTION launch law (<see cref="LaunchLaw.Origin"/>) over a chain of
/// events on one node, against the decode in docs/org/objectMotion.md: the original's update
/// writes velocities only and adds each step to the node's live translation, so every leg of a
/// chain continues from where the last one ended. The numbers are C3/M03's <c>sub_movement</c>,
/// read straight from the extracted program.
/// </summary>
public sealed class MotionChainTests
{
    private static readonly Vector3 Start = new(-12032f, -6f, -13197.5f);
    private static readonly Vector3 BayPlacement = new(-12032f, -6f, -11516.288f);

    // (initial, delta, run_time) as authored: accelerate, cruise, brake.
    private static readonly (Vector3 V0, Vector3 Accel, float Seconds)[] Legs =
    {
        (Vector3.Zero, new Vector3(0f, 0f, 20f), 2f),
        (new Vector3(0f, 0f, 40f), Vector3.Zero, 40f),
        (new Vector3(0f, 0f, 40f), new Vector3(0f, 0f, -20f), 2f),
    };

    [Fact]
    public void A_chain_continues_each_leg_from_the_last_legs_end()
    {
        var pos = Start;
        foreach (var (v0, accel, seconds) in Legs)
        {
            pos = LaunchLaw.Origin(pos, v0, accel, seconds);
        }

        // 40 + 1600 + 40 = 1680 m along +Z, 1.2 m short of the closing FromTo's authored point.
        Assert.Equal(Start.Z + 1680f, pos.Z, 2);
        Assert.Equal(Start.X, pos.X, 3);
        Assert.Equal(Start.Y, pos.Y, 3);
        Assert.InRange(pos.DistanceTo(BayPlacement), 0f, 2f);
    }

    [Fact]
    public void A_leg_reseated_on_the_authored_rest_pose_is_the_reported_jump()
    {
        // The gamez places the hull at the map origin; a leg seeded there runs 17.8 km away.
        var reseated = LaunchLaw.Origin(Vector3.Zero, Legs[1].V0, Legs[1].Accel, 0f);
        Assert.True(reseated.DistanceTo(Start) > 17000f);
    }

    [Fact]
    public void Delta_is_an_acceleration_not_a_ramp_over_the_run_time()
    {
        var end = LaunchLaw.Origin(Vector3.Zero, Vector3.Zero, new Vector3(0f, 0f, 20f), 2f);
        Assert.Equal(40f, end.Z, 3);
        var mid = LaunchLaw.Origin(Vector3.Zero, Vector3.Zero, new Vector3(0f, 0f, 20f), 1f);
        Assert.Equal(10f, mid.Z, 3);
    }
}
