# The third `OBJECT_MOTION` launch shape: no `RUN_TIME`, no `BOUNCE_SEQUENCE`

`census.py` walks every compiled `ObjectMotion` event in all 8 chapters' `cam_anim`, splits the
ballistic ones by which termination fields they carry, and answers the three questions `BL-257`
made the precondition of any fix. Run it from the repo root:

```
python analysis/bl-257-nulled-launch/census.py
```

## The finding (2026-08-06)

Of **1,968 ballistic `ObjectMotion` events / 958 distinct `(file, anim, node)`**, four shapes:

| shape | events | distinct | who |
|---|---|---|---|
| `RUN_TIME` alone | 1,477 | 677 | the ordinary timed motion |
| `RUN_TIME` + `BOUNCE_SEQUENCE` | 204 | 49 | `BL-245`'s deferred half (102 name a live `water` branch) |
| `BOUNCE_SEQUENCE` alone | 120 | 113 | `BL-240`'s solved bounce, plus `BL-245`'s falls |
| **NEITHER** | **167** | **119** | **`BL-257` — this shape** |

**Q1 — how many omit both?** 167 events across 119 distinct defs. Not one repro: `dblcannon_
flying_parts`' eight zeppelin-cannon parts are the reachable one, but the same shape carries every
`m_stuff_blowup`/`m_gens_blowup` (14 + 6 defs), both `loading_crane`s, `destroy_jsign`,
`col_tower_destroy`, `mineshack_destroy`, `shaft_destroy`, `sluice_destroy`, `switchhouse_destroy`
and `collapse_platform`.

**Q2 — do they all launch upward?** Every one carries gravity (**−9.8** on 161, **−10** on 6) and
`do_intersections` is **false** on all 167. ⚠ That was read here as "no ground test is being asked
for"; it is not what the flag means — `false` selects the original's **cheap** contact tier rather
than opting out of contact (`docs/org/objectMotion.md`, 2026-08-13), so all 167 are contact-tested
today. **159 of the 167
always launch upward** — an apex exists for every draw the range allows, so
`MotionRuntime.FlightToLaunchHeight` solves them. The other **8 have no reliable apex** and are
`BL-245` falls wearing this shape:

| def / node | vertical launch speed over every draw |
|---|---|
| `fuelboxbreaks1`/`2` `rockerarm` | elevation 90°, speed **−45…45** — half the draws point down |
| `bridge_destroy01` `bridge_truck01` | `translation.initial.y` **0**, no spread — level |
| `rope1burn` `part2`/`3a`/`3b`/`3c` | −0.44…−0.5 m/s ± ~1 — a burning rope end dropping |
| `rope1burn` `part4` | −1.0 m/s ± 1 — same |

⚠ The vertical speed is `(elevation/90) · speed` for the `translation_range` form (a **linear**
elevation, not the spherical `sin(elevation)` this page assumed — corrected 2026-08-11; the sign
argument is unaffected), so a **negative speed flips an upward elevation into a downward launch** —
the `rockerarm`s read as "elevation 90°, straight up" until the speed range is read with them.

**Q3 — is the following event the piece's own deactivation?** **159 of 167** are switched off by
their own `ACTIVE_STATE 0` downstream of the launch, and every intervening event is **null-start**,
i.e. chained behind the flight (158 have it as the literal next event; `destroy_pwr_station`'s
`pwr_engines` puts three `CALL_ANIMATION`s in between). The remaining **8** — `collapse_platform`'s
`platform_b`, both `rockerarm`s, and all five `rope1burn` parts — are simply the **last event of
their sequence**, so nothing hides them either way.

So the shape is "fly, then vanish", and the null-start deactivation is the thing that needs the
flight time. Read as duration 0 (the pre-fix gate), it fired on the launch tick and hid every piece
before it moved.

## What landed

`MotionRuntime.Create`'s solve gate dropped `&& data.Has("bounce_sequence")` — the admission test is
now the **absent `RUN_TIME` plus an apex**, not the bounce. `PendingBounce` still arms only where a
`BOUNCE_SEQUENCE` is named, so this shape solves its flight and owes nothing.

The 8 no-apex events were unaffected by construction: `FlightToLaunchHeight` returns 0 for
`v0y ≤ 0`, the caller declined the body, and they stayed posed at rest. ✅ **They fall and land now**
— the launch-height solve supplies only the duration an untimed body *reports*, and what ENDS a body
is its contact tier or the original's watchdog, neither of which needs an apex.

## What this does NOT settle

~~**The 8 falls still do not fall.**~~ ✅ **Closed 2026-08-13.** `rope1burn`'s rope ends visibly drop
in the original (user, at the controls, 2026-08-06) and held their rest pose here. They fall now,
along with the rest of `BL-245`'s deferred falls, because contact is the default rather than
`do_intersections`-gated.

**Return-to-launch-height is still a CHOICE, not a decode** — inherited whole from `BL-240` — but it
is no longer what ends a flight, only the duration one REPORTS to its sequence. ⚠ The argument
offered for it here ("the original tested real ground via `do_intersections`; every one of these 167
authors it `false`") is retired: `false` is the cheap tier, not the absence of a test, and the
original bounds an untimed body with a 15 s / 35 s watchdog. See
[`docs/org/objectMotion.md`](../../docs/org/objectMotion.md).
