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

internal static class AiTargetingAndZeppelinSuites
{
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
            var inputs = new HumanFlightAdapter.Inputs
            {
                PlanesGamez = planesGamez,
                StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
                AiStatsFor = (plane, aiDef) => PlaneStats.LoadForAi(ctx.ZrdrPath, plane, aiDef),
                RigCount = 0,
                PaintRng = new RandomNumberGenerator(),
                ZrdrPath = ctx.ZrdrPath,
                StockLoadouts = StockLoadouts.Load(),
                WeaponDefs = weaponDefs,
                Textures = textures,
                Projectiles = live,
                Shakes = ShakeDefs.Load(ctx.ZrdrPath),
            };
            // worldEffects null!: never dereferenced — CrashProgram/WorldScene stay null, so the
            // spawner's crash-runtime block (its only reader) is skipped.
            var spawner = new FlightRoster(spec, liveries, null!, ctx.Host, inputs);

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
            ctx.Check(!subject.PlaneModel!.Visible, $"the inert aircraft is not drawn");

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
            ctx.Check(subject.PlaneModel.Visible, $"the activated aircraft is drawn");

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

                    // ⚠ Drive this the way Godot's physics tick does, with a realtime clock in Current. A runtime
                    // stepped only from GameSession.DriveSimSteps is inert on the realtime clock every real session
                    // uses, and every suite and golden runs fixed-step, which is exactly the blind spot (INSTR-14).
                    var savedClock = Utils.GameClock.Current;
                    Utils.GameClock.Current = new Utils.GameClock { Mode = Utils.GameClock.RunMode.Realtime };
                    int shotsBeforeRealtime = ZepShots();
                    for (int i = 0; i < 240; i++)
                    {
                        runtime._PhysicsProcess(1.0 / 60.0);
                        live.SimStep(1f / 60f);
                    }
                    Utils.GameClock.Current = savedClock;
                    ctx.Check(ZepShots() > shotsBeforeRealtime,
                        $"the runtime steps ITSELF on a realtime clock: {ZepShots() - shotsBeforeRealtime} more shot(s) with nobody calling SimStep");

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

    // TargetRef, entirely tree-free, which is the seam's whole claim. One ref per source kind: an
    // aircraft with both health pools, a sub-part with health alone, a turret emplacement with
    // neither. That spread is the point, since the turret's nulls are the case that must not
    // silently become 1.0.
    internal static void TargetRefModel(TestContext ctx)
    {
        int ownTeam = AimAssist.PlayerTeam;
        var plane = new object();  // stands in for the FlightController the collector hands back
        var engine = new object(); // a zeppelin engine's DestructibleRegistry.Instance
        var gun = new object();    // a TurretController

        // --- an enemy aircraft: the Kestrel screenshot's case ------------------------------------
        var planeCandidate = new AimCandidate
        {
            Position = new Vector3(120f, 300f, -640f),
            Velocity = new Vector3(0f, 0f, -90f),
            Team = AimAssist.PlayerTeam + 1,
            Live = true,
            Source = plane,
        };
        var kestrel = TargetRef.ForAircraft(planeCandidate, TargetClass.Enemy, "ai1_player_kestrel",
            "Kestrel", TargetRef.Fraction(70.2f, 90f), TargetRef.Fraction(91f, 100f));
        ctx.Check(kestrel.Position == planeCandidate.Position
                  && kestrel.Velocity == planeCandidate.Velocity
                  && kestrel.Team == planeCandidate.Team && kestrel.Live
                  && ReferenceEquals(kestrel.Source, plane),
            $"the aircraft ref forwards the wrapped candidate's pose, team, liveness and source");
        ctx.Check(kestrel.Kind == AimTargetKind.Vehicle && kestrel.Class == TargetClass.Enemy
                  && !kestrel.Objective && kestrel.Name == "ai1_player_kestrel"
                  && kestrel.DisplayName == "Kestrel",
            $"…and reads back its own pool, cycle, identity name '{kestrel.Name}' and the marker's own '{kestrel.DisplayName}' (--target= pins the node name, the marker prints the airframe)");
        ctx.Check(TargetRef.ForAircraft(planeCandidate, TargetClass.Enemy, "bandit").DisplayName
                  == "bandit",
            $"a source with no roster entry prints its own name rather than an empty label");
        ctx.Check(kestrel.CategoryLine.Length == 0,
            $"an ordinary aircraft carries neither label half, so line 1 is blank (the Kestrel shot shows line 2 alone) got='{kestrel.CategoryLine}'");
        ctx.Check(kestrel.Health is { } h && Mathf.IsEqualApprox(h, 0.78f)
                  && kestrel.Armor is { } a && Mathf.IsEqualApprox(a, 0.91f),
            $"health and armor read separately, never blended (decision 12's H78 A91) h={kestrel.Health:0.00} a={kestrel.Armor:0.00}");

        // --- a zeppelin sub-part: the C1 M04 objective, health but no armor ----------------------
        var engineCandidate = new AimCandidate
        {
            Position = new Vector3(-40f, 900f, 1200f),
            Velocity = new Vector3(6f, 0f, 0f),
            Team = AimAssist.WorldTeam,
            Live = true,
            Source = engine,
        };
        var promisedLand = TargetRef.ForStructure(engineCandidate, TargetClass.Enemy,
            "Promised Land", "Zeppelin", "Destroy", objective: true,
            TargetRef.Fraction(150f, 200f));
        ctx.Check(promisedLand.Kind == AimTargetKind.Structure && promisedLand.Objective
                  && promisedLand.Class == TargetClass.Enemy,
            $"a flagged sub-part is a Structure riding the ENEMY cycle with the objective companion set (the -too switch, which moves objectives to Non-Aircraft, is not ported)");
        ctx.Check(promisedLand.CategoryLine == "Zeppelin [Destroy] -"
                  && promisedLand.Name == "Promised Land",
            $"both label halves compose the original's '%s [%s] -' got='{promisedLand.CategoryLine}'");
        ctx.Check(promisedLand.Health is { } ph && Mathf.IsEqualApprox(ph, 0.75f)
                  && promisedLand.Armor == null,
            $"a structure has health and NO armor pool h={promisedLand.Health:0.00} a={promisedLand.Armor?.ToString("0.00") ?? "none"}");

        // --- a turret emplacement: no health model at all ----------------------------------------
        var gunCandidate = new AimCandidate
        {
            Position = new Vector3(500f, 12f, 500f),
            Velocity = Vector3.Zero,
            Team = AimAssist.PlayerTeam + 1,
            Live = false, // its healthy node was swapped out — the decoded permanent kill switch
            Source = gun,
        };
        var flak = TargetRef.ForTurret(gunCandidate, TargetClass.NonAircraft, "AA Emplacement");
        ctx.Check(flak.Kind == AimTargetKind.Turret && flak.Class == TargetClass.NonAircraft
                  && !flak.Live && flak.Name == "AA Emplacement",
            $"the turret ref carries its own kind and cycle and forwards a DEAD candidate's liveness rather than hiding it");
        ctx.Check(flak.Health == null && flak.Armor == null && flak.CategoryLine.Length == 0,
            $"a turret emplacement has neither pool, so both figures are omitted, never defaulted to full (decision 12's trap) h={flak.Health?.ToString() ?? "none"} a={flak.Armor?.ToString() ?? "none"}");
        ctx.Check(TargetRef.Fraction(50f, 0f) == null && TargetRef.Fraction(300f, 200f) == 1f,
            $"Fraction is the one place that decides 'no source, no figure', and it clamps");

        // --- the class model, FUN_004b5cd0's own order -------------------------------------------
        ctx.Check(TargetRef.Classify(AimTargetKind.Vehicle, live: true, ownTeam + 1, ownTeam)
                      == TargetClass.Enemy
                  && TargetRef.Classify(AimTargetKind.Vehicle, live: true, ownTeam, ownTeam)
                      == TargetClass.Ally
                  && TargetRef.Classify(AimTargetKind.Vehicle, live: true, AimAssist.NeutralTeam,
                      ownTeam) == TargetClass.Ally,
            $"the aircraft split: a different non-zero team is Enemy, the same team is Ally, and either side unaffiliated is Ally too");
        ctx.Check(TargetRef.Classify(AimTargetKind.Vehicle, live: true, ownTeam, ownTeam,
                      objectiveTarget: true) == TargetClass.Enemy
                  && TargetRef.Classify(AimTargetKind.Structure, live: true, AimAssist.WorldTeam,
                      ownTeam, otherTarget: true, objectiveTarget: true) == TargetClass.Enemy,
            $"objectiveTarget overrides everything below it, including an own-team aircraft and otherTarget on the same entity");
        ctx.Check(TargetRef.Classify(AimTargetKind.Structure, live: true, AimAssist.WorldTeam,
                      ownTeam, otherTarget: true) == TargetClass.NonAircraft
                  && TargetRef.Classify(AimTargetKind.Turret, live: true, ownTeam + 1, ownTeam,
                      otherTarget: true) == TargetClass.NonAircraft,
            $"otherTarget puts a structure or a turret on the Non-Aircraft cycle");
        ctx.Check(TargetRef.Classify(AimTargetKind.Turret, live: true, ownTeam + 1, ownTeam) == null
                  && TargetRef.Classify(AimTargetKind.Structure, live: true, AimAssist.WorldTeam,
                      ownTeam) == null,
            $"an UNFLAGGED turret or structure is not selectable at all — the mission decides, not the world (which is why DestructibleRegistry never feeds this pool)");
        ctx.Check(TargetRef.Classify(AimTargetKind.Vehicle, live: false, ownTeam + 1, ownTeam) == null
                  && TargetRef.Classify(AimTargetKind.Structure, live: false, AimAssist.WorldTeam,
                      ownTeam, objectiveTarget: true) == null,
            $"the liveness predicate runs FIRST, so a dead objective classifies to nothing");

        // --- identity is the source object, not the wrapper --------------------------------------
        var sameEngineLater = TargetRef.ForStructure(
            new AimCandidate { Position = Vector3.Up, Live = true, Source = engine },
            TargetClass.NonAircraft, "Promised Land");
        ctx.Check(promisedLand.IsSameTarget(sameEngineLater),
            $"a ref rebuilt next frame at a new pose still names the same target (FUN_004b6490 re-finds the selection by ENTITY — the wrappers are new objects every frame)");
        ctx.Check(!promisedLand.IsSameTarget(kestrel)
                  && !TargetRef.ForTurret(default, TargetClass.NonAircraft, "")
                      .IsSameTarget(TargetRef.ForTurret(default, TargetClass.NonAircraft, "")),
            $"two different sources never match, and a null source matches nothing — including another null, which would otherwise make every sourceless ref the same target");
    }

    /// <summary>The tap/hold decoding. The suite reads no gamepad and no bare key press, so what
    /// is pinned here is everything BETWEEN the device read and the
    /// action: <see cref="TapHoldButton"/>'s tap-versus-hold rule, and the attacker queue's live
    /// wiring through a real <see cref="FlightController.TakeProjectileHit"/> on real rigs in a real
    /// pool. The key and pad reads themselves are owed as live play.</summary>
    internal static void TargetInputModel(TestContext ctx)
    {
        // --- the tap/hold decision: pure, no device involved --------------------------------
        const float dt = 1f / 60f;
        (int Taps, int Holds) Press(TapHoldButton b, int downFrames)
        {
            int taps = 0, holds = 0;
            void Count(TapHold r)
            {
                if (r == TapHold.Tap)
                {
                    taps++;
                }
                else if (r == TapHold.Hold)
                {
                    holds++;
                }
            }

            for (int i = 0; i < downFrames; i++)
            {
                Count(b.Step(true, dt));
            }

            Count(b.Step(false, dt));
            return (taps, holds);
        }

        var btn = new TapHoldButton(0.25f);
        ctx.Check(btn.Step(false, dt) == TapHold.None,
            $"a button that is simply up reports nothing — a release with no press is not a tap");
        ctx.Check(Press(btn, 12) == (1, 0),
            $"a 0.20 s press taps once on RELEASE and never holds");
        ctx.Check(Press(btn, 18) == (0, 1),
            $"a 0.30 s press holds once and the release is then SPENT — it does not also tap, which is the flicker decision 7 exists to avoid");
        ctx.Check(Press(btn, 120) == (0, 1),
            $"holding for two seconds still fires exactly once — this is a tap/hold split, not a repeat");
        ctx.Check(Press(btn, 6) == (1, 0),
            $"and the next press taps again, so a hold leaves no state behind");

        // --- the attacker queue, wired through a real hit ------------------------------------
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var gun = weapons.All.FirstOrDefault(w => w.IsGun && w.ArmorDamage is > 0f);
        ctx.Check(gun != null, $"a gun with ARMOR_DAMAGE exists in the data");
        if (gun == null)
        {
            return;
        }

        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? self = null;
        FlightController? hostile = null;
        FlightController? friendly = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            FlightController BuildRig(int playerIndex, int team, Vector3 pos)
            {
                var model = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
                var rig = new FlightController
                {
                    PlaneModel = model,
                    Collider = PlaneCollider.Build(model),
                    Damage = new PlaneDamage(stats.DestroyableParts),
                    PlayerIndex = playerIndex,
                    Team = team,
                    Projectiles = live,
                    UseKeyboard = false,
                    AllowPause = false,
                };
                rig.AddChild(model);
                rig.Setup(new FlightModel(stats), ctx.Camera, new CamParams(), pos, pos + Vector3.Forward);
                ctx.Host.AddChild(rig);
                return rig;
            }

            self = BuildRig(0, AimAssist.PlayerTeam, Vector3.Zero);
            friendly = BuildRig(1, AimAssist.PlayerTeam, new Vector3(0f, 0f, -200f));
            hostile = BuildRig(2, InstantActionRuntime.EnemyTeam, new Vector3(0f, 0f, -400f));
            self.Targeting = new TargetSelection();

            ctx.Check(ReferenceEquals(live.RigOfShooter(2), hostile)
                      && ReferenceEquals(live.RigOfShooter(0), self),
                $"RigOfShooter resolves a shooter id to the plane that fired — the ids are unique across the session, so it names one plane and not a class of them");
            ctx.Check(live.RigOfShooter(ProjectilePool.NoShooter) == null
                      && live.RigOfShooter(9999) == null,
                $"…and an unowned round or an unregistered id resolves to nothing");

            var impact = new Vector3(0f, 0f, -2f);
            self.TakeProjectileHit(gun, impact, "nose", ProjectilePool.NoShooter);
            ctx.Check(self.Targeting.Attackers.Count == 0,
                $"an unowned round (a turret's, a zeppelin broadside) records no attacker — there is nobody to target");
            self.TakeProjectileHit(gun, impact, "nose", friendly.PlayerIndex);
            ctx.Check(self.Targeting.Attackers.Count == 0,
                $"friendly fire records no attacker either: the engine's gate is a shooter on a DIFFERENT, non-zero team");
            self.TakeProjectileHit(gun, impact, "nose", hostile.PlayerIndex);
            ctx.Check(self.Targeting.Attackers.Count == 1
                      && ReferenceEquals(self.Targeting.Attackers[0], hostile),
                $"a hostile round puts its shooter on the queue Next Enemy walks first count={self.Targeting.Attackers.Count}");
            self.TakeProjectileHit(gun, impact, "nose", hostile.PlayerIndex);
            ctx.Check(self.Targeting.Attackers.Count == 1,
                $"…and a second round from the same shooter does not list it twice");
        }
        finally
        {
            self?.Free();
            friendly?.Free();
            hostile?.Free();
            pool?.Free();
            textures.Dispose();
        }
    }

    // TargetSelection, tree-free and data-free: plain object sources through the real TargetPool,
    // a plane at the origin on the identity basis, no world. The geometry is chosen so the decoded
    // sector order and a plain nearest-in-space order DISAGREE (nearest is 100 m off the right
    // wing, the cycle head is 400 m ahead), so an implementation that sorted by range fails.
    internal static void TargetSelectionModel(TestContext ctx)
    {
        var ahead1 = new object();      // 900 m ahead   -> sector 0
        var ahead2 = new object();      // 400 m ahead   -> sector 0, nearer
        var behind = new object();      // 200 m behind  -> sector 1
        var left = new object();        // 200 m left    -> sector 2
        var right = new object();       // 100 m right   -> sector 3, the nearest thing in space
        var ally = new object();        // 300 m ahead, own team
        int own = AimAssist.PlayerTeam;
        int foe = InstantActionRuntime.EnemyTeam;
        var basis = Basis.Identity;

        AimCandidateSet Scan(params (object Src, Vector3 At, int Team)[] entries)
        {
            var s = new AimCandidateSet();
            foreach (var (src, at, team) in entries)
            {
                s.AddVehicle(at, Vector3.Zero, team, live: true, src);
            }

            return s;
        }

        var full = Scan(
            (ahead1, new Vector3(0f, 0f, -900f), foe),
            (ahead2, new Vector3(0f, 0f, -400f), foe),
            (behind, new Vector3(0f, 0f, 200f), foe),
            (left, new Vector3(-200f, 0f, 0f), foe),
            (right, new Vector3(100f, 0f, 0f), foe),
            (ally, new Vector3(0f, 0f, -300f), own));

        // The sector key itself, against the decode's own table.
        ctx.Check(TargetSelection.SectorKey(Vector3.Forward * 5f, basis, false) == 0
                  && TargetSelection.SectorKey(Vector3.Back * 5f, basis, false) == 1
                  && TargetSelection.SectorKey(Vector3.Left * 5f, basis, false) == 2
                  && TargetSelection.SectorKey(Vector3.Right * 5f, basis, false) == 3
                  && TargetSelection.SectorKey(Vector3.Right * 5f, basis, true) == -1,
            $"FUN_004bbd60's sectors: ahead 0, behind 1, left 2, right 3, and an objective overrides to -1");

        // An objective is hand-filed: nothing sets TargetRef.Objective yet, but the -1 key is the
        // cycle's first rule. The pool is driven directly, not through Rebuild, so the objective is
        // there on the FIRST resolve; pre-resolving would make the auto-acquire claim vacuous.
        var objective = new object();
        var sel = new TargetSelection();
        sel.Pool.Rebuild(full, null, own, null);
        sel.Pool.Add(TargetRef.ForStructure(
            new AimCandidate { Position = new Vector3(0f, 0f, 1500f), Team = foe, Live = true, Source = objective },
            TargetClass.Enemy, "Promised Land", "Zeppelin", "Destroy", objective: true));
        sel.Resolve(Vector3.Zero, basis);

        var order = sel.Ordered.Select(t => t.Source).ToList();
        ctx.Check(order.SequenceEqual(new[] { objective, ahead2, ahead1, behind, left, right }),
            $"the whole cycle order in one read: the objective first (1500 m BEHIND, and still first), then ahead nearest-first, then behind, left, right — the 100 m target off the right wing is LAST");
        ctx.Check(sel.Current is { } head && ReferenceEquals(head.Source, objective)
                  && sel.ActiveClass == TargetClass.Enemy,
            $"auto-acquire: a fresh selector starts on the Enemy cycle already holding its head, with no input");
        ctx.Check(!sel.Ordered.Any(t => ReferenceEquals(t.Source, ally)),
            $"…and the ally is not in the Enemy cycle at all");

        // Stepping.
        sel.Next(TargetClass.Enemy);
        ctx.Check(ReferenceEquals(sel.Current?.Source, objective),
            $"a handler mutates state only — Current still reads the old target until the next Resolve publishes it, which is the original's own one-frame shape");
        sel.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, ahead2), $"Next steps one entry down the cycle");
        sel.Previous(TargetClass.Enemy);
        sel.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, objective), $"Previous steps back up");
        sel.Previous(TargetClass.Enemy);
        sel.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, right),
            $"…and wraps past the head to the tail");
        sel.Next(TargetClass.Enemy);
        sel.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, objective), $"…and back again past the tail");

        // Nearest is the HEAD, not the nearest thing.
        sel.Next(TargetClass.Enemy);
        sel.Next(TargetClass.Enemy);
        sel.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, ahead1), $"walked two down the cycle");
        sel.Nearest(TargetClass.Enemy);
        sel.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, objective),
            $"'Nearest' returns to the HEAD of the cycle, which is 1500 m away, not the 100 m target off the wing");

        // Death: the selected target leaves the pool. It drops to the HEAD, not to its neighbour.
        sel.Next(TargetClass.Enemy);
        sel.Next(TargetClass.Enemy);
        sel.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, ahead1), $"holding the third entry");
        var afterDeath = Scan(
            (ahead2, new Vector3(0f, 0f, -400f), foe),
            (behind, new Vector3(0f, 0f, 200f), foe),
            (left, new Vector3(-200f, 0f, 0f), foe),
            (right, new Vector3(100f, 0f, 0f), foe));
        sel.Rebuild(afterDeath, null, own, null, Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, ahead2),
            $"the selected target dying drops to the HEAD of the cycle, never to the dead entry's neighbour (which would have been 'behind')");

        // Stickiness: nothing but death, input and the explicit clear moves it.
        sel.Next(TargetClass.Enemy);
        sel.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, behind), $"holding a mid-cycle entry");
        sel.Rebuild(afterDeath, null, own, null, Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, behind),
            $"own respawn (a rebuild with the target still alive) preserves the live selection");
        var orderBefore = sel.Ordered.Select(t => t.Source).ToList();
        var farBasis = new Basis(Vector3.Up, Mathf.Pi * 0.75f);
        sel.Rebuild(afterDeath, null, own, null, new Vector3(4000f, 900f, -6000f), farBasis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, behind)
                  && !sel.Ordered.Select(t => t.Source).SequenceEqual(orderBefore),
            $"flying 7 km away and swinging the nose onto a new bearing re-sorts the cycle but does NOT drop the selection — there is no range, bearing or LOS gate anywhere in the decoded path");

        // Target Nothing STAYS cleared.
        sel.Clear();
        ctx.Check(sel.Current == null && sel.ActiveClass == null, $"Target Nothing clears the target and every class flag");
        for (int i = 0; i < 3; i++)
        {
            sel.Rebuild(afterDeath, null, own, null, Vector3.Zero, basis);
        }

        ctx.Check(sel.Current == null && sel.Pool.Count == 0,
            $"…and STAYS cleared through repeated rebuilds — the collection pass is skipped, so the auto-acquire cannot fire again pool={sel.Pool.Count}");
        sel.Next(TargetClass.Enemy);
        sel.Rebuild(afterDeath, null, own, null, Vector3.Zero, basis);
        ctx.Check(sel.Current != null && sel.ActiveClass == TargetClass.Enemy,
            $"…until a class action presses, which is the only thing that ends the clear");

        // Nearest crosshairs: the NOSE cone, friend or foe, 2 km cap.
        var crosshair = new TargetSelection();
        var coneScan = Scan(
            (ally, new Vector3(0f, 0f, -300f), own),                 // dead ahead, friendly
            (right, new Vector3(100f, 0f, -100f), foe),              // 45° off the nose: outside 15°
            (ahead1, new Vector3(0f, 0f, -2400f), foe));             // on the nose but past 2 km
        crosshair.Rebuild(coneScan, null, own, null, Vector3.Zero, basis);
        ctx.Check(crosshair.NearestCrosshairs(Vector3.Zero, basis),
            $"nearest-crosshairs finds something in the cone");
        crosshair.Resolve(Vector3.Zero, basis);
        ctx.Check(crosshair.ActiveClass == TargetClass.Ally
                  && ReferenceEquals(crosshair.Current?.Source, ally),
            $"it reaches an ALLY 300 m dead ahead and writes the class back to Ally, so the next Next/Previous continues in that cycle");
        ctx.Check(!new TargetSelection().NearestCrosshairs(Vector3.Zero, basis),
            $"an empty pool finds nothing");
        var farOnly = new TargetSelection();
        farOnly.Rebuild(Scan((ahead1, new Vector3(0f, 0f, -2400f), foe)), null, own, null, Vector3.Zero, basis);
        ctx.Check(!farOnly.NearestCrosshairs(Vector3.Zero, basis),
            $"a target dead on the nose but past the hard 2 km cap is never picked");
        var offAxis = new TargetSelection();
        offAxis.Rebuild(Scan((right, new Vector3(100f, 0f, -100f), foe)), null, own, null, Vector3.Zero, basis);
        ctx.Check(!offAxis.NearestCrosshairs(Vector3.Zero, basis),
            $"a target 45° off the nose is outside the 15° half-angle cone, however close");

        // 0x24's attacker queue, walked backwards from the end.
        var shot = new TargetSelection();
        shot.Rebuild(full, null, own, null, Vector3.Zero, basis);
        shot.RecordAttacker(ahead1);
        shot.RecordAttacker(right);
        ctx.Check(shot.Attackers.Count == 2 && ReferenceEquals(shot.Attackers[1], right),
            $"the queue is an end insert, oldest first");
        shot.RecordAttacker(ahead1);
        ctx.Check(shot.Attackers.Count == 2 && ReferenceEquals(shot.Attackers[1], ahead1),
            $"a repeat attacker MOVES to the end rather than listing twice (inference, not decode — FUN_004bc1e0 was not traced)");
        shot.NextEnemy();
        shot.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(shot.Current?.Source, ahead1),
            $"Next Enemy with a target not in the queue takes the MOST RECENT attacker, ignoring the ordinary cycle");
        shot.NextEnemy();
        shot.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(shot.Current?.Source, right),
            $"…pressing again walks BACKWARDS to an older attacker");
        shot.NextEnemy();
        shot.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(shot.Current?.Source, ahead2),
            $"…and from the queue's FIRST entry it falls through to an ordinary +1 step, which from the cycle's tail wraps to its head");
        shot.ForgetTarget(ahead1);
        ctx.Check(shot.Attackers.Count == 1 && ReferenceEquals(shot.Attackers[0], right),
            $"the death hook prunes the queue, so a dead shooter is never offered again");
    }

    // --target=. Everything the flag means lives in ApplyInitial and Select, which take a pose and
    // no tree, so the whole grammar is pinned here rather than only by the two screenshot runs. The
    // pool is built from REAL sources rather than hand-filed refs, because the claim is about the
    // names TargetPool actually produces.
    internal static void TargetFlagModel(TestContext ctx)
    {
        int own = AimAssist.PlayerTeam;
        int foe = InstantActionRuntime.EnemyTeam;
        var basis = Basis.Identity;
        var self = new FlightController { Name = "player_fury", Team = own };
        var far = new FlightController { Name = "ai1_player_fury", IsHumanPiloted = false, Team = foe };
        var near = new FlightController { Name = "ai2_player_fury", IsHumanPiloted = false, Team = foe };
        var wing = new FlightController { Name = "wing1_kestrel", IsHumanPiloted = false, Team = own };
        var gasbagNode = new Node3D { Name = "gasbag1" };
        try
        {
            ctx.Host.AddChild(gasbagNode);
            var scan = new AimCandidateSet();
            scan.AddVehicle(Vector3.Zero, Vector3.Zero, own, live: true, self);
            scan.AddVehicle(new Vector3(0f, 0f, -900f), Vector3.Zero, foe, live: true, far);
            scan.AddVehicle(new Vector3(0f, 0f, -400f), Vector3.Zero, foe, live: true, near);
            scan.AddVehicle(new Vector3(0f, 0f, -300f), Vector3.Zero, own, live: true, wing);
            var registry = new DestructibleRegistry();
            var gasbagInst = registry.Register(
                new AnimDefinition { Name = "gasbag1", AnimName = "zep_zone_gasbag1" }, gasbagNode, 200f);
            var parts = new List<AimCandidate>
            {
                new() { Position = new Vector3(60f, 0f, -600f), Velocity = Vector3.Zero, Team = AimAssist.WorldTeam, Live = true, Source = gasbagInst },
            };

            TargetSelection Fresh()
            {
                var s = new TargetSelection();
                s.Rebuild(scan, parts, own, self, Vector3.Zero, basis);
                return s;
            }

            ctx.Check(SessionSpec.Parse(new[] { "--target=ai1_player_fury" }).TargetSelect == "ai1_player_fury"
                      && SessionSpec.Parse(System.Array.Empty<string>()).TargetSelect == null,
                $"--target= reaches the spec verbatim, and its absence is null rather than 'none' — an unscripted session keeps the ordinary auto-acquire");

            // The auto-acquire picks the NEARER enemy ahead. Everything below that names the far one
            // is therefore a claim the flag actually moved the selection.
            var auto = Fresh();
            ctx.Check(ReferenceEquals(auto.Current?.Source, near),
                $"CONTROL: with no flag the pool auto-acquires the nearer enemy ahead, so pinning the far one cannot pass by coincidence");

            var pinned = Fresh();
            ctx.Check(pinned.ApplyInitial("ai1_player_fury", Vector3.Zero, basis)
                      && ReferenceEquals(pinned.Current?.Source, far)
                      && pinned.ActiveClass == TargetClass.Enemy,
                $"--target=<node name> pins the named aircraft, not the one the auto-acquire chose");
            var upper = Fresh();
            ctx.Check(upper.ApplyInitial("AI1_PLAYER_FURY", Vector3.Zero, basis)
                      && ReferenceEquals(upper.Current?.Source, far),
                $"…matched case-insensitively, so a shell's capitalisation cannot change what a golden shot frames");

            // One grammar reaches all three cycles, which is this item's open TODO settled: a
            // zeppelin sub-part is named by its own world node (TargetPool.NameOf -> Instance.Anchor),
            // exactly the shape an aircraft's name has, so no second grammar is needed for it.
            var ally = Fresh();
            ctx.Check(ally.ApplyInitial("wing1_kestrel", Vector3.Zero, basis)
                      && ReferenceEquals(ally.Current?.Source, wing)
                      && ally.ActiveClass == TargetClass.Ally,
                $"the same grammar reaches an ALLY, writing the class back — without that the next Resolve would drop a target outside the active cycle");
            var part = Fresh();
            ctx.Check(part.ApplyInitial("gasbag1", Vector3.Zero, basis)
                      && ReferenceEquals(part.Current?.Source, gasbagInst)
                      && part.Current?.Kind == AimTargetKind.Structure
                      && part.ActiveClass == TargetClass.NonAircraft,
                $"…and a zeppelin SUB-PART by its part node's name, on the Non-Aircraft cycle: the TODO's premise (a sub-part has no node name of an aircraft's shape) is wrong, so one grammar covers all three");

            // The four words, each mapping onto the ordinary action rather than a scripted path.
            var nearest = Fresh();
            nearest.Next(TargetClass.Enemy);      // walk off the head first, so returning to it means something
            nearest.Resolve(Vector3.Zero, basis);
            ctx.Check(ReferenceEquals(nearest.Current?.Source, far)
                      && nearest.ApplyInitial("nearest", Vector3.Zero, basis)
                      && ReferenceEquals(nearest.Current?.Source, near),
                $"--target=nearest is the original's Nearest action: the HEAD of the cycle");
            var next = Fresh();
            ctx.Check(next.ApplyInitial("next", Vector3.Zero, basis)
                      && ReferenceEquals(next.Current?.Source, far),
                $"--target=next steps the enemy cycle once from the auto-acquired head");
            var cross = Fresh();
            ctx.Check(cross.ApplyInitial("crosshair", Vector3.Zero, basis)
                      && ReferenceEquals(cross.Current?.Source, wing),
                $"--target=crosshair runs the nose-cone scan, which reaches the nearest thing on the nose whatever its side — here the ally at 300 m");
            var cleared = Fresh();
            ctx.Check(cleared.ApplyInitial("none", Vector3.Zero, basis)
                      && cleared.Current == null && cleared.ActiveClass == null,
                $"--target=none is Target Nothing: no target and no class");
            cleared.Rebuild(scan, parts, own, self, Vector3.Zero, basis);
            ctx.Check(cleared.Current == null && cleared.Pool.Count == 0,
                $"…and stays cleared through the next rebuild, so a --screenshot run can capture the HUD with nothing selected");

            // A name nothing carries: the selection is left exactly as it was.
            var miss = Fresh();
            ctx.Check(!miss.ApplyInitial("ai7_nonesuch", Vector3.Zero, basis)
                      && ReferenceEquals(miss.Current?.Source, near),
                $"an unknown name reports failure and leaves the selection alone rather than clearing it");

            // Determinism, the flag's whole purpose: the claim the two screenshot runs make, made
            // here against two independently built selectors.
            var runA = Fresh();
            var runB = Fresh();
            runA.ApplyInitial("ai1_player_fury", Vector3.Zero, basis);
            runB.ApplyInitial("ai1_player_fury", Vector3.Zero, basis);
            ctx.Check(ReferenceEquals(runA.Current?.Source, runB.Current?.Source)
                      && ReferenceEquals(runA.Current?.Source, far),
                $"two selectors given the same spec land on the same target — the property a reproducible golden shot rests on");

            // ⚠ The item's own trap: the flag sets the INITIAL selection and must not hold it.
            var live = Fresh();
            live.ApplyInitial("ai1_player_fury", Vector3.Zero, basis);
            live.Next(TargetClass.Enemy);
            live.Resolve(Vector3.Zero, basis);
            ctx.Check(ReferenceEquals(live.Current?.Source, near),
                $"a keypress after the flag moves the selection off the pinned target");
            live.Rebuild(scan, parts, own, self, Vector3.Zero, basis);
            ctx.Check(ReferenceEquals(live.Current?.Source, near),
                $"…and the next frame's rebuild does NOT snap back to it — the flag is spent, so an interactive session started with it still cycles");
        }
        finally
        {
            gasbagNode.Free();
            self.Free();
            far.Free();
            near.Free();
            wing.Free();
        }
    }

    // TargetPool. The pure half runs over a hand-built AimCandidateSet with no world, which pins
    // every membership and exclusion rule; the world half runs C1's REAL emplacement census through
    // the same pool, because "an emplacement is selectable" is a claim about objects the session
    // builds. The carried-gunner exclusion rides the turret-gunner suite, where one already exists.
    internal static void TargetPoolModel(TestContext ctx)
    {
        var self = new FlightController { PlayerIndex = 1, Team = AimAssist.PlayerTeam };
        var wingman = new FlightController { IsHumanPiloted = false, Team = AimAssist.PlayerTeam };
        var enemy = new FlightController { IsHumanPiloted = false, Team = InstantActionRuntime.EnemyTeam };
        var deadEnemy = new FlightController { IsHumanPiloted = false, Team = InstantActionRuntime.EnemyTeam };
        var neutral = new FlightController { IsHumanPiloted = false, Team = AimAssist.NeutralTeam };
        var crate = new Node3D { Name = "crate1" };
        var gasbag = new Node3D { Name = "gasbag1" };
        var deadEngine = new Node3D { Name = "engine2" };
        try
        {
            ctx.Host.AddChild(crate);
            ctx.Host.AddChild(gasbag);
            ctx.Host.AddChild(deadEngine);

            var scan = new AimCandidateSet();
            scan.AddVehicle(Vector3.Zero, Vector3.Zero, self.Team, live: true, self);
            scan.AddVehicle(new Vector3(0f, 0f, -100f), Vector3.Zero, wingman.Team, live: true, wingman);
            scan.AddVehicle(new Vector3(0f, 0f, -400f), Vector3.Zero, enemy.Team, live: true, enemy);
            scan.AddVehicle(new Vector3(0f, 0f, -450f), Vector3.Zero, deadEnemy.Team, live: false, deadEnemy);
            scan.AddVehicle(new Vector3(0f, 0f, -500f), Vector3.Zero, neutral.Team, live: true, neutral);

            // The registry, fed the way the GUN assist feeds it. The pool must ignore all of it.
            var registry = new DestructibleRegistry();
            var crateInst = registry.Register(
                new AnimDefinition { Name = "crate", AnimName = "crate_blow" }, crate, 30f);
            scan.AddStructures(registry);
            scan.AddOrdnance(new Vector3(0f, 0f, -50f), Vector3.Zero, InstantActionRuntime.EnemyTeam,
                new object());

            var pool = new TargetPool();
            pool.Rebuild(scan, null, self.Team, self);
            ctx.Check(pool.Enemy.Count == 1 && ReferenceEquals(pool.Enemy[0].Source, enemy),
                $"the hostile-team plane is the only Enemy entry count={pool.Enemy.Count}");
            ctx.Check(pool.Ally.Count == 2
                      && pool.Ally.Any(t => ReferenceEquals(t.Source, wingman))
                      && pool.Ally.Any(t => ReferenceEquals(t.Source, neutral)),
                $"a wingman lands in ALLY, not Enemy, and so does a neutral (either side unaffiliated is Ally) count={pool.Ally.Count}");
            ctx.Check(!pool.Enemy.Concat(pool.Ally).Concat(pool.NonAircraft)
                    .Any(t => ReferenceEquals(t.Source, self)),
                $"the selecting plane is excluded from its own pool");
            ctx.Check(!pool.Enemy.Any(t => ReferenceEquals(t.Source, deadEnemy)),
                $"a dead plane is listed by the collector but absent from the cycles");
            ctx.Check(pool.NonAircraft.Count == 0 && scan.Structures.Count == 1
                      && scan.Ordnance.Count == 1,
                $"the registry contributes NOTHING though it is populated, and neither does an ordnance entry carrying no flyout state structures={scan.Structures.Count} ordnance={scan.Ordnance.Count} nonAircraft={pool.NonAircraft.Count}");
            ctx.Check(!pool.Enemy.Concat(pool.Ally).Concat(pool.NonAircraft)
                    .Any(t => ReferenceEquals(t.Source, crateInst)),
                $"…and specifically the crate never becomes selectable (decision 8: ours would walk every crate and fence, the original's walks a curated targets.zrd list)");

            // The able-to-fail control: derive the side from the pilot index the way the HUD used
            // to, and P2's own wingman turns hostile. ⚠ The real enemy now stays in Enemy, where it
            // used to drop out — that derived side WAS EnemyTeam until the versus band (BL-403).
            pool.Rebuild(scan, null, AimAssist.TeamOfPilot(self.PlayerIndex), self);
            ctx.Check(pool.Enemy.Any(t => ReferenceEquals(t.Source, wingman))
                      && AimAssist.TeamOfPilot(self.PlayerIndex) != InstantActionRuntime.EnemyTeam,
                $"CONTROL: deriving P2's side from its pilot index puts the wingman in Enemy — the bug this item diagnosed — and no longer collides with the enemy team itself");

            // Sub-parts: the only channel by which a structure becomes selectable.
            var gasbagInst = registry.Register(
                new AnimDefinition { Name = "gasbag1", AnimName = "zep_zone_gasbag1" }, gasbag, 200f);
            var engineInst = registry.Register(
                new AnimDefinition { Name = "engine2", AnimName = "zep_zone_engine2" }, deadEngine, 100f);
            engineInst.Status = DestructibleRegistry.State.Destroyed;
            var hullVel = new Vector3(0f, 0f, -12f);
            var parts = new List<AimCandidate>
            {
                new() { Position = gasbag.GlobalPosition, Velocity = hullVel, Team = AimAssist.WorldTeam, Live = true, Source = gasbagInst },
                new() { Position = deadEngine.GlobalPosition, Velocity = hullVel, Team = AimAssist.WorldTeam, Live = false, Source = engineInst },
            };
            pool.Rebuild(scan, parts, self.Team, self);
            ctx.Check(pool.NonAircraft.Count == 1
                      && ReferenceEquals(pool.NonAircraft[0].Source, gasbagInst),
                $"a zeppelin contributes its live parts to Non-Aircraft count={pool.NonAircraft.Count}");
            ctx.Check(!pool.NonAircraft.Any(t => ReferenceEquals(t.Source, engineInst)),
                $"a DESTROYED engine is absent");
            var bag = pool.NonAircraft[0];
            ctx.Check(bag.Kind == AimTargetKind.Structure && bag.Velocity == hullVel
                      && bag.Name == "gasbag1"
                      && bag.Health is { } bh && Mathf.IsEqualApprox(bh, 1f) && bag.Armor == null,
                $"…carrying its hull's velocity (never zero — the bracket gate has to lead it), its part node's name and health with no armor pool name='{bag.Name}' v={bag.Velocity}");

            // --- C1's real emplacements through the same pool --------------------------------
            ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
            string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
            ctx.RequireData(texturesPath, $"C1 textures");
            var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
            var turretDefs = TurretDefs.Load(ctx.ZrdrPath);
            ctx.WithWorld("C1", collision: false, world =>
            {
                var textures = new TextureArchive(texturesPath);
                ProjectilePool? live = null;
                Session.TurretEmplacementRuntime? emplacements = null;
                try
                {
                    live = new ProjectilePool(textures, null, null);
                    ctx.Host.AddChild(live);
                    emplacements = new Session.TurretEmplacementRuntime(turretDefs, weapons,
                        (pattern, scope) => world.Runtime.FindNodes(pattern, scope), live,
                        world.Runtime.WorldRoot);
                    // The runtime registers its own emplacements with the pool; a second
                    // RegisterWorldTurrets here would list every gun twice.
                    var worldScan = new AimCandidateSet();
                    live.CollectTurrets(worldScan);
                    var worldPool = new TargetPool();
                    worldPool.Rebuild(worldScan, null, AimAssist.PlayerTeam, null);
                    int aliveEmplacements = emplacements.Emplacements.Count(t => t.Alive);
                    ctx.Check(worldScan.Turrets.Count == emplacements.Count && aliveEmplacements > 0,
                        $"C1's whole emplacement census reaches the scan turrets={worldScan.Turrets.Count} of {emplacements.Count}");
                    ctx.Check(worldPool.NonAircraft.Count > 0
                              && worldPool.NonAircraft.All(t => t.Kind == AimTargetKind.Turret)
                              && worldPool.Enemy.Count == 0 && worldPool.Ally.Count == 0,
                        $"every selectable emplacement lands on the NON-AIRCRAFT cycle, never Enemy or Ally, whatever its team nonAircraft={worldPool.NonAircraft.Count}");
                    ctx.Check(worldPool.NonAircraft.All(t => t.Health == null && t.Armor == null
                                  && t.Name.Length > 0),
                        $"…each with its TITLE@site label and no health figure at all (the retail loaders read no HEALTH key)");
                    ctx.Note($"C1 target pool: {worldPool.NonAircraft.Count} selectable emplacements of {emplacements.Count} placed, {aliveEmplacements} alive");
                }
                finally
                {
                    emplacements?.Free();
                    live?.Free();
                    textures.Dispose();
                }
            });
        }
        finally
        {
            self.Free();
            wingman.Free();
            enemy.Free();
            deadEnemy.Free();
            neutral.Free();
            crate.Free();
            gasbag.Free();
            deadEngine.Free();
        }
    }

    // The targeting HUD on AI hostiles, in two halves. The pure selection (TargetHud.NearestHostile
    // over a constructed candidate set, no scene) pins the filters and HostileTag; the in-engine half
    // runs the tracker against real spawned AI planes in a live pool, covering acquisition, the
    // switch to a closer hostile, the crash drop and the empty-pool null. A hud built without a
    // pool never tracks, which is the seam that keeps the golden VS output untouched.
    internal static void HostileMarkerHud(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        // --- the pure selection, over a constructed candidate set (no tree, no pool) ---
        var pureNear = new FlightController { IsHumanPiloted = false };
        var pureFar = new FlightController { IsHumanPiloted = false };
        var pureDead = new FlightController { IsHumanPiloted = false };
        var pureHuman = new FlightController();
        try
        {
            int ownTeam = AimAssist.TeamOfPilot(0);
            var set = new AimCandidateSet();
            set.AddVehicle(new Vector3(0f, 0f, -50f), Vector3.Zero,
                AimAssist.TeamOfPilot(1), live: true, pureHuman); // a human: an opponent, never a hostile
            set.AddVehicle(new Vector3(0f, 0f, -100f), Vector3.Zero,
                AimAssist.TeamOfPilot(101), live: false, pureDead); // listed but not live (crashed)
            set.AddVehicle(new Vector3(0f, 0f, -10f), Vector3.Zero,
                AimAssist.NeutralTeam, live: true, pureNear); // neutral side rejects the pair
            set.AddVehicle(new Vector3(0f, 0f, -500f), Vector3.Zero,
                AimAssist.TeamOfPilot(100), live: true, pureNear);
            set.AddVehicle(new Vector3(0f, 0f, -2000f), Vector3.Zero,
                AimAssist.TeamOfPilot(102), live: true, pureFar);
            ctx.Check(ReferenceEquals(TargetHud.NearestHostile(Vector3.Zero, ownTeam, set), pureNear),
                $"the nearest LIVE AI hostile wins over a closer human, a closer dead plane and a closer neutral");
            ctx.Check(TargetHud.NearestHostile(Vector3.Zero, AimAssist.NeutralTeam, set) == null,
                $"a neutral own side targets nothing (the engine's either-side-0 rule)");
            ctx.Check(TargetHud.NearestHostile(Vector3.Zero, ownTeam, new AimCandidateSet()) == null,
                $"an empty scan tracks nothing");
            ctx.Check(TargetHud.HostileTag("ai1_player_fury") == "AI1"
                && TargetHud.HostileTag("bandit") == "BANDIT" && TargetHud.HostileTag("") == "AI",
                $"the marker tag is the name's first segment uppercased, 'AI' as the fallback");

            // The wingman-in-the-marker bug: OwnTeam must read the pane's own Team FIELD. The pilot
            // index derivation is right for P1 by coincidence and wrong for P2-P4 the moment a
            // mission sets teams, which every Instant Action and --coop session does.
            var p2 = new TargetHud { PlayerIndex = 1 };
            ctx.Check(p2.OwnTeam == AimAssist.TeamOfPilot(1),
                $"with no aircraft bound the HUD still falls back to the pilot-index derivation own={p2.OwnTeam}");
            p2.Own = pureHuman;
            pureHuman.Team = AimAssist.PlayerTeam;
            ctx.Check(p2.OwnTeam == AimAssist.PlayerTeam
                      && AimAssist.TeamOfPilot(1) != AimAssist.PlayerTeam,
                $"P2 flying an Instant Action mission is on the PLAYER team, which its pilot index would have derived as {AimAssist.TeamOfPilot(1)} — the wingman-in-the-marker bug");
            var wingScan = new AimCandidateSet();
            wingScan.AddVehicle(new Vector3(0f, 0f, -80f), Vector3.Zero, AimAssist.PlayerTeam,
                live: true, pureNear);                                  // P2's own wingman
            wingScan.AddVehicle(new Vector3(0f, 0f, -900f), Vector3.Zero,
                InstantActionRuntime.EnemyTeam, live: true, pureFar);   // the actual enemy
            ctx.Check(ReferenceEquals(
                    TargetHud.NearestHostile(Vector3.Zero, p2.OwnTeam, wingScan), pureFar),
                $"…so the far ENEMY is tracked and the near wingman is not");
            ctx.Check(ReferenceEquals(
                    TargetHud.NearestHostile(Vector3.Zero, AimAssist.TeamOfPilot(1), wingScan),
                    pureNear),
                $"CONTROL: the old derivation tracks the WINGMAN instead, and skips the enemy as own-team");
            p2.Free();
        }
        finally
        {
            pureNear.Free();
            pureFar.Free();
            pureDead.Free();
            pureHuman.Free();
        }

        // --- The shipped marker's rules: colour, the gun-reach bracket gate, the label lines -----
        // All three are pure and decoded (FUN_004a5f40 / FUN_004574d0 / FUN_004579e0); what no test
        // can reach is the drawn geometry itself, which is the c1-targeting-hud golden.
        int hostileTeam = AimAssist.TeamOfPilot(100);
        var enemyRef = TargetRef.ForAircraft(
            new AimCandidate { Team = hostileTeam, Live = true, Source = new object() },
            TargetClass.Enemy, "ai1_player_fury", "Fury");
        var allyRef = TargetRef.ForAircraft(
            new AimCandidate { Team = AimAssist.PlayerTeam, Live = true, Source = new object() },
            TargetClass.Ally, "ai2_player_kestrel", "Kestrel");
        var destroyRef = TargetRef.ForStructure(
            new AimCandidate { Team = AimAssist.WorldTeam, Live = true, Source = new object() },
            TargetClass.Enemy, "Promised Land", "Zeppelin", "Destroy", objective: true);
        var protectRef = TargetRef.ForStructure(
            new AimCandidate { Team = AimAssist.PlayerTeam, Live = true, Source = new object() },
            TargetClass.Enemy, "Convoy", "Freighter", "Protect", objective: true);
        ctx.Check(TargetHud.MarkerColor(enemyRef, AimAssist.PlayerTeam)
                  != TargetHud.MarkerColor(allyRef, AimAssist.PlayerTeam)
                  && TargetHud.MarkerColor(allyRef, AimAssist.PlayerTeam)
                     != TargetHud.MarkerColor(protectRef, AimAssist.PlayerTeam),
            $"three colours, not two: a hostile, a friendly and a non-destructive objective all read differently (the decode's own rule — a FRIENDLY is green, blue is the objective)");
        ctx.Check(TargetHud.MarkerColor(destroyRef, AimAssist.PlayerTeam)
                  == TargetHud.MarkerColor(enemyRef, AimAssist.PlayerTeam),
            $"a Destroy objective is the hostile colour, whatever team the entity carries — the four destructive categories override the team test");
        ctx.Check(TargetHud.MarkerColor(enemyRef, AimAssist.NeutralTeam)
                  == TargetHud.MarkerColor(allyRef, AimAssist.PlayerTeam),
            $"a neutral own side has no enemies: with either team 0 the categoryless rule falls to the friendly colour");

        // The gate is the SELECTED GUN's reach through a lead solve, not a distance constant. A
        // 860 m/s round with RANGE 1000 reaches ~1 km; the same target 2 km out does not.
        const float RoundSpeed = 860f, GunRange = 1000f;
        var muzzle = Vector3.Zero;
        ctx.Check(TargetHud.GunReaches(muzzle, Vector3.Zero, RoundSpeed, GunRange,
                      new Vector3(0f, 0f, -600f), Vector3.Zero)
                  && !TargetHud.GunReaches(muzzle, Vector3.Zero, RoundSpeed, GunRange,
                      new Vector3(0f, 0f, -2000f), Vector3.Zero),
            $"a target inside the gun's authored RANGE is bracketed and one past it is not");
        ctx.Check(!TargetHud.GunReaches(muzzle, Vector3.Zero, RoundSpeed, GunRange,
                      new Vector3(0f, 0f, -600f), new Vector3(0f, 0f, -900f)),
            $"a target OUTRUNNING the round is never bracketed, at any range — the solver returns no intercept (the port of 'a fixed-metres threshold would lose this')");
        ctx.Check(!TargetHud.GunReaches(muzzle, Vector3.Zero, RoundSpeed, GunRange,
                      new Vector3(0f, 0f, -1010f), Vector3.Zero)
                  && TargetHud.GunReaches(muzzle, Vector3.Zero, RoundSpeed,
                      GunRange + TargetHud.BracketHysteresis, new Vector3(0f, 0f, -1010f),
                      Vector3.Zero),
            $"the hysteresis is what a target hovering at the boundary rides: off by the plain gate, still on by the widened one (TUNE, ours not the original's)");

        var lines = new List<string>();
        TargetHud.LabelLines(enemyRef, null, lines);
        ctx.Check(lines.Count == 2 && lines[0].Length == 0 && lines[1] == "Fury",
            $"an ordinary aircraft under a box is its airframe name alone, in the SECOND slot — the blank category line still holds the first, which is what keeps the name out of the silhouette ({string.Join(" / ", lines)})");
        lines.Clear();
        TargetHud.LabelLines(enemyRef, "4 o'clock", lines, keepSlots: false);
        ctx.Check(lines.Count == 2 && lines[0] == "Fury" && lines[1] == "4 o'clock",
            $"…and off screen, where there is no box to measure the slot from, it compacts to the two lines HUD.png shows ({string.Join(" / ", lines)})");
        lines.Clear();
        TargetHud.LabelLines(destroyRef, null, lines);
        ctx.Check(lines.Count == 2 && lines[0] == "Zeppelin [Destroy] -"
                  && lines[1] == "Promised Land",
            $"a named objective composes both lines, C1 M04 Zeppelin.png's case, with no wrap width to port ({string.Join(" / ", lines)})");

        // --- The debug-marker string: identity kept whole, health/armor gated on the source -------
        var healthyRef = TargetRef.ForAircraft(
            new AimCandidate { Team = hostileTeam, Live = true, Source = new object() },
            TargetClass.Enemy, "ai1_player_fury", "Fury",
            TargetRef.Fraction(78f, 100f), TargetRef.Fraction(91f, 100f));
        string healthyTag = TargetHud.DebugTag("AI1", healthyRef, 640f, "");
        ctx.Check(healthyTag == "AI1 Fury 640 m H78 A91",
            $"the debug tag keeps the FULL identity (unlike the shipped marker's plane-type-alone label), adds the plane type, then health and armor as whole percentages with no percent sign, health first: '{healthyTag}'");
        ctx.Check(TargetHud.DebugTag("AI1", healthyRef, 640f, "  pursue") == "AI1 Fury 640 m H78 A91  pursue",
            $"the AI mode trails the figures, carrying ModeSuffix's own leading spaces: '{TargetHud.DebugTag("AI1", healthyRef, 640f, "  pursue")}'");
        string noHealthTag = TargetHud.DebugTag("AI2", enemyRef, 250f, "");
        ctx.Check(noHealthTag == "AI2 Fury 250 m",
            $"a source with no health model (enemyRef carries none) omits BOTH figures rather than printing H100 A100: '{noHealthTag}'");

        // --- the live tracker, against real AI planes registered in a real pool ---
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? ai1 = null, ai2 = null;
        TargetHud? hud = null, noPoolHud = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            FlightController SpawnAi(int index, Vector3 pos)
            {
                var model = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
                var fc = new FlightController
                {
                    PlaneModel = model,
                    Collider = PlaneCollider.Build(model),
                    PlayerIndex = FlightRoster.ShooterIdBase + index,
                    IsHumanPiloted = false,
                    Pilot = AiPilot.HoldingCourse(pos, pos + Vector3.Forward),
                    Projectiles = live,
                    UseKeyboard = false,
                    PadDevices = System.Array.Empty<int>(),
                    AllowPause = false,
                };
                fc.AddChild(model);
                fc.Setup(new FlightModel(stats), null, new CamParams(), pos, pos + Vector3.Forward);
                fc.Name = $"ai{index + 1}_{ctx.PlaneName}";
                ctx.Host.AddChild(fc);
                return fc;
            }

            ai1 = SpawnAi(0, new Vector3(0f, 500f, -800f));
            hud = TargetHud.Build(0, ctx.Camera, live);
            hud.PlanePos = new Vector3(0f, 500f, 0f);
            hud.UpdateHostile();
            ctx.Check(ReferenceEquals(hud.TrackedHostile, ai1),
                $"a plain-session pane acquires the spawned AI plane tracked={hud.TrackedHostile?.Name ?? "-"}");

            // A closer hostile joining the pool takes the marker over on the next update; the
            // per-frame nearest re-select is also how generators' runtime spawns appear.
            ai2 = SpawnAi(1, new Vector3(0f, 500f, -300f));
            hud.UpdateHostile();
            ctx.Check(ReferenceEquals(hud.TrackedHostile, ai2),
                $"a closer hostile takes the marker over tracked={hud.TrackedHostile?.Name ?? "-"}");

            // A crashed hostile is still registered but no longer live: it drops cleanly and
            // the next nearest takes over; with every hostile down the pane tracks nothing.
            ai2.DebugForceCrash();
            hud.UpdateHostile();
            ctx.Check(ReferenceEquals(hud.TrackedHostile, ai1),
                $"a crashed hostile drops and the next nearest takes over tracked={hud.TrackedHostile?.Name ?? "-"}");
            ai1.DebugForceCrash();
            hud.UpdateHostile();
            ctx.Check(hud.TrackedHostile == null,
                $"with every hostile down the pane tracks nothing");

            // A hud with no pool bound (HostilePool left null) never tracks whatever the pool
            // holds — the seam that keeps a golden shot with no hostile in play untouched.
            ai1.Respawn();
            noPoolHud = new TargetHud();
            noPoolHud.PlanePos = hud.PlanePos;
            noPoolHud.UpdateHostile();
            ctx.Check(noPoolHud.TrackedHostile == null,
                $"a hud built without a pool never tracks");

            // --debug-markers' own selection: EVERY live aircraft, not the nearest one, each
            // flagged by team against the pane's own. ai1 is live again (respawned above); ai2 is
            // still down, so it must not be marked at all.
            ai2.Team = AimAssist.PlayerTeam;
            var scan = new AimCandidateSet();
            live.CollectAircraft(scan);
            var marks = new List<(TargetRef Target, bool Friendly)>();
            TargetHud.CollectMarks(AimAssist.PlayerTeam, null, scan, marks);
            ctx.Check(marks.Count == 1 && ReferenceEquals(marks[0].Target.Source, ai1),
                $"a crashed plane is never marked marks={marks.Count}");
            ctx.Check(!marks[0].Friendly,
                $"ai1 is on the enemy team, so it marks hostile friendly={marks[0].Friendly}");
            string airframeName = PlaneRoster.PlaneDisplayName(stats);
            ctx.Check(marks[0].Target.DisplayName == airframeName
                      && marks[0].Target.Health == null && marks[0].Target.Armor == null,
                $"CollectMarks wraps a TargetRef carrying the airframe's display name, and a bare rig with no Damage ledger bound omits both figures: name={marks[0].Target.DisplayName} h={marks[0].Target.Health} a={marks[0].Target.Armor}");
            ai1.Team = AimAssist.PlayerTeam;
            marks.Clear();
            scan.Clear();
            live.CollectAircraft(scan);
            TargetHud.CollectMarks(AimAssist.PlayerTeam, null, scan, marks);
            ctx.Check(marks.Count == 1 && marks[0].Friendly,
                $"the same plane on the pane's own team marks friendly friendly={marks[0].Friendly}");
            marks.Clear();
            TargetHud.CollectMarks(AimAssist.PlayerTeam, ai1, scan, marks);
            ctx.Check(marks.Count == 0, $"the pane's own aircraft is excluded marks={marks.Count}");

            // Once the plane carries a damage ledger, CollectMarks' TargetRef reads it straight
            // off — the same optional-field contract a sub-part or emplacement would use once the
            // scan widens past aircraft, not a plane-specific field read of its own.
            ai1.Damage = new PlaneDamage(stats.DestroyableParts);
            var firstPart = stats.DestroyableParts.First();
            ai1.Damage.Apply(firstPart.Name, healthDamage: 5f, armorDamage: 5f);
            marks.Clear();
            scan.Clear();
            live.CollectAircraft(scan);
            TargetHud.CollectMarks(AimAssist.PlayerTeam, null, scan, marks);
            float expectHealth = TargetRef.Fraction(ai1.Damage.WholeHealth, ai1.Damage.WholeHealthMax)!.Value;
            float expectArmor = TargetRef.Fraction(ai1.Damage.WholeArmor, ai1.Damage.WholeArmorMax)!.Value;
            ctx.Check(marks.Count == 1 && marks[0].Target.Health == expectHealth
                      && marks[0].Target.Armor == expectArmor,
                $"a damaged plane's health/armor fractions come straight off its own Damage ledger h={marks[0].Target.Health} a={marks[0].Target.Armor} (expected h={expectHealth} a={expectArmor})");
            string damagedTag = TargetHud.DebugTag(TargetHud.HostileTag(marks[0].Target.Name),
                marks[0].Target, 640f, "");
            ctx.Check(damagedTag
                      == $"AI1 {airframeName} 640 m H{Mathf.RoundToInt(expectHealth * 100f)} A{Mathf.RoundToInt(expectArmor * 100f)}",
                $"the full string a live damaged plane produces: '{damagedTag}'");

            // The mode suffix the marker tag carries, in the engine's own vocabulary. A pilot
            // with no mode machine (this suite's own bare-orders spawn) adds nothing rather than
            // inventing a state; armed, it names whatever mode the machine is in.
            ctx.Check(TargetHud.ModeSuffix(ai1) == "",
                $"a pilot with no mode machine adds nothing to the tag: '{TargetHud.ModeSuffix(ai1)}'");
            ai1.Pilot!.Machine = new AiModeMachine(new System.Random(5));
            ai1.Pilot.Machine.Enter(AiMode.Pursue, "suite");
            ctx.Check(TargetHud.ModeSuffix(ai1).Trim() == "pursue",
                $"the marker tag carries the plane's mode: '{TargetHud.ModeSuffix(ai1).Trim()}'");
        }
        finally
        {
            hud?.Free();
            noPoolHud?.Free();
            ai1?.Free();
            ai2?.Free();
            pool?.Free();
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
        int tagged = 0;
        foreach (var n in net.Nodes)
        {
            if (n.Tags.Count > 0)
                tagged++;
        }
        ctx.Check(tagged > 0,
            $"the zeppelin route carries raw shape-A tags, preserved and acted on by nothing (stop-point vs segment id undecoded) tagged={tagged}/{net.Nodes.Count}");

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

            // Fly until three node captures, checking per-step displacement against the
            // record's own speed limit and every hop against the edge list.
            const float dt = 1f / 60f;
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

            FlightController? SpawnPlane(string planeName, Vector3 pos, Vector3 look, AiPilot pilot)
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

    // Exports a built plane to a temp .glb and asserts the file lands and re-imports with at least one
    // textured mesh: the round trip the viewer's --export-gltf=/F10 path relies on, including that the
    // shader skins convert to a glTF-serializable material.

}
