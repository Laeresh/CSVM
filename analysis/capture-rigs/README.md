# Capture rigs

Input-automation scripts for recording **the original game**, where a capture's value depends on
the inputs being repeatable rather than flown by hand. Each rig writes a timestamped log of what it
pressed, so the analysis reads event boundaries out of the log instead of inferring them from
on-screen motion.

These are recording aids for `playtest.md` §0 captures. They drive the original, not our build —
nothing here is part of the game or the test suite.

## Two rules that make a capture analysable

Both were learned the expensive way on `CAP-07`, whose first, hand-flown take had to be thrown out
(`docs/HISTORY.md`, 2026-08-04). They generalise past the numpad rigs, so apply them to any new rig
here.

**1 · Script the input, and log it.** A rig's timestamped log removes event segmentation from the
analysis entirely. Instead of inferring "when did the thing happen" from on-screen motion — which
fails exactly when the motion is fast, i.e. when it matters — the event windows are read off the
log and aligned to the video by a **single offset**, taken from the first frame that visibly moves.
Everything downstream inherits that one alignment. A hand-flown take gives you neither the
boundaries nor a way to check them.

**2 · Hold the aircraft straight and level, and use the compass to prove it.** A steady subject
means its body axes equal world axes, so angles measured off the footage are angles relative to the
aircraft directly, with no attitude solve in between. The HUD compass ribbon is the check, and a
good one: it tracks the *aircraft's* heading and does **not** move when the camera swings, so a
ribbon that holds still is proof the subject held still — independent of whatever the camera did.

## `NumpadViewSweep.ahk` — `CAP-07`

AutoHotkey v2. Press **F13** to sweep the numpad camera views: 3 s of straight-and-level baseline,
then `Kp1`–`Kp9` each held **alone** for 3.5 s with a 3.5 s return to base between, 3 s tail.
About 69 s. **Esc** aborts and releases whatever is held.

Writes `sweep-log.txt` next to the script — one timestamped line per press and release.

- **Fly straight and level throughout** — rule 2 above. The whole point of this capture is angles
  measured against the airframe.
- **One key at a time is the whole point.** `CAP-07` take 1 was rejected because the presses
  overlapped — the camera lerped straight from one fixed position to the next without passing
  through base, and most holds never settled. See `docs/HISTORY.md`, 2026-08-04.
- **`Kp5` is in the sweep deliberately.** `BL-150`(e) records it as unbound on the strength of
  assumption; a held-with-no-movement clip is what makes that evidence.
- **`Kp0` is not**, and should not be added: it is rudder-left, not a camera key.
- Scancodes are sent rather than `{NumpadN}`, so NumLock state cannot change what the game reads.
- The on-screen key label is a ToolTip, so it needs **windowed or borderless-windowed** mode — it
  will not draw over an exclusive-fullscreen game. The log covers you either way; set
  `SHOW_TOOLTIP := false` if it is in the shot.

## `NumpadComboSweep.ahk` — `CAP-08`

AutoHotkey v2, **F13**, same conventions (scancodes, ToolTip, Esc to abort). Writes `combo-log.txt`.
14 steps at ~9.5 s each, a little over two minutes. Trim `STEPS` if that is too long.

Each step is **staggered on purpose**, and that is the design:

| | |
|---|---|
| press the first key | `SETTLE_MS` 2.5 s — the camera reaches *that key's own* position, alone |
| add the rest | `HOLD_MS` 3.5 s — the combined position |
| release all | `GAP_MS` 3.5 s — ease back to base |

Pressing a combination simultaneously would only show where it ends up. Arriving at a single key's
position first and *then* adding the second, in one continuous shot, shows what adding it actually
does — blend toward a further position, replace the first outright, or be ignored while the first
is still down. That is the open question in `BL-150`(d), and it cannot be read off a still of the
end state alone. The stagger also means every step doubles as a second, independent single-key
observation to cross-check `CAP-07` against.

`STEPS` covers the eight camera keys as a ring (7 8 9 6 3 2 1 4 clockwise): the **eight adjacent
pairs** around it, most likely to blend into intermediate positions; the **four opposite pairs**,
which test the contradictory case (cancel, first-wins, or something else); and **two triples**, to
show whether whatever rule governs pairs extends past two keys. No `5` (unbound), no `0` (rudder).

## `ElevatorDutySweep.ahk` — `CAP-10` re-record

AutoHotkey v2. **F13** runs the duty sweep (~54 s), **F14** the sustained-hold ladder (~28 s),
**Esc** aborts and releases. Writes `elevator-<mode>-<stamp>-log.txt` next to the script.

`CAP-10`'s five hand-flown takes ruled climb rate out as the driver of the engine note (the take
flown to vary climb rate correlates at R² 0.188; the 90°-banked take swings the note 6.2% while
holding altitude to 168 ft). What is left is the **elevator input**, which is a stick position and
so appears on no gauge — the only way to correlate against it is to script it.

The sweep alternates nose-up/nose-down at a fixed 600 ms period, which holds the **mean pitch rate
at zero** so altitude, speed and flight-path angle stay put, and steps the **duty cycle** — the
fraction of each half-cycle the key is held — through 0, ¼, ½, ¾, 1 and back down, 6 s per step.
Every step therefore has the same flight state and a different amount of elevator deflection. If the
note steps with duty, the input is the driver and the staircase reads the coefficient directly; if
it stays flat, the input is not the driver either, which is equally a result. Running the sweep up
and back down makes ordering and hysteresis visible instead of assumed.

- **Fly straight and level at 100% throttle, in an external/chase view.** The cockpit view is
  useless for this capture — its engine mix is damped and carries no moving harmonic
  (`docs/HISTORY.md`, 2026-08-04).
- **Beeps mark start (1.2 kHz) and end (two at 600 Hz)**, as in `pitch_cadence.ahk`. Normally
  unacceptable here — on this capture the audio *is* the measurement — but the user's rig records
  the **game's audio only, not the system mixer**, so they never reach the file. ⚠ **Set
  `BEEP := false` if the capture setup ever changes to desktop/system audio**: 1.2 kHz sits in the
  band this analysis reads and would be indistinguishable from engine harmonics at exactly the step
  boundaries where the measurement is taken. Alignment does not depend on them either way (rule 1).
- ⚠ **`SendMode "Event"` is load-bearing — this is rule 3.** The first version used `SendInput` and
  typed perfectly into Notepad while doing **nothing** in the game. `SendInput` delivers a batch
  atomically with no gap between events; the original samples key state once per frame, so both
  edges land inside one sample and are never seen. Event mode goes through `keybd_event` one event
  at a time with a real `SetKeyDelay`, which is why the two numpad rigs work. For the same reason
  the shortest hold in a rig must clear a frame with margin — hence the 600 ms period here, whose
  lowest non-zero duty is 75 ms (~2.3 frames at 30 fps).
- ⚠ **`pitch_cadence.ahk` (`CAP-04`) uses `SendInput` and may have this bug.** Its header records it
  as verified *"with the key sending stubbed out so the test could not type into anything"* — so its
  timing is proven and its delivery into the game never was. Check it before trusting a re-record.
- Arrow-key scancodes are extended (`sc148`/`sc150`), deliberately not numpad 8/2 — those are the
  camera, and would swing the view mid-measurement.

## `ThrottleSweep.ahk` — `CAP-21`

AutoHotkey v2. **F13** steps throttle up `1`→`9` (0/8 → 8/8) at 5 s per step (~51 s), **F14** the
same downward (~51 s), **F15** goes `1` then `9` and **F16** `9` then `1` (~16 s each). **F21**
aborts. Writes `throttle-<mode>-<stamp>-log.txt` next to the script.

Throttle in the original is a discrete 9-position setting on the **main-row** digits, so the sweep
is a staircase of taps rather than a held axis.

- **The two-point runs are the control, and they are why this is scripted.** The staircase walks the
  speed range in readable plateaux; F15/F16 traverse *the same speeds* with a different throttle
  history and a much larger acceleration. If apparent size tracks **speed**, the two must agree
  wherever their speeds agree; if it tracks **throttle setting** or **acceleration**, they cannot,
  and the disagreement says which. One gentle hand-flown sweep confounds all three at once.
- ⚠ **`STEP_S` 5 s is a stall CEILING, not a settling time** (user, 2026-08-04). Held at idle the
  aircraft bleeds speed and eventually stalls, ending the take, so 5 s is the longest the low end
  tolerates — not the shortest the measurement needs. **Do not raise it.** That it also falls short
  of equilibrium is fine and worth knowing so nobody "fixes" it: the pairing this capture needs is
  (apparent size, airspeed) read off the *same frame*, so a step still accelerating just supplies
  more distinct speeds per second of footage. Equilibria are `CAP-20`'s job.
- **The tail is throttle-aware, for that reason.** A flat 3 s tail after `down`/`hi-lo` would leave
  the aircraft at 0/8 for `STEP_S + TAIL_S` = 8 s, 60% past the budget the 5 s exists to respect. A
  run ending below `TAIL_MIN_KEY` therefore takes its final step's dwell in full — that step is the
  near-stall end of the range and the most valuable in the run — then skips the tail and taps
  `SAFE_KEY` (6 = 5/8) to recover. The recovery is logged *after* the sweep-end marker so it can
  never be read as a step. Set `RECOVER := false` to be handed the aircraft exactly as it ended.
- ⚠ **F14 (`down`) is the mode most likely to stall**, and inherently so: it spends 5 s at 1/8
  *before* its 5 s at 0/8, so it arrives at idle already slow. F16 (`hi-lo`) reaches 0/8 straight
  from full speed with maximum energy in hand, so if the low end is proving marginal, get it from
  F16 and treat F14's last step or two as expendable.
- **Key `0` is not bound to throttle** (user-confirmed, 2026-08-04) — the set is `1`–`9`, nine keys
  for nine notches. Recorded rather than assumed, as `BL-150`(e) records `Kp5`.
- ✅ **Flown 2026-08-04, four takes, and the control paid for itself.** The staircase and the
  two-point runs agree at ~296 mph to **0.46%** across both directions and two altitudes, which is
  what pinned the distance to *speed* rather than throttle setting; the two-point runs' ~10× larger
  acceleration is what exposed the second term, a **+15%** transient relaxing at 0.65 /sim-s that a
  gentle hand-flown sweep would have folded invisibly into the speed law. Decoded by
  `analysis/video-flight-calibration/chasesize.py`; result on `BL-248` and in `docs/HISTORY.md`
  (2026-08-04 (f)).
- **The logs were not needed, and that is a property of this capture rather than of the rig.** The
  speedometer is in every frame, so the abscissa comes off the footage and no clock alignment
  against `throttle-*-log.txt` is required — unlike `CAP-08`, where recovering that alignment from
  motion alone was the hard part. Keep writing them anyway: they cost nothing and they are the only
  record of *which* mode a take was.
- **What is in the shot is the recorder's FPS counter** (top-left, `FPS 120 …`), not the rig's
  tooltip. It sits well clear of the aircraft and of both chase dial columns, so it cost nothing
  here — but it would land inside a **cockpit** capture's panel ROI, so turn it off before
  re-recording anything that goes through `extract.py`'s cockpit path. Incidentally it records that
  these takes ran at **120 fps in-game** while captured at 30: the sim-clock factor k = 1.390 was
  measured on an earlier session, so any figure quoted here in *sim* seconds inherits that
  assumption. The wall-clock figures do not.
- ⚠ **Do not touch the camera** — no numpad key, no `+`/`−` distance trim, no view change. Apparent
  size *is* the measurement. This is also why the rig uses main-row scancodes (`sc002`–`sc00A`) and
  why numpad scancodes must never appear in it.
- A flat result closes `BL-248`(a) rather than leaving it open: constant apparent size across the
  whole speed range disproves the hypothesis, and a correct disproof is a close.
- `TAP_MS` is 120 ms (~3.6 frames at 30 fps), not the 20 ms a bare `Send` would give — rule 3 again.
  A 20 ms tap is 0.6 of a frame and a once-per-frame sampler drops steps out of the middle of the
  staircase silently.
- Verified 2026-08-04 on AutoHotkey 2.0.26 by a dry run with the key sending stubbed to a recorder:
  edges land at 3.000 / 8.000 / 13.000 … s to the millisecond, taps measure 120 ms, the emitted
  scancodes are `sc002`…`sc00A` in the right order, and all three ending cases behave — `down` and
  `hi-lo` hold exactly 5.0 s at 0/8 then recover, `lo-hi` ends high and keeps its tail. ⚠ As with
  `pitch_cadence.ahk`, that verifies **timing and ordering, not delivery into the game** — only a
  real take proves the original saw the taps. Check the first log against the footage before
  trusting a long run.

⚠ The rigs must not be run in the same take — they all bind F13, and the analysis keys off one log
per recording.
