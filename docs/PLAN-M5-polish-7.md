# Milestone 5 polish, run 7: the campaign plays clean

**ACTIVE PLAN** (written 2026-08-30). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

This run takes ten open items that a player meets while flying the delivered campaign: the
animation prerequisite the parse drops, the two cutscene handoffs that lose the aeroplane or the
ground under it, CM10's attack balloons and their marker, the campaign's own hangar screen, and the
three measured frame stalls. The selection criterion is "defects a campaign flight hits, plus the
three stalls that hitch it" (Decision 1): open `[Bug]`, `[Feature]` and `[Perf]` items that are
unblocked, are not `[Owed-playtest]`, and whose deliverable is a code change rather than an answer.
Out of scope by that rule: every `[Research]` item (`BL-515`, `BL-558`, `BL-637`), every
`[Owed-playtest]` and `[Tuning]` item, everything `[Blocked: ...]` (`BL-639`'s `CAP-47`,
`BL-314`'s `PT-45`), the plan-sized or user-deferred items (`BL-150`, `BL-446`, `BL-463`,
`BL-256`, `BL-299`), and the AI mode machine (`BL-523`, `BL-550`, `BL-565`, `BL-566`), which is one
subject and deserves its own run rather than a slot here.

**Every item was re-verified still-open against the record in this session**, by `git log --grep`
on each id: every hit is a filing, a minting, a trace or a cross-reference, and none is a landing.
That check also found `BL-621` (CM07's parachutist) already fixed and merged in `e6cc93b0`
while its entry still stands in `backlog.md`; it is excluded here and the stale entry is A1's
housekeeping. Two items were additionally checked against the code and say so in their Evidence
(A1, B11). The other eight carry a `<TODO: re-verify still-open against the code>`.
That `git log --grep` check was not enough for A1: the mechanism had already landed under two
commits that name `BL-574`/`BL-575` and `BL-599` rather than `BL-525`, so A1's section below
records a verification and a closure instead of the implementation it was written as.

**Decode stays open per item.** Each item's Approach names the data file, the decoded rule or the
instrument that settles it where one is known, and an implementer may take that lane instead of the
code-side lead whenever the lead runs out. A decoded rule beats a plausible fix here, as in every
prior run.

## Milestone goal

- A definition's authored `ACTIVATION_PREREQUISITE` decides whether its call answers, in both parse
  paths, so a docking objective completes on the docking that earned it and a hook swings once.
- A cutscene hands flight back the way it took it: the flown aeroplane is drawn from the episode's
  external camera, the cockpit the pilot chose is back on the handoff, and the aircraft is above
  the terrain rather than under it.
- CM10's attack balloons come apart when they are shot, and the objective marker sits on the
  balloon it names rather than on the water below it.
- The campaign's Plane Construction screen shows the profile's own aircraft and offers buying and
  selling, not Instant Action's build list and its verbs.
- The campaign's three measured stalls are attributed and answered: CM18's two spawn stalls, CM11's
  physics tick spikes, and the sustained rate drop CM09 and CM13 share.

**No item here judges a constant at the controls.** Every `[Tuning]` and `[Owed-playtest]` item is
deliberately out; those need the user flying rather than an implementer, and they are consolidated
in [`playtest.md`](../playtest.md). Where a fix wants a confirming flight (A2, B11, B12, C21, C22),
the code side lands first behind a headless assertion and the flight confirms it afterwards.

## Decisions (2026-08-30)

| # | Question | Decision |
|---|---|---|
| 1 | Which selection criterion | **Defects a campaign flight hits, plus the three stalls that hitch it.** M5 delivered the campaign, so its polish run is the walk itself: what a player sees go wrong, and where the frame stops. Seven slots to visible defects, three to the measured stalls. |
| 2 | Items needing a human at the controls | **Excluded.** All nineteen `[Owed-playtest]` items are out, so every item here can be finished and verified by an implementer without stopping on the user. |
| 3 | Items whose deliverable is an answer | **Excluded.** No `[Research]` item takes a slot. A polish run ends with the milestone visibly better, not with new documents. |
| 4 | Weight | **Routine, with the decode lane kept open on every item.** No up-front data survey; each item names what in the mission data, the decoded record or the instrument settles it. |
| 5 | Size and shape | **Ten items in five waves.** One wave per subject, so a wave can be taken whole by one agent, with the file contention between waves A and E written down rather than discovered. |

## ⚠ Read this before implementing anything

Two readings in this plan's neighbourhood are already dead. Do not re-derive them.

| # | The wrong claim | How it died |
|---|---|---|
| 1 | CM11's physics step costs about 39 ms per frame and its sim runs at half wall time | Godot's `physics_ms` monitor holds the WORST tick of the last wall second and refreshes at about 1 Hz, so it was read as a per-frame cost. The bracketed instrument puts the mean tick at 1.81 ms and the sim/wall ratio at 0.9999 over 306 wall seconds (`docs/verification.md` PERF-21, and `git log --grep=BL-562`). E42 is about the spikes alone. |
| 2 | Cutting ordinary allocation shrinks the settled GC pause | Halving the allocation rate moved the collection from gen0 every 13 s at 10 to 13 ms to gen1 every 31 to 36 s at 25 to 27 ms, leaving the pause per wall second unchanged at about 0.8 ms (`git log --grep=BL-536`). Relevant to E42 and E43 if either lands on the allocator: judge on pause per second, never on per-collection pause. |

The evidence quality is uneven, which is the price of picking across the campaign rather than down
one system. Budget by the grade, not by the item's apparent size.

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code or data** | A1, B11, C21, D31, E41 | Confirm the trace, then implement. |
| **Direction sound, mechanism a candidate** | A2, C22, E42 | The symptom is measured and a candidate is named. Ruling the candidate out is a result. |
| **Leads only, no mechanism yet** | B12, E43 | Budget for investigation; either may end in a disproof. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees, so never use it in a
worktree session here; use a local commit or a file copy.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets its record in the landing commit's message (`docs/HISTORY.md` is
  frozen — never append) and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — the dropped animation prerequisite

1. ☑ `BL-525` The node-active `ACTIVATION_PREREQUISITE` shape reaches both parse paths and gates the call
2. ◐ `BL-628` A campaign docking swings its hook once, not three times (not reproducible headless; owed at the controls)

### Wave B — the cutscene handoff

11. ☑ `BL-625` A cutscene entered from the cockpit frames the aeroplane, and gives the cockpit back
12. ☐ `BL-633` CM15's handoff leaves the player above the terrain, not under it

### Wave C — CM10's attack balloons

21. ☐ `BL-629` A shot attack balloon bursts and falls instead of hanging in the air
22. ☑ `BL-636` The Destroy Attack Balloon marker sits on its balloon, at any altitude

### Wave D — the campaign's own hangar

31. ☑ `BL-634` Plane Construction lists the profile's aircraft and offers buy and sell

### Wave E — the campaign's frame stalls

41. ☐ `BL-641` CM18's two spawn stalls are spread across frames
42. ☐ `BL-562` CM11's physics tick spikes are attributed and removed
43. ☐ `BL-612` CM09 and CM13 hold their rate for the whole mission

## Dependency and parallelism notes

A1 comes first in its wave and A2 depends on it: A2's most likely cause is the shape A1 parses, so
A2 begins by re-flying a docking on A1's build and is closed as fixed, or re-opened as a separate
fault, from what that shows. B11 and B12 both edit `Session/CutsceneController.cs` and run as a
chain, never in parallel worktrees. C21 and C22 are the same mission and one sortie verifies both,
but they touch different systems (the animation runtime and the marker layer) and can run in
parallel. D31 is confined to `UI/` and contends with nothing.

**File contention: A1 and E42 both edit `Mech3/AnimRuntime.cs`.** A1 closed without touching that
file, so the queue is clear for E42, whose named candidate is `NameResolver.ClearFindCache` and its
callers, the `IndexStage` / `IndexSpawnedCopy` / `IndexRebasedStage` / `IndexPooledCopy` paths.
C21 may also reach `AnimRuntime`'s `ObjectMotion` dispatch; if the diagnosis takes it there, the two
still run one after the other rather than in parallel worktrees.

E41, E42 and E43 are one measuring session's worth of work and share the instrument setup
(`--det`, `--perf`, `HitchSidecar`'s attribution). Taking the wave whole is cheaper than three
separate arm-and-measure cycles, but the three fixes are independent once each is attributed.

---

# Wave A — the dropped animation prerequisite

## A1 ☑ `BL-525` The node-active `ACTIVATION_PREREQUISITE` shape reaches both parse paths and gates the call

**Outcome: the mechanism was already on `main`. This item is a verification and a closure, and it
landed no engine change.** The section as written asserted that both parse paths drop the
node-active shape and that nothing enforces it. That is false, and all three pieces are present:

- `Mech3/AnimDefs.cs`'s `ACTIVATION_PREREQUISITE` case reads `REQUIRED` and `OPTIONS` carrying
  `OBJECT_ACTIVE_LIST` / `OBJECT_INACTIVE_LIST` node paths into `AnimDefinition.PrereqNodes` as
  `AnimNodePrereq(path, active, required)`.
- `Mech3/CompiledAnim.cs` reads the `Parent` run closed by an `Object` leaf into the same list,
  taking the required state from bit 0 of `active_raw`.
- `Mech3/AnimRuntime.cs`'s `Start` refuses a definition whose REQUIRED entries are unmet
  (`NodePrerequisitesMet`), in silence, counted as `Start(prerequisite unmet)`.

The parses and the gate landed in `4a135e13` under `BL-574`/`BL-575`, and `e3d5de68` corrected the
compiled state word under `BL-599`. Neither commit names `BL-525`, which is why a `git log --grep`
on the id found only filings. The durable record is already written: `docs/architecture.md`'s
`CompiledAnim.cs` and `AnimRuntime.cs` entries and `docs/formats/anim-definitions.md`'s
`ACTIVATION_PREREQUISITE` row all state the two parses and the `Start` gate, so the only doc edit
this item needed was replacing that row's estimated shape count with the measured one below.

**What the data authors.** `extracted/C1C/M01/zrdr/wv_tailhook.zrd.json` carries four definitions
with the node form. `wv_drop_copilot` requires `[wv_tailhook, dropoff_node]` ACTIVE;
`wv_pickup_copilot`, `wv_pickup_fassenb` and `wv_setup_fassenb` each require
`[wv_tailhook, pickup_node]` ACTIVE. `wv_initiate_hookup` calls `wv_drop_copilot`,
`wv_pickup_fassenb` and `wv_pickup_copilot` in that order with no condition of its own, so the
prerequisite is the whole fork. The mission's objectives open one node each: OBJECTIVE11's
`WAKE_ANIM` is `activate_dropoff_node` (the first hook) and OBJECTIVE15's is `activate_pickup_node`
(the second). The compiled copies agree, `pickup_cpilot-wv_drop_copilot.json` carrying
`Parent wv_tailhook` + `Object dropoff_node` with `active_raw: 1`.

**What the built world does.** `landings-docking-hold` drives that docking over C1C/M01's built
world and now reads the fork. The world build leaves both nodes INACTIVE; the mission's own
objective chain turns `dropoff_node` on while arming the row; and at the instant
`wv_initiate_hookup` dispatches its legs the states read `wv_tailhook/dropoff_node=ACTIVE`,
`wv_tailhook/pickup_node=INACTIVE`. `wv_drop_copilot` plays once, `wv_pickup_fassenb` and
`wv_pickup_copilot` play zero times, and `wv_pickup_copilot` therefore does not reach `EXECUTED` on
the first docking, which is the condition OBJECTIVE15 wakes on. Both paths resolve to nodes this
world built, so the gate is not passing vacuously; that unresolved-path case, which
`NodePrerequisitesMet` treats as met, was the surviving gap worth checking and it does not apply
here. The suite's existing no-teleport-after-release check covers the other half of the reported
symptom.

**Blast radius, as a number.** Across every `mis_anim` and `cam_anim` archive, 437 definition files
carry a node-state prerequisite, which is 112 distinct root-animation definitions; those hold 1028
node entries, 784 of them REQUIRED and therefore enforced at `Start`. On the reader side 26 `zrdr`
files carry the `OBJECT_ACTIVE_LIST` / `OBJECT_INACTIVE_LIST` spelling, 52 keys in all, most of them
in `C2/M01/goosepath.zrd` (12), `C1C/M01/wv_tailhook.zrd` (4) and `C1/M02/hangar_drop.zrd` (4). The
entry's "roughly fifty" was an undercount.

**What landed.** `backlog.md` loses `BL-525` and, as this item's housekeeping, `BL-621`, whose fix
merged in `e6cc93b0` and whose entry was never struck. `BL-628`'s and `BL-632`'s cross-references to
the two deleted ids are rewritten, and the Gemini gasbag item's "the other prerequisite form, which
is dropped" now says it is parsed and enforced. `Testing/LandingApproachSuites.cs` gains the
assertions above.

**Verify.** All eleven `landings-` suites PASS in 53.7s, engine errors clean, with
`landings-docking-hold`'s new `fork:` lines reading `wv_drop_copilot ... built=True, at dispatch
ACTIVE, played 1x` and both pickup legs `built=True, at dispatch INACTIVE, played 0x`. The nine
suites the section named, `anim-activation-prerequisite`, `campaign-objectives`,
`campaign-objectives-hud`, `campaign-cutscene`, `campaign-cutscene-skip`,
`campaign-cutscene-ownership`, `campaign-zeppelins`, `campaign-airframe-swap` and
`campaign-hangar-handover`, PASS in 41.7s, errors clean.

Red before green was taken on the gate itself, by dropping the `return` from `Start`'s
`NodePrerequisitesMet` refusal and leaving the counter. `landings-docking-hold` then FAILs on both
new checks:

```
and a leg runs exactly when its own required state holds at the dispatch, so the docking's wrong leg stays off expected=0 actual=2
and no definition 'wv_initiate_hookup' reaches plays twice in one episode, the hook legs included (rem_pas x3, deactivate_pickup_node x2, deactivate_dropoff_node x2) expected=0 actual=3
```

That run starts `wv_pickup_fassenb` and `wv_pickup_copilot` at t=7.32 with `pickup_node` INACTIVE
and pushes the handoff from t=9.93 to t=15.95, which is the reported defect reproduced on demand.
Note what else it produces: with the gate off, `rem_pas` plays three times in one docking. That is
A2's shape, from the mechanism A2 named, and it is off on today's build.

**Verified.** The complete `.\RunTests.ps1` on the merged wave-1 tree: build clean, units 2683
passed of 2683, 192 engine suites passed with engine errors clean, and 18 golden shots
hash-identical. Exit 0 in 187.3 s, the engine stage over its 100 s budget at 125.3 s (awareness
only).

## A2 ◐ `BL-628` A campaign docking swings its hook once, not three times

**Goal.** The opening camera shot of a docking cutscene shows one hook swing. The hook still engages
correctly, which it does today.

**Outcome: the candidate mechanism is ruled out and the repeat does not reproduce headless. The item
stays open, owed at the controls.** No code changed for it.

**The count, measured.** The item's own discriminator, one call site playing three times against
three call sites playing once, is now a headless number in three shipped docking episodes, each
driven over its own built world with every start counted by name:

| suite | mission | hook definitions started | repeats in the whole episode |
|---|---|---|---|
| `landings-hookup-airframe` | CM01 (C3/M01), Balmoral | `pz_deploy_hook`, `hook_impact`, `player_extend_hook`, `bal_hook_extend`, once each | `random_prop` x12, `wing_lights_blink` x2 |
| `landings-hookup-airframe` | CM01, Pirate Fighter | the same four with `pirate_hook_extend`, once each | `random_prop` x12 |
| `landings-balmoral-dock` | CM02 (C3/M05) | the same four with `bal_hook_extend`, once each | `random_prop` x12 |
| `landings-docking-hold` | CM06 (C1C/M01) | `player_extend_hook`, `blood_hook_extend`, once each | `wing_lights_blink` x2 |

`random_prop` is the ambient prop dice and `wing_lights_blink` is a blinker loop; neither is in the
docking definition's call closure, and no definition that closure reaches starts twice.

**The call-site census.** Every archive that names `player_extend_hook` names it once: the
definition itself in each chapter's `cam_anim`, one call in `player-hooked_to_klondike` per mission
and one in `wv_tailhook-wv_initiate_hookup`. So the shared docking and the Workers' Voyage docking
each reach the hook leg from a single call site, and the definition itself is eleven `If NodeActive
n` branches each closed by `StopSequence`, so one airframe branch answers. `LandingApproachRuntime`
cannot re-fire a row underneath a playing episode either: its `Tick` returns while a cutscene is
playing, and a manual row latches until its condition stops passing.

**Why A1 does not close it.** A1's mechanism is enforced on today's build, so the fault A1 names
cannot be producing this. The disproof is two-sided: with A1's gate deliberately switched off,
`landings-docking-hold` does show a definition playing three times in one docking (`rem_pas` x3),
which is the shape A2 describes; with the gate on, as it ships, nothing in the closure repeats.

**What is left.** The count is only observable at the controls. Fly a campaign docking with
`.\RunGame.ps1 --campaign=<profile>:<slot>` and watch the opening shot alone, then read
`.scratch/logs/fly-*.log` for the episode's starts. CM11 (C2/M02)'s closing autodock is the reported
case; CM01 (C3/M01) is the earliest. The question the sortie has to answer is which of the four hook
definitions a docking plays is the one seen three times, because a viewer counting swings is not
counting definitions, and the headless count says the definitions each run once.

**Model recommendation.** medium, unchanged. The remaining work is one sortie and whatever it names.

**Verify.** `landings-docking-hold`, `landings-hookup-airframe`, `landings-balmoral-dock` and
`campaign-cutscene` green (with the eight other `landings-` suites and the campaign-cutscene family,
20 suites in all). The play-count assertions above are permanent, so a regression that introduces
the repeat fails a suite rather than waiting for a sortie. At the controls, still owed: any campaign
docking watched through its opening shot alone, one hook swing, and the hook still engages.
**Verified.** The complete `.\RunTests.ps1` on the merged wave-1 tree: build clean, units 2683
passed of 2683, 192 engine suites passed with engine errors clean, and 18 golden shots
hash-identical. Exit 0 in 187.3 s, the engine stage over its 100 s budget at 125.3 s (awareness
only).

**⚠ Traps.** Do not silence this by latching "already played" on the runtime. A repeat that a
definition authors is data, and a latch would hide the same defect wherever else it happens. The
player-visible hook engagement is correct today and must stay correct. Do not re-open this against
the node-state prerequisite; it is parsed and enforced, and the suites above assert the count.

---

# Wave B — the cutscene handoff

## B11 ☑ `BL-625` A cutscene entered from the cockpit frames the aeroplane, and gives the cockpit back

**What landed.** The presentation code writes the flown aircraft's own visibility at both edges.
`FlightController.SetViewedFromOutside(bool)` is the new seam: on it calls the crash cut's own
`LeaveFirstPerson`, so the airframe is drawn and the interior pass is deactivated; off it re-applies
`CockpitVisibility.Rules` for the view the pilot still has SELECTED, re-syncs the pass and restores
`_panelShown`. `CutsceneController.ApplyPresentation` calls it for every human beside `CameraOwned`,
so both exits (the definition ending and a player's skip) go through it, since both reach the handoff
code through `Restore`. A crashed pilot is skipped on the restore leg: `_Process` writes nothing to
the camera while crashed, so a restore there would hold the interior over the crash camera until the
respawn. No view mode is written and no ground clamp of any kind was added.

**Evidence, re-verified against today's code.** The trace held. `ApplyPresentation` set `CameraOwned`
and nothing else about visibility; the `Cockpit?.Apply` / `CockpitPass?.Sync` pair lives only in the
final arm of `FlightController._Process`, which `CameraOwned` short-circuits, so the last flying
frame's `Shown(Interior: true, Body: false, ...)` stood for the whole episode. `git log --grep=BL-625`
returned only the two filing commits, no landing. The plan's line numbers were stale again by the
time the item ran, which is why the members were re-located by name rather than by line.

**Red before green, in two stages.** The assertion was added first and the whole item seen red:
`campaign-cutscene` FAIL with `so the code draws the airframe itself, and the episode's camera frames
an aeroplane rather than nothing (the definition ending leg)` and its interior-pass twin, on both the
definition-ending and the skip leg. Adding only the presenting half turned those green and left the
restore half red: `…and puts the cockpit seat back in the cockpit it chose, rather than leaving it
outside its own aeroplane` and `…with its interior pass drawing again`. Adding the restore turned the
suite green. Each half was therefore seen able to fail on its own.

**Verify.** `campaign-cutscene` carries the new check (`PresentingFramesTheAirframe`): two real
human seats built with `cockpitInterior: true` and their own `CockpitOverlay`, one pinned to
`PilotViewMode.Cockpit` and one to `Chase`, driven through a whole episode twice, once ended by the
definition and once by a skip. It asserts the entering state, then the airframe drawn with the
interior and its pass off while presenting, then both back on the hand-back, with the chase seat
untouched throughout and neither seat's selected view moved.

Targeted results, all on this worktree with `CSVM_DATA_ROOT` set:

- `-Filter cutscene`: 5 passed, 0 failed (`campaign-cutscene`, `cutscene-letterbox`,
  `campaign-cutscene-skip`, `campaign-cutscene-ownership`, `campaign-coop-cutscene-fullscreen`),
  engine errors clean.
- `-Filter cockpit`: 3 passed, 0 failed (`cockpit-interior`, `cockpit-overlay-pass`,
  `cockpit-panel-staging`).
- `-Filter death`: 6 passed, 0 failed, which is the crash cut's own coverage past the new guard.
- `-Filter intro`: 2 passed. `-Filter coop`: 7 passed. `-Filter landings`: 11 passed.
  `-Filter swap`: 2 passed.
- `.\RunTests.ps1 -Quick`: units 241 passed of 241, engine 13 passed of 13, engine errors clean.

**Verified.** The complete `.\RunTests.ps1` on the merged wave-1 tree: build clean, units 2683
passed of 2683, 192 engine suites passed with engine errors clean, and 18 golden shots
hash-identical. Exit 0 in 187.3 s, the engine stage over its 100 s budget at 125.3 s (awareness
only).

**Owed at the controls.** The flight that confirms the picture is not answered by a headless
assertion and is owed as its own item: CM01 (C3/M01)'s docking episode, `.\RunGame.ps1
--campaign=<profile>:0`, cycle to the cockpit view (F8) before the docking fires and watch it
through, then repeat with the skip key, which takes the other restore path.

**⚠ Traps that still bind.** Do not write `ViewMode = Chase` for the duration: `MirrorCamera` owns
every rig camera's pose outright while presenting, so the view mode buys nothing there, and the mode
left behind is what the hand-back reads. The interior lives outside `PlaneModel` under `CockpitPass`,
so un-hiding the airframe does not take the panel off screen; the pass has to be deactivated and
re-synced by name. An intro cutscene drawing no aircraft is a different thing: those definitions
deactivate the `player` node themselves in `ApplyOutOfFlight`.

## B12 ☐ `BL-633` CM15's handoff leaves the player above the terrain, not under it

**Goal.** When CM15 (C2/M05)'s cutscene hands control back, the aircraft is at the pose the episode
ended on and above the ground, and the player can fly on without dying.

**Evidence (confidence: lead-only).** Reported at the controls. The handoff leaves the aircraft below
the ground surface rather than at the pose the episode ended on, and since terrain colliders are
single-sided a crossing from below is silent, so dying is the only way out. No mechanism is
established. The nearest known shape is the marker-relative position that survived an episode's own
reparenting, fixed for a different episode in `BL-610` (`git log --grep=BL-610`), so the first
question is whether this episode takes that path. `<TODO: re-verify still-open against the code>`

**Approach.** Log the pose the episode restores against the pose it ended on, over CM15's episode,
before changing anything. Read `Session/CutsceneController.cs`'s restore path (`ApplyPresentation(false)`
and the reparent undo around `_cameraHome`) against `BL-610`'s fix and establish whether the same
reparenting is in play. Take B11 first: it edits the same restore path, and landing them in one file
one after the other avoids a merge against yourself.

**Model recommendation.** high. The diagnosis is open, the instrument has to be chosen, and the
restore path is shared with B11 and with every other episode in the campaign.

**Verify.** A headless assertion over CM15's episode that the restored pose matches the pose the
episode ended on, and that the aircraft's Y is above the terrain height at that XZ. Seen red before
green. `campaign-cutscene-ownership` and `campaign-cutscene` green. At the controls, CM15 flown
through the episode, then flown on.

**⚠ Traps.** Do not add a ground clamp to the handoff. It would hide a wrong restore everywhere else
it happens, and it would fight an episode that legitimately ends below a surface. The single-sided
colliders that make the symptom unrecoverable are a separate property, recorded from the air side in
`BL-566`, and are not this item's to change.

---

# Wave C — CM10's attack balloons

## C21 ☐ `BL-629` A shot attack balloon bursts and falls instead of hanging in the air

**Goal.** A shot-down attack balloon in CM10 (C1/M05) comes apart and falls, as its definition
authors, rather than hanging in the air as a destroyed model.

**Evidence (confidence: traced).** Reported at the controls. The parts of the chain that work are the
weapon hit, the model swap to `destroyed_balloon`, the death of the balloon's `bbtur` turret and the
lifeboat's drop, which falls, splashes and drives its net. The authored death is more than a swap:
`extracted/C1/M05/mis_anim/lifesaver11-lifefall11-lifeballoon.json` (`activation: WeaponHit`,
`health: 60.0`) carries eight `ObjectMotion` events, of which only the first drives `lifeboat` down
under `gravity: -10`; the rest move `b_dbase` and `b_part1` through `b_part6`, the balloon's own
bursting pieces, alongside seven `ObjectOpacityFromTo` fades and fourteen puffer states. The
balloon-side half of one definition is not taking effect while the boat-side half is. Nine balloons
author the same five definitions, so the answer applies nine times.
`<TODO: re-verify still-open against the code>`

**Approach.** Run that definition headless in the anim lab on the real C1/M05 world and report which
of its eight motions start, then follow the first one that does not. Establish whether the balloon
pieces resolve as nodes at all before asking why they do not move; a name that resolves to nothing is
the cheapest of the candidate answers and `BL-618` records that shape.
`<TODO: name the anim-lab invocation for a single mis_anim definition on a built world, from
docs/verification.md, so the measurement is reproducible.>`

**Model recommendation.** high. The diagnosis runs through the animation runtime's dispatch and the
name resolver, and the fix has to hold for all nine balloons and five definitions rather than for the
one that was watched.

**Verify.** A new suite over C1/M05 that kills one balloon and asserts the `b_part1` to `b_part6`
pieces move and fade while the lifeboat still falls and splashes, seen red before green with the fix
reverted. `campaign-objective-markers` and `campaign-objectives` green (the mission's own progress
must not move), and the complete `.\RunTests.ps1` before landing. At the controls, CM10: shoot one
balloon and watch what is left in the air. `<TODO: name the new suite.>`

**⚠ Traps.** Do not delete the balloon on death as a shortcut. The pieces are authored to fall and
fade, and a despawn would remove the wreck the original shows falling. `set_bbtur_off` and the model
swap are already doing their jobs, so neither is the suspect. If the fix reaches `AnimRuntime`, check
the file-contention queue in the dependency notes first.

## C22 ☑ `BL-636` The Destroy Attack Balloon marker sits on its balloon, at any altitude

**Goal.** CM10's Destroy Attack Balloon marker stands on the balloon it names, at whatever altitude
the balloon is at, and retires with the balloon rather than following the lifeboat that drops out of
it.

**Still open, confirmed against the code.** `ObjectiveSites.SiteAnchor` kept `GlobalPosition` for any
node standing off the world origin, and a probe over the built C1/M05 world put the offered
`lifesaver11` site at `(-10075, 0, -2375)`, the water, with the balloon at `(-10075, 16.4, -2380.1)`
and the lifeboat's own geometry topping out at y 4.1. `BL-602`'s origin-standing branch never fired
here, so nothing on main had already answered this.

**What the mechanism turned out to be.** The plan's candidate ("a marker anchored on the group") is
right, and the reason is the group node's own origin rather than its mesh centre. Each
`lifesaverNM` is a direct child of `world1` carrying the no-transform `"Initial"`, flown to the water
by the `ObjectMotionSiScript` on the inner `lifesaver` node, with `lifeballoon` hung
`(0, 16.393, -5.07)` above that origin and `lifeboat` sitting on it. Reading the group's position
therefore reads the boat's waterline exactly.

**The decoded rule replaced the heuristic.** `crimson.exe` publishes a mission structure's targeting
position from vtable `0x00608848` slot 0, which is `LEA EAX, [ECX + 0x74]` at `0x004a36e0`, a cached
vector. `FUN_004a2570` fills `+0x74` from `FUN_004cf2c0(node)`, which reads the node's active
bounding box (`FUN_004cd960`, the six floats at `node + 0x70`) and takes its midpoint
(`FUN_004d8b10`), in the frame `FUN_004cef20` accumulates up the parent chain; `FUN_004a2730`
recomputes it every frame for a structure whose `+0x8f` moving flag is set. So the original marks a
site at **the centre of its node's bounding box**, never at the node's origin. `SiteAnchor` is now
that one rule, and the `HasOwnMesh` short-circuit, the origin gate and `BL-602`'s `door`-named leaf
rule are gone with it. The decode is written up in `docs/org/targeting.md` ("Where a mission
structure is") with its four addresses added to that page's function map.

**The port reproduces the authored bbox exactly.** The world AABB of `lifesaver11`'s built meshes
centres at `(-10075, 11.667011, -2376.3086)`; the gamez node's authored `child_bbox` (its
`active_bbox` is `Child`) spans y −1.4109578 to 24.744844, x ±13.761361 and z −21.070253 to
18.45244, whose centre is `(0, 11.6669, −1.3089)` off a group origin at `(-10075, 0, -2375)`. The two
agree to 1e-5, which is the check that the built subtree stands in for the original's active bbox.

**What moved elsewhere.** C2/M03's `sghangar` improves: its anchor goes from the door-leaf pair at
`z −5623.9`, 129 m from `dz1`, to `z −5496.6`, **2.5 m** from `dz1`, so `campaign-race-chain`'s 150 m
assertion holds far more comfortably than before. C1/M04's `piratezep/rock_zeppelin` moves 64 m onto
the hull's own geometry, and `campaign-objective-target-path`'s exact-equality assertion was rewritten
to the anchor plus a new check that the site stays more than 500 m from every other `rock_zeppelin`,
which is what that suite was really discriminating. C1/M05's `rch_hull` moves 22.6 m along the
hospital ship and 13.3 m up; C1/M04's `ap_transmitter` moves 22 m up onto its tower.

**The walk costs, and where the cost went.** The union is taken over the built subtree once per site
per pane per frame, so it was measured rather than assumed (1000 calls on the built world, stopwatch
in the suite, instrumentation removed afterwards). A 380-mesh site (`piratezep/rock_zeppelin`, the
worst shipped case) cost **1.9 ms** per call with the obvious `GetChildren()` recursion, because that
call allocates a Godot array per node; walking by `GetChildCount`/`GetChild` instead costs **0.50 ms**
for the same result, and `lifesaver11`'s 43 meshes cost about 0.024 ms. The indexed walk is what
landed. A cached mesh list (built once, live transforms merged per frame) measured 0.26 ms on the
same 380 meshes, so there is another 1.9x available if a pane count ever makes it matter.

**Files.** `CSVM/src/Session/ObjectiveSites.cs` (the rule), `CSVM/src/Testing/CampaignMarkerSuites.cs`
(the new suite plus the rewritten path assertion), `CSVM/src/Testing/SuiteCatalog.cs`,
`CSVM.Tests/SuiteCatalogTests.cs` (191 → 192), `analysis/engine-suite-weights.json`,
`docs/architecture.md`, `docs/org/targeting.md`.

**Verify.** New suite `campaign-balloon-marker`: the shipped table describes nine attack-balloon
sites and nine objectives each retire their own site on its own `healthy_balloon`; over the built
world the marker stands clear of the lifeboat, flies with the whole assembly and rises when the
balloon alone rises, and retires with the balloon while the boat is still afloat. Seen RED first with
the old `SiteAnchor` in place, `the marker stands clear of the lifeboat below it (0.0 m against the
boat's 4.1 m)`, the artifact reading `marker y 0.0 -> assembly climbed 0.0 -> balloon alone climbed
0.0`; GREEN after, `marker y 11.7 -> assembly climbed 311.7 -> balloon alone climbed 161.7`.
Targeted runs on this tree, `CSVM_DATA_ROOT` set and the suite count printed non-zero each time:
`-Filter campaign` **40 passed, 0 failed**, engine errors clean (`campaign-race-chain`,
`campaign-objective-markers`, `campaign-objective-target-path`, `campaign-objective-labels`,
`campaign-objectives` and `campaign-objectives-hud` among them); `-Filter target` **8 passed, 0
failed**; `-Suite hostile-marker-hud` **1 passed**; `-UnitFilter SuiteCatalogTests` **6 passed**;
`-Quick` **241 units and 13 engine suites passed, 0 failed**, engine errors clean.
`.\CheckCommentCaps.ps1 -Summary`: all comment blocks within cap.
**Verified.** The complete `.\RunTests.ps1` on the merged wave-1 tree: build clean, units 2683
passed of 2683, 192 engine suites passed with engine errors clean, and 18 golden shots
hash-identical. Exit 0 in 187.3 s, the engine stage over its 100 s budget at 125.3 s (awareness
only).

**Follow-up proposed, not minted.** A site's anchor is still recomputed once per pane, so a four-pane
co-op CM10 with nine balloons alive pays about 0.86 ms a frame and a four-pane C1/M04 about 2.0 ms.
Computing each site's anchor once per frame and handing every pane the same value, with the cached
mesh list above, would take both under 0.3 ms. Worth its own `[Perf]` entry alongside Wave E.

**Still owed at the controls.** CM10 with a wave in frame at two different heights, to judge whether
the assembly's bbox centre (11.7 m up, among the ropes under the envelope) reads as a marker on the
balloon. It is where the original puts it, so a different answer would be a deliberate divergence
rather than a bug. The same sortie is already owed on `BL-629`, C21.

**⚠ Traps.** Do not offset the marker upward by a constant, and do not anchor on the balloon node
itself: the decode says bounding-box centre, and a rule that follows one authored child would put a
zeppelin's marker on one of its fourteen engines. `campaign-objective-target-path` and
`campaign-race-chain` both assert world positions, so any further change to `SiteAnchor` has to be
checked against them.

---

# Wave D — the campaign's own hangar

## D31 ☑ `BL-634` Plane Construction lists the profile's aircraft and offers buy and sell

**Landed.** Ownership now separates the campaign hangar from Instant Action's, over the one
`user://Planes/` build store both doors still write into.

**Re-verified still open against the code first.** The two cited line numbers had drifted, the
mechanism had not. `Saved = store.List()` was exactly `UI/HangarFlow.cs:150` as filed;
`HangarCampaignContext`'s `Purchase`/`Sell`/`CanSell`/`SellPrice` were at `:98-122` as filed; the
campaign door is `LaunchMenu.OpenCampaignHangar` at `:1967-1981`, not the filed `:1866-1869`, and it
does hand the flow `CustomPlaneStore.UserPlanes()` unfiltered. The money half (`Commit` debiting
through `Purchase`, `DeleteSaved` crediting through `Sell`, the special-plane and two-plane-floor
refusals) had already landed with PLAN-hangar's B13, so the open half was the listing and the verbs.

- **The roster.** `HangarFlow.ReadRoster` puts `HangarCampaignContext.OwnedBuilds()` in `Saved` over
  a campaign flow and the whole directory over the two wallet-free doors. `OwnedBuilds` walks
  `Profile.Planes` in the ownership list's own order and resolves each record to its stored build,
  else the reward aircraft's own award template (`CampaignProgression.BuildForOwned`, the order
  `LaunchMenu`'s launch path already resolves in), else the campaign's starting-Devastator spec. The
  two seeded starters are never hangar-built, which is why that last arm exists; `SellPrice` now
  shares the same resolution instead of carrying its own copy of it.
- **The verbs.** `HangarPlaneSelectionPage` reads as the original's INVENTORY (langui 1257) over a
  campaign flow: a `Buy a New Plane` row detailing the wallet (1149), one row per owned plane with
  its airframe short name (3020 + id) and value (1258), and a trailing `Sell a plane` opening the
  same two-stage confirm list, whose rows read `Sell <name>` and carry either the price or the
  refusal a press would meet. Instant Action's rows are untouched.
- **An owned row is inert over a campaign flow.** The decoded economy has no partial upgrade: a
  build is paid in full and a sale credits in full, so editing in place would charge again and
  strand the old plane's value. The sell stage is what acts on an owned plane.
- **Two holes the filter would otherwise have opened.** `IsNameTaken` spans the whole build
  directory rather than the visible roster, so a campaign build cannot silently overwrite an Instant
  Action plane of the same name (`HangarNamePage`'s roller and its overwrite warning both ask
  through it). `Purchase` updates an existing ownership record rather than adding a second one for a
  name already owned, since the commit writes one file per name and a duplicate record would let one
  sale remove two.
- **Fixed in passing:** `DeleteSaved`'s reward-aircraft refusal read langui 704 raw, so with the
  real table loaded it reached the pilot as `This %1!s!, %2!s!, cannot be sold.`. It is composed now
  through `CannotSellText`, with the airframe short name and the plane name filled in.
- **Decode recorded.** [`docs/org/hangar.md`](org/hangar.md) gained "The inventory screen's own
  strings": the screen's `langui` symbols (1257, 1258, 1256, 1003, 1139, 1149, 204, 702, 703), which
  of them carry arguments and are unusable raw (700 and 704, with 700's inline bold markup), and
  that Export is a campaign verb Instant Action does not have.

**Verify.** Red before green, seen by reverting the three production branches in place
(`Saved = stored`, the campaign flag in `RowText`, the inert-row arm in `Accept`): 8 of the 17
`HangarCampaignContextTests` failed, including
`Assert.Equal() Failure: Strings differ / Expected: "Sell a plane" / Actual: "Delete a saved plane"`
and `AFreshProfileListsExactlyItsTwoStarters` with an empty actual collection. `Purchase`'s
duplicate guard was reverted separately, failing
`RebuildingAnOwnedNameKeepsOneOwnershipRecord` with `Expected: 2 / Actual: 3`.

Green: `HangarCampaignContextTests` 17/17 (the six behaviours the item names, plus the inert row,
the cross-mode name collision and the duplicate-ownership guard); the full unit tier 2683/2683;
`CustomPlaneStoreTests` 16/16 including a new `PathFor_KeepsEveryNameInsideTheStore`; engine
`-Filter hangar` 3/3 (`hangar-door-wake`, `landings-hangar-drop-gate`,
`campaign-hangar-handover`), errors clean; engine `-Suite campaign-persistence` 1/1, errors clean.
`CheckCommentCaps.ps1` clean over every changed file.

**Verified.** The complete `.\RunTests.ps1` on the merged wave-1 tree: build clean, units 2683
passed of 2683, 192 engine suites passed with engine errors clean, and 18 golden shots
hash-identical. Exit 0 in 187.3 s, the engine stage over its 100 s budget at 125.3 s (awareness
only).

**⚠ Traps.** Do not give the campaign its own build directory to get the separation. The two starters
a fresh profile is seeded with are never hangar-built and have no entry there at all, which is why
`SellPrice` falls back to the campaign's own Devastator spec; a per-mode store would strand them. The
delete row is not the sell row: deleting a build must not credit the wallet.

**Still open, and deliberately not taken here.** The screen has not been seen at the controls, so a
confirming flight is owed. The decoded slot cap (25 records, the free-slot finder reserving six,
langui 204) is not enforced on a campaign purchase. Instant Action's list still shows campaign
builds with no export step, which is the mirror image of what this item fixed and is what langui
702 and 1139 exist for.

---

# Wave E — the campaign's frame stalls

## E41 ☐ `BL-641` CM18's two spawn stalls are spread across frames

**Goal.** CM18 (C4/M03) runs its early flight without the two roughly 300 ms frame stalls, at one
human and at four.

**Evidence (confidence: traced).** `analysis/campaign-coop-4p-perf/FINDINGS.md` (D33): `--det`'s
deterministic clock put the stall at sim frame 241 (about 320 ms against a 45.7 ms threshold) and
frame 482 (about 290 to 300 ms) in every one of the four 1P/4P by cockpit/external launches measured.
`HitchSidecar`'s own attribution assigned essentially the whole frame to one `ai_spawn` sample
(`attributed_ms` within 20 ms of `frame_ms` both times). Identical across player counts, so this is a
single mission-script or generator spawn cost on the main thread, not something splitscreen's pane
count multiplies. `<TODO: re-verify still-open against the code>`

**Approach.** Find what CM18's roster or generator script does at those two sim instants (a
`WAKEUP_GENERATOR`, a roster credit or a similar bulk spawn) and establish whether the work can be
spread across frames. D33 measured it and deliberately did not fix it, so the measurement is the
starting point rather than work to redo.

**Model recommendation.** high. Spreading a spawn across frames changes when entities exist, which
mission scripts and the objective graph both observe; the correctness risk is larger than the
performance work.

**Verify.** Re-run the D33 measurement under `--det` at 1P and 4P and show both frames under the
45.7 ms threshold, with the attribution no longer naming `ai_spawn`. `campaign-squad-wakeup`,
`campaign-roster` and `campaign-objectives` green, and the complete `.\RunTests.ps1` before landing.
`docs/verification.md` PERF-12 and PERF-13 apply.

**⚠ Traps.** The stall is far past `HitchMonitor`'s grace window and reproduces to the same frame under
`--det`, so it is not workstation noise. Spreading the spawn must not change the sim frame on which
the entities become observable to the objective graph, or the mission's own pacing moves with it.

## E42 ☐ `BL-562` CM11's physics tick spikes are attributed and removed

**Goal.** CM11 (C2/M02) runs no single physics tick long enough to exhaust Godot's eight-step catch-up
cap, so no sim step is discarded.

**Evidence (confidence: direction-sound).** The bracketed instrument (`Utils/PhysicsTickCost.cs`, and
`--perf`'s `phys_tick_ms` / `phys_tick_max_ms` / `phys_hz`) over 333 windows of a loaded CM11 session
puts the mean tick at 1.81 ms (p95 3.20 ms) and the tick rate at a median of 60.0 per wall second, but
`phys_tick_max_ms` hit 72, 102 and 177 ms on individual ticks. `max_physics_steps_per_frame` is at
Godot's default 8, so a 177 ms stall leaves about six sim steps undeliverable and they are discarded,
not deferred. The named candidate is `Mech3/Anim/NameResolver.cs`'s `ClearFindCache`, which drops the
whole find memo, so the next tick's roughly sixty objective name resolutions re-walk the roughly
5000-row index calling `GodotObject.IsInstanceValid` on every row; its callers are the `IndexStage` /
`IndexSpawnedCopy` / `IndexRebasedStage` / `IndexPooledCopy` spawn and warm-up paths in
`Mech3/AnimRuntime.cs`, which is the right shape for a spike that lands on a spawn rather than
steadily. `<TODO: re-verify still-open against the code>`

**Approach.** Confirm the candidate by bracketing a spiking tick rather than by inference: instrument
what a spiking tick is doing before changing the memo. If it is the find cache, the fix is an
invalidation narrower than dropping the whole memo. Check the file-contention queue: A1 edits the same
file.

**Model recommendation.** high. The instruments here have already misled once on this exact item, and
a cache invalidation that is too narrow returns a stale node rather than a slow frame.

**Verify.** Re-run the bracketed instrument over a loaded CM11 session and show `phys_tick_max_ms`
under the 16.7 ms budget across the same window count, with the mean unchanged. Compare durations in
sim seconds, never wall seconds. The complete `.\RunTests.ps1` before landing, since a memo change
reaches every name resolution in the game.

**⚠ Traps.** **Do not chase the sustained step**; see the disproven-claims table above. Do not raise
`max_physics_steps_per_frame`: it deepens the catch-up spiral rather than recovering the lost steps.
The original measurement ran under `--debug-objective=18` with nobody at the controls, so it
under-weights projectiles and destruction cascades; a re-measurement should say which mode it used.

## E43 ☐ `BL-612` CM09 and CM13 hold their rate for the whole mission

**Goal.** CM09 (C1/M04) does not fall under 60 for the rest of the mission once the Promised Land is
down and the next fighter squad spawns, and CM13 (C2/M03), which shows the same sustained drop, holds
its rate too.

**Evidence (confidence: lead-only).** Reported at the controls on the plan branch, and it is a
sustained rate drop rather than single hitches. The sortie log's late `[perf] hitch` lines carry the
shape in their baseline: 19 to 26 ms against 10 ms earlier in the same sortie, with `script_ms` about
25 and `physics_ms` 17 to 18 while `render_cpu_ms` stays near 1 and `gpu_ms` near 0.5, `nodes` about
37000 and `mem_mb` about 1300. The frame is spent on the CPU in script and physics, not on the GPU. No
culprit is named: `attributed_ms` is 0 in those lines because the sampler was not armed.
`<TODO: re-verify still-open against the code>`

**Approach.** The hitch detector reports outliers against a rolling baseline, so a whole-run slowdown
reads only in that baseline. Measure the rate itself instead (a frame-time trace over the sortie, or
the probe's own timing), then attribute the script time with the sampler armed. **Measure CM13 first:**
that mission spawns its whole field at the start and involves no zeppelin death, so if both missions
share a cause it is the number of live aircraft rather than the destruction choreography. Then read
CM09's second variable: whether the cost is the zeppelin's death choreography left running (the burn,
finisher and sink anims and their emitters persist after the hull is down), the destroyed hull's
colliders and debris still stepping, or the new squad's rigs adding AI and physics on top. The node
count says nothing was freed. Compare a CM09 run that leaves the zeppelin alive.

**Model recommendation.** high. The item is a measurement before it is a fix, the instrument has to be
chosen against a detector that answers a different question, and the outcome may be a disproof.

**Verify.** A frame-time trace over a full CM13 sortie and a full CM09 sortie showing the rate held
across the second half, with the attribution naming what changed. Take the baseline first: an
unchanged number is not evidence unless you have seen it able to fail. `docs/verification.md` PERF-12,
PERF-13 and PERF-19 apply. The complete `.\RunTests.ps1` before landing.

**⚠ Traps.** The hitch detector is opt-in (`.\RunTests.ps1 -Hitch`) and answers a different question
than this one; its lines name no culprit here because the sampler was not armed. Do not treat CM09's
zeppelin as the first variable: CM13 is the cleaner test case and comes first. If the answer turns out
to be the settled GC pause, read the second disproven claim above before optimising allocation.
