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

internal static class CombatSuites
{
    internal static void LoadoutBind(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var r = Probes.Loadouts(ctx.ZrdrPath, ctx.MessagesPath, ctx.PlanesGamezPath, ctx.DataRoot,
            "", ctx.LoadoutOverride);
        ctx.Check(r.Error == null, $"loadout inputs load error={r.Error ?? "-"}");
        if (r.Error != null)
        {
            return;
        }
        if (ctx.LoadoutOverride != null)
        {
            ctx.Note($"cross-binding every plane to loadout={ctx.LoadoutOverride} — not the stock check");
        }
        ctx.Same(PlayerAirframes, r.Bound, $"stock loadouts bound");
        ctx.Same(0, r.Failed, $"loadout binding failures");
        foreach (string f in r.Failures)
        {
            ctx.Check(false, $"loadout binding {f}");
        }

        // A partial stock fit takes the FILL ORDER's prefix (1,5,2,6,3,7,4,8), not
        // pylon1..pylonN — Hardpoint.Index must be the true pylon number so the weapon gauge's
        // belt lights land at the original's physical positions, gaps included.
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, Messages.Load(ctx.MessagesPath));
        var stock = StockLoadouts.Load();
        var textures = new TextureArchive(texturesPath);
        try
        {
            foreach (var def in stock.All.Values)
            {
                if (def.Hardpoints is not { Count: > 0 } hp)
                {
                    continue;
                }
                Node3D? plane = null;
                try
                {
                    plane = new PlaneBuilder(planesGamez, textures).Build(def.Model);
                    var loadout = Loadout.Bind(def, plane, weapons);
                    var wantIndices = new int[hp.Count];
                    System.Array.Copy(Loadout.PylonFillOrder, wantIndices, hp.Count);
                    var gotIndices = new int[loadout.Hardpoints.Count];
                    for (int i = 0; i < loadout.Hardpoints.Count; i++)
                    {
                        gotIndices[i] = loadout.Hardpoints[i].Index;
                    }
                    string want = string.Join(",", wantIndices), got = string.Join(",", gotIndices);
                    ctx.Check(want == got,
                        $"{def.Display}: hardpoints bind to the fill-order's pylon numbers (want {want}, got {got})");
                }
                finally
                {
                    plane?.Free();
                }
            }

            AiLoadoutBind(ctx, planesGamez, textures, weapons);
        }
        finally
        {
            textures.Dispose();
        }
    }

    // The AI half of the same bind: a militia def's authored weapons tuples onto its airframe's rig,
    // through the same Loadout.Bind. Black Hat's Warhawk is the case worth pinning — eight torpedoes
    // and a gun off one authored list, told apart by the weapon def's own CANNON flag.
    internal static void AiLoadoutBind(TestContext ctx, GameZ planesGamez, TextureArchive textures,
        WeaponDefs weapons)
    {
        var stats = PlaneStats.LoadForAi(ctx.ZrdrPath, "player_warhawk", "bhatwarhawk");
        ctx.Same(2, stats.AiWeapons.Count, $"bhatwarhawk authors two weapon entries");
        Node3D? plane = null;
        try
        {
            plane = new PlaneBuilder(planesGamez, textures).Build(stats.NodeName);
            var loadout = Loadout.BindAi(stats.AiWeapons, "bhatwarhawk", plane, weapons);
            ctx.Same(1, loadout.Hardpoints.Count, $"the torpedo entry takes one pylon");
            ctx.Check(loadout.Hardpoints[0].Weapon.Id == "wep_14",
                $"the pylon carries the authored torpedo (got {loadout.Hardpoints[0].Weapon.Id})");
            ctx.Same(8, loadout.Hardpoints[0].Ammo, $"it carries the authored eight rounds");
            ctx.Check(loadout.Hardpoints[0].Weapon.DamagesZeppelin,
                $"the torpedo is the DAMAGES_ZEPPELIN weapon the ordnance match keeps off aircraft");
            ctx.Same(1, loadout.Guns.Count, $"the gun entry binds one group");
            ctx.Check(loadout.Guns[0].Ammo == 8000,
                $"the gun carries its authored rounds (got {loadout.Guns[0].Ammo})");
            ctx.Check(loadout.Guns[0].Muzzles.Count > 0, $"the gun group resolved its firepoints");
        }
        finally
        {
            plane?.Free();
        }
    }

    // ---- needs a built plane in the tree --------------------------------------------------------

    internal static void WeaponsFire(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, Messages.Load(ctx.MessagesPath));
        // The pool holds the archive past construction (it bakes the tracer and impact stand-ins),
        // so it is disposed only after the self-test has run.
        var textures = new TextureArchive(texturesPath);
        Node3D? plane = null;
        ProjectilePool? pool = null;
        try
        {
            plane = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            ctx.Host.AddChild(plane);
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
            // The whole rig, not just what stock names — so every weapon has a mount of its
            // own class and a skip means a real gap, not a fallback that did not fire.
            var loadout = Loadout.ForRig(plane, weapons, stock);
            pool = new ProjectilePool(textures, null, null);
            ctx.Host.AddChild(pool);
            var result = WeaponBench.Run(plane, loadout, weapons, pool);
            ctx.Same(WeaponDefCount, result.Total, $"weapons offered to the bench");
            ctx.Same(WeaponDefCount, result.Ok, $"weapons that mounted and fired");
            ctx.Same(0, result.Errors, $"weapons that threw");
            // ForRig seats 4 gun-group slots on every airframe (loadout-forrig proves that across
            // all 11); the pylon count is per-rig, so only its presence is pinned here — 0 pylons
            // would turn every hardpoint weapon into a skip.
            ctx.Same(RigGunGroups, result.GunMounts, $"gun groups the bench fired from");
            ctx.Check(result.PylonMounts > 0, $"pylons the bench fired from ({result.PylonMounts})");
            // A skip is a success-looking outcome in the report — a weapon with no mount of its
            // class on this plane never fires, and nothing else would notice.
            ctx.Same(0, result.Skipped, $"weapons with no mount");
        }
        finally
        {
            pool?.Free();
            plane?.Free();
            textures.Dispose();
        }
    }

    // B2's per-muzzle slot state (the forget + catch-up pass), B3's intercept solver and
    // B4's candidate scan, the first three in isolation — no plane, no pool,
    // AimAssist is engine-free by design — plus a golden check that the player.json
    // and weapons.json values the assist consumes parse at their documented shipped figures
    // at their shipped figures. The ordnance list is the one
    // case that needs a live pool, since the list IS a filter over the rounds in flight.
    internal static void AimAssistSuite(TestContext ctx)
    {
        AimAssistCatchup(ctx);
        AimAssistForgetTimer(ctx);
        AimAssistShippedData(ctx);
        AimAssistIntercept(ctx);
        AimAssistScanGates(ctx);
        AimAssistSelection(ctx);
        AimAssistOrdnancePriority(ctx);
        AimAssistScatter(ctx);
        AimAssistFireDirection(ctx);
    }

    // The catch-up slerp: a ~0.2 s time constant at the shipped catchup_rate (5.0), full
    // convergence given enough time, and an outright snap on a single frame at or past
    // 1/catchup_rate — the real hitch-behaviour difference docs/org/aim-assist.md flags, not a
    // rounding detail to smooth away.
    internal static void AimAssistCatchup(TestContext ctx)
    {
        const float catchupRate = 5f;       // shipped sticky_bullet_catchup_rate
        const float forgetInterval = 100f;  // isolate this test from the forget pass
        const float dt = 1f / 60f;
        var target = new Vector3(0.5f, 0f, -0.8660254f); // 30° off local forward

        var slot = new GunAimSlot
        {
            Active = true,
            Smoothed = AimAssist.LocalForward,
            Target = target,
            LastUpdate = 0.0,
        };
        var slots = new[] { slot };
        float initialAngle = AimAssist.LocalForward.AngleTo(target);
        double now = 0.0;
        int tauSteps = Mathf.RoundToInt(1f / catchupRate / dt); // 12 steps at 60 Hz
        for (int i = 0; i < tauSteps; i++)
        {
            now += dt;
            AimAssist.Tick(slots, now, dt, forgetInterval, catchupRate);
        }
        float remainingFrac = slots[0].Smoothed.AngleTo(target) / initialAngle;
        ctx.Check(remainingFrac is > 0.25f and < 0.5f,
            $"aim-assist catch-up: one time constant (~{1f / catchupRate:0.0}s) leaves {remainingFrac:0.000} of the initial angle (e^-1 approx 0.368)");

        for (int i = 0; i < tauSteps * 8; i++)
        {
            now += dt;
            AimAssist.Tick(slots, now, dt, forgetInterval, catchupRate);
        }
        ctx.Check(slots[0].Smoothed.Dot(target) > 1f - 1e-4f,
            $"aim-assist catch-up: converges onto the target given enough time");

        var snapSlot = new GunAimSlot
        {
            Active = true,
            Smoothed = AimAssist.LocalForward,
            Target = target,
            LastUpdate = 0.0,
        };
        var snapSlots = new[] { snapSlot };
        AimAssist.Tick(snapSlots, 1f / catchupRate, 1f / catchupRate, forgetInterval, catchupRate);
        ctx.Check(snapSlots[0].Smoothed.IsEqualApprox(target),
            $"aim-assist catch-up: a frame ≥ 1/catchup_rate snaps the gun line in one step");
    }

    // The forget timer measures time since the barrel last FIRED, not time since a lock
    // was lost: stop restamping and the target unwinds to local forward exactly
    // forget_interval seconds later; keep restamping (as B5's fire call will) and it never does.
    internal static void AimAssistForgetTimer(TestContext ctx)
    {
        const float forgetInterval = 1.5f; // shipped sticky_bullet_forget_interval
        const float dt = 1f / 60f;
        var marker = new Vector3(0.5f, 0f, -0.8660254f); // 30° off forward — distinct from "forgotten"

        var slot = new GunAimSlot
        {
            Active = true,
            Smoothed = marker,
            Target = marker,
            LastUpdate = 0.0,
        };
        var slots = new[] { slot };
        double now = 0.0;
        int steps = Mathf.CeilToInt(forgetInterval / dt) + 2; // a couple past the threshold
        bool unwoundEarly = false;
        for (int i = 0; i < steps; i++)
        {
            now += dt;
            AimAssist.Tick(slots, now, dt, forgetInterval, 0f); // catchupRate 0 isolates the forget check
            if (now < forgetInterval && !slots[0].Target.IsEqualApprox(marker))
            {
                unwoundEarly = true;
            }
        }
        ctx.Check(!unwoundEarly, $"aim-assist forget: target holds until forget_interval elapses");
        ctx.Check(slots[0].Target.IsEqualApprox(AimAssist.LocalForward),
            $"aim-assist forget: target unwinds to local forward {forgetInterval}s after the last shot");

        var held = new GunAimSlot
        {
            Active = true,
            Smoothed = marker,
            Target = marker,
            LastUpdate = 0.0,
        };
        var heldSlots = new[] { held };
        now = 0.0;
        float sinceShot = 0f;
        float shotInterval = forgetInterval * 0.5f; // fires well inside the forget window
        for (int i = 0; i < steps * 2; i++)
        {
            now += dt;
            sinceShot += dt;
            if (sinceShot >= shotInterval)
            {
                heldSlots[0].LastUpdate = now; // a round goes out this frame (B5's restamp, simulated)
                sinceShot = 0f;
            }
            AimAssist.Tick(heldSlots, now, dt, forgetInterval, 0f);
        }
        ctx.Check(heldSlots[0].Target.IsEqualApprox(marker),
            $"aim-assist forget: continuous fire never lets the target unwind");
    }

    // Golden check: the keys B2/B4 consume parse off the real player.json and
    // weapons.json at their documented shipped values, not just their compiled-in defaults. The
    // dist_factor one matters most — it ships at 0.0 where the executable's compiled fallback is
    // 2.5e-4, so a reader that quietly failed to find the key would restore a distance term the
    // shipped data deliberately turns off.
    internal static void AimAssistShippedData(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        ctx.Check(Mathf.IsEqualApprox(5.0f, stats.StickyBulletCatchupRate),
            $"sticky_bullet_catchup_rate parses as the shipped 5.0");
        ctx.Check(Mathf.IsEqualApprox(1.5f, stats.StickyBulletForgetInterval),
            $"sticky_bullet_forget_interval parses as the shipped 1.5");
        ctx.Check(stats.StickyBulletDistFactor == 0f,
            $"sticky_bullet_dist_factor parses as the shipped 0.0 (compiled default 2.5e-4), so selection is purely most-aligned");
        ctx.Check(Mathf.IsEqualApprox(1.0f, Mathf.RadToDeg(stats.StickyBulletInaccuracy), 1e-4f),
            $"sticky_bullet_inaccuracy parses as the shipped 1.0 DEGREE, stored in radians as the original stores it");

        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var gun = weapons.All.FirstOrDefault(w => w.IsGun && w.CannonSpread is > 0f);
        ctx.Check(gun != null, $"a gun ships CANNON_SPREAD — the assist's acceptance cone (BL-342 A1)");
        if (gun != null)
        {
            ctx.Check(Mathf.IsEqualApprox(6.0f, gun.CannonSpread!.Value),
                $"{gun.Id} ships CANNON_SPREAD 6.0, a 6° cone half-angle: cos={AimAssist.WeaponConeCos(gun):0.000000}");
        }
    }

    // B3's constant-velocity intercept solver (AimAssist.TryIntercept): a
    // stationary target dead ahead solves to the plain displacement direction with t =
    // distance/speed; a crossing target's solved direction and t place the round at exactly the
    // target's projected position (self-consistency, not an independent re-derivation of the
    // quadratic); a target receding faster than the round returns no solution rather than a
    // bogus direction.
    internal static void AimAssistIntercept(TestContext ctx)
    {
        var muzzle = Vector3.Zero;
        const float speed = 300f;

        var deadAhead = new Vector3(0f, 0f, -100f);
        bool hit = AimAssist.TryIntercept(muzzle, speed, deadAhead, Vector3.Zero, out var aimDir, out float t);
        ctx.Check(hit, $"aim-assist intercept: a stationary target dead ahead solves");
        if (hit)
        {
            ctx.Check(aimDir.IsEqualApprox(deadAhead.Normalized()),
                $"aim-assist intercept: dead-ahead aim direction equals the displacement direction");
            ctx.Check(Mathf.IsEqualApprox(t, deadAhead.Length() / speed, 1e-4f),
                $"aim-assist intercept: dead-ahead t equals distance/speed");
        }

        var crossingPos = new Vector3(0f, 0f, -200f);
        var crossingRelVel = new Vector3(50f, 0f, 0f); // crosses left-to-right at 50 m/s
        hit = AimAssist.TryIntercept(muzzle, speed, crossingPos, crossingRelVel, out aimDir, out t);
        ctx.Check(hit, $"aim-assist intercept: a crossing target solves");
        if (hit)
        {
            var roundAt = muzzle + aimDir * speed * t;
            var targetAt = crossingPos + crossingRelVel * t;
            ctx.Check((roundAt - targetAt).Length() < 1e-2f,
                $"aim-assist intercept: the crossing target's solved direction and t meet at the same point");
            ctx.Check(!aimDir.IsEqualApprox(crossingPos.Normalized()),
                $"aim-assist intercept: a crossing target's aim leads it, not fired at its current position");
        }

        var recedingPos = new Vector3(0f, 0f, -100f);
        var recedingRelVel = new Vector3(0f, 0f, -500f); // outruns the 300 m/s round in a straight line
        hit = AimAssist.TryIntercept(muzzle, speed, recedingPos, recedingRelVel, out _, out _);
        ctx.Check(!hit, $"aim-assist intercept: a target outrunning the round has no solution");
    }

    // The B4 scan fixture's baseline context — see the ScanSpeed/ScanRange/ScanConeDeg constants.
    internal static AimScan MakeScan(float distFactor = 0f, object? self = null) => new()
    {
        MuzzlePosition = Vector3.Zero,
        ShooterVelocity = Vector3.Zero,
        Forward = Vector3.Forward,
        Team = AimAssist.TeamOfPilot(0),
        Speed = ScanSpeed,
        RangeSquared = ScanRange * ScanRange,
        ConeCos = Mathf.Cos(Mathf.DegToRad(ScanConeDeg)),
        DistFactor = distFactor,
        Self = self,
    };

    // A point `range` metres out, `offAxisDeg` off the shooter's nose in the XZ plane.
    internal static Vector3 ScanPoint(float range, float offAxisDeg)
    {
        float a = Mathf.DegToRad(offAxisDeg);
        return new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a)) * range;
    }

    // B4's rejection gates, each proved able to fail: the same candidate that is accepted
    // on the baseline is rejected when exactly one thing changes. Covers the engine's order —
    // self, not live, same team (and either side unaffiliated), out of RANGE, outside the cone —
    // plus the per-target `+0x50` cone override, which nothing ships but which is ported
    // deliberately (Decision 2), and the turret pass, which exists and iterates nothing until M4
    // puts a list in it.
    internal static void AimAssistScanGates(TestContext ctx)
    {
        int enemy = AimAssist.TeamOfPilot(1);
        var deadAhead = ScanPoint(300f, 0f);
        var set = new AimCandidateSet();

        void Only(int team, bool live, Vector3 at, object? source = null,
            float cone = AimAssist.NoConeOverride)
        {
            set.Clear();
            set.AddVehicle(at, Vector3.Zero, team, live, source, cone);
        }

        Only(enemy, live: true, deadAhead);
        var scan = MakeScan();
        ctx.Check(AimAssist.Scan(scan, set, out var best) && best.Kind == AimTargetKind.Vehicle,
            $"aim-assist scan: baseline — a live enemy 300 m dead ahead is accepted");
        ctx.Check(best.Direction.IsEqualApprox(Vector3.Forward),
            $"aim-assist scan: a stationary target dead ahead scores on the plain nose direction");

        Only(AimAssist.TeamOfPilot(0), live: true, deadAhead);
        ctx.Check(!AimAssist.Scan(scan, set, out _), $"aim-assist scan: a SAME-team target is rejected");

        Only(AimAssist.NeutralTeam, live: true, deadAhead);
        ctx.Check(!AimAssist.Scan(scan, set, out _),
            $"aim-assist scan: an unaffiliated (team 0) target is rejected — 0 is not a wildcard");

        Only(enemy, live: true, deadAhead);
        var neutralShooter = MakeScan();
        neutralShooter.Team = AimAssist.NeutralTeam;
        ctx.Check(!AimAssist.Scan(neutralShooter, set, out _),
            $"aim-assist scan: an unaffiliated SHOOTER snaps onto nothing — either side being 0 rejects the pair");

        Only(enemy, live: false, deadAhead);
        ctx.Check(!AimAssist.Scan(scan, set, out _),
            $"aim-assist scan: a dead / not-yet-live target is rejected (the vtable +0x14 predicate)");

        var self = new object();
        Only(enemy, live: true, deadAhead, source: self);
        ctx.Check(!AimAssist.Scan(MakeScan(self: self), set, out _),
            $"aim-assist scan: the shooter never snaps onto itself");

        Only(enemy, live: true, ScanPoint(ScanRange - 100f, 0f));
        ctx.Check(AimAssist.Scan(scan, set, out _),
            $"aim-assist scan: a target just inside RANGE ({ScanRange - 100f:0} m of {ScanRange:0} m) is accepted");
        Only(enemy, live: true, ScanPoint(ScanRange + 100f, 0f));
        ctx.Check(!AimAssist.Scan(scan, set, out _),
            $"aim-assist scan: a target beyond RANGE ({ScanRange + 100f:0} m) is rejected");

        Only(enemy, live: true, ScanPoint(300f, ScanConeDeg - 0.5f));
        ctx.Check(AimAssist.Scan(scan, set, out _),
            $"aim-assist scan: {ScanConeDeg - 0.5f:0.0}° off-axis, just INSIDE the {ScanConeDeg:0}° cone, is accepted");
        Only(enemy, live: true, ScanPoint(300f, ScanConeDeg + 0.5f));
        ctx.Check(!AimAssist.Scan(scan, set, out _),
            $"aim-assist scan: {ScanConeDeg + 0.5f:0.0}° off-axis, just OUTSIDE it, is rejected");

        // The per-target override: the same off-cone geometry, accepted because the candidate
        // advertises a wider cone of its own (a half-angle in RADIANS, unlike the weapon's degrees).
        Only(enemy, live: true, ScanPoint(300f, ScanConeDeg + 0.5f), cone: Mathf.DegToRad(20f));
        ctx.Check(AimAssist.Scan(scan, set, out _),
            $"aim-assist scan: a target advertising a 20° cone (+0x50) is accepted where the weapon's {ScanConeDeg:0}° rejected it");

        // The turret pass: nothing in CSVM fills this list until M4, so the check that matters is
        // that the pass is wired — a turret put in it can win, and no code change is owed.
        set.Clear();
        set.AddTurret(deadAhead, Vector3.Zero, enemy, live: true, source: null);
        ctx.Check(AimAssist.Scan(scan, set, out best) && best.Kind == AimTargetKind.Turret,
            $"aim-assist scan: the turret pass exists and scores — M4 wires a list in, it does not re-derive this");
    }

    // Selection among survivors, on the shipped `dist_factor 0.0`: a distant
    // on-axis target outranks a near off-axis one at any range inside RANGE, because the distance
    // term is deleted outright. The contrast case runs the identical geometry at the executable's
    // compiled 2.5e-4 default and shows the winner FLIPS — so this is a measurement of the shipped
    // value, not of the arithmetic being insensitive to it.
    internal static void AimAssistSelection(TestContext ctx)
    {
        int enemy = AimAssist.TeamOfPilot(1);
        var far = new object();
        var near = new object();
        var set = new AimCandidateSet();
        set.AddVehicle(ScanPoint(900f, 0f), Vector3.Zero, enemy, live: true, far);
        set.AddVehicle(ScanPoint(100f, 5f), Vector3.Zero, enemy, live: true, near);

        ctx.Check(AimAssist.Scan(MakeScan(), set, out var best) && ReferenceEquals(best.Source, far),
            $"aim-assist selection: at the shipped dist_factor 0.0 a 900 m on-axis target beats a 100 m 5°-off one");
        ctx.Check(AimAssist.Scan(MakeScan(distFactor: 2.5e-4f), set, out best) && ReferenceEquals(best.Source, near),
            $"aim-assist selection: at the executable's 2.5e-4 default the same pair flips to the near one — the term is live, the shipped data turns it off");
    }

    // The rocket snap, on a live pool: a real proximity-fused round in flight is a
    // candidate, and it outranks the aircraft behind it. The ordnance list is a FILTER over the
    // rounds in flight, so this is the one B4 case that cannot be proved off-engine — and the gun
    // round fired alongside is the able-to-fail half: it is in the same pool, alive, and must NOT
    // be collected, since it carries no proximity fuse.
    internal static void AimAssistOrdnancePriority(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"weapon definitions");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        // Data-driven, not hardcoded ids: the fuse test IS the list membership rule, so the fixture
        // asserts its two halves off the data before relying on them.
        var fused = weapons.All.FirstOrDefault(w => w.DetonationDistance is > AimAssist.MinFuseDistance);
        var gun = weapons.All.FirstOrDefault(w => w.IsGun && w.DetonationDistance is not > AimAssist.MinFuseDistance);
        ctx.Check(fused != null, $"a weapon ships DETONATION_DISTANCE > {AimAssist.MinFuseDistance:0.0} m — the assist's ordnance-list test");
        ctx.Check(gun != null, $"a gun ships no proximity fuse, so it is NOT ordnance the assist can snap onto");
        if (fused == null || gun == null)
            return;

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            // The incoming round: on the shooter's nose axis 400 m out, flying straight back at it,
            // fired by player 1. The aircraft sits 800 m out and 3° off-axis — inside the cone, so
            // it is a genuine survivor the round has to outrank, not a candidate the gates drop.
            var incoming = ScanPoint(400f, 0f);
            live.Spawn(fused, new Transform3D(Basis.LookingAt(Vector3.Back, Vector3.Up), incoming),
                Vector3.Zero, shooterId: 1);
            live.Spawn(gun, new Transform3D(Basis.LookingAt(Vector3.Back, Vector3.Up), ScanPoint(380f, 0f)),
                Vector3.Zero, shooterId: 1);
            live.SimStep(1f / 60f);

            var set = new AimCandidateSet();
            live.CollectFusedOrdnance(set);
            ctx.Same(1, set.Ordnance.Count,
                $"the fused round is a candidate and the gun round in the same pool is not");

            var plane = new object();
            set.AddVehicle(ScanPoint(800f, 3f), Vector3.Zero, AimAssist.TeamOfPilot(1), live: true, plane);
            var scan = MakeScan();
            ctx.Check(AimAssist.Scan(scan, set, out var best), $"aim-assist ordnance: the scan finds a target");
            ctx.Check(best.Kind == AimTargetKind.Ordnance,
                $"aim-assist ordnance: the guns snap onto the incoming fused round, not the aircraft behind it (won={best.Kind} score={best.Score:0.0000})");

            // Able to fail the other way: drop the round out of the pool and the aircraft wins.
            live.Clear();
            set.Clear();
            live.CollectFusedOrdnance(set);
            set.AddVehicle(ScanPoint(800f, 3f), Vector3.Zero, AimAssist.TeamOfPilot(1), live: true, plane);
            ctx.Check(AimAssist.Scan(scan, set, out best) && best.Kind == AimTargetKind.Vehicle,
                $"aim-assist ordnance: with no round in flight the same aircraft wins — the snap was the round, not the ranking");
        }
        finally
        {
            pool?.Free();
            textures.Dispose();
        }
    }

    // B5's launch scatter (AimAssist.Scatter): every round lands inside the
    // cone, the polar angle is UNIFORM IN THE ANGLE rather than over the cone's solid angle (the
    // reflex port, and what `ProjectilePool.ApplySpread`'s `sqrt(rand)` does, which would
    // pile shots at the rim), and the roll about the aim axis covers the full circle. The
    // able-to-fail control is the solid-angle sampling itself, computed alongside from the same
    // draws: it fails the flatness test this one passes.
    internal static void AimAssistScatter(TestContext ctx)
    {
        const int shots = 20000;
        const int bins = 5;
        float cone = Mathf.DegToRad(1f); // the shipped sticky_bullet_inaccuracy
        var rng = new RandomNumberGenerator { Seed = 20260813 };
        var aim = new Vector3(0.3f, -0.2f, -0.9f).Normalized(); // deliberately off every world axis
        var flat = new int[bins];
        var cap = new int[bins];
        var rollQuadrants = new int[4];
        float maxAngle = 0f;
        // A frame to measure the roll in: any two axes perpendicular to the aim direction.
        var right = aim.Cross(Vector3.Up).Normalized();
        var up = right.Cross(aim).Normalized();
        for (int i = 0; i < shots; i++)
        {
            var dir = AimAssist.Scatter(aim, cone, rng);
            float angle = aim.AngleTo(dir);
            maxAngle = Mathf.Max(maxAngle, angle);
            flat[Mathf.Min(bins - 1, (int)(angle / cone * bins))]++;
            // The same draw scored as a solid-angle sample would be: equal-AREA bands, which is
            // what "uniform over the cap" means. A flat-in-angle sample fills these unevenly.
            float band = 1f - Mathf.Cos(angle);
            float bandMax = 1f - Mathf.Cos(cone);
            cap[Mathf.Min(bins - 1, (int)(band / bandMax * bins))]++;
            var off = dir - aim * dir.Dot(aim);
            if (off.LengthSquared() > 0f)
            {
                float roll = Mathf.Atan2(off.Dot(up), off.Dot(right)) + Mathf.Pi;
                rollQuadrants[Mathf.Min(3, (int)(roll / Mathf.Tau * 4f))]++;
            }
        }
        ctx.Check(maxAngle <= cone + 1e-5f,
            $"aim-assist scatter: every round is inside the {Mathf.RadToDeg(cone):0.0}° cone (worst {Mathf.RadToDeg(maxAngle):0.000}°)");
        float lo = (float)flat[0] / shots * bins;
        float hi = (float)flat[bins - 1] / shots * bins;
        ctx.Check(lo is > 0.85f and < 1.15f && hi is > 0.85f and < 1.15f,
            $"aim-assist scatter: the polar angle is flat across [0,θ] — innermost band {lo:0.00}× of even, outermost {hi:0.00}× (1.00 = flat)");
        float capLo = (float)cap[0] / shots * bins;
        ctx.Check(capLo > 1.5f,
            $"able to fail: scored as equal-AREA bands the same draws are anything but flat ({capLo:0.00}× in the innermost) — a solid-angle port would have passed the check above and failed this one");
        int rollMin = Mathf.Min(Mathf.Min(rollQuadrants[0], rollQuadrants[1]), Mathf.Min(rollQuadrants[2], rollQuadrants[3]));
        ctx.Check(rollMin > shots / 4 * 9 / 10,
            $"aim-assist scatter: the roll about the aim axis covers the whole circle (thinnest quadrant {rollMin} of {shots / 4} even)");
    }

    // B5's fire-call step order (`FUN_004b6530`), which is asymmetric on purpose: the
    // scan updates the slot's TARGET, and what leaves the muzzle is the SMOOTHED direction from
    // previous frames. Run on a rolled plane basis, so a world/local mix-up cannot pass: the fired
    // direction must be the smoothed LOCAL vector rotated out to world (inside the scatter cone),
    // and the stored target must be the scan winner rotated INTO local. Firing this frame's scan
    // result instead would remove the lag entirely and read as an aimbot.
    internal static void AimAssistFireDirection(TestContext ctx)
    {
        var rng = new RandomNumberGenerator { Seed = 4242 };
        float cone = Mathf.DegToRad(1f);
        // A plane rolled 30° and yawed 40° — nothing lines up with the world axes.
        var basis = new Basis(Vector3.Up, Mathf.DegToRad(40f)) * new Basis(Vector3.Forward, Mathf.DegToRad(30f));
        var nose = -basis.Z;
        var smoothedLocal = new Vector3(0.25f, 0.1f, -0.96f).Normalized(); // a gun line lagging off-centre
        var slots = new[]
        {
            new GunAimSlot { Active = true, Smoothed = smoothedLocal, Target = AimAssist.LocalForward, LastUpdate = 0.0 },
        };

        // A target 8° off the nose, well inside a 20° cone, stationary — its intercept direction is
        // just the displacement direction, so the expected local target is computable by hand.
        var muzzle = new Vector3(0f, 500f, 0f);
        var offAxis = (nose + basis.X * 0.14f).Normalized();
        var targetPos = muzzle + offAxis * 400f;
        var set = new AimCandidateSet();
        set.AddVehicle(targetPos, Vector3.Zero, AimAssist.TeamOfPilot(1), live: true, source: null);
        var scan = new AimScan
        {
            MuzzlePosition = muzzle,
            ShooterVelocity = Vector3.Zero,
            Forward = nose,
            Team = AimAssist.TeamOfPilot(0),
            Speed = ScanSpeed,
            RangeSquared = ScanRange * ScanRange,
            ConeCos = Mathf.Cos(Mathf.DegToRad(20f)),
            DistFactor = 0f,
            Self = null,
        };

        var fired = AimAssist.FireDirection(ref slots[0], scan, set, basis, now: 12.5, cone, rng, out var found);
        ctx.Check(found.Found, $"aim-assist fire: the scan found the off-axis target");
        var expectedFired = (basis * smoothedLocal).Normalized();
        ctx.Check(fired.AngleTo(expectedFired) <= cone + 1e-5f,
            $"aim-assist fire: the round leaves along the SMOOTHED line (off by {Mathf.RadToDeg(fired.AngleTo(expectedFired)):0.000}°, inside the {Mathf.RadToDeg(cone):0.0}° scatter) — not this frame's scan result");
        ctx.Check(fired.AngleTo(offAxis) > cone,
            $"able to fail: the scan winner is {Mathf.RadToDeg(fired.AngleTo(offAxis)):0.0}° away from what was fired, so firing it instead would have been visible here");
        var expectedTarget = (basis.Transposed() * offAxis).Normalized();
        ctx.Check(slots[0].Target.Dot(expectedTarget) > 1f - 1e-3f,
            $"aim-assist fire: the winner is stored as the slot's plane-LOCAL target, ready for the next frame's catch-up");
        ctx.Check(slots[0].LastUpdate == 12.5,
            $"aim-assist fire: the shot restamps the slot, which is what makes the forget timer run from the last SHOT");

        // No candidate at all: the target unwinds to the plane's own forward (local forward), which
        // is the engine's "no target found" seed, and the fired direction is unchanged.
        set.Clear();
        AimAssist.FireDirection(ref slots[0], scan, set, basis, now: 13.0, cone, rng, out found);
        ctx.Check(!found.Found && slots[0].Target.Dot(AimAssist.LocalForward) > 1f - 1e-4f,
            $"aim-assist fire: with nothing to snap onto the slot's target is seeded with the plane's own forward axis");
    }

    // Loadout.ForRig against all 11 player airframes — 4 gun groups
    // covering every `firepointN` the rig actually carries (the Kestrel's odd 7th), one
    // hardpoint per `pylonN`, no marker bound to two groups, and every synthesized group
    // fireable even where stock marks the slot a turret.
    internal static void LoadoutForRig(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, Messages.Load(ctx.MessagesPath));
        var stock = StockLoadouts.Load();
        var textures = new TextureArchive(texturesPath);
        try
        {
            foreach (var (model, display) in MarkerRig.PlayerAirframes)
            {
                var rig = MarkerRig.Extract(planesGamez, model);
                ctx.Check(rig != null, $"{display}: marker rig extracted");
                if (rig == null)
                {
                    continue;
                }
                LoadoutDef? stockDef = null;
                foreach (var d in stock.All.Values)
                {
                    if (d.Model == model)
                    {
                        stockDef = d;
                        break;
                    }
                }

                Node3D? plane = null;
                try
                {
                    plane = new PlaneBuilder(planesGamez, textures).Build(model);
                    ctx.Host.AddChild(plane);
                    var loadout = Loadout.ForRig(plane, weapons, stockDef);
                    ctx.Same(4, loadout.Guns.Count, $"{display}: gun groups synthesized");

                    var bound = new HashSet<string>();
                    bool boundTwice = false;
                    foreach (var g in loadout.Guns)
                    {
                        ctx.Check(!g.IsTurret, $"{display}: slot {g.Slot} fireable in the lab (never inert)");
                        foreach (var m in g.Muzzles)
                        {
                            string name = m.HasMeta(AnimRuntime.NameMeta)
                                ? m.GetMeta(AnimRuntime.NameMeta).AsString() : m.Name;
                            if (!bound.Add(name))
                            {
                                boundTwice = true;
                            }
                        }
                    }
                    ctx.Check(!boundTwice, $"{display}: no firepoint bound to two groups");

                    int rigFirepoints = 0, rigPylons = 0;
                    foreach (var m in rig.Markers)
                    {
                        if (m.Kind == MarkerRig.MarkerKind.Firepoint)
                        {
                            rigFirepoints++;
                        }
                        else if (m.Kind == MarkerRig.MarkerKind.Pylon)
                        {
                            rigPylons++;
                        }
                    }
                    ctx.Same(rigFirepoints, bound.Count, $"{display}: every rig firepoint covered");
                    ctx.Same(rigPylons, loadout.Hardpoints.Count, $"{display}: one hardpoint per pylon");
                }
                finally
                {
                    plane?.Free();
                }
            }
        }
        finally
        {
            textures.Dispose();
        }
    }

    // The fly-mode damage-trail regression: Godot's TopLevel toggle PRESERVES the node's global
    // transform, so a trail emitter parented under a flying plane kept the plane's attitude as its
    // basis and every world-space puff was yawed around the world origin. Invisible at the identity
    // -Z heading, which is why the parked viewer and every scripted dive looked fine. The suite feeds
    // a trail under a carrier at the C1 spawn pose and asserts the emitter re-anchored to identity.
    internal static void TrailWorldAnchor(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var textures = new TextureArchive(texturesPath);
        Node3D? carrier = null;
        try
        {
            // A flying plane stand-in: the C1 default spawn's pose (heading well off -Z, 9 km
            // from the world origin) — the exact conditions that made the bug invisible to every
            // earlier -Z-heading check.
            var pose = new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(132f)),
                new Vector3(-7065f, 326f, -5519f));
            carrier = new Node3D();
            ctx.Host.AddChild(carrier);
            carrier.GlobalTransform = pose;

            var trail = Effects.Puffer.MakePuffer(ctx.ZrdrPath, textures, carrier, "pufftrails.json", "firepuffer");
            ctx.Check(trail != null, $"dense_firetrail firepuffer builds from pufftrails.json");
            if (trail == null)
                return;

            var a = pose.Origin;
            var b = a + new Vector3(3f, 0f, -2f); // several DISTANCE_INTERVALs of motion
            trail.Emit(a, pose.Basis, 0f);
            trail.Emit(b, pose.Basis, 0f);
            ctx.Check(trail.LiveCount > 0, $"puffs spawned over {a.DistanceTo(b):0.0} m of motion live={trail.LiveCount}");
            ctx.Check(trail.GlobalTransform.Basis.IsEqualApprox(Basis.Identity),
                $"emitter basis is world identity under the rotated carrier basis={trail.GlobalTransform.Basis}");
            ctx.Check(trail.GlobalTransform.Origin.IsEqualApprox(Vector3.Zero),
                $"emitter origin is the world origin origin={trail.GlobalTransform.Origin}");

            // One manual tick lands the CPU particles in the MultiMesh buffer; the rendered
            // instance must sit on the fed segment (walk-back spawning plus deviation jitter
            // keeps every puff within an interval of it), not rotated kilometres away.
            trail._Process(1.0 / 60.0);
            var mmi = trail.GetChildren().OfType<MultiMeshInstance3D>().FirstOrDefault();
            ctx.Check(mmi != null, $"trail emitter carries a MultiMeshInstance3D");
            if (mmi != null)
            {
                var inst = (mmi.GlobalTransform * mmi.Multimesh.GetInstanceTransform(0)).Origin;
                float offSegment = inst.DistanceTo(a) + inst.DistanceTo(b) - a.DistanceTo(b);
                ctx.Check(offSegment < 1f,
                    $"first rendered puff sits on the fed segment inst=({inst.X:0.0},{inst.Y:0.0},{inst.Z:0.0}) off={offSegment:0.00} m");
            }
        }
        finally
        {
            carrier?.Free();
            textures.Dispose();
        }
    }

    // The incoming-fire near-miss cue's wiring, with its able-to-fail baseline: a real round from
    // another pilot flying past registers a pass, the same round fired by the target's own identity
    // registers none, and a round a hundred metres wide of the aircraft registers none either, so a
    // pass count of 1 means the geometry. The accumulator's own arithmetic is unit-tested off-engine
    // (WarningShotCueTests); this is the pool half, on real ballistics.
    internal static void WarningShot(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"weapon definitions");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        if (!weapons.TryGet("wep_01", out var gun))
        {
            ctx.Check(false, $"wep_01 definition loads");
            return;
        }
        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);
            var target = new Vector3(0f, 500f, 0f);
            int passes = 0;
            float closest = float.MaxValue;
            live.NearMissTargets.Add(new ProjectilePool.NearMissTarget
            {
                ShooterId = 0,
                Position = () => target,
                OnPass = d =>
                {
                    passes++;
                    closest = Mathf.Min(closest, d);
                },
            });

            // A round overtaking the aircraft 3 m abeam, fired 60 m astern along +Z. A round leaves
            // dead straight (A1 — CANNON_SPREAD is not a dispersion cone), so the pass distance is
            // the requested one, not a budget against a scatter cone.
            void FireBy(int shooter, float abeam)
            {
                var origin = target + new Vector3(abeam, 0f, -60f);
                live.Spawn(gun, new Transform3D(Basis.LookingAt(Vector3.Back, Vector3.Up), origin),
                    Vector3.Zero, shooter);
                for (int i = 0; i < 60; i++)
                    live.SimStep(1f / 60f);
                live.Clear();
            }

            FireBy(shooter: 1, abeam: 3f);
            ctx.Check(passes > 0, $"another pilot's round registers a pass passes={passes}");
            ctx.Check(closest <= WarningShotCue.PassRadius,
                $"the pass is measured, not assumed closest={(closest < float.MaxValue ? closest : -1f):0.0} m");

            passes = 0;
            FireBy(shooter: 0, abeam: 3f);
            ctx.Same(0, passes, $"the target's OWN round never warns it");

            passes = 0;
            FireBy(shooter: 1, abeam: 100f);
            ctx.Same(0, passes, $"a round 100 m wide registers nothing");
        }
        finally
        {
            pool?.Free();
            textures.Dispose();
        }
    }

    // The decoded graze restitution on real contacts: a shallow dive onto a floor and a shallow scrape
    // along a vertical wall, flown by a real rig through the real collision sweep. Two things need a
    // live contact and cannot be read off FlightModel (whose arithmetic BounceRestitutionTests pins):
    // the player-only gate, where an AI rig on the same trajectory gets only the position correction,
    // and both orientations. ⚠ The wall assertion is about the rebound AXIS, not a
    // per-surface coefficient; the impulse has no surface dependence, only the contact normal differs.
    internal static void GrazeBounce(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        ctx.Check(Mathf.IsEqualApprox(stats.BounceFactor, 0.6f),
            $"the airframe carries this install's authored bounce_factor={stats.BounceFactor:0.###}");

        var textures = new TextureArchive(texturesPath);
        StaticBody3D? surface = null;
        FlightController? rig = null;
        try
        {
            // One contact run: a rig placed startPos out, flying dir at speed, stepped until the resolver
            // answers. Returns the normal-direction speed entering and leaving (positive = away from the
            // surface) with the vertical pair alongside, so the wall run reads on the axis the altimeter sees.
            (float NormalIn, float NormalOut, float VerticalIn, float VerticalOut, bool Contacted,
             bool Crashed) Run(bool human, Vector3 startPos, Vector3 dir, Vector3 normal, float speed)
            {
                var plane = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
                var model = new FlightModel(stats, aiForcePath: !human);
                rig = new FlightController
                {
                    PlaneModel = plane,
                    Collider = PlaneCollider.Build(plane),
                    Damage = new PlaneDamage(stats.DestroyableParts),
                    PlayerIndex = 0,
                    IsHumanPiloted = human,
                    // ⚠ Keep both runs pilot-less. An AiPilot would fly its own course off a ground probe, so the
                    // two trajectories would stop being the same one and the gate would no longer be the only
                    // difference reaching the contact.
                    UseKeyboard = false,
                    PadDevices = System.Array.Empty<int>(),
                    AllowPause = false,
                };
                rig.AddChild(plane);
                rig.Setup(model, human ? ctx.Camera : null, new CamParams(), startPos, startPos + dir);
                ctx.Host.AddChild(rig);
                // Straight onto the approach, past whatever Setup's respawn left: the model is ours,
                // so the state entering the sweep is stated rather than flown into.
                model.Reset(startPos, Basis.LookingAt(dir, Vector3.Up), speed, 0f);
                model.VelocityDir = dir;
                // This suite isolates collision response. The strengthened decoded AI ground-blow
                // impulse would otherwise turn its unpiloted AI control case before the sweep.
                model.Stats.GroundBlowElev = 0f;

                float nIn = 0f, nOut = 0f, yIn = 0f, yOut = 0f;
                bool contacted = false;
                for (int i = 0; i < 900 && !contacted && !rig.Crashed; i++)
                {
                    var before = model.VelocityDir * model.Speed;
                    rig.SimStep(1f / 60f);
                    var after = model.VelocityDir * model.Speed;
                    // The resolver is the only thing in the frame that can turn the normal
                    // component around; nothing else moves it by metres per second in one step.
                    if (after.Dot(normal) - before.Dot(normal) > 0.5f)
                    {
                        contacted = true;
                        nIn = before.Dot(normal);
                        nOut = after.Dot(normal);
                        yIn = before.Y;
                        yOut = after.Y;
                    }
                }
                bool crashed = rig.Crashed;
                var freed = rig;
                rig = null;
                freed.Free();
                return (nIn, nOut, yIn, yOut, contacted, crashed);
            }

            // Flat ground: a 15 degree descent at 60 m/s puts 15.5 m/s on the normal, under the 25 m/s crash
            // threshold, so this is the survivable graze the impulse belongs to. Started a few metres out
            // because the AI plant's ground blow flies the AI rig off this trajectory over a long approach.
            surface = Plate("graze-floor", new Vector3(600f, 4f, 600f), new Vector3(0f, -2f, 0f));
            ctx.Host.AddChild(surface);
            var descent = new Vector3(0f, -Mathf.Sin(Mathf.DegToRad(15f)),
                                      -Mathf.Cos(Mathf.DegToRad(15f))).Normalized();
            var start = new Vector3(0f, 6f, 30f);

            var player = Run(true, start, descent, Vector3.Up, 60f);
            var ai = Run(false, start, descent, Vector3.Up, 60f);
            surface.Free();
            surface = null;

            ctx.Check(player.Contacted && !player.Crashed,
                $"the player rig grazed the floor and survived it vn={-player.NormalIn:0.0} m/s");
            // ⚠ The AI does NOT survive this, and that is the decoded local_11 rule (0x0048d79e),
            // not a regression: a non-player striker that resolved anything other than an
            // aeroplane is destroyed whatever health it has left.
            ctx.Check(ai.Crashed,
                $"an AI aircraft is destroyed outright by the same terrain contact the player grazes (crashed={ai.Crashed})");
            if (player.Contacted)
            {
                ctx.Check(player.NormalOut > 0.3f * -player.NormalIn,
                    $"the player rebounds along the contact normal in={player.NormalIn:0.00} out={player.NormalOut:0.00} m/s (bounce_factor {stats.BounceFactor:0.##} × the lever partition)");
                ctx.Check(player.VerticalOut > 0f,
                    $"…and on flat ground that rebound is what the altimeter reads vy {player.VerticalIn:0.00} → {player.VerticalOut:0.00} m/s");
                ctx.Check(!ai.Contacted || Mathf.Abs(ai.NormalOut) < 0.05f * -ai.NormalIn,
                    $"an AI aircraft never gains normal speed from a contact — no impulse (0x0048d7f0's player gate) in={ai.NormalIn:0.00} out={ai.NormalOut:0.00} m/s");
                ctx.Note($"floor graze: player {player.NormalIn:0.00} → {player.NormalOut:0.00} m/s on the normal (e={player.NormalOut / -player.NormalIn:0.00}), AI crashed={ai.Crashed}");
            }

            // --- a vertical face, same approach angle, so the only thing that changes is which way
            // the normal points. The rebound must follow the normal and leave the altimeter alone.
            surface = Plate("graze-wall", new Vector3(4f, 600f, 600f), new Vector3(-100f, 0f, 0f));
            ctx.Host.AddChild(surface);
            var scrape = new Vector3(-Mathf.Sin(Mathf.DegToRad(10f)), 0f,
                                     -Mathf.Cos(Mathf.DegToRad(10f))).Normalized();
            var wallNormal = Vector3.Right;
            var wallStart = new Vector3(-30f, 200f, 300f);

            var alongWall = Run(true, wallStart, scrape, wallNormal, 60f);
            surface.Free();
            surface = null;

            ctx.Check(alongWall.Contacted && !alongWall.Crashed,
                $"the player rig scraped the vertical face and survived it vn={-alongWall.NormalIn:0.0} m/s");
            if (alongWall.Contacted)
            {
                ctx.Check(alongWall.NormalOut > 0.3f * -alongWall.NormalIn,
                    $"the same impulse fires on a wall — no surface test anywhere in it in={alongWall.NormalIn:0.00} out={alongWall.NormalOut:0.00} m/s");
                ctx.Check(Mathf.Abs(alongWall.VerticalOut - alongWall.VerticalIn) < 1f,
                    $"…and it is entirely horizontal: an altimeter reads nothing across the contact vy {alongWall.VerticalIn:0.00} → {alongWall.VerticalOut:0.00} m/s (CAP-14's vertical-face runs)");
                ctx.Note($"wall scrape: {alongWall.NormalIn:0.00} → {alongWall.NormalOut:0.00} m/s on the normal (e={alongWall.NormalOut / -alongWall.NormalIn:0.00}), vy {alongWall.VerticalIn:0.00} → {alongWall.VerticalOut:0.00}");
            }
        }
        finally
        {
            rig?.Free();
            surface?.Free();
            textures.Dispose();
        }
    }

    // The AI flavour of an airframe. The original spawns its AI aircraft from the AI def chain, never
    // the player one, and no such chain authors destroyable_parts, so an AI plane is ZONE-LESS: an
    // authored whole armor/health pair and no per-part ledger. Two halves, because the regressions
    // live in different places. The data half pins the resolver over all eleven airframes; the spawn
    // half pins the glue, since a null PlaneDamage is invulnerable and a drifted DefName misses the
    // stock-loadout table and flies unarmed. Both would ship green without this.
    internal static void AiPlaneDefs(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");

        // node name, AI def, authored pair, def-level injure entries. The pools are vehicle.json's
        // own (docs/formats/vehicle.md); the ladder is seven everywhere but the balmoral's eight.
        var airframes = new (string Node, string AiDef, float Pool, int Injure)[]
        {
            ("player_autogyro", "autogyro", 60f, 7),
            ("player_bhawk", "bloodhawk", 64f, 7),
            ("player_peacemaker", "peacemaker", 68f, 7),
            ("player_fury", "fury", 72f, 7),
            ("player_avenger", "avenger", 76f, 7),
            ("player_pfighter", "devastator", 80f, 7),
            ("player_brigand", "brigand", 84f, 7),
            ("player_kestrel", "kestrel", 84f, 7),
            ("player_fbrand", "firebrand", 88f, 7),
            ("player_warhawk", "warhawk", 96f, 7),
            ("player_balmoral", "balmoral", 100f, 8),
        };

        foreach (var (node, aiDef, pool, injure) in airframes)
        {
            var ai = PlaneStats.LoadForAi(ctx.ZrdrPath, node);
            ctx.Check(ai.AiDefName == aiDef, $"{node} resolves the AI def '{ai.AiDefName}' (want '{aiDef}')");
            ctx.Check(ai.VehicleHealth is { } h && Mathf.IsEqualApprox(h, pool)
                      && ai.VehicleArmor is { } a && Mathf.IsEqualApprox(a, pool),
                $"{aiDef} seeds its authored pair armor={ai.VehicleArmor:0.#} health={ai.VehicleHealth:0.#} (want {pool:0.#}/{pool:0.#})");
            ctx.Check(ai.DestroyableParts.Count == 0,
                $"{aiDef} is zone-less — destroyable_parts={ai.DestroyableParts.Count}");
            ctx.Check(ai.VehicleInjureAnims.Count == injure,
                $"{aiDef} carries the AI injure ladder: {ai.VehicleInjureAnims.Count} entries (want {injure})");

            // The identity split: the damage model moved, nothing else did. DefName still keys the
            // stock-loadout table (eleven player defs) and still feeds PlaneRoster's display name.
            var player = PlaneStats.Load(ctx.ZrdrPath, node);
            ctx.Check(ai.DefName == player.DefName && ai.AiDefName != ai.DefName,
                $"{node} keeps the player def '{ai.DefName}' as its identity while damage reads '{ai.AiDefName}'");
            ctx.Check(Mathf.IsEqualApprox(ai.FdSpeed, player.FdSpeed)
                      && Mathf.IsEqualApprox(ai.EnginePower, player.EnginePower)
                      && ai.TurretMounts.Count == player.TurretMounts.Count,
                $"{node} flies the same plant and carries the same turrets on both loads");
            ctx.Check(player.DestroyableParts.Count == 4 && player.VehicleHealth == null,
                $"…and the player load is untouched: {player.DestroyableParts.Count} zones, no authored pair");
        }

        // The Fury worked through: 90/90 summed over the player's four zones against the AI def's
        // authored 72/72 — the ~20 % the original's enemies were missing.
        var furyAi = PlaneStats.LoadForAi(ctx.ZrdrPath, "player_fury");
        var furyPlayer = PlaneStats.Load(ctx.ZrdrPath, "player_fury");
        float summed = 0f;
        foreach (var p in furyPlayer.DestroyableParts)
            summed += p.MaxHp;
        ctx.Note($"fury hull: AI {furyAi.VehicleHealth:0.#} authored against {summed:0.#} summed over the player zones");

        // --- the spawn half: through the real spawner, the glue that can silently drop either the
        // damage ledger or the loadout.
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        FlightController? spawned = null;
        ProjectilePool? projectiles = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            projectiles = live;
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
                WeaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null),
                Textures = textures,
                Shakes = ShakeDefs.Load(ctx.ZrdrPath),
            };

            var start = new Vector3(0f, 500f, 0f);
            var spawner = new FlightRoster(FlightRosterPolicy.From(spec), liveries, null!, ctx.Host, inputs, new FlightWorldBindings { Projectiles = live, Gamez = planesGamez }, new HumanRosterBindings());
            spawned = spawner.SpawnAi(new AiSpawn("player_fury", start, start + Vector3.Forward,
                AiPilot.HoldingCourse(start, start + Vector3.Forward)));

            // The regression that would otherwise arrive as "enemies are invulnerable": a zone-less
            // airframe passing a parts-only guard leaves Damage null and nothing can hurt it.
            ctx.Check(spawned.Damage != null,
                $"a zone-less AI Fury still carries a damage ledger");
            if (spawned.Damage is { } dmg)
            {
                ctx.Check(dmg.Parts.Count == 0, $"…with no zones: parts={dmg.Parts.Count}");
                // The per-spawn ±5 % lands on the authored pair, so this is a band, not an equality.
                float lo = 72f * (1f - PlaneStats.AiSpawnJitterSpread);
                float hi = 72f * (1f + PlaneStats.AiSpawnJitterSpread);
                ctx.Check(dmg.WholeHealthMax >= lo && dmg.WholeHealthMax <= hi,
                    $"…seeded off the authored 72 inside the jitter band: {dmg.WholeHealthMax:0.##} in [{lo:0.##}, {hi:0.##}]");
                ctx.Check(dmg.WholeHealthMax < summed * (1f - PlaneStats.AiSpawnJitterSpread),
                    $"…and strictly below the old summed-over-player-zones {summed:0.#}");
            }

            // The other silent one: DefName keys stock_loadouts.json, which holds the eleven player
            // defs alone. An AI def name there binds nothing and the plane flies with no guns.
            ctx.Check(spawned.Loadout != null,
                $"the AI Fury is armed — its stock loadout still binds off the player def name");
        }
        finally
        {
            spawned?.Free();
            projectiles?.Free();
            textures.Dispose();
        }
    }

    // The per-spawn jitter where only a real spawn can show it: through FlightRoster, over the
    // session's shared per-airframe stats cache, read out as flown trajectory rather than as a field.
    // Two aircraft off one airframe, given the same pose and the same orders, must fly apart; the same
    // ordinal drawn again must fly the same line; and the cache must come out untouched, since every
    // later spawn and every human rig reads it.
    internal static void AiSpawnJitter(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        FlightController? flying = null;
        ProjectilePool? pool = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            var spec = SessionSpec.Parse(System.Array.Empty<string>());
            var liveries = new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof"));
            // The one cache the real session holds: every aircraft below is built from THIS object.
            var shared = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
            var inputs = new AircraftAssemblyResources
            {
                PlanesGamez = planesGamez,
                StatsFor = _ => shared,
                // The same one object on both seams: this suite measures the jitter's spread over
                // a SHARED cache entry, so the AI flavour must not quietly become a second object.
                AiStatsFor = (_, _) => shared,
                PaintRng = new RandomNumberGenerator(),
                ZrdrPath = ctx.ZrdrPath,
                StockLoadouts = StockLoadouts.Load(),
                WeaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null),
                Textures = textures,
                Shakes = ShakeDefs.Load(ctx.ZrdrPath),
            };

            // One aircraft's flown displacement over a fixed window, straight and level on orders it never
            // has to correct, so what separates two runs is the plant, not the pilot. Freed at the end of its
            // own run: every run flies the same pose, and two aircraft there would be measuring a collision.
            var start = new Vector3(0f, 500f, 0f);
            Vector3 Fly(FlightRoster spawner)
            {
                var pilot = AiPilot.HoldingCourse(start, start + Vector3.Forward);
                var ai = spawner.SpawnAi(new AiSpawn(ctx.PlaneName, start, start + Vector3.Forward, pilot));
                flying = ai;
                for (int i = 0; i < 240; i++)
                    ai.SimStep(1f / 60f);
                var flown = ai.WorldPosition - start;
                flying = null;
                ai.Free();
                return flown;
            }

            // Two ordinals off one spawner: two aeroplanes, same pose, same orders.
            var spawner1 = new FlightRoster(FlightRosterPolicy.From(spec), liveries, null!, ctx.Host, inputs, new FlightWorldBindings { Projectiles = live, Gamez = planesGamez }, new HumanRosterBindings());
            var first = Fly(spawner1);
            var second = Fly(spawner1);

            // A fresh spawner restarts at ordinal 0, and the draw is keyed by ordinal — so this is
            // the same aircraft as `first`, which is what a --det replay reproduces.
            var spawner2 = new FlightRoster(FlightRosterPolicy.From(spec), liveries, null!, ctx.Host, inputs, new FlightWorldBindings { Projectiles = live, Gamez = planesGamez }, new HumanRosterBindings());
            var replay = Fly(spawner2);

            float spread = (first - second).Length();
            ctx.Check(spread > 1f,
                $"two aircraft off one airframe fly apart on identical orders: {spread:0.00} m over 4 s ({first.Length():0.0} m against {second.Length():0.0} m)");
            ctx.Check(spread < 0.25f * first.Length(),
                $"…by a 5 % spread, not a different aeroplane: {spread / first.Length() * 100f:0.0} % of the distance flown");
            ctx.Check(first.IsEqualApprox(replay),
                $"the same spawn ordinal replays the same line: {(first - replay).Length():0.000} m apart");
            ctx.Check(Mathf.IsEqualApprox(shared.FdSpeed, PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName).FdSpeed)
                && Mathf.IsEqualApprox(shared.EnginePower, PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName).EnginePower),
                $"the session's shared airframe stats came out unperturbed fd_speed={shared.FdSpeed:0.###} thrust={shared.EnginePower:0.###}");
            ctx.Note($"jitter spread: {first.Length():0.0} / {second.Length():0.0} m flown in 4 s, {spread:0.00} m apart");
        }
        finally
        {
            flying?.Free();
            pool?.Free();
            textures.Dispose();
        }
    }

    // A static world plate for the graze runs — layer 1 (the world), placed once at its
    // final pose because a body MOVED after creation is invisible to space queries this frame.
    internal static StaticBody3D Plate(string name, Vector3 size, Vector3 at)
    {
        var body = new StaticBody3D { Name = name };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        body.GlobalTransform = new Transform3D(Basis.Identity, at);
        return body;
    }

    // The neighbour-splash repro pair on controlled geometry: a torpedo detonates against a thin wall
    // beside one end of a long neighbour whose transform origin sits outside the blast radius while
    // its near face sits well inside it. Origin-scored falloff reads zero splash, so a real fix scores
    // measurable splash, and the struck wall's own direct-hit damage stays unscaled either way.
    internal static void BlastNeighborShape(TestContext ctx)
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
        float fullDamage = torpedo.HealthDamage!.Value;
        float radius = torpedo.ImpactProximity!.Value;
        var detonation = new Vector3(0f, 0f, -10f);

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        StaticBody3D? wall = null;
        StaticBody3D? neighbor = null;
        try
        {
            // The struck wall: a thin plate the round's raycast hits almost immediately.
            wall = new StaticBody3D { Name = "blast-test-wall" };
            wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(10f, 10f, 0.2f) } });
            wall.GlobalTransform = new Transform3D(Basis.Identity, detonation);
            ctx.Host.AddChild(wall);

            // The neighbour: a long body (a zeppelin gasbag/long building mesh, the shape the
            // original bug showed) whose CENTRE (transform origin) sits well past `radius`, while its near
            // face sits `nearFaceDistance` from the detonation — well inside `radius`.
            float nearFaceDistance = 2f;
            float halfLen = radius + 20f;
            neighbor = new StaticBody3D { Name = "blast-test-neighbor" };
            neighbor.AddChild(new CollisionShape3D
            { Shape = new BoxShape3D { Size = new Vector3(halfLen * 2f, 4f, 4f) } });
            neighbor.GlobalTransform = new Transform3D(Basis.Identity,
                detonation + new Vector3(nearFaceDistance + halfLen, 0f, 0f));
            ctx.Host.AddChild(neighbor);

            float originDistance = neighbor.GlobalPosition.DistanceTo(detonation);
            ctx.Check(originDistance > radius,
                $"repro precondition: the neighbour's transform origin sits outside the blast radius distance={originDistance:0.#} radius={radius:0.#}");

            var recorded = new List<(Node? Body, float Damage)>();
            pool = new ProjectilePool(textures, null, null)
            {
                DamageSink = (body, damage) => { recorded.Add((body, damage)); return true; },
            };
            ctx.Host.AddChild(pool);
            pool.Spawn(torpedo, new Transform3D(Basis.Identity, Vector3.Zero), Vector3.Zero);
            for (int i = 0; i < 120 && recorded.Count == 0; i++)
                pool.SimStep(1f / 60f);

            ctx.Check(recorded.Count > 0, $"the round reached and detonated on the wall");

            var wallHit = recorded.FirstOrDefault(r => r.Body == wall);
            ctx.Check(wallHit.Body == wall, $"the directly struck wall is in the damage report");
            if (wallHit.Body == wall)
            {
                ctx.Check(Mathf.IsEqualApprox(wallHit.Damage, fullDamage),
                    $"direct-hit damage stays full and unscaled by the blast falloff damage={wallHit.Damage:0.#} full={fullDamage:0.#}");
            }

            var neighborHit = recorded.FirstOrDefault(r => r.Body == neighbor);
            ctx.Check(neighborHit.Body == neighbor,
                $"a neighbour whose ORIGIN sits outside the blast radius still takes splash damage, scored to its nearest surface (BL-239) — origin-scoring would have read zero here");
            if (neighborHit.Body == neighbor)
            {
                float expected = ProjectilePool.BlastDamage(fullDamage, radius, nearFaceDistance);
                ctx.Check(neighborHit.Damage > 0f && Mathf.Abs(neighborHit.Damage - expected) < 1f,
                    $"neighbour damage matches the shape-scored falloff from its near face expected={expected:0.#} actual={neighborHit.Damage:0.#}");
            }
        }
        finally
        {
            pool?.Free();
            wall?.Free();
            neighbor?.Free();
            textures.Dispose();
        }
    }

    // The engine note's two non-throttle terms, against the shipped curves and against the figures
    // CAP-10 measured off the original BEFORE the expression was read (BL-109). Decode:
    // docs/formats/vehicle.md, "The engine slot's pitch and gain are not throttle alone".
    // ⚠ The measured numbers are a CHECK on the decode, never its source. If one disagrees, the
    // coefficients still stand: they are read from the image and a recording may not contest them.
    internal static void EngineNote(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var stats = PlaneStats.Load(ctx.ZrdrPath, "player_bhawk");

        // Everything below is quoted in PERCENT OF THE LEVEL BASELINE, so the shipped curve has to
        // be the one the decode assumes before any of it means anything.
        ctx.Check(Mathf.IsEqualApprox(stats.EnginePitch.MinY, 0.6f)
                  && Mathf.IsEqualApprox(stats.EnginePitch.MaxY, 1f),
            $"the shipped engine pitch curve spans {stats.EnginePitch.MinY:0.##}→{stats.EnginePitch.MaxY:0.##} (want 0.6→1)");
        ctx.Check(Mathf.IsEqualApprox(stats.EngineVolume.MinY, stats.EngineVolume.MaxY),
            $"…and its volume curve is FLAT at {stats.EngineVolume.MinY:0.##}, which is what makes the 0.26 term inert");

        float Pitch(EngineDrive d) => EngineAudioCurves.Engine(stats, d, 1f).Pitch;
        float Volume(EngineDrive d) => EngineAudioCurves.Engine(stats, d, 1f).Volume;
        var level = new EngineDrive(1f, 0f, 0f);
        float baseline = Pitch(level);
        ctx.Check(Mathf.IsEqualApprox(baseline, 1f),
            $"full throttle, wings level, no rotation reads the curve's own top: {baseline:0.0000}");

        // Term (b). A vertical dive puts the nose at −1 in Y, and the original's `a` is −nose.Y, so
        // it reaches +1 and subtracts 0.15 of the parameter: 0.6 + 0.4·0.85 = 0.94.
        float dive = Pitch(new EngineDrive(1f, 0f, 1f));
        ctx.Note($"vertical dive reads {dive:0.0000} ({(dive - 1f) * 100f:+0.0;-0.0} %), against CAP-10's measured 0.9370 (−6.3 %)");
        ctx.Check(Mathf.IsEqualApprox(dive, 0.94f),
            $"a sustained vertical dive drops the note to {dive:0.0000} (want 0.94)");
        ctx.Check(Mathf.Abs(dive - 0.9370f) < 0.01f,
            $"…which lands within a point of the figure CAP-10 measured off the original, 0.9370");

        // Term (a). ⚠ Taken from OPPOSITE body rates, not from one drive quoted twice: the property
        // CAP-10 needed three takes to establish is that the transient is UNSIGNED, up for push and
        // pull alike, and only a magnitude of the rate can do that.
        var pullModel = new FlightModel(stats) { Throttle = 1f, BodyRates = new Vector3(0.3f, 0f, 0f) };
        var pushModel = new FlightModel(stats) { Throttle = 1f, BodyRates = new Vector3(-0.3f, 0f, 0f) };
        float pull = Pitch(EngineAudioCurves.DriveFrom(pullModel));
        float push = Pitch(EngineAudioCurves.DriveFrom(pushModel));
        ctx.Check(pull > baseline && Mathf.IsEqualApprox(pull, push),
            $"±0.3 rad/s both RAISE the note, to {pull:0.0000} pulling and {push:0.0000} pushing");
        ctx.Check(Mathf.Abs((pull - 1f) - 0.030f) < 0.005f,
            $"…by {(pull - 1f) * 100f:0.0} %, against CAP-10's measured +3.0 % transient");

        // ⚠ Roll is the arm that separates this from a plain rate magnitude: the original drops the
        // nose-axis component, so a fast roll must not move the note at all.
        var rolling = new FlightModel(stats) { BodyRates = new Vector3(0f, 0f, 4f) };
        var rollDrive = EngineAudioCurves.DriveFrom(rolling);
        ctx.Check(Mathf.IsEqualApprox(rollDrive.TurnRate, 0f),
            $"a 4 rad/s ROLL reads turn rate {rollDrive.TurnRate:0.000} — the nose-axis component is dropped");
        var pitching = new FlightModel(stats) { BodyRates = new Vector3(0.3f, 0f, 4f) };
        ctx.Check(Mathf.IsEqualApprox(EngineAudioCurves.DriveFrom(pitching).TurnRate, 0.3f),
            $"…while the same roll with 0.3 rad/s of pitch on top reads exactly the pitch rate");

        // The sign trap, taken off a real attitude rather than asserted: our nose is −Z, so a climb
        // has to come out NEGATIVE to match the original's own reading of its row 2.
        var climbing = new FlightModel(stats);
        climbing.Reset(Vector3.Zero, new Basis(Vector3.Right, Mathf.Pi / 4f), 100f, 1f);
        float climbA = EngineAudioCurves.DriveFrom(climbing).ClimbAttitude;
        ctx.Check(climbA < -0.5f,
            $"a 45° climb reads a={climbA:0.000}, negative as the original's row-2 decode says");
        ctx.Check(Pitch(new EngineDrive(1f, 0f, climbA)) > baseline,
            $"…so a climb RAISES the note where the dive above lowered it");

        // Both terms move the parameter; only pitch has a span to show it.
        ctx.Check(Mathf.IsEqualApprox(Volume(level), Volume(new EngineDrive(1f, 0.3f, 1f))),
            $"neither term is audible on volume against the flat curve: {Volume(level):0.0000} either way");

        // Boost REPLACES the parameters. Nothing sets it today (no nitro system), so this is the
        // only thing that exercises the branch.
        float boosted = Pitch(new EngineDrive(0f, 0f, 0f, Boosting: true));
        ctx.Check(Mathf.IsEqualApprox(boosted, stats.EnginePitch.Remap(1.25f)),
            $"boost pins the pitch parameter at 1.25 regardless of a closed throttle: {boosted:0.0000}");

        // The clamp is what stops a spin from running the note away.
        float spun = Pitch(new EngineDrive(1f, 50f, 0f));
        ctx.Check(Mathf.IsEqualApprox(spun, stats.EnginePitch.Remap(1.5f)),
            $"an absurd 50 rad/s tumble clamps at the authored 1.5 parameter: {spun:0.0000}");
    }

    // The air-to-air hit chain on two real flight rigs driven by manual sim steps: body strike,
    // struck-shape to part mapping, armor-first damage, the decoded whole-vehicle kill rule, a
    // crashed plane's immunity, Downed attribution into a real VersusMatch, the VS respawn loop, and
    // the rocket proximity fuse and blast falloff. Full inventory: this module's architecture entry.
    // ⚠ The zero-self-hits negative case stays non-optional; without it a broken owner exclusion
    // arrives silently as "guns too strong".
    internal static void AirToAir(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        // The first gun carrying both damage magnitudes — data-driven, not a hardcoded id.
        WeaponDef? gun = weapons.All.FirstOrDefault(w =>
            w.IsGun && w.ArmorDamage is > 0f && w.HealthDamage is > 0f);
        ctx.Check(gun != null, $"a gun with ARMOR_DAMAGE and HEALTH_DAMAGE exists in the data");
        if (gun == null)
            return;
        float armorDmg = gun.ArmorDamage!.Value;
        float healthDmg = gun.HealthDamage!.Value;

        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var nose = stats.DestroyableParts.FirstOrDefault(p =>
            p.Name.Equals("nose", System.StringComparison.OrdinalIgnoreCase));
        ctx.Check(nose is { Critical: true }, $"{ctx.PlaneName} carries a critical nose part");
        if (nose == null)
            return;
        ctx.Check(nose.MaxArmor > 2f * armorDmg,
            $"precondition: nose armor {nose.MaxArmor:0} absorbs the two measured shots (2×{armorDmg:0.#})");

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? target = null;
        FlightController? shooter = null;
        FlightController? bystander = null;
        FlightController? fury = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            FlightController BuildRig(int playerIndex, Vector3 pos)
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
                // Nose on world -Z (identity attitude): plane-local == world - pos.
                rig.Setup(new FlightModel(stats), ctx.Camera, new CamParams(), pos, pos + Vector3.Forward);
                ctx.Host.AddChild(rig);
                return rig;
            }

            // Two rigs on a known bearing: the target on the origin column, the shooter well
            // abeam so its own test bursts cross nothing but its own airframe.
            var targetPos = new Vector3(0f, 500f, 0f);
            var shooterPos = new Vector3(500f, 500f, 0f);
            target = BuildRig(1, targetPos);
            shooter = BuildRig(0, shooterPos);
            ctx.Check(target.Body != null && shooter.Body != null,
                $"both rigs derived collider boxes and built an AircraftBody");
            if (target.Body == null || shooter.Body == null)
                return;
            ctx.Check(ProjectilePool.SurfaceIdOf(target.Body) == SurfaceRegistry.Player,
                $"an aircraft body reads as surface id {SurfaceRegistry.Player} (player), the IMPACT row 44 weapons author");

            // The kill-attribution seam, scored exactly the way GameSession does in --vs:
            // each rig's Downed report forwarded into a real (unlimited, untimed) VersusMatch —
            // a killer inside the roster is a kill, anything else a plain death.
            var match = new VersusMatch(2, killTarget: 0, timeLimit: 0f);
            void ScoreDowned(FlightController rig) => rig.Downed += (victim, killer) =>
            {
                if (killer is int k && k >= 0 && k < match.PlayerCount)
                    match.RegisterKill(k, victim);
                else
                    match.RegisterDeath(victim);
            };
            ScoreDowned(target);
            ScoreDowned(shooter);

            // The core claim, straight off the space state: a ray at the fuselage returns the
            // body, and the struck shape index maps back to a Parts entry.
            var space = live.GetWorld3D().DirectSpaceState;
            var probe = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                targetPos + new Vector3(0f, 0f, -30f), targetPos, CollisionLayers.WorldAndAircraft));
            bool probeHitBody = probe.Count > 0 && ReferenceEquals(probe["collider"].Obj, target.Body);
            ctx.Check(probeHitBody, $"a ray at the fuselage returns the aircraft body");
            if (probeHitBody)
            {
                string probePart = target.Body.PartName(probe["shape"].AsInt32());
                ctx.Check(probePart == "fuselage",
                    $"the struck shape maps to the expected Parts entry part={probePart}");
            }

            bool Pristine(FlightController rig) => rig.Damage!.Parts.Values.All(
                p => p.Hp >= p.Def.MaxHp && p.Armor >= p.Def.MaxArmor);
            float Combined(FlightController rig) => rig.Damage!.Parts.Values.Sum(p => p.Hp + p.Armor);

            // One shot per call from `muzzlePos` along world +Z / -Z per the basis, then enough
            // manual sim steps to land it; leftovers (misses fly a full RANGE) are cleared so no
            // phase leaks rounds into the next.
            void FireOne(Transform3D muzzle, int shooterId, int steps)
            {
                live.Spawn(gun, muzzle, Vector3.Zero, shooterId);
                for (int i = 0; i < steps; i++)
                    live.SimStep(1f / 60f);
                live.Clear();
            }

            // ⚠ Run the negative case first, while both airframes are pristine: a burst fired forward through
            // the shooter's own airframe from 14 m behind its tail, owned by that same pilot. Broken owner
            // exclusion turns several of these into self-hits; correct exclusion registers none.
            var selfMuzzle = new Transform3D(Basis.Identity, shooterPos + new Vector3(0f, 0f, 14f));
            for (int i = 0; i < 25; i++)
                live.Spawn(gun, selfMuzzle, Vector3.Zero, shooter.PlayerIndex);
            for (int i = 0; i < 60; i++)
                live.SimStep(1f / 60f);
            live.Clear();
            ctx.Check(Pristine(shooter) && !shooter.Crashed,
                $"a burst through the shooter's own geometry registers zero self-hits");
            ctx.Check(Pristine(target), $"the abeam burst touched nothing else");

            // The measured hits: single rounds from 10 m ahead of the target's nose on the centerline, fired
            // by the opposing identity. Each registering round spends exactly one ARMOR_DAMAGE from the nose
            // pool and moves no other part, which is MapStruckPart naming the right one.
            var noseMuzzle = new Transform3D(
                Basis.LookingAt(Vector3.Back, Vector3.Up), targetPos + new Vector3(0f, 0f, -10f));
            var noseState = target.Damage!.Parts["nose"];
            for (int shot = 1; shot <= 2; shot++)
            {
                // One round per attempt; a round leaves dead straight, so this registers on the first try and the
                // retry is only a defensive margin against a near-miss. A registering round must move the pool by
                // exactly one ARMOR_DAMAGE quantum, which is the assertion.
                float before = noseState.Armor;
                int tries = 0;
                while (noseState.Armor >= before && tries < 5)
                {
                    tries++;
                    FireOne(noseMuzzle, shooter.PlayerIndex, 10);
                }
                if (tries > 1)
                    ctx.Note($"shot {shot} needed {tries} rounds");
                ctx.Check(Mathf.IsEqualApprox(noseState.Armor, nose.MaxArmor - shot * armorDmg),
                    $"shot {shot}: nose armor moved by the weapon's ARMOR_DAMAGE armor={noseState.Armor:0.##} expected={nose.MaxArmor - shot * armorDmg:0.##}");
                ctx.Check(Mathf.IsEqualApprox(noseState.Hp, nose.MaxHp),
                    $"shot {shot}: health untouched while armor absorbs hp={noseState.Hp:0.##}");
            }
            ctx.Check(target.Damage.Parts.Values.All(
                    p => p.Def == nose || (p.Hp >= p.Def.MaxHp && p.Armor >= p.Def.MaxArmor)),
                $"no part but the nose moved");

            // The kill under the decoded rule: whole-vehicle health exhausted, not one dead critical part.
            // Each zone is fired on its own bearing because a dead part keeps its collider and shields the far
            // side; per-zone budgets are derived from that zone's own pools, tripled for misses.
            int fired = 0;
            void ExhaustPart(string partName, Transform3D muzzle)
            {
                var st = target!.Damage!.Parts[partName];
                int partBudget = (int)(st.Def.MaxArmor / armorDmg + st.Def.MaxHp / healthDmg) * 3 + 20;
                int spent = 0;
                while (st.Hp > 0f && spent < partBudget)
                {
                    spent++;
                    FireOne(muzzle, shooter!.PlayerIndex, 8);
                }
                fired += spent;
                ctx.Check(st.Hp <= 0f, $"sustained fire empties the {partName} pools rounds={spent}/{partBudget}");
            }

            // A bearing straight through a named collider box's own centre (identity attitude:
            // plane-local == world − pos), from 30 m outside it along the firing axis — so the
            // wing rounds cross the wing at its real height/chord rather than the fuselage line.
            Transform3D BoxMuzzle(string boxName, bool leftSide, Vector3 fireDir)
            {
                Vector3 centre = Vector3.Zero;
                float best = -1f;
                foreach (var p in target!.Collider!.Parts)
                {
                    if (p.Name != boxName || (boxName == "wing" && (p.Local.Origin.X < 0f) != leftSide))
                        continue;
                    float span = Mathf.Abs(p.Local.Origin.X);
                    if (span > best)
                    {
                        best = span;
                        centre = targetPos + p.Local.Origin;
                    }
                }
                return new Transform3D(Basis.LookingAt(fireDir, Vector3.Up), centre - fireDir * 30f);
            }

            ExhaustPart("nose", noseMuzzle);
            // The retirement's own pin: the nose is a `critical` part and it is DEAD — under the
            // old any-critical-part rule this plane would be down. The decoded rule keeps it
            // flying until the whole vehicle's health is gone.
            ctx.Check(!target.Crashed,
                $"a lone dead critical part no longer downs the plane (the retired divergence)");
            ExhaustPart("tail", BoxMuzzle("tail", leftSide: false, Vector3.Forward));
            ExhaustPart("leftwing", BoxMuzzle("wing", leftSide: true, Vector3.Right));
            ctx.Check(!target.Crashed, $"three of four zones dead still flies");
            ExhaustPart("rightwing", BoxMuzzle("wing", leftSide: false, Vector3.Left));
            ctx.Check(target.Crashed,
                $"exhausting the last zone's health downs the plane (whole-vehicle health ≤ 0) rounds={fired}");
            ctx.Check(noseState.Hp <= 0f, $"the nose health pool is empty hp={noseState.Hp:0.##}");
            ctx.Note($"kill took {fired} rounds of {gun.Id} across all four zones (nose armor {nose.MaxArmor:0}/{armorDmg:0.#}, hp {nose.MaxHp:0}/{healthDmg:0.#})");
            ctx.Check(match.KillsOf(0) == 1 && match.DeathsOf(1) == 1,
                $"the weapon kill scored the shooter through the real Downed path kills(P1)={match.KillsOf(0)} deaths(P2)={match.DeathsOf(1)}");
            ctx.Check(match.KillsOf(1) == 0 && match.DeathsOf(0) == 0,
                $"nobody else's tally moved kills(P2)={match.KillsOf(1)} deaths(P1)={match.DeathsOf(0)}");

            // --- a crashed plane is out of the fight: its body is unhittable and further rounds
            // change nothing.
            float afterCrash = Combined(target);
            FireOne(noseMuzzle, shooter.PlayerIndex, 10);
            ctx.Check(Mathf.IsEqualApprox(Combined(target), afterCrash),
                $"a crashed plane soaks no further rounds");
            ctx.Check(match.KillsOf(0) == 1 && match.DeathsOf(1) == 1,
                $"rounds into a wreck report no second death kills(P1)={match.KillsOf(0)}");

            // A crash with no round behind it — the terrain/mid-air shape — is a death with a
            // null killer: a tally for the victim, a kill for nobody.
            target.Respawn();
            target.DebugForceCrash();
            ctx.Check(match.DeathsOf(1) == 2 && match.KillsOf(0) == 1 && match.KillsOf(1) == 0,
                $"a killer-less crash registers a death and no kill anywhere deaths(P2)={match.DeathsOf(1)}");

            // An unowned round that downs the plane is likewise a death with no killer. ⚠ Pre-empty the other
            // three zones with exact spends so no leftover reaches the whole pool; an overkill spend would
            // down the plane through the overflow before the burst.
            target.Respawn();
            void ExhaustAllButNose()
            {
                foreach (var p in target!.Damage!.Parts.Values)
                {
                    if (p.Def != nose)
                    {
                        target.Damage.Apply(p.Def.Name, 0f, p.Armor);
                        target.Damage.Apply(p.Def.Name, p.Hp, 0f);
                    }
                }
            }

            ExhaustAllButNose();
            int noseBudget = (int)(nose.MaxArmor / armorDmg + nose.MaxHp / healthDmg) * 3 + 20;
            fired = 0;
            while (!target.Crashed && fired < noseBudget)
            {
                fired++;
                FireOne(noseMuzzle, ProjectilePool.NoShooter, 8);
            }
            ctx.Check(target.Crashed, $"the unowned burst downed the plane rounds={fired}/{noseBudget}");
            ctx.Check(match.DeathsOf(1) == 3 && match.KillsOf(0) == 1 && match.KillsOf(1) == 0,
                $"an unowned round's kill is a death with no killer deaths(P2)={match.DeathsOf(1)} kills={match.KillsOf(0)}/{match.KillsOf(1)}");

            // The VS respawn loop, in sim frames. Default (AutoRespawnAfter null): a crash
            // waits for R — 4 s of crash-cam sim steps respawn nothing.
            for (int i = 0; i < 240; i++)
                target.SimStep(1f / 60f);
            ctx.Check(target.Crashed, $"without AutoRespawnAfter a crash waits for R (still down after 4 s)");

            // Armed at 3 s, what the session sets per rig in --vs: the timer runs from Crash in sim frames, so
            // the aircraft is still down just short of the mark and flying again within a frame or two of it,
            // and the respawn itself reports no death.
            target.Respawn();
            target.AutoRespawnAfter = 3f;
            target.DebugForceCrash();
            int deathsAtCrash = match.DeathsOf(1);
            for (int i = 0; i < 175; i++)
                target.SimStep(1f / 60f);
            ctx.Check(target.Crashed, $"just short of the 3 s mark the plane is still on the crash cam");
            int extra = 0;
            while (target.Crashed && extra < 10)
            {
                extra++;
                target.SimStep(1f / 60f);
            }
            ctx.Check(!target.Crashed,
                $"the armed crash auto-respawns at the 3 s mark (step {175 + extra} of 180±5)");
            ctx.Check(match.DeathsOf(1) == deathsAtCrash && match.KillsOf(0) == 1,
                $"respawn emitted nothing — the death was reported at Crash deaths(P2)={match.DeathsOf(1)}");

            // Data-driven pick for the proximity fuse: a dumbfire, spread-free rocket whose blast radius
            // exceeds both its fuse distance and the suite's fixed pass gap. Equal ARMOR/HEALTH magnitudes
            // make the combined delta equal the scaled magnitude regardless of armor left (the carry-over rule).
            WeaponDef? rocket = weapons.All.FirstOrDefault(w =>
                w.IsRocket && w.DetonationDotProduct is null && w.CannonSpread is not > 0f
                && w.ArmorDamage is > 0f && w.HealthDamage is > 0f
                && w.DetonationDistance is > 8f
                && w.ImpactProximity is { } prox && prox >= 2f * w.DetonationDistance!.Value);
            ctx.Check(rocket != null,
                $"a fused rocket with a blast radius beyond its trigger distance exists in the data");
            if (rocket == null)
                return;
            float fuseRange = rocket.DetonationDistance!.Value;
            float blastRadius = rocket.ImpactProximity!.Value;
            float rocketDmg = rocket.ArmorDamage!.Value;
            ctx.Check(Mathf.IsEqualApprox(rocketDmg, rocket.HealthDamage!.Value),
                $"precondition: the rocket's two damage magnitudes are equal ({rocket.Id})");
            ctx.Note($"rocket phases use {rocket.Id} (fuse {fuseRange:0} m, blast {blastRadius:0} m, dmg {rocketDmg:0})");

            // The crossing line: level with the target nose's own nearest hull point, a fixed gap
            // ahead of its front face — the nearest box to any point on it is that front face, so
            // the detonation distance IS the gap and the struck part maps forward (the nose).
            const float FuseGap = 5f;
            ctx.Check(FuseGap < fuseRange && blastRadius >= 60f,
                $"precondition: the pass gap sits inside the fuse range and the radius leaves falloff room");
            target.Respawn();
            target.Body.NearestShape(targetPos + new Vector3(0f, 0f, -60f), out _, out var noseTip);
            var passPoint = new Vector3(noseTip.X, noseTip.Y, noseTip.Z - FuseGap);
            var crossMuzzle = new Transform3D(
                Basis.LookingAt(Vector3.Right, Vector3.Up), passPoint + new Vector3(-150f, 0f, 0f));

            // A third airframe straight below the pass: inside the blast radius but farther from
            // the detonation than the fused-on target — the nearer > farther falloff witness.
            bystander = BuildRig(2, passPoint + new Vector3(0f, -0.4f * blastRadius, 0f));

            void FireRocket(Transform3D muzzle, int shooterId, int steps = 30)
            {
                live.Spawn(rocket, muzzle, Vector3.Zero, shooterId);
                for (int i = 0; i < steps; i++)
                    live.SimStep(1f / 60f);
                live.Clear();
            }

            // --- the fused pass: one rocket across the nose gap. The fuse must hold while the
            // round is still closing and pop at the closest approach, blasting the nose by the
            // weapon's own magnitudes under the linear falloff at exactly the gap distance.
            float beforeNear = Combined(target);
            float beforeFar = Combined(bystander);
            FireRocket(crossMuzzle, shooter.PlayerIndex);
            float movedNear = beforeNear - Combined(target);
            float expectedBlast = ProjectilePool.BlastDamage(rocketDmg, blastRadius, FuseGap);
            ctx.Check(Mathf.Abs(movedNear - expectedBlast) < 1f,
                $"the fused pass blasts by the falloff at the {FuseGap:0} m gap moved={movedNear:0.##} expected={expectedBlast:0.##}");
            ctx.Check(target.Damage.Parts.Values.All(
                    p => p.Def == nose || (p.Hp >= p.Def.MaxHp && p.Armor >= p.Def.MaxArmor)),
                $"the blast lands on the nearest box only — it maps to the nose");
            float movedFar = beforeFar - Combined(bystander);
            ctx.Check(movedFar > 0f && movedFar < movedNear,
                $"a farther plane inside the radius takes less nearer={movedNear:0.##} farther={movedFar:0.##}");
            ctx.Check(Pristine(shooter), $"the shooter's own plane took nothing from its own blast");

            // --- the same pass fired by nobody: an unowned round excludes no plane, so the
            // shooter's airframe — 500 m out, far beyond IMPACT_PROXIMITY — is a legal blast
            // candidate and still records nothing: zero outside the radius.
            target.Respawn();
            bystander.Respawn();
            FireRocket(crossMuzzle, ProjectilePool.NoShooter);
            ctx.Check(Combined(target) < beforeNear,
                $"an unowned rocket fuses like any other moved={beforeNear - Combined(target):0.##}");
            ctx.Check(Pristine(shooter), $"zero blast outside the radius (the shooter's plane, 500 m out)");

            // The attributed blast kill: fused passes across the critical nose until it zeroes, with the Downed
            // report carrying the shooter through the same seam the gun kill used. Counters entering this
            // phase: kills(P1)=1, deaths(P2)=4.
            target.Respawn();
            ExhaustAllButNose(); // attribution scaffolding again: the blast lands on the nose only
            int rockets = 0;
            int rocketBudget = (int)((nose.MaxArmor + nose.MaxHp) / expectedBlast) + 6;
            while (!target.Crashed && rockets < rocketBudget)
            {
                rockets++;
                FireRocket(crossMuzzle, shooter.PlayerIndex);
            }
            ctx.Check(target.Crashed,
                $"sustained fused passes exhaust the last zone rockets={rockets}/{rocketBudget}");
            ctx.Check(match.KillsOf(0) == 2 && match.DeathsOf(1) == 5,
                $"the blast kill scored the shooter through Downed kills(P1)={match.KillsOf(0)} deaths(P2)={match.DeathsOf(1)}");

            // --- a wreck is out of the fight for rockets too: it neither fuses a round nor
            // soaks its blast, and no second death is reported.
            float wreck = Combined(target);
            FireRocket(crossMuzzle, shooter.PlayerIndex);
            ctx.Check(Mathf.IsEqualApprox(Combined(target), wreck) && match.DeathsOf(1) == 5,
                $"a wreck neither fuses a rocket nor soaks its blast");

            // --- the launch trap: a rocket spawns INSIDE its shooter's own collision boxes.
            // Owner exclusion must keep it from fusing on or blasting its own plane at launch —
            // and the round must fly on and still fuse on the opponent downrange.
            target.Respawn();
            var aim = (passPoint - shooterPos).Normalized();
            var ownMuzzle = new Transform3D(Basis.LookingAt(aim, Vector3.Up), shooterPos);
            float tBefore = Combined(target);
            FireRocket(ownMuzzle, shooter.PlayerIndex, steps: 60);
            ctx.Check(Pristine(shooter) && !shooter.Crashed,
                $"a rocket fired from inside its own airframe never self-fuses or self-damages");
            ctx.Check(Combined(target) < tBefore,
                $"…and the same round flew on to fuse on the opponent moved={tBefore - Combined(target):0.##}");

            // The D14 correction (docs/org/vehicleDamage.md): concentrated fire on ONE bearing kills. Once the
            // nose dies the resolver redirects its hits to surviving zones and every unabsorbed leftover drains
            // the whole-vehicle pool, so a plane immortal to one-zone fire is the regression.
            target.Respawn();
            int oneZoneBudget = (int)(stats.DestroyableParts.Sum(p => p.MaxArmor + p.MaxHp)
                / Mathf.Min(armorDmg, healthDmg)) * 3 + 40;
            int oneZone = 0;
            while (!target.Crashed && oneZone < oneZoneBudget)
            {
                oneZone++;
                FireOne(noseMuzzle, shooter.PlayerIndex, 8);
            }

            ctx.Check(target.Crashed,
                $"concentrated fire on the nose bearing alone downs the plane rounds={oneZone}/{oneZoneBudget}");
            ctx.Note($"one-bearing kill took {oneZone} rounds of {gun.Id} (redirect + whole-pool overflow)");

            // --- the user-reported sponge, decode-confirmed fix: a Fury dies to a few HE
            // rockets (wep_06 BOOM, 40 armor / 60 health), fired head-on from one bearing.
            // Each detonation reaches the plane through the blast pass at its nearest box.
            var he = weapons.Get("wep_06");
            ctx.Check(he is { ArmorDamage: 40f, HealthDamage: 60f },
                $"wep_06 carries the authored 40/60 damage pair");
            if (he == null)
                return;
            var furyStats = PlaneStats.Load(ctx.ZrdrPath, "player_fury");
            var furyPos = new Vector3(-900f, 500f, 0f);
            var furyModel = new PlaneBuilder(planesGamez, textures).Build("player_fury");
            fury = new FlightController
            {
                PlaneModel = furyModel,
                Collider = PlaneCollider.Build(furyModel),
                Damage = PlaneDamage.For(furyStats),
                PlayerIndex = 3,
                Projectiles = live,
                UseKeyboard = false,
                AllowPause = false,
            };
            fury.AddChild(furyModel);
            fury.Setup(new FlightModel(furyStats), ctx.Camera, new CamParams(), furyPos,
                furyPos + Vector3.Forward);
            ctx.Host.AddChild(fury);
            ctx.Check(Mathf.IsEqualApprox(fury.Damage!.WholeHealthMax, 90f),
                $"the Fury's whole pool seeds as the sum over parts (25+25+20+20) max={fury.Damage.WholeHealthMax:0.#}");

            var heMuzzle = new Transform3D(
                Basis.LookingAt(Vector3.Back, Vector3.Up), furyPos + new Vector3(0f, 0f, -120f));
            int heRockets = 0;
            while (!fury.Crashed && heRockets < 12)
            {
                heRockets++;
                live.Spawn(he, heMuzzle, Vector3.Zero, shooter.PlayerIndex);
                for (int i = 0; i < 40; i++)
                    live.SimStep(1f / 60f);
                live.Clear();
            }

            ctx.Check(fury.Crashed && heRockets <= 8,
                $"a few HE rockets down a Fury rockets={heRockets} (the reported 9-rocket sponge is the regression)");
            ctx.Check(heRockets >= 2, $"…but not a single one rockets={heRockets}");
            ctx.Note($"the Fury fell to {heRockets} head-on wep_06 rockets on one bearing");
        }
        finally
        {
            pool?.Free();
            target?.Free();
            shooter?.Free();
            bystander?.Free();
            fury?.Free();
            textures.Dispose();
        }
    }

    // The team model: two distinct pilot indices can share one explicit FlightController.Team, which
    // the old AimAssist.TeamOfPilot stand-in made impossible by deriving a team from the pilot index.
    // Proves the plumbing end to end (ProjectilePool.CollectAircraft into AimAssist.Scan): a shooter's
    // scan snaps onto a same-index-range aircraft on a different team and never onto one sharing its
    // own. A round that reaches a teammate still costs it HP; there is no damage gate, only targeting.
    internal static void TeamModel(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        WeaponDef? gun = weapons.All.FirstOrDefault(w => w.IsGun && w.ArmorDamage is > 0f);
        ctx.Check(gun != null, $"a gun with ARMOR_DAMAGE exists in the data");
        if (gun == null)
            return;

        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? human = null;
        FlightController? wingman = null;
        FlightController? enemy = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            FlightController BuildRig(int playerIndex, Vector3 pos, int? team)
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
                if (team is { } t)
                    rig.Team = t;
                rig.AddChild(model);
                rig.Setup(new FlightModel(stats), ctx.Camera, new CamParams(), pos, pos + Vector3.Forward);
                ctx.Host.AddChild(rig);
                live.RegisterAircraft(rig.Body!);
                return rig;
            }

            // Two DIFFERENT pilot indices (a human's and an AI's, well outside each other's range)
            // pinned to the SAME explicit team; a third index lands on a different one. Under the
            // retired stand-in every one of the three would have been its own team.
            var humanPos = new Vector3(0f, 500f, 0f);
            var wingPos = humanPos + new Vector3(200f, 0f, 0f);
            var enemyPos = humanPos + new Vector3(0f, 0f, -300f);
            human = BuildRig(0, humanPos, team: null); // the untouched default: TeamOfPilot(0)
            wingman = BuildRig(FlightRoster.ShooterIdBase, wingPos, team: AimAssist.PlayerTeam);
            enemy = BuildRig(FlightRoster.ShooterIdBase + 1, enemyPos, team: AimAssist.PlayerTeam + 1);
            ctx.Check(human.Team == wingman.Team && human.Team != enemy.Team,
                $"two distinct pilot indices share one explicit team, a third sits on another: human={human.Team} wingman={wingman.Team} enemy={enemy.Team}");

            // --- targeting: the plumbed scan (CollectAircraft -> AimAssist.Scan) reads Team, not
            // PlayerIndex — it snaps onto the team-2 enemy and refuses the team-1 wingman.
            var candidates = new AimCandidateSet();
            live.CollectAircraft(candidates);
            var scan = new AimScan
            {
                MuzzlePosition = humanPos,
                Forward = Vector3.Forward,
                Team = human.Team,
                Speed = 500f,
                RangeSquared = 1000f * 1000f,
                ConeCos = -1f, // whole forward hemisphere: only the team gate decides this scan
                Self = human,
            };
            bool found = AimAssist.Scan(scan, candidates, out var result);
            ctx.Check(found && ReferenceEquals(result.Source, enemy),
                $"the scan snaps onto the team-2 enemy and never the team-1 wingman found={found}");

            // --- A2's corroboration, fired for real: a team-1 round that reaches the team-1
            // wingman still costs it HP, because Decision 3/A2 gates targeting only, never damage.
            bool Pristine(FlightController rig) => rig.Damage!.Parts.Values.All(
                p => p.Hp >= p.Def.MaxHp && p.Armor >= p.Def.MaxArmor);
            var muzzle = new Transform3D(
                Basis.LookingAt((wingPos - humanPos).Normalized(), Vector3.Up), humanPos);
            for (int tries = 0; tries < 5 && Pristine(wingman); tries++)
            {
                live.Spawn(gun, muzzle, Vector3.Zero, human.PlayerIndex);
                for (int i = 0; i < 30; i++)
                    live.SimStep(1f / 60f);
                live.Clear();
            }
            ctx.Check(!Pristine(wingman),
                $"a team-1 round that reaches a team-1 wingman still costs it HP — no damage gate");
        }
        finally
        {
            pool?.Free();
            human?.Free();
            wingman?.Free();
            enemy?.Free();
            textures.Dispose();
        }
    }


}
