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

using static CSVM.Testing.AiTargetingAndZeppelinSuites;
using static CSVM.Testing.AnimationAndEffectsSuites;
using static CSVM.Testing.CombatSuites;
using static CSVM.Testing.InstantActionSuites;
using static CSVM.Testing.OrdnanceSuites;
using static CSVM.Testing.PufferSuites;
using static CSVM.Testing.WorldAndToolSuites;
namespace CSVM.Testing;

public static class SuiteCatalog
{
    public static readonly IReadOnlyList<string> Names = new[]
    {
        "emitter-lifetime",
        "puffer-modes",
        "puffer-wind",
        "puffer-distance-fade",
        "puffer-priority-size",
        "puffer-fire-column",
        "loadout-bind",
        "weapons-fire",
        "aim-assist",
        "loadout-forrig",
        "warning-shot",
        "blast-neighbor-shape",
        "blast-curve-cover-cap",
        "launch-velocity-decay",
        "smoke-screen",
        "disabling-hits",
        "impact-orientation",
        "ordnance-launch-axis",
        "motor-acceleration",
        "ordnance-end-conditions",
        "shootable-flyout",
        "ordnance-guidance",
        "air-to-air",
        "ai-plane-defs",
        "team-model",
        "instant-action",
        "instant-action-zeppelin",
        "instant-action-end",
        "instant-action-wrapup",
        "inert-aircraft",
        "world-turrets",
        "carried-turrets",
        "graze-bounce",
        "ai-spawn-jitter",
        "ai-actor",
        "ai-gunnery",
        "ai-modes",
        "voice-runtime",
        "ai-voice",
        "ai-net-follow",
        "zeppelin-motion",
        "zeppelin-launch",
        "zeppelin-damage",
        "zeppelin-broadside",
        "damage-stages",
        "damage-hd",
        "stop-sequence",
        "death-slot",
        "wait-for-completion",
        "emitter-host-deactivation",
        "effect-template-mesh",
        "fbfx-flash",
        "ordnance-burst-timeline",
        "effect-pool-reset",
        "effects-census",
        "bounce-launch",
        "ground-contact",
        "forward-rotation",
        "self-ref-launch",
        "nulled-launch",
        "destructible-census",
        "clutter-determinism",
        "lens-flare-gates",
        "sun-orientation",
        "tex-dropin",
        "gltf-export",
        "collision-visibility",
        "nodelab-visibility",
        "trail-world-anchor",
        "damage-template-pool",
        "damage-staging-pool",
        "crash-rig-anchors",
        "ai-crash-defs",
        "hostile-marker-hud",
        "target-ref",
        "target-pool",
        "target-selection",
        "target-input",
        "target-flag",
        "splitscreen-listeners",
        "world-lights-nearest-viewer",
    };

    internal static void RegisterAll(List<TestHarness.Suite> into)
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
        into.Add(new TestHarness.Suite("blast-curve-cover-cap",
            "splash damage follows 1 - d^2/R^2 at 0.25R, 0.5R and 0.75R (C10), a destructible " +
            "behind a wall takes nothing while the same layout without the wall takes the curve's " +
            "value (C11), a burst over 40 targets damages exactly the nearest 32 (C11), and a " +
            "chapter-style body (server-side shapes, no shape owners, origin at ground level) " +
            "takes the curve's value with no engine error",
            BlastCurveCoverCap));
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
        into.Add(new TestHarness.Suite("disabling-hits",
            "the hit-side dispatch of the no-damage types on a live pool (D15-D17): a wep_08 into a " +
            "human's tail washes that pane red at weight 1 for 5 s after a 1 s delay and no other " +
            "pane, a wep_09 from behind does nothing while one from ahead washes white, two humans " +
            "hit in one second each carry their own wash and an AI 20 m from one burst is stunned; " +
            "an AI is stunned for DisablingIntensity's seconds by a burst on the ground inside " +
            "IMPACT_PROXIMITY, raised to 5 s by a direct hit and overwritten back down by the next " +
            "ground burst; a wep_12 leaves a 2 s cloud that chokes the struck AI for the formula's " +
            "seconds at its origin distance and a human 20 m out for the 5 s floor, refreshes both " +
            "while it lives and lets the timer run once gone; and no ledger moves",
            DisablingHits));
        into.Add(new TestHarness.Suite("impact-orientation",
            "the IMPACT row's SURFACE_ANIMATION is placed with world up rotated onto the struck " +
            "normal while the plain ANIMATION keeps its fixed axis (C12): a wep_06 into a 30° slope " +
            "hands he_ground_effect a basis whose Y is the slope normal, the same round into flat " +
            "ground hands identity, and a wep_12's scatter_effect on the slope stays identity",
            ImpactOrientation));
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
            "instead, proving the two paths are separate; and wep_14 is unhittable for its first " +
            "300 m and hittable from there on",
            OrdnanceEndConditions));
        into.Add(new TestHarness.Suite("shootable-flyout",
            "the torpedo's two shootable halves (E19/E20): a live wep_14 is the player's one Enemy " +
            "entry, named for --target= and labelled 'Aerial torpedo' with a full health bar, " +
            "sorting ahead of every sector as incoming ordnance, while a fused wep_06 reaches the " +
            "same fourth list with the admission byte clear and contributes nothing; the entry " +
            "vanishes when the round ends; and the 10-point pair spends armour-then-health, holds " +
            "the −1.0 sentinel on a round without FLYOUT_HEALTH, and on zero plays " +
            "torpedo_destroy_effect with no detonation at all; the body is drawn from launch in " +
            "torpedo_trail's folded look and unfolds its wings at the def's 3.5 s",
            ShootableFlyout));
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
            "representative AI rating, FlightRoster.SpawnAi given an authored team/livery " +
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
            "and no more, a still-parked member counts as present so the " +
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
        into.Add(new TestHarness.Suite("effect-pool-reset",
            "a pooled effect copy is re-reset on checkout: the sonic burst played five times over a four-slot pool draws its rings on the fifth play exactly as on the first (BL-406)", EffectPoolReset));
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
            + "AimCandidateSet.Structures is, an ordnance entry with the admission byte clear is "
            + "refused (the TARGETABLE half is the shootable-flyout suite's), and a zeppelin "
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

    // Kills a refuel tank through a CountingEmitterFactory; only instance retirement ends its fire_n_smoke.
    // ⚠ Assert factory reach, emitter start, and retained-but-stopped state; "stopped" alone can mean teardown.
}
