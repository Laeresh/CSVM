# Playtest checklist

Everything that needs a human at the controls (or the original game open for A/B), consolidated.
**This file holds only what is actionable *today*.** Anything whose test is blocked on an unlanded
fix lives on its `backlog.md` entry as a `*Playtest after fix:*` line instead — so an empty section
here means the work is queued, not forgotten. Deep evidence and traps live in
[`backlog.md`](backlog.md); the two are kept in step.

**Every item carries a stable ID** — `CAP-nn` for an owed capture, `PT-nn` for something to fly.
Cite them from `backlog.md` and in conversation the way `BL-nnn` is cited. IDs are permanent: when
an item closes its ID retires with it and is never reused, so numbering gaps are expected.
Retired IDs disappear from this file, so never mint a new ID by scanning the entries below — run
**`./New-ItemId.ps1 -Kind CAP`** (or `-Kind PT`), which increments a shared locked counter in
`.git/item-id-counters.json` and is safe under concurrent sessions. ⚠ **Run it for EVERY id, every
time**: it is not a once-per-session lookup, and deriving the next id by adding 1 leaves the counter
behind the file, so the number you invented gets handed out again later. `-Count n` reserves a block
in one call when you need several. Retired IDs' verdicts are in
the retiring commit's message (`git log --grep=<ID>`); pre-2026-08-06 retirements are in
`docs/HISTORY.md` (frozen).

**Structure.** Section 0 lists owed captures as themed tables — one table per filming batch, the
theme naming the capture setup (cockpit gauges in frame, external view, …) — with fixed columns
ID · Capture · What must be in frame · Unblocks. The theme sections are standing — an emptied
table stays, meaning nothing is currently owed in that batch. Section 1 groups flights by **flight
profile** — one section is one sortie (chapter + plane + situation), headed by a copyable launch
command. Sections sort by chapter then plane; items within a section by ascending ID. An item free
to choose its plane or chapter piggybacks on an existing profile — it never opens a section of its
own. Every PT item is one bullet:

    - `PT-nn` `[A/B: <ref>]`-or-`[Own]` **What to check (`BL-NNN`).** context… *Look for:* … *Blocks:* …

`[A/B: <ref>]` names the capture or `OriginalScreenshots/` shot to have open *before* launching;
`[Own]` is a judgement call on our own remake with no original reference. A mixed item takes
`[A/B]` — the per-check references stay in their bullets. *Look for:* holds one sub-bullet per
check, lettered `(a)(b)(c)` when there is more than one. *Blocks:* is mandatory — name what a pass
closes, or state outright that nothing tracks the outcome and a fail mints a new `BL` item.
Optional: context prose between title and *Look for:* (a few lines at most — deep evidence lives
in `backlog.md`), and *Variations:* for extra flags or re-runs beyond the section's command.

**Captures staged for an item live in `playtest/<ID>/`** — git-ignored (they are renders of the
player's own game files) and, unlike `.scratch/`, **not swept by `CleanScratch.ps1`**, so they
survive until the item that owns them closes. Delete the folder with the item.

**How to launch.** `./RunGame.ps1` (no args) builds and opens the in-game launchscreen
(Mode → Chapter → Plane, keyboard or pad). Any args bypass the menu and drop you straight in, e.g.
`./RunGame.ps1 --plane=player_fury --chapter=C1`. Flight is the default. Full flag list:
[`docs/cli.md`](docs/cli.md).

**Plane model names** (for `--plane=`): `player_autogyro` Hoplite · `player_avenger` Hellhound ·
`player_balmoral` Balmoral · `player_bhawk` Bloodhawk · `player_brigand` Brigand ·
`player_pfighter` Devastator · `player_fbrand` Firebrand · `player_fury` Fury ·
`player_kestrel` Kestrel · `player_peacemaker` Peacemaker · `player_warhawk` Warhawk.

**Cross-checked against the original design documentation 2026-07-25.** A *pre-release* spec: it
settles **system shape**, never numbers or art direction (rebalanced before release — extracted data
or an `OriginalScreenshots/` capture wins wherever either exists). **It is silent on the rest, so do
not re-run this cross-check:** all of the splitscreen work (the original's multiplayer was
networked, so there is no splitscreen reference at all); every *tuning* question — pitch, stall
recovery, dive speed, camera, mix levels, weather — which it covers with qualitative rules and no
numbers; and the C3 spiderweb, patrol-boat hit points, map-edge continuation, which world axis is
north, and the crossed `pdpN_h` numbering.

⚠ **The spec's HUD and damage material is unreliable as a class — three of its claims were
overturned by direct observation in one sitting (2026-07-30).** It said the low-altitude warning
beeps (it does not), that the ammo gauge's yellow tier meant gun heat/jam rather than ammo (it is a
real ammo tier, on gun belts only), and that the damage gauge ramps from a blue full-health state
(there is no blue tier and never was). Treat any remaining `[spec]`-sourced HUD claim as a
hypothesis to check, not a target to build against.

---

## 0 · Owed captures (`CAP-nn`)

**One sit-down list.** These are recordings and screenshots of *the original game*, not our build.
Most of the items below and a large part of `backlog.md` are blocked on one of these, so filming
them in a batch unblocks far more than doing them one at a time.

⚠ **Validity gate for every cockpit-gauge clip: auto head turn must be OFF, or the clip is
unusable.** This already cost two takes. The capture spec and the clip-validity rules were in
`analysis/video-flight-calibration/FINDINGS.md`, deleted 2026-08-14; recover them with
`git log -p -- analysis/video-flight-calibration/FINDINGS.md`.

### Flight model — cockpit gauges in frame, head turn off

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-20` | Throttle equilibria + a shallow held climb | Two level runs held to equilibrium at **1/4** and **1/2** throttle (the thrust-vs-throttle curve), then a **shallow, steady climb** at fixed throttle — shallow enough that the ADI does **not** saturate, i.e. keep the nose under ~+25°, and hold it 10 s+. `CAP-05`'s 50%-throttle clip failed on exactly this: it was a zoom, the ADI pinned at sky fraction 0.730, and the nose angle became unreadable. ⚠ Still owed after D32, and now the ONLY thing that can settle the climb residual: the 90° climb clip gives a clean speed plateau (163.05 mph at a 56.3° path) but its ADI saturates too, so the nose angle — and with it α, the leading candidate for the model's remaining +25% — is unreadable in every climb capture taken so far | `BL-410` (the sustained-climb residual; `ClimbGravityScale` itself is retired) |

⚠ **Partial STICK deflection cannot be captured: the controls are keyboard, so pitch, roll and
yaw are 100 % or 0 %.** Any capture asking for "a light, steady pull" or any other intermediate
*axis* position is unfilmable by construction, not merely unflown — do not file one, ask the binary
instead. This retired `CAP-32` (2026-08-15). **Throttle is not affected**: it is a stepped setting
and every eighth is reachable from the keyboard, which is how `CAP-31` flew 1/8 and `CAP-05` flew
50 %, so `CAP-20`'s 1/4 and 1/2 runs above remain perfectly filmable.

### HUD — ammo gauge in frame

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|

### Camera

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|

### Audio

| ID | Capture | What must be audible | Unblocks |
|---|---|---|---|

### Weather & visuals

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-34` | Wing-light flare shape + view-dependence | Any player plane except the Bloodhawk (the one airframe with no wing-light anim or flare nodes) with wing lights on, one continuous orbit from front through side to tail. Close enough to read whether the flare shows sharp radiating star points (vs a soft round glow) and whether it stays visible across the orbit or only from a narrow chase-view cone | `BL-284` |

### Damage & collision

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-26` | Rocket impacts, one clip per type, **with audio** | Fire each rocket type at open ground and film it close enough to count and orient the rings, with clean audio on the same take: `wep_04` (9M/incendiary), `wep_05` (ARMOR), `wep_06` (BOOM/HE), `wep_08` (SONIC). Two playtests point here: `PT-17` found HE's second ring present but its orientation "kinda random", and `PT-20` judged the sounds "a lot better" but not settleable by ear alone. *Look for:* ring count, ring orientation and how fast the burst reads (`BL-016`'s open "faster than the original" half), plus the launch bark and the per-type impact sound | `BL-016` (open half), `BL-211` |
| `CAP-27` | Does the original spark on the airframe at all? | Take damage in the original — a light scrape is enough — with the aircraft in frame (external/chase fine), and look for a **spark burst on the airframe itself**, distinct from smoke at the contact point. ⚠ This capture can **delete** a feature rather than tune one: our per-impact spark burst is driven by a 0.99 `injure_anims` entry that exists on **1 of 11** aircraft (the Devastator), plausibly an authoring leftover (was `BL-090` item 2, closed — `git log --grep=BL-090`). If the original never sparks, our implementation goes. If it does, `BL-281`'s ricochet mix can be judged | `BL-281` |
| `CAP-30` | Firing-wobble amplitude across calibers and airframes | Dead-astern external/chase clips, level flight, guns held 3 s+: **(a)** one plane with two well-separated calibers (30 vs 70), **(b)** one caliber on a light vs a heavy plane, **(c)** — added 2026-08-07 — a **Bloodhawk 40-cal** clip framed and fire-rate-matched to `Gun Wobble and animation.mp4`, giving a *second independent amplitude measurement* of the same case the law was derived from. (c) is what lets this capture serve as `BL-266`(a)'s fallback instrument: (a)/(b) alone ask only whether caliber and plane weight enter the law, and **cannot** settle the uniform ~2–4× shortfall our render shows against the reference clip. ⚠ Dead-astern framing is load-bearing: it makes the on-screen roll angle the world roll angle with no projection model (`analysis/gun-wobble-shake/FINDINGS.md`, capture spec there). Confirms or refutes the pure-caliber magnitude law (7e-5 × caliber, measured on one 40-cal clip) and whether plane model/weight enter; a being-hit clip on the same sortie also pins the impact sources' stand-in quantities | `BL-266` |
| `CAP-38` | Beeper and seeker hits ON an aircraft, **with audio** | In the original, fire the beeper (`wep_10`) and the seeker (`wep_11`) at an aircraft and film the hit itself, external/chase, close enough to read the burst on the airframe. Both weapons author `ANIMATION large_fireball` on their aircraft `IMPACT` row and ours plays exactly that on a fused or struck plane; the ground-side look is already signed off, so this clip is only the on-plane half. *Look for:* whether the original shows the large fireball on the plane, something smaller, or nothing beyond the paint, and the per-type impact sound on the same take (`snd_missile_beeper` / `snd_missile_seeker`) | Nothing tracks the outcome; a mismatch with our on-plane burst mints a new `BL` |
| `CAP-29` | Panel-damage semantics | Take controlled damage per part in the original, own aircraft in frame (external/chase), damage display visible if possible. **Reduced 2026-08-15 by the `BL-297` decode**, which answered all three questions out of `crimson.exe` (`docs/org/vehicleDamage.md`, "Damage staging"): (a) effects land at the node the def names, so a nose hit DOES spark wing sites; (b) nothing per-part fires at all while a part's armor absorbs; (c) each entry fires once per downward crossing, so a panel tears once until repaired. **What is still owed is the look:** watch one panel cross its tear threshold and judge whether the flung debris reads as a piece of that panel or as generic flakes, and what visibly changes on the airframe. The other three are now confirmation, worth capturing on the same take if the framing allows but not worth a dedicated sortie | `BL-297` |

### World

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|

### AI flight — an AI aircraft flying itself, external view

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-37` | An AI aircraft flying a patrol/attack loop, unprompted by the player | An AI-controlled aircraft in external/chase view, held long enough to cover a sustained turn, a low-speed moment and a patrol leg's end, with the player's own aircraft in frame where possible for a same-shot comparison. Behavioural and comparative questions only, **no absolute distances or speeds read off this footage** (`docs/verification.md`; a decode is never contested with a footage-derived measurement): does it gain altitude through a sustained turn or hold it; is its turn tighter or wider than the player's in the same airframe; does it hold a speed through manoeuvres or bleed and recover like a lever-driven aircraft; does it wallow at low speed or stay crisp; what does it do at the end of a patrol leg | `docs/plans/PLAN-ai-flight.md` F52 (the AI-side at-the-controls verdict for waves C and E) |

---

## 1 · Actionable now (`PT-nn`)

### C1 · Bloodhawk — the overcast sky, ground to above the deck

```powershell
./RunGame.ps1 --plane=player_bhawk --chapter=C1
```

- `PT-47` `[A/B: both C1 IA1 Fog stills + CAP-12]` **The overcast-match plan's exit verdict**
  (`OriginalScreenshots/C1 IA1 Fog river.png`, `.../C1 IA1 Fog above clouddeck.png`,
  `playtest/CAP-12/`; the plan is `docs/plans/PLAN-overcast-match.md`,
  completed 2026-08-09; it closed `BL-118`, `BL-312`, `BL-303`, `BL-100`, `BL-101`, whose records
  are in that plan and in `git log --grep=<ID>`). Three waves rebuilt this sky — the sprite
  scatter, the fog model and the deck's brightness — and every number in the plan's final tables
  is a still. This is the one sortie that judges it **in motion**, which is the half no box can
  reach: climb from the river up through the whiteout and out above the deck, then do it again
  looking back down.

  Both reference stills are freecam-reproducible if you want the exact frames beside you:
  `--pos=-7323,192,-3829 --direction=-0.997,-0.1,0.070` (river) and
  `--pos=-7323,1192,-3829 --direction=0,0,-1` (above deck). ⚠ Both originals are chase frames at
  a slightly different pitch from ours, so judge *character* against them, not the horizon's
  height in frame (the plan's `SHOT-23` and its C24 tables have the numbers).
  *Look for:*
  - (a) **below the deck, and while climbing** — the ceiling is one mottled sheet that reaches the
    horizon and dies into the fog wall with no sky stripe and no visible rim. The 13-px bright
    strip at the deck's edge that used to appear on a climb should be gone (it is now ~4 px, hidden
    inside the dome wall's own gradient). Climb slowly from 200 m to 900 m watching the horizon:
    nothing should slide, step or brighten as you go;
  - (b) **the whiteout crossing** — entering the band at ~970 m the world should ramp to a total
    whiteout in the core (1032–1062 m) and clear again by ~1124 m, with no snap and no hard edge
    at either end, and no cloud card visibly punching through the pane;
  - (c) **above the deck, the tops and the floor are ONE tone** — the straight-edged wedges the
    deck floor used to cut through the near cards are gone (measured: floor↔card gap +49 → −4).
    Look down and forward: no hard colour cut anywhere along the mesh↔sprite boundary, and nothing
    darker than the fog colour showing between cards;
  - (d) **C1C above its band** (`--chapter=C1C`, climb past 1082 m) — this one is **expected to
    look WORSE than C1**, and judging how much worse is the point. Its `fvol` cards author
    `lighting: true` where C1's do not, so its frame holds three cloud tones at once: placed cloud
    facades 235, deck floor 196, `fvol` cards 164. `BL-327` asks whether that flag is a
    `WorldLight` gate at all; your verdict on how bad it reads is what prioritises it;
  - (e) **density and character against the original** — cloud spacing and size versus `CAP-12`'s
    own climb, at grazing angles along the tops and along the base: no lattice or comb at any
    angle, no visible field edge over the base map, and the sheet's mottling reading as
    multi-scale rather than smooth broad bands (ours is measurably blurrier than the original's —
    high-pass RMS ≈ 0.2 against ≈ 0.9 — so say whether that is visible in motion).
  - (f) **the in-band flicker** (`BL-329`, `PLAN-weather-decompile-match` D32) — hold still a few
    seconds in the whiteout RAMP, not the opaque core (~970–1032 m or ~1062–1124 m; the fully
    white core in between is a flat colour by design and never flickers): the pane should shimmer
    subtly rather than sit dead flat, on a pace of roughly a couple to several seconds per swing.
    The rate is a declared TUNE (`BandFlicker.DefaultRate`), not a decoded figure, so judge
    whether it reads as "clouds breathing" at all — too fast reads as a strobe, too slow reads as
    nothing happening.

  *Blocks:* this is the plan's own exit verdict — a pass confirms it. A fail on (a), (b), (c) or
  (e) is fresh evidence on the closed item's successor, not a reopening: name which check failed
  and mint against the mechanism it belongs to (`A7`/`C25`/`C26` for (a), `CLOUD_COVER` for (b),
  `C23`'s fork for (c), `BL-312`'s scatter or `BL-327`'s far-field half for (e)). (d) has its
  item already: it feeds `BL-327`. (f) feeds `BL-329`.
  *Variations:* `--chapter=C4` for the one deck chapter whose `WorldLight` clamps to 1.0 — its
  deck must look exactly as it did, and its cards must still be *there* above the band.

### C1 · Bloodhawk vs AI — the kill sequence, sound on

```powershell
./RunGame.ps1 --chapter=C1 --plane=player_bhawk --ai=player_fury --ai-attack=9 --volume=1.0 --no-det
```

⚠ `--volume=1.0` is not optional: the default master volume is 0, so a run without it is silent for
reasons that have nothing to do with any of these checks.

- `PT-77` `[A/B: OriginalScreenshots/Videos/Enemy AI Shotdown.mp4]` **A downed AI's wreck flies its
  FROZEN commands.** The wreck flies itself for three seconds after the airburst, until the destroy
  def's `Callback 15` releases it to the anim. What it flies with changed: the commands now freeze at
  the pilot's last values rather than being neutralised, which is what the decode says and which
  measures 344 m downrange against the 143 m it used to travel. That number deliberately moves *away*
  from the ~175 m a reference recording gave, because a footage-derived distance may not contest a
  decode (`docs/org/flightModel.md`); this sortie is the judgement that number cannot supply.
  *Look for:*
  - (a) the three-second fall reads as a wounded aircraft carrying its momentum, not as a hull
    being flung. It is faster and travels further than it did;
  - (b) ⚠ the one way freezing could be wrong: an aircraft killed **mid-turn** keeps its stick
    deflected, so it keeps turning. The recordings show the wreck holding its `x`/`z` heading. Kill
    one in a hard turn and one in level flight and compare;
  - (c) the second, smokier explosion reads as a separate beat downrange rather than landing on top
    of the airburst.
  *Blocks:* the at-the-controls half of `D24` (`docs/plans/PLAN-ai-damage-and-engine-audio.md`). A
  fail on (b) is not a reason to reinstate neutralised commands: it points at whether the original
  zeroes an AI's stick on death, which is undecoded and would be a fresh item.

- `PT-78` `[A/B: OriginalScreenshots/Videos/Enemy AI Shotdown.mp4]` **The bailed pilot hangs level
  for the whole descent.** The chute is placed at 3.0 s and used to freeze at whatever attitude the
  wreck held at that instant, leaving the canopy edge-on and standing on its side. It is now placed
  level, which is what the airframe data authors (`chuteman`/`chutemanparent` are `transform:
  "Initial"`). The golden pins one frame of this; the descent is what it cannot see.
  *Look for:*
  - (a) the canopy is a dome over the man from the moment it appears until the ground, never
    edge-on and never rotating into a wrong pose a few seconds in;
  - (b) the authored 4 s drive and the sway still read as a drift, so it does not look pinned;
  - (c) a fidelity question rather than a defect: **every chute now has identical yaw**, because the
    authored template is identity. Shoot down two or three and see whether a formation of canopies
    all facing one way reads wrong.
  *Blocks:* the at-the-controls half of `D26`. (c) failing is a decode question to reopen, not a
  bug: the identical yaw is what the data says.

- `PT-79` `[Own]` **A shot-down player leaves a wreck, and its hull flies your last stick.**
  Get yourself killed rather than killing. `player-player` is the one destroy def that does not fall
  as an intact hull: it breaks into four separately flown pieces with a cockpit eject.
  ⚠ Decoded for an AI, assumed for a human: the decode shows an AI's commands stop being written
  because the AI think loop is what death skips, but whether the original also stops reading a
  human's stick is not decoded, and both paths share `StepWreckFall`.
  *Variations:* to be shot at rather than shoot, fly into a wing of them —
  `--ai=player_pfighter,player_pfighter,player_pfighter --ai-attack=9`.
  *Look for:*
  - (a) other pilots see a wreck at all, rather than the aircraft vanishing or hanging in the air;
  - (b) all four pieces appear and fly their own arcs, and the pilot ejects;
  - (c) the case the assumption bears on: die while **holding full deflection** and watch whether
    the hull's motion reads plausibly or as a spiral nothing authored.
  *Blocks:* nothing tracks (c) — a fail mints a fresh item against whether the original keeps
  polling a dead player's input.

- `PT-80` `[Own]` **Damage stages read HEALTH only, and armour hides nothing behind it.** The
  original divides health alone at both the def level and the per-part level, armour never entering
  either quotient, and a part whose armour still covers the hit takes no health damage at all, so an
  armoured zone should cross no threshold whatever.
  *Variations:* take the fire rather than give it —
  `--ai=player_pfighter,player_pfighter --ai-attack=9`.
  *Look for:*
  - (a) nothing at all shows on a zone while its armour is still absorbing, the 0.99 spark shim
    included; that shim going quiet on early hits is the most visible change here;
  - (b) a panel tears only once that zone's own health crosses its threshold, not before;
  - (c) `player_damage_trail` starts when the hull gauge reads about 10%, not earlier.
  *Blocks:* the owed flight A/B for the staging scope and pool fixes. ⚠ The gauge dial is a separate
  scale and is *correct* on combined armour+health, so a dial that disagrees with the staging here
  is not a fault.

- `PT-81` `[Own]` **A repair retracts the stage it lifted back over (F5 damage lab).** Staging used
  to latch one way; the original stops an entry's anim and clears its handle on the upward crossing.
  This one needs no AI, so it flies on the bare command:
  ```powershell
  ./RunGame.ps1 --chapter=C1 --plane=player_bhawk
  ```
  *Look for:* open the F5 damage lab, drag a zone down past a threshold to start its stage, then
  repair back above it and watch the stage stop. Un-staging on repair is faithful, not a regression.
  *Look for also:* it must fire ONCE per downward crossing. A stage that re-fires while the fraction
  merely stays below its threshold is a different bug, not this fix working.
  *Blocks:* the owed lab check for the staging lifetime fix.

- `PT-82` `[A/B: OriginalScreenshots/Videos/Bloodhawk Dive Sound.mp4]` **The engine note moves with
  the stick and with the climb, not with the throttle alone.** Two decoded terms landed on the engine
  slot: an unsigned transient on pitch and yaw rate, and a level that falls through a dive and rises
  in a climb. The note was previously a function of the throttle lever and nothing else, so at a
  fixed throttle it was flat whatever the aircraft did. This one needs no AI:
  ```powershell
  ./RunGame.ps1 --chapter=C1 --plane=player_bhawk --volume=1.0 --no-det
  ```
  *Look for:*
  - (a) hold the throttle fixed in level flight and pull, then push. The note rises **both** times.
    That it rises on a push is the surprising half and is what the decode says;
  - (b) a sustained vertical dive at fixed throttle sits about 6% below level, and returns to level
    when you pull out rather than staying down;
  - (c) roll hard at fixed throttle. The note must **not** move at all: the original drops the
    nose-axis rate, so a barrel roll is silent where a pitch input is not.
  *Blocks:* nothing tracks this; it is the last thing owed on the engine note now that the mechanism
  is decoded and pinned (`git log --grep=BL-109`, `--grep=BL-423`). The numbers already match the
  original to within a third of a point, so a fail here is about whether it READS right, not whether
  it matches. ⚠ Do not ask for a re-tune of the coefficients on a listen: they are read from the
  executable, and `docs/verification.md`'s rule that a recording may not contest a decode applies to
  the ear too.
  ⚠ **This is probably what the retired `BL-123` was hearing** (`git log --grep=BL-123`). That item
  A/B'd our dive whine against the original and reported it "close, but could be a bit louder",
  which read as a mix problem. The original has no whine at all, so what the ear was matching in
  that dive was the engine slot's own movement, and these terms are what produces it.

### C1 · two pilots — Dogfight (splitscreen VS)

```powershell
./RunGame.ps1 --vs --players=2 --chapter=C1
```

- `PT-52` `[Own]` **The puffer distance fade in two panes (`BL-339` landed 2026-08-15, plan B11).**
  The fade now runs its bands against every pane's camera and each particle takes the most
  favourable pane's alpha, so a trail near player 2 draws in player 2's pane. What no instrument
  here can judge is the remaining divergence: one alpha per particle for the whole world, so a pane
  can see a puff its own camera would have faded further. A scripted shot cannot set this up —
  there is no per-player placement flag and no scripted fire, so both panes spawn near-coincident.
  *Launch:* `./RunGame.ps1 --fly --players=2 --chapter=C3` (plain 2-pane free flight, two pads or
  pad + keyboard) — the section's Dogfight launch above works too if a target is wanted.
  *Look for:* (a) the reported repro is gone — P2 astern of P1 fires a rocket past him and sees the
  whole trail, not just the stretch beside P1; (b) neither pane shows a puffer popping in or out as
  the OTHER player turns or flies away (the shared-alpha tell); (c) flying through an emitter still
  culls it in the pane that flew through it rather than filling that screen.
  *Blocks:* the fidelity verdict the plan's nearest/union boundary rule asks for
  (`docs/plans/PLAN-splitscreen-polish.md`'s Milestone goal) — per-pane alpha (one MultiMesh per pane) is
  reached for only if (b) visibly fails, and a fail mints its own `BL` item.
  *Variations:* C3 (`--chapter=C3`, the waterfalls' `spew_puffer` is the tightest authored band);
  `--players=4` for the same question with four alphas competing.
  *Also carries B12 (`BL-340` landed 2026-08-15):* the `FBFX_COLOR_FROM_TO` screen wash now paints
  only the panes whose camera is inside the burst's authored 100 m radius, and the same missing
  levers (no per-player placement, no scripted fire) keep it off the scripted path. In the same
  session: put P2 over the ground alone and have him rocket the terrain — P2's pane flashes
  white/violet and P1's, a few hundred metres off, does not; then fly the pair in together and both
  flash. A wash that still paints all panes, or one that paints none, is the failure.

### C1 · two pilots — stunt race (splitscreen starting grid)

```powershell
./RunGame.ps1 --stunt --players=2 --chapter=C1
```

- `PT-45` `[Own]` **The abreast race starting grid** (`docs/PLAN-race-grid.md`, landed 2026-08-08;
  it closed `BL-084`, whose record is in that commit — `git log --grep=BL-084`).
  Two pads (or pad + keyboard); menu path: Stunt → C1 → both press Start. Splitscreen stunt racing is
  our invention — the original had no splitscreen at all — so every call here is a judgement on our
  own remake, with no reference to A/B against.

  **Why this sitting is the only evidence there will ever be.** The grid is selected only when a
  session is an actual race, and a `--det` run is explicitly given the old per-player spawn walk
  instead, so no scripted run, screenshot or golden can exercise this path — by design, since that
  bypass is what keeps every scripted spawn byte-identical. The grid geometry is also not
  photographable: the panes are chase-cam only, so at the default 60 m spacing your neighbour sits
  outside your own frustum. **Read the geometry off the console instead** — every launch logs one
  line per slot, e.g. `spawn [P1 grid slot 1 of 4] pos=(-4974,260,-3771) heading=90° spacing=60m
  lift=81m`, with the anchor's own line above them. On C1 with `--spawn=0` the field lifts 81 m.

  **Both numbers are live config, and settling them is the point of this sitting.** `slotSpacing`
  (default **60 m** between neighbouring slots) and `groundClearance` (default **100 m** of air the
  lowest slot must have under it) are read from `config.json` as `raceGrid.slotSpacing` and
  `raceGrid.groundClearance`, listed by `--dump-config`, and take effect on the next launch with no
  rebuild. Neither is a finding — 60 m is just the figure already in the tree — so dial them between
  launches until the start looks right and record what you landed on.
  *Look for:*
  - (a) **does it read as a starting line** — at the moment of spawn, does the field feel like a
    grid you are lined up on, at 2 panes and at `--players=4`;
  - (b) **spacing at the wingtips** — 60 m: too far apart to feel like a race start, or too close
    for comfort in the first seconds of manoeuvring? Try 30 m and 100 m before deciding;
  - (c) **the uniform lift** — the whole field rises together by whatever its worst slot needs, so
    over broken ground it can look absurd (the field hovering high over a valley) or, if clearance
    is dialled too low, too tight (an outer wingtip in a hillside). Watch an outer slot, not P1;
  - (d) **a felt end-of-grid advantage** — do the outer slots feel meaningfully better or worse than
    the middle for reaching the first Danger Zone? Slots are fixed by player index today; a *felt*
    bias is the trigger to randomise the slot order per race (not to rotate it per rematch);
  - (e) **the anchor still varies** — relaunch a few times without `--spawn=`: the whole grid should
    sit somewhere else each time (the anchor is a random pick from the mission's spawn list), not on
    the same point every launch;
  - (f) **`--pos` still wins** — `--pos=x,y,z` must still place the field where you asked, grid or
    no grid, since the override is resolved beneath the grid rather than beside it.

  *Blocks:* the two config values in (b)/(c) hardening from fallbacks into decisions; the
  slot-rotation call in (d); and `BL-314`, the race countdown, which must not be started until the
  grid it counts down over has been flown. A structural fail — a plane in terrain, a field that is
  not level or not on one heading — mints its own `BL` item.
  *Variations:* `--players=4` for the case (a)/(b)/(d) are really about; `--chapter=C2` for a
  different terrain profile under (c).

### C1B · Bloodhawk, night — sky, clouds, self-lit art

```powershell
./RunGame.ps1 --plane=player_bhawk --chapter=C1B --infinite-ammo
```

- `PT-28` `[A/B: C1B IA1 Bloodhawk tracer and ejection.png]` **Night self-lit art (C9 / `BL-214`
  landed 2026-08-02).** The model `lighting` flag is now honoured, so on a night map the cloud
  sprite cards, water splashes, beacons and effect meshes draw at full brightness while the terrain
  and sea still dim with the mission SUNLIGHT. Fly C1B at night and judge **the clouds
  specifically** — that is the one part with no matched capture of the original. *Look for:*
  - (a) do the cloud cards read as moonlit at the right level, or as blown-out white cut-outs
    against the dark sea;
  - (b) do the gun splashes on the water read like
    `OriginalScreenshots/C1B IA1 Bloodhawk tracer and ejection.png` (they measure the same);
  - (c) does the skydome still meet the terrain in a grey band — the dome deliberately keeps its
    fog even though the data says otherwise, and a hard horizon edge would mean that call is
    wrong.

  *Blocks:* nothing open — `BL-214` is closed; a fail mints a new `BL` item.

- `PT-41` `[Own]` **The per-chapter sky/fog zone in C1B, C2 and C3 (C9 / `BL-277` landed
  2026-08-06).** Those three define `ZONE2` fog but ship no `zone2` dome at all, so they rendered
  the engine's clear colour with a hard horizon cut; they now build `zone1` — sky and fog
  together — and the dome scale is fitted inside the far plane (C1B's zone1 dome is 21.8 km and
  clipped open at the 2.5× anchor). Headless goldens cover the three poses; what they cannot judge
  is whether the chosen sky is the *right* one and how it reads in flight. *Look for:*
  - (a) a real dome in all three, from the deck up to the ceiling and looking straight up — no
    grey wedge, no hard cut, at any altitude or heading;
  - (b) C3's haze reads as daylight grey on a sunlit mission (it used to be night-blue), and C2's
    as the pale sky-blue its `ZONE1` authors;
  - (c) C1B stays fogged above ~1.2 km, where the old `ZONE2` band stopped;
  - (d) the four chapters the rule deliberately leaves alone — C1, C1C, C2B, C4 — look exactly as
    they did.

  *CAP-11 evidence (2026-08-07, `playtest/CAP-11/`; the capture is retired, `BL-110` closed):*
  three of the look-fors already have numbers. (b) is **failing** — at the matched canyon pose
  (`--pos=-3504,710,-3619`, 2329 ft) the original is nearly clear (near slope 36, far hills
  20–60, blue-gradient sky 195) while our C3 renders full `c9c9c9` murk (near slope 130, far
  hills flat 201, sky = fog): the "daylight grey haze" is far too dense and the sky the wrong
  colour. (d) fails for **C2B above the deck** — original dark-blue dome 82.7, ours flat b0b0b0
  fog 176 at 1230–1500 m. Both, plus C5's black sky, are now `BL-303` (shared 9000–10000 m
  fog-band suspect). And C1B's night brightness is judged: terrain at the matched spawn −12%
  (`WorldLight` 0.426 confirmed, `git log --grep=BL-110`), but our zone1 dome is **−26%** vs the
  original's night sky and **no moon renders** where the original shows a large one
  (`t0.5`/`t5` stills) — `WorldBuilder.BuildHorizon` knows how to billboard a moon, so the (a)
  sweep should check whether the built zone1 subtree simply lacks the node.
  *Blocks:* nothing open — `BL-100`'s remaining four chapters were settled by render evidence
  instead (`PLAN-overcast-match` `B12`: C1/C2B/C4/C1C = `zone2`), not by a fresh flight.
  *Variations:* one flight each — repeat with `--chapter=C3`, then `--chapter=C2`; (d)'s four
  untouched chapters need only a glance in each.

- `PT-53` `[Own]` **Graze feel now that a graze bounces (`docs/plans/PLAN-ai-flight.md` `C25`, landed
  2026-08-15, closing `BL-172`).** A survivable scrape now rebounds along the contact normal off the
  shipped `bounce_factor` 0.6, where before it only slid. Three surfaces, at speed, in C1 or C5:
  - (a) **a shallow belly skim over flat ground** — the plane should come off the ground and fly on,
    not skip like a stone or bury itself. This is the case the suite measures at `e = 0.56`;
  - (b) **an oblique scrape along a building wall or a cliff face** — the rebound there is
    horizontal, so the altimeter should barely move; what to judge is whether being pushed off the
    wall reads as a graze rather than as a bounce off a trampoline;
  - (c) **a building corner** — `CAP-14` shows the original surviving a 144.5 mph graze on a C5
    skyscraper and dying at 144.2 against another, so survival there is geometry, not speed.
  ⚠ The three graze constants (`GrazeKick`, `GrazeFriction`, `GrazeStopSpeed`, `BL-271`) were tuned
  against the OLD no-bounce slide and were not re-tuned when the impulse landed. If a graze feels
  wrong, they are the first suspects — not `bounce_factor`, which is authored data.
  *Blocks:* `BL-271`'s re-tune, `BL-381`'s multi-tick scrape (a fail on (b) is evidence for it), and
  `docs/plans/PLAN-ai-flight.md` F52's player-side graze check (C25) — no separate PT item repeats it.

### AI flight — external view, own build (F52 AI arm)

- `PT-54` `[Own]` **AI plant A/B against the old plant (`docs/plans/PLAN-ai-flight.md` C21–C24, F52 AI
  arm).** Fly the new AI force path, then relaunch flipping AI aircraft back onto the player plant
  with `--no-ai-plant` and fly the same engagement again — the switch exists for exactly this
  comparison (`docs/cli.md` `--no-ai-plant`) and is temporary, removed once this verdict lands.
  ```powershell
  ./RunGame.ps1 --stage=empty --plane=player_bhawk --ai=player_fury,player_avenger
  ./RunGame.ps1 --stage=empty --plane=player_bhawk --ai=player_fury,player_avenger --no-ai-plant
  ```
  *Look for:* the divergences wave C/E ported onto the AI plant — nose-aligned airflow instead of
  weathervane centring, the AI's own speed floor, ground blow, and the authority ramp/reverse
  factor now shared with the player path (C24) — against a plant with none of them wired in. Does
  the new plant read as a distinct AI flight character, or as indistinguishable from the old one?
  *Blocks:* F52's AI-side verdict.

- `PT-55` `[Own]` **AI plant under `--ai-attack`, free flight (F52 AI arm).**
  ```powershell
  ./RunGame.ps1 --stage=empty --plane=player_bhawk --ai=player_fury --ai-attack=9
  ```
  *Look for:* the same behavioural and comparative questions `CAP-37` asks of the original — does
  the AI gain altitude through a sustained turn or hold it, is its turn tighter or wider than the
  player's own in the same airframe, does it hold speed through manoeuvres or bleed and recover like
  a lever-driven aircraft, does it wallow at low speed or stay crisp — now under the hard
  maneuvering of pursuing and firing on a live target.
  *Blocks:* F52's AI-side verdict.

- `PT-56` `[Own]` **AI plant in a chapter mission with patrol nets running (F52 AI arm).**
  ```powershell
  ./RunGame.ps1 --chapter=C1 --plane=player_bhawk --ai=player_fury:M4ReinfAce --ai-attack=9
  ```
  *Look for:* the same questions as `PT-55`, this time along a real net in a real mission context —
  what the AI does at a patrol leg's end, and whether the plant holds up once `AiModeMachine` is
  actually cycling patrol/pursue/lay off rather than idling in an empty stage.
  *Variations:* pair with `--debug-ainets=M4ReinfAce` to watch the drawn route alongside the flight.
  *Blocks:* F52's AI-side verdict.

- `PT-58` `[Own]` **Crash avoidance over a ridge that sits above the net's authored altitude.**
  ```powershell
  ./RunGame.ps1 --chapter=C1 --plane=player_bhawk --ai=player_fury:M4ReinfAce --ai-attack=9 --debug-markers
  ```
  *Look for:* fly out over the high ground east of the spawn with F13 up and watch a netted enemy
  cross ground that stands above the net's authored 400 m. It should pitch up and climb out on its
  own rather than fly into the slope, and it should rejoin the graph afterwards rather than hold the
  climb. The mode transitions print as `patrol -> avoid crash` and back, naming what the ray struck.
  *Look for also:* the climb-out is a 45° break up and to the right of the aircraft's own track, not
  a vertical pull-up, and the state releases as soon as the line is clear rather than dwelling.
  *Blocks:* the cockpit half of crash avoidance; the mechanism itself is decoded and measured
  (`docs/org/aiPilot.md`, "Crash avoidance is a STATE, not an altitude rule").

### Autogyro, Balmoral, Fury — low-speed authority ramp (F52 player arm)

- `PT-57` `[Own]` **Low-speed handling across `BL-330`'s authority-ramp extremes
  (`docs/plans/PLAN-ai-flight.md` C24, F52 player arm, judged against `BL-330`'s existing corroboration).**
  The ramp fades roll and pitch to nothing at 10 mph and back to full at 50; `BL-330`'s own decode
  picked out the two airframes furthest apart on it — the autogyro (18.5 mph stall, ~21% of
  authority left there) and the Balmoral (45.5 mph stall, ~89% left) — plus a mid-pack airframe for
  the common case (Fury, in the 52–57 mph band nine of the eleven share).
  ```powershell
  ./RunGame.ps1 --plane=player_autogyro --chapter=C1
  ./RunGame.ps1 --plane=player_balmoral --chapter=C1
  ./RunGame.ps1 --plane=player_fury --chapter=C1
  ```
  *Look for:* controls going progressively mushy on the approach to stall and gone outright at
  10 mph, roll and pitch only (yaw is unaffected — C21); the autogyro's fade should be felt hard and
  early relative to its own stall, the Balmoral barely at all, and the Fury somewhere between.
  *Blocks:* F52's player-side verdict.

### C1 · two pilots — the victim-routed screen wash (`PLAN-ordnance-types` F22)

```powershell
./RunGame.ps1 --coop --players=2 --chapter=C1 --debug-wash=2
```

`--rocket=<wep_id>` (used here and below) swaps every pylon to the named type. All 11 stock
loadouts fit HE `wep_06`, so a flag is what pins a type here rather than the menu's own Ammo
Selection screen: the flag beats a menu-chosen fit deliberately, so a row that names a type gets it
whatever was clicked on the way in. The rocket trigger is **F**, one round per pull.

⚠ **Every figure quoted in this section and the ones below is a decoded constant or an authored
value.** A clip that disagrees with one is evidence about our implementation, never a correction to
the constant: a decode is not contested with a measurement read off a running picture
(`docs/verification.md` DET-12).

- `PT-74` `[Own]` **The wash is addressed to the viewer who was hit, and blends
  (`PLAN-ordnance-types` `D13`, Decision 2).** The original holds one wash state for the whole
  machine, which would blind viewer 1 when viewer 3 is flashed; ours routes by the victim's own pane
  and composites over the existing proximity ramp instead of replacing it. `--debug-wash=N` fires
  two overlapping scripted washes at viewer N (red at weight 1 for 5 s on the first frame, then
  white at weight 0.5 two seconds in).
  *Look for:*
  - (a) **routing**: pane 2 washes red, then pink as the white lands on it, and pane 1 stays clean
    throughout;
  - (b) **the ramp is untouched**: with `--rocket=wep_06` and no `--debug-wash`, an HE burst still
    washes by camera proximity exactly as it did, both panes flashing when both are near and only
    the near one when they are apart. Take that baseline **before** judging anything else in this
    section;
  - (c) **a real hit routes the same way**: with `--rocket=wep_08`, a pilot caught in the **other**
    pilot's sonic burst washes their own pane alone. (A pilot's own burst never washes them: the
    gather excludes the round's owner, `FUN_005aca30`.)

  *Blocks:* nothing tracks the outcome (`PLAN-ordnance-types` is complete; (a)/(b) passed at the
  controls and only (c) is still owed): a fail on routing is a `D13` regression and mints a new
  `BL`.
  *Variations:* `--debug-wash=3` in a two-pane session, which answers to no pane and must paint
  nothing at all.

### C1 · four pilots — the four-viewer ordnance pass (`PLAN-ordnance-types` F22)

```powershell
./RunGame.ps1 --coop --players=4 --chapter=C1 --rocket=wep_08 --infinite-ammo
```

Four viewers is the case the wash channel was designed against: the original routes its wash
through a single global and that does not survive four panes. ⚠ `BL-389` already reports that the
splitscreen weapon mix wants a retune, with rockets too quiet against guns and worst with four guns
firing at once. **A mix problem is not a behaviour problem**: judge what happens, and file loudness
against `BL-389` rather than against the wash routing.

- `PT-75` `[Own]` **The disabling types with four viewers on one team (`PLAN-ordnance-types` F22,
  `D13`, `D15`, `D18`).**
  *Look for:*
  - (a) **two viewers washed in the same second** carry their own wash each, with the other two
    panes clean, and neither washed pane is brighter or shorter for having a neighbour;
  - (b) **a smoke screen laid by one pilot** stuns and washes only the pilots actually behind it,
    and the layer's own pane stays clear;
  - (c) **`--rocket=wep_12`**: a choked pilot's own pane shows nothing at all (the choker has no
    wash), and only their thrust goes;
  - (d) **the blast still reads**: HE bursts near two panes at once still stage destructibles the
    way `PT-65` judged them with one viewer;
  - (e) frame cost holds up with four panes and a dense burst (`--debug-fps`), since every splash
    candidate now costs a cover ray.

  *Blocks:* nothing tracks the outcome (`PLAN-ordnance-types` is complete): a fail on routing is a
  `D13` regression and mints a new `BL`; a fail on loudness is `BL-389`.

- `PT-76` `[Own]` **The same pass under Dogfight rules, four viewers hostile
  (`PLAN-ordnance-types` F22).** `--coop` puts every human on one team, so the beeper's hostility
  gate and the AI-side effects never fire between players there. Dogfight makes them mutually
  hostile, which is the only way to judge the tag gate and a human-on-human paint.
  *Launch:* `./RunGame.ps1 --vs --players=4 --chapter=C1 --rocket=wep_10 --infinite-ammo`
  *Look for:*
  - (a) a beeper into a hostile human paints them and deals nothing, and a second beeper into an
    already-painted pilot neither re-tags nor refreshes the first;
  - (b) stepped to `wep_11`, a seeker follows the painted pilot across all four panes and its trail
    draws in every pane it passes;
  - (c) `wep_08` and `wep_09` between hostile humans wash the struck pilot's pane alone, with the
    facing rule still holding for the flash;
  - (d) the match keeps scoring normally: none of the no-damage types registers a hit or a kill.

  *Blocks:* nothing tracks the outcome (`PLAN-ordnance-types` is complete): a fail mints a new
  `BL`.
  *Variations:* `--players=2` and `--players=3` for the intermediate pane counts, which is where a
  routing off-by-one would show.

---

## Everything else

Everything blocked on an unlanded fix is tracked in [`backlog.md`](backlog.md) with its own
`*Playtest after fix:*` line. Do not re-add those here; the entry brings its own test when the
fix lands.
