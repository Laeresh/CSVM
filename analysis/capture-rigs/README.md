# Capture rigs

Input-automation scripts for recording **the original game**, where a capture's value depends on
the inputs being repeatable rather than flown by hand. Each rig writes a timestamped log of what it
pressed, so the analysis reads event boundaries out of the log instead of inferring them from
on-screen motion.

These are recording aids for `playtest.md` §0 captures. They drive the original, not our build —
nothing here is part of the game or the test suite.

⚠ **No rig here serves a flight-model question, and none may be added for one.** A quantity read
off the cockpit panel does not settle a flight constant (`docs/verification.md` DET-12); those
questions go to `crimson.exe`. The surviving rigs serve camera-layout captures, where what is
being established is which view a key selects, not a number.

## Three rules that make a capture analysable

The first two were learned the expensive way on `CAP-07`, whose first, hand-flown take had to be
thrown out (`docs/HISTORY.md`, 2026-08-04). They generalise past the numpad rigs, so apply them to
any new rig here.

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

**3 · Send input with `SendMode "Event"`, never `SendInput`.** `SendInput` delivers a batch
atomically with no gap between events; the original samples key state once per frame, so both edges
land inside one sample and are never seen. A rig built on it types perfectly into Notepad while
doing **nothing** in the game. Event mode goes through `keybd_event` one event at a time with a real
`SetKeyDelay`, which is why the numpad rigs work. For the same reason a rig's shortest hold must
clear a frame with margin.

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
