using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Effects;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Mech3.Anim;
using CSVM.Session;
using CSVM.UI;
using CSVM.Utils;
using Godot;

using static CSVM.Testing.SuiteConstants;
namespace CSVM.Testing;

internal static class InstantActionSuites
{
    internal static void InstantActionAce(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        // The display-name -> gamez-node table (docs/formats/instant-action.md's IDS_IA_PLANES
        // order): a real entry resolves, a typo/invention does not.
        ctx.Check(InstantAction.PlaneNodeFor("Warhawk") == "player_warhawk",
            $"'Warhawk' resolves to its gamez node: {InstantAction.PlaneNodeFor("Warhawk")}");
        ctx.Check(InstantAction.PlaneNodeFor("Warhawk Mk2") == null,
            $"an unrecognised display name resolves to null, not a guess");

        // RepresentativeRating: every shipped chapter's ace_stats is a uniform 9 (spawns.md), but
        // a hand-authored --ia= file could differ — eight 9s and one 7 must average (and round)
        // to 9, not silently pick one arbitrary slot.
        var mixedStats = new AiSkillVector
        {
            DareDevil = 9,
            NaturalTouch = 9,
            SixthSense = 9,
            DeadEye = 7,
            QuickDraw = 9,
            SteadyHand = 9,
            StunRecovery = 9,
            Talker = 9,
            Constitution = 9,
        };
        int rating = InstantActionRuntime.RepresentativeRating(mixedStats);
        ctx.Check(rating == 9, $"eight 9s and one 7 average (rounded) to 9: rating={rating}");
        ctx.Check(InstantActionRuntime.RepresentativeRating(default) == 5,
            $"every slot unset falls back to 5, the same default --ai-attack= takes");

        // ChooseAceSpawn: a draw landing on the player's own index substitutes the LITERAL last
        // index (never a re-roll); a non-colliding draw is used as-is.
        var spawns = new List<SpawnPoint>
        {
            new(new Vector3(0f, 500f, 0f), 0f),
            new(new Vector3(100f, 500f, 0f), 0f),
            new(new Vector3(200f, 500f, 0f), 90f),
        };
        var (collided, _) = InstantActionRuntime.ChooseAceSpawn(spawns, playerSpawnIndex: 0, draw: 0);
        ctx.Check(collided == spawns.Count - 1,
            $"a draw colliding with the player's index substitutes the literal last index: idx={collided}");
        var (clean, _) = InstantActionRuntime.ChooseAceSpawn(spawns, playerSpawnIndex: 1, draw: 0);
        ctx.Check(clean == 0, $"a non-colliding draw is used as-is: idx={clean}");

        // D9: the wingman standing-order table (docs/formats/instant-action.md "The player and
        // the wingmen") is pure over its 0-based index — fan placement, the escort chain (0/1/3
        // escort the player; 2/4 escort wingmen 1/3), and the authored accent ids.
        var slot0 = InstantActionRuntime.WingmanSlotFor(0);
        ctx.Check(slot0 is { MetresOut: 100f, OffsetDeg: -45f, PrimaryTargetIsWingman: null, AccentId: 12 },
            $"wingman 0: 100 m / -45°, escorts the player, accent 12: {slot0}");
        var slot1 = InstantActionRuntime.WingmanSlotFor(1);
        ctx.Check(slot1 is { MetresOut: 100f, OffsetDeg: 45f, PrimaryTargetIsWingman: null, AccentId: 14 },
            $"wingman 1: 100 m / +45°, escorts the player, accent 14: {slot1}");
        var slot2 = InstantActionRuntime.WingmanSlotFor(2);
        ctx.Check(slot2 is { MetresOut: 200f, OffsetDeg: 45f, PrimaryTargetIsWingman: 1, AccentId: 15 },
            $"wingman 2: 200 m / +45°, escorts wingman 1, accent 15: {slot2}");
        var slot3 = InstantActionRuntime.WingmanSlotFor(3);
        ctx.Check(slot3 is { MetresOut: 200f, OffsetDeg: -45f, PrimaryTargetIsWingman: null, AccentId: 13 },
            $"wingman 3: 200 m / -45°, escorts the player, accent 13: {slot3}");
        var slot4 = InstantActionRuntime.WingmanSlotFor(4);
        ctx.Check(slot4 is { MetresOut: 300f, OffsetDeg: -45f, PrimaryTargetIsWingman: 3, AccentId: 16 },
            $"wingman 4: 300 m / -45°, escorts wingman 3, accent 16: {slot4}");

        // Decision 8a's flight-size clamp: 1-4 humans against 5 configured wingmen expects
        // 5/4/3/2, and a below-cap case (2 humans, 2 configured) proves the configured count
        // stands untouched. 0 configured always flies none.
        ctx.Check(InstantActionRuntime.FlownWingmen(5, humans: 1) == 5, $"1 human, 5 configured: flies all 5");
        ctx.Check(InstantActionRuntime.FlownWingmen(5, humans: 2) == 4, $"2 humans, 5 configured: clamped to 4");
        ctx.Check(InstantActionRuntime.FlownWingmen(5, humans: 3) == 3, $"3 humans, 5 configured: clamped to 3");
        ctx.Check(InstantActionRuntime.FlownWingmen(5, humans: 4) == 2, $"4 humans, 5 configured: clamped to 2");
        ctx.Check(InstantActionRuntime.FlownWingmen(2, humans: 2) == 2, $"below the cap: 2 humans/2 configured stays 2");
        ctx.Check(InstantActionRuntime.FlownWingmen(0, humans: 1) == 0, $"0 configured flies none");

        // The actual spawn integration: FlightRoster.SpawnAi given an authored scheme/team
        // (the C8 extension) wears them as-is — the ace lands on team 2 flying the configured
        // airframe, not a pilot-index-derived team.
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null);
        var stockLoadouts = StockLoadouts.Load();
        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? ace = null;
        var wingmen = new List<FlightController>();
        var waveMembers = new List<FlightController>();
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            var spec = SessionSpec.Parse(System.Array.Empty<string>());
            var liveries = new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof"));
            var inputs = new HumanFlightAdapter.Inputs
            {
                PlanesGamez = planesGamez,
                StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
                AiStatsFor = plane => PlaneStats.LoadForAi(ctx.ZrdrPath, plane),
                RigCount = 0,
                PaintRng = new RandomNumberGenerator(),
                ZrdrPath = ctx.ZrdrPath,
                StockLoadouts = stockLoadouts,
                WeaponDefs = weaponDefs,
                Textures = textures,
                Projectiles = live,
                Shakes = ShakeDefs.Load(ctx.ZrdrPath),
            };
            // worldEffects null!: never dereferenced — Inputs.CrashProgram/WorldScene stay null,
            // so Spawn's crash-runtime block (the only reader) is skipped.
            var spawner = new FlightRoster(spec, liveries, null!, ctx.Host, inputs);

            string aceNode = InstantAction.PlaneNodeFor("Warhawk")!;
            var aceLivery = new PaintScheme { Pattern = "cccp", Color1 = PaintScheme.FromBytes(200, 10, 10) };
            var pos = new Vector3(0f, 500f, 0f);
            var pilot = AiPilot.HoldingCourse(pos, pos + Vector3.Forward);
            ace = spawner.SpawnAi(new AiSpawn(aceNode, pos, pos + Vector3.Forward, pilot,
                Scheme: aceLivery, Team: InstantActionRuntime.EnemyTeam));

            ctx.Check(ace.Team == InstantActionRuntime.EnemyTeam,
                $"the spawned ace carries the authored team, not a pilot-index default: team={ace.Team}");
            ctx.Check(ace.Name.ToString().Contains(aceNode),
                $"the ace flies its configured airframe: {ace.Name}");

            // The wingman census: N aircraft on team 1, since humans and wingmen share the player's side,
            // flying the configured airframe. Spawned through the same FlightRoster.SpawnAi seam as the
            // ace above, on AimAssist.PlayerTeam instead of the enemy team.
            string wingmanNode = InstantAction.PlaneNodeFor("Fury")!;
            for (int i = 0; i < 3; i++)
            {
                var wPos = new Vector3(500f + i * 10f, 500f, 0f);
                var wPilot = AiPilot.HoldingCourse(wPos, wPos + Vector3.Forward);
                wingmen.Add(spawner.SpawnAi(new AiSpawn(wingmanNode, wPos, wPos + Vector3.Forward, wPilot,
                    Scheme: null, Team: AimAssist.PlayerTeam)));
            }
            ctx.Check(wingmen.Count == 3, $"3 wingmen spawned: {wingmen.Count}");
            ctx.Check(wingmen.All(w => w.Team == AimAssist.PlayerTeam),
                $"every wingman carries team 1, not a pilot-index default: teams={string.Join(",", wingmen.Select(w => w.Team))}");
            ctx.Check(wingmen.All(w => w.Name.ToString().Contains(wingmanNode)),
                $"every wingman flies the configured airframe: {string.Join(",", wingmen.Select(w => w.Name))}");

            // The synthetic roster block writes 10000 m into all three volumes, so the airframe gates the
            // spawner seeds first must not survive on an Instant Action actor. Overriding activation alone
            // leaves attack as the real engagement gate: the mode machine enters pursue on the minimum of two.
            var volPos = new Vector3(600f, 500f, 0f);
            var volPilot = AiPilot.HoldingCourse(volPos, volPos + Vector3.Forward);
            volPilot.Machine = new AiModeMachine(new System.Random(7));
            wingmen.Add(spawner.SpawnAi(new AiSpawn(wingmanNode, volPos, volPos + Vector3.Forward, volPilot,
                Scheme: null, Team: AimAssist.PlayerTeam)));
            var vol = volPilot.Machine;
            ctx.Check(Mathf.IsEqualApprox(vol.AttackRange, 2000f)
                && Mathf.IsEqualApprox(vol.ReturnRange, 1200f),
                $"the spawner seeds the airframe's own gates first: attack={vol.AttackRange:0} return={vol.ReturnRange:0}");
            InstantActionRuntime.ApplyActorVolumes(vol);
            ctx.Check(Mathf.IsEqualApprox(vol.ActivationRange, InstantActionRuntime.ActorVolumeRadiusM)
                && Mathf.IsEqualApprox(vol.AttackRange, InstantActionRuntime.ActorVolumeRadiusM)
                && Mathf.IsEqualApprox(vol.ReturnRange, InstantActionRuntime.ActorVolumeRadiusM),
                $"all three volumes take the authored 10000 m: activation={vol.ActivationRange:0} attack={vol.AttackRange:0} return={vol.ReturnRange:0}");

            // E11: RandomPilotStats/ResolveWaveAccentId are pure over their draw — row 4 is the
            // flat-4 personality, and only accent 12 (the wingman range's own base) re-rolls.
            var flatRow = InstantActionRuntime.RandomPilotStats(draw: 4);
            ctx.Check(flatRow is { DareDevil: 4, Constitution: 4 },
                $"draw 4 selects row 4, the flat personality: {flatRow}");
            ctx.Check(InstantActionRuntime.ResolveWaveAccentId(12, draw: 3) == 15,
                $"accent 12 re-rolls to 12 + draw%5: {InstantActionRuntime.ResolveWaveAccentId(12, 3)}");
            ctx.Check(InstantActionRuntime.ResolveWaveAccentId(7, draw: 99) == 7,
                $"any other accent id passes through unchanged: {InstantActionRuntime.ResolveWaveAccentId(7, 99)}");

            // E11: a real InstantActionWaves sequence over real spawned aircraft — wave 1 (2
            // members, live) killed down to 0 triggers wave 2 (1 member, built inert) activating
            // at a spawn point at least 500 m from the human, fanned off it.
            var iaWaves = new InstantActionWaves(new[] { 2, 1, 0, 0 });
            int firstWave = iaWaves.Start();
            ctx.Check(firstWave == 1, $"wave 1 is current at mission start: {firstWave}");

            string waveNode = InstantAction.PlaneNodeFor("Brigand")!;
            var wave1Pos = new Vector3(0f, 500f, 0f);
            for (int i = 0; i < 2; i++)
            {
                var wmPos = wave1Pos + new Vector3(i * 10f, 0f, 0f);
                var wmPilot = AiPilot.HoldingCourse(wmPos, wmPos + Vector3.Forward);
                waveMembers.Add(spawner.SpawnAi(new AiSpawn(waveNode, wmPos, wmPos + Vector3.Forward, wmPilot,
                    Scheme: null, Team: InstantActionRuntime.EnemyTeam)));
            }
            var wave2Pilot = AiPilot.HoldingCourse(Vector3.Zero, Vector3.Forward);
            // Every Instant Action actor carries the chapter's first patrol net and an inert one still ticks,
            // so this two-node stand-in net has a node at the parking pose and another out at the wave spawn
            // point, to watch which one it flies after the teleport.
            var parkNet = new AiNet
            {
                Id = 1,
                Name = "TestWaveNet",
                Nodes = new[]
                {
                    new AiNetNode(Vector3.Zero, System.Array.Empty<float>()),
                    new AiNetNode(new Vector3(600f, 500f, 0f), System.Array.Empty<float>()),
                },
                Edges = new[] { (0, 1) },
            };
            wave2Pilot.Patrol = new AiNetFollower(parkNet, new System.Random(3));
            var wave2Member = spawner.SpawnAi(new AiSpawn(waveNode, Vector3.Zero, Vector3.Forward, wave2Pilot,
                Scheme: null, Team: InstantActionRuntime.EnemyTeam, Inert: true));
            waveMembers.Add(wave2Member);
            wave2Pilot.Patrol.Update(Vector3.Zero);
            ctx.Check(wave2Pilot.Patrol.CurrentIndex == 0,
                $"parked inert, the follower seats on the node by the parking pose: idx={wave2Pilot.Patrol.CurrentIndex}");

            int aliveWave1 = waveMembers.Take(2).Count(m => m.InPlay);
            ctx.Check(aliveWave1 == 2, $"both wave-1 members InPlay before any kill: {aliveWave1}");
            ctx.Check(!wave2Member.InPlay, $"wave 2's member is inert (not InPlay) before activation");
            ctx.Check(iaWaves.Step(aliveInCurrentWave: aliveWave1) == 0,
                $"wave 1 still alive: no advance");

            foreach (var m in waveMembers.Take(2))
                m.DebugForceCrash();
            int aliveAfterKills = waveMembers.Take(2).Count(m => m.InPlay);
            ctx.Check(aliveAfterKills == 0, $"both wave-1 members crashed: {aliveAfterKills}");
            int nextWave = iaWaves.Step(aliveInCurrentWave: aliveAfterKills);
            ctx.Check(nextWave == 2, $"wave 1's last kill advances to wave 2: {nextWave}");
            ctx.Check(iaWaves.CurrentWave == 2 && !iaWaves.Finished,
                $"the sequencer's own state agrees: {iaWaves.CurrentWave}");

            var humanPos = new Vector3(0f, 500f, 0f);
            var waveSpawns = new List<SpawnPoint>
            {
                new(new Vector3(10f, 500f, 0f), 0f),      // 10 m from the human — too close
                new(new Vector3(600f, 500f, 0f), 0f),     // 600 m — eligible
            };
            var (spIdx, sp) = InstantActionWaves.ChooseWaveSpawn(
                waveSpawns, new[] { humanPos }, draw: 0);
            ctx.Check(spIdx == 1, $"the near point is excluded, the far one drawn: idx={spIdx}");
            var fwd2 = new Basis(Vector3.Up, Mathf.DegToRad(sp.HeadingDeg)) * Vector3.Forward;
            wave2Member.Activate(sp.Position, sp.Position + fwd2);
            ctx.Check(wave2Member.InPlay, $"wave 2's member is InPlay once activated");
            float distSq = wave2Member.WorldPosition.DistanceSquaredTo(humanPos);
            ctx.Check(distSq >= InstantActionWaves.MinSpawnDistanceSquared,
                $"activated at least 500 m from the human: dist={Mathf.Sqrt(distSq):0} m");
            // The activation snap the original does (FUN_004b0f40 → FUN_00432010): the arrival
            // re-seats the walk, so the member patrols from where it was put down instead of
            // flying back to the node by its parking pose.
            wave2Pilot.Patrol.Update(wave2Member.WorldPosition);
            ctx.Check(wave2Pilot.Patrol.CurrentIndex == 1,
                $"activation re-seats it on the node by its ARRIVAL: idx={wave2Pilot.Patrol.CurrentIndex}");
        }
        finally
        {
            pool?.Free();
            ace?.Free();
            foreach (var w in wingmen)
                w.Free();
            foreach (var m in waveMembers)
                m.Free();
            textures.Dispose();
        }
    }

    // The F12 zeppelin run: the objective-zeppelin selection, the builder's own switch,
    // and the wave arm that replaces E11's teleport. Everything runs over C1/IA1's real
    // `ia.zrd.json` / `egen.zrd.json` / `zeppelins.zrd.json`, on the same host +
    // `cargobay` stand-in world the `zeppelin-launch` suite uses, so the drop geometry
    // under test is the one `AiGeneratorRuntime` already owns.
    internal static void InstantActionZeppelin(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, "C1");
        ctx.RequireData(chapterZrdr, $"C1 zrdr");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C1", "IA1");
        ctx.RequireData(missionZrdr, $"C1/IA1 zrdr");

        // The objective selection, over the shipped file: C1 authors zeppelin_type cargo and
        // names multiplayer1zep for all three types.
        var iaDef = InstantAction.Load(missionZrdr);
        ctx.Check(iaDef.ZeppelinType == "cargo",
            $"C1/IA1 authors zeppelin_type: {iaDef.ZeppelinType ?? "(unauthored)"}");
        ctx.Check(InstantActionRuntime.SelectedZeppelinNode(iaDef) == "multiplayer1zep",
            $"cargo selects the cargo_zeppelin node: {InstantActionRuntime.SelectedZeppelinNode(iaDef)}");
        ctx.Same(3, InstantActionRuntime.ZeppelinNodes(iaDef).Count,
            $"the three *_zeppelin names are offered in type order");
        ctx.Check(InstantActionRuntime.ZeppelinTypeIndex("passenger") == 1
            && InstantActionRuntime.ZeppelinTypeIndex("military") == 2,
            $"passenger/military index 1/2");
        // ⚠ The fallback is cargo, not an error: the parser REJECTS an unrecognised string over a
        // record whose reset wrote 0 (FUN_00458ff0's param_1[0x95] = 0).
        ctx.Check(InstantActionRuntime.ZeppelinTypeIndex(null) == 0
            && InstantActionRuntime.ZeppelinTypeIndex("blimp") == 0,
            $"an unauthored or unrecognised zeppelin_type falls back to cargo (index 0)");

        var egen = EnemyGenerators.Load(missionZrdr);
        ctx.Check(egen.Count == 1 && egen[0].Node == "multiplayer1zep" && egen[0].IsZeppelin,
            $"the objective zeppelin is the host of C1/IA1's one generator count={egen.Count}");
        if (egen.Count != 1)
            return;
        var genDef = egen[0];
        var nets = AiNets.Load(chapterZrdr);
        var zepDefs = Zeppelins.Load(missionZrdr);
        ctx.Check(zepDefs.Count == 1 && zepDefs[0].Node == "multiplayer1zep",
            $"…and of its one zeppelin record count={zepDefs.Count}");
        if (zepDefs.Count != 1)
            return;

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null);
        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        Node3D? host = null;
        Node3D? heldHost = null;
        AiGeneratorRuntime? gens = null;
        ZeppelinRuntime? zeps = null;
        var wave1 = new List<FlightController>();
        var wave2 = new List<FlightController>();
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);
            var spec = SessionSpec.Parse(System.Array.Empty<string>());
            var liveries = new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof"));
            var inputs = new HumanFlightAdapter.Inputs
            {
                PlanesGamez = planesGamez,
                StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
                AiStatsFor = plane => PlaneStats.LoadForAi(ctx.ZrdrPath, plane),
                RigCount = 0,
                PaintRng = new RandomNumberGenerator(),
                ZrdrPath = ctx.ZrdrPath,
                StockLoadouts = StockLoadouts.Load(),
                WeaponDefs = weaponDefs,
                Textures = textures,
                Projectiles = live,
                Shakes = ShakeDefs.Load(ctx.ZrdrPath),
            };
            var spawner = new FlightRoster(spec, liveries, null!, ctx.Host, inputs);

            // Both waves built INERT at the origin, which is what the original does on this one
            // mode for wave 1 as well ("even wave 1 is built deactivated at the origin").
            string waveNode = InstantAction.PlaneNodeFor("Firebrand")!;
            List<FlightController> BuildWave(int count)
            {
                var built = new List<FlightController>(count);
                for (int i = 0; i < count; i++)
                {
                    var pilot = AiPilot.HoldingCourse(Vector3.Zero, Vector3.Forward);
                    built.Add(spawner.SpawnAi(new AiSpawn(waveNode, Vector3.Zero, Vector3.Forward, pilot,
                        Scheme: null, Team: InstantActionRuntime.EnemyTeam, Inert: true)));
                }
                return built;
            }

            wave1.AddRange(BuildWave(6));
            wave2.AddRange(BuildWave(3));
            ctx.Check(wave1.All(m => m.Inert) && wave2.All(m => m.Inert),
                $"both waves are parked inert before the mission starts");

            // The stand-in world: the host above its 100 m launch gate, with the authored
            // cargobay drop node under the hull.
            host = new Node3D { Name = "multiplayer1zep", Position = new Vector3(0f, 500f, 0f) };
            var cargobay = new Node3D { Name = "cargobay", Position = new Vector3(0f, -20f, 0f) };
            host.AddChild(cargobay);
            ctx.Host.AddChild(host);
            var resolvedHost = host;
            var resolvedBay = cargobay;

            int freshSpawns = 0;
            gens = new AiGeneratorRuntime(new[] { genDef },
                (name, scope) => name.Equals("multiplayer1zep", System.StringComparison.OrdinalIgnoreCase)
                    ? resolvedHost
                    : name.Equals("cargobay", System.StringComparison.OrdinalIgnoreCase) ? resolvedBay : null,
                nets, ctx.PlaneName,
                (_, _, _, _) => { freshSpawns++; return null; },
                (_, _) => 1, (_, _) => { });
            ctx.Same(1, gens.LiveCount, $"the generator is live");

            int launchWave = 0;
            var releasedAt = new List<Vector3>();
            var releasedVelocities = new List<Vector3>();
            var releasedThrottles = new List<float>();
            var releaseDropDistances = new List<float>();
            FlightController? Release(Vector3 pos, Vector3 lookAt, Vector3 launchVelocity)
            {
                var roster = launchWave == 1 ? wave1 : launchWave == 2 ? wave2 : null;
                foreach (var member in roster ?? new List<FlightController>())
                {
                    if (!member.Inert)
                        continue;
                    member.Activate(pos, lookAt, launchVelocity, carrierDrop: true);
                    releasedAt.Add(pos);
                    releasedVelocities.Add(member.WorldVelocity);
                    releasedThrottles.Add(member.Throttle);
                    releaseDropDistances.Add(pos.DistanceTo(cargobay.GlobalPosition));
                    return member;
                }
                return null;
            }

            ctx.Same(1, gens.UseInstantActionLaunches("multiplayer1zep", Release),
                $"the objective zeppelin's generator takes the Instant Action launch arm");

            // Uncredited: the decoded capacity rule is in force (the capacity-0 stand-in is OFF
            // on this arm), so a full minute of sim above the altitude gate launches nothing.
            const float dt = 1f / 60f;
            var hostVelocity = new Vector3(14f, 3f, -8f);
            void StepGenerators()
            {
                host.Position += hostVelocity * dt;
                gens.SimStep(dt);
            }
            for (int i = 0; i < 60 * 60; i++)
                StepGenerators();
            ctx.Check(releasedAt.Count == 0 && wave1.All(m => m.Inert),
                $"an uncredited generator launches nothing released={releasedAt.Count}");

            // The sequencer's own trigger: a wave whose members are all still in the bay counts
            // as present (the decoded +0x945 rule), so it must NOT read as cleared.
            var waves = new InstantActionWaves(new[] { 6, 3, 0, 0 });
            ctx.Same(1, waves.Start(), $"wave 1 is current at mission start");
            int parked = wave1.Count(m => !m.Crashed);
            ctx.Check(parked == 6 && wave1.Count(m => m.InPlay) == 0,
                $"…with all 6 members parked and none InPlay parked={parked}");
            ctx.Same(0, waves.Step(parked),
                $"a wave still waiting in the bay does not advance the sequencer");

            // The credit: one wave's member count, then the generator's own 7 s composed
            // schedule releases exactly that many and stops.
            launchWave = 1;
            ctx.Same(1, gens.GrantWaveCapacity("multiplayer1zep", wave1.Count),
                $"wave 1's member count is credited to the generator");
            for (int i = 0; i < 60 * 120 && releasedAt.Count < 6; i++)
                StepGenerators();
            ctx.Same(6, releasedAt.Count, $"exactly wave 1's six members are released");
            ctx.Check(wave1.All(m => m.InPlay), $"…and all six are in play");
            ctx.Check(wave2.All(m => m.Inert),
                $"wave 2 is untouched — one wave's credit releases one wave");
            ctx.Check(releaseDropDistances.All(distance => distance < 0.1f),
                $"…at the generator's live cargobay drop point max error={releaseDropDistances.Max():0.###} m");
            var expectedLaunchVelocity = hostVelocity + Vector3.Down * 22.352f;
            ctx.Check(releasedVelocities.All(v => v.DistanceTo(expectedLaunchVelocity) < 0.01f),
                $"…with host velocity minus 22.352 m/s vertically velocity={releasedVelocities[0]}");
            ctx.Check(releasedThrottles.All(throttle => Mathf.IsEqualApprox(throttle, 0.1f)),
                $"…at the decoded 10% carrier-drop throttle throttle={releasedThrottles[0]:0.##}");
            ctx.Same(0, freshSpawns,
                $"the generator never spawned an aircraft of its own on this arm");

            for (int i = 0; i < 60 * 60; i++)
                StepGenerators();
            ctx.Same(6, releasedAt.Count, $"the credit is spent: a further minute releases nothing");

            // The last kill advances, exactly as on the teleport arm.
            foreach (var m in wave1)
                m.DebugForceCrash();
            ctx.Same(2, waves.Step(wave1.Count(m => !m.Crashed)),
                $"wave 1's last kill advances to wave 2");

            // The builder's other arm: a zeppelin it switches off is HELD — still placed at its
            // authored pose, but no longer flown (a merely hidden one would keep flying its net
            // and firing its broadside).
            heldHost = new Node3D { Name = "multiplayer1zep" };
            ctx.Host.AddChild(heldHost);
            var resolvedHeld = heldHost;
            zeps = new ZeppelinRuntime(zepDefs,
                name => name.Equals("multiplayer1zep", System.StringComparison.OrdinalIgnoreCase)
                    ? resolvedHeld : null, nets);
            ctx.Same(1, zeps.LiveCount, $"the zeppelin record is placed");
            ctx.Check(zeps.Hold("multiplayer1zep"), $"…and the builder can hold it");
            ctx.Check(!zeps.Hold("nosuchzep"), $"holding an unknown node reports it, never throws");
            var placedAt = heldHost.GlobalPosition;
            for (int i = 0; i < 60 * 10; i++)
                zeps.SimStep(dt);
            ctx.Check(heldHost.GlobalPosition.DistanceTo(placedAt) < 0.01f,
                $"a held zeppelin stays at its authored pose through 10 s of sim moved={heldHost.GlobalPosition.DistanceTo(placedAt):0.###} m");
        }
        finally
        {
            gens?.Free();
            zeps?.Free();
            host?.Free();
            heldHost?.Free();
            pool?.Free();
            foreach (var m in wave1)
                m.Free();
            foreach (var m in wave2)
                m.Free();
            textures.Dispose();
        }

        // ⚠ Keep this read-only against the shared cached world: registering a pool or leaving a node
        // switched on here is handed to every later C1 suite, measured once as an inflated
        // destructible-census. What it measures is the decoded activation restoring C1/IA1's objective.
        ctx.WithWorld("C1", collision: true, mission: "IA1", world =>
        {
            var runtime = world.Session.Runtime;
            var objective = runtime.FindNodes("multiplayer1zep").FirstOrDefault();
            ctx.Check(objective != null, $"the objective zeppelin's node is in C1/IA1's world");
            if (objective == null)
                return;
            ctx.Check(!objective.IsVisibleInTree(),
                $"C1/IA1's mission script has switched it off at world load (the state F12 inherits)");

            var bagNode = runtime.FindNodes("gasbag1", objective).FirstOrDefault();
            ctx.Check(bagNode != null, $"gasbag1 resolves under the objective's subtree");
            if (bagNode == null)
                return;
            var (shapesTotal, shapesOff) = ShapeStates(bagNode);
            ctx.Check(shapesTotal > 0 && shapesOff == shapesTotal,
                $"…with all {shapesTotal} of its collision shapes switched off with it off={shapesOff}");
            try
            {
                // The decoded builder step: gwNodeSetActive(node, TRUE) on the selected zeppelin.
                objective.Visible = true;
                var (_, stillOff) = ShapeStates(bagNode);
                // ⚠ Not "all 36 back on": the ones that stay off are descendants that are themselves deactivated,
                // the hidden destroyed variants, which is why WorldCollision derives the flag instead of walking a
                // subtree to re-enable it. The measurement is the crossing, not a full count.
                ctx.Check(objective.IsVisibleInTree() && stillOff < shapesTotal,
                    $"the Instant Action activation puts BOTH back: visible again ({objective.IsVisibleInTree()}), {shapesTotal - stillOff} of {shapesTotal} shapes re-enabled (the {stillOff} left off are the hidden destroyed variants)");
            }
            finally
            {
                objective.Visible = false;
                var (_, offAgain) = ShapeStates(bagNode);
                ctx.Check(offAgain == shapesTotal,
                    $"…and the world is left exactly as this suite found it off={offAgain} of {shapesTotal}");
            }
        });
    }

    // How many collision shapes hang anywhere under this node, and how many of those are
    // switched off — the state `Mech3/WorldCollision` derives from its owner's visibility.
    // Recursive, because a world node's shapes hang off its MESH children rather than off the
    // named node itself; a non-recursive count reads 0 of 0 and passes an "all disabled" test
    // vacuously.
    internal static (int Total, int Disabled) ShapeStates(Node node)
    {
        int total = 0, disabled = 0;
        if (node is CollisionShape3D collision)
        {
            total++;
            if (collision.Disabled)
                disabled++;
        }
        foreach (var child in node.GetChildren())
        {
            var (t, d) = ShapeStates(child);
            total += t;
            disabled += d;
        }
        return (total, disabled);
    }

    // The Instant Action mission end: one mission type at a time, each driven to its end through the
    // same signal GameSession subscribes to, plus the lives ledger's two ends on a real
    // FlightController. Inventory: this module's docs/architecture.md entry.
    // ⚠ Pair every win check with a SECOND runtime of another mission type on the same signal that
    // must stay Running. Reporting an objective the mission does not run on is the one mistake this
    // design can make.
    internal static void InstantActionEnd(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C1", "IA1");
        ctx.RequireData(missionZrdr, $"C1/IA1 zrdr");
        ctx.RequireData(ctx.MessagesPath, $"messages.json");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null);
        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        var spawned = new List<FlightController>();
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);
            var spec = SessionSpec.Parse(System.Array.Empty<string>());
            var liveries = new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof"));
            var inputs = new HumanFlightAdapter.Inputs
            {
                PlanesGamez = planesGamez,
                StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
                AiStatsFor = plane => PlaneStats.LoadForAi(ctx.ZrdrPath, plane),
                RigCount = 0,
                PaintRng = new RandomNumberGenerator(),
                ZrdrPath = ctx.ZrdrPath,
                StockLoadouts = StockLoadouts.Load(),
                WeaponDefs = weaponDefs,
                Textures = textures,
                Projectiles = live,
                Shakes = ShakeDefs.Load(ctx.ZrdrPath),
            };
            var spawner = new FlightRoster(spec, liveries, null!, ctx.Host, inputs);
            string enemyNode = InstantAction.PlaneNodeFor("Warhawk")!;

            FlightController SpawnAt(Vector3 pos, int team, bool inert = false)
            {
                var pilot = AiPilot.HoldingCourse(pos, pos + Vector3.Forward);
                var fc = spawner.SpawnAi(new AiSpawn(enemyNode, pos, pos + Vector3.Forward, pilot,
                    Scheme: null, Team: team, Inert: inert));
                spawned.Add(fc);
                return fc;
            }

            // ---- dogfight_ace: the ace's own Downed report ----------------------------------
            var aceMission = new InstantActionRuntime(EndDef(ctx, "ace", "dogfight_ace"));
            var notAceMission = new InstantActionRuntime(EndDef(ctx, "squadron", "dogfight_squadron"));
            var ace = SpawnAt(new Vector3(0f, 500f, 0f), InstantActionRuntime.EnemyTeam);
            ace.Downed += (_, _) =>
            {
                aceMission.ReportObjective(InstantActionObjective.AceDown);
                notAceMission.ReportObjective(InstantActionObjective.AceDown);
            };
            ctx.Check(!aceMission.Ended, $"the ace duel is running before the ace goes down");
            ace.DebugForceCrash();
            ctx.Check(aceMission.Outcome == InstantActionOutcome.Won,
                $"the ace's own Downed report WINS a dogfight_ace mission: {aceMission.Outcome}");
            ctx.Check(!notAceMission.Ended,
                $"…and the same report leaves a dogfight_squadron mission running: {notAceMission.Outcome}");

            // ---- dogfight_squadron: the sequencer's exhausted counter ------------------------
            var squadron = new InstantActionRuntime(EndDef(ctx, "squadron2", "dogfight_squadron"));
            var notSquadron = new InstantActionRuntime(EndDef(ctx, "zeppelin0", "zeppelin_run"));
            var waves = new InstantActionWaves(new[] { 2, 1, 0, 0 });
            var wave1 = new List<FlightController>
            {
                SpawnAt(new Vector3(200f, 500f, 0f), InstantActionRuntime.EnemyTeam),
                SpawnAt(new Vector3(220f, 500f, 0f), InstantActionRuntime.EnemyTeam),
            };
            var wave2 = new List<FlightController>
            {
                SpawnAt(new Vector3(240f, 500f, 0f), InstantActionRuntime.EnemyTeam, inert: true),
            };
            ctx.Same(1, waves.Start(), $"wave 1 is current at mission start");

            // GameSession.StepInstantAction's own loop body, over the real rosters.
            void StepWaves(List<FlightController> roster)
            {
                int next = waves.Step(roster.Count(m => m.InPlay));
                if (next == 2)
                {
                    wave2[0].Activate(new Vector3(2000f, 500f, 0f), new Vector3(2000f, 500f, 100f));
                }
                else if (next == 0 && waves.Finished)
                {
                    squadron.ReportObjective(InstantActionObjective.WavesCleared);
                    notSquadron.ReportObjective(InstantActionObjective.WavesCleared);
                }
            }

            foreach (var m in wave1)
            {
                m.DebugForceCrash();
            }
            StepWaves(wave1);
            ctx.Check(waves.CurrentWave == 2 && wave2[0].InPlay,
                $"wave 1 cleared: wave 2 is current and flying — the mission is NOT over yet");
            ctx.Check(!squadron.Ended, $"…and the squadron mission is still running with a wave left");
            wave2[0].DebugForceCrash();
            StepWaves(wave2);
            ctx.Check(waves.Finished && squadron.Outcome == InstantActionOutcome.Won,
                $"the last wave's last kill WINS a dogfight_squadron mission: {squadron.Outcome}");
            ctx.Check(!notSquadron.Ended,
                $"…and a zeppelin run clearing its waves the same way is NOT won: {notSquadron.Outcome}");

            // The lives ledger's two ends on a real aircraft: the mechanism is FlightController's own
            // crash/respawn path, which an AI-piloted aircraft takes byte for byte, so the probe is a spawned
            // plane rather than a rig. What is under test is the arming and the Spectating pin.
            var lifeLedger = new InstantActionRuntime(EndDef(ctx, "lives3", "dogfight_squadron", lives: 3));
            var probe = SpawnAt(new Vector3(-400f, 500f, 0f), AimAssist.PlayerTeam);
            lifeLedger.RegisterPilot(probe.PlayerIndex);
            probe.AutoRespawnAfter = 3f;
            probe.DebugForceCrash();
            ctx.Check(probe.Crashed && lifeLedger.NotifyPilotDown(probe.PlayerIndex),
                $"3 lives, first death: the ledger says fly again ({lifeLedger.LivesLeft(probe.PlayerIndex)} left)");
            for (int i = 0; i < 300; i++)
            {
                probe.SimStep(1f / 60f);
            }
            ctx.Check(!probe.Crashed,
                $"…and 5 s later the armed 3 s crash cam has respawned it — the able-to-fail control");

            var lastLife = new InstantActionRuntime(EndDef(ctx, "lives1", "dogfight_squadron"));
            lastLife.RegisterPilot(probe.PlayerIndex);
            probe.DebugForceCrash();
            bool fliesAgain = lastLife.NotifyPilotDown(probe.PlayerIndex);
            probe.Spectating = !fliesAgain;
            ctx.Check(!fliesAgain && lastLife.IsSpectating(probe.PlayerIndex),
                $"the default 1 life sends the same pilot straight to spectate on its first death");
            ctx.Check(lastLife.Outcome == InstantActionOutcome.Lost,
                $"…and with no other human alive the mission is LOST: {lastLife.Outcome}");
            for (int i = 0; i < 600; i++)
            {
                probe.SimStep(1f / 60f);
            }
            ctx.Check(probe.Crashed,
                $"…and 10 s later the wreck is still there: Spectating outranks the armed timer");
        }
        finally
        {
            pool?.Free();
            foreach (var fc in spawned)
            {
                fc.Free();
            }
            textures.Dispose();
        }

        // ---- stunt_flying: StuntRace's all-finished path over the authored zones -------------
        ctx.WithWorld("C1", collision: false, mission: "IA1", world =>
        {
            var zones = StuntMission.Load(world.Gamez, missionZrdr, Messages.Load(ctx.MessagesPath));
            ctx.Check(zones != null, $"C1/IA1 ships danger zones for a stunt_flying mission");
            if (zones == null)
            {
                return;
            }
            var stunt = new InstantActionRuntime(EndDef(ctx, "stunt", "stunt_flying"));
            var notStunt = new InstantActionRuntime(EndDef(ctx, "ace2", "dogfight_ace"));
            var race = new StuntRace();
            var second = zones.ForAnotherPlayer();
            race.Add(0, zones, "P1");
            race.Add(1, second, "P2");
            // GameSession.CheckInstantActionZoneSets, over the same predicate it calls: the two
            // pilots' runs, with P2's out-of-lives state under the suite's control.
            bool p2OutOfLives = false;
            void CheckZoneSets()
            {
                var pilots = new[] { (false, zones.AllComplete), (p2OutOfLives, second.AllComplete) };
                if (InstantActionRuntime.ZoneSetsFlown(pilots))
                {
                    stunt.ReportObjective(InstantActionObjective.ZonesFlown);
                    notStunt.ReportObjective(InstantActionObjective.ZonesFlown);
                }
            }

            zones.RunCompleted += CheckZoneSets;
            second.RunCompleted += CheckZoneSets;

            // Decision 10: all-finished, never first past the post.
            zones.DebugCompleteAll();
            ctx.Check(zones.AllComplete && !race.AllFinished && !stunt.Ended,
                $"the FIRST pilot's zone set is flown and the mission runs on ({race.FinishedCount} of 2 in)");
            second.DebugCompleteAll();
            ctx.Check(race.AllFinished && stunt.Outcome == InstantActionOutcome.Won,
                $"the last pilot in WINS a stunt_flying mission: {stunt.Outcome}");
            ctx.Check(!notStunt.Ended,
                $"…and the same report leaves a dogfight_ace mission running: {notStunt.Outcome}");

            // The same field with P2 out of lives instead: a pilot who can never clear another
            // gate must not hold the mission open, which is what StuntRace's own all-finished
            // rule alone would do (AllFinished is still false here).
            var outOfLives = new InstantActionRuntime(EndDef(ctx, "stunt-out", "stunt_flying"));
            var rerun = zones.ForAnotherPlayer();
            var stranded = zones.ForAnotherPlayer();
            rerun.RunCompleted += () =>
            {
                if (InstantActionRuntime.ZoneSetsFlown(new[] { (false, rerun.AllComplete), (true, stranded.AllComplete) }))
                {
                    outOfLives.ReportObjective(InstantActionObjective.ZonesFlown);
                }
            };
            rerun.DebugCompleteAll();
            ctx.Check(!stranded.AllComplete && outOfLives.Outcome == InstantActionOutcome.Won,
                $"the last FLYING pilot's zone set wins it with the other out of lives: {outOfLives.Outcome}");
        });

        // ---- zeppelin_run: the objective's real death ----------------------------------------
        string m04Zrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C1", "M04");
        ctx.RequireData(m04Zrdr, $"C1/M04 zrdr");
        var zepDefs = Zeppelins.Load(m04Zrdr);
        ctx.Check(zepDefs.Count == 1 && zepDefs[0].Node == "piratezep",
            $"C1/M04 authors the one zeppelin this mission is built around count={zepDefs.Count}");
        if (zepDefs.Count != 1)
        {
            return;
        }
        var zepNets = AiNets.Load(SessionPaths.ChapterZrdr(ctx.DataRoot, "C1"));
        ctx.WithWorld("C1", collision: true, mission: "M04", world =>
        {
            var runtime = world.Session.Runtime;
            var host = runtime.FindNodes("piratezep").FirstOrDefault();
            ctx.Check(host != null, $"the piratezep world node resolves");
            if (host == null)
            {
                return;
            }
            ZeppelinRuntime? zeps = null;
            try
            {
                zeps = new ZeppelinRuntime(zepDefs,
                    name => runtime.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null, zepNets);
                zeps.WireDamage(runtime);

                // The node filter GameSession puts on both subscriptions: a signal from THIS
                // world's zeppelin wins a mission whose objective is it, and never one naming
                // another node.
                void Report(string node, params InstantActionRuntime[] missions)
                {
                    foreach (var ia in missions)
                    {
                        if (string.Equals(node, InstantActionRuntime.SelectedZeppelinNode(ia.Def),
                                System.StringComparison.OrdinalIgnoreCase))
                        {
                            ia.ReportObjective(InstantActionObjective.ZeppelinDisabled);
                        }
                    }
                }

                // ---- path 1: the engines, which is what the mode is FOR --------------------
                var engineMission = new InstantActionRuntime(EndDef(ctx, "zep-engines",
                    "zeppelin_run", cargoZeppelin: "piratezep"));
                var otherHull = new InstantActionRuntime(EndDef(ctx, "zep-other", "zeppelin_run",
                    cargoZeppelin: "someotherzep"));
                zeps.ZeppelinEnginesDisabled += n => Report(n, engineMission, otherHull);
                zeps.ZeppelinKilled += n => Report(n, engineMission, otherHull);
                ctx.Check(InstantActionRuntime.SelectedZeppelinNode(engineMission.Def) == "piratezep",
                    $"the mission's objective resolves to the world's zeppelin");

                var engines = zepDefs[0].Engines;
                var motion = zeps.MotionFor("piratezep");
                ctx.Check(motion != null && engines.Count > 0 && motion.AliveEngines == engines.Count,
                    $"all {engines.Count} of piratezep's authored engines start alive: {motion?.AliveEngines}");
                for (int i = 0; i < engines.Count; i++)
                {
                    runtime.DamageAt(runtime.FindNodes(engines[i], host).FirstOrDefault(), 10_000f);
                    zeps.SimStep(1f / 60f);
                    if (i == engines.Count - 2)
                    {
                        // The able-to-fail control on the win below: one engine short is not it.
                        ctx.Check(!engineMission.Ended && motion?.AliveEngines == 1,
                            $"with ONE engine left the mission runs on: alive={motion?.AliveEngines} outcome={engineMission.Outcome}");
                    }
                }
                ctx.Check(motion?.AliveEngines == 0
                    && engineMission.Outcome == InstantActionOutcome.Won,
                    $"the last engine dying WINS a zeppelin_run mission: alive={motion?.AliveEngines} outcome={engineMission.Outcome}");
                ctx.Check(!zeps.IsDead("piratezep"),
                    $"…with the HULL still alive — engines are their own win, not a kill");
                ctx.Check(!otherHull.Ended,
                    $"…and a mission whose objective is another hull is untouched: {otherHull.Outcome}");

                // ---- path 2: the hull, which also wins the mode ----------------------------
                // Subscribed only now, so the engines signal already fired above cannot be what
                // ends it: this runtime sees the gasbag threshold and nothing else.
                var hullMission = new InstantActionRuntime(EndDef(ctx, "zep-hull", "zeppelin_run",
                    cargoZeppelin: "piratezep"));
                zeps.ZeppelinKilled += n => Report(n, hullMission);

                // The decoded survivor threshold does the killing (4 of 6 required): two gasbags
                // down is not enough, the third is.
                runtime.DamageAt(runtime.FindNodes("gasbag1", host).FirstOrDefault(), 10_000f);
                runtime.DamageAt(runtime.FindNodes("gasbag2", host).FirstOrDefault(), 10_000f);
                zeps.SimStep(1f / 60f);
                ctx.Check(!zeps.IsDead("piratezep") && !hullMission.Ended,
                    $"two gasbags down: the hull lives and the mission runs on");
                runtime.DamageAt(runtime.FindNodes("gasbag3", host).FirstOrDefault(), 10_000f);
                zeps.SimStep(1f / 60f);
                ctx.Check(zeps.IsDead("piratezep")
                    && hullMission.Outcome == InstantActionOutcome.Won,
                    $"the objective's real death ALSO wins a zeppelin_run mission: {hullMission.Outcome}");
            }
            finally
            {
                zeps?.Free();
            }
        });
    }

    // A hand-authored `--ia=` file for one end-condition case, written to the
    // scratch folder and read back through the REAL reader — so a change to how
    // `mission_type`/`lives` parse moves this suite too, and no test builds an
    // `InstantActionDef` the CLI could not produce.
    internal static InstantActionDef EndDef(TestContext ctx, string tag, string missionType,
        int? lives = null, string? cargoZeppelin = null)
    {
        string json = $"{{\"mission_type\": \"{missionType}\""
            + (lives is { } n ? $", \"lives\": {n}" : string.Empty)
            + (cargoZeppelin != null ? $", \"cargo_zeppelin\": \"{cargoZeppelin}\"" : string.Empty)
            + "}";
        string name = $"ia-end-{tag}.json";
        ctx.WriteArtifact(name, json);
        return InstantAction.LoadFromJson(Path.Combine(ctx.ScratchDir, name));
    }

    // The wrap-up board's two shot counters (docs/formats/instant-action.md): ProjectilePool is the
    // single choke point for both, so this fires real rounds through the real pool at a real target
    // rather than asserting on the arithmetic in isolation. The board's other two rows are a live read
    // of StuntMission.CompletedCount and a Downed tally already exercised by InstantActionEnd.
    internal static void InstantActionWrapup(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null);
        WeaponDef? cannon = weaponDefs.All.FirstOrDefault(w => w.IsCannon && w.ArmorDamage is > 0f);
        WeaponDef? rocket = weaponDefs.All.FirstOrDefault(w => w.IsRocket);
        ctx.Check(cannon != null && rocket != null,
            $"a CANNON gun and a rocket both exist in the data (cannon={cannon != null} rocket={rocket != null})");
        if (cannon == null || rocket == null)
            return;

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? target = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            var spec = SessionSpec.Parse(System.Array.Empty<string>());
            var liveries = new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof"));
            var inputs = new HumanFlightAdapter.Inputs
            {
                PlanesGamez = planesGamez,
                StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
                AiStatsFor = plane => PlaneStats.LoadForAi(ctx.ZrdrPath, plane),
                RigCount = 0,
                PaintRng = new RandomNumberGenerator(),
                ZrdrPath = ctx.ZrdrPath,
                StockLoadouts = StockLoadouts.Load(),
                WeaponDefs = weaponDefs,
                Textures = textures,
                Projectiles = live,
                Shakes = ShakeDefs.Load(ctx.ZrdrPath),
            };
            var spawner = new FlightRoster(spec, liveries, null!, ctx.Host, inputs);

            var pos = new Vector3(0f, 500f, 0f);
            target = spawner.SpawnAi(new AiSpawn(ctx.PlaneName, pos, pos + Vector3.Forward,
                AiPilot.HoldingCourse(pos, pos + Vector3.Forward),
                Scheme: null, Team: InstantActionRuntime.EnemyTeam));
            ctx.Check(target?.Body != null, $"the target built a collision body");
            if (target?.Body == null)
                return;

            // ScoredShooters: shooter 0 stands in for a registered human seat, 999 for an
            // AI's shooter id, which a mission never adds to the set.
            live.ScoredShooters.Add(0);

            var muzzle = new Transform3D(
                Basis.LookingAt(Vector3.Back, Vector3.Up), pos + new Vector3(0f, 0f, -20f));
            void FireOnce(WeaponDef weapon, int shooterId)
            {
                live.Spawn(weapon, muzzle, Vector3.Zero, shooterId: shooterId);
                for (int i = 0; i < 20; i++)
                    live.SimStep(1f / 60f);
                live.Clear();
            }

            FireOnce(cannon, shooterId: 0);
            ctx.Check(live.CannonRoundsFired == 1 && live.CannonHits == 1,
                $"a scored shooter's cannon round counts as both fired and hit: fired={live.CannonRoundsFired} hits={live.CannonHits}");

            FireOnce(cannon, shooterId: 999);
            ctx.Check(live.CannonRoundsFired == 1 && live.CannonHits == 1,
                $"an unscored shooter's identical shot moves neither counter: fired={live.CannonRoundsFired} hits={live.CannonHits}");

            FireOnce(rocket, shooterId: 0);
            ctx.Check(live.CannonRoundsFired == 1 && live.CannonHits == 1,
                $"a scored shooter's ROCKET is excluded from the cannon-only counters: fired={live.CannonRoundsFired} hits={live.CannonHits}");
        }
        finally
        {
            pool?.Free();
            target?.Free();
            textures.Dispose();
        }
    }

    // The inert state: an aircraft built complete and held out until Activate.
    // ⚠ Compare a live control, inert aircraft, and that aircraft activated with real physics, targeting,
    // shots, and simulation; absence alone can pass for the wrong reason (METHOD-9/METHOD-10).

}
