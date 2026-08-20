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

internal static class AnimationAndEffectsSuites
{
    internal static void EffectsCensus(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var names = Session.EffectCatalogue.WorldEffectAnimNames(world.Session.Program);
            // The staged set is DERIVED, so this census stages what the
            // real world-effects build stages, from the same call — a root the closure gains and
            // this chapter's gamez cannot supply throws here, naming the def and the anchor.
            var roots = Session.WorldEffectsFactory.EffectStageRootNames(world.Session.Program, world.Gamez);
            var stage = new Node3D { Name = "EffectCensusStage" };
            var pool = new Node3D { Name = "pool0" };
            pool.SetMeta(AnimRuntime.PoolSlotMeta, 0);
            stage.AddChild(pool);
            int built = Session.WorldEffectsFactory.BuildEffectStage(world.Gamez,
                world.Session.Builder.Scene, pool, roots);
            ctx.Check(built == roots.Count, $"staged {built}/{roots.Count} template root(s)");
            foreach (var child in pool.GetChildren())
                if (child is Node3D root)
                {
                    root.Visible = false;
                }

            var point = new Vector3(150, 40, 90);
            var runtime = AnimRuntime.ForEffects(
                AnimRuntime.NewTemplateStage(pooled: true, shown: true, placesCalled: true),
                1, new CountingEmitterFactory(), false, 32f,
                () => point);
            runtime.ManualAdvance = true;
            ctx.Host.AddChild(stage);
            ctx.Host.AddChild(runtime);
            try
            {
                runtime.Bind(stage, world.Session.Program.Subset(names));
                var r = Probes.Effects(runtime, names, point, stage, ctx.Chapter);

                ctx.Check(r.Ok, $"all effects resolve ({r.Resolved}/{names.Count} resolved)");
                var far = r.Rows.SelectMany(row => row.MeshPeaks
                        .Where(pk => pk.Visible > 0 && pk.Distance > 100f)
                        .Select(pk => $"{row.Name}: {pk.Root} @{pk.Distance:0} m"))
                    .ToList();
                ctx.Check(far.Count == 0,
                    $"every lit template mesh peaked at the CALL SITE, not the stage origin{(far.Count == 0 ? "" : $" — {string.Join("; ", far)}")}");
                var lit = r.Rows.Where(row => row.Residual.Count > 0)
                    .Select(row => $"{string.Join("/", row.Residual.Select(x => x.Root))} after {row.Name}")
                    .ToList();
                ctx.Check(lit.Count == 0,
                    $"no template mesh left lit after its effect was stopped{(lit.Count == 0 ? "" : $" — {string.Join("; ", lit)}")}");
                ctx.Check(r.Puffered == 30,
                    $"the puffer half's tally holds under suite conditions ({r.Puffered} built one, expected 30)");
                // The 19 includes `biggun_flying_parts` (its eight parts fly their solved parabola
                // before their own deactivation, so samples catch them drawing) and the seeker's
                // `ballflare.flt`, whose one sequence lights the flare disc it is anchored on.
                ctx.Check(r.Meshed == 19,
                    $"the mesh half's tally holds under suite conditions ({r.Meshed} showed meshes, expected 19)");
            }
            finally
            {
                runtime.Free();
                stage.Free();
            }

            // The derivation IS the staged set; there is no hand table to compare against. What still needs
            // saying per chapter is that the pool config sizes the set really staged, since a root renamed on
            // one side sizes nothing, silently.
            var unsized = Utils.EffectPools.Load().UnknownRoots(roots);
            ctx.Check(unsized.Count == 0,
                $"effect_pools.json sizes only roots this bind stages — {ctx.Chapter}{(unsized.Count == 0 ? "" : $" — sizes nothing: {string.Join(", ", unsized)}")}");

            CrashStageRootTripwire(ctx, world);
        });
    }

    // The crash half, on a replica of the crash rig's own bind scope: the player crash root, the plane
    // model and its destroyed wreck, minus the runtime the anchor question does not need. Per-plane on
    // purpose, since the wreck and part subtrees vary by airframe and the Devastator is the one whose
    // own model root a crash def names. Asserts the rig's derived roots all BUILD from this chapter's
    // gamez; a root the closure asks for that the chapter cannot supply is the silent-miss failure.
    internal static void CrashStageRootTripwire(TestContext ctx, TestWorld world)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, world.Chapter);
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        try
        {
            foreach (var model in new[] { "player_bhawk", "player_pfighter" })
            {
                var rigScope = new Node3D { Name = "player" };
                rigScope.SetMeta(AnimRuntime.NameMeta, "player");
                try
                {
                    var builder = new PlaneBuilder(planesGamez, textures);
                    rigScope.AddChild(builder.Build(model));
                    if (builder.BuildDestroyed(model) is { } wreck)
                    {
                        rigScope.AddChild(wreck);
                    }
                    ctx.Host.AddChild(rigScope);
                    var resolve = Session.WorldEffectsFactory.StageRootResolver(world.Gamez, rigScope);
                    var rigRoots = Session.EffectCatalogue.CrashStageRoots(world.Session.Program, resolve);
                    var built = new Node3D { Name = "crash_template_replica" };
                    ctx.Host.AddChild(built);
                    int n = Session.WorldEffectsFactory.BuildEffectStage(world.Gamez,
                        world.Session.Builder.Scene, built, rigRoots);
                    built.Free();
                    ctx.Check(n == rigRoots.Count,
                        $"{model}: the crash rig stages every root its bound defs anchor on ({n}/{rigRoots.Count}) — {world.Chapter}");
                }
                finally
                {
                    rigScope.Free();
                }
            }
        }
        finally
        {
            textures.Dispose();
        }
    }

    // ---- contact: the flight ends where the world says ------------------------------------------

    // A gravity-bearing OBJECT_MOTION must be cut short by real geometry instead of running its
    // authored RUN_TIME out below the terrain, through whichever of the two tiers its flags select, and
    // must pick its BOUNCE_SEQUENCE branch from the surface it struck. Driven as a synthetic body
    // thrown downward from a known height, so flight time, resting height and branch are predictable.
    // ⚠ Keep the last case, the same bodies with no mask handed over, running their full clock far
    // below the surface: without it a suite that fired no query at all would pass its landing checks.
    internal static void GroundContact(TestContext ctx)
    {
        const float Tick = 1f / 60f;
        const float Authored = 20f;      // the run time these bodies carry; contact must beat it
        const float DropHeight = 60f;    // above whatever the probe finds, well clear of the arming epsilon
        const string Land = "testhit_ground";
        const string Wet = "testhit_water";

        ctx.WithWorld(ctx.Chapter, collision: true, world =>
        {
            var runtime = world.Runtime;
            var root = world.Session.Root;

            // A suite that builds no colliders would pass every contact check by taking the
            // fallback and proving nothing, so the collision world is asserted before anything
            // else is asked of it.
            var space = root.GetWorld3D()?.DirectSpaceState;
            ctx.Check(space != null, $"the world built a collision space to sweep against chapter={ctx.Chapter}");
            if (space == null)
            {
                return;
            }

            // Somewhere with ground under it: probe straight down from high up over the origin
            // column and take what the world actually offers, rather than assuming a height.
            var from = new Vector3(0f, 400f, 0f);
            var probe = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                from, new Vector3(0f, -400f, 0f), CollisionLayers.World));
            ctx.Check(probe.Count > 0, $"a downward probe finds chapter geometry chapter={ctx.Chapter}");
            if (probe.Count == 0)
            {
                return;
            }

            float surfaceY = probe["position"].AsVector3().Y;

            // One authored OBJECT_MOTION, built by hand: a 5 m/s downward throw under Earth
            // gravity from DropHeight above that surface, `do_intersections` on, a 20 s run time
            // and both bounce branches named so the choice is observable.
            static Dictionary<string, object?> Vec(float x, float y, float z) =>
                new() { ["x"] = x, ["y"] = y, ["z"] = z };

            // flagged: false is the DEFAULT authoring and selects the ground column; noAltitude is the opt-out
            // that selects neither; timed: false omits run_time the way the ballistic events do, and is thrown
            // upward so the parabola has an apex to report to the sequence.
            AnimData Body(bool flagged = true, bool noAltitude = false, bool complex = true,
                bool timed = true, bool impactForce = false)
            {
                var props = new Dictionary<string, object?>
                {
                    ["impact_force"] = impactForce,
                    ["gravity"] = new Dictionary<string, object?>
                    {
                        ["value"] = -9.8f,
                        ["complex"] = complex,
                        ["no_altitude"] = noAltitude,
                        ["do_intersections"] = flagged,
                    },
                    ["translation"] = new Dictionary<string, object?>
                    {
                        ["initial"] = Vec(0f, timed ? -5f : 10f, 0f),
                        ["delta"] = Vec(0f, 0f, 0f),
                        ["rnd_xz"] = Vec(0f, 0f, 0f),
                    },
                    ["bounce_sequence"] = new Dictionary<string, object?>
                    {
                        ["default"] = Land,
                        ["water"] = Wet,
                        ["lava"] = null,
                    },
                };
                if (timed)
                {
                    props["run_time"] = Authored;
                }

                return new AnimData(props);
            }

            // Runs one body to a stop and reports what happened to it. waterHook stands in for the session's
            // ProjectilePool.SurfaceIsWater binding: the surface-id read has its own coverage, and stubbing it
            // is what makes the branch choice assertable without needing a chapter with reachable sea.
            (float Flight, float EndY, string? Bounce, bool ByContact, MotionContactTier Tier,
                int ColumnLandings, int SweepLandings, float Reported) Run(
                uint mask, System.Func<GodotObject?, bool>? waterHook, AnimData? body = null,
                float limit = 0f)
            {
                var node = new Node3D { Name = "ground-contact-probe" };
                root.AddChild(node);
                node.GlobalPosition = new Vector3(0f, surfaceY + DropHeight, 0f);

                uint maskWas = runtime.ContactMask;
                var hookWas = runtime.SurfaceIsWater;
                runtime.ContactMask = mask;
                runtime.SurfaceIsWater = waterHook;
                try
                {
                    var data = body ?? Body();
                    // What AnimRuntime passes: the authored value, or 0 when the event omits one.
                    var motion = MotionRuntime.Create(runtime, node, data, data.Num("run_time") ?? 0f);
                    if (motion == null)
                    {
                        return (0f, node.GlobalPosition.Y, null, false, MotionContactTier.None, 0, 0, 0f);
                    }

                    var set = new MotionSet();
                    set.Add(motion, world.Runtime.Destructibles.All.First().Def, null);
                    float flown = 0f;
                    string? bounce = null;
                    float cap = limit > 0f ? limit : Authored;
                    for (int i = 0; i < (int)(cap / Tick) + 2 && !motion.Finished; i++)
                    {
                        foreach (var landing in set.Tick(Tick))
                        {
                            bounce = landing.Bounce;
                        }

                        flown += Tick;
                    }

                    return (flown, node.GlobalPosition.Y, bounce, motion.LandedByContact,
                        motion.ContactTier, set.ColumnLandings, set.SweepLandings, motion.RunTime);
                }
                finally
                {
                    runtime.ContactMask = maskWas;
                    runtime.SurfaceIsWater = hookWas;
                    node.QueueFree();
                }
            }

            // 1 — the SWEEP tier cuts the flight short and rests the body ON the surface.
            var hit = Run(CollisionLayers.World, _ => false);
            ctx.Check(hit.Tier == MotionContactTier.Sweep,
                $"do_intersections selects the sweep tier={hit.Tier}");
            ctx.Check(hit.ByContact, $"the sweep ended the body on a collider flight={hit.Flight:0.00}s");
            ctx.Check(hit.Flight < Authored,
                $"contact beat the authored run time flight={hit.Flight:0.00}s authored={Authored:0}s");
            // A band, not a point: the hit lands between two frames and the body is a point, so
            // "on the surface" is within a tick's fall of it, never below it.
            ctx.Check(hit.EndY >= surfaceY - 1f && hit.EndY <= surfaceY + 2f,
                $"the body rests at the struck surface endY={hit.EndY:0.00} surfaceY={surfaceY:0.00}");
            ctx.Check(hit.Bounce == Land, $"contact dispatched the default branch bounce={hit.Bounce ?? "(none)"}");
            ctx.Check(hit.SweepLandings == 1 && hit.ColumnLandings == 0,
                $"and it is tallied as a sweep landing sweep={hit.SweepLandings} column={hit.ColumnLandings}");

            // 2 — the same contact over water takes the water branch.
            var wet = Run(CollisionLayers.World, _ => true);
            ctx.Check(wet.Bounce == Wet, $"a water surface picks the water branch bounce={wet.Bounce ?? "(none)"}");

            // 1b — the DEFAULT tier. The identical body with `do_intersections` off, which is how
            // 1,466 of the install's gravity-bearing events are authored, must land too and rest in
            // the same place. Before C6 this body sank through the world and ran its 20 s clock out.
            var column = Run(CollisionLayers.World, _ => false, Body(flagged: false));
            ctx.Check(column.Tier == MotionContactTier.Column,
                $"an unflagged gravity body selects the default column tier={column.Tier}");
            ctx.Check(column.ByContact && column.Flight < Authored,
                $"the column ended the body on a surface flight={column.Flight:0.00}s authored={Authored:0}s");
            ctx.Check(column.EndY >= surfaceY - 1f && column.EndY <= surfaceY + 2f,
                $"and rests it at the struck surface endY={column.EndY:0.00} surfaceY={surfaceY:0.00}");
            ctx.Check(column.Bounce == Land,
                $"the column landing dispatches its branch too bounce={column.Bounce ?? "(none)"}");
            ctx.Check(column.ColumnLandings == 1 && column.SweepLandings == 0,
                $"and is tallied apart from the sweep column={column.ColumnLandings} sweep={column.SweepLandings}");

            // 1c — NO_ALTITUDE is the opt-out, and it vetoes the COLUMN only. `gunshell` is its one
            // author install-wide, and it must keep falling through the world exactly as before.
            var optedOut = Run(CollisionLayers.World, _ => false, Body(flagged: false, noAltitude: true));
            ctx.Check(optedOut.Tier == MotionContactTier.None,
                $"no_altitude vetoes the column tier={optedOut.Tier}");
            ctx.Check(!optedOut.ByContact && optedOut.EndY < surfaceY - 100f,
                $"so the body falls straight through endY={optedOut.EndY:0.00} surfaceY={surfaceY:0.00}");

            // 1d — and it does not suppress an explicitly authored sweep. The original's branch
            // order reaches the veto only on an unflagged body, so a reading that treats the two
            // flags as independent conditions fails right here.
            var bothFlags = Run(CollisionLayers.World, _ => false, Body(flagged: true, noAltitude: true));
            ctx.Check(bothFlags.Tier == MotionContactTier.Sweep && bothFlags.ByContact,
                $"do_intersections outranks no_altitude tier={bothFlags.Tier} byContact={bothFlags.ByContact}");

            // An UNTIMED launch, the shape C8 re-terminates. ⚠ It must keep REPORTING its parabola rather than
            // its watchdog: the reported number is what the sequence waits on, so a body reporting 15 s would
            // leave every vanish-shape piece on screen that long and divide its tumble by the same figure.
            var untimed = Run(CollisionLayers.World, _ => false, Body(flagged: false, timed: false));
            ctx.Check(untimed.ByContact && untimed.Flight < 10f,
                $"an untimed launch still ends on the ground flight={untimed.Flight:0.00}s byContact={untimed.ByContact}");
            ctx.Check(untimed.Reported > 0f && untimed.Reported < 5f,
                $"and reports its own parabola to the sequence, not the 15 s watchdog reported={untimed.Reported:0.00}s");
            ctx.Check(untimed.Bounce == Land,
                $"its branch comes from the surface it struck bounce={untimed.Bounce ?? "(none)"}");

            // 1g — THE WATCHDOG ITSELF, which nothing else here can fire: the same untimed body
            // with a mask no collider answers. The tier is selected (the mask is non-zero) but
            // every query comes back empty, which is the only thing that charges the accumulator.
            // Without it this body would fly forever, since an untimed launch has no clock.
            const uint EmptyLayer = 1u << 20;   // no collider in this project is built on it
            var watchdog = Run(EmptyLayer, _ => false, Body(flagged: false, timed: false), limit: 20f);
            ctx.Check(watchdog.Tier == MotionContactTier.Column && !watchdog.ByContact,
                $"a query that answers nothing still selects the tier tier={watchdog.Tier} byContact={watchdog.ByContact}");
            ctx.Check(watchdog.Flight > 14f && watchdog.Flight < 16f,
                $"and the 15 s column watchdog ends the body flight={watchdog.Flight:0.00}s");
            ctx.Check(watchdog.Bounce == Land,
                $"a watchdog end owes the default branch, from its null surface bounce={watchdog.Bounce ?? "(none)"}");

            // The veto on REAL extracted data. Every case above builds its gravity block by hand, which pins
            // the branch but not that no_altitude survives extraction and reaches Create at all. gunshell is
            // its only author install-wide, and it is reachable because muzzleburst_effects CallAnimations it.
            {
                var shellDefs = world.Session.Program.ByAnimName("gunshell");
                AnimData? shell = null;
                foreach (var def in shellDefs)
                {
                    foreach (var seq in def.Sequences)
                    {
                        foreach (var ev in seq.Events)
                        {
                            if (ev.Kind == "ObjectMotion" && ev.Data.Obj("translation_range") != null)
                            {
                                shell ??= ev.Data;
                            }
                        }
                    }
                }

                ctx.Check(shell != null,
                    $"the chapter program carries gunshell's launch defs={shellDefs.Count} chapter={ctx.Chapter}");
                if (shell != null)
                {
                    ctx.Check(shell.Obj("gravity")?.Bool("no_altitude") == true,
                        $"and the extracted event still authors no_altitude value={shell.Obj("gravity")?.Bool("no_altitude")}");
                    var node = new Node3D { Name = "ground-contact-gunshell" };
                    root.AddChild(node);
                    node.GlobalPosition = new Vector3(0f, surfaceY + DropHeight, 0f);
                    uint maskWas = runtime.ContactMask;
                    runtime.ContactMask = CollisionLayers.World;
                    try
                    {
                        var casing = MotionRuntime.Create(runtime, node, shell, shell.Num("run_time") ?? 2f);
                        ctx.Check(casing is { ContactTier: MotionContactTier.None },
                            $"so the one def that opts out selects no tier even with a mask wired tier={casing?.ContactTier}");
                    }
                    finally
                    {
                        runtime.ContactMask = maskWas;
                        node.QueueFree();
                    }
                }
            }

            // The bounce is a CONTINUATION: the sequence a landing dispatches re-launches the very node that
            // landed, and MotionRuntime.Create ordinarily re-homes a ballistic launch to the node's authored
            // rest pose, which shows as the crash jumping back to the crash point once per piece.
            {
                var node = new Node3D { Name = "ground-contact-resume" };
                root.AddChild(node);
                var restPose = new Vector3(0f, surfaceY + DropHeight, 0f);
                node.GlobalPosition = restPose;
                runtime.RestOf(node);   // record that pose as the authored rest, as a built node has

                uint maskWas = runtime.ContactMask;
                runtime.ContactMask = CollisionLayers.World;
                try
                {
                    var first = MotionRuntime.Create(runtime, node, Body(), Authored);
                    var set = new MotionSet();
                    set.Add(first!, world.Runtime.Destructibles.All.First().Def, null);
                    bool landed = false;
                    for (int i = 0; i < (int)(Authored / Tick) + 2 && !first!.Finished; i++)
                    {
                        foreach (var landing in set.Tick(Tick))
                        {
                            // What TickMotions does before dispatching the sequence.
                            landed = true;
                            if (landing.ByContact)
                            {
                                runtime.MarkLandingResume(landing.Target);
                            }
                        }
                    }

                    ctx.Check(landed, $"the first flight landed by contact before the follow-up y={node.GlobalPosition.Y:0.00}");
                    float restedY = node.GlobalPosition.Y;
                    // The follow-up is built the way pNhit authors one: the SAME body with the flag off. ⚠ It must
                    // resume from the landing and still be contact-tested, or it runs its whole clock and buries the
                    // piece under the airfield, which is the "plane went through the ground" symptom.
                    var settle = Body(flagged: false, impactForce: true); // ⚠ ON, or the authored gate refuses before the landing rule is asked
                    // A dive hands the crash rig a large downward momentum. The FIRST launch spends
                    // it; a hop off the ground must not be handed it again, or it covers the 2 m
                    // arming epsilon in 0.044 s and is under the terrain before the sweep can look.
                    var inheritWas = runtime.InheritedWorldVelocity;
                    runtime.ArmInheritedVelocity(new Vector3(0f, -45f, 0f));
                    var second = MotionRuntime.Create(runtime, node, settle, Authored);
                    runtime.ArmInheritedVelocity(inheritWas);
                    second?.Seek(0f);
                    float relaunchY = node.GlobalPosition.Y;
                    ctx.Check(Mathf.Abs(relaunchY - restedY) < 1f,
                        $"the follow-up launch starts from the landing, not the authored rest relaunchY={relaunchY:0.00} restedY={restedY:0.00} rest={restPose.Y:0.00}");
                    ctx.Check(second is { ContactTier: MotionContactTier.Column },
                        $"the settle hop is contact-tested through the default column tier={second?.ContactTier}");
                    second?.Seek(0.2f);
                    float hopY = node.GlobalPosition.Y;
                    // The band, not a point: this synthetic body is authored throwing DOWNWARD at
                    // 5 m/s, so 0.2 s of it is −1.20 m on its own. Inheriting the −45 m/s dive on
                    // top would put it another 9 m under.
                    ctx.Check(hopY > relaunchY - 3f,
                        $"and it inherits none of the dive's momentum hopY={hopY:0.00} relaunchY={relaunchY:0.00} (inherited it would be ≈{relaunchY - 10.2f:0.00})");

                    // A plain launch on a node that did NOT just land takes the same tier: the resume mark buys
                    // momentum and re-homing rules, never a different contact mechanism. A body that reported Sweep
                    // here would mean the retired inheritance had grown back somewhere.
                    var elsewhere = new Node3D { Name = "ground-contact-unflagged" };
                    root.AddChild(elsewhere);
                    elsewhere.GlobalPosition = restPose;
                    var plain = MotionRuntime.Create(runtime, elsewhere, settle, Authored);
                    ctx.Check(plain is { ContactTier: MotionContactTier.Column },
                        $"a false-flagged launch that continues nothing takes the column too tier={plain?.ContactTier}");

                    // The IMPACT_FORCE gate itself: one armed runtime, one body, the authored flag
                    // the only difference. ⚠ High above the surface, not at the rest pose, or the
                    // column tier stops both launches on the same polygon and they read alike.
                    elsewhere.GlobalPosition = new Vector3(0f, surfaceY + DropHeight, 0f);
                    float LaunchY(bool impactForce)
                    {
                        MotionRuntime.Create(runtime, elsewhere, Body(flagged: false, impactForce: impactForce),
                            Authored)?.Seek(0.2f);
                        return elsewhere.GlobalPosition.Y;
                    }

                    runtime.ArmInheritedVelocity(new Vector3(0f, -45f, 0f));
                    float gated = LaunchY(impactForce: false);
                    float carried = LaunchY(impactForce: true);
                    ctx.Check(gated - carried > 5f,
                        $"the authored impact_force flag is what admits the momentum, not the def's name: flagged fell to {carried:0.00}, unflagged to {gated:0.00}");
                    // ⚠ And the arm is a flag, not "the vector is non-zero": a sub-threshold Callback
                    // 16 CLEARS it in the original (`004ee143`), so a flagged body gets nothing after
                    // one, even though the stored vector is not zero.
                    runtime.ArmInheritedVelocity(new Vector3(0f, -0.005f, 0f));
                    ctx.Check(!runtime.InheritedVelocityArmed
                              && runtime.InheritedWorldVelocity != Vector3.Zero,
                        $"a sub-0.01 Callback 16 disarms the instance and still stores its vector armed={runtime.InheritedVelocityArmed} v={runtime.InheritedWorldVelocity}");
                    float afterDisarm = LaunchY(impactForce: true);
                    ctx.Check(Mathf.Abs(afterDisarm - gated) < 0.01f,
                        $"…so the flagged launch inherits nothing after it y={afterDisarm:0.00} unflagged={gated:0.00}");
                    runtime.ArmInheritedVelocity(inheritWas);
                    elsewhere.QueueFree();
                }
                finally
                {
                    runtime.ContactMask = maskWas;
                    node.QueueFree();
                }
            }

            // 3 — THE CONTROL. No mask: neither tier, so the body runs its full clock and ends far
            // below the surface — which is also the no-collision-world fallback every golden
            // capture takes.
            var free = Run(0u, _ => false);
            ctx.Check(free.Tier == MotionContactTier.None && !free.ByContact,
                $"with no mask the body selects no tier at all tier={free.Tier} flight={free.Flight:0.00}s");
            ctx.Check(free.EndY < surfaceY - 100f,
                $"the unmasked body falls straight through endY={free.EndY:0.00} surfaceY={surfaceY:0.00}");
            var freeColumn = Run(0u, _ => false, Body(flagged: false));
            ctx.Check(freeColumn.Tier == MotionContactTier.None && freeColumn.EndY < surfaceY - 100f,
                $"and so does the unflagged body the column would otherwise land tier={freeColumn.Tier} endY={freeColumn.EndY:0.00}");
            ctx.Note($"sweep {hit.Flight:0.00}s ending {hit.EndY - surfaceY:0.00} m from the surface; column {column.Flight:0.00}s ending {column.EndY - surfaceY:0.00} m from it; unmasked {free.Flight:0.00}s ending {free.EndY - surfaceY:0.00} m from it");
            // ⚠ Two decoded rules are deliberately not asserted here. With a downward column, the
            // descending-step admission and complex's widening of it can only save the query, never change the
            // outcome; both are transcribed in TryGroundColumn, and the query's shape is what to re-check.
        });
    }

    // ---- the tumble: a rate about the launch's own perpendicular ---------------------------------

    // FORWARD_ROTATION turns a launched body about the horizontal PERPENDICULAR of its own launch
    // direction, at the authored rate and scaled by that direction's horizontal length, so one authored
    // number tumbles a flat throw fast and a steep one slowly. Every case is arithmetic on a synthetic
    // body, with min = max ranges and the pose read back as geometry rather than as the euler triple
    // the implementation writes. ⚠ A body launched by the vector translation form must hold its
    // orientation exactly; that is the case that fails if the axis is ever "fixed" to a mesh axis.
    internal static void ForwardRotation(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var runtime = world.Runtime;
            var root = world.Session.Root;

            static Dictionary<string, object?> Vec(float x, float y, float z) =>
                new() { ["x"] = x, ["y"] = y, ["z"] = z };
            static Dictionary<string, object?> Range(float v) =>
                new() { ["min"] = v, ["max"] = v };

            // A ranged launch at one exact azimuth/elevation, tumbling at `rate` (+`accel`).
            static AnimData Ranged(float azimuth, float elevation, float rate, float accel = 0f,
                float runTime = 5f) =>
                new(new Dictionary<string, object?>
                {
                    ["translation_range"] = new Dictionary<string, object?>
                    {
                        ["xz"] = Range(azimuth),
                        ["y"] = Range(elevation),
                        ["initial"] = Range(10f),
                        ["delta"] = Range(0f),
                    },
                    ["forward_rotation"] = new Dictionary<string, object?>
                    {
                        ["Time"] = new Dictionary<string, object?>
                        {
                            ["initial"] = rate,
                            ["delta"] = accel,
                        },
                    },
                    ["run_time"] = runTime,
                });

            // The same tumble on the VECTOR launch form — the shape the crash pieces author.
            static AnimData Vector(float rate) =>
                new(new Dictionary<string, object?>
                {
                    ["translation"] = new Dictionary<string, object?>
                    {
                        ["initial"] = Vec(10f, 0f, 0f),
                        ["delta"] = Vec(0f, 0f, 0f),
                        ["rnd_xz"] = Vec(0f, 0f, 0f),
                    },
                    ["forward_rotation"] = new Dictionary<string, object?>
                    {
                        ["Time"] = new Dictionary<string, object?>
                        {
                            ["initial"] = rate,
                            ["delta"] = 0f,
                        },
                    },
                    ["run_time"] = 5f,
                });

            Basis Pose(AnimData data, float t)
            {
                var node = new Node3D { Name = "forward-rotation-probe" };
                root.AddChild(node);
                try
                {
                    var motion = MotionRuntime.Create(runtime, node, data, data.Num("run_time") ?? 0f);
                    motion?.Seek(t);
                    return node.Transform.Basis.Orthonormalized();
                }
                finally
                {
                    node.QueueFree();
                }
            }

            // 1 — the axis. A flat throw along +X (azimuth 0, h = 1) turns about (0, 0, −1): after a
            // quarter turn at π/2 rad/s the body's own up axis points along the throw and its own X
            // points down, which is an end-over-end tumble FORWARD over the launch.
            var alongX = Pose(Ranged(0f, 0f, Mathf.Pi / 2f), 1f);
            ctx.Check(alongX.Y.IsEqualApprox(Vector3.Right) && alongX.X.IsEqualApprox(Vector3.Down),
                $"a throw along +X pitches forward over it up={alongX.Y} fwd={alongX.X}");

            // And the axis FOLLOWS the throw rather than the mesh: the same body launched along +Z
            // turns about (1, 0, 0) instead, which the old local-X reading cannot produce.
            var alongZ = Pose(Ranged(90f, 0f, Mathf.Pi / 2f), 1f);
            ctx.Check(alongZ.Y.IsEqualApprox(Vector3.Back),
                $"and a throw along +Z pitches forward over THAT up={alongZ.Y}");

            // 2 — the rate is a RATE, not an angle over the run time. Two bodies with the same
            // authored number and different run times must be in the same pose at the same instant;
            // under the ÷ run_time reading the 2 s body would have turned 2.5× as far.
            var slow = Pose(Ranged(0f, 0f, 1f, runTime: 5f), 1f);
            var fast = Pose(Ranged(0f, 0f, 1f, runTime: 2f), 1f);
            ctx.Check(slow.X.IsEqualApprox(fast.X) && slow.Y.IsEqualApprox(fast.Y),
                $"the run time does not scale the tumble at 5 s={slow.X} at 2 s={fast.X}");
            ctx.Check(Mathf.Abs(slow.GetEuler(EulerOrder.Yxz).Z + 1f) < 1e-3f,
                $"one second of 1 rad/s is one radian euler={slow.GetEuler(EulerOrder.Yxz)}");

            // 3 — the launch's own horizontal length scales it, which is what makes a steep throw
            // tumble slowly off the same authored number. At 60° of elevation h = 1/3.
            var steep = Pose(Ranged(0f, 60f, 1f), 1f);
            float steepAngle = -steep.GetEuler(EulerOrder.Yxz).Z;
            ctx.Check(Mathf.Abs(steepAngle - (1f / 3f)) < 1e-3f,
                $"a 60° launch turns at h = 1 − |elev|/90 of the authored rate angle={steepAngle:0.000} rad expected=0.333");

            // 4 — `delta` is the rate's own acceleration, integrated rather than dropped: from rest
            // at 2 rad/s², one second is 1 rad.
            var ramped = Pose(Ranged(0f, 0f, 0f, accel: 2f), 1f);
            float rampedAngle = -ramped.GetEuler(EulerOrder.Yxz).Z;
            ctx.Check(Mathf.Abs(rampedAngle - 1f) < 1e-3f,
                $"forward_rotation.delta accelerates the rate angle={rampedAngle:0.000} rad expected=1.000");

            // 5 — the report from the controls. The vector launch form never fills the direction
            // cache the tumble multiplies through, and the parser zeroes the event struct before
            // reading it, so these bodies hold their orientation however large the authored rate is.
            var vec = Pose(Vector(15.708f), 1f);
            ctx.Check(vec.IsEqualApprox(Basis.Identity),
                $"a vector-translation launch does not tumble at all basis={vec}");
            ctx.Note($"flat 1 rad/s = {-slow.GetEuler(EulerOrder.Yxz).Z:0.000} rad/s, the same launch at 60° = {steepAngle:0.000} rad/s, vector form = 0");
        });
    }

    // ---- the MAIN_ROOT_NODE self-reference: a launch onto the def's own anchor -------------------

    // MAIN_ROOT_NODE / INPUT_NODE mean "the node this definition was invoked on", a sentinel and not a
    // name, so a resolver that only matches names finds nothing and drops the event without a word,
    // leaving the whole self-referencing population unlaunched. C5's agyrobus is the whole test: it is
    // the only carrier a player can reach, and having no placement of its own, a launch that re-homes
    // to rest teleports the wreck kilometres away, so the launch must take the node over from the live
    // playback. ⚠ Stay branch-agnostic; randomdestseq opens with IF RandomWeight and either may draw.
    internal static void SelfRefLaunch(TestContext ctx)
    {
        const float Tick = 1f / 60f;
        const float Settle = 2f;    // let the fly script get the bus clear of the origin first

        ctx.WithWorld("C5", collision: true, world =>
        {
            var runtime = world.Runtime;
            var bus = runtime.Destructibles.All.FirstOrDefault(
                i => i.Def.AnimName is { } n && n.Equals("agyrobus", System.StringComparison.OrdinalIgnoreCase));
            ctx.Check(bus != null, $"C5 ships the agyrobus destructible pools={runtime.Destructibles.All.Count}");
            if (bus?.Anchor is not { } anchor)
            {
                return;
            }

            if (bus.Status == DestructibleRegistry.State.Destroyed)
            {
                runtime.ResetDestructible(bus);
            }

            IAnimMotion? DriverOf(Node3D node) => runtime.Motions.Live
                .FirstOrDefault(m => m.Target == node && m.Channel == MotionChannel.Transform);

            var origin = anchor.GlobalPosition;   // where the world PLACED it: the map origin
            for (int i = 0; i < (int)(Settle / Tick); i++)
            {
                runtime.Advance(Tick);
            }

            // The precondition, and the thing that makes the re-home wrong: the bus's whole
            // position is the SI script's doing, and its authored rest is nowhere near it.
            var flown = anchor.GlobalPosition;
            ctx.Check(DriverOf(anchor) is ScriptPlayback,
                $"agbus_fly's SI script drives the bus before the kill driver={DriverOf(anchor)?.GetType().Name ?? "(none)"}");
            ctx.Check((flown - origin).Length() > 100f,
                $"the fly script has carried the bus clear of its authored rest flown={(flown - origin).Length():0} m");

            uint maskWas = runtime.ContactMask;
            runtime.ContactMask = CollisionLayers.World;   // what a real session wires; a suite world does not
            try
            {
                int launchesWas = runtime.Motions.LaunchCount;
                runtime.DamageAt(anchor, bus.MaxHealth + 1f);
                // TWO frames, and the second matters: the death's sequence runs during an Advance after that
                // frame's motions have ticked, so one frame in the launch is registered but has written no pose,
                // and a re-homed launch would look like it had not moved.
                runtime.Advance(Tick);
                runtime.Advance(Tick);

                // 1 — the sentinel resolved. Without it the OBJECT_MOTION dispatches, targets
                // nothing, and registers no body at all: launches+0, which is the only way that
                // failure shows in the log.
                var driver = DriverOf(anchor);
                ctx.Check(driver is MotionRuntime,
                    $"the MAIN_ROOT_NODE launch drives the def's own anchor driver={driver?.GetType().Name ?? "(none)"} launches+{runtime.Motions.LaunchCount - launchesWas}");

                // 2 — and it took over from the playback rather than re-basing on the map origin.
                var launchedAt = anchor.GlobalPosition;
                ctx.Check((launchedAt - flown).Length() < 5f,
                    $"the launch starts where the bus was, not at its authored rest jump={(launchedAt - flown).Length():0.0} m rest={(launchedAt - origin).Length():0} m away");

                for (int i = 0; i < (int)(5f / Tick); i++)
                {
                    runtime.Advance(Tick);
                }

                // 3 — the wreck falls instead of flying on. Both halves show here: the fly script
                // is off the node, and the horizontal travel collapses from the ~60 m/s route to
                // the ballistic drift of a hull with no launch velocity.
                var after = anchor.GlobalPosition;
                ctx.Check(DriverOf(anchor) is not ScriptPlayback,
                    $"agbus_fly no longer drives the wreck driver={DriverOf(anchor)?.GetType().Name ?? "(none)"}");
                float horizontal = new Vector2(after.X - launchedAt.X, after.Z - launchedAt.Z).Length();
                float routeSpeed = new Vector2(flown.X - origin.X, flown.Z - origin.Z).Length() / Settle;
                ctx.Check(horizontal < routeSpeed,   // one second of route, against five of falling
                    $"the wreck falls rather than continuing its route horizontal={horizontal:0.0} m over 5 s (route was {routeSpeed:0} m/s)");
                ctx.Check(after.Y < launchedAt.Y - 10f,
                    $"and it is going down drop={launchedAt.Y - after.Y:0.0} m");
                ctx.Note($"contact tallies: {runtime.Motions.ContactLandings} landed on a collider, {runtime.Motions.ClockEndings} ran their clock out");
            }
            finally
            {
                runtime.ContactMask = maskWas;
            }
        });
    }

    // ---- bounce-terminated launches fly and land ------------------------------------------------

    // An OBJECT_MOTION that omits RUN_TIME and names a BOUNCE_SEQUENCE is the data's "fly until you hit
    // something" idiom: it must solve its own flight time, actually fly, and dispatch that sequence on
    // landing. Kills one refuel* tank through AnimRuntime.DamageAt, the same call a rocket makes, and
    // asserts on the dispatch timeline. Scope and able-to-fail: this module's docs/architecture.md entry.
    // ⚠ Assert a BAND, never an exact time; both launches draw speed and elevation per instance and
    // the draw moves with suite order. ⚠ Do not read the emitter census here; that is emitter-lifetime's.
    internal static void BounceLaunch(TestContext ctx)
    {
        // The two bounce-terminated events of refuel1's healthy def: t = 2*v0y/|g| over the authored speed
        // and elevation ranges, so the support is closed. A landing is detected on a frame boundary, so an
        // observed time can run one tick long.
        // ⚠ The elevation is LINEAR, not spherical (MotionRuntime.RangeLaunchDirection): v0y is
        // speed*elev/90, not speed*sin(elev), which is why these bands sit ~23 % below the sin() ones.
        const float Part3Min = 2.400f, Part3Max = 3.422f;
        const float Part4Min = 2.178f, Part4Max = 4.000f;
        const float Tick = 1f / 60f;
        const string chapter = "C1";   // the only chapter shipping refuel* (5 defs)
        const string lateBounce = "ObjectMotion(bounce landed after its instance ended)";

        // The bands are derived from the AUTHORED speed and elevation ranges, because what this suite
        // asserts is the decode: a bounce-terminated body flies the parabola the data describes. With the
        // old global launch tune gone, the authored arc IS what flies and the bands apply directly.
        ctx.WithWorld(chapter, collision: false, world =>
        {
            var runtime = world.Runtime;
            DestructibleRegistry.Instance? tank = null;
            foreach (var inst in runtime.Destructibles.All)
            {
                if (inst.Def.AnimName is { } name
                    && name.StartsWith("refuel", System.StringComparison.OrdinalIgnoreCase))
                {
                    tank = inst;
                    break;
                }
            }
            ctx.Check(tank != null, $"chapter ships a refuel tank chapter={chapter}");
            if (tank == null)
            {
                return;
            }

            // A shared cached world reaches this suite already swept by damage-hd, and DamageAt is
            // a no-op on something already destroyed — heal first so the death actually runs.
            if (tank.Status == DestructibleRegistry.State.Destroyed)
            {
                runtime.ResetDestructible(tank);
            }

            int Missed() =>
                runtime.UnhandledEventCounts.TryGetValue(lateBounce, out int n) ? n : 0;

            var timeline = new List<(float T, string Seq, string Kind, string? Name)>();
            // Only the tank's OWN dispatches: on a shared cached world another suite's earlier kill can still
            // be running its authored death, and a runtime-wide read here would count that neighbour's launch
            // against this one (INSTR-10).
            var tankDef = tank.Def;
            float clock = 0f;
            var previous = runtime.OnEventDispatched;
            int missedBefore = Missed();
            bool everOwed = false;
            try
            {
                runtime.OnEventDispatched = d =>
                {
                    if (d.Def == tankDef)
                    {
                        timeline.Add((clock, d.Sequence, d.EventKind, d.EventName));
                    }
                };
                runtime.DamageAt(tank.Anchor, tank.MaxHealth + 1f);
                for (int i = 0; i < 600; i++)   // 10 s, comfortably past the 4.0 s worst-case flight
                {
                    clock += Tick;
                    runtime.Advance(Tick);
                    everOwed |= runtime.Motions.OwesBounce(tank.Def, tank.Anchor);
                }
            }
            finally
            {
                runtime.OnEventDispatched = previous;
            }

            ctx.Check(everOwed, $"a launched {tank.Def.AnimName} piece owed its BOUNCE_SEQUENCE while in flight");
            ctx.Check(!runtime.Motions.OwesBounce(tank.Def, tank.Anchor),
                $"nothing is still owed once every piece has landed");

            // part1/part2 with an authored RUN_TIME plus part3/part4 solved, and all four are this def's own
            // ObjectMotion events. Counted from the def-scoped timeline rather than the runtime-wide tally,
            // for the neighbour launch a shared world can bleed into this window.
            int launched = timeline.Count(e => e.Kind == "ObjectMotion");
            ctx.Same(4, launched, $"ballistic launches on one {tank.Def.AnimName} death");

            float LaunchAt(string node) => timeline
                .Where(e => e.Kind == "ObjectMotion" && e.Name == node)
                .Select(e => e.T).DefaultIfEmpty(-1f).First();
            float BounceAt(string seq) => timeline
                .Where(e => e.Seq == seq)
                .Select(e => e.T).DefaultIfEmpty(-1f).First();

            CheckFlight(ctx, "part3", "sparkout3", LaunchAt("part3"), BounceAt("sparkout3"),
                Part3Min, Part3Max + Tick);
            CheckFlight(ctx, "part4", "sparkout4", LaunchAt("part4"), BounceAt("sparkout4"),
                Part4Min, Part4Max + Tick);

            // The bounce sequence is what deactivates the flying piece and pops its fireball —
            // both of its events must run, not just the first.
            ctx.Same(2, timeline.Count(e => e.Seq == "sparkout3"), $"sparkout3 events dispatched");
            ctx.Same(2, timeline.Count(e => e.Seq == "sparkout4"), $"sparkout4 events dispatched");
            ctx.Same(0, Missed() - missedBefore, $"refuel bounces landing after their instance ended");

            // A landing must reach a LIVE instance and the launch is the last event of its sequence, so nothing
            // but the retirement hold keeps one reachable; refuel* cannot show that but the yard buildings can.
            // Grouped by ANCHOR, since a wildcard def and its compiled twin both bind these seven nodes.
            var yard = runtime.Destructibles.All
                .Where(i => i.Def.AnimName is { } n
                            && n.StartsWith("m_build", System.StringComparison.OrdinalIgnoreCase))
                .GroupBy(i => i.Anchor)
                .Select(g => g.First())
                .ToList();
            ctx.Same(7, yard.Count, $"chapter ships the yard buildings chapter={chapter}");

            timeline.Clear();
            clock = 0f;
            missedBefore = Missed();
            // Same def scoping as the refuel hook: the yard buildings' own sparkout3/sparkout4
            // sequence names repeat on the refuel def, so an unscoped read would also count a
            // neighbour's late bounce.
            var yardDefs = new HashSet<AnimDefinition>(yard.Select(b => b.Def));
            try
            {
                runtime.OnEventDispatched = d =>
                {
                    if (yardDefs.Contains(d.Def))
                    {
                        timeline.Add((clock, d.Sequence, d.EventKind, d.EventName));
                    }
                };
                foreach (var b in yard)
                {
                    if (b.Status == DestructibleRegistry.State.Destroyed)
                    {
                        runtime.ResetDestructible(b);
                    }
                    runtime.DamageAt(b.Anchor, b.MaxHealth + 1f);
                }
                for (int i = 0; i < 600; i++)
                {
                    clock += Tick;
                    runtime.Advance(Tick);
                }
            }
            finally
            {
                runtime.OnEventDispatched = previous;
            }

            ctx.Same(0, Missed() - missedBefore, $"yard bounces landing after their instance ended");
            ctx.Note($"the two zero-miss checks are invariants this seed does not discriminate: removing A4's retirement hold leaves both green here. the able-to-fail control is a SEED SWEEP of the --destroy=m_build probe, not this suite and not one run of that probe — with the hold removed it misses on 4 of 10 seeds and is clean on seed 1 (INSTR-6)");
            ctx.Same(
                yard.Count * 4,   // sparkout3 + sparkout4, two events each, per building
                timeline.Count(e => e.Seq == "sparkout3" || e.Seq == "sparkout4"),
                $"yard sparkout events dispatched");
        });
    }

    // One bounce-terminated piece: it must launch, and its bounce sequence must fire a
    // flight time later that lands inside the band its authored `translation_range` allows.
    // A missing launch and a missing landing are reported apart — they are different bugs.
    internal static void CheckFlight(
        TestContext ctx, string node, string seq, float launchAt, float bounceAt, float min, float max)
    {
        ctx.Check(launchAt >= 0f, $"{node}'s bounce-terminated OBJECT_MOTION dispatches t={launchAt:0.000}");
        ctx.Check(bounceAt >= 0f, $"{seq} dispatches on landing t={bounceAt:0.000}");
        if (launchAt < 0f || bounceAt < 0f)
        {
            return;
        }
        float flight = bounceAt - launchAt;
        ctx.Note($"{node} solved flight {flight:0.000} s (band {min:0.000}…{max:0.000})");
        ctx.Check(flight >= min && flight <= max,
            $"{node}'s solved flight is inside its authored band flight={flight:0.000} band={min:0.000}…{max:0.000}");
    }

    // ---- a launch that names neither RUN_TIME nor BOUNCE_SEQUENCE -------------------------------

    // The third launch shape: OBJECT_MOTION events that omit RUN_TIME and BOUNCE_SEQUENCE and follow
    // the launch with the flying piece's own null-start ACTIVE_STATE 0. A flight solve gated on a
    // bounce being named declines all of them, so the piece is hidden before it moves. The zeppelin
    // cannon's eight parts are the reachable repro (analysis/bl-257-nulled-launch/).
    // ⚠ Measure the GAP between each part's launch and its own deactivation, not a mesh count, which
    // reads 8/8 either way. ⚠ Assert a BAND; elevation and speed are per-instance draws, and linear.
    internal static void NulledLaunch(TestContext ctx)
    {
        // extracted/*/cam_anim/zep_can_dstry1-dblcannon_flying_parts.json: eight parts, each
        // xz −135…135°, y 10…70°, initial 17…25 m/s, gravity −9.8, no run_time, no bounce_sequence.
        //   min  2·17·(10/90)/9.8 = 0.385 s       max  2·25·(70/90)/9.8 = 3.968 s
        // A dispatch is observed on a frame boundary, so the gap can run one tick long.
        const float FlightMin = 0.385f, FlightMax = 3.968f;
        const float Tick = 1f / 60f;
        const string root = "zep_ng_dstry1_flt";   // the staged copy's node name, '.' sanitised

        // Same as bounce-launch: the band is the AUTHORED support, which is now simply what flies.
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            EffectStageSuiteHelper.WithEffectStage(ctx, world, "biggun_flying_parts", new[] { "zep_ng_dstry1.flt" },
                (stage, runtime, point) =>
            {
                var parts = new Dictionary<string, Node3D>(System.StringComparer.Ordinal);
                CollectNamed(stage, parts);
                ctx.Same(8, parts.Count, $"the staged wreck carries its eight parts");

                var launched = new Dictionary<string, float>(System.StringComparer.Ordinal);
                var switchedOff = new Dictionary<string, float>(System.StringComparer.Ordinal);
                var moved = new Dictionary<string, float>(System.StringComparer.Ordinal);
                var restAt = new Dictionary<string, Vector3>(System.StringComparer.Ordinal);
                bool bounceOwed = false;
                float clock = 0f;
                var previous = runtime.OnEventDispatched;
                try
                {
                    runtime.OnEventDispatched = d =>
                    {
                        if (d.EventName is not { } name || !parts.ContainsKey(name))
                        {
                            return;
                        }
                        if (d.EventKind == "ObjectMotion" && !launched.ContainsKey(name))
                        {
                            launched[name] = clock;
                            restAt[name] = parts[name].Position;
                        }
                        else if (d.EventKind == "ObjectActiveState" && !switchedOff.ContainsKey(name))
                        {
                            switchedOff[name] = clock;
                        }
                        // Nothing here names a bounce, so nothing may owe one — the widened gate
                        // must solve the flight WITHOUT arming a landing sequence that does not
                        // exist — the over-generalization to watch for.
                        bounceOwed |= runtime.Motions.OwesBounce(d.Def, d.Anchor);
                    };
                    runtime.PlayEffectAt("biggun_flying_parts", point);
                    for (int i = 0; i < 360; i++)   // 6 s, past the 3.97 s worst-case flight
                    {
                        clock += Tick;
                        runtime.Advance(Tick);
                        foreach (var (name, node) in parts)
                        {
                            if (restAt.TryGetValue(name, out var rest))
                            {
                                float d = node.Position.DistanceTo(rest);
                                moved[name] = Mathf.Max(moved.GetValueOrDefault(name), d);
                            }
                        }
                    }
                }
                finally
                {
                    runtime.OnEventDispatched = previous;
                }

                ctx.Same(8, launched.Count, $"parts launched by one {root} destruction");
                ctx.Check(!bounceOwed, $"no part owes a BOUNCE_SEQUENCE — the data names none");
                foreach (var name in parts.Keys.OrderBy(n => n, System.StringComparer.Ordinal))
                {
                    if (!launched.TryGetValue(name, out float at))
                    {
                        ctx.Check(false, $"{name} never launched");
                        continue;
                    }
                    ctx.Check(switchedOff.TryGetValue(name, out float off),
                        $"{name}'s own null-start deactivation dispatches");
                    if (!switchedOff.ContainsKey(name))
                    {
                        continue;
                    }
                    float flight = off - at;
                    ctx.Note($"{name} flew {flight:0.000} s and travelled {moved.GetValueOrDefault(name):0.0} m (band {FlightMin:0.000}…{FlightMax:0.000})");
                    ctx.Check(flight >= FlightMin && flight <= FlightMax + Tick,
                        $"{name} flies its solved parabola before it is switched off flight={flight:0.000} band={FlightMin:0.000}…{FlightMax:0.000}");
                    ctx.Check(moved.GetValueOrDefault(name) > 1f,
                        $"{name} actually left its rest pose travelled={moved.GetValueOrDefault(name):0.0} m");
                }
            });
        });
    }

    // The `part1`…`part8` the flying-parts def drives, by node name, from
    // anywhere under the staged template. Named lookup rather than a child index: the wreck is
    // real gamez geometry and its parts sit at whatever depth it authors them.
    internal static void CollectNamed(Node node, Dictionary<string, Node3D> into)
    {
        if (node is Node3D n3 && n3.Name.ToString().StartsWith("part", System.StringComparison.Ordinal))
        {
            into[n3.Name.ToString()] = n3;
        }
        foreach (var child in node.GetChildren())
        {
            CollectNamed(child, into);
        }
    }

    // ---- node lab tree rows must follow live Visible --------------------------------------------

    // Hides a node through the lab's own Hide action, then re-shows it through a real RESET_STATE def,
    // the same path a world animation uses, and checks the tree row both times, never through the
    // button, only through Node3D.Visible. A def re-showing a node the user hid is correct behaviour,
    // so the row must follow it. ⚠ Use a chapter other than TestContext.Chapter: that one is cached
    // and shared with damage-hd, so the candidate search would otherwise depend on suite run order.
    internal static void NodeLabVisibility(TestContext ctx)
    {
        ctx.WithWorld("C2", collision: false, world =>
        {
            DestructibleRegistry.Instance? chosen = null;
            Node3D? healthy = null;
            foreach (var inst in world.Runtime.Destructibles.All)
            {
                if (inst.Def.ResetState == null)
                {
                    continue;
                }
                if (FindVariant(inst.Anchor, "healthy") is { Visible: true } found)
                {
                    chosen = inst;
                    healthy = found;
                    break;
                }
            }
            ctx.Check(chosen != null,
                $"chapter has a destructible with a visible 'healthy' variant and a RESET_STATE chapter={ctx.Chapter}");
            if (chosen == null || healthy == null)
            {
                return;
            }

            var selection = new SelectionService(world.Session.Root, ctx.Camera);
            var lab = new NodeLab(world.Session.Root, selection, world.Runtime, world.Session.Program,
                world.Session.Builder.Scene, collisionBuilt: false);
            ctx.Host.AddChild(selection);
            ctx.Host.AddChild(lab);
            try
            {
                lab.Toggle();
                selection.Select(healthy);
                lab.RevealSelectionForTest();
                lab.ToggleHide();
                ctx.Check(!healthy.Visible, $"ToggleHide actually hides the node node={SelectionService.NameOf(healthy)}");

                var hidden = lab.RowStateForTest(healthy);
                ctx.Check(hidden is { Dim: true } row1 && row1.Text.Contains("(hidden)"),
                    $"row reads hidden right after the button node={SelectionService.NameOf(healthy)} text={hidden?.Text} dim={hidden?.Dim}");

                // The re-show is a real def, not the lab: RESET_STATE's OBJECT_ACTIVE_STATE events
                // are what an animation uses to bring the healthy subtree back, with no button
                // press and nothing telling the lab this node exists.
                world.Runtime.ResetDestructible(chosen);
                ctx.Check(healthy.Visible, $"RESET_STATE re-shows the node node={SelectionService.NameOf(healthy)}");

                lab.RefreshStatusForTest();
                var shown = lab.RowStateForTest(healthy);
                ctx.Check(shown is { Dim: false } row2 && !row2.Text.Contains("(hidden)"),
                    $"row follows the def's re-show without user input node={SelectionService.NameOf(healthy)} text={shown?.Text} dim={shown?.Dim}");
            }
            finally
            {
                lab.Free();
                selection.Free();
            }
        });
    }

    // The first descendant (inclusive) whose cs_name contains the tag — "healthy"/"destroyed" name
    // their variant subtrees exactly as CountVariants (Probes.cs) scans for, but this returns the
    // node itself rather than a count.
    internal static Node3D? FindVariant(Node3D node, string tag)
    {
        string cs = node.HasMeta(AnimRuntime.NameMeta) ? node.GetMeta(AnimRuntime.NameMeta).AsString() : node.Name.ToString();
        if (node.HasMeta(AnimRuntime.NameMeta) && cs.Contains(tag, System.StringComparison.OrdinalIgnoreCase))
        {
            return node;
        }
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D n3d && FindVariant(n3d, tag) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    // Asserts every pane of a 2-, 3- and 4-player rig is a 3D audio listener. The check reads trivial
    // and is not: a fresh SubViewport is NOT a listener, and in splitscreen the main camera stands down,
    // which takes it out of the World3D listener set. With no listener-enabled viewport left,
    // AudioStreamPlayer3D finds no listener in range, clears its bus volumes, and every 3D emitter in
    // the world is silent, with nothing logged or counted to say so.
    internal static void SplitscreenListeners(TestContext ctx)
    {
        var main = ctx.Host.GetViewport();
        ctx.Check(main.AudioListenerEnable3D,
            $"the main viewport is a 3D audio listener (the untouched 1P path)");

        // The default the rig has to override, proved rather than assumed.
        using (var bare = new SubViewport())
        {
            ctx.Check(!bare.AudioListenerEnable3D,
                $"a fresh SubViewport is NOT an audio listener, so each pane must set it");
        }

        for (int players = 2; players <= SplitScreen.MaxPlayers; players++)
        {
            var split = SplitScreen.Build(players, main);
            ctx.Host.AddChild(split);
            try
            {
                ctx.Same(players, split.Views.Count, $"{players}P panes");
                foreach (var view in split.Views)
                {
                    ctx.Check(view.AudioListenerEnable3D,
                        $"{players}P pane {view.Name} is a 3D audio listener");
                }
            }
            finally
            {
                ctx.Host.RemoveChild(split);
                split.Free();
            }
        }
    }

    // The B13 rule: WorldLights.Commit fades and ranks
    // each light against the NEAREST of every pane's camera, not a single position. Driven
    // straight against a real WorldLights instance with synthetic positions —
    // there is no per-player placement flag to give two scripted panes independent spots (the
    // same CLI gap B11/B12 hit), so the rule is pinned here instead and the visual verdict is
    // PT-52's, alongside B11/B12's own owed at-the-controls check.
    internal static void WorldLightsNearestViewer(TestContext ctx)
    {
        var p1 = Vector3.Zero;
        // Well past FadeEnd (1500 m) from P1 alone, but 100 m from a second viewer.
        var farFromP1 = new Vector3(0f, 0f, -2000f);
        var p2 = new Vector3(0f, 0f, -2100f);

        var lights = new WorldLights();

        lights.Begin();
        lights.Add(farFromP1, Colors.White, 1f, 10f);
        lights.Commit(new[] { p1 });
        ctx.Check(!lights.CommittedPositions.Contains(farFromP1),
            $"ABLE-TO-FAIL CONTROL: 2000 m from a lone P1 is past the 1500 m FadeEnd, so the light drops");

        lights.Begin();
        lights.Add(farFromP1, Colors.White, 1f, 10f);
        lights.Commit(new[] { p1, p2 });
        ctx.Check(lights.CommittedPositions.Contains(farFromP1),
            $"the same light stays committed once a second viewer sits 100 m from it — nearest, not P1 alone");

        // The MaxActive budget's Significance rank must answer to the same nearest-viewer rule, not just
        // the fade: the 16-slot budget is packed with filler lights strictly farther from P1 than besideP2
        // sits from P2, so a correct nearest-viewer rank keeps besideP2 and cuts the farthest filler.
        var besideP2 = new Vector3(0f, 5f, -2100f);
        lights.Begin();
        for (int i = 0; i < WorldLights.MaxActive; i++)
            lights.Add(new Vector3(5f + i, 0f, -5f), Colors.White, 1f, 10f);
        lights.Add(besideP2, Colors.White, 1f, 10f);
        lights.Commit(new[] { p1 });
        ctx.Check(lights.CommittedPositions.Count == WorldLights.MaxActive
                  && !lights.CommittedPositions.Contains(besideP2),
            $"ABLE-TO-FAIL CONTROL: against P1 alone the 17th light (right beside where P2 will be) is past FadeEnd and never reaches the budget");

        lights.Begin();
        for (int i = 0; i < WorldLights.MaxActive; i++)
            lights.Add(new Vector3(5f + i, 0f, -5f), Colors.White, 1f, 10f);
        lights.Add(besideP2, Colors.White, 1f, 10f);
        lights.Commit(new[] { p1, p2 });
        ctx.Check(lights.CommittedPositions.Count == WorldLights.MaxActive
                  && lights.CommittedPositions.Contains(besideP2),
            $"with P2 present the same light is nearest to a viewer and outranks the farthest filler for a slot in the budget");

        // Single viewer must read exactly as it did before this item — the goldens' own invariant.
        var nearP1 = new Vector3(0f, 0f, -5f);
        lights.Begin();
        lights.Add(nearP1, Colors.White, 1f, 10f);
        lights.Commit(new[] { p1 });
        ctx.Check(lights.CommittedPositions.Count == 1 && lights.CommittedPositions.Contains(nearP1),
            $"one viewer (single player) is the unchanged, pre-B13 rule");
    }
}
