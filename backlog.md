# Backlog — unscheduled future work

Everything known-but-not-scheduled, so it survives between polish runs. The **active** plan is
`docs/PLAN-docs-cleanup.md`; completed plans are in `docs/plans/`. Per-item history/diagnosis
detail is in `docs/HISTORY.md` (dated entries) and `docs/architecture.md` (module bullets); how to
verify a change without fooling yourself is `docs/verification.md`. **The live list of hand-tuned
constants awaiting playtest lives here** (see "TUNE constants pending playtest" below) — it moved
out of CLAUDE.md on 2026-07-22, since CLAUDE.md's status section is current-state-and-next-step
only. When an item gets scheduled into a plan, move it there; when it lands, delete it here.

## Blocked / deferred

- ~~**Animated world vehicles**~~ — **LANDED 2026-07-21** (`docs/PLAN-anim-playback.md`, revival-plan
  item 7): the C1 train drives its SI-script track loop, the road vehicles run their
  `OBJECT_MOTION_FROM_TO` chains and the hangar doors swing, via the generic `AnimRuntime`.
  `PufferState` landed the same day (the train's steam plume, waterfall mist — user-confirmed
  in-game), including two follow-up bugs found and fixed the same day: a reader-def dedupe gap
  that let a duplicate `waterfall01` instance re-kill the splash puffers every frame, and the
  `AT_NODE` spread offset being parsed nowhere (silently dropped on 862 of 4387 PUFFER_STATE
  events install-wide) — the mist sat on one point instead of spreading across the falls until
  fixed. **Everything else this entry originally listed (the remaining event kinds, `If`/
  `Elseif` evaluation, mission-spawned entity rosters, `texture_scroll`) is now scheduled in
  `docs/PLAN-anim-rendering-followups.md`** (2026-07-21, 4 independent session-sized items with
  goal/evidence/approach/verify each) — see that plan rather than this entry for current detail.
- **Burning-object fires (`fire1`/`fire2` templates + `EFFECTS` flipbooks)** — **POSTPONED
  2026-07-21 by user decision: minor detail, and the trigger is not findable.** Fully decoded,
  so nothing needs re-deriving; what is missing is *when* to start a fire, not how. Blocked on
  a decision, not on data. Decode in `docs/formats/anim-definitions.md` ("Fire: templates,
  flipbooks, and a trigger that lives in the exe"):
  - **Templates:** `fire1`/`fire2` are real single-poly `Facade`/`CylindricalY` meshes under the
    **parentless roots** `fire1.flt`/`fire2.flt` (C1 nodes 493–496), which `WorldBuilder` never
    builds (it builds only World children + partition-referenced subtrees). Same for the other
    effect roots (`large_firetrail`, `short_firetrail`, `lg_fireball`, … ~gamez idx 74–150).
  - **Flipbook:** `effects.zrd.json` gives `fire1` 12 maps @ 10 fps, `fire2` 6 @ 5 fps, resolved
    **by filename from the texture archive** — `textures.json` registers only `fire101`/`fire102`
    while `extracted/<ch>/texture/` ships all twelve `fire1NN.png`. `TextureCycler` already plays
    frame lists, so this is small *once the templates are built*.
  - **EFFECTS is node-keyed, not texture-keyed** (user-confirmed: a *sustained* muzzle flash never
    changes texture, always `fire101`). So `flame01` — the refinery gas flare, sharing material 88
    with the `fire1` template — is a **static base flame**, and the animated fire the user sees
    there is a **placed `fire2` instance** (6 frames @ 5 fps, matching their independent read).
  - **Why it is blocked:** the four `fire.zrd.json` behaviours (`timed_big_fire`,
    `persistent_big_fire`, `persistent_small_fire`, `timed_small_fire`, all anchored on
    `fire2.flt`) are called by **nothing** — their names appear in exactly one file, their own,
    and `CALL_ANIMATION` references animations by name string only (no index form exists anywhere
    in this data). **User searched the disassembly 2026-07-21 and found no trigger either.** So
    the original starts them engine-side by a condition we cannot recover; reproducing them means
    inventing our own trigger, which is a fidelity guess rather than a data-driven port.
  - **If resumed:** the placement half already works — `CALL_ANIMATION`'s target parameter landed
    2026-07-21 and is the mechanism that puts a template at a site. Build the template pool first.
- **mech3ax upstream PR** (cosmetic): planes.zbd round-trip differs by 72 bytes — swapped
  `\0`/`.` garbage past the null terminator in fixed-width texture-name fields. Semantically
  lossless; folded into `docs/PLAN-mech3ax-cs-revival.md` item 12 (stretch goal, same code
  the gamez revival is already touching) / item 14 (its own small upstream PR if not already
  folded into the gamez PR).
- **Drop the `SDL_JOYSTICK_DIRECTINPUT=0` launch-script workaround** (set 2026-07-19 in
  RunGame.ps1/RunDev.ps1) once tools/godot ships a Godot bundling **SDL ≥ 3.4.4**: the bundled
  SDL (3.2.28 up to Godot 4.7.1) hard-freezes the engine when a >255-button DirectInput device
  disconnects — the 8BitDo Ultimate 2 dongle's HID interface is one (`Uint8` loop counter vs
  uncapped dinput `nbuttons`; godot#115667, SDL#14961, fixed by SDL#15304). Check the bundled
  `thirdparty/sdl/joystick/SDL_joystick.c` `SDL_PrivateJoystickForceRecentering` for the `int i`
  fix before removing. Side effect while active: DirectInput-only controllers (non-XInput
  sticks without an SDL HIDAPI driver) are invisible in-game.

- **Degenerate zeppelin node transforms (found 2026-07-22, pre-existing).** On C1/M04 some
  zeppelin animation nodes carry an astronomically large world basis: `gasbag3` reads a global
  origin of ~(4.6e27, -4.8e28, 4.0e29) with basis X ~(0.36, -2.5e24, -2.9e27), and one
  `rock_zeppelin` instance the same — while *sibling instances of the same node names* are
  perfectly sane (`rock_zeppelin` at (-5248, 200, -5208), identity basis). The chain enters
  between `rock_zeppelin` and `gasbag3`; local transforms all along it look normal, so the blowup
  is in an inherited basis, not a local one. **Verified pre-existing** by probing the build from
  *before* the ambient-sound work, with no sound code present — the sound path only reads
  transforms and was simply the first consumer to look at one (its emitter reported a position of
  1e29). Currently harmless-by-accident: nothing else reads those nodes, and `WorldSounds`
  silences a host whose pose has blown up and logs it. Worth chasing because it means some
  animation is writing a garbage transform, which could bite anything that later reads those
  nodes. Start at whatever poses `move_zeppelin`/`tilt_zeppelin`/`rock_zeppelin` in a mission
  where the zeppelins are visible (C1/M04 reproduces; C1/IA1 does not, because it deactivates
  them). Candidates: a repeated relative `PoseTranslate`/`PoseRotate` accumulating, or an
  SI-script/`ObjectScaleState` applied to an already-scaled parent.

- **Animation event kinds that need weapons or cutscenes — `CALLBACK`, `OBJECT_CYCLE_TEXTURE`,
  one-shot `SOUND`** (triaged 2026-07-22, the last of `docs/PLAN-anim-rendering-followups.md`
  item 2 after `OBJECT_MOTION` landed). All three still dispatch at bootstrap, so the counts in
  the "not yet acted on" report look like open work — **they are not**. Each was probed at the
  dispatch site across C1/C3/C4/C5 (def, anchor, resolved target count, payload), and each fails
  for a concrete reason rather than a suspicion. Implementing any of them today is a provable
  no-op, the same verdict `OBJECT_ADD_CHILD` got:

  | Kind | Count | Why it cannot do anything |
  |---|---|---|
  | `Callback` | ×8 every chapter | Every dispatch is `def=camera1`, **unanchored**, values 1/2/10/11/14/20/913/914 — engine notifications for the intro **cutscene** camera. This project has no cutscenes, and a callback's whole purpose is to notify mission logic that does not exist here. |
  | `ObjectCycleTexture` | ×1–2 per chapter | Every dispatch is `node=taildamage` with **`targets=0`** — the node never resolves, so there is nothing to cycle. The one real use of this mechanism (the cockpit damage-indicator hilite) is already a build-time material swap in `GaugeCluster.cs`. |
  | one-shot `Sound` | ×1, C3 only | `def=spew node=snd_waterfall`, **`targets=0`** — it names a sound *definition*, not a node. That waterfall already sounds through `SOUND_NODE`. The family at large is 4,378 `OnCall` + 1,650 `WeaponHit` combat audio (21 names are `DYNAMIC_WEIGHTS` groups needing a further decode), which needs weapons this project does not have. |

  **Pick these up when the thing they depend on exists** — weapons for `SOUND`, a cutscene player
  for `Callback` — not before. `ObjectCycleTexture` needs neither; it needs a mission that
  actually builds a `taildamage` node, which none of the ones this project defaults to do.
  The one kind from that list that *was* reachable, `OBJECT_OPACITY_STATE`, is scheduled work and
  stays in the plan, not here.

- **World renders into only the upper-left quadrant when the camera sits at the world origin**
  (noticed 2026-07-22 while verifying `OBJECT_OPACITY_STATE`; **pre-existing** — reproduced on the
  pre-change build). `--viewer --chapter=C1 --campos=0,30,420 --lookat=0,0,0` draws terrain, sea
  and cloud sprites only in the left ~640 x top ~360 px, the rest showing bare skydome gradient,
  with a hard rectangular edge at exactly half width and half height. Reads like a 4P splitscreen
  pane with one player. The origin is outside C1's playable area (its airfield is near
  -5466,-5136), so nothing normally looks from there and it has never mattered — but an exact
  half-viewport boundary is not a terrain edge, so something is clipping. Worth a look before
  trusting any screenshot taken from an unusual camera.

## Feature backlog

- **Paint scheme follow-ups** (the core landed 2026-07-20 — see `docs/formats/paint.md`
  "Known divergences"; these are the leftovers):
  - **Achromatic paint regions.** A hue window cannot see a paint region with no hue, so the
    Bloodhawk's outer wing panels stay gray where the original paints them black. Needs a
    per-texture value-band rule (hand-authored per aircraft) or a better region key.
  - **Slot order.** Regions are assigned to colour slots by area; validated only on the
    Bloodhawk, and even there the reference cannot separate slots 2 and 3 (both white under
    Fortune Hunters).
  - **The paint UI's "Shade" column** is unmodelled — three Colour *and* three Shade
    dropdowns exist in the UI, only three colours in the data. We ramp black → colour.
  - **A livery picker in the launchscreen.** Selection is CLI-only (`--paint=`); flight
    randomizes per player. Decide from playtest whether the menu should offer it.
  - **AI/ace liveries.** `ia.json` `ace_*` and the AI defs' own `paint_*` are parsed into the
    catalog but nothing flies them — there are no AI aircraft yet.
- **Full `player_plane_destruct` crash choreography**: surface variants
  (`player_crash_default/_dirt/_water`), sparks, black smokeball, `plane_destroy_sg` sound,
  crash trails. Current state = fireball + breakup pieces + 10 s wreck fire (item 10d).
  Folds in the crash-video follow-ups (2026-07-19, `C1 IA1 Crash.mp4`): brown dust burst on
  hard grazes, burning debris arcs, explosion scale/persistence.
- **`flight_ceiling`** (data: 2500 m) unenforced — at full throttle a steep climb is a stable
  equilibrium and sails past it (accepted arcade artifact of the Run-1 item-6 flight model).
- **PLAYER_INIT fields [3]/[4] semantics + per-plane spawn speed** — story-mission spawns
  currently assume the IA convention (0.5 throttle / 53.6 m/s).
- **Sky UV scroll** (`h_zone*scroll`) — scroll rate unknown, not implemented.
- **Star twinkle + undecoded light fields** (flags 523/…, the 0.17 float) — stars/beacons
  render as fixed-size soft sprites, no twinkle.
- **Visual prop spin-up/down** (`startprops`/`stopprops` disc crossfade) — spawning mid-air
  already turning is by design; becomes relevant with a landing/shutdown flow
  (`FlightAudio.OnEngineStop` is already wired for the audio half).
- **Engine dual-stack chorus**: the original plays the engine loop as a ~5%-detuned pair
  (measured in the dive-video analysis, HISTORY 2026-07-19); ours is a single loop.
- **Positional 3D audio for other aircraft** — all sound is own-plane non-positional today;
  the original's IA traffic is clearly audible with Doppler in the reference video.
- **Future cockpit view** would consume already-parsed data: `pcdpN` cockpit damage panels,
  the `*_damage_green/yellow/red` indicator anims (PlaneStats parses them, DamageVisuals
  skips them), `cockpit_engine_sound` (`*_cp` WAVs), `player_fuelleak` (0.85 threshold).
- **Runway `lite*`/`ltout*` lights-on/off state-variant quads** still z-tie (needs an
  engine-side light-state toggle to pick one variant).
- **C2/C5 `cblock*` city-block clutter templates** (non-quad 3D decorations) detected +
  skipped — only flat sprite cards billboard today (C2 palms work, city blocks don't).
- **Rail-over-transition z-nit**: one 6-poly rail patch NE of the C1 bridges sits below the
  draw-order tie-break's resolution.

- **Finished-pilot behaviour in a splitscreen stunt race** (M2.5 item 7): a pilot who clears
  every zone freezes at the finish showing their placing while the field flies on. It matches
  the solo run's freeze and makes the placing unmissable, but it parks a player with nothing
  to do for as long as the slowest pilot takes. The alternative — keep flying freely with the
  timer stopped — is a small change (drop the AllComplete early-return when `Race != null` and
  gate only the objective/marker updates). Decide from the two-controller playtest.
- **Race spawn fairness**: each player takes the next entry in the mission's `stunt_flying`
  spawn list, so pilots start at genuinely different distances from the first zone. Fine for
  a prototype, unfair as a race. Options: spawn everyone abreast from one point (the
  `--spawn-at` `SpawnAbreast` fan already does this), or rank on a per-player-normalised time.

## Open fidelity questions (answerable by testing the original)

- **Map-edge continuation**: ours alternately *reflects* the border tiles (seam-free by
  construction); the original likely plain-repeats them, possibly sharing the edge vertex row.
  A/B an asymmetric border feature in the original; one-line swap in `MapEdgeExtender.MirrorAxis`.
- **Compass north convention**: north = −Z is assumed (one-line flip in FlightController's
  heading feed); drum projection constants + nearest-tick sampling pending A/B.
- **Crossed `pdpN_h` numbering** (bloodhawk/firebrand/brigand data quirk): does the *original*
  amputate the wrong wingtip on wing damage too? Its engine hides healthy skins by an
  engine-side rule we can't see; we pair by mesh position since 2026-07-19.
- **Dive terminal speed**: the dive-video gauge frames pin the original's near-vertical dive
  terminal at ≈ 1.27×fd_speed (~385 mph); our drag curve + `MaxDiveSpeedFrac` cap runs to
  1.7×. Matching it means reshaping the overspeed drag (or the cap) — interacts with the
  whine/rattle curves that key off speed/fd_speed (HISTORY 2026-07-19).
- **Pitch rate vs speed**: the user's 11 s sustained full-pitch 360° visibly bled speed in the
  original — its pitch rate may slow with speed; ours is constant (item-12 calibration matches
  the 11 s average). Likewise the yaw `eff` speed shape (`1.4 − clamp(v/fd)`) is an unvalidated
  interim model away from cruise.
- **Engine pitch behavior in dives**: the original's engine drops ~12% through a dive and
  overshoots ~1.05 at pull-out — not reproducible by the throttle-only pitch curve (cap 1.0).
  Throttle cut? Camera Doppler? A speed/RPM term? Needs a controlled full-throttle-dive
  recording (HISTORY 2026-07-19).
- **`SunIncidence` 0.46** (item-6 world brightness) rests on a single overcast reference —
  a C1B-night and a bright-day original screenshot would confirm/refine the self-scaling.
- **`fogRangeFactor` 2** halving predates the sRGB fog-color fix — re-A/B in game.

## TUNE constants pending playtest

The live list (moved here from CLAUDE.md 2026-07-22). Each is a hand-tuned constant that is
plausible but unvalidated against the original — they need the user in the cockpit, not another
scripted screenshot.

- **Compass tape** — north = −Z convention (unverified vs the original; one-line flip in
  `FlightController`'s heading line), plus `TileOverscan` / `RimGain` / the nearest-tick look.
- **`fogRangeFactor` 2** — the halving predates the sRGB fog-colour fix, so re-A/B it in game.
- **Flight model** — `StallNoseRate`, `KnifeAlignFloor`, `ClimbGravityScale`,
  `LowSpeedDragBlend`, and the Run-2 item-12 per-axis `PitchTune` 0.75 / `YawTune` 1.32 /
  `RollTune` 2.12. Those three are measurement-calibrated (2026-07-19) but the *feel* A/B is
  pending — **especially the ~2.7× cut in pitch authority**, the largest single change to how the
  aircraft handles.
- **Control surfaces** — deflection angles and slew rate.
- **Cloud puffs** — opacity and density. **Cloud deck** — brightness reads ~40 units lighter
  than the original.
- **Wing lights** — `FlashDuration`.
- **Collision feel** — behaviour against building corners.
- **Damage (Run-2 item 10)** — `CrashSpeed` 25, graze friction + attitude kick, tree softness,
  `GrazeStopSpeed`, breakup scatter, and whether the 10c panel-flip and smoke-trail look right in
  real flight (the thresholds need states normal play actually reaches).
- **Audio (Run-2 item 11)** — `WhineMixGain` 0.12; A/B a dive against the original.
- **Stunt mode** — `DzRadius` 30 m (tightened from 60 on user feedback), marker-HUD placement,
  font and distance units; scoreboard fonts and placement.
- **Splitscreen** — the `HudMetrics` sqrt pane damping, `MixGain`, `SpawnAbreast`, join/lock
  feel, tag-gutter widths.
- **Aircraft brightness** — every aircraft got substantially brighter when the inverted-normal
  bug was fixed (2026-07-20); whether flight lighting now wants re-tuning against the reference
  videos is unassessed.

## Owed playtests (need hardware or a human at the controls)

- **The M2.5 playtest pass.** Items 6 and 7 of `docs/plans/PLAN-M2.5-prototype.md` — the
  launchscreen join flow and the splitscreen stunt race — rest on construction plus scripted
  verification for everything needing **two controllers**, which this machine does not have.
  Unverified at runtime: join / un-join, the lock race, per-pad flight binding, Esc-from-
  splitscreen-to-menu, board fonts and placement, rematch feel. Also open as a *design*
  question: should a finished pilot keep flying rather than freeze at the finish?
- **`--freecam` interactive feel** — look sensitivity and the speed curve have never been
  assessed by hand; the module was built entirely through scripted screenshots and
  `--debug-anim`.
- **The labs are mouse-driven** (`--viewer`: damage on H, livery on L, mesh on M) and have had no
  interactive playtest beyond scripted verification.

## C3 ships gamez references to textures its texture.zbd does not contain

Surfaced 2026-07-21 during the extraction cutover and deliberately left alone. C3's gamez
references `cloud1`/`cloud2`, which its own `texture.zbd` does not ship — a **retail-data gap**,
true in both the v0.6.1 and fork extraction trees, so not something the cutover caused.

The fix is a one-line addition to `TextureArchive.KnownAbsentFromGameData`, which would render
them neutral gray instead of magenta. It is left to the user because it is a **visible** change
and it deliberately gives up the magenta signal that means "our bug" for those two names — the
project's convention is that magenta is diagnostic, so suppressing it is a judgement call, not a
cleanup.

## C5 ground z-fighting — coarse quad coplanar with the detailed city ground

Found 2026-07-21 while investigating the animation-runtime performance regression; the user
asked whether follow-up plan item 3 would clear it. **It would not** — full diagnosis in
`docs/HISTORY.md` (2026-07-21 entry), summarised here so it isn't re-chased.

Repro: `--viewer --chapter=C5 --sky-zone=zone2 --campos=-9533.178,76.319,-3367.413
--lookat=-9451.281,28.148,-3398.597`, measured with `--shots=5 --jitter=0.006` (a *sub-pixel*
dither — the 0.15° default moves the camera far too much to isolate depth flips). 8.42% of
pixels flip.

Ruled out: the map-edge extender (identical flicker without `--sky-zone`), and entity rosters /
item 3 (only 2 mesh nodes within 1500 m, generic ground names — item 3 hides discrete roster
objects). Confirmed cause: the depth bias is proportional to view distance
(`VERTEX *= 1.0 - (depth_bias + node_bias)`), so coplanar surfaces sharing a draw priority get
`rank × 2e-6` ≈ 0.16 mm at ~80 m. A 100× bias drops the flicker to 0.01%.

**Open question before fixing:** raising the bias makes a large flat low-resolution quad win
over the detailed night-city ground, which is likely backwards. Why is that coarse quad drawn
coplanar with the fine city at all — is it a coarse LOD tile that SceneBuilder's nearest-LOD
selection should have dropped? — and which does the original draw on top? Answer that before
touching the constants; a bias bump alone would lock in the wrong surface across all chapters.
