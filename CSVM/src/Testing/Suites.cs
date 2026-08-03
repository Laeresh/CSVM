using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Effects;
using CSVM.Flight;
using CSVM.Mech3;
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

    /// <summary>Gun-group slots <see cref="Loadout.ForRig"/> seats on any airframe (B4) — the
    /// weapon bench fires every gun from all of them, so a drop here would quietly shrink its
    /// coverage without changing the 48/48 line.</summary>
    private const int RigGunGroups = 4;

    /// <summary>How many flight scenarios carry a measured target to assert. Pinned so that
    /// silently demoting one to informational cannot read as a green run.</summary>
    private const int FlightScenarios = 6;

    /// <summary>Destructible instances / distinct node groups per chapter, at each chapter's
    /// default mission. Instances exceed node groups where a reader wildcard def and its compiled
    /// per-instance twin bind the same nodes.
    ///
    /// <para>Both columns are far below what plain NAME matching yields, and that is the point: a
    /// compiled def binds the ONE instance its symbol table names (<c>AnimRuntime.Anchors</c>), so
    /// a mission's zeppelin defs no longer register every other zeppelin in the shared chapter
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
            "the emitter's burst, distance-trail and sustain modes, driven through a fake renderer with no GPU", PufferModes));
        into.Add(new TestHarness.Suite("gauge-colours",
            "the belt indicator's yellow tier is gun-only; hardpoints step green→red", GaugeColours));
        into.Add(new TestHarness.Suite("weapons-defs",
            "every weapons.json BALLISTICS entry reads through the typed reader", WeaponsDefs));
        into.Add(new TestHarness.Suite("weapon-blast",
            "blast falloff, authored fuse/radius independence, and zero-damage special exclusion", WeaponBlast));
        into.Add(new TestHarness.Suite("flight-envelope",
            "the flown envelope still matches the original's measured manoeuvres", FlightEnvelope));
        into.Add(new TestHarness.Suite("markers-rig",
            "every player airframe has a firepoint/pylon rig in planes.zbd", MarkersRig));
        into.Add(new TestHarness.Suite("loadout-bind",
            "every stock loadout binds to its model with every marker resolved", LoadoutBind));
        into.Add(new TestHarness.Suite("weapons-fire",
            "all 48 weapons mount and fire from a built plane", WeaponsFire));
        into.Add(new TestHarness.Suite("loadout-forrig",
            "Loadout.ForRig covers every firepoint/pylon on all 11 airframes, seeded from stock", LoadoutForRig));
        into.Add(new TestHarness.Suite("warning-shot",
            "the incoming-fire near-miss cue fires on another pilot's round, never on your own", WarningShot));
        into.Add(new TestHarness.Suite("damage-stages",
            "each DAMAGE_SEQUENCE def fires its stage effects across an HP sweep", DamageStages));
        into.Add(new TestHarness.Suite("damage-hd",
            "weapon hits destroy, swap, drop colliders, and survive destroy→reset→destroy", DamageHd));
        into.Add(new TestHarness.Suite("stop-sequence",
            "authored STOP_SEQUENCE stops run: the fireball's 0.3 s stopper and the 30 s fire's halt", StopSequenceStops));
        into.Add(new TestHarness.Suite("bounce-launch",
            "a bounce-terminated OBJECT_MOTION flies its solved parabola and fires its BOUNCE_SEQUENCE on landing", BounceLaunch));
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
        into.Add(new TestHarness.Suite("stunt-gates",
            "C4 Danger Zones require their authored entry and exit apertures, not a marker sphere", StuntGates));
        into.Add(new TestHarness.Suite("trail-world-anchor",
            "a trail emitter under a rotated carrier anchors at world identity and drops puffs where it is fed", TrailWorldAnchor));
    }

    // ---- BL-241: emitter lifetime is observable with no GPU -------------------------------------

    /// <summary>Closes `BL-241`: kills a <c>refuel*</c> tank with a <see cref="CountingEmitterFactory"/>
    /// installed and asserts on its <c>fire_n_smoke</c> emitter — the def whose <c>ACTIVE_STATE 1</c>
    /// carries no authored stop of its own, so only `BL-236`'s instance-retirement rule
    /// (<see cref="AnimRuntime.Retirable"/> → <c>FinishEffectInstance</c> → <see cref="EmitterDirector.EndFor"/>)
    /// ever ends it.
    ///
    /// <para>Asserts THREE distinct facts, not one — the traps `BL-241`'s own fix note and `E15`
    /// both name: the fake was actually reached (a name census, not a count — several runtimes could
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

    /// <summary>Drives all three <see cref="Puffer"/> emission modes through a
    /// <see cref="RecordingEmitterRenderer"/> — the seam `E15b` cut so that 847 lines reachable by
    /// nothing could be reached by something. No atlas, no <c>TextureArchive</c> and no
    /// <c>MultiMesh</c> is constructed anywhere in this suite, which is also its own tripwire: if it
    /// ever gets slow, something started building real emitters again.
    ///
    /// <para>Both states are read from the shipped readers rather than written here, so every
    /// expected number below is derived from authored data: <c>flame_ball.json</c>'s
    /// <c>fierypuffer</c> (TIME_INTERVAL 0.2, NUMBER 18, LIFETIME 0.8–1.0, a six-frame
    /// TEXTURE_SEQUENCE, no COLORS) and <c>pufftrails.json</c>'s <c>smokepuffer</c>
    /// (DISTANCE_INTERVAL 2, LIFETIME 1.5–4.5, a COLORS ramp).</para>
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

        var clock = GameClock.Current;
        GameClock.Current = null;
        try
        {
            PufferBurstMode(ctx, burstState);
            PufferSustainMode(ctx, burstState);
            PufferTrailMode(ctx, trailState);
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

    /// <summary>Sustain: the pool is sized to the steady-state population, emission starts on the
    /// very first frame, a long frame cannot overrun the pool, and <c>SustainEnd</c> stops emission
    /// without cutting the live particles short.</summary>
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
            puffer.SustainAt(origin, Basis.Identity, 1f / 60f);
            puffer._Process(1f / 60f);
            ctx.Same(18, gpu.Shown, $"sustain emits on its very first frame");
            ctx.Check(gpu.LastFrame.All(p => p.Position.DistanceTo(origin) < 3f),
                $"sustained particles spawn at the world point they are driven at, not at the node");

            // A 5 s hitch asks for 25 batches; the catch-up cap and the pool between them must keep
            // that inside 108. Integrated at a normal dt so the spawns are observable before they age
            // out — the cap is a spawn-path rule, and this is where it is read.
            puffer.SustainAt(origin, Basis.Identity, 5f);
            puffer._Process(1f / 60f);
            ctx.Same(108, gpu.Shown, $"a 5 s hitch fills the pool and stops there");
            ctx.Check(gpu.MaxIndex == gpu.Capacity - 1,
                $"the hitch reached the pool's last slot and no further max={gpu.MaxIndex} pool={gpu.Capacity}");

            puffer.SustainEnd();
            puffer._Process(0.05f);
            ctx.Check(gpu.Shown > 0, $"SustainEnd stops emission without clearing the live particles");
            for (int i = 0; i < 30; i++)  // 1.5 s, past LIFETIME_RANGE's 1 s
                puffer._Process(0.05f);
            ctx.Same(0, gpu.Shown, $"the live particles finish their own lifetimes and the emitter goes quiet");
        }
        finally
        {
            puffer.Free();
        }
    }

    /// <summary>Distance trail: the first call homes the trail rather than emitting at it, and the
    /// per-meter emission carries its remainder across frames instead of rounding it away.</summary>
    private static void PufferTrailMode(TestContext ctx, PufferState state)
    {
        var gpu = new RecordingEmitterRenderer();
        var puffer = Puffer.CreateWith(state, gpu);
        ctx.Host.AddChild(puffer);
        try
        {
            ctx.Same(640, gpu.Capacity, $"trail pool is the live-particle cap, not a burst count");

            puffer.TrailAdvance(new Vector3(0f, 600f, 0f));
            puffer._Process(1f / 60f);
            ctx.Same(0, gpu.Shown, $"the trail's first call homes it at the muzzle and emits nothing");

            // 10 m at one puff per 2 m = 5, then 3 m more = 1 puff with 1 m carried, not 2 rounded up.
            puffer.TrailAdvance(new Vector3(10f, 600f, 0f));
            puffer._Process(1f / 60f);
            ctx.Same(5, gpu.Shown, $"the trail emits one puff per DISTANCE_INTERVAL of motion");
            ctx.Check(gpu.LastFrame.All(p => Mathf.IsEqualApprox(p.Alpha, 1f)),
                $"a COLORS ramp owns the fade, so the life envelope stays out of it");

            puffer.TrailAdvance(new Vector3(13f, 600f, 0f));
            puffer._Process(1f / 60f);
            ctx.Same(6, gpu.Shown, $"a partial interval carries into the next frame instead of rounding");
            ctx.Check(gpu.MaxIndex < gpu.Capacity, $"trail never writes past its pool max={gpu.MaxIndex} pool={gpu.Capacity}");
        }
        finally
        {
            puffer.Free();
        }
    }

    // ---- pure data -----------------------------------------------------------------------------

    private static void StuntGates(TestContext ctx)
    {
        const string chapter = "C4";
        string gamezPath = SessionPaths.ChapterGamez(ctx.DataRoot, chapter);
        string missionPath = SessionPaths.MissionZrdr(ctx.DataRoot, chapter, "IA1");
        ctx.RequireData(gamezPath, $"chapter {chapter} gamez");
        ctx.RequireData(missionPath, $"chapter {chapter} IA1 zrdr");
        ctx.RequireData(ctx.MessagesPath, $"messages table");

        var mission = StuntMission.Load(GameZ.Load(gamezPath), missionPath, Messages.Load(ctx.MessagesPath));
        ctx.Check(mission != null, $"C4 IA1 loads authored Danger Zone gates");
        if (mission == null)
            return;
        ctx.Same(14, mission.TotalCount, $"C4 IA1 resolved gate pairs");
        var zone = mission.Zones.FirstOrDefault(z => z.PathName == "dzpath14");
        ctx.Check(zone != null, $"C4 dzpath14 resolves despite its route being polygon 2");
        if (zone == null)
            return;

        var separated = mission.Zones.FirstOrDefault(z => z.GreenGate.Center.DistanceTo(z.RedGate.Center) > 100f);
        ctx.Check(separated != null, $"C4 has a Danger Zone with separated gate pair");
        if (separated == null)
            return;
        Cross(mission, separated.RedGate);
        ctx.Check(!separated.Completed, $"C4 red gate alone does not score");
        Cross(mission, separated.GreenGate);
        ctx.Check(separated.Completed, $"C4 scores after both gates in red-to-green order");

        mission.Reset();
        var side = zone.GreenGate.Normal.Cross(Vector3.Up);
        if (side.LengthSquared() < 1e-4f)
            side = zone.GreenGate.Normal.Cross(Vector3.Right);
        side = side.Normalized() * 10000f;
        mission.Update(zone.GreenGate.Center + side - zone.GreenGate.Normal * 20f);
        mission.Update(zone.GreenGate.Center + side + zone.GreenGate.Normal * 20f);
        ctx.Check(!zone.Completed, $"C4 dzpath14 plane crossing beside the aperture does not score");
    }

    private static void Cross(StuntMission mission, StuntGate gate)
    {
        mission.Update(gate.Center - gate.Normal * 20f);
        mission.Update(gate.Center + gate.Normal * 20f);
    }

    /// <summary>The Bloodhawk's flown envelope against the original's, measured off cockpit-gauge
    /// video. These are golden numbers in the same sense as the destructible census — the original
    /// is a fixed artifact, so "150 → 290 mph in 3.76 s" is an invariant of it.
    ///
    /// <para>Why a suite and not a playtest: the flight constants are coupled (thrust sets speed,
    /// speed sets the yaw <c>eff</c>), so editing one silently moves others. The probe's
    /// informational rows — the 1/8-throttle pair and the zoom climb — are deliberately NOT asserted;
    /// they record open questions and must not fail a build.</para></summary>
    private static void FlightEnvelope(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var r = Probes.FlightEnvelope(ctx.ZrdrPath, "player_bhawk");
        ctx.Check(r.Error == null, $"plane stats load error={r.Error ?? "-"}");
        if (r.Error != null)
        {
            return;
        }
        ctx.WriteArtifact("flight_envelope.txt", r.Text);
        ctx.Same(FlightScenarios, r.Asserted, $"asserted flight scenarios");
        foreach (var row in r.Rows)
        {
            if (row.Asserted)
            {
                string measured = $"original={row.Measured:0.00}{row.Unit} "
                                  + $"tol=±{row.Tolerance:0.00} err={row.ErrorPct:+0.0;-0.0}%";
                ctx.Check(row.Ok, $"{row.Name} model={row.Model:0.00}{row.Unit} {measured}");
            }
            else
            {
                ctx.Note($"{row.Name} model={row.Model:0.00}{row.Unit} not asserted — {row.Detail}");
            }
        }
        ctx.Note($"{r.Summary}");
    }

    /// <summary>BL-024: the belt indicator's yellow tier belongs to guns only — a per-pylon
    /// hardpoint steps straight from green to red at empty, matching the original.</summary>
    private static void GaugeColours(TestContext ctx)
    {
        for (float frac = 0f; frac <= 1f; frac += 0.01f)
        {
            ctx.Check(GaugeCluster.HardpointIndicatorColor(frac) != 1, $"hardpoint colour never yellow at frac={frac:0.00}");
        }
        ctx.Check(GaugeCluster.HardpointIndicatorColor(0f) == 2, $"hardpoint colour red at empty");
        ctx.Check(GaugeCluster.HardpointIndicatorColor(1f) == 0, $"hardpoint colour green at full");

        ctx.Check(GaugeCluster.GunIndicatorColor(GaugeCluster.IndicatorLowFrac) == 1,
            $"gun colour yellow at the low threshold frac={GaugeCluster.IndicatorLowFrac:0.00}");
        ctx.Check(GaugeCluster.GunIndicatorColor(GaugeCluster.IndicatorLowFrac + 0.01f) == 0,
            $"gun colour green just above the low threshold");
        ctx.Check(GaugeCluster.GunIndicatorColor(0f) == 2, $"gun colour red at empty");
    }

    private static void WeaponsDefs(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var r = Probes.Weapons(ctx.ZrdrPath, ctx.MessagesPath, "");
        ctx.Check(r.Error == null, $"weapons.json loads error={r.Error ?? "-"}");
        if (r.Error != null)
        {
            return;
        }
        ctx.Same(WeaponDefCount, r.Total, $"weapon defs");
        ctx.Same(0, r.UnhandledTotal, $"unhandled weapon keys");
        ctx.Check(!string.IsNullOrEmpty(r.EmptyClipSound), $"empty-clip sound resolves value={r.EmptyClipSound ?? "-"}");
        ctx.Note($"{r.Summary}");
    }

    private static void WeaponBlast(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"weapon definitions");
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        ctx.Check(weapons.TryGet("wep_14", out var torpedo), $"torpedo definition loads");
        ctx.Check(Mathf.IsEqualApprox(torpedo.DetonationDistance ?? -1f, 1f), $"torpedo authored fuse distance = 1 m");
        ctx.Check(Mathf.IsEqualApprox(torpedo.ImpactProximity ?? -1f, 30f), $"torpedo authored blast radius = 30 m");
        ctx.Check(Mathf.IsEqualApprox(ProjectilePool.BlastDamage(200f, 30f, 0f), 200f), $"blast full damage at centre");
        ctx.Check(Mathf.IsEqualApprox(ProjectilePool.BlastDamage(200f, 30f, 15f), 100f), $"blast linear half damage");
        ctx.Check(Mathf.IsZeroApprox(ProjectilePool.BlastDamage(200f, 30f, 30f)), $"blast zero damage at edge");

        ctx.Check(weapons.TryGet("wep_09", out var flash), $"flash definition loads");
        ctx.Check(weapons.TryGet("wep_15", out var flare), $"flare definition loads");
        ctx.Check(!ProjectilePool.HasBlastDamage(flash), $"zero-damage FLASH radius is not a damage blast");
        ctx.Check(!ProjectilePool.HasBlastDamage(flare), $"zero-damage FLARE radius is not a damage blast");
        ctx.Check(ProjectilePool.FuseDotAllows(0.3f, Vector3.Forward, Vector3.Forward),
            $"authored dot gate accepts a target ahead");
        ctx.Check(!ProjectilePool.FuseDotAllows(0.3f, Vector3.Forward, Vector3.Back),
            $"authored dot gate rejects a target behind");
    }

    private static void MarkersRig(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        var r = Probes.Markers(ctx.PlanesGamezPath, "");
        ctx.Check(r.Error == null, $"planes gamez loads error={r.Error ?? "-"}");
        if (r.Error != null)
        {
            return;
        }
        ctx.Same(PlayerAirframes, r.Requested, $"known player airframes");
        ctx.Same(PlayerAirframes, r.Done, $"airframes with a marker rig");
        ctx.Check(r.Missing.Count == 0, $"no airframe missing its root node missing={string.Join(",", r.Missing)}");
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
            // The whole rig, not just what stock names (D9) — so every weapon has a mount of its
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
            trail.TrailAdvance(a);
            trail.TrailAdvance(b);
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
    /// <summary>The incoming-fire near-miss cue's wiring (BL-087), with its able-to-fail baseline:
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

    // ---- BL-240: bounce-terminated launches fly and land ---------------------------------------

    /// <summary>BL-240: an <c>OBJECT_MOTION</c> that omits <c>RUN_TIME</c> and names a
    /// <c>BOUNCE_SEQUENCE</c> is the data's "fly until you hit something" idiom. It must solve its
    /// own flight time, actually fly, and dispatch that sequence on landing — before the fix all
    /// ~150 such pieces were posed at rest on the wreck they should have left.
    ///
    /// <para>Kills one <c>refuel*</c> tank through <see cref="AnimRuntime.DamageAt"/>, the same
    /// call a rocket makes, and asserts on the dispatch timeline. The def is the clean A/B: the
    /// same death launches <c>part1</c>/<c>part2</c> with an authored <c>RUN_TIME</c> and
    /// <c>part3</c>/<c>part4</c> without one, so a regression that re-broke only the solved half
    /// still shows here.</para>
    ///
    /// <para>⚠ What this suite is shown able to fail on is the SOLVE and the DISPATCH: with the
    /// flight solve disabled it reports 2 launches instead of 4 and no <c>sparkout</c> at all. The
    /// two zero-miss checks are carried invariants, not guards — removing A4's retirement hold
    /// leaves them green at this seed, because every C1 def with a solvable launch also runs an
    /// unbounded <c>fire_n_smoke</c> loop that keeps its instance alive anyway. A4's able-to-fail
    /// control is the <c>--destroy=m_build</c> probe on the record, not this suite.</para>
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
            float clock = 0f;
            var previous = runtime.OnEventDispatched;
            int launchesBefore = runtime.BallisticMotionsLaunched;
            int missedBefore = Missed();
            bool everOwed = false;
            try
            {
                runtime.OnEventDispatched = d => timeline.Add((clock, d.Sequence, d.EventKind, d.EventName));
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

            // part1/part2 (authored RUN_TIME) plus part3/part4 (solved) — four, measured. The
            // called fireball defs carry no ballistic motion of their own, so this is the whole
            // count and a fifth would mean the death grew a launch nobody authored.
            int launched = runtime.BallisticMotionsLaunched - launchesBefore;
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

            // ---- the retirement hold (A4) ----
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
            try
            {
                runtime.OnEventDispatched = d => timeline.Add((clock, d.Sequence, d.EventKind, d.EventName));
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

    // ---- BL-044: node lab tree rows must follow live Visible ------------------------------------

    /// <summary>Hides a node through the lab's own Hide action, then re-shows it through a real
    /// <c>RESET_STATE</c> def (the same path a world animation uses) and checks the tree row both
    /// times — never through the button, only through <c>Node3D.Visible</c>. A def re-showing a
    /// node the user hid is correct behaviour (see BL-044's trap), so the row must follow it.
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
