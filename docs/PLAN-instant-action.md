# Instant Action

**ACTIVE PLAN** (written 2026-08-14). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

This plan delivers the original's **Instant Action** as a configurable mission: pick an environment,
pick one of four mission types (dogfighting an ace, dogfighting a squadron, stunt flying, attacking a
zeppelin), set your aircraft, your wingmen and up to four enemy waves, and fly it to a win or a loss
with a scored wrap-up. It is built to the decoded original: the option sets, their ordering, the
militia-limited aircraft lists, the authored per-environment ace and the wave sequencer's own spawn
law all come out of `ia.zrd.json`, `crimson.rof` and `crimson.exe`, and anything the data does not
carry is marked INVENTED where it is written.

Three adjacent features the original's Instant Action screen also offers are **out of scope** and
filed instead: the Table of Contents of 19 preset scenarios with its *View Story* page (`BL-352`),
the *Weapon Loadout* editor (`BL-353`), and *Build Custom Plane*, the whole hangar flow (`BL-354`).
Each needs a decode of its own and none of them blocks a flyable mission. Multiplayer's own lobby
(`MULTIPLAYERLOBBY_*.SCRIPT`) is a separate mode and is not touched.

No item here is drawn from `backlog.md`, so the re-verification requirement does not apply. Two
standing notes elsewhere in the record are closed by this plan as a side effect and are named in the
items that close them: `Session/AiGeneratorRuntime`'s note that C1/IA1's mission setup deactivates
its own zeppelin and that the Instant Action wave logic is what wakes it (F12; A4 has since
disproved the second half, so what F12 closes is the corrected note), and M4 E16's note that voice
trigger id 20 is unreachable until a team model exists (B7).

## Milestone goal

- A single typed `InstantActionDef` describes a whole mission, produced three ways that converge on
  one build path: read from a chapter's shipped `ia.zrd.json`, read from a hand-authored JSON file
  via `--ia=<path>`, or built by the in-game wizard.
- All four mission types are flyable, completable and loseable, solo or in splitscreen, with a
  scored wrap-up carrying the four rows the original actually wires (A1 found a fifth title
  string, "Total Kills", that no script or layout row uses).
- CSVM gains a real team model, replacing `AimAssist.TeamOfPilot`'s documented stand-in.
- The launch menu's top level becomes Free Flight / Instant Action / Dogfight, with Stunt Flying
  moving inside Instant Action as a mission type.

**Nothing in this plan invents a value that the original's data or binary can be made to answer.**
Four decode items run first for exactly this reason, and each one names the fallback it would use
plus the words "marked INVENTED" if the decode comes back empty.

## Decisions (2026-08-14)

| # | Question | Decision |
|---|---|---|
| 1 | Fidelity target | **Match the decoded original.** Option sets, defaults, the ace, the wave law and the militia plane lists all come from data; divergences are named individually. |
| 2 | What an Instant Action setup is expressed in | **One typed `InstantActionDef`.** A `src/Mech3` reader parses the shipped `ia.zrd.json` into it; `--ia=<path>` reads a plain JSON object with the same field names into the same record; the wizard builds the same record. |
| 3 | Friendly fire | **Decoded (A2, 2026-08-14): the original applies it.** The team gate is a targeting gate and nothing more; the damage path carries no team test. CSVM matches, which means B7 adds no damage gate at all. The fallback ("block it, marked INVENTED") is not taken. |
| 4 | Team ids | **The decoded turret convention:** 0 neutral, 1 the player's side, 2 and up enemy. Humans and wingmen are team 1, every Instant Action enemy is team 2. Waves are cohorts inside one enemy team, not teams of their own. |
| 5 | Wingman behaviour | **Decoded (A3, 2026-08-14): the mode machine on team 1, with a `primary_target` chain.** Wingmen 0, 1 and 3 take `primary_target player`; 2 and 4 take wingmen 1 and 3. No net, no formation, activation volumes opened to ±10000 m, skill vector unset. The INVENTED fallback is not taken. |
| 6 | When wave aircraft are built | **All at session build, held inert**, then teleported and activated on wave change. Faithful to the sequencer, and it keeps a six-plane build out of a live combat frame while `PLAN-perf-hitches` is open. |
| 7 | Militia to aircraft list | **The `.BM` pattern coverage** (`PatternLibrary.PatternsFor` inverted), not `vehicle.json` defs. Settled by `LAYOUT.CSV`: `IA_D_PLANEE0` is an eleven-row dropdown and only pattern coverage reaches eleven. |
| 8 | Splitscreen | **Humans join team 1 in addition to the wingmen**, so `num_wingmen` keeps meaning exactly what the data says. The sequencer's "500 m from the player" becomes "500 m from the nearest human", named as the extension it is. |
| 8a | Splitscreen and the flight size cap | **The friendly flight is capped at 6 aircraft** (the data's own maximum: 1 pilot plus 5 wingmen), and wingmen are the ones that give. Flown wingmen = `min(num_wingmen, 6 - humans)`, so 2 humans with 5 configured wingmen fly 4 of them and 4 humans fly 2. Below the cap the configured count is honoured untouched. The cap is derived from the shipped 0-to-5 range; the clamp rule itself is INVENTED. |
| 9 | Mission end and the wrap-up | **Both in scope.** Per-mode end conditions plus the wrap-up board with the four decoded rows (A1 found the shipped screen wires four of the six `langui` wrap-up strings, not five). |
| 10 | Stunt flying with several pilots | **Every player flies their own zone set and the mission ends when all of them have finished**, which is what `StuntRace` already does. Not first past the post. |
| 11 | Zeppelin mode's wave source | **Decoded (A4, 2026-08-14): the generator, and only the generator.** The `mission-type-2` branch **replaces** the teleport, so on `zeppelin_run` every enemy launches out of the selected zeppelin's bay and none is ever moved by the sequencer. CSVM matches: zeppelin mode feeds `AiGeneratorRuntime` a wave's worth of capacity per wave change and runs no teleport at all. |
| 12 | Adjacent original features | **Out of scope, filed as `BL-352` / `BL-353` / `BL-354`.** |
| 13 | First playable slice | **Dogfighting an ace**, before any wave machinery exists. It is the smallest complete mission and it gives the later waves something working to land against. |
| 14 | Player death | **One life by default; a downed pilot spectates.** The mission runs while any human is alive and the wrap-up shows the loss when the last one goes down. |
| 15 | Lives | **A `lives` field on the def, INVENTED** (nothing in `ia.json` carries one). Default 1, which is the faithful one-life run. `N` gives N-1 respawns on the existing 3 s `VersusRespawnDelay` path. `0` is unlimited. Per pilot, not shared. |
| 16 | Skill names to AI rating | **Decoded (A3, 2026-08-14): there is no such mapping.** `novice`/`veteran`/`ace` parse to 0/1/2 and act only as the difficulty tier for that one spawn, which scales armour and health by 0.875/1.0/1.25. A wave pilot's nine stats are rolled from a five-row table; the ace's come from `ace_stats`. The 3/6/9 fallback is not taken and must not be reintroduced. |
| 17 | Top-level menu | **Free Flight / Instant Action / Dogfight.** Stunt Flying stops being top-level and becomes a mission type inside Instant Action, offered only where the environment has `dzones`. `--stunt` survives as a CLI flag. |
| 18 | Where `lives` appears in the wizard | **Step 2, beside the mission choice**, so the step count stays at five and step 4 remains the wingmen step. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "The Instant Action ace is picked at random." | Every chapter's `ia.zrd.json` names one ace in full: `ace_name`, `ace_plane`, `ace_skill`, `ace_stats`, `ace_accentID` and a complete `ace_pattern` / `ace_colorN` / `ace_decalN` livery. Authored in 8 of 8, censused 2026-08-14. |
| 2 | "A militia's aircraft list comes from the `vehicle.json` defs carrying its `paint_pattern`." | `LAYOUT.CSV`'s `IA_D_PLANEE0` is an eleven-row dropdown. Under the def reading the largest militia has three aircraft; under `.BM` pattern coverage Fortune Hunter covers exactly eleven. The two readings also disagree on Sacred Trust (defs give Hellhound alone, coverage gives Warhawk and Hellhound). |
| 3 | "The original has formation or wingman AI to copy." | M4 B7, closed disproven 2026-08-13 (`analysis/m4-b7-group-slot/FINDINGS.md`): `group` is a mission-logic cohort id, nothing in steering, targeting or the mode machine reads it, and the nine-mode dispatch has no formation or wingman mode. Escort-looking behaviour rides `player`-trailer nets, which are authored per story mission. |
| 4 | "Instant Action offers one environment per chapter, so eight." | The `langui` string block at 3650 holds seven, so one chapter is omitted, and it is **C1C**. The launcher `FUN_004174d0` maps the seven dropdown rows to chapter ids 1, 5, 6, 8, 2, 7, 4 and never 3 (A5). |
| 4a | "C2B is the chapter Instant Action omits, and 'the clouds' is C1C." | ⚠ **This plan's own A1 got this backwards and it stood for part of a day.** The correct pairing is **the clouds = C2B**, with **C1C** not offered, which `CSVM/src/UI/LaunchMenu.cs` had already carried before this plan started. A1 reached the wrong answer by elimination: the world content never discriminated between C1C and C2B (both bar stunt flying, both ship no `dzones`, both run `zeppelin_run`), and the tiebreak used, C2B's missing `player_plane`/`num_wingmen`, means only that the setup screen supplies them, which A3 later showed it does for every chapter. A5 decoded the launcher and it agrees with `LaunchMenu.cs`. |
| 5 | "CSVM already has a team model to hang this on." | `AimAssist.TeamOfPilot` is a documented stand-in: pilot index plus one, which makes every pane hostile to every other and every AI hostile to every other AI. `AimAssist.cs:310-317` says so in its own comment. |
| 6 | "`enemy_skill` sets a wave's AI skill." | A3, 2026-08-14. The string `enemy_skill` does not exist in `crimson.exe` and the wave parser `FUN_00458e00` reads four keys, none of them that one. It is authored in all 8 chapters and read by nothing; a wave's nine pilot stats are rolled from a five-row table instead. |
| 7 | "The mission-type ids follow the UI dropdown order." | A3, 2026-08-14. `FUN_00458c20` gives 0 ace, 1 squadron, **2 zeppelin run**, 3 ground target, **4 stunt flying**; the dropdown shows ace, squadron, stunt, zeppelin. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A2, A3, A4, A5, B6, D9, E11, G14 | The option sets, the `ia.json` key census, the damage path, the whole setup path, both of the wave sequencer's arms and all four wrap-up counters are read out of the shipped data and out of `crimson.exe`. Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | E10, G13, G14, H15, H16 | The behaviour is settled; the numbers and the presentation are not. Anything numeric here is TUNE, not fact. |
| **Leads only, no mechanism yet** | B7, C8, F12 | All four Wave A decodes have landed, so what is left here rests on implementation choices rather than on unread code. A4 settled F12's wave source (the generator, exclusively) but not how Instant Action's own zeppelin deactivation composes with the mission script's, so F12 stays here. Budget for a decode ending in a disproof: A2, A3 and A4 each did, against the "block it" and "3 / 6 / 9" fallbacks and against the standing "the wave logic wakes the zeppelin" note. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees, and other sessions may
push or pop it concurrently. Never use a bare `git stash` in a worktree session here; use a local WIP
commit, or `git stash push -u -m "<unique-tag>"` and restore by SHA with `git stash apply`.

## What the data actually ships

Three sources combine, all read on 2026-08-14. The UI half is `ASSETS/SCRIPTS/INSTANTACTION.SCRIPT`
plus `ASSETS/LAYOUT.CSV` inside `crimson.rof` (see [`formats/rof.md`](formats/rof.md)), with the
labels in the `langui` string table (see [`formats/strings.md`](formats/strings.md)). The per-chapter
configuration is `<chapter>/IA1/zrdr/ia.zrd.json` (see [`formats/spawns.md`](formats/spawns.md)). The
runtime half is `crimson.exe`, already partly traced by M4 B7
(`analysis/m4-b7-group-slot/FINDINGS.md`).

### The screen's controls

`INSTANTACTION.SCRIPT` declares every widget and the engine callback that fills it. `LAYOUT.CSV`'s
last column on a `D` row is the dropdown's visible-row count, which for these small lists equals the
item count.

| Widget | Fill / select callback | Rows | Contents |
|---|---|---|---|
| `ia_d_planep` | 2312 / 2313 | 20 | the 11 airframes, plus the player's saved custom planes (the script sizes the list from `callback(1024)` + 11) |
| `ia_d_nwing` | 2314 / 2315 | 6 | 0 to 5 wingmen |
| `ia_d_planew` | 2316 / 2317 | 11 | the wingman aircraft, hidden entirely when the wingman count is 0 |
| `ia_d_misstype` | 2318 / 2319 | 4 | the four mission types |
| `ia_d_environment` | 2320 / 2321 | 7 | the seven environments |
| `ia_d_nenemyN` | 2322 | 7 | 0 to 6 enemies, one per wave, N in 0..3 |
| `ia_d_egroupN` | 2324 | 13 | the militia flying that wave |
| `ia_d_planeeN` | 2330 / 2331 / 2332 / 2333 | 11 | that militia's aircraft. Each wave has its **own** list id, and selecting a militia resets the wave's plane index to 0 (`AV[BA].QG = 0`), which is the script's own statement that the list depends on the militia |
| `ia_d_difficultyN` | 2326 | 4 | three skills in a four-row box |
| `ia_tl_contents` | 2300 / 2302 | 14 visible | the Table of Contents, 19 preset scenarios (`BL-352`) |

Two behaviours of the screen matter beyond the option sets. Selecting mission type 0 hides every
enemy control (`if (0 == WT)` in `gui_init` and in mailbox 20001), which is the script's own
statement that **dogfighting an ace takes no wave configuration**. And the enemy rows are paged: page
one shows the four top dropdowns plus wave 1, page two shows waves 2 to 4 (mailbox 20002).

### The option strings

From the `langui` table, read out of `extracted/rof/ui_strings.json`.

| Block | Base id | Values |
|---|---|---|
| Environments | 3650 | an airfield, the clouds, Hawaii, Manhattan, the ocean, Sky Haven, a movie studio |
| Mission types | 3660 | dogfighting an ace, dogfighting a squadron, stunt flying, attacking a zeppelin |
| Militias | 3670 | Black Hat, Black Swan, Blake Aviation, British, Fortune Hunter, Hollywood Knight, Hughes Aviation, Medusa, Russian, Sacred Trust, German, Studio Security, Broadway Bomber |
| Skills | 3695 | novice, veteran, ace |
| Aircraft (plural forms) | 3700 | Hoplites, Hellhounds, Balmorals, Bloodhawks, Brigands, Devastators, Firebrands, Furys, Kestrels, Peacemakers, Warhawks |

The wrap-up screen's own strings are at 1133 to 1138: *Instant Action* (the screen title), *Time to
Complete Mission*, *Enemies Shot Down*, *Danger Zones Completed*, *Shot %*, *Total Kills*. Of the
five row-title strings, only the first four are wired to a row (A1 traced this in
`docs/formats/instant-action.md`) — *Total Kills* has no line in `LAYOUT.CSV`, no text object in
`IA_WRAPUP.SCRIPT`, and no format id in `RESOURCE.H`.

Note that the aircraft plural list calls the autogyro a **Hoplite**, while `ia.json`'s
`enemy_plane` / `player_plane` values use the singular vehicle display names and call it
**Autogyro**. Two vocabularies for one aircraft; the def carries the `ia.json` spelling and the UI
renders the plural.

### Environment to chapter

Seven strings, eight chapters, and both the pairing and the row order are **decoded**: the launcher
`FUN_004174d0` switches on the dropdown index and writes a chapter id (A5). **C1C is the omitted
chapter.**

| Row | Environment | Chapter |
|---|---|---|
| 0 | an airfield | C1 |
| 1 | the clouds | C2B |
| 2 | Hawaii | C3 |
| 3 | Manhattan | C5 |
| 4 | the ocean | C1B |
| 5 | Sky Haven | C4 |
| 6 | a movie studio | C2 |

This matches `CSVM/src/UI/LaunchMenu.cs`, which carried the same mapping before this plan started.
⚠ A1 originally wrote this table with "the clouds" as C1C and C2B excluded; see wrong-claim 4a
above for why that reasoning failed, and do not reintroduce it.

### The militia aircraft lists

Inverted from [`formats/paint.md`](formats/paint.md)'s per-aircraft pattern table, which is measured
over the `.BM` skins each pattern ships in `crimson.rof`.

| Militia | Pattern | Aircraft |
|---|---|---|
| Black Hat | `blackhat` | Warhawk, Brigand, Hoplite |
| Black Swan | `blckswan` | Fury |
| Blake Aviation | `blake` | Bloodhawk, Peacemaker |
| British | `british` | Peacemaker, Balmoral |
| Fortune Hunter | `player_fortune` | all eleven |
| Hollywood Knight | `hollywd` | Firebrand |
| Hughes Aviation | `hughes` | Bloodhawk, Kestrel, Fury |
| Medusa | `medusas` | Kestrel, Brigand |
| Russian | `cccp` | Devastator |
| Sacred Trust | `sactrust` | Warhawk, Hellhound |
| German | `german` | Hellhound |
| Studio Security | `studio` | Fury, Hoplite |
| Broadway Bomber | `BROADWAY` | Peacemaker |

`ITSTAXI` is the fourteenth pattern folder and covers the Hoplite, but no militia string names it, so
it is not an Instant Action militia. `BROADWAY` and `ITSTAXI` ship no `paint_pattern` in
`vehicle.json` and therefore have no canonical colours; `PatternLibrary` already offers them with
whatever colours are current, which is the behaviour a Broadway Bomber wave inherits.

### The per-chapter configuration

Key census over all 8 chapters, from [`formats/spawns.md`](formats/spawns.md) and re-read this
session. `mission_type` and `disallow_missions`, the two keys that decide what a map can host:

| Chapter | `mission_type` | `disallow_missions` | `player_plane` | `num_wingmen` |
|---|---|---|---|---|
| C1 | dogfight_squadron | ground_target | Bloodhawk | 3 |
| C1B | stunt_flying | ground_target | Firebrand | 3 |
| C1C | zeppelin_run | ground_target, stunt_flying | Fury | 3 |
| C2 | stunt_flying | ground_target | Brigand | 3 |
| C2B | zeppelin_run | ground_target, stunt_flying | (absent) | (absent) |
| C3 | dogfight_squadron | ground_target | Hellhound | 3 |
| C4 | stunt_flying | ground_target | Peacemaker | 3 |
| C5 | stunt_flying | ground_target | Kestrel | 3 |

`ground_target` is a fifth mission type that every map disallows, so the UI offers four. Each
`groupN` carries `num_enemies`, `enemy_name` (an `MSG_*` key naming the militia and aircraft
together, for example `MSG_VEH_BHAT_WARHAWK`), `enemy_plane` and `enemy_skill`. `spawn_points` is a
dict of scenario name to `[x, y, z, heading]` entries, eight per scenario except `zeppelin_run`'s
four.

### The wave sequencer

Traced by M4 B7 (`analysis/m4-b7-group-slot/FINDINGS.md`), function `FUN_0045b9d0`, with the
`ia.zrd` parser at `FUN_00459390` (the `group%d` string sits at `0x00625640`):

> A global current-group counter (`DAT_00718cd0`): when no living enemy of the current group remains,
> it increments the counter, picks a stored spawn point at least 500 m from the player (`250000` =
> 500 squared), teleports every member of the new group there fanned 100 m apart at 45-degree
> offsets, and reactivates them (`FUN_004b0f40(0)`, the inverse of the `deactivated` flag the spawn
> applied).

So wave members exist from mission start, deactivated, and are teleported in rather than spawned.
The same function's `mission-type-2` branch adds the new group's member count to a generator's
remaining capacity (`+0x80`) and stamps the generator's group (`+0x64`), which is how zeppelins launch
fighters in Instant Action with `capacity 0`.

A4 read the function whole on 2026-08-14 and the branch turns out to be **the `else` of the
teleport, not an addition to it** (`CMP EBX,0x2` / `JNZ` at `0x0045ba9b`, the type-2 arm ending
`JMP 0x0045bd83` past the whole teleport block). Mission type 2 is the zeppelin run, as A3 had
already established from the parser. The spawn list the teleport draws from is `ia.json`'s own
`spawn_points`, keyed by mission-type id, and the full reading is in
[`formats/instant-action.md`](formats/instant-action.md).

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

A bare id (`B7`, `E11`) is always an item of **this** plan. `PLAN-M4-ai` used the same letter-number
scheme and this plan cites several of its items, so those are always written with the prefix: `M4 B7`
is M4's formation disproof, `B7` is the team model below.

### Wave A — Decode

1. ☑ A1 The Instant Action format page: the whole configurable surface, written down
2. ☑ A2 Does the original refuse friendly damage, or only friendly targeting?
3. ☑ A3 The Instant Action setup path: what a wingman is given, and what `enemy_skill` becomes
4. ☑ A4 Re-read `FUN_0045b9d0`: the zeppelin branch, the mission-type ids, the spawn-point source
5. ☑ A5 The wrap-up screen's four counters: what each one actually counts

### Wave B — The primitives

6. ☑ B6 `InstantActionDef`, its `ia.zrd.json` reader, and `--ia=<path>`
7. ☑ B7 The team model, replacing `AimAssist.TeamOfPilot`'s stand-in

### Wave C — Dogfighting an ace

8. ☑ C8 The Instant Action runtime and the authored ace

### Wave D — Wingmen

9. ☑ D9 Wingman spawn

### Wave E — Waves

10. ☑ E10 The inert aircraft state
11. ☐ E11 The wave sequencer

### Wave F — Attacking a zeppelin

12. ☐ F12 Zeppelin mode, and waking C1/IA1's own zeppelin

### Wave G — Ends

13. ☐ G13 End conditions, lives and spectating
14. ☐ G14 The wrap-up board

### Wave H — The menu

15. ☐ H15 Top-level restructure and wizard steps 1 to 2
16. ☐ H16 Wizard steps 3 to 5, and one build path from wizard and CLI

## Dependency and parallelism notes

A1 is documentation and can run beside everything. A2, A3, A4 and A5 are four independent Ghidra
reads and can run in parallel with each other; each one gates exactly one later item (A2 gated B7's
damage gate, A3 gated D9 and E11's skill mapping, both answered; A4 gates F12, A5 gates G14), so a
later item may start on its fallback and flip a single value when its decode lands. A5 is the
loosest of the four:
it gates only the wrap-up board's arithmetic, so it may land last without holding anything up.

B6 blocks every item after it, since the def is the currency. B7 blocks C8 (the ace needs a hostile
team) and G13. C8 blocks D9, E11, F12 and G13, because it owns the runtime those hang off. E10 blocks
E11. G13 blocks G14. H15 blocks H16.

File contention, which is what decides what may not run in parallel worktrees: **B7, C8 and G13 all
edit `Flight/FlightController.cs`** (team field, mission binding, lives and spectate), and **C8, D9,
E11 and F12 all edit `Session/InstantActionRuntime.cs`**. Run each of those groups in sequence.
`Session/AiAircraftSpawner.cs` is touched by C8, D9 and E10. `SessionSpec.cs` is touched by B6 and
H16 only. The menu items H15 and H16 own `UI/LaunchMenu.cs` alone.

---

# Wave A — Decode

## A1 ☑ The Instant Action format page: the whole configurable surface, written down

**Goal.** `docs/formats/instant-action.md` exists and is the description of record for what an
Instant Action mission can be configured to: the seven environments and the chapter each names, the
five mission types and the one every map disallows, the thirteen militias with their aircraft lists,
the wingman and wave ranges, the three skills, and the ace record. Anyone implementing waves B to H
reads that page instead of re-deriving it.

**Evidence (confidence: traced).** Every table in this plan's "What the data actually ships" section
was read this session out of `extracted/rof/ASSETS/SCRIPTS/INSTANTACTION.SCRIPT`,
`extracted/rof/ASSETS/LAYOUT.CSV`, `extracted/rof/ui_strings.json` and the eight
`extracted/<chapter>/IA1/zrdr/ia.zrd.json` files. Sky Haven was the one environment elimination alone
reached, and the user confirmed it as C4 on 2026-08-14, so the map is settled. One claim on the page
is still an inference and must be labelled as such: **the dropdown ordering is assumed to follow the
string-id order**. The instrument that settles it is flying each chapter and reading the label
against what is out of the window, which is cheap and needs no decode.

**Approach.** New page in `docs/formats/`, indexed in that directory's `README.md`, following the
house shape of [`spawns.md`](formats/spawns.md) (which already documents `ia.json`'s keys and should
gain a pointer here rather than being duplicated). Cross-link [`paint.md`](formats/paint.md) for the
militia-to-aircraft derivation and [`rof.md`](formats/rof.md) for the UI archive. Do not restate the
`ia.json` key census that `spawns.md` already carries; point at it.

**Model recommendation.** Medium. The facts are already gathered and the work is careful writing plus
two labelled inferences, not judgement about mechanism.

**Verify.** The page's tables reproduce from the files it cites: re-run the string-block dump and the
`LAYOUT.CSV` row extract and diff against the page. No engine change, so no regression surface.

**⚠ Traps.** (a) The autogyro has two names in the shipped data, **Hoplite** in the UI plural list
and **Autogyro** in `ia.json`; record both and say which layer uses which, or the plane list will
look like it has twelve entries. (b) `LAYOUT.CSV`'s last column on a `D` row is the visible-row count,
not the item count, and they coincide only because these lists are short; `ia_d_planep`'s 20 is a cap
for the variable custom-plane list, not twenty aircraft. (c) Do not write the militia lists from
`vehicle.json` defs; that reading is disproven above.

## A2 ☑ Does the original refuse friendly damage, or only friendly targeting?

**Answered 2026-08-14: only friendly targeting.** The damage path from impact to drained pool
carries no team test, so a round from a team-1 aircraft hurts and can kill another team-1 aircraft.
The one place that path asks about teams computes the answer into a stack byte and passes it to a
parameter nothing reads. The decode is [`org/vehicleDamage.md`](org/vehicleDamage.md)'s "Teams and
friendly fire"; the consequence for this plan is in decision 3, B7 and D9.

**Goal.** A settled, sourced answer to whether a round from a team-1 aircraft can hurt another team-1
aircraft in the original, so that the rule CSVM implements is matched rather than invented.

**Evidence (confidence: lead-only).** What is already traced is a **targeting** gate:
`AimAssist`'s candidate filter rejects a pair on matching team or on either side being team 0
(`AimAssist.cs:409-410`), and that is the engine's own rule in the engine's own order. The team id
sits at record `+0x34` and the cohort at instance `+0x388` (`analysis/m4-b7-group-slot/FINDINGS.md`),
so the offsets to follow are known. Nothing has been read on the damage-apply side.

**Approach.** From the projectile impact path, follow to whatever applies damage to a vehicle
instance and check whether it reads the shooter's and victim's team ids before applying. The M4
weapons work already located the impact-outcome machinery, so start from the surface-id dispatch. The
answer, either way, is one or two sentences in `docs/formats/instant-action.md` (A1) plus the gate in
B7.

**Model recommendation.** High. Binary reading with an ambiguous target and a real chance of a
disproof.

**Verify.** The decode itself is the deliverable. If the answer is "damage is refused", B7's gate is
exercised by an in-engine assertion: a team-1 AI firing at a team-1 human registers an impact and no
HP loss. If the answer is "damage applies", the same assertion is inverted. Either way the assertion
must be able to fail, so write it against the opposite behaviour first and watch it go red.

**⚠ Traps.** (a) A team-0 (neutral) reading is not the same as a same-team reading; the engine's rule
rejects a pair when **either** side is 0, so a neutral aircraft is not a wildcard and must not be
tested as one. (b) If the decode comes back empty, the fallback is "block it", and the code comment
and the plan item must both say INVENTED. Do not let a fallback quietly become a finding.

## A3 ☑ The Instant Action setup path: what a wingman is given, and what `enemy_skill` becomes

**Answered 2026-08-14, both halves, with no fallback taken.** A wingman is a synthetic `aiv` roster
block handed to the ordinary roster spawn `FUN_0047c210`: team 1, group 0, no net, activation
volumes opened to ±10000 m, the `fortune` pattern in the player's own colours, an unset skill
vector, and a `primary_target` chain in which wingmen 0, 1 and 3 escort the player while 2 and 4
escort 1 and 3. `enemy_skill` becomes **nothing**: no such string exists in the executable and the
wave parser never reads it. The skill *names* parse to 0/1/2 elsewhere and act only as the global
difficulty for the duration of one spawn, which is a **hit-point** multiplier (0.875/1.0/1.25), not
a pilot rating; a wave pilot's nine stats are rolled from a five-row table instead. The decode is
[`formats/instant-action.md`](formats/instant-action.md)'s "setup path" section.

**Also settled, for other items.** Mission-type internal ids are `dogfight_ace` 0,
`dogfight_squadron` 1, `zeppelin_run` 2, `ground_target` 3, `stunt_flying` 4, which answers one of
A4's three questions outright: "mission type 2" is the zeppelin run. `num_wingmen` and all four wave
counts are **forced to 0 when `mission_type` is 0**, so the ace duel is solo in the data. Waves 2 to
4 are built deactivated at the origin, which is E10's inert state arriving from the original.

**Goal.** Two answers. First, what a wingman aircraft is created with in Instant Action: its team,
its net if any, its `primary_target`, and which mode the machine starts in. Second, what the
`novice` / `veteran` / `ace` string is converted to on the vehicle record, given that `AiSkills`
interpolates every stat off a 1 to 9 rating.

**Evidence (confidence: lead-only).** The `ia.zrd` parser is located at `FUN_00459390` and reads the
`group%d` keys from `0x00625640`; the function that creates the player's flight sits in the same
module. M4 B7 established that no formation mode exists and that escort-looking behaviour rides
`player`-trailer nets plus `primary_target player`, with 27 roster blocks authoring the latter
(`analysis/m4-b7-group-slot/FINDINGS.md`, "Where formation behaviour actually lives"). `ace_stats`
is `[9,9,9,9,9,9,9,9,9]` in all eight chapters, which pins the top of the skill scale and nothing
else.

**Approach.** Read outward from `FUN_00459390` to its callers and to whatever consumes `player_plane`
and `num_wingmen`. The wingman creation is where team, net and target are stamped. For the skill
half, find where the `enemy_skill` string is compared and what integer it writes; the nine-stat block
layout is already documented in [`formats/vehicle.md`](formats/vehicle.md) and
[`formats/ai-rosters.md`](formats/ai-rosters.md).

**Model recommendation.** High. Two open questions in unfamiliar code, and the wingman answer decides
a visible behaviour.

**Verify.** The decode is the deliverable; it lands as prose in `docs/formats/instant-action.md` and
as the values D9 and E11 use. If either half comes back empty, say so on the page and name the
fallback explicitly (D9's mode-machine-on-team-1, and 3 / 6 / 9) as INVENTED.

**⚠ Traps.** (a) Do not read a `player`-trailer net found in a **story** mission as the Instant
Action wingman mechanism; those are authored per mission and the Instant Action maps are IA1 folders.
(b) Two of the nine pilot stats improve **downward** (`dead_eye_angle`, `steady_hand_chance`), so a
rating-to-stat mapping read as "higher is better" everywhere will be wrong in two places
(`Mech3/AiSkills.cs` carries the warning). (c) `natural_touch` deliberately has no
`ai_skill_parameters` entry and asking for it throws.

## A4 ☑ Re-read `FUN_0045b9d0`: the zeppelin branch, the mission-type ids, the spawn-point source

**Landed 2026-08-14. All three questions answered; the record is the "wave sequencer and the mission
end" section of [`formats/instant-action.md`](formats/instant-action.md).** (1) The branch
**replaces** the teleport: the discriminator at `0x0045ba9b` is an `if`/`else` on the mission-type
id and the type-2 arm jumps past the entire teleport block, so on `zeppelin_run` the sequencer never
picks a spawn point, never moves an aircraft and never reactivates one. Every enemy reaches the air
out of the zeppelin's bay. (2) The ids were already A3's, and this read corroborates them from the
other side: `FUN_0045a150` uses `FUN_00458c20`'s return directly as the index into the 20-byte
per-mission-type table. (3) The spawn source is `ia.json`'s own `spawn_points`, appended per
scenario by `FUN_00459ef0` as 16-byte `[x, y, z, heading-in-radians]` records into the very vector
the sequencer reads, so trap (c) is answered: not a separate list. The reference point for the
500 m test is the local player's vehicle (`DAT_0071c298`), and ⚠ **when nothing is 500 m away the
index falls back to a literal 0 rather than a random pick**. A fourth thing fell out: the mission
builder deactivates all three zeppelins and merely declines to deactivate the selected one on a
zeppelin run, and the sequencer contains no zeppelin wake-up at all, which disproves the standing
note this plan set out to close (see F12).

**Goal.** Two answers left out of one function. Whether the `mission-type-2` branch replaces the
teleport for that mode or runs alongside it. And which list the "at least 500 m from the player"
spawn point is drawn from, most likely the scenario's own `spawn_points` entries.

**Already answered by A3:** internal id 2 is `zeppelin_run` (0 ace, 1 squadron, 2 zeppelin,
3 ground target, 4 stunt), so the branch's behaviour and its id agree and the UI dropdown order is
simply not the internal order. A3 also found that waves 2 to 4 are built **deactivated at the world
origin** and that wave 1 spawns at a scenario spawn point drawn as `rand() % (count − 1)` with the
last index substituted on a collision with the player's, which is most of the third question.
It also narrows the first: ⚠ **on `zeppelin_run` even wave 1 is built deactivated at the origin**,
and the wave block runs there even with no spawn points at all, so on that one mode nothing is
airborne at mission start and every enemy waits on a release. Whether that release is the teleport,
the generator branch, or both is exactly what is left to read.

**Evidence (confidence: traced for the main path, lead-only for the branch).** The main path is
already written down verbatim in `analysis/m4-b7-group-slot/FINDINGS.md`: current-group counter
`DAT_00718cd0`, the wave-clear trigger, the `250000` squared-distance test, the 100 m fan at 45
degrees, and reactivation through `FUN_004b0f40(0)`. The branch is recorded only as a side finding of
the `capacity` puzzle: it adds the new group's member count to generator `+0x80` and stamps `+0x64`.
The M4 pass did not chase it, and said so.

**Approach.** Open `FUN_0045b9d0` and read it whole rather than searching for the two facts already
known. The mission-type discriminator will be a field on the mission state; follow it back to where
`mission_type` is parsed to settle the id ordering. Record the result in
`docs/formats/instant-action.md` (A1) and in
[`formats/mission-entities.md`](formats/mission-entities.md)'s capacity section, which currently
carries the puzzle.

**Model recommendation.** High. A single function, but three intertwined questions and the answer
reshapes F12.

**Verify.** The decode is the deliverable. The reading is corroborated in-engine at F12: if the branch
is launch-only, a zeppelin-run mission with waves must produce no teleport at all, and the `egen:`
lines must account for every enemy that appears.

**⚠ Traps.** (a) `capacity` is 0 on all 23 shipped generators and its blocking rule is still
unresolved (`Session/GeneratorCycle.cs` carries a documented stand-in); do not let this re-read turn
into a second attempt at that puzzle unless the function hands it over. (b) The two untraced launch
timers at `+0xac` and `+0xb4` are recorded in `BL-350` as deliberately not interpreted; leave them
that way. (c) The spawn-point source may be a separate stored list rather than `spawn_points`;
confirm rather than assume, because Instant Action's four `zeppelin_run` entries are a different
count from the other scenarios' eight.

## A5 ☑ The wrap-up screen's four counters: what each one actually counts

**Landed 2026-08-14. All four defined; the record is the "What the four numbers count" section of
[`formats/instant-action.md`](formats/instant-action.md).** Callback 2352 resolves to
`0x0040c644`, which formats all four rows from a snapshot block at `0x0064ad8c` that
`FUN_00419630` fills at mission end from one live counter object at `0x0071d2a0`.
**Shot % is cannon hits over cannon rounds fired, both by the local player**, so of the item's two
candidate readings it is the one that excludes ordnance, and it is symmetric: bit `0x40` on the
weapon def is the `CANNON` flag, the three hit sites test it directly, and the fire site is inside
the arm the same bit selects. **Time** is a truncating elapsed clock in milliseconds. **Enemies Shot
Down** sums two per-aircraft kill tables over the eleven types and counts only victims whose team is
greater than 1, which silently excludes non-aircraft kills (they go to a bucket the wrap-up never
reads) and, given A2, excludes wingmen you shoot down. **Danger Zones Completed** counts distinct
zones, latched so a second run of the same zone does not count, and shows `0` rather than hiding on
a non-stunt mission, which answers trap (b). Two edges are read from the instructions and not
observed: a zero denominator is unguarded and would print a large negative number, and both shot
counters are 32-bit but snapshotted as 16-bit. A bonus fell out of the launcher `FUN_004174d0`:
it confirms the mission-type dropdown map (0/1/4/2) from the launcher side, and its
environment-to-chapter-id map settles the environment table, including its row order. ⚠ **That map
corrects A1: "the clouds" is C2B and the omitted chapter is C1C**, which is what
`CSVM/src/UI/LaunchMenu.cs` already carried and what A1 contradicted by reasoning from world content
that never discriminated between the two. Recorded as wrong-claim 4a.

**Goal.** A sourced definition of each of the wrap-up screen's four numbers, so G14 renders the
original's arithmetic rather than a plausible reimplementation of it. The one that genuinely needs
settling is **Shot %**: its numerator and denominator are unknown, and the difference between "rounds
that hit over rounds fired" and "rounds that hit over rounds fired excluding ordnance" is a number
the player reads and compares.

**Evidence (confidence: lead-only for the counters, traced for the row count).** A1
(`docs/formats/instant-action.md`) found that only **four** of the six `langui` ids 1133-1138 are
wired to a row: *Time to Complete Mission*, *Enemies Shot Down*, *Danger Zones Completed*, *Shot %*.
*Total Kills* (`IDS_IAWU_KILLS_TITLE`, 1138) is a defined string with no line in `LAYOUT.CSV`, no
text object in `IA_WRAPUP.SCRIPT`, and no `IDS_IAWU_KILLS` format id in `RESOURCE.H` — it does not
render, so this item does not need a definition for it. The four wired rows have their formats
(`IDS_IAWU_TIME` is `%02d:%02d`, `IDS_IAWU_PERCENTAGE` is `%d%%`, the other two plain `%d`). What no
source yet gives is what feeds them. One numerator candidate fell out of A2 and is worth checking
first: `_DAT_0071d300` is incremented on every hit whose shooter is the local player and whose
weapon carries flag `0x40`, from **two** sites: `0x004ba04e` in the take-hit body `FUN_004b9bc0`
(the victim is a vehicle) and the same test in `FUN_004bab50` (it is not). Two sites
for one counter is exactly the kind of thing a single-site read gets wrong.

**Approach.** `ASSETS/SCRIPTS/IA_WRAPUP.SCRIPT` is already extracted and is the cheap half: read its
`object` declarations and the callback ids they fill from, the same way `INSTANTACTION.SCRIPT` was
read for the setup screen. Each callback id then leads into `crimson.exe` for the counter it reads.
Record the result in `docs/formats/instant-action.md` (A1) beside the setup surface, since the two
screens are one feature.

**Model recommendation.** High. It starts as a script read but ends in the binary, and the value of
the item is entirely in not guessing.

**Verify.** The decode is the deliverable. Corroboration comes at G14: a mission flown with a known
round count and a known hit count reproduces the same percentage the definition predicts, which is
only a real check if the definition was written down first.

**⚠ Traps.** (a) Do not settle *Shot %* by picking whichever definition is easiest to instrument.
`FireControl` counts rounds and the projectile pool counts impacts, so both readings are equally
easy, which means convenience cannot choose between them. (b) *Danger Zones Completed* exists on the
wrap-up for every mission type, not only stunt flying, so check whether a squadron fight shows 0 or
shows the row at all before hiding it. (c) If the counters turn out to be per-mission-type, that is a
finding about the screen and belongs on the page, not a reason to widen this item into building the
board.

---

# Wave B — The primitives

## B6 ☑ `InstantActionDef`, its `ia.zrd.json` reader, and `--ia=<path>`

**Landed 2026-08-14.** `Mech3/InstantAction.cs` holds `InstantActionDef` and one shared
`BuildDef(ZrdrDict)` field-population path reached two ways: `Load` for a chapter's shipped
`ia.zrd.json`, `LoadFromJson` for a hand-authored `--ia=<path>` plain JSON object (mapped onto
the same `ZrdrDict` shape, not a second schema). Every optional key resolves to the original's
own decoded default (`FUN_00458ff0`/`FUN_00459390`/`FUN_00458d00`) rather than to null —
`PlayerPlane` reads "Devastator" on C2B, a wave with no `groupN` reads the built-in "Blake
Firebrand" wave, the ace defaults to "Marshall Bill Redmann" — and `NumWingmen`/every wave's
`NumEnemies` are forced to 0 on `dogfight_ace`, the decoded parse-time rule (confirmed: none of
the 8 shipped chapters' own `mission_type` is `dogfight_ace`, so this only exercises via
`--ia=`). `wingman_plane`, the wave-only `enemy_accentID`, and `ground_target_name`/
`ground_target_node` are decoded but deliberately left out (D9's field to add / an unreachable
mode). `--ia=<path>` is `SessionSpec.IaPath`, a path value only — no file I/O in the spec, per
its own purity contract; loading it is the runtime's job (C8). Verification:
`CSVM.Tests/InstantActionTests.cs` (6 facts: the full-record fixture including the
num_enemies-clamped-to-6 and bare-`groupN,null`-wave cases, the built-in-defaults-plus-ace-zeroing
fixture, the `--ia=` JSON-object path including a JSON-`null` wave, a non-object-root rejection,
and an 8-chapter install golden re-read directly from `extracted/<chapter>/IA1/zrdr/ia.zrd.json`
this session) plus two `SessionSpecTests` facts for the new flag — 138/138 passed, 0 skipped,
`CSVM_DATA_ROOT` set. `.\RunTests.ps1` run in full (build/units/engine/goldens).

**Goal.** One typed record describes a whole mission, and two of its three producers exist: the
shipped-file reader and the CLI. `--ia=<path>` flies a mission described by a hand-authored JSON
file, with the chapter's own `ia.zrd.json` as the default when no path is given.

**Evidence (confidence: traced).** The key set is censused in
[`formats/spawns.md`](formats/spawns.md) and re-read this session across all eight chapters:
`mission_type`, `disallow_missions`, `spawn_points`, `dzones`, `player_plane`, `num_wingmen`,
`group1` to `group4` (each `num_enemies` / `enemy_name` / `enemy_plane` / `enemy_skill`),
`zeppelin_type`, the three `*_zeppelin` node names, and the ace block (`ace_name`, `ace_plane`,
`ace_skill`, `ace_stats`, `ace_accentID`, `ace_pattern`, `ace_colorN`, `ace_decalN`). `player_plane`
and `num_wingmen` are absent on C2B and present on the other seven; `dzones` is absent on C1C and
C2B.

**Approach.** New `src/Mech3/InstantAction.cs` holding `InstantActionDef` and the zrdr reader, in the
shape of `Mech3/Zeppelins.cs` and `Mech3/EnemyGenerators.cs` (flat-alternating `KEY, [values…]`, per
the shared conventions in [`formats/README.md`](formats/README.md)). `Flight/SpawnPoints.LoadIa`
already parses `spawn_points` out of the same file and `Flight/StuntMission` parses `dzones`; keep
both where they are and have the def carry the rest, rather than moving working code. The `--ia=`
path deserializes a plain JSON object with the same field names onto the same record, which is a
small hand-written mapping and not a second schema. Add the `lives` field here, defaulted to 1, and
comment it as an engine extension.

**Model recommendation.** Medium. It is the house reader pattern with a golden-count test, executed
carefully.

**Verify.** `CSVM.Tests/InstantActionTests.cs` on hand-authored fixtures for every optional-key case
(C2B's missing `player_plane`, C1C's missing `dzones`, a `[null]` group), plus install goldens over
all eight chapters counting waves, wave sizes and ace presence, skipped when `extracted/` is absent.
Then `.\RunTests.ps1`.

**⚠ Traps.** (a) Colour triples in the ace block are always integer 0 to 255, never `weather.json`'s
dual float/int encoding (`Mech3/PaintScheme.cs`). (b) `ace_stats` is nine values in the same order
`vehicle.json` emits its nine pilot-skill keys, and that order is **inferred, not decoded**, because
every chapter stores all nines; record it as inferred in the type's own comment. (c) `enemy_name` is
an `MSG_*` key naming militia and aircraft together, not a plain name; it resolves through
`Mech3/Messages.cs` and an unknown key resolves to itself rather than to blank.

## B7 ☑ The team model, replacing `AimAssist.TeamOfPilot`'s stand-in

**Landed 2026-08-14.** `FlightController.Team` (nullable-backed, defaulting to
`AimAssist.TeamOfPilot(PlayerIndex)`) is the aircraft's side; whoever builds a mission aircraft
sets it explicitly and free flight/`--vs` are untouched since nothing sets it yet. Threaded
through the four named consumers — `ProjectilePool.CollectAircraft`/`CollectTurrets`/
`CollectFusedOrdnance` (the last stamps `Proj.Team` once at `Spawn`, never re-deriving it from the
shooter id per scan), `FlightController.SelectRankedTarget`'s `ownTeam`, `TurretController`'s
carried-turret registration and `EngineTeamFor`'s ally case (now the fixed `AimAssist.PlayerTeam`,
never a particular pilot's own), and `AiVoiceRuntime`'s `Register`/`DeathCry`/`Broadcast` calls —
plus a `TurretController.Spawn` correctness fix along the way: a world emplacement's own fired
rounds now carry its real `EngineTeam` instead of reading as `NeutralTeam` through the
`NoShooter` shooter-id fallback. **Added no damage gate**, per Decision 3/A2. M4 E16's voice
trigger id 20 (`DA`) is wired (fires when the dying aircraft's `Team` equals
`AimAssist.PlayerTeam`); ids 22–24 and 28 are documented as reachable in `combat-voice.md` but
left unwired, per the item's own scope. Verification: the `team-model` in-engine suite (two
distinct `PlayerIndex` values sharing one explicit team via the aim-assist scan, plus a real fired
round proving A2's no-damage-gate corroboration) alongside the five named baseline suites
(`ai-gunnery`, `ai-modes`, `ai-voice`, `carried-turrets`, `world-turrets`), unchanged —
`.\RunTests.ps1`: build clean, 1181/1181 unit tests, 54/54 engine suites, all 14 golden hashes
(the manifest has grown since the plan's "eleven" was written) hash-identical.

**Goal.** An aircraft has a team, and everything that already asks "is this hostile" asks the team
instead of a stand-in. Team 1 is the player's side, team 2 and up are enemies, team 0 is neutral and
is not a wildcard.

**Evidence (confidence: lead-only for the damage gate, traced for the convention).** The id
convention is decoded: M4 C9b established that an absent `TEAM` on a world emplacement defaults to
the **first enemy team, id 2**, with 0 neutral and 1 ally, and that the four authored `TEAM 1`
entries are the player's own defensive rings. The stand-in it replaces is explicit in
`AimAssist.cs:310-317`: pilot index plus one, chosen because "CSVM has NO team model at all". M4 E16
recorded voice trigger id 20 as unreachable for the same reason. The gate does **not** cover damage:
A2 decoded the original's damage path as team-blind (`org/vehicleDamage.md`, "Teams and friendly
fire"), so this item is a targeting and announcement change only.

**Approach.** A `Team` field on `FlightController`, set by whatever builds the aircraft, with the
existing `TeamOfPilot` becoming the default for sessions that have no mission (free flight, Dogfight)
so nothing outside Instant Action changes behaviour. Then thread it through the four existing
consumers: `AimAssist`'s candidate registration, `AiGunner`'s nearest-hostile pick,
`TurretController`'s target selection, and `AiVoiceDispatcher.Register`. Wire M4 E16's id 20 while
there, since its blocker is being removed. **Add no damage gate**, and say in the code comment that
the omission is decoded rather than an oversight, because that is the line a later reader will
otherwise "fix". Voice triggers 22 to 24 and 28 become reachable at the same time and by the same
argument ([`formats/combat-voice.md`](formats/combat-voice.md)); wiring them is optional here, but
leaving their rows in that page's dispatch table stale is not.

**Model recommendation.** High. It replaces a convention that four subsystems read, and the failure
mode is silent (an AI that no longer shoots anyone, or shoots its own side).

**Verify.** Baseline first, because this is global: capture the eleven golden hashes and the
`ai-gunnery`, `ai-modes`, `ai-voice`, `carried-turrets` and `world-turrets` suites before the change,
then confirm each is unchanged after, since Dogfight and free flight must keep the old behaviour
exactly. Then a new assertion that a team-1 AI ignores a team-1 human and engages a team-2 one, which
must be watched failing first. A2's own corroboration belongs in the same pair: a team-1 round that
does reach a team-1 human still costs it HP. That one passes vacuously today (nothing gates damage
now), so it is only a real check once written against a deliberately gated build and watched failing.

**⚠ Traps.** (a) Team 0 rejects a pair from **either** side; it is not "hostile to everyone" and not
"friendly to everyone" (`AimAssist.cs:409-410`). (b) `AimAssist.WorldTeam` is 100 and exists so world
destructibles are hostile to every pilot; do not fold it into the new scheme without checking every
`AddStructure` caller. (c) `AiAircraftSpawner.ShooterIdBase` is 100 and shooter ids are **not** team
ids; the two must stop being derived from each other, which is the whole point of this item. (d) A
Dogfight match scores an AI kill as a plain death because the shooter id sits outside the match
roster; that is deliberate and must survive.

---

# Wave C — Dogfighting an ace

## C8 ☑ The Instant Action runtime and the authored ace

**Landed 2026-08-14.** `Session/InstantActionRuntime.cs` is new: it holds the loaded
`InstantActionDef` plus two engine-free static helpers, `ChooseAceSpawn` (the setup path's own
`rand() % (count-1)`-with-last-index-substitution draw) and `RepresentativeRating` (an
`AiSkillVector` collapsed to the one flat rating CSVM's AI tuning takes — every shipped
`ace_stats` is a uniform 9, so this is an averaging policy for a hand-authored `--ia=` file, not a
decode). `GameSession.StartSession` loads `--ia=<path>` via `InstantAction.LoadFromJson` before any
archive opens (fails soft — a warning, no mission — on a bad path); `BuildFlightRigs` then reads
the def directly: `MissionType` is the scenario key `SpawnPoints.LoadIa`/`SpawnPicker` already draw
from (no redundant `--scenario=` needed), `PlayerPlane` (via `InstantAction.PlaneNodeFor`'s new
eleven-entry display-name table) overrides `--plane=` for every human alike, and every human takes
`AimAssist.PlayerTeam` (Decision 8) regardless of pilot index. For `dogfight_ace` the ace itself is
spawned through a new non-optional-parameter `GameSession.SpawnAiAircraft` overload — kept separate
from the original 4-parameter one because C# does not extend a method-group-to-delegate conversion
(`AiGeneratorRuntime`'s spawn callback) over trailing optional parameters — carrying its authored
`PaintScheme` (worn as-is, no RNG draw, so a pinned golden's livery cannot move) and
`InstantActionRuntime.EnemyTeam` (2), armed at its averaged rating regardless of `--ai-attack=`, and
voiced through its `ace_accentID`. D9/E10/E11/F12 are the remaining mission types and the wave
sequencer; this item wires `dogfight_ace` alone. Verification: the new `instant-action` in-engine
suite (the plane-node table, `ChooseAceSpawn`'s collision substitution, `RepresentativeRating`'s
averaging, and a real `AiAircraftSpawner.Spawn` call proving the ace lands on team 2 flying its
configured airframe with its authored `PaintScheme` passed straight through) plus a manual
`--det --ia=<path> --chapter=C1 --frames=120 --screenshot=` run, whose log lines confirm the whole
build path fired for real (`ia: ace 'MSG_TEST_ACE' (player_warhawk) rating=9 team=2 spawn #5 of 8`,
`[flight] spawn [C1/IA1 dogfight_ace #0 of 8]` — the latter only reads correctly because of the
`SpawnPicker.ScenarioOverride` fix that rode along, see trap (d)). **Not done: the item's own
pixel-sample check** — the ace spawned 1803 m out at frame 120, too far to sample; the suite's
direct `Spawn(..., scheme: ...)` assertion is the livery evidence this session has, and a closer
`--det` framing is left for whoever next needs the screenshot.
`.\RunTests.ps1`: build clean, 1181/1181 unit tests, 55/55 engine suites (the new one included),
all 14 golden hashes unchanged.

**Goal.** `--ia=<path>` with `mission_type: dogfight_ace` builds and flies a complete mission: the
right chapter, a spawn from the right scenario list, the player's configured aircraft, and one
hostile ace on team 2 wearing its authored livery, flying its authored aircraft at rating 9 with its
authored voice.

**Evidence (confidence: lead-only for the runtime shape, traced for the ace record).** The ace is
fully authored in all eight chapters and the fields map onto existing types with no translation:
`ace_pattern` / `ace_colorN` / `ace_decalN` are exactly `PaintScheme`'s fields under different names
(stated in [`formats/paint.md`](formats/paint.md)), `ace_accentID` is the same field as
`vehicle.json`'s `accentID` and resolves through M4 B8's chain, and `ace_skill` is `ace` with
`ace_stats` all nines. `AiAircraftSpawner.Spawn` already builds a painted, hittable, damageable,
AI-flown aircraft with a crash runtime, and `--ai=…:accent=N` already gives one a voice.

**Approach.** New `Session/InstantActionRuntime.cs`, constructed by `GameSession` when the spec
carries a def, owning: environment to chapter resolution, mission type to spawn scenario, and the
actor set. For this item the actor set is the ace alone. Spawn it through the existing
`GameSession.SpawnAiAircraft` seam rather than a new path, passing the ace's `PaintScheme` instead of
letting the livery resolver draw one. Read `docs/architecture.md`'s entries for `GameSession.cs`,
`AiAircraftSpawner.cs` and `LiveryResolver.cs` before touching any of them.

**Model recommendation.** High. It is the seam every later wave hangs off, and getting its ownership
boundaries wrong is expensive to undo.

**Verify.** A `--det` screenshot of the ace in the air with its authored livery, compared against the
`ace_colorN` triple by pixel sample, so the livery is proved to be the authored one rather than a
random draw. A new `instant-action` in-engine suite asserting one hostile aircraft exists, on team 2,
with the configured airframe. Then `.\RunTests.ps1` with the eleven goldens unchanged, since no
existing session path should move.

**⚠ Traps.** (a) The paint RNG draw order is load-bearing under `--det`
(`Session/LiveryResolver.cs`): players draw at build and AI at spawn, and an ace that takes an
authored scheme must **not** consume a draw, or every pinned livery in the goldens moves. (b) The
spawned subtree is deliberately not indexed into the world runtime's `NameResolver`; keep it that
way. (c) `--pos` beats the mission spawn list by a deliberate ordering in `SpawnPicker`; a mission
must not undo that. (d) **Found while landing:** `SpawnPicker.LogSpawn`'s printed tag names
`_spec.Scenario` directly, so overriding the spawn LIST's scenario key (the def's `MissionType`)
without also telling `SpawnPicker` left the `[flight] spawn […]` line printing the CLI's stale
scenario while the list itself was already correct — a log lie, not a functional bug, but the kind
that misleads whoever reads that line next. Fixed with `SpawnPicker.ScenarioOverride`, set once in
`BuildFlightRigs`.

---

# Wave D — Wingmen

## D9 ☑ Wingman spawn

**Landed 2026-08-14.** `Mech3/InstantAction.cs` gained `InstantActionDef.WingmanPlane` (reads
`wingman_plane`, defaulting to `"Devastator"` like `PlayerPlane`/`AcePlane` — authored by no
shipped chapter, so every install-golden chapter reads the default). `Session/InstantActionRuntime.cs`
gained two pure static helpers: `WingmanSlotFor(i)` — the decoded per-wingman standing order
(`100 · ((i >> 1) + 1)` m at ±45° off the player's spawn heading, which of wingmen 1/3 wingmen
2/4 escort (null = the player), the authored accent id 12/14/15/13/16) — and `FlownWingmen`,
decision 8a's `min(configured, 6 - humans)` flight-size clamp. `GameSession.BuildFlightRigs`
spawns them right after the ace block (mutually exclusive with it: `NumWingmen` is parse-time
forced to 0 on `dogfight_ace`), on `AimAssist.PlayerTeam`, fanned off `_rigs[0]`'s pose, wearing
the paint catalog's `player_fortune` entry (no RNG draw, same reason the ace's scheme is worn
as-is) and armed at a fixed `attackRating: 5` so every wingman's `Gunner`/`Machine` exist
regardless of `--ai-attack=`. Each wingman's `AiPilot` is kept as a local so
`Gunner.PrimaryTargetName`/`Machine.ActivationRange` (10000 m, the decoded activation-volume
override) can be set AFTER `SpawnAiAircraft` has populated them; wingmen 2 and 4's target is the
already-spawned `FlightController.Name` for wingmen 1/3 (ascending spawn order is load-bearing),
read off the real spawned node through the existing `AiGunner.PrimaryTargetName`/
`FlightController.SelectRankedTarget` seam (M4 D12) rather than a new mechanism — the escort
chain is targeting-only, matching A2's decoded friendly-fire finding: a wingman the player (or
another wingman) shoots down is correct, not a bug, and no gate was added. The clamp is logged
when it fires (`ia: wingmen clamped to N of M configured (...)`), never applied silently.
Verification: the `instant-action` suite gained the wingman fan/escort/accent-id table asserted
pure over all five indices, the decision-8a clamp table (1–4 humans against 5 configured
expecting 5/4/3/2, plus the below-cap 2-vs-2 case), and a real `AiAircraftSpawner.Spawn` census
proving 3 wingmen land on team 1 flying the configured airframe. Plus a manual
`--det --ia=<path> --chapter=C1 --frames=120 --screenshot=` run on a hand-authored
`dogfight_squadron`/3-wingmen/Fury mission: the log shows `ia: 3 wingman(s) (player_fury) team=1`,
each `ai voice:` line naming the right accent (12/14/15), and the AI mode machine actually
driving them (`patrol -> avoid crash -> patrol`), proving the whole build path — not just the
suite's direct `Spawn()` call — fires for real. `.\RunTests.ps1`: build clean, 1181/1181 unit
tests, 55/55 engine suites (the extended one included), all 14 golden hashes unchanged.

**Goal.** `num_wingmen` friendly aircraft fly with the player, in the configured wingman aircraft, on
team 1, doing whatever A3 says the original gives them.

**Evidence (confidence: traced).** `num_wingmen` is 3 in all seven chapters that carry it, the
range is 0 to 5 from `LAYOUT.CSV` (the parser clamps to 5 as well), and the wingman aircraft dropdown
is hidden entirely when the count is 0 (`INSTANTACTION.SCRIPT`, `if (0 == TU.QG) mail(10000, UU)`).
A3 decoded the rest and the INVENTED fallback is dropped: build each wingman as a roster block on
**team 1, group 0, no net, activation volumes ±10000 m, skill vector unset**, wearing `fortune` with
the six livery values the setup screen supplies, and give it the decoded `primary_target` chain (0, 1 and 3 on
the player; 2 and 4 on wingmen 1 and 3). Placement is `100 · ((i >> 1) + 1)` metres at `±45°` off
the player's spawn heading, sign `+` when `i & 3` is 1 or 2. Accent ids run 12, 14, 15, 13, 16.
Full detail in [`formats/instant-action.md`](formats/instant-action.md).

**Approach.** Extend `Session/InstantActionRuntime`'s actor set. Spawn through the same
`SpawnAiAircraft` seam as the ace, on team 1, with the `fortune` pattern (A3 confirmed it; the
player's own militia is Fortune Hunter and `player_fortune` is the only pattern covering all eleven
aircraft). `IDS_IA_FRIENDLYPLANENAME` is the original's naming format for a friendly; use it if the
HUD names them. ⚠ **`num_wingmen` is forced to 0 for `dogfight_ace`** by the original's own parser,
so C8's duel must stay solo however the def is authored.

**Model recommendation.** Medium. Mechanically it is the ace item repeated N times; the judgement was
spent in A3.

**Verify.** The `instant-action` suite gains a wingman census: N aircraft on team 1 with the
configured airframe, and 0 wingmen producing none and no orphaned state. The clamp gets its own row
per player count, 1 through 4 humans against 5 configured wingmen, expecting 5 / 4 / 3 / 2, plus a
below-cap case (2 humans, 2 wingmen) proving the configured count is untouched. At the controls, a
`PT-` item judging whether the flight reads as a flight, because that is not a thing a suite can
answer.

**⚠ Traps.** (a) Wingmen are team 1 with the humans, and A2 settled what that means: a wingman the
player shoots down is **correct**, not a bug. Do not add a gate to "fix" it, and expect the report. (b) `player_fortune` ships a pattern with no colours in `vehicle.json`; its
triple is inferred from artwork (`red, black, white`), which is stated as an inference in
[`formats/paint.md`](formats/paint.md) and must not be restated as fact. (c) Splitscreen humans add
to the flight rather than consuming the wingman budget, up to a flight of 6 (decisions 8 and 8a).
Only past that cap do wingmen give way, `min(num_wingmen, 6 - humans)`, and the clamp must be
reported in the spawn log rather than applied silently, since a player who asked for 5 wingmen and
got 2 has no other way to find out why. (d) **Found while landing:**
`GameSession.SpawnAiAircraft`'s `Gunner`/`Machine` are only built when
`attackRating ?? _spec.AiAttackSkill` resolves to something, so a wingman spawned with
`attackRating: null` (the naive reading of "skill vector unset") would never get an
`AiGunner`/`AiModeMachine` at all on a CLI launch that carries no `--ai-attack=` — silently
dropping the whole `primary_target` mechanism this item exists to wire. Fixed by passing a fixed
`attackRating: 5`, matching the ace's own unconditional arming.

---

# Wave E — Waves

## E10 ☑ The inert aircraft state

**Landed 2026-08-14.** `Flight/FlightController.cs` gained one flag, `Inert`, and one derived
question, `InPlay` (`!Crashed && !Inert`), which is now the single "is this aircraft present"
test every roster asks. The flag itself pushes only the two facts the engine can hold no other
way — the airframe's visibility and its body's collision layer — through a private
`ApplyPresence()` called from `Respawn` and `_Ready` (the two points where those pieces come
into existence; `Setup` runs `Respawn` before `_Ready` has built the body, which is why both are
needed). Everything else CONSULTS the flag rather than being torn down: `SimStep` and `_Process`
return immediately, `TakeProjectileHit` and `DebugForceCrash` refuse, `DriveAiGunner` drops a
standing target that leaves play, and outside the class `ProjectilePool.CollectAircraft` (so the
aim assist, `SelectRankedTarget` and the H22 hostile tracker all follow from one edit),
`ProximityFuseTriggered`, `BlastAircraftPass`, `TurretController.Alive` (a carried gunner on an
inert host), `AiPilot.Next`'s quarry test and `VersusHud`'s hostile draw read `InPlay`.
⚠ The pool's fuse and blast passes walk the pool's OWN roster rather than issuing a physics
query, so the collision layer that hides an inert plane from the hit ray never reaches them —
that is exactly why they read the flag. `AiVoiceRuntime.RegisterAi` mirrors `InPlay` into the
dispatcher's `Speaker.Alive`, the only thing there that can see a `FlightController`, so an
unlaunched wave is registered in speaker order but never elected for a broadcast.
`FlightController.Activate(pos, lookAt)` is the inverse: re-home, clear the flag, `Respawn` —
the original's teleport-then-reactivate in one call, and the seam E11 will drive.
`AiAircraftSpawner.Spawn` and `GameSession.SpawnAiAircraft` take `inert:`, set in the object
initializer before `Setup` and before the node joins the tree, so an aircraft built inert never
has a live frame; the spawn log says `INERT`.
Verification: a new `inert-aircraft` engine suite runs FOUR real instruments (a physics raycast
on the shared space state, `CollectAircraft` into a real `AimAssist.Scan`, a real round fired
through the pool, and `SimStep`) over three subjects — a live control, the inert aircraft, and
that same aircraft after `Activate` — because "did not appear in the list" is the check that
passes for the wrong reason (verification.md METHOD-9/METHOD-10). Each instrument is watched
answering YES on the control, NO on the inert plane, and YES again after activation, plus the
roster check that an inert plane is LISTED as a candidate and simply not live (trap (b): inert is
not "left off the roster", and E11 needs it listed to count a parked wave as present). Three
single-line perturbations were run and each one failed exactly the check it should:
dropping `ApplyPresence()` from `_Ready` failed the raycast (and NOTHING else — the round check
still passed, which is the two damage layers being independent), reverting `CollectAircraft` to
`!rig.Crashed` failed the scan and the roster check, and disabling `SimStep`'s guard failed the
sim-step check. The `ai-voice` suite gained the speaker-eligibility flip in both directions.
`.\RunTests.ps1`: build clean, 1181/1181 unit tests, 56/56 engine suites, all 14 golden hashes
unchanged. Plus a full 8-chapter `--freecam` sweep (zero errors, standing node/mesh counts) and
the D9 `--det --ia=<path> --chapter=C1 --frames=120` re-run, which still prints
`ia: 3 wingman(s) (player_fury) team=1` with the same accents and the same
`patrol -> avoid crash -> patrol` mode trace — the live path is untouched.
⚠ **Hit while landing, and `ai-actor` already carries the same warning:** a suite lives inside ONE
frame, so a body moved after creation stays invisible to space queries until a physics flush, and
no raycast finds it at the new spot. Re-measured here rather than taken on faith — the LIVE control
also stops answering the raycast once a sim step has moved it — so the suite activates its subject
at its build pose for the instrument flip and asserts the re-home separately off the flight model.
Nothing in the shipped code is affected; it is a constraint on how these suites are written.

**Goal.** An aircraft can be built and then held completely out of the session until it is activated:
not stepped, not collidable, not targetable by anything, not drawn, and not counted as living by
whatever asks "is this wave clear".

**Evidence (confidence: direction-sound).** The original spawns wave members deactivated and
reactivates them through `FUN_004b0f40(0)`, the inverse of the flag the spawn applied
(`analysis/m4-b7-group-slot/FINDINGS.md`), so the state exists in the original and the observable
behaviour is settled. A3 adds where they sit while inert: **waves 2 to 4 are built at the world
origin** (0, 0, 0) with the deactivated byte set. Read that as incidental, not as the mechanism, and
see trap (c) below. What is a judgement call is which of CSVM's per-aircraft systems must be told,
since CSVM's aircraft is assembled from more parts than the original's record.

**Approach.** A single flag on `FlightController` that the session's step loop honours, plus explicit
opt-outs for each thing that would otherwise see the aircraft: `AircraftBody` collision layers, the
`ProjectilePool` registration that makes it a hit target, `AimAssist` candidate registration, the
`AiVoiceDispatcher` speaker list, and visibility. Prefer one flag consulted in each place over five
independent teardowns, because the failure mode is a system that was missed.

**Model recommendation.** High. New lifecycle state on the highest-traffic flight class, and the
failure mode is an invisible aircraft that still absorbs bullets.

**Verify.** An assertion that builds an inert aircraft and proves each system does not see it: a
raycast that does not hit it, an `AimAssist` scan that does not return it, a projectile that passes
through, and a sim step that does not move it. Each of those must be watched failing against a live
aircraft first, since "did not appear in the list" is exactly the check that passes for the wrong
reason. Then the eleven goldens unchanged.

**⚠ Traps.** (a) Godot's default query mask is **all** layers, so a query that should not see an
inert plane must say so explicitly (`Flight/CollisionLayers.cs` carries this warning already). (b)
The pool registration in `_Ready` is what makes a plane a hit target, independently of its loadout
(`AiAircraftSpawner.cs:87-89`); inert must reach that, not just the collider. (c) Do not implement
inert by parking the aircraft far away; that was considered and rejected because it still ticks,
collides and burns CPU for the whole mission.

## E11 ☐ The wave sequencer

**Goal.** Up to four configured waves arrive one at a time: wave 1 at mission start, and each
subsequent wave when no member of the current wave is still alive, teleported in at least 500 m from
the nearest human, fanned 100 m apart at 45-degree offsets, wearing their militia's livery in one of
that militia's aircraft at their configured skill.

**Evidence (confidence: traced).** The law is quoted verbatim in this plan's data survey from
`analysis/m4-b7-group-slot/FINDINGS.md`: the counter, the wave-clear trigger, the `250000`
squared-distance test, the fan, and the reactivation call. The 500 m is measured from "the player"
in a single-player game; extending it to "the nearest human" is the splitscreen extension of
decision 8 and is named as such. The militia-to-aircraft list is the pattern-coverage table above.
A3 settled the rest of a wave member: **team 2, group N, `primary_target player`**, the plain
`<plane>` def, its militia's livery, and nine pilot stats rolled per aircraft from a five-row table
rather than derived from a skill. The wave's skill is a **hit-point** tier (0.875/1.0/1.25) applied
around the spawn, and an `accentID` of exactly 12 is re-rolled as `12 + rand() % 5`.

A4 read the sequencer whole and tightened three details this item must honour.
(a) **This teleport arm does not run on `zeppelin_run`** at all; that mode is F12's generator path
and E11 should not try to serve both. (b) The draw is two-step and not a rejection loop: collect
every spawn point at or beyond 500 m, then take `rand() % n` of that collection, and ⚠ **fall back
to index 0, not to a random index, when the collection is empty**. (c) The fan places the **first**
member exactly on the point and offsets only the rest, member `k` after it going
`100 · ((k >> 1) + 1)` m at `±π/4` off the spawn heading with the sign `+` when `k & 3` is 1 or 2,
so six aircraft sit at 0, 100, 100, 200, 200 and 300 m on sides −, +, +, −, −. One more rule that the
degenerate cases turn on: the original's "is this wave clear" walk treats a **deactivated** aircraft
as still present, so a wave parked inert never reads as cleared.

**Approach.** An engine-free `Session/InstantActionWaves.cs` holding the selection and trigger logic,
in the shape of `Session/GeneratorCycle.cs`, `Flight/VersusMatch.cs` and `Flight/ZeppelinDamage.cs`:
no `GD.*`, no `Godot.` type, no `Node`, so it is unit-testable off-engine. `InstantActionRuntime`
feeds it the live positions and takes back "activate these aircraft at these poses". A wave of 0
enemies is immediately clear and must fall through to the next wave rather than deadlocking.

**Model recommendation.** Medium. The law is decoded and the class is pure; the care goes into the
degenerate cases.

**Verify.** Off-engine tests for the trigger and the geometry: wave advance on last kill, a 0-enemy
wave falling through, the 500 m minimum honoured against the nearest of several humans, the fan
producing the right count at the right offsets, and no advance past wave 4. Then an in-engine
`instant-action` assertion that a scripted kill of wave 1 produces wave 2 at a legal distance.

**⚠ Traps.** (a) Deconflict against `Rng` stream ordering: any random pick here must draw from a
named stream so `--det` stays pinned, and must not be inserted into an existing stream's draw order.
(b) "No living enemy of the current group" must not count inert aircraft of *later* waves as living,
which is the obvious way to make the sequencer never advance. (c) Index catalogs with `RandiRange`,
never `(int)Randi() % n`; the uint cast goes negative half the time
(`Mech3/PaintScheme.cs` carries this warning). (d) The 100 m and 45 degrees are decoded values, not
TUNE; do not adjust them because the formation looks wide.

---

# Wave F — Attacking a zeppelin

## F12 ☐ Zeppelin mode, and waking C1/IA1's own zeppelin

**Goal.** `mission_type: zeppelin_run` flies a mission whose objective is a live zeppelin, destroyed
through the existing M4 F18 damage path, with its waves arriving by whatever route A4 establishes.

**Evidence (confidence: the wave source traced by A4, the deactivation composition still lead-only).**
The zeppelin runtime already exists in full: `Session/ZeppelinRuntime` places and flies records under
`--zeppelins`, M4 F18 wires per-part damage and owns the kill, and M4 F19 fires the broadside.
`ia.json` carries `zeppelin_type` (`cargo` in all eight) and the three `*_zeppelin` node names, all
resolving to `multiplayer1zep` in this install; `zeppelin_type` parses to 0 cargo / 1 passenger /
2 military and indexes the three resolved nodes. `ZeppelinRuntime.ZeppelinKilled` is the existing
kill signal.

⚠ **A4 disproved the standing note this item was written to close.** `Session/AiGeneratorRuntime`
and `formats/mission-entities.md` said C1/IA1's setup deactivates its zeppelin and *the Instant
Action wave logic would wake it*. The first half holds and has two independent causes (the mission
script `support\c1\ia1.gw`, and Instant Action's own builder, which deactivates all three zeppelins
on every mode but `zeppelin_run`). The second half is wrong: `FUN_0045b9d0` touches the generator's
`capacityRemaining` and group and nothing else, and there is no wake-up anywhere in it. On a zeppelin
run the target is simply **never deactivated by the builder** in the first place. What is still
untraced, and is this item's, is how that skip composes with the mission script's own deactivation
list, which names zeppelin nodes independently.

**Approach.** Have `InstantActionRuntime` request the zeppelin runtime for this mission rather than
requiring `--zeppelins`, and resolve the objective node through `zeppelin_type`. Rather than "waking"
the zeppelin, match the builder: on `zeppelin_run`, **suppress the deactivation of the selected node**
and let every other zeppelin stay off. Route waves per A4, which means the generator path only:
on each wave change, add the new group's member count to the selected zeppelin's generator and stamp
its group, and run no teleport. Subscribe `ZeppelinKilled` as the mode's win condition, handed to G13.

**Model recommendation.** High. A4 settled that this is a second spawn path rather than a reuse of
E11's, so the wave routing is this item's own work.

**Verify.** A run on C1/IA1 in which the zeppelin flies (it does not today without `--zeppelins`) and
is destroyable, with the `zep:` and `egen:` lines accounting for every enemy that appears. The
`zeppelin-motion`, `zeppelin-damage`, `zeppelin-broadside` and `zeppelin-launch` suites unchanged,
since none of their behaviour should move.

**⚠ Traps.** (a) A `deactivated` record is placed but held for a mission-script wake-up. Whatever
this item does about that must be scoped to the Instant Action objective zeppelin and must not
touch every deactivated record in the world. Prefer the original's shape (do not deactivate it)
over a wake-up, because per A4 no wake-up exists in the binary to copy. (b) The generator drop point is the origin node minus an invented 12 m
clearance, because the authored `cargobay` sits on the bay floor and an airframe spawned exactly
there dies into the hull on frame one; that value is already in `AiGeneratorRuntime` and must not be
re-derived. (c) `BL-350` is open and blocked: generator-spawned planes crash inside closed hangars
because the door animation is mission scripting we do not run. If zeppelin-mode waves come out of the
generator path, that bug is in this mission's way and must be named, not silently worked around. (d)
Effect templates snap to absolute world points and never track a moving host, so a hit effect on a
flying zeppelin stays behind; that is known and is not this item's bug.

---

# Wave G — Ends

## G13 ☐ End conditions, lives and spectating

**Goal.** Every mission type can be won and lost. Dogfighting an ace ends when the ace is down.
Dogfighting a squadron ends when all configured waves are cleared. Stunt flying ends when every
player has completed their zone set. Attacking a zeppelin ends when the zeppelin is destroyed. Any of
them is lost when the last human is out of lives, and a downed pilot with no lives left watches from
the spectator camera while the others fly on.

**Evidence (confidence: direction-sound).** The end conditions follow from the mission types and each
has an existing signal to hang off: `Downed` on the ace's controller, the sequencer's own state,
`StuntMission.CompletedAt` plus `StuntRace`'s existing all-finished handling, and
`ZeppelinRuntime.ZeppelinKilled`. Decision 10 fixes stunt on all-finished rather than first-past-the-
post, which is what `StuntRace` already does (a finisher parks showing their placing while the field
flies on, and the last one in raises the board). `lives` is an INVENTED extension with no authority in
the data; 1 is the default because the original is a one-life game.

**Approach.** End-condition evaluation belongs in `InstantActionRuntime`, one predicate per mission
type, fed by the signals above rather than by polling. Lives ride the existing `--vs` respawn arming
(`VersusRespawnDelay`, 3 s) with a per-pilot counter; at zero, hand the pane to `SpectatorCamera`
instead of respawning. `StuntRace`'s all-finished path is reused rather than reimplemented.

**Model recommendation.** High. It touches `FlightController`'s respawn path, which Dogfight also
uses, and a mistake there breaks a working mode.

**Verify.** One in-engine assertion per mission type, each driving the mode to its end through the
real signal (the `--debug-scoreboard` forced-kill pattern is the existing shape for this). Plus:
`lives=0` never ends on death, `lives=1` goes straight to spectate, and Dogfight's own respawn
behaviour is unchanged, which needs the `--vs` suites green before and after.

**⚠ Traps.** (a) `StuntMission` is deliberately **not** reset on respawn, so a mid-run crash keeps
the zones and the clock; lives must not change that. (b) `VersusMatch` treats kill target 0 and time
limit 0 as deliberately disabled end conditions; `lives=0` is the same shape and must be as
deliberate. (c) The match clock advances on sim dt only, so a halt freezes it with the sim; any
mission clock this item adds should follow that rule rather than wall time. (d) The spectator camera
exists for `--freecam` and has never been driven from a mid-session death; expect it to need a mode
that follows a live aircraft rather than free-flying.

## G14 ☐ The wrap-up board

**Goal.** A completed or failed mission shows the original's four rows: Time to Complete Mission,
Enemies Shot Down, Danger Zones Completed, Shot %.

**Evidence (confidence: traced).** The strings are decoded at `langui` ids 1133 to 1138, with their
formats (`IDS_IAWU_TIME` is `%02d:%02d`, `IDS_IAWU_PERCENTAGE` is `%d%%`), and `IA_WRAPUP.SCRIPT`
holds the screen's layout. A1 (`docs/formats/instant-action.md`) found that the sixth string,
*Total Kills*, is defined but never wired to a row by `LAYOUT.CSV` or `IA_WRAPUP.SCRIPT`, so this
board has four rows, not five. **A5 landed the arithmetic**, so nothing here is a judgement call any
more:

- **Time to Complete Mission** is elapsed mission time in milliseconds, rendered `ms / 60000` and
  `(ms / 1000) % 60`, both truncating.
- **Enemies Shot Down** counts only victims on a hostile team (team > 1) that are one of the eleven
  aircraft types. Non-aircraft kills go to a bucket the screen never reads, and a wingman the player
  shoots down does not count, which matters because A2 established the original lets you shoot one.
- **Danger Zones Completed** counts **distinct** zones, latched, so a repeat run of the same zone
  does not increment it. The row renders `0` on a mission with no zones rather than hiding.
- **Shot %** is `100 × cannon hits / cannon rounds fired`, both by the local player. Ordnance is
  excluded from **both** halves, so the ratio is over guns only.

⚠ Two edges A5 read from the instructions but did not observe in the original, both of which this
item must decide deliberately rather than reproduce blindly: the original does not guard a zero
denominator (fire no cannon round and the row would print a large negative number), and its two shot
counters are 32-bit but truncated to 16-bit at the snapshot. Neither is behaviour worth copying;
name whichever choice is made as a divergence.

**Approach.** A `CanvasLayer` board in the shape of `Flight/StuntScoreboard` and `Flight/VersusBoard`,
built by `GameSession` on its own layer as those two are. The counters come from existing sources
where they exist: `StuntMission` for zones, the kill accounting G13 already needs for the mission end,
and the mission clock. Shot % needs a shots-fired and shots-hit counter, which `FireControl` and the
projectile pool are the natural homes for; wire the counters A5's definition names, not both and a
choice at render time. Two of the four counters need a `CANNON`-class filter on the weapon, matching
the original's `0x40` flag; `WeaponDef` already carries the parsed key.

**Model recommendation.** Medium. It is the third instance of a board shape that exists twice, and
A5 has taken the judgement out of the arithmetic.

**Verify.** A `--screenshot` of the board in each mission type with `--det`, so the layout is pinned;
the numeric assertions ride G13's per-mode end tests, checking that the board's four values match the
session's own counters rather than checking the board can be drawn.

**⚠ Traps.** (a) `--debug-scoreboard` is the existing convention for forcing a board visible in a
scripted shot; follow it rather than inventing a new flag. (b) Race totals are deliberately not
persisted as best times while stunt solo runs are; do not change either rule while adding a board
that shows both. (c) A5 has landed, so Shot % has one definition and it is not negotiable: cannon
hits over cannon rounds fired. A wrong percentage is indistinguishable from a right one on screen
and would never be revisited, so do not let a convenient counter that already exists stand in for
the filtered one.

---

# Wave H — The menu

## H15 ☐ Top-level restructure and wizard steps 1 to 2

**Goal.** The launchscreen's top level reads Free Flight / Instant Action / Dogfight. Choosing
Instant Action gives step 1, the seven environments, then step 2, the mission types that environment
can host, with the lives setting beside them.

**Evidence (confidence: direction-sound).** `LaunchMenu` today is Mode then Chapter then Plane, three
rows mapping 1:1 onto `SessionSpec.MenuMode`'s ordinals, with input polled per frame through one
`MenuInput` per player. The filtering rule is data: `disallow_missions` bars a scenario outright (C1C
and C2B bar stunt flying), and a chapter with no `dzones` cannot host a stunt run (C1C and C2B again,
per [`formats/missions.md`](formats/missions.md)). The environment list and its order are decoded
(A5, from the launcher `FUN_004174d0`) and match `LaunchMenu.cs`'s existing `Chapters` table:
**C1, C2B, C3, C5, C1B, C4, C2**, with C1C not offered. The screen's environment step is therefore a
filter over the existing roster, not a new mapping to invent.

**Approach.** Extend `MenuMode` and the screen sequence rather than rewriting the menu; the existing
re-entrancy rules (`ShowMenu` resets to Mode, clears locks, primes input edges from the current raw
state so a held Esc or Start does not read as a fresh press) must survive. The lives control is a
small numeric stepper on the same screen as the mission choice.

**Model recommendation.** Medium. Established UI with well-documented invariants; the work is
following them.

**Verify.** `--menu=` screenshots of each new screen at 1 and 4 players, since `LayoutScale` caps
fonts so 4P fits 720p and a new screen can break that. Confirm the mode rows still map onto their
ordinals by launching each from the menu.

**⚠ Traps.** (a) Player 1 is the keyboard plus the **set** of all unclaimed pads until `ClaimP1Pad`
pins its real pad, never `pads[0]`; using `pads[0]` re-breaks the phantom-device fix. (b) Dogfight
withholds the launch gesture below 2 joined players and says so in its hint line; Instant Action must
not inherit that gate. (c) `SessionSpec.FromMenu` does not re-resolve and must keep writing every
menu-settable field, or the pristine base re-opens the carry-over bug.

## H16 ☐ Wizard steps 3 to 5, and one build path from wizard and CLI

**Goal.** Steps 3 to 5 complete the wizard: a wave editor that starts empty and adds up to four
waves, each configured with count, militia, aircraft and skill; then the wingman count and aircraft;
then plane selection with splitscreen join. The wizard emits an `InstantActionDef` and launches it
through exactly the path `--ia=` uses.

**Evidence (confidence: direction-sound).** The option sets and their dependencies are decoded: the
aircraft list depends on the militia and resets to index 0 when the militia changes
(`INSTANTACTION.SCRIPT`, `AV[BA].QG = 0`), the wingman aircraft control is hidden at 0 wingmen, and
the ranges are 0 to 6 enemies, 13 militias, 3 skills, 0 to 5 wingmen. The original presents all four
waves as always-present dropdowns across two pages; your "empty at start, add a wave" editor is a
deliberate presentation divergence and is recorded as such in decision 1's boundary.

**Approach.** Reuse the existing pane machinery: with more than one player the plane screen already
becomes real `SplitScreen.PaneRect` panes and joining is gated to it. Build the def in the menu and
hand it to `SessionSpec.FromMenu`, which already exists to carry menu state into a spec without
re-resolving. The wave editor is the only genuinely new widget.

**Model recommendation.** Medium. The widget is new; everything it plugs into is established.

**Verify.** `--menu=` screenshots of the wave editor with 0, 1 and 4 waves configured, and of the
plane screen showing the wingman count re-clamped as a third and fourth pilot join. A test that a
wizard-built def and the equivalent `--ia=` JSON produce byte-identical `InstantActionDef` values,
which is what "one build path" actually means and is the only check that catches the two drifting
apart. The clamp must not touch the def's stored `num_wingmen`, only how many are flown, so that
check has to compare a 4-player wizard def against its 1-player equivalent and find them identical.

**⚠ Traps.** (a) Joining is gated to the plane screen today, so a player who joins at step 5 has not
seen steps 1 to 4. A late joiner changes **nothing** about the mission except one thing: the wingman
count is re-clamped to `min(num_wingmen, 6 - humans)` (decision 8a), and only when the join pushes
the flight over 6. Everything else the def carries belongs to whoever configured it. Show the clamped
count on the plane screen as it changes, so the flight the pilots see is the flight they get. (b) A militia with one aircraft (Black
Swan, Hollywood Knight, Russian, German, Broadway Bomber) gives a one-entry dropdown; make sure it
does not read as broken. (c) Fortune Hunter as an enemy militia is legal in the original's list and
gives all eleven aircraft; do not filter it out for being the player's own side.
