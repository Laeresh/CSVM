using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>Whether a zeppelin ring holds fire on a bearing its own hull blocks while that hull
/// flies its net, which a parked hull cannot show. Each ring's obstruction is found once by a
/// gimbal sweep and then held in the HULL's own frame, so the target keeps the same station on
/// the far flank however the hull translates and turns: the hull's motion is then the only
/// difference between the parked control leg and the flying leg, and any shot on the flying leg
/// is the motion's doing. Successive rings are probed at successive points of the net, so the
/// verdict is not one pose's.</summary>
internal static class TurretMovingHullSuites
{
    private const float Dt = 1f / 60f;

    // Several of the line-of-sight cache's random 1-2 s windows, so a verdict the cache merely
    // held over cannot pass as "held fire".
    private const float LegSeconds = 6f;

    // Rings probed. Each costs two legs of LegSeconds with a subtree transform push per step;
    // six spans both flanks, the belly and a cannon.
    private const int MaxRings = 6;

    // Where the held plane stands off the ring. Well clear of the hull's own 111 x 145 x 571 m
    // body, and inside every emplacement's DETECTION_RANGE.
    private const float StandoffM = 400f;

    // A shot across the BODY of the hull rather than past one of its ends or through an empty
    // corner of its box: the segment passes through at least this much of it. The hull's own box
    // is 111 x 145 x 571 m. ⚠ Do not lower this to admit more rings. A shallower crossing is met
    // by a neighbouring ring's own body a few metres off, which the pose lag below slips past, and
    // the probe then reports the lag as a hull leak.
    private const float DeepChordM = 120f;

    [Suite("turret-moving-hull-blocks-own-fire",
        "a zeppelin ring holds fire on a hostile plane its own MOVING hull stands between " +
        "(BL-714): each ring on C1/M04's piratezep is gimbal-swept for a bearing its own hull " +
        "blocks, the plane is held at that station in the hull's own frame, and the ring is " +
        "probed over several line-of-sight cache windows with the hull docked and again with it " +
        "flying its net, reading the collider's pose against the drawn hull's each step")]
    internal static void TurretMovingHullBlocksOwnFire(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, "C1");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C1", "M04");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(chapterZrdr, $"C1 zrdr");
        ctx.RequireData(missionZrdr, $"C1/M04 zrdr");
        ctx.RequireData(texturesPath, $"C1 textures");

        var zepDefs = Zeppelins.Load(missionZrdr);
        ctx.Check(zepDefs.Count == 1 && zepDefs[0].Node == "piratezep",
            $"C1/M04 authors one zeppelin, piratezep count={zepDefs.Count}");
        if (zepDefs.Count != 1)
        {
            return;
        }
        var nets = AiNets.Load(chapterZrdr);
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var turretDefs = TurretDefs.Load(ctx.ZrdrPath);
        var report = new StringBuilder();

        // ⚠ Without a clock of its own a suite reads GameClock.Current?.Time as a constant, and
        // the gunner's 1-2 s line-of-sight cache never expires: the whole leg then rides ONE cast.
        var savedClock = Utils.GameClock.Current;
        var clock = new Utils.GameClock { Mode = Utils.GameClock.RunMode.FixedStep };
        Utils.GameClock.Current = clock;

        ctx.WithWorld("C1", collision: true, mission: "M04", world =>
        {
            var textures = new TextureArchive(texturesPath);
            ProjectilePool? pool = null;
            FlightController? plane = null;
            TurretEmplacementRuntime? emplacements = null;
            ZeppelinRuntime? zeps = null;
            try
            {
                var live = new ProjectilePool(textures, null, null) { DamageSink = world.Runtime.DamageAt };
                pool = live;
                ctx.Host.AddChild(live);
                var runtime = emplacements = new TurretEmplacementRuntime(turretDefs, weapons,
                    (pattern, scope) => world.Runtime.FindNodes(pattern, scope), live,
                    world.Runtime.WorldRoot);

                var hulls = world.Runtime.FindNodes("piratezep");
                ctx.Check(hulls.Count == 1, $"C1/M04's world carries the piratezep hull");
                if (hulls.Count != 1)
                {
                    return;
                }
                var hull = hulls[0];
                world.Runtime.SetTargetActive(hull, true);

                var rings = runtime.Emplacements
                    .Where(t => t.Site is { } s && (s == hull || hull.IsAncestorOf(s))).ToList();
                ctx.Check(rings.Count > 0, $"piratezep carries emplacements count={rings.Count}");
                if (rings.Count == 0)
                {
                    return;
                }
                // The record ships piratezep allied (TEAM 1); forced hostile here only so a real
                // plane is something for AcquireTarget to find, as on the parked probe.
                runtime.SetTeamUnder(hull, InstantActionRuntime.EnemyTeam);

                var rig = BuildRig(ctx, planesGamez, textures, live, hull);
                plane = rig;

                var hullBox = MeshBoxOf(hull);
                report.AppendLine(Fmt($"piratezep mesh box (hull frame) pos={hullBox.Position} size={hullBox.Size}"));

                var space = ctx.Host.GetWorld3D().DirectSpaceState;
                FieldOfFire(ctx, space, rings, hull, hullBox, report);
                var blocked = SweepStations(ctx, space, rings, hull, hullBox, rig, report, across: true);
                var open = SweepStations(ctx, space, rings, hull, hullBox, rig, report, across: false);
                ctx.Check(blocked.Count > 0,
                    $"the gimbal sweep finds an in-arc station across its own hull's real geometry for at least one ring ({blocked.Count} of {rings.Count})");
                ctx.Check(open.Count > 0,
                    $"…and an in-arc station clear of it for at least one ring ({open.Count} of {rings.Count})");
                if (blocked.Count == 0 || open.Count == 0)
                {
                    return;
                }

                zeps = new ZeppelinRuntime(zepDefs, name =>
                    name.Equals("piratezep", System.StringComparison.OrdinalIgnoreCase) ? hull : null, nets);
                ctx.Same(1, zeps.LiveCount, $"the piratezep record is live on its net");

                // The guard the fix must not break: at a station its own hull does NOT stand in,
                // every ring still engages. A line-of-sight rule that silences the gun is the
                // failure the mount is kept out of the cast to prevent.
                int silent = 0;
                foreach (var station in open)
                {
                    if (RunLeg(ctx, zeps, live, rig, hull, station, report, flying: false) == 0)
                    {
                        silent++;
                    }
                }
                ctx.Same(0, silent,
                    $"every ring still engages a target its own hull is clear of ({open.Count - silent} of {open.Count} fired)");

                // Docked first, with the hull still on its armed spawn stop point, so the flying
                // leg's verdict is about the motion and nothing else.
                int dockedShots = 0;
                foreach (var station in blocked)
                {
                    dockedShots += RunLeg(ctx, zeps, live, rig, hull, station, report, flying: false);
                }
                ctx.Same(0, dockedShots,
                    $"docked: every probed ring holds fire across its own hull");

                // COMPLETED_STOPPOINT ["PirateZep1", 1, 0] is the mission's own release: from here
                // the hull translates and turns under its rings every step.
                ctx.Same(1, zeps.SetStopPoint(zepDefs[0].Net, 1, false),
                    $"the mission's own stop-point release puts the hull on its net");
                var flightStart = hull.GlobalPosition;
                int flyingShots = 0;
                foreach (var station in blocked)
                {
                    flyingShots += RunLeg(ctx, zeps, live, rig, hull, station, report, flying: true);
                }
                float flown = flightStart.DistanceTo(hull.GlobalPosition);
                ctx.Check(flown > 50f,
                    $"the hull really flew its net across the probed legs dist={flown.ToString("0", CultureInfo.InvariantCulture)} m");
                ctx.Same(0, flyingShots,
                    $"flying: every probed ring holds fire across its own MOVING hull");

                ctx.WriteArtifact("test-turret-moving-hull.txt", report.ToString());
                ctx.Note($"{blocked.Count} ring(s) probed across the hull docked and flying over {flown:0} m of net, {open.Count} clear of it; across shots={dockedShots}/{flyingShots} clear silent={silent}");
            }
            finally
            {
                Utils.GameClock.Current = savedClock;
                zeps?.Free();
                plane?.Free();
                emplacements?.Free();
                pool?.Free();
                textures.Dispose();
            }
        });
    }

    // A suite gets no physics flush, so a body an animation moved is still queried at its old pose
    // (docs/verification.md INSTR-24). Pushed once per step BEFORE the step that moves the hull,
    // which is the live ordering: a session's ray reads the pose written on the previous tick.
    private static void SyncColliders(Node3D root)
    {
        root.ForceUpdateTransform();
        foreach (var child in root.GetChildren())
        {
            if (child is Node3D node)
            {
                SyncColliders(node);
            }
        }
    }

    private static string Fmt(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    // The hull's own drawn extent, in the hull's frame: every mesh instance's AABB through its
    // transform relative to the hull. The station test's ground truth, independent of the physics
    // geometry the mechanism under test queries.
    private static Aabb MeshBoxOf(Node3D hull)
    {
        var inverse = hull.GlobalTransform.AffineInverse();
        Aabb? box = null;
        void Walk(Node node)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                var local = (inverse * mi.GlobalTransform) * mi.GetAabb();
                box = box.HasValue ? box.Value.Merge(local) : local;
            }
            foreach (var child in node.GetChildren())
            {
                Walk(child);
            }
        }
        Walk(hull);
        return box ?? new Aabb();
    }

    // How much of the hull's own drawn body the segment passes through, metres, both in the hull's
    // frame: the slab test kept as a length rather than a yes/no, so a station can be chosen for
    // the DEEPEST crossing (the reported far-flank shot) instead of the first grazed panel edge.
    private static float BoxChord(Aabb box, Vector3 from, Vector3 to)
    {
        var d = to - from;
        float lo = 0f;
        float hi = 1f;
        for (int axis = 0; axis < 3; axis++)
        {
            float o = from[axis];
            float dir = d[axis];
            float min = box.Position[axis];
            float max = box.Position[axis] + box.Size[axis];
            if (Mathf.Abs(dir) < 1e-6f)
            {
                if (o < min || o > max)
                {
                    return 0f;
                }
                continue;
            }
            float t0 = (min - o) / dir;
            float t1 = (max - o) / dir;
            if (t0 > t1)
            {
                (t0, t1) = (t1, t0);
            }
            lo = Mathf.Max(lo, t0);
            hi = Mathf.Min(hi, t1);
            if (lo > hi)
            {
                return 0f;
            }
        }
        return (hi - lo) * d.Length();
    }

    private static FlightController BuildRig(TestContext ctx, GameZ planesGamez,
        TextureArchive textures, ProjectilePool live, Node3D hull)
    {
        var st = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var model = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
        var rig = new FlightController
        {
            PlaneModel = model,
            Collider = PlaneCollider.Build(model),
            Damage = new PlaneDamage(st.DestroyableParts),
            PlayerIndex = 0,
            Projectiles = live,
            UseKeyboard = false,
            AllowPause = false,
        };
        rig.AddChild(model);
        rig.Setup(new FlightModel(st), ctx.Camera, new CamParams(), hull.GlobalPosition,
            hull.GlobalPosition + Vector3.Forward);
        ctx.Host.AddChild(rig);
        return rig;
    }

    // Per ring, one station in the HULL's frame, so the same relative geometry survives every pose
    // the hull takes on its net. across: the in-arc bearing whose segment passes through the most
    // of its own hull AND meets real hull collider there, which is the reported far-flank shot.
    // Otherwise: the in-arc bearing crossing the least of the hull on which the ring engages
    // TODAY. Swept before any SimStep moves a PitchNode off the rest pose BaseBasis reads.
    private static List<Station> SweepStations(TestContext ctx, PhysicsDirectSpaceState3D space,
        List<TurretController> rings, Node3D hull, Aabb hullBox, FlightController rig,
        StringBuilder report, bool across)
    {
        var toHull = hull.GlobalTransform.AffineInverse();
        var found = new List<Station>();
        foreach (var ring in rings)
        {
            if (across && found.Count >= MaxRings)
            {
                break;
            }
            var ringPos = ring.WorldPosition;
            var anchor = ring.YawNode ?? ring.PitchNode;
            var parentBasis = (anchor.GetParent() as Node3D)?.GlobalBasis ?? Basis.Identity;
            var baseBasis = (parentBasis * anchor.Transform.Basis).Orthonormalized();
            float yawMin = ring.Def.YawRestricted ? ring.Def.YawMinDeg!.Value : -180f;
            float yawMax = ring.Def.YawRestricted ? ring.Def.YawMaxDeg!.Value : 180f;
            float pitchMin = ring.Def.PitchRestricted ? ring.Def.PitchMinDeg!.Value : -80f;
            float pitchMax = ring.Def.PitchRestricted ? ring.Def.PitchMaxDeg!.Value : 80f;
            float standoff = Mathf.Min(ring.Def.DetectionRange * 0.6f, StandoffM);
            var localRing = toHull * ringPos;
            Vector3 best = default;
            float bestChord = across ? 0f : float.MaxValue;
            string what = "none";
            for (float yaw = yawMin; yaw <= yawMax; yaw += 5f)
            {
                for (float pitch = pitchMin; pitch <= pitchMax; pitch += 5f)
                {
                    var target = ringPos + (baseBasis * TurretController.LocalDir(yaw, pitch)) * standoff;
                    var localTarget = toHull * target;
                    if (hullBox.HasPoint(localTarget))
                    {
                        continue;   // a station inside the hull is not a shot across or clear of it
                    }
                    float chord = BoxChord(hullBox, localRing, localTarget);
                    if (across ? chord <= bestChord : chord >= bestChord)
                    {
                        continue;
                    }
                    // The AABB overstates a hull's ends, so an "across" station is only one where
                    // the hull's own colliders really do stand in the way; a "clear" one is only
                    // one the ring engages on today, with its mounting section left out.
                    string hit = HitName(space, ringPos, target + Vector3.Up * 0.2f,
                        across ? null : ring.PlatformColliderRids());
                    if (across == (hit == "none"))
                    {
                        continue;
                    }
                    bestChord = chord;
                    best = localTarget;
                    what = hit;
                }
            }
            if (across ? bestChord < DeepChordM : bestChord >= float.MaxValue)
            {
                report.AppendLine(Fmt($"{ring.Label} no {(across ? "across" : "clear")} station: best in-arc crossing of its own hull is {bestChord:0.0} m"));
                continue;
            }
            found.Add(new Station { Ring = ring, LocalTarget = best, Chord = bestChord, Across = across });
            rig.PlaceHeld(hull.GlobalTransform * best, ringPos);
            var to = rig.WorldPosition + Vector3.Up * 0.2f;
            report.AppendLine(Fmt($"{ring.Label} {(across ? "across" : "clear")} station platform='{ring.Site?.GetParent()?.Name}' owned-bodies={ring.PlatformColliderRids().Count} range={ring.Def.DetectionRange:0} hull-chord={bestChord:0.0}m meets='{what}' section-excluded-ray='{HitName(space, ringPos, to, ring.PlatformColliderRids())}' whole-ray='{HitName(space, ringPos, to, null)}'"));
        }
        return found;
    }

    // Every ring's whole in-arc field, bearing by bearing: how much of it the rule leaves clear,
    // and how much of the part that crosses the body of its own hull. Swept with the shipped
    // predicate, so it reports the rule the gunner runs rather than a copy of it.
    private static void FieldOfFire(TestContext ctx, PhysicsDirectSpaceState3D space,
        List<TurretController> rings, Node3D hull, Aabb hullBox, StringBuilder report)
    {
        var toHull = hull.GlobalTransform.AffineInverse();
        int deepTotal = 0;
        int deepClearTotal = 0;
        int deepSectionClearTotal = 0;
        foreach (var ring in rings)
        {
            var ringPos = ring.WorldPosition;
            var anchor = ring.YawNode ?? ring.PitchNode;
            var parentBasis = (anchor.GetParent() as Node3D)?.GlobalBasis ?? Basis.Identity;
            var baseBasis = (parentBasis * anchor.Transform.Basis).Orthonormalized();
            float yawMin = ring.Def.YawRestricted ? ring.Def.YawMinDeg!.Value : -180f;
            float yawMax = ring.Def.YawRestricted ? ring.Def.YawMaxDeg!.Value : 180f;
            float pitchMin = ring.Def.PitchRestricted ? ring.Def.PitchMinDeg!.Value : -80f;
            float pitchMax = ring.Def.PitchRestricted ? ring.Def.PitchMaxDeg!.Value : 80f;
            float standoff = Mathf.Min(ring.Def.DetectionRange * 0.6f, StandoffM);
            var localRing = toHull * ringPos;
            int total = 0;
            int sectionClear = 0;
            int clear = 0;
            int deep = 0;
            int deepClear = 0;
            int deepSectionClear = 0;
            for (float yaw = yawMin; yaw <= yawMax; yaw += 10f)
            {
                for (float pitch = pitchMin; pitch <= pitchMax; pitch += 10f)
                {
                    var target = ringPos + (baseBasis * TurretController.LocalDir(yaw, pitch)) * standoff;
                    if (hullBox.HasPoint(toHull * target))
                    {
                        continue;
                    }
                    total++;
                    bool isDeep = BoxChord(hullBox, localRing, toHull * target) >= DeepChordM;
                    deep += isDeep ? 1 : 0;
                    var to = target + Vector3.Up * 0.2f;
                    if (space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                        ringPos, to, CollisionLayers.World, ring.PlatformColliderRids())).Count == 0)
                    {
                        sectionClear++;
                        deepSectionClear += isDeep ? 1 : 0;
                    }
                    if (!TurretController.WorldBlocksEmplacementLine(space, ringPos, to))
                    {
                        clear++;
                        deepClear += isDeep ? 1 : 0;
                    }
                }
            }
            deepTotal += deep;
            deepClearTotal += deepClear;
            deepSectionClearTotal += deepSectionClear;
            report.AppendLine(Fmt($"field {ring.Label} bearings={total} clear={clear} (section rule {sectionClear}) deep={deep} deep-clear={deepClear} (section rule {deepSectionClear})"));
            ctx.Check(clear * 5 >= total * 2,
                $"{ring.Label} keeps a field of fire: {clear} of {total} in-arc bearings clear");
        }
        report.AppendLine(Fmt($"field TOTAL deep={deepTotal} deep-clear={deepClearTotal} (section rule {deepSectionClearTotal})"));
        ctx.Check(deepClearTotal * 2 <= deepTotal,
            $"a bearing across the body of its own hull is blocked for most rings: {deepClearTotal} of {deepTotal} still clear, against {deepSectionClearTotal} under the mounting-section rule");
    }

    // What a world-layer ray meets first, named by the world object rather than the body: "none"
    // when the line is clear. The exclusion set is the parameter under study, so it is passed in.
    private static string HitName(PhysicsDirectSpaceState3D space, Vector3 from, Vector3 to,
        Godot.Collections.Array<Rid>? excluded)
    {
        var query = excluded != null
            ? PhysicsRayQueryParameters3D.Create(from, to, CollisionLayers.World, excluded)
            : PhysicsRayQueryParameters3D.Create(from, to, CollisionLayers.World);
        var hit = space.IntersectRay(query);
        if (hit.Count == 0)
        {
            return "none";
        }
        string name = hit["collider"].Obj is Node node ? WorldCollision.OwnerOf(node).Name.ToString() : "?";
        return Fmt($"{name}@{from.DistanceTo((Vector3)hit["position"]):0}m");
    }

    // One ring's leg: the plane held at its station in the hull's frame every step, the ring
    // ticked alone (stepping the roster would slew a sibling barrel across this ring's own
    // bearing), and the collider's pose read against the drawn hull's. Returns the shots fired.
    private static int RunLeg(TestContext ctx, ZeppelinRuntime zeps, ProjectilePool live,
        FlightController rig, Node3D hull, Station station, StringBuilder report, bool flying)
    {
        var ring = station.Ring;
        ring.SetActivated(true);
        SyncColliders(hull);   // the leg's own baseline: a docked hull must read zero lag
        var body = FirstBody(hull);
        int before = ring.ShotsFired;
        int frames = (int)(LegSeconds / Dt);
        var gates = new List<TurretGate>();
        int onPlane = 0;
        int clearRays = 0;
        int clearUnexcluded = 0;
        float worstLag = 0f;
        var startPos = hull.GlobalPosition;
        double startTime = Utils.GameClock.Current?.Time ?? 0.0;
        for (int i = 0; i < frames; i++)
        {
            Utils.GameClock.Current?.BeginFrame(Dt);
            if (flying)
            {
                SyncColliders(hull);
            }
            zeps.SimStep(Dt);
            var target = hull.GlobalTransform * station.LocalTarget;
            rig.PlaceHeld(target, ring.WorldPosition);
            ring.SimStep(Dt);
            live.SimStep(Dt);
            if (i % 30 != 0)
            {
                continue;
            }
            gates.Add(ring.Gate);
            if (ring.TargetPosition.DistanceTo(rig.WorldPosition) < 1f)
            {
                onPlane++;
            }
            var space = ctx.Host.GetWorld3D().DirectSpaceState;
            var to = rig.WorldPosition + Vector3.Up * 0.2f;
            if (!TurretController.WorldBlocksEmplacementLine(space, ring.WorldPosition, to))
            {
                clearRays++;
            }
            if (space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                ring.WorldPosition, to, CollisionLayers.World)).Count == 0)
            {
                clearUnexcluded++;
            }
            worstLag = Mathf.Max(worstLag, ColliderLag(body));
        }
        int shots = ring.ShotsFired - before;
        string leg = station.Across ? (flying ? "across-flying" : "across-docked") : "clear-docked";
        report.AppendLine(Fmt($"{ring.Label} {leg}: shots={shots} moved={startPos.DistanceTo(hull.GlobalPosition):0.0}m worst-collider-lag={worstLag:0.000}m target-on-plane={onPlane}/{gates.Count} rule-clear={clearRays}/{gates.Count} whole-ray-clear={clearUnexcluded}/{gates.Count} gates=[{string.Join(",", gates)}]"));
        double elapsed = (Utils.GameClock.Current?.Time ?? 0.0) - startTime;
        ctx.Check(onPlane > 0 && elapsed > 4.0,
            $"{ring.Label} {leg}: the ring tracked the held plane on {onPlane} of {gates.Count} samples over {elapsed:0.0} s of game time");
        if (station.Across)
        {
            ctx.Check(shots == 0,
                $"{ring.Label} {leg}: holds fire over {LegSeconds:0}s across {station.Chord:0} m of its own hull shots={shots} gates=[{string.Join(",", gates)}]");
        }
        else
        {
            ctx.Check(shots > 0,
                $"{ring.Label} {leg}: still engages over {LegSeconds:0}s with its own hull clear of the line shots={shots} gates=[{string.Join(",", gates)}]");
        }
        return shots;
    }

    // How far the physics body has fallen behind the node it hangs off: the collider-trails-the-
    // drawn-hull candidate, read straight off the server rather than inferred from a ray.
    private static float ColliderLag(StaticBody3D? body)
    {
        if (body == null || !GodotObject.IsInstanceValid(body))
        {
            return 0f;
        }
        var held = PhysicsServer3D.BodyGetState(body.GetRid(), PhysicsServer3D.BodyState.Transform)
            .AsTransform3D();
        return held.Origin.DistanceTo(body.GlobalTransform.Origin);
    }

    private static StaticBody3D? FirstBody(Node node)
    {
        if (node is StaticBody3D body)
        {
            return body;
        }
        foreach (var child in node.GetChildren())
        {
            if (FirstBody(child) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    private sealed class Station
    {
        public required TurretController Ring { get; init; }

        public required Vector3 LocalTarget { get; init; }

        public required float Chord { get; init; }

        public required bool Across { get; init; }
    }
}
