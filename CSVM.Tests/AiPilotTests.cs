using System.IO;
using CSVM.Flight;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The AI actor seam's input driver, engine-free: <see cref="AiPilot"/> over a real
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

    /// <summary>The aspect test (<c>FUN_0041d9f0</c> at <c>0x0041dd49</c>) takes BOTH of the
    /// victim's cones: a victim coming at us and a victim we sit behind read alike, and only a
    /// beam aspect leaves the pursuer flying to the lead point. The cone is the pilot's own
    /// <c>quick_draw_angle</c>, so a better pilot goes to a firing solution from further off the
    /// axis.</summary>
    [Fact]
    public void TheAspectTestTakesBothOfTheVictimsCones()
    {
        var toQuarry = new Vector3(0f, 0f, -300f);   // the victim is dead ahead of us
        var comingAtUs = new Vector3(0f, 0f, 1f);    // its nose points back down the line
        var flyingAway = new Vector3(0f, 0f, -1f);
        var beam = new Vector3(1f, 0f, 0f);

        Assert.True(AiPilot.IsOnGunAxis(toQuarry, comingAtUs, 50f));
        Assert.True(AiPilot.IsOnGunAxis(toQuarry, flyingAway, 50f));   // the tail chase, decoded
        Assert.False(AiPilot.IsOnGunAxis(toQuarry, beam, 50f));

        // The cone is the pilot's: 60° off the axis is beam to a rating-1 pilot (50°) and on-axis
        // to a rating-9 one (89°).
        var off60 = new Vector3(Mathf.Sin(Mathf.DegToRad(60f)), 0f, -Mathf.Cos(Mathf.DegToRad(60f)));
        Assert.False(AiPilot.IsOnGunAxis(toQuarry, off60, 50f));
        Assert.True(AiPilot.IsOnGunAxis(toQuarry, off60, 89f));

        // Degenerate inputs never claim an axis.
        Assert.False(AiPilot.IsOnGunAxis(Vector3.Zero, comingAtUs, 50f));
        Assert.False(AiPilot.IsOnGunAxis(toQuarry, Vector3.Zero, 50f));
    }

    /// <summary>The merge test's four decoded gates (<c>FUN_0041d9f0</c> at <c>0x0041e130</c>):
    /// inside 400 m, we fly at the victim, the victim flies at us, and either party more than
    /// ~37° off the line of sight disarms it. Pure geometry, so it is pinned directly rather
    /// than through a flown pursuit (which would need a scene tree for the quarry).</summary>
    [Fact]
    public void TheMergeTestArmsOnlyOnATrueHeadOnInsideItsRange()
    {
        var closing = new Vector3(0f, 0f, -100f);    // us, flying at a victim dead ahead
        var oncoming = new Vector3(0f, 0f, 100f);    // the victim, flying back at us

        Assert.True(AiPilot.IsMerging(new Vector3(0f, 0f, -300f), closing, oncoming));
        Assert.False(AiPilot.IsMerging(new Vector3(0f, 0f, -500f), closing, oncoming));
        Assert.False(AiPilot.IsMerging(new Vector3(0f, 0f, -400f), closing, oncoming));  // exclusive

        // A tail chase and a beam pass are both merge-free however close they get.
        Assert.False(AiPilot.IsMerging(new Vector3(0f, 0f, -100f), closing, closing));
        Assert.False(AiPilot.IsMerging(new Vector3(0f, 0f, -100f), closing, new Vector3(100f, 0f, 0f)));
        Assert.False(AiPilot.IsMerging(new Vector3(0f, 0f, -100f), new Vector3(100f, 0f, 0f), oncoming));

        // The closure cone: 30° off the line of sight still merges, 40° does not (cos 37° ≈ 0.8).
        Vector3 Off(float deg, float sign) => new(
            sign * 100f * Mathf.Sin(Mathf.DegToRad(deg)), 0f, sign * 100f * Mathf.Cos(Mathf.DegToRad(deg)));
        Assert.True(AiPilot.IsMerging(new Vector3(0f, 0f, -300f), Off(30f, -1f), Off(30f, 1f)));
        Assert.False(AiPilot.IsMerging(new Vector3(0f, 0f, -300f), Off(40f, -1f), oncoming));
        Assert.False(AiPilot.IsMerging(new Vector3(0f, 0f, -300f), closing, Off(40f, 1f)));

        // A stationary party has no closure to measure and never merges.
        Assert.False(AiPilot.IsMerging(new Vector3(0f, 0f, -300f), Vector3.Zero, oncoming));
        Assert.False(AiPilot.IsMerging(new Vector3(0f, 0f, -300f), closing, Vector3.Zero));
    }

    /// <summary>The merge's vertical bias ramp: dive at or below 50 mph, climb at or above
    /// 90 mph, linear between and zero at the 70 mph the aim velocity collapses to. ⚠ These are
    /// metres per unit of a UNIT vector's horizontal magnitude, so the whole term is worth 0.3 m
    /// at most — see <see cref="AiPilot.MergeVerticalBias"/>.</summary>
    [Fact]
    public void TheMergeVerticalBiasRampsFromDiveToClimbAcrossItsTwoSpeeds()
    {
        Assert.Equal(-0.3f, AiPilot.MergeVerticalBias(10f), 4);
        Assert.Equal(-0.3f, AiPilot.MergeVerticalBias(22.352f), 4);
        Assert.Equal(0f, AiPilot.MergeVerticalBias(31.292799f), 4);
        Assert.Equal(0.3f, AiPilot.MergeVerticalBias(40.2336f), 4);
        Assert.Equal(0.3f, AiPilot.MergeVerticalBias(120f), 4);
        Assert.True(AiPilot.MergeVerticalBias(26f) < 0f);   // still diving below 70 mph
        Assert.True(AiPilot.MergeVerticalBias(36f) > 0f);   // climbing above it
    }
}
