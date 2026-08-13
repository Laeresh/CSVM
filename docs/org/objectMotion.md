# The `OBJECT_MOTION` rigid body, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), 2026-08-10/13, over the course of
[`PLAN-object-motion-decode.md`](../PLAN-object-motion-decode.md). Every claim below names the
function it came from, and every install-wide count was re-derived from `extracted/` rather than
carried forward.

Everything here is a description of *behaviour and constants*. No decompiler output is reproduced;
the addresses are given so any claim can be re-checked at source.

**Where the other halves live.** The authored side — the `OBJECT_MOTION` key list, the compiled
event schema, the three `gravity` booleans as they reach us — is
[`formats/anim-definitions.md`](../formats/anim-definitions.md), and the destructible's-eye view of
what a kill throws is [`formats/destructibles.md`](../formats/destructibles.md). Our implementation
is `CSVM/src/Mech3/Anim/MotionRuntime.cs`, whose entry in
[`architecture.md`](../architecture.md) carries the plumbing and the traps. The sequence clock a
motion's duration feeds is [`org/sequences.md`](sequences.md). This page is the original's runtime:
what the engine does with those keys, per frame.

⚠ **This page is a decode, not a proposal.** Where it disagrees with a footage measurement, the
decode wins and the disagreement is a note (`docs/verification.md` **DET-12**, and this project's
standing rule on video evidence). Every claim below is **decoded from the executable** unless it
says otherwise; the two **measured-off-footage** numbers on this page are labelled where they
appear, and both are retired findings rather than live ones. Where CSVM deliberately differs, that
is listed at the bottom rather than hidden.

⚠ **Scope.** This is the `OBJECT_MOTION` event kind alone. `OBJECT_MOTION_FROM_TO` and
`OBJECT_MOTION_SI_SCRIPT` share a `RUN_TIME` field name and nothing else — different events,
different update paths, none of this applies to them.

## Function map

| Address | Role |
|---|---|
| `FUN_004e8fa0` | The per-frame `OBJECT_MOTION` update — every behaviour on this page below the parser |
| `FUN_00508590` | The event parser: one token-block per authored key, each setting a bit in the event's flag word at `+0xc` |
| `FUN_004e9e30` | The **default** contact tier — the ground-column test, and the surface pick |
| `FUN_004c76e0` | The terrain-grid column query `FUN_004e9e30` calls: floors `(x, z)` into the world database's cell and returns that cell's surface records |
| `FUN_004c8ec0` | The `DO_INTERSECTIONS` contact tier — a full geometry sweep against the same world database |
| `FUN_004cf200` | Hands the column query the flying node's **origin** — the reason a landing rests the origin on the polygon, with no bounding-box term anywhere in the chain |
| `FUN_0055bbc0` / `FUN_0055d5c0` | Fill a struck surface record's height: walk the struck mesh's polygons, return the polygon's height at `(x, z)` |
| `FUN_004ccf50` / `FUN_004ccf00` | The self-hit guard: clear the flying piece's own `intersect_surface` bit around the query, then restore it |
| `FUN_0053c6c0` | The sincos the launch azimuth is passed to — the **only** trigonometry on the launch path |
| `FUN_004d25c0` / `FUN_004d1ba0` | Accumulate the tumble onto the node's own euler angles (`node+0x18..0x20`) |

The parser emits `OBJECT_MOTION: NO_ALTITUDE fall lacks RUN_TIME` (`0063161c`) and
`OBJECT_MOTION: GRAVITY syntax error`, which is what names the dependency between the landing test
and the termination model from the parser's own side.

## The flag word at `motion+0xc`

Every bit is the `OR` `FUN_00508590` executes on recognising the token, at the address given. The
full table with its corroboration is in `analysis/object-motion-flags/FINDINGS.md`.

| Bit | Token | Parser | What the update does with it |
|---|---|---|---|
| `0x1` | `GRAVITY` | `0050868b` | gates the gravity fold **and the whole contact block** |
| `0x2` | `IMPACT_FORCE` | `00508d03` | adds the parent object's velocity into the launch (not built — `BL-343`) |
| `0x4` | `TRANSLATION` | `00508d27` | the vector launch form |
| `0x8` / `0x10` | `TRANSLATION_RANGE_MIN` / `_MAX` | `005095de` / `00509df0` | the polar launch form: draws the four ranges and builds the direction |
| `0x20` | `XYZ_ROTATION` | `0050a60f` | steady spin integration |
| `0x40` / `0x80` | `FORWARD_ROTATION DISTANCE` / `TIME` | `0050b309` / `0050b34b` | the tumble, in two parameterisations |
| `0x100` | `SCALE` | `0050b7ac` | scale ramp, clamped at 0.001 |
| `0x200` | `MORPH` | `0050c21a` | writes `node+0x3c → +0x24`, clamped to 1 |
| `0x400` | `RUN_TIME` | `0050ca4e` | selects ceiling semantics over watchdog semantics |
| `0x800` | `BOUNCE_SEQUENCE` | `0050c678` | resolves the branch name against the def's table |
| `0x1000` | `BOUNCE_SOUND` | `0050c732` | volume scaled by impact speed / `FULL_VOLUME_VELOCITY` |
| `0x2000` | `GRAVITY COMPLEX` | `0050899c` | picks the transformed gravity fold, and widens the landing test |
| `0x4000` | `GRAVITY NO_ALTITUDE` | `00508bf8` | the opt-out from the landing test |
| `0x8000` | `GRAVITY DO_INTERSECTIONS` | `00508c41` | upgrades the landing test to the geometry sweep |

**The `GRAVITY` block parses five tokens and only three of them survive compilation.** `DEFAULT` and
`LOCAL <number>` are *value selectors* — both end with one float in the gravity slot and neither sets
a bit — so they are indistinguishable once compiled. The three booleans the extractor ships are
exactly the three that exist; there is nothing here for it to be taught. `COMPLEX` is the one token
that both sets a bit and may carry the value.

⚠ **The parser zeroes the whole 0x14c-byte event struct before parsing** (`REP STOSD` at
`005085e0`). That is not housekeeping: it is what makes an unfilled cache read as zero rather than
as garbage, and it is the whole mechanism behind the vector form's missing tumble, below.

## The event's live slots

Enough of the layout to follow the rest of the page:

| Offset | What |
|---|---|
| `+0xc` | the flag word above |
| `+0x18` | the authored gravity value |
| `+0x40`…`+0x48` | authored launch velocity (vector form) |
| `+0x4c`…`+0x54` | authored `delta` |
| `+0x58`…`+0x60` | **live** velocity |
| `+0x64`…`+0x6c` | **live** acceleration |
| `+0x70` / `+0x74` / `+0x78` | the launch **direction cache** — x, y, z |
| `+0x80` / `+0x84` | tumble `delta`, and the **live** tumble rate |
| `+0x148` | `RUN_TIME`, or — with no `RUN_TIME` authored — the watchdog accumulator reusing the same slot |

## The launch direction, and why it is not unit length

`TRANSLATION_RANGE` (flags `0x8`/`0x10`) draws an azimuth, an elevation, a speed and a `delta` from
their authored ranges, then builds a direction that is **linear in the elevation, not spherical**:

- `dirY` is `elevation × 0.011111111` — `1/90` written out, so ±90° gives ±1.
- The horizontal magnitude is the **L1 remainder**, `h = 1 − |elevation|/90`, and the azimuth's
  cosine and sine are scaled by it.
- Only the **azimuth** is converted deg→rad (`× 0.017453292`) and passed to the sincos at
  `FUN_0053c6c0`. The elevation never touches trigonometry.

**The resulting direction is deliberately NOT unit length.** Its magnitude is 1 at 0° and at ±90°
and dips to **0.707 at 45°**; across a 60–70° band it runs **0.745–0.81**. The launch velocity is
that vector times the drawn speed, so the elevation reading directly scales how fast a piece leaves.
The direction is cached at `+0x70`/`+0x74`/`+0x78` and read again by the tumble.

The vector `TRANSLATION` form (flag `0x4`) copies the authored velocity and `delta` into the live
slots unchanged. It is unaffected by any of the above — **and it never writes the direction cache**.

## `delta` is an acceleration

The authored `delta` is multiplied by the same launch direction and stored **straight into the
acceleration slot** (`+0x4c`…`+0x54` → `+0x64`…`+0x6c`). There is no division by `RUN_TIME` anywhere
on that path: `delta` is m/s², not a total speed change spread over the flight. Gravity is then
folded into the acceleration's Y **once**, at creation, not per frame.

Install-wide, `delta` is non-zero on **233 of the 1,226** `translation_range` events and **92 of the
757** vector ones, so most of the data cannot tell the two readings apart — the ones that can are
worth checking directly.

## Gravity, in two forms

The fold happens once, and which form it takes is `COMPLEX`'s (bit `0x2000`) only job on the
acceleration:

- **Plain** (`0x1` set, `0x2000` clear): the authored value is added to the acceleration's Y **in
  the body's own parent frame**.
- **`COMPLEX`** (`0x2000` set): the scalar add is skipped, and the world-down vector
  `(0, value, 0)` is run through the node's matrix into that frame instead — the identical machinery
  the `IMPACT_FORCE` branch uses to bring a parent's world velocity in.

The two are arithmetically identical under a world-aligned parent, which is exactly why the install
authors `COMPLEX` on aircraft wreckage and on nothing else: only wreckage hangs off a frame carrying
whatever attitude the aircraft died in. ⚠ **`COMPLEX` does not remove gravity.** Reading the skipped
scalar add as "no gravity" floats a quarter of the install's ballistic bodies away; the non-constant
path is forty lines below in the same function.

`IMPACT_FORCE` is a strict subset of `COMPLEX` install-wide (182 events, all `complex: true`), which
makes unreachable the one combination where the original would fold gravity **twice** — the scalar
add followed by the transformed one. It is in the binary; no data reaches it.

## Contact is the default, in two tiers

The contact block sits behind `GRAVITY` (`0x1`) and branches
`!DO_INTERSECTIONS → !NO_ALTITUDE → column`:

- **The column tier** (`FUN_004e9e30`, the default) queries a **terrain-grid column** at the body's
  next position — the world database, the body's `(x, z)` and its `y + stepY`, handed to
  `FUN_004c76e0`. That query floors `(x, z)` into the database's cell and walks that cell's nodes,
  admitting only those whose flags carry
  `altitude_surface` **and** `intersect_surface`. It returns the cell's surface records — `0x2c`
  bytes each, height at `+0x14`.
- **The sweep tier** (`FUN_004c8ec0`, `DO_INTERSECTIONS`) is a full geometry sweep against the same
  world database, and it runs **every** frame regardless of step direction.
- **`NO_ALTITUDE` is the opt-out**, and it vetoes the **column only**. It is authored on `gunshell`
  and nothing else — 8 events, one per chapter.

**How the column picks its surface, exactly.** It takes the *first* record unconditionally, whatever
its height, then keeps whichever candidate's height is nearest the body's y in **absolute** value,
rejecting only *replacements* more than 10 m **above** the body. So a surface above the body can
win, and the landing test `y + stepY < height` then lifts the body onto it. This is not "the nearest
surface below within 10 m", and it is not a downward ray.

**The per-step admission test** inside the column branch is `stepY < 0 || COMPLEX`: without
`COMPLEX` the query runs only on a **descending** step, with it on every step. The sweep branch
carries no such condition.

**The struck surface picks the bounce branch.** Its type at `+0x20` maps `1→1`, `4→2` — the branch
index for `default` / `water` / `lava`. Install-wide the `BOUNCE_SEQUENCE` blocks name `default` 324
times and `water` 104; **no block in the install names a lava branch**.

⚠ `altitude_surface` is **not** a terrain-only bit — the great majority of world nodes carry it,
buildings included — so filtering a column by it does not keep debris off rooftops. Any argument
that reaches for it on that reasoning is reaching for the wrong thing.

## The landing response

The original **replaces the step's Y**; it reflects nothing.

- While any of `|vx| ≥ 0.1`, `|vz| ≥ 0.1`, `|vy| ≥ 0.5` holds:
  `stepY = |stepY × 0.5| + height − y` — i.e. the body is set half its descending step **above** the
  surface.
- Once all three have fallen below: `stepY = height − y` exactly, resting the body on the surface.
- **X and Z are left untouched**, so the body keeps its horizontal travel through the contact frame.
- All three velocity components are then multiplied by `0.19999999` — `0.2` in float — **keeping
  their signs**.

**Termination is an energy test, not a bounce count:** the body ends when `accel² > speed²` at
contact, and damps-and-continues otherwise. Each contact takes four fifths of the speed, so under
Earth gravity a piece striking at 20 m/s damps to 4 and ends on its next contact — one or two hops.

⚠ The two thresholds are asymmetric on purpose, and the horizontal one is tested **per axis**, not
on the horizontal magnitude. ⚠ A damped body whose velocity keeps its downward sign does not hop off
the surface: what lifts it clear is the half-step in the **pose**, and reading the response as a
launch is how an implementation ends up with debris hovering.

⚠ **Nothing in this chain carries an extent term.** `FUN_004cf200` hands the query the node's
**origin**, `FUN_0055bbc0`/`FUN_0055d5c0` return the struck polygon's height at `(x, z)`, and the
database admits only surfaces at or below the query point. The original therefore rests a piece's
*origin* on the polygon, and how deep the piece looks is how far its own geometry hangs below that
origin. There is no offset here to find.

## The termination model

**With `RUN_TIME` authored** (`0x400`): the final step is shortened by the overshoot
(`dt = frameDt − (elapsed − RUN_TIME)`) so the motion ends exactly on time, and the update returns
"done" once elapsed ≥ `RUN_TIME`. `RUN_TIME` is a **ceiling**, universally — a body that lands first
ends first.

**With no `RUN_TIME`**: `+0x148` is reused as a watchdog accumulator that ends the body at **15 s**
on the column path and **35 s** on the sweep path. ⚠ It is charged only on a step whose contact
query **ran and came back empty** — a body descending toward ground it can see never accumulates a
tick, and a non-`COMPLEX` body climbing is not even queried. It is a backstop, not a flight timer.

Both halves of the landing response also run when the watchdog fires, with a **null** surface
record — which indexes to `default`, and is what makes an untimed body still dispatch its bounce
branch.

## The tumble (`FORWARD_ROTATION`)

`Time.initial` is a **rate in rad/s**, not a total angle, and `Time.delta` is that rate's
acceleration: the update seeds a live rate at `+0x84` from `initial` and integrates it by
`+0x84 += dt × delta` each frame. `RUN_TIME` never enters the derivation.

Per frame it applies the euler triple `(dirZ × rate × dt, 0, −dirX × rate × dt)` off the direction
cache at `+0x70`/`+0x78`, accumulated onto the node's own angles (`FUN_004d25c0`/`FUN_004d1ba0`).
That is a rotation about the horizontal axis **perpendicular to the launch direction**, left
unnormalised — so its length is the launch's own `h = 1 − |elevation|/90`, and **a steep throw
tumbles slowly while a flat one tumbles fast off the same authored number**.

⚠ **A body launched by the vector `TRANSLATION` form does not tumble at all.** The direction cache
is filled only by the `translation_range` branch and the parser zeroed the struct, so the multiply
is by zero. That is **495 of the install's 1,399** authored tumbles, including all four
`player_crash_dirt` pieces — which carry the largest authored numbers in the install.

`FORWARD_ROTATION DISTANCE` (flag `0x40`) is the same shape driven by the step rather than by `dt`,
i.e. a turn per metre travelled. **All 1,399 tumbles in the install author `Time` and none authors
`Distance`**, so the branch is documented and unbuilt.

## The other channels

- `XYZ_ROTATION` (`0x20`) is a steady spin about the node's own axes, rad/s compiled.
- `SCALE` (`0x100`) is a linear ramp, clamped at 0.001, and the authored numbers are **offsets from
  unit scale** rather than absolute sizes.
- `MORPH` (`0x200`) writes `node+0x3c` into `+0x24`, clamped to 1. The original reads it; this
  engine does not, and no chapter in this install authors it.

## What the install actually authors

Re-derived over all 8 chapters' `cam_anim`, walking **both** `sequences` and `unknown_seq` — the
compiled destruction slot a real kill dispatches (`analysis/object-motion-flags/census.py`).

**3,066** `ObjectMotion` events, of which **1,983** are ballistic and **1,640** carry a `gravity`
block; every gravity-bearing motion is ballistic. The three booleans take four combinations and no
others:

| `complex` | `no_altitude` | `do_intersections` | events | distinct shapes |
|---|---|---|---|---|
| false | false | false | **1,378** | 867 |
| true | false | **true** | **166** | 25 |
| true | false | false | 88 | 11 |
| false | **true** | false | 8 (`gunshell`, one per chapter) | 1 |

So **1,466** bodies take the column tier, 166 the sweep, and 8 opt out. `do_intersections: true` is
a strict subset of `complex: true`, and all 254 `COMPLEX` carriers are aircraft wreckage — the
eleven airframes, `player`, both `player_crash_*`, `agyrobus`, `autogyro_loserotor`,
`drop_smokescreen_canister`. Three of them author gravity **stronger** than Earth's (−15, −20),
which is not what a body with gravity switched off gets authored.

Over those 1,640 gravity blocks the commonest authored values are **−9.8** (632 events) and **−10**
(408); the weak −1/−2/−3 sit on smoke trails, where floating is the authored look.

**296** ballistic events author no `RUN_TIME` at all, and **every one of them carries a gravity
block** — so every untimed body descends, and the watchdog is genuinely a backstop rather than the
thing that ends them. 120 of the 296 name a `BOUNCE_SEQUENCE`; the rest are switched off downstream
by the flying piece's own `ACTIVE_STATE`.

## Where CSVM deliberately differs

Everything here is a known, deliberate divergence — not a gap waiting to be closed.

| Divergence | Why |
|---|---|
| **The column tier is a downward ray taking the first surface under the body**, not a grid-cell record pick | The original's pick degenerates to exactly this whenever a cell holds one ground surface, and it cannot lift a piece onto a ceiling it was flying beneath. The engine has no terrain-grid cell database to query |
| **The column reuses the collider set** (`intersect_surface`), without the original's `altitude_surface` filter | Measured, not assumed: the two sets differ by 28 nodes install-wide, every one a destructible's own sub-part rather than terrain or a building shell |
| **`ColumnDepth` 4096 m** bounds the ray | The original bounds nothing — its query is a cell lookup and the cell's surfaces come back at whatever depth they sit at. A ray needs a finite end; this one is past any chapter's vertical extent. ⚠ Shortening it invents a rule the original does not have |
| **The sweep's `ArmDistance`/`ArmSeconds` epsilon** | Stands in for the original's own self-hit guard (`FUN_004ccf50`/`FUN_004ccf00`), which clears the flying piece's `intersect_surface` bit around the query instead |
| **A session that wires no collision mask runs neither tier** | Structural, and it is what keeps the labs, the headless suites and 9 of the 14 goldens deterministic. The original always has a world database |
| **`RunTime` and the termination ceiling are two numbers** | The engine reports a duration to the sequence clock (which hides vanish-shape debris) *and* terminates the body; the original's single `+0x148` slot does both because its sequence timing reads the event differently |
| **A no-`RUN_TIME` body still reports the parabola's return to launch height as its duration** | Only as the *reported* duration, never as the ceiling. It is what `ACTIVE_STATE`-hidden pieces are timed against, and a watchdog fed into it would leave them on screen for 15 s |
| **The per-step admission test and its `COMPLEX` widening are transcribed but behaviourally inert** | With a downward ray, a step that ends higher than it starts cannot end below a surface found at or under its start. They are load-bearing in the original because its cell query can return a surface *above* the body |
| **A defensive contact cap (8)** | The energy test ends a body in two or three contacts, so it is never reached; it exists so a mistake in that test cannot spin a body forever on the hot path |
| **`IMPACT_FORCE`, `MORPH` and `FORWARD_ROTATION DISTANCE` are not built** | No data reaches the first (unreachable combination), no chapter authors the second, and all 1,399 tumbles author `Time` for the third. `IMPACT_FORCE` is filed as `BL-343` |

## Retired and superseded readings

Kept because in each case a reading *died*, and the next reader must not re-derive it.

### ⚠ `translation_range.y` as a spherical elevation — RETIRED (B4, 2026-08-11)

The elevation was applied as `sin(el)` with `cos(el)` on the horizontal, giving a unit-length
direction. The binary computes `elev/90` with the L1 remainder and never passes the elevation
through trigonometry at all. **Do not "fix" the non-unit magnitude** — a direction whose length
varies with elevation looks like a bug and is the whole finding; normalising it reinstates the
error. Measured at the controls, the unit-sphere reading launched `m_build03`'s 60–70° debris
20–25 % too fast and cut six of its nine pieces at 67–72 % of their arc, still climbing.

### ⚠ `DebrisTune.LaunchScale = 0.65` — DELETED (D10, 2026-08-13)

A global multiplier on every launch speed, judged at the controls against
`OriginalScreenshots/Videos/m_build03 destruction.mp4`. It was a decode error wearing a tune's
clothes: the elevation finding above supplies **0.745–0.81** across `m_build03`'s own 60–70° band,
which is what the 0.65 was standing in for. ⚠ The **~0.58** that sits beside it in the older record
is **measured off footage** — a frame comparison of that same kill — and by this project's standing
rule a footage-derived measurement never contests a decode. The knob is gone in full: the class, the
`--debris-launch`/`--debris-gravity` flags, the `debris.*` config keys and the lab panel. **If an arc
reads wrong from here on, the answer is a further decode or a filed item, never a scalar.**

### ⚠ `forward_rotation.Time.initial` as a total angle over `RUN_TIME`, about local X — RETIRED (C10, 2026-08-13)

It is a rate in rad/s about the launch's own horizontal perpendicular. The reasoning that produced
the total-angle reading — that "5π and 4.44π are clean multiples of π" — is a coincidence of the
authored numbers and must not be used to argue the decode back. ⚠ `CAP-16`'s wing-panel strip
measured **20–30 °/s** (**measured off footage**) and was recorded as *confirming* the total-angle
reading; it fits "those pieces do not tumble at all" better than either reading, and it is noted as
agreement only. No measurement off that footage decides this (`docs/verification.md` **DET-12**).

### ⚠ `translation.delta` as a ramp divided by `run_time` — RETIRED (B5, 2026-08-11)

The reasoning was that a change spread *over* the run time must be divided by it. The original
stores `dir·delta` straight into the acceleration slot. The units change by the run time itself: a
`delta` under a 5 s `RUN_TIME` now contributes **five times** as much acceleration. If a specific def
looks wrong afterwards, re-read its block — do not reinstate the division.

### ⚠ "`do_intersections: false` means the original ran no contact test" — RETIRED (C6, 2026-08-12)

The default path **is** a contact test; `DO_INTERSECTIONS` only upgrades it to the geometry sweep.
This claim is what justified 1,466 bodies sinking through the world as fidelity, and what left
`BL-245`'s falls deferred on "a decision to diverge" that never existed. Its companion — "`RUN_TIME`
is a flight duration" — died with it.

### ⚠ "`no_altitude` is gravity or spawn positioning reckoned relative to terrain altitude" — RETIRED (C7, 2026-08-13)

Exactly inverted: `NO_ALTITUDE` is the **opt-out from the landing test**. The reasoning that killed
the correct reading ran from `PT-46` (d), below.

### ⚠ `PT-46` (d) — the OBSERVATION stands, the MECHANISM was misattributed (C6/C7)

At the controls, the original's debris was seen sinking through terrain, and that observation is
real and is not withdrawn. What was wrong is the mechanism drawn from it — "so the original runs no
ground test, and `do_intersections: false` is the proof" — which then propagated into two documents
as a settled reading and killed the correct one. The decode resolves the conflict in the other
direction: the original *does* test, and it rests a piece's node **origin** on the struck polygon
with no extent term, so a piece whose geometry hangs below its origin looks sunk. ⚠ Recorded as a
conflict resolved by the decode, not as something the user did not see.

### ⚠ "Every golden capture builds no world colliders" — RETIRED (A1, 2026-08-10)

`Fly` sessions build collision, and three goldens are flight sessions. What was true is narrower and
was measured rather than assumed: none of the 13 goldens then pinned contained a piece actually
coming to rest, one being structurally unable to and the other's window far too short. `c1-debris-rest`
was pinned to close that gap, and its coverage was proved by disabling the tier and watching the hash
move.
