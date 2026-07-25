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

# label -> user args. Grouped by what each group is here to pin down; the label is what a diff
# names, so keep them stable.
$matrix = [ordered]@{
    # No content arg: the launchscreen base every menu launch is patched on top of.
    "menu-bare"             = @()
    "menu-forced"           = @("--menu")
    "menu-screen"           = @("--menu=plane")
    "menu-over-content"     = @("--menu", "--chapter=C4", "--plane=player_fury")

    # Flight -- the default for any content arg.
    "fly-bare"              = @("--fly")
    "fly-plane"             = @("--plane=player_fury")
    "fly-chapter"           = @("--chapter=C4")
    "fly-mission-spawn"     = @("--fly", "--chapter=C2", "--mission=M01", "--spawn=3")
    "fly-pos-dir"           = @("--fly", "--pos=100,200,300", "--direction=0,0,-1")
    "fly-pos-lookat"        = @("--fly", "--pos=100,200,300", "--lookat=0,0,0")
    "fly-dir-no-pos"        = @("--fly", "--direction=0,0,-1")
    "fly-view"              = @("--fly", "--view=4")
    "fly-deprecated"        = @("--fly", "--campos=1,2,3", "--spawn-at=4,5,6", "--spawn-dir=0,0,-1")

    # The --det bundle: implied, opted out of, and overridden constituent by constituent.
    "det-explicit"          = @("--fly", "--det")
    "det-implied-shot"      = @("--fly", "--screenshot=.scratch/session-baseline.png")
    "det-opted-out"         = @("--fly", "--screenshot=.scratch/session-baseline.png", "--no-det")
    "det-overridden"        = @("--fly", "--screenshot=.scratch/session-baseline.png", "--seed=7", "--spawn=2", "--paint-seed=99", "--jitter=0.5")
    "det-burst"             = @("--fly", "--screenshot=.scratch/session-baseline.png", "--shots=4", "--frames=30")

    # Stunt -- no golden covers it.
    "stunt-bare"            = @("--stunt")
    "stunt-scenario"        = @("--stunt", "--scenario=zeppelin_run")
    "stunt-2p"              = @("--stunt", "--plane=player_bhawk,player_fury")

    # Splitscreen -- no golden covers it either, and the count arrives three different ways.
    "split-3p"              = @("--fly", "--players=3")
    "split-implied"         = @("--plane=player_bhawk,player_fury,player_kestrel")
    "split-clamped"         = @("--viewer", "--players=2")

    # The static inspection view and its labs.
    "viewer-bare"           = @("--viewer")
    "viewer-chapter"        = @("--viewer", "--chapter=C3")
    "viewer-damage"         = @("--viewer", "--damage")
    "viewer-beats-fly"      = @("--viewer", "--fly", "--plane=player_fury")
    "viewer-weaponlab"      = @("--weapon-lab=wep_06", "--weapon-mount=firepoint1")
    "viewer-node"           = @("--node=kkgate")

    # The spectator view.
    "freecam-bare"          = @("--freecam", "--chapter=C2")
    "freecam-collision"     = @("--freecam", "--chapter=C2", "--collision=show", "--debug-nodelab=deps")
    "freecam-beats-fly"     = @("--freecam", "--fly")
    "freecam-debug-damage"  = @("--freecam", "--debug-damage=kill")

    # The animation lab -- the most specific mode, and the one with the most field sites.
    "animlab-bare"          = @("--anim-lab", "--chapter=C2")
    "animlab-play"          = @("--play-anim=train")
    "animlab-beats-all"     = @("--anim-lab", "--fly", "--viewer", "--freecam")
    "animlab-node"          = @("--anim-lab", "--node=kkgate")

    # The synthetic stage, accepted and twice refused.
    "stage-empty"           = @("--stage=empty")
    "stage-empty-in-viewer" = @("--viewer", "--stage=empty")
    "stage-unknown"         = @("--stage=nosuch")

    # The probes that wear a mode as a disguise, coercing it at parse time.
    "probe-damage-test"     = @("--damage-test", "--damage-hd=25")
    "probe-effects-test"    = @("--effects-test")
    "probe-weapon-test"     = @("--weapon-test")
    "probe-run-tests"       = @("--run-tests=weapons")
    "probe-dump-flight"     = @("--dump-flight")

    # Modifiers that touch no mode.
    # --paint-color takes bytes, not floats, and repeats the last slot when given fewer than three.
    "paint-overrides"       = @("--fly", "--paint=none", "--paint-color=255,0,0/0,255,0/0,0,255", "--paint-decal=1,2,3")
    "paint-one-slot"        = @("--fly", "--paint-color=255,128,0")
    "tex-instruments"       = @("--freecam", "--tex-census", "--tex-override=foo", "--no-fog")
    "misc-modifiers"        = @("--fly", "--mute", "--no-vsync", "--perf", "--no-pads", "--infinite-ammo", "--fire", "--gun-select=1")
}

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
