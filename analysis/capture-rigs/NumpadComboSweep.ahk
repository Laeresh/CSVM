#Requires AutoHotkey v2.0
; ---------------------------------------------------------------------------
; CAP-08 rig - numpad camera-view KEY COMBINATIONS
;
; Press F13 to run one sweep. Esc aborts and releases everything held.
;
; Each step is staggered on purpose, and that is the whole design:
;
;     press the first key  ->  SETTLE_MS  (the camera reaches that key's own
;                                          fixed position, alone)
;     add the rest         ->  HOLD_MS    (the combined position)
;     release all          ->  GAP_MS     (ease back to base)
;
; Pressing a combination simultaneously would only show where it ends up.
; Arriving at a single key's position FIRST and then adding the second, in one
; continuous shot, shows what adding it actually does - whether the second key
; blends toward a further position, replaces the first outright, or is ignored
; while the first is still down. That is exactly the question BL-150(d) is
; open on, and it cannot be read off a still of the end state alone.
;
; Every press and release is timestamped into combo-log.txt next to this
; script, so the analysis need not infer event boundaries from motion.
; ---------------------------------------------------------------------------

; ---- tunables -------------------------------------------------------------
LEAD_MS   := 3000       ; straight-and-level baseline before the first step
SETTLE_MS := 2500       ; first key alone, long enough to reach its position
HOLD_MS   := 3500       ; the combination held
GAP_MS    := 3500       ; recovery to base after release
TAIL_MS   := 3000       ; baseline after the last step

SHOW_TOOLTIP := true    ; on-screen label. Needs windowed or borderless mode -
                        ; it will not draw over an exclusive-fullscreen game.

; The eight camera keys ring the numpad clockwise: 7 8 9 6 3 2 1 4. The pairs
; below are the eight ADJACENT pairs around that ring (most likely to blend
; into an intermediate position), then the four OPPOSITE pairs (which test the
; contradictory case - cancel, first-wins, or something else), then two
; triples. 5 is omitted: it is unbound. 0 is omitted: it is rudder-left.
;
; Trim this list if the run is too long - each step costs about 9.5 s, so as
; written the sweep is a little over two minutes.
STEPS := [
    ["7","8"], ["8","9"], ["9","6"], ["6","3"],     ; adjacent, clockwise
    ["3","2"], ["2","1"], ["1","4"], ["4","7"],
    ["1","9"], ["3","7"], ["2","8"], ["4","6"],     ; opposite
    ["7","8","9"], ["1","2","3"] ]                  ; triples

; Scancodes rather than {NumpadN}: they survive NumLock state and are what a
; DirectInput-era game actually reads.
SC := Map("0","sc052", "1","sc04F", "2","sc050", "3","sc051", "4","sc04B",
          "5","sc04C", "6","sc04D", "7","sc047", "8","sc048", "9","sc049")
; ---------------------------------------------------------------------------

SendMode "Event"
SetKeyDelay 20, 20
SetNumLockState "AlwaysOn"

LOGPATH := A_ScriptDir "\combo-log.txt"
t0 := 0
held := []

Note(text) {
    global t0, LOGPATH
    FileAppend Format("{:8.3f}  {}`n", (A_TickCount - t0) / 1000, text), LOGPATH, "UTF-8"
}

Say(text) {
    global SHOW_TOOLTIP
    if SHOW_TOOLTIP
        ToolTip text, 40, 40
}

Join(a, sep := ",") {
    s := ""
    for v in a
        s .= (s = "" ? "" : sep) v
    return s
}

Push(k) {
    global held, SC
    Send "{" SC[k] " down}"
    held.Push(k)
    Note "DOWN  Numpad" k "        held=" Join(held, "+")
}

ReleaseAll() {
    global held, SC
    while held.Length {
        k := held.Pop()
        Send "{" SC[k] " up}"
        Note "UP    Numpad" k "        held=" (held.Length ? Join(held, "+") : "-")
    }
}

Esc:: {
    ReleaseAll()
    ToolTip
    Note "ABORTED"
    ExitApp
}

F13:: {
    global t0, held, STEPS, SC, LEAD_MS, SETTLE_MS, HOLD_MS, GAP_MS, TAIL_MS, LOGPATH

    t0 := A_TickCount
    held := []
    try FileDelete LOGPATH
    Note "COMBO SWEEP START  lead=" LEAD_MS " settle=" SETTLE_MS " hold=" HOLD_MS " gap=" GAP_MS
    Note "steps=" STEPS.Length

    Say "baseline"
    Sleep LEAD_MS

    for i, step in STEPS {
        label := Join(step, "+")
        Note "STEP " i " of " STEPS.Length "  " label

        ; --- first key alone, let it reach its own fixed position ---
        Push step[1]
        Say "[" i "/" STEPS.Length "]  " step[1] "   (alone, settling)"
        Sleep SETTLE_MS

        ; --- add the rest, together ---
        loop step.Length - 1
            Push step[A_Index + 1]
        Say "[" i "/" STEPS.Length "]  " label "   (combined)"
        Sleep HOLD_MS

        ReleaseAll()
        Say "-- base --"
        Sleep GAP_MS
    }

    Say "done"
    Note "COMBO SWEEP END"
    Sleep TAIL_MS
    ToolTip
    MsgBox "Combo sweep complete - " STEPS.Length " steps.`n`nLog: " LOGPATH, "CAP-08", "T3"
}
