# Captures the launch-resolution baseline: every row below is run through --dump-session and its
# resolved settings appended to baseline.txt.
#
# The matrix is the point of this file. It is weighted toward the modes the pixel goldens do not
# reach at all (--anim-lab, --stunt, splitscreen, the menu), because those are the ones where a
# green RunTests.ps1 says nothing about whether a launch still resolves the way it used to.
#
# Re-run it after any change to argument parsing or mode arbitration and diff against the committed
# baseline. A row that moves is either the change you meant or the one you did not.
#
#   ./analysis/session-baseline/capture.ps1               # rewrite baseline.txt in place
#   ./analysis/session-baseline/capture.ps1 -Out new.txt  # capture elsewhere, then diff by hand

param(
    [string]$Out = "$PSScriptRoot/baseline.txt"
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path "$PSScriptRoot/../.."
$godot = Join-Path $repo "tools/godot/Godot_v4.7-stable_mono_win64/Godot_v4.7-stable_mono_win64_console.exe"
if (-not (Test-Path $godot)) {
    throw "Godot not found at $godot -- see docs/tooling.md"
}

# The matrix itself lives beside this file, so any other instrument runs exactly the same command lines.
. "$PSScriptRoot/matrix.ps1"
$sb = [System.Text.StringBuilder]::new()
$null = $sb.Append("# CSVM launch-resolution baseline -- regenerate with analysis/session-baseline/capture.ps1`n")
$null = $sb.Append("# $($matrix.Count) command lines. No timestamp and no absolute path: this file is diffed, not read once.`n")

$failed = 0
$i = 0
foreach ($label in $matrix.Keys) {
    $i++
    $userArgs = @("--dump-session") + $matrix[$label]
    Write-Host ("[{0,2}/{1}] {2}" -f $i, $matrix.Count, $label)
    # Not $out: PowerShell variable names are case-insensitive, so that would silently overwrite
    # the $Out parameter holding the destination path.
    $captured = & $godot --headless --path (Join-Path $repo "CSVM") "res://scenes/Main.tscn" "--" @userArgs 2>&1
    $code = $LASTEXITCODE
    if ($code -ne 0) {
        $failed++
        Write-Host "        exit $code" -ForegroundColor Red
    }
    # Keep only the report itself. Everything above it is the engine's own startup chatter, which
    # carries a log filename with a timestamp in it and would make the baseline differ every run.
    $lines = @($captured | ForEach-Object { "$_" })
    $start = -1
    for ($j = 0; $j -lt $lines.Count; $j++) {
        if ($lines[$j] -like "# session dump*") { $start = $j; break }
    }
    $null = $sb.Append("`n===== $label (exit $code) =====`n")
    if ($start -lt 0) {
        $null = $sb.Append("!! no report produced`n")
        continue
    }
    for ($j = $start; $j -lt $lines.Count; $j++) {
        if ($lines[$j] -match '^\d+ settings') { break }
        $null = $sb.Append($lines[$j].TrimEnd()).Append("`n")
    }
}

# UTF-8 without BOM, LF: the probe already emits LF, and a BOM would show up as a diff hunk on the
# first line for anything reading the file as text.
[System.IO.File]::WriteAllText($Out, $sb.ToString().Replace("`r`n", "`n"), (New-Object System.Text.UTF8Encoding($false)))
Write-Host ""
Write-Host "$($matrix.Count) rows -> $Out" -ForegroundColor Green
if ($failed -gt 0) {
    Write-Host "$failed row(s) exited nonzero" -ForegroundColor Red
    exit 1
}
