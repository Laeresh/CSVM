using System;
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

using static CSVM.Testing.AiSuites;
using static CSVM.Testing.AlphaCutoutRaySuites;
using static CSVM.Testing.AnimationAndEffectsSuites;
using static CSVM.Testing.CampaignBriefingNoteSuites;
using static CSVM.Testing.CampaignBriefingRepaintSuites;
using static CSVM.Testing.CampaignHudSuites;
using static CSVM.Testing.CampaignLoopSuites;
using static CSVM.Testing.CampaignMarkerSuites;
using static CSVM.Testing.CampaignRacerSuites;
using static CSVM.Testing.CampaignRosterSuites;
using static CSVM.Testing.CampaignSuites;
using static CSVM.Testing.CampaignZeppelinSuites;
using static CSVM.Testing.CampaignZeppelinWakeSuites;
using static CSVM.Testing.CombatSuites;
using static CSVM.Testing.DamageSuites;
using static CSVM.Testing.DestroyChoreographySuites;
using static CSVM.Testing.DropoffChutemanSuites;
using static CSVM.Testing.DropoffPlacementSuites;
using static CSVM.Testing.GeneratorRosterSuites;
using static CSVM.Testing.InstantActionSuites;
using static CSVM.Testing.IntroAircraftSuites;
using static CSVM.Testing.LandingApproachSuites;
using static CSVM.Testing.MenuCaptureSuites;
using static CSVM.Testing.MusicSuites;
using static CSVM.Testing.OrdnanceSuites;
using static CSVM.Testing.PufferSuites;
using static CSVM.Testing.TargetingCandidateSuites;
using static CSVM.Testing.TargetingSuites;
using static CSVM.Testing.WingmanSuites;
using static CSVM.Testing.WorldAndToolSuites;
using static CSVM.Testing.WorldFidelitySuites;
using static CSVM.Testing.ZeppelinCannonBurnoutSuites;
using static CSVM.Testing.ZeppelinHullActivationSuites;
using static CSVM.Testing.ZeppelinIdentitySuites;
using static CSVM.Testing.ZeppelinSuites;
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
        "ordnance-impact-effects",
        "ordnance-launch-axis",
        "motor-acceleration",
        "ordnance-end-conditions",
        "shootable-flyout",
        "ordnance-guidance",
        "air-to-air",
        "ai-plane-defs",
        "engine-note",
        "team-model",
        "instant-action",
        "instant-action-zeppelin",
        "instant-action-end",
        "instant-action-wrapup",
        "results-board-shell",
        "flight-roster-transaction",
        "inert-aircraft",
        "world-turrets",
        "turret-self-fire",
        "c1-aa-guns",
        "mission-off-turrets",
        "carried-turrets",
        "graze-bounce",
        "ai-spawn-jitter",
        "ai-actor",
        "ai-far-field-plant",
        "ai-gunnery",
        "ai-modes",
        "voice-runtime",
        "ai-voice",
        "ai-net-follow",
        "zeppelin-motion",
        "zeppelin-pandora-dead-end",
        "zeppelin-scripted-pose",
        "zeppelin-launch",
        "zeppelin-damage",
        "zeppelin-broadside",
        "zeppelin-cannon-burnout",
        "zeppelin-hull-activation",
        "damage-stages",
        "damage-hd",
        "stop-sequence",
        "first-person-condition",
        "death-slot",
        "start-state-swap-pool",
        "carried-state-silent",
        "wait-for-completion",
        "emitter-host-deactivation",
        "effect-template-mesh",
        "fbfx-flash",
        "callback-events",
        "ordnance-burst-timeline",
        "effect-pool-reset",
        "effect-pool-spawn-pose",
        "repeat-call-slots",
        "effects-census",
        "bounce-launch",
        "ground-contact",
        "forward-rotation",
        "launch-direction-cache",
        "barracuda-drive",
        "anim-activation-prerequisite",
        "self-ref-launch",
        "nulled-launch",
        "destructible-census",
        "clutter-determinism",
        "lens-flare-gates",
        "sun-orientation",
        "tex-dropin",
        "gltf-export",
        "plane-shader-reuse",
        "cockpit-interior",
        "cockpit-overlay-pass",
        "collision-visibility",
        "nodelab-visibility",
        "trail-world-anchor",
        "turret-death-effect-world-anchor",
        "turret-death-fire-follows-hull",
        "ring-death-effects-follow-hull",
        "damage-template-pool",
        "damage-staging-pool",
        "cockpit-panel-staging",
        "damage-stage-slots",
        "ai-damage-stages",
        "crash-rig-anchors",
        "nitro-boost-anchors",
        "emitter-prewarm",
        "ai-crash-defs",
        "ai-wreck-fall",
        "player-destroy-choreography",
        "hostile-marker-hud",
        "target-ref",
        "target-pool",
        "target-selection",
        "target-input",
        "target-flag",
        "splitscreen-listeners",
        "world-lights-nearest-viewer",
        "campaign-persistence",
        "music-states",
        "campaign-objectives",
        "campaign-mission-end",
        "targeting-candidates",
        "debug-kill-target",
        "ranked-pool-carried-turret-dedup",
        "partition-areas",
        "scripted-path",
        "generator-takeoff-run",
        "generator-launch-climb-out",
        "wingman-station",
        "wingman-engage",
        "campaign-objectives-hud",
        "campaign-cutscene",
        "cutscene-letterbox",
        "intro-aircraft-stage",
        "intro-wingmen",
        "dropoff-chuteman-stage",
        "dropoff-placement",
        "cutscene-handoff-unposed",
        "hangar-door-wake",
        "anim-clock-realtime",
        "fog-state",
        "campaign-submarine",
        "campaign-roster",
        "generator-roster-params",
        "campaign-bomber-formation",
        "campaign-kill-credit",
        "campaign-loop",
        "campaign-zeppelins",
        "campaign-danger-zones",
        "campaign-racers",
        "campaign-objective-markers",
        "campaign-objective-target-path",
        "campaign-objective-labels",
        "campaign-race-chain",
        "campaign-balloon-marker",
        "campaign-balloon-death",
        "landings-approach-trigger",
        "landings-wingwalk-gate",
        "landings-train-pickup-gate",
        "landings-train-pickup-ride",
        "landings-trailer-pickup-gate",
        "landings-car-pickup-credit",
        "landings-auto-land-button",
        "landings-hookup-airframe",
        "landings-hangar-drop-gate",
        "landings-docking-hold",
        "campaign-airframe-swap",
        "campaign-hangar-handover",
        "campaign-wingwalk-camera",
        "campaign-cutscene-skip",
        "roster-spawn-names",
        "roster-voice-ratings",
        "mission-radio",
        "campaign-zeppelin-wakeup",
        "campaign-squad-wakeup",
        "campaign-blacke-search",
        "campaign-set-ai-net",
        "campaign-surface-vehicles",
        "campaign-player-death",
        "alpha-cutout-ray-census",
        "zeppelin-identity",
        "campaign-briefing-repaint",
        "menu-screenshot-key",
        "perf-hud-layout",
        "campaign-briefing-note",
        "campaign-capture-group",
        "campaign-capture-chutes",
        "landings-balmoral-dock",
        "campaign-cutscene-ownership",
        "airframe-hull-coverage",
        "campaign-coop-human-field",
        "campaign-coop-episode-owner",
        "campaign-coop-approach-row",
        "campaign-coop-dropoff",
        "campaign-coop-death",
        "campaign-coop-attempt",
        "campaign-coop-cutscene-fullscreen",
    };

    // The suites `--run-tests=tier:quick` runs: one representative per failure surface, checked in
    // here rather than inferred from a diff, every name also in Names. The selection rule is in
    // docs/tooling.md; the tier is partial and the full catalog stays the landing gate.
    public static readonly IReadOnlyList<string> QuickTier = new[]
    {
        "puffer-modes",
        "loadout-bind",
        "weapons-fire",
        "air-to-air",
        "ai-actor",
        "instant-action",
        "damage-stages",
        "damage-hd",
        "effect-template-mesh",
        "collision-visibility",
        "target-selection",
        "campaign-objectives",
        "music-states",
    };

    /// <summary>The suite names a tier holds, or null when no tier carries that name (which a
    /// selector must treat as a miss, not as an empty selection).</summary>
    public static IReadOnlyList<string>? Tier(string name) =>
        name.Equals("quick", StringComparison.OrdinalIgnoreCase) ? QuickTier : null;

    internal static void RegisterAll(List<TestHarness.Suite> into)
    {
        // Registration order is presentation only: no suite here depends on running after another,
        // which is what lets any subset of this registry run in a process of its own.
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
        into.Add(new TestHarness.Suite("ordnance-impact-effects",
            "the beeper/seeker impacts play what the data authors (PT-67): a wep_10 bursting on its " +
            "own fused target indexes the aircraft's IMPACT row and plays large_fireball, the same " +
            "burst on a non-aircraft target plays the named-and-empty default row's nothing, and a " +
            "wep_11 into the ground hands its authored ballflare.flt to the effects runtime — the " +
            "white growing flare — instead of standing a static gamez-template instance in for it",
            OrdnanceImpactEffects));
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
        into.Add(new TestHarness.Suite("engine-note",
            "the engine slot's two non-throttle terms (BL-423): a sustained vertical dive drops the " +
            "note to 0.94 against the 0.9370 CAP-10 measured off the original, ±0.3 rad/s both RAISE " +
            "it by the +3.0 % that item measured because the term is a MAGNITUDE, a 4 rad/s roll " +
            "moves it not at all because the nose-axis component is dropped, a climb reads the " +
            "opposite sign to a dive off a real attitude, neither term is audible on the shipped " +
            "flat volume curve, and the authored 1.5 parameter clamp holds a tumble",
            EngineNote));
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
        into.Add(new TestHarness.Suite("results-board-shell",
            "the ResultsBoard shell contract, once for all four results boards: waking raises " +
            "Ended, the resting Photo Mode row changes nothing, the standard Restart leaves the " +
            "release to the live flag, the flag clearing retires the board and releases the " +
            "clock — and the wrap-up board's own menu-driven retire, which no flag ever performs",
            ResultsBoardShell));
        into.Add(new TestHarness.Suite("flight-roster-transaction",
            "FlightRoster owns human and AI assembly as atomic transactions: a late second-human " +
            "failure removes external bindings, a retry commits both humans in order with complete " +
            "bindings, and a late AI failure restores pilot/RNG state and consumes no identity",
            FlightRosterTransaction));
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
            "stays cover, skip same-team targets, join the aim-assist candidate list, and go permanently quiet " +
            "when the emplacement's own destructible dies", WorldTurrets));
        into.Add(new TestHarness.Suite("turret-self-fire",
            "C1's five aagun emplacements, each woken alone and fired at a plane parked low on " +
            "eight bearings so the line of fire crosses the fort's own structures: no gun ever " +
            "takes damage from its own rounds, whether by a muzzle-side strike on its own mount " +
            "or by its burst's splash, while a neighbour's burst still reaches it", TurretSelfFire));
        into.Add(new TestHarness.Suite("c1-aa-guns",
            "CM07's own flak on the mission it is flown in: C1/M02 places five aagun emplacements, "
            + "all standing and shipped dormant, the mission's OBJECTIVE1 WAKEUP_TURRETS 'aagun**' "
            + "arms exactly those five through the world lookup and the subtree write, each one "
            + "acquires a plane parked inside DETECTION_RANGE and fires without taking its own "
            + "rounds, and a chapter 1 persist log holding all five wrecked carries NOTHING into "
            + "CM07, since it is that chapter's first mission and the engine's backwards walk finds "
            + "no earlier carrier (the same log applied with a cut that reaches it kills them all)",
            C1AaGuns));
        into.Add(new TestHarness.Suite("mission-off-turrets",
            "an emplacement whose site the mission's .gw switched OFF is out of the world: C3/M03's " +
            "six balloon turrets read dead, tick to Dead once woken, and are listed dead in the gunner " +
            "scan with no structure candidate on their canopies, while C3/M02 leaves the same six " +
            "standing and alive", MissionOffTurrets));
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
        into.Add(new TestHarness.Suite("ai-far-field-plant",
            "the far-field plant's session plumbing on live rigs: an AI 1200 m from the human " +
            "flies the decoded speed-hold branch and one at 100 m keeps the aerodynamics, the " +
            "range is horizontal (3 km of altitude is not distance), the NEAREST of several " +
            "humans decides it, an unbound seam stays near-field, and the far rig holds " +
            "throttle x fd_speed + 5 m/s where the near rig on the same orders does not",
            AiFarFieldPlant));
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
            "net following (B5): a real chapter net resolves by id and by name, its node fields " +
            "ride along inert on an AIRCRAFT walk, and an AI plane with a net-following pilot captures node after " +
            "node with every hop an EDGE of the graph, never node order. Then the same net, " +
            "anchored (BL-377), rides its target 6 km east and the plane laps the MOVED ring at " +
            "its authored altitude, never seating on the edgeless anchor node", AiNetFollow));
        into.Add(new TestHarness.Suite("zeppelin-motion",
            "zeppelin motion (F17): C1/M04's piratezep record loads, its world node is placed at " +
            "the authored pose, which is PirateZep1's node 0 and an ARMED stop point, so it sits " +
            "docked until SetStopPoint releases stop-point id 1 — then flown between manual sim " +
            "steps (every hop an EDGE, displacement never over max_speed·dt) until the route's " +
            "far end, armed under the unaddressable id 0, docks it for good; total engine loss " +
            "decelerates it to a stop through the decoded sqrt curve, and a deactivated record " +
            "is placed but held", ZeppelinMotionSuite));
        into.Add(new TestHarness.Suite("zeppelin-pandora-dead-end",
            "the structural dead-end hold (BL-529): CM08's piratezep flies its own Klondike1 " +
            "chain, releasing the two stops the file arms (ids 7 and 8), pitch never past the " +
            "steepest leg's slope the whole route, until it reaches node 0 — an open end with NO " +
            "stop point authored at all — and holds there for good rather than re-picking node " +
            "1 and shuttling the altitude swing back and forth forever", ZeppelinPandoraDeadEndSuite));
        into.Add(new TestHarness.Suite("zeppelin-scripted-pose",
            "a zeppelin under an ObjectMotionSiScript: CM04's pzep_todrydock owns piratezep's " +
            "pose from its frame 0 over C3/M03's built world, the zeppelin runtime places " +
            "nothing over it and its follower parks (never stepped, no frame jumps, never at the " +
            "record seat) until the script ends 61.65 s later ON the record seat, where the " +
            "follower resumes from the script's last frame and holds node 0's armed stop point",
            ZeppelinScriptedPoseSuite));
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
            "broadside cannons (F19): C3/M03's Pandora as shipped (no COMPLETED_ZEPCANNONS) " +
            "neither deploys nor fires on a player abeam inside range for 30 s and does both " +
            "once engaged; then on C1/M04's engaged, flying piratezep a player inside the port " +
            "arc triggers the authored deploy anims (durations read from the defs, 4 s), the " +
            "readied side volleys real unowned wep_28 rounds lead-solved at the player while " +
            "the far side stays stowed, out-of-arc holds fire and retracts after the invented " +
            "idle window, an F18-destroyed cannon thins the next volley to 5, a " +
            "cannon_inaccuracy clone shows real scatter, and the zeppelin-vs-zeppelin arm " +
            "rand()-picks only the target's IN-ARC gasbags on constructed geometry", ZeppelinBroadsideSuite));
        into.Add(new TestHarness.Suite("zeppelin-cannon-burnout",
            "the animation-authored zeppelin kill on C1/M04's hk_zep (no zeppelin record): its " +
            "sabotaged broadside doors are deployed from t=0, real gun rounds destroy one door and " +
            "DamageAt two more, each door's WeaponHit death calls its gasbag burn, the burn " +
            "switches the panels off and calls the finisher whose REQUIRED OBJECT_INACTIVE_LIST " +
            "prerequisite reads those panels (compiled active_raw 2 = inactive, bit 1 is the " +
            "local-nodes scope), three finishers satisfy finish_locklear, lockleargoesdown brings " +
            "the hull down and the primary completes off lkgasbag05/panelleft1",
            ZeppelinCannonBurnout));
        into.Add(new TestHarness.Suite("zeppelin-hull-activation",
            "CM13's Pandora over C2/M03's real world: C2 alone ships the piratezep node with its " +
            "gamez active bit clear and no mission .gw sets it back, so the world builds the hull " +
            "hidden and the zeppelin record is what switches it on. With it on, the hook point, " +
            "the hangar bay and both landing cones resolve UNDER the hull, and after pzhomebase " +
            "(the anim OBJECTIVE8 wakes at the end of the race) has run its dock choreography the " +
            "hull, the hook and the hangar bay all draw — rather than a live dock on an invisible " +
            "Pandora", ZeppelinHullActivation));
        into.Add(new TestHarness.Suite("damage-stages",
            "each DAMAGE_SEQUENCE def fires its stage effects across an HP sweep", DamageStages));
        into.Add(new TestHarness.Suite("damage-hd",
            "weapon hits destroy, swap, drop colliders, and survive destroy→reset→destroy", DamageHd));
        into.Add(new TestHarness.Suite("stop-sequence",
            "authored STOP_SEQUENCE stops run: the fireball's 0.3 s stopper, the 30 s fire's halt, and the car lap's stopped car_dust1 refusing every later lap's call", StopSequenceStops));
        into.Add(new TestHarness.Suite("first-person-condition",
            "the PLAYER_1ST_PERSON condition follows the pilot's selected view mode: the bullethole def's else branch runs in Chase and is skipped in Cockpit and Nose (A1)", PlayerFirstPersonCondition));
        into.Add(new TestHarness.Suite("death-slot",
            "a killed destructible dispatches its compiled destruction slot — the block carrying the 30 s fire's 1,035 death calls (BL-276)", DeathSlotDispatches));
        into.Add(new TestHarness.Suite("start-state-swap-pool",
            "a destructible whose own Initial sequence authors the healthy/destroyed swap directly (a start-state script's shape, never DamageAt) leaves the HP pool destroyed too, so a later hit does not replay the death choreography (BL-513, BL-521)", StartStateSwapSyncsThePool));
        into.Add(new TestHarness.Suite("carried-state-silent",
            "a persist-log state lands on the pool and the destroyed pose with no instance started, a carried partial HP lands at its stage, and a later hit on the carried kill is a no-op", CarriedStateIsSilent));
        into.Add(new TestHarness.Suite("wait-for-completion",
            "a WAIT_FOR_COMPLETION call holds the caller's next event for its callee, and an unflagged one beside it does not (BL-228)", WaitForCompletion));
        into.Add(new TestHarness.Suite("emitter-host-deactivation",
            "a host going inactive spares the emitter that started in its own instant and still ends the one that did not (BL-229)", EmitterHostDeactivation));
        into.Add(new TestHarness.Suite("effect-template-mesh",
            "an effect's template meshes show at the call site — including a CALLED template's — and go dark when it ends (BL-061)", EffectTemplateMesh));
        into.Add(new TestHarness.Suite("fbfx-flash",
            "he_ground_effect's six-step full-screen wash reports its authored run times, so the 1.2 s ramp does not collapse into one instant; the ramp routes by pane proximity and the victim-routed blend wash by player index, composited over it", FbfxFlash));
        into.Add(new TestHarness.Suite("callback-events",
            "a destroy def's CALLBACK 16 hands the instance the rig's wreck velocity and its 15 stops the damage stages, on player-player and fury-fury; an authored code the runtime does not act on is counted, and no def in the chapter authors the free arm, code 0 (D18)", CallbackEvents));
        into.Add(new TestHarness.Suite("ordnance-burst-timeline",
            "the HE, flash and sonic bursts play end to end and every sequence's whole event timeline matches the authored JSON — in order, at the authored time (D31)", OrdnanceBurstTimeline));
        into.Add(new TestHarness.Suite("effect-pool-reset",
            "a pooled effect copy is re-reset on checkout: the sonic burst played five times over a four-slot pool draws its rings on the fifth play exactly as on the first (BL-406)", EffectPoolReset));
        into.Add(new TestHarness.Suite("effect-pool-spawn-pose",
            "a pooled effect copy is restored to its SPAWN POSE on checkout: the HE burst played five times over a four-slot pool, overlapping so the fifth play takes the first's still-live copy, launches its debris from the same pose the first did (BL-511)", EffectPoolSpawnPose));
        into.Add(new TestHarness.Suite("repeat-call-slots",
            "a template root CALLED REPEATEDLY from one anchor takes a pooled copy per authored call, not one for the anchor: pdpanel7's four gimmeflakes calls at pdp7 hold four copies, and a second tear reclaims those four rather than wrapping the pool (D21)", RepeatCallSlots));
        into.Add(new TestHarness.Suite("effects-census",
            "the full --effects-test sweep as verdicts: every effect resolves, template meshes show at the CALL SITE (not the stage origin), and none stays lit after its stop", EffectsCensus));
        into.Add(new TestHarness.Suite("bounce-launch",
            "a bounce-terminated OBJECT_MOTION flies its solved parabola and fires its BOUNCE_SEQUENCE on landing", BounceLaunch));
        into.Add(new TestHarness.Suite("ground-contact",
            "a gravity-bearing OBJECT_MOTION is cut short by real geometry through the right tier (default column, do_intersections sweep, no_altitude neither), rests on the surface and picks its BOUNCE_SEQUENCE branch from what it struck, and does none of it without a mask", GroundContact));
        into.Add(new TestHarness.Suite("forward-rotation",
            "an OBJECT_MOTION tumble turns at the authored RATE about its own launch direction's horizontal perpendicular, scaled by that direction's length — a vector-translation launch about its compiled direction, and not at all when that is zero", ForwardRotation));
        into.Add(new TestHarness.Suite("launch-direction-cache",
            "a vector-form OBJECT_MOTION's third triple is the compiled launch DIRECTION the tumble reads back, not a random spread: two bodies fly the identical path and end exactly where initial × run_time puts them", LaunchDirectionCache));
        into.Add(new TestHarness.Suite("barracuda-drive",
            "a chain of OBJECT_MOTION events on one placed node integrates every leg from the node's live pose: C3/M03's sub_movement drives the Barracuda 1680 m along +Z from its reset placement with no frame-to-frame jump and ends within metres of its closing FromTo in the bay", BarracudaDrive));
        into.Add(new TestHarness.Suite("anim-activation-prerequisite",
            "a CALL_ANIMATION is answered only once MINIMUM_TO_SATISFY of the animations the callee's ACTIVATION_PREREQUISITE names have run: CM18's cargozep_floatfree holds the Black Swan at its mooring through four of its five restraint deaths and releases it on the fifth, while an explicit Play still starts a hull death carrying the same shape", ActivationPrerequisite));
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
        into.Add(new TestHarness.Suite("plane-shader-reuse",
            "a second aircraft build reuses the first's Shader resources instead of generating its own copies of the same text, which is what keeps a mid-flight generator launch off Godot's per-shader compile",
            PlaneShaderReuse));
        into.Add(new TestHarness.Suite("cockpit-interior",
            "the player plane's cockpit1 interior builds hidden at the cockpit_camera marker, an AI-style build gains nothing, and the per-mode hiding follows the pilot's view (B11)", CockpitInterior));
        into.Add(new TestHarness.Suite("cockpit-overlay-pass",
            "--cockpit-pass moves the interior into a world of its own, where it and the camera both sit at the origin and no chapter-scale coordinate reaches the panel's transform (PLAN-cockpit-panel C21)", CockpitOverlayPass));
        into.Add(new TestHarness.Suite("collision-visibility",
            "nothing a chapter hides is left solid: no enabled collider under an invisible node", CollisionVisibility));
        into.Add(new TestHarness.Suite("nodelab-visibility",
            "the node lab's tree row follows live Visible, not the hide button's last action", NodeLabVisibility));
        into.Add(new TestHarness.Suite("trail-world-anchor",
            "a trail emitter under a rotated carrier anchors at world identity and drops puffs where it is fed", TrailWorldAnchor));
        into.Add(new TestHarness.Suite("turret-death-effect-world-anchor",
            "a carried turret's death fire, anchored on TurretController.Site under a moving/rotating hull, follows the hull between ticks rather than freezing at the pose it started at (BL-514)", TurretDeathEffectWorldAnchor));
        into.Add(new TestHarness.Suite("turret-death-fire-follows-hull",
            "a zeppelin gun ring's death fire, routed WITH_NODE to the world-effects stage, moves with the ring as the hull flies on instead of holding the point it was placed at", TurretDeathFireFollowsHull));
        into.Add(new TestHarness.Suite("ring-death-effects-follow-hull",
            "a C1C/M01 gun ring killed on the moving hull: its AT_NODE fireball and its flying-parts debris move with the hull over the next frames instead of standing where the ring died", RingDeathEffectsFollowHull));
        into.Add(new TestHarness.Suite("damage-template-pool",
            "a second panel's tear takes its own pooled gimmeflakes copy and leaves the first burst flying at its site (BL-288)", DamageTemplatePool));
        into.Add(new TestHarness.Suite("damage-staging-pool",
            "the injure staging reads health only: a zone stripped of armour tears no panel though its combined fraction has crossed the threshold, and the panel appears once health itself crosses (BL-384)", DamageStagingPool));
        into.Add(new TestHarness.Suite("cockpit-panel-staging",
            "the cockpit-interior torn panels pcdp4/pcdp6 flip off the SAME pdpanel4/pdpanel6 injure entries as their exterior namesakes, survive a CockpitVisibility view-mode switch, and clear together on respawn's Reset() (B12)", CockpitPanelStaging));
        into.Add(new TestHarness.Suite("damage-stage-slots",
            "the injure ladder stages per ENTRY: fury's six random_remote_damage thresholds each fire, a repair retracts what it lifted back over, and one entry on four zones fires four times (BL-385/BL-384)", DamageStageSlots));
        into.Add(new TestHarness.Suite("ai-damage-stages",
            "an AI plane spawned through FlightRoster stages end to end: its hull falls through the take-hit path and the rig runtime starts six random_remote_damage instances plus one pfsmoketrail, all anchored inside that aircraft, a repair tears each stage down once, and the Bloodhawk's missing elevator pair is named (BL-385)", AiDamageStages));
        into.Add(new TestHarness.Suite("crash-rig-anchors",
            "binding the crash rig leaves the airframe model under the controller — even the Devastator, whose model root shares the crash defs' authored NAME — and stages every pooled copy in the same reset pose", CrashRigAnchors));
        into.Add(new TestHarness.Suite("nitro-boost-anchors",
            "nitro_boost/nitro_decay author NAME \"warhawk\" as their anchor, which never resolves inside a per-plane crash rig; Play's PlaneModel fallback (the same shape startprops/stopprops already use) starts both defs on the flown Warhawk and sustains its nitropuff1 exhaust puffer, though no flyable model carries the nitropropN disc geometry itself", NitroBoostAnchors));
        into.Add(new TestHarness.Suite("emitter-prewarm",
            "a crash rig's and the world-effects stage's PUFFER_STATE emitters are built at bind, unstarted: a crash, a panel tear, a post-respawn crash and five sonic bursts over a four-slot pool all reach the factory for no emitter, and the claims still count as built", EmitterPrewarm));
        into.Add(new TestHarness.Suite("ai-crash-defs",
            "an AI plane's crash rig binds the ai_crash_* family and its crash indexes it by the struck surface id — dirt(13) plays ai_crash_dirt, no material plays ai_crash_default, and the def switches off both the airframe's healthy subtree and the crash root's wreck — while a human rig off the same factory keeps player_crash_* (G21)", AiCrashDefs));
        into.Add(new TestHarness.Suite("ai-wreck-fall",
            "a killed AI aircraft's whole fall: the kill starts its self-named destroy def and no ai_crash_* def, the airframe is drawn on every frame of the fall, the hull travels under the flight model until Callback 15 releases it at the authored 3.0 s, Callback 16 hands the anim the velocity it reached THERE, a wreck that meets the ground first plays its surface-indexed crash def and is hidden only then (D21), and the lever/surface command last written by the AI think reads back bit-identical every frame of the fall (BL-451)", AiWreckFall));
        into.Add(new TestHarness.Suite("player-destroy-choreography",
            "a shot-down player plays player-player whole: the two authored arms are chosen by the def's own IF NODE_ACTIVE 1 (its node one is `player_autogyro`, so only the autogyro stops its rotor), the cockpit eject stages and shows its cpilot, all four wreck pieces appear and fly their own OBJECT_MOTION, and the camera-only Callback 3 stays counted rather than invented (D25)", PlayerDestroyChoreography));
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
            + "real emplacements, every site dead as ia1.gw leaves it and the five aaguns landing "
            + "on the Non-Aircraft cycle once their sites are switched on", TargetPoolModel));
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
        into.Add(new TestHarness.Suite("campaign-persistence",
            "the cross-mission state log (B12, BL-243): three PERSIST_LOG objects destroyed in one "
            + "campaign mission are captured, survive the profile file and a second store instance, "
            + "and start the next mission of the SAME chapter destroyed, while a save-only "
            + "destructible killed alongside them never enters the log and starts that mission "
            + "intact; the later world is built from the bootstrap, so the log is the only thing "
            + "that could have wrecked them",
            CampaignPersistence));
        into.Add(new TestHarness.Suite("music-states",
            "the state-driven score (D37): each game state cues the track family its data names, "
            + "prebattle and battle loop while the stingers and the success tracks play once, "
            + "re-entering the playing state never restarts it, the objective stingers alternate "
            + "their two takes instead of drawing at random, and a combat ping cuts prebattle to "
            + "battle at silence, ramps it to full in a quarter second, holds it 20 s and fades it "
            + "out over four; and the briefing's duck rides over that fade without touching it, "
            + "holding the channel down while the screen shows and lifting when it goes",
            MusicStates));
        into.Add(new TestHarness.Suite("campaign-objectives",
            "the objectives runtime (D31) over a shipped mission's own choreography, headless: the "
            + "BEGIN_DORMANT wake timings and their sound/turret actions, the primary completing "
            + "off an INACTIVEn node, its KILL/WAKE/NAP chains and target-list edits, the display "
            + "rows and the mask's bit 0, plus BOTH endings the script authors — the INSTANTWIN "
            + "path and the 300 s reminder fuse that naps the INSTANTLOSS objective",
            CampaignObjectives));
        into.Add(new TestHarness.Suite("campaign-mission-end",
            "the campaign mission-end flow against a BUILT world (D31): a scripted kill drives an "
            + "INACTIVEn condition off real node state, the graph's own end ends the mission, and "
            + "the result reaches the profile through CampaignProgression with the destruction log "
            + "captured and the return-to-cabin exit raised",
            CampaignMissionEnd));
        into.Add(new TestHarness.Suite("targeting-candidates",
            "the D36 widened AI acquisition (BL-363): a registered structure whose pool authors no " +
            "team is nobody's target and a same-team one is refused, while " +
            "a real team's AI routes a winning structure candidate into AiGunner.GroundTarget " +
            "rather than the aircraft-only Target field (so AiPilot's flight law sees nothing new), " +
            "and the gunner fires real rounds at a zeppelin structure with no aircraft in the scan " +
            "at all",
            TargetingCandidates));
        into.Add(new TestHarness.Suite("debug-kill-target",
            "the F17 debug kill key's routing (A2, BL-534): an aircraft source crashes through the " +
            "attributed DebugForceCrash/Downed path, a destructible source (a zeppelin sub-part) is " +
            "destroyed through the same AnimRuntime.DamageAt a rocket uses, and a turret or any " +
            "other source with no decoded HEALTH key is left inert rather than inventing a kill " +
            "path for it; nothing selected does not throw",
            DebugKillTargetRouting));
        into.Add(new TestHarness.Suite("ranked-pool-carried-turret-dedup",
            "the AI ranked pool's carried-turret guard (BL-507): a hostile aircraft with a crewed " +
            "rear mount rides the pool as one Vehicle entry, never a second entry for its own " +
            "turret, while a world emplacement in the same scene still reaches the pool",
            RankedPoolCarriedTurretDedup));
        into.Add(new TestHarness.Suite("partition-areas",
            "the area-selected node toggle (BL-037) over C3's own three story rectangles: each " +
            "resolves through the partition grid to real world content, the half-open cell rule " +
            "holds (a rectangle inside one cell selects nothing), corner order does not change " +
            "the selection, and C3/M01's built world really has its third area switched off with " +
            "the first left standing",
            PartitionAreas));
        into.Add(new TestHarness.Suite("scripted-path",
            "the second movement law (BL-361) over C1's authored pp1 takeoff path: a placed " +
            "vehicle sits frozen on its first waypoint however long the mission runs, START_TAXI " +
            "releases it onto a 40 mph taxi, the final leg accelerates past that speed and lifts " +
            "it off the strip, and reaching the final leg's decoded 300 m point hands it back at " +
            "the speed it reached",
            ScriptedPathTaxi));
        into.Add(new TestHarness.Suite("generator-takeoff-run",
            "a surface generator's launch flies its take-off run (BL-522) over C1/M02's eairg31: " +
            "the launched aircraft is held on the run from the decoded launch pose, passes every " +
            "eag31_aip point in order at the taxi law's speeds, and at the final leg's decoded " +
            "300 m overshoot point, well past the last point and climbing, is released into the " +
            "flight model well above the taxi speed with the lever open, nose up, its patrol net " +
            "reseated where it arrived",
            GeneratorTakeOffRun));
        into.Add(new TestHarness.Suite("generator-launch-climb-out",
            "the same eairg31 launch on C1/M02's real airfield with the world's colliders up, " +
            "flown by its own pilot for 30 s after the hand-off: the Peacemaker is alive with no " +
            "ram and no crash, never came back down to the strip, and ends above the field",
            GeneratorLaunchClimbOut));
        into.Add(new TestHarness.Suite("wingman-station",
            "the D34 campaign wingman (BL-362): the decoded netless mode-wingman escort law as " +
            "geometry (both body-frame stations, the rolled-leader frame, the 106.68/259.08 m " +
            "target station, the 80 m separation push, the 700 m and 20.576 m/s join gates) and " +
            "then flown against a scripted leader, a live wingman joining from 1200 m abeam, " +
            "staying with the leader for the rest of the run, and riding the aft station behind " +
            "a player leader where it rides the forward one behind an AI leader",
            WingmanStation));
        into.Add(new TestHarness.Suite("wingman-engage",
            "what a campaign wingman does about a hostile (BL-505): every escorting block CM02 " +
            "plans flies an attack gate wider than BL-504's 1 m and most author rating_biases, " +
            "and over a flown leg with one bandit ahead of the pair the wingman acquires it " +
            "through the ordinary ranking and fires from the station, while the decoded escort " +
            "state never leaves the formation, which is FUN_0041e760's own shape",
            WingmanEngage));
        into.Add(new TestHarness.Suite("campaign-objectives-hud",
            "D33's in-flight objectives display and cue firing against a BUILT campaign world: " +
            "ObjectivesHud carries one line per ObjectiveGraph display row, a scripted " +
            "IDENTITY objective completing off whichever condition the chapter's own mission " +
            "authors (an INACTIVEn node list, a danger zone, or no condition at all) marks its " +
            "own readout line " +
            "(not only the graph's), a WAKEUP_SOUND_GROUP the mission authors starts a real " +
            "AudioStreamPlayer3D through WorldSounds (D31's existing routing, counted rather than " +
            "duplicated), and whichever cue surface the mission chose, WAKEUP_SOUND_GROUP or " +
            "COMPLETED_SOUND_GROUP, one of its groups reaches a real player (BL-483)",
            CampaignObjectivesHud));
        into.Add(new TestHarness.Suite("campaign-cutscene",
            "the cutscene host over C1/M04's shipped intro definition (D32): its authored callback "
            + "codes reach the host through the runtime's own dispatch, the world and the "
            + "objectives update stop while callback 20 holds them, the vehicle-death codes are "
            + "declined, and the definition's end hands off with every piece of cutscene state put "
            + "back; and C3/M03's opening scene, which no start list names, is reached through its "
            + "start anim's call: the world build stages its camera and player marker, the host "
            + "takes its codes, the destruction runs under it once, and the skip it arms ends it; "
            + "plus what an episode owes a seat that entered it from the cockpit view, at two seats "
            + "and over both exits: the airframe drawn and the interior pass off while the "
            + "presentation holds, both back on the hand-back, and neither seat's selected view "
            + "moved to get there (BL-625)",
            CampaignCutscene));
        into.Add(new TestHarness.Suite("cutscene-letterbox",
            "the letterbox bars are data (D32): the chapter ships the node switched off as its "
            + "definition's base state, calling the definition switches it on outright with no "
            + "reveal, and the AT_NODE re-assert copies the cutscene camera's whole frame onto it "
            + "every tick",
            CutsceneLetterbox));

        // BL-482: the world build put no aircraft-archive node in the runtime's node table, so both
        // names an intro animates claimed a symbol with a null binding and the camera moved over an
        // empty sky.
        into.Add(new TestHarness.Suite("intro-aircraft-stage",
            "the two aircraft a story-mission intro animates, over the first story mission's BUILT "
            + "world: the aircraft archive's 'player' and 'piratefighter' answer the compiled "
            + "intro's own cross-archive pointers in the runtime's node table, the intro's scripts "
            + "fly both away from the world origin, its OBJECT_ADD_CHILD puts the prop under the "
            + "airship, the flown airframe is drawn on the 'player' marker while callback 11 holds "
            + "the pilot out of flight, and the drop's launch cue fires",
            IntroAircraftStage));
        into.Add(new TestHarness.Suite("intro-wingmen",
            "the wingman C1/M04's shipped intro shows beside the player, over its BUILT world: "
            + "'playerdrop' and 'playerthruclouds' each run their 'pfighterNN' definition, and the "
            + "aircraft archive's 'piratefighter' prop is drawn, posed off the origin and inside the "
            + "cutscene camera's frustum through the launch and the dive",
            IntroWingmanSuites.IntroWingmen));

        // BL-540: the aircraft archive's 'chuteman' subtree never joined the world build, so a
        // mid-mission drop-off's own definition claimed a symbol with a null binding, the same
        // staging gap B8 fixed for the intro's two aircraft.
        into.Add(new TestHarness.Suite("dropoff-chuteman-stage",
            "the parachutist a mid-mission drop-off animates, over C3/M01's BUILT world: the "
            + "aircraft archive's 'chuteman'/'chutemanparent'/'pilot'/'stamp' answer the compiled "
            + "drop's own cross-archive pointers in the runtime's node table, the subtree ships "
            + "switched off as the shared def's own base state, and calling the drop's definition "
            + "directly (never the approach cone) switches it visible",
            DropoffChutemanStage));

        // BL-553: the drop-off's handoff put the pilot back where the cutscene found them, so the
        // pose its own definition parks the 'player' node at, and the callback that reads it, both
        // went nowhere.
        into.Add(new TestHarness.Suite("dropoff-placement",
            "where a mid-mission cutscene leaves the pilot, over C3/M01's BUILT world: the mission's "
            + "own cutscenes directory carries the definition raising the re-placement callback, and "
            + "driving it directly (never the approach cone) flies the aeroplane out of the world "
            + "pose that definition parked its 'player' node at, on that node's heading and at the "
            + "original's release speed, rather than out of where the pilot flew in",
            DropoffPlacement));

        // BL-633: the re-placement callback read the marker wherever the build parked it, so a
        // definition that raises it without posing that node put the pilot on the world root.
        into.Add(new TestHarness.Suite("cutscene-handoff-unposed",
            "where a mid-mission cutscene leaves the pilot when it authors no placement, over "
            + "C2/M05's BUILT world: its paratrooper drop raises the same re-placement callback "
            + "while naming no 'player' node at all, and the handoff leaves the aeroplane at the "
            + "pose the drop found it at, above the surface measured under that pose, rather than "
            + "on the world root the marker is parked at",
            CutsceneHandoffUnposed));
        into.Add(new TestHarness.Suite("hangar-door-wake",
            "hangar doors over C1/M04's real world (BL-350): OBJECTIVE1's WAKE_ANIM reaches "
            + "'hangar3_doors' through the director at its authored 2 s dormancy and its four "
            + "panels slide their authored 50 m by 12 s; the generator eairg31's own hangar takes "
            + "the loader's node-name door default (eairg_open31, the close resolving the open), "
            + "and its door starts opening the decoded 4 s before the spawn, which lands at that "
            + "hangar",
            HangarDoorWake));
        into.Add(new TestHarness.Suite("anim-clock-realtime",
            "which callback feeds the animation runtime under each clock mode: on a realtime "
            + "session the physics tick advances it by its own dt and the frame's wall delta "
            + "advances nothing, a cutscene hold hands the advance back to the frame, a halt "
            + "advances neither until a queued step, and fixed-step takes exactly one step per "
            + "frame from the frame; a mission-ending hold keeps both callbacks quiet on the "
            + "last flown pose",
            AnimClockRealtime));
        into.Add(new TestHarness.Suite("fog-state",
            "the FOG_STATE animation event (BL-038) over C1/M04's intro definition: a weather "
            + "rig writes the zone's fog at build, playing the intro raises its RESET_STATE fog "
            + "through the runtime's own dispatch, the three fog globals change to the event's "
            + "authored 'drop_fog' values, an event raised before the rig has built lands after "
            + "the zone, and a field the event omits is left as the zone wrote it",
            FogStateEvent));
        into.Add(new TestHarness.Suite("campaign-submarine",
            "CM04's authored-inactive barracuda remains built but hidden until sub_movement "
            + "resolves and activates it after the patrol phase",
            CampaignSubmarine));
        into.Add(new TestHarness.Suite("campaign-roster",
            "the campaign roster spawner (D34, BL-362/BL-364) over C1/M04's shipped aiv roster in "
            + "its built world: every enabled non-player block gets a rig, while disabled blocks "
            + "remain generator templates; the decoded fork puts an escort "
            + "on the netless wingman_1 (leader: the player rig) and on wingman_2/3 (leaders: the "
            + "devastator blocks) and a patrol net on every netted block with no block carrying "
            + "both, the deactivated blocks are inert, the four taxiPath vehicles are placed frozen, "
            + "a net's authored 700 m return radius reaches its block under the min_ai_active_dist "
            + "floor, and over a two-minute flown run wingman_1 holds the scripted player inside "
            + "the wingman-station leash",
            CampaignRoster));
        into.Add(new TestHarness.Suite("generator-roster-params",
            "the mission spawner's roster read (BL-453) over C5/M04: the dantezep generator's "
            + "vehicle.params label 'Miles' resolves to the disabled block stihellhound_5_7 with "
            + "no campaign profile in the run, the aircraft it launches carries that block's "
            + "nitro slot and its own authored name, its volumes reach the machine under the "
            + "min_ai_active_dist floor, the CLI-airframe fallback a parameterless generator "
            + "takes installs no injector, and over C1/M04 eairg32's misspelt 'Eairg32_params' "
            + "resolves the decoded empty launch (nothing built, no airframe) while eairg31's "
            + "label resolves its block, and over C2/M01 eshipg31's 'Eshipg31_params' resolves a "
            + "surface launch of the patrolboat hull",
            GeneratorRosterParams));
        into.Add(new TestHarness.Suite("campaign-bomber-formation",
            "CM02's three netted bombers (BL-498) spawned from C3/M05's own aiv roster into its "
            + "built world: all three carry net 19, they leave their shared seat node the same way "
            + "and fly one node of it together for a minute with nobody engaging them, and a "
            + "certain steady-hand failure on one leaves it on that node and back with the other two",
            BomberFormation));
        into.Add(new TestHarness.Suite("campaign-kill-credit",
            "the debrief's two per-airframe kill tallies, credited off real Downed "
            + "reports over C3/M05's shipped roster: two of the three netted britbalmoral bombers "
            + "and three of the five plain britpeace Peacemakers land in the plain array, the ace "
            + "britpeace_7 lands in the starred array instead, and a friendly wingman's loss and an "
            + "unattributed one reach neither",
            CampaignKillCredit));

        // ⚠ Keep after every other content suite: it leaves its own profile file behind for the
        // next process to read. campaign-zeppelins below touches none of that state.
        into.Add(new TestHarness.Suite("campaign-loop",
            "the whole campaign loop on the campaign's first mission (E41): a profile created on a "
            + "store holding none, the cabin, the briefing, the flight check, an ammunition change "
            + "that reaches the flown aircraft's guns, the mission's intro cutscene holding the "
            + "objectives clock, its primary objective completed by flying the approach it names, a "
            + "track its own data cues, the authored end recorded into the profile, and the cabin "
            + "again with Next Mission advanced; the profile is left on disk, so a second run "
            + "reads what the first one wrote",
            CampaignLoop));

        // BL-451: a --campaign= launch's SessionSpec never carried Zeppelins/Generators, so the
        // mission's zeppelins sat deactivated at the origin.
        into.Add(new TestHarness.Suite("campaign-zeppelins",
            "SessionSpec.FromCampaign sets neither Zeppelins nor Generators; GameSession's "
            + "campaign-mission peek turns Zeppelins on for C3/M01 (ships zeppelins.zrd) and leaves "
            + "Generators off (its egen.zrd is authored empty), and the mission's own zeppelins "
            + "place live once the flag is on",
            CampaignZeppelins));
        // Registered after campaign-loop, not because order matters to it: this suite's director
        // uses no file-backed store (profile is in-memory), so it cannot disturb what campaign-loop
        // left on disk for a later process.
        into.Add(new TestHarness.Suite("campaign-danger-zones",
            "BL-458's campaign danger zones over C3/M01's own dzpathN gates, resolved against real "
            + "world geometry: the mission's DANGER_ZONES_COMPLETED names (dzpath1, dzpath4) are "
            + "armed and no others, a scripted crossing of both authored gates fires each zone's "
            + "completion, and the director's real NotifyDangerZoneCompleted path completes the "
            + "SECONDARY (OBJECTIVE3) and OBJECTIVE11 the way a flown mission would",
            CampaignDangerZoneObjectives));
        into.Add(new TestHarness.Suite("campaign-racers",
            "CM13's six hafury racers spawned from C2/M03's own aiv roster into its built world "
            + "with the colliders up: each resolves basic_airplane's single origin collision probe "
            + "(the decoded AI contact shape, so its wings clip through the dbase arch dzpath2 "
            + "threads at rail height), and over the flown run every racer flies its net's seven "
            + "tagged zones on rails in course order, each end to end and once only, returning to "
            + "its net between them and never ramming anything",
            CampaignRacers));

        // BL-468: the objective-target store had no consumer, so a flown mission never showed the
        // player where its sites were.
        into.Add(new TestHarness.Suite("campaign-objective-markers",
            "the campaign's objective markers over the first story mission's BUILT world: every "
            + "site its targets.zrd flags carries a marker with the original's category line over "
            + "the site name, in the decoded blue, sitting on the world node the mission named, "
            + "and flying one site's own TRAVELERS approach retires that marker alone",
            CampaignObjectiveMarkers));

        into.Add(new TestHarness.Suite("campaign-objective-target-path",
            "a path-authored objective target over C1/M04's BUILT world: with two rock_zeppelin "
            + "nodes present, ADD_OBJECTIVE_TARGET [[piratezep, rock_zeppelin]] is held as one key, "
            + "offers one site standing on the hull's own child rather than the ground node, and "
            + "carries the SET_HELP_LABEL written against the same path",
            CampaignObjectiveTargetPath));

        into.Add(new TestHarness.Suite("campaign-objective-labels",
            "the objective marker's text over C1C/M01's BUILT world, the one mission with no "
            + "targets.zrd of its own: its three flown targets take the chapter's table through the "
            + "reader search path and label 'Zeppelin [Disable] -' over 'Worker's Voyage' in red and "
            + "'[Dock] -' over each docking hook's proper name in blue, the node key kept as identity",
            CampaignObjectiveLabels));

        into.Add(new TestHarness.Suite("campaign-race-chain",
            "the Hollywood race (C2/M03) over its own files: against the BUILT chapter world the "
            + "first site's marker stands on the seaplane hangar the zone runs through (a group "
            + "node at the world origin, its parts carrying the coordinates) beside dz1, and "
            + "headless over the real graph a player flying dzpath1, 2 and 3 in order with every "
            + "racer alive completes OBJECTIVE17, 18 and 19 with no racer-death DEDG firing, while "
            + "a racer dying afterwards fires its DEDG and kills the rest of the chain",
            CampaignRaceSuites.CampaignRaceChain));

        into.Add(new TestHarness.Suite("campaign-balloon-marker",
            "CM10's attack-balloon markers over C1/M05's BUILT world: its nine lifesaver sites are "
            + "group nodes standing on the water with the balloon hung above and the lifeboat at "
            + "the group's own origin, so the marker stands on the group's geometry clear of the "
            + "boat, flies with the assembly and rises when the balloon alone rises, and retires "
            + "when the balloon its objective watches goes inactive while the boat is still afloat",
            CampaignBalloonMarker));

        into.Add(new TestHarness.Suite("campaign-balloon-death",
            "CM10's attack balloons as destructibles over C1/M05's BUILT world: each lifesaver "
            + "group node carries two weapon-hit pools, the balloon's lifefallNM at 60 HP and the "
            + "lifeboat's lboat_destructionNM at 40, so a hit on the envelope reaches the balloon's "
            + "own pool on all nine sites, and killing one flies its six bursting pieces, drops the "
            + "envelope and leaves healthy_balloon hidden for the objective graph",
            CampaignBalloonDeathSuites.CampaignBalloonDeath));

        // BL-467: nothing read landings.zrd, so no mission could ever play the cutscene an
        // ANIM_STATE objective waits on.
        into.Add(new TestHarness.Suite("landings-approach-trigger",
            "the mid-mission cutscene trigger over the first story mission's BUILT world: the "
            + "chapter's landings.zrd rows resolve to the cone, half-cone and sphere volumes their "
            + "approach nodes author, a mission carrying none of the animations resolves none of "
            + "them, the drop-off rows start disarmed and fire nothing, the mission's own objective "
            + "chain arms them, and flying one cone starts the drop cutscene through the cutscene "
            + "host, runs it to EXECUTED and completes the primary objective gated on it",
            LandingApproachTrigger));

        // BL-492/BL-495: CM02's wing-walk capture, whose approach rows the mission gates on a
        // land_on node state switched for all three Balmorals at once, and whose approach nodes
        // reach the world only on the rigs the roster spawns.
        into.Add(new TestHarness.Suite("landings-wingwalk-gate",
            "CM02's Balmoral capture gate over its BUILT world with its own roster spawned: the "
            + "mission's three approach rows resolve and differ only by index, the world build "
            + "alone reaches none of them, the roster spawn grafts each onto the rig its block "
            + "spawned so all five rows bind, one pair of definitions arms and un-arms all three "
            + "land_on nodes by gamez index, the objective calling the arming one waits on those "
            + "planes' aiv group being down to one, and a driven approach at an armed Balmoral "
            + "starts the capture where the same approach before the gate starts nothing",
            WingWalkCaptureGate));

        into.Add(new TestHarness.Suite("landings-train-pickup-gate",
            "CM07's caboose pickup through its real range-triggered mission path: approaching the "
            + "train stages the passenger and flare rig, ladder sensor and docking cone from their "
            + "library root, the train's own pickup_timing opens land_on in its first flyable "
            + "phase, the landing trigger discovers that late-created cone, and flying it starts "
            + "the hosted pickup cutscene beside the caboose, faces the passenger, runs to handoff "
            + "and clears the primary pickup objective",
            TrainPickupGate));

        into.Add(new TestHarness.Suite("landings-train-pickup-ride",
            "CM07's caboose pickup as the original runs it, over the mission's real moving train: "
            + "the staged passenger is the caboose's child and keeps its offset while the caboose "
            + "travels, the pickup timing opening the switch selects the wave with the lit flare "
            + "and its smoke trail laying sprites on that passenger's own hand, a level aircraft "
            + "inside the 100 m sensor drops the rope ladder and the drop's own callback settles it "
            + "deployed swinging on its looped wind script, and the pickup cutscene's call to "
            + "caboosepickup holds a live instance for the person's climb while cabpkup_ladder "
            + "swings the rungs he climbs",
            TrainPickupRide));

        into.Add(new TestHarness.Suite("landings-trailer-pickup-gate",
            "CM11's trailer pickup through the objective script's own WAKE_ANIM: the dock "
            + "objective's definition stages the approach cone from its library root under the "
            + "trailer's sensor, the landing trigger discovers that late-created cone, and flying "
            + "it starts the hosted pickup cutscene, runs to handoff and clears the dock objective",
            TrailerPickupGate));

        into.Add(new TestHarness.Suite("landings-car-pickup-credit",
            "CM16's armoured-car pickup, the one whose own film destroys what the mission is "
            + "watching: the mission's chain stages the cone and the waving passenger, flying it "
            + "starts the pickup, the row does not fire again underneath the episode, the film "
            + "calls destroy_car01 on its authored beat and reaches got_sparks on its own, and the "
            + "mission-completion code it raises wins the mission",
            CarPickupCredit));

        into.Add(new TestHarness.Suite("landings-auto-land-button",
            "the auto-land button over the first story mission's BUILT world: flying the chapter's "
            + "auto row lights AutoLandOffered but starts nothing while the button is up, a realtime "
            + "frame of the flown rig's own _Process actually draws the prompt, pressing the button "
            + "starts the same animation the manual row would, the cutscene host still runs it, and "
            + "holding the button past the handoff does not re-fire the row",
            AutoLandButton));

        // The hookup plays with no hook, the aeroplane too high and the wings unfolded when the
        // flown airframe's own subtree is not in the runtime's node table.
        into.Add(new TestHarness.Suite("landings-hookup-airframe",
            "the zeppelin hookup on two airframes over the first story mission's BUILT world, "
            + "every value read from the aircraft archive's own definitions: the flown aircraft is "
            + "in the animation runtime's node table so the hookup's per-airframe branches can read "
            + "its active bit, it carries its own docking-hook group built retracted, and the "
            + "episode ends with that hook extended, the airframe hung at the mount offset the "
            + "extend-hook definition authors for it, and its wings turned to the angles its own "
            + "fold definition authors where the airframe has one, having swung that hook once "
            + "(BL-628: the episode's every start counted by name, the shared extend-hook "
            + "definition among them)",
            HookupAirframe));
        into.Add(new TestHarness.Suite("landings-hangar-drop-gate",
            "CM07's zeppelin-hangar drop over its BUILT world, every name read from the mission's "
            + "own data: the one cutscene definition it range-gates, the ambient definition that "
            + "calls it, and the objective node the drop's RESET_STATE clears. The call ARMS the "
            + "drop without running it while the player is outside the authored band, reaching the "
            + "hangar runs it and hands it to the cutscene host, and the reset block at the handoff "
            + "clears that node, which is what completes the fly-through objective",
            HangarDropGate));

        // The docking cutscene handed the player flight back when its first callee ended and
        // teleported them when the row's definition ran out, because the episode was booked to
        // whichever definition raised the first code.
        into.Add(new TestHarness.Suite("landings-docking-hold",
            "CM06's docking onto the Workers' Voyage over its BUILT world, the one shipped row "
            + "whose definition raises no code of its own: the episode belongs to the row the "
            + "trigger started rather than the hookup callee that raised the first code, the "
            + "player is held out of flight from the hookup through the drop to the unhook's own "
            + "handoff code although the row's definition ends before it on a trailing wait, the "
            + "host hands the session back on that code, the released aeroplane flies out of the "
            + "player marker's pose where the re-placement code read it, and no frame after the "
            + "release teleports it. The hookup calls its drop and pickup legs unconditionally and "
            + "only each leg's own REQUIRED node state keeps the wrong one off, so all three fork "
            + "paths resolve to nodes this world built (one resolving to none would pass the gate "
            + "vacuously), the drop leg runs on the first docking while the two pickup legs do "
            + "not, and nothing the row reaches plays twice (BL-525, BL-628)",
            DockingHold));

        // BL-494: callback codes 965 to 967, which put the player in a different airframe mid
        // mission and reached nothing until the roster grew a swap.
        into.Add(new TestHarness.Suite("campaign-airframe-swap",
            "the mission-script host's airframe swap over CM02's BUILT world: the three decoded "
            + "codes name the defs the shipped stat rows carry, the mission's own capture "
            + "definition authors the Balmoral one, and driving that code through the host "
            + "rebuilds the player's rig on the named airframe at the pose, heading and speed it "
            + "was flying, with that airframe's stock hardpoint table at full ammunition and its "
            + "own armour pools and damage zones rather than the airframe it replaced, the "
            + "outgoing aircraft out of the world with no registration of its own left in the "
            + "projectile pool, and the cutscene flags the code sets landing on the aircraft the "
            + "swap built; plus the two things 967 does past that rebuild, on the same data: the "
            + "capture definition's own aircraft hidden with what is left of its hull carried onto "
            + "the player's, and the outgoing aeroplane handed to wingman_4 -- authored "
            + "deactivated, flying the player's own airframe in this mission and its own def's "
            + "everywhere else, revealed 100 m off the old nose at -45 degrees on the player's own "
            + "heading with the sums measured off the hull it was given",
            AirframeSwapSuites.AirframeSwap));

        // BL-574/BL-575: the hangar hand-over gave a stock Bloodhawk in the pilot's own paint, and
        // both camera legs of the drop ran at once because their node-state prerequisite was
        // parsed away.
        into.Add(new TestHarness.Suite("campaign-hangar-handover",
            "CM07's hangar drop over that mission's BUILT world, played through the runtime on a "
            + "realtime clock: each camera leg and its twin carry opposite node-state "
            + "prerequisites on hdrop_direction, exactly one of each pair starts and it is the one "
            + "the sensor's state picks, the two legs after the hand-over start, and the 965 the "
            + "drop raises rebuilds the player on the Blue Streak build the special-plane template "
            + "carries -- twin 40 over twin 30, one pylon a wing, 20 armour a zone, the nitrous "
            + "injector -- in its shipped blo_* skins with no scheme over them (the blue-grey "
            + "body and yellow wingtips of the original), while a plain swap onto the same node "
            + "stays a stock Bloodhawk with no injector; the flown aeroplane rides the drop in "
            + "view: the hangar-floor Bloodhawk prop shows for the first leg and goes at the "
            + "swap, and after it the rig is drawn on the player marker, wearing the staged "
            + "undercarriage, as the lift leg moves that marker, and the parachutist the drop's "
            + "own site-less chute call animates is the one staged figure, drawn for the whole "
            + "leg, hanging under the actor the stage left where it stands and coming down at the "
            + "hangar rather than kilometres off it",
            AirframeSwapSuites.HangarHandover));

        // BL-542: the capture cutscene's camera rides the wing walk's moving frame, and that frame
        // is posed onto the aeroplane the capture belongs to.
        into.Add(new TestHarness.Suite("campaign-wingwalk-camera",
            "CM02's capture cutscene framing over that mission's BUILT world: the mission's own "
            + "capture definition started through the mission-trigger seam its approach table "
            + "starts it with, played on a realtime clock as isolated camera choreography, "
            + "with the captured aeroplane spawned from its own roster block at the position the "
            + "mission authors it -- far from the world origin, so a shot posed off nothing is not "
            + "mistaken for a framed one -- and camera1 read once a second against that "
            + "aeroplane's own position",
            WingWalkCameraSuites.WingWalkCamera));

        // The skip key removed the picture from a cutscene the original arms no skip on, handing
        // the player back an aeroplane mid wing walk.
        into.Add(new TestHarness.Suite("campaign-cutscene-skip",
            "which cutscene episodes offer the player a skip, over CM02's BUILT world: the "
            + "mission's capture definition and its whole call closure author no hold code, "
            + "which is the code that arms a skip, so playing that capture on a realtime clock "
            + "and pressing the skip key two seconds in is "
            + "refused -- the episode keeps the session and its picture, and the swap, the hide "
            + "and the hand-over land at the same second and in the same end state as the run "
            + "played undisturbed -- while an episode that has raised the hold code takes the "
            + "same key press and restores on it",
            CutsceneSkipSuites.CutsceneSkip));

        // BL-401: the assembler named every spawn ai{n}_{plane}, which no authored pattern can
        // match, so rating_biases was dead on the campaign path.
        into.Add(new TestHarness.Suite("roster-spawn-names",
            "BL-401's authored spawn identity over C1/M02's shipped roster: a campaign spawn "
            + "wears its roster block's own name while a spawn with none keeps the counter form, "
            + "wingman_4's authored exclusion on the bloodhawk_2 BLOCK matches the spawned node "
            + "and moves the live pick off it, and bloodhawk_2's always-target on the 'player' "
            + "role takes the human rig over a nearer aircraft",
            RosterSpawnNames));

        // BL-497: CampaignDirector passed null where a spawn's own talker/constitution ratings
        // would go, so every campaign pilot chattered at the session's flat rating-5 default.
        into.Add(new TestHarness.Suite("roster-voice-ratings",
            "BL-497's voice hand-off over C5/M01's shipped roster: autogyro_1 authors both talker "
            + "and constitution (7, 8) and an accent, and its resolved chances each read their own "
            + "authored rating on their own curve rather than the session's rating-5 fallback",
            RosterVoiceRatings));

        // BL-465/BL-461: mission callouts played from a point in the world, and a cue naming a VO
        // dialogue chain played nothing at all.
        into.Add(new TestHarness.Suite("mission-radio",
            "the mission radio queue over the first story mission's own callout vocabulary: every "
            + "wake/complete cue the mission authors is a queued radio line or a chain of them and "
            + "none is a positional definition, a chain speaks all of its lines in order, a second "
            + "cue queues behind the one speaking instead of cutting in, the whole queue drains "
            + "without starting a positional player, and STOP_QUEUED_SOUNDS drops a call that has "
            + "not begun",
            CampaignHudSuites.MissionRadioCallouts));

        // BL-476: a zeppelin's authored team reached nothing and its `deactivated` flag held only
        // the motion, so the airship a mission reveals partway through was in play from t=0.
        into.Add(new TestHarness.Suite("campaign-zeppelin-wakeup",
            "the hidden zeppelin and the authored team over the first story mission's BUILT world: "
            + "the three parser team names resolve to the ids the one shared team space mints, an "
            + "authored pool team beats the world fall-through while an unauthored one keeps it, a "
            + "dormant pool is no candidate at all, C3/M01's deactivated cargozep1 starts "
            + "uncollidable and out of both target pools while its sibling does not, and "
            + "OBJECTIVE39's own WAKEUP_ENEMIES puts it into the world through the real graph",
            CampaignZeppelinWakeup));

        // BL-499: the aircraft arm of the same directive, which the zeppelin arm above does not
        // stand in for. CM02's ace squad is the worked case.
        into.Add(new TestHarness.Suite("campaign-squad-wakeup",
            "CM02's ace squad over C3/M05's own BUILT world: britpeace_7/8/9 ship deactivated and "
            + "OBJECTIVE8 names exactly them in WAKEUP_ENEMIES, the three are inert and out of play "
            + "at mission start while the first Peacemaker squad flies, wiping group 1 completes "
            + "OBJECTIVE5 and its authored nap puts them into the world 15 s later through the real "
            + "graph, each woken block walks a patrol net, and britpeace_8's authored always-target "
            + "on the player role moves its live pick off a nearer candidate onto the human",
            CampaignSquadWakeSuites.CampaignSquadWakeup));

        // CM17 could not be finished: WARP_VEHICLE was a named no-op and a node-form TRAVELERS
        // could not see a roster aircraft, so all four of the mission's spot checks went
        // unanswered and its PRIMARY 1 never woke.
        into.Add(new TestHarness.Suite("campaign-blacke-search",
            "CM17's search for Blacke over C4/M02's own BUILT world: OBJECTIVE23 hides "
            + "bhatgyro_1 on one of four authored waypoints, each of the four search locations "
            + "wakes a 500 m spot check and naps the radio line that kills it two seconds later, "
            + "and nothing but a spot check wakes the mission's PRIMARY 1; the aircraft is a "
            + "roster block with no world node of its name, the warp places it while it is still "
            + "deactivated, a player half a radius away completes the check and wakes the "
            + "primary, and a player four radii away reads FALSE rather than unanswered",
            CampaignBlackeSearchSuites.CampaignBlackeSearch));

        // BL-500: the three SET_AI_* directives were named no-ops, so most of what CM02 does to
        // its own aircraft never happened and every one of them flew its roster block all mission.
        into.Add(new TestHarness.Suite("campaign-set-ai-net",
            "a mission re-commanding the aircraft it spawned, over C3/M05's own BUILT world: the "
            + "three Balmorals fly the M5Bombers their own blocks author until OBJECTIVE4 puts all "
            + "three on M5Bombrun, each capturing the route at the node nearest where it is rather "
            + "than restarting it, the net's own volumes reaching the aeroplane; the ace squad is "
            + "on M5Escort after the mission's own wake chain naps OBJECTIVE68 awake; an "
            + "appended objective drives SET_AI_TEAM and SET_AI_ATTACK_RADIUS, which no shipped "
            + "mission authors, including a name that is there for neither; and a bomber on that "
            + "1 m attack radius still carries the two turret gunners britbalmoral authors, "
            + "tracking and hitting a Fortune Hunter under its own shooter id while its pilot "
            + "stays out of combat, taking nothing on its own airframe and going quiet when it is "
            + "downed",
            CampaignSetAiSuites.CampaignSetAiNet));

        into.Add(new TestHarness.Suite("campaign-surface-vehicles",
            "a mode ship roster block or generator launch builds a hull, not an aircraft: CM08's "
            + "four patrolboat blocks in C1B's built world sit on the water at their authored "
            + "spots on their Patrolboat nets, deactivated and hidden until woken, count for DEDG "
            + "only once woken, start their wake emitters on their own pt_emitter nodes, drive "
            + "their nets under the scripted-path law at the taxi speed with their height pinned "
            + "to the water, land on the HUD's Enemy cycle and the gun aim assist's VehicleList "
            + "once woken, and die once through the chapter's destructible pool; CM12's eshipg31 "
            + "launch resolves a surface launch off Eshipg31_params, builds patrolboat_eg0 on the "
            + "host's first take-off point kilometres from the world origin, runs the path "
            + "westward at the taxi speed, never asks for an aircraft, and a realtime simulation "
            + "request advances it once through the shared session owner",
            SurfaceVehicleSuites.CampaignSurfaceVehicles));

        // BL-491: a campaign mission had three endings and none of them was the player dying, so
        // the aircraft could be lost and the mission flew on.
        into.Add(new TestHarness.Suite("campaign-player-death",
            "losing the player's aircraft over C3/M01's own BUILT world and shipped script, the "
            + "mission being one of the four that author no loss at all, so a Lost outcome there "
            + "can only be the death: a crash driven through the production death path ends the "
            + "mission lost where the wreck lands rather than on either wrap-up delay, hands the "
            + "player back to the cabin, and commits nothing to the persist log; --no-crash-loss "
            + "leaves the same crash flying, a mission nobody crashes in runs on, and an aircraft "
            + "the under-map backstop teleported ends nothing at all",
            CampaignPlayerDeathSuites.CampaignPlayerDeath));

        // BL-477: what stops a shot at the cargo zeppelin's slung tanks was inferred from the
        // geometry; this measures it. The original's own ray test reads no texture at all
        // (docs/org/weaponRay.md), so this is a census of OUR occluders, not a fidelity gate.
        into.Add(new TestHarness.Suite("alpha-cutout-ray-census",
            "the occluders standing between a weapon ray and C3/M01's cargo zeppelin: rays at "
            + "hydrogentank1's mesh centre from 36 azimuths at five elevations, each naming the "
            + "first collider's gamez node, over the mission's own world with the zeppelins placed "
            + "at their authored pose; then the splash half, a burst on the hull underside plate "
            + "g482 run through the production cover ray down to each of hydrogentank1..4",
            AlphaCutoutRayCensus));
        into.Add(new TestHarness.Suite("zeppelin-identity",
            "the owning-zeppelin identity a zone and a gun carry (BL-476): C5/M03's authored "
            + "cargozep* exclusion reaches a gasbag only through its hull's name and not through "
            + "the zone's own, and over C1/MP3's built world the record team fans onto every "
            + "emplacement standing on the ally hull while the unauthored sibling's guns keep "
            + "their TURRET default",
            ZeppelinIdentity));

        // BL-485: the shell advanced the reveal every frame and repainted the board only when a
        // discrete property moved, which a shot taken at a named second cannot see.
        into.Add(new TestHarness.Suite("campaign-briefing-repaint",
            "the briefing reveal reaching the screen, driven frame by frame through a real "
            + "LaunchMenu: the composed board the surface holds is compared against a board "
            + "composed from the page on each of 5400 driven frames, the reveal must compose the "
            + "screen afresh on far more than the handful of frames an objective count moves on, "
            + "and one frame's board is counted so the cost of repainting it is on the record",
            CampaignBriefingRepaint));

        // BL-489: the screenshot key reached the launcher only when no menu was up, so a menu
        // defect could be described but not shown.
        into.Add(new TestHarness.Suite("menu-screenshot-key",
            "the screenshot key on the menu screens: a real key event is pushed through the real "
            + "viewport with a LaunchMenu standing, once on the launchscreen and once on a "
            + "campaign board, and each press must leave one more file in the folder the flight "
            + "capture writes to",
            MenuScreenshotKey));
        into.Add(new TestHarness.Suite("perf-hud-layout",
            "where the frame-cost readout lands: the real Full tier is driven a frame, then its "
            + "label and its frame-time strip are both required to keep their right edge 8 px "
            + "inside the window and their left edge on screen, since a top-right control placed "
            + "by its LEFT edge walks its own width off the side",
            PerfHudSuites.PerfHudLayout));
        // BL-490: an objective wrapped to a second line drew over the next one's authored slot,
        // which only shows on a mission whose sentences are longer than the first's.
        into.Add(new TestHarness.Suite("campaign-briefing-note",
            "how the briefing's objectives note flows (BL-490): all 24 missions' reveals driven to "
            + "their end, each composed note measured with the font the screen writes it in, and "
            + "no entry allowed to start above the bottom of the one before it or to run past the "
            + "parchment's authored 240 px box",
            CampaignBriefingNote));
        // The CM02 capture lost the mission 20 s into the wing walk: the cutscene's 913 parked the
        // last bomber inert, its group read empty, and the wiped-out DEDG napped the instant loss
        // awake. The original's park sets a hold flag, never the dead byte DEDG reads.
        into.Add(new TestHarness.Suite("campaign-capture-group",
            "CM02's capture over C3/M05's own roster and objective graph, played through the "
            + "cutscene host from the approach row's trigger: two bombers down through the debug "
            + "kill complete the at-most-one DEDG and not the at-zero one, the wing walk's 913 "
            + "parks britbalmoral_1 and the group goes on counting it, the 967 swap hides that "
            + "bomber, stamps its group on the rebuilt rig and re-points wingman_4's escort onto "
            + "it, the group never reads below one from the trigger to 20 s past the handoff with "
            + "the at-zero DEDG incomplete throughout and no exception, and the rebuilt rig's "
            + "death still ends the mission lost",
            CaptureGroupSuites.CaptureGroup));
        into.Add(new TestHarness.Suite("campaign-capture-chutes",
            "the crew bailing out at the end of CM02's capture over C3/M05's BUILT world, the "
            + "capture played through the cutscene host from the approach row's trigger: the wing "
            + "walk's player definition calls the chute definition three times, each figure is "
            + "placed at the walk frame the call sites it on rather than at the archive's own "
            + "origin, three hang in the air at once, and each stays drawn for its whole drift",
            CaptureChuteSuites.CaptureChutes));
        into.Add(new TestHarness.Suite("landings-balmoral-dock",
            "CM02's own ending over C3/M05's BUILT world: the docking row flown in the Balmoral the "
            + "capture hands over, whose branch of the shared hookup is the only one that folds a "
            + "wing. The episode holds through that branch's authored two-second turn, both wings "
            + "reach the angle the fold authors, and the mission-completion code lands at its end "
            + "while the definition is still running, so it beats the objective watching that same "
            + "definition for EXECUTED and wins the mission on its own frame, once, with a later "
            + "completion code changing nothing; the world then stands still for the whole leaving "
            + "hold, taking no aeroplane step, no animation advance and no stick, so the film's "
            + "last live frame is the code's own and the session goes to the cabin two seconds "
            + "after it rather than on it",
            LandingApproachSuites.BalmoralDock));
        into.Add(new TestHarness.Suite("campaign-cutscene-ownership",
            "which definition an episode belongs to when the OBJECTIVE SCRIPT starts it rather than "
            + "a landings row, over C5/M02's BUILT world and its own objective chain: an ordinary "
            + "WAKE_ANIM left running claims no cutscene slot, the mission's ending is woken and "
            + "its first code comes from a callee, yet the episode belongs to the definition the "
            + "objective started and outlives that callee to the code the mission ends on",
            CutsceneOwnershipSuites.CutsceneOwnership));
        into.Add(new TestHarness.Suite("airframe-hull-coverage",
            "every player airframe's collision hulls measured against its own mesh: each hull "
            + "inside the box it replaces, the whole silhouette's triangle area covered by some hull "
            + "within the tolerance, the part names and their order the damage mapping relies on, "
            + "and the per-part box-to-hull volume the sweep no longer bridges",
            AirframeColliderSuites.AirframeHullCoverage));
        into.Add(new TestHarness.Suite("campaign-coop-human-field",
            "the human field over C3/M01's own shipped script and gate geometry: OBJECTIVE5's "
            + "authored 'player' TRAVELERS completes on the guest who reaches the point while the "
            + "scripted player stays 5 km out, and completes on nobody while the same aeroplane sits "
            + "there outside the field, an escorting block's 'player' leader still resolves to P1's "
            + "rig alone, and dzpath1's gate pair completes when P1 flies the green gate and the "
            + "guest the red one, which neither half completes by itself",
            CampaignHumanFieldSuites.CampaignCoopHumanField));
        into.Add(new TestHarness.Suite("campaign-coop-episode-owner",
            "the episode owner over CM02's own capture with two humans flying: the mission's swap "
            + "rebuilds the rig whose trigger claimed the episode, on the record the code names, "
            + "while the other human keeps the aeroplane it was flying; the swapped pilot keeps "
            + "their own shooter id and their one near-miss registration, the other pilot's is "
            + "untouched, and the same capture claimed by nobody swaps the scripted player",
            CoopEpisodeOwnerSuites.CampaignCoopEpisodeOwner));
        into.Add(new TestHarness.Suite("campaign-coop-approach-row",
            "the first story mission's own auto row flown by two humans: the guest inside the "
            + "sphere is offered the prompt in their own pane while the scripted player a kilometre "
            + "out is not and their held button starts nothing, the guest's press starts the row "
            + "and the episode belongs to the guest rather than to player 1, and a second human "
            + "standing in the same volume is not a second entry into it",
            LandingApproachSuites.CampaignCoopApproachRow));
        into.Add(new TestHarness.Suite("campaign-coop-dropoff",
            "the first story mission's own drop-off driven with two humans flying 2 km apart: the "
            + "episode owner rides the staged 'player' marker for every frame of the episode and "
            + "flies out of the re-placement the definition authors, while the other human is inert "
            + "at its own coordinates throughout and back in play there at the handoff; the same "
            + "definition claimed by nobody poses the scripted player and holds the guest instead",
            CoopDropoffSuites.CampaignCoopDropoff));
        into.Add(new TestHarness.Suite("campaign-coop-death",
            "the co-op loss rule over C3/M01, a mission whose script authors no loss at all: one "
            + "human down leaves the mission running and hands that pane a spectator camera with "
            + "the wreck pinned, while the other keeps flying; the LAST human's death is what ends "
            + "it lost once no wreck is still falling, whichever of them went first; "
            + "--no-crash-loss pins neither, and a solo death ends the mission on its only human",
            CoopDeathSuites.CampaignCoopDeath));
        into.Add(new TestHarness.Suite("campaign-coop-attempt",
            "what a co-op sortie writes to the seated profile: two human rigs each fire a real "
            + "cannon round at the other's aircraft, and only the seated pilot's reaches the recorded "
            + "attempt's Shots/Hits, WireScoredShooter gating on the scripted player and never a "
            + "guest; Money sums to 0 because no mission reward source feeds the director",
            CampaignCoopAttempt));
        into.Add(new TestHarness.Suite("campaign-coop-cutscene-fullscreen",
            "the window a cutscene plays in at 2, 3 and 4 panes: an episode gives pane 1 the whole "
            + "window, takes the other panes down and leaves EXACTLY ONE listener-enabled viewport "
            + "rather than none, and both exits — the definition ending and a player's skip — lay "
            + "the panes back out with a listener each; an intro whose first code lands before the "
            + "rig exists collapses as the rigs bind, and a skip names the player in their colour",
            CoopCutsceneSuites.CampaignCoopCutsceneFullscreen));
    }

    // ---- emitter lifetime is observable with no GPU ---------------------------------------------

    // Kills a refuel tank through a CountingEmitterFactory; only instance retirement ends its fire_n_smoke.
    // ⚠ Assert factory reach, emitter start, and retained-but-stopped state; "stopped" alone can mean teardown.
}
