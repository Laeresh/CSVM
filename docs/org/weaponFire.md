# Gun fire timing — how the original emits rounds, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), 2026-08-18, settling the fire-rate half of backlog
`BL-266`(a). Every claim names the function or address it came from. No decompiler output is
reproduced; addresses are given so any claim can be re-checked at source.

**The question this page answers.** The engine fires the player's guns *one selected slot at a
time* at the authored `FIRE_RATE` (8.0 for `wep_40`), alternating its two muzzles. A clip of the
original firing 40-cal slugs gave a counter-derived "~12–13 rounds/s", which raised the worry
that the original either fires two gun groups at once or double-rate. This page decodes the true
round-emission model from the binary. Answer up front: **the original fires exactly one round per
fire-tick from a single selected gun group at the authored `FIRE_RATE`; the engine's model is
correct and is not under-firing by rate.** The clip's higher number was a redraw-window artifact.

## One selected gun group, per-plane weapon defs

The plane object (`DAT_0071c298` for the player) carries **four gun groups**, each described by a
weapon def pointer stored at `plane + 0x4f4 + group·0xc`, plus a parallel **ammo/cooldown counter**
at `plane + (group·3 + 0x13e)·4` (= byte offset `0x138 + 12·group`). Only **one** group is the
*gold* armament at a time, selected by the single index `plane + 0x604`:

- Arming/switching: `FUN_004b20b0(plane, def)` writes the chosen def to `plane + 0x950` (the
  armed-def pointer) and, for the player, immediately runs the fire routine `FUN_004540c0`.
  `FUN_004b2550` arms a group wholesale: it stores the def at `+0x4f4 + group·0xc` and sets that
  group's counter at `+0x138 + 12·group` to an ammo count (`param_4`, from `FUN_004bad90`).
- Selection helpers read only `plane + 0x604`: `FUN_004b3470` advances through groups to pick the
  one that can fire, and `FUN_004b34f0` drives the "fire when a ready group exists" logic using
  the same single `+0x604` index.

There is no second "active gun" state — four groups, one armed at a time, one `+0x604` selector.
That is the engine's "one gun slot at a time" model, verbatim.

## The fire tick spawns exactly ONE round

The per-frame fire update `FUN_004897c0` (the main weapon/AI tick) calls, for the player plane:

```
FUN_004881e0(plane)   // fire-tick: spawn a round if the group is ready
FUN_004b34f0()        // auto-fire selection / cooldown gate
FUN_004b3e50()        // muzzle-flash / recoil timers
```

`FUN_004881e0` reads the trigger (input slot `0x13`, fire bits `0x3/0x4`), reconciles the armed
def (`+0x950`) with the selected group's def (re-arming if they differ, `FUN_004b20b0`), and then
spawns a round: a **single** `FUN_00498850` call and a **single** `FUN_0045e470` projectile spawn
(disassembly `00488440` / `004884b8`). There is **no loop over barrels and no salvofire** — one
round leaves per fire-tick. The round interval is gated by the group's counter, decremented
elsewhere until it drops below the fire threshold seen in `FUN_004b34f0`
(`plane + (group·3 + 0x13e)·4 < 1`).

Consequently the effective rounds per second equals the authored `FIRE_RATE` — the same data the
engine already reads (8.0 for `wep_40`). The two muzzle slots are visual/aim, not a doubled rate.

## The two barrels are muzzle-flash / assist, not a rate doubler

Each gun group still has two barrel slots:
`FUN_004b3e50` iterates **4 groups × 2 barrels** at `plane + 0x3b8 + i·0x24` (the same
`+0x3a4 + i·0x24` slot array the aim-assist half reads for its "two barrel slots",
[`aim-assist.md`](aim-assist.md)). It updates muzzle-flash/recoil timers
(`_DAT_0071c45c + counter < DAT_0071c470` → reset) — visuals for a two-muzzle gun, matching the
engine's alternating muzzles at the *same total* rate. They do not emit a second projectile.

## Why the clip read "~12–13/s" and why that is wrong

The clip's counter dropped **46 rounds** (2371→2325). At the true `FIRE_RATE` 8.0/s that is a
~5.75 s burst. The motion-redraw window the earlier analysis used to time the burst only captured
~4.5 s (frames 113–247 at 30 fps) — the redraw detector under-bounds the firing, and dividing the
46-counter by shorter windows inflates the rate (46/4.5 ≈ 10.2; the "12–13" figure came from an even
narrower window). This is the same redraw-artifact trap `FINDINGS.md` already flagged. The engine's
8.0/s is the correct rate; there is no rate shortfall to fix.

## Implication for the gun-rattle amplitude (`BL-266`(a))

With rate ruled out, BL-266's remaining amplitude avenue — the "(B) 60 fps pose-interpolation"
loss — is now also ruled out: the shake pivot is written once per 60 Hz physics tick and Godot
auto physics interpolation is OFF, so the 15 Hz sawtooth renders stepped (~3.5× above Nyquist)
with no smoothing loss. The gap is instead a **law-derivation conflation**: the `7e-5 × caliber`
law (kick amplitude 2.80e-3 rad) was matched to the clip's *rendered* RMS (2.80e-3 rad), but a
kick of envelope `E` renders only ~0.25·E as RMS at 8/s, so the engine reads ~4× under the law's
literal number by construction — not a pipeline loss. Whether the clip's 2.8e-3 RMS is itself
inflated (it reads ~6.5× over the model) is unresolved and needs an engine ground truth
(pivot-`Roll` log) before any `magnitude_factor` change. Full reconciliation:
`analysis/gun-wobble-shake/FINDINGS.md`.
