using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>BL-714: whether a zeppelin ring holds fire on a target its own hull stands between,
/// over C1/M04's real <c>piratezep</c>. For every emplacement, maps its own authored yaw/pitch
/// envelope against a <c>WorldRayBlocked</c>-shaped cast (world layer, own mount excluded) to find
/// a direction the hull itself should block, then parks a hostile plane there and reads whether
/// the ring holds fire across several of the cached line-of-sight's 1-2 s windows.</summary>
internal static class TurretHullBlockSuites
{
    private const float Dt = 1f / 60f;

    // Long enough to cross several of WorldRayBlocked's random 1-2 s cache windows, so a shot the
    // cache merely missed on its first read cannot pass as "held fire".
    private const float ProbeSeconds = 8f;

    [Suite("turret-hull-blocks-own-fire",
        "a zeppelin ring holds fire on a hostile plane its own hull stands between, over several " +
        "of the cached line-of-sight's 1-2s windows (BL-714): every emplacement on C1/M04's real " +
        "piratezep is gimbal-swept for a direction its own hull blocks (world layer, own mount " +
        "excluded), then probed live at that direction")]
    internal static void TurretHullBlocksOwnFire(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var turretDefs = TurretDefs.Load(ctx.ZrdrPath);
        var report = new StringBuilder();

        ctx.WithWorld("C1", collision: true, mission: "M04", world =>
        {
            var textures = new TextureArchive(texturesPath);
            ProjectilePool? pool = null;
            FlightController? plane = null;
            TurretEmplacementRuntime? emplacements = null;
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
                // plane is something for AcquireTarget to find. The mechanism under test does not
                // read team.
                runtime.SetTeamUnder(hull, InstantActionRuntime.EnemyTeam);

                var space = ctx.Host.GetWorld3D().DirectSpaceState;

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
                plane = rig;

                // Every ring's gimbal envelope, mapped before any SimStep moves a PitchNode off the
                // rest pose BaseBasis reads: per (yaw, pitch), whether a WorldRayBlocked-shaped cast
                // (world layer, own section excluded) meets the hull inside DETECTION_RANGE.
                var candidates = new List<(TurretController Ring, Vector3 Dir, float Fraction)>();
                foreach (var ring in rings)
                {
                    var ringPos = ring.WorldPosition;
                    var anchor = ring.YawNode ?? ring.PitchNode;
                    var parentBasis = (anchor.GetParent() as Node3D)?.GlobalBasis ?? Basis.Identity;
                    var baseBasis = (parentBasis * anchor.Transform.Basis).Orthonormalized();
                    var excluded = ring.PlatformColliderRids();
                    float yawMin = ring.Def.YawRestricted ? ring.Def.YawMinDeg!.Value : -180f;
                    float yawMax = ring.Def.YawRestricted ? ring.Def.YawMaxDeg!.Value : 180f;
                    float pitchMin = ring.Def.PitchRestricted ? ring.Def.PitchMinDeg!.Value : -80f;
                    float pitchMax = ring.Def.PitchRestricted ? ring.Def.PitchMaxDeg!.Value : 80f;
                    Vector3? blockedDir = null;
                    float blockedFraction = 0f;
                    string blockedName = "none";
                    for (float yaw = yawMin; blockedDir == null && yaw <= yawMax; yaw += 10f)
                    {
                        for (float pitch = pitchMin; pitch <= pitchMax; pitch += 10f)
                        {
                            var dir = baseBasis * TurretController.LocalDir(yaw, pitch);
                            var to = ringPos + dir * ring.Def.DetectionRange;
                            var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                                ringPos, to, CollisionLayers.World, excluded));
                            if (hit.Count > 0)
                            {
                                blockedDir = dir;
                                var hitPos = (Vector3)hit["position"];
                                blockedFraction = ringPos.DistanceTo(hitPos) / ring.Def.DetectionRange;
                                blockedName = hit["collider"].Obj is Node hitNode
                                    ? WorldCollision.OwnerOf(hitNode).Name.ToString() : "?";
                                break;
                            }
                        }
                    }
                    if (blockedDir is { } dirFound)
                    {
                        candidates.Add((ring, dirFound, blockedFraction));
                    }
                    report.AppendLine($"{ring.Label} excluded={excluded.Count} sweep-hit={blockedName} at {blockedFraction:0.00} of DETECTION_RANGE");
                }
                ctx.Check(candidates.Count > 0,
                    $"piratezep's own gimbal sweep finds at least one ring with an in-arc, hull-blocked direction (found {candidates.Count} of {rings.Count})");

                // A dormant ring's SimStep returns before the LOS check ever runs (ACTIVATED gates
                // tracking too), so it can neither confirm nor break this mechanism; wake every
                // candidate so its probe actually exercises WorldRayBlocked.
                foreach (var (ring, _, _) in candidates)
                {
                    ring.SetActivated(true);
                }

                // Ticked in isolation (ring.SimStep, not runtime.SimStep): stepping the whole roster
                // would slew every OTHER candidate off the rest pose its own sweep read, which found
                // a since-moved sibling ring's own barrel as "the hull" while tracing this.
                var testOrder = candidates.OrderByDescending(c => c.Ring.Label.EndsWith("@ctur2")).ToList();
                int ringsBlocked = 0;
                int ringsConfirmed = 0;
                foreach (var (ring, dir, fraction) in testOrder)
                {
                    var ringPos = ring.WorldPosition;
                    var anchorNode = (Node3D)(ring.YawNode ?? ring.PitchNode);
                    var ringSpace = anchorNode.GetWorld3D()?.DirectSpaceState;
                    var probeExcluded = ring.PlatformColliderRids();

                    // WorldPosition sits meters off the raw PlaceHeld pose, so a fixed distance guess
                    // can clip a thin panel's edge; confirmed instead against increasing fractions
                    // past the sweep's own hit point, with WorldRayBlocked's own exact call shape.
                    Vector3? confirmedPos = null;
                    for (float extra = 0.15f; extra <= 0.6f; extra += 0.1f)
                    {
                        var tryPos = ringPos + dir * (ring.Def.DetectionRange * Mathf.Min(0.97f, fraction + extra));
                        rig.PlaceHeld(tryPos, ringPos);
                        var checkTo = rig.WorldPosition + Vector3.Up * 0.2f;
                        var checkHit = ringSpace?.IntersectRay(PhysicsRayQueryParameters3D.Create(
                            ring.WorldPosition, checkTo, CollisionLayers.World, probeExcluded));
                        if (checkHit is { Count: > 0 })
                        {
                            confirmedPos = tryPos;
                            break;
                        }
                    }
                    report.AppendLine($"{ring.Label} dir={dir} sweep-hit-fraction={fraction:0.00} confirmed-blocked-placement={(confirmedPos.HasValue ? "yes" : "NO")}");
                    if (confirmedPos is not { } chosenPos)
                    {
                        // The sweep's own obstruction could not be confirmed with WorldRayBlocked's
                        // exact call at any tried distance (a thin panel's edge, most likely): not
                        // this ring's evidence either way, so it is not asserted on.
                        continue;
                    }
                    rig.PlaceHeld(chosenPos, ringPos);
                    ringsConfirmed++;
                    int before = ring.ShotsFired;
                    bool sawBlocked = false;
                    var gates = new List<TurretGate>();
                    int frames = (int)(ProbeSeconds / Dt);
                    for (int i = 0; i < frames; i++)
                    {
                        ring.SimStep(Dt);
                        live.SimStep(Dt);
                        if (ring.Gate == TurretGate.Blocked)
                        {
                            sawBlocked = true;
                        }
                        if (i % 60 == 0)
                        {
                            gates.Add(ring.Gate);
                        }
                    }
                    int shots = ring.ShotsFired - before;
                    if (sawBlocked)
                    {
                        ringsBlocked++;
                    }
                    report.AppendLine($"{ring.Label} dir={dir} pos={chosenPos}: shots={shots} sawBlocked={sawBlocked} gates=[{string.Join(",", gates)}]");
                    ctx.Check(shots == 0,
                        $"{ring.Label} holds fire over {ProbeSeconds:0}s on the far side of its own hull (confirmed blocked by its own gimbal sweep) shots={shots} gates=[{string.Join(",", gates)}]");
                }
                ctx.Check(ringsConfirmed > 0,
                    $"at least one ring's sweep-found obstruction is confirmed with WorldRayBlocked's exact call shape ({ringsConfirmed} of {candidates.Count})");
                ctx.Check(ringsBlocked == ringsConfirmed,
                    $"every confirmed-blocked ring reads Blocked at least once rather than merely missing the target ({ringsBlocked} of {ringsConfirmed})");

                ctx.WriteArtifact("test-turret-hull-block.txt", report.ToString());
                ctx.Note($"{candidates.Count} of {rings.Count} ring(s) had an in-arc hull-blocked direction; {ringsConfirmed} confirmed, all held fire there");
            }
            finally
            {
                plane?.Free();
                emplacements?.Free();
                pool?.Free();
                textures.Dispose();
            }
        });
    }
}
