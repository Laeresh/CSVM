# `BL-061` item 2 — the template MESH half: what actually failed, and what did not

The instrument is in the engine, not in this directory: `--effects-test` now reports the mesh half
beside the puffer half. Reproduce either reading with

```
.\RunGame.ps1 --effects-test --chapter=C1      # → .scratch/effects_test.txt
```

`--effects-test` implies `--det` (master seed 1), which the item's Verify step asks for by name:
several gun effects gate their puffer behind `RANDOM_WEIGHT`, so an unseeded run manufactures a
different set of effects each time and two runs are not comparable.

## The claim as filed, and how much of it survived

`BL-061` item 2 (M3 D32, 2026-07-24) read: *the effects stage is hidden, so puffers — drawn
independently — render while the template's meshes do not.* Three quarters of that had already been
fixed by work that came after it and was never checked back against the item:

- **The stage is not hidden any more.** D31 made the stage visible with each ROOT hidden and had
  `AnimRuntime.ShowPlacedTemplates` reveal the one a `PlayEffectAt` lands on.
- **`BL-225`** turned each root into `EffectPoolSlots` pooled copies, so "a copied instance per
  call" — the shape the item proposed — exists.
- The **`gunhit` bits** the item names render, and have been seen rendering in a real capture: the
  `chunk` quad is `CAP-25` (`analysis/gunhit-family/FINDINGS.md`), identified off a freeze-frame of
  our own build. `bit1`–`bit3` carry **0 vertices in this install** and never had anything to draw.
- `he_ring` (the HE ground ring), `ap_effect`, `flash_control`, `carnage_ring`,
  `huge_splash_model` and `dum_gunhit` all showed their meshes at the call site before this item.

What was left is one mechanism, and it is not the stage:

> **A template reached through `CALL_ANIMATION` was MOVED to the call site and left hidden.**
> Only `PlayEffectAt` revealed anything, so an effect whose geometry lives in a *called* def's own
> template root drew none of it — while its puffers, which are world-level particles and do not
> care about the root's visibility, emitted at the site and made the effect read as working.

## Measured, per effect (C1, `--det`; C5 is identical — the templates are chapter-invariant)

| Effect | mesh half before | after | what appeared |
|---|---|---|---|
| `he_ground_effect` | `he_ring 1/1` | `he_ring 1/1; he_ring1 1/1` | the HE **upper ring**, called via `call_he_ring1` |
| `sonic_ground_effect` | **nothing** | `sonic_ring1 2/2; sonic_ring2..4 1/2` | the sonic **rising rings**, called via `ring_up1`–`4` |
| `torpedo_water_effect` | `carnage_ring 1/1` | `+ huge_splash_model 1/1; ripple 1/3` | the torpedo **splash model and ripple**, called via `huge_splash` |
| `big_splash` | `huge_splash_model 1/1` | `+ ripple 2/3` | the splash **ripple rings** |
| every other name | unchanged | unchanged | — |

The called defs are the D31 ring set the stage exists for: `call_he_ring1` is anchored on the
separate staged root `he_ring1`, `ring_up1`–`ring_up4`/`ring_down1` on `sonic_ring1`–`5`, and each
one activates its own mesh and then scales/fades it. Every one of those roots is in
`WorldEffectsFactory.EffectStageRoots` already — they were staged, placed, animated, and invisible.

## The second half: an ended effect left its mesh lit

Found by the same census, in its residual line (what is still visible one tick after the effect is
stopped). **`dum_gunhit` — the ap/dum/mag gun-hit chunk mesh — stayed lit at the impact point for
the rest of the session**, and every effect that played afterwards was read against a stage
carrying it.

The asymmetry is the tell, and it is authored: the **ap/dum/mag** hits ship an `ACTIVE_STATE 0`
stop and finish 0.1–0.3 s in, so the instance retires and `FinishEffectInstance` consumes its TTL
entry — leaving nothing to hide the root, since `Stop` reaches a template only through a live
instance. The **slug** hits ship no stop at all, run to their TTL, and were hidden by the sweep's
`Stop`. So the family with the *more* complete data was the one that leaked.

Three things had to be true of the hide, each measured rather than assumed:

1. **Not on instance-finish alone.** The ring defs' scale/opacity motions outlive the sequence that
   launched them — hiding on finish cuts the explosion ring off mid-expansion (D31). The hold asks
   whether anything is still animating the copy.
2. **Asked of the ROOT, not of the def.** What a template flings is routinely driven by a CALLEE's
   motions on the CALLER's copy (`biggun_flying_parts` is one `CALL_ANIMATION` onto its own root), so
   a def-scoped hold would hide the copy out from under the pieces still flying in it.
3. **Not while a sibling def is live on the same copy.** `rear_flash_effect` is two instantaneous
   events plus a `CALL_ANIMATION` back onto `rear_flash_control`, so it finishes on the tick it
   starts; a private hide blanked the callee's flash mesh that had just been revealed there. Caught
   as a regression in this census (`rear_flash_control` went 1/2 → 0/2), not by reasoning.

A def whose t=0 events complete it never reaches the retire walk at all — `Start` removes such an
instance itself — so the hide is scheduled from the reveal for that case.

## What this does NOT settle

**`zep_ng_dstry1.flt` — the zeppelin destruction model — still shows 0 of its 8 meshes, and the
cause is not the mesh half.** Its parts are `8/8 self-visible` in the gamez base state and the root
is revealed at the call site, so the geometry and the visibility path are both fine. They go dark
because `biggun_flying_parts` calls `dblcannon_flying_parts`, whose eight sequences each launch a
part with `OBJECT_MOTION` and then switch that part off with a **null-start** event — i.e. after the
motion's own duration. Those launches carry **no `RUN_TIME` and no `BOUNCE_SEQUENCE`**, and
`MotionRuntime`'s solved-flight path (`BL-240`) is gated on a bounce being named, so the launch
reports duration 0, the deactivation fires in the same instant, and all eight parts vanish on the
tick they were thrown. That is a third launch shape next to `BL-240`'s solved bounce and `BL-245`'s
falls; filed as `BL-257`, not fixed here.

**The picture is not verified.** The 13 goldens are hash-identical, `c1-destroy-effects` among them,
which says this changed nothing they see — the rings only appear on a live rocket impact, which no
headless shot fires. `PT-35` (playtest.md) owes the capture: a rocket hit with its ring set in
frame.
