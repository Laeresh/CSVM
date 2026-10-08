<#
PLAN-campaign-coop D33: the 4P cost of a heavy campaign mission.

Runs --perf over CM18 (C4/M03, "Deceit at Devil's Horn", seq 17 -- chosen by roster_census.py,
see FINDINGS.md) at each player count in -Players, with and without cockpit view, following the
same protocol docs/tooling.md's perf stage uses: -Launches launches per config, drop the first
launch (cold-cache warmup, PERF-7) and each kept launch's first perf window (shader compile),
then report the median of the remaining windows.

Needs a real campaign profile (a scripted --campaign= entry with no profile "flies without a
mission" -- CampaignDirector.TryCreate, no roster/objectives/generators at all). This script
seeds one in a store under .scratch\d33-perf\profiles and points every launch at it with
--profiles=, missionsCompleted=17 so CM18 (seq 17) is reachable; it never reads or writes
user://Profiles. Every launch goes through RunProbe.ps1, so no window reaches the screen.

Run from the repo root with CSVM_DATA_ROOT set to the primary tree in a worktree:
    $env:CSVM_DATA_ROOT = "Z:\CSVM"; .\analysis\campaign-coop-4p-perf\measure.ps1
    .\analysis\campaign-coop-4p-perf\measure.ps1 -Players 1,2,3,4 -Launches 2
#>
param(
    [int[]]$Players = @(1, 4),
    [int]$Launches = 3,
    [int]$Frames = 600
)

$ErrorActionPreference = "Continue"  # native stderr lines must not abort the run (PS 5.1 NativeCommandError)
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$scratch = Join-Path $repoRoot ".scratch\d33-perf"
New-Item -ItemType Directory -Force -Path $scratch | Out-Null
$probe = Join-Path $repoRoot "RunProbe.ps1"

# --- the profile the CM18 launch needs, in a store of this run's own ---
$store = Join-Path $scratch "profiles"
$profileDir = Join-Path $store "d33-perf"
New-Item -ItemType Directory -Force -Path $profileDir | Out-Null
$profileJson = @'
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
'@
[System.IO.File]::WriteAllText((Join-Path $profileDir "profile.json"), $profileJson, (New-Object System.Text.UTF8Encoding($false)))

$configs = @()
foreach ($view in @("external", "cockpit")) {
    foreach ($n in $Players) {
        $extra = if ($view -eq "cockpit") { @("--view=cockpit") } else { @() }
        $configs += @{ name = "${n}p-$view"; players = $n; extra = $extra }
    }
}

function Get-PerfWindows($logPath) {
    $windows = @()
    if (-not (Test-Path $logPath)) { return $windows }
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
    $planeArg = "--plane=" + (@("player_bhawk") * $cfg.players -join ",")
    $allWindows = @()
    $hitchTotal = 0
    for ($i = 0; $i -lt $Launches; $i++) {
        $shot = Join-Path $scratch "$($cfg.name)-$i.png"
        $launchArgs = @(
            "--campaign=d33-perf:17", "--profiles=$store", "--players=$($cfg.players)", $planeArg,
            "--det", "--perf", "--no-vsync", "--mute", "--frames=$Frames", "--screenshot=$shot"
        ) + $cfg.extra
        $logBase = Join-Path $scratch "$($cfg.name)-$i"
        Write-Host "[$($cfg.name) $i/$Launches] $($launchArgs -join ' ')"
        & $probe -TimeoutSec 600 -EngineArgs '--log-file', $logBase @launchArgs | Out-Null
        if ($Launches -gt 1 -and $i -eq 0) { continue }  # cold-cache warmup launch, discarded (PERF-7)

        $logOut = "$logBase.out"
        $windows = Get-PerfWindows $logOut
        if ($windows.Count -gt 1) { $windows = $windows[1..($windows.Count - 1)] }  # drop first window (shader compile)
        $allWindows += $windows

        $logLine = Get-Content $logOut -ErrorAction SilentlyContinue | Where-Object { $_ -match '\[core\] log file=(.+?) mode=' } | Select-Object -First 1
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

Write-Host "`n=== D33 4P perf census: CM18 (C4/M03), $Frames sim frames x $Launches launches ===`n"
"{0,-14}{1,10}{2,10}{3,9}{4,9}{5,10}{6,9}{7,9}" -f "config", "render_cpu", "gpu_ms", "draws", "nodes", "frame_ms", "mem_mb", "hitches" | Write-Host
foreach ($name in $configs.name) {
    $m = $results[$name]
    "{0,-14}{1,10:N3}{2,10:N3}{3,9:N0}{4,9:N0}{5,10:N3}{6,9:N1}{7,9}" -f $name, $m.render_cpu_ms, $m.gpu_ms, $m.draws, $m.nodes, $m.frame_ms, $m.mem_mb, $m.hitch_count_natural | Write-Host
}

$results | ConvertTo-Json -Depth 5 | Set-Content -Path (Join-Path $scratch "results.json") -Encoding utf8
Write-Host "`nRaw logs and results.json under $scratch"
