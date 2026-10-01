using CSVM.Flight.Camera;
using CSVM.Spec;
using Godot;

namespace CSVM.Testing;

/// <summary>The two settled chase poses <c>--chase-rig=</c> chooses between. Both are read off a
/// live <see cref="CameraController"/> writing a real <see cref="Camera3D"/>. The hand-picked
/// default sits 15.7° above the tail and aims ahead of the nose. The authored rig, off the shipped
/// camparam block, sits 7.57° above it and aims along the nose. Decode:
/// docs/org/cameraViews.md, "The chase rig".</summary>
internal static class ChaseRigSuites
{
    private const float Dt = 1f / 60f;
    private const float Speed = 100f;
    private const float DegTol = 0.05f;

    /// <summary>Snaps and flies each rig level and reads where the camera sat and looked.</summary>
    [Suite("chase-rig",
        "--chase-rig parses to the rig it names and keeps the picked default otherwise; the picked rig settles 15.7 degrees above the tail looking down at a point ahead of the nose, and the authored rig 7.57 degrees above it looking along the nose 0.29 degrees up, both at the snap and after a second of chase steps")]
    internal static void ChaseRig(TestContext ctx)
    {
        ctx.Check(SessionSpec.Parse(new[] { "--fly" }).ChaseRig == Flight.Camera.ChaseRig.Picked
            && SessionSpec.Parse(new[] { "--fly", "--chase-rig=authored" }).ChaseRig == Flight.Camera.ChaseRig.Authored
            && SessionSpec.Parse(new[] { "--fly", "--chase-rig=sideways" }).ChaseRig == Flight.Camera.ChaseRig.Picked,
            $"--chase-rig=authored selects the authored rig, and no flag or an unknown word keeps the picked one");

        var picked = Fly(ctx, Flight.Camera.ChaseRig.Picked);
        var authored = Fly(ctx, Flight.Camera.ChaseRig.Authored);

        float pickedDeg = Mathf.RadToDeg(Mathf.Atan2(4.5f, 16f));
        ctx.Check(Mathf.Abs(picked.SnapElevation - pickedDeg) < DegTol
            && Mathf.Abs(picked.FlownElevation - pickedDeg) < DegTol,
            $"the picked rig sits {picked.SnapElevation:0.###} degrees above the tail at the snap and {picked.FlownElevation:0.###} after flying, against {pickedDeg:0.###}");
        ctx.Check(picked.AimElevation < -1f,
            $"and looks down at its point ahead of the nose ({picked.AimElevation:0.###} degrees)");

        ctx.Check(Mathf.Abs(authored.SnapElevation - 7.567f) < DegTol
            && Mathf.Abs(authored.FlownElevation - 7.567f) < DegTol,
            $"the authored rig sits {authored.SnapElevation:0.###} degrees above the tail at the snap and {authored.FlownElevation:0.###} after flying, against 7.567");
        ctx.Check(Mathf.Abs(authored.AimElevation - 0.29f) < DegTol,
            $"and looks along the nose tilted up by the authored pitch ({authored.AimElevation:0.###} degrees)");
        ctx.Note($"picked {picked.SnapElevation:0.###}/{picked.AimElevation:0.###}, authored {authored.SnapElevation:0.###}/{authored.AimElevation:0.###} (offset/aim, degrees)");
    }

    // One level sortie at the origin on the shipped default block. It reads the snap, then a
    // second of chase steps, so the eased pose is pinned as well as the immediate one.
    private static Pose Fly(TestContext ctx, Flight.Camera.ChaseRig rig)
    {
        var camera = new Camera3D { Name = $"chase-rig-{rig}" };
        ctx.Host.AddChild(camera);
        try
        {
            var cam = new CameraController(camera, new CamParams(), _ => false, 0, rig: rig);
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
