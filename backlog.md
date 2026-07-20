# Backlog — unscheduled future work

Everything known-but-not-scheduled, so it survives between polish runs. The **active** run's
checklist lives in `docs/PLAN-M2-polish-2.md`; per-item history/diagnosis detail is in
`docs/HISTORY.md` (dated entries) and `docs/architecture.md` (module bullets). The live list of
hand-tuned constants awaiting playtest stays in CLAUDE.md → "Open TUNE items pending playtest".
When an item gets scheduled into a plan, move it there; when it lands, delete it here.

## Blocked / deferred

- **Animated world vehicles** (Run-2 item 9, deferred 2026-07-18, user decision): train/car/zep
  motion is `OBJECT_MOTION_SI_SCRIPT` whose `.zan` spline scripts exist only compiled inside
  `cam_anim.zbd`, which mech3ax does not extract for CS → needs a **mech3ax fork extension**.
  **Scheduled 2026-07-20: `docs/PLAN-mech3ax-cs-revival.md`** (Track B, items 1–7) — adding
  `cam_anim.zbd`/`mis_anim.zbd` as a fourth variant of the existing `crates/anim` MW/PM/RC
  container format, plus the same plan's Track A (items 8–14) for reviving `gamez.zbd`/
  `planes.zbd` support upstream deleted in commit `7f592ec`. Resuming this also unlocks:
  anim-state engine part 2 (per-object NAME1/generic-root defs), timed hangar-door motion,
  train steam `PUFFER_STATE`, road-vehicle `OBJECT_MOTION_FROM_TO` chains
  (`cars_moving`/`trucks_moving`).
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

## Feature backlog

- **Paint scheme follow-ups** (the core landed 2026-07-20 — see `docs/formats/paint.md`
  "Known divergences"; these are the leftovers):
  - **Achromatic paint regions.** A hue window cannot see a paint region with no hue, so the
    Bloodhawk's outer wing panels stay gray where the original paints them black. Needs a
    per-texture value-band rule (hand-authored per aircraft) or a better region key.
  - **The Fury is unpaintable.** Its skins are an all-neutral near-black shading map with no
    key at all, yet the original flies a studio-blue `secfury`. It gets decals only today.
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

See CLAUDE.md → "Open TUNE items pending playtest" (kept there, next to current status):
compass drum, fog range factor, flight-model constants + the item-12 per-axis
`PitchTune`/`YawTune`/`RollTune` (calibrated 2026-07-19, feel A/B pending), control-surface
angles/slew, cloud-puff opacity/density, wing-light flash duration, collision feel vs
building corners, item-10 damage feel set, item-11 `WhineMixGain` 0.12.
