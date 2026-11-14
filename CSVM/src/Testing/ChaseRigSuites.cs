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

    /// <summary>Flies the eased chase rig through a banked turn and a loop on the level frame. The
    /// same turn on the original frame is the contrast.</summary>
    [Suite("chase-level",
        "the level chase camera keeps its right axis horizontal through a 60 degree turn from the first frame after a banked snap, follows a loop over the top upside down, and never turns more than 5 degrees a frame; the original chase banks with the turn")]
    internal static void ChaseLevel(TestContext ctx)
    {
        var camera = new Camera3D { Name = "chase-level" };
        ctx.Host.AddChild(camera);
        try
        {
            var cam = new CameraController(camera, new CamParams(), _ => false, 0);
            var turn = Fly(cam, camera, 3f, t => new Basis(Vector3.Up, -0.35f * t) * new Basis(Vector3.Back, -1.0472f), true);
            ctx.Check(turn.WorstTilt < 1e-3f, $"the level chase's right axis rises at most {turn.WorstTilt:0.#####} through a 60 degree turn");
            var loop = Fly(cam, camera, 4f, t => new Basis(Vector3.Right, Mathf.DegToRad(90f) * t), true);
            ctx.Check(loop.TopUp < -0.9f, $"over the top of the loop the view's up points {loop.TopUp:0.###} (down is -1)");
            float worst = Mathf.Max(turn.WorstStepDeg, loop.WorstStepDeg);
            ctx.Check(worst < 5f, $"the camera turns at most {worst:0.###} degrees a frame");
            var rolled = Fly(cam, camera, 3f, t => new Basis(Vector3.Up, -0.35f * t) * new Basis(Vector3.Back, -1.0472f), false);
            ctx.Check(rolled.WorstTilt > 0.8f, $"the original chase banks with the turn (right axis height {rolled.WorstTilt:0.###})");
            ctx.Note($"level turn tilt {turn.WorstTilt:0.#####}, loop top up {loop.TopUp:0.###}, worst step {worst:0.###} deg");
        }
        finally
        {
            camera.QueueFree();
        }
    }

    // A snapped sortie flown for the given seconds, read every frame off the camera itself. The
    // loop's top is read at its midpoint, two seconds in.
    private static Sortie Fly(CameraController cam, Camera3D camera, float seconds, System.Func<float, Basis> at, bool level)
    {
        // A seat chases on its own frame before it respawns, so the snap settles on that frame.
        cam.Chase(0f, Vector3.Zero, Basis.Identity, level: level);
        cam.Snap(Vector3.Zero, at(0f), Speed, new Transform3D(at(0f), Vector3.Zero));
        var last = camera.Basis.GetRotationQuaternion();
        float tilt = Mathf.Abs(camera.Basis.X.Normalized().Y), step = 0f, topUp = 1f;
        for (int i = 0; i * Dt <= seconds; i++)
        {
            cam.Chase(Dt, Vector3.Zero, at(i * Dt), level: level);
            var now = camera.Basis.GetRotationQuaternion();
            tilt = Mathf.Max(tilt, Mathf.Abs(camera.Basis.X.Normalized().Y));
            step = i == 0 ? step : Mathf.Max(step, Mathf.RadToDeg(last.AngleTo(now)));
            last = now;
            if (i == 120)
            {
                topUp = camera.Basis.Y.Normalized().Y;
            }
        }

        return new Sortie(tilt, step, topUp);
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

    private readonly record struct Sortie(float WorstTilt, float WorstStepDeg, float TopUp);

    private readonly record struct Pose(float SnapElevation, float FlownElevation, float AimElevation);
}
