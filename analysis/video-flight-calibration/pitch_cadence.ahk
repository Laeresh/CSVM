; pitch_cadence.ahk - fixed-cadence pitch input for the CAP-04 re-record (BL-147)
;
; WHY THIS EXISTS.  The original's pitch is a digital key, so its spin-up time
; constant tau cannot be read off a single step edge - tau <~ 0.2 sim-s is about
; four frames at 30 fps.  Driving the pitch keys as a SQUARE WAVE turns tau into a
; ripple AMPLITUDE at a known frequency, which averages down over hundreds of
; cycles.  Alternating nose-up / nose-down (rather than pulsing one key) holds the
; mean rate at zero, so the aircraft porpoises about level: speed, the aero gain and
; the altimeter band all stay put and the ADI never saturates.  See backlog.md
; BL-147 for the modelled amplitudes.
;
; WHAT MATTERS, AND WHAT DOES NOT.  The period does NOT have to hit its nominal
; value - the real period is fitted from the footage.  What matters is that it is
; STABLE.  A constant per-cycle overhead is harmless (it is just a slightly longer,
; still-constant period); random jitter is the enemy.  So this script schedules
; every edge against an ABSOLUTE clock derived from QueryPerformanceCounter - errors
; never accumulate - raises the Windows timer resolution to 1 ms, and busy-spins the
; last ~2 ms into each edge.  It also LOGS every press and release with a
; sub-millisecond timestamp, which is ground truth for the input waveform: we no
; longer have to assume the pulses landed, we can look.
;
; REQUIRES AutoHotkey v2.0 or newer (v1 will refuse to run this file, loudly).
;
; VERIFIED 2026-08-03 on this machine, with the key sending stubbed out so the test
; could not type into anything: 26 cycles at a nominal 230 ms came out at a measured
; 230.000 ms, sd 0.002 ms, worst single half-step 0.011 ms off, cumulative drift
; 0.004 ms over the whole run.  That is ~2 us of jitter against a 33 ms frame
; interval - negligible.  The first attempt slept to within 2 ms of each edge and
; landed +-12 ms out (Windows' ~15.6 ms scheduler quantum); see SPIN_MS below.

#Requires AutoHotkey v2.0
#SingleInstance Force
InstallKeybdHook()

; ---------------------------------------------------------------- configuration
; Scan codes, not characters: DirectInput-era games often read scan codes and
; ignore synthesised virtual-key events.  sc148 = Up arrow, sc150 = Down arrow.
; NOTE the leading 1: these are the EXTENDED (E0-prefixed) codes.  sc048 / sc050
; are numpad 8 / 2 - and the numpad is the CAMERA in the original (CAP-07/CAP-08),
; so getting this wrong would swing the view in the middle of the measurement
; instead of pitching the aircraft.
KEY_PUSH := "sc148"        ; Up arrow   - nose down
KEY_PULL := "sc150"        ; Down arrow - nose up
LOG_DIR  := A_ScriptDir "\cadence-logs"

; Nominal periods are set on the hotkeys at the bottom: 230 / 370 / 570 / 930 ms.
; Each is a non-integer number of frames at 30, 60 AND 120 fps (6.9/13.8/27.6,
; 11.1/22.2/44.4, 17.1/34.2/68.4, 27.9/55.8/111.6), so the sampling phase drifts
; through the cycle and reconstructs the waveform below the frame interval,
; whatever the recorder turns out to do.

QPC_FREQ := 0
DllCall("QueryPerformanceFrequency", "Int64*", &QPC_FREQ)
DllCall("winmm\timeBeginPeriod", "UInt", 1)
OnExit(Cleanup)

RUNNING := false
EVENTS := []

; ------------------------------------------------------------------- QPC clock
Now() {
    global QPC_FREQ
    c := 0
    DllCall("QueryPerformanceCounter", "Int64*", &c)
    return c / QPC_FREQ
}

; Sleep to within SPIN_MS of the target, then busy-spin the rest.  The target is
; ABSOLUTE, so a late wake-up on one edge does not push every later edge back.
;
; SPIN_MS is 25, not 2, and that is the whole difference between a usable cadence
; and a useless one.  Windows' scheduler quantum is ~15.6 ms and timeBeginPeriod(1)
; does not reliably buy it back, so sleeping to within 2 ms of the edge still landed
; +-12 ms out - measured, not assumed.  Spinning the last 25 ms costs a rounding
; error of one core for a 20 s run and pins the edges to well under a millisecond.
SPIN_MS := 0.025

WaitUntil(t) {
    global SPIN_MS
    loop {
        r := t - Now()
        if (r <= 0)
            return
        if (r > SPIN_MS)
            Sleep(Integer((r - SPIN_MS) * 1000))
    }
}

; --------------------------------------------------------------------- runtime
Press(sc) {
    SendInput("{" sc " down}")
}

Release(sc) {
    SendInput("{" sc " up}")
}

ReleaseAll() {
    global KEY_PULL, KEY_PUSH
    Release(KEY_PULL)
    Release(KEY_PUSH)
}

; mode "alt"  - alternate pull / push, mean rate zero, the tau measurement
; mode "duty" - pulse the PULL key only at 50% duty; the self-check (see NOTES)
RunCadence(periodMs, mode) {
    global RUNNING, EVENTS, KEY_PULL, KEY_PUSH
    if (RUNNING) {                       ; same key again, or any other, = stop
        RUNNING := false
        return
    }
    RUNNING := true
    EVENTS := []
    half := periodMs / 2000.0            ; seconds
    SoundBeep(1200, 120)                 ; audible start marker - see NOTES
    t0 := Now()
    prev := ""
    i := 0
    while (RUNNING) {
        WaitUntil(t0 + i * half)
        if (mode = "alt") {
            if (Mod(i, 2) = 0)
                want := KEY_PULL
            else
                want := KEY_PUSH
        } else {
            if (Mod(i, 2) = 0)
                want := KEY_PULL
            else
                want := ""
        }
        t := Now() - t0
        if (prev != "" && prev != want) {
            Release(prev)
            if (prev = KEY_PULL)
                EVENTS.Push(Format("{:.6f},pull,up", t))
            else
                EVENTS.Push(Format("{:.6f},push,up", t))
        }
        if (want != "" && want != prev) {
            Press(want)
            if (want = KEY_PULL)
                EVENTS.Push(Format("{:.6f},pull,down", t))
            else
                EVENTS.Push(Format("{:.6f},push,down", t))
        }
        prev := want
        i := i + 1
        if (GetKeyState("F21", "P")) {   ; panic key, polled as a backstop
            RUNNING := false
            break
        }
    }
    ReleaseAll()
    EVENTS.Push(Format("{:.6f},stop,-", Now() - t0))
    SoundBeep(600, 90)
    SoundBeep(600, 90)
    WriteLog(periodMs, mode, i)
}

WriteLog(periodMs, mode, halves) {
    global EVENTS, LOG_DIR
    if (!DirExist(LOG_DIR))
        DirCreate(LOG_DIR)
    stamp := FormatTime(, "yyyyMMdd-HHmmss")
    path := LOG_DIR "\cadence-" periodMs "ms-" mode "-" stamp ".csv"
    body := "# nominal_period_ms=" periodMs " mode=" mode "`n"
    body := body "# t_seconds,key,edge`n"
    for line in EVENTS
        body := body line "`n"
    FileAppend(body, path, "UTF-8-RAW")   ; RAW = no BOM; a BOM hides the first '#'
                                          ; from a naive comment-stripping parser

    ; Measured stability, reported straight back: this is the number that says
    ; whether the run is usable, and it costs nothing to compute here.
    downs := []
    for line in EVENTS {
        p := StrSplit(line, ",")
        if (p.Length = 3 && p[3] = "down" && p[2] = "pull")
            downs.Push(Number(p[1]))
    }
    msg := "period " periodMs " ms, mode " mode "`nhalf-steps: " halves
    if (downs.Length > 2) {
        gaps := []
        total := 0.0
        idx := 1
        while (idx < downs.Length) {
            v := downs[idx + 1] - downs[idx]
            gaps.Push(v)
            total := total + v
            idx := idx + 1
        }
        mean := total / gaps.Length
        ss := 0.0
        for v in gaps
            ss := ss + (v - mean) * (v - mean)
        sd := Sqrt(ss / gaps.Length)
        msg := msg "`ncycles: " gaps.Length
        msg := msg "`nmeasured period: " Format("{:.2f}", mean * 1000) " ms"
        msg := msg "`njitter (sd): " Format("{:.2f}", sd * 1000) " ms"
    }
    TrayTip(msg "`n" path, "cadence done")
}

Cleanup(*) {
    ReleaseAll()
    DllCall("winmm\timeEndPeriod", "UInt", 1)
}

; --------------------------------------------------------------------- hotkeys
;
; ALL on F13-F24, on purpose.  AHK hotkeys are global and CONSUME the key, so a
; binding on any key a real keyboard has would break that key everywhere for as long
; as the script runs - in the game, in an editor, anywhere.  F13-F24 are legal
; virtual keys that virtually no physical keyboard emits and nothing else claims, so
; there is nothing to collide with.  Bind them on the Razer keyboard (Synapse can
; remap any key to F13-F24; that is the standard trick for exactly this).
;
; Ordered by period, longest first - the long ones carry by far the strongest signal.
;
;   F13  1300 ms    F16   570 ms  (recorded)     F19  400 ms duty
;   F14   930 ms    F17   370 ms  (recorded)     F20  230 ms duty
;   F15   700 ms    F18   230 ms  (recorded)     F21  STOP
;
F13::RunCadence(1300, "alt")     ; extends the curve where the signal is largest
F14::RunCadence(930, "alt")
F15::RunCadence(700, "alt")      ; fills the gap between 570 and 930
F16::RunCadence(570, "alt")
F17::RunCadence(370, "alt")
F18::RunCadence(230, "alt")

; Duty self-checks.  These measure a MEAN rate, not a ripple, so they work at any
; period - which makes them the control for whether short presses reach the game at
; all.  If the presses land, mean pitch rate must be half the full-deflection rate
; (~16.5 deg/sim-s) regardless of how fast the cadence is.
F19::RunCadence(400, "duty")     ; 200 ms presses
F20::RunCadence(230, "duty")     ; 115 ms presses - the control for the 230 ms null

F21::                            ; panic: stop and release both keys
{
    global RUNNING
    RUNNING := false
    ReleaseAll()
}

; ----------------------------------------------------------------------- NOTES
;
; HOW TO FLY IT.  Get level at a fixed throttle (both CAP-04 takes entered at
; ~300 mph, which makes them directly comparable), start recording, then press the
; cadence key and leave the keyboard alone for ~20 s.  Press the same key again, or
; F21, to stop.  Do every period in one session - tau is fitted from the RATIO of
; ripple amplitudes ACROSS periods, which cancels the unknown gain from body rate to
; flight path.  One period on its own cannot separate the two.
;
; IF THE F13+ REMAP IS NOT ACTIVE nothing will start, so there is nothing to stop -
; but if you ever need to kill a run without the keys, right-click the tray icon and
; Exit.  OnExit releases both arrow keys, so exiting is always a clean full stop.
;
; THE BEEP IS DELIBERATE.  One beep at start, two at stop.  Recorded in the video's
; audio track, it marks the run boundaries and lets the log be aligned to the
; footage without guessing.  Do not mute the capture.
;
; THE SELF-CHECK (F9) IS WORTH THE 20 SECONDS.  It pulses the pull key alone at 50%
; duty.  If the game is receiving every pulse, the mean pitch rate must come out at
; half the full-deflection rate - about 16.5 deg/sim-s.  If it comes out
; significantly lower, the game is quantising key state and the input waveform is
; not what this script thinks it is; say so before recording the rest.
;
; IF THE GAME IGNORES THE KEYS.  In order: (1) run this script as administrator -
; a non-elevated script cannot send input to an elevated window; (2) try windowed
; rather than exclusive-fullscreen mode; (3) if it still ignores them, the game is
; reading raw DirectInput device state, which SendInput cannot reach - tell me and
; I will rework this against an interception driver instead.
;
; WHAT I NEED BACK.  The video files, and the cadence-logs\*.csv for each run.  The
; CSV is what turns this from "we think it was 230 ms" into a measured input
; waveform with its jitter quantified.
