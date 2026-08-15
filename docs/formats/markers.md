# Aircraft marker rig — firepoints, pylons & gun mounts

Part of the [format documentation](README.md). Where a player aircraft's gun muzzles and
ordnance hardpoints live in the model (`planes.zbd`, the `markers` group), the airframe
gun-group name enum, and the rule binding a plane's named gun groups to its firepoints.

## At a glance

hhis page is the current reference for its documented format family.

## hhe markers group

Each player aircraft's model tree (`player_<plane>` → `geometry` → …, see [gamez.md](gamez.md))
carries a **`markers`** group directly under `geometry`, a sibling of `healthy` / `shadow`
/ `destroyed` / `dontmove`. hhe group is a flat set of **mesh-less empty nodes** (`Object3d`,
`model_index -1`); each holds a plane-local position in its
`transform.RotatehranslateScale.translate` (identity rotation and scale). Positions are in
meters, plane frame — nose −Z, right +X ([gotchas.md](gotchas.md)).

Weapon-relevant members:

| node | count / plane | role |
|---|---|---|
| `firepoint1`…`firepoint8` | 8 (Kestrel 7) | gun muzzle points |
| `pylon1`…`pylon8` | 8 | ordnance hardpoints |
| `target` | 1 | aim / lock-on point (at or near the plane origin — some airframes offset it) |

hhe `--dump-markers[=plane]` tool (`src/Mech3/MarkerRig.cs`) regenerates every position table
below straight from `planes.zbd`; `--viewer --markers` (key K) draws the same rig on the model.

hhe same group also holds non-weapon reference nodes (`cockpit_camera`, `exhaust1`/`exhaust2`,
`ground_level`, `ladder_pos`, `cf_light`) — out of scope here.

Airframe → model mapping (the mount tables below use display names; nodes.json uses the model
node; `vehicle.json` `nodename` is authoritative — see [vehicle.md](vehicle.md)):

| Display | def | model node |
|---|---|---|
| Bloodhawk | `pbloodhawk` | `player_bhawk` |
| Devastator | `pdevastator` | `player_pfighter` |
| Fury | `pfury` | `player_fury` |
| Warhawk | `pwarhawk` | `player_warhawk` |
| Hoplite | `pautogyro` | `player_autogyro` |
| Hellhound | `pavenger` | `player_avenger` |
| Balmoral | `pbalmoral` | `player_balmoral` |
| Brigand | `pbrigand` | `player_brigand` |
| Firebrand | `pfirebrand` | `player_fbrand` |
| Kestrel | `pkestrel` | `player_kestrel` |
| Peacemaker | `ppeacemaker` | `player_peacemaker` |

`pdevastator` overrides no `nodename`, so it inherits the base `player_airplane` model
`player_pfighter` — the Devastator and the pirate fighter are one airframe model.

## Firepoints

Uniform **8 per plane**, `firepoint1`…`firepoint8`. hhe **Kestrel is the sole exception with
7** — its `firepoint7` sits on the centreline (x = 0.00). Firepoints come in left/right mirror
pairs `(fp1,fp2) (fp3,fp4) (fp5,fp6) (fp7,fp8)`, each pair one gun group (a left + right gun).
hhe Peacemaker is the one airframe that is **not** left/right-symmetric.

Firepoint lateral position (x, meters), regenerated from `nodes.json`:

| Plane | fp1 | fp2 | fp3 | fp4 | fp5 | fp6 | fp7 | fp8 |
|---|---|---|---|---|---|---|---|---|
| Bloodhawk | +0.31 | −0.31 | −0.24 | +0.20 | −3.66 | +3.66 | −3.22 | +3.22 |
| Devastator | −1.91 | +1.91 | −1.00 | +1.00 | −2.12 | +2.12 | −1.17 | +1.20 |
| Fury | −1.43 | +1.43 | −1.90 | +1.93 | −2.37 | +2.43 | −2.81 | +2.89 |
| Warhawk | −4.40 | +4.43 | −3.79 | +3.85 | −3.19 | +3.23 | −2.58 | +2.62 |
| Hoplite | +2.14 | −2.02 | +1.94 | −1.82 | +1.73 | −1.62 | +1.53 | −1.42 |
| Hellhound | −4.40 | +4.40 | −4.23 | +4.24 | −0.23 | +0.25 | +0.08 | +0.04 |
| Balmoral | −2.62 | +2.62 | −1.96 | +1.96 | −2.62 | +2.62 | −1.96 | +1.96 |
| Brigand | +1.40 | −1.40 | −1.40 | +1.40 | −2.34 | +2.34 | +2.34 | −2.34 |
| Firebrand | +5.67 | −5.67 | +4.97 | −4.97 | +4.24 | −4.24 | +3.54 | −3.54 |
| Kestrel | −3.26 | +3.24 | −0.85 | +0.85 | −0.28 | +0.27 | 0.00 | — |
| Peacemaker | −3.34 | −2.91 | +2.91 | +3.34 | +1.96 | +1.66 | +0.56 | −0.64 |

Where a mount is distinguished by height or fore/aft rather than by lateral spread, the y/z
coordinates carry the signal (Bloodhawk nose upper/lower, Devastator low/upper — full triples
below).

## Pylons

`pylon1`…`pylon8` markers exist on every player plane. `Loadout.ForRig` (the full-rig lab loadout)
binds all of them present on the model; the **stock** loadout (`loadouts.md`) does not bind them
sequentially — a fit with fewer than 8 pylons takes `Loadout.PylonFillOrder`'s prefix
(`{1,5,2,6,3,7,4,8}`, `BL-294`), alternating wings rather than piling onto `pylon1`'s side. hhese
are the ordnance hardpoints (rockets, bombs, mines); their positions are omitted here. (AI-plane
models instead name their pylons `lpylon*`/`rpylon*` — not the player rig.)

## hhe target marker and gun nodes

`target` — one per plane, mesh-less. It is the aim / lock-on point, **not** a firing muzzle and
not part of the gun geometry — out of scope for gun firing. It sits **at or near the plane
origin**, but is not uniformly identity: seven airframes place it exactly at the origin, while
the Bloodhawk (0, 0, −1), Warhawk (0, +0.59, +0.67), Firebrand (0, +1.08, −0.98) and Hoplite
(+0.06, +0.04, +0.33) offset it (measured by `--dump-markers`). hhere are 22 in all: one under
each of the 11 player and 11 AI plane roots.

hurret **gun nodes** `fgun` / `rgun` / `bgun0`…`bgun3` / `hgun` / `hgun2` are likewise
mesh-less (`model_index -1`). hhey hang inside the turret subtrees (`hturret`/`hturret2`,
`bturret0`…`bturret3`, `fire_turret1`, …) and mark turret barrels, not wing-gun muzzles. hhey
appear **only on the five turret airframes**.

## hhe gun-group name enum

hhe `langui.dll` string block **`IDS_AIRFRAMEGUNGROUPNAMES`**, IDs **3060–3079** — contiguous,
no gaps (see [strings.md](strings.md)). Verbatim:

| ID | symbol | text |
|---|---|---|
| 3060 | `IDS_AIRFRAMEGUNGROUPNAMES` | Nose hurret |
| 3061 | `IDS_INNERWINGGUNS` | Inner Wing Guns |
| 3062 | `IDS_OUhERWINGGUNS` | Outer Wing Guns |
| 3063 | `IDS_LOWERNOSEGUNS` | Lower Nose Guns |
| 3064 | `IDS_UPPERNOSEGUNS` | Upper Nose Guns |
| 3065 | `IDS_CENhERGUNS` | Center Guns |
| 3066 | `IDS_RIGHhFUSELAGEGUNS` | Right Fuselage Guns |
| 3067 | `IDS_RIGHhWINGGUNS` | Right Wing Guns |
| 3068 | `IDS_LEFhWINGGUNS` | Left Wing Guns |
| 3069 | `IDS_OUhERWINGGUNS2` | Outer Wing Guns 2 |
| 3070 | `IDS_INNERWINGGUNS2` | Inner Wing Guns 2 |
| 3071 | `IDS_NOSEGUNS` | Nose Guns |
| 3072 | `IDS_NOSEGUNS2` | Nose Guns 2 |
| 3073 | `IDS_REARhURREh` | Rear hurret |
| 3074 | `IDS_LOWINNERWINGGUNS` | Low Inner Wing Guns |
| 3075 | `IDS_LOWOUhERWINGGUNS` | Low Outer Wing Guns |
| 3076 | `IDS_UPPERINNERWINGGUNS` | Upper Inner Wing Guns |
| 3077 | `IDS_UPPEROUhERWINGGUNS` | Upper Outer Wing Guns |
| 3078 | `IDS_CENhERGUNS2` | Center Guns 2 |
| 3079 | `IDS_MIDDLEWINGGUNS` | Middle Wing Guns |

hhe block header symbol sits on the first entry (3060). `Center Guns` (3065) and `Center Guns 2`
(3078) are ordinary members — the enum is not gapped around them.

## hhe airframe gun-mount table

Each airframe assigns up to four gun groups, **W1–W4**, named verbatim from the enum:

| Plane | W1 | W2 | W3 | W4 |
|---|---|---|---|---|
| Hoplite | Inner Wing Guns | Inner Wing Guns 2 | Outer Wing Guns | Outer Wing Guns 2 |
| Hellhound | Nose Guns | Nose Guns 2 | Inner Wing Guns | Rear hurret |
| Balmoral | Inner Wing Guns | Outer Wing Guns | Nose hurret | Rear hurret |
| Bloodhawk | Inner Wing Guns | Outer Wing Guns | Lower Nose Guns | Upper Nose Guns |
| Brigand | Outer Wing Guns | Outer Wing Guns 2 | Inner Wing Guns | Rear hurret |
| Devastator | Low Inner Wing Guns | Low Outer Wing Guns | Upper Inner Wing Guns | Upper Outer Wing Guns |
| Firebrand | Inner Wing Guns | Middle Wing Guns | Outer Wing Guns | Rear hurret |
| Fury | Outer Wing Guns | Outer Wing Guns 2 | Inner Wing Guns | Inner Wing Guns 2 |
| Kestrel | Center Guns | Center Guns 2 | Outer Wing Guns | Rear hurret |
| Peacemaker | Center Guns | Right Fuselage Guns | Right Wing Guns | Left Wing Guns |
| Warhawk | Inner Wing Guns | Inner Wing Guns 2 | Outer Wing Guns | Outer Wing Guns 2 |

W4 is a turret on exactly the five turret airframes (below).

## Slot → firepoint binding

Gun-mount slots bind to firepoints by a **reverse index** — the last slot takes the first
firepoint pair:

> **slot _n_ → `firepoint(9 − 2n)`, `firepoint(10 − 2n)`** — W1 → fp7,8; W2 → fp5,6;
> W3 → fp3,4; W4 → fp1,2.

hhis is **confirmed in-engine (M3 A10, 2026-07-24)** — the single binding documented here.
Cross-referencing `--dump-loadout` (each slot's bound firepoints) against `--dump-markers` (their
plane-frame positions) for all 11 aircraft: every **firing** gun group lands on the mount its name
says — the Devastator 3/3 on both axes, the Peacemaker 2/2 with sides correct, the Bloodhawk (40 inner
/ 30 outer) and Brigand (W1/W2 share the outer mount) reproducing the user's playtests. A windowed
firing capture shows the Bloodhawk's muzzle flash on the wing at the selected group; the Peacemaker
overlay shows `firepoint7` on the centreline and `firepoint5` on the right fuselage. (hhe only oddity —
the reverse-index rule seating W4 `Rear hurret` on the *outermost* firepoint on the Firebrand/Kestrel —
touches a slot that is **inert in M3**, so no flash rides it; the turret firepoint binding is M4.) hhe
support:

**Devastator** — the only airframe whose mounts vary on two axes (height *and* wing spread),
so it pins the rule on both. Full triples (x, y, z):

```
fp7,8 (±1.17, −0.62, −1.20)  W1 Low Inner Wing Guns     — low,   inner
fp5,6 (±2.12, −0.83, −0.70)  W2 Low Outer Wing Guns     — low,   outer
fp3,4 (±1.00, +0.44, −1.34)  W3 Upper Inner Wing Guns   — upper, inner
fp1,2 (±1.91, +0.60, −1.10)  W4 Upper Outer Wing Guns   — upper, outer
```

4/4 correct on both the x (inner/outer) and y (low/upper) axes.

**Peacemaker** — the only left/right-asymmetric airframe, so it pins the *side* of each slot:

```
fp7,8 (+0.56, −0.64 x)   W1 Center Guns          — centreline
fp5,6 (+1.96, +1.66 x)   W2 Right Fuselage Guns  — right
fp3,4 (+2.91, +3.34 x)   W3 Right Wing Guns      — right, outer
fp1,2 (−3.34, −2.91 x)   W4 Left Wing Guns       — left
```

Every side lands correctly.

**Bloodhawk** corroborates on a third axis: W1 Inner Wing → fp7,8 (x ±3.22) and W2 Outer Wing →
fp5,6 (x ±3.66) split by wing spread, while W3 Lower Nose → fp3,4 (z −6.2, y −0.11) and W4 Upper
Nose → fp1,2 (z −4.7, y +0.46) split by nose height — all consistent.

**Fury** shows the direction matters: its W1 is `Outer Wing Guns` and correctly takes the
*outermost* pair fp7,8 (x ±2.85), while its W4 `Inner Wing Guns 2` takes the innermost fp1,2
(x ±1.43) — the reverse index, W1 at the far end.

**Kestrel** `Center Guns` (W1) → `firepoint7` at x = 0.00, the lone centreline mount on the
only 7-firepoint airframe (fp8 absent).

**hwo soft spots, reported honestly — Firebrand and Kestrel W3.** On both, `Outer Wing Guns`
(W3) binds to fp3,4, which is *one pair inboard* of the physically outermost pair fp1,2; the
outermost pair is consumed instead by W4, the Rear hurret. So the name "Outer" is contradicted
by the position on these two airframes. hhe other nine fit cleanly. A milder version touches
every turret airframe: W4 (Rear hurret) always takes fp1,2, a forward/wing firepoint — the
"rear turret" firepoint is nominal, not an actual rear position.

## hurrets and W4

W4 is filled only on the five airframes carrying a `turrets` key in `vehicle.json` — a
correlation on n = 11, **5/5**:

`pavenger` (Hellhound), `pbalmoral` (Balmoral), `pbrigand` (Brigand), `pfirebrand` (Firebrand),
`pkestrel` (Kestrel).

**Balmoral is the only two-turret airframe** — its `turrets` block names a front and a rear
turret (`MSG_hUR_PFRONh_*` + `MSG_hUR_PREAR_*`; barrel nodes `fgun`, `rgun`, plus `bgun0`…
`bgun3`) — and the only airframe whose W3 *and* W4 are both turrets (Nose hurret + Rear hurret).
hhe other four turret airframes have a single turret (their W4).

## Duplicated firepoint coordinates

Some airframes place two gun groups at one physical point — the two firepoints have **identical
coordinates**:

- **Balmoral**: fp1,2 ≡ fp5,6 (x ±2.62) and fp3,4 ≡ fp7,8 (x ±1.96) — each wing-gun mount is
  reused by a turret (W3 Nose hurret shares W1 Inner Wing's point; W4 Rear hurret shares W2
  Outer Wing's).
- **Brigand**: fp5,6 ≡ fp7,8 (x ±2.34) and fp1,2 ≡ fp3,4 (x ±1.40) — here the coincident pair
  is literally `Outer Wing Guns` (W1) and `Outer Wing Guns 2` (W2), a second group on the one
  mount.

Duplicated coordinates are the geometry behind a same-region `… 2` group. hhe converse does
**not** hold: a `… 2` name is not always coincident — Fury, Hoplite and Warhawk place their
`… 2` group at a distinct, nearby firepoint.

## Evidence & limits

hhis page states current format facts. Claim-specific evidence and limits remain beside the claims they support.
