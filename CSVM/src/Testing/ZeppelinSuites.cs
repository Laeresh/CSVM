using System.Collections.Generic;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>Suites asserting the zeppelins: motion along their nets, fighter launch, multi-zone
/// damage, and the broadside cannons.</summary>
internal static class ZeppelinSuites
{
    internal static void ZeppelinMotionSuite(TestContext ctx)
    {
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, "C1");
        ctx.RequireData(chapterZrdr, $"C1 zrdr");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C1", "M04");
        ctx.RequireData(missionZrdr, $"C1/M04 zrdr");

        var defs = Zeppelins.Load(missionZrdr);
        ctx.Check(defs.Count == 1 && defs[0].Node == "piratezep",
            $"C1/M04 authors one zeppelin, piratezep count={defs.Count}");
        if (defs.Count != 1)
            return;
        var def = defs[0];

        var nets = AiNets.Load(chapterZrdr);
        var net = AiNets.ByName(nets, def.Net);
        ctx.Check(net != null, $"its net '{def.Net}' resolves in C1's neindex");
        if (net == null)
            return;
        int tagged = 0, armed = 0;
        foreach (var n in net.Nodes)
        {
            if (n.StopPointId > 0)
                tagged++;
            if (n.StopsHere)
                armed++;
        }
        ctx.Check(tagged > 0 && armed > 0,
            $"the route carries stop points, armed in the file ids={tagged} armed={armed} of {net.Nodes.Count} nodes");
        ctx.Check(net.Nodes[0].StopPointId == 1 && net.Nodes[0].StopsHere
            && def.Position.DistanceTo(net.Nodes[0].Position) < 5f,
            $"…and the record spawns ON node 0, which is stop point 1 and armed — so C1/M04's PANDORA starts docked, not flying");

        Node3D? host = null;
        Node3D? heldHost = null;
        ZeppelinRuntime? runtime = null;
        ZeppelinRuntime? heldRuntime = null;
        try
        {
            // A mission zeppelin is a world/anim node; the runtime moves the NODE, no flight
            // model, so a bare Node3D is the exact contract (resolver stands in for the world
            // runtime's FindNodes).
            host = new Node3D { Name = "piratezep" };
            ctx.Host.AddChild(host);
            var resolvedHost = host;
            runtime = new ZeppelinRuntime(defs, name =>
                name.Equals("piratezep", System.StringComparison.OrdinalIgnoreCase) ? resolvedHost : null, nets);
            ctx.Same(1, runtime.LiveCount, $"the zeppelin is live");
            ctx.Check(host.GlobalPosition.DistanceTo(def.Position) < 0.1f,
                $"placed at the authored position at load pos={host.GlobalPosition}");

            var motion = runtime.MotionFor("piratezep");
            ctx.Check(motion != null, $"MotionFor finds the live motion (the F18 seam's lookup)");
            if (motion == null)
                return;

            const float dt = 1f / 60f;

            // Docked: the armed stop point under the spawn holds the hull where it was placed,
            // which is the state OBJECTIVE-side COMPLETED_STOPPOINT releases.
            var docked = host.GlobalPosition;
            for (int i = 0; i < 60 * 60; i++)
                runtime.SimStep(dt);
            ctx.Check(motion.Follower.Holding && motion.Speed == 0f
                && docked.DistanceTo(host.GlobalPosition) < 1f,
                $"a minute on an armed stop point moves it {docked.DistanceTo(host.GlobalPosition):0.##} m, speed={motion.Speed:0.##}");
            ctx.Same(0, motion.Follower.Advances, $"…and the walk never left node 0");

            // COMPLETED_STOPPOINT ["PirateZep1", 1, 0]: the mission's own release.
            ctx.Same(1, runtime.SetStopPoint(def.Net, 1, false),
                $"SetStopPoint('{def.Net}', 1, false) writes the one follower flying it");
            ctx.Same(-1, runtime.MotionFor("piratezep")!.Follower.StopPointNode(9),
                $"…and an id no node carries addresses nothing");

            // Fly until three node captures, checking per-step displacement against the
            // record's own speed limit and every hop against the edge list.
            var hops = new List<(int From, int To)>();
            int last = motion.Follower.CurrentIndex;
            int speedViolations = 0;
            var start = host.GlobalPosition;
            int steps = 0, budget = 60 * 900;
            while (motion.Follower.Advances < 3 && steps < budget)
            {
                steps++;
                var before = host.GlobalPosition;
                runtime.SimStep(dt);
                if (before.DistanceTo(host.GlobalPosition) > (def.MaxSpeed * dt) + 0.01f)
                    speedViolations++;
                if (motion.Follower.CurrentIndex != last)
                {
                    if (last >= 0)
                        hops.Add((last, motion.Follower.CurrentIndex));
                    last = motion.Follower.CurrentIndex;
                }
            }
            ctx.Check(motion.Follower.Advances >= 3,
                $"the node captures 3 net nodes advances={motion.Follower.Advances} in {steps / 60f:0} s of sim");
            ctx.Check(start.DistanceTo(host.GlobalPosition) > 100f,
                $"…moving the world node dist={start.DistanceTo(host.GlobalPosition):0} m");
            bool allEdges = hops.Count > 0;
            foreach (var (from, to) in hops)
            {
                if (!net.Edges.Contains((from, to)) && !net.Edges.Contains((to, from)))
                    allEdges = false;
            }
            ctx.Check(allEdges,
                $"every hop is an EDGE of the graph hops={string.Join(" ", hops.ConvertAll(h => $"{h.From}→{h.To}"))}");
            ctx.Same(0, speedViolations, $"no step ever moved farther than max_speed·dt");

            // Total engine loss: the decoded sqrt curve takes max_speed to 0 (accel keeps its
            // 20 % floor), so the zeppelin decelerates to a stop. This is the seam F18 drives.
            motion.AliveEngines = 0;
            for (int i = 0; i < 60 * 60 && motion.Speed > 0f; i++)
                runtime.SimStep(dt);
            ctx.Check(motion.Speed == 0f,
                $"total engine loss decelerates to a stop speed={motion.Speed:0.##}");
            var stopped = host.GlobalPosition;
            runtime.SimStep(dt);
            ctx.Check(stopped.DistanceTo(host.GlobalPosition) < 1e-3f,
                $"…and the node no longer moves");

            // Engines back, and the route's far end holds it for good: node 4 is armed but carries
            // stop-point id 0, the "no stop point" id the script side refuses, so nothing can
            // release it. An open path therefore ENDS at its dock instead of shuttling back.
            motion.AliveEngines = motion.TotalEngines;
            for (int i = 0; i < 60 * 600 && !motion.Follower.Holding; i++)
                runtime.SimStep(dt);
            ctx.Check(motion.Follower.Holding && motion.Follower.CurrentIndex == net.Nodes.Count - 1
                && motion.Speed == 0f,
                $"the far end holds it for good node={motion.Follower.CurrentIndex} id={net.Nodes[^1].StopPointId} speed={motion.Speed:0.##}");
            ctx.Same(-1, motion.Follower.SetStopPoint(0, false),
                $"…and stop-point id 0 addresses no node, so no script can release that dock");

            // A deactivated record is placed at its pose but held (mission script would wake
            // it; out of M4 scope).
            var held = new ZeppelinDef
            {
                Node = "heldzep",
                Position = new Vector3(500f, 640f, -500f),
                YawDeg = 90f,
                MaxSpeed = def.MaxSpeed,
                MaxAccel = def.MaxAccel,
                AccelPitchDeg = def.AccelPitchDeg,
                AccelYawDeg = def.AccelYawDeg,
                MaxRateYawDeg = def.MaxRateYawDeg,
                MaxRatePitchDeg = def.MaxRatePitchDeg,
                MinPitchDeg = def.MinPitchDeg,
                MaxPitchDeg = def.MaxPitchDeg,
                Net = def.Net,
                Targets = System.Array.Empty<string>(),
                Healthy = System.Array.Empty<ZeppelinHealthyZone>(),
                NumHealthyRequired = 1,
                Engines = System.Array.Empty<string>(),
                Gasbags = System.Array.Empty<ZeppelinGasbag>(),
                LeftCannons = System.Array.Empty<ZeppelinCannon>(),
                RightCannons = System.Array.Empty<ZeppelinCannon>(),
                CannonHealth = System.Array.Empty<ZeppelinCannonHealth>(),
                Deactivated = true,
            };
            heldHost = new Node3D { Name = "heldzep" };
            ctx.Host.AddChild(heldHost);
            var resolvedHeld = heldHost;
            heldRuntime = new ZeppelinRuntime(new[] { held }, _ => resolvedHeld, nets);
            for (int i = 0; i < 60; i++)
                heldRuntime.SimStep(dt);
            ctx.Check(heldHost.GlobalPosition.DistanceTo(held.Position) < 1e-3f,
                $"a deactivated record is placed but held pos={heldHost.GlobalPosition}");
        }
        finally
        {
            runtime?.Free();
            heldRuntime?.Free();
            host?.Free();
            heldHost?.Free();
        }
    }

    internal static void ZeppelinLaunch(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, "C1");
        ctx.RequireData(chapterZrdr, $"C1 zrdr");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C1", "IA1");
        ctx.RequireData(missionZrdr, $"C1/IA1 zrdr");

        // The authored generator: the decoded zeppelin-launch shape, doors named explicitly.
        var egen = EnemyGenerators.Load(missionZrdr);
        ctx.Check(egen.Count == 1 && egen[0].IsZeppelin,
            $"C1/IA1 authors one zeppelin-launch generator count={egen.Count}");
        if (egen.Count != 1)
            return;
        var def = egen[0];
        ctx.Check(def.Node == "multiplayer1zep" && def.Origin == "cargobay"
            && def.OpenAnim == "mp1_open_doors" && def.CloseAnim == "mp1_close_doors"
            && def.MinAltitude is 100f,
            $"…on multiplayer1zep: cargobay origin, mp1 door anims, 100 m gate");
        ctx.Check(def.RotationDeg is { } rot && Mathf.Abs(rot.X + 90f) < 0.01f,
            $"the authored drop attitude is pitch −90° rot={def.RotationDeg}");
        ctx.Check(Mathf.Abs(def.IndPeriod + def.WavePeriod - 7f) < 0.01f,
            $"the composed spawn gap is ind 5 + wave 2 = 7 s");

        // What the model ships for doors: the compiled mis_anim carries both OnCall defs,
        // each moving the hull's door_left/door_right nodes (this is the visual F20 wires).
        var (_, missionAnim) = AnimProgram.ArchivePaths(ctx.DataRoot, "C1", "IA1");
        var archive = AnimArchive.Load(missionAnim, "mis_anim");
        ctx.Check(archive != null, $"C1/IA1's compiled mis_anim archive loads");
        if (archive == null)
            return;
        foreach (var animName in new[] { def.OpenAnim!, def.CloseAnim! })
        {
            AnimDefinition? doorDef = null;
            foreach (var d in archive.Defs)
            {
                if (string.Equals(d.AnimName, animName, System.StringComparison.OrdinalIgnoreCase))
                    doorDef = d;
            }
            var movedNodes = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            if (doorDef != null)
                foreach (var seq in doorDef.Sequences)
                    foreach (var ev in seq.Events)
                        if (ev.Data.Str("name") is { Length: > 0 } target)
                            movedNodes.Add(target);
            ctx.Check(doorDef != null && movedNodes.Contains("door_left") && movedNodes.Contains("door_right"),
                $"'{animName}' ships as a compiled OnCall def over the hull's door nodes nodes=[{string.Join(",", movedNodes)}]");
        }

        var nets = AiNets.Load(chapterZrdr);
        var zepDefs = new List<ZeppelinDef>();
        foreach (var z in Zeppelins.Load(missionZrdr))
        {
            if (z.Node.Equals(def.Node, System.StringComparison.OrdinalIgnoreCase))
                zepDefs.Add(z);
        }
        ctx.Check(zepDefs.Count == 1 && zepDefs[0].Position.Y >= 100f,
            $"the host zeppelin record exists and its authored altitude clears the gate y={(zepDefs.Count > 0 ? zepDefs[0].Position.Y : 0):0}");
        if (zepDefs.Count != 1)
            return;

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var textures = new TextureArchive(texturesPath);
        var spawned = new List<FlightController>();
        var spawnPositions = new List<Vector3>();
        var spawnLooks = new List<Vector3>();
        var animPlays = new List<string>();
        Node3D? host = null;
        Node3D? host2 = null;
        AiGeneratorRuntime? gens = null;
        AiGeneratorRuntime? capped = null;
        ZeppelinRuntime? zeps = null;
        try
        {
            // The world stand-in: the host node with the cargobay drop point riding under the
            // hull, exactly the parent/child shape the chapter gamez builds.
            host = new Node3D { Name = "multiplayer1zep" };
            var cargobay = new Node3D { Name = "cargobay", Position = new Vector3(0f, -20f, 0f) };
            host.AddChild(cargobay);
            ctx.Host.AddChild(host);
            var resolvedHost = host;
            var resolvedBay = cargobay;

            FlightController? SpawnPlane(EnemyGeneratorDef generator, Vector3 pos, Vector3 look,
                AiPilot pilot)
            {
                var model = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
                var c = new FlightController
                {
                    PlaneModel = model,
                    Collider = PlaneCollider.Build(model),
                    PlayerIndex = FlightRoster.ShooterIdBase + spawned.Count,
                    IsHumanPiloted = false,
                    Pilot = pilot,
                    UseKeyboard = false,
                    PadDevices = System.Array.Empty<int>(),
                    AllowPause = false,
                };
                c.AddChild(model);
                c.Setup(new FlightModel(stats), null, new CamParams(), pos, look);
                ctx.Host.AddChild(c);
                spawned.Add(c);
                spawnPositions.Add(pos);
                spawnLooks.Add(look);
                return c;
            }

            gens = new AiGeneratorRuntime(new[] { def },
                (name, scope) => name.Equals("multiplayer1zep", System.StringComparison.OrdinalIgnoreCase)
                    ? resolvedHost
                    : name.Equals("cargobay", System.StringComparison.OrdinalIgnoreCase) ? resolvedBay : null,
                nets, ctx.PlaneName, SpawnPlane,
                (name, _) => { animPlays.Add(name); return 1; }, (_, _) => { });
            ctx.Same(1, gens.LiveCount, $"the generator is live (host + IAZep net resolve)");

            // Held below the gate: the unplaced host sits at y −20/0. Ten seconds of sim spawn
            // nothing and never open the door (the blocked branch only ever CLOSES it).
            const float dt = 1f / 60f;
            for (int i = 0; i < 600; i++)
                gens.SimStep(dt);
            ctx.Check(spawned.Count == 0 && animPlays.Count == 0,
                $"held below the 100 m gate: no spawn, door shut spawns={spawned.Count} plays={animPlays.Count}");

            // F17 places and flies the zeppelin; the gate releases. The held spawn is overdue
            // (timer 10 s > the 7 s threshold), so the first unblocked step opens the door and
            // drops the fighter through it in the same tick.
            zeps = new ZeppelinRuntime(zepDefs, name =>
                name.Equals("multiplayer1zep", System.StringComparison.OrdinalIgnoreCase) ? resolvedHost : null, nets);
            ctx.Same(1, zeps.LiveCount, $"the zeppelin is placed and flying (F17)");
            for (int i = 0; i < 120; i++)
                zeps.SimStep(dt);   // two seconds aloft: the hull is moving before any drop
            ctx.Check(host.GlobalPosition.DistanceTo(zepDefs[0].Position) > 1f,
                $"…and has left its authored pose dist={host.GlobalPosition.DistanceTo(zepDefs[0].Position):0.#} m");

            gens.SimStep(dt);
            ctx.Check(spawned.Count == 1, $"the held spawn fires on the first step at altitude");
            ctx.Check(animPlays.Count == 1 && animPlays[0] == "mp1_open_doors",
                $"the door opened with the authored anim plays=[{string.Join(",", animPlays)}]");
            ctx.Check(spawnPositions.Count == 1
                && spawnPositions[0].DistanceTo(cargobay.GlobalPosition) < 0.1f,
                $"the fighter dropped at the origin node's LIVE position (riding the moving hull) pos={spawnPositions[0]} bay={cargobay.GlobalPosition}");
            ctx.Check(spawnLooks.Count == 1 && (spawnLooks[0] - spawnPositions[0]).Normalized().Y < -0.9f,
                $"…in the authored drop attitude (pitch −90°, clamped shy of vertical) dropY={(spawnLooks[0] - spawnPositions[0]).Normalized().Y:0.##}");

            // The fast cycle: the next spawn is 7 s away, never more than 8, so the door stays
            // open through the second drop (no close anim, no second open).
            var firstPos = spawnPositions[0];
            int steps = 0;
            while (spawned.Count < 2 && steps < 60 * 12)
            {
                steps++;
                zeps.SimStep(dt);
                gens.SimStep(dt);
            }
            ctx.Check(spawned.Count == 2, $"the second fighter drops on the 7 s composed schedule t=+{steps / 60f:0.#} s");
            ctx.Check(animPlays.Count == 1,
                $"the hangar stayed open across it (close early only past an 8 s gap) plays=[{string.Join(",", animPlays)}]");
            ctx.Check(spawnPositions.Count == 2 && spawnPositions[1].DistanceTo(firstPos) > 5f,
                $"…again at the live drop point, which has flown on dist={spawnPositions[1].DistanceTo(firstPos):0.#} m");

            // max_active: a capacity-untouched clone capped at 1 live spawn blocks after its
            // first drop (wave_size − spawned + active > max_active) for as long as it lives.
            var cappedDef = new EnemyGeneratorDef
            {
                Node = def.Node,
                VehicleParams = def.VehicleParams,
                Nets = def.Nets,
                Capacity = def.Capacity,
                MaxActive = 1,
                WaveSize = def.WaveSize,
                WavePeriod = def.WavePeriod,
                IndPeriod = def.IndPeriod,
                IsZeppelin = def.IsZeppelin,
                OpenAnim = def.OpenAnim,
                CloseAnim = def.CloseAnim,
                Origin = def.Origin,
                RotationDeg = def.RotationDeg,
                MinAltitude = def.MinAltitude,
            };
            host2 = new Node3D { Name = "multiplayer1zep", Position = new Vector3(0f, 500f, 0f) };
            ctx.Host.AddChild(host2);
            var resolvedHost2 = host2;
            int before = spawned.Count;
            capped = new AiGeneratorRuntime(new[] { cappedDef },
                (name, scope) => name.Equals("multiplayer1zep", System.StringComparison.OrdinalIgnoreCase)
                    ? resolvedHost2 : null,
                nets, ctx.PlaneName, SpawnPlane, (name, _) => 1, (_, _) => { });
            for (int i = 0; i < 60 * 30; i++)
                capped.SimStep(dt);
            ctx.Same(1, spawned.Count - before,
                $"max_active 1 allows exactly one live spawn in 30 s");
        }
        finally
        {
            gens?.Free();
            capped?.Free();
            zeps?.Free();
            foreach (var c in spawned)
                c.Free();
            host?.Free();
            host2?.Free();
            textures.Dispose();
        }
    }

    // The multi-zone damage chain on a real mission zeppelin, in the mission's own world (C1/M04, where
    // piratezep is live; the run's default IA1 switches it off). Real rounds prove the pipeline, then
    // the bulk gasbag kills go through runtime.DamageAt directly, the same sink minus the flight time.
    // ⚠ Aim rounds at the gasbag collider's BUILT pose, captured before ZeppelinRuntime places the
    // node: a moved physics body never re-enters the space queries inside one frame (INSTR-13).
    internal static void ZeppelinDamageSuite(TestContext ctx)
    {
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C1", "M04");
        ctx.RequireData(missionZrdr, $"C1/M04 zrdr");
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        // Data-driven picks: the first flagged weapon (wep_14/wep_28 ship the flag) — a
        // blast-less one preferred so its damage lands on exactly the struck pool — and the
        // first unflagged gun.
        WeaponDef? zepWeapon = weapons.All.FirstOrDefault(w =>
                w.DamagesZeppelin && w.HealthDamage is > 0f && w.ImpactProximity is not > 0f)
            ?? weapons.All.FirstOrDefault(w => w.DamagesZeppelin && w.HealthDamage is > 0f);
        WeaponDef? gun = weapons.All.FirstOrDefault(w =>
            w.IsGun && !w.DamagesZeppelin && w.HealthDamage is > 0f);
        ctx.Check(zepWeapon != null, $"a DAMAGES_ZEPPELIN weapon with HEALTH_DAMAGE ships");
        ctx.Check(gun != null, $"a gun without DAMAGES_ZEPPELIN ships");
        if (zepWeapon == null || gun == null)
            return;

        ctx.WithWorld("C1", collision: true, mission: "M04", world =>
        {
            var runtime = world.Session.Runtime;
            var defs = Zeppelins.Load(missionZrdr);
            ctx.Check(defs.Count == 1 && defs[0].Node == "piratezep",
                $"C1/M04 authors one zeppelin, piratezep count={defs.Count}");
            if (defs.Count != 1)
                return;
            var def = defs[0];
            var host = runtime.FindNodes("piratezep").FirstOrDefault();
            ctx.Check(host != null, $"the piratezep world node resolves in the M04 world");
            if (host == null)
                return;

            // The built pose, BEFORE ZeppelinRuntime moves the node (INSTR-13 — see summary).
            var bagNode = runtime.FindNodes("gasbag1", host).FirstOrDefault();
            var engNode = runtime.FindNodes(def.Engines[0], host).FirstOrDefault();
            ctx.Check(bagNode != null && engNode != null,
                $"gasbag1 and {def.Engines[0]} resolve under the piratezep subtree");
            if (bagNode == null || engNode == null)
                return;
            var builtBagPos = bagNode.GlobalPosition;

            var nets = AiNets.Load(SessionPaths.ChapterZrdr(ctx.DataRoot, "C1"));
            ZeppelinRuntime? zeps = null;
            ProjectilePool? pool = null;
            TextureArchive? textures = null;
            var started = new List<string>();
            try
            {
                zeps = new ZeppelinRuntime(defs,
                    name => runtime.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null, nets);
                zeps.WireDamage(runtime);
                var motion = zeps.MotionFor("piratezep")!;

                // Seeding: the gasbag pool carries the RECORD's 120 hp (its def has HEALTH 0),
                // the engine pool its compiled def's 40 — record where authored, def where not.
                var bagPool = runtime.Destructibles.PoolsOn(bagNode).FirstOrDefault();
                var engPool = runtime.Destructibles.PoolsOn(engNode).FirstOrDefault();
                ctx.Check(bagPool != null && Mathf.IsEqualApprox(bagPool.MaxHealth, 120f),
                    $"gasbag1's pool is seeded from the record hp={bagPool?.MaxHealth ?? -1f} (authored 120)");
                ctx.Check(engPool != null && Mathf.IsEqualApprox(engPool.MaxHealth, 40f),
                    $"{def.Engines[0]}'s pool keeps its compiled def HEALTH hp={engPool?.MaxHealth ?? -1f} (authored 40)");
                ctx.Same(def.Healthy.Count, zeps.SurvivorsOf("piratezep"),
                    $"all {def.Healthy.Count} healthy entries start alive");
                if (bagPool == null || engPool == null)
                    return;

                // A live pool with the gate wired, damage routed exactly as flight wires it.
                int gateRefusals = 0;
                textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, "C1"));
                pool = new ProjectilePool(textures, null, null)
                {
                    DamageSink = runtime.DamageAt,
                };
                pool.WorldDamageGate = (struckNode, weapon) =>
                {
                    bool allowed = zeps.GateWeaponDamage(struckNode, weapon);
                    if (!allowed)
                        gateRefusals++;
                    return allowed;
                };
                ctx.Host.AddChild(pool);

                // The muzzle 80 m from the gasbag's BUILT pose, aimed straight at it.
                var muzzlePos = builtBagPos + new Vector3(0f, 80f, 0f);
                var aim = (builtBagPos - muzzlePos).Normalized();
                var muzzle = new Transform3D(Basis.LookingAt(aim, Vector3.Right), muzzlePos);

                // 1. The unflagged gun: the round strikes the gasbag, the gate refuses the
                // damage, the pool is untouched.
                float before = bagPool.Health;
                pool.Spawn(gun, muzzle, Vector3.Zero);
                for (int i = 0; i < 180 && gateRefusals == 0; i++)
                    pool.SimStep(1f / 60f);
                ctx.Check(gateRefusals > 0,
                    $"the {gun.Id} round STRUCK the gasbag and was refused by the gate refusals={gateRefusals}");
                ctx.Check(Mathf.IsEqualApprox(bagPool.Health, before),
                    $"…and gasbag hp is untouched hp={bagPool.Health:0.##}");

                // 2. The DAMAGES_ZEPPELIN weapon: the same geometry spends real hp.
                pool.Spawn(zepWeapon, muzzle, Vector3.Zero);
                for (int i = 0; i < 180 && Mathf.IsEqualApprox(bagPool.Health, before); i++)
                    pool.SimStep(1f / 60f);
                ctx.Check(bagPool.Health <= before - zepWeapon.HealthDamage!.Value + 0.01f,
                    $"the {zepWeapon.Id} round spends its HEALTH_DAMAGE hp {before:0.##}→{bagPool.Health:0.##}");

                // 3. An engine kill drives the F17 sqrt seam: fewer alive engines, lower cap.
                runtime.OnInstanceStarted = (d, _) => { if (d.AnimName != null) started.Add(d.AnimName); };
                float capBefore = motion.EffectiveMaxSpeed;
                runtime.DamageAt(engNode, engPool.MaxHealth + 1f);
                zeps.SimStep(1f / 60f);
                ctx.Same(motion.TotalEngines - 1, motion.AliveEngines,
                    $"the destroyed engine leaves the alive count");
                ctx.Check(motion.EffectiveMaxSpeed < capBefore,
                    $"…and the sqrt curve lowers the speed cap {capBefore:0.##}→{motion.EffectiveMaxSpeed:0.##} m/s");

                // 4. The survivor threshold, in the engine: required 4 of 6. Two gasbags dead
                // (survivors 4) lives; the third (survivors 3 < 4) kills — the decoded
                // polarity. The design's destroy-count reading would still be alive here.
                runtime.DamageAt(bagNode, 10_000f); // finishes gasbag1
                runtime.DamageAt(runtime.FindNodes("gasbag2", host).FirstOrDefault(), 10_000f);
                zeps.SimStep(1f / 60f);
                ctx.Same(4, zeps.SurvivorsOf("piratezep"), $"two gasbags down leaves 4 survivors");
                ctx.Check(!zeps.IsDead("piratezep"),
                    $"survivors 4 >= required {def.NumHealthyRequired}: alive");
                runtime.DamageAt(runtime.FindNodes("gasbag3", host).FirstOrDefault(), 10_000f);
                zeps.SimStep(1f / 60f);
                ctx.Same(3, zeps.SurvivorsOf("piratezep"), $"the third leaves 3 survivors");
                ctx.Check(zeps.IsDead("piratezep"),
                    $"survivors 3 < required {def.NumHealthyRequired}: the zeppelin DIES (the decoded polarity)");
                ctx.Check(started.Contains("all_pzep_gasbags"),
                    $"the kill plays the authored hull death started=[{string.Join(", ", started)}]");

                // The dead hull stops flying: the node no longer moves.
                var restingPos = host.GlobalPosition;
                zeps.SimStep(1f);
                ctx.Check(restingPos.DistanceTo(host.GlobalPosition) < 1e-3f,
                    $"the dead zeppelin's motion is stopped");
            }
            finally
            {
                runtime.OnInstanceStarted = null;
                pool?.Free();
                zeps?.Free();
                textures?.Dispose();
            }
        });
    }

    // The broadside chain on C1/M04's piratezep in its own mission world: arc-gated deploy, a real
    // volley at a player stand-in, hold-fire-and-retract out of arc, the thinning, scatter on a
    // cannon_inaccuracy clone, and the zeppelin-versus-zeppelin gasbag pick on constructed geometry.
    // The stand-in is repositioned through its flight model each step to hold the tested bearing.
    // ⚠ Assert rounds at the spawn seam, count and direction, never as hits: a moved body never
    // re-enters the one-frame space queries (INSTR-13).
    internal static void ZeppelinBroadsideSuite(TestContext ctx)
    {
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C1", "M04");
        ctx.RequireData(missionZrdr, $"C1/M04 zrdr");
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var wep28 = weapons.Get(ZeppelinRuntime.BroadsideWeaponId);
        ctx.Check(wep28 is { Velocity: not null },
            $"the hardcoded {ZeppelinRuntime.BroadsideWeaponId} resolves in weapons.zrd v={wep28?.Velocity ?? 0f:0}");
        if (wep28 == null)
            return;

        ctx.WithWorld("C1", collision: true, mission: "M04", world =>
        {
            var runtime = world.Session.Runtime;
            var defs = Zeppelins.Load(missionZrdr);
            var def = defs[0];
            var host = runtime.FindNodes("piratezep").FirstOrDefault();
            ctx.Check(defs.Count == 1 && host != null && def.CannonInaccuracyDeg == null,
                $"C1/M04 authors piratezep (no cannon_inaccuracy), and its node resolves");
            if (host == null)
                return;

            var nets = AiNets.Load(SessionPaths.ChapterZrdr(ctx.DataRoot, "C1"));
            var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
            var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
            TextureArchive? textures = null;
            ProjectilePool? pool = null;
            ZeppelinRuntime? zeps = null;
            ZeppelinRuntime? scatterZeps = null;
            ZeppelinRuntime? zvz = null;
            FlightController? player = null;
            Node3D? attackerHost = null;
            Node3D? targetHost = null;
            var started = new List<string>();
            try
            {
                textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, "C1"));
                var live = new ProjectilePool(textures, null, null);
                pool = live;
                ctx.Host.AddChild(live);
                runtime.OnInstanceStarted = (d, _) => { if (d.AnimName != null) started.Add(d.AnimName); };

                zeps = new ZeppelinRuntime(defs,
                    name => runtime.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null, nets);
                zeps.WireDamage(runtime);
                zeps.WireCannons(live, weapons);
                var bs = zeps.BroadsideOf("piratezep");
                ctx.Check(bs != null && bs.Cannons.Count == 12,
                    $"the broadside wires 6+6 cannons count={bs?.Cannons.Count ?? 0}");
                if (bs == null)
                    return;
                ctx.Check(bs.Cannons.All(c => Mathf.IsEqualApprox(c.DeploySeconds, 4f)),
                    $"deploy durations are read from the authored anim defs (4 s run_time) first={bs.Cannons[0].DeploySeconds:0.##}");

                // The player stand-in: a real registered aircraft whose flight-model position
                // the suite steers to hold each phase's bearing on the FLYING hull.
                var fm = new FlightModel(stats);
                var model = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
                player = new FlightController
                {
                    PlaneModel = model,
                    Collider = PlaneCollider.Build(model),
                    PlayerIndex = 0,
                    Projectiles = live,
                    UseKeyboard = false,
                    PadDevices = System.Array.Empty<int>(),
                    AllowPause = false,
                };
                player.AddChild(model);
                player.Setup(fm, null, new CamParams(), host.GlobalPosition + Vector3.Right * 300f,
                    host.GlobalPosition);
                ctx.Host.AddChild(player);

                var motion = zeps.MotionFor("piratezep")!;
                const float dt = 1f / 60f;
                Vector3 PortAbeam() => host.GlobalPosition + ZeppelinBroadside.SideNormal(
                    motion.YawRad, motion.PitchRad, BroadsideSide.Left, bs.RightSign) * 300f;
                void StepAt(System.Func<Vector3> where, int steps, ZeppelinRuntime target)
                {
                    for (int i = 0; i < steps; i++)
                    {
                        fm.Position = where();
                        target.SimStep(dt);
                        live.SimStep(dt);
                    }
                }

                // 1. In the port arc: the port cannons deploy (authored anims), starboard
                // stays stowed, and at 4 s the readied side volleys 6 real rounds.
                StepAt(PortAbeam, 30, zeps);
                ctx.Check(started.Count(a => a.StartsWith("deploy_pzep_lbroad")) == 6
                          && !started.Any(a => a.StartsWith("deploy_pzep_rbroad")),
                    $"the port six deploy, starboard stays stowed anims=[{string.Join(",", started)}]");
                ctx.Same(0, zeps.BroadsideShotsOf("piratezep"),
                    $"mid-deploy nothing fires (stowed cannons deploy INSTEAD of firing)");
                StepAt(PortAbeam, (int)(4.5f / dt), zeps);
                ctx.Same(6, zeps.BroadsideShotsOf("piratezep"),
                    $"the readied port side volleys one round per live cannon");
                // A 30 degree bound: the hull flies on between volley and check and the muzzles sit ~100 m along
                // it. The solve's exactness is pinned engine-free (ZeppelinBroadsideTests); the in-engine claim is
                // only "at the player, out of the port side".
                var volley = zeps.LastVolleyOf("piratezep");
                var portNow = ZeppelinBroadside.SideNormal(
                    motion.YawRad, motion.PitchRad, BroadsideSide.Left, bs.RightSign);
                float worstOff = 0f;
                float worstSide = 1f;
                foreach (var dir in volley)
                {
                    worstOff = Mathf.Max(worstOff, dir.AngleTo(fm.Position - host.GlobalPosition));
                    worstSide = Mathf.Min(worstSide, dir.Normalized().Dot(portNow));
                }
                ctx.Check(volley.Count == 6 && worstOff < 0.52f && worstSide > 0.5f,
                    $"every round leaves lead-solved toward the player, out of the port side dirs={volley.Count} worstOff={Mathf.RadToDeg(worstOff):0.#}° minPortDot={worstSide:0.##}");

                // 2. Out of arc: dead ahead. Fire holds through the 20 s re-fire horizon and
                // the idle window retracts the port cannons.
                started.Clear();
                int shotsBefore = zeps.BroadsideShotsOf("piratezep");
                Vector3 Ahead() => host.GlobalPosition + motion.Forward * 300f;
                StepAt(Ahead, (int)(25f / dt), zeps);
                ctx.Same(shotsBefore, zeps.BroadsideShotsOf("piratezep"),
                    $"out of both arcs the broadside holds fire for 25 s");
                ctx.Check(started.Count(a => a.StartsWith("retract_pzep_lbroad")) == 6,
                    $"the idle port cannons retract (invented {ZeppelinBroadside.StowAfterIdleSeconds:0} s window) anims=[{string.Join(",", started.Where(a => a.Contains("retract")))}]");

                // 3. F18 thinning: destroy one port cannon (its compiled def pool, HEALTH 60),
                // return to the port arc — the redeployed volley is 5, not 6.
                var lbroadNode = runtime.FindNodes(def.LeftCannons[0].Node, host).FirstOrDefault();
                var lbroadPool = lbroadNode == null ? null : runtime.Destructibles.PoolsOn(lbroadNode).FirstOrDefault();
                ctx.Check(lbroadPool != null,
                    $"'{def.LeftCannons[0].Node}' carries its compiled def pool hp={lbroadPool?.MaxHealth ?? 0f:0}");
                if (lbroadPool == null)
                    return;
                runtime.DamageAt(lbroadNode, lbroadPool.MaxHealth + 1f);
                shotsBefore = zeps.BroadsideShotsOf("piratezep");
                int guard = 0;
                while (zeps.BroadsideShotsOf("piratezep") == shotsBefore && guard++ < (int)(30f / dt))
                {
                    fm.Position = PortAbeam();
                    zeps.SimStep(dt);
                    live.SimStep(dt);
                }
                ctx.Same(5, zeps.BroadsideShotsOf("piratezep") - shotsBefore,
                    $"the destroyed cannon drops out — the volley thins to 5 after {guard * dt:0.#} s");

                // 4. Scatter: a clone authoring cannon_inaccuracy 10° (the C2B/M04 value) on
                // the same hull spreads a volley the exact solve would collapse to a point.
                var scatterDef = CloneWithInaccuracy(def, 10f);
                scatterZeps = new ZeppelinRuntime(new[] { scatterDef },
                    name => runtime.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null, nets);
                scatterZeps.WireCannons(live, weapons);
                var sMotion = scatterZeps.MotionFor("piratezep")!;
                var sBs = scatterZeps.BroadsideOf("piratezep")!;
                Vector3 SPort() => host.GlobalPosition + ZeppelinBroadside.SideNormal(
                    sMotion.YawRad, sMotion.PitchRad, BroadsideSide.Left, sBs.RightSign) * 300f;
                StepAt(SPort, (int)(5f / dt), scatterZeps);
                var scattered = scatterZeps.LastVolleyOf("piratezep");
                float maxPair = 0f;
                for (int i = 0; i < scattered.Count; i++)
                    for (int j = i + 1; j < scattered.Count; j++)
                        maxPair = Mathf.Max(maxPair, scattered[i].AngleTo(scattered[j]));
                ctx.Check(scattered.Count == 6 && maxPair > Mathf.DegToRad(1f),
                    $"cannon_inaccuracy 10° spreads the volley max pair angle {Mathf.RadToDeg(maxPair):0.##}° over {scattered.Count} rounds");

                // 5. The zeppelin-vs-zeppelin arm, constructed geometry (see summary): a near-
                // static attacker whose target zeppelin is deactivated abeam, one gasbag
                // placed outside the 0.707 arc — every pick lands on an IN-ARC bag.
                attackerHost = new Node3D { Name = "attackzep" };
                var cb1 = new Node3D { Name = "cb1", Position = new Vector3(20f, 0f, -30f) };
                var cb2 = new Node3D { Name = "cb2", Position = new Vector3(20f, 0f, 30f) };
                attackerHost.AddChild(cb1);
                attackerHost.AddChild(cb2);
                targetHost = new Node3D { Name = "targetzep" };
                var g1 = new Node3D { Name = "g1", Position = new Vector3(0f, 0f, -40f) };
                var g2 = new Node3D { Name = "g2", Position = new Vector3(0f, 0f, 40f) };
                var g3 = new Node3D { Name = "g3", Position = new Vector3(-290f, 0f, -290f) };
                targetHost.AddChild(g1);
                targetHost.AddChild(g2);
                targetHost.AddChild(g3);
                ctx.Host.AddChild(attackerHost);
                ctx.Host.AddChild(targetHost);
                var attacker = SyntheticZep("attackzep", new Vector3(0f, 4000f, 0f), nets[0].Name,
                    targets: new[] { "targetzep" });
                var target = SyntheticZep("targetzep", new Vector3(300f, 4000f, 0f), nets[0].Name,
                    deactivated: true,
                    healthy: new[] { "g1", "g2", "g3" });
                var resolvedAtt = attackerHost;
                var resolvedTgt = targetHost;
                zvz = new ZeppelinRuntime(new[] { attacker, target }, name =>
                    name == "attackzep" ? resolvedAtt : name == "targetzep" ? resolvedTgt : null, nets);
                zvz.WireCannons(live, weapons);
                for (int i = 0; i < (int)(6f / dt) && zvz.BroadsideShotsOf("attackzep") == 0; i++)
                {
                    zvz.SimStep(dt);
                    live.SimStep(dt);
                }
                var picks = zvz.LastVolleyOf("attackzep");
                ctx.Same(2, picks.Count, $"both starboard cannons fire at the target zeppelin");
                bool onlyInArc = picks.Count > 0 && picks.All(dir =>
                {
                    var fromCb = dir.Normalized();
                    float toG1 = fromCb.AngleTo(g1.GlobalPosition - attackerHost.GlobalPosition);
                    float toG2 = fromCb.AngleTo(g2.GlobalPosition - attackerHost.GlobalPosition);
                    float toG3 = fromCb.AngleTo(g3.GlobalPosition - attackerHost.GlobalPosition);
                    return Mathf.Min(toG1, toG2) < toG3;
                });
                ctx.Check(onlyInArc,
                    $"every rand()-picked aim point is an IN-ARC gasbag, never the out-of-arc g3");
            }
            finally
            {
                runtime.OnInstanceStarted = null;
                pool?.Free();
                zeps?.Free();
                scatterZeps?.Free();
                zvz?.Free();
                player?.Free();
                attackerHost?.Free();
                targetHost?.Free();
                textures?.Dispose();
            }
        });
    }

    // A copy of a shipped record with `cannon_inaccuracy` authored — the scatter
    // phase's instrument (no C1 record authors one; C2B/M04's 10° is the shipped value).
    internal static ZeppelinDef CloneWithInaccuracy(ZeppelinDef def, float inaccuracyDeg) => new()
    {
        Node = def.Node,
        Position = def.Position,
        YawDeg = def.YawDeg,
        PitchDeg = def.PitchDeg,
        MaxSpeed = def.MaxSpeed,
        MaxAccel = def.MaxAccel,
        AccelPitchDeg = def.AccelPitchDeg,
        AccelYawDeg = def.AccelYawDeg,
        MaxRateYawDeg = def.MaxRateYawDeg,
        MaxRatePitchDeg = def.MaxRatePitchDeg,
        MinPitchDeg = def.MinPitchDeg,
        MaxPitchDeg = def.MaxPitchDeg,
        Net = def.Net,
        Targets = def.Targets,
        Healthy = def.Healthy,
        NumHealthyRequired = def.NumHealthyRequired,
        Engines = def.Engines,
        Gasbags = def.Gasbags,
        CannonFireDelay = def.CannonFireDelay,
        CannonFireRange = def.CannonFireRange,
        LeftCannons = def.LeftCannons,
        RightCannons = def.RightCannons,
        CannonHealth = def.CannonHealth,
        CannonInaccuracyDeg = inaccuracyDeg,
    };

    // A minimal constructed zeppelin record for the zeppelin-vs-zeppelin phase:
    // near-static (rates/speed floored) so the constructed bearings hold while cannons
    // deploy on the fallback timing.
    internal static ZeppelinDef SyntheticZep(string node, Vector3 pos, string net,
        string[]? targets = null, string[]? healthy = null, bool deactivated = false)
    {
        var zones = new List<ZeppelinHealthyZone>();
        foreach (var h in healthy ?? System.Array.Empty<string>())
            zones.Add(new ZeppelinHealthyZone(h, "panels"));
        return new ZeppelinDef
        {
            Node = node,
            Position = pos,
            YawDeg = 0f,
            MaxSpeed = 0.1f,
            MaxAccel = 4.47f,
            AccelYawDeg = 0.1f,
            AccelPitchDeg = 0.1f,
            MaxRateYawDeg = 0.1f,
            MaxRatePitchDeg = 0.1f,
            MinPitchDeg = -30f,
            MaxPitchDeg = 30f,
            Net = net,
            Targets = targets ?? System.Array.Empty<string>(),
            Healthy = zones,
            NumHealthyRequired = 1,
            Engines = System.Array.Empty<string>(),
            Gasbags = System.Array.Empty<ZeppelinGasbag>(),
            CannonHealth = System.Array.Empty<ZeppelinCannonHealth>(),
            LeftCannons = System.Array.Empty<ZeppelinCannon>(),
            RightCannons = targets == null
                ? (IReadOnlyList<ZeppelinCannon>)System.Array.Empty<ZeppelinCannon>()
                : new[]
                {
                    new ZeppelinCannon("cb1", "deploy_cb1", "retract_cb1"),
                    new ZeppelinCannon("cb2", "deploy_cb2", "retract_cb2"),
                },
            CannonFireDelay = 20f,
            CannonFireRange = 500f,
            Deactivated = deactivated,
        };
    }
}
