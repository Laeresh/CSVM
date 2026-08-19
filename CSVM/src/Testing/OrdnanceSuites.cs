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

namespace CSVM.Testing;

/// <summary>The decoded ordnance runtime's in-engine scenario bodies (BL-406): launch velocity
/// and motors, end conditions, guidance and the beeper pair, blast falloff/cover/cap, the
/// disabling washes, smoke screens, the shootable torpedo flyout, launch axes, impact
/// orientation, and the effect-pool re-reset. Fixtures follow the same rules as the other
/// suite modules: golden numbers are measured against the retail install.</summary>
internal static class OrdnanceSuites
{
    // C10/C11 on controlled geometry: a torpedo fired straight down onto a ground plate, so the burst
    // sits at a known point on a known face and every target's near face is a measured distance
    // away. Boxes stand on the plate with a bottom edge at the exact range, so the nearest-shape
    // distance IS the range. Phase one is the curve at 0.25R/0.5R/0.75R; phase two the same target
    // without and then with a wall in the way; phase three forty targets against the 32 cap; phase
    // four a body built the way the clutter builder builds one, with a ground-level origin.
    internal static void BlastCurveCoverCap(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"weapon definitions");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        if (!weapons.TryGet("wep_14", out var torpedo)
            || torpedo.HealthDamage is not > 0f || torpedo.ImpactProximity is not > 0f)
        {
            ctx.Check(false, $"torpedo (wep_14) carries HEALTH_DAMAGE + IMPACT_PROXIMITY");
            return;
        }
        float full = torpedo.HealthDamage!.Value;
        float radius = torpedo.ImpactProximity!.Value;
        ctx.Check(Mathf.IsEqualApprox(full, 200f) && Mathf.IsEqualApprox(radius, 30f),
            $"wep_14 authors HEALTH_DAMAGE 200 and IMPACT_PROXIMITY 30 (the data this lab is scaled to)");

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        var bodies = new List<StaticBody3D>();
        StaticBody3D Box(string name, Vector3 size, Vector3 at)
        {
            var b = CombatSuites.Plate(name, size, at);
            ctx.Host.AddChild(b);
            bodies.Add(b);
            return b;
        }
        // A unit box standing on the plate whose near face is `range` metres from `burst` along
        // `dir` (a horizontal unit vector): its bottom edge toward the burst is at exactly `range`.
        StaticBody3D Target(string name, Vector3 burst, Vector3 dir, float range) =>
            Box(name, Vector3.One, burst + dir * (range + 0.5f) + new Vector3(0f, 0.5f, 0f));
        try
        {
            var plate = Box("blast-lab-plate", new Vector3(400f, 0.2f, 400f), new Vector3(0f, -0.1f, 0f));
            var recorded = new List<(Node? Body, float Damage)>();
            pool = new ProjectilePool(textures, null, null)
            {
                DamageSink = (body, damage) => { recorded.Add((body, damage)); return true; },
            };
            ctx.Host.AddChild(pool);

            // One burst: fired from 20 m straight above `at`, stepped until the plate records the
            // direct hit, then the pool is emptied so the next burst starts clean.
            void Burst(Vector3 at)
            {
                recorded.Clear();
                var muzzle = new Transform3D(Basis.LookingAt(Vector3.Down, Vector3.Forward), at + new Vector3(0f, 20f, 0f));
                pool!.Spawn(torpedo, muzzle, Vector3.Zero);
                for (int i = 0; i < 120 && recorded.Count == 0; i++)
                    pool.SimStep(1f / 60f);
                pool.Clear();
                var plateHit = recorded.FirstOrDefault(r => r.Body == plate);
                ctx.Check(plateHit.Body == plate && Mathf.IsEqualApprox(plateHit.Damage, full),
                    $"the round struck the plate under ({at.X:0},{at.Z:0}) for the full {full:0} (direct hit, unscaled)");
            }
            float DealtTo(Node body) => recorded.Where(r => r.Body == body).Sum(r => r.Damage);

            // --- the curve. Three targets on three bearings so no cover ray crosses another.
            var origin = Vector3.Zero;
            var near = Target("blast-lab-025R", origin, Vector3.Left, 0.25f * radius);
            var mid = Target("blast-lab-050R", origin, Vector3.Back, 0.5f * radius);
            var far = Target("blast-lab-075R", origin, Vector3.Forward, 0.75f * radius);
            Burst(origin);
            (StaticBody3D Body, float Fraction)[] curve = { (near, 0.25f), (mid, 0.5f), (far, 0.75f) };
            foreach (var (body, f) in curve)
            {
                float expected = full * (1f - f * f);
                float linear = full * (1f - f);
                float dealt = DealtTo(body);
                ctx.Check(Mathf.Abs(dealt - expected) < 1f,
                    $"at {f:0.00}R the splash is {dealt:0.#} against the curve's {expected:0.#} (a linear curve would give {linear:0.#})");
            }
            ctx.Check(recorded.Count(r => r.Body != plate) == 3,
                $"the three targets and nothing else took splash (dealt to {recorded.Count(r => r.Body != plate)} bodies)");
            foreach (var b in curve) { b.Body.Free(); bodies.Remove(b.Body); }

            // --- cover. The same target with nothing in the way, then a wall between.
            var site = new Vector3(0f, 0f, 100f);
            var behind = Target("blast-lab-behind", site, Vector3.Right, 20f);
            float open = full * (1f - (20f * 20f) / (radius * radius));
            Burst(site);
            float dealtOpen = DealtTo(behind);
            ctx.Check(Mathf.Abs(dealtOpen - open) < 1f,
                $"with the way clear the target 20 m out takes the curve's {open:0.#} (dealt {dealtOpen:0.#})");
            var wall = Box("blast-lab-wall", new Vector3(1f, 5f, 3f), site + new Vector3(10f, 2.5f, 0f));
            Burst(site);
            float dealtCovered = DealtTo(behind);
            ctx.Check(dealtCovered == 0f,
                $"with a wall between the burst and the target it takes nothing (dealt {dealtCovered:0.#})");
            float wallShare = full * (1f - (9.5f * 9.5f) / (radius * radius));
            ctx.Check(Mathf.Abs(DealtTo(wall) - wallShare) < 1f,
                $"the wall itself, 9.5 m out and in the open, takes the curve's {wallShare:0.#} (dealt {DealtTo(wall):0.#})");
            behind.Free(); bodies.Remove(behind);
            wall.Free(); bodies.Remove(wall);

            // --- the cap. Forty small targets on a golden-angle spiral, 4 m to 23.5 m out, all
            // inside the radius and none shadowing another; the nearest 32 take damage, the last 8
            // are dropped and the pool prints the cap line.
            var ringSite = new Vector3(100f, 0f, 0f);
            var ring = new List<StaticBody3D>();
            for (int i = 0; i < 40; i++)
            {
                float d = 4f + 0.5f * i;
                float a = Mathf.DegToRad(137.508f * i);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                ring.Add(Box($"blast-lab-ring-{i:00}", new Vector3(0.3f, 0.3f, 0.3f),
                    ringSite + dir * d + new Vector3(0f, 0.15f, 0f)));
            }
            Burst(ringSite);
            int hit = ring.Count(b => DealtTo(b) > 0f);
            ctx.Check(hit == 32, $"forty targets inside the radius, exactly 32 damaged (the original's hit buffer): {hit}");
            ctx.Check(ring.Take(32).All(b => DealtTo(b) > 0f) && ring.Skip(32).All(b => DealtTo(b) == 0f),
                $"the 32 that took damage are the nearest 32; the farthest 8 were dropped");
            foreach (var b in ring) { b.Free(); bodies.Remove(b); }

            // --- a chapter-style body: shapes attached through PhysicsServer3D.BodyAddShape with no
            // CollisionShape3D child (Clutter.BuildSolidCollision), origin sunk in the plate like a
            // mesh node's; the cover ray must aim at the box, not the origin, and must not error.
            var serverSite = new Vector3(0f, 0f, -100f);
            var serverBody = new StaticBody3D { Name = "blast-lab-server-shape" };
            var serverShape = new BoxShape3D { Size = Vector3.One };
            serverBody.SetMeta("shape_anchor", serverShape); // keeps the Ref alive, as the clutter root does
            serverBody.GlobalTransform = new Transform3D(Basis.Identity, serverSite + new Vector3(15.5f, -0.05f, 0f));
            // Attached before the body enters the space: a shape added to a body already in a space
            // waits for the next physics flush to reach the broadphase, and this suite never yields
            // a frame. In play the clutter builder's order works because frames follow.
            PhysicsServer3D.BodyAddShape(serverBody.GetRid(), serverShape.GetRid(),
                new Transform3D(Basis.Identity, new Vector3(0f, 0.55f, 0f)));
            ctx.Host.AddChild(serverBody);
            bodies.Add(serverBody);
            // The pool's effect scatter draws off the shared Weapons stream, and ai-gunnery's assist
            // verdict later in the run reads that stream's position (one extra burst here reads
            // "moved=0" there), so this phase leaves the stream where it found it.
            ulong weaponsRng = Rng.Stream(Rng.Weapons).State;
            Burst(serverSite);
            Rng.Stream(Rng.Weapons).State = weaponsRng;
            float serverShare = full * (1f - (15f * 15f) / (radius * radius));
            ctx.Check(Mathf.Abs(DealtTo(serverBody) - serverShare) < 1f,
                $"a body with server-side shapes and no shape owners, its origin sunk in the ground, takes the curve's {serverShare:0.#} at 15 m (dealt {DealtTo(serverBody):0.#})");
        }
        finally
        {
            pool?.Free();
            foreach (var b in bodies)
                b.Free();
            textures.Dispose();
        }
    }

    // A2's launch-velocity inheritance and its decay, on a live pool with no world around it: a
    // torpedo launched fast leaves fast and settles onto its authored VELOCITY over LOCK_ON
    // seconds, one launched slow barely changes, a round holding no target sheds nothing, and
    // neither a gun nor a rocket without LOCK_ON reads any differently than it does today.
    internal static void LaunchVelocityDecay(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"weapon definitions");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        if (!weapons.TryGet("wep_14", out var torpedo) || !weapons.TryGet("wep_12", out var choker)
            || !weapons.TryGet("wep_00", out var gun))
        {
            ctx.Check(false, $"wep_14, wep_12 and wep_00 all resolve");
            return;
        }
        float cruise = torpedo.Velocity ?? 0f;
        float window = torpedo.LockOn ?? 0f;
        ctx.Check(Mathf.IsEqualApprox(cruise, 60f) && Mathf.IsEqualApprox(window, 2.5f),
            $"wep_14 authors VELOCITY {cruise:0.#} and LOCK_ON {window:0.##} — the decode's 60 m/s over 2.5 s");
        ctx.Check(ProjectilePool.CarriesLockOn(torpedo) && !ProjectilePool.CarriesLockOn(choker)
                  && !ProjectilePool.CarriesLockOn(gun),
            $"the inherit-at-all flag is LOCK_ON itself: wep_14 carries it, the choker and the gun do not");
        ctx.Check(ProjectilePool.SteeringStepRuns(torpedo, hasTarget: true)
                  && !ProjectilePool.SteeringStepRuns(torpedo, hasTarget: false)
                  && !ProjectilePool.SteeringStepRuns(choker, hasTarget: true),
            $"the steering step's gate needs BOTH halves — LOCK_ON and a held target (wrong-claim 3)");

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        try
        {
            pool = new ProjectilePool(textures, null, null);
            ctx.Host.AddChild(pool);
            // Well clear of anything another suite may have left in the host, so the round's hit
            // ray finds nothing and the whole flight is the integrator's.
            var muzzle = new Transform3D(Basis.LookingAt(Vector3.Forward, Vector3.Up),
                new Vector3(0f, 2000f, 0f));
            var target = new object();
            var live = new List<(Vector3 Pos, Vector3 Velocity)>();

            float SpeedAt(WeaponDef weapon, float launchSpeed, object? held, float seconds)
            {
                pool!.Clear();
                pool.Spawn(weapon, muzzle, Vector3.Forward * launchSpeed, target: held);
                for (int i = 0; i < Mathf.RoundToInt(seconds * 60f); i++)
                {
                    pool.SimStep(1f / 60f);
                }
                live.Clear();
                pool.CollectLiveRounds(live);
                return live.Count > 0 ? live[0].Velocity.Length() : 0f;
            }

            // The decode's own worked example: a launcher at 120 m/s over the torpedo's 2.5 s.
            float fast0 = SpeedAt(torpedo, 120f, target, 0f);
            float fastHalf = SpeedAt(torpedo, 120f, target, 1.25f);
            float fastEnd = SpeedAt(torpedo, 120f, target, 2.5f);
            float fastLate = SpeedAt(torpedo, 120f, target, 4f);
            ctx.Check(Mathf.Abs(fast0 - 180f) < 0.5f,
                $"a torpedo launched at 120 m/s leaves at launcher + VELOCITY speed={fast0:0.##} expected=180");
            ctx.Check(Mathf.Abs(fastHalf - 120f) < 0.5f,
                $"half a LOCK_ON window in, half the inherited velocity is left speed={fastHalf:0.##} expected=120");
            ctx.Check(Mathf.Abs(fastEnd - 60f) < 0.5f,
                $"at LOCK_ON the round is down to its authored VELOCITY speed={fastEnd:0.##} expected=60");
            ctx.Check(Mathf.Abs(fastLate - 60f) < 0.5f,
                $"and it HOLDS there rather than continuing to fall speed={fastLate:0.##} expected=60");

            // The other half of the goal: a slow launch has almost nothing to shed.
            float slow0 = SpeedAt(torpedo, 10f, target, 0f);
            float slowEnd = SpeedAt(torpedo, 10f, target, 2.5f);
            ctx.Check(Mathf.Abs(slow0 - 70f) < 0.5f && Mathf.Abs(slowEnd - 60f) < 0.5f,
                $"a torpedo launched at 10 m/s barely slows at all launch={slow0:0.##} settled={slowEnd:0.##}");

            // ⚠ The recorded divergence: the original's decay lives inside its target-gated
            // steering step, ours runs for every LOCK_ON round, so a torpedo fired with nothing
            // selected still settles onto its authored 60 rather than flying at 180 forever.
            float untargeted = SpeedAt(torpedo, 120f, null, 4f);
            ctx.Check(Mathf.Abs(untargeted - 60f) < 0.5f,
                $"a LOCK_ON round holding NO target still sheds its launcher's velocity speed={untargeted:0.##} expected=60");

            float chokerSpeed = SpeedAt(choker, 120f, target, 0.3f);
            ctx.Check(Mathf.Abs(chokerSpeed - (choker.Velocity ?? 0f)) < 0.5f,
                $"a rocket without LOCK_ON inherits nothing at all speed={chokerSpeed:0.##} expected={choker.Velocity ?? 0f:0.##}");

            float gun0 = SpeedAt(gun, 120f, null, 0f);
            float gunLater = SpeedAt(gun, 120f, null, 0.2f);
            float gunExpected = (gun.Velocity ?? 0f) + 120f;
            ctx.Check(Mathf.Abs(gun0 - gunExpected) < 0.5f && Mathf.Abs(gunLater - gunExpected) < 0.5f,
                $"a gun round still carries its launcher's velocity, undecayed launch={gun0:0.##} later={gunLater:0.##} expected={gunExpected:0.##}");
        }
        finally
        {
            pool?.Free();
            textures.Dispose();
        }
    }

    // A3's motor on a live pool with no world around it: the four ACCELERATION carriers climb to a
    // cap seeded from VELOCITY plus the launcher's speed, hold there, and nothing anywhere slows a
    // round down. wep_26 is the launcher-speed case because it authors no LOCK_ON, so its sampled
    // world velocity is its own velocity with nothing inherited riding on top.
    internal static void MotorAcceleration(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"weapon definitions");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        if (!weapons.TryGet("wep_04", out var incendiary) || !weapons.TryGet("wep_26", out var fake)
            || !weapons.TryGet("wep_12", out var choker) || !weapons.TryGet("wep_00", out var gun))
        {
            ctx.Check(false, $"wep_04, wep_26, wep_12 and wep_00 all resolve");
            return;
        }
        float motor = incendiary.Acceleration ?? 0f;
        float cruise = incendiary.Velocity ?? 0f;
        ctx.Check(Mathf.IsEqualApprox(motor, 150f) && Mathf.IsEqualApprox(cruise, 450f),
            $"wep_04 authors ACCELERATION {motor:0.#} m/s² and VELOCITY {cruise:0.#} m/s");
        ctx.Check((choker.Acceleration ?? 0f) == 0f && (gun.Acceleration ?? 0f) == 0f,
            $"the choker and the gun author no motor at all, so they fly at VELOCITY throughout");

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        try
        {
            pool = new ProjectilePool(textures, null, null);
            ctx.Host.AddChild(pool);
            var muzzle = new Transform3D(Basis.LookingAt(Vector3.Forward, Vector3.Up),
                new Vector3(0f, 2000f, 0f));
            var live = new List<(Vector3 Pos, Vector3 Velocity)>();

            // Steps one round to `seconds`, returning its speed there and the lowest speed seen
            // along the way relative to the step before it (negative = something slowed it).
            (float Speed, float WorstDrop) Fly(WeaponDef weapon, float launchSpeed, float seconds)
            {
                pool!.Clear();
                pool.Spawn(weapon, muzzle, Vector3.Forward * launchSpeed);
                float last = -1f, worst = 0f, speed = 0f;
                for (int i = 0; i < Mathf.RoundToInt(seconds * 60f); i++)
                {
                    pool.SimStep(1f / 60f);
                    live.Clear();
                    pool.CollectLiveRounds(live);
                    if (live.Count == 0)
                        break;
                    speed = live[0].Velocity.Length();
                    if (last >= 0f)
                        worst = Mathf.Min(worst, speed - last);
                    last = speed;
                }
                return (speed, worst);
            }

            var atOne = Fly(incendiary, 0f, 1f);
            var atTwo = Fly(incendiary, 0f, 2f);
            var atThree = Fly(incendiary, 0f, 3f);
            ctx.Check(Mathf.Abs(atOne.Speed - 150f) < 0.5f && Mathf.Abs(atTwo.Speed - 300f) < 0.5f,
                $"a motor round off a standing launcher climbs at its authored rate 1s={atOne.Speed:0.#} 2s={atTwo.Speed:0.#} expected=150/300");
            ctx.Check(Mathf.Abs(atThree.Speed - cruise) < 0.5f,
                $"and reaches VELOCITY exactly as the motor's own arithmetic predicts speed={atThree.Speed:0.#} expected={cruise:0.#}");
            // Its RANGE expires at 900 m, which it passes at ~3.46 s, so this is the last sample
            // that is about the cap rather than about the round's death.
            var held = Fly(incendiary, 0f, 3.3f);
            ctx.Check(Mathf.Abs(held.Speed - cruise) < 0.5f,
                $"the cap HOLDS it rather than the motor running on speed={held.Speed:0.#} expected={cruise:0.#}");

            // wep_26's own cap sits 100 m/s higher than wep_04's off the same launcher, but it flies
            // its 900 m out at 2.86 s and the cap is 3 s away, so the flight itself only shows the
            // offset climb; Ballistics.LaunchSpeed carries the cap arithmetic under unit test.
            var launched = Fly(fake, 100f, 1f);
            var launchedLater = Fly(fake, 100f, 2f);
            ctx.Check(Mathf.Abs(launched.Speed - 250f) < 0.5f && Mathf.Abs(launchedLater.Speed - 400f) < 0.5f,
                $"a motor round off a 100 m/s launcher leaves at it and climbs from there 1s={launched.Speed:0.#} 2s={launchedLater.Speed:0.#} expected=250/400");
            var (fakeSpeed, fakeCap) = Ballistics.LaunchSpeed(fake.Velocity ?? 0f, fake.Acceleration ?? 0f, 100f);
            ctx.Check(Mathf.Abs(fakeCap - ((fake.Velocity ?? 0f) + 100f)) < 0.01f && Mathf.Abs(fakeSpeed - 100f) < 0.01f,
                $"and its cap is VELOCITY above the launcher's speed cap={fakeCap:0.#} launch={fakeSpeed:0.#}");

            var coasting = Fly(choker, 120f, 1f);
            var fired = Fly(gun, 120f, 0.8f);
            ctx.Check(atThree.WorstDrop >= -0.001f && held.WorstDrop >= -0.001f
                      && coasting.WorstDrop >= -0.001f && fired.WorstDrop >= -0.001f,
                $"no flight step reduced a round's speed, the original carrying no drag (worst step: motor={atThree.WorstDrop:0.###} capped={held.WorstDrop:0.###} rocket={coasting.WorstDrop:0.###} gun={fired.WorstDrop:0.###})");
        }
        finally
        {
            pool?.Free();
            textures.Dispose();
        }
    }

    // A4's three end conditions on a live pool, plus the RANGE_MINIMUM intersect gate that rides the
    // same travelled-distance accumulator. The detonation observer is the pool's EffectSink: a round
    // that ends by detonating hands its IMPACT row's effect name and the point it went off, and one
    // that expires quietly hands nothing, so the same seam reads all four outcomes.
    internal static void OrdnanceEndConditions(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"weapon definitions");
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        if (!weapons.TryGet("wep_24", out var he) || !weapons.TryGet("wep_12", out var choker)
            || !weapons.TryGet("wep_15", out var flare) || !weapons.TryGet("wep_08", out var sonic)
            || !weapons.TryGet("wep_14", out var torpedo))
        {
            ctx.Check(false, $"wep_24, wep_12, wep_15, wep_08 and wep_14 all resolve");
            return;
        }

        ctx.Check(he.Range is 1000f && choker.Range is 1000f && flare.Range is null,
            $"the two 1000 m rounds author RANGE and the flare authors none, taking the engine's own {ProjectilePool.DefaultRange:0} m default");
        ctx.Check(ProjectilePool.DetonatesAtRange(he) && !ProjectilePool.DetonatesAtRange(choker),
            $"a round detonates at RANGE only if its weapon carries LOCK_ON: the HE rocket does, the choker does not");
        ctx.Check(flare.DetonationTime is 2.0f && he.DetonationTime is null,
            $"the rear-arc flare is the one weapon authoring a timed fuse, at 2.0 s");
        ctx.Check(sonic.DetonationDistance is 35f && sonic.DetonationDistanceSqM is 1225f,
            $"the sonic's DETONATION_DISTANCE is 35 m, stored squared as 1225 the way the engine keeps it");
        ctx.Check(torpedo.RangeMinimum is 300f && torpedo.FlyoutHealth is 10
                  && ProjectilePool.FlyoutUnhittableAtLaunch(torpedo) && !ProjectilePool.FlyoutUnhittableAtLaunch(he),
            $"the torpedo alone carries both halves of the intersect gate (RANGE_MINIMUM 300 m and FLYOUT_HEALTH)");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? bystander = null;
        Node3D? mark = null;
        try
        {
            var effects = new List<(string Name, Vector3 At)>();
            var live = new ProjectilePool(textures, null, null)
            {
                EffectSink = (name, at, orient, ttl) => effects.Add((name, at)),
            };
            pool = live;
            ctx.Host.AddChild(live);

            // Well clear of anything another suite left in the host, so every flight below is the
            // integrator's alone until this suite registers an aircraft of its own.
            var origin = new Vector3(0f, 2000f, 0f);
            var muzzle = new Transform3D(Basis.LookingAt(Vector3.Forward, Vector3.Up), origin);
            // The round's own target: 300 m downrange and 30 m off the flight line, so a 35 m fuse
            // catches it without the round ever passing through it.
            mark = new Node3D { Name = "end-conditions-target" };
            ctx.Host.AddChild(mark);
            mark.GlobalPosition = origin + new Vector3(30f, 0f, -300f);

            var gate = new List<(float Travelled, bool Unhittable)>();

            // Flies one round until it ends or the bound runs out. `At` is where it detonated (null
            // for a quiet expiry), `Travelled` its path length on the last step it was alive, and
            // `Steps` how many steps it survived.
            (Vector3? At, float Travelled, int Steps) Fly(WeaponDef weapon, object? held, float seconds)
            {
                live.Clear();
                effects.Clear();
                live.Spawn(weapon, muzzle, Vector3.Zero, shooterId: 0, target: held);
                float travelled = 0f;
                int steps = 0;
                for (int i = 0; i < Mathf.RoundToInt(seconds * 60f); i++)
                {
                    live.SimStep(1f / 60f);
                    gate.Clear();
                    live.CollectFlyoutIntersect(gate);
                    if (gate.Count == 0)
                        break;
                    travelled = gate[0].Travelled;
                    steps++;
                }
                return (effects.Count > 0 ? effects[0].At : null, travelled, steps);
            }

            // 1. RANGE. The HE rocket ends within one step of its authored 1000 m and detonates
            // there; the choker, authoring no LOCK_ON, reaches the same 1000 m and goes out silent.
            float heStep = (he.Velocity ?? 0f) / 60f;
            var ranged = Fly(he, null, 2f);
            ctx.Check(ranged.At is { } heAt && heAt.DistanceTo(origin) > (he.Range ?? 0f) - heStep
                      && heAt.DistanceTo(origin) <= he.Range,
                $"the HE rocket detonates at its authored RANGE at={ranged.At?.DistanceTo(origin):0.#} m range={he.Range:0} step={heStep:0.#}");
            var quiet = Fly(choker, null, 2f);
            ctx.Check(quiet.At == null && quiet.Travelled > (choker.Range ?? 0f) - (choker.Velocity ?? 0f) / 60f,
                $"the choker flies the same full RANGE and expires with no detonation at all travelled={quiet.Travelled:0.#} m");

            // 2. The timed fuse, with nothing near and 498 m of range left unspent.
            var fused = Fly(flare, null, 4f);
            float fuseSeconds = fused.Steps / 60f;
            ctx.Check(fused.At != null && Mathf.Abs(fuseSeconds - 2f) < 0.05f,
                $"the flare detonates on its 2.0 s fuse t={fuseSeconds:0.###} s");
            ctx.Check(fused.Travelled < 5f,
                $"and does so two metres from the launcher, nowhere near a range expiry travelled={fused.Travelled:0.##} m");

            // 3. The round's own target, and the LOCK_ON half of its gate.
            var onTarget = Fly(sonic, mark, 3f);
            float onTargetRange = onTarget.At?.DistanceTo(origin) ?? 0f;
            ctx.Check(onTarget.At != null && onTargetRange > 270f && onTargetRange < 295f,
                $"a sonic holding a target detonates as it comes within DETONATION_DISTANCE of it at={onTargetRange:0.#} m");
            var noTarget = Fly(sonic, null, 3f);
            ctx.Check(noTarget.At is { } freeAt && freeAt.DistanceTo(origin) > 900f,
                $"the same round holding NO target flies past that point to its RANGE at={noTarget.At?.DistanceTo(origin):0.#} m");
            var gated = Fly(choker, mark, 3f);
            ctx.Check(gated.At == null && gated.Travelled > 500f,
                $"and a weapon without LOCK_ON holding the same target flies past it untouched travelled={gated.Travelled:0.#} m");

            // 4. The intersect gate, on the same accumulator. The torpedo's 60 m/s puts 300 m at
            // 5 s; the round is drawn from its first frame, only its hittability waits.
            live.Clear();
            live.Spawn(torpedo, muzzle, Vector3.Zero);
            bool unhittableEarly = true, armedLate = false;
            float armedAt = 0f;
            for (int i = 0; i < 400; i++)
            {
                live.SimStep(1f / 60f);
                gate.Clear();
                live.CollectFlyoutIntersect(gate);
                if (gate.Count == 0)
                    break;
                var (travelled, unhittable) = gate[0];
                if (travelled < (torpedo.RangeMinimum ?? 0f) && !unhittable)
                    unhittableEarly = false;
                if (!unhittable && !armedLate)
                {
                    armedLate = true;
                    armedAt = travelled;
                }
            }
            ctx.Check(unhittableEarly, $"the torpedo's intersect bit stays clear for every metre short of RANGE_MINIMUM");
            ctx.Check(armedLate && Mathf.Abs(armedAt - (torpedo.RangeMinimum ?? 0f)) < 2f,
                $"and is set as it passes it at={armedAt:0.#} m minimum={torpedo.RangeMinimum:0} m");
            live.Clear();

            // 5. The two fuse paths are separate. An aircraft 25 m off the flight line, closer to
            // the round than its own target is, and still being closed on — so the sweep is holding
            // fire (StillClosingFraction) while the per-round target fuse goes off anyway.
            var planePos = origin + new Vector3(25f, 0f, -300f);
            var model = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            bystander = new FlightController
            {
                PlaneModel = model,
                Collider = PlaneCollider.Build(model),
                Damage = new PlaneDamage(stats.DestroyableParts),
                PlayerIndex = 1,
                Projectiles = live,
                UseKeyboard = false,
                AllowPause = false,
            };
            bystander.AddChild(model);
            bystander.Setup(new FlightModel(stats), ctx.Camera, new CamParams(),
                planePos, planePos + Vector3.Forward);
            ctx.Host.AddChild(bystander);
            ctx.Check(bystander.Body != null, $"the bystander derived a collider box and built an AircraftBody");

            var beside = Fly(sonic, mark, 3f);
            float besideRange = beside.At?.DistanceTo(origin) ?? 0f;
            float toPlane = beside.At?.DistanceTo(planePos) ?? 0f;
            float toMark = beside.At?.DistanceTo(mark.GlobalPosition) ?? 0f;
            ctx.Check(beside.At != null && Mathf.Abs(besideRange - onTargetRange) < 1f,
                $"the target fuse fires in the same place with an aircraft alongside at={besideRange:0.#} m");
            ctx.Check(toPlane < toMark,
                $"and the aircraft was the NEARER of the two when it did plane={toPlane:0.#} m target={toMark:0.#} m");
            var sweptUp = Fly(sonic, null, 3f);
            float sweepRange = sweptUp.At?.DistanceTo(origin) ?? 0f;
            ctx.Check(sweptUp.At != null && sweepRange > besideRange + 5f && sweepRange < 340f,
                $"the same round holding no target is fused by the aircraft sweep instead, further downrange at={sweepRange:0.#} m");
        }
        finally
        {
            bystander?.Free();
            mark?.Free();
            pool?.Free();
            textures.Dispose();
        }
    }

    // E19/E20: the torpedo's two shootable halves. TARGETABLE puts a round on the player's Enemy
    // cycle with the right class, label and health figure and takes it off again when the round
    // ends; FLYOUT_HEALTH gives it a 10-point pool spent armour-then-health, an intersect box that
    // only exists past RANGE_MINIMUM, and a destruction that plays DESTROY_ANIMATION and does NOT
    // detonate. An ordinary rocket carries neither and is inert to both. The body itself is drawn
    // from the spawn frame in the launch look its def's RESET_STATE poses (wings and prop off).
    internal static void ShootableFlyout(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"weapon definitions");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        ctx.RequireData(ctx.MessagesPath, $"messages.json");
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, Messages.Load(ctx.MessagesPath));
        if (!weapons.TryGet("wep_14", out var torpedo) || !weapons.TryGet("wep_06", out var rocket))
        {
            ctx.Check(false, $"wep_14 and wep_06 both resolve");
            return;
        }

        var gun = weapons.All.FirstOrDefault(w => w.IsGun && w.HealthDamage is > 0f);
        if (gun == null)
        {
            ctx.Check(false, $"a gun with HEALTH_DAMAGE exists in the data");
            return;
        }

        // --- the authored halves ---------------------------------------------------------------
        ctx.Check(torpedo.Targetable && torpedo.FlyoutHealth is 10 && torpedo.ProjectileBbox is 0
                  && torpedo.DestroyAnimation == "torpedo_destroy_effect",
            $"wep_14 is the one entry carrying TARGETABLE, FLYOUT_HEALTH 10, PROJECTILE_BBOX 0 and a DESTROY_ANIMATION");
        ctx.Check(!rocket.Targetable && rocket.FlyoutHealth == null && rocket.DestroyAnimation == null
                  && rocket.DetonationDistance is > AimAssist.MinFuseDistance,
            $"wep_06 carries neither key though it IS fused, so it reaches the same fourth pool with the admission byte clear");
        ctx.Check(weapons.All.Count(w => w.Targetable) == 1 && weapons.All.Count(w => w.FlyoutHealth is > 0) == 1,
            $"and nothing else in the table authors either key");

        // --- the pair's arithmetic, off the round's own state ------------------------------------
        var seeded = ProjectilePool.SeedFlyout(torpedo, 3);
        ctx.Check(seeded is { Targetable: true, Armour: 0f, Health: 10f, HealthMax: 10f }
                  && seeded.Name == "wep_14#3",
            $"a torpedo's pair seeds from weapon +0x8c/+0x90: armour {seeded?.Armour} (the parser's literal 0) and health {seeded?.Health}");
        ctx.Check(ProjectilePool.SeedFlyout(rocket, 0) == null,
            $"an ordinary rocket gets no flyout state at all, which is the −1.0 sentinel's outcome without the per-round allocation");
        seeded!.Spend(999f, 4f);
        ctx.Check(seeded is { Armour: 0f, Health: 6f },
            $"the first hit spends HEALTH directly because the armour pool is already empty health={seeded.Health}");
        seeded.Spend(0f, 100f);
        ctx.Check(seeded is { Health: 0f, Destroyed: true },
            $"an overkill hit clamps the pool at zero rather than going negative, which is what the equality test reads");

        // The sentinel, on a lab def carrying TARGETABLE and no FLYOUT_HEALTH: the pool the original
        // seeds to −1.0 must never satisfy the == 0.0 test however hard it is hit.
        var labSentinel = ProjectilePool.SeedFlyout(
            new WeaponDef { Id = "lab_targetable", Targetable = true }, 0);
        labSentinel!.Spend(500f, 500f);
        ctx.Check(labSentinel is { Health: ProjectilePool.Flyout.NotShootable, Destroyed: false },
            $"a TARGETABLE round with no FLYOUT_HEALTH holds the −1.0 sentinel and is never destroyed health={labSentinel.Health}");

        // --- the live pool: admission, the cycle, and the end -----------------------------------
        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        try
        {
            var effects = new List<(string Name, Vector3 At)>();
            var live = new ProjectilePool(textures, null, null)
            {
                EffectSink = (name, at, orient, ttl) => effects.Add((name, at)),
            };
            pool = live;
            ctx.Host.AddChild(live);

            var origin = new Vector3(3000f, 2000f, 0f);
            var muzzle = new Transform3D(Basis.LookingAt(Vector3.Forward, Vector3.Up), origin);
            var shooterTeam = InstantActionRuntime.EnemyTeam;
            live.Spawn(torpedo, muzzle, Vector3.Zero, shooterId: 7, team: shooterTeam);
            live.Spawn(rocket, muzzle, Vector3.Zero, shooterId: 7, team: shooterTeam);

            var flyouts = new List<ProjectilePool.Flyout>();
            live.CollectFlyouts(flyouts);
            ctx.Check(flyouts.Count == 1 && flyouts[0].Targetable && flyouts[0].Health == 10f,
                $"exactly one of the two live rounds carries flyout state count={flyouts.Count}");

            var scan = new AimCandidateSet();
            live.CollectFusedOrdnance(scan);
            ctx.Check(scan.Ordnance.Count == 2
                      && scan.Ordnance.Count(c => c.Source is ProjectilePool.Flyout) == 1,
                $"both rounds are wrapped onto the fourth list, and only the TARGETABLE one carries a source count={scan.Ordnance.Count}");

            var pilot = new FlightController { PlayerIndex = 0, Team = AimAssist.PlayerTeam };
            try
            {
                var sel = new TargetSelection();
                sel.Rebuild(scan, null, pilot.Team, pilot, origin + new Vector3(0f, 0f, 400f),
                    Basis.Identity);
                ctx.Check(sel.Pool.Enemy.Count == 1 && sel.Pool.Ally.Count == 0
                          && sel.Pool.NonAircraft.Count == 0,
                    $"the torpedo is the pool's one entry and lands on the ENEMY cycle, the ordinary rocket contributing nothing");
                var round = sel.Pool.Enemy[0];
                ctx.Check(round.Kind == AimTargetKind.Ordnance && round.Name == "wep_14#0"
                          && round.DisplayName == "Aerial torpedo" && round.TypeLabel == null,
                    $"…named for --target= and labelled with the weapon's own DESC name='{round.Name}' display='{round.DisplayName}'");
                ctx.Check(round.Health is { } h && Mathf.IsEqualApprox(h, 1f) && round.Armor == null,
                    $"…carrying a full health bar and no armour figure at all health={round.Health}");
                ctx.Check(round.SortsFirst && sel.Current is { } cur && cur.IsSameTarget(round),
                    $"…sorting ahead of every sector as incoming hostile ordnance, and auto-acquired as the cycle head");

                // The end. Flying the torpedo to its RANGE takes it off the list; nothing survives it.
                for (int i = 0; i < 60 * 25; i++)
                    live.SimStep(1f / 60f);
                scan.Clear();
                live.CollectFusedOrdnance(scan);
                sel.Rebuild(scan, null, pilot.Team, pilot, origin, Basis.Identity);
                ctx.Check(scan.Ordnance.Count == 0 && sel.Pool.Count == 0 && sel.Current == null,
                    $"the entry vanishes when the round ends, and the selection drops with it");
                ctx.Check(round.Source is ProjectilePool.Flyout { Live: false },
                    $"…the retired round's own state reads dead, so a stale reference cannot be re-selected");
            }
            finally
            {
                pilot.Free();
            }

            // --- destruction: the pair emptied, then the frame check ---------------------------
            live.Clear();
            effects.Clear();
            live.Spawn(torpedo, muzzle, Vector3.Zero, shooterId: 7, team: shooterTeam);
            flyouts.Clear();
            live.CollectFlyouts(flyouts);
            int shots = 0;
            while (!flyouts[0].Destroyed && shots < 100)
            {
                flyouts[0].Spend(gun.ArmorDamage ?? 0f, gun.HealthDamage ?? 0f);
                shots++;
            }
            int expected = Mathf.CeilToInt(10f / (gun.HealthDamage ?? 1f));
            ctx.Check(shots == expected,
                $"{gun.Id}'s HEALTH_DAMAGE {gun.HealthDamage} empties the 10-point pool in {shots} hits (expected {expected})");
            live.SimStep(1f / 60f);
            flyouts.Clear();
            live.CollectFlyouts(flyouts);
            ctx.Check(flyouts.Count == 0,
                $"the frame check destroys the round the step after its health reaches zero");
            ctx.Check(effects.Count == 1 && effects[0].Name == "torpedo_destroy_effect",
                $"…playing DESTROY_ANIMATION and NOTHING else: no impact effect, no detonation ({string.Join(",", effects.Select(e => e.Name))})");
        }
        finally
        {
            pool?.Free();
            textures.Dispose();
        }

        // --- the same thing through a REAL hit ray, which needs a real flyout body ---------------
        // The box is built over the round's instanced FLYOUT MODEL, so this half wants a chapter
        // gamez; C1 carries a_torpedo. Built without collision, so nothing but the round is solid.
        ctx.WithWorld("C1", collision: false, world =>
        {
            var worldTextures = new TextureArchive(texturesPath);
            ProjectilePool? live = null;
            try
            {
                live = new ProjectilePool(worldTextures, null, null,
                    flyoutGamez: world.Gamez, flyoutScene: world.Session.Builder.Scene,
                    flyoutAnims: world.Session.Program);
                ctx.Host.AddChild(live);

                var origin = new Vector3(6000f, 3000f, 0f);
                var muzzle = new Transform3D(Basis.LookingAt(Vector3.Forward, Vector3.Up), origin);
                live.Spawn(torpedo, muzzle, Vector3.Zero, shooterId: 7,
                    team: InstantActionRuntime.EnemyTeam);
                var rounds = new List<(Vector3 Pos, Vector3 Velocity)>();
                var flyouts = new List<ProjectilePool.Flyout>();

                // The launch look: the body is drawn from the spawn frame, its wings and prop
                // hidden by torpedo_trail's RESET_STATE, and the wings come on at the def's 3.5 s.
                var bodies = new List<Node3D>();
                live.CollectFlyoutBodies(bodies);
                var body = bodies.Count > 0 ? bodies[0] : null;
                bool NodeShown(string name)
                {
                    foreach (var n in body!.FindChildren("*", "Node3D", recursive: true, owned: false))
                    {
                        if (n is Node3D n3d && n3d.HasMeta(AnimRuntime.NameMeta)
                            && n3d.GetMeta(AnimRuntime.NameMeta).AsString() == name)
                            return n3d.Visible;
                    }
                    return false;
                }
                ctx.Check(body is { Visible: true } && !NodeShown("rightwing") && !NodeShown("leftwing")
                          && !NodeShown("atprop") && NodeShown("atbody"),
                    $"a torpedo just launched is VISIBLE with wings and prop off: body={body?.Visible} rightwing={(body != null && NodeShown("rightwing"))} atbody={(body != null && NodeShown("atbody"))}");
                for (int i = 0; i < 60 * 5; i++)
                    live.SimStep(1f / 60f);
                ctx.Check(body != null && NodeShown("rightwing") && NodeShown("leftwing") && NodeShown("atprop"),
                    $"five seconds on, the def has switched the wings (3.5 s) and the prop (4.0 s) on rightwing={(body != null && NodeShown("rightwing"))} atprop={(body != null && NodeShown("atprop"))}");
                live.Clear();
                live.Spawn(torpedo, muzzle, Vector3.Zero, shooterId: 7,
                    team: InstantActionRuntime.EnemyTeam);

                // A gun round fired from five metres astern of the torpedo, along its own flight
                // line: whether it lands is the hit ray's answer, not this suite's.
                float ShootIt()
                {
                    rounds.Clear();
                    live.CollectLiveRounds(rounds);
                    if (rounds.Count == 0)
                        return -1f;
                    var dir = rounds[0].Velocity.Normalized();
                    live.Spawn(gun, new Transform3D(Basis.LookingAt(dir, Vector3.Up),
                        rounds[0].Pos - dir * 5f), Vector3.Zero);
                    live.SimStep(1f / 60f);
                    flyouts.Clear();
                    live.CollectFlyouts(flyouts);
                    return flyouts.Count > 0 ? flyouts[0].Health : -1f;
                }

                // Inside RANGE_MINIMUM the intersect bit is clear, so gunfire passes through the
                // drawn body.
                float early = ShootIt();
                ctx.Check(Mathf.IsEqualApprox(early, 10f),
                    $"a torpedo still inside RANGE_MINIMUM takes nothing from a round straight through it health={early}");

                for (int i = 0; i < 60 * 6; i++)
                    live.SimStep(1f / 60f);
                float after = ShootIt();
                ctx.Check(after >= 0f && after < 10f,
                    $"once revealed, the same shot spends {10f - after} of its 10 points through the real hit ray health={after}");

                int hits = 1;
                while (flyouts.Count > 0 && !flyouts[0].Destroyed && hits < 40)
                {
                    ShootIt();
                    hits++;
                }
                ctx.Check(flyouts.Count > 0 && flyouts[0].Destroyed
                          && hits == Mathf.CeilToInt(10f / (gun.HealthDamage ?? 1f)),
                    $"and {hits} hits of {gun.Id} empty the pool, the count its HEALTH_DAMAGE {gun.HealthDamage} predicts");
                live.SimStep(1f / 60f);
                flyouts.Clear();
                live.CollectFlyouts(flyouts);
                ctx.Check(flyouts.Count == 0, $"…after which the round is gone from the pool");

                // No shot can catch a REVEALED wep_06: at 1200 m/s it outruns every gun in the
                // table. The hidden-torpedo shot above is this rig's able-to-fail control instead.
                live.Clear();
                live.Spawn(rocket, muzzle, Vector3.Zero, shooterId: 7,
                    team: InstantActionRuntime.EnemyTeam);
                flyouts.Clear();
                live.CollectFlyouts(flyouts);
                ctx.Check(flyouts.Count == 0,
                    $"CONTROL: an ordinary rocket flying the same line has no flyout state and so no body to strike");
            }
            finally
            {
                live?.Free();
                worldTextures.Dispose();
            }
        });
    }

    // B6-B9 on a live pool with a lab tag list beside it: the turn clamp and its speed penalty, the
    // sentinel rate under the same gate, the target-free decay, LOCK_ON_LEAD's blend on a lab def
    // (no shipped carrier reaches its onset), the seeker's per-frame pick, and the beeper's paint.
    internal static void OrdnanceGuidance(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"weapon definitions");
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        if (!weapons.TryGet("wep_11", out var seeker) || !weapons.TryGet("wep_14", out var torpedo)
            || !weapons.TryGet("wep_10", out var beeper))
        {
            ctx.Check(false, $"wep_11, wep_14 and wep_10 all resolve");
            return;
        }
        const float dt = 1f / 60f;
        ctx.Check(seeker.TurnRate is 1.25f && seeker.BeeperSeeker && seeker.LockOnLead == null
                  && torpedo.TurnRate is > 0.0009f and < 0.0011f && beeper.BeeperTime is 20f,
            $"wep_11 authors TURN_RATE 1.25 and BEEPER_SEEKER with no LOCK_ON_LEAD, wep_14 the 0.001 sentinel, wep_10 TIME 20");
        var carriers = weapons.All.Where(w => w.LockOnLead != null).ToList();
        ctx.Check(carriers.Count == 3 && carriers.All(w => w.TurnRate is < 0.01f),
            $"LOCK_ON_LEAD's carriers ({string.Join(",", carriers.Select(w => w.Id))}) all pair it with the sentinel rate");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var textures = new TextureArchive(texturesPath);
        var tags = new BeeperTags<FlightController>();
        ProjectilePool? pool = null;
        Node3D? mark = null;
        var rigs = new List<FlightController>();
        try
        {
            var live = new ProjectilePool(textures, null, null) { BeeperTags = tags };
            pool = live;
            ctx.Host.AddChild(live);
            // Well clear of anything another suite left in the host.
            var origin = new Vector3(0f, 3000f, 0f);
            var muzzle = new Transform3D(Basis.LookingAt(Vector3.Forward, Vector3.Up), origin);
            var rounds = new List<(Vector3 Pos, Vector3 Velocity)>();
            var held = new List<object?>();

            FlightController BuildRig(int playerIndex, Vector3 pos, Vector3 lookAt)
            {
                var model = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
                var rig = new FlightController
                {
                    PlaneModel = model,
                    Collider = PlaneCollider.Build(model),
                    Damage = new PlaneDamage(stats.DestroyableParts),
                    PlayerIndex = playerIndex,
                    Projectiles = live,
                    UseKeyboard = false,
                    AllowPause = false,
                };
                rig.AddChild(model);
                rig.Setup(new FlightModel(stats), ctx.Camera, new CamParams(), pos, lookAt);
                ctx.Host.AddChild(rig);
                rigs.Add(rig);
                return rig;
            }

            (Vector3 Pos, Vector3 Vel)? Round()
            {
                rounds.Clear();
                live.CollectLiveRounds(rounds);
                return rounds.Count > 0 ? rounds[0] : null;
            }

            static float AngleFrom(Vector3 dir, Vector3 reference) =>
                Mathf.Acos(Mathf.Clamp(dir.Normalized().Dot(reference.Normalized()), -1f, 1f));

            // 1. The turn clamp and its penalty on a PAINTED rig dead abeam (a seeker steers only at
            // the tag list's pick, so the plain mark would be dropped on its first frame): the swing
            // is exactly TURN_RATE per second and every steering frame costs 0.8+0.2cos(clamp).
            mark = new Node3D { Name = "guidance-mark" };
            ctx.Host.AddChild(mark);
            mark.GlobalPosition = origin + new Vector3(800f, 0f, 0f);
            var beacon = BuildRig(6, mark.GlobalPosition, mark.GlobalPosition + Vector3.Forward);
            ctx.Check(tags.TryTag(AimAssist.TeamOfPilot(0), beacon, beeper.BeeperTime ?? 0f),
                $"the abeam rig is painted for the seeker to steer at");
            live.Clear();
            live.Spawn(seeker, muzzle, Vector3.Zero, shooterId: 0);
            int halfSecond = Mathf.RoundToInt(0.5f / dt);
            for (int i = 0; i < halfSecond; i++)
                live.SimStep(dt);
            var turning = Round();
            float turned = turning is { } tr ? AngleFrom(tr.Vel, Vector3.Forward) : -1f;
            float perFrame = ProjectilePool.MaxTurnRad(seeker, dt);
            ctx.Check(turning != null && Mathf.Abs(turned - halfSecond * perFrame) < 0.002f,
                $"the seeker turns at its authored TURN_RATE turned={Mathf.RadToDeg(turned):0.##}° expected={Mathf.RadToDeg(halfSecond * perFrame):0.##}° after 0.5 s");
            float expectedSpeed = (seeker.Velocity ?? 0f)
                                  * Mathf.Pow(ProjectilePool.TurnPenaltyFactor(perFrame), halfSecond);
            float turningSpeed = turning?.Vel.Length() ?? 0f;
            ctx.Check(turningSpeed < (seeker.Velocity ?? 0f) && Mathf.Abs(turningSpeed - expectedSpeed) < 0.05f,
                $"and loses speed by the per-frame penalty exactly speed={turningSpeed:0.###} expected={expectedSpeed:0.###} of {seeker.Velocity:0}");
            // Once aligned it stops paying: the angle each frame is zero.
            for (int i = 0; i < Mathf.RoundToInt(1.5f / dt); i++)
                live.SimStep(dt);
            var aligned = Round();
            ctx.Check(aligned is { } al && AngleFrom(al.Vel, mark.GlobalPosition - al.Pos) < 0.01f
                      && al.Vel.Length() > expectedSpeed - 2f,
                $"after the swing it is on the mark's bearing and its speed has settled speed={aligned?.Vel.Length():0.##}");
            tags.Clear();

            // 2. The dumbfire sentinel under the same gate: the torpedo IS steered (a target and
            // LOCK_ON), by 0.001 rad/s, so 4 s buys it 0.23° toward a mark 90° off.
            live.Clear();
            live.Spawn(torpedo, muzzle, Vector3.Zero, target: mark);
            for (int i = 0; i < Mathf.RoundToInt(4f / dt); i++)
                live.SimStep(dt);
            var dumb = Round();
            float dumbTurn = dumb is { } db ? AngleFrom(db.Vel, Vector3.Forward) : -1f;
            ctx.Check(dumb != null && dumbTurn > 0.003f && dumbTurn < 0.005f,
                $"a sentinel-rate round with a target flies effectively straight, turning only its 0.001 rad/s turned={Mathf.RadToDeg(dumbTurn):0.###}° in 4 s");

            // 3. No target: the launcher's velocity still decays (the recorded divergence) and the
            // heading never moves.
            live.Clear();
            live.Spawn(torpedo, muzzle, Vector3.Forward * 120f);
            for (int i = 0; i < Mathf.RoundToInt(2.5f / dt); i++)
                live.SimStep(dt);
            var free = Round();
            float freeTurn = free is { } fr ? AngleFrom(fr.Vel, Vector3.Forward) : -1f;
            ctx.Check(free != null && Mathf.Abs(free.Value.Vel.Length() - 60f) < 0.5f && freeTurn < 1e-4f,
                $"a LOCK_ON round holding no target decays to its own 60 m/s and does not turn speed={free?.Vel.Length():0.##} turned={Mathf.RadToDeg(freeTurn):0.####}°");

            // 4. LOCK_ON_LEAD. No shipped carrier reaches its element 0 before RANGE (wep_04 off a
            // standing launcher, its slowest case, is gone by 3.5 s of its 4 s onset), so the blend
            // is exercised on a lab def below, snapping to the desired direction every frame.
            if (weapons.TryGet("wep_04", out var incendiary))
            {
                live.Clear();
                live.Spawn(incendiary, muzzle, Vector3.Zero, target: mark);
                int alive = 0;
                for (int i = 0; i < Mathf.RoundToInt(4f / dt) && Round() != null; i++)
                {
                    live.SimStep(dt);
                    alive++;
                }
                float ended = alive * dt;
                ctx.Check(ended < (incendiary.LockOnLead?.Item1 ?? 0f),
                    $"wep_04 off a standing launcher ends at {ended:0.##} s, before its LOCK_ON_LEAD onset at {incendiary.LockOnLead?.Item1:0} s");
            }
            // The heading after each step is that frame's desired direction, compared with the
            // bearing and the AimAssist.TryIntercept solve computed off the same pre-step state of
            // the round and a real rig crossing at its spawn speed.
            var lab = new WeaponDef
            {
                Id = "lab-lead",
                Name = "LAB LEAD",
                IsRocket = true,
                Velocity = seeker.Velocity,
                LockOn = seeker.LockOn,
                LockOnLead = (4f, 8f),
                TurnRate = 20f,
                Range = 10000f,
            };
            var crossing = BuildRig(1, origin + new Vector3(-2000f, 0f, -5000f),
                origin + new Vector3(-1000f, 0f, -5000f));
            ctx.Check(crossing.WorldVelocity.Length() > 10f,
                $"the crossing rig flies at its spawn speed {crossing.WorldVelocity.Length():0.#} m/s");
            live.Clear();
            live.Spawn(lab, muzzle, Vector3.Zero, target: crossing);
            bool bearingBefore = true, leadAfter = true, monotone = true, blendOk = true, solved = true;
            float lastFraction = 0f, fractionAtOnset = -1f, fractionNearEnd = -1f;
            int frames = 0;
            for (int k = 0; k < Mathf.RoundToInt(9f / dt); k++)
            {
                if (Round() is not { } before)
                    break;
                float age = k * dt;
                var targetPos = crossing.WorldPosition;
                var targetVel = crossing.WorldVelocity;
                var bearing = (targetPos - before.Pos).Normalized();
                if (!AimAssist.TryIntercept(before.Pos, before.Vel.Length(), targetPos, targetVel, out var lead, out _))
                {
                    solved = false;
                    break;
                }
                live.SimStep(dt);
                crossing.SimStep(dt);
                if (Round() is not { } after || k < 2)
                    continue;
                frames++;
                var heading = after.Vel.Normalized();
                if (age < 4f)
                {
                    bearingBefore &= heading.Dot(bearing) > 0.99999f;
                }
                else if (age < 8f)
                {
                    float fraction = AngleFrom(heading, bearing) / AngleFrom(lead, bearing);
                    if (fractionAtOnset < 0f)
                        fractionAtOnset = fraction;
                    if (age > 7.9f)
                        fractionNearEnd = fraction;
                    blendOk &= Mathf.Abs(fraction - (age - 4f) / 4f) < 0.03f;
                    monotone &= fraction >= lastFraction - 1e-3f;
                    lastFraction = fraction;
                }
                else
                {
                    leadAfter &= heading.Dot(lead) > 0.99999f;
                }
            }
            ctx.Check(solved && frames > 500,
                $"the lead solve had an answer on every frame and the round flew the whole 9 s frames={frames}");
            ctx.Check(bearingBefore, $"below element 0 the desired direction is the plain bearing");
            ctx.Check(blendOk && monotone && fractionAtOnset is >= 0f and < 0.03f && fractionNearEnd > 0.95f,
                $"between elements the heading eases from bearing to lead, no step at the onset onset={fractionAtOnset:0.###} near-end={fractionNearEnd:0.###}");
            ctx.Check(leadAfter, $"from element 1 on the desired direction is the full intercept");
            // The crossing rig stays registered with the pool until the suite's teardown (its
            // AircraftBody is on the pool's roster; freeing it here would leave a disposed body
            // there), 5 km away from everything below.

            // 5. The seeker's pick. Two painted rigs and one unpainted, the seeker launched HOLDING
            // the unpainted one: the tag list's pick replaces it on the first frame and every frame
            // after, the unpainted rig is never held, and an emptied list clears the slot.
            var ahead = BuildRig(2, origin + new Vector3(0f, 0f, -1000f), origin + new Vector3(0f, 0f, -1100f));
            var abeam = BuildRig(3, origin + new Vector3(475f, 0f, -823f), origin + new Vector3(475f, 0f, -923f));
            var unpainted = BuildRig(4, origin + new Vector3(-250f, 0f, -300f), origin + new Vector3(-250f, 0f, -400f));
            int shooterTeam = AimAssist.TeamOfPilot(0);
            bool taggedAhead = tags.TryTag(shooterTeam, ahead, beeper.BeeperTime ?? 0f);
            tags.SimStep(dt);
            bool taggedAbeam = tags.TryTag(shooterTeam, abeam, beeper.BeeperTime ?? 0f);
            ctx.Check(taggedAhead && taggedAbeam && tags.PaintingCount == 2 && !tags.IsPainted(unpainted),
                $"two rigs are painted for the pick and the third is not");
            live.Clear();
            live.Spawn(seeker, muzzle, Vector3.Zero, shooterId: 0, target: unpainted);
            bool matchesRule = true, neverUnpainted = true, firstIsAbeam = false;
            int picks = 0;
            for (int k = 0; k < Mathf.RoundToInt(1.5f / dt); k++)
            {
                if (Round() is not { } before)
                    break;
                var expected = tags.PickTarget(before.Pos, before.Vel.Normalized());
                live.SimStep(dt);
                tags.SimStep(dt);
                held.Clear();
                live.CollectHeldTargets(held);
                if (held.Count == 0)
                    break;
                picks++;
                if (k == 0)
                    firstIsAbeam = ReferenceEquals(held[0], abeam);
                matchesRule &= ReferenceEquals(held[0], expected);
                neverUnpainted &= !ReferenceEquals(held[0], unpainted);
            }
            ctx.Check(picks > 80 && matchesRule,
                $"the seeker's held target is PickTarget's answer on every one of {picks} frames");
            ctx.Check(firstIsAbeam,
                $"the launch target is overridden on the first frame: the nearer rig 30° off steals the pick from the one dead ahead (range leads)");
            ctx.Check(neverUnpainted, $"the unpainted rig is never held");
            tags.Clear();
            live.SimStep(dt);
            held.Clear();
            live.CollectHeldTargets(held);
            ctx.Check(held.Count == 1 && held[0] == null,
                $"with nothing painted the seeker holds nothing, as the callback writes zeros");
            live.Clear();

            // 6. The beeper's paint. A round into a hostile rig's tail tags it for TIME and spends
            // nothing on it; the tag expires at TIME, a fresh one lands in the tail, and a crash
            // collapses it on the next step.
            var victim = BuildRig(5, origin + new Vector3(0f, -400f, -600f), origin + new Vector3(0f, -400f, -700f));
            bool Pristine(FlightController rig) => rig.Damage!.Parts.Values.All(
                p => p.Hp >= p.Def.MaxHp && p.Armor >= p.Def.MaxArmor);
            var tailMuzzle = new Transform3D(Basis.LookingAt(Vector3.Forward, Vector3.Up),
                victim.WorldPosition + new Vector3(0f, 0f, 40f));
            int Paint()
            {
                live.Spawn(beeper, tailMuzzle, Vector3.Zero, shooterId: 0);
                int steps = 0;
                while (Round() != null && steps < 30)
                {
                    live.SimStep(dt);
                    tags.SimStep(dt);
                    steps++;
                }
                return steps;
            }
            int hitSteps = Paint();
            float? remaining = tags.RemainingFor(victim);
            ctx.Check(hitSteps < 30 && tags.IsPainted(victim) && remaining is { } r0
                      && Mathf.Abs(r0 - ((beeper.BeeperTime ?? 0f) - hitSteps * dt)) < 0.05f,
                $"a wep_10 into a hostile rig paints it for TIME steps={hitSteps} remaining={remaining:0.###} of {beeper.BeeperTime:0}");
            ctx.Check(Pristine(victim) && victim.InPlay,
                $"and the damage ledger is untouched: no zone lost a point");
            for (int i = 0; i < Mathf.RoundToInt(19.5f / dt) - hitSteps; i++)
                tags.SimStep(dt);
            bool paintedLate = tags.IsPainted(victim);
            for (int i = 0; i < Mathf.RoundToInt(0.6f / dt); i++)
                tags.SimStep(dt);
            ctx.Check(paintedLate && !tags.IsPainted(victim),
                $"the paint lasts TIME: still on at 19.5 s, off past 20 s");
            int again = Paint();
            bool repainted = tags.IsPainted(victim);
            victim.DebugForceCrash();
            tags.SimStep(dt);
            ctx.Check(again < 30 && repainted && !tags.IsPainted(victim) && tags.Count > 0,
                $"a fresh tag lands on a rig whose first is in its tail, and collapses the step its rig dies (tail entries={tags.Count})");
        }
        finally
        {
            foreach (var rig in rigs)
                rig.Free();
            mark?.Free();
            pool?.Free();
            textures.Dispose();
        }
    }

    // D18's mechanism over a live roster, with the launch hook stood in for by a direct Lay: the
    // layer is a human rig flying -Z, so its backward axis is +Z and everything at +Z of it is
    // "behind". Two AI and two more humans sit where the cone must catch or miss them, the pool
    // and the flight models run for real, and the wash goes through a real two-pane ScreenFlash
    // wrapped by a recording sink so the third human (no pane) is still observable.
    internal static void SmokeScreenSuite(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        if (!weapons.TryGet("wep_13", out var smoker) || smoker.SmokeScreenTime is not { } screenTime)
        {
            ctx.Check(false, $"wep_13 resolves and carries a SMOKE_SCREEN TIME");
            return;
        }
        ctx.Check(Mathf.IsEqualApprox(screenTime, 8f), $"wep_13 authors SMOKE_SCREEN TIME {screenTime:0.#}, the decode's 8 s");
        var tunables = SmokeScreenTunables.Load(ctx.ZrdrPath);
        ctx.Check(Mathf.IsEqualApprox(tunables.RangeM, 600f) && Mathf.IsEqualApprox(tunables.StunIntervalS, 5f)
                  && Mathf.Abs(tunables.HalfAngleCos - Mathf.Cos(Mathf.DegToRad(85f))) < 1e-5f,
            $"player.json reads 600 m, 5 s and 170° stored as cos 85° ({tunables.RangeM:0}/{tunables.StunIntervalS:0}/{tunables.HalfAngleCos:0.####})");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        var rigs = new List<FlightController>();
        ScreenFlash? flash = null;
        Node[] panes = System.Array.Empty<Node>();
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            FlightController BuildRig(int playerIndex, bool human, Vector3 pos)
            {
                var model = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
                var rig = new FlightController
                {
                    PlaneModel = model,
                    Collider = PlaneCollider.Build(model),
                    Damage = new PlaneDamage(stats.DestroyableParts),
                    PlayerIndex = playerIndex,
                    IsHumanPiloted = human,
                    Pilot = human ? null : AiPilot.HoldingCourse(pos, pos + Vector3.Forward),
                    Projectiles = live,
                    UseKeyboard = false,
                    PadDevices = System.Array.Empty<int>(),
                    AllowPause = false,
                };
                rig.AddChild(model);
                rig.Setup(new FlightModel(stats), human ? ctx.Camera : null, new CamParams(), pos, pos + Vector3.Forward);
                rig.Name = (human ? "p" : "ai") + playerIndex;
                ctx.Host.AddChild(rig);
                live.RegisterAircraft(rig.Body!);
                // Held at the spawn pose (the weapon lab's pin, not the clock halt: the rigs still
                // step): five airframes in two lanes would otherwise overtake and ram each other
                // inside the 8 s, and the geometry the cone is asserted against must stand still.
                rig.Held = true;
                rigs.Add(rig);
                return rig;
            }

            var layerPos = new Vector3(0f, 500f, 0f);
            var layer = BuildRig(0, human: true, layerPos);
            var humanBehind = BuildRig(1, human: true, layerPos + new Vector3(0f, 0f, 200f));
            var humanAhead = BuildRig(2, human: true, layerPos + new Vector3(0f, 0f, -400f));
            var aiBehind = BuildRig(FlightRoster.ShooterIdBase, human: false, layerPos + new Vector3(0f, 0f, 300f));
            // 97° off the backward axis at 400 m: inside the range, outside the 85° edge.
            var aiSide = BuildRig(FlightRoster.ShooterIdBase + 1, human: false, layerPos + new Vector3(397f, 0f, -49f));

            (flash, panes) = WorldAndToolSuites.PaneFlash(ctx, null);
            var washes = new List<(int Player, float Weight, float Time)>();
            float clock = 0f;
            var flashSink = flash;
            var screens = new SmokeScreens(tunables, () => rigs, (player, colour, weight, duration) =>
            {
                washes.Add((player, weight, clock));
                flashSink.PlayBlend(player, colour, weight, duration);
            });
            // The emitter seam through a recorder rather than a Puffer: what this suite owes is
            // that a screen starts one, drives it at the layer's live pose and stops it at the end,
            // which is the pairing the real particle runtime cannot be asked about without a GPU.
            var laid = new List<RecordingSmokeEmitter>();
            screens.Emitters = () =>
            {
                var fresh = new RecordingSmokeEmitter();
                laid.Add(fresh);
                return fresh;
            };
            // The authored states the real factory reads, off the compiled chapter archive the
            // session's own program is built from (docs/architecture.md, SmokeScreens.cs: the
            // reader form of the same definition carries no DISTANCE_INTERVAL).
            var (chapterAnim, _) = AnimProgram.ArchivePaths(ctx.DataRoot, "C1", "IA1");
            var authored = new SmokeScreenEmitters(
                AnimArchive.Load(chapterAnim, "cam_anim")?.Defs, textures, ctx.Host);
            ctx.Check(authored.StateCount == 2,
                $"generate_smokescreen authors {authored.StateCount} DISTANCE_INTERVAL puffer state(s), the decode's two");
            if (authored.StateCount == 2)
                SmokeScreenCloud(ctx, authored.States[0]);

            const float dt = 1f / 60f;
            void Step()
            {
                foreach (var rig in rigs)
                    rig.SimStep(dt);
                screens.SimStep(dt);
                flash.Advance(dt);
                clock += dt;
            }

            for (int i = 0; i < 30; i++)
                Step();
            ctx.Check(!aiBehind.Pilot!.IsStunned && washes.Count == 0 && screens.ActiveCount == 0,
                $"nothing happens to anyone before a screen is laid stunned={aiBehind.Pilot.IsStunned} washes={washes.Count}");

            screens.Lay(layer, screenTime);
            ctx.Check(screens.ActiveCount == 1 && screens.IsLaying(layer), $"Lay registers one running screen on the layer");
            ctx.Check(laid.Count == 1 && laid[0].HomedAtLaunch && !laid[0].Stopped,
                $"the lay starts one emitter, homed with a zero step at the launch pose emitters={laid.Count}");
            Step();
            ctx.Check(aiBehind.Pilot.IsStunned && Mathf.Abs(aiBehind.Pilot.StunRemainingS - tunables.StunIntervalS) < 0.05f,
                $"the AI 300 m dead astern is stunned on the first step for smokescreen_stun_interval remaining={aiBehind.Pilot.StunRemainingS:0.00}");
            ctx.Check(!aiSide.Pilot!.IsStunned,
                $"the AI 400 m out at 97° off the backward axis is not touched");
            ctx.Check(washes.Count == 1 && washes[0].Player == 1 && Mathf.IsEqualApprox(washes[0].Weight, 0.97f),
                $"the human 200 m behind gets the first-hit wash at 0.97 on its own player index washes={washes.Count} first={(washes.Count > 0 ? $"P{washes[0].Player + 1}@{washes[0].Weight:0.00}" : "-")}");

            // The rest of the 8 s: the AI's stun is refreshed every step it stays inside, so it
            // never runs down while the screen runs; the human is re-washed every 1.5 s at 0.9.
            float lowestStun = float.MaxValue;
            while (clock < 0.5f + screenTime - dt)
            {
                Step();
                if (screens.ActiveCount > 0)
                    lowestStun = Mathf.Min(lowestStun, aiBehind.Pilot.StunRemainingS);
            }
            ctx.Check(lowestStun > tunables.StunIntervalS - 0.1f,
                $"the per-step refresh holds the AI's remaining stun at the interval for the whole screen lowest={lowestStun:0.00}");
            ctx.Check(!aiSide.Pilot.IsStunned, $"the AI beyond 85° stays untouched for the whole screen");
            int rewashes = washes.Count(w => w.Player == 1) - 1;
            ctx.Check(rewashes >= 4 && washes.Where(w => w.Player == 1).Skip(1).All(w => Mathf.IsEqualApprox(w.Weight, 0.9f)),
                $"the human behind is re-washed at 0.9 while it stays inside rewashes={rewashes} (1.5 s apart over 8 s)");
            var gaps = washes.Where(w => w.Player == 1).Select(w => w.Time).ToList();
            bool spaced = true;
            for (int i = 1; i < gaps.Count; i++)
                spaced &= Mathf.Abs(gaps[i] - gaps[i - 1] - 1.5f) < 0.05f;
            ctx.Check(spaced, $"and each re-wash comes 1.5 s after the last, the re-arm firing under 0.5 s of the 2 s timer");
            ctx.Check(washes.All(w => w.Player == 1),
                $"neither the layer (P1) nor the human 400 m ahead (P3) is ever washed players={string.Join(",", washes.Select(w => w.Player).Distinct())}");
            var pane2 = flash.CurrentFor(1);
            ctx.Check(pane2.A > 0.5f && pane2.G > pane2.R && pane2.R > pane2.B,
                $"the human behind's pane carries the grey-green wash ({pane2})");
            ctx.Check(flash.CurrentFor(0).IsEqualApprox(new Color(0f, 0f, 0f, 0f)),
                $"the layer's own pane stays clear ({flash.CurrentFor(0)})");
            ctx.Check(!laid[0].Stopped && laid[0].Steps > 400
                      && laid[0].LastPos.IsEqualApprox(layer.WorldPosition)
                      && laid[0].LastBasis.Z.IsEqualApprox(-layer.NoseDirection),
                $"the emitter runs at the layer's live pose for the whole screen steps={laid[0].Steps}");

            // Expiry: the screen is gone at TIME, and the AI's last refresh runs down and frees it.
            for (int i = 0; i < 6; i++)
                Step();
            ctx.Check(screens.ActiveCount == 0, $"the screen has expired at its TIME active={screens.ActiveCount}");
            int stepsAtExpiry = laid[0].Steps;
            ctx.Check(laid[0].Stopped, $"and its emitter is stopped with it, laying no more smoke");
            float atExpiry = aiBehind.Pilot.StunRemainingS;
            // Released so its pilot flies (and counts its stun down) again; a held airframe reads
            // no pilot input at all.
            aiBehind.Held = false;
            for (int i = 0; i < Mathf.RoundToInt((tunables.StunIntervalS + 0.5f) * 60f); i++)
                Step();
            ctx.Check(atExpiry > 0f && !aiBehind.Pilot.IsStunned,
                $"the AI recovers once its last stun runs out after the screen expires atExpiry={atExpiry:0.00} stunned={aiBehind.Pilot.IsStunned}");
            ctx.Check(laid[0].Steps == stepsAtExpiry,
                $"the stopped emitter takes no further step over the recovery run steps={laid[0].Steps}");

            // A layer going down ends its screen on the spot: nothing walks for a dead layer.
            int washesBefore = washes.Count;
            screens.Lay(layer, screenTime);
            layer.DebugForceCrash();
            Step();
            ctx.Check(screens.ActiveCount == 0 && washes.Count == washesBefore,
                $"a screen whose layer is no longer in play ends immediately and hits nobody active={screens.ActiveCount}");
            ctx.Check(laid.Count == 2 && laid[1].Stopped && laid[1].Steps == 0,
                $"a downed layer's emitter is stopped on the same step, having laid nothing emitters={laid.Count}");
        }
        finally
        {
            flash?.Free();
            foreach (var pane in panes)
                pane.Free();
            foreach (var rig in rigs)
                rig.Free();
            pool?.Free();
            textures.Dispose();
        }
    }

    // D15/D16/D17's hit side on a live pool: two human rigs on a real two-pane ScreenFlash and two
    // AI rigs, all held so every distance is the one laid out here, shot in turn with the sonic,
    // the flash and the choker. What the pool owes is the routing (a human's pane, an AI's pilot,
    // an engine either way) and the numbers the decoded curves give at the measured distances.
    internal static void DisablingHits(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        if (!weapons.TryGet("wep_08", out var sonic) || !weapons.TryGet("wep_09", out var flashRocket)
            || !weapons.TryGet("wep_12", out var choker))
        {
            ctx.Check(false, $"wep_08, wep_09 and wep_12 all resolve");
            return;
        }
        ctx.Check(sonic.Sonic && !sonic.Flash && sonic.ImpactProximity is 35f && flashRocket.Flash
                  && flashRocket.ImpactProximity is 450f && choker.Tangler is { Radius: 35f, Time: 2f },
            $"wep_08 is SONIC at 35 m, wep_09 FLASH at 450 m, wep_12 a TANGLER of RADIUS 35 and TIME 2");
        var bounds = TanglerChoke.EngineDeadBounds(weapons);
        ctx.Check(bounds == (5f, 13f), $"the catalogue's ENGINE_DEAD pair is [5, 13] ({bounds.Min}, {bounds.Max})");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        var rigs = new List<FlightController>();
        ScreenFlash? flash = null;
        Node[] panes = System.Array.Empty<Node>();
        try
        {
            (flash, panes) = WorldAndToolSuites.PaneFlash(ctx, null);
            var washes = new List<(int Player, Color Colour, float Weight, float Duration, float Delay)>();
            var flashSink = flash;
            var live = new ProjectilePool(textures, null, null)
            {
                EngineDeadBounds = bounds,
                WashSink = (player, colour, weight, duration, delay) =>
                {
                    washes.Add((player, colour, weight, duration, delay));
                    flashSink.PlayBlend(player, colour, weight, duration, delay);
                },
            };
            pool = live;
            ctx.Host.AddChild(live);

            FlightController BuildRig(int playerIndex, bool human, Vector3 pos)
            {
                var model = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
                var rig = new FlightController
                {
                    PlaneModel = model,
                    Collider = PlaneCollider.Build(model),
                    Damage = new PlaneDamage(stats.DestroyableParts),
                    PlayerIndex = playerIndex,
                    IsHumanPiloted = human,
                    Pilot = human ? null : AiPilot.HoldingCourse(pos, pos + Vector3.Forward),
                    Projectiles = live,
                    UseKeyboard = false,
                    PadDevices = System.Array.Empty<int>(),
                    AllowPause = false,
                };
                rig.AddChild(model);
                rig.Setup(new FlightModel(stats), human ? ctx.Camera : null, new CamParams(), pos, pos + Vector3.Forward);
                rig.Name = (human ? "p" : "ai") + playerIndex;
                ctx.Host.AddChild(rig); // _Ready registers the body with the pool
                rig.Held = true;
                rigs.Add(rig);
                return rig;
            }
            bool Pristine(FlightController rig) => rig.Damage!.Parts.Values.All(
                p => p.Hp >= p.Def.MaxHp && p.Armor >= p.Def.MaxArmor);

            // Every rig noses along -Z. The two humans a kilometre apart so no burst reaches both;
            // the choker's AI 20 m beside the second human, inside the sonic's 35 m and the cloud's.
            var origin = new Vector3(0f, 3500f, 0f);
            var human0 = BuildRig(0, human: true, origin);
            var human1 = BuildRig(1, human: true, origin + new Vector3(1000f, 0f, 0f));
            var aiNear = BuildRig(FlightRoster.ShooterIdBase, human: false, origin + new Vector3(1020f, 0f, 0f));
            var aiFar = BuildRig(FlightRoster.ShooterIdBase + 1, human: false, origin + new Vector3(2000f, 0f, 0f));

            const float dt = 1f / 60f;
            var rounds = new List<(Vector3 Pos, Vector3 Velocity)>();
            bool RoundsAlive()
            {
                rounds.Clear();
                live.CollectLiveRounds(rounds);
                return rounds.Count > 0;
            }
            // Fires and steps until the round is gone; the flash advances on the same clock.
            int Fire(WeaponDef weapon, Vector3 from, Vector3 along)
            {
                var up = Mathf.Abs(along.Normalized().Dot(Vector3.Up)) > 0.99f ? Vector3.Forward : Vector3.Up;
                live.Spawn(weapon, new Transform3D(Basis.LookingAt(along, up), from), Vector3.Zero);
                int steps = 0;
                do
                {
                    foreach (var rig in rigs)
                        rig.SimStep(dt);
                    live.SimStep(dt);
                    flash.Advance(dt);
                    steps++;
                }
                while (RoundsAlive() && steps < 120);
                return steps;
            }
            void Advance(float seconds)
            {
                for (int i = 0; i < Mathf.RoundToInt(seconds / dt); i++)
                {
                    foreach (var rig in rigs)
                        rig.SimStep(dt);
                    live.SimStep(dt);
                    flash.Advance(dt);
                }
            }
            Vector3 Tail(FlightController rig) => rig.WorldPosition + new Vector3(0f, 0f, 40f);
            Vector3 Nose(FlightController rig) => rig.WorldPosition + new Vector3(0f, 0f, -40f);
            bool Clear(int pane) => flash.CurrentFor(pane).IsEqualApprox(new Color(0f, 0f, 0f, 0f));

            // 1. The sonic into a human's tail: one wash, red, full weight, five seconds, on that
            // pane alone, painting only after the 1 s delay and gone at the duration.
            int steps = Fire(sonic, Tail(human0), Vector3.Forward);
            ctx.Check(steps < 120 && washes.Count == 1 && washes[0].Player == 0
                      && washes[0].Colour == new Color(1f, 0f, 0f) && washes[0].Weight > 0.999f
                      && Mathf.Abs(washes[0].Duration - 5f) < 0.01f && washes[0].Delay == 1f,
                $"a wep_08 into P1's tail washes pane 1 red at weight 1 for 5 s after 1 s (steps={steps} washes={washes.Count} first={(washes.Count > 0 ? $"P{washes[0].Player + 1} {washes[0].Colour} w={washes[0].Weight:0.###} d={washes[0].Duration:0.##} delay={washes[0].Delay}" : "-")})");
            Advance(0.5f);
            ctx.Check(Clear(0) && Clear(1), $"nothing paints during the start delay ({flash.CurrentFor(0)})");
            Advance(1.5f);
            var pane1 = flash.CurrentFor(0);
            ctx.Check(pane1.A > 0.9f && pane1.R > 0.9f && pane1.G < 0.1f && pane1.B < 0.1f && Clear(1),
                $"past the delay pane 1 is washed red ({pane1}) and pane 2 stays clear ({flash.CurrentFor(1)})");
            Advance(4.5f);
            ctx.Check(Clear(0), $"the wash is gone at its 5 s duration ({flash.CurrentFor(0)})");
            ctx.Check(Pristine(human0) && human0.InPlay, $"the sonic spent nothing on the human's ledger");

            // 2. The flash needs the victim facing it: from behind nothing, from ahead a white wash.
            int before = washes.Count;
            Fire(flashRocket, Tail(human0), Vector3.Forward);
            ctx.Check(washes.Count == before, $"a wep_09 into the same tail, behind the pilot, washes nothing (washes={washes.Count - before})");
            Fire(flashRocket, Nose(human0), Vector3.Back);
            ctx.Check(washes.Count == before + 1 && washes[^1].Player == 0 && washes[^1].Colour == new Color(1f, 1f, 1f)
                      && washes[^1].Weight > 0.95f,
                $"a wep_09 into the nose washes pane 1 white at full weight (washes={washes.Count - before} last={(washes.Count > before ? $"{washes[^1].Colour} w={washes[^1].Weight:0.###}" : "-")})");
            Advance(7f);
            ctx.Check(Clear(0) && Clear(1) && Pristine(human0), $"and it clears; the ledger is still untouched");

            // 3. Two viewers hit inside one second: each pane its own wash, and the AI 20 m from the
            // second burst is inside the sonic's plateau and stunned for the full 5 s.
            before = washes.Count;
            live.Spawn(sonic, new Transform3D(Basis.LookingAt(Vector3.Forward, Vector3.Up), Tail(human0)), Vector3.Zero);
            live.Spawn(sonic, new Transform3D(Basis.LookingAt(Vector3.Forward, Vector3.Up), Tail(human1)), Vector3.Zero);
            Advance(0.5f);
            ctx.Check(!RoundsAlive() && washes.Count == before + 2
                      && washes.Skip(before).Select(w => w.Player).OrderBy(p => p).SequenceEqual(new[] { 0, 1 }),
                $"two sonics in one step wash P1 and P2 once each (players={string.Join(",", washes.Skip(before).Select(w => w.Player + 1))})");
            Advance(1.5f);
            ctx.Check(flash.CurrentFor(0).A > 0.9f && flash.CurrentFor(1).A > 0.9f && flash.CurrentFor(1).R > 0.9f,
                $"and each pane carries its own red wash ({flash.CurrentFor(0)} / {flash.CurrentFor(1)})");
            ctx.Check(aiNear.Pilot!.IsStunned && Mathf.Abs(aiNear.Pilot.StunRemainingS - 5f) < 0.02f,
                $"the AI 20 m from the second burst is stunned for the full 5 s (remaining={aiNear.Pilot.StunRemainingS:0.00})");
            ctx.Check(!aiFar.Pilot!.IsStunned && Pristine(human1) && Pristine(aiNear),
                $"the AI a kilometre away is untouched and no ledger moved");
            Advance(6f);

            // 4. The AI's stun follows the intensity at the burst's distance to its hull: a round
            // passing abeam fuses inside IMPACT_PROXIMITY and stuns for the fade's seconds, a direct
            // hit raises that to 5 s, and the next passing burst overwrites it back down (written, never maxed).
            Fire(flashRocket, Tail(aiFar), Vector3.Forward);
            ctx.Check(!aiFar.Pilot.IsStunned, $"a wep_09 into the AI's tail, behind it, stuns nothing");
            // A vertical line abeam whose closest approach to the hull sits in the fade band, past
            // the plateau's 27 m and inside the 35 m fuse; the fuse bursts the round at that point.
            float abeam = 0f, hullDistance = 0f;
            Vector3 lineTop = default, lineBottom = default;
            for (float x = 30f; x <= 40f && hullDistance == 0f; x += 0.5f)
            {
                var top = aiFar.WorldPosition + new Vector3(x, 40f, 0f);
                var bottom = aiFar.WorldPosition + new Vector3(x, -40f, 0f);
                float d = aiFar.Body!.SegmentDistance(top, bottom, out _, out _);
                if (d is > 29f and < 33f)
                {
                    abeam = x;
                    hullDistance = d;
                    lineTop = top;
                    lineBottom = bottom;
                }
            }
            bool inFade = DisablingIntensity.TryResolve(hullDistance * hullDistance, sonic.ImpactProximitySqM ?? 0f,
                requiresFacing: false, facingDot: 0f, out float fadeIntensity, out float fadeSeconds);
            ctx.Check(hullDistance > 0f && inFade && fadeIntensity is > 0.05f and < 0.95f,
                $"a line {abeam:0.#} m abeam passes {hullDistance:0.#} m from the AI's hull, in the fade (intensity {fadeIntensity:0.00}, {fadeSeconds:0.00} s)");
            Fire(sonic, lineTop, lineBottom - lineTop);
            ctx.Check(aiFar.Pilot.IsStunned && Mathf.Abs(aiFar.Pilot.StunRemainingS - fadeSeconds) < 0.1f,
                $"a sonic fusing abeam inside IMPACT_PROXIMITY stuns the AI for the curve's {fadeSeconds:0.00} s (remaining={aiFar.Pilot.StunRemainingS:0.00})");
            Fire(sonic, Tail(aiFar), Vector3.Forward);
            ctx.Check(Mathf.Abs(aiFar.Pilot.StunRemainingS - 5f) < 0.02f,
                $"a direct hit while stunned raises it to 5 s (remaining={aiFar.Pilot.StunRemainingS:0.00})");
            Fire(sonic, lineTop, lineBottom - lineTop);
            ctx.Check(Mathf.Abs(aiFar.Pilot.StunRemainingS - fadeSeconds) < 0.1f,
                $"the next passing burst overwrites the running stun back down to {fadeSeconds:0.00} s (remaining={aiFar.Pilot.StunRemainingS:0.00})");
            ctx.Check(Pristine(aiFar) && aiFar.InPlay, $"none of the three spent a point on the AI's ledger");

            // 5. The choker: a round into the AI's back leaves a 2 s cloud at the burst; the AI is
            // choked for the formula's seconds at its ORIGIN distance, the human 20 m out for the
            // floor, both refreshed every step the cloud lives, and the timer runs once it is gone.
            var clouds = new List<(Vector3 Centre, float Remaining)>();
            steps = Fire(choker, aiNear.WorldPosition + new Vector3(0f, 30f, 0f), Vector3.Down);
            live.CollectTanglerClouds(clouds);
            ctx.Check(steps < 120 && clouds.Count == 1 && Mathf.Abs(clouds[0].Remaining - (2f - steps * dt)) < 0.02f,
                $"a wep_12 into the AI leaves one cloud running its 2 s TIME (clouds={clouds.Count} remaining={(clouds.Count > 0 ? clouds[0].Remaining : 0f):0.00} after {steps} steps)");
            float originSq = clouds.Count > 0 ? aiNear.WorldPosition.DistanceSquaredTo(clouds[0].Centre) : 0f;
            float expected = TanglerChoke.Duration(originSq, choker.Tangler!.Radius!.Value, bounds.Min, bounds.Max);
            ctx.Check(expected > 10f && Mathf.Abs(aiNear.EngineDeadRemainingS - expected) < 0.05f,
                $"the struck AI's engine is dead for the formula's {expected:0.00} s at {Mathf.Sqrt(originSq):0.##} m from its origin (remaining={aiNear.EngineDeadRemainingS:0.00})");
            ctx.Check(Mathf.Abs(human1.EngineDeadRemainingS - bounds.Min) < 0.05f,
                $"the human 20 m out, inside RADIUS, is choked for the {bounds.Min:0} s floor (remaining={human1.EngineDeadRemainingS:0.00})");
            ctx.Check(human0.EngineDeadRemainingS == 0f && aiFar.EngineDeadRemainingS == 0f,
                $"nobody outside the cloud is touched");
            ctx.Check(Pristine(aiNear) && Pristine(human1), $"the choker spent nothing on either ledger");
            Advance(1.5f);
            clouds.Clear();
            live.CollectTanglerClouds(clouds);
            ctx.Check(clouds.Count == 1 && Mathf.Abs(aiNear.EngineDeadRemainingS - expected) < 0.05f,
                $"while the cloud lives the choke is refreshed every step (remaining={aiNear.EngineDeadRemainingS:0.00} at cloud {(clouds.Count > 0 ? clouds[0].Remaining : 0f):0.00} s left)");
            Advance(0.6f);
            clouds.Clear();
            live.CollectTanglerClouds(clouds);
            ctx.Check(clouds.Count == 0, $"the cloud is gone at its TIME (clouds={clouds.Count})");
            // Released so its model steps and the timer counts down; a held airframe steps nothing.
            aiNear.Held = false;
            float atRelease = aiNear.EngineDeadRemainingS;
            Advance(3f);
            ctx.Check(atRelease > 10f && Mathf.Abs(aiNear.EngineDeadRemainingS - (atRelease - 3f)) < 0.1f,
                $"once the cloud is gone the engine timer runs down ({atRelease:0.00} → {aiNear.EngineDeadRemainingS:0.00} over 3 s)");
        }
        finally
        {
            flash?.Free();
            foreach (var pane in panes)
                pane.Free();
            foreach (var rig in rigs)
                rig.Free();
            pool?.Free();
            textures.Dispose();
        }
    }

    // C12's SURFACE_ANIMATION orientation on a live pool: the same rocket into a 30° slope and into
    // flat ground, and the choker (whose default row is a plain ANIMATION) into the slope, with the
    // effect sink recording the basis each play was handed.
    internal static void ImpactOrientation(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"weapon definitions");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        if (!weapons.TryGet("wep_06", out var he) || !weapons.TryGet("wep_12", out var choker))
        {
            ctx.Check(false, $"wep_06 and wep_12 resolve");
            return;
        }
        var heRow = he.ImpactFor(SurfaceRegistry.Default);
        var chokerRow = choker.ImpactFor(SurfaceRegistry.Default);
        ctx.Check(heRow is { Animation: null, SurfaceAnimation: "he_ground_effect" }
                  && chokerRow is { Animation: "scatter_effect", SurfaceAnimation: null },
            $"wep_06's default row binds SURFACE_ANIMATION he_ground_effect and wep_12's a plain ANIMATION scatter_effect");

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        var bodies = new List<StaticBody3D>();
        try
        {
            var plays = new List<(string Name, Vector3 At, Basis Orient)>();
            var live = new ProjectilePool(textures, null, null)
            {
                EffectSink = (name, at, orient, ttl) => plays.Add((name, at, orient)),
            };
            pool = live;
            ctx.Host.AddChild(live);

            var origin = new Vector3(600f, 4000f, 600f);
            // A plate rolled 30° about Z: its top normal leans toward -X.
            var slope = new StaticBody3D { Name = "orientation-lab-slope" };
            slope.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(60f, 0.2f, 60f) } });
            slope.GlobalTransform = new Transform3D(new Basis(Vector3.Back, Mathf.DegToRad(30f)), origin);
            ctx.Host.AddChild(slope);
            bodies.Add(slope);
            var slopeNormal = slope.GlobalTransform.Basis.Y.Normalized();
            var flat = CombatSuites.Plate("orientation-lab-flat", new Vector3(60f, 0.2f, 60f), origin + new Vector3(200f, 0f, 0f));
            ctx.Host.AddChild(flat);
            bodies.Add(flat);

            (string Name, Vector3 At, Basis Orient)? Drop(WeaponDef weapon, Vector3 above)
            {
                plays.Clear();
                live.Spawn(weapon, new Transform3D(Basis.LookingAt(Vector3.Down, Vector3.Forward), above), Vector3.Zero);
                for (int i = 0; i < 120 && plays.Count == 0; i++)
                    live.SimStep(1f / 60f);
                live.Clear();
                return plays.Count > 0 ? plays[0] : null;
            }
            static float DegreesBetween(Vector3 a, Vector3 b) =>
                Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(a.Normalized().Dot(b.Normalized()), -1f, 1f)));

            static string Describe((string Name, Vector3 At, Basis Orient)? play) =>
                play is { } p ? $"fx={p.Name} Y={p.Orient.Y}" : "no play";

            var onSlope = Drop(he, origin + new Vector3(0f, 20f, 0f));
            ctx.Check(onSlope is { Name: "he_ground_effect" } s1 && DegreesBetween(s1.Orient.Y, slopeNormal) < 0.5f
                      && DegreesBetween(s1.Orient.Y, Vector3.Up) > 29f,
                $"wep_06 into the 30° slope plays he_ground_effect with its Y on the slope normal ({Describe(onSlope)} normal={slopeNormal})");
            ctx.Check(onSlope is { } s2 && Mathf.IsEqualApprox(s2.Orient.Determinant(), 1f)
                      && s2.Orient.Y.IsEqualApprox(s2.Orient * Vector3.Up),
                $"the basis is a pure rotation");

            var onFlat = Drop(he, flat.GlobalPosition + new Vector3(0f, 20f, 0f));
            ctx.Check(onFlat is { Name: "he_ground_effect" } f1 && f1.Orient.IsEqualApprox(Basis.Identity),
                $"the same round into flat ground plays it on the fixed axis ({Describe(onFlat)})");

            var chokerOnSlope = Drop(choker, origin + new Vector3(0f, 20f, 0f));
            ctx.Check(chokerOnSlope is { Name: "scatter_effect" } c1 && c1.Orient.IsEqualApprox(Basis.Identity),
                $"wep_12's plain ANIMATION scatter_effect keeps its fixed axis on the slope ({Describe(chokerOnSlope)})");
        }
        finally
        {
            pool?.Free();
            foreach (var b in bodies)
                b.Free();
            textures.Dispose();
        }
    }

    // A5's launch axis and D18's launch hook over a live fire path: the rig fires its own pylons
    // through FireControl, and the pool is never stepped, so every round still stands at the pose
    // it launched from. No shipped airframe cants a pylon marker, so the salvo flies off markers
    // this suite cants itself; an aligned rig cannot tell the aircraft axis from the marker's.
    internal static void OrdnanceLaunchAxis(TestContext ctx)
    {
        // The yaw this suite puts on each pylon marker, alternating in sign: big enough that a
        // salvo launched off the markers misses by hundreds of metres at rocket range.
        const float CantDeg = 20f;

        // The rule itself, both branches, with no airframe in it.
        var canted = new Basis(Vector3.Up, Mathf.DegToRad(30f));
        var mountAim = new Vector3(0.6f, 0f, -0.8f);
        ctx.Check(FlightController.OrdnanceLaunchDir(true, canted, false, mountAim) is { } humanDir
                  && humanDir.IsEqualApprox(-canted.Z),
            $"a human's round leaves along the aircraft's own axis, negated, ignoring any mount aim");
        ctx.Check(FlightController.OrdnanceLaunchDir(true, canted, true, null) is { } rearDir
                  && rearDir.IsEqualApprox(canted.Z),
            $"a REAR weapon takes the same axis unnegated");
        ctx.Check(FlightController.OrdnanceLaunchDir(false, canted, false, mountAim) is { } aiDir
                  && aiDir.IsEqualApprox(mountAim),
            $"an AI's round leaves along the clamped mount aim instead — the original's own asymmetry");
        ctx.Check(FlightController.OrdnanceLaunchDir(false, canted, false, null) == null,
            $"and an AI with no aim to clamp keeps the mount's own axis");

        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        if (!weapons.TryGet("wep_13", out var smoker) || smoker.SmokeScreenTime is not { } screenTime)
        {
            ctx.Check(false, $"wep_13 resolves and carries a SMOKE_SCREEN TIME");
            return;
        }
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var stockLoadouts = StockLoadouts.Load();
        var textures = new TextureArchive(texturesPath);
        try
        {
            // The census: how far off the airframe's own axis any shipped pylon marker sits.
            string worstModel = string.Empty;
            string worstDisplay = string.Empty;
            float shippedCantDeg = 0f;
            int mostPylons = 0;
            foreach (var (model, display) in MarkerRig.PlayerAirframes)
            {
                Node3D? plane = null;
                try
                {
                    plane = new PlaneBuilder(planesGamez, textures).Build(model);
                    ctx.Host.AddChild(plane);
                    var scan = Loadout.ForRig(plane, weapons, StockFor(stockLoadouts, model));
                    foreach (var hp in scan.Hardpoints)
                    {
                        shippedCantDeg = Mathf.Max(shippedCantDeg, Mathf.RadToDeg(
                            (-hp.Pylon.GlobalTransform.Basis.Z).AngleTo(-plane.GlobalTransform.Basis.Z)));
                    }
                    // The salvo flies off the widest rig there is, so the fan has the most pylons
                    // it can have.
                    if (scan.Hardpoints.Count > mostPylons)
                    {
                        (mostPylons, worstModel, worstDisplay) = (scan.Hardpoints.Count, model, display);
                    }
                }
                finally
                {
                    plane?.Free();
                }
            }
            // ⚠ Every shipped pylon marker is square to its airframe, so the two launch axes agree
            // on the shipped fit and the fix is a guard rather than a visible change. The salvo
            // below therefore cants its own markers; nothing else here can tell the two apart.
            ctx.Check(shippedCantDeg < 0.5f && mostPylons > 1,
                $"no shipped airframe cants a pylon marker (worst {shippedCantDeg:0.00}°); the salvo flies the {worstDisplay}'s {mostPylons} pylons");

            var stats = PlaneStats.Load(ctx.ZrdrPath, worstModel);
            var stock = StockFor(stockLoadouts, worstModel);
            ProjectilePool? pool = null;
            FlightController? human = null;
            FlightController? ai = null;
            var live = new List<(Vector3 Pos, Vector3 Velocity)>();
            try
            {
                pool = new ProjectilePool(textures, null, null);
                ctx.Host.AddChild(pool);

                // Nose 45° off world -Z, so a round flying down the world axis instead of the
                // aircraft's would read as an obvious failure rather than a rounding difference.
                var spawn = new Vector3(0f, 1000f, 0f);
                var lookAt = spawn + new Vector3(1f, 0f, -1f);
                FlightController BuildRig(int playerIndex, bool isHuman)
                {
                    var model = new PlaneBuilder(planesGamez, textures).Build(worstModel);
                    var rig = new FlightController
                    {
                        PlaneModel = model,
                        Collider = PlaneCollider.Build(model),
                        Damage = new PlaneDamage(stats.DestroyableParts),
                        Loadout = Loadout.ForRig(model, weapons, stock),
                        PlayerIndex = playerIndex,
                        IsHumanPiloted = isHuman,
                        Pilot = isHuman ? null : AiPilot.HoldingCourse(spawn, lookAt),
                        Projectiles = pool,
                        UseKeyboard = false,
                        PadDevices = System.Array.Empty<int>(),
                        AllowPause = false,
                        AutoFireRockets = true,
                    };
                    rig.AddChild(model);
                    rig.Setup(new FlightModel(stats), null, new CamParams(), spawn, lookAt);
                    rig.Name = (isHuman ? "p" : "ai") + playerIndex;
                    ctx.Host.AddChild(rig);
                    rig.Held = true;   // the pose the launch axis is asserted against must stand still
                    for (int i = 0; i < rig.Loadout!.Hardpoints.Count; i++)
                    {
                        var hp = rig.Loadout.Hardpoints[i];
                        // One round per pylon, so the salvo walks every pylon instead of draining
                        // one, and a cant the shipped rig does not carry, alternating side to side.
                        hp.Capacity = 1;
                        hp.Ammo = 1;
                        hp.Pylon.Transform = new Transform3D(
                            new Basis(Vector3.Up, Mathf.DegToRad(i % 2 == 0 ? CantDeg : -CantDeg)),
                            hp.Pylon.Position);
                    }
                    return rig;
                }

                human = BuildRig(0, isHuman: true);
                int pylons = human.Loadout!.Hardpoints.Count;
                var nose = -human.GlobalTransform.Basis.Orthonormalized().Z;
                float appliedCant = 0f;
                foreach (var hp in human.Loadout.Hardpoints)
                {
                    appliedCant = Mathf.Max(appliedCant,
                        Mathf.RadToDeg((-hp.Pylon.GlobalTransform.Basis.Orthonormalized().Z).AngleTo(nose)));
                }
                ctx.Check(Mathf.Abs(appliedCant - CantDeg) < 0.5f,
                    $"the rig under test carries {appliedCant:0.00}° of pylon cant, which the old launch axis fanned by");
                // The pool is never stepped, so the rounds pile up where they were launched.
                for (int i = 0; i < 900 && CountLive(pool, live) < pylons; i++)
                {
                    human.SimStep(1f / 60f);
                }
                ctx.Same(pylons, CountLive(pool, live), $"the human salvo puts one round on every pylon");
                float worstFan = 0f;
                foreach (var round in live)
                {
                    worstFan = Mathf.Max(worstFan, Mathf.RadToDeg(round.Velocity.Normalized().AngleTo(nose)));
                }
                ctx.Check(worstFan < 0.5f,
                    $"every round of the salvo flies parallel to the nose worst={worstFan:0.00}° (markers cant {appliedCant:0.00}°)");
                float worstOrigin = 0f;
                foreach (var round in live)
                {
                    worstOrigin = Mathf.Max(worstOrigin, NearestMarkerDistance(human.Loadout!, round.Pos));
                }
                ctx.Check(live.Select(r => r.Pos).Distinct().Count() == pylons && worstOrigin < 0.05f,
                    $"and each leaves from its own pylon marker, which is what the mount still gives worst={worstOrigin:0.000} m");

                // The AI branch is a different rule and must not have moved: with no rocketeer to
                // clamp an aim it still launches down the marker's own axis, cant and all.
                pool.Clear();
                ai = BuildRig(FlightRoster.ShooterIdBase, isHuman: false);
                var aiNose = -ai.GlobalTransform.Basis.Orthonormalized().Z;
                for (int i = 0; i < 900 && CountLive(pool, live) < pylons; i++)
                {
                    ai.SimStep(1f / 60f);
                }
                float offMarker = 0f;
                float offNose = 0f;
                foreach (var round in live)
                {
                    var dir = round.Velocity.Normalized();
                    offMarker = Mathf.Max(offMarker, NearestMarkerAngleDeg(ai.Loadout!, dir));
                    offNose = Mathf.Max(offNose, Mathf.RadToDeg(dir.AngleTo(aiNose)));
                }
                ctx.Check(live.Count == pylons && offMarker < 0.5f,
                    $"an AI's launch is unchanged: every round leaves along its own mount's axis worst={offMarker:0.00}°");
                ctx.Check(Mathf.Abs(offNose - CantDeg) < 0.5f,
                    $"and that axis is the canted one, so the AI's salvo still fans by {offNose:0.00}°");

                // D18's hook: a SMOKE_SCREEN pylon lays a screen and spawns nothing at all.
                pool.Clear();
                var screens = new SmokeScreens(SmokeScreenTunables.Image,
                    System.Array.Empty<FlightController>, null);
                human.SmokeScreens = screens;
                foreach (var hp in human.Loadout!.Hardpoints)
                {
                    hp.Weapon = smoker;
                    hp.Capacity = 1;
                    hp.Ammo = 1;
                }
                int before = human.Loadout.Hardpoints.Sum(h => h.Ammo);
                for (int i = 0; i < 900 && !screens.IsLaying(human); i++)
                {
                    human.SimStep(1f / 60f);
                }
                ctx.Check(screens.IsLaying(human) && screens.ActiveCount == 1,
                    $"firing wep_13 lays one screen on the aircraft that fired it");
                ctx.Same(0, CountLive(pool, live), $"and spawns no round at all — the pool stays empty");
                ctx.Check(human.Loadout.Hardpoints.Sum(h => h.Ammo) == before - 1,
                    $"the smoker still spends its round of ammo, as every other pylon weapon does");
            }
            finally
            {
                ai?.Free();
                human?.Free();
                pool?.Free();
            }
        }
        finally
        {
            textures.Dispose();
        }
    }

    // The sonic burst's five ring defs end with the ring INACTIVE at opacity 0, and only their
    // RESET_STATE ever re-activates it. The original instances a fresh copy per CALL_ANIMATION, so
    // that pose is what every burst starts from; the pool reuses each copy, so from the wrap on it
    // is what every burst starts WITHOUT unless the checkout re-applies it. Four slots, five plays:
    // the fifth lands on the first's copy, and its rings must draw as the first's did.
    internal static void EffectPoolReset(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            const int slots = 4;
            const int plays = slots + 1;
            const string anim = "sonic_ground_effect";
            var stage = StageBurstRoots(ctx, world, anim, slots);
            var runtime = AnimRuntime.ForEffects(
                AnimRuntime.NewTemplateStage(pooled: true, shown: true, placesCalled: true),
                1, new CountingEmitterFactory(), false, SuiteConstants.BurstTtl,
                () => ctx.Camera.GlobalPosition);
            runtime.ManualAdvance = true;
            ctx.Host.AddChild(stage);
            ctx.Host.AddChild(runtime);
            try
            {
                runtime.Bind(stage, world.Session.Program.Subset(anim));
                var slot0 = stage.GetNode<Node3D>("pool0");
                var readings = new List<List<RingReading>>();
                for (int play = 1; play <= plays; play++)
                {
                    ctx.Check(runtime.PlayEffectAt(anim, ctx.Camera.GlobalPosition), $"play {play} started");
                    // Three frames in: the rings' opening motions have taken their FROM poses.
                    for (int i = 0; i < 3; i++)
                        runtime.Advance(1f / 60f);
                    readings.Add(RingReadingsIn(slot0));
                    // Play the burst out; the next play must find its slot idle, not recycled.
                    int steps = Mathf.RoundToInt(SuiteConstants.BurstSeconds * 60f);
                    for (int i = 0; i < steps; i++)
                        runtime.Advance(1f / 60f);
                }

                ctx.Same(0, runtime.PoolRecycles, $"no play wrapped onto a live copy: each burst ended before the pool came round");
                var first = readings[0];
                var fifth = readings[plays - 1];
                ctx.Check(first.Count > 0 && first.Any(r => r.Visible),
                    $"play 1 draws its rings on slot 0 ({first.Count(r => r.Visible)}/{first.Count} ring mesh(es) visible)");
                ctx.Check(fifth.Any(r => r.Visible),
                    $"play {plays} draws its rings on the same copy ({fifth.Count(r => r.Visible)}/{fifth.Count} visible)");
                ctx.Same(first.Count, fifth.Count, $"the same ring meshes were read on both plays");
                for (int i = 0; i < System.Math.Min(first.Count, fifth.Count); i++)
                {
                    var (a, b) = (first[i], fifth[i]);
                    ctx.Check(a.Visible == b.Visible && a.Scale.IsEqualApprox(b.Scale)
                              && Mathf.Abs(a.Opacity - b.Opacity) < 0.01f,
                        $"{a.Root}/{a.Mesh}: play {plays} starts as play 1 did (visible {b.Visible} vs {a.Visible}, scale {b.Scale} vs {a.Scale}, opacity {b.Opacity:0.00} vs {a.Opacity:0.00})");
                }
            }
            finally
            {
                runtime.Free();
                stage.Free();
            }
        });
    }

    // The screen's cloud through the particle runtime: `smokerpuff` (NUMBER 4 per 0.65 m,
    // SIZE_RANGE 0.15–0.25 growing 85×, LOCAL_VELOCITY 10 m/s astern, lifetime 2.5–4 s) driven
    // straight and level at 100 m/s for four seconds. What the authored numbers owe: every batch's
    // four puffs, so the population runs into the thousands and past the trail pool it starts on;
    // puffs tens of metres across near the end of life; and the local velocity blowing them down
    // the host's own backward axis, not the world's.
    private static void SmokeScreenCloud(TestContext ctx, PufferState state)
    {
        ctx.Check(state.Number == 4 && Mathf.IsEqualApprox(state.DistanceInterval, 0.65f)
                  && Mathf.IsEqualApprox(state.GrowthFactor, 85f)
                  && state.LocalVelocity.IsEqualApprox(new Vector3(0f, 0f, 10f)),
            $"smokerpuff authors NUMBER 4, 0.65 m, growth 85, local (0,0,10) n={state.Number} di={state.DistanceInterval} g={state.GrowthFactor} lv={state.LocalVelocity}");
        var gpu = new RecordingEmitterRenderer();
        var puffer = Puffer.CreateWith(state, gpu);
        ctx.Host.AddChild(puffer);
        // Detached for the run, as PufferModes does: the harness clock is a FixedStep one nothing
        // steps, so with it attached no puff would age and every size check would pass or fail
        // vacuously.
        var clock = GameClock.Current;
        GameClock.Current = null;
        try
        {
            const float dt = 1f / 60f;
            const float speed = 100f;
            // The host flies world +X; its own backward axis (+Z of the basis) is world −Y here,
            // so an authored local velocity that is honoured shows up as a downward drift.
            var basis = new Basis(Vector3.Right, Vector3.Forward, Vector3.Down);
            var pos = Vector3.Zero;
            puffer.Emit(pos, basis, 0f);
            float maxSize = 0f;
            for (int i = 0; i < 240; i++)
            {
                pos += new Vector3(speed * dt, 0f, 0f);
                puffer.Emit(pos, basis, dt);
                puffer._Process(dt);
                foreach (var p in gpu.LastFrame)
                    maxSize = Mathf.Max(maxSize, p.Size);
            }
            // 4 per 0.65 m at 100 m/s is 615 births a second; with a 2.5–4 s life the fourth
            // second holds about 2,000, so a pool that stayed at 640 or a spawn that dropped NUMBER
            // would both read well under 1,500.
            ctx.Check(gpu.Shown > 1500 && gpu.Capacity > 640,
                $"the fourth second of a 100 m/s screen holds thousands of puffs live={gpu.Shown} pool={gpu.Capacity}");
            ctx.Check(maxSize > 20f,
                $"a puff nearing the end of its life spans tens of metres (0.5 m × 85 growth) max={maxSize:0.0}");
            // The world-frame random vertical (−20..16 m/s, mean −2) alone leaves the cloud's mean
            // about 2.5 m under the track over the population's ages; the authored 10 m/s astern,
            // downward in this pose and damped by FRICTION 0.3, takes it to about −15 m.
            float meanY = gpu.LastFrame.Count > 0 ? gpu.LastFrame.Average(p => p.Position.Y) : 0f;
            ctx.Check(meanY < -8f,
                $"LOCAL_VELOCITY blows the cloud down the host's backward axis, world −Y in this pose meanY={meanY:0.0}");
        }
        finally
        {
            GameClock.Current = clock;
            puffer.Free();
        }
    }

    private static LoadoutDef? StockFor(StockLoadouts stock, string model)
    {
        foreach (var d in stock.All.Values)
        {
            if (d.Model == model)
            {
                return d;
            }
        }
        return null;
    }

    private static float NearestMarkerDistance(Loadout loadout, Vector3 pos)
    {
        float best = float.MaxValue;
        foreach (var hp in loadout.Hardpoints)
        {
            best = Mathf.Min(best, hp.Pylon.GlobalPosition.DistanceTo(pos));
        }
        return best;
    }

    private static float NearestMarkerAngleDeg(Loadout loadout, Vector3 dir)
    {
        float best = float.MaxValue;
        foreach (var hp in loadout.Hardpoints)
        {
            best = Mathf.Min(best,
                Mathf.RadToDeg(dir.AngleTo(-hp.Pylon.GlobalTransform.Basis.Orthonormalized().Z)));
        }
        return best;
    }

    private static int CountLive(ProjectilePool pool, List<(Vector3 Pos, Vector3 Velocity)> into)
    {
        into.Clear();
        pool.CollectLiveRounds(into);
        return into.Count;
    }

    // A burst's miniature world-effects stage: the def's derived anchor roots (its CALL_ANIMATION
    // closure against the chapter gamez, the production derivation, so an unresolvable anchor
    // throws here naming itself) built once per pool slot, each copy hidden, exactly as
    // WorldEffectsFactory stages them. The caller frees the returned stage.
    private static Node3D StageBurstRoots(TestContext ctx, TestWorld world, string animName, int slots)
    {
        var roots = Session.EffectCatalogue.StageRootsFor(world.Session.Program, new[] { animName },
            Session.WorldEffectsFactory.StageRootResolver(world.Gamez));
        ctx.Check(roots.Count > 0,
            $"{animName}: its call closure's anchor roots derived ({roots.Count}: {string.Join(", ", roots)})");
        var stage = new Node3D { Name = $"BurstStage_{animName}" };
        for (int slot = 0; slot < slots; slot++)
        {
            var pool = new Node3D { Name = $"pool{slot}" };
            pool.SetMeta(AnimRuntime.PoolSlotMeta, slot);
            stage.AddChild(pool);
            int built = Session.WorldEffectsFactory.BuildEffectStage(world.Gamez,
                world.Session.Builder.Scene, pool, roots);
            ctx.Same(roots.Count, built, $"{animName}: template roots staged from the chapter gamez (slot {slot})");
            foreach (var child in pool.GetChildren())
            {
                if (child is Node3D root)
                {
                    root.Visible = false;
                }
            }
        }
        return stage;
    }

    // ---- a pooled copy is re-reset on checkout ------------------------------------------------

    // Every ring mesh under the slot's five sonic_ring<N> copies, in tree order: whether it is
    // drawing, its scale, and its per-instance opacity (1 when never faded).
    private static List<RingReading> RingReadingsIn(Node3D slot)
    {
        var rows = new List<RingReading>();
        foreach (var child in slot.GetChildren())
        {
            if (child is not Node3D root || !root.Name.ToString().StartsWith("sonic_ring", System.StringComparison.Ordinal))
                continue;
            foreach (var mesh in Descendants(root).OfType<MeshInstance3D>())
            {
                var alpha = mesh.GetInstanceShaderParameter(SceneBuilder.OpacityParam);
                rows.Add(new RingReading(root.Name, mesh.Name, mesh.IsVisibleInTree(), mesh.Scale,
                    alpha.VariantType == Variant.Type.Nil ? 1f : alpha.AsSingle()));
            }
        }
        return rows;
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            yield return child;
            foreach (var deeper in Descendants(child))
                yield return deeper;
        }
    }

    // One ring mesh's state at a sampled instant of a sonic play (the effect-pool-reset suite):
    // which staged root it sits under, whether it draws, its scale and its per-instance opacity.
    private readonly record struct RingReading(string Root, string Mesh, bool Visible, Vector3 Scale, float Opacity);

    // The smoke-screen suite's stand-in for one screen's authored trail: it keeps the drive instead
    // of drawing it, so the start/stop pairing and the pose the screen feeds it are assertable
    // without a GPU or a chapter's textures.
    private sealed class RecordingSmokeEmitter : ISmokeEmitter
    {
        private bool _first = true;

        public bool HomedAtLaunch { get; private set; }

        public bool Stopped { get; private set; }

        public int Steps { get; private set; }

        public Vector3 LastPos { get; private set; }

        public Basis LastBasis { get; private set; }

        public void Emit(Vector3 worldPos, Basis worldBasis, float dt)
        {
            if (_first)
            {
                _first = false;
                HomedAtLaunch = dt == 0f;
            }
            else
            {
                Steps++;
            }
            LastPos = worldPos;
            LastBasis = worldBasis;
        }

        public void Stop() => Stopped = true;
    }
}
