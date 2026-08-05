# Milestone 3 — Polish 8: Authored animations — play the defs

**ACTIVE PLAN** (written 2026-08-05). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

Twelve items, selected by the user from the 2026-08-05 hand-coded-vs-authored and
invented-behaviour audits under one criterion — **declared animations that are missing or wrongly
implemented in CSVM, and hand-coded behaviours whose authored counterpart exists** — plus the two
CamParams items (`BL-248`, `BL-260`) the user added 2026-08-05: authored camera *parameters*
rather than anim defs, but the same disease (shipped data we parse and don't drive with). Ten of
the backlog entries (`BL-119`, `BL-259`, `BL-261`–`BL-267`, `BL-270`) were filed or reframed
2026-08-05 from direct code+data reads in the same session, so they are trivially still-open;
`BL-248` (filed 2026-08-04 with CAP-21's measurements) and `BL-260` were re-checked open the same
day. None appears in `docs/HISTORY.md` as landed.

## Milestone goal

- Every particle effect renders at its **authored size** (the global 4× stand-in is gone; the
  config knobs remain for deliberate tuning).
- The gun's secondaries — muzzle smoke, muzzle flash, casing — are **exactly what `muzzle_burst`
  and `gunshell` author**: no invented eject-puff cluster, no invented flash triad.
- Wing lights, prop spin, water splash, and engine start/stop play **their defs** instead of
  hand-rolled duty cycles, rotate loops, and dropped fades.
- The player plane's damage stages are **the authored menu** — `player_fuelleak`, `pdpanelN`,
  `player_damage_trail` — with only the HP thresholds chosen by us.
- The chase camera breathes with speed and throttle by the **CAP-21-measured law**, and the
  authored special cameras exist — the crash camera at minimum, the rest as far as their units
  decode.

**No new feel laws get invented in this plan.** Where a def leaves a gap (a threshold, an energy,
a unit), the gap is named as TUNE in `backlog.md` — not filled with a guess. For the camera items
that rule bites hardest: `BL-260`'s undecoded field units mean the item may deliberately land
*partially*, with the un-decodable cameras recorded as capture-gated rather than guessed.
Excluded by the user's criterion: `BL-268`/`BL-269` (audio mix), `BL-271` (graze feel laws),
`BL-272` (precipitation calibration).

## Decisions (2026-08-05)

| # | Question | Decision |
|---|---|---|
| 1 | Selection criterion for the audit items | **Authored/declared animations missing or wrong in CSVM** — user's words: "declared animations that are not or wrongly in CSVM and hardcoded animations that should be authored ones. The ejector puffs too." |
| 2 | First item | **`Puffer.SizeScaleDefault` 4 → 1** — user-fixed opener; every later visual judgement is conditioned on it |
| 3 | Do the `puffer.*SizeScale` config keys survive the revert? | **Yes** — the keys stay as tuning knobs; only the *default* reverts to the authored 1× |
| 4 | Code changes before the plan? | **No** — a same-day attempt to land BL-261 directly was rolled back on user instruction; everything goes through this plan |
| 5 | Are the CamParams items in scope? | **Yes, added by the user 2026-08-05** — `BL-248` and `BL-260` join as C8/C9, ahead of camera shake; authored parameters count as "authored counterpart exists" |

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets a dated entry in `docs/HISTORY.md` and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — the gun line and the global baseline (`Projectile.cs`/`Puffer.cs`, sequential)

1. ☑ `BL-282` — `Puffer.SizeScaleDefault` 4 → 1; config knobs stay (landed 2026-08-05)
2. ☑ `BL-261` — render the authored `muzzlepuffer`, delete the eject-puff cluster (landed 2026-08-05; look-check → `PT-39`)
3. ◐ `BL-263` — muzzle flash: authored single-node roll + the unplayed `_muzzle2` flipbook frame (authored form landed as default 2026-08-05; the pick between forms is the user's → `PT-39`)

### Wave B — airframe fixtures

4. ☑ `BL-119` — play `wing_lights_blink`: the def's duty cycle, `LIGHT_STATE` point lights, LOD gate (landed 2026-08-05; follow-up → `BL-284`)
5. ☑ `BL-264` — prop/rotor spin through the runtime's `XYZ_ROTATION` semantics (landed 2026-08-05; units were already exact)

### Wave C — effect & camera choreography

6. ☐ `BL-265` — water splash: authored opacity fades + `splash01→03` flipbook; re-judge the 8× width
7. ☐ `BL-267` — engine start/stop: play `startprops`/`stopprops`; throttle-slam smoke wired magnitude-gated (`CAP-21` re-read)
8. ☑ `BL-248` — dynamic chase distance: implement the two CAP-21-measured terms (landed 2026-08-05)
9. ☑ `BL-260` — the four authored cameras: crash first; un-decodable ones recorded, not guessed (crash + look-behind landed 2026-08-05; death/flyby capture-gated on the entry)
10. ☑ `BL-266` — camera shake: find the authored law (`shakes.zrd.json`/`damage_shakes.zrd.json`), then implement or re-scope (re-scoped 2026-08-05: laws authored, inputs not — decode landed as `docs/formats/shakes.md`, no code)

### Wave D — the damage menu (after PLAN-effect-catalogue's runtime capability)

11. ☐ `BL-259` — play the authored damage animations: `player_fuelleak` / `pdpanelN` / `player_damage_trail`
12. ☐ `BL-270` — healthy↔torn panel pairing from the defs, not AABB proximity

## Dependency and parallelism notes

**A1 lands first and alone** — it changes the rendered size of every effect in the game, so every
later "does this look right" judgement in Waves A–D is conditioned on it. A1→A2→A3 then run
sequentially: all three edit `Projectile.cs`/`Puffer.cs`. Waves B and C are independent of each
other and of Wave A (different files) but every visual verify should happen post-A1.

**D11/D12 are gated on `PLAN-effect-catalogue`** (in flight in a concurrent session as of
2026-08-05, items B2/B3 landed): they need the runtime to `CALL_ANIMATION` into an `ON_CALL` def
with an `INPUT_NODE` binding. **Do not start Wave D while that plan has uncommitted work in
`AnimRuntime.cs` / the effect-template pool** — coordinate via the user. D12 sequences after D11
(same defs, same wiring). Within Wave C, C8→C9 run in order (both edit
`CameraController.cs`/`CamParams.cs`, and C9's crash camera should be judged with C8's dynamic
distance already in). File contention within this plan: A-wave items share `Projectile.cs`;
C8/C9/C10 share the camera files; D-wave items share
`DamageVisuals.cs`/`FlightRigAssembler.cs` — never in parallel worktrees.

---

# Wave A — the gun line and the global baseline

## A1 ☐ `BL-282` — `Puffer.SizeScaleDefault` 4 → 1; config knobs stay

**Goal.** Every `PUFFER_STATE` renders at its authored `SIZE_RANGE`. The three `puffer.*SizeScale`
config keys remain, defaulting to 1, for deliberate tuning.

**Evidence (confidence: traced).** `Puffer.cs:281` — `public const float SizeScaleDefault = 4f`,
commented "a judged stand-in for a missing engine constant, not a decode … settled at the controls
(TUNE, 2026-08-01)". Applied per emission mode at `Puffer.cs:700,721,757` via
`_burst/_trail/_sustainSizeScale`, read from config at `:654-656` (`Config.cs:231-233` registers
the keys). Every authored effect — crash fireball, trails, splash puffers, gunhit — passes through
it.

**Approach.** Change the const to `1f`; nothing else. The config keys and their plumbing stay
untouched (Decision 3). Do not compensate individual effects in the same change — the point is to
see the authored sizes plainly, then tune per effect via config only where the user judges it.

**Model recommendation.** medium, low effort — a one-line change; the work is in the verify.

**Verify.** Take BEFORE captures at 4× first (an unchanged baseline you've seen able to fail):
the crash fireball (`--crash` pose), a sustained gunhit into dirt, and the touchdown scrape puff.
Repeat at 1×, same poses, and hand the pairs to the user for the A/B — expect everything to read
markedly smaller; that is the intended outcome, not a regression. Then the standard 8-chapter
`--freecam` regression (zero errors; counts unchanged — this change cannot alter geometry).

**Playtest confirmation 2026-08-05 (`PT-24`, closed into this item).** The user judged 4× at the
controls and reported it **too big** — and, importantly, that "the multiplier is different for
different puffers", i.e. no single global number serves the graze smoke, the rocket trails and the
30 s destruction fire at once. That is the case for this revert stated from the cockpit rather than
from the data, and it also raises the follow-on this item should answer while it is open: **check
whether the gamez host nodes carry a per-puffer scale we are ignoring** before reaching for
per-site TUNE constants — the same pattern that turned up `fogvol.zrd` (`BL-273`) and `BL-259` in
the same sitting. Still owed on the sparks half: an A/B against `playtest/CAP-14`'s building graze
and crash footage, which the playtest did not get to.

**⚠ Traps.** (a) `FlightController.cs:1744` — the graze reaction's contact-point staging leans on
the 4× scale to lift the touchdown puffer's authored −0.5 Y offset clear of the struck surface; at
1× those puffs may emit half a metre UNDER the ground. If they vanish, that is this known
interaction, not a new bug — the recorded alternative is `graze.siteAtContact=false` (stage on the
aircraft, which the data argues for anyway). (b) Do not "fix" small-looking effects by editing
their defs — the defs are authored truth; if 1× is genuinely wrong game-wide the answer is one
config value, and `BL-282`'s backlog text names the capture that would prove it. (c) 2026-08-01's
"settled at the controls" judgement was made *with* the invented eject-puff cluster and 0-count
muzzle smoke on screen — the world it tuned against no longer exists once A2 lands; judge fresh.

**Outcome (2026-08-05) — landed as scoped.** The const is 1; the knobs and every individual
effect untouched. The follow-on lead is **disproven**: the gamez node `scale` field is unit on 0
of 4,181 nodes (`GameZ.cs`'s own audit), so no per-puffer host scale is being ignored — the
"different multiplier per puffer" read is the authored per-effect `SIZE_RANGE`s. Five goldens
re-pinned (`c5-city-night` was a fifth mover the architecture note had missed). Full
`RunTests.ps1` green on the merged main; A/B pairs (crash fireball, gunhit-into-dirt, touchdown
scrape, 4× vs 1×) staged in `.scratch/plan8/A1/` for the user. The touchdown-puff trap (⚠a) did
not manifest. Still owed: the sparks A/B against `CAP-14`'s footage.

## A2 ☐ `BL-261` — render the authored `muzzlepuffer`, delete the eject-puff cluster

**Goal.** Gun smoke is `muzzle_burst`'s `muzzlepuffer` playing as authored; the casing flies bare,
as `gunshell` authors it. The invented 5-puff eject cluster is gone.

**Evidence (confidence: traced).** `extracted/zrdr/gunshell.zrd.json` is motion-only —
`OBJECT_MOTION` (±10° bearing, −75…−85° elevation, 1.5–1.8 m/s, 1200 °/s tumble, `RUN_TIME` 2 s)
then deactivate; **no `PUFFER_STATE`** (read 2026-08-05). `Projectile.cs:119` has the authored
muzzle smoke transcribed verbatim but `MuzzleSmokePuffs = 0`; `:126-135` + `SpawnCasing`'s loop
spawn the invented cluster whose own comment admits it "matched no shipped def". User-confirmed
reading: the white puffs near falling brass in the captures are muzzle smoke misattributed.

**Approach.** Set the muzzle smoke to the authored window (6 puffs / 0.3 s — the per-shot count is
the gloss of `TIME_INTERVAL 0.05` over the window; keep the transcribed ranges as-is), delete the
`EjectPuffs` constant block, `EjectPuffTint`, and the cluster loop in `SpawnCasing`, and update the
comments that mention the cluster (`_smoke` field doc, the `AddMultiMesh("smoke101", …)` comment,
the C22 secondaries comment). The pooled casing path itself is already faithful — do not touch it.
A reverted 2026-08-05 attempt at exactly this change exists in this conversation's history; the
shape is known-good, it was rolled back only because the user wants it landed through this plan.

**Model recommendation.** medium, low effort — mechanical deletion plus one constant; the verify is
a look-check.

**Verify.** Sustained gun fire in chase view: smoke sits on the gun line and drifts aft; casings
tumble bare. A/B against the original's gun-fire captures (`C1B IA1 Bloodhawk tracer and
ejection.png` and its sibling show the reference). 8-chapter freecam regression unchanged.

**⚠ Traps.** (a) Run after A1 — at 4× the six authored 0.3–0.6 m puffs read as a wall of smoke and
the judgement will be wrong. (b) `MaxSmoke = 256` was sized "the persisting eject puffs dominate
this pool"; the muzzlepuffer's 0.1–0.2 s lives need far less — leave the cap alone, it is a cap.
(c) The authored emission is 6 puffs *over a 0.3 s window from the moving muzzle node*; the
per-shot instant spawn is an accepted gloss (sustained fire fills the streak) — do not build a
windowed emitter here; if the single-shot look bothers the user, that is a named TUNE, not a bug.

## A3 ☐ `BL-263` — muzzle flash: the authored single-node roll AND the unplayed second flipbook frame

**Goal.** The muzzle flash is the authored form: one quad rolled to the def's discrete random
angle per shot, playing the ammo's **two-frame** `_muzzle1`→`_muzzle2` flipbook — or, if the
at-the-controls A/B says the authored form doesn't reproduce the reference stills, the triad
stays with the reason recorded.

**Evidence (confidence: direction-sound, outcome a judgement).** Two authored mechanisms are
currently not played: (1) `muzzle_burst.zrd.json` authors ONE `mb_spinflame` node with
`RANDOM_WEIGHT` over 30/80/140° of Z roll per shot, inside the per-ammo
`muzzle_burst_{slug,dum,ap,mag}` wrapper the weapon's `FIRE` binding selects;
`Projectile.cs:98-104` draws three quads 120° apart sharing one continuous roll instead. (2) The
flash texture is a **two-frame flipbook per ammo** — `{slug,dum,ap,mag}_muzzle1`/`_muzzle2`
(`docs/formats/weapon-effects.md` "Muzzle & tracer textures") — and the C24 wiring draws only the
`_muzzle1` frame; `_muzzle2` is never played by the flash (it got reused as an unrelated
impact-spark texture, predating that wiring). The triad was justified by the 3-lobed reading of
`MuzzleFlash1-3.png` — but those lobes may be the texture art plus the frame flip, in which case
the authored single node already produces them and the triad triples it.

**Approach.** Implement the authored form behind the existing constants (`MuzzleFlashCount = 1` +
a discrete 3-bucket roll + the 1→2 frame flip over `MuzzleLife`), capture both forms at the same
pose against `MuzzleFlash1-3.png`, and let the user pick at the controls. Whichever loses is
deleted, not flagged off. The frame flip is worth testing *with* the single node before judging —
it may be the missing ingredient that made one quad look wrong in the first place.

**Model recommendation.** medium — small code, but the A/B framing has to be honest (same ammo
type, same pose, freeze-frame).

**Verify.** Freeze-frame captures (pause + `.` step) of single shots, both forms, against the
three reference stills — including at least one non-slug ammo so the per-ammo axis is seen
working; then sustained fire for the in-motion read. No regression surface beyond the gun line.

**⚠ Traps.** (a) The per-ammo *selection* axis (`MuzzleAmmoIndex` from the `FIRE` binding,
unsuffixed `muzzle_burst`/`muzzle_burst2` defaulting to slug) is already correct — don't touch it
while changing the shape. (b) The impact stand-in spark currently reuses `_muzzle2` as a generic
bright-flash texture; giving `_muzzle2` back to the flash must not restyle the impact spark in
the same change — if the shared use grates, that's a separate note on `BL-263`. (c) Run post-A1
like everything visual.

**A2 Outcome (2026-08-05) — landed.** Defs re-confirmed first (`gunshell` motion-only; the
`muzzlepuffer` window is exactly 6 × 0.05 s intervals over 0.3 s). Cluster deleted, smoke on,
casing untouched, goldens unchanged. Look-check rides `PT-39`.

**A3 Outcome (2026-08-05) — authored form landed as default; pick pending.** Discrete 3-bucket
roll + `_muzzle1`→`_muzzle2` flip over an even half-life split (declared gloss); frame 2 has its
own MultiMesh pool so the impact spark's `slug_muzzle2` reuse is untouched; triad reachable via
`MuzzleFlashCount = 3`. Neither form fully reproduces the stills' multi-lobed burst in
freeze-frame — the pick (and the loser's deletion) is the user's at the controls, `PT-39`.

# Wave B — airframe fixtures

## B4 ☐ `BL-119` — play `wing_lights_blink`: the def's duty cycle, point lights, LOD gate

**Goal.** The wingtip lights blink by the authored def: `RESET_STATE` off, the `blink_lights`
sequence looping at the authored 1.5 s, the two `LIGHT_STATE` point lights emitted alongside the
flare sprites, gated by `ANIMATION_LOD` HIGH.

**Evidence (confidence: traced, one named TUNE remains).** `wing_light.zrd.json` (read
2026-08-05): per-plane `ON_CALL` defs; `LIGHT_STATE`s at `wing_flare1/2`, range 0.5–1.25 m, colour
0.88/0.78/0.36; an `ANIMATION_LOD HIGH` gate with an explicit else-branch turning the flares off.
`WingLightBlinker.cs:19` hand-widens the authored one-frame ON window to 0.08 s (`FlashDuration`,
the known TUNE — 0.0001 s is imperceptible); `WingLights.cs:18-20` deliberately drops the point
lights; `PlaneBuilder.cs:204-249` re-skins the one-sided flare quad as an additive both-sides
billboard.

**Approach.** Drive the blink from the def's sequence timing rather than the re-implemented duty
cycle, emit the two `OmniLight3D`s with the authored range/colour, and honour the LOD gate (map it
to a config/quality flag; the else-branch means OFF is the authored low-LOD behaviour, not
"always blink"). Keep `FlashDuration` as the one declared widening (it stays a TUNE — the authored
literal is unusable). Leave the billboard re-skin in place but record it on the entry: the
original may show the flare only from behind, and the backlog names the one clip (orbit a lit
plane) that settles it — don't churn the material twice.

**Model recommendation.** medium — runtime wiring with a settled def in hand.

**Verify.** Night-ish pose, chase orbit: flares + point lights blink together at 1.5 s with the
0.08 s window; low-LOD path shows them dark. 8-chapter freecam regression (AI planes carry the
same defs — watch counts stay flat).

**⚠ Traps.** `BL-119`'s entry documents that the 0.0001 s ON literal is unusable as-is — do not
"faithfully" implement it and ship invisible lights; the widening is deliberate and stays. The
point lights were skipped as "negligible at chase distance" — that judgement predates the 1×
revert and the A/B should re-test it, not inherit it.

**Playtest evidence 2026-08-05 (`PT-03`, closed into `BL-119`).** Two measurements from the
original, staged as an A/B still in `playtest/PT-03/Light Original New.png` (original above, ours
below). (a) **The flash is twice as long as it should be:** at 30 fps the original's light occupies
**one** frame, ours **two** — `FlashDuration` 0.08 s ≈ 2 frames vs the original's ~0.033 s. This is
the first measured bound on the widening, so the "keep 0.08 s as the declared TUNE" line above
should be revisited: ~1 frame is what the footage supports. (b) **The flare renders with the wrong
orientation** — ours draws a large flat yellow wedge at the wing where the original shows a compact
camera-facing star burst, which is the `PlaneBuilder.cs:204-249` both-sides re-skin showing itself.
The billboard deviation now has a screenshot against it rather than being a suspicion, so the
authored one-sided quad is the thing to restore.

## B5 ☐ `BL-264` — prop/rotor spin through the runtime's `XYZ_ROTATION` semantics

**Goal.** Props and rotors spin with the authored `XYZ_ROTATION` values under the runtime's
settled unit semantics — the hand `RotateObjectLocal` loop and its "degrees/second" guess are gone.

**Evidence (confidence: traced values, unit via the runtime).** `PropParts.cs:25-28` carries the
authored triples (`plane_props.json` `spinprops`, `autogyro.json` `agyro_rotors`) with "source
units are undecoded; we treat them as degrees/second (a visual TUNE)". `AnimRuntime`'s
`SpinMotion.cs` already decodes `XYZ_ROTATION` events with settled semantics — two implementations
of one authored concept, and only the hand one guesses.

**Approach.** Resolve the spin through the same decode `SpinMotion` uses (share the code path or
the conversion, whichever `docs/architecture.md`'s AnimRuntime entry sanctions — read it first;
PLAN-effect-catalogue is actively refactoring nearby). Keep the throttle/windmill modulation
(`PropIdleSpin`, the ghost-disc swap) as the engine-side part layered on top, exactly as `BL-259`
keeps thresholds engine-side.

**Model recommendation.** medium — the risk is coordination with the concurrent plan, not the code.

**Verify.** Side-by-side of a Bloodhawk at idle and full throttle vs the current build (the rate
should only change if the runtime's unit semantics disagree with the deg/s guess — if it changes,
that IS the finding; record the factor). Autogyro rotor same. Freecam regression for the AI/parked
planes.

**⚠ Traps.** (a) **File contention with PLAN-effect-catalogue** — it is landing changes around
`AnimRuntime` right now; check with the user before touching shared files. (b) If the runtime's
semantics produce an absurd rate, the answer is a disproof recorded on `BL-264` ("the props do NOT
use XYZ_ROTATION semantics"), not a compensating constant.

**B4 Outcome (2026-08-05) — landed.** Def-driven flash (~2 sim ticks, PT-03's measured bound),
`OmniLight3D`s at the authored range/colour (visible at chase distance — the "negligible"
judgement was stale), `ANIMATION_LOD` gate on `SessionSpec.AnimLod`, authored one-sided quad
restored. Corrected en route: only `piratefighter`/`brigand` carry the def; the Bloodhawk has no
flare nodes. No goldens moved. Residual star-burst/view-dependence question → `BL-284`.

**B5 Outcome (2026-08-05) — landed; the premise was disproven.** The deg/s constants match the
authored triples bit-for-bit and the conversion already equalled `AnimDefs.Spin`'s — no rate
change anywhere. The real gain: `PropAnimator` now composes absolute poses from rest via the
extracted `SpinMotion.ComposeSpin` (drift-free) instead of integrating `RotateObjectLocal`.
`empty-stage`/`c1-destroy-effects` re-pinned (1–3 px prop-hub, integration method only).

# Wave C — effect & camera choreography

## C6 ☐ `BL-265` — water splash: authored fades + flipbook; re-judge the 8× width

**Goal.** The gun-round water splash plays its authored 0.05 s opacity fade-in / 1 s fade-out and
the `splash01→03` `OBJECT_CYCLE_TEXTURE` flipbook; the 8× column widening is re-judged against the
reference once the fades exist.

**Evidence (confidence: traced).** `Projectile.cs:173-189`: values "verbatim from the defs"
(`splash1.zrd.json`/`bsplsh.zrd.json`) except the fades and flipbook are unimplemented
(comment at `:177-179`) and `SplashColumnWidthScale = 8f` was judged against `Water Splash.png` —
plausibly *because* the missing fade left the thin authored column reading as "an invisible grey
sliver".

**Approach.** Implement the fade envelope and the flipbook (reuse `TextureCycler`'s frame-list
machinery if it fits — read its architecture entry). Then capture the column at 1× width against
`Water Splash.png` and let the user set the width knob; the 8× only survives as a config-visible
TUNE if the authored width still reads wrong *with* fades.

**Model recommendation.** medium.

**Verify.** Slug fire into C1B water at the reference pose vs `Water Splash.png`; the fade-in
should kill the current pop-in. Freecam regression.

**⚠ Traps.** Post-A1 judging only — the splash puffers also lose their 4×. Don't fold the rocket
water column (`SplashColumnScale 100`, a different constant with its own justification) into this
item; it is not part of the finding.

## C7 ☐ `BL-267` — engine start/stop: the authored cues wired

**Goal.** Engine death plays `snd_propstop`; engine start's loop ramp is sourced or named as TUNE;
`engine_start_smoke` (the authored puff of the start moment) is played if the data binds it.

**Evidence (confidence: traced for the choreography; the trigger is the open half).** The
authored start/stop choreography exists and is named (`plane_props.zrd.json`, read 2026-08-05):
**`startprops`** — `snd_propstart`, each static blade prop cross-fades to its spinning prop
(`OBJECT_OPACITY_FROM_TO`, `RUN_TIME 2.0`), and a `smokepuff1`–`3` burst per engine nacelle —
with a matching **`stopprops`** (same file, ~line 1233). `engine_start_smoke`
(`pufftrails.zrd.json`) is the generic 0.5 s `ON_CALL` variant at any `INPUT_NODE`. Also
authored in the same file: `nitro_boost`/`nitro_decay` — `snd_nitrostart`, the `nitropropN`
discs fading in over 1 s, and `exhaust1`–`4` trail puffers streaming from the exhaust nodes
while boosting — the only *sustained* thrust-linked smoke in the corpus. Engine side:
`FlightAudio.cs:317-325` — `OnEngineStop()` fully implemented, zero production callers; `:51` —
`EngineStartRamp = 1.8f` bare literal (the `startprops` 2.0 s prop cross-fade is the nearest
authored duration — check whether the ramp should be it). **The throttle-step half is settled
by the `CAP-21` re-read (2026-08-05, decode in `docs/HISTORY.md`):** the original streams dark
trail-style exhaust smoke from the cowling sides for ~2–3 wall-s (×1.390 for sim-s) on *large*
throttle jumps only — idle→8/8 and idle→5/8 fire it, onset ≤0.5 s; all fifteen single-1/8
steps, every throttle cut, and sustained 8/8 show nothing. The shape is the `exhaust1`–`4`
stream (`nitro_boost` morphology, no nitro involved), not the round `smokepuffN` cough — so the
trigger is magnitude-gated (or proportional to the RPM ramp), not a per-step call.

**Approach.** Play `startprops`/`stopprops` as authored at spawn and engine-death (crash,
destruction, wreck), wiring `OnEngineStop` in the same change; source the audio ramp from the
authored 2.0 s if the listen A/B agrees. Wire the throttle-slam smoke magnitude-gated per the
capture: a jump of several notches at once streams the exhaust-node puffers for ~2–3 s, a
single-notch step plays nothing (and never fire it on throttle decrease). Acceptance is the
observed cadence: idle→8/8 slam → dark aft-streaming smoke visible within 0.5 s, gone by ~3 s;
stepping 1/8 at a time up the whole range → no visible smoke.

**Model recommendation.** medium — half investigation, half wiring.

**Verify.** A crash and a scripted destruction both end with the wind-down cue instead of a cut
loop; spawn plays the start smoke if wired. Listen A/B is the user's; the regression surface is
`RunTests.ps1` (FlightAudio has suite coverage) + freecam.

**⚠ Traps.** The ×0.2 "Temporary fix" volume scale (`BL-268`, deliberately out of this plan) sits
on these same paths — do not "fix the mix" while wiring the cue; if the stop cue sounds wrong at
×0.2, note it on `BL-268` and move on.

## C8 ☐ `BL-248` — dynamic chase distance: implement the two CAP-21-measured terms

**Goal.** The chase camera's distance is dynamic as the original's is: the per-plane authored
`dist` grows with speed by the `dist_factor` law, and throttle transients pull a lag that relaxes
at the measured rate.

**Evidence (confidence: measured — the strongest in this plan).** `BL-248`'s entry carries the
full 2026-08-04 decode from four CAP-21 Bloodhawk staircase clips: the speed term is real and is
`dist_factor` — `d(V)/d(0) = 1 + 5.65e-4·V(m/s)`, implying `dist_factor` **0.0105** against the
shipped **0.01** (5% agreement); the acceleration transient relaxes at **0.65 /sim-s**. `BL-149`
already landed the reader and the fixed per-plane `dist` (`CamParams`/`CameraController`); only
the two dynamic terms are unwired.

**Approach.** Wire the speed term from the airframe's own `dist_factor` (authored, per plane) and
the transient as a first-order lag at the measured 0.65 /sim-s — cited as *measured*, not
authored (the entry shows it matches neither `pos_catch_up` nor `look_catch_up`). Read the full
`BL-248` entry before starting; its traps section is the map.

**Model recommendation.** medium — the law is handed over; the risk is unit discipline.

**Verify.** Reproduce CAP-21's own numbers in our build: level plateaux at ~118 and ~297 mph
should show ~4.1% apparent-size change; a full-throttle slam shows the transient relaxing at the
measured rate in sim-time. `docs/verification.md` first — the ~1% clip-to-clip systematic in the
entry bounds what a match can claim.

**⚠ Traps.** All inherited from `BL-248`'s entry, restated: (a) `dist_min`/`dist_max` is **not**
a clamp — the default block's own `dist` 13.0 sits outside its min/max, and CAP-21 never reaches
`dist_max`; do not wire a clamp. (b) 0.65 /sim-s is a **wall→sim converted** measurement
(×1.390) — apply it in sim seconds or it runs 39% slow, the exact BL-148 trap. (c) The chase
radius is shared with the numpad fixed views **by design** — the dynamic terms moving both
cameras is intended; do not give the views their own copy. (d) `thirdp_pitch` ≈ our hand-picked
elevation stays a suggestive near-match, not a decode — out of scope here.

## C9 ☐ `BL-260` — the four authored cameras: crash first; un-decodable ones recorded, not guessed

**Goal.** The authored special cameras (`camparam.zrd.json`: death, crash, flyby, look-behind)
replace the ordinary chase camera at their moments — starting with the crash camera, whose scene
already exists. Cameras whose field units cannot be decoded without original footage land as
*recorded gaps*, not invented laws (plan boundary).

**Evidence (confidence: traced fields; units lead-only).** `CamParams.cs:60-84` parses all four
blocks and is deliberately dormant: death (`DeathInterval`/`DeathZ`/`DeathX`/`DeathAlt`/
`DeathMinAlt`), crash (`CrashHoriz`/`CrashY`/`CrashChordY`/`CrashElev`), look-behind
(`BackDistMin`/`BackDistMax`), flyby (12 fields). Today a death or crash plays out on the chase
camera and no look-behind binding exists at all. Filed as `BL-260` (2026-08-05 audit).

**Approach.** Crash camera first — the data-driven crash rig is the existing scene missing its
authored framing; its four fields are the smallest decode. Then look-behind (two fields plus a
key binding to add). Death and flyby are attempted only as far as their units decode from
geometry/plausibility checks; where they don't, the item records "capture-gated" per camera on
`BL-260` and stops — that partial landing is the intended shape, not a failure.

**Model recommendation.** high — unit decoding with no reference is judgement-heavy, and the
discipline to *stop* rather than guess is the point of the item.

**Verify.** A terrain crash cuts to the authored crash framing (A/B against `C1 IA1 Crash.mp4` /
`Crash 2.mp4` in `OriginalScreenshots\Videos\` — original crash footage already on disk); the
look-behind key shows the authored back view at `BackDistMin`–`Max`. Regression: the ordinary
chase camera unchanged when no special camera is active, numpad views unaffected.

**⚠ Traps.** (a) The `BL-248`(d) rule generalises: a near-match between an authored field and a
hand-picked value is suggestive, never a decode — wire nothing on the strength of one
coincidence. (b) `C1 IA1 Crash.mp4` was analysed for CAP-14's collision physics (HISTORY
2026-08-04) — re-read it for *camera framing* this time; the physics read does not answer this.
(c) Sequenced after C8: judge the crash camera with the dynamic chase distance already landed,
or its framing will be re-judged twice.

## C10 ☐ `BL-266` — camera shake: find the authored law, then implement or re-scope

**Goal.** Either weapons flagged `SHAKES_CAMERA` shake the camera by an authored law found in the
data, or the item is re-scoped with the finding "only the flag is authored" and the law named as
TUNE on `BL-266`.

**Evidence (confidence: lead-only).** `WeaponDefs.cs:103,258` parses the flag; `Probes.cs:151`
dumps it; zero consumers. `shakes.zrd.json` and `damage_shakes.zrd.json` exist in
`extracted/zrdr/` and have never been read — the law (amplitude/frequency/decay, possibly a
`cockpit_bulletholes`-adjacent damage shake too) may be authored there.

**Approach.** Read both files first — this item's shape depends entirely on what they hold. If a
law is authored: implement it on the flagged weapons' fire path. If not: implement nothing,
record the disproof, and downgrade `BL-266` to a TUNE-gated item awaiting original footage of the
heaviest flagged weapon.

**Model recommendation.** medium — investigation-first; escalate only if the decode is rich.

**Verify.** If implemented: fire the heaviest `SHAKES_CAMERA` weapon, chase and cockpit-ish views,
and A/B the feel against original footage if any exists. `--incoming` and scripted runs must stay
shake-free unless their weapons carry the flag.

**⚠ Traps.** A shake law has a screenshot-instrument interaction: scripted screenshot verification
poses will jitter — take stills with the shake settled or gate shake off under `--screenshot`,
and say so in the item's landing notes.

**C8 Outcome (2026-08-05) — landed, measured against CAP-21's own numbers.** `d = dist +
dist_factor·V` + the 0.65 /sim-s transient, no clamp, numpad views share the radius. The follower
now smooths the offset, not the world position — the CAP-21 entry's predicted `V/CamSmooth` trail
bug is gone with it. In-build reproduction: plateaux exactly on the law; span 5.7% vs predicted
4.6% (inside the ~1% clip systematic); decay fit 0.651 /sim-s. Four flown-plane goldens re-pinned.

**C9 Outcome (2026-08-05) — landed partially, as intended.** Crash camera (30 m behind / 45 m up,
hard cut, HUD hidden — decoded off the two crash clips) and look-behind (numpad 0 / `--view=back`,
radius bounded into `back_dist_min/max`; the Kestrel proves the min bites) landed. `crash_elev`/
`crash_chord_y`, death and flyby recorded capture-gated on `BL-260` — nothing guessed. `--view=6`
pixmd5-identical pre/post; only `c1-crash` moved.

**C10 Outcome (2026-08-05) — re-scoped, no code; the discipline held.** The laws are authored
(six oscillator sources + `ON_CALL` shake defs — first read of both files); `SHAKES_CAMERA` is a
scripted marker on one zero-damage fake weapon, not the fire path. Every wire-up crosses an
unauthored input, so the decode landed as `docs/formats/shakes.md` and the gaps are named on
`BL-266`.

# Wave D — the damage menu

## D11 ☐ `BL-259` — play the authored damage animations

**Goal.** The player plane's visible damage progression is the authored menu playing as data:
`player_fuelleak` (partial damage — the vapor leak we currently render nothing for), `pdpanelN`
(torn panel + `gimmeflakes` debris + `short_firetrail` at the panel; `pdpanel3` uses
`loop_short_firetrail`), `player_damage_trail` (`short_firetrail` at `prop1` + the `fire_lt` nose
light). Only the HP thresholds that *call* each stage are ours.

**Evidence (confidence: traced; thresholds are the named gap).** `BL-259`'s entry (filed
2026-08-05) carries the full decode: `player-1.zrd.json`'s `ON_CALL` defs, the two corrections to
current assumptions (heavy trail authored at **`prop1`**, and it is `short_firetrail`, **not** the
`dense_firetrail` pair `FlightRigAssembler.cs:241-249` wires — nothing in the player defs calls
`dense_firetrail`), and CAP-15's footage (`playtest/CAP-15/`, HISTORY 2026-08-05) as the timing
reference: the burn is `short_firetrail` verbatim, staged 4/6/8 s, sputtering past 31 wall-s.

**Approach.** Replace the four bare `firepuffer`s (`FlightRigAssembler.cs:246-248`) and the
`dense_firetrail` pair with `CALL_ANIMATION` invocations of the authored defs through the effect
runtime landed by PLAN-effect-catalogue, bound `WITH_NODE` to the real `pdpN`/`prop1` nodes.
Thresholds: start from the damage-gauge tiers the `*_damage_yellow/red` defs key to (the tier
flips are themselves authored — `OBJECT_CYCLE_TEXTURE` on the gauge silhouette) and CAP-15's
timeline; every chosen number is recorded as TUNE on the entry. Before deleting the
`dense_firetrail` wiring, grep the full def corpus for who *does* call it — the entry demands that
check.

**Model recommendation.** high — highest blast radius in the plan: choreography, thresholds, and
the deletion of a shipped behaviour, judged against reference footage.

**Verify.** The F5 damage lab drives each stage on demand: leak at the partial tier, panel burn
matching CAP-15's staging (fire ~12 wall-s → black smoke → sputter), heavy stage at `prop1` with
the light. A real graze reproduces the CAP-15 look end-to-end. `trail-world-anchor` suite +
`damage-hd` suite + freecam regression.

**⚠ Traps.** (a) **Gated on PLAN-effect-catalogue's `CALL_ANIMATION`-with-`INPUT_NODE` capability
— confirm it has landed and the concurrent session is not mid-refactor before starting.** (b)
`BL-246` stays open and separate: *when* the heavy stage fires organically is a reachability/
design question this item must not solve by inventing generous thresholds. (c) The smoke/fire
trail's `TopLevel` anchor bug history (HISTORY 2026-08-03) — verify at a real mission spawn and
heading, not the identity pose; that trap has bitten twice. (d) CAP-15 timing is wall-clock ×1.390
sim — use sim seconds when comparing def offsets, the same trap BL-148 documents.

## D12 ☐ `BL-270` — panel pairing from the defs, not AABB proximity

**Goal.** The healthy↔torn skin mapping is derived from the authored data (`pdpanelN`'s named
`pdpN` targets, `player_destruct_reset`'s re-ACTIVE list) instead of the mesh-AABB proximity guess.

**Evidence (confidence: traced defs; guess admitted in code).** `DamageVisuals.cs:22-27,57,60` —
`MaxPairDistance = 1.0`, `MirrorMinX = 0.15`, comment conceding the original pairs "by some rule
of its own". The defs name the relationship explicitly; no geometry needed.

**Approach.** Build the pairing table from the defs at rig-assembly time (D11's wiring already
resolves the same nodes — reuse it); keep the geometric pairing as a logged fallback for any plane
whose defs miss a panel, and log loudly when it engages. Delete the two constants if nothing falls
back across all 11 aircraft.

**Model recommendation.** medium — mechanical once D11's node resolution exists.

**Verify.** All 11 aircraft through the F5 lab: every panel flips its own skin, no mirror-side
mispair (the failure the AABB guess risked). Freecam + `damage-hd` regression.

**⚠ Traps.** Sequenced strictly after D11 — same files, same def wiring; doing it first builds the
table twice. The fallback must log, not silently engage, or a def gap on one plane hides for
months.
