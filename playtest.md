# Playtest checklist

Everything that needs a human at the controls (or the original game open for A/B), consolidated.
**This file holds only what is actionable *today*.** Anything whose test is blocked on an unlanded
fix lives on its `backlog.md` entry as a `*Playtest after fix:*` line instead — so an empty section
here means the work is queued, not forgotten. Deep evidence and traps live in
[`backlog.md`](backlog.md); the two are kept in step.

**Every item carries a stable ID** — `CAP-nn` for an owed capture, `PT-nn` for something to fly.
Cite them from `backlog.md` and in conversation the way `BL-nnn` is cited. IDs are permanent: when
an item closes its ID retires with it and is never reused, so numbering gaps are expected.

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
unusable.** This already cost two takes. The capture spec and the clip-validity rules are in
`analysis/video-flight-calibration/FINDINGS.md`.

### Flight model — cockpit gauges in frame, head turn off

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-01` | Sustained banked max-pull turn | A held maximum-G banked turn, gauges readable throughout — the speed bleed is the signal | `BL-092` |
| `CAP-02` | Low pass along a canyon wall | A close pass down a canyon face; the only possible source of ground-blow magnitude | `BL-095` |
| `CAP-03` | Level top speed at four altitudes | Full-throttle level runs held to equilibrium at 5500 / 6000 / 6500 / 6800 ft | `BL-094` |
| `CAP-04` | Moderate-deflection pitch trace | A ~45° pull from level, timed frame by frame like the existing sustained-rate clips | `BL-147` |
| `CAP-05` | Stall & knife-edge recovery | A full stall entry and recovery, plus a sustained knife-edge | `BL-115`, `BL-124` |
| `CAP-06` | Graded stall-warning reference | The airspeed dial's warning blink **through the approach to stall**, not only after it | `BL-148` |

### HUD — ammo gauge in frame

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-18` | Weapon-switch gauge arrow | Cycle gun groups (G) and hardpoints several times each way with the ammo gauge readable — does the pointer **sweep** to the new slot or snap instantly, and if it sweeps, over roughly how long? | `BL-184` |

### Camera

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-07` | Numpad view stills | One still per numpad key held — **including 0**, which the original binds and we do not — framed wide enough to read the angle and height | `BL-150` |
| `CAP-08` | Numpad key-combination stills | Two or more numpad keys held together, one still per combination tried | `BL-150` |

*(The numpad +/− distance trim needs no capture — you already have video of it.)*

### Audio

| ID | Capture | What must be audible | Unblocks |
|---|---|---|---|
| `CAP-09` | Doppler pass-by | A fast close pass by the C1 waterfall (a fixed emitter — isolates listener motion, the cleanest Doppler signature); the moving police car is the harder emitter-motion case | `BL-160`, scopes `BL-079` |
| `CAP-10` | Engine note through a dive | A full-throttle dive to pull-out, with enough HUD and stick visible to correlate the pitch change against **climb rate and elevator input** — the two you identified as driving it | `BL-109` |

### Weather & visuals

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-11` | `SunIncidence` per chapter | World brightness framed like `OriginalScreenshots/C1 IA1 Zone1 environment Spawn3.png`, for every chapter, plus a few seconds of video each. **Priority pair: a C1B night mission and a C1C bright-day mission** — the two extremes the self-scaling model predicts (0.43 / clamp 1.0) and is riskiest on | `BL-110` |
| `CAP-12` | Cloud-deck pass-through | A climb from below the cloud band, through it, and out above, **altimeter visible throughout** so puff density can be correlated against altitude | `BL-118` |
| `CAP-13` | Lens flare | The sun at several screen positions — centred, near-edge, and partially occluded by terrain — to read the streak count, colour and fade | `BL-165` |

### Damage & collision

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-14` | Graze vs crash | At least one shallow graze that survives and one outright crash; a building-corner clip if you can get one | `BL-172` |
| `CAP-15` | Visible damage stages | A graze sequence slow enough to watch a part cross each damage threshold and reach ≤10% HP **without dying** — that band is where the smoke/fire trail should appear | `BL-174` |
| `CAP-16` | Crash puffs | A full crash sequence, close enough to judge sparks, fireball cluster, black smokeball, dirt burst and debris arcs | `BL-122` |

### World

| ID | Capture | What must be in frame | Unblocks |
|---|---|---|---|
| `CAP-17` | Map-edge continuation | Fly to a map edge over a **genuinely asymmetric** border tile — a coastline bend, an isolated building or rock; open ocean cannot discriminate mirror from repeat — then continue straight for **4+ tile crossings, filming continuously**. The silhouette flips on a mirror and repeats identically on a plain repeat, and the crossing count answers how far out the world continues | `BL-105` |

---

## 1 · Actionable now (`PT-nn`)

- `PT-01` **E37 pipper — at-the-controls only; the design question is already settled.** There is no
  gun convergence to validate: the fixed reticle is airframe-locked with every weapon aimed at that
  centre, and the floating one exists only because inertial forces make shots lag through a turn —
  velocity inheritance, already modelled. `GunConvergenceDist` 250 m is purely the distance the
  pipper is *drawn* at. *Look for:* it sits where the rounds actually land in a hard turn.
  `./RunGame.ps1 --plane=player_bhawk --chapter=C1 --infinite-ammo --fire`. *Blocks:* retiring the
  TUNE.

- `PT-02` **Cloud puffs / cloud deck.** Puff opacity and density, and the in-cloud pass-through
  look. Two known symptoms with the mechanism already traced (`BL-118`): the deck reads with dense
  puffs while you fly through it, and in **C1/IA1** puffs surround the plane at *all* altitudes
  rather than in a band. The vertical extent is a code fix, not a TUNE — but *density* has no data
  source and stays a judgement call. *Look for:* how dense the field should read once it is confined
  to the real band. *Blocks:* cloud sign-off.

- `PT-03` **Wing-light `FlashDuration`** (0.08 s blink). The resource check is done and settled the
  period (1.5 s, data-exact, already shipped) but **not** the on-duration — the data's own 0.0001 s
  is a single-tick pulse, imperceptible at any real frame rate, so 0.08 s is a deliberate widening
  with no better source. *Look for:* whether the wingtip blink reads at the right visibility.

- `PT-04` **Data-driven crash.** `WreckMomentum` 0.4, the piece tumble rate, the debris-arc scale,
  overall crash intensity (fireball + cluster + debris fire are additive — judge the whole), and the
  `snd_exp_ground_a` mix. A/B against branch `bespoke-crash-animation`. *Blocks:* crash sign-off.

- `PT-05` **Surface-class impacts (`BL-041` landed).** The area-quorum vote is measured good
  (census + scripted impact runs: water → splash, `nycity` → building hit, the wooden dock → plain
  ricochet), but nobody has *flown* it. *Look for:* gunfire into a C2 building gives a ricochet,
  not a splash; gunfire into water gives the splash; no building sounds wet.
  `./RunGame.ps1 --plane=player_bhawk --chapter=C2 --fire --infinite-ammo`. ⚠ Do not judge by the
  **C** collider overlay until `BL-042` lands — its wireframes draw offset from the geometry, so
  read the impact effects and sounds, not the colours.

- `PT-06` **Damage-stage smoke/fire in flight (`BL-021` landed 2026-07-30).** Guns-only into a
  tower, HP falling — smoke should render on the object at the 60 % stage and fire smoke at the
  30 % stage, not just the final blast. Known limit: each stage shows one brief burst rather than
  the original's intermittent sputter (`BL-199`). *Look for:* the stage effects appearing at the
  right HP, anchored on the object. `./RunGame.ps1 --plane=player_pfighter --chapter=C1 --fire`.

---

## Everything else

Blocked on an unlanded fix, and tracked in [`backlog.md`](backlog.md) with its own
`*Playtest after fix:*` line — the weapons re-tests (`BL-011`–`BL-028`), the inspect-tool
follow-ups (`BL-042`–`BL-046`), the danger-zone gates (`BL-088`), the numpad camera rebuild
(`BL-150`), graze pushback (`BL-172`) and the C3 spiderweb trigger (`BL-183`). Do not re-add them
here; the entry brings its own test when the fix lands.
