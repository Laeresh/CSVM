using System.IO;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>The campaign wingman's station-keeping: the decoded netless <c>mode wingman</c>
/// escort law, first as pure geometry and then flown by two live aircraft.</summary>
internal static class WingmanSuites
{
    // The wingman starts this far out on its leader's beam: outside the 700 m join threshold, so
    // the join is something the suite watches happen rather than something it starts inside of,
    // and abeam rather than astern so the closure never depends on out-running the leader.
    private const float StartAbeamM = 1200f;

    private const float StepDt = 1f / 60f;

    // The scripted leader's lever and spawn speed. It flies with no pilot at all, stick centred,
    // so it holds a straight course at a speed a wingman can match: the decoded law caps an AI's
    // own demand at 250 mph (AiControlLaw.SpeedCeiling), so a leader flown flat out cannot be
    // formated on at all, which is a property of the original and not of this suite.
    private const float LeaderThrottle = 0.3f;
    private const float LeaderSpeedMps = 55f;

    // The hold window, and what the hold is judged against. The commanded point is at most 99 m
    // from the leader (the 19 m station plus the 80 m push) and the law weaves around it instead
    // of settling on it, so the leash allows for that weave while still failing the behaviour
    // BL-362 reports, a wingman that simply leaves.
    private const float HoldFromS = 60f;
    private const float LeashM = 600f;
    private const float MeanHoldM = 250f;

    internal static void WingmanStation(TestContext ctx)
    {
        StationGeometry(ctx);

        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var textures = new TextureArchive(texturesPath);
        try
        {
            var behindPlayer = FlyLeg(ctx, planesGamez, textures, stats, playerLeader: true);
            var withAi = FlyLeg(ctx, planesGamez, textures, stats, playerLeader: false);

            // The A/B the two decoded stations predict: 18 m astern of a player against 8 m ahead
            // of an AI, so the same wingman rides farther AFT behind a player leader. Both legs
            // orbit the leader under the separation push, which is common to them and cancels.
            ctx.Check(behindPlayer.Z > withAi.Z,
                $"a player leader's wingman rides aft of an AI leader's, its own decoded station: {behindPlayer.Z:0.0} m against {withAi.Z:0.0} m");
        }
        finally
        {
            textures.Dispose();
        }
    }

    // The station geometry, with no engine state at all: the two decoded offsets in a leader's own
    // body frame, the speed-ramped station on a target, and the separation push.
    private static void StationGeometry(TestContext ctx)
    {
        var leaderPos = new Vector3(100f, 500f, -200f);
        var station = AiEscort.FormationStation(leaderPos, Basis.Identity, playerLeader: true);
        ctx.Check(station.IsEqualApprox(leaderPos + new Vector3(6f, 0f, 18f)),
            $"a player leader's station is 6 m right and 18 m astern of it: {station - leaderPos}");

        // Rolled 90° right, the leader's own right axis points DOWN, so the 6 m "out" goes with
        // it: the offset is in the leader's frame, never in the world's.
        var rolled = new Basis(Vector3.Forward, Mathf.DegToRad(90f));
        var rolledStation = AiEscort.FormationStation(leaderPos, rolled, playerLeader: true);
        var rolledOffset = rolledStation - leaderPos;
        ctx.Check(rolledOffset.IsEqualApprox(new Vector3(0f, -6f, 18f)),
            $"…and rolls with the leader: 90° right of bank puts it 6 m LOW {rolledOffset}");

        var aiStation = AiEscort.FormationStation(leaderPos, Basis.Identity, playerLeader: false);
        ctx.Check(aiStation.IsEqualApprox(leaderPos + new Vector3(8f, -2f, -8f)),
            $"an AI leader's station is 8 m out, 2 m low and 8 m AHEAD: {aiStation - leaderPos}");

        // The station on a selected target: the decoded 350 ft / 850 ft ramp, placed along the
        // negation of the target's backward axis, i.e. ahead of it along its own facing.
        var targetPos = new Vector3(0f, 400f, 0f);
        var backward = Vector3.Back;
        var slow = AiEscort.TargetStation(targetPos, backward, 10f);
        var fast = AiEscort.TargetStation(targetPos, backward, 200f);
        ctx.Check(slow.IsEqualApprox(targetPos - (backward * 106.68f)),
            $"a slow target's station sits 106.68 m ahead of it: {slow - targetPos}");
        ctx.Check(fast.IsEqualApprox(targetPos - (backward * 259.08f)),
            $"…and a fast one's 259.08 m ahead: {fast - targetPos}");

        // The separation push: a station 19 m from the leader is always inside the 80 m test, so
        // the commanded point is pushed out along the leader-to-wingman line to ~99 m.
        var own = leaderPos + new Vector3(6f, 0f, 18f);
        var pushed = AiEscort.Separated(own, own, leaderPos);
        float pushedRange = pushed.DistanceTo(leaderPos);
        ctx.Check(Mathf.IsEqualApprox(pushedRange, own.DistanceTo(leaderPos) + AiEscort.SeparationM, 0.01f),
            $"the 80 m separation push moves the station out to {pushedRange:0.0} m of the leader");
        var far = leaderPos + new Vector3(0f, 0f, 120f);
        ctx.Check(AiEscort.Separated(far, far, leaderPos).IsEqualApprox(far),
            $"…and stands down outside 80 m");

        // The join: the threshold is a range AND a speed, both from the transition table.
        var escort = new AiEscort();
        var leader = new EscortLeader { Attitude = Basis.Identity, IsPlayer = true };
        escort.Next(new Vector3(0f, 0f, 701f), 60f, leader, null, out _);
        ctx.Check(escort.State == EscortState.Joining,
            $"701 m astern the wingman has not joined: {escort.State}");
        escort.Next(new Vector3(0f, 0f, 699f), 20f, leader, null, out _);
        ctx.Check(escort.State == EscortState.Joining,
            $"…inside 700 m but below 20.576 m/s it still has not: {escort.State}");
        escort.Next(new Vector3(0f, 0f, 699f), 21f, leader, null, out _);
        ctx.Check(escort.State == EscortState.Station,
            $"…and inside 700 m above 20.576 m/s it joins: {escort.State}");
    }

    // One leader/wingman pair, flown, reporting the wingman's mean offset in the leader's frame.
    // The leader is scripted: no pilot at all and the stick centred, so it holds a straight course.
    // IsHumanPiloted on it is the one thing that picks which decoded station the wingman flies.
    private static Vector3 FlyLeg(TestContext ctx, GameZ planesGamez, TextureArchive textures,
        PlaneStats stats, bool playerLeader)
    {
        string kind = playerLeader ? "player" : "AI";
        ProjectilePool? pool = null;
        FlightController? leader = null;
        FlightController? wing = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            var leaderPos = new Vector3(0f, 500f, 0f);
            var wingPos = leaderPos + new Vector3(StartAbeamM, 0f, 0f);
            leader = Rig(ctx, planesGamez, textures, stats, live, leaderPos, playerLeader, null,
                FlightRoster.ShooterIdBase);

            var escort = new AiEscort { Leader = leader };
            var pilot = AiPilot.HoldingCourse(wingPos, wingPos + Vector3.Forward);
            pilot.Escort = escort;
            pilot.Machine = new AiModeMachine(new System.Random(7));
            wing = Rig(ctx, planesGamez, textures, stats, live, wingPos, false, pilot,
                FlightRoster.ShooterIdBase + 1);

            float joinedRange = -1f;
            bool joined = false, unjoined = false, leftStation = false;
            float worstLate = 0f, meanRange = 0f;
            var lateOffset = Vector3.Zero;
            var commandedOffset = Vector3.Zero;
            var farStationError = 0f;
            int lateSamples = 0, farSamples = 0;
            for (int i = 0; i < (int)(120f / StepDt); i++)
            {
                live.SimStep(StepDt);
                leader.SimStep(StepDt);
                // The pose the escort law read this step: the leader after its step, the wingman
                // before its own, so the commanded-point check below rebuilds the law's inputs.
                var wingBefore = wing.WorldPosition;
                wing.SimStep(StepDt);
                float range = wing.WorldPosition.DistanceTo(leader.WorldPosition);
                if (!joined && escort.State == EscortState.Station)
                {
                    joined = true;
                    joinedRange = range;
                }
                else if (joined && escort.State == EscortState.Joining)
                {
                    unjoined = true;
                }

                if (i % 1800 == 0)
                {
                    ctx.Note($"[{kind}] t={i * StepDt:0}s range={range:0} escort={escort.State} mode={AiModeMachine.NameOf(pilot.Machine!.Mode)} wingY={wing.WorldPosition.Y:0} leadY={leader.WorldPosition.Y:0} wingV={wing.WorldVelocity.Length():0} leadV={leader.WorldVelocity.Length():0} wingUp={wing.InPlay}/{wing.Crashed} leadUp={leader.InPlay}/{leader.Crashed}");
                }

                // The second minute is the hold; the first is the join and its settling.
                if (i * StepDt >= HoldFromS)
                {
                    lateSamples++;
                    worstLate = Mathf.Max(worstLate, range);
                    meanRange += range;
                    var leaderBasis = leader.GlobalTransform.Basis;
                    var leaderFrame = leaderBasis.Inverse();
                    lateOffset += leaderFrame * (wing.WorldPosition - leader.WorldPosition);
                    commandedOffset += leaderFrame * (escort.StationPoint - leader.WorldPosition);
                    leftStation |= escort.State != EscortState.Station;
                    // Outside the 80 m push the commanded point IS the decoded station, rebuilt
                    // from the inputs the law read. Avoid crash runs AHEAD of the escort in the
                    // fork order, so a step it owns commands the climb-out instead.
                    bool escortFlew = pilot.Machine!.Mode is not (AiMode.AvoidCrash or AiMode.Stunned);
                    if (escortFlew && wingBefore.DistanceTo(leader.WorldPosition) >= AiEscort.SeparationM)
                    {
                        farSamples++;
                        var station = AiEscort.FormationStation(leader.WorldPosition, leaderBasis, playerLeader);
                        farStationError = Mathf.Max(farStationError, escort.StationPoint.DistanceTo(station));
                    }
                }
            }

            var mean = lateOffset / Mathf.Max(1, lateSamples);
            var meanCommanded = commandedOffset / Mathf.Max(1, lateSamples);
            meanRange /= Mathf.Max(1, lateSamples);
            ctx.Note($"[{kind}] hold: mean range {meanRange:0} m, worst {worstLate:0} m, mean flown offset out {mean.X:0.0} up {mean.Y:0.0} along {mean.Z:0.0}, mean commanded offset out {meanCommanded.X:0.0} up {meanCommanded.Y:0.0} along {meanCommanded.Z:0.0}, {farSamples} samples outside the push (station error {farStationError:0.00} m)");

            // A player leader's wingman walks the join gate; an AI leader's is forced into the
            // engaging state every frame and, with no target, reads the formation station at once.
            if (playerLeader)
            {
                ctx.Check(joined, $"[{kind} leader] the wingman joins from {StartAbeamM:0} m abeam");
                ctx.Check(joined && joinedRange < AiEscort.JoinRangeM,
                    $"…inside the decoded 700 m threshold: joined at {joinedRange:0} m");
                ctx.Check(!unjoined, $"…and never falls back out of the formation state");
            }
            else
            {
                ctx.Check(escort.State == EscortState.Station,
                    $"[{kind} leader] the forced engaging state reads the formation station: {escort.State}");
            }

            // The commanded point never sits farther from the leader than the station's own 19 m
            // plus the 80 m separation push, and the law weaves around it rather than settling on
            // it (that limit cycle is the decoded law's own: docs/org/aiPilot.md).
            ctx.Check(worstLate > 0f && worstLate < LeashM,
                $"…and stays with it: worst {worstLate:0} m of {LeashM:0}, mean {meanRange:0} m over the last minute");
            ctx.Check(meanRange < MeanHoldM,
                $"…averaging inside {MeanHoldM:0} m of the leader: {meanRange:0} m");
            // The decode pins the COMMANDED station, not where the push's weave puts the flown
            // mean (docs/PLAN-M5-campaign.md D34): the formation state is never left, and outside
            // the push the commanded point is the body-frame station to the centimetre.
            if (playerLeader)
            {
                ctx.Check(!leftStation, $"…never leaving the formation state during the hold");
            }
            ctx.Check(farSamples > 0 && farStationError < 0.01f,
                $"…commanding the decoded station whenever outside the 80 m push: {farSamples} samples, worst error {farStationError:0.000} m");

            return mean;
        }
        finally
        {
            wing?.Free();
            leader?.Free();
            pool?.Free();
        }
    }

    // One AI-flown aircraft on the suite's own stage: no camera, no HUD, no devices, exactly what
    // FlightRoster builds for an AI actor.
    private static FlightController Rig(TestContext ctx, GameZ planesGamez, TextureArchive textures,
        PlaneStats stats, ProjectilePool live, Vector3 pos, bool human, AiPilot? pilot, int shooterId)
    {
        var model = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
        var rig = new FlightController
        {
            PlaneModel = model,
            Collider = PlaneCollider.Build(model),
            Damage = new PlaneDamage(stats.DestroyableParts),
            PlayerIndex = shooterId,
            IsHumanPiloted = human,
            Pilot = pilot,
            Projectiles = live,
            UseKeyboard = false,
            PadDevices = System.Array.Empty<int>(),
            AllowPause = false,
        };
        rig.AddChild(model);
        rig.Setup(new FlightModel(stats), null, new CamParams(), pos, pos + Vector3.Forward,
            LeaderThrottle, LeaderSpeedMps);
        ctx.Host.AddChild(rig);
        return rig;
    }
}
