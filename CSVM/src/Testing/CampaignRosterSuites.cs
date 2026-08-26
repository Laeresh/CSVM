using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>The campaign roster spawner over a shipped story mission's <c>aiv</c> roster and its
/// built chapter world: every enabled block gets a rig, the decoded net-versus-escort fork is applied per
/// block, the leader pass names the player rig, a deactivated block is inert, the taxi-path
/// vehicles are placed, and the escorting wingman then holds the player within the
/// <c>wingman-station</c> leash over a two-minute flown run.</summary>
internal static class CampaignRosterSuites
{
    // The story mission with the richest roster shape in one file: a player-escorting wingman_1,
    // two wingmen escorting AI leaders, netted enemies, deactivated blocks and four taxi-path
    // vehicles (docs/formats/ai-rosters.md).
    private const string RosterChapter = "C1";
    private const string RosterMission = "M04";

    private const float StepDt = 1f / 60f;
    private const float RunS = 120f;
    private const float HoldFromS = 60f;

    // The scripted player: no pilot, stick centred, a cruise lever, the same leader the
    // wingman-station suite flies against, so the leash below is that suite's.
    private const float LeaderThrottle = 0.3f;
    private const float LeaderSpeedMps = 55f;
    private const float LeashM = 600f;
    private const float MeanHoldM = 250f;
    private const float FlownLegLiftM = 800f;

    // BL-401's mission: C1/M02 is where both arms of rating_biases name something a spawn can be.
    // The player's side (wingman_4 and the devastators) authors an outright exclusion on the enemy
    // ace BLOCK bloodhawk_2, and bloodhawk_2 itself authors always-target on the player. Neither
    // pattern can match the assembler's ai{n}_{plane} fallback.
    private const string BiasChapter = "C1";
    private const string BiasMission = "M02";
    private const string ExcludingBlock = "wingman_4";
    private const string ExcludedBlock = "bloodhawk_2";
    private const string PlainBlock = "blakepeace_2_1";

    // CM02: three britbalmoral blocks sharing net 19, group 5, no rating_biases, spawned about
    // 110 m apart. The formation is authored as three aircraft on one net, not as a station.
    private const string BomberChapter = "C3";
    private const string BomberMission = "M05";
    private const int BomberNetId = 19;
    private const float BomberRunS = 60f;
    private const float BomberFireAtS = 20f;

    // How far apart the three may ever get. Their spawn spread is about 145 m, and a run that
    // splits at a node passes this inside seconds.
    private const float BomberSpreadCeilingM = 600f;

    // The two candidate rings, both dead ahead and level, so distance and the authored bias are
    // the only terms that differ between an arm's two candidates.
    private const float NearRingM = 400f;
    private const float MidRingM = 900f;
    private const float FarRingM = 1500f;
    private const float ScanRangeM = 3000f;

    private static readonly string[] BomberBlocks =
    {
        "britbalmoral_1", "britbalmoral_2", "britbalmoral_3",
    };

    internal static void CampaignRoster(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, RosterChapter, RosterMission);
        ctx.RequireData(missionZrdr, $"{RosterChapter}/{RosterMission} zrdr");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, RosterChapter);
        ctx.RequireData(texturesPath, $"{RosterChapter} textures");

        CampaignMission? found = null;
        foreach (var m in CampaignSequence.Load(ctx.ZrdrPath))
        {
            if (m.ChapterFolder.Equals(RosterChapter, StringComparison.OrdinalIgnoreCase)
                && m.MissionFolder.Equals(RosterMission, StringComparison.OrdinalIgnoreCase))
            {
                found = m;
            }
        }
        if (found is not { } mission)
        {
            throw new SuiteSkippedException($"{RosterChapter}/{RosterMission} is not in cm_sequence");
        }

        var blocks = AiSkills.LoadRoster(missionZrdr);
        var skills = AiSkills.Load(ctx.ZrdrPath);
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var script = ObjectiveScript.Load(missionZrdr);
        var director = CampaignDirector.Create(script, mission, CampaignProfileDef.NewProfile("Zachary"), null);
        var report = new StringBuilder();
        report.AppendLine($"{RosterChapter}/{RosterMission} seq={mission.Seq}: {blocks.Count} roster block(s)");

        ctx.WithWorld(RosterChapter, collision: false, RosterMission, world =>
        {
            var textures = new TextureArchive(texturesPath);
            var rigs = new List<FlightController>();
            ProjectilePool? pool = null;
            try
            {
                var live = new ProjectilePool(textures, null, null);
                pool = live;
                ctx.Host.AddChild(live);

                // The human rig at the player block's own pose: a scripted leader, as the
                // wingman-station suite's is.
                var playerPose = PlayerPose(blocks);
                var playerStats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
                var player = Rig(ctx, planesGamez, textures, playerStats, live, ctx.PlaneName,
                    playerPose.Position, playerPose.Position + playerPose.Forward, human: true,
                    pilot: null, FlightRoster.ShooterIdBase, AimAssist.PlayerTeam);
                rigs.Add(player);

                int shooterId = FlightRoster.ShooterIdBase + 1;
                string what = director.BuildRoster(new CampaignDirector.RosterInputs
                {
                    ChapterZrdrPath = SessionPaths.ChapterZrdr(ctx.DataRoot, RosterChapter),
                    MissionZrdrPath = missionZrdr,
                    ZrdrPath = ctx.ZrdrPath,
                    MinAiActiveDist = skills.MinAiActiveDist,
                    Player = () => player,
                    NetTrailers = new NetTrailerTargets(
                        () => player.WorldPosition,
                        name => world.Runtime.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null),
                    FindNodes = name => world.Runtime.FindNodes(name),
                    Spawn = (plan, pos, look, pilot) =>
                    {
                        var stats = StatsFor(ctx, plan, report);
                        // A machine so the volumes have somewhere to land and the escort dispatch
                        // runs the decoded fork order; no gunner, so nothing in this run engages.
                        pilot.Machine = new AiModeMachine(new System.Random(7))
                        {
                            ActivationRange = skills.MinAiActiveDist,
                            AttackRange = stats.AiAttackRange,
                            ReturnRange = stats.AiReturnRange,
                        };
                        var rig = Rig(ctx, planesGamez, textures, stats, live, plan.PlaneNode, pos, look,
                            human: false, pilot, shooterId++, plan.Team ?? AimAssist.PlayerTeam);
                        rig.Inert = plan.Inert;
                        rigs.Add(rig);
                        return rig;
                    },
                    Rng = new System.Random(1),
                });
                report.AppendLine($"build summary suffix: '{what}'");

                var roster = director.Roster;
                int expectedRoster = 0;
                foreach (var (name, fields) in blocks)
                {
                    if (!name.Equals(CampaignRosterPlan.PlayerBlock,
                            StringComparison.OrdinalIgnoreCase) && AiSkills.RosterEnabled(fields))
                    {
                        expectedRoster++;
                    }
                }
                ctx.Same(expectedRoster, roster.Count,
                    $"every enabled non-player roster block has a rig");
                CheckFork(ctx, roster, player, report);
                CheckPaths(ctx, director, report);
                CheckVolumes(ctx, roster, skills, report);
                FlyEscort(ctx, roster, player, rigs, live, director, report);
            }
            finally
            {
                foreach (var rig in rigs)
                {
                    rig.Free();
                }
                pool?.Free();
                textures.Dispose();
            }
        });

        ctx.WriteArtifact($"test-campaign-roster-{RosterChapter}-{RosterMission}.txt", report.ToString());
        ctx.Note($"spawned {RosterChapter}/{RosterMission}'s roster from its aiv blocks and flew wingman_1 on the player");
    }

    /// <summary>BL-401: a campaign spawn wears its roster block's own name, so the patterns
    /// <c>rating_biases</c> is authored with reach it, and an authored bias then moves the pick.
    /// Spawns run through the session's own <see cref="FlightRoster"/> and the same
    /// <see cref="CampaignRosterPlan.SpawnFor"/> record the campaign director builds.</summary>
    internal static void RosterSpawnNames(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, BiasChapter, BiasMission);
        ctx.RequireData(missionZrdr, $"{BiasChapter}/{BiasMission} zrdr");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, BiasChapter);
        ctx.RequireData(texturesPath, $"{BiasChapter} textures");

        var plan = CampaignRosterPlan.Build(
            AiSkills.LoadRoster(missionZrdr),
            VehicleDefs.Load(ctx.ZrdrPath),
            AiNets.Load(SessionPaths.ChapterZrdr(ctx.DataRoot, BiasChapter)),
            netDraw: _ => 0);
        var excluding = PlanNamed(plan, ExcludingBlock);
        var excluded = PlanNamed(plan, ExcludedBlock);
        var plain = PlanNamed(plan, PlainBlock);
        if (excluding == null || excluded == null || plain == null)
        {
            throw new SuiteSkippedException(
                $"{BiasChapter}/{BiasMission} does not plan {ExcludingBlock}/{ExcludedBlock}/{PlainBlock}");
        }

        // The authored term, read before anything is built: the exclusion is written against the
        // BLOCK name, and it saturates rather than merely penalising.
        AiRatingBias? exclusion = null;
        foreach (var b in excluding.Biases)
        {
            if (exclusion == null && b.Matches(excluded.Name))
            {
                exclusion = b;
            }
        }
        string exclusionBias = Bias(exclusion);
        ctx.Check(exclusion is { Bias: <= -1f },
            $"'{ExcludingBlock}' authors a rating_biases exclusion on the block name '{excluded.Name}': bias={exclusionBias}");
        AiRatingBias? attract = null;
        foreach (var b in excluded.Biases)
        {
            if (attract == null && b.Matches(AiTargetRanking.PlayerRole))
            {
                attract = b;
            }
        }
        string attractBias = Bias(attract);
        ctx.Check(attract is { Bias: >= 1f },
            $"'{ExcludedBlock}' authors always-target on the '{AiTargetRanking.PlayerRole}' role: bias={attractBias}");
        if (exclusion == null)
        {
            return;
        }

        var report = new StringBuilder();
        report.AppendLine($"{BiasChapter}/{BiasMission}: {plan.Spawns.Count} planned block(s)");
        var textures = new TextureArchive(texturesPath);
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        ProjectilePool? pool = null;
        FlightRoster? roster = null;
        FlightController? human = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);
            roster = Spawner(ctx, planesGamez, textures, live);

            var origin = new Vector3(0f, 500f, 0f);
            var fwd = Vector3.Forward;
            FlightController Launch(RosterSpawnPlan block, float ring)
            {
                var at = origin + (fwd * ring);
                return roster!.SpawnAi(CampaignRosterPlan.SpawnFor(block, at, at + fwd,
                    AiPilot.HoldingCourse(at, at + fwd)));
            }

            // The shooter authors no name of its own, so the counter fallback is exercised in the
            // same run: only a caller WITH an authored name takes it.
            var shooterPilot = AiPilot.HoldingCourse(origin, origin + fwd);
            var shooter = roster.SpawnAi(new AiSpawn(ctx.PlaneName, origin, origin + fwd,
                shooterPilot, Scheme: null, Team: AimAssist.PlayerTeam, AttackRating: 5));
            ctx.Check(shooter.Name == $"ai1_{ctx.PlaneName}",
                $"a spawn with no authored name keeps the assembler's counter form: {shooter.Name}");

            var aceRig = Launch(excluded, NearRingM);
            var plainRig = Launch(plain, MidRingM);
            var wingRig = Launch(excluding, MidRingM);
            ctx.Check(aceRig.Name == excluded.Name && plainRig.Name == plain.Name
                      && wingRig.Name == excluding.Name,
                $"every campaign spawn wears its block's own name: {aceRig.Name}, {plainRig.Name}, {wingRig.Name}");
            ctx.Check(exclusion.Matches(aceRig.Name),
                $"the authored pattern '{exclusion.Pattern}' matches the SPAWNED node's name '{aceRig.Name}'");
            ctx.Check(!exclusion.Matches($"ai2_{excluded.PlaneNode}"),
                $"…and matches nothing of the old counter shape 'ai2_{excluded.PlaneNode}'");

            // BL-493: the block's own pilot name (aiv slot 20) is what the original's readout
            // prints, and it outranks the airframe title. The plain block authors none, so the two
            // sources are told apart in one run rather than agreeing by accident.
            var strings = Messages.Load(ctx.MessagesPath);
            string aceName = PlaneRoster.PlaneDisplayName(aceRig.Stats!);
            string plainName = PlaneRoster.PlaneDisplayName(plainRig.Stats!);
            string wingName = PlaneRoster.PlaneDisplayName(wingRig.Stats!);
            ctx.Check(excluded.Title != null && aceName == strings.Get(excluded.Title),
                $"the ace block's authored '{excluded.Title}' is the name its marker prints: '{aceName}'");
            ctx.Check(excluding.Title != null && wingName == strings.Get(excluding.Title),
                $"…and so is the named wingman's '{excluding.Title}': '{wingName}'");
            ctx.Check(plain.Title == null && plainName != aceName && plainName != wingName,
                $"a block with no authored name keeps the airframe title instead: '{plainName}'");
            report.AppendLine($"marker names: {excluded.Name}='{aceName}' " +
                $"{excluding.Name}='{wingName}' {plain.Name}='{plainName}'");

            // Two of these blocks ship deactivated, and an out-of-play rig never reaches the scan
            // at all: leaving them inert would let a control pass for the wrong reason. Their
            // activation is campaign-roster's subject, not this suite's; the geometry is.
            var stage = new[] { shooter, aceRig, plainRig, wingRig };
            foreach (var rig in stage)
            {
                rig.Held = true;
                rig.Inert = false;
            }
            ctx.Check(Array.TrueForAll(stage, rig => rig.InPlay),
                $"every rig on the stage is in play before the ranking is measured");
            if (shooter.Pilot?.Machine is { } machine)
            {
                machine.ActivationRange = ScanRangeM;
            }
            var gunner = shooter.Pilot?.Gunner;
            ctx.Check(gunner != null, $"the shooter's spawn armed a gunner");
            if (gunner == null)
            {
                return;
            }

            void Park()
            {
                shooter.PlaceHeld(origin, origin + fwd);
                aceRig.PlaceHeld(origin + (fwd * NearRingM), origin + (fwd * (NearRingM + 1f)));
                plainRig.PlaceHeld(origin + (fwd * MidRingM), origin + (fwd * (MidRingM + 1f)));
                wingRig.PlaceHeld(origin + (fwd * MidRingM), origin + (fwd * (MidRingM + 1f)));
                if (human != null)
                {
                    human.PlaceHeld(origin + (fwd * FarRingM), origin + (fwd * (FarRingM + 1f)));
                }
            }

            FlightController? Acquire(IReadOnlyList<AiRatingBias>? biases)
            {
                Park();
                gunner.AutoTarget = true;
                gunner.RatingBiases = biases;
                gunner.Target = null;
                shooter.SimStep(StepDt);
                live.SimStep(StepDt);
                return gunner.Target as FlightController;
            }

            // Arm one, the exclusion. The shooter stands in for wingman_4, so the enemy blocks are
            // its candidates and its own side is gated out by team.
            ctx.Check(aceRig.Team != shooter.Team && plainRig.Team != shooter.Team
                      && wingRig.Team == shooter.Team,
                $"the authored teams scan the enemy blocks and gate the wingman out: shooter={shooter.Team} ace={aceRig.Team} plain={plainRig.Team} wing={wingRig.Team}");
            var control = Acquire(null);
            ctx.Check(ReferenceEquals(control, aceRig),
                $"with no biases the nearer '{excluded.Name}' at {NearRingM:0} m is the pick: {control?.Name.ToString() ?? "none"}");
            var biased = Acquire(excluding.Biases);
            ctx.Check(ReferenceEquals(biased, plainRig),
                $"the authored exclusion moves the pick to '{plain.Name}' at {MidRingM:0} m: {biased?.Name.ToString() ?? "none"}");
            ReportRanks(ctx, report, origin, fwd, "exclusion",
                (aceRig, NearRingM), (plainRig, MidRingM), excluding.Biases);

            // Arm two, the player role. bloodhawk_2's own always-target is authored against
            // "player", which no human rig is NAMED (HumanFlightAdapter builds player1), so only
            // the role resolution can carry it. The shooter changes sides to be that ace.
            var playerStats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
            var humanPos = origin + (fwd * FarRingM);
            human = Rig(ctx, planesGamez, textures, playerStats, live, ctx.PlaneName, humanPos,
                humanPos + fwd, human: true, pilot: null, FlightRoster.ShooterIdBase + 50,
                AimAssist.PlayerTeam);
            human.Name = "player1";
            human.Held = true;
            shooter.Team = aceRig.Team;
            ctx.Check(human.Name != AiTargetRanking.PlayerRole,
                $"the human rig is NOT named '{AiTargetRanking.PlayerRole}': {human.Name}");
            var roleControl = Acquire(null);
            ctx.Check(ReferenceEquals(roleControl, wingRig),
                $"with no biases the wingman at {MidRingM:0} m out-ranks the human at {FarRingM:0} m: {roleControl?.Name.ToString() ?? "none"}");
            var roleBiased = Acquire(excluded.Biases);
            ctx.Check(ReferenceEquals(roleBiased, human),
                $"'{excluded.Name}'s authored always-target takes the human instead: {roleBiased?.Name.ToString() ?? "none"}");
            ReportRanks(ctx, report, origin, fwd, "player role",
                (wingRig, MidRingM), (human, FarRingM), excluded.Biases);
        }
        finally
        {
            // Only what this suite put on the host: the host is shared with every other suite in
            // the run, so sweeping it by type would free a neighbour's rig.
            human?.Free();
            var members = new List<FlightController>(roster?.AiAircraft ?? Array.Empty<FlightController>());
            roster?.ClearMembership();
            foreach (var rig in members)
            {
                rig.Free();
            }
            pool?.Free();
            textures.Dispose();
        }

        ctx.WriteArtifact($"test-roster-spawn-names-{BiasChapter}-{BiasMission}.txt", report.ToString());
        ctx.Note($"{BiasChapter}/{BiasMission}'s authored rating_biases reach their spawns and move the pick");
    }

    /// <summary>The session's own AI spawner with no world effects: <c>CrashProgram</c> and
    /// <c>WorldScene</c> stay null, so the assembler's crash-runtime block (their only reader) is
    /// skipped and <c>worldEffects</c> is never dereferenced. Shared with
    /// <see cref="LandingApproachSuites"/>, which spawns a mission's roster for the scaffolding
    /// those rigs carry.</summary>
    internal static FlightRoster Spawner(TestContext ctx, GameZ planesGamez, TextureArchive textures,
        ProjectilePool live)
    {
        var spec = SessionSpec.Parse(Array.Empty<string>());
        var resources = new AircraftAssemblyResources
        {
            PlanesGamez = planesGamez,
            StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
            AiStatsFor = (plane, aiDef) => PlaneStats.LoadForAi(ctx.ZrdrPath, plane, aiDef),
            PaintRng = new RandomNumberGenerator(),
            ZrdrPath = ctx.ZrdrPath,
            StockLoadouts = StockLoadouts.Load(),
            WeaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null),
            // The string table the assembler resolves a spawn's authored name through. Without it
            // every rig falls back to the def-name derivation and the name checks pass vacuously.
            WeaponMessages = Messages.Load(ctx.MessagesPath),
            Textures = textures,
            Shakes = ShakeDefs.Load(ctx.ZrdrPath),
        };
        return new FlightRoster(FlightRosterPolicy.From(spec),
            new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof")),
            null!, ctx.Host, resources,
            new FlightWorldBindings { Projectiles = live, Gamez = planesGamez },
            new HumanRosterBindings());
    }

    /// <summary>CM02's three bombers hold the formation their shared net flies. They are spawned
    /// from that mission's own roster into its own world, flown with nobody engaging them, then one
    /// is hit hard enough that its steady-hand test is certain to fail. What is under test is that
    /// the three leave their seat node the same way and stay on one node together, which the seeded
    /// branch draw did not do (<c>BL-498</c>).</summary>
    internal static void BomberFormation(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, BomberChapter, BomberMission);
        ctx.RequireData(missionZrdr, $"{BomberChapter}/{BomberMission} zrdr");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, BomberChapter);
        ctx.RequireData(texturesPath, $"{BomberChapter} textures");

        CampaignMission? found = null;
        foreach (var m in CampaignSequence.Load(ctx.ZrdrPath))
        {
            if (m.ChapterFolder.Equals(BomberChapter, StringComparison.OrdinalIgnoreCase)
                && m.MissionFolder.Equals(BomberMission, StringComparison.OrdinalIgnoreCase))
            {
                found = m;
            }
        }
        if (found is not { } mission)
        {
            throw new SuiteSkippedException($"{BomberChapter}/{BomberMission} is not in cm_sequence");
        }

        var blocks = AiSkills.LoadRoster(missionZrdr);
        var skills = AiSkills.Load(ctx.ZrdrPath);
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var script = ObjectiveScript.Load(missionZrdr);
        var director = CampaignDirector.Create(script, mission, CampaignProfileDef.NewProfile("Zachary"), null);
        var report = new StringBuilder();
        report.AppendLine($"{BomberChapter}/{BomberMission} seq={mission.Seq}: {blocks.Count} roster block(s)");

        ctx.WithWorld(BomberChapter, collision: false, BomberMission, world =>
        {
            var textures = new TextureArchive(texturesPath);
            var rigs = new List<FlightController>();
            ProjectilePool? pool = null;
            FlightRoster? spawner = null;
            FlightController? player = null;
            try
            {
                var live = new ProjectilePool(textures, null, null);
                pool = live;
                ctx.Host.AddChild(live);
                spawner = Spawner(ctx, planesGamez, textures, live);

                var playerPose = PlayerPose(blocks);
                var playerStats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
                player = Rig(ctx, planesGamez, textures, playerStats, live, ctx.PlaneName,
                    playerPose.Position, playerPose.Position + playerPose.Forward, human: true,
                    pilot: null, FlightRoster.ShooterIdBase, AimAssist.PlayerTeam);

                var human = player;
                director.BuildRoster(new CampaignDirector.RosterInputs
                {
                    ChapterZrdrPath = SessionPaths.ChapterZrdr(ctx.DataRoot, BomberChapter),
                    MissionZrdrPath = missionZrdr,
                    ZrdrPath = ctx.ZrdrPath,
                    MinAiActiveDist = skills.MinAiActiveDist,
                    Player = () => human,
                    NetTrailers = new NetTrailerTargets(
                        () => human.WorldPosition,
                        name => world.Runtime.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null),
                    FindNodes = name => world.Runtime.FindNodes(name),
                    // The session's own assembler, so the machine, the skills and the inert flag
                    // are the ones a played mission gets rather than the plan read back by hand.
                    Spawn = (plan, pos, look, pilot) =>
                    {
                        var rig = spawner!.SpawnAi(CampaignRosterPlan.SpawnFor(plan, pos, look, pilot));
                        rigs.Add(rig);
                        return rig;
                    },
                    Rng = new System.Random(1),
                });

                FlyBombers(ctx, director.Roster, rigs, live, report);
            }
            finally
            {
                player?.Free();
                var members = new List<FlightController>(
                    spawner?.AiAircraft ?? Array.Empty<FlightController>());
                spawner?.ClearMembership();
                foreach (var rig in members)
                {
                    rig.Free();
                }
                pool?.Free();
                textures.Dispose();
            }
        });

        ctx.WriteArtifact($"test-bomber-formation-{BomberChapter}-{BomberMission}.txt", report.ToString());
        ctx.Note($"flew {BomberChapter}/{BomberMission}'s three netted bombers, unattacked and then fired on");
    }

    // The decoded fork per block: wingman_1 escorts the player with no net; a wingman whose
    // primary_target is another block escorts THAT rig; a netted block patrols and never escorts;
    // a deactivated block is inert and out of play.
    private static void CheckFork(TestContext ctx, IReadOnlyDictionary<string, FlightController> roster,
        FlightController player, StringBuilder report)
    {
        int escorts = 0, netted = 0, inert = 0;
        foreach (var (name, rig) in roster)
        {
            var pilot = rig.Pilot;
            bool hasEscort = pilot?.Escort != null;
            bool hasNet = pilot?.Patrol != null;
            ctx.Check(!(hasEscort && hasNet), $"'{name}' never carries both an escort and a patrol net");
            escorts += hasEscort ? 1 : 0;
            netted += hasNet ? 1 : 0;
            inert += rig.Inert ? 1 : 0;
            report.AppendLine($"  {name}: escort={(hasEscort ? pilot!.Escort!.Leader?.Name.ToString() : "-")} " +
                              $"net={(hasNet ? pilot!.Patrol!.Net.Id.ToString() : "-")} inert={rig.Inert} team={rig.Team}");
        }
        report.AppendLine($"{escorts} escort(s), {netted} netted, {inert} inert");

        ctx.Check(roster.TryGetValue(CampaignDirector.WingmanName, out var wingman1),
            $"'{CampaignDirector.WingmanName}' is spawned");
        if (wingman1?.Pilot is { } w1)
        {
            ctx.Check(w1.Escort is { } e && ReferenceEquals(e.Leader, player),
                $"wingman_1 carries an escort whose leader is the player rig");
            ctx.Check(w1.Patrol == null, $"wingman_1 (netless mode wingman) carries no patrol net");
        }

        if (roster.TryGetValue("wingman_3", out var wingman3) && roster.TryGetValue("devastator_3", out var dev3))
        {
            ctx.Check(wingman3.Pilot?.Escort is { } e3 && ReferenceEquals(e3.Leader, dev3),
                $"wingman_3's primary_target names devastator_3 and resolves to that rig");
        }

        if (roster.TryGetValue("blakepeace_2_1", out var netBlock))
        {
            ctx.Check(netBlock.Pilot?.Patrol is { Net.Id: 15 },
                $"blakepeace_2_1 (netids 15) carries that net as its patrol: {netBlock.Pilot?.Patrol?.Net.Id}");
            ctx.Check(netBlock.Pilot?.Escort == null, $"…and no escort (a net demotes any wingman)");
        }

        if (roster.TryGetValue("blakebloodhawk_8", out var parked))
        {
            ctx.Check(parked.Inert && !parked.InPlay, $"blakebloodhawk_8 (deactivated 1) is inert and out of play");
        }
        ctx.Check(escorts == 3, $"the three wingman blocks are the mission's three escorts: {escorts}");
        ctx.Check(inert == 9, $"the nine deactivated blocks are inert: {inert}");
    }

    // The four taxi-path vehicles are placed, frozen on their authored paths in the built world.
    private static void CheckPaths(TestContext ctx, CampaignDirector director, StringBuilder report)
    {
        var paths = director.Paths;
        ctx.Check(paths != null, $"the roster build created the scripted-path registry");
        if (paths == null)
        {
            return;
        }
        ctx.Same(4, paths.Count, $"the four blocks authoring a taxiPath are placed");
        ctx.Check(paths.IsFrozen("blakepeace_2_3"), $"blakepeace_2_3 (pp1) is frozen on its path until START_TAXI");
        report.AppendLine($"{paths.Count} vehicle(s) on a path");
    }

    // The volume order: a net's authored 700 m return radius reaches a block that authors none of
    // its own, and the activation radius is floored at min_ai_active_dist.
    private static void CheckVolumes(TestContext ctx, IReadOnlyDictionary<string, FlightController> roster,
        AiSkills skills, StringBuilder report)
    {
        if (roster.TryGetValue("devastator_2", out var dev2) && dev2.Pilot?.Machine is { } m)
        {
            report.AppendLine($"devastator_2 volumes: act={m.ActivationRange:0} att={m.AttackRange:0} ret={m.ReturnRange:0}");
            ctx.Check(Mathf.IsEqualApprox(m.ReturnRange, 700f),
                $"devastator_2 takes its net's authored 700 m return radius: {m.ReturnRange:0} m");
            ctx.Check(m.ActivationRange >= skills.MinAiActiveDist,
                $"…and its activation radius is floored at min_ai_active_dist: {m.ActivationRange:0} m");
        }
    }

    // The flown half: wingman_1 stays with the scripted player rig over two minutes, judged over
    // the second minute against the wingman-station suite's own leash.
    private static void FlyEscort(TestContext ctx, IReadOnlyDictionary<string, FlightController> roster,
        FlightController player, List<FlightController> rigs, ProjectilePool live,
        CampaignDirector director, StringBuilder report)
    {
        if (!roster.TryGetValue(CampaignDirector.WingmanName, out var wing) || wing.Pilot?.Escort is not { } escort)
        {
            return;
        }
        // The flown leg is lifted well clear of the chapter's terrain: the roster poses (110 m
        // at the player block) are the spawner's business, and under a collision world a
        // scripted leader at that height flies into the ground.
        var up = Vector3.Up * FlownLegLiftM;
        player.Activate(player.WorldPosition + up, player.WorldPosition + up + player.NoseDirection);
        wing.Activate(wing.WorldPosition + up, wing.WorldPosition + up + wing.NoseDirection);
        float worstLate = 0f, meanRange = 0f;
        int lateSamples = 0;
        bool joined = false;
        for (int i = 0; i < (int)(RunS / StepDt); i++)
        {
            live.SimStep(StepDt);
            foreach (var rig in rigs)
            {
                if (rig.InPlay)
                {
                    rig.SimStep(StepDt);
                }
            }
            director.Paths?.Step(StepDt);
            float range = wing.WorldPosition.DistanceTo(player.WorldPosition);
            joined |= escort.State == EscortState.Station;
            if (i % 1800 == 0)
            {
                ctx.Note($"t={i * StepDt:0}s range={range:0} escort={escort.State} mode={AiModeMachine.NameOf(wing.Pilot!.Machine!.Mode)} wingV={wing.WorldVelocity.Length():0} leadV={player.WorldVelocity.Length():0}");
            }
            if (i * StepDt >= HoldFromS)
            {
                lateSamples++;
                worstLate = Mathf.Max(worstLate, range);
                meanRange += range;
            }
        }
        meanRange /= Mathf.Max(1, lateSamples);
        report.AppendLine($"wingman_1 hold: mean {meanRange:0} m, worst {worstLate:0} m over the last minute, joined={joined}");
        ctx.Check(joined, $"wingman_1 joins the formation state on the player");
        ctx.Check(wing.InPlay && player.InPlay, $"both the player and wingman_1 are still flying after {RunS:0} s");
        ctx.Check(worstLate > 0f && worstLate < LeashM,
            $"wingman_1 stays with the player: worst {worstLate:0} m of {LeashM:0} over the last minute");
        ctx.Check(meanRange < MeanHoldM,
            $"…averaging inside {MeanHoldM:0} m of the player: {meanRange:0} m");
    }

    // The flown half: the three bombers over a minute of their shared net, with one hit part way.
    private static void FlyBombers(TestContext ctx, IReadOnlyDictionary<string, FlightController> roster,
        List<FlightController> rigs, ProjectilePool live, StringBuilder report)
    {
        var bombers = new List<FlightController>();
        foreach (string name in BomberBlocks)
        {
            if (roster.TryGetValue(name, out var rig))
            {
                bombers.Add(rig);
            }
        }
        ctx.Same(BomberBlocks.Length, bombers.Count, $"the three britbalmoral blocks all have a rig");
        if (bombers.Count != BomberBlocks.Length)
        {
            return;
        }
        foreach (var rig in bombers)
        {
            ctx.Check(rig.Pilot?.Patrol is { Net.Id: BomberNetId },
                $"{rig.Name} carries its authored net {BomberNetId}: {rig.Pilot?.Patrol?.Net.Id}");
        }

        float spawnSpread = Spread(bombers);
        report.AppendLine($"spawn spread {spawnSpread:0} m");
        // Each aircraft's own walk, appended when it steps. They are a hundred metres apart, so
        // they draw abeam a node frames apart; what must match is the route, not the frame.
        var routes = new List<List<int>> { new(), new(), new() };
        bool split = false, drifted = false, wasHit = false;
        float worstSpread = 0f;
        int fireFrame = (int)(BomberFireAtS / StepDt);
        for (int i = 0; i < (int)(BomberRunS / StepDt); i++)
        {
            live.SimStep(StepDt);
            foreach (var rig in rigs)
            {
                if (rig.InPlay)
                {
                    rig.SimStep(StepDt);
                }
            }
            if (i == fireFrame && bombers[1].Pilot?.Machine is { } hit)
            {
                // A certain steady-hand failure, which is the worst the roll can do: the reaction
                // runs rather than being rolled away, and the aircraft must still hold its net.
                hit.SteadyHandChance = 1f;
                hit.NotifyDamage(20f, Vector3.Right);
                wasHit = hit.Mode != AiMode.Patrol;
                report.AppendLine($"t={i * StepDt:0}s hit {bombers[1].Name}: mode={AiModeMachine.NameOf(hit.Mode)}");
            }
            int node = bombers[0].Pilot!.Patrol!.CurrentIndex;
            for (int b = 0; b < bombers.Count; b++)
            {
                var walk = bombers[b].Pilot!.Patrol!;
                var route = routes[b];
                if (route.Count == 0 || route[route.Count - 1] != walk.CurrentIndex)
                {
                    route.Add(walk.CurrentIndex);
                }
                // A lag of more than one node is a divergence rather than a straggler.
                split |= Math.Abs(walk.Advances - bombers[0].Pilot!.Patrol!.Advances) > 1;
            }
            float spread = Spread(bombers);
            worstSpread = Mathf.Max(worstSpread, spread);
            drifted |= spread > BomberSpreadCeilingM;
            if (i % 600 == 0)
            {
                string modes = $"{AiModeMachine.NameOf(bombers[0].Pilot!.Machine!.Mode)}/"
                    + $"{AiModeMachine.NameOf(bombers[1].Pilot!.Machine!.Mode)}/"
                    + $"{AiModeMachine.NameOf(bombers[2].Pilot!.Machine!.Mode)}";
                ctx.Note($"t={i * StepDt:0}s node={node} spread={spread:0} m modes={modes}");
            }
        }

        float finalSpread = Spread(bombers);
        for (int b = 0; b < routes.Count; b++)
        {
            report.AppendLine($"{bombers[b].Name} route: {string.Join(" ", routes[b])}");
        }
        report.AppendLine($"worst spread {worstSpread:0} m, final {finalSpread:0} m, split={split}");
        string route0 = string.Join(" ", routes[0]);
        ctx.Check(string.Join(" ", routes[1]) == route0 && string.Join(" ", routes[2]) == route0,
            $"all three walk the same nodes of their net: {route0}");
        ctx.Check(!split, $"…never more than one node apart over the whole {BomberRunS:0} s");
        ctx.Check(wasHit, $"the hit did run a reaction, so the held formation is not an unfired test");
        ctx.Check(!drifted,
            $"…and never spread past {BomberSpreadCeilingM:0} m of each other: worst {worstSpread:0} m");
        ctx.Check(finalSpread < BomberSpreadCeilingM,
            $"the one that was fired on is back with the other two: {finalSpread:0} m");
    }

    // The widest gap between any two of the group, metres.
    private static float Spread(List<FlightController> group)
    {
        float worst = 0f;
        for (int i = 0; i < group.Count; i++)
        {
            for (int j = i + 1; j < group.Count; j++)
            {
                worst = Mathf.Max(worst, group[i].WorldPosition.DistanceTo(group[j].WorldPosition));
            }
        }
        return worst;
    }

    private static (Vector3 Position, Vector3 Forward) PlayerPose(
        IReadOnlyList<(string Name, List<object?> Fields)> blocks)
    {
        foreach (var (name, fields) in blocks)
        {
            if (name.Equals(CampaignRosterPlan.PlayerBlock, StringComparison.OrdinalIgnoreCase)
                && AiSkills.RosterSpawnPose(fields) is { } pose)
            {
                return (pose.Position, new Basis(Vector3.Up, Mathf.DegToRad(pose.YawDeg)) * Vector3.Forward);
            }
        }
        return (new Vector3(0f, 500f, 0f), Vector3.Forward);
    }

    // The AI flavour of the plan's airframe; a def PlaneStats cannot resolve as a variant of that
    // airframe falls back to the plain base def, which is what the session's own spawner does.
    private static PlaneStats StatsFor(TestContext ctx, RosterSpawnPlan plan, StringBuilder report)
    {
        try
        {
            return PlaneStats.LoadForAi(ctx.ZrdrPath, plan.PlaneNode, plan.AiDef);
        }
        catch (ArgumentException e)
        {
            report.AppendLine($"  {plan.Name}: '{plan.AiDef}' not loadable as a variant of {plan.PlaneNode} ({e.Message}); base def used");
            return PlaneStats.LoadForAi(ctx.ZrdrPath, plan.PlaneNode);
        }
    }

    // One aircraft on the suite's own stage, the shape WingmanSuites.Rig builds: no camera, no
    // HUD, no devices.
    private static FlightController Rig(TestContext ctx, GameZ planesGamez, TextureArchive textures,
        PlaneStats stats, ProjectilePool live, string planeNode, Vector3 pos, Vector3 lookAt, bool human,
        AiPilot? pilot, int shooterId, int team)
    {
        var model = new PlaneBuilder(planesGamez, textures).Build(planeNode);
        var rig = new FlightController
        {
            PlaneModel = model,
            Collider = PlaneCollider.Build(model),
            Damage = stats.DestroyableParts.Count > 0 || stats.VehicleHealth is > 0f ? PlaneDamage.For(stats) : null,
            PlayerIndex = shooterId,
            IsHumanPiloted = human,
            Pilot = pilot,
            Projectiles = live,
            UseKeyboard = false,
            PadDevices = Array.Empty<int>(),
            AllowPause = false,
            Team = team,
        };
        rig.AddChild(model);
        if (human)
        {
            rig.Setup(new FlightModel(stats), null, new CamParams(), pos, lookAt, LeaderThrottle, LeaderSpeedMps);
        }
        else
        {
            rig.Setup(new FlightModel(stats, aiForcePath: true), null, new CamParams(), pos, lookAt);
        }
        rig.Name = $"{planeNode}_{shooterId}";
        ctx.Host.AddChild(rig);
        return rig;
    }

    private static string Bias(AiRatingBias? entry) =>
        entry?.Bias.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) ?? "-";

    private static RosterSpawnPlan? PlanNamed(CampaignRosterPlan plan, string name)
    {
        foreach (var spawn in plan.Spawns)
        {
            if (spawn.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return spawn;
            }
        }
        return null;
    }

    // The two candidates' ranks with and without the authored list, computed on the same inputs
    // the live acquisition builds, so the artifact carries the numbers behind each pick.
    private static void ReportRanks(TestContext ctx, StringBuilder report, Vector3 origin,
        Vector3 fwd, string arm, (FlightController Rig, float Ring) a, (FlightController Rig, float Ring) b,
        IReadOnlyList<AiRatingBias> biases)
    {
        report.AppendLine($"-- {arm} --");
        foreach (var (rig, ring) in new[] { a, b })
        {
            string key = rig.IsHumanPiloted ? AiTargetRanking.PlayerRole : rig.Name.ToString();
            var candidate = new RankedTargetCandidate
            {
                Position = origin + (fwd * ring),
                Forward = fwd,
                IsPlayer = rig.IsHumanPiloted,
                ObjectiveBias = 0f,
            };
            float plainRank = AiTargetRanking.Score(origin, fwd, ScanRangeM, candidate).Rank;
            candidate.ObjectiveBias = AiTargetRanking.ObjectiveBiasFor(key, biases);
            float biasedRank = AiTargetRanking.Score(origin, fwd, ScanRangeM, candidate).Rank;
            report.AppendLine($"  {rig.Name} as '{key}' at {ring:0} m: rank {plainRank:0.#} -> "
                              + $"{biasedRank:0.#} (bias term {candidate.ObjectiveBias:0.#})");
        }
        ctx.Note($"{arm}: ranks written to the artifact");
    }
}
