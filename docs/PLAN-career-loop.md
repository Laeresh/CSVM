# Career mode, phase 1: the loop on one world

**ACTIVE PLAN** (written 2026-11-23). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

This plan delivers phase 1 of the career mode designed in [`career-mode.md`](career-mode.md): the
smallest playable career loop on one chapter world. A player starts a career, takes a generated
patrol or strike contract from one port's board, flies it, and comes back to a debrief that pays
the fee, returns unfired ammunition to the hold and leaves the plane's damage on it until it is
repaired. Days pass, and the weekly bill (wages, upkeep, debt) is charged. The career is a
remake-only feature: nothing in it is a decode, and every number is a tune.

Out of scope, each in a later phase of the design doc's roadmap: travel and the strategic map, more
than one port, the zeppelin on the sortie map and the relaunch (phase 3), pilot skills, hiring,
injury and the full market (phase 2), negotiation, modifiers, prizes and the other job types
(phase 4), standing, heat, rivals, events, morale and traitors (phase 5), Legends acts and co-op
(phase 6), generated worlds (phase 7). This plan draws no `backlog` issues. Its whole substance is
the design doc and the grilling that settled it in one session; no item's evidence was checked
against the code in that session, so every item is `lead-only`.

## Milestone goal

- A career can be started, saved, quit and resumed from the main menu.
- One port on one chapter offers a seeded contract board with patrol and strike contracts, each
  with a threat rating, a fee and an expiry.
- A contract builds a generated sortie, with new ground targets placed into the original world for
  strikes, and the same seed always builds the same sortie (`--career-seed=`).
- The sortie reports back what happened; the debrief pays, the hold's ammunition stock goes down by
  what was fired, and zone damage and damaged parts stay on the plane.
- The calendar advances by repair and rest days, and a weekly bill is charged against the ledger.

**Phase 1 is the loop and nothing else.** Every system the roadmap puts in a later phase stays out,
even where a stub would be easy, so the loop is judged at the controls on its own before anything
builds on it.

## Decisions (2026-11-23)

From the design grilling. The career-wide decisions are recorded here because phase 1 is where they
first bind; the design doc holds the rest.

| # | Question | Decision |
|---|---|---|
| 1 | Where the career sits | **A new milestone**, since the campaign is complete and the project is polishing |
| 2 | First playable slice | **The loop on one world**: ledger, calendar, patrol and strike, added ground targets, persistent damage, ammunition stock, one port, no crew skills |
| 3 | Time model | **Days plus a weekly payday** |
| 4 | Damage carried between sorties | **Armour zones and damaged parts**; no airframe wear |
| 5 | Ammunition economy | **Stock in the hold**; slug rounds always for sale, specialist rounds and rockets limited by market stock |
| 6 | Captain shot down with no relaunch | **The sortie ends**; surviving wingmen withdraw, pay only for objectives met, injury roll. In phase 1 there is never a relaunch (no zeppelin on the map) |
| 7 | New ground targets in original worlds | **Allowed in a career sortie**, install models placed as prefabs, checked against the built world; never changes a campaign or Instant Action mission |
| 8 | Menu presentation | **Modern presentation only** for new screens; existing original pages reused where they match. Career logic stays UI-free because the modern presentation will be replaced |
| 9 | Starting region | **Player chooses** later; each region has a threat floor. Phase 1 has one region only |
| 10 | The captain always flies | **Yes**; no auto-resolve |
| 11 | A bill cash cannot cover | **The shortfall becomes debt**; cash never goes negative, seizure is a later phase |
| 12 | Where the tunes live | **One C# class**, `CareerTunes`; a JSON override can come later |
| 13 | How repair works | **The whole plane in one action**: one quote in dollars and days |
| 14 | A damaged part in flight | **A damaged gun cannot fire; a damaged engine loses some top speed** |
| 15 | How gun ammunition is counted | **Rounds per calibre and kind**; slug unlimited at the port, sold per hundred |
| 16 | The board's behaviour | **Five slots, daily refill**; offers last 3 to 7 days |
| 17 | Employers in phase 1 | **Kinds only** (nation, corporation, militia, broker); named employers arrive with standing |
| 18 | Save layout | **One file per career plus a backup**, `user://Careers/<name>.json` and `.json.bak` |
| 19 | Wingmen in phase 1 | **The sortie supports all three wingman seats; the starting gang fills one.** Instant Action already flies up to five wingmen (`InstantActionRuntime.FlownWingmen`), so three is in the engine's range. The other seats fill as the gang grows, once hiring and the market arrive in phase 2 |
| 20 | When the site catalogue is built | **At runtime** from the install, cached in memory; nothing derived from game data is committed |
| 21 | Terrain tags (sea, land, flat ground) | **Checked at placement** in the built world (B13), not stored in the catalogue |

## ⚠ Read this before implementing anything

Nothing has been disproven yet; the table below is the confidence roll-up only.

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | none | |
| **Direction sound, magnitude a judgement call** | none | |
| **Leads only, no mechanism yet** | B12 to C25 | Every item rests on the design discussion. Read the modules named in each Approach before building; an item may end in a redesign of its seam. All economy numbers are TUNE. |

## What the data actually ships

What the design leans on, cited from the repo's docs (not re-checked against code in the planning
session):

- **Prices.** Airframe costs ($1,870 Balmoral to $6,800 Hoplite), gun costs per calibre for wing
  and turret mounts, $410 and 480 lb per hardpoint, armour at $20 per five units: the decode in
  [`org/hangar.md`](org/hangar.md).
- **The saved-plane record** keeps four armour zone dwords (nose, tail, left wing, right wing),
  which is what lets per-zone damage persist: [`org/hangar.md`](org/hangar.md) and
  [`formats/paint.md`](formats/paint.md) "Saved custom planes". `CustomPlaneDef` is the remake's
  pure model of that record.
- **Stock loadouts** of the 11 planes: `CSVM/data/stock_loadouts.json`,
  [`formats/loadouts.md`](formats/loadouts.md).
- **Session building from a description:** `InstantActionDef` and its three producers
  (`src/Mech3/InstantAction.cs`).
- **Site sources** for the catalogue: AI nets (`src/Mech3/AiNets.cs`), landing approaches
  (`src/Mech3/LandingApproaches.cs`), spawn tables (`src/Flight/Modes/SpawnPoints.cs`), enemy
  generators (`src/Mech3/EnemyGenerators.cs`), mission targets
  (`src/Flight/Weapons/MissionTargets.cs`), the world query (`IWorldQuery`).
- **Out-of-mission flow:** `CampaignFlow` and its `ICampaignPage` mount point
  (`src/UI/Campaign/CampaignFlow.cs`), the flight check and ammo selection pages.

<TODO: which chapter world phase 1 uses. The design doc makes Hawaii (`C3`) the easiest region;
confirm `C3` has enough AI nets, flat ground and destructibles for patrol and strike sites.>

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the mistake this project makes most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + the module's entry in `docs/architecture/<Namespace>.md` (plus its index
  bullet in `docs/architecture.md`) / `docs/formats/` are updated in the same turn** as each landed
  item; a landed item gets its record in the landing commit's message and its issue is **closed**. New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything**, the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture/<Namespace>.md` (found through the index in
  `docs/architecture.md`) before modifying it,** then the comments on the members you touch; dead
  ends are in the landing commits (`git log --grep=<ID>`), so search those before re-chasing one.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A, the engine-free career core

1. ☑ `Career` namespace, `CareerState` and the versioned save
2. ☑ Calendar and ledger, with the weekly bill and the debt
3. ☑ Plane instances: zone condition, damaged parts, repair cost and days
4. ☑ The hold and the port's ammunition shelf
5. ☑ Contracts and the seeded board (patrol, strike, threat rating, fee, expiry)

### Wave B, the sortie seam

11. ☑ The site catalogue for the phase 1 chapter
12. ☐ `MissionSpec` and the generator for patrol and strike
13. ☐ The ground-target placer and the first prefabs
14. ☐ The session builds a sortie from a `MissionSpec` (`--career-seed=`)
15. ☐ `SortieResult`: what the sortie reports back

### Wave C, the screens and the loop

21. ☐ Career start, save slots and the main menu entry
22. ☐ The bridge, contract board and ledger pages
23. ☐ Flight check and briefing over the career, loading from the hold
24. ☐ Debrief, the bill and the hangar's repair
25. ☐ The economy simulation and the first tune pass

## Dependency and parallelism notes

A1 blocks everything. A2 to A5 depend only on A1 and can run in parallel, each owning its own new
file under the `Career` namespace. B11 is independent of Wave A and can start at once. B12 needs A5
and B11; B13 needs B11; B14 needs B12 and B13; B15 needs B14 and A3/A4 (it writes into them). Wave
C needs the Wave A types; C21 and C22 can start once A1 to A5 land, C23 and C24 need B14 and B15.
C25 needs everything. File contention: C22, C23 and C24 all add pages to the campaign flow's
stack; do not run them in parallel worktrees. <TODO: name the shared files once C21 has chosen
where the career's pages mount.>

---

# Wave A, the engine-free career core

## A1 ☑ `Career` namespace, `CareerState` and the versioned save

**Landed.** `Session.Career` namespace (inside the `Session` family, so the family-order test needed no new row). `CareerState` holds name, seed, day, ledger, planes, hold, shelf, board and the active contract; `CareerStore` writes one file per career at `user://Careers/<name>.json`, copies the previous file to `<name>.json.bak` before each save, and loads from the backup when the file does not read. Plane builds nest `CustomPlaneStore`'s JSON. Starting planes' builds come from the caller (C21), so Wave A reads no game data.

**Verified.** `CareerTests`: exact round-trip through a store and a second store instance, backup fallback on a damaged file, refusal of another version and of malformed text, delete removes both files.

**Original approach (kept for reference).**

**Goal.** A career's whole state is one engine-free object that round-trips through a JSON save at
`user://career/<name>.json`, with a version field and a backup written before each overwrite.

**Evidence (confidence: lead-only).** The design doc's data model sketch: `CareerState` holds the
calendar day, ledger, hangar, hold, the open contracts and the options; nothing in flight reads it.
The repo's existing stores (`ScoreStore`, campaign profiles) write under `user://`.

**Approach.** New namespace `Career` beside `Session`, engine-free so a unit test can drive it
without Godot. `CareerState` plus a store class that reads and writes it with an explicit version
and a backup. Phase 1 holds only the fields the plan needs (calendar, ledger, planes, hold,
contracts, starting options); later phases add theirs. <TODO: where in the namespace family order
`Career` ranks (see `docs/architecture.md`), and whether `Session` or `UI.Campaign` may depend on
it.> <TODO: whether the store reuses an existing JSON helper or the campaign profile's save path.>

**Model recommendation.** <TODO>

**Verify.** <TODO: unit tests: a state round-trips byte-stable; an older version loads or is refused
cleanly; a failed write leaves the backup readable.>

**⚠ Traps.** Every formatted number uses `CultureInfo.InvariantCulture` (coding conventions), or a
German-locale save will not load on another machine.

## A2 ☑ Calendar and ledger, with the weekly bill and the debt

**Landed.** `Ledger` (cash, debt, entries by day and category) and the calendar inside `CareerState` (`Day`, `Week`, `NextPayday`, `AdvanceDays`). A payday charges wages, upkeep, then interest and the instalment. A bill cash cannot cover becomes debt as arrears; cash never goes negative; seizure is deferred. All numbers live in the `CareerTunes` class (decision 12), with a JSON override possible later.

**Verified.** `CareerTests`: the first payday's figures by hand (cash 3,600, debt 24,500), and a broke payday's arrears plus interest.

**Original approach (kept for reference).**

**Goal.** Days advance by explicit actions (repair, rest); every seventh day the ledger charges
wages, upkeep and the debt instalment with interest, and every entry is listed by day and category.

**Evidence (confidence: lead-only).** Design doc "Time and money" and "Economy tunes": starting cash
$6,000, debt $25,000 at $1,000 a week with 2 % weekly interest, upkeep $500 a week, pilot wages
$150 to $900 by skill. All TUNE.

**Approach.** `Calendar` (day count, the next payday) and `Ledger` (entries, balance, debt, the
weekly bill computed from the state). Phase 1 has no fuel (no travel) and no mooring fee (one port).
Tunes live in one data file, not in code. <TODO: the tunes file's path and shape under
`CSVM/data/`.> <TODO: what phase 1 does when cash goes below zero: the design doc's bankruptcy rule
(seizure above a debt limit) is in scope or deferred.>

**Model recommendation.** <TODO>

**Verify.** <TODO: unit tests over the weekly bill and the interest, including a payday crossed by
a multi-day repair.>

**⚠ Traps.** <TODO>

## A3 ☑ Plane instances: zone condition, damaged parts, repair cost and days

**Landed.** `PlaneInstance` wraps an unchanged `CustomPlaneDef` and adds per-zone armour units lost (wings independent), damaged parts (four gun slots, the engine), sorties and kills. Repair is one action for the whole plane (decision 13): `QuoteRepair` charges $5 a unit and half of a part's hangar price, `CareerState.Repair` pays and passes the days. What a damaged part does in flight (decision 14: a damaged gun cannot fire, a damaged engine loses some top speed) is applied on the flight side in B14/B15.

**Verified.** `CareerTests`: the starting wear, a repair quote by hand, a paid repair clearing the damage and passing two days, damage clamped to the zone.

**Original approach (kept for reference).**

**Goal.** Each owned plane is an instance carrying its `CustomPlaneDef` record, the condition of its
four armour zones and a set of damaged parts, and the repair of each has a price and a number of
days.

**Evidence (confidence: lead-only).** Decision 4 (zones and parts, no wear). The saved-plane record's
four zone dwords ([`org/hangar.md`](org/hangar.md)). Repair tune: $5 per armour unit restored, half
the part price for a damaged part.

**Approach.** `PlaneInstance` wraps `CustomPlaneDef` (do not change the record's shape) and adds the
career fields. A damaged gun or engine is marked, not removed. Prices come from the decoded tables
the hangar already uses. <TODO: what a damaged part does in flight in phase 1 (a dead gun group, a
power cut on the engine, or nothing until a later phase).> <TODO: how many repair days per unit,
and whether repair is one action for the whole plane or per zone.>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** The original's ARMOR tab moves both wings together, but the record keeps four dwords
and the mission side damages one wing at a time ([`org/hangar.md`](org/hangar.md)); the instance
must keep the wings independent.

## A4 ☑ The hold and the port's ammunition shelf

**Landed.** `Hold` counts gun rounds per calibre and kind (decision 15) and rockets per `wep_*` id. `PortShelf` sells slug without limit and specialist rounds and rockets from a weekly stock rolled from the career's seed; rounds sell in lots of a hundred. Phase 1 sells ammunition only.

**Verified.** `CareerTests`: an unlimited slug purchase charged by hand, a specialist purchase limited by stock, a purchase cash cannot cover changing nothing, the restock pure in seed and week.

**Original approach (kept for reference).**

**Goal.** Ammunition and rockets are stock in the hold. The phase 1 port sells slug rounds without
limit and specialist rounds and rockets from a weekly stock.

**Evidence (confidence: lead-only).** Decision 5. Ammunition kinds `slug`, `dumdum`, `ap`,
`magnesium` ([`formats/loadouts.md`](formats/loadouts.md)); rocket ids under `wep_04` to `wep_15`
([`formats/weapons.md`](formats/weapons.md)). Price tune: AP and magnesium three times slug,
rockets $40 to $250 each.

**Approach.** `Hold` (counts per ammunition kind and calibre, per rocket id) and `PortShelf` (stock
and prices, refreshed on the weekly payday from a seed). Phase 1 sells ammunition only; the full
market for planes and parts is phase 2. <TODO: whether rounds are counted per calibre and kind, or
per gun group's magazine as `FireControl` counts them.> <TODO: what phase 1 sells besides
ammunition, if anything (armour for repairs counts as repair, not stock).>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** <TODO>

## A5 ☑ Contracts and the seeded board (patrol, strike, threat rating, fee, expiry)

**Landed.** `Contract` and `ContractBoard`: five slots, expired offers drop daily and empty slots refill (decision 16), offers last 3 to 7 days. Threat is the lower of two draws above the threat floor; the fee is a draw in the threat's band; employers are kinds only (decision 17). Draws key on the offer's id, so ids and offers replay exactly. `CareerState.Accept` takes one contract at a time.

**Verified.** `CareerTests`: two careers with one seed show identical boards after 20 days, fees inside their bands, the threat floor holding at 4, one active contract at a time.

**Original approach (kept for reference).**

**Goal.** The port's board offers a few contracts, each a patrol or a strike with a threat rating
of one to five skulls, a fee and an expiry in days, all derived from the career's seed and the day.

**Evidence (confidence: lead-only).** Design doc "Contracts" and "Economy tunes": one skull $1,500
to $2,500, five skulls $10,000 to $16,000; the region's threat floor sets the lowest rating offered.
Phase 1 has no negotiation, so a contract's terms are fixed.

**Approach.** `Contract` (employer, job type, seed, threat rating, fee, expiry) and a board generator
seeded per day. Expired contracts drop at day change; an accepted one leaves the board. <TODO: the
board's size, refresh rule and how many contracts per threat rating.> <TODO: employers in phase 1:
named placeholders or the chapter's militias (`MilitiaPaint`).>

**Model recommendation.** <TODO>

**Verify.** <TODO: unit tests: the same seed and day give the same board; the threat floor holds.>

**⚠ Traps.** <TODO>

# Wave B, the sortie seam

## B11 ☑ The site catalogue for the phase 1 chapter

**Landed.** `SiteCatalogue` in `Session.Career`. `Build` takes already-read nets, spawns and zeppelin starts; `Harvest` reads them from a chapter scope (`AiNets.Load`) and each mission scope (`SpawnPoints.LoadPlayerInit`, `Zeppelins.Load`), a missing file adding nothing. Routes are the fixed nets (a trailer-carried net is dropped: its points are offsets around a moving target), with their edge length. Ground candidates are the fixed nets' points, x and z only, one per 400 m cell. Terrain tags are not stored (decision 21): the placer checks sea, land and slope in the built world. Built at runtime, never committed (decision 20). The catalogue is chapter-agnostic, so which chapter phase 1 uses is still open; the synthetic data tree ships no nets, so per-chapter counts need the install.

**Verified.** `SiteCatalogueTests` over the committed `ainets` and `zeppelins` fixtures and a temp mission scope: trailer nets dropped (5, 11, 13 out; 7 and 9 kept), route lengths by hand, one candidate per cell, ground-plane queries ignoring height, a mission's start and zeppelins harvested, empty scopes giving an empty catalogue.

**Original approach (kept for reference).**

**Goal.** One chapter has a catalogue of tagged sites (`airfield`, `rail`, `canyon`, `city`,
`open_sea`, `dock` and so on) that the generator can ask for by tag inside an area.

**Evidence (confidence: lead-only).** Design doc "Procedural mission generation" step 1: sites are
harvested from data the remake already reads (AI nets, landing approaches, spawn tables, danger
zones, destructibles, open water).

**Approach.** Harvest once per chapter, engine-free where the sources are, and cache it. <TODO: the
chapter (see "What the data actually ships").> <TODO: harvested at build time into a committed
file, or at run time from the install; a committed file must hold positions only, never asset
data.> <TODO: the tag list and the rule that assigns each tag.>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** The repo never commits game assets. A committed catalogue may hold coordinates and tags
derived from the install, but nothing that reproduces world data in bulk; check against the hard
rule in PROJECT_CONTEXT.md before choosing the build-time option.

## B12 ☐ `MissionSpec` and the generator for patrol and strike

**Goal.** Given a contract and the site catalogue, the generator returns a `MissionSpec`: the world,
the area, the player's spawn, the enemies bought against the opposition budget, the placed targets
and the objectives. The same seed always returns the same spec.

**Evidence (confidence: lead-only).** Design doc steps 2, 3 and 5: job templates with roles; the
threat rating becomes an opposition budget in the same currency as the player's force; objectives
are primary (paid) and secondary (paid extra). Modifiers (step 4) are phase 4.

**Approach.** Engine-free generator, unit-tested on a hand-authored site catalogue. Patrol: a route
of waypoints over an AI net leg with enemy groups along it. Strike: two to five target sites and a
flak ring. Enemies come from the employer's enemy's roster with its paint. <TODO: how an enemy
plane is valued for the budget (airframe cost plus guns, or a separate tune table).> <TODO: whether
phase 1 has reinforcement waves through the enemy generators, or a fixed spawn only.> The spec carries up to three wingman seats, one per fit pilot with a fit
plane (decision 19); the starting gang fills one.

**Model recommendation.** <TODO>

**Verify.** Unit tests assert on the spec, not on pixels (design doc "Determinism"). <TODO: the
specific assertions: budget spent within a margin, every required role filled, seed stability.>

**⚠ Traps.** <TODO>

## B13 ☐ The ground-target placer and the first prefabs

**Goal.** A strike's targets are placed into the original world as prefabs of install models, each
checked against the built world, so nothing floats, buries itself or overlaps an original node.

**Evidence (confidence: lead-only).** Design doc "Added ground targets": the placer samples the
ground through the world query, refuses slopes above a limit, keeps a clearance from original nodes
and AI net flight volumes, and drops an object that fails. Prefabs are small authored layouts in
remake data naming install models.

**Approach.** A pure placement rule over `IWorldQuery` (`Ray` for ground height, `Overlaps` for
clearance), and a prefab data file. Phase 1 ships two or three prefabs (a fuel depot, a flak
battery). <TODO: which install models the prefabs name, and which reader loads a mission entity or
destructible outside the mission that authored it.> <TODO: the slope limit and clearance distances
(TUNE).>

**Model recommendation.** <TODO>

**Verify.** <TODO: a targeted capture of each prefab on the phase 1 chapter, plus the 8-chapter
freecam regression unchanged (the placer only runs in a career sortie).>

**⚠ Traps.** Decision 7: placement must never reach a campaign or Instant Action session. A placed
destructible needs its full damage and destroy choreography, not a static mesh.

## B14 ☐ The session builds a sortie from a `MissionSpec` (`--career-seed=`)

**Goal.** A `MissionSpec` launches a playable sortie, and `--career-seed=<seed>` builds and flies
one from the command line without a career save.

**Evidence (confidence: lead-only).** Design doc: the session builds a spec the way it builds
Instant Action from an `InstantActionDef`; nothing in flight reads `CareerState`.

**Approach.** Extend or sit beside the `InstantActionDef` producers so the session takes a spec.
<TODO: whether `InstantActionDef` can carry targets, objectives and enemy groups, or a new
definition type is needed (read `src/Mech3/InstantAction.cs` and its consumers first).> <TODO: how
objectives from a spec reach the existing objective and HUD message systems.> <TODO: where the
`--career-seed=` flag is parsed and its `docs/cli.md` bullet.>

**Model recommendation.** <TODO>

**Verify.** <TODO: a scripted `--career-seed=` run through `RunProbe.ps1`, the same seed twice
giving the same spawn and targets; a `tier:ci` suite if the synthetic data tree can host it.>

**⚠ Traps.** Every scripted Godot launch goes through `RunProbe.ps1`, never the executable directly.

## B15 ☐ `SortieResult`: what the sortie reports back

**Goal.** When a sortie ends, the session returns objectives met, rounds and rockets fired per plane,
zone damage and damaged parts per plane, crashes and kills, and the career applies them to the hold
and the plane instances.

**Evidence (confidence: lead-only).** Design doc data model sketch. `FireControl` already draws
ammunition down; the airframe already keeps armour and health per zone.

**Approach.** The session fills a plain record; the career applies it in one engine-free step that
a unit test can drive. A crashed captain ends the sortie (decision 6). <TODO: what a crashed plane
costs in phase 1: written off, or recovered as a wreck needing heavy repair.> <TODO: where the
session knows a sortie has ended, for a win, a loss and a quit.>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** <TODO>

# Wave C, the screens and the loop

## C21 ☐ Career start, save slots and the main menu entry

**Goal.** The main menu offers Career; the player can start a new career (a name and the starting
options phase 1 supports), continue one, or delete one.

**Evidence (confidence: lead-only).** Decision 8 (modern presentation only, logic UI-free). Design
doc "Career options".

**Approach.** New pages mounted in the campaign flow's stack, drawn by the modern presentation.
<TODO: where the career's pages mount, and whether `CampaignFlow` is shared or the career gets its
own flow over the same `ICampaignPage` seam.> <TODO: which options phase 1 exposes (economy level
only, or none).>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** The modern presentation will be replaced; keep every career operation callable without
a page, so the replacement draws the same state.

## C22 ☐ The bridge, contract board and ledger pages

**Goal.** The bridge shows the date, cash, debt, next payday and the way to every other page; the
contract board lists the offers; the ledger lists the entries by week.

**Evidence (confidence: lead-only).** Design doc "Screens".

**Approach.** Modern-presentation pages over the Wave A state. <TODO: layout and art; the design doc
reuses the campaign board's parchment and plaques where it fits.>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** <TODO>

## C23 ☐ Flight check and briefing over the career, loading from the hold

**Goal.** Accepting a contract shows the briefing (the generated map, objectives, known threats)
and the flight check, where the player picks a plane and loads ammunition from the hold.

**Evidence (confidence: lead-only).** Decision 8: existing original-presentation pages (briefing,
flight check, ammo selection) are reused where they match. `LoadoutChoice` holds a pilot's edits to
a fit.

**Approach.** <TODO: whether the briefing page can draw a generated map at all, or the career's
briefing is a new modern page.> <TODO: how the ammo selection page is limited by the hold's
stock.>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** <TODO>

## C24 ☐ Debrief, the bill and the hangar's repair

**Goal.** After a sortie the debrief shows objectives, kills, the fee, the ammunition used and the
damage; the hangar repairs zones and parts for money and days.

**Evidence (confidence: lead-only).** Design doc "Screens" (debrief, hangar). The custom-plane
hangar already edits armour per zone.

**Approach.** <TODO: reuse the existing debrief page or a new career debrief.> <TODO: the hangar in
phase 1 is repair only, or also refits from the hold.>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** <TODO>

## C25 ☐ The economy simulation and the first tune pass

**Goal.** A scripted gang takes random contracts for a hundred weeks, and the tunes are set so the
first two weeks are tight and a well-run gang is comfortable by the end of the region.

**Evidence (confidence: lead-only).** Design doc "Economy tunes": balance by simulation first, then
at the controls.

**Approach.** An engine-free simulation over the Wave A types with a sortie outcome model in place
of flying. <TODO: the outcome model (win odds per threat rating, ammunition and damage per sortie)
and where the simulation runs (a unit test, a script, a `--run-tests` suite).>

**Model recommendation.** <TODO>

**Verify.** <TODO: the curve's shape as the pass criterion, then a playtest at the controls.>

**⚠ Traps.** The tunes are TUNE, not fact; a number that only feels right in the simulation is
still owed a playtest.
