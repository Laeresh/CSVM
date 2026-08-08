# What happens to a launched piece *after* its flight — ground-rest, and who actually asked for it

`census.py` walks every compiled ballistic `ObjectMotion` event in all 8 chapters' `cam_anim` and
asks what the sibling census (`analysis/bl-257-nulled-launch/`, which splits the same events by
*termination field*) does not: which pieces are still in the world when the flight ends, and which
ones the original itself collision-tested. Run from the repo root:

```
python analysis/object-motion-ground-rest/census.py              # the three reports
python analysis/object-motion-ground-rest/census.py pass_plane01 # drill into one def
```

Written to answer `PT-46` (flown 2026-08-08, retired; verdicts in `git log --grep=PT-46`), whose
check (d) asked whether debris passing *through* the ground is a defect. It is not — and this is
why.

## The finding (2026-08-08)

**1,968 ballistic events / 958 distinct `(file, anim, node, shape)`.** Split by whether the launched
node is ever switched off by its own `ACTIVE_STATE 0` downstream:

| shape | stays | events | distinct | what it means |
|---|---|---|---|---|
| `NEITHER` | no | 159 | 111 | fly, then vanish — `BL-257`'s shape, authored to disappear |
| `NEITHER` | **yes** | 8 | 8 | the no-apex strays `BL-257` left posed at rest |
| `bounce` | **yes** | 120 | 113 | `BL-240`'s solved landing — **the population that comes to rest** |
| `run_time` | no | 996 | 559 | timed motion, hidden afterwards |
| `run_time` | **yes** | 481 | 118 | timed motion left in place, wherever the parabola ended |
| `run_time+bounce` | no | 44 | 9 | |
| `run_time+bounce` | **yes** | 160 | 40 | `BL-245`'s deferred half, left in place |

**The rule, and it is simple: a piece comes to rest only if its launch names no `RUN_TIME`.** That
absent run time is the admission test for `MotionRuntime.FlightToLaunchHeight`, the only landing
solve we have. Anything carrying a `RUN_TIME` integrates its parabola for exactly that long and then
holds its final pose — which, at 3.5–20 s under Earth gravity, is routinely below the terrain.

### `do_intersections` — the original's own collision test

| | `false` | `null` | **`true`** |
|---|---|---|---|
| `NEITHER` | 167 | — | — |
| `bounce` | 120 | — | — |
| `run_time` | 1,118 | 343 | **16** |
| `run_time+bounce` | 54 | — | **150** |

**The original was not ground-testing these either.** It is `false` on *all* 120 of the bounce shape
and *all* 167 of the vanish shape, and on 924 of the `run_time` shape. So a piece sinking through
the terrain reproduces the original rather than diverging from it — confirmed at the controls by
`PT-46` (d), independently of this census.

### The strict test set — ground-tested **and** left lying there

Only **16 distinct `(def, node)` pairs** in all 8 chapters ask for collision *and* stay in the
world. These, and only these, are the cases where "it went through the ground" is a real defect:

| def | node(s) | chapter | shape | `run_time` |
|---|---|---|---|---|
| `player_crash_dirt` | `piece1`–`piece4` | C1B | `run_time`+bounce | 6.0 |
| the eleven airframes (`autogyro`, `avenger`, `balmoral`, `bloodhawk`, `brigand`, `firebrand`, `fury`, `kestrel`, `peacemaker`, `piratefighter`, `warhawk`) | `MAIN_ROOT_NODE` | C1B | `run_time`+bounce | 20.0 |
| `agyrobus` | `MAIN_ROOT_NODE` | C5 | `run_time`+bounce | 20.0 |

Every one carries a `RUN_TIME`, so **we ground-test none of them** — they are exactly `BL-245`'s
deferred half. The player's own crash wreck is the cheap repro: crash and watch `piece1`–`4`.

*Observed 2026-08-08:* `player_crash_dirt`'s four pieces stay above ground. `agyrobus` was shot down
in C5 but lost from sight between buildings. The C1B airframes do not spawn in Instant Action, so
they are unreachable today.

> ✅ **Answered 2026-08-09 — `PLAN-ground-contact` (`docs/plans/`), which this census scoped.** All
> 16 pairs are ground-tested now: a swept trajectory segment ends a flagged body on the first
> collider, `RUN_TIME` demoted to a ceiling, and the `BOUNCE_SEQUENCE` branch picked from the struck
> surface. They are **not** `BL-245`'s deferred half — that half is the 379 events authoring
> `do_intersections: false`, and it is blocked on a decision to diverge, not on a ray.
> ⚠ Two corrections this census could not have seen, both found at the controls: the eleven
> airframes and `agyrobus` name their node **`MAIN_ROOT_NODE`**, a sentinel the resolver dropped, so
> 90 of the 166 flagged events had never launched at all; and a piece's *second* motion (the
> `pNhit` settle hop) is authored `false` yet has to be swept too, or it buries the very piece the
> first one just landed. Both cockpit checks passed 2026-08-09.

## The worked example: one def, both behaviours

`pass_plane01` — a plane parked on the C1 airfield, and the case that made the rule legible:

```
C1   seq0   part1   shape=run_time  run_time=5.0   do_int=False  g=-10.0  stays=True
C1   seq1   part2   shape=run_time  run_time=3.5   do_int=False  g=-10.0  stays=True
C1   seq2   part3   shape=bounce    run_time=None  do_int=False  g=-10.0  stays=True
C1   seq4   part4   shape=bounce    run_time=None  do_int=False  g=-10.0  stays=True
```

All four remain in the world; `part3`/`part4` land and rest because we solve them, `part1`/`part2`
sink because nothing stops them. `m_build01` is the same pattern at nine parts. This is why a
playtest report of "the larger parts stay, the wings go through" is *per-part within one def*, not a
per-object bug.

## ⚠ `do_intersections` is probably a collider test, not a terrain ray

User's reading, 2026-08-08, and it changes the shape of the deferred work rather than any decode: a
collider intersection can land a piece on a rooftop or bounce it off a wall, which no down-ray
reproduces — the `agyrobus` case was lost between C5 buildings precisely because it may have bounced
off one. Scope `BL-059` item 1 / `BL-245` as a collision query, with a down-ray as the cheap first
cut, and record which of the two shipped. `BL-245`'s `[Blocked: ground ray]` tag is the narrower
reading of the same blocker.

## ⚠ A trap the script itself fell into

A null-start deactivation carries `start: null`, so a "was it switched off?" test that returns the
`start` value reads *every vanishing piece as persistent* — it reported all 1,968 events as staying
in the world. `is_switched_off` returns a bool deliberately. Any re-derivation of these numbers
should sanity-check that the four shapes do **not** all come back `stays=True`.

## Related

`analysis/bl-257-nulled-launch/` (the termination-field split these shapes come from) ·
`analysis/object-motion-range/` (the 2026-08-01 azimuth/elevation/speed decode) ·
[`docs/plans/PLAN-ground-contact.md`](../../docs/plans/PLAN-ground-contact.md) (what `BL-059`
item 1 became, and where this census's strict test set was discharged) ·
`BL-245` · `BL-319` (`run_time` is not a flight duration — pieces cut mid-arc or flying long past
landing; found closing `BL-022`, whose arc scale shipped as `DebrisTune.LaunchScale` 0.65,
`git log --grep=BL-022`) · `docs/formats/destructibles.md` "Debris tumbles"
