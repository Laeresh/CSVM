using System.IO;
using CSVM.Flight;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The AI actor seam's input driver (M4 A2), engine-free: <see cref="AiPilot"/> over a real
/// <see cref="FlightModel"/> on the shipped Bloodhawk stats, on the fixed sim dt. Pins that the
/// control law holds a level course, converges onto an ordered heading, and — the seam's design
/// requirement — that its orders are mutable mid-flight: a retarget and a new altitude issued
/// between steps are flown to without any respawn or rebuild.
///
/// <para>Unchanged across E41, and deliberately not re-pinned: every bound here passed as written
/// when the placeholder law was replaced by the original's (<see cref="AiControlLaw"/>), which is
/// the useful result — the two laws agree on course-holding, capture and retargeting, and they
/// differ in the maneuvering this file never measured. The law's own arithmetic is pinned by
/// <see cref="AiControlLawTests"/> against the decode, not against these tolerances.</para>
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

    /// <summary>`SteeringPatrol` reports what the last step actually did, which is what F13's
    /// leashes colour on: a bare-orders pilot is not flying a net however its mode reads, and a
    /// net-following one is: the distinction between "holds that node" and "is flying at it".</summary>
    [ExtractedDataFact]
    public void SteeringPatrolReportsWhetherTheLastStepFlewTheNet()
    {
        var stats = PlaneStats.Load(ZrdrPath, "player_bhawk");
        var model = new FlightModel(stats);
        model.Reset(new Vector3(0f, 400f, 0f), Basis.Identity, 80f, 0.85f);

        var pilot = new AiPilot { TargetHeadingDeg = 0f, TargetAltitude = 400f };
        Assert.False(pilot.SteeringPatrol);   // nothing has stepped yet
        pilot.Next(model, Dt);
        Assert.False(pilot.SteeringPatrol);   // bare orders: there is no net to fly

        var net = new CSVM.Mech3.AiNet
        {
            Id = 3,
            Name = "TestRing",
            Nodes = new[]
            {
                new CSVM.Mech3.AiNetNode(new Vector3(0f, 400f, -2000f), System.Array.Empty<float>()),
                new CSVM.Mech3.AiNetNode(new Vector3(2000f, 400f, -2000f), System.Array.Empty<float>()),
            },
            Edges = new[] { (0, 1) },
        };
        pilot.Patrol = new AiNetFollower(net, new System.Random(1));
        pilot.Next(model, Dt);
        Assert.True(pilot.SteeringPatrol);
        Assert.Equal(0, pilot.Patrol.CurrentIndex);   // the node the leash would point at

        // Steering something else clears it on the very next step, even though the follower
        // still holds its node, which is the case a leash must draw dimmed rather than bright.
        pilot.Patrol = null;
        pilot.Next(model, Dt);
        Assert.False(pilot.SteeringPatrol);
    }
}
