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

    // B4's scan fixture (aim-assist): a shooter at the origin on player 0's team, nose down world
    // -Z, firing a 500 m/s round with a 1000 m RANGE through the stock guns' 6° cone. Every gate
    // case moves ONE thing off this baseline, so a rejection can only be the gate under test.
    private const float ScanSpeed = 500f;
    private const float ScanRange = 1000f;
    private const float ScanConeDeg = 6f; // the shipped CANNON_SPREAD, asserted in AimAssistShippedData

    /// <summary>The fixed step <c>ordnance-burst-timeline</c> drives its three bursts at.
    /// Deliberately FOUR TIMES finer than <see cref="SequenceRunner.AnimFrame"/>: the authored gaps
    /// under test go down to 0.01 s, which one 1/60 s step cannot resolve at all, and nothing in
    /// those three definitions is denominated in animation frames (none of them carries a
    /// <c>LOOP</c>, so the AnimFrame floor is never reached). A finer step makes every authored
    /// instant a real measurement instead of a rounding.</summary>
    private const float BurstDt = 1f / 240f;

    /// <summary>How far a burst's dispatch may sit from its authored instant. Six steps (0.025 s),
    /// which is what the mechanism costs and no more: one step because the recorded stamp is taken
    /// after the <c>Advance</c> that fired it, one per level of CALL the row sits under (a called
    /// sequence's or a called animation's first event fires one tick late, deliberately), and one
    /// for a gate landing a step late on binary float. Every authored gap in
    /// those three definitions except two is wider than this, and the two that are not (0.025 s and
    /// 0.01 s inside <c>he_light_seq</c>) are carried by the rows behind them: a collapse there
    /// moves the 0.35/0.40/0.41 s tail by 0.15 s.</summary>
    private const float BurstSlack = 6f * BurstDt;

    /// <summary>How long each burst is driven for — past the last authored event of its longest
    /// lane, with room for the lag above. Sonic is the long one: its <c>sonic_growlight</c> ends at
    /// an authored 3.2 s.</summary>
    private const float BurstSeconds = 3.5f;

    /// <summary>The burst suite's instance TTL — an order of magnitude past the longest burst, so
    /// the bound never truncates a timeline. Explicitly NOT <c>--effects-test</c>'s 0.3 s, which
    /// exists for the gun path and would cut the 1.2 s wash off at 0.3 s while every remaining
    /// assertion still passed.</summary>
    private const float BurstTtl = 32f;

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
        into.Add(new TestHarness.Suite("puffer-wind",
            "the traced integration order (position on last frame's velocity, then accel, then damp) "
            + "and friction damping toward the WIND rather than toward rest, WIND_FACTOR and all (B6)",
            PufferWind));
        into.Add(new TestHarness.Suite("puffer-distance-fade",
            "the NEAR_FADE/FAR_FADE camera-distance alpha and its two culls, against the shipped "
            + "bands of C3's spew_puffer and volcanosmoke — the cross-wire included (C7)",
            PufferDistanceFade));
        into.Add(new TestHarness.Suite("puffer-priority-size",
            "PRIORITY inflates the drawn sprite by 1 + K·PRIORITY, folded into BaseSize at spawn (C8)",
            PufferPrioritySize));
        into.Add(new TestHarness.Suite("puffer-fire-column",
            "the 30 s fire's authored column height, still air and in C1's own upward wind — the readout that retired the invented fire scales (D10)",
            PufferFireColumn));
        into.Add(new TestHarness.Suite("loadout-bind",
            "every stock loadout binds to its model with every marker resolved", LoadoutBind));
        into.Add(new TestHarness.Suite("weapons-fire",
            "all 48 weapons mount and fire from a built plane", WeaponsFire));
        into.Add(new TestHarness.Suite("aim-assist",
            "the gun aim assist's per-muzzle slot (B2): a ~0.2 s catch-up time constant that snaps "
            + "outright past 1/catchup_rate, and a forget timer keyed to the last SHOT, not to losing "
            + "a lock; plus the shipped sticky_bullet_* values player.json actually carries, B3's "
            + "constant-velocity intercept solver (dead-ahead, crossing, and outrun-with-no-solution) "
            + "and B4's candidate scan — every rejection gate proved able to fail, the most-aligned "
            + "selection the shipped dist_factor 0.0 produces, and a real fused round in a live pool "
            + "outranking the aircraft behind it; plus B5's 1° launch scatter (flat in the polar "
            + "angle, not over the solid angle) and the fire call's asymmetric step order, which "
            + "fires the SMOOTHED line while the scan updates the target",
            AimAssistSuite));
        into.Add(new TestHarness.Suite("loadout-forrig",
            "Loadout.ForRig covers every firepoint/pylon on all 11 airframes, seeded from stock", LoadoutForRig));
        into.Add(new TestHarness.Suite("warning-shot",
            "the incoming-fire near-miss cue fires on another pilot's round, never on your own", WarningShot));
        into.Add(new TestHarness.Suite("blast-neighbor-shape",
            "splash falloff on a neighbour scores to its nearest collision-shape surface, not its " +
            "transform origin (BL-239)", BlastNeighborShape));
        into.Add(new TestHarness.Suite("air-to-air",
            "a round strikes the target plane's body, maps to the data part, moves armor/HP by the " +
            "weapon's own values, downs it when whole-vehicle health exhausts (a lone dead critical " +
            "part no longer kills — the decoded rule, D14) with the kill attributed through the " +
            "Downed event — never hits the shooter's own geometry — a rocket fuses on a passing " +
            "plane, blasting with falloff and attributing the kill, concentrated fire on ONE " +
            "bearing kills through the decoded redirect + whole-pool overflow (the 2026-08-14 " +
            "correction), and a Fury dies to a few HE rockets", AirToAir));
        into.Add(new TestHarness.Suite("team-model",
            "the B7 team model: two distinct pilot indices (real PlayerIndex values, not synthetic " +
            "ints) share one explicit FlightController.Team and a third sits on another — " +
            "impossible under the retired pilot-index-derived stand-in — the plumbed aim-assist " +
            "scan reads Team and snaps onto the enemy while refusing the teammate, and a real " +
            "fired round that reaches the teammate still costs it HP (Decision 3/A2: targeting is " +
            "gated, damage never is)", TeamModel));
        into.Add(new TestHarness.Suite("instant-action",
            "the C8/D9/E11 Instant Action runtime: an Instant Action display name (\"Warhawk\") " +
            "resolves to its gamez node and an unrecognised one resolves to null rather than a " +
            "guess, the ace's own spawn draw substitutes the LITERAL last index on a collision " +
            "with the player's (never a re-roll), a mixed ace_stats vector averages to one " +
            "representative AI rating, AiAircraftSpawner.Spawn given an authored team/livery " +
            "wears them as-is (the ace lands on team 2 flying its configured airframe), the " +
            "wingman fan/escort-chain/accent-id table and the decision-8a flight-size clamp are " +
            "pure over their inputs, a real spawn census puts N wingmen on team 1 flying the " +
            "configured airframe with wingmen 2/4's PrimaryTargetName resolving to wingmen 1/3's " +
            "own spawned name, the wave-member personality/accent draws are pure over theirs, " +
            "and a real InstantActionWaves sequence over spawned aircraft advances from wave 1 " +
            "to wave 2 exactly on the last kill, activating wave 2's built-inert member at a " +
            "drawn spawn point at least 500 m from the human",
            InstantActionAce));
        into.Add(new TestHarness.Suite("instant-action-zeppelin",
            "the F12 zeppelin run over C1/IA1's own data: zeppelin_type selects the objective node " +
            "(cargo/passenger/military, an unauthored or unrecognised value falling back to cargo " +
            "the way the record reset does), the mission script's own deactivation of " +
            "multiplayer1zep is undone for the objective while a non-selected zeppelin is switched " +
            "off AND held (placed, no longer flown), and the wave arm is the generator alone: the " +
            "claimed generator launches nothing on an uncredited budget, one wave's credit " +
            "releases exactly that wave's built-inert members from the live cargobay drop point " +
            "(12 m under the doors) and no more, a still-parked member counts as present so the " +
            "sequencer does not skip the wave, and the last kill advances it; in C1/IA1's real " +
            "world the objective starts hidden with every one of its gasbag's collision shapes " +
            "switched off, and the activation brings the hull and those colliders back together " +
            "(leaving off only the descendants that are themselves deactivated)",
            InstantActionZeppelin));
        into.Add(new TestHarness.Suite("instant-action-end",
            "the G13 mission end, one mission type at a time and each through the real signal: an " +
            "ace's own Downed report wins the duel, the wave sequencer's last kill wins the " +
            "squadron (with a wave still flying it does not), the LAST pilot in wins the stunt run " +
            "over C1/IA1's authored zones while the first does not — and the last still-flying one " +
            "does when the other is out of lives — and really shooting out every one of C1/M04's " +
            "piratezep engines wins the zeppelin run with its hull still alive (one engine short " +
            "does not), as does the gasbag threshold on its own; each with a second mission of " +
            "another type subscribed to the same " +
            "signal and staying Running, plus a hull that is not the objective leaving it running; " +
            "and the lives ledger on a real aircraft: with a life left the armed 3 s crash cam " +
            "respawns it, out of lives the wreck is still there 10 s later and the solo mission " +
            "is LOST",
            InstantActionEnd));
        into.Add(new TestHarness.Suite("instant-action-wrapup",
            "the G14 wrap-up board's two shot counters, ScoredShooters-filtered exactly as the " +
            "decode's own 'the local player' is: a scored shooter's cannon round counts as both " +
            "fired and hit, an unscored (AI) shooter's identical shot moves neither counter, and " +
            "a scored shooter's ROCKET (not CANNON) round is excluded from both",
            InstantActionWrapup));
        into.Add(new TestHarness.Suite("inert-aircraft",
            "the E10 inert state, each claim watched passing on a live aircraft first and on the " +
            "inert one AFTER activation: a plane built inert is not returned by a raycast, is " +
            "listed by the aim assist's candidate collector but not as LIVE (so a scan pointed " +
            "straight at it finds nothing), takes no damage from a round fired through it, does " +
            "not move under a sim step and is not drawn — then Activate re-homes it and every one " +
            "of those flips back",
            InertAircraft));
        into.Add(new TestHarness.Suite("world-turrets",
            "the world AA emplacements (C9b) place at their NODES patterns against the real C1 " +
            "world (census pinned, one entry many turrets, scoped multi-segment paths), honour " +
            "shipped ACTIVATED (a dormant aagun holds fire with a hostile plane in range until " +
            "the --wake-turrets stand-in wakes it, then acquires and fires under its own " +
            "enemy-default team), take the Instant Action builder's subtree-scoped ACTIVATED " +
            "write on the objective zeppelin (14 rings armed and shooting back, nothing outside " +
            "the hull touched, the same call with the flag cleared stowing them again), keep " +
            "their own mounting SECTION out of their own sight line while the rest of the hull " +
            "stays cover, step THEMSELVES on a realtime clock (the mode every real session runs, " +
            "and the one no suite or golden uses), skip " +
            "same-team targets, join the aim-assist candidate list, and go permanently quiet " +
            "when the emplacement's own destructible dies", WorldTurrets));
        into.Add(new TestHarness.Suite("carried-turrets",
            "a carried turret gunner (C9a) builds from ai.zrd + the vehicle def's thirdp mount, " +
            "poses at its arc centre, tracks and fires on a hostile plane inside DETECTION_RANGE " +
            "with hits landing under the host's shooter id (never on the host's own airframe), " +
            "holds fire while tracking through a bored window, parks at the NEARER yaw end stop " +
            "out of arc, treats YAW [0,0] as unrestricted rather than locked, and goes quiet with " +
            "a crashed host — plus the aim assist's turret candidate list is fed", CarriedTurrets));
        into.Add(new TestHarness.Suite("ai-actor",
            "the M4 AI actor seam: an AI-piloted plane (AiPilot input, IsHumanPiloted false, no " +
            "camera/HUD/devices) spawned into an already-running sim flies its orders, takes a " +
            "mid-flight retarget, and is present, ticking, damageable by the weapon's own values " +
            "and killable with the kill attributed to the shooter through Downed", AiActor));
        into.Add(new TestHarness.Suite("ai-gunnery",
            "the D14 AI gunner + D12 acquisition: acquires through the decoded target ranking " +
            "as mutable state (0.7 player weight, primary_target override, 1e21 activation " +
            "cutoff, all live in the engine), refuses the shot " +
            "outside the ±11° forward gun cone and outside its quick-draw cone off the target's " +
            "nose/tail, fires real rounds through the fire-control path under its own shooter id " +
            "with dead-eye scatter (skill 1 hits measurably less than skill 9), downs the target " +
            "with the kill attributed, and NEVER gets the human aim assist (the IsHumanPiloted " +
            "gate, A/B'd in place)", AiGunnery));
        into.Add(new TestHarness.Suite("ai-modes",
            "the D11 nine-mode machine on a live AI plane: patrol activates into pursue inside " +
            "the shipped 2000 m radius, a scripted failed steady-hand roll on a real projectile " +
            "hit breaks off into an evasive maneuver that plays to Done and returns, a failed " +
            "sixth-sense roll stuns (gunner silent) and recovers after stun_recovery_interval, " +
            "the avoid-crash override climbs out on a blocked probe and releases, and the D15 " +
            "rubber-band assist: a chasing human fallen behind puts the machine in lay off " +
            "(throttle eased, fire held) and --no-assist's switch never enters it under the " +
            "same geometry — every transition in the engine's own mode vocabulary", AiModes));
        into.Add(new TestHarness.Suite("voice-runtime",
            "the B8 combat-voice runtime: the accent→voice.zrd→pilot-clip chain resolves against " +
            "the real archive, a roster-subset prewarm makes the lines playable after the loader " +
            "is retired (a never-prewarmed def stays null), a source-following one-shot tracks a " +
            "moving node and survives its source's death, and the full-set prewarm cost is " +
            "measured and reported", VoiceRuntime));
        into.Add(new TestHarness.Suite("ai-voice",
            "the E16 trigger dispatch on a live AI plane against the real archive: a projectile "
            + "hit crossing a DI threshold plays exactly ONE source-following line the pilot's "
            + "accent owns (the 15 s slot cooldown swallowing the follow-up hits), and the kill "
            + "plays the dead pilot's own death cry through the force flag while an unforced "
            + "dispatch on the same dead speaker stays silent", AiVoice));
        into.Add(new TestHarness.Suite("ai-net-follow",
            "net following (B5): a real chapter net resolves by id and by name, its trailer and " +
            "tags ride along unacted-on, and an AI plane with a net-following pilot captures node " +
            "after node with every hop an EDGE of the graph, never node order", AiNetFollow));
        into.Add(new TestHarness.Suite("zeppelin-motion",
            "zeppelin motion (F17): C1/M04's piratezep record loads, its world node is placed at " +
            "the authored pose and flown along PirateZep1 between manual sim steps — every hop an " +
            "EDGE, displacement never over max_speed·dt, the route's raw shape-A tags acted on by " +
            "NOTHING (stop nodes undecoded) — total engine loss decelerates it to a stop through " +
            "the decoded sqrt curve, and a deactivated record is placed but held", ZeppelinMotionSuite));
        into.Add(new TestHarness.Suite("zeppelin-launch",
            "zeppelin fighter launch (F20): C1/IA1's zeppelin-launch generator authors the " +
            "decoded shape (cargobay origin, −90° drop, mp1 door anims — both shipped as " +
            "compiled OnCall defs over door_left/door_right), holds below the 100 m gate with " +
            "the door shut, and on F17's flown zeppelin opens the door and drops fighters at " +
            "the origin node's LIVE position on the composed 7 s schedule — the fast cycle " +
            "leaving the hangar open (close early only past an 8 s gap) — while a max_active 1 " +
            "clone stops after one live spawn", ZeppelinLaunch));
        into.Add(new TestHarness.Suite("zeppelin-damage",
            "multi-zone zeppelin damage (F18) on C1/M04's piratezep in its own mission world: " +
            "gasbag pools seeded from the record (120 over the def-less 0), engines from their " +
            "compiled defs (40), a no-DAMAGES_ZEPPELIN gun round strikes a gasbag and is refused " +
            "while a DAMAGES_ZEPPELIN round spends real hp, an engine kill slows the zeppelin " +
            "through the F17 sqrt seam, and the survivor threshold kills with the decoded " +
            "polarity — dead at survivors 3 < required 4, NOT at the design's destroy count — " +
            "playing the authored all_pzep_gasbags death and stopping the motion", ZeppelinDamageSuite));
        into.Add(new TestHarness.Suite("zeppelin-broadside",
            "broadside cannons (F19) on C1/M04's flying piratezep: a player inside the port " +
            "arc triggers the authored deploy anims (durations read from the defs, 4 s), the " +
            "readied side volleys real unowned wep_28 rounds lead-solved at the player while " +
            "the far side stays stowed, out-of-arc holds fire and retracts after the invented " +
            "idle window, an F18-destroyed cannon thins the next volley to 5, a " +
            "cannon_inaccuracy clone shows real scatter, and the zeppelin-vs-zeppelin arm " +
            "rand()-picks only the target's IN-ARC gasbags on constructed geometry", ZeppelinBroadsideSuite));
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
        into.Add(new TestHarness.Suite("fbfx-flash",
            "he_ground_effect's six-step full-screen wash reports its authored run times, so the 1.2 s ramp does not collapse into one instant", FbfxFlash));
        into.Add(new TestHarness.Suite("ordnance-burst-timeline",
            "the HE, flash and sonic bursts play end to end and every sequence's whole event timeline matches the authored JSON — in order, at the authored time (D31)", OrdnanceBurstTimeline));
        into.Add(new TestHarness.Suite("effects-census",
            "the full --effects-test sweep as verdicts: every effect resolves, template meshes show at the CALL SITE (not the stage origin), and none stays lit after its stop", EffectsCensus));
        into.Add(new TestHarness.Suite("bounce-launch",
            "a bounce-terminated OBJECT_MOTION flies its solved parabola and fires its BOUNCE_SEQUENCE on landing", BounceLaunch));
        into.Add(new TestHarness.Suite("ground-contact",
            "a gravity-bearing OBJECT_MOTION is cut short by real geometry through the right tier (default column, do_intersections sweep, no_altitude neither), rests on the surface and picks its BOUNCE_SEQUENCE branch from what it struck, and does none of it without a mask", GroundContact));
        into.Add(new TestHarness.Suite("forward-rotation",
            "an OBJECT_MOTION tumble turns at the authored RATE about its own launch direction's horizontal perpendicular, scaled by that direction's length — and a vector-translation launch does not turn at all", ForwardRotation));
        into.Add(new TestHarness.Suite("self-ref-launch",
            "an OBJECT_MOTION naming the MAIN_ROOT_NODE sentinel launches the node its def was invoked on, taking that node over from whatever was driving it", SelfRefLaunch));
        into.Add(new TestHarness.Suite("nulled-launch",
            "an OBJECT_MOTION naming NEITHER RUN_TIME nor BOUNCE_SEQUENCE flies its solved parabola before its own deactivation switches it off (BL-257)", NulledLaunch));
        into.Add(new TestHarness.Suite("destructible-census",
            "per-chapter destructible registry totals", DestructibleCensus));
        into.Add(new TestHarness.Suite("clutter-determinism",
            "templates.zrd's substitute + scale_range move C1's species mix and sizes without changing the instance total, and two builds of the same chapter are identical transform for transform", ClutterDeterminism));
        into.Add(new TestHarness.Suite("lens-flare-gates",
            "the sun's lens flare is gated on chapter data alone, and its two independent gates — the gamez `sun` node and init.gw's LensFlareTexture slots — agree chapter by chapter, in C2 and C3 and nowhere else (BL-165)", LensFlareGates));
        into.Add(new TestHarness.Suite("sun-orientation",
            "the world's light wears the flown zone's authored SUNLIGHT_ORIENTATION, and follows it across a zone change (BL-324)", SunOrientation));
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
        into.Add(new TestHarness.Suite("ai-crash-defs",
            "an AI plane's crash rig binds the ai_crash_* family and its crash indexes it by the struck surface id — dirt(13) plays ai_crash_dirt, no material plays ai_crash_default — while a human rig off the same factory keeps player_crash_* (G21)", AiCrashDefs));
        into.Add(new TestHarness.Suite("hostile-marker-hud",
            "the H22 targeting HUD outside --vs: the matchless VersusHud tracks the pane's " +
            "nearest LIVE AI hostile off the pool's own aircraft roster (a closer human, dead " +
            "plane or neutral is never picked), switches to a closer hostile, drops a crashed " +
            "one, and a hud built without a pool (the VS default) never tracks", HostileMarkerHud));
        into.Add(new TestHarness.Suite("splitscreen-listeners",
            "every 2–4P pane is a 3D audio listener, which a SubViewport is not by default — the "
            + "pinned listener model (A2), and the one thing standing between splitscreen and a "
            + "world with no listener at all", SplitscreenListeners));
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

    // ---- the emitter's own modes, with no GPU ---------------------------------------------------

    /// <summary>Drives every <see cref="Puffer"/> emission path through the collapsed
    /// <c>Emit</c>/<c>Stop</c> pair (plus the still-separate <c>Burst</c>) over a
    /// <see cref="RecordingEmitterRenderer"/>. No atlas, no <c>TextureArchive</c> and no
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
        var speedCueState = PufferState.Load(
            SessionPaths.ChapterZrdr(ctx.DataRoot, ctx.Chapter), "speed_cue.json", "cuepuffer1");
        var speedCue2 = PufferState.Load(
            SessionPaths.ChapterZrdr(ctx.DataRoot, ctx.Chapter), "speed_cue.json", "cuepuffer2");
        var speedCue3 = PufferState.Load(
            SessionPaths.ChapterZrdr(ctx.DataRoot, ctx.Chapter), "speed_cue.json", "cuepuffer3");
        ctx.Check(burstState != null, $"flame_ball.json defines fierypuffer");
        ctx.Check(trailState != null, $"pufftrails.json defines smokepuffer");
        ctx.Check(speedCueState != null, $"{ctx.Chapter} speed_cue.json defines cuepuffer1");
        ctx.Check(speedCue2 != null, $"{ctx.Chapter} speed_cue.json defines cuepuffer2");
        ctx.Check(speedCue3 != null, $"{ctx.Chapter} speed_cue.json defines cuepuffer3");
        if (burstState == null || trailState == null || speedCueState == null
            || speedCue2 == null || speedCue3 == null)
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
            PufferSustainSubFrameEmission(ctx);
            PufferTrailMode(ctx, trailState);
            PufferTrailOffset(ctx, speedCueState);
            SpeedCueBands(ctx, speedCueState, speedCue2, speedCue3);
            SpeedCueChapterVariants(ctx);
            PufferStillSputter(ctx, trailState);
            PufferStaticBurn(ctx, trailState);
            PufferStopRevive(ctx, trailState);
            PufferTeleportGuard(ctx, trailState);
            PufferStartAgeBehavior(ctx);
        }
        finally
        {
            GameClock.Current = clock;
        }
    }

    /// <summary>The particle integrator against the original's own arithmetic
    /// (<c>docs/org/puffer.md</c>), on a state built here rather than loaded, because the point is
    /// the closed form and an authored puffer's random draws would only obscure it.
    ///
    /// <para>Three claims, each with the control that makes its "unchanged" readable:
    /// (1) at ZERO wind the coupling is algebraically absent — the track is the pure damped curve
    /// — and the same emitter in a wind is provably not, so "matches the curve" is evidence rather
    /// than an untested branch; (2) acceleration is applied AFTER the position step and BEFORE the
    /// damp, which puts a moving particle exactly one frame of velocity behind where our old order
    /// put it; (3) <c>WIND_FACTOR</c> 0 is genuinely becalmed and 1 is fully carried, with the
    /// engine's <c>friction != 0</c> gate keeping a frictionless puffer out of the wind
    /// entirely.</para></summary>
    private static void PufferWind(TestContext ctx)
    {
        var clock = GameClock.Current;
        GameClock.Current = null;
        try
        {
            PufferWindZeroIsNoOp(ctx);
            PufferAccelOrder(ctx);
            PufferWindFactorCoupling(ctx);
            PufferFrictionlessIgnoresWind(ctx);
            PufferWindGustModel(ctx);
        }
        finally
        {
            GameClock.Current = clock;
        }
    }

    // ---- the camera-distance fade ---------------------------------------------------------------

    /// <summary>The distance alpha, driven entirely off the numbers two shipped readers author.
    /// Every distance below is a VIEW-SPACE DEPTH: the camera sits at the origin looking down −Z
    /// (Godot's forward), so a particle placed at <c>(0, 0, −d)</c> is at depth <c>d</c>, and one
    /// pushed sideways is deliberately used to prove the measure is depth and not range.</summary>
    private static void PufferDistanceFade(TestContext ctx)
    {
        var clock = GameClock.Current;
        GameClock.Current = null;
        try
        {
            PufferFadeBands(ctx);
            PufferFadeCrossWire(ctx);
            PufferFadeSwitchesOff(ctx);
            PufferFadeNoCameraNoFade(ctx);
        }
        finally
        {
            GameClock.Current = clock;
        }
    }

    /// <summary>One particle that never moves and never dies, carrying a COLORS ramp so the drawn
    /// alpha channel is the DISTANCE alpha alone: the ramp path writes <c>1 × distAlpha</c> and
    /// leaves the life envelope out of it (the ramp's own alpha rides in the colour, exactly as
    /// the shader's <c>v_alpha × v_color.a</c> expects).</summary>
    private static PufferState FadeTestState(string name,
        float nearStart, float nearEnd, float farStart, float farEnd) => new()
        {
            Name = name,
            Number = 1,
            TimeInterval = 10f,
            SizeMin = 1f,
            SizeMax = 1f,
            LifetimeMin = 1000f,
            LifetimeMax = 1000f,
            NearFadeStart = nearStart,
            NearFadeEnd = nearEnd,
            FarFadeStart = farStart,
            FarFadeEnd = farEnd,
            Textures = new[] { "smoke101" },
            Colors = new[] { (0f, Colors.White), (1f, Colors.White) },
        };

    /// <summary>Bursts one particle at <paramref name="at"/> and returns the alpha it was drawn
    /// with, or null when it was discarded. The burst is fired AT the point (burst mode stores
    /// positions in the node's own frame, whose origin is the burst point), and a single
    /// <c>_Process</c> at a dt small enough to leave the particle where it was born runs the draw.
    /// </summary>
    private static float? FadeAlphaAt(TestContext ctx, PufferState state, Vector3 at,
        EffectAmbience ambience, PufferFadeSwitches? switches = null)
    {
        var gpu = new RecordingEmitterRenderer();
        var puffer = Puffer.CreateWith(state, gpu, activeDuration: 0.01f, ambience: ambience,
            fade: switches);
        ctx.Host.AddChild(puffer);
        try
        {
            puffer.Burst(at);
            puffer._Process(1f / 60f);
            ctx.Same(1, puffer.LiveCount,
                $"{state.Name}: the particle stays ALIVE whatever the fade decides — it is a draw rule, not a reaper");
            return gpu.Shown > 0 ? gpu.LastFrame[0].Alpha : null;
        }
        finally
        {
            puffer.Free();
        }
    }

    /// <summary>C3's <c>spew_puffer</c> exactly as <c>waterfalls.zrd.json</c> authors it —
    /// <c>FADE_RANGE [300, 400]</c>, <c>NEAR_FADE [40, 5]</c> — the only puffer in the install
    /// carrying both a near fade and the tightest far band.</summary>
    private static void PufferFadeBands(TestContext ctx)
    {
        var state = FadeTestState("spew_puffer", 40f, 5f, 300f, 400f);
        var amb = new EffectAmbience();
        amb.SetCamera(Vector3.Zero, Vector3.Forward);   // at the origin, looking down −Z

        ctx.Check(FadeAlphaAt(ctx, state, new Vector3(0f, 0f, -20f), amb) == null,
            $"inside NEAR_FADE[0] the particle is culled outright, not faded (20 m < 40 m)");
        ctx.Check(FadeAlphaAt(ctx, state, new Vector3(0f, 0f, -100f), amb) is { } mid
                  && Mathf.IsEqualApprox(mid, 1f),
            $"between the bands it draws at full alpha");
        ctx.Check(FadeAlphaAt(ctx, state, new Vector3(0f, 0f, -300f), amb) is { } edge
                  && Mathf.IsEqualApprox(edge, 1f),
            $"FADE_RANGE[0] is the LAST full-alpha distance, not the first faded one");

        float? half = FadeAlphaAt(ctx, state, new Vector3(0f, 0f, -350f), amb);
        ctx.Check(half is { } h && Mathf.IsEqualApprox(h, 0.5f, 1e-4f),
            $"half way across the far band the alpha is half alpha={half}");
        ctx.Check(FadeAlphaAt(ctx, state, new Vector3(0f, 0f, -450f), amb) == null,
            $"past FADE_RANGE[1] it is discarded (450 m > 400 m)");
        ctx.Check(FadeAlphaAt(ctx, state, new Vector3(0f, 0f, 100f), amb) == null,
            $"and a particle BEHIND the camera is culled by the unauthored-near rule at depth 0");

        // Depth, not range: 350 m ahead and 600 m sideways is 694 m away and still mid-band.
        float? sideways = FadeAlphaAt(ctx, state, new Vector3(600f, 0f, -350f), amb);
        ctx.Check(sideways is { } s && Mathf.IsEqualApprox(s, 0.5f, 1e-4f),
            $"the measure is VIEW-SPACE DEPTH: 600 m off-axis does not change the fade alpha={sideways}");
        float range = new Vector3(600f, 0f, -350f).Length();
        ctx.Check(range > 400f,
            $"ABLE-TO-FAIL CONTROL: that same point is {range:0} m away — a euclidean-range implementation would have discarded it");
    }

    /// <summary>The cross-wire, reproduced rather than repaired: the near ramp's origin is
    /// <c>FAR_FADE[0]</c>. C3's <c>volcanosmoke</c> — <c>NEAR_FADE [1, 75]</c>,
    /// <c>FAR_FADE [2000, 3000]</c> — is the only puffer in the install whose near pair ascends and
    /// therefore the only one that reaches the ramp branch at all, where the wrong origin drives
    /// the alpha hard negative and the <c>alpha &gt; 0</c> gate culls it. The near band is a cull
    /// on every puffer in the install, and this is the one that had to be checked to say
    /// so.</summary>
    private static void PufferFadeCrossWire(TestContext ctx)
    {
        var state = FadeTestState("volcanosmoke", 1f, 75f, 2000f, 3000f);
        var amb = new EffectAmbience();
        amb.SetCamera(Vector3.Zero, Vector3.Forward);

        ctx.Check(FadeAlphaAt(ctx, state, new Vector3(0f, 0f, -50f), amb) == null,
            $"at 50 m volcanosmoke is culled: the near ramp reads FAR_FADE[0] (2000), so its alpha is (50-2000)/74");
        ctx.Check(FadeAlphaAt(ctx, state, new Vector3(0f, 0f, -80f), amb) is { } beyond
                  && Mathf.IsEqualApprox(beyond, 1f),
            $"past NEAR_FADE[1] it pops in at full alpha — a hard edge at 75 m, not a 1-to-75 m fade-in");
        // The repair we deliberately did NOT make, stated as a number so it cannot creep back in.
        float repaired = (50f - 1f) / (75f - 1f);
        ctx.Check(repaired > 0.6f,
            $"ABLE-TO-FAIL CONTROL: with NEAR_FADE[0] as the ramp origin the 50 m sample would have drawn at alpha {repaired:0.000}");
    }

    /// <summary>The author's three switches, each shown to switch. Config is file-backed with no
    /// setter, so these come through <c>CreateWith</c>'s test-only override — see
    /// <see cref="PufferFadeSwitches"/>.</summary>
    private static void PufferFadeSwitchesOff(TestContext ctx)
    {
        var state = FadeTestState("spew_puffer", 40f, 5f, 300f, 400f);
        var amb = new EffectAmbience();
        amb.SetCamera(Vector3.Zero, Vector3.Forward);
        var near = new Vector3(0f, 0f, -20f);
        var far = new Vector3(0f, 0f, -450f);
        var band = new Vector3(0f, 0f, -350f);

        var noNear = new PufferFadeSwitches(DistanceFade: true, FarCull: true, NearCull: false);
        ctx.Check(FadeAlphaAt(ctx, state, near, amb, noNear) is { } n && Mathf.IsEqualApprox(n, 1f),
            $"puffer.nearCull false draws the particle the camera flew through");

        var noFade = new PufferFadeSwitches(DistanceFade: false, FarCull: true, NearCull: true);
        ctx.Check(FadeAlphaAt(ctx, state, band, amb, noFade) is { } b && Mathf.IsEqualApprox(b, 1f),
            $"puffer.distanceFade false keeps full alpha across the authored band");
        ctx.Check(FadeAlphaAt(ctx, state, far, amb, noFade) == null,
            $"and leaves the far CUTOFF alone — the ramp and the cull are two switches");

        // ⚠ farCull alone is a no-op, and that is a finding, not an oversight: the authored ramp
        // reaches zero at exactly the cutoff distance, so the alpha > 0 gate removes what the cull
        // would have. Asserted so the pairing stays documented in something that runs.
        var noFarCull = new PufferFadeSwitches(DistanceFade: true, FarCull: false, NearCull: true);
        ctx.Check(FadeAlphaAt(ctx, state, far, amb, noFarCull) == null,
            $"puffer.farCull false ALONE changes nothing — past the band the ramp's own alpha is already negative");
        var wideOpen = new PufferFadeSwitches(DistanceFade: false, FarCull: false, NearCull: true);
        ctx.Check(FadeAlphaAt(ctx, state, far, amb, wideOpen) is { } w && Mathf.IsEqualApprox(w, 1f),
            $"it takes distanceFade AND farCull together to keep a distant puffer drawn");

        // The original's own far-band multiplier: below 1 it pushes the fade outward, and it
        // touches the far band only.
        var pushedOut = new PufferFadeSwitches(true, true, true, GlobalFadeFactor: 0.5f);
        var wayOut = new Vector3(0f, 0f, -700f);
        ctx.Check(FadeAlphaAt(ctx, state, wayOut, amb) == null,
            $"ABLE-TO-FAIL CONTROL: at the default factor 700 m is well past the cutoff and culled");
        float? scaled = FadeAlphaAt(ctx, state, wayOut, amb, pushedOut);
        ctx.Check(scaled is { } sc && Mathf.IsEqualApprox(sc, 0.5f, 1e-4f),
            $"globalFadeFactor 0.5 measures that same 700 m as 350 m — half way across the band alpha={scaled}");
        ctx.Check(FadeAlphaAt(ctx, state, new Vector3(0f, 0f, -20f), amb, pushedOut) == null,
            $"and it does NOT reach the near band, which still culls at 20 m on the unscaled depth");
    }

    /// <summary>No camera published ⇒ no distance fade at all, rather than one measured against
    /// the world origin. This is what keeps the unit suites, the plane viewer and the damage lab
    /// out of the unauthored near cull at depth 0, and it is the state every OTHER puffer suite
    /// runs in — which is why none of them moved.</summary>
    private static void PufferFadeNoCameraNoFade(TestContext ctx)
    {
        var state = FadeTestState("no_camera", 40f, 5f, 300f, 400f);
        var amb = new EffectAmbience();          // wind may be written; a camera never is
        ctx.Check(!amb.HasCamera, $"the ambience under test has no camera");
        ctx.Check(FadeAlphaAt(ctx, state, new Vector3(0f, 0f, -450f), amb) is { } far
                  && Mathf.IsEqualApprox(far, 1f),
            $"with no camera a particle past the far cutoff still draws at full alpha");
        ctx.Check(FadeAlphaAt(ctx, state, new Vector3(0f, 0f, -20f), amb) is { } near
                  && Mathf.IsEqualApprox(near, 1f),
            $"and one inside the near cull draws too");
    }

    // ---- PRIORITY inflates the sprite -------------------------------------------------------------

    /// <summary>A PRIORITY 10 puffer spawns particles exactly 20% larger
    /// than the same state at PRIORITY 0 — <c>1 + 0.02·10 = 1.2</c>, the hardware-path <c>K</c>
    /// (<c>PriorityScaleDefault</c>). Burst mode, NUMBER 1, a degenerate SIZE_RANGE so the drawn
    /// size is deterministic and the only thing that can move it is PRIORITY.</summary>
    private static void PufferPrioritySize(TestContext ctx)
    {
        static PufferState State(float priority) => new()
        {
            Name = "test_priority",
            Number = 1,
            TimeInterval = 10f,
            SizeMin = 2f,
            SizeMax = 2f,
            LifetimeMin = 100f,
            LifetimeMax = 100f,
            Priority = priority,
            Textures = new[] { "smoke101" },
        };

        static float DrawnSize(TestContext ctx, PufferState state)
        {
            var gpu = new RecordingEmitterRenderer();
            var puffer = Puffer.CreateWith(state, gpu, activeDuration: 0.01f);
            ctx.Host.AddChild(puffer);
            try
            {
                puffer.Burst(Vector3.Zero);
                puffer._Process(1f / 60f);
                ctx.Same(1, gpu.Shown, $"{state.Name} priority={state.Priority}: exactly one particle is under test");
                return gpu.LastFrame[0].Size;
            }
            finally
            {
                puffer.Free();
            }
        }

        float baseline = DrawnSize(ctx, State(0f));
        float inflated = DrawnSize(ctx, State(10f));

        ctx.Check(Mathf.IsEqualApprox(baseline, 4f, 1e-4f),
            $"PRIORITY 0 draws at the plain size (radius 2 × diameter convention, A1) size={baseline:0.0000}");
        ctx.Check(Mathf.IsEqualApprox(inflated, baseline * 1.2f, 1e-4f),
            $"PRIORITY 10 draws 20% larger than PRIORITY 0 — 1 + 0.02·10 baseline={baseline:0.0000} inflated={inflated:0.0000}");
    }

    /// <summary>One particle, no randomness: NUMBER 1, a degenerate random-velocity range (min ==
    /// max, so the draw is exact), no deviation, no growth, no ramps. Burst mode, so the single
    /// batch lands at t = 0 at the burst point.</summary>
    private static PufferState WindTestState(string name, Vector3 v0, Vector3 accel,
        float friction, float windFactor) => new()
        {
            Name = name,
            Number = 1,
            TimeInterval = 10f,          // one batch only, inside the 0.01 s active duration below
            SizeMin = 1f,
            SizeMax = 1f,
            LifetimeMin = 100f,
            LifetimeMax = 100f,
            MinRandomVelocity = v0,
            MaxRandomVelocity = v0,
            WorldAcceleration = accel,
            Friction = friction,
            WindFactor = windFactor,
            Textures = new[] { "smoke101" },
        };

    /// <summary>Runs one <see cref="WindTestState"/> particle for <paramref name="frames"/> steps of
    /// <paramref name="dt"/> and returns its displacement. The burst is fired at the world origin
    /// and a burst puffer's particles are stored in its own (there, identity) frame, so the
    /// written position IS the displacement.</summary>
    private static Vector3 RunWindParticle(TestContext ctx, PufferState state, EffectAmbience ambience,
        int frames, float dt)
    {
        var gpu = new RecordingEmitterRenderer();
        var puffer = Puffer.CreateWith(state, gpu, activeDuration: 0.01f, ambience: ambience);
        ctx.Host.AddChild(puffer);
        try
        {
            puffer.Burst(Vector3.Zero);
            for (int i = 0; i < frames; i++)
                puffer._Process(dt);
            ctx.Same(1, gpu.Shown, $"{state.Name}: exactly one particle is under test");
            return gpu.LastFrame.Count > 0 ? gpu.LastFrame[0].Position : Vector3.Zero;
        }
        finally
        {
            puffer.Free();
        }
    }

    /// <summary>Zero wind ⇒ the coupling term vanishes: <c>(v − 0)·damp + 0 == v·damp</c>, so a
    /// friction particle decays to rest on the pure damped curve. Its closed form is exact — with
    /// no acceleration, <c>v_n = v0·damp^n</c> and <c>pos_n = v0·dt·(1 − damp^n)/(1 − damp)</c>,
    /// the geometric sum of the positions step, which the traced order takes BEFORE the damp.
    ///
    /// <para>⚠ The able-to-fail control matters more than the match: the identical emitter in a
    /// 10 m/s wind must land somewhere else, or "it followed the windless curve" would be a
    /// statement about a branch nothing exercised (<c>docs/verification.md</c>).</para></summary>
    private static void PufferWindZeroIsNoOp(TestContext ctx)
    {
        const float Dt = 1f / 60f, Friction = 3f;
        const int Frames = 120;
        var v0 = new Vector3(20f, 0f, 0f);
        float damp = Mathf.Exp(-Friction * Dt);
        float expectedX = v0.X * Dt * (1f - Mathf.Pow(damp, Frames)) / (1f - damp);

        var still = new EffectAmbience();   // never written: Wind stays Vector3.Zero
        var calm = RunWindParticle(ctx, WindTestState("wind_zero", v0, Vector3.Zero, Friction, 1f),
            still, Frames, Dt);
        ctx.Check(Mathf.IsEqualApprox(calm.X, expectedX, 0.01f),
            $"at zero wind a friction particle rides the pure damped curve x={calm.X:0.0000} expected={expectedX:0.0000}");
        ctx.Check(Mathf.Abs(calm.Y) < 1e-4f && Mathf.Abs(calm.Z) < 1e-4f,
            $"and moves in no other axis y={calm.Y:0.000000} z={calm.Z:0.000000}");
        // 2 s at friction 3 leaves damp^120 = e^-6 = 0.0025 of the launch speed: at rest, and the
        // remaining travel per frame is below a millimetre.
        ctx.Check(Mathf.Pow(damp, Frames) < 0.01f,
            $"120 frames at friction 3 really is 'to rest' remaining={Mathf.Pow(damp, Frames):0.0000}");

        var blowing = new EffectAmbience();
        blowing.SetWind(new Vector3(0f, 0f, 10f));
        var carried = RunWindParticle(ctx, WindTestState("wind_control", v0, Vector3.Zero, Friction, 1f),
            blowing, Frames, Dt);
        ctx.Check(carried.Z > 5f,
            $"ABLE-TO-FAIL CONTROL: the same particle in a 10 m/s crosswind is carried instead z={carried.Z:0.000}");
        ctx.Check(Mathf.IsEqualApprox(carried.X, expectedX, 0.01f),
            $"and the crosswind leaves the unblown axis alone x={carried.X:0.0000}");
    }

    /// <summary>The reorder, isolated: friction 0, no wind, a pure world acceleration. The traced
    /// order steps position on LAST frame's velocity and only then adds <c>a·dt</c>, giving
    /// <c>pos_n = a·dt²·n(n−1)/2</c>. Our old order (<c>v += a·dt</c> first, position after) gave
    /// <c>a·dt²·n(n+1)/2</c> — larger by exactly <c>a·dt²·n</c>, i.e. one frame of the current
    /// velocity, which is the whole of the difference and is asserted as such.</summary>
    private static void PufferAccelOrder(TestContext ctx)
    {
        const float Dt = 1f / 60f;
        const int Frames = 60;
        var accel = new Vector3(0f, -9.8f, 0f);
        float traced = accel.Y * Dt * Dt * Frames * (Frames - 1) / 2f;
        float oldOrder = accel.Y * Dt * Dt * Frames * (Frames + 1) / 2f;

        var still = new EffectAmbience();
        var end = RunWindParticle(ctx, WindTestState("accel_order", Vector3.Zero, accel, 0f, 1f),
            still, Frames, Dt);

        ctx.Check(Mathf.IsEqualApprox(end.Y, traced, 0.0005f),
            $"accel lands after the position step y={end.Y:0.00000} traced={traced:0.00000}");
        ctx.Check(!Mathf.IsEqualApprox(end.Y, oldOrder, 0.0005f),
            $"and NOT where the old order put it old={oldOrder:0.00000}");
        ctx.Check(Mathf.IsEqualApprox(oldOrder - traced, accel.Y * Dt * Dt * Frames, 0.0005f),
            $"the gap is exactly one frame of the final velocity gap={oldOrder - traced:0.00000}");
    }

    /// <summary>The per-puffer coupling. Both particles start at rest with no acceleration, so the
    /// ONLY thing that can move them is the wind: <c>WIND_FACTOR</c> 0 must therefore not move at
    /// all, and 1 must converge on the wind velocity. The carried one's closed form is exact —
    /// <c>v_n = w(1 − damp^n)</c>, <c>pos_n = w·dt·(n − (1 − damp^n)/(1 − damp))</c>.</summary>
    private static void PufferWindFactorCoupling(TestContext ctx)
    {
        const float Dt = 1f / 60f, Friction = 3f;
        const int Frames = 120;
        var wind = new Vector3(8f, 0f, 0f);
        float damp = Mathf.Exp(-Friction * Dt);
        float geometric = (1f - Mathf.Pow(damp, Frames)) / (1f - damp);
        float carriedX = wind.X * Dt * (Frames - geometric);

        var blowing = new EffectAmbience();
        blowing.SetWind(wind);

        var uncoupled = RunWindParticle(ctx,
            WindTestState("wf_zero", Vector3.Zero, Vector3.Zero, Friction, 0f), blowing, Frames, Dt);
        ctx.Check(uncoupled.Length() < 1e-5f,
            $"WIND_FACTOR 0 is genuinely becalmed in an 8 m/s wind drift={uncoupled.Length():0.000000}");

        var carried = RunWindParticle(ctx,
            WindTestState("wf_one", Vector3.Zero, Vector3.Zero, Friction, 1f), blowing, Frames, Dt);
        ctx.Check(Mathf.IsEqualApprox(carried.X, carriedX, 0.01f),
            $"WIND_FACTOR 1 is fully carried x={carried.X:0.0000} expected={carriedX:0.0000}");

        var half = RunWindParticle(ctx,
            WindTestState("wf_half", Vector3.Zero, Vector3.Zero, Friction, 0.3f), blowing, Frames, Dt);
        ctx.Check(Mathf.IsEqualApprox(half.X, carriedX * 0.3f, 0.01f),
            $"and the coupling is linear in WIND_FACTOR — 0.3 drifts 0.3× as far x={half.X:0.0000}");
    }

    /// <summary>The engine's own gate: the whole damp-toward-wind block sits inside
    /// <c>if (friction != 0)</c>, so a frictionless puffer is untouched by any wind at any factor.
    /// It is load-bearing only because there is a wind term inside the block: with an unconditional
    /// <c>Exp(0) == 1</c> damp and no wind the gate would be the identity.</summary>
    private static void PufferFrictionlessIgnoresWind(TestContext ctx)
    {
        const float Dt = 1f / 60f;
        const int Frames = 120;
        var blowing = new EffectAmbience();
        blowing.SetWind(new Vector3(50f, 0f, 0f));

        var end = RunWindParticle(ctx,
            WindTestState("no_friction", Vector3.Zero, Vector3.Zero, 0f, 1f), blowing, Frames, Dt);
        ctx.Check(end.Length() < 1e-5f,
            $"a FRICTION 0 puffer feels no wind at all drift={end.Length():0.000000}");
    }

    /// <summary>The gust itself (<see cref="WorldWind"/>), against the shipped authored values —
    /// <c>STATIC_VELOCITY (0,2,0)</c>, <c>RANDOM_MAX_SPEED 10</c>, <c>RANDOM_ACCEL 5</c>,
    /// <c>RANDOM_ANG_VEL 5</c>, which every one of the install's 53 weather readers carries.</summary>
    private static void PufferWindGustModel(TestContext ctx)
    {
        var wind = new WorldWind(new Vector3(0f, 2f, 0f), 10f, 5f, 5f, new System.Random(7));
        ctx.Check(wind.Velocity == new Vector3(0f, 2f, 0f),
            $"frame 0 is the static vector alone (the engine's globals start at BSS zero)");

        float maxHorizontal = 0f;
        bool everGusted = false;
        for (int i = 0; i < 2000; i++)
        {
            wind.Step(1f / 60f);
            ctx.Check(wind.Magnitude >= 0f && wind.Magnitude <= 10f,
                $"gust magnitude stays in [0, RANDOM_MAX_SPEED] mag={wind.Magnitude:0.000}");
            if (!Mathf.IsEqualApprox(wind.Velocity.Y, 2f))
                ctx.Check(false, $"the gust is horizontal — Y must stay the static 2 m/s, saw {wind.Velocity.Y}");
            float h = new Vector2(wind.Velocity.X, wind.Velocity.Z).Length();
            maxHorizontal = Mathf.Max(maxHorizontal, h);
            if (h > 1f)
                everGusted = true;
        }
        ctx.Check(everGusted, $"the gust actually blows over 2000 frames");
        ctx.Check(maxHorizontal <= 10.001f, $"and never exceeds the ceiling max={maxHorizontal:0.000}");

        // The still-air wind a mission without a weather.json gets: no draws, no drift, ever.
        var still = WorldWind.Still();
        for (int i = 0; i < 100; i++)
            still.Step(1f / 60f);
        ctx.Check(still.Velocity == Vector3.Zero, $"WorldWind.Still() never blows v={still.Velocity}");
    }

    /// <summary>A moving DISTANCE_INTERVAL host keeps AT_NODE's offset in the host frame for every
    /// emitted puff, not only the still-host fallback. The retail speed cue is the canary: its
    /// player,0,0,-60 attachment is what places the wisps 60 m ahead of the aircraft.</summary>
    private static void PufferTrailOffset(TestContext ctx, PufferState state)
    {
        ctx.Check(state.AtNodeOffset.IsEqualApprox(new Vector3(0f, 0f, -60f)),
            $"cuepuffer1 carries its authored 60 m forward AT_NODE offset");
        ctx.Check(Mathf.IsEqualApprox(30f, state.DistanceInterval),
            $"cuepuffer1 emits every authored 30 m");
        state.DeviationDistance = 0f;
        var gpu = new RecordingEmitterRenderer();
        var puffer = Puffer.CreateWith(state, gpu);
        ctx.Host.AddChild(puffer);
        try
        {
            const float dt = 1f / 60f;
            puffer.Emit(Vector3.Zero, Basis.Identity, dt);
            puffer.Emit(new Vector3(60f, 0f, 0f), Basis.Identity, dt);
            puffer._Process(dt);
            ctx.Same(3, gpu.Shown, $"homing puff plus two distance puffs were emitted");
            ctx.Check(gpu.LastFrame.All(p => Mathf.Abs(p.Position.Z + 60f) < 0.1f),
                $"every moving-trail puff keeps the authored -60 m local-Z offset");
        }
        finally
        {
            puffer.Free();
        }
    }

    /// <summary>The speed-cue adapter selects the retail altitude bands, preserves the selected
    /// puffer above the final authored threshold, suppresses every cue near ground, and Reset
    /// removes live particles so a respawn cannot bridge positions.</summary>
    private static void SpeedCueBands(TestContext ctx, params PufferState[] states)
    {
        var gpu = new RecordingEmitterRenderer[3];
        var puffer = new Puffer[3];
        for (int i = 0; i < 3; i++)
        {
            states[i].DeviationDistance = 0f;
            gpu[i] = new RecordingEmitterRenderer();
            puffer[i] = Puffer.CreateWith(states[i], gpu[i]);
            ctx.Host.AddChild(puffer[i]);
        }
        try
        {
            var cue = SpeedCue.CreateWith(puffer[0], puffer[1], puffer[2]);
            const float dt = 1f / 60f;
            void Tick()
            {
                foreach (var p in puffer)
                    p._Process(dt);
            }

            cue.Update(dt, Vector3.Zero, Basis.Identity, 700f, 100f);
            Tick();
            cue.Update(dt, new Vector3(60f, 0f, 0f), Basis.Identity, 700f, 100f);
            Tick();
            ctx.Same(3, gpu[0].Shown, $"below 800 m cue1 emits at its 30 m interval");
            ctx.Same(-1, gpu[1].Shown, $"below 800 m cue2 has never started");
            ctx.Same(-1, gpu[2].Shown, $"below 800 m cue3 has never started");

            for (int i = 0; i < 6; i++)
            {
                cue.Update(dt, new Vector3(60f, 0f, 0f), Basis.Identity, 850f, 100f);
                Tick();
            }
            Tick();
            cue.Update(dt, new Vector3(90f, 0f, 0f), Basis.Identity, 850f, 100f);
            Tick();
            ctx.Same(3, gpu[1].Shown, $"800-900 m cue2 emits at its 15 m interval");

            for (int i = 0; i < 6; i++)
            {
                cue.Update(dt, new Vector3(90f, 0f, 0f), Basis.Identity, 1000f, 100f);
                Tick();
            }
            Tick();
            cue.Update(dt, new Vector3(106f, 0f, 0f), Basis.Identity, 1000f, 100f);
            Tick();
            ctx.Same(3, gpu[2].Shown, $"900-1200 m cue3 emits at its 8 m interval");

            cue.Reset();
            ctx.Same(0, gpu[0].Shown, $"speed-cue reset clears cue1 particles");
            ctx.Same(0, gpu[1].Shown, $"speed-cue reset clears cue2 particles");
            ctx.Same(0, gpu[2].Shown, $"speed-cue reset clears cue3 particles");

            cue.Update(dt, Vector3.Zero, Basis.Identity, 700f, 49f);
            Tick();
            ctx.Same(0, gpu[0].Shown, $"within 50 m of ground no speed cue starts");

            for (int i = 0; i < 6; i++)
                cue.Update(dt, Vector3.Zero, Basis.Identity, 850f, 100f);
            for (int i = 0; i < 6; i++)
                cue.Update(dt, Vector3.Zero, Basis.Identity, 1600f, 100f);
            cue.Update(dt, new Vector3(30f, 0f, 0f), Basis.Identity, 1600f, 100f);
            Tick();
            ctx.Same(4, gpu[1].Shown,
                $"above 1500 m preserves the previously selected puffer, matching the empty ELSE");
        }
        finally
        {
            foreach (var p in puffer)
                p.Free();
        }
    }

    /// <summary>C1 and C4 share cue geometry and timing but retain their chapter-authored alpha
    /// variants instead of collapsing onto one global tune.</summary>
    private static void SpeedCueChapterVariants(TestContext ctx)
    {
        var expected = new[]
        {
            (Chapter: "C1", PeakAlpha: new[] { 0.4f, 0.5f, 0.5f }),
            (Chapter: "C4", PeakAlpha: new[] { 0.6f, 0.7f, 0.7f }),
        };
        foreach (var chapter in expected)
        {
            string path = SessionPaths.ChapterZrdr(ctx.DataRoot, chapter.Chapter);
            for (int i = 0; i < 3; i++)
            {
                var state = PufferState.Load(path, "speed_cue.json", $"cuepuffer{i + 1}");
                ctx.Check(state != null, $"{chapter.Chapter} defines cuepuffer{i + 1}");
                if (state == null)
                    continue;
                ctx.Check(state.Colors.Count == 3,
                    $"{chapter.Chapter} cuepuffer{i + 1} keeps its three-point colour ramp");
                if (state.Colors.Count == 3)
                    ctx.Check(Mathf.Abs(chapter.PeakAlpha[i] - state.Colors[1].Color.A) < 0.001f,
                        $"{chapter.Chapter} cuepuffer{i + 1} keeps its authored peak alpha");
            }
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
    /// on the very first frame, a long frame's OLDER catch-up batches are born already dead (the
    /// sub-frame age offset + the engine's born-dead skip) while its youngest are born alive and
    /// still cannot overrun the pool, the hitch drains its own accumulator so no burst follows it
    /// (the batch loop is uncapped), and <c>Stop</c> ends emission without cutting the live
    /// particles short. See <c>docs/org/puffer.md</c>.</summary>
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

            // A 5 s hitch. The batch loop has no per-frame cap, so the accumulator of 5.017 s over
            // TIME_INTERVAL 0.2 asks for all 25 batches at once. Batch b carries the engine's
            // sub-frame start-age offset (1 - frac)·dt with
            // frac = (b+1)·interval/accumulator, so they are born 4.80, 4.60, … 0.01 s old: the
            // early ones are far past this state's ≤ 1 s LIFETIME_RANGE and the born-dead skip
            // discards them without taking a pool slot, while the LAST few — those whose virtual
            // emission moment is within a lifetime of now — are born alive, which is exactly the
            // ~1 s of particles a 1 s-lifetime emitter should have left after a 5 s stall.
            // Read before _Process, so this is the SPAWN path and not the reaper tidying up.
            puffer.Emit(origin, Basis.Identity, 5f);
            int afterHitch = puffer.LiveCount;
            ctx.Check(afterHitch <= gpu.Capacity,
                $"a 25-batch hitch cannot overrun the pool live={afterHitch} pool={gpu.Capacity}");
            ctx.Check(afterHitch > 18,
                $"its youngest batches ARE born alive — the hitch is not silently dropped whole live={afterHitch}");
            // ⚠ Not asserted here: how many of the 450 spawns the born-dead skip discarded. The
            // pool is sized to one LIFETIME's worth of emission and the survivors ARE one
            // lifetime's worth, so the two meet and the count saturates at capacity — a check on
            // the discard fraction would be answered by the pool clamp, not by the skip. The
            // skip's own arithmetic is read where it can be: the drain assertion below, whose
            // exactness depends on all 25 batches having been generated and charged to the
            // accumulator.
            puffer._Process(1f / 60f);

            // The load-bearing half of the uncapped loop: the hitch DRAINED its own accumulator
            // (25 × 0.2 = 5.0 of 5.017), so the next ordinary frame carries 0.033 s — a sixth of
            // an interval — and emits nothing. A per-frame batch cap would leave the surplus in the
            // accumulator and make this frame emit 8 more batches, a burst the engine never
            // produces. Measured with no _Process in between, so only the spawn path can move the
            // count. (Able to fail: with a cap in place this reads a full pool instead of no
            // change.)
            int beforeNext = puffer.LiveCount;
            puffer.Emit(origin, Basis.Identity, 1f / 60f);
            ctx.Same(beforeNext, puffer.LiveCount,
                $"the hitch drained its accumulator; the next frame emits no catch-up burst live={puffer.LiveCount}");
            puffer._Process(1f / 60f);
            ctx.Check(gpu.MaxIndex < gpu.Capacity,
                $"the sustain path never writes past its pool max={gpu.MaxIndex} pool={gpu.Capacity}");

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

    /// <summary>A fast-moving TIME_INTERVAL emitter spreads a frame's batches along its motion
    /// since the previous call instead of stacking them all on today's pose
    /// (<c>prevOrigin + (origin - prevOrigin)*frac</c>, paired with the matching
    /// <c>(1 - frac)*dt</c> start-age offset), and the first frame after a <c>Stop()</c>/restart
    /// re-homes instead of interpolating from the stale pre-stop pose (the rocket-explosion
    /// ghost-trail rule). See <c>docs/org/puffer.md</c>.
    ///
    /// <para>A synthetic zero-velocity, zero-deviation, zero-COLORS state removes every other
    /// source of scatter so the eight spawned positions are exactly the interpolated points, not a
    /// distribution to eyeball: with TIME_INTERVAL 0.1 s and a single 0.8 s accumulator (homed with
    /// 0 leftover carry), <c>accumulator/interval = 8</c> exactly, so
    /// <c>frac_k = (k+1)/8</c> for <c>k = 0..7</c> lands at X = 12.5, 25, …, 100 — evenly spaced
    /// 12.5 m apart, the last one exactly at the current pose, none at the origin. LIFETIME_RANGE
    /// is pinned to 10 s for two reasons: the batches' age offsets (0.7 s down to 0 s, on the raw
    /// unclamped dt the engine uses) stay well clear of the born-dead skip, so this test reads the
    /// interpolation and nothing else; and they stay inside the life-fade envelope's linear ramp
    /// (<c>FadeFor</c>'s first 0.12 of life), which turns the rendered alpha into a direct, exact
    /// readout of the age-offset term: <c>alpha = ageOffset / (10 * 0.12)</c>. The skip's own
    /// reachability is asserted in <c>PufferSustainMode</c>'s 5 s hitch.</para></summary>
    private static void PufferSustainSubFrameEmission(TestContext ctx)
    {
        var state = new PufferState
        {
            Name = "test_subframe_sustain",
            Number = 1,
            TimeInterval = 0.1f,
            SizeMin = 1f,
            SizeMax = 1f,
            LifetimeMin = 10f,
            LifetimeMax = 10f,
        };

        var gpu = new RecordingEmitterRenderer();
        var puffer = Puffer.CreateWith(state, gpu, sustained: true);
        ctx.Host.AddChild(puffer);
        try
        {
            var a = new Vector3(0f, 1200f, 0f);
            puffer.Emit(a, Basis.Identity, 0f); // homes at A, carry = TimeInterval exactly ⇒ 0 leftover
            puffer._Process(0f);
            ctx.Same(1, gpu.Shown, $"the homing frame sputters its own single batch at A");

            var b = a + new Vector3(100f, 0f, 0f); // 100 m in one (hitched) frame
            puffer.Emit(b, Basis.Identity, 0.8f);  // accumulator = 0 + 0.8 = 8 × interval exactly
            puffer._Process(0f);
            ctx.Same(9, gpu.Shown, $"8 more batches join the homing one shown={gpu.Shown}");

            var moved = gpu.LastFrame.Where(p => p.Position.X > 1f).OrderBy(p => p.Position.X).ToList();
            ctx.Same(8, moved.Count, $"the jump's own 8 batches, excluding the homing puff at A");

            bool evenlySpaced = true, endsAtB = false, noneAtOrigin = true;
            for (int k = 0; k < moved.Count; k++)
            {
                float expectedX = 12.5f * (k + 1);
                if (!Mathf.IsEqualApprox(moved[k].Position.X, expectedX, 0.05f))
                    evenlySpaced = false;
                // ageOffset_k = (1 - (k+1)/8) * dt with the raw dt = 0.8 s, i.e. 0.7, 0.6, … 0.0;
                // alpha = ageOffset / (10 * FadeIn=0.12).
                float expectedAlpha = (0.8f - 0.1f * (k + 1)) / 1.2f;
                if (!Mathf.IsEqualApprox(moved[k].Alpha, expectedAlpha, 0.001f))
                    ctx.Check(false,
                        $"batch {k} alpha reads its age offset directly: want {expectedAlpha:F4} got {moved[k].Alpha:F4}");
            }
            endsAtB = Mathf.IsEqualApprox(moved[^1].Position.X, 100f, 0.05f);
            noneAtOrigin = moved.All(p => p.Position.X > 5f);
            ctx.Check(evenlySpaced,
                $"eight spawn positions evenly spaced 12.5 m apart along the segment, not stacked at one point");
            ctx.Check(endsAtB, $"the last (frac=1) batch sits exactly at the current pose");
            ctx.Check(noneAtOrigin, $"none of the eight sit back at the segment's start");

            // Restart case: Stop() then Emit() at a new, far site — the first frame's puffs must
            // appear only at the new site, never strung back from B (the ghost-trail trap). The
            // restart frame is ALSO given a multi-batch accumulator (the re-home's own 0.1 s of
            // carry plus 0.8 s of dt = 9 batches, since the batch loop is uncapped):
            // a batch count of exactly 1 would land at frac=1 regardless of prevOrigin, which
            // would pass even with the re-home fix missing — this is the able-to-fail control
            // (a stale prevOrigin=B would string 8 puffs from ~211 to ~989, well inside the
            // "stray" window below).
            puffer.Stop();
            var c = b + new Vector3(1000f, 0f, 0f);
            puffer.Emit(c, Basis.Identity, 0.8f);
            puffer._Process(0f);
            int strays = gpu.LastFrame.Count(p => p.Position.X > b.X + 10f && p.Position.X < c.X - 10f);
            ctx.Same(0, strays,
                $"a restart re-homes at the new site — no puffs interpolated across the 1000 m jump");
            ctx.Check(gpu.LastFrame.Any(p => p.Position.DistanceTo(c) < 3f),
                $"the restart's first batch lands at the new site");
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

    /// <summary>The original's teleport guard on the DISTANCE path: the frame's motion length is
    /// added to the emission accumulator only <c>if (len &lt; 200.0)</c>, so an emitter carried
    /// across the world in one frame lays no puff line along the jump.
    /// <see cref="PufferStopRevive"/> covers the jump that goes through <c>Stop</c> (which
    /// re-homes); this is the one that does NOT — a pooled slot re-pointed at a new site while
    /// still trailing, which without the guard draws the whole 500 m as smoke.
    ///
    /// <para>Four steps, each of which fails differently: a 199 m move pins the boundary FROM BELOW
    /// (a guard written as "any big move" would eat it), the 500 m jump emits nothing, the guard
    /// counter reads 1 — which is also the proof the log line executed, since it is incremented in
    /// the same statement that emits it, so the zero counts in the regression log are only evidence
    /// because this makes the line fire — and a 20 m move afterwards proves the carried remainder
    /// survived the guard untouched rather than being reset with it.</para></summary>
    private static void PufferTeleportGuard(TestContext ctx, PufferState state)
    {
        var gpu = new RecordingEmitterRenderer();
        var puffer = Puffer.CreateWith(state, gpu);
        ctx.Host.AddChild(puffer);
        try
        {
            const float dt = 1f / 60f;
            const float interval = 2f;   // smokepuffer's authored DISTANCE_INTERVAL, asserted above
            var a = new Vector3(0f, 1400f, 0f);
            puffer.Emit(a, Basis.Identity, dt);          // homes, + one still-host sputter batch
            puffer._Process(dt);
            ctx.Same(1, gpu.Shown, $"the homing frame sputters its own batch shown={gpu.Shown}");

            // 199 m: one metre under the guard, and the largest move the engine still accumulates.
            var b = a + new Vector3(199f, 0f, 0f);
            puffer.Emit(b, Basis.Identity, dt);
            puffer._Process(dt);
            ctx.Same(1 + (int)(199f / interval), gpu.Shown,
                $"a 199 m move is under the 200 m guard and emits its full 99 puffs shown={gpu.Shown}");
            ctx.Same(0, puffer.TeleportGuardCount, $"and does not trip the guard");

            // The teleport: 500 m in one frame, with no Stop in between — the case Stop's re-home
            // rule cannot reach.
            int before = gpu.Shown;
            var c = b + new Vector3(500f, 0f, 0f);
            puffer.Emit(c, Basis.Identity, dt);
            puffer._Process(dt);
            ctx.Same(before, gpu.Shown, $"a 500 m frame emits nothing at all shown={gpu.Shown}");
            ctx.Same(1, puffer.TeleportGuardCount,
                $"the guard tripped once, which is also the proof its log line fired");
            ctx.Same(0, gpu.LastFrame.Count(p => p.Position.X > b.X + 10f && p.Position.X < c.X - 10f),
                $"no puff was laid anywhere along the 500 m jump");

            // 20 m from the new pose: 1 m of carry survived the guard, so 21 m buys 10 puffs.
            puffer.Emit(c + new Vector3(20f, 0f, 0f), Basis.Identity, dt);
            puffer._Process(dt);
            ctx.Same(before + 10, gpu.Shown,
                $"the emitter resumes at the new site with its carry intact shown={gpu.Shown}");
        }
        finally
        {
            puffer.Free();
        }
    }

    /// <summary><c>START_AGE_RANGE</c>: a synthetic state authoring a negative minimum, mirroring
    /// <c>fire_at_zepskin3</c>'s (−1.0, 0.1) — the census's only negative case, ~91% of whose
    /// particles are born already aged. Checks four things the integrator settles: every particle
    /// is drawn (this state's drawn age never reaches its drawn lifetime, so the born-dead skip —
    /// which IS implemented, and which the sub-frame age term makes reachable on any long frame —
    /// correctly stays silent here); a
    /// negative-age particle is pinned to stop 0 of both the colour ramp and the growth
    /// ramp rather than being skipped or extrapolated past/below it; and it outlives its authored
    /// LIFETIME_RANGE by up to |StartAgeMin| instead of reaping at its unshifted lifetime.</summary>
    private static void PufferStartAgeBehavior(TestContext ctx)
    {
        var state = new PufferState
        {
            Name = "test_start_age",
            Number = 40,
            TimeInterval = 1000f, // never re-fires on its own — one Burst, one batch
            SizeMin = 2f,
            SizeMax = 2f,
            GrowthFactor = 5f, // an unclamped negative t would shrink (or negate) size below BaseSize
            LifetimeMin = 1f,
            LifetimeMax = 1f,
            StartAgeMin = -1f,
            StartAgeMax = 0.1f,
            Colors = new[] { (0f, Colors.Red), (0.5f, Colors.Green) },
        };
        ctx.Check(state.HasStartAgeRange, $"the synthetic state authors START_AGE_RANGE");

        var gpu = new RecordingEmitterRenderer();
        var puffer = Puffer.CreateWith(state, gpu, activeDuration: 0.01f);
        ctx.Host.AddChild(puffer);
        try
        {
            puffer.Burst(new Vector3(0f, 1100f, 0f));
            puffer._Process(1f / 60f);

            ctx.Same(40, gpu.Shown,
                $"every particle draws — this state's start age (max 0.1) never reaches its lifetime (1), so the born-dead skip stays silent shown={gpu.Shown}");

            int redCount = gpu.LastFrame.Count(p => p.Color == Colors.Red);
            ctx.Check(redCount > gpu.LastFrame.Count / 2,
                $"most particles (born with age <= 0) show the colour ramp's stop 0, not skipped or interpolated past it red={redCount}/{gpu.LastFrame.Count}");

            var bySize = gpu.LastFrame.GroupBy(p => p.Size).OrderByDescending(g => g.Count()).First();
            ctx.Check(bySize.Count() > gpu.LastFrame.Count / 2,
                $"most particles collapse onto one identical size — the growth ramp clamps age<=0 to stop 0 rather than each drawing its own extrapolated value count={bySize.Count()}/{gpu.LastFrame.Count}");
            float commonSize = bySize.Key;
            ctx.Check(gpu.LastFrame.All(p => p.Size >= commonSize - 1e-3f),
                $"no particle sits below the clamped stop-0 size — an unclamped negative age would shrink (or negate) it");

            // Past the unmodified 1 s LIFETIME_RANGE, particles born with a negative start age
            // must still be alive: they need up to Life - StartAgeMin = 2 s of Age to reap.
            for (int i = 0; i < 63; i++) // 1.05 s
                puffer._Process(1f / 60f);
            // ~85% of the authored range is expected to still be alive here (age0 in [-1,0.1)
            // uniformly, elapsed ~1.07 s ⇒ P(dead) ≈ 0.15); a generous >=20/40 margin below that.
            ctx.Check(gpu.Shown >= 20,
                $"negative-age particles outlive their authored LIFETIME_RANGE by |age0| instead of reaping on the old schedule shown={gpu.Shown}");

            for (int i = 0; i < 60; i++) // + 1 s more = past every particle's 2 s worst case
                puffer._Process(1f / 60f);
            ctx.Same(0, gpu.Shown, $"every particle eventually reaps once its real (shifted) age reaches its lifetime");
        }
        finally
        {
            puffer.Free();
        }
    }

    /// <summary>The <c>fire_n_smoke</c> column, measured. What this suite guards is the AUTHORED
    /// column, with no engine-side rise or lifetime multiplier on top, so a change that quietly
    /// shortens or re-stretches the fire has to argue with a number. See
    /// <c>docs/org/puffer.md</c>.
    ///
    /// <para>The state comes from the chapter's own compiled program, not from a reader:
    /// <c>large_30sec_fire</c>'s <c>fire_n_smoke</c> authors neither NUMBER nor DISTANCE_INTERVAL,
    /// so <see cref="PufferState.FindInReader"/> would call it a stub and return null. The compiled
    /// event is what the game actually runs.</para>
    ///
    /// <para>Two heights are recorded because they answer different questions. The CENTRE apex is
    /// the integration — spawn velocity against FRICTION and WORLD_ACCELERATION. The DRAWN top adds
    /// the sprite's own half-extent, so it also moves with the sprite-size convention. The fade
    /// switches are forced off: this puffer authors NEAR_FADE (70, 20), and at the suite's
    /// origin-camera every particle would otherwise be near-culled and nothing would be measured at
    /// all.</para>
    ///
    /// <para>It is run twice: in still air, and in C1 IA1's own authored wind. That weather's
    /// <c>STATIC_VELOCITY</c> is <c>(0, 2, 0)</c> — straight UP — and friction damps toward the
    /// wind rather than toward rest, so with FRICTION 0.6 against WORLD_ACCELERATION −1 the
    /// particle's terminal velocity is <c>2 − 1/0.6 = +0.33 m/s</c>: it never turns over, and the
    /// column climbs until the lifetime ends. That is why the authored numbers reach on their own,
    /// and it is asserted rather than argued — a regression that decouples the wind would show up
    /// here as the still-air and wind columns converging. The static part is held constant; the
    /// shipped model gusts around it (<c>RANDOM_MAX_SPEED</c> 10, <c>RANDOM_ACCEL</c> 5), which is
    /// a distribution, not a height.</para>
    ///
    /// <para>⚠ <b>Every height is one seed's EXTREME</b> — the tallest particle of ~300 draws — and
    /// each emitter's <c>_rng</c> is seeded by how many puffers were constructed before it, so this
    /// suite's own numbers move ~±1.5 m between a <c>-Filter</c> run and a full run. That is why
    /// the checks below are ratios and wide bands and the exact figures live in <c>ctx.Note</c>:
    /// pinning a decimal here would fail on a run order, not on a regression.</para></summary>
    private static void PufferFireColumn(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var defs = world.Session.Program.Subset(new[] { "large_30sec_fire" }).ByAnimName("large_30sec_fire");
            ctx.Check(defs.Count > 0, $"chapter program has large_30sec_fire defs={defs.Count}");
            if (defs.Count == 0)
                return;

            AnimData? payload = null;
            foreach (var seq in defs[0].Sequences)
            {
                foreach (var ev in seq.Events)
                {
                    if (payload == null && ev.Kind == "PufferState" && (ev.Data.Num("active_state") ?? 0f) > 0f)
                        payload = ev.Data;
                }
            }
            ctx.Check(payload != null, $"large_30sec_fire carries an ACTIVE_STATE 1 PufferState event");
            if (payload == null)
                return;

            var fire = PufferState.FromAnimEvent(payload);

            // The compiled numbers this measurement rests on, asserted rather than assumed: a data
            // change must fail here naming itself, not silently re-baseline the heights below.
            ctx.Check(fire.Name == "fire_n_smoke", $"the 30 s fire's emitter name={fire.Name}");
            ctx.Same(1, fire.Number, $"large_30sec_fire authors no NUMBER — one puff per interval (BL-218)");
            ctx.Check(Mathf.IsEqualApprox(0.1f, fire.TimeInterval), $"TIME_INTERVAL 0.1 s");
            ctx.Check(Mathf.IsEqualApprox(5f, fire.LocalVelocity.Y) && Mathf.IsEqualApprox(4f, fire.WorldVelocity.Y),
                $"the rise is LOCAL_VELOCITY 5 + WORLD_VELOCITY 4 m/s");
            ctx.Check(Mathf.IsEqualApprox(-1f, fire.WorldAcceleration.Y), $"WORLD_ACCELERATION −1 m/s²");
            ctx.Check(Mathf.IsEqualApprox(0.6f, fire.Friction), $"FRICTION 0.6");
            ctx.Check(Mathf.IsEqualApprox(1f, fire.SizeMin) && Mathf.IsEqualApprox(3.5f, fire.SizeMax),
                $"SIZE_RANGE 1–3.5 m (a RADIUS — A1)");
            ctx.Check(Mathf.IsEqualApprox(3.5f, fire.LifetimeMin) && Mathf.IsEqualApprox(5.5f, fire.LifetimeMax),
                $"LIFETIME_RANGE 3.5–5.5 s");
            ctx.Check(Mathf.IsEqualApprox(2.5f, fire.GrowthFactor), $"GROWTH_FACTOR 2.5");

            var clock = GameClock.Current;
            GameClock.Current = null;
            try
            {
                // C1 IA1's authored weather, static part held: extracted/C1/IA1/zrdr/weather.zrd.json
                // WIND STATIC_VELOCITY [0, 2, 0].
                var breeze = new EffectAmbience();
                breeze.SetWind(new Vector3(0f, 2f, 0f));

                var still = MeasureFireColumn(ctx, fire, EffectAmbience.Still);
                var wind = MeasureFireColumn(ctx, fire, breeze);

                ctx.Note($"large_30sec_fire column, 30 s at 1/60, still host — heights above the emitter:");
                ctx.Note($"  still air: centre apex {still.Centre:0.0} m, drawn top {still.Top:0.0} m (pre-A1 sprite: {still.PreA1Top:0.0} m), peak live {still.Live}, largest sprite {still.Sprite:0.0} m");
                ctx.Note($"  C1 IA1 wind (0,2,0): centre apex {wind.Centre:0.0} m, drawn top {wind.Top:0.0} m, peak live {wind.Live}");

                // What the authored column has to keep doing, as assertions rather than prose:
                // (1) the rise itself — LOCAL+WORLD velocity against FRICTION and the −1 accel;
                ctx.Check(still.Centre > 8f && still.Centre < 22f,
                    $"the authored rise integrates to a high-teens column centre={still.Centre:0.0} m");
                // (2) the sprite's own half-extent is a real share of the plume — the SIZE_RANGE
                //     radius showing up in the picture rather than only in a constant;
                ctx.Check(still.Top > still.Centre * 1.3f,
                    $"the drawn top stands well above the centre apex — A1's sprite is a real share of the plume top={still.Top:0.0} centre={still.Centre:0.0}");
                // (3) the wind coupling lifts this puffer. C1 IA1's authored wind blows straight
                //     UP, and with no engine-side fire tune this is the whole reason the authored
                //     column reaches: decouple the wind and the plume loses ~6 m of height.
                ctx.Check(wind.Centre > still.Centre + 3f,
                    $"C1's upward wind lifts the authored column wind={wind.Centre:0.0} still={still.Centre:0.0}");
                // (4) the height the controls verdict settled on: a plume ~2x this band was judged
                //     twice too tall, so re-introducing any rise/lifetime multiplier breaks it
                //     immediately.
                ctx.Check(wind.Top > 24f && wind.Top < 42f,
                    $"the wind-carried plume tops out in the judged band top={wind.Top:0.0} m");
            }
            finally
            {
                GameClock.Current = clock;
            }
        });
    }

    /// <summary>Drives one sustained emitter for 30 s of held-still emission and reports the
    /// column it built: the highest particle CENTRE, the highest drawn sprite TOP (centre plus the
    /// quad's half-side — the renderer scales a 1×1 quad by <c>Size</c>), the peak live population
    /// and the largest sprite ever drawn. Heights are relative to the emitter's own origin.</summary>
    private static (float Centre, float Top, float PreA1Top, int Live, float Sprite) MeasureFireColumn(
        TestContext ctx, PufferState state, EffectAmbience ambience)
    {
        var gpu = new RecordingEmitterRenderer();
        // Every switch off: this puffer authors NEAR_FADE (70, 20) and the suite's camera sits at
        // the origin, so the authored near cull would discard the whole column before it is read.
        var puffer = Puffer.CreateWith(state, gpu, sustained: true, ambience: ambience,
            fade: new PufferFadeSwitches(DistanceFade: false, FarCull: false, NearCull: false));
        ctx.Host.AddChild(puffer);
        try
        {
            var origin = Vector3.Zero;
            const float dt = 1f / 60f;
            float centre = 0f, top = 0f, preA1 = 0f, sprite = 0f;
            int live = 0;
            for (int i = 0; i < 1800; i++)   // the authored 30 s of ACTIVE_STATE 1
            {
                puffer.Emit(origin, Basis.Identity, dt);
                puffer._Process(dt);
                foreach (var p in gpu.LastFrame)
                {
                    float y = p.Position.Y - origin.Y;
                    if (y > centre)
                        centre = y;
                    if (y + p.Size * 0.5f > top)
                        top = y + p.Size * 0.5f;
                    // The same run read at the half-size sprite convention. `Size` is exactly
                    // linear in SizeScaleDefault and the RNG stream does not depend on it, so
                    // quartering the side here is that convention's drawn top exactly, not an
                    // estimate — which is what makes the sprite convention's own contribution to
                    // the column readable without a second build.
                    if (y + p.Size * 0.25f > preA1)
                        preA1 = y + p.Size * 0.25f;
                    if (p.Size > sprite)
                        sprite = p.Size;
                }
                if (puffer.LiveCount > live)
                    live = puffer.LiveCount;
            }
            ctx.Check(live > 0, $"{state.Name}: the emitter actually ran live={live}");
            return (centre, top, preA1, live, sprite);
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

    /// <summary>B2's per-muzzle slot state (the forget + catch-up pass), B3's intercept solver and
    /// B4's candidate scan, the first three in isolation — no plane, no pool,
    /// <see cref="AimAssist"/> is engine-free by design — plus a golden check that the player.json
    /// and weapons.json values the assist consumes parse at their documented shipped figures
    /// (docs/PLAN-sticky-bullets.md "What the data actually ships"). B4's ordnance list is the one
    /// case that needs a live pool, since the list IS a filter over the rounds in flight.</summary>
    private static void AimAssistSuite(TestContext ctx)
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

    /// <summary>The catch-up slerp: a ~0.2 s time constant at the shipped catchup_rate (5.0), full
    /// convergence given enough time, and an outright snap on a single frame at or past
    /// 1/catchup_rate — the real hitch-behaviour difference docs/org/aim-assist.md flags, not a
    /// rounding detail to smooth away.</summary>
    private static void AimAssistCatchup(TestContext ctx)
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

    /// <summary>The forget timer measures time since the barrel last FIRED, not time since a lock
    /// was lost: stop restamping and the target unwinds to local forward exactly
    /// forget_interval seconds later; keep restamping (as B5's fire call will) and it never does.</summary>
    private static void AimAssistForgetTimer(TestContext ctx)
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

    /// <summary>Golden check: the keys B2/B4 consume parse off the real player.json and
    /// weapons.json at their documented shipped values, not just their compiled-in defaults. The
    /// dist_factor one matters most — it ships at 0.0 where the executable's compiled fallback is
    /// 2.5e-4, so a reader that quietly failed to find the key would restore a distance term the
    /// shipped data deliberately turns off.</summary>
    private static void AimAssistShippedData(TestContext ctx)
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

    /// <summary>B3's constant-velocity intercept solver (<see cref="AimAssist.TryIntercept"/>): a
    /// stationary target dead ahead solves to the plain displacement direction with t =
    /// distance/speed; a crossing target's solved direction and t place the round at exactly the
    /// target's projected position (self-consistency, not an independent re-derivation of the
    /// quadratic); a target receding faster than the round returns no solution rather than a
    /// bogus direction.</summary>
    private static void AimAssistIntercept(TestContext ctx)
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
    private static AimScan MakeScan(float distFactor = 0f, object? self = null) => new()
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
    private static Vector3 ScanPoint(float range, float offAxisDeg)
    {
        float a = Mathf.DegToRad(offAxisDeg);
        return new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a)) * range;
    }

    /// <summary>B4's rejection gates, each proved able to fail: the same candidate that is accepted
    /// on the baseline is rejected when exactly one thing changes. Covers the engine's order —
    /// self, not live, same team (and either side unaffiliated), out of RANGE, outside the cone —
    /// plus the per-target <c>+0x50</c> cone override, which nothing ships but which is ported
    /// deliberately (Decision 2), and the turret pass, which exists and iterates nothing until M4
    /// puts a list in it.</summary>
    private static void AimAssistScanGates(TestContext ctx)
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

    /// <summary>Selection among survivors, on the shipped <c>dist_factor 0.0</c>: a distant
    /// on-axis target outranks a near off-axis one at any range inside RANGE, because the distance
    /// term is deleted outright. The contrast case runs the identical geometry at the executable's
    /// compiled 2.5e-4 default and shows the winner FLIPS — so this is a measurement of the shipped
    /// value, not of the arithmetic being insensitive to it.</summary>
    private static void AimAssistSelection(TestContext ctx)
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

    /// <summary>The rocket snap, on a live pool: a real proximity-fused round in flight is a
    /// candidate, and it outranks the aircraft behind it. The ordnance list is a FILTER over the
    /// rounds in flight, so this is the one B4 case that cannot be proved off-engine — and the gun
    /// round fired alongside is the able-to-fail half: it is in the same pool, alive, and must NOT
    /// be collected, since it carries no proximity fuse.</summary>
    private static void AimAssistOrdnancePriority(TestContext ctx)
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

    /// <summary>B5's launch scatter (<see cref="AimAssist.Scatter"/>): every round lands inside the
    /// cone, the polar angle is UNIFORM IN THE ANGLE rather than over the cone's solid angle (the
    /// reflex port, and what <c>ProjectilePool.ApplySpread</c>'s <c>sqrt(rand)</c> does, which would
    /// pile shots at the rim), and the roll about the aim axis covers the full circle. The
    /// able-to-fail control is the solid-angle sampling itself, computed alongside from the same
    /// draws: it fails the flatness test this one passes.</summary>
    private static void AimAssistScatter(TestContext ctx)
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

    /// <summary>B5's fire-call step order (<c>FUN_004b6530</c>), which is asymmetric on purpose: the
    /// scan updates the slot's TARGET, and what leaves the muzzle is the SMOOTHED direction from
    /// previous frames. Run on a rolled plane basis, so a world/local mix-up cannot pass: the fired
    /// direction must be the smoothed LOCAL vector rotated out to world (inside the scatter cone),
    /// and the stored target must be the scan winner rotated INTO local. Firing this frame's scan
    /// result instead would remove the lag entirely and read as an aimbot.</summary>
    private static void AimAssistFireDirection(TestContext ctx)
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

    /// <summary><see cref="Loadout.ForRig"/> against all 11 player airframes — 4 gun groups
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

    /// <summary>The air-to-air hit chain with its able-to-fail negative case. Two
    /// real flight rigs (built plane, PlaneCollider boxes, AircraftBody, per-part PlaneDamage) in
    /// an otherwise empty world, driven purely on manual sim steps — deterministic, no wall clock.
    /// Pins: a physics ray at the fuselage returns the aircraft body and its struck shape maps to
    /// a Parts entry; a scripted round hits, `MapStruckPart` names the expected part (nose), and
    /// armor moves by the weapon's own ARMOR_DAMAGE while health waits behind it (armor-first);
    /// the decoded kill rule (A4/D14, corrected 2026-08-14): a zeroed nose alone does NOT down
    /// the plane — the old any-critical-part kill is a retired divergence — and exhausting all
    /// four zones' health triggers the real Crash; concentrated fire on ONE bearing also kills
    /// (the resolver redirects hits on the dead zone to survivors and the unabsorbed leftover
    /// drains the whole-vehicle pool — the fix for the 9-rocket sponge), and a Fury falls to a
    /// few wep_06 HE rockets, the measured count noted; a crashed plane soaks
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
                // One round per attempt. A round leaves dead straight (A1 — CANNON_SPREAD is not a
                // dispersion cone), so this should register on the first try; the retry stays as a
                // defensive margin against an unrelated near-miss, and a REGISTERING round must move
                // the pool by exactly one ARMOR_DAMAGE quantum, which is the assertion.
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

            // --- the kill, under the decoded rule (A4/D14): whole-vehicle health exhausted, not
            // one dead critical part. Fire each zone's own bearing until its health empties —
            // nose from ahead, tail from astern, each wing from its own side (a dead part keeps
            // its collider, so its box shields the far side; per-zone budgets are derived from
            // that zone's own pools, tripled for misses).
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

            // An unowned round (NoShooter — nobody's identity) that downs the plane is likewise
            // a death with no killer, never a kill. Attribution scaffolding, not spend
            // mechanics: the other three zones are pre-emptied with EXACT spends (armor
            // stripped, then the bare zone's health) so no leftover reaches the whole pool —
            // an overkill spend would down the plane through the overflow before the burst.
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

            // ---- the proximity fuse arms on aircraft and blast damage reaches them ----
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
            // (the gun kill, the killer-less crash, the unowned kill, the forced crash above).
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

            // --- the D14 correction (2026-08-14, docs/org/vehicleDamage.md): concentrated fire
            // on ONE bearing kills. Once the nose dies the resolver redirects its hits to
            // surviving zones and every unabsorbed leftover drains the whole-vehicle pool, so
            // nose-only fire downs the plane without any other bearing being flown — the
            // at-the-controls sponge (a plane immortal to one-zone fire) is the regression.
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

    /// <summary>The B7 team model: two distinct pilot indices can now share one explicit
    /// <see cref="FlightController.Team"/> — impossible under the old
    /// <see cref="AimAssist.TeamOfPilot"/> stand-in, which derived a team from the pilot index and
    /// so gave every pilot its own. Proves the plumbing end to end (<c>ProjectilePool.CollectAircraft</c>
    /// → <see cref="AimAssist.Scan"/>): a shooter's scan snaps onto a same-index-range aircraft on a
    /// DIFFERENT team and never onto one sharing its own team, real PlayerIndex values included. Then
    /// A2's own corroboration, fired for real through the pool: a round that reaches a teammate still
    /// costs it HP — Decision 3, no damage gate, targeting only.</summary>
    private static void TeamModel(TestContext ctx)
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
            wingman = BuildRig(AiAircraftSpawner.ShooterIdBase, wingPos, team: AimAssist.PlayerTeam);
            enemy = BuildRig(AiAircraftSpawner.ShooterIdBase + 1, enemyPos, team: AimAssist.PlayerTeam + 1);
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

    private static void InstantActionAce(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        // The display-name -> gamez-node table (docs/formats/instant-action.md's IDS_IA_PLANES
        // order): a real entry resolves, a typo/invention does not.
        ctx.Check(InstantAction.PlaneNodeFor("Warhawk") == "player_warhawk",
            $"'Warhawk' resolves to its gamez node: {InstantAction.PlaneNodeFor("Warhawk")}");
        ctx.Check(InstantAction.PlaneNodeFor("Warhawk Mk2") == null,
            $"an unrecognised display name resolves to null, not a guess");

        // RepresentativeRating: every shipped chapter's ace_stats is a uniform 9 (spawns.md), but
        // a hand-authored --ia= file could differ — eight 9s and one 7 must average (and round)
        // to 9, not silently pick one arbitrary slot.
        var mixedStats = new AiSkillVector
        {
            DareDevil = 9,
            NaturalTouch = 9,
            SixthSense = 9,
            DeadEye = 7,
            QuickDraw = 9,
            SteadyHand = 9,
            StunRecovery = 9,
            Talker = 9,
            Constitution = 9,
        };
        int rating = InstantActionRuntime.RepresentativeRating(mixedStats);
        ctx.Check(rating == 9, $"eight 9s and one 7 average (rounded) to 9: rating={rating}");
        ctx.Check(InstantActionRuntime.RepresentativeRating(default) == 5,
            $"every slot unset falls back to 5, the same default --ai-attack= takes");

        // ChooseAceSpawn: a draw landing on the player's own index substitutes the LITERAL last
        // index (never a re-roll); a non-colliding draw is used as-is.
        var spawns = new List<SpawnPoint>
        {
            new(new Vector3(0f, 500f, 0f), 0f),
            new(new Vector3(100f, 500f, 0f), 0f),
            new(new Vector3(200f, 500f, 0f), 90f),
        };
        var (collided, _) = InstantActionRuntime.ChooseAceSpawn(spawns, playerSpawnIndex: 0, draw: 0);
        ctx.Check(collided == spawns.Count - 1,
            $"a draw colliding with the player's index substitutes the literal last index: idx={collided}");
        var (clean, _) = InstantActionRuntime.ChooseAceSpawn(spawns, playerSpawnIndex: 1, draw: 0);
        ctx.Check(clean == 0, $"a non-colliding draw is used as-is: idx={clean}");

        // D9: the wingman standing-order table (docs/formats/instant-action.md "The player and
        // the wingmen") is pure over its 0-based index — fan placement, the escort chain (0/1/3
        // escort the player; 2/4 escort wingmen 1/3), and the authored accent ids.
        var slot0 = InstantActionRuntime.WingmanSlotFor(0);
        ctx.Check(slot0 is { MetresOut: 100f, OffsetDeg: -45f, PrimaryTargetIsWingman: null, AccentId: 12 },
            $"wingman 0: 100 m / -45°, escorts the player, accent 12: {slot0}");
        var slot1 = InstantActionRuntime.WingmanSlotFor(1);
        ctx.Check(slot1 is { MetresOut: 100f, OffsetDeg: 45f, PrimaryTargetIsWingman: null, AccentId: 14 },
            $"wingman 1: 100 m / +45°, escorts the player, accent 14: {slot1}");
        var slot2 = InstantActionRuntime.WingmanSlotFor(2);
        ctx.Check(slot2 is { MetresOut: 200f, OffsetDeg: 45f, PrimaryTargetIsWingman: 1, AccentId: 15 },
            $"wingman 2: 200 m / +45°, escorts wingman 1, accent 15: {slot2}");
        var slot3 = InstantActionRuntime.WingmanSlotFor(3);
        ctx.Check(slot3 is { MetresOut: 200f, OffsetDeg: -45f, PrimaryTargetIsWingman: null, AccentId: 13 },
            $"wingman 3: 200 m / -45°, escorts the player, accent 13: {slot3}");
        var slot4 = InstantActionRuntime.WingmanSlotFor(4);
        ctx.Check(slot4 is { MetresOut: 300f, OffsetDeg: -45f, PrimaryTargetIsWingman: 3, AccentId: 16 },
            $"wingman 4: 300 m / -45°, escorts wingman 3, accent 16: {slot4}");

        // Decision 8a's flight-size clamp: 1-4 humans against 5 configured wingmen expects
        // 5/4/3/2, and a below-cap case (2 humans, 2 configured) proves the configured count
        // stands untouched. 0 configured always flies none.
        ctx.Check(InstantActionRuntime.FlownWingmen(5, humans: 1) == 5, $"1 human, 5 configured: flies all 5");
        ctx.Check(InstantActionRuntime.FlownWingmen(5, humans: 2) == 4, $"2 humans, 5 configured: clamped to 4");
        ctx.Check(InstantActionRuntime.FlownWingmen(5, humans: 3) == 3, $"3 humans, 5 configured: clamped to 3");
        ctx.Check(InstantActionRuntime.FlownWingmen(5, humans: 4) == 2, $"4 humans, 5 configured: clamped to 2");
        ctx.Check(InstantActionRuntime.FlownWingmen(2, humans: 2) == 2, $"below the cap: 2 humans/2 configured stays 2");
        ctx.Check(InstantActionRuntime.FlownWingmen(0, humans: 1) == 0, $"0 configured flies none");

        // The actual spawn integration: AiAircraftSpawner.Spawn given an authored scheme/team
        // (the C8 extension) wears them as-is — the ace lands on team 2 flying the configured
        // airframe, not a pilot-index-derived team.
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null);
        var stockLoadouts = StockLoadouts.Load();
        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? ace = null;
        var wingmen = new List<FlightController>();
        var waveMembers = new List<FlightController>();
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            var spec = SessionSpec.Parse(System.Array.Empty<string>());
            var liveries = new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof"));
            var inputs = new FlightRigAssembler.Inputs
            {
                PlanesGamez = planesGamez,
                StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
                RigCount = 0,
                PaintRng = new RandomNumberGenerator(),
                ZrdrPath = ctx.ZrdrPath,
                StockLoadouts = stockLoadouts,
                WeaponDefs = weaponDefs,
                Textures = textures,
                Projectiles = live,
                Shakes = ShakeDefs.Load(ctx.ZrdrPath),
            };
            // worldEffects null!: never dereferenced — Inputs.CrashProgram/WorldScene stay null,
            // so Spawn's crash-runtime block (the only reader) is skipped.
            var spawner = new AiAircraftSpawner(spec, liveries, null!, ctx.Host, inputs);

            string aceNode = InstantAction.PlaneNodeFor("Warhawk")!;
            var aceLivery = new PaintScheme { Pattern = "cccp", Color1 = PaintScheme.FromBytes(200, 10, 10) };
            var pos = new Vector3(0f, 500f, 0f);
            var pilot = AiPilot.HoldingCourse(pos, pos + Vector3.Forward);
            ace = spawner.Spawn(aceNode, pos, pos + Vector3.Forward, pilot,
                scheme: aceLivery, team: InstantActionRuntime.EnemyTeam);

            ctx.Check(ace.Team == InstantActionRuntime.EnemyTeam,
                $"the spawned ace carries the authored team, not a pilot-index default: team={ace.Team}");
            ctx.Check(ace.Name.ToString().Contains(aceNode),
                $"the ace flies its configured airframe: {ace.Name}");

            // D9's wingman census: N aircraft on team 1 (Decision 8: humans + wingmen share the
            // player's side), flying the configured airframe. Spawned through the same
            // AiAircraftSpawner.Spawn seam as the ace above, on AimAssist.PlayerTeam instead of
            // InstantActionRuntime.EnemyTeam.
            string wingmanNode = InstantAction.PlaneNodeFor("Fury")!;
            for (int i = 0; i < 3; i++)
            {
                var wPos = new Vector3(500f + i * 10f, 500f, 0f);
                var wPilot = AiPilot.HoldingCourse(wPos, wPos + Vector3.Forward);
                wingmen.Add(spawner.Spawn(wingmanNode, wPos, wPos + Vector3.Forward, wPilot,
                    scheme: null, team: AimAssist.PlayerTeam));
            }
            ctx.Check(wingmen.Count == 3, $"3 wingmen spawned: {wingmen.Count}");
            ctx.Check(wingmen.All(w => w.Team == AimAssist.PlayerTeam),
                $"every wingman carries team 1, not a pilot-index default: teams={string.Join(",", wingmen.Select(w => w.Team))}");
            ctx.Check(wingmen.All(w => w.Name.ToString().Contains(wingmanNode)),
                $"every wingman flies the configured airframe: {string.Join(",", wingmen.Select(w => w.Name))}");

            // E11: RandomPilotStats/ResolveWaveAccentId are pure over their draw — row 4 is the
            // flat-4 personality, and only accent 12 (the wingman range's own base) re-rolls.
            var flatRow = InstantActionRuntime.RandomPilotStats(draw: 4);
            ctx.Check(flatRow is { DareDevil: 4, Constitution: 4 },
                $"draw 4 selects row 4, the flat personality: {flatRow}");
            ctx.Check(InstantActionRuntime.ResolveWaveAccentId(12, draw: 3) == 15,
                $"accent 12 re-rolls to 12 + draw%5: {InstantActionRuntime.ResolveWaveAccentId(12, 3)}");
            ctx.Check(InstantActionRuntime.ResolveWaveAccentId(7, draw: 99) == 7,
                $"any other accent id passes through unchanged: {InstantActionRuntime.ResolveWaveAccentId(7, 99)}");

            // E11: a real InstantActionWaves sequence over real spawned aircraft — wave 1 (2
            // members, live) killed down to 0 triggers wave 2 (1 member, built inert) activating
            // at a spawn point at least 500 m from the human, fanned off it.
            var iaWaves = new InstantActionWaves(new[] { 2, 1, 0, 0 });
            int firstWave = iaWaves.Start();
            ctx.Check(firstWave == 1, $"wave 1 is current at mission start: {firstWave}");

            string waveNode = InstantAction.PlaneNodeFor("Brigand")!;
            var wave1Pos = new Vector3(0f, 500f, 0f);
            for (int i = 0; i < 2; i++)
            {
                var wmPos = wave1Pos + new Vector3(i * 10f, 0f, 0f);
                var wmPilot = AiPilot.HoldingCourse(wmPos, wmPos + Vector3.Forward);
                waveMembers.Add(spawner.Spawn(waveNode, wmPos, wmPos + Vector3.Forward, wmPilot,
                    scheme: null, team: InstantActionRuntime.EnemyTeam));
            }
            var wave2Pilot = AiPilot.HoldingCourse(Vector3.Zero, Vector3.Forward);
            var wave2Member = spawner.Spawn(waveNode, Vector3.Zero, Vector3.Forward, wave2Pilot,
                scheme: null, team: InstantActionRuntime.EnemyTeam, inert: true);
            waveMembers.Add(wave2Member);

            int aliveWave1 = waveMembers.Take(2).Count(m => m.InPlay);
            ctx.Check(aliveWave1 == 2, $"both wave-1 members InPlay before any kill: {aliveWave1}");
            ctx.Check(!wave2Member.InPlay, $"wave 2's member is inert (not InPlay) before activation");
            ctx.Check(iaWaves.Step(aliveInCurrentWave: aliveWave1) == 0,
                $"wave 1 still alive: no advance");

            foreach (var m in waveMembers.Take(2))
                m.DebugForceCrash();
            int aliveAfterKills = waveMembers.Take(2).Count(m => m.InPlay);
            ctx.Check(aliveAfterKills == 0, $"both wave-1 members crashed: {aliveAfterKills}");
            int nextWave = iaWaves.Step(aliveInCurrentWave: aliveAfterKills);
            ctx.Check(nextWave == 2, $"wave 1's last kill advances to wave 2: {nextWave}");
            ctx.Check(iaWaves.CurrentWave == 2 && !iaWaves.Finished,
                $"the sequencer's own state agrees: {iaWaves.CurrentWave}");

            var humanPos = new Vector3(0f, 500f, 0f);
            var waveSpawns = new List<SpawnPoint>
            {
                new(new Vector3(10f, 500f, 0f), 0f),      // 10 m from the human — too close
                new(new Vector3(600f, 500f, 0f), 0f),     // 600 m — eligible
            };
            var (spIdx, sp) = InstantActionWaves.ChooseWaveSpawn(
                waveSpawns, new[] { humanPos }, draw: 0);
            ctx.Check(spIdx == 1, $"the near point is excluded, the far one drawn: idx={spIdx}");
            var fwd2 = new Basis(Vector3.Up, Mathf.DegToRad(sp.HeadingDeg)) * Vector3.Forward;
            wave2Member.Activate(sp.Position, sp.Position + fwd2);
            ctx.Check(wave2Member.InPlay, $"wave 2's member is InPlay once activated");
            float distSq = wave2Member.WorldPosition.DistanceSquaredTo(humanPos);
            ctx.Check(distSq >= InstantActionWaves.MinSpawnDistanceSquared,
                $"activated at least 500 m from the human: dist={Mathf.Sqrt(distSq):0} m");
        }
        finally
        {
            pool?.Free();
            ace?.Free();
            foreach (var w in wingmen)
                w.Free();
            foreach (var m in waveMembers)
                m.Free();
            textures.Dispose();
        }
    }

    /// <summary>The F12 zeppelin run: the objective-zeppelin selection, the builder's own switch,
    /// and the wave arm that replaces E11's teleport. Everything runs over C1/IA1's real
    /// <c>ia.zrd.json</c> / <c>egen.zrd.json</c> / <c>zeppelins.zrd.json</c>, on the same host +
    /// <c>cargobay</c> stand-in world the <c>zeppelin-launch</c> suite uses, so the drop geometry
    /// under test is the one <c>AiGeneratorRuntime</c> already owns.</summary>
    private static void InstantActionZeppelin(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, "C1");
        ctx.RequireData(chapterZrdr, $"C1 zrdr");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C1", "IA1");
        ctx.RequireData(missionZrdr, $"C1/IA1 zrdr");

        // The objective selection, over the shipped file: C1 authors zeppelin_type cargo and
        // names multiplayer1zep for all three types.
        var iaDef = InstantAction.Load(missionZrdr);
        ctx.Check(iaDef.ZeppelinType == "cargo",
            $"C1/IA1 authors zeppelin_type: {iaDef.ZeppelinType ?? "(unauthored)"}");
        ctx.Check(InstantActionRuntime.SelectedZeppelinNode(iaDef) == "multiplayer1zep",
            $"cargo selects the cargo_zeppelin node: {InstantActionRuntime.SelectedZeppelinNode(iaDef)}");
        ctx.Same(3, InstantActionRuntime.ZeppelinNodes(iaDef).Count,
            $"the three *_zeppelin names are offered in type order");
        ctx.Check(InstantActionRuntime.ZeppelinTypeIndex("passenger") == 1
            && InstantActionRuntime.ZeppelinTypeIndex("military") == 2,
            $"passenger/military index 1/2");
        // ⚠ The fallback is cargo, not an error: the parser REJECTS an unrecognised string over a
        // record whose reset wrote 0 (FUN_00458ff0's param_1[0x95] = 0).
        ctx.Check(InstantActionRuntime.ZeppelinTypeIndex(null) == 0
            && InstantActionRuntime.ZeppelinTypeIndex("blimp") == 0,
            $"an unauthored or unrecognised zeppelin_type falls back to cargo (index 0)");

        var egen = EnemyGenerators.Load(missionZrdr);
        ctx.Check(egen.Count == 1 && egen[0].Node == "multiplayer1zep" && egen[0].IsZeppelin,
            $"the objective zeppelin is the host of C1/IA1's one generator count={egen.Count}");
        if (egen.Count != 1)
            return;
        var genDef = egen[0];
        var nets = AiNets.Load(chapterZrdr);
        var zepDefs = Zeppelins.Load(missionZrdr);
        ctx.Check(zepDefs.Count == 1 && zepDefs[0].Node == "multiplayer1zep",
            $"…and of its one zeppelin record count={zepDefs.Count}");
        if (zepDefs.Count != 1)
            return;

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null);
        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        Node3D? host = null;
        Node3D? heldHost = null;
        AiGeneratorRuntime? gens = null;
        ZeppelinRuntime? zeps = null;
        var wave1 = new List<FlightController>();
        var wave2 = new List<FlightController>();
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);
            var spec = SessionSpec.Parse(System.Array.Empty<string>());
            var liveries = new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof"));
            var inputs = new FlightRigAssembler.Inputs
            {
                PlanesGamez = planesGamez,
                StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
                RigCount = 0,
                PaintRng = new RandomNumberGenerator(),
                ZrdrPath = ctx.ZrdrPath,
                StockLoadouts = StockLoadouts.Load(),
                WeaponDefs = weaponDefs,
                Textures = textures,
                Projectiles = live,
                Shakes = ShakeDefs.Load(ctx.ZrdrPath),
            };
            var spawner = new AiAircraftSpawner(spec, liveries, null!, ctx.Host, inputs);

            // Both waves built INERT at the origin, which is what the original does on this one
            // mode for wave 1 as well ("even wave 1 is built deactivated at the origin").
            string waveNode = InstantAction.PlaneNodeFor("Firebrand")!;
            List<FlightController> BuildWave(int count)
            {
                var built = new List<FlightController>(count);
                for (int i = 0; i < count; i++)
                {
                    var pilot = AiPilot.HoldingCourse(Vector3.Zero, Vector3.Forward);
                    built.Add(spawner.Spawn(waveNode, Vector3.Zero, Vector3.Forward, pilot,
                        scheme: null, team: InstantActionRuntime.EnemyTeam, inert: true));
                }
                return built;
            }

            wave1.AddRange(BuildWave(6));
            wave2.AddRange(BuildWave(3));
            ctx.Check(wave1.All(m => m.Inert) && wave2.All(m => m.Inert),
                $"both waves are parked inert before the mission starts");

            // The stand-in world: the host above its 100 m launch gate, with the authored
            // cargobay drop node under the hull.
            host = new Node3D { Name = "multiplayer1zep", Position = new Vector3(0f, 500f, 0f) };
            var cargobay = new Node3D { Name = "cargobay", Position = new Vector3(0f, -20f, 0f) };
            host.AddChild(cargobay);
            ctx.Host.AddChild(host);
            var resolvedHost = host;
            var resolvedBay = cargobay;

            int freshSpawns = 0;
            gens = new AiGeneratorRuntime(new[] { genDef },
                (name, scope) => name.Equals("multiplayer1zep", System.StringComparison.OrdinalIgnoreCase)
                    ? resolvedHost
                    : name.Equals("cargobay", System.StringComparison.OrdinalIgnoreCase) ? resolvedBay : null,
                nets, ctx.PlaneName,
                (_, _, _, _) => { freshSpawns++; return null; },
                (_, _) => 1, (_, _) => { });
            ctx.Same(1, gens.LiveCount, $"the generator is live");

            int launchWave = 0;
            var releasedAt = new List<Vector3>();
            FlightController? Release(Vector3 pos, Vector3 lookAt)
            {
                var roster = launchWave == 1 ? wave1 : launchWave == 2 ? wave2 : null;
                foreach (var member in roster ?? new List<FlightController>())
                {
                    if (!member.Inert)
                        continue;
                    member.Activate(pos, lookAt);
                    releasedAt.Add(pos);
                    return member;
                }
                return null;
            }

            ctx.Same(1, gens.UseInstantActionLaunches("multiplayer1zep", Release),
                $"the objective zeppelin's generator takes the Instant Action launch arm");

            // Uncredited: the decoded capacity rule is in force (the capacity-0 stand-in is OFF
            // on this arm), so a full minute of sim above the altitude gate launches nothing.
            const float dt = 1f / 60f;
            for (int i = 0; i < 60 * 60; i++)
                gens.SimStep(dt);
            ctx.Check(releasedAt.Count == 0 && wave1.All(m => m.Inert),
                $"an uncredited generator launches nothing released={releasedAt.Count}");

            // The sequencer's own trigger: a wave whose members are all still in the bay counts
            // as present (the decoded +0x945 rule), so it must NOT read as cleared.
            var waves = new InstantActionWaves(new[] { 6, 3, 0, 0 });
            ctx.Same(1, waves.Start(), $"wave 1 is current at mission start");
            int parked = wave1.Count(m => !m.Crashed);
            ctx.Check(parked == 6 && wave1.Count(m => m.InPlay) == 0,
                $"…with all 6 members parked and none InPlay parked={parked}");
            ctx.Same(0, waves.Step(parked),
                $"a wave still waiting in the bay does not advance the sequencer");

            // The credit: one wave's member count, then the generator's own 7 s composed
            // schedule releases exactly that many and stops.
            launchWave = 1;
            ctx.Same(1, gens.GrantWaveCapacity("multiplayer1zep", wave1.Count),
                $"wave 1's member count is credited to the generator");
            for (int i = 0; i < 60 * 120 && releasedAt.Count < 6; i++)
                gens.SimStep(dt);
            ctx.Same(6, releasedAt.Count, $"exactly wave 1's six members are released");
            ctx.Check(wave1.All(m => m.InPlay), $"…and all six are in play");
            ctx.Check(wave2.All(m => m.Inert),
                $"wave 2 is untouched — one wave's credit releases one wave");
            var expectedDrop = cargobay.GlobalPosition + Vector3.Down * 12f;
            ctx.Check(releasedAt.All(p => p.DistanceTo(expectedDrop) < 0.1f),
                $"…at the generator's own drop point, 12 m under the bay floor drop={releasedAt[0]} bay={cargobay.GlobalPosition}");
            ctx.Same(0, freshSpawns,
                $"the generator never spawned an aircraft of its own on this arm");

            for (int i = 0; i < 60 * 60; i++)
                gens.SimStep(dt);
            ctx.Same(6, releasedAt.Count, $"the credit is spent: a further minute releases nothing");

            // The last kill advances, exactly as on the teleport arm.
            foreach (var m in wave1)
                m.DebugForceCrash();
            ctx.Same(2, waves.Step(wave1.Count(m => !m.Crashed)),
                $"wave 1's last kill advances to wave 2");

            // The builder's other arm: a zeppelin it switches off is HELD — still placed at its
            // authored pose, but no longer flown (a merely hidden one would keep flying its net
            // and firing its broadside).
            heldHost = new Node3D { Name = "multiplayer1zep" };
            ctx.Host.AddChild(heldHost);
            var resolvedHeld = heldHost;
            zeps = new ZeppelinRuntime(zepDefs,
                name => name.Equals("multiplayer1zep", System.StringComparison.OrdinalIgnoreCase)
                    ? resolvedHeld : null, nets);
            ctx.Same(1, zeps.LiveCount, $"the zeppelin record is placed");
            ctx.Check(zeps.Hold("multiplayer1zep"), $"…and the builder can hold it");
            ctx.Check(!zeps.Hold("nosuchzep"), $"holding an unknown node reports it, never throws");
            var placedAt = heldHost.GlobalPosition;
            for (int i = 0; i < 60 * 10; i++)
                zeps.SimStep(dt);
            ctx.Check(heldHost.GlobalPosition.DistanceTo(placedAt) < 0.01f,
                $"a held zeppelin stays at its authored pose through 10 s of sim moved={heldHost.GlobalPosition.DistanceTo(placedAt):0.###} m");
        }
        finally
        {
            gens?.Free();
            zeps?.Free();
            host?.Free();
            heldHost?.Free();
            pool?.Free();
            foreach (var m in wave1)
                m.Free();
            foreach (var m in wave2)
                m.Free();
            textures.Dispose();
        }

        // The half only a built world can answer: C1/IA1's own mission script switches
        // multiplayer1zep off (support\c1\ia1.gw, 'NodeSetActive off'), so the objective of a
        // zeppelin run starts hidden AND non-collidable — colliders derive from visibility
        // (Mech3/WorldCollision). The builder's decoded activation has to put BOTH back or the
        // objective cannot be shot at all, and that crossing is what is measured here.
        //
        // ⚠ Read-only against the shared world, and it must stay that way: the run's own
        // chapter+mission world is CACHED across suites (TestHarness.WithWorld), so registering a
        // pool or leaving a node switched on here would be handed to every later C1 suite —
        // measured, as an inflated destructible-census. Hence no WireDamage (the pool seeding and
        // the round path are the zeppelin-damage suite's, on a zeppelin its mission leaves live)
        // and the visibility is put back before returning.
        ctx.WithWorld("C1", collision: true, mission: "IA1", world =>
        {
            var runtime = world.Session.Runtime;
            var objective = runtime.FindNodes("multiplayer1zep").FirstOrDefault();
            ctx.Check(objective != null, $"the objective zeppelin's node is in C1/IA1's world");
            if (objective == null)
                return;
            ctx.Check(!objective.IsVisibleInTree(),
                $"C1/IA1's mission script has switched it off at world load (the state F12 inherits)");

            var bagNode = runtime.FindNodes("gasbag1", objective).FirstOrDefault();
            ctx.Check(bagNode != null, $"gasbag1 resolves under the objective's subtree");
            if (bagNode == null)
                return;
            var (shapesTotal, shapesOff) = ShapeStates(bagNode);
            ctx.Check(shapesTotal > 0 && shapesOff == shapesTotal,
                $"…with all {shapesTotal} of its collision shapes switched off with it off={shapesOff}");
            try
            {
                // The decoded builder step: gwNodeSetActive(node, TRUE) on the selected zeppelin.
                objective.Visible = true;
                var (_, stillOff) = ShapeStates(bagNode);
                // ⚠ Not "all 36 back on": the ones that stay off are descendants that are
                // themselves deactivated (the hidden `destroyed` variants), which is exactly why
                // WorldCollision DERIVES the flag instead of walking a subtree to re-enable it.
                // The measurement is the crossing, not a full count.
                ctx.Check(objective.IsVisibleInTree() && stillOff < shapesTotal,
                    $"the Instant Action activation puts BOTH back: visible again ({objective.IsVisibleInTree()}), {shapesTotal - stillOff} of {shapesTotal} shapes re-enabled (the {stillOff} left off are the hidden destroyed variants)");
            }
            finally
            {
                objective.Visible = false;
                var (_, offAgain) = ShapeStates(bagNode);
                ctx.Check(offAgain == shapesTotal,
                    $"…and the world is left exactly as this suite found it off={offAgain} of {shapesTotal}");
            }
        });
    }

    /// <summary>How many collision shapes hang anywhere under this node, and how many of those are
    /// switched off — the state <c>Mech3/WorldCollision</c> derives from its owner's visibility.
    /// Recursive, because a world node's shapes hang off its MESH children rather than off the
    /// named node itself; a non-recursive count reads 0 of 0 and passes an "all disabled" test
    /// vacuously.</summary>
    private static (int Total, int Disabled) ShapeStates(Node node)
    {
        int total = 0, disabled = 0;
        if (node is CollisionShape3D collision)
        {
            total++;
            if (collision.Disabled)
                disabled++;
        }
        foreach (var child in node.GetChildren())
        {
            var (t, d) = ShapeStates(child);
            total += t;
            disabled += d;
        }
        return (total, disabled);
    }

    /// <summary>The G13 mission end: one assertion per mission type, each driven to its end
    /// through the SAME signal <c>GameSession</c> subscribes to — the ace's own <c>Downed</c>
    /// report, <c>InstantActionWaves.Finished</c> over real spawned aircraft, two real
    /// <c>StuntMission</c> runs over C1/IA1's authored danger zones through
    /// <c>InstantActionRuntime.ZoneSetsFlown</c> (the predicate the session itself calls), and
    /// <c>ZeppelinRuntime</c>'s two zeppelin signals raised by really damaging C1/M04's piratezep
    /// — <c>ZeppelinEnginesDisabled</c> from shooting out all of its engines, which is the mode's
    /// actual objective, and <c>ZeppelinKilled</c> from the gasbag threshold, which also wins it —
    /// plus the lives ledger's two ends on a real <see cref="FlightController"/>: with a life
    /// left the armed crash cam ends in a respawn, out of lives it never does.
    ///
    /// <para>Every win check carries its own able-to-fail control, and they are the cheap kind:
    /// a SECOND runtime of a different mission type subscribed to the same signal must stay
    /// Running, which is the one mistake this design could make (reporting an objective the
    /// mission does not run on).</para></summary>
    private static void InstantActionEnd(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C1", "IA1");
        ctx.RequireData(missionZrdr, $"C1/IA1 zrdr");
        ctx.RequireData(ctx.MessagesPath, $"messages.json");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null);
        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        var spawned = new List<FlightController>();
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);
            var spec = SessionSpec.Parse(System.Array.Empty<string>());
            var liveries = new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof"));
            var inputs = new FlightRigAssembler.Inputs
            {
                PlanesGamez = planesGamez,
                StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
                RigCount = 0,
                PaintRng = new RandomNumberGenerator(),
                ZrdrPath = ctx.ZrdrPath,
                StockLoadouts = StockLoadouts.Load(),
                WeaponDefs = weaponDefs,
                Textures = textures,
                Projectiles = live,
                Shakes = ShakeDefs.Load(ctx.ZrdrPath),
            };
            var spawner = new AiAircraftSpawner(spec, liveries, null!, ctx.Host, inputs);
            string enemyNode = InstantAction.PlaneNodeFor("Warhawk")!;

            FlightController SpawnAt(Vector3 pos, int team, bool inert = false)
            {
                var pilot = AiPilot.HoldingCourse(pos, pos + Vector3.Forward);
                var fc = spawner.Spawn(enemyNode, pos, pos + Vector3.Forward, pilot,
                    scheme: null, team: team, inert: inert);
                spawned.Add(fc);
                return fc;
            }

            // ---- dogfight_ace: the ace's own Downed report ----------------------------------
            var aceMission = new InstantActionRuntime(EndDef(ctx, "ace", "dogfight_ace"));
            var notAceMission = new InstantActionRuntime(EndDef(ctx, "squadron", "dogfight_squadron"));
            var ace = SpawnAt(new Vector3(0f, 500f, 0f), InstantActionRuntime.EnemyTeam);
            ace.Downed += (_, _) =>
            {
                aceMission.ReportObjective(InstantActionObjective.AceDown);
                notAceMission.ReportObjective(InstantActionObjective.AceDown);
            };
            ctx.Check(!aceMission.Ended, $"the ace duel is running before the ace goes down");
            ace.DebugForceCrash();
            ctx.Check(aceMission.Outcome == InstantActionOutcome.Won,
                $"the ace's own Downed report WINS a dogfight_ace mission: {aceMission.Outcome}");
            ctx.Check(!notAceMission.Ended,
                $"…and the same report leaves a dogfight_squadron mission running: {notAceMission.Outcome}");

            // ---- dogfight_squadron: the sequencer's exhausted counter ------------------------
            var squadron = new InstantActionRuntime(EndDef(ctx, "squadron2", "dogfight_squadron"));
            var notSquadron = new InstantActionRuntime(EndDef(ctx, "zeppelin0", "zeppelin_run"));
            var waves = new InstantActionWaves(new[] { 2, 1, 0, 0 });
            var wave1 = new List<FlightController>
            {
                SpawnAt(new Vector3(200f, 500f, 0f), InstantActionRuntime.EnemyTeam),
                SpawnAt(new Vector3(220f, 500f, 0f), InstantActionRuntime.EnemyTeam),
            };
            var wave2 = new List<FlightController>
            {
                SpawnAt(new Vector3(240f, 500f, 0f), InstantActionRuntime.EnemyTeam, inert: true),
            };
            ctx.Same(1, waves.Start(), $"wave 1 is current at mission start");

            // GameSession.StepInstantAction's own loop body, over the real rosters.
            void StepWaves(List<FlightController> roster)
            {
                int next = waves.Step(roster.Count(m => m.InPlay));
                if (next == 2)
                {
                    wave2[0].Activate(new Vector3(2000f, 500f, 0f), new Vector3(2000f, 500f, 100f));
                }
                else if (next == 0 && waves.Finished)
                {
                    squadron.ReportObjective(InstantActionObjective.WavesCleared);
                    notSquadron.ReportObjective(InstantActionObjective.WavesCleared);
                }
            }

            foreach (var m in wave1)
            {
                m.DebugForceCrash();
            }
            StepWaves(wave1);
            ctx.Check(waves.CurrentWave == 2 && wave2[0].InPlay,
                $"wave 1 cleared: wave 2 is current and flying — the mission is NOT over yet");
            ctx.Check(!squadron.Ended, $"…and the squadron mission is still running with a wave left");
            wave2[0].DebugForceCrash();
            StepWaves(wave2);
            ctx.Check(waves.Finished && squadron.Outcome == InstantActionOutcome.Won,
                $"the last wave's last kill WINS a dogfight_squadron mission: {squadron.Outcome}");
            ctx.Check(!notSquadron.Ended,
                $"…and a zeppelin run clearing its waves the same way is NOT won: {notSquadron.Outcome}");

            // ---- lives: the two ends of the ledger, on a real aircraft -----------------------
            // The mechanism is FlightController's own crash/respawn path, which an AI-piloted
            // aircraft takes byte-for-byte (M4 A2), so the probe is a spawned plane rather than a
            // rig: what is under test is the arming and the Spectating pin, not who is at the
            // controls.
            var lifeLedger = new InstantActionRuntime(EndDef(ctx, "lives3", "dogfight_squadron", lives: 3));
            var probe = SpawnAt(new Vector3(-400f, 500f, 0f), AimAssist.PlayerTeam);
            lifeLedger.RegisterPilot(probe.PlayerIndex);
            probe.AutoRespawnAfter = 3f;
            probe.DebugForceCrash();
            ctx.Check(probe.Crashed && lifeLedger.NotifyPilotDown(probe.PlayerIndex),
                $"3 lives, first death: the ledger says fly again ({lifeLedger.LivesLeft(probe.PlayerIndex)} left)");
            for (int i = 0; i < 300; i++)
            {
                probe.SimStep(1f / 60f);
            }
            ctx.Check(!probe.Crashed,
                $"…and 5 s later the armed 3 s crash cam has respawned it — the able-to-fail control");

            var lastLife = new InstantActionRuntime(EndDef(ctx, "lives1", "dogfight_squadron"));
            lastLife.RegisterPilot(probe.PlayerIndex);
            probe.DebugForceCrash();
            bool fliesAgain = lastLife.NotifyPilotDown(probe.PlayerIndex);
            probe.Spectating = !fliesAgain;
            ctx.Check(!fliesAgain && lastLife.IsSpectating(probe.PlayerIndex),
                $"the default 1 life sends the same pilot straight to spectate on its first death");
            ctx.Check(lastLife.Outcome == InstantActionOutcome.Lost,
                $"…and with no other human alive the mission is LOST: {lastLife.Outcome}");
            for (int i = 0; i < 600; i++)
            {
                probe.SimStep(1f / 60f);
            }
            ctx.Check(probe.Crashed,
                $"…and 10 s later the wreck is still there: Spectating outranks the armed timer");
        }
        finally
        {
            pool?.Free();
            foreach (var fc in spawned)
            {
                fc.Free();
            }
            textures.Dispose();
        }

        // ---- stunt_flying: StuntRace's all-finished path over the authored zones -------------
        ctx.WithWorld("C1", collision: false, mission: "IA1", world =>
        {
            var zones = StuntMission.Load(world.Gamez, missionZrdr, Messages.Load(ctx.MessagesPath));
            ctx.Check(zones != null, $"C1/IA1 ships danger zones for a stunt_flying mission");
            if (zones == null)
            {
                return;
            }
            var stunt = new InstantActionRuntime(EndDef(ctx, "stunt", "stunt_flying"));
            var notStunt = new InstantActionRuntime(EndDef(ctx, "ace2", "dogfight_ace"));
            var race = new StuntRace();
            var second = zones.ForAnotherPlayer();
            race.Add(0, zones, "P1");
            race.Add(1, second, "P2");
            // GameSession.CheckInstantActionZoneSets, over the same predicate it calls: the two
            // pilots' runs, with P2's out-of-lives state under the suite's control.
            bool p2OutOfLives = false;
            void CheckZoneSets()
            {
                var pilots = new[] { (false, zones.AllComplete), (p2OutOfLives, second.AllComplete) };
                if (InstantActionRuntime.ZoneSetsFlown(pilots))
                {
                    stunt.ReportObjective(InstantActionObjective.ZonesFlown);
                    notStunt.ReportObjective(InstantActionObjective.ZonesFlown);
                }
            }

            zones.RunCompleted += CheckZoneSets;
            second.RunCompleted += CheckZoneSets;

            // Decision 10: all-finished, never first past the post.
            zones.DebugCompleteAll();
            ctx.Check(zones.AllComplete && !race.AllFinished && !stunt.Ended,
                $"the FIRST pilot's zone set is flown and the mission runs on ({race.FinishedCount} of 2 in)");
            second.DebugCompleteAll();
            ctx.Check(race.AllFinished && stunt.Outcome == InstantActionOutcome.Won,
                $"the last pilot in WINS a stunt_flying mission: {stunt.Outcome}");
            ctx.Check(!notStunt.Ended,
                $"…and the same report leaves a dogfight_ace mission running: {notStunt.Outcome}");

            // The same field with P2 out of lives instead: a pilot who can never clear another
            // gate must not hold the mission open, which is what StuntRace's own all-finished
            // rule alone would do (AllFinished is still false here).
            var outOfLives = new InstantActionRuntime(EndDef(ctx, "stunt-out", "stunt_flying"));
            var rerun = zones.ForAnotherPlayer();
            var stranded = zones.ForAnotherPlayer();
            rerun.RunCompleted += () =>
            {
                if (InstantActionRuntime.ZoneSetsFlown(new[] { (false, rerun.AllComplete), (true, stranded.AllComplete) }))
                {
                    outOfLives.ReportObjective(InstantActionObjective.ZonesFlown);
                }
            };
            rerun.DebugCompleteAll();
            ctx.Check(!stranded.AllComplete && outOfLives.Outcome == InstantActionOutcome.Won,
                $"the last FLYING pilot's zone set wins it with the other out of lives: {outOfLives.Outcome}");
        });

        // ---- zeppelin_run: the objective's real death ----------------------------------------
        string m04Zrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C1", "M04");
        ctx.RequireData(m04Zrdr, $"C1/M04 zrdr");
        var zepDefs = Zeppelins.Load(m04Zrdr);
        ctx.Check(zepDefs.Count == 1 && zepDefs[0].Node == "piratezep",
            $"C1/M04 authors the one zeppelin this mission is built around count={zepDefs.Count}");
        if (zepDefs.Count != 1)
        {
            return;
        }
        var zepNets = AiNets.Load(SessionPaths.ChapterZrdr(ctx.DataRoot, "C1"));
        ctx.WithWorld("C1", collision: true, mission: "M04", world =>
        {
            var runtime = world.Session.Runtime;
            var host = runtime.FindNodes("piratezep").FirstOrDefault();
            ctx.Check(host != null, $"the piratezep world node resolves");
            if (host == null)
            {
                return;
            }
            ZeppelinRuntime? zeps = null;
            try
            {
                zeps = new ZeppelinRuntime(zepDefs,
                    name => runtime.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null, zepNets);
                zeps.WireDamage(runtime);

                // The node filter GameSession puts on both subscriptions: a signal from THIS
                // world's zeppelin wins a mission whose objective is it, and never one naming
                // another node.
                void Report(string node, params InstantActionRuntime[] missions)
                {
                    foreach (var ia in missions)
                    {
                        if (string.Equals(node, InstantActionRuntime.SelectedZeppelinNode(ia.Def),
                                System.StringComparison.OrdinalIgnoreCase))
                        {
                            ia.ReportObjective(InstantActionObjective.ZeppelinDisabled);
                        }
                    }
                }

                // ---- path 1: the engines, which is what the mode is FOR --------------------
                var engineMission = new InstantActionRuntime(EndDef(ctx, "zep-engines",
                    "zeppelin_run", cargoZeppelin: "piratezep"));
                var otherHull = new InstantActionRuntime(EndDef(ctx, "zep-other", "zeppelin_run",
                    cargoZeppelin: "someotherzep"));
                zeps.ZeppelinEnginesDisabled += n => Report(n, engineMission, otherHull);
                zeps.ZeppelinKilled += n => Report(n, engineMission, otherHull);
                ctx.Check(InstantActionRuntime.SelectedZeppelinNode(engineMission.Def) == "piratezep",
                    $"the mission's objective resolves to the world's zeppelin");

                var engines = zepDefs[0].Engines;
                var motion = zeps.MotionFor("piratezep");
                ctx.Check(motion != null && engines.Count > 0 && motion.AliveEngines == engines.Count,
                    $"all {engines.Count} of piratezep's authored engines start alive: {motion?.AliveEngines}");
                for (int i = 0; i < engines.Count; i++)
                {
                    runtime.DamageAt(runtime.FindNodes(engines[i], host).FirstOrDefault(), 10_000f);
                    zeps.SimStep(1f / 60f);
                    if (i == engines.Count - 2)
                    {
                        // The able-to-fail control on the win below: one engine short is not it.
                        ctx.Check(!engineMission.Ended && motion?.AliveEngines == 1,
                            $"with ONE engine left the mission runs on: alive={motion?.AliveEngines} outcome={engineMission.Outcome}");
                    }
                }
                ctx.Check(motion?.AliveEngines == 0
                    && engineMission.Outcome == InstantActionOutcome.Won,
                    $"the last engine dying WINS a zeppelin_run mission: alive={motion?.AliveEngines} outcome={engineMission.Outcome}");
                ctx.Check(!zeps.IsDead("piratezep"),
                    $"…with the HULL still alive — engines are their own win, not a kill");
                ctx.Check(!otherHull.Ended,
                    $"…and a mission whose objective is another hull is untouched: {otherHull.Outcome}");

                // ---- path 2: the hull, which also wins the mode ----------------------------
                // Subscribed only now, so the engines signal already fired above cannot be what
                // ends it: this runtime sees the gasbag threshold and nothing else.
                var hullMission = new InstantActionRuntime(EndDef(ctx, "zep-hull", "zeppelin_run",
                    cargoZeppelin: "piratezep"));
                zeps.ZeppelinKilled += n => Report(n, hullMission);

                // The decoded survivor threshold does the killing (4 of 6 required): two gasbags
                // down is not enough, the third is.
                runtime.DamageAt(runtime.FindNodes("gasbag1", host).FirstOrDefault(), 10_000f);
                runtime.DamageAt(runtime.FindNodes("gasbag2", host).FirstOrDefault(), 10_000f);
                zeps.SimStep(1f / 60f);
                ctx.Check(!zeps.IsDead("piratezep") && !hullMission.Ended,
                    $"two gasbags down: the hull lives and the mission runs on");
                runtime.DamageAt(runtime.FindNodes("gasbag3", host).FirstOrDefault(), 10_000f);
                zeps.SimStep(1f / 60f);
                ctx.Check(zeps.IsDead("piratezep")
                    && hullMission.Outcome == InstantActionOutcome.Won,
                    $"the objective's real death ALSO wins a zeppelin_run mission: {hullMission.Outcome}");
            }
            finally
            {
                zeps?.Free();
            }
        });
    }

    /// <summary>A hand-authored <c>--ia=</c> file for one end-condition case, written to the
    /// scratch folder and read back through the REAL reader — so a change to how
    /// <c>mission_type</c>/<c>lives</c> parse moves this suite too, and no test builds an
    /// <c>InstantActionDef</c> the CLI could not produce.</summary>
    private static InstantActionDef EndDef(TestContext ctx, string tag, string missionType,
        int? lives = null, string? cargoZeppelin = null)
    {
        string json = $"{{\"mission_type\": \"{missionType}\""
            + (lives is { } n ? $", \"lives\": {n}" : string.Empty)
            + (cargoZeppelin != null ? $", \"cargo_zeppelin\": \"{cargoZeppelin}\"" : string.Empty)
            + "}";
        string name = $"ia-end-{tag}.json";
        ctx.WriteArtifact(name, json);
        return InstantAction.LoadFromJson(Path.Combine(ctx.ScratchDir, name));
    }

    /// <summary>The G14 wrap-up board's two shot counters (PLAN-instant-action.md,
    /// docs/formats/instant-action.md "What the four numbers count"): <c>ProjectilePool</c> is the
    /// single choke point for both, so this fires real rounds through the real pool at a real
    /// target rather than asserting on the arithmetic in isolation. The board's other two rows —
    /// Danger Zones Completed (a live read of <c>StuntMission.CompletedCount</c>) and Enemies Shot
    /// Down (a plain <c>Downed</c>-event tally already exercised by every other Downed-driven
    /// assertion in <see cref="InstantActionEnd"/>) — need no dedicated instrument here.</summary>
    private static void InstantActionWrapup(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null);
        WeaponDef? cannon = weaponDefs.All.FirstOrDefault(w => w.IsCannon && w.ArmorDamage is > 0f);
        WeaponDef? rocket = weaponDefs.All.FirstOrDefault(w => w.IsRocket);
        ctx.Check(cannon != null && rocket != null,
            $"a CANNON gun and a rocket both exist in the data (cannon={cannon != null} rocket={rocket != null})");
        if (cannon == null || rocket == null)
            return;

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? target = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            var spec = SessionSpec.Parse(System.Array.Empty<string>());
            var liveries = new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof"));
            var inputs = new FlightRigAssembler.Inputs
            {
                PlanesGamez = planesGamez,
                StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
                RigCount = 0,
                PaintRng = new RandomNumberGenerator(),
                ZrdrPath = ctx.ZrdrPath,
                StockLoadouts = StockLoadouts.Load(),
                WeaponDefs = weaponDefs,
                Textures = textures,
                Projectiles = live,
                Shakes = ShakeDefs.Load(ctx.ZrdrPath),
            };
            var spawner = new AiAircraftSpawner(spec, liveries, null!, ctx.Host, inputs);

            var pos = new Vector3(0f, 500f, 0f);
            target = spawner.Spawn(ctx.PlaneName, pos, pos + Vector3.Forward,
                AiPilot.HoldingCourse(pos, pos + Vector3.Forward),
                scheme: null, team: InstantActionRuntime.EnemyTeam);
            ctx.Check(target?.Body != null, $"the target built a collision body");
            if (target?.Body == null)
                return;

            // ScoredShooters (G14): shooter 0 stands in for a registered human seat, 999 for an
            // AI's shooter id, which a mission never adds to the set.
            live.ScoredShooters.Add(0);

            var muzzle = new Transform3D(
                Basis.LookingAt(Vector3.Back, Vector3.Up), pos + new Vector3(0f, 0f, -20f));
            void FireOnce(WeaponDef weapon, int shooterId)
            {
                live.Spawn(weapon, muzzle, Vector3.Zero, shooterId: shooterId);
                for (int i = 0; i < 20; i++)
                    live.SimStep(1f / 60f);
                live.Clear();
            }

            FireOnce(cannon, shooterId: 0);
            ctx.Check(live.CannonRoundsFired == 1 && live.CannonHits == 1,
                $"a scored shooter's cannon round counts as both fired and hit: fired={live.CannonRoundsFired} hits={live.CannonHits}");

            FireOnce(cannon, shooterId: 999);
            ctx.Check(live.CannonRoundsFired == 1 && live.CannonHits == 1,
                $"an unscored shooter's identical shot moves neither counter: fired={live.CannonRoundsFired} hits={live.CannonHits}");

            FireOnce(rocket, shooterId: 0);
            ctx.Check(live.CannonRoundsFired == 1 && live.CannonHits == 1,
                $"a scored shooter's ROCKET is excluded from the cannon-only counters: fired={live.CannonRoundsFired} hits={live.CannonHits}");
        }
        finally
        {
            pool?.Free();
            target?.Free();
            textures.Dispose();
        }
    }

    /// <summary>The E10 inert state (PLAN-instant-action.md): an aircraft built complete and then
    /// held out of the session until it is activated. Every claim is measured by ONE instrument run
    /// over three subjects — a live control, the inert aircraft, and that same aircraft after
    /// <c>Activate</c> — because "did not appear in the list" is precisely the check that passes for
    /// the wrong reason (verification.md METHOD-9/METHOD-10: the perturbation must be watched
    /// flipping every observation, in both directions). The instruments are the real ones: a
    /// physics raycast on the shared space state, <c>ProjectilePool.CollectAircraft</c> into a real
    /// <see cref="AimAssist.Scan"/>, a real round fired through the pool, and
    /// <c>FlightController.SimStep</c>.</summary>
    private static void InertAircraft(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null);
        WeaponDef? gun = weaponDefs.All.FirstOrDefault(w => w.IsGun && w.ArmorDamage is > 0f);
        ctx.Check(gun != null, $"a gun with ARMOR_DAMAGE exists in the data");
        if (gun == null)
            return;

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? control = null;
        FlightController? subject = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            var spec = SessionSpec.Parse(System.Array.Empty<string>());
            var liveries = new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof"));
            var inputs = new FlightRigAssembler.Inputs
            {
                PlanesGamez = planesGamez,
                StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
                RigCount = 0,
                PaintRng = new RandomNumberGenerator(),
                ZrdrPath = ctx.ZrdrPath,
                StockLoadouts = StockLoadouts.Load(),
                WeaponDefs = weaponDefs,
                Textures = textures,
                Projectiles = live,
                Shakes = ShakeDefs.Load(ctx.ZrdrPath),
            };
            // worldEffects null!: never dereferenced — CrashProgram/WorldScene stay null, so the
            // spawner's crash-runtime block (its only reader) is skipped.
            var spawner = new AiAircraftSpawner(spec, liveries, null!, ctx.Host, inputs);

            // One scan origin, the control dead ahead on −Z, the subject 90° off it on +X, so a
            // scan aimed at either sits well outside the other's acceptance cone.
            var origin = new Vector3(0f, 500f, 0f);
            var controlPos = origin + new Vector3(0f, 0f, -300f);
            var subjectPos = origin + new Vector3(300f, 0f, 0f);
            control = spawner.Spawn(ctx.PlaneName, controlPos, controlPos + Vector3.Forward,
                AiPilot.HoldingCourse(controlPos, controlPos + Vector3.Forward),
                scheme: null, team: InstantActionRuntime.EnemyTeam);
            subject = spawner.Spawn(ctx.PlaneName, subjectPos, subjectPos + Vector3.Forward,
                AiPilot.HoldingCourse(subjectPos, subjectPos + Vector3.Forward),
                scheme: null, team: InstantActionRuntime.EnemyTeam, inert: true);
            ctx.Check(control.Body != null && subject.Body != null && control.Damage != null
                      && subject.Damage != null,
                $"both aircraft built a collision body and per-part damage");
            if (control.Body == null || subject.Body == null
                || control.Damage == null || subject.Damage == null)
                return;
            ctx.Check(control.InPlay && !subject.InPlay,
                $"the control is in play and the inert one is not: control={control.InPlay} subject={subject.InPlay}");

            // --- the four instruments. Each takes the aircraft it is measuring and reads its LIVE
            // position, so a subject that has moved (activation re-homes it) is still measured
            // where it actually is.
            var space = live.GetWorld3D().DirectSpaceState;
            bool RayFinds(FlightController rig)
            {
                var at = rig.WorldPosition;
                var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                    at + new Vector3(0f, 0f, -30f), at, CollisionLayers.WorldAndAircraft));
                return hit.Count > 0 && ReferenceEquals(hit["collider"].Obj, rig.Body);
            }

            var candidates = new AimCandidateSet();
            bool ScanFinds(FlightController rig)
            {
                candidates.Clear();
                live.CollectAircraft(candidates);
                var scan = new AimScan
                {
                    MuzzlePosition = origin,
                    Forward = (rig.WorldPosition - origin).Normalized(),
                    Team = AimAssist.PlayerTeam,   // hostile to both aircraft (team 2)
                    Speed = 500f,
                    RangeSquared = 2000f * 2000f,
                    ConeCos = Mathf.Cos(Mathf.DegToRad(10f)),
                };
                return AimAssist.Scan(scan, candidates, out var result)
                       && ReferenceEquals(result.Source, rig);
            }

            float Combined(FlightController rig) => rig.Damage!.Parts.Values.Sum(p => p.Hp + p.Armor);
            bool RoundBites(FlightController rig)
            {
                float before = Combined(rig);
                var at = rig.WorldPosition;
                var muzzle = new Transform3D(
                    Basis.LookingAt(Vector3.Back, Vector3.Up), at + new Vector3(0f, 0f, -20f));
                for (int tries = 0; tries < 6 && Combined(rig) >= before; tries++)
                {
                    live.Spawn(gun, muzzle, Vector3.Zero, shooterId: 0);
                    for (int i = 0; i < 20; i++)
                        live.SimStep(1f / 60f);
                    live.Clear();
                }
                return Combined(rig) < before;
            }

            bool StepMoves(FlightController rig)
            {
                var before = rig.WorldPosition;
                for (int i = 0; i < 10; i++)
                    rig.SimStep(1f / 60f);
                return rig.WorldPosition.DistanceTo(before) > 1f;
            }

            // --- the able-to-fail baseline: the live control answers YES to all four, so a NO
            // below is the inert flag and not a broken instrument.
            ctx.Check(RayFinds(control), $"baseline: a raycast returns the live control's body");
            ctx.Check(ScanFinds(control), $"baseline: an aim-assist scan returns the live control");
            ctx.Check(RoundBites(control), $"baseline: a round fired through the live control costs it HP");
            ctx.Check(StepMoves(control), $"baseline: a sim step moves the live control");
            ctx.Check(control.PlaneModel!.Visible, $"baseline: the live control is drawn");

            // --- the inert aircraft: the same four instruments, same session, all NO.
            ctx.Check(!RayFinds(subject), $"a raycast does not return the inert aircraft");
            ctx.Check(!ScanFinds(subject), $"an aim-assist scan does not return the inert aircraft");
            float inertBefore = Combined(subject);
            ctx.Check(!RoundBites(subject), $"a round fired through the inert aircraft passes through it");
            ctx.Check(Mathf.IsEqualApprox(Combined(subject), inertBefore),
                $"…and its damage pools are untouched: {Combined(subject):0.##} vs {inertBefore:0.##}");
            ctx.Check(!StepMoves(subject), $"a sim step does not move the inert aircraft");
            ctx.Check(!subject.PlaneModel!.Visible, $"the inert aircraft is not drawn");

            // Trap (b): inert is NOT "left off the pool's roster". The plane reached
            // RegisterAircraft in _Ready like any other, so the fuse/blast passes (which walk that
            // roster, not the physics layers) can see it at all — and are the reason InPlay is
            // read there. It is LISTED as a candidate and simply not live, which is also what lets
            // E11's wave walk count a parked wave as still present.
            candidates.Clear();
            live.CollectAircraft(candidates);
            var listed = candidates.Vehicles.FirstOrDefault(c => ReferenceEquals(c.Source, subject));
            ctx.Check(listed.Source != null && !listed.Live,
                $"the inert aircraft is on the pool's candidate roster but not live: listed={listed.Source != null} live={listed.Live}");

            // --- activation: the same aircraft answers YES to all four again. This is the
            // perturbation run backwards, and it is what proves the checks above were not passing
            // because the aircraft was simply broken.
            // ⚠ Activated AT ITS BUILD POSE on purpose. Godot flushes transform notifications at
            // the end of a frame, and this harness completes inside one _Ready and never yields
            // one — so a body MOVED here keeps its build-pose transform on the physics server and
            // no raycast can find it at the new spot. Measured, not assumed: the live control also
            // stops answering RayFinds once a sim step has moved it. The re-home is therefore
            // asserted below off the flight model, which is the truth either way.
            subject.Activate(subjectPos, subjectPos + Vector3.Forward);
            ctx.Check(subject.InPlay && !subject.Inert, $"Activate cleared the inert flag");
            ctx.Check(RayFinds(subject), $"the activated aircraft is returned by a raycast");
            ctx.Check(ScanFinds(subject), $"the activated aircraft is returned by an aim-assist scan");
            ctx.Check(RoundBites(subject), $"a round fired through the activated aircraft costs it HP");
            ctx.Check(StepMoves(subject), $"a sim step moves the activated aircraft");
            ctx.Check(subject.PlaneModel.Visible, $"the activated aircraft is drawn");

            // Activation's other half: it re-homes the aircraft at the pose it is given, which is
            // how E11's wave teleport will arrive. Read off the flight model (WorldPosition is the
            // sim value) rather than the node, for the frame-flush reason above.
            var elsewhere = subjectPos + new Vector3(0f, 0f, -900f);
            subject.Activate(elsewhere, elsewhere + Vector3.Forward);
            ctx.Check(subject.WorldPosition.DistanceTo(elsewhere) < 1f,
                $"Activate re-homes the aircraft at the pose it is given pos={subject.WorldPosition}");
            float pristine = subject.Damage.Parts.Values.Sum(p => p.Def.MaxHp + p.Def.MaxArmor);
            ctx.Check(Mathf.IsEqualApprox(Combined(subject), pristine),
                $"…with a repaired airframe, the respawn it rides on top of: {Combined(subject):0.##}/{pristine:0.##}");
        }
        finally
        {
            pool?.Free();
            control?.Free();
            subject?.Free();
            textures.Dispose();
        }
    }

    private static void CarriedTurrets(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var turretDefs = TurretDefs.Load(ctx.ZrdrPath);
        ctx.Check(turretDefs.All.Count(d => d.Carried) == 16,
            $"the 16 carried ai.zrd entries parse carried={turretDefs.All.Count(d => d.Carried)}");

        // The Kestrel: a single rear turret (thirdp MSG_TUR_PAC_G3 — PITCH [20,50], YAW
        // [105,255], the directed rear arc through 180°, DETECTION_RANGE 450, FIRE_RATE 0.4,
        // ATTACK 4 s / BORED 3 s scalars).
        const string HostPlane = "player_kestrel";
        var stats = PlaneStats.Load(ctx.ZrdrPath, HostPlane);
        ctx.Check(stats.TurretMounts.Count(m => !m.FirstPerson) == 1,
            $"{HostPlane} carries one thirdp turret mount");

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? host = null;
        FlightController? target = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            FlightController BuildRig(string plane, PlaneStats st, int playerIndex, Vector3 pos)
            {
                var model = new PlaneBuilder(planesGamez, textures).Build(plane);
                var rig = new FlightController
                {
                    PlaneModel = model,
                    Collider = PlaneCollider.Build(model),
                    Damage = new PlaneDamage(st.DestroyableParts),
                    PlayerIndex = playerIndex,
                    Projectiles = live,
                    UseKeyboard = false,
                    AllowPause = false,
                };
                rig.AddChild(model);
                // Nose on world -Z (identity attitude): plane-local == world - pos.
                rig.Setup(new FlightModel(st), ctx.Camera, new CamParams(), pos, pos + Vector3.Forward);
                ctx.Host.AddChild(rig);
                // Park at zero speed: Setup leaves the model at spawn speed, and a rig this
                // suite never steps would otherwise REPORT that velocity while standing still —
                // the turret then leads a phantom motion and every round misses.
                rig.PlaceHeld(pos, pos + Vector3.Forward);
                return rig;
            }

            var hostPos = new Vector3(0f, 500f, 0f);
            host = BuildRig(HostPlane, stats, 0, hostPos);
            host.Turrets = TurretController.BuildCarried(
                turretDefs, stats, host.PlaneModel!, weapons, host, live);
            ctx.Check(host.Turrets.Length == 1,
                $"the mount resolves against the built model turrets={host.Turrets.Length}");
            if (host.Turrets.Length != 1)
                return;
            var turret = host.Turrets[0];
            ctx.Check(turret.YawNode != null && turret.Firepoints.Length == 1,
                $"the PARTS chain resolved yaw={turret.YawNode?.Name} muzzles={turret.Firepoints.Length}");
            ctx.Check(turret.Weapon.Id == turret.Def.WeaponName,
                $"WEAPON.NAME resolved as a ballistics id ({turret.Weapon.Id})");

            // Load pose: the centre of each arc — yaw 180 (rearward), pitch 35.
            var (restYaw, restPitch) = TurretController.AnglesOfLocal(turret.BarrelLocal);
            ctx.Check(Mathf.Abs(Mathf.Wrap(restYaw - 180f, -180f, 180f)) < 0.5f
                      && Mathf.Abs(restPitch - 35f) < 0.5f,
                $"the turret poses at its arc centre yaw={restYaw:0.#} pitch={restPitch:0.#}");

            // The target: in-arc (behind and above the host — yaw ~180, elevation ~35°), inside
            // DETECTION_RANGE, on a hostile team. INACCURACY is zeroed so every gated round flies
            // the solved line — the scatter cone itself is covered by the aim-assist suite.
            turret.Def.InaccuracyDeg = 0f;
            var targetStats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
            var targetPos = hostPos + new Vector3(0f, 105f, 150f);
            target = BuildRig(ctx.PlaneName, targetStats, 1, targetPos);

            float Combined(FlightController rig) => rig.Damage!.Parts.Values.Sum(p => p.Hp + p.Armor);
            bool Pristine(FlightController rig) => rig.Damage!.Parts.Values.All(
                p => p.Hp >= p.Def.MaxHp && p.Armor >= p.Def.MaxArmor);
            void Step(int frames)
            {
                for (int i = 0; i < frames; i++)
                {
                    turret.SimStep(1f / 60f);
                    live.SimStep(1f / 60f);
                }
            }

            // The aim assist's turret candidate list is fed (the AimAssist.cs:387 seam).
            var candidates = new AimCandidateSet();
            live.CollectTurrets(candidates);
            ctx.Check(candidates.Turrets.Count == 1
                      && candidates.Turrets[0].Team == AimAssist.TeamOfPilot(host.PlayerIndex)
                      && candidates.Turrets[0].Live,
                $"CollectTurrets feeds the candidate scan count={candidates.Turrets.Count}");

            // --- track and fire: two seconds inside the initial 4 s attack window. The barrel
            // slews onto the target and the shots land — under the host's shooter id, so the
            // rounds crossing the host's own tail (the rear arc points across it) exclude it.
            float before = Combined(target);
            Step(120);
            var toTarget = (target.WorldPosition - turret.WorldPosition).Normalized();
            ctx.Check(turret.BarrelWorldDir.Dot(toTarget) > TurretController.FireGateCos,
                $"the barrel slewed onto the target dot={turret.BarrelWorldDir.Dot(toTarget):0.000}");
            ctx.Check(turret.ShotsFired >= 3,
                $"the turret fires through the 15° gate at its FIRE_RATE shots={turret.ShotsFired}");
            ctx.Check(Combined(target) < before,
                $"turret rounds strike the target moved={before - Combined(target):0.##}");
            ctx.Check(Pristine(host) && !host.Crashed,
                $"the host's own airframe took nothing from its own gunner");

            // --- the duty cycle: run to the bored window (ATTACK 4 s from build), then move the
            // target across the arc — firing stops, tracking does not.
            int shotsAtBored = -1;
            for (int i = 0; i < 600 && turret.Attacking; i++)
            {
                Step(1);
            }
            ctx.Check(!turret.Attacking, $"the attack window expires into bored");
            shotsAtBored = turret.ShotsFired;
            target.PlaceHeld(hostPos + new Vector3(90f, 105f, 120f), hostPos); // still in-arc, new bearing
            Step(90); // 1.5 s of the 3 s bored window
            var toMoved = (target.WorldPosition - turret.WorldPosition).Normalized();
            ctx.Check(turret.ShotsFired == shotsAtBored,
                $"bored suppresses firing shots={turret.ShotsFired} (was {shotsAtBored})");
            ctx.Check(turret.BarrelWorldDir.Dot(toMoved) > TurretController.FireGateCos,
                $"…but tracking continues through it dot={turret.BarrelWorldDir.Dot(toMoved):0.000}");

            // --- out of arc: a target ahead of the host sits outside YAW [105,255]; the turret
            // parks at the angularly NEARER end stop and never fires, whatever the duty cycle.
            target.PlaceHeld(hostPos + new Vector3(-52f, 105f, -140f), hostPos); // yaw ≈ +20°, in reach
            int shotsAtOutOfArc = turret.ShotsFired;
            Step(300); // 5 s spans at least one full attack window
            var (parkedYaw, _) = TurretController.AnglesOfLocal(turret.BarrelLocal);
            ctx.Check(turret.ShotsFired == shotsAtOutOfArc,
                $"an out-of-arc target draws no fire shots={turret.ShotsFired}");
            ctx.Check(Mathf.Abs(parkedYaw - 105f) < 1.5f,
                $"the barrel parks at the nearer end stop (105°, not 255°) yaw={parkedYaw:0.#}");

            // --- YAW [0,0] means UNRESTRICTED: with the limit spelled that way the same ahead
            // target becomes reachable and the turret opens fire — the misread ('locked forward')
            // would keep it silent forever. Pitch stays authored, so keep the target elevated.
            turret.Def.YawMinDeg = 0f;
            turret.Def.YawMaxDeg = 0f;
            Step(300);
            ctx.Check(turret.ShotsFired > shotsAtOutOfArc,
                $"YAW [0,0] removes the traverse limit shots={turret.ShotsFired} (was {shotsAtOutOfArc})");

            // --- a crashed host silences its gunner, and the candidate list reports it dead.
            host.DebugForceCrash();
            int shotsAtCrash = turret.ShotsFired;
            Step(120);
            ctx.Check(!turret.Alive && turret.ShotsFired == shotsAtCrash,
                $"a crashed host's turret goes quiet shots={turret.ShotsFired}");
            candidates.Clear();
            live.CollectTurrets(candidates);
            ctx.Check(candidates.Turrets.Count == 1 && !candidates.Turrets[0].Live,
                $"the dead turret stays listed but not live");
        }
        finally
        {
            pool?.Free();
            host?.Free();
            target?.Free();
            textures.Dispose();
        }
    }

    /// <summary>The world AA emplacements (M4 C9b) against the real C1 chapter world: the NODES
    /// placement census, the shipped-ACTIVATED default, the --wake-turrets stand-in, the
    /// enemy-default/ally team split, the aim-assist candidate list, and the healthy-node kill
    /// switch. Zeppelin-slung entries are placed (they are world nodes) and their one gameplay
    /// path is checked here too: the Instant Action builder's subtree-scoped activation of the
    /// objective hull's rings, both directions, plus the fire it puts on a plane alongside.</summary>
    private static void WorldTurrets(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var turretDefs = TurretDefs.Load(ctx.ZrdrPath);
        ctx.Check(turretDefs.All.Count(d => !d.Carried) == 26,
            $"the 26 standalone ai.zrd entries parse standalone={turretDefs.All.Count(d => !d.Carried)}");

        // The mapping CSVM runs the decoded team space through: neutral stays untargetable,
        // ally = player one's side, the enemy default lands clear of every pilot team.
        ctx.Check(TurretController.EngineTeamFor(0) == AimAssist.NeutralTeam
                  && TurretController.EngineTeamFor(1) == AimAssist.TeamOfPilot(0)
                  && TurretController.EngineTeamFor(TurretDef.DefaultTeamId) > AimAssist.WorldTeam,
            $"the team mapping: neutral 0, ally = P1's team, enemy default past every pilot team");

        ctx.WithWorld("C1", collision: true, world =>
        {
            var textures = new TextureArchive(texturesPath);
            ProjectilePool? pool = null;
            FlightController? target = null;
            FlightController? friend = null;
            FlightController? zepBait = null;   // its own rig: the zeppelin rings shoot it to bits
            Session.TurretEmplacementRuntime? emplacements = null;   // a Node now: freed below
            try
            {
                var live = new ProjectilePool(textures, null, null)
                {
                    DamageSink = world.Runtime.DamageAt,
                };
                pool = live;
                ctx.Host.AddChild(live);
                var runtime = emplacements = new Session.TurretEmplacementRuntime(turretDefs, weapons,
                    (pattern, scope) => world.Runtime.FindNodes(pattern, scope), live,
                    world.Runtime.WorldRoot);

                // The placement census: one entry instantiates as many turrets as its patterns
                // match, so the counts are properties of C1's world model. Pinned as goldens.
                var byEntry = new Dictionary<string, int>();
                foreach (var t in runtime.Emplacements)
                {
                    string site = t.Label[(t.Label.IndexOf('@') + 1)..];
                    string root = site.TrimEnd("0123456789 ".ToCharArray());
                    string key = $"{t.Def.Title}:{root}";
                    byEntry.TryGetValue(key, out int had);
                    byEntry[key] = had + 1;
                }
                ctx.Note($"C1 census: {string.Join(", ", byEntry.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"))}");
                int AagunCount() => runtime.Emplacements.Count(t => t.Label.StartsWith("MSG_TUR_AAA@aagun"));
                ctx.Same(5, AagunCount(), $"C1 places the five aagun emplacements");
                ctx.Same(9, runtime.Emplacements.Count(t => t.Label.StartsWith("MSG_TUR_BALLOON_TOP@bbtur")),
                    $"C1 places the nine balloon-top emplacements");
                ctx.Same(74, runtime.Count, $"C1's whole emplacement census");
                ctx.Same(15, runtime.AwakeCount,
                    $"shipped ACTIVATED: only the piratezep's own awake rings are up (ally, TEAM 1)");
                ctx.Check(runtime.Emplacements.Where(t => t.Activated)
                        .All(t => t.EngineTeam == AimAssist.TeamOfPilot(0)),
                    $"every emplacement awake by data is on the ally team — no hostile fires unwoken");

                // A dormant hostile emplacement: aagun32, enemy by the loader's no-TEAM default.
                var aagun = runtime.Emplacements.FirstOrDefault(t => t.Label.EndsWith("@aagun32"));
                ctx.Check(aagun != null, $"aagun32 built a gunner");
                if (aagun == null)
                    return;
                ctx.Check(!aagun.Activated && aagun.EngineTeam > AimAssist.WorldTeam,
                    $"aagun32 is dormant and hostile-by-default team={aagun.EngineTeam}");
                aagun.Def.InaccuracyDeg = 0f; // determinism: the scatter cone is the assist suite's

                FlightController BuildRig(string plane, int playerIndex, Vector3 pos, Vector3 look)
                {
                    var st = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
                    var model = new PlaneBuilder(planesGamez, textures).Build(plane);
                    var rig = new FlightController
                    {
                        PlaneModel = model,
                        Collider = PlaneCollider.Build(model),
                        Damage = new PlaneDamage(st.DestroyableParts),
                        PlayerIndex = playerIndex,
                        Projectiles = live,
                        UseKeyboard = false,
                        AllowPause = false,
                    };
                    rig.AddChild(model);
                    rig.Setup(new FlightModel(st), ctx.Camera, new CamParams(), pos, look);
                    ctx.Host.AddChild(rig);
                    rig.PlaceHeld(pos, look); // parked: no phantom velocity for the lead solve
                    return rig;
                }

                // A hostile plane parked inside DETECTION_RANGE (500 m), well above the fort.
                var gunPos = aagun.WorldPosition;
                var targetPos = gunPos + new Vector3(120f, 250f, 0f);
                target = BuildRig(ctx.PlaneName, 0, targetPos, targetPos + Vector3.Forward);

                void Step(int frames)
                {
                    for (int i = 0; i < frames; i++)
                    {
                        runtime.SimStep(1f / 60f);
                        live.SimStep(1f / 60f);
                    }
                }

                float Combined(FlightController rig) => rig.Damage!.Parts.Values.Sum(p => p.Hp + p.Armor);

                // Dormant = inert: no tracking, no fire, through several attack windows.
                var restDir = aagun.BarrelWorldDir;
                Step(240);
                ctx.Check(aagun.ShotsFired == 0 && aagun.BarrelWorldDir.IsEqualApprox(restDir),
                    $"a dormant emplacement neither tracks nor fires shots={aagun.ShotsFired}");

                // The Instant Action builder's own turret arm: a subtree-scoped ACTIVATED write
                // over the objective zeppelin's node, which is what arms multiplayer1zep's four
                // dormant entries on a zeppelin_run. Scoped, and reversible: the deactivation
                // loop is the same call with the flag cleared.
                var mp1 = world.Runtime.FindNodes("multiplayer1zep");
                ctx.Same(1, mp1.Count, $"C1's world carries the Instant Action zeppelin node");
                var mp2 = world.Runtime.FindNodes("multiplayer2zep");
                ctx.Same(1, mp2.Count, $"…and the second MP zeppelin, the control for the scoping");
                if (mp1.Count > 0 && mp2.Count > 0)
                {
                    List<TurretController> RingsOn(Node3D root) => runtime.Emplacements
                        .Where(t => t.Site is { } s && (s == root || root.IsAncestorOf(s))).ToList();
                    var mp1Rings = RingsOn(mp1[0]);
                    var mp2Rings = RingsOn(mp2[0]);
                    ctx.Same(14, mp1Rings.Count,
                        $"multiplayer1zep carries 14 rings (3 nose, 3 belly, 4 left, 4 right)");
                    ctx.Check(mp1Rings.All(t => !t.Activated),
                        $"…every one of them dormant by data, which is why it flies unarmed");
                    ctx.Same(14, runtime.SetActivatedUnder(mp1[0], true),
                        $"the builder's zeppelin arm arms every ring on the objective hull");
                    ctx.Check(mp1Rings.All(t => t.Activated && t.EngineTeam > AimAssist.WorldTeam),
                        $"…all of them awake and hostile, the no-TEAM loader default");
                    ctx.Check(!aagun.Activated && mp2Rings.All(t => !t.Activated),
                        $"…and nothing outside that subtree woke with it");

                    // The sight-line rule, both halves. A ring's own MOUNTING SECTION (the hull
                    // group its site hangs off) is out of its sight line, because the ray starts
                    // inside that geometry and would report blocked in every direction; the rest
                    // of the hull stays in, which is what keeps a ring from shooting through its
                    // own zeppelin. ⚠ Neither half is asserted through ray outcomes here: every
                    // unplaced vehicle loads at the map corner (interp.md), so piratezep and
                    // multiplayer2zep sit INSIDE multiplayer1zep in this world and block any line
                    // whatever is excluded. The flown check is a session — where the near rings
                    // engage and the ones firing across the hull report blocked.
                    var ring = mp1Rings[0];
                    var section = TurretController.PlatformOf(ring.Site, world.Runtime.WorldRoot);
                    ctx.Check(section != null && section != mp1[0] && mp1[0].IsAncestorOf(section)
                              && section.IsAncestorOf(ring.Site!),
                        $"a ring's mounting section is a piece OF the hull ('{section?.Name}'), never the whole hull and never just its own rig");
                    var excluded = ring.PlatformColliderRids();
                    var ownMount = ring.Site!.FindChildren("*", "CollisionObject3D", true, false)
                        .OfType<CollisionObject3D>().ToList();
                    var hullBodies = mp1[0].FindChildren("*", "CollisionObject3D", true, false)
                        .OfType<CollisionObject3D>().ToList();
                    ctx.Check(ownMount.Count > 0 && ownMount.All(b => excluded.Contains(b.GetRid())),
                        $"the gun's own mount is out of its sight line: {ownMount.Count} body/bodies, the ones the ray starts inside");
                    ctx.Check(excluded.Count > ownMount.Count && excluded.Count < hullBodies.Count,
                        $"…with its own section but NOT the whole hull: {excluded.Count} excluded of the hull's {hullBodies.Count}");

                    // What the player actually feels: an armed hull shoots back. Its own rig, so
                    // the rounds it eats do not touch the aagun measurements below. ⚠ The hull is
                    // SHOWN first, exactly as the Instant Action builder shows it: C1/IA1's script
                    // hides multiplayer1zep, and a hidden hull has no live colliders, so a suite
                    // that skips this line cannot see a turret blocked by its own zeppelin.
                    mp1[0].Visible = true;
                    int ZepShots() => mp1Rings.Sum(t => t.ShotsFired);
                    var ringPos = mp1Rings.Count > 0 ? mp1Rings[0].WorldPosition : Vector3.Zero;
                    zepBait = BuildRig(ctx.PlaneName, 0, ringPos + new Vector3(0f, -80f, 200f), ringPos);
                    float baitBefore = Combined(zepBait);
                    Step(300);
                    ctx.Check(ZepShots() > 0,
                        $"the armed zeppelin engages a hostile plane alongside shots={ZepShots()}");
                    ctx.Check(Combined(zepBait) < baitBefore,
                        $"…with rounds striking it moved={baitBefore - Combined(zepBait):0.##}");

                    // ⚠ The tick contract, and the reason this suite could pass while the guns
                    // stood silent in ordinary play: GameSession.DriveSimSteps runs ONLY on a
                    // parent-driven clock, so a runtime that steps from there alone is inert on
                    // the realtime clock every real session uses — and every suite and golden
                    // runs fixed-step, which is exactly the blind spot. Driven here the way
                    // Godot's physics tick drives it, with a realtime clock in Current.
                    var savedClock = Utils.GameClock.Current;
                    Utils.GameClock.Current = new Utils.GameClock { Mode = Utils.GameClock.RunMode.Realtime };
                    int shotsBeforeRealtime = ZepShots();
                    for (int i = 0; i < 240; i++)
                    {
                        runtime._PhysicsProcess(1.0 / 60.0);
                        live.SimStep(1f / 60f);
                    }
                    Utils.GameClock.Current = savedClock;
                    ctx.Check(ZepShots() > shotsBeforeRealtime,
                        $"the runtime steps ITSELF on a realtime clock: {ZepShots() - shotsBeforeRealtime} more shot(s) with nobody calling SimStep");

                    zepBait.PlaceHeld(ringPos + new Vector3(0f, -80f, 20000f), ringPos);

                    ctx.Same(14, runtime.SetActivatedUnder(mp1[0], false),
                        $"the same call with the flag cleared stows them again (the b=0 arm)");
                }

                // The stand-in wakes it — explicit, counted, logged — and it engages.
                int woken = runtime.WakeAll();
                ctx.Same(runtime.Count - 15, woken, $"--wake-turrets stand-in wakes every dormant emplacement");
                float before = Combined(target);
                Step(360);
                var toTarget = (target.WorldPosition - aagun.WorldPosition).Normalized();
                ctx.Check(aagun.BarrelWorldDir.Dot(toTarget) > TurretController.FireGateCos,
                    $"the woken gun slewed onto the plane dot={aagun.BarrelWorldDir.Dot(toTarget):0.000}");
                ctx.Check(aagun.ShotsFired >= 2,
                    $"…and fires at its FIRE_RATE shots={aagun.ShotsFired}");
                ctx.Check(Combined(target) < before,
                    $"…with rounds striking the target moved={before - Combined(target):0.##}");

                // The team gate, both halves over the piratezep's allied rings (authored TEAM 1
                // = the player's side): none engages the player's own plane, and the same rings
                // DO engage a hostile pane there — the able-to-fail control for the hold. The
                // per-ring arcs are the zep's own frame, so the assertions run over the whole
                // allied population rather than betting on one ring's arc.
                var allied = runtime.Emplacements
                    .Where(t => t.EngineTeam == AimAssist.TeamOfPilot(0)).ToList();
                ctx.Check(allied.Count > 0, $"the piratezep's allied rings exist count={allied.Count}");
                if (allied.Count > 0)
                {
                    int AlliedShots() => allied.Sum(t => t.ShotsFired);
                    var zepPos = allied[0].WorldPosition;
                    target.PlaceHeld(zepPos + new Vector3(0f, -80f, 200f), zepPos);
                    Step(240);
                    ctx.Check(AlliedShots() == 0,
                        $"every allied ring holds fire on the player's own team shots={AlliedShots()}");
                    friend = BuildRig(ctx.PlaneName, 1, zepPos + new Vector3(50f, -80f, 200f), zepPos);
                    Step(600);
                    ctx.Check(AlliedShots() > 0,
                        $"…and the same rings engage a hostile pane there shots={AlliedShots()}");
                }

                // The aim assist's turret list now carries the emplacements too — the player's
                // lock-on sees world AA, dormant or not, until it dies.
                var candidates = new AimCandidateSet();
                live.CollectTurrets(candidates);
                ctx.Same(runtime.Count, candidates.Turrets.Count,
                    $"CollectTurrets feeds every emplacement to the candidate scan");

                // The kill switch: the emplacement's own destructible dies (the weapon-damage
                // path, ProbeRunner.TriggerDestroy = DamageAt), its healthy node hides, and the
                // gunner goes permanently quiet — ai.zrd HEALTH is authored-but-unread; the real
                // pool is the gamez destroy def's (aagun32: 30 hp).
                target.PlaceHeld(targetPos, targetPos + Vector3.Forward);
                int killed = ProbeRunner.TriggerDestroy(world.Runtime, "aagun32", out _);
                ctx.Check(killed > 0, $"aagun32's destructible died to weapon damage killed={killed}");
                int shotsAtDeath = aagun.ShotsFired;
                Step(240);
                ctx.Check(!aagun.Alive && aagun.ShotsFired == shotsAtDeath,
                    $"the dead emplacement goes quiet shots={aagun.ShotsFired}");
                candidates.Clear();
                live.CollectTurrets(candidates);
                ctx.Check(candidates.Turrets.Count(c => !c.Live) >= 1,
                    $"…and the candidate list reports it dead");
            }
            finally
            {
                pool?.Free();
                target?.Free();
                friend?.Free();
                zepBait?.Free();
                emplacements?.Free();
                textures.Dispose();
            }
        });

        // A second chapter's census (C4: the ground AA belt — aagun/tcargun/t_truck/8igun),
        // built and freed here; placement only, no firing. b_turret sites place too, but the
        // C3 balloon wiring bug (BL-348) and its fix stay out of this item.
        ctx.WithWorld("C4", collision: false, world =>
        {
            using var c4Textures = new TextureArchive(texturesPath);
            var live = new ProjectilePool(c4Textures, null, null);
            ctx.Host.AddChild(live);
            Session.TurretEmplacementRuntime? c4Emplacements = null;
            try
            {
                var runtime = c4Emplacements = new Session.TurretEmplacementRuntime(turretDefs, weapons,
                    (pattern, scope) => world.Runtime.FindNodes(pattern, scope), live,
                    world.Runtime.WorldRoot);
                var census = new List<string>();
                foreach (var g in runtime.Emplacements.GroupBy(t => t.Def.Title).OrderBy(g => g.Key))
                    census.Add($"{g.Key}={g.Count()}");
                ctx.Note($"C4 census: {string.Join(", ", census)}");
                ctx.Same(5, runtime.Emplacements.Count(t => t.Label.StartsWith("MSG_TUR_AAA@aagun")),
                    $"C4 places the five aagun emplacements");
                ctx.Same(3, runtime.Emplacements.Count(t => t.Label.StartsWith("MSG_TUR_TRAIN@tcargun")),
                    $"C4 places the three train-car guns");
                ctx.Same(1, runtime.Emplacements.Count(t => t.Label.StartsWith("MSG_TUR_8_INCH@8igun")),
                    $"C4 places the one 8-inch gun");
                ctx.Same(92, runtime.Count, $"C4's whole emplacement census");
                // The piratezep model (and its allied awake rings) is part of EVERY chapter's
                // world — the awake set is a world-model property, not a C1 fact.
                ctx.Same(15, runtime.AwakeCount, $"C4's awake set is the piratezep's own rings again");
            }
            finally
            {
                c4Emplacements?.Free();
                live.Free();
            }
        });
    }

    /// <summary>The AI actor seam (M4 A2), against real engine state on manual sim steps. A human
    /// rig is built and stepped first, so the AI plane demonstrably joins a RUNNING sim — the
    /// runtime-spawn half of the seam — with an <see cref="AiPilot"/> for input, no camera
    /// (<c>Setup(null)</c>), no HUD, no input devices, and <c>IsHumanPiloted</c> false. Pins:
    /// the spawned plane is present and registered as a hit target (its body answers the
    /// <c>player</c> surface id); it TICKS — displacement along its ordered course, altitude
    /// held; its orders are mutable mid-flight (a 90° retarget between steps is flown to);
    /// a round moves its part pools by the weapon's own ARMOR_DAMAGE; and sustained fire downs
    /// it — under the whole-vehicle kill rule (D14), the other zones pre-emptied as scaffolding —
    /// with the kill attributed to the human shooter's id through <c>Downed</c>.</summary>
    private static void AiActor(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        WeaponDef? gun = weapons.All.FirstOrDefault(w =>
            w.IsGun && w.ArmorDamage is > 0f && w.HealthDamage is > 0f);
        ctx.Check(gun != null, $"a gun with ARMOR_DAMAGE and HEALTH_DAMAGE exists in the data");
        if (gun == null)
            return;
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var nose = stats.DestroyableParts.FirstOrDefault(p =>
            p.Name.Equals("nose", System.StringComparison.OrdinalIgnoreCase));
        ctx.Check(nose is { Critical: true }, $"{ctx.PlaneName} carries a critical nose part");
        if (nose == null)
            return;

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? shooter = null;
        FlightController? ai = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            // The human rig first, and 120 sim steps before the AI exists: the spawn below is
            // a RUNTIME spawn into a sim already in motion, not part of a session build.
            var shooterModel = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            shooter = new FlightController
            {
                PlaneModel = shooterModel,
                Collider = PlaneCollider.Build(shooterModel),
                Damage = new PlaneDamage(stats.DestroyableParts),
                PlayerIndex = 0,
                Projectiles = live,
                UseKeyboard = false,
                AllowPause = false,
            };
            shooter.AddChild(shooterModel);
            shooter.Setup(new FlightModel(stats), ctx.Camera, new CamParams(),
                new Vector3(2000f, 500f, 0f), new Vector3(2000f, 500f, -1f));
            ctx.Host.AddChild(shooter);
            for (int i = 0; i < 120; i++)
            {
                live.SimStep(1f / 60f);
                shooter.SimStep(1f / 60f);
            }

            // The AI actor: an AiPilot ordered to hold the spawn course, a null camera, no HUD,
            // no devices — exactly what AiAircraftSpawner builds, on the suite's own stage.
            var spawnPos = new Vector3(0f, 500f, 0f);
            var pilot = AiPilot.HoldingCourse(spawnPos, spawnPos + Vector3.Forward);
            var aiModel = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            ai = new FlightController
            {
                PlaneModel = aiModel,
                Collider = PlaneCollider.Build(aiModel),
                Damage = new PlaneDamage(stats.DestroyableParts),
                PlayerIndex = AiAircraftSpawner.ShooterIdBase,
                IsHumanPiloted = false,
                Pilot = pilot,
                Projectiles = live,
                UseKeyboard = false,
                PadDevices = System.Array.Empty<int>(),
                AllowPause = false,
            };
            ai.AddChild(aiModel);
            ai.Setup(new FlightModel(stats), null, new CamParams(), spawnPos, spawnPos + Vector3.Forward);
            ctx.Host.AddChild(ai);

            // Present: in the tree, body built, registered as a hit target on the player id, and
            // visible to a physics ray where it spawned.
            ctx.Check(ai.IsInsideTree() && ai.Body != null,
                $"the AI plane is in the tree with an AircraftBody");
            if (ai.Body == null)
                return;
            ctx.Check(ProjectilePool.SurfaceIdOf(ai.Body) == SurfaceRegistry.Player,
                $"the AI body answers surface id {SurfaceRegistry.Player} (player) — weapon IMPACT rows fire on it");
            var space = live.GetWorld3D().DirectSpaceState;
            var probe = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                spawnPos + new Vector3(0f, 0f, -30f), spawnPos, CollisionLayers.WorldAndAircraft));
            ctx.Check(probe.Count > 0 && ReferenceEquals(probe["collider"].Obj, ai.Body),
                $"a physics ray at the spawned AI returns its body");

            // ⚠ The gunfire phases run at the SPAWN pose, before the plane flies anywhere: the
            // suite runs inside one engine frame, and a body MOVED after creation is invisible
            // to space queries until a physics flush this frame never gets (measured: the same
            // ray at the flown-to position hits nothing). Damage first, flight after.
            float armorDmg = gun.ArmorDamage!.Value;
            float healthDmg = gun.HealthDamage!.Value;
            float Combined() => ai!.Damage!.Parts.Values.Sum(p => p.Hp + p.Armor);
            var muzzle = new Transform3D(
                Basis.LookingAt(Vector3.Back, Vector3.Up), spawnPos + new Vector3(0f, 0f, -10f));
            void FireOne(int steps = 10)
            {
                live.Spawn(gun, muzzle, Vector3.Zero, shooter!.PlayerIndex);
                for (int i = 0; i < steps; i++)
                    live.SimStep(1f / 60f);
                live.Clear();
            }

            // Damageable: a registering round moves the pools by exactly the weapon's
            // ARMOR_DAMAGE (armor-first over full pools, so the combined total moves by it).
            float beforeHit = Combined();
            int tries = 0;
            while (Combined() >= beforeHit && tries < 5)
            {
                tries++;
                FireOne();
            }
            ctx.Check(Mathf.IsEqualApprox(beforeHit - Combined(), armorDmg),
                $"a round moves the AI's pools by the weapon's ARMOR_DAMAGE moved={beforeHit - Combined():0.##} expected={armorDmg:0.##} (rounds={tries})");

            // Killable, with the kill attributed: pre-empty the other zones with EXACT spends
            // (attribution scaffolding — an overkill spend would down the plane through the
            // whole-pool overflow before the AI's own burst; the spending itself is the
            // air-to-air suite's), then sustained fire on the same bearing exhausts the nose;
            // Downed reports (AI shooter id, human killer id).
            foreach (var p in ai.Damage!.Parts.Values)
            {
                if (p.Def != nose)
                {
                    ai.Damage.Apply(p.Def.Name, 0f, p.Armor);
                    ai.Damage.Apply(p.Def.Name, p.Hp, 0f);
                }
            }
            int? downedVictim = null, downedKiller = null;
            ai.Downed += (victim, killer) => { downedVictim = victim; downedKiller = killer; };
            int budget = (int)(nose.MaxArmor / armorDmg + nose.MaxHp / healthDmg) * 3 + 20;
            int fired = 0;
            while (!ai.Crashed && fired < budget)
            {
                fired++;
                FireOne(8);
            }
            ctx.Check(ai.Crashed, $"sustained fire downs the AI plane rounds={fired}/{budget}");
            ctx.Check(downedVictim == AiAircraftSpawner.ShooterIdBase,
                $"Downed reports the AI's own shooter id victim={downedVictim?.ToString() ?? "-"}");
            ctx.Check(downedKiller == shooter.PlayerIndex,
                $"…with the kill attributed to the human shooter killer={downedKiller?.ToString() ?? "-"}");

            // Back into the air for the flying half — Respawn repairs the wreck and re-arms the
            // same pilot; nothing below needs a physics query.
            ai.Respawn();
            void FlyAi(float seconds)
            {
                for (int i = 0; i < (int)(seconds * 60f); i++)
                {
                    live.SimStep(1f / 60f);
                    ai!.SimStep(1f / 60f);
                }
            }

            // Ticking, on its orders: 10 s of manual sim steps move it along the ordered course
            // (world -Z) at altitude, driven by AiPilot — no keyboard, no hold script.
            var before = ai.WorldPosition;
            FlyAi(10f);
            var disp = ai.WorldPosition - before;
            ctx.Check(disp.Length() > 300f,
                $"the AI plane flies under its pilot moved={disp.Length():0} m in 10 s");
            ctx.Check(disp.Normalized().Dot(Vector3.Forward) > 0.9f,
                $"…along its ordered course dot={disp.Normalized().Dot(Vector3.Forward):0.00}");
            ctx.Check(Mathf.Abs(ai.WorldPosition.Y - 500f) < 80f,
                $"…holding its ordered altitude y={ai.WorldPosition.Y:0}");

            // Orders are mutable mid-flight: retarget 90° between steps, no rebuild, no respawn.
            pilot.TargetHeadingDeg = 90f;
            FlyAi(25f);
            var noseDir = -ai.GlobalTransform.Basis.Z;
            float errDeg = Mathf.Wrap(90f - AiPilot.HeadingDegOf(noseDir), -180f, 180f);
            ctx.Check(Mathf.Abs(errDeg) < 10f,
                $"a mid-flight retarget is flown to err={errDeg:0.0}° after 25 s");
        }
        finally
        {
            pool?.Free();
            shooter?.Free();
            ai?.Free();
            textures.Dispose();
        }
    }

    /// <summary>The H22 targeting HUD on AI hostiles. Two halves: the pure selection
    /// (<see cref="VersusHud.NearestHostile"/> over a constructed candidate set, no scene) pins
    /// the filters (live only, AI-piloted only, the engine's either-side-neutral rejection,
    /// nearest wins) plus <see cref="VersusHud.HostileTag"/>; the in-engine half runs the
    /// matchless tracker against real spawned AI planes registered in a live pool: acquisition,
    /// the switch to a closer hostile, the crash drop (a crashed plane is listed but not live),
    /// and the empty-pool null. A hud built WITHOUT a pool (the VS constructor's default) never
    /// tracks, which is the seam keeping the golden VS output untouched.</summary>
    private static void HostileMarkerHud(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        // --- the pure selection, over a constructed candidate set (no tree, no pool) ---
        var pureNear = new FlightController { IsHumanPiloted = false };
        var pureFar = new FlightController { IsHumanPiloted = false };
        var pureDead = new FlightController { IsHumanPiloted = false };
        var pureHuman = new FlightController();
        try
        {
            int ownTeam = AimAssist.TeamOfPilot(0);
            var set = new AimCandidateSet();
            set.AddVehicle(new Vector3(0f, 0f, -50f), Vector3.Zero,
                AimAssist.TeamOfPilot(1), live: true, pureHuman); // a human: an opponent, never a hostile
            set.AddVehicle(new Vector3(0f, 0f, -100f), Vector3.Zero,
                AimAssist.TeamOfPilot(101), live: false, pureDead); // listed but not live (crashed)
            set.AddVehicle(new Vector3(0f, 0f, -10f), Vector3.Zero,
                AimAssist.NeutralTeam, live: true, pureNear); // neutral side rejects the pair
            set.AddVehicle(new Vector3(0f, 0f, -500f), Vector3.Zero,
                AimAssist.TeamOfPilot(100), live: true, pureNear);
            set.AddVehicle(new Vector3(0f, 0f, -2000f), Vector3.Zero,
                AimAssist.TeamOfPilot(102), live: true, pureFar);
            ctx.Check(ReferenceEquals(VersusHud.NearestHostile(Vector3.Zero, ownTeam, set), pureNear),
                $"the nearest LIVE AI hostile wins over a closer human, a closer dead plane and a closer neutral");
            ctx.Check(VersusHud.NearestHostile(Vector3.Zero, AimAssist.NeutralTeam, set) == null,
                $"a neutral own side targets nothing (the engine's either-side-0 rule)");
            ctx.Check(VersusHud.NearestHostile(Vector3.Zero, ownTeam, new AimCandidateSet()) == null,
                $"an empty scan tracks nothing");
            ctx.Check(VersusHud.HostileTag("ai1_player_fury") == "AI1"
                && VersusHud.HostileTag("bandit") == "BANDIT" && VersusHud.HostileTag("") == "AI",
                $"the marker tag is the name's first segment uppercased, 'AI' as the fallback");
        }
        finally
        {
            pureNear.Free();
            pureFar.Free();
            pureDead.Free();
            pureHuman.Free();
        }

        // --- the live tracker, against real AI planes registered in a real pool ---
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? ai1 = null, ai2 = null;
        VersusHud? hud = null, vsHud = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            FlightController SpawnAi(int index, Vector3 pos)
            {
                var model = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
                var fc = new FlightController
                {
                    PlaneModel = model,
                    Collider = PlaneCollider.Build(model),
                    PlayerIndex = AiAircraftSpawner.ShooterIdBase + index,
                    IsHumanPiloted = false,
                    Pilot = AiPilot.HoldingCourse(pos, pos + Vector3.Forward),
                    Projectiles = live,
                    UseKeyboard = false,
                    PadDevices = System.Array.Empty<int>(),
                    AllowPause = false,
                };
                fc.AddChild(model);
                fc.Setup(new FlightModel(stats), null, new CamParams(), pos, pos + Vector3.Forward);
                fc.Name = $"ai{index + 1}_{ctx.PlaneName}";
                ctx.Host.AddChild(fc);
                return fc;
            }

            ai1 = SpawnAi(0, new Vector3(0f, 500f, -800f));
            hud = VersusHud.BuildHostileTracker(0, ctx.Camera, live);
            hud.PlanePos = new Vector3(0f, 500f, 0f);
            hud.UpdateHostile();
            ctx.Check(ReferenceEquals(hud.TrackedHostile, ai1),
                $"a plain-session pane acquires the spawned AI plane tracked={hud.TrackedHostile?.Name ?? "-"}");

            // A closer hostile joining the pool takes the marker over on the next update; the
            // per-frame nearest re-select is also how generators' runtime spawns appear.
            ai2 = SpawnAi(1, new Vector3(0f, 500f, -300f));
            hud.UpdateHostile();
            ctx.Check(ReferenceEquals(hud.TrackedHostile, ai2),
                $"a closer hostile takes the marker over tracked={hud.TrackedHostile?.Name ?? "-"}");

            // A crashed hostile is still registered but no longer live: it drops cleanly and
            // the next nearest takes over; with every hostile down the pane tracks nothing.
            ai2.DebugForceCrash();
            hud.UpdateHostile();
            ctx.Check(ReferenceEquals(hud.TrackedHostile, ai1),
                $"a crashed hostile drops and the next nearest takes over tracked={hud.TrackedHostile?.Name ?? "-"}");
            ai1.DebugForceCrash();
            hud.UpdateHostile();
            ctx.Check(hud.TrackedHostile == null,
                $"with every hostile down the pane tracks nothing");

            // The VS constructor leaves HostilePool null (the rig assembler opts it in), so a
            // bare VS hud never tracks whatever the pool holds, which is the golden shots' path.
            ai1.Respawn();
            vsHud = VersusHud.Build(new VersusMatch(2, killTarget: 0, timeLimit: 0f), 0, ctx.Camera);
            vsHud.PlanePos = hud.PlanePos;
            vsHud.UpdateHostile();
            ctx.Check(vsHud.TrackedHostile == null,
                $"a hud built without a pool (the VS default) never tracks");
        }
        finally
        {
            hud?.Free();
            vsHud?.Free();
            ai1?.Free();
            ai2?.Free();
            pool?.Free();
            textures.Dispose();
        }
    }

    /// <summary>The D14 AI gunnery, on real engine state: an AI-piloted, stock-armed plane HELD
    /// at fixed poses (the weapon-lab pin — held rigs fire through the normal path) against a
    /// parked hostile target. Pins: nearest-hostile acquisition into the gunner's MUTABLE target
    /// field; the quick-draw gate (a beam-ish bearing is refused at rating 1's 50° cone and
    /// taken at rating 9's 89°); the ±11° forward gun cone as a hard fire gate (nose 30° off the
    /// bearing = no fire, whatever quick draw says); dead-eye scatter as a per-shot cone whose
    /// interpolated rating-1 angle (4.0°) lands measurably fewer hits than rating 9's (1.45°) at
    /// fixed range, with the rounds under the AI's own shooter id and none on its own airframe;
    /// the kill attributed to the AI id through Downed; and the IsHumanPiloted assist exclusion
    /// A/B'd in place — the same off-boresight geometry misses as an AI and hits the moment the
    /// same rig is flagged human (the assist snapping on).
    ///
    /// <para>⚠ INSTR-13: the target is parked at its spawn pose and never moved — a body moved
    /// within the suite's single frame is invisible to the rounds' space queries.</para></summary>
    private static void AiGunnery(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var skills = AiSkills.Load(ctx.ZrdrPath);
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
        if (stock == null)
            return;

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? target = null;
        FlightController? ai = null;
        FlightController? rival = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            // The parked target: a human-index rig at the origin column, PINNED at zero speed so
            // the gunner's intercept sees a static target (a never-stepped rig would otherwise
            // report its spawn speed and every lead would miss a plane that is not moving).
            var targetPos = new Vector3(0f, 500f, 0f);
            var targetModel = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            target = new FlightController
            {
                PlaneModel = targetModel,
                Collider = PlaneCollider.Build(targetModel),
                Damage = new PlaneDamage(stats.DestroyableParts),
                PlayerIndex = 0,
                Projectiles = live,
                UseKeyboard = false,
                PadDevices = System.Array.Empty<int>(),
                AllowPause = false,
            };
            target.AddChild(targetModel);
            target.Setup(new FlightModel(stats), ctx.Camera, new CamParams(), targetPos, targetPos + Vector3.Forward);
            ctx.Host.AddChild(target);
            target.PlaceHeld(targetPos, targetPos + Vector3.Forward);

            // The AI shooter: stock-armed (the gunner fires through FireControl, not scripted
            // spawns), AiPilot + AiGunner, held at each phase's pose. Its own seeded scatter rng
            // makes every volley reproducible.
            var gunner = new AiGunner(new RandomNumberGenerator { Seed = 20260813 })
            {
                DeadEyeAngleDeg = skills.DeadEyeAngleDeg(9),
                QuickDrawAngleDeg = skills.QuickDrawAngleDeg(1),
            };
            var aiPos = targetPos + new Vector3(0f, 0f, 500f); // dead astern of the target's tail
            var pilot = AiPilot.HoldingCourse(aiPos, targetPos);
            pilot.Gunner = gunner;
            var aiModel = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            ai = new FlightController
            {
                PlaneModel = aiModel,
                Collider = PlaneCollider.Build(aiModel),
                Damage = new PlaneDamage(stats.DestroyableParts),
                PlayerIndex = AiAircraftSpawner.ShooterIdBase,
                IsHumanPiloted = false,
                Pilot = pilot,
                Projectiles = live,
                UseKeyboard = false,
                PadDevices = System.Array.Empty<int>(),
                AllowPause = false,
            };
            ai.AddChild(aiModel);
            ai.Loadout = Loadout.Bind(stock, aiModel, weapons);
            ai.Setup(new FlightModel(stats), null, new CamParams(), aiPos, targetPos);
            ctx.Host.AddChild(ai);
            ai.Held = true;
            ai.PlaceHeld(aiPos, targetPos);

            var gun = ai.Loadout.FirableGuns.First();
            float armorDmg = gun.Weapon.ArmorDamage ?? 0f;
            ctx.Check(armorDmg > 0f && Mathf.IsEqualApprox(armorDmg, gun.Weapon.HealthDamage ?? -1f),
                $"the stock gun's two damage magnitudes are equal ({gun.Weapon.Id}, {armorDmg:0.#}) — the hit counter's precondition");
            float Combined(FlightController rig) => rig.Damage!.Parts.Values.Sum(p => p.Hp + p.Armor);
            bool Pristine(FlightController rig) => rig.Damage!.Parts.Values.All(
                p => p.Hp >= p.Def.MaxHp && p.Armor >= p.Def.MaxArmor);
            void Step(int frames)
            {
                for (int i = 0; i < frames; i++)
                {
                    ai!.SimStep(1f / 60f);
                    live.SimStep(1f / 60f);
                }
            }

            // --- acquisition: the nearest hostile lands in the gunner's mutable target field.
            Step(1);
            ctx.Check(ReferenceEquals(gunner.Target, target),
                $"the gunner auto-acquires the nearest hostile aircraft");
            // Mutable state: cleared orders re-acquire; an explicit assignment stands.
            gunner.Target = null;
            Step(1);
            ctx.Check(ReferenceEquals(gunner.Target, target), $"a cleared target re-acquires next tick");

            // --- the quick-draw gate: 80° off the target's tail axis (a beam-ish shot). At
            // rating 1 the 50° cone refuses it; at rating 9 the 89° cone takes it.
            var beamPos = targetPos + new Vector3(0.9848f, 0f, 0.1736f) * 500f; // 80° off +Z
            ai.PlaceHeld(beamPos, targetPos);
            int ammoAtBeam = gun.Ammo;
            Step(60);
            ctx.Check(!gunner.WantsFire && gun.Ammo == ammoAtBeam,
                $"a beam-ish shot is refused at quick-draw rating 1 (50°) rounds={ammoAtBeam - gun.Ammo}");
            gunner.QuickDrawAngleDeg = skills.QuickDrawAngleDeg(9);
            Step(60);
            ctx.Check(gun.Ammo < ammoAtBeam,
                $"the same bearing is taken at rating 9 (89°) rounds={ammoAtBeam - gun.Ammo}");

            // --- the forward gun cone is a hard gate: nose 30° off the bearing, quick draw
            // willing — no fire.
            ai.PlaceHeld(beamPos, beamPos + (targetPos - beamPos).Normalized()
                .Rotated(Vector3.Up, Mathf.DegToRad(30f)) * 100f);
            int ammoAtOffBore = gun.Ammo;
            Step(60);
            ctx.Check(!gunner.WantsFire && gun.Ammo == ammoAtOffBore,
                $"outside the ±11° forward cone the AI holds fire (nose 30° off)");

            // --- dead-eye scatter, skill 1 vs 9: same fixed geometry (the high rear quarter at
            // ~212 m, where the planform presents real area — dead astern the airframe is
            // edge-on and both cones mostly miss, drowning the difference), a fixed round
            // budget each, hits counted off the damage ledger. The rating-1 cone (4.0°) must
            // land measurably fewer than rating 9's (1.45°).
            ai.PlaceHeld(targetPos + new Vector3(0f, 150f, 150f), targetPos);
            (int Rounds, int Hits) Volley(float deadEyeDeg, int roundCap)
            {
                gunner!.DeadEyeAngleDeg = deadEyeDeg;
                gunner.AutoTarget = true;
                gunner.Target = target;
                target!.Respawn();
                target.PlaceHeld(targetPos, targetPos + Vector3.Forward);
                live.Clear(); // no stragglers from an earlier phase land on this ledger
                float before = Combined(target);
                int ammoBefore = gun.Ammo;
                for (int guard = 0; ammoBefore - gun.Ammo < roundCap && guard < 1800; guard++)
                    Step(1);
                gunner.AutoTarget = false;
                gunner.Target = null; // cease fire; let the last rounds land
                Step(90);
                return (ammoBefore - gun.Ammo, (int)Mathf.Round((before - Combined(target)) / armorDmg));
            }

            var low = Volley(skills.DeadEyeAngleDeg(1), 30);
            var high = Volley(skills.DeadEyeAngleDeg(9), 30);
            ctx.Note($"dead-eye: skill 1 hit {low.Hits}/{low.Rounds}, skill 9 hit {high.Hits}/{high.Rounds} from the high rear quarter at ~212 m");
            // Fixed seed, so the measured gap (13 vs 6 of 30) is exact; the +4 margin is what
            // "measurably" means here, not a statistical bound.
            ctx.Check(high.Hits >= low.Hits + 4,
                $"skill 9's tighter cone out-hits skill 1 ({high.Hits}/{high.Rounds} vs {low.Hits}/{low.Rounds})");
            ctx.Check(high.Hits >= 1 && low.Hits < low.Rounds,
                $"both regimes are real: skill 9 lands rounds, skill 1 scatters some wide");
            ctx.Check(Pristine(ai) && !ai.Crashed, $"the AI's own airframe took none of its own fire");

            // --- the kill, attributed to the AI's shooter id, from dead astern at 150 m (the
            // bearing whose first box is the tail). Scaffolding per the whole-vehicle rule: the
            // zones this bearing cannot reach are pre-emptied; the AI's own fire finishes the
            // plane.
            gunner.AutoTarget = true;
            gunner.DeadEyeAngleDeg = skills.DeadEyeAngleDeg(9);
            ai.PlaceHeld(targetPos + new Vector3(0f, 0f, 150f), targetPos);
            target.Respawn();
            target.PlaceHeld(targetPos, targetPos + Vector3.Forward);
            live.Clear();
            foreach (var p in target.Damage!.Parts.Values)
            {
                if (!p.Def.Name.Equals("tail", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Exact spends: an overkill spend would kill through the whole-pool overflow.
                    target.Damage.Apply(p.Def.Name, 0f, p.Armor);
                    target.Damage.Apply(p.Def.Name, p.Hp, 0f);
                }
            }
            int? downedVictim = null, downedKiller = null;
            target.Downed += (victim, killer) => { downedVictim = victim; downedKiller = killer; };
            for (int guard = 0; !target.Crashed && guard < 3600; guard++)
                Step(1);
            ctx.Check(target.Crashed, $"the AI's own gunnery downs the target");
            ctx.Check(downedVictim == target.PlayerIndex && downedKiller == ai.PlayerIndex,
                $"the kill is attributed to the AI's shooter id killer={downedKiller?.ToString() ?? "-"}");

            // --- the IsHumanPiloted assist exclusion, A/B'd in place: nose 3° off the bearing at
            // 400 m, gunner disarmed, trigger held raw (--fire's AutoFire). As an AI the rounds
            // leave along the muzzle axis and ALL miss; the same rig flagged human gets the
            // assist, which snaps onto the target and lands hits. First testable here: before
            // D14 no AI ever pulled a trigger.
            pilot.Gunner = null;
            ai.AutoFire = true;
            target.Respawn();
            target.PlaceHeld(targetPos, targetPos + Vector3.Forward);
            live.Clear(); // the kill phase's last rounds must not land on this ledger
            var nearPos = targetPos + new Vector3(0f, 0f, 400f);
            var offDir = (targetPos - nearPos).Normalized().Rotated(Vector3.Up, Mathf.DegToRad(3f));
            ai.PlaceHeld(nearPos, nearPos + offDir * 100f);
            float pristineCombined = Combined(target);
            Step(180);
            ctx.Check(Mathf.IsEqualApprox(Combined(target), pristineCombined),
                $"an AI plane gets NO aim assist: every off-boresight round misses");
            ai.IsHumanPiloted = true;
            Step(180);
            ctx.Check(Combined(target) < pristineCombined,
                $"the same geometry flagged human is assisted onto the target moved={pristineCombined - Combined(target):0.##}");

            // --- D12 ranked acquisition: a hostile pair at equal geometry (same distance,
            // same bearing/altitude/facing arms). The human-piloted target carries the decoded
            // 0.7 base weight and out-ranks the AI rival; a primary_target assignment
            // overrides the ranking; a candidate beyond the activation radius scores the
            // engine's 1e21 and is never picked.
            ai.IsHumanPiloted = false;
            ai.AutoFire = false;
            pilot.Gunner = gunner;
            gunner.AutoTarget = true;
            gunner.Target = null;
            target.Respawn();
            target.PlaceHeld(targetPos, targetPos + Vector3.Forward);
            var shooterPos = targetPos + new Vector3(0f, 0f, 800f);
            ai.PlaceHeld(shooterPos, targetPos);
            // Same 800 m ring, 14.5° off the shooter's nose, same altitude, nose away.
            var rivalPos = targetPos + new Vector3(200f, 0f, 800f - Mathf.Sqrt(800f * 800f - 200f * 200f));
            var rivalModel = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            rival = new FlightController
            {
                PlaneModel = rivalModel,
                Collider = PlaneCollider.Build(rivalModel),
                PlayerIndex = AiAircraftSpawner.ShooterIdBase + 1,
                IsHumanPiloted = false,
                Projectiles = live,
                UseKeyboard = false,
                PadDevices = System.Array.Empty<int>(),
                AllowPause = false,
            };
            rival.AddChild(rivalModel);
            rival.Setup(new FlightModel(stats), null, new CamParams(), rivalPos,
                rivalPos + Vector3.Forward);
            rival.Name = "rival_hostile";
            ctx.Host.AddChild(rival);
            rival.PlaceHeld(rivalPos, rivalPos + Vector3.Forward);

            Step(1);
            ctx.Check(ReferenceEquals(gunner.Target, target),
                $"equal geometry: the player's 0.7 weight out-ranks the AI rival (360 rank units)");
            gunner.Target = null;
            gunner.PrimaryTargetName = "rival_hostile";
            Step(1);
            ctx.Check(ReferenceEquals(gunner.Target, rival),
                $"an assigned primary_target overrides the ranking");
            gunner.Target = null;
            gunner.PrimaryTargetName = "player";
            Step(1);
            ctx.Check(ReferenceEquals(gunner.Target, target),
                $"primary_target 'player' resolves to the human-piloted aircraft");
            gunner.PrimaryTargetName = null;
            gunner.Target = null;
            target.PlaceHeld(targetPos + new Vector3(0f, 0f, -2500f),
                targetPos + new Vector3(0f, 0f, -2600f));
            Step(1);
            ctx.Check(ReferenceEquals(gunner.Target, rival),
                $"beyond the activation radius the player scores 1e21 and the rival is picked");
        }
        finally
        {
            pool?.Free();
            target?.Free();
            ai?.Free();
            rival?.Free();
            textures.Dispose();
        }
    }

    /// <summary>The D11 mode machine on a real flying AI rig. The reaction rolls are scripted by
    /// pinning the shipped chance fields to 0/1 (the machine's own rng stays seeded), and the
    /// steady-hand entry rides a REAL projectile hit through <c>TakeProjectileHit</c> — the same
    /// call the pool makes — so the hit-path wiring is what is exercised, not the machine API.
    ///
    /// <para>⚠ INSTR-13: no phase here queries physics at a flown-to position — the hit is
    /// delivered by the direct pool entry point, and the terrain probe is an injected flag, so
    /// the plane may genuinely fly between phases.</para></summary>
    private static void AiModes(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var skills = AiSkills.Load(ctx.ZrdrPath);
        var library = Maneuvers.Load(ctx.ZrdrPath);
        WeaponDef? gun = weapons.All.FirstOrDefault(w =>
            w.IsGun && w.ArmorDamage is > 0f && w.HealthDamage is > 0f);
        ctx.Check(gun != null, $"a gun with authored damage exists in the data");
        if (gun == null)
            return;

        // The shipped range gates the machine runs on.
        ctx.Check(Mathf.IsEqualApprox(skills.MinAiActiveDist, 2000f),
            $"player.json min_ai_active_dist is the decoded 2000 m got={skills.MinAiActiveDist:0}");
        ctx.Check(Mathf.IsEqualApprox(stats.AiAttackRange, 2000f)
            && Mathf.IsEqualApprox(stats.AiReturnRange, 1200f),
            $"vehicle.json attack/return_range are the decoded 2000/1200 m got={stats.AiAttackRange:0}/{stats.AiReturnRange:0}");

        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? target = null;
        FlightController? ai = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            // The hostile: a parked human-index rig the machine can activate against.
            var targetPos = new Vector3(0f, 500f, 0f);
            var targetModel = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            target = new FlightController
            {
                PlaneModel = targetModel,
                Collider = PlaneCollider.Build(targetModel),
                PlayerIndex = 0,
                Projectiles = live,
                UseKeyboard = false,
                PadDevices = System.Array.Empty<int>(),
                AllowPause = false,
            };
            target.AddChild(targetModel);
            target.Setup(new FlightModel(stats), ctx.Camera, new CamParams(), targetPos,
                targetPos + Vector3.Forward);
            ctx.Host.AddChild(target);
            target.PlaceHeld(targetPos, targetPos + Vector3.Forward);

            // The AI: pilot + gunner + machine, spawned OUTSIDE the activation radius flying
            // straight at the hostile. The terrain probe is injected before the controller can
            // wire its own, so avoid-crash is a scripted flag here.
            bool terrainBlocked = false;
            var machine = new AiModeMachine(new System.Random(11))
            {
                ActivationRange = skills.MinAiActiveDist,
                AttackRange = stats.AiAttackRange,
                ReturnRange = stats.AiReturnRange,
                SteadyHandChance = 0f, // scripted per phase; approach fire reactions off
                SixthSenseChance = 1f,
                StunRecoveryIntervalS = skills.At("stun_recovery_interval", 5),
                NaturalTouch = 1,
                Library = library,
                ProbeBlocked = (_, _) => terrainBlocked,
            };
            var transitions = new List<string>();
            machine.ModeChanged += (from, to, _) =>
                transitions.Add($"{AiModeMachine.NameOf(from)}>{AiModeMachine.NameOf(to)}");
            string? lastRoll = null;
            machine.RollLogged += line => lastRoll = line;

            var aiPos = targetPos + new Vector3(0f, 0f, 2600f); // outside the 2000 m radius
            var pilot = AiPilot.HoldingCourse(aiPos, targetPos);
            pilot.Gunner = new AiGunner(new RandomNumberGenerator { Seed = 20260813 })
            {
                DeadEyeAngleDeg = skills.DeadEyeAngleDeg(5),
                QuickDrawAngleDeg = skills.QuickDrawAngleDeg(5),
            };
            pilot.Machine = machine;
            var aiModel = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            ai = new FlightController
            {
                PlaneModel = aiModel,
                Collider = PlaneCollider.Build(aiModel),
                Damage = new PlaneDamage(stats.DestroyableParts),
                PlayerIndex = AiAircraftSpawner.ShooterIdBase,
                IsHumanPiloted = false,
                Pilot = pilot,
                Projectiles = live,
                UseKeyboard = false,
                PadDevices = System.Array.Empty<int>(),
                AllowPause = false,
            };
            ai.AddChild(aiModel);
            ai.Loadout = Loadout.Bind(StockLoadouts.Load().All.Values
                .First(d => d.Model == ctx.PlaneName), aiModel, weapons);
            ai.Setup(new FlightModel(stats), null, new CamParams(), aiPos, targetPos);
            ctx.Host.AddChild(ai);

            void Step(int frames)
            {
                for (int i = 0; i < frames; i++)
                {
                    ai!.SimStep(1f / 60f);
                    live.SimStep(1f / 60f);
                }
            }
            float Dist() => ai!.WorldPosition.DistanceTo(targetPos);

            // --- activation: patrol until the approach carries it inside the radius, then
            // pursue, announced in the decoded vocabulary.
            ctx.Check(machine.Mode == AiMode.Patrol, $"the machine starts on patrol");
            Step(1);
            ctx.Check(machine.Mode == AiMode.Patrol && Dist() > 2000f,
                $"outside min_ai_active_dist it stays on patrol d={Dist():0} m");
            int budget = 60 * 60;
            while (machine.Mode == AiMode.Patrol && budget-- > 0)
                Step(1);
            ctx.Check(machine.Mode == AiMode.Pursue,
                $"the approach activates it into pursue d={Dist():0} m");
            ctx.Check(Dist() <= 2010f, $"…at the activation radius, not before d={Dist():0} m");
            ctx.Check(transitions.Contains("patrol>pursue"),
                $"…logged as patrol>pursue transitions=[{string.Join(" ", transitions)}]");

            // --- a scripted FAILED steady-hand roll on a real projectile hit: the decoded
            // reaction, through the same TakeProjectileHit the pool calls.
            machine.SteadyHandChance = 1f;
            ai.TakeProjectileHit(gun, ai.WorldPosition + new Vector3(2f, 0f, 0f), "fuselage", 0);
            ctx.Check(lastRoll != null && lastRoll.Contains("steady hand test failed. Evading."),
                $"the hit rolls steady hand in the engine's vocabulary roll={lastRoll}");
            ctx.Check(machine.Mode == AiMode.EvasiveManeuver && machine.Executor != null,
                $"the failed test breaks off into an evasive maneuver mode={AiModeMachine.NameOf(machine.Mode)}");
            var flown = machine.Executor?.Maneuver;
            ctx.Check(flown != null && flown.EligibleFor(machine.NaturalTouch),
                $"…an eligible library entry name={flown?.Name} difficulty={flown?.Difficulty}/{machine.NaturalTouch}");
            machine.SteadyHandChance = 0f; // stray hits must not re-trigger mid-phase

            // --- the maneuver plays to Done and the machine returns to a flyable mode.
            budget = 60 * 60;
            while (machine.Mode == AiMode.EvasiveManeuver && budget-- > 0)
                Step(1);
            ctx.Check(machine.Mode is AiMode.Pursue or AiMode.Patrol,
                $"the maneuver returns to the prior mode mode={AiModeMachine.NameOf(machine.Mode)}");
            ctx.Check(machine.Executor == null, $"…and the executor is released");

            // --- a scripted FAILED sixth-sense roll stuns: gunner silent, then recovery after
            // stun_recovery_interval (the shipped value at rating 5).
            machine.Enter(AiMode.Pursue, "test: rejoin");
            machine.SixthSenseChance = 0f;
            machine.NotifyTargetEvaded();
            ctx.Check(machine.Mode == AiMode.Stunned,
                $"a failed sixth-sense roll stuns roll={lastRoll}");
            ctx.Check(lastRoll != null && lastRoll.Contains("Sixth sense test failed; AI now stunned."),
                $"…in the engine's vocabulary");
            Step(6);
            ctx.Check(!pilot.Gunner.WantsFire, $"the gunner is silent while stunned");
            float stunS = machine.StunRecoveryIntervalS;
            Step((int)(stunS * 60f) - 30);
            ctx.Check(machine.Mode == AiMode.Stunned,
                $"still stunned inside stun_recovery_interval ({stunS:0.0} s)");
            Step(60);
            ctx.Check(machine.Mode == AiMode.Pursue,
                $"…and recovered to the prior mode after it mode={AiModeMachine.NameOf(machine.Mode)}");

            // --- avoid crash: a blocked probe overrides with a climb-out, a cleared one
            // releases back.
            machine.SixthSenseChance = 1f;
            float yBefore = ai.WorldPosition.Y;
            terrainBlocked = true;
            Step(30);
            ctx.Check(machine.Mode == AiMode.AvoidCrash,
                $"a blocked terrain probe takes the mode mode={AiModeMachine.NameOf(machine.Mode)}");
            ctx.Check(machine.ClimbOutAltitude > yBefore,
                $"…ordering a climb-out to {machine.ClimbOutAltitude:0} m from {yBefore:0} m");
            Step(240);
            ctx.Check(ai.WorldPosition.Y > yBefore,
                $"the plane is climbing out y={ai.WorldPosition.Y:0} from {yBefore:0}");
            terrainBlocked = false;
            Step(120);
            ctx.Check(machine.Mode != AiMode.AvoidCrash,
                $"a cleared probe releases the override mode={AiModeMachine.NameOf(machine.Mode)}");

            // --- lay off (D15, the rubber-band assist). Entry A/B on fixed geometry through
            // the machine's own tick: a chasing human 600 m dead astern enters lay off with
            // the assist on, and never with --no-assist's switch off.
            machine.Enter(AiMode.Pursue, "test: rejoin for lay off");
            var aiPos2 = ai.WorldPosition;
            var ownVel = new Vector3(0f, 0f, -100f);               // flying -Z
            var pursuerPos = aiPos2 + new Vector3(0f, 0f, 600f);   // 600 m dead astern
            var pursuerVel = new Vector3(0f, 0f, -80f);            // giving chase
            // Entry needs the geometry SUSTAINED (LayOffSustainS), so both arms tick through
            // the window; one passing frame must not enter (the misfire regression).
            int sustainFrames = (int)(AiModeMachine.LayOffSustainS * 60f) + 2;
            machine.AssistEnabled = false;
            for (int i = 0; i < sustainFrames; i++)
                machine.Update(aiPos2, ownVel, pursuerPos, null, 1f / 60f, pursuerVel, targetIsHuman: true);
            ctx.Check(machine.Mode == AiMode.Pursue,
                $"--no-assist: the same pursued geometry never enters lay off mode={AiModeMachine.NameOf(machine.Mode)}");
            machine.AssistEnabled = true;
            machine.Update(aiPos2, ownVel, pursuerPos, null, 1f / 60f, pursuerVel, targetIsHuman: true);
            ctx.Check(machine.Mode == AiMode.Pursue,
                $"one passing frame does not enter lay off mode={AiModeMachine.NameOf(machine.Mode)}");
            for (int i = 0; i < sustainFrames; i++)
                machine.Update(aiPos2, ownVel, pursuerPos, null, 1f / 60f, pursuerVel, targetIsHuman: true);
            ctx.Check(machine.Mode == AiMode.LayOff,
                $"assist on: a chasing human fallen 600 m behind enters lay off mode={AiModeMachine.NameOf(machine.Mode)}");
            ctx.Check(transitions.Contains("pursue>lay off"),
                $"…logged as pursue>lay off transitions=[{string.Join(" ", transitions)}]");

            // The observable assist, through the live pilot: the throttle eases off flat-out
            // (pursue's is 1) and the gunner's trigger is held for the whole dwell.
            Step(30);
            ctx.Check(machine.Mode == AiMode.LayOff,
                $"the anti-chatter hold keeps the mode mode={AiModeMachine.NameOf(machine.Mode)}");
            ctx.Check(pilot.Throttle < 1f,
                $"the throttle is eased off flat-out throttle={pilot.Throttle:0.00}");
            ctx.Check(!pilot.Gunner.WantsFire, $"fire is held while laying off");

            // The parked target is not actually chasing, so once the hold expires the machine
            // releases back to pursue and the throttle runs flat out again.
            Step(150);
            ctx.Check(machine.Mode == AiMode.Pursue,
                $"a non-pursuing target releases lay off after the hold mode={AiModeMachine.NameOf(machine.Mode)}");
            Step(5);
            ctx.Check(Mathf.IsEqualApprox(pilot.Throttle, 1f),
                $"…and pursue runs flat out again throttle={pilot.Throttle:0.00}");

            ctx.Note($"transitions: {string.Join(" ", transitions)}");
        }
        finally
        {
            pool?.Free();
            target?.Free();
            ai?.Free();
            textures.Dispose();
        }
    }

    /// <summary>The B8 voice runtime, against the real soundsh archive. Reproduces the session's
    /// own lifecycle in order: resolve the chain (accent 12 → VO id 2), prewarm that one pilot's
    /// clips, retire the loader exactly as <c>WorldSession.Build</c> does, then prove a resolved
    /// line still plays while a def never prewarmed returns null. The source-following one-shot is
    /// asserted by position only; whether anything is audible is the user's half
    /// (<c>docs/verification.md</c>, "What this project cannot verify itself"); the player node's
    /// tracked <c>GlobalPosition</c> is what IS assertable headless. Closes with the measured cost
    /// of prewarming the ENTIRE voice bank, the number that justifies the roster-subset choice.</summary>
    private static void VoiceRuntime(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"shared zrdr");
        ctx.RequireData(ctx.SoundsPath, $"sound archive (soundsh)");
        var defs = SoundDefs.Load(ctx.ZrdrPath);
        var groups = SoundDefs.LoadGroups(ctx.ZrdrPath);
        var voice = new CombatVoice(defs, groups, CombatVoice.LoadAccents(ctx.ZrdrPath));
        ctx.Same(35, voice.AccentIds.Count, $"voice.zrd accent rows");

        using var archive = new SoundArchive(ctx.SoundsPath);
        WorldSounds? sounds = null;
        Node3D? mover = null;
        try
        {
            sounds = new WorldSounds(defs, groups)
            {
                Loader = (d, warn) => archive.Find(d.WavName, d.Looped, warn),
            };
            ctx.Host.AddChild(sounds);

            // The chain's worked example: accent 12 is a single-id pool, VO id 2 (the pilot with
            // the full bearing set). Prewarm that pilot exactly as a mission roster would.
            int? pilot = voice.PilotFor(12, new System.Random(1));
            ctx.Check(pilot == 2, $"accent 12 resolves to VO id 2 got={pilot?.ToString() ?? "null"}");
            var subset = voice.PrewarmNames(new[] { 12 });
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int decoded = sounds.Prewarm(subset);
            sw.Stop();
            ctx.Check(decoded >= 70, $"the accent-12 subset decodes names={subset.Count} decoded={decoded}");
            ctx.Note($"accent-12 prewarm: {subset.Count} defs, {decoded} streams, {sw.ElapsedMilliseconds} ms");
            sounds.Loader = null;   // the session's build scope closing (WorldSession.Build)

            string? playable = voice.PlayableFor(2, "DI-LowDmg");
            ctx.Check(playable == "snd_DI-LowDmg-A_id2_random",
                $"DI-LowDmg resolves to the shipped variant group got={playable}");
            string? bearing = voice.PlayableForTrigger(2, 6);   // WA-Enemy-3H
            ctx.Check(bearing != null && sounds.HasStream(bearing),
                $"the bearing clip's stream survived the loader retirement name={bearing}");

            // A prewarmed line plays from a MOVING source and tracks it across ticks.
            mover = new Node3D();
            ctx.Host.AddChild(mover);
            mover.GlobalPosition = new Vector3(100f, 200f, 300f);
            string? resolved = sounds.PlayOneShot(playable!, mover, new System.Random(2));
            ctx.Check(resolved != null && resolved.StartsWith("snd_id2_DI-LowDmg"),
                $"a prewarmed voice line plays after the archive closed resolved={resolved}");
            var player = LastOneShotPlayer(sounds);
            ctx.Check(player != null && player.GlobalPosition.DistanceTo(mover.GlobalPosition) < 0.01f,
                $"the one-shot starts at its source pos={player?.GlobalPosition}");
            mover.GlobalPosition = new Vector3(-450f, 60f, 1200f);
            sounds.Tick();
            ctx.Check(player!.GlobalPosition.DistanceTo(mover.GlobalPosition) < 0.01f,
                $"the one-shot follows the moved source pos={player.GlobalPosition}");
            var lastPos = mover.GlobalPosition;
            mover.Free();
            mover = null;
            sounds.Tick();
            ctx.Check(GodotObject.IsInstanceValid(player) && player.GlobalPosition.DistanceTo(lastPos) < 0.01f,
                $"a freed source leaves the line finishing at its last position");

            // The positional overload is untouched, and a def never prewarmed is null once the
            // loader is gone: the exact failure the prewarm exists to prevent.
            ctx.Check(sounds.PlayOneShot("snd_id26_TA-SucShk-A", Vector3.Zero, new System.Random(3)) == null,
                $"an unprewarmed pilot's line stays null after the archive closed");

            // The cost of prewarm-everything, measured on a fresh archive so nothing is cached:
            // the number the roster-subset strategy is justified against.
            using var fresh = new SoundArchive(ctx.SoundsPath);
            long bytes = 0;
            int ok = 0, absent = 0;
            sw.Restart();
            foreach (var name in voice.AllClipNames())
            {
                var def = defs[name];
                if (fresh.Find(def.WavName, def.Looped, warn: false) is { } stream)
                {
                    ok++;
                    bytes += stream.Data.Length;
                }
                else
                {
                    absent++;
                }
            }
            sw.Stop();
            ctx.Note($"full voice bank: {voice.AllClipNames().Count} defs, {ok} decoded ({absent} defs without a WAV), {bytes / (1024.0 * 1024.0):0.0} MB PCM, {sw.ElapsedMilliseconds} ms; why the session prewarms the roster subset");
        }
        finally
        {
            mover?.Free();
            if (sounds != null)
            {
                sounds.FlushOneShots();
                sounds.Free();
            }
        }
    }

    /// <summary>The E16 dispatch on a live AI aircraft, over the same B8 lifecycle the session
    /// runs (prewarm the accent's clips, retire the loader, play after the archive is closed).
    /// The talker chance is pinned to 1 so the assertions are about the dispatch rules, not the
    /// dice; audibility itself is the user's half (docs/verification.md, "What this project
    /// cannot verify itself") — what IS assertable is the dispatch decision, the resolved clip
    /// name and the PlayOneShot call.</summary>
    private static void AiVoice(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(ctx.SoundsPath, $"sound archive (soundsh)");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var defs = SoundDefs.Load(ctx.ZrdrPath);
        var groups = SoundDefs.LoadGroups(ctx.ZrdrPath);
        var voice = new CombatVoice(defs, groups, CombatVoice.LoadAccents(ctx.ZrdrPath));
        WeaponDef? gun = weapons.All.FirstOrDefault(w =>
            w.IsGun && w.ArmorDamage is > 0f && w.HealthDamage is > 0f);
        ctx.Check(gun != null && stats.DestroyableParts.Count > 0,
            $"a damaging gun and destroyable parts exist in the data");
        if (gun == null)
            return;

        var textures = new TextureArchive(texturesPath);
        using var archive = new SoundArchive(ctx.SoundsPath);
        WorldSounds? sounds = null;
        Session.AiVoiceRuntime? runtime = null;
        FlightController? ai = null;
        try
        {
            // The session lifecycle: prewarm accent 12's pilot (VO id 2, the full clip set),
            // then retire the loader as WorldSession.Build does.
            sounds = new WorldSounds(defs, groups)
            {
                Loader = (d, warn) => archive.Find(d.WavName, d.Looped, warn),
            };
            ctx.Host.AddChild(sounds);
            sounds.Prewarm(voice.PrewarmNames(new[] { 12 }));
            sounds.Loader = null;

            runtime = new Session.AiVoiceRuntime(voice, sounds, new System.Random(5));
            ctx.Host.AddChild(runtime);

            var aiModel = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            ai = new FlightController
            {
                PlaneModel = aiModel,
                Collider = PlaneCollider.Build(aiModel),
                Damage = new PlaneDamage(stats.DestroyableParts),
                PlayerIndex = AiAircraftSpawner.ShooterIdBase,
                IsHumanPiloted = false,
                Pilot = new AiPilot(),
                UseKeyboard = false,
                PadDevices = System.Array.Empty<int>(),
                AllowPause = false,
            };
            ai.AddChild(aiModel);
            var aiPos = new Vector3(0f, 500f, 0f);
            ai.Setup(new FlightModel(stats), null, new CamParams(), aiPos, aiPos + Vector3.Forward);
            ctx.Host.AddChild(ai);

            // Talker chance pinned to 1: every roll passes, so a silent trigger is a GATE
            // decision (cooldown, aliveness), never luck.
            runtime.RegisterAi(ai, accentId: 12, talkerChance: 1f, constitutionChance: 1f);
            var speaker = runtime.Dispatcher.Find(ai.PlayerIndex);
            ctx.Check(speaker is { VoId: 2 },
                $"accent 12 registers the AI as VO id 2 got={speaker?.VoId.ToString() ?? "none"}");
            if (speaker == null)
                return;

            // E10: an inert aircraft is registered as a speaker (registration order is unchanged)
            // but is not eligible — the runtime mirrors InPlay into the dispatcher's own aliveness
            // gate, which is the only thing here that can see a FlightController. Checked in both
            // directions so "not eligible" cannot be the flag's resting state.
            ctx.Check(speaker.Alive, $"baseline: a live AI is an eligible speaker");
            ai.Inert = true;
            ctx.Check(!speaker.Alive, $"going inert takes the speaker out of the broadcast election");
            ai.Inert = false;
            ctx.Check(speaker.Alive, $"…and activation puts it back");

            var played = new List<(int Trigger, string Clip)>();
            runtime.LinePlayed += (_, trigger, clip) => played.Add((trigger, clip));
            runtime.Step(3f); // past the decoded 2 s mute window

            // --- DI: hits through the pool's own entry point until the summary crosses 70 %.
            // The hits WALK the four zones (a single zone's exhausted pool floors the summary
            // at 75 % on this airframe and could never cross the threshold).
            var struck = new (string Part, Vector3 Offset)[]
            {
                ("wing", new Vector3(-2f, 0f, 0f)), ("wing", new Vector3(2f, 0f, 0f)),
                ("fuselage", new Vector3(0f, 0f, -2f)), ("fuselage", new Vector3(0f, 0f, 2f)),
            };
            int budget = 400;
            while (ai.Damage!.SummaryHealthFraction >= 0.7f && budget-- > 0)
            {
                var (part, offset) = struck[budget % struck.Length];
                ai.TakeProjectileHit(gun, ai.WorldPosition + offset, part, 0);
            }
            float fraction = ai.Damage.SummaryHealthFraction;
            ctx.Check(fraction is < 0.7f and > 0.3f,
                $"the sweep stopped inside the DI band health={fraction * 100f:0}%");
            ctx.Check(played.Count == 1 && played[0].Clip.StartsWith("snd_id2_DI-"),
                $"crossing the threshold played exactly one DI line of the pilot's own played=[{string.Join(", ", played)}]");
            var oneShot = LastOneShotPlayer(sounds);
            ctx.Check(oneShot != null && oneShot.GlobalPosition.DistanceTo(ai.WorldPosition) < 1f,
                $"…as a source-following one-shot at the aircraft pos={oneShot?.GlobalPosition}");

            // Follow-up hits in the same tier stay silent: the slot cooldown swallowed them
            // (armed by the PLAY here; the failed-roll arming is the unit suite's,
            // AiVoiceDispatcherTests).
            int before = played.Count;
            static int Tier(float f) => f < 0.3f ? 3 : f < 0.5f ? 2 : f < 0.7f ? 1 : 0;
            int tier = Tier(ai.Damage.SummaryHealthFraction);
            ai.TakeProjectileHit(gun, ai.WorldPosition, "fuselage", 0, damageScale: 0.02f);
            if (Tier(ai.Damage.SummaryHealthFraction) == tier)
            {
                ctx.Check(played.Count == before,
                    $"a follow-up hit in the same tier is silent under the 15 s cooldown");
            }

            // --- the kill: the dying pilot's own cry, dispatched with force (the speaker is
            // already dead when it plays).
            ai.DebugForceCrash();
            ctx.Check(!speaker.Alive, $"the Downed report marked the speaker dead");
            ctx.Check(played.Count >= before + 1 && played[^1].Clip.StartsWith("snd_id2_DE-"),
                $"…and the death cry played THROUGH the dead state (force) clip={(played.Count > 0 ? played[^1].Clip : "none")}");
            ctx.Check(played[^1].Trigger == AiVoiceDispatcher.DeEnemy,
                $"…as id 21 (DE): no team model puts an AI on the player's team, documented");

            // The force flag is the death cry's alone: an ordinary dispatch on the same dead
            // speaker is gated out before anything rolls.
            var unforced = runtime.Dispatcher.Dispatch(ai.PlayerIndex,
                AiVoiceDispatcher.TaSucShk, runtime.Now);
            ctx.Check(unforced.Clip == null && unforced.Outcome == "speaker dead",
                $"an unforced dispatch on the dead speaker is refused outcome={unforced.Outcome}");

            ctx.Note($"lines: {string.Join(", ", played)}");
        }
        finally
        {
            ai?.Free();
            runtime?.Free();
            if (sounds != null)
            {
                sounds.FlushOneShots();
                sounds.Free();
            }
            textures.Dispose();
        }
    }

    /// <summary>The most recent one-shot player under a <see cref="WorldSounds"/> node.</summary>
    private static AudioStreamPlayer3D? LastOneShotPlayer(WorldSounds sounds)
    {
        AudioStreamPlayer3D? last = null;
        foreach (var child in sounds.GetChildren())
        {
            if (child is AudioStreamPlayer3D p)
            {
                last = p;
            }
        }
        return last;
    }

    private static void AiNetFollow(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, "C1");
        ctx.RequireData(chapterZrdr, $"C1 zrdr");

        // The chapter graph resolves both ways the data references it: by id (aiv field 0)
        // and by neindex name (egen/zeppelins/objectives), case-insensitively.
        var nets = AiNets.Load(chapterZrdr);
        var byId = AiNets.ById(nets, 10);
        ctx.Check(byId is { Name: "M4ReinfAce" }, $"net id 10 resolves to M4ReinfAce");
        var byName = AiNets.ByName(nets, "m4reinface");
        ctx.Check(byId != null && ReferenceEquals(byId, byName),
            $"the name lookup (case-insensitive) finds the same net");
        ctx.Check(byId != null && ReferenceEquals(byId, AiNets.Resolve(nets, "10"))
            && ReferenceEquals(byId, AiNets.Resolve(nets, "M4ReinfAce")),
            $"Resolve takes either spelling");
        if (byId == null)
            return;
        var net = byId;

        // The trailer is recorded on the net, unacted-on (B5's documented decision: no
        // target-relative motion until a later wave decodes what to do with it).
        ctx.Check(net.Trailer is { NodeIndex: 10, Name: "player" },
            $"the trailer [10, player] rides the net trailer={net.Trailer?.ToString() ?? "-"}");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var textures = new TextureArchive(texturesPath);
        FlightController? ai = null;
        try
        {
            // The ai-actor pattern: a pilot-driven controller, no camera, no HUD, no devices,
            // spawned on the net's first node, its follower seeded with a fixed value so the
            // route repeats.
            var spawn = net.Nodes[0].Position;
            var look = net.Nodes[1].Position;
            var pilot = AiPilot.HoldingCourse(spawn, look);
            pilot.Throttle = AiPilot.PatrolThrottle;
            var follower = new AiNetFollower(net, new System.Random(1));
            pilot.Patrol = follower;
            var aiModel = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
            ai = new FlightController
            {
                PlaneModel = aiModel,
                Collider = PlaneCollider.Build(aiModel),
                PlayerIndex = AiAircraftSpawner.ShooterIdBase,
                IsHumanPiloted = false,
                Pilot = pilot,
                UseKeyboard = false,
                PadDevices = System.Array.Empty<int>(),
                AllowPause = false,
            };
            ai.AddChild(aiModel);
            ai.Setup(new FlightModel(stats), null, new CamParams(), spawn, look);
            ctx.Host.AddChild(ai);

            // Fly until the follower has captured five nodes (or the budget runs out), logging
            // each target change so a hop off the edge list cannot hide.
            var hops = new List<(int From, int To)>();
            int last = follower.CurrentIndex;
            const int wanted = 5;
            int steps = 0, budget = 120 * 60;
            while (follower.Advances < wanted && steps < budget)
            {
                steps++;
                ai.SimStep(1f / 60f);
                if (follower.CurrentIndex != last)
                {
                    if (last >= 0)
                        hops.Add((last, follower.CurrentIndex));
                    last = follower.CurrentIndex;
                }
            }
            ctx.Check(follower.Advances >= wanted,
                $"the plane captures {wanted} net nodes advances={follower.Advances} in {steps / 60f:0} s of sim");
            bool allEdges = hops.Count > 0;
            foreach (var (from, to) in hops)
            {
                if (!net.Edges.Contains((from, to)) && !net.Edges.Contains((to, from)))
                    allEdges = false;
            }
            ctx.Check(allEdges,
                $"every hop is an EDGE of the graph hops={string.Join(" ", hops.ConvertAll(h => $"{h.From}→{h.To}"))}");
            ctx.Check(Mathf.Abs(ai.WorldPosition.Y - 400f) < 250f,
                $"…within the placeholder law's altitude leash of the net's 400 m y={ai.WorldPosition.Y:0}");
        }
        finally
        {
            ai?.Free();
            textures.Dispose();
        }
    }

    private static void ZeppelinMotionSuite(TestContext ctx)
    {
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, "C1");
        ctx.RequireData(chapterZrdr, $"C1 zrdr");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C1", "M04");
        ctx.RequireData(missionZrdr, $"C1/M04 zrdr");

        var defs = Zeppelins.Load(missionZrdr);
        ctx.Check(defs.Count == 1 && defs[0].Node == "piratezep",
            $"C1/M04 authors one zeppelin, piratezep count={defs.Count}");
        if (defs.Count != 1)
            return;
        var def = defs[0];

        var nets = AiNets.Load(chapterZrdr);
        var net = AiNets.ByName(nets, def.Net);
        ctx.Check(net != null, $"its net '{def.Net}' resolves in C1's neindex");
        if (net == null)
            return;
        int tagged = 0;
        foreach (var n in net.Nodes)
        {
            if (n.Tags.Count > 0)
                tagged++;
        }
        ctx.Check(tagged > 0,
            $"the zeppelin route carries raw shape-A tags, preserved and acted on by nothing (stop-point vs segment id undecoded) tagged={tagged}/{net.Nodes.Count}");

        Node3D? host = null;
        Node3D? heldHost = null;
        ZeppelinRuntime? runtime = null;
        ZeppelinRuntime? heldRuntime = null;
        try
        {
            // A mission zeppelin is a world/anim node; the runtime moves the NODE, no flight
            // model, so a bare Node3D is the exact contract (resolver stands in for the world
            // runtime's FindNodes).
            host = new Node3D { Name = "piratezep" };
            ctx.Host.AddChild(host);
            var resolvedHost = host;
            runtime = new ZeppelinRuntime(defs, name =>
                name.Equals("piratezep", System.StringComparison.OrdinalIgnoreCase) ? resolvedHost : null, nets);
            ctx.Same(1, runtime.LiveCount, $"the zeppelin is live");
            ctx.Check(host.GlobalPosition.DistanceTo(def.Position) < 0.1f,
                $"placed at the authored position at load pos={host.GlobalPosition}");

            var motion = runtime.MotionFor("piratezep");
            ctx.Check(motion != null, $"MotionFor finds the live motion (the F18 seam's lookup)");
            if (motion == null)
                return;

            // Fly until three node captures, checking per-step displacement against the
            // record's own speed limit and every hop against the edge list.
            const float dt = 1f / 60f;
            var hops = new List<(int From, int To)>();
            int last = motion.Follower.CurrentIndex;
            int speedViolations = 0;
            var start = host.GlobalPosition;
            int steps = 0, budget = 60 * 900;
            while (motion.Follower.Advances < 3 && steps < budget)
            {
                steps++;
                var before = host.GlobalPosition;
                runtime.SimStep(dt);
                if (before.DistanceTo(host.GlobalPosition) > (def.MaxSpeed * dt) + 0.01f)
                    speedViolations++;
                if (motion.Follower.CurrentIndex != last)
                {
                    if (last >= 0)
                        hops.Add((last, motion.Follower.CurrentIndex));
                    last = motion.Follower.CurrentIndex;
                }
            }
            ctx.Check(motion.Follower.Advances >= 3,
                $"the node captures 3 net nodes advances={motion.Follower.Advances} in {steps / 60f:0} s of sim");
            ctx.Check(start.DistanceTo(host.GlobalPosition) > 100f,
                $"…moving the world node dist={start.DistanceTo(host.GlobalPosition):0} m");
            bool allEdges = hops.Count > 0;
            foreach (var (from, to) in hops)
            {
                if (!net.Edges.Contains((from, to)) && !net.Edges.Contains((to, from)))
                    allEdges = false;
            }
            ctx.Check(allEdges,
                $"every hop is an EDGE of the graph hops={string.Join(" ", hops.ConvertAll(h => $"{h.From}→{h.To}"))}");
            ctx.Same(0, speedViolations, $"no step ever moved farther than max_speed·dt");

            // Total engine loss: the decoded sqrt curve takes max_speed to 0 (accel keeps its
            // 20 % floor), so the zeppelin decelerates to a stop. This is the seam F18 drives.
            motion.AliveEngines = 0;
            for (int i = 0; i < 60 * 60 && motion.Speed > 0f; i++)
                runtime.SimStep(dt);
            ctx.Check(motion.Speed == 0f,
                $"total engine loss decelerates to a stop speed={motion.Speed:0.##}");
            var stopped = host.GlobalPosition;
            runtime.SimStep(dt);
            ctx.Check(stopped.DistanceTo(host.GlobalPosition) < 1e-3f,
                $"…and the node no longer moves");

            // A deactivated record is placed at its pose but held (mission script would wake
            // it; out of M4 scope).
            var held = new ZeppelinDef
            {
                Node = "heldzep",
                Position = new Vector3(500f, 640f, -500f),
                YawDeg = 90f,
                MaxSpeed = def.MaxSpeed,
                MaxAccel = def.MaxAccel,
                AccelPitchDeg = def.AccelPitchDeg,
                AccelYawDeg = def.AccelYawDeg,
                MaxRateYawDeg = def.MaxRateYawDeg,
                MaxRatePitchDeg = def.MaxRatePitchDeg,
                MinPitchDeg = def.MinPitchDeg,
                MaxPitchDeg = def.MaxPitchDeg,
                Net = def.Net,
                Targets = System.Array.Empty<string>(),
                Healthy = System.Array.Empty<ZeppelinHealthyZone>(),
                NumHealthyRequired = 1,
                Engines = System.Array.Empty<string>(),
                Gasbags = System.Array.Empty<ZeppelinGasbag>(),
                LeftCannons = System.Array.Empty<ZeppelinCannon>(),
                RightCannons = System.Array.Empty<ZeppelinCannon>(),
                CannonHealth = System.Array.Empty<ZeppelinCannonHealth>(),
                Deactivated = true,
            };
            heldHost = new Node3D { Name = "heldzep" };
            ctx.Host.AddChild(heldHost);
            var resolvedHeld = heldHost;
            heldRuntime = new ZeppelinRuntime(new[] { held }, _ => resolvedHeld, nets);
            for (int i = 0; i < 60; i++)
                heldRuntime.SimStep(dt);
            ctx.Check(heldHost.GlobalPosition.DistanceTo(held.Position) < 1e-3f,
                $"a deactivated record is placed but held pos={heldHost.GlobalPosition}");
        }
        finally
        {
            runtime?.Free();
            heldRuntime?.Free();
            host?.Free();
            heldHost?.Free();
        }
    }

    private static void ZeppelinLaunch(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, "C1");
        ctx.RequireData(chapterZrdr, $"C1 zrdr");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C1", "IA1");
        ctx.RequireData(missionZrdr, $"C1/IA1 zrdr");

        // The authored generator: the decoded zeppelin-launch shape, doors named explicitly.
        var egen = EnemyGenerators.Load(missionZrdr);
        ctx.Check(egen.Count == 1 && egen[0].IsZeppelin,
            $"C1/IA1 authors one zeppelin-launch generator count={egen.Count}");
        if (egen.Count != 1)
            return;
        var def = egen[0];
        ctx.Check(def.Node == "multiplayer1zep" && def.Origin == "cargobay"
            && def.OpenAnim == "mp1_open_doors" && def.CloseAnim == "mp1_close_doors"
            && def.MinAltitude is 100f,
            $"…on multiplayer1zep: cargobay origin, mp1 door anims, 100 m gate");
        ctx.Check(def.RotationDeg is { } rot && Mathf.Abs(rot.X + 90f) < 0.01f,
            $"the authored drop attitude is pitch −90° rot={def.RotationDeg}");
        ctx.Check(Mathf.Abs(def.IndPeriod + def.WavePeriod - 7f) < 0.01f,
            $"the composed spawn gap is ind 5 + wave 2 = 7 s");

        // What the model ships for doors: the compiled mis_anim carries both OnCall defs,
        // each moving the hull's door_left/door_right nodes (this is the visual F20 wires).
        var (_, missionAnim) = AnimProgram.ArchivePaths(ctx.DataRoot, "C1", "IA1");
        var archive = AnimArchive.Load(missionAnim, "mis_anim");
        ctx.Check(archive != null, $"C1/IA1's compiled mis_anim archive loads");
        if (archive == null)
            return;
        foreach (var animName in new[] { def.OpenAnim!, def.CloseAnim! })
        {
            AnimDefinition? doorDef = null;
            foreach (var d in archive.Defs)
            {
                if (string.Equals(d.AnimName, animName, System.StringComparison.OrdinalIgnoreCase))
                    doorDef = d;
            }
            var movedNodes = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            if (doorDef != null)
                foreach (var seq in doorDef.Sequences)
                    foreach (var ev in seq.Events)
                        if (ev.Data.Str("name") is { Length: > 0 } target)
                            movedNodes.Add(target);
            ctx.Check(doorDef != null && movedNodes.Contains("door_left") && movedNodes.Contains("door_right"),
                $"'{animName}' ships as a compiled OnCall def over the hull's door nodes nodes=[{string.Join(",", movedNodes)}]");
        }

        var nets = AiNets.Load(chapterZrdr);
        var zepDefs = new List<ZeppelinDef>();
        foreach (var z in Zeppelins.Load(missionZrdr))
        {
            if (z.Node.Equals(def.Node, System.StringComparison.OrdinalIgnoreCase))
                zepDefs.Add(z);
        }
        ctx.Check(zepDefs.Count == 1 && zepDefs[0].Position.Y >= 100f,
            $"the host zeppelin record exists and its authored altitude clears the gate y={(zepDefs.Count > 0 ? zepDefs[0].Position.Y : 0):0}");
        if (zepDefs.Count != 1)
            return;

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var textures = new TextureArchive(texturesPath);
        var spawned = new List<FlightController>();
        var spawnPositions = new List<Vector3>();
        var spawnLooks = new List<Vector3>();
        var animPlays = new List<string>();
        Node3D? host = null;
        Node3D? host2 = null;
        AiGeneratorRuntime? gens = null;
        AiGeneratorRuntime? capped = null;
        ZeppelinRuntime? zeps = null;
        try
        {
            // The world stand-in: the host node with the cargobay drop point riding under the
            // hull, exactly the parent/child shape the chapter gamez builds.
            host = new Node3D { Name = "multiplayer1zep" };
            var cargobay = new Node3D { Name = "cargobay", Position = new Vector3(0f, -20f, 0f) };
            host.AddChild(cargobay);
            ctx.Host.AddChild(host);
            var resolvedHost = host;
            var resolvedBay = cargobay;

            FlightController? SpawnPlane(string planeName, Vector3 pos, Vector3 look, AiPilot pilot)
            {
                var model = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
                var c = new FlightController
                {
                    PlaneModel = model,
                    Collider = PlaneCollider.Build(model),
                    PlayerIndex = AiAircraftSpawner.ShooterIdBase + spawned.Count,
                    IsHumanPiloted = false,
                    Pilot = pilot,
                    UseKeyboard = false,
                    PadDevices = System.Array.Empty<int>(),
                    AllowPause = false,
                };
                c.AddChild(model);
                c.Setup(new FlightModel(stats), null, new CamParams(), pos, look);
                ctx.Host.AddChild(c);
                spawned.Add(c);
                spawnPositions.Add(pos);
                spawnLooks.Add(look);
                return c;
            }

            gens = new AiGeneratorRuntime(new[] { def },
                (name, scope) => name.Equals("multiplayer1zep", System.StringComparison.OrdinalIgnoreCase)
                    ? resolvedHost
                    : name.Equals("cargobay", System.StringComparison.OrdinalIgnoreCase) ? resolvedBay : null,
                nets, ctx.PlaneName, SpawnPlane,
                (name, _) => { animPlays.Add(name); return 1; }, (_, _) => { });
            ctx.Same(1, gens.LiveCount, $"the generator is live (host + IAZep net resolve)");

            // Held below the gate: the unplaced host sits at y −20/0. Ten seconds of sim spawn
            // nothing and never open the door (the blocked branch only ever CLOSES it).
            const float dt = 1f / 60f;
            for (int i = 0; i < 600; i++)
                gens.SimStep(dt);
            ctx.Check(spawned.Count == 0 && animPlays.Count == 0,
                $"held below the 100 m gate: no spawn, door shut spawns={spawned.Count} plays={animPlays.Count}");

            // F17 places and flies the zeppelin; the gate releases. The held spawn is overdue
            // (timer 10 s > the 7 s threshold), so the first unblocked step opens the door and
            // drops the fighter through it in the same tick.
            zeps = new ZeppelinRuntime(zepDefs, name =>
                name.Equals("multiplayer1zep", System.StringComparison.OrdinalIgnoreCase) ? resolvedHost : null, nets);
            ctx.Same(1, zeps.LiveCount, $"the zeppelin is placed and flying (F17)");
            for (int i = 0; i < 120; i++)
                zeps.SimStep(dt);   // two seconds aloft: the hull is moving before any drop
            ctx.Check(host.GlobalPosition.DistanceTo(zepDefs[0].Position) > 1f,
                $"…and has left its authored pose dist={host.GlobalPosition.DistanceTo(zepDefs[0].Position):0.#} m");

            gens.SimStep(dt);
            ctx.Check(spawned.Count == 1, $"the held spawn fires on the first step at altitude");
            ctx.Check(animPlays.Count == 1 && animPlays[0] == "mp1_open_doors",
                $"the door opened with the authored anim plays=[{string.Join(",", animPlays)}]");
            ctx.Check(spawnPositions.Count == 1
                && spawnPositions[0].DistanceTo(cargobay.GlobalPosition + Vector3.Down * 12f) < 0.1f,
                $"the fighter dropped at the origin node's LIVE position (riding the moving hull), 12 m under the doors (the invented bay clearance) pos={spawnPositions[0]} bay={cargobay.GlobalPosition}");
            ctx.Check(spawnLooks.Count == 1 && (spawnLooks[0] - spawnPositions[0]).Normalized().Y < -0.9f,
                $"…in the authored drop attitude (pitch −90°, clamped shy of vertical) dropY={(spawnLooks[0] - spawnPositions[0]).Normalized().Y:0.##}");

            // The fast cycle: the next spawn is 7 s away, never more than 8, so the door stays
            // open through the second drop (no close anim, no second open).
            var firstPos = spawnPositions[0];
            int steps = 0;
            while (spawned.Count < 2 && steps < 60 * 12)
            {
                steps++;
                zeps.SimStep(dt);
                gens.SimStep(dt);
            }
            ctx.Check(spawned.Count == 2, $"the second fighter drops on the 7 s composed schedule t=+{steps / 60f:0.#} s");
            ctx.Check(animPlays.Count == 1,
                $"the hangar stayed open across it (close early only past an 8 s gap) plays=[{string.Join(",", animPlays)}]");
            ctx.Check(spawnPositions.Count == 2 && spawnPositions[1].DistanceTo(firstPos) > 5f,
                $"…again at the live drop point, which has flown on dist={spawnPositions[1].DistanceTo(firstPos):0.#} m");

            // max_active: a capacity-untouched clone capped at 1 live spawn blocks after its
            // first drop (wave_size − spawned + active > max_active) for as long as it lives.
            var cappedDef = new EnemyGeneratorDef
            {
                Node = def.Node,
                VehicleParams = def.VehicleParams,
                Nets = def.Nets,
                Capacity = def.Capacity,
                MaxActive = 1,
                WaveSize = def.WaveSize,
                WavePeriod = def.WavePeriod,
                IndPeriod = def.IndPeriod,
                IsZeppelin = def.IsZeppelin,
                OpenAnim = def.OpenAnim,
                CloseAnim = def.CloseAnim,
                Origin = def.Origin,
                RotationDeg = def.RotationDeg,
                MinAltitude = def.MinAltitude,
            };
            host2 = new Node3D { Name = "multiplayer1zep", Position = new Vector3(0f, 500f, 0f) };
            ctx.Host.AddChild(host2);
            var resolvedHost2 = host2;
            int before = spawned.Count;
            capped = new AiGeneratorRuntime(new[] { cappedDef },
                (name, scope) => name.Equals("multiplayer1zep", System.StringComparison.OrdinalIgnoreCase)
                    ? resolvedHost2 : null,
                nets, ctx.PlaneName, SpawnPlane, (name, _) => 1, (_, _) => { });
            for (int i = 0; i < 60 * 30; i++)
                capped.SimStep(dt);
            ctx.Same(1, spawned.Count - before,
                $"max_active 1 allows exactly one live spawn in 30 s");
        }
        finally
        {
            gens?.Free();
            capped?.Free();
            zeps?.Free();
            foreach (var c in spawned)
                c.Free();
            host?.Free();
            host2?.Free();
            textures.Dispose();
        }
    }

    /// <summary>The F18 multi-zone damage chain on a real mission zeppelin, in the mission's
    /// own world (C1/M04, where piratezep is live — the run's default IA1 switches it off).
    /// Real rounds prove the pipeline (raycast → gate → DamageAt → pool); the bulk gasbag
    /// kills then go through runtime.DamageAt directly, the same sink minus the flight time.
    /// ⚠ INSTR-13: rounds are aimed at the gasbag collider's BUILT pose, captured before
    /// ZeppelinRuntime places the node at its authored position — a moved physics body never
    /// re-enters the space queries inside one frame.</summary>
    private static void ZeppelinDamageSuite(TestContext ctx)
    {
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C1", "M04");
        ctx.RequireData(missionZrdr, $"C1/M04 zrdr");
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        // Data-driven picks: the first flagged weapon (wep_14/wep_28 ship the flag) — a
        // blast-less one preferred so its damage lands on exactly the struck pool — and the
        // first unflagged gun.
        WeaponDef? zepWeapon = weapons.All.FirstOrDefault(w =>
                w.DamagesZeppelin && w.HealthDamage is > 0f && w.ImpactProximity is not > 0f)
            ?? weapons.All.FirstOrDefault(w => w.DamagesZeppelin && w.HealthDamage is > 0f);
        WeaponDef? gun = weapons.All.FirstOrDefault(w =>
            w.IsGun && !w.DamagesZeppelin && w.HealthDamage is > 0f);
        ctx.Check(zepWeapon != null, $"a DAMAGES_ZEPPELIN weapon with HEALTH_DAMAGE ships");
        ctx.Check(gun != null, $"a gun without DAMAGES_ZEPPELIN ships");
        if (zepWeapon == null || gun == null)
            return;

        ctx.WithWorld("C1", collision: true, mission: "M04", world =>
        {
            var runtime = world.Session.Runtime;
            var defs = Zeppelins.Load(missionZrdr);
            ctx.Check(defs.Count == 1 && defs[0].Node == "piratezep",
                $"C1/M04 authors one zeppelin, piratezep count={defs.Count}");
            if (defs.Count != 1)
                return;
            var def = defs[0];
            var host = runtime.FindNodes("piratezep").FirstOrDefault();
            ctx.Check(host != null, $"the piratezep world node resolves in the M04 world");
            if (host == null)
                return;

            // The built pose, BEFORE ZeppelinRuntime moves the node (INSTR-13 — see summary).
            var bagNode = runtime.FindNodes("gasbag1", host).FirstOrDefault();
            var engNode = runtime.FindNodes(def.Engines[0], host).FirstOrDefault();
            ctx.Check(bagNode != null && engNode != null,
                $"gasbag1 and {def.Engines[0]} resolve under the piratezep subtree");
            if (bagNode == null || engNode == null)
                return;
            var builtBagPos = bagNode.GlobalPosition;

            var nets = AiNets.Load(SessionPaths.ChapterZrdr(ctx.DataRoot, "C1"));
            ZeppelinRuntime? zeps = null;
            ProjectilePool? pool = null;
            TextureArchive? textures = null;
            var started = new List<string>();
            try
            {
                zeps = new ZeppelinRuntime(defs,
                    name => runtime.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null, nets);
                zeps.WireDamage(runtime);
                var motion = zeps.MotionFor("piratezep")!;

                // Seeding: the gasbag pool carries the RECORD's 120 hp (its def has HEALTH 0),
                // the engine pool its compiled def's 40 — record where authored, def where not.
                var bagPool = runtime.Destructibles.PoolsOn(bagNode).FirstOrDefault();
                var engPool = runtime.Destructibles.PoolsOn(engNode).FirstOrDefault();
                ctx.Check(bagPool != null && Mathf.IsEqualApprox(bagPool.MaxHealth, 120f),
                    $"gasbag1's pool is seeded from the record hp={bagPool?.MaxHealth ?? -1f} (authored 120)");
                ctx.Check(engPool != null && Mathf.IsEqualApprox(engPool.MaxHealth, 40f),
                    $"{def.Engines[0]}'s pool keeps its compiled def HEALTH hp={engPool?.MaxHealth ?? -1f} (authored 40)");
                ctx.Same(def.Healthy.Count, zeps.SurvivorsOf("piratezep"),
                    $"all {def.Healthy.Count} healthy entries start alive");
                if (bagPool == null || engPool == null)
                    return;

                // A live pool with the gate wired, damage routed exactly as flight wires it.
                int gateRefusals = 0;
                textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, "C1"));
                pool = new ProjectilePool(textures, null, null)
                {
                    DamageSink = runtime.DamageAt,
                };
                pool.WorldDamageGate = (struckNode, weapon) =>
                {
                    bool allowed = zeps.GateWeaponDamage(struckNode, weapon);
                    if (!allowed)
                        gateRefusals++;
                    return allowed;
                };
                ctx.Host.AddChild(pool);

                // The muzzle 80 m from the gasbag's BUILT pose, aimed straight at it.
                var muzzlePos = builtBagPos + new Vector3(0f, 80f, 0f);
                var aim = (builtBagPos - muzzlePos).Normalized();
                var muzzle = new Transform3D(Basis.LookingAt(aim, Vector3.Right), muzzlePos);

                // 1. The unflagged gun: the round strikes the gasbag, the gate refuses the
                // damage, the pool is untouched.
                float before = bagPool.Health;
                pool.Spawn(gun, muzzle, Vector3.Zero);
                for (int i = 0; i < 180 && gateRefusals == 0; i++)
                    pool.SimStep(1f / 60f);
                ctx.Check(gateRefusals > 0,
                    $"the {gun.Id} round STRUCK the gasbag and was refused by the gate refusals={gateRefusals}");
                ctx.Check(Mathf.IsEqualApprox(bagPool.Health, before),
                    $"…and gasbag hp is untouched hp={bagPool.Health:0.##}");

                // 2. The DAMAGES_ZEPPELIN weapon: the same geometry spends real hp.
                pool.Spawn(zepWeapon, muzzle, Vector3.Zero);
                for (int i = 0; i < 180 && Mathf.IsEqualApprox(bagPool.Health, before); i++)
                    pool.SimStep(1f / 60f);
                ctx.Check(bagPool.Health <= before - zepWeapon.HealthDamage!.Value + 0.01f,
                    $"the {zepWeapon.Id} round spends its HEALTH_DAMAGE hp {before:0.##}→{bagPool.Health:0.##}");

                // 3. An engine kill drives the F17 sqrt seam: fewer alive engines, lower cap.
                runtime.OnInstanceStarted = (d, _) => { if (d.AnimName != null) started.Add(d.AnimName); };
                float capBefore = motion.EffectiveMaxSpeed;
                runtime.DamageAt(engNode, engPool.MaxHealth + 1f);
                zeps.SimStep(1f / 60f);
                ctx.Same(motion.TotalEngines - 1, motion.AliveEngines,
                    $"the destroyed engine leaves the alive count");
                ctx.Check(motion.EffectiveMaxSpeed < capBefore,
                    $"…and the sqrt curve lowers the speed cap {capBefore:0.##}→{motion.EffectiveMaxSpeed:0.##} m/s");

                // 4. The survivor threshold, in the engine: required 4 of 6. Two gasbags dead
                // (survivors 4) lives; the third (survivors 3 < 4) kills — the decoded
                // polarity. The design's destroy-count reading would still be alive here.
                runtime.DamageAt(bagNode, 10_000f); // finishes gasbag1
                runtime.DamageAt(runtime.FindNodes("gasbag2", host).FirstOrDefault(), 10_000f);
                zeps.SimStep(1f / 60f);
                ctx.Same(4, zeps.SurvivorsOf("piratezep"), $"two gasbags down leaves 4 survivors");
                ctx.Check(!zeps.IsDead("piratezep"),
                    $"survivors 4 >= required {def.NumHealthyRequired}: alive");
                runtime.DamageAt(runtime.FindNodes("gasbag3", host).FirstOrDefault(), 10_000f);
                zeps.SimStep(1f / 60f);
                ctx.Same(3, zeps.SurvivorsOf("piratezep"), $"the third leaves 3 survivors");
                ctx.Check(zeps.IsDead("piratezep"),
                    $"survivors 3 < required {def.NumHealthyRequired}: the zeppelin DIES (the decoded polarity)");
                ctx.Check(started.Contains("all_pzep_gasbags"),
                    $"the kill plays the authored hull death started=[{string.Join(", ", started)}]");

                // The dead hull stops flying: the node no longer moves.
                var restingPos = host.GlobalPosition;
                zeps.SimStep(1f);
                ctx.Check(restingPos.DistanceTo(host.GlobalPosition) < 1e-3f,
                    $"the dead zeppelin's motion is stopped");
            }
            finally
            {
                runtime.OnInstanceStarted = null;
                pool?.Free();
                zeps?.Free();
                textures?.Dispose();
            }
        });
    }

    /// <summary>The F19 broadside chain on C1/M04's piratezep in its own mission world: arc-
    /// gated deploy, a real wep_28 volley at a player stand-in, hold-fire-and-retract out of
    /// arc, the F18 thinning, scatter on a cannon_inaccuracy clone, and the zeppelin-vs-
    /// zeppelin gasbag pick on constructed geometry (no shipped mission puts two zeppelins in
    /// one M04 world; C1/MP3's pair exercises the arm in a live session). The player stand-in
    /// is REPOSITIONED through its flight model each step to hold the tested bearing on the
    /// flying hull; rounds are asserted at the spawn seam (count + direction), never as hits —
    /// INSTR-13, a moved body never re-enters the one-frame space queries.</summary>
    private static void ZeppelinBroadsideSuite(TestContext ctx)
    {
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C1", "M04");
        ctx.RequireData(missionZrdr, $"C1/M04 zrdr");
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var wep28 = weapons.Get(ZeppelinRuntime.BroadsideWeaponId);
        ctx.Check(wep28 is { Velocity: not null },
            $"the hardcoded {ZeppelinRuntime.BroadsideWeaponId} resolves in weapons.zrd v={wep28?.Velocity ?? 0f:0}");
        if (wep28 == null)
            return;

        ctx.WithWorld("C1", collision: true, mission: "M04", world =>
        {
            var runtime = world.Session.Runtime;
            var defs = Zeppelins.Load(missionZrdr);
            var def = defs[0];
            var host = runtime.FindNodes("piratezep").FirstOrDefault();
            ctx.Check(defs.Count == 1 && host != null && def.CannonInaccuracyDeg == null,
                $"C1/M04 authors piratezep (no cannon_inaccuracy), and its node resolves");
            if (host == null)
                return;

            var nets = AiNets.Load(SessionPaths.ChapterZrdr(ctx.DataRoot, "C1"));
            var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
            var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
            TextureArchive? textures = null;
            ProjectilePool? pool = null;
            ZeppelinRuntime? zeps = null;
            ZeppelinRuntime? scatterZeps = null;
            ZeppelinRuntime? zvz = null;
            FlightController? player = null;
            Node3D? attackerHost = null;
            Node3D? targetHost = null;
            var started = new List<string>();
            try
            {
                textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, "C1"));
                var live = new ProjectilePool(textures, null, null);
                pool = live;
                ctx.Host.AddChild(live);
                runtime.OnInstanceStarted = (d, _) => { if (d.AnimName != null) started.Add(d.AnimName); };

                zeps = new ZeppelinRuntime(defs,
                    name => runtime.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null, nets);
                zeps.WireDamage(runtime);
                zeps.WireCannons(live, weapons);
                var bs = zeps.BroadsideOf("piratezep");
                ctx.Check(bs != null && bs.Cannons.Count == 12,
                    $"the broadside wires 6+6 cannons count={bs?.Cannons.Count ?? 0}");
                if (bs == null)
                    return;
                ctx.Check(bs.Cannons.All(c => Mathf.IsEqualApprox(c.DeploySeconds, 4f)),
                    $"deploy durations are read from the authored anim defs (4 s run_time) first={bs.Cannons[0].DeploySeconds:0.##}");

                // The player stand-in: a real registered aircraft whose flight-model position
                // the suite steers to hold each phase's bearing on the FLYING hull.
                var fm = new FlightModel(stats);
                var model = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
                player = new FlightController
                {
                    PlaneModel = model,
                    Collider = PlaneCollider.Build(model),
                    PlayerIndex = 0,
                    Projectiles = live,
                    UseKeyboard = false,
                    PadDevices = System.Array.Empty<int>(),
                    AllowPause = false,
                };
                player.AddChild(model);
                player.Setup(fm, null, new CamParams(), host.GlobalPosition + Vector3.Right * 300f,
                    host.GlobalPosition);
                ctx.Host.AddChild(player);

                var motion = zeps.MotionFor("piratezep")!;
                const float dt = 1f / 60f;
                Vector3 PortAbeam() => host.GlobalPosition + ZeppelinBroadside.SideNormal(
                    motion.YawRad, motion.PitchRad, BroadsideSide.Left, bs.RightSign) * 300f;
                void StepAt(System.Func<Vector3> where, int steps, ZeppelinRuntime target)
                {
                    for (int i = 0; i < steps; i++)
                    {
                        fm.Position = where();
                        target.SimStep(dt);
                        live.SimStep(dt);
                    }
                }

                // 1. In the port arc: the port cannons deploy (authored anims), starboard
                // stays stowed, and at 4 s the readied side volleys 6 real rounds.
                StepAt(PortAbeam, 30, zeps);
                ctx.Check(started.Count(a => a.StartsWith("deploy_pzep_lbroad")) == 6
                          && !started.Any(a => a.StartsWith("deploy_pzep_rbroad")),
                    $"the port six deploy, starboard stays stowed anims=[{string.Join(",", started)}]");
                ctx.Same(0, zeps.BroadsideShotsOf("piratezep"),
                    $"mid-deploy nothing fires (stowed cannons deploy INSTEAD of firing)");
                StepAt(PortAbeam, (int)(4.5f / dt), zeps);
                ctx.Same(6, zeps.BroadsideShotsOf("piratezep"),
                    $"the readied port side volleys one round per live cannon");
                // 30° bound: the hull flies on between volley and check and the muzzles sit
                // ~100 m along it (22° seen from the hull origin at 300 m) — the solve's
                // exactness is pinned engine-free (ZeppelinBroadsideTests); the in-engine
                // claim is "at the player, out of the port side".
                var volley = zeps.LastVolleyOf("piratezep");
                var portNow = ZeppelinBroadside.SideNormal(
                    motion.YawRad, motion.PitchRad, BroadsideSide.Left, bs.RightSign);
                float worstOff = 0f;
                float worstSide = 1f;
                foreach (var dir in volley)
                {
                    worstOff = Mathf.Max(worstOff, dir.AngleTo(fm.Position - host.GlobalPosition));
                    worstSide = Mathf.Min(worstSide, dir.Normalized().Dot(portNow));
                }
                ctx.Check(volley.Count == 6 && worstOff < 0.52f && worstSide > 0.5f,
                    $"every round leaves lead-solved toward the player, out of the port side dirs={volley.Count} worstOff={Mathf.RadToDeg(worstOff):0.#}° minPortDot={worstSide:0.##}");

                // 2. Out of arc: dead ahead. Fire holds through the 20 s re-fire horizon and
                // the idle window retracts the port cannons.
                started.Clear();
                int shotsBefore = zeps.BroadsideShotsOf("piratezep");
                Vector3 Ahead() => host.GlobalPosition + motion.Forward * 300f;
                StepAt(Ahead, (int)(25f / dt), zeps);
                ctx.Same(shotsBefore, zeps.BroadsideShotsOf("piratezep"),
                    $"out of both arcs the broadside holds fire for 25 s");
                ctx.Check(started.Count(a => a.StartsWith("retract_pzep_lbroad")) == 6,
                    $"the idle port cannons retract (invented {ZeppelinBroadside.StowAfterIdleSeconds:0} s window) anims=[{string.Join(",", started.Where(a => a.Contains("retract")))}]");

                // 3. F18 thinning: destroy one port cannon (its compiled def pool, HEALTH 60),
                // return to the port arc — the redeployed volley is 5, not 6.
                var lbroadNode = runtime.FindNodes(def.LeftCannons[0].Node, host).FirstOrDefault();
                var lbroadPool = lbroadNode == null ? null : runtime.Destructibles.PoolsOn(lbroadNode).FirstOrDefault();
                ctx.Check(lbroadPool != null,
                    $"'{def.LeftCannons[0].Node}' carries its compiled def pool hp={lbroadPool?.MaxHealth ?? 0f:0}");
                if (lbroadPool == null)
                    return;
                runtime.DamageAt(lbroadNode, lbroadPool.MaxHealth + 1f);
                shotsBefore = zeps.BroadsideShotsOf("piratezep");
                int guard = 0;
                while (zeps.BroadsideShotsOf("piratezep") == shotsBefore && guard++ < (int)(30f / dt))
                {
                    fm.Position = PortAbeam();
                    zeps.SimStep(dt);
                    live.SimStep(dt);
                }
                ctx.Same(5, zeps.BroadsideShotsOf("piratezep") - shotsBefore,
                    $"the destroyed cannon drops out — the volley thins to 5 after {guard * dt:0.#} s");

                // 4. Scatter: a clone authoring cannon_inaccuracy 10° (the C2B/M04 value) on
                // the same hull spreads a volley the exact solve would collapse to a point.
                var scatterDef = CloneWithInaccuracy(def, 10f);
                scatterZeps = new ZeppelinRuntime(new[] { scatterDef },
                    name => runtime.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null, nets);
                scatterZeps.WireCannons(live, weapons);
                var sMotion = scatterZeps.MotionFor("piratezep")!;
                var sBs = scatterZeps.BroadsideOf("piratezep")!;
                Vector3 SPort() => host.GlobalPosition + ZeppelinBroadside.SideNormal(
                    sMotion.YawRad, sMotion.PitchRad, BroadsideSide.Left, sBs.RightSign) * 300f;
                StepAt(SPort, (int)(5f / dt), scatterZeps);
                var scattered = scatterZeps.LastVolleyOf("piratezep");
                float maxPair = 0f;
                for (int i = 0; i < scattered.Count; i++)
                    for (int j = i + 1; j < scattered.Count; j++)
                        maxPair = Mathf.Max(maxPair, scattered[i].AngleTo(scattered[j]));
                ctx.Check(scattered.Count == 6 && maxPair > Mathf.DegToRad(1f),
                    $"cannon_inaccuracy 10° spreads the volley max pair angle {Mathf.RadToDeg(maxPair):0.##}° over {scattered.Count} rounds");

                // 5. The zeppelin-vs-zeppelin arm, constructed geometry (see summary): a near-
                // static attacker whose target zeppelin is deactivated abeam, one gasbag
                // placed outside the 0.707 arc — every pick lands on an IN-ARC bag.
                attackerHost = new Node3D { Name = "attackzep" };
                var cb1 = new Node3D { Name = "cb1", Position = new Vector3(20f, 0f, -30f) };
                var cb2 = new Node3D { Name = "cb2", Position = new Vector3(20f, 0f, 30f) };
                attackerHost.AddChild(cb1);
                attackerHost.AddChild(cb2);
                targetHost = new Node3D { Name = "targetzep" };
                var g1 = new Node3D { Name = "g1", Position = new Vector3(0f, 0f, -40f) };
                var g2 = new Node3D { Name = "g2", Position = new Vector3(0f, 0f, 40f) };
                var g3 = new Node3D { Name = "g3", Position = new Vector3(-290f, 0f, -290f) };
                targetHost.AddChild(g1);
                targetHost.AddChild(g2);
                targetHost.AddChild(g3);
                ctx.Host.AddChild(attackerHost);
                ctx.Host.AddChild(targetHost);
                var attacker = SyntheticZep("attackzep", new Vector3(0f, 4000f, 0f), nets[0].Name,
                    targets: new[] { "targetzep" });
                var target = SyntheticZep("targetzep", new Vector3(300f, 4000f, 0f), nets[0].Name,
                    deactivated: true,
                    healthy: new[] { "g1", "g2", "g3" });
                var resolvedAtt = attackerHost;
                var resolvedTgt = targetHost;
                zvz = new ZeppelinRuntime(new[] { attacker, target }, name =>
                    name == "attackzep" ? resolvedAtt : name == "targetzep" ? resolvedTgt : null, nets);
                zvz.WireCannons(live, weapons);
                for (int i = 0; i < (int)(6f / dt) && zvz.BroadsideShotsOf("attackzep") == 0; i++)
                {
                    zvz.SimStep(dt);
                    live.SimStep(dt);
                }
                var picks = zvz.LastVolleyOf("attackzep");
                ctx.Same(2, picks.Count, $"both starboard cannons fire at the target zeppelin");
                bool onlyInArc = picks.Count > 0 && picks.All(dir =>
                {
                    var fromCb = dir.Normalized();
                    float toG1 = fromCb.AngleTo(g1.GlobalPosition - attackerHost.GlobalPosition);
                    float toG2 = fromCb.AngleTo(g2.GlobalPosition - attackerHost.GlobalPosition);
                    float toG3 = fromCb.AngleTo(g3.GlobalPosition - attackerHost.GlobalPosition);
                    return Mathf.Min(toG1, toG2) < toG3;
                });
                ctx.Check(onlyInArc,
                    $"every rand()-picked aim point is an IN-ARC gasbag, never the out-of-arc g3");
            }
            finally
            {
                runtime.OnInstanceStarted = null;
                pool?.Free();
                zeps?.Free();
                scatterZeps?.Free();
                zvz?.Free();
                player?.Free();
                attackerHost?.Free();
                targetHost?.Free();
                textures?.Dispose();
            }
        });
    }

    /// <summary>A copy of a shipped record with <c>cannon_inaccuracy</c> authored — the scatter
    /// phase's instrument (no C1 record authors one; C2B/M04's 10° is the shipped value).</summary>
    private static ZeppelinDef CloneWithInaccuracy(ZeppelinDef def, float inaccuracyDeg) => new()
    {
        Node = def.Node,
        Position = def.Position,
        YawDeg = def.YawDeg,
        PitchDeg = def.PitchDeg,
        MaxSpeed = def.MaxSpeed,
        MaxAccel = def.MaxAccel,
        AccelPitchDeg = def.AccelPitchDeg,
        AccelYawDeg = def.AccelYawDeg,
        MaxRateYawDeg = def.MaxRateYawDeg,
        MaxRatePitchDeg = def.MaxRatePitchDeg,
        MinPitchDeg = def.MinPitchDeg,
        MaxPitchDeg = def.MaxPitchDeg,
        Net = def.Net,
        Targets = def.Targets,
        Healthy = def.Healthy,
        NumHealthyRequired = def.NumHealthyRequired,
        Engines = def.Engines,
        Gasbags = def.Gasbags,
        CannonFireDelay = def.CannonFireDelay,
        CannonFireRange = def.CannonFireRange,
        LeftCannons = def.LeftCannons,
        RightCannons = def.RightCannons,
        CannonHealth = def.CannonHealth,
        CannonInaccuracyDeg = inaccuracyDeg,
    };

    /// <summary>A minimal constructed zeppelin record for the zeppelin-vs-zeppelin phase:
    /// near-static (rates/speed floored) so the constructed bearings hold while cannons
    /// deploy on the fallback timing.</summary>
    private static ZeppelinDef SyntheticZep(string node, Vector3 pos, string net,
        string[]? targets = null, string[]? healthy = null, bool deactivated = false)
    {
        var zones = new List<ZeppelinHealthyZone>();
        foreach (var h in healthy ?? System.Array.Empty<string>())
            zones.Add(new ZeppelinHealthyZone(h, "panels"));
        return new ZeppelinDef
        {
            Node = node,
            Position = pos,
            YawDeg = 0f,
            MaxSpeed = 0.1f,
            MaxAccel = 4.47f,
            AccelYawDeg = 0.1f,
            AccelPitchDeg = 0.1f,
            MaxRateYawDeg = 0.1f,
            MaxRatePitchDeg = 0.1f,
            MinPitchDeg = -30f,
            MaxPitchDeg = 30f,
            Net = net,
            Targets = targets ?? System.Array.Empty<string>(),
            Healthy = zones,
            NumHealthyRequired = 1,
            Engines = System.Array.Empty<string>(),
            Gasbags = System.Array.Empty<ZeppelinGasbag>(),
            CannonHealth = System.Array.Empty<ZeppelinCannonHealth>(),
            LeftCannons = System.Array.Empty<ZeppelinCannon>(),
            RightCannons = targets == null
                ? (IReadOnlyList<ZeppelinCannon>)System.Array.Empty<ZeppelinCannon>()
                : new[]
                {
                    new ZeppelinCannon("cb1", "deploy_cb1", "retract_cb1"),
                    new ZeppelinCannon("cb2", "deploy_cb2", "retract_cb2"),
                },
            CannonFireDelay = 20f,
            CannonFireRange = 500f,
            Deactivated = deactivated,
        };
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

    /// <summary>The two authored per-kind behaviours of <c>templates.zrd</c>'s substitute and
    /// scale_range, asserted as an A/B against the build that does not read <c>templates.zrd</c> at
    /// all — the same <see cref="ClutterBuilder"/> over the same gamez, differing only in whether a
    /// spec was handed to it. See <c>docs/org/clutter.md</c>.
    ///
    /// <para>Three builds, because three separate things can go wrong and each needs its own
    /// able-to-fail control:</para>
    /// <list type="number">
    /// <item><b>Bare</b> (no spec) — an unsubstituted monoculture at authored size. The baseline
    /// every other claim is measured against, rather than against numbers pasted from a previous
    /// session.</item>
    /// <item><b>Dressed</b> — with C1's spec. The instance TOTAL must be unchanged (a substitution
    /// moves a stamp between kinds, it never adds or drops one — a total that moved would mean the
    /// roll is running somewhere it should not), the species mix must have moved (<c>firtree1</c>
    /// rolls 9:1 to <c>firtree2</c>), and the scales must span a range rather than sit at 1.</item>
    /// <item><b>Dressed again</b> — identical transform for transform, in the SAME process and with
    /// no <see cref="Rng"/> reset between them, which is the strong form: the stream is a function
    /// of the data alone. ⚠ Seeding it from <c>Rng.Master</c> instead rerolls C1's forest on every
    /// unpinned launch (~15,150 firtree1, differing run to run) where the original's is
    /// fixed.</item>
    /// </list></summary>
    private static void ClutterDeterminism(TestContext ctx)
    {
        const string chapter = "C1";
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, chapter);
        string gamezPath = SessionPaths.ChapterGamez(ctx.DataRoot, chapter);
        ctx.RequireData(texturesPath, $"{chapter} textures");
        ctx.RequireData(gamezPath, $"{chapter} gamez");
        ctx.RequireData(ctx.InterpPath, $"interp.json");

        var gamez = GameZ.Load(gamezPath);
        using var textures = new TextureArchive(texturesPath);
        var names = ClutterBuilder.TemplateNames(ctx.InterpPath, chapter);
        var props = ClutterTemplateSpec.Load(SessionPaths.ChapterZrdr(ctx.DataRoot, chapter));
        ctx.Check(props != null && props.Kinds.Count > 0, $"{chapter} templates.zrd read");

        // Each build's kinds as (label → placements), plus the flat transform list in kind order.
        static (Dictionary<string, int> ByKind, List<Transform3D> Placements, int Total) Take(
            ClutterBuilder builder, IReadOnlyList<string> names)
        {
            var root = builder.Build(names);
            var byKind = new Dictionary<string, int>(System.StringComparer.Ordinal);
            var placements = new List<Transform3D>();
            foreach (var kind in builder.ExportedKinds ?? System.Array.Empty<ClutterBuilder.KindExport>())
            {
                byKind.TryGetValue(kind.Texture, out int had);
                byKind[kind.Texture] = had + kind.Placements.Count;
                placements.AddRange(kind.Placements);
            }
            int total = builder.InstanceCount + builder.SolidCount;
            root?.Free();
            return (byKind, placements, total);
        }

        var bare = Take(new ClutterBuilder(gamez, textures), names);
        var dressed = Take(new ClutterBuilder(gamez, textures, null, props), names);
        var again = Take(new ClutterBuilder(gamez, textures, null, props), names);

        ctx.Check(bare.Total > 0, $"the bare build placed something total={bare.Total}");
        ctx.Same(bare.Total, dressed.Total, $"{chapter} instance total is unchanged by substitution");

        // The mix moved, and in the authored direction: firtree1 sheds a tenth of its stamps to
        // firtree2. Asserted as a direction plus a band rather than as an exact count, so the
        // suite survives a re-seed but still fails if the roll stops happening or inverts.
        bare.ByKind.TryGetValue("firtree1.tif", out int bareFir1);
        dressed.ByKind.TryGetValue("firtree1.tif", out int dressedFir1);
        bare.ByKind.TryGetValue("firtree2.tif", out int bareFir2);
        dressed.ByKind.TryGetValue("firtree2.tif", out int dressedFir2);
        int moved = bareFir1 - dressedFir1;
        ctx.Check(moved > 0 && dressedFir2 - bareFir2 == moved,
            $"firtree1 sheds stamps and firtree2 gains exactly those lost={moved} gained={dressedFir2 - bareFir2}");
        ctx.Check(moved > bareFir1 * 0.08f && moved < bareFir1 * 0.12f,
            $"the shed fraction is the authored 9:1 moved={moved} of {bareFir1}");

        // Scale: uniform, inside C1's widest authored band, and actually varying. A build that
        // silently dropped the draw would pass every count assertion above.
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var xf in dressed.Placements)
        {
            var s = xf.Basis.Scale;
            ctx.Check(Mathf.Abs(s.X - s.Y) < 1e-5f && Mathf.Abs(s.X - s.Z) < 1e-5f,
                $"the scale is uniform scale={s}");
            lo = Mathf.Min(lo, s.X);
            hi = Mathf.Max(hi, s.X);
        }
        foreach (var xf in bare.Placements)
        {
            ctx.Check(Mathf.Abs(xf.Basis.Scale.X - 1f) < 1e-5f,
                $"the bare build leaves every instance at its authored size scale={xf.Basis.Scale.X}");
        }
        ctx.Check(lo >= 0.9f - 1e-4f && hi <= 1.5f + 1e-4f, $"scales stay inside C1's authored 0.9-1.5 lo={lo} hi={hi}");
        ctx.Check(hi - lo > 0.4f, $"scales actually vary lo={lo} hi={hi}");

        // The strong form: same process, no reseed, transform for transform.
        ctx.Same(dressed.Placements.Count, again.Placements.Count, $"the second dressed build placed the same count");
        int drift = 0;
        for (int i = 0; i < Mathf.Min(dressed.Placements.Count, again.Placements.Count); i++)
        {
            if (dressed.Placements[i] != again.Placements[i])
            {
                drift++;
            }
        }
        ctx.Same(0, drift, $"two dressed builds are identical transform for transform");
        ctx.Note($"{chapter} bare firtree1={bareFir1} firtree2={bareFir2}; dressed firtree1={dressedFir1} firtree2={dressedFir2}; scales {lo:0.000}-{hi:0.000}");
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

    /// <summary>The lens flare's gating, chapter by chapter.
    ///
    /// <para>The flare is deliberately gated on <b>chapter data</b>, never on a chapter name, and it
    /// reads <b>two</b> independent gates: a gamez node called <c>sun</c> in the horizon subtree, and
    /// <c>LensFlareTexture</c> slot registrations in <c>support\&lt;ch&gt;\init.gw</c>. Across the
    /// retail install both are true of C2 and C3 and of nothing else — as is the presence of a
    /// texture named <c>sun</c>, a third agreement this suite does not need to re-check.</para>
    ///
    /// <para>This is the check most likely to rot silently: nothing about C1 looking correct would
    /// tell you a flare rig had started building there, and nothing about C3 looking correct would
    /// tell you the C2 gate had stopped resolving. Both directions are asserted.</para>
    ///
    /// <para>Data-only on purpose — no world is built. The interp parse is a file read and the sun
    /// node is a gamez lookup, so this stays a fast suite rather than a third full eight-chapter
    /// world sweep beside <c>destructible-census</c> and <c>collision-visibility</c>.</para></summary>
    /// <summary>The <c>DirectionalLight3D</c> is pointed by the flown zone's authored
    /// <c>SUNLIGHT_ORIENTATION</c>, and keeps following it when the camera's weather state moves
    /// to another zone.
    ///
    /// <para>The unit tests in <c>CSVM.Tests</c> already pin the parse and the euler→direction
    /// mapping; neither can see the failure this suite exists for — the light being wired to the
    /// wrong seam, or the zone edge firing without carrying it. Both are invisible in a screenshot
    /// too: a light at the previous zone's bearing looks like a light.</para>
    ///
    /// <para><b>C2/MP2 is the only shape in the install that can fail this.</b> Every other mission
    /// authors the same bearing in both its zones, so a
    /// zone change there moves the light from a value to the same value and would pass with the
    /// write deleted. C2's MP2 and MP3 are the two files whose ZONE1 (−65°) and ZONE2 (−25°) pitch
    /// differ. Do not "simplify" this onto C1/IA1.</para></summary>
    private static void SunOrientation(TestContext ctx)
    {
        string zrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C2", "MP2");
        ctx.RequireData(zrdr, $"C2/MP2 mission zrdr");

        var sun = new DirectionalLight3D { Name = "sun-orientation-probe" };
        ctx.Host.AddChild(sun);
        var camera = new Camera3D { Name = "sun-orientation-camera" };
        ctx.Host.AddChild(camera);
        try
        {
            // No --sky-zone: an explicit one disarms the state machine outright, which
            // would make the zone change below unobservable. The default request is zone2, and with
            // no horizon geometry to correct it that is what the flight builds with.
            var spec = SessionSpec.Parse(new[] { "--chapter=C2", "--mission=MP2" });
            var rig = new PlayerRig { Index = 0, Camera = camera, HudParent = ctx.Host };
            var rigs = new List<PlayerRig> { rig };

            var weatherRig = new WeatherRig(spec, ctx.Host, sun);
            weatherRig.Build(zrdr, rigs, System.Array.Empty<HorizonZone>(), _ => { });
            ctx.Check(NearDegrees(sun.RotationDegrees, -25f, 90f),
                $"C2/MP2 builds at ZONE2's bearing (got {sun.RotationDegrees.X:0.#}°/{sun.RotationDegrees.Y:0.#}°)");

            // Below the cloud band (19024–20124 m) the camera is in weather state 1, so the edge
            // trigger swaps to ZONE1 — and the light must ride along. Ticked twice: the first Tick
            // publishes the rig's new state, and the swap is asserted after it has settled.
            camera.Position = new Vector3(0f, 0f, 0f);
            weatherRig.Tick(rigs);
            ctx.Same(1, rig.CameraWeatherState, $"camera below the band is in weather state 1");
            ctx.Check(NearDegrees(sun.RotationDegrees, -65f, 90f),
                $"a zone change carries the sun to ZONE1's bearing (got {sun.RotationDegrees.X:0.#}°/{sun.RotationDegrees.Y:0.#}°)");

            // ...and back. A one-way test would pass on a light that moved once and stuck.
            camera.Position = new Vector3(0f, 25000f, 0f);
            weatherRig.Tick(rigs);
            ctx.Same(2, rig.CameraWeatherState, $"camera above the band is in weather state 2");
            ctx.Check(NearDegrees(sun.RotationDegrees, -25f, 90f),
                $"and back to ZONE2's on the return crossing (got {sun.RotationDegrees.X:0.#}°/{sun.RotationDegrees.Y:0.#}°)");

            // The other half, asserted where it would regress: shadow mapping stays off, so this
            // light cannot cast the raked plane-on-plane shadows the original never draws (the
            // real ground shadow is drawn elsewhere).
            ctx.Check(!sun.ShadowEnabled, $"the world light casts no shadow map");
        }
        finally
        {
            sun.QueueFree();
            camera.QueueFree();
        }
    }

    // Degrees, not radians, and a loose epsilon: the assertion is "this is the authored bearing",
    // not "this is bit-identical to a round trip through Basis".
    private static bool NearDegrees(Vector3 rotationDegrees, float pitch, float yaw)
        => Mathf.Abs(rotationDegrees.X - pitch) < 0.1f && Mathf.Abs(rotationDegrees.Y - yaw) < 0.1f;

    private static void LensFlareGates(TestContext ctx)
    {
        ctx.RequireData(ctx.InterpPath, $"interp.json");
        int withFlare = 0;
        foreach (var (chapter, _, _) in Census)
        {
            bool expected = chapter is "C2" or "C3";

            var slots = LensFlareRig.FlareTextureNames(ctx.InterpPath, chapter);
            ctx.Same(expected ? 4 : 0, slots.Count, $"{chapter} LensFlareTexture slots");

            string gamezPath = SessionPaths.ChapterGamez(ctx.DataRoot, chapter);
            ctx.RequireData(gamezPath, $"{chapter} gamez");
            var gamez = GameZ.Load(gamezPath);
            int sunNodes = 0;
            foreach (var n in gamez.Nodes)
            {
                if (string.Equals(n.Name, "sun", System.StringComparison.OrdinalIgnoreCase))
                    sunNodes++;
            }

            ctx.Same(expected ? 1 : 0, sunNodes, $"{chapter} gamez sun node");
            // The gates must not merely each be right — they must AGREE. A chapter with textures
            // and no sun (or the reverse) is data telling us something we have not decoded, and
            // the rig logs a warning for exactly that case.
            ctx.Check(slots.Count > 0 == sunNodes > 0, $"{chapter} both flare gates agree");
            if (expected)
            {
                withFlare++;
                ctx.Note($"{chapter} flare slots: {string.Join(",", slots)}");
            }
        }

        ctx.Same(2, withFlare, $"chapters authoring a lens flare");
        // ⚠ C2's flare is PREDICTED, not verified: the data says it has one and there is no
        // footage of it. Only C3 was ever captured.
        ctx.Note($"C2's flare is predicted from data only — no capture of the original exists");
    }

    /// <summary>The authored STOP_SEQUENCE stops must actually run — nothing else in the gate
    /// measures an effect's DURATION (<c>--effects-test</c> only proves a puffer builds; see
    /// <c>docs/verification.md</c>). Asserts on the dispatch timeline via
    /// <see cref="AnimRuntime.OnEventDispatched"/>, not on puffers, so it needs no textures:
    /// the rocket fireball's ON_CALL stopper is named by a stop while nothing runs it and must
    /// therefore stay silent, and the 30 s fire's emitting poll loop is halted — an un-halted
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
    /// <c>null</c>, so they must still all start together at t=0 — "0 and null are different
    /// authored states", asserted on real data rather than argued. A runtime that
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
    /// <para>DEBRIS (`m_build01`, a real destructible death). Its
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
        // building's key and ends the debris trail early. ⚠ That is a faithful-stage artifact, not
        // a runtime rule (docs/verification.md): omit these roots and the assertion below is
        // masked.
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
    /// answers about whichever row comes first, which lets a debris assertion pass against a
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
    /// the separate staged root <c>he_ring1</c>. A call that relocates the template but leaves it
    /// hidden means the ring never draws — the same shape as <c>sonic_ground_effect</c>'s four
    /// rising rings and the torpedo's <c>ripple</c>. Measured with <c>--effects-test</c>'s mesh
    /// census.</para>
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

    // ---- the full-screen wash reports its authored run times ------------------------------------

    /// <summary>`he_ground_effect`'s `frame_buffer_effects1` is six `FBFX_COLOR_FROM_TO` steps
    /// washing the picture white↔violet over 1.2 s, reached through
    /// `If PlayerRange 10000 → CallSequence`. The handler must report each step's authored
    /// <c>run_time</c> as its duration, because that is the only thing spacing them: report 0 and
    /// all six fire in one instant and the wash is a single frame of violet. Asserts the six
    /// ramps arrive in order with their authored run times and colours, and that the chain
    /// actually spans its authored 1.1 s from the first fire to the last.</summary>
    private static void FbfxFlash(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            WithEffectStage(ctx, world, "he_ground_effect", new[] { "he_ring", "he_ring1", "he_trails" },
                (stage, runtime, point) =>
            {
                // The authored chain, from extracted/C1/cam_anim/he_ring-he_ground_effect.json.
                var white = new Color(1f, 1f, 1f, 0.3f);
                var violet = new Color(0.2f, 0f, 1f, 0.2f);
                var wantFrom = new[] { white, violet, violet, white, violet, violet };
                var wantTo = new[] { violet, violet, white, violet, violet, white };
                var wantRun = new[] { 0.2f, 0.4f, 0.2f, 0.1f, 0.2f, 0.1f };

                const float dt = 1f / 60f;
                float clock = 0f;
                var fired = new List<(float T, Color From, Color To, float Run)>();
                runtime.ScreenFlash = (from, to, seconds) => fired.Add((clock, from, to, seconds));

                // At the camera, so the def's own PLAYER_RANGE 10000 gate passes.
                runtime.PlayEffectAt("he_ground_effect", point);
                for (int i = 0; i < 150; i++)
                {
                    clock += dt;
                    runtime.Advance(dt);
                }

                string times = string.Join(", ", fired.Select(f => $"{f.T:0.####}s (run {f.Run:0.##})"));
                ctx.Note($"the wash fired at {times}");
                ctx.Check(fired.Count == 6, $"the six FBFX_COLOR_FROM_TO steps all fired ({fired.Count})");
                if (fired.Count != 6)
                    return;
                for (int i = 0; i < 6; i++)
                {
                    ctx.Check(Mathf.IsEqualApprox(fired[i].Run, wantRun[i]),
                        $"step {i + 1} reports its authored run time ({fired[i].Run:0.###} s, want {wantRun[i]:0.###})");
                    ctx.Check(fired[i].From.IsEqualApprox(wantFrom[i]) && fired[i].To.IsEqualApprox(wantTo[i]),
                        $"step {i + 1} ramps its authored colours ({fired[i].From} → {fired[i].To})");
                }
                // Each step must start one previous run time after the one before it — the
                // collapse this suite exists to catch, which no per-step assertion above can see.
                for (int i = 1; i < 6; i++)
                {
                    float gap = fired[i].T - fired[i - 1].T;
                    ctx.Check(Mathf.Abs(gap - wantRun[i - 1]) <= 2f * dt,
                        $"step {i + 1} waits step {i}'s run time ({gap:0.###} s, want {wantRun[i - 1]:0.###})");
                }
                // One step of headroom per gap: an authored run time is an exact multiple of the
                // step here, but neither it nor the accumulated clock is exact in binary float, so
                // a gate can miss by a frame and the misses do not cancel. Measured: three of the
                // five gaps land one step late, 0.05 s over the chain.
                ctx.Check(Mathf.Abs((fired[5].T - fired[0].T) - 1.1f) <= 5f * dt,
                    $"the chain spans its authored 1.1 s first-to-last fire ({fired[5].T - fired[0].T:0.###} s)");
            });
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
            // The camera point, so the gun family's PLAYER_RANGE 500 condition passes — a probe
            // standing off further than that builds a def that renders nothing.
            body(stage, runtime, ctx.Camera.GlobalPosition);
        }
        finally
        {
            runtime.Free();
            stage.Free();
        }
    }

    // ---- three ordnance bursts, played end to end against their authored timelines -------------

    /// <summary>Plays `he_ground_effect`, `flash_effect` and
    /// `sonic_ground_effect` end to end on a fixed-dt clock and asserts each one's FULL event
    /// timeline against the authored JSON: every sequence entered, every event fired, in its
    /// sequence's order, at its authored instant. Membership alone would pass a broken scheduler,
    /// so nothing here is a membership check. See <c>docs/org/sequences.md</c>.
    ///
    /// <para>These three between them exercise every scheduler divergence from the original.
    /// `he_ground_effect` reaches `large_fireball`, whose `STOP_SEQUENCE stop_p1trail` names a
    /// parked ON_CALL sequence nothing ever called — the stop must halt nothing and START nothing,
    /// so that lane's two events must never appear (16 shipped definitions author
    /// exactly this, in every chapter, inside the HE explosion's chain). `sonic_ground_effect`
    /// calls `sonic_light_seq` twice, which is the one-runner-per-sequence identity: exactly two
    /// passes, the second beginning at the authored 1.2 s. Both carry `LightAnimation` run-time
    /// chains, which is what the event-timer origin schedules, and both gate on the
    /// instance clock (`START_TIME ANIMATION`) — sonic at 1.2 s, flash at 1.5 s.
    /// `he_ground_effect`'s `frame_buffer_effects1` is the six-step wash, reached through an
    /// `If PlayerRange 10000`. `flash_effect` is the third because it is the
    /// pure light/no-particle case, so it isolates the scheduler from the emitter.</para>
    ///
    /// <para>⚠ The staged roots are DERIVED (<c>EffectCatalogue.StageRootsFor</c>), never a hand
    /// list, and the derivation throws on an anchor that resolves nowhere. That is the failure mode
    /// `docs/formats/weapon-effects.md` records: a def whose anchor root was not staged plays NOTHING,
    /// silently, and a suite that asserted only "these events fired" would have nothing to say
    /// about the ones that did not. The `PufferState(no host node)` tally is checked for the same
    /// reason — it is the tell that a template staged but did not resolve.</para>
    ///
    /// <para>⚠ This suite is NOT `--effects-test`: it must not inherit that probe's 0.3 s instance
    /// TTL or its 0.1 s per-name throttle, which exist for the gun path. The TTL here is
    /// <see cref="BurstTtl"/>, an order of magnitude past the longest burst, and each burst is
    /// played once — a 1.2 s wash truncated at 0.3 s would "pass" short.</para></summary>
    private static void OrdnanceBurstTimeline(TestContext ctx)
    {
        var report = new System.Text.StringBuilder();
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            HeBurstTimeline(ctx, world, report);
            FlashBurstTimeline(ctx, world, report);
            SonicBurstTimeline(ctx, world, report);
        });
        ctx.WriteArtifact("ordnance-burst-timeline.txt", report.ToString());
    }

    /// <summary>`he_ring-he_ground_effect.json` — five sequences, two of them unnamed and Initial,
    /// three ON_CALL — plus `flame_ball_01-large_fireball.json`, which its fourth CALL_ANIMATION
    /// reaches and which carries the parked-stopper case.</summary>
    private static void HeBurstTimeline(TestContext ctx, TestWorld world, System.Text.StringBuilder report)
    {
        // The first unnamed Initial sequence: eight events, none carrying a start, so the whole
        // burst fires in the instant the effect starts.
        var opening = new BurstLane("", new[]
        {
            new BurstStep(0, "CallSequence", "he_light_seq", 0f),
            new BurstStep(1, "CallAnimation", "call_he_ring", 0f),
            new BurstStep(2, "CallAnimation", "call_hetrails_up", 0f),
            new BurstStep(3, "CallAnimation", "large_fireball", 0f),
            new BurstStep(4, "CallAnimation", "call_he_ring1", 0f),
            new BurstStep(5, "Sound", "ground_mixed_exp_sg", 0f),
            new BurstStep(6, "CallAnimation", "call_hetrails_up", 0f),
            new BurstStep(7, "CallSequence", "he_flashes", 0f),
        });
        // The second unnamed Initial sequence: `If PlayerRange 10000 / CallSequence / Endif`. The
        // IF and the ENDIF are control flow the runner interprets itself and never dispatches, so
        // #1 is the only row this lane can produce — and it produces it only because the burst is
        // played at the camera, which is what makes the range condition true.
        var fbfxGate = new BurstLane("", new[]
        {
            new BurstStep(1, "CallSequence", "frame_buffer_effects1", 0f),
        });
        // he_light_seq: LIGHT_STATE on, then six LIGHT_ANIMATION ramps whose run times chain
        // (0.05, 0.025, 0.05, 0.025 → 0.15), one `Event + 0.2` gap, then 0.05 and 0.01, then off.
        var lightSeq = new BurstLane("he_light_seq", new[]
        {
            new BurstStep(0, "LightState", "he_light", 0f),
            new BurstStep(1, "LightAnimation", "he_light", 0f),
            new BurstStep(2, "LightAnimation", "he_light", 0.05f),
            new BurstStep(3, "LightAnimation", "he_light", 0.075f),
            new BurstStep(4, "LightAnimation", "he_light", 0.125f),
            new BurstStep(5, "LightAnimation", "he_light", 0.35f),
            new BurstStep(6, "LightAnimation", "he_light", 0.4f),
            new BurstStep(7, "LightState", "he_light", 0.41f),
        });
        var flashes = new BurstLane("he_flashes", new[]
        {
            new BurstStep(0, "LightState", "he_light1", 0f),
            new BurstStep(1, "LightAnimation", "he_light1", 0f),
            new BurstStep(2, "LightAnimation", "he_light1", 0.1f),
            new BurstStep(3, "LightState", "he_light1", 0.6f),
        });
        // The wash: six FBFX_COLOR_FROM_TO steps of 0.2/0.4/0.2/0.1/0.2/0.1 s. A handler
        // reporting 0 as its duration fires all six in one instant, which is what the times here
        // refuse. (`fbfx-flash` asserts the colours and run times; this asserts their place in the
        // burst.)
        var wash = new BurstLane("frame_buffer_effects1", new[]
        {
            new BurstStep(0, "FbfxColorFromTo", null, 0f),
            new BurstStep(1, "FbfxColorFromTo", null, 0.2f),
            new BurstStep(2, "FbfxColorFromTo", null, 0.6f),
            new BurstStep(3, "FbfxColorFromTo", null, 0.8f),
            new BurstStep(4, "FbfxColorFromTo", null, 0.9f),
            new BurstStep(5, "FbfxColorFromTo", null, 1.1f),
        });
        // The callee. `activate_puffer` shows the fireball, calls `p1trail` (which starts the
        // emitter) and then, at an authored `Event + 0.3`, STOPs `stop_p1trail` — an ON_CALL
        // sequence nothing has called, so it is parked and the stop halts nothing.
        var fireball = new[]
        {
            new BurstLane("activate_puffer", new[]
            {
                new BurstStep(0, "ObjectActiveState", "flame_ball_01", 0f),
                new BurstStep(1, "CallSequence", "p1trail", 0f),
                new BurstStep(2, "StopSequence", "stop_p1trail", 0.3f),
            }),
            new BurstLane("p1trail", new[]
            {
                new BurstStep(0, "PufferState", "fierypuffer", 0f),
            }),
        };

        WithBurst(ctx, world, "he_ground_effect", report, fired =>
        {
            CheckLanes(ctx, "he_ground_effect", fired, new[] { opening, fbfxGate, lightSeq, flashes, wash }, report);
            CheckLanes(ctx, "large_fireball", fired, fireball, report);
            // The parked stopper, and the reason `large_fireball` is asserted here at all. Its
            // `p1trail` lane above is the live control: without it, "the stopper fired nothing" is also what a
            // fireball that never started reports.
            int stopper = fired.Count(f => f.Anim == "large_fireball" && f.Sequence == "stop_p1trail");
            ctx.Check(stopper == 0,
                $"large_fireball's parked stop_p1trail dispatched nothing — a STOP_SEQUENCE halts and never starts (B12) ({stopper} event(s))");
        });
    }

    /// <summary>`flash_control-flash_effect.json` — the pure light case, two sequences. The one
    /// timed event in it is a `START_TIME ANIMATION 1.5`, read against the instance clock.</summary>
    private static void FlashBurstTimeline(TestContext ctx, TestWorld world, System.Text.StringBuilder report)
    {
        var lanes = new[]
        {
            new BurstLane("", new[]
            {
                new BurstStep(0, "ObjectActiveState", "lens_flash", 0f),
                new BurstStep(1, "CallSequence", "flash_flashes", 0f),
                new BurstStep(2, "CallAnimation", "call_flasher", 0f),
                // `Animation + 1.5` — the instance clock, which for this Initial sequence is also
                // its own, so the value is the assertion and the origin is not (sonic's second
                // `sonic_light_seq` pass is where the two clocks differ).
                new BurstStep(3, "ObjectActiveState", "lens_flash", 1.5f),
            }),
            new BurstLane("flash_flashes", new[]
            {
                new BurstStep(0, "LightState", "flash_light1", 0f),
                new BurstStep(1, "LightAnimation", "flash_light1", 0f),
                new BurstStep(2, "LightAnimation", "flash_light1", 0.5f),
                new BurstStep(3, "LightState", "flash_light1", 0.75f),
            }),
        };
        WithBurst(ctx, world, "flash_effect", report,
            fired => CheckLanes(ctx, "flash_effect", fired, lanes, report));
    }

    /// <summary>`sonic_effect-sonic_ground_effect.json` — four sequences, two unnamed and Initial.
    /// The repeat-call case: the first Initial sequence calls `sonic_light_seq` at #0 and again at #4,
    /// 1.2 s later.</summary>
    private static void SonicBurstTimeline(TestContext ctx, TestWorld world, System.Text.StringBuilder report)
    {
        // The 15-event Initial sequence. #3 carries `START_TIME ANIMATION 1.2`; everything behind
        // it is untimed, so the whole second half fires in that instant.
        var main = new BurstLane("", new[]
        {
            new BurstStep(0, "CallSequence", "sonic_light_seq", 0f),
            new BurstStep(1, "CallAnimation", "sonic_puff1", 0f),
            new BurstStep(2, "CallAnimation", "call_flare", 0f),
            new BurstStep(3, "CallAnimation", "sonic_puff4", 1.2f),
            new BurstStep(4, "CallSequence", "sonic_light_seq", 1.2f),
            new BurstStep(5, "CallSequence", "sonic_growlight", 1.2f),
            new BurstStep(6, "CallAnimation", "call_flare", 1.2f),
            new BurstStep(7, "CallAnimation", "sonic_puff5", 1.2f),
            new BurstStep(8, "CallAnimation", "sonic_puff6", 1.2f),
            new BurstStep(9, "CallAnimation", "sonic_puff7", 1.2f),
            new BurstStep(10, "CallAnimation", "sonic_puff8", 1.2f),
            new BurstStep(11, "CallAnimation", "sonic_puff9", 1.2f),
            new BurstStep(12, "CallAnimation", "sonic_puff10", 1.2f),
            new BurstStep(13, "CallAnimation", "sonic_puff11", 1.2f),
            new BurstStep(14, "CallAnimation", "sonic_emit_downer", 1.2f),
        });
        // The second Initial sequence: four rising rings at once, then one at `START_TIME
        // SEQUENCE 1.2` — an absolute gate against this sequence's own clock, not the instance's.
        var rings = new BurstLane("", new[]
        {
            new BurstStep(0, "CallAnimation", "ring_up1", 0f),
            new BurstStep(1, "CallAnimation", "ring_up2", 0f),
            new BurstStep(2, "CallAnimation", "ring_up3", 0f),
            new BurstStep(3, "CallAnimation", "ring_up4", 0f),
            new BurstStep(4, "CallAnimation", "ring_down1", 1.2f),
        });
        // Two passes of ONE sequence, 1.2 s apart. The first must have ended (its
        // LIGHT_ANIMATION runs 0.3 s) for the second call to find it parked and restart it —
        // a call into a running sequence is a no-op, and a second concurrent copy is not a thing
        // the original can express.
        BurstLane LightPass(float from) => new("sonic_light_seq", new[]
        {
            new BurstStep(0, "LightState", "sonic_light", from),
            new BurstStep(1, "LightAnimation", "sonic_light", from),
            new BurstStep(2, "LightState", "sonic_light", from + 0.3f),
        });
        var grow = new BurstLane("sonic_growlight", new[]
        {
            new BurstStep(0, "LightState", "sonic_light1", 1.2f),
            new BurstStep(1, "LightAnimation", "sonic_light1", 1.2f),
            new BurstStep(2, "LightAnimation", "sonic_light1", 2.4f),
            new BurstStep(3, "LightState", "sonic_light1", 3.2f),
        });
        WithBurst(ctx, world, "sonic_ground_effect", report, fired =>
        {
            CheckLanes(ctx, "sonic_ground_effect", fired,
                new[] { main, rings, LightPass(0f), LightPass(1.2f), grow }, report);
            // Stated on its own as well as through the lanes: the lane pair above proves the two
            // passes ran, and this proves nothing else did. A third pass would be a call that
            // found the sequence parked when the original would not have.
            int passes = fired.Count(f => f.Anim == "sonic_ground_effect"
                                          && f.Sequence == "sonic_light_seq" && f.Index == 0);
            ctx.Same(2, passes, $"sonic_light_seq's two authored calls started it exactly twice (B11)");
        });
    }

    /// <summary>Plays one burst on its own miniature world-effects stage and hands the recorded
    /// dispatch log to <paramref name="body"/>.
    ///
    /// <para>The stage's template ROOTS are derived from the definition's own CALL_ANIMATION
    /// closure against the chapter gamez — the same derivation the production bind runs — so a
    /// definition whose anchor resolves nowhere throws here, naming it, instead of quietly playing
    /// nothing. Everything else is the production world-effects role:
    /// <c>TemplateStage.Pooled</c> + <c>Shown</c> + relocate-on-call, one pool slot, and the camera
    /// as the player position so a `PLAYER_RANGE` gate reads the burst as close.</para></summary>
    private static void WithBurst(TestContext ctx, TestWorld world, string animName,
        System.Text.StringBuilder report, System.Action<IReadOnlyList<BurstFire>> body)
    {
        var roots = Session.EffectCatalogue.StageRootsFor(world.Session.Program, new[] { animName },
            Session.WorldEffectsFactory.StageRootResolver(world.Gamez));
        ctx.Check(roots.Count > 0,
            $"{animName}: its call closure's anchor roots derived ({roots.Count}: {string.Join(", ", roots)})");
        var stage = new Node3D { Name = $"BurstStage_{animName}" };
        var pool = new Node3D { Name = "pool0" };
        pool.SetMeta(AnimRuntime.PoolSlotMeta, 0);
        stage.AddChild(pool);
        int built = Session.WorldEffectsFactory.BuildEffectStage(world.Gamez,
            world.Session.Builder.Scene, pool, roots);
        ctx.Same(roots.Count, built, $"{animName}: template roots staged from the chapter gamez");
        foreach (var child in pool.GetChildren())
        {
            if (child is Node3D root)
            {
                root.Visible = false;
            }
        }

        var runtime = AnimRuntime.ForEffects(
            AnimRuntime.NewTemplateStage(pooled: true, shown: true, placesCalled: true),
            1, new CountingEmitterFactory(), false, BurstTtl,
            () => ctx.Camera.GlobalPosition);
        runtime.ManualAdvance = true;
        ctx.Host.AddChild(stage);
        ctx.Host.AddChild(runtime);
        try
        {
            runtime.Bind(stage, world.Session.Program.Subset(animName));
            var fired = new List<BurstFire>();
            float clock = 0f;
            runtime.OnEventDispatched = d => fired.Add(new BurstFire(clock,
                d.Def.AnimName ?? d.Def.Name, d.Sequence, d.EventIndex, d.EventKind, d.EventName));
            // At the camera, so the definitions' own PLAYER_RANGE gates pass.
            ctx.Check(runtime.PlayEffectAt(animName, ctx.Camera.GlobalPosition),
                $"{animName} resolved to a definition and started");
            int steps = Mathf.RoundToInt(BurstSeconds / BurstDt);
            for (int i = 0; i < steps; i++)
            {
                clock += BurstDt;
                runtime.Advance(BurstDt);
            }

            runtime.OnEventDispatched = null;
            runtime.UnhandledEventCounts.TryGetValue("PufferState(no host node)", out int hostless);
            ctx.Same(0, hostless, $"{animName}: PUFFER_STATE events that found no host node");
            ctx.Note($"{animName}: {fired.Count} dispatch(es) over {BurstSeconds:0.#} s at {BurstDt:0.####} s steps");
            body(fired);
        }
        finally
        {
            runtime.Free();
            stage.Free();
        }
    }

    /// <summary>Matches a definition's recorded dispatches against its authored lanes and asserts
    /// both halves of "the timeline is right": ORDER (each lane's rows arrive in the sequence's own
    /// order, and nothing arrives that no lane authored) and TIME (each row lands on its authored
    /// instant, within <see cref="BurstSlack"/>).
    ///
    /// <para>A row is claimed by the first lane whose next unconsumed step it matches on
    /// (sequence, index, kind, name). Claiming in order is what makes this an order assertion: a
    /// row that arrives early or twice matches no lane's PENDING step and is reported as stray.
    /// The four-part key is needed because a definition's sequence NAMES are not unique — both
    /// bursts here ship two unnamed Initial sequences — and the key is verified unique against the
    /// JSON for all three.</para></summary>
    private static void CheckLanes(TestContext ctx, string animName, IReadOnlyList<BurstFire> fired,
        BurstLane[] lanes, System.Text.StringBuilder report)
    {
        var own = fired.Where(f => f.Anim == animName).ToList();
        report.AppendLine($"--- {animName}: {own.Count} dispatch(es) ---");
        foreach (var f in own)
        {
            report.AppendLine($"  {f.T,7:0.0000}s  [{(f.Sequence.Length == 0 ? "<unnamed>" : f.Sequence)}] "
                              + $"#{f.Index} {f.Kind} {f.Name}");
        }

        var cursor = new int[lanes.Length];
        var at = new float[lanes.Length][];
        for (int i = 0; i < lanes.Length; i++)
        {
            at[i] = new float[lanes[i].Steps.Length];
        }

        var stray = new List<BurstFire>();
        foreach (var f in own)
        {
            int lane = -1;
            for (int l = 0; l < lanes.Length && lane < 0; l++)
            {
                if (cursor[l] >= lanes[l].Steps.Length)
                {
                    continue;
                }
                var step = lanes[l].Steps[cursor[l]];
                if (lanes[l].Sequence == f.Sequence && step.Index == f.Index
                    && step.Kind == f.Kind && step.Name == f.Name)
                {
                    lane = l;
                }
            }
            if (lane < 0)
            {
                stray.Add(f);
                continue;
            }
            at[lane][cursor[lane]] = f.T;
            cursor[lane]++;
        }

        string strays = string.Join(", ",
            stray.Select(s => $"{s.T:0.###}s [{s.Sequence}] #{s.Index} {s.Kind} {s.Name}"));
        ctx.Check(stray.Count == 0,
            $"{animName}: every dispatch is an authored event arriving in its sequence's order ({stray.Count} stray: {strays})");
        for (int l = 0; l < lanes.Length; l++)
        {
            var lane = lanes[l];
            string tag = $"{animName} [{(lane.Sequence.Length == 0 ? "<unnamed>" : lane.Sequence)}]";
            ctx.Same(lane.Steps.Length, cursor[l], $"{tag}: authored events fired, in order");
            for (int s = 0; s < cursor[l]; s++)
            {
                var step = lane.Steps[s];
                ctx.Check(Mathf.Abs(at[l][s] - step.At) <= BurstSlack,
                    $"{tag} #{step.Index} {step.Kind} fires at its authored {step.At:0.###} s ({at[l][s]:0.###} s)");
            }
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
    /// crash-rig subset, and asserts the two things that go wrong in flight. (1) The
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
                                  && planeModel.Visible,
                            $"{model}: the airframe model is still parented, placed and visible after the tear");

                        // Crash → respawn → move → crash again: the wreck and every template a
                        // crash reveals must play at the SECOND crash's site. The failure looks
                        // like the destroyed plane and the dirt burst replaying at the FIRST
                        // crash's position on every crash after the first.
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

    // ---- an AI plane's crash picks from the ai_crash_* vector (G21) ----------------------------

    /// <summary>The AI arm of the crash-family split, through the REAL factory call
    /// (<c>WorldEffectsFactory.BuildFlightCrashRuntime</c> keys the family on
    /// <c>IsHumanPiloted</c>): an AI controller's rig binds the <c>ai_crash_*</c> vector (three
    /// playable slots against every shipped chapter), a crash on a body stamped <c>dirt</c>(13)
    /// selects <c>ai_crash_dirt</c>, and a crash with no struck body takes the null-material arm
    /// to slot 0, <c>ai_crash_default</c> — never a <c>player_crash_*</c> def. The A/B control is
    /// a human-piloted controller through the same factory, which keeps the player family; without
    /// it a family mix-up in the pick would be invisible from the AI side alone.</summary>
    private static void AiCrashDefs(TestContext ctx)
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
                    PlayerIndex = AiAircraftSpawner.ShooterIdBase,
                    IsHumanPiloted = false,
                    Pilot = AiPilot.HoldingCourse(spawn, spawn + Vector3.Forward),
                    UseKeyboard = false,
                    PadDevices = System.Array.Empty<int>(),
                    AllowPause = false,
                };
                ai.AddChild(aiModel);
                ai.Setup(new FlightModel(stats), null, new CamParams(), spawn, spawn + Vector3.Forward);
                ctx.Host.AddChild(ai);
                factory.BuildFlightCrashRuntime(ai, builder, ctx.PlaneName, world.Gamez,
                    world.Session.Builder.Scene, textures, world.Session.Program, verbose: false);

                ctx.Check(ai.CrashRuntime != null && ai.CrashDefs != null,
                    $"the AI rig built a crash runtime with a def table");
                if (ai.CrashDefs == null)
                    return;
                ctx.Check(ai.CrashDefs.PlayableDefs.Count == 3
                          && ai.CrashDefs.PlayableDefs.All(d =>
                              d.StartsWith(Session.EffectCatalogue.AiCrashDefPrefix, System.StringComparison.Ordinal)),
                    $"the AI table's playable slots are the ai_crash_* trio [{string.Join(", ", ai.CrashDefs.PlayableDefs)}]");

                // A crash on a known surface: a struck body stamped dirt(13) — the id cascade's
                // own-slot arm, through the production Crash path.
                dirt = new StaticBody3D { Name = "dirt_probe" };
                dirt.SetMeta(SceneBuilder.SurfaceIdMeta, 13);
                ctx.Host.AddChild(dirt);
                ai.DebugForceCrash(null, dirt);
                ctx.Check(ai.Crashed && ai.LastCrashDef == Session.EffectCatalogue.AiCrashDefPrefix + "dirt",
                    $"an AI crash on dirt(13) plays ai_crash_dirt def={ai.LastCrashDef ?? "-"}");

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
                    world.Session.Builder.Scene, textures, world.Session.Program, verbose: false);
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

    // ---- the full effects sweep as suite verdicts ----------------------------------------------

    /// <summary>The whole `--effects-test` sweep, asserted instead of read: every one of
    /// <c>EffectCatalogue.EffectAnimNames</c> played through <see cref="Probes.Effects"/> on a
    /// full replica stage. The census's two sweep-wide verdicts are otherwise only `.scratch` text
    /// nobody reads, however many effects the probe says resolved; this makes them fail a build. The play
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

    // ---- contact: the flight ends where the world says ------------------------------------------

    /// <summary>A gravity-bearing OBJECT_MOTION must be cut short by real geometry instead of
    /// running its authored <c>RUN_TIME</c> out below the terrain, through whichever of the two
    /// tiers its flags select, and must pick its <c>BOUNCE_SEQUENCE</c> branch from the surface it
    /// struck. Tier selection is asserted as well as the landing, since the two tiers are one
    /// mechanism apiece and the wrong one lands a piece in the wrong place.
    ///
    /// <para>Driven as a synthetic body rather than off a chapter's own debris, deliberately. The
    /// reachable carriers reach their launch through a death sequence and a randomised draw, so the
    /// launch is authored by hand and thrown downward from a known height at real chapter geometry.
    /// The flight time, the resting height and the branch are then exactly predictable.</para>
    ///
    /// <para>⚠ The control is the last case: the same bodies with no mask handed over run their
    /// full 20 s and end far below the surface. Without it, a suite that never fired a query at all
    /// would still pass its landing checks on a body that simply had not got anywhere yet, and it
    /// is also the assertion that the no-collision-world fallback runs its authored clock rather
    /// than being merely untested.</para>
    ///
    /// <para>⚠ Shown able to fail by disabling each tier's landing test in turn: the matching case
    /// then reports the same 20.02 s and −2000 m as the unmasked control. It is NOT able to fail on
    /// the sweep's arming rule, because arming is distance OR time and <c>ArmSeconds</c> still arms
    /// the body at 0.1 s, long before a 60 m drop reaches anything. Arming is what the cockpit
    /// checks cover; this suite covers tier selection, truncation, resting height, branch choice
    /// and the fallback.</para></summary>
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

            // `flagged: false` is the DEFAULT authoring — 1,466 events install-wide — which selects
            // the ground column; `noAltitude` is the opt-out that selects neither.
            // `timed: false` omits run_time the way 296 ballistic events install-wide do, which is
            // the shape C8's watchdog bounds; it is thrown UPWARD so the parabola has an apex to
            // report to the sequence.
            AnimData Body(bool flagged = true, bool noAltitude = false, bool complex = true,
                bool timed = true)
            {
                var props = new Dictionary<string, object?>
                {
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

            // Runs one body to a stop and reports what happened to it. `waterHook` stands in for
            // the session's ProjectilePool.SurfaceIsWater binding — the surface-id read has its own
            // coverage, and stubbing it is what makes the branch choice assertable without needing
            // a chapter with reachable sea.
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

            // 1f — an UNTIMED launch, the shape C8 re-terminates. It must land on the column like
            // any other body, and it must keep REPORTING its parabola rather than its watchdog:
            // the reported number is what the sequence waits on, so a body reporting 15 s here
            // would leave every vanish-shape piece on screen for 15 s (BL-257) and divide its
            // tumble by the same figure.
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

            // 1e — the veto on REAL extracted data. Every case above builds its gravity block by
            // hand, which pins the branch but not that `no_altitude` survives extraction and
            // reaches Create at all: the engine had never read that field before C7. gunshell is
            // its only author install-wide, 8 events, one per chapter, and it is reachable because
            // muzzleburst_effects CallAnimations it.
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

            // 2b — the bounce is a CONTINUATION. The sequence a landing dispatches re-launches the
            // very node that landed (`pNhit` throws `pieceN` on again), and MotionRuntime.Create
            // ordinarily re-homes a ballistic launch to the node's AUTHORED rest pose. Left alone,
            // that teleports the piece back to where the wreck was, which shows as the crash
            // "jumping back to the crash point" once per piece. The follow-up must start from
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
                    // The follow-up is built the way `pNhit` authors one: the SAME body with the
                    // flag OFF. It must still resume from the landing AND still be contact-tested —
                    // otherwise it runs its whole clock and buries the piece (3t − 4.9t² is 107 m
                    // under the airfield at t=5 — the "plane went through the ground" symptom).
                    // ⚠ It is tested because the default tier tests it, like every other unflagged
                    // gravity body. A judged inheritance used to do that job (a hop carried the
                    // sweep over from the landing it continued), and the default column dissolves
                    // the case it existed for, so it is gone rather than kept alongside.
                    var settle = Body(flagged: false);
                    // A dive hands the crash rig a large downward momentum. The FIRST launch spends
                    // it; a hop off the ground must not be handed it again, or it covers the 2 m
                    // arming epsilon in 0.044 s and is under the terrain before the sweep can look.
                    var inheritWas = runtime.InheritedWorldVelocity;
                    runtime.InheritedWorldVelocity = new Vector3(0f, -45f, 0f);
                    var second = MotionRuntime.Create(runtime, node, settle, Authored);
                    runtime.InheritedWorldVelocity = inheritWas;
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

                    // And a plain launch on a node that did NOT just land takes the same tier — the
                    // resume mark buys momentum and re-homing rules, never a different contact
                    // mechanism. A body that reported Sweep here would mean the retired
                    // inheritance had grown back somewhere.
                    var elsewhere = new Node3D { Name = "ground-contact-unflagged" };
                    root.AddChild(elsewhere);
                    elsewhere.GlobalPosition = restPose;
                    var plain = MotionRuntime.Create(runtime, elsewhere, settle, Authored);
                    ctx.Check(plain is { ContactTier: MotionContactTier.Column },
                        $"a false-flagged launch that continues nothing takes the column too tier={plain?.ContactTier}");
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
            // ⚠ Two decoded rules are deliberately NOT asserted here, because asserting them would
            // claim coverage the suite does not have. With a downward column, the descending-step
            // admission and `complex`'s widening of it can only save the query, never change the
            // outcome: a step that ends higher than it starts cannot end below a surface the ray
            // found at or under its start. Both are transcribed in TryGroundColumn. What a later
            // item must re-check is the query's shape, not those two lines.
        });
    }

    // ---- the tumble: a rate about the launch's own perpendicular ---------------------------------

    /// <summary><c>FORWARD_ROTATION</c> turns a launched body about the horizontal PERPENDICULAR of
    /// its own launch direction, at the authored rate, with that direction's own horizontal length
    /// as the scale — so the same authored number tumbles a flat throw fast and a steep one slowly,
    /// and a body launched by the vector <c>translation</c> form does not turn at all.
    ///
    /// <para>Every case is arithmetic on a synthetic body: the ranges are authored min = max so the
    /// draw is deterministic, and the pose is read back as geometry (where the body's own axes point
    /// after a quarter turn) rather than as the euler triple the implementation writes, which would
    /// assert nothing.</para>
    ///
    /// <para>⚠ The last case is the one that came from the controls. A crash piece flies the vector
    /// form, whose launch never fills the direction cache the tumble reads, so it must hold its
    /// orientation exactly — this is the case that fails if the axis is ever "fixed" back to a mesh
    /// axis.</para></summary>
    private static void ForwardRotation(TestContext ctx)
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

    /// <summary><c>MAIN_ROOT_NODE</c> / <c>INPUT_NODE</c> mean "the node this definition was
    /// invoked on" — a sentinel, not a name, so a resolver that only matches names finds nothing
    /// and drops the event without a word. 154 events install-wide carry it under a key
    /// <c>AnimRuntime.Targets</c> resolves, and <b>all 90 of the OBJECT_MOTION ones author
    /// <c>do_intersections: true</c></b>: the eleven airframes' whole-hull fall (8 chapters × 11,
    /// unreachable today — nothing kills an AI plane) and <c>agyrobus</c>' two, which are reachable.
    /// A name-only resolver therefore leaves the whole self-referencing population unlaunched.
    ///
    /// <para>C5's <c>agyrobus</c> is the whole test, because it is the only carrier a player can
    /// reach and because it also fixes the second half of the bug. The bus has no placement of its
    /// own — the world leaves it at the map origin and <c>agbus_fly</c>'s looping SI script flies
    /// it — so its "authored rest" is the origin, and a launch that re-homes to rest teleports the
    /// wreck kilometres away. The launch must instead take the node over from the live playback.
    /// Both halves are asserted here, and the second is why this cannot be a data-only check.</para>
    ///
    /// <para>⚠ Branch-agnostic on purpose. <c>randomdestseq</c> opens with
    /// <c>IF RandomWeight(0.5)</c>: one branch drops the whole hull on a 20 s run time, the other
    /// drops it on 3 s and breaks it into four pieces, and only the second issues
    /// <c>STOP_ANIMATION agbus_fly</c>. Which one this seed draws depends on how many times the
    /// shared <c>anim</c> stream has been drawn from before this suite runs, so every assertion
    /// here is true of BOTH: the sentinel resolved, the launch started where the bus was, and the
    /// fly script stopped driving the root. Where it comes to rest is <c>ground-contact</c>'s
    /// question, not this one.</para></summary>
    private static void SelfRefLaunch(TestContext ctx)
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
                // TWO frames, and the second is load-bearing: the death's sequence runs during an
                // Advance, AFTER that frame's motions have ticked, so one frame in the launch is
                // registered but has not yet written a pose — the node still carries the SI
                // script's last write and a re-homed launch would look like it had not moved.
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
    /// suite's claims are motion and dispatch, neither of which reads the emitter factory; that census
    /// is <c>emitter-lifetime</c>'s job.</para></summary>
    private static void BounceLaunch(TestContext ctx)
    {
        // extracted/C1/cam_anim/refuel1-refuel1-healthy.json, the two bounce-terminated events.
        // t = 2·v0y/|g| = 2·speed·(elev/90)/10 over the authored ranges, so the support is closed.
        // ⚠ The elevation is LINEAR, not spherical (MotionRuntime.RangeLaunchDirection, decoded
        // from FUN_004e8fa0) — v0y is speed·elev/90, not speed·sin(elev), which is why these bands
        // sit ~23 % lower than the sin() ones they replaced:
        //   part3  speed 18…22  elev 60…70°  g −10  →  2.400 … 3.422 s
        //   part4  speed 28…36  elev 35…50°  g −10  →  2.178 … 4.000 s
        // A landing is detected on a frame boundary, so the observed time can run one tick long.
        const float Part3Min = 2.400f, Part3Max = 3.422f;
        const float Part4Min = 2.178f, Part4Max = 4.000f;
        const float Tick = 1f / 60f;
        const string chapter = "C1";   // the only chapter shipping refuel* (5 defs)
        const string lateBounce = "ObjectMotion(bounce landed after its instance ended)";

        // The bands above are derived from the AUTHORED speed/elevation ranges, because what this
        // suite asserts is the DECODE: that a bounce-terminated body flies the parabola the data
        // describes. That used to need pinning against a global launch tune sitting on top of the
        // authored arc; the tune is gone, so the authored arc IS what flies and the bands apply
        // directly.
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
            // runtime-wide read here would count that neighbour's launch against this death — a
            // selector coarser than the subject asserts about something else.
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
            // buildings can — kill all seven and, without the hold, one lands after its instance
            // has ended. Which one is a coin toss (speed and elevation are per-instance draws), so the
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
    /// declined: the root IS revealed and the parts ARE self-visible, and the census's peak fold
    /// catches the tick before the hide. A gap is 0 with the solve declined and the solved flight
    /// with it, so it discriminates and a visibility count does not. Displacement is asserted
    /// beside it — a dispatched launch is not a moved piece.</para>
    ///
    /// <para>⚠ Assert a BAND (the same rule as <c>bounce-launch</c>): elevation and speed are
    /// per-instance draws, so the flight is a random variable whose support the authored ranges fix
    /// — <c>t = 2·speed·(elevation/90)/9.8</c> over elevation 10…70° and speed 17…25 m/s. ⚠ The
    /// elevation is LINEAR, not spherical (<see cref="MotionRuntime.RangeLaunchDirection"/>), so
    /// <c>v0y</c> is <c>speed·elev/90</c> and not <c>speed·sin(elev)</c>.</para>
    /// </summary>
    private static void NulledLaunch(TestContext ctx)
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

    /// <summary>Asserts every pane of a 2-, 3- and 4-player rig is a 3D audio listener.
    ///
    /// <para>The check reads trivial and is not: a fresh <c>SubViewport</c> is NOT a listener, and
    /// in splitscreen the main camera stands down (<c>GameSession.BuildRigs</c>), which takes it out
    /// of the World3D listener set — a camera joins that set on becoming current and leaves it on
    /// losing current (Godot 4.7 <c>Camera3D::_notification</c>). With no listener-enabled viewport
    /// left, <c>AudioStreamPlayer3D::_update_panning</c> finds no listener in range, clears its bus
    /// volumes, and every 3D emitter in the world is silent — with nothing logged or counted to say
    /// so. That is the state this one property prevents.</para></summary>
    private static void SplitscreenListeners(TestContext ctx)
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

    /// <summary>One authored event on an <c>ordnance-burst-timeline</c> lane: where it sits in its
    /// sequence, what it is, and the instant the JSON says it fires — a cumulative sum of the
    /// preceding events' <c>run_time</c>s and start offsets, read off the definition by hand.
    /// Never computed from the runtime, which is the whole point of the assertion.</summary>
    private readonly record struct BurstStep(int Index, string Kind, string? Name, float At);

    /// <summary>One recorded dispatch, off <see cref="AnimRuntime.OnEventDispatched"/>: the
    /// playhead instant plus the identity the seam already carries. <c>Anim</c> is the DEFINITION's
    /// animation name, so a burst's own timeline can be told apart from the timelines of the
    /// definitions its CALL_ANIMATIONs reach.</summary>
    private readonly record struct BurstFire(float T, string Anim, string Sequence, int Index,
        string Kind, string? Name);

    /// <summary>One authored sequence PASS. A pass, not a sequence: <c>sonic_ground_effect</c>
    /// calls <c>sonic_light_seq</c> from two sites 1.2 s apart, and each call is its own lane,
    /// which is how the timeline says the second call restarted a parked sequence rather than
    /// being swallowed or running a second concurrent copy.</summary>
    private sealed record BurstLane(string Sequence, BurstStep[] Steps);
}
