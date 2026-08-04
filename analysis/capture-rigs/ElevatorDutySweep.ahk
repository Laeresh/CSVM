; ElevatorDutySweep.ahk - scripted elevator input for the CAP-10 re-record (BL-109)
;
; WHY THIS EXISTS.  CAP-10's five hand-flown takes settled that the engine note is
; NOT driven by climb rate: the take flown specifically to vary climb rate correlates
; at R2 = 0.188, and the 90 deg-banked take swings the note 6.2% while holding
; altitude to 168 ft over 15 s.  Airspeed (0.13-0.21), flight-path angle and energy
; bleed are all worse.  What is left is the ELEVATOR INPUT itself - which is a stick
; position, not a state, so no gauge in the footage carries it.  The only way to
; correlate against it is to know it, i.e. to script it.
;
; THE DESIGN.  Alternating nose-up / nose-down at a fixed period holds the MEAN pitch
; rate at zero, so the aircraft porpoises about level and altitude, speed and
; flight-path angle all stay put (the same trick pitch_cadence.ahk uses for tau).
; On top of that this rig steps the DUTY CYCLE - the fraction of each half-period the
; key is actually held down:
;
;     duty 0.00   stick never touched          <- the note's own baseline
;     duty 0.50   deflected half the time
;     duty 1.00   always deflected, alternating direction
;
; Every step has the same mean rate and therefore the same flight state; only the
; amount of elevator deflection changes.  So if the note steps with duty, the driver
; is the input, and the staircase reads the coefficient straight off.  If the note
; stays flat across the whole sweep, the input is NOT the driver either and the
; hypothesis is dead - which is equally a result.  The sweep runs up and back down so
; ordering and hysteresis are visible rather than assumed.
;
; BEEPS.  Start and end are marked with SoundBeep, as in pitch_cadence.ahk.  On this
; capture the recording's AUDIO IS THE MEASUREMENT, so that would normally be
; unacceptable - but the user's rig records the GAME's audio only, not the system
; mixer, so the beeps do not reach the file (confirmed by the user, 2026-08-04).
; ⚠ If the capture setup ever changes to a system/desktop-audio recording, set
; BEEP := false before recording: a 1.2 kHz tone lands squarely in the 400-900 Hz to
; ~2 kHz range this analysis reads, and would be indistinguishable from engine
; harmonics near the step boundaries - i.e. exactly where the measurement is taken.
; Alignment does not depend on them either way (capture-rigs rule 1: the log gives the
; boundaries, the first visibly-pitching frame gives the single offset); they are a
; convenience for finding the run in a long recording.
;
; REQUIRES AutoHotkey v2.0.  F13 runs the sweep, F14 runs the sustained-hold ladder,
; Esc aborts and releases everything.

; ⚠ SEND MODE IS NOT A STYLE CHOICE.  The first version of this rig used SendInput,
; which typed fine into Notepad and did NOTHING in the game.  SendInput injects a
; whole batch atomically with no gap between events, and the original samples key
; state once per frame, so the edges land inside a single sample and are never seen.
; SendMode "Event" goes through keybd_event one event at a time with a real delay,
; which is what NumpadViewSweep.ahk and NumpadComboSweep.ahk both use and why they
; work.  Do not "optimise" this back to SendInput.
;
; ⚠ Note for pitch_cadence.ahk: it uses SendInput, and its header records that it was
; verified "with the key sending stubbed out so the test could not type into
; anything" - i.e. its TIMING is verified but its delivery into the game never was.
; It may well have this same bug.  Check before trusting a CAP-04 re-record from it.

#Requires AutoHotkey v2.0
#SingleInstance Force
InstallKeybdHook()
SendMode "Event"
SetKeyDelay 20, 20

; ---------------------------------------------------------------- configuration
; Scan codes, not characters, and EXTENDED (leading 1) - DirectInput-era games read
; scan codes.  sc148 = Up arrow, sc150 = Down arrow.  NOT sc048/sc050, which are
; numpad 8/2 and are the CAMERA in the original: getting this wrong swings the view
; mid-measurement instead of pitching the aircraft.
KEY_PUSH := "sc148"        ; Up arrow   - nose down
KEY_PULL := "sc150"        ; Down arrow - nose up

; 600 ms, not 400, because of the send mode above: the shortest hold in the sweep is
; the lowest non-zero duty, 0.25 * half.  At 400 ms that is 50 ms - about 1.5 frames
; at 30 fps, and only 2.5x SetKeyDelay.  At 600 ms it is 75 ms, ~2.3 frames, which a
; once-per-frame sampler cannot miss.  Raise this, never lower it.
PERIOD_MS := 600           ; one full pull+push cycle
STEP_S    := 6.0           ; seconds per duty step - long enough for a readable plateau
DUTIES    := [0.00, 0.25, 0.50, 0.75, 1.00, 0.75, 0.50, 0.25, 0.00]
; Sustained-hold ladder (F14): held pull, then an equal recovery gap, growing.
HOLDS_MS  := [250, 500, 1000, 1500, 2000]
GAP_S     := 4.0

LOG_DIR := A_ScriptDir
SPIN_MS := 0.025           ; busy-spin window; see pitch_cadence.ahk for why 25 ms
BEEP    := true            ; audible start/end markers; see the header before changing

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
    global SPIN_MS, RUNNING
    loop {
        r := t - Now()
        if (r <= 0)
            return
        if (GetKeyState("Escape", "P")) {
            RUNNING := false
            return
        }
        if (r > SPIN_MS)
            Sleep(Integer((r - SPIN_MS) * 1000))
    }
}

Mark(t, what) {
    global EVENTS
    EVENTS.Push(Format("{:.6f},{}", t, what))
}

ReleaseAll() {
    global KEY_PULL, KEY_PUSH
    Send "{" KEY_PULL " up}"
    Send "{" KEY_PUSH " up}"
}

; SoundBeep BLOCKS for its full duration, so the start marker must sound BEFORE the
; run's t0 is taken - otherwise the first half-cycle is already 120 ms late against a
; schedule that is then absolute for the rest of the run.  The end markers sound after
; the keys are released, so they cannot delay an edge at all.
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

; ------------------------------------------------------------------ duty sweep
RunSweep() {
    global RUNNING, EVENTS, KEY_PULL, KEY_PUSH, PERIOD_MS, STEP_S, DUTIES
    if (RUNNING) {
        RUNNING := false
        return
    }
    RUNNING := true
    EVENTS := []
    half := PERIOD_MS / 2000.0                  ; seconds per half-cycle
    BeepStart()                                 ; before t0 - see BeepStart()
    t0 := Now()
    Mark(0.0, "run,start,period_ms=" PERIOD_MS)
    tNext := 0.0

    for idx, duty in DUTIES {
        Mark(Now() - t0, "step,begin,duty=" Format("{:.2f}", duty))
        nHalves := Integer(Round(STEP_S / half))
        held := duty * half                     ; seconds held within each half-cycle
        loop nHalves {
            if (!RUNNING)
                break
            i := A_Index - 1
            key  := Mod(i, 2) = 0 ? KEY_PULL : KEY_PUSH
            name := Mod(i, 2) = 0 ? "pull" : "push"
            ; press at the half-cycle boundary, release `held` seconds later
            WaitUntil(t0 + tNext)
            if (!RUNNING)
                break
            if (duty > 0) {
                Send "{" key " down}"
                Mark(Now() - t0, name ",down")
                WaitUntil(t0 + tNext + held)
                Send "{" key " up}"
                Mark(Now() - t0, name ",up")
            }
            tNext := tNext + half
        }
        if (!RUNNING)
            break
    }
    ReleaseAll()
    Mark(Now() - t0, "run,stop")
    BeepEnd()
    WriteLog("duty-sweep")
}

; --------------------------------------------------------- sustained-hold ladder
; One-sided held pulls of growing length, each followed by an equal-or-longer gap.
; This one DOES move the aircraft - it is the cross-check against the hand-flown
; banked take, where the note stayed elevated for as long as the stick was held.
RunHolds() {
    global RUNNING, EVENTS, KEY_PULL, HOLDS_MS, GAP_S
    if (RUNNING) {
        RUNNING := false
        return
    }
    RUNNING := true
    EVENTS := []
    BeepStart()                                  ; before t0 - see BeepStart()
    t0 := Now()
    Mark(0.0, "run,start,mode=holds")
    t := GAP_S                                   ; a baseline gap before the first pull
    for idx, ms in HOLDS_MS {
        if (!RUNNING)
            break
        WaitUntil(t0 + t)
        if (!RUNNING)
            break
        Send "{" KEY_PULL " down}"
        Mark(Now() - t0, "pull,down,hold_ms=" ms)
        WaitUntil(t0 + t + ms / 1000.0)
        Send "{" KEY_PULL " up}"
        Mark(Now() - t0, "pull,up,hold_ms=" ms)
        t := t + ms / 1000.0 + GAP_S
    }
    ReleaseAll()
    Mark(Now() - t0, "run,stop")
    BeepEnd()
    WriteLog("hold-ladder")
}

; ------------------------------------------------------------------------- log
WriteLog(mode) {
    global EVENTS, LOG_DIR, PERIOD_MS, STEP_S, DUTIES
    stamp := FormatTime(, "yyyyMMdd-HHmmss")
    path := LOG_DIR "\elevator-" mode "-" stamp "-log.txt"
    body := "# ElevatorDutySweep.ahk mode=" mode " period_ms=" PERIOD_MS
    body := body " step_s=" STEP_S "`n"
    body := body "# t_seconds,event`n"
    for line in EVENTS
        body := body line "`n"
    FileAppend(body, path, "UTF-8-RAW")          ; RAW = no BOM; a BOM hides the '#'
    ToolTip("written: " path)
    SetTimer(() => ToolTip(), -4000)
}

Cleanup(*) {
    ReleaseAll()
    DllCall("winmm\timeEndPeriod", "UInt", 1)
}

F13:: RunSweep()
F14:: RunHolds()
Esc:: {
    global RUNNING
    RUNNING := false
    ReleaseAll()
}
