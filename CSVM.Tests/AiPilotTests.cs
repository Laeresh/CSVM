using System.IO;
using CSVM.Flight;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The AI actor seam's input driver (M4 A2), engine-free: <see cref="AiPilot"/> over a real
/// <see cref="FlightModel"/> on the shipped Bloodhawk stats, on the fixed sim dt. Pins that the
/// placeholder control law holds a level course, converges onto an ordered heading, and — the
/// seam's design requirement — that its orders are mutable mid-flight: a retarget and a new
/// altitude issued between steps are flown to without any respawn or rebuild.
/// </summary>
public class AiPilotTests
{
    private const float Dt = 1f / 60f;

    private static string ZrdrPath =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    [ExtractedDataFact]
    public void HoldsCourseFliesOrderedTurnsAndTakesMidFlightRetargets()
    {
        var stats = PlaneStats.Load(ZrdrPath, "player_bhawk");
        var model = new FlightModel(stats);
        model.Reset(new Vector3(0f, 400f, 0f), Basis.Identity, 80f, 0.85f); // nose -Z = heading 0
        var pilot = new AiPilot { TargetHeadingDeg = 0f, TargetAltitude = 400f };

        void Fly(float seconds)
        {
            for (int i = 0; i < (int)(seconds / Dt); i++)
                model.Step(pilot.Next(model, Dt), Dt);
        }

        float HeadingErrDeg()
        {
            var nose = -model.Attitude.Z;
            float headingDeg = AiPilot.HeadingDegOf(nose);
            return Mathf.Wrap(pilot.TargetHeadingDeg - headingDeg, -180f, 180f);
        }

        // Straight and level: 20 s on the spawn course stays on heading and altitude.
        Fly(20f);
        Assert.True(Mathf.Abs(HeadingErrDeg()) < 5f, $"level hold drifted {HeadingErrDeg():0.0}° off course");
        Assert.InRange(model.Position.Y, 340f, 460f);
        Assert.True(model.Speed > 40f, $"level hold bled speed to {model.Speed:0.0} m/s");

        // An ordered 90° turn converges and recaptures level flight.
        pilot.TargetHeadingDeg = 90f;
        Fly(30f);
        Assert.True(Mathf.Abs(HeadingErrDeg()) < 6f, $"turn to 090 ended {HeadingErrDeg():0.0}° off");
        Assert.InRange(model.Position.Y, 320f, 480f);

        // Orders are mutable between steps: retarget across the compass AND re-order the
        // altitude on the same pilot, mid-flight — the mission-script surface's requirement.
        pilot.TargetHeadingDeg = 225f;
        pilot.TargetAltitude = 500f;
        Fly(45f);
        Assert.True(Mathf.Abs(HeadingErrDeg()) < 6f, $"retarget to 225 ended {HeadingErrDeg():0.0}° off");
        Assert.InRange(model.Position.Y, 440f, 560f);
        Assert.True(model.Speed > 40f, $"the flight ended stalled at {model.Speed:0.0} m/s");
    }
}
