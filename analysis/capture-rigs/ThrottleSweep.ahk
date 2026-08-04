; ThrottleSweep.ahk - scripted throttle staircase for CAP-21 (BL-248)
;
; WHY THIS EXISTS.  CAP-21 asks whether the original's chase camera distance moves
; with airspeed at all - i.e. whether `dist_factor`/`dist_vary` in the shipped camera
; data drive distance from speed, or are something else entirely.  The measurement is
; the aircraft's APPARENT SIZE in frame against the airspeed on the MPH gauge in the
; same frame, so the capture needs the speed swept across the aircraft's whole usable
; range, slowly, with the view untouched.
;
; Throttle in the original is a discrete 9-position setting on the main-row number
; keys: `1` = 0/8 (idle) through `9` = 8/8 (full).  So the sweep is a staircase of
; taps, not a held axis.
;
;   F13   up     1 -> 9   (0/8 -> 8/8), 5 s per step   ~51 s
;   F14   down   9 -> 1   (8/8 -> 0/8), 5 s per step   ~51 s
;   F15   lo-hi  1, then 9                             ~16 s
;   F16   hi-lo  9, then 1                             ~16 s
;   F21   abort - releases everything and exits
;
; WHY FOUR MODES AND NOT ONE.  The staircase (F13/F14) is the main instrument: it
; walks the speed range in readable plateaux and, run both ways, makes ordering and
; hysteresis visible instead of assumed.  The two-point runs (F15/F16) are the
; CONTROL, and they are the reason this rig is worth scripting rather than flying.
; They traverse the same speeds with a completely different throttle history and a
; much larger acceleration, so:
;
;   - if apparent size tracks SPEED, the staircase and the two-point run must agree
;     wherever their speeds agree;
;   - if it tracks THROTTLE SETTING or ACCELERATION instead, they cannot agree, and
;     the disagreement localises which one.
;
; A single slow sweep confounds all three, because in one gentle run they move
; together.  A flat result is equally a result: constant apparent size across the
; whole range disproves BL-248(a) rather than leaving it open, and a correct disproof
; closes it.
;
; ⚠ STEP_S IS A CEILING, NOT A SETTLING TIME - it is set by the STALL at 0/8 (user,
; 2026-08-04).  Held at idle the aircraft bleeds speed and eventually stalls, which ends
; the take, so 5 s is the longest the low end tolerates rather than the shortest the
; measurement needs.  Do not raise it.
;
; That it also does not reach equilibrium is fine, and worth knowing so nobody "fixes"
; it: the pairing this capture needs is (apparent size, airspeed) READ OFF THE SAME
; FRAME - a per-frame pairing, not a per-step steady state.  A step still accelerating
; simply supplies more distinct speeds per second of footage.  Equilibria are CAP-20's
; job, not this one.
;
; ⚠ THE TAIL IS THROTTLE-AWARE, and that is why.  A flat 3 s tail after `down`/`hi-lo`
; would leave the aircraft at 0/8 for STEP_S + TAIL_S = 8 s, 60% past the stall budget
; the 5 s was chosen to respect - the rig would break its own constraint at the one
; place it matters.  So the tail is taken only when the run ends at a throttle that can
; hold level flight, and a run ending low instead taps SAFE_KEY straight away.  The
; recovery tap is logged, and lands AFTER the sweep-end marker, so it can never fall
; inside a measured window.
;
; Key `0` is NOT bound to throttle (user-confirmed, 2026-08-04) - the set is `1`-`9`.
; Recorded rather than assumed, the same way BL-150(e) records Kp5 as unbound.
;
; ⚠ DO NOT TOUCH THE CAMERA.  The whole measurement is apparent size, so any numpad
; key (a fixed view), the +/- distance trim, or a view change mid-run destroys the
; take.  Note the numpad is the CAMERA in the original, which is why this rig uses the
; main-row digits and why sc04F/sc050/sc051 must never appear in it.
;
; REQUIRES AutoHotkey v2.0.

; ⚠ SEND MODE IS NOT A STYLE CHOICE (capture-rigs rule 3).  SendInput injects a batch
; atomically with no gap between events; the original samples key state once per frame,
; so both edges of a tap land inside one sample and the keypress is never seen - it
; types perfectly into Notepad and does nothing in the game.  SendMode "Event" goes
; through keybd_event one event at a time.  Do not "optimise" this back to SendInput.
; For the same reason TAP_MS below is a real hold, not a bare Send.

#Requires AutoHotkey v2.0
#SingleInstance Force
InstallKeybdHook()
SendMode "Event"
SetKeyDelay 20, 20

; ---------------------------------------------------------------- configuration
; Scan codes for the MAIN-ROW digits, not characters and not numpad.  A DirectInput-era
; game reads scan codes, so this survives any keyboard layout; and the numpad digits
; (sc047-sc052) are the camera views, which must not move during this capture.
SC := Map(1,"sc002", 2,"sc003", 3,"sc004", 4,"sc005", 5,"sc006",
          6,"sc007", 7,"sc008", 8,"sc009", 9,"sc00A")

; Throttle notch each key selects, purely for the log - key N is (N-1)/8.
NOTCH := Map(1,"0/8", 2,"1/8", 3,"2/8", 4,"3/8", 5,"4/8",
             6,"5/8", 7,"6/8", 8,"7/8", 9,"8/8")

STEP_S := 5.0              ; dwell after each key - a STALL CEILING, see header
LEAD_S := 3.0              ; straight-and-level baseline before the first key
TAIL_S := 3.0              ; baseline after the last key, when the ending allows one
TAP_MS := 120              ; how long each digit is held down

; Stall guard for the end of a run.  A run finishing on a key below TAIL_MIN_KEY gets no
; tail and an immediate recovery tap instead, so total time at the low setting stays
; within STEP_S.  SAFE_KEY 6 = 5/8, comfortably above anything that decays.
TAIL_MIN_KEY := 4          ; 3/8 - the lowest ending that may hold a tail
SAFE_KEY     := 6          ; 5/8 - what a low-ending run recovers to
RECOVER      := true       ; set false to be handed the aircraft exactly as it ended

; TAP_MS is 120, not 20, for the send-mode reason above: at 30 fps a 20 ms tap is
; 0.6 of a frame and a once-per-frame sampler will miss some of them, silently
; dropping a step out of the middle of the staircase.  120 ms is ~3.6 frames.
; Raise this if a step is ever seen to go unregistered; never lower it.

MODES := Map(
    "up",    [1,2,3,4,5,6,7,8,9],
    "down",  [9,8,7,6,5,4,3,2,1],
    "lo-hi", [1,9],
    "hi-lo", [9,1])

ABORT_KEY := "F21"
LOG_DIR := A_ScriptDir
SPIN_MS := 0.025           ; busy-spin window; see ElevatorDutySweep.ahk
BEEP    := true            ; audible start/end markers
SHOW_TOOLTIP := true       ; needs windowed/borderless - will not draw over exclusive
                           ; fullscreen.  Set false if it lands in the shot.

QPC_FREQ := 0
DllCall("QueryPerformanceFrequency", "Int64*", &QPC_FREQ)
DllCall("winmm\timeBeginPeriod", "UInt", 1)
OnExit(Cleanup)

RUNNING := false
EVENTS  := []

; ------------------------------------------------------------------- QPC clock
Now() {
    global QPC_FREQ
    c := 0
    DllCall("QueryPerformanceCounter", "Int64*", &c)
    return c / QPC_FREQ
}

; Absolute target, so a late wake-up on one edge does not push every later edge back.
WaitUntil(t) {
    global SPIN_MS, RUNNING, ABORT_KEY
    loop {
        r := t - Now()
        if (r <= 0)
            return
        if (GetKeyState(ABORT_KEY, "P")) {
            RUNNING := false
            return
        }
        if (r > SPIN_MS)
            Sleep(Integer((r - SPIN_MS) * 1000))
    }
}

Mark(t, what) {
    global EVENTS
    EVENTS.Push(Format("{:9.3f}  {}", t, what))
}

Say(text) {
    global SHOW_TOOLTIP
    if (SHOW_TOOLTIP)
        ToolTip text, 40, 40
}

; Release every digit this rig can send.  Taps are short, so normally nothing is down;
; this exists for the abort path, which can land inside one.
ReleaseAll() {
    global SC
    for n, code in SC
        Send "{" code " up}"
}

; SoundBeep BLOCKS for its full duration, so the start marker must sound BEFORE t0 is
; taken - otherwise the first step is already 120 ms late against a schedule that is
; absolute for the rest of the run.  The end markers sound after the last key, so they
; cannot delay an edge.
BeepStart() {
    global BEEP
    if (BEEP)
        SoundBeep(1200, 120)
}

BeepEnd() {
    global BEEP
    if (BEEP) {
        SoundBeep(600, 90)
        SoundBeep(600, 90)
    }
}

; -------------------------------------------------------------------- the sweep
; NOT named Run() - that is an AHK v2 built-in (it launches programs) and cannot be
; redefined.
RunSweep(mode) {
    global RUNNING, EVENTS, MODES, SC, NOTCH, STEP_S, LEAD_S, TAIL_S, TAP_MS
    global TAIL_MIN_KEY, SAFE_KEY, RECOVER
    if (RUNNING) {
        ToolTip "already running"
        return
    }
    steps := MODES[mode]
    RUNNING := true
    EVENTS := []

    BeepStart()
    t0 := Now()
    Mark 0, Format("THROTTLE SWEEP START  mode={}  steps={}  step={:.1f}s  "
                 . "lead={:.1f}s  tap={}ms", mode, steps.Length, STEP_S, LEAD_S, TAP_MS)

    Say "baseline"
    WaitUntil(t0 + LEAD_S)

    t := LEAD_S
    for i, n in steps {
        if (!RUNNING)
            break
        Say Format("[{}/{}]  key {}  = {}", i, steps.Length, n, NOTCH[n])
        WaitUntil(t0 + t)
        Send "{" SC[n] " down}"
        Mark Now() - t0, Format("DOWN  key{}   throttle {}", n, NOTCH[n])
        WaitUntil(t0 + t + TAP_MS / 1000)
        Send "{" SC[n] " up}"
        Mark Now() - t0, Format("UP    key{}   throttle {}", n, NOTCH[n])
        t += STEP_S
    }

    ReleaseAll()
    aborted := !RUNNING
    if (!aborted) {
        ; The final step's own dwell is always taken - it is a measured step like any
        ; other, and at the low end it is the most valuable one in the run.  What the
        ; stall guard drops is only the TAIL on top of it: ending at 0/8 or 1/8, a 3 s
        ; tail would push time-at-idle to 8 s, 60% past the budget STEP_S respects.
        last := steps[steps.Length]
        WaitUntil(t0 + t)
        if (last >= TAIL_MIN_KEY) {
            Say "-- tail --"
            WaitUntil(t0 + t + TAIL_S)
        } else {
            Mark Now() - t0, Format("NO TAIL  (ended on key{} = {}, below key{}) "
                                  . "- stall guard", last, NOTCH[last], TAIL_MIN_KEY)
        }
        Mark Now() - t0, "THROTTLE SWEEP END"
        ; Recovery lands AFTER the end marker on purpose: it is not part of the sweep
        ; and must never be read as a step.
        if (RECOVER && last < TAIL_MIN_KEY) {
            Say Format("recover -> key {} = {}", SAFE_KEY, NOTCH[SAFE_KEY])
            Send "{" SC[SAFE_KEY] " down}"
            Mark Now() - t0, Format("RECOVER DOWN  key{}   throttle {}",
                                    SAFE_KEY, NOTCH[SAFE_KEY])
            Sleep TAP_MS
            Send "{" SC[SAFE_KEY] " up}"
            Mark Now() - t0, Format("RECOVER UP    key{}   throttle {}",
                                    SAFE_KEY, NOTCH[SAFE_KEY])
        }
    } else {
        Mark Now() - t0, "ABORTED"
    }
    BeepEnd()

    ; Flush BEFORE honouring an abort.  F21 deliberately does not ExitApp while a
    ; sweep is running: a part-run take is still analysable, but only if its log
    ; survives, and a capture is expensive to re-fly.
    Flush(mode)
    ToolTip
    RUNNING := false
    if (aborted)
        ExitApp
}

Flush(mode) {
    global EVENTS, LOG_DIR
    path := LOG_DIR "\throttle-" mode "-" FormatTime(, "yyyyMMdd-HHmmss") "-log.txt"
    FileAppend StrReplace(Join(EVENTS), "`r", "") "`n", path, "UTF-8"
    ToolTip "log: " path
    SetTimer(() => ToolTip(), -2500)
}

Join(a) {
    s := ""
    for v in a
        s .= (s = "" ? "" : "`n") v
    return s
}

Cleanup(*) {
    ReleaseAll()
    DllCall("winmm\timeEndPeriod", "UInt", 1)
}

; ---------------------------------------------------------------------- hotkeys
F13:: RunSweep("up")
F14:: RunSweep("down")
F15:: RunSweep("lo-hi")
F16:: RunSweep("hi-lo")

; Abort.  While a sweep is running this only lowers the flag - WaitUntil sees it, the
; sweep unwinds, writes its log and then exits.  With nothing running it exits at once.
F21:: {
    global RUNNING
    if (RUNNING) {
        RUNNING := false
        ReleaseAll()
        return
    }
    ReleaseAll()
    ToolTip
    ExitApp
}
