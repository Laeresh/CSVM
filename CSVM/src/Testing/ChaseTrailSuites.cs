using System.Collections.Generic;
using System.Text;
using CSVM.Flight;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The enhanced presentation's two chase cues, driven straight through
/// <see cref="CameraController"/> rather than through a built world: the lagged attitude that
/// leaves the camera trailing the nose through a roll before it springs back, and the speed
/// widening of the external FOV. The faithful arm is the reason this suite exists: the same
/// scripted roll is flown with and without the host's per-frame cue step under
/// <c>--graphics=original</c>, and the two camera poses must agree to the bit, since every pinned
/// golden is that presentation's own image. Decode of the camera the cues sit beside:
/// docs/org/cameraViews.md.</summary>
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

    // Where the settled pose has to be reached to: the position catch-up leaves under a
    // thousandth of the swing after two seconds, so anything above this is a cue that never sprang
    // back.
    private const float SettledToleranceDeg = 0.5f;

    // How much further the enhanced camera must trail than the faithful one at the end of the
    // roll. A roll about the nose swings the chase offset by the roll rate times the sine of its
    // own elevation, so the two read about 2.8° and 8.3°; half that gap is a floor no rounding
    // reaches and no disabled lag passes.
    private const float TrailMarginDeg = 3f;

    /// <summary>Flies one scripted roll per presentation and reads the camera it left.</summary>
    [Suite("chase-trail",
        "the enhanced chase cues: a roll leaves the camera trailing the aircraft's nose and it springs back to the settled pose, the external FOV widens toward the airframe's rated max and is untouched at cruise, and under the faithful presentation the same scripted roll writes the same camera pose and FOV to the bit whether or not the host steps the cues")]
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
                && stepped.SettledFov == stepped.BuiltFov,
                $"and the faithful external FOV is the one the camera was built with ({stepped.SettledFov:0.0000}° against {stepped.BuiltFov:0.0000}°)");

            // The teeth on the check above: the enhanced arm has to move the same poses, or an
            // arm that did nothing at all would pass every line here.
            int enhancedMoved = 0;
            for (int i = 0; i < stepped.Poses.Count; i++)
            {
                if (stepped.Poses[i] != enhanced.Poses[i])
                {
                    enhancedMoved++;
                }
            }
            ctx.Check(enhancedMoved > 0,
                $"while the enhanced presentation moves the pose on {enhancedMoved} of those frames");

            // The cue itself: trailing through the roll, settled after it.
            ctx.Check(enhanced.RolledLagDeg > stepped.RolledLagDeg + TrailMarginDeg,
                $"the enhanced camera trails the nose through the roll by {enhanced.RolledLagDeg:0.#}°, against the faithful camera's {stepped.RolledLagDeg:0.#}°");
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
            ctx.Check(enhanced.RatedMaxFov == enhanced.BuiltFov + widen,
                $"so the enhanced external FOV at rated max is the built-in {enhanced.BuiltFov:0.00}° plus it ({enhanced.RatedMaxFov:0.0000}°)");
            ctx.Check(enhanced.CruiseFov == enhanced.BuiltFov,
                $"and at cruise it is the built-in angle exactly ({enhanced.CruiseFov:0.0000}°)");
            ctx.Check(stepped.RatedMaxFov == stepped.BuiltFov,
                $"while the faithful presentation holds the built-in angle at rated max ({stepped.RatedMaxFov:0.0000}°)");
            ctx.Check(enhanced.CrashCutFov == enhanced.BuiltFov,
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
        var sortie = new Sortie { BuiltFov = camera.Fov };
        try
        {
            var cam = new CameraController(camera, new CamParams(), _ => false, 0);
            var attitude = Basis.Identity;
            cam.Snap(Vector3.Zero, attitude, Speed, new Transform3D(attitude, Vector3.Zero));
            // The settled offset direction in the PLANE's frame, read off the settle-immediately
            // path rather than restated here, so the trail is measured against the camera's own
            // idea of where it belongs.
            var settledDir = camera.Position.Normalized();

            for (int i = 1; i <= RollFrames; i++)
            {
                attitude = new Basis(Vector3.Forward, Mathf.DegToRad(RollRateDeg * i * Dt));
                Step(cam, attitude, stepCues, RatedMaxFrac);
                sortie.Poses.Add(camera.Transform);
            }
            sortie.RolledLagDeg = LagDeg(camera, attitude, settledDir);
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

            report.AppendLine($"{mode} cues={stepCues}: rolled {sortie.RolledLagDeg:0.##}°, holding {sortie.HalfwayLagDeg:0.##}°, settled {sortie.SettledLagDeg:0.####}°, fov built {sortie.BuiltFov:0.0000}° rated-max {sortie.RatedMaxFov:0.0000}° cruise {sortie.CruiseFov:0.0000}° crash {sortie.CrashCutFov:0.0000}°");
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
            cam.StepEnhancedCues(Dt, attitude, speedFrac);
        }
        cam.RestoreExternalFov();
        cam.Chase(Dt, Vector3.Zero, attitude);
    }

    // How far the camera's offset direction trails the one the settled pose would put it at.
    private static float LagDeg(Camera3D camera, Basis attitude, Vector3 settledDir) =>
        Mathf.RadToDeg(camera.Position.Normalized().AngleTo(attitude * settledDir));

    private sealed class Sortie
    {
        public List<Transform3D> Poses { get; } = new List<Transform3D>();

        public float BuiltFov { get; init; }

        public float RolledLagDeg { get; set; }

        public float HalfwayLagDeg { get; set; }

        public float SettledLagDeg { get; set; }

        public float RatedMaxFov { get; set; }

        public float SettledFov { get; set; }

        public float CruiseFov { get; set; }

        public float CrashCutFov { get; set; }
    }
}
