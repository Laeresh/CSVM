# The `gunhit` family: 12 defs, and only three of them fail to stop

`census.py` prints every `*gunhit*` anim def in all 8 chapters' `cam_anim` — which puffer each
starts, whether anything stops it, and the debris `OBJECT_MOTION` run times. Run from the repo root:

```
python analysis/gunhit-family/census.py
```

## The finding (2026-08-01)

A gun's `IMPACT` `default` names a `<caliber><ammo>_gunhit`: **3 calibers** (`3040`, `5060`, `70`)
× **4 ammo types** (`slug`, `dum`, `ap`, `mag`) = **12 defs**, identical in all 8 chapters. They
split into three shapes by ammo, and the emitter-stop question splits with them:

| Ammo | Anim root | Puffer(s) started | `ACTIVE_STATE 0` |
|---|---|---|---|
| `ap`, `dum` (6 defs) | `dum_gunhit` | `whitehotpuffer` | **yes**, at `EVENT_OFFSET` +0.1 s |
| `mag` (3 defs) | `mag_gunhit` | `firepuffer` + `whitehotpuffer` | **yes**, both at +0.3 s |
| `slug` (3 defs) | `gunhit` | `blacksmokepuffer` | **NONE** |

So the claim "the `gunhit` smoke has no stop event" is **true of the case that matters and false of
three quarters of the family**. Slug is the stock ammo on every gun weapon — `wep_00`–`wep_03`,
`30`/`40`/`50`/`60`/`70slug`, `MPTUR` and `TURRET` — so an engine that plays these defs must supply
a bound for slug and must not assume the other nine need one.

⚠ **The `mag` firepuffer stop carries no `START_TIME`.** A null start means "immediately after the
previous event" (`SequenceRunner.SetDue`), and it follows the `whitehotpuffer` stop at +0.3 s — so
both stop at +0.3 s, not at 0.

## What bounds a slug hit, given the data ships no stop

Two numbers, neither invented:

- The family's **longest authored stop is 0.3 s** (mag). That is what the engine uses as the gun
  instance TTL (`ProjectilePool.GunEffectTtl`).
- `blacksmokepuffer`'s `TIME_INTERVAL` is **1.1 s** (`gunhit.zrd.json`) — one particle per 1.1 s of
  emission. Any bound below that yields exactly one puff per hit, which is what the effect is: a
  single 0.1–0.5 m smoke puff, `GROWTH_FACTOR` 1.2, lifetime 2.1–3.5 s. It is not a plume, and a
  capture that expects one will read as "nothing rendered".

The slug defs' only other authored duration is their debris `OBJECT_MOTION` `RUN_TIME` — 1 s for
`bit1`, 2 s (3040) or 4 s (5060/70) for `bit2`/`bit3`/`chunk` — which does not gate emission. Those
nodes carry no geometry in this install anyway (`bit1`–`bit3` 0 vertices; `chunk` one quad).

## Two things that make a probe of this lie

⚠ **`PLAYER_RANGE 500` gates the puffer** (logged as `cond PlayerRange(250000)`). A strafing probe
set up at a natural standoff put the impacts ~800 m out: every round hit, the `impact:` breadcrumb
resolved the right effect name, and not one puffer was built — indistinguishable from unwired code.
The debris gate is 200 m and the light gate 1000 m. See `docs/verification.md` WORLD-24.

⚠ **A gun's `buildings` entry is not a `gunhit`.** Every gun but `wep_02` (50slug, which routes to
`large_fireball`) has `buildings` → `bld_damage.flt`, absent from the install. The `gunhit` family is
reached through `default`, i.e. terrain — so gun-impact work is verified by strafing dirt.
