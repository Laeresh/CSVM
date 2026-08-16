# AI damage visuals, crash audio and the decoded engine-audio model

**ACTIVE PLAN** (written 2026-08-16). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

This plan closes `BL-385` (enemy and wingman aircraft show no damage at all, and their crash is
silent) and absorbs `BL-384` item (3) (the one-way `_applied` latch), which cannot be separated from
it: an AI ladder names the same anim at six thresholds, so the name-keyed latch that `BL-384` item
(3) was going to replace is what stops this item from working at all. `BL-385` was re-verified
still-open against both the record and the code in this session: `controller.Visuals` is assigned at
`FlightRigAssembler.cs:284` only, `controller.Audio` at `:298` only, and `AiAircraftSpawner` assigns
neither. `BL-384` items (1) and (2) were confirmed landed; item (3) was confirmed still open.
`BL-386` was confirmed closed, which is what unblocked this work.

Three decodes were run against `crimson.exe` in this session and their results are written up in
`docs/org/vehicleDamage.md`: the anim root retarget rule, the destroyed-vehicle lifetime, and the
engine-audio model. Every constant in this plan carries the address it came from. The plan
deliberately extends past `BL-385`'s stated scope in one direction, on an explicit call: the
engine-audio decode corrected four readings on the **player's** audio path, and those corrections
are in scope here rather than deferred, because leaving them would mean running two contradictory
models of one subsystem in one file.

**Out of scope:** the AI wreck's persistence. The decode settled that the original never frees a
destroyed vehicle either, so there is no despawn work to do; what this plan does is make our wreck
*invisible* the way the original's is, which falls out of A3. `BL-343` (wreck momentum) stays its own
item, though this plan records the mechanism the decode found for it.

## Milestone goal

- An enemy or wingman aircraft stages its own authored damage ladder as its hull health falls, and
  every entry in that ladder fires, including the five repeats of one anim name.
- A shot-down AI aircraft disappears into its crash effects instead of leaving a visible pristine
  hull, matching the original's `ai_crash_*` choreography.
- An AI kill is audible, positionally, from the crash animation's own authored sound events.
- AI aircraft carry the engine loop, positional, culled at the decoded distance. The whine slot the
  plan expected to fill turned out to be unassigned in the retail install (B14), so there is no
  second loop to carry.
- The player's engine audio matches the decoded model rather than four readings the decode refuted.

**No behaviour in this plan is invented from feel or footage.** Every threshold, distance, curve and
loop assignment traces to an address in `crimson.exe` or to authored data in `extracted/`. Where the
decode left a question open it stays open and marked, not filled with a plausible number.

## Decisions (2026-08-16)

| # | Question | Decision |
|---|---|---|
| 1 | Is this item visuals only, or visuals + audio + wreck? | **All three** — with the wreck half reduced to "make it invisible", since the decode removed the despawn question. |
| 2 | How do AI stages get past `RigAnimFor`'s player whitelist? | **A second curated list** (`AiDamageStageAnims`), union-tested with the player's. Matches the existing curated-list convention (`CallSuppliedAnchors`, `AirframeScopedAnchors`); a program-existence rule would start playing cockpit gauge defs on the airframe. |
| 3 | What is an AI's `DamageVisuals` built with? | **No panels, no pairing** — and the need for pairing is **derived from the data** (does either ladder name a `pdpanel*` stage?), not from a constructor flag. A player def does, an AI def doesn't. |
| 4 | How does an AI kill get its sound? | **Give the AI crash runtime its `Sounds` reference** and let the authored `Sound` events play positionally. Not a `FlightAudio` per AI plane, which is own-ship and non-positional by design. |
| 5 | Where do AI engine loops live? | **A separate positional component**, with the pitch/gain curve maths extracted into a helper shared with `FlightAudio`. Not a dual-mode `FlightAudio`. |
| 6 | Which loops does an AI plane get? | **Decode first, then match it.** Decode ran: engine, damaged-engine as a def swap on the engine slot, no rattle. The `prop_sound` whine slot exists in the reader but **no shipped vehicle def authors it**, so the original assigns it on no aircraft (B14's re-decode; see row 3). |
| 7 | How is the `_applied` latch fixed? | **Per-entry slots, and the retraction too** — this plan absorbs `BL-384` item (3) rather than touching the same structure twice. |
| 8 | Where does the shared wiring live? | **Two-phase extraction** — a shared construction helper called unconditionally by both spawners, and the sink/stop wiring folded into `BuildFlightCrashRuntime`. Folding construction in too would silently delete the damage lab's visuals. |
| 9 | How much of the decoded audio machinery do we build? | **The 2000-unit cull, yes; the 15-voice budget, no.** The cull is what the player hears; the voice cap is a DirectSound buffer-pool artifact. |
| 10 | How many of the decode's four player-side corrections does this take? | **All four**, including removing the detuned dual engine stack. |
| 11 | Does the AI crash def get played on the aircraft? | **Yes — drop the `kestrel` scaffold** and play `ai_crash_*` with the plane as context node, which is what the retarget decode says the original does. |
| 12 | What evidence closes this? | **Three layers** — test-suite coverage for the staging logic, a pinned golden for the visible trail, and a debug log line for the audio. Plus the playtest lines. |

## ⚠ Read this before implementing anything

Five readings this session refuted. None of them was obviously wrong; four of them read as settled.

| # | The wrong claim | How it died |
|---|---|---|
| 1 | `pfsmoketrail`'s `anim_root_name` (`piratefighter`) may need an explicit OPERAND_NODE retarget onto other airframes (`BL-385` trap (b), filed as "unverified"). | Decode: `anim_root_name` is an offset within the caller's context node, not a target selector. A def whose root name equals its own name has both fields overwritten with the context node's name at play time (`FUN_00520910` / `FUN_00521180`). No retarget exists or is needed. |
| 2 | An AI wreck persisting is a bug of ours; the original presumably removes it after some time. | Decode: nothing on the death path frees a vehicle. No timeout, no distance cull, no count cap, no recycling. The free is a handshake between `LAB_00480710` and `FUN_0047bab0`, and no roster aircraft reaches a caller of the latter. The wreck stops being *visible* because the crash anim deactivates all four of its nodes. |
| 3 | `PlaneStats.WhineSound = "snd_enginewhine"` — "not named in the readers; the only pitch-shiftable candidate" (`PlaneStats.cs:209`). | Decode: the KEY is named in the readers. Slot 1's def is `prop_sound`, VDEF+0x74, parsed by `FUN_00479240` at `0x0047a2aa` and assigned at `0x004771b3`. B14 then found the rest: **no shipped vehicle def authors the key**, `FUN_00478a00` leaves the field at 0, and `snd_enginewhine` is a literal nowhere in `crimson.exe`. So slot 1 is never assigned and the original plays **no whine at all**. |
| 4 | The damaged-engine sound is a second loop blended in over the engine by a gain curve (`PlaneStats.cs:219-222`, itself flagged "a TUNE candidate, not a confirmed original mechanic"). | Decode: it is a **definition swap** on slot 0, drawn at random from an array at VDEF+0x7c, with the pitch multiplier `veh+0x68` randomised into `[entry+0x14, entry+0x18]` when the entry's flag byte `+0x10` is set. No crossfade, no second voice, and an array we collapse to one entry. |
| 5 | The original plays its engine as a detuned dual stack, ~5% apart — from spectral combs in a reference dive recording (`FlightAudio.cs:35-48`). | Decode: one handle per slot. The combs are better explained by engine + `prop_sound`, two loops with different curves. Per this repo's own rule, a video measurement does not contest a decode. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A2, A3, B11, B12, B13, B14 | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | B15 | The *what* is settled; the roll-off curve stands in for the decoded one and stays TUNE. |
| **Leads only — no mechanism yet** | C16 | Budget for investigation; the anchor census may end in a disproof. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy. This plan is being run from
`.claude/worktrees/bl385-ai-damage-visuals`.

## What the data actually ships

**The AI damage ladders.** Each AI airframe authors its own seven-entry `injure_anims` list in
`extracted/zrdr/vehicle.zrd.json`, restated verbatim on its `r*` variant. `fury` (lines 4738-4768):

| Fraction | Anim |
|---|---|
| 0.95, 0.80, 0.65, 0.50, 0.45, 0.25 | `random_remote_damage` |
| 0.40 | `pfsmoketrail` |

`bswingman` (def at line 3993) authors `[[0.5, "pfsmoketrail"]]` — one entry. `basic_airplane`
authors no `injure_anims` at all. **Six of `fury`'s seven entries share one anim name**, which is
what breaks the name-keyed latch. The 88 `pdpanel` occurrences in that file are all on the zoned
`p*`/`r*` defs, and no roster block names an `r*` (`docs/org/vehicleDamage.md`, the 2026-08-16
correction), so **no AI airframe ever stages a panel**.

**The stage defs.** `piratefighter-pfsmoketrail.json` is a `smokepuffer` + `firepuffer` pair at
`prop1`, `LOOP -1`, distance-interval emission. `player_pfighter-random_remote_damage.json` is a
five-sequence random cascade (`RandomWeight 0.33` at each step) calling `small_fireball_follow`,
`short_fireball_follow` and `short_fire_follow` onto four nodes: `railer1`, `lailer1`, `lft_elev`,
`rt_elev`. Both defs have `name == anim_root_name`, so both take the total-retarget branch.

**The AI crash defs.** `kestrel-ai_crash_default` / `_dirt` / `_water`, one set per chapter,
`has_callbacks: false`, every event untimed. Each deactivates `dontmove`, `markers`, `healthy` and
`destroyed`, plays a sound, and calls one or two effects. `_dirt` authors `snd_exp_ground_a` at node
`destroyed`, with a matching `static_sounds` entry.

**The engine-audio model** (`FUN_004b18a0`, the per-frame engine audio for every aircraft, called
per non-player vehicle then once for the player by `FUN_004897c0`):

| Slot | Def | Started for AI? |
|---|---|---|
| 0 | `engine_sound`, VDEF+0x6c | Yes, gain 1.0 |
| 1 | `prop_sound` (the whine), VDEF+0x74 | Gain 1.0 — but ⚠ **never in practice: no shipped def authors the key**, so the slot has no definition for anybody (B14) |
| 0 (swapped) | `damaged_engine_sound[]`, VDEF+0x7c | Yes, random entry, `veh+0x68` randomised into `[entry+0x14, entry+0x18]` when the entry's flag `+0x10` is set. The shipped entry sets the flag with the range **0.0 to 1.0**, so a damaged engine's pitch is drawn anywhere from the frequency floor to normal, once per swap, and holds (B14) |
| — | `cockpit_engine_sound`, VDEF+0x70 | Never — player, camera MODES 6/7, which are cockpit modes CSVM has no counterpart for (B14) |
| — | rattle (`player.zrd` globals, `DAT_0071c334`) | Never — plays through camera shake, gated on being the camera's subject vehicle |
| 2, 3 | collision / landing one-shots | Never — player-gated at all three call sites |

Positional or not is **the sound def's own `3D` flag** (bit 0x4, read in `FUN_004b1470` at
`0x004b1484`), not a player check. The AI arm forks at `0x004b194a` and adds exactly two things: the
cull, and `veh+0x68` forced to 1.0. The cull compares squared distance to the player against
`4000000.0` (`0x0060350c`) — **2000 world units** — stopping both handles beyond it. Attenuation
inside is the def's own `RANGE` (`def+0x1c` full-volume, `def+0x20` audible), full volume holding to
`full + ⅛ of the span`, silent at `1.1 × audible`. Voice budget: **15 buffers per definition**
(`DAT_0063a024`). Pitch and volume track throttle and airspeed identically for AI and player, off
global `player.zrd` curves, with an airspeed term of 0.26 into volume and 0.25 into pitch, clamped
to [0, 1.5]; final frequency clamp [4000, 55200] Hz.

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

### Wave A — Make the staging correct before anything reads it

1. ☑ Replace `_applied` with per-entry slots, and add retraction (absorbs `BL-384` item 3)
2. ☑ Derive panel-pairing need from the data, and let `DamageVisuals` be built hull-only
3. ☑ Play `ai_crash_*` on the aircraft: drop the `kestrel` scaffold for a context node

### Wave B — Wire the AI up

11. ☐ Extract the two-phase `DamageVisuals` wiring and give the AI spawner both phases
12. ☑ Add `AiDamageStageAnims` and open `RigAnimFor` to it
13. ☐ Give the AI crash runtime its `Sounds` reference
14. ☑ Correct the player's engine-audio model to the decode
15. ☑ Positional AI engine audio with the 2000-unit cull (component built; the spawner's attach line
    is owed by B11's file, tracked below)

### Wave C — Evidence

16. ☑ Anchor census: do the AI stage defs' nodes exist on every AI airframe?
17. ☐ Tests, golden and audio debug line

## Dependency and parallelism notes

A1 blocks B11, B12 and C17 — every stage that fires depends on the slot rework being right, and
testing the ladder before it exists tests nothing. A2 blocks B11 (the shared construction helper
takes the derived panel decision as given). A3 is independent of A1 and A2 and can run in parallel:
it touches `WorldEffectsFactory`'s crash-rig assembly and the `ai_crash_*` anchoring, not
`DamageVisuals`.

B13 → B14 → B15 is not a chain but a contention: **B14 and B15 both edit `FlightAudio.cs`** (B14
corrects it, B15 extracts the shared curve helper out of it). Do not run those in parallel
worktrees. B13 edits `WorldEffectsFactory.cs`, which **A3 also edits** — same warning.

C16 is pure investigation, depends on nothing, and should run early because a negative result
changes B12's approach. C17 lands last.

File ownership if these are split across agents: A1+A2 own `DamageVisuals.cs`; A3+B13 own
`WorldEffectsFactory.cs`; B11 owns `FlightRigAssembler.cs` and `AiAircraftSpawner.cs`; B12 owns
`EffectCatalogue.cs` **and `RigAnimFor` in `DamageVisuals.cs`**, which the original ownership note
missed; B14+B15 own `FlightAudio.cs` and `PlaneStats.cs`. A1 landed the `DamageEffectStopOne` end of
the wiring in `FlightRigAssembler.cs`, so B11 inherits that seam rather than a clean file.

---

# Wave A — Make the staging correct before anything reads it

## A1 ☐ Replace `_applied` with per-entry slots, and add retraction

**Goal.** Every entry in an `injure_anims` ladder fires when its own threshold is crossed downward,
independently of whether another entry names the same anim; and a stage retracts when a repair lifts
the fraction back over its threshold, so it can fire again on the next crossing.

**Evidence (confidence: traced).** `DamageVisuals._applied` is a `HashSet<string>` keyed on the anim
name (`DamageVisuals.cs:48`), tested with `!_applied.Add(anim)` at `:142` (per-part) and `:185`
(hull). `fury`'s ladder names `random_remote_damage` at six thresholds
(`extracted/zrdr/vehicle.zrd.json:4738-4768`), so five of the six are suppressed. The same set is
shared across parts, so an entry authored on all four player zones fires once instead of four times.
⚠ That second half has **no shipped instance**: a census of all 22 defs carrying `destroyable_parts`
(the eleven `p*` plus eleven `r*`) found none naming one anim on two of its zones. The per-(part,
entry) keying is still what the original does and stands, but its test is driven from a synthetic
four-zone ladder; the break that bites in shipped data is `fury`'s six same-named hull entries.
The original keys per entry: one handle slot per ladder entry in `inst+0x890` (def-level) and
`part+0x4c` (per-part), started when the slot reads zero, cleared on the **upward** crossing alone
via `FUN_004ed480` — never when the anim finishes (`docs/org/vehicleDamage.md`, "One start per
downward crossing"). `FUN_004b3e20` and `FUN_004b3910` are the wipes; `FUN_004b8180`, the set-health
path behind repairs, calls the per-part wipe before re-running `FUN_004b3d70`.

**Approach.** Replace `_applied` with two slot arrays mirroring the original's: one per vehicle-level
ladder entry, one per (part, entry index). A slot holds the started anim's handle so the stop path
can clear it, which is exactly the structure `BL-384` item (3) specified. `Reset()` keeps its current
job (stop everything, clear all slots). Add the upward-crossing clear in both `OnPartDamage` and
`OnHullDamage`. Do not touch the shipped 0.10 / 0.85 / 0.99 thresholds or `GaugeCluster`'s combined
armour+health scale — `BL-384`'s traps (a) and (b) still hold.

**Model recommendation.** high — the semantics are subtle (once per downward crossing, cleared only
on the upward one), the failure mode is silent, and it is the item everything else rests on.

**Verify.** A test that walks `fury`'s ladder down through 0.25 and counts **six**
`random_remote_damage` starts plus one `pfsmoketrail`; a test that repairs past a threshold and
confirms the stage stops and can re-fire; a test that the same entry on four player zones fires four
times. Then the F5 damage lab: repair and watch the stage retract (`BL-384` item (3)'s own playtest
line, inherited here).

**⚠ Traps.** A stage that re-fires whenever the fraction merely *stays* below its threshold is a
different bug, not the fix — the slot is cleared on the upward crossing alone
(`BL-297`'s decode, restated in `BL-384`). Retraction makes the F5 lab's repair path visibly
un-stage; that is faithful, not a regression. `DamageVisuals.Reset` stays for respawn.

## A2 ☐ Derive panel-pairing need from the data, and let `DamageVisuals` be built hull-only

**Goal.** A plane whose data names no `pdpanel*` stage does no panel pairing at all, and says nothing
alarming about it; the "no authored pairing data" warning keeps firing only for planes that do need
pairing and can't get it.

**Evidence (confidence: traced).** The constructor runs `PairHealthySkins` unconditionally
(`DamageVisuals.cs:69`), a mesh-AABB walk over every panel. An AI airframe resolves no
`destroyable_parts` (`docs/org/vehicleDamage.md`, 2026-08-16 correction), so `OnPartDamage` never
runs, and no AI ladder names a `pdpanel*` entry. The null-pairing branch warns loudly
(`DamageVisuals.cs:321-324`), which would fire on every AI spawn.

**Approach.** In the constructor, decide from `stats` whether either ladder (part-level or
vehicle-level) names a `pdpanel*` stage. If neither does, skip pairing entirely and skip the warning.
No flag, no overload, no spawner-side knowledge — the data answers it. Panels and pairing become
optional inputs that an AI spawn simply does not supply.

**Model recommendation.** medium — mechanical, but it touches a warning whose meaning must not
change.

**Verify.** Spawn an AI plane and confirm no pairing warning and no pairing work; spawn a player
plane with the crash program deliberately absent and confirm the warning still fires. Full 8-chapter
`--freecam` regression for unchanged node counts.

**⚠ Traps.** The warning is a real alarm for the player path — the whole point of this item is to
keep it that way. Do not silence it by making the null case quiet in general.

## A3 ☐ Play `ai_crash_*` on the aircraft: drop the `kestrel` scaffold for a context node

**Goal.** A shot-down AI aircraft's four nodes (`dontmove`, `markers`, `healthy`, `destroyed`) are
deactivated by its own crash def, so the wreck disappears into its effects instead of leaving a
visible pristine hull.

**Evidence (confidence: traced).** `kestrel-ai_crash_default` deactivates all four nodes, every event
untimed. We anchor those defs on a **meshless `kestrel` scaffold**, a childless `Node3D` added under
the crash root so the defs' authored NAME resolves to something
(`WorldEffectsFactory.cs:286-294`), and `AnimRuntime.Play` resolves node references relative to the
anchor it found (`AnimRuntime.cs:737-750`). The decode says the original needs no scaffold: the
caller supplies the **vehicle's own node** as context, and because these defs have
`name == anim_root_name`, both fields are overwritten with that node's name at play time
(`FUN_00520910` / `FUN_00521180`; `docs/org/vehicleDamage.md`, "Which airframe a stage's anim binds
to").

**Approach.** Drop the scaffold and play the `ai_crash_*` family with the AI plane as the context
node. The place-exemption (⚠ TopLevel-pinning the scaffold would drag the wreck and every pooled
template copy to the first crash's site) is **transferred, not dropped**: `kestrel` moves from
`CrashScaffoldAnchors` to `AirframeScopedAnchors`, whose entries are both dropped from the stage-root
closure (`EffectCatalogue.SuppliedElsewhere`, which is what keeps the closure from throwing on a
name no rig carries) and place-exempt. `NewCrashTemplateStage` concatenates the two lists, so the
exempt set is unchanged; `CrashScaffoldAnchors` shrinks to `player`, the crash root that actually
parents the wreck and the pool containers.

**Model recommendation.** high — it removes a workaround in the crash-rig assembly, and the failure
mode (a pinned wreck at the first crash site) is spectacular and easy to miss in a headless run.

**Verify.** Shoot down an AI plane and confirm the airframe vanishes as the crash effects play, then
shoot down a second one somewhere else and confirm its effects play at *its* site, not the first
one's. Full 8-chapter `--freecam` regression.

**⚠ Traps.** `destroyed` is built into the crash root by `BuildDestroyed` while `healthy` is on the
aircraft, so the def spans two subtrees — the context node must be chosen so both resolve. **The
crash root satisfies both, and no reparenting is needed**: the crash runtime binds the whole
controller (`crashRuntime.Bind(controller, …)`), and with `NameResolveFallback` set every rig is one
name scope, so `NameResolver.ResolveScoped`'s third tier answers a name the anchor's own subtree
lacks. `destroyed` resolves in tier 1 under the crash root and `healthy` in tier 3 across the rig.
The able-to-fail control is the bind scope, not the anchor: narrowing `Bind` to the crash root
leaves `healthy=on destroyed=off` on a real AI crash. The `kestrel` NAME resolves nowhere in a rig
(our model roots are `player_*`), so `AnimRuntime.Play`'s placeless-def arm falls back to the
caller's anchor, which is the crash root — the same shape as the player family, where the `player`
NAME matches that node outright.
Do not "fix" this by hiding the aircraft from `Crash()` in code; that moves authored behaviour into
code, which is the pattern this codebase keeps decoding its way out of.

---

# Wave B — Wire the AI up

## B11 ☐ Extract the two-phase `DamageVisuals` wiring and give the AI spawner both phases

**Goal.** An AI plane has a `DamageVisuals` and a live `DamageEffectSink`/`DamageEffectStop` pair,
built from the same code the player rig uses, without either spawner duplicating the other.

**Evidence (confidence: traced).** Phase 1, construction, is `FlightRigAssembler.cs:274-289` and
needs only the model and stats; it must run even when there is no crash program, because the parked
damage lab and world-less flight get panel flips with a null sink and `PlayStage` has a branch that
says so out loud (`DamageVisuals.cs:306-311`). Phase 2, the sink/stop wiring, is `:438-471` and needs
the rig runtime, which exists only after the controller joins the tree. `AiAircraftSpawner` already
builds a crash runtime at `:146-151`, so the sink has something to play into.

**Approach.** A shared construction helper both spawners call unconditionally (phase 1); the
sink/stop wiring folded into `WorldEffectsFactory.BuildFlightCrashRuntime` (phase 2), where "after
the runtime exists" is already true and which already branches on `IsHumanPiloted` for the crash
family. Comment both ends explaining why one object's setup lands in two files. The stop closure
must stay derived from the program, not a hand list (`FlightRigAssembler.cs:456-458`).

**Model recommendation.** high — a refactor across two spawners with an ordering constraint that is
invisible until the damage lab breaks.

**Verify.** AI plane takes damage and stages; player rig unchanged (staging, panels, fuel leak, trail
all as before); F5 damage lab still stages with a null sink. Full 8-chapter `--freecam` regression.

**⚠ Traps.** Folding phase 1 into `BuildFlightCrashRuntime` would silently delete visuals from every
rig without a crash program. That is the whole reason the phases are split.

## B12 ☐ Add `AiDamageStageAnims` and open `RigAnimFor` to it

**Goal.** `pfsmoketrail` and `random_remote_damage` play when an AI ladder names them.

**Evidence (confidence: traced).** `RigAnimFor` (`DamageVisuals.cs:120-131`) returns non-null only
for `pdpanel*`, `*_damage_effects`, `player_fuelleak`, and maps `player_smoketrail` →
`player_damage_trail`; everything else returns null and `OnHullDamage` skips it (`:187`). A
program-existence rule is not available as a substitute: C1 ships `nose-nose_damage_green/yellow/red`
and three more sets like it, plus four `*_got_hit` defs, all cockpit gauge indicators that
`OnPartDamage` deliberately drops (`:169-171`).

**Approach.** Add `EffectCatalogue.AiDamageStageAnims = { "pfsmoketrail", "random_remote_damage" }`
next to `PlayerDamageStageAnims`, with the same ⚠ the neighbouring curated lists carry
(`EffectCatalogue.cs:129-142`: extend the list, never the mechanical walk). Make `RigAnimFor` a
membership test over the union, keeping the one documented `player_smoketrail` remap. Include the AI
list in the stop closure and in the crash rig's bound-def set the same way the player list is
(`:185`).

**Model recommendation.** medium — small and mechanical once A1 and B11 are in.

**Verify.** An AI plane at 40% hull trails smoke; at 0.95 and below it takes `random_remote_damage`
bursts. Confirm no cockpit gauge def ever plays on an airframe.

**⚠ Traps.** Do not give AI planes the `player_*` menu; their data names different anims
(`BL-385` trap (c)).

## B13 ☐ Give the AI crash runtime its `Sounds` reference

**Goal.** An AI kill is audible, positionally, from the crash animation's own authored sound events.

**Evidence (confidence: traced).** `kestrel-ai_crash_dirt` authors a `Sound` event for
`snd_exp_ground_a` at node `destroyed`, plus a matching `static_sounds` entry. `AnimRuntime` plays
`Sound` events through `Sounds.PlayOneShot` at the event's position (`AnimRuntime.cs:2564`), and
`WorldSounds.PlayOneShot(name, Node3D, rng)` is source-following and already used for AI combat voice
(`AiVoiceRuntime.cs:189`). The crash rig is deliberately built with **no** `Sounds`:
"No audio here; `FlightAudio.Crash()` already plays it" (`WorldEffectsFactory.cs:320`). That is
correct for the player and is exactly why the AI is silent.

**Approach.** Pass the session `WorldSounds` into the crash runtime for AI rigs only; leave the
player rig's `Sounds` null so `FlightAudio` stays the single owner of own-ship crash audio. Comment
the asymmetry on the seam, or it will be "fixed" later into a double-play. This also makes the damage
stages' own sound events audible, which is intended.

**Model recommendation.** medium — a small wiring change, but the asymmetry needs the comment.

**Verify.** Shoot down an AI plane over dirt and over water and confirm distinct, positional booms
that attenuate with distance; confirm the player's own crash still plays exactly once.

**⚠ Traps.** `PlayCrashBoom` and `OnEngineStop` are **not** the fix and should not be reached for an
AI plane — the entry's original diagnosis pointed at them, and the decode says the sound comes from
the animation's authored events instead.

## B14 ☐ Correct the player's engine-audio model to the decode

**Goal.** The player's engine audio matches the decoded model: `prop_sound` as the whine's source,
`cockpit_engine_sound` in views 6/7 and the positional `engine_sound` otherwise, damaged-engine as a
def swap with randomised pitch, and no detuned second voice.

**Evidence (confidence: traced).** All four corrections are tabulated in "⚠ Read this before
implementing anything" above, rows 3, 4 and 5 plus the missing `cockpit_engine_sound` read. Slot
assignment is `FUN_00476250` at `0x0047719f` / `0x004771b3`; the view branch is the player arm of
`FUN_004b18a0`; the 3D decision is `FUN_004b1470` at `0x004b1484`; the reader offsets are
`FUN_00479240` at `0x0047a249` / `0x0047a27d` / `0x0047a2aa` / `0x0047a2ea`.

**Approach.** Read `prop_sound` and `cockpit_engine_sound` in `PlaneStats`; read
`damaged_engine_sound` as the array it is, with the per-entry flag and pitch range. In `FlightAudio`,
drop `_engine2` and `EngineVoiceGain`, swap the engine def on damage instead of blending a second
loop, and select `cockpit_engine_sound` vs `engine_sound` by view. Keep every TUNE constant that the
decode did *not* refute untouched.

**Model recommendation.** high — it changes how the player's aircraft sounds, and the constants
around it carry spectral-analysis provenance that must not be disturbed by accident.

**Verify.** Two instruments, because audio cannot be screenshot-verified.
(1) *Headless, machine-readable, and the half a listener could not separate by ear anyway:* the
`sound` log category. The rig-build line names every resolved slot definition
(`audio: engine=snd_bloodhawkengine damaged=snd_damagedengine whine=none (no def names prop_sound)
rattle=snd_planeshake`), and the swap line records the definition the engine slot took and the pitch
it drew (`engine sound: slot 0 -> snd_damagedengine pitchMul=0.634`). Reproduce with
`RunProbe.ps1 --fly --plane=player_bhawk --stage=empty --damage=nose:0.4 --volume=1.0 --frames=90
--screenshot=… --log=sound:debug`. `--dump-config` is the companion check: the `flightAudio` block
must be gone, since all three of its keys scaled refuted mechanisms.
(2) *At the controls, which only the user can do* (`docs/verification.md`, "What this project cannot
verify itself"): same plane, same chapter, `--volume=1.0 --no-det`, HEAD against this build. At a
fixed throttle in level flight the chorus/beating of the dual voice is gone; in a full dive past
`fd_speed` no whine layer rises in over the engine, because the original has none; after taking hits
the engine loop is replaced outright by `snd_damagedengine` at a random pitch rather than joined by
a second loop.
The view 6/7 half of this A/B is void — there is nothing to switch, see the traps below.

**⚠ Traps.** Removing the second voice will be noticed at the controls; it is deliberate and
decode-backed, and the reason belongs in the landing commit message so nobody restores it from the
old comment. `MixGain` is an own-ship splitscreen concept and stays on `FlightAudio` alone.
⚠ **Our view indices are NOT the original's 6/7 and the branch must not be wired to them.** The
original's 6 and 7 are camera MODES, and its cockpit modes at that; our `--view=`/numpad set is
external throughout (`CameraController.Views`: 6 is a level flank, 7 an above-flank), CSVM ships no
cockpit view at all (`BL-080` is unstarted, and `PlaneStats.TurretMount` already says so), and
`BL-150`'s `CAP-07` measurement puts the original's own 6 and 7 at a port flank and an
astern-starboard low view. So `cockpit_engine_sound` is read into `PlaneStats` and nothing selects
it; binding it to numpad 6/7 would be inventing behaviour.

## B15 ☐ Positional AI engine audio with the 2000-unit cull

**Goal.** AI aircraft are audible as they pass, engine (and the whine slot, which no shipped def
fills — see the model table), attenuating with distance and silent past 2000 units.

**Evidence (confidence: direction-sound).** The loop assignment, the fork, the cull constant
(`4000000.0` at `0x0060350c`, squared, i.e. 2000 units), the curve inputs and the clamps are all
traced (see "What the data actually ships"). What is *not* reproduced exactly is the roll-off shape:
`WorldSounds` uses Godot's inverse-distance curve with `UnitSize = def.RangeMin` and
`MaxDistance = def.RangeMax` and already marks that approximation TUNE
(`WorldSounds.cs:163-173`), whereas the original holds full volume to `full + ⅛ span` then rolls off
in log2 and cuts at `1.1 × audible`.

**Approach.** A separate positional component attached to the AI controller, owning slot 0 and slot 1
on `AudioStreamPlayer3D`, with the pitch/gain curve maths extracted from `FlightAudio.Update` into a
helper both call — extracted, not reimplemented, and without changing a single TUNE value. Implement
the 2000-unit cull (stop both handles beyond it, start them when back inside). Do **not** implement
the 15-buffers-per-definition voice budget: it is a DirectSound buffer-pool artifact, and Godot has
its own voice management.

**Model recommendation.** high — a new component plus an extraction out of a TUNE-heavy file.

**Verify.** The debug log line from C17: which loops started, per plane, with distance, and the frame
the cull silenced them. At the controls: fly past an AI plane and hear it pass.

**⚠ Traps.** The extraction must not change `EngineDetuneRatio`, `WhineMixGain` or any other TUNE
constant that survives B14 — a player engine that changes tone here would be blamed on the wrong
item. The damaged-engine def swap (B14) applies to AI too and must be shared, not duplicated.
Doppler is an **open question** in the decode (the call site pushes three arguments where the callee
takes four; it needs a debugger) — do not add Doppler on the assumption that it is there.

---

# Wave C — Evidence

## C16 ☐ Anchor census: do the AI stage defs' nodes exist on every AI airframe?

**Goal.** Know, per AI airframe, whether `prop1` (for `pfsmoketrail`) and `railer1` / `lailer1` /
`lft_elev` / `rt_elev` (for `random_remote_damage`) resolve — before trusting what the stages look
like.

**Evidence (confidence: lead-only).** A missing anchor is a **soft** failure in the original: the
reference resolves to zero, the anim still starts, and `FUN_004efaf0`'s global by-name fallback binds
it to any node of that name anywhere in the loaded scene before it reaches the NULL case
(`docs/org/vehicleDamage.md`, "Where a stage's effects land"). So a wrong-but-plausible attachment on
a small distant aircraft is exactly the failure that survives a playtest. `EffectCatalogue`'s existing
note claims `player_pfighter` is "written against the Devastator's own model root, inert on the other
ten airframes" (`:136-142`) — that claim predates the retarget decode and needs re-reading in its
light.

**Approach.** Census the eleven AI airframes' gamez for those five node names. Report, don't fix:
what the answer changes is B12's approach and whether a warning is needed when a stage's anchor is
absent.

**Model recommendation.** medium, low effort — a mechanical census over the model data.

**Result.** Censused by node identity over `extracted/planes/nodes.json`, written up in
`docs/org/vehicleDamage.md`. `prop1` is present on all twenty-two airframe roots. The **Bloodhawk**
is the only exception and only on the elevator pair: it spells its elevators `l_elev` / `r_elev`
under `nose`, so two of `random_remote_damage`'s five cascade steps have no anchor there. B12's flat
two-name list is unchanged by this; what it adds is a warn-once line naming (airframe, stage anim,
unresolved node), which fires exactly twice in the whole install. `EffectCatalogue`'s
"inert on the other ten airframes" note is wrong about the definitions (all twenty `player_pfighter`
defs take the total-retarget branch) and right about the anchor (no gamez ships that node).

**Verify.** N/A — this item's product is the census itself.

**⚠ Traps.** Do not conclude "it works" from seeing smoke: the global fallback means smoke can appear
at another object's `prop1`. Resolve by node identity, not by eye.

## C17 ☐ Tests, golden and audio debug line

**Goal.** The staging logic is covered by tests, the visible trail is pinned by a golden, and the
audio has a headless observable.

**Evidence (confidence: traced).** The precedent for the audio half is `WorldSounds.Debug`, which
exists because "Audio cannot be screenshot-verified, so this is the headless equivalent" and which
distinguishes "silent because the mission deactivated its host" from "silent because the host never
resolved" (`WorldSounds.cs:32-35`). Without the equivalent here, "the AI is silent" and "the AI is
2001 units away" are indistinguishable.

**Approach.** Tests: `fury`'s ladder firing six `random_remote_damage` starts plus one
`pfsmoketrail`; retraction on repair; the zone-less hull path; the same entry on four player zones
firing four times. Golden: a pinned shot of an AI plane trailing at 40%. Audio: a debug line naming
which loops started per plane, the distance, and cull transitions.

**Model recommendation.** medium — mechanical once the behaviour is settled, but the test assertions
are the item's whole value and must assert counts, not "something happened".

**Verify.** The suite passes; the golden re-pins cleanly; the debug line distinguishes the two silent
cases on a real chapter run.

**⚠ Traps.** The golden manifest's `exercises` field is hook-checked: under 250 chars, no item id, no
date, no "also exercises" clause, and it is **rewritten** on a re-pin, never appended to.

**Playtest (owed, covers the whole plan).** Shoot down a wingman and an enemy in C1. Smoke should
start at 40% on an enemy and 50% on a campaign wingman, `random_remote_damage` bursts should show on
the way down, the airframe should vanish into its crash effects rather than lying there intact, the
fireball should be audible and positional, and AI aircraft should be audible as they pass and silent
when far off.
