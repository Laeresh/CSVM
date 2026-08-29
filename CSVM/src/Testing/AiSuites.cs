using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>Suites asserting how a computer-controlled combatant behaves: pilots, mounted gunners,
/// combat voice, and the inert state they wait in.</summary>
internal static class AiSuites
{
    internal static void FlightRosterTransaction(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        ctx.RequireData(chapterZrdr, $"C1 zrdr");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        var pool = new ProjectilePool(textures, null, null);
        FlightController? spawned = null;
        SubViewport? pane = null;
        ctx.Host.AddChild(pool);
        try
        {
            var spec = SessionSpec.Parse(System.Array.Empty<string>());
            bool failSecondHuman = true;
            int camParamsCalls = 0;
            var resources = new AircraftAssemblyResources
            {
                PlanesGamez = planesGamez,
                StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
                AiStatsFor = (plane, aiDef) => PlaneStats.LoadForAi(ctx.ZrdrPath, plane, aiDef),
                CamParamsFor = _ => failSecondHuman && ++camParamsCalls == 2
                    ? throw new System.InvalidOperationException("forced second human failure")
                    : new CamParams(),
                PaintRng = new RandomNumberGenerator(),
                ZrdrPath = ctx.ZrdrPath,
                StockLoadouts = StockLoadouts.Load(),
                WeaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null),
                WeaponMessages = Messages.Load(ctx.MessagesPath),
                Textures = textures,
                Shakes = ShakeDefs.Load(ctx.ZrdrPath),
            };
            pane = new SubViewport();
            ctx.Host.AddChild(pane);
            var rigs = new[]
            {
                new PlayerRig { Index = 0, Camera = ctx.Camera, HudParent = pane, Viewport = pane },
                new PlayerRig { Index = 1, Camera = ctx.Camera, HudParent = pane, Viewport = pane },
            };
            var pauseState = new PauseState();
            System.Action<List<AimCandidate>> targetSource = _ => { };
            var humanRoster = new FlightRoster(FlightRosterPolicy.From(spec),
                new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof")),
                new WorldEffectsFactory(spec, ctx.Host, () => Vector3.Zero), ctx.Host, resources,
                new FlightWorldBindings
                {
                    Projectiles = pool,
                    Gamez = planesGamez,
                    ChapterZrdrPath = chapterZrdr,
                },
                new HumanRosterBindings
                {
                    RigCount = 2,
                    Rigs = rigs,
                    PauseState = pauseState,
                    MenuInputFor = _ => new UI.MenuInput(),
                    ExitSession = () => { },
                }, new FixedFlightStarts());
            humanRoster.SetTargetSubParts(targetSource);
            int humanChildrenBefore = ctx.Host.GetChildCount();
            bool humanThrew = false;
            try
            {
                humanRoster.BuildPlayers(rigs);
            }
            catch (System.InvalidOperationException)
            {
                humanThrew = true;
            }
            ctx.Check(humanThrew, $"a late failure in the second human reports its failure");
            ctx.Same(0, humanRoster.Humans.Count, $"failed human batch commits no members");
            ctx.Same(humanChildrenBefore, ctx.Host.GetChildCount(),
                $"failed human batch removes every world-root child it created");
            ctx.Same(0, pane.GetChildCount(),
                $"failed human batch removes its external HUD canvas");
            ctx.Same(0, pool.NearMissTargets.Count,
                $"failed human batch removes its projectile near-miss registration");
            ctx.Check(pool.RigOfShooter(0) == null,
                $"failed human batch removes its projectile shooter registration");
            ctx.Check(rigs.All(rig => rig.Controller == null),
                $"failed human batch leaves every adopted slot unbound");

            failSecondHuman = false;
            humanRoster.BuildPlayers(rigs);
            ctx.Same(2, humanRoster.Humans.Count,
                $"successful human batch commits the complete field");
            ctx.Check(ReferenceEquals(humanRoster.Humans[0], rigs[0])
                      && ReferenceEquals(humanRoster.Humans[1], rigs[1])
                      && rigs[0].Controller?.PlayerIndex == 0
                      && rigs[1].Controller?.PlayerIndex == 1,
                $"successful human batch preserves player identity and order");
            ctx.Check(rigs.All(rig => rig.Controller?.PauseState == pauseState
                                      && rig.Controller.TargetSubParts == targetSource
                                      && rig.Controller.SmokeScreens == null),
                $"finished humans publish pause and target bindings while optional smoke stays absent");
            var humanControllers = rigs.Select(rig => rig.Controller!).ToArray();
            humanRoster.ClearMembership();
            foreach (var controller in humanControllers)
                controller.Free();

            bool failAiCommit = true;
            var roster = new FlightRoster(FlightRosterPolicy.From(spec),
                new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof")),
                null, ctx.Host, resources,
                new FlightWorldBindings { Projectiles = pool, Gamez = planesGamez },
                new HumanRosterBindings(), aiAssemblyFault: () =>
                {
                    if (failAiCommit)
                        throw new System.InvalidOperationException("forced late AI assembly failure");
                });
            int childrenBefore = ctx.Host.GetChildCount();
            ulong paintState = resources.PaintRng.State;
            ulong aiRngState = Utils.Rng.Stream(Utils.Rng.Ai).State;
            ulong spawnRngState = Utils.Rng.Stream(Utils.Rng.Spawn).State;
            var at = new Vector3(0f, 500f, 0f);
            var pilot = AiPilot.HoldingCourse(at, at + Vector3.Forward);
            var failedSpawn = new AiSpawn(ctx.PlaneName, at, at + Vector3.Forward, pilot);
            bool threw = false;
            try
            {
                roster.SpawnAi(failedSpawn);
            }
            catch (System.InvalidOperationException)
            {
                threw = true;
            }
            ctx.Check(threw, $"a failed AI assembly reports its failure");
            ctx.Same(0, roster.AiAircraft.Count, $"failed assembly commits no roster member");
            ctx.Same(childrenBefore, ctx.Host.GetChildCount(),
                $"failed assembly leaves no world-root child");
            ctx.Check(pilot.Gunner == null && pilot.Rocketeer == null && pilot.Machine == null,
                $"failed assembly restores the caller's pilot state");
            ctx.Check(resources.PaintRng.State == paintState
                      && Utils.Rng.Stream(Utils.Rng.Ai).State == aiRngState
                      && Utils.Rng.Stream(Utils.Rng.Spawn).State == spawnRngState,
                $"failed assembly restores the paint, AI, and spawn streams");

            failAiCommit = false;
            var position = new Vector3(0f, 500f, -100f);
            spawned = roster.SpawnAi(new AiSpawn(ctx.PlaneName, position,
                position + Vector3.Forward,
                AiPilot.HoldingCourse(position, position + Vector3.Forward)));
            ctx.Same(1, roster.AiAircraft.Count, $"successful assembly commits one member");
            ctx.Check(ReferenceEquals(spawned, roster.AiAircraft[0]),
                $"the roster exposes the committed controller");
            ctx.Check(spawned.PlayerIndex == FlightRoster.ShooterIdBase
                      && spawned.Name == $"ai1_{ctx.PlaneName}",
                $"the failed attempt consumed neither shooter id nor roster name");
            roster.ClearMembership();
            ctx.Same(0, roster.AiAircraft.Count, $"teardown clears roster membership");
        }
        finally
        {
            spawned?.Free();
            pane?.Free();
            pool.Free();
            textures.Dispose();
        }
    }

    internal static void InertAircraft(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null);
        WeaponDef? gun = weaponDefs.All.FirstOrDefault(w => w.IsGun && w.ArmorDamage is > 0f);
        ctx.Check(gun != null, $"a gun with ARMOR_DAMAGE exists in the data");
        if (gun == null)
            return;

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? control = null;
        FlightController? subject = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            var spec = SessionSpec.Parse(System.Array.Empty<string>());
            var liveries = new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof"));
            var inputs = new AircraftAssemblyResources
            {
                PlanesGamez = planesGamez,
                StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
                AiStatsFor = (plane, aiDef) => PlaneStats.LoadForAi(ctx.ZrdrPath, plane, aiDef),
                PaintRng = new RandomNumberGenerator(),
                ZrdrPath = ctx.ZrdrPath,
                StockLoadouts = StockLoadouts.Load(),
                WeaponDefs = weaponDefs,
                Textures = textures,
                Shakes = ShakeDefs.Load(ctx.ZrdrPath),
            };
            // worldEffects null!: never dereferenced — CrashProgram/WorldScene stay null, so the
            // spawner's crash-runtime block (its only reader) is skipped.
            var spawner = new FlightRoster(FlightRosterPolicy.From(spec), liveries, null!, ctx.Host, inputs, new FlightWorldBindings { Projectiles = live, Gamez = planesGamez }, new HumanRosterBindings());

            // One scan origin, the control dead ahead on −Z, the subject 90° off it on +X, so a
            // scan aimed at either sits well outside the other's acceptance cone.
            var origin = new Vector3(0f, 500f, 0f);
            var controlPos = origin + new Vector3(0f, 0f, -300f);
            var subjectPos = origin + new Vector3(300f, 0f, 0f);
            control = spawner.SpawnAi(new AiSpawn(ctx.PlaneName, controlPos, controlPos + Vector3.Forward,
                AiPilot.HoldingCourse(controlPos, controlPos + Vector3.Forward),
                Scheme: null, Team: InstantActionRuntime.EnemyTeam));
            subject = spawner.SpawnAi(new AiSpawn(ctx.PlaneName, subjectPos, subjectPos + Vector3.Forward,
                AiPilot.HoldingCourse(subjectPos, subjectPos + Vector3.Forward),
                Scheme: null, Team: InstantActionRuntime.EnemyTeam, Inert: true));
            ctx.Check(control.Body != null && subject.Body != null && control.Damage != null
                      && subject.Damage != null,
                $"both aircraft built a collision body and per-part damage");
            if (control.Body == null || subject.Body == null
                || control.Damage == null || subject.Damage == null)
                return;
            ctx.Check(control.InPlay && !subject.InPlay,
                $"the control is in play and the inert one is not: control={control.InPlay} subject={subject.InPlay}");

            // --- the four instruments. Each takes the aircraft it is measuring and reads its LIVE
            // position, so a subject that has moved (activation re-homes it) is still measured
            // where it actually is.
            var space = live.GetWorld3D().DirectSpaceState;
            bool RayFinds(FlightController rig)
            {
                var at = rig.WorldPosition;
                var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                    at + new Vector3(0f, 0f, -30f), at, CollisionLayers.WorldAndAircraft));
                return hit.Count > 0 && ReferenceEquals(hit["collider"].Obj, rig.Body);
            }

            var candidates = new AimCandidateSet();
            bool ScanFinds(FlightController rig)
            {
                candidates.Clear();
                live.CollectAircraft(candidates);
                var scan = new AimScan
                {
                    MuzzlePosition = origin,
                    Forward = (rig.WorldPosition - origin).Normalized(),
                    Team = AimAssist.PlayerTeam,   // hostile to both aircraft (team 2)
                    Speed = 500f,
                    RangeSquared = 2000f * 2000f,
                    ConeCos = Mathf.Cos(Mathf.DegToRad(10f)),
                };
                return AimAssist.Scan(scan, candidates, out var result)
                       && ReferenceEquals(result.Source, rig);
            }

            // The whole-vehicle pair, not a sum over zones: these aircraft come off FlightRoster and an
            // AI airframe is zone-less, so a parts sum reads a flat zero and no round could ever bite. It is
            // what the decoded death test reads either way.
            float Combined(FlightController rig) => rig.Damage!.WholeArmor + rig.Damage.WholeHealth;
            bool RoundBites(FlightController rig)
            {
                float before = Combined(rig);
                var at = rig.WorldPosition;
                var muzzle = new Transform3D(
                    Basis.LookingAt(Vector3.Back, Vector3.Up), at + new Vector3(0f, 0f, -20f));
                for (int tries = 0; tries < 6 && Combined(rig) >= before; tries++)
                {
                    live.Spawn(gun, muzzle, Vector3.Zero, shooterId: 0);
                    for (int i = 0; i < 20; i++)
                        live.SimStep(1f / 60f);
                    live.Clear();
                }
                return Combined(rig) < before;
            }

            bool StepMoves(FlightController rig)
            {
                var before = rig.WorldPosition;
                for (int i = 0; i < 10; i++)
                    rig.SimStep(1f / 60f);
                return rig.WorldPosition.DistanceTo(before) > 1f;
            }

            // --- the able-to-fail baseline: the live control answers YES to all four, so a NO
            // below is the inert flag and not a broken instrument.
            ctx.Check(RayFinds(control), $"baseline: a raycast returns the live control's body");
            ctx.Check(ScanFinds(control), $"baseline: an aim-assist scan returns the live control");
            ctx.Check(RoundBites(control), $"baseline: a round fired through the live control costs it HP");
            ctx.Check(StepMoves(control), $"baseline: a sim step moves the live control");
            ctx.Check(control.PlaneModel!.Visible, $"baseline: the live control is drawn");

            // --- the inert aircraft: the same four instruments, same session, all NO.
            ctx.Check(!RayFinds(subject), $"a raycast does not return the inert aircraft");
            ctx.Check(!ScanFinds(subject), $"an aim-assist scan does not return the inert aircraft");
            float inertBefore = Combined(subject);
            ctx.Check(!RoundBites(subject), $"a round fired through the inert aircraft passes through it");
            ctx.Check(Mathf.IsEqualApprox(Combined(subject), inertBefore),
                $"…and its damage pools are untouched: {Combined(subject):0.##} vs {inertBefore:0.##}");
            ctx.Check(!StepMoves(subject), $"a sim step does not move the inert aircraft");
            ctx.Check(!subject.PlaneModel!.IsVisibleInTree(), $"the inert aircraft is not drawn");

            // Trap: inert is NOT "left off the pool's roster". The plane reached RegisterAircraft like any
            // other, so the fuse and blast passes that walk that roster can see it, which is why InPlay is read
            // there; it is listed as a candidate and simply not live.
            candidates.Clear();
            live.CollectAircraft(candidates);
            var listed = candidates.Vehicles.FirstOrDefault(c => ReferenceEquals(c.Source, subject));
            ctx.Check(listed.Source != null && !listed.Live,
                $"the inert aircraft is on the pool's candidate roster but not live: listed={listed.Source != null} live={listed.Live}");

            // ⚠ Activate AT ITS BUILD POSE. A suite completes inside one _Ready and never yields a frame, so
            // a body moved here keeps its build-pose transform on the physics server and no raycast can find
            // it (INSTR-13); the re-home is asserted off the flight model instead.
            subject.Activate(subjectPos, subjectPos + Vector3.Forward);
            ctx.Check(subject.InPlay && !subject.Inert, $"Activate cleared the inert flag");
            ctx.Check(RayFinds(subject), $"the activated aircraft is returned by a raycast");
            ctx.Check(ScanFinds(subject), $"the activated aircraft is returned by an aim-assist scan");
            ctx.Check(RoundBites(subject), $"a round fired through the activated aircraft costs it HP");
            ctx.Check(StepMoves(subject), $"a sim step moves the activated aircraft");
            // In the tree, not the model's own bit: presence is written on the pivot above it, and
            // the model's bit is the airframe's archive ACTIVE state (FlightController.ApplyPresence).
            ctx.Check(subject.PlaneModel.IsVisibleInTree(), $"the activated aircraft is drawn");

            // Activation's other half: it re-homes the aircraft at the pose it is given, which is
            // how E11's wave teleport will arrive. Read off the flight model (WorldPosition is the
            // sim value) rather than the node, for the frame-flush reason above.
            var elsewhere = subjectPos + new Vector3(0f, 0f, -900f);
            subject.Activate(elsewhere, elsewhere + Vector3.Forward);
            ctx.Check(subject.WorldPosition.DistanceTo(elsewhere) < 1f,
                $"Activate re-homes the aircraft at the pose it is given pos={subject.WorldPosition}");
            float pristine = subject.Damage.WholeArmorMax + subject.Damage.WholeHealthMax;
            ctx.Check(Mathf.IsEqualApprox(Combined(subject), pristine),
                $"…with a repaired airframe, the respawn it rides on top of: {Combined(subject):0.##}/{pristine:0.##}");
        }
        finally
        {
            pool?.Free();
            control?.Free();
            subject?.Free();
            textures.Dispose();
        }
    }

    internal static void CarriedTurrets(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var turretDefs = TurretDefs.Load(ctx.ZrdrPath);
        ctx.Check(turretDefs.All.Count(d => d.Carried) == 16,
            $"the 16 carried ai.zrd entries parse carried={turretDefs.All.Count(d => d.Carried)}");

        // The Kestrel: a single rear turret (thirdp MSG_TUR_PAC_G3 — PITCH [20,50], YAW
        // [105,255], the directed rear arc through 180°, DETECTION_RANGE 450, FIRE_RATE 0.4,
        // ATTACK 4 s / BORED 3 s scalars).
        const string HostPlane = "player_kestrel";
        var stats = PlaneStats.Load(ctx.ZrdrPath, HostPlane);
        ctx.Check(stats.TurretMounts.Count(m => !m.FirstPerson) == 1,
            $"{HostPlane} carries one thirdp turret mount");

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? host = null;
        FlightController? target = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            FlightController BuildRig(string plane, PlaneStats st, int playerIndex, Vector3 pos)
            {
                var model = new PlaneBuilder(planesGamez, textures).Build(plane);
                var rig = new FlightController
                {
                    PlaneModel = model,
                    Collider = PlaneCollider.Build(model),
                    Damage = new PlaneDamage(st.DestroyableParts),
                    PlayerIndex = playerIndex,
                    Projectiles = live,
                    UseKeyboard = false,
                    AllowPause = false,
                };
                rig.AddChild(model);
                // Nose on world -Z (identity attitude): plane-local == world - pos.
                rig.Setup(new FlightModel(st), ctx.Camera, new CamParams(), pos, pos + Vector3.Forward);
                ctx.Host.AddChild(rig);
                // Park at zero speed: Setup leaves the model at spawn speed, and a rig this
                // suite never steps would otherwise REPORT that velocity while standing still —
                // the turret then leads a phantom motion and every round misses.
                rig.PlaceHeld(pos, pos + Vector3.Forward);
                return rig;
            }

            var hostPos = new Vector3(0f, 500f, 0f);
            host = BuildRig(HostPlane, stats, 0, hostPos);
            host.Turrets = TurretController.BuildCarried(
                turretDefs, stats, host.PlaneModel!, weapons, host, live);
            ctx.Check(host.Turrets.Length == 1,
                $"the mount resolves against the built model turrets={host.Turrets.Length}");
            if (host.Turrets.Length != 1)
                return;
            var turret = host.Turrets[0];
            ctx.Check(turret.YawNode != null && turret.Firepoints.Length == 1,
                $"the PARTS chain resolved yaw={turret.YawNode?.Name} muzzles={turret.Firepoints.Length}");
            ctx.Check(turret.Weapon.Id == turret.Def.WeaponName,
                $"WEAPON.NAME resolved as a ballistics id ({turret.Weapon.Id})");

            // Load pose: the centre of each arc — yaw 180 (rearward), pitch 35.
            var (restYaw, restPitch) = TurretController.AnglesOfLocal(turret.BarrelLocal);
            ctx.Check(Mathf.Abs(Mathf.Wrap(restYaw - 180f, -180f, 180f)) < 0.5f
                      && Mathf.Abs(restPitch - 35f) < 0.5f,
                $"the turret poses at its arc centre yaw={restYaw:0.#} pitch={restPitch:0.#}");

            // The target: in-arc (behind and above the host — yaw ~180, elevation ~35°), inside
            // DETECTION_RANGE, on a hostile team. INACCURACY is zeroed so every gated round flies
            // the solved line — the scatter cone itself is covered by the aim-assist suite.
            turret.Def.InaccuracyDeg = 0f;
            var targetStats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
            var targetPos = hostPos + new Vector3(0f, 105f, 150f);
            target = BuildRig(ctx.PlaneName, targetStats, 1, targetPos);

            float Combined(FlightController rig) => rig.Damage!.Parts.Values.Sum(p => p.Hp + p.Armor);
            bool Pristine(FlightController rig) => rig.Damage!.Parts.Values.All(
                p => p.Hp >= p.Def.MaxHp && p.Armor >= p.Def.MaxArmor);
            void Step(int frames)
            {
                for (int i = 0; i < frames; i++)
                {
                    turret.SimStep(1f / 60f);
                    live.SimStep(1f / 60f);
                }
            }

            // The aim assist's turret candidate list is fed (the AimAssist.cs:387 seam).
            var candidates = new AimCandidateSet();
            live.CollectTurrets(candidates);
            ctx.Check(candidates.Turrets.Count == 1
                      && candidates.Turrets[0].Team == AimAssist.TeamOfPilot(host.PlayerIndex)
                      && candidates.Turrets[0].Live,
                $"CollectTurrets feeds the candidate scan count={candidates.Turrets.Count}");
            // The same list feeds the player's target pool, which takes emplacements only. A
            // carried gunner's host is already a target in its own right, so offering both would put
            // two entries on one silhouette; the discriminator is the placement Site.
            var carriedPool = new TargetPool();
            carriedPool.Rebuild(candidates, null, AimAssist.TeamOfPilot(host.PlayerIndex + 1), null);
            ctx.Check(turret.Site == null && carriedPool.Count == 0,
                $"a CARRIED turret is never selectable, though the same scan entry is a live aim-assist candidate on a hostile team pool={carriedPool.Count}");

            // --- track and fire: two seconds inside the initial 4 s attack window. The barrel
            // slews onto the target and the shots land — under the host's shooter id, so the
            // rounds crossing the host's own tail (the rear arc points across it) exclude it.
            float before = Combined(target);
            Step(120);
            var toTarget = (target.WorldPosition - turret.WorldPosition).Normalized();
            ctx.Check(turret.BarrelWorldDir.Dot(toTarget) > TurretController.FireGateCos,
                $"the barrel slewed onto the target dot={turret.BarrelWorldDir.Dot(toTarget):0.000}");
            ctx.Check(turret.ShotsFired >= 3,
                $"the turret fires through the 15° gate at its FIRE_RATE shots={turret.ShotsFired}");
            ctx.Check(Combined(target) < before,
                $"turret rounds strike the target moved={before - Combined(target):0.##}");
            ctx.Check(Pristine(host) && !host.Crashed,
                $"the host's own airframe took nothing from its own gunner");

            // --- the duty cycle: run to the bored window (ATTACK 4 s from build), then move the
            // target across the arc — firing stops, tracking does not.
            int shotsAtBored = -1;
            for (int i = 0; i < 600 && turret.Attacking; i++)
            {
                Step(1);
            }
            ctx.Check(!turret.Attacking, $"the attack window expires into bored");
            shotsAtBored = turret.ShotsFired;
            target.PlaceHeld(hostPos + new Vector3(90f, 105f, 120f), hostPos); // still in-arc, new bearing
            Step(90); // 1.5 s of the 3 s bored window
            var toMoved = (target.WorldPosition - turret.WorldPosition).Normalized();
            ctx.Check(turret.ShotsFired == shotsAtBored,
                $"bored suppresses firing shots={turret.ShotsFired} (was {shotsAtBored})");
            ctx.Check(turret.BarrelWorldDir.Dot(toMoved) > TurretController.FireGateCos,
                $"…but tracking continues through it dot={turret.BarrelWorldDir.Dot(toMoved):0.000}");

            // --- out of arc: a target ahead of the host sits outside YAW [105,255]; the turret
            // parks at the angularly NEARER end stop and never fires, whatever the duty cycle.
            target.PlaceHeld(hostPos + new Vector3(-52f, 105f, -140f), hostPos); // yaw ≈ +20°, in reach
            int shotsAtOutOfArc = turret.ShotsFired;
            Step(300); // 5 s spans at least one full attack window
            var (parkedYaw, _) = TurretController.AnglesOfLocal(turret.BarrelLocal);
            ctx.Check(turret.ShotsFired == shotsAtOutOfArc,
                $"an out-of-arc target draws no fire shots={turret.ShotsFired}");
            ctx.Check(Mathf.Abs(parkedYaw - 105f) < 1.5f,
                $"the barrel parks at the nearer end stop (105°, not 255°) yaw={parkedYaw:0.#}");

            // --- YAW [0,0] means UNRESTRICTED: with the limit spelled that way the same ahead
            // target becomes reachable and the turret opens fire — the misread ('locked forward')
            // would keep it silent forever. Pitch stays authored, so keep the target elevated.
            turret.Def.YawMinDeg = 0f;
            turret.Def.YawMaxDeg = 0f;
            Step(300);
            ctx.Check(turret.ShotsFired > shotsAtOutOfArc,
                $"YAW [0,0] removes the traverse limit shots={turret.ShotsFired} (was {shotsAtOutOfArc})");

            // --- a crashed host silences its gunner, and the candidate list reports it dead.
            host.DebugForceCrash();
            int shotsAtCrash = turret.ShotsFired;
            Step(120);
            ctx.Check(!turret.Alive && turret.ShotsFired == shotsAtCrash,
                $"a crashed host's turret goes quiet shots={turret.ShotsFired}");
            candidates.Clear();
            live.CollectTurrets(candidates);
            ctx.Check(candidates.Turrets.Count == 1 && !candidates.Turrets[0].Live,
                $"the dead turret stays listed but not live");
        }
        finally
        {
            pool?.Free();
            host?.Free();
            target?.Free();
            textures.Dispose();
        }
    }

    // The world AA emplacements against the real C1 chapter world: the NODES
    // placement census, the shipped-ACTIVATED default, the --wake-turrets stand-in, the
    // enemy-default/ally team split, the aim-assist candidate list, and the healthy-node kill
    // switch. Zeppelin-slung entries are placed (they are world nodes) and their one gameplay
    // path is checked here too: the Instant Action builder's subtree-scoped activation of the
    // objective hull's rings, both directions, plus the fire it puts on a plane alongside.
    internal static void WorldTurrets(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var turretDefs = TurretDefs.Load(ctx.ZrdrPath);
        ctx.Check(turretDefs.All.Count(d => !d.Carried) == 26,
            $"the 26 standalone ai.zrd entries parse standalone={turretDefs.All.Count(d => !d.Carried)}");

        // BL-403: one team space, so an emplacement and an aircraft on the same authored side
        // must compare equal. The enemy default is the id an Instant Action wave carries, and the
        // gate they meet at is the same one AcquireTarget runs.
        ctx.Check(!AimAssist.Hostile(TurretDef.DefaultTeamId, InstantActionRuntime.EnemyTeam)
                  && AimAssist.Hostile(TurretDef.DefaultTeamId, AimAssist.PlayerTeam),
            $"a no-TEAM emplacement ({TurretDef.DefaultTeamId}) spares the wave and engages the player");

        // The versus band exists so a splitscreen human cannot inherit an emplacement's side.
        ctx.Check(AimAssist.TeamOfPilot(0) == AimAssist.PlayerTeam
                  && AimAssist.TeamOfPilot(1) > AimAssist.VersusTeamBand
                  && AimAssist.Hostile(TurretDef.DefaultTeamId, AimAssist.TeamOfPilot(1)),
            $"--vs pilot 1 (team {AimAssist.TeamOfPilot(1)}) is still engaged by a no-TEAM emplacement");

        // Neutral is not a wildcard, and the predicate is symmetric on both sides of it.
        ctx.Check(!AimAssist.Hostile(AimAssist.NeutralTeam, AimAssist.PlayerTeam)
                  && !AimAssist.Hostile(AimAssist.PlayerTeam, AimAssist.NeutralTeam)
                  && !AimAssist.Hostile(AimAssist.PlayerTeam, AimAssist.PlayerTeam),
            $"team 0 never shoots and is never shot, and no side is hostile to itself");

        ctx.WithWorld("C1", collision: true, world =>
        {
            var textures = new TextureArchive(texturesPath);
            ProjectilePool? pool = null;
            FlightController? target = null;
            FlightController? friend = null;
            FlightController? zepBait = null;   // its own rig: the zeppelin rings shoot it to bits
            Session.TurretEmplacementRuntime? emplacements = null;   // a Node now: freed below
            try
            {
                var live = new ProjectilePool(textures, null, null)
                {
                    DamageSink = world.Runtime.DamageAt,
                };
                pool = live;
                ctx.Host.AddChild(live);
                var runtime = emplacements = new Session.TurretEmplacementRuntime(turretDefs, weapons,
                    (pattern, scope) => world.Runtime.FindNodes(pattern, scope), live,
                    world.Runtime.WorldRoot);

                // The placement census: one entry instantiates as many turrets as its patterns
                // match, so the counts are properties of C1's world model. Pinned as goldens.
                var byEntry = new Dictionary<string, int>();
                foreach (var t in runtime.Emplacements)
                {
                    string site = t.Label[(t.Label.IndexOf('@') + 1)..];
                    string root = site.TrimEnd("0123456789 ".ToCharArray());
                    string key = $"{t.Def.Title}:{root}";
                    byEntry.TryGetValue(key, out int had);
                    byEntry[key] = had + 1;
                }
                ctx.Note($"C1 census: {string.Join(", ", byEntry.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"))}");
                int AagunCount() => runtime.Emplacements.Count(t => t.Label.StartsWith("MSG_TUR_AAA@aagun"));
                ctx.Same(5, AagunCount(), $"C1 places the five aagun emplacements");
                ctx.Same(9, runtime.Emplacements.Count(t => t.Label.StartsWith("MSG_TUR_BALLOON_TOP@bbtur")),
                    $"C1 places the nine balloon-top emplacements");
                ctx.Same(74, runtime.Count, $"C1's whole emplacement census");
                ctx.Same(15, runtime.AwakeCount,
                    $"shipped ACTIVATED: only the piratezep's own awake rings are up (ally, TEAM 1)");
                ctx.Check(runtime.Emplacements.Where(t => t.Activated)
                        .All(t => t.Team == AimAssist.PlayerTeam),
                    $"every emplacement awake by data is on the ally team — no hostile fires unwoken");

                // A dormant hostile emplacement: aagun32, enemy by the loader's no-TEAM default.
                var aagun = runtime.Emplacements.FirstOrDefault(t => t.Label.EndsWith("@aagun32"));
                ctx.Check(aagun != null, $"aagun32 built a gunner");
                if (aagun == null)
                    return;
                ctx.Check(!aagun.Activated && AimAssist.Hostile(aagun.Team, AimAssist.PlayerTeam),
                    $"aagun32 is dormant and hostile to the player by default team={aagun.Team}");
                aagun.Def.InaccuracyDeg = 0f; // determinism: the scatter cone is the assist suite's

                FlightController BuildRig(string plane, int playerIndex, Vector3 pos, Vector3 look)
                {
                    var st = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
                    var model = new PlaneBuilder(planesGamez, textures).Build(plane);
                    var rig = new FlightController
                    {
                        PlaneModel = model,
                        Collider = PlaneCollider.Build(model),
                        Damage = new PlaneDamage(st.DestroyableParts),
                        PlayerIndex = playerIndex,
                        Projectiles = live,
                        UseKeyboard = false,
                        AllowPause = false,
                    };
                    rig.AddChild(model);
                    rig.Setup(new FlightModel(st), ctx.Camera, new CamParams(), pos, look);
                    ctx.Host.AddChild(rig);
                    rig.PlaceHeld(pos, look); // parked: no phantom velocity for the lead solve
                    return rig;
                }

                // A hostile plane parked inside DETECTION_RANGE (500 m), well above the fort.
                var gunPos = aagun.WorldPosition;
                var targetPos = gunPos + new Vector3(120f, 250f, 0f);
                target = BuildRig(ctx.PlaneName, 0, targetPos, targetPos + Vector3.Forward);

                void Step(int frames)
                {
                    for (int i = 0; i < frames; i++)
                    {
                        runtime.SimStep(1f / 60f);
                        live.SimStep(1f / 60f);
                    }
                }

                float Combined(FlightController rig) => rig.Damage!.Parts.Values.Sum(p => p.Hp + p.Armor);

                // Dormant = inert: no tracking, no fire, through several attack windows.
                var restDir = aagun.BarrelWorldDir;
                Step(240);
                ctx.Check(aagun.ShotsFired == 0 && aagun.BarrelWorldDir.IsEqualApprox(restDir),
                    $"a dormant emplacement neither tracks nor fires shots={aagun.ShotsFired}");

                // The Instant Action builder's own turret arm: a subtree-scoped ACTIVATED write over the objective
                // zeppelin's node, which is what arms multiplayer1zep's four dormant entries. Scoped and
                // reversible; the deactivation loop is the same call with the flag cleared.
                var mp1 = world.Runtime.FindNodes("multiplayer1zep");
                ctx.Same(1, mp1.Count, $"C1's world carries the Instant Action zeppelin node");
                var mp2 = world.Runtime.FindNodes("multiplayer2zep");
                ctx.Same(1, mp2.Count, $"…and the second MP zeppelin, the control for the scoping");
                if (mp1.Count > 0 && mp2.Count > 0)
                {
                    List<TurretController> RingsOn(Node3D root) => runtime.Emplacements
                        .Where(t => t.Site is { } s && (s == root || root.IsAncestorOf(s))).ToList();
                    var mp1Rings = RingsOn(mp1[0]);
                    var mp2Rings = RingsOn(mp2[0]);
                    ctx.Same(14, mp1Rings.Count,
                        $"multiplayer1zep carries 14 rings (3 nose, 3 belly, 4 left, 4 right)");
                    ctx.Check(mp1Rings.All(t => !t.Activated),
                        $"…every one of them dormant by data, which is why it flies unarmed");
                    ctx.Same(14, runtime.SetActivatedUnder(mp1[0], true),
                        $"the builder's zeppelin arm arms every ring on the objective hull");
                    // BL-403: hostile to the PLAYER, and never to the wave this hull launches —
                    // both halves, since the band made the second half impossible.
                    ctx.Check(mp1Rings.All(t => t.Activated
                            && AimAssist.Hostile(t.Team, AimAssist.PlayerTeam)
                            && !AimAssist.Hostile(t.Team, InstantActionRuntime.EnemyTeam)),
                        $"…all awake, hostile to the player, and allied with their own bay wave");
                    ctx.Check(!aagun.Activated && mp2Rings.All(t => !t.Activated),
                        $"…and nothing outside that subtree woke with it");

                    // A ring's own MOUNTING SECTION is out of its sight line, because the ray starts inside that
                    // geometry; the rest of the hull stays in. ⚠ Do not assert that through ray outcomes here: every
                    // unplaced vehicle loads at the map corner, so other zeppelins sit inside this one and block a line.
                    var ring = mp1Rings[0];
                    var section = TurretController.PlatformOf(ring.Site, world.Runtime.WorldRoot);
                    ctx.Check(section != null && section != mp1[0] && mp1[0].IsAncestorOf(section)
                              && section.IsAncestorOf(ring.Site!),
                        $"a ring's mounting section is a piece OF the hull ('{section?.Name}'), never the whole hull and never just its own rig");
                    var excluded = ring.PlatformColliderRids();
                    var ownMount = ring.Site!.FindChildren("*", "CollisionObject3D", true, false)
                        .OfType<CollisionObject3D>().ToList();
                    var hullBodies = mp1[0].FindChildren("*", "CollisionObject3D", true, false)
                        .OfType<CollisionObject3D>().ToList();
                    ctx.Check(ownMount.Count > 0 && ownMount.All(b => excluded.Contains(b.GetRid())),
                        $"the gun's own mount is out of its sight line: {ownMount.Count} body/bodies, the ones the ray starts inside");
                    ctx.Check(excluded.Count > ownMount.Count && excluded.Count < hullBodies.Count,
                        $"…with its own section but NOT the whole hull: {excluded.Count} excluded of the hull's {hullBodies.Count}");

                    // What the player actually feels: an armed hull shoots back, on its own rig so its rounds do not
                    // touch the aagun figures. ⚠ Show the hull first, exactly as the Instant Action builder does:
                    // C1/IA1 hides multiplayer1zep, and a hidden hull has no live colliders to block a turret.
                    mp1[0].Visible = true;
                    int ZepShots() => mp1Rings.Sum(t => t.ShotsFired);
                    var ringPos = mp1Rings.Count > 0 ? mp1Rings[0].WorldPosition : Vector3.Zero;
                    zepBait = BuildRig(ctx.PlaneName, 0, ringPos + new Vector3(0f, -80f, 200f), ringPos);
                    float baitBefore = Combined(zepBait);
                    Step(300);
                    ctx.Check(ZepShots() > 0,
                        $"the armed zeppelin engages a hostile plane alongside shots={ZepShots()}");
                    ctx.Check(Combined(zepBait) < baitBefore,
                        $"…with rounds striking it moved={baitBefore - Combined(zepBait):0.##}");

                    zepBait.PlaceHeld(ringPos + new Vector3(0f, -80f, 20000f), ringPos);

                    ctx.Same(14, runtime.SetActivatedUnder(mp1[0], false),
                        $"the same call with the flag cleared stows them again (the b=0 arm)");
                }

                // The stand-in wakes it — explicit, counted, logged — and it engages.
                int woken = runtime.WakeAll();
                ctx.Same(runtime.Count - 15, woken, $"--wake-turrets stand-in wakes every dormant emplacement");
                float before = Combined(target);
                Step(360);
                var toTarget = (target.WorldPosition - aagun.WorldPosition).Normalized();
                ctx.Check(aagun.BarrelWorldDir.Dot(toTarget) > TurretController.FireGateCos,
                    $"the woken gun slewed onto the plane dot={aagun.BarrelWorldDir.Dot(toTarget):0.000}");
                ctx.Check(aagun.ShotsFired >= 2,
                    $"…and fires at its FIRE_RATE shots={aagun.ShotsFired}");
                ctx.Check(Combined(target) < before,
                    $"…with rounds striking the target moved={before - Combined(target):0.##}");

                // The team gate, both halves over the piratezep's allied rings: none engages the player's own
                // plane, and the same rings do engage a hostile pane, which is the able-to-fail control. Run over
                // the whole allied population, since the per-ring arcs are the zeppelin's own frame.
                var allied = runtime.Emplacements
                    .Where(t => t.Team == AimAssist.PlayerTeam).ToList();
                ctx.Check(allied.Count > 0, $"the piratezep's allied rings exist count={allied.Count}");
                if (allied.Count > 0)
                {
                    int AlliedShots() => allied.Sum(t => t.ShotsFired);
                    var zepPos = allied[0].WorldPosition;
                    target.PlaceHeld(zepPos + new Vector3(0f, -80f, 200f), zepPos);
                    Step(240);
                    ctx.Check(AlliedShots() == 0,
                        $"every allied ring holds fire on the player's own team shots={AlliedShots()}");
                    friend = BuildRig(ctx.PlaneName, 1, zepPos + new Vector3(50f, -80f, 200f), zepPos);
                    Step(600);
                    ctx.Check(AlliedShots() > 0,
                        $"…and the same rings engage a hostile pane there shots={AlliedShots()}");
                }

                // The aim assist's turret list now carries the emplacements too — the player's
                // lock-on sees world AA, dormant or not, until it dies.
                var candidates = new AimCandidateSet();
                live.CollectTurrets(candidates);
                ctx.Same(runtime.Count, candidates.Turrets.Count,
                    $"CollectTurrets feeds every emplacement to the candidate scan");

                // The kill switch: the emplacement's own destructible dies through the weapon-damage path, its
                // healthy node hides, and the gunner goes permanently quiet. ai.zrd HEALTH is authored-but-unread;
                // the real pool is the gamez destroy def's.
                target.PlaceHeld(targetPos, targetPos + Vector3.Forward);
                int killed = ProbeRunner.TriggerDestroy(world.Runtime, "aagun32", out _);
                ctx.Check(killed > 0, $"aagun32's destructible died to weapon damage killed={killed}");
                int shotsAtDeath = aagun.ShotsFired;
                Step(240);
                ctx.Check(!aagun.Alive && aagun.ShotsFired == shotsAtDeath,
                    $"the dead emplacement goes quiet shots={aagun.ShotsFired}");
                candidates.Clear();
                live.CollectTurrets(candidates);
                ctx.Check(candidates.Turrets.Count(c => !c.Live) >= 1,
                    $"…and the candidate list reports it dead");
            }
            finally
            {
                pool?.Free();
                target?.Free();
                friend?.Free();
                zepBait?.Free();
                emplacements?.Free();
                textures.Dispose();
            }
        });

        // A second chapter's census (C4: the ground AA belt — aagun/tcargun/t_truck/8igun),
        // built and freed here; placement only, no firing. b_turret sites place too.
        ctx.WithWorld("C4", collision: false, world =>
        {
            using var c4Textures = new TextureArchive(texturesPath);
            var live = new ProjectilePool(c4Textures, null, null);
            ctx.Host.AddChild(live);
            Session.TurretEmplacementRuntime? c4Emplacements = null;
            try
            {
                var runtime = c4Emplacements = new Session.TurretEmplacementRuntime(turretDefs, weapons,
                    (pattern, scope) => world.Runtime.FindNodes(pattern, scope), live,
                    world.Runtime.WorldRoot);
                var census = new List<string>();
                foreach (var g in runtime.Emplacements.GroupBy(t => t.Def.Title).OrderBy(g => g.Key))
                    census.Add($"{g.Key}={g.Count()}");
                ctx.Note($"C4 census: {string.Join(", ", census)}");
                ctx.Same(5, runtime.Emplacements.Count(t => t.Label.StartsWith("MSG_TUR_AAA@aagun")),
                    $"C4 places the five aagun emplacements");
                ctx.Same(3, runtime.Emplacements.Count(t => t.Label.StartsWith("MSG_TUR_TRAIN@tcargun")),
                    $"C4 places the three train-car guns");
                ctx.Same(1, runtime.Emplacements.Count(t => t.Label.StartsWith("MSG_TUR_8_INCH@8igun")),
                    $"C4 places the one 8-inch gun");
                ctx.Same(92, runtime.Count, $"C4's whole emplacement census");
                // The piratezep model (and its allied awake rings) is part of EVERY chapter's
                // world — the awake set is a world-model property, not a C1 fact.
                ctx.Same(15, runtime.AwakeCount, $"C4's awake set is the piratezep's own rings again");
            }
            finally
            {
                c4Emplacements?.Free();
                live.Free();
            }
        });
    }

    // The AI actor seam against real engine state on manual sim steps. A human rig is built and stepped
    // first, so the AI plane demonstrably joins a RUNNING sim, with an AiPilot for input, no camera, no
    // HUD and IsHumanPiloted false. It pins presence as a hit target, ticking along its ordered course,
    // mid-flight retargeting, part pools moved by the weapon's own ARMOR_DAMAGE, and a kill attributed
    // to the human shooter through Downed. Inventory: this module's docs/architecture.md entry.
    internal static void AiActor(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        WeaponDef? gun = weapons.All.FirstOrDefault(w =>
            w.IsGun && w.ArmorDamage is > 0f && w.HealthDamage is > 0f);
        ctx.Check(gun != null, $"a gun with ARMOR_DAMAGE and HEALTH_DAMAGE exists in the data");
        if (gun == null)
            return;
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var nose = stats.DestroyableParts.FirstOrDefault(p =>
            p.Name.Equals("nose", System.StringComparison.OrdinalIgnoreCase));
        ctx.Check(nose is { Critical: true }, $"{ctx.PlaneName} carries a critical nose part");
        if (nose == null)
            return;

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? shooter = null;
        FlightController? ai = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            // The human rig first, and 120 sim steps before the AI exists: the spawn below is
            // a RUNTIME spawn into a sim already in motion, not part of a session build.
            var shooterModel = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            shooter = new FlightController
            {
                PlaneModel = shooterModel,
                Collider = PlaneCollider.Build(shooterModel),
                Damage = new PlaneDamage(stats.DestroyableParts),
                PlayerIndex = 0,
                Projectiles = live,
                UseKeyboard = false,
                AllowPause = false,
            };
            shooter.AddChild(shooterModel);
            shooter.Setup(new FlightModel(stats), ctx.Camera, new CamParams(),
                new Vector3(2000f, 500f, 0f), new Vector3(2000f, 500f, -1f));
            ctx.Host.AddChild(shooter);
            for (int i = 0; i < 120; i++)
            {
                live.SimStep(1f / 60f);
                shooter.SimStep(1f / 60f);
            }

            // The AI actor: an AiPilot ordered to hold the spawn course, a null camera, no HUD,
            // no devices — exactly what FlightRoster builds, on the suite's own stage.
            var spawnPos = new Vector3(0f, 500f, 0f);
            var pilot = AiPilot.HoldingCourse(spawnPos, spawnPos + Vector3.Forward);
            var aiModel = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            ai = new FlightController
            {
                PlaneModel = aiModel,
                Collider = PlaneCollider.Build(aiModel),
                Damage = new PlaneDamage(stats.DestroyableParts),
                PlayerIndex = FlightRoster.ShooterIdBase,
                IsHumanPiloted = false,
                Pilot = pilot,
                Projectiles = live,
                UseKeyboard = false,
                PadDevices = System.Array.Empty<int>(),
                AllowPause = false,
            };
            ai.AddChild(aiModel);
            ai.Setup(new FlightModel(stats), null, new CamParams(), spawnPos, spawnPos + Vector3.Forward);
            ctx.Host.AddChild(ai);

            // Present: in the tree, body built, registered as a hit target on the player id, and
            // visible to a physics ray where it spawned.
            ctx.Check(ai.IsInsideTree() && ai.Body != null,
                $"the AI plane is in the tree with an AircraftBody");
            if (ai.Body == null)
                return;
            ctx.Check(ProjectilePool.SurfaceIdOf(ai.Body) == SurfaceRegistry.Player,
                $"the AI body answers surface id {SurfaceRegistry.Player} (player) — weapon IMPACT rows fire on it");
            var space = live.GetWorld3D().DirectSpaceState;
            var probe = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                spawnPos + new Vector3(0f, 0f, -30f), spawnPos, CollisionLayers.WorldAndAircraft));
            ctx.Check(probe.Count > 0 && ReferenceEquals(probe["collider"].Obj, ai.Body),
                $"a physics ray at the spawned AI returns its body");

            // ⚠ Run the gunfire phases at the SPAWN pose, before the plane flies anywhere: a body moved after
            // creation is invisible to space queries until a physics flush this one-frame suite never gets
            // (INSTR-13). Damage first, flight after.
            float armorDmg = gun.ArmorDamage!.Value;
            float healthDmg = gun.HealthDamage!.Value;
            float Combined() => ai!.Damage!.Parts.Values.Sum(p => p.Hp + p.Armor);
            var muzzle = new Transform3D(
                Basis.LookingAt(Vector3.Back, Vector3.Up), spawnPos + new Vector3(0f, 0f, -10f));
            void FireOne(int steps = 10)
            {
                live.Spawn(gun, muzzle, Vector3.Zero, shooter!.PlayerIndex);
                for (int i = 0; i < steps; i++)
                    live.SimStep(1f / 60f);
                live.Clear();
            }

            // Damageable: a registering round moves the pools by exactly the weapon's
            // ARMOR_DAMAGE (armor-first over full pools, so the combined total moves by it).
            float beforeHit = Combined();
            int tries = 0;
            while (Combined() >= beforeHit && tries < 5)
            {
                tries++;
                FireOne();
            }
            ctx.Check(Mathf.IsEqualApprox(beforeHit - Combined(), armorDmg),
                $"a round moves the AI's pools by the weapon's ARMOR_DAMAGE moved={beforeHit - Combined():0.##} expected={armorDmg:0.##} (rounds={tries})");

            // Killable, with the kill attributed: pre-empty the other zones with EXACT spends, since an
            // overkill spend would down the plane through the whole-pool overflow before the AI's own burst,
            // then sustained fire on the same bearing exhausts the nose.
            foreach (var p in ai.Damage!.Parts.Values)
            {
                if (p.Def != nose)
                {
                    ai.Damage.Apply(p.Def.Name, 0f, p.Armor);
                    ai.Damage.Apply(p.Def.Name, p.Hp, 0f);
                }
            }
            int? downedVictim = null, downedKiller = null;
            ai.Downed += (victim, killer) => { downedVictim = victim; downedKiller = killer; };
            int budget = (int)(nose.MaxArmor / armorDmg + nose.MaxHp / healthDmg) * 3 + 20;
            int fired = 0;
            while (!ai.Crashed && fired < budget)
            {
                fired++;
                FireOne(8);
            }
            ctx.Check(ai.Crashed, $"sustained fire downs the AI plane rounds={fired}/{budget}");
            ctx.Check(downedVictim == FlightRoster.ShooterIdBase,
                $"Downed reports the AI's own shooter id victim={downedVictim?.ToString() ?? "-"}");
            ctx.Check(downedKiller == shooter.PlayerIndex,
                $"…with the kill attributed to the human shooter killer={downedKiller?.ToString() ?? "-"}");

            // Back into the air for the flying half — Respawn repairs the wreck and re-arms the
            // same pilot; nothing below needs a physics query.
            ai.Respawn();
            void FlyAi(float seconds)
            {
                for (int i = 0; i < (int)(seconds * 60f); i++)
                {
                    live.SimStep(1f / 60f);
                    ai!.SimStep(1f / 60f);
                }
            }

            // Ticking, on its orders: 10 s of manual sim steps move it along the ordered course
            // (world -Z) at altitude, driven by AiPilot — no keyboard, no hold script.
            var before = ai.WorldPosition;
            FlyAi(10f);
            var disp = ai.WorldPosition - before;
            ctx.Check(disp.Length() > 300f,
                $"the AI plane flies under its pilot moved={disp.Length():0} m in 10 s");
            ctx.Check(disp.Normalized().Dot(Vector3.Forward) > 0.9f,
                $"…along its ordered course dot={disp.Normalized().Dot(Vector3.Forward):0.00}");
            ctx.Check(Mathf.Abs(ai.WorldPosition.Y - 500f) < 80f,
                $"…holding its ordered altitude y={ai.WorldPosition.Y:0}");

            // Orders are mutable mid-flight: retarget 90° between steps, no rebuild, no respawn.
            pilot.TargetHeadingDeg = 90f;
            FlyAi(25f);
            var noseDir = -ai.GlobalTransform.Basis.Z;
            float errDeg = Mathf.Wrap(90f - AiPilot.HeadingDegOf(noseDir), -180f, 180f);
            ctx.Check(Mathf.Abs(errDeg) < 10f,
                $"a mid-flight retarget is flown to err={errDeg:0.0}° after 25 s");
        }
        finally
        {
            pool?.Free();
            shooter?.Free();
            ai?.Free();
            textures.Dispose();
        }
    }

    // The far-field plant's PLUMBING on live rigs: which aircraft is told where the humans are, how
    // that range is measured, and that the plant switches on it. The branch's own arithmetic is
    // engine-free in CSVM.Tests/FarFieldPlantTests; only the seam needs an engine.
    // Inventory: this module's docs/architecture.md entry.
    internal static void AiFarFieldPlant(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var textures = new TextureArchive(texturesPath);
        var humanPos = new Vector3(0f, 500f, 0f);
        var humans = new List<Vector3> { humanPos };

        FlightController? near = null;
        FlightController? far = null;
        try
        {
            // One AI rig at a given offset from the human, flying its spawn course. Its own model is
            // kept by the caller because the branch readout lives on the plant, not on the node.
            (FlightController Rig, FlightModel Model) BuildAi(int index, Vector3 offset)
            {
                var model3d = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
                var pos = humanPos + offset;
                var rig = new FlightController
                {
                    PlaneModel = model3d,
                    PlayerIndex = FlightRoster.ShooterIdBase + index,
                    IsHumanPiloted = false,
                    Pilot = AiPilot.HoldingCourse(pos, pos + Vector3.Forward),
                    UseKeyboard = false,
                    PadDevices = System.Array.Empty<int>(),
                    AllowPause = false,
                    HumanPositions = () => humans,
                };
                rig.AddChild(model3d);
                var plant = new FlightModel(stats, aiForcePath: true);
                rig.Setup(plant, null, new CamParams(), pos, pos + Vector3.Forward);
                ctx.Host.AddChild(rig);
                return (rig, plant);
            }

            // 100 m out and 1200 m out, level with the human and on the same heading. The near rig's
            // range grows as it flies, so it is placed to stay inside 1000 m for the whole run.
            var (nearRig, nearPlant) = BuildAi(0, new Vector3(100f, 0f, 0f));
            var (farRig, farPlant) = BuildAi(1, new Vector3(1200f, 0f, 0f));
            near = nearRig;
            far = farRig;
            nearRig.SimStep(1f / 60f);
            farRig.SimStep(1f / 60f);

            ctx.Check(!nearPlant.FarFieldPlant,
                $"an AI 100 m from the human flies the near-field aerodynamics");
            ctx.Check(farPlant.FarFieldPlant,
                $"an AI 1200 m from the human flies the far-field speed-hold plant");

            // Horizontal only: 3 km of altitude between them is not range. The human is moved rather
            // than the rig rebuilt, so nothing but the vertical separation changes.
            humans[0] = humanPos + new Vector3(0f, 3000f, 0f);
            nearRig.SimStep(1f / 60f);
            ctx.Check(!nearPlant.FarFieldPlant,
                $"3 km of vertical separation does not send an aircraft far-field: the range is horizontal");
            humans[0] = humanPos;

            // The NEAREST human decides it, which is this engine's four-player widening of the
            // original's single player pointer. A second human beside the far rig brings it back.
            humans.Add(farRig.WorldPosition + new Vector3(0f, 0f, 200f));
            farRig.SimStep(1f / 60f);
            ctx.Check(!farPlant.FarFieldPlant,
                $"a second human 200 m from the far rig returns it to the near-field plant");
            humans.RemoveAt(1);
            farRig.SimStep(1f / 60f);
            ctx.Check(farPlant.FarFieldPlant, $"and it goes far-field again when that human leaves");

            // Five seconds of flight. Both rigs fly the same orders from the same relative pose, so
            // a divergence between them is the plant and nothing else.
            for (int i = 0; i < 300; i++)
            {
                nearRig.SimStep(1f / 60f);
                farRig.SimStep(1f / 60f);
            }

            ctx.Check(!nearPlant.FarFieldPlant && farPlant.FarFieldPlant,
                $"both rigs held their branch across the run near={nearPlant.FarFieldPlant} far={farPlant.FarFieldPlant}");

            // The speed-hold: the far rig sits on throttle x fd_speed + 5 m/s, tracking its own lever
            // through the 1/s lag, while the near rig on the identical orders is elsewhere.
            float held = (farPlant.Throttle * stats.FdSpeed) + 5f;
            float farErr = Mathf.Abs(farPlant.Speed - held);
            float nearErr = Mathf.Abs(nearPlant.Speed - ((nearPlant.Throttle * stats.FdSpeed) + 5f));
            ctx.Check(farErr < 2f,
                $"the far rig holds throttle x fd_speed + 5 speed={farPlant.Speed:0.0} held={held:0.0} err={farErr:0.00} m/s");
            ctx.Check(nearErr > 5f,
                $"the near rig's aerodynamics do not put it there err={nearErr:0.0} m/s (the control)");

            // A rig with no seam bound stays near-field: no snapshot means no known human, and the
            // aerodynamics are what an aircraft with no session around it must keep flying.
            farRig.HumanPositions = null;
            farRig.SimStep(1f / 60f);
            ctx.Check(!farPlant.FarFieldPlant,
                $"an unbound HumanPositions seam leaves the aircraft on the near-field plant");
        }
        finally
        {
            near?.Free();
            far?.Free();
            textures.Dispose();
        }
    }

    // The AI gunnery on real engine state: an AI-piloted, stock-armed plane HELD at fixed poses against
    // a parked hostile. It pins acquisition into the gunner's mutable target field, the quick-draw
    // gate, the forward gun cone, dead-eye scatter at skill 1 against 9, the kill attributed through
    // Downed, and the IsHumanPiloted assist exclusion A/B'd on one rig. Inventory: docs/architecture.md.
    // ⚠ Park the target at its spawn pose and never move it; a body moved inside the suite's single
    // frame is invisible to the rounds' space queries (INSTR-13).
    internal static void AiGunnery(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var skills = AiSkills.Load(ctx.ZrdrPath);
        LoadoutDef? stock = null;
        foreach (var def in StockLoadouts.Load().All.Values)
        {
            if (def.Model == ctx.PlaneName)
            {
                stock = def;
                break;
            }
        }
        ctx.Check(stock != null, $"stock loadout found for plane={ctx.PlaneName}");
        if (stock == null)
            return;

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? target = null;
        FlightController? ai = null;
        FlightController? rival = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            // The parked target: a human-index rig at the origin column, PINNED at zero speed so
            // the gunner's intercept sees a static target (a never-stepped rig would otherwise
            // report its spawn speed and every lead would miss a plane that is not moving).
            var targetPos = new Vector3(0f, 500f, 0f);
            var targetModel = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            target = new FlightController
            {
                PlaneModel = targetModel,
                Collider = PlaneCollider.Build(targetModel),
                Damage = new PlaneDamage(stats.DestroyableParts),
                PlayerIndex = 0,
                Projectiles = live,
                UseKeyboard = false,
                PadDevices = System.Array.Empty<int>(),
                AllowPause = false,
            };
            target.AddChild(targetModel);
            target.Setup(new FlightModel(stats), ctx.Camera, new CamParams(), targetPos, targetPos + Vector3.Forward);
            ctx.Host.AddChild(target);
            target.PlaceHeld(targetPos, targetPos + Vector3.Forward);

            // The AI shooter: stock-armed (the gunner fires through FireControl, not scripted
            // spawns), AiPilot + AiGunner, held at each phase's pose. Its own seeded scatter rng
            // makes every volley reproducible.
            var gunner = new AiGunner(new RandomNumberGenerator { Seed = 20260813 })
            {
                DeadEyeAngleDeg = skills.DeadEyeAngleDeg(9),
                QuickDrawAngleDeg = skills.QuickDrawAngleDeg(1),
            };
            var aiPos = targetPos + new Vector3(0f, 0f, 500f); // dead astern of the target's tail
            var pilot = AiPilot.HoldingCourse(aiPos, targetPos);
            pilot.Gunner = gunner;
            var aiModel = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            ai = new FlightController
            {
                PlaneModel = aiModel,
                Collider = PlaneCollider.Build(aiModel),
                Damage = new PlaneDamage(stats.DestroyableParts),
                PlayerIndex = FlightRoster.ShooterIdBase,
                IsHumanPiloted = false,
                Pilot = pilot,
                Projectiles = live,
                UseKeyboard = false,
                PadDevices = System.Array.Empty<int>(),
                AllowPause = false,
            };
            ai.AddChild(aiModel);
            ai.Loadout = Loadout.Bind(stock, aiModel, weapons);
            ai.Setup(new FlightModel(stats), null, new CamParams(), aiPos, targetPos);
            ctx.Host.AddChild(ai);
            ai.Held = true;
            ai.PlaceHeld(aiPos, targetPos);

            var gun = ai.Loadout.FirableGuns.First();
            float armorDmg = gun.Weapon.ArmorDamage ?? 0f;
            ctx.Check(armorDmg > 0f && Mathf.IsEqualApprox(armorDmg, gun.Weapon.HealthDamage ?? -1f),
                $"the stock gun's two damage magnitudes are equal ({gun.Weapon.Id}, {armorDmg:0.#}) — the hit counter's precondition");
            float Combined(FlightController rig) => rig.Damage!.Parts.Values.Sum(p => p.Hp + p.Armor);
            bool Pristine(FlightController rig) => rig.Damage!.Parts.Values.All(
                p => p.Hp >= p.Def.MaxHp && p.Armor >= p.Def.MaxArmor);
            void Step(int frames)
            {
                for (int i = 0; i < frames; i++)
                {
                    ai!.SimStep(1f / 60f);
                    live.SimStep(1f / 60f);
                }
            }

            // --- acquisition: the nearest hostile lands in the gunner's mutable target field.
            Step(1);
            ctx.Check(ReferenceEquals(gunner.Target, target),
                $"the gunner auto-acquires the nearest hostile aircraft");
            // Mutable state: cleared orders re-acquire; an explicit assignment stands.
            gunner.Target = null;
            Step(1);
            ctx.Check(ReferenceEquals(gunner.Target, target), $"a cleared target re-acquires next tick");

            // --- the quick-draw gate: 80° off the target's tail axis (a beam-ish shot). At
            // rating 1 the ~54° cone refuses it; at rating 9 the 89° cone takes it.
            var beamPos = targetPos + new Vector3(0.9848f, 0f, 0.1736f) * 500f; // 80° off +Z
            ai.PlaceHeld(beamPos, targetPos);
            int ammoAtBeam = gun.Ammo;
            Step(60);
            ctx.Check(!gunner.WantsFire && gun.Ammo == ammoAtBeam,
                $"a beam-ish shot is refused at quick-draw rating 1 (~54°) rounds={ammoAtBeam - gun.Ammo}");
            gunner.QuickDrawAngleDeg = skills.QuickDrawAngleDeg(9);
            Step(60);
            ctx.Check(gun.Ammo < ammoAtBeam,
                $"the same bearing is taken at rating 9 (89°) rounds={ammoAtBeam - gun.Ammo}");

            // --- the aim gate: nose 30° off the bearing clamps to the airframe's 11° and leaves
            // a 19° residual, past the gun's 10°, so the shot is refused with quick draw willing.
            ai.PlaceHeld(beamPos, beamPos + (targetPos - beamPos).Normalized()
                .Rotated(Vector3.Up, Mathf.DegToRad(30f)) * 100f);
            int ammoAtOffBore = gun.Ammo;
            Step(60);
            ctx.Check(!gunner.WantsFire && gun.Ammo == ammoAtOffBore,
                $"a 19° residual after the traverse clamp holds fire (nose 30° off)");

            // Dead-eye scatter, skill 1 against 9 on fixed geometry: the high rear quarter at ~212 m, where the
            // planform presents real area. Dead astern the airframe is edge-on and both cones mostly miss,
            // which drowns the difference the volley is measuring.
            ai.PlaceHeld(targetPos + new Vector3(0f, 150f, 150f), targetPos);
            (int Rounds, int Hits) Volley(float deadEyeDeg, int roundCap)
            {
                gunner!.DeadEyeAngleDeg = deadEyeDeg;
                gunner.AutoTarget = true;
                gunner.Target = target;
                target!.Respawn();
                target.PlaceHeld(targetPos, targetPos + Vector3.Forward);
                live.Clear(); // no stragglers from an earlier phase land on this ledger
                float before = Combined(target);
                int ammoBefore = gun.Ammo;
                for (int guard = 0; ammoBefore - gun.Ammo < roundCap && guard < 1800; guard++)
                    Step(1);
                gunner.AutoTarget = false;
                gunner.Target = null; // cease fire; let the last rounds land
                Step(90);
                return (ammoBefore - gun.Ammo, (int)Mathf.Round((before - Combined(target)) / armorDmg));
            }

            var low = Volley(skills.DeadEyeAngleDeg(1), 30);
            var high = Volley(skills.DeadEyeAngleDeg(9), 30);
            ctx.Note($"dead-eye: skill 1 hit {low.Hits}/{low.Rounds}, skill 9 hit {high.Hits}/{high.Rounds} from the high rear quarter at ~212 m");
            // Fixed seed, so the measured gap (13 vs 6 of 30) is exact; the +4 margin is what
            // "measurably" means here, not a statistical bound.
            ctx.Check(high.Hits >= low.Hits + 4,
                $"skill 9's tighter cone out-hits skill 1 ({high.Hits}/{high.Rounds} vs {low.Hits}/{low.Rounds})");
            ctx.Check(high.Hits >= 1 && low.Hits < low.Rounds,
                $"both regimes are real: skill 9 lands rounds, skill 1 scatters some wide");
            ctx.Check(Pristine(ai) && !ai.Crashed, $"the AI's own airframe took none of its own fire");

            // The kill attributed to the AI's shooter id, from dead astern at 150 m. Scaffolding per the
            // whole-vehicle rule: the zones this bearing cannot reach are pre-emptied, and the AI's own fire
            // finishes the plane.
            gunner.AutoTarget = true;
            gunner.DeadEyeAngleDeg = skills.DeadEyeAngleDeg(9);
            ai.PlaceHeld(targetPos + new Vector3(0f, 0f, 150f), targetPos);
            target.Respawn();
            target.PlaceHeld(targetPos, targetPos + Vector3.Forward);
            live.Clear();
            foreach (var p in target.Damage!.Parts.Values)
            {
                if (!p.Def.Name.Equals("tail", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Exact spends: an overkill spend would kill through the whole-pool overflow.
                    target.Damage.Apply(p.Def.Name, 0f, p.Armor);
                    target.Damage.Apply(p.Def.Name, p.Hp, 0f);
                }
            }
            int? downedVictim = null, downedKiller = null;
            target.Downed += (victim, killer) => { downedVictim = victim; downedKiller = killer; };
            for (int guard = 0; !target.Crashed && guard < 3600; guard++)
                Step(1);
            ctx.Check(target.Crashed, $"the AI's own gunnery downs the target");
            ctx.Check(downedVictim == target.PlayerIndex && downedKiller == ai.PlayerIndex,
                $"the kill is attributed to the AI's shooter id killer={downedKiller?.ToString() ?? "-"}");

            // The IsHumanPiloted assist exclusion, A/B'd in place: nose 3 degrees off the bearing at 400 m,
            // gunner disarmed, trigger held raw. As an AI the rounds leave along the muzzle axis and all miss;
            // the same rig flagged human gets the assist, which snaps onto the target and lands hits.
            pilot.Gunner = null;
            ai.AutoFire = true;
            target.Respawn();
            target.PlaceHeld(targetPos, targetPos + Vector3.Forward);
            live.Clear(); // the kill phase's last rounds must not land on this ledger
            var nearPos = targetPos + new Vector3(0f, 0f, 400f);
            var offDir = (targetPos - nearPos).Normalized().Rotated(Vector3.Up, Mathf.DegToRad(3f));
            ai.PlaceHeld(nearPos, nearPos + offDir * 100f);
            float pristineCombined = Combined(target);
            Step(180);
            ctx.Check(Mathf.IsEqualApprox(Combined(target), pristineCombined),
                $"an AI plane gets NO aim assist: every off-boresight round misses");
            ai.IsHumanPiloted = true;
            Step(180);
            ctx.Check(Combined(target) < pristineCombined,
                $"the same geometry flagged human is assisted onto the target moved={pristineCombined - Combined(target):0.##}");

            // Ranked acquisition on a hostile pair at equal geometry: the human-piloted target carries the
            // decoded 0.7 base weight and out-ranks the AI rival, a primary_target assignment overrides the
            // ranking, and a candidate beyond the activation radius scores the engine's 1e21 and is never picked.
            ai.IsHumanPiloted = false;
            ai.AutoFire = false;
            pilot.Gunner = gunner;
            gunner.AutoTarget = true;
            gunner.Target = null;
            target.Respawn();
            target.PlaceHeld(targetPos, targetPos + Vector3.Forward);
            var shooterPos = targetPos + new Vector3(0f, 0f, 800f);
            ai.PlaceHeld(shooterPos, targetPos);
            // Same 800 m ring, 14.5° off the shooter's nose, same altitude, nose away.
            var rivalPos = targetPos + new Vector3(200f, 0f, 800f - Mathf.Sqrt(800f * 800f - 200f * 200f));
            var rivalModel = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            rival = new FlightController
            {
                PlaneModel = rivalModel,
                Collider = PlaneCollider.Build(rivalModel),
                PlayerIndex = FlightRoster.ShooterIdBase + 1,
                IsHumanPiloted = false,
                Projectiles = live,
                UseKeyboard = false,
                PadDevices = System.Array.Empty<int>(),
                AllowPause = false,
            };
            rival.AddChild(rivalModel);
            rival.Setup(new FlightModel(stats), null, new CamParams(), rivalPos,
                rivalPos + Vector3.Forward);
            rival.Name = "rival_hostile";
            ctx.Host.AddChild(rival);
            rival.PlaceHeld(rivalPos, rivalPos + Vector3.Forward);

            Step(1);
            ctx.Check(ReferenceEquals(gunner.Target, target),
                $"equal geometry: the player's 0.7 weight out-ranks the AI rival (360 rank units)");
            gunner.Target = null;
            gunner.PrimaryTargetName = "rival_hostile";
            Step(1);
            ctx.Check(ReferenceEquals(gunner.Target, rival),
                $"an assigned primary_target overrides the ranking");
            gunner.Target = null;
            gunner.PrimaryTargetName = "player";
            Step(1);
            ctx.Check(ReferenceEquals(gunner.Target, target),
                $"primary_target 'player' resolves to the human-piloted aircraft");

            // With two humans in the scan, 'player' is a ROLE and resolves to whichever human is NEAREST this
            // attacker, not the first registered; the rival registered second, so pulling the pick across is
            // what proves distance decides. PlaceHeld is legitimate here: this branch queries no physics space.
            rival.IsHumanPiloted = true;
            var nearRivalPos = targetPos + new Vector3(0f, 0f, 500f); // 300 m from the shooter
            rival.PlaceHeld(nearRivalPos, nearRivalPos + Vector3.Forward);
            gunner.Target = null;
            Step(1);
            ctx.Check(ReferenceEquals(gunner.Target, rival),
                $"two humans: 'player' takes the NEARER one (300 m) over the first registered (800 m)");
            var nearTargetPos = targetPos + new Vector3(0f, 0f, 700f); // 100 m from the shooter
            target.PlaceHeld(nearTargetPos, nearTargetPos + Vector3.Forward);
            gunner.Target = null;
            Step(1);
            ctx.Check(ReferenceEquals(gunner.Target, target),
                $"swapping which human is nearer swaps the pick (100 m beats 300 m)");
            rival.IsHumanPiloted = false;
            rival.PlaceHeld(rivalPos, rivalPos + Vector3.Forward);
            target.PlaceHeld(targetPos, targetPos + Vector3.Forward);

            gunner.PrimaryTargetName = null;
            gunner.Target = null;
            target.PlaceHeld(targetPos + new Vector3(0f, 0f, -2500f),
                targetPos + new Vector3(0f, 0f, -2600f));
            Step(1);
            ctx.Check(ReferenceEquals(gunner.Target, rival),
                $"beyond the activation radius the player scores 1e21 and the rival is picked");
        }
        finally
        {
            pool?.Free();
            target?.Free();
            ai?.Free();
            rival?.Free();
            textures.Dispose();
        }
    }

    // The mode machine on a real flying AI rig. The reaction rolls are scripted by pinning the shipped
    // chance fields to 0/1 while the machine's own rng stays seeded, and the steady-hand entry rides a
    // REAL projectile hit through TakeProjectileHit, the same call the pool makes, so the hit-path
    // wiring is what is exercised rather than the machine API. The plane may genuinely fly between
    // phases: no phase here queries physics at a flown-to position (INSTR-13).
    internal static void AiModes(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var skills = AiSkills.Load(ctx.ZrdrPath);
        var library = Maneuvers.Load(ctx.ZrdrPath);
        WeaponDef? gun = weapons.All.FirstOrDefault(w =>
            w.IsGun && w.ArmorDamage is > 0f && w.HealthDamage is > 0f);
        ctx.Check(gun != null, $"a gun with authored damage exists in the data");
        if (gun == null)
            return;

        // The shipped range gates the machine runs on.
        ctx.Check(Mathf.IsEqualApprox(skills.MinAiActiveDist, 2000f),
            $"player.json min_ai_active_dist is the decoded 2000 m got={skills.MinAiActiveDist:0}");
        ctx.Check(Mathf.IsEqualApprox(stats.AiAttackRange, 2000f)
            && Mathf.IsEqualApprox(stats.AiReturnRange, 1200f),
            $"vehicle.json attack/return_range are the decoded 2000/1200 m got={stats.AiAttackRange:0}/{stats.AiReturnRange:0}");

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? target = null;
        FlightController? ai = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            // The hostile: a parked human-index rig the machine can activate against.
            var targetPos = new Vector3(0f, 500f, 0f);
            var targetModel = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            target = new FlightController
            {
                PlaneModel = targetModel,
                Collider = PlaneCollider.Build(targetModel),
                PlayerIndex = 0,
                Projectiles = live,
                UseKeyboard = false,
                PadDevices = System.Array.Empty<int>(),
                AllowPause = false,
            };
            target.AddChild(targetModel);
            target.Setup(new FlightModel(stats), ctx.Camera, new CamParams(), targetPos,
                targetPos + Vector3.Forward);
            ctx.Host.AddChild(target);
            target.PlaceHeld(targetPos, targetPos + Vector3.Forward);

            // The AI: pilot + gunner + machine, spawned OUTSIDE the activation radius flying
            // straight at the hostile. The terrain probe is injected before the controller can
            // wire its own, so avoid-crash is a scripted flag here.
            bool terrainBlocked = false;
            var machine = new AiModeMachine(new System.Random(11))
            {
                ActivationRange = skills.MinAiActiveDist,
                AttackRange = stats.AiAttackRange,
                ReturnRange = stats.AiReturnRange,
                SteadyHandChance = 0f, // scripted per phase; approach fire reactions off
                SixthSenseChance = 1f,
                StunRecoveryIntervalS = skills.At("stun_recovery_interval", 5),
                NaturalTouch = 1,
                Library = library,
                ProbeBlocked = (_, _) => terrainBlocked ? "suite/terrain" : null,
            };
            var transitions = new List<string>();
            machine.ModeChanged += (from, to, _) =>
                transitions.Add($"{AiModeMachine.NameOf(from)}>{AiModeMachine.NameOf(to)}");
            string? lastRoll = null;
            machine.RollLogged += line => lastRoll = line;

            var aiPos = targetPos + new Vector3(0f, 0f, 2600f); // outside the 2000 m radius
            var pilot = AiPilot.HoldingCourse(aiPos, targetPos);
            pilot.Gunner = new AiGunner(new RandomNumberGenerator { Seed = 20260813 })
            {
                DeadEyeAngleDeg = skills.DeadEyeAngleDeg(5),
                QuickDrawAngleDeg = skills.QuickDrawAngleDeg(5),
            };
            pilot.Machine = machine;
            var aiModel = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            ai = new FlightController
            {
                PlaneModel = aiModel,
                Collider = PlaneCollider.Build(aiModel),
                Damage = new PlaneDamage(stats.DestroyableParts),
                PlayerIndex = FlightRoster.ShooterIdBase,
                IsHumanPiloted = false,
                Pilot = pilot,
                Projectiles = live,
                UseKeyboard = false,
                PadDevices = System.Array.Empty<int>(),
                AllowPause = false,
            };
            ai.AddChild(aiModel);
            ai.Loadout = Loadout.Bind(StockLoadouts.Load().All.Values
                .First(d => d.Model == ctx.PlaneName), aiModel, weapons);
            ai.Setup(new FlightModel(stats), null, new CamParams(), aiPos, targetPos);
            ctx.Host.AddChild(ai);

            void Step(int frames)
            {
                for (int i = 0; i < frames; i++)
                {
                    ai!.SimStep(1f / 60f);
                    live.SimStep(1f / 60f);
                }
            }
            float Dist() => ai!.WorldPosition.DistanceTo(targetPos);

            // --- activation: patrol until the approach carries it inside the radius, then
            // pursue, announced in the decoded vocabulary.
            ctx.Check(machine.Mode == AiMode.Patrol, $"the machine starts on patrol");
            Step(1);
            ctx.Check(machine.Mode == AiMode.Patrol && Dist() > 2000f,
                $"outside min_ai_active_dist it stays on patrol d={Dist():0} m");
            int budget = 60 * 60;
            while (machine.Mode == AiMode.Patrol && budget-- > 0)
                Step(1);
            ctx.Check(machine.Mode == AiMode.Pursue,
                $"the approach activates it into pursue d={Dist():0} m");
            ctx.Check(Dist() <= 2010f, $"…at the activation radius, not before d={Dist():0} m");
            ctx.Check(transitions.Contains("patrol>pursue"),
                $"…logged as patrol>pursue transitions=[{string.Join(" ", transitions)}]");

            // --- a scripted FAILED steady-hand roll on a real projectile hit: the decoded
            // reaction, through the same TakeProjectileHit the pool calls.
            machine.SteadyHandChance = 1f;
            ai.TakeProjectileHit(gun, ai.WorldPosition + new Vector3(2f, 0f, 0f), "fuselage", 0);
            ctx.Check(lastRoll != null && lastRoll.Contains("steady hand test failed. Evading."),
                $"the hit rolls steady hand in the engine's vocabulary roll={lastRoll}");
            ctx.Check(machine.Mode == AiMode.EvasiveManeuver && machine.Executor != null,
                $"the failed test breaks off into an evasive maneuver mode={AiModeMachine.NameOf(machine.Mode)}");
            var flown = machine.Executor?.Maneuver;
            ctx.Check(flown != null && flown.EligibleFor(machine.NaturalTouch),
                $"…an eligible library entry name={flown?.Name} difficulty={flown?.Difficulty}/{machine.NaturalTouch}");
            machine.SteadyHandChance = 0f; // stray hits must not re-trigger mid-phase

            // --- the maneuver plays to Done and the machine returns to a flyable mode.
            budget = 60 * 60;
            while (machine.Mode == AiMode.EvasiveManeuver && budget-- > 0)
                Step(1);
            ctx.Check(machine.Mode is AiMode.Pursue or AiMode.Patrol,
                $"the maneuver returns to the prior mode mode={AiModeMachine.NameOf(machine.Mode)}");
            ctx.Check(machine.Executor == null, $"…and the executor is released");

            // --- a scripted FAILED sixth-sense roll stuns: gunner silent, then recovery after
            // stun_recovery_interval (the shipped value at rating 5).
            machine.Enter(AiMode.Pursue, "test: rejoin");
            machine.SixthSenseChance = 0f;
            machine.NotifyTargetEvaded();
            ctx.Check(machine.Mode == AiMode.Stunned,
                $"a failed sixth-sense roll stuns roll={lastRoll}");
            ctx.Check(lastRoll != null && lastRoll.Contains("Sixth sense test failed; AI now stunned."),
                $"…in the engine's vocabulary");
            Step(6);
            ctx.Check(!pilot.Gunner.WantsFire, $"the gunner is silent while stunned");
            float stunS = machine.StunRecoveryIntervalS;
            Step((int)(stunS * 60f) - 30);
            ctx.Check(machine.Mode == AiMode.Stunned,
                $"still stunned inside stun_recovery_interval ({stunS:0.0} s)");
            Step(60);
            ctx.Check(machine.Mode == AiMode.Pursue,
                $"…and recovered to the prior mode after it mode={AiModeMachine.NameOf(machine.Mode)}");

            // --- avoid crash: a blocked probe overrides with a climb-out, a cleared one
            // releases back. Stepped past ProbeIntervalMaxS, since the probe's cadence is the
            // original's per-plane 0.5…1.0 s draw and this plane's is unknown to the test.
            machine.SixthSenseChance = 1f;
            float yBefore = ai.WorldPosition.Y;
            terrainBlocked = true;
            Step((int)(AiModeMachine.ProbeIntervalMaxS * 60f) + 6);
            ctx.Check(machine.Mode == AiMode.AvoidCrash,
                $"a blocked terrain probe takes the mode mode={AiModeMachine.NameOf(machine.Mode)}");
            ctx.Check(machine.ClimbOutAltitude > yBefore,
                $"…ordering a climb-out to {machine.ClimbOutAltitude:0} m from {yBefore:0} m");
            Step(240);
            ctx.Check(ai.WorldPosition.Y > yBefore,
                $"the plane is climbing out y={ai.WorldPosition.Y:0} from {yBefore:0}");
            terrainBlocked = false;
            Step(120);
            ctx.Check(machine.Mode != AiMode.AvoidCrash,
                $"a cleared probe releases the override mode={AiModeMachine.NameOf(machine.Mode)}");

            // --- lay off (the rubber-band assist). Entry A/B on fixed geometry through
            // the machine's own tick: a chasing human 600 m dead astern enters lay off with
            // the assist on, and never with --no-assist's switch off.
            machine.Enter(AiMode.Pursue, "test: rejoin for lay off");
            // Pursue's own lever, to ease off FROM. ⚠ Not a fixed number: the law walks the lever
            // toward its desired speed, so what pursue is commanding here depends on how fast the
            // plane happens to be after the climb-out above.
            float leverPursuing = pilot.Throttle;
            var aiPos2 = ai.WorldPosition;
            var ownVel = new Vector3(0f, 0f, -100f);               // flying -Z
            var pursuerPos = aiPos2 + new Vector3(0f, 0f, 600f);   // 600 m dead astern
            var pursuerVel = new Vector3(0f, 0f, -80f);            // giving chase
            // Entry needs the geometry SUSTAINED (LayOffSustainS), so both arms tick through
            // the window; one passing frame must not enter (the misfire regression).
            int sustainFrames = (int)(AiModeMachine.LayOffSustainS * 60f) + 2;
            machine.AssistEnabled = false;
            for (int i = 0; i < sustainFrames; i++)
                machine.Update(aiPos2, ownVel, pursuerPos, null, 1f / 60f, pursuerVel, targetIsHuman: true);
            ctx.Check(machine.Mode == AiMode.Pursue,
                $"--no-assist: the same pursued geometry never enters lay off mode={AiModeMachine.NameOf(machine.Mode)}");
            machine.AssistEnabled = true;
            machine.Update(aiPos2, ownVel, pursuerPos, null, 1f / 60f, pursuerVel, targetIsHuman: true);
            ctx.Check(machine.Mode == AiMode.Pursue,
                $"one passing frame does not enter lay off mode={AiModeMachine.NameOf(machine.Mode)}");
            for (int i = 0; i < sustainFrames; i++)
                machine.Update(aiPos2, ownVel, pursuerPos, null, 1f / 60f, pursuerVel, targetIsHuman: true);
            ctx.Check(machine.Mode == AiMode.LayOff,
                $"assist on: a chasing human fallen 600 m behind enters lay off mode={AiModeMachine.NameOf(machine.Mode)}");
            ctx.Check(transitions.Contains("pursue>lay off"),
                $"…logged as pursue>lay off transitions=[{string.Join(" ", transitions)}]");

            // The observable assist, through the live pilot: the throttle eases off pursue's own
            // lever and the gunner's trigger is held for the whole dwell.
            Step(30);
            ctx.Check(machine.Mode == AiMode.LayOff,
                $"the anti-chatter hold keeps the mode mode={AiModeMachine.NameOf(machine.Mode)}");
            ctx.Check(pilot.Throttle < leverPursuing,
                $"the throttle is eased off pursue's own lever throttle={pilot.Throttle:0.000} from {leverPursuing:0.000}");
            ctx.Check(!pilot.Gunner.WantsFire, $"fire is held while laying off");

            // Once the hold expires the machine releases back to pursue and the lever runs up to its ceiling.
            // ⚠ Do not pin an exact throttle: pursue assigns no lever, AiControlLaw walks it at 0.35/s toward
            // its own desired speed, so an exact value would pin the settling transient instead of the release.
            Step(150);
            ctx.Check(machine.Mode == AiMode.Pursue,
                $"a non-pursuing target releases lay off after the hold mode={AiModeMachine.NameOf(machine.Mode)}");

            ctx.Note($"transitions: {string.Join(" ", transitions)}");
        }
        finally
        {
            pool?.Free();
            target?.Free();
            ai?.Free();
            textures.Dispose();
        }
    }

    // The voice runtime against the real soundsh archive, reproducing the session's own lifecycle in
    // order: resolve the chain, prewarm that one pilot's clips, retire the loader as WorldSession.Build
    // does, then prove a resolved line still plays while a def never prewarmed returns null. The
    // source-following one-shot is asserted by position only; audibility is the user's half
    // (docs/verification.md). Closes with the measured cost of prewarming the entire voice bank, the
    // number that justifies the roster-subset choice.
    internal static void VoiceRuntime(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"shared zrdr");
        ctx.RequireData(ctx.SoundsPath, $"sound archive (soundsh)");
        var defs = SoundDefs.Load(ctx.ZrdrPath);
        var groups = SoundDefs.LoadGroups(ctx.ZrdrPath);
        var voice = new CombatVoice(defs, groups, CombatVoice.LoadAccents(ctx.ZrdrPath));
        ctx.Same(35, voice.AccentIds.Count, $"voice.zrd accent rows");

        using var archive = new SoundArchive(ctx.SoundsPath);
        WorldSounds? sounds = null;
        Node3D? mover = null;
        try
        {
            sounds = new WorldSounds(defs, groups)
            {
                Loader = (d, warn) => archive.Find(d.WavName, d.Looped, warn),
            };
            ctx.Host.AddChild(sounds);

            // The chain's worked example: accent 12 is a single-id pool, VO id 2 (the pilot with
            // the full bearing set). Prewarm that pilot exactly as a mission roster would.
            int? pilot = voice.PilotFor(12, new System.Random(1));
            ctx.Check(pilot == 2, $"accent 12 resolves to VO id 2 got={pilot?.ToString() ?? "null"}");
            var subset = voice.PrewarmNames(new[] { 12 });
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int decoded = sounds.Prewarm(subset);
            sw.Stop();
            ctx.Check(decoded >= 70, $"the accent-12 subset decodes names={subset.Count} decoded={decoded}");
            ctx.Note($"accent-12 prewarm: {subset.Count} defs, {decoded} streams, {sw.ElapsedMilliseconds} ms");
            sounds.Loader = null;   // the session's build scope closing (WorldSession.Build)

            string? playable = voice.PlayableFor(2, "DI-LowDmg");
            ctx.Check(playable == "snd_DI-LowDmg-A_id2_random",
                $"DI-LowDmg resolves to the shipped variant group got={playable}");
            string? bearing = voice.PlayableForTrigger(2, 6);   // WA-Enemy-3H
            ctx.Check(bearing != null && sounds.HasStream(bearing),
                $"the bearing clip's stream survived the loader retirement name={bearing}");

            // A prewarmed line plays from a MOVING source and tracks it across ticks.
            mover = new Node3D();
            ctx.Host.AddChild(mover);
            mover.GlobalPosition = new Vector3(100f, 200f, 300f);
            string? resolved = sounds.PlayOneShot(playable!, mover, new System.Random(2));
            ctx.Check(resolved != null && resolved.StartsWith("snd_id2_DI-LowDmg"),
                $"a prewarmed voice line plays after the archive closed resolved={resolved}");
            var player = LastOneShotPlayer(sounds);
            ctx.Check(player != null && player.GlobalPosition.DistanceTo(mover.GlobalPosition) < 0.01f,
                $"the one-shot starts at its source pos={player?.GlobalPosition}");
            mover.GlobalPosition = new Vector3(-450f, 60f, 1200f);
            sounds.Tick();
            ctx.Check(player!.GlobalPosition.DistanceTo(mover.GlobalPosition) < 0.01f,
                $"the one-shot follows the moved source pos={player.GlobalPosition}");
            var lastPos = mover.GlobalPosition;
            mover.Free();
            mover = null;
            sounds.Tick();
            ctx.Check(GodotObject.IsInstanceValid(player) && player.GlobalPosition.DistanceTo(lastPos) < 0.01f,
                $"a freed source leaves the line finishing at its last position");

            // The positional overload is untouched, and a def never prewarmed is null once the
            // loader is gone: the exact failure the prewarm exists to prevent.
            ctx.Check(sounds.PlayOneShot("snd_id26_TA-SucShk-A", Vector3.Zero, new System.Random(3)) == null,
                $"an unprewarmed pilot's line stays null after the archive closed");

            // The cost of prewarm-everything, measured on a fresh archive so nothing is cached:
            // the number the roster-subset strategy is justified against.
            using var fresh = new SoundArchive(ctx.SoundsPath);
            long bytes = 0;
            int ok = 0, absent = 0;
            sw.Restart();
            foreach (var name in voice.AllClipNames())
            {
                var def = defs[name];
                if (fresh.Find(def.WavName, def.Looped, warn: false) is { } stream)
                {
                    ok++;
                    bytes += stream.Data.Length;
                }
                else
                {
                    absent++;
                }
            }
            sw.Stop();
            ctx.Note($"full voice bank: {voice.AllClipNames().Count} defs, {ok} decoded ({absent} defs without a WAV), {bytes / (1024.0 * 1024.0):0.0} MB PCM, {sw.ElapsedMilliseconds} ms; why the session prewarms the roster subset");
        }
        finally
        {
            mover?.Free();
            if (sounds != null)
            {
                sounds.FlushOneShots();
                sounds.Free();
            }
        }
    }

    // The E16 dispatch on a live AI aircraft, over the same B8 lifecycle the session
    // runs (prewarm the accent's clips, retire the loader, play after the archive is closed).
    // The talker chance is pinned to 1 so the assertions are about the dispatch rules, not the
    // dice; audibility itself is the user's half (docs/verification.md, "What this project
    // cannot verify itself") — what IS assertable is the dispatch decision, the resolved clip
    // name and the PlayOneShot call.
    internal static void AiVoice(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(ctx.SoundsPath, $"sound archive (soundsh)");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var defs = SoundDefs.Load(ctx.ZrdrPath);
        var groups = SoundDefs.LoadGroups(ctx.ZrdrPath);
        var voice = new CombatVoice(defs, groups, CombatVoice.LoadAccents(ctx.ZrdrPath));
        WeaponDef? gun = weapons.All.FirstOrDefault(w =>
            w.IsGun && w.ArmorDamage is > 0f && w.HealthDamage is > 0f);
        ctx.Check(gun != null && stats.DestroyableParts.Count > 0,
            $"a damaging gun and destroyable parts exist in the data");
        if (gun == null)
            return;

        var textures = new TextureArchive(texturesPath);
        using var archive = new SoundArchive(ctx.SoundsPath);
        WorldSounds? sounds = null;
        Session.AiVoiceRuntime? runtime = null;
        FlightController? ai = null;
        try
        {
            // The session lifecycle: prewarm accent 12's pilot (VO id 2, the full clip set),
            // then retire the loader as WorldSession.Build does.
            sounds = new WorldSounds(defs, groups)
            {
                Loader = (d, warn) => archive.Find(d.WavName, d.Looped, warn),
            };
            ctx.Host.AddChild(sounds);
            sounds.Prewarm(voice.PrewarmNames(new[] { 12 }));
            sounds.Loader = null;

            runtime = new Session.AiVoiceRuntime(voice, sounds, new System.Random(5));
            ctx.Host.AddChild(runtime);

            var aiModel = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            ai = new FlightController
            {
                PlaneModel = aiModel,
                Collider = PlaneCollider.Build(aiModel),
                Damage = new PlaneDamage(stats.DestroyableParts),
                PlayerIndex = FlightRoster.ShooterIdBase,
                IsHumanPiloted = false,
                Pilot = new AiPilot(),
                UseKeyboard = false,
                PadDevices = System.Array.Empty<int>(),
                AllowPause = false,
            };
            ai.AddChild(aiModel);
            var aiPos = new Vector3(0f, 500f, 0f);
            ai.Setup(new FlightModel(stats), null, new CamParams(), aiPos, aiPos + Vector3.Forward);
            ctx.Host.AddChild(ai);

            // Talker chance pinned to 1: every roll passes, so a silent trigger is a GATE
            // decision (cooldown, aliveness), never luck.
            runtime.RegisterAi(ai, accentId: 12, talkerChance: 1f, constitutionChance: 1f);
            var speaker = runtime.Dispatcher.Find(ai.PlayerIndex);
            ctx.Check(speaker is { VoId: 2 },
                $"accent 12 registers the AI as VO id 2 got={speaker?.VoId.ToString() ?? "none"}");
            if (speaker == null)
                return;

            // An inert aircraft is registered as a speaker but is not eligible: the runtime mirrors InPlay into
            // the dispatcher's own aliveness gate, the only thing here that can see a FlightController. Checked
            // in both directions, so "not eligible" cannot be the flag's resting state.
            ctx.Check(speaker.Alive, $"baseline: a live AI is an eligible speaker");
            ai.Inert = true;
            ctx.Check(!speaker.Alive, $"going inert takes the speaker out of the broadcast election");
            ai.Inert = false;
            ctx.Check(speaker.Alive, $"…and activation puts it back");

            var played = new List<(int Trigger, string Clip)>();
            runtime.LinePlayed += (_, trigger, clip) => played.Add((trigger, clip));
            runtime.Step(3f); // past the decoded 2 s mute window

            // --- DI: hits through the pool's own entry point until the summary crosses 70 %.
            // The hits WALK the four zones (a single zone's exhausted pool floors the summary
            // at 75 % on this airframe and could never cross the threshold).
            var struck = new (string Part, Vector3 Offset)[]
            {
                ("wing", new Vector3(-2f, 0f, 0f)), ("wing", new Vector3(2f, 0f, 0f)),
                ("fuselage", new Vector3(0f, 0f, -2f)), ("fuselage", new Vector3(0f, 0f, 2f)),
            };
            int budget = 400;
            while (ai.Damage!.SummaryHealthFraction >= 0.7f && budget-- > 0)
            {
                var (part, offset) = struck[budget % struck.Length];
                ai.TakeProjectileHit(gun, ai.WorldPosition + offset, part, 0);
            }
            float fraction = ai.Damage.SummaryHealthFraction;
            ctx.Check(fraction is < 0.7f and > 0.3f,
                $"the sweep stopped inside the DI band health={fraction * 100f:0}%");
            ctx.Check(played.Count == 1 && played[0].Clip.StartsWith("snd_id2_DI-"),
                $"crossing the threshold played exactly one DI line of the pilot's own played=[{string.Join(", ", played)}]");
            var oneShot = LastOneShotPlayer(sounds);
            ctx.Check(oneShot != null && oneShot.GlobalPosition.DistanceTo(ai.WorldPosition) < 1f,
                $"…as a source-following one-shot at the aircraft pos={oneShot?.GlobalPosition}");

            // Follow-up hits in the same tier stay silent: the slot cooldown swallowed them
            // (armed by the PLAY here; the failed-roll arming is the unit suite's,
            // AiVoiceDispatcherTests).
            int before = played.Count;
            static int Tier(float f) => f < 0.3f ? 3 : f < 0.5f ? 2 : f < 0.7f ? 1 : 0;
            int tier = Tier(ai.Damage.SummaryHealthFraction);
            ai.TakeProjectileHit(gun, ai.WorldPosition, "fuselage", 0, damageScale: 0.02f);
            if (Tier(ai.Damage.SummaryHealthFraction) == tier)
            {
                ctx.Check(played.Count == before,
                    $"a follow-up hit in the same tier is silent under the 15 s cooldown");
            }

            // --- the kill: the dying pilot's own cry, dispatched with force (the speaker is
            // already dead when it plays).
            ai.DebugForceCrash();
            ctx.Check(!speaker.Alive, $"the Downed report marked the speaker dead");
            ctx.Check(played.Count >= before + 1 && played[^1].Clip.StartsWith("snd_id2_DE-"),
                $"…and the death cry played THROUGH the dead state (force) clip={(played.Count > 0 ? played[^1].Clip : "none")}");
            ctx.Check(played[^1].Trigger == AiVoiceDispatcher.DeEnemy,
                $"…as id 21 (DE): no team model puts an AI on the player's team, documented");

            // The force flag is the death cry's alone: an ordinary dispatch on the same dead
            // speaker is gated out before anything rolls.
            var unforced = runtime.Dispatcher.Dispatch(ai.PlayerIndex,
                AiVoiceDispatcher.TaSucShk, runtime.Now);
            ctx.Check(unforced.Clip == null && unforced.Outcome == "speaker dead",
                $"an unforced dispatch on the dead speaker is refused outcome={unforced.Outcome}");

            ctx.Note($"lines: {string.Join(", ", played)}");
        }
        finally
        {
            ai?.Free();
            runtime?.Free();
            if (sounds != null)
            {
                sounds.FlushOneShots();
                sounds.Free();
            }
            textures.Dispose();
        }
    }

    // The most recent one-shot player under a WorldSounds node.
    internal static AudioStreamPlayer3D? LastOneShotPlayer(WorldSounds sounds)
    {
        AudioStreamPlayer3D? last = null;
        foreach (var child in sounds.GetChildren())
        {
            if (child is AudioStreamPlayer3D p)
            {
                last = p;
            }
        }
        return last;
    }

    internal static void AiNetFollow(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, "C1");
        ctx.RequireData(chapterZrdr, $"C1 zrdr");

        // The chapter graph resolves both ways the data references it: by id (aiv field 0)
        // and by neindex name (egen/zeppelins/objectives), case-insensitively.
        var nets = AiNets.Load(chapterZrdr);
        var byId = AiNets.ById(nets, 10);
        ctx.Check(byId is { Name: "M4ReinfAce" }, $"net id 10 resolves to M4ReinfAce");
        var byName = AiNets.ByName(nets, "m4reinface");
        ctx.Check(byId != null && ReferenceEquals(byId, byName),
            $"the name lookup (case-insensitive) finds the same net");
        ctx.Check(byId != null && ReferenceEquals(byId, AiNets.Resolve(nets, "10"))
            && ReferenceEquals(byId, AiNets.Resolve(nets, "M4ReinfAce")),
            $"Resolve takes either spelling");
        if (byId == null)
            return;
        var net = byId;

        // The trailer names the player at node 10, and that node carries no edge: the shipped
        // shape of all 76 anchored nets, and why the seat scan has to skip edgeless nodes.
        ctx.Check(net.Trailer is { NodeIndex: 10, Name: "player" },
            $"the trailer [10, player] rides the net trailer={net.Trailer?.ToString() ?? "-"}");
        bool anchorEdgeless = true;
        foreach (var (a, b) in net.Edges)
        {
            if (a == 10 || b == 10)
                anchorEdgeless = false;
        }
        ctx.Check(anchorEdgeless, $"…and the anchor node 10 is parked off the ring, edgeless");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var textures = new TextureArchive(texturesPath);
        FlightController? ai = null;
        try
        {
            // The ai-actor pattern: a pilot-driven controller, no camera, no HUD, no devices,
            // spawned on the net's first node, its follower seeded with a fixed value so the
            // route repeats.
            var spawn = net.Nodes[0].Position;
            var look = net.Nodes[1].Position;
            var pilot = AiPilot.HoldingCourse(spawn, look);
            var follower = new AiNetFollower(net, new System.Random(1));
            pilot.Patrol = follower;
            var aiModel = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            ai = new FlightController
            {
                PlaneModel = aiModel,
                Collider = PlaneCollider.Build(aiModel),
                PlayerIndex = FlightRoster.ShooterIdBase,
                IsHumanPiloted = false,
                Pilot = pilot,
                UseKeyboard = false,
                PadDevices = System.Array.Empty<int>(),
                AllowPause = false,
            };
            ai.AddChild(aiModel);
            ai.Setup(new FlightModel(stats), null, new CamParams(), spawn, look);
            ctx.Host.AddChild(ai);

            // Fly until the follower has captured five nodes (or the budget runs out), logging
            // each target change so a hop off the edge list cannot hide.
            var hops = new List<(int From, int To)>();
            int last = follower.CurrentIndex;
            const int wanted = 5;
            int steps = 0, budget = 120 * 60;
            while (follower.Advances < wanted && steps < budget)
            {
                steps++;
                ai.SimStep(1f / 60f);
                if (follower.CurrentIndex != last)
                {
                    if (last >= 0)
                        hops.Add((last, follower.CurrentIndex));
                    last = follower.CurrentIndex;
                }
            }
            ctx.Check(follower.Advances >= wanted,
                $"the plane captures {wanted} net nodes advances={follower.Advances} in {steps / 60f:0} s of sim");
            bool allEdges = hops.Count > 0;
            foreach (var (from, to) in hops)
            {
                if (!net.Edges.Contains((from, to)) && !net.Edges.Contains((to, from)))
                    allEdges = false;
            }
            ctx.Check(allEdges,
                $"every hop is an EDGE of the graph hops={string.Join(" ", hops.ConvertAll(h => $"{h.From}→{h.To}"))}");
            ctx.Check(Mathf.Abs(ai.WorldPosition.Y - 400f) < 250f,
                $"…within the placeholder law's altitude leash of the net's 400 m y={ai.WorldPosition.Y:0}");

            // The same net, now RIDING a stand-in for the player 6 km east of where it
            // was authored. The ring must move with it and keep its authored 400 m; the plane
            // must fly the moved ring, not the one in the file.
            var trailed = net.Nodes[10].Position + new Vector3(6000f, -300f, 0f);
            var anchoredFollower = new AiNetFollower(net, new System.Random(1),
                trailerTarget: () => trailed);
            ctx.Check(anchoredFollower.Anchored
                && anchoredFollower.NodePosition(0).IsEqualApprox(
                    net.Nodes[0].Position + new Vector3(6000f, 0f, 0f)),
                $"the anchored net rides its target 6 km east, Y untouched node0={anchoredFollower.NodePosition(0)}");
            pilot.Patrol = anchoredFollower;
            ai.Activate(anchoredFollower.NodePosition(0), anchoredFollower.NodePosition(1));
            bool seatedOnAnchor = false;
            for (steps = 0; anchoredFollower.Advances < 3 && steps < budget; steps++)
            {
                ai.SimStep(1f / 60f);
                if (anchoredFollower.CurrentIndex == 10)
                    seatedOnAnchor = true;   // the edgeless anchor is never a flight target
            }
            ctx.Check(anchoredFollower.Advances >= 3 && !seatedOnAnchor,
                $"…and the plane laps the MOVED ring advances={anchoredFollower.Advances} in {steps / 60f:0} s of sim");
            float onMoved = ai.WorldPosition.DistanceTo(anchoredFollower.CurrentTarget);
            float onAuthored = ai.WorldPosition.DistanceTo(net.Nodes[anchoredFollower.CurrentIndex].Position);
            ctx.Check(onMoved < onAuthored,
                $"…flying the ridden ring, not the authored one moved={onMoved:0} m authored={onAuthored:0} m");
        }
        finally
        {
            ai?.Free();
            textures.Dispose();
        }
    }

    private sealed class FixedFlightStarts : IFlightStarts
    {
        public IReadOnlyList<FlightStart> ChooseStarts(IReadOnlyList<SpawnPoint>? spawns,
            string missionZrdrPath, int spawnBase, int playerCount)
        {
            var starts = new FlightStart[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                var position = new Vector3(i * 60f, 500f, 0f);
                starts[i] = new FlightStart(position, position + Vector3.Forward, 0.5f, 100f);
            }
            return starts;
        }
    }
}
