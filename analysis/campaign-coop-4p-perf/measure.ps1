<#
PLAN-campaign-coop D33: the 4P cost of a heavy campaign mission.

Runs --perf over CM18 (C4/M03, "Deceit at Devil's Horn", seq 17 -- chosen by roster_census.py,
see FINDINGS.md) at 1 and 4 players, with and without cockpit view, following the same protocol
docs/tooling.md's perf stage uses: 3 launches per config, drop the first launch (cold-cache
warmup, PERF-7) and each kept launch's first perf window (shader compile), then report the
median of the remaining windows.

Needs a real campaign profile on disk (a scripted --campaign= entry with no profile "flies
without a mission" -- CampaignDirector.TryCreate, no roster/objectives/generators at all). This
script writes one to user://Profiles/d33-perf/ (CSVM's own userdata dir), missionsCompleted=17
so CM18 (seq 17) is reachable; it does not touch any of the user's own profiles.

Run from the repo root with CSVM_DATA_ROOT set to the primary tree in a worktree:
    $env:CSVM_DATA_ROOT = "Z:\CSVM"; .\analysis\campaign-coop-4p-perf\measure.ps1
#>

$ErrorActionPreference = "Continue"  # native stderr lines must not abort the run (PS 5.1 NativeCommandError)
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$scratch = Join-Path $repoRoot ".scratch\d33-perf"
New-Item -ItemType Directory -Force -Path $scratch | Out-Null

$dataRoot = if ($env:CSVM_DATA_ROOT) { $env:CSVM_DATA_ROOT } else { $repoRoot }
$godot = Join-Path $repoRoot "tools\godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe"
if (-not (Test-Path $godot)) { $godot = Join-Path $dataRoot "tools\godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe" }
if (-not (Test-Path $godot)) { throw "no Godot exe at $godot -- set CSVM_DATA_ROOT to the primary tree" }

# --- the profile the CM18 launch needs (CampaignDirector.TryCreate warns and flies without a
# mission -- no roster, no objectives, no generators -- when the named profile does not exist) ---
$profileDir = Join-Path $env:APPDATA "Godot\app_userdata\CSVM\Profiles\d33-perf"
New-Item -ItemType Directory -Force -Path $profileDir | Out-Null
@'
{
  "version": 2,
  "name": "d33-perf",
  "funds": 0,
  "selectedPlane": 0,
  "wingmanPlane": 1,
  "missionsCompleted": 17,
  "planes": [
    { "name": "Gypsy Magic", "airframe": 5, "ammo": [0,0,0,0], "ordnance": [0,0,0,0,0,0,0,0], "special": false },
    { "name": "The Knave", "airframe": 5, "ammo": [0,0,0,0], "ordnance": [0,0,0,0,0,0,0,0], "special": false }
  ],
  "grantedAircraft": [],
  "missionResults": []
}
'@ | Set-Content -Path (Join-Path $profileDir "profile.json") -Encoding utf8

$configs = @(
    @{ name = "1p-external"; players = 1; extra = @() },
    @{ name = "4p-external"; players = 4; extra = @() },
    @{ name = "1p-cockpit";  players = 1; extra = @("--view=cockpit") },
    @{ name = "4p-cockpit";  players = 4; extra = @("--view=cockpit") }
)

$frames = 600
$launchesPerConfig = 3

function Get-PerfWindows($logPath) {
    $windows = @()
    foreach ($line in Get-Content $logPath) {
        if ($line -match '\[perf\] window (.+)$') {
            $fields = @{}
            foreach ($tok in ($matches[1] -split '\s+')) {
                if ($tok -match '^([a-zA-Z0-9_]+)=(.+)$') { $fields[$matches[1]] = $matches[2] }
            }
            $windows += $fields
        }
    }
    return $windows
}

function Get-Median($nums) {
    $sorted = $nums | Sort-Object
    $n = $sorted.Count
    if ($n -eq 0) { return $null }
    if ($n % 2 -eq 1) { return $sorted[[int](($n - 1) / 2)] }
    return ($sorted[$n / 2 - 1] + $sorted[$n / 2]) / 2.0
}

$results = @{}

foreach ($cfg in $configs) {
    $planeArg = if ($cfg.players -eq 1) { "--plane=player_bhawk" } else {
        "--plane=" + (@("player_bhawk") * $cfg.players -join ",")
    }
    $allWindows = @()
    $hitchTotal = 0
    for ($i = 0; $i -lt $launchesPerConfig; $i++) {
        $shot = Join-Path $scratch "$($cfg.name)-$i.png"
        $args = @(
            "--campaign=d33-perf:17", "--players=$($cfg.players)", $planeArg,
            "--det", "--perf", "--no-vsync", "--mute", "--frames=$frames", "--screenshot=$shot"
        ) + $cfg.extra
        $logOut = Join-Path $scratch "$($cfg.name)-$i.out.log"
        Write-Host "[$($cfg.name) $i/$launchesPerConfig] $godot -- $($args -join ' ')"
        & $godot --path (Join-Path $repoRoot "CSVM") -- @args *> $logOut
        if ($i -eq 0) { continue }  # cold-cache warmup launch, discarded (PERF-7)

        $windows = Get-PerfWindows $logOut
        if ($windows.Count -gt 1) { $windows = $windows[1..($windows.Count - 1)] }  # drop first window (shader compile)
        $allWindows += $windows

        $logLine = Get-Content $logOut | Where-Object { $_ -match '\[core\] log file=(.+?) mode=' } | Select-Object -First 1
        if ($logLine -match '\[core\] log file=(.+?) mode=') {
            $hitchPath = [System.IO.Path]::ChangeExtension($matches[1], $null).TrimEnd('.') + ".hitches.jsonl"
            if (Test-Path $hitchPath) {
                $lines = Get-Content $hitchPath | Where-Object { $_.Trim() -ne "" }
                $hitchTotal += $lines.Count
            }
        }
    }

    $metrics = @{}
    foreach ($m in @("render_cpu_ms", "gpu_ms", "draws", "prims", "nodes", "frame_ms", "script_ms", "mem_mb", "max_ms", "p95_ms")) {
        $vals = $allWindows | ForEach-Object { [double]$_[$m] } | Where-Object { $_ -ne $null }
        $metrics[$m] = Get-Median $vals
    }
    $metrics["hitch_count_natural"] = $hitchTotal
    $metrics["windows"] = $allWindows.Count
    $results[$cfg.name] = $metrics
}

Write-Host "`n=== D33 4P perf census: CM18 (C4/M03), $frames sim frames x $launchesPerConfig launches (first dropped) ===`n"
"{0,-14}{1,10}{2,10}{3,9}{4,9}{5,10}{6,9}{7,9}" -f "config", "render_cpu", "gpu_ms", "draws", "nodes", "frame_ms", "mem_mb", "hitches" | Write-Host
foreach ($name in $configs.name) {
    $m = $results[$name]
    "{0,-14}{1,10:N3}{2,10:N3}{3,9:N0}{4,9:N0}{5,10:N3}{6,9:N1}{7,9}" -f $name, $m.render_cpu_ms, $m.gpu_ms, $m.draws, $m.nodes, $m.frame_ms, $m.mem_mb, $m.hitch_count_natural | Write-Host
}

$results | ConvertTo-Json -Depth 5 | Set-Content -Path (Join-Path $scratch "results.json") -Encoding utf8
Write-Host "`nRaw logs and results.json under $scratch"
