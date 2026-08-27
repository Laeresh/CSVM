using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>Suites asserting the choreography a death dispatches: destroy defs, wreck
/// flights, crash rigs, callbacks, and the stops that end them.</summary>
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
            var pdp7 = DamageSuites.PoolAnchorNode("pdp7", new Vector3(-10, 0, 0));
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

    // The authored STOP_SEQUENCE stops must actually run; nothing else in the gate measures an effect's
    // DURATION (--effects-test only proves a puffer builds, docs/verification.md). It asserts on the
    // dispatch timeline via AnimRuntime.OnEventDispatched, so it needs no textures: the rocket
    // fireball's ON_CALL stopper is named by a stop nothing runs and must stay silent, and the 30 s
    // fire's emitting poll loop is halted, since an un-halted Loop{-1} re-fires every frame forever.
    internal static void StopSequenceStops(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var program = world.Session.Program.Subset(new[] { "large_fireball", "large_30sec_fire", "car_loop1_start" });
            var fireball = program.ByAnimName("large_fireball");
            var fire30 = program.ByAnimName("large_30sec_fire");
            var carLoop = program.ByAnimName("car_loop1_start");
            ctx.Check(fireball.Count > 0, $"chapter program has large_fireball defs={fireball.Count}");
            ctx.Check(fire30.Count > 0, $"chapter program has large_30sec_fire defs={fire30.Count}");
            ctx.Check(carLoop.Count > 0, $"chapter program has car_loop1_start defs={carLoop.Count}");
            if (fireball.Count == 0 || fire30.Count == 0 || carLoop.Count == 0)
            {
                return;
            }

            var stage = new Node3D { Name = "StopSequenceStage" };
            var runtime = new AnimRuntime { AutoStart = false, ManualAdvance = true, SoundHandledElsewhere = true };
            ctx.Host.AddChild(stage);
            ctx.Host.AddChild(runtime);
            try
            {
                runtime.Bind(stage, program);
                var timeline = new List<(float T, string Seq, string Kind)>();
                float clock = 0f;
                runtime.OnEventDispatched = d => timeline.Add((clock, d.Sequence, d.EventKind));

                // The fireball: activate_puffer names its ON_CALL stopper at EVENT_OFFSET 0.3 while
                // nothing runs under that name — the stop halts nothing and must START nothing,
                // so the stopper's teardown never dispatches at all.
                runtime.Start(fireball[0], stage);
                for (int i = 0; i < 60; i++)
                {
                    clock += 1f / 60f;
                    runtime.Advance(1f / 60f);
                }
                int stopperDispatches = 0;
                int trailPuffs = 0;
                foreach (var e in timeline)
                {
                    if (e.Seq == "stop_p1trail")
                    {
                        stopperDispatches++;
                    }
                    else if (e.Kind == "PufferState")
                    {
                        trailPuffs++;
                    }
                }
                ctx.Check(trailPuffs > 0, $"the fireball's own puffer events dispatch puffs={trailPuffs}");
                ctx.Same(0, stopperDispatches, $"stop_p1trail dispatches nothing within 1 s");

                // The 30 s fire: fire_n_smoke is a running Loop{-1} poll re-asserting its emitter
                // every frame — the ANIMATION_OFFSET 30 stop must HALT it (the halt idiom), or the
                // re-assert would revive the puffer one frame after the paired INACTIVE.
                float t0 = clock;
                runtime.Start(fire30[0], stage);
                for (int i = 0; i < 320; i++)
                {
                    clock += 0.1f;
                    runtime.Advance(0.1f);
                }
                int pollsBefore = 0;
                float lastPoll = -1f;
                int stopPuffs = 0;
                float stop30At = -1f;
                foreach (var e in timeline)
                {
                    float rel = e.T - t0;
                    if (e.Seq == "fire_n_smoke")
                    {
                        pollsBefore += rel <= 30f ? 1 : 0;
                        lastPoll = rel > lastPoll ? rel : lastPoll;
                    }
                    else if (e.Seq == "stop_fire_n_smoke" && e.Kind == "PufferState")
                    {
                        stopPuffs++;
                        stop30At = rel;
                    }
                }
                ctx.Check(pollsBefore > 100, $"the fire's poll loop runs until its stop polls={pollsBefore}");
                ctx.Check(lastPoll <= 30.2f, $"no fire_n_smoke dispatch after the authored 30 s halt last={lastPoll:0.0}");
                ctx.Same(1, stopPuffs, $"stop_fire_n_smoke PUFFER_STATE dispatches");
                ctx.Check(stop30At >= 29.5f && stop30At <= 30.5f,
                    $"the fire's own puffer-off lands at the authored 30 s t={stop30At:0.0}");

                // The car lap: start_cruisin CALLs car_dust1, STOPs it later in the lap, then LOOPs.
                // A stopped sequence is DONE and a call starts only from PARKED, so every lap after
                // the first is refused and car_dust1's own puffer event fires exactly once.
                runtime.Start(carLoop[0], stage);
                timeline.Clear();
                // A lap is about 25 s; 150 s covers five of them.
                for (int i = 0; i < 1500; i++)
                {
                    clock += 0.1f;
                    runtime.Advance(0.1f);
                }
                int dustCalls = 0;
                int dustRuns = 0;
                float secondCallAt = -1f;
                foreach (var e in timeline)
                {
                    if (e.Seq == "start_cruisin" && e.Kind == "CallSequence")
                    {
                        dustCalls++;
                        if (dustCalls == 2)
                            secondCallAt = e.T;
                    }
                    else if (e.Seq == "car_dust1")
                    {
                        dustRuns++;
                    }
                }
                ctx.Check(dustCalls >= 2, $"the car's lap loop calls car_dust1 on at least two laps calls={dustCalls}");
                ctx.Same(1, dustRuns, $"car_dust1 runs on the first lap only; the later calls are refused (second call at t={secondCallAt:0.0})");
            }
            finally
            {
                runtime.Free();
                stage.Free();
            }
        });
    }

    // ---- PLAYER_1ST_PERSON follows the pilot's view mode -----------------------------------------

    // Condition 120 answered a hardwired false until the Cockpit/Nose view modes existed (A1), so
    // no branch behind it had ever been taken. Subject: the shipped `bullet1` def, whose first
    // Initial sequence is `IF PLAYER_1ST_PERSON / ELSE CALL_ANIMATION two_bulletholes_a / ENDIF` —
    // the exterior bulletholes are what a pilot NOT in a cockpit gets. Its second Initial sequence
    // runs either way and is the control: it proves the def ran at all, so a zero call count reads
    // as "the branch was taken", never as "nothing happened".
    internal static void PlayerFirstPersonCondition(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var program = world.Session.Program.Subset("bullet1");
            var defs = program.ByAnimName("bullet1");
            ctx.Check(defs.Count > 0, $"the program carries the first-person-gated bullet1 def defs={defs.Count}");
            if (defs.Count == 0)
                return;

            var stage = new Node3D { Name = "FirstPersonConditionStage" };
            var runtime = new AnimRuntime { AutoStart = false, ManualAdvance = true, SoundHandledElsewhere = true };
            ctx.Host.AddChild(stage);
            ctx.Host.AddChild(runtime);
            try
            {
                runtime.Bind(stage, program);
                int calls = 0, other = 0;
                runtime.OnEventDispatched = d =>
                {
                    if (d.EventKind == "CallAnimation")
                        calls++;
                    else
                        other++;
                };

                // No seam wired: the answer every runtime without a session gives, and the answer
                // this condition gave everywhere before A1.
                RunBulletDef(runtime, defs[0], stage);
                ctx.Check(calls == 1, $"with no view seam the else branch calls the exterior bulletholes calls={calls}");
                ctx.Check(other > 0, $"and the def's ungated second sequence ran events={other}");

                foreach (var mode in new[] { PilotViewMode.Cockpit, PilotViewMode.Nose })
                {
                    runtime.FirstPersonView = () => PilotView.IsFirstPerson(mode);
                    calls = 0;
                    other = 0;
                    RunBulletDef(runtime, defs[0], stage);
                    ctx.Check(calls == 0, $"in {PilotView.Name(mode)} the condition holds and the call is skipped calls={calls}");
                    ctx.Check(other > 0, $"while the same def's ungated sequence still ran events={other}");
                }

                // Back to Chase on the same runtime: the condition is polled, not latched at bind.
                runtime.FirstPersonView = () => PilotView.IsFirstPerson(PilotViewMode.Chase);
                calls = 0;
                RunBulletDef(runtime, defs[0], stage);
                ctx.Check(calls == 1, $"selecting the chase view again re-opens the else branch calls={calls}");
            }
            finally
            {
                runtime.Free();
                stage.Free();
            }
        });
    }

    // ---- the compiled destruction slot dispatches at death --------------------------------------

    // The live-path start check the direct-Start stop-sequence suite cannot make: a real kill must
    // dispatch the def's compiled destruction slot (AnimDefinition.DeathSlot), the block carrying
    // nearly all of large_30sec_fire's death calls. An unparsed block no-ops every one of them and
    // every "the fire ends on time" check reads the absence as a pass (DIAG-20). Subject: a C1 AA gun.
    // Able to fail: with RunDeathSlot deleted, no destruction_slot lane ever dispatches.
    internal static void DeathSlotDispatches(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var runtime = world.Runtime;
            DestructibleRegistry.Instance? gun = null;
            foreach (var inst in runtime.Destructibles.All)
            {
                if (inst.Def.DeathSlot is { } s && s.Events.Any(e => e.Kind == "CallAnimation"))
                {
                    gun = inst;
                    break;
                }
            }
            ctx.Check(gun != null, $"chapter ships a destructible with a calling destruction slot chapter={ctx.Chapter}");
            if (gun == null)
            {
                return;
            }
            if (gun.Status == DestructibleRegistry.State.Destroyed)
            {
                runtime.ResetDestructible(gun);
            }

            var gunDef = gun.Def;
            var slotDispatches = new List<(string Kind, string? Name)>();
            var previous = runtime.OnEventDispatched;
            try
            {
                runtime.OnEventDispatched = d =>
                {
                    if (d.Def == gunDef && d.Sequence == "destruction_slot")
                    {
                        slotDispatches.Add((d.EventKind, d.EventName));
                    }
                };
                runtime.DamageAt(gun.Anchor, gun.MaxHealth + 1f);
                for (int i = 0; i < 30; i++)
                {
                    runtime.Advance(1f / 60f);
                }
            }
            finally
            {
                runtime.OnEventDispatched = previous;
            }

            ctx.Check(slotDispatches.Count > 0,
                $"the destruction slot dispatched on death def={gunDef.AnimName} events={slotDispatches.Count}");
            ctx.Check(slotDispatches.Any(e => e.Kind == "CallAnimation"),
                $"the slot's CALL_ANIMATION dispatched targets=[{string.Join(",", slotDispatches.Where(e => e.Kind == "CallAnimation").Select(e => e.Name))}]");
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
                var spawner = new FlightRoster(FlightRosterPolicy.From(spec), liveries, factory, ctx.Host, inputs, new FlightWorldBindings { Projectiles = live, Gamez = world.Gamez, WorldScene = world.Session.Builder.Scene, CrashProgram = world.Session.Program }, new HumanRosterBindings());

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
                // Both airframes, because the reported split was between them: the immortal stage is
                // pfsmoketrail at `prop1`, which every airframe carries, and the Bloodhawk's missing
                // lft_elev/rt_elev belong to random_remote_damage, which ends on its own timeline.
                StagedEffectsEndAtTheDeath(ctx, spawner, FallFrom, "player_fury", -3000f);
                StagedEffectsEndAtTheDeath(ctx, spawner, FallFrom, "player_bhawk", -6000f);
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
                // Every airframe, not ctx.PlaneName: part of the verdict IS the branch between
                // them, and `pilot` is a name all eleven carry, so a resolver binding the wrong
                // one is wrong everywhere at once.
                foreach (var planeName in Session.EffectCatalogue.AirframeDestroyAnims.Keys)
                {
                    PlayerDestroyArm(ctx, world, planesGamez, textures, planeName,
                        autogyro: string.Equals(planeName, "player_autogyro", System.StringComparison.OrdinalIgnoreCase));
                }
            }
            finally
            {
                textures.Dispose();
            }
        });
    }

    // ---- binding the crash rig must leave the airframe under the controller --------------------

    // Builds the crash rig the way WorldEffectsFactory.BuildFlightCrashRuntime does, binds the
    // crash-rig subset, and asserts the two things that go wrong in flight. ⚠ The airframe model stays
    // a plain child of the controller, not world-pinned: on the Devastator the reset defs' authored
    // name is the model root itself, and the reset chain must not relocate the aircraft the way it
    // places effect templates. ⚠ Every pooled template copy of one root must show the same lit mesh
    // count as its slot-0 sibling; a copy the reset pass missed stays lit for the whole session.
    internal static void CrashRigAnchors(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
            var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, world.Chapter));
            try
            {
                foreach (var model in new[] { "player_bhawk", "player_pfighter" })
                {
                    var controller = new Node3D { Name = "controller_replica" };
                    var runtime = AnimRuntime.ForCrashRig(
                        Session.WorldEffectsFactory.NewCrashTemplateStage(),
                        1, new CountingEmitterFactory(), false);
                    runtime.ManualAdvance = true;
                    try
                    {
                        var builder = new PlaneBuilder(planesGamez, textures);
                        var planeModel = builder.Build(model);
                        controller.AddChild(planeModel);
                        var crashRoot = new Node3D { Name = "player" };
                        crashRoot.SetMeta(AnimRuntime.NameMeta, "player");
                        crashRoot.Transform = planeModel.Transform;
                        controller.AddChild(crashRoot);
                        var rootNames = Session.WorldEffectsFactory.CrashStageRootNames(
                            world.Session.Program, world.Gamez, controller);
                        Session.WorldEffectsFactory.StageCrashTemplates(world.Gamez,
                            world.Session.Builder.Scene, crashRoot, rootNames,
                            Utils.EffectPools.Load());
                        var copies = new List<(string Root, int Slot, Node3D Copy)>();
                        foreach (var child in crashRoot.GetChildren())
                        {
                            if (child is Node3D pool && pool.HasMeta(AnimRuntime.PoolSlotMeta))
                            {
                                int slot = (int)pool.GetMeta(AnimRuntime.PoolSlotMeta);
                                foreach (var staged in pool.GetChildren())
                                {
                                    if (staged is Node3D copy)
                                    {
                                        string root = copy.HasMeta(AnimRuntime.NameMeta)
                                            ? (string)copy.GetMeta(AnimRuntime.NameMeta)
                                            : copy.Name;
                                        copies.Add((root, slot, copy));
                                    }
                                }
                            }
                        }

                        var wreck = builder.BuildDestroyed(model);
                        var restPoses = new List<(Node3D Node, Transform3D RestPose)>();
                        if (wreck != null)
                        {
                            wreck.Visible = false;
                            crashRoot.AddChild(wreck);
                            CollectRestPoses(wreck, restPoses);
                        }

                        ctx.Host.AddChild(controller);
                        ctx.Host.AddChild(runtime);
                        var restOrigin = planeModel.GlobalTransform.Origin;
                        runtime.Bind(controller,
                            world.Session.Program.Subset(Session.EffectCatalogue.CrashRigAnimNames(
                                Session.EffectCatalogue.CrashDefTable(world.Session.Program))));
                        for (int i = 0; i < 6; i++)
                        {
                            runtime.Advance(1f / 60f);
                        }

                        ctx.Check(!planeModel.TopLevel,
                            $"{model}: the airframe model is not world-pinned (TopLevel) by the rig's bind");
                        ctx.Check(planeModel.GetParent() == controller,
                            $"{model}: the airframe model still hangs under the controller");
                        ctx.Check(planeModel.GlobalTransform.Origin.DistanceTo(restOrigin) < 0.5f,
                            $"{model}: the airframe model has not moved off its rig position");
                        // Staged dark: a copy left lit sits at the plane's centre for the whole
                        // session (the flake/gunhit family has no authored deactivation).
                        var lit = string.Join("; ", copies
                            .Select(c => (c.Root, c.Slot, Lit: LitMeshCount(c.Copy)))
                            .Where(c => c.Lit > 0)
                            .Select(c => $"'{c.Root}' slot{c.Slot} lights {c.Lit}"));
                        ctx.Check(lit.Length == 0,
                            $"{model}: every staged template copy is dark after the bind{(lit.Length == 0 ? "" : $" — {lit}")}");

                        // A real tear: the damage sink's own call shape. The CALLed gimmeflakes
                        // copy must light at its pdp5 site, and the panel def's instance ending on
                        // the AIRFRAME anchor must not drag the model into the retire-hide.
                        runtime.Play("pdpanel5", planeModel, applyReset: false);
                        for (int i = 0; i < 6; i++)
                        {
                            runtime.Advance(1f / 60f);
                        }

                        ctx.Check(copies.Any(c => c.Root == "planeflakes" && LitMeshCount(c.Copy) > 0),
                            $"{model}: the tear's planeflakes copy is revealed while its burst flies");
                        for (int i = 0; i < 120; i++)
                        {
                            runtime.Advance(1f / 60f);
                        }

                        ctx.Check(copies.All(c => c.Root != "planeflakes" || LitMeshCount(c.Copy) == 0),
                            $"{model}: the burst's copy goes dark again once the effect is over");
                        ctx.Check(!planeModel.TopLevel
                                  && planeModel.GlobalTransform.Origin.DistanceTo(restOrigin) < 0.5f
                                  && planeModel.IsVisibleInTree(),
                            $"{model}: the airframe model is still parented, placed and visible after the tear");

                        // Crash, respawn, move, crash again: the wreck and every template a crash reveals must play at the
                        // SECOND crash's site. The failure looks like the destroyed plane and the dirt burst replaying at
                        // the first crash's position on every crash after the first.
                        runtime.Play("player_crash_dirt", crashRoot, applyReset: false);
                        for (int i = 0; i < 180; i++)
                        {
                            runtime.Advance(1f / 60f);
                        }

                        // The respawn ritual, FlightController.Respawn's crash arm verbatim.
                        runtime.ResetToBaseState();
                        foreach (var (node, rest) in restPoses)
                        {
                            node.Transform = rest;
                        }

                        var leftover = string.Join("; ", copies
                            .Select(c => (c.Root, c.Slot, Lit: LitMeshCount(c.Copy)))
                            .Where(c => c.Lit > 0)
                            .Select(c => $"'{c.Root}' slot{c.Slot} lights {c.Lit}"));
                        ctx.Check(leftover.Length == 0,
                            $"{model}: respawn leaves no crash template revealed{(leftover.Length == 0 ? "" : $" — {leftover}")}");

                        controller.Position += new Vector3(400, 0, 0);
                        runtime.Play("player_crash_dirt", crashRoot, applyReset: false);
                        for (int i = 0; i < 180; i++)
                        {
                            runtime.Advance(1f / 60f);
                        }

                        var here = controller.GlobalTransform.Origin;
                        if (wreck != null)
                        {
                            ctx.Check(wreck.GlobalTransform.Origin.DistanceTo(here) < 150f,
                                $"{model}: the wreck flies from the SECOND crash's site ({wreck.GlobalTransform.Origin.DistanceTo(here):0} m away)");
                        }

                        var stale = string.Join("; ", copies
                            .Where(c => LitMeshCount(c.Copy) > 0
                                        && c.Copy.GlobalTransform.Origin.DistanceTo(here) > 150f)
                            .Select(c => $"'{c.Root}' slot{c.Slot} at {c.Copy.GlobalTransform.Origin.DistanceTo(here):0} m"));
                        ctx.Check(stale.Length == 0,
                            $"{model}: every template the second crash reveals plays at its own site{(stale.Length == 0 ? "" : $" — {stale}")}");
                        ctx.Check(!crashRoot.TopLevel,
                            $"{model}: the crash scaffold is never world-pinned by a crash's own calls");
                    }
                    finally
                    {
                        runtime.Free();
                        controller.Free();
                    }
                }
            }
            finally
            {
                textures.Dispose();
            }
        });
    }

    // The pre-warm's contract on a replica rig: after Bind and PrewarmEmitters nothing emits, a
    // crash and a panel tear reach the factory for no emitter, the claims count as built, and
    // respawn keeps the emitters so the next crash builds nothing either.
    internal static void EmitterPrewarm(TestContext ctx)
    {
        const string model = "player_bhawk";
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
            var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, world.Chapter));
            var controller = new Node3D { Name = "controller_replica" };
            var fake = new CountingEmitterFactory();
            var runtime = AnimRuntime.ForCrashRig(
                Session.WorldEffectsFactory.NewCrashTemplateStage(), 1, fake, false);
            runtime.ManualAdvance = true;
            try
            {
                var builder = new PlaneBuilder(planesGamez, textures);
                var planeModel = builder.Build(model);
                controller.AddChild(planeModel);
                var crashRoot = new Node3D { Name = "player" };
                crashRoot.SetMeta(AnimRuntime.NameMeta, "player");
                crashRoot.Transform = planeModel.Transform;
                controller.AddChild(crashRoot);
                var program = world.Session.Program;
                var crashDefs = Session.EffectCatalogue.CrashDefTable(program);
                var rootNames = Session.WorldEffectsFactory.CrashStageRootNames(
                    program, world.Gamez, controller, crashDefs);
                Session.WorldEffectsFactory.StageCrashTemplates(world.Gamez,
                    world.Session.Builder.Scene, crashRoot, rootNames, Utils.EffectPools.Load());
                var wreck = builder.BuildDestroyed(model);
                if (wreck != null)
                {
                    wreck.Visible = false;
                    crashRoot.AddChild(wreck);
                }

                ctx.Host.AddChild(controller);
                ctx.Host.AddChild(runtime);
                runtime.Bind(controller,
                    program.Subset(Session.EffectCatalogue.CrashRigAnimNames(crashDefs)));
                for (int i = 0; i < 6; i++)
                {
                    runtime.Advance(1f / 60f);
                }

                int factoryAtBind = fake.Built.Count;
                int builtAtBind = runtime.PuffersBuilt;
                var warmed = runtime.PrewarmEmitters(planeModel, crashRoot);
                int warmedCount = fake.Built.Count;
                ctx.Note($"pre-warm: built={warmed.Built} unhosted={warmed.Unhosted} self_hosted={warmed.SelfHosted} factory_calls={warmedCount - factoryAtBind}");
                ctx.Check(warmed.Built > 0 && warmedCount - factoryAtBind == warmed.Built,
                    $"{model}: the pre-warm built its emitters through the factory built={warmed.Built}");
                ctx.Check(fake.Built.All(e => e.Started == 0 && !e.Sustaining),
                    $"{model}: nothing the pre-warm built has started");
                ctx.Check(runtime.PuffersBuilt == builtAtBind,
                    $"{model}: the pre-warm does not count as built (PuffersBuilt {runtime.PuffersBuilt})");
                ctx.Check(runtime.Emitters.Census.All(r => !r.Emitting),
                    $"{model}: no census row emits after the pre-warm");

                // The crash: every emitter it asserts must already exist.
                runtime.Play("player_crash_dirt", crashRoot, applyReset: false);
                for (int i = 0; i < 180; i++)
                {
                    runtime.Advance(1f / 60f);
                }

                ctx.Check(fake.Built.Count == warmedCount,
                    $"{model}: the crash reaches the factory for no emitter (built {fake.Built.Count - warmedCount} more)");
                ctx.Check(fake.Built.Any(e => e.Started > 0),
                    $"{model}: the crash started pre-warmed emitters");
                ctx.Check(runtime.PuffersBuilt > builtAtBind,
                    $"{model}: a claimed emitter counts as built (PuffersBuilt {runtime.PuffersBuilt})");

                // A panel tear, the damage sink's own call shape.
                runtime.Play("pdpanel5", planeModel, applyReset: false);
                for (int i = 0; i < 6; i++)
                {
                    runtime.Advance(1f / 60f);
                }

                var late = fake.Built.Skip(warmedCount).Select(e => e.Key).ToList();
                var lateRows = runtime.Emitters.Census
                    .Where(r => late.Contains(r.Name) && r.Emitting)
                    .Select(r => $"{r.Name}@{r.Host}[{r.Def}]");
                ctx.Check(fake.Built.Count == warmedCount,
                    $"{model}: the tear reaches the factory for no emitter (built {late.Count} more: {string.Join(", ", lateRows)})");
                int afterTear = fake.Built.Count;

                // Respawn keeps the emitters, so the second crash builds nothing either.
                runtime.ResetToBaseState();
                ctx.Check(fake.Built.All(e => e.IsValid && !e.Sustaining),
                    $"{model}: respawn keeps every emitter, stopped");
                runtime.Play("player_crash_dirt", crashRoot, applyReset: false);
                for (int i = 0; i < 60; i++)
                {
                    runtime.Advance(1f / 60f);
                }

                ctx.Check(fake.Built.Count == afterTear,
                    $"{model}: the second crash reaches the factory for no emitter (built {fake.Built.Count - afterTear} more)");
                ctx.Check(fake.Built.Any(e => e.Sustaining),
                    $"{model}: the second crash emits from the kept emitters");
            }
            finally
            {
                runtime.Free();
                controller.Free();
                textures.Dispose();
            }
        });
        WorldEffectsPrewarm(ctx);
    }

    // Every wreck node's rest pose — the local mirror of
    // `WorldEffectsFactory.CollectRestPoses`, so the suite's respawn ritual can re-home the
    // flung pieces the way `FlightController.Respawn` does.
    internal static void CollectRestPoses(Node3D node, List<(Node3D Node, Transform3D RestPose)> into)
    {
        into.Add((node, node.Transform));
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D sub)
            {
                CollectRestPoses(sub, into);
            }
        }
    }

    // Meshes drawing under one staged template copy — visibility taken in-tree, so a
    // parent the reset pass switched off darkens the whole copy the way it does on screen.
    internal static int LitMeshCount(Node3D copy)
    {
        int n = copy is MeshInstance3D lit && lit.IsVisibleInTree() ? 1 : 0;
        foreach (var child in copy.GetChildren())
        {
            if (child is Node3D sub)
            {
                n += LitMeshCount(sub);
            }
        }
        return n;
    }

    // ---- an AI plane's crash picks from the ai_crash_* vector ----------------------------

    // The AI arm of the crash-family split, through the REAL factory call, which keys the family on
    // IsHumanPiloted: an AI controller's rig binds the ai_crash_* vector, a crash on a body stamped
    // dirt selects ai_crash_dirt, and a crash with no struck body takes the null-material arm to slot
    // 0, ai_crash_default, never a player_crash_* def. ⚠ Keep the human-piloted A/B control; without
    // it a family mix-up in the pick would be invisible from the AI side alone.
    internal static void AiCrashDefs(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
            var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, world.Chapter));
            var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
            var factory = new Session.WorldEffectsFactory(
                SessionSpec.Parse(System.Array.Empty<string>()), ctx.Host, () => Vector3.Zero);
            FlightController? ai = null;
            FlightController? human = null;
            StaticBody3D? dirt = null;
            try
            {
                var spawn = new Vector3(0f, 500f, 0f);
                var builder = new PlaneBuilder(planesGamez, textures);
                var aiModel = builder.Build(ctx.PlaneName);
                ai = new FlightController
                {
                    PlaneModel = aiModel,
                    Collider = PlaneCollider.Build(aiModel),
                    PlayerIndex = FlightRoster.ShooterIdBase,
                    IsHumanPiloted = false,
                    Pilot = AiPilot.HoldingCourse(spawn, spawn + Vector3.Forward),
                    UseKeyboard = false,
                    PadDevices = System.Array.Empty<int>(),
                    AllowPause = false,
                };
                ai.AddChild(aiModel);
                ai.Setup(new FlightModel(stats), null, new CamParams(), spawn, spawn + Vector3.Forward);
                ctx.Host.AddChild(ai);
                // The planes gamez goes in as both spawners pass it: the destroy def's `chuteman`
                // is a template root of planes.zbd, and without it the rig cannot stage it.
                factory.BuildFlightCrashRuntime(ai, builder, ctx.PlaneName, world.Gamez,
                    world.Session.Builder.Scene, textures, world.Session.Program, verbose: false,
                    planesGamez: planesGamez);

                ctx.Check(ai.CrashRuntime != null && ai.CrashDefs != null,
                    $"the AI rig built a crash runtime with a def table");
                if (ai.CrashDefs == null)
                    return;
                ctx.Check(ai.CrashDefs.PlayableDefs.Count == 3
                          && ai.CrashDefs.PlayableDefs.All(d =>
                              d.StartsWith(Session.EffectCatalogue.AiCrashDefPrefix, System.StringComparison.Ordinal)),
                    $"the AI table's playable slots are the ai_crash_* trio [{string.Join(", ", ai.CrashDefs.PlayableDefs)}]");

                // The two subtrees one context node has to reach: `healthy` sits on the plane
                // model, `destroyed` under the crash root. Both shown first, or the wreck's
                // built-hidden state would answer for the deactivation instead of the def.
                var healthy = ai.PlaneModel?.FindChild("healthy", true, false) as Node3D;
                var wreck = ai.CrashAnchor?.FindChild("destroyed", true, false) as Node3D;
                if (healthy != null)
                    healthy.Visible = true;
                if (wreck != null)
                    wreck.Visible = true;

                // A crash on a known surface: a struck body stamped dirt(13) — the id cascade's
                // own-slot arm, through the production Crash path.
                dirt = new StaticBody3D { Name = "dirt_probe" };
                dirt.SetMeta(SceneBuilder.SurfaceIdMeta, 13);
                ctx.Host.AddChild(dirt);
                ai.DebugForceCrash(null, dirt);
                ctx.Check(ai.Crashed && ai.LastCrashDef == Session.EffectCatalogue.AiCrashDefPrefix + "dirt",
                    $"an AI crash on dirt(13) plays ai_crash_dirt def={ai.LastCrashDef ?? "-"}");
                string healthyState = healthy == null ? "-" : healthy.Visible ? "on" : "off";
                string wreckState = wreck == null ? "-" : wreck.Visible ? "on" : "off";
                ctx.Check(healthy is { Visible: false } && wreck is { Visible: false },
                    $"…and the def's own OBJECT_ACTIVE_STATE events reach BOTH subtrees off one context node: healthy={healthyState} destroyed={wreckState}");

                // No struck body: the null-material arm resolves slot 0 of the SAME family.
                ai.Respawn();
                ai.DebugForceCrash();
                ctx.Check(ai.LastCrashDef == Session.EffectCatalogue.AiCrashDefPrefix + "default",
                    $"an AI crash with no material falls to ai_crash_default def={ai.LastCrashDef ?? "-"}");

                // The A/B control: a human rig through the same factory keeps the player family.
                var humanBuilder = new PlaneBuilder(planesGamez, textures);
                var humanModel = humanBuilder.Build(ctx.PlaneName);
                human = new FlightController
                {
                    PlaneModel = humanModel,
                    Collider = PlaneCollider.Build(humanModel),
                    PlayerIndex = 0,
                    UseKeyboard = false,
                    AllowPause = false,
                };
                human.AddChild(humanModel);
                human.Setup(new FlightModel(stats), ctx.Camera, new CamParams(),
                    spawn + new Vector3(2000f, 0f, 0f), spawn + new Vector3(2000f, 0f, -1f));
                ctx.Host.AddChild(human);
                factory.BuildFlightCrashRuntime(human, humanBuilder, ctx.PlaneName, world.Gamez,
                    world.Session.Builder.Scene, textures, world.Session.Program, verbose: false,
                    planesGamez: planesGamez);
                ctx.Check(human.CrashDefs != null && human.CrashDefs.PlayableDefs.All(d =>
                        d.StartsWith(Session.EffectCatalogue.CrashDefPrefix, System.StringComparison.Ordinal)),
                    $"the same factory keeps a human rig on player_crash_* [{string.Join(", ", human.CrashDefs?.PlayableDefs ?? System.Array.Empty<string>())}]");
                human.DebugForceCrash(null, dirt);
                ctx.Check(human.LastCrashDef == Session.EffectCatalogue.CrashDefPrefix + "dirt",
                    $"…and its dirt crash plays player_crash_dirt def={human.LastCrashDef ?? "-"}");
            }
            finally
            {
                dirt?.Free();
                human?.Free();
                ai?.Free();
                textures.Dispose();
            }
        });
    }

    internal static int Count(IReadOnlyDictionary<string, int> counts, string key) =>
        counts.TryGetValue(key, out var n) ? n : 0;

    // One exact health spend, so a band is entered by crossing it rather than by an approximate
    // hit: the armour pool is already empty, so the whole amount reaches the hull pair.
    internal static void SpendHullTo(FlightController ai, PlaneDamage damage, float fraction)
    {
        float spend = damage.WholeHealth - (fraction * damage.WholeHealthMax);
        if (spend > 0f)
            ai.TakeCollisionHit(0f, spend, ai.GlobalPosition, 0);
    }

    // The same pre-warm at the second host, the world-effects stage: the sonic burst's puffer defs
    // are built at bind, and five plays over a four-slot pool then reach the factory for none of
    // them. Five, not one, because each fresh slot copy is its own host and so its own key.
    private static void WorldEffectsPrewarm(TestContext ctx)
    {
        const int slots = 4;
        const string anim = "sonic_ground_effect";
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var stage = OrdnanceSuites.StageBurstRoots(ctx, world, anim, slots);
            var fake = new CountingEmitterFactory();
            var runtime = AnimRuntime.ForEffects(
                AnimRuntime.NewTemplateStage(pooled: true, shown: true, placesCalled: true),
                1, fake, false, SuiteConstants.BurstTtl, () => ctx.Camera.GlobalPosition);
            runtime.ManualAdvance = true;
            ctx.Host.AddChild(stage);
            ctx.Host.AddChild(runtime);
            try
            {
                runtime.Bind(stage, world.Session.Program.Subset(anim));
                int atBind = fake.Built.Count;
                var warmed = runtime.PrewarmEmitters();
                int warmedCount = fake.Built.Count;
                ctx.Note($"world-effects pre-warm: built={warmed.Built} unhosted={warmed.Unhosted} self_hosted={warmed.SelfHosted}");
                ctx.Check(warmed.Built > 0 && warmedCount - atBind == warmed.Built,
                    $"{anim}: the pre-warm built its emitters through the factory built={warmed.Built}");
                ctx.Check(fake.Built.All(e => e.Started == 0 && !e.Sustaining),
                    $"{anim}: nothing the pre-warm built has started");
                ctx.Check(runtime.Emitters.Census.All(r => !r.Emitting),
                    $"{anim}: no census row emits after the pre-warm");

                int steps = Mathf.RoundToInt(SuiteConstants.BurstSeconds * 60f);
                for (int play = 1; play <= slots + 1; play++)
                {
                    ctx.Check(runtime.PlayEffectAt(anim, ctx.Camera.GlobalPosition), $"burst {play} started");
                    for (int i = 0; i < steps; i++)
                    {
                        runtime.Advance(1f / 60f);
                    }
                }

                var late = fake.Built.Skip(warmedCount).Select(e => e.Key).Distinct().ToList();
                ctx.Check(late.Count == 0,
                    $"{anim}: {slots + 1} bursts over {slots} slot(s) reach the factory for no emitter{(late.Count == 0 ? string.Empty : $", built {string.Join(", ", late)}")}");
                ctx.Check(fake.Built.Any(e => e.Started > 0),
                    $"{anim}: the bursts started pre-warmed emitters");
            }
            finally
            {
                runtime.Free();
                stage.Free();
            }
        });
    }

    // One start of the bullethole def, advanced past its authored second (its last event sits at
    // 0.91 s), so the whole of both Initial sequences has dispatched before anything is counted.
    private static void RunBulletDef(AnimRuntime runtime, AnimDefinition def, Node3D stage)
    {
        runtime.Start(def, stage);
        for (int i = 0; i < 120; i++)
            runtime.Advance(1f / 60f);
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

            AnimationAndEffectsSuites.WithEmitterStage(ctx, program, $"Callback_{animName}", destroyDefNodes, (stage, runtime, fake) =>
            {
                int stops = 0;
                runtime.WreckVelocity = () => handed;
                runtime.StopDamageStages = () => stops++;
                runtime.Start(defs[0], stage);
                for (int i = 0; i < Steps; i++)
                {
                    runtime.Advance(1f / 60f);
                }

                ctx.Check(runtime.InheritedWorldVelocity.IsEqualApprox(handed) && runtime.InheritedVelocityArmed,
                    $"{animName}: CALLBACK 16 handed the instance the rig's velocity and ARMED it inherited={runtime.InheritedWorldVelocity} handed={handed} armed={runtime.InheritedVelocityArmed}");
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
            AnimationAndEffectsSuites.WithEmitterStage(ctx, program, $"CallbackBare_{animName}", destroyDefNodes, (stage, runtime, fake) =>
            {
                runtime.Start(defs[0], stage);
                for (int i = 0; i < Steps; i++)
                {
                    runtime.Advance(1f / 60f);
                }

                ctx.Check(runtime.InheritedWorldVelocity == Vector3.Zero && !runtime.InheritedVelocityArmed,
                    $"ABLE-TO-FAIL CONTROL {animName}: with no seam wired the wreck inherits nothing inherited={runtime.InheritedWorldVelocity} armed={runtime.InheritedVelocityArmed}");
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

            // ⚠ Fly it before killing it. On the spawn frame the commands are still default, so the
            // frozen-commands check below cannot fail: freezing and neutralising are the same step
            // there. One second of live flight puts the pilot's real throttle in them.
            for (int i = 0; i < 60; i++)
                ai.SimStep(Dt);

            var posAtKill = ai.WorldPosition;
            var velAtKill = ai.WorldVelocity;

            // The kill through the production ram entry, both magnitudes — standing armour would
            // otherwise null the health damage outright (PlaneDamage.Spend).
            float overkill = (damage.WholeHealthMax + damage.WholeArmorMax) * 4f;
            ai.TakeCollisionHit(overkill, overkill, ai.GlobalPosition, 0);
            var commandAtKill = ai.LastCommand;   // the lever/surface command the AI think left behind

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
            // The lever/surface freeze: FUN_004b82d0 zeroes neither on the original, and nothing
            // here writes _lastInput once Crashed, so the same struct should read back
            // bit-identical on every one of these frames, not just at the endpoints.
            bool commandDrifted = false;
            var commandAtDrift = commandAtKill;
            for (float t = 0f; t < 8f && handoverAt < 0f; t += Dt)
            {
                ai.SimStep(Dt);
                // Read between the two halves of the frame: Callback 16 fires inside Advance below
                // and samples the hull the step above has just moved.
                velAtHandover = ai.WorldVelocity;
                posAtHandover = ai.WorldPosition;
                var command = ai.LastCommand;
                if (!commandDrifted && (command.Pitch != commandAtKill.Pitch
                    || command.Roll != commandAtKill.Roll || command.Yaw != commandAtKill.Yaw
                    || command.Throttle != commandAtKill.Throttle))
                {
                    commandDrifted = true;
                    commandAtDrift = command;
                }
                rig.Advance(Dt);
                if (!model.Visible)
                    hullHiddenFrames++;
                if (healthy is { Visible: false } && wreck is { Visible: false })
                    blankFrames++;
                if (!ai.WreckFalling)
                    handoverAt = t + Dt;
            }

            var commandAtHandover = ai.LastCommand;
            ctx.Note($"lever/surface command at kill: throttle={commandAtKill.Throttle:0.000} pitch={commandAtKill.Pitch:0.000} roll={commandAtKill.Roll:0.000} yaw={commandAtKill.Yaw:0.000}; at handover ({handoverAt:0.00} s): throttle={commandAtHandover.Throttle:0.000} pitch={commandAtHandover.Pitch:0.000} roll={commandAtHandover.Roll:0.000} yaw={commandAtHandover.Yaw:0.000}");
            ctx.Check(!commandDrifted,
                $"the lever/surface command stayed frozen every frame of the dead-hull flight (else it moved to throttle={commandAtDrift.Throttle:0.000} pitch={commandAtDrift.Pitch:0.000} roll={commandAtDrift.Roll:0.000} yaw={commandAtDrift.Yaw:0.000})");
            ctx.Check(commandAtHandover.Throttle == commandAtKill.Throttle
                && commandAtHandover.Pitch == commandAtKill.Pitch
                && commandAtHandover.Roll == commandAtKill.Roll
                && commandAtHandover.Yaw == commandAtKill.Yaw,
                $"…and reads back bit-identical at the handover (BL-451)");

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
            // The arm that tells freezing from neutralising: a default input bleeds speed (measured
            // 82 to 58 m/s), a frozen throttle holds it. ⚠ Speed RETAINED, never a downrange
            // distance: the recordings' 175 m may not contest a decode (docs/org/flightModel.md).
            float retained = velAtKill.Length() > 0.01f
                ? velAtHandover.Length() / velAtKill.Length() : 0f;
            ctx.Check(retained >= 0.95f,
                $"the wreck flew its FROZEN commands, not neutral ones: it kept {retained * 100f:0}% of its speed ({velAtKill.Length():0} → {velAtHandover.Length():0} m/s, want 95% or better)");
            ctx.Check(rig.InheritedWorldVelocity.IsEqualApprox(velAtHandover)
                      && velAtHandover != Vector3.Zero,
                $"Callback 16 handed the anim the velocity the wreck had reached inherited={rig.InheritedWorldVelocity} wreck={velAtHandover}");
            // The magnitude of the change, not its sign: what this proves is that the sample is the
            // handover's rather than the kill's, and whether the hull gained or lost speed getting
            // there is the frozen throttle's business, not this claim's.
            ctx.Check(Mathf.Abs(velAtKill.Length() - rig.InheritedWorldVelocity.Length()) > 5f,
                $"…sampled at the handover, not at the kill: it went from {velAtKill.Length():0.0} to {rig.InheritedWorldVelocity.Length():0.0} m/s over those seconds");
            ctx.Check(ai.LastCrashDef == null,
                $"nothing played the ground-impact family on the way down def={ai.LastCrashDef ?? "-"}");

            // Past the handover the hull is the anim's: the flight model must not still be moving it,
            // or the wreck and its ObjectMotion would fly the same node apart.
            var afterHandover = ai.WorldPosition;
            for (int i = 0; i < 60; i++)
                ai.SimStep(Dt);
            ctx.Check(ai.WorldPosition.IsEqualApprox(afterHandover),
                $"and the flight model stopped moving it once released moved={ai.WorldPosition.DistanceTo(afterHandover):0.00} m");

            // What carries it from here is IMPACT_FORCE, and randomdestseq's MAIN_ROOT_NODE is the
            // install's clearest carrier: it authors translation (0,0,0), so every metre downrange
            // below is inherited. ⚠ A wreck that drops vertically means the gate refused it.
            var wreckFrom = ai.CrashAnchor?.GlobalPosition ?? Vector3.Zero;
            for (int i = 0; i < 60; i++)
                rig.Advance(Dt);
            var wreckTo = ai.CrashAnchor?.GlobalPosition ?? Vector3.Zero;
            float downrange = new Vector3(wreckTo.X - wreckFrom.X, 0f, wreckTo.Z - wreckFrom.Z).Length();
            ctx.Note($"the wreck carried {downrange:0} m downrange in the second after the handover, off {velAtHandover.Length():0} m/s inherited");
            ctx.Check(downrange > 20f,
                $"the authored IMPACT_FORCE gate ADMITTED the hull's momentum: {downrange:0} m downrange in that second (want over 20; unflagged it would be 0)");
        }
        finally
        {
            ai?.Free();
        }
    }

    // The stages the hull was wearing when it died. Both AI stage anims are LOOP −1 with no authored
    // exit, so the only thing that can end them is the destroy def's own Callback 15; a stage left
    // live keeps emitting at whatever node the dead hull left behind.
    // ⚠ Balance STARTS against FINISHES, never a puffer count: an emitter with nothing left to emit
    // reads as quiet for a while and then resumes.
    private static void StagedEffectsEndAtTheDeath(TestContext ctx, FlightRoster spawner,
        float fallFrom, string planeName, float lane)
    {
        const float Dt = 1f / 60f;
        FlightController? ai = null;
        try
        {
            var spawn = new Vector3(lane, fallFrom, 0f);
            ai = spawner.SpawnAi(new AiSpawn(planeName, spawn, spawn + Vector3.Forward,
                AiPilot.HoldingCourse(spawn, spawn + Vector3.Forward)));
            if (ai.CrashRuntime is not { } rig || ai.Damage is not { } damage)
            {
                ctx.Check(false, $"the staged rig spawned with a crash runtime and a damage ledger");
                return;
            }

            rig.ManualAdvance = true;
            var live = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            rig.OnInstanceStarted += (def, _) =>
            {
                string n = def.AnimName ?? def.Name ?? "";
                live[n] = live.TryGetValue(n, out var c) ? c + 1 : 1;
            };
            rig.OnInstanceFinished += (def, _) =>
            {
                string n = def.AnimName ?? def.Name ?? "";
                live[n] = live.TryGetValue(n, out var c) ? c - 1 : -1;
            };

            // The ladder walked down through the production take-hit entry, as AiDamageStages does,
            // so the aircraft is wearing both stage anims when the kill lands.
            ai.TakeCollisionHit(damage.WholeArmor, 0f, ai.GlobalPosition, 0);
            foreach (float frac in new[] { 0.9f, 0.7f, 0.55f, 0.47f, 0.42f, 0.3f, 0.2f })
                SpendHullTo(ai, damage, frac);
            int stagedAtKill = Count(live, "pfsmoketrail") + Count(live, "random_remote_damage");
            ctx.Check(Count(live, "pfsmoketrail") > 0 && Count(live, "random_remote_damage") > 0,
                $"{planeName}: the wounded hull is wearing {stagedAtKill} live stage instance(s) at the kill ({Count(live, "pfsmoketrail")} pfsmoketrail, {Count(live, "random_remote_damage")} random_remote_damage)");

            float overkill = (damage.WholeHealthMax + damage.WholeArmorMax) * 4f;
            ai.TakeCollisionHit(overkill, overkill, ai.GlobalPosition, 0);
            for (float t = 0f; t < 5f; t += Dt)
            {
                ai.SimStep(Dt);
                rig.Advance(Dt);
            }
            ctx.Check(!ai.WreckFalling,
                $"…the fall ran past the def's 3.0 s gate, so Callback 15 has fired falling={ai.WreckFalling}");
            int stagedAfter = Count(live, "pfsmoketrail") + Count(live, "random_remote_damage");
            ctx.Same(0, stagedAfter,
                $"…and Callback 15 ended every stage the dead hull was wearing: {stagedAfter} still live ({Count(live, "pfsmoketrail")} pfsmoketrail, {Count(live, "random_remote_damage")} random_remote_damage)");
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
            var live = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            rig.OnInstanceStarted += (def, _) =>
            {
                string n = def.AnimName ?? def.Name ?? "";
                live[n] = live.TryGetValue(n, out var c) ? c + 1 : 1;
            };
            rig.OnInstanceFinished += (def, _) =>
            {
                string n = def.AnimName ?? def.Name ?? "";
                live[n] = live.TryGetValue(n, out var c) ? c - 1 : -1;
            };

            // Wounded before it is killed, so the ground contact arrives with stages already
            // burning: the order the ladder and the death run in is what this arm's last check is
            // about.
            ai.TakeCollisionHit(damage.WholeArmor, 0f, ai.GlobalPosition, 0);
            foreach (float frac in new[] { 0.9f, 0.7f, 0.55f, 0.47f, 0.42f, 0.3f, 0.2f })
                SpendHullTo(ai, damage, frac);
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

            // The landing does NOT cancel the destroy def, so its Callback 15 still arrives at the
            // authored 3.0 s and the stages this hull was wearing end there — the ground reaching
            // the wreck first must not cost it its stop (BL-422).
            for (float t = landedAt; t < 4f; t += Dt)
            {
                ai.SimStep(Dt);
                rig.Advance(Dt);
            }
            int stagedAfter = Count(live, "pfsmoketrail") + Count(live, "random_remote_damage");
            ctx.Same(0, stagedAfter,
                $"…and the stages it was wearing ended anyway: {stagedAfter} still live ({Count(live, "pfsmoketrail")} pfsmoketrail, {Count(live, "random_remote_damage")} random_remote_damage)");
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

            // Callback 3 would leave the crash view that Destroy already selected, so it stays
            // counted rather than overriding CSVM's crash-camera choreography.
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
            // ⚠ The eject hides the SEATED pilot, never the parachutist's body — both nodes are
            // named `pilot`, and the staged chute copy hangs under the crash root `cpeject1`
            // anchors on.
            var seatedPilot = Find(planeModel, "pilot");
            var chutePilot = chuteman != null ? Find(chuteman, "pilot") : null;
            ctx.Check(seatedPilot is { Visible: false } && chutePilot is { Visible: true },
                $"{planeName}: the seat is empty and the man under the canopy has a body seated={(seatedPilot?.Visible.ToString() ?? "-")} chute={(chutePilot?.Visible.ToString() ?? "-")}");
            ctx.Note($"{planeName}: eject at t={ejectAt:0.00} s, breakup at t={breakupAt:0.00} s");
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
