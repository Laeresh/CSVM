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
/// A suite whose data is absent skips rather than passing.
/// ⚠ Expected counts here are golden numbers measured against the retail install. The data is a
/// fixed input, so 48 weapon defs and 210 C1 destructibles are invariants; change one only with
/// the measurement that moved it.
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

    // The fixed step `ordnance-burst-timeline` drives its three bursts at.
    // Deliberately FOUR TIMES finer than SequenceRunner.AnimFrame: the authored gaps
    // under test go down to 0.01 s, which one 1/60 s step cannot resolve at all, and nothing in
    // those three definitions is denominated in animation frames (none of them carries a
    // `LOOP`, so the AnimFrame floor is never reached). A finer step makes every authored
    // instant a real measurement instead of a rounding.
    private const float BurstDt = 1f / 240f;

    // How far a burst's dispatch may sit from its authored instant: one step because the stamp is
    // taken after the Advance that fired it, one per level of CALL the row sits under, and one for a
    // gate landing a step late on binary float. Every authored gap in the three definitions is wider
    // than this bar two, and those two are carried by the rows behind them.
    private const float BurstSlack = 6f * BurstDt;

    // How long each burst is driven for — past the last authored event of its longest
    // lane, with room for the lag above. Sonic is the long one: its `sonic_growlight` ends at
    // an authored 3.2 s.
    private const float BurstSeconds = 3.5f;

    // The burst suite's instance TTL — an order of magnitude past the longest burst, so
    // the bound never truncates a timeline. Explicitly NOT `--effects-test`'s 0.3 s, which
    // exists for the gun path and would cut the 1.2 s wash off at 0.3 s while every remaining
    // assertion still passed.
    private const float BurstTtl = 32f;

    // Gun-group slots Loadout.ForRig seats on any airframe — the
    // weapon bench fires every gun from all of them, so a drop here would quietly shrink its
    // coverage without changing the 48/48 line.
    private const int RigGunGroups = 4;

    // Destructible instances / distinct node groups per chapter, at each chapter's default mission.
    // Instances exceed node groups where a reader wildcard def and its compiled per-instance twin
    // bind the same nodes. Both columns sit far below plain NAME matching because a compiled def
    // binds the one instance its symbol table names (AnimRuntime.Anchors).
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

    // C1 textures spanning the three alpha classes the flatten must leave alone: opaque,
    // hard cutout, and the soft overlays the builder alpha-blends.
    private static readonly string[] DropInSamples =
    {
        "lkzepskin", "grass1", "cloudlayer", "sky1", // no alpha channel
        "firtree1", "bush1",                          // hard cutouts
        "abld_shadow",                                // soft baked shadow overlay
    };

    public static void Register(List<TestHarness.Suite> into)
    {
        // ⚠ Keep this registered first. It is the only suite installing a fake IEmitterFactory, and
        // WithWorld caches one world per chapter, so it must build the shared C1 world while the fake is
        // in effect; damage-hd's collision:true immediately after forces the real rebuild for everyone.
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
            + "bands of C3's spew_puffer and volcanosmoke — the cross-wire included (C7), and the "
            + "most-favourable-pane rule every viewer gets an answer from (B11)",
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
        into.Add(new TestHarness.Suite("launch-velocity-decay",
            "a LOCK_ON round carries its launcher's velocity and sheds it linearly over LOCK_ON " +
            "seconds (BL-290): wep_14 launched at 120 m/s leaves at 180, reads 120 at half the " +
            "window and holds its authored 60 from 2.5 s on, while a slow launch barely changes; " +
            "the decay runs for a LOCK_ON round holding NO target too (the recorded divergence " +
            "from the original's target-gated step); and neither a gun nor a rocket without " +
            "LOCK_ON changes",
            LaunchVelocityDecay));
        into.Add(new TestHarness.Suite("smoke-screen",
            "a smoke screen laid through SmokeScreens.Lay on a live roster (D18): the AI directly " +
            "behind the layer inside 600 m is stunned for the whole 8 s TIME with its stun refreshed " +
            "every step and recovers after the screen expires, an AI off to the side beyond 85° is " +
            "never touched, the human behind gets the grey-green wash on its own pane at 0.97 then " +
            "0.9 every 1.5 s while a second human elsewhere and the layer's own pane stay clear, " +
            "and a layer going down ends its screen on the spot",
            SmokeScreenSuite));
        into.Add(new TestHarness.Suite("ordnance-launch-axis",
            "a player's pylon salvo leaves along the AIRCRAFT's axis while an AI's leaves along its " +
            "mount's (A5): no shipped airframe cants a pylon marker, so the widest rig's markers are " +
            "canted 20° here, and every round a human fires still flies parallel to the nose from " +
            "its own marker's position while the same rig flown by an AI fans by the full 20°; plus " +
            "D18's launch hook, where a wep_13 pylon lays a screen, spends its ammo and spawns no round",
            OrdnanceLaunchAxis));
        into.Add(new TestHarness.Suite("motor-acceleration",
            "a round authoring ACCELERATION climbs to its speed cap and stops there (A3): wep_04 " +
            "off a standing launcher reads 150/300/450 m/s at 1/2/3 s and holds 450 from then on, " +
            "the same weapon off a 100 m/s launcher caps 100 higher, and no step of any round's " +
            "flight — motor, coasting rocket or gun — ever reduces its own speed, because the " +
            "original carries no drag term",
            MotorAcceleration));
        into.Add(new TestHarness.Suite("ordnance-end-conditions",
            "the three ways a round ends itself (A4): wep_24 flies its authored 1000 m and " +
            "detonates there while wep_12, authoring no LOCK_ON, flies the same 1000 m and " +
            "expires silently; wep_15's 2.0 s timed fuse ends it two metres out with nothing near; a " +
            "round fuses on its OWN target while a registered aircraft is nearer, and the same " +
            "round holding no target flies past that point and is fused by the aircraft sweep " +
            "instead, proving the two paths are separate; and wep_14 is hidden for its first 300 m " +
            "and shown from there on",
            OrdnanceEndConditions));
        into.Add(new TestHarness.Suite("ordnance-guidance",
            "the steering step and the beeper pair on a live pool (B6-B9): wep_11 turns onto a target " +
            "abeam at its authored 1.25 rad/s and its speed reads the per-frame 0.8+0.2cos penalty " +
            "exactly; wep_14 with a target turns its 0.001 sentinel and no more, so the gate is on " +
            "LOCK_ON and not the rate; a LOCK_ON round with no target sheds its launcher's velocity " +
            "and does not turn; LOCK_ON_LEAD on a lab def eases from bearing to the AimAssist " +
            "intercept between element 0 and 1 with no step at either end, while every shipped " +
            "carrier expires before element 0; a seeker's held target is the tag list's pick every " +
            "frame, overriding what it was launched with, never an untagged aircraft, and nothing " +
            "once nothing is painted; and a wep_10 hit paints its victim for TIME with the damage " +
            "ledger untouched, expiring at 20 s and collapsing on death",
            OrdnanceGuidance));
        into.Add(new TestHarness.Suite("air-to-air",
            "a round strikes the target plane's body, maps to the data part, moves armor/HP by the " +
            "weapon's own values, downs it when whole-vehicle health exhausts (a lone dead critical " +
            "part no longer kills — the decoded rule, D14) with the kill attributed through the " +
            "Downed event — never hits the shooter's own geometry — a rocket fuses on a passing " +
            "plane, blasting with falloff and attributing the kill, concentrated fire on ONE " +
            "bearing kills through the decoded redirect + whole-pool overflow (the 2026-08-14 " +
            "correction), and a Fury dies to a few HE rockets", AirToAir));
        into.Add(new TestHarness.Suite("ai-plane-defs",
            "an AI aircraft resolves its OWN vehicle def for the damage model (BL-386): all eleven " +
            "airframes seed the authored whole armor/health pair with no destroyable_parts at all " +
            "and the AI seven-entry injure ladder, while the player def still supplies the loadout " +
            "key and the rig — and a real spawn comes out damageable, zone-less and armed",
            AiPlaneDefs));
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
            "own spawned name, ApplyActorVolumes puts the authored 10000 m on all three of a " +
            "spawned actor's range gates over the airframe's own 2000/2000/1200, the wave-member " +
            "personality/accent draws are pure over theirs, " +
            "and a real InstantActionWaves sequence over spawned aircraft advances from wave 1 " +
            "to wave 2 exactly on the last kill, activating wave 2's built-inert member at a " +
            "drawn spawn point at least 500 m from the human, where its patrol net re-seats on " +
            "the node by that arrival, not the one by the parking pose it seated on while inert",
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
        into.Add(new TestHarness.Suite("graze-bounce",
            "the decoded graze restitution (C25) on real contacts: a player rig flown into a floor " +
            "rebounds along the contact normal off the shipped bounce_factor, an AI rig on the " +
            "identical trajectory never gains normal speed (the original's player-only impulse " +
            "gate) and is destroyed outright by that same contact (the decoded local_11 rule), " +
            "and the same impulse on a vertical face is entirely horizontal — one coefficient, " +
            "no surface test anywhere in it", GrazeBounce));
        into.Add(new TestHarness.Suite("ai-spawn-jitter",
            "the decoded per-spawn dynamics spread (C26) through the real spawner: two AI aircraft " +
            "off ONE airframe cache, given the same pose and the same orders, fly measurably apart " +
            "but by a few percent rather than as different aeroplanes; the same spawn ordinal drawn " +
            "again replays the same line (the --det property); and the shared per-airframe stats " +
            "every later spawn and every human rig reads come out unperturbed", AiSpawnJitter));
        into.Add(new TestHarness.Suite("ai-actor",
            "the M4 AI actor seam: an AI-piloted plane (AiPilot input, IsHumanPiloted false, no " +
            "camera/HUD/devices) spawned into an already-running sim flies its orders, takes a " +
            "mid-flight retarget, and is present, ticking, damageable by the weapon's own values " +
            "and killable with the kill attributed to the shooter through Downed", AiActor));
        into.Add(new TestHarness.Suite("ai-gunnery",
            "the D14 AI gunner + D12 acquisition: acquires through the decoded target ranking " +
            "as mutable state (0.7 player weight, primary_target override, a 'player' assignment " +
            "resolving to the NEAREST human of several, 1e21 activation " +
            "cutoff, all live in the engine), refuses the shot " +
            "when the residual after the ±11° traverse clamp exceeds the gun's 10° aim gate, and " +
            "outside its quick-draw cone off the target's " +
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
            "net following (B5): a real chapter net resolves by id and by name, its tags ride " +
            "along unacted-on, and an AI plane with a net-following pilot captures node after " +
            "node with every hop an EDGE of the graph, never node order. Then the same net, " +
            "anchored (BL-377), rides its target 6 km east and the plane laps the MOVED ring at " +
            "its authored altitude, never seating on the edgeless anchor node", AiNetFollow));
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
            "he_ground_effect's six-step full-screen wash reports its authored run times, so the 1.2 s ramp does not collapse into one instant; the ramp routes by pane proximity and the victim-routed blend wash by player index, composited over it", FbfxFlash));
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
        into.Add(new TestHarness.Suite("damage-staging-pool",
            "the injure staging reads health only: a zone stripped of armour tears no panel though its combined fraction has crossed the threshold, and the panel appears once health itself crosses (BL-384)", DamageStagingPool));
        into.Add(new TestHarness.Suite("crash-rig-anchors",
            "binding the crash rig leaves the airframe model under the controller — even the Devastator, whose model root shares the crash defs' authored NAME — and stages every pooled copy in the same reset pose", CrashRigAnchors));
        into.Add(new TestHarness.Suite("ai-crash-defs",
            "an AI plane's crash rig binds the ai_crash_* family and its crash indexes it by the struck surface id — dirt(13) plays ai_crash_dirt, no material plays ai_crash_default — while a human rig off the same factory keeps player_crash_* (G21)", AiCrashDefs));
        into.Add(new TestHarness.Suite("hostile-marker-hud",
            "the targeting HUD (TargetHud, every flight session): the tracker picks the " +
            "pane's nearest LIVE AI hostile off the pool's own aircraft roster (a closer human, " +
            "dead plane or neutral is never picked), switches to a closer hostile, drops a " +
            "crashed one, and a hud built without a pool never tracks; plus --debug-markers' " +
            "own selection, which takes EVERY live aircraft instead of the nearest, flags each " +
            "by team against the pane's own, skips a crashed one and skips the pane's own " +
            "aircraft; plus the shipped marker's rules — the three decoded colours, the bracket " +
            "gate's gun reach (inside RANGE brackets, past it does not, a target outrunning the " +
            "round never does, and the hysteresis holds the boundary case), and the label lines " +
            "an aircraft, an off-screen target and a named objective each compose",
            HostileMarkerHud));
        into.Add(new TestHarness.Suite("target-ref",
            "the one abstraction over every selectable thing, tree-free: a TargetRef built for "
            + "each of the three source kinds (aircraft, zeppelin sub-part, turret emplacement) "
            + "forwards the wrapped AimCandidate's pose/team/liveness/source and reads its own "
            + "identity back; health and armor are genuinely optional, so the turret carries "
            + "neither and the structure carries health alone; the label line runs the original's "
            + "four format strings; Classify reproduces FUN_004b5cd0's order (objective over "
            + "otherTarget over the team split, an unflagged turret not selectable at all); and "
            + "identity is the SOURCE object, not the wrapper", TargetRefModel));
        into.Add(new TestHarness.Suite("target-pool",
            "the classed candidate pool: the three cycles built off the aim assist's own typed "
            + "lists. A wingman lands in Ally and an enemy in Enemy off the TEAM FIELD (never the "
            + "pilot-index derivation, which is the wingman-in-the-marker bug), the selecting plane "
            + "is excluded from its own pool, a dead plane and a destroyed zeppelin engine are "
            + "absent, the destructible registry contributes nothing however full "
            + "AimCandidateSet.Structures is, live ordnance is not walked, and a zeppelin "
            + "contributes one entry per gasbag/engine/cannon with its hull's velocity; plus C1's "
            + "real emplacements landing on the Non-Aircraft cycle", TargetPoolModel));
        into.Add(new TestHarness.Suite("target-selection",
            "the sticky selection, tree-free: the decoded cycle order in one assertion "
            + "(objectives, then ahead/behind/left/right with distance inside a sector), the "
            + "auto-acquire at the head, Next/Previous stepping and wrapping, Nearest as HEAD OF "
            + "CYCLE rather than nearest-in-space, target death dropping to the head and not to the "
            + "dead entry's neighbour, own respawn preserving a live selection, range/bearing/"
            + "attitude changes never dropping one, Target Nothing STAYING cleared through repeated "
            + "rebuilds, nearest-crosshairs scoring the NOSE cone (not the pipper) with its 2 km cap "
            + "and reaching an ally, and 0x24's attacker queue walked backwards", TargetSelectionModel));
        into.Add(new TestHarness.Suite("target-input",
            "the tap/hold decoding, which is what the suite CAN read (a gamepad and a bare key "
            + "press it cannot): TapHoldButton's resolve-on-release rule — a short press taps, "
            + "crossing 250 ms fires the hold ONCE mid-press and the release is then spent, a held "
            + "button never repeats, and an up button with no press reports nothing; plus the "
            + "attacker queue's live wiring, where a real hostile round through TakeProjectileHit "
            + "records its shooter, a friendly-fire round and an unowned one record nothing, and "
            + "ProjectilePool.RigOfShooter resolves a shooter id to its plane", TargetInputModel));
        into.Add(new TestHarness.Suite("target-flag",
            "the --target= scripted twin: the four words mapping onto the ordinary actions "
            + "(nearest as head-of-cycle, next, crosshair, none), a name pinning an aircraft the "
            + "auto-acquire would NOT have chosen, the same one grammar reaching an ally and a "
            + "zeppelin sub-part by writing the class back, case-insensitive matching, an unknown "
            + "name leaving the selection alone, two selectors given one spec landing on the same "
            + "target, and the flag NOT pinning against later input", TargetFlagModel));
        into.Add(new TestHarness.Suite("splitscreen-listeners",
            "every 2–4P pane is a 3D audio listener, which a SubViewport is not by default — the "
            + "pinned listener model (A2), and the one thing standing between splitscreen and a "
            + "world with no listener at all", SplitscreenListeners));
        into.Add(new TestHarness.Suite("world-lights-nearest-viewer",
            "WorldLights budgets its 900-1500 m distance fade and its MaxActive significance rank "
            + "against the NEAREST of every pane's camera, not player 1's alone (B13, BL-366): a "
            + "light 2000 m from a lone P1 stays committed once a second viewer sits 100 m from it, "
            + "the able-to-fail control against P1 alone drops the same light, and the one-viewer "
            + "case reads exactly what it read before", WorldLightsNearestViewer));
    }

    // ---- emitter lifetime is observable with no GPU ---------------------------------------------

    // Kills a refuel* tank with a CountingEmitterFactory installed and asserts on its fire_n_smoke
    // emitter, the def whose ACTIVE_STATE 1 carries no authored stop, so only instance retirement
    // ever ends it. ⚠ Assert all three facts: the fake was reached, the emitter started, and it
    // stopped without being forgotten (the row still present, Emitting false). A suite reading only
    // "stopped" cannot tell a correct pause from a teardown by the wrong selector.
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

    // Every Puffer emission path through the collapsed Emit/Stop pair, plus Burst, over a
    // RecordingEmitterRenderer. No atlas, TextureArchive or MultiMesh is built anywhere here, which
    // is its own tripwire: if it gets slow, something started building real emitters again. Both
    // states are read from the shipped readers, so every expected number derives from authored data.
    // ⚠ Keep GameClock.Current detached for the suite's duration. The harness clock is a FixedStep
    // one nothing steps, so FrameDt is 0 and every check would pass vacuously.
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

        // Detached for the duration: the harness's own clock is a FixedStep one nothing steps, so
        // left installed every emitter tick would advance no sim and every check would pass
        // vacuously. The suites below drive their own dt directly instead.
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

    // The particle integrator against the original's own arithmetic (docs/org/puffer.md), on a state
    // built here rather than loaded, because an authored puffer's random draws would obscure the
    // closed form. Three claims, each with the control that makes its "unchanged" readable: at zero
    // wind the coupling is algebraically absent, acceleration is applied after the position step and
    // before the damp, and WIND_FACTOR 0 is becalmed while 1 is fully carried behind the friction gate.
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

    // The distance alpha, driven entirely off the numbers two shipped readers author.
    // Every distance below is a VIEW-SPACE DEPTH: the camera sits at the origin looking down −Z
    // (Godot's forward), so a particle placed at `(0, 0, −d)` is at depth `d`, and one
    // pushed sideways is deliberately used to prove the measure is depth and not range.
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
            PufferFadeEveryPane(ctx);
        }
        finally
        {
            GameClock.Current = clock;
        }
    }

    // One particle that never moves and never dies, carrying a COLORS ramp so the drawn
    // alpha channel is the DISTANCE alpha alone: the ramp path writes `1 × distAlpha` and
    // leaves the life envelope out of it (the ramp's own alpha rides in the colour, exactly as
    // the shader's `v_alpha × v_color.a` expects).
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

    // Bursts one particle at `at` and returns the alpha it was drawn
    // with, or null when it was discarded. The burst is fired AT the point (burst mode stores
    // positions in the node's own frame, whose origin is the burst point), and a single
    // `_Process` at a dt small enough to leave the particle where it was born runs the draw.
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

    // C3's `spew_puffer` exactly as `waterfalls.zrd.json` authors it —
    // `FADE_RANGE [300, 400]`, `NEAR_FADE [40, 5]` — the only puffer in the install
    // carrying both a near fade and the tightest far band.
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

    // The cross-wire, reproduced rather than repaired: the near ramp's origin is FAR_FADE[0]. C3's
    // volcanosmoke is the only puffer in the install whose near pair ascends, so it is the only one
    // reaching the ramp branch, where the wrong origin drives alpha negative and the gate culls it.
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

    // The author's three switches, each shown to switch. Config is file-backed with no
    // setter, so these come through `CreateWith`'s test-only override — see
    // PufferFadeSwitches.
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

    // No camera published ⇒ no distance fade at all, rather than one measured against
    // the world origin. This is what keeps the unit suites, the plane viewer and the damage lab
    // out of the unauthored near cull at depth 0, and it is the state every OTHER puffer suite
    // runs in — which is why none of them moved.
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

    // The splitscreen rule: the bands are evaluated against EVERY
    // pane's camera and the particle takes the most favourable answer, so a trail 20 m in front of
    // player 2 draws even while player 1's own camera near-culls it. Driven through the real
    // ViewerSet over real `Camera3D` nodes, which is the seam
    // `WeatherRig.Tick` publishes from.
    private static void PufferFadeEveryPane(TestContext ctx)
    {
        var state = FadeTestState("spew_puffer", 40f, 5f, 300f, 400f);

        // Two panes looking the same way down −Z, player 2 astern of player 1 by 200 m — the
        // repro's geometry (P2 behind P1, shooting past him).
        var p1 = ViewerCamera(ctx, Vector3.Zero);
        var p2 = ViewerCamera(ctx, new Vector3(0f, 0f, 200f));
        try
        {
            var both = new ViewerSet();
            both.Bind(new[] { p1, p2 });
            var amb = new EffectAmbience();
            amb.SetViewers(both);

            var justAheadOfP1 = new Vector3(0f, 0f, -20f);
            ctx.Check(FadeAlphaAt(ctx, state, justAheadOfP1, amb) is { } near
                      && Mathf.IsEqualApprox(near, 1f),
                $"20 m ahead of P1 is inside P1's near cull but 220 m ahead of P2, so it DRAWS — the pane that can see it decides");

            var onlyP1 = new EffectAmbience();
            onlyP1.SetCamera(Vector3.Zero, Vector3.Forward);
            ctx.Check(FadeAlphaAt(ctx, state, justAheadOfP1, onlyP1) == null,
                $"ABLE-TO-FAIL CONTROL: that same particle against P1's camera alone is culled, which is the reported bug");

            ctx.Check(FadeAlphaAt(ctx, state, new Vector3(0f, 0f, 300f), amb) == null,
                $"behind BOTH panes it is still culled — 'any pane' is a union, not a disabled fade");

            // The most FAVOURABLE answer, not the first or the last: P1 reads 350 m (half way
            // across its far band), P2 sits 50 m short of it and reads full alpha.
            var p3 = ViewerCamera(ctx, new Vector3(0f, 0f, -300f));
            try
            {
                var nearer = new ViewerSet();
                nearer.Bind(new[] { p1, p3 });
                var ambNearer = new EffectAmbience();
                ambNearer.SetViewers(nearer);
                float? shared = FadeAlphaAt(ctx, state, new Vector3(0f, 0f, -350f), ambNearer);
                ctx.Check(shared is { } s && Mathf.IsEqualApprox(s, 1f),
                    $"a particle half-faded for P1 and full-alpha for the nearer pane draws at full alpha={shared}");
                ctx.Check(FadeAlphaAt(ctx, state, new Vector3(0f, 0f, -350f), onlyP1) is { } lone
                          && Mathf.IsEqualApprox(lone, 0.5f, 1e-4f),
                    $"ABLE-TO-FAIL CONTROL: P1 alone reads that same particle at half alpha");
            }
            finally
            {
                p3.Free();
            }

            // One pane is the old behaviour exactly — the single-viewer case must not move, and it
            // is what every capture, freecam shot and single-player session runs.
            var alone = new ViewerSet();
            alone.Bind(new[] { p1 });
            var ambAlone = new EffectAmbience();
            ambAlone.SetViewers(alone);
            float? single = FadeAlphaAt(ctx, state, new Vector3(0f, 0f, -350f), ambAlone);
            ctx.Check(single is { } one && Mathf.IsEqualApprox(one, 0.5f, 1e-4f),
                $"with ONE viewer the answer is that viewer's own, unchanged alpha={single}");
        }
        finally
        {
            p1.Free();
            p2.Free();
        }
    }

    // A bare `Camera3D` at a world position looking down −Z, parented to the test
    // host — a stand-in for a pane's camera, which is all a ViewerSet reads.
    private static Camera3D ViewerCamera(TestContext ctx, Vector3 at)
    {
        var cam = new Camera3D { Current = false };
        ctx.Host.AddChild(cam);
        cam.GlobalTransform = new Transform3D(Basis.Identity, at);
        return cam;
    }

    // ---- PRIORITY inflates the sprite -------------------------------------------------------------

    // A PRIORITY 10 puffer spawns particles exactly 20% larger
    // than the same state at PRIORITY 0 — `1 + 0.02·10 = 1.2`, the hardware-path `K`
    // (`PriorityScaleDefault`). Burst mode, NUMBER 1, a degenerate SIZE_RANGE so the drawn
    // size is deterministic and the only thing that can move it is PRIORITY.
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

    // One particle, no randomness: NUMBER 1, a degenerate random-velocity range (min ==
    // max, so the draw is exact), no deviation, no growth, no ramps. Burst mode, so the single
    // batch lands at t = 0 at the burst point.
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

    // Runs one WindTestState particle for `frames` steps of
    // `dt` and returns its displacement. The burst is fired at the world origin
    // and a burst puffer's particles are stored in its own (there, identity) frame, so the
    // written position IS the displacement.
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

    // Zero wind means the coupling term vanishes, so a friction particle decays to rest on the pure
    // damped curve: v_n = v0*damp^n and pos_n = v0*dt*(1 - damp^n)/(1 - damp), the geometric sum of
    // the position step, which the traced order takes before the damp.
    // ⚠ Keep the able-to-fail control: the identical emitter in a 10 m/s wind must land somewhere
    // else, or the match is a statement about a branch nothing exercised (docs/verification.md).
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

    // The reorder, isolated: friction 0, no wind, a pure world acceleration. The traced
    // order steps position on LAST frame's velocity and only then adds `a·dt`, giving
    // `pos_n = a·dt²·n(n−1)/2`. Our old order (`v += a·dt` first, position after) gave
    // `a·dt²·n(n+1)/2` — larger by exactly `a·dt²·n`, i.e. one frame of the current
    // velocity, which is the whole of the difference and is asserted as such.
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

    // The per-puffer coupling. Both particles start at rest with no acceleration, so the
    // ONLY thing that can move them is the wind: `WIND_FACTOR` 0 must therefore not move at
    // all, and 1 must converge on the wind velocity. The carried one's closed form is exact —
    // `v_n = w(1 − damp^n)`, `pos_n = w·dt·(n − (1 − damp^n)/(1 − damp))`.
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

    // The engine's own gate: the whole damp-toward-wind block sits inside
    // `if (friction != 0)`, so a frictionless puffer is untouched by any wind at any factor.
    // It is load-bearing only because there is a wind term inside the block: with an unconditional
    // `Exp(0) == 1` damp and no wind the gate would be the identity.
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

    // The gust itself (WorldWind), against the shipped authored values —
    // `STATIC_VELOCITY (0,2,0)`, `RANDOM_MAX_SPEED 10`, `RANDOM_ACCEL 5`,
    // `RANDOM_ANG_VEL 5`, which every one of the install's 53 weather readers carries.
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

    // A moving DISTANCE_INTERVAL host keeps AT_NODE's offset in the host frame for every
    // emitted puff, not only the still-host fallback. The retail speed cue is the canary: its
    // player,0,0,-60 attachment is what places the wisps 60 m ahead of the aircraft.
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

    // The speed-cue adapter selects the retail altitude bands, preserves the selected
    // puffer above the final authored threshold, suppresses every cue near ground, and Reset
    // removes live particles so a respawn cannot bridge positions.
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

    // C1 and C4 share cue geometry and timing but retain their chapter-authored alpha
    // variants instead of collapsing onto one global tune.
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

    // Burst: the pool is sized from the calling animation's stop time, the first batch is
    // spawned at t = 0 rather than one interval in, the flipbook walks its whole sequence, and the
    // emitter puts itself away once the last particle dies.
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

    // A TIME_INTERVAL state through Emit: the authored state picks the sustain path, the pool is
    // sized to the steady-state population, emission starts on the first frame, a long frame's older
    // catch-up batches are born already dead while its youngest are born alive, the hitch drains its
    // own accumulator, and Stop ends emission without cutting the live particles short.
    // See docs/org/puffer.md.
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

            // A 5 s hitch: the uncapped batch loop asks for all 25 batches at once, each carrying the
            // engine's sub-frame start-age offset, so the early ones are past LIFETIME_RANGE and the
            // born-dead skip drops them. Read before _Process, so this is the spawn path, not the reaper.
            puffer.Emit(origin, Basis.Identity, 5f);
            int afterHitch = puffer.LiveCount;
            ctx.Check(afterHitch <= gpu.Capacity,
                $"a 25-batch hitch cannot overrun the pool live={afterHitch} pool={gpu.Capacity}");
            ctx.Check(afterHitch > 18,
                $"its youngest batches ARE born alive — the hitch is not silently dropped whole live={afterHitch}");
            // ⚠ Do not assert how many of the spawns the born-dead skip discarded. The pool is sized to one
            // lifetime and the survivors are one lifetime's worth, so the pool clamp would answer the check
            // instead of the skip; the drain assertion below is where the skip's arithmetic is readable.
            puffer._Process(1f / 60f);

            // The hitch drained its own accumulator, so the next ordinary frame carries a sixth of an interval
            // and emits nothing. A per-frame batch cap would instead burst 8 more batches, which the engine
            // never produces. Able to fail: with a cap in place this reads a full pool instead of no change.
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

    // A fast-moving TIME_INTERVAL emitter spreads a frame's batches along its motion since the
    // previous call instead of stacking them on today's pose, and the first frame after a
    // Stop()/restart re-homes rather than interpolating from the stale pre-stop pose (the
    // rocket-explosion ghost-trail rule). See docs/org/puffer.md. A synthetic zero-velocity,
    // zero-deviation, zero-COLORS state removes every other source of scatter, and LIFETIME_RANGE is
    // pinned to 10 s so the batches clear the born-dead skip and stay in the fade envelope's ramp.
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

            // Restart case: Stop() then Emit() at a far new site, whose first frame's puffs must appear only
            // there. ⚠ Give the restart frame a multi-batch accumulator; at exactly one batch frac is 1
            // regardless of prevOrigin and the check passes with the re-home missing.
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

    // A DISTANCE_INTERVAL state through `Emit`: the first call homes the trail and
    // time-sputters one batch (the still-host rule — on the homing frame no motion has elapsed
    // yet), a moving host emits one puff per interval of actual motion with the remainder carried
    // across frames instead of rounded away.
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

    // A distance state whose host stands still keeps the time cadence (the damaged
    // building's sputter): the authored interval can never elapse, so `Emit` with no burn
    // rate falls back to one batch per synthetic 0.1 s TIME_INTERVAL, at the held point.
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

    // A host that CANNOT move (the damage lab's parked plane) declares a burn rate:
    // `Emit` with `staticBurnMps` spends virtual metres at the held point — the
    // authored per-metre density, not the time cadence — through the same carry as the moving
    // trail.
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

    // The pause + far revive, through `Stop` itself: a pooled effect-template slot
    // is teleported to each new call site, so a distance-state emitter stopped at one blast and
    // revived at the next must re-home there — a kept trail origin draws a puff line across the
    // whole jump (the rocket-explosion ghost trails). `Stop` ends trail AND sustain
    // unconditionally; the revive's first call sputters fresh at the new site.
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

    // The original's teleport guard on the DISTANCE path: the frame's motion length joins the emission
    // accumulator only if len < 200, so an emitter carried across the world lays no puff line along
    // the jump. PufferStopRevive covers the jump that goes through Stop and re-homes; this is the one
    // that does not. Four steps, each failing differently: a 199 m move pins the boundary from below,
    // the 500 m jump emits nothing, the guard counter reads 1, and a 20 m move afterwards proves the
    // carried remainder survived the guard rather than being reset with it.
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

    // START_AGE_RANGE, on a synthetic state authoring a negative minimum like fire_at_zepskin3's
    // (-1.0, 0.1), the census's only negative case. Checks what the integrator settles: every particle
    // is drawn, a negative-age particle pins to stop 0 of the colour and growth ramps rather than
    // being skipped or extrapolated, and it outlives its authored LIFETIME_RANGE by up to StartAgeMin.
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

    // The fire_n_smoke column, measured: the AUTHORED column with no engine-side rise or lifetime
    // multiplier on top, so a change that shortens the fire has to argue with a number. Its state
    // comes from the chapter's compiled program, which the reader would call a stub. It is run in
    // still air and in C1 IA1's authored upward wind, which friction damps toward. docs/org/puffer.md.
    // ⚠ Assert ratios and bands, never a pinned decimal; a height is one seed's extreme and moves
    // about 1.5 m with suite order, so the exact figures belong in ctx.Note.
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

    // Drives one sustained emitter for 30 s of held-still emission and reports the
    // column it built: the highest particle CENTRE, the highest drawn sprite TOP (centre plus the
    // quad's half-side — the renderer scales a 1×1 quad by `Size`), the peak live population
    // and the largest sprite ever drawn. Heights are relative to the emitter's own origin.
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
                    // The same run read at the half-size sprite convention: Size is exactly linear in
                    // SizeScaleDefault and the RNG stream does not depend on it, so quartering the side here is that
                    // convention's drawn top exactly rather than an estimate.
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

    // Stays whole here rather than splitting an engine-free half into CSVM.Tests: Probes.Loadouts
    // calls StockLoadouts.Load (Godot.FileAccess) and PlaneBuilder.Build to resolve Loadout.Bind's
    // markers, both native-backed and fatal off-engine (AccessViolationException). Binding needs a
    // built plane, so there is no pure half to extract without reimplementing marker resolution.
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

    // B2's per-muzzle slot state (the forget + catch-up pass), B3's intercept solver and
    // B4's candidate scan, the first three in isolation — no plane, no pool,
    // AimAssist is engine-free by design — plus a golden check that the player.json
    // and weapons.json values the assist consumes parse at their documented shipped figures
    // at their shipped figures. The ordnance list is the one
    // case that needs a live pool, since the list IS a filter over the rounds in flight.
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

    // The catch-up slerp: a ~0.2 s time constant at the shipped catchup_rate (5.0), full
    // convergence given enough time, and an outright snap on a single frame at or past
    // 1/catchup_rate — the real hitch-behaviour difference docs/org/aim-assist.md flags, not a
    // rounding detail to smooth away.
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

    // The forget timer measures time since the barrel last FIRED, not time since a lock
    // was lost: stop restamping and the target unwinds to local forward exactly
    // forget_interval seconds later; keep restamping (as B5's fire call will) and it never does.
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

    // Golden check: the keys B2/B4 consume parse off the real player.json and
    // weapons.json at their documented shipped values, not just their compiled-in defaults. The
    // dist_factor one matters most — it ships at 0.0 where the executable's compiled fallback is
    // 2.5e-4, so a reader that quietly failed to find the key would restore a distance term the
    // shipped data deliberately turns off.
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

    // B3's constant-velocity intercept solver (AimAssist.TryIntercept): a
    // stationary target dead ahead solves to the plain displacement direction with t =
    // distance/speed; a crossing target's solved direction and t place the round at exactly the
    // target's projected position (self-consistency, not an independent re-derivation of the
    // quadratic); a target receding faster than the round returns no solution rather than a
    // bogus direction.
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

    // B4's rejection gates, each proved able to fail: the same candidate that is accepted
    // on the baseline is rejected when exactly one thing changes. Covers the engine's order —
    // self, not live, same team (and either side unaffiliated), out of RANGE, outside the cone —
    // plus the per-target `+0x50` cone override, which nothing ships but which is ported
    // deliberately (Decision 2), and the turret pass, which exists and iterates nothing until M4
    // puts a list in it.
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

    // Selection among survivors, on the shipped `dist_factor 0.0`: a distant
    // on-axis target outranks a near off-axis one at any range inside RANGE, because the distance
    // term is deleted outright. The contrast case runs the identical geometry at the executable's
    // compiled 2.5e-4 default and shows the winner FLIPS — so this is a measurement of the shipped
    // value, not of the arithmetic being insensitive to it.
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

    // The rocket snap, on a live pool: a real proximity-fused round in flight is a
    // candidate, and it outranks the aircraft behind it. The ordnance list is a FILTER over the
    // rounds in flight, so this is the one B4 case that cannot be proved off-engine — and the gun
    // round fired alongside is the able-to-fail half: it is in the same pool, alive, and must NOT
    // be collected, since it carries no proximity fuse.
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

    // B5's launch scatter (AimAssist.Scatter): every round lands inside the
    // cone, the polar angle is UNIFORM IN THE ANGLE rather than over the cone's solid angle (the
    // reflex port, and what `ProjectilePool.ApplySpread`'s `sqrt(rand)` does, which would
    // pile shots at the rim), and the roll about the aim axis covers the full circle. The
    // able-to-fail control is the solid-angle sampling itself, computed alongside from the same
    // draws: it fails the flatness test this one passes.
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

    // B5's fire-call step order (`FUN_004b6530`), which is asymmetric on purpose: the
    // scan updates the slot's TARGET, and what leaves the muzzle is the SMOOTHED direction from
    // previous frames. Run on a rolled plane basis, so a world/local mix-up cannot pass: the fired
    // direction must be the smoothed LOCAL vector rotated out to world (inside the scatter cone),
    // and the stored target must be the scan winner rotated INTO local. Firing this frame's scan
    // result instead would remove the lag entirely and read as an aimbot.
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

    // Loadout.ForRig against all 11 player airframes — 4 gun groups
    // covering every `firepointN` the rig actually carries (the Kestrel's odd 7th), one
    // hardpoint per `pylonN`, no marker bound to two groups, and every synthesized group
    // fireable even where stock marks the slot a turret.
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

    // The fly-mode damage-trail regression: Godot's TopLevel toggle PRESERVES the node's global
    // transform, so a trail emitter parented under a flying plane kept the plane's attitude as its
    // basis and every world-space puff was yawed around the world origin. Invisible at the identity
    // -Z heading, which is why the parked viewer and every scripted dive looked fine. The suite feeds
    // a trail under a carrier at the C1 spawn pose and asserts the emitter re-anchored to identity.
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

    // The incoming-fire near-miss cue's wiring, with its able-to-fail baseline: a real round from
    // another pilot flying past registers a pass, the same round fired by the target's own identity
    // registers none, and a round a hundred metres wide of the aircraft registers none either, so a
    // pass count of 1 means the geometry. The accumulator's own arithmetic is unit-tested off-engine
    // (WarningShotCueTests); this is the pool half, on real ballistics.
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

    // The decoded graze restitution on real contacts: a shallow dive onto a floor and a shallow scrape
    // along a vertical wall, flown by a real rig through the real collision sweep. Two things need a
    // live contact and cannot be read off FlightModel (whose arithmetic BounceRestitutionTests pins):
    // the player-only gate, where an AI rig on the same trajectory gets only the position correction,
    // and both orientations. ⚠ The wall assertion is about the rebound AXIS, not a
    // per-surface coefficient; the impulse has no surface dependence, only the contact normal differs.
    private static void GrazeBounce(TestContext ctx)
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
    private static void AiPlaneDefs(TestContext ctx)
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
            var inputs = new FlightRigAssembler.Inputs
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
            };

            var start = new Vector3(0f, 500f, 0f);
            var spawner = new AiAircraftSpawner(spec, liveries, null!, ctx.Host, inputs);
            spawned = spawner.Spawn("player_fury", start, start + Vector3.Forward,
                AiPilot.HoldingCourse(start, start + Vector3.Forward));

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

    // The per-spawn jitter where only a real spawn can show it: through AiAircraftSpawner, over the
    // session's shared per-airframe stats cache, read out as flown trajectory rather than as a field.
    // Two aircraft off one airframe, given the same pose and the same orders, must fly apart; the same
    // ordinal drawn again must fly the same line; and the cache must come out untouched, since every
    // later spawn and every human rig reads it.
    private static void AiSpawnJitter(TestContext ctx)
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
            var inputs = new FlightRigAssembler.Inputs
            {
                PlanesGamez = planesGamez,
                StatsFor = _ => shared,
                // The same one object on both seams: this suite measures the jitter's spread over
                // a SHARED cache entry, so the AI flavour must not quietly become a second object.
                AiStatsFor = _ => shared,
                RigCount = 0,
                PaintRng = new RandomNumberGenerator(),
                ZrdrPath = ctx.ZrdrPath,
                StockLoadouts = StockLoadouts.Load(),
                WeaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null),
                Textures = textures,
                Projectiles = live,
                Shakes = ShakeDefs.Load(ctx.ZrdrPath),
            };

            // One aircraft's flown displacement over a fixed window, straight and level on orders it never
            // has to correct, so what separates two runs is the plant, not the pilot. Freed at the end of its
            // own run: every run flies the same pose, and two aircraft there would be measuring a collision.
            var start = new Vector3(0f, 500f, 0f);
            Vector3 Fly(AiAircraftSpawner spawner)
            {
                var pilot = AiPilot.HoldingCourse(start, start + Vector3.Forward);
                var ai = spawner.Spawn(ctx.PlaneName, start, start + Vector3.Forward, pilot);
                flying = ai;
                for (int i = 0; i < 240; i++)
                    ai.SimStep(1f / 60f);
                var flown = ai.WorldPosition - start;
                flying = null;
                ai.Free();
                return flown;
            }

            // Two ordinals off one spawner: two aeroplanes, same pose, same orders.
            var spawner1 = new AiAircraftSpawner(spec, liveries, null!, ctx.Host, inputs);
            var first = Fly(spawner1);
            var second = Fly(spawner1);

            // A fresh spawner restarts at ordinal 0, and the draw is keyed by ordinal — so this is
            // the same aircraft as `first`, which is what a --det replay reproduces.
            var spawner2 = new AiAircraftSpawner(spec, liveries, null!, ctx.Host, inputs);
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
    private static StaticBody3D Plate(string name, Vector3 size, Vector3 at)
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

    // A2's launch-velocity inheritance and its decay, on a live pool with no world around it: a
    // torpedo launched fast leaves fast and settles onto its authored VELOCITY over LOCK_ON
    // seconds, one launched slow barely changes, a round holding no target sheds nothing, and
    // neither a gun nor a rocket without LOCK_ON reads any differently than it does today.
    private static void LaunchVelocityDecay(TestContext ctx)
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
    private static void MotorAcceleration(TestContext ctx)
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

    // A4's three end conditions on a live pool, plus the RANGE_MINIMUM reveal that rides the same
    // travelled-distance accumulator. The detonation observer is the pool's EffectSink: a round
    // that ends by detonating hands its IMPACT row's effect name and the point it went off, and one
    // that expires quietly hands nothing, so the same seam reads all four outcomes.
    private static void OrdnanceEndConditions(TestContext ctx)
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
                  && ProjectilePool.FlyoutHiddenAtLaunch(torpedo) && !ProjectilePool.FlyoutHiddenAtLaunch(he),
            $"the torpedo alone carries both halves of the reveal gate (RANGE_MINIMUM 300 m and FLYOUT_HEALTH)");

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
                EffectSink = (name, at, ttl) => effects.Add((name, at)),
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

            var reveal = new List<(float Travelled, bool Hidden)>();

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
                    reveal.Clear();
                    live.CollectFlyoutReveal(reveal);
                    if (reveal.Count == 0)
                        break;
                    travelled = reveal[0].Travelled;
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

            // 4. The reveal, on the same accumulator. The torpedo's 60 m/s puts 300 m at 5 s.
            live.Clear();
            live.Spawn(torpedo, muzzle, Vector3.Zero);
            bool hiddenEarly = true, shownLate = false;
            float shownAt = 0f;
            for (int i = 0; i < 400; i++)
            {
                live.SimStep(1f / 60f);
                reveal.Clear();
                live.CollectFlyoutReveal(reveal);
                if (reveal.Count == 0)
                    break;
                var (travelled, hidden) = reveal[0];
                if (travelled < (torpedo.RangeMinimum ?? 0f) && !hidden)
                    hiddenEarly = false;
                if (!hidden && !shownLate)
                {
                    shownLate = true;
                    shownAt = travelled;
                }
            }
            ctx.Check(hiddenEarly, $"the torpedo's flyout stays hidden for every metre short of RANGE_MINIMUM");
            ctx.Check(shownLate && Mathf.Abs(shownAt - (torpedo.RangeMinimum ?? 0f)) < 2f,
                $"and is shown as it passes it at={shownAt:0.#} m minimum={torpedo.RangeMinimum:0} m");
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

    // B6-B9 on a live pool with a lab tag list beside it: the turn clamp and its speed penalty, the
    // sentinel rate under the same gate, the target-free decay, LOCK_ON_LEAD's blend on a lab def
    // (no shipped carrier reaches its onset), the seeker's per-frame pick, and the beeper's paint.
    private static void OrdnanceGuidance(TestContext ctx)
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

    // The air-to-air hit chain on two real flight rigs driven by manual sim steps: body strike,
    // struck-shape to part mapping, armor-first damage, the decoded whole-vehicle kill rule, a
    // crashed plane's immunity, Downed attribution into a real VersusMatch, the VS respawn loop, and
    // the rocket proximity fuse and blast falloff. Full inventory: this module's architecture entry.
    // ⚠ The zero-self-hits negative case stays non-optional; without it a broken owner exclusion
    // arrives silently as "guns too strong".
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

    // D18's mechanism over a live roster, with the launch hook stood in for by a direct Lay: the
    // layer is a human rig flying -Z, so its backward axis is +Z and everything at +Z of it is
    // "behind". Two AI and two more humans sit where the cone must catch or miss them, the pool
    // and the flight models run for real, and the wash goes through a real two-pane ScreenFlash
    // wrapped by a recording sink so the third human (no pane) is still observable.
    private static void SmokeScreenSuite(TestContext ctx)
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
            var aiBehind = BuildRig(AiAircraftSpawner.ShooterIdBase, human: false, layerPos + new Vector3(0f, 0f, 300f));
            // 97° off the backward axis at 400 m: inside the range, outside the 85° edge.
            var aiSide = BuildRig(AiAircraftSpawner.ShooterIdBase + 1, human: false, layerPos + new Vector3(397f, 0f, -49f));

            (flash, panes) = PaneFlash(ctx, null);
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

    // A5's launch axis and D18's launch hook over a live fire path: the rig fires its own pylons
    // through FireControl, and the pool is never stepped, so every round still stands at the pose
    // it launched from. No shipped airframe cants a pylon marker, so the salvo flies off markers
    // this suite cants itself; an aligned rig cannot tell the aircraft axis from the marker's.
    private static void OrdnanceLaunchAxis(TestContext ctx)
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
                ai = BuildRig(AiAircraftSpawner.ShooterIdBase, isHuman: false);
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
                AiStatsFor = plane => PlaneStats.LoadForAi(ctx.ZrdrPath, plane),
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

            // The wingman census: N aircraft on team 1, since humans and wingmen share the player's side,
            // flying the configured airframe. Spawned through the same AiAircraftSpawner.Spawn seam as the
            // ace above, on AimAssist.PlayerTeam instead of the enemy team.
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

            // The synthetic roster block writes 10000 m into all three volumes, so the airframe gates the
            // spawner seeds first must not survive on an Instant Action actor. Overriding activation alone
            // leaves attack as the real engagement gate: the mode machine enters pursue on the minimum of two.
            var volPos = new Vector3(600f, 500f, 0f);
            var volPilot = AiPilot.HoldingCourse(volPos, volPos + Vector3.Forward);
            volPilot.Machine = new AiModeMachine(new System.Random(7));
            wingmen.Add(spawner.Spawn(wingmanNode, volPos, volPos + Vector3.Forward, volPilot,
                scheme: null, team: AimAssist.PlayerTeam));
            var vol = volPilot.Machine;
            ctx.Check(Mathf.IsEqualApprox(vol.AttackRange, 2000f)
                && Mathf.IsEqualApprox(vol.ReturnRange, 1200f),
                $"the spawner seeds the airframe's own gates first: attack={vol.AttackRange:0} return={vol.ReturnRange:0}");
            InstantActionRuntime.ApplyActorVolumes(vol);
            ctx.Check(Mathf.IsEqualApprox(vol.ActivationRange, InstantActionRuntime.ActorVolumeRadiusM)
                && Mathf.IsEqualApprox(vol.AttackRange, InstantActionRuntime.ActorVolumeRadiusM)
                && Mathf.IsEqualApprox(vol.ReturnRange, InstantActionRuntime.ActorVolumeRadiusM),
                $"all three volumes take the authored 10000 m: activation={vol.ActivationRange:0} attack={vol.AttackRange:0} return={vol.ReturnRange:0}");

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
            // Every Instant Action actor carries the chapter's first patrol net and an inert one still ticks,
            // so this two-node stand-in net has a node at the parking pose and another out at the wave spawn
            // point, to watch which one it flies after the teleport.
            var parkNet = new AiNet
            {
                Id = 1,
                Name = "TestWaveNet",
                Nodes = new[]
                {
                    new AiNetNode(Vector3.Zero, System.Array.Empty<float>()),
                    new AiNetNode(new Vector3(600f, 500f, 0f), System.Array.Empty<float>()),
                },
                Edges = new[] { (0, 1) },
            };
            wave2Pilot.Patrol = new AiNetFollower(parkNet, new System.Random(3));
            var wave2Member = spawner.Spawn(waveNode, Vector3.Zero, Vector3.Forward, wave2Pilot,
                scheme: null, team: InstantActionRuntime.EnemyTeam, inert: true);
            waveMembers.Add(wave2Member);
            wave2Pilot.Patrol.Update(Vector3.Zero);
            ctx.Check(wave2Pilot.Patrol.CurrentIndex == 0,
                $"parked inert, the follower seats on the node by the parking pose: idx={wave2Pilot.Patrol.CurrentIndex}");

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
            // The activation snap the original does (FUN_004b0f40 → FUN_00432010): the arrival
            // re-seats the walk, so the member patrols from where it was put down instead of
            // flying back to the node by its parking pose.
            wave2Pilot.Patrol.Update(wave2Member.WorldPosition);
            ctx.Check(wave2Pilot.Patrol.CurrentIndex == 1,
                $"activation re-seats it on the node by its ARRIVAL: idx={wave2Pilot.Patrol.CurrentIndex}");
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

    // The F12 zeppelin run: the objective-zeppelin selection, the builder's own switch,
    // and the wave arm that replaces E11's teleport. Everything runs over C1/IA1's real
    // `ia.zrd.json` / `egen.zrd.json` / `zeppelins.zrd.json`, on the same host +
    // `cargobay` stand-in world the `zeppelin-launch` suite uses, so the drop geometry
    // under test is the one `AiGeneratorRuntime` already owns.
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
                AiStatsFor = plane => PlaneStats.LoadForAi(ctx.ZrdrPath, plane),
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

        // ⚠ Keep this read-only against the shared cached world: registering a pool or leaving a node
        // switched on here is handed to every later C1 suite, measured once as an inflated
        // destructible-census. What it measures is the decoded activation restoring C1/IA1's objective.
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
                // ⚠ Not "all 36 back on": the ones that stay off are descendants that are themselves deactivated,
                // the hidden destroyed variants, which is why WorldCollision derives the flag instead of walking a
                // subtree to re-enable it. The measurement is the crossing, not a full count.
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

    // How many collision shapes hang anywhere under this node, and how many of those are
    // switched off — the state `Mech3/WorldCollision` derives from its owner's visibility.
    // Recursive, because a world node's shapes hang off its MESH children rather than off the
    // named node itself; a non-recursive count reads 0 of 0 and passes an "all disabled" test
    // vacuously.
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

    // The Instant Action mission end: one mission type at a time, each driven to its end through the
    // same signal GameSession subscribes to, plus the lives ledger's two ends on a real
    // FlightController. Inventory: this module's docs/architecture.md entry.
    // ⚠ Pair every win check with a SECOND runtime of another mission type on the same signal that
    // must stay Running. Reporting an objective the mission does not run on is the one mistake this
    // design can make.
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
                AiStatsFor = plane => PlaneStats.LoadForAi(ctx.ZrdrPath, plane),
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

            // The lives ledger's two ends on a real aircraft: the mechanism is FlightController's own
            // crash/respawn path, which an AI-piloted aircraft takes byte for byte, so the probe is a spawned
            // plane rather than a rig. What is under test is the arming and the Spectating pin.
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

    // A hand-authored `--ia=` file for one end-condition case, written to the
    // scratch folder and read back through the REAL reader — so a change to how
    // `mission_type`/`lives` parse moves this suite too, and no test builds an
    // `InstantActionDef` the CLI could not produce.
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

    // The wrap-up board's two shot counters (docs/formats/instant-action.md): ProjectilePool is the
    // single choke point for both, so this fires real rounds through the real pool at a real target
    // rather than asserting on the arithmetic in isolation. The board's other two rows are a live read
    // of StuntMission.CompletedCount and a Downed tally already exercised by InstantActionEnd.
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
                AiStatsFor = plane => PlaneStats.LoadForAi(ctx.ZrdrPath, plane),
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

            // ScoredShooters: shooter 0 stands in for a registered human seat, 999 for an
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

    // The inert state: an aircraft built complete and held out of the session until Activate.
    // ⚠ Run every claim as ONE instrument over three subjects, a live control, the inert aircraft and
    // that same aircraft activated, so each observation is watched flipping both ways. "Did not appear
    // in the list" is exactly the check that passes for the wrong reason (METHOD-9/METHOD-10). The
    // instruments are the real ones: a physics raycast, CollectAircraft into an AimAssist.Scan, a
    // round fired through the pool, and FlightController.SimStep.
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
                AiStatsFor = plane => PlaneStats.LoadForAi(ctx.ZrdrPath, plane),
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

            // The whole-vehicle pair, not a sum over zones: these aircraft come off AiAircraftSpawner and an
            // AI airframe is zone-less, so a parts sum reads a flat zero and no round could ever bite. It is
            // what the decoded death test reads either way.
            float Combined(FlightController rig) => rig.Damage!.WholeArmor + rig.Damage.WholeHealth;
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

            // Trap: inert is NOT "left off the pool's roster". The plane reached RegisterAircraft like any
            // other, so the fuse and blast passes that walk that roster can see it, which is why InPlay is read
            // there; it is listed as a candidate and simply not live.
            candidates.Clear();
            live.CollectAircraft(candidates);
            var listed = candidates.Vehicles.FirstOrDefault(c => ReferenceEquals(c.Source, subject));
            ctx.Check(listed.Source != null && !listed.Live,
                $"the inert aircraft is on the pool's candidate roster but not live: listed={listed.Source != null} live={listed.Live}");

            // ⚠ Activate AT ITS BUILD POSE. A suite completes inside one _Ready and never yields a frame, so
            // a body moved here keeps its build-pose transform on the physics server and no raycast can find
            // it (INSTR-13); the re-home is asserted off the flight model instead.
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
            float pristine = subject.Damage.WholeArmorMax + subject.Damage.WholeHealthMax;
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
            // The same list feeds the player's target pool, which takes emplacements only. A
            // carried gunner's host is already a target in its own right, so offering both would put
            // two entries on one silhouette; the discriminator is the placement Site.
            var carriedPool = new TargetPool();
            carriedPool.Rebuild(candidates, null, AimAssist.TeamOfPilot(host.PlayerIndex + 1), null);
            ctx.Check(turret.Site == null && carriedPool.Count == 0,
                $"a CARRIED turret is never selectable, though the same scan entry is a live aim-assist candidate on a hostile team pool={carriedPool.Count}");

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

    // The world AA emplacements against the real C1 chapter world: the NODES
    // placement census, the shipped-ACTIVATED default, the --wake-turrets stand-in, the
    // enemy-default/ally team split, the aim-assist candidate list, and the healthy-node kill
    // switch. Zeppelin-slung entries are placed (they are world nodes) and their one gameplay
    // path is checked here too: the Instant Action builder's subtree-scoped activation of the
    // objective hull's rings, both directions, plus the fire it puts on a plane alongside.
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

        // BL-403: one team space, so an emplacement and an aircraft on the same authored side
        // must compare equal. The enemy default is the id an Instant Action wave carries, and the
        // gate they meet at is the same one AcquireTarget runs.
        ctx.Check(!AimAssist.Hostile(TurretDef.DefaultTeamId, InstantActionRuntime.EnemyTeam)
                  && AimAssist.Hostile(TurretDef.DefaultTeamId, AimAssist.PlayerTeam),
            $"a no-TEAM emplacement ({TurretDef.DefaultTeamId}) spares the wave and engages the player");

        // The versus band exists so a splitscreen human cannot inherit an emplacement's side.
        ctx.Check(AimAssist.TeamOfPilot(0) == AimAssist.PlayerTeam
                  && AimAssist.TeamOfPilot(1) > AimAssist.VersusTeamBand
                  && AimAssist.Hostile(TurretDef.DefaultTeamId, AimAssist.TeamOfPilot(1)),
            $"--vs pilot 1 (team {AimAssist.TeamOfPilot(1)}) is still engaged by a no-TEAM emplacement");

        // Neutral is not a wildcard, and the predicate is symmetric on both sides of it.
        ctx.Check(!AimAssist.Hostile(AimAssist.NeutralTeam, AimAssist.PlayerTeam)
                  && !AimAssist.Hostile(AimAssist.PlayerTeam, AimAssist.NeutralTeam)
                  && !AimAssist.Hostile(AimAssist.PlayerTeam, AimAssist.PlayerTeam),
            $"team 0 never shoots and is never shot, and no side is hostile to itself");

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
                        .All(t => t.Team == AimAssist.PlayerTeam),
                    $"every emplacement awake by data is on the ally team — no hostile fires unwoken");

                // A dormant hostile emplacement: aagun32, enemy by the loader's no-TEAM default.
                var aagun = runtime.Emplacements.FirstOrDefault(t => t.Label.EndsWith("@aagun32"));
                ctx.Check(aagun != null, $"aagun32 built a gunner");
                if (aagun == null)
                    return;
                ctx.Check(!aagun.Activated && AimAssist.Hostile(aagun.Team, AimAssist.PlayerTeam),
                    $"aagun32 is dormant and hostile to the player by default team={aagun.Team}");
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

                // The Instant Action builder's own turret arm: a subtree-scoped ACTIVATED write over the objective
                // zeppelin's node, which is what arms multiplayer1zep's four dormant entries. Scoped and
                // reversible; the deactivation loop is the same call with the flag cleared.
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
                    // BL-403: hostile to the PLAYER, and never to the wave this hull launches —
                    // both halves, since the band made the second half impossible.
                    ctx.Check(mp1Rings.All(t => t.Activated
                            && AimAssist.Hostile(t.Team, AimAssist.PlayerTeam)
                            && !AimAssist.Hostile(t.Team, InstantActionRuntime.EnemyTeam)),
                        $"…all awake, hostile to the player, and allied with their own bay wave");
                    ctx.Check(!aagun.Activated && mp2Rings.All(t => !t.Activated),
                        $"…and nothing outside that subtree woke with it");

                    // A ring's own MOUNTING SECTION is out of its sight line, because the ray starts inside that
                    // geometry; the rest of the hull stays in. ⚠ Do not assert that through ray outcomes here: every
                    // unplaced vehicle loads at the map corner, so other zeppelins sit inside this one and block a line.
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

                    // What the player actually feels: an armed hull shoots back, on its own rig so its rounds do not
                    // touch the aagun figures. ⚠ Show the hull first, exactly as the Instant Action builder does:
                    // C1/IA1 hides multiplayer1zep, and a hidden hull has no live colliders to block a turret.
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

                    // ⚠ Drive this the way Godot's physics tick does, with a realtime clock in Current. A runtime
                    // stepped only from GameSession.DriveSimSteps is inert on the realtime clock every real session
                    // uses, and every suite and golden runs fixed-step, which is exactly the blind spot (INSTR-14).
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

                // The team gate, both halves over the piratezep's allied rings: none engages the player's own
                // plane, and the same rings do engage a hostile pane, which is the able-to-fail control. Run over
                // the whole allied population, since the per-ring arcs are the zeppelin's own frame.
                var allied = runtime.Emplacements
                    .Where(t => t.Team == AimAssist.PlayerTeam).ToList();
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

                // The kill switch: the emplacement's own destructible dies through the weapon-damage path, its
                // healthy node hides, and the gunner goes permanently quiet. ai.zrd HEALTH is authored-but-unread;
                // the real pool is the gamez destroy def's.
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

    // The AI actor seam against real engine state on manual sim steps. A human rig is built and stepped
    // first, so the AI plane demonstrably joins a RUNNING sim, with an AiPilot for input, no camera, no
    // HUD and IsHumanPiloted false. It pins presence as a hit target, ticking along its ordered course,
    // mid-flight retargeting, part pools moved by the weapon's own ARMOR_DAMAGE, and a kill attributed
    // to the human shooter through Downed. Inventory: this module's docs/architecture.md entry.
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

            // ⚠ Run the gunfire phases at the SPAWN pose, before the plane flies anywhere: a body moved after
            // creation is invisible to space queries until a physics flush this one-frame suite never gets
            // (INSTR-13). Damage first, flight after.
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

            // Killable, with the kill attributed: pre-empty the other zones with EXACT spends, since an
            // overkill spend would down the plane through the whole-pool overflow before the AI's own burst,
            // then sustained fire on the same bearing exhausts the nose.
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

    // TargetRef, entirely tree-free, which is the seam's whole claim. One ref per source kind: an
    // aircraft with both health pools, a sub-part with health alone, a turret emplacement with
    // neither. That spread is the point, since the turret's nulls are the case that must not
    // silently become 1.0.
    private static void TargetRefModel(TestContext ctx)
    {
        int ownTeam = AimAssist.PlayerTeam;
        var plane = new object();  // stands in for the FlightController the collector hands back
        var engine = new object(); // a zeppelin engine's DestructibleRegistry.Instance
        var gun = new object();    // a TurretController

        // --- an enemy aircraft: the Kestrel screenshot's case ------------------------------------
        var planeCandidate = new AimCandidate
        {
            Position = new Vector3(120f, 300f, -640f),
            Velocity = new Vector3(0f, 0f, -90f),
            Team = AimAssist.PlayerTeam + 1,
            Live = true,
            Source = plane,
        };
        var kestrel = TargetRef.ForAircraft(planeCandidate, TargetClass.Enemy, "ai1_player_kestrel",
            "Kestrel", TargetRef.Fraction(70.2f, 90f), TargetRef.Fraction(91f, 100f));
        ctx.Check(kestrel.Position == planeCandidate.Position
                  && kestrel.Velocity == planeCandidate.Velocity
                  && kestrel.Team == planeCandidate.Team && kestrel.Live
                  && ReferenceEquals(kestrel.Source, plane),
            $"the aircraft ref forwards the wrapped candidate's pose, team, liveness and source");
        ctx.Check(kestrel.Kind == AimTargetKind.Vehicle && kestrel.Class == TargetClass.Enemy
                  && !kestrel.Objective && kestrel.Name == "ai1_player_kestrel"
                  && kestrel.DisplayName == "Kestrel",
            $"…and reads back its own pool, cycle, identity name '{kestrel.Name}' and the marker's own '{kestrel.DisplayName}' (--target= pins the node name, the marker prints the airframe)");
        ctx.Check(TargetRef.ForAircraft(planeCandidate, TargetClass.Enemy, "bandit").DisplayName
                  == "bandit",
            $"a source with no roster entry prints its own name rather than an empty label");
        ctx.Check(kestrel.CategoryLine.Length == 0,
            $"an ordinary aircraft carries neither label half, so line 1 is blank (the Kestrel shot shows line 2 alone) got='{kestrel.CategoryLine}'");
        ctx.Check(kestrel.Health is { } h && Mathf.IsEqualApprox(h, 0.78f)
                  && kestrel.Armor is { } a && Mathf.IsEqualApprox(a, 0.91f),
            $"health and armor read separately, never blended (decision 12's H78 A91) h={kestrel.Health:0.00} a={kestrel.Armor:0.00}");

        // --- a zeppelin sub-part: the C1 M04 objective, health but no armor ----------------------
        var engineCandidate = new AimCandidate
        {
            Position = new Vector3(-40f, 900f, 1200f),
            Velocity = new Vector3(6f, 0f, 0f),
            Team = AimAssist.WorldTeam,
            Live = true,
            Source = engine,
        };
        var promisedLand = TargetRef.ForStructure(engineCandidate, TargetClass.Enemy,
            "Promised Land", "Zeppelin", "Destroy", objective: true,
            TargetRef.Fraction(150f, 200f));
        ctx.Check(promisedLand.Kind == AimTargetKind.Structure && promisedLand.Objective
                  && promisedLand.Class == TargetClass.Enemy,
            $"a flagged sub-part is a Structure riding the ENEMY cycle with the objective companion set (the -too switch, which moves objectives to Non-Aircraft, is not ported)");
        ctx.Check(promisedLand.CategoryLine == "Zeppelin [Destroy] -"
                  && promisedLand.Name == "Promised Land",
            $"both label halves compose the original's '%s [%s] -' got='{promisedLand.CategoryLine}'");
        ctx.Check(promisedLand.Health is { } ph && Mathf.IsEqualApprox(ph, 0.75f)
                  && promisedLand.Armor == null,
            $"a structure has health and NO armor pool h={promisedLand.Health:0.00} a={promisedLand.Armor?.ToString("0.00") ?? "none"}");

        // --- a turret emplacement: no health model at all ----------------------------------------
        var gunCandidate = new AimCandidate
        {
            Position = new Vector3(500f, 12f, 500f),
            Velocity = Vector3.Zero,
            Team = AimAssist.PlayerTeam + 1,
            Live = false, // its healthy node was swapped out — the decoded permanent kill switch
            Source = gun,
        };
        var flak = TargetRef.ForTurret(gunCandidate, TargetClass.NonAircraft, "AA Emplacement");
        ctx.Check(flak.Kind == AimTargetKind.Turret && flak.Class == TargetClass.NonAircraft
                  && !flak.Live && flak.Name == "AA Emplacement",
            $"the turret ref carries its own kind and cycle and forwards a DEAD candidate's liveness rather than hiding it");
        ctx.Check(flak.Health == null && flak.Armor == null && flak.CategoryLine.Length == 0,
            $"a turret emplacement has neither pool, so both figures are omitted, never defaulted to full (decision 12's trap) h={flak.Health?.ToString() ?? "none"} a={flak.Armor?.ToString() ?? "none"}");
        ctx.Check(TargetRef.Fraction(50f, 0f) == null && TargetRef.Fraction(300f, 200f) == 1f,
            $"Fraction is the one place that decides 'no source, no figure', and it clamps");

        // --- the class model, FUN_004b5cd0's own order -------------------------------------------
        ctx.Check(TargetRef.Classify(AimTargetKind.Vehicle, live: true, ownTeam + 1, ownTeam)
                      == TargetClass.Enemy
                  && TargetRef.Classify(AimTargetKind.Vehicle, live: true, ownTeam, ownTeam)
                      == TargetClass.Ally
                  && TargetRef.Classify(AimTargetKind.Vehicle, live: true, AimAssist.NeutralTeam,
                      ownTeam) == TargetClass.Ally,
            $"the aircraft split: a different non-zero team is Enemy, the same team is Ally, and either side unaffiliated is Ally too");
        ctx.Check(TargetRef.Classify(AimTargetKind.Vehicle, live: true, ownTeam, ownTeam,
                      objectiveTarget: true) == TargetClass.Enemy
                  && TargetRef.Classify(AimTargetKind.Structure, live: true, AimAssist.WorldTeam,
                      ownTeam, otherTarget: true, objectiveTarget: true) == TargetClass.Enemy,
            $"objectiveTarget overrides everything below it, including an own-team aircraft and otherTarget on the same entity");
        ctx.Check(TargetRef.Classify(AimTargetKind.Structure, live: true, AimAssist.WorldTeam,
                      ownTeam, otherTarget: true) == TargetClass.NonAircraft
                  && TargetRef.Classify(AimTargetKind.Turret, live: true, ownTeam + 1, ownTeam,
                      otherTarget: true) == TargetClass.NonAircraft,
            $"otherTarget puts a structure or a turret on the Non-Aircraft cycle");
        ctx.Check(TargetRef.Classify(AimTargetKind.Turret, live: true, ownTeam + 1, ownTeam) == null
                  && TargetRef.Classify(AimTargetKind.Structure, live: true, AimAssist.WorldTeam,
                      ownTeam) == null,
            $"an UNFLAGGED turret or structure is not selectable at all — the mission decides, not the world (which is why DestructibleRegistry never feeds this pool)");
        ctx.Check(TargetRef.Classify(AimTargetKind.Vehicle, live: false, ownTeam + 1, ownTeam) == null
                  && TargetRef.Classify(AimTargetKind.Structure, live: false, AimAssist.WorldTeam,
                      ownTeam, objectiveTarget: true) == null,
            $"the liveness predicate runs FIRST, so a dead objective classifies to nothing");

        // --- identity is the source object, not the wrapper --------------------------------------
        var sameEngineLater = TargetRef.ForStructure(
            new AimCandidate { Position = Vector3.Up, Live = true, Source = engine },
            TargetClass.NonAircraft, "Promised Land");
        ctx.Check(promisedLand.IsSameTarget(sameEngineLater),
            $"a ref rebuilt next frame at a new pose still names the same target (FUN_004b6490 re-finds the selection by ENTITY — the wrappers are new objects every frame)");
        ctx.Check(!promisedLand.IsSameTarget(kestrel)
                  && !TargetRef.ForTurret(default, TargetClass.NonAircraft, "")
                      .IsSameTarget(TargetRef.ForTurret(default, TargetClass.NonAircraft, "")),
            $"two different sources never match, and a null source matches nothing — including another null, which would otherwise make every sourceless ref the same target");
    }

    /// <summary>The tap/hold decoding. The suite reads no gamepad and no bare key press, so what
    /// is pinned here is everything BETWEEN the device read and the
    /// action: <see cref="TapHoldButton"/>'s tap-versus-hold rule, and the attacker queue's live
    /// wiring through a real <see cref="FlightController.TakeProjectileHit"/> on real rigs in a real
    /// pool. The key and pad reads themselves are owed as live play.</summary>
    private static void TargetInputModel(TestContext ctx)
    {
        // --- the tap/hold decision: pure, no device involved --------------------------------
        const float dt = 1f / 60f;
        (int Taps, int Holds) Press(TapHoldButton b, int downFrames)
        {
            int taps = 0, holds = 0;
            void Count(TapHold r)
            {
                if (r == TapHold.Tap)
                {
                    taps++;
                }
                else if (r == TapHold.Hold)
                {
                    holds++;
                }
            }

            for (int i = 0; i < downFrames; i++)
            {
                Count(b.Step(true, dt));
            }

            Count(b.Step(false, dt));
            return (taps, holds);
        }

        var btn = new TapHoldButton(0.25f);
        ctx.Check(btn.Step(false, dt) == TapHold.None,
            $"a button that is simply up reports nothing — a release with no press is not a tap");
        ctx.Check(Press(btn, 12) == (1, 0),
            $"a 0.20 s press taps once on RELEASE and never holds");
        ctx.Check(Press(btn, 18) == (0, 1),
            $"a 0.30 s press holds once and the release is then SPENT — it does not also tap, which is the flicker decision 7 exists to avoid");
        ctx.Check(Press(btn, 120) == (0, 1),
            $"holding for two seconds still fires exactly once — this is a tap/hold split, not a repeat");
        ctx.Check(Press(btn, 6) == (1, 0),
            $"and the next press taps again, so a hold leaves no state behind");

        // --- the attacker queue, wired through a real hit ------------------------------------
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var gun = weapons.All.FirstOrDefault(w => w.IsGun && w.ArmorDamage is > 0f);
        ctx.Check(gun != null, $"a gun with ARMOR_DAMAGE exists in the data");
        if (gun == null)
        {
            return;
        }

        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? self = null;
        FlightController? hostile = null;
        FlightController? friendly = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            FlightController BuildRig(int playerIndex, int team, Vector3 pos)
            {
                var model = new PlaneBuilder(planesGamez, textures).Build(ctx.PlaneName);
                var rig = new FlightController
                {
                    PlaneModel = model,
                    Collider = PlaneCollider.Build(model),
                    Damage = new PlaneDamage(stats.DestroyableParts),
                    PlayerIndex = playerIndex,
                    Team = team,
                    Projectiles = live,
                    UseKeyboard = false,
                    AllowPause = false,
                };
                rig.AddChild(model);
                rig.Setup(new FlightModel(stats), ctx.Camera, new CamParams(), pos, pos + Vector3.Forward);
                ctx.Host.AddChild(rig);
                return rig;
            }

            self = BuildRig(0, AimAssist.PlayerTeam, Vector3.Zero);
            friendly = BuildRig(1, AimAssist.PlayerTeam, new Vector3(0f, 0f, -200f));
            hostile = BuildRig(2, InstantActionRuntime.EnemyTeam, new Vector3(0f, 0f, -400f));
            self.Targeting = new TargetSelection();

            ctx.Check(ReferenceEquals(live.RigOfShooter(2), hostile)
                      && ReferenceEquals(live.RigOfShooter(0), self),
                $"RigOfShooter resolves a shooter id to the plane that fired — the ids are unique across the session, so it names one plane and not a class of them");
            ctx.Check(live.RigOfShooter(ProjectilePool.NoShooter) == null
                      && live.RigOfShooter(9999) == null,
                $"…and an unowned round or an unregistered id resolves to nothing");

            var impact = new Vector3(0f, 0f, -2f);
            self.TakeProjectileHit(gun, impact, "nose", ProjectilePool.NoShooter);
            ctx.Check(self.Targeting.Attackers.Count == 0,
                $"an unowned round (a turret's, a zeppelin broadside) records no attacker — there is nobody to target");
            self.TakeProjectileHit(gun, impact, "nose", friendly.PlayerIndex);
            ctx.Check(self.Targeting.Attackers.Count == 0,
                $"friendly fire records no attacker either: the engine's gate is a shooter on a DIFFERENT, non-zero team");
            self.TakeProjectileHit(gun, impact, "nose", hostile.PlayerIndex);
            ctx.Check(self.Targeting.Attackers.Count == 1
                      && ReferenceEquals(self.Targeting.Attackers[0], hostile),
                $"a hostile round puts its shooter on the queue Next Enemy walks first count={self.Targeting.Attackers.Count}");
            self.TakeProjectileHit(gun, impact, "nose", hostile.PlayerIndex);
            ctx.Check(self.Targeting.Attackers.Count == 1,
                $"…and a second round from the same shooter does not list it twice");
        }
        finally
        {
            self?.Free();
            friendly?.Free();
            hostile?.Free();
            pool?.Free();
            textures.Dispose();
        }
    }

    // TargetSelection, tree-free and data-free: plain object sources through the real TargetPool,
    // a plane at the origin on the identity basis, no world. The geometry is chosen so the decoded
    // sector order and a plain nearest-in-space order DISAGREE (nearest is 100 m off the right
    // wing, the cycle head is 400 m ahead), so an implementation that sorted by range fails.
    private static void TargetSelectionModel(TestContext ctx)
    {
        var ahead1 = new object();      // 900 m ahead   -> sector 0
        var ahead2 = new object();      // 400 m ahead   -> sector 0, nearer
        var behind = new object();      // 200 m behind  -> sector 1
        var left = new object();        // 200 m left    -> sector 2
        var right = new object();       // 100 m right   -> sector 3, the nearest thing in space
        var ally = new object();        // 300 m ahead, own team
        int own = AimAssist.PlayerTeam;
        int foe = InstantActionRuntime.EnemyTeam;
        var basis = Basis.Identity;

        AimCandidateSet Scan(params (object Src, Vector3 At, int Team)[] entries)
        {
            var s = new AimCandidateSet();
            foreach (var (src, at, team) in entries)
            {
                s.AddVehicle(at, Vector3.Zero, team, live: true, src);
            }

            return s;
        }

        var full = Scan(
            (ahead1, new Vector3(0f, 0f, -900f), foe),
            (ahead2, new Vector3(0f, 0f, -400f), foe),
            (behind, new Vector3(0f, 0f, 200f), foe),
            (left, new Vector3(-200f, 0f, 0f), foe),
            (right, new Vector3(100f, 0f, 0f), foe),
            (ally, new Vector3(0f, 0f, -300f), own));

        // The sector key itself, against the decode's own table.
        ctx.Check(TargetSelection.SectorKey(Vector3.Forward * 5f, basis, false) == 0
                  && TargetSelection.SectorKey(Vector3.Back * 5f, basis, false) == 1
                  && TargetSelection.SectorKey(Vector3.Left * 5f, basis, false) == 2
                  && TargetSelection.SectorKey(Vector3.Right * 5f, basis, false) == 3
                  && TargetSelection.SectorKey(Vector3.Right * 5f, basis, true) == -1,
            $"FUN_004bbd60's sectors: ahead 0, behind 1, left 2, right 3, and an objective overrides to -1");

        // An objective is hand-filed: nothing sets TargetRef.Objective yet, but the -1 key is the
        // cycle's first rule. The pool is driven directly, not through Rebuild, so the objective is
        // there on the FIRST resolve; pre-resolving would make the auto-acquire claim vacuous.
        var objective = new object();
        var sel = new TargetSelection();
        sel.Pool.Rebuild(full, null, own, null);
        sel.Pool.Add(TargetRef.ForStructure(
            new AimCandidate { Position = new Vector3(0f, 0f, 1500f), Team = foe, Live = true, Source = objective },
            TargetClass.Enemy, "Promised Land", "Zeppelin", "Destroy", objective: true));
        sel.Resolve(Vector3.Zero, basis);

        var order = sel.Ordered.Select(t => t.Source).ToList();
        ctx.Check(order.SequenceEqual(new[] { objective, ahead2, ahead1, behind, left, right }),
            $"the whole cycle order in one read: the objective first (1500 m BEHIND, and still first), then ahead nearest-first, then behind, left, right — the 100 m target off the right wing is LAST");
        ctx.Check(sel.Current is { } head && ReferenceEquals(head.Source, objective)
                  && sel.ActiveClass == TargetClass.Enemy,
            $"auto-acquire: a fresh selector starts on the Enemy cycle already holding its head, with no input");
        ctx.Check(!sel.Ordered.Any(t => ReferenceEquals(t.Source, ally)),
            $"…and the ally is not in the Enemy cycle at all");

        // Stepping.
        sel.Next(TargetClass.Enemy);
        ctx.Check(ReferenceEquals(sel.Current?.Source, objective),
            $"a handler mutates state only — Current still reads the old target until the next Resolve publishes it, which is the original's own one-frame shape");
        sel.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, ahead2), $"Next steps one entry down the cycle");
        sel.Previous(TargetClass.Enemy);
        sel.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, objective), $"Previous steps back up");
        sel.Previous(TargetClass.Enemy);
        sel.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, right),
            $"…and wraps past the head to the tail");
        sel.Next(TargetClass.Enemy);
        sel.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, objective), $"…and back again past the tail");

        // Nearest is the HEAD, not the nearest thing.
        sel.Next(TargetClass.Enemy);
        sel.Next(TargetClass.Enemy);
        sel.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, ahead1), $"walked two down the cycle");
        sel.Nearest(TargetClass.Enemy);
        sel.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, objective),
            $"'Nearest' returns to the HEAD of the cycle, which is 1500 m away, not the 100 m target off the wing");

        // Death: the selected target leaves the pool. It drops to the HEAD, not to its neighbour.
        sel.Next(TargetClass.Enemy);
        sel.Next(TargetClass.Enemy);
        sel.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, ahead1), $"holding the third entry");
        var afterDeath = Scan(
            (ahead2, new Vector3(0f, 0f, -400f), foe),
            (behind, new Vector3(0f, 0f, 200f), foe),
            (left, new Vector3(-200f, 0f, 0f), foe),
            (right, new Vector3(100f, 0f, 0f), foe));
        sel.Rebuild(afterDeath, null, own, null, Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, ahead2),
            $"the selected target dying drops to the HEAD of the cycle, never to the dead entry's neighbour (which would have been 'behind')");

        // Stickiness: nothing but death, input and the explicit clear moves it.
        sel.Next(TargetClass.Enemy);
        sel.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, behind), $"holding a mid-cycle entry");
        sel.Rebuild(afterDeath, null, own, null, Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, behind),
            $"own respawn (a rebuild with the target still alive) preserves the live selection");
        var orderBefore = sel.Ordered.Select(t => t.Source).ToList();
        var farBasis = new Basis(Vector3.Up, Mathf.Pi * 0.75f);
        sel.Rebuild(afterDeath, null, own, null, new Vector3(4000f, 900f, -6000f), farBasis);
        ctx.Check(ReferenceEquals(sel.Current?.Source, behind)
                  && !sel.Ordered.Select(t => t.Source).SequenceEqual(orderBefore),
            $"flying 7 km away and swinging the nose onto a new bearing re-sorts the cycle but does NOT drop the selection — there is no range, bearing or LOS gate anywhere in the decoded path");

        // Target Nothing STAYS cleared.
        sel.Clear();
        ctx.Check(sel.Current == null && sel.ActiveClass == null, $"Target Nothing clears the target and every class flag");
        for (int i = 0; i < 3; i++)
        {
            sel.Rebuild(afterDeath, null, own, null, Vector3.Zero, basis);
        }

        ctx.Check(sel.Current == null && sel.Pool.Count == 0,
            $"…and STAYS cleared through repeated rebuilds — the collection pass is skipped, so the auto-acquire cannot fire again pool={sel.Pool.Count}");
        sel.Next(TargetClass.Enemy);
        sel.Rebuild(afterDeath, null, own, null, Vector3.Zero, basis);
        ctx.Check(sel.Current != null && sel.ActiveClass == TargetClass.Enemy,
            $"…until a class action presses, which is the only thing that ends the clear");

        // Nearest crosshairs: the NOSE cone, friend or foe, 2 km cap.
        var crosshair = new TargetSelection();
        var coneScan = Scan(
            (ally, new Vector3(0f, 0f, -300f), own),                 // dead ahead, friendly
            (right, new Vector3(100f, 0f, -100f), foe),              // 45° off the nose: outside 15°
            (ahead1, new Vector3(0f, 0f, -2400f), foe));             // on the nose but past 2 km
        crosshair.Rebuild(coneScan, null, own, null, Vector3.Zero, basis);
        ctx.Check(crosshair.NearestCrosshairs(Vector3.Zero, basis),
            $"nearest-crosshairs finds something in the cone");
        crosshair.Resolve(Vector3.Zero, basis);
        ctx.Check(crosshair.ActiveClass == TargetClass.Ally
                  && ReferenceEquals(crosshair.Current?.Source, ally),
            $"it reaches an ALLY 300 m dead ahead and writes the class back to Ally, so the next Next/Previous continues in that cycle");
        ctx.Check(!new TargetSelection().NearestCrosshairs(Vector3.Zero, basis),
            $"an empty pool finds nothing");
        var farOnly = new TargetSelection();
        farOnly.Rebuild(Scan((ahead1, new Vector3(0f, 0f, -2400f), foe)), null, own, null, Vector3.Zero, basis);
        ctx.Check(!farOnly.NearestCrosshairs(Vector3.Zero, basis),
            $"a target dead on the nose but past the hard 2 km cap is never picked");
        var offAxis = new TargetSelection();
        offAxis.Rebuild(Scan((right, new Vector3(100f, 0f, -100f), foe)), null, own, null, Vector3.Zero, basis);
        ctx.Check(!offAxis.NearestCrosshairs(Vector3.Zero, basis),
            $"a target 45° off the nose is outside the 15° half-angle cone, however close");

        // 0x24's attacker queue, walked backwards from the end.
        var shot = new TargetSelection();
        shot.Rebuild(full, null, own, null, Vector3.Zero, basis);
        shot.RecordAttacker(ahead1);
        shot.RecordAttacker(right);
        ctx.Check(shot.Attackers.Count == 2 && ReferenceEquals(shot.Attackers[1], right),
            $"the queue is an end insert, oldest first");
        shot.RecordAttacker(ahead1);
        ctx.Check(shot.Attackers.Count == 2 && ReferenceEquals(shot.Attackers[1], ahead1),
            $"a repeat attacker MOVES to the end rather than listing twice (inference, not decode — FUN_004bc1e0 was not traced)");
        shot.NextEnemy();
        shot.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(shot.Current?.Source, ahead1),
            $"Next Enemy with a target not in the queue takes the MOST RECENT attacker, ignoring the ordinary cycle");
        shot.NextEnemy();
        shot.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(shot.Current?.Source, right),
            $"…pressing again walks BACKWARDS to an older attacker");
        shot.NextEnemy();
        shot.Resolve(Vector3.Zero, basis);
        ctx.Check(ReferenceEquals(shot.Current?.Source, ahead2),
            $"…and from the queue's FIRST entry it falls through to an ordinary +1 step, which from the cycle's tail wraps to its head");
        shot.ForgetTarget(ahead1);
        ctx.Check(shot.Attackers.Count == 1 && ReferenceEquals(shot.Attackers[0], right),
            $"the death hook prunes the queue, so a dead shooter is never offered again");
    }

    // --target=. Everything the flag means lives in ApplyInitial and Select, which take a pose and
    // no tree, so the whole grammar is pinned here rather than only by the two screenshot runs. The
    // pool is built from REAL sources rather than hand-filed refs, because the claim is about the
    // names TargetPool actually produces.
    private static void TargetFlagModel(TestContext ctx)
    {
        int own = AimAssist.PlayerTeam;
        int foe = InstantActionRuntime.EnemyTeam;
        var basis = Basis.Identity;
        var self = new FlightController { Name = "player_fury", Team = own };
        var far = new FlightController { Name = "ai1_player_fury", IsHumanPiloted = false, Team = foe };
        var near = new FlightController { Name = "ai2_player_fury", IsHumanPiloted = false, Team = foe };
        var wing = new FlightController { Name = "wing1_kestrel", IsHumanPiloted = false, Team = own };
        var gasbagNode = new Node3D { Name = "gasbag1" };
        try
        {
            ctx.Host.AddChild(gasbagNode);
            var scan = new AimCandidateSet();
            scan.AddVehicle(Vector3.Zero, Vector3.Zero, own, live: true, self);
            scan.AddVehicle(new Vector3(0f, 0f, -900f), Vector3.Zero, foe, live: true, far);
            scan.AddVehicle(new Vector3(0f, 0f, -400f), Vector3.Zero, foe, live: true, near);
            scan.AddVehicle(new Vector3(0f, 0f, -300f), Vector3.Zero, own, live: true, wing);
            var registry = new DestructibleRegistry();
            var gasbagInst = registry.Register(
                new AnimDefinition { Name = "gasbag1", AnimName = "zep_zone_gasbag1" }, gasbagNode, 200f);
            var parts = new List<AimCandidate>
            {
                new() { Position = new Vector3(60f, 0f, -600f), Velocity = Vector3.Zero, Team = AimAssist.WorldTeam, Live = true, Source = gasbagInst },
            };

            TargetSelection Fresh()
            {
                var s = new TargetSelection();
                s.Rebuild(scan, parts, own, self, Vector3.Zero, basis);
                return s;
            }

            ctx.Check(SessionSpec.Parse(new[] { "--target=ai1_player_fury" }).TargetSelect == "ai1_player_fury"
                      && SessionSpec.Parse(System.Array.Empty<string>()).TargetSelect == null,
                $"--target= reaches the spec verbatim, and its absence is null rather than 'none' — an unscripted session keeps the ordinary auto-acquire");

            // The auto-acquire picks the NEARER enemy ahead. Everything below that names the far one
            // is therefore a claim the flag actually moved the selection.
            var auto = Fresh();
            ctx.Check(ReferenceEquals(auto.Current?.Source, near),
                $"CONTROL: with no flag the pool auto-acquires the nearer enemy ahead, so pinning the far one cannot pass by coincidence");

            var pinned = Fresh();
            ctx.Check(pinned.ApplyInitial("ai1_player_fury", Vector3.Zero, basis)
                      && ReferenceEquals(pinned.Current?.Source, far)
                      && pinned.ActiveClass == TargetClass.Enemy,
                $"--target=<node name> pins the named aircraft, not the one the auto-acquire chose");
            var upper = Fresh();
            ctx.Check(upper.ApplyInitial("AI1_PLAYER_FURY", Vector3.Zero, basis)
                      && ReferenceEquals(upper.Current?.Source, far),
                $"…matched case-insensitively, so a shell's capitalisation cannot change what a golden shot frames");

            // One grammar reaches all three cycles, which is this item's open TODO settled: a
            // zeppelin sub-part is named by its own world node (TargetPool.NameOf -> Instance.Anchor),
            // exactly the shape an aircraft's name has, so no second grammar is needed for it.
            var ally = Fresh();
            ctx.Check(ally.ApplyInitial("wing1_kestrel", Vector3.Zero, basis)
                      && ReferenceEquals(ally.Current?.Source, wing)
                      && ally.ActiveClass == TargetClass.Ally,
                $"the same grammar reaches an ALLY, writing the class back — without that the next Resolve would drop a target outside the active cycle");
            var part = Fresh();
            ctx.Check(part.ApplyInitial("gasbag1", Vector3.Zero, basis)
                      && ReferenceEquals(part.Current?.Source, gasbagInst)
                      && part.Current?.Kind == AimTargetKind.Structure
                      && part.ActiveClass == TargetClass.NonAircraft,
                $"…and a zeppelin SUB-PART by its part node's name, on the Non-Aircraft cycle: the TODO's premise (a sub-part has no node name of an aircraft's shape) is wrong, so one grammar covers all three");

            // The four words, each mapping onto the ordinary action rather than a scripted path.
            var nearest = Fresh();
            nearest.Next(TargetClass.Enemy);      // walk off the head first, so returning to it means something
            nearest.Resolve(Vector3.Zero, basis);
            ctx.Check(ReferenceEquals(nearest.Current?.Source, far)
                      && nearest.ApplyInitial("nearest", Vector3.Zero, basis)
                      && ReferenceEquals(nearest.Current?.Source, near),
                $"--target=nearest is the original's Nearest action: the HEAD of the cycle");
            var next = Fresh();
            ctx.Check(next.ApplyInitial("next", Vector3.Zero, basis)
                      && ReferenceEquals(next.Current?.Source, far),
                $"--target=next steps the enemy cycle once from the auto-acquired head");
            var cross = Fresh();
            ctx.Check(cross.ApplyInitial("crosshair", Vector3.Zero, basis)
                      && ReferenceEquals(cross.Current?.Source, wing),
                $"--target=crosshair runs the nose-cone scan, which reaches the nearest thing on the nose whatever its side — here the ally at 300 m");
            var cleared = Fresh();
            ctx.Check(cleared.ApplyInitial("none", Vector3.Zero, basis)
                      && cleared.Current == null && cleared.ActiveClass == null,
                $"--target=none is Target Nothing: no target and no class");
            cleared.Rebuild(scan, parts, own, self, Vector3.Zero, basis);
            ctx.Check(cleared.Current == null && cleared.Pool.Count == 0,
                $"…and stays cleared through the next rebuild, so a --screenshot run can capture the HUD with nothing selected");

            // A name nothing carries: the selection is left exactly as it was.
            var miss = Fresh();
            ctx.Check(!miss.ApplyInitial("ai7_nonesuch", Vector3.Zero, basis)
                      && ReferenceEquals(miss.Current?.Source, near),
                $"an unknown name reports failure and leaves the selection alone rather than clearing it");

            // Determinism, the flag's whole purpose: the claim the two screenshot runs make, made
            // here against two independently built selectors.
            var runA = Fresh();
            var runB = Fresh();
            runA.ApplyInitial("ai1_player_fury", Vector3.Zero, basis);
            runB.ApplyInitial("ai1_player_fury", Vector3.Zero, basis);
            ctx.Check(ReferenceEquals(runA.Current?.Source, runB.Current?.Source)
                      && ReferenceEquals(runA.Current?.Source, far),
                $"two selectors given the same spec land on the same target — the property a reproducible golden shot rests on");

            // ⚠ The item's own trap: the flag sets the INITIAL selection and must not hold it.
            var live = Fresh();
            live.ApplyInitial("ai1_player_fury", Vector3.Zero, basis);
            live.Next(TargetClass.Enemy);
            live.Resolve(Vector3.Zero, basis);
            ctx.Check(ReferenceEquals(live.Current?.Source, near),
                $"a keypress after the flag moves the selection off the pinned target");
            live.Rebuild(scan, parts, own, self, Vector3.Zero, basis);
            ctx.Check(ReferenceEquals(live.Current?.Source, near),
                $"…and the next frame's rebuild does NOT snap back to it — the flag is spent, so an interactive session started with it still cycles");
        }
        finally
        {
            gasbagNode.Free();
            self.Free();
            far.Free();
            near.Free();
            wing.Free();
        }
    }

    // TargetPool. The pure half runs over a hand-built AimCandidateSet with no world, which pins
    // every membership and exclusion rule; the world half runs C1's REAL emplacement census through
    // the same pool, because "an emplacement is selectable" is a claim about objects the session
    // builds. The carried-gunner exclusion rides the turret-gunner suite, where one already exists.
    private static void TargetPoolModel(TestContext ctx)
    {
        var self = new FlightController { PlayerIndex = 1, Team = AimAssist.PlayerTeam };
        var wingman = new FlightController { IsHumanPiloted = false, Team = AimAssist.PlayerTeam };
        var enemy = new FlightController { IsHumanPiloted = false, Team = InstantActionRuntime.EnemyTeam };
        var deadEnemy = new FlightController { IsHumanPiloted = false, Team = InstantActionRuntime.EnemyTeam };
        var neutral = new FlightController { IsHumanPiloted = false, Team = AimAssist.NeutralTeam };
        var crate = new Node3D { Name = "crate1" };
        var gasbag = new Node3D { Name = "gasbag1" };
        var deadEngine = new Node3D { Name = "engine2" };
        try
        {
            ctx.Host.AddChild(crate);
            ctx.Host.AddChild(gasbag);
            ctx.Host.AddChild(deadEngine);

            var scan = new AimCandidateSet();
            scan.AddVehicle(Vector3.Zero, Vector3.Zero, self.Team, live: true, self);
            scan.AddVehicle(new Vector3(0f, 0f, -100f), Vector3.Zero, wingman.Team, live: true, wingman);
            scan.AddVehicle(new Vector3(0f, 0f, -400f), Vector3.Zero, enemy.Team, live: true, enemy);
            scan.AddVehicle(new Vector3(0f, 0f, -450f), Vector3.Zero, deadEnemy.Team, live: false, deadEnemy);
            scan.AddVehicle(new Vector3(0f, 0f, -500f), Vector3.Zero, neutral.Team, live: true, neutral);

            // The registry, fed the way the GUN assist feeds it. The pool must ignore all of it.
            var registry = new DestructibleRegistry();
            var crateInst = registry.Register(
                new AnimDefinition { Name = "crate", AnimName = "crate_blow" }, crate, 30f);
            scan.AddStructures(registry);
            scan.AddOrdnance(new Vector3(0f, 0f, -50f), Vector3.Zero, InstantActionRuntime.EnemyTeam,
                new object());

            var pool = new TargetPool();
            pool.Rebuild(scan, null, self.Team, self);
            ctx.Check(pool.Enemy.Count == 1 && ReferenceEquals(pool.Enemy[0].Source, enemy),
                $"the hostile-team plane is the only Enemy entry count={pool.Enemy.Count}");
            ctx.Check(pool.Ally.Count == 2
                      && pool.Ally.Any(t => ReferenceEquals(t.Source, wingman))
                      && pool.Ally.Any(t => ReferenceEquals(t.Source, neutral)),
                $"a wingman lands in ALLY, not Enemy, and so does a neutral (either side unaffiliated is Ally) count={pool.Ally.Count}");
            ctx.Check(!pool.Enemy.Concat(pool.Ally).Concat(pool.NonAircraft)
                    .Any(t => ReferenceEquals(t.Source, self)),
                $"the selecting plane is excluded from its own pool");
            ctx.Check(!pool.Enemy.Any(t => ReferenceEquals(t.Source, deadEnemy)),
                $"a dead plane is listed by the collector but absent from the cycles");
            ctx.Check(pool.NonAircraft.Count == 0 && scan.Structures.Count == 1
                      && scan.Ordnance.Count == 1,
                $"the registry and the ordnance list contribute NOTHING though both are populated structures={scan.Structures.Count} ordnance={scan.Ordnance.Count} nonAircraft={pool.NonAircraft.Count}");
            ctx.Check(!pool.Enemy.Concat(pool.Ally).Concat(pool.NonAircraft)
                    .Any(t => ReferenceEquals(t.Source, crateInst)),
                $"…and specifically the crate never becomes selectable (decision 8: ours would walk every crate and fence, the original's walks a curated targets.zrd list)");

            // The able-to-fail control: derive the side from the pilot index the way the HUD used
            // to, and P2's own wingman turns hostile. ⚠ The real enemy now stays in Enemy, where it
            // used to drop out — that derived side WAS EnemyTeam until the versus band (BL-403).
            pool.Rebuild(scan, null, AimAssist.TeamOfPilot(self.PlayerIndex), self);
            ctx.Check(pool.Enemy.Any(t => ReferenceEquals(t.Source, wingman))
                      && AimAssist.TeamOfPilot(self.PlayerIndex) != InstantActionRuntime.EnemyTeam,
                $"CONTROL: deriving P2's side from its pilot index puts the wingman in Enemy — the bug this item diagnosed — and no longer collides with the enemy team itself");

            // Sub-parts: the only channel by which a structure becomes selectable.
            var gasbagInst = registry.Register(
                new AnimDefinition { Name = "gasbag1", AnimName = "zep_zone_gasbag1" }, gasbag, 200f);
            var engineInst = registry.Register(
                new AnimDefinition { Name = "engine2", AnimName = "zep_zone_engine2" }, deadEngine, 100f);
            engineInst.Status = DestructibleRegistry.State.Destroyed;
            var hullVel = new Vector3(0f, 0f, -12f);
            var parts = new List<AimCandidate>
            {
                new() { Position = gasbag.GlobalPosition, Velocity = hullVel, Team = AimAssist.WorldTeam, Live = true, Source = gasbagInst },
                new() { Position = deadEngine.GlobalPosition, Velocity = hullVel, Team = AimAssist.WorldTeam, Live = false, Source = engineInst },
            };
            pool.Rebuild(scan, parts, self.Team, self);
            ctx.Check(pool.NonAircraft.Count == 1
                      && ReferenceEquals(pool.NonAircraft[0].Source, gasbagInst),
                $"a zeppelin contributes its live parts to Non-Aircraft count={pool.NonAircraft.Count}");
            ctx.Check(!pool.NonAircraft.Any(t => ReferenceEquals(t.Source, engineInst)),
                $"a DESTROYED engine is absent");
            var bag = pool.NonAircraft[0];
            ctx.Check(bag.Kind == AimTargetKind.Structure && bag.Velocity == hullVel
                      && bag.Name == "gasbag1"
                      && bag.Health is { } bh && Mathf.IsEqualApprox(bh, 1f) && bag.Armor == null,
                $"…carrying its hull's velocity (never zero — the bracket gate has to lead it), its part node's name and health with no armor pool name='{bag.Name}' v={bag.Velocity}");

            // --- C1's real emplacements through the same pool --------------------------------
            ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
            string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
            ctx.RequireData(texturesPath, $"C1 textures");
            var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
            var turretDefs = TurretDefs.Load(ctx.ZrdrPath);
            ctx.WithWorld("C1", collision: false, world =>
            {
                var textures = new TextureArchive(texturesPath);
                ProjectilePool? live = null;
                Session.TurretEmplacementRuntime? emplacements = null;
                try
                {
                    live = new ProjectilePool(textures, null, null);
                    ctx.Host.AddChild(live);
                    emplacements = new Session.TurretEmplacementRuntime(turretDefs, weapons,
                        (pattern, scope) => world.Runtime.FindNodes(pattern, scope), live,
                        world.Runtime.WorldRoot);
                    // The runtime registers its own emplacements with the pool; a second
                    // RegisterWorldTurrets here would list every gun twice.
                    var worldScan = new AimCandidateSet();
                    live.CollectTurrets(worldScan);
                    var worldPool = new TargetPool();
                    worldPool.Rebuild(worldScan, null, AimAssist.PlayerTeam, null);
                    int aliveEmplacements = emplacements.Emplacements.Count(t => t.Alive);
                    ctx.Check(worldScan.Turrets.Count == emplacements.Count && aliveEmplacements > 0,
                        $"C1's whole emplacement census reaches the scan turrets={worldScan.Turrets.Count} of {emplacements.Count}");
                    ctx.Check(worldPool.NonAircraft.Count > 0
                              && worldPool.NonAircraft.All(t => t.Kind == AimTargetKind.Turret)
                              && worldPool.Enemy.Count == 0 && worldPool.Ally.Count == 0,
                        $"every selectable emplacement lands on the NON-AIRCRAFT cycle, never Enemy or Ally, whatever its team nonAircraft={worldPool.NonAircraft.Count}");
                    ctx.Check(worldPool.NonAircraft.All(t => t.Health == null && t.Armor == null
                                  && t.Name.Length > 0),
                        $"…each with its TITLE@site label and no health figure at all (the retail loaders read no HEALTH key)");
                    ctx.Note($"C1 target pool: {worldPool.NonAircraft.Count} selectable emplacements of {emplacements.Count} placed, {aliveEmplacements} alive");
                }
                finally
                {
                    emplacements?.Free();
                    live?.Free();
                    textures.Dispose();
                }
            });
        }
        finally
        {
            self.Free();
            wingman.Free();
            enemy.Free();
            deadEnemy.Free();
            neutral.Free();
            crate.Free();
            gasbag.Free();
            deadEngine.Free();
        }
    }

    // The targeting HUD on AI hostiles, in two halves. The pure selection (TargetHud.NearestHostile
    // over a constructed candidate set, no scene) pins the filters and HostileTag; the in-engine half
    // runs the tracker against real spawned AI planes in a live pool, covering acquisition, the
    // switch to a closer hostile, the crash drop and the empty-pool null. A hud built without a
    // pool never tracks, which is the seam that keeps the golden VS output untouched.
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
            ctx.Check(ReferenceEquals(TargetHud.NearestHostile(Vector3.Zero, ownTeam, set), pureNear),
                $"the nearest LIVE AI hostile wins over a closer human, a closer dead plane and a closer neutral");
            ctx.Check(TargetHud.NearestHostile(Vector3.Zero, AimAssist.NeutralTeam, set) == null,
                $"a neutral own side targets nothing (the engine's either-side-0 rule)");
            ctx.Check(TargetHud.NearestHostile(Vector3.Zero, ownTeam, new AimCandidateSet()) == null,
                $"an empty scan tracks nothing");
            ctx.Check(TargetHud.HostileTag("ai1_player_fury") == "AI1"
                && TargetHud.HostileTag("bandit") == "BANDIT" && TargetHud.HostileTag("") == "AI",
                $"the marker tag is the name's first segment uppercased, 'AI' as the fallback");

            // The wingman-in-the-marker bug: OwnTeam must read the pane's own Team FIELD. The pilot
            // index derivation is right for P1 by coincidence and wrong for P2-P4 the moment a
            // mission sets teams, which every Instant Action and --coop session does.
            var p2 = new TargetHud { PlayerIndex = 1 };
            ctx.Check(p2.OwnTeam == AimAssist.TeamOfPilot(1),
                $"with no aircraft bound the HUD still falls back to the pilot-index derivation own={p2.OwnTeam}");
            p2.Own = pureHuman;
            pureHuman.Team = AimAssist.PlayerTeam;
            ctx.Check(p2.OwnTeam == AimAssist.PlayerTeam
                      && AimAssist.TeamOfPilot(1) != AimAssist.PlayerTeam,
                $"P2 flying an Instant Action mission is on the PLAYER team, which its pilot index would have derived as {AimAssist.TeamOfPilot(1)} — the wingman-in-the-marker bug");
            var wingScan = new AimCandidateSet();
            wingScan.AddVehicle(new Vector3(0f, 0f, -80f), Vector3.Zero, AimAssist.PlayerTeam,
                live: true, pureNear);                                  // P2's own wingman
            wingScan.AddVehicle(new Vector3(0f, 0f, -900f), Vector3.Zero,
                InstantActionRuntime.EnemyTeam, live: true, pureFar);   // the actual enemy
            ctx.Check(ReferenceEquals(
                    TargetHud.NearestHostile(Vector3.Zero, p2.OwnTeam, wingScan), pureFar),
                $"…so the far ENEMY is tracked and the near wingman is not");
            ctx.Check(ReferenceEquals(
                    TargetHud.NearestHostile(Vector3.Zero, AimAssist.TeamOfPilot(1), wingScan),
                    pureNear),
                $"CONTROL: the old derivation tracks the WINGMAN instead, and skips the enemy as own-team");
            p2.Free();
        }
        finally
        {
            pureNear.Free();
            pureFar.Free();
            pureDead.Free();
            pureHuman.Free();
        }

        // --- The shipped marker's rules: colour, the gun-reach bracket gate, the label lines -----
        // All three are pure and decoded (FUN_004a5f40 / FUN_004574d0 / FUN_004579e0); what no test
        // can reach is the drawn geometry itself, which is the c1-targeting-hud golden.
        int hostileTeam = AimAssist.TeamOfPilot(100);
        var enemyRef = TargetRef.ForAircraft(
            new AimCandidate { Team = hostileTeam, Live = true, Source = new object() },
            TargetClass.Enemy, "ai1_player_fury", "Fury");
        var allyRef = TargetRef.ForAircraft(
            new AimCandidate { Team = AimAssist.PlayerTeam, Live = true, Source = new object() },
            TargetClass.Ally, "ai2_player_kestrel", "Kestrel");
        var destroyRef = TargetRef.ForStructure(
            new AimCandidate { Team = AimAssist.WorldTeam, Live = true, Source = new object() },
            TargetClass.Enemy, "Promised Land", "Zeppelin", "Destroy", objective: true);
        var protectRef = TargetRef.ForStructure(
            new AimCandidate { Team = AimAssist.PlayerTeam, Live = true, Source = new object() },
            TargetClass.Enemy, "Convoy", "Freighter", "Protect", objective: true);
        ctx.Check(TargetHud.MarkerColor(enemyRef, AimAssist.PlayerTeam)
                  != TargetHud.MarkerColor(allyRef, AimAssist.PlayerTeam)
                  && TargetHud.MarkerColor(allyRef, AimAssist.PlayerTeam)
                     != TargetHud.MarkerColor(protectRef, AimAssist.PlayerTeam),
            $"three colours, not two: a hostile, a friendly and a non-destructive objective all read differently (the decode's own rule — a FRIENDLY is green, blue is the objective)");
        ctx.Check(TargetHud.MarkerColor(destroyRef, AimAssist.PlayerTeam)
                  == TargetHud.MarkerColor(enemyRef, AimAssist.PlayerTeam),
            $"a Destroy objective is the hostile colour, whatever team the entity carries — the four destructive categories override the team test");
        ctx.Check(TargetHud.MarkerColor(enemyRef, AimAssist.NeutralTeam)
                  == TargetHud.MarkerColor(allyRef, AimAssist.PlayerTeam),
            $"a neutral own side has no enemies: with either team 0 the categoryless rule falls to the friendly colour");

        // The gate is the SELECTED GUN's reach through a lead solve, not a distance constant. A
        // 860 m/s round with RANGE 1000 reaches ~1 km; the same target 2 km out does not.
        const float RoundSpeed = 860f, GunRange = 1000f;
        var muzzle = Vector3.Zero;
        ctx.Check(TargetHud.GunReaches(muzzle, Vector3.Zero, RoundSpeed, GunRange,
                      new Vector3(0f, 0f, -600f), Vector3.Zero)
                  && !TargetHud.GunReaches(muzzle, Vector3.Zero, RoundSpeed, GunRange,
                      new Vector3(0f, 0f, -2000f), Vector3.Zero),
            $"a target inside the gun's authored RANGE is bracketed and one past it is not");
        ctx.Check(!TargetHud.GunReaches(muzzle, Vector3.Zero, RoundSpeed, GunRange,
                      new Vector3(0f, 0f, -600f), new Vector3(0f, 0f, -900f)),
            $"a target OUTRUNNING the round is never bracketed, at any range — the solver returns no intercept (the port of 'a fixed-metres threshold would lose this')");
        ctx.Check(!TargetHud.GunReaches(muzzle, Vector3.Zero, RoundSpeed, GunRange,
                      new Vector3(0f, 0f, -1010f), Vector3.Zero)
                  && TargetHud.GunReaches(muzzle, Vector3.Zero, RoundSpeed,
                      GunRange + TargetHud.BracketHysteresis, new Vector3(0f, 0f, -1010f),
                      Vector3.Zero),
            $"the hysteresis is what a target hovering at the boundary rides: off by the plain gate, still on by the widened one (TUNE, ours not the original's)");

        var lines = new List<string>();
        TargetHud.LabelLines(enemyRef, null, lines);
        ctx.Check(lines.Count == 2 && lines[0].Length == 0 && lines[1] == "Fury",
            $"an ordinary aircraft under a box is its airframe name alone, in the SECOND slot — the blank category line still holds the first, which is what keeps the name out of the silhouette ({string.Join(" / ", lines)})");
        lines.Clear();
        TargetHud.LabelLines(enemyRef, "4 o'clock", lines, keepSlots: false);
        ctx.Check(lines.Count == 2 && lines[0] == "Fury" && lines[1] == "4 o'clock",
            $"…and off screen, where there is no box to measure the slot from, it compacts to the two lines HUD.png shows ({string.Join(" / ", lines)})");
        lines.Clear();
        TargetHud.LabelLines(destroyRef, null, lines);
        ctx.Check(lines.Count == 2 && lines[0] == "Zeppelin [Destroy] -"
                  && lines[1] == "Promised Land",
            $"a named objective composes both lines, C1 M04 Zeppelin.png's case, with no wrap width to port ({string.Join(" / ", lines)})");

        // --- The debug-marker string: identity kept whole, health/armor gated on the source -------
        var healthyRef = TargetRef.ForAircraft(
            new AimCandidate { Team = hostileTeam, Live = true, Source = new object() },
            TargetClass.Enemy, "ai1_player_fury", "Fury",
            TargetRef.Fraction(78f, 100f), TargetRef.Fraction(91f, 100f));
        string healthyTag = TargetHud.DebugTag("AI1", healthyRef, 640f, "");
        ctx.Check(healthyTag == "AI1 Fury 640 m H78 A91",
            $"the debug tag keeps the FULL identity (unlike the shipped marker's plane-type-alone label), adds the plane type, then health and armor as whole percentages with no percent sign, health first: '{healthyTag}'");
        ctx.Check(TargetHud.DebugTag("AI1", healthyRef, 640f, "  pursue") == "AI1 Fury 640 m H78 A91  pursue",
            $"the AI mode trails the figures, carrying ModeSuffix's own leading spaces: '{TargetHud.DebugTag("AI1", healthyRef, 640f, "  pursue")}'");
        string noHealthTag = TargetHud.DebugTag("AI2", enemyRef, 250f, "");
        ctx.Check(noHealthTag == "AI2 Fury 250 m",
            $"a source with no health model (enemyRef carries none) omits BOTH figures rather than printing H100 A100: '{noHealthTag}'");

        // --- the live tracker, against real AI planes registered in a real pool ---
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? ai1 = null, ai2 = null;
        TargetHud? hud = null, noPoolHud = null;
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
            hud = TargetHud.Build(0, ctx.Camera, live);
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

            // A hud with no pool bound (HostilePool left null) never tracks whatever the pool
            // holds — the seam that keeps a golden shot with no hostile in play untouched.
            ai1.Respawn();
            noPoolHud = new TargetHud();
            noPoolHud.PlanePos = hud.PlanePos;
            noPoolHud.UpdateHostile();
            ctx.Check(noPoolHud.TrackedHostile == null,
                $"a hud built without a pool never tracks");

            // --debug-markers' own selection: EVERY live aircraft, not the nearest one, each
            // flagged by team against the pane's own. ai1 is live again (respawned above); ai2 is
            // still down, so it must not be marked at all.
            ai2.Team = AimAssist.PlayerTeam;
            var scan = new AimCandidateSet();
            live.CollectAircraft(scan);
            var marks = new List<(TargetRef Target, bool Friendly)>();
            TargetHud.CollectMarks(AimAssist.PlayerTeam, null, scan, marks);
            ctx.Check(marks.Count == 1 && ReferenceEquals(marks[0].Target.Source, ai1),
                $"a crashed plane is never marked marks={marks.Count}");
            ctx.Check(!marks[0].Friendly,
                $"ai1 is on the enemy team, so it marks hostile friendly={marks[0].Friendly}");
            string airframeName = PlaneRoster.PlaneDisplayName(stats);
            ctx.Check(marks[0].Target.DisplayName == airframeName
                      && marks[0].Target.Health == null && marks[0].Target.Armor == null,
                $"CollectMarks wraps a TargetRef carrying the airframe's display name, and a bare rig with no Damage ledger bound omits both figures: name={marks[0].Target.DisplayName} h={marks[0].Target.Health} a={marks[0].Target.Armor}");
            ai1.Team = AimAssist.PlayerTeam;
            marks.Clear();
            scan.Clear();
            live.CollectAircraft(scan);
            TargetHud.CollectMarks(AimAssist.PlayerTeam, null, scan, marks);
            ctx.Check(marks.Count == 1 && marks[0].Friendly,
                $"the same plane on the pane's own team marks friendly friendly={marks[0].Friendly}");
            marks.Clear();
            TargetHud.CollectMarks(AimAssist.PlayerTeam, ai1, scan, marks);
            ctx.Check(marks.Count == 0, $"the pane's own aircraft is excluded marks={marks.Count}");

            // Once the plane carries a damage ledger, CollectMarks' TargetRef reads it straight
            // off — the same optional-field contract a sub-part or emplacement would use once the
            // scan widens past aircraft, not a plane-specific field read of its own.
            ai1.Damage = new PlaneDamage(stats.DestroyableParts);
            var firstPart = stats.DestroyableParts.First();
            ai1.Damage.Apply(firstPart.Name, healthDamage: 5f, armorDamage: 5f);
            marks.Clear();
            scan.Clear();
            live.CollectAircraft(scan);
            TargetHud.CollectMarks(AimAssist.PlayerTeam, null, scan, marks);
            float expectHealth = TargetRef.Fraction(ai1.Damage.WholeHealth, ai1.Damage.WholeHealthMax)!.Value;
            float expectArmor = TargetRef.Fraction(ai1.Damage.WholeArmor, ai1.Damage.WholeArmorMax)!.Value;
            ctx.Check(marks.Count == 1 && marks[0].Target.Health == expectHealth
                      && marks[0].Target.Armor == expectArmor,
                $"a damaged plane's health/armor fractions come straight off its own Damage ledger h={marks[0].Target.Health} a={marks[0].Target.Armor} (expected h={expectHealth} a={expectArmor})");
            string damagedTag = TargetHud.DebugTag(TargetHud.HostileTag(marks[0].Target.Name),
                marks[0].Target, 640f, "");
            ctx.Check(damagedTag
                      == $"AI1 {airframeName} 640 m H{Mathf.RoundToInt(expectHealth * 100f)} A{Mathf.RoundToInt(expectArmor * 100f)}",
                $"the full string a live damaged plane produces: '{damagedTag}'");

            // The mode suffix the marker tag carries, in the engine's own vocabulary. A pilot
            // with no mode machine (this suite's own bare-orders spawn) adds nothing rather than
            // inventing a state; armed, it names whatever mode the machine is in.
            ctx.Check(TargetHud.ModeSuffix(ai1) == "",
                $"a pilot with no mode machine adds nothing to the tag: '{TargetHud.ModeSuffix(ai1)}'");
            ai1.Pilot!.Machine = new AiModeMachine(new System.Random(5));
            ai1.Pilot.Machine.Enter(AiMode.Pursue, "suite");
            ctx.Check(TargetHud.ModeSuffix(ai1).Trim() == "pursue",
                $"the marker tag carries the plane's mode: '{TargetHud.ModeSuffix(ai1).Trim()}'");
        }
        finally
        {
            hud?.Free();
            noPoolHud?.Free();
            ai1?.Free();
            ai2?.Free();
            pool?.Free();
            textures.Dispose();
        }
    }

    // The AI gunnery on real engine state: an AI-piloted, stock-armed plane HELD at fixed poses against
    // a parked hostile. It pins acquisition into the gunner's mutable target field, the quick-draw
    // gate, the forward gun cone, dead-eye scatter at skill 1 against 9, the kill attributed through
    // Downed, and the IsHumanPiloted assist exclusion A/B'd on one rig. Inventory: docs/architecture.md.
    // ⚠ Park the target at its spawn pose and never move it; a body moved inside the suite's single
    // frame is invisible to the rounds' space queries (INSTR-13).
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
            // rating 1 the ~54° cone refuses it; at rating 9 the 89° cone takes it.
            var beamPos = targetPos + new Vector3(0.9848f, 0f, 0.1736f) * 500f; // 80° off +Z
            ai.PlaceHeld(beamPos, targetPos);
            int ammoAtBeam = gun.Ammo;
            Step(60);
            ctx.Check(!gunner.WantsFire && gun.Ammo == ammoAtBeam,
                $"a beam-ish shot is refused at quick-draw rating 1 (~54°) rounds={ammoAtBeam - gun.Ammo}");
            gunner.QuickDrawAngleDeg = skills.QuickDrawAngleDeg(9);
            Step(60);
            ctx.Check(gun.Ammo < ammoAtBeam,
                $"the same bearing is taken at rating 9 (89°) rounds={ammoAtBeam - gun.Ammo}");

            // --- the aim gate: nose 30° off the bearing clamps to the airframe's 11° and leaves
            // a 19° residual, past the gun's 10°, so the shot is refused with quick draw willing.
            ai.PlaceHeld(beamPos, beamPos + (targetPos - beamPos).Normalized()
                .Rotated(Vector3.Up, Mathf.DegToRad(30f)) * 100f);
            int ammoAtOffBore = gun.Ammo;
            Step(60);
            ctx.Check(!gunner.WantsFire && gun.Ammo == ammoAtOffBore,
                $"a 19° residual after the traverse clamp holds fire (nose 30° off)");

            // Dead-eye scatter, skill 1 against 9 on fixed geometry: the high rear quarter at ~212 m, where the
            // planform presents real area. Dead astern the airframe is edge-on and both cones mostly miss,
            // which drowns the difference the volley is measuring.
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

            // The kill attributed to the AI's shooter id, from dead astern at 150 m. Scaffolding per the
            // whole-vehicle rule: the zones this bearing cannot reach are pre-emptied, and the AI's own fire
            // finishes the plane.
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

            // The IsHumanPiloted assist exclusion, A/B'd in place: nose 3 degrees off the bearing at 400 m,
            // gunner disarmed, trigger held raw. As an AI the rounds leave along the muzzle axis and all miss;
            // the same rig flagged human gets the assist, which snaps onto the target and lands hits.
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

            // Ranked acquisition on a hostile pair at equal geometry: the human-piloted target carries the
            // decoded 0.7 base weight and out-ranks the AI rival, a primary_target assignment overrides the
            // ranking, and a candidate beyond the activation radius scores the engine's 1e21 and is never picked.
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

            // With two humans in the scan, 'player' is a ROLE and resolves to whichever human is NEAREST this
            // attacker, not the first registered; the rival registered second, so pulling the pick across is
            // what proves distance decides. PlaceHeld is legitimate here: this branch queries no physics space.
            rival.IsHumanPiloted = true;
            var nearRivalPos = targetPos + new Vector3(0f, 0f, 500f); // 300 m from the shooter
            rival.PlaceHeld(nearRivalPos, nearRivalPos + Vector3.Forward);
            gunner.Target = null;
            Step(1);
            ctx.Check(ReferenceEquals(gunner.Target, rival),
                $"two humans: 'player' takes the NEARER one (300 m) over the first registered (800 m)");
            var nearTargetPos = targetPos + new Vector3(0f, 0f, 700f); // 100 m from the shooter
            target.PlaceHeld(nearTargetPos, nearTargetPos + Vector3.Forward);
            gunner.Target = null;
            Step(1);
            ctx.Check(ReferenceEquals(gunner.Target, target),
                $"swapping which human is nearer swaps the pick (100 m beats 300 m)");
            rival.IsHumanPiloted = false;
            rival.PlaceHeld(rivalPos, rivalPos + Vector3.Forward);
            target.PlaceHeld(targetPos, targetPos + Vector3.Forward);

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

    // The mode machine on a real flying AI rig. The reaction rolls are scripted by pinning the shipped
    // chance fields to 0/1 while the machine's own rng stays seeded, and the steady-hand entry rides a
    // REAL projectile hit through TakeProjectileHit, the same call the pool makes, so the hit-path
    // wiring is what is exercised rather than the machine API. The plane may genuinely fly between
    // phases: no phase here queries physics at a flown-to position (INSTR-13).
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
                ProbeBlocked = (_, _) => terrainBlocked ? "suite/terrain" : null,
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
            // releases back. Stepped past ProbeIntervalMaxS, since the probe's cadence is the
            // original's per-plane 0.5…1.0 s draw and this plane's is unknown to the test.
            machine.SixthSenseChance = 1f;
            float yBefore = ai.WorldPosition.Y;
            terrainBlocked = true;
            Step((int)(AiModeMachine.ProbeIntervalMaxS * 60f) + 6);
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

            // --- lay off (the rubber-band assist). Entry A/B on fixed geometry through
            // the machine's own tick: a chasing human 600 m dead astern enters lay off with
            // the assist on, and never with --no-assist's switch off.
            machine.Enter(AiMode.Pursue, "test: rejoin for lay off");
            // Pursue's own lever, to ease off FROM. ⚠ Not a fixed number: the law walks the lever
            // toward its desired speed, so what pursue is commanding here depends on how fast the
            // plane happens to be after the climb-out above.
            float leverPursuing = pilot.Throttle;
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

            // The observable assist, through the live pilot: the throttle eases off pursue's own
            // lever and the gunner's trigger is held for the whole dwell.
            Step(30);
            ctx.Check(machine.Mode == AiMode.LayOff,
                $"the anti-chatter hold keeps the mode mode={AiModeMachine.NameOf(machine.Mode)}");
            ctx.Check(pilot.Throttle < leverPursuing,
                $"the throttle is eased off pursue's own lever throttle={pilot.Throttle:0.000} from {leverPursuing:0.000}");
            ctx.Check(!pilot.Gunner.WantsFire, $"fire is held while laying off");

            // Once the hold expires the machine releases back to pursue and the lever runs up to its ceiling.
            // ⚠ Do not pin an exact throttle: pursue assigns no lever, AiControlLaw walks it at 0.35/s toward
            // its own desired speed, so an exact value would pin the settling transient instead of the release.
            Step(150);
            ctx.Check(machine.Mode == AiMode.Pursue,
                $"a non-pursuing target releases lay off after the hold mode={AiModeMachine.NameOf(machine.Mode)}");
            Step(5);
            ctx.Check(pilot.Throttle > 1f,
                $"…and pursue commands past full again throttle={pilot.Throttle:0.000}");

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

    // The voice runtime against the real soundsh archive, reproducing the session's own lifecycle in
    // order: resolve the chain, prewarm that one pilot's clips, retire the loader as WorldSession.Build
    // does, then prove a resolved line still plays while a def never prewarmed returns null. The
    // source-following one-shot is asserted by position only; audibility is the user's half
    // (docs/verification.md). Closes with the measured cost of prewarming the entire voice bank, the
    // number that justifies the roster-subset choice.
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

    // The E16 dispatch on a live AI aircraft, over the same B8 lifecycle the session
    // runs (prewarm the accent's clips, retire the loader, play after the archive is closed).
    // The talker chance is pinned to 1 so the assertions are about the dispatch rules, not the
    // dice; audibility itself is the user's half (docs/verification.md, "What this project
    // cannot verify itself") — what IS assertable is the dispatch decision, the resolved clip
    // name and the PlayOneShot call.
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

            // An inert aircraft is registered as a speaker but is not eligible: the runtime mirrors InPlay into
            // the dispatcher's own aliveness gate, the only thing here that can see a FlightController. Checked
            // in both directions, so "not eligible" cannot be the flag's resting state.
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

    // The most recent one-shot player under a WorldSounds node.
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

        // The trailer names the player at node 10, and that node carries no edge: the shipped
        // shape of all 76 anchored nets, and why the seat scan has to skip edgeless nodes.
        ctx.Check(net.Trailer is { NodeIndex: 10, Name: "player" },
            $"the trailer [10, player] rides the net trailer={net.Trailer?.ToString() ?? "-"}");
        bool anchorEdgeless = true;
        foreach (var (a, b) in net.Edges)
        {
            if (a == 10 || b == 10)
                anchorEdgeless = false;
        }
        ctx.Check(anchorEdgeless, $"…and the anchor node 10 is parked off the ring, edgeless");

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

            // The same net, now RIDING a stand-in for the player 6 km east of where it
            // was authored. The ring must move with it and keep its authored 400 m; the plane
            // must fly the moved ring, not the one in the file.
            var trailed = net.Nodes[10].Position + new Vector3(6000f, -300f, 0f);
            var anchoredFollower = new AiNetFollower(net, new System.Random(1),
                trailerTarget: () => trailed);
            ctx.Check(anchoredFollower.Anchored
                && anchoredFollower.NodePosition(0).IsEqualApprox(
                    net.Nodes[0].Position + new Vector3(6000f, 0f, 0f)),
                $"the anchored net rides its target 6 km east, Y untouched node0={anchoredFollower.NodePosition(0)}");
            pilot.Patrol = anchoredFollower;
            ai.Activate(anchoredFollower.NodePosition(0), anchoredFollower.NodePosition(1));
            bool seatedOnAnchor = false;
            for (steps = 0; anchoredFollower.Advances < 3 && steps < budget; steps++)
            {
                ai.SimStep(1f / 60f);
                if (anchoredFollower.CurrentIndex == 10)
                    seatedOnAnchor = true;   // the edgeless anchor is never a flight target
            }
            ctx.Check(anchoredFollower.Advances >= 3 && !seatedOnAnchor,
                $"…and the plane laps the MOVED ring advances={anchoredFollower.Advances} in {steps / 60f:0} s of sim");
            float onMoved = ai.WorldPosition.DistanceTo(anchoredFollower.CurrentTarget);
            float onAuthored = ai.WorldPosition.DistanceTo(net.Nodes[anchoredFollower.CurrentIndex].Position);
            ctx.Check(onMoved < onAuthored,
                $"…flying the ridden ring, not the authored one moved={onMoved:0} m authored={onAuthored:0} m");
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

    // The multi-zone damage chain on a real mission zeppelin, in the mission's own world (C1/M04, where
    // piratezep is live; the run's default IA1 switches it off). Real rounds prove the pipeline, then
    // the bulk gasbag kills go through runtime.DamageAt directly, the same sink minus the flight time.
    // ⚠ Aim rounds at the gasbag collider's BUILT pose, captured before ZeppelinRuntime places the
    // node: a moved physics body never re-enters the space queries inside one frame (INSTR-13).
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

    // The broadside chain on C1/M04's piratezep in its own mission world: arc-gated deploy, a real
    // volley at a player stand-in, hold-fire-and-retract out of arc, the thinning, scatter on a
    // cannon_inaccuracy clone, and the zeppelin-versus-zeppelin gasbag pick on constructed geometry.
    // The stand-in is repositioned through its flight model each step to hold the tested bearing.
    // ⚠ Assert rounds at the spawn seam, count and direction, never as hits: a moved body never
    // re-enters the one-frame space queries (INSTR-13).
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
                // A 30 degree bound: the hull flies on between volley and check and the muzzles sit ~100 m along
                // it. The solve's exactness is pinned engine-free (ZeppelinBroadsideTests); the in-engine claim is
                // only "at the player, out of the port side".
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

    // A copy of a shipped record with `cannon_inaccuracy` authored — the scatter
    // phase's instrument (no C1 record authors one; C2B/M04's 10° is the shipped value).
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

    // A minimal constructed zeppelin record for the zeppelin-vs-zeppelin phase:
    // near-static (rates/speed floored) so the constructed bearings hold while cannons
    // deploy on the fallback timing.
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

    // Exports a built plane to a temp .glb and asserts the file lands and re-imports with at least one
    // textured mesh: the round trip the viewer's --export-gltf=/F10 path relies on, including that the
    // shader skins convert to a glTF-serializable material.
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

    // How many MeshInstance3D in the subtree carry a material with an albedo
    // texture — the glTF importer hands each surface back a StandardMaterial3D.
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

    // The invisible-wall tripwire: after a chapter's world has bootstrapped — mission
    // setup script, RESET_STATEs, ON_STARTUP, the unplaced sweep — no collider may still be
    // enabled where nothing is drawn. Every chapter, because what each mission hides differs and
    // the failure is silent until someone flies into it (C1/IA1's `hk_zep`).
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

    // templates.zrd's substitute and scale_range, asserted as an A/B against the build that does not
    // read the spec at all: the same ClutterBuilder over the same gamez, differing only in whether a
    // spec was handed to it. Three builds, each with its own able-to-fail control: bare, dressed, and
    // dressed again in the SAME process with no Rng reset, which is the strong form because it proves
    // the stream is a function of the data alone. See docs/org/clutter.md.
    // ⚠ Never seed it from Rng.Master; that rerolls C1's forest on every unpinned launch.
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

    // The DirectionalLight3D is pointed by the flown zone's authored SUNLIGHT_ORIENTATION and keeps
    // following it when the camera's weather state moves to another zone. The CSVM.Tests units pin the
    // parse and the euler-to-direction mapping; neither can see the light wired to the wrong seam, or
    // a zone edge firing without carrying it, and a screenshot cannot either.
    // ⚠ Do not simplify this onto C1/IA1. C2/MP2 is the only shape in the install whose two zones
    // author different bearings, so anywhere else a zone change would pass with the write deleted.
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

    // The lens flare's gating, chapter by chapter. ⚠ Gate it on chapter data, never on a chapter
    // name: it reads a gamez sun node in the horizon subtree and init.gw's LensFlareTexture slot
    // registrations, both true of C2 and C3 and of nothing else. Both directions are asserted, because
    // nothing about C1 looking correct would tell you a flare rig had started building there.
    // Data-only on purpose, so it stays a fast suite rather than a third eight-chapter world sweep.
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

    // The authored STOP_SEQUENCE stops must actually run; nothing else in the gate measures an effect's
    // DURATION (--effects-test only proves a puffer builds, docs/verification.md). It asserts on the
    // dispatch timeline via AnimRuntime.OnEventDispatched, so it needs no textures: the rocket
    // fireball's ON_CALL stopper is named by a stop nothing runs and must stay silent, and the 30 s
    // fire's emitting poll loop is halted, since an un-halted Loop{-1} re-fires every frame forever.
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

    // The live-path start check the direct-Start stop-sequence suite cannot make: a real kill must
    // dispatch the def's compiled destruction slot (AnimDefinition.DeathSlot), the block carrying
    // nearly all of large_30sec_fire's death calls. An unparsed block no-ops every one of them and
    // every "the fire ends on time" check reads the absence as a pass (DIAG-20). Subject: a C1 AA gun.
    // Able to fail: with RunDeathSlot deleted, no destruction_slot lane ever dispatches.
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

    // WAIT_FOR_COMPLETION on the authored case, with its own control beside it in the same sequence.
    // player_crash_water's destroy_crash is the install's clean discriminator: eleven events, of which
    // exactly one carries the flag, followed immediately by an unflagged large_steam_spray that would
    // otherwise start with the splash instead of after it.
    // ⚠ Assert the control too, the nine unflagged calls that must still all start at t=0. A runtime
    // that held every call would pass the spray check and fail those.
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

    // An OBJECT_ACTIVE_STATE INACTIVE ends the emitters under that host, but not one that started in
    // the same instant. ⚠ Assert BOTH halves: dropping the stop entirely passes the splash half, and
    // shipping the stop unconditioned passes the debris half. They are the two populations the
    // install-wide census splits, and the split is total (analysis/bl-229-emitter-host-deactivation/).
    // SPLASH is plane_big_splash, whose emitter must survive its host's deactivation and still be gone
    // by ~0.7 s; DEBRIS is m_build01, whose deactivation is the trail's only authored stop.
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

        // ⚠ Keep the fireball template roots on the stage. small_fireball declares a puffer also called
        // trailpuffer2, and with its own root missing the name resolution falls back to the call anchor,
        // so its stop lands on the building's key and ends the debris trail early, masking the assertion.
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
            // Asserted against the DISPATCH MOMENT, never a fixed second: part3 is a bounce-solved launch, so
            // when it lands is computed rather than authored. The only other thing that could stop this trail,
            // the instance retiring, happens a second later, which a wall-clock check would blur.
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

    // Is the emitter `name` ON `host` emitting? Null when
    // no such emitter is known. Host-qualified on purpose: puffer names are NOT unique across
    // definitions — `small_fireball` declares a `trailpuffer2` of its own, and a name-only read
    // answers about whichever row comes first, which lets a debris assertion pass against a
    // runtime with the stop deleted outright.
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

    // A bare stage carrying the nodes a definition names, plus a runtime bound to it
    // through a CountingEmitterFactory. Flat children, never a hierarchy: the point is
    // to give each named host its own subtree, so a stop that reaches the wrong one is visible
    // rather than being absorbed by a shared ancestor.
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
        // The two role flags AnimRuntime.ForCrashRig sets, for a def the crash rig is the only
        // production caller of: the splash is played by the per-player rig, which relocates its own
        // called templates and holds no ExternalEffect, so the start and the stop meet on ONE director.
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

    // An effect's template MESHES must be visible at the call site while it plays and dark once it is
    // over. The world-effects stage keeps every template root hidden and the engine reveals the one a
    // call lands on (TemplateStage.Shown), so both halves are engine rules.
    // ⚠ Assert both: revealing and never hiding leaves a mesh burning at the last hit point for the
    // session, while hiding eagerly or never revealing shows nothing at all. The CALLED case is
    // he_ground_effect's staged he_ring1; the ENDED case is 3040ap_gunhit's stop-retired chunk mesh.
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

    // he_ground_effect's frame_buffer_effects1 is six FBFX_COLOR_FROM_TO steps washing the picture over
    // 1.2 s, reached through an If PlayerRange call. The handler must report each step's authored
    // run_time as its duration, because that is the only thing spacing them: report 0 and all six fire
    // in one instant. It then asserts the routing, that each step reports where the burst was and the
    // def's own gate, and that the gate answers to the NEAREST human rather than to one camera.
    // Full inventory: this module's docs/architecture.md entry.
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
                var fired = new List<(float T, Color From, Color To, float Run, Vector3 At, float GateSq)>();
                runtime.ScreenFlash = (from, to, seconds, at, gateSq) =>
                    fired.Add((clock, from, to, seconds, at, gateSq));

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
                // One step of headroom per gap: an authored run time is an exact multiple of the step here, but
                // neither it nor the accumulated clock is exact in binary float and the misses do not cancel.
                // Measured: three of the five gaps land one step late, 0.05 s over the chain.
                ctx.Check(Mathf.Abs((fired[5].T - fired[0].T) - 1.1f) <= 5f * dt,
                    $"the chain spans its authored 1.1 s first-to-last fire ({fired[5].T - fired[0].T:0.###} s)");

                // The routing half at the source: every step carries the burst point and the def's OWN gate, which
                // is what lets the overlay pick panes. 10000 is metres squared, the compiled PLAYER_RANGE
                // convention, and all 24 shipped wash defs author exactly that one gate.
                ctx.Check(fired.All(f => Mathf.IsEqualApprox(f.GateSq, 10000f)),
                    $"every step reports the def's authored PlayerRange gate ({fired[0].GateSq:0.#} m², want 10000 = 100 m)");
                float drift = fired.Max(f => f.At.DistanceTo(point));
                ctx.Note($"the wash routes from {fired[0].At} on the def's own {fired[0].GateSq:0.#} m² gate ({Mathf.Sqrt(fired[0].GateSq):0.#} m), {drift:0.###} m off the play point");
                ctx.Check(drift <= 1f,
                    $"every step reports the burst's own world point ({drift:0.###} m from where it was played)");
            });
        });
        WashPaintsOnlyThePanesItReached(ctx);
        BlendWashRoutesToTheVictimsPane(ctx);
        PlayerRangeNearestHuman(ctx);
    }

    // The victim-routed blend channel (D13): a wash addressed to player 2 paints pane 2 and leaves
    // pane 1 untouched, whatever the cameras are doing; it composites OVER a proximity ramp already
    // running in the pane and leaves that ramp's own picture unchanged where no wash is running.
    // METHOD-12: the ramp readouts are the invariant, the blended pane is what moves.
    private static void BlendWashRoutesToTheVictimsPane(TestContext ctx)
    {
        const float gate = 10000f;
        var clear = new Color(0f, 0f, 0f, 0f);
        var white = new Color(1f, 1f, 1f, 0.3f);
        var violet = new Color(0.2f, 0f, 1f, 0.2f);
        var red = new Color(1f, 0f, 0f);

        // Both cameras at one point: the ramp's proximity gate cannot tell the panes apart, so any
        // difference between them below is the blend channel's routing alone.
        var p1 = ViewerCamera(ctx, Vector3.Zero);
        var p2 = ViewerCamera(ctx, Vector3.Zero);
        var viewers = new ViewerSet();
        viewers.Bind(new[] { p1, p2 });
        var (flash, panes) = PaneFlash(ctx, viewers);
        try
        {
            const float dt = 1f / 60f;
            // A wash addressed to player 2 (pane index 1), stepped through its 0.15 × 4 s attack.
            flash.PlayBlend(1, red, 1f, 4f);
            for (int i = 0; i < 40; i++)
                flash.Advance(dt);
            var pane2 = flash.CurrentFor(1);
            ctx.Check(pane2.A > 0.99f && pane2.R > 0.99f && pane2.G < 0.01f,
                $"a blend wash addressed to player 2 paints pane 2 red at its full weight after the attack ({pane2})");
            ctx.Check(flash.CurrentFor(0).IsEqualApprox(clear),
                $"and pane 1, whose camera stands at the same point, stays clear — routed by victim, not by proximity ({flash.CurrentFor(0)})");
            ctx.Check(!flash.RunningFor(1),
                $"and starts no RAMP in pane 2: the two channels are separate states ({flash.RunningFor(1)})");

            // A proximity ramp reaching both panes: pane 1 shows the ramp alone, pane 2 the wash
            // over the ramp — the pixel the ramp would have painted, with red laid over it.
            flash.Play(white, violet, 0.2f, Vector3.Zero, gate);
            ctx.Check(flash.CurrentFor(0).IsEqualApprox(white),
                $"an HE ramp reaching both panes paints pane 1 exactly as before the blend channel existed ({flash.CurrentFor(0)})");
            var composite = flash.CurrentFor(1);
            var want = BlendWash.Composite(white, red, flash.BlendFor(1)!.Weight);
            ctx.Check(composite.IsEqualApprox(want),
                $"and pane 2 shows the wash composited over that ramp ({composite}, want {want})");
            ctx.Check(flash.RunningFor(0) && flash.RunningFor(1),
                $"while the ramp itself runs in both panes, its routing untouched by the wash ({flash.RunningFor(0)}/{flash.RunningFor(1)})");

            // The wash ends at its duration and pane 2 falls back to whatever the ramp channel has,
            // which by then is nothing.
            for (int i = 0; i < 260; i++)
                flash.Advance(dt);
            ctx.Check(flash.CurrentFor(1).IsEqualApprox(clear) && flash.BlendFor(1)!.Running == false,
                $"the wash is gone at its 4 s duration and pane 2 reads clear again ({flash.CurrentFor(1)})");

            // A victim with no pane (an AI's player index) addresses nothing and throws nothing.
            flash.PlayBlend(AiAircraftSpawner.ShooterIdBase, red, 1f, 4f);
            flash.Advance(dt);
            ctx.Check(flash.CurrentFor(0).IsEqualApprox(clear) && flash.CurrentFor(1).IsEqualApprox(clear),
                $"a wash addressed to an AI's player index paints no pane ({flash.CurrentFor(0)} / {flash.CurrentFor(1)})");
        }
        finally
        {
            flash.Free();
            foreach (var pane in panes)
                pane.Free();
            p2.Free();
            p1.Free();
        }
    }

    // The wash reaches the panes the burst reached and no others: two panes 120 m apart under the
    // authored 100 m gate, so one pane, the other pane, and both are each reachable by moving the burst.
    // ⚠ The wash paints every player inside the burst's own authored radius, not just a hit or nearest
    // one. That is the original's rule read literally, asked once per player here, and the two
    // ground-effect defs carrying it play on terrain impacts with no hit aircraft to route to at all.
    // Ramp state is per pane; within a pane it still replaces (docs/org/sequences.md).
    private static void WashPaintsOnlyThePanesItReached(TestContext ctx)
    {
        // The gate every shipped wash def authors: metres SQUARED in the compiled convention.
        const float gate = 10000f;
        var clear = new Color(0f, 0f, 0f, 0f);
        var white = new Color(1f, 1f, 1f, 0.3f);
        var violet = new Color(0.2f, 0f, 1f, 0.2f);
        var green = new Color(0f, 1f, 0f, 0.5f);

        var p1 = ViewerCamera(ctx, Vector3.Zero);
        var p2 = ViewerCamera(ctx, new Vector3(0f, 0f, 120f));
        var viewers = new ViewerSet();
        viewers.Bind(new[] { p1, p2 });
        var (flash, panes) = PaneFlash(ctx, viewers);
        var (blind, blindPanes) = PaneFlash(ctx, null);
        try
        {
            ctx.Check(flash.PaneCount == 2, $"the overlay built one ramp per pane ({flash.PaneCount})");

            // 50 m ahead of P1, 170 m from P2: inside the gate for one of them only.
            flash.Play(white, violet, 0.2f, new Vector3(0f, 0f, -50f), gate);
            ctx.Check(flash.RunningFor(0) && flash.CurrentFor(0).IsEqualApprox(white),
                $"a burst 50 m from P1 washes P1's pane ({flash.CurrentFor(0)})");
            ctx.Check(!flash.RunningFor(1) && flash.CurrentFor(1).IsEqualApprox(clear),
                $"and leaves P2's pane, 170 m away, clear — the BL-340 report ({flash.CurrentFor(1)})");

            // 50 m past P2, 170 m from P1 — the same case from the other side, while P1's own ramp
            // is still running: two panes, two independent states.
            flash.Play(violet, white, 0.2f, new Vector3(0f, 0f, 170f), gate);
            ctx.Check(flash.RunningFor(1) && flash.CurrentFor(1).IsEqualApprox(violet),
                $"a second burst 50 m from P2 washes P2's pane ({flash.CurrentFor(1)})");
            ctx.Check(flash.CurrentFor(0).IsEqualApprox(white),
                $"without touching the ramp P1 is already watching ({flash.CurrentFor(0)}) — the state is per pane");

            // Between them: 60 m from each, so BOTH are inside the burst's own radius.
            flash.Play(green, white, 0.2f, new Vector3(0f, 0f, 60f), gate);
            ctx.Check(flash.CurrentFor(0).IsEqualApprox(green) && flash.CurrentFor(1).IsEqualApprox(green),
                $"a burst 60 m from both washes both panes — every player inside the radius, not just the nearest ({flash.CurrentFor(0)} / {flash.CurrentFor(1)})");
            ctx.Check(flash.RunningFor(0) && flash.RunningFor(1),
                $"and replaces what each pane was running rather than compositing with it ({flash.RunningFor(0)}/{flash.RunningFor(1)})");

            // An ungated def (the intro cutscene's gi_scene1 authors no PlayerRange) is not a
            // proximity effect at all, so it still paints everything.
            flash.Play(white, violet, 0.2f, new Vector3(0f, 0f, -5000f), 0f);
            ctx.Check(flash.CurrentFor(0).IsEqualApprox(white) && flash.CurrentFor(1).IsEqualApprox(white),
                $"an UNGATED wash 5 km out still paints every pane ({flash.CurrentFor(0)} / {flash.CurrentFor(1)})");

            // The floor: the def's gate already fired, so something was near it. If no pane's own
            // camera agrees, the nearest pane still gets it rather than the burst washing nobody.
            flash.Play(violet, green, 0.2f, new Vector3(0f, 0f, -5000f), gate);
            ctx.Check(flash.CurrentFor(0).IsEqualApprox(violet) && flash.CurrentFor(1).IsEqualApprox(white),
                $"a gated wash no pane is in range of falls to the nearest pane alone ({flash.CurrentFor(0)} / {flash.CurrentFor(1)})");

            blind.Play(white, violet, 0.2f, new Vector3(0f, 0f, -50f), gate);
            ctx.Check(blind.CurrentFor(0).IsEqualApprox(white) && blind.CurrentFor(1).IsEqualApprox(white),
                $"ABLE-TO-FAIL CONTROL: the same burst with no viewer set bound paints both panes, which is what this did before the routing existed ({blind.CurrentFor(1)})");
        }
        finally
        {
            blind.Free();
            foreach (var pane in blindPanes)
                pane.Free();
            flash.Free();
            foreach (var pane in panes)
                pane.Free();
            p2.Free();
            p1.Free();
        }
    }

    // The wash's own gate — `If PlayerRange 10000` — answers to the NEAREST human, not
    // one camera: a burst still fires while the camera this stage was built
    // against sits 5 km off, as long as SOME entry in `PlayerPositions` is inside the 100 m
    // gate. This is upstream of B12's routing (which panes a fired wash reaches) — here nothing
    // has fired yet, so no pane would have anything to route.
    private static void PlayerRangeNearestHuman(TestContext ctx)
    {
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            WithEffectStage(ctx, world, "he_ground_effect", new[] { "he_ring", "he_ring1", "he_trails" },
                (stage, runtime, point) =>
            {
                const float dt = 1f / 60f;
                var fired = new List<float>();
                runtime.ScreenFlash = (from, to, seconds, at, gateSq) => fired.Add(seconds);

                // Every known human 5 km out: nowhere near the def's own 100 m gate. 150 steps
                // (2.5 s) is the same margin FbfxFlash drives the full chain for above — long
                // enough that a gate wrongly left open would have fired well within it.
                runtime.PlayerPositions = () => new[] { point + new Vector3(0f, 0f, -5000f) };
                runtime.PlayEffectAt("he_ground_effect", point);
                for (int i = 0; i < 150; i++)
                    runtime.Advance(dt);
                ctx.Check(fired.Count == 0,
                    $"the burst's own PLAYER_RANGE gate stays closed while every PlayerPositions entry is 5 km off ({fired.Count} fired)");

                // A second human standing at the burst: the NEAREST of the two is now in range,
                // and the def's gate is asked against that one, not the far singleton.
                runtime.PlayerPositions = () => new[] { point + new Vector3(0f, 0f, -5000f), point };
                runtime.PlayEffectAt("he_ground_effect", point);
                for (int i = 0; i < 150; i++)
                    runtime.Advance(dt);
                ctx.Check(fired.Count == 6,
                    $"the same def fires its six-step wash once the NEAREST PlayerPositions entry stands at the burst ({fired.Count})");
            });
        });
    }

    // A two-pane ScreenFlash over bare HUD parents — the shape
    // `GameSession` builds from the rigs, with nothing but the parents and the viewer set,
    // since that is all the routing reads.
    private static (ScreenFlash Flash, Node[] Panes) PaneFlash(TestContext ctx, ViewerSet? viewers)
    {
        var panes = new[] { new Node { Name = "pane1_hud" }, new Node { Name = "pane2_hud" } };
        foreach (var pane in panes)
            ctx.Host.AddChild(pane);
        var flash = ScreenFlash.Build(panes, viewers);
        ctx.Host.AddChild(flash);
        return (flash, panes);
    }

    // A miniature world-effects stage: the named template roots built from the chapter's
    // real gamez into one pool slot, each hidden, under an effects-role runtime bound to the
    // subset of the program the effect needs. Real geometry on purpose — this suite is about mesh
    // VISIBILITY, which named empty nodes cannot express — and the roles are the production ones
    // (`TemplateStage.Shown` + `Pooled`, the pair `WorldEffectsFactory` seals into
    // the stage it builds), since the reveal exists only under them.
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

    // Plays he_ground_effect, flash_effect and sonic_ground_effect end to end on a fixed-dt clock and
    // matches each one's FULL event timeline against the authored JSON. Nothing here is a membership
    // check, which would pass a broken scheduler. Inventory: this module's docs/architecture.md entry
    // and docs/org/sequences.md.
    // ⚠ Derive the staged roots (EffectCatalogue.StageRootsFor), never hand-list them; a def whose
    // anchor root was not staged plays nothing, silently. ⚠ Do not inherit --effects-test's 0.3 s TTL.
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

    // `he_ring-he_ground_effect.json` — five sequences, two of them unnamed and Initial,
    // three ON_CALL — plus `flame_ball_01-large_fireball.json`, which its fourth CALL_ANIMATION
    // reaches and which carries the parked-stopper case.
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
        // The second unnamed Initial sequence. The IF and the ENDIF are control flow the runner interprets
        // itself and never dispatches, so #1 is the only row this lane can produce, and it produces it only
        // because the burst is played at the camera, which is what makes the range condition true.
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
        // The wash: six FBFX_COLOR_FROM_TO steps. A handler reporting 0 as its duration fires all six in
        // one instant, which is what the times here refuse. fbfx-flash asserts the colours and run times;
        // this asserts their place in the burst.
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

    // `flash_control-flash_effect.json` — the pure light case, two sequences. The one
    // timed event in it is a `START_TIME ANIMATION 1.5`, read against the instance clock.
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

    // `sonic_effect-sonic_ground_effect.json` — four sequences, two unnamed and Initial.
    // The repeat-call case: the first Initial sequence calls `sonic_light_seq` at #0 and again at #4,
    // 1.2 s later.
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
        // Two passes of ONE sequence, 1.2 s apart. The first must have ended for the second call to find it
        // parked and restart it: a call into a running sequence is a no-op, and a second concurrent copy is
        // not a thing the original can express.
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

    // Plays one burst on its own miniature world-effects stage and hands the recorded dispatch log to
    // body. The stage's template ROOTS are derived from the definition's own CALL_ANIMATION closure
    // against the chapter gamez, the same derivation the production bind runs, so a definition whose
    // anchor resolves nowhere throws here, naming it, instead of quietly playing nothing. Everything
    // else is the production world-effects role, with the camera as the player position.
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

    // Matches a definition's recorded dispatches against its authored lanes and asserts both halves of
    // "the timeline is right": ORDER, each lane's rows arriving in the sequence's own order with
    // nothing unauthored arriving, and TIME, each row landing on its authored instant within
    // BurstSlack. A row is claimed by the first lane whose next unconsumed step it matches on sequence,
    // index, kind and name, so a row that arrives early or twice is reported stray. The four-part key
    // is needed because a definition's sequence NAMES are not unique.
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

    // The crash rig's damage-stage templates are pooled, and a relocating CALL_ANIMATION from a NEW
    // anchor takes its own copy instead of teleporting the one a previous anchor's burst is still
    // flying on. Reproduces the shipped shape: two authored pdpanelN defs each calling gimmeflakes at
    // their own pdpN, on a runtime carrying the crash rig's role flags plus the pool. Without the pool
    // the second call restarts the single shared planeflakes root mid-flight, which is the "panels fly
    // away repeatedly, and from the wrong site" symptom.
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

    // A flat named call-site node for DamageTemplatePool — name meta set
    // the way the crash rig's own anchor scaffold sets it, so resolution finds it.
    private static Node3D PoolAnchorNode(string name, Vector3 at)
    {
        var node = new Node3D { Name = name, Position = at };
        node.SetMeta(AnimRuntime.NameMeta, name);
        return node;
    }

    private static bool AtPoolSite(Node3D? copy, Node3D site) =>
        copy != null
        && copy.GlobalTransform.Origin.DistanceTo(site.GlobalTransform.Origin) < 0.5f;

    // ---- the injure staging is keyed on health, not the combined progression -------------------

    // The decoded per-part panel threshold: the original divides the part's health by its health max,
    // and blocks health damage outright while that part's armour covers the hit, so an armoured zone
    // crosses no per-part threshold at all. Driven on a real plane model with the shipped injure_anims:
    // strip the zone's armour and no panel may flip, then drive health under the threshold and the
    // panel must appear. The first half is what fails when the staging is fed PartState.Fraction.
    private static void DamageStagingPool(TestContext ctx)
    {
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);

        // Data-driven, not a hardcoded zone: any part whose authored list flips a pdpanelN, taking
        // its HIGHEST threshold so one health step crosses exactly one panel.
        DestroyablePart? part = null;
        float threshold = 0f;
        string panelAnim = "";
        foreach (var p in stats.DestroyableParts)
            foreach (var (frac, anim) in p.InjureAnims)
                if (anim.StartsWith("pdpanel", System.StringComparison.OrdinalIgnoreCase)
                    && frac > threshold)
                {
                    part = p;
                    threshold = frac;
                    panelAnim = anim;
                }

        ctx.Check(part != null, $"{ctx.PlaneName} authors a pdpanelN entry on some zone");
        if (part == null)
            return;
        ctx.Check(part.MaxArmor > 0f && part.MaxHp > 0f,
            $"precondition: {part.Name} carries both pools ({part.MaxArmor:0} armour, {part.MaxHp:0} hp)");
        if (part.MaxArmor <= 0f || part.MaxHp <= 0f)
            return;

        // The combined fraction with armour gone is MaxHp/(MaxHp+MaxArmor); the half this suite
        // pins only means anything when that already sits at or under the panel's threshold.
        float strippedCombined = part.MaxHp / (part.MaxHp + part.MaxArmor);
        ctx.Check(strippedCombined <= threshold,
            $"precondition: armour gone puts the COMBINED fraction at {strippedCombined:0.00}, already past {panelAnim}'s {threshold:0.00} — the early tear this pins");
        if (strippedCombined > threshold)
            return;

        var textures = new TextureArchive(texturesPath);
        // damagePanels: the pdpN nodes are skipped in a plain static build (PlaneBuilder 10c).
        var builder = new PlaneBuilder(planesGamez, textures, damagePanels: true);
        var model = builder.Build(ctx.PlaneName);
        ctx.Host.AddChild(model);
        try
        {
            var visuals = new DamageVisuals(builder.DamagePanels, model, stats);
            var torn = builder.DamagePanels
                .Where(p => p.Name.ToString().StartsWith("pdp", System.StringComparison.OrdinalIgnoreCase)
                            && !p.Name.ToString().EndsWith("_h", System.StringComparison.OrdinalIgnoreCase))
                .ToList();
            ctx.Check(torn.Count > 0, $"{ctx.PlaneName} carries {torn.Count} torn-panel nodes");
            ctx.Check(torn.All(p => !p.Visible), $"…and every one of them starts hidden");

            var damage = new PlaneDamage(stats.DestroyableParts);
            var state = damage.Apply(part.Name, 0f, part.MaxArmor)!;
            ctx.Check(state.Armor <= 0f && Mathf.IsEqualApprox(state.HealthFraction, 1f),
                $"{part.Name}: armour stripped to {state.Armor:0.#}, health untouched at {state.HealthFraction * 100f:0}% (combined {state.Fraction:0.00})");

            visuals.OnPartDamage(part.Name, state.HealthFraction);
            visuals.OnHullDamage(damage.SummaryHealthFraction);
            ctx.Check(torn.All(p => !p.Visible),
                $"no panel tore on the armour spend, though the combined fraction ({state.Fraction:0.00}) is past {panelAnim}'s {threshold:0.00}");

            // Now health itself crosses: drive it just under the threshold.
            float target = (threshold - 0.02f) * part.MaxHp;
            state = damage.Apply(part.Name, part.MaxHp - target, 0f)!;
            ctx.Check(state.HealthFraction <= threshold,
                $"{part.Name} health driven to {state.HealthFraction:0.00}, under {threshold:0.00}");

            visuals.OnPartDamage(part.Name, state.HealthFraction);
            ctx.Check(torn.Any(p => p.Visible),
                $"…and {panelAnim} flipped its torn panel once HEALTH crossed");
        }
        finally
        {
            model.Free();
        }
    }

    // ---- binding the crash rig must leave the airframe under the controller --------------------

    // Builds the crash rig the way WorldEffectsFactory.BuildFlightCrashRuntime does, binds the
    // crash-rig subset, and asserts the two things that go wrong in flight. ⚠ The airframe model stays
    // a plain child of the controller, not world-pinned: on the Devastator the reset defs' authored
    // name is the model root itself, and the reset chain must not relocate the aircraft the way it
    // places effect templates. ⚠ Every pooled template copy of one root must show the same lit mesh
    // count as its slot-0 sibling; a copy the reset pass missed stays lit for the whole session.
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

    // Every wreck node's rest pose — the local mirror of
    // `WorldEffectsFactory.CollectRestPoses`, so the suite's respawn ritual can re-home the
    // flung pieces the way `FlightController.Respawn` does.
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

    // Meshes drawing under one staged template copy — visibility taken in-tree, so a
    // parent the reset pass switched off darkens the whole copy the way it does on screen.
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

    // ---- an AI plane's crash picks from the ai_crash_* vector ----------------------------

    // The AI arm of the crash-family split, through the REAL factory call, which keys the family on
    // IsHumanPiloted: an AI controller's rig binds the ai_crash_* vector, a crash on a body stamped
    // dirt selects ai_crash_dirt, and a crash with no struck body takes the null-material arm to slot
    // 0, ai_crash_default, never a player_crash_* def. ⚠ Keep the human-piloted A/B control; without
    // it a family mix-up in the pick would be invisible from the AI side alone.
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

    // The whole --effects-test sweep, asserted instead of read: every EffectCatalogue.EffectAnimNames
    // entry played through Probes.Effects on a full replica stage, so the census's sweep-wide verdicts
    // fail a build instead of sitting in .scratch text nobody reads. The play point is a fixed spot
    // ~180 m from the stage origin, so the distance column discriminates a template that failed to
    // relocate. ⚠ The puffer and mesh tallies are golden counts under THIS suite's conditions, literal
    // seed 1 and the counting factory, and are pinned separately from the probe's.
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

    // A gravity-bearing OBJECT_MOTION must be cut short by real geometry instead of running its
    // authored RUN_TIME out below the terrain, through whichever of the two tiers its flags select, and
    // must pick its BOUNCE_SEQUENCE branch from the surface it struck. Driven as a synthetic body
    // thrown downward from a known height, so flight time, resting height and branch are predictable.
    // ⚠ Keep the last case, the same bodies with no mask handed over, running their full clock far
    // below the surface: without it a suite that fired no query at all would pass its landing checks.
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

            // flagged: false is the DEFAULT authoring and selects the ground column; noAltitude is the opt-out
            // that selects neither; timed: false omits run_time the way the ballistic events do, and is thrown
            // upward so the parabola has an apex to report to the sequence.
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

                    // A plain launch on a node that did NOT just land takes the same tier: the resume mark buys
                    // momentum and re-homing rules, never a different contact mechanism. A body that reported Sweep
                    // here would mean the retired inheritance had grown back somewhere.
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

    // MAIN_ROOT_NODE / INPUT_NODE mean "the node this definition was invoked on", a sentinel and not a
    // name, so a resolver that only matches names finds nothing and drops the event without a word,
    // leaving the whole self-referencing population unlaunched. C5's agyrobus is the whole test: it is
    // the only carrier a player can reach, and having no placement of its own, a launch that re-homes
    // to rest teleports the wreck kilometres away, so the launch must take the node over from the live
    // playback. ⚠ Stay branch-agnostic; randomdestseq opens with IF RandomWeight and either may draw.
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
    private static void BounceLaunch(TestContext ctx)
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

    // The third launch shape: OBJECT_MOTION events that omit RUN_TIME and BOUNCE_SEQUENCE and follow
    // the launch with the flying piece's own null-start ACTIVE_STATE 0. A flight solve gated on a
    // bounce being named declines all of them, so the piece is hidden before it moves. The zeppelin
    // cannon's eight parts are the reachable repro (analysis/bl-257-nulled-launch/).
    // ⚠ Measure the GAP between each part's launch and its own deactivation, not a mesh count, which
    // reads 8/8 either way. ⚠ Assert a BAND; elevation and speed are per-instance draws, and linear.
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

    // The `part1`…`part8` the flying-parts def drives, by node name, from
    // anywhere under the staged template. Named lookup rather than a child index: the wreck is
    // real gamez geometry and its parts sit at whatever depth it authors them.
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

    // Hides a node through the lab's own Hide action, then re-shows it through a real RESET_STATE def,
    // the same path a world animation uses, and checks the tree row both times, never through the
    // button, only through Node3D.Visible. A def re-showing a node the user hid is correct behaviour,
    // so the row must follow it. ⚠ Use a chapter other than TestContext.Chapter: that one is cached
    // and shared with damage-hd, so the candidate search would otherwise depend on suite run order.
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

    // Asserts every pane of a 2-, 3- and 4-player rig is a 3D audio listener. The check reads trivial
    // and is not: a fresh SubViewport is NOT a listener, and in splitscreen the main camera stands down,
    // which takes it out of the World3D listener set. With no listener-enabled viewport left,
    // AudioStreamPlayer3D finds no listener in range, clears its bus volumes, and every 3D emitter in
    // the world is silent, with nothing logged or counted to say so.
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

    // The B13 rule: WorldLights.Commit fades and ranks
    // each light against the NEAREST of every pane's camera, not a single position. Driven
    // straight against a real WorldLights instance with synthetic positions —
    // there is no per-player placement flag to give two scripted panes independent spots (the
    // same CLI gap B11/B12 hit), so the rule is pinned here instead and the visual verdict is
    // PT-52's, alongside B11/B12's own owed at-the-controls check.
    private static void WorldLightsNearestViewer(TestContext ctx)
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

    // One authored event on an `ordnance-burst-timeline` lane: where it sits in its
    // sequence, what it is, and the instant the JSON says it fires — a cumulative sum of the
    // preceding events' `run_time`s and start offsets, read off the definition by hand.
    // Never computed from the runtime, which is the whole point of the assertion.
    private readonly record struct BurstStep(int Index, string Kind, string? Name, float At);

    // One recorded dispatch, off AnimRuntime.OnEventDispatched: the
    // playhead instant plus the identity the seam already carries. `Anim` is the DEFINITION's
    // animation name, so a burst's own timeline can be told apart from the timelines of the
    // definitions its CALL_ANIMATIONs reach.
    private readonly record struct BurstFire(float T, string Anim, string Sequence, int Index,
        string Kind, string? Name);

    // One authored sequence PASS. A pass, not a sequence: `sonic_ground_effect`
    // calls `sonic_light_seq` from two sites 1.2 s apart, and each call is its own lane,
    // which is how the timeline says the second call restarted a parked sequence rather than
    // being swallowed or running a second concurrent copy.
    private sealed record BurstLane(string Sequence, BurstStep[] Steps);

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
