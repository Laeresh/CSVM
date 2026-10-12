<#
CM23 (C5/M03, seq 22) frame cost on this PC, split by player count, graphics mode and how the
second player joins: 1P, 2P splitscreen, and 2P network co-op as host over loopback with a
headless guest (so the guest draws nothing on the GPU the host is measured on).

Every launch is wall clock (--no-det, so the saved display options apply: resolution, TAA, shadow
quality), --perf --no-vsync --mute, and quits on a --screenshot at sim frame -Frames. Every launch
reads and writes only the profile store this script seeds under .scratch\cm23-host-perf\. Every
launch goes through RunProbe.ps1, on the hidden desktop or headless.

    $env:CSVM_DATA_ROOT = "Z:\CSVM"; .\analysis\cm23-host-perf\measure.ps1 -Cases 1p-enh,net-enh
#>
param(
    [string[]]$Cases = @("1p-enh", "1p-orig", "net-enh", "net-orig", "split-enh", "split-orig"),
    [int]$Launches = 3,
    [int]$Frames = 7200,
    [string]$Resolution = "5120x1440",
    [int]$Port = 48960,
    # Every seat's scripted input; an empty value flies hands-off. -Tag prefixes the log names.
    [string]$Hold = "0.05,0,0,1",
    [string]$Tag = "",
    [string[]]$Extra = @()
)

$ErrorActionPreference = "Continue"  # native stderr lines must not abort the run (PS 5.1)
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$scratch = Join-Path $repoRoot ".scratch\cm23-host-perf"
$probe = Join-Path $repoRoot "RunProbe.ps1"

# One store for the host, one for the guest, so two processes never save into the same folder.
function New-Store($name) {
    $dir = Join-Path $scratch "$name\cm23"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $json = '{ "version": 3, "name": "cm23", "funds": 0, "selectedPlane": 0, "wingmanPlane": 1, ' +
        '"missionsCompleted": 22, "planes": [ ' +
        '{ "name": "Gypsy Magic", "airframe": 5, "ammo": [0,0,0,0], "ordnance": [0,0,0,0,0,0,0,0], "special": false }, ' +
        '{ "name": "The Knave", "airframe": 5, "ammo": [0,0,0,0], "ordnance": [0,0,0,0,0,0,0,0], "special": false } ], ' +
        '"grantedAircraft": [], "missionResults": [] }'
    [IO.File]::WriteAllText((Join-Path $dir "profile.json"), $json, (New-Object Text.UTF8Encoding($false)))
    return (Join-Path $scratch $name)
}
$hostStore = New-Store "profiles-host"
$guestStore = New-Store "profiles-guest"
$hold = if ($Hold) { @("--hold=$Hold") } else { @() }

foreach ($case in $Cases) {
    $mode = if ($case -like "*-enh") { "enhanced" } else { "original" }
    for ($i = 0; $i -lt $Launches; $i++) {
        $base = Join-Path $scratch "$Tag$case-$i"
        $common = @("--campaign=cm23:22", "--graphics=$mode", "--no-det", "--perf", "--no-vsync", "--mute",
            "--frames=$Frames", "--screenshot=$base.png") + $hold + $Extra
        $hostArgs = @("--profiles=$hostStore") + $common
        $job = $null
        if ($case -like "split-*") {
            $hostArgs += @("--players=2", "--plane=player_bhawk,player_bhawk")
        } elseif ($case -like "net-*") {
            $hostArgs += @("--net-port-base=$Port", "--net-host=127.0.0.1:$Port")
            $guestArgs = @("--profiles=$guestStore", "--campaign=cm23:22", "--graphics=$mode", "--no-det", "--mute",
                "--net-port-base=$Port", "--net-join=127.0.0.1:$Port") + $hold
            # The guest renders nothing, so the host's GPU and render thread are its own.
            $job = Start-Job -ScriptBlock {
                param($p, $root, $logBase, $a)
                $env:CSVM_DATA_ROOT = $root
                & $p -TimeoutSec 900 -EngineArgs '--headless', '--log-file', $logBase @a
            } -ArgumentList $probe, $env:CSVM_DATA_ROOT, "$base-guest", $guestArgs
            # The host's socket opens at launch; the guest retries for 30 s, so a head start is enough.
        }
        Write-Host "[$case $i] $($hostArgs -join ' ')"
        & $probe -TimeoutSec 900 -Resolution $Resolution -EngineArgs '--log-file', $base @hostArgs | Out-Null
        if ($job) {
            # A headless guest cannot take a --screenshot and outlives its host, so it is killed
            # here, before the next host launches beside it.
            Get-CimInstance Win32_Process -Filter "Name like 'Godot%'" |
                Where-Object { $_.CommandLine -like "*$guestStore*" } |
                ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
            Stop-Job $job
            Remove-Job $job -Force
        }
    }
}
Write-Host "Logs under $scratch; summarise.ps1 reads them."
