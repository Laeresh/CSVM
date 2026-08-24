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

    // Every rig's spawn lever and speed, the value a mission's PLAYER_INIT would give. The scripted
    // legs below never move off this lever, so their leader holds one straight cruise; the flown leg
    // firewalls from it, which is the case those legs cannot fail on.
    private const float LeaderThrottle = 0.3f;
    private const float LeaderSpeedMps = 55f;

    // The hold window, and what the hold is judged against. The commanded point is at most 99 m
    // from the leader (the 19 m station plus the 80 m push) and the law weaves around it instead
    // of settling on it, so the leash allows for that weave while still failing the behaviour
    // BL-362 reports, a wingman that simply leaves.
    private const float HoldFromS = 60f;
    private const float LeashM = 600f;
    private const float MeanHoldM = 250f;

    // The campaign's own starting airframe, which is what C3/M01 gives the player and wingman_1.
    // ⚠ Its fd_speed is 113 m/s against the law's 111.76 m/s ceiling, a 1.24 m/s margin, where the
    // suite's default Bloodhawk has 23 m/s. Quote a campaign figure off this one only.
    private const string FlownPlaneNode = "player_pfighter";

    // The flown-leader leg: its spawn altitude, how long it runs, and from when it is judged. The
    // first seconds are the leader's own acceleration off the spawn lever, which no station-keeper
    // can be inside of, so the hold is judged after them.
    private const float FlownLeaderAltitudeM = 1200f;
    private const float FlownRunS = 120f;
    private const float FlownHoldFromS = 10f;

    // The flown leg's leash IS the join gate, because an escort that has never joined commands a
    // point 200 m above its leader and that is the reported symptom. ⚠ It is not an exit: the
    // formation state is never left once entered, so crossing this line matters at the FIRST join,
    // not later. The mean allows for the gap the leader opens firewalling from its spawn lever.
    private const float FlownLeashM = AiEscort.JoinRangeM;
    private const float FlownMeanHoldM = 400f;

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
            // Twice, and the pair is the point: the campaign's own airframe is the case that
            // matters, and the suite's default is the same leg with the mechanism eighteen times
            // larger. ⚠ A number off the default describes the mechanism, never the campaign.
            FlyFlownLeaderLeg(ctx, planesGamez, textures, FlownPlaneNode);
            FlyFlownLeaderLeg(ctx, planesGamez, textures, ctx.PlaneName);
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

        // What an escort that has NOT joined commands, which is the reported symptom's own shape:
        // a point 200 m directly above the leader. The formation state is never left inside the
        // law, so this is reachable only before the first join, never after it.
        var unjoined = new AiEscort();
        var overfly = unjoined.Next(new Vector3(0f, 0f, 100f), 5f, leader, null, out _);
        ctx.Check(unjoined.State == EscortState.Joining
            && Mathf.IsEqualApprox(overfly.Y - leader.Position.Y, AiEscort.LeaderOverflyM, 0.01f),
            $"a wingman below the join speed commands {AiEscort.LeaderOverflyM:0} m ABOVE its leader: {unjoined.State}, {overfly.Y - leader.Position.Y:0.0} m up");

        // …and the formation state, once entered, is never left however far the wingman strays.
        var joined = new AiEscort();
        joined.Next(new Vector3(0f, 0f, 100f), 60f, leader, null, out _);
        joined.Next(new Vector3(0f, 0f, 5000f), 60f, leader, null, out _);
        ctx.Check(joined.State == EscortState.Station,
            $"…and a joined wingman 5 km out is still in the formation state, not re-joining: {joined.State}");
    }

    // The leader's stick for the flown leg: a human profile, not a cruise lever. Full throttle
    // throughout, a right turn, a climb, a level-off and a left turn, which is the speed and
    // altitude range a flown mission covers. Holding a straight cruise lever is not station-keeping
    // on a player, so the scripted legs cannot fail on it.
    private static (FlightInput Input, float Duration)[] FlownLeaderProfile() => new[]
    {
        (new FlightInput { Throttle = 1f }, 20f),
        (new FlightInput { Throttle = 1f, Roll = 0.6f }, 1.5f),
        (new FlightInput { Throttle = 1f, Pitch = 0.35f }, 8f),
        (new FlightInput { Throttle = 1f, Roll = -0.6f }, 1.5f),
        (new FlightInput { Throttle = 1f }, 5f),
        (new FlightInput { Throttle = 1f, Pitch = 0.15f }, 6f),
        (new FlightInput { Throttle = 1f, Pitch = -0.15f }, 6f),
        (new FlightInput { Throttle = 1f }, 5f),
        (new FlightInput { Throttle = 1f, Roll = -0.6f }, 1.5f),
        (new FlightInput { Throttle = 1f, Pitch = 0.35f }, 8f),
        (new FlightInput { Throttle = 1f, Roll = 0.6f }, 1.5f),
        (new FlightInput { Throttle = 1f }, 0f),
    };

    // The leg the scripted one cannot fail on: a leader flown the way a player flies, on a human
    // stick at full throttle, with the wingman on the AI force path the live roster gives it. Both
    // fly the CAMPAIGN's own airframe, not the suite's default: the Devastator's fd_speed sits
    // 1.24 m/s above the decoded ceiling, where a Bloodhawk's sits 23 m/s above it, so the default
    // would measure a far larger effect than a campaign wingman ever meets.
    private static void FlyFlownLeaderLeg(TestContext ctx, GameZ planesGamez, TextureArchive textures,
        string planeNode)
    {
        var stats = PlaneStats.Load(ctx.ZrdrPath, planeNode);
        var aiStats = PlaneStats.LoadForAi(ctx.ZrdrPath, planeNode);
        ctx.Note($"[flown {planeNode}] leader fd_speed {stats.FdSpeed:0.0} m/s, wingman fd_speed {aiStats.FdSpeed:0.0} m/s, ceiling {AiControlLaw.SpeedCeiling:0.0} m/s, margin {stats.FdSpeed - AiControlLaw.SpeedCeiling:0.0} m/s");
        ProjectilePool? pool = null;
        FlightController? leader = null;
        FlightController? wing = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            var leaderPos = new Vector3(0f, FlownLeaderAltitudeM, 0f);
            leader = Rig(ctx, planesGamez, textures, stats, live, leaderPos, true, null,
                FlightRoster.ShooterIdBase, FlownLeaderProfile(), null, out _, planeNode);

            // Spawned on its own decoded station, which is where the campaign's aiv puts it: this
            // leg is about holding the station, not about reaching it.
            var wingPos = AiEscort.FormationStation(leaderPos, Basis.Identity, playerLeader: true);
            var escort = new AiEscort { Leader = leader };
            var pilot = AiPilot.HoldingCourse(wingPos, wingPos + Vector3.Forward);
            pilot.Escort = escort;
            pilot.Machine = new AiModeMachine(new System.Random(7));
            // The human field a live session binds, so the wingman reaches the far-field plant as
            // it does in the game. That plant is no escape from the ceiling: it holds the LEVER
            // times fd_speed, and the lever already carries the cap. The readout below proves it.
            var leaderRig = leader;
            var humans = new Vector3[1];
            wing = Rig(ctx, planesGamez, textures, aiStats, live, wingPos, false, pilot,
                FlightRoster.ShooterIdBase + 1, null,
                () => { humans[0] = leaderRig.WorldPosition; return humans; }, out var wingPlant,
                planeNode);

            float worst = 0f, meanRange = 0f, worstAbove = 0f, worstLeaderSpeed = 0f;
            float farFastest = 0f, nearFastest = 0f;
            bool leftStation = false, everCrashed = false;
            int samples = 0, farSteps = 0, allSteps = 0;
            for (int i = 0; i < (int)(FlownRunS / StepDt); i++)
            {
                live.SimStep(StepDt);
                leader.SimStep(StepDt);
                wing.SimStep(StepDt);
                float range = wing.WorldPosition.DistanceTo(leader.WorldPosition);
                worstLeaderSpeed = Mathf.Max(worstLeaderSpeed, leader.WorldVelocity.Length());
                everCrashed |= wing.Crashed || leader.Crashed;
                allSteps++;
                float wingSpeed = wing.WorldVelocity.Length();
                if (wingPlant.FarFieldPlant)
                {
                    farSteps++;
                    farFastest = Mathf.Max(farFastest, wingSpeed);
                }
                else
                {
                    nearFastest = Mathf.Max(nearFastest, wingSpeed);
                }
                if (i % 900 == 0)
                {
                    ctx.Note($"[flown {planeNode}] t={i * StepDt:0}s range={range:0} escort={escort.State} mode={AiModeMachine.NameOf(pilot.Machine!.Mode)} dY={wing.WorldPosition.Y - leader.WorldPosition.Y:0} wingV={wing.WorldVelocity.Length():0} leadV={leader.WorldVelocity.Length():0} lever={pilot.Throttle:0.00}");
                }

                if (i * StepDt < FlownHoldFromS)
                    continue;
                samples++;
                worst = Mathf.Max(worst, range);
                meanRange += range;
                worstAbove = Mathf.Max(worstAbove, wing.WorldPosition.Y - leader.WorldPosition.Y);
                leftStation |= escort.State != EscortState.Station;
            }

            meanRange /= Mathf.Max(1, samples);
            ctx.Note($"[flown {planeNode}] hold: mean {meanRange:0} m, worst {worst:0} m, worst above {worstAbove:0} m, leader peak {worstLeaderSpeed:0} m/s");
            ctx.Note($"[flown {planeNode}] plant: far-field {farSteps} of {allSteps} steps ({100f * farSteps / Mathf.Max(1, allSteps):0.0}%), fastest far {farFastest:0.0} m/s, fastest near {nearFastest:0.0} m/s, ceiling {AiControlLaw.SpeedCeiling:0.0}, fd_speed {stats.FdSpeed:0.0}");
            ctx.Check(!everCrashed,
                $"[flown leader] neither aircraft goes in over the {FlownRunS:0} s the leader is flown");
            ctx.Check(!leftStation,
                $"…the wingman never falls out of the formation state, which commands {AiEscort.LeaderOverflyM:0} m above the leader");
            ctx.Check(worst > 0f && worst < FlownLeashM,
                $"…and stays with a leader flown on a human stick: worst {worst:0} m of {FlownLeashM:0}");
            ctx.Check(meanRange < FlownMeanHoldM,
                $"…averaging inside {FlownMeanHoldM:0} m: {meanRange:0} m");
        }
        finally
        {
            wing?.Free();
            leader?.Free();
            pool?.Free();
        }
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
                FlightRoster.ShooterIdBase, null, null, out _);

            var escort = new AiEscort { Leader = leader };
            var pilot = AiPilot.HoldingCourse(wingPos, wingPos + Vector3.Forward);
            pilot.Escort = escort;
            pilot.Machine = new AiModeMachine(new System.Random(7));
            wing = Rig(ctx, planesGamez, textures, stats, live, wingPos, false, pilot,
                FlightRoster.ShooterIdBase + 1, null, null, out _);

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

    // One aircraft on the suite's own stage: no camera, no HUD, no devices, exactly what
    // FlightRoster builds for an actor, the AI force path included. `holdSegments` gives the
    // leader a scripted human stick; null leaves it on its spawn lever with the stick centred.
    private static FlightController Rig(TestContext ctx, GameZ planesGamez, TextureArchive textures,
        PlaneStats stats, ProjectilePool live, Vector3 pos, bool human, AiPilot? pilot, int shooterId,
        (FlightInput Input, float Duration)[]? holdSegments,
        System.Func<System.Collections.Generic.IReadOnlyList<Vector3>>? humanPositions,
        out FlightModel plant, string? planeNode = null)
    {
        var model = new PlaneBuilder(planesGamez, textures).Build(planeNode ?? ctx.PlaneName);
        var rig = new FlightController();
        rig.Bind(new FlightControllerBuild
        {
            PlaneModel = model,
            Collider = PlaneCollider.Build(model),
            Damage = new PlaneDamage(stats.DestroyableParts),
            PlayerIndex = shooterId,
            IsHumanPiloted = human,
            Pilot = pilot,
            HoldSegments = holdSegments,
            HumanPositions = humanPositions,
            Projectiles = live,
            UseKeyboard = false,
            PadDevices = System.Array.Empty<int>(),
            AllowPause = false,
        });
        plant = new FlightModel(stats, aiForcePath: pilot != null);
        rig.Setup(plant, null, new CamParams(), pos, pos + Vector3.Forward,
            LeaderThrottle, LeaderSpeedMps);
        ctx.Host.AddChild(rig);
        return rig;
    }
}
