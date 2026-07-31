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
| `CAP-15` | Visible damage stages | A graze sequence slow enough to watch a part cross each damage threshold and reach ≤10% HP **without dying** — the mechanism is confirmed working (`docs/HISTORY.md` 2026-07-31); this capture judges whether the panel-flip/smoke-trail look and timing feel right | `BL-121` |
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

- `PT-13` **Damage-stage smoke/fire re-test (A1 / `BL-199` landed 2026-07-31).** Hold a
  destructible in its stages with gunfire: black smoke should sputter at ≤60 % HP, black smoke +
  climbing fire at ≤30 %, both anchored on the object, for as long as the stage holds (past 32 s),
  ending at death or reset. *Look for:* (a) the sputter reads as intermittent thickness, per the
  authored 50 %/0.1 s dice; (b) **size** — the authored puffs are 0.6–1 m growing ×3, which reads
  small from flight range; **tune it yourself** via `config.json`'s `puffer` block
  (`sustainSizeScale` is the damage-stage smoke; `burstSizeScale`/`trailSizeScale` cover the other
  spawn paths — `--dump-config` writes the template), and report the value that reads right.
  `./RunGame.ps1 --plane=player_pfighter --chapter=C1 --fire --infinite-ammo` (the water tower and
  the `m_build` hangars by the airfield are 60 HP two-stagers). *Blocks:* closing PLAN-m3-polish-3
  A1's cockpit half.

- `PT-14` **C2 blue-water re-test (A3 / `BL-204` landed 2026-07-31).** Fly the C2 coastline and
  fire on both water looks — the open turquoise water and the near-shore blue water that
  previously took no splash. *Look for:* every visible water surface answers with the splash +
  sound, with no readable difference between the two looks; the **C** collider overlay agrees
  (turquoise wireframe over both). The root cause was not a texture-name gap (`BL-041`'s patterns
  already covered every water texture C2 uses) but a mesh-granularity one: a coastal tile is
  mostly beach/cliff by area, so the whole-mesh area-quorum vote gave the real water polygons on it
  to `default` regardless of size — measured at 7.9% of C2's classified water area stranded this
  way, up to 86.7% for the same effect on `buildings` in C4. `SceneBuilder.CollidersForMesh` now
  builds one collider per surface class actually present in a mesh instead of one for the whole
  mesh, so there is nothing left to vote on.
  `./RunGame.ps1 --plane=player_bhawk --chapter=C2 --fire --infinite-ammo`. *Blocks:* closing
  PLAN-m3-polish-3 A3.

- `PT-15` **C3 spiderweb fly-through re-test (B11 / `BL-206` landed 2026-07-31).** Fly full speed
  dead-centre into the tikicave web (around x −4470, y 130, z −4839; approach from the west/−X
  side). *Look for:* the web starts its 0.7 s fade on approach and the plane passes through it
  unharmed — no crash, no damage, matching `OriginalScreenshots/Videos/C3 Spiderweb.mp4`. The fix
  honours the gamez `intersect_surface` flag install-wide, so also worth a feel pass: wreck debris
  (e.g. the kkgate pieces), spinning props and effect geometry no longer collide anywhere — but
  terrain, water and buildings are unchanged. `./RunGame.ps1 --chapter=C3 --plane=player_bhawk`.
  *Blocks:* closing PLAN-m3-polish-3 B11.

- `PT-16` **kkgate debris fade re-test (B12 / `BL-207` landed 2026-07-31).** Kill the propane tank
  and watch the gate pieces through their ride. *Look for:* the 12 wreck pieces visibly turn
  transparent over their authored 3–6 s fades while still tumbling — mid-fade you should see the
  deck/water through the wood, not opaque-then-gone — and the gate is flyable right behind the
  blast (the pieces build no colliders since B11). The fix: the opaque world shader had no runtime
  alpha path, so the authored fade was a silent no-op; a fading piece now swaps to a translucent
  twin material for the fade's duration. `./RunGame.ps1 --plane=player_pfighter --chapter=C2 --fire`.
  *Blocks:* closing PLAN-m3-polish-3 B12.

- `PT-17` **Rocket explosion A/B against the original (D31 / `BL-016` landed 2026-07-31).** Fire
  each rocket type at open ground and compare against the original, one type at a time:
  `--rocket=wep_05` (ARMOR), the stock `wep_06` (BOOM), `--rocket=wep_08` (SONIC). *Look for:* a
  per-type ring now expanding from the impact — yellow-green cracks for AP, a violet-fringed white
  disc plus a second larger ring above it for HE, a stack of five pale-cyan rings rising for SONIC —
  plus the smoke-trail columns that never had a host node before. The rings' scale/fade timings are
  the data's and were not retuned, so **the remaining "faster than the original" half of `BL-016`
  is the open question**: if the burst still reads too fast after this, that is a new finding, not
  this fix failing. `./RunGame.ps1 --plane=player_bhawk --chapter=C1 --fire-rockets`.
  *Blocks:* closing PLAN-m3-polish-3 D31.

---

## Everything else

Blocked on an unlanded fix, and tracked in [`backlog.md`](backlog.md) with its own
`*Playtest after fix:*` line — the weapons re-tests (`BL-017`–`BL-028`), the inspect-tool
follow-ups (`BL-042`–`BL-046`), the danger-zone gates (`BL-088`), the numpad camera rebuild
(`BL-150`), graze pushback (`BL-172`) and the whole 2026-07-31 pass (`PT-05`–`PT-12`, retired —
their re-tests ride `BL-203`–`BL-211`, scheduled in `docs/PLAN-m3-polish-3.md`; `BL-199`/`BL-204`/
`BL-206`/`BL-207`/`BL-016` landed and came back as `PT-13`–`PT-17`). Do not
re-add them here; the entry brings its own test when the fix lands.
