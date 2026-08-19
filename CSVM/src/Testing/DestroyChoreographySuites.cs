using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Effects;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Mech3.Anim;
using CSVM.Session;
using CSVM.Utils;
using Godot;

using static CSVM.Testing.SuiteConstants;
namespace CSVM.Testing;

/// <summary>The death choreography: the CALLBACK codes a destroy def authors, the per-entry
/// injure staging behind them, and the two anim slots a killed aircraft plays on its way
/// down. Its own module rather than part of the animation/effects one because these six
/// suites drive whole aircraft through a kill, not a staged template.</summary>
internal static class DestroyChoreographySuites
{
    // ---- CALLBACK: the two vehicle-death codes ---------------------------------------------------

    // A destroy def's CALLBACK events reach the seams the rig supplies: 16 hands the instance the
    // wreck's velocity, 15 stops the damage stages. Both are asserted on shipped defs — player-player
    // (which authors 3, then 16 and 15 through destroy_craft) and the AI's fury-fury.
    // ⚠ Assert the unknown code and the unwired control too. A handler that acted on every code
    // would pass the two arms and still be inventing behaviour, and one wired to nothing at all
    // leaves the wreck motionless exactly as it did before this existed.
    internal static void CallbackEvents(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            CallbackSeamsReceiveTheirCodes(ctx, world);
            CallbackCodeZeroIsNeverAuthored(ctx, world);
        });
    }

    // ---- one anchor, several authored calls, one copy each -------------------------------------

    // The other half of the pool's keying: a root CALLED REPEATEDLY from one anchor. The slot is
    // claimed per (root, anchor, authored event), so pdpanel7's four gimmeflakes calls at pdp7 take
    // four copies and the Balmoral's three chuteman calls at `destroyed` become three parachutes.
    // Keyed per anchor alone, calls two to four resolve to the first call's copy and the live guard
    // drops them, which is the "one chute where the data asks for three" symptom.
    internal static void RepeatCallSlots(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var stage = new Node3D { Name = "RepeatCallStage" };
            var pdp7 = WorldAndToolSuites.PoolAnchorNode("pdp7", new Vector3(-10, 0, 0));
            stage.AddChild(pdp7);
            var copies = new List<Node3D>();
            for (int slot = 0; slot < 4; slot++)
            {
                var pool = new Node3D { Name = $"pool{slot}" };
                pool.SetMeta(AnimRuntime.PoolSlotMeta, slot);
                stage.AddChild(pool);
                Session.WorldEffectsFactory.BuildEffectStage(world.Gamez,
                    world.Session.Builder.Scene, pool, new[] { "planeflakes" });
                foreach (var child in pool.GetChildren())
                {
                    if (child is Node3D copy)
                    {
                        copies.Add(copy);
                    }
                }
            }

            ctx.Check(copies.Count == 4, $"four planeflakes copies staged ({copies.Count})");
            var runtime = new AnimRuntime(
                Session.WorldEffectsFactory.NewCrashTemplateStage())
            {
                AutoStart = false,
                ManualAdvance = true,
                SoundHandledElsewhere = true,
                EmitterFactory = new CountingEmitterFactory(),
                NameResolveFallback = true,
            };
            ctx.Host.AddChild(stage);
            ctx.Host.AddChild(runtime);
            try
            {
                runtime.Bind(stage, world.Session.Program.Subset(new[] { "pdpanel7" }));
                // The authored sites: one call at pdp7 + 2 m, three at pdp7 - 2 m, the last three
                // staggered 0.2 / 0.3 / 0.4 s apart. Debris flies off after placement, so the set
                // is collected AS each call lands rather than sampled at the end.
                var siteA = pdp7.GlobalTransform.Origin + new Vector3(2, 0, 0);
                var siteB = pdp7.GlobalTransform.Origin + new Vector3(-2, 0, 0);
                var taken = new HashSet<Node3D>();
                runtime.Play("pdpanel7", stage, applyReset: false);
                for (int i = 0; i < 90; i++)
                {
                    runtime.Advance(1f / 60f);
                    foreach (var copy in copies)
                    {
                        var at = copy.GlobalTransform.Origin;
                        if (at.DistanceTo(siteA) < 0.5f || at.DistanceTo(siteB) < 0.5f)
                            taken.Add(copy);
                    }
                }

                ctx.Check(taken.Count == 4,
                    $"pdpanel7's four authored gimmeflakes calls took four different copies ({taken.Count})");
                ctx.Check(runtime.PoolRecycles == 0,
                    $"no pool wrap for four call sites over four copies ({runtime.PoolRecycles})");
                // Sticky per call site, as it is per anchor: a second tear reclaims the copies its
                // own four events already hold rather than four more.
                runtime.Play("pdpanel7", stage, applyReset: false);
                for (int i = 0; i < 90; i++)
                {
                    runtime.Advance(1f / 60f);
                }

                ctx.Check(runtime.PoolRecycles == 0,
                    $"a second tear reclaims its own copies ({runtime.PoolRecycles} wrap(s))");
            }
            finally
            {
                runtime.Free();
                stage.Free();
            }
        });
    }

    // ---- the injure ladder stages per ENTRY, and retracts on the upward crossing ---------------

    // fury's AI ladder names random_remote_damage at six of its seven thresholds, so a latch keyed
    // on the anim name plays five of them never. Three halves: the count over the real ladder, the
    // retraction a repair makes (cleared on the upward crossing alone, never by staying below), and
    // the per-(part, entry) keying, which no shipped def exercises — see the synthetic ladder below.
    internal static void DamageStageSlots(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var stats = PlaneStats.LoadForAi(ctx.ZrdrPath, "player_fury");
        ctx.Check(stats.VehicleInjureAnims.Count == 7,
            $"fury's AI ladder carries {stats.VehicleInjureAnims.Count} entries (want 7)");

        var root = new Node3D { Name = "fury_root" };
        ctx.Host.AddChild(root);
        try
        {
            var visuals = new DamageVisuals(System.Array.Empty<Node3D>(), root, stats);
            ctx.Check(!visuals.PairsPanels,
                $"fury's AI data names no pdpanel stage, so nothing is paired and nothing warns");

            // one step per band, so each threshold is crossed on its own
            foreach (float frac in new[] { 0.99f, 0.9f, 0.7f, 0.55f, 0.47f, 0.42f, 0.3f, 0.2f })
                visuals.OnHullDamage(frac);
            int repeats = visuals.StagedEntryCount("random_remote_damage");
            ctx.Check(repeats == 6,
                $"the whole ladder walked down: {repeats} random_remote_damage entries staged (want 6)");
            ctx.Check(visuals.StagedEntryCount("pfsmoketrail") == 1,
                $"…and the one pfsmoketrail entry at 0.40 staged with them");

            // repaired to half: the three entries under 0.5 retract, the four at or above hold
            visuals.OnHullDamage(0.5f);
            ctx.Check(visuals.StagedEntryCount("random_remote_damage") == 4
                      && visuals.StagedEntryCount("pfsmoketrail") == 0,
                $"repaired to 50%: {visuals.StagedEntryCount("random_remote_damage")} random_remote_damage and {visuals.StagedEntryCount("pfsmoketrail")} pfsmoketrail entries still staged (want 4 and 0)");
            visuals.OnHullDamage(0.2f);
            ctx.Check(visuals.StagedEntryCount("random_remote_damage") == 6
                      && visuals.StagedEntryCount("pfsmoketrail") == 1,
                $"…and every retracted entry fires again on the next descent");
        }
        finally
        {
            root.Free();
        }

        // Per-(part, entry) keying. A census of all 22 shipped defs carrying destroyable_parts found
        // no anim authored on two zones of one def, so the shared-entry case is driven from a
        // synthetic ladder: four zones naming pdpanel1. The pairing warning fires here by design.
        var multi = new PlaneStats();
        foreach (string zone in new[] { "nose", "tail", "leftwing", "rightwing" })
            multi.DestroyableParts.Add(new DestroyablePart { Name = zone, InjureAnims = { (0.5f, "pdpanel1") } });

        var zoneRoot = new Node3D { Name = "zoned_root" };
        ctx.Host.AddChild(zoneRoot);
        try
        {
            var visuals = new DamageVisuals(System.Array.Empty<Node3D>(), zoneRoot, multi);
            int starts = 0, stops = 0;
            visuals.DamageEffectSink = _ => starts++;
            visuals.DamageEffectStopOne = _ => stops++;
            foreach (var part in multi.DestroyableParts)
                visuals.OnPartDamage(part.Name, 0.4f);
            ctx.Check(starts == 4 && visuals.StagedEntryCount("pdpanel1") == 4,
                $"one entry authored on four zones started {starts} times and holds {visuals.StagedEntryCount("pdpanel1")} slots (want 4 and 4)");

            foreach (var part in multi.DestroyableParts)
                visuals.OnPartDamage(part.Name, 0.3f);
            ctx.Check(starts == 4, $"…and staying below the threshold started nothing new ({starts} total)");

            visuals.OnPartDamage("nose", 1f);
            ctx.Check(visuals.StagedEntryCount("pdpanel1") == 3 && stops == 0,
                $"one zone repaired clears its own slot and stops nothing — three zones still hold the anim");
            foreach (var part in multi.DestroyableParts)
                visuals.OnPartDamage(part.Name, 1f);
            ctx.Check(visuals.StagedEntryCount("pdpanel1") == 0 && stops == 1,
                $"…and the last one to retract stops the stage exactly once (stops={stops})");

            visuals.OnPartDamage("nose", 0.4f);
            ctx.Check(starts == 5, $"a repaired zone re-crossing fires again ({starts} starts)");
        }
        finally
        {
            zoneRoot.Free();
        }

        // A2's other half: a player airframe DOES name pdpanel stages, so pairing (and its
        // missing-data alarm) stays armed on that path.
        var player = PlaneStats.Load(ctx.ZrdrPath, "player_fury");
        var playerRoot = new Node3D { Name = "player_root" };
        ctx.Host.AddChild(playerRoot);
        try
        {
            var visuals = new DamageVisuals(System.Array.Empty<Node3D>(), playerRoot, player);
            ctx.Check(visuals.PairsPanels,
                $"the player fury names pdpanel stages, so panel pairing still runs for it");
        }
        finally
        {
            playerRoot.Free();
        }
    }

    // ---- an AI plane's ladder reaching the rig runtime, through the real spawner ----------------

    // The seam the tree-free slot tests cannot reach: FlightRoster building an aircraft whose
    // hull then falls, with the stages counted off the RUNTIME's own instance hook rather than off
    // the sink the wiring under test installs. Phase 1 (the DamageVisuals) and phase 2 (the sink and
    // stops, wired inside BuildFlightCrashRuntime) live in different files, so "the object exists"
    // and "it plays into a runtime" are separate failures and asserted separately.
    internal static void AiDamageStages(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
            var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, world.Chapter));
            ProjectilePool? pool = null;
            FlightController? ai = null;
            FlightController? bhawk = null;
            try
            {
                var live = new ProjectilePool(textures, null, null);
                pool = live;
                ctx.Host.AddChild(live);
                var spec = SessionSpec.Parse(System.Array.Empty<string>());
                var liveries = new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof"));
                var factory = new Session.WorldEffectsFactory(spec, ctx.Host, () => Vector3.Zero);
                var inputs = new HumanFlightAdapter.Inputs
                {
                    PlanesGamez = planesGamez,
                    StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
                    AiStatsFor = plane => PlaneStats.LoadForAi(ctx.ZrdrPath, plane),
                    RigCount = 0,
                    PaintRng = new RandomNumberGenerator(),
                    ZrdrPath = ctx.ZrdrPath,
                    StockLoadouts = StockLoadouts.Load(),
                    WeaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null),
                    Textures = textures,
                    Projectiles = live,
                    Shakes = ShakeDefs.Load(ctx.ZrdrPath),
                    Gamez = world.Gamez,
                    WorldScene = world.Session.Builder.Scene,
                    CrashProgram = world.Session.Program,
                };
                var spawner = new FlightRoster(spec, liveries, factory, ctx.Host, inputs);
                var start = new Vector3(0f, 500f, 0f);
                ai = spawner.SpawnAi(new AiSpawn("player_fury", start, start + Vector3.Forward,
                    AiPilot.HoldingCourse(start, start + Vector3.Forward)));

                ctx.Check(ai.Visuals != null,
                    $"the spawner built the AI Fury's DamageVisuals (phase 1)");
                ctx.Check(ai.Visuals?.DamageEffectSink != null && ai.Visuals?.DamageEffectStop != null
                          && ai.Visuals?.DamageEffectStopOne != null,
                    $"…and the crash-runtime build wired its sink and both stops (phase 2)");
                if (ai.Visuals is not { } visuals || ai.CrashRuntime is not { } rig
                    || ai.Damage is not { } damage)
                {
                    return;
                }

                var starts = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
                var stops = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
                var anchors = new List<Node3D?>();
                rig.OnInstanceStarted += (def, anchor) =>
                {
                    string n = def.AnimName ?? def.Name ?? "";
                    starts[n] = starts.TryGetValue(n, out var c) ? c + 1 : 1;
                    if (Session.EffectCatalogue.AiDamageStageAnims.Contains(n, System.StringComparer.OrdinalIgnoreCase))
                        anchors.Add(anchor);
                };
                rig.OnInstanceFinished += (def, _) =>
                {
                    string n = def.AnimName ?? def.Name ?? "";
                    stops[n] = stops.TryGetValue(n, out var c) ? c + 1 : 1;
                };

                // Armour off first, in one spend, then the ladder walked down one band at a time
                // through TakeCollisionHit — the production take-hit entry, not the visuals API.
                ai.TakeCollisionHit(damage.WholeArmor, 0f, ai.GlobalPosition, 0);
                foreach (float frac in new[] { 0.9f, 0.7f, 0.55f, 0.47f, 0.42f, 0.3f, 0.2f })
                    SpendHullTo(ai, damage, frac);
                ctx.Check(damage.SummaryHealthFraction is > 0.19f and < 0.21f,
                    $"the AI Fury's hull walked down to {damage.SummaryHealthFraction * 100f:0}% through the take-hit path");
                ctx.Check(Count(starts, "random_remote_damage") == 6 && Count(starts, "pfsmoketrail") == 1,
                    $"the rig runtime STARTED {Count(starts, "random_remote_damage")} random_remote_damage and {Count(starts, "pfsmoketrail")} pfsmoketrail instance(s) (want 6 and 1)");
                ctx.Check(visuals.StagedEntryCount("random_remote_damage") == 6
                          && visuals.StagedEntryCount("pfsmoketrail") == 1,
                    $"…and the ladder holds one slot per start: {visuals.StagedEntryCount("random_remote_damage")} and {visuals.StagedEntryCount("pfsmoketrail")}");
                int offPlane = anchors.Count(a => a == null || (a != ai && !ai.IsAncestorOf(a)));
                ctx.Check(anchors.Count == 7 && offPlane == 0,
                    $"every one of the {anchors.Count} stage instances anchored inside THIS aircraft ({offPlane} elsewhere)");

                // The repair: the whole ladder retracts, and each stage is stopped exactly once —
                // once per ANIM, not once per entry, or five of fury's six would stop nothing.
                stops.Clear();
                visuals.OnHullDamage(1f);
                ctx.Check(Count(stops, "random_remote_damage") == 1 && Count(stops, "pfsmoketrail") == 1,
                    $"a full repair tore down {Count(stops, "random_remote_damage")} random_remote_damage and {Count(stops, "pfsmoketrail")} pfsmoketrail instance(s) (want 1 and 1)");
                ctx.Check(visuals.StagedEntryCount("random_remote_damage") == 0,
                    $"…leaving no entry staged");
                foreach (float frac in new[] { 0.9f, 0.7f, 0.55f, 0.47f, 0.42f, 0.3f, 0.2f })
                    visuals.OnHullDamage(frac);
                ctx.Check(Count(starts, "random_remote_damage") == 12 && Count(starts, "pfsmoketrail") == 2,
                    $"…and the next descent fires all seven again ({Count(starts, "random_remote_damage")} and {Count(starts, "pfsmoketrail")} starts in total)");

                // C16's anchor census as a live A/B: the stage defs are authored against the
                // Devastator, and the Bloodhawk spells its elevators l_elev/r_elev. Same rig, same
                // pinned dice, one airframe apart — so the warned set is about the airframe alone.
                ctx.Check(rig.AnchorWarnLabel == "player_fury" && rig.AnchorWarnAnimNames != null
                          && rig.AnchorWarnAnimNames.Contains("random_remote_damage"),
                    $"the AI rig is armed to name an unresolved stage anchor (label={rig.AnchorWarnLabel ?? "-"})");
                CascadeUntilExhausted(rig, ai.PlaneModel);
                ctx.Check(rig.UnresolvedStageAnchors.Count == 0,
                    $"the Fury carries all five stage anchors: {rig.UnresolvedStageAnchors.Count} unresolved [{string.Join(", ", rig.UnresolvedStageAnchors)}]");

                bhawk = spawner.SpawnAi(new AiSpawn("player_bhawk", start + new Vector3(3000f, 0f, 0f),
                    start + new Vector3(3000f, 0f, -1f),
                    AiPilot.HoldingCourse(start + new Vector3(3000f, 0f, 0f), start + new Vector3(3000f, 0f, -1f))));
                if (bhawk.CrashRuntime is { } bhawkRig)
                {
                    CascadeUntilExhausted(bhawkRig, bhawk.PlaneModel);
                    ctx.Check(bhawkRig.UnresolvedStageAnchors.SequenceEqual(new[]
                        {
                            "random_remote_damage|lft_elev", "random_remote_damage|rt_elev",
                        }),
                        $"the Bloodhawk misses exactly the elevator pair: [{string.Join(", ", bhawkRig.UnresolvedStageAnchors)}]");
                }
            }
            finally
            {
                bhawk?.Free();
                ai?.Free();
                pool?.Free();
                textures.Dispose();
            }
        });
    }

    // ---- an AI kill, from the death frame to the ground -----------------------------------------

    // The whole fall as one sequence: the kill starts the SELF-NAMED destroy def and nothing else,
    // the airframe stays VISIBLE for every frame of it, the hull travels a measured distance under
    // the flight model, Callback 16 hands the anim the velocity it reached THERE, and a wreck that
    // meets the ground first plays the ground-impact def and is hidden only then.
    // ⚠ Keep the unwired control: the bug this pins is a hull hidden on the death frame.
    // Decode: docs/org/vehicleDamage.md, "What happens to the wreck".
    internal static void AiWreckFall(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.WithWorld(ctx.Chapter, collision: true, world =>
        {
            var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
            var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, world.Chapter));
            ProjectilePool? pool = null;
            try
            {
                var live = new ProjectilePool(textures, null, null);
                pool = live;
                ctx.Host.AddChild(live);
                var spec = SessionSpec.Parse(System.Array.Empty<string>());
                var liveries = new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof"));
                var factory = new Session.WorldEffectsFactory(spec, ctx.Host, () => Vector3.Zero);
                var inputs = new HumanFlightAdapter.Inputs
                {
                    PlanesGamez = planesGamez,
                    StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
                    AiStatsFor = plane => PlaneStats.LoadForAi(ctx.ZrdrPath, plane),
                    RigCount = 0,
                    PaintRng = new RandomNumberGenerator(),
                    ZrdrPath = ctx.ZrdrPath,
                    StockLoadouts = StockLoadouts.Load(),
                    WeaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null),
                    Textures = textures,
                    Projectiles = live,
                    Shakes = ShakeDefs.Load(ctx.ZrdrPath),
                    Gamez = world.Gamez,
                    WorldScene = world.Session.Builder.Scene,
                    CrashProgram = world.Session.Program,
                };
                var spawner = new FlightRoster(spec, liveries, factory, ctx.Host, inputs);

                // A suite that built no colliders would pass the landing arm by never reaching the
                // ground at all, so the chapter's geometry is found before anything is asked of it.
                var space = world.Session.Root.GetWorld3D()?.DirectSpaceState;
                if (space == null)
                {
                    ctx.Check(false, $"the world built a collision space to fall into chapter={ctx.Chapter}");
                    return;
                }

                bool found = FindFlatRun(space, out var lowSpawn, out var lowAim);
                ctx.Check(found,
                    $"chapter {ctx.Chapter} offers a flat run of land to sink onto at {(found ? $"({lowSpawn.X:0},{lowSpawn.Y:0},{lowSpawn.Z:0})" : "nowhere")}");

                // ⚠ Below FlightModel's altitude cap, which SNAPS a higher spawn down to 2045.8 m on
                // its first step: a fall arm above it measures that teleport as 450 m of falling.
                const float FallFrom = 1500f;
                WreckFallsBeforeItIsHandedOver(ctx, spawner, FallFrom);
                WreckWithNoDestroyDefIsHiddenOnTheKill(ctx, spawner, FallFrom);
                if (found)
                    WreckReachingTheGroundPlaysItsCrashDef(ctx, spawner, lowSpawn, lowAim);
            }
            finally
            {
                pool?.Free();
                textures.Dispose();
            }
        });
    }

    // The player's own destroy choreography. `player-player` is the one destroy def that does not
    // fall as an intact hull: it breaks into four burning pieces, each flown by its own
    // OBJECT_MOTION, with a cockpit eject and a parachutist.
    // ⚠ Both arms are READ, never guessed: `If NODE_ACTIVE 1` names entry one of the def's own node
    // list, `player_autogyro`, so the rotor arm is an airframe test and every other airframe takes
    // `random_destroy`. Decode: docs/org/vehicleDamage.md.
    internal static void PlayerDestroyChoreography(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
            var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, world.Chapter));
            try
            {
                // A fixed pair, not ctx.PlaneName: the verdict IS the branch between them, so
                // --plane= must not be able to make both runs the same arm.
                PlayerDestroyArm(ctx, world, planesGamez, textures, "player_bhawk", autogyro: false);
                PlayerDestroyArm(ctx, world, planesGamez, textures, "player_autogyro", autogyro: true);
            }
            finally
            {
                textures.Dispose();
            }
        });
    }

    private static void CallbackSeamsReceiveTheirCodes(TestContext ctx, TestWorld world)
    {
        // 3.0 s past chuteman's authored start, which is what fury-fury's own callbacks follow.
        const int Steps = 360;
        var handed = new Vector3(11f, -23f, 37f);
        // The nodes player-player and fury-fury name, flat, so a call that misses one is visible
        // rather than absorbed by a shared ancestor.
        var destroyDefNodes = new[]
        {
            "healthy", "destroyed", "dontmove", "markers", "shadow", "cockpit1",
            "piece1", "piece2", "piece3", "piece4",
            "prop1", "prop1b", "wing_flare1", "wing_flare2", "staticprop1", "nitroprop1",
        };
        foreach (var animName in new[] { "player", "fury" })
        {
            var program = world.Session.Program.Subset(animName);
            var defs = program.ByAnimName(animName);
            ctx.Check(defs.Count > 0, $"chapter program has the self-named destroy def {animName} defs={defs.Count}");
            if (defs.Count == 0)
            {
                continue;
            }

            WorldAndToolSuites.WithEmitterStage(ctx, program, $"Callback_{animName}", destroyDefNodes, (stage, runtime, fake) =>
            {
                int stops = 0;
                runtime.WreckVelocity = () => handed;
                runtime.StopDamageStages = () => stops++;
                runtime.Start(defs[0], stage);
                for (int i = 0; i < Steps; i++)
                {
                    runtime.Advance(1f / 60f);
                }

                ctx.Check(runtime.InheritedWorldVelocity.IsEqualApprox(handed),
                    $"{animName}: CALLBACK 16 handed the instance the rig's velocity inherited={runtime.InheritedWorldVelocity} handed={handed}");
                ctx.Same(1, stops, $"{animName}: CALLBACK 15 stopped the damage stages exactly once");
                foreach (var (key, n) in runtime.UnhandledEventCounts)
                {
                    ctx.Check(!key.Contains("no seam wired"),
                        $"{animName}: no acted-on code went unwired ({key}×{n})");
                }
            },
                asCrashRig: true);

            // THE CONTROL, and the unknown-code arm: the same def with no seams. Nothing moves, and
            // player-player's authored 3 is counted rather than guessed at either way.
            WorldAndToolSuites.WithEmitterStage(ctx, program, $"CallbackBare_{animName}", destroyDefNodes, (stage, runtime, fake) =>
            {
                runtime.Start(defs[0], stage);
                for (int i = 0; i < Steps; i++)
                {
                    runtime.Advance(1f / 60f);
                }

                ctx.Check(runtime.InheritedWorldVelocity == Vector3.Zero,
                    $"ABLE-TO-FAIL CONTROL {animName}: with no seam wired the wreck inherits nothing inherited={runtime.InheritedWorldVelocity}");
                ctx.Check(runtime.UnhandledEventCounts.ContainsKey("Callback(16, no seam wired)")
                          && runtime.UnhandledEventCounts.ContainsKey("Callback(15, no seam wired)"),
                    $"...and both codes are reported as unwired rather than silently skipped keys=[{string.Join(",", runtime.UnhandledEventCounts.Keys)}]");
                if (animName == "player")
                {
                    ctx.Check(runtime.UnhandledEventCounts.TryGetValue("Callback(3)", out int unknown) && unknown > 0,
                        $"player: the authored code 3 is counted and ignored, never invented (×{(runtime.UnhandledEventCounts.TryGetValue("Callback(3)", out int u) ? u : 0)})");
                }
            },
                asCrashRig: true);
        }
    }

    // The census behind the prohibition on implementing code 0, the free/delete arm: no def in the
    // loaded program authors it, in a sequence or in a RESET_STATE.
    private static void CallbackCodeZeroIsNeverAuthored(TestContext ctx, TestWorld world)
    {
        var codes = new SortedDictionary<int, int>();
        foreach (var def in world.Session.Program.Defs)
        {
            var blocks = new List<AnimSequence>(def.Sequences);
            if (def.ResetState is { } reset)
            {
                blocks.Add(reset);
            }

            foreach (var block in blocks)
            {
                foreach (var ev in block.Events)
                {
                    if (ev.Kind != "Callback")
                    {
                        continue;
                    }

                    int code = (int)(ev.Data.Num("value") ?? -1f);
                    codes[code] = codes.TryGetValue(code, out int n) ? n + 1 : 1;
                }
            }
        }

        ctx.Note($"chapter {ctx.Chapter} authors callback codes {string.Join(", ", codes.Select(kv => $"{kv.Key}×{kv.Value}"))}");
        ctx.Same(0, codes.TryGetValue(0, out int zeroes) ? zeroes : 0,
            $"no def authors the free/delete arm, code 0");
        ctx.Check(codes.ContainsKey(16) && codes.ContainsKey(15),
            $"and the two codes this runtime acts on ARE authored here 16×{(codes.TryGetValue(16, out int c16) ? c16 : 0)} 15×{(codes.TryGetValue(15, out int c15) ? c15 : 0)}");
    }

    private static int Count(IReadOnlyDictionary<string, int> counts, string key) =>
        counts.TryGetValue(key, out var n) ? n : 0;

    // One exact health spend, so a band is entered by crossing it rather than by an approximate
    // hit: the armour pool is already empty, so the whole amount reaches the hull pair.
    private static void SpendHullTo(FlightController ai, PlaneDamage damage, float fraction)
    {
        float spend = damage.WholeHealth - (fraction * damage.WholeHealthMax);
        if (spend > 0f)
            ai.TakeCollisionHit(0f, spend, ai.GlobalPosition, 0);
    }

    // random_remote_damage is a RandomWeight 0.33 chain: one start reaches at most one of its four
    // anchors, so the census below needs the whole cascade walked. 200 starts puts the deepest step
    // (0.67³ × 0.33 ≈ 10 %) beyond any doubt while the seed keeps it identical run to run.
    private static void CascadeUntilExhausted(AnimRuntime rig, Node3D? planeModel)
    {
        for (int i = 0; i < 200; i++)
            rig.Play("random_remote_damage", planeModel, applyReset: false);
    }

    // The fall itself, from a height clear of both the ground and the altitude cap. Everything here
    // is one aircraft's own timeline, so the checks are ordered as the frames are.
    private static void WreckFallsBeforeItIsHandedOver(TestContext ctx, FlightRoster spawner,
        float fallFrom)
    {
        const float Dt = 1f / 60f;
        FlightController? ai = null;
        try
        {
            var spawn = new Vector3(0f, fallFrom, 0f);
            ai = spawner.SpawnAi(new AiSpawn("player_fury", spawn, spawn + Vector3.Forward,
                AiPilot.HoldingCourse(spawn, spawn + Vector3.Forward)));
            if (ai.CrashRuntime is not { } rig || ai.Damage is not { } damage
                || ai.PlaneModel is not { } model)
            {
                ctx.Check(false, $"the spawner built the AI Fury a crash runtime, a damage ledger and a model");
                return;
            }

            rig.ManualAdvance = true;
            ctx.Check(ai.DestroyDef == "fury" && ai.DestroyDefFliesWreck,
                $"the AI Fury's death slot is its own self-named def '{ai.DestroyDef ?? "-"}', and the def flies the hull itself (fliesOwnHull={ai.DestroyDefFliesWreck})");

            var starts = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            rig.OnInstanceStarted += (def, _) =>
            {
                string n = def.AnimName ?? def.Name ?? "";
                starts[n] = starts.TryGetValue(n, out var c) ? c + 1 : 1;
            };

            var healthy = Find(model, "healthy");
            var wreck = ai.CrashAnchor != null ? Find(ai.CrashAnchor, "destroyed") : null;
            var posAtKill = ai.WorldPosition;
            var velAtKill = ai.WorldVelocity;

            // The kill through the production ram entry, both magnitudes — standing armour would
            // otherwise null the health damage outright (PlaneDamage.Spend).
            float overkill = (damage.WholeHealthMax + damage.WholeArmorMax) * 4f;
            ai.TakeCollisionHit(overkill, overkill, ai.GlobalPosition, 0);

            ctx.Check(ai.Destroyed && ai.WreckFalling,
                $"the kill starts the destroy anim and leaves the hull FLYING destroyed={ai.Destroyed} falling={ai.WreckFalling}");
            ctx.Same(1, Count(starts, "fury"),
                $"the rig started the self-named destroy def exactly once on the kill");
            int crashStarts = starts.Where(kv => kv.Key.StartsWith(
                Session.EffectCatalogue.AiCrashDefPrefix, System.StringComparison.OrdinalIgnoreCase))
                .Sum(kv => kv.Value);
            ctx.Same(0, crashStarts,
                $"…and no ai_crash_* def, which belongs to the ground contact seconds away");
            ctx.Check(model.Visible && healthy is { Visible: true },
                $"the airframe is still there on the death frame model={model.Visible} healthy={(healthy?.Visible.ToString() ?? "-")}");

            // The fall, sampled every frame rather than at its ends: a hull hidden for part of it and
            // shown again would read as never hidden from the endpoints alone.
            int blankFrames = 0;
            int hullHiddenFrames = 0;
            float handoverAt = -1f;
            var velAtHandover = Vector3.Zero;
            var posAtHandover = posAtKill;
            for (float t = 0f; t < 8f && handoverAt < 0f; t += Dt)
            {
                ai.SimStep(Dt);
                // Read between the two halves of the frame: Callback 16 fires inside Advance below
                // and samples the hull the step above has just moved.
                velAtHandover = ai.WorldVelocity;
                posAtHandover = ai.WorldPosition;
                rig.Advance(Dt);
                if (!model.Visible)
                    hullHiddenFrames++;
                if (healthy is { Visible: false } && wreck is { Visible: false })
                    blankFrames++;
                if (!ai.WreckFalling)
                    handoverAt = t + Dt;
            }

            float travelled = posAtHandover.DistanceTo(posAtKill);
            ctx.Note($"the AI Fury's wreck flew {travelled:0} m of its own in {handoverAt:0.00} s, from y={posAtKill.Y:0} to y={posAtHandover.Y:0} m (a glide, not a drop), at {velAtKill.Length():0} → {velAtHandover.Length():0} m/s");
            ctx.Check(handoverAt is >= 2.9f and <= 3.2f,
                $"Callback 15 released the hull at the def's authored 3.0 s gate t={handoverAt:0.00} s");
            ctx.Same(0, hullHiddenFrames,
                $"the airframe node was never hidden across the {handoverAt:0.00} s fall");
            ctx.Same(0, blankFrames,
                $"…and something of the aircraft was drawn on every one of those frames (healthy, then the destroyed wreck)");
            // The dead hull GLIDES rather than dropping, which the decode says is faithful: the
            // original changes nothing about a destroyed aircraft's integration.
            ctx.Check(travelled > 100f,
                $"the hull TRAVELLED under the flight model before the handover: {travelled:0} m (want over 100)");
            ctx.Check(rig.InheritedWorldVelocity.IsEqualApprox(velAtHandover)
                      && velAtHandover != Vector3.Zero,
                $"Callback 16 handed the anim the velocity the wreck had reached inherited={rig.InheritedWorldVelocity} wreck={velAtHandover}");
            ctx.Check(velAtKill.Length() - rig.InheritedWorldVelocity.Length() > 5f,
                $"…sampled at the handover, not at the kill: drag took it from {velAtKill.Length():0.0} to {rig.InheritedWorldVelocity.Length():0.0} m/s over those seconds");
            ctx.Check(ai.LastCrashDef == null,
                $"nothing played the ground-impact family on the way down def={ai.LastCrashDef ?? "-"}");

            // Past the handover the hull is the anim's: the flight model must not still be moving it,
            // or the wreck and its ObjectMotion would fly the same node apart.
            var afterHandover = ai.WorldPosition;
            for (int i = 0; i < 60; i++)
                ai.SimStep(Dt);
            ctx.Check(ai.WorldPosition.IsEqualApprox(afterHandover),
                $"and the flight model stopped moving it once released moved={ai.WorldPosition.DistanceTo(afterHandover):0.00} m");
        }
        finally
        {
            ai?.Free();
        }
    }

    // THE ABLE-TO-FAIL CONTROL, and the bug in one line: with no destroy def bound the airframe is
    // hidden on the death frame and never travels a metre. Every check in the arm above passes on
    // this rig too if it is written loosely enough, which is why it runs.
    private static void WreckWithNoDestroyDefIsHiddenOnTheKill(TestContext ctx,
        FlightRoster spawner, float fallFrom)
    {
        const float Dt = 1f / 60f;
        FlightController? ai = null;
        try
        {
            var spawn = new Vector3(3000f, fallFrom, 0f);
            ai = spawner.SpawnAi(new AiSpawn("player_fury", spawn, spawn + Vector3.Forward,
                AiPilot.HoldingCourse(spawn, spawn + Vector3.Forward)));
            if (ai.Damage is not { } damage || ai.PlaneModel is not { } model)
            {
                ctx.Check(false, $"the control rig spawned with a damage ledger and a model");
                return;
            }

            ai.DestroyDef = null;
            var posAtKill = ai.WorldPosition;
            float overkill = (damage.WholeHealthMax + damage.WholeArmorMax) * 4f;
            ai.TakeCollisionHit(overkill, overkill, ai.GlobalPosition, 0);
            for (int i = 0; i < 180; i++)
                ai.SimStep(Dt);
            ctx.Check(!model.Visible && !ai.WreckFalling,
                $"ABLE-TO-FAIL CONTROL: with no destroy def the airframe is hidden on the death frame model={model.Visible} falling={ai.WreckFalling}");
            ctx.Check(ai.WorldPosition.DistanceTo(posAtKill) < 1f,
                $"…and the hull never falls: {ai.WorldPosition.DistanceTo(posAtKill):0.00} m in three seconds");
        }
        finally
        {
            ai?.Free();
        }
    }

    // A run of chapter land for a wreck to strike, 60 m under the spawn and aimed into it. ⚠ The
    // DIVE is what makes it reachable: a dead hull holds its altitude almost exactly, so one killed
    // in level flight glides out its three seconds and never meets the ground at all. Both ends of
    // the run must be land at one height; sea level is skipped, so the crash table indexes its own
    // ground slot rather than the water one.
    private static bool FindFlatRun(PhysicsDirectSpaceState3D space, out Vector3 spawn, out Vector3 aim)
    {
        spawn = Vector3.Zero;
        aim = Vector3.Forward;
        for (int xi = -3; xi <= 3; xi++)
        {
            for (int zi = -3; zi <= 3; zi++)
            {
                var a = new Vector3(xi * 1500f, 0f, zi * 1500f);
                var b = a + Vector3.Forward * 120f;
                if (SurfaceHeight(space, a) is not { } ha || SurfaceHeight(space, b) is not { } hb)
                    continue;
                if (ha < 1f || Mathf.Abs(ha - hb) > 10f)
                    continue;
                spawn = new Vector3(a.X, ha + 60f, a.Z);
                aim = spawn + new Vector3(0f, -1f, -0.3f);
                return true;
            }
        }

        return false;
    }

    private static float? SurfaceHeight(PhysicsDirectSpaceState3D space, Vector3 column)
    {
        var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
            new Vector3(column.X, 3000f, column.Z), new Vector3(column.X, -200f, column.Z),
            CollisionLayers.World));
        return hit.Count > 0 ? hit["position"].AsVector3().Y : null;
    }

    // The other outcome the same code path serves: a wreck that reaches the world INSIDE the three
    // seconds is still a vehicle, so its contact runs the surface-indexed ai_crash_* table, and that
    // def is what hides it. Spawned low over the flat run above, because a hull that holds its
    // altitude for three seconds started high reaches no ground at all.
    private static void WreckReachingTheGroundPlaysItsCrashDef(TestContext ctx,
        FlightRoster spawner, Vector3 spawn, Vector3 aim)
    {
        const float Dt = 1f / 60f;
        FlightController? ai = null;
        try
        {
            ai = spawner.SpawnAi(new AiSpawn("player_fury", spawn, aim, AiPilot.HoldingCourse(spawn, aim)));
            if (ai.CrashRuntime is not { } rig || ai.Damage is not { } damage
                || ai.PlaneModel is not { } model)
            {
                ctx.Check(false, $"the low rig spawned with a crash runtime, a damage ledger and a model");
                return;
            }

            rig.ManualAdvance = true;
            float overkill = (damage.WholeHealthMax + damage.WholeArmorMax) * 4f;
            ai.TakeCollisionHit(overkill, overkill, ai.GlobalPosition, 0);
            ctx.Check(model.Visible && ai.LastCrashDef == null,
                $"this wreck leaves the kill frame visible and unlanded too def={ai.LastCrashDef ?? "-"}");

            float landedAt = -1f;
            int visibleFramesBeforeLanding = 0;
            for (float t = 0f; t < 8f && landedAt < 0f; t += Dt)
            {
                ai.SimStep(Dt);
                rig.Advance(Dt);
                if (ai.LastCrashDef != null)
                    landedAt = t + Dt;
                else if (model.Visible)
                    visibleFramesBeforeLanding++;
            }

            ctx.Note($"the gliding wreck struck the ground at t={landedAt:0.00} s, {spawn.DistanceTo(ai.WorldPosition):0} m out, def={ai.LastCrashDef ?? "-"}");
            ctx.Check(landedAt is > 0f and < 3f,
                $"the wreck reached the world inside the 3.0 s window, so it was still a vehicle t={landedAt:0.00} s");
            ctx.Check(ai.LastCrashDef != null && ai.LastCrashDef.StartsWith(
                    Session.EffectCatalogue.AiCrashDefPrefix, System.StringComparison.Ordinal),
                $"…and its contact played the AI ground-impact family, indexed by the struck surface def={ai.LastCrashDef ?? "-"}");
            ctx.Check(visibleFramesBeforeLanding > 0 && !model.Visible,
                $"…which is what hides it, and only then: {visibleFramesBeforeLanding} visible frame(s) of flight, then model={model.Visible}");
            ctx.Check(!ai.WreckFalling,
                $"…and the wreck stopped flying itself on that contact falling={ai.WreckFalling}");
        }
        finally
        {
            ai?.Free();
        }
    }

    // One airframe's whole death, through the production factory and the production take-hit path.
    private static void PlayerDestroyArm(TestContext ctx, TestWorld world, GameZ planesGamez,
        TextureArchive textures, string planeName, bool autogyro)
    {
        var factory = new Session.WorldEffectsFactory(
            SessionSpec.Parse(System.Array.Empty<string>()), ctx.Host, () => Vector3.Zero);
        FlightController? player = null;
        try
        {
            var spawn = new Vector3(0f, 500f, 0f);
            var stats = PlaneStats.Load(ctx.ZrdrPath, planeName);
            var builder = new PlaneBuilder(planesGamez, textures);
            var planeModel = builder.Build(planeName);
            player = new FlightController
            {
                PlaneModel = planeModel,
                Collider = PlaneCollider.Build(planeModel),
                PlayerIndex = 0,
                UseKeyboard = false,
                PadDevices = System.Array.Empty<int>(),
                AllowPause = false,
                // The ledger the production assembler gives a human rig — without it the take-hit
                // path returns early and nothing can reach Destroy.
                Damage = PlaneDamage.For(stats),
            };
            player.AddChild(planeModel);
            player.Setup(new FlightModel(stats), null, new CamParams(), spawn, spawn + Vector3.Forward);
            // Dying banked, not level: the parachute is staged under this controller, so a level
            // airframe cannot tell a chute that levels from one that copies the wreck.
            player.Rotation = new Vector3(0.35f, 1.2f, -0.8f);
            ctx.Host.AddChild(player);
            factory.BuildFlightCrashRuntime(player, builder, planeName, world.Gamez,
                world.Session.Builder.Scene, textures, world.Session.Program, verbose: false,
                planesGamez: planesGamez);
            if (player.CrashRuntime is not { } rig || player.CrashAnchor is not { } crashRoot)
            {
                ctx.Check(false, $"{planeName}: the human rig built a crash runtime");
                return;
            }

            rig.ManualAdvance = true;
            ctx.Check(player.DestroyDef == Session.EffectCatalogue.PlayerDestroyAnim
                      && !player.DestroyDefFliesWreck,
                $"{planeName}: the human rig's destroy def is '{player.DestroyDef ?? "-"}' and authors no hull ObjectMotion (fliesOwnHull={player.DestroyDefFliesWreck})");

            // The eject's own template: `cpilot` is a planes-gamez root no def NAMES, so the anchor
            // closure alone would leave cpeject1/cpeject2 playing on nothing.
            var cpilot = Find(crashRoot, "cpilot");
            var chuteman = Find(crashRoot, "chuteman");
            ctx.Check(cpilot != null && chuteman != null,
                $"{planeName}: the rig staged the bailing pilot and the parachute cpilot={(cpilot != null ? "yes" : "NO")} chuteman={(chuteman != null ? "yes" : "NO")}");
            var pieces = new[] { "piece1", "piece2", "piece3", "piece4" }
                .Select(n => (Name: n, Node: Find(crashRoot, n))).ToList();
            ctx.Check(pieces.All(p => p.Node != null),
                $"{planeName}: BuildDestroyed built all four wreck pieces [{string.Join(", ", pieces.Select(p => $"{p.Name}={(p.Node != null ? "yes" : "NO")}"))}]");
            if (pieces.Any(p => p.Node == null) || cpilot == null)
            {
                return;
            }

            var before = pieces.Select(p => p.Node!.GlobalPosition).ToList();
            var healthy = Find(planeModel, "healthy");
            var wreck = Find(crashRoot, "destroyed");

            // The kill, through the production ram entry — not a visuals API. Both magnitudes, or
            // standing armour nulls the health damage outright (PlaneDamage.Spend).
            float overkill = (player.Damage!.WholeHealthMax + player.Damage.WholeArmorMax) * 4f;
            player.TakeCollisionHit(overkill, overkill, player.GlobalPosition, 1);
            ctx.Check(player.Destroyed, $"{planeName}: the hull is spent and the destroy def is playing");

            // The eject first, and destroy_craft only once it COMPLETES: both arms call
            // cpeject1/cpeject2 with WAIT_FOR_COMPLETION, so the breakup is authored to wait for
            // the pilot to leave. The autogyro's own call sits behind a 0.15 s rotor event.
            float ejectAt = AdvanceUntil(rig, 30f, () => cpilot.Visible);
            ctx.Check(ejectAt >= 0f,
                $"{planeName}: the cockpit eject switched its cpilot on at t={ejectAt:0.00} s");
            float breakupAt = AdvanceUntil(rig, 30f, () => wreck is { Visible: true });
            ctx.Check(breakupAt >= 0f,
                $"{planeName}: destroy_craft swapped the airframe for the wreck at t={breakupAt:0.00} s, once the eject completed");
            ctx.Check(healthy is { Visible: false },
                $"{planeName}: …and switched the airframe's healthy subtree off healthy={(healthy?.Visible.ToString() ?? "-")}");

            // Which arm ran, read off the two firetrail templates the branches call: the autogyro
            // arm hangs `sputter_firetrail` on `destroyed`, `random_destroy` hangs
            // `dense_firetrail` on `healthy`.
            var sputter = Find(crashRoot, "sputter_firetrail");
            var dense = Find(crashRoot, "dense_firetrail");
            string arm = sputter is { Visible: true } ? "autogyro" : dense is { Visible: true } ? "random_destroy" : "neither";
            ctx.Check(arm == (autogyro ? "autogyro" : "random_destroy"),
                $"{planeName}: IF NODE_ACTIVE 1 chose the {arm} arm (want {(autogyro ? "autogyro" : "random_destroy")})");
            ctx.Check(pieces.All(p => p.Node!.Visible),
                $"{planeName}: all four pieces are showing [{string.Join(", ", pieces.Select(p => $"{p.Name}={p.Node!.Visible}"))}]");

            // Two seconds of the pieces' own motion.
            for (int i = 0; i < 120; i++)
            {
                rig.Advance(1f / 60f);
            }

            var moved = pieces.Select((p, i) => (p.Name, Dist: p.Node!.GlobalPosition.DistanceTo(before[i]))).ToList();
            ctx.Check(moved.All(m => m.Dist > 1f),
                $"{planeName}: every piece flew its own ObjectMotion [{string.Join(", ", moved.Select(m => $"{m.Name}={m.Dist:0.0} m"))}]");
            ctx.Check(rig.BallisticMotionsLaunched >= 4,
                $"{planeName}: the rig launched {rig.BallisticMotionsLaunched} ballistic motion(s), one per piece at least");

            // Callback 3 is a CAMERA command (docs/org/vehicleDamage.md), and CSVM has no view for
            // it to leave, so the code stays counted rather than acted on.
            ctx.Check(rig.UnhandledEventCounts.TryGetValue("Callback(3)", out int three) && three > 0,
                $"{planeName}: the authored Callback 3 is counted, not invented (×{(rig.UnhandledEventCounts.TryGetValue("Callback(3)", out int n3) ? n3 : 0)})");
            ctx.Check(chuteman is { Visible: true },
                $"{planeName}: the parachutist is out too, untimed here where the ten AI defs gate him at 3.0 s");
            // He hangs level in the WORLD while the airframe he left is banked: his template is
            // authored at identity, and only our staging could hand the placing call a wreck basis.
            var chuteEuler = chuteman != null
                ? chuteman.GlobalBasis.GetEuler() * (180f / Mathf.Pi) : Vector3.Zero;
            ctx.Check(chuteman != null && chuteEuler.Length() < 1f,
                $"{planeName}: the canopy hangs level, world rot ({chuteEuler.X:0.0}, {chuteEuler.Y:0.0}, {chuteEuler.Z:0.0}), under a wreck at ({player.GlobalRotationDegrees.X:0.0}, {player.GlobalRotationDegrees.Y:0.0}, {player.GlobalRotationDegrees.Z:0.0})");
            ctx.Note($"{planeName}: eject at t={ejectAt:0.00} s, breakup at t={breakupAt:0.00} s, seated pilot visible={(Find(planeModel, "pilot")?.Visible.ToString() ?? "-")}, chute pilot visible={(chuteman != null ? Find(chuteman, "pilot")?.Visible.ToString() ?? "-" : "-")}");
            ctx.Note($"{planeName}: unhandled event kinds [{string.Join(", ", rig.UnhandledEventCounts.Select(kv => $"{kv.Key}×{kv.Value}"))}]");
        }
        finally
        {
            player?.Free();
        }
    }

    // Steps the rig at 60 Hz until the beat lands, and returns when — or -1 after the budget. A
    // fixed frame count cannot express "once the eject completes"; the wait is authored, not timed.
    private static float AdvanceUntil(AnimRuntime rig, float budgetSeconds, System.Func<bool> beat)
    {
        const float Dt = 1f / 60f;
        for (float t = 0f; t <= budgetSeconds; t += Dt)
        {
            if (beat())
            {
                return t;
            }

            rig.Advance(Dt);
        }

        return -1f;
    }

    // The first descendant carrying this authored NAME (or Godot name), the way a def's own
    // resolution finds it — a suite must not assume the Godot node name survived staging.
    private static Node3D? Find(Node root, string name)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Node3D n3d)
            {
                string authored = n3d.HasMeta(AnimRuntime.NameMeta)
                    ? (string)n3d.GetMeta(AnimRuntime.NameMeta)
                    : n3d.Name.ToString();
                if (authored.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return n3d;
                }
            }

            if (child != null && Find(child, name) is { } hit)
            {
                return hit;
            }
        }

        return null;
    }
}
