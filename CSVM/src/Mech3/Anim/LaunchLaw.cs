using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>
/// The OBJECT_MOTION launch law, engine-free so the unit tests can pin it: where a body stands
/// after <c>elapsed</c> seconds from the pose it was seeded at, under the event's <c>initial</c>
/// velocity and <c>delta</c> acceleration. The seed is the node's LIVE pose, never its authored
/// rest, because the original's update writes velocities only and adds each step to the node's
/// own translation (docs/org/objectMotion.md), so a chain of events on one node continues each
/// leg from the last leg's end. <see cref="MotionRuntime"/> is the one caller.
/// </summary>
public static class LaunchLaw
{
    public static Vector3 Origin(Vector3 from, Vector3 v0, Vector3 accel, float elapsed) =>
        from + v0 * elapsed + 0.5f * elapsed * elapsed * accel;
}
