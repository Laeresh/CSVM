#Requires AutoHotkey v2.0
; ---------------------------------------------------------------------------
; CAP-07 re-record rig - numpad camera-view sweep
;
; Press F13 to run one sweep. Each camera key is held ALONE for HOLD_MS, then
; released, and the camera is given GAP_MS to ease all the way back to base
; before the next key goes down.
;
; That separation is the whole point. In the first take the presses overlapped:
; the camera lerped straight from one fixed position to the next without ever
; passing through base, and several holds never reached a stable pose, so no
; single frame could be trusted as "this is where key N puts the camera".
;
; Esc aborts mid-sweep and releases whatever is held.
;
; Every press and release is timestamped into sweep-log.txt next to this
; script, so the analysis does not have to infer event boundaries from motion.
; ---------------------------------------------------------------------------

; ---- tunables -------------------------------------------------------------
LEAD_MS := 3000         ; straight-and-level baseline before the first key
HOLD_MS := 3500         ; how long each key is held down
GAP_MS  := 3500         ; recovery to base after release, before the next key
TAIL_MS := 3000         ; baseline after the last key

SHOW_TOOLTIP := true    ; on-screen label of the active key. Needs windowed or
                        ; borderless-windowed mode - it will not draw over an
                        ; exclusive-fullscreen game.

; 5 is included deliberately: BL-150(e) records it as unbound on the strength
; of assumption, and a held-with-no-movement clip is what turns that into
; evidence. 0 is NOT included - it is rudder-left, not a camera key.
KEYS := ["1", "2", "3", "4", "5", "6", "7", "8", "9"]

; Scancodes rather than {Numpad1}: they survive NumLock state and are what a
; DirectInput-era game actually reads.
SC := Map("0","sc052", "1","sc04F", "2","sc050", "3","sc051", "4","sc04B",
          "5","sc04C", "6","sc04D", "7","sc047", "8","sc048", "9","sc049")
; ---------------------------------------------------------------------------

SendMode "Event"
SetKeyDelay 20, 20
SetNumLockState "AlwaysOn"

LOGPATH := A_ScriptDir "\sweep-log.txt"
t0 := 0
held := ""

Note(text) {
    global t0, LOGPATH
    FileAppend Format("{:8.3f}  {}`n", (A_TickCount - t0) / 1000, text), LOGPATH, "UTF-8"
}

Say(text) {
    global SHOW_TOOLTIP
    if SHOW_TOOLTIP
        ToolTip text, 40, 40
}

Release() {
    global held, SC
    if held != "" {
        Send "{" SC[held] " up}"
        Note "UP    Numpad" held
        held := ""
    }
}

Join(a) {
    s := ""
    for v in a
        s .= (s = "" ? "" : ",") v
    return s
}

Esc:: {
    Release()
    ToolTip
    Note "ABORTED"
    ExitApp
}

F13:: {
    global t0, held, KEYS, SC, LEAD_MS, HOLD_MS, GAP_MS, TAIL_MS, LOGPATH

    t0 := A_TickCount
    try FileDelete LOGPATH
    Note "SWEEP START  lead=" LEAD_MS " hold=" HOLD_MS " gap=" GAP_MS "  keys=" Join(KEYS)

    Say "baseline"
    Sleep LEAD_MS

    for k in KEYS {
        held := k
        Send "{" SC[k] " down}"
        Note "DOWN  Numpad" k
        Say "Numpad " k "   (held)"
        Sleep HOLD_MS

        Release()
        Say "-- base --"
        Sleep GAP_MS
    }

    Say "done"
    Note "SWEEP END"
    Sleep TAIL_MS
    ToolTip
    MsgBox "Sweep complete - " KEYS.Length " keys.`n`nLog: " LOGPATH, "CAP-07", "T3"
}
