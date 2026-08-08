using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Effects;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Mech3.Anim;
using CSVM.UI;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>
/// The registered <c>--run-tests</c> suites. Each one asserts on a <see cref="Probes"/> verdict or
/// on live engine state; none of them re-implements a check the inspection reports already do.
///
/// <para>Expected counts here are <b>golden numbers measured against the retail install</b> — the
/// data is a fixed input, so 48 weapon defs and 210 C1 destructibles are invariants, not
/// guesses. A suite whose data is absent skips rather than passing.</para>
/// </summary>
public static class Suites
{
    private const int PlayerAirframes = 11;
    private const int WeaponDefCount = 48;

    /// <summary>Gun-group slots <see cref="Loadout.ForRig"/> seats on any airframe — the
    /// weapon bench fires every gun from all of them, so a drop here would quietly shrink its
    /// coverage without changing the 48/48 line.</summary>
    private const int RigGunGroups = 4;

    /// <summary>Destructible instances / distinct node groups per chapter, at each chapter's
    /// default mission. Instances exceed node groups where a reader wildcard def and its compiled
    /// per-instance twin bind the same nodes.
    ///
    /// <para>Both columns are far below what plain NAME matching yields, and that is the point: a
    /// compiled def binds the ONE instance its symbol table names (<c>AnimRuntime.Anchors</c>), so
    /// a mission's zeppelin defs never register every other zeppelin in the shared chapter
    /// gamez as a destructible of the same name. Every group the narrowing removes sits on an
    /// object the loaded mission authors no def for.</para></summary>
    private static readonly (string Chapter, int Instances, int Anchors)[] Census =
    {
        ("C1", 210, 143),
        ("C1B", 29, 29),
        ("C1C", 28, 28),
        ("C2", 200, 133),
        ("C2B", 28, 28),
        ("C3", 221, 147),
        ("C4", 92, 67),
        ("C5", 176, 112),
    };

    /// <summary>C1 textures spanning the three alpha classes the flatten must leave alone: opaque,
    /// hard cutout, and the soft overlays the builder alpha-blends.</summary>
    private static readonly string[] DropInSamples =
    {
        "lkzepskin", "grass1", "cloudlayer", "sky1", // no alpha channel
        "firtree1", "bush1",                          // hard cutouts
        "abld_shadow",                                // soft baked shadow overlay
    };

    public static void Register(List<TestHarness.Suite> into)
    {
        // Registered FIRST, deliberately: it is the only suite that installs a fake
        // IEmitterFactory, and TestContext.WithWorld caches one world per chapter — the chapter
        // it needs (C1, the only one shipping refuel* tanks) is also ctx.Chapter, the one every
        // other C1-touching suite below shares. Running first means it builds that shared world
        // while the fake is installed; damage-hd's collision:true immediately after forces a
        // rebuild with the real factory again (ctx.EmitterFactory is reset by then), so nothing
        // downstream ever sees the fake. See EmitterLifetime's own doc comment.
        into.Add(new TestHarness.Suite("emitter-lifetime",
            "a destructible's death starts a PUFFER_STATE emitter and BL-236's own retirement rule stops it", EmitterLifetime));
        into.Add(new TestHarness.Suite("puffer-modes",
            "every continuous emitter path through Emit/Stop (plus Burst), driven through a fake renderer with no GPU", PufferModes));
        into.Add(new TestHarness.Suite("loadout-bind",
            "every stock loadout binds to its model with every marker resolved", LoadoutBind));
        into.Add(new TestHarness.Suite("weapons-fire",
            "all 48 weapons mount and fire from a built plane", WeaponsFire));
        into.Add(new TestHarness.Suite("loadout-forrig",
            "Loadout.ForRig covers every firepoint/pylon on all 11 airframes, seeded from stock", LoadoutForRig));
        into.Add(new TestHarness.Suite("warning-shot",
            "the incoming-fire near-miss cue fires on another pilot's round, never on your own", WarningShot));
        into.Add(new TestHarness.Suite("blast-neighbor-shape",
            "splash falloff on a neighbour scores to its nearest collision-shape surface, not its " +
            "transform origin (BL-239)", BlastNeighborShape));
        into.Add(new TestHarness.Suite("air-to-air",
            "a round strikes the target plane's body, maps to the data part, moves armor/HP by the " +
            "weapon's own values, downs it on a critical zero with the kill attributed through the " +
            "Downed event — never hits the shooter's own geometry — and a rocket fuses on a passing " +
            "plane, blasting with falloff and attributing the kill", AirToAir));
        into.Add(new TestHarness.Suite("damage-stages",
            "each DAMAGE_SEQUENCE def fires its stage effects across an HP sweep", DamageStages));
        into.Add(new TestHarness.Suite("damage-hd",
            "weapon hits destroy, swap, drop colliders, and survive destroy→reset→destroy", DamageHd));
        into.Add(new TestHarness.Suite("stop-sequence",
            "authored STOP_SEQUENCE stops run: the fireball's 0.3 s stopper and the 30 s fire's halt", StopSequenceStops));
        into.Add(new TestHarness.Suite("death-slot",
            "a killed destructible dispatches its compiled destruction slot — the block carrying the 30 s fire's 1,035 death calls (BL-276)", DeathSlotDispatches));
        into.Add(new TestHarness.Suite("wait-for-completion",
            "a WAIT_FOR_COMPLETION call holds the caller's next event for its callee, and an unflagged one beside it does not (BL-228)", WaitForCompletion));
        into.Add(new TestHarness.Suite("emitter-host-deactivation",
            "a host going inactive spares the emitter that started in its own instant and still ends the one that did not (BL-229)", EmitterHostDeactivation));
        into.Add(new TestHarness.Suite("effect-template-mesh",
            "an effect's template meshes show at the call site — including a CALLED template's — and go dark when it ends (BL-061)", EffectTemplateMesh));
        into.Add(new TestHarness.Suite("effects-census",
            "the full --effects-test sweep as verdicts: every effect resolves, template meshes show at the CALL SITE (not the stage origin), and none stays lit after its stop", EffectsCensus));
        into.Add(new TestHarness.Suite("bounce-launch",
            "a bounce-terminated OBJECT_MOTION flies its solved parabola and fires its BOUNCE_SEQUENCE on landing", BounceLaunch));
        into.Add(new TestHarness.Suite("ground-contact",
            "a do_intersections OBJECT_MOTION is cut short by real geometry, rests on the surface and picks its BOUNCE_SEQUENCE branch from what it struck — and does none of it without a mask", GroundContact));
        into.Add(new TestHarness.Suite("nulled-launch",
            "an OBJECT_MOTION naming NEITHER RUN_TIME nor BOUNCE_SEQUENCE flies its solved parabola before its own deactivation switches it off (BL-257)", NulledLaunch));
        into.Add(new TestHarness.Suite("destructible-census",
            "per-chapter destructible registry totals", DestructibleCensus));
        into.Add(new TestHarness.Suite("tex-dropin",
            "the census/override flatten repaints RGB and changes nothing else", TexDropIn));
        into.Add(new TestHarness.Suite("gltf-export",
            "the viewer plane exports to glTF and re-imports with a textured mesh", GltfExport));
        into.Add(new TestHarness.Suite("collision-visibility",
            "nothing a chapter hides is left solid: no enabled collider under an invisible node", CollisionVisibility));
        into.Add(new TestHarness.Suite("nodelab-visibility",
            "the node lab's tree row follows live Visible, not the hide button's last action", NodeLabVisibility));
        into.Add(new TestHarness.Suite("trail-world-anchor",
            "a trail emitter under a rotated carrier anchors at world identity and drops puffs where it is fed", TrailWorldAnchor));
        into.Add(new TestHarness.Suite("damage-template-pool",
            "a second panel's tear takes its own pooled gimmeflakes copy and leaves the first burst flying at its site (BL-288)", DamageTemplatePool));
        into.Add(new TestHarness.Suite("crash-rig-anchors",
            "binding the crash rig leaves the airframe model under the controller — even the Devastator, whose model root shares the crash defs' authored NAME — and stages every pooled copy in the same reset pose", CrashRigAnchors));
    }

    // ---- emitter lifetime is observable with no GPU ---------------------------------------------

    /// <summary>Kills a <c>refuel*</c> tank with a <see cref="CountingEmitterFactory"/>
    /// installed and asserts on its <c>fire_n_smoke</c> emitter — the def whose <c>ACTIVE_STATE 1</c>
    /// carries no authored stop of its own, so only the instance-retirement rule
    /// (<see cref="AnimRuntime.Retirable"/> → <c>FinishEffectInstance</c> → <see cref="EmitterDirector.EndFor"/>)
    /// ever ends it.
    ///
    /// <para>Asserts THREE distinct facts, not one — the traps this family's bugs shipped
    /// through: the fake was actually reached (a name census, not a count — several runtimes could
    /// otherwise mask each other); the emitter started (the census reads a row that is
    /// <see cref="EmitterCensusRow.Emitting"/>); and once the death instance retires, it stops
    /// WITHOUT being forgotten (the row is still present, `Emitting` false) — `EndFor`'s disposition,
    /// never `Discard`'s. A suite reading only "stopped" cannot tell a correct pause from the emitter
    /// having been torn down by the wrong selector, which is exactly how this family's bugs
    /// shipped.</para></summary>
    private static void EmitterLifetime(TestContext ctx)
    {
        const string chapter = "C1";   // the only chapter shipping refuel* (5 defs) — matches bounce-launch
        const string pufferName = "fire_n_smoke";
        var fake = new CountingEmitterFactory();
        ctx.EmitterFactory = fake;
        try
        {
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

                if (tank.Status == DestructibleRegistry.State.Destroyed)
                {
                    runtime.ResetDestructible(tank);
                }

                var before = new HashSet<string>(runtime.Emitters.Census.Select(r => r.Name));
                ctx.Check(!before.Contains(pufferName), $"{pufferName} is not yet known before the kill");

                runtime.DamageAt(tank.Anchor, tank.MaxHealth + 1f);
                ctx.Check(fake.Built.Count > 0, $"the death reached the fake factory count={fake.Built.Count}");

                bool sawEmitting = false;
                const float tick = 1f / 60f;
                for (int i = 0; i < 600; i++)   // 10 s, comfortably past the ~5 s the tank's own death instance takes to retire
                {
                    runtime.Advance(tick);
                    if (runtime.Emitters.Census.Any(r => r.Name == pufferName && r.Emitting))
                    {
                        sawEmitting = true;
                    }
                }
                ctx.Check(sawEmitting, $"{pufferName} started emitting after the kill");

                bool rowPresent = runtime.Emitters.Census.Any(r => r.Name == pufferName);
                ctx.Check(rowPresent, $"{pufferName}'s emitter row survives the kill — EndFor pauses, it does not forget");

                bool stillEmitting = rowPresent
                    && runtime.Emitters.Census.First(r => r.Name == pufferName).Emitting;
                ctx.Check(!stillEmitting,
                    $"{pufferName} stopped once the death instance retired (BL-236's own rule)");
            });
        }
        finally
        {
            ctx.EmitterFactory = null;
        }
    }

    // ---- E15b: the emitter's own modes, with no GPU ---------------------------------------------

    /// <summary>Drives every <see cref="Puffer"/> emission path through the collapsed
    /// <c>Emit</c>/<c>Stop</c> pair (plus the still-separate <c>Burst</c>) over a
    /// <see cref="RecordingEmitterRenderer"/> — the seam `E15b` cut so that 847 lines reachable by
    /// nothing could be reached by something. No atlas, no <c>TextureArchive</c> and no
    /// <c>MultiMesh</c> is constructed anywhere in this suite, which is also its own tripwire: if it
    /// ever gets slow, something started building real emitters again.
    ///
    /// <para>Both states are read from the shipped readers rather than written here, so every
    /// expected number below is derived from authored data: <c>flame_ball.json</c>'s
    /// <c>fierypuffer</c> (TIME_INTERVAL 0.2, NUMBER 18, LIFETIME 0.8–1.0, a six-frame
    /// TEXTURE_SEQUENCE, no COLORS) and <c>pufftrails.json</c>'s <c>smokepuffer</c>
    /// (DISTANCE_INTERVAL 2, LIFETIME 1.5–4.5, a COLORS ramp — and NO authored TIME_INTERVAL or
    /// NUMBER, so its still-host cadence is the parsers' synthetic 0.1 s at one puff per batch,
    /// asserted below because the sputter counts derive from it).</para>
    ///
    /// <para>⚠ The suite detaches <see cref="GameClock.Current"/> for its duration. <c>_Process</c>
    /// takes its dt from the clock when one is installed, and the harness's clock is a FixedStep one
    /// nothing is stepping — <c>FrameDt</c> is 0, so every tick would advance no sim at all and
    /// every check below would pass vacuously.</para></summary>
    private static void PufferModes(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr effect readers");

        var burstState = PufferState.Load(ctx.ZrdrPath, "flame_ball.json", "fierypuffer");
        var trailState = PufferState.Load(ctx.ZrdrPath, "pufftrails.json", "smokepuffer");
        ctx.Check(burstState != null, $"flame_ball.json defines fierypuffer");
        ctx.Check(trailState != null, $"pufftrails.json defines smokepuffer");
        if (burstState == null || trailState == null)
            return;

        // The authored inputs every expected count below is derived from. Asserted rather than
        // assumed: a reader change must fail here, naming itself, rather than silently re-baselining
        // the mode assertions that read off it.
        ctx.Same(18, burstState.Number, $"fierypuffer NUMBER");
        ctx.Check(Mathf.IsEqualApprox(0.2f, burstState.TimeInterval), $"fierypuffer TIME_INTERVAL is 0.2 s");
        ctx.Check(Mathf.IsEqualApprox(1f, burstState.LifetimeMax), $"fierypuffer LIFETIME_RANGE max is 1 s");
        ctx.Same(6, burstState.TextureSequence.Count, $"fierypuffer flipbook frames");
        ctx.Same(0, burstState.Colors.Count, $"fierypuffer has no COLORS ramp");
        ctx.Check(Mathf.IsEqualApprox(2f, trailState.DistanceInterval), $"smokepuffer DISTANCE_INTERVAL is 2 m");
        ctx.Check(trailState.Colors.Count > 0, $"smokepuffer carries a COLORS ramp");
        ctx.Check(Mathf.IsEqualApprox(0.1f, trailState.TimeInterval),
            $"smokepuffer authors no TIME_INTERVAL — the still-host cadence is the synthetic 0.1 s");
        ctx.Same(1, trailState.Number, $"smokepuffer authors no NUMBER — a still-host batch is one puff");

        var clock = GameClock.Current;
        GameClock.Current = null;
        try
        {
            PufferBurstMode(ctx, burstState);
            PufferSustainMode(ctx, burstState);
            PufferTrailMode(ctx, trailState);
            PufferStillSputter(ctx, trailState);
            PufferStaticBurn(ctx, trailState);
            PufferStopRevive(ctx, trailState);
        }
        finally
        {
            GameClock.Current = clock;
        }
    }

    /// <summary>Burst: the pool is sized from the calling animation's stop time, the first batch is
    /// spawned at t = 0 rather than one interval in, the flipbook walks its whole sequence, and the
    /// emitter puts itself away once the last particle dies.</summary>
    private static void PufferBurstMode(TestContext ctx, PufferState state)
    {
        var gpu = new RecordingEmitterRenderer();
        var puffer = Puffer.CreateWith(state, gpu, activeDuration: 0.3f);
        ctx.Host.AddChild(puffer);
        try
        {
            // 0.3 s of emission at one batch per 0.2 s = batches at t=0 and t=0.2, so 2 × NUMBER.
            ctx.Same(36, gpu.Capacity, $"burst pool = NUMBER × the batches 0.3 s of emission fits");
            ctx.Check(gpu.CullMargin >= 4f, $"burst emitter pads its cull margin margin={gpu.CullMargin}");

            puffer.Burst(new Vector3(0f, 500f, 0f));
            puffer._Process(1f / 60f);
            ctx.Same(18, gpu.Shown, $"burst spawns its first batch at t=0, not one interval in");
            ctx.Check(gpu.LastFrame.Count > 0 && gpu.LastFrame.All(p => p.Alpha < 1f),
                $"a ramp-less state fades in on the life envelope rather than drawing at full alpha");

            for (int i = 0; i < 6; i++)   // 0.3 s: comfortably past the second batch at 0.2 s
                puffer._Process(0.05f);
            ctx.Same(36, gpu.Shown, $"burst's second batch lands one TIME_INTERVAL in");

            for (int i = 0; i < 30; i++)  // 1.5 s: past the last batch's 1 s lifetime
                puffer._Process(0.05f);
            ctx.Same(0, gpu.Shown, $"burst ends when its last particle dies");
            ctx.Check(!puffer.Visible, $"a finished burst hides itself");
            ctx.Same(5, (long)gpu.MaxFrame, $"the flipbook reaches its last column (TEXTURE_SEQUENCE keys are life fractions)");
            ctx.Check(gpu.MaxIndex < gpu.Capacity, $"burst never writes past its pool max={gpu.MaxIndex} pool={gpu.Capacity}");
        }
        finally
        {
            puffer.Free();
        }
    }

    /// <summary>A TIME_INTERVAL state through <c>Emit</c>: the authored state, not the caller,
    /// picks the sustain path; the pool is sized to the steady-state population, emission starts
    /// on the very first frame, a long frame cannot overrun the pool, and <c>Stop</c> ends
    /// emission without cutting the live particles short.</summary>
    private static void PufferSustainMode(TestContext ctx, PufferState state)
    {
        var gpu = new RecordingEmitterRenderer();
        var puffer = Puffer.CreateWith(state, gpu, sustained: true);
        ctx.Host.AddChild(puffer);
        try
        {
            // ceil(NUMBER × LIFETIME_max / TIME_INTERVAL) + NUMBER = ceil(18 × 1 / 0.2) + 18.
            ctx.Same(108, gpu.Capacity, $"sustain pool = the steady-state population, not a burst count");

            var origin = new Vector3(0f, 800f, 0f);
            puffer.Emit(origin, Basis.Identity, 1f / 60f);
            puffer._Process(1f / 60f);
            ctx.Same(18, gpu.Shown, $"a time state through Emit sustains, on its very first frame");
            ctx.Check(gpu.LastFrame.All(p => p.Position.DistanceTo(origin) < 3f),
                $"sustained particles spawn at the world point they are driven at, not at the node");

            // A 5 s hitch asks for 25 batches; the catch-up cap and the pool between them must keep
            // that inside 108. Integrated at a normal dt so the spawns are observable before they age
            // out — the cap is a spawn-path rule, and this is where it is read.
            puffer.Emit(origin, Basis.Identity, 5f);
            puffer._Process(1f / 60f);
            ctx.Same(108, gpu.Shown, $"a 5 s hitch fills the pool and stops there");
            ctx.Check(gpu.MaxIndex == gpu.Capacity - 1,
                $"the hitch reached the pool's last slot and no further max={gpu.MaxIndex} pool={gpu.Capacity}");

            puffer.Stop();
            puffer._Process(0.05f);
            ctx.Check(gpu.Shown > 0, $"Stop ends emission without clearing the live particles");
            for (int i = 0; i < 30; i++)  // 1.5 s, past LIFETIME_RANGE's 1 s
                puffer._Process(0.05f);
            ctx.Same(0, gpu.Shown, $"the live particles finish their own lifetimes and the emitter goes quiet");
        }
        finally
        {
            puffer.Free();
        }
    }

    /// <summary>A DISTANCE_INTERVAL state through <c>Emit</c>: the first call homes the trail and
    /// time-sputters one batch (the still-host rule — on the homing frame no motion has elapsed
    /// yet), a moving host emits one puff per interval of actual motion with the remainder carried
    /// across frames instead of rounded away.</summary>
    private static void PufferTrailMode(TestContext ctx, PufferState state)
    {
        var gpu = new RecordingEmitterRenderer();
        var puffer = Puffer.CreateWith(state, gpu);
        ctx.Host.AddChild(puffer);
        try
        {
            ctx.Same(640, gpu.Capacity, $"trail pool is the live-particle cap, not a burst count");

            puffer.Emit(new Vector3(0f, 600f, 0f), Basis.Identity, 1f / 60f);
            puffer._Process(1f / 60f);
            ctx.Same(1, gpu.Shown, $"the first call homes the trail and time-sputters one still-host batch");

            // 10 m at one puff per 2 m = 5 (+ the homing sputter), then 3 m more = 1 puff with
            // 1 m carried, not 2 rounded up.
            puffer.Emit(new Vector3(10f, 600f, 0f), Basis.Identity, 1f / 60f);
            puffer._Process(1f / 60f);
            ctx.Same(6, gpu.Shown, $"a moving host emits one puff per DISTANCE_INTERVAL of motion");
            ctx.Check(gpu.LastFrame.All(p => Mathf.IsEqualApprox(p.Alpha, 1f)),
                $"a COLORS ramp owns the fade, so the life envelope stays out of it");

            puffer.Emit(new Vector3(13f, 600f, 0f), Basis.Identity, 1f / 60f);
            puffer._Process(1f / 60f);
            ctx.Same(7, gpu.Shown, $"a partial interval carries into the next frame instead of rounding");
            ctx.Check(gpu.MaxIndex < gpu.Capacity, $"trail never writes past its pool max={gpu.MaxIndex} pool={gpu.Capacity}");
        }
        finally
        {
            puffer.Free();
        }
    }

    /// <summary>A distance state whose host stands still keeps the time cadence (the damaged
    /// building's sputter): the authored interval can never elapse, so <c>Emit</c> with no burn
    /// rate falls back to one batch per synthetic 0.1 s TIME_INTERVAL, at the held point.</summary>
    private static void PufferStillSputter(TestContext ctx, PufferState state)
    {
        var gpu = new RecordingEmitterRenderer();
        var puffer = Puffer.CreateWith(state, gpu);
        ctx.Host.AddChild(puffer);
        try
        {
            var p0 = new Vector3(0f, 900f, 0f);
            const float dt = 1f / 60f;
            for (int i = 0; i < 60; i++)   // 1 s held still — no particle dies (LIFETIME ≥ 1.5 s)
            {
                puffer.Emit(p0, Basis.Identity, dt);
                puffer._Process(dt);
            }
            ctx.Check(gpu.Shown >= 10 && gpu.Shown <= 12,
                $"a still host sputters on the 0.1 s time cadence, ~11 batches in 1 s shown={gpu.Shown}");
            ctx.Check(gpu.LastFrame.All(p => p.Position.DistanceTo(p0) < 3f),
                $"the sputter stays at the held point");
        }
        finally
        {
            puffer.Free();
        }
    }

    /// <summary>A host that CANNOT move (the damage lab's parked plane) declares a burn rate:
    /// <c>Emit</c> with <c>staticBurnMps</c> spends virtual metres at the held point — the
    /// authored per-metre density, not the time cadence — through the same carry as the moving
    /// trail.</summary>
    private static void PufferStaticBurn(TestContext ctx, PufferState state)
    {
        var gpu = new RecordingEmitterRenderer();
        var puffer = Puffer.CreateWith(state, gpu);
        ctx.Host.AddChild(puffer);
        try
        {
            var p0 = new Vector3(0f, 1000f, 0f);
            // 15 m/s × 0.2 s = 3 m/call over DISTANCE_INTERVAL 2: puff counts 1,2,1,2 as the
            // carry wraps — 6 total for 12 virtual metres. The time cadence over the same 0.8 s
            // would be 9 batches, so the count also proves which fallback ran.
            for (int i = 0; i < 4; i++)
            {
                puffer.Emit(p0, Basis.Identity, 0.2f, staticBurnMps: 15f);
                puffer._Process(0.2f);
            }
            ctx.Same(6, gpu.Shown, $"a declared burn rate spends virtual metres, not the time cadence");
            ctx.Check(gpu.LastFrame.All(p => p.Position.DistanceTo(p0) < 6f),
                $"the burn stays at the held point");
        }
        finally
        {
            puffer.Free();
        }
    }

    /// <summary>The pause + far revive, through <c>Stop</c> itself: a pooled effect-template slot
    /// is teleported to each new call site, so a distance-state emitter stopped at one blast and
    /// revived at the next must re-home there — a kept trail origin draws a puff line across the
    /// whole jump (the rocket-explosion ghost trails). <c>Stop</c> ends trail AND sustain
    /// unconditionally; the revive's first call sputters fresh at the new site.</summary>
    private static void PufferStopRevive(TestContext ctx, PufferState state)
    {
        var gpu = new RecordingEmitterRenderer();
        var puffer = Puffer.CreateWith(state, gpu, sustained: true);
        ctx.Host.AddChild(puffer);
        try
        {
            const float dt = 1f / 60f;
            var a = new Vector3(0f, 700f, 0f);
            puffer.Emit(a, Basis.Identity, dt);                              // homes at A
            puffer.Emit(a + new Vector3(10f, 0f, 0f), Basis.Identity, dt);   // trails 10 m
            puffer._Process(dt);
            ctx.Check(gpu.Shown > 0, $"the emitter trailed at the first site shown={gpu.Shown}");

            puffer.Stop();
            puffer.Stop();   // idempotent — a second stop is a no-op, not an error
            var b = a + new Vector3(1000f, 0f, 0f);
            puffer.Emit(b, Basis.Identity, dt);
            puffer._Process(dt);
            int strays = gpu.LastFrame.Count(p => p.Position.X > 20f && p.Position.X < 980f);
            ctx.Same(0, strays,
                $"a stopped trail revived at a far site re-homes there, no puff line across the jump");
            ctx.Check(gpu.LastFrame.Any(p => p.Position.DistanceTo(b) < 3f),
                $"the revive restarted emission fresh at the new site");
        }
        finally
        {
            puffer.Free();
        }
    }

    private static void LoadoutBind(TestContext ctx)
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
        }
        finally
        {
            textures.Dispose();
        }
    }

    // ---- needs a built plane in the tree --------------------------------------------------------

    private static void WeaponsFire(TestContext ctx)
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
            // ForRig seats 4 gun-group slots on every airframe (B4's own suite proves that across
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

    /// <summary>B4: <see cref="Loadout.ForRig"/> against all 11 player airframes — 4 gun groups
    /// covering every <c>firepointN</c> the rig actually carries (the Kestrel's odd 7th), one
    /// hardpoint per <c>pylonN</c>, no marker bound to two groups, and every synthesized group
    /// fireable even where stock marks the slot a turret.</summary>
    private static void LoadoutForRig(TestContext ctx)
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

    /// <summary>The fly-mode damage-trail regression: Godot's <c>TopLevel</c> toggle PRESERVES the
    /// node's global transform, so a trail emitter parented under a flying plane kept the plane's
    /// attitude-at-first-puff as its basis — and every "world-space" puff position was yawed around
    /// the world origin, kilometres off at a real mission spawn (invisible at any heading except the
    /// identity -Z, which is why the parked viewer and every scripted -Z dive looked fine). The
    /// suite feeds a trail under a carrier rotated to the C1 spawn heading and parked at the C1
    /// spawn coordinates, then asserts the emitter re-anchored to world identity and the rendered
    /// instance sits at the fed segment, not swung around the origin.</summary>
    private static void TrailWorldAnchor(TestContext ctx)
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

    /// <summary>Exports a built plane to a temp <c>.glb</c> and asserts the file lands and re-imports
    /// with at least one textured mesh — the round trip the viewer's <c>--export-gltf=</c>/F10 path
    /// relies on, including that the shader skins convert to a glTF-serializable material.</summary>
    /// <summary>The incoming-fire near-miss cue's wiring, with its able-to-fail baseline:
    /// a real round from another pilot flying past registers a pass, the SAME round fired by the
    /// target's own identity registers none, and a round on a track a hundred metres wide of the
    /// aircraft registers none either — so a pass count of 1 means the geometry, not a threshold
    /// wide enough to catch anything. The accumulator's own arithmetic is unit-tested off-engine
    /// (WarningShotCueTests); this is the pool half, on real ballistics.</summary>
    private static void WarningShot(TestContext ctx)
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

            // A round overtaking the aircraft 3 m abeam, fired 60 m astern along +Z. Short enough
            // that the weapon's own 6° CANNON_SPREAD cone cannot throw it past the trigger radius.
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

    /// <summary>The neighbour-splash repro pair, on controlled geometry: a torpedo detonates against a thin
    /// wall placed right next to one end of a long neighbouring body. The neighbour's transform
    /// origin sits well OUTSIDE the blast radius (the bug's exact symptom — origin-scored falloff
    /// reads zero splash), while its near face sits well inside it, so a real fix must score it
    /// as taking measurable splash. The struck wall's own direct-hit damage must stay byte-identical
    /// (full, unscaled) either way — this suite is disjoint from `weapon-blast`, which only checks
    /// the pure falloff curve, not the neighbour-scoring geometry.</summary>
    private static void BlastNeighborShape(TestContext ctx)
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

    /// <summary>The air-to-air hit chain (VS-mode wave A) with its able-to-fail negative case. Two
    /// real flight rigs (built plane, PlaneCollider boxes, AircraftBody, per-part PlaneDamage) in
    /// an otherwise empty world, driven purely on manual sim steps — deterministic, no wall clock.
    /// Pins: a physics ray at the fuselage returns the aircraft body and its struck shape maps to
    /// a Parts entry; a scripted round hits, `MapStruckPart` names the expected part (nose), and
    /// armor moves by the weapon's own ARMOR_DAMAGE while health waits behind it (armor-first);
    /// sustained fire zeroes the critical nose and triggers the real Crash; a crashed plane soaks
    /// no further rounds; and a burst fired through the shooter's OWN airframe registers zero
    /// self-hits — the regression that would otherwise arrive silently as "guns too strong".
    /// The Downed reports feed a real VersusMatch through the same forwarding GameSession
    /// uses: the weapon kill scores exactly the shooter, a wreck reports no second death, a
    /// killer-less crash and an unowned round's kill each tally a death and score nobody. The tail
    /// pins the VS respawn loop: without AutoRespawnAfter a crash waits for R; armed at the
    /// session's 3 s it auto-respawns at that mark in sim frames, and the respawn reports nothing.
    /// The rocket phases pin the proximity fuse and blast: a rocket crossing a fixed gap
    /// ahead of the target's nose fuses there and blasts the nose by exactly the linear falloff at
    /// that gap; a second plane farther inside the radius takes less, a plane outside it nothing;
    /// sustained fused passes down the target with the kill attributed through the same Downed
    /// seam; a wreck neither fuses a round nor soaks blast; and a rocket fired from INSIDE its own
    /// shooter's boxes never self-fuses or self-damages, flying on to fuse on the opponent.</summary>
    private static void AirToAir(TestContext ctx)
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
            ctx.Check(ProjectilePool.ClassifySurface(target.Body) == SurfaceClass.Player,
                $"an aircraft body classifies as the player IMPACT surface");

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

            // A1's core claim, straight off the space state: a ray at the fuselage returns the
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

            // --- the negative case FIRST, while both airframes are pristine: a burst from 14 m
            // behind the shooter's own tail, fired forward through its whole airframe (tail →
            // fuselage → nose, world -Z), owned by that same pilot. Broken owner exclusion turns
            // several of these into self-hits; correct exclusion registers none.
            var selfMuzzle = new Transform3D(Basis.Identity, shooterPos + new Vector3(0f, 0f, 14f));
            for (int i = 0; i < 25; i++)
                live.Spawn(gun, selfMuzzle, Vector3.Zero, shooter.PlayerIndex);
            for (int i = 0; i < 60; i++)
                live.SimStep(1f / 60f);
            live.Clear();
            ctx.Check(Pristine(shooter) && !shooter.Crashed,
                $"a burst through the shooter's own geometry registers zero self-hits");
            ctx.Check(Pristine(target), $"the abeam burst touched nothing else");

            // --- the measured hits: single rounds from 10 m ahead of the target's nose, on the
            // centerline, fired by the opposing identity. Each registering round spends exactly
            // ARMOR_DAMAGE from the nose pool; health waits behind the armor (armor-first), and
            // no other part moves — which is MapStruckPart naming the right part.
            var noseMuzzle = new Transform3D(
                Basis.LookingAt(Vector3.Back, Vector3.Up), targetPos + new Vector3(0f, 0f, -10f));
            var noseState = target.Damage!.Parts["nose"];
            for (int shot = 1; shot <= 2; shot++)
            {
                // One round per attempt: a CANNON_SPREAD deviation can miss the box from any
                // range, so retry a clean miss (armor unmoved) — but a REGISTERING round must
                // move the pool by exactly one ARMOR_DAMAGE quantum, which is the assertion.
                float before = noseState.Armor;
                int tries = 0;
                while (noseState.Armor >= before && tries < 5)
                {
                    tries++;
                    FireOne(noseMuzzle, shooter.PlayerIndex, 10);
                }
                if (tries > 1)
                    ctx.Note($"shot {shot} needed {tries} rounds (spread misses)");
                ctx.Check(Mathf.IsEqualApprox(noseState.Armor, nose.MaxArmor - shot * armorDmg),
                    $"shot {shot}: nose armor moved by the weapon's ARMOR_DAMAGE armor={noseState.Armor:0.##} expected={nose.MaxArmor - shot * armorDmg:0.##}");
                ctx.Check(Mathf.IsEqualApprox(noseState.Hp, nose.MaxHp),
                    $"shot {shot}: health untouched while armor absorbs hp={noseState.Hp:0.##}");
            }
            ctx.Check(target.Damage.Parts.Values.All(
                    p => p.Def == nose || (p.Hp >= p.Def.MaxHp && p.Armor >= p.Def.MaxArmor)),
                $"no part but the nose moved");

            // --- the kill: keep firing the same bearing until the critical nose zeroes. The
            // round budget is derived from the data (armor + health over the per-round values),
            // tripled for spread misses.
            int budget = (int)(nose.MaxArmor / armorDmg + nose.MaxHp / healthDmg) * 3 + 20;
            int fired = 0;
            while (!target.Crashed && fired < budget)
            {
                fired++;
                FireOne(noseMuzzle, shooter.PlayerIndex, 8);
            }
            ctx.Check(target.Crashed,
                $"sustained fire zeroes the critical nose and triggers Crash rounds={fired}/{budget}");
            ctx.Check(noseState.Hp <= 0f, $"the nose health pool is empty hp={noseState.Hp:0.##}");
            ctx.Note($"kill took {fired} rounds of {gun.Id} (armor {nose.MaxArmor:0}/{armorDmg:0.#}, hp {nose.MaxHp:0}/{healthDmg:0.#})");
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

            // An unowned round (NoShooter — nobody's identity) that downs the plane is likewise
            // a death with no killer, never a kill.
            target.Respawn();
            fired = 0;
            while (!target.Crashed && fired < budget)
            {
                fired++;
                FireOne(noseMuzzle, ProjectilePool.NoShooter, 8);
            }
            ctx.Check(target.Crashed, $"the unowned burst downed the plane rounds={fired}/{budget}");
            ctx.Check(match.DeathsOf(1) == 3 && match.KillsOf(0) == 1 && match.KillsOf(1) == 0,
                $"an unowned round's kill is a death with no killer deaths(P2)={match.DeathsOf(1)} kills={match.KillsOf(0)}/{match.KillsOf(1)}");

            // The VS respawn loop, in sim frames. Default (AutoRespawnAfter null): a crash
            // waits for R — 4 s of crash-cam sim steps respawn nothing.
            for (int i = 0; i < 240; i++)
                target.SimStep(1f / 60f);
            ctx.Check(target.Crashed, $"without AutoRespawnAfter a crash waits for R (still down after 4 s)");

            // Armed at 3 s (what the session sets per rig in --vs): the timer runs from Crash in
            // sim frames — still down just short of the mark, flying again within a frame or two
            // of it (the 180 × 1/60f float subtractions leave the exact frame a knife-edge), and
            // the respawn itself reports no death.
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

            // ---- B14: the proximity fuse arms on aircraft and blast damage reaches them ----
            // Data-driven pick: a dumbfire, spread-free rocket whose blast radius comfortably
            // exceeds its fuse trigger distance (the flak profile), so a fused detonation still
            // lands damage inside the linear falloff; the fuse range must also clear the suite's
            // fixed pass gap. Equal ARMOR/HEALTH magnitudes make the combined armor+HP delta equal
            // the scaled magnitude regardless of how much armor is left (the carry-over rule).
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

            // --- the attributed blast kill: fused passes across the critical nose until it
            // zeroes. The Downed report carries the shooter and the match scores it — the same
            // seam the gun kill used. Counters entering this phase: kills(P1)=1, deaths(P2)=4
            // (the gun kill, the killer-less crash, the unowned kill, B13's forced crash).
            target.Respawn();
            int rockets = 0;
            int rocketBudget = (int)((nose.MaxArmor + nose.MaxHp) / expectedBlast) + 6;
            while (!target.Crashed && rockets < rocketBudget)
            {
                rockets++;
                FireRocket(crossMuzzle, shooter.PlayerIndex);
            }
            ctx.Check(target.Crashed,
                $"sustained fused passes zero the critical nose rockets={rockets}/{rocketBudget}");
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
        }
        finally
        {
            pool?.Free();
            target?.Free();
            shooter?.Free();
            bystander?.Free();
            textures.Dispose();
        }
    }

    private static void GltfExport(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        Node3D? plane = null;
        string path = Path.Combine(ctx.ScratchDir, $"gltf-export-{ctx.PlaneName}.glb");
        try
        {
            Directory.CreateDirectory(ctx.ScratchDir);
            plane = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            ctx.Host.AddChild(plane);

            var err = GltfExporter.Export(plane, path);
            ctx.Same((long)Error.Ok, (long)err, $"export write result");
            bool wrote = File.Exists(path) && new FileInfo(path).Length > 0;
            ctx.Check(wrote, $"exported .glb is present and non-empty path={path}");

            // Re-import the file the exporter just wrote and count the textured meshes that survived
            // the material conversion — proves the shader skins became serializable StandardMaterials.
            if (wrote)
            {
                var doc = new GltfDocument();
                var state = new GltfState();
                var readErr = doc.AppendFromFile(path, state);
                ctx.Same((long)Error.Ok, (long)readErr, $"re-import read result");
                var scene = doc.GenerateScene(state) as Node3D;
                ctx.Check(scene != null, $"re-imported scene has a Node3D root");
                int textured = scene == null ? 0 : CountTexturedMeshes(scene);
                ctx.Check(textured >= 1, $"re-imported textured meshes count={textured}");
                scene?.Free();
            }
        }
        finally
        {
            plane?.Free();
            textures.Dispose();
        }
    }

    /// <summary>How many <see cref="MeshInstance3D"/> in the subtree carry a material with an albedo
    /// texture — the glTF importer hands each surface back a <see cref="StandardMaterial3D"/>.</summary>
    private static int CountTexturedMeshes(Node node)
    {
        int count = 0;
        if (node is MeshInstance3D mesh)
        {
            for (int i = 0; i < mesh.GetSurfaceOverrideMaterialCount(); i++)
            {
                if (mesh.GetActiveMaterial(i) is BaseMaterial3D { AlbedoTexture: not null })
                {
                    count++;
                    break;
                }
            }
        }
        foreach (var child in node.GetChildren())
        {
            count += CountTexturedMeshes(child);
        }
        return count;
    }

    // ---- needs a chapter world ------------------------------------------------------------------

    private static void DamageStages(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var r = Probes.Damage(world.Runtime, ctx.Chapter, "", damageHd: 0f);
            ctx.WriteArtifact($"test-damage-stages-{ctx.Chapter}.txt", r.Text);
            ctx.Check(r.Rows.Count > 0, $"chapter has DAMAGE_SEQUENCE defs chapter={ctx.Chapter} rows={r.Rows.Count}");
            foreach (var row in r.Rows)
            {
                ctx.Check(row.Resolved, $"deep descendant resolves back to its destructible def={row.Def}");
                ctx.Check(row.StagesFired > 0, $"HP sweep fires a stage effect def={row.Def} stages={row.StagesFired}");
            }
            ctx.Note($"{r.Summary}");
        });
    }

    /// <summary>The invisible-wall tripwire: after a chapter's world has bootstrapped — mission
    /// setup script, RESET_STATEs, ON_STARTUP, the unplaced sweep — no collider may still be
    /// enabled where nothing is drawn. Every chapter, because what each mission hides differs and
    /// the failure is silent until someone flies into it (C1/IA1's <c>hk_zep</c>).</summary>
    private static void CollisionVisibility(TestContext ctx)
    {
        foreach (var (chapter, _, _) in Census)
        {
            ctx.WithWorld(chapter, collision: true, world =>
            {
                var solid = Probes.InvisibleEnabledColliders(world.Session.Root);
                ctx.Same(0, solid.Count, $"{chapter} invisible-but-solid colliders");
                for (int i = 0; i < solid.Count && i < 8; i++)
                {
                    ctx.Note($"{chapter} solid where nothing is drawn: {solid[i]}");
                }
            });
        }
    }

    private static void DamageHd(TestContext ctx)
    {
        // Collision forced on: the collider census measures which destructible geometry is solid
        // and whether the death removes it, and a world built without collision censuses zero.
        ctx.WithWorld(ctx.Chapter, collision: true, world =>
        {
            var r = Probes.Damage(world.Runtime, ctx.Chapter, "", damageHd: 25f);
            ctx.WriteArtifact($"test-damage-hd-{ctx.Chapter}.txt", r.Text);
            ctx.Check(r.Rows.Count > 0, $"chapter has destructibles chapter={ctx.Chapter} rows={r.Rows.Count}");
            int destroyed = 0;
            foreach (var row in r.Rows)
            {
                ctx.Check(row.Resolved, $"deep descendant resolves back to its destructible def={row.Def}");
                ctx.Check(row.Destroyed, $"enough weapon hits destroy it def={row.Def} hits={row.Hits}");
                if (!row.Destroyed)
                {
                    continue;
                }
                destroyed++;
                ctx.Check(row.ResetHealthy == true, $"reset restores it def={row.Def}");
                ctx.Check(row.RekillMatched == true, $"rekill takes the same hits def={row.Def} hits={row.Hits}");
            }
            ctx.Same(r.Rows.Count, destroyed, $"destructibles destroyed by weapon hits");
            ctx.Note($"{r.Summary}");
            ctx.Note($"colliders world={r.CollidableMeshes} swept={r.Rows.Count} capped={r.Capped}");
        });
    }

    // ---- needs Godot's Image, nothing else -------------------------------------------------------

    private static void TexDropIn(TestContext ctx)
    {
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        using var textures = new TextureArchive(texturesPath);
        var colors = new Dictionary<string, Color>();
        foreach (string name in DropInSamples)
        {
            var img = textures.FindImage(name);
            ctx.Check(img != null, $"sample texture resolves texture={name}");
            if (img == null)
            {
                continue;
            }
            int w = img.GetWidth(), h = img.GetHeight();
            var format = img.GetFormat();
            bool hadAlpha = format is Image.Format.Rgba8 or Image.Format.La8 or Image.Format.Rgba4444;
            byte[] before = img.GetData();

            var color = TextureDropIn.ColorForName(name);
            colors[name] = color;
            TextureDropIn.Flatten(img, color);
            byte[] after = img.GetData();

            ctx.Check(img.GetWidth() == w && img.GetHeight() == h,
                $"flatten keeps the size texture={name} before={w}x{h} after={img.GetWidth()}x{img.GetHeight()}");
            ctx.Check(!img.HasMipmaps(), $"flatten adds no mipmaps texture={name}");
            var flatFormat = img.GetFormat();
            ctx.Check(flatFormat == (hadAlpha ? Image.Format.Rgba8 : Image.Format.Rgb8),
                $"flatten lands in the three-channel form of the original texture={name} was={format} now={flatFormat}");

            int stride = flatFormat == Image.Format.Rgba8 ? 4 : 3;
            ctx.Same(w * h * stride, after.Length, $"{name} flattened byte count");
            int wrongRgb = 0, wrongAlpha = 0;
            for (int i = 0; i + stride <= after.Length; i += stride)
            {
                if (after[i] != color.R8 || after[i + 1] != color.G8 || after[i + 2] != color.B8)
                {
                    wrongRgb++;
                }
                // Only comparable when the source was already the same layout; a converted source
                // has no byte-for-byte predecessor to check against.
                if (stride == 4 && format == Image.Format.Rgba8 && after[i + 3] != before[i + 3])
                {
                    wrongAlpha++;
                }
            }
            ctx.Same(0, wrongRgb, $"{name} texels not repainted to the flat colour");
            ctx.Same(0, wrongAlpha, $"{name} texels whose alpha the flatten moved");
        }
        // The identity every count report depends on: no two of these names share a colour, and
        // each keeps one channel pinned to full brightness.
        var seen = new Dictionary<string, string>();
        foreach (var (name, color) in colors)
        {
            string key = $"{color.R8},{color.G8},{color.B8}";
            ctx.Check(!seen.ContainsKey(key), $"census colour is unique texture={name} colour={key} clashes_with={(seen.TryGetValue(key, out var other) ? other : "-")}");
            seen[key] = name;
            ctx.Check(color.R8 == 255 || color.G8 == 255 || color.B8 == 255,
                $"census colour is full brightness texture={name} colour={key}");
        }
        ctx.Note($"{colors.Count} sample textures flattened, {seen.Count} distinct colours");
    }

    private static void DestructibleCensus(TestContext ctx)
    {
        foreach (var (chapter, instances, anchors) in Census)
        {
            ctx.WithWorld(chapter, collision: false, world =>
            {
                // The registry totals, never the swept rows — the sweep is capped at
                // Probes.SweepCap and would silently under-count.
                var registry = world.Runtime.Destructibles;
                ctx.Same(instances, registry.Count, $"{chapter} destructible instances");
                ctx.Same(anchors, registry.DistinctAnchors, $"{chapter} destructible node groups");
            });
        }
    }

    /// <summary>The authored STOP_SEQUENCE stops must actually run — nothing else in the gate
    /// measures an effect's DURATION (<c>--effects-test</c> only proves a puffer builds;
    /// verification.md WORLD-19). Asserts on the dispatch timeline via
    /// <see cref="AnimRuntime.OnEventDispatched"/>, not on puffers, so it needs no textures:
    /// the rocket fireball's ON_CALL stopper is reached through the call fallback at its
    /// authored 0.3 s, and the 30 s fire's emitting poll loop is halted — an un-halted
    /// <c>Loop{-1}</c> re-fires every frame forever, so "no dispatches after the stop" is the
    /// crisp discriminator.</summary>
    private static void StopSequenceStops(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var program = world.Session.Program.Subset(new[] { "large_fireball", "large_30sec_fire" });
            var fireball = program.ByAnimName("large_fireball");
            var fire30 = program.ByAnimName("large_30sec_fire");
            ctx.Check(fireball.Count > 0, $"chapter program has large_fireball defs={fireball.Count}");
            ctx.Check(fire30.Count > 0, $"chapter program has large_30sec_fire defs={fire30.Count}");
            if (fireball.Count == 0 || fire30.Count == 0)
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
                // nothing runs under that name — the stop must CALL it (the stopper idiom).
                runtime.Start(fireball[0], stage);
                for (int i = 0; i < 60; i++)
                {
                    clock += 1f / 60f;
                    runtime.Advance(1f / 60f);
                }
                int stopperPuffs = 0;
                float stopAt = -1f;
                bool objectOffAfter = false;
                foreach (var e in timeline)
                {
                    if (e.Seq != "stop_p1trail")
                    {
                        continue;
                    }
                    if (e.Kind == "PufferState")
                    {
                        stopperPuffs++;
                        stopAt = e.T;
                    }
                    else if (e.Kind == "ObjectActiveState" && stopAt >= 0f)
                    {
                        objectOffAfter = true;
                    }
                }
                ctx.Same(1, stopperPuffs, $"stop_p1trail PUFFER_STATE dispatches within 1 s");
                ctx.Check(stopAt >= 0.3f && stopAt <= 0.45f,
                    $"the stopper runs at its authored 0.3 s t={stopAt:0.000}");
                ctx.Check(objectOffAfter, $"the stopper's OBJECT_ACTIVE_STATE off follows its puffer stop");

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
            }
            finally
            {
                runtime.Free();
                stage.Free();
            }
        });
    }

    // ---- the compiled destruction slot dispatches at death --------------------------------------

    /// <summary>The live-path start check the direct-Start <c>stop-sequence</c> suite cannot make:
    /// a real kill must dispatch the def's compiled destruction slot
    /// (<see cref="AnimDefinition.DeathSlot"/> — mech3ax's <c>unknown_seq</c>), the block that
    /// carries ~all of <c>large_30sec_fire</c>'s 1,035 death calls. An unparsed block silently
    /// no-ops every one of those calls, and every "the fire ends on time"
    /// check reads the absence as a pass — an effect that never starts satisfies any stop assertion.
    /// Subject: a C1 AA gun, whose slot is the healthy/destroyed swap plus
    /// <c>CallAnimation genx12</c>. Able to fail: with <c>RunDeathSlot</c> deleted, no
    /// <c>destruction_slot</c> lane ever dispatches.</summary>
    private static void DeathSlotDispatches(TestContext ctx)
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

    // ---- WAIT_FOR_COMPLETION --------------------------------------------------------------------

    /// <summary>WAIT_FOR_COMPLETION on the authored case, with its own control beside it in the same sequence.
    ///
    /// <para><c>player_crash_water</c>'s <c>destroy_crash</c> is the install's clean discriminator:
    /// eleven events, of which exactly ONE carries the flag — <c>plane_big_splash</c>, whose own
    /// choreography runs 3.0 s (<c>plane_sp_polys</c>' scale and <c>plane_sp_polyfade</c>' opacity
    /// ramp) — followed immediately by an UNFLAGGED <c>large_steam_spray</c>. Without the hold,
    /// both retarget on the same tick and the spray starts with the splash instead of after it.
    /// </para>
    ///
    /// <para>The control is the other nine calls in that same sequence. <c>call_crash_trails</c>
    /// (twice) and <c>large_10sec_fire</c> sit immediately BEFORE the flagged one and carry
    /// <c>null</c>, so they must still all start together at t=0 — that is trap (b), "0 and null
    /// are different authored states", asserted on real data rather than argued. A runtime that
    /// held every call would pass the spray check and fail these.</para></summary>
    private static void WaitForCompletion(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            const string animName = "player_crash_water";
            const string flagged = "plane_big_splash";
            const string held = "large_steam_spray";
            var program = world.Session.Program.Subset(animName);
            var defs = program.ByAnimName(animName);
            ctx.Check(defs.Count > 0, $"chapter program has {animName} defs={defs.Count}");
            if (defs.Count == 0)
            {
                return;
            }

            // The caller's own nodes plus each callee's ROOT: on a flat stage a callee whose root
            // is missing falls back to the caller's anchor, which still runs but stops being the
            // separate instance whose lifetime is the subject here.
            var nodes = new List<string>
            {
                "player", "healthy", "destroyed", "dontmove", "markers", "shadow", "cockpit1",
                "huge_splash_model", "splash_polys", "sp_1", "white_water_impact",
                "carnage_trails", "large_fire", "ripple1",
            };
            for (int i = 1; i <= 4; i++)
            {
                nodes.Add($"piece{i}");
            }

            WithEmitterStage(ctx, program, "CrashWaterStage", nodes, (stage, runtime, fake) =>
            {
                float clock = 0f;
                var startedAt = new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
                runtime.OnInstanceStarted = (d, _) =>
                {
                    if (d.AnimName is { } name && !startedAt.ContainsKey(name))
                    {
                        startedAt[name] = clock;
                    }
                };
                runtime.Start(defs[0], stage);
                for (int i = 0; i < 480; i++)   // 8 s — well past the splash's authored 3.0 s
                {
                    clock += 1f / 60f;
                    runtime.Advance(1f / 60f);
                }
                runtime.OnInstanceStarted = null;

                ctx.Check(startedAt.ContainsKey(flagged), $"{flagged} became a live instance");
                ctx.Check(startedAt.ContainsKey(held), $"{held} became a live instance");
                if (!startedAt.TryGetValue(flagged, out float splashAt)
                    || !startedAt.TryGetValue(held, out float sprayAt))
                {
                    return;
                }

                ctx.Note($"destroy_crash: {flagged} t={splashAt:0.000}s, {held} t={sprayAt:0.000}s (gap {sprayAt - splashAt:0.000}s vs the authored 3.0s), holds armed={runtime.WaitsInstalled} abandoned={runtime.WaitsAbandoned}");
                ctx.Check(splashAt <= 2f / 60f,
                    $"the flagged call itself is NOT delayed — the hold is on what follows it (t={splashAt:0.000})");
                ctx.Check(sprayAt - splashAt >= 2.9f,
                    $"{held} waits out {flagged}'s authored 3.0 s choreography (gap={sprayAt - splashAt:0.000} s)");
                ctx.Check(sprayAt - splashAt <= 4.5f,
                    $"...and starts when the splash ENDS, not at some ceiling (gap={sprayAt - splashAt:0.000} s)");

                // The control: the unflagged calls ahead of it in the same sequence.
                foreach (var unflagged in new[] { "call_crash_trails", "large_10sec_fire" })
                {
                    if (startedAt.TryGetValue(unflagged, out float t))
                    {
                        ctx.Check(t <= 2f / 60f,
                            $"unflagged {unflagged} is not held (t={t:0.000}) — null and 0 are different authored states");
                    }
                }

                ctx.Check(runtime.WaitsInstalled >= 1,
                    $"the runtime armed the hold rather than the gap coming from somewhere else (installed={runtime.WaitsInstalled})");
                ctx.Same(0, runtime.WaitsAbandoned,
                    $"no hold ended at the WaitCeilingS backstop instead of at its callee");
            },
                asCrashRig: true);
        });
    }

    // ---- what a host deactivation may and may not stop ------------------------------------------

    /// <summary>An <c>OBJECT_ACTIVE_STATE … INACTIVE</c> ends the emitters under that host
    /// — but not one that started in the same instant, and this asserts BOTH halves,
    /// because either alone is satisfied by a broken runtime. Dropping the stop entirely passes the
    /// splash half; shipping the stop unconditioned passes the debris half. They are the two
    /// populations the install-wide census splits, and the split is total
    /// (`analysis/bl-229-emitter-host-deactivation/`): 32 same-instant pairs in 4 shapes against 382
    /// later ones a median 3.5 s out, with nothing in between.
    ///
    /// <para>SPLASH (`plane_big_splash`, the sea dive's own definition). Three offset-less events —
    /// activate <c>sp_1</c>, call <c>hg_splasher</c> onto it, switch <c>sp_1</c> off — all in one
    /// runtime batch, while the callee authors a 0.5 s <c>STOP_SEQUENCE</c> and its own
    /// <c>PUFFER_STATE 0</c> 0.1 s after that. So the emitter must survive its host's deactivation
    /// AND still be gone by ~0.7 s: a runtime that simply never stopped it would show the same first
    /// assertion.</para>
    ///
    /// <para>DEBRIS (`m_build01`, a real destructible death and the case trap (a) names). Its
    /// <c>part1</c> is activated, given <c>trailpuffer1</c> in the same instant, flown by a 5 s
    /// <c>OBJECT_MOTION</c> and only then switched off. That deactivation is the trail's ONLY
    /// authored stop, so it has to keep working — this is the subtree swap whose emitters would leak
    /// forever if the same-instant exemption were built by weakening the stop instead of dating it.</para></summary>
    private static void EmitterHostDeactivation(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            SplashSurvivesItsOwnInstant(ctx, world);
            DebrisTrailStillEndsWithItsHost(ctx, world);
        });
    }

    private static void SplashSurvivesItsOwnInstant(TestContext ctx, TestWorld world)
    {
        const string animName = "plane_big_splash";
        const string pufferName = "splasher";
        var program = world.Session.Program.Subset(animName);
        var defs = program.ByAnimName(animName);
        ctx.Check(defs.Count > 0, $"chapter program has {animName} defs={defs.Count}");
        if (defs.Count == 0)
        {
            return;
        }

        WithEmitterStage(ctx, program, "SplashStage",
            new[] { "huge_splash_model", "splash_polys", "sp_1", "ripple1", "ripple2", "ripple3" },
            (stage, runtime, fake) =>
        {
            runtime.Start(defs[0], stage);
            runtime.Advance(1f / 60f);

            ctx.Check(fake.Built.Any(e => e.Key == pufferName),
                $"{animName} reached the fake factory and built {pufferName}");
            ctx.Check(EmitterOn(runtime, pufferName, "sp_1") == true,
                $"{pufferName} survives the sp_1 deactivation it shares an instant with (BL-229)");

            for (int i = 0; i < 18; i++)   // 0.3 s — inside the callee's authored 0.5 s run
            {
                runtime.Advance(1f / 60f);
            }
            ctx.Check(EmitterOn(runtime, pufferName, "sp_1") == true,
                $"{pufferName} is still emitting 0.3 s in");

            for (int i = 0; i < 30; i++)   // out to 0.8 s, past the authored 0.5 + 0.1 s stop
            {
                runtime.Advance(1f / 60f);
            }
            ctx.Check(EmitterOn(runtime, pufferName, "sp_1") == false,
                $"{pufferName} ends on the run hg_splasher authors, not on its host (and its row is still known, so this is a pause, not a teardown)");
            var emitter = fake.Built.FirstOrDefault(e => e.Key == pufferName);
            ctx.Check(emitter is { Started: > 0 }, $"{pufferName} actually sustained particles");
        },
            asCrashRig: true);
    }

    private static void DebrisTrailStillEndsWithItsHost(TestContext ctx, TestWorld world)
    {
        const string animName = "m_build01";
        const string pufferName = "trailpuffer3";
        const string host = "part3";
        const string offSequence = "sparkout3";   // where part3's own deactivation is authored
        var program = world.Session.Program.Subset(animName);
        var defs = program.ByAnimName(animName);
        ctx.Check(defs.Count > 0, $"chapter program has {animName} defs={defs.Count}");
        if (defs.Count == 0)
        {
            return;
        }

        // The fireball template roots belong on the stage as much as the building's own parts do:
        // `small_fireball` declares a puffer ALSO called `trailpuffer2`, and with its own root
        // missing the name resolution falls back to the call anchor, so its stop lands on the
        // building's key and ends the debris trail early. That is a faithful-stage artifact, not a
        // runtime rule (verification.md WORLD-12) — and it masked this very assertion once.
        var nodes = new List<string>
        {
            "m_bld_healthy", "m_bld_destroyed", "dbase", "flame_ball_01", "flame_ball_02",
        };
        for (int i = 1; i <= 9; i++)
        {
            nodes.Add($"part{i}");
        }
        WithEmitterStage(ctx, program, "DebrisStage", nodes, (stage, runtime, fake) =>
        {
            // Asserted against the DISPATCH MOMENT, never a fixed second: part3 is a
            // bounce-solved launch, so when it lands (and its `sparkout3` switches it off) is
            // computed, not authored. Anything else that could stop this trail — the instance
            // retiring — happens a second later, so "stopped on the deactivation's own frame" is
            // what separates the two, and it is the reading a wall-clock check would blur.
            float clock = 0f;
            float deactivatedAt = -1f;
            float stoppedAt = -1f;
            bool everEmitted = false;
            runtime.OnEventDispatched = d =>
            {
                if (deactivatedAt < 0f && d.Sequence == offSequence && d.EventKind == "ObjectActiveState")
                {
                    deactivatedAt = clock;
                }
            };
            runtime.Start(defs[0], stage);
            for (int i = 0; i < 300; i++)   // 5 s — past the landing and past the instance's own end
            {
                clock += 1f / 60f;
                runtime.Advance(1f / 60f);
                bool? on = EmitterOn(runtime, pufferName, host);
                everEmitted |= on == true;
                if (everEmitted && stoppedAt < 0f && on == false)
                {
                    stoppedAt = clock;
                }
            }
            runtime.OnEventDispatched = null;

            ctx.Check(fake.Built.Any(e => e.Key == pufferName), $"{animName}'s death built {pufferName}");
            ctx.Check(everEmitted, $"{pufferName} trails {host} while it flies");
            ctx.Check(deactivatedAt > 0f, $"{offSequence} switched {host} off t={deactivatedAt:0.000}");
            ctx.Check(stoppedAt > 0f, $"{pufferName} stopped within the 5 s window t={stoppedAt:0.000}");
            ctx.Check(stoppedAt > 0f && deactivatedAt > 0f && Mathf.Abs(stoppedAt - deactivatedAt) <= 2f / 60f,
                $"{pufferName} ends on {host}'s own deactivation frame, not later — BL-224's stop is dated, not dropped (off={deactivatedAt:0.000} stop={stoppedAt:0.000})");
        });
    }

    /// <summary>Is the emitter <paramref name="name"/> ON <paramref name="host"/> emitting? Null when
    /// no such emitter is known. Host-qualified on purpose: puffer names are NOT unique across
    /// definitions — `small_fireball` declares a `trailpuffer2` of its own, and a name-only read
    /// answers about whichever row comes first, which is how a debris assertion here once passed a
    /// runtime with the stop deleted outright.</summary>
    private static bool? EmitterOn(AnimRuntime runtime, string name, string host)
    {
        foreach (var row in runtime.Emitters.Census)
        {
            if (row.Name == name && row.Host == host)
            {
                return row.Emitting;
            }
        }
        return null;
    }

    /// <summary>A bare stage carrying the nodes a definition names, plus a runtime bound to it
    /// through a <see cref="CountingEmitterFactory"/>. Flat children, never a hierarchy: the point is
    /// to give each named host its own subtree, so a stop that reaches the wrong one is visible
    /// rather than being absorbed by a shared ancestor.</summary>
    private static void WithEmitterStage(TestContext ctx, AnimProgram program, string stageName,
        IEnumerable<string> nodeNames,
        System.Action<Node3D, AnimRuntime, CountingEmitterFactory> body,
        bool asCrashRig = false)
    {
        var stage = new Node3D { Name = stageName };
        foreach (var name in nodeNames)
        {
            stage.AddChild(new Node3D { Name = name });
        }
        var fake = new CountingEmitterFactory();
        // The two role flags AnimRuntime.ForCrashRig sets, for a definition the crash rig is the
        // only production caller of: the splash is played by the per-player rig, which resolves
        // and relocates its own called templates and holds no ExternalEffect, so the start and
        // the stop meet on ONE director. Reproducing that here is the point. The relocation half
        // is stage construction state, so it arrives sealed.
        var runtime = new AnimRuntime(AnimRuntime.NewTemplateStage(placesCalled: asCrashRig))
        {
            AutoStart = false,
            ManualAdvance = true,
            SoundHandledElsewhere = true,
            EmitterFactory = fake,
            NameResolveFallback = asCrashRig,
        };
        ctx.Host.AddChild(stage);
        ctx.Host.AddChild(runtime);
        try
        {
            runtime.Bind(stage, program);
            body(stage, runtime, fake);
        }
        finally
        {
            runtime.Free();
            stage.Free();
        }
    }

    // ---- the template MESH half renders at the call site ----------------------------------------

    /// <summary>An effect's template MESHES — half of what it looks like — must be visible
    /// at the call site while it plays, and dark once it is over. The world-effects stage keeps
    /// every template ROOT hidden and the engine reveals the one a call lands on
    /// (<c>TemplateStage.Shown</c>), so both halves are engine rules and both are asserted here,
    /// because either alone is satisfied by a broken runtime: revealing and never hiding leaves a
    /// mesh burning at the last hit point for the session, and hiding eagerly (or never revealing)
    /// shows nothing at all.
    ///
    /// <para>CALLED (`he_ground_effect`). The HE rocket's own def is anchored on <c>he_ring</c> and
    /// reaches the upper ring through <c>CALL_ANIMATION call_he_ring1</c>, whose def is anchored on
    /// the separate staged root <c>he_ring1</c>. That call relocated the template and left it
    /// hidden, so the ring never drew — the same is true of <c>sonic_ground_effect</c>'s four rising
    /// rings and the torpedo's <c>ripple</c>. Measured with <c>--effects-test</c>'s mesh census.</para>
    ///
    /// <para>ENDED (`3040ap_gunhit`). The ap/dum/mag gun hits author an <c>ACTIVE_STATE 0</c> stop
    /// and finish 0.3 s in, which retires the instance and consumes its TTL entry — so nothing was
    /// left to hide their <c>dum_gunhit</c> chunk mesh, and it stayed lit at the impact point. The
    /// slug hit, which ships no stop and runs to its TTL, was hidden by the sweep's Stop and looked
    /// fine, which is why one half alone proves nothing.</para></summary>
    private static void EffectTemplateMesh(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            CalledTemplateShowsItsMesh(ctx, world);
            EndedEffectLeavesNoMeshLit(ctx, world);
        });
    }

    private static void CalledTemplateShowsItsMesh(TestContext ctx, TestWorld world)
    {
        WithEffectStage(ctx, world, "he_ground_effect", new[] { "he_ring", "he_ring1", "he_trails" },
            (stage, runtime, point) =>
        {
            ctx.Check(Probes.MeshCensus.VisibleMeshes(stage) == 0,
                $"the staged templates start hidden ({Probes.MeshCensus.VisibleMeshes(stage)} visible)");
            runtime.PlayEffectAt("he_ground_effect", point);
            int peak = 0;
            for (int i = 0; i < 30; i++)
            {
                runtime.Advance(1f / 60f);
                peak = Mathf.Max(peak, Probes.MeshCensus.VisibleMeshes(stage));
            }

            ctx.Check(Probes.MeshCensus.VisibleMeshesUnder(stage, "he_ring") > 0,
                $"he_ground_effect's own template mesh (he_ring) is visible — the PlayEffectAt half ({Probes.MeshCensus.VisibleMeshesUnder(stage, "he_ring")})");
            ctx.Check(Probes.MeshCensus.VisibleMeshesUnder(stage, "he_ring1") > 0,
                $"the CALLED template's mesh (he_ring1, the upper ring) is visible too — BL-061 ({Probes.MeshCensus.VisibleMeshesUnder(stage, "he_ring1")})");
            ctx.Check(peak >= 2, $"both rings drew in the same window (peak {peak} mesh(es))");
        });
    }

    private static void EndedEffectLeavesNoMeshLit(TestContext ctx, TestWorld world)
    {
        WithEffectStage(ctx, world, "3040ap_gunhit", new[] { "dum_gunhit" }, (stage, runtime, point) =>
        {
            runtime.PlayEffectAt("3040ap_gunhit", point, null, 0.3f);
            runtime.Advance(1f / 60f);
            ctx.Check(Probes.MeshCensus.VisibleMeshesUnder(stage, "dum_gunhit") > 0,
                $"the ap gun hit's chunk mesh is visible while it plays ({Probes.MeshCensus.VisibleMeshesUnder(stage, "dum_gunhit")})");

            // Past the def's own authored ACTIVE_STATE 0 at +0.1 s, which ends the instance well
            // inside the 0.3 s TTL — the case that would otherwise leave the mesh lit for the session.
            for (int i = 0; i < 30; i++)
            {
                runtime.Advance(1f / 60f);
            }

            ctx.Check(Probes.MeshCensus.VisibleMeshesUnder(stage, "dum_gunhit") == 0,
                $"and is dark once the effect has ended, without waiting for its TTL — BL-061 ({Probes.MeshCensus.VisibleMeshesUnder(stage, "dum_gunhit")} still lit)");
        });
    }

    /// <summary>A miniature world-effects stage: the named template roots built from the chapter's
    /// real gamez into one pool slot, each hidden, under an effects-role runtime bound to the
    /// subset of the program the effect needs. Real geometry on purpose — this suite is about mesh
    /// VISIBILITY, which named empty nodes cannot express — and the roles are the production ones
    /// (<c>TemplateStage.Shown</c> + <c>Pooled</c>, the pair <c>WorldEffectsFactory</c> seals into
    /// the stage it builds), since the reveal exists only under them.</summary>
    private static void WithEffectStage(TestContext ctx, TestWorld world, string animName,
        IEnumerable<string> rootNames, System.Action<Node3D, AnimRuntime, Vector3> body)
    {
        var stage = new Node3D { Name = $"EffectStage_{animName}" };
        var pool = new Node3D { Name = "pool0" };
        pool.SetMeta(AnimRuntime.PoolSlotMeta, 0);
        stage.AddChild(pool);
        int built = Session.WorldEffectsFactory.BuildEffectStage(world.Gamez,
            world.Session.Builder.Scene, pool, rootNames);
        ctx.Check(built == rootNames.Count(), $"{animName}: staged {built}/{rootNames.Count()} template root(s)");
        foreach (var child in pool.GetChildren())
            if (child is Node3D root)
            {
                root.Visible = false;
            }

        var runtime = AnimRuntime.ForEffects(
            AnimRuntime.NewTemplateStage(pooled: true, shown: true, placesCalled: true),
            1, new CountingEmitterFactory(), false, 32f,
            () => ctx.Camera.GlobalPosition);
        runtime.ManualAdvance = true;
        ctx.Host.AddChild(stage);
        ctx.Host.AddChild(runtime);
        try
        {
            runtime.Bind(stage, world.Session.Program.Subset(animName));
            // The camera point, so the gun family's PLAYER_RANGE 500 condition passes (WORLD-24 —
            // a probe standing off further than that builds a def that renders nothing).
            body(stage, runtime, ctx.Camera.GlobalPosition);
        }
        finally
        {
            runtime.Free();
            stage.Free();
        }
    }

    // ---- a new panel's tear must not steal a live panel's template copy ------------------------

    /// <summary>The crash rig's damage-stage templates are pooled, and a relocating
    /// CALL_ANIMATION from a NEW anchor takes its own copy instead of teleporting the one a
    /// previous anchor's burst is still flying on. Reproduces the shipped shape exactly: two of
    /// the authored `pdpanelN` menu defs each CALL <c>gimmeflakes</c> AT_NODE their own
    /// <c>pdpN</c>, on a runtime carrying the crash rig's role flags plus the pool
    /// (<c>TemplateStage.Pooled</c> + <see cref="AnimRuntime.PoolSlotMeta"/> slot containers, the
    /// shape <c>WorldEffectsFactory.BuildFlightCrashRuntime</c> builds). Without the pool the
    /// second call relocates and restarts the single shared <c>planeflakes</c> root mid-flight —
    /// the "panels fly away repeatedly, and from the wrong site" symptom.</summary>
    private static void DamageTemplatePool(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var stage = new Node3D { Name = "DamagePoolStage" };
            var pdp5 = PoolAnchorNode("pdp5", new Vector3(-10, 0, 0));
            var pdp4 = PoolAnchorNode("pdp4", new Vector3(10, 0, 0));
            stage.AddChild(pdp5);
            stage.AddChild(pdp4);
            var copies = new List<Node3D>();
            for (int slot = 0; slot < 2; slot++)
            {
                var pool = new Node3D { Name = $"pool{slot}" };
                pool.SetMeta(AnimRuntime.PoolSlotMeta, slot);
                stage.AddChild(pool);
                int built = Session.WorldEffectsFactory.BuildEffectStage(world.Gamez,
                    world.Session.Builder.Scene, pool, new[] { "planeflakes" });
                ctx.Check(built == 1, $"slot {slot} staged its planeflakes copy");
                foreach (var child in pool.GetChildren())
                {
                    if (child is Node3D copy)
                    {
                        copies.Add(copy);
                    }
                }
            }

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
                runtime.Bind(stage, world.Session.Program.Subset(new[] { "pdpanel4", "pdpanel5" }));
                runtime.Play("pdpanel5", stage, applyReset: false);
                for (int i = 0; i < 6; i++)
                {
                    runtime.Advance(1f / 60f);
                }

                var first = copies.Find(c => AtPoolSite(c, pdp5));
                ctx.Check(first != null, $"pdpanel5's tear placed a planeflakes copy at pdp5");

                // Mid-flight of the first burst (the def's authored RUN_TIME is 1.0 s), the
                // second panel tears.
                runtime.Play("pdpanel4", stage, applyReset: false);
                for (int i = 0; i < 6; i++)
                {
                    runtime.Advance(1f / 60f);
                }

                var second = copies.Find(c => AtPoolSite(c, pdp4));
                ctx.Check(second != null, $"pdpanel4's tear placed a planeflakes copy at pdp4");
                ctx.Check(first != null && AtPoolSite(first, pdp5),
                    $"pdp5's copy stayed at ITS OWN site — the new tear did not steal it (BL-288)");
                ctx.Check(first != null && second != null && !ReferenceEquals(first, second),
                    $"the two tears hold two different copies");
                ctx.Check(runtime.PoolRecycles == 0,
                    $"no pool wrap for two anchors over two copies ({runtime.PoolRecycles})");
            }
            finally
            {
                runtime.Free();
                stage.Free();
            }
        });
    }

    /// <summary>A flat named call-site node for <see cref="DamageTemplatePool"/> — name meta set
    /// the way the crash rig's own anchor scaffold sets it, so resolution finds it.</summary>
    private static Node3D PoolAnchorNode(string name, Vector3 at)
    {
        var node = new Node3D { Name = name, Position = at };
        node.SetMeta(AnimRuntime.NameMeta, name);
        return node;
    }

    private static bool AtPoolSite(Node3D? copy, Node3D site) =>
        copy != null
        && copy.GlobalTransform.Origin.DistanceTo(site.GlobalTransform.Origin) < 0.5f;

    // ---- binding the crash rig must leave the airframe under the controller --------------------

    /// <summary>Builds the crash rig the way <c>WorldEffectsFactory.BuildFlightCrashRuntime</c>
    /// does — real plane model, <c>player</c> crash root, pooled template slots, wreck — binds the
    /// crash-rig subset, and asserts two things a live Dogfight showed going wrong. (1) The
    /// airframe model is still a plain child of the controller, not world-pinned: the damage/reset
    /// defs' authored NAME is <c>player_pfighter</c>, which on the Devastator is the model root
    /// itself, and the bind's reset chain (crash reset → <c>player_destruction_reset</c> →
    /// CALL <c>plane_reset</c>) must not relocate the aircraft the way it places effect templates.
    /// (2) Every pooled template copy of one root shows the same number of lit meshes as its
    /// slot-0 sibling — a copy the reset pass missed stays lit at the plane's centre for the whole
    /// session.</summary>
    private static void CrashRigAnchors(TestContext ctx)
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
                            world.Session.Program.Subset(Session.EffectCatalogue.CrashRigAnimNames));
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
                                  && planeModel.Visible,
                            $"{model}: the airframe model is still parented, placed and visible after the tear");

                        // Crash → respawn → move → crash again: the wreck and every template a
                        // crash reveals must play at the SECOND crash's site (live report: the
                        // destroyed plane and the dirt burst replayed at the first crash's
                        // position on every crash after the first).
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

    /// <summary>Every wreck node's rest pose — the local mirror of
    /// <c>WorldEffectsFactory.CollectRestPoses</c>, so the suite's respawn ritual can re-home the
    /// flung pieces the way <c>FlightController.Respawn</c> does.</summary>
    private static void CollectRestPoses(Node3D node, List<(Node3D Node, Transform3D RestPose)> into)
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

    /// <summary>Meshes drawing under one staged template copy — visibility taken in-tree, so a
    /// parent the reset pass switched off darkens the whole copy the way it does on screen.</summary>
    private static int LitMeshCount(Node3D copy)
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

    // ---- the full effects sweep as suite verdicts ----------------------------------------------

    /// <summary>The whole `--effects-test` sweep, asserted instead of read: every one of
    /// <c>EffectCatalogue.EffectAnimNames</c> played through <see cref="Probes.Effects"/> on a
    /// full replica stage. The census's two sweep-wide verdicts sat in `.scratch` text while the
    /// probe "read 33/33 resolved for months" (INSTR-11); this makes them fail a build. The play
    /// point is a fixed spot ~180 m from the stage origin, so the distance column discriminates:
    /// a template that failed to relocate sits at the origin and reads &gt;100 m, a placed one reads
    /// the effect's own authored offsets (0–12 m measured). The runtime's player position IS the
    /// play point, so range-gated effects (the gun family's PLAYER_RANGE 500) pass wherever the
    /// world camera happens to be.
    ///
    /// <para>The puffer/mesh tallies are golden counts under THIS suite's conditions — literal
    /// seed 1 and the counting emitter factory — which are not the probe's (`--det` derives the
    /// effects seed from the master, and RANDOM_WEIGHT dice gate several gun puffers), so the two
    /// are pinned independently, each by its own measurement.</para></summary>
    private static void EffectsCensus(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var names = Session.EffectCatalogue.EffectAnimNames;
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

                ctx.Check(r.Ok, $"all effects resolve ({r.Resolved}/{names.Length} resolved)");
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
                // The 18 includes `biggun_flying_parts` (`mesh[8] zep_ng_dstry1_flt 8/8 @0.0 m`):
                // its eight parts fly their solved parabola before their own deactivation switches
                // them off, so samples in the window catch them drawing.
                ctx.Check(r.Meshed == 18,
                    $"the mesh half's tally holds under suite conditions ({r.Meshed} showed meshes, expected 18)");
            }
            finally
            {
                runtime.Free();
                stage.Free();
            }

            // The derivation IS the staged set — there is no hand table to compare against.
            // What still needs saying, per chapter, because chapter data decides it:
            // the pool config sizes the set that is really staged — a root renamed on one side
            // sizes nothing, silently.
            var unsized = Utils.EffectPools.Load().UnknownRoots(roots);
            ctx.Check(unsized.Count == 0,
                $"effect_pools.json sizes only roots this bind stages — {ctx.Chapter}{(unsized.Count == 0 ? "" : $" — sizes nothing: {string.Join(", ", unsized)}")}");

            CrashStageRootTripwire(ctx, world);
        });
    }

    /// <summary>The crash half, on a replica of the crash rig's own bind scope (the <c>player</c>
    /// crash root, the plane model, its <c>destroyed</c> wreck) — the scope
    /// <c>BuildFlightCrashRuntime</c> derives its template roots in, minus the runtime itself,
    /// which the anchor question does not need. Per-plane on purpose: the wreck and part subtrees
    /// vary by airframe, and the Devastator is the one whose own model root a crash def names.
    /// Asserts what the world half asserts: the rig's derived roots all BUILD from this chapter's
    /// gamez — a root the closure asks for that the chapter cannot supply is the silent-miss
    /// failure.</summary>
    private static void CrashStageRootTripwire(TestContext ctx, TestWorld world)
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

    // ---- do_intersections: the flight ends where the world says ---------------------------------

    /// <summary>The 166 events that author <c>do_intersections: true</c> must be cut short by real
    /// geometry instead of running their authored <c>RUN_TIME</c> out below the terrain, and must
    /// pick their <c>BOUNCE_SEQUENCE</c> branch from the surface they struck.
    ///
    /// <para>Driven as a synthetic body rather than off a chapter's own debris, deliberately: the
    /// reachable carriers are a crashed player's wreck and C5's <c>agyrobus</c>, both of which
    /// reach their launch through a death sequence and a randomised draw. What is under test here
    /// is the sweep, so the launch is authored by hand — thrown downward from a known height at
    /// real chapter geometry — and the flight time, the resting height and the branch are all then
    /// exactly predictable.</para>
    ///
    /// <para>⚠ The control is the THIRD case: the identical body with no mask handed over runs its
    /// full 20 s and ends far below the surface. Without it a suite that never fired the query at
    /// all would still pass its first two checks on a body that simply had not got anywhere yet —
    /// and it is also the assertion that the no-collision-world fallback is the old behaviour and
    /// not merely untested.</para>
    ///
    /// <para>⚠ Shown able to fail by disabling <c>TryContact</c>'s gate: the contact case then
    /// reports the same 20.02 s / −2000 m as the unmasked control and four checks go red. It is
    /// NOT able to fail on the arming rule — raising <c>ArmDistance</c> alone changes nothing here,
    /// because arming is distance OR time and <c>ArmSeconds</c> still arms the body at 0.1 s, long
    /// before a 60 m drop reaches anything. Arming is what the cockpit checks cover (the self-hit
    /// at launch, where a piece starts inside the wreck); this suite covers truncation, resting
    /// height, branch choice and the fallback.</para></summary>
    private static void GroundContact(TestContext ctx)
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

            AnimData Body() => new(new Dictionary<string, object?>
            {
                ["gravity"] = new Dictionary<string, object?>
                {
                    ["value"] = -9.8f,
                    ["complex"] = true,
                    ["no_altitude"] = false,
                    ["do_intersections"] = true,
                },
                ["translation"] = new Dictionary<string, object?>
                {
                    ["initial"] = Vec(0f, -5f, 0f),
                    ["delta"] = Vec(0f, 0f, 0f),
                    ["rnd_xz"] = Vec(0f, 0f, 0f),
                },
                ["bounce_sequence"] = new Dictionary<string, object?>
                {
                    ["default"] = Land,
                    ["water"] = Wet,
                    ["lava"] = null,
                },
                ["run_time"] = Authored,
            });

            // Runs one body to a stop and reports what happened to it. `waterHook` stands in for
            // the session's ProjectilePool.ClassifySurface binding — the classifier has its own
            // coverage, and stubbing it is what makes the branch choice assertable without needing
            // a chapter with reachable sea.
            (float Flight, float EndY, string? Bounce, bool ByContact) Run(uint mask, System.Func<GodotObject?, bool>? waterHook)
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
                    var motion = MotionRuntime.Create(runtime, node, Body(), Authored);
                    if (motion == null)
                    {
                        return (0f, node.GlobalPosition.Y, null, false);
                    }

                    var set = new MotionSet();
                    set.Add(motion, world.Runtime.Destructibles.All.First().Def, null);
                    float flown = 0f;
                    string? bounce = null;
                    for (int i = 0; i < (int)(Authored / Tick) + 2 && !motion.Finished; i++)
                    {
                        foreach (var landing in set.Tick(Tick))
                        {
                            bounce = landing.Bounce;
                        }

                        flown += Tick;
                    }

                    return (flown, node.GlobalPosition.Y, bounce, motion.LandedByContact);
                }
                finally
                {
                    runtime.ContactMask = maskWas;
                    runtime.SurfaceIsWater = hookWas;
                    node.QueueFree();
                }
            }

            // 1 — contact cuts the flight short and rests the body ON the surface.
            var hit = Run(CollisionLayers.World, _ => false);
            ctx.Check(hit.ByContact, $"the sweep ended the body on a collider flight={hit.Flight:0.00}s");
            ctx.Check(hit.Flight < Authored,
                $"contact beat the authored run time flight={hit.Flight:0.00}s authored={Authored:0}s");
            // A band, not a point: the hit lands between two frames and the body is a point, so
            // "on the surface" is within a tick's fall of it, never below it.
            ctx.Check(hit.EndY >= surfaceY - 1f && hit.EndY <= surfaceY + 2f,
                $"the body rests at the struck surface endY={hit.EndY:0.00} surfaceY={surfaceY:0.00}");
            ctx.Check(hit.Bounce == Land, $"contact dispatched the default branch bounce={hit.Bounce ?? "(none)"}");

            // 2 — the same contact over water takes the water branch.
            var wet = Run(CollisionLayers.World, _ => true);
            ctx.Check(wet.Bounce == Wet, $"a water surface picks the water branch bounce={wet.Bounce ?? "(none)"}");

            // 2b — the bounce is a CONTINUATION. The sequence a landing dispatches re-launches the
            // very node that landed (`pNhit` throws `pieceN` on again), and MotionRuntime.Create
            // ordinarily re-homes a ballistic launch to the node's AUTHORED rest pose. Left alone,
            // that teleports the piece back to where the wreck was: seen at the controls as the
            // crash "jumping back to the crash point" once per piece. The follow-up must start from
            // the landing.
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
                    var second = MotionRuntime.Create(runtime, node, Body(), Authored);
                    second?.Seek(0f);
                    float relaunchY = node.GlobalPosition.Y;
                    ctx.Check(Mathf.Abs(relaunchY - restedY) < 1f,
                        $"the follow-up launch starts from the landing, not the authored rest relaunchY={relaunchY:0.00} restedY={restedY:0.00} rest={restPose.Y:0.00}");
                }
                finally
                {
                    runtime.ContactMask = maskWas;
                    node.QueueFree();
                }
            }

            // 3 — THE CONTROL. No mask: no sweep, so the body runs its full clock and ends far
            // below the surface, exactly as it did before this work — which is also the
            // no-collision-world fallback every golden capture takes.
            var free = Run(0u, _ => false);
            ctx.Check(!free.ByContact, $"with no mask the body never tests contact flight={free.Flight:0.00}s");
            ctx.Check(free.EndY < surfaceY - 100f,
                $"the unmasked body falls straight through endY={free.EndY:0.00} surfaceY={surfaceY:0.00}");
            ctx.Note($"contact {hit.Flight:0.00}s ending {hit.EndY - surfaceY:0.00} m from the surface; unmasked {free.Flight:0.00}s ending {free.EndY - surfaceY:0.00} m from it");
        });
    }

    // ---- bounce-terminated launches fly and land ------------------------------------------------

    /// <summary>An <c>OBJECT_MOTION</c> that omits <c>RUN_TIME</c> and names a
    /// <c>BOUNCE_SEQUENCE</c> is the data's "fly until you hit something" idiom. It must solve its
    /// own flight time, actually fly, and dispatch that sequence on landing — without the solve
    /// all ~150 such pieces sit posed at rest on the wreck they should have left.
    ///
    /// <para>Kills one <c>refuel*</c> tank through <see cref="AnimRuntime.DamageAt"/>, the same
    /// call a rocket makes, and asserts on the dispatch timeline. The def is the clean A/B: the
    /// same death launches <c>part1</c>/<c>part2</c> with an authored <c>RUN_TIME</c> and
    /// <c>part3</c>/<c>part4</c> without one, so a regression that re-broke only the solved half
    /// still shows here.</para>
    ///
    /// <para>⚠ What this suite is shown able to fail on is the SOLVE and the DISPATCH: with the
    /// flight solve disabled it reports 2 launches instead of 4 and no <c>sparkout</c> at all. The
    /// two zero-miss checks are carried invariants, not guards — removing the retirement hold
    /// leaves them green at this seed, because every C1 def with a solvable launch also runs an
    /// unbounded <c>fire_n_smoke</c> loop that keeps its instance alive anyway. The retirement
    /// hold's able-to-fail control is the <c>--destroy=m_build</c> probe, not this suite.</para>
    ///
    /// <para>⚠ Assert a BAND, never an exact time. Both launches draw speed and elevation from
    /// <c>translation_range</c> per instance, so the flight is a random variable whose support the
    /// authored ranges fix exactly (see the constants below). The master seed is pinned
    /// (<c>--run-tests</c> implies <c>--det</c>), but the draw still depends on how many times the
    /// shared <c>anim</c> stream has been drawn from before this suite runs — which suite order
    /// and a cached world's earlier kills both move. ⚠ Nothing here reads the emitter census — this
    /// fix's claims are motion and dispatch, neither of which reads the emitter factory; that census
    /// is <c>emitter-lifetime</c>'s job.</para></summary>
    private static void BounceLaunch(TestContext ctx)
    {
        // extracted/C1/cam_anim/refuel1-refuel1-healthy.json, the two bounce-terminated events.
        // t = 2·v0y/|g| = 2·speed·sin(elev)/10 over the authored ranges, so the support is closed:
        //   part3  speed 18…22  elev 60…70°  g −10  →  3.118 … 4.135 s
        //   part4  speed 28…36  elev 35…50°  g −10  →  3.212 … 5.516 s
        // A landing is detected on a frame boundary, so the observed time can run one tick long.
        const float Part3Min = 3.118f, Part3Max = 4.135f;
        const float Part4Min = 3.212f, Part4Max = 5.516f;
        const float Tick = 1f / 60f;
        const string chapter = "C1";   // the only chapter shipping refuel* (5 defs)
        const string lateBounce = "ObjectMotion(bounce landed after its instance ended)";

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
            // Only the tank's OWN dispatches: on a shared cached world, another suite's earlier
            // kill can still be running its authored death (the AA guns' destruction slot calls
            // genx12, whose staggered great_balls_of_fire launches ballistic fireballs), and a
            // runtime-wide read here would count that neighbour's launch against this death
            // (INSTR-10 — a selector coarser than the subject asserts about something else).
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
                for (int i = 0; i < 600; i++)   // 10 s, comfortably past the 5.5 s worst-case flight
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

            // part1/part2 (authored RUN_TIME) plus part3/part4 (solved) — four, measured, and all
            // four are this def's own ObjectMotion events (the called fireball defs carry no
            // ballistic motion of their own). Counted from the def-scoped timeline, not the
            // runtime-wide BallisticMotionsLaunched: see the hook's remark for the neighbour
            // launch a shared world can bleed into this window.
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

            // ---- the retirement hold ----
            // A landing must reach a LIVE instance, and the launch is the LAST event of its
            // sequence, so nothing but the hold keeps one reachable. refuel* cannot show that: its
            // own fire_n_smoke Loop keeps the instance alive whatever the hold does. The yard
            // buildings can — killing all seven, one landed after its instance ended before A4.
            // Which one is a coin toss (speed and elevation are per-instance draws), so the
            // assertion is the invariant "none of them", not "this one".
            // Grouped by ANCHOR, not taken as registry rows: a reader wildcard def and its compiled
            // per-instance twin both bind these seven nodes, so the registry holds 14 rows for
            // seven buildings and the second kill of a pair is a no-op on an already-dead object.
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

    /// <summary>One bounce-terminated piece: it must launch, and its bounce sequence must fire a
    /// flight time later that lands inside the band its authored <c>translation_range</c> allows.
    /// A missing launch and a missing landing are reported apart — they are different bugs.</summary>
    private static void CheckFlight(
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

    /// <summary>The third launch shape. 167 <c>OBJECT_MOTION</c> events install-wide (119
    /// distinct defs — <c>analysis/bl-257-nulled-launch/</c>) omit <c>RUN_TIME</c> <b>and</b>
    /// <c>BOUNCE_SEQUENCE</c>, and follow the launch with the flying piece's own null-start
    /// <c>ACTIVE_STATE 0</c>. A flight solve gated on a bounce being named declines all of
    /// them: the launch reports duration 0, the deactivation lands on the same tick, and the
    /// piece is hidden before it moves. The zeppelin cannon's eight parts are the reachable repro
    /// — <c>biggun_flying_parts</c> is one <c>CALL_ANIMATION</c> onto <c>dblcannon_flying_parts</c>,
    /// whose eight sequences are each exactly this pair.
    ///
    /// <para>⚠ The measurement is the GAP between each part's launch and its own deactivation, not
    /// a mesh count. <c>--effects-test</c> reads this def as <c>8/8</c> mesh even with the solve
    /// declined (INSTR-11): the root IS revealed and the parts ARE self-visible, and the census's peak fold
    /// catches the tick before the hide. A gap is 0 with the solve declined and the solved flight
    /// with it, so it discriminates and a visibility count does not. Displacement is asserted
    /// beside it — DIAG-13, a dispatched launch is not a moved piece.</para>
    ///
    /// <para>⚠ Assert a BAND (the same rule as <c>bounce-launch</c>): elevation and speed are
    /// per-instance draws, so the flight is a random variable whose support the authored ranges fix
    /// — <c>t = 2·speed·sin(elevation)/9.8</c> over elevation 10…70° and speed 17…25 m/s.</para>
    /// </summary>
    private static void NulledLaunch(TestContext ctx)
    {
        // extracted/*/cam_anim/zep_can_dstry1-dblcannon_flying_parts.json: eight parts, each
        // xz −135…135°, y 10…70°, initial 17…25 m/s, gravity −9.8, no run_time, no bounce_sequence.
        //   min  2·17·sin(10°)/9.8 = 0.602 s      max  2·25·sin(70°)/9.8 = 4.794 s
        // A dispatch is observed on a frame boundary, so the gap can run one tick long.
        const float FlightMin = 0.602f, FlightMax = 4.794f;
        const float Tick = 1f / 60f;
        const string root = "zep_ng_dstry1_flt";   // the staged copy's node name, '.' sanitised

        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            WithEffectStage(ctx, world, "biggun_flying_parts", new[] { "zep_ng_dstry1.flt" },
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
                        // exist (the over-generalization this item's trap names).
                        bounceOwed |= runtime.Motions.OwesBounce(d.Def, d.Anchor);
                    };
                    runtime.PlayEffectAt("biggun_flying_parts", point);
                    for (int i = 0; i < 360; i++)   // 6 s, past the 4.79 s worst-case flight
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

    /// <summary>The <c>part1</c>…<c>part8</c> the flying-parts def drives, by node name, from
    /// anywhere under the staged template. Named lookup rather than a child index: the wreck is
    /// real gamez geometry and its parts sit at whatever depth it authors them.</summary>
    private static void CollectNamed(Node node, Dictionary<string, Node3D> into)
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

    /// <summary>Hides a node through the lab's own Hide action, then re-shows it through a real
    /// <c>RESET_STATE</c> def (the same path a world animation uses) and checks the tree row both
    /// times — never through the button, only through <c>Node3D.Visible</c>. A def re-showing a
    /// node the user hid is correct behaviour (the trap: it looks like a bug), so the row must follow it.
    ///
    /// <para>Deliberately a chapter other than <see cref="TestContext.Chapter"/>: that one is
    /// cached and shared with <c>damage-hd</c>, which leaves its swept defs re-killed, so reusing
    /// it here would make the candidate search depend on suite run order. Any other chapter is
    /// always built fresh and torn down by <see cref="TestContext.WithWorld"/>.</para></summary>
    private static void NodeLabVisibility(TestContext ctx)
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
    private static Node3D? FindVariant(Node3D node, string tag)
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
}
