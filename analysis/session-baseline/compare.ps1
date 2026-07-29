# The SessionSpec equivalence gate: runs every command line in matrix.ps1 with
# --dump-session=compare, which resolves each launch setting TWICE in one process -- once through
# PlaneViewer's own fields, once through SessionSpec -- and exits nonzero on any disagreement.
#
# Two resolutions can only be compared while both exist. That is the whole reason this is a probe
# in a live PlaneViewer rather than a unit test: the field side has no other home, and it stops
# existing the moment the migration deletes those fields.
#
# A clean run here proves the RULES match. It says nothing about the call-site edits that follow --
# that is a different failure mode with a different signal, and a green gate here must never be
# cited as evidence for it.
#
#   ./analysis/session-baseline/compare.ps1          # every row, one PASS/FAIL line each
#   ./analysis/session-baseline/compare.ps1 -Verbose # plus the full two-column report per row

param(
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path "$PSScriptRoot/../.."
$godot = Join-Path $repo "tools/godot/Godot_v4.7-stable_mono_win64/Godot_v4.7-stable_mono_win64_console.exe"
if (-not (Test-Path $godot)) {
    throw "Godot not found at $godot -- see docs/tooling.md"
}

. "$PSScriptRoot/matrix.ps1"

$failed = 0
$compared = 0
$i = 0
foreach ($label in $matrix.Keys) {
    $i++
    $userArgs = @("--dump-session=compare") + $matrix[$label]
    $captured = & $godot --headless --path (Join-Path $repo "CSVM") "res://scenes/Main.tscn" "--" @userArgs 2>&1
    $code = $LASTEXITCODE
    $lines = @($captured | ForEach-Object { "$_" })
    $summary = ($lines | Where-Object { $_ -match "settings compared" } | Select-Object -First 1)
    $mismatches = @($lines | Where-Object { $_ -match "^!! " })
    if ($summary -match "^(\d+) of") { $compared += [int]$Matches[1] }

    # The exit code is the verdict; the mismatch lines are why. A row that produced no report at
    # all is a failure too -- silence is not agreement.
    if ($code -ne 0 -or $null -eq $summary) {
        $failed++
        Write-Host ("[{0,2}/{1}] FAIL {2}  exit {3}" -f $i, $matrix.Count, $label, $code) -ForegroundColor Red
        foreach ($m in $mismatches) { Write-Host "         $m" -ForegroundColor Red }
        if ($null -eq $summary) { Write-Host "         no report produced" -ForegroundColor Red }
    }
    else {
        Write-Host ("[{0,2}/{1}] ok   {2}" -f $i, $matrix.Count, $label)
    }
    if ($Verbose) {
        $start = -1
        for ($j = 0; $j -lt $lines.Count; $j++) {
            if ($lines[$j] -like "# session compare*") { $start = $j; break }
        }
        if ($start -ge 0) {
            for ($j = $start; $j -lt $lines.Count; $j++) { Write-Host "    $($lines[$j])" }
        }
    }
}

Write-Host ""
if ($failed -gt 0) {
    Write-Host "$failed of $($matrix.Count) row(s) DISAGREE -- see the mismatch lines above" -ForegroundColor Red
    exit 1
}
Write-Host "$($matrix.Count) rows agree, $compared field/spec value pairs compared" -ForegroundColor Green
