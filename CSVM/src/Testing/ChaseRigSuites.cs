using CSVM.Flight.Camera;
using Godot;

namespace CSVM.Testing;

/// <summary>The chase camera's settled pose, read off a live <see cref="CameraController"/> writing
/// a real <see cref="Camera3D"/>. On the shipped camparam default block the authored rig sits
/// 7.57° above the tail and aims along the nose, tilted up by the 0.29° pitch. Decode:
/// docs/org/cameraViews.md, "The chase rig".</summary>
internal static class ChaseRigSuites
{
    private const float Dt = 1f / 60f;
    private const float Speed = 100f;
    private const float DegTol = 0.05f;

    /// <summary>Snaps and flies the chase camera level and reads where it sat and looked.</summary>
    [Suite("chase-rig",
        "the chase camera rests on camparam's authored rig: 7.57 degrees above the tail looking along the nose 0.29 degrees up, both at the snap and after a second of chase steps")]
    internal static void ChaseRig(TestContext ctx)
    {
        var pose = Fly(ctx);

        ctx.Check(Mathf.Abs(pose.SnapElevation - 7.567f) < DegTol
            && Mathf.Abs(pose.FlownElevation - 7.567f) < DegTol,
            $"the chase camera sits {pose.SnapElevation:0.###} degrees above the tail at the snap and {pose.FlownElevation:0.###} after flying, against 7.567");
        ctx.Check(Mathf.Abs(pose.AimElevation - 0.29f) < DegTol,
            $"and looks along the nose tilted up by the authored pitch ({pose.AimElevation:0.###} degrees)");
        ctx.Note($"settled {pose.SnapElevation:0.###}/{pose.AimElevation:0.###} (offset/aim, degrees)");
    }

    // One level sortie at the origin on the shipped default block. It reads the snap, then a
    // second of chase steps, so the eased pose is pinned as well as the immediate one.
    private static Pose Fly(TestContext ctx)
    {
        var camera = new Camera3D { Name = "chase-rig" };
        ctx.Host.AddChild(camera);
        try
        {
            var cam = new CameraController(camera, new CamParams(), _ => false, 0);
            var attitude = Basis.Identity;
            cam.Snap(Vector3.Zero, attitude, Speed, new Transform3D(attitude, Vector3.Zero));
            float snap = ElevationDeg(camera.Position);
            for (int i = 0; i < 60; i++)
            {
                cam.Chase(Dt, Vector3.Zero, attitude);
            }
            return new Pose(snap, ElevationDeg(camera.Position), ElevationDeg(-camera.Basis.Z));
        }
        finally
        {
            camera.QueueFree();
        }
    }

    private static float ElevationDeg(Vector3 v) =>
        Mathf.RadToDeg(Mathf.Atan2(v.Y, Mathf.Sqrt((v.X * v.X) + (v.Z * v.Z))));

    private readonly record struct Pose(float SnapElevation, float FlownElevation, float AimElevation);
}
