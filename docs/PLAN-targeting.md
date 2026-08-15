# Player target selection — decode, then a selectable target marker

**ACTIVE PLAN** (written 2026-08-15). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

This plan replaces the shipped auto-nearest hostile marker with the original's **player-selected**
target: a sticky selection the pilot chooses, over three target classes (Enemy/Objective, Ally,
Non-Aircraft), drawn with the original's plane-name label and range-gated brackets. It opens with a
decode pass into `docs/org/targeting.md` — the original's player targeting has never been decoded,
and the shipped marker was minted as a port invention on the (now disproven) belief that there was
no reference to copy. The decode is a hard prerequisite for the selection rules and for the one
number we have no substitute for, the bracket range threshold.

**Out of scope, deliberately.** (a) The **rebindable keymap** — every one of the original's eleven
targeting keys collides with our WASD + `Shift`/`Ctrl`-throttle scheme, which makes a rebind layer
the real answer to key placement; it is its own feature and this plan ships a curated default set
instead (`BL-<TODO: mint>`). (b) **Track Target's camera behaviour** — the `L` binding is reserved
and documented here, but "keep the target framed" hides a pile of camera decisions (snap vs smooth,
override vs blend with chase, behaviour with no target or a target behind you, interaction with the
E42 right-stick free look) that are camera work, not targeting work (`BL-<TODO: mint>`).
(c) **`Structures` / `DestructibleRegistry` as a selectable class** — the original's Non-Aircraft
cycle walks a curated `targets.zrd` mission-structure list; ours would walk every crate and fence in
the world, producing a cycle nobody would use. Held until a curated list exists
(`BL-<TODO: mint>`). (d) **The modernized marker** — the user's own preferred rule (brackets only
*past* 500 m, the inverse of the original's) is deliberately not built; the original's behaviour
ships first and the improvement is a later, separate call (`BL-<TODO: mint>`).

**Backlog provenance.** No item here is drawn from `backlog.md`, so no re-verification pass is owed.
Two adjacent existing entries are *touched* rather than closed: `BL-357` (the original steps weapon
selectors both ways) gains direct corroboration from the Weapons keybind page, and `BL-363` (the AI
candidate pool is aircraft-only) shares the pool-widening work but is not closed by this plan.
<TODO: re-verify BL-357 and BL-363 still-open against `git log --grep` + the code before citing them
in a landing commit.>

## Milestone goal

- The pilot chooses the target. `D-pad Up` (tap) steps to the next enemy; holding it selects the
  target nearest the crosshair, friend or foe. A curated keyboard set does the same and reaches
  allies and non-aircraft directly.
- The selection is **sticky**: it holds until the pilot changes it, the target dies (the original
  switches on death), or the pilot clears it. Nothing else drops it — not range, not line of sight,
  not leaving the field of view.
- The marker reads like the original: the plane's common name (`Fury`, `Kestrel`, `Bloodhawk`),
  fixed-size brackets under a range threshold, the label below, and an edge arrow with a two-line
  clock bearing when the target is off screen.
- Zeppelin sub-parts (gasbag, engines, cannons) and turret emplacements are selectable in their own
  right, not just the zeppelin as a whole.
- `--debug-markers` keeps full identity (`AI1 Fury 640 m H78 A91 pursue`) and gains the health and
  armor read.
- The targeting marker lives in its own module, outside `VersusHud`.

**No behaviour in this plan is invented where the original can be read.** Where the decode answers a
question, the decode wins; where it cannot, the answer is marked TUNE or deferred, never guessed.
The one place we knowingly diverge is key placement, and only because our own flight scheme has
already consumed the original's keys.

## Decisions (2026-08-15)

| # | Question | Decision |
|---|---|---|
| 1 | Design freely, or decode the original's player targeting first? | **Decode first, docs only, no C# from it yet** — the shipped marker's "no reference to copy" claim is false, so there is a reference |
| 2 | Decode scope: selection logic, HUD presentation, or both? | **Both as deliverables** — not HUD-as-best-effort |
| 3 | Which of the user's asks are memories of the original vs port inventions? | **Controls are own design; the label and brackets are the original's** — settled by screenshots, not memory |
| 4 | Mirror the original's keyboard scheme, bind only what was asked, or pad-only? | **Mirror as far as collisions allow, compress onto the pad** — and move the node-label overlay to `F13+` |
| 5 | How to place the keyboard binds? | **Keep every original bind we can, rehome the heads** — with the rebind layer to the backlog |
| 6 | The pad scheme? | **`D-pad Up` only: tap = Next Enemy, hold = Nearest Crosshairs** — `D-pad Down` stays free for future features |
| 7 | Tap-vs-hold semantics on one button? | **Tap resolves on release, 250 ms threshold** — the alternative flickers a wrong selection on every long press |
| 8 | What is selectable? | **Aircraft + zeppelin sub-parts + turret emplacements**, `Structures` excluded until a curated list exists |
| 9 | Selection lifecycle? | **Auto-acquire at spawn; switch on target death; survive own respawn; nothing else drops it** — confirmed against the original for the death case |
| 10 | Label content per case? | **Plane type alone (ambiguity accepted, as the original does); `P1`/`P2` kept for VS opponents; sub-parts unnumbered; `--debug-markers` keeps the full string; no range on the shipped marker** |
| 11 | Bracket and label geometry? | **The original's: brackets only *under* a range threshold, label always below, full `[role] - proper name` format prepared but empty-tolerant, one selected target + all VS opponents** |
| 12 | `--debug-markers` health figure? | **`H78 A91`** — health and armor separately, health first, no percent sign; combined dropped as a blended, dishonest number |
| 13 | Does the selection drive anything besides the HUD? | **No consumer ships in this plan**, but the state is exposed cleanly so the camera and AI orders can read it later |
| 14 | Keyboard binds, given all eleven original keys collide? | **Curated subset on free keys** (`T` `Y` `U` `I` `O` + `L`), rebind layer promoted to the next real item |
| 15 | Does Track Target's camera ship now? | **Bind `L`, stub the behaviour** — the camera questions are a feature of their own |
| 16 | Where does this live? | **Extract the pure modules *and* split the marker out of `VersusHud`** into its own HUD |
| 17 | Scripted twin shape? | **`--target=` naming the initial selection** — the cycle is better covered by unit tests than a scripted key sequence |
| 18 | Decode deliverable and stop rule? | **`docs/org/targeting.md`; selection is a hard requirement, HUD is hard on the range threshold and best-effort past it** |

## ⚠ Read this before implementing anything

Three claims died during the grilling that produced this plan. They are recorded so nobody
re-derives them.

| # | The wrong claim | How it died |
|---|---|---|
| 1 | The targeting marker is a port invention with "no reference to copy" — stated in `VersusHud`'s own module doc | `OriginalScreenshots/HUD.png` shows the original drawing an edge arrow with a two-line `Kestrel` / `4 o'clock` label. Our `DrawOpponent` edge branch already *is* the original's behaviour. The doc comment must be corrected as part of A1 |
| 2 | The six `Shift`/`Ctrl` targeting combos are free in our flight keymap | Our throttle is `KeyAxis(Key.Shift, Key.Ctrl)` (`FlightController.cs:2449`) — the bare modifiers are a *held axis*, so `Shift+E` moves the throttle while it targets. All eleven of the original's targeting keys collide, not five |
| 3 | The original brackets a target when it is *far* (the premise of the "brackets past 500 m" ask) | The original draws brackets only *under* a range threshold; distant enemies get none, and near ones get brackets swallowed by the silhouette. The user's rule is the inverse of the original's and is deferred as a deliberate improvement, not shipped as fidelity |

A fourth correction, smaller: `AimCandidateSet.Turrets`' doc comment still reads *"Empty in every
build today — turrets are M4"*. `TurretEmplacementRuntime` feeds it and the suites exercise it.
Fix it in passing.

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A2, A3, B14 | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | B11, B12, B13, C21, C22 | The *what* is settled; the *how much* is TUNE — add it to `backlog.md`'s TUNE list, don't invent it as fact. |
| **Leads only — no mechanism yet** | A1, B15, C23, C24, D31 | Budget for investigation; this may end in a disproof. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy. This plan is being executed on
`worktree-targeting`.

## What the data actually ships

Ten original-game screenshots under `OriginalScreenshots/` carry the whole evidence base. All
measurements below were read off them in the 2026-08-15 grilling.

**The seven keybind pages** (`Keybinds <page>.png`, German UI: `UMSCHALT` = Shift, `STRG` = Ctrl,
`ZEHNERTASTATUR` = numpad, `LEER` = Space). The Targeting page in full, at defaults:

| Action | Control A | Control B |
|---|---|---|
| Next Enemy/Objective | `E` | Joystick 3 |
| Previous Enemy/Objective | `Shift+E` | |
| Nearest Enemy/Objective | `Ctrl+E` | |
| Next Ally | `W` | |
| Previous Ally | `Shift+W` | |
| Nearest Ally | `Ctrl+W` | |
| Next Non-Aircraft | `R` | Joystick 6 |
| Previous Non-Aircraft | `Shift+R` | |
| Nearest Non-Aircraft | `Ctrl+R` | |
| Select Target Nearest Crosshairs | `Q` | |
| Target Nothing | `T` | |

Eleven actions over **three classes**, each with next/previous/nearest. The letter-per-class pattern
(`E` enemy, `W` ally, `R` non-aircraft; plain = next, `Shift` = previous, `Ctrl` = nearest) is only
available to the original because **it flies on the arrow keys**: Movement binds Point Nose Down/Up
to Up/Down arrow, Roll Left/Right to Left/Right arrow, Turn Left/Right to `,`/`.`, Level Off to
`Shift+L`. Throttle is the number row (`1`–`9` = 0/8 through 8/8, plus `´`/`ß` for up/down). Weapons
are `Space`/`X` with `F3`–`F6` for the selectors. That leaves the entire letter block free for
targeting — which our port does not.

Also on the pages, relevant here: **Views 1 → `Track Target` = `L`** (free in our flight keymap; our
`L` is the viewer-only livery lab), Snap/Smooth Look `K`/`J`, Cycle Cockpit Views `F8`, and
**Weapons → Cycle guns clockwise `F3` / counterclockwise `F4`, rockets `F5`/`F6`** — direct
corroboration of `BL-357`, and evidence the original has *two* selectors per weapon class where we
have one.

**Full collision audit against our flight keymap:**

| Original action | Key | What we already use it for |
|---|---|---|
| Next / Prev / Nearest Enemy | `E` `Shift+E` `Ctrl+E` | rudder right; + throttle up; + throttle down |
| Next / Prev / Nearest Ally | `W` `Shift+W` `Ctrl+W` | pitch; + throttle up; + throttle down |
| Next / Prev / Nearest Non-Aircraft | `R` `Shift+R` `Ctrl+R` | respawn; throttle up; throttle down |
| Nearest Crosshairs | `Q` | rudder left |
| Target Nothing | `T` | node-name label overlay |
| Track Target | `L` | **free in flight** |

Eleven of eleven collide. Also colliding, for the record: Auto-Dock `A` (roll left), Fire Rockets
`X` (class overlay), Display Scores `Tab` (cycle stunt target), Bail Out `Ctrl+X` (throttle),
External Camera `F10`–`F12` (export / print-pos / screenshot). Free in flight: `I` `J` `K` `L` `M`
`N` `O` `U` `Y` `Z`, `F1`–`F4`, `F6`–`F9`, and the number row.

**The three HUD screenshots.**

`Targeting HUD Kestrel.png` — a selected enemy aircraft at moderate range: a red `[ ]` pair at
**fixed pixel size** straddling the plane's centre (the plane is wider than the brackets, so they
sit *inside* the silhouette), with **`Kestrel`** in red **below** the aircraft. Our marker draws its
tag *above*.

`HUD.png` — the **off-screen** case: a red arrow pointing off the right edge, with `Kestrel` /
`4 o'clock` stacked on **two lines** below it. This is `VersusHud.DrawOpponent`'s edge-arrow branch,
which we believed we invented (see ⚠ table row 1); ours writes one line, the original two.

`C1 M04 Zeppelin.png` — the selected target is a **zeppelin**, labelled over two wrapped lines:

```
        [   ]
Zeppelin [Destroy] -
   Promised Land
```

So an objective target carries its **mission role** (`[Destroy]`) and its **proper name**
(`Promised Land`) — which is why the class is "Enemy/**Objective**", not just "Enemy". The faint red
tick marks on the hull were checked and are **engine geometry, not HUD**.

**Decode entry points already in hand**, from existing `docs/org/` work:

| Symbol | What it is | Source |
|---|---|---|
| `FUN_0041f9c0` | The shared "best target across all four lists" query — *"not part of the assist, but it fixes the lists' priority order"*. Almost certainly what the player's targeting calls | `aim-assist.md:40` |
| `FUN_0041fe10` | The **AI's** target selection, writes `+0x948` — for contrast, not for copying | `aiPilot.md:72,535` |
| `0x004227a0` | The `Target` virtual at vtable `+0x1c` — the **gasbag** target, the one the original's overlay prints `Gasbag targeted` from | `aiPilot.md` |
| `DAT_0071dabc` / `DAT_0071d914` / `DAT_0071d33c` / `DAT_0064f78c` | `TargetVehicle` / `TargetTurret` / `TargetStruct` / `TargetProjectile` — the four typed pools | `aiPilot.md` |

**Our side already has the matching shape.** `AimCandidateSet` (`AimAssist.cs:460`) carries exactly
those four lists — `Vehicles`, `Turrets`, `Structures`, `Ordnance` — modelled deliberately on the
same decode. `ProjectilePool.CollectAircraft` / `CollectTurrets` / `CollectFusedOrdnance` fill them,
and `FlightController.ApplyFireOutcome` (`FlightController.cs:1885`) already builds all four for the
gun aim assist. The player's target pool should read that same structure rather than growing a
parallel one. `Structures` is `DestructibleRegistry`, which its own doc records as an
**approximation** of the original's `targets.zrd` mission-structure list — the reason it stays out
of the cycle.

Ghidra is live for the decode: project `CSVMCrimsonExe`, `crimson.exe` open on `127.0.0.1:8089`.

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

### Wave A — Decode and clear the ground

1. ☑ Decode the original's player targeting into `docs/org/targeting.md`
2. ☑ Drop `A` from `RespawnPressed()` — respawning fires a rocket
3. ☐ Move the node-name label overlay from `T` to the `F13+` debug range

### Wave B — The selection model

11. ☐ `TargetRef` — one abstraction over every selectable thing
12. ☐ The classed candidate pool, including zeppelin sub-parts and turret emplacements
13. ☐ `TargetSelection` — sticky choice, cycles, nearest queries, lifecycle
14. ☐ Input: `D-pad Up` tap/hold, and the curated keyboard set
15. ☐ `--target=` scripted twin

### Wave C — The HUD

21. ☐ Split the targeting marker out of `VersusHud` into `TargetHud`
22. ☐ Draw the original's marker: name label, range-gated brackets, two-line edge tag
23. ☐ `--debug-markers`: keep full identity, add `H78 A91`
24. ☐ One golden screenshot for the geometry

### Wave D — Close out

31. ☐ Docs, backlog spin-offs, and the reserved `L` binding

## Dependency and parallelism notes

A1 blocks B13 and C22 — the selection rules and the bracket range threshold are its output, and
building either before the decode lands is exactly the guessing this plan exists to avoid. A2 and A3
are independent one-file fixes and can land in any order, including before A1.

B11 blocks B12 and B13 (both are written in terms of `TargetRef`). B12 → B13 → B14 is a chain. B15
needs B13. C21 is a file move and should land *before* C22 and C23 so those are written in their
final home. C24 needs C22.

**File contention.** B14, A2 and A3 all edit `FlightController.cs` — do not run them in parallel
worktrees. C21, C22 and C23 all edit the HUD files — same rule; run them in listed order. D31 edits
`controls.md`, `cli.md` and `architecture.md`, which nothing else touches, so it can run alongside
Wave C provided the items it documents have landed.

---

# Wave A — Decode and clear the ground

## A1 ☑ Decode the original's player targeting into `docs/org/targeting.md`

**Landed 2026-08-16.** [`docs/org/targeting.md`](org/targeting.md) answers every question in list
(A) and the bracket gate in list (B). Four results change later items and are called out here so
nobody builds against the pre-decode assumptions:

- **The bracket range gate is not a distance constant.** The box draws when the *selected gun
  group* could reach the target's lead-solved intercept inside its authored `RANGE`
  (`FUN_004574d0`, def `+0x20`), which is `RANGE 1000` for the player guns. C22 must implement a
  weapon-dependent gate, not a metres figure, and the ~50 m hysteresis it proposes is still ours.
- **"Nearest" is the head of the cycle, not the nearest thing.** The cycle sorts objectives first,
  then by 90° sector (ahead, behind, left, right) with distance as the tie-break inside a sector
  (`FUN_004bbd60`). B13 implements that order; "nearest" is `head-of-list`.
- **Nearest-crosshairs scores against the NOSE axis**, a hard 15° half-angle cone with a 2000 m
  cap, not against the pipper (`FUN_00488ce0`). B13's `ImpactReticle` TODO is answered: use the
  nose.
- **Turrets and structures are selectable only when the mission flags them** `otherTarget` /
  `objectiveTarget`, and there is **no sub-part enumeration anywhere in the targeting path**. A
  zeppelin gasbag is selectable because it is its own `MStruct` carrying that flag. B12's
  "enumerate zeppelin sub-parts" is therefore a port invention, not the original's behaviour, and
  should be re-scoped or marked as a deliberate divergence.

Also settled: friendly targets are **green**, not blue (blue is a non-destructive objective);
selection is per-frame re-resolved so target death drops to the head of the cycle; `Target Nothing`
clears the class flags too, which is why it stays cleared; and Next Enemy/Objective walks a queue of
whoever has shot you before it touches the ordinary cycle.

*Original item text follows.*

**Goal.** A `docs/org/` entry that answers how the original picks, holds and drops a player target,
and how it draws one — enough that Waves B and C implement from a decode rather than from
screenshots and inference.

**Evidence (confidence: lead-only).** Entry points are named in "What the data actually ships":
`FUN_0041f9c0` is the strongest (`aim-assist.md:40` calls it the shared best-target query across all
four lists and says it fixes their priority order — "shared" and "not part of the assist" together
point at the player path). `FUN_0041fe10` is the AI's equivalent and is useful as contrast. The
keybind action strings themselves (`"Next Enemy/Objective"`, `"Select Target Nearest Crosshairs"`,
`"Target Nothing"`) should lead to action IDs and their handlers. The gasbag `Target` virtual at
`0x004227a0` is the thread for sub-part targeting. Nothing here has been traced yet — this item *is*
the tracing.

**Approach.** Ghidra is live (project `CSVMCrimsonExe`, `crimson.exe`, `127.0.0.1:8089`). Two halves,
with different bars:

*(A) Selection — hard requirement.* Cycle order within a class (nearest-first? spawn order? screen
order?); what "Nearest" measures (slant range, or something crosshair-weighted); how a class is
decided, in particular whether a zeppelin is Enemy/Objective or Non-Aircraft and where `[Destroy]`
comes from; whether sub-parts are their own pool entries; what "Nearest Crosshairs" scores; what
drops a selection; whether there is an auto-acquire at spawn.

*(B) HUD — hard on one number, best-effort past it.* **The bracket range threshold** is the
requirement; it is the only open value with no substitute, since decision 11 dropped our own 500 m
rule. Then, best-effort: bracket pixel size and whether it scales, the label format rule that
produced `Zeppelin [Destroy] -\nPromised Land`, the wrap width, whether the label is ever drawn
above, and the colours.

Write the findings only. **No C# comes out of this item** — that is a standing constraint from the
grilling, so that Wave B is designed against a complete picture rather than incrementally patched as
fragments arrive. Mark unverified findings the way the other `docs/org/` pages do.

**Model recommendation.** high — decompiler reading is judgement-heavy, the failure mode is a
confident wrong reading, and every later item builds on this one.

**Verify.** The doc answers every question in list (A) or explicitly records which it could not, and
carries the bracket threshold as a number with the function and constant it came from.
**Cross-check used (2026-08-16):** the bracket *geometry* constants were measured off two
independent screenshots. `Targeting HUD Kestrel.png` puts the vertical strokes at x 1943/1963 over
rows 202–218 and `C1 M04 Zeppelin.png` at x 1839/1859 over rows 834–850 — both a 20 x 16 px box
with 4 px arms, matching `0x00607a0c`/`0x00607a14`/`0x00607a10` exactly, and both label blocks sit
at `boxBottom + 3` with a 15 px line pitch, matching `FUN_004574d0`/`FUN_004579e0`. The *range*
gate is a weapon `RANGE` test rather than a constant, so an in-engine `--target=` shot at a measured
range is owed once B15/C24 exist; it is recorded as the open cross-check there.

**⚠ Traps.** (a) **`docs/org/aiPilot.md` is about the AI, not the player.** `FUN_0041fe10` writes
`+0x948` for an AI actor; do not assume the player's selection lives in the same field or follows
the same rules — the AI has a sticky standing target and rating biases the player plainly does not.
(b) **The stop rule is real.** If the 2D render path resists after a genuine attempt, record what was
found and what was not and fall back to measuring the threshold from a screenshot pair — do not sink
a day into a blitter. That is how `tracers.md` and `shadows.md` went. (c) `VersusHud`'s "no reference
to copy" doc comment is **false** (⚠ table row 1) and should be corrected as part of this item's
docs, so the next reader does not repeat the mistake.

## A2 ☑ Drop `A` from `RespawnPressed()` — respawning fires a rocket

**Landed 2026-08-16.** Pressing gamepad `A` to respawn no longer launches a rocket the moment the
plane comes live. `Y` remains the respawn button.

**Evidence (confidence: traced).** `RespawnPressed()` (`FlightController.cs:2225`) read
`KeyDown(Key.R) || PadPressed(JoyButton.Y) || PadPressed(JoyButton.A)`, and `RocketFirePressed()`
(`FlightController.cs:1615`) reads `KeyDown(Key.F) || PadPressed(JoyButton.A)`. `PadPressed` is a
**level** read (`FlightController.cs:2196`), not edge-detected. `RocketFirePressed`'s doc comment
argued the two are safe because they belong to states that never overlap — which is true of the
*states* and false of the *button press*, since `A` is still held on the frame after respawn, when
the plane is live. Reported from live play.

**Approach.** Removed `|| PadPressed(JoyButton.A)` from `RespawnPressed()`. Corrected
`RocketFirePressed()`'s doc comment and the crash-branch comment above it: the reason the two no
longer collide is that `A` is no longer a respawn button, not that their states are disjoint.

**TODO resolved — which paths call `RespawnPressed()`, and does the Dogfight results board need `A`
kept?** Four call sites (`FlightController.cs:1073,1089,1100,1120`): the crash respawn, the solo
stunt-run restart, the race rematch, and the Dogfight results-board rematch. All four ultimately run
through `GameSession.RestartRace`/`RestartMatch` (`GameSession.cs:3172-3189`), and both call
`rig.Controller?.Respawn()` on every rig — the identical "plane goes live this frame, `A` is still
held, `RocketFirePressed` reads it" mechanism as the crash case. **The results board does not need
`A` kept**; keeping it there would reproduce the exact bug this item removes, just on the rematch
path instead of the crash path. `controls.md`'s respawn row also had no pad button documented at
all (a pre-existing gap, since `Y` was never listed) — fixed to `Y` in the same edit.

**Model recommendation.** medium, low effort — a one-line change with a comment correction; the
judgement is in the doc comment, not the code.

**Verify.** With a pad: crash, hold `A` to respawn, and confirm the rocket count is unchanged after
the plane comes live. Confirm `Y` still respawns. **No suite case covers respawn input** — `Suites.cs`
calls `FlightController.Respawn()` directly everywhere it exercises a respawn (e.g. `target.Respawn()`
at `Suites.cs:3303` etc.) and contains no `JoyButton`/`RespawnPressed`/`RocketFirePressed` reference,
so the button-press decoding this item touches is untested by the suite; live pad play is the only
check per the plan's own "what this project cannot verify itself" gap (`docs/verification.md`). This
mirrors A2's own evidence trail: the bug was reported from live play, not caught by the suite, because
the suite never reads a gamepad.

**⚠ Traps.** The stale comment was the actual hazard here — leaving it in place means the next reader
re-derives the same false safety argument. The general shape (a level-read button spanning a state
transition) may bite elsewhere; do not go hunting for it in this item, but it is worth a backlog
note if a second instance turns up.

## A3 ☐ Move the node-name label overlay from `T` to the `F13+` debug range

**Goal.** `T` is free for targeting; the node-name label overlay moves to a function key in the
range `controls.md` already reserves for debug overlays.

**Evidence (confidence: traced).** `controls.md:42` binds `T` to node-name labels under "Any mode",
and `controls.md:44` states outright that `F13`–`F24` is "the first tenant of the range reserved for
debug overlays; the letter-key overlays (`C`/`X`/`T`/…) are to migrate there". `F13` is AI patrol
nets, `F14` frame cost, `F15` the targeting overlay. So this migration is already the documented
intent and this plan simply triggers it for `T`.

**Approach.** Rebind to the next free key in the range — `F16` on current occupancy,
<TODO: confirm `F16` is unclaimed and that Godot delivers it on this platform.> Update
`controls.md`. Leave `C` and `X` where they are: migrating them is the same documented intent but
not this plan's business, and doing them together turns a targeted change into a controls sweep.

**Model recommendation.** medium, low effort — mechanical rebind plus a docs line.

**Verify.** The overlay toggles on the new key in `--fly` and in `--freecam`; `T` does nothing until
B14 lands.

**⚠ Traps.** Whether the host actually receives `F16` is worth checking before committing to it —
some platforms and some keyboards do not produce the high function keys, which is presumably why
`F13`–`F15` were chosen as "reserved" rather than "used". If `F16` does not arrive, pick another key
in the range rather than falling back to a letter.

# Wave B — The selection model

## B11 ☐ `TargetRef` — one abstraction over every selectable thing

**Goal.** A single type answers, for anything the player can target: where are you, are you alive,
what is your display name, what is your health and armor, and which class are you. An enemy Fury, a
zeppelin engine and a turret emplacement are handled identically by every call site.

**Evidence (confidence: direction-sound).** Decision 8 makes the pool span three unrelated C# types
(`FlightController`, zeppelin sub-parts backed by `ZeppelinCannonHealth` / `ZeppelinDamage`, and
turret emplacements via `TurretEmplacementRuntime`). Today `VersusHud` handles exactly one
(`FlightController`) and reaches into it directly — `c.Source is not FlightController fc` in both
`NearestHostile` and `CollectMarks` (`VersusHud.cs:161,183`). Without the abstraction those type
tests multiply across the pool, the cycles, the label formatter and the HUD. The *shape* is settled;
which fields the sub-part health sources can actually supply is not.

**Approach.** Model on the existing `AimCandidate` (`AimAssist.cs:56`), which already solves the same
problem for the aim assist — position, velocity, team, `Live`, and an `object? Source` handed back to
the caller. `TargetRef` is that plus display name, health/armor fractions, and class. Keep it a
value-type-ish record with no Godot node dependency so it is unit-testable without a tree, the way
`NearestHostile` and `CollectMarks` are static and testable today.
<TODO: decide whether `TargetRef` wraps an `AimCandidate` or replaces it for this path — they carry
overlapping fields and duplicating them invites drift.>

**Model recommendation.** high — this is the interface every later item is written against, and
getting the seam wrong is expensive to undo.

**Verify.** Unit tests in `Suites.cs` construct a `TargetRef` for each of the three source kinds from
synthetic data and assert every field reads correctly, with no Godot tree.

**⚠ Traps.** Health is the field most likely to go wrong: `PlaneDamage` exposes `WholeHealth`/
`WholeArmor` against their maxima, but zeppelin sub-parts and turret emplacements have their own,
differently-shaped health, and some selectable things may have none at all. Decision 12 says omit the
figure silently where there is no source rather than printing a misleading `100%` — that means
health/armor must be genuinely optional on `TargetRef`, not defaulted.

## B12 ☐ The classed candidate pool

**Goal.** Each frame (or on demand) the pool yields three lists — Enemy/Objective, Ally,
Non-Aircraft — of `TargetRef`, including one entry per zeppelin gasbag, engine and cannon, and one
per turret emplacement.

**Evidence (confidence: direction-sound).** `AimCandidateSet` (`AimAssist.cs:460`) already carries the
original's four typed lists and `ProjectilePool.CollectAircraft`/`CollectTurrets` already fill two of
them; `FlightController.ApplyFireOutcome` (`FlightController.cs:1885`) builds all four for the gun
assist. So the collection plumbing largely exists. What does not exist is sub-part enumeration:
zeppelin parts are damageable (`ZeppelinDamage.HealthyNodes`, `ZeppelinCannonHealth`) but are not
candidates today. Class assignment — in particular whether a zeppelin is Enemy/Objective or
Non-Aircraft — is **A1's** to answer.

**Approach.** Build on `AimCandidateSet` rather than beside it. Add sub-part enumeration off
`ZeppelinRuntime`. Feed `Turrets` (already populated). **Do not feed `Structures`** — decision 8, and
the reason is in "What the data actually ships". Split into classes by team plus the class rule A1
returns; until A1 lands, leave the class rule behind a single function so it is one edit.

**Model recommendation.** high — spans three subsystems and the correctness bar is "the cycle
contains exactly what it should".

**Verify.** Unit tests over a synthetic pool assert list membership and exclusion — a wingman lands in
Ally not Enemy, a destroyed engine is absent, `Structures` contributes nothing, a zeppelin
contributes its parts. Plus an in-engine count log, the way `ApplyFireOutcome` already prints its
one-time candidate-list breadcrumb (`FlightController.cs:1906`).

**⚠ Traps.** (a) The wingman-in-the-cycle complaint that started this work should already be handled
by `NearestHostile`'s team gate (`VersusHud.cs:155`) — so either the gate is right and a wingman's
`Team` is being set wrong at spawn, or the gate is being bypassed. **Find out which before writing new
filtering**, or the same bug reappears behind a new abstraction.
<TODO: reproduce the wingman-selected symptom and identify the cause.> (b) `AimCandidateSet.Turrets`'
doc comment claiming it is empty in every build is stale — fix it here. (c) Sub-parts multiply the
pool fast; a zeppelin with a gasbag, four engines and six cannons is eleven entries, and several
zeppelins make the Non-Aircraft cycle long. That is the original's behaviour as far as we know, but
watch it in playtest.

## B13 ☐ `TargetSelection` — sticky choice, cycles, nearest queries, lifecycle

**Goal.** The selection state and every rule that changes it, as one module with no Godot
dependency: next/previous/nearest within a class, nearest-to-crosshair across classes, clear, and
the lifecycle.

**Evidence (confidence: direction-sound for the lifecycle, lead-only for the ordering).** Decision 9
fixes the lifecycle: auto-acquire at spawn, switch on target death (confirmed as the original's
behaviour by the user), survive own respawn if the target still lives, and nothing else drops it. The
existence of a "Target Nothing" action is itself the evidence that the selection is sticky — it would
be meaningless against a per-frame re-pick. **Cycle order is A1's to answer** and is not guessed here.

**Approach.** One instance per pane. Expose the current selection as a plain readable property so the
camera (Track Target) and any later AI-order consumer can read it without rework — decision 13. Keep
every query pure over a pool snapshot, matching the existing static, tree-free style of
`NearestHostile` and `CollectMarks`. "Nearest to crosshair" scores against the **`ImpactReticle`
point, not screen centre** — the reticle is deliberately not centred (`ImpactReticle.cs:7`), so
"crosshair" means the pipper. <TODO: confirm against A1 what the original scores — screen-space angle
from the reticle, world-space angle from the nose, or something else.>

**Model recommendation.** high — the rules are subtle, the state is per-pane, and this is the module
everything else reads.

**Verify.** Unit tests over a synthetic pool: the full cycle order in one assertion; death of the
selected target advances to the expected next; own respawn preserves a live selection and re-acquires
a dead one; an explicit clear stays cleared until the next selection input; range, bearing and LOS
changes never drop a selection.

**⚠ Traps.** The explicit clear must **stay** cleared — auto-acquire at spawn and auto-advance on
death are the only two automatic transitions, and if a third creeps in ("nothing selected, so pick
one") then `Target Nothing` silently stops working. Test that case specifically.

## B14 ☐ Input: `D-pad Up` tap/hold, and the curated keyboard set

**Goal.** `D-pad Up` tap steps to the next enemy; holding it past 250 ms selects the target nearest
the crosshair. `T` `Y` `U` `I` `O` do next-enemy / next-ally / next-non-aircraft / nearest-crosshair /
target-nothing. `D-pad Down` stays unbound.

**Evidence (confidence: traced, for the collisions).** The full audit is in "What the data actually
ships": all eleven of the original's targeting keys collide with us, because our throttle is
`KeyAxis(Key.Shift, Key.Ctrl)` (`FlightController.cs:2449`) and our flight keys are WASD + `Q`/`E`.
Free pad inputs are `D-pad Up`, `D-pad Down`, `Back` and `L3` — the rest are taken (`A` rockets +
respawn, `B` guns, `X` cycle stunt target, `Y` respawn, `Start` pause, shoulders rudder, triggers
throttle, `D-pad ←/→` weapon selectors, `R3` look back). `HoldToRepeat` (`src/Utils/HoldToRepeat.cs`)
is the existing tap-vs-hold primitive.

**Approach.** Tap resolves **on release** (decision 7): release before 250 ms → next enemy; crossing
the threshold while held → nearest-crosshair fires at that instant and the release does nothing.
`HoldToRepeat` takes an initial delay already; a zero repeat interval plus a released-before-delay
test gives this without new machinery. Mark 250 ms as **TUNE**. Feed edges into `TargetSelection`;
`FlightController` should carry input reading only, not selection logic. Keyboard keys are defaults,
not a scheme — they become rebindable when that item lands.

**Model recommendation.** medium — mechanical once the semantics are fixed, but it edits
`FlightController.cs`, which A2 and A3 also touch.

**Verify.** In `--fly` with a pad: tap cycles enemies, hold selects the plane nearest the pipper
including a friendly, `D-pad Down` does nothing. On keyboard, each of the six keys performs its
action. Confirm holding `D-pad Up` does not also fire a tap on release.

**⚠ Traps.** (a) Splitscreen: input must read **this pane's own** device list via `Pads.For(PadDevices)`
the way `PadPressed` does (`FlightController.cs:2196`) — never `pads[0]`, and P2–P4 are pad-only
(`KeyDown` is gated on `UseKeyboard`). Four panes each need their own selection. (b) `T` is only free
once **A3** lands. (c) Do not reach for `Shift`/`Ctrl` combos — that is ⚠ table row 2, and it is the
exact mistake this plan already made once.

## B15 ☐ `--target=` scripted twin

**Goal.** A screenshot or `--det` run can pin the initial selection without a human, so a golden
shot is reproducible.

**Evidence (confidence: lead-only).** The repo's convention is that every interactive input has a
scripted twin — `--gun-select=`, `--view=`, `--fire`, `--weapon-click=` (`cli.md`). Decision 17 chose
a flag naming the initial selection over a scripted key sequence, because the cycle is better
asserted by a unit test than driven by timed keypresses. The exact grammar is not settled.

**Approach.** `--target=nearest|crosshair|next|none`, plus `--target=<node name>` to pin a specific
aircraft (`ai2_player_fury`) — the node-name form is what makes C24's golden reproducible.
<TODO: settle the grammar for pinning a zeppelin sub-part, which has no node name of the same
shape.> Follow `--gun-select=`'s pattern for a headless selector hook.

**Model recommendation.** medium — a CLI flag over an existing seam.

**Verify.** A `--screenshot` run with `--target=ai2_player_fury` puts the marker on that aircraft
deterministically across two runs.

**⚠ Traps.** The flag sets the *initial* selection; it must not pin it against later input, or an
interactive session started with the flag would have targeting frozen.

# Wave C — The HUD

## C21 ☐ Split the targeting marker out of `VersusHud` into `TargetHud`

**Goal.** `VersusHud` is the VS status line, the kill banner and the VS opponent markers — the things
it is named for. The targeting marker, the hostile tracker and `--debug-markers` move to their own
HUD.

**Evidence (confidence: direction-sound).** `VersusHud` currently does four jobs and its module doc
runs three paragraphs of "also this"; the H22 hostile tracker and `--debug-markers` were both bolted
on (`VersusHud.cs:28-40`). The marker draws in **every** flight session, not only `--vs`
(`BuildHostileTracker`, `VersusHud.cs:132`), so it is not a Versus feature at all. Decision 16.

**Approach.** A file move plus a wiring change, done **before** C22 and C23 so those are written in
the final home. `VersusHud` keeps `StatusLine`, the banner and the `Rigs` opponent loop; `TargetHud`
takes `TrackedHostile`, `MarkAll`, `Own`, `HostilePool` and the drawing helpers. Shared drawing
primitives (`DrawTag`, `DrawArrow`, `EdgePoint`, `ClockHour`, `DrawOpponent`) are used by both —
<TODO: decide whether they move to a shared helper or are duplicated; duplicating invites drift, but
a shared base class for two `Control`s has its own cost.>

**Model recommendation.** medium — mechanical, but it touches `FlightRigAssembler` wiring and the
suites that build a `VersusHud` and read `TrackedHostile`.

**Verify.** Existing suites pass with the rename; a `--vs` session and a plain `--fly` session both
draw what they drew before the split.

**⚠ Traps.** The suites reach for `VersusHud.TrackedHostile`, `NearestHostile`, `CollectMarks`,
`HostileTag` and `ModeSuffix` — they all move, and the test references move with them. Correct the
"no reference to copy" claim (⚠ row 1) while rewriting the module doc.

## C22 ☐ Draw the original's marker

**Goal.** The selected target reads like the original: the plane's common name below it, fixed-size
red brackets when within the range threshold and none beyond it, and off screen an edge arrow with
the tag and clock bearing on two lines. Blue for a friendly, red for a hostile.

**Evidence (confidence: direction-sound; the threshold is A1's).** `Targeting HUD Kestrel.png` gives
fixed-size red brackets and a red name label below the plane. `HUD.png` gives the edge arrow with a
two-line `Kestrel` / `4 o'clock` — our `DrawOpponent` edge branch already does this in one line
(`VersusHud.cs:384`). `C1 M04 Zeppelin.png` gives the `<type> [<role>] -\n<proper name>` format
wrapped to two lines. The label is **always below** (decision 11; the user could not reproduce an
above case). Today's marker draws the tag *above* (`RefOnScreenLift`, `VersusHud.cs:83`).

**Approach.** Name comes from `PlaneRoster.PlaneDisplayName(stats)`, which already yields
`Fury`/`Kestrel`/`Bloodhawk` — this needs a public accessor on `FlightController`, whose `_model` is
private (`FlightController.cs:422`). **Stop parsing the node name**: `HostileTag` cuts
`"ai1_player_fury"` at the first `_` to get `"AI1"` (`VersusHud.cs:199`), which is both wrong for the
shipped label and unnecessary when the stats carry the name. Flip the label below. Prepare the full
`[role] - proper name` format as empty-tolerant so it renders bare until objectives exist. Brackets
draw only inside the threshold, with **~50 m of hysteresis** so a target hovering at the boundary does
not strobe — hysteresis is ours, not the original's, and is TUNE. Keep `P1`/`P2` for VS opponents
(decision 10). Mark **one** selected target plus all VS opponents (decision 11).

**Model recommendation.** high — several rules interacting, and the visual result is the deliverable.

**Verify.** C24's golden, plus a by-eye comparison against `Targeting HUD Kestrel.png` and `HUD.png`
at comparable ranges.

**⚠ Traps.** (a) The bracket range threshold is **A1's output** — do not substitute the user's 500 m
figure, which is from the deferred modernization and is the *inverse* rule (⚠ row 3). (b) Fixed
pixel size means scaling by `HudMetrics.Scale` for resolution but **not** by range. (c) The two-line
edge tag needs the stagger logic (`RefStaggerStep`, `VersusHud.cs:383`) rechecked — two lines are
taller, so the existing spacing may overlap.

## C23 ☐ `--debug-markers`: keep full identity, add `H78 A91`

**Goal.** The debug overlay reads `AI1 Fury 640 m H78 A91 pursue` — identity, plane type, range,
health, armor, AI mode.

**Evidence (confidence: direction-sound).** `PlaneDamage` exposes `WholeHealth`/`WholeHealthMax` and
`WholeArmor`/`WholeArmorMax` (`PlaneDamage.cs:55-62`). The decoded kill rule is **`WholeHealth <= 0`
alone** (`PlaneDamage.cs:79`) — armor never keeps a plane alive, which is why decision 12 rejected a
combined figure: a plane with full armor and 10% health reads ~55% combined at exactly the moment the
number matters most. Two separate figures, health first, no percent sign.

**Approach.** Extend the existing `MarkAll` string (`VersusHud.cs:300`), which already carries tag,
range and `ModeSuffix`. Keep `HostileTag`'s `AI1` here — identity is the whole point of the overlay
(decision 10), and it is `HostileTag`'s only remaining caller once C22 stops using it. Read
health/armor through `TargetRef` so sub-parts and emplacements are covered, and **omit both figures
silently** where there is no health source rather than printing `H100 A100`.

**Model recommendation.** medium — a string change over an existing seam.

**Verify.** A `--fly --debug-markers` run with damaged AI shows the figures falling; a plane at zero
health is gone from the overlay before `H0` could be displayed. <TODO: confirm whether a plane can be
observed at low health long enough to read the figure, or whether the damage lab is the better
instrument.>

**⚠ Traps.** This is the one place the full identity string survives; do not "simplify" it to match
the shipped marker. Its own doc already says it is never a gameplay feature.

## C24 ☐ One golden screenshot for the geometry

**Goal.** A pinned golden shot covering the parts no unit test can reach: bracket geometry, the range
threshold, label placement, and the debug string.

**Evidence (confidence: direction-sound).** Decision 17 chose exactly one golden. The pure logic
(pool, cycles, lifecycle, label formatting, health read) is unit-tested in B11–B13, and the
off-screen arrow case rides those tests since `HUD.png` shows our existing branch already matches.

**Approach.** `--target=<node name>` (B15) pins the subject; frame a selected enemy **inside** the
bracket range showing brackets, name label and the `--debug-markers` string. Follow
`analysis/goldens/manifest.json` conventions — and note the repo hook: the `exercises` field is
capped at 250 chars, carries no item id, no date and no "also exercises" clause, and is **rewritten**
on a re-pin, never appended to.

**Model recommendation.** medium — following an established pinning convention.

**Verify.** Two runs produce an identical image; the shot visibly shows brackets, label and debug
string.

**⚠ Traps.** <TODO: pick the world, chapter and pose, and confirm the AI spawn is deterministic
enough for a stable golden — a re-pinned golden that drifts every run is worse than none.>

# Wave D — Close out

## D31 ☐ Docs, backlog spin-offs, and the reserved `L` binding

**Goal.** The documentation matches what landed, the four deferred features exist as backlog items,
and `L` is reserved for Track Target so nothing else claims it.

**Evidence (confidence: lead-only).** The deferrals are decisions 5, 8, 11 and 15; the docs list is
the repo's standing convention that `PROJECT_CONTEXT.md`, `docs/architecture.md` and the relevant
`docs/` pages update in the same turn as the item.

**Approach.** Update `controls.md` (new binds, the `T` → `F13+` move, `A` dropped from respawn, `L`
reserved), `cli.md` (`--target=`, the extended `--debug-markers`), and `architecture.md` (the new
modules and the `VersusHud` split). Bind `L` to nothing but document it as reserved (decision 15).
Mint four backlog items: **rebindable keymap** (the real answer to key placement; note that all
eleven original binds collide), **Track Target camera** (naming the open camera questions),
**curated mission-target list** (unblocks `Structures` as a selectable class), and **modernize the
target marker** (holding the brackets-past-500 m inversion, and recording that it is the *opposite*
of the original's rule so nobody "fixes" it back).

Also correct in passing: `AimCandidateSet.Turrets`' stale "empty in every build today" comment, and
add the `BL-357` corroboration from the Weapons keybind page (`F3`/`F4` guns, `F5`/`F6` rockets —
the original has two selectors per weapon class where we have one).

**Model recommendation.** medium — documentation and backlog authoring, judgement in the framing.

**Verify.** `controls.md` matches the shipped binds exactly; each new `BL-` id is unique (the commit
hook checks this); `PROJECT_CONTEXT.md`'s "Current status" points at this plan.

**⚠ Traps.** The commit hooks bite here: duplicate `BL-` ids fail the commit, and any double-encoded
UTF-8 in a changed text file fails it too. Write docs with the Read/Edit/Write tools, never a
PowerShell `Get-Content`/`Set-Content` round-trip.
