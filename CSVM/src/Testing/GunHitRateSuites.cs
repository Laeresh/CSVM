using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Roster;
using CSVM.Spec;
using Godot;

namespace CSVM.Testing;

/// <summary>The player's guns against a FLYING aeroplane, through the real fire path. That is the
/// trigger read, FireControl, the per-muzzle aim assist and the pool's swept ray. Each burst's
/// measured hit fraction is set against the fraction the shipped scatter cone alone allows.</summary>
internal static class GunHitRateSuites
{
    private const float StepDt = 1f / 60f;
    private const float BurstS = 3f;
    private const float AltitudeM = 800f;
    private const string ShooterPlane = "player_pfighter";
    private const string TargetPlane = "player_fury";
    private const int Samples = 4000;
    private const int Passes = 6;

    // Bursts per scenario, so a hit fraction rests on about a hundred rounds rather than two dozen.
    private const int Repeats = 4;

    // Below this share of its scatter-only expectation a burst is flagged as broken.
    private const float FloorShare = 0.5f;

    // LeadShare is the floor for a leading burst on a crossing target, as a share of its expectation.
    // AsternSlack bounds how far a leading astern burst strays from the plain one. A hundred rounds
    // carry a binomial spread near 0.05, so both bounds sit two to three spreads out.
    private const float LeadShare = 0.6f;
    private const float AsternSlack = 0.15f;

    // The pipper check's single-round passes, and its verdicts. With no scatter a boresight round on
    // the marker should all but always strike.
    private const int PipperPasses = 48;
    private const float PipperHitFloor = 0.8f;
    private const float PipperRayGapDeg = 0.5f;

    // A full pack folder to compare as well as <dataRoot>/HDModels, e.g. the Deck's Corvette pack.
    private const string PackEnv = "CSVM_GUN_HIT_HD_PACK";

    [Suite("gun-hit-rate",
        "the player_pfighter's stock gun groups, fired through the human trigger, FireControl and the " +
        "aim assist at a straight-flying AI player_fury dead astern at 150/300/500 m and 25 degrees off " +
        "at 300 m; every astern burst lands at least half the hit fraction the shipped scatter cone allows " +
        "with the leading option off or on, the original's lagging line trails the crossing target, and the " +
        "leading option lands near its expectation there; the snap and boresight controls are reported, and " +
        "the hit hulls are compared against the drawn meshes with and without a model pack")]
    internal static void GunHitRate(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        ProjectilePool? pool = null;
        FlightRoster? roster = null;
        FlightController? shooter = null;
        FlightController? target = null;
        PlaneStats? shooterStats = null;
        float shippedCatchup = 0f;
        try
        {
            pool = new ProjectilePool(textures, null, null);
            ctx.Host.AddChild(pool);
            var spec = SessionSpec.Parse(Array.Empty<string>());
            roster = new FlightRoster(FlightRosterPolicy.From(spec),
                new LiveryResolver(spec, System.IO.Path.Combine(ctx.DataRoot, "extracted", "rof")),
                null, ctx.Host,
                SuiteConstants.AircraftResources(ctx, planesGamez, textures),
                new FlightWorldBindings { Projectiles = pool, Gamez = planesGamez },
                new HumanRosterBindings());
            var tpos = new Vector3(0f, AltitudeM, 0f);
            target = roster.SpawnAi(new AiSpawn(TargetPlane, tpos, tpos + Vector3.Forward,
                AiPilot.HoldingCourse(tpos, tpos + Vector3.Forward), Team: AimAssist.PlayerTeam + 1));
            float targetSpeed = target.WorldVelocity.Length();

            var stats = PlaneStats.Load(ctx.ZrdrPath, ShooterPlane);
            var model = new PlaneBuilder(planesGamez, textures).Build(ShooterPlane);
            var stock = StockLoadouts.Load().For(stats.DefName);
            ctx.Check(stock != null, $"{ShooterPlane} has a stock loadout ({stats.DefName})");
            if (stock == null || target.Body == null)
                return;
            shooter = new FlightController
            {
                PlaneModel = model,
                Collider = PlaneCollider.Build(model),
                PlayerIndex = 0,
                IsHumanPiloted = true,
                Projectiles = pool,
                UseKeyboard = false,
                PadDevices = Array.Empty<int>(),
                AllowPause = false,
                Team = AimAssist.PlayerTeam,
                InfiniteAmmo = true,
                Loadout = Loadout.Bind(stock, model, weapons),
                // The target's own guns are not under test here.
                HitRouter = _ => true,
            };
            shooter.AddChild(model);
            var spos = tpos + new Vector3(0f, 0f, 300f);
            shooter.Setup(new FlightModel(stats), null, new CamParams(), spos, spos + Vector3.Forward,
                spawnThrottle: 1f, spawnSpeed: targetSpeed);
            ctx.Host.AddChild(shooter);

            var groups = shooter.Loadout!.FirableGuns.ToList();
            ctx.Note($"{ShooterPlane} gun groups: {string.Join("; ", groups.Select(g => $"{g.Mount} {g.Weapon.Id} vel={g.Weapon.Velocity ?? 0f:0} range={g.Weapon.Range ?? 0f:0} rate={g.Weapon.FireRate:0.#}/s spread={g.Weapon.CannonSpread ?? 0f:0.#}deg muzzles={g.MuzzleCount}"))}");
            ctx.Note($"assist: inaccuracy={Mathf.RadToDeg(stats.StickyBulletInaccuracy):0.##}deg catchup={stats.StickyBulletCatchupRate:0.##}/s forget={stats.StickyBulletForgetInterval:0.##}s; target {TargetPlane} at {targetSpeed:0} m/s");

            int fired = 0, hits = 0;
            bool liveDamage = false;
            double aimErrSum = 0, originErrSum = 0;
            int aimErrN = 0;
            FlightController tgt = target, sh = shooter;
            target.HitRouter = h =>
            {
                if (h.Shooter == sh.PlayerIndex)
                    hits++;
                // Swallowed unless live: a struck AI breaks away, which is not the straight target asked for.
                return !liveDamage;
            };
            shooter.WeaponFired += (w, at, dir) =>
            {
                fired++;
                var vs = sh.WorldVelocity;
                if (AimAssist.TryIntercept(at, w.Velocity ?? ProjectilePool.DefaultVelocity, tgt.WorldPosition,
                        tgt.WorldVelocity - vs, out var ideal, out _))
                {
                    aimErrSum += Mathf.RadToDeg(ideal.AngleTo(dir));
                    if (AimAssist.TryIntercept(sh.WorldPosition, w.Velocity ?? ProjectilePool.DefaultVelocity, tgt.WorldPosition,
                            tgt.WorldVelocity - vs, out var centre, out _))
                        originErrSum += Mathf.RadToDeg(centre.AngleTo(dir));
                    aimErrN++;
                }
            };

            var space = pool.GetWorld3D().DirectSpaceState;
            var targetRid = target.Body.GetRid();
            // The harness runs in one frame, so a kinematic body never moves on the server; a static one does.
            PhysicsServer3D.BodySetMode(targetRid, PhysicsServer3D.BodyMode.Static);
            void Pin() => PhysicsServer3D.BodySetState(targetRid, PhysicsServer3D.BodyState.Transform,
                tgt.Body!.GlobalTransform);

            var scenarios = new (string Name, float Range, float OffDeg, bool Assist, bool Snap, bool Lead)[]
            {
                ("astern-150", 150f, 0f, true, false, false),
                ("astern-300", 300f, 0f, true, false, false),
                ("astern-500", 500f, 0f, true, false, false),
                ("deflect25-300", 300f, 25f, true, false, false),
                ("astern-150-lead", 150f, 0f, true, false, true),
                ("astern-300-lead", 300f, 0f, true, false, true),
                ("astern-500-lead", 500f, 0f, true, false, true),
                ("deflect25-300-lead", 300f, 25f, true, false, true),
                ("astern-300-snap", 300f, 0f, true, true, false),
                ("deflect25-300-snap", 300f, 25f, true, true, false),
                ("astern-300-noassist", 300f, 0f, false, false, false),
                ("deflect25-300-noassist", 300f, 25f, false, false, false),
                ("astern-300-live", 300f, 0f, true, false, false),
            };
            var faithful = new Dictionary<(int, float), float>();
            var rng = new RandomNumberGenerator { Seed = 1234 };
            shippedCatchup = stats.StickyBulletCatchupRate;
            shooterStats = stats;
            for (int gi = 0; gi < groups.Count; gi++)
            {
                var group = groups[gi];
                float speed = group.Weapon.Velocity ?? ProjectilePool.DefaultVelocity;
                float range = group.Weapon.Range ?? 0f;
                foreach (var sc in scenarios)
                {
                    fired = hits = 0;
                    aimErrSum = originErrSum = 0;
                    aimErrN = 0;
                    float moved = 0f, endRange = 0f, expected = 0f, turned = 0f;
                    bool probeHit = false;
                    float off = Mathf.DegToRad(sc.OffDeg);
                    var s = tpos + sc.Range * new Vector3(Mathf.Sin(off), 0f, Mathf.Cos(off));
                    shooter.SelectGunGroup(gi);
                    shooter.IsHumanPiloted = sc.Assist;
                    shooter.LeadingAimAssist = sc.Lead;
                    // The snap control takes the catch-up lag out: one tick reaches the scan's answer.
                    stats.StickyBulletCatchupRate = sc.Snap ? 1f / StepDt : shippedCatchup;
                    float drift = 0f;
                    liveDamage = sc.Name.EndsWith("-live", StringComparison.Ordinal);
                    // Short passes re-placed on the scenario geometry. The rig cannot steer, and one 3 s
                    // burst drifts off a crossing target a pilot would keep tracking.
                    for (int pass = 0; pass < Passes * Repeats; pass++)
                    {
                        pool.Clear();
                        target.RespawnAt(tpos, tpos + Vector3.Forward);
                        var vt = target.WorldVelocity;
                        var nose = (tpos - s).Normalized();
                        for (int it = 0; it < 3; it++)
                        {
                            if (AimAssist.TryIntercept(s, speed, tpos, vt - nose * targetSpeed, out var lead, out _))
                                nose = lead;
                        }
                        shooter.RespawnAt(s, s + nose * 100f);
                        var placedNose = shooter.NoseDirection;
                        Pin();
                        if (pass == 0)
                        {
                            using var probe = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                                s, tpos + (tpos - s).Normalized() * 20f, CollisionLayers.Aircraft, shooter.Body!.ExcludeSelf));
                            probeHit = probe.Count > 0 && ReferenceEquals(probe["collider"].Obj, target.Body);
                            expected = Expected(space, shooter, target, group, speed, range,
                                stats.StickyBulletInaccuracy, sc.Assist, rng);
                        }

                        int burst = Mathf.RoundToInt(BurstS / Passes / StepDt);
                        int tail = Mathf.CeilToInt((sc.Range * 1.5f / speed + 0.2f) / StepDt);
                        for (int i = 0; i < burst + tail && target.InPlay && shooter.InPlay; i++)
                        {
                            shooter.AutoFire = i < burst;
                            if (i == burst)
                                drift = Mathf.Max(drift, Mathf.RadToDeg(placedNose.AngleTo(shooter.NoseDirection)));
                            float before = Combined(target);
                            Pin();
                            pool.SimStep(StepDt);
                            shooter.SimStep(StepDt);
                            target.SimStep(StepDt);
                            moved += Mathf.Max(0f, before - Combined(target));
                        }
                        shooter.AutoFire = false;
                        endRange += shooter.WorldPosition.DistanceTo(target.WorldPosition) / (Passes * Repeats);
                        turned = Mathf.Max(turned, Mathf.RadToDeg(target.NoseDirection.AngleTo(Vector3.Forward)));
                    }
                    float measured = fired > 0 ? (float)hits / fired : 0f;
                    float aimErr = aimErrN > 0 ? (float)(aimErrSum / aimErrN) : float.NaN;
                    ctx.Note($"{group.Weapon.Id} {sc.Name}: fired={fired} hits={hits} measured={measured:0.000} expected={expected:0.000} ledger damage={moved:0.#} carried armor damage={hits * (group.Weapon.ArmorDamage ?? 0f):0.#} range {sc.Range:0}->{endRange:0} m mean aim error {aimErr:0.00}deg (vs the rig-origin solve {(aimErrN > 0 ? originErrSum / aimErrN : 0):0.00}deg) target turned {turned:0.0}deg shooter nose drift {drift:0.0}deg probe={probeHit}");
                    ctx.Check(probeHit, $"{group.Weapon.Id} {sc.Name}: the pinned target body answers a ray");
                    ctx.Check(fired > 0, $"{group.Weapon.Id} {sc.Name}: the trigger fired rounds ({fired})");
                    bool plain = sc.Assist && !sc.Snap && !sc.Lead && !liveDamage;
                    CheckScenario(ctx, $"{group.Weapon.Id} {sc.Name}", sc.OffDeg > 0f, plain, sc.Lead,
                        measured, expected, faithful, (gi, sc.Range));
                }
            }
            // The lead marker against the pipper, boresight, assist off. The nose puts the pipper's
            // world ray through the marker, and a round fired so must hit the crossing target.
            var pipGroup = groups[0];
            float pipSpeed = pipGroup.Weapon.Velocity ?? ProjectilePool.DefaultVelocity;
            var pipFrom = tpos + 300f * new Vector3(Mathf.Sin(Mathf.DegToRad(25f)), 0f, Mathf.Cos(Mathf.DegToRad(25f)));
            shooter.SelectGunGroup(0);
            shooter.IsHumanPiloted = false;
            shooter.LeadingAimAssist = false;
            stats.StickyBulletCatchupRate = shippedCatchup;
            liveDamage = false;
            float PipperShots(bool worldPoint)
            {
                fired = hits = 0;
                for (int pass = 0; pass < PipperPasses; pass++)
                {
                    pool.Clear();
                    target.RespawnAt(tpos, tpos + Vector3.Forward);
                    var nose = (tpos - pipFrom).Normalized();
                    for (int it = 0; it < 3; it++)
                    {
                        shooter.RespawnAt(pipFrom, pipFrom + nose * 100f);
                        var vs = shooter.WorldVelocity;
                        var mid = pipGroup.MuzzleMidpoint();
                        if (shooter.LeadPointFor(target.WorldPosition, target.WorldVelocity) is not { } marker
                            || !AimAssist.TryIntercept(mid, pipSpeed, target.WorldPosition, target.WorldVelocity - vs, out _, out float t))
                        {
                            break;
                        }

                        var u = ((worldPoint ? marker : marker - vs * t) - mid).Normalized();
                        float b = u.Dot(vs);
                        float along = b + Mathf.Sqrt(Mathf.Max(0f, (b * b) - vs.LengthSquared() + (pipSpeed * pipSpeed)));
                        nose = ((u * along) - vs).Normalized();
                    }

                    shooter.RespawnAt(pipFrom, pipFrom + nose * 100f);
                    for (int i = 0; i < Mathf.CeilToInt(1f / StepDt); i++)
                    {
                        shooter.AutoFire = i == 0;
                        Pin();
                        pool.SimStep(StepDt);
                        shooter.SimStep(StepDt);
                        target.SimStep(StepDt);
                    }
                }

                shooter.AutoFire = false;
                return fired > 0 ? (float)hits / fired : 0f;
            }

            float onWorld = PipperShots(worldPoint: true);
            int worldFired = fired;
            float onRelative = PipperShots(worldPoint: false);
            ctx.Note($"{pipGroup.Weapon.Id} pipper on the lead marker, boresight at 25 degrees off and 300 m: world point {onWorld:0.000} of {worldFired} rounds, shooter-frame point {onRelative:0.000} of {fired}");
            ctx.Check(worldFired > 0 && onWorld >= PipperHitFloor,
                $"the pipper laid on the marker's world point hits the crossing target ({onWorld:0.000}, at least {PipperHitFloor:0.0})");
            // A plane flies along its nose, so the shooter-frame point sits on the same muzzle ray a few
            // metres nearer. Only the chase camera's offset tells the two apart, which is the parallax.
            target.RespawnAt(tpos, tpos + Vector3.Forward);
            shooter.RespawnAt(pipFrom, tpos);
            float rayGap = float.NaN;
            var gapMid = pipGroup.MuzzleMidpoint();
            var gapVs = shooter.WorldVelocity;
            if (shooter.LeadPointFor(target.WorldPosition, target.WorldVelocity) is { } gapMarker
                && AimAssist.TryIntercept(gapMid, pipSpeed, target.WorldPosition, target.WorldVelocity - gapVs, out _, out float gapT))
            {
                rayGap = Mathf.RadToDeg((gapMarker - gapMid).AngleTo(gapMarker - (gapVs * gapT) - gapMid));
            }

            ctx.Note($"the shooter-frame point stands {rayGap:0.000} deg off the marker's muzzle ray, which is why it also hits ({onRelative:0.000})");
            ctx.Check(rayGap < PipperRayGapDeg,
                $"…and the same intercept in the shooter's frame lies on the marker's muzzle ray, so the two differ only by the camera's parallax ({rayGap:0.000} deg)");
            shooter.IsHumanPiloted = true;
            shooter.LeadingAimAssist = false;

            CompareHulls(ctx, planesGamez, textures, TargetPlane);
            CompareHulls(ctx, planesGamez, textures, ShooterPlane);
            CompareHulls(ctx, planesGamez, textures, "player_autogyro");
        }
        finally
        {
            if (shooterStats != null)
                shooterStats.StickyBulletCatchupRate = shippedCatchup;
            shooter?.Free();
            roster?.ClearMembership();
            target?.Free();
            pool?.Free();
            textures.Dispose();
        }
    }

    // Monte Carlo over the shipped scatter, every muzzle aimed exactly at the intercept on the target's
    // origin. Each round is cast in the target's frame (relative velocity) against the pinned hulls.
    private static float Expected(PhysicsDirectSpaceState3D space, FlightController shooter,
        FlightController target, GunGroup group, float speed, float range, float inaccuracy, bool assist,
        RandomNumberGenerator rng)
    {
        var vs = shooter.WorldVelocity;
        var vt = target.WorldVelocity;
        int hit = 0, cast = 0;
        var forward = -shooter.GlobalTransform.Basis.Z.Normalized();
        for (int n = 0; n < Samples; n++)
        {
            var muzzle = group.Muzzles[n % group.MuzzleCount];
            var from = muzzle.GlobalPosition;
            Vector3 dir;
            if (assist)
            {
                if (!AimAssist.TryIntercept(from, speed, target.WorldPosition, vt - vs, out var ideal, out _))
                    continue;
                dir = AimAssist.Scatter(ideal, inaccuracy, rng);
            }
            else
            {
                dir = -muzzle.GlobalTransform.Basis.Z.Normalized();
                if (dir.Dot(forward) < 0.5f)
                    dir = forward;
            }
            var rel = (dir * speed + vs - vt).Normalized();
            cast++;
            using var res = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                from, from + rel * range, CollisionLayers.Aircraft, shooter.Body!.ExcludeSelf));
            if (res.Count > 0 && ReferenceEquals(res["collider"].Obj, target.Body))
                hit++;
        }
        return cast > 0 ? (float)hit / cast : 0f;
    }

    // The verdicts on the plain and the leading bursts; the controls are reported only. Astern, both
    // land at least half the scatter-only fraction and the option barely moves it. Crossing, the
    // faithful line trails the target, and the leading option reaches near the expectation.
    private static void CheckScenario(TestContext ctx, string name, bool crossing, bool plain, bool lead,
        float measured, float expected, Dictionary<(int, float), float> faithful, (int, float) key)
    {
        if (plain && !crossing)
            faithful[key] = measured;
        if (!plain && !lead)
            return;
        if (!crossing)
        {
            ctx.Check(measured >= expected * FloorShare,
                $"{name}: hit fraction {measured:0.000} is at least {FloorShare:0.0} of the scatter-only {expected:0.000}");
            if (lead && faithful.TryGetValue(key, out float before))
            {
                ctx.Check(Mathf.Abs(measured - before) <= AsternSlack,
                    $"{name}: the leading option leaves astern fire where it was, {measured:0.000} against {before:0.000} (slack {AsternSlack:0.00})");
            }

            return;
        }

        if (lead)
        {
            ctx.Check(measured >= expected * LeadShare,
                $"{name}: the leading option lands {measured:0.000}, at least {LeadShare:0.0} of the scatter-only {expected:0.000}");
        }
        else
        {
            ctx.Check(measured <= expected * FloorShare,
                $"{name}: the original's lagging line trails a crossing target, {measured:0.000} under {FloorShare:0.0} of {expected:0.000}");
        }
    }

    private static float Combined(FlightController rig) =>
        rig.Damage is { } d ? d.WholeHealth + d.WholeArmor : 0f;

    // The hit hulls' box against the drawn meshes' box, in the rig frame. Measured for the original
    // model and under every model pack the suite can find.
    private static void CompareHulls(TestContext ctx, GameZ gamez, TextureArchive textures, string plane)
    {
        var packs = new List<(string Label, string Folder)> { ("original", "csvm-no-such-pack") };
        if (System.IO.Directory.Exists(System.IO.Path.Combine(ctx.DataRoot, "HDModels")))
            packs.Add(("dataRoot HDModels", "HDModels"));
        if (System.Environment.GetEnvironmentVariable(PackEnv) is { Length: > 0 } extra && System.IO.Directory.Exists(extra))
            packs.Add((extra, extra));
        foreach (var (label, folder) in packs)
        {
            var saved = HdModels.Current;
            using var pack = HdModels.Open(ctx.DataRoot, folder);
            HdModels.Current = pack;
            Node3D? model = null;
            try
            {
                model = new PlaneBuilder(gamez, textures).Build(plane);
                var collider = PlaneCollider.Build(model);
                if (collider == null)
                {
                    ctx.Check(false, $"{plane} ({label}): hulls derive");
                    continue;
                }
                var hullBox = collider.Parts
                    .Select(p => new Aabb(p.Local.Origin + p.Hull.Bounds.Position, p.Hull.Bounds.Size))
                    .Aggregate((a, b) => a.Merge(b));
                int replaced = 0;
                var drawn = new Aabb();
                var gameplay = new Aabb();
                bool any = false;
                Walk(model, model.Transform, (mi, xf) =>
                {
                    if (mi.HasMeta(SceneBuilder.GamezMeshMeta))
                        replaced++;
                    var d = xf * mi.GetAabb();
                    var g = xf * SceneBuilder.GameplayAabb(mi);
                    drawn = any ? drawn.Merge(d) : d;
                    gameplay = any ? gameplay.Merge(g) : g;
                    any = true;
                });
                var delta = drawn.Size - hullBox.Size;
                ctx.Note($"{plane} ({label}): hulls {hullBox.Size.X:0.00}x{hullBox.Size.Y:0.00}x{hullBox.Size.Z:0.00} m at {hullBox.GetCenter()}, drawn {drawn.Size.X:0.00}x{drawn.Size.Y:0.00}x{drawn.Size.Z:0.00} m at {drawn.GetCenter()}, gamez {gameplay.Size.X:0.00}x{gameplay.Size.Y:0.00}x{gameplay.Size.Z:0.00} m, drawn-minus-hull {delta.X:0.00}/{delta.Y:0.00}/{delta.Z:0.00} m, {replaced} mesh(es) from the pack");
            }
            finally
            {
                HdModels.Current = saved;
                model?.Free();
            }
        }
    }

    private static void Walk(Node node, Transform3D xf, Action<MeshInstance3D, Transform3D> visit)
    {
        if (node is Node3D n3d)
        {
            if (!n3d.Visible)
                return;
            var kind = PropParts.Classify(n3d.Name);
            if (kind is PropParts.Kind.PropMain or PropParts.Kind.PropGhost)
                return;
            if (n3d is MeshInstance3D mi && mi.Mesh != null)
            {
                visit(mi, xf);
                return;
            }
        }
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D c)
                Walk(c, xf * c.Transform, visit);
            else
                Walk(child, xf, visit);
        }
    }
}
