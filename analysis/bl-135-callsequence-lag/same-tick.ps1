# How many sequences can ONE definition legitimately start inside ONE tick?
#
# That number is the bound on the same-pass CALL_SEQUENCE drain (BL-135 /
# PLAN-m3-polish-6 B12): the drain must let every authored same-tick chain complete, and must
# stop a cyclic one. This walks each definition's sequences statically and counts the
# CALL_SEQUENCE events reachable before the clock moves.
#
# Rules, matching SequenceRunner:
#   * an event schedules time when its start offset is > 0, or its kind is one the dispatch
#     table returns a duration for (OBJECT_MOTION_FROM_TO / _OPACITY_FROM_TO / OBJECT_MOTION /
#     OBJECT_MOTION_SI_SCRIPT). Everything else is instantaneous.
#     ⚠ ObjectMotionSiScriptAllNames is NOT in that list — it has no handler, so it falls to
#       Dispatch's `default` and reports duration 0. That is what makes marypickford's cycle
#       instantaneous IN THIS ENGINE.
#   * a LOOP ends the tick (an instantaneous iteration is gated one AnimFrame; a timed one
#     waits on _due), so the walk stops there.
#   * every IF branch is assumed taken — a worst case, since the conditions are dynamic.
#
# Read-only, writes to .scratch/; no game data is committed.
#
#   .\analysis\bl-135-callsequence-lag\same-tick.ps1 [-Extracted Z:\CSVM\extracted] [-Out .scratch\bl-135]

[CmdletBinding()]
param(
    [string]$Extracted = (Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) 'extracted'),
    [string]$Out = (Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) '.scratch\bl-135')
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $Out | Out-Null

$Timed = @('ObjectMotionFromTo', 'ObjectOpacityFromTo', 'ObjectMotion', 'ObjectMotionSiScript')

$files = Get-ChildItem $Extracted -Recurse -Filter *.json -File |
    Where-Object { $_.FullName -match '\\(mis_anim|cam_anim)\\' }
$candidates = $files | Where-Object { (Get-Content $_.FullName -Raw) -match '"CallSequence"' }
Write-Host "scanning $($candidates.Count) CallSequence-bearing compiled anim files"

$rows = @()
foreach ($f in $candidates) {
    $rel = $f.FullName.Substring($Extracted.Length).TrimStart('\')
    $j = Get-Content $f.FullName -Raw | ConvertFrom-Json

    $byName = @{}
    $entries = @()
    if ($j.reset_state) { $entries += [pscustomobject]@{ name = '<reset_state>'; events = $j.reset_state.events } }
    foreach ($s in $j.sequences) {
        $e = [pscustomobject]@{ name = "$($s.name)"; events = $s.events; onCall = ($s.seq_state -eq 'OnCall') }
        $byName["$($s.name)".ToLowerInvariant()] = $e
        $entries += $e
    }

    # Sequences a CALL_SEQUENCE starts in the same tick as $seq, transitively.
    function StartedBy($seq, $seen) {
        $started = @()
        if (-not $seq) { return $started }
        foreach ($ev in $seq.events) {
            if ($ev.start -and $ev.start.time -gt 0) { break }
            $d = $ev.data
            $kind = ($d.PSObject.Properties | Select-Object -First 1).Name
            if ($kind -eq 'Loop') { break }              # the tick ends at a LOOP
            if ($Timed -contains $kind) { break }        # and at the first event that takes time
            if ($kind -ne 'CallSequence') { continue }
            $target = "$($d.CallSequence.name)".ToLowerInvariant()
            $started += $target
            if ($seen -contains $target) { continue }    # a cycle: counted once, not followed
            $started += StartedBy $byName[$target] ($seen + $target)
        }
        return $started
    }

    foreach ($e in $entries) {
        $chain = @(StartedBy $e @($e.name.ToLowerInvariant()))
        if ($chain.Count -eq 0) { continue }
        $cyclic = ($chain | Group-Object | Where-Object Count -gt 1).Count -gt 0 -or
                  ($chain -contains $e.name.ToLowerInvariant())
        $rows += [pscustomobject]@{
            file = $rel; anim = "$($j.name)"; activation = "$($j.activation)"
            sequence = $e.name; started = $chain.Count; cyclic = $cyclic
            chain = ($chain -join ' -> ')
        }
    }
}

$rows | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $Out 'same-tick.json') -Encoding utf8

Write-Host ""
Write-Host "entry sequences that start another sequence in the same tick: $($rows.Count)"
$rows | Group-Object started | Sort-Object { [int]$_.Name } | ForEach-Object {
    Write-Host ("  starts {0,2} sequence(s) in one tick: {1} entry sequence(s)" -f $_.Name, $_.Count)
}
Write-Host ""
Write-Host "worst cases:"
$rows | Sort-Object started -Descending |
    Group-Object { "$($_.anim)|$($_.sequence)|$($_.started)" } |
    Select-Object -First 10 | ForEach-Object {
        $r = $_.Group[0]
        Write-Host ("  {0,-22} '{1}' starts {2} (x{3} file(s)) {4}" -f $r.anim, $r.sequence, $r.started, $_.Count, $r.chain)
    }
Write-Host ""
Write-Host "wrote $(Join-Path $Out 'same-tick.json')"
