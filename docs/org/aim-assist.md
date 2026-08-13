# Sticky bullets — the gun aim assist, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), 2026-08-10, settling `BL-091`. Every claim below
names the function or address it came from. A second pass on 2026-08-12 settled the assist cone
(`CANNON_SPREAD`) and the per-target `+0x50` override, both flagged open by the first.

Everything here is a description of *behaviour*. No decompiler output is reproduced; the addresses
are given so any claim can be re-checked at source.

**Where the other halves live.** The authored side — the four `player.json` keys and their shipped
values — is [`formats/vehicle.md`](../formats/vehicle.md)'s `player.json` table, which now points
here rather than guessing. The weapon side — `RANGE`, `VELOCITY`, `CANNON_SPREAD` — is
[`formats/weapons.md`](../formats/weapons.md). CSVM has **no implementation**; building one is
`BL-342`.

⚠ **This page is a decode, not a proposal.** Where it disagrees with a footage measurement, the
decode wins and the disagreement is a note.

## The headline: it is a launch-direction assist, not bullet steering

The name is misleading and the pre-decode reading in `vehicle.md` was wrong. **Nothing steers a
round after it leaves the muzzle.** `sticky_bullet_*` picks a target, solves a lead, and rotates
the *firing vector* at the instant the round is spawned. A round in flight is a straight line,
exactly as [`tracers.md`](tracers.md) describes (`FUN_005b0770`'s re-orientation branch is dead in
this install because every weapon ships `GRAVITY 0`).

It also applies to **the local player only**. AI gunnery is a separate, simpler mechanism (below).

## Function map

| Address | Role |
|---|---|
| `FUN_004735b0` @ `0x004744e0`–`0x004745ad` | `player.json` reader — parses the four keys into globals |
| `FUN_004897c0` | Sim tick — advances `DAT_0071c470` (game time) and drives the two functions below |
| `FUN_004b6820` | Per-weapon fire loop — decides a round goes out, then asks for its direction |
| `FUN_004b6530` | **The assist.** Builds the target scan, applies smoothing + scatter, returns the launch vector |
| `FUN_004bae60` / `FUN_004bb110` / `FUN_004bb3b0` / `FUN_004bb660` | Per-candidate scorer, once per entity list. **Byte-identical scoring**; they differ only in the container accessor |
| `FUN_00441830` | Post-spawn hook that registers a round in the tracked-ordnance list (`LAB_004417c0` removes it) |
| `FUN_0041f9c0` | The shared "best target across all four lists" query — not part of the assist, but it fixes the lists' priority order |
| `FUN_00460e30` | Lead/intercept solver (quadratic) |
| `FUN_004b3e50` | **Per-frame slot update** — the forget timer and the catch-up slerp |
| `FUN_00460840` | Slerp-toward with snap (`FUN_00538d70` is the slerp) |
| `FUN_00460940` → `FUN_004608a0` | Random cone scatter about a direction |
| `FUN_00440ad0` | Returns `DAT_0064f750` — the **network-game flag** (set at `0x00440247` from the `"Network"` subsystem lookup at `0x006237f4`) |

Globals: `DAT_0071c470` = game time (seconds, advanced by `DAT_009ad744` = frame `dt`);
`DAT_0071c298` = the local player's plane.

Weapon-def offsets used: `+0x1c` = `RANGE`, `+0x20` = **`RANGE²`** (precomputed at `0x005adfb8`),
`+0x2c` = `VELOCITY` in m/s (written at `0x005ae0b8`; a `TIME_TO_MAX_RANGE` reciprocal and a
`GRAVITY`-derived `sqrt(2·g·RANGE)` both write the same slot earlier in the parse, and both are
overwritten — `GRAVITY` is 0 for every weapon in this install), `+0x210` = pointer to a secondary
block whose `+0x08` is the **assist-cone cosine** (see below: it is `−cos(CANNON_SPREAD)`).

## The four keys — what the parser actually stores

| Key | Global | Transform at parse | Built-in default |
|---|---|---|---|
| `sticky_bullet_catchup_rate` | `0x0071c454` | stored raw | `1.0` |
| `sticky_bullet_inaccuracy` | `0x0071c458` | **× `[0x006040e8]` = π/180** — the file value is in **degrees** | `1°` |
| `sticky_bullet_forget_interval` | `0x0071c45c` | stored raw, seconds | `0.5` |
| `sticky_bullet_dist_factor` | `0x0071c460` | stored raw, per metre | `2.5e-4` |

⚠ **An original-game bug in the defaults.** The *missing-key* branch for `inaccuracy`
(`0x00474550`) writes its default to **`0x0071c454`** — `catchup_rate`'s global — not to
`0x0071c458`. If `sticky_bullet_inaccuracy` were absent from `player.json`, the game would set
catch-up to `0.001745` and leave inaccuracy uninitialised. The shipped `player.json` always
carries the key, so the branch never fires; do not reproduce it.

The key parsed immediately *before* `catchup_rate` lands in `0x0071c450` and has **zero readers**
anywhere in the binary — genuinely dead, unlike these four.

## Per-muzzle state — 8 slots on the plane

Eight 0x24-byte slots at plane `+0x3a4`, indexed `weaponGroup·2 + barrelToggle` (4 groups × the 2
alternating barrels; `FUN_004b6820` computes the index from `+0x604`/`+0x4c4`).

| Offset in slot | Field |
|---|---|
| `+0x00` | muzzle attachment handle (0 = slot unused) |
| `+0x04` | **smoothed direction** — what the round is actually fired along |
| `+0x10` | **target direction** — what the smoothed one is chasing |
| `+0x1c` | last-update time |
| `+0x20` | flags byte |

⚠ **Both directions are held in plane-local space.** `FUN_004b3e50` resets the target to
`(0, 0, −1)` — local forward — and does the catch-up slerp there. The assist therefore lags *your
own* manoeuvring as well as the target's, and a hard roll drags the gun line with it. Smoothing in
world space is a different feel and would be the wrong port.

## On fire — `FUN_004b6530`

Called from `FUN_004b6820` only when a round is actually going out, and only for
`param_1 == DAT_0071c298`. Steps:

1. Seed the slot's target direction with the plane's own forward axis, and stamp `+0x1c` with the
   current time. This is the "no target found" answer.
2. Build a scan context — muzzle position, the weapon def, a best-score cell initialised to
   **`−FLT_MAX`** (`0xff7fffff`), and a best-direction cell aliasing the slot's target direction.
3. Run the four scorers over the four entity lists (identified below). All four score identically,
   so **the assist is not aircraft-only.**
4. Rotate the winning direction world→local into the slot's target field.
5. Rotate the slot's **smoothed** direction local→world into the output. Note the asymmetry: the
   scan updates the *target*, and what gets fired is the *smoothed* value from previous frames.
6. Apply the scatter cone (below).
7. If the network flag is set, also cache the result at plane `+0x704` — the direction sent on the
   wire. Remote planes' shots use `+0x704`–`+0x70c` verbatim and never run the scan, so **the
   assist is computed once, by the shooter's own machine**.

### The four lists — what the assist can snap onto

| Global | Scorer | What it holds |
|---|---|---|
| `DAT_0071dabc` | `FUN_004bae60` | **`VehicleList`** — aircraft and AI ground/sea vehicles. Named by its net registration at `FUN_004729b0` (`s_VehicleList_00627414`, packer `LAB_00472b20`); it is also the list `FUN_004897c0` walks as the per-plane sim pass. |
| `DAT_0071d914` | `FUN_004bb110` | **Turrets.** The function that owns it, `FUN_004ac170`, carries three `D:\zipper\Crimson\turret.cpp` assert strings (`0x004ac1ce`, `0x004ac26f`, `0x004ac4f8`). |
| `DAT_0071d33c` … `DAT_0071d340` | `FUN_004bb3b0` | **`MStructList`** — the mission structures loaded from `targets.zrd`. A flat `vector<T*>` (stride 4, addressed **by index** over the wire), net-registered at `0x004a2b17` under `MStructList` / `MStruct_%03d` (`0x00629678`). |
| `DAT_0064f78c` | `FUN_004bb660` | **Live proximity-fused ordnance in flight** — see below. |

That last one is worth spelling out because it is not a static entity category. `FUN_00441830`
runs immediately after **every** projectile spawn (both spawn sites in `FUN_004b6820`) and pushes a
0x70-byte tracking record onto `DAT_0064f78c` when either

- the weapon def's secondary-block flag `0x20` is set — the record then carries a localised label
  pulled by `FUN_0059ce40(0x2f6a)`; or
- the def's `+0x44` exceeds `0.01`. That field is **`DETONATION_DISTANCE` squared**
  (`0x005adbb9`, key string at `0x0063d148`), so the test is a proximity fuse longer than 0.1 m —
  the 13 ordnance carriers in [`formats/weapons.md`](../formats/weapons.md).

The record is unhooked when the round dies (the removal thunk at `LAB_004417c0` is stored in the
projectile at `+0x68c`), and `FUN_004bb660` additionally skips records whose `+0x6c` byte is clear.

⚠ **So the assist will snap your guns onto an enemy rocket in flight.** `FUN_0041f9c0` — the shared
"best target" query used elsewhere — ranks this list **above** all three others, which is
consistent with it being the incoming-threat list. It also matches `wep_14` (TORPDO) shipping
`FLYOUT_HEALTH 10` + `TARGETABLE`: trackable ordnance is a deliberate design, not an accident of
the container.

### Scoring one candidate

Rejections, in order:

- the candidate is the local player itself;
- a virtual predicate at vtable `+0x14` returns true (dead / not yet live);
- **same team** — the `+0x08` team id matches the player's, or either is 0;
- `FUN_00460e30` finds no intercept;
- `speed² · t² > RANGE²` — the round could not reach the intercept point inside the weapon's
  authored range;
- the intercept direction falls outside the assist cone.

The cone half-angle comes from the **target**: its `+0x50` field, if `≥ 0`, is a half-angle in
radians and the test uses `−cos` of it. If `+0x50 < 0` the weapon's own cosine at
`[def+0x210]+0x08` is used instead. So an entity can advertise how easy it is to snap onto.

### The cone is `CANNON_SPREAD`, and it is not a dispersion term

Decoded 2026-08-12, settling the composition question this page previously left open.

`CANNON_SPREAD` (string `0x0062b334`) has **exactly one reader in the executable**: the secondary
block parse `FUN_004ba6f0` at `0x004ba9da`, which stores `−cos(value × π/180)` into the block's
`+0x08` (`0x004ba9ef`–`0x004ba9fc`). That slot's only consumers are the four scorers
(`0x004bb088`, `0x004bb32c`, `0x004bb5d2`, `0x004bb887`); no other `[def+0x210]` load in the
program reads `+0x08`. The key is therefore **the assist's acceptance cone**, 6° half-angle for the
stock guns, and nothing scatters a round by it.

The block is `calloc(1, 0x38)` at `0x004ba6f6`, so a weapon def with **no** `CANNON_SPREAD` key
keeps `0.0`, which is `−cos(90°)`: the whole forward hemisphere is in cone. There is no non-zero
built-in default.

⚠ **Sign convention.** The scorer tests `dot(interceptDir, planeAxis) < coneCos` with both sides in
the engine's negated-forward convention, and ranks by `−(distance × dist_factor) − dot`. That is
the same relation as the positive-alignment form used elsewhere on this page; do not mix halves of
the two conventions.

### Who writes the per-target override

`+0x50` sits on the shared entity base class (vtable `0x00608b58`). Every constructor initialises
it to `−1.0f` (`0xbf800000`), which routes the test to the weapon's cone:

| Family | Constructor | Address |
|---|---|---|
| Aircraft / AI vehicles | `FUN_004aff80` | `0x004b0006` |
| Turrets | `FUN_004a9a60` | `0x004a9ae7` |
| Mission structures | `FUN_004a2570` | `0x004a25ee` |
| Tracked ordnance in flight | `FUN_00441b90` | `0x00441be1` |

Two writers can raise it. The authored one is the turret key **`STICKINESS`** (string `0x00629cec`,
`ai.zrd`), parsed in `FUN_004a9df0` at `0x004aa57e`–`0x004aa5b4`: degrees × π/180 into `+0x50`, and
if the authored value is not greater than `0.0` it stores `−1.0` (`0x006034e8`) instead. That
string has one xref, so turrets are the only family with a keyword for it, and
[`../formats/turrets.md`](../formats/turrets.md) records it shipping **zero times**.

The other is `FUN_004a2e00` at `0x004a30d7`, which copies `+0x50` verbatim (no degree conversion)
from the campaign-structs table entry field `+0x48` (`DAT_0071d34c`, stride `0x5c`) when building
mission structures. Where that table field is authored was not traced; it is a binary record, not a
keyword.

**So with the shipped data no entity overrides the cone** and the assist cone is always the firing
weapon's `CANNON_SPREAD`. The override path still belongs in a port, since dropping it changes
behaviour the moment a mission sets it, but it carries no tuning decision.

Surviving candidates are ranked by

```
score = alignment − distance × dist_factor
```

where `alignment` is the dot product of the intercept direction with the plane's forward axis
(≈ 1 when the target is dead ahead) and `distance` is the straight-line range in metres. Highest
score wins; the first survivor always beats the `−FLT_MAX` seed.

⚠ **`dist_factor` ships at `0.0`, which deletes the distance term entirely.** Selection is then
purely "whose intercept is closest to my gun axis" — a distant target dead ahead outranks a near
one slightly off-axis, at any range inside `RANGE`. The executable's own default is `2.5e-4` per
metre, i.e. a 1000 m target loses 0.25 of alignment; the shipped data deliberately turns that off.

### The lead solver — `FUN_00460e30`

A textbook constant-velocity intercept, in the numerically stable form. Given muzzle position,
projectile speed, target position and the **relative** velocity (target velocity minus the
player's), it solves for `u = 1/t` in

```
|displacement|²·u² + 2·(relVel · displacement)·u + (|relVel|² − speed²) = 0
```

taking the root via `a / (b ± √disc)` rather than the unstable `(−b ± √disc) / 2a`, and returns
`aimDir = normalize(relVel + displacement / t)` plus `t`. It fails (returns 0) on zero separation
or a negative discriminant — i.e. a target outrunning the round is simply not assisted. The square
root is the `(x >> 1) + 0x1fc00000` bit-trick approximation, so the lead is accurate to roughly a
per-cent, not exactly.

### The scatter cone — `FUN_004608a0`

Build any perpendicular to the aim direction, rotate it about the aim axis by `rand()/32767 · 2π`
(uniform roll), then rotate the aim direction about that perpendicular by
`rand()/32767 · inaccuracy`.

⚠ **The polar angle is uniform in `[0, θmax]`, not uniform over the solid angle.** Sampling
uniformly on the cap — the reflex when porting — puts noticeably more shots near the rim. With
`inaccuracy 1.0` the cone is 1° wide.

**This is the only scatter.** `CANNON_SPREAD` was assumed to be a second, larger per-gun dispersion
term composing with it somehow; it is not a dispersion term at all (above). A player's round is
perturbed once, by `inaccuracy`; an AI's round once, by the plane's `+0x95c` dead-eye scalar. There
is no second stage.

## Per frame — `FUN_004b3e50`

Called from `FUN_004897c0`'s per-plane pass, **local player only**. For each of the 8 slots with a
live handle and its flag byte set:

1. **Forget.** If `lastUpdate + forget_interval < now`, reset the target direction to local
   forward `(0, 0, −1)` and push `lastUpdate` to `now + forget_interval`. Because `FUN_004b6530`
   stamps `lastUpdate` on every shot, the timer measures **time since you last fired that barrel**,
   not time since the lock was lost. Stop shooting for `forget_interval` and the gun line unwinds
   to centre; keep shooting and it never expires.
2. **Catch up.** Slerp the smoothed direction toward the target direction by
   `catchup_rate × dt`, snapping outright once that product reaches `1.0`.

With the shipped `catchup_rate 5.0` that is a ~0.2 s time constant — and a **full snap on any
frame longer than 200 ms**, which is a real hitch-behaviour difference, not a rounding detail.

## AI gunnery is a different system

`FUN_004b6530`'s other branch — everything that is not the local player — reads the hardpoint's own
authored direction and perturbs it by plane `+0x95c`, the field behind the debug string
`"Dead eye resulting in maximum inaccuracy %f"` (`0x0062b130`), through the same
`FUN_00460940` cone. No target scan, no lead solve, no smoothing.

So `player.json` holding these keys is not evidence that they assist the AI: **the AI's accuracy
model is one per-plane scalar, and the sticky-bullet path is the human's.** That settles the trap
`BL-091` recorded.

## What CSVM would need

`CSVM/src/Flight/Projectile.cs` fires straight down the muzzle axis with no assist at all, which is
why `BL-301`/`PT-43` report gun kills as impractical in VS mode. `BL-342` carries the
implementation. Deltas worth naming up front:

| | Original | CSVM today |
|---|---|---|
| When it runs | at spawn, per round | n/a |
| Target set | vehicles + turrets + `MStruct` targets + **live proximity-fused ordnance**, cone- and range-gated, team-filtered | n/a |
| Selection | most-aligned intercept (`dist_factor` off) | n/a |
| Lead | full constant-velocity intercept on **relative** velocity | built (B3, 2026-08-13) — `AimAssist.TryIntercept`; not yet fed a real target (B4) or driving a fired round (B5) |
| Smoothing | slerp in **plane-local** space, ~0.2 s, snaps past 200 ms frames | built (B2, 2026-08-13) — `AimAssist.Tick`; not yet driving a fired round (B5) |
| Forget | resets on time since **last shot**, not since lock loss | built (B2) — same caveat: the timer runs, but nothing restamps it on a shot until B5 |
| Scatter | 1° cone, polar angle uniform in `[0, θ]` | none — the wrong `CANNON_SPREAD` scatter was removed (A1, 2026-08-13); the 1° `inaccuracy` cone this row describes is still unbuilt, landing with B5 |
| Cone gate | `CANNON_SPREAD` half-angle, per-target `+0x50` override | n/a |
| Multiplayer | shooter-authoritative; the assisted vector is transmitted | n/a |
