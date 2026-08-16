# Player target selection — decode, then a selectable target marker

**COMPLETE 2026-08-16** (written 2026-08-15; executed 2026-08-16). All 11 checklist items are
☑, Waves A–D. Indexed in [`plans.md`](plans.md); read as history.

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
instead (`BL-394`). (b) **Track Target's camera behaviour** — the `L` binding is reserved
and documented here, but "keep the target framed" hides a pile of camera decisions (snap vs smooth,
override vs blend with chase, behaviour with no target or a target behind you, interaction with the
E42 right-stick free look) that are camera work, not targeting work (`BL-395`).
(c) **`Structures` / `DestructibleRegistry` as a selectable class** — the original's Non-Aircraft
cycle walks a curated `targets.zrd` mission-structure list; ours would walk every crate and fence in
the world, producing a cycle nobody would use. Held until a curated list exists
(`BL-396`). (d) **The modernized marker** — the user's own preferred rule (brackets only
*past* 500 m, the inverse of the original's) is deliberately not built; the original's behaviour
ships first and the improvement is a later, separate call (`BL-397`).

**Backlog provenance.** No item here is drawn from `backlog.md`, so no re-verification pass is owed.
Two adjacent existing entries are *touched* rather than closed: `BL-357` (the original steps weapon
selectors both ways) gains direct corroboration from the Weapons keybind page, and `BL-363` (the AI
candidate pool is aircraft-only) shares the pool-widening work but is not closed by this plan.
**Re-verified 2026-08-16 (D31):** `git log --grep=BL-357` and `--grep=BL-363` each return only the
commit that *wrote* the entry (`b5f6e68d` for `BL-357`, one entry point of this plan itself; `3d94d7d9`
"Decode BL-363's candidate pool…" for `BL-363`) — no closing commit for either, and both are still
present, open, in `backlog.md`. `BL-357` gained its corroboration in this item; `BL-396` (new, D31)
is what actually closes the door on `BL-363`'s pool-widening half by scoping the equivalent
Non-Aircraft gap in the player's own targeting to "needs a curated list", the same shape `BL-363`
itself already argues for on the AI side.

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
3. ☑ Move the node-name label overlay from `T` to the `F13+` debug range

### Wave B — The selection model

11. ☑ `TargetRef` — one abstraction over every selectable thing
12. ☑ The classed candidate pool, including zeppelin sub-parts and turret emplacements
13. ☑ `TargetSelection` — sticky choice, cycles, nearest queries, lifecycle
14. ☑ Input: `D-pad Up` tap/hold, and the curated keyboard set
15. ☑ `--target=` scripted twin

### Wave C — The HUD

21. ☑ Split the targeting marker out of `VersusHud` into `TargetHud`
22. ☑ Draw the original's marker: name label, range-gated brackets, two-line edge tag
23. ☑ `--debug-markers`: keep full identity, add `H78 A91`
24. ☑ One golden screenshot for the geometry

### Wave D — Close out

31. ☑ Docs, backlog spin-offs, and the reserved `L` binding

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

## A3 ☑ Move the node-name label overlay from `T` to the `F13+` debug range

**Landed 2026-08-16.** `NodeLabels` now binds `Key.F16` instead of `Key.T`; the in-scene HUD
breadcrumb reads `node labels [F16]: …`. `T` is free.

**TODO resolved — is `F16` unclaimed, and does Godot deliver it on this platform?** Unclaimed:
`grep`ping the whole tree for `Key.F1[3-9]` / `Key.F2[0-4]` before this edit found only `F13`
(`AiNetsOverlay.cs:123`), `F14` (`PerfHud.cs:113`) and `F15` (`TargetingOverlay.cs:81`) bound —
no `Key.F16` anywhere in `src/`. `docs/architecture.md:167` confirms `F16` was previously used by
`TileGridOverlay` and was explicitly freed by `PLAN-perf-hitches` A1; `docs/cli.md:189` still called
it live (`--map-edge-mode=mirror` toggle) but that line was stale from before that removal, not a
live claim — no code binds it. Delivered on this platform: `F13`–`F15` are already live, working
binds in this exact build, in the same contiguous Godot `Key` enum range as `F16`; there is no
platform reason the next key in that range would be withheld when its neighbours are not.

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
**Verified (2026-08-16):** `--screenshot` runs with `--debug-names=meshes` in `--viewer`, `--fly`
and `--freecam` all show the HUD breadcrumb reading `node labels [F16]: …`, confirming the overlay
still builds and renders correctly through the same `SetMode`/`Refresh` path the key handler calls.
`grep`ping `NodeLabels.cs` for `Key.T` after the edit returns nothing, so `T` is genuinely inert for
this overlay. `dotnet build` is clean. Pressing `F16` interactively was not exercised — `--debug-names`
presets `InitialMode` directly rather than going through `_UnhandledKeyInput`, and there is no scripted
twin for a bare key press (the same gap A2 hit for gamepad buttons); a live `F16` press is owed once a
human is at the keyboard, but the input path is textually identical to `F13`–`F15`, which are already
live, working binds in this build.

**⚠ Traps.** Whether the host actually receives `F16` is worth checking before committing to it —
some platforms and some keyboards do not produce the high function keys, which is presumably why
`F13`–`F15` were chosen as "reserved" rather than "used". If `F16` does not arrive, pick another key
in the range rather than falling back to a letter.

# Wave B — The selection model

## B11 ☑ `TargetRef` — one abstraction over every selectable thing

**Landed 2026-08-16.** [`CSVM/src/Flight/TargetRef.cs`](../CSVM/src/Flight/TargetRef.cs) plus the
tree-free `target-ref` suite. Three things later items should build against rather than re-decide:

- **`TargetRef` wraps an `AimCandidate`; it does not restate it** (the TODO below, resolved). The
  five shared facts (position, velocity, team, liveness, source) stay in the candidate and are read
  through forwarding properties, so there is no second copy to drift and the existing collectors
  feed it unchanged. `AimCandidate.ConeOverride` rides along unused, since it is the same entity's
  data rather than a duplicated field.
- **`TargetRef.Classify` is the decoded class model**, `FUN_004b5cd0`'s order verbatim, returning
  null for "not selectable at all". B12 calls it rather than writing its own split. Objective is not
  a fourth class: it returns `Enemy` (the cycle objectives ride with `-too` off) and the caller
  records the companion flag.
- **Health and armor are nullable and never defaulted.** The three shipped sources genuinely differ
  (aircraft both, structure health only, turret emplacement neither), so C23's `H78 A91` must omit a
  missing figure rather than print a full bar.

Also settled in passing: `AimTargetKind` is reused for "which pool" rather than minting a second
enum, and identity is the source object (`IsSameTarget`), because the original re-finds its
selection by underlying entity every frame.

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
**TODO resolved — wraps, does not replace.** The five overlapping fields are not incidentally
similar, they are the *same reads off the same four pools*: `ProjectilePool.CollectAircraft` /
`CollectTurrets` and `AimCandidateSet.AddStructures` already produce them, and B12 is instructed to
build on `AimCandidateSet` rather than beside it. Replacing would mean a second collector writing a
second copy of position/velocity/team/liveness/source, which is exactly the drift the TODO names;
wrapping makes it structurally impossible. The cost is that a caller constructing a `TargetRef` must
build its `AimCandidate` first, which the suite does and which is honest about where those facts come
from. The targeting-only fields (class, labels, health) live on `TargetRef` alone, and the assist's
`ConeOverride` is left on the candidate untouched rather than promoted or dropped.

**Model recommendation.** high — this is the interface every later item is written against, and
getting the seam wrong is expensive to undo.

**Verify.** Unit tests in `Suites.cs` construct a `TargetRef` for each of the three source kinds from
synthetic data and assert every field reads correctly, with no Godot tree.
**Verified (2026-08-16):** the `target-ref` suite builds one ref per source kind (aircraft, zeppelin
sub-part, turret emplacement) from synthetic `AimCandidate`s and plain `object` sources (no plane is
built, no pool registered, no data root required) and asserts the forwarded pose/team/liveness/source,
the own fields, the four label formats, the optional health/armor spread (both / health only / neither,
plus `Fraction`'s zero-maximum and clamp cases), `Classify`'s full order including the two "not
selectable" cases and the dead-objective case, and source identity across a rebuilt ref. **Able to
fail:** flipping the turret's `Health == null` assertion to `== 1f` (the exact defaulting mistake
decision 12 forbids) turns the suite FAIL and exit 1; restored, it passes. Full run on the primary
tree's data root: `RunTests.ps1 -SkipGoldens -SkipHitch` = 1378/1378 units, 65/65 in-engine suites,
engine errors clean, `dotnet build` clean of new warnings. No 8-chapter freecam regression is owed
here: the item adds a type and a suite and changes no shipped call site, so no rendered path moved.

**⚠ Traps.** Health is the field most likely to go wrong: `PlaneDamage` exposes `WholeHealth`/
`WholeArmor` against their maxima, but zeppelin sub-parts and turret emplacements have their own,
differently-shaped health, and some selectable things may have none at all. Decision 12 says omit the
figure silently where there is no source rather than printing a misleading `100%` — that means
health/armor must be genuinely optional on `TargetRef`, not defaulted.

## B12 ☑ The classed candidate pool

**Landed 2026-08-16.** [`CSVM/src/Flight/TargetPool.cs`](../CSVM/src/Flight/TargetPool.cs), plus
`ZeppelinRuntime.CollectTargetParts` and the one-line team fix trap (a) turned up. Four things later
items should build against rather than re-decide:

- **The pool reads `Vehicles` and `Turrets` and NOTHING else.** `Structures` is never walked, so
  feeding a scan `AddStructures(registry)` cannot leak a crate into the cycles; selectable
  structures arrive through `Rebuild`'s separate `subParts` argument. That turns "do not feed
  `Structures`" from a wiring convention into a property of the pool, which the suite proves by
  populating `Structures` and asserting nothing comes out. `Ordnance` is not walked either.
- **`Rebuild` takes the selecting plane's `Team` FIELD.** See trap (a) below: the derivation is the
  bug, and the suite keeps it as a named able-to-fail CONTROL.
- **Sub-part enumeration ships as a marked divergence**, per A1's finding. Zeppelin gasbags, engines
  and cannons are enumerated off the F18 zones, each carrying the hull's velocity so C22's bracket
  gate has something to lead.
- **Turret emplacements are selectable; carried gunners are not.** A carried gunner's host is already
  a target, so offering both would put two entries on one silhouette. The discriminator is
  `TurretController.Site`, and B13's cycles get one entry per emplacement.

The aircraft display name is the plain node name for now; C22 replaces it with the airframe's common
name, which needs an accessor `FlightController` does not have yet. Trap (b) is fixed: the
`AimCandidateSet.Turrets` doc comment no longer claims the list is empty.

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

**As built:** the class rule is B11's `TargetRef.Classify`, so the pool writes no split of its own.
"Do not feed `Structures`" became "the pool does not READ `Structures`", which is the stronger form
and is directly testable. `Turrets` is filtered to emplacements: `ProjectilePool.CollectTurrets`
fills that list with carried gunners as well, and a carried gunner's host is already a target.

**Model recommendation.** high — spans three subsystems and the correctness bar is "the cycle
contains exactly what it should".

**Verify.** Unit tests over a synthetic pool assert list membership and exclusion — a wingman lands in
Ally not Enemy, a destroyed engine is absent, `Structures` contributes nothing, a zeppelin
contributes its parts. Plus an in-engine count log, the way `ApplyFireOutcome` already prints its
one-time candidate-list breadcrumb (`FlightController.cs:1906`).
**Verified (2026-08-16):** the new `target-pool` suite covers all four in its world-free half, over a
hand-built `AimCandidateSet` with bare `FlightController`s and `DestructibleRegistry.Instance`s: the
wingman and a neutral land in Ally while the hostile-team plane is the only Enemy, the selecting plane
is excluded from its own pool, a dead plane and a destroyed engine are absent, the registry and the
ordnance list contribute nothing though both are populated, and the live gasbag arrives with its
hull's velocity, its part-node name and health with no armor. Its world half then runs C1's **real**
74-emplacement census through the same pool and asserts every one lands on Non-Aircraft with no health
figure; that census note is this item's count log, since the pool has no live call site until B13 owns
an instance. ⚠ That half asserts a *shape* (`> 0`, and every entry Non-Aircraft), not a hard count, on
purpose: the note reads 74 of 74 alive when the suite runs alone and 73 when the whole set runs, so an
earlier suite in the same process leaves one emplacement dead. A pinned number there would be a flake. The carried-gunner exclusion rides the existing `turret-gunner` suite, where a real
carried turret already exists. `hostile-marker-hud` gains the trap (a) regression.
**Able to fail:** two named CONTROLs are permanent parts of the suites — re-deriving P2's side from
its pilot index puts the wingman in Enemy and drops the real enemy (`target-pool`), and the same
derivation makes `NearestHostile` track the wingman (`hostile-marker-hud`). Plus a one-off flip:
adding a `scan.Structures` walk back into `Rebuild` turns `target-pool` FAIL and exit 1; removed, it
passes. Full run: 1378/1378 units, 66/66 in-engine suites, engine errors clean, `dotnet build` clean
of new warnings. No freecam regression is owed — the pool has no call site, and the only shipped
behaviour that moved is the HUD team read, which the suite covers.

**⚠ Traps.** (a) The wingman-in-the-cycle complaint that started this work should already be handled
by `NearestHostile`'s team gate (`VersusHud.cs:155`) — so either the gate is right and a wingman's
`Team` is being set wrong at spawn, or the gate is being bypassed. **Find out which before writing new
filtering**, or the same bug reappears behind a new abstraction.

**TODO resolved — the gate is right and was being bypassed.** Not a spawn problem: `AiAircraftSpawner`
sets every wingman to `AimAssist.PlayerTeam` and the suite already pinned that
(`Suites.cs:3812`). The bypass is `VersusHud`, which derived the pane's OWN side per call as
`AimAssist.TeamOfPilot(PlayerIndex)` (`UpdateHostile`, and `CollectMarks` under `--debug-markers`)
instead of reading `FlightController.Team` — the field `architecture.md`'s own AimAssist entry says
every consumer must read, precisely because "shooter ids are not team ids".

The arithmetic: `TeamOfPilot(0)` is 1, which *is* `AimAssist.PlayerTeam`, so **P1 was correct by
coincidence**. `FlightRigAssembler.cs:119` puts every human on `PlayerTeam` in Instant Action and
under `--coop`, so **P2 derived team 2** — `InstantActionRuntime.EnemyTeam`. Its own wingmen (team 1)
then failed the same-team test and read hostile, while the real enemies (team 2) matched and were
skipped as own-team. So P2 tracked a wingman and could not track an enemy at all, and under
`--debug-markers` the colours were inverted with it. Fixed here: `VersusHud.OwnTeam` reads
`Own?.Team`, `FlightRigAssembler` binds `Own` on every pane rather than only under
`--debug-markers`, and `TargetPool.Rebuild` takes the team as a parameter documented as the field.

(b) `AimCandidateSet.Turrets`' doc comment claiming it is empty in every build is stale — fix it
here. **Done.** (c) Sub-parts multiply the
pool fast; a zeppelin with a gasbag, four engines and six cannons is eleven entries, and several
zeppelins make the Non-Aircraft cycle long. That is the original's behaviour as far as we know, but
watch it in playtest.

## B13 ☑ `TargetSelection` — sticky choice, cycles, nearest queries, lifecycle

**Landed 2026-08-16.** [`CSVM/src/Flight/TargetSelection.cs`](../CSVM/src/Flight/TargetSelection.cs).
Four things B14 and Wave C should build against rather than re-decide:

- **Handlers mutate; `Resolve` publishes.** An action only changes the class and the selection
  identity and steps the list that already exists; the per-frame `Resolve` re-sorts and re-finds the
  selection by entity, falling back to the list head. That one fallback IS the whole lifecycle — the
  auto-acquire, the switch on death, and the drop when a target leaves the class are the same failed
  re-find. B14 should call the handlers on input and `Rebuild` once per frame, and must not rebuild
  inside a handler (see the one-frame-lag note below).
- **`Nearest` is head-of-cycle**, not nearest-in-space, and it always restarts the cycle where
  Next/Previous only reset on a class change.
- **Nearest-crosshairs scores the NOSE**, 15° half-angle, 2 km hard cap, friendlies included, and it
  writes the class back from what it picked.
- **`TargetSelection` owns the `TargetPool`** and prints the `target pool:` count breadcrumb, closing
  what B12 deferred. Nothing in a live session constructs one yet — that wiring is B14's.

`RecordAttacker`/`ForgetTarget` exist and are tested, but nothing calls them yet: the damage path and
the death hook are owed, and are the natural companions to B14's binds.

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
"crosshair" means the pipper.

**TODO resolved — it is the NOSE, and this paragraph's pipper reading is wrong.** A1's decode
(`FUN_00488db0`/`FUN_00488ce0`, `docs/org/targeting.md` "Select Target Nearest Crosshairs") is
explicit: the scan tests `dot(v, row2) > d · −cos(15°)` against the plane's own negated forward axis
and scores the survivor by plain slant range, with the running best seeded at 2000.0 — so a hard 15°
half-angle **nose** cone and a hard 2 km cap. Nothing in that path reads the pipper, which is a
separate velocity-derived point (`docs/formats/hud.md`, `aim-assist.md`). Two further findings the
paragraph above predates: the action **ignores the class flags and the candidate list entirely** and
runs its own scan over every pool, **friendlies included** (that is how one keypress reaches an ally);
and having chosen, it writes the class back from what it found, so a following Next/Previous continues
in that target's own cycle. `ImpactReticle` is not touched by this item at all.

**Model recommendation.** high — the rules are subtle, the state is per-pane, and this is the module
everything else reads.

**Verify.** Unit tests over a synthetic pool: the full cycle order in one assertion; death of the
selected target advances to the expected next; own respawn preserves a live selection and re-acquires
a dead one; an explicit clear stays cleared until the next selection input; range, bearing and LOS
changes never drop a selection.
**Verified (2026-08-16):** the `target-selection` suite, tree-free and data-free, over plain `object`
sources through the real `TargetPool`. The geometry is chosen so the decoded sector order and a plain
range order **disagree** — the nearest candidate is 100 m off the right wing and the head of the cycle
is an objective 1500 m *behind* — so a pool sorted by distance cannot pass. It covers the whole order
in one `SequenceEqual`, `SectorKey` against the decode's own table, the auto-acquire on a selector that
has never resolved, Next/Previous stepping and wrapping both ways, `Nearest` returning to the head
rather than to the 100 m target, target death dropping to the **head** and not to the dead entry's
neighbour, a rebuild preserving a live selection (own respawn), a 7 km move plus a 135° yaw re-sorting
the cycle without dropping the selection, `Target Nothing` staying cleared through three rebuilds and
ending only on a class action, nearest-crosshairs reaching an **ally** dead ahead with its class
written back plus the two rejections (past 2 km on the nose, and 45° off it), and the attacker queue
walked backwards through all three branches plus the death prune.
**Able to fail:** replacing the sector key with a constant, i.e. sorting purely by range, turns the
suite FAIL and exit 1 on seven checks including the order and the auto-acquire; restored, it passes.
The one-frame handler/resolve split is asserted directly (`Current` still reads the old target until
the next `Resolve`), so a synchronous rebuild inside a handler would show up as a failure rather than
silently.
Full run: 1378/1378 units, 67/67 in-engine suites, engine errors clean, `dotnet build` clean of new
warnings. The `target pool: enemy=… ally=… nonAircraft=… class=… acquired=…` breadcrumb prints. No
freecam regression is owed — nothing in a live session constructs a `TargetSelection` yet.

**⚠ Traps.** The explicit clear must **stay** cleared — auto-acquire at spawn and auto-advance on
death are the only two automatic transitions, and if a third creeps in ("nothing selected, so pick
one") then `Target Nothing` silently stops working. Test that case specifically.

## B14 ☑ Input: `D-pad Up` tap/hold, and the curated keyboard set

**Landed 2026-08-16.** This is the item that first puts targeting in a live session, so it carries
the wiring as well as the binds. `FlightRigAssembler` builds one `TargetSelection` per human pane;
`FlightController.StepTargeting` feeds it every frame; `GameSession` binds the zeppelin sub-part feed
once the zeppelins exist (they are built after the rigs, which is why it is a delegate and not a
runtime reference). A live `--fly --chapter=C1 --zeppelins --ai=player_fury` run now prints
`target pool: enemy=1 ally=0 nonAircraft=99 class=Enemy acquired=ai1_player_fury` — the auto-acquire,
the 74 C1 emplacements and the 25 zeppelin parts, all through the real path.

Five things later items should build against:

- **The tap/hold decision is its own module.** `Utils/TapHoldButton` owns the timing and the
  resolve-on-release rule, so the *decoding* is unit-tested even though the device read is not. Reuse
  it for any later two-action button rather than hand-rolling a timer.
- **Input is gated on `InPlay`; the rebuild is not.** A downed pilot watches from the freecam
  controls, which bind `U` among WASD/QE — reading targeting keys from a spectator would fight the
  camera. The selection keeps re-resolving, so it survives the pilot's own respawn.
- **The attacker queue is wired** in `TakeProjectileHit`, through the new
  `ProjectilePool.RigOfShooter`, behind the engine's own different-and-non-zero-team gate.
  `ForgetTarget` is called from `StepTargeting`'s own prune rather than from a session-wide death
  broadcast, because outside `--vs` no such broadcast exists.
- **`sg_switchtarget` cannot ship, and that is a data finding, not a preference.** The string is in
  the executable; the name appears nowhere in `extracted/zrdr/sounds.zrd.json` (whose entries are all
  `snd_*`) nor among the 2521 assets in `extracted/soundsh/`. `snd_select` exists and is a plausible
  candidate, but no resolution to it has been traced, so choosing it would be an invention. Targeting
  ships silent.
- **250 ms is TUNE**, ours not the original's — the original needs no threshold because it has a key
  per action.

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
**Verified (2026-08-16), and what is NOT.** The suite reads no gamepad and no bare key press — the
same gap A2 and A3 both recorded — so the split is explicit:

*Pinned by tests.* The new `target-input` suite covers everything between the device read and the
action. `TapHoldButton`: a 0.20 s press taps once on release and never holds; a 0.30 s press holds
once and the release is **spent**, so the hold does not also fire a tap (the exact thing this item's
Verify step asks a human to check); a two-second press still fires exactly once, so it is a tap/hold
split and not a key-repeat; the next press taps again, so a hold leaves no state behind; and a button
that is simply up reports nothing. The attacker queue is exercised through the real path — three
rigs in a live pool, `TakeProjectileHit` called with each shooter id — proving `RigOfShooter` resolves
an id to its plane (and to nothing for `NoShooter` or an unregistered id), that a hostile round
records its shooter, that friendly fire and unowned rounds record nothing, and that a repeat shooter
is not listed twice.

*Pinned in-engine, end to end.* `--fly --chapter=C1 --zeppelins --ai=player_fury --frames=400` prints
`target pool: enemy=1 ally=0 nonAircraft=99 class=Enemy acquired=ai1_player_fury` with no script
errors: the per-pane instance, the per-frame rebuild, the sub-part feed and the auto-acquire all run
in a real session.

*Regression.* 1378/1378 units, 68/68 in-engine suites, engine errors clean, and **14/14 golden shots
hash-identical** — the first item in this plan to touch a live frame path, so the goldens were run
rather than skipped. The full 8-chapter `--freecam` sweep (C1, C1B, C1C, C2, C2B, C3, C4, C5) is clean
at zero errors with unchanged node/mesh counts. `dotnet format --verify-no-changes` clean.

*Owed as live play, and only this.* Pressing the physical buttons: that `D-pad Up` and `T`/`Y`/`U`/
`I`/`O` are actually delivered, that `D-pad Down` stays inert, and how 250 ms feels in the hand. The
logic behind each of those reads is pinned above; what is untested is the read itself.

**⚠ Traps.** (a) Splitscreen: input must read **this pane's own** device list via `Pads.For(PadDevices)`
the way `PadPressed` does (`FlightController.cs:2196`) — never `pads[0]`, and P2–P4 are pad-only
(`KeyDown` is gated on `UseKeyboard`). Four panes each need their own selection. (b) `T` is only free
once **A3** lands. (c) Do not reach for `Shift`/`Ctrl` combos — that is ⚠ table row 2, and it is the
exact mistake this plan already made once.

## B15 ☑ `--target=` scripted twin

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

**Landed 2026-08-16.**

**TODO resolved — the sub-part grammar needs no grammar of its own; the TODO's premise was wrong.**
`TargetPool.NameOf` already names a sub-part by its own world node
(`DestructibleRegistry.Instance.Anchor.Name` → `gasbag1`, `engine2`), which is exactly the shape an
aircraft's name has (`ai1_player_fury`), so `--target=<name>` covers all three cycles with one match.
Confirmed live rather than from the code alone: a C1 session with `--zeppelins` answers a miss with
`selectable now: ai1_player_fury, gasbag1, gasbag2, gasbag3, …`. What the match does need is the
**class write-back** — `Select` sets `ActiveClass` from the entry it found, the same rule
nearest-crosshairs uses, without which the next `Resolve` would immediately drop a pin outside the
active cycle. Duplicate names (two zeppelins carrying identically named zones) resolve to the first in
Enemy → Ally → Non-Aircraft order and then in the pool's own collector order, which is stable per run,
so the flag stays reproducible; a disambiguating suffix is not invented until something needs it.

**Divergence, recorded.** A miss logs `WARN [core] --target=…: no match — selectable now: <names>`
(capped at 24 with a `+N more` count). That listing is why there is no `--target=list` mode.

**Verified (2026-08-16):**

- **Pinned by tests** — the new `target-flag` suite, tree-free, over a pool built from REAL sources
  (bare `FlightController`s and a `DestructibleRegistry.Instance` on its own anchor), because the
  claim is about the names `TargetPool` actually produces. It has an able-to-fail control: the pool's
  auto-acquire takes the *nearer* enemy, so every assertion naming the far one would fail if the flag
  did nothing. Covers all four words (`nearest` returning to the head of the cycle after a step off
  it, `next`, `crosshair` reaching the ally on the nose, `none` clearing and staying cleared through
  the next rebuild), the name form reaching an aircraft, an ally and a zeppelin sub-part,
  case-insensitive matching, an unknown name reporting failure and leaving the selection alone, two
  independently built selectors given one spec landing on the same target, and the trap: a keypress
  after the flag moves off the pinned target and the next rebuild does not snap back.
- **Pinned in-engine**, six runs of `--fly --chapter=C1 --zeppelins --ai=player_fury --frames=400
  --screenshot`: `--target=ai1_player_fury` twice (identical), then `gasbag1`, `none`, `crosshair`,
  `next`, each printing its own `--target=<spec>: <picked> (class=…)` line —
  `gasbag1 (class=NonAircraft)` is the sub-part decision proven end to end, `nothing (class=cleared)`
  is Target Nothing. No script errors in any run.
- **⚠ The screenshot pair does not photograph the pin, and cannot yet.** Both runs are
  `pixmd5=b6a3936f…` — but so is a run with no flag at all, because C22 has not landed and nothing
  draws the selection. The pair proves the run is reproducible; the *printed line* is what proves the
  flag selected what it says. C24's golden is where the pixels start carrying the claim, and it needs
  C22 first. Recorded rather than dressed up as a passing Verify step.
- **Regression:** 1378/1378 units, 69/69 in-engine suites, engine errors clean, 14/14 goldens
  hash-identical, the hitch detector still firing on an injected stall and silent without one, the
  8-chapter `--freecam` sweep (C1, C1B, C1C, C2, C2B, C3, C4, C5) at zero engine errors, and
  `dotnet format --verify-no-changes` clean.

# Wave C — The HUD

## C21 ☑ Split the targeting marker out of `VersusHud` into `TargetHud`

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

**Landed 2026-08-16.**

**TODO resolved — duplicate, following the codebase's own precedent.** `VersusHud` already carries a
private copy of `EdgePoint`/`ClockHour`/`DrawArrow` documented as "MarkerHud's … verbatim" rather
than sharing a base class with `MarkerHud`; that is the established answer to this exact question,
one HUD move earlier. `TargetHud` follows it: its own private copies of `DrawOpponent`/`EdgePoint`/
`ClockHour`/`DrawArrow`/`DrawTag`, documented as copied verbatim from `VersusHud`'s copy (plus the
`stagger` parameter `--debug-markers` needs, which `VersusHud`'s own opponent loop never uses and
so lost from its copy). A shared base class for two `Control`s would be the first of its kind in this
codebase and duplicates a decision already made the other way.

`TargetHud` is built **unconditionally** now, one per human pane in every flight session including
`--vs` — `VersusHud.BuildHostileTracker`'s old "matchless" framing is gone along with the branch that
picked between it and `VersusHud.Build`; both HUDs are simply built when their session calls for them
(`VersusHud` only under `--vs`, `TargetHud` always), which is simpler than the two-build split it
replaces. `Own`/`OwnTeam`/`MarkAll`/`HostilePool` moved to `TargetHud` in full — `VersusHud`'s
remaining `Rigs` opponent loop never read `OwnTeam` (it marks every living rig by `SplitScreen`
identity colour, not by team), so nothing stayed behind needing them.

**Verified (2026-08-16):**

- **Pinned by tests** — `hostile-marker-hud` and `HostileTagTests` moved onto `TargetHud` with no
  behaviour change (the "hud built without a pool never tracks" case now constructs a bare
  `TargetHud` directly rather than through the old VS-constructor side effect, since `TargetHud.Build`
  always takes a pool).
- **Regression:** 1378/1378 units, 69/69 in-engine suites, engine errors clean, 14/14 goldens
  hash-identical (byte-for-byte with the pre-split shots — the plain-`--fly` half of the Verify step),
  the hitch detector still firing on an injected stall and silent without one, and `dotnet format
  --verify-no-changes` clean.
- **Targeted `--vs` capture** (the half no golden covers): `--chapter=C1
  --plane=player_bhawk,player_fury --vs --ai=player_kestrel --hold=0.2,0.1,0,1 --det --mute
  --frames=120 --screenshot=`, 2 human panes + 1 AI hostile. Log shows both HUDs building on both
  panes (`dogfight HUD: …` + `targeting HUD: …`) and `TargetHud` acquiring the hostile on each
  (`targeting hud: P1 tracking ai1_player_kestrel at 8932 m`, same for P2); the screenshot shows both
  panes' status line/dials/reticle drawing normally with no missing chrome or exception. No golden is
  minted for this — no `--vs` shot exists in the manifest yet (C24 is a Wave C item still open).

## C22 ☑ Draw the original's marker

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

**Landed 2026-08-16.**

**Three things the Approach could not have known, all decided by A1's decode:**

- **The range threshold is not a number.** It is the SELECTED GUN's reach through a lead solve
  (`FUN_004574d0`): brackets draw when the round would still be inside the weapon's authored `RANGE`
  at the intercept. `FlightController.GunReachesTarget` owns the weapon half, `TargetHud.GunReaches`
  the arithmetic. So the marker is weapon-dependent, a target outrunning the round is never bracketed
  at any range, and a rocket-only loadout takes the original's own `distance <= 1e6` fallback, which
  never rejects. The reach is measured along the muzzle velocity **with the plane's own velocity
  added** (the aim assist's gate uses the round speed alone) — the original composes that vector,
  which is why flying away from a target extends its bracket range and closing on it shortens it.
  The 50 m hysteresis is ours and TUNE, kept because the original re-answers with no memory.
- **A friendly is GREEN.** This item's own goal line said "blue for a friendly"; that was written
  pre-decode. `Target::GetColor` returns red / green / blue where blue is the *non-destructive
  objective* (protect, escort). Ported verbatim in `TargetHud.MarkerColor`, including the four
  destructive categories overriding the team test.
- **The clock line is the off-screen case only.** `FUN_004579e0` draws three lines unconditionally,
  but the bearing string comes out of `FUN_0049d940`'s off-screen pass and
  `Targeting HUD Kestrel.png` shows an on-screen target with its name alone. Trap (c) resolved
  itself: the stagger step belongs to the one-line `--debug-markers` tags, and the selected target is
  a single marker that never staggers.

**Two decisions the Approach left open.**
`TargetRef` gained a `DisplayName` beside `Name` rather than replacing it: the marker wants `Fury`
and `--target=` wants `ai2_player_fury`, and a golden pinned on "Fury" could not say which of three
Furies it meant (B15's node-name form and its `selectable now:` list both keep working). And the
**blank category line keeps its slot** under a box — the original's three lines sit at fixed 15-pixel
offsets and a blank one just draws nothing, so an aircraft's name is the SECOND line's distance below
the box. Compacting it put the name inside the silhouette, which is what the first by-eye comparison
against the Kestrel shot caught.

The H22 nearest-hostile marker is now a **fallback**: it draws only on a pane with no selection at
all (decision 11, one selected target). `--debug-markers` still replaces that fallback, but no longer
replaces the selection marker, so one frame can carry brackets, label and the debug string — which is
what C24 needs.

**Verified (2026-08-16):**

- **By eye against the originals** (C24 is still open, so this is the Verify step's stated
  substitute). `--fly --chapter=C1 --plane=player_fury --ai=player_kestrel
  --target=ai1_player_kestrel --det --mute --frames=90 --screenshot=`: fixed-size red brackets
  straddling the Kestrel's centre and sitting *inside* the silhouette, with `Kestrel` in red below —
  `Targeting HUD Kestrel.png`'s own composition. A banking run to put the same target off screen
  gives the red edge arrow with `Kestrel` / `1 o'clock` stacked on two lines, which is `HUD.png`.
  With `--debug-markers` and two AI planes, the overlay's `AI1 270 m patrol` and the selection's
  brackets + `Kestrel` draw together on one plane.
- **The range gate, live.** A run that turns away from the target logs
  `targeting hud: P1 brackets on Kestrel at 250 m` and then
  `targeting hud: P1 brackets off Kestrel at 1317 m` — past 1 km, and past it by the margin the
  plane's own velocity adds to the muzzle vector.
- **Pinned by tests.** The `hostile-marker-hud` suite gained C22's three pure rules: the colour table
  (three colours, the Destroy override, the neutral-own-side case), the gun-reach gate (inside
  `RANGE` brackets, past it does not, an outrunning target never does, the hysteresis holds the
  boundary case) and the label lines (the aircraft's kept slot, the compacted edge block, the
  objective's two lines). `target-ref` covers `DisplayName` and its fallback.
- **Regression:** 1378/1378 units, 69/69 in-engine suites, engine errors clean, 14/14 goldens
  hash-identical, the hitch detector firing on an injected stall and silent without one, and
  `dotnet format --verify-no-changes` clean. One flake seen and not reproduced:
  `PerfSampleTests.AScopeAllocatesNothing` failed once in a whole-suite run and passed on a re-run of
  both the file and the full unit stage; it measures allocations and nothing here touches it.
- **Not run, deliberately:** the 8-chapter `--freecam` sweep. This item touches HUD drawing only, no
  world-load path, and the 14 goldens (which cover all eight chapters) are byte-identical.

## C23 ☑ `--debug-markers`: keep full identity, add `H78 A91`

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

**Landed 2026-08-16.** `TargetHud.DebugTag` (`TargetHud.cs`) builds the string; `CollectMarks` now
wraps each scanned aircraft as a `TargetRef` (the same construction `TargetPool`'s Vehicle branch
uses, reading `FlightController.Damage`/`Stats`) instead of handing back a bare `FlightController`,
which is what lets the string read health/armor as `TargetRef`'s optional fields rather than a
plane-specific read of its own — the moment `--debug-markers`' scan widens past aircraft, a turret
or sub-part's `TargetRef.Health` is already the right shape (null, correctly). Plane type
(`DisplayName`, C22's own accessor) was added alongside the figures: the item's Goal string named
it explicitly even though the checklist title did not, and the pre-existing string had never
carried it.

**Two things the Approach could not have known.** The identity + type ordering reads `AI1 Fury`,
not `Fury AI1` — `DebugTag` puts `HostileTag` first because it is the FULL-identity marker (the
trap this item's own text calls out), so the plane-type-alone label the shipped marker centres on
(decision 10) rides second, not first. And the omission is per-figure, not per-plane: a source that
has health but no armor model (none shipped, but `TargetRef`'s contract allows it) would print `H`
alone — the two `is { } x` checks are independent, matching decision 12's "two figures" framing
rather than an all-or-nothing gate.

**Verified (2026-08-16):**

- **Pinned by tests.** The `hostile-marker-hud` suite gained `DebugTag`'s pure format (identity +
  type + range + both figures + mode; the no-health-source omission dropping both) over synthetic
  `TargetRef`s, plus `CollectMarks`' live wrapping: a bare rig's `TargetRef` carries the airframe's
  `DisplayName` with `Health`/`Armor` both null (no `Damage` ledger bound), and — once
  `PlaneDamage` is bound and a hit applied through `Apply` — the ref's fractions match the ledger's
  own `WholeHealth`/`WholeArmor` getters exactly, checked dynamically rather than hand-derived
  (`PlaneDamage`'s armor-first spend arithmetic is its own module's concern, not this item's).
  **Able to fail:** defaulting the no-`Damage` health read to `1f` instead of `null` (the exact
  "print `H100` for a source with no health model" mistake the goal forbids) turns the suite FAIL
  and exit 1 on the omission check; reverted, it passes.
- **Live capture.** `--fly --chapter=C1 --plane=player_fury --ai=player_kestrel --debug-markers
  --det --mute --frames=90 --screenshot=`: the tag reads `AI1 Kestrel 270 m H100 A100  patrol`
  (full health/armor on an undamaged AI, its live AI mode), drawn on the same frame as the shipped
  selection marker (`Kestrel`'s brackets + label) — the C24 golden's target composition. **Not
  captured live:** a plane at reduced health reading a `H` figure below 100 — no CLI flag pre-damages
  a spawned AI for a scripted screenshot (the plan's own open TODO), and combat damage is not
  frame-deterministic enough to land a chosen HP value inside a fixed `--frames` budget. Resolved
  by the pinned suite instead, which drives `PlaneDamage.Apply` directly and reads the resulting
  fraction back off `TargetRef` and `DebugTag` together — the same plumbing a live hit would
  exercise, without depending on combat RNG to produce a specific number on screen.
- **Regression:** 1378/1378 units, 69/69 in-engine suites, engine errors clean, `dotnet format
  --verify-no-changes` clean. No 8-chapter `--freecam` sweep is owed — this item touches HUD string
  formatting and one collection helper only, no world-load path.

## C24 ☑ One golden screenshot for the geometry

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

**Landed 2026-08-16.** `analysis/goldens/manifest.json`'s `c1-targeting-hud`: `--chapter=C1
--plane=player_fury --ai=player_kestrel --target=ai1_player_kestrel --debug-markers
--hold=0,0,0,0.5 --det --mute`, frame 90. `--ai=` places the Kestrel 250 m ahead of P1 on the same
course, so a level hold (no turn, half throttle) keeps it on screen and inside the gun's authored
`RANGE` without hunting for a pose — the TODO's "pick the world, chapter and pose" resolved to
reusing `c1-flight`'s own chapter and airframe rather than inventing a new one. `--target=` (B15)
pins the subject explicitly rather than relying on the auto-acquire picking the sole enemy, so the
shot stays correct if a second candidate is ever added to this scene.

**The TODO's determinism half resolved clean.** An AI spawn's only randomness is jitter (fd/thrust)
and livery paint, both pinned by `--det`'s master seed; the target's own position is `--ai=`'s fixed
250 m-ahead placement, not a random one. Two independent `RunTests.ps1` passes (a `-RegenGoldens`
render and a plain verify run afterward) hashed identically with no retry, so the AI's small
in-frame jitter never moved a pixel by frame 90.

**Verified (2026-08-16):**

- The shot visibly shows all three pieces the Goal asked for: the fixed-size brackets around the
  Kestrel (inside the fury's gun range at 270 m), `Kestrel` in the label block below it, and the
  `--debug-markers` string `AI1 Kestrel 270 m H100 A100  patrol` drawn above — the same frame
  composition C22 and C23 built toward.
- **Two runs, identical.** `RunTests.ps1 -RegenGoldens` (the render that minted the hash) and a
  plain `RunTests.ps1` verify pass immediately after both hashed `23f8af3edd99e9afedd69352fac3aa8f`
  — the Verify step's own bar.
- **The other 14 shots are untouched**: the regen run reported `1 hash(es) changed` — only the new
  entry — so this item moved no existing pixel.
- Measured frame-sensitivity (frame 90 vs 91, raw pixel diff): **48.18 %**, the most sensitive shot
  in the set (surpassing `c1-flight`'s 34.52 %) — a flying plane's chase camera plus a moving AI
  target compounds `c1-flight`'s own motion. Recorded in the manifest's `exercises` field and in
  `analysis/goldens/README.md`'s frame-sensitivity roster (now seven shots, not six).
- **Regression:** 1378/1378 units, 69/69 in-engine suites, engine errors clean, 15/15 goldens
  hash-identical (the pre-existing 14 plus the new one), `dotnet format --verify-no-changes` clean.
  No code changed in this item — only the golden manifest and its README — so no build/test
  regression was possible in the first place; the runs above are the golden stage's own proof.

# Wave D — Close out

## D31 ☑ Docs, backlog spin-offs, and the reserved `L` binding

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

**Landed 2026-08-16.** Most of the Approach's doc updates turned out already done: `docs/controls.md`
already carried the curated `T`/`Y`/`U`/`I`/`O` binds, the `D-pad Up` tap/hold row, the `T` → `F16`
move and `A` dropped from respawn (`R`/pad `Y` only) — all landed in their own items (A2, A3, B14)
per the plan's own "docs update in the same turn" ground rule, so there was nothing stale left to
sweep up. `docs/cli.md`'s `--target=` and `--debug-markers` entries were current for the same reason
(B15, C23). `docs/architecture.md`'s new-module entries (`TargetRef.cs`, `TargetPool.cs`,
`TargetSelection.cs`, `TapHoldButton.cs`, `TargetHud.cs`) and the `VersusHud` split — including the
"no reference to copy" correction (⚠ table row 1) — were likewise already in from C21/C22. What
D31 actually added:

- **`L` reserved** in `docs/controls.md`'s Flight table: bound to nothing, documented as reserved
  for Track Target, pointing at the new `BL-395` for the camera work itself (decision 15).
- **`BL-357` gained its corroboration**: the original's Weapons keybind page names `Cycle guns
  clockwise`/`counterclockwise` (`F3`/`F4`) and the rocket pair (`F5`/`F6`) as distinct actions —
  direct evidence the original has two selectors per weapon class where CSVM has one, cited from the
  keybind page itself rather than from watching a play session.
- **`BL-357`/`BL-363` re-verified still-open** (the plan's own owed TODO): `git log --grep` for each
  returns only the commit that *wrote* the entry, no closing commit for either, and both are still
  present in `backlog.md`.
- **Four backlog items minted** for the plan's four deliberate deferrals: `BL-394` (rebindable
  keymap — the real fix for targeting's key placement), `BL-395` (Track Target's camera, naming the
  open questions snap/smooth, override/blend, no-target/behind-target, and E42 interaction),
  `BL-396` (curated mission-target list to unblock `Structures` as Non-Aircraft), `BL-397` (the
  modernized brackets-past-range marker, recorded as the deliberate INVERSE of the original's own
  rule so nobody "fixes" C22's gate back toward it by mistake).
- **`AimCandidateSet.Turrets`' doc comment checked and confirmed already fixed** by B12 — no second
  pass needed. `AimTargetKind.Turret`'s own separate, similarly-stale "Empty until M4 builds them"
  comment was noticed in passing but is **not** touched here: it is a different declaration than the
  one this item names, and is M4/M3-era drift outside this plan's scope, not a targeting leftover.

**Model recommendation.** medium — documentation and backlog authoring, judgement in the framing.

**Verify.** `docs/controls.md` matches the shipped binds exactly (checked directly against
`FlightController.cs`'s `DispatchTargetKey`/`_targetHold` reads — `T`/`Y`/`U`/`I`/`O` and
`D-pad Up` tap/hold match verbatim, and `RespawnPressed()` reads only `Key.R`/pad `Y`). Each new
`BL-` id (`394`–`397`) is unique — confirmed by grep before writing, and the commit hook would have
caught a collision regardless. `PROJECT_CONTEXT.md`'s "Current status" points at this plan through
the landing commit, then is swapped to name whatever plan (or none) follows once this one moves to
`docs/plans/`.

This closes the plan. All 11 checklist items are ☑ across Waves A–D; moved to `docs/plans/` with a
`COMPLETE` banner and indexed in `plans.md` in the same commit.
