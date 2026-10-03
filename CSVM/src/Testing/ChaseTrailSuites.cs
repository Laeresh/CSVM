using System.Collections.Generic;
using System.Text;
using CSVM.Flight.Camera;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>How the chase camera trails a roll in each presentation, driven straight through
/// <see cref="CameraController"/> rather than through a built world. Both presentations take the
/// decoded <c>pos_catch_up</c>/<c>look_catch_up</c> ease, so each lag is read against that law,
/// beside the enhanced widening of the external FOV. The faithful roll is also flown without the
/// host's cue step. The two poses must agree to the bit, since every pinned golden is that
/// presentation's image. Decode: docs/org/cameraViews.md, "The chase rig".</summary>
internal static class ChaseTrailSuites
{
    private const float Dt = 1f / 60f;

    // A cruising speed for the radius law; the trail is an angle, so the figure only has to keep
    // the camera off the aeroplane.
    private const float Speed = 100f;

    // A roll fast enough that the lag is tens of degrees and slow enough that the settled pose is
    // reached inside the hold below: 90°/s over one second, then two seconds wings-level.
    private const float RollRateDeg = 90f;
    private const int RollFrames = 60;
    private const int HoldFrames = 120;

    // The airframe-independent speed scale the widening is quoted on (fd_speed fractions), and a
    // cruise the ramp must leave alone.
    private const float RatedMaxFrac = 1f;
    private const float CruiseFrac = 0.6f;

    // Where the settled pose has to be reached to. The slower faithful catch-up leaves under a
    // hundredth of its lag after two seconds, so more is a camera that never sprang back.
    private const float SettledToleranceDeg = 0.5f;

    // How close the measured lags must sit to the ones the decoded ease predicts. The offset lag
    // reads about 4.1° and the aim about 21.7°. An exponential fraction in place of the linear one
    // reads 0.07° further. A 4/s attitude lag in place of the ease reads 1.3° nearer, and one
    // stacked on it degrees further. A hundredth therefore separates every wrong law from the right one.
    private const float LawToleranceDeg = 0.01f;

    /// <summary>Flies one scripted roll per presentation and reads the camera it left.</summary>
    [Suite("chase-trail",
        "the chase camera's trail through a roll: both presentations ease the offset and aim at camparam's pos_catch_up and look_catch_up scaled by the rig's swing factor, the enhanced camera with no lag of its own in place of that ease or on top of it, each lag matching what the decoded law predicts, and both spring back to the settled pose; the external FOV widens toward the airframe's rated max under Enhanced alone and is untouched at cruise, and under the faithful presentation the same scripted roll writes the same camera pose and FOV to the bit whether or not the host steps the cues")]
    internal static void ChaseTrail(TestContext ctx)
    {
        bool wasEnhanced = GraphicsMode.Enhanced;
        var report = new StringBuilder();
        try
        {
            var stepped = Fly(ctx, GraphicsMode.Default, stepCues: true, report);
            var unstepped = Fly(ctx, GraphicsMode.Default, stepCues: false, report);
            var enhanced = Fly(ctx, GraphicsMode.EnhancedWord, stepCues: true, report);

            // The faithful path: the cue step is the only new call on it, so the pose it leaves
            // must be the pose the pre-change order left, frame for frame and bit for bit.
            int moved = 0;
            for (int i = 0; i < stepped.Poses.Count; i++)
            {
                if (stepped.Poses[i] != unstepped.Poses[i])
                {
                    moved++;
                }
            }
            ctx.Same(0, moved,
                $"frames of the faithful roll whose camera pose the cue step moved, out of {stepped.Poses.Count}");
            ctx.Check(stepped.SettledFov == unstepped.SettledFov
                && stepped.SettledFov == stepped.BaseFov,
                $"and the faithful external FOV is the decoded base angle ({stepped.SettledFov:0.0000}° against {stepped.BaseFov:0.0000}°)");

            // The teeth on the check above: the roll has to move the pose at all. A camera that
            // never followed the aeroplane would pass it.
            ctx.Check(stepped.Poses[0] != stepped.Poses[^1],
                $"while the roll itself moves the faithful pose across those {stepped.Poses.Count} frames");

            // Enhanced adds no lag of its own, so its pose is the faithful one frame for frame.
            // The widening it does add is a FOV, which the transform does not carry.
            int enhancedMoved = 0;
            for (int i = 0; i < stepped.Poses.Count; i++)
            {
                if (stepped.Poses[i] != enhanced.Poses[i])
                {
                    enhancedMoved++;
                }
            }
            ctx.Same(0, enhancedMoved,
                $"frames of the roll whose camera pose the enhanced presentation moved off the faithful one, out of {stepped.Poses.Count}");

            // Both presentations trail at the decoded law and no other: the catch-up rates times
            // the swing factor. Nothing stands in place of that ease or on top of it.
            var cam = new CamParams();
            float scale = CameraController.CatchUpScale(
                CameraController.AuthoredRig(0f, 0f, cam.ThirdpHeight, cam.ThirdpPitchRad).Offset);
            float posLaw = PredictedLagDeg(Mathf.Min(cam.PosCatchUp * scale * Dt, 1f), stepped.SettledDir);
            float lookLaw = PredictedLagDeg(Mathf.Min(cam.LookCatchUp * scale * Dt, 1f), null);
            ctx.Check(Mathf.Abs(stepped.RolledLagDeg - posLaw) < LawToleranceDeg,
                $"the faithful camera's offset trails the roll by {stepped.RolledLagDeg:0.###}°, the decoded pos_catch_up {cam.PosCatchUp:0.#}/s x {scale:0.####} predicting {posLaw:0.###}°");
            ctx.Check(Mathf.Abs(stepped.RolledAimLagDeg - lookLaw) < LawToleranceDeg,
                $"and its aim by {stepped.RolledAimLagDeg:0.###}°, the decoded look_catch_up {cam.LookCatchUp:0.#}/s x {scale:0.####} predicting {lookLaw:0.###}°");
            ctx.Check(Mathf.Abs(enhanced.RolledLagDeg - posLaw) < LawToleranceDeg
                && Mathf.Abs(enhanced.RolledAimLagDeg - lookLaw) < LawToleranceDeg,
                $"the enhanced camera trails by {enhanced.RolledLagDeg:0.###}° offset and {enhanced.RolledAimLagDeg:0.###}° aim, the same decoded ease predicting {posLaw:0.###}° and {lookLaw:0.###}°");
            ctx.Check(enhanced.HalfwayLagDeg < enhanced.RolledLagDeg
                && enhanced.SettledLagDeg < enhanced.HalfwayLagDeg,
                $"and converges once the roll stops ({enhanced.RolledLagDeg:0.#}° to {enhanced.HalfwayLagDeg:0.#}° to {enhanced.SettledLagDeg:0.###}°)");
            ctx.Check(enhanced.SettledLagDeg < SettledToleranceDeg
                && stepped.SettledLagDeg < SettledToleranceDeg,
                $"back onto the settled pose both presentations share (enhanced {enhanced.SettledLagDeg:0.###}°, faithful {stepped.SettledLagDeg:0.###}°)");

            // The widening: the law, then what the camera is actually left carrying.
            ctx.Check(CameraController.SpeedFovWiden(0f) == 0f
                && CameraController.SpeedFovWiden(CruiseFrac) == 0f,
                $"nothing widens the view at or below {CruiseFrac:0.##} of rated max");
            float widen = CameraController.SpeedFovWiden(RatedMaxFrac);
            ctx.Check(widen > 0f && CameraController.SpeedFovWiden(1.4f) == widen,
                $"the widening reaches {widen:0.##}° at rated max and is held there in a dive past it");
            ctx.Check(enhanced.RatedMaxFov == enhanced.BaseFov + widen,
                $"so the enhanced external FOV at rated max is the decoded {enhanced.BaseFov:0.00}° plus it ({enhanced.RatedMaxFov:0.0000}°)");
            ctx.Check(enhanced.CruiseFov == enhanced.BaseFov,
                $"and at cruise it is the decoded angle exactly ({enhanced.CruiseFov:0.0000}°)");
            ctx.Check(stepped.RatedMaxFov == stepped.BaseFov,
                $"while the faithful presentation holds the decoded angle at rated max ({stepped.RatedMaxFov:0.0000}°)");
            ctx.Check(enhanced.CrashCutFov == enhanced.BaseFov,
                $"and the crash cut keeps the decoded angle whatever speed the aeroplane was carrying ({enhanced.CrashCutFov:0.0000}°)");

            ctx.WriteArtifact("test-chase-trail.txt", report.ToString());
            ctx.Note($"flew one {RollRateDeg:0}°/s roll per presentation and read the camera it left");
        }
        finally
        {
            GraphicsMode.Resolve(wasEnhanced ? GraphicsMode.EnhancedWord : GraphicsMode.Default);
        }
    }

    // One scripted sortie: snap to the settled pose, roll at a constant rate, then hold the
    // attitude while the camera catches up, reading the trail off the offset the camera sits at.
    // The aeroplane is held at the origin, so the offset IS the camera's own position.
    private static Sortie Fly(TestContext ctx, string mode, bool stepCues, StringBuilder report)
    {
        GraphicsMode.Resolve(mode);
        var camera = new Camera3D { Name = $"chase-trail-{mode}-{stepCues}" };
        ctx.Host.AddChild(camera);
        var sortie = new Sortie { BaseFov = CameraController.ExternalFovDeg };
        try
        {
            var cam = new CameraController(camera, new CamParams(), _ => false, 0);
            var attitude = Basis.Identity;
            cam.Snap(Vector3.Zero, attitude, Speed, new Transform3D(attitude, Vector3.Zero));
            // The settled offset direction in the PLANE's frame, read off the settle-immediately
            // path rather than restated here, so the trail is measured against the camera's own
            // idea of where it belongs.
            var settledDir = camera.Position.Normalized();
            var settledAim = camera.Basis;
            sortie.SettledDir = settledDir;

            for (int i = 1; i <= RollFrames; i++)
            {
                attitude = new Basis(Vector3.Forward, Mathf.DegToRad(RollRateDeg * i * Dt));
                Step(cam, attitude, stepCues, RatedMaxFrac);
                sortie.Poses.Add(camera.Transform);
            }
            sortie.RolledLagDeg = LagDeg(camera, attitude, settledDir);
            sortie.RolledAimLagDeg = TurnDeg((attitude * settledAim).Inverse() * camera.Basis);
            sortie.RatedMaxFov = camera.Fov;

            for (int i = 1; i <= HoldFrames; i++)
            {
                Step(cam, attitude, stepCues, RatedMaxFrac);
                if (i == HoldFrames / 4)
                {
                    sortie.HalfwayLagDeg = LagDeg(camera, attitude, settledDir);
                }
            }
            sortie.SettledLagDeg = LagDeg(camera, attitude, settledDir);
            sortie.SettledFov = camera.Fov;

            Step(cam, attitude, stepCues, CruiseFrac);
            sortie.CruiseFov = camera.Fov;
            Step(cam, attitude, stepCues, RatedMaxFrac);
            cam.CrashView(Vector3.Zero, -attitude.Z);
            sortie.CrashCutFov = camera.Fov;

            report.AppendLine($"{mode} cues={stepCues}: rolled {sortie.RolledLagDeg:0.####}° (aim {sortie.RolledAimLagDeg:0.####}°), holding {sortie.HalfwayLagDeg:0.##}°, settled {sortie.SettledLagDeg:0.####}°, fov base {sortie.BaseFov:0.0000}° rated-max {sortie.RatedMaxFov:0.0000}° cruise {sortie.CruiseFov:0.0000}° crash {sortie.CrashCutFov:0.0000}°");
            return sortie;
        }
        finally
        {
            camera.QueueFree();
        }
    }

    // One frame in the order FlightController runs it: the cues, the external FOV write, the pose.
    private static void Step(CameraController cam, Basis attitude, bool stepCues, float speedFrac)
    {
        if (stepCues)
        {
            cam.StepEnhancedCues(speedFrac);
        }
        cam.RestoreExternalFov();
        cam.Chase(Dt, Vector3.Zero, attitude);
    }

    // How far the camera's offset direction trails the one the settled pose would put it at.
    private static float LagDeg(Camera3D camera, Basis attitude, Vector3 settledDir) =>
        Mathf.RadToDeg(camera.Position.Normalized().AngleTo(attitude * settledDir));

    // The angle a basis turns through, in degrees.
    private static float TurnDeg(Basis b) =>
        Mathf.RadToDeg(2f * Mathf.Acos(Mathf.Min(1f, Mathf.Abs(b.GetRotationQuaternion().W))));

    // The lag a frame eased by `fraction` a step leaves behind the scripted roll, from the law alone.
    // The frame and the roll share one axis, so each step closes that fraction of the angle gap.
    // With a direction it returns how far that direction swings; without, the frame's own turn.
    private static float PredictedLagDeg(float fraction, Vector3? dir)
    {
        float step = Mathf.DegToRad(RollRateDeg) * Dt, frame = 0f, roll = 0f;
        for (int i = 1; i <= RollFrames; i++)
        {
            roll = step * i;
            frame += fraction * (roll - frame);
        }
        float lag = roll - frame;
        return dir is { } d
            ? Mathf.RadToDeg(d.AngleTo(new Basis(Vector3.Forward, lag) * d))
            : Mathf.RadToDeg(lag);
    }

    private sealed class Sortie
    {
        public List<Transform3D> Poses { get; } = new List<Transform3D>();

        public float BaseFov { get; init; }

        public Vector3 SettledDir { get; set; }

        public float RolledLagDeg { get; set; }

        public float RolledAimLagDeg { get; set; }

        public float HalfwayLagDeg { get; set; }

        public float SettledLagDeg { get; set; }

        public float RatedMaxFov { get; set; }

        public float SettledFov { get; set; }

        public float CruiseFov { get; set; }

        public float CrashCutFov { get; set; }
    }
}
