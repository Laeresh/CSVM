# verify.ps1 — the anim-debugger plan's verification instrument (Waves 1-3, reusable for 4-5).
#
# Re-runs every scripted check the plan's waves were verified with, against the CURRENT build:
#   1. regression  — static plane-viewer screenshot md5 + C1/C5 world boot censuses, diffed
#                    against the committed baselines in this directory
#   2. quiet stage — --anim-lab boots with 0 ON_STARTUP / 0 startanims / 0 instances while
#                    reset states, mission setup and the safety net still run
#   3. clock rate  — a 610-frame scripted lab run of train_on_track logs exactly 10 debug
#                    sim-seconds (the regression test for the ManualAdvance 2x-clock bug:
#                    20 groups = the runtime's own _Process is ticking again)
#   4. determinism — two same-seed --shots=3 bursts are byte-identical; a different --frames
#                    run differs (the able-to-fail control, docs/verification.md rule 5)
#   5. seed        — same seed = same RandomWeight verdict sequence on C1/M05 random_prop;
#                    a differing seed takes a different branch (1 vs 3 at the time of writing)
#
# Outputs (PNGs, logs) go to .scratch/anim-lab-verify/ — they are ephemeral probe output and
# must NOT be committed (rendered frames are game-derived). This script and the baselines are
# the durable part; FINDINGS.md records the numbers each wave was accepted on.
#
# HEAD-vs-after A/B (how the wave regressions were actually run): run this script, `git stash`,
# rebuild, run it again into another -OutDir, `git stash pop`, rebuild, diff the two census
# sets and compare the two md5s. The screenshot md5 is machine- and window-size-dependent —
# only ever compare md5s produced on the same machine in the same session.
#
# The lab-vs-live train pose comparison (FINDINGS.md) is deliberately not fully scripted: the
# freecam side needs the LogMotions Take(12) cap raised in a throwaway build (the train's
# motions register late; docs/architecture.md AnimRuntime bullet). Check 3 covers the clock
# rate, which is the part that regressed once already.

param(
    [switch]$SkipBuild,
    [string]$OutDir = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$csvm = Join-Path $root 'CSVM'
$godot = Join-Path $root 'tools\godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe'
if ($OutDir -eq '') {
    $OutDir = Join-Path $root '.scratch\anim-lab-verify'
}
New-Item -ItemType Directory -Force $OutDir | Out-Null
if (-not (Test-Path $godot)) {
    Write-Host "FAIL: Godot not found at $godot"
    exit 1
}

if (-not $SkipBuild) {
    dotnet build (Join-Path $root 'CSVM\CSVM.sln') | Select-Object -Last 3
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'FAIL: build failed'
        exit 1
    }
}

$failures = @()

function Invoke-Game([string]$Name, [string[]]$GameArgs) {
    $log = Join-Path $OutDir ($Name + '.log')
    $err = Join-Path $OutDir ($Name + '.err.log')
    $all = @('--path', $csvm, 'res://scenes/Main.tscn', '--') + $GameArgs
    $quoted = ($all | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
    $p = Start-Process -FilePath $godot -ArgumentList $quoted `
        -RedirectStandardOutput $log -RedirectStandardError $err -NoNewWindow -PassThru -Wait
    if ($p.ExitCode -ne 0) {
        Write-Host "  note: $Name exited $($p.ExitCode)"
    }
    return $log
}

function Get-Census([string]$Log) {
    Get-Content -Encoding UTF8 $Log |
        Where-Object { $_ -match '^(anim:|loaded |clutter:|texture cycles:|texture scroll:|world:|mission setup:|weather)' } |
        ForEach-Object { $_ -replace '\d+ ms', 'X ms' }
}

function Get-Md5([string]$Path) {
    return (Get-FileHash -Algorithm MD5 $Path).Hash
}

function Check([string]$Name, [bool]$Ok, [string]$Detail) {
    if ($Ok) {
        Write-Host "PASS  $Name  $Detail"
    } else {
        Write-Host "FAIL  $Name  $Detail"
        $script:failures += $Name
    }
}

# ---- 1. regression: plane-viewer md5 + C1/C5 censuses vs the committed baselines ------------
$shot = Join-Path $OutDir 'bhawk.png'
Invoke-Game 'bhawk' @('--viewer', '--plane=player_bhawk', '--no-pads', '--mute', "--screenshot=$shot") | Out-Null
Write-Host ("INFO  plane-viewer md5 " + (Get-Md5 $shot) + "  (same-machine A/B only; 2026-07-23 value F1290254F2DDA1E3B8A9BCEE867D6C3D)")

foreach ($ch in 'C1', 'C5') {
    $log = Invoke-Game "census_$ch" @("--chapter=$ch", '--no-pads', '--mute', "--screenshot=$(Join-Path $OutDir "census_$ch.png")")
    $census = Get-Census $log
    $census | Out-File -Encoding utf8 (Join-Path $OutDir "census_$ch.txt")
    $baseline = Get-Content -Encoding UTF8 (Join-Path $PSScriptRoot "baseline_$($ch.ToLower()).census")
    $diff = Compare-Object $baseline $census
    Check "census-$ch" ($null -eq $diff) "$($census.Count) lines vs baseline"
    if ($null -ne $diff) {
        $diff | Format-Table | Out-String | Write-Host
    }
}

# ---- 2. quiet stage -------------------------------------------------------------------------
$log = Invoke-Game 'quiet' @('--anim-lab', '--chapter=C1', '--no-pads', '--mute', "--screenshot=$(Join-Path $OutDir 'quiet.png')")
$content = Get-Content -Encoding UTF8 $log
$quietLine = $content | Where-Object { $_ -match '^anim: 0 ON_STARTUP \+ 0 start anims running, 0 live instance' }
$stateLine = $content | Where-Object { $_ -match '^anim: .* state ops applied' }
$netLine = $content | Where-Object { $_ -match '^anim: safety net hid' }
Check 'quiet-stage' (($null -ne $quietLine) -and ($null -ne $stateLine) -and ($null -ne $netLine)) 'nothing plays; reset states + safety net still run'

# ---- 3. clock rate: 610 scripted frames = exactly 10 logged sim-seconds ---------------------
$log = Invoke-Game 'trainrate' @('--anim-lab', '--chapter=C1', '--play-anim=train_on_track', '--debug-anim',
    '--no-pads', '--mute', '--frames=610', "--screenshot=$(Join-Path $OutDir 'trainrate.png')")
$groups = @(Get-Content -Encoding UTF8 $log | Where-Object { $_ -match '^anim/debug: caboose' }).Count
Check 'clock-rate' ($groups -eq 10) "caboose logged $groups sim-second(s), want 10 (20 = the runtime's own _Process is ticking again)"

# ---- 4. determinism: same-seed bursts identical; different step count differs ---------------
function Get-BurstMd5s([string]$Name, [int]$Frames) {
    $base = Join-Path $OutDir "$Name.png"
    Invoke-Game $Name @('--anim-lab', '--chapter=C1', '--play-anim=mp_hangar3_open', '--seed=42',
        '--no-pads', '--mute', "--frames=$Frames", '--shots=3', '--jitter=0', "--screenshot=$base") | Out-Null
    return 0, 1, 2 | ForEach-Object { Get-Md5 (Join-Path $OutDir ("{0}_{1:D2}.png" -f $Name, $_)) }
}
$a = Get-BurstMd5s 'det_a' 90
$b = Get-BurstMd5s 'det_b' 90
$c = Get-BurstMd5s 'det_c' 30
$same = ($a[0] -eq $b[0]) -and ($a[1] -eq $b[1]) -and ($a[2] -eq $b[2])
Check 'determinism-same-seed' $same 'two seed-42 frames-90 bursts byte-identical'
Check 'determinism-control' ($a[0] -ne $c[0]) 'frames-30 differs from frames-90 (instrument can fail)'

# ---- 5. seed: same seed = same dice; a different seed branches differently ------------------
function Get-Verdicts([string]$Name, [int]$Seed) {
    $log = Invoke-Game $Name @('--anim-lab', '--chapter=C1', '--mission=M05', '--play-anim=random_prop',
        "--seed=$Seed", '--debug-anim', '--no-pads', '--mute', '--frames=60',
        "--screenshot=$(Join-Path $OutDir "$Name.png")")
    return (Get-Content -Encoding UTF8 $log | Where-Object { $_ -match 'RandomWeight' } |
        ForEach-Object { ($_ -split ' ')[-1] }) -join ' '
}
$s1a = Get-Verdicts 'seed1a' 1
$s1b = Get-Verdicts 'seed1b' 1
$s3 = Get-Verdicts 'seed3' 3
Check 'seed-pinned' ($s1a -eq $s1b) "seed 1 twice: '$s1a'"
Check 'seed-live' ($s1a -ne $s3) "seed 1 '$s1a' vs seed 3 '$s3' (if equal, try other seeds before concluding — 3 rolls at weight 0.125 collide easily)"

# ---- summary --------------------------------------------------------------------------------
Write-Host ''
if ($failures.Count -eq 0) {
    Write-Host "ALL CHECKS PASSED (outputs in $OutDir — ephemeral, do not commit)"
    exit 0
} else {
    Write-Host ("FAILED: " + ($failures -join ', '))
    exit 1
}
